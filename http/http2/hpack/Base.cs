namespace Samicpp.Http.Http2.Hpack;

using System.Collections.ObjectModel;
// using System.Linq;


// https://httpwg.org/specs/rfc7541.html

// Appendix A #static.table.definition
public static class StaticTable
{
    public static readonly (byte[] name, byte[] value)[] table =
    [
        (":authority"u8.ToArray(), []),
        (":method"u8.ToArray(), "GET"u8.ToArray()),
        (":method"u8.ToArray(), "POST"u8.ToArray()),
        (":path"u8.ToArray(), "/"u8.ToArray()),
        (":path"u8.ToArray(), "/index.html"u8.ToArray()),
        (":scheme"u8.ToArray(), "http"u8.ToArray()),
        (":scheme"u8.ToArray(), "https"u8.ToArray()),
        (":status"u8.ToArray(), "200"u8.ToArray()),
        (":status"u8.ToArray(), "204"u8.ToArray()),
        (":status"u8.ToArray(), "206"u8.ToArray()),
        (":status"u8.ToArray(), "304"u8.ToArray()),
        (":status"u8.ToArray(), "400"u8.ToArray()),
        (":status"u8.ToArray(), "404"u8.ToArray()),
        (":status"u8.ToArray(), "500"u8.ToArray()),
        ("accept-"u8.ToArray(), []),
        ("accept-encoding"u8.ToArray(), "gzip, deflate"u8.ToArray()),
        ("accept-language"u8.ToArray(), []),
        ("accept-ranges"u8.ToArray(), []),
        ("accept"u8.ToArray(), []),
        ("access-control-allow-origin"u8.ToArray(), []),
        ("age"u8.ToArray(), []),
        ("allow"u8.ToArray(), []),
        ("authorization"u8.ToArray(), []),
        ("cache-control"u8.ToArray(), []),
        ("content-disposition"u8.ToArray(), []),
        ("content-encoding"u8.ToArray(), []),
        ("content-language"u8.ToArray(), []),
        ("content-length"u8.ToArray(), []),
        ("content-location"u8.ToArray(), []),
        ("content-range"u8.ToArray(), []),
        ("content-type"u8.ToArray(), []),
        ("cookie"u8.ToArray(), []),
        ("date"u8.ToArray(), []),
        ("etag"u8.ToArray(), []),
        ("expect"u8.ToArray(), []),
        ("expires"u8.ToArray(), []),
        ("from"u8.ToArray(), []),
        ("host"u8.ToArray(), []),
        ("if-match"u8.ToArray(), []),
        ("if-modified-since"u8.ToArray(), []),
        ("if-none-match"u8.ToArray(), []),
        ("if-range"u8.ToArray(), []),
        ("if-unmodified-since"u8.ToArray(), []),
        ("last-modified"u8.ToArray(), []),
        ("link"u8.ToArray(), []),
        ("location"u8.ToArray(), []),
        ("max-forwards"u8.ToArray(), []),
        ("proxy-authenticate"u8.ToArray(), []),
        ("proxy-authorization"u8.ToArray(), []),
        ("range"u8.ToArray(), []),
        ("referer"u8.ToArray(), []),
        ("refresh"u8.ToArray(), []),
        ("retry-after"u8.ToArray(), []),
        ("server"u8.ToArray(), []),
        ("set-cookie"u8.ToArray(), []),
        ("strict-transport-security"u8.ToArray(), []),
        ("transfer-encoding"u8.ToArray(), []),
        ("user-agent"u8.ToArray(), []),
        ("vary"u8.ToArray(), []),
        ("via"u8.ToArray(), []),
        ("www-authenticate"u8.ToArray(), []),
    ];
}

public class DynamicTable(int headerTableSize)
{
    public readonly LinkedList<(byte[] name, byte[] value)> table = new();
    int tableSize = headerTableSize;
    int size = 0;

    public int Size { get => size; }
    public int TableSize { get => tableSize; set { tableSize = value; Evict(); } }
    // public LinkedList<(byte[] name, byte[] value)> Table { get => table; }

    public void AddHeader(byte[] name, byte[] value)
    {
        table.AddFirst((name, value));
        size += name.Length + value.Length + 32; // 4.2 #calculating.table.size
        Evict();
    }

    public (byte[],byte[]) Get(int index)
    {
        if (index < 0 || index >= table.Count) throw new ArgumentOutOfRangeException();
        
        LinkedListNode<(byte[], byte[])> current;
        if (index < table.Count / 2)
        {
            current = table.First!;
            for (int i = 0; i < index; i++) current = current.Next!;
        }
        else
        {
            current = table.Last!;
            for (int i = table.Count - 1; i > index; i--) current = current.Previous!;
        }
        
        return current.Value;
    }

    void Evict()
    {
        while (table.Count != 0 && size > tableSize)
        {
            var (k, v) = table.Last();
            size -= k.Length + v.Length + 32;
        }
    }
}