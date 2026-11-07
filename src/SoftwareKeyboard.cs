using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace SlopProX {
 public sealed class SoftwareKeyboard:Form,IMessageFilter {
  private Action<int,int,bool> _note;private HashSet<int> _held=new HashSet<int>();private Dictionary<Keys,int> _keys=new Dictionary<Keys,int>();private KeyboardCanvas _canvas;private Form _owner;private int _octave=4,_velocity=96;
  private readonly Keys[] _map={Keys.A,Keys.W,Keys.S,Keys.E,Keys.D,Keys.F,Keys.T,Keys.G,Keys.Y,Keys.H,Keys.U,Keys.J,Keys.K,Keys.O,Keys.L,Keys.P,Keys.OemSemicolon};
  public SoftwareKeyboard(Form owner,Action<int,int,bool> note){_owner=owner;_note=note;Text="Musical Typing";ClientSize=new Size(650,220);MinimumSize=new Size(480,200);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(35,37,41);ForeColor=Color.Gainsboro;ShowInTaskbar=false;KeyPreview=true;_canvas=new KeyboardCanvas(this){Dock=DockStyle.Fill};var label=new Label{Text="A W S E D F T G Y H U J K  ·  Z/X octave  ·  C/V velocity",Dock=DockStyle.Bottom,Height=32,TextAlign=ContentAlignment.MiddleCenter};Controls.Add(_canvas);Controls.Add(label);Application.AddMessageFilter(this);FormClosed+=delegate{ReleaseAll();Application.RemoveMessageFilter(this);};Deactivate+=delegate{ReleaseAll();};owner.Deactivate+=OwnerDeactivate;}
  private void OwnerDeactivate(object s,EventArgs e){if(Form.ActiveForm!=this)ReleaseAll();}
  public int HeldCount{get{return _held.Count;}}
  public void ReleaseAll(){foreach(int note in new List<int>(_held))SetNote(note,false);_keys.Clear();}
  private void SetNote(int key,bool down){if(down){if(!_held.Add(key))return;}else if(!_held.Remove(key))return;_note(key,_velocity,down);_canvas.Invalidate();}
  public bool PreFilterMessage(ref Message message){
   if(IsDisposed||!Visible||(Form.ActiveForm!=_owner&&Form.ActiveForm!=this))return false;
   bool down=message.Msg==0x100,up=message.Msg==0x101;if(!down&&!up)return false;Keys key=(Keys)message.WParam.ToInt32();
   if(up){int note;if(_keys.TryGetValue(key,out note)){_keys.Remove(key);SetNote(note,false);return true;}return false;}
   if(Control.ModifierKeys!=Keys.None)return false;
   var main=_owner as MainForm;if(Form.ActiveForm==_owner&&main!=null&&main.TextEntryFocused)return false;
   int index=Array.IndexOf(_map,key);if(index>=0){if(!_keys.ContainsKey(key)){int n=Math.Min(127,(_octave+1)*12+index);_keys[key]=n;SetNote(n,true);}return true;}
   if(key==Keys.Z||key==Keys.X){ReleaseAll();_octave=Math.Max(0,Math.Min(8,_octave+(key==Keys.X?1:-1)));_canvas.Invalidate();return true;}
   if(key==Keys.C||key==Keys.V){_velocity=Math.Max(1,Math.Min(127,_velocity+(key==Keys.V?8:-8)));_canvas.Invalidate();return true;}
   return false;
  }
  protected override bool ProcessCmdKey(ref Message message,Keys keyData){if(keyData==(Keys.Control|Keys.K)){if((message.LParam.ToInt64()&(1L<<30))==0)Close();return true;}var main=_owner as MainForm;if(main!=null&&main.ProcessMusicalTypingShortcut(ref message,keyData))return true;return base.ProcessCmdKey(ref message,keyData);}
  protected override void Dispose(bool disposing){if(disposing){ReleaseAll();Application.RemoveMessageFilter(this);if(_owner!=null)_owner.Deactivate-=OwnerDeactivate;}_owner=null;base.Dispose(disposing);}
  private sealed class KeyboardCanvas:Control {
   private SoftwareKeyboard _form;private int _mouse=-1;private Dictionary<int,Rectangle> _rects=new Dictionary<int,Rectangle>();private List<int> _black=new List<int>();
   public KeyboardCanvas(SoftwareKeyboard form){_form=form;ImeMode=ImeMode.Disable;DoubleBuffered=true;BackColor=Color.FromArgb(35,37,41);}
   protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);_rects.Clear();_black.Clear();int[] whites={0,2,4,5,7,9,11};float w=Width/15f;int baseNote=(_form._octave+1)*12;
    for(int i=0;i<15;i++){int note=baseNote+(i/7)*12+whites[i%7];if(note>127)break;var r=new Rectangle((int)(i*w)+1,33,Math.Max(1,(int)w-2),Math.Max(1,Height-35));_rects[note]=r;using(var b=new SolidBrush(_form._held.Contains(note)?Color.FromArgb(79,170,222):Color.FromArgb(218,222,226)))e.Graphics.FillRectangle(b,r);if(note%12==0)StudioDraw.Label(e.Graphics,"C"+(note/12-1),Font,new Rectangle(r.X,r.Bottom-22,r.Width,20),Color.FromArgb(60,64,69),TextFormatFlags.HorizontalCenter);}
    for(int i=0;i<14;i++){int pc=whites[i%7];if(pc==4||pc==11)continue;int note=baseNote+(i/7)*12+pc+1;if(note>127)continue;var r=new Rectangle((int)((i+1)*w-w*.29),33,Math.Max(1,(int)(w*.58)),Math.Max(1,(int)((Height-35)*.62)));_rects[note]=r;_black.Add(note);using(var b=new SolidBrush(_form._held.Contains(note)?Color.FromArgb(45,131,195):Color.FromArgb(32,34,39)))e.Graphics.FillRectangle(b,r);}
    StudioDraw.Label(e.Graphics,UiText.Translate("Octave")+" "+_form._octave+"    "+UiText.Translate("Velocity")+" "+_form._velocity,Font,new Rectangle(8,3,Width-16,26),Color.Gainsboro,TextFormatFlags.VerticalCenter);
   }
   private int Hit(Point p){foreach(int note in _black)if(_rects[note].Contains(p))return note;foreach(var pair in _rects)if(!_black.Contains(pair.Key)&&pair.Value.Contains(p))return pair.Key;return -1;}
   protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Capture=true;_mouse=Hit(e.Location);if(_mouse>=0)_form.SetNote(_mouse,true);}
   protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!Capture)return;int n=Hit(e.Location);if(n!=_mouse){if(_mouse>=0)_form.SetNote(_mouse,false);_mouse=n;if(n>=0)_form.SetNote(n,true);}}
   protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(_mouse>=0)_form.SetNote(_mouse,false);_mouse=-1;Capture=false;}
  }
 }
}