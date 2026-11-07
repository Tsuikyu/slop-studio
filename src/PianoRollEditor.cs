using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace SlopProX
{
    public sealed class MidiNotesChangedEventArgs : EventArgs
    {
        public IList<MidiNote> Notes { get; private set; }
        public MidiNotesChangedEventArgs(IList<MidiNote> notes) { Notes = ProjectFactory.CloneNotes(notes); }
    }

    public sealed class PianoRollEditor : UserControl
    {
        private readonly PianoRollCanvas _canvas;
        private readonly StudioScrollBar _horizontal;
        private readonly StudioScrollBar _vertical;
        private Panel _top;private bool _sizingTop;private float _fontScale;private readonly Label _title;
        private readonly StudioChoice _snap;
        private Track _track;
        private Region _region;
        public event EventHandler<MidiNotesChangedEventArgs> NotesChanged;public event EventHandler<PlayheadRequestEventArgs> PlayheadRequested;public void SetPlayheadBeat(double beat){_canvas.PlayheadLocal=_region==null?(double?)null:beat-_region.StartBeat;_canvas.Invalidate();}
        public int NoteCount { get { return _canvas.NoteCount; } }
        public int SelectionCount{get{return _canvas.SelectionCount;}} public void ZoomHorizontal(int direction){_canvas.PixelsPerBeat=Math.Max(12,Math.Min(384,_canvas.PixelsPerBeat+(direction*8)));UpdateScrollbars();_canvas.Invalidate();}public void ZoomVertical(int direction){_canvas.NoteHeight=Math.Max(6,Math.Min(40,_canvas.NoteHeight+direction*2));UpdateScrollbars();_canvas.Invalidate();}public bool EditCommand(AppCommand command,double absoluteBeat){return _canvas.EditCommand(command,_region==null?0:Math.Max(0,absoluteBeat-_region.StartBeat));}public bool HandleNoteKey(Keys keys){return _canvas.HandleNoteKey(keys); }public void ShowTools(){_canvas.ShowTools();}private int _savedZoom;private int _savedScroll;public void FitSelection(){if(_savedZoom>0){_canvas.PixelsPerBeat=_savedZoom;_savedZoom=0;UpdateScrollbars();_horizontal.Value=_savedScroll;return;}_savedZoom=_canvas.PixelsPerBeat;_savedScroll=_horizontal.Value;double first,last;_canvas.SelectionRange(out first,out last);_canvas.PixelsPerBeat=Math.Max(12,Math.Min(384,(int)((_canvas.Width-_canvas.KeyWidth-20)/Math.Max(.25,last-first))));UpdateScrollbars();_horizontal.Value=(int)(first*4);}

        public PianoRollEditor()
        {
            Dock = DockStyle.Fill; BackColor = Color.FromArgb(40,42,46); ForeColor = Color.Gainsboro;
            Panel top = new Panel { Dock=DockStyle.Top, Height=28, BackColor=Color.FromArgb(49,51,55), Padding=new Padding(10,4,8,3) };
            _top=top;
            _title = new Label { Dock=DockStyle.Left, Width=185, Text="PIANO ROLL", ForeColor=Color.FromArgb(225,228,232), TextAlign=ContentAlignment.MiddleLeft, Font=new Font("Segoe UI",8.0f,FontStyle.Bold) };
            Label snapLabel = new Label { Dock=DockStyle.Left, Width=42, Text="Snap", ForeColor=Color.Silver, TextAlign=ContentAlignment.MiddleRight };
            _snap = new StudioChoice { Dock=DockStyle.Left, Width=82, DropDownStyle=ComboBoxStyle.DropDownList, FlatStyle=FlatStyle.Flat, BackColor=Color.FromArgb(31,33,36), ForeColor=Color.Gainsboro };
            _snap.Items.AddRange(new object[]{"1/4 note","1/8 note","1/16 note"}); _snap.SelectedIndex=2;
            var secondary=new StudioChoice{Dock=DockStyle.Left,Width=126};secondary.Items.AddRange(new object[]{"Ctrl: Marquee","Ctrl: Pencil","Ctrl: Eraser"});secondary.SelectedIndex=0;secondary.SelectedIndexChanged+=delegate{_canvas.SecondaryTool=secondary.SelectedIndex;};var add=new Button{Text="Pencil",Dock=DockStyle.Right,Width=68,FlatStyle=FlatStyle.Flat};add.Click+=delegate{_canvas.Pencil=!_canvas.Pencil;add.BackColor=_canvas.Pencil?Color.FromArgb(55,106,155):top.BackColor;};top.Controls.Add(secondary);top.Controls.Add(_snap);top.Controls.Add(snapLabel);top.Controls.Add(_title);top.Controls.Add(add);
            _canvas = new PianoRollCanvas(); _canvas.Dock=DockStyle.Fill; _canvas.NotesChanged += delegate(object s, MidiNotesChangedEventArgs e) { EventHandler<MidiNotesChangedEventArgs> h=NotesChanged; if(h!=null)h(this,e); };
            _horizontal = new StudioScrollBar { Dock=DockStyle.Fill, Minimum=0, SmallChange=1, LargeChange=4 };
            _vertical = new StudioScrollBar { Vertical=true, Dock=DockStyle.Fill, Minimum=0, Maximum=127, SmallChange=1, LargeChange=12, Value=55 };
            Panel corner = new Panel { Dock=DockStyle.Fill, BackColor=Color.FromArgb(40,42,46) };
            TableLayoutPanel grid = new TableLayoutPanel { Dock=DockStyle.Fill, Margin=Padding.Empty, Padding=Padding.Empty, ColumnCount=2, RowCount=2 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,12));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent,100)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute,12));
            grid.Controls.Add(_canvas,0,0); grid.Controls.Add(_vertical,1,0); grid.Controls.Add(_horizontal,0,1); grid.Controls.Add(corner,1,1);
            Controls.Add(grid); Controls.Add(top);
            _horizontal.Scroll += delegate { UpdateScroll(); }; _vertical.Scroll += delegate { UpdateScroll(); };
            _canvas.SeekRequested+=delegate(double local){if(_region!=null&&PlayheadRequested!=null)PlayheadRequested(this,new PlayheadRequestEventArgs(_region.StartBeat+local));};_canvas.SetScroll(0,72,0.25);
            _snap.SelectedIndexChanged += delegate { _canvas.Snap = SnapBeat; };
            Resize += delegate { ApplyScale();UpdateScrollbars(); };
        }
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);ApplyScale();}
        internal void ApplyScale(){if(_top==null||_title==null||_snap==null||_sizingTop)return;_sizingTop=true;try{float scale=DisplayScale.For(this);_top.Height=(int)Math.Ceiling(32*scale);_top.Padding=new Padding((int)(6*scale),(int)(3*scale),(int)(6*scale),(int)(3*scale));foreach(Control c in _top.Controls){if(c==_title)c.Width=(int)(200*scale);else if(c is Label)c.Width=(int)(42*scale);else if(c is Button)c.Width=(int)(72*scale);else if(c is StudioChoice)c.Width=(int)((c==_snap?86:140)*scale);}float font=DisplayScale.User;if(_fontScale!=font){_fontScale=font;_top.Font=new Font("Segoe UI",8*font);_title.Font=new Font("Segoe UI",8*font,FontStyle.Bold);}}finally{_sizingTop=false;}}
        public void NavigateWheel(Point screen,int delta,Keys modifiers){if((modifiers&Keys.Control)!=0){Point p=_canvas.PointToClient(screen);double x=Math.Max(0,p.X/_canvas.CanvasScale-_canvas.KeyWidth),anchor=_horizontal.Value/4.0+x/_canvas.PixelsPerBeat;_canvas.PixelsPerBeat=Math.Max(12,Math.Min(384,(int)Math.Round(_canvas.PixelsPerBeat*(delta>0?1.16:1/1.16))));UpdateScrollbars();_horizontal.Value=Math.Max(0,(int)Math.Round((anchor-x/_canvas.PixelsPerBeat)*4));_canvas.Invalidate();}else if((modifiers&Keys.Shift)!=0)_horizontal.Value-=(int)Math.Round(delta/120.0*8);else _vertical.Value-=(int)Math.Round(delta/120.0*3);}
        private double _touchPitch;public void PanGesture(double dx,double dy){_horizontal.Value=(int)Math.Round(_horizontal.Value-dx/_canvas.PixelsPerBeat*4);_touchPitch+=dy/_canvas.NoteHeight;int rows=(int)_touchPitch;if(rows!=0){_vertical.Value-=rows;_touchPitch-=rows;}}public void ZoomGesture(Point screen,double factor){if(Double.IsNaN(factor)||factor<=0)return;Point p=_canvas.PointToClient(screen);double x=Math.Max(0,p.X/_canvas.CanvasScale-_canvas.KeyWidth),anchor=_horizontal.Value/4.0+x/_canvas.PixelsPerBeat;_canvas.PixelsPerBeat=Math.Max(12,Math.Min(384,(int)Math.Round(_canvas.PixelsPerBeat*factor)));UpdateScrollbars();_horizontal.Value=(int)Math.Round(Math.Max(0,anchor-x/_canvas.PixelsPerBeat)*4);}
        public void CenterNotes(){if(_region==null||_region.Notes.Count==0)return;PerformLayout();int low=127,high=0;foreach(var note in _region.Notes){low=Math.Min(low,note.Pitch);high=Math.Max(high,note.Pitch);}int rows=Math.Max(4,(_canvas.Height-27)/_canvas.NoteHeight);int top=Math.Max(0,Math.Min(127,(high+low+rows-1)/2));_vertical.Value=127-top;UpdateScroll();}
        private double SnapBeat { get { return _snap.SelectedIndex==0?1.0:_snap.SelectedIndex==1?0.5:0.25; } }
        public void SetMeter(int numerator,int denominator){_canvas.BarBeats=numerator*4.0/denominator;_canvas.Invalidate();}
        public void SetContent(Track track, Region region)
        {
            _track=track; _region=region; _title.Text=track!=null&&region!=null?UiText.Translate("PIANO ROLL")+"  ·  "+region.Name:UiText.Translate("PIANO ROLL  ·  Select a MIDI region");
            _canvas.SetContent(track,region); UpdateScrollbars();
        }
        private void UpdateScrollbars()
        {
            if(_canvas==null)return;
            double length=_region==null?64:_region.LengthBeats; int visible=Math.Max(1,(int)Math.Ceiling(_canvas.VisibleBeats)); int max=Math.Max(16,(int)Math.Ceiling(length)-visible+16);
            max*=4;_horizontal.LargeChange=visible*4; _horizontal.Maximum=max; if(_horizontal.Value>max)_horizontal.Value=max;
            _vertical.Maximum=127; _vertical.LargeChange=12; if(_vertical.Value>127)_vertical.Value=127; UpdateScroll();
        }
        private void UpdateScroll() { _canvas.SetScroll(_horizontal.Value/4.0,127-_vertical.Value,SnapBeat); }
    }

    internal sealed class PianoRollCanvas :ScaledCanvas
    {
        public double? PlayheadLocal;public event Action<double> SeekRequested;private bool _playheadDragging,_velocityDrag;private int _velocityStartY;private List<MidiNote> _velocityBefore;private int VelocityTop{get{return Height>=140?Math.Max(RulerHeight+40,Height-64):Height;}}public int KeyWidth=55;private const int RulerHeight=27;public int NoteHeight=14;public int PixelsPerBeat=48;private bool _keyResize;
        private Track _track; private Region _region; private List<MidiNote> _notes=new List<MidiNote>();
        private readonly HashSet<Guid> _selected=new HashSet<Guid>();private static List<MidiNote> _clipboard=new List<MidiNote>();private List<MidiNote> _before;private bool _marquee,_additive,_dirty,_copyDrag;private Rectangle _selectionBox;private HashSet<Guid> _baseSelection;public bool Pencil;public int SecondaryTool;public int SelectionCount{get{return _selected.Count;}}
        private double _firstBeat;private int _topPitch=84; private double _snap=0.25; private MidiNote _dragNote; private Point _dragStart; private bool _dragging,_resizing;
        public event EventHandler<MidiNotesChangedEventArgs> NotesChanged;
        public int NoteCount { get { return _notes.Count; } }
        public double BarBeats=4;
        public double VisibleBeats { get { return Math.Max(1,(ClientSize.Width-KeyWidth)/(double)PixelsPerBeat); } }
        public double Snap { get { return _snap; } set { _snap=value<=0?0.25:value; Invalidate(); } }
        public PianoRollCanvas()
        { BackColor=Color.FromArgb(34,36,39); ForeColor=Color.Gainsboro; TabStop=true; SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true); Font=new Font("Segoe UI",8.0f); }
        public void SetContent(Track track, Region region) {bool same=_region!=null&&region!=null&&_region.Id==region.Id;_track=track;_region=region;if(!same)_selected.Clear();if(!_dragging&&!_marquee&&!_velocityDrag){_notes=region==null?new List<MidiNote>():ProjectFactory.CloneNotes(region.Notes);_selected.RemoveWhere(id=>!_notes.Exists(n=>n.Id==id));}Invalidate(); }
        public void SetScroll(double beat,int topPitch,double snap) { _firstBeat=Math.Max(0,beat); _topPitch=Math.Max(0,Math.Min(127,topPitch)); Snap=snap; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {PreparePaint(e);
            base.OnPaint(e); Graphics g=e.Graphics; g.Clear(CyberTheme.Background); g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;
            DrawRuler(g); DrawKeys(g); DrawGrid(g); DrawNotes(g);DrawVelocity(g);if(PlayheadLocal.HasValue&&PlayheadLocal.Value>=_firstBeat){int x=KeyWidth+(int)((PlayheadLocal.Value-_firstBeat)*PixelsPerBeat);if(x<Width)using(var pen=new Pen(Color.FromArgb(224,228,233)))g.DrawLine(pen,x,RulerHeight,x,Height);}
            if(_region==null||_track==null||_track.Type!=TrackType.Midi) StudioDraw.Label(g,"Select a MIDI region. Drag to select; Shift-click to add; Alt-drag to copy; double-click to create.",Font,new Rectangle(KeyWidth+16,RulerHeight+10,Math.Max(0,ClientSize.Width-KeyWidth-32),38),Color.FromArgb(164,170,178),TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);
        }
        private void DrawVelocity(Graphics g){
            int top=VelocityTop;if(top>=Height)return;using(var b=new SolidBrush(CyberTheme.Elevated))g.FillRectangle(b,0,top,Width,Height-top);
            using(var p=new Pen(CyberTheme.Border))g.DrawLine(p,0,top,Width,top);
            StudioDraw.Label(g,UiText.Translate("Velocity"),Font,new Rectangle(3,top+2,KeyWidth-6,20),CyberTheme.Secondary,TextFormatFlags.EndEllipsis);
            foreach(var n in _notes){int x=KeyWidth+(int)Math.Round((n.StartBeat-_firstBeat)*PixelsPerBeat);if(x<KeyWidth||x>=Width)continue;int h=(int)Math.Round((Height-top-14)*n.Velocity/127.0);using(var b=new SolidBrush(_selected.Contains(n.Id)?CyberTheme.Accent:Color.FromArgb(72,140,176)))g.FillRectangle(b,x-2,Height-4-h,5,h);}
        }
        private void BeginVelocity(MouseEventArgs e){
            MidiNote closest=null;int distance=8;foreach(var n in _notes){int x=KeyWidth+(int)Math.Round((n.StartBeat-_firstBeat)*PixelsPerBeat);int d=Math.Abs(x-e.X);if(d<distance){closest=n;distance=d;}}
            if(closest==null)return;if(!_selected.Contains(closest.Id)){if((ModifierKeys&Keys.Shift)==0)_selected.Clear();_selected.Add(closest.Id);}
            _velocityBefore=ProjectFactory.CloneNotes(_notes);_velocityStartY=e.Y;_velocityDrag=true;_dirty=false;Capture=true;Invalidate();
        }
        private void DragVelocity(MouseEventArgs e){
            int delta=(int)Math.Round((_velocityStartY-e.Y)*127.0/Math.Max(1,Height-VelocityTop-14));
            _notes=ProjectFactory.CloneNotes(_velocityBefore);foreach(var n in _notes)if(_selected.Contains(n.Id))n.Velocity=Math.Max(1,Math.Min(127,n.Velocity+delta));_dirty=delta!=0;Invalidate();
        }
        private void DrawRuler(Graphics g)
        {
            using(SolidBrush b=new SolidBrush(CyberTheme.Elevated))g.FillRectangle(b,0,0,ClientSize.Width,RulerHeight);
            using(SolidBrush b=new SolidBrush(CyberTheme.Surface))g.FillRectangle(b,0,0,KeyWidth,RulerHeight);
            double origin=_region==null?0:_region.StartBeat;for(int number=(int)Math.Floor((_firstBeat+origin)/BarBeats);number*BarBeats<origin+_firstBeat+VisibleBeats+2;number++){int x=KeyWidth+(int)Math.Round((number*BarBeats-origin-_firstBeat)*PixelsPerBeat);if(x<KeyWidth)continue;using(Pen p=new Pen(Color.FromArgb(122,127,135)))g.DrawLine(p,x,RulerHeight-5,x,ClientSize.Height);StudioDraw.Label(g,(number+1).ToString(CultureInfo.InvariantCulture),Font,new Rectangle(x+4,2,40,20),Color.Gainsboro,TextFormatFlags.VerticalCenter);}
            StudioDraw.Label(g,"BAR",Font,new Rectangle(4,0,KeyWidth-8,RulerHeight),Color.Silver,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
        }
        private void DrawKeys(Graphics g)
        {
            using(SolidBrush paper=new SolidBrush(Color.FromArgb(199,199,199)))g.FillRectangle(paper,0,RulerHeight,KeyWidth,Height-RulerHeight);
            for(int row=0;row<(ClientSize.Height-RulerHeight+NoteHeight-1)/NoteHeight;row++) { int pitch=_topPitch-row;if(pitch<0)break;int y=RulerHeight+row*NoteHeight;bool black=IsBlack(pitch);Rectangle key=new Rectangle(0,y,black?KeyWidth-15:KeyWidth,NoteHeight);if(black){using(SolidBrush b=new SolidBrush(Color.FromArgb(47,47,47)))g.FillRectangle(b,key);using(Pen p=new Pen(Color.FromArgb(81,81,81)))g.DrawLine(p,0,y+1,key.Right,y+1);}else{using(Pen p=new Pen(Color.FromArgb(118,118,118)))g.DrawLine(p,0,y+NoteHeight,KeyWidth,y+NoteHeight);}if(pitch%12==0)StudioDraw.Label(g,PitchName(pitch),Font,new Rectangle(27,y,KeyWidth-28,NoteHeight),Color.FromArgb(55,55,55),TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);}
        }
        private void DrawGrid(Graphics g)
        {
            int bottom=ClientSize.Height; using(SolidBrush b=new SolidBrush(CyberTheme.Background))g.FillRectangle(b,KeyWidth,RulerHeight,ClientSize.Width-KeyWidth,bottom-RulerHeight);
            for(int row=0;row<(bottom-RulerHeight+NoteHeight-1)/NoteHeight;row++) { int pitch=_topPitch-row; if(pitch<0)break; int y=RulerHeight+row*NoteHeight; bool c=pitch%12==0; using(SolidBrush b=new SolidBrush(IsBlack(pitch)?CyberTheme.Background:(c?CyberTheme.Elevated:CyberTheme.Surface)))g.FillRectangle(b,KeyWidth,y,ClientSize.Width-KeyWidth,NoteHeight); using(Pen p=new Pen(CyberTheme.Border))g.DrawLine(p,KeyWidth,y,ClientSize.Width,y); }
            for(int beat=(int)Math.Floor(_firstBeat);beat<_firstBeat+VisibleBeats+2;beat++) { int x=KeyWidth+(int)Math.Round((beat-_firstBeat)*PixelsPerBeat); using(Pen p=new Pen(Math.Abs(beat/BarBeats-Math.Round(beat/BarBeats))<0.0001?CyberTheme.Border:Color.FromArgb(25,25,35),Math.Abs(beat/BarBeats-Math.Round(beat/BarBeats))<0.0001?1.3f:1.0f))g.DrawLine(p,x,RulerHeight,x,bottom); for(int sub=1;sub<4;sub++){int sx=x+sub*PixelsPerBeat/4;using(Pen p=new Pen(Color.FromArgb(16,16,24)))g.DrawLine(p,sx,RulerHeight,sx,bottom);} }
        }
        private Rectangle NoteRect(MidiNote n){return new Rectangle(KeyWidth+(int)Math.Round((n.StartBeat-_firstBeat)*PixelsPerBeat),RulerHeight+(_topPitch-n.Pitch)*NoteHeight+1,Math.Max(7,(int)Math.Round(n.LengthBeats*PixelsPerBeat)-2),Math.Max(3,NoteHeight-2));}
        private void DrawNotes(Graphics g){var old=g.Save();g.SetClip(new Rectangle(KeyWidth,RulerHeight,Math.Max(0,Width-KeyWidth),Math.Max(0,VelocityTop-RulerHeight)));foreach(var n in _notes){var rect=NoteRect(n);if(!rect.IntersectsWith(new Rectangle(KeyWidth,RulerHeight,Width,Height)))continue;bool selected=_selected.Contains(n.Id);using(var b=new SolidBrush(selected?Color.FromArgb(159,213,171):Color.FromArgb(48,158,75)))g.FillRectangle(b,rect);using(var p=new Pen(selected?Color.WhiteSmoke:Color.FromArgb(93,197,112)))g.DrawRectangle(p,rect);if(rect.Width>28&&NoteHeight>=14)StudioDraw.Label(g,PitchName(n.Pitch),Font,rect,selected?Color.FromArgb(24,54,30):Color.FromArgb(212,240,214),TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}if(_marquee){using(var b=new SolidBrush(Color.FromArgb(42,120,180,225)))g.FillRectangle(b,_selectionBox);using(var p=new Pen(Color.FromArgb(155,196,234)))g.DrawRectangle(p,_selectionBox);}g.Restore(old);}
        public bool EditCommand(AppCommand command,double pasteBeat){if(_region==null||_track==null||_track.Type!=TrackType.Midi)return false;
         if(command==AppCommand.InvertSelection){foreach(var n in _notes)if(!_selected.Remove(n.Id))_selected.Add(n.Id);Invalidate();return true;}if(command==AppCommand.SelectAll){foreach(var n in _notes)_selected.Add(n.Id);Invalidate();return true;}if(command==AppCommand.DeselectAll){_selected.Clear();Invalidate();return true;}
         if(command==AppCommand.Copy||command==AppCommand.Cut){var chosen=_notes.FindAll(n=>_selected.Contains(n.Id));if(chosen.Count==0)return true;double first=Double.MaxValue;foreach(var n in chosen)first=Math.Min(first,n.StartBeat);_clipboard=ProjectFactory.CloneNotes(chosen);foreach(var n in _clipboard)n.StartBeat-=first;if(command==AppCommand.Cut){_notes.RemoveAll(n=>_selected.Contains(n.Id));_selected.Clear();RaiseNotesChanged();}Invalidate();return true;}
         if(command==AppCommand.DuplicateRegion){var chosen=_notes.FindAll(n=>_selected.Contains(n.Id));if(chosen.Count==0)return true;double first=Double.MaxValue,last=0;foreach(var n in chosen){first=Math.Min(first,n.StartBeat);last=Math.Max(last,n.StartBeat+n.LengthBeats);}double span=Math.Max(Snap,Math.Ceiling((last-first)/Snap)*Snap);_selected.Clear();foreach(var source in chosen){var copy=ProjectFactory.CloneNotes(new[]{source})[0];copy.Id=Guid.NewGuid();copy.StartBeat+=span;if(copy.StartBeat>=_region.LengthBeats)continue;copy.LengthBeats=Math.Min(copy.LengthBeats,_region.LengthBeats-copy.StartBeat);_notes.Add(copy);_selected.Add(copy.Id);}RaiseNotesChanged();Invalidate();return true;}
         if(command==AppCommand.SplitRegion){var after=new List<MidiNote>();foreach(var n in _notes){double end=n.StartBeat+n.LengthBeats;if((_selected.Count==0||_selected.Contains(n.Id))&&pasteBeat-n.StartBeat>=.0625&&end-pasteBeat>=.0625){var right=ProjectFactory.CloneNotes(new[]{n})[0];right.Id=Guid.NewGuid();right.StartBeat=pasteBeat;right.LengthBeats=end-pasteBeat;n.LengthBeats=pasteBeat-n.StartBeat;after.Add(right);}}_notes.AddRange(after);if(after.Count>0)RaiseNotesChanged();Invalidate();return true;}
         if(command==AppCommand.TransposeUp||command==AppCommand.TransposeDown||command==AppCommand.TransposeOctaveUp||command==AppCommand.TransposeOctaveDown){int delta=command==AppCommand.TransposeUp?1:command==AppCommand.TransposeDown?-1:command==AppCommand.TransposeOctaveUp?12:-12;MoveSelected(0,delta,false);RaiseNotesChanged();Invalidate();return true;}
         if(command==AppCommand.NudgeLeft||command==AppCommand.NudgeRight||command==AppCommand.LengthLeft||command==AppCommand.LengthRight){double delta=(command==AppCommand.NudgeLeft||command==AppCommand.LengthLeft)?-Snap:Snap;bool length=command==AppCommand.LengthLeft||command==AppCommand.LengthRight;if(length){foreach(var n in _notes)if(_selected.Contains(n.Id))n.LengthBeats=Math.Max(.0625,Math.Min(_region.LengthBeats-n.StartBeat,n.LengthBeats+delta));}else MoveSelected(delta,0,false);RaiseNotesChanged();Invalidate();return true;}
         if(command==AppCommand.Paste){if(_clipboard.Count==0)return true;_selected.Clear();foreach(var source in _clipboard){var n=ProjectFactory.CloneNotes(new[]{source})[0];n.Id=Guid.NewGuid();n.StartBeat=SnapValue(pasteBeat)+source.StartBeat;if(n.StartBeat>=_region.LengthBeats)continue;n.LengthBeats=Math.Min(n.LengthBeats,_region.LengthBeats-n.StartBeat);_notes.Add(n);_selected.Add(n.Id);}RaiseNotesChanged();Invalidate();return true;}
         if(command==AppCommand.DeleteRegion){_notes.RemoveAll(n=>_selected.Contains(n.Id));_selected.Clear();RaiseNotesChanged();Invalidate();return true;}
         if(command==AppCommand.Quantize){foreach(var n in _notes)if(_selected.Contains(n.Id)){n.StartBeat=Math.Min(_region.LengthBeats-.0625,Math.Max(0,SnapValue(n.StartBeat)));n.LengthBeats=Math.Min(n.LengthBeats,_region.LengthBeats-n.StartBeat);}RaiseNotesChanged();Invalidate();return true;}return false;
        }
        public bool HandleNoteKey(Keys keys){Keys key=keys&Keys.KeyCode;if((keys&(Keys.Control|Keys.Alt))!=0||_region==null)return false;if(key==Keys.Delete||key==Keys.Back)return EditCommand(AppCommand.DeleteRegion,0);if(key!=Keys.Up&&key!=Keys.Down&&key!=Keys.Left&&key!=Keys.Right)return false;if(_selected.Count==0)return true;int pitch=(key==Keys.Up?1:key==Keys.Down?-1:0)*((keys&Keys.Shift)!=0?12:1);double beat=key==Keys.Left?-Snap:key==Keys.Right?Snap:0;MoveSelected(beat,pitch,false);RaiseNotesChanged();Invalidate();return true;}
        public void SelectionRange(out double first,out double last){first=Double.MaxValue;last=0;foreach(var n in _notes)if(_selected.Count==0||_selected.Contains(n.Id)){first=Math.Min(first,n.StartBeat);last=Math.Max(last,n.StartBeat+n.LengthBeats);}if(first==Double.MaxValue){first=0;last=_region==null?16:_region.LengthBeats;}}
        public void ShowTools(){var menu=new ContextMenuStrip();menu.Items.Add(UiText.Translate("Pointer"),null,delegate{Pencil=false;});menu.Items.Add(UiText.Translate("Pencil"),null,delegate{Pencil=true;});menu.Items.Add(UiText.Translate("Ctrl: Marquee"),null,delegate{SecondaryTool=0;});menu.Items.Add(UiText.Translate("Ctrl: Pencil"),null,delegate{SecondaryTool=1;});menu.Items.Add(UiText.Translate("Ctrl: Eraser"),null,delegate{SecondaryTool=2;});menu.Show(this,new Point(30,25));}
        private void MoveSelected(double beat,int pitch,bool resize){double low=Double.MaxValue,high=0;int min=127,max=0;foreach(var n in _notes)if(_selected.Contains(n.Id)){low=Math.Min(low,n.StartBeat);high=Math.Max(high,n.StartBeat+n.LengthBeats);min=Math.Min(min,n.Pitch);max=Math.Max(max,n.Pitch);}if(low==Double.MaxValue)return;beat=Math.Max(-low,Math.Min(_region.LengthBeats-high,beat));pitch=Math.Max(-min,Math.Min(127-max,pitch));foreach(var n in _notes)if(_selected.Contains(n.Id)){if(resize)n.LengthBeats=Math.Max(.0625,Math.Min(_region.LengthBeats-n.StartBeat,n.LengthBeats+beat));else{n.StartBeat+=beat;n.Pitch+=pitch;}}}
        protected override void OnMouseDown(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseDown(e);Focus();if(e.Button==MouseButtons.Left&&e.Y<VelocityTop&&Math.Abs(e.X-KeyWidth)<8){_keyResize=true;Capture=true;Cursor=Cursors.VSplit;return;}if(_region==null||_track==null||_track.Type!=TrackType.Midi)return;if(e.Button==MouseButtons.Left&&e.Y>=VelocityTop&&e.X>=KeyWidth){BeginVelocity(e);return;}if(e.Button==MouseButtons.Left&&e.Y<RulerHeight&&e.X>KeyWidth){_playheadDragging=true;Capture=true;if(SeekRequested!=null)SeekRequested(Math.Max(0,_firstBeat+(e.X-KeyWidth)/(double)PixelsPerBeat));return;}MidiNote hit=Hit(e.Location);
         if(e.Button==MouseButtons.Right){if(hit!=null&&!_selected.Contains(hit.Id)){_selected.Clear();_selected.Add(hit.Id);}var menu=new ContextMenuStrip();foreach(var cmd in new[]{AppCommand.SelectAll,AppCommand.Copy,AppCommand.Cut,AppCommand.Paste,AppCommand.DeleteRegion,AppCommand.Quantize}){var copy=cmd;menu.Items.Add((cmd==AppCommand.DeleteRegion?UiText.Translate("Delete"):UiText.CommandName(cmd))+"  "+ShortcutRegistry.Display(cmd),null,delegate{EditCommand(copy,Math.Max(0,_firstBeat+(e.X-KeyWidth)/(double)PixelsPerBeat));});}UiText.ApplyItems(menu.Items);menu.Closed+=delegate{if(IsHandleCreated&&!IsDisposed)BeginInvoke((Action)delegate{menu.Dispose();});};menu.Show(this,new Point((int)(e.X*CanvasScale),(int)(e.Y*CanvasScale)));Invalidate();return;}
         if(e.Button!=MouseButtons.Left||e.Y<RulerHeight)return;if(e.X<KeyWidth){int pitch=_topPitch-(e.Y-RulerHeight)/NoteHeight;if((ModifierKeys&Keys.Shift)==0)_selected.Clear();foreach(var n in _notes)if(n.Pitch==pitch)_selected.Add(n.Id);Invalidate();return;}
         bool ctrl=(ModifierKeys&Keys.Control)!=0;_additive=(ModifierKeys&Keys.Shift)!=0;_dragStart=e.Location;
         if(ctrl&&SecondaryTool==2){if(hit!=null){_notes.Remove(hit);_selected.Remove(hit.Id);RaiseNotesChanged();}return;}
         if((Pencil&&!ctrl)||(ctrl&&SecondaryTool==1)){if(hit==null){CreateNote(e);return;}}
         if(hit==null||(ctrl&&SecondaryTool==0)){_baseSelection=_additive?new HashSet<Guid>(_selected):new HashSet<Guid>();if(!_additive)_selected.Clear();_marquee=true;_selectionBox=new Rectangle(e.X,e.Y,0,0);Capture=true;Invalidate();return;}
         if(_additive){if(_selected.Contains(hit.Id)){_selected.Remove(hit.Id);Invalidate();return;}_selected.Add(hit.Id);}else if(!_selected.Contains(hit.Id)){_selected.Clear();_selected.Add(hit.Id);}
         _dragNote=hit;_before=ProjectFactory.CloneNotes(_notes);_dirty=false;_copyDrag=(ModifierKeys&Keys.Alt)!=0;_resizing=e.X>=NoteRect(hit).Right-Math.Min(7,NoteRect(hit).Width/3);_dragging=true;Capture=true;Invalidate();
        }
        private void CreateNote(MouseEventArgs e){double beat=SnapValue(_firstBeat+(e.X-KeyWidth)/(double)PixelsPerBeat);if(beat>=_region.LengthBeats)return;var n=new MidiNote{StartBeat=Math.Max(0,beat),LengthBeats=Math.Min(Snap,_region.LengthBeats-beat),Pitch=Math.Max(0,Math.Min(127,_topPitch-(e.Y-RulerHeight)/NoteHeight))};_notes.Add(n);_selected.Clear();_selected.Add(n.Id);_dragNote=n;_before=ProjectFactory.CloneNotes(_notes);_dirty=true;_copyDrag=false;_dragStart=e.Location;_resizing=true;_dragging=true;Capture=true;Invalidate();}
        protected override void OnMouseDoubleClick(MouseEventArgs e){var logical=LogicalMouse(e);base.OnMouseDoubleClick(e);if(_region!=null&&_track!=null&&_track.Type==TrackType.Midi&&logical.Button==MouseButtons.Left&&logical.X>KeyWidth&&logical.Y>RulerHeight&&logical.Y<VelocityTop&&Hit(logical.Location)==null){_marquee=false;CreateNote(logical);}}
        protected override void OnMouseMove(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseMove(e);if(_velocityDrag){DragVelocity(e);return;}if(_playheadDragging){if(SeekRequested!=null)SeekRequested(Math.Max(0,_firstBeat+(e.X-KeyWidth)/(double)PixelsPerBeat));return;}if(_keyResize){KeyWidth=Math.Max(38,Math.Min(180,e.X));Invalidate();return;}if(_marquee){_selectionBox=Rectangle.FromLTRB(Math.Min(_dragStart.X,e.X),Math.Min(_dragStart.Y,e.Y),Math.Max(_dragStart.X,e.X),Math.Max(_dragStart.Y,e.Y));_selected.Clear();foreach(var id in _baseSelection)_selected.Add(id);foreach(var n in _notes)if(NoteRect(n).IntersectsWith(_selectionBox))_selected.Add(n.Id);Invalidate();return;}
         if(!_dragging){var hit=Hit(e.Location);Cursor=Math.Abs(e.X-KeyWidth)<8?Cursors.VSplit:hit!=null&&e.X>NoteRect(hit).Right-7?Cursors.SizeWE:Pencil?Cursors.Cross:Cursors.Default;return;}
         double beat=SnapValue((e.X-_dragStart.X)/(double)PixelsPerBeat);int pitch=-(e.Y-_dragStart.Y)/NoteHeight;if(Math.Abs(e.X-_dragStart.X)+Math.Abs(e.Y-_dragStart.Y)<3)return;_dirty=true;_notes=ProjectFactory.CloneNotes(_before);if(_copyDrag){var oldSelected=new HashSet<Guid>(_selected);_selected.Clear();foreach(var n in _before)if(oldSelected.Contains(n.Id)){var copy=ProjectFactory.CloneNotes(new[]{n})[0];copy.Id=Guid.NewGuid();_notes.Add(copy);_selected.Add(copy.Id);}_copyDrag=false;_before=ProjectFactory.CloneNotes(_notes);}
         if(_resizing){foreach(var n in _notes)if(_selected.Contains(n.Id))n.LengthBeats=Math.Max(.0625,Math.Min(_region.LengthBeats-n.StartBeat,n.LengthBeats+beat));}else MoveSelected(beat,pitch,false);Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e){e=LogicalMouse(e);base.OnMouseUp(e);if(e.Button!=MouseButtons.Left)return;_playheadDragging=false;_keyResize=false;_marquee=false;Capture=false;if(_velocityDrag){_velocityDrag=false;if(_dirty)RaiseNotesChanged();}_velocityBefore=null;if(_dragging){_dragging=false;if(_dirty)RaiseNotesChanged();}_dirty=false;Invalidate();}
        protected override bool IsInputKey(Keys keyData){Keys key=keyData&Keys.KeyCode;return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||base.IsInputKey(keyData);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(HandleNoteKey(e.KeyData)){e.Handled=true;e.SuppressKeyPress=true;}if(e.KeyCode==Keys.Escape&&_velocityDrag){_notes=ProjectFactory.CloneNotes(_velocityBefore);_velocityDrag=false;_dirty=false;Capture=false;Invalidate();}if(e.KeyCode==Keys.Escape&&_dragging){_notes=ProjectFactory.CloneNotes(_before);_dragging=false;_dirty=false;Capture=false;Invalidate();}}
        private MidiNote Hit(Point p)
        {
            if(p.Y>=VelocityTop)return null;for(int i=_notes.Count-1;i>=0;i--){MidiNote n=_notes[i];int x=KeyWidth+(int)Math.Round((n.StartBeat-_firstBeat)*PixelsPerBeat),y=RulerHeight+(_topPitch-n.Pitch)*NoteHeight,w=Math.Max(8,(int)Math.Round(n.LengthBeats*PixelsPerBeat)-2);if(new Rectangle(x,y,w,NoteHeight-2).Contains(p))return n;}return null;
        }
        private double SnapValue(double value){return Math.Round(value/Math.Max(0.0625,_snap),MidpointRounding.AwayFromZero)*Math.Max(0.0625,_snap);}
        private void RaiseNotesChanged(){EventHandler<MidiNotesChangedEventArgs> h=NotesChanged;if(h!=null)h(this,new MidiNotesChangedEventArgs(_notes));}
        private static bool IsBlack(int pitch){int n=pitch%12;return n==1||n==3||n==6||n==8||n==10;}
        private static string PitchName(int pitch){string[] names={"C","C♯","D","D♯","E","F","F♯","G","G♯","A","A♯","B"};return names[pitch%12]+(pitch/12-1).ToString(CultureInfo.InvariantCulture);}
    }
}
