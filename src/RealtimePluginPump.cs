using System;
using System.Collections.Concurrent;
using System.Threading;
namespace SlopProX {
 internal sealed class PluginAudioJob {
  public float[] Audio;public MidiEventPacket[] Events;public NativePlugin.QueuedParameter[] Parameters;public int Frames,Count,Numerator,Denominator;public long Position;public double Tempo,Beat;public bool Playing;
  public PluginAudioJob(int block){Audio=new float[block*2];Events=new MidiEventPacket[4096];}
 }
 // Fixed two-block pipeline: the device callback never waits for a vendor or worker process.
 internal sealed class RealtimePluginPump:IDisposable {
  private readonly ConcurrentQueue<PluginAudioJob> _free=new ConcurrentQueue<PluginAudioJob>(),_input=new ConcurrentQueue<PluginAudioJob>(),_ready=new ConcurrentQueue<PluginAudioJob>();
  private readonly AutoResetEvent _signal=new AutoResetEvent(false);private Thread _thread;private Action<PluginAudioJob> _process;private volatile bool _stopping;private PluginAudioJob _read;private long _position;public int Delay{get;private set;}public long Missed,Expired;
  public RealtimePluginPump(int block,Action<PluginAudioJob> process){Delay=block*2;_process=process;for(int i=0;i<8;i++)_free.Enqueue(new PluginAudioJob(block));_thread=new Thread(Run){IsBackground=true,Name="Nonblocking plug-in pipeline"};_thread.Start();}
  private void Run(){while(!_stopping){PluginAudioJob job;if(!_input.TryDequeue(out job)){_signal.WaitOne(100);continue;}try{_process(job);}catch{Array.Clear(job.Audio,0,job.Frames*2);}_ready.Enqueue(job);}}
  public void Process(float[] audio,int frames,MidiEventPacket[] events,int count,NativePlugin.QueuedParameter[] parameters,double tempo,double beat,bool playing,int numerator,int denominator){
   PluginAudioJob job;if(_free.TryDequeue(out job)){job.Frames=frames;job.Count=count;job.Parameters=parameters;job.Tempo=tempo;job.Beat=beat;job.Playing=playing;job.Numerator=numerator;job.Denominator=denominator;job.Position=_position;Array.Copy(audio,job.Audio,frames*2);Array.Copy(events,job.Events,count);_input.Enqueue(job);_signal.Set();}else Interlocked.Increment(ref Missed);
   Array.Clear(audio,0,frames*2);long first=_position-Delay;int destination=0;
   while(destination<frames){
    long at=first+destination;
    if(_read==null){PluginAudioJob next;if(!_ready.TryPeek(out next))break;if(next.Position+next.Frames<=at){_ready.TryDequeue(out next);_free.Enqueue(next);Interlocked.Increment(ref Expired);continue;}if(next.Position>=first+frames)break;_ready.TryDequeue(out _read);}
    if(at<_read.Position){int silence=(int)Math.Min(frames-destination,_read.Position-at);destination+=silence;continue;}
    int offset=(int)(at-_read.Position);if(offset>=_read.Frames){_free.Enqueue(_read);_read=null;continue;}
    int copy=Math.Min(frames-destination,_read.Frames-offset);Array.Copy(_read.Audio,offset*2,audio,destination*2,copy*2);destination+=copy;
    if(offset+copy>=_read.Frames){_free.Enqueue(_read);_read=null;}
   }
   _position+=frames;
  }
  public void Dispose(){_stopping=true;_signal.Set();if(_thread!=Thread.CurrentThread)_thread.Join(600);}
 }
}