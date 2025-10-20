namespace Samicpp.Http.Http2.Hpack;

public class Encoder(int headerTableSize)
{
    private readonly (byte[] n, byte[] v)[] staticTable = StaticTable.table;
    private readonly DynamicTable dynamic = new(headerTableSize);
    private readonly Huffman huffman = new();
    public int TableSize { get => dynamic.TableSize; set => dynamic.TableSize = value; }

    
}