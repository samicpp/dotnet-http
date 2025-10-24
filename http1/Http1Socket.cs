namespace Samicpp.Http.Http1;

using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Samicpp.Http;
using Samicpp.Http.Http2;
using Samicpp.Http.WebSocket;

public class Http1Exception(string? message, Exception? other) : HttpException(message, other)
{
    public sealed class WebSocketNotSupported(string? err = null) : Http1Exception(err, null);
}

public class Http1Socket(IDualSocket socket) : IDualHttpSocket
{
    public bool IsHttps { get => socket.IsSecure; }
    protected readonly IDualSocket socket = socket;
    public void Dispose()
    {
        socket.Dispose();
        GC.SuppressFinalize(this);
    }
    public async ValueTask DisposeAsync()
    {
        await socket.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private readonly HttpClient client = new();
    public IHttpClient Client { get => client; }
    public bool IsClosed { get; set; }
    public bool HeadSent { get; set; }

    public int Status { get; set; } = 200;
    public string StatusMessage { get; set; } = "OK";
    public Compression Compression
    {
        get; set
        {
            field = value;
            switch (value)
            {
                case Compression.None: headers.Remove("Content-Encoding"); break;
                case Compression.Gzip: headers["Content-Encoding"] = ["gzip"]; break;
                case Compression.Deflate: headers["Content-Encoding"] = ["deflate"]; break;
                case Compression.Brotli: headers["Content-Encoding"] = ["br"]; break;
            }
        }
    }

    private readonly Dictionary<string, List<string>> headers = new(/*StringComparer.OrdinalIgnoreCase*/) { { "Connection", ["close"] } };
    public void SetHeader(string name, string value) => headers[name] = [value];
    public void AddHeader(string name, string value)
    {
        if (headers.TryGetValue(name, out List<string>? ls)) ls.Add(value);
        else headers[name] = [value];
    }
    public List<string> DelHeader(string name)
    {
        var head = headers[name];
        if (head == null) return [];
        headers.Remove(name);
        return head;
    }


    public IHttpClient ReadClient()
    {
        bool justReadHeaders = false; // technically redundant due to `else if`
        if (!client.HeadersComplete)
        {
            var buff = socket.ReadUntil([13, 10, 13, 10]);
            var text = Encoding.UTF8.GetString([.. buff]);
            var lines = text.Split("\r\n");
            var mpv = lines[0].Split(" ", 3);

            client.Method = mpv[0];
            client.Path = mpv[1];
            client.Version = "HTTP/1.1";

            foreach (var header in lines[1..])
            {
                if (string.IsNullOrWhiteSpace(header)) continue;
                var hv = header.Split(":", 2);
                var h = hv[0].Trim().ToLower();
                var v = hv[1].Trim();

                if (client.Headers.TryGetValue(h, out List<string>? c)) c.Add(v);
                else client.Headers[h] = [v];
            }

            client.HeadersComplete =
            justReadHeaders = true;
        }
        else if (!client.BodyComplete)
        {
            if (client.Headers.TryGetValue("content-length", out List<string>? slength))
            {
                var _ = int.TryParse(slength[0], out int length);
                client.Body = [.. socket.ReadCertain(length)];
                client.BodyComplete = true;
            }
            else if (!justReadHeaders && client.Headers.TryGetValue("transfer-encoding", out List<string>? te) && te[0] == "chunked")
            {
                var len = socket.ReadUntil([13, 10]);
                var slen = Encoding.UTF8.GetString([.. len]);
                var length = Convert.ToInt32(slen, 16);

                if (length <= 0)
                {
                    client.BodyComplete = true;
                }
                else
                {
                    var chunk = socket.ReadCertain(length + 2);
                    client.Body.AddRange(chunk);
                }
            }
            else
            {
                client.BodyComplete = true;
            }
        }
        return Client;
    }
    public async Task<IHttpClient> ReadClientAsync()
    {
        bool justReadHeaders = false; // technically unnececary due to `else if`
        if (!client.HeadersComplete)
        {
            var buff = await socket.ReadUntilAsync([13, 10, 13, 10]);
            var text = Encoding.UTF8.GetString([.. buff]);
            var lines = text.Split("\r\n");
            var mpv = lines[0].Split(" ", 3);

            client.Method = mpv[0];
            client.Path = mpv[1];

            foreach (var header in lines[1..])
            {
                if (string.IsNullOrWhiteSpace(header)) continue;
                var hv = header.Split(":", 2);
                var h = hv[0].Trim().ToLower();
                var v = hv[1].Trim();

                if (client.Headers.TryGetValue(h, out List<string>? c)) c.Add(v);
                else client.Headers[h] = [v];
            }

            client.HeadersComplete =
            justReadHeaders = true;
        }
        else if (!client.BodyComplete)
        {
            if (client.Headers.TryGetValue("content-length", out List<string>? slength))
            {
                var _ = int.TryParse(slength[0], out int length);
                client.Body = [.. socket.ReadCertain(length)];
                client.BodyComplete = true;
            }
            else if (!justReadHeaders && client.Headers.TryGetValue("transfer-encoding", out List<string>? te) && te[0] == "chunked")
            {
                var len = await socket.ReadUntilAsync([13, 10]);
                var slen = Encoding.UTF8.GetString([.. len]);
                var length = Convert.ToInt32(slen, 16);

                if (length <= 0)
                {
                    client.BodyComplete = true;
                }
                else
                {
                    var chunk = await socket.ReadCertainAsync(length + 2);
                    client.Body.AddRange(chunk);
                }
            }
            else
            {
                client.BodyComplete = true;
            }
        }
        return Client;
    }

    private void SendHead()
    {
        if (!HeadSent)
        {
            string text = $"HTTP/1.1 {Status} {StatusMessage}\r\n";
            foreach (var (h, vs) in headers) foreach (var v in vs) text += $"{h}: {v}\r\n";
            text += "\r\n";
            var buff = Encoding.UTF8.GetBytes(text);
            socket.Write(buff);
            HeadSent = true;
        }
    }
    private async Task SendHeadAsync()
    {
        if (!HeadSent)
        {
            string text = $"HTTP/1.1 {Status} {StatusMessage}\r\n";
            foreach (var (h, vs) in headers) foreach (var v in vs) text += $"{h}: {v}\r\n";
            text += "\r\n";
            var buff = Encoding.UTF8.GetBytes(text);
            await socket.WriteAsync(buff);
            HeadSent = true;
        }
    }


    public void Close(string text) => Close(Encoding.UTF8.GetBytes(text));
    public void Close(byte[] bytes)
    {
        if (!IsClosed && !HeadSent)
        {
            var compressed = Compressor.Compress(this.Compression, bytes);
            headers["Content-Length"] = [compressed.Length.ToString()];
            SendHead();
            socket.Write(compressed);
            IsClosed = true;
        }
        else if (!IsClosed)
        {
            // TODO: add support for sending final headers
            socket.Write(Encoding.UTF8.GetBytes(bytes.Length.ToString("X") + "\r\n"));
            socket.Write(bytes);
            socket.Write([13, 10, 48, 13, 10, 13, 10]);
        }
    }

    public async Task CloseAsync(string text) => await CloseAsync(Encoding.UTF8.GetBytes(text));
    public async Task CloseAsync(byte[] bytes)
    {
        if (!IsClosed && !HeadSent)
        {
            var compressed = await Compressor.CompressAsync(this.Compression, bytes);
            headers["Content-Length"] = [compressed.Length.ToString()];
            await SendHeadAsync();
            await socket.WriteAsync(compressed);
            IsClosed = true;
        }
        else if (!IsClosed)
        {
            // TODO: add support for sending final headers
            byte[] term = [13, 10, 48, 13, 10, 13, 10];
            await socket.WriteAsync(Encoding.UTF8.GetBytes(bytes.Length.ToString("X") + "\r\n"));
            await socket.WriteAsync(bytes);
            await socket.WriteAsync(term);
        }
    }

    public void Write(string text) => Write(Encoding.UTF8.GetBytes(text));
    public void Write(byte[] bytes)
    {
        if (!IsClosed)
        {
            if (!HeadSent)
            {
                headers["Transfer-Encoding"] = ["chunked"];
                SendHead();
            }
            socket.Write(Encoding.UTF8.GetBytes(bytes.Length.ToString("X") + "\r\n"));
            socket.Write(bytes);
            socket.Write([13, 10]);
        }
    }

    public async Task WriteAsync(string text) => await WriteAsync(Encoding.UTF8.GetBytes(text));
    public async Task WriteAsync(byte[] bytes)
    {
        if (!IsClosed)
        {
            if (!HeadSent)
            {
                headers["Transfer-Encoding"] = ["chunked"];
                await SendHeadAsync();
            }
            await socket.WriteAsync(Encoding.UTF8.GetBytes(bytes.Length.ToString("X") + "\r\n"));
            await socket.WriteAsync(bytes);
            await socket.WriteAsync("\r\n"u8.ToArray());
        }
    }


    public static readonly byte[] H2C_UPGRADE = "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n"u8.ToArray();
    public static readonly byte[] WS_UPGRADE = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "u8.ToArray();

    public WebSocket WebSocket()
    {
        if (client.Headers.TryGetValue("sec-websocket-key", out List<string>? kl))
        {
            var ckey = kl[0];
            byte[] ukey = [.. Encoding.UTF8.GetBytes(ckey), .. Http.WebSocket.WebSocket.MAGIC];
            using var sha1 = SHA1.Create();
            byte[] hash = sha1.ComputeHash(ukey);
            var key = Encoding.UTF8.GetBytes(Convert.ToBase64String(hash));
            byte[] res = [.. WS_UPGRADE, .. key, 13, 10, 13, 10];
            socket.Write(res);
            return new WebSocket(socket);
        }
        else
        {
            throw new Http1Exception.WebSocketNotSupported("no websocket key");
        }
    }
    public async Task<WebSocket> WebSocketAsync()
    {
        if (client.Headers.TryGetValue("sec-websocket-key", out List<string>? kl))
        {
            var ckey = kl[0];
            byte[] ukey = [.. Encoding.UTF8.GetBytes(ckey), .. Http.WebSocket.WebSocket.MAGIC];
            using var sha1 = SHA1.Create();
            using var stream = new MemoryStream(ukey);
            byte[] hash = await sha1.ComputeHashAsync(stream);
            var key = Encoding.UTF8.GetBytes(Convert.ToBase64String(hash));
            byte[] res = [.. WS_UPGRADE, .. key, 13, 10, 13, 10];
            await socket.WriteAsync(res);
            return new WebSocket(socket);
        }
        else
        {
            throw new Http1Exception.WebSocketNotSupported("no websocket key");
        }
    }

    public Http2Session H2C()
    {
        Http2Settings settings;
        if (client.Headers.TryGetValue("http2-settings", out List<string>? sl))
        {
            var ssett = sl[0];
            byte[] sb = Convert.FromBase64String(ssett)!;
            settings = Http2Settings.Parse(sb) ?? Http2Settings.Default();
        }
        else
        {
            settings = Http2Settings.Default();
        }

        socket.Write(H2C_UPGRADE);
        var conn = new Http2Session(socket, settings);
        return conn;
    }
    public async Task<Http2Session> H2CAsync()
    {
        Http2Settings settings;
        if (client.Headers.TryGetValue("http2-settings", out List<string>? sl))
        {
            var ssett = sl[0];
            byte[] sb = Convert.FromBase64String(ssett)!;
            settings = Http2Settings.Parse(sb) ?? Http2Settings.Default();
        }
        else
        {
            settings = Http2Settings.Default();
        }
        
        await socket.WriteAsync(H2C_UPGRADE);
        var conn = new Http2Session(socket, settings);

        Http2Status stream = new(settings.initial_window_size ?? 16384, 1, true, true, false, false);

        stream.headers.Add((":authority"u8.ToArray(), Encoding.UTF8.GetBytes(client.Host)));
        stream.headers.Add((":method"u8.ToArray(), Encoding.UTF8.GetBytes(client.Method)));
        stream.headers.Add((":path"u8.ToArray(), Encoding.UTF8.GetBytes(client.Path)));

        foreach (var (h, vs) in client.Headers) foreach (var v in vs) stream.headers.Add((Encoding.UTF8.GetBytes(h), Encoding.UTF8.GetBytes(v)));

        conn.streams[1] = stream;

        return conn;
    }
}