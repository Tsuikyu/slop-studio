using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SlopProX
{
    // Original five-band graph: the curve is calculated from the actual filter coefficients.
    public sealed class EqGraph : ScaledCanvas
    {
        private static readonly Color[] BandColors = {
            Color.FromArgb(96, 183, 255), Color.FromArgb(82, 220, 210),
            Color.FromArgb(166, 130, 255), Color.FromArgb(255, 171, 104),
            Color.FromArgb(243, 117, 172)
        };
        private readonly ToolTip _tip = new ToolTip();
        private int _dragBand = -1;
        public StockEffectSlot Slot { get; set; }
        public event Action<int, double, double> BandChanged;

        public EqGraph() { DoubleBuffered = true; BackColor = Color.FromArgb(8, 13, 24); Font = new Font("Segoe UI", 8); }
        private Rectangle Plot { get { return new Rectangle(48, 30, Math.Max(1, Width - 68), Math.Max(1, Height - 58)); } }
        private static double Frequency(double x, Rectangle p) { return 20 * Math.Pow(1000, Math.Max(0, Math.Min(1, (x - p.Left) / p.Width))); }
        private static float X(double hz, Rectangle p) { return p.Left + (float)(Math.Log10(Math.Max(20, Math.Min(20000, hz)) / 20) / 3 * p.Width); }
        private static float Y(double db, Rectangle p) { return p.Top + (float)((24 - Math.Max(-24, Math.Min(24, db))) / 48 * p.Height); }

        protected override void OnPaint(PaintEventArgs e)
        {
            PreparePaint(e); Graphics g = e.Graphics; g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle p = Plot;
            using (var grid = new Pen(Color.FromArgb(29, 41, 57)))
            using (var center = new Pen(Color.FromArgb(73, 93, 116)))
            {
                foreach (int db in new[] { -24, -18, -12, -6, 0, 6, 12, 18, 24 })
                {
                    float y = Y(db, p); g.DrawLine(db == 0 ? center : grid, p.Left, y, p.Right, y);
                    StudioDraw.Label(g, db > 0 ? "+" + db : db.ToString(), Font, new Rectangle(2, (int)y - 7, 39, 15), CyberTheme.Secondary, TextFormatFlags.Right);
                }
                foreach (double hz in new[] { 20d, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 })
                {
                    int x = (int)X(hz, p); g.DrawLine(grid, x, p.Top, x, p.Bottom);
                    string label = hz >= 1000 ? (hz / 1000).ToString("0.#") + "k" : hz.ToString("0");
                    StudioDraw.Label(g, label, Font, new Rectangle(x - 20, p.Bottom + 4, 40, 16), CyberTheme.Secondary, TextFormatFlags.HorizontalCenter);
                }
            }
            StudioDraw.Label(g, "5-BAND PARAMETRIC  ·  DRAG A NODE", Font, new Rectangle(p.Left, 5, p.Width, 19), CyberTheme.Secondary, TextFormatFlags.Left);
            if (Slot == null) return;
            var bands = new EqBand[5];
            for (int i = 0; i < bands.Length; i++)
            {
                bands[i] = new EqBand();
                bands[i].Configure(48000, Slot.Value("Frequency" + i, new[] { 80d, 300, 1000, 4000, 12000 }[i]), Slot.Value("Gain" + i, 0), Slot.Value("Q" + i, .707));
            }
            var points = new List<PointF>();
            for (int x = 0; x <= p.Width; x++)
            {
                double hz = Frequency(p.Left + x, p), gain = 1;
                foreach (var band in bands) gain *= band.Magnitude(hz, 48000);
                points.Add(new PointF(p.Left + x, Y(20 * Math.Log10(Math.Max(1e-6, gain)), p)));
            }
            if (points.Count > 1)
            {
                using (var path = new GraphicsPath())
                {
                    path.AddLines(points.ToArray()); path.AddLine(points[points.Count - 1], new PointF(p.Right, p.Bottom));
                    path.AddLine(new PointF(p.Right, p.Bottom), new PointF(p.Left, p.Bottom)); path.CloseFigure();
                    using (var fill = new SolidBrush(Color.FromArgb(45, 80, 129, 210))) g.FillPath(fill, path);
                }
                using (var line = new Pen(Color.FromArgb(112, 191, 255), 2)) g.DrawLines(line, points.ToArray());
            }
            for (int i = 0; i < 5; i++)
            {
                PointF point = Node(i, p);
                using (var fill = new SolidBrush(BandColors[i])) g.FillEllipse(fill, point.X - 7, point.Y - 7, 14, 14);
                using (var border = new Pen(Color.FromArgb(14, 20, 35), 1.5f)) g.DrawEllipse(border, point.X - 7, point.Y - 7, 14, 14);
                StudioDraw.Label(g, (i + 1).ToString(), Font, new Rectangle((int)point.X - 7, (int)point.Y - 7, 14, 14), Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
        private PointF Node(int index, Rectangle p)
        {
            return new PointF(X(Slot.Value("Frequency" + index, 1000), p), Y(Slot.Value("Gain" + index, 0), p));
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); e = LogicalMouse(e); if (e.Button != MouseButtons.Left || Slot == null) return;
            double best = 18 * 18; _dragBand = -1;
            for (int i = 0; i < 5; i++) { PointF n = Node(i, Plot); double d = (n.X - e.X) * (n.X - e.X) + (n.Y - e.Y) * (n.Y - e.Y); if (d < best) { best = d; _dragBand = i; } }
            if (_dragBand >= 0) { Capture = true; MoveBand(e); }
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); e = LogicalMouse(e); if (_dragBand >= 0) MoveBand(e); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _dragBand = -1; Capture = false; _tip.Hide(this); }
        private void MoveBand(MouseEventArgs e)
        {
            Rectangle p = Plot; double hz = Frequency(e.X, p), db = Math.Max(-24, Math.Min(24, 24 - (e.Y - p.Top) * 48.0 / p.Height));
            if (BandChanged != null) BandChanged(_dragBand, hz, db);
            _tip.SetToolTip(this, String.Format("{0:0} Hz  ·  {1:+0.0;-0.0;0.0} dB", hz, db));
        }
    }
}
