using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SlopStudio
{
    public sealed class SlopPresetRecord
    {
        public string Schema { get; set; }
        public int Version { get; set; }
        public string Format { get; set; }
        public string PluginId { get; set; }
        public string State { get; set; }
    }

    public static class SlopPresetFile
    {
        private const int MaximumBytes = 8 * 1024 * 1024;
        public static SlopPresetRecord Create(string format, string id, string state)
        {
            if (String.IsNullOrEmpty(format) || String.IsNullOrEmpty(id) || String.IsNullOrEmpty(state))
                throw new InvalidDataException("This processor does not expose a savable state.");
            return new SlopPresetRecord { Schema = "slopstudio-preset", Version = 1, Format = format, PluginId = id, State = state };
        }
        public static string Encode(SlopPresetRecord preset)
        {
            Validate(preset);
            return new JavaScriptSerializer { MaxJsonLength = MaximumBytes }.Serialize(preset);
        }
        public static SlopPresetRecord Decode(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new InvalidDataException("Preset exceeds the size limit.");
            var preset = new JavaScriptSerializer { MaxJsonLength = MaximumBytes }.Deserialize<SlopPresetRecord>(json);
            Validate(preset);
            return preset;
        }
        public static void Validate(SlopPresetRecord preset)
        {
            if (preset == null || preset.Schema != "slopstudio-preset" || preset.Version != 1 ||
                String.IsNullOrEmpty(preset.Format) || String.IsNullOrEmpty(preset.PluginId) || String.IsNullOrEmpty(preset.State))
                throw new InvalidDataException("Unsupported or incomplete Slop Studio preset.");
        }
        public static void RequireCompatible(SlopPresetRecord preset, string format, string id)
        {
            Validate(preset);
            if (!String.Equals(preset.Format, format, StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(preset.PluginId, id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("This preset belongs to a different plug-in or processor.");
        }
        public static void Save(string path, SlopPresetRecord preset)
        {
            if (!String.Equals(Path.GetExtension(path), ".sloppreset", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Use the .sloppreset extension.");
            string full = Path.GetFullPath(path);
            string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string json = Encode(preset);
            if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new InvalidDataException("Preset exceeds the size limit.");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) { writer.Write(json); writer.Flush(); stream.Flush(true); }
                if (File.Exists(full)) File.Replace(temporary, full, full + "." + DateTime.UtcNow.Ticks + ".bak");
                else File.Move(temporary, full);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public static SlopPresetRecord Load(string path)
        {
            var file = new FileInfo(path);
            if (!file.Exists) throw new FileNotFoundException("Preset file was not found.", path);
            if (file.Length > MaximumBytes) throw new InvalidDataException("Preset exceeds the size limit.");
            return Decode(File.ReadAllText(path, new UTF8Encoding(false, true)));
        }
    }

    public sealed class PresetToolbar : UserControl
    {
        private readonly string _format, _id;
        private readonly Func<string> _capture;
        private readonly Action<string> _restore;
        private string _reference, _alternate;
        private bool _comparing;
        private readonly Button _compare;
        private static SlopPresetRecord _clipboard;

        public PresetToolbar(string format, string id, Func<string> capture, Action<string> restore)
        {
            _format = format; _id = id; _capture = capture; _restore = restore;
            Dock = DockStyle.Top; Height = 42; BackColor = CyberTheme.Surface;
            var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(9, 5, 0, 3) };
            var menuButton = Button("Preset ▾", 100); var menu = new ContextMenuStrip();
            menu.Items.Add(UiText.Translate("Load Preset…"), null, delegate { Run(LoadDialog); });
            menu.Items.Add(UiText.Translate("Save Preset…"), null, delegate { Run(SaveDialog); });
            menuButton.Click += delegate { menu.Show(menuButton, new Point(0, menuButton.Height)); };
            var copy = Button("Copy Setting", 104); copy.Click += delegate { Run(CopyPreset); };
            var paste = Button("Paste Setting", 104); paste.Click += delegate { Run(PastePreset); };
            _compare = Button("Compare", 86); _compare.Enabled = false; _compare.Click += delegate { Run(ToggleCompare); };
            row.Controls.Add(menuButton); row.Controls.Add(copy); row.Controls.Add(paste); row.Controls.Add(_compare);
            row.Controls.Add(new Label { Text = UiText.Translate("Slop Studio presets"), AutoSize = true, ForeColor = CyberTheme.Secondary, Margin = new Padding(10, 6, 0, 0) });
            Controls.Add(row); Disposed += delegate { menu.Dispose(); };
        }
        private static Button Button(string text, int width)
        {
            return new Button { Text = UiText.Translate(text), Width = width, Height = 29, FlatStyle = FlatStyle.Flat, BackColor = CyberTheme.Control, ForeColor = CyberTheme.Text, Margin = new Padding(2, 0, 2, 0) };
        }
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception error) { MessageBox.Show(FindForm(), UiText.Translate(error.Message), UiText.Translate("Preset operation failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        public void CaptureReference()
        {
            try { _reference = SlopPresetFile.Create(_format, _id, _capture()).State; _compare.Enabled = true; }
            catch { _reference = null; _compare.Enabled = false; }
        }
        public void CopyPreset() { _clipboard = SlopPresetFile.Create(_format, _id, _capture()); }
        public void PastePreset()
        {
            if (_clipboard == null) throw new InvalidOperationException("No copied setting is available.");
            SlopPresetFile.RequireCompatible(_clipboard, _format, _id);
            _restore(_clipboard.State); _comparing = false; _alternate = null; _compare.BackColor = CyberTheme.Control;
        }
        public void SaveTo(string path) { SlopPresetFile.Save(path, SlopPresetFile.Create(_format, _id, _capture())); }
        public void LoadFrom(string path)
        {
            var preset = SlopPresetFile.Load(path);
            SlopPresetFile.RequireCompatible(preset, _format, _id);
            _restore(preset.State); _comparing = false; _alternate = null; _compare.BackColor = CyberTheme.Control;
        }
        public void ToggleCompare()
        {
            if (_reference == null) throw new InvalidOperationException("No comparison state is available.");
            if (!_comparing) { _alternate = _capture(); _restore(_reference); _comparing = true; }
            else { _restore(_alternate); _comparing = false; }
            _compare.BackColor = _comparing ? CyberTheme.AccentDark : CyberTheme.Control;
        }
        private void SaveDialog()
        {
            using (var dialog = new SaveFileDialog { Filter = "Slop Studio preset|*.sloppreset", DefaultExt = "sloppreset", AddExtension = true, OverwritePrompt = true, FileName = "Preset.sloppreset" })
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK) SaveTo(dialog.FileName);
        }
        private void LoadDialog()
        {
            using (var dialog = new OpenFileDialog { Filter = "Slop Studio preset|*.sloppreset", CheckFileExists = true })
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK) LoadFrom(dialog.FileName);
        }
    }
}
