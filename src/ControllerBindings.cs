using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SlopStudio
{
    public static class GameControllerBindings
    {
        public static readonly string[] ButtonNames = { "A / Button 1", "B / Button 2", "X / Button 3", "Y / Button 4", "LB / Button 5", "RB / Button 6", "Back / Button 7", "Start / Button 8", "D-pad Left", "D-pad Right", "D-pad Up", "D-pad Down", "Left Stick", "Right Stick" };
        public static readonly string[] Commands = { "None", "PlayOrStop", "Stop", "Pause", "Record", "ToggleSoftwareKeyboard", "RewindBar", "ForwardBar", "ToggleMixer", "OpenPreferences", "SelectPreviousTrack", "SelectNextTrack", "GoToBeginning", "ToggleCycle", "Undo", "Redo", "ToggleMute", "ToggleSolo" };
        public static string[] Default() { return new[] { "PlayOrStop", "Stop", "Record", "ToggleSoftwareKeyboard", "RewindBar", "ForwardBar", "ToggleMixer", "OpenPreferences", "RewindBar", "ForwardBar", "SelectPreviousTrack", "SelectNextTrack", "None", "None" }; }
        public static void Validate(string[] bindings)
        {
            if (bindings == null || bindings.Length != ButtonNames.Length) throw new InvalidDataException("Controller bindings have the wrong number of buttons.");
            foreach (string command in bindings) if (Array.IndexOf(Commands, command) < 0) throw new InvalidDataException("Unsupported controller command.");
        }
        public static bool TryCommand(string name, out AppCommand command)
        {
            command = default(AppCommand); if (String.IsNullOrEmpty(name) || name == "None" || Array.IndexOf(Commands, name) < 0) return false;
            return Enum.TryParse<AppCommand>(name, out command);
        }
    }

    public sealed class ControllerBindingForm : Form
    {
        private sealed class Choice { public string Id, Name; public override string ToString() { return Name; } }
        private readonly ComboBox[] _choices = new ComboBox[GameControllerBindings.ButtonNames.Length];
        public string[] Result { get; private set; }
        public ControllerBindingForm(string[] current)
        {
            GameControllerBindings.Validate(current); Text = UiText.Translate("Game Controller Commands"); ClientSize = new Size(580, 600); MinimumSize = new Size(460, 360);
            StartPosition = FormStartPosition.CenterParent; BackColor = CyberTheme.Background; ForeColor = CyberTheme.Text;
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = _choices.Length, Padding = new Padding(12) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
            for (int i = 0; i < _choices.Length; i++)
            {
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                table.Controls.Add(new Label { Text = GameControllerBindings.ButtonNames[i], Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, i);
                var choice = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = CyberTheme.Control, ForeColor = CyberTheme.Text };
                foreach (string id in GameControllerBindings.Commands)
                {
                    AppCommand command; string name = GameControllerBindings.TryCommand(id, out command) ? UiText.CommandName(command) : UiText.Translate("None");
                    int index = choice.Items.Add(new Choice { Id = id, Name = name }); if (id == current[i]) choice.SelectedIndex = index;
                }
                _choices[i] = choice; table.Controls.Add(choice, 1, i);
            }
            scroll.Controls.Add(table);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            var apply = new Button { Text = UiText.Translate("Apply"), AutoSize = true }; var cancel = new Button { Text = UiText.Translate("Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
            apply.Click += delegate { var result = new string[_choices.Length]; for (int i=0;i<result.Length;i++) result[i]=((Choice)_choices[i].SelectedItem).Id; GameControllerBindings.Validate(result); Result=result; DialogResult=DialogResult.OK; Close(); };
            footer.Controls.Add(apply); footer.Controls.Add(cancel); Controls.Add(scroll); Controls.Add(footer); AcceptButton=apply; CancelButton=cancel;
        }
    }
}
