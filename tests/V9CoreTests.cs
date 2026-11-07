using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SlopProX;

internal static class V9CoreTests
{
    private static string root;
    private static int failed;
    private static readonly List<string> lines = new List<string>();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Test(string name, Action action)
    {
        try { action(); lines.Add("PASS " + name); }
        catch (Exception ex) { failed++; lines.Add("FAIL " + name + " :: " + ex); }
        File.WriteAllLines(Path.Combine(root, "core.txt"), lines);
    }
    private static string Audio(string name, float value, int frames)
    {
        string file = Path.Combine(root, name + ".wav");
        float[] data = new float[frames * 2];
        for (int i = 0; i < data.Length; i++) data[i] = value;
        using (var writer = new ExportWaveWriter(file, 48000, 2, 32, true)) writer.Write(data, 0, data.Length);
        return file;
    }
    private static Track AudioTrack(string name, string file)
    {
        var t = ProjectFactory.CreateTrack(name, TrackType.Audio);
        var r = ProjectFactory.CreateRegion(name, 0, 4);
        r.MediaPath = file; t.Regions.Add(r); return t;
    }
    private static float Max(float[] data) { return data.Select(Math.Abs).DefaultIfEmpty(0).Max(); }
    private static int Main(string[] args)
    {
        root = Path.Combine(Path.GetFullPath(args[0]), "run-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("SLOPX_PROFILE", Path.Combine(root, "profile"));
        string a = Audio("a", .2f, 96000), b = Audio("b", .1f, 96000);
        Test("Sampler imported PCM, root note, ADSR and project persistence", delegate {
            var p = new ProjectDocument(); var t = ProjectFactory.CreateTrack("Sampler", TrackType.Midi); p.Tracks.Add(t);
            var cfg = new SamplerSettings { TrackId=t.Id, SamplePath=a, RootNote=60, Attack=.1, Decay=.1, Sustain=.4, Release=.1 };
            p.Studio.Samplers.Add(cfg);
            var loaded = ProjectFile.Deserialize(ProjectFile.Serialize(p)).Studio.Sampler(t.Id);
            Check(loaded.SamplePath==a && loaded.RootNote==60 && Math.Abs(loaded.Sustain-.4)<.001,"Sampler state not saved");
            var sampler = new SamplerInstrument(); sampler.Prepare(t.Instrument,loaded);
            var data = new float[48000*2];
            sampler.Render(data,48000,48000,t.Instrument,new[]{new MidiEventPacket(0,0x90,60,127),new MidiEventPacket(24000,0x80,60,0)},2);
            Check(Math.Abs(data[100*2])<Math.Abs(data[5500*2])*.1,"Attack did not rise");
            Check(Math.Abs(data[14000*2])<Math.Abs(data[5500*2])*.55,"Decay/sustain not applied");
            Check(Math.Abs(data[32000*2])<Math.Abs(data[24000*2])*.01,"Release did not fade");
            using(var engine=new ArrangementEngine(new AppSettings())){engine.SetProject(p);engine.SetMidiThruTrack(t.Id);engine.InjectMidi(0x90,60,127);float[] outp=new float[1024];for(int i=0;i<12;i++)engine.Render(outp,512,0);Check(Max(outp)>.005,"Imported sample silent in engine");}
        });
        Test("Sampler recording writer preserves a selected synthetic input channel", delegate {
            var project=new ProjectDocument();var t=ProjectFactory.CreateTrack("Sample recording",TrackType.Midi);project.Tracks.Add(t);
            project.Studio.Inputs.Add(new InputRoute{TrackId=t.Id,Channel=1,Channels=1});
            var input=new float[4096*2];for(int i=0;i<4096;i++){input[i*2]=.1f;input[i*2+1]=.4f;}
            var recorder=new AudioRecordingSession(new[]{t},project.Studio,Path.Combine(root,"recorded-sample"),48000,2);
            recorder.Receive(input,4096,2,0);var takes=recorder.Finish();Check(takes.Count==1&&takes[0].Frames==4096,"Recorded sample missing");
            var wave=WaveAudio.Load(takes[0].Path);short sample=BitConverter.ToInt16(wave.Samples,100*2);Check(Math.Abs(sample/32768.0-.4)<.001,"Incorrect input channel recorded");
            var cfg=new SamplerSettings{TrackId=t.Id,SamplePath=takes[0].Path};var instrument=new SamplerInstrument();instrument.Prepare(t.Instrument,cfg);var output=new float[1024];instrument.Render(output,512,48000,t.Instrument,new[]{new MidiEventPacket(0,0x90,60,100)},1);Check(Max(output)>.005,"Recorded sample did not play");
        });
        Test("MIDI merge preserves timing, transpose, short notes and original regions", delegate {
            var p=new ProjectDocument();var t=ProjectFactory.CreateTrack("Notes",TrackType.Midi);p.Tracks.Add(t);
            var x=ProjectFactory.CreateRegion("A",2,2);x.Transpose=2;x.Notes.Add(new MidiNote{StartBeat=.5,LengthBeats=.02,Pitch=60,Velocity=90});
            var y=ProjectFactory.CreateRegion("B",5,2);y.Notes.Add(new MidiNote{StartBeat=.25,LengthBeats=.5,Pitch=67,Velocity=80});t.Regions.Add(x);t.Regions.Add(y);
            var joined=RegionMerge.Build(p,t,new[]{x,y},null,48000);
            Check(joined.StartBeat==2&&joined.LengthBeats==5&&joined.Notes.Count==2,"MIDI region bounds/count");
            Check(joined.Notes.Any(n=>n.Pitch==62&&Math.Abs(n.LengthBeats-.02)<.0001)&&joined.Notes.Any(n=>Math.Abs(n.StartBeat-3.25)<.0001),"MIDI timing/transpose lost");
            Check(t.Regions.Count==2,"Source regions changed");
        });
        Test("Audio merge renders overlaps and gaps while preserving source WAVs", delegate {
            var p=new ProjectDocument{Tempo=120};var t=AudioTrack("Audio",a);p.Tracks.Add(t);
            var first=t.Regions[0];first.LengthBeats=1;var second=ProjectFactory.CreateRegion("B",2,1);second.MediaPath=b;t.Regions.Add(second);
            string dest=Path.Combine(root,"merged.wav");var merged=RegionMerge.Build(p,t,new[]{first,second},dest,48000);
            var wave=WaveAudio.Load(dest);Check(wave.Info.EncodingTag==3&&Math.Abs(wave.Info.DurationSeconds-1.5)<.002,"Joined WAV format/duration");
            float firstSample=BitConverter.ToSingle(wave.Samples,1000*8),gap=BitConverter.ToSingle(wave.Samples,30000*8),secondSample=BitConverter.ToSingle(wave.Samples,50000*8);
            Check(Math.Abs(firstSample-.2)<.002&&Math.Abs(gap)<.0001&&Math.Abs(secondSample-.1)<.002,"Joined audio contents wrong");
            Check(File.Exists(a)&&File.Exists(b),"Source media removed");
        });
        Test("Native and stock insert order persists and changes actual DSP", delegate {
            var p=new ProjectDocument();var t=AudioTrack("Insert",a);p.Tracks.Add(t);
            string fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TestFixtures","FixtureGain.clap");
            var desc=NativePlugin.Scan(fixture)[0];var plug=desc.CreateSlot();
            using(var instance=new NativePlugin(plug,48000,512)){instance.SetParameter(1000,1);instance.Process(new float[1024],512,new MidiEventPacket[0],0,120,0,false,4,4);plug.State=instance.SaveState();}
            t.Plugins.Add(plug);var limiter=StockEffects.Create(t.Id,"Limiter");limiter.Values["Ceiling"]=-10; p.Studio.Effects.Add(limiter);
            InsertChain.Set(p.Studio,t.Id,new[]{plug.Id,limiter.Id});float first;
            using(var engine=new ArrangementEngine(new AppSettings()))first=Max(engine.RenderOffline(p,8192,0));
            InsertChain.Set(p.Studio,t.Id,new[]{limiter.Id,plug.Id});float second;
            using(var engine=new ArrangementEngine(new AppSettings()))second=Max(engine.RenderOffline(p,8192,0));
            Check(second>first*1.25,"Insert reorder did not alter processing: "+first+" / "+second);
            var loaded=ProjectFile.Deserialize(ProjectFile.Serialize(p));
            Check(InsertChain.Entries(loaded,loaded.Tracks[0]).Select(x=>x.Id).SequenceEqual(new[]{limiter.Id,plug.Id}),"Insert order lost on save");
        });
        Test("Master native and stock inserts process the sum, reorder, bypass and persist", delegate {
            var p=new ProjectDocument();p.Tracks.Add(AudioTrack("One",a));p.Tracks.Add(AudioTrack("Two",b));
            string fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TestFixtures","FixtureGain.clap");
            var plug=NativePlugin.Scan(fixture)[0].CreateSlot();
            using(var instance=new NativePlugin(plug,48000,512)){instance.SetParameter(1000,1);instance.Process(new float[1024],512,new MidiEventPacket[0],0,120,0,false,4,4);plug.State=instance.SaveState();}
            var limiter=StockEffects.Create(Guid.Empty,"Limiter");limiter.Values["Ceiling"]=-10;
            p.Studio.MasterPlugins.Add(plug);p.Studio.Effects.Add(limiter);InsertChain.Set(p.Studio,Guid.Empty,new[]{plug.Id,limiter.Id});
            float first;using(var engine=new ArrangementEngine(new AppSettings()))first=Max(engine.RenderOffline(p,8192,0));
            InsertChain.Set(p.Studio,Guid.Empty,new[]{limiter.Id,plug.Id});
            float second;using(var engine=new ArrangementEngine(new AppSettings()))second=Max(engine.RenderOffline(p,8192,0));
            Check(second>first*1.25,"Master insert order did not change audio: "+first+" / "+second);
            string path=Path.Combine(root,"master.slopdaw");ProjectFile.SaveAtomic(p,path);var loaded=ProjectFile.Load(path);
            Check(InsertChain.Entries(loaded,Guid.Empty).Select(x=>x.Id).SequenceEqual(new[]{limiter.Id,plug.Id}),"Master insert order lost");
            float restored;using(var engine=new ArrangementEngine(new AppSettings()))restored=Max(engine.RenderOffline(loaded,8192,0));
            Check(Math.Abs(restored-second)<.01,"Master chain output changed after reopen");
            loaded.Studio.MasterPlugins[0].Bypassed=true;float bypassed;using(var engine=new ArrangementEngine(new AppSettings()))bypassed=Max(engine.RenderOffline(loaded,8192,0));
            Check(bypassed<restored*.75,"Master native bypass did not change output");
        });
        Test("Summing stack retains child state, controls PCM and collapses in view", delegate {
            var p=new ProjectDocument();var t1=AudioTrack("One",a);var other=ProjectFactory.CreateTrack("Other",TrackType.Midi);var t2=AudioTrack("Two",b);
            t1.Pan=.2;p.Tracks.Add(t1);p.Tracks.Add(other);p.Tracks.Add(t2);Guid parent;
            var stacked=TrackStackOperations.Create(p,new[]{t1.Id,t2.Id},true,out parent);
            var parentTrack=stacked.Tracks.Find(t=>t.Id==parent);parentTrack.GainDb=-6.0205999;
            var children=stacked.Studio.Stacks[0].Children;
            Check(children.SequenceEqual(new[]{t1.Id,t2.Id})&&Math.Abs(stacked.Tracks.Find(t=>t.Id==t1.Id).Pan-.2)<.0001,"Child mixing state changed");
            using(var engine=new ArrangementEngine(new AppSettings())){float[] outp=engine.RenderOffline(stacked,4096,0);Check(Math.Abs(outp[101]-.15)<.01,"Summing parent fader did not control bus: "+outp[101]);}
            stacked.Studio.Stacks[0].Collapsed=true;Check(TrackRouting.Visible(stacked).Count==2,"Collapsed children remained visible");
            var saved=ProjectFile.Deserialize(ProjectFile.Serialize(stacked));Check(saved.Studio.Stacks[0].Children.Count==2&&saved.Studio.Stacks[0].Collapsed,"Stack not persisted");
        });
        Test("Post-fader Aux send processes audio and routing cycles are rejected", delegate {
            var p=new ProjectDocument();var source=AudioTrack("Source",a);var aux=ProjectFactory.CreateTrack("Aux",TrackType.Audio);aux.GainDb=-6.0205999;
            p.Tracks.Add(aux);p.Tracks.Add(source);p.Studio.Buses.Add(aux.Id);p.Studio.Sends.Add(new AuxSend{SourceId=source.Id,TargetId=aux.Id,GainDb=0});
            using(var engine=new ArrangementEngine(new AppSettings())){var audio=engine.RenderOffline(p,4096,0);Check(Math.Abs(audio[100]-.3)<.01,"Aux send not summed: "+audio[100]);}
            p.Studio.Sends.Add(new AuxSend{SourceId=aux.Id,TargetId=source.Id,GainDb=0});bool rejected=false;try{ProjectValidator.Validate(p);}catch(InvalidDataException){rejected=true;}Check(rejected,"Feedback loop accepted");
        });
        Test("Plugin latency aligns summing, Aux sends, bus inserts and direct audio", delegate {
            string impulse=Path.Combine(root,"pdc-impulse.wav");var samples=new float[8192];samples[0]=samples[1]=.1f;
            using(var writer=new ExportWaveWriter(impulse,48000,2,32,true))writer.Write(samples,0,samples.Length);
            var project=new ProjectDocument();var one=AudioTrack("Dry child",impulse);var two=AudioTrack("Delayed child",impulse);var direct=AudioTrack("Direct",impulse);
            string fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"TestFixtures","FixtureSandbox.clap");
            var descriptor=NativePlugin.Scan(fixture)[0];two.Plugins.Add(descriptor.CreateSlot());
            project.Tracks.Add(one);project.Tracks.Add(two);project.Tracks.Add(direct);Guid parentId;
            project=TrackStackOperations.Create(project,new[]{one.Id,two.Id},true,out parentId);
            var parent=project.Tracks.Find(track=>track.Id==parentId);parent.Plugins.Add(descriptor.CreateSlot());
            var aux=AudioTrack("Aux with local audio",impulse);aux.Plugins.Add(descriptor.CreateSlot());project.Tracks.Add(aux);project.Studio.Buses.Add(aux.Id);
            project.Studio.Sends.Add(new AuxSend{SourceId=one.Id,TargetId=aux.Id,GainDb=0});
            project.Studio.Sends.Add(new AuxSend{SourceId=two.Id,TargetId=aux.Id,GainDb=0});
            using(var engine=new ArrangementEngine(new AppSettings())){
                var output=engine.RenderOffline(project,2048,0);
                Check(engine.CompensationSamples==512,"Combined bus latency incorrect: "+engine.CompensationSamples);
                Check(Math.Abs(output[0])<.00001&&Math.Abs(output[256*2])<.00001,"Early misaligned impulse");
                Check(Math.Abs(output[512*2]-.6)<.003,"Bus, send and direct impulses were not aligned: "+output[512*2]);
                Check(Math.Abs(output[768*2])<.00001,"Late duplicate impulse");
            }
        });
        Test("Ducker track sidechain changes real target PCM", delegate {
            var p=new ProjectDocument();var key=AudioTrack("Key",a);var target=AudioTrack("Target",b);p.Tracks.Add(target);p.Tracks.Add(key);
            var fx=StockEffects.Create(target.Id,"EDM Ducker");fx.Values["Mode"]=2;fx.Values["Threshold"]=-40;fx.Values["Depth"]=80;fx.SidechainTrackId=key.Id;p.Studio.Effects.Add(fx);
            float ducked;using(var engine=new ArrangementEngine(new AppSettings())){engine.RenderOffline(p,4096,0);ducked=engine.TrackPeak(target.Id);}
            fx.SidechainTrackId=null;float dry;using(var engine=new ArrangementEngine(new AppSettings())){engine.RenderOffline(p,4096,0);dry=engine.TrackPeak(target.Id);}
            Check(ducked<dry*.35,"Track key did not reduce target: "+ducked+" / "+dry);
        });
        Test("Cycle locator edit undo and shortcut registry remain valid", delegate {
            var p=new ProjectDocument();var history=new ProjectHistory();history.Execute(p,new SetCycleRangeCommand(p,8,20));
            Check(p.CycleEnabled&&p.CycleStartBeat==8&&p.CycleEndBeat==20,"Cycle did not change");
            history.Undo(p);Check(!p.CycleEnabled&&p.CycleStartBeat==0&&p.CycleEndBeat==16,"Cycle undo failed");
            ShortcutRegistry.ValidateUniqueActiveAssignments();Check(ShortcutRegistry.Find(AppCommand.JoinRegions)!=null,"Join shortcut missing");
        });
        Console.WriteLine(String.Join(Environment.NewLine, lines.ToArray()));
        return failed==0?0:1;
    }
}
