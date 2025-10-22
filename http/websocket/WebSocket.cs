namespace Samicpp.Http.WebSocket;

using System.Text;
using System.Threading.Tasks;
using Samicpp.Http;

public class WebSocket(IDualSocket socket) : IDisposable, IAsyncDisposable
{
    protected readonly IDualSocket socket = socket;
    public static readonly byte[] MAGIC = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"u8.ToArray();

    public void Dispose() => socket.Dispose();
    public async ValueTask DisposeAsync() => await socket.DisposeAsync();

    public List<WebSocketFrame> Incoming()
    {
        var buff = socket.ReadAll();
        var framesb = WebSocketFrame.Split([.. buff]);
        List<WebSocketFrame> frames = [];

        try
        {
            foreach (var fb in framesb) frames.Add(WebSocketFrame.Parse(fb));
        }
        catch (Exception)
        {
            // throw e;
        }

        return frames;
    }
    public async Task<List<WebSocketFrame>> IncomingAsync()
    {
        var buff = await socket.ReadAllAsync();
        var framesb = WebSocketFrame.Split([.. buff]);
        List<WebSocketFrame> frames = [];

        try
        {
            foreach (var fb in framesb) frames.Add(WebSocketFrame.Parse(fb));
        }
        catch (Exception)
        {
            // throw e;
        }

        return frames;
    }

    public void SendText(string payload) => SendText(Encoding.UTF8.GetBytes(payload));
    public void SendText(byte[] payload) => socket.Write(WebSocketFrame.Create(true, 1, payload));
    public async Task SendTextAsync(string payload) => await SendTextAsync(Encoding.UTF8.GetBytes(payload));
    public async Task SendTextAsync(byte[] payload) => await socket.WriteAsync(WebSocketFrame.Create(true, 1, payload));

    public void SendBinary(string payload) => SendBinary(Encoding.UTF8.GetBytes(payload));
    public void SendBinary(byte[] payload) => socket.Write(WebSocketFrame.Create(true, 2, payload));
    public async Task SendBinaryAsync(string payload) => await SendBinaryAsync(Encoding.UTF8.GetBytes(payload));
    public async Task SendBinaryAsync(byte[] payload) => await socket.WriteAsync(WebSocketFrame.Create(true, 2, payload));

    public void SendPing(string payload) => SendPing(Encoding.UTF8.GetBytes(payload));
    public void SendPing(byte[] payload) => socket.Write(WebSocketFrame.Create(true, 9, payload));
    public async Task SendPingAsync(string payload) => await SendPingAsync(Encoding.UTF8.GetBytes(payload));
    public async Task SendPingAsync(byte[] payload) => await socket.WriteAsync(WebSocketFrame.Create(true, 9, payload));

    public void SendPong(string payload) => SendPong(Encoding.UTF8.GetBytes(payload));
    public void SendPong(byte[] payload) => socket.Write(WebSocketFrame.Create(true, 10, payload));
    public async Task SendPongAsync(string payload) => await SendPongAsync(Encoding.UTF8.GetBytes(payload));
    public async Task SendPongAsync(byte[] payload) => await socket.WriteAsync(WebSocketFrame.Create(true, 10, payload));

    public void SendCloseConnection(string payload) => SendCloseConnection(Encoding.UTF8.GetBytes(payload));
    public void SendCloseConnection(byte[] payload) => socket.Write(WebSocketFrame.Create(true, 8, payload));
    public async Task SendCloseConnectionAsync(string payload) => await SendCloseConnectionAsync(Encoding.UTF8.GetBytes(payload));
    public async Task SendCloseConnectionAsync(byte[] payload) => await socket.WriteAsync(WebSocketFrame.Create(true, 8, payload));
}
