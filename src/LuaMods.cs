using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace SlopStudio {
 public sealed class LuaOperation {public string Command;public Dictionary<string,object> Arguments;}
 public static class LuaMods {
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate int Host(IntPtr json,IntPtr result,int capacity);
  [DllImport("SlopControl.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int slc_lua(byte[] source,Host callback,[Out]byte[] error,int capacity);
  private static string ReadUtf8(IntPtr p){int n=0;while(n<65536&&Marshal.ReadByte(p,n)!=0)n++;byte[] b=new byte[n];Marshal.Copy(p,b,0,n);return Encoding.UTF8.GetString(b);}
  private static string ToLua(object o){if(o==null)return "nil";if(o is bool)return (bool)o?"true":"false";if(o is string)return "\""+((string)o).Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r")+"\"";var d=o as IDictionary;if(d!=null){var parts=new List<string>();foreach(DictionaryEntry e in d)parts.Add("["+ToLua(Convert.ToString(e.Key))+"]="+ToLua(e.Value));return "{"+String.Join(",",parts.ToArray())+"}";}return Convert.ToString(o,System.Globalization.CultureInfo.InvariantCulture);}
  public static void RunWorker(string source,string report){
   var j=new JavaScriptSerializer{MaxJsonLength=1024*1024};var ops=new List<LuaOperation>();try{
    if(new FileInfo(source).Length>256*1024)throw new InvalidDataException("Lua Mod exceeds 256 KiB.");
    Host host=delegate(IntPtr request,IntPtr output,int capacity){try{if(ops.Count>=4096)throw new InvalidDataException("Lua command count exceeds 4096.");var root=j.Deserialize<Dictionary<string,object>>(ReadUtf8(request));string command=Convert.ToString(root["command"]);if(Array.IndexOf(new[]{"create_track","add_midi_note","set_tempo","set_track_mix","set_automation","transport"},command)<0)throw new InvalidOperationException("Unsupported Lua command: "+command);var args=j.ConvertToType<Dictionary<string,object>>(root["arguments"]);object result=true;if(command=="create_track"){Guid id=Guid.NewGuid();args["id"]=id.ToString();result=new Dictionary<string,object>{{"trackId",id.ToString()}};}ops.Add(new LuaOperation{Command=command,Arguments=args});byte[] bytes=Encoding.UTF8.GetBytes("return "+ToLua(result)+"\0");if(bytes.Length>capacity)throw new InvalidDataException("Lua response too large.");Marshal.Copy(bytes,0,output,bytes.Length);return 0;}catch(Exception ex){byte[] bytes=Encoding.UTF8.GetBytes(ex.Message+"\0");Marshal.Copy(bytes,0,output,Math.Min(bytes.Length,capacity));return -1;}};
    byte[] error=new byte[4096];int status=slc_lua(Encoding.UTF8.GetBytes(File.ReadAllText(source)+"\0"),host,error,error.Length);GC.KeepAlive(host);if(status!=0)throw new InvalidDataException(Encoding.UTF8.GetString(error).TrimEnd('\0'));
    File.WriteAllText(report,j.Serialize(new {operations=ops}),new UTF8Encoding(false));Environment.ExitCode=0;
   }catch(Exception ex){File.WriteAllText(report,j.Serialize(new {error=ex.Message}));Environment.ExitCode=1;}
  }
  public static List<LuaOperation> Load(string source){
   string report=Path.Combine(Path.GetTempPath(),"SlopLua-"+Guid.NewGuid().ToString("N")+".json");
   try{using(var process=Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SlopStudio.exe"),"--lua-run \""+source+"\" \""+report+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){if(!process.WaitForExit(2500)){process.Kill();throw new TimeoutException("Lua Mod stopped: the 2.5 second execution limit was exceeded.");}var j=new JavaScriptSerializer{MaxJsonLength=1024*1024};var obj=j.Deserialize<Dictionary<string,object>>(File.ReadAllText(report));if(process.ExitCode!=0||obj.ContainsKey("error"))throw new InvalidDataException(Convert.ToString(obj["error"]));return j.ConvertToType<List<LuaOperation>>(obj["operations"]);}}finally{try{File.Delete(report);}catch{}}
  }
  public static ProjectDocument Apply(ProjectDocument source,IList<LuaOperation> ops){
   var p=ProjectFactory.Clone(source);
   foreach(var op in ops){var a=op.Arguments;switch(op.Command){
    case "create_track":var track=ProjectFactory.CreateTrack(Convert.ToString(a["name"]),Convert.ToString(a["type"])=="audio"?TrackType.Audio:TrackType.Midi);track.Id=Guid.Parse(Convert.ToString(a["id"]));p.Tracks.Add(track);break;
    case "set_tempo":p.Tempo=Convert.ToDouble(a["bpm"]);break;
    case "add_midi_note":var t=p.Tracks.Find(x=>x.Id==Guid.Parse(Convert.ToString(a["trackId"])));if(t==null||t.Type!=TrackType.Midi)throw new InvalidDataException("Lua note destination is not a MIDI track.");if(t.Regions.Count==0)t.Regions.Add(ProjectFactory.CreateRegion("Lua MIDI",0,4));var r=t.Regions[0];double start=Convert.ToDouble(a["start"]),length=Convert.ToDouble(a["duration"]);r.LengthBeats=Math.Max(r.LengthBeats,start+length);r.Notes.Add(new MidiNote{StartBeat=start,LengthBeats=length,Pitch=Convert.ToInt32(a["pitch"]),Velocity=Convert.ToInt32(a["velocity"])});break;
    case "set_track_mix":var mix=p.Tracks.Find(x=>x.Id==Guid.Parse(Convert.ToString(a["trackId"])));if(mix==null)throw new InvalidDataException("Lua track unavailable.");if(a.ContainsKey("gainDb"))mix.GainDb=Convert.ToDouble(a["gainDb"]);if(a.ContainsKey("pan"))mix.Pan=Convert.ToDouble(a["pan"]);if(a.ContainsKey("mute"))mix.Muted=Convert.ToBoolean(a["mute"]);if(a.ContainsKey("solo"))mix.Solo=Convert.ToBoolean(a["solo"]);break;
    case "set_automation":var lane=new AutomationLane{TrackId=Guid.Parse(Convert.ToString(a["trackId"])),Target=Convert.ToString(a["target"])};lane.Points.Add(new AutomationPoint{Beat=Convert.ToDouble(a["beat"]),Value=Convert.ToDouble(a["value"])});p.Studio.Automation.RemoveAll(x=>x.TrackId==lane.TrackId&&x.Target==lane.Target);p.Studio.Automation.Add(lane);break;
   }}
   ProjectValidator.Validate(p);return p;
  }
 }
 public sealed class SetProjectContentCommand:IProjectCommand {
  private ProjectDocument _before,_after;private string _name;
  public SetProjectContentCommand(string name,ProjectDocument before,ProjectDocument after){_name=name;_before=ProjectFactory.Clone(before);_after=ProjectFactory.Clone(after);}
  public string Description{get{return _name;}}
  private void Set(ProjectDocument to,ProjectDocument from){var p=ProjectFactory.Clone(from);to.Name=p.Name;to.Tempo=p.Tempo;to.TimeSignatureNumerator=p.TimeSignatureNumerator;to.TimeSignatureDenominator=p.TimeSignatureDenominator;to.Tracks=p.Tracks;to.Studio=p.Studio;to.MasterGainDb=p.MasterGainDb;to.CycleEnabled=p.CycleEnabled;to.CycleStartBeat=p.CycleStartBeat;to.CycleEndBeat=p.CycleEndBeat;}
  public void Apply(ProjectDocument p){Set(p,_after);}public void Revert(ProjectDocument p){Set(p,_before);}
 }
}