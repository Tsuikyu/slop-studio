using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SlopProX
{
    // No vendor module is loaded in this process. One x64 worker owns one instance.
    public sealed partial class NativePlugin : IDisposable
    {
        public struct Parameter { public uint Id; public byte[] NameBytes; public string Name {get{return Encoding.UTF8.GetString(NameBytes??new byte[0]).TrimEnd('\0');}} public double Value,Minimum,Maximum;public int Steps; }
        private Process _worker;private EventWaitHandle _controlRequest,_controlResponse;
        private MemoryMappedFile _memory;private MemoryMappedViewAccessor _view;
        private EventWaitHandle _request,_response;private readonly object _control=new object(),_dsp=new object();
        internal struct QueuedParameter{public uint Id;public double Value;public int Offset;}private List<QueuedParameter> _parameters=new List<QueuedParameter>();
        public void QueueParameter(uint id,double value,int offset){lock(_parameterSync){if(_parameters.Count>=240)throw new InvalidOperationException("Too many parameter events in one audio block.");_parameters.Add(new QueuedParameter{Id=id,Value=value,Offset=offset});}}
        private object _parameterSync=new object();private RealtimePluginPump _realtime;private int _sampleRate,_latency;private bool _disposed,_failed;private int _processed;public PluginSlot Slot{get;private set;}
        public int WorkerId {get{return _worker==null?0:_worker.Id;}} public bool Healthy{get{return !_disposed&&!_failed&&_worker!=null&&!_worker.HasExited;}}
        public string Failure {get;private set;} public int LatencySamples{get{if(_view!=null&&!_disposed)try{int value=_view.ReadInt32(52);if(value>=0&&value<=_sampleRate*10)_latency=value;}catch{}return _latency+(_realtime==null?0:_realtime.Delay);}}public long RealtimeMissedBlocks{get{return _realtime==null?0:_realtime.Missed+_realtime.Expired;}}
        internal const int AudioOffset=4096,EventOffset=AudioOffset+8192*2*4,MemorySize=EventOffset+4096*16+8*1024*1024;
        internal const int ControlOffset=EventOffset+4096*16;
        public static string ResolveBinary(string path){return LocalPlugin.ResolveBinary(path);}
        public static void ValidateArchitecture(string path){LocalPlugin.ValidateArchitecture(path);}
        public static List<PluginDescriptor> Scan(string path){return PluginCatalog.ScanIsolated(path);}
        public NativePlugin(PluginSlot slot,int sampleRate,int bufferFrames)
        {
            Slot=slot;_sampleRate=sampleRate;ValidateArchitecture(ResolveBinary(slot.Path));
            string key="SlopPlugin-"+Guid.NewGuid().ToString("N");
            try{
                _memory=MemoryMappedFile.CreateNew(key,MemorySize);_view=_memory.CreateViewAccessor();_view.Write(48,System.Diagnostics.Process.GetCurrentProcess().Id);
                _request=new EventWaitHandle(false,EventResetMode.AutoReset,key+"-request");
                _response=new EventWaitHandle(false,EventResetMode.AutoReset,key+"-response");
                _controlRequest=new EventWaitHandle(false,EventResetMode.AutoReset,key+"-control-request");_controlResponse=new EventWaitHandle(false,EventResetMode.AutoReset,key+"-control-response");
                ProcessStartInfo start=new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SlopStudio.exe"),"--plugin-worker "+key){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WindowStyle=ProcessWindowStyle.Hidden};
                _worker=System.Diagnostics.Process.Start(start);_worker.BeginOutputReadLine();_worker.BeginErrorReadLine();
                if(!_controlResponse.WaitOne(15000))throw new TimeoutException("The plug-in worker did not start.");
                Rpc("create",new object[]{slot,sampleRate,bufferFrames},15000);
                _latency=Convert.ToInt32(Rpc("latency",null,3000));_view.Write(52,_latency);if(LatencySamples<0||LatencySamples>sampleRate*10)throw new InvalidDataException("Plug-in latency must be between zero and ten seconds.");
            }catch(Exception ex){Fail(ex.Message);Dispose();throw new InvalidOperationException(slot.Name+": "+ex.Message,ex);}
        }
        private object Rpc(string operation,object argument,int timeout)
        {
            lock(_control){if(_disposed||_failed)throw new InvalidOperationException(Failure??"Plug-in worker is closed.");
                IDictionary<string,object> result;
                try{
                    SandboxMemory.Write(_view,new Dictionary<string,object>{{"op",operation},{"arg",argument}});_controlRequest.Set();
                    if(!_controlResponse.WaitOne(timeout))throw new TimeoutException("Worker control timed out.");
                    result=SandboxMemory.Read(_view);
                }catch(Exception ex){Fail(operation+": "+ex.Message);throw;}
                // A rejected preset or unsupported operation is not itself a worker crash.
                if(result.ContainsKey("error"))throw new InvalidOperationException(Convert.ToString(result["error"]));
                return result.ContainsKey("result")?result["result"]:null;
            }
        }
        private void Fail(string reason){_failed=true;Failure=Slot.Name+" worker failed: "+reason;try{if(_worker!=null&&!_worker.HasExited)_worker.Kill();}catch{}}
        private QueuedParameter[] TakeParameters(){lock(_parameterSync){var p=_parameters.ToArray();_parameters.Clear();return p;}}
        public void EnableRealtime(int block){if(_realtime!=null)return;_realtime=new RealtimePluginPump(block,delegate(PluginAudioJob job){ProcessBlocking(job.Audio,job.Frames,job.Events,job.Count,job.Tempo,job.Beat,job.Playing,job.Numerator,job.Denominator,job.Parameters);});}
        public void Process(float[] audio,int frames,MidiEventPacket[] events,int count,double tempo,double beat,bool playing,int numerator,int denominator){var parameters=TakeParameters();if(_realtime!=null){_realtime.Process(audio,frames,events,count,parameters,tempo,beat,playing,numerator,denominator);return;}ProcessBlocking(audio,frames,events,count,tempo,beat,playing,numerator,denominator,parameters);}
        private void ProcessBlocking(float[] audio,int frames,MidiEventPacket[] events,int count,double tempo,double beat,bool playing,int numerator,int denominator,QueuedParameter[] parameters)
        {
            lock(_dsp){
                if(!Healthy){Array.Clear(audio,0,frames*2);throw new InvalidOperationException(Failure??"Plug-in worker exited: "+Slot.Name);}
                if(frames<1||frames>8192||count>4096)throw new ArgumentOutOfRangeException("frames");
                _view.Write(0,frames);_view.Write(4,count);_view.Write(8,tempo);_view.Write(16,beat);_view.Write(24,playing?1:0);_view.Write(28,numerator);_view.Write(32,denominator);_view.Write(36,0);
                _view.Write(40,parameters.Length);for(int i=0;i<parameters.Length;i++){var p=parameters[i];_view.Write(64+i*16,p.Offset);_view.Write(68+i*16,p.Id);_view.Write(72+i*16,p.Value);}
                _view.WriteArray(AudioOffset,audio,0,frames*2);
                for(int i=0;i<count;i++){int offset=EventOffset+i*16;_view.Write(offset,events[i].Offset);_view.Write(offset+4,events[i].Status);_view.Write(offset+8,events[i].Data1);_view.Write(offset+12,events[i].Data2);}
                _request.Set();
                // First process call may initialize vendor resources. Subsequent hangs are bounded.
                int timeout=_processed++==0?5000:250;
                if(!_response.WaitOne(timeout)||_worker.HasExited){Array.Clear(audio,0,frames*2);Fail("DSP exited or exceeded "+timeout+" ms.");throw new InvalidOperationException(Failure);}
                if(_view.ReadInt32(36)!=0){Array.Clear(audio,0,frames*2);Fail("DSP rejected the block.");throw new InvalidOperationException(Failure);}
                _view.ReadArray(AudioOffset,audio,0,frames*2);for(int i=0;i<frames*2;i++)if(Single.IsNaN(audio[i])||Single.IsInfinity(audio[i])){Array.Clear(audio,0,frames*2);Fail("DSP emitted non-finite audio.");throw new InvalidOperationException(Failure);}
            }
        }
        public void BindSlot(PluginSlot slot){Slot=slot;}
        public void RestoreState(string state){Rpc("restore",state,10000);}
        public string SnapshotState(){return Convert.ToString(Rpc("state",null,10000));}
        public string SaveState(){string state=SnapshotState();Slot.State=state;if(Slot.UseAra)Slot.AraState=Convert.ToString(Rpc("ara-state",null,15000));return state;}
        public List<Parameter> Parameters(){
            object raw=Rpc("parameters",null,5000);var serializer=new JavaScriptSerializer();var source=serializer.ConvertToType<List<SandboxParameter>>(raw);var result=new List<Parameter>();
            foreach(var p in source)result.Add(new Parameter{Id=p.Id,NameBytes=Encoding.UTF8.GetBytes(p.Name),Value=p.Value,Minimum=p.Minimum,Maximum=p.Maximum,Steps=p.Steps});return result;
        }
        public void SetParameter(uint id,double value){Rpc("set",new object[]{id,value},5000);}
        public double GetParameter(uint id){return Convert.ToDouble(Rpc("get",id,5000));}
        public bool OpenEditor(IntPtr parent,out int width,out int height){var values=(System.Collections.IList)Rpc("editor",parent.ToInt64(),10000);width=Convert.ToInt32(values[1]);height=Convert.ToInt32(values[2]);return Convert.ToBoolean(values[0]);}
        public void CloseEditor(){if(Healthy)Rpc("close-editor",null,3000);}
        public void Idle(){/* The worker STA owns its own idle timer. */}
        public void TerminateWorkerForTest(){if(_worker!=null&&!_worker.HasExited)_worker.Kill();}
        public void Dispose(){
            if(_disposed)return;
            if(_realtime!=null){_realtime.Dispose();_realtime=null;}
            try{if(Healthy)Rpc("dispose",null,3000);}catch{}
            _disposed=true;try{if(_worker!=null&&!_worker.WaitForExit(1000))_worker.Kill();}catch{}
            if(_controlRequest!=null)_controlRequest.Dispose();if(_controlResponse!=null)_controlResponse.Dispose();if(_view!=null)_view.Dispose();if(_memory!=null)_memory.Dispose();if(_request!=null)_request.Dispose();if(_response!=null)_response.Dispose();if(_worker!=null)_worker.Dispose();
        }
    }
    internal sealed class SandboxParameter{public uint Id;public string Name;public double Value,Minimum,Maximum;public int Steps;}
    internal static class SandboxMemory
    {
        public static void Write(MemoryMappedViewAccessor view,object value){byte[] bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer{MaxJsonLength=8*1024*1024-16}.Serialize(value));if(bytes.Length>8*1024*1024-16)throw new InvalidDataException("IPC message too large.");view.Write(NativePlugin.ControlOffset,bytes.Length);view.WriteArray(NativePlugin.ControlOffset+4,bytes,0,bytes.Length);}
        public static IDictionary<string,object> Read(MemoryMappedViewAccessor view){int size=view.ReadInt32(NativePlugin.ControlOffset);if(size<2||size>8*1024*1024-16)throw new InvalidDataException("Invalid IPC frame.");byte[] b=new byte[size];view.ReadArray(NativePlugin.ControlOffset+4,b,0,size);return (IDictionary<string,object>)new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.DeserializeObject(Encoding.UTF8.GetString(b));}
    }
    internal static class PluginWorker
    {
        public static void Run(string key)
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var memory=MemoryMappedFile.OpenExisting(key))
            using(var view=memory.CreateViewAccessor())
            using(var request=EventWaitHandle.OpenExisting(key+"-request"))
            using(var response=EventWaitHandle.OpenExisting(key+"-response"))
            using(var controlRequest=EventWaitHandle.OpenExisting(key+"-control-request"))
            using(var controlResponse=EventWaitHandle.OpenExisting(key+"-control-response"))
            using(var owner=new Form{ShowInTaskbar=false,Opacity=0,Width=1,Height=1})
            {
                LocalPlugin local=null;bool running=true;IntPtr handle=owner.Handle;int parentId=view.ReadInt32(48);new Thread(delegate(){try{using(var parent=System.Diagnostics.Process.GetProcessById(parentId))parent.WaitForExit();}catch{}Environment.Exit(0);}){IsBackground=true,Name="Parent watchdog"}.Start();
                var dsp=new Thread(delegate(){
                    float[] audio=new float[8192*2];var events=new MidiEventPacket[4096];
                    try{while(running){if(!request.WaitOne(100))continue;if(!running)break;int frames=view.ReadInt32(0),count=view.ReadInt32(4);try{if(frames<1||frames>8192||count<0||count>4096||local==null)throw new InvalidDataException("Invalid DSP request.");view.ReadArray(NativePlugin.AudioOffset,audio,0,frames*2);for(int i=0;i<count;i++){int o=NativePlugin.EventOffset+i*16;events[i]=new MidiEventPacket(view.ReadInt32(o),view.ReadInt32(o+4),view.ReadInt32(o+8),view.ReadInt32(o+12));}int parameters=view.ReadInt32(40);if(parameters<0||parameters>240)throw new InvalidDataException("Invalid parameter count.");for(int i=0;i<parameters;i++)local.QueueParameter(view.ReadUInt32(68+i*16),view.ReadDouble(72+i*16),view.ReadInt32(64+i*16));local.Process(audio,frames,events,count,view.ReadDouble(8),view.ReadDouble(16),view.ReadInt32(24)!=0,view.ReadInt32(28),view.ReadInt32(32));view.WriteArray(NativePlugin.AudioOffset,audio,0,frames*2);view.Write(36,0);}catch{view.Write(36,-1);}response.Set();}}catch{Environment.Exit(72);}
                }){IsBackground=true,Name="Plug-in DSP"};dsp.Start();
                var timer=new System.Windows.Forms.Timer{Interval=10};
                timer.Tick+=delegate{
                    if(controlRequest.WaitOne(0)){
                        var input=SandboxMemory.Read(view);object result=null;Exception failure=null;string op=Convert.ToString(input["op"]);object arg=input["arg"];
                        try{var serializer=new JavaScriptSerializer();var list=arg as System.Collections.IList;
                            switch(op){
                            case "create":local=new LocalPlugin(serializer.ConvertToType<PluginSlot>(list[0]),Convert.ToInt32(list[1]),Convert.ToInt32(list[2]));break;
                            case "latency":result=local.LatencySamples;break;
                            case "ara-update":local.UpdateAraDocument(serializer.ConvertToType<AraDocumentInfo>(arg));break;case "ara-state":result=local.SaveAraState();break;
                            case "parameters":var parameters=new List<SandboxParameter>();foreach(var p in local.Parameters())parameters.Add(new SandboxParameter{Id=p.Id,Name=p.Name,Value=p.Value,Minimum=p.Minimum,Maximum=p.Maximum,Steps=p.Steps});result=parameters;break;
                            case "set":local.SetParameter(Convert.ToUInt32(list[0]),Convert.ToDouble(list[1]));break;
                            case "get":result=local.GetParameter(Convert.ToUInt32(arg));break;
                            case "state":result=local.SaveState();break;
                            case "restore":local.RestoreState(Convert.ToString(arg));break;
                            case "editor":int w,h;bool open=local.OpenEditor(new IntPtr(Convert.ToInt64(arg)),out w,out h);result=new object[]{open,w,h};break;
                            case "close-editor":local.CloseEditor();break;
                            case "dispose":running=false;if(local!=null){local.Dispose();local=null;}break;
                            default:throw new InvalidOperationException("Unknown worker command.");
                            }
                        }catch(Exception ex){failure=ex;}
                        SandboxMemory.Write(view,failure==null?new Dictionary<string,object>{{"result",result}}:new Dictionary<string,object>{{"error",failure.Message}});controlResponse.Set();
                        if(!running)owner.Close();
                    }
                    try{if(local!=null){local.Idle();view.Write(52,local.LatencySamples);var ara=local.TakeAraRequest();if(ara!=null){view.Write(3908,ara.Flags);view.Write(3912,ara.Position);view.Write(3920,ara.CycleStart);view.Write(3928,ara.CycleLength);view.Write(3936,ara.CycleEnabled?1:0);view.Write(3944,view.ReadInt32(3944)+1);}}}catch{}
                };timer.Start();controlResponse.Set();
                Application.Run(new ApplicationContext(owner));running=false;request.Set();dsp.Join(1000);timer.Dispose();
            }
        }
    }
}
