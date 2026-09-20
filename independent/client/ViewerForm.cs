using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmazonZLRemote
{
    internal sealed class ViewerForm : Form
    {
        private readonly PictureBox _picture = new PictureBox();
        private readonly Label _status = new Label();
        private readonly Func<Dictionary<string, object>, Task> _sendControl;
        private DateTime _lastMove = DateTime.MinValue;

        public ViewerForm(Func<Dictionary<string, object>, Task> sendControl)
        {
            _sendControl = sendControl;
            Text = "Amazon ZL Remote — sesión";
            Width = 1100;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            KeyPreview = true;

            _status.Dock = DockStyle.Top;
            _status.Height = 28;
            _status.TextAlign = ContentAlignment.MiddleCenter;
            _status.Text = "SESIÓN REMOTA ACTIVA — la otra persona autorizó esta conexión";
            _status.BackColor = Color.FromArgb(31, 122, 77);
            _status.ForeColor = Color.White;

            _picture.Dock = DockStyle.Fill;
            _picture.BackColor = Color.Black;
            _picture.SizeMode = PictureBoxSizeMode.Zoom;
            _picture.TabStop = true;

            Controls.Add(_picture);
            Controls.Add(_status);

            _picture.MouseMove += Picture_MouseMove;
            _picture.MouseDown += Picture_MouseDown;
            _picture.MouseUp += Picture_MouseUp;
            _picture.MouseEnter += (s, e) => _picture.Focus();
            KeyDown += ViewerForm_KeyDown;
            KeyUp += ViewerForm_KeyUp;
            FormClosed += ViewerForm_FormClosed;
        }

        public void SetFrame(byte[] jpeg)
        {
            if (IsDisposed) return;
            try
            {
                using (var ms = new MemoryStream(jpeg))
                using (var temp = Image.FromStream(ms))
                {
                    var copy = new Bitmap(temp);
                    var old = _picture.Image;
                    _picture.Image = copy;
                    if (old != null) old.Dispose();
                }
            }
            catch { }
        }

        private async void Picture_MouseMove(object sender, MouseEventArgs e)
        {
            if ((DateTime.UtcNow - _lastMove).TotalMilliseconds < 45) return;
            _lastMove = DateTime.UtcNow;
            double nx, ny;
            if (!TryNormalize(e.Location, out nx, out ny)) return;
            await SafeSend(new Dictionary<string, object>
            {
                ["type"] = "input",
                ["kind"] = "move",
                ["x"] = nx,
                ["y"] = ny
            });
        }

        private async void Picture_MouseDown(object sender, MouseEventArgs e)
        {
            double nx, ny;
            if (!TryNormalize(e.Location, out nx, out ny)) return;
            await SafeSend(new Dictionary<string, object>
            {
                ["type"] = "input",
                ["kind"] = "mouse",
                ["x"] = nx,
                ["y"] = ny,
                ["button"] = ButtonName(e.Button),
                ["down"] = true
            });
        }

        private async void Picture_MouseUp(object sender, MouseEventArgs e)
        {
            double nx, ny;
            if (!TryNormalize(e.Location, out nx, out ny)) return;
            await SafeSend(new Dictionary<string, object>
            {
                ["type"] = "input",
                ["kind"] = "mouse",
                ["x"] = nx,
                ["y"] = ny,
                ["button"] = ButtonName(e.Button),
                ["down"] = false
            });
        }

        private async void ViewerForm_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            await SafeSend(new Dictionary<string, object>
            {
                ["type"] = "input",
                ["kind"] = "key",
                ["keyCode"] = e.KeyValue,
                ["down"] = true
            });
        }

        private async void ViewerForm_KeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            await SafeSend(new Dictionary<string, object>
            {
                ["type"] = "input",
                ["kind"] = "key",
                ["keyCode"] = e.KeyValue,
                ["down"] = false
            });
        }

        private async void ViewerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            await SafeSend(new Dictionary<string, object> { ["type"] = "disconnect" });
        }

        private async Task SafeSend(Dictionary<string, object> message)
        {
            try { await _sendControl(message); } catch { }
        }

        private string ButtonName(MouseButtons button)
        {
            if (button == MouseButtons.Right) return "right";
            if (button == MouseButtons.Middle) return "middle";
            return "left";
        }

        private bool TryNormalize(Point p, out double nx, out double ny)
        {
            nx = ny = 0;
            if (_picture.Image == null || _picture.ClientSize.Width <= 0 || _picture.ClientSize.Height <= 0)
                return false;

            double imageAspect = (double)_picture.Image.Width / _picture.Image.Height;
            double boxAspect = (double)_picture.ClientSize.Width / _picture.ClientSize.Height;

            double drawW, drawH, offsetX, offsetY;
            if (imageAspect > boxAspect)
            {
                drawW = _picture.ClientSize.Width;
                drawH = drawW / imageAspect;
                offsetX = 0;
                offsetY = (_picture.ClientSize.Height - drawH) / 2.0;
            }
            else
            {
                drawH = _picture.ClientSize.Height;
                drawW = drawH * imageAspect;
                offsetY = 0;
                offsetX = (_picture.ClientSize.Width - drawW) / 2.0;
            }

            if (p.X < offsetX || p.X > offsetX + drawW || p.Y < offsetY || p.Y > offsetY + drawH)
                return false;

            nx = (p.X - offsetX) / drawW;
            ny = (p.Y - offsetY) / drawH;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _picture.Image != null)
            {
                _picture.Image.Dispose();
                _picture.Image = null;
            }
            base.Dispose(disposing);
        }
    }
}
