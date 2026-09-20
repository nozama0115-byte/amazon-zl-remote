using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace AmazonZLRelayServer;

enum PacketType : byte
{
    Control = 1,
    Screen = 2
}

sealed class Packet
{
    public PacketType Type { get; init; }
    public byte[] Payload { get; init; } = Array.Empty<byte>();
}

sealed class ClientSession
{
    public Guid ConnectionId { get; } = Guid.NewGuid();
    public TcpClient Tcp { get; }
    public Stream Stream { get; }
    public SemaphoreSlim SendLock { get; } = new(1, 1);
    public string? PublicId { get; set; }
    public string Name { get; set; } = "Equipo";
    public ClientSession? Peer { get; set; }

    public ClientSession(TcpClient tcp, Stream stream)
    {
        Tcp = tcp;
        Stream = stream;
    }

    public async Task SendPacketAsync(PacketType type, byte[] payload)
    {
        byte[] header = new byte[5];
        header[0] = (byte)type;
        byte[] len = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(payload.Length));
        Buffer.BlockCopy(len, 0, header, 1, 4);

        await SendLock.WaitAsync();
        try
        {
            await Stream.WriteAsync(header);
            if (payload.Length > 0) await Stream.WriteAsync(payload);
            await Stream.FlushAsync();
        }
        finally
        {
            SendLock.Release();
        }
    }

    public Task SendJsonAsync(object value)
    {
        return SendPacketAsync(PacketType.Control, JsonSerializer.SerializeToUtf8Bytes(value));
    }
}

sealed class PendingRequest
{
    public required string RequestId { get; init; }
    public required ClientSession Controller { get; init; }
    public required ClientSession Host { get; init; }
}

static class Relay
{
    private static readonly ConcurrentDictionary<string, ClientSession> Hosts = new();
    private static readonly ConcurrentDictionary<string, PendingRequest> Pending = new();

    public static async Task RunClientAsync(ClientSession client)
    {
        try
        {
            while (true)
            {
                Packet packet = await ReadPacketAsync(client.Stream);

                if (packet.Type == PacketType.Screen)
                {
                    if (client.Peer != null)
                        await client.Peer.SendPacketAsync(packet.Type, packet.Payload);
                    continue;
                }

                string json = Encoding.UTF8.GetString(packet.Payload);
                using JsonDocument doc = JsonDocument.Parse(json);
                string type = doc.RootElement.TryGetProperty("type", out var typeEl)
                    ? typeEl.GetString() ?? ""
                    : "";

                if (type == "register")
                {
                    string id = Digits(doc, "id");
                    string name = Text(doc, "name");
                    if (id.Length != 9)
                    {
                        await client.SendJsonAsync(new { type = "error", message = "invalid_id" });
                        continue;
                    }

                    if (client.PublicId != null)
                        Hosts.TryRemove(client.PublicId, out _);

                    if (!Hosts.TryAdd(id, client))
                    {
                        await client.SendJsonAsync(new { type = "error", message = "id_in_use" });
                        continue;
                    }

                    client.PublicId = id;
                    client.Name = string.IsNullOrWhiteSpace(name) ? "Equipo" : name;
                    await client.SendJsonAsync(new { type = "registered", id });
                    continue;
                }

                if (type == "connect_request")
                {
                    if (client.Peer != null)
                    {
                        await client.SendJsonAsync(new { type = "error", message = "already_in_session" });
                        continue;
                    }

                    string target = Digits(doc, "target");
                    if (!Hosts.TryGetValue(target, out var host) || host == client)
                    {
                        await client.SendJsonAsync(new { type = "not_found" });
                        continue;
                    }

                    if (host.Peer != null)
                    {
                        await client.SendJsonAsync(new { type = "error", message = "target_busy" });
                        continue;
                    }

                    string requestId = Guid.NewGuid().ToString("N");
                    var pending = new PendingRequest
                    {
                        RequestId = requestId,
                        Controller = client,
                        Host = host
                    };
                    Pending[requestId] = pending;

                    await host.SendJsonAsync(new
                    {
                        type = "incoming",
                        requestId,
                        name = client.Name,
                        controllerId = client.PublicId ?? ""
                    });
                    continue;
                }

                if (type == "accept" || type == "reject")
                {
                    string requestId = Text(doc, "requestId");
                    if (!Pending.TryRemove(requestId, out var pending) || pending.Host != client)
                        continue;

                    if (type == "reject")
                    {
                        await pending.Controller.SendJsonAsync(new { type = "rejected" });
                        continue;
                    }

                    if (pending.Controller.Peer != null || pending.Host.Peer != null)
                    {
                        await pending.Controller.SendJsonAsync(new { type = "error", message = "busy" });
                        continue;
                    }

                    pending.Controller.Peer = pending.Host;
                    pending.Host.Peer = pending.Controller;

                    await pending.Host.SendJsonAsync(new
                    {
                        type = "connected",
                        role = "host",
                        peer = pending.Controller.Name
                    });
                    await pending.Controller.SendJsonAsync(new
                    {
                        type = "connected",
                        role = "controller",
                        peer = pending.Host.Name
                    });
                    continue;
                }

                if (type == "disconnect")
                {
                    await UnpairAsync(client);
                    continue;
                }

                if (type == "input")
                {
                    if (client.Peer != null)
                        await client.Peer.SendPacketAsync(PacketType.Control, packet.Payload);
                    continue;
                }
            }
        }
        catch (EndOfStreamException) { }
        catch (IOException) { }
        catch (SocketException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"Client {client.ConnectionId}: {ex.Message}");
        }
        finally
        {
            await CleanupAsync(client);
        }
    }

    private static async Task UnpairAsync(ClientSession client)
    {
        ClientSession? peer = client.Peer;
        client.Peer = null;
        if (peer != null)
        {
            peer.Peer = null;
            try { await peer.SendJsonAsync(new { type = "disconnected" }); } catch { }
        }

        try { await client.SendJsonAsync(new { type = "disconnected" }); } catch { }
    }

    private static async Task CleanupAsync(ClientSession client)
    {
        if (client.PublicId != null)
            Hosts.TryRemove(client.PublicId, out _);

        foreach (var item in Pending.ToArray())
        {
            if (item.Value.Controller == client || item.Value.Host == client)
            {
                if (Pending.TryRemove(item.Key, out var p))
                {
                    ClientSession other = p.Controller == client ? p.Host : p.Controller;
                    try { await other.SendJsonAsync(new { type = "disconnected" }); } catch { }
                }
            }
        }

        await UnpairAsync(client);

        try { client.Stream.Dispose(); } catch { }
        try { client.Tcp.Close(); } catch { }
        client.SendLock.Dispose();
    }

    private static async Task<Packet> ReadPacketAsync(Stream stream)
    {
        byte[] header = await ReadExactAsync(stream, 5);
        var type = (PacketType)header[0];
        int length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header, 1));
        if (length < 0 || length > 25 * 1024 * 1024)
            throw new InvalidDataException("Invalid packet length.");

        return new Packet
        {
            Type = type,
            Payload = await ReadExactAsync(stream, length)
        };
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int count)
    {
        byte[] data = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = await stream.ReadAsync(data.AsMemory(offset, count - offset));
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
        return data;
    }

    private static string Text(JsonDocument doc, string name)
    {
        return doc.RootElement.TryGetProperty(name, out var el) ? el.GetString() ?? "" : "";
    }

    private static string Digits(JsonDocument doc, string name)
    {
        return new string(Text(doc, name).Where(char.IsDigit).ToArray());
    }
}

static class Program
{
    public static async Task Main(string[] args)
    {
        int port = GetInt(args, "--port", 21120);
        bool insecureDev = args.Contains("--insecure-dev", StringComparer.OrdinalIgnoreCase);
        string? pfx = GetValue(args, "--pfx");
        string? password = GetValue(args, "--password");

        X509Certificate2? certificate = null;
        if (!insecureDev)
        {
            if (string.IsNullOrWhiteSpace(pfx))
            {
                Console.Error.WriteLine("Production mode requires --pfx CERTIFICATE.pfx. Use --insecure-dev only for local testing.");
                Environment.ExitCode = 2;
                return;
            }
            certificate = new X509Certificate2(pfx, password);
        }

        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        Console.WriteLine($"Amazon ZL Relay Server listening on port {port}.");
        Console.WriteLine(insecureDev
            ? "WARNING: insecure development mode. Do not expose this mode to the Internet."
            : "TLS enabled.");

        while (true)
        {
            TcpClient tcp = await listener.AcceptTcpClientAsync();
            tcp.NoDelay = true;
            _ = Task.Run(async () =>
            {
                Stream stream = tcp.GetStream();
                try
                {
                    if (!insecureDev)
                    {
                        var ssl = new SslStream(stream, false);
                        await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                        {
                            ServerCertificate = certificate,
                            ClientCertificateRequired = false,
                            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                        });
                        stream = ssl;
                    }

                    var client = new ClientSession(tcp, stream);
                    await Relay.RunClientAsync(client);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Connection failed: {ex.Message}");
                    try { stream.Dispose(); } catch { }
                    try { tcp.Close(); } catch { }
                }
            });
        }
    }

    private static string? GetValue(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static int GetInt(string[] args, string key, int fallback)
    {
        string? value = GetValue(args, key);
        return int.TryParse(value, out int n) ? n : fallback;
    }
}
