namespace Samicpp.Http.Http2.Hpack;

public class Decoder(int headerTableSize)
{
    private readonly (byte[] n, byte[] v)[] staticTable = StaticTable.table;
    private readonly DynamicTable dynamic = new(headerTableSize);
    private readonly Huffman huffman = new();
    public int TableSize { get => dynamic.TableSize; set => dynamic.TableSize = value; }

    public (byte[] name, byte[] value) GetHeader(int index)
    {
        var ssize = staticTable.Length;
        var dsize = dynamic.table.Count;

        if (1 <= index && index <= ssize)
        {
            return staticTable[index - 1];
        }
        else if (ssize < index && index <= ssize + dsize)
        {
            return dynamic.Get(index - 1 - ssize);
        }
        else
        {
            throw new ArgumentOutOfRangeException();
        }
    }

    static int ReadInteger(byte[] data, int[] posRef, int prefixBits)
    {
        var pos = posRef[0];
        if (pos >= data.Length) throw new IndexOutOfRangeException();
        var first = data[pos] & 0xFF;
        var prefixMask = (1 << prefixBits) - 1;
        var value = first & prefixMask;
        pos++;
        if (value == prefixMask) 
        {
            var shift = 0;
            int b;
            do
            {
                if (pos >= data.Length) throw new IndexOutOfRangeException();
                b = data[pos] & 0xFF;
                pos++;
                value += (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
        }

        posRef[0] = pos;
        return value;
    }


    byte[] ReadString(byte[] data, int[] posRef)
    {
        var pos = posRef[0];
        if (pos >= data.Length) throw new IndexOutOfRangeException();

        var first = data[pos] & 0xFF;
        var huffmanFlag = (first & 0x80) != 0;
        var length = ReadInteger(data, posRef, 7);
        var newPos = posRef[0];

        if (newPos + length > data.Length) throw new IndexOutOfRangeException();

        var bytes = data[newPos..(newPos + length)];
        posRef[0] = newPos + length;

        byte[] raw;
        if (huffmanFlag) raw = [.. huffman.Decode(bytes)];
        else raw = bytes;

        return raw;
    }
    
    public List<(byte[],byte[])> Decode(byte[] block){
        List<(byte[], byte[])> dec = [];
        int[] posRef = [0];

        while (posRef[0] < block.Length) 
        {
            var b0 = block[posRef[0]] & 0xFF;


            if ((b0 & 0x80) != 0) {
                var index = ReadInteger(block, posRef, 7);
                dec.Add(GetHeader(index));
            }
            else if ((b0 & 0xC0) == 0x40) {
                var nameIndex = ReadInteger(block, posRef, 6);
                byte[] name;
                if (nameIndex == 0) name = ReadString(block, posRef);
                else name = GetHeader(nameIndex).name;
                var value = ReadString(block, posRef);
                dynamic.AddHeader(name, value);
                dec.Add((name, value));
            }
            else if ((b0 & 0xE0) == 0x20)
            {
                var newSize = ReadInteger(block, posRef, 5);
                TableSize = newSize;
            }
            else if ((b0 & 0xF0) == 0x00)
            {
                var nameIndex = ReadInteger(block, posRef, 4);
                byte[] name;
                if (nameIndex == 0) name = ReadString(block, posRef);
                else name = GetHeader(nameIndex).name;
                var value = ReadString(block, posRef);
                dec.Add((name, value));
            }
            else if ((b0 & 0xF0) == 0x10) 
            {
                var nameIndex = ReadInteger(block, posRef, 4);
                byte[] name;
                if (nameIndex == 0) name = ReadString(block, posRef);
                else name = GetHeader(nameIndex).name;
                var value = ReadString(block, posRef);
                dec.Add((name, value));
            }

            else{
                throw new Exception($"Unrecognized header field representation at pos {posRef[0]} (byte=0x{b0.ToString("X")})");
            }
            
        }

        return dec;
    }
}