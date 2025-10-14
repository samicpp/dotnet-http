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

interface IAsyncSocket
{

}
interface ISyncSocket
{

}
