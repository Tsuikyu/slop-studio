using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SlopStudio
{
    public static class LuaModRegistry
    {
        private static string FileName { get { return Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath), "lua-mods.json"); } }
        public static List<string> Load()
        {
            if (!File.Exists(FileName)) return new List<string>();
            if (new FileInfo(FileName).Length > 128 * 1024) throw new InvalidDataException("Lua Mod registry is too large.");
            var list = new JavaScriptSerializer().Deserialize<List<string>>(File.ReadAllText(FileName, Encoding.UTF8));
            if (list == null || list.Count > 256 || list.Any(p => String.IsNullOrWhiteSpace(p) || p.Length > 32767)) throw new InvalidDataException("Lua Mod registry is invalid.");
            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static void Save(IEnumerable<string> paths)
        {
            var list = paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (list.Count > 256 || list.Any(p => !String.Equals(Path.GetExtension(p), ".lua", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Invalid Lua Mod list.");
            Directory.CreateDirectory(Path.GetDirectoryName(FileName)); string temp = FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, new JavaScriptSerializer().Serialize(list), new UTF8Encoding(false));
                if (File.Exists(FileName)) File.Replace(temp, FileName, FileName + "." + Guid.NewGuid().ToString("N") + ".bak"); else File.Move(temp, FileName);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static void Add(string path) { var list = Load(); list.Add(Path.GetFullPath(path)); Save(list); }
    }

    public sealed class LuaManagerForm : Form
    {
        private readonly ListBox _list;
        private readonly Action<string> _run;
        public LuaManagerForm(Action<string> run)
        {
            _run = run; Text = UiText.Translate("Lua Mod Manager"); ClientSize = new Size(700, 460); MinimumSize = new Size(520, 330);
            StartPosition = FormStartPosition.CenterParent; BackColor = CyberTheme.Background; ForeColor = CyberTheme.Text;
            var info = new Label { Dock = DockStyle.Top, Height = 58, Padding = new Padding(12, 8, 8, 0), Text = UiText.Translate("Mods run only when selected. Removing a file from this list does not undo its project changes or delete the file.") };
            _list = new ListBox { Dock = DockStyle.Fill, BackColor = CyberTheme.Elevated, ForeColor = CyberTheme.Text, HorizontalScrollbar = true };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8, 6, 0, 0), WrapContents = false };
            var add = new Button { Text = UiText.Translate("Add…"), AutoSize = true, Height = 28 };
            var remove = new Button { Text = UiText.Translate("Remove from List"), AutoSize = true, Height = 28 };
            var execute = new Button { Text = UiText.Translate("Run Selected"), AutoSize = true, Height = 28 };
            add.Click += delegate { using (var dialog = new OpenFileDialog { Filter = "Lua Mod|*.lua", Multiselect = true }) if (dialog.ShowDialog(this) == DialogResult.OK) { try { var paths = LuaModRegistry.Load(); paths.AddRange(dialog.FileNames); LuaModRegistry.Save(paths); RefreshList(); } catch (Exception ex) { Report(ex); } } };
            remove.Click += delegate { if (_list.SelectedItem == null) return; try { var paths = LuaModRegistry.Load(); paths.RemoveAll(p => String.Equals(p, Convert.ToString(_list.SelectedItem), StringComparison.OrdinalIgnoreCase)); LuaModRegistry.Save(paths); RefreshList(); } catch (Exception ex) { Report(ex); } };
            execute.Click += delegate { string path = Convert.ToString(_list.SelectedItem); if (String.IsNullOrEmpty(path)) return; if (!File.Exists(path)) { Report(new FileNotFoundException("Lua Mod file is unavailable.", path)); return; } _run(path); };
            buttons.Controls.Add(add); buttons.Controls.Add(remove); buttons.Controls.Add(execute);
            Controls.Add(_list); Controls.Add(info); Controls.Add(buttons); RefreshList(); Shown += delegate { UiText.Apply(this); };
        }
        private void RefreshList() { try { _list.Items.Clear(); foreach (string path in LuaModRegistry.Load()) _list.Items.Add(path); } catch (Exception ex) { Report(ex); } }
        private void Report(Exception error) { MessageBox.Show(this, error.Message, UiText.Translate("Lua Mod Manager"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    public sealed partial class MainForm
    {
        private void ShowLuaManager() { var window = new LuaManagerForm(LoadLuaMod); window.Show(this); }
    }
}
