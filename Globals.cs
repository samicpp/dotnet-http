namespace Samicpp.Http;

public interface IHttpSocket
{
    IHttpClient Client { get; }
    bool IsClosed { get; }
    bool IsHttps { get; }
    bool HeadSent { get; }
    int Status { get; set; }
    string StatusMessage { get; set; }

    void SetHeader(string name, string value);
    void AddHeader(string name, string value);
    List<string> DelHeader(string name);
    Compression Compression { get; set; }
}
public interface ISyncHttpSocket : IHttpSocket, IDisposable
{
    IHttpClient ReadClient();
    void Close(string text);
    void Close(byte[] bytes);
    void Write(string text);
    void Write(byte[] bytes);
    WebSocket.WebSocket WebSocket();
}
public interface IAsyncHttpSocket: IHttpSocket, IAsyncDisposable
{
    Task<IHttpClient> ReadClientAsync();
    Task CloseAsync(string text);
    Task CloseAsync(byte[] bytes);
    Task WriteAsync(string text);
    Task WriteAsync(byte[] bytes);
    Task<WebSocket.WebSocket> WebSocketAsync();
}
public interface IDualHttpSocket : IAsyncHttpSocket, ISyncHttpSocket { }

public interface IHttpClient
{
    public Dictionary<string, List<string>> Headers { get; }
    public string Host { get; }
    public string Method { get; }
    public string Path { get; }
    public string Version { get; }
    public List<byte> Body { get; }
    public bool HeadersComplete { get; }
    public bool BodyComplete { get; }
}

public interface ISocket
{
    bool CanRead { get; }
    bool CanWrite { get; }
    bool IsSecure { get; }

}
public interface IAsyncSocket : ISocket, IAsyncDisposable
{
    Task FlushAsync();
    // ValueTask DisposeAsync();
    Task<int> ReadAsync(Memory<byte> bytes);
    Task<int> ReadAsync(byte[] bytes, int offset, int size);
    Task WriteAsync(Memory<byte> bytes);
    Task WriteAsync(byte[] bytes, int offset, int size);

    Task<List<byte>> ReadAllAsync();
    Task<byte[]> ReadCertainAsync(int size);
    Task<List<byte>> ReadUntilAsync(byte[] stop);
    Task<List<byte>> ReadUntilAsync(params byte[][] stop);
}
public interface ISyncSocket : ISocket, IDisposable
{
    void Flush();
    void Close();
    // void Dispose();
    int Read(Span<byte> bytes);
    int Read(byte[] bytes, int offset, int size);
    void Write(Span<byte> bytes);
    void Write(byte[] bytes, int offset, int size);

    public List<byte> ReadAll();
    byte[] ReadCertain(int size);
    List<byte> ReadUntil(byte[] stop);
    List<byte> ReadUntil(params byte[][] stop);
}
public interface IDualSocket : IAsyncSocket, ISyncSocket { }
