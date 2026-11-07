using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
namespace SlopStudio
{
 public sealed class IntegrationSettings
 {
  public bool ControllerEnabled{get;set;}public bool OscEnabled{get;set;}public int OscReceivePort{get;set;}public int OscSendPort{get;set;}public string OscHost{get;set;}public bool LinkEnabled{get;set;}public bool QuickStart{get;set;}public int RecoverySeconds{get;set;}public string[] ControllerBindings{get;set;}
  public bool LaunchpadEnabled{get;set;}public int LaunchpadInput{get;set;}public int LaunchpadOutput{get;set;}public string LaunchpadInputName{get;set;}public string LaunchpadOutputName{get;set;}
  public IntegrationSettings(){OscReceivePort=9001;OscSendPort=9000;OscHost="127.0.0.1";QuickStart=true;RecoverySeconds=30;ControllerBindings=GameControllerBindings.Default();LaunchpadInput=-1;LaunchpadOutput=-1;LaunchpadInputName="";LaunchpadOutputName="";}
  public void Validate(){if(OscReceivePort<1024||OscReceivePort>65535||OscSendPort<1024||OscSendPort>65535||RecoverySeconds<10||RecoverySeconds>600||String.IsNullOrWhiteSpace(OscHost))throw new InvalidDataException("Integration settings are invalid.");if(ControllerBindings==null)ControllerBindings=GameControllerBindings.Default();GameControllerBindings.Validate(ControllerBindings);if(LaunchpadInput < -1||LaunchpadOutput < -1||LaunchpadEnabled&&(LaunchpadInput<0||LaunchpadOutput<0||String.IsNullOrEmpty(LaunchpadInputName)||String.IsNullOrEmpty(LaunchpadOutputName)))throw new InvalidDataException("Select both Launchpad MIDI ports.");}
 }
 internal sealed class AudioInputBus
 {
  private readonly object _sync=new object();private readonly float[] _ring=new float[32768*32];private long _write,_read;private int _channels=1;
  public int Channels{get{lock(_sync)return _channels;}}
  public void Push(float[] data,int frames,int channels){lock(_sync){if(channels<1||channels>32)return;_channels=channels;for(int i=0;i<frames;i++){int pos=(int)((_write+i)%32768)*32;Array.Copy(data,i*channels,_ring,pos,channels);}_write+=frames;if(_write-_read>8192)_read=_write-8192;}}
  public float[] Read(int frames){float[] data=new float[frames*32];lock(_sync){long count=Math.Min(frames,_write-_read);for(int i=0;i<count;i++)Array.Copy(_ring,(int)((_read+i)%32768)*32,data,i*32,_channels);_read+=count;}return data;}
 }
 public sealed class RecordedTake{public Guid RegionId=Guid.NewGuid();public Guid TrackId;public string Path;public long Frames;public int Channels;}
 public sealed class AudioRecordingSession:IDisposable
 {
  private sealed class Destination{public InputRoute Route;public PcmWaveWriter Writer;public RecordedTake Take;}
  private sealed class Packet{public float[] Data;public int Frames,Channels;}
  private readonly List<Destination> _destinations=new List<Destination>();private readonly Queue<Packet> _queue=new Queue<Packet>();private readonly AutoResetEvent _signal=new AutoResetEvent(false);private readonly Thread _thread;private bool _stopping;private int _queued;public Exception Error{get;private set;}
  public AudioRecordingSession(IEnumerable<Track> tracks,StudioState studio,string folder,int rate,int availableChannels){
   Directory.CreateDirectory(folder);try{foreach(var t in tracks){InputRoute route=studio.Route(t.Id);if(route.Channel+route.Channels>availableChannels)throw new InvalidOperationException(t.Name+": selected input is unavailable ("+availableChannels+" channels).");string file=System.IO.Path.Combine(folder,"Take-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+t.Id.ToString("N").Substring(0,8)+".wav");_destinations.Add(new Destination{Route=route,Writer=new PcmWaveWriter(file,rate,route.Channels),Take=new RecordedTake{Path=file,TrackId=t.Id,Channels=route.Channels}});}}catch{foreach(var d in _destinations)d.Writer.Dispose();throw;}
   _thread=new Thread(Write){IsBackground=true,Name="Recording disk writer"};_thread.Start();
  }
  public void Receive(float[] data,int frames,int channels,long position){lock(_queue){if(_stopping||Error!=null)return;if(_queued>480000){Error=new IOException("Recording disk queue overflow.");return;}float[] copy=new float[frames*channels];Array.Copy(data,copy,copy.Length);_queue.Enqueue(new Packet{Data=copy,Frames=frames,Channels=channels});_queued+=frames;}_signal.Set();}
  private void Write(){try{while(true){Packet p=null;lock(_queue){if(_queue.Count>0){p=_queue.Dequeue();_queued-=p.Frames;}else if(_stopping)break;}if(p==null){_signal.WaitOne(100);continue;}foreach(var d in _destinations){if(d.Route.Channel+d.Route.Channels>p.Channels)throw new IOException("The input channel count changed during recording.");byte[] bytes=new byte[p.Frames*d.Route.Channels*2];for(int f=0;f<p.Frames;f++)for(int c=0;c<d.Route.Channels;c++){short sample=(short)Math.Round(Math.Max(-1,Math.Min(0.9999695,p.Data[f*p.Channels+d.Route.Channel+c]))*32768);int o=(f*d.Route.Channels+c)*2;bytes[o]=(byte)sample;bytes[o+1]=(byte)(sample>>8);}d.Writer.Write(bytes,bytes.Length);d.Take.Frames+=p.Frames;}}}catch(Exception ex){Error=ex;}finally{foreach(var d in _destinations)d.Writer.Dispose();}}
  public List<RecordedTake> PendingTakes(){var result=new List<RecordedTake>();foreach(var d in _destinations)if(d.Take.Frames>0)result.Add(new RecordedTake{TrackId=d.Take.TrackId,RegionId=d.Take.RegionId,Path=d.Take.Path,Frames=d.Take.Frames,Channels=d.Take.Channels});return result;}
  public List<RecordedTake> Finish(){Dispose();if(Error!=null)throw Error;var result=new List<RecordedTake>();foreach(var d in _destinations)if(d.Take.Frames>0)result.Add(d.Take);return result;}
  private bool _closed;public void Dispose(){if(_closed)return;_closed=true;lock(_queue)_stopping=true;_signal.Set();if(!_thread.Join(5000))throw new TimeoutException("Recording writer did not stop.");_signal.Dispose();}
 }
}
