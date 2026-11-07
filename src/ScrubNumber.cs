using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlopProX
{
    // Numeric entry remains keyboard-editable; drag vertically or turn the wheel to adjust.
    public sealed class ScrubNumber : NumericUpDown
    {
        private Control _edit;
        private bool _dragging;
        private int _startY;
        private decimal _startValue;
        private EditMouseHook _mouseHook;
        public ScrubNumber()
        {
            foreach (Control child in Controls)
            {
                if (child is TextBox)
                {
                    _edit = child; child.Cursor = Cursors.SizeNS;
                    _mouseHook = new EditMouseHook(this, child);
                    child.MouseWheel += delegate(object sender, MouseEventArgs e) { SetValue(Value + (e.Delta / 120) * Increment); var handled = e as HandledMouseEventArgs; if (handled != null) handled.Handled = true; };
                }
                else child.Visible = false;
            }
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (_edit == null) return;
            foreach (Control child in Controls) if (child != _edit) child.Visible = false;
            _edit.Bounds = new Rectangle(1, Math.Max(0, (ClientSize.Height - _edit.Height) / 2), Math.Max(1, ClientSize.Width - 2), _edit.Height);
        }
        protected override void OnMouseWheel(MouseEventArgs e) { SetValue(Value + (e.Delta / 120) * Increment); var handled = e as HandledMouseEventArgs; if (handled != null) handled.Handled = true; }
        private void SetValue(decimal value) { Value = Math.Max(Minimum, Math.Min(Maximum, value)); }
        protected override void Dispose(bool disposing) { if (disposing && _mouseHook != null) { _mouseHook.Dispose(); _mouseHook = null; } base.Dispose(disposing); }

        // The native edit inside NumericUpDown does not reliably raise WinForms
        // MouseDown/Move events. Observe its Windows messages without replacing its
        // normal text-entry behaviour.
        private sealed class EditMouseHook : NativeWindow, IDisposable
        {
            private readonly ScrubNumber _owner;
            private readonly Control _edit;
            public EditMouseHook(ScrubNumber owner, Control edit)
            {
                _owner = owner; _edit = edit;
                edit.HandleCreated += HandleCreated; edit.HandleDestroyed += HandleDestroyed;
                if (edit.IsHandleCreated) AssignHandle(edit.Handle);
            }
            private void HandleCreated(object sender, EventArgs e) { AssignHandle(_edit.Handle); }
            private void HandleDestroyed(object sender, EventArgs e) { ReleaseHandle(); }
            protected override void WndProc(ref Message m)
            {
                int kind = m.Msg;
                long pos = m.LParam.ToInt64();
                Point point = new Point((short)(pos & 0xffff), (short)((pos >> 16) & 0xffff));
                base.WndProc(ref m);
                if (_owner.IsDisposed || !_edit.Enabled) return;
                if (kind == 0x201) { _owner._dragging = true; _owner._startY = _edit.PointToScreen(point).Y; _owner._startValue = _owner.Value; _edit.Capture = true; }
                else if (kind == 0x200 && _owner._dragging) { int delta = _owner._startY - _edit.PointToScreen(point).Y; if (Math.Abs(delta) >= 3) _owner.SetValue(_owner._startValue + (delta / 3) * _owner.Increment); }
                else if (kind == 0x202 || kind == 0x215) { _owner._dragging = false; }
            }
            public void Dispose() { _edit.HandleCreated -= HandleCreated; _edit.HandleDestroyed -= HandleDestroyed; ReleaseHandle(); }
        }
    }
}
