using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
namespace SlopProX
{
    public static class BuiltinSounds
    {
        public static readonly string[] Names={"Classic Electric Piano","Soft Piano","Analog Lead","Warm Pad","Sub Bass","Pluck","Drum Kit"};
        public static string Category(string name){return name=="Sub Bass"?"Bass":name=="Drum Kit"?"Drums":name=="Classic Electric Piano"||name=="Soft Piano"?"Keyboards":"Synthesizer";}
    }
    internal sealed class CachedAudio
    {
        public int Rate,Channels;public float[] Samples;public long Frames{get{return Samples.Length/Channels;}}
        public static CachedAudio Load(string path){PcmWaveData wave=WaveAudio.Load(path);byte[] b=wave.Samples;int bytes=wave.Info.BitsPerSample/8;float[] f=new float[b.Length/bytes];for(int i=0,o=0;i<f.Length;i++,o+=bytes){if(wave.Info.EncodingTag==3)f[i]=BitConverter.ToSingle(b,o);else if(bytes==1)f[i]=(b[o]-128)/128f;else if(bytes==2)f[i]=BitConverter.ToInt16(b,o)/32768f;else if(bytes==3){int n=b[o]|b[o+1]<<8|b[o+2]<<16;if((n&0x800000)!=0)n|=unchecked((int)0xff000000);f[i]=(float)(n/8388608.0);}else f[i]=(float)(BitConverter.ToInt32(b,o)/2147483648.0);}for(int j=0;j<f.Length;j++)if(Single.IsNaN(f[j])||Single.IsInfinity(f[j]))throw new InvalidDataException("WAV contains non-finite audio samples.");return new CachedAudio{Rate=wave.Info.SampleRate,Channels=wave.Info.Channels,Samples=f};}
    }
    public sealed class ArrangementEngine:IDisposable
    {
        private sealed class SendTarget{public Guid TargetId;public float Gain;public SampleDelay Delay=new SampleDelay();}
        private sealed class Scene{public Track Track;public List<Region> Arranged;public BuiltinInstrument Synth=new BuiltinInstrument();public List<NativePlugin> Plugins=new List<NativePlugin>();public List<InsertEntry> Inserts=new List<InsertEntry>();public List<KeyValuePair<Region,CachedAudio>> Clips=new List<KeyValuePair<Region,CachedAudio>>();public float[] Buffer;public MidiEventPacket[] Events=new MidiEventPacket[4096];public int[] ActiveNotes=new int[16*128];public SampleDelay Latency,InputDelay,DestinationDelay;public int Compensation,InputArrival,OutputArrival;public string SamplerKey;public Guid? Destination;public List<SendTarget> Sends=new List<SendTarget>();public long LastLoopFrame=Int64.MinValue;public int EventCount,NoteHash;public bool ResetNotes=true,ProcessingFailed;public float Peak;}
        private sealed class PacketComparer:IComparer<MidiEventPacket>{public int Compare(MidiEventPacket a,MidiEventPacket b){int d=a.Offset.CompareTo(b.Offset);if(d!=0)return d;return ((a.Status&240)==0x80?0:1)-((b.Status&240)==0x80?0:1);}}
        private readonly Dictionary<Guid,StockEffectProcessor> _stock=new Dictionary<Guid,StockEffectProcessor>();public StockEffectProcessor GetStockProcessor(Guid id){lock(_sync){StockEffectProcessor p;return _stock.TryGetValue(id,out p)?p:null;}}
        private List<InsertEntry> _masterInserts=new List<InsertEntry>();private HashSet<Guid> _failedMasterInserts=new HashSet<Guid>();private float[] _masterBuffer,_masterBackup;private static readonly MidiEventPacket[] EmptyEvents=new MidiEventPacket[0];private Dictionary<Guid,Scene> _sceneById=new Dictionary<Guid,Scene>();private Dictionary<Guid,float[]> _busBuffers=new Dictionary<Guid,float[]>(),_sidechainBuffers=new Dictionary<Guid,float[]>();private HashSet<Guid> _soloAudible;private static readonly PacketComparer Comparer=new PacketComparer();private readonly object _sync=new object();private List<Scene> _scenes=new List<Scene>();private Dictionary<string,CachedAudio> _audio=new Dictionary<string,CachedAudio>(StringComparer.OrdinalIgnoreCase);private Dictionary<Guid,NativePlugin> _plugins=new Dictionary<Guid,NativePlugin>();private ConcurrentQueue<MidiInputMessage> _live=new ConcurrentQueue<MidiInputMessage>();private List<MidiInputMessage> _liveBlock=new List<MidiInputMessage>();private ProjectDocument _project,_sourceProject;private AppSettings _settings;private IAudioOutput _output;private IDisposable _audioInput;private AudioInputBus _inputBus=new AudioInputBus();private float[] _monitorBlock;private int _pdc;private readonly Dictionary<Guid,Tuple<Region,double>> _launched=new Dictionary<Guid,Tuple<Region,double>>();private readonly Dictionary<Guid,Tuple<Region,double>> _pendingLaunch=new Dictionary<Guid,Tuple<Region,double>>();private Guid? _midiThruTrack;public long MidiMessageCount{get;private set;}public string MidiInputName{get;private set;}private MidiInputDevice _input;private WinMmMidiOutput _midiOut;private double _frame,_startBeat;private bool _playing;private float _masterPeak;private double _load;public readonly List<string> Warnings=new List<string>();public event Action<Exception> Failed;public event EventHandler<MidiInputMessage> MidiReceived;public long LastMidiTimestamp {get;private set;}public MidiInputMessage LastMidiMessage{get;private set;}private readonly List<string> _deviceWarnings=new List<string>();
        public event Action<float[],int,int,long> AudioInputReceived;public int InputChannels{get{return _audioInput==null?0:_audioInput is WasapiStream?((WasapiStream)_audioInput).Channels:_audioInput is AsioCompanion?((AsioCompanion)_audioInput).InputChannels:_inputBus.Channels;}}public int CompensationSamples{get{lock(_sync)return _pdc;}}
        public int SampleRate{get{return _settings.SampleRate;}}public int BufferFrames{get{return _settings.BufferFrames;}}public bool Playing{get{lock(_sync)return _playing;}}public float MasterPeak{get{lock(_sync)return _masterPeak;}}public double CpuLoad{get{lock(_sync)return _load;}}public bool DeviceRunning{get{return _output!=null;}}public bool MidiInputConnected{get{return _input!=null;}}
        public ArrangementEngine(AppSettings settings){_settings=settings;}
        private readonly TransportHistory _presentation=new TransportHistory();private double _launcherFrame;
        public double PositionBeat{get{lock(_sync){if(_project==null)return 0;if(!_playing||_output==null)return _frame*_project.Tempo/(60*_settings.SampleRate);return _presentation.Position(_output.FramesPlayed,_pdc);}}}
        public void Configure(AppSettings settings){CloseDevices();lock(_sync){foreach(NativePlugin p in _plugins.Values)p.Dispose();_plugins.Clear();_scenes.Clear();_settings=settings;}OpenDevices();}
        public void OpenDevices(){
 if(_output!=null)return;_deviceWarnings.Clear();MidiInputName=null;
 lock(_sync)foreach(var plugin in _plugins.Values)plugin.EnableRealtime(_settings.BufferFrames);
 if(_settings.AudioBackend=="ASIO")_output=new AsioCompanion(_settings,Render,ReceiveInput);
 else if(_settings.AudioBackend.StartsWith("WASAPI"))_output=new WasapiStream(_settings.OutputEndpoint,false,_settings.AudioBackend=="WASAPI Exclusive",_settings.SampleRate,_settings.BufferFrames,delegate(float[] data,int n,int channels,long position){Render(data,n,position);});
 else _output=new WaveOutputStream(_settings.AudioOutput,_settings.SampleRate,_settings.BufferFrames,Render);
 _output.Failed+=OnFailure;
 try{if(_settings.AudioBackend=="ASIO")_audioInput=(IDisposable)_output;else if(_settings.AudioBackend.StartsWith("WASAPI"))_audioInput=new WasapiStream(_settings.InputEndpoint,true,_settings.AudioBackend=="WASAPI Exclusive",_settings.SampleRate,_settings.BufferFrames,ReceiveInput);else{try{_audioInput=new WaveCaptureStream(_settings.AudioInput,_settings.SampleRate,_settings.BufferFrames,null,ReceiveInput,2);}catch{_audioInput=new WaveCaptureStream(_settings.AudioInput,_settings.SampleRate,_settings.BufferFrames,null,ReceiveInput,1);}}}catch(Exception ex){_deviceWarnings.Add("Audio input: "+ex.Message);Warnings.Add("Audio input: "+ex.Message);}
 if(_settings.MidiInput>=0){try{_input=new MidiInputDevice(_settings.MidiInput,OnMidi);MidiInputName=WinMmDevices.MidiInputs().Find(d=>d.Id==_settings.MidiInput).Name;}catch(Exception ex){_deviceWarnings.Add("MIDI input: "+ex.Message);Warnings.Add("MIDI input: "+ex.Message);}}
 if(_settings.MidiOutput>=0){try{_midiOut=WinMmMidiOutput.OpenDevice(_settings.MidiOutput);}catch(Exception ex){Warnings.Add(ex.Message);}}
}
 private void ReceiveInput(float[] data,int frames,int channels,long position){_inputBus.Push(data,frames,channels);var h=AudioInputReceived;if(h!=null)h(data,frames,channels,position);}
 public void InjectAudioInput(float[] data,int frames,int channels){ReceiveInput(data,frames,channels,0);}
 public void Launch(Guid trackId,Guid? regionId,double quantum){
  lock(_sync){Track track=_project.Tracks.Find(delegate(Track t){return t.Id==trackId;});if(track==null)throw new InvalidOperationException("Launcher track unavailable.");Region region=regionId.HasValue?track.Regions.Find(delegate(Region r){return r.Id==regionId.Value;}):null;if(regionId.HasValue&&region==null)throw new InvalidOperationException("Launcher clip unavailable.");double now=_launcherFrame*_project.Tempo/(60*_settings.SampleRate);double start=_playing&&quantum>0?Math.Ceiling((now+0.000001)/quantum)*quantum:now;_pendingLaunch[trackId]=Tuple.Create(region,start);}
 }
 public void ReturnToArrangement(){lock(_sync){_launched.Clear();_pendingLaunch.Clear();foreach(var s in _scenes)s.ResetNotes=true;}}
 public bool IsLaunched(Guid track,Guid region){lock(_sync){Tuple<Region,double> item;return _launched.TryGetValue(track,out item)&&item.Item1!=null&&item.Item1.Id==region;}}
 public bool IsLaunchPending(Guid track,Guid region){lock(_sync){Tuple<Region,double> item;return _pendingLaunch.TryGetValue(track,out item)&&item.Item1!=null&&item.Item1.Id==region;}}
 private IEnumerable<Region> Regions(Scene s,double beat,double launchBeat){
  Tuple<Region,double> active;
  if(_launched.TryGetValue(s.Track.Id,out active)){if(active.Item1!=null){Region r=active.Item1;double loop=active.Item2+Math.Max(0,Math.Floor((launchBeat-active.Item2+0.00000001)/r.LengthBeats))*r.LengthBeats;double phase=Math.Max(0,launchBeat-loop);long loopFrame=(long)Math.Round(loop*60*_settings.SampleRate/_project.Tempo);if(s.LastLoopFrame!=loopFrame){s.LastLoopFrame=loopFrame;s.ResetNotes=true;}yield return new Region{Id=r.Id,Name=r.Name,StartBeat=beat-phase,LengthBeats=r.LengthBeats,MediaPath=r.MediaPath,AudioOffsetSeconds=r.AudioOffsetSeconds,Muted=r.Muted,Transpose=r.Transpose,VelocityOffset=r.VelocityOffset,Notes=r.Notes};}yield break;}
  foreach(Region r in s.Arranged)yield return r;
 }
 private void OnFailure(Exception ex){Action<Exception> h=Failed;if(h!=null)h(ex);}
        private void OnMidi(object sender,MidiInputMessage e){LastMidiTimestamp=e.Timestamp;LastMidiMessage=e;MidiMessageCount++;_live.Enqueue(e);EventHandler<MidiInputMessage> h=MidiReceived;if(h!=null)h(this,e);}
        public void SetMidiThruTrack(Guid? id){lock(_sync){if(_midiThruTrack==id)return;foreach(var scene in _scenes)if(scene.Track.Id==_midiThruTrack){scene.ResetNotes=true;scene.Synth.Reset();}_midiThruTrack=id;}}
        public void InjectMidi(int status,int a,int b){OnMidi(this,new MidiInputMessage{Status=status,Data1=a,Data2=b,Timestamp=Stopwatch.GetTimestamp()});}
        private static string SamplerFingerprint(Track t,SamplerSettings config){if(config==null)return t.Instrument+"|default";string file=String.Empty;if(!String.IsNullOrEmpty(config.SamplePath)){try{var info=new FileInfo(config.SamplePath);file=info.FullName+"|"+(info.Exists?info.Length:0)+"|"+(info.Exists?info.LastWriteTimeUtc.Ticks:0);}catch{file=config.SamplePath;}}return t.Instrument+"|"+file+"|"+config.RootNote+"|"+config.Attack.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+config.Decay.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+config.Sustain.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"|"+config.Release.ToString("R",System.Globalization.CultureInfo.InvariantCulture);}
        private static int NoteFingerprint(Track t){unchecked{int h=t.Instrument.GetHashCode()^t.Armed.GetHashCode()^t.ExternalMidi.GetHashCode();foreach(Region r in t.Regions){h=h*31+r.StartBeat.GetHashCode();h=h*31+r.LengthBeats.GetHashCode();h=h*31+r.Muted.GetHashCode()+r.Transpose*13+r.VelocityOffset;foreach(MidiNote n in r.Notes){h=h*31+n.StartBeat.GetHashCode();h=h*31+n.LengthBeats.GetHashCode();h=h*31+n.Pitch*127+n.Velocity;}}foreach(PluginSlot p in t.Plugins)h=h*31+p.Id.GetHashCode()+p.Bypassed.GetHashCode();return h;}}
        // Publish parameter-only edits to the current graph without recreating plugins
        // or resetting compressor envelopes on each knob movement.
        public bool UpdateStockEffect(StockEffectSlot value){
         StockEffects.Validate(value);
         lock(_sync){
          if(_project==null)return false;
          var current=_project.Studio.Effects.Find(f=>f.Id==value.Id);
          if(current==null||current.Kind!=value.Kind||current.TrackId!=value.TrackId||
             current.SidechainTrackId!=value.SidechainTrackId||current.Enabled!=value.Enabled)return false;
          current.Values=new Dictionary<string,double>(value.Values);
          return true;
         }
        }
        // The rendering graph owns a snapshot; keep its gain in sync without
        // rebuilding the graph for every mouse movement.
        public void UpdateMasterGain(double gainDb){
         if(Double.IsNaN(gainDb)||Double.IsInfinity(gainDb)||gainDb < -60 || gainDb > 6)throw new ArgumentOutOfRangeException("gainDb");
         lock(_sync){if(_project!=null)_project.MasterGainDb=gainDb;}
        }
        public void SetProject(ProjectDocument project){
         // Build and load the next graph outside the device callback lock.
         ProjectDocument snapshot=ProjectFactory.Clone(project);var warnings=new List<string>();List<Scene> oldScenes;Dictionary<Guid,NativePlugin> oldPlugins;Dictionary<string,CachedAudio> audio;
         lock(_sync){oldScenes=new List<Scene>(_scenes);oldPlugins=new Dictionary<Guid,NativePlugin>(_plugins);audio=new Dictionary<string,CachedAudio>(_audio,StringComparer.OrdinalIgnoreCase);}
         var scenes=new List<Scene>();var plugins=new Dictionary<Guid,NativePlugin>();var failedAudio=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
         foreach(Track track in TrackRouting.ProcessingOrder(snapshot)){
          Scene old=oldScenes.Find(s=>s.Track.Id==track.Id);var scene=new Scene{Track=track,Arranged=CompRegions(snapshot,track),NoteHash=NoteFingerprint(track),Inserts=InsertChain.Entries(snapshot,track)};
          var samplerConfig=snapshot.Studio.Samplers.Find(x=>x.TrackId==track.Id);scene.SamplerKey=SamplerFingerprint(track,samplerConfig);var parent=snapshot.Studio.Stacks.Find(g=>g.Summing&&g.Children.Contains(track.Id)&&snapshot.Tracks.Any(t=>t.Id==g.ParentId));if(parent!=null)scene.Destination=parent.ParentId;scene.Sends=snapshot.Studio.Sends.Where(x=>x.Enabled&&x.SourceId==track.Id&&snapshot.Tracks.Any(t=>t.Id==x.TargetId)).Select(x=>new SendTarget{TargetId=x.TargetId,Gain=(float)Math.Pow(10,x.GainDb/20)}).ToList();
          unchecked{foreach(var slice in snapshot.Studio.Comps.Where(c=>c.TrackId==track.Id).SelectMany(c=>c.Slices))scene.NoteHash=scene.NoteHash*31+slice.RegionId.GetHashCode()+slice.StartBeat.GetHashCode()+slice.EndBeat.GetHashCode();}
          if(old!=null){scene.ActiveNotes=old.ActiveNotes;scene.Synth=old.SamplerKey==scene.SamplerKey?old.Synth:new BuiltinInstrument();scene.Peak=old.Peak;scene.LastLoopFrame=old.LastLoopFrame;scene.ResetNotes=old.ResetNotes||old.NoteHash!=scene.NoteHash||old.SamplerKey!=scene.SamplerKey;scene.Latency=old.Latency;scene.InputDelay=old.InputDelay;if(scene.Destination==old.Destination)scene.DestinationDelay=old.DestinationDelay;foreach(var send in scene.Sends){var previous=old.Sends.Find(x=>x.TargetId==send.TargetId);if(previous!=null)send.Delay=previous.Delay;}}
          if(scene.Latency==null)scene.Latency=new SampleDelay();if(scene.InputDelay==null)scene.InputDelay=new SampleDelay();if(scene.DestinationDelay==null)scene.DestinationDelay=new SampleDelay();scene.Buffer=old!=null&&old.Buffer.Length==_settings.BufferFrames*2?old.Buffer:new float[_settings.BufferFrames*2];
          foreach(Region r in track.Regions)if(track.Type==TrackType.Audio&&!String.IsNullOrEmpty(r.MediaPath)&&!failedAudio.Contains(r.MediaPath))try{CachedAudio clip;if(!audio.TryGetValue(r.MediaPath,out clip))audio[r.MediaPath]=CachedAudio.Load(r.MediaPath);}catch(Exception ex){failedAudio.Add(r.MediaPath);warnings.Add(track.Name+": "+ex.Message);}
          foreach(PluginSlot slot in track.Plugins){
           if(slot.UseAra)slot.AraDocument=AraDocumentInfo.FromTrack(snapshot,track);
           NativePlugin plugin;oldPlugins.TryGetValue(slot.Id,out plugin);
           if(plugin!=null&&(plugin.Slot.State!=slot.State||plugin.Slot.UseAra!=slot.UseAra||plugin.Slot.AraState!=slot.AraState||plugin.Slot.Path!=slot.Path||plugin.Slot.ClassId!=slot.ClassId))plugin=null;
           try{if(plugin==null&&!slot.Bypassed)plugin=new NativePlugin(slot,_settings.SampleRate,_settings.BufferFrames);if(plugin!=null){plugin.BindSlot(slot);if(slot.UseAra)plugin.UpdateAraDocument(slot.AraDocument);if(_output!=null)plugin.EnableRealtime(_settings.BufferFrames);plugins[slot.Id]=plugin;if(!slot.Bypassed)scene.Plugins.Add(plugin);}}
           catch(Exception ex){warnings.Add(slot.Name+": "+ex.Message);}
          }
          if(track.Type==TrackType.Midi&&!track.ExternalMidi&&!track.Plugins.Exists(p=>p.IsInstrument)&&(old==null||old.SamplerKey!=scene.SamplerKey||old.Track.Type!=TrackType.Midi||old.Track.ExternalMidi||old.Track.Plugins.Exists(p=>p.IsInstrument))){try{scene.Synth.Prepare(track.Instrument,samplerConfig);}catch(Exception ex){warnings.Add(track.Name+" Sampler: "+ex.Message);scene.Synth.Prepare(track.Instrument);}}scenes.Add(scene);
         }
         var masterInserts=InsertChain.Entries(snapshot,Guid.Empty);
         foreach(PluginSlot slot in snapshot.Studio.MasterPlugins??new List<PluginSlot>()){
          NativePlugin plugin;oldPlugins.TryGetValue(slot.Id,out plugin);
          if(plugin!=null&&(plugin.Slot.State!=slot.State||plugin.Slot.UseAra!=slot.UseAra||plugin.Slot.AraState!=slot.AraState||plugin.Slot.Path!=slot.Path||plugin.Slot.ClassId!=slot.ClassId))plugin=null;
          try{if(plugin==null&&!slot.Bypassed)plugin=new NativePlugin(slot,_settings.SampleRate,_settings.BufferFrames);if(plugin!=null){plugin.BindSlot(slot);if(_output!=null)plugin.EnableRealtime(_settings.BufferFrames);plugins[slot.Id]=plugin;}}
          catch(Exception ex){warnings.Add("Master / "+slot.Name+": "+ex.Message);}
         }
         var busBuffers=new Dictionary<Guid,float[]>();foreach(var scene in scenes){if(scene.Destination.HasValue&&!busBuffers.ContainsKey(scene.Destination.Value))busBuffers.Add(scene.Destination.Value,new float[_settings.BufferFrames*2]);foreach(var send in scene.Sends)if(!busBuffers.ContainsKey(send.TargetId))busBuffers.Add(send.TargetId,new float[_settings.BufferFrames*2]);}var sidechainBuffers=new Dictionary<Guid,float[]>();foreach(var effect in snapshot.Studio.Effects)if(effect.Enabled&&effect.SidechainTrackId.HasValue&&snapshot.Tracks.Any(t=>t.Id==effect.SidechainTrackId.Value)&&!sidechainBuffers.ContainsKey(effect.SidechainTrackId.Value))sidechainBuffers.Add(effect.SidechainTrackId.Value,new float[_settings.BufferFrames*2]);var soloAudible=TrackRouting.SoloAudible(snapshot);
         lock(_sync){if(_project!=null&&Math.Abs(_project.Tempo-snapshot.Tempo)>0.000001){double ratio=_project.Tempo/snapshot.Tempo;_frame*=ratio;_launcherFrame*=ratio;}_sceneById=scenes.ToDictionary(x=>x.Track.Id);_masterInserts=masterInserts;_masterBuffer=new float[_settings.BufferFrames*2];_masterBackup=new float[_settings.BufferFrames*2];_failedMasterInserts.Clear();_busBuffers=busBuffers;_sidechainBuffers=sidechainBuffers;_soloAudible=soloAudible;_sourceProject=project;_scenes=scenes;_project=snapshot;_plugins=plugins;_audio=audio;Warnings.Clear();Warnings.AddRange(_deviceWarnings);Warnings.AddRange(warnings);foreach(var fx in snapshot.Studio.Effects)if(!_stock.ContainsKey(fx.Id))_stock[fx.Id]=new StockEffectProcessor();UpdatePdc();}
         foreach(var pair in oldPlugins)if(!plugins.ContainsKey(pair.Key)||plugins[pair.Key]!=pair.Value)pair.Value.Dispose();
        }
        private static List<Region> CompRegions(ProjectDocument project,Track track){
         var result=new List<Region>();var comp=project.Studio.Comps.Find(c=>c.TrackId==track.Id);foreach(var region in track.Regions)if(comp==null||!comp.Takes.Contains(region.Id))result.Add(region);if(comp==null)return result;
         foreach(var slice in comp.Slices){var source=track.Regions.Find(r=>r.Id==slice.RegionId);if(source==null)continue;var region=new Region{Id=source.Id,Name=source.Name,StartBeat=slice.StartBeat,LengthBeats=slice.EndBeat-slice.StartBeat,MediaPath=source.MediaPath,AudioOffsetSeconds=source.AudioOffsetSeconds+(slice.StartBeat-source.StartBeat)*60/project.Tempo,Muted=source.Muted,Transpose=source.Transpose,VelocityOffset=source.VelocityOffset};if(track.Type==TrackType.Midi)foreach(var note in source.Notes){double start=Math.Max(0,source.StartBeat+note.StartBeat-slice.StartBeat),end=Math.Min(region.LengthBeats,source.StartBeat+note.StartBeat+note.LengthBeats-slice.StartBeat);if(end-start>=.0625)region.Notes.Add(new MidiNote{StartBeat=start,LengthBeats=end-start,Pitch=note.Pitch,Velocity=note.Velocity});}result.Add(region);}return result;
        }
        // The graph is topologically ordered. Align each incoming audio path at the bus,
        // and align the final direct outputs at the master. Sidechain keys have separate
        // timing requirements and are not included in this audio-path compensation.
        private void UpdatePdc(){
         int limit=SampleRate*10,maximum=0;
         foreach(var scene in _scenes)scene.InputArrival=0;
         foreach(var scene in _scenes){
          long pluginLatency=0;if(_settings.PdcEnabled)foreach(var plugin in scene.Plugins)pluginLatency+=Math.Max(0,plugin.LatencySamples);
          scene.Compensation=(int)Math.Min(limit,pluginLatency);scene.OutputArrival=(int)Math.Min(limit,(long)scene.InputArrival+scene.Compensation);
          Scene target;if(scene.Destination.HasValue&&_sceneById.TryGetValue(scene.Destination.Value,out target))target.InputArrival=Math.Max(target.InputArrival,scene.OutputArrival);
          foreach(var send in scene.Sends)if(_sceneById.TryGetValue(send.TargetId,out target))target.InputArrival=Math.Max(target.InputArrival,scene.OutputArrival);
          if(!scene.Destination.HasValue)maximum=Math.Max(maximum,scene.OutputArrival);
         }
         _pdc=maximum;
         foreach(var scene in _scenes){
          scene.InputDelay.SetDelay(scene.InputArrival);
          Scene target;if(scene.Destination.HasValue&&_sceneById.TryGetValue(scene.Destination.Value,out target))scene.DestinationDelay.SetDelay(Math.Max(0,target.InputArrival-scene.OutputArrival));
          else scene.Latency.SetDelay(Math.Max(0,maximum-scene.OutputArrival));
          foreach(var send in scene.Sends)if(_sceneById.TryGetValue(send.TargetId,out target))send.Delay.SetDelay(Math.Max(0,target.InputArrival-scene.OutputArrival));
         }
        }
        public void RestartPlugin(Guid id){lock(_sync){NativePlugin p;if(_plugins.TryGetValue(id,out p)){p.Dispose();_plugins.Remove(id);}}}
        public NativePlugin GetPlugin(Guid id){lock(_sync){NativePlugin p;return _plugins.TryGetValue(id,out p)?p:null;}}
        public void CapturePluginStates(){
         List<NativePlugin> plugins;lock(_sync)plugins=new List<NativePlugin>(_plugins.Values);
         foreach(var p in plugins){if(!p.Healthy){Warnings.Add(p.Slot.Name+": preserving last saved state; worker is unavailable.");continue;}string state=p.SaveState();if(_sourceProject!=null){PluginSlot master=_sourceProject.Studio.MasterPlugins.Find(s=>s.Id==p.Slot.Id);if(master!=null){master.State=state;master.AraState=p.Slot.AraState;continue;}foreach(var track in _sourceProject.Tracks){var slot=track.Plugins.Find(s=>s.Id==p.Slot.Id);if(slot!=null){slot.State=state;slot.AraState=p.Slot.AraState;}}}}
        }
        public bool DeviceFailed{get{var asio=_output as AsioCompanion;return asio!=null&&asio.Error!=null;}}
        public bool OpenActiveAsioPanel(string clsid){var asio=_output as AsioCompanion;if(asio==null||asio.Error!=null||!String.Equals(_settings.AsioDriver,clsid,StringComparison.OrdinalIgnoreCase))return false;asio.ControlPanel();return true;}
        public void Start(double beat){var asio=_output as AsioCompanion;if(asio!=null&&asio.Error!=null)CloseDevices();if(_output==null)OpenDevices();lock(_sync){_startBeat=Math.Max(0,beat);_frame=Math.Round(_startBeat*60*_settings.SampleRate/_project.Tempo);if(_launched.Count==0&&_pendingLaunch.Count==0)_launcherFrame=_frame;_presentation.Reset(_startBeat);_playing=true;foreach(Scene scene in _scenes){scene.ResetNotes=true;scene.Synth.Reset();}}}
        public void Seek(double beat){bool was=Playing;if(was)Start(beat);else lock(_sync){_frame=Math.Max(0,beat)*60*_settings.SampleRate/(_project==null?120:_project.Tempo);if(_launched.Count==0&&_pendingLaunch.Count==0)_launcherFrame=_frame;_presentation.Reset(Math.Max(0,beat));foreach(Scene scene in _scenes){scene.ResetNotes=true;scene.Synth.Reset();}}}
        public void Stop(){lock(_sync){double beat=PositionBeat;_playing=false;_frame=beat*60*_settings.SampleRate/(_project==null?120:_project.Tempo);foreach(Scene scene in _scenes){scene.ResetNotes=true;scene.Synth.Reset();}if(_midiOut!=null)_midiOut.AllNotesOff();}}
        public float TrackPeak(Guid id){lock(_sync){foreach(Scene scene in _scenes)if(scene.Track.Id==id)return scene.Peak;return 0;}}
        private static void Add(Scene s,int offset,int status,int a,int b){if(s.EventCount<s.Events.Length)s.Events[s.EventCount++]=new MidiEventPacket(offset,status,a,b);}
        private readonly List<Guid> _activatedLauncher=new List<Guid>();
        public void Render(float[] output,int frames,long deviceStart){
         long started=Stopwatch.GetTimestamp();lock(_sync){
          Array.Clear(output,0,frames*2);if(_project==null)return;UpdatePdc();
          _liveBlock.Clear();MidiInputMessage message;while(_liveBlock.Count<1024&&_live.TryDequeue(out message))_liveBlock.Add(message);
          _monitorBlock=_inputBus.Read(frames);int used=0;double perFrame=_project.Tempo/(60*_settings.SampleRate);
          while(used<frames){
           if(_playing&&_project.CycleEnabled){double endFrame=_project.CycleEndBeat/perFrame;if(_frame>=endFrame){_frame=_project.CycleStartBeat/perFrame;foreach(Scene scene in _scenes)if(!_launched.ContainsKey(scene.Track.Id)){scene.ResetNotes=true;scene.Synth.Reset();}}}
           int count=frames-used;double launchBeat=_launcherFrame*perFrame;
           _activatedLauncher.Clear();foreach(var pending in _pendingLaunch){
            if(!_playing||launchBeat+0.0000001>=pending.Value.Item2){_launched[pending.Key]=pending.Value;_activatedLauncher.Add(pending.Key);foreach(var scene in _scenes)if(scene.Track.Id==pending.Key)scene.ResetNotes=true;}
            else count=Math.Min(count,Math.Max(1,(int)Math.Round(pending.Value.Item2/perFrame-_launcherFrame)));
           }
           foreach(var id in _activatedLauncher)_pendingLaunch.Remove(id);
           foreach(var active in _launched.Values)if(active.Item1!=null){double length=active.Item1.LengthBeats;double end=active.Item2+(Math.Floor((launchBeat-active.Item2+0.0000001)/length)+1)*length;count=Math.Min(count,Math.Max(1,(int)Math.Round(end/perFrame-_launcherFrame)));}
           if(_playing&&_project.CycleEnabled){double endFrame=_project.CycleEndBeat/perFrame;count=Math.Min(count,Math.Max(1,(int)Math.Ceiling(endFrame-_frame)));}
           if(_playing&&_output!=null)_presentation.Record(deviceStart+used,count,_frame*perFrame,perFrame);
           RenderSegment(output,used,count,_frame,_launcherFrame,used==0);
           if(_playing){_frame+=count;_launcherFrame+=count;}used+=count;
          }
          float peak=0;for(int i=0;i<frames*2;i++)peak=Math.Max(peak,Math.Abs(output[i]));_masterPeak=Math.Max(peak,_masterPeak*0.85f);
          _load=_load*0.9+0.1*((Stopwatch.GetTimestamp()-started)/(double)Stopwatch.Frequency)/(frames/(double)_settings.SampleRate);
         }
        }
        private void RenderSegment(float[] output,int destination,int frames,double frame,double launchFrame,bool includeLive){bool solo=_soloAudible!=null;foreach(var bus in _busBuffers.Values)Array.Clear(bus,0,bus.Length);foreach(var key in _sidechainBuffers.Values)Array.Clear(key,0,key.Length);double tempo=_project.Tempo;double beat=frame*tempo/(60*_settings.SampleRate);foreach(Scene s in _scenes){Array.Clear(s.Buffer,0,s.Buffer.Length);float[] incoming;_busBuffers.TryGetValue(s.Track.Id,out incoming);s.EventCount=0;var regions=new List<Region>(Regions(s,beat,launchFrame*tempo/(60*_settings.SampleRate)));bool reset=s.ResetNotes;if(reset){for(int i=0;i<s.ActiveNotes.Length;i++){int count=s.ActiveNotes[i];for(int n=0;n<count;n++)Add(s,0,0x80|(i/128),i%128,0);s.ActiveNotes[i]=0;}s.ResetNotes=false;}if(s.Track.Type==TrackType.Audio){InputRoute route=_project.Studio.Route(s.Track.Id);if(route.Monitor&&_monitorBlock!=null)for(int i=0;i<frames;i++)for(int c=0;c<2;c++)s.Buffer[i*2+c]+=_monitorBlock[(destination+i)*32+Math.Min(31,route.Channel+(route.Channels==1?0:c))];}
 if(_playing&&s.Track.Type==TrackType.Audio)foreach(Region region in regions){
  if(region.Muted||String.IsNullOrEmpty(region.MediaPath))continue;CachedAudio media;if(!_audio.TryGetValue(region.MediaPath,out media))continue;
  double clipStart=region.StartBeat*60*_settings.SampleRate/tempo,clipEnd=(region.StartBeat+region.LengthBeats)*60*_settings.SampleRate/tempo;
  int first=Math.Max(0,(int)Math.Ceiling(clipStart-frame)),last=Math.Min(frames,(int)Math.Ceiling(clipEnd-frame));
  for(int i=first;i<last;i++){double source=(frame+i-clipStart)*media.Rate/_settings.SampleRate+region.AudioOffsetSeconds*media.Rate;long index=(long)source;if(index<0||index>=media.Frames)continue;double f=source-index;long next=Math.Min(media.Frames-1,index+1);for(int c=0;c<2;c++){int channel=media.Channels==1?0:c;float x=media.Samples[index*media.Channels+channel],y=media.Samples[next*media.Channels+channel];s.Buffer[i*2+c]+=(float)(x+(y-x)*f);}}
 }
 if(_playing&&s.Track.Type==TrackType.Midi)foreach(Region region in regions)foreach(MidiNote note in region.Notes){if(region.Muted)continue;int pitch=Math.Max(0,Math.Min(127,note.Pitch+region.Transpose)),velocity=Math.Max(1,Math.Min(127,note.Velocity+region.VelocityOffset));double on=Math.Round((region.StartBeat+note.StartBeat)*60*_settings.SampleRate/tempo);double off=Math.Round((region.StartBeat+note.StartBeat+note.LengthBeats)*60*_settings.SampleRate/tempo);if(on>=frame&&on<frame+frames)Add(s,(int)Math.Round(on-frame),0x90,pitch,velocity);else if(reset&&on<frame&&off>frame)Add(s,0,0x90,pitch,velocity);if(off>=frame&&off<frame+frames)Add(s,(int)Math.Round(off-frame),0x80,pitch,0);}
 if(includeLive&&(s.Track.Armed||s.Track.Id==_midiThruTrack)&&s.Track.Type==TrackType.Midi)foreach(MidiInputMessage e in _liveBlock)Add(s,0,e.Status,e.Data1,e.Data2);Array.Sort(s.Events,0,s.EventCount,Comparer);for(int i=0;i<s.EventCount;i++){var e=s.Events[i];int status=e.Status&240;if((status==0x80||status==0x90)&&e.Data1>=0&&e.Data1<128){int at=(e.Status&15)*128+e.Data1;if(status==0x90&&e.Data2>0)s.ActiveNotes[at]=Math.Min(64,s.ActiveNotes[at]+1);else if(s.ActiveNotes[at]>0)s.ActiveNotes[at]--;}}if(s.Track.Type==TrackType.Midi&&!s.Track.ExternalMidi&&!s.Track.Plugins.Exists(delegate(PluginSlot p){return p.IsInstrument;}))s.Synth.Render(s.Buffer,frames,_settings.SampleRate,s.Track.Instrument,s.Events,s.EventCount);foreach(NativePlugin plugin in s.Plugins)if(plugin.Slot.IsInstrument)ProcessInsertPlugin(s,plugin,frames,beat,tempo);for(int i=0;i<frames;i++){float l=s.Buffer[i*2],r=s.Buffer[i*2+1];s.InputDelay.Process(ref l,ref r);s.Buffer[i*2]=l+(incoming==null?0:incoming[i*2]);s.Buffer[i*2+1]=r+(incoming==null?0:incoming[i*2+1]);}foreach(var entry in s.Inserts){if(s.ProcessingFailed){Array.Clear(s.Buffer,0,s.Buffer.Length);break;}if(entry.Plugin!=null){NativePlugin plugin;if(!entry.Plugin.Bypassed&&_plugins.TryGetValue(entry.Id,out plugin))ProcessInsertPlugin(s,plugin,frames,beat,tempo);}else if(entry.Stock.Enabled){StockEffectProcessor processor;if(_stock.TryGetValue(entry.Id,out processor))processor.Process(entry.Stock,s.Buffer,frames,_settings.SampleRate,beat,tempo,s.Events,s.EventCount,_monitorBlock,destination,_liveBlock,_playing,entry.Stock.SidechainTrackId.HasValue&&_sidechainBuffers.ContainsKey(entry.Stock.SidechainTrackId.Value)?_sidechainBuffers[entry.Stock.SidechainTrackId.Value]:null);}}
 if(_midiOut!=null&&s.Track.Type==TrackType.Midi&&s.Track.ExternalMidi)for(int i=0;i<s.EventCount;i++){MidiEventPacket e=s.Events[i];if((e.Status&240)==0x80||((e.Status&240)==0x90&&e.Data2==0)||(!s.Track.Muted&&(!solo||_soloAudible.Contains(s.Track.Id))))_midiOut.SendPacket(e.Status,e.Data1,e.Data2);}float peak=0;for(int i=0;i<frames;i++){
 double at=beat+i*tempo/(60*_settings.SampleRate),db=_project.Studio.Value(s.Track.Id,"Volume",at,s.Track.GainDb),pan=_project.Studio.Value(s.Track.Id,"Pan",at,s.Track.Pan);
 double trackGain=(s.Track.Muted||_project.Studio.Value(s.Track.Id,"Mute",at,0)>=0.5)?0:Math.Pow(10,db/20);double gain=(solo&&!_soloAudible.Contains(s.Track.Id))?0:trackGain;
 float left=s.Buffer[i*2],right=s.Buffer[i*2+1];
 float keyLeft=(float)(left*trackGain*(pan>0?1-pan:1)),keyRight=(float)(right*trackGain*(pan<0?1+pan:1));left*=(float)(gain*(pan>0?1-pan:1));right*=(float)(gain*(pan<0?1+pan:1));
 float[] target;if(s.Destination.HasValue&&_busBuffers.TryGetValue(s.Destination.Value,out target)){float l=left,r=right;s.DestinationDelay.Process(ref l,ref r);target[i*2]+=l;target[i*2+1]+=r;}else{float l=left,r=right;s.Latency.Process(ref l,ref r);output[(destination+i)*2]+=l;output[(destination+i)*2+1]+=r;}foreach(var send in s.Sends)if(_busBuffers.TryGetValue(send.TargetId,out target)){float l=left,r=right;send.Delay.Process(ref l,ref r);target[i*2]+=l*send.Gain;target[i*2+1]+=r*send.Gain;}float[] key;if(_sidechainBuffers.TryGetValue(s.Track.Id,out key)){key[i*2]=keyLeft;key[i*2+1]=keyRight;}peak=Math.Max(peak,Math.Max(Math.Abs(left),Math.Abs(right)));
 }s.Peak=Math.Max(peak,s.Peak*0.85f);}ProcessMasterInserts(output,destination,frames,beat,tempo);float master=(float)Math.Pow(10,_project.MasterGainDb/20);for(int i=0;i<frames*2;i++)output[destination*2+i]*=master;}
        private void ProcessMasterInserts(float[] output,int destination,int frames,double beat,double tempo){
         if(!_project.Studio.MasterProcessingEnabled||_masterInserts==null||_masterInserts.Count==0)return;int count=frames*2,offset=destination*2;
         if(_masterBuffer==null||_masterBuffer.Length<count){_masterBuffer=new float[count];_masterBackup=new float[count];}
         Array.Copy(output,offset,_masterBuffer,0,count);
         foreach(var entry in _masterInserts){
          if(entry.Bypassed||_failedMasterInserts.Contains(entry.Id))continue;
          Array.Copy(_masterBuffer,0,_masterBackup,0,count);
          try{
           if(entry.Plugin!=null){NativePlugin plugin;if(!_plugins.TryGetValue(entry.Id,out plugin))continue;
            foreach(var lane in _project.Studio.Automation)if(lane.Enabled&&lane.TrackId==Guid.Empty&&lane.Target.StartsWith("Plugin:"+plugin.Slot.Id.ToString()+":",StringComparison.Ordinal)){uint id=UInt32.Parse(lane.Target.Split(':')[2]);int stride=Math.Max(1,(frames+31)/32);for(int sample=0;sample<frames;sample+=stride)plugin.QueueParameter(id,lane.Evaluate(beat+sample*tempo/(60*_settings.SampleRate),0),sample);}
            plugin.Process(_masterBuffer,frames,EmptyEvents,0,tempo,beat,_playing,_project.TimeSignatureNumerator,_project.TimeSignatureDenominator);
           }else if(entry.Stock!=null&&entry.Stock.Enabled){StockEffectProcessor processor;if(_stock.TryGetValue(entry.Id,out processor)){float[] sidechain=null;if(entry.Stock.SidechainTrackId.HasValue)_sidechainBuffers.TryGetValue(entry.Stock.SidechainTrackId.Value,out sidechain);processor.Process(entry.Stock,_masterBuffer,frames,_settings.SampleRate,beat,tempo,EmptyEvents,0,_monitorBlock,destination,_liveBlock,_playing,sidechain);}}
          }catch(Exception ex){Array.Copy(_masterBackup,0,_masterBuffer,0,count);if(_failedMasterInserts.Add(entry.Id)){Warnings.Add("Master / "+entry.Name+": insert bypassed after processing error: "+ex.Message);OnFailure(ex);}}
         }
         Array.Copy(_masterBuffer,0,output,offset,count);
        }
        private void ProcessInsertPlugin(Scene s,NativePlugin plugin,int frames,double beat,double tempo){if(s.ProcessingFailed){Array.Clear(s.Buffer,0,s.Buffer.Length);return;}try{foreach(var lane in _project.Studio.Automation)if(lane.Enabled&&lane.TrackId==s.Track.Id&&lane.Target.StartsWith("Plugin:"+plugin.Slot.Id.ToString()+":",StringComparison.Ordinal)){uint id=UInt32.Parse(lane.Target.Split(':')[2]);int stride=Math.Max(1,(frames+31)/32);for(int offset=0;offset<frames;offset+=stride)plugin.QueueParameter(id,lane.Evaluate(beat+offset*tempo/(60*_settings.SampleRate),0),offset);}plugin.Process(s.Buffer,frames,s.Events,s.EventCount,tempo,beat,_playing,_project.TimeSignatureNumerator,_project.TimeSignatureDenominator);}catch(Exception ex){s.ProcessingFailed=true;OnFailure(ex);Array.Clear(s.Buffer,0,s.Buffer.Length);}}
        public void PrepareOffline(ProjectDocument project,double beat){SetProject(project);lock(_sync){_frame=Math.Round(beat*60*_settings.SampleRate/project.Tempo);_launcherFrame=_frame;_presentation.Reset(beat);_playing=true;}}
        public float[] RenderOffline(ProjectDocument project,int frames,double beat){SetProject(project);lock(_sync){_frame=Math.Round(beat*60*_settings.SampleRate/project.Tempo);_launcherFrame=_frame;_presentation.Reset(beat);_playing=true;}float[] result=new float[frames*2];float[] block=new float[_settings.BufferFrames*2];for(int start=0;start<frames;start+=_settings.BufferFrames){int n=Math.Min(_settings.BufferFrames,frames-start);Render(block,n,0);Array.Copy(block,0,result,start*2,n*2);}lock(_sync)_playing=false;return result;}
        public void ResetProject(){Stop();ReturnToArrangement();lock(_sync){foreach(NativePlugin p in _plugins.Values)p.Dispose();_plugins.Clear();_scenes.Clear();_audio.Clear();_launcherFrame=0;_presentation.Reset(0);_project=null;}}
        private void CloseDevices(){if(_audioInput!=null&&!Object.ReferenceEquals(_audioInput,_output)){_audioInput.Dispose();}_audioInput=null;if(_input!=null){_input.Dispose();_input=null;}if(_output!=null){_output.Dispose();_output=null;}if(_midiOut!=null){_midiOut.Dispose();_midiOut=null;}}
        public void Dispose(){CloseDevices();lock(_sync){foreach(NativePlugin p in _plugins.Values)p.Dispose();_plugins.Clear();_scenes.Clear();}}
    }
    public sealed class MidiRecordingSession
    {
        private readonly object _sync=new object();private readonly Dictionary<int,Tuple<double,int>> _held=new Dictionary<int,Tuple<double,int>>();private readonly List<MidiNote> _notes=new List<MidiNote>();private readonly long _start;private readonly double _tempo;public MidiRecordingSession(double tempo){_tempo=tempo;_start=Stopwatch.GetTimestamp();}
        public void Receive(MidiInputMessage e){double beat=Math.Max(0,(e.Timestamp-_start)/(double)Stopwatch.Frequency*_tempo/60);int key=((e.Status&15)<<8)|e.Data1;int status=e.Status&240;lock(_sync){Tuple<double,int> held;if(status==0x90&&e.Data2>0){if(_held.TryGetValue(key,out held))Finish(key,held,beat);_held[key]=Tuple.Create(beat,e.Data2);}else if(status==0x80||(status==0x90&&e.Data2==0)){if(_held.TryGetValue(key,out held)){Finish(key,held,beat);_held.Remove(key);}}}}
        private void Finish(int key,Tuple<double,int> held,double end){_notes.Add(new MidiNote{StartBeat=held.Item1,LengthBeats=Math.Max(0.0625,end-held.Item1),Pitch=key&127,Velocity=held.Item2});}
        public List<MidiNote> Stop(){lock(_sync){double beat=Math.Max(0,(Stopwatch.GetTimestamp()-_start)/(double)Stopwatch.Frequency*_tempo/60);foreach(KeyValuePair<int,Tuple<double,int>> held in _held)Finish(held.Key,held.Value,beat);_held.Clear();return ProjectFactory.CloneNotes(_notes);}}
    }
}
