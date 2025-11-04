namespace Samicpp.Http.Debug;

using System.Net;
using System.Text;
using Samicpp.Http;


// TODO: add more options
public class FakeHttpSocket(HttpClient client) : IDualHttpSocket
{
    public bool IsHttps { get => true; }

    readonly HttpClient _client = client;
    readonly HttpClient client = new();
    public IHttpClient Client { get => client; }
    public bool IsClosed { get; set; }
    public bool HeadSent { get; set; }

    // IDualSocket IDualHttpSocket.Conn { get => throw new Exception("cannot access connection socket in fake socket"); }
    // IAsyncSocket IAsyncHttpSocket.Conn { get => throw new Exception("cannot access connection socket in fake socket"); }
    // ISyncSocket ISyncHttpSocket.Conn { get => throw new Exception("cannot access connection socket in fake socket"); }
    // ISocket IHttpSocket.Conn { get => throw new Exception("cannot access connection socket in fake socket"); }
    public EndPoint? EndPoint => null;

    public int Status { get; set { field = value; Console.WriteLine($"set status to {value}"); } } = 200;
    public string StatusMessage { get; set { field = value; Console.WriteLine($"set status message to {value}"); } } = "OK";
    public CompressionType Compression { get; set { field = value; Console.WriteLine($"set compression to {value}"); } } = CompressionType.None;

    private readonly Dictionary<string, List<string>> headers = new() { { "Connection", ["close"] } };
    public void SetHeader(string name, string value) {
        if (!HeadSent)
        {
            Console.WriteLine($"[ ] set header {name}: {value}");
            headers[name] = [value];
        }
        else
        {
            Console.WriteLine($"\x1b[31m[X]\x1b[0m setting header {name} after head sent");
        }
    }
    public void AddHeader(string name, string value)
    {
        if (!HeadSent)
        {
            Console.WriteLine($"[ ] adding {value} to header {name}");
            if (headers.TryGetValue(name, out List<string>? ls)) ls.Add(value);
            else headers[name] = [value];
        }
        else
        {
            Console.WriteLine($"\x1b[31m[X]\x1b[0m adding to header {name} after head sent");
        }
    }
    public List<string> DelHeader(string name)
    {
        if (!HeadSent)
        {
            Console.WriteLine($"\x1b[33m[~]\x1b[0m removing header {name}");
            var head = headers[name];
            if (head == null)
            {
                Console.WriteLine($"\x1b[31m[X]\x1b[0m attempted to remove nonexistent header {name}");
            }
            else
            {
                headers.Remove(name);
            }
            return head ?? [];
        }
        else
        {
            Console.WriteLine($"\x1b[31m[X]\x1b[0m deleting header {name} after head sent");
            return [];
        }
        
    }

    public Task<IHttpClient> ReadClientAsync() => Task.FromResult(ReadClient());
    public IHttpClient ReadClient()
    {
        if (!client.HeadersComplete && !client.BodyComplete) Console.WriteLine("\x1b[32m[*]\x1b[0m reading client");
        else if (!client.BodyComplete) Console.WriteLine("\x1b[32m[*]\x1b[0m reading client again for complete body");
        else Console.WriteLine("\x1b[31m[X]\x1b[0m reading when client already complete");

        // client.Body = "HttpClient.Body"u8.ToArray().ToList();
        // client.HeadersComplete = true;
        // client.BodyComplete = true;
        // client.Version = "FakeSocket";

        // client.Headers = new()
        // {
        //     { "Host", [ "about:blank" ] }
        // };

        client.Body = _client.Body;

        if (client.HeadersComplete) client.BodyComplete = true;
        else client.BodyComplete = false;

        client.HeadersComplete = true;
        client.Version = _client.Version;

        client.Headers = [];
        foreach (var (h, v) in _client.Headers) client.Headers[h] = v;

        return client;
    }

    // void SendHead() { }
    // async Task SendHeadAsync() { }

    public void Close() => Close([]);
    public void Close(string data) => Close(Encoding.UTF8.GetBytes(data));
    public void Close(byte[] data)
    {
        if (!client.BodyComplete) Console.WriteLine("\x1b[33m[~]\x1b[0m closing connection before fully read client");
        if (!IsClosed) 
        {
            Console.WriteLine($"\x1b[32m[*]\x1b[0m closing connection with {data.Length} bytes");
            HeadSent = true;
            IsClosed = true;
        }
        else
        {
            Console.WriteLine("\x1b[31m[X]\x1b[0m attempted closing connection when already closed");
        }
    }

    public Task CloseAsync() { Close([]); return Task.CompletedTask; }
    public Task CloseAsync(string data) { Close(data); return Task.CompletedTask; }
    public Task CloseAsync(byte[] data) { Close(data); return Task.CompletedTask; }

    public void Write(string data) => Write(Encoding.UTF8.GetBytes(data));
    public void Write(byte[] data)
    {
        if (!client.BodyComplete) Console.WriteLine("\x1b[33m[~]\x1b[0m writing to connection before fully read client");
        if (!IsClosed) 
        {
            Console.WriteLine($"\x1b[32m[*]\x1b[0m writing to connection with {data.Length} bytes");
            HeadSent = true;
        }
        else
        {
            Console.WriteLine("\x1b[31m[X]\x1b[0m attempted writing to connection when already closed");
        }
    }

    public Task WriteAsync(string data) { Write(data); return Task.CompletedTask; }
    public Task WriteAsync(byte[] data) { Write(data); return Task.CompletedTask; }

    public WebSocket.WebSocket WebSocket()
    {
        throw new NotImplementedException("Cant start websocket in fake socket");
    }
    public Task<WebSocket.WebSocket> WebSocketAsync()
    {
        throw new NotImplementedException("Cant start websocket in fake socket");
    }

    public void Dispose()
    {
        if (!IsClosed) Console.WriteLine("\x1b[31m[X]\x1b[0m socket disposed before closing connection");
        else Console.WriteLine($"\x1b[32m[*]\x1b[0m disposed fake socket");
        GC.SuppressFinalize(this);
    }
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    
    ~FakeHttpSocket()
    {
        Console.WriteLine("\x1b[31m[X]\x1b[0m fake socket not disposed");
        if (!IsClosed) Console.WriteLine("\x1b[31m[X]\x1b[0m socket dropped before closing connection");
    }
}