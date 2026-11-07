using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using SlopStudio;

internal static class V12CoreTests
{
    private static string _root; private static int _failed;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action action) { try { action(); Console.WriteLine("PASS " + name); } catch (Exception ex) { _failed++; Console.WriteLine("FAIL " + name + " :: " + ex); } }
    private static void Event(BinaryWriter writer, int id, byte[] data) { writer.Write((byte)id); if(id>=192){int n=data.Length;while(n>=128){writer.Write((byte)((n&127)|128));n>>=7;}writer.Write((byte)n);}writer.Write(data); }
    private static object Scene(ArrangementEngine engine) { return ((IEnumerable)typeof(ArrangementEngine).GetField("_scenes", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(engine)).Cast<object>().First(); }
    [STAThread] private static int Main(string[] args)
    {
        _root = Path.Combine(Path.GetFullPath(args[0]), "run-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("SLOPSTUDIO_PROFILE", Path.Combine(_root, "profile"));
        Test("88-byte FL playlist items import and survive project save/reopen", delegate {
            string path=Path.Combine(_root,"fixture.flp"); using(var events=new MemoryStream()) using(var w=new BinaryWriter(events)) {
                Event(w,65,BitConverter.GetBytes((ushort)1)); byte[] note=new byte[24]; Array.Copy(BitConverter.GetBytes(96),0,note,8,4); Array.Copy(BitConverter.GetBytes((ushort)60),0,note,12,2); note[21]=80; Event(w,224,note);
                byte[] clip=new byte[88]; Array.Copy(BitConverter.GetBytes(384),clip,4); Array.Copy(BitConverter.GetBytes((ushort)20480),0,clip,4,2); Array.Copy(BitConverter.GetBytes((ushort)20481),0,clip,6,2); Array.Copy(BitConverter.GetBytes(384),0,clip,8,4); Event(w,233,clip);
                using(var output=new BinaryWriter(File.Create(path))){output.Write(Encoding.ASCII.GetBytes("FLhd"));output.Write(6);output.Write((short)0);output.Write((ushort)1);output.Write((ushort)96);output.Write(Encoding.ASCII.GetBytes("FLdt"));output.Write((int)events.Length);output.Write(events.ToArray());}
            }
            var p=ProjectExchange.ImportFlp(path).Project; Check(p.Tracks.Count==1&&p.Tracks[0].Regions.Count==1&&p.Tracks[0].Regions[0].StartBeat==4,"Playlist timing is wrong.");
            var reopened=ProjectFile.Deserialize(ProjectFile.Serialize(p)); Check(reopened.Tracks[0].Regions[0].Notes.Count==1,"MIDI note was lost.");
        });
        Test("Missing FL samples retain their timeline positions and warnings", delegate {
            string path=Path.Combine(_root,"missing-sample.flp");using(var events=new MemoryStream())using(var w=new BinaryWriter(events)){
                Event(w,199,Encoding.ASCII.GetBytes("26.1.1\0"));Event(w,64,BitConverter.GetBytes((ushort)3));Event(w,196,Encoding.Unicode.GetBytes("absent.wav\0"));
                byte[] clip=new byte[88];Array.Copy(BitConverter.GetBytes(192),clip,4);Array.Copy(BitConverter.GetBytes((ushort)20480),0,clip,4,2);Array.Copy(BitConverter.GetBytes((ushort)3),0,clip,6,2);Array.Copy(BitConverter.GetBytes(384),0,clip,8,4);Event(w,233,clip);
                using(var output=new BinaryWriter(File.Create(path))){output.Write(Encoding.ASCII.GetBytes("FLhd"));output.Write(6);output.Write((short)0);output.Write((ushort)1);output.Write((ushort)96);output.Write(Encoding.ASCII.GetBytes("FLdt"));output.Write((int)events.Length);output.Write(events.ToArray());}
            }
            var result=ProjectExchange.ImportFlp(path);var region=result.Project.Tracks.Single().Regions.Single();Check(region.StartBeat==2&&region.LengthBeats==4&&region.Name.StartsWith("[Missing]")&&region.MediaPath.EndsWith("absent.wav"),"Missing clip position or path lost.");
            Check(result.Warnings.Any(x=>x.Contains("silent region retained")),"Missing sample was not disclosed.");Check(ProjectFile.Deserialize(ProjectFile.Serialize(result.Project)).Tracks.Single().Regions.Single().MediaPath==region.MediaPath,"Missing sample reference lost on save.");
        });
        Test("Relink updates matching audio regions and undo restores original paths", delegate {
            string original=Path.Combine(_root,"missing-source.wav"), replacement=Path.Combine(_root,"replacement.wav");
            using(var writer=new ExportWaveWriter(replacement,48000,1,16,false))writer.Write(Enumerable.Repeat(.1f,4800).ToArray(),0,4800);
            var project=new ProjectDocument();var track=ProjectFactory.CreateTrack("Audio",TrackType.Audio);
            var a=ProjectFactory.CreateRegion("[Missing] One",2,4);a.MediaPath=original;var b=ProjectFactory.CreateRegion("[Missing] Two",8,2);b.MediaPath=original;
            track.Regions.Add(a);track.Regions.Add(b);project.Tracks.Add(track);
            var changed=ProjectMediaRelink.Replace(project,original,replacement);var history=new ProjectHistory();history.Execute(project,new SetProjectContentCommand("Relink",project,changed));
            Check(project.Tracks[0].Regions.All(r=>r.MediaPath==replacement&&!r.Name.StartsWith("[Missing]"))&&project.Tracks[0].Regions[0].StartBeat==2,"Relink lost source position or matching regions.");
            history.Undo(project);Check(project.Tracks[0].Regions.All(r=>r.MediaPath==original)&&File.Exists(replacement),"Undo changed the source file or failed to restore references.");
        });
        Test("MIDI reset sends only active notes and preserves tracking across graph refresh", delegate {
            var p=new ProjectDocument();var track=ProjectFactory.CreateTrack("Note",TrackType.Midi);var region=ProjectFactory.CreateRegion("Long",0,4);region.Notes.Add(new MidiNote{Pitch=60,LengthBeats=3.75});track.Regions.Add(region);p.Tracks.Add(track);
            using(var engine=new ArrangementEngine(new AppSettings{SampleRate=48000,BufferFrames=512})) {
                engine.PrepareOffline(p,0);var samples=new float[1024];engine.Render(samples,512,0);object scene=Scene(engine);var type=scene.GetType();
                Check((int)type.GetField("EventCount").GetValue(scene)==1,"Startup sent unneeded note-offs.");
                engine.SetProject(p);scene=Scene(engine);type=scene.GetType();type.GetField("ResetNotes").SetValue(scene,true);engine.Render(samples,512,0);
                var events=(MidiEventPacket[])type.GetField("Events").GetValue(scene);int count=(int)type.GetField("EventCount").GetValue(scene);
                Check(count==2&&(events[0].Status&240)==0x80&&events[0].Data1==60&&(events[1].Status&240)==0x90,"Reset failed to release and resume the active note.");
            }
        });
        Test("Master processing switch changes actual PCM and persists", delegate {
            string wave=Path.Combine(_root,"signal.wav");using(var writer=new ExportWaveWriter(wave,48000,2,32,true))writer.Write(Enumerable.Repeat(.2f,96000).ToArray(),0,96000);
            var p=new ProjectDocument();var t=ProjectFactory.CreateTrack("Audio",TrackType.Audio);var r=ProjectFactory.CreateRegion("Signal",0,2);r.MediaPath=wave;t.Regions.Add(r);p.Tracks.Add(t);
            var fx=StockEffects.Create(Guid.Empty,"Saturator");fx.Values["Drive"]=24;fx.Values["Output"]=0;p.Studio.Effects.Add(fx);
            float enabled,disabled;using(var engine=new ArrangementEngine(new AppSettings()))enabled=engine.RenderOffline(p,4096,0).Max(x => Math.Abs(x));
            p.Studio.MasterProcessingEnabled=false;using(var engine=new ArrangementEngine(new AppSettings()))disabled=engine.RenderOffline(p,4096,0).Max(x => Math.Abs(x));
            Check(enabled>disabled*1.5f,"Master processing switch did not change PCM.");Check(!ProjectFile.Deserialize(ProjectFile.Serialize(p)).Studio.MasterProcessingEnabled,"Switch did not persist.");
            var json=new JavaScriptSerializer{MaxJsonLength=64*1024*1024};var obj=(Dictionary<string,object>)json.DeserializeObject(ProjectFile.Serialize(p));((Dictionary<string,object>)obj["studio"]).Remove("MasterProcessingEnabled");
            Check(ProjectFile.Deserialize(json.Serialize(obj)).Studio.MasterProcessingEnabled,"Older projects should enable master processing by default.");
        });
        Test("Track and region colors survive save and undo", delegate {
            var p=new ProjectDocument();var t=ProjectFactory.CreateTrack("Colors",TrackType.Midi);var r=ProjectFactory.CreateRegion("Clip",0,4);t.Regions.Add(r);p.Tracks.Add(t);
            var next=p.Studio.Copy();next.ItemColors.Add(new StudioItemColor{Id=t.Id,HasBackground=true,BackgroundArgb=Color.Red.ToArgb(),HasText=true,TextArgb=Color.White.ToArgb()});next.ItemColors.Add(new StudioItemColor{Id=r.Id,Region=true,HasBackground=true,BackgroundArgb=Color.Blue.ToArgb()});
            var history=new ProjectHistory();history.Execute(p,new SetStudioCommand("Colors",p.Studio,next));var loaded=ProjectFile.Deserialize(ProjectFile.Serialize(p));Check(loaded.Studio.Background(t.Id,false,Color.Black).ToArgb()==Color.Red.ToArgb()&&loaded.Studio.Background(r.Id,true,Color.Black).ToArgb()==Color.Blue.ToArgb(),"Colors did not persist.");history.Undo(p);Check(p.Studio.ItemColors.Count==0,"Color undo failed.");
        });
        Test("Controller assignment persists and dispatches the chosen command", delegate {
            var s=new AppSettings();s.Integration.ControllerBindings[0]="ToggleMixer";string file=Path.Combine(_root,"settings.json");SettingsStore.Save(s,file);var loaded=SettingsStore.Load(file);Check(loaded.Integration.ControllerBindings[0]=="ToggleMixer","Binding lost.");
            var commands=new List<AppCommand>();using(var pad=new GameController()){pad.Bindings=loaded.Integration.ControllerBindings;pad.Apply(new GameControllerState{Buttons=1},commands.Add,delegate{});pad.Apply(new GameControllerState{Buttons=1},commands.Add,delegate{});}
            Check(commands.Count==1&&commands[0]==AppCommand.ToggleMixer,"Controller command or key repeat is wrong.");
        });
        Test("Multiple Lua Mod paths persist and removing one preserves both files", delegate {
            string one=Path.Combine(_root,"one.lua"),two=Path.Combine(_root,"two.lua");File.WriteAllText(one,"slop.call(\"set_tempo\", {bpm=129})");File.WriteAllText(two,"slop.call(\"set_tempo\", {bpm=130})");
            LuaModRegistry.Save(new[]{one,two});Check(LuaModRegistry.Load().Count==2,"Mods not saved.");var operations=LuaMods.Load(one);var changed=LuaMods.Apply(new ProjectDocument(),operations);Check(changed.Tempo==129,"Lua command not applied.");LuaModRegistry.Save(new[]{two});Check(File.Exists(one)&&File.Exists(two)&&LuaModRegistry.Load().Count==1,"Removal deleted a Mod or failed.");
        });
        Test("ASIO panel rejects invalid driver before launching a helper", delegate { bool rejected=false;try{AsioDrivers.ShowControlPanel("invalid");}catch(InvalidDataException){rejected=true;}Check(rejected,"Invalid ASIO driver accepted."); });
        return _failed==0?0:1;
    }
}
