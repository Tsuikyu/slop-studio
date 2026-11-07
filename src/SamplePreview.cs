using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlopStudio
{
    public sealed class SampleWaveformPreview : Control
    {
        private string _path;
        public string SourcePath { get { return _path; } set { _path = value; Invalidate(); } }
        public SampleWaveformPreview() { DoubleBuffered = true; BackColor = CyberTheme.Background; ForeColor = CyberTheme.Secondary; Height = 115; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); e.Graphics.Clear(BackColor);
            var box = new Rectangle(8, 26, Math.Max(1, Width - 16), Math.Max(1, Height - 34));
            using (var pen = new Pen(CyberTheme.Border)) e.Graphics.DrawRectangle(pen, box);
            StudioDraw.Label(e.Graphics, String.IsNullOrEmpty(_path) ? "Select a sample to preview" : Path.GetFileName(_path), Font,
                new Rectangle(9, 3, Math.Max(1, Width - 18), 20), CyberTheme.Text, TextFormatFlags.EndEllipsis);
            WaveformPeaks peaks = WaveformCache.Get(_path, this);
            if (peaks != null && peaks.Duration > 0) WaveformCache.Draw(e.Graphics, Rectangle.Inflate(box, -4, -4), _path, 0, peaks.Duration, this, CyberTheme.Accent);
        }
    }

    public sealed class MasterReferenceForm : Form
    {
        private readonly SampleWaveformPreview _wave;
        private readonly Label _status;
        private string _decoded;
        private int _generation;
        public MasterReferenceForm()
        {
            Text = UiText.Translate("Mastering Reference"); ClientSize = new Size(640, 230); MinimumSize = new Size(420, 210);
            BackColor = CyberTheme.Surface; ForeColor = CyberTheme.Text; StartPosition = FormStartPosition.CenterParent;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 5, 0, 0), WrapContents = false };
            var open = new Button { Text = UiText.Translate("Open Reference…"), AutoSize = true, Height = 28 };
            var play = new Button { Text = UiText.Translate("Preview"), AutoSize = true, Height = 28 };
            var stop = new Button { Text = UiText.Translate("Stop"), AutoSize = true, Height = 28 };
            open.Click += async delegate
            {
                using (var dialog = new OpenFileDialog { Filter = "Audio|*.wav;*.wave;*.aif;*.aiff;*.flac;*.mp3;*.ogg;*.m4a;*.wma" })
                    if (dialog.ShowDialog(this) == DialogResult.OK) await LoadReference(dialog.FileName);
            };
            play.Click += delegate { try { if (!String.IsNullOrEmpty(_decoded)) WaveAudio.Play(_decoded); } catch (Exception ex) { _status.Text = ex.Message; } };
            stop.Click += delegate { WaveAudio.Stop(); };
            buttons.Controls.Add(open); buttons.Controls.Add(play); buttons.Controls.Add(stop);
            _wave = new SampleWaveformPreview { Dock = DockStyle.Fill };
            _status = new Label { Dock = DockStyle.Bottom, Height = 30, AutoEllipsis = true, Text = UiText.Translate("Preview uses the Windows default output device.") };
            Controls.Add(_wave); Controls.Add(buttons); Controls.Add(_status);
            FormClosed += delegate { _generation++; WaveAudio.Stop(); };
            Shown += delegate { UiText.Apply(this); };
        }
        private async Task LoadReference(string path)
        {
            int generation = ++_generation; WaveAudio.Stop(); _status.Text = UiText.Translate("Preparing preview…");
            try
            {
                string folder = Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath), "Preview Cache");
                string decoded = await Task.Factory.StartNew(() => MediaImport.Decode(path, folder));
                if (IsDisposed || generation != _generation) return;
                _decoded = decoded; _wave.SourcePath = decoded; _status.Text = Path.GetFileName(path); WaveAudio.Play(decoded);
            }
            catch (Exception ex) { if (!IsDisposed && generation == _generation) _status.Text = ex.Message; }
        }
    }

    public sealed partial class MainForm
    {
        private void ShowMasterReference() { var form = new MasterReferenceForm(); form.Show(this); }
    }
}
