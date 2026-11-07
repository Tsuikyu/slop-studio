using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace SlopProX {
 public static class UiText {
  public static string Language="en-US";
  private static Dictionary<string,string> _words=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  private sealed class Entry { public string Source,Last,Language;public bool Updating; }
  private static HashSet<Control> _watched=new HashSet<Control>();
  private static Dictionary<Control,Entry> _controls=new Dictionary<Control,Entry>();
  private static Dictionary<object,Entry> _headers=new Dictionary<object,Entry>();private static HashSet<ListBox> _drawnLists=new HashSet<ListBox>();private static HashSet<Form> _darkWindows=new HashSet<Form>();
  [System.Runtime.InteropServices.DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  private static Dictionary<ToolStripItem,Entry> _items=new Dictionary<ToolStripItem,Entry>();
  public const string Disclaimer="This software is a joke. The author and distributors accept no liability arising from its use.";
  public const string Attribution="AI-generated with Codex by OpenAI.";
  public static void Load(string language) {
   Language=language;_words.Clear();
   try {string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"locales");var j=new JavaScriptSerializer{MaxJsonLength=1024*1024};
    var en=(Dictionary<string,object>)j.DeserializeObject(File.ReadAllText(Path.Combine(root,"en-US.json")));
    var loc=(Dictionary<string,object>)j.DeserializeObject(File.ReadAllText(Path.Combine(root,language+".json")));
    var source=(Dictionary<string,object>)en["strings"];var target=(Dictionary<string,object>)loc["strings"];
    foreach(var p in source)if(target.ContainsKey(p.Key))_words[Convert.ToString(p.Value)]=Convert.ToString(target[p.Key]);
    if(loc.ContainsKey("ui"))foreach(var p in (Dictionary<string,object>)loc["ui"])_words[p.Key]=Convert.ToString(p.Value);
   }catch{}
  }
  public static string Translate(string text) {
   if(String.IsNullOrEmpty(text)||Language=="en-US")return text;
   string translated;if(_words.TryGetValue(text,out translated))return translated;
   if(text.StartsWith("PIANO ROLL",StringComparison.Ordinal))return Translate("Piano Roll")+text.Substring(10);
   if(text.StartsWith("Slop ",StringComparison.Ordinal))return "Slop "+Translate(text.Substring(5));
   foreach(string ending in new[]{"…","..."," ▾"," ⌄"})if(text.EndsWith(ending,StringComparison.Ordinal)&&text.Length>ending.Length)return Translate(text.Substring(0,text.Length-ending.Length))+ending;
   var band=System.Text.RegularExpressions.Regex.Match(text,@"^Band ([1-5]) (Frequency|Gain|Q)$");if(band.Success)return Translate("Band")+" "+band.Groups[1].Value+" "+Translate(band.Groups[2].Value);
   int separator=text.IndexOf(" · ",StringComparison.Ordinal);if(separator>0)return Translate(text.Substring(0,separator))+" · "+Translate(text.Substring(separator+3));
   int tab=text.IndexOf('\t');if(tab>0)return Translate(text.Substring(0,tab))+text.Substring(tab);
   foreach(string prefix in new[]{"⌄  Region: ","›  Region: ","⌄  Track: ","Region: ","Track: ","PIANO ROLL  ·  ","Slop Studio — "})
    if(text.StartsWith(prefix,StringComparison.Ordinal))return (_words.TryGetValue(prefix.Trim(),out translated)?translated+" ":prefix)+Translate(text.Substring(prefix.Length));
   return text;
  }
  public static string CommandName(AppCommand command) {
   foreach(var b in ShortcutRegistry.Bindings)if(b.Command==command&&!String.IsNullOrEmpty(b.LogicCommand))return Translate(b.LogicCommand);
   return Translate(System.Text.RegularExpressions.Regex.Replace(command.ToString(),"([a-z])([A-Z])","$1 $2"));
  }
  public static void Apply(Control root) {
   if(root==null||root.IsDisposed)return;CyberTheme.ApplyControl(root);
   if(_watched.Add(root)){root.ControlAdded+=delegate(object sender,ControlEventArgs args){Apply(args.Control);};root.Disposed+=delegate{_watched.Remove(root);};var watchedStrip=root as ToolStrip;if(watchedStrip!=null)watchedStrip.ItemAdded+=delegate(object sender,ToolStripItemEventArgs args){ApplyItem(args.Item);};}
   if(root is Form&&root.IsHandleCreated&&_darkWindows.Add((Form)root)){var window=(Form)root;window.HandleDestroyed+=delegate{_darkWindows.Remove(window);};try{int dark=1,caption=0x202020,text=0xE8E8E8;DwmSetWindowAttribute(root.Handle,20,ref dark,4);DwmSetWindowAttribute(root.Handle,35,ref caption,4);DwmSetWindowAttribute(root.Handle,36,ref text,4);window.Icon=System.Drawing.Icon.ExtractAssociatedIcon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SlopStudio.exe"));}catch{}}
   var list=root as ListBox;if(list!=null&&list.DrawMode==DrawMode.Normal&&_drawnLists.Add(list)){list.DrawMode=DrawMode.OwnerDrawFixed;list.ItemHeight=Math.Max(list.ItemHeight,list.Font.Height+8);list.DrawItem+=delegate(object sender,DrawItemEventArgs e){if(e.Index<0)return;var box=(ListBox)sender;using(var b=new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(54,102,150):box.BackColor))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,Translate(Convert.ToString(box.Items[e.Index])),box.Font,new Rectangle(e.Bounds.X+7,e.Bounds.Y,e.Bounds.Width-10,e.Bounds.Height),box.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};}
   var view=root as ListView;if(view!=null)foreach(ColumnHeader column in view.Columns){Entry entry;if(!_headers.TryGetValue(column,out entry))_headers[column]=entry=new Entry{Source=column.Text};column.Text=Translate(entry.Source);}
   var grid=root as DataGridView;if(grid!=null)foreach(DataGridViewColumn column in grid.Columns){Entry entry;if(!_headers.TryGetValue(column,out entry))_headers[column]=entry=new Entry{Source=column.HeaderText};column.HeaderText=Translate(entry.Source);}
   if(!(root is TextBoxBase)&&!(root is NumericUpDown)&&!(root is StudioChoice)&&!(root is ListBox)){
    Entry e;if(!_controls.TryGetValue(root,out e)){_controls[root]=e=new Entry{Source=root.Text,Last=root.Text};Entry tracked=e;root.TextChanged+=delegate{if(tracked.Updating||root.IsDisposed)return;if(root.Text!=Translate(tracked.Source))tracked.Source=root.Text;tracked.Updating=true;try{string value=Translate(tracked.Source);if(root.Text!=value)root.Text=value;tracked.Last=root.Text;}finally{tracked.Updating=false;}};root.Disposed+=delegate{_controls.Remove(root);};}
    if(root.Text!=e.Last)e.Source=root.Text;
    string t=Translate(e.Source);e.Updating=true;try{if(root.Text!=t)root.Text=t;e.Last=t;}finally{e.Updating=false;}if(e.Language!=Language){e.Language=Language;root.Invalidate(true);}
   }
   var strip=root as ToolStrip;if(strip!=null)foreach(ToolStripItem i in strip.Items)ApplyItem(i);
   var tabs=root as TabControl;if(tabs!=null)foreach(TabPage p in tabs.TabPages)Apply(p);
   foreach(Control child in root.Controls)Apply(child);
   if(_controls.Count>2500){var dead=new List<Control>();foreach(var c in _controls.Keys)if(c.IsDisposed)dead.Add(c);foreach(var c in dead)_controls.Remove(c);}
  }
  public static void ApplyItems(ToolStripItemCollection items){foreach(ToolStripItem item in items)ApplyItem(item);}
  private static void ApplyItem(ToolStripItem item){
   Entry e;if(!_items.TryGetValue(item,out e)){_items[item]=e=new Entry{Source=item.Text,Last=item.Text};Entry tracked=e;item.TextChanged+=delegate{if(tracked.Updating)return;if(item.Text!=Translate(tracked.Source))tracked.Source=item.Text;tracked.Updating=true;try{item.Text=Translate(tracked.Source);tracked.Last=item.Text;}finally{tracked.Updating=false;}};item.Disposed+=delegate{_items.Remove(item);};var dropdown=item as ToolStripDropDownItem;if(dropdown!=null)dropdown.DropDownOpening+=delegate{foreach(ToolStripItem child in dropdown.DropDownItems)ApplyItem(child);};}
   if(item.Text!=e.Last)e.Source=item.Text;e.Updating=true;try{item.Text=Translate(e.Source);e.Last=item.Text;item.ToolTipText=Translate(item.ToolTipText);}finally{e.Updating=false;}
   var menu=item as ToolStripDropDownItem;if(menu!=null)foreach(ToolStripItem child in menu.DropDownItems)ApplyItem(child);
  }
 }
 public static class ErrorLog {
  public static void Write(string context,Exception error){
   try{string root=Path.GetDirectoryName(SettingsStore.DefaultPath);Directory.CreateDirectory(root);string p=Path.Combine(root,"errors.log");if(File.Exists(p)&&new FileInfo(p).Length>1024*1024)File.Move(p,p+"."+DateTime.UtcNow.Ticks);File.AppendAllText(p,DateTime.UtcNow.ToString("O")+" "+context+Environment.NewLine+error+Environment.NewLine);}catch{}
  }
 }
}