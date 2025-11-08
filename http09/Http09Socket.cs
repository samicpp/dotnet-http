namespace Samicpp.Http.Http09;

using System.Net;
using System.Text;
using System.Threading.Tasks;


// for educational purposes only
// HTTP/0.9 is useless

public class Http09Client : HttpClient
{
    public Http09Client()
    {
        IsValid = true;
        Host = "about:blank";
        Version = "HTTP/0.9";
        BodyComplete = true;
    }
}

public class Http09Socket(IDualSocket socket, EndPoint? endPoint = null) : IDualHttpSocket
{
    public bool IsHttps { get => socket.IsSecure; }
    protected readonly IDualSocket socket = socket;
    readonly Http09Client client = new();
    public IHttpClient Client { get => client; }
    public bool HeadSent { get; set; } = true;
    public bool IsClosed { get; set; }
    public EndPoint? EndPoint => endPoint;
    // IDualSocket IDualHttpSocket.Conn { get => socket; }
    // IAsyncSocket IAsyncHttpSocket.Conn { get => socket; }
    // ISyncSocket ISyncHttpSocket.Conn { get => socket; }
    // ISocket IHttpSocket.Conn { get => socket; }


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

    public IHttpClient ReadClient()
    {
        if (!client.HeadersComplete)
        {
            var b = socket.ReadUntil([10]);
            var s = Encoding.UTF8.GetString([.. b]);
            var mp = s.Split(" ", 2);

            client.Method = (string?)mp.GetValue(0) ?? client.Method;
            client.Path = (string?)mp.GetValue(1) ?? client.Path;
            client.HeadersComplete = true;
        }

        return client;
    }
    public async Task<IHttpClient> ReadClientAsync()
    {
        if (!client.HeadersComplete)
        {
            var b = await socket.ReadUntilAsync([10]);
            var s = Encoding.UTF8.GetString([.. b]);
            var mp = s.Split(" ", 2);
            
            client.Method = (string?)mp.GetValue(0) ?? client.Method;
            client.Path = (string?)mp.GetValue(1) ?? client.Path;
            client.HeadersComplete = true;
        }

        return client;
    }

    public void Close() => Close([]);
    public void Close(string text) => Close(Encoding.UTF8.GetBytes(text));
    public void Close(byte[] data)
    {
        if (!IsClosed)
        {
            socket.Write(data);
            IsClosed = true;
        }
    }

    public async Task CloseAsync() => await CloseAsync([]);
    public async Task CloseAsync(string text) => await CloseAsync(Encoding.UTF8.GetBytes(text));
    public async Task CloseAsync(byte[] data)
    {
        if (!IsClosed)
        {
            await socket.WriteAsync(data);
            IsClosed = true;
        }
    }


    public void Write() => Write([]);
    public void Write(string text) => Write(Encoding.UTF8.GetBytes(text));
    public void Write(byte[] data)
    {
        if (!IsClosed)
        {
            socket.Write(data);
        }
    }

    public async Task WriteAsync() => await WriteAsync([]);
    public async Task WriteAsync(string text) => await WriteAsync(Encoding.UTF8.GetBytes(text));
    public async Task WriteAsync(byte[] data)
    {
        if (!IsClosed)
        {
            await socket.WriteAsync(data);
        }
    }

    public int Status { get; set; } = 200;
    public string StatusMessage { get; set; } = "OK";
    public CompressionType Compression { get; set; } = CompressionType.None;

    public void SetHeader(string name, string value) { }
    public void AddHeader(string name, string value) { }
    public List<string> DelHeader(string name) => [];

    public WebSocket.WebSocket WebSocket()
    {
        return new(socket);
    }
    public Task<WebSocket.WebSocket> WebSocketAsync()
    {
        return Task.FromResult(new WebSocket.WebSocket(socket));
    }
}