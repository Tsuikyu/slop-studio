using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using SlopProX;

internal static class V11GuiTests
{
    private static readonly BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly List<string> Lines = new List<string>();
    private static readonly List<Exception> Errors = new List<Exception>();
    private static string _root;
    private static int _failed;
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h, int message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    private static object Field(object obj, string name) { for (Type t = obj.GetType(); t != null; t = t.BaseType) { var f = t.GetField(name, F | BindingFlags.DeclaredOnly); if (f != null) return f.GetValue(obj); } throw new Exception(name); }
    private static void Set(object obj, string name, object value) { obj.GetType().GetField(name, F).SetValue(obj, value); }
    private static object Call(object obj, string name, params object[] args)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            var m = t.GetMethods(F | BindingFlags.DeclaredOnly).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == args.Length);
            if (m != null) return m.Invoke(obj, args);
        }
        throw new Exception(name);
    }
    private static IEnumerable<Control> Children(Control root) { foreach (Control c in root.Controls) { yield return c; foreach (var child in Children(c)) yield return child; } }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Pump(int ms) { var sw = Stopwatch.StartNew(); while (sw.ElapsedMilliseconds < ms) { Application.DoEvents(); Thread.Sleep(3); } }
    private static void Test(string name, Action test)
    {
        int before = Errors.Count;
        try { test(); Pump(40); Check(before == Errors.Count, "UI exception: " + (Errors.Count > before ? Errors.Last().ToString() : "")); Lines.Add("PASS " + name); }
        catch (Exception error) { _failed++; Lines.Add("FAIL " + name + " :: " + error); }
        File.WriteAllLines(Path.Combine(_root, "gui.txt"), Lines);
    }
    private static void Mouse(Control control, int message, double x, double y, int buttons)
    {
        float scale = control is ScaledCanvas ? ((ScaledCanvas)control).CanvasScale : 1;
        int ix = (int)(x * scale), iy = (int)(y * scale);
        SendMessage(control.Handle, message, (IntPtr)buttons, (IntPtr)((iy << 16) | (ix & 65535)));
    }
    private static void Drag(Control control, double x, double y, double endX, double endY)
    {
        Mouse(control, 0x201, x, y, 1); Mouse(control, 0x200, endX, endY, 1); Mouse(control, 0x202, endX, endY, 0); Pump(40);
    }
    private static void Screenshot(Form form, string name)
    {
        Pump(50);
        using (var bitmap = new Bitmap(form.Width, form.Height))
        {
            using (var g = Graphics.FromImage(bitmap)) { IntPtr dc = g.GetHdc(); try { Check(PrintWindow(form.Handle, dc, 2), "Window capture failed."); } finally { g.ReleaseHdc(dc); } }
            bitmap.Save(Path.Combine(_root, name + ".png"));
        }
    }
    [STAThread] private static int Main(string[] args)
    {
        _root = Path.Combine(Path.GetFullPath(args[0]), "run-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("SLOPX_PROFILE", Path.Combine(_root, "profile"));
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { Errors.Add(e.Exception); };
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); UiText.Load("en-US");
        var settings = new AppSettings { AudioBackend = "WASAPI" }; settings.Integration.QuickStart = false;
        var locale = LocaleCatalog.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales", "en-US.json"), "en-US");
        using (var form = new MainForm(settings, locale, false, null))
        {
            form.Show(); Pump(200);
            var project = new ProjectDocument { Name = "V11 GUI fixture" };
            var track = ProjectFactory.CreateTrack("Fixture", TrackType.Midi);
            var region = ProjectFactory.CreateRegion("Notes", 0, 4); region.Notes.Add(new MidiNote { Pitch = 60, LengthBeats = 1 }); track.Regions.Add(region); project.Tracks.Add(track);
            var compressor = StockEffects.Create(track.Id, "Compressor"); project.Studio.Effects.Add(compressor);
            Set(form, "_project", project); ((ProjectHistory)Field(form, "_history")).Reset(true); Call(form, "Select", track.Id, region.Id); Pump(80);
            var engine = (ArrangementEngine)Field(form, "_engine");
            Test("Dark file menu has readable text and the application menu exposes real settings", delegate {
                var menu = (MenuStrip)Field(form, "_menu");
                var app = menu.Items.OfType<ToolStripMenuItem>().First(item => item.Text == "Slop Studio");
                Check(app.DropDownItems.OfType<ToolStripMenuItem>().Count() >= 5, "Application menu is missing actions.");
                var settingsMenu = app.DropDownItems.OfType<ToolStripMenuItem>().First(item => item.Text == "Settings");
                Check(settingsMenu.DropDownItems.OfType<ToolStripMenuItem>().Count() == 7, "Settings categories missing.");
                var file = menu.Items.OfType<ToolStripMenuItem>().First(item => item.Text == "File");
                file.ShowDropDown(); Pump(100); var drop = file.DropDown; var item0 = file.DropDownItems[0];
                using (var bitmap = new Bitmap(drop.Width, drop.Height))
                {
                    drop.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(_root, "file-menu.png"));
                    int bright = 0; Rectangle area = Rectangle.Intersect(item0.Bounds, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    for (int y = area.Top + 2; y < area.Bottom - 2; y++) for (int x = area.Left + 25; x < area.Right - 4; x++)
                    { Color c = bitmap.GetPixel(x, y); if (c.R > 175 && c.G > 175 && c.B > 175) bright++; }
                    Check(bright > 15, "No light text pixels were rendered on the dark menu.");
                }
                file.HideDropDown();
            });
            Test("Compressor knob and presets change the live project and preserve undo", delegate {
                Call(form, "OpenStockEffect", compressor.Id); Pump(80);
                var editor = Application.OpenForms.OfType<StockEffectEditor>().First();
                var knob = Children(editor).OfType<EffectKnob>().First(k => k.Parameter.Key == "Threshold");
                var processor = engine.GetStockProcessor(compressor.Id);
                double before = ((ProjectDocument)Field(form, "_project")).Studio.Effects.Find(f => f.Id == compressor.Id).Value("Threshold", 0);
                Drag(knob, knob.Width / 2, knob.Height / 2, knob.Width / 2, knob.Height / 2 + 45);
                double changed = ((ProjectDocument)Field(form, "_project")).Studio.Effects.Find(f => f.Id == compressor.Id).Value("Threshold", 0);
                Check(Math.Abs(changed - before) > 1 && Object.ReferenceEquals(processor, engine.GetStockProcessor(compressor.Id)), "Knob did not update the running processor.");
                var bar = Children(editor).OfType<PresetToolbar>().Single(); string path = Path.Combine(_root, "compressor.sloppreset"); bar.SaveTo(path); bar.CopyPreset();
                Drag(knob, knob.Width / 2, knob.Height / 2, knob.Width / 2, knob.Height / 2 - 20); bar.PastePreset();
                Check(Math.Abs(((ProjectDocument)Field(form, "_project")).Studio.Effects.Find(f => f.Id == compressor.Id).Value("Threshold", 0) - changed) < .01, "Paste did not restore settings.");
                bar.LoadFrom(path); Check(Math.Abs(((ProjectDocument)Field(form, "_project")).Studio.Effects.Find(f => f.Id == compressor.Id).Value("Threshold", 0) - changed) < .01, "Preset did not restore settings.");
                bar.ToggleCompare(); bar.ToggleCompare();
                Call(form, "Undo"); double undone = ((ProjectDocument)Field(form, "_project")).Studio.Effects.Find(f => f.Id == compressor.Id).Value("Threshold", 0);
                var number = Children(editor).OfType<NumericUpDown>().First(n => n.AccessibleName == "Threshold value");
                Check(Math.Abs(undone - before) < .01 && Math.Abs((double)number.Value - before) < .01, "Undo did not refresh the open editor.");
                Call(form, "Redo"); Check(Math.Abs(((ProjectDocument)Field(form, "_project")).Studio.Effects.Find(f => f.Id == compressor.Id).Value("Threshold", 0) - changed) < .01, "Redo did not restore the compared setting.");
                Screenshot(editor, "compressor"); editor.Close();
            });
            Test("EQ graph drag edits frequency and gain in the real model", delegate {
                var state = ((ProjectDocument)Field(form, "_project")).Studio.Copy(); var eq = StockEffects.Create(track.Id, "Visual Equalizer"); state.Effects.Add(eq);
                Call(form, "CommitStudio", "Add EQ fixture", state); Call(form, "OpenStockEffect", eq.Id); Pump(80);
                var editor = Application.OpenForms.OfType<StockEffectEditor>().First(); var graph = Children(editor).OfType<EqGraph>().Single();
                double x = 48 + Math.Log10(80.0 / 20) / 3 * (graph.Width - 68), y = 30 + (graph.Height - 58) / 2.0;
                Drag(graph, x, y, x + 35, y - 25);
                var current = ((ProjectDocument)Field(form, "_project")).Studio.Effects.Find(f => f.Id == eq.Id);
                Check(current.Value("Frequency0", 80) > 100 && current.Value("Gain0", 0) > 1, "EQ drag did not change both parameters.");
                Screenshot(editor, "equalizer"); editor.Close();
            });
            Test("Sampler exposes four ADSR knobs and round-trips their preset", delegate {
                Call(form, "OpenSampler", track); Pump(80);
                var sampler = Application.OpenForms.OfType<SamplerEditor>().Single();
                var knobs = Children(sampler).OfType<EffectKnob>().ToArray(); Check(knobs.Length == 4, "Four envelope knobs missing.");
                var attack = knobs.First(k => k.Parameter.Key == "Attack (s)");
                var bar = Children(sampler).OfType<PresetToolbar>().Single(); string path = Path.Combine(_root, "sampler.sloppreset"); bar.SaveTo(path);
                Drag(attack, attack.Width / 2, attack.Height / 2, attack.Width / 2, attack.Height / 2 - 40);
                Check(((ProjectDocument)Field(form, "_project")).Studio.Sampler(track.Id).Attack > .05, "Attack knob did not change Sampler.");
                bar.LoadFrom(path); Check(Math.Abs(((ProjectDocument)Field(form, "_project")).Studio.Sampler(track.Id).Attack - .004) < .002, "Sampler preset did not restore attack.");
                Screenshot(sampler, "sampler"); sampler.Close();
            });
            Test("Native VST3 and CLAP parameter editors expose working host preset bars", delegate {
                foreach(string format in new[]{"vst3","clap"}){
                var descriptor = NativePlugin.Scan(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFixtures", "FixtureGain."+format))[0];
                using (var plugin = new NativePlugin(descriptor.CreateSlot(), 48000, 256))
                using (var window = new PluginEditorForm(plugin))
                {
                    window.Show(form); Pump(100); var bar = Children(window).OfType<PresetToolbar>().Single(); var parameter = plugin.Parameters()[0];
                    plugin.SetParameter(parameter.Id, .8); var block = new float[512]; plugin.Process(block, 256, new MidiEventPacket[0], 0, 120, 0, true, 4, 4);
                    string path = Path.Combine(_root, "external-"+format+".sloppreset"); bar.SaveTo(path); plugin.SetParameter(parameter.Id, .1); plugin.Process(block, 256, new MidiEventPacket[0], 0, 120, 0, true, 4, 4);
                    bar.LoadFrom(path); Check(Math.Abs(plugin.GetParameter(parameter.Id) - .8) < .01, "External plug-in preset did not restore.");
                    Screenshot(window, "plugin-host-bar-"+format); window.Close();
                }
                }
            });
            Set(form, "_closing", true); form.Close();
        }
        Test("Japanese menu has readable localized text", delegate {
            var japanese = new AppSettings { AudioBackend = "WASAPI", Language = "ja-JP" }; japanese.Integration.QuickStart = false;
            var ja = LocaleCatalog.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales", "ja-JP.json"), "ja-JP");
            using (var form = new MainForm(japanese, ja, false, null))
            {
                form.Show(); Pump(120); var menu = (MenuStrip)Field(form, "_menu");
                var file = menu.Items.OfType<ToolStripMenuItem>().First(item => item.Text == "ファイル");
                file.ShowDropDown(); Pump(80); var drop = file.DropDown; var first = file.DropDownItems[0];
                Check(first.Text.Contains("プロジェクト"), "Japanese label is missing.");
                Check(!file.DropDownItems.OfType<ToolStripMenuItem>().Any(item => item.Text.StartsWith("Open Example") || item.Text.StartsWith("Bounce")), "File menu has untranslated actions.");
                using (var bitmap = new Bitmap(drop.Width, drop.Height))
                {
                    drop.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(_root, "file-menu-ja.png"));
                    int bright = 0; Rectangle area = Rectangle.Intersect(first.Bounds, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    for (int y = area.Top + 2; y < area.Bottom - 2; y++) for (int x = area.Left + 25; x < area.Right - 4; x++)
                    { Color c = bitmap.GetPixel(x, y); if (c.R > 175 && c.G > 175 && c.B > 175) bright++; }
                    Check(bright > 15, "Japanese menu text is not readable.");
                }
                file.HideDropDown();
                var app = menu.Items.OfType<ToolStripMenuItem>().First(item => item.Text == "Slop Studio");
                app.ShowDropDown(); Pump(60);
                Check(app.DropDownItems.OfType<ToolStripMenuItem>().Any(item => item.Text == "環境設定"), "Localized application settings menu is missing.");
                using (var bitmap = new Bitmap(app.DropDown.Width, app.DropDown.Height)) { app.DropDown.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(_root, "app-menu-ja.png")); }
                app.HideDropDown();
                var project = new ProjectDocument { Name = "Japanese editor fixture" };
                var track = ProjectFactory.CreateTrack("Test", TrackType.Midi); project.Tracks.Add(track);
                var eq = StockEffects.Create(track.Id, "Visual Equalizer"); project.Studio.Effects.Add(eq);
                Set(form, "_project", project); ((ProjectHistory)Field(form, "_history")).Reset(true); Call(form, "Select", track.Id, null);
                Call(form, "OpenStockEffect", eq.Id); Pump(80);
                var eqWindow = Application.OpenForms.OfType<StockEffectEditor>().Single();
                Check(Children(eqWindow).OfType<Label>().Any(label => label.Text.Contains("周波数")), "EQ parameter labels were not translated.");
                Check(Children(eqWindow).OfType<Button>().Any(button => button.Text.Contains("プリセット")), "EQ preset toolbar was not translated.");
                Screenshot(eqWindow, "equalizer-ja"); eqWindow.Close();
                Call(form, "OpenSampler", track); Pump(80);
                var sampler = Application.OpenForms.OfType<SamplerEditor>().Single();
                Check(Children(sampler).OfType<Label>().Any(label => label.Text.Contains("アタック")), "Sampler envelope labels were not translated.");
                Screenshot(sampler, "sampler-ja"); sampler.Close();
                Set(form, "_closing", true); form.Close();
            }
        });
        Lines.Add("NOT RUN physical mouse, MIDI keyboard, microphone or speaker audition.");
        File.WriteAllLines(Path.Combine(_root, "gui.txt"), Lines); Console.WriteLine(String.Join(Environment.NewLine, Lines.ToArray()));
        return _failed == 0 ? 0 : 1;
    }
}
