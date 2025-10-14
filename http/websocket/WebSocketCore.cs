namespace Samicpp.Http.WebSocket;

public readonly struct WebSocketFrame
{
    public static WebSocketFrame Parse(Span<byte> bytes)
    {
        return new();
    }
    public static List<byte> Create()
    {
        return [];
    }
}
