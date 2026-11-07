using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlopProX
{
    public sealed class PluginEditorForm : Form
    {
        private readonly NativePlugin _plugin;
        private readonly Timer _timer;
        private readonly Panel _body;
        private readonly PresetToolbar _presets;
        public event EventHandler StateChanged;

        public PluginEditorForm(NativePlugin plugin)
        {
            _plugin = plugin; Text = plugin.Slot.Name + " — " + plugin.Slot.Format;
            BackColor = CyberTheme.Background; ForeColor = CyberTheme.Text; StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(600, 462);
            _body = new Panel { Dock = DockStyle.Fill };
            _presets = new PresetToolbar(plugin.Slot.Format, plugin.Slot.ClassId, () => _plugin.SnapshotState(), RestorePreset);
            Controls.Add(_body); Controls.Add(_presets);
            _timer = new Timer { Interval = 33 };
            _timer.Tick += delegate { try { _plugin.Idle(); } catch { _timer.Stop(); } };
            Shown += OnShown;
            FormClosed += delegate
            {
                _timer.Stop(); _timer.Dispose();
                try { _plugin.CloseEditor(); } catch (Exception error) { ErrorLog.Write("Close plug-in editor", error); }
                EventHandler handler = StateChanged; if (handler != null) handler(this, EventArgs.Empty);
            };
        }
        private void RestorePreset(string state)
        {
            string before = _plugin.SnapshotState();
            try { _plugin.RestoreState(state); }
            catch { try { _plugin.RestoreState(before); } catch (Exception rollback) { ErrorLog.Write("Plug-in preset rollback", rollback); } throw; }
        }
        private void OnShown(object sender, EventArgs e)
        {
            int width = 0, height = 0; bool editor = false; string error = null;
            try
            {
                editor = _plugin.OpenEditor(_body.Handle, out width, out height);
                if (editor) ClientSize = new Size(Math.Max(420, Math.Min(1800, width)), Math.Max(142, Math.Min(1142, height + _presets.Height)));
            }
            catch (Exception ex) { error = ex.Message; ErrorLog.Write("Plug-in editor", ex); }
            if (!editor)
            {
                if (!_plugin.Healthy) { ShowFailure(_plugin.Failure ?? error ?? "The plug-in worker is unavailable."); return; }
                try { ShowParameters(_plugin.Parameters()); }
                catch (Exception ex) { ShowFailure(ex.Message); return; }
            }
            _presets.CaptureReference(); _timer.Start();
        }
        private void ShowFailure(string message)
        {
            _presets.Enabled = false;
            _body.Controls.Add(new Label { Text = UiText.Translate("Plug-in editor unavailable") + Environment.NewLine + message,
                Dock = DockStyle.Top, Height = 90, Padding = new Padding(16), ForeColor = CyberTheme.Text });
        }
        private void ShowParameters(List<NativePlugin.Parameter> parameters)
        {
            var rows = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(12) };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            foreach (var parameter in parameters)
            {
                NativePlugin.Parameter captured = parameter; int row = rows.RowCount++; rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                var name = new Label { Text = parameter.Name, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
                var slider = new TrackBar { Minimum = 0, Maximum = 1000, Value = (int)Math.Max(0, Math.Min(1000, Math.Round(parameter.Value * 1000))), TickStyle = TickStyle.None, Dock = DockStyle.Fill };
                var value = new Label { Text = PlainValue(parameter, parameter.Value), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
                slider.ValueChanged += delegate
                {
                    try
                    {
                        double normalized = slider.Value / 1000.0;
                        if (captured.Steps > 0) normalized = Math.Round(normalized * captured.Steps) / captured.Steps;
                        _plugin.SetParameter(captured.Id, normalized); value.Text = PlainValue(captured, normalized);
                    }
                    catch (Exception ex) { value.Text = UiText.Translate("Error"); ErrorLog.Write("Plug-in parameter", ex); }
                };
                rows.Controls.Add(name, 0, row); rows.Controls.Add(slider, 1, row); rows.Controls.Add(value, 2, row);
            }
            _body.AutoScroll = true; _body.Controls.Add(rows);
            if (rows.RowCount == 0) _body.Controls.Add(new Label { Text = UiText.Translate("This plugin exposes no editor or parameters."), Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter });
        }
        private static string PlainValue(NativePlugin.Parameter parameter, double normalized)
        {
            return (parameter.Minimum + normalized * (parameter.Maximum - parameter.Minimum)).ToString("0.###");
        }
    }
}
