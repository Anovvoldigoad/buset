using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NSC_ModManager.Controls
{
    /// <summary>
    /// WinForms replacement for the WPF LoadingControl. The original used three
    /// chained Storyboard animations (rotation + pulsing scale + opacity fade) on
    /// two overlapping images; this is a plain rotating-arc spinner drawn with
    /// GDI+. Same public surface (LoadingState) so call sites don't need to change.
    /// </summary>
    public partial class LoadingControl : UserControl
    {
        private readonly System.Windows.Forms.Timer _timer;
        private float _angle;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        [System.ComponentModel.Browsable(false)]
        public System.Windows.Visibility LoadingState
        {
            get => _loadingState;
            set
            {
                _loadingState = value;
                Visible = value == System.Windows.Visibility.Visible;
                if (Visible) _timer.Start(); else _timer.Stop();
            }
        }
        private System.Windows.Visibility _loadingState = System.Windows.Visibility.Hidden;

        public LoadingControl()
        {
            Width = 70;
            Height = 70;
            DoubleBuffered = true;
            Visible = false;

            _timer = new System.Windows.Forms.Timer { Interval = 30 };
            _timer.Tick += (s, e) => { _angle = (_angle + 8) % 360; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new RectangleF(6, 6, Width - 12, Height - 12);
            using (var pen = new Pen(Color.White, 6) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(pen, rect, _angle, 270);
            }
        }
    }
}
