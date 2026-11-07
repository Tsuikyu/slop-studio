using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SlopProX
{
    public sealed class StockEffectEditor : Form
    {
        private StockEffectSlot _slot;
        private readonly Action<StockEffectSlot> _change;
        private readonly Func<StockEffectProcessor> _processor;
        private readonly Dictionary<string, NumericUpDown> _fields = new Dictionary<string, NumericUpDown>();
        private readonly Dictionary<string, EffectKnob> _knobs = new Dictionary<string, EffectKnob>();
        private EqGraph _curve;
        private EffectDisplay _display;
        private CheckBox _enabled;
        private Label _meters;
        private Timer _timer;
        private bool _syncing;
        private readonly PresetToolbar _presets;

        public StockEffectEditor(StockEffectSlot slot, Action<StockEffectSlot> change, Func<StockEffectProcessor> processor, IList<Track> tracks = null)
        {
            _slot = slot.Copy(); _change = change; _processor = processor;
            bool eq = slot.Kind == StockEffects.Names[0];
            Text = "Slop " + slot.Kind; ClientSize = new Size(860, eq ? 760 : 590);
            MinimumSize = new Size(640, 440); StartPosition = FormStartPosition.CenterParent;
            BackColor = CyberTheme.Background; ForeColor = CyberTheme.Text; Font = new Font("Segoe UI", 9);
            var header = new Panel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(18, 8, 18, 5), BackColor = CyberTheme.Surface };
            header.Controls.Add(new Label { Text = "SLOP  /  " + UiText.Translate(slot.Kind), Font = new Font("Segoe UI", 17, FontStyle.Bold), Dock = DockStyle.Fill });
            _enabled = new CheckBox { Text = "Enabled", Checked = _slot.Enabled, Dock = DockStyle.Right, Width = 90 };
            _enabled.CheckedChanged += delegate { if (_syncing) return; _slot.Enabled = _enabled.Checked; Commit(); };
            header.Controls.Add(_enabled);

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 8, 18, 8), BackColor = CyberTheme.Background };
            var parameterScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = CyberTheme.Background };
            List<EffectParameter> parameters = StockEffects.Parameters(slot.Kind);
            int columns = eq ? 5 : Math.Min(6, parameters.Count);
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = columns, Padding = new Padding(0, 8, 0, 4) };
            for (int i = 0; i < columns; i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            int index = 0;
            foreach (var parameter in parameters)
            {
                EffectParameter p = parameter; int at = index++;
                int col = eq ? at / 3 : at % columns, row = eq ? at % 3 : at / columns;
                while (table.RowStyles.Count <= row) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
                var group = new Panel { Height = 105, Dock = DockStyle.Fill, Margin = new Padding(4, 3, 4, 4), BackColor = CyberTheme.Control };
                var label = new Label { Text = p.Key == "Mode" ? "Trigger" : p.Key == "Input" ? "Sidechain Input" : p.Name,
                    Dock = DockStyle.Top, Height = 23, TextAlign = ContentAlignment.MiddleCenter, ForeColor = CyberTheme.Secondary };
                var knob = new EffectKnob { Dock = DockStyle.Fill, Parameter = p, Value = _slot.Value(p.Key, p.Default), AccessibleName = p.Name, TabStop = true };
                var number = new NumericUpDown { Minimum = (decimal)p.Min, Maximum = (decimal)p.Max, DecimalPlaces = p.Key == "Mode" || p.Key == "Input" ? 0 : 2,
                    Increment = (decimal)(p.Key.StartsWith("Frequency") ? 10 : .1), Value = (decimal)knob.Value, Dock = DockStyle.Bottom, Height = 24,
                    TextAlign = HorizontalAlignment.Center, BackColor = CyberTheme.Elevated, ForeColor = CyberTheme.Text, BorderStyle = BorderStyle.FixedSingle, AccessibleName = p.Name + " value" };
                _fields[p.Key] = number; _knobs[p.Key] = knob;
                number.ValueChanged += delegate { if (!_syncing) SetParameter(p.Key, (double)number.Value); };
                knob.Changed += delegate(double value) { if (!_syncing) SetParameter(p.Key, value); };
                group.Controls.Add(knob); group.Controls.Add(label); group.Controls.Add(number); table.Controls.Add(group, col, row);
            }
            parameterScroll.Controls.Add(table); body.Controls.Add(parameterScroll);
            if (eq)
            {
                _curve = new EqGraph { Dock = DockStyle.Top, Height = 240, Slot = _slot };
                _curve.BandChanged += delegate(int band, double frequency, double gain)
                {
                    SetValues(new Dictionary<string, double> { { "Frequency" + band, frequency }, { "Gain" + band, gain } });
                };
                body.Controls.Add(_curve);
            }
            else
            {
                _display = new EffectDisplay { Slot = _slot, Processor = _processor, Dock = DockStyle.Top, Height = 210 };
                body.Controls.Add(_display);
            }
            if (slot.Kind == StockEffects.Names[4]) AddSidechain(body, tracks);
            _meters = new Label { Dock = DockStyle.Bottom, Height = 30, TextAlign = ContentAlignment.MiddleCenter };
            _presets = new PresetToolbar("Built-in", slot.Kind,
                () => new JavaScriptSerializer().Serialize(_slot), RestorePreset);
            Controls.Add(body); Controls.Add(_meters); Controls.Add(_presets); Controls.Add(header);
            _timer = new Timer { Interval = 70 };
            _timer.Tick += delegate
            {
                if (_display != null) _display.Invalidate();
                var p = _processor();
                if (p != null) _meters.Text = "IN " + Db(p.InputPeak) + " dB    OUT " + Db(p.OutputPeak) + " dB    GR " + p.Reduction.ToString("0.0") + " dB";
            };
            _timer.Start();
            Shown += delegate { UiText.Apply(this); _presets.CaptureReference(); };
            FormClosed += delegate { _timer.Stop(); _timer.Dispose(); };
        }

        private void AddSidechain(Panel body, IList<Track> tracks)
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40 };
            panel.Controls.Add(new Label { Text = UiText.Translate("Audio sidechain"), AutoSize = true, Margin = new Padding(3, 8, 8, 0) });
            var source = new StudioChoice { Width = 210, Height = 25 };
            source.Items.Add(new KeyChoice { Id = null, Name = UiText.Translate("Audio input") });
            if (tracks != null) foreach (var track in tracks) if (track.Id != _slot.TrackId) source.Items.Add(new KeyChoice { Id = track.Id, Name = track.Name });
            source.SelectedIndex = 0;
            for (int i = 0; i < source.Items.Count; i++) if (((KeyChoice)source.Items[i]).Id == _slot.SidechainTrackId) source.SelectedIndex = i;
            source.SelectedIndexChanged += delegate
            {
                if (_syncing) return;
                Guid? prior = _slot.SidechainTrackId; _slot.SidechainTrackId = ((KeyChoice)source.SelectedItem).Id;
                try { Commit(); } catch (Exception ex) { _slot.SidechainTrackId = prior; MessageBox.Show(this, ex.Message, UiText.Translate("Audio sidechain"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            panel.Controls.Add(source);
            panel.Controls.Add(new Label { Text = UiText.Translate("Set Trigger to Audio (2)"), AutoSize = true, Margin = new Padding(8, 8, 0, 0) });
            body.Controls.Add(panel);
        }

        private void SetParameter(string key, double value) { SetValues(new Dictionary<string, double> { { key, value } }); }
        private void SetValues(Dictionary<string, double> values)
        {
            foreach (var entry in values)
            {
                var number = _fields[entry.Key];
                double bounded = Math.Max((double)number.Minimum, Math.Min((double)number.Maximum, entry.Value));
                _slot.Values[entry.Key] = bounded;
            }
            SyncControls(); Commit();
        }
        private void SyncControls()
        {
            _syncing = true;
            try
            {
                _enabled.Checked = _slot.Enabled;
                foreach (var pair in _fields)
                {
                    EffectKnob knob = _knobs[pair.Key]; double value = _slot.Value(pair.Key, knob.Parameter.Default);
                    pair.Value.Value = Math.Max(pair.Value.Minimum, Math.Min(pair.Value.Maximum, (decimal)value));
                    knob.Value = value; knob.Invalidate();
                }
                if (_curve != null) { _curve.Slot = _slot; _curve.Invalidate(); }
                if (_display != null) { _display.Slot = _slot; _display.Invalidate(); }
            }
            finally { _syncing = false; }
        }
        public void RefreshFrom(StockEffectSlot slot)
        {
            if (slot == null || slot.Id != _slot.Id) return;
            _slot = slot.Copy(); SyncControls();
        }
        private void RestorePreset(string state)
        {
            var parsed = new JavaScriptSerializer().Deserialize<StockEffectSlot>(state);
            if (parsed == null || parsed.Kind != _slot.Kind || parsed.Values == null) throw new InvalidOperationException("Invalid built-in effect preset.");
            var next = _slot.Copy(); next.Values = new Dictionary<string, double>(parsed.Values); next.Enabled = parsed.Enabled;
            StockEffects.Validate(next); _slot = next; SyncControls(); Commit();
        }
        private void Commit()
        {
            _change(_slot.Copy()); if (_curve != null) _curve.Invalidate(); if (_display != null) _display.Invalidate();
        }
        private sealed class KeyChoice { public Guid? Id; public string Name; public override string ToString() { return Name; } }
        private static string Db(float peak) { return peak < .000001 ? "−∞" : (20 * Math.Log10(peak)).ToString("0.0"); }
    }
}
