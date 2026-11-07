using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using SlopStudio;

internal static class V11InstalledPluginTests
{
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr handle, int command);
    private delegate bool WindowVisitor(IntPtr handle, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowVisitor visit, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, WindowVisitor visit, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr handle, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle, int message, IntPtr w, IntPtr l);
    private static void Pump(int milliseconds) { var time = Stopwatch.StartNew(); while (time.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); } }
    private static float Peak(float[] samples)
    {
        if (samples.Any(x => Single.IsNaN(x) || Single.IsInfinity(x))) throw new Exception("Non-finite PCM.");
        return samples.Select(Math.Abs).DefaultIfEmpty(0).Max();
    }
    [STAThread] private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[1]); Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("SLOPSTUDIO_PROFILE", Path.Combine(root, "profile"));
        string report = Path.Combine(root, "result.txt");
        try
        {
            var descriptor = NativePlugin.Scan(args[0])[0];
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using (var plugin = new NativePlugin(descriptor.CreateSlot(), 48000, 256))
            using (var editor = new PluginEditorForm(plugin))
            {
                editor.Show(); Pump(300);
                var body = (Panel)typeof(PluginEditorForm).GetField("_body", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                var toolbar = (PresetToolbar)typeof(PluginEditorForm).GetField("_presets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                if (GetWindow(body.Handle, 5) == IntPtr.Zero) throw new Exception("Native editor did not attach to host window.");
                string preset = Path.Combine(root, "vendor.sloppreset");
                toolbar.SaveTo(preset); toolbar.LoadFrom(preset); Pump(200);
                if (!plugin.Healthy || GetWindow(body.Handle, 5) == IntPtr.Zero) throw new Exception("Plug-in editor failed after preset restoration.");
                editor.Close(); Pump(100);
            }
            string wave = Path.Combine(root, "synthetic.wav"); var signal = new float[96000 * 2];
            for (int i = 0; i < signal.Length / 2; i++) signal[i * 2] = signal[i * 2 + 1] = (float)(.12 * Math.Sin(2 * Math.PI * 440 * i / 48000));
            using (var writer = new ExportWaveWriter(wave, 48000, 2, 32, true)) writer.Write(signal, 0, signal.Length);
            var project = new ProjectDocument(); var track = ProjectFactory.CreateTrack("Test signal", descriptor.Instrument ? TrackType.Midi : TrackType.Audio);
            var region = ProjectFactory.CreateRegion("Signal", 0, 4);
            if (descriptor.Instrument) region.Notes.Add(new MidiNote { StartBeat = 0, LengthBeats = 2, Pitch = 60, Velocity = 100 });
            else region.MediaPath = wave;
            track.Regions.Add(region);
            track.Plugins.Add(descriptor.CreateSlot()); project.Tracks.Add(track);
            using (var engine = new ArrangementEngine(new AppSettings { SampleRate = 48000, BufferFrames = 256 }))
            {
                var first = engine.RenderOffline(project, 48000, 0); float a = Peak(first);
                if (a < .001f || engine.Warnings.Count > 0) throw new Exception("Application graph did not process the signal: " + a + "; " + String.Join("; ", engine.Warnings.ToArray()));
                engine.CapturePluginStates();
                if (String.IsNullOrEmpty(project.Tracks[0].Plugins[0].State)) throw new Exception("Plug-in state was not saved.");
                var reopened = ProjectFile.Deserialize(ProjectFile.Serialize(project)); engine.ResetProject();
                var second = engine.RenderOffline(reopened, 48000, 0); float b = Peak(second);
                if (b < .001f || engine.Warnings.Count > 0) throw new Exception("Restored graph did not process the signal: " + b + "; " + String.Join("; ", engine.Warnings.ToArray()));
                File.WriteAllText(Path.Combine(root, "audio.txt"), "synthetic peak " + a + " / " + b);
            }
            var settings = new AppSettings { AudioBackend = "WASAPI" }; settings.Integration.QuickStart = false;
            var locale = LocaleCatalog.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales", "en-US.json"), "en-US");
            using (var form = new MainForm(settings, locale, false, null))
            {
                form.Show(); Pump(150);
                var live = new ProjectDocument(); var target = ProjectFactory.CreateTrack("GUI insertion", descriptor.Instrument ? TrackType.Midi : TrackType.Audio); live.Tracks.Add(target);
                typeof(MainForm).GetField("_project", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, live);
                typeof(MainForm).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Single(m => m.Name == "Select" && m.GetParameters().Length == 2).Invoke(form, new object[] { target.Id, null });
                string message = null;
                var guard = new System.Windows.Forms.Timer { Interval = 100 };
                guard.Tick += delegate {
                    EnumWindows((handle, data) => {
                        uint pid; GetWindowThreadProcessId(handle, out pid); if (pid != (uint)Process.GetCurrentProcess().Id) return true;
                        var name = new StringBuilder(256); GetClassName(handle, name, name.Capacity); if (name.ToString() != "#32770") return true;
                        var text = new StringBuilder(); EnumChildWindows(handle, (child, extra) => { var value = new StringBuilder(1024); GetWindowText(child, value, value.Capacity); if (value.Length > 0) text.Append(value).Append(" | "); return true; }, IntPtr.Zero);
                        message = text.ToString(); PostMessage(handle, 0x10, IntPtr.Zero, IntPtr.Zero); return true;
                    }, IntPtr.Zero);
                };
                guard.Start();
                File.WriteAllText(Path.Combine(root, "stage.txt"), "before application insert");
                typeof(MainForm).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Single(m => m.Name == "LoadPlugin" && m.GetParameters().Length == 2).Invoke(form, new object[] { target, descriptor });
                File.WriteAllText(Path.Combine(root, "stage.txt"), "application insert returned");
                Pump(200);
                File.WriteAllText(Path.Combine(root, "stage.txt"), "events pumped after insert");
                if (message != null) throw new Exception("Application reported an error: " + message);
                var current = (ProjectDocument)typeof(MainForm).GetField("_project", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                var slot = current.Tracks.SelectMany(t => t.Plugins).FirstOrDefault(p => p.ClassId == descriptor.ClassId);
                var appEngine = (ArrangementEngine)typeof(MainForm).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                if (slot == null || appEngine.GetPlugin(slot.Id) == null || !appEngine.GetPlugin(slot.Id).Healthy || !Application.OpenForms.OfType<PluginEditorForm>().Any())
                    throw new Exception("Application insert command did not create a healthy instance and editor.");
                File.WriteAllText(Path.Combine(root, "stage.txt"), "before closing editor");
                foreach (var window in Application.OpenForms.OfType<PluginEditorForm>().ToArray()) window.Close();
                File.WriteAllText(Path.Combine(root, "stage.txt"), "after closing editor");
                Pump(100); guard.Stop(); guard.Dispose(); if (message != null) throw new Exception("Application reported an error: " + message);
                typeof(MainForm).GetField("_closing", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, true);
                form.Close(); Pump(100);
            }
            string line = "PASS " + descriptor.Name + ": real native editor with host preset bar, preset save/load while open, application insert command, app-track " + (descriptor.Instrument ? "MIDI synthesis" : "audio processing") + ", state capture, project reopen and graph restoration; " + File.ReadAllText(Path.Combine(root, "audio.txt")) + ".";
            File.WriteAllText(report, line); Console.WriteLine(line);
            return 0;
        }
        catch (Exception error) { string line = "FAIL " + error; File.WriteAllText(report, line); Console.WriteLine(line); return 1; }
    }
}
