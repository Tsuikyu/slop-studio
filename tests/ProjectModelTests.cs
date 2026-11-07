using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using SlopStudio;

internal static class ProjectModelTests
{
    private static int _passed;
    private static int _failed;

    private static int Main(string[] args)
    {
        string localeDirectory = args.Length > 0 ? args[0] : Path.Combine(Environment.CurrentDirectory, "locales");
        string scratchDirectory = args.Length > 1 ? args[1] : Path.Combine(Environment.CurrentDirectory, "test-work");
        Directory.CreateDirectory(scratchDirectory);
        string testDirectory = Path.Combine(scratchDirectory, "SlopStudioTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDirectory);

        Run("default model and serialization round-trip", delegate { TestRoundTrip(); });
        Run("PCM WAV metadata and invalid-format handling", delegate { TestPcmWave(testDirectory); });
        Run("preview copy crops, applies gain and pans mono PCM", delegate { TestPlaybackTransform(testDirectory); });
        Run("reference application shortcut translation registry and conflict check", delegate { TestShortcuts(); });
        Run("unknown fields and malformed input", delegate { TestMalformedInput(); });
        Run("project validation bounds and identifiers", delegate { TestValidation(); });
        Run("atomic save, replace, and failed-save preservation", delegate { TestAtomicSave(testDirectory); });
        Run("MIDI note persistence, validation, and undo/redo", delegate { TestMidi(); });
        Run("semantic undo/redo and saved-state tracking", delegate { TestHistory(); });
        Run("settings validation and persistence", delegate { TestSettings(testDirectory); });
        Run("multitrack PCM and internal instrument rendering",delegate{TestArrangement(testDirectory);});
        Run("recorded PCM finalization and MIDI timestamp capture",delegate{TestCapture(testDirectory);});
        Run("v5 plugin, recording, routing and non-destructive edit persistence",delegate{TestVersionFive();});
        Run("language-pack schema and placeholders", delegate { TestLocales(localeDirectory); });

        Console.WriteLine("RESULT: " + _passed + " passed, " + _failed + " failed");
        return _failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action action)
    {
        try
        {
            action();
            _passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception exception)
        {
            _failed++;
            Console.WriteLine("FAIL " + name + " :: " + exception.ToString());
        }
    }

    private static void TestRoundTrip()
    {
        ProjectDocument project = ProjectFactory.Create("Round Trip", "Audio 1");
        project.Tempo = 128.5;
        Region intro = ProjectFactory.CreateRegion("Intro", 0.0, 8.0);
        intro.MediaPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "linked-audio.wav"));
        project.Tracks[0].Regions.Add(intro);
        Track midi = ProjectFactory.CreateTrack("MIDI 1", TrackType.Midi);
        midi.Regions.Add(ProjectFactory.CreateRegion("Theme", 8.0, 4.0));
        project.Tracks.Add(midi);
        ProjectValidator.Validate(project);
        string json = ProjectFile.Serialize(project);
        Assert(json.Contains("\"schema\":\"slopdaw-project\""), "Serialized schema name is missing.");
        Assert(json.Contains("\"version\":1"), "New projects must use project format version 1.");
        Assert(json.Contains("\"mediaPath\""), "Linked audio media path was not serialized.");
        Assert(ProjectFile.Equivalent(project, ProjectFile.Deserialize(json)), "Round-trip state differs.");
    }

    private static void TestPcmWave(string directory)
    {
        string path = Path.Combine(directory, "fixture.wav");
        const int sampleRate = 22050;
        const int channels = 1;
        const int bits = 16;
        const int dataBytes = 4410;
        using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(new byte[] { 82, 73, 70, 70 });
            writer.Write(36 + dataBytes);
            writer.Write(new byte[] { 87, 65, 86, 69 });
            writer.Write(new byte[] { 102, 109, 116, 32 });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bits / 8);
            writer.Write((short)(channels * bits / 8));
            writer.Write((short)bits);
            writer.Write(new byte[] { 100, 97, 116, 97 });
            writer.Write(dataBytes);
            writer.Write(new byte[dataBytes]);
        }
        PcmWaveInfo info = WaveAudio.Inspect(path);
        Assert(info.Channels == 1 && info.SampleRate == sampleRate && info.BitsPerSample == bits,
            "PCM WAV format metadata differs from the fixture.");
        Assert(Math.Abs(info.DurationSeconds - 0.1) < 0.0001, "PCM WAV duration is incorrect.");

        byte[] invalid = File.ReadAllBytes(path);
        invalid[20] = 3;
        invalid[21] = 0;
        string invalidPath = Path.Combine(directory, "unsupported.wav");
        File.WriteAllBytes(invalidPath, invalid);
        AssertThrows<InvalidDataException>(delegate { WaveAudio.Inspect(invalidPath); });
    }

    private static void TestPlaybackTransform(string directory)
    {
        string source=Path.Combine(directory,"transform-source.wav");const int rate=8000;short[] values=new short[]{4096,8192,12288,16384};
        using(BinaryWriter w=new BinaryWriter(File.Create(source))){int dataBytes=values.Length*2;w.Write(new byte[]{82,73,70,70});w.Write(36+dataBytes);w.Write(new byte[]{87,65,86,69});w.Write(new byte[]{102,109,116,32});w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(new byte[]{100,97,116,97});w.Write(dataBytes);foreach(short value in values)w.Write(value);}
        string copy=WaveAudio.CreatePlaybackCopy(source,0.00025,0.00025,6.020599913,-1.0);
        try{PcmWaveData data=WaveAudio.Load(copy);Assert(data.Info.Channels==2&&data.Info.BitsPerSample==16,"Preview copy was not converted to stereo PCM16.");Assert(data.Samples.Length==8,"Preview copy did not crop to the requested two frames.");Assert(BitConverter.ToInt16(data.Samples,0)==24576,"Preview copy gain/left pan is incorrect.");Assert(BitConverter.ToInt16(data.Samples,2)==0&&BitConverter.ToInt16(data.Samples,6)==0,"Hard-left pan did not silence the right channel.");Assert(BitConverter.ToInt16(data.Samples,4)==32767,"Preview copy did not clamp a clipped sample.");}finally{if(File.Exists(copy))File.Delete(copy);}
    }

    private static void TestShortcuts()
    {
        ShortcutRegistry.ValidateUniqueActiveAssignments();ShortcutBinding binding;
        Assert(ShortcutRegistry.TryFind(Keys.Space,false,out binding)&&binding.Command==AppCommand.PlayOrStop,"Space does not map to transport Play/Stop.");
        Assert(ShortcutRegistry.TryFind(Keys.Control|Keys.Shift|Keys.N,false,out binding)&&binding.Command==AppCommand.NewProject,"reference application New Empty Project did not translate to Ctrl+Shift+N.");
        Assert(ShortcutRegistry.TryFind(Keys.Control|Keys.Shift|Keys.Z,false,out binding)&&binding.Command==AppCommand.Redo,"reference application Shift-Command-Z did not translate to Ctrl+Shift+Z.");
        Assert(ShortcutRegistry.TryFind(Keys.Control|Keys.Alt|Keys.S,true,out binding)&&binding.Command==AppCommand.AddMidiTrack,"New Software Instrument Track must win the Ctrl+Alt+S collision.");
        Assert(ShortcutRegistry.TryFind(Keys.M,true,out binding)&&binding.Command==AppCommand.ToggleMute,"reference application M did not toggle track mute.");
        Assert(ShortcutRegistry.TryFind(Keys.S,true,out binding)&&binding.Command==AppCommand.ToggleSolo,"reference application S did not toggle track solo.");
        Assert(ShortcutRegistry.TryFind(Keys.Control|Keys.Alt|Keys.A,false,out binding)&&binding.Command==AppCommand.AddAudioTrack,"Option-Command-A translation failed.");
        Assert(ShortcutRegistry.TryFind(Keys.Control|Keys.Alt|Keys.X,false,out binding)&&binding.Command==AppCommand.AddExternalMidiTrack,"Option-Command-X translation failed.");
        Assert(ShortcutRegistry.TryFind(Keys.Control|Keys.Shift|Keys.I,false,out binding)&&binding.Command==AppCommand.ImportAudio,"Shift-Command-I translation failed.");
        Assert(ShortcutRegistry.TryFind(Keys.Alt|Keys.Shift|Keys.I,false,out binding)&&binding.Command==AppCommand.ToggleInspector,"Option-Shift-I translation failed.");
        foreach(ShortcutBinding item in ShortcutRegistry.Bindings){if(item.Implemented){Assert(ShortcutRegistry.TryFind(item.WindowsKeys,item.ArrangementOnly,out binding)&&binding.Command==item.Command,"Active registry entry does not resolve: "+item.Command);if(item.ArrangementOnly)Assert(!ShortcutRegistry.TryFind(item.WindowsKeys,false,out binding),"Arrangement-only entry leaked to global context: "+item.Command);}else if(ShortcutRegistry.TryFind(item.WindowsKeys,item.ArrangementOnly,out binding))Assert(binding.Command!=item.Command,"Inactive entry captured keys: "+item.Command);}
        Assert(ShortcutRegistry.Display(AppCommand.Undo)=="Ctrl+Z","Undo menu label does not use the registry.");
    }
    private static void TestMalformedInput()
    {
        ProjectDocument project = ProjectFactory.Create("Unknown Fields", "Audio 1");
        string json = ProjectFile.Serialize(project);
        string withUnknown = json.Substring(0, json.Length - 1) + ",\"futureField\":true}";
        Assert(ProjectFile.Equivalent(project, ProjectFile.Deserialize(withUnknown)), "Unknown top-level field was not safely ignored.");
        Assert(ProjectFile.Equivalent(project, ProjectFile.Deserialize(json)), "Version 1 project failed.");
        string wrongVersion = json.Replace("\"version\":1", "\"version\":99");
        AssertThrows<InvalidDataException>(delegate { ProjectFile.Deserialize(json.Replace("slopdaw-project", "unknown-project")); });
        AssertThrows<InvalidDataException>(delegate { ProjectFile.Deserialize(wrongVersion); });
        AssertThrows<InvalidDataException>(delegate { ProjectFile.Deserialize("{"); });
        AssertThrows<InvalidDataException>(delegate { ProjectFile.Deserialize("null"); });
    }

    private static void TestValidation()
    {
        ProjectDocument invalidTempo = ProjectFactory.Create("Bad Tempo", "Audio 1");
        invalidTempo.Tempo = 0.0;
        AssertThrows<InvalidDataException>(delegate { ProjectFile.Serialize(invalidTempo); });

        ProjectDocument duplicateId = ProjectFactory.Create("Duplicate", "Audio 1");
        Region region = ProjectFactory.CreateRegion("Clip", 0.0, 4.0);
        region.Id = duplicateId.Tracks[0].Id;
        duplicateId.Tracks[0].Regions.Add(region);
        AssertThrows<InvalidDataException>(delegate { ProjectValidator.Validate(duplicateId); });

        ProjectDocument invalidSignature = ProjectFactory.Create("Bad Meter", "Audio 1");
        invalidSignature.TimeSignatureDenominator = 3;
        AssertThrows<InvalidDataException>(delegate { ProjectValidator.Validate(invalidSignature); });

        ProjectDocument invalidName = ProjectFactory.Create("Bad Name", "Audio 1");
        invalidName.Tracks[0].Name = "Bad\nName";
        AssertThrows<InvalidDataException>(delegate { ProjectValidator.Validate(invalidName); });

        ProjectDocument tooManyTracks = new ProjectDocument();
        tooManyTracks.Name = "Limit";
        for (int i = 0; i <= ProjectValidator.MaxTracks; i++)
            tooManyTracks.Tracks.Add(ProjectFactory.CreateTrack("Track " + i, TrackType.Audio));
        AssertThrows<InvalidDataException>(delegate { ProjectValidator.Validate(tooManyTracks); });
    }

    private static void TestAtomicSave(string directory)
    {
        string path = Path.Combine(directory, "atomic.slopdaw");
        ProjectDocument first = ProjectFactory.Create("Atomic", "Audio 1");
        first.Tracks[0].Regions.Add(ProjectFactory.CreateRegion("First", 0.0, 4.0));
        ProjectFile.SaveAtomic(first, path);
        Assert(ProjectFile.Equivalent(first, ProjectFile.Load(path)), "Initial save/load differs.");

        ProjectDocument second = ProjectFactory.Clone(first);
        second.Tempo = 141.0;
        second.Tracks[0].Regions[0].Name = "Second";
        ProjectFile.SaveAtomic(second, path);
        Assert(ProjectFile.Equivalent(second, ProjectFile.Load(path)), "Replacement save/load differs.");
        string[] backups = Directory.GetFiles(directory, "atomic.slopdaw.*.bak");
        Assert(backups.Any(file => ProjectFile.Equivalent(first, ProjectFile.Load(file))), "Prior project backup missing or changed.");
        AssertThrows<InvalidDataException>(delegate { ProjectFile.SaveAtomic(second, Path.Combine(directory, "wrong-extension.txt")); });

        ProjectDocument invalid = ProjectFactory.Clone(second);
        invalid.Tempo = 0.0;
        AssertThrows<InvalidDataException>(delegate { ProjectFile.SaveAtomic(invalid, path); });
        Assert(ProjectFile.Equivalent(second, ProjectFile.Load(path)), "Failed save modified the previous valid project.");

        AssertThrows<DirectoryNotFoundException>(delegate
        {
            ProjectFile.SaveAtomic(second, Path.Combine(directory, "missing", "project.slopdaw"));
        });
        Assert(ProjectFile.Equivalent(second, ProjectFile.Load(path)), "Failed destination write modified the previous valid project.");
        string[] leftovers = Directory.GetFiles(directory, "atomic.slopdaw.*.tmp");
        Assert(leftovers.Length == 0, "Temporary save file was left behind.");
    }

    private static void TestMidi()
    {
        ProjectDocument project=ProjectFactory.Create("MIDI","Audio 1"); Track track=ProjectFactory.CreateTrack("Piano",TrackType.Midi); Region region=ProjectFactory.CreateRegion("Phrase",0,4); MidiNote first=new MidiNote(); first.StartBeat=0.25; first.LengthBeats=0.5; first.Pitch=64; first.Velocity=96; region.Notes.Add(first); track.Regions.Add(region); project.Tracks.Add(track);
        string json=ProjectFile.Serialize(project); Assert(json.Contains("\"notes\"")&&json.Contains("\"pitch\":64"),"MIDI note events were not serialized."); ProjectDocument reopened=ProjectFile.Deserialize(json); Assert(ProjectFile.Equivalent(project,reopened),"MIDI note round-trip differs.");
        ProjectHistory history=new ProjectHistory(); history.Reset(true); List<MidiNote> changed=ProjectFactory.CloneNotes(region.Notes); MidiNote next=new MidiNote(); next.StartBeat=1.0; next.LengthBeats=0.5; next.Pitch=67; changed.Add(next); history.Execute(project,new SetMidiNotesCommand(project,track.Id,region.Id,changed)); Assert(region.Notes.Count==2,"MIDI note edit was not applied."); history.Undo(project); Assert(region.Notes.Count==1,"MIDI note undo failed."); history.Redo(project); Assert(region.Notes.Count==2,"MIDI note redo failed.");
        MidiNote invalid=new MidiNote(); invalid.StartBeat=3.9; invalid.LengthBeats=1; region.Notes.Add(invalid); AssertThrows<InvalidDataException>(delegate { ProjectValidator.Validate(project); });
    }

    private static void TestHistory()
    {
        ProjectDocument project = ProjectFactory.Create("History", "Audio 1");
        ProjectHistory history = new ProjectHistory();
        Track added = ProjectFactory.CreateTrack("MIDI 1", TrackType.Midi);
        history.Execute(project, new AddTrackCommand(added, project.Tracks.Count));
        Assert(history.IsDirty && project.Tracks.Count == 2, "Add track did not mark project dirty.");
        history.Undo(project);
        Assert(!history.IsDirty && project.Tracks.Count == 1, "Undo did not return to the saved state.");
        history.Redo(project);
        Assert(history.IsDirty && project.Tracks.Count == 2, "Redo did not restore the edit.");

        history.Undo(project);
        history.Execute(project, new RenameTrackCommand(project.Tracks[0].Id, "Audio 1", "Drums"));
        Assert(history.IsDirty && !history.CanRedo, "A branched edit retained invalid redo history.");
        history.Undo(project);
        Assert(!history.IsDirty && project.Tracks[0].Name == "Audio 1", "Undo did not restore renamed track.");
        history.Redo(project);
        Assert(project.Tracks[0].Name == "Drums", "Redo did not restore renamed track.");

        history.MarkSaved();
        Track audio = project.Tracks[0];
        Region region = ProjectFactory.CreateRegion("Clip", 0.0, 4.0);
        history.Execute(project, new AddRegionCommand(audio.Id, region, audio.Regions.Count));
        history.Execute(project, new MoveRegionCommand(audio.Id, region.Id, 0.0, 2.0));
        Assert(region.StartBeat == 2.0, "Move region command did not apply.");
        history.Undo(project);
        Assert(region.StartBeat == 0.0, "Undo did not restore region position.");
        history.Redo(project);
        history.Execute(project, new RenameRegionCommand(audio.Id, region.Id, "Clip", "Verse"));
        Assert(region.Name == "Verse", "Rename region command did not apply.");
        history.Undo(project);
        Assert(region.Name == "Clip", "Undo did not restore region name.");
        history.Redo(project);
        history.Execute(project, new DeleteRegionCommand(project, audio.Id, region.Id));
        Assert(audio.Regions.Count == 0, "Delete region command did not apply.");
        history.Undo(project);
        Assert(audio.Regions.Count == 1 && audio.Regions[0].Id == region.Id, "Undo did not restore deleted region.");

        history.Execute(project, new SetTempoCommand(project.Tempo, 132.0));
        Assert(project.Tempo == 132.0, "Set tempo command did not apply.");
        history.Undo(project);
        Assert(project.Tempo == 120.0, "Undo did not restore tempo.");
        history.Redo(project);
        Assert(project.Tempo == 132.0, "Redo did not restore tempo.");

        history.Execute(project, new AddTrackCommand(added, project.Tracks.Count));
        history.Execute(project, new DeleteTrackCommand(project, added.Id));
        Assert(project.Tracks.Count == 1, "Delete track command did not apply.");
        history.Undo(project);
        Assert(project.Tracks.Count == 2 && project.Tracks[1].Id == added.Id, "Undo did not restore deleted track.");
        ProjectValidator.Validate(project);
    }

    private static void TestSettings(string directory)
    {
        string path = Path.Combine(directory, "settings.json");
        AppSettings settings = new AppSettings();
        settings.Language = "ja-JP";
        settings.TimelineZoom = 80;settings.AudioInput=3;settings.MidiInput=0;settings.BufferFrames=256;settings.UiScale=115;settings.DefaultTempo=128.5;settings.DefaultNumerator=3;
        SettingsStore.Save(settings, path);
        AppSettings loaded = SettingsStore.Load(path);
        Assert(loaded.Language == "ja-JP" && loaded.TimelineZoom == 80, "Settings round-trip differs.");Assert(loaded.AudioInput==3&&loaded.MidiInput==0&&loaded.BufferFrames==256&&loaded.UiScale==115&&loaded.DefaultTempo==128.5&&loaded.DefaultNumerator==3,"New persistent settings were lost.");

        AppSettings invalid = new AppSettings();
        invalid.Language = "xx-XX";
        AssertThrows<InvalidDataException>(delegate { SettingsStore.Save(invalid, path); });
        AppSettings stillValid = SettingsStore.Load(path);
        Assert(stillValid.Language == "ja-JP" && stillValid.TimelineZoom == 80, "Rejected settings changed saved settings.");
    }


    private static float Peak(float[] data){float peak=0;foreach(float value in data){Assert(!Single.IsNaN(value)&&!Single.IsInfinity(value),"Audio contains a non-finite sample.");peak=Math.Max(peak,Math.Abs(value));}return peak;}
    private static void TestArrangement(string directory)
    {
        AppSettings settings=new AppSettings{SampleRate=48000,BufferFrames=256};ProjectDocument p=new ProjectDocument{Name="Render"};Track t=ProjectFactory.CreateTrack("Piano",TrackType.Midi);Region r=ProjectFactory.CreateRegion("Notes",0,4);r.Notes.Add(new MidiNote{StartBeat=0,LengthBeats=1,Pitch=60,Velocity=100});t.Regions.Add(r);p.Tracks.Add(t);
        using(ArrangementEngine engine=new ArrangementEngine(settings)){float[] samples=engine.RenderOffline(p,2048,0);Assert(Peak(samples)>0.02,"MIDI did not synthesize real PCM.");engine.ResetProject();r.Muted=true;Assert(Peak(engine.RenderOffline(p,1024,0))==0,"Muted region leaked audio.");r.Muted=false;engine.ResetProject();r.Transpose=12;float[] octave=engine.RenderOffline(p,2048,0);double difference=0;for(int i=0;i<octave.Length;i++)difference+=Math.Abs(octave[i]-samples[i]);Assert(difference>1,"Region transpose did not change the synthesized pitch.");r.Transpose=0;engine.ResetProject();r.VelocityOffset=-70;Assert(Peak(engine.RenderOffline(p,2048,0))<Peak(samples)*0.5,"Region velocity offset did not affect MIDI playback.");r.VelocityOffset=0;engine.ResetProject();t.Muted=true;Assert(Peak(engine.RenderOffline(p,1024,0))==0,"Muted track leaked audio.");engine.ResetProject();t.Muted=false;t.Pan=-1;samples=engine.RenderOffline(p,1024,0);for(int i=1;i<samples.Length;i+=2)Assert(samples[i]==0,"Hard-left pan leaked right channel.");engine.ResetProject();t.Pan=0;t.Solo=false;Track second=ProjectFactory.CreateTrack("Solo silent",TrackType.Midi);second.Solo=true;p.Tracks.Add(second);Assert(Peak(engine.RenderOffline(p,1024,0))==0,"Solo did not exclude other tracks.");engine.ResetProject();p.Tracks.Remove(second);p.CycleEnabled=true;p.CycleEndBeat=0.5;samples=engine.RenderOffline(p,24000,0);Assert(Peak(samples)>0.02&&engine.PositionBeat<0.51,"Cycle did not wrap the render position.");}
        t.Plugins.Add(new PluginSlot{Path=Path.Combine(directory,"absent.vst3"),ClassId="missing",Name="Missing instrument",IsInstrument=true});using(ArrangementEngine missing=new ArrangementEngine(settings)){Assert(Peak(missing.RenderOffline(p,1024,0))==0&&missing.Warnings.Count>0,"A missing instrument was silently substituted with a built-in synth.");t.Plugins[0].Bypassed=true;Assert(Peak(missing.RenderOffline(p,1024,0))==0,"Bypassing a plugin instrument incorrectly selected a different instrument.");}
        string file=Path.Combine(directory,"arrangement.wav");using(PcmWaveWriter writer=new PcmWaveWriter(file,48000,1)){byte[] pcm=new byte[4000];for(int i=0;i<2000;i++){short value=(short)(i<1000?8192:16384);pcm[i*2]=(byte)value;pcm[i*2+1]=(byte)(value>>8);}writer.Write(pcm,pcm.Length);}p=new ProjectDocument{Name="PCM"};t=ProjectFactory.CreateTrack("Audio",TrackType.Audio);r=ProjectFactory.CreateRegion("Offset",0,1);r.MediaPath=file;r.AudioOffsetSeconds=1000.0/48000;t.Regions.Add(r);p.Tracks.Add(t);using(ArrangementEngine engine=new ArrangementEngine(settings)){float[] samples=engine.RenderOffline(p,200,0);Assert(Math.Abs(samples[0]-0.5)<0.001,"Source offset did not select the real PCM content.");engine.ResetProject();p.Tracks.Add(ProjectFactory.Clone(p).Tracks[0]);p.Tracks[1].Id=Guid.NewGuid();p.Tracks[1].Regions[0].Id=Guid.NewGuid();samples=engine.RenderOffline(p,200,0);Assert(Math.Abs(samples[0]-1)<0.001,"Two timeline audio lanes were not summed.");}
    }
    private static void TestCapture(string directory)
    {
        string path=Path.Combine(directory,"recorded.wav");PcmWaveWriter writer=new PcmWaveWriter(path,48000,1);byte[] samples=new byte[960];for(int i=0;i<samples.Length;i++)samples[i]=(byte)(i%211);writer.Write(samples,samples.Length);writer.Dispose();writer.Dispose();PcmWaveData wave=WaveAudio.Load(path);Assert(wave.Info.DataBytes==samples.Length&&wave.Samples[201]==samples[201],"Recorded PCM bytes changed or WAV header was not finalized.");MidiRecordingSession recording=new MidiRecordingSession(120);long now=System.Diagnostics.Stopwatch.GetTimestamp();recording.Receive(new MidiInputMessage{Status=0x91,Data1=64,Data2=99,Timestamp=now});recording.Receive(new MidiInputMessage{Status=0x91,Data1=64,Data2=0,Timestamp=now+System.Diagnostics.Stopwatch.Frequency/2});List<MidiNote> notes=recording.Stop();Assert(notes.Count==1&&notes[0].Pitch==64&&notes[0].Velocity==99&&Math.Abs(notes[0].LengthBeats-1)<0.001,"MIDI note-on velocity zero or timestamp capture failed.");
    }
    private static void TestVersionFive()
    {
        ProjectDocument p=ProjectFactory.Create("V5","Audio");p.MasterGainDb=-3.5;p.CycleEnabled=true;p.CycleStartBeat=4;p.CycleEndBeat=20;Track t=ProjectFactory.CreateTrack("External",TrackType.Midi);t.Armed=true;t.ExternalMidi=true;t.Instrument="Warm Pad";t.ColorIndex=4;t.Plugins.Add(new PluginSlot{Path="C:/Fixture.vst3",ClassId="test",Name="Fixture",Format="VST3",State="AQID",Bypassed=true,IsInstrument=true});Region r=ProjectFactory.CreateRegion("Recorded",4.21443500000000001,4);r.AudioOffsetSeconds=0.125;r.Muted=true;r.Transpose=12;r.VelocityOffset=-9;r.Notes.Add(new MidiNote{StartBeat=0.21443500000000001,LengthBeats=1.0000000000000002,Pitch=64,Velocity=99});t.Regions.Add(r);p.Tracks.Add(t);Assert(ProjectFile.Equivalent(p,ProjectFile.Deserialize(ProjectFile.Serialize(p))),"V5 state failed a round-trip.");ProjectDocument changed=ProjectFactory.Clone(p);changed.Tracks[1].Regions[0].Notes[0].LengthBeats+=0.000001;Assert(!ProjectFile.Equivalent(p,changed),"Persistence comparison hid a meaningful edit.");ProjectHistory history=new ProjectHistory();history.Reset(true);history.Execute(p,new SetInstrumentCommand(t,"Soft Piano"));Assert(!t.ExternalMidi&&t.Instrument=="Soft Piano","Internal patch did not clear external routing.");history.Undo(p);Assert(t.ExternalMidi&&t.Instrument=="Warm Pad","Undo lost external MIDI routing.");history.Execute(p,new TrimRegionCommand(t,r,5,2,0.5));Assert(r.StartBeat==5&&r.LengthBeats==2,"Trim command failed.");history.Undo(p);Assert(r.StartBeat>4.2&&r.Notes.Count==1,"Trim undo lost original notes.");
    }

    private static void TestLocales(string directory)
    {
        Dictionary<string, string> english = new Dictionary<string, string>();
        foreach (string language in LocaleCatalog.SupportedLanguages)
        {
            string path = Path.Combine(directory, language + ".json");
            LocaleCatalog pack = LocaleCatalog.Load(path, language);
            Assert(pack.Language == language, "Pack language identifier differs for " + language + ".");
            Assert(pack.T("app.title") == "Slop Studio", "Product name changed in " + language + ".");
            if (language == "en-US")
            {
                string json = File.ReadAllText(path);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                IDictionary<string, object> root = serializer.DeserializeObject(json) as IDictionary<string, object>;
                IDictionary<string, object> strings = root["strings"] as IDictionary<string, object>;
                foreach (KeyValuePair<string, object> entry in strings)
                    english[entry.Key] = entry.Value as string;
            }
            foreach (KeyValuePair<string, string> source in english)
            {
                string translated = pack.T(source.Key);
                string[] expected = Placeholders(source.Value);
                string[] actual = Placeholders(translated);
                Assert(expected.Length == actual.Length, "Placeholder count differs for " + language + " key " + source.Key + ".");
                Array.Sort(expected);
                Array.Sort(actual);
                for (int i = 0; i < expected.Length; i++)
                    Assert(expected[i] == actual[i], "Placeholder differs for " + language + " key " + source.Key + ".");
            }
        }
    }

    private static string[] Placeholders(string text)
    {
        MatchCollection matches = Regex.Matches(text, "\\{\\d+(?::[^}]+)?\\}");
        string[] result = new string[matches.Count];
        for (int i = 0; i < matches.Count; i++) result[i] = matches[i].Value;
        return result;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertThrows<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Expected " + typeof(T).Name + " but received " + exception.GetType().Name + ".", exception);
        }
        throw new InvalidOperationException("Expected " + typeof(T).Name + " but no exception was raised.");
    }
}
