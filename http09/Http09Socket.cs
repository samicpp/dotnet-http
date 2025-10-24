namespace Samicpp.Http.Http09;

using System.Text;
using System.Threading.Tasks;


// for educational purposes only
// HTTP/0.9 is useless

public readonly struct Http09Client(string method, string path)
{
    public readonly string method = method;
    public readonly string path = path;
    public readonly string version = "HTTP/0.9";
}

public class Http09Socket(IDualSocket socket) : IDisposable, IAsyncDisposable
{
    public bool IsHttps { get => socket.IsSecure; }
    protected readonly IDualSocket socket = socket;
    Http09Client? client = null;
    public Http09Client? Client { get => client; }
    bool closed = false;

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

    public Http09Client ReadClient()
    {
        if (client == null)
        {
            var b = socket.ReadUntil([10]);
            var s = Encoding.UTF8.GetString([.. b]);

            var mp = s.Split(" ", 2);
            client = new(mp[0], mp[1]);
        }

        return (Http09Client)client;
    }
    public async Task<Http09Client> ReadClientAsync()
    {
        if (client == null)
        {
            var b = await socket.ReadUntilAsync([10]);
            var s = Encoding.UTF8.GetString([.. b]);

            var mp = s.Split(" ", 2);
            client = new(mp[0], mp[1]);
        }

        return (Http09Client)client;
    }

    public void Close(string text) => Close(Encoding.UTF8.GetBytes(text));
    public void Close(byte[] data)
    {
        if (!closed)
        {
            socket.Write(data);
            closed = true;
        }
    }

    public async Task CloseAsync(string text) => await CloseAsync(Encoding.UTF8.GetBytes(text));
    public async Task CloseAsync(byte[] data)
    {
        if (!closed)
        {
            await socket.WriteAsync(data);
            closed = true;
        }
    }
}