namespace Samicpp.Http.Http2.Hpack;

public class Encoder(int headerTableSize)
{
    public int TableSize { get; set; } = headerTableSize;
}