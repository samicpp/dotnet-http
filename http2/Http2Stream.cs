namespace Samicpp.Http.Http2;

using System.Net;
using System.Text;
using System.Threading.Tasks;
using Samicpp.Http;
using Samicpp.Http.Http2.Hpack;

public class Http2Stream(int streamID, Http2Session conn) : IDualHttpSocket
{
    public readonly Http2Session conn = conn;
    public readonly int streamID = streamID;
    public readonly List<string> cache = [":status", "content-type"];

    public bool IsHttps { get => conn.IsSecure; }

    readonly HttpClient client = new();
    public IHttpClient Client { get => client; }
    public bool IsClosed { get; set; }
    public bool HeadSent { get; set; }
    public EndPoint? EndPoint => conn.EndPoint;
    // IDualSocket IDualHttpSocket.Conn { get => conn.Conn; }
    // IAsyncSocket IAsyncHttpSocket.Conn { get => conn.Conn; }
    // ISyncSocket ISyncHttpSocket.Conn { get => conn.Conn; }
    // ISocket IHttpSocket.Conn { get => conn.Conn; }

    public int Status { get; set; } = 200;
    public string StatusMessage { get; set; } = "OK"; // doesnt matter
    public Compression Compression { 
        get; set
        {
            field = value;
            switch (value)
            {
                case Compression.None: headers.Remove("content-encoding"); break;
                case Compression.Gzip: headers["content-encoding"] = ["gzip"]; break;
                case Compression.Deflate: headers["content-encoding"] = ["deflate"]; break;
                case Compression.Brotli: headers["content-encoding"] = ["br"]; break;
            }
        } 
    } = Compression.None;

    private readonly Dictionary<string, List<string>> headers = [];
    public void SetHeader(string name, string value) => headers[name.ToLower()] = [value];
    public void AddHeader(string name, string value)
    {
        if (headers.TryGetValue(name.ToLower(), out List<string>? ls)) ls.Add(value);
        else headers[name.ToLower()] = [value];
    }
    public List<string> DelHeader(string name)
    {
        var head = headers[name.ToLower()];
        if (head == null) return [];
        headers.Remove(name);
        return head;
    }

    public IHttpClient ReadClient()
    {
        var stream = conn.GetStream(streamID) ?? throw new Http2Exception.StreamDoesntExist("huh?");

        client.Body = stream.body;
        client.HeadersComplete = stream.end_headers;
        client.BodyComplete = stream.end_stream;
        client.Version = "HTTP/2";

        client.Headers.Clear();
        foreach (var (hb, vb) in stream.headers)
        {
            var header = Encoding.UTF8.GetString(hb);
            var value = Encoding.UTF8.GetString(vb);

            if (header == ":path") client.Path = value;
            else if (header == ":method") client.Method = value;
            else if (header == ":authority") client.Host = value;
            else if (client.Headers.TryGetValue(header, out List<string>? ls)) ls.Add(value);
            else client.Headers[header] = [value];
        }

        return client;
    }
    public async Task<IHttpClient> ReadClientAsync()
    {
        var stream = await conn.GetStreamAsync(streamID) ?? throw new Http2Exception.StreamDoesntExist("huh?");

        client.Body = stream.body;
        client.HeadersComplete = stream.end_headers;
        client.BodyComplete = stream.end_stream;
        client.Version = "HTTP/2";

        client.Headers.Clear();
        foreach (var (hb, vb) in stream.headers)
        {
            var header = Encoding.UTF8.GetString(hb);
            var value = Encoding.UTF8.GetString(vb);

            if (header == ":path") client.Path = value;
            else if (header == ":method") client.Method = value;
            else if (header == ":authority") client.Host = value;
            else if (client.Headers.TryGetValue(header, out List<string>? ls)) ls.Add(value);
            else client.Headers[header] = [value];
        }

        return client;
    }

    void SendHead(bool end = false)
    {
        if (!HeadSent && !IsClosed)
        {
            List<HeaderEntry> head = [
                new(":status"u8.ToArray(), Encoding.UTF8.GetBytes(Status.ToString())),
            ];
            foreach (var (header, vs) in headers) foreach (var value in vs) head.Add(new(Encoding.UTF8.GetBytes(header), Encoding.UTF8.GetBytes(value), cache.Contains(header)));
            conn.SendHeaders(streamID, end, head.ToArray());
            HeadSent = true;
            IsClosed = end;
        }
    }
    async Task SendHeadAsync(bool end = false)
    {
        if (!HeadSent && !IsClosed)
        {
            List<HeaderEntry> head = [
                new(":status"u8.ToArray(), Encoding.UTF8.GetBytes(Status.ToString())),
            ];
            foreach (var (header, vs) in headers) foreach (var value in vs) head.Add(new(Encoding.UTF8.GetBytes(header), Encoding.UTF8.GetBytes(value), cache.Contains(header)));
            await conn.SendHeadersAsync(streamID, end, head.ToArray());
            HeadSent = true;
            IsClosed = end;
        }
    }

    public void Close() => SendHead(true);
    public void Close(string data) => Close(Encoding.UTF8.GetBytes(data));
    public void Close(byte[] data)
    {
        if (!IsClosed && !HeadSent)
        {
            var compressed = Compressor.Compress(Compression, data);
            headers["content-length"] = [compressed.Length.ToString()];
            SendHead(false);
            conn.SendData(streamID, true, compressed);
            IsClosed = true;
        }
        else if (!IsClosed)
        {
            conn.SendData(streamID, true, data);
            IsClosed = true;
        }
    }

    public async Task CloseAsync() => await SendHeadAsync(true);
    public async Task CloseAsync(string data) => await CloseAsync(Encoding.UTF8.GetBytes(data));
    public async Task CloseAsync(byte[] data)
    {
        if (!IsClosed && !HeadSent)
        {
            var compressed = await Compressor.CompressAsync(Compression, data);
            headers["content-length"] = [compressed.Length.ToString()];
            SendHead(false);
            await conn.SendDataAsync(streamID, true, compressed);
            IsClosed = true;
        }
        else if (!IsClosed)
        {
            await conn.SendDataAsync(streamID, true, data);
            IsClosed = true;
        }
    }

    public void Write(string data) => Write(Encoding.UTF8.GetBytes(data));
    public void Write(byte[] data)
    {
        if (!IsClosed)
        {
            if (!HeadSent) SendHead();
            conn.SendData(streamID, false, data);
        }
    }

    public async Task WriteAsync(string data) => await WriteAsync(Encoding.UTF8.GetBytes(data));
    public async Task WriteAsync(byte[] data)
    {
        if (!IsClosed)
        {
            if (!HeadSent) await SendHeadAsync();
            await conn.SendDataAsync(streamID, false, data);
        }
    }

    public WebSocket.WebSocket WebSocket()
    {
        conn.SendRstStream(streamID, 0xd);
        throw new NotImplementedException("HTTP/1.1 only for now");
    }
    public async Task<WebSocket.WebSocket> WebSocketAsync()
    {
        await conn.SendRstStreamAsync(streamID, 0xd);
        throw new NotImplementedException("HTTP/1.1 only for now");
    }

    public void Dispose()
    {
        if (!IsClosed) conn.SendRstStream(streamID, 0x0);
    }
    public async ValueTask DisposeAsync()
    {
        if (!IsClosed) await conn.SendRstStreamAsync(streamID, 0x0);
    }
}