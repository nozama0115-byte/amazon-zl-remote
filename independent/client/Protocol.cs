using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AmazonZLRemote
{
    internal enum PacketType : byte
    {
        Control = 1,
        Screen = 2
    }

    internal sealed class Packet
    {
        public PacketType Type { get; set; }
        public byte[] Payload { get; set; }
    }

    internal sealed class RemoteConnection : IDisposable
    {
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private TcpClient _tcp;
        private Stream _stream;

        public bool IsConnected
        {
            get { return _tcp != null && _tcp.Connected && _stream != null; }
        }

        public async Task ConnectAsync(string host, int port, bool tls)
        {
            DisposeTransport();
            _tcp = new TcpClient();
            _tcp.NoDelay = true;
            await _tcp.ConnectAsync(host, port).ConfigureAwait(false);
            Stream network = _tcp.GetStream();

            if (tls)
            {
                var ssl = new SslStream(network, false);
                await ssl.AuthenticateAsClientAsync(host, null, SslProtocols.Tls12, true).ConfigureAwait(false);
                _stream = ssl;
            }
            else
            {
                _stream = network;
            }
        }

        public async Task SendControlAsync(Dictionary<string, object> message)
        {
            string text = _json.Serialize(message);
            await SendPacketAsync(PacketType.Control, Encoding.UTF8.GetBytes(text)).ConfigureAwait(false);
        }

        public Task SendScreenAsync(byte[] jpeg)
        {
            return SendPacketAsync(PacketType.Screen, jpeg);
        }

        public Dictionary<string, object> ParseControl(Packet packet)
        {
            string text = Encoding.UTF8.GetString(packet.Payload);
            return _json.Deserialize<Dictionary<string, object>>(text);
        }

        public async Task<Packet> ReadPacketAsync(CancellationToken cancellationToken)
        {
            byte[] header = await ReadExactAsync(5, cancellationToken).ConfigureAwait(false);
            PacketType type = (PacketType)header[0];
            int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 1));
            if (length < 0 || length > 25 * 1024 * 1024)
                throw new InvalidDataException("Tamaño de paquete inválido.");

            byte[] payload = await ReadExactAsync(length, cancellationToken).ConfigureAwait(false);
            return new Packet { Type = type, Payload = payload };
        }

        private async Task SendPacketAsync(PacketType type, byte[] payload)
        {
            if (_stream == null) throw new IOException("No hay conexión con el servidor.");
            if (payload == null) payload = new byte[0];

            byte[] header = new byte[5];
            header[0] = (byte)type;
            byte[] len = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));
            Buffer.BlockCopy(len, 0, header, 1, 4);

            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await _stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
                if (payload.Length > 0)
                    await _stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
                await _stream.FlushAsync().ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task<byte[]> ReadExactAsync(int count, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = await _stream.ReadAsync(buffer, offset, count - offset, cancellationToken).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
            return buffer;
        }

        private void DisposeTransport()
        {
            try { if (_stream != null) _stream.Dispose(); } catch { }
            try { if (_tcp != null) _tcp.Close(); } catch { }
            _stream = null;
            _tcp = null;
        }

        public void Dispose()
        {
            DisposeTransport();
            _sendLock.Dispose();
        }
    }
}
