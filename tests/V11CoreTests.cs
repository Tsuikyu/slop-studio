using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlopProX;

internal static class V11CoreTests
{
    private static readonly List<string> Lines = new List<string>();
    private static string _root;
    private static int _failures;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Test(string name, Action action)
    {
        try { action(); Lines.Add("PASS " + name); }
        catch (Exception error) { _failures++; Lines.Add("FAIL " + name + " :: " + error); }
        File.WriteAllLines(Path.Combine(_root, "core.txt"), Lines);
    }
    private static string Fixture(string name) { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFixtures", name); }
    private static void Block(NativePlugin plugin, float level)
    {
        var audio = Enumerable.Repeat(level, 512).ToArray();
        plugin.Process(audio, 256, new MidiEventPacket[0], 0, 120, 0, true, 4, 4);
        Check(audio.All(v => !Single.IsNaN(v) && !Single.IsInfinity(v)) && audio.Any(v => Math.Abs(v) > .01), "No valid processed PCM.");
    }
    [STAThread] private static int Main(string[] args)
    {
        _root = Path.Combine(Path.GetFullPath(args[0]), "run-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root); Environment.SetEnvironmentVariable("SLOPX_PROFILE", Path.Combine(_root, "profile"));
        Test("VST3 no-op processing notification still processes and restores actual audio", delegate {
            var descriptor = NativePlugin.Scan(Fixture("FixtureNoStart.vst3"))[0];
            using (var plugin = new NativePlugin(descriptor.CreateSlot(), 48000, 256))
            {
                Block(plugin, .2f); string saved = plugin.SnapshotState(); Check(!String.IsNullOrEmpty(saved), "No state.");
                plugin.RestoreState(saved); Block(plugin, .2f);
                try { plugin.RestoreState(Convert.ToBase64String(new byte[] { 1 })); throw new Exception("Invalid state accepted."); }
                catch (InvalidOperationException) { }
                Check(plugin.Healthy, "Rejected state killed the worker."); Block(plugin, .2f);
            }
        });
        Test("VST3 explicit processing rejection is reported", delegate {
            var descriptor = NativePlugin.Scan(Fixture("FixtureReject.vst3"))[0]; bool rejected = false;
            try { using (var plugin = new NativePlugin(descriptor.CreateSlot(), 48000, 256)) { } }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Explicit VST3 error was ignored.");
        });
        Test("Variable-size plug-in state is captured once per save", delegate {
            var descriptor = NativePlugin.Scan(Fixture("FixtureVariableState.vst3"))[0];
            using (var plugin = new NativePlugin(descriptor.CreateSlot(), 48000, 256))
                for (int i = 0; i < 12; i++) { string state = plugin.SnapshotState(); Check(!String.IsNullOrEmpty(state), "Empty state."); plugin.RestoreState(state); Block(plugin, .2f); }
        });
        Test("MIDI mapping is queried after the controller handler is attached", delegate {
            var descriptor = NativePlugin.Scan(Fixture("FixtureMidiOrder.vst3"))[0];
            using (var plugin = new NativePlugin(descriptor.CreateSlot(), 48000, 256))
            {
                var audio = Enumerable.Repeat(.2f, 512).ToArray();
                plugin.Process(audio, 256, new[] { new MidiEventPacket(0, 0xB0, 1, 127) }, 1, 120, 0, true, 4, 4);
                Check(Math.Abs(audio[0] - .4) < .005, "Controller assignment was queried before the host handler was available.");
            }
        });
        Test("Plug-in editor can open while a DSP block is in flight", delegate {
            var descriptor = NativePlugin.Scan(Fixture("FixtureConcurrentEditor.vst3"))[0];
            using (var plugin = new NativePlugin(descriptor.CreateSlot(), 48000, 256))
            using (var host = new Form())
            {
                host.Show(); var work = Task.Factory.StartNew(() => Block(plugin, .2f)); Thread.Sleep(150);
                Check(!work.IsCompleted, "Concurrent fixture did not enter the blocking DSP block.");
                int width, height; Check(plugin.OpenEditor(host.Handle, out width, out height), "Editor could not open while DSP was running.");
                Check(work.Wait(3000) && work.Exception == null, "DSP and editor did not make progress together.");
                plugin.CloseEditor(); host.Close();
            }
        });
        Test("VST3 bundle enumeration and scan keep the bundle identity", delegate {
            string folder = Path.Combine(_root, "scan"), bundle = Path.Combine(folder, "Example.vst3");
            string binary = Path.Combine(bundle, "Contents", "x86_64-win", "Example.vst3");
            Directory.CreateDirectory(Path.GetDirectoryName(binary)); File.Copy(Fixture("FixtureGain.vst3"), binary);
            var paths = PluginCatalog.Enumerate(new[] { folder });
            Check(paths.Count == 1 && String.Equals(paths[0], bundle, StringComparison.OrdinalIgnoreCase), "Bundle scan duplicated or lost the bundle.");
            Check(String.Equals(NativePlugin.Scan(bundle)[0].Path, bundle, StringComparison.OrdinalIgnoreCase), "Scan did not preserve bundle path.");
        });
        Test("Compressor live edit changes PCM without rebuilding its processor", delegate {
            string wave = Path.Combine(_root, "compressor.wav"); var source = Enumerable.Repeat(.7f, 96000 * 2).ToArray();
            using (var writer = new ExportWaveWriter(wave, 48000, 2, 32, true)) writer.Write(source, 0, source.Length);
            var project = new ProjectDocument(); var track = ProjectFactory.CreateTrack("Audio", TrackType.Audio);
            var region = ProjectFactory.CreateRegion("Signal", 0, 4); region.MediaPath = wave; track.Regions.Add(region); project.Tracks.Add(track);
            var fx = StockEffects.Create(track.Id, "Compressor"); fx.Values["Threshold"] = 0; fx.Values["Ratio"] = 8; fx.Values["Attack"] = .1;
            project.Studio.Effects.Add(fx);
            using (var engine = new ArrangementEngine(new AppSettings { SampleRate = 48000, BufferFrames = 256 }))
            {
                engine.PrepareOffline(project, 0); var processor = engine.GetStockProcessor(fx.Id); var audio = new float[512];
                for (int i = 0; i < 20; i++) engine.Render(audio, 256, 0);
                float before = audio.Select(Math.Abs).Average(); fx.Values["Threshold"] = -40;
                Check(engine.UpdateStockEffect(fx), "Live update rejected compatible parameters.");
                for (int i = 0; i < 20; i++) engine.Render(audio, 256, 0);
                float after = audio.Select(Math.Abs).Average();
                Check(Object.ReferenceEquals(processor, engine.GetStockProcessor(fx.Id)), "Processor graph was rebuilt.");
                Check(after < before * .2, "Live threshold did not affect PCM: " + before + " -> " + after);
            }
        });
        Test("Preset identity, atomic overwrite, backup and VST3 state round-trip", delegate {
            string file = Path.Combine(_root, "gain.sloppreset");
            var descriptor = NativePlugin.Scan(Fixture("FixtureGain.vst3"))[0];
            using (var plugin = new NativePlugin(descriptor.CreateSlot(), 48000, 256))
            {
                var parameter = plugin.Parameters()[0]; plugin.SetParameter(parameter.Id, .8); Block(plugin, .2f);
                var first = SlopPresetFile.Create(descriptor.Format, descriptor.ClassId, plugin.SnapshotState());
                SlopPresetFile.Save(file, first); plugin.SetParameter(parameter.Id, .1); Block(plugin, .2f);
                SlopPresetFile.Save(file, SlopPresetFile.Create(descriptor.Format, descriptor.ClassId, plugin.SnapshotState()));
                Check(Directory.GetFiles(_root, "gain.sloppreset.*.bak").Length == 1, "Previous preset was not backed up.");
                string backup = Directory.GetFiles(_root, "gain.sloppreset.*.bak")[0];
                var saved = SlopPresetFile.Load(backup); SlopPresetFile.RequireCompatible(saved, descriptor.Format, descriptor.ClassId);
                plugin.RestoreState(saved.State); Check(Math.Abs(plugin.GetParameter(parameter.Id) - .8) < .01, "Preset did not restore the plug-in parameter.");
                bool mismatch = false; try { SlopPresetFile.RequireCompatible(saved, "CLAP", "unrelated"); } catch (InvalidDataException) { mismatch = true; }
                Check(mismatch, "Different plug-in preset was accepted.");
            }
        });
        Console.WriteLine(String.Join(Environment.NewLine, Lines.ToArray())); return _failures == 0 ? 0 : 1;
    }
}
