namespace Samicpp.Http.Debug;

using Samicpp.Http;


// TODO: add logging on method invocation
public class FakeHttpSocket(HttpClient client) : IDualHttpSocket
{
    public bool IsHttps { get => true; }

    readonly HttpClient _client = client;
    readonly HttpClient client = new();
    public IHttpClient Client { get => client; }
    public bool IsClosed { get; set; }
    public bool HeadSent { get; set; }

    public int Status { get; set; } = 200;
    public string StatusMessage { get; set; } = "OK";
    public Compression Compression { get; set; } = Compression.None;

    private readonly Dictionary<string, List<string>> headers = new() { { "Connection", ["close"] } };
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

    public async Task<IHttpClient> ReadClientAsync() => ReadClient();
    public IHttpClient ReadClient()
    {
        // client.Body = "HttpClient.Body"u8.ToArray().ToList();
        // client.HeadersComplete = true;
        // client.BodyComplete = true;
        // client.Version = "FakeSocket";

        // client.Headers = new()
        // {
        //     { "Host", [ "about:blank" ] }
        // };

        client.Body = _client.Body;
        client.HeadersComplete = true;
        client.BodyComplete = true;
        client.Version = _client.Version;

        client.Headers = [];
        foreach (var (h, v) in _client.Headers) client.Headers[h] = v;

        return client;
    }

    void SendHead() { }
    async Task SendHeadAsync() { }

    public void Close(string data) { }
    public void Close(byte[] data) { }

    public async Task CloseAsync(string data) { }
    public async Task CloseAsync(byte[] data) { }

    public void Write(string data) { }
    public void Write(byte[] data) { }

    public async Task WriteAsync(string data) { }
    public async Task WriteAsync(byte[] data) { }

    public WebSocket.WebSocket WebSocket()
    {
        throw new NotImplementedException("Cant start websocket in fake socket");
    }
    public async Task<WebSocket.WebSocket> WebSocketAsync()
    {
        throw new NotImplementedException("Cant start websocket in fake socket");
    }

    public void Dispose()
    {
        //
    }
    public async ValueTask DisposeAsync()
    {
        //
    }
}