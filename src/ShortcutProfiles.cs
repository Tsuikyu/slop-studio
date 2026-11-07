using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SlopProX
{
    public static class ShortcutProfileStore
    {
        public static Dictionary<string,int> Load(string path)
        {
            var info=new FileInfo(path);if(!info.Exists||info.Length>65536)throw new InvalidDataException("Shortcut profile is missing or too large.");
            var serializer=new JavaScriptSerializer{MaxJsonLength=65536};
            var root=serializer.DeserializeObject(File.ReadAllText(path,new UTF8Encoding(false,true))) as IDictionary<string,object>;
            if(root==null||!root.ContainsKey("schema")||Convert.ToString(root["schema"])!="slopstudio-shortcuts"||!root.ContainsKey("version")||Convert.ToInt32(root["version"])!=1||!root.ContainsKey("overrides"))throw new InvalidDataException("Unsupported shortcut profile.");
            var input=root["overrides"] as IDictionary<string,object>;if(input==null)throw new InvalidDataException("Invalid shortcut assignments.");
            var result=new Dictionary<string,int>();foreach(var entry in input){if(entry.Value is bool||!(entry.Value is IConvertible))throw new InvalidDataException("Shortcut values must be integers.");double n=Convert.ToDouble(entry.Value,System.Globalization.CultureInfo.InvariantCulture);if(n!=Math.Floor(n)||n<Int32.MinValue||n>Int32.MaxValue)throw new InvalidDataException("Shortcut values must be integers.");result.Add(entry.Key,(int)n);}
            ShortcutRegistry.ValidateOverrides(result);return result;
        }
        public static void Save(string path,IDictionary<string,int> overrides)
        {
            ShortcutRegistry.ValidateOverrides(overrides);string full=Path.GetFullPath(path),temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
            var serializer=new JavaScriptSerializer{MaxJsonLength=65536};byte[] bytes=new UTF8Encoding(false).GetBytes(serializer.Serialize(new {schema="slopstudio-shortcuts",version=1,overrides=overrides}));
            try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(File.Exists(full))File.Replace(temp,full,null);else File.Move(temp,full);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }

    public sealed class ShortcutProfilesForm:Form
    {
        private Dictionary<string,int> _working;private readonly DataGridView _grid;
        public Dictionary<string,int> Result{get;private set;}
        public ShortcutProfilesForm(IDictionary<string,int> initial)
        {
            _working=new Dictionary<string,int>(initial);Text=UiText.Translate("Shortcut Profiles");ClientSize=new Size(820,560);MinimumSize=new Size(600,420);StartPosition=FormStartPosition.CenterParent;BackColor=CyberTheme.Surface;ForeColor=CyberTheme.Text;
            _grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=CyberTheme.Surface,GridColor=CyberTheme.Border};
            _grid.EnableHeadersVisualStyles=false;_grid.DefaultCellStyle=new DataGridViewCellStyle{BackColor=CyberTheme.Elevated,ForeColor=CyberTheme.Text,SelectionBackColor=CyberTheme.AccentDark,SelectionForeColor=CyberTheme.Text};_grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=CyberTheme.Control,ForeColor=CyberTheme.Text};
            _grid.Columns.Add("command",UiText.Translate("Command"));_grid.Columns.Add("current",UiText.Translate("Current Shortcut"));_grid.Columns.Add("default",UiText.Translate("Default Shortcut"));
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=45,FlowDirection=FlowDirection.LeftToRight,WrapContents=false};
            Add(buttons,"Assign…",Assign);Add(buttons,"Import…",Import);Add(buttons,"Save Profile…",Save);Add(buttons,"Restore Defaults",delegate{_working.Clear();RefreshGrid();});
            Add(buttons,"Use Profile",delegate{Result=new Dictionary<string,int>(_working);DialogResult=DialogResult.OK;Close();});Add(buttons,"Cancel",delegate{DialogResult=DialogResult.Cancel;Close();});
            Controls.Add(_grid);Controls.Add(buttons);RefreshGrid();
        }
        private void Add(Control parent,string text,Action action){var b=new Button{Text=UiText.Translate(text),AutoSize=true,Height=32,FlatStyle=FlatStyle.Flat,ForeColor=CyberTheme.Text,BackColor=CyberTheme.Control};b.Click+=delegate{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,UiText.Translate("Shortcut Profiles"),MessageBoxButtons.OK,MessageBoxIcon.Warning);}};parent.Controls.Add(b);}
        private void RefreshGrid(){_grid.Rows.Clear();foreach(var b in ShortcutRegistry.Bindings.Where(x=>x.Implemented)){int raw;Keys current=_working.TryGetValue(b.ProfileId,out raw)?(Keys)raw:b.DefaultWindowsKeys;int row=_grid.Rows.Add(UiText.Translate(b.LogicCommand),ShortcutRegistry.Format(current),ShortcutRegistry.Format(b.DefaultWindowsKeys));_grid.Rows[row].Tag=b;}}
        private void Assign(){if(_grid.SelectedRows.Count==0)return;var binding=(ShortcutBinding)_grid.SelectedRows[0].Tag;using(var capture=new ShortcutCaptureForm(binding.LogicCommand)){if(capture.ShowDialog(this)!=DialogResult.OK)return;var next=new Dictionary<string,int>(_working);if(capture.Key==binding.DefaultWindowsKeys)next.Remove(binding.ProfileId);else next[binding.ProfileId]=(int)capture.Key;ShortcutRegistry.ValidateOverrides(next);_working=next;RefreshGrid();}}
        private void Import(){using(var d=new OpenFileDialog{Filter="Slop Studio shortcut profile|*.slopkeys",CheckFileExists=true})if(d.ShowDialog(this)==DialogResult.OK){_working=ShortcutProfileStore.Load(d.FileName);RefreshGrid();}}
        private void Save(){using(var d=new SaveFileDialog{Filter="Slop Studio shortcut profile|*.slopkeys",DefaultExt="slopkeys",AddExtension=true,OverwritePrompt=true})if(d.ShowDialog(this)==DialogResult.OK)ShortcutProfileStore.Save(d.FileName,_working);}
    }
    internal sealed class ShortcutCaptureForm:Form
    {
        public Keys Key{get;private set;}
        public ShortcutCaptureForm(string name){Text=UiText.Translate("Assign Shortcut");ClientSize=new Size(460,120);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;KeyPreview=true;BackColor=CyberTheme.Surface;ForeColor=CyberTheme.Text;Controls.Add(new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Text=UiText.Translate("Press a key combination; Backspace clears, Escape cancels.")+Environment.NewLine+UiText.Translate(name)});}
        protected override bool ProcessCmdKey(ref Message msg,Keys data){Keys code=data&Keys.KeyCode;if(code==Keys.ShiftKey||code==Keys.ControlKey||code==Keys.Menu)return true;if(data==Keys.Escape){DialogResult=DialogResult.Cancel;Close();return true;}Key=data==Keys.Back?Keys.None:data;DialogResult=DialogResult.OK;Close();return true;}
    }
}
