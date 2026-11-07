using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

namespace SlopStudio
{
    // Minimal CyberSquare tokens shared by the main workspace and standard dialogs.
    public static class CyberTheme
    {
        public static Color Background = Color.FromArgb(0, 0, 0);
        public static Color Surface = Color.FromArgb(8, 8, 13);
        public static Color Elevated = Color.FromArgb(16, 16, 24);
        public static Color Control = Color.FromArgb(25, 25, 35);
        public static Color Border = Color.FromArgb(48, 44, 64);
        public static Color Text = Color.White;
        public static Color Secondary = Color.FromArgb(200, 200, 208);
        public static Color Muted = Color.FromArgb(169, 169, 181);
        public static Color Accent = Color.FromArgb(116, 102, 255);
        public static Color AccentDark = Color.FromArgb(98, 81, 217);
        public static Color Selection = Color.FromArgb(30, 26, 53);
        public static Color Recording = Color.FromArgb(194, 45, 56);

        private static readonly Dictionary<string,int> Defaults=new Dictionary<string,int>{
            {"Background",Color.FromArgb(0,0,0).ToArgb()},{"Surface",Color.FromArgb(8,8,13).ToArgb()},{"Elevated",Color.FromArgb(16,16,24).ToArgb()},
            {"Control",Color.FromArgb(25,25,35).ToArgb()},{"Border",Color.FromArgb(48,44,64).ToArgb()},{"Text",Color.White.ToArgb()},
            {"Secondary",Color.FromArgb(200,200,208).ToArgb()},{"Muted",Color.FromArgb(169,169,181).ToArgb()},
            {"Accent",Color.FromArgb(116,102,255).ToArgb()},{"AccentDark",Color.FromArgb(98,81,217).ToArgb()},
            {"Selection",Color.FromArgb(30,26,53).ToArgb()},{"Recording",Color.FromArgb(194,45,56).ToArgb()}};
        public static IDictionary<string,int> DefaultPalette{get{return new Dictionary<string,int>(Defaults);}}
        public static void ValidatePalette(IDictionary<string,int> values){if(values==null||values.Count>Defaults.Count)throw new ArgumentException("Invalid theme palette.");foreach(var entry in values)if(!Defaults.ContainsKey(entry.Key)||Color.FromArgb(entry.Value).A!=255)throw new ArgumentException("Unknown or transparent theme color: "+entry.Key);}
        public static void ApplyPalette(IDictionary<string,int> values)
        {
            ValidatePalette(values);var all=new Dictionary<string,int>(Defaults);foreach(var entry in values)all[entry.Key]=entry.Value;
            Background=Color.FromArgb(all["Background"]);Surface=Color.FromArgb(all["Surface"]);Elevated=Color.FromArgb(all["Elevated"]);Control=Color.FromArgb(all["Control"]);Border=Color.FromArgb(all["Border"]);Text=Color.FromArgb(all["Text"]);Secondary=Color.FromArgb(all["Secondary"]);Muted=Color.FromArgb(all["Muted"]);Accent=Color.FromArgb(all["Accent"]);AccentDark=Color.FromArgb(all["AccentDark"]);Selection=Color.FromArgb(all["Selection"]);Recording=Color.FromArgb(all["Recording"]);
        }

        private static double Linear(int c)
        {
            double v = c / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        public static Color TextOn(Color color)
        {
            double light = 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
            return 1.05 / (light + 0.05) >= 4.5 ? Color.White : Color.Black;
        }

        private static bool Neutral(Color c)
        {
            return Math.Abs(c.R - c.G) < 14 && Math.Abs(c.G - c.B) < 14;
        }

        public static void ApplyControl(Control control)
        {
            if (control == null || control is ScaledCanvas) return;
            if (control is Form) { control.BackColor = Background; control.ForeColor = Text; }
            else if (control is Button)
            {
                if (Neutral(control.BackColor)) control.BackColor = Control;
                if (Neutral(control.ForeColor)) control.ForeColor = Text;
                Button button = (Button)control;
                if (button.FlatStyle == FlatStyle.Flat) { button.FlatAppearance.BorderColor = Border; button.FlatAppearance.MouseOverBackColor = Selection; }
            }
            else if (control is TextBoxBase || control is NumericUpDown || control is ComboBox || control is ListBox || control is ListView)
            {
                if (Neutral(control.BackColor)) control.BackColor = Elevated;
                if (Neutral(control.ForeColor)) control.ForeColor = Text;
            }
            else if (control is Label || control is CheckBox || control is RadioButton || control is GroupBox)
            {
                if (Neutral(control.ForeColor) && control.ForeColor.R < 120) control.ForeColor = Text;
            }
            else if (control is Panel || control is TabPage || control is SplitContainer)
            {
                if (Neutral(control.BackColor)) control.BackColor = Surface;
            }
            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                grid.BackgroundColor = Surface; grid.GridColor = Border;
                grid.DefaultCellStyle.BackColor = Elevated; grid.DefaultCellStyle.ForeColor = Text;
                grid.DefaultCellStyle.SelectionBackColor = AccentDark; grid.DefaultCellStyle.SelectionForeColor = Text;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Control; grid.ColumnHeadersDefaultCellStyle.ForeColor = Text;
                grid.EnableHeadersVisualStyles = false;
            }
        }
    }
}
