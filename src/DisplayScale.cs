using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace SlopStudio
{
 public static class DisplayScale
 {
  public static float User=1;public static bool TouchMode;
  [DllImport("user32.dll")]private static extern uint GetDpiForWindow(IntPtr window);
  public static float For(Control control){float dpi=1;try{if(control.IsHandleCreated)dpi=GetDpiForWindow(control.Handle)/96f;}catch{}return Math.Max(.5f,Math.Min(5,((control.FindForm() is MainForm)?User:1)*dpi));}
  public static int Pixels(Control c,float logical){return (int)Math.Round(logical*For(c));}
 }
 public class ScaledCanvas:Control
 {
  public ScaledCanvas(){ImeMode=ImeMode.Disable;}
  public float CanvasScale{get{return DisplayScale.For(this);}}
  public new int Width{get{return Math.Max(1,(int)(base.ClientSize.Width/CanvasScale));}set{base.Width=value;}}
  public new int Height{get{return Math.Max(1,(int)(base.ClientSize.Height/CanvasScale));}set{base.Height=value;}}
  public new Size ClientSize{get{return new Size(Width,Height);}set{base.ClientSize=value;}}
  public new Rectangle ClientRectangle{get{return new Rectangle(0,0,Width,Height);}}
  protected void PreparePaint(PaintEventArgs e){float scale=CanvasScale;e.Graphics.ScaleTransform(scale,scale);e.Graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;}

  [StructLayout(LayoutKind.Sequential)]private struct GestureConfig{public uint Id,Want,Block;}
  [StructLayout(LayoutKind.Sequential)]private struct GestureInfo{public uint Size,Flags,Id;public IntPtr Target;public short X,Y;public uint Instance,Sequence;public ulong Arguments;public uint Extra;}
  [DllImport("user32.dll")]private static extern bool SetGestureConfig(IntPtr window,uint reserved,uint count,[In]GestureConfig[] config,uint size);
  [DllImport("user32.dll")]private static extern bool GetGestureInfo(IntPtr handle,ref GestureInfo info);
  [DllImport("user32.dll")]private static extern bool CloseGestureInfoHandle(IntPtr handle);
  private Point _gesturePoint;private ulong _gestureDistance;
  protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);try{SetGestureConfig(Handle,0,2,new[]{new GestureConfig{Id=3,Want=1},new GestureConfig{Id=4,Want=1,Block=6}},12);}catch{}}
  public bool ApplyNavigationGesture(uint id,Point screen,ulong arguments,bool begin){
   if(begin){_gesturePoint=screen;_gestureDistance=arguments;return true;}
   double factor=id==3&&_gestureDistance>0?arguments/(double)_gestureDistance:1;double dx=(screen.X-_gesturePoint.X)/CanvasScale,dy=(screen.Y-_gesturePoint.Y)/CanvasScale;
   for(Control parent=this;parent!=null;parent=parent.Parent){var timeline=parent as TimelineEditor;if(timeline!=null){if(id==3)timeline.ZoomGesture(screen,factor);else if(id==4)timeline.PanGesture(dx,dy);_gesturePoint=screen;_gestureDistance=arguments;return true;}var piano=parent as PianoRollEditor;if(piano!=null){if(id==3)piano.ZoomGesture(screen,factor);else if(id==4)piano.PanGesture(dx,dy);_gesturePoint=screen;_gestureDistance=arguments;return true;}}return false;
  }
  protected override void WndProc(ref Message message){if(message.Msg==0x119&&DisplayScale.TouchMode){var info=new GestureInfo{Size=(uint)Marshal.SizeOf(typeof(GestureInfo))};if(GetGestureInfo(message.LParam,ref info)&&(info.Id==3||info.Id==4)){bool handled=ApplyNavigationGesture(info.Id,new Point(info.X,info.Y),info.Arguments,(info.Flags&1)!=0);if(handled){CloseGestureInfoHandle(message.LParam);message.Result=IntPtr.Zero;return;}}}base.WndProc(ref message);}

  protected MouseEventArgs LogicalMouse(MouseEventArgs e){float s=CanvasScale;return new MouseEventArgs(e.Button,e.Clicks,(int)Math.Round(e.X/s),(int)Math.Round(e.Y/s),e.Delta);}
 }
}
