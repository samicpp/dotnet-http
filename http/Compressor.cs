namespace Samicpp.Http;

using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

public enum Compression
{
    None,
    Gzip,
    Deflate,
    Brotli
}

public static class Compressor
{
    public static CompressionLevel level = CompressionLevel.Fastest;
    public static (Stream,MemoryStream) CStream(Compression compression)
    {
        // if (compression == Compression.None) return new MemoryStream(uncompressed);

        var mstream = new MemoryStream();
        Stream stream = compression switch
        {
            Compression.Gzip => new GZipStream(mstream, level, leaveOpen: true),
            Compression.Deflate => new DeflateStream(mstream, level, leaveOpen: true),
            Compression.Brotli => new BrotliStream(mstream, level, leaveOpen: true),
            _ => throw new NotSupportedException($"Compression type {compression} is not supported.")
        };

        // stream.Write(uncompressed, 0, uncompressed.Length);


        return (stream,mstream);
    }
    public static byte[] Compress(Compression compression, byte[] uncompressed)
    {
        // var output = new MemoryStream();
        if (compression == Compression.None) return uncompressed;
        var (stream, mstream) = CStream(compression);
        stream.Write(uncompressed);
        stream.Dispose();
        var output = mstream.ToArray();
        mstream.Dispose();
        return output;
    }
    public static async Task<byte[]> CompressAsync(Compression compression, byte[] uncompressed)
    {
        if (compression == Compression.None) return uncompressed;
        var (stream, mstream) = CStream(compression);
        await stream.WriteAsync(uncompressed, 0, uncompressed.Length);
        await stream.DisposeAsync();
        var output = mstream.ToArray();
        await mstream.DisposeAsync();
        return output;
    }

    

    public static Stream DStream(Compression compression, byte[] compressed)
    {
        if (compression == Compression.None) return new MemoryStream(compressed);

        using var input = new MemoryStream(compressed);
        Stream stream = compression switch
        {
            Compression.Gzip => new GZipStream(input, CompressionMode.Decompress),
            Compression.Deflate => new DeflateStream(input, CompressionMode.Decompress),
            Compression.Brotli => new BrotliStream(input, CompressionMode.Decompress),
            _ => throw new NotSupportedException($"Compression type {compression} is not supported.")
        };
        // stream.CopyTo(output);
        return stream;
    }
    public static byte[] Decompress(Compression compression, byte[] compressed)
    {
        var output = new MemoryStream();
        using var stream = DStream(compression, compressed);
        stream.CopyTo(output);
        return output.ToArray();
    }
    public static async Task<byte[]> DecompressAsync(Compression compression, byte[] compressed)
    {
        var output = new MemoryStream();
        using var stream = DStream(compression, compressed);
        await stream.CopyToAsync(output);
        return output.ToArray();
    }
}
