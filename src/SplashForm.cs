using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace SlopStudio
{
    public sealed class SplashForm : Form
    {
        private Bitmap _art;
        private int _progress;
        private string _stage = "Opening the studio…";

        public int Progress { get { return _progress; } }
        public bool HasArtwork { get { return _art != null; } }

        public SplashForm()
        {
            Text = "Slop Studio";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(5, 4, 12);
            DoubleBuffered = true;
            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            int width = Math.Max(420, Math.Min(1050, (int)Math.Min(area.Width * .88, area.Height * .85 * 1672.0 / 941.0)));
            ClientSize = new Size(width, (int)Math.Round(width * 941.0 / 1672.0));
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Startup.png");
                using (var source = Image.FromFile(path)) _art = new Bitmap(source);
            }
            catch (Exception ex) { ErrorLog.Write("Startup artwork", ex); }
        }

        public void SetProgress(int value, string stage)
        {
            _progress = Math.Max(0, Math.Min(100, value));
            _stage = stage ?? "";
            Invalidate();
            if (IsHandleCreated) Update();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (_art != null) g.DrawImage(_art, ClientRectangle);
            else
            {
                BrandMark.Draw(g, new Rectangle(Width / 2 - 48, Height / 3 - 48, 96, 96));
                TextRenderer.DrawText(g, "Slop Studio", Font, new Rectangle(0, Height / 2, Width, 35), Color.White, TextFormatFlags.HorizontalCenter);
            }
            float sx = Width / 1672f, sy = Height / 941f;
            Func<int, int, int, int, Rectangle> box = (x, y, w, h) =>
                new Rectangle((int)(x * sx), (int)(y * sy), Math.Max(1, (int)(w * sx)), Math.Max(1, (int)(h * sy)));
            Rectangle bar = box(626, 806, 420, 26);
            using (var background = new SolidBrush(Color.FromArgb(245, 5, 4, 17))) g.FillRectangle(background, bar);
            Rectangle fill = Rectangle.Inflate(bar, -2, -2);
            fill.Width = (int)Math.Round(fill.Width * _progress / 100.0);
            using (var brush = new SolidBrush(Color.FromArgb(112, 76, 245))) g.FillRectangle(brush, fill);
            using (var border = new Pen(Color.FromArgb(174, 152, 255))) g.DrawRectangle(border, bar.X, bar.Y, bar.Width - 1, bar.Height - 1);

            Rectangle caption = box(550, 841, 572, 34);
            using (var background = new SolidBrush(Color.FromArgb(235, 5, 4, 14))) g.FillRectangle(background, caption);
            using (var captionFont = new Font("Segoe UI", Math.Max(9f, 16f * sy), GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, UiText.Translate(_stage), captionFont, caption, Color.FromArgb(204, 193, 244),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            Rectangle footer = box(196, 879, 1280, 58);
            using (var background = new SolidBrush(Color.FromArgb(220, 5, 4, 14))) g.FillRectangle(background, footer);
            using (var footerFont = new Font("Segoe UI", Math.Max(9f, 15f * sy), GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(g, UiText.Translate(UiText.Disclaimer), footerFont,
                    new Rectangle(footer.X + 6, footer.Y + 2, footer.Width - 12, footer.Height / 2),
                    Color.Silver, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, UiText.Translate(UiText.Attribution), footerFont,
                    new Rectangle(footer.X + 6, footer.Y + footer.Height / 2, footer.Width - 12, footer.Height / 2),
                    Color.FromArgb(156, 151, 192), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _art != null) { _art.Dispose(); _art = null; }
            base.Dispose(disposing);
        }
    }
}
