using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace SlopProX
{
 public sealed class StudioChoice:ScaledCanvas
 {
  public List<object> Items=new List<object>();private int _index=-1;private ContextMenuStrip _menu;public event EventHandler SelectedIndexChanged;public ComboBoxStyle DropDownStyle{get;set;}public FlatStyle FlatStyle{get;set;}
  public StudioChoice(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Height=21;BackColor=CyberTheme.Control;ForeColor=Color.Gainsboro;Font=new Font("Segoe UI",8);TabStop=true;}
  public int SelectedIndex{get{return _index>=0&&_index<Items.Count?_index:-1;}set{int v=Math.Max(-1,Math.Min(Items.Count-1,value));if(_index==v)return;_index=v;Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
  public object SelectedItem{get{return _index>=0&&_index<Items.Count?Items[_index]:null;}set{SelectedIndex=Items.IndexOf(value);}}
  public override string Text{get{return SelectedItem==null?String.Empty:SelectedItem.ToString();}set{int index=Items.FindIndex(delegate(object item){return item.ToString()==value;});SelectedIndex=index;}}
  protected override void OnPaint(PaintEventArgs e){PreparePaint(e);StudioDraw.Box(e.Graphics,new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1)),Focused?CyberTheme.Selection:BackColor,Color.FromArgb(66,66,66),3);StudioDraw.Label(e.Graphics,Text,Font,new Rectangle(6,0,Math.Max(0,Width-23),Height),ForeColor,TextFormatFlags.VerticalCenter);using(Pen p=new Pen(Color.FromArgb(180,180,180))){int x=Width-11,y=Height/2;e.Graphics.DrawLines(p,new[]{new Point(x-3,y-1),new Point(x,y+2),new Point(x+3,y-1)});}}
  private void Open(){if(_menu!=null&&_menu.Visible)return;if(_menu==null)_menu=new ContextMenuStrip{BackColor=CyberTheme.Elevated,ForeColor=Color.Gainsboro,ShowImageMargin=false};_menu.Items.Clear();for(int i=0;i<Items.Count;i++){int index=i;var item=new ToolStripMenuItem(UiText.Translate(Items[i].ToString())){Checked=i==_index};item.Click+=delegate{if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)delegate{if(!IsDisposed)SelectedIndex=index;});};_menu.Items.Add(item);}_menu.Show(this,new Point(0,base.Height));}
  protected override void Dispose(bool disposing){if(disposing&&_menu!=null){_menu.Dispose();_menu=null;}base.Dispose(disposing);}
  protected override void OnMouseDown(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseDown(e);if(e.Button==MouseButtons.Left){Focus();Open();}}
  protected override bool IsInputKey(Keys keyData){Keys k=keyData&Keys.KeyCode;return k==Keys.Left||k==Keys.Right||k==Keys.Up||k==Keys.Down||base.IsInputKey(keyData);}
  protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter){Open();e.Handled=true;}else if(e.KeyCode==Keys.Down||e.KeyCode==Keys.Right){SelectedIndex++;e.Handled=true;}else if(e.KeyCode==Keys.Up||e.KeyCode==Keys.Left){SelectedIndex--;e.Handled=true;}base.OnKeyDown(e);}
 }
 public sealed class ProjectMediaBrowser:UserControl
 {
  private ListBox _items;private TextBox _search;private ProjectDocument _project;public event EventHandler<StudioSelectionEventArgs> SelectionRequested;public event EventHandler ImportRequested;
  public ProjectMediaBrowser(){Dock=DockStyle.Fill;BackColor=Color.FromArgb(54,54,54);ForeColor=Color.Gainsboro;Font=new Font("Segoe UI",8);Label title=new Label{Dock=DockStyle.Top,Height=28,Text="Project Audio / Regions",TextAlign=ContentAlignment.MiddleCenter};_search=new TextBox{Dock=DockStyle.Top,BackColor=Color.FromArgb(42,42,42),ForeColor=ForeColor,BorderStyle=BorderStyle.FixedSingle};_items=new ListBox{Dock=DockStyle.Fill,BackColor=Color.FromArgb(48,48,48),ForeColor=ForeColor,BorderStyle=BorderStyle.None,IntegralHeight=false};Button import=new Button{Dock=DockStyle.Bottom,Height=29,Text="Import Audio…",FlatStyle=FlatStyle.Flat};Controls.Add(_items);Controls.Add(_search);Controls.Add(title);Controls.Add(import);import.Click+=delegate{if(ImportRequested!=null)ImportRequested(this,EventArgs.Empty);};_search.TextChanged+=delegate{Populate();};_items.DoubleClick+=delegate{BrowserEntry e=_items.SelectedItem as BrowserEntry;if(e!=null&&SelectionRequested!=null)SelectionRequested(this,new StudioSelectionEventArgs(e.TrackId,e.RegionId));};}
  public void SetProject(ProjectDocument p){_project=p;Populate();}
  private void Populate(){_items.BeginUpdate();_items.Items.Clear();if(_project!=null)foreach(Track t in _project.Tracks)foreach(Region r in t.Regions){string label=r.Name+"  ·  "+t.Name;if(String.IsNullOrEmpty(_search.Text)||label.IndexOf(_search.Text,StringComparison.OrdinalIgnoreCase)>=0)_items.Items.Add(new BrowserEntry{TrackId=t.Id,RegionId=r.Id,Label=label});}_items.EndUpdate();}
 }
}
