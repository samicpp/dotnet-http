namespace Samicpp.Http.Http2.Hpack;


public struct HeaderEntry(byte[] name, byte[] value, bool index = true, bool never = false, bool? useHuff = null)
{
    public bool index = index;
    public bool never = never;
    public bool? useHuffman = useHuff;

    public byte[] name = name;
    public byte[] value = value;
}

public class Encoder(int headerTableSize)
{
    private readonly (byte[] n, byte[] v)[] staticTable = StaticTable.table;
    private readonly DynamicTable dynamic = new(headerTableSize);
    private readonly Huffman huffman = new();
    public int TableSize { get => dynamic.TableSize; set => dynamic.TableSize = value; }

    static void WriteInteger(Stream stream, int value, int prefixBits, int prefixStatic)
    {
        var maxPrefix = (1 << prefixBits) - 1;
        if (value < maxPrefix)
        {
            stream.WriteByte((byte)(prefixStatic | value));
        }
        else
        {
            stream.WriteByte((byte)(prefixStatic | maxPrefix));
            var remainder = value - maxPrefix;
            while (remainder >= 128)
            {
                var b = (remainder & 0x7f) | 0x80;
                stream.WriteByte((byte)b);
                remainder >>= 7;
            }
            stream.WriteByte((byte)remainder);
        }
    }
    void WriteString(Stream stream, byte[] str, bool? useHuff = null)
    {
        var huff = huffman.Encode(str);

        if (useHuff == true || str.Length > huff.Count)
        {
            WriteInteger(stream, huff.Count, 7, 0x80);
            stream.Write(huff.ToArray());
        }
        else
        {
            WriteInteger(stream, str.Length, 7, 0x00);
            stream.Write(str.ToArray());
        }
    }

    void WriteIndexed(Stream stream, int index)
    {
        WriteInteger(stream, index, 7, 0x80);
    }
    void WriteIndexedName(Stream stream, int index, byte[] value, bool? useHuff = null)
    {
        WriteInteger(stream, index, 6, 0x40);
        WriteString(stream, value, useHuff);
    }
    void WriteNewIndexed(Stream stream, byte[] name, byte[] value, bool? useHuff = null)
    {
        WriteInteger(stream, 0, 6, 0x40);
        WriteString(stream, name, useHuff);
        WriteString(stream, value, useHuff);
    }
    void WriteNoIndex(Stream stream, int index, byte[] value, bool? useHuff = null)
    {
        WriteInteger(stream, index, 4, 0x00);
        WriteString(stream, value, useHuff);
    }
    void WriteNewNoIndex(Stream stream, byte[] name, byte[] value, bool? useHuff = null)
    {
        WriteInteger(stream, 0, 4, 0x00);
        WriteString(stream, name, useHuff);
        WriteString(stream, value, useHuff);
    }
    void WriteNeverIndex(Stream stream, int index, byte[] value, bool? useHuff = null)
    {
        WriteInteger(stream, index, 4, 0x10);
        WriteString(stream, value, useHuff);
    }
    void WriteNewNeverIndex(Stream stream, byte[] name, byte[] value, bool? useHuff = null)
    {
        WriteInteger(stream, 0, 4, 0x10);
        WriteString(stream, name, useHuff);
        WriteString(stream, value, useHuff);
    }

    int? FindExactHeader(byte[] name, byte[] value)
    {
        var ssize = staticTable.Length;
        var dsize = dynamic.table.Count;

        for (int i = 1; i < ssize + 1; i++)
        {
            var (h, v) = staticTable[i - 1];
            if (h.AsSpan().SequenceEqual(name) && v.AsSpan().SequenceEqual(value))
            {
                return i;
            }
        }
        for (int i = 1; i < dsize + 1; i++)
        {
            var (h, v) = dynamic.Get(i - 1);
            if (h.AsSpan().SequenceEqual(name) && v.AsSpan().SequenceEqual(value))
            {
                return ssize + i;
            }
        }

        return null;
    }
    int? FindHeader(byte[] name)
    {
        var ssize = staticTable.Length;
        var dsize = dynamic.table.Count;

        for (int i = 1; i < ssize + 1; i++)
        {
            var (h, _) = staticTable[i - 1];
            if (h.AsSpan().SequenceEqual(name))
            {
                return i;
            }
        }
        for (int i = 1; i < dsize + 1; i++)
        {
            var (h, _) = dynamic.Get(i - 1);
            if (h.AsSpan().SequenceEqual(name))
            {
                return ssize + i;
            }
        }

        return null;
    }

    public byte[] Encode((byte[], byte[])[] headers)
    {
        var stream = new MemoryStream();

        foreach (var (name, value) in headers)
        {
            var eindex = FindExactHeader(name, value);
            if (eindex != null)
            {
                WriteIndexed(stream, (int)eindex);
                continue;
            }

            var index = FindHeader(name);

            if (index != null)
            {
                WriteIndexedName(stream, (int)index, value);
                dynamic.AddHeader(name, value);
            }
            else
            {
                WriteNewIndexed(stream, name, value);
                dynamic.AddHeader(name, value);
            }
        }

        return stream.ToArray();
    }
    public byte[] Encode(HeaderEntry[] headers)
    {
        var stream = new MemoryStream();

        foreach (var header in headers)
        {
            var eindex = FindExactHeader(header.name, header.value);
            if (eindex != null)
            {
                WriteIndexed(stream, (int)eindex);
                continue;
            }

            var index = FindHeader(header.name);

            if (index != null && header.index)
            {
                WriteIndexedName(stream, (int)index, header.value, header.useHuffman);
                dynamic.AddHeader(header.name, header.value);
            }
            else if (index != null && !header.index)
            {
                if (header.never) WriteNeverIndex(stream, (int)index, header.value, header.useHuffman);
                else WriteNoIndex(stream, (int)index, header.value, header.useHuffman);
            }
            else if (header.index)
            {
                WriteNewIndexed(stream, header.name, header.value, header.useHuffman);
                dynamic.AddHeader(header.name, header.value);
            }
            else
            {
                if (header.never) WriteNewNeverIndex(stream, header.name, header.value, header.useHuffman);
                else WriteNewNoIndex(stream, header.name, header.value, header.useHuffman);
            }
        }

        return stream.ToArray();
    }
}