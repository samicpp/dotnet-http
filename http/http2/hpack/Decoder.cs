namespace Samicpp.Http.Http2.Hpack;

public class Decoder(int headerTableSize)
{
    public int TableSize { get; set; } = headerTableSize;
}