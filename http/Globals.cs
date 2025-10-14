namespace Samicpp.Http;

public class HttpException : Exception
{
    public readonly string message;
    public HttpException(string? message = null)
    {
        this.message = message ?? "";
    }
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
