namespace Samicpp.Http.Http2;

public struct Http2Frame
{
    public static Http2Frame Parse(byte[] bytes)
    {
        return new();
    }

    public static byte[] Create()
    {
        return [];
    }
    
    public readonly byte[] ToBytes()
    {
        var _ = this;
        return [];
    }
}

public struct Http2Settings
{
    public static Http2Settings Parse(byte[] bytes)
    {
        return new();
    }
    public readonly byte[] ToBytes()
    {
        var _ = this;
        return [];
    }
}
