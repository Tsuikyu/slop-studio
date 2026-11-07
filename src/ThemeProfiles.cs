using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SlopStudio
{
    public static class ThemeProfileStore
    {
        public static Dictionary<string,int> Load(string path)
        {
            var info=new FileInfo(path);if(!info.Exists||info.Length>16384)throw new InvalidDataException("Theme profile is missing or too large.");
            var serializer=new JavaScriptSerializer{MaxJsonLength=16384};var root=serializer.DeserializeObject(File.ReadAllText(path,new UTF8Encoding(false,true))) as IDictionary<string,object>;
            if(root==null||!root.ContainsKey("schema")||Convert.ToString(root["schema"])!="slopstudio-theme"||!root.ContainsKey("version")||Convert.ToInt32(root["version"])!=1||!root.ContainsKey("colors"))throw new InvalidDataException("Unsupported theme profile.");
            var input=root["colors"] as IDictionary<string,object>;if(input==null)throw new InvalidDataException("Invalid theme colors.");var result=new Dictionary<string,int>();foreach(var entry in input){if(entry.Value is bool||!(entry.Value is IConvertible))throw new InvalidDataException("Theme colors must be integers.");double n=Convert.ToDouble(entry.Value,System.Globalization.CultureInfo.InvariantCulture);if(n!=Math.Floor(n)||n<Int32.MinValue||n>Int32.MaxValue)throw new InvalidDataException("Theme colors must be integers.");result.Add(entry.Key,(int)n);}CyberTheme.ValidatePalette(result);return result;
        }
        public static void Save(string path,IDictionary<string,int> colors)
        {
            CyberTheme.ValidatePalette(colors);string full=Path.GetFullPath(path),temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";byte[] bytes=new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(new{schema="slopstudio-theme",version=1,colors=colors}));
            try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
    public sealed class ThemeProfilesForm:Form
    {
        private Dictionary<string,int> _working;private DataGridView _grid;
        public Dictionary<string,int> Result{get;private set;}
        public ThemeProfilesForm(IDictionary<string,int> initial)
        {
            _working=new Dictionary<string,int>(initial);Text=UiText.Translate("Theme Profiles");ClientSize=new Size(800,540);MinimumSize=new Size(600,400);StartPosition=FormStartPosition.CenterParent;BackColor=CyberTheme.Surface;ForeColor=CyberTheme.Text;
            _grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=CyberTheme.Surface,GridColor=CyberTheme.Border};_grid.EnableHeadersVisualStyles=false;_grid.DefaultCellStyle=new DataGridViewCellStyle{BackColor=CyberTheme.Elevated,ForeColor=CyberTheme.Text,SelectionBackColor=CyberTheme.AccentDark,SelectionForeColor=CyberTheme.Text};_grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=CyberTheme.Control,ForeColor=CyberTheme.Text};_grid.Columns.Add("name",UiText.Translate("Color"));_grid.Columns.Add("value",UiText.Translate("Preview"));
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=45,FlowDirection=FlowDirection.LeftToRight,WrapContents=false};Add(buttons,"Choose Color…",Choose);Add(buttons,"Import…",Import);Add(buttons,"Save Profile…",Save);Add(buttons,"Restore Defaults",delegate{_working.Clear();RefreshGrid();});Add(buttons,"Use Profile",delegate{Result=new Dictionary<string,int>(_working);DialogResult=DialogResult.OK;Close();});Add(buttons,"Cancel",delegate{DialogResult=DialogResult.Cancel;Close();});
            var note=new Label{Dock=DockStyle.Top,Height=48,Text=UiText.Translate("Theme colors apply after restarting. Some older plug-in windows retain their own colors."),TextAlign=ContentAlignment.MiddleLeft};Controls.Add(_grid);Controls.Add(note);Controls.Add(buttons);RefreshGrid();
        }
        private void Add(Control parent,string text,Action action){var b=new Button{Text=UiText.Translate(text),AutoSize=true,Height=32,FlatStyle=FlatStyle.Flat,ForeColor=CyberTheme.Text,BackColor=CyberTheme.Control};b.Click+=delegate{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,UiText.Translate("Theme Profiles"),MessageBoxButtons.OK,MessageBoxIcon.Warning);}};parent.Controls.Add(b);}
        private void RefreshGrid(){_grid.Rows.Clear();foreach(var entry in CyberTheme.DefaultPalette){int color;if(!_working.TryGetValue(entry.Key,out color))color=entry.Value;int row=_grid.Rows.Add(UiText.Translate(entry.Key),Color.FromArgb(color).Name);_grid.Rows[row].Tag=entry.Key;var cell=_grid.Rows[row].Cells[1];cell.Style.BackColor=Color.FromArgb(color);cell.Style.ForeColor=CyberTheme.TextOn(Color.FromArgb(color));}}
        private void Choose(){if(_grid.SelectedRows.Count==0)return;string key=(string)_grid.SelectedRows[0].Tag;int color;if(!_working.TryGetValue(key,out color))color=CyberTheme.DefaultPalette[key];using(var dialog=new ColorDialog{FullOpen=true,Color=Color.FromArgb(color)})if(dialog.ShowDialog(this)==DialogResult.OK){_working[key]=dialog.Color.ToArgb();RefreshGrid();}}
        private void Import(){using(var d=new OpenFileDialog{Filter="Slop Studio theme profile|*.sloptheme",CheckFileExists=true})if(d.ShowDialog(this)==DialogResult.OK){_working=ThemeProfileStore.Load(d.FileName);RefreshGrid();}}
        private void Save(){using(var d=new SaveFileDialog{Filter="Slop Studio theme profile|*.sloptheme",DefaultExt="sloptheme",AddExtension=true,OverwritePrompt=true})if(d.ShowDialog(this)==DialogResult.OK)ThemeProfileStore.Save(d.FileName,_working);}
    }
}
