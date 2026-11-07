using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
namespace SlopStudio
{
 public sealed class AutomationPoint{public double Beat{get;set;}public double Value{get;set;}}
 public sealed class AutomationLane
 {
  public Guid TrackId{get;set;}public string Target{get;set;}public bool Enabled{get;set;}public List<AutomationPoint> Points{get;set;}
  public AutomationLane(){Target="Volume";Enabled=true;Points=new List<AutomationPoint>();}
  public double Evaluate(double beat,double fallback){if(!Enabled||Points.Count==0)return fallback;if(beat<=Points[0].Beat)return Points[0].Value;for(int i=1;i<Points.Count;i++)if(beat<Points[i].Beat){var a=Points[i-1];var b=Points[i];return a.Value+(b.Value-a.Value)*(beat-a.Beat)/(b.Beat-a.Beat);}return Points[Points.Count-1].Value;}
 }
 public sealed class LauncherCell{public Guid TrackId{get;set;}public int Scene{get;set;}public Guid RegionId{get;set;}}
 public sealed class InputRoute{public Guid TrackId{get;set;}public int Channel{get;set;}public int Channels{get;set;}public bool Monitor{get;set;}public InputRoute(){Channels=1;}}
 public sealed class CompSlice{public Guid RegionId{get;set;}public double StartBeat{get;set;}public double EndBeat{get;set;}}
 public sealed class CompLane{public Guid TrackId{get;set;}public List<Guid> Takes{get;set;}public List<CompSlice> Slices{get;set;}public CompLane(){Takes=new List<Guid>();Slices=new List<CompSlice>();}}
 public sealed class ChordEvent{public double Beat{get;set;}public double Length{get;set;}public string Name{get;set;}}
 public sealed class VideoReference{public string Path{get;set;}public double StartBeat{get;set;}}
 public sealed class StudioItemColor{public Guid Id{get;set;}public bool Region{get;set;}public bool HasBackground{get;set;}public bool HasText{get;set;}public int BackgroundArgb{get;set;}public int TextArgb{get;set;}}
 public sealed class StudioState
 {
  public string ImportNotes{get;set;}public List<ChordEvent> Chords{get;set;}public VideoReference Video{get;set;}public bool MasterProcessingEnabled{get;set;}public List<StudioItemColor> ItemColors{get;set;}public ProjectMetadata Metadata{get;set;}
  public List<TrackStack> Stacks{get;set;}public List<Guid> Buses{get;set;}public List<AuxSend> Sends{get;set;}public List<SamplerSettings> Samplers{get;set;}public List<TrackInsertOrder> InsertOrders{get;set;}public List<PluginSlot> MasterPlugins{get;set;}public List<StockEffectSlot> Effects{get;set;}public List<AutomationLane> Automation{get;set;}public List<LauncherCell> Cells{get;set;}public List<InputRoute> Inputs{get;set;}public List<CompLane> Comps{get;set;}
  public StudioState(){MasterProcessingEnabled=true;Metadata=new ProjectMetadata();ItemColors=new List<StudioItemColor>();Stacks=new List<TrackStack>();Buses=new List<Guid>();Sends=new List<AuxSend>();Samplers=new List<SamplerSettings>();InsertOrders=new List<TrackInsertOrder>();MasterPlugins=new List<PluginSlot>();Chords=new List<ChordEvent>();Effects=new List<StockEffectSlot>();Automation=new List<AutomationLane>();Cells=new List<LauncherCell>();Inputs=new List<InputRoute>();Comps=new List<CompLane>();}
  public StudioState Copy(){var j=new JavaScriptSerializer{MaxJsonLength=8*1024*1024};return j.Deserialize<StudioState>(j.Serialize(this));}
  public SamplerSettings Sampler(Guid track){var s=Samplers.Find(a=>a.TrackId==track);return s??new SamplerSettings{TrackId=track};}
  public InputRoute Route(Guid track){var r=Inputs.Find(delegate(InputRoute a){return a.TrackId==track;});return r??new InputRoute{TrackId=track};}
  public StudioItemColor ItemColor(Guid id,bool region){return ItemColors==null?null:ItemColors.Find(c=>c.Id==id&&c.Region==region);}
  public Color Background(Guid id,bool region,Color fallback){var c=ItemColor(id,region);return c!=null&&c.HasBackground?Color.FromArgb(c.BackgroundArgb):fallback;}
  public Color TextColor(Guid id,bool region,Color fallback){var c=ItemColor(id,region);return c!=null&&c.HasText?Color.FromArgb(c.TextArgb):fallback;}
  public double Value(Guid track,string target,double beat,double fallback){var l=Automation.Find(delegate(AutomationLane a){return a.TrackId==track&&a.Target==target;});return l==null?fallback:l.Evaluate(beat,fallback);}
  public static bool IsPluginTarget(string target){if(target==null)return false;string[] parts=target.Split(':');Guid id;uint param;return parts.Length==3&&parts[0]=="Plugin"&&Guid.TryParse(parts[1],out id)&&UInt32.TryParse(parts[2],out param);}
  public void Validate(ProjectDocument p){
   if(Metadata==null)Metadata=new ProjectMetadata();Metadata.Validate();
   if(Automation==null||Cells==null||Inputs==null||Comps==null||Automation.Count>1024||Cells.Count>4096||Inputs.Count>128||Comps.Count>128)throw new InvalidDataException("Studio state exceeds limits.");
   if(ItemColors==null)ItemColors=new List<StudioItemColor>();if(ItemColors.Count>ProjectValidator.MaxRegions+ProjectValidator.MaxTracks||ItemColors.Any(c=>c==null||c.Id==Guid.Empty)||ItemColors.GroupBy(c=>new{c.Id,c.Region}).Any(g=>g.Count()>1))throw new InvalidDataException("Invalid item colors.");
   if(Chords==null)Chords=new List<ChordEvent>();if(Chords.Count>4096)throw new InvalidDataException("Too many chords.");foreach(var chord in Chords){if(chord==null||Double.IsNaN(chord.Beat)||Double.IsInfinity(chord.Beat)||chord.Beat<0||chord.Beat>ProjectValidator.MaxBeat||Double.IsNaN(chord.Length)||Double.IsInfinity(chord.Length)||chord.Length<.25||chord.Length>ProjectValidator.MaxBeat)throw new InvalidDataException("Invalid chord timing.");ChordProgression.Pitches(chord.Name);}if(Video!=null&&(String.IsNullOrWhiteSpace(Video.Path)||Video.Path.Length>2048||Video.StartBeat<0||Double.IsNaN(Video.StartBeat)||Double.IsInfinity(Video.StartBeat)))throw new InvalidDataException("Invalid movie reference.");
   if(Stacks==null)Stacks=new List<TrackStack>();if(Buses==null)Buses=new List<Guid>();if(Sends==null)Sends=new List<AuxSend>();if(Stacks.Count>128||Buses.Count>128||Sends.Count>1024||Stacks.Any(s=>s==null||s.Children==null||s.ParentId==Guid.Empty||s.Children.Count>128||s.Children.Contains(s.ParentId)||s.Children.Distinct().Count()!=s.Children.Count)||Sends.Any(s=>s==null||s.SourceId==Guid.Empty||s.TargetId==Guid.Empty||s.SourceId==s.TargetId||Double.IsNaN(s.GainDb)||Double.IsInfinity(s.GainDb)||s.GainDb< -96||s.GainDb>12))throw new InvalidDataException("Invalid track routing.");if(Stacks.GroupBy(s=>s.ParentId).Any(g=>g.Count()>1)||Stacks.SelectMany(s=>s.Children).GroupBy(id=>id).Any(g=>g.Count()>1)||Buses.Distinct().Count()!=Buses.Count||Sends.GroupBy(s=>new{s.SourceId,s.TargetId}).Any(g=>g.Count()>1))throw new InvalidDataException("Duplicate track routing.");if(Samplers==null)Samplers=new List<SamplerSettings>();if(Samplers.Count>128||Samplers.Any(a=>a==null)||Samplers.GroupBy(a=>a.TrackId).Any(g=>g.Count()>1))throw new InvalidDataException("Invalid Sampler state.");foreach(var sample in Samplers)sample.Validate();if(InsertOrders==null)InsertOrders=new List<TrackInsertOrder>();if(MasterPlugins==null)MasterPlugins=new List<PluginSlot>();if(MasterPlugins.Count>16||MasterPlugins.Any(s=>s==null||s.Id==Guid.Empty||s.Path==null||s.Path.Length>32767||String.IsNullOrWhiteSpace(s.ClassId)||s.ClassId.Length>1024||(s.Format!="VST3"&&s.Format!="CLAP")||s.State==null||s.State.Length>4*1024*1024||s.IsInstrument))throw new InvalidDataException("Master plug-in data is invalid.");var pluginIds=new HashSet<Guid>();if(p.Tracks!=null)foreach(var track in p.Tracks)if(track!=null&&track.Plugins!=null)foreach(var plugin in track.Plugins)if(plugin!=null)pluginIds.Add(plugin.Id);foreach(var slot in MasterPlugins){if(!pluginIds.Add(slot.Id))throw new InvalidDataException("Duplicate master plug-in identifier.");slot.Name=ProjectValidator.NormalizeName(slot.Name,"Master plug-in name");}if(InsertOrders.Count>129||InsertOrders.Any(o=>o==null||o.Items==null||o.Items.Count>2048||o.Items.Distinct().Count()!=o.Items.Count)||InsertOrders.GroupBy(o=>o.TrackId).Any(g=>g.Count()>1))throw new InvalidDataException("Invalid insert order.");if(Effects==null)Effects=new List<StockEffectSlot>();if(Effects.Count>1024)throw new InvalidDataException("Stock effect count exceeds 1024.");foreach(var effect in Effects)StockEffects.Validate(effect);if(p.Tracks!=null)TrackRouting.ProcessingOrder(p);
   foreach(var lane in Automation){if(lane==null||lane.Points==null||lane.Points.Count>16384||!(lane.Target=="Volume"||lane.Target=="Pan"||lane.Target=="Mute"||IsPluginTarget(lane.Target)))throw new InvalidDataException("Invalid automation lane.");double previous=-1;foreach(var pt in lane.Points){if(Double.IsNaN(pt.Beat)||Double.IsInfinity(pt.Beat)||pt.Beat<0||pt.Beat>ProjectValidator.MaxBeat||pt.Beat<=previous||Double.IsNaN(pt.Value)||Double.IsInfinity(pt.Value)||pt.Value<(lane.Target=="Volume"?-96:lane.Target=="Pan"?-1:0)||pt.Value>(lane.Target=="Volume"?12:1))throw new InvalidDataException("Automation points are invalid or unordered.");previous=pt.Beat;}}
   var groups=Automation.FindAll(a=>IsPluginTarget(a.Target)).GroupBy(a=>a.TrackId+":"+a.Target.Split(':')[1]);foreach(var group in groups)if(group.Count()>4)throw new InvalidDataException("At most four automated parameters per plug-in instance are supported.");
   foreach(var c in Cells)if(c==null||c.Scene<0||c.Scene>63)throw new InvalidDataException("Invalid launcher scene.");
   foreach(var r in Inputs)if(r==null||r.Channel<0||r.Channel>31||r.Channels<1||r.Channels>2||r.Channel+r.Channels>32)throw new InvalidDataException("Invalid input route.");
   foreach(var c in Comps){if(c==null||c.Takes==null||c.Slices==null||c.Slices.Count>4096)throw new InvalidDataException("Invalid comp lane.");foreach(var s in c.Slices)if(s.StartBeat<0||s.EndBeat<=s.StartBeat||s.EndBeat>ProjectValidator.MaxBeat)throw new InvalidDataException("Invalid comp segment.");}
  }
 }
 public sealed class SetStudioCommand:IProjectCommand
 {
  private StudioState _before,_after;private string _name;
  public SetStudioCommand(string name,StudioState before,StudioState after){_name=name;_before=before.Copy();_after=after.Copy();}
  public string Description{get{return _name;}}public void Apply(ProjectDocument p){p.Studio=_after.Copy();}public void Revert(ProjectDocument p){p.Studio=_before.Copy();}
 }
}
