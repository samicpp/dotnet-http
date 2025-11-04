namespace Samicpp.Http;

using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

public enum CompressionType
{
    None,
    Gzip,
    Deflate,
    Brotli
}

public class Compressor
{
    public readonly CompressionType type = CompressionType.None;
    public readonly CompressionLevel level = CompressionLevel.Optimal;
    readonly MemoryStream buffer = new();
    readonly Stream? stream;
    bool finished;

    public Compressor(CompressionType compressionType = CompressionType.None, CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        type = compressionType;
        level = compressionLevel;

        if (compressionType == CompressionType.None) return;

        stream = type switch
        {
            CompressionType.Gzip => new GZipStream(buffer, level, true),
            CompressionType.Deflate => new DeflateStream(buffer, level, true),
            CompressionType.Brotli => new BrotliStream(buffer, level, true),
            _ => throw new NotSupportedException(""),
        };
    }

    public byte[] Write(ReadOnlySpan<byte> bytes)
    {
        if (finished) throw new Exception("compressor finished");
        if (type == CompressionType.None) return bytes.ToArray();

        var offset = (int)buffer.Length;
        stream!.Write(bytes);
        stream.Flush();

        var b = new byte[buffer.Length - offset];
        buffer.Position = offset;
        buffer.Read(b, 0, b.Length);
        return b;
    }
    public async Task<byte[]> WriteAsync(ReadOnlyMemory<byte> bytes)
    {
        if (finished) throw new Exception("compressor finished");
        if (type == CompressionType.None) return bytes.ToArray();

        var offset = (int)buffer.Length;
        await stream!.WriteAsync(bytes);
        await stream.FlushAsync();

        var b = new byte[buffer.Length - offset];
        buffer.Position = offset;
        await buffer.ReadAsync(b);
        return b;
    }

    public byte[] Finish(ReadOnlySpan<byte> bytes)
    {
        if (finished) throw new Exception("compressor finished");
        if (type == CompressionType.None) return bytes.ToArray();

        stream!.Write(bytes);
        stream.Dispose();
        finished = true;

        return buffer.ToArray();
    }
    public async Task<byte[]> FinishAsync(ReadOnlyMemory<byte> bytes)
    {
        if (finished) throw new Exception("compressor finished");
        if (type == CompressionType.None) return bytes.ToArray();

        await stream!.WriteAsync(bytes);
        await stream.DisposeAsync();
        finished = true;

        return buffer.ToArray();
    }
}
