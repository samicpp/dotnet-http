namespace Samicpp.Http;

public class HttpException(string? message = null, Exception? source = null) : Exception(message)
{
    public readonly Exception? source = source;
    public sealed class ConnectionClosed(string? message) : HttpException(message);
}
interface IHttpSocket
{

}

interface IHttpClient
{

}

interface ISocket
{
    bool CanRead { get; }
    bool CanWrite { get; }
}
interface IAsyncSocket : ISocket
{
    Task FlushAsync();
    Task DisposeAsync();
    Task<int> ReadAsync(Memory<byte> bytes);
    Task<int> ReadAsync(byte[] bytes, int offset, int size);
    Task WriteAsync(Memory<byte> bytes);
    Task WriteAsync(byte[] bytes, int offset, int size);
}
interface ISyncSocket : ISocket
{
    void Flush();
    void Close();
    void Dispose();
    int Read(Span<byte> bytes);
    int Read(byte[] bytes, int offset, int size);
    void Write(Span<byte> bytes);
    void Write(byte[] bytes, int offset, int size);
}
