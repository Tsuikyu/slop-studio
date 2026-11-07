using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SlopStudio
{
    public sealed partial class MainForm
    {
        private sealed class RoutingChoice
        {
            public Guid Id;
            public string Name;
            public override string ToString() { return Name; }
        }

        private void ShowAuxRouting(Track selected)
        {
            if (selected == null) return;
            Guid sourceId = selected.Id;
            using (var dialog = new Form { Text = UiText.Translate("Aux Sends") + " — " + selected.Name,
                ClientSize = new Size(580, 330), MinimumSize = new Size(480, 300),
                StartPosition = FormStartPosition.CenterParent, BackColor = Color.FromArgb(45, 45, 47),
                ForeColor = Color.Gainsboro })
            {
                var sends = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(34, 34, 36), ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.None };
                var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 86, Padding = new Padding(8), AutoScroll = true };
                var buses = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
                var level = new NumericUpDown { Minimum = -96, Maximum = 12, DecimalPlaces = 1, Increment = 1, Value = -12, Width = 70 };
                var create = new Button { Text = UiText.Translate("New Aux"), AutoSize = true };
                var add = new Button { Text = UiText.Translate("Add / Update Send"), AutoSize = true };
                var remove = new Button { Text = UiText.Translate("Remove Send"), AutoSize = true };
                footer.Controls.Add(buses); footer.Controls.Add(level);
                footer.Controls.Add(new Label { Text = "dB", AutoSize = true, Margin = new Padding(2, 8, 8, 0) });
                footer.Controls.Add(create); footer.Controls.Add(add); footer.Controls.Add(remove);
                dialog.Controls.Add(sends); dialog.Controls.Add(footer);
                Action refresh = delegate
                {
                    buses.Items.Clear(); sends.Items.Clear();
                    foreach (Track t in _project.Tracks)
                        if (t.Id != sourceId && (_project.Studio.Buses.Contains(t.Id) || _project.Studio.Stacks.Any(g => g.Summing && g.ParentId == t.Id)))
                            buses.Items.Add(new RoutingChoice { Id = t.Id, Name = t.Name });
                    if (buses.Items.Count > 0) buses.SelectedIndex = 0;
                    foreach (var send in _project.Studio.Sends.Where(x => x.SourceId == sourceId))
                    {
                        var dest = _project.Tracks.Find(t => t.Id == send.TargetId);
                        if (dest != null) sends.Items.Add(new RoutingChoice { Id = dest.Id, Name = dest.Name + "  ·  " + send.GainDb.ToString("0.0") + " dB" });
                    }
                };
                create.Click += delegate
                {
                    try
                    {
                        _engine.CapturePluginStates();
                        var next = ProjectFactory.Clone(_project);
                        var bus = ProjectFactory.CreateTrack("Aux " + (next.Studio.Buses.Count + 1), TrackType.Audio);
                        next.Tracks.Add(bus); next.Studio.Buses.Add(bus.Id);
                        _history.Execute(_project, new SetProjectContentCommand("Create Aux", _project, next));
                        RefreshState(); refresh();
                        for (int i = 0; i < buses.Items.Count; i++) if (((RoutingChoice)buses.Items[i]).Id == bus.Id) buses.SelectedIndex = i;
                    }
                    catch (Exception ex) { MessageBox.Show(dialog, ex.Message, UiText.Translate("Aux Sends"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                };
                add.Click += delegate
                {
                    if (buses.SelectedItem == null) return;
                    try
                    {
                        Guid target = ((RoutingChoice)buses.SelectedItem).Id;
                        var state = _project.Studio.Copy();
                        state.Sends.RemoveAll(x => x.SourceId == sourceId && x.TargetId == target);
                        state.Sends.Add(new AuxSend { SourceId = sourceId, TargetId = target, GainDb = (double)level.Value });
                        CommitStudio("Set aux send", state); refresh();
                    }
                    catch (Exception ex) { MessageBox.Show(dialog, ex.Message, UiText.Translate("Aux Sends"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                };
                remove.Click += delegate
                {
                    if (sends.SelectedItem == null) return;
                    Guid target = ((RoutingChoice)sends.SelectedItem).Id;
                    var state = _project.Studio.Copy();
                    state.Sends.RemoveAll(x => x.SourceId == sourceId && x.TargetId == target);
                    CommitStudio("Remove aux send", state); refresh();
                };
                sends.SelectedIndexChanged += delegate
                {
                    var choice = sends.SelectedItem as RoutingChoice;
                    if (choice == null) return;
                    var send = _project.Studio.Sends.Find(x => x.SourceId == sourceId && x.TargetId == choice.Id);
                    if (send != null) level.Value = (decimal)send.GainDb;
                    for (int i = 0; i < buses.Items.Count; i++) if (((RoutingChoice)buses.Items[i]).Id == choice.Id) buses.SelectedIndex = i;
                };
                refresh(); UiText.Apply(dialog); dialog.ShowDialog(this);
            }
        }
    }
}
