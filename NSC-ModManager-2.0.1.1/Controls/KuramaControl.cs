using System;
using System.IO;
using System.Windows.Forms;

namespace NSC_ModManager.Controls
{
    /// <summary>
    /// WinForms replacement for the WPF KuramaControl. The original composited a
    /// body image with three independently tail-wiggling images via Storyboards;
    /// this shows the body image only (static, no wiggle) -- purely decorative,
    /// so no behavior is lost, just the idle animation.
    /// </summary>
    public partial class KuramaControl : UserControl
    {
        private readonly PictureBox _body;

        public KuramaControl()
        {
            Width = 150;
            Height = 150;

            _body = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
            Controls.Add(_body);

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Styles", "UI", "kurama", "kurama_body.png");
            if (File.Exists(path))
                _body.Image = System.Drawing.Image.FromFile(path);
        }
    }
}
