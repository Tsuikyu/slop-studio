using System.Drawing;
using System.Drawing.Drawing2D;
namespace SlopProX {
 public static class BrandMark {
  public static void Draw(Graphics g,Rectangle bounds){var state=g.Save();g.TranslateTransform(bounds.X,bounds.Y);g.ScaleTransform(bounds.Width/256f,bounds.Height/256f);g.SmoothingMode=SmoothingMode.AntiAlias;
   using(var background=StudioDraw.Rounded(new Rectangle(4,4,248,248),52))using(var brush=new SolidBrush(Color.FromArgb(23,28,38)))g.FillPath(brush,background);
   using(var head=new GraphicsPath()){head.AddPolygon(new[]{new Point(68,104),new Point(61,39),new Point(109,76),new Point(147,76),new Point(195,39),new Point(188,104),new Point(188,158),new Point(175,184),new Point(148,196),new Point(108,196),new Point(81,184),new Point(68,158)});using(var fill=new LinearGradientBrush(new Rectangle(55,35,146,170),Color.FromArgb(177,221,247),Color.FromArgb(63,122,171),90))g.FillPath(fill,head);}
   using(var pen=new Pen(Color.FromArgb(116,186,224),12)){g.DrawArc(pen,42,62,172,155,192,156);}
   using(var brush=new SolidBrush(Color.FromArgb(108,175,216))){g.FillRectangle(brush,39,113,22,54);g.FillRectangle(brush,195,113,22,54);g.FillRectangle(brush,112,197,32,15);}
   using(var path=StudioDraw.Rounded(new Rectangle(88,212,80,13),6))using(var brush=new SolidBrush(Color.FromArgb(79,132,175)))g.FillPath(brush,path);
   g.Restore(state);
  }
 }
}
