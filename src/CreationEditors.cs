using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
namespace SlopStudio
{
 internal static class EditorInk
 {
  public static readonly Color Ground=Color.FromArgb(39,40,41),Grid=Color.FromArgb(64,65,66),Text=Color.FromArgb(222,222,222),Blue=Color.FromArgb(65,137,206);
  public static void TextAt(Graphics g,string value,Font font,Color color,Rectangle rect){StudioDraw.Label(g,value,font,rect,color,TextFormatFlags.VerticalCenter);}
 }
 public sealed class LauncherPanel:ScaledCanvas
 {
  private ProjectDocument _project;private ArrangementEngine _engine;private int _row;public int CellWidth=88,RowHeight=54,HeaderWidth=205,HeaderHeight=38;public event Action<Guid,Guid?> LaunchRequested;public event Action<Guid,int,Guid?> AssignRequested;public event Action<Guid> TrackSelected;public event Action<Guid,string> TrackCommandRequested;public event Action ReturnRequested;
  public LauncherPanel(){DoubleBuffered=true;Dock=DockStyle.Fill;BackColor=CyberTheme.Background;Font=new Font("Segoe UI",8.5f);MouseWheel+=delegate(object s,MouseEventArgs e){_row=Math.Max(0,Math.Min(Math.Max(0,(_project==null?0:_project.Tracks.Count)-1),_row-e.Delta/120));Invalidate();};}
  public void SetContent(ProjectDocument project,ArrangementEngine engine){_project=project;_engine=engine;Invalidate();}
  private LauncherCell Cell(Guid track,int scene){return _project.Studio.Cells.Find(c=>c.TrackId==track&&c.Scene==scene);}
  protected override void OnPaint(PaintEventArgs e)
  {
   PreparePaint(e); base.OnPaint(e); var g=e.Graphics; g.Clear(CyberTheme.Background);
   using(var b=new SolidBrush(CyberTheme.Surface))g.FillRectangle(b,0,0,Width,HeaderHeight);
   using(var b=new SolidBrush(CyberTheme.Elevated))g.FillRectangle(b,0,HeaderHeight,HeaderWidth,Math.Max(0,Height-HeaderHeight));
   using(var p=new Pen(CyberTheme.Border)) {g.DrawLine(p,0,HeaderHeight-1,Width,HeaderHeight-1);g.DrawLine(p,HeaderWidth-1,0,HeaderWidth-1,Height);}
   StudioDraw.Label(g,"SLOP LAUNCHER",Font,new Rectangle(12,0,HeaderWidth-20,HeaderHeight),CyberTheme.Text,TextFormatFlags.VerticalCenter);
   using(var b=new SolidBrush(CyberTheme.Control))g.FillRectangle(b,Width-100,4,96,HeaderHeight-8);
   using(var p=new Pen(CyberTheme.AccentDark))g.DrawRectangle(p,Width-100,4,95,HeaderHeight-9);
   StudioDraw.Label(g,"Arrangement ↗",Font,new Rectangle(Width-98,4,94,HeaderHeight-8),CyberTheme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
   if(_project==null)return;
   int scenes=Math.Min(8,Math.Max(1,(Width-HeaderWidth)/CellWidth));
   for(int scene=0;scene<scenes;scene++){
    int x=HeaderWidth+scene*CellWidth;using(var p=new Pen(CyberTheme.Border))g.DrawLine(p,x,4,x,HeaderHeight-5);
    StudioDraw.Label(g,"▶  "+(scene+1).ToString("00"),Font,new Rectangle(x+4,0,CellWidth-8,HeaderHeight),CyberTheme.Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
   }
   for(int row=_row;row<_project.Tracks.Count;row++){
    int y=HeaderHeight+(row-_row)*RowHeight;if(y>Height)break;Track track=_project.Tracks[row];Color color=StudioDraw.TrackColor(track,track.ColorIndex,_project.Studio);
    using(var p=new Pen(CyberTheme.Border))g.DrawLine(p,0,y+RowHeight-1,Width,y+RowHeight-1);
    using(var b=new SolidBrush(color))g.FillRectangle(b,0,y,4,RowHeight-1);
    StudioDraw.Label(g,(row+1)+"  "+track.Name,Font,new Rectangle(12,y+3,HeaderWidth-18,21),_project.Studio.TextColor(track.Id,false,CyberTheme.Text),TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    StudioDraw.Label(g,track.Type==TrackType.Audio?"Audio":"Instrument",Font,new Rectangle(12,y+27,HeaderWidth-105,21),CyberTheme.Secondary,TextFormatFlags.VerticalCenter);
    for(int button=0;button<3;button++){
     bool active=button==0?track.Muted:button==1?track.Solo:track.Armed;Rectangle hit=new Rectangle(HeaderWidth-82+button*24,y+29,21,18);
     using(var b=new SolidBrush(active?(button==2?CyberTheme.Recording:CyberTheme.AccentDark):CyberTheme.Control))g.FillRectangle(b,hit);
     using(var p=new Pen(CyberTheme.Border))g.DrawRectangle(p,hit);
     StudioDraw.Label(g,new[]{"M","S","R"}[button],Font,hit,CyberTheme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
    }
    if(_engine!=null){float peak=Math.Min(1,_engine.TrackPeak(track.Id));using(var b=new SolidBrush(CyberTheme.Accent))g.FillRectangle(b,12,y+49,(HeaderWidth-105)*peak,2);}
    for(int scene=0;scene<scenes;scene++){
     Rectangle rect=new Rectangle(HeaderWidth+scene*CellWidth+2,y+2,CellWidth-5,RowHeight-5);var cell=Cell(track.Id,scene);Region clip=cell==null?null:track.Regions.Find(x=>x.Id==cell.RegionId);
     Color background=clip==null?CyberTheme.Surface:_project.Studio.Background(clip.Id,true,Color.FromArgb(255,color.R/3,color.G/3,color.B/3));
     using(var b=new SolidBrush(background))g.FillRectangle(b,rect);using(var p=new Pen(clip==null?CyberTheme.Border:color))g.DrawRectangle(p,rect);
     if(clip==null)continue;using(var b=new SolidBrush(color))g.FillRectangle(b,rect.X+1,rect.Y+1,3,rect.Height-2);
     StudioDraw.Label(g,clip.Name,Font,new Rectangle(rect.X+7,rect.Y+1,rect.Width-11,20),_project.Studio.TextColor(clip.Id,true,_project.Studio.TextColor(track.Id,false,CyberTheme.Text)),TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
     using(var p=new Pen(Color.FromArgb(190,color))){
      if(track.Type==TrackType.Midi){foreach(var note in clip.Notes){float x=rect.X+6+(float)(note.StartBeat/clip.LengthBeats)*(rect.Width-11), yy=rect.Y+29+(72-note.Pitch)*.35f;g.DrawLine(p,x,yy,x+Math.Max(2,(float)(note.LengthBeats/clip.LengthBeats)*(rect.Width-11)),yy);}}
      else {var peaks=WaveformCache.Get(clip.MediaPath,this);if(peaks!=null&&peaks.Peaks.Length>0)for(int px=0;px<rect.Width-12;px++){int index=Math.Min(peaks.Peaks.Length-1,(int)(px/(double)Math.Max(1,rect.Width-12)*peaks.Peaks.Length));float h=peaks.Peaks[index]*12;g.DrawLine(p,rect.X+7+px,rect.Y+34-h,rect.X+7+px,rect.Y+34+h);}}
     }
     bool active=_engine!=null&&_engine.IsLaunched(track.Id,clip.Id),pending=_engine!=null&&_engine.IsLaunchPending(track.Id,clip.Id);
     if(active||pending){using(var p=new Pen(active?CyberTheme.Accent:Color.Gold,2))g.DrawRectangle(p,Rectangle.Inflate(rect,-1,-1));if(active){double progress=(_engine.PositionBeat%clip.LengthBeats)/clip.LengthBeats;using(var b=new SolidBrush(CyberTheme.Accent))g.FillRectangle(b,rect.X,rect.Bottom-3,(float)(rect.Width*progress),2);}}
    }
   }
  }
  protected override void OnMouseDown(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseDown(e);Focus();if(_project==null)return;if(e.Y<38){if(e.X>Width-100){if(ReturnRequested!=null)ReturnRequested();return;}int scene=(e.X-HeaderWidth)/CellWidth;if(e.X>=HeaderWidth)foreach(var t in _project.Tracks){var c=Cell(t.Id,scene);if(c!=null&&LaunchRequested!=null)LaunchRequested(t.Id,c.RegionId);}return;}int row=_row+(e.Y-38)/RowHeight;if(row>=_project.Tracks.Count)return;var track=_project.Tracks[row];if(TrackSelected!=null)TrackSelected(track.Id);if(e.X<HeaderWidth){int button=(e.X-(HeaderWidth-82))/24;if(e.X>=HeaderWidth-82&&(e.Y-38)%RowHeight>=29&&button>=0&&button<3&&TrackCommandRequested!=null)TrackCommandRequested(track.Id,new[]{"Mute","Solo","Arm"}[button]);return;}int col=(e.X-HeaderWidth)/CellWidth;if(col>=8)return;var cell=Cell(track.Id,col);if(e.Button==MouseButtons.Right){var menu=new ContextMenuStrip();foreach(var region in track.Regions){Region item=region;menu.Items.Add(region.Name,null,delegate{if(AssignRequested!=null)AssignRequested(track.Id,col,item.Id);});}menu.Items.Add("Clear cell",null,delegate{if(AssignRequested!=null)AssignRequested(track.Id,col,null);});menu.Items.Add("Stop track",null,delegate{if(LaunchRequested!=null)LaunchRequested(track.Id,null);});menu.Show(this,e.Location);}else if((ModifierKeys&Keys.Shift)!=0){if(AssignRequested!=null)AssignRequested(track.Id,col,null);}else if(cell!=null&&LaunchRequested!=null)LaunchRequested(track.Id,cell.RegionId);}
 }
 public sealed class StepSequencerEditor:ScaledCanvas
 {
  private Track _track;private Region _region;private int _steps=16;private readonly int[] _keys={42,46,38,40,36,48,50,60};public event Action<List<MidiNote>> NotesChanged;
  public StepSequencerEditor(){DoubleBuffered=true;Dock=DockStyle.Fill;BackColor=EditorInk.Ground;Font=new Font("Segoe UI",8.5f);}
  public void SetContent(Track t,Region r){_track=t;_region=r;Invalidate();}
  protected override void OnPaint(PaintEventArgs e){PreparePaint(e);base.OnPaint(e);var g=e.Graphics;g.Clear(BackColor);EditorInk.TextAt(g,"Step Sequencer   ·   1/16 note   ·   wheel: velocity   ·   right-click: clear",Font,EditorInk.Text,new Rectangle(12,2,Width-20,28));if(_track==null||_track.Type!=TrackType.Midi||_region==null){EditorInk.TextAt(g,"Select a MIDI region to edit its actual notes.",Font,Color.Silver,new Rectangle(12,40,Width-24,30));return;}_steps=Math.Min(64,Math.Max(16,(int)Math.Ceiling(_region.LengthBeats*4)));int cell=Math.Max(8,(Width-100)/_steps),h=Math.Max(22,Math.Min(42,(Height-42)/8));for(int row=0;row<8;row++){int key=_keys[row];EditorInk.TextAt(g,""+key,Font,Color.Silver,new Rectangle(15,34+row*h,65,h));for(int step=0;step<_steps;step++){MidiNote note=_region.Notes.Find(n=>n.Pitch==key&&Math.Abs(n.StartBeat-step*.25)<.01);Rectangle r=new Rectangle(90+step*cell,36+row*h,cell-3,h-4);using(var b=new SolidBrush(note==null?(step/4%2==0?Color.FromArgb(65,66,67):Color.FromArgb(53,54,55)):Color.FromArgb(40+note.Velocity,96+note.Velocity,57+note.Velocity)))g.FillRectangle(b,r);if(row==0&&step%4==0)EditorInk.TextAt(g,""+(step/4+1),Font,Color.Black,r);}}}
  private bool Hit(Point p,out int row,out int step){int cell=Math.Max(8,(Width-100)/_steps),h=Math.Max(22,Math.Min(42,(Height-42)/8));row=(p.Y-36)/h;step=(p.X-90)/cell;return _region!=null&&_track!=null&&_track.Type==TrackType.Midi&&p.X>=90&&p.Y>=36&&row>=0&&row<8&&step>=0&&step<_steps&&step*.25<_region.LengthBeats;}
  protected override void OnMouseDown(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseDown(e);Focus();int row,step;if(!Hit(e.Location,out row,out step))return;var notes=ProjectFactory.CloneNotes(_region.Notes);int key=_keys[row];var existing=notes.Find(n=>n.Pitch==key&&Math.Abs(n.StartBeat-step*.25)<.01);if(existing!=null)notes.Remove(existing);else if(e.Button==MouseButtons.Left)notes.Add(new MidiNote{Pitch=key,StartBeat=step*.25,LengthBeats=Math.Min(.2,_region.LengthBeats-step*.25),Velocity=100});if(NotesChanged!=null)NotesChanged(notes);}
  protected override void OnMouseWheel(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseWheel(e);int row,step;if(!Hit(e.Location,out row,out step))return;var notes=ProjectFactory.CloneNotes(_region.Notes);var n=notes.Find(x=>x.Pitch==_keys[row]&&Math.Abs(x.StartBeat-step*.25)<.01);if(n!=null){n.Velocity=Math.Max(1,Math.Min(127,n.Velocity+e.Delta/120*4));if(NotesChanged!=null)NotesChanged(notes);}}
 }
 public sealed class ScoreEditor:ScaledCanvas
 {
  private Track _track;private Region _region;private int _numerator=4;private Guid? _selection;public event Action<List<MidiNote>> NotesChanged;
  public ScoreEditor(){DoubleBuffered=true;Dock=DockStyle.Fill;BackColor=Color.FromArgb(236,234,222);Font=new Font("Segoe UI",9);}
  public void SetContent(Track track,Region region,int numerator){_track=track;_region=region;_numerator=numerator;Invalidate();}
  private static int Diatonic(int midi){int[] positions={0,0,1,1,2,3,3,4,4,5,5,6};return midi/12*7+positions[midi%12];}
  private static int Midi(int step){int[] semitones={0,2,4,5,7,9,11};int octave=(int)Math.Floor(step/7.0),degree=step-octave*7;return Math.Max(0,Math.Min(127,octave*12+semitones[degree]));}
  private double ScaleBeat{get{return _region==null?60:Math.Max(24,(Width-120)/Math.Max(8,_region.LengthBeats));}}
  private int StaffBottom{get{return Math.Min(180,Math.Max(115,Height/2));}}
  protected override void OnPaint(PaintEventArgs e){PreparePaint(e);base.OnPaint(e);var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;EditorInk.TextAt(g,"Score   ·   click: quarter note   ·   right-click: delete   ·   arrows: pitch/time   ·   Shift+arrows: duration",Font,Color.FromArgb(70,67,62),new Rectangle(12,0,Width-24,30));int bottom=StaffBottom;using(var pen=new Pen(Color.FromArgb(65,62,56))){for(int line=0;line<5;line++)g.DrawLine(pen,45,bottom-line*10,Width-20,bottom-line*10);g.DrawLine(pen,45,bottom-40,45,bottom);for(double beat=0;_region!=null&&beat<=_region.LengthBeats;beat+=_numerator){float x=100+(float)(beat*ScaleBeat);g.DrawLine(pen,x,bottom-40,x,bottom);EditorInk.TextAt(g,""+((int)(beat/_numerator)+1),Font,Color.DimGray,new Rectangle((int)x,45,40,22));}}
   using(var clef=new Font("Segoe UI Symbol",42.67f,FontStyle.Regular,GraphicsUnit.Pixel))g.DrawString(UiText.Translate("𝄞"),clef,Brushes.Black,48,bottom-60);
   if(_track==null||_track.Type!=TrackType.Midi||_region==null){EditorInk.TextAt(g,"Select a MIDI region.",Font,Color.DimGray,new Rectangle(100,bottom+30,Width-120,30));return;}
   foreach(var n in _region.Notes){float x=100+(float)(n.StartBeat*ScaleBeat),y=bottom-(Diatonic(n.Pitch)-Diatonic(64))*5;Color color=_selection==n.Id?EditorInk.Blue:Color.FromArgb(27,25,22);using(var b=new SolidBrush(color))g.FillEllipse(b,x-5,y-3,11,7);using(var pen=new Pen(color,1.2f)){if(n.LengthBeats<4){bool down=n.Pitch>=71;g.DrawLine(pen,x+(down?-5:5),y,x+(down?-5:5),y+(down?31:-31));if(n.LengthBeats<1)g.DrawBezier(pen,x+5,y-31,x+19,y-23,x+16,y-15,x+10,y-13);}for(float line=bottom+10;line<=y+2;line+=10)g.DrawLine(pen,x-9,line,x+10,line);for(float line=bottom-50;line>=y-2;line-=10)g.DrawLine(pen,x-9,line,x+10,line);}if(new[]{1,3,6,8,10}.Contains(n.Pitch%12))g.DrawString(UiText.Translate("♯"),Font,Brushes.Black,x-19,y-9);}
  }
  protected override void OnMouseDown(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseDown(e);Focus();if(_region==null||_track==null||_track.Type!=TrackType.Midi||e.X<90||e.Y<40)return;var notes=ProjectFactory.CloneNotes(_region.Notes);double beat=Math.Max(0,Math.Round((e.X-100)/ScaleBeat*4)/4);if(beat>=_region.LengthBeats)return;int pitch=Midi(Diatonic(64)+(int)Math.Round((StaffBottom-e.Y)/5.0));var n=notes.Find(x=>Math.Abs(x.StartBeat-beat)<.15&&Math.Abs(x.Pitch-pitch)<2);if(n!=null){_selection=n.Id;if(e.Button==MouseButtons.Right){notes.Remove(n);if(NotesChanged!=null)NotesChanged(notes);}else Invalidate();}else if(e.Button==MouseButtons.Left){n=new MidiNote{Pitch=pitch,StartBeat=beat,LengthBeats=Math.Min(1,_region.LengthBeats-beat)};notes.Add(n);_selection=n.Id;if(NotesChanged!=null)NotesChanged(notes);}}
  protected override bool IsInputKey(Keys key){return (key&Keys.KeyCode)==Keys.Left||(key&Keys.KeyCode)==Keys.Right||(key&Keys.KeyCode)==Keys.Up||(key&Keys.KeyCode)==Keys.Down||base.IsInputKey(key);}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(_region==null||!_selection.HasValue)return;var notes=ProjectFactory.CloneNotes(_region.Notes);var n=notes.Find(x=>x.Id==_selection.Value);if(n==null)return;if(e.KeyCode==Keys.Delete)notes.Remove(n);else if(e.KeyCode==Keys.Up)n.Pitch=Math.Min(127,n.Pitch+1);else if(e.KeyCode==Keys.Down)n.Pitch=Math.Max(0,n.Pitch-1);else if(e.KeyCode==Keys.Right){if(e.Shift)n.LengthBeats=Math.Min(_region.LengthBeats-n.StartBeat,n.LengthBeats+.25);else n.StartBeat=Math.Min(_region.LengthBeats-n.LengthBeats,n.StartBeat+.25);}else if(e.KeyCode==Keys.Left){if(e.Shift)n.LengthBeats=Math.Max(.0625,n.LengthBeats-.25);else n.StartBeat=Math.Max(0,n.StartBeat-.25);}else return;e.Handled=true;if(NotesChanged!=null)NotesChanged(notes);}
 }
 public sealed class AutomationEditor:ScaledCanvas
 {
  private ProjectDocument _project;private Guid? _track;private string _target="Volume";public event Action<StudioState> Changed;public event Action<Point> PluginTargetRequested;public void SetTarget(string target){_target=target;Invalidate();}
  public AutomationEditor(){DoubleBuffered=true;Dock=DockStyle.Fill;BackColor=EditorInk.Ground;Font=new Font("Segoe UI",8.5f);}
  public void SetContent(ProjectDocument p,Guid? track){_project=p;_track=track;Invalidate();}
  private double End{get{if(_project==null)return 16;return Math.Max(16,_project.Tracks.SelectMany(t=>t.Regions).Select(r=>r.StartBeat+r.LengthBeats).DefaultIfEmpty(16).Max());}}
  private double Min{get{return _target=="Volume"?-60:_target=="Pan"?-1:0;}}private double Max{get{return _target=="Volume"?6:1;}}
  private PointF Position(AutomationPoint p){return new PointF(70+(float)(p.Beat/End*(Width-95)),40+(float)((Max-p.Value)/(Max-Min)*(Height-68)));}
  protected override void OnPaint(PaintEventArgs e){PreparePaint(e);base.OnPaint(e);var g=e.Graphics;g.Clear(BackColor);EditorInk.TextAt(g,"Automation   ·   click/drag: write point   ·   right-click: delete",Font,EditorInk.Text,new Rectangle(10,0,Width-310,29));for(int i=0;i<4;i++){string name=new[]{"Volume","Pan","Mute","Plug-in…"}[i];Rectangle rect=new Rectangle(Width-328+i*80,3,76,23);using(var b=new SolidBrush((_target==name||(i==3&&_target.StartsWith("Plugin:")))?EditorInk.Blue:Color.FromArgb(72,73,74)))g.FillRectangle(b,rect);EditorInk.TextAt(g,name,Font,EditorInk.Text,rect);}if(_project==null||!_track.HasValue)return;using(var pen=new Pen(EditorInk.Grid)){for(int b=0;b<=End;b+=4){float x=70+(float)(b/End*(Width-95));g.DrawLine(pen,x,38,x,Height-25);EditorInk.TextAt(g,""+(b/4+1),Font,Color.Silver,new Rectangle((int)x,Height-24,35,20));}for(int i=0;i<=6;i++){float y=40+i*(Height-68)/6f;g.DrawLine(pen,70,y,Width-25,y);EditorInk.TextAt(g,(Max-i*(Max-Min)/6).ToString("0.#"),Font,Color.Silver,new Rectangle(4,(int)y-10,60,20));}}
   var lane=_project.Studio.Automation.Find(a=>a.TrackId==_track.Value&&a.Target==_target);if(lane==null)return;using(var pen=new Pen(Color.FromArgb(221,177,66),2))using(var brush=new SolidBrush(Color.FromArgb(250,208,94))){PointF? previous=null;foreach(var p in lane.Points){PointF at=Position(p);if(previous.HasValue)g.DrawLine(pen,previous.Value,at);g.FillEllipse(brush,at.X-3,at.Y-3,6,6);previous=at;}}
  }
  private void Edit(MouseEventArgs e){if(_project==null||!_track.HasValue||e.X<70||e.Y<35||e.X>Width-25||e.Y>Height-25)return;var state=_project.Studio.Copy();var lane=state.Automation.Find(a=>a.TrackId==_track.Value&&a.Target==_target);if(lane==null){lane=new AutomationLane{TrackId=_track.Value,Target=_target};state.Automation.Add(lane);}double beat=Math.Max(0,Math.Min(End,Math.Round((e.X-70)/(double)(Width-95)*End*4)/4));var existing=lane.Points.Find(p=>Math.Abs(p.Beat-beat)<.12);if(existing!=null)lane.Points.Remove(existing);if(e.Button!=MouseButtons.Right)lane.Points.Add(new AutomationPoint{Beat=beat,Value=Math.Max(Min,Math.Min(Max,Max-(e.Y-40)/(double)(Height-68)*(Max-Min)))});lane.Points.Sort((a,b)=>a.Beat.CompareTo(b.Beat));if(Changed!=null)Changed(state);}
  protected override void OnMouseDown(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseDown(e);Focus();if(e.Y<30&&e.X>=Width-328){int i=(e.X-(Width-328))/80;if(i<3){_target=new[]{"Volume","Pan","Mute"}[i];Invalidate();}else if(PluginTargetRequested!=null)PluginTargetRequested(new Point((int)(e.X*CanvasScale),(int)(e.Y*CanvasScale)));return;}Edit(e);}
  protected override void OnMouseMove(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseMove(e);if(e.Button==MouseButtons.Left)Edit(e);}
 }
 public sealed class CompEditor:ScaledCanvas
 {
  private ProjectDocument _project;private Track _track;private double _start;private int _take=-1;public event Action<StudioState> Changed;
  public CompEditor(){Dock=DockStyle.Fill;DoubleBuffered=true;BackColor=EditorInk.Ground;Font=new Font("Segoe UI",8.5f);}
  public void SetContent(ProjectDocument p,Track t){_project=p;_track=t;Invalidate();}
  private double End{get{return _track==null?16:Math.Max(4,_track.Regions.Select(r=>r.StartBeat+r.LengthBeats).DefaultIfEmpty(16).Max());}}
  protected override void OnPaint(PaintEventArgs e){PreparePaint(e);base.OnPaint(e);var g=e.Graphics;g.Clear(BackColor);EditorInk.TextAt(g,"Comping   ·   drag a range in an audio or MIDI take   ·   right-click: clear comp",Font,EditorInk.Text,new Rectangle(12,0,Width-24,28));if(_track==null){EditorInk.TextAt(g,"Select an audio or MIDI track with recorded takes.",Font,Color.Silver,new Rectangle(12,40,Width-24,30));return;}var comp=_project.Studio.Comps.Find(c=>c.TrackId==_track.Id);int h=Math.Max(24,Math.Min(54,(Height-40)/Math.Max(1,_track.Regions.Count)));for(int row=0;row<_track.Regions.Count;row++){var r=_track.Regions[row];int y=35+row*h;EditorInk.TextAt(g,r.Name,Font,EditorInk.Text,new Rectangle(8,y,160,h));Rectangle rect=new Rectangle(175+(int)(r.StartBeat/End*(Width-195)),y+2,(int)(r.LengthBeats/End*(Width-195)),h-4);using(var b=new SolidBrush(Color.FromArgb(53,78,98)))g.FillRectangle(b,rect);if(comp!=null)foreach(var s in comp.Slices)if(s.RegionId==r.Id)using(var b=new SolidBrush(Color.FromArgb(64,157,99)))g.FillRectangle(b,175+(float)(s.StartBeat/End*(Width-195)),y+2,(float)((s.EndBeat-s.StartBeat)/End*(Width-195)),h-4);}}
  protected override void OnMouseDown(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseDown(e);if(_track==null||_track.Regions.Count==0)return;if(e.Button==MouseButtons.Right){var state=_project.Studio.Copy();state.Comps.RemoveAll(c=>c.TrackId==_track.Id);if(Changed!=null)Changed(state);return;}int h=Math.Max(24,Math.Min(54,(Height-40)/Math.Max(1,_track.Regions.Count)));_take=(e.Y-35)/h;if(e.X<175||e.Y<35||_take>=_track.Regions.Count){_take=-1;return;}_start=Math.Max(0,(e.X-175)/(double)(Width-195)*End);Capture=true;}
  protected override void OnMouseUp(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseUp(e);Capture=false;if(_take<0||_track==null||_take>=_track.Regions.Count)return;double end=Math.Max(0,(e.X-175)/(double)(Width-195)*End);var r=_track.Regions[_take];double a=Math.Max(r.StartBeat,Math.Min(_start,end)),b=Math.Min(r.StartBeat+r.LengthBeats,Math.Max(_start,end));_take=-1;if(b-a<.0625)return;var state=_project.Studio.Copy();var lane=state.Comps.Find(c=>c.TrackId==_track.Id);if(lane==null){lane=new CompLane{TrackId=_track.Id,Takes=_track.Regions.Select(x=>x.Id).ToList()};state.Comps.Add(lane);}var rebuilt=new List<CompSlice>();foreach(var s in lane.Slices){if(s.EndBeat<=a||s.StartBeat>=b)rebuilt.Add(s);else{if(s.StartBeat<a)rebuilt.Add(new CompSlice{RegionId=s.RegionId,StartBeat=s.StartBeat,EndBeat=a});if(s.EndBeat>b)rebuilt.Add(new CompSlice{RegionId=s.RegionId,StartBeat=b,EndBeat=s.EndBeat});}}rebuilt.Add(new CompSlice{RegionId=r.Id,StartBeat=a,EndBeat=b});lane.Slices=rebuilt.OrderBy(s=>s.StartBeat).ToList();if(Changed!=null)Changed(state);}
 }
}
