namespace Samicpp.Http.WebSocket;

public struct WebSocketFrame
{
    public static WebSocketFrame Parse(byte[] bytes)
    {
        return new();
    }
    public static byte[] Create()
    {
        return [];
    }
}
