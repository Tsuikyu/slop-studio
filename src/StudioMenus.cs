using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SlopProX
{
    public sealed partial class MainForm
    {
        private bool _mcpAllowEditing;
        private readonly List<string> _mcpAudit = new List<string>();

        private void InitializeStudioMenus()
        {
            var app = new ToolStripMenuItem("Slop Studio");
            AddMenuItem(app, ShowAbout).Text = "About Slop Studio";
            app.DropDownItems.Add(new ToolStripSeparator());
            var settings = new ToolStripMenuItem("Settings");
            string[] pageNames = { "Audio Settings…", "MIDI Settings…", "Plug-in Settings…", "Display Settings…", "Project Defaults…", "Integration Settings…", "Key Commands…" };
            for (int page = 0; page < pageNames.Length; page++) { int selectedPage = page; AddMenuItem(settings, delegate { OpenPreferencesPage(selectedPage); }).Text = pageNames[page]; }
            app.DropDownItems.Add(settings);
            AddCommandMenu(app, AppCommand.OpenPreferences, "All Settings…");
            AddCommandMenu(app, AppCommand.QuickStart, "Quick Start…");
            AddCommandMenu(app, AppCommand.UserManual, "User Manual");
            app.DropDownItems.Add(new ToolStripSeparator());
            AddMenuItem(app, Close).Text = "Quit Slop Studio";
            _menu.Items.Insert(0, app);

            // Expose existing commands through menus as well as shortcuts and context menus.
            var track = _menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Track");
            if (track != null)
            {
                track.DropDownItems.Add(new ToolStripSeparator());
                AddCommandMenu(track, AppCommand.RenameTrack, "Rename Track");
                AddCommandMenu(track, AppCommand.DeleteTrack, "Delete Track");
                AddCommandMenu(track, AppCommand.CreateFolderStack, "Create Folder Stack");
                AddCommandMenu(track, AppCommand.CreateSummingStack, "Create Mix Stack");
            }
            _editMenu.DropDownItems.Add(new ToolStripSeparator());
            AddCommandMenu(_editMenu, AppCommand.DuplicateRegion, "Duplicate Region");
            AddCommandMenu(_editMenu, AppCommand.SplitRegion, "Split at Playhead");
            AddCommandMenu(_editMenu, AppCommand.Quantize, "Quantize Notes");
            var mix = _menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Mix");
            if (mix != null) { AddCommandMenu(mix, AppCommand.UnmuteAll, "Unmute All"); AddCommandMenu(mix, AppCommand.ShowAutomation, "Show Automation"); }
            _viewMenu.DropDownItems.Add(new ToolStripSeparator());
            AddCommandMenu(_viewMenu, AppCommand.ShowPianoRoll, "Piano Roll");
            AddCommandMenu(_viewMenu, AppCommand.ZoomIn, "Zoom In");
            AddCommandMenu(_viewMenu, AppCommand.ZoomOut, "Zoom Out");
            _fileMenu.DropDownItems.Remove(_exitMenuItem);
            _fileMenu.DropDownItems.Add(new ToolStripSeparator());
            _fileMenu.DropDownItems.Add(_exitMenuItem);

            var window = _menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Window");
            var mods = _menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Mods");
            if (window != null)
            {
                window.DropDownItems.Add(new ToolStripSeparator());
                if (mods != null)
                {
                    _menu.Items.Remove(mods);
                    mods.Text = "Lua Scripts";
                    var manager = new ToolStripMenuItem("Lua Mod Manager…");manager.Click+=delegate{ShowLuaManager();};mods.DropDownItems.Insert(0,manager);
                    window.DropDownItems.Add(mods);
                }
                else AddCommandMenu(window, AppCommand.LoadLua, "Load Lua Mod…");
                AddCommandMenu(window, AppCommand.OpenOscSettings, "OSC / VRChat Settings…");
                AddCommandMenu(window, AppCommand.OpenMcpManager, "MCP AI Integration Manager…");
                AddCommandMenu(window, AppCommand.OpenControllerSettings, "Game Controller Settings…");
                AddCommandMenu(window, AppCommand.Panic, "Panic — Stop All Notes");
            }

            var screensets = new ToolStripMenuItem("1") { Enabled = false, ToolTipText = "Screensets are not available in this version." };
            _menu.Items.Insert(_menu.Items.IndexOf(_helpMenu), screensets);
        }

        private void AuditMcp(string name, string result)
        {
            if (_mcpAudit.Count >= 200) _mcpAudit.RemoveAt(0);
            _mcpAudit.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + name + "  " + result);
        }

        private void AuthorizeMcpRequest(string name, IDictionary<string, object> arguments)
        {
            if (name == "get_project") { AuditMcp(name, "read"); return; }
            if (!_mcpAllowEditing)
            {
                AuditMcp(name, "denied");
                throw new InvalidOperationException(UiText.Translate("MCP editing is disabled. Enable it in the MCP manager for this session."));
            }
            bool fileAction = name == "save_project" || name == "open_project" || name == "export_audio";
            bool armAction = name == "set_track_mix" && arguments.ContainsKey("arm");
            if (fileAction || armAction)
            {
                string target = fileAction && arguments.ContainsKey("path") ? Convert.ToString(arguments["path"]) : String.Empty;
                string message = UiText.Translate("An MCP client requests an operation requiring approval:") + Environment.NewLine + name;
                if (!String.IsNullOrEmpty(target)) message += Environment.NewLine + target;
                if (MessageBox.Show(this, message, "Slop Studio — MCP", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    AuditMcp(name, "rejected");
                    throw new InvalidOperationException(UiText.Translate("MCP operation was not approved."));
                }
            }
            AuditMcp(name, "allowed");
        }

        private void ShowMcpManager()
        {
            using (var form = new Form { Text = UiText.Translate("MCP AI Integration Manager"), ClientSize = new Size(620, 390), StartPosition = FormStartPosition.CenterParent, BackColor = CyberTheme.Background, ForeColor = CyberTheme.Text, Font = new Font("Segoe UI", 9) })
            {
                var heading = new Label { Dock = DockStyle.Top, Height = 76, Padding = new Padding(16, 12, 16, 0), Text = UiText.Translate(McpMode ? "MCP server: active over standard input/output." : "MCP server: stopped. Launch SlopStudio.exe --mcp from a local client.") + Environment.NewLine + UiText.Translate("Editing is disabled by default; file operations require separate approval.") };
                var allow = new CheckBox { Dock = DockStyle.Top, Height = 34, Padding = new Padding(16, 0, 0, 0), Text = "Allow MCP editing for this session", Checked = _mcpAllowEditing, Enabled = McpMode };
                var history = new ListBox { Dock = DockStyle.Fill, BackColor = CyberTheme.Elevated, ForeColor = CyberTheme.Text, BorderStyle = BorderStyle.None };
                foreach (string item in _mcpAudit) history.Items.Add(item);
                var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 47, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
                var close = new Button { Text = "Close", Width = 105, Height = 30, FlatStyle = FlatStyle.Flat, DialogResult = DialogResult.Cancel };
                var apply = new Button { Text = "Apply", Width = 105, Height = 30, FlatStyle = FlatStyle.Flat, Enabled = McpMode };
                apply.Click += delegate { _mcpAllowEditing = allow.Checked; AuditMcp("permission", _mcpAllowEditing ? "editing enabled" : "read only"); form.Close(); };
                footer.Controls.Add(close); footer.Controls.Add(apply);
                form.Controls.Add(history); form.Controls.Add(allow); form.Controls.Add(heading); form.Controls.Add(footer);
                UiText.Apply(form); form.ShowDialog(this);
            }
        }
    }
}
