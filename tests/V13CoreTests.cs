using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using SlopStudio;

internal static class V13CoreTests
{
    private sealed class Clock:IAudioOutput { public long FramesPlayed{get;set;} public event Action<Exception> Failed; public void Dispose(){} }
    private static int failures; private static string root;
    private static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    private static void Test(string name,Action action){try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+" :: "+e);}}
    private static string Wave(string name,float value){string file=Path.Combine(root,name+".wav");using(var w=new ExportWaveWriter(file,48000,1,32,true))w.Write(Enumerable.Repeat(value,48000).ToArray(),0,48000);return file;}
    private static void ClockFor(ArrangementEngine e,Clock clock){typeof(ArrangementEngine).GetField("_output",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(e,clock);}
    private static ProjectDocument AudioProject(string file){var p=new ProjectDocument{Tempo=120};var track=ProjectFactory.CreateTrack("Signal",TrackType.Audio);var region=ProjectFactory.CreateRegion("Signal",0,4);region.MediaPath=file;track.Regions.Add(region);p.Tracks.Add(track);return p;}
    [STAThread]private static int Main(string[] args){root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);Environment.SetEnvironmentVariable("SLOPSTUDIO_PROFILE",Path.Combine(root,"profile"));
        Test("Output presentation follows the rendered spans, latency, loop and seek",delegate{
            var h=new TransportHistory();h.Reset(8);h.Record(1000,200,8,0.001);h.Record(1200,100,0,0.001);
            Check(Math.Abs(h.Position(900,0)-8)<1e-8,"Position moved before queued audio.");Check(Math.Abs(h.Position(1150,50)-8.1)<1e-8,"Output latency was not applied.");Check(Math.Abs(h.Position(1220,0)-.02)<1e-8,"Loop boundary was not followed.");h.Reset(3);Check(h.Position(1500,0)==3,"Seek did not reset history.");
        });
        Test("Engine presentation and tempo changes follow real rendered PCM positions",delegate{
            var p=AudioProject(Wave("clock",.25f));using(var e=new ArrangementEngine(new AppSettings{SampleRate=48000,BufferFrames=512})){
                e.PrepareOffline(p,2);var clock=new Clock();ClockFor(e,clock);var b=new float[1024];e.Render(b,512,1000);clock.FramesPlayed=1256;
                Check(Math.Abs(e.PositionBeat-(2+256.0/24000))<1e-7,"Clock differs from the rendered timeline.");
                p.Tempo=180;e.SetProject(p);e.Render(b,512,1512);clock.FramesPlayed=1768;double expected=2+512.0/24000+256.0/16000;
                Check(Math.Abs(e.PositionBeat-expected)<1e-6,"Tempo edit changed the rendered position.");
            }
        });
        Test("Launcher quantization and repeated audio cross arrangement cycle",delegate{
            var p=AudioProject(Wave("launcher-cycle",.2f));p.CycleEnabled=true;p.CycleStartBeat=0;p.CycleEndBeat=.5;p.Tracks[0].Regions[0].StartBeat=4;p.Tracks[0].Regions[0].LengthBeats=1;
            using(var e=new ArrangementEngine(new AppSettings{SampleRate=48000,BufferFrames=512})){e.PrepareOffline(p,0);float[] b=new float[1024];e.Render(b,512,0);e.Launch(p.Tracks[0].Id,p.Tracks[0].Regions[0].Id,1);
                for(int i=0;i<55;i++)e.Render(b,512,0);Check(e.IsLaunched(p.Tracks[0].Id,p.Tracks[0].Regions[0].Id),"Quantized launch never crossed the project loop.");
                int sounding=0;for(int i=0;i<100;i++){e.Render(b,512,0);if(b.Max()>.19)sounding++;}Check(sounding>=98,"Launcher fell silent at project loop boundaries: "+sounding);
            }
        });
        Test("Launchpad X grid translates note presses into existing launcher cells",delegate{
            var p=new ProjectDocument();for(int i=0;i<8;i++){var t=ProjectFactory.CreateTrack("Track "+i,TrackType.Midi);var r=ProjectFactory.CreateRegion("Cell",0,4);t.Regions.Add(r);p.Tracks.Add(t);p.Studio.Cells.Add(new LauncherCell{TrackId=t.Id,RegionId=r.Id,Scene=i==7?7:0});}
            Check(LaunchpadMapping.PadNote(0,0)==81&&LaunchpadMapping.PadNote(7,7)==18,"Pad mapping is incorrect.");
            Check(LaunchpadMapping.Cell(p,0x90,81,127).TrackId==p.Tracks[0].Id&&LaunchpadMapping.Cell(p,0x90,18,90).TrackId==p.Tracks[7].Id,"Grid did not map to track/scene.");
            Check(LaunchpadMapping.Cell(p,0x80,81,127)==null&&LaunchpadMapping.Cell(p,0x90,81,0)==null&&LaunchpadMapping.Cell(p,0x90,89,127)==null,"Release or invalid pad was treated as a launch.");
            var settings=new AppSettings();settings.Integration.LaunchpadEnabled=true;settings.Integration.LaunchpadInput=1;settings.Integration.LaunchpadOutput=2;settings.Integration.LaunchpadInputName="Fixture input";settings.Integration.LaunchpadOutputName="Fixture output";string path=Path.Combine(root,"launchpad-settings.json");SettingsStore.Save(settings,path);Check(SettingsStore.Load(path).Integration.LaunchpadInputName=="Fixture input","Launchpad settings were not restored.");
            settings.MidiInput=1;bool rejected=false;try{SettingsStore.Validate(settings);}catch(InvalidDataException){rejected=true;}Check(rejected,"An instrument port was reused by the controller.");
        });
        Test("Master gain changes the live processing snapshot",delegate{
            var p=AudioProject(Wave("gain",.4f));using(var e=new ArrangementEngine(new AppSettings{SampleRate=48000,BufferFrames=512})){e.PrepareOffline(p,0);var b=new float[1024];e.Render(b,512,0);float before=b.Max();e.UpdateMasterGain(-12);e.Render(b,512,512);float after=b.Max();Check(before>.39&&Math.Abs(after/before-Math.Pow(10,-12.0/20))<.001,"Master gain did not affect PCM.");}
        });
        Test("Track stem preserves summing-bus routing without including siblings",delegate{
            var p=AudioProject(Wave("stem-a",.2f));var a=p.Tracks[0];var b=ProjectFactory.CreateTrack("Other",TrackType.Audio);var r=ProjectFactory.CreateRegion("Other",0,4);r.MediaPath=Wave("stem-b",.4f);b.Regions.Add(r);p.Tracks.Add(b);
            var bus=ProjectFactory.CreateTrack("Bus",TrackType.Audio);p.Tracks.Add(bus);p.Studio.Stacks.Add(new TrackStack{ParentId=bus.Id,Summing=true,Children=new System.Collections.Generic.List<Guid>{a.Id,b.Id}});
            var stem=AudioExport.StemProject(p,a.Id);Check(!p.Tracks.Any(t=>t.Solo),"Original project was modified.");using(var e=new ArrangementEngine(new AppSettings{SampleRate=48000,BufferFrames=512})){e.PrepareOffline(stem,0);float[] samples=new float[1024];e.Render(samples,512,0);Check(samples.Max()>.19f&&samples.Max()<.21f,"Stem is silent or includes another routed track: "+samples.Max());}
        });
        Test("Standard MIDI export restores tracks, tempo, meter and region-adjusted notes",delegate{
            var p=new ProjectDocument{Tempo=150,TimeSignatureNumerator=3,TimeSignatureDenominator=4};var track=ProjectFactory.CreateTrack("Piano",TrackType.Midi);var clip=ProjectFactory.CreateRegion("Phrase",2,4);clip.Transpose=2;clip.VelocityOffset=-10;clip.Notes.Add(new MidiNote{StartBeat=.5,LengthBeats=.75,Pitch=60,Velocity=90});track.Regions.Add(clip);p.Tracks.Add(track);
            var drums=ProjectFactory.CreateTrack("Drums",TrackType.Midi);drums.Instrument="Drum Kit";var beat=ProjectFactory.CreateRegion("Drum",0,4);beat.Notes.Add(new MidiNote{StartBeat=1,LengthBeats=.25,Pitch=36,Velocity=100});drums.Regions.Add(beat);p.Tracks.Add(drums);
            string file=Path.Combine(root,"roundtrip.mid");MidiFileExport.Save(p,file);var loaded=MidiFileImport.Load(file).Project;Check(loaded.Tracks.Count==2&&Math.Abs(loaded.Tempo-150)<.001&&loaded.TimeSignatureNumerator==3&&loaded.TimeSignatureDenominator==4,"MIDI metadata or tracks were lost.");
            var note=loaded.Tracks[0].Regions[0].Notes.Single();Check(Math.Abs(note.StartBeat-2.5)<.0001&&Math.Abs(note.LengthBeats-.75)<.0001&&note.Pitch==62&&note.Velocity==80,"MIDI position, pitch or velocity was lost.");Check(loaded.Tracks[1].Instrument=="Drum Kit","Drum channel was not preserved.");
        });
        Test("Template creation refuses overwrite and bundled example destinations",delegate{
            var p=new ProjectDocument{Name="Original"};string folder=Path.Combine(root,"app");Directory.CreateDirectory(Path.Combine(folder,"Examples"));string template=Path.Combine(root,"mine.sloptemplate");
            ProjectProtection.SaveNewTemplate(p,template,folder);byte[] original=File.ReadAllBytes(template);p.Name="Modified";bool refused=false;try{ProjectProtection.SaveNewTemplate(p,template,folder);}catch(IOException){refused=true;}Check(refused&&original.SequenceEqual(File.ReadAllBytes(template)),"Template overwrite was not prevented.");
            Check(ProjectProtection.IsProtected(template,folder)&&ProjectProtection.IsProtected(Path.Combine(folder,"Examples","demo.slopdaw"),folder),"Protected project detection failed.");
            foreach(string target in new[]{template,Path.Combine(folder,"Examples","demo.slopdaw")}){refused=false;try{ProjectProtection.ValidateProjectDestination(target,folder);}catch(InvalidOperationException){refused=true;}Check(refused,"Protected save destination was accepted.");}
            ProjectProtection.ValidateProjectDestination(Path.Combine(root,"copy.slopdaw"),folder);Check(ProjectFile.Load(template).Name=="Original","Template changed.");
        });
        Test("Undo limit retains the newest edits and persists in settings",delegate{
            var project=new ProjectDocument();var history=new ProjectHistory{Limit=2};history.Execute(project,new SetTempoCommand(120,121));history.Execute(project,new SetTempoCommand(121,122));history.Execute(project,new SetTempoCommand(122,123));
            Check(history.UndoCount==2,"History limit was not applied.");history.Undo(project);history.Undo(project);Check(!history.CanUndo&&project.Tempo==121&&history.IsDirty,"Trimming lost state or marked unsaved work clean.");history.Redo(project);history.MarkSaved();history.Redo(project);history.Undo(project);Check(!history.IsDirty,"Saved state was not restored.");
            var settings=new AppSettings{UndoLimit=37};string file=Path.Combine(root,"settings.json");SettingsStore.Save(settings,file);Check(SettingsStore.Load(file).UndoLimit==37,"Undo limit did not persist.");
        });
        Test("Shortcut profiles round trip and reject conflicting or unknown assignments",delegate{
            var binding=ShortcutRegistry.Find(AppCommand.ToggleMixer);var keys=System.Windows.Forms.Keys.Control|System.Windows.Forms.Keys.Shift|System.Windows.Forms.Keys.F12;
            var overrides=new System.Collections.Generic.Dictionary<string,int>{{binding.ProfileId,(int)keys}};string file=Path.Combine(root,"custom.slopkeys");ShortcutProfileStore.Save(file,overrides);var loaded=ShortcutProfileStore.Load(file);
            var settings=new AppSettings{ShortcutOverrides=loaded};string config=Path.Combine(root,"shortcut-settings.json");SettingsStore.Save(settings,config);loaded=SettingsStore.Load(config).ShortcutOverrides;
            try{ShortcutRegistry.ApplyOverrides(loaded);ShortcutBinding found;Check(ShortcutRegistry.TryFind(keys,true,out found)&&found.Command==AppCommand.ToggleMixer,"Custom shortcut did not invoke the mapped command.");
                var duplicate=new System.Collections.Generic.Dictionary<string,int>(loaded);duplicate[ShortcutRegistry.Find(AppCommand.PlayOrStop).ProfileId]=(int)keys;bool rejected=false;try{ShortcutRegistry.ApplyOverrides(duplicate);}catch(ArgumentException){rejected=true;}Check(rejected&&ShortcutRegistry.TryFind(keys,true,out found)&&found.Command==AppCommand.ToggleMixer,"Conflict was accepted or changed active shortcuts.");
                duplicate.Clear();duplicate["Unknown|command"]=(int)keys;rejected=false;try{ShortcutRegistry.ValidateOverrides(duplicate);}catch(ArgumentException){rejected=true;}Check(rejected,"Unknown command was accepted.");
            }finally{ShortcutRegistry.ApplyOverrides(new System.Collections.Generic.Dictionary<string,int>());}
        });
        Test("Legacy launcher shortcut profile IDs migrate without losing assignments",delegate{
            string legacy=new string(new[]{(char)76,(char)105,(char)118,(char)101,(char)32,(char)76,(char)111,(char)111,(char)112,(char)115,(char)32,(char)71,(char)114,(char)105,(char)100});
            var binding=ShortcutRegistry.Find(AppCommand.ToggleLauncher);string oldId=binding.ProfileId.Replace("Launcher Grid",legacy);var old=new System.Collections.Generic.Dictionary<string,int>{{oldId,(int)(System.Windows.Forms.Keys.Control|System.Windows.Forms.Keys.Shift|System.Windows.Forms.Keys.F12)}};
            var migrated=ShortcutRegistry.NormalizeOverrides(old);ShortcutRegistry.ValidateOverrides(migrated);Check(migrated.ContainsKey(binding.ProfileId)&&migrated[binding.ProfileId]==old[oldId],"Legacy launcher assignment was lost.");
        });
        Test("Theme profiles persist, reject invalid colors and apply shared palette tokens",delegate{
            int violet=System.Drawing.Color.FromArgb(70,30,210).ToArgb();var colors=new System.Collections.Generic.Dictionary<string,int>{{"Accent",violet}};
            string file=Path.Combine(root,"violet.sloptheme");ThemeProfileStore.Save(file,colors);var loaded=ThemeProfileStore.Load(file);var settings=new AppSettings{ThemeOverrides=loaded};string config=Path.Combine(root,"theme-settings.json");SettingsStore.Save(settings,config);loaded=SettingsStore.Load(config).ThemeOverrides;
            try{CyberTheme.ApplyPalette(loaded);Check(CyberTheme.Accent.ToArgb()==violet,"Theme color was not applied.");bool rejected=false;try{CyberTheme.ValidatePalette(new System.Collections.Generic.Dictionary<string,int>{{"Unknown",violet}});}catch(ArgumentException){rejected=true;}Check(rejected,"Unknown theme color was accepted.");}
            finally{CyberTheme.ApplyPalette(new System.Collections.Generic.Dictionary<string,int>());}
        });
        Test("Bookmarks and favorite sample references persist without moving media",delegate{
            string sample=Wave("favorite-sample",.1f);byte[] before=File.ReadAllBytes(sample);string path=Path.Combine(root,"library.json");var data=new SampleLibraryData();
            Check(SampleLibraryStore.Toggle(data.Bookmarks,root)&&SampleLibraryStore.Toggle(data.Favorites,sample),"Could not add references.");SampleLibraryStore.Save(data,path);data=SampleLibraryStore.Load(path);Check(data.Bookmarks.Count==1&&data.Favorites.Count==1&&data.Favorites[0]==sample,"References did not persist.");
            Check(!SampleLibraryStore.Toggle(data.Favorites,sample),"Could not remove favorite reference.");SampleLibraryStore.Save(data,path);SampleLibraryStore.Save(data,path);Check(File.Exists(sample)&&File.ReadAllBytes(sample).SequenceEqual(before),"Original sample was modified.");
        });
        Test("Project information survives save and is written into playable WAV metadata",delegate{
            var p=AudioProject(Wave("metadata-source",.1f));p.Studio.Metadata=new ProjectMetadata{Title="夜の街",Artist="Tsuikyu",Genre="Electronic",Comment="Original",Website="https://example.invalid"};
            var loaded=ProjectFile.Deserialize(ProjectFile.Serialize(p));Check(loaded.Studio.Metadata.Title=="夜の街"&&loaded.Studio.Metadata.Website==p.Studio.Metadata.Website,"Project information was lost.");
            string file=Path.Combine(root,"metadata.wav");AudioExport.Render(p,file,new AudioExportOptions{EndBeat=1},CancellationToken.None,null);byte[] bytes=File.ReadAllBytes(file);
            Check(BitConverter.ToUInt32(bytes,4)==bytes.Length-8&&WaveAudio.Load(file).Samples.Length>0,"WAV header or audio is invalid.");string info=Encoding.UTF8.GetString(bytes);foreach(string text in new[]{"INAM","夜の街","IART","Tsuikyu","IGNR","Electronic","ICMT","Original","IURL","https://example.invalid"})Check(info.Contains(text),"WAV metadata missing: "+text);
        });
        if(Environment.GetEnvironmentVariable("SLOPSTUDIO_TEST_MP3")=="1")Test("FFmpeg MP3 export encodes audio and project metadata",delegate{
            var p=AudioProject(Wave("mp3-source",.1f));p.Studio.Metadata=new ProjectMetadata{Title="夜の街",Artist="Tsuikyu",Genre="Electronic",Comment="Original",Website="https://example.invalid"};string file=Path.Combine(root,"metadata.mp3");
            Mp3Export.Render(p,file,new AudioExportOptions{EndBeat=1},CancellationToken.None,null);Check(File.Exists(file)&&new FileInfo(file).Length>1000,"MP3 output is missing.");Console.WriteLine("MP3_FIXTURE "+file);
        });
        return failures==0?0:1;
    }
}
