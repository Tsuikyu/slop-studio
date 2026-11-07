using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Text;
using Microsoft.Win32;
namespace SlopStudio
{
 public sealed class AsioDriverChoice{public string Name,Clsid;public override string ToString(){return Name;}}
 public static class AsioDrivers
 {
  public static List<AsioDriverChoice> Enumerate(){var result=new List<AsioDriverChoice>();using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))using(var key=root.OpenSubKey(@"SOFTWARE\ASIO")){if(key!=null)foreach(var name in key.GetSubKeyNames())using(var d=key.OpenSubKey(name)){string id=Convert.ToString(d.GetValue("CLSID"));Guid parsed;if(Guid.TryParse(id,out parsed))result.Add(new AsioDriverChoice{Name=Convert.ToString(d.GetValue("Description",name)),Clsid=id});}}return result;}
  public static void ShowControlPanel(string clsid){Guid id;if(!Guid.TryParse(clsid,out id))throw new InvalidDataException("Select a valid ASIO driver.");using(var ipc=new CompanionMemory("--asio-panel {key} "+id.ToString("B"))){if(!ipc.Process.WaitForExit(600000))throw new TimeoutException("The ASIO control panel did not close.");if(ipc.View.ReadInt32(0)<0){byte[] error=new byte[1900];ipc.View.ReadArray(128,error,0,error.Length);throw new InvalidOperationException(Encoding.UTF8.GetString(error).TrimEnd('\0'));}}}
 }
 internal sealed class CompanionMemory:IDisposable
 {
  public MemoryMappedFile Memory;public MemoryMappedViewAccessor View;public EventWaitHandle Request,Response,Ready;public Process Process;public string Key;
  public CompanionMemory(string arguments){Key="SlopBridge-"+Guid.NewGuid().ToString("N");Memory=MemoryMappedFile.CreateNew(Key,2*1024*1024);View=Memory.CreateViewAccessor();View.Write(80,System.Diagnostics.Process.GetCurrentProcess().Id);Request=new EventWaitHandle(false,EventResetMode.AutoReset,Key+"-request");Response=new EventWaitHandle(false,EventResetMode.AutoReset,Key+"-response");Ready=new EventWaitHandle(false,EventResetMode.ManualReset,Key+"-ready");string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Companions","SlopBridge.exe");try{var start=new ProcessStartInfo(exe,arguments.Replace("{key}",Key)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WindowStyle=ProcessWindowStyle.Hidden};Process=System.Diagnostics.Process.Start(start);Process.BeginOutputReadLine();Process.BeginErrorReadLine();if(!Ready.WaitOne(12000)||View.ReadInt32(0)!=1){byte[] error=new byte[1900];View.ReadArray(128,error,0,error.Length);throw new InvalidOperationException(Encoding.UTF8.GetString(error).TrimEnd('\0')+" (companion startup)");}}catch{Dispose();throw;}}
  public void Dispose(){if(View!=null)View.Write(28,1);try{if(Process!=null&&!Process.WaitForExit(3000))Process.Kill();}catch{}if(Process!=null)Process.Dispose();if(View!=null)View.Dispose();if(Memory!=null)Memory.Dispose();if(Request!=null)Request.Dispose();if(Response!=null)Response.Dispose();if(Ready!=null)Ready.Dispose();}
 }
 public sealed class AsioCompanion:IAudioOutput
 {
  private CompanionMemory _ipc;private Thread _thread;private volatile bool _stopping;private Action<Exception> _failed;public Exception Error{get;private set;}
  public event Action<Exception> Failed{add{_failed+=value;if(Error!=null)value(Error);}remove{_failed-=value;}}
  private long SubmittedFrames{get{return _ipc==null?0:_ipc.View.ReadInt64(32);}}public long FramesPlayed{get{return Math.Max(0,SubmittedFrames-Math.Max(0,OutputLatency));}}public int InputChannels{get;private set;}public int InputLatency{get;private set;}public int OutputLatency{get;private set;}public int Underruns{get{return _ipc==null?0:_ipc.View.ReadInt32(48);}}
  public AsioCompanion(AppSettings settings,Action<float[],int,long> render,Action<float[],int,int,long> input){
   if(String.IsNullOrWhiteSpace(settings.AsioDriver))throw new InvalidOperationException("Select an installed 64-bit ASIO driver.");
   _ipc=new CompanionMemory("--asio {key} "+settings.AsioDriver+" "+settings.SampleRate+" "+settings.BufferFrames);InputChannels=_ipc.View.ReadInt32(4);InputLatency=_ipc.View.ReadInt32(20);OutputLatency=_ipc.View.ReadInt32(24);int block=_ipc.View.ReadInt32(12);
   _thread=new Thread(delegate(){float[] capture=new float[block*InputChannels],output=new float[block*2];var activity=Stopwatch.StartNew();try{while(!_stopping){
    bool requested=_ipc.Request.WaitOne(100);if(_stopping)break;
    if(_ipc.Process.HasExited||_ipc.View.ReadInt32(0)<0)throw new IOException("ASIO companion exited. Press Play to reconnect the device.");
    int reset=_ipc.View.ReadInt32(52);if(reset!=0)throw new IOException(reset==2?"ASIO sample rate changed. Reapply Audio Settings.":"ASIO driver requested a reset. Press Play to reconnect the device.");
    if(!requested){if(activity.ElapsedMilliseconds>3000)throw new IOException("ASIO stopped sending audio buffers. Press Play to reconnect the device.");continue;}
    activity.Restart();int sequence=_ipc.View.ReadInt32(88);long position=SubmittedFrames;
    if(InputChannels>0){_ipc.View.ReadArray(4096,capture,0,capture.Length);if(input!=null)input(capture,block,InputChannels,position);}
    Array.Clear(output,0,output.Length);render(output,block,position);
    // A timed-out request must never acknowledge a newer input buffer.
    if(sequence==_ipc.View.ReadInt32(88)){_ipc.View.WriteArray(4096+8192*32*4,output,0,output.Length);Thread.MemoryBarrier();_ipc.View.Write(92,sequence);_ipc.Response.Set();}
   }}catch(Exception ex){if(!_stopping){Error=ex;var handler=_failed;if(handler!=null)handler(ex);}}}){IsBackground=true,Name="ASIO IPC audio",Priority=ThreadPriority.AboveNormal};_thread.Start();
  }
  public void ControlPanel(){if(_ipc!=null)_ipc.View.Write(44,1);}
  public void Dispose(){_stopping=true;if(_ipc!=null){_ipc.Request.Set();if(_thread!=null&&!_thread.Join(3000))throw new TimeoutException("ASIO processing did not stop.");_ipc.Dispose();_ipc=null;}}
 }
 public sealed class LinkCompanion:IDisposable
 {
  private CompanionMemory _ipc;
  public LinkCompanion(){_ipc=new CompanionMemory("--link {key}");}
  public int Peers{get{return _ipc.View.ReadInt32(4);}}public double Tempo{get{return _ipc.View.ReadDouble(8);}}public double Beat{get{return _ipc.View.ReadDouble(16);}}public bool Playing{get{return _ipc.View.ReadInt32(40)!=0;}}
  public void Commit(double tempo,bool playing){_ipc.View.Write(64,tempo);_ipc.View.Write(72,playing?1:0);_ipc.View.Write(44,1);}
  public void Dispose(){if(_ipc!=null){_ipc.Dispose();_ipc=null;}}
 }
}
