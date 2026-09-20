using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmazonZLRemote
{
    internal sealed class MainForm : Form
    {
        private readonly TextBox _serverHost = new TextBox();
        private readonly NumericUpDown _serverPort = new NumericUpDown();
        private readonly CheckBox _useTls = new CheckBox();
        private readonly Button _serverButton = new Button();
        private readonly Label _serverStatus = new Label();

        private readonly Label _myId = new Label();
        private readonly TextBox _remoteId = new TextBox();
        private readonly Button _connectButton = new Button();
        private readonly Label _sessionBanner = new Label();

        private RemoteConnection _connection;
        private CancellationTokenSource _readCts;
        private CancellationTokenSource _captureCts;
        private ViewerForm _viewer;
        private readonly string _localId;

        private bool _pairedAsHost;
        private bool _pairedAsController;

        public MainForm()
        {
            _localId = GenerateId();
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Amazon ZL Remote — Independiente";
            Width = 840;
            Height = 510;
            MinimumSize = new Size(760, 470);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;

            _sessionBanner.Dock = DockStyle.Top;
            _sessionBanner.Height = 34;
            _sessionBanner.TextAlign = ContentAlignment.MiddleCenter;
            _sessionBanner.Text = "SIN SESIÓN REMOTA";
            _sessionBanner.BackColor = Color.FromArgb(236, 240, 241);
            _sessionBanner.ForeColor = Color.FromArgb(45, 55, 65);

            var title = new Label
            {
                Text = "AMAZON ZL REMOTE",
                Font = new Font("Segoe UI", 17, FontStyle.Bold),
                ForeColor = Color.FromArgb(31, 122, 77),
                AutoSize = true,
                Location = new Point(30, 58)
            };

            var subtitle = new Label
            {
                Text = "Aplicación independiente — conexión visible y autorizada",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(33, 92)
            };

            var serverGroup = new GroupBox
            {
                Text = "Servidor propio",
                Location = new Point(30, 125),
                Size = new Size(760, 90)
            };

            _serverHost.Location = new Point(18, 33);
            _serverHost.Width = 250;
            _serverHost.Text = "127.0.0.1";

            _serverPort.Location = new Point(280, 33);
            _serverPort.Width = 85;
            _serverPort.Minimum = 1;
            _serverPort.Maximum = 65535;
            _serverPort.Value = 21120;

            _useTls.Text = "TLS";
            _useTls.Location = new Point(380, 35);
            _useTls.AutoSize = true;

            _serverButton.Text = "Conectar al servidor";
            _serverButton.Location = new Point(455, 29);
            _serverButton.Size = new Size(145, 32);
            _serverButton.Click += ServerButton_Click;

            _serverStatus.Text = "No conectado";
            _serverStatus.Location = new Point(612, 36);
            _serverStatus.AutoSize = true;
            _serverStatus.ForeColor = Color.Firebrick;

            serverGroup.Controls.Add(_serverHost);
            serverGroup.Controls.Add(_serverPort);
            serverGroup.Controls.Add(_useTls);
            serverGroup.Controls.Add(_serverButton);
            serverGroup.Controls.Add(_serverStatus);

            var left = new GroupBox
            {
                Text = "Este equipo",
                Location = new Point(30, 235),
                Size = new Size(355, 180)
            };

            left.Controls.Add(new Label
            {
                Text = "Código de este equipo",
                AutoSize = true,
                Location = new Point(20, 35)
            });

            _myId.Text = FormatId(_localId);
            _myId.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            _myId.ForeColor = Color.FromArgb(31, 122, 77);
            _myId.AutoSize = true;
            _myId.Location = new Point(18, 60);
            left.Controls.Add(_myId);

            left.Controls.Add(new Label
            {
                Text = "Cada solicitud debe aceptarse en esta PC.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Location = new Point(20, 118)
            });

            var right = new GroupBox
            {
                Text = "Conectar a otro equipo",
                Location = new Point(435, 235),
                Size = new Size(355, 180)
            };

            _remoteId.Location = new Point(22, 48);
            _remoteId.Width = 305;
            _remoteId.Font = new Font("Segoe UI", 14);
            _remoteId.MaxLength = 11;

            _connectButton.Text = "Conectar";
            _connectButton.Location = new Point(22, 92);
            _connectButton.Size = new Size(305, 38);
            _connectButton.BackColor = Color.FromArgb(31, 122, 77);
            _connectButton.ForeColor = Color.White;
            _connectButton.FlatStyle = FlatStyle.Flat;
            _connectButton.Enabled = false;
            _connectButton.Click += ConnectButton_Click;

            right.Controls.Add(_remoteId);
            right.Controls.Add(_connectButton);

            Controls.Add(_sessionBanner);
            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(serverGroup);
            Controls.Add(left);
            Controls.Add(right);

            FormClosing += MainForm_FormClosing;
        }

        private async void ServerButton_Click(object sender, EventArgs e)
        {
            _serverButton.Enabled = false;
            _serverStatus.Text = "Conectando...";
            _serverStatus.ForeColor = Color.DarkOrange;

            try
            {
                DisconnectTransport();
                _connection = new RemoteConnection();
                await _connection.ConnectAsync(_serverHost.Text.Trim(), (int)_serverPort.Value, _useTls.Checked);
                await _connection.SendControlAsync(new Dictionary<string, object>
                {
                    ["type"] = "register",
                    ["id"] = _localId,
                    ["name"] = Environment.MachineName
                });

                _readCts = new CancellationTokenSource();
                _ = Task.Run(() => ReadLoopAsync(_readCts.Token));

                _serverStatus.Text = "Conectado";
                _serverStatus.ForeColor = Color.FromArgb(31, 122, 77);
                _connectButton.Enabled = true;
                _serverButton.Text = "Reconectar";
            }
            catch (Exception ex)
            {
                _serverStatus.Text = "Error";
                _serverStatus.ForeColor = Color.Firebrick;
                MessageBox.Show(this, "No se pudo conectar al servidor propio.\n\n" + ex.Message,
                    "Amazon ZL Remote", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _serverButton.Enabled = true;
            }
        }

        private async void ConnectButton_Click(object sender, EventArgs e)
        {
            string target = DigitsOnly(_remoteId.Text);
            if (target.Length != 9)
            {
                MessageBox.Show(this, "Escriba un código de 9 dígitos.", "Amazon ZL Remote",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (target == _localId)
            {
                MessageBox.Show(this, "Ese código pertenece a este mismo equipo.", "Amazon ZL Remote",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                _connectButton.Enabled = false;
                await _connection.SendControlAsync(new Dictionary<string, object>
                {
                    ["type"] = "connect_request",
                    ["target"] = target,
                    ["name"] = Environment.MachineName
                });
                _sessionBanner.Text = "ESPERANDO AUTORIZACIÓN DEL EQUIPO REMOTO";
                _sessionBanner.BackColor = Color.FromArgb(255, 243, 205);
            }
            catch (Exception ex)
            {
                _connectButton.Enabled = true;
                MessageBox.Show(this, ex.Message, "Error de conexión",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _connection != null)
                {
                    Packet packet = await _connection.ReadPacketAsync(token).ConfigureAwait(false);

                    if (packet.Type == PacketType.Screen)
                    {
                        byte[] frame = packet.Payload;
                        BeginInvoke((Action)(() =>
                        {
                            if (_viewer != null && !_viewer.IsDisposed)
                                _viewer.SetFrame(frame);
                        }));
                        continue;
                    }

                    var msg = _connection.ParseControl(packet);
                    string type = GetString(msg, "type");
                    await HandleControlAsync(type, msg).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                if (!IsDisposed)
                {
                    BeginInvoke((Action)(() =>
                    {
                        _serverStatus.Text = "Desconectado";
                        _serverStatus.ForeColor = Color.Firebrick;
                        _connectButton.Enabled = false;
                        EndSessionUi();
                    }));
                }
            }
        }

        private async Task HandleControlAsync(string type, Dictionary<string, object> msg)
        {
            if (type == "registered")
            {
                return;
            }

            if (type == "incoming")
            {
                string requestId = GetString(msg, "requestId");
                string name = GetString(msg, "name");
                bool accept = false;

                Invoke((Action)(() =>
                {
                    var result = MessageBox.Show(this,
                        "El equipo \"" + (string.IsNullOrWhiteSpace(name) ? "desconocido" : name) +
                        "\" solicita controlar esta PC.\n\n¿Permitir esta sesión remota?",
                        "Solicitud de conexión — Amazon ZL Remote",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button2);
                    accept = result == DialogResult.Yes;
                }));

                await _connection.SendControlAsync(new Dictionary<string, object>
                {
                    ["type"] = accept ? "accept" : "reject",
                    ["requestId"] = requestId
                }).ConfigureAwait(false);
                return;
            }

            if (type == "connected")
            {
                string role = GetString(msg, "role");
                if (role == "host")
                {
                    _pairedAsHost = true;
                    StartCapture();
                    BeginInvoke((Action)(() =>
                    {
                        _sessionBanner.Text = "SESIÓN REMOTA ACTIVA — ESTA PC ESTÁ SIENDO CONTROLADA";
                        _sessionBanner.BackColor = Color.FromArgb(255, 193, 7);
                        _sessionBanner.ForeColor = Color.Black;
                    }));
                }
                else
                {
                    _pairedAsController = true;
                    BeginInvoke((Action)(() =>
                    {
                        _viewer = new ViewerForm(SendControlSafeAsync);
                        _viewer.FormClosed += (s, e) => _viewer = null;
                        _viewer.Show(this);
                        _sessionBanner.Text = "SESIÓN REMOTA ACTIVA — CONTROLANDO OTRO EQUIPO";
                        _sessionBanner.BackColor = Color.FromArgb(31, 122, 77);
                        _sessionBanner.ForeColor = Color.White;
                    }));
                }
                return;
            }

            if (type == "rejected")
            {
                BeginInvoke((Action)(() =>
                {
                    _connectButton.Enabled = true;
                    EndSessionUi();
                    MessageBox.Show(this, "La persona del otro equipo rechazó la solicitud.",
                        "Conexión no autorizada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
                return;
            }

            if (type == "not_found")
            {
                BeginInvoke((Action)(() =>
                {
                    _connectButton.Enabled = true;
                    EndSessionUi();
                    MessageBox.Show(this, "Ese código no está conectado al servidor.",
                        "Equipo no disponible", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
                return;
            }

            if (type == "disconnected")
            {
                _pairedAsHost = false;
                _pairedAsController = false;
                StopCapture();
                BeginInvoke((Action)(() =>
                {
                    if (_viewer != null && !_viewer.IsDisposed) _viewer.Close();
                    _connectButton.Enabled = _connection != null && _connection.IsConnected;
                    EndSessionUi();
                }));
                return;
            }

            if (type == "input" && _pairedAsHost)
            {
                string kind = GetString(msg, "kind");
                if (kind == "move")
                {
                    InputController.MoveNormalized(GetDouble(msg, "x"), GetDouble(msg, "y"));
                }
                else if (kind == "mouse")
                {
                    InputController.MoveNormalized(GetDouble(msg, "x"), GetDouble(msg, "y"));
                    InputController.MouseButton(GetString(msg, "button"), GetBool(msg, "down"));
                }
                else if (kind == "key")
                {
                    InputController.Keyboard(GetInt(msg, "keyCode"), GetBool(msg, "down"));
                }
            }
        }

        private Task SendControlSafeAsync(Dictionary<string, object> message)
        {
            if (_connection == null) return Task.CompletedTask;
            return _connection.SendControlAsync(message);
        }

        private void StartCapture()
        {
            StopCapture();
            _captureCts = new CancellationTokenSource();
            _ = Task.Run(() => CaptureLoopAsync(_captureCts.Token));
        }

        private void StopCapture()
        {
            if (_captureCts != null)
            {
                try { _captureCts.Cancel(); } catch { }
                _captureCts.Dispose();
                _captureCts = null;
            }
        }

        private async Task CaptureLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _pairedAsHost && _connection != null)
            {
                try
                {
                    byte[] jpeg = CaptureScreenJpeg();
                    await _connection.SendScreenAsync(jpeg).ConfigureAwait(false);
                    await Task.Delay(350, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch { await Task.Delay(800).ConfigureAwait(false); }
            }
        }

        private byte[] CaptureScreenJpeg()
        {
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            using (var full = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(full))
                {
                    g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
                }

                int targetWidth = Math.Min(1280, full.Width);
                int targetHeight = (int)Math.Round((double)full.Height * targetWidth / full.Width);

                using (var scaled = new Bitmap(targetWidth, targetHeight))
                {
                    using (Graphics g = Graphics.FromImage(scaled))
                    {
                        g.DrawImage(full, 0, 0, targetWidth, targetHeight);
                    }

                    using (var ms = new MemoryStream())
                    {
                        ImageCodecInfo jpg = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
                        using (var parameters = new EncoderParameters(1))
                        {
                            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 48L);
                            scaled.Save(ms, jpg, parameters);
                        }
                        return ms.ToArray();
                    }
                }
            }
        }

        private void EndSessionUi()
        {
            _sessionBanner.Text = "SIN SESIÓN REMOTA";
            _sessionBanner.BackColor = Color.FromArgb(236, 240, 241);
            _sessionBanner.ForeColor = Color.FromArgb(45, 55, 65);
        }

        private void DisconnectTransport()
        {
            StopCapture();
            if (_readCts != null)
            {
                try { _readCts.Cancel(); } catch { }
                _readCts.Dispose();
                _readCts = null;
            }
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
            _pairedAsHost = false;
            _pairedAsController = false;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            DisconnectTransport();
        }

        private static string GenerateId()
        {
            byte[] bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            uint value = BitConverter.ToUInt32(bytes, 0);
            return (100000000u + (value % 900000000u)).ToString();
        }

        private static string FormatId(string id)
        {
            return id.Substring(0, 3) + " " + id.Substring(3, 3) + " " + id.Substring(6, 3);
        }

        private static string DigitsOnly(string value)
        {
            return new string((value ?? "").Where(char.IsDigit).ToArray());
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : "";
        }

        private static double GetDouble(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? Convert.ToDouble(v) : 0;
        }

        private static int GetInt(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? Convert.ToInt32(v) : 0;
        }

        private static bool GetBool(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null && Convert.ToBoolean(v);
        }
    }
}
