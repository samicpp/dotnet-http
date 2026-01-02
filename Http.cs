namespace Samicpp.Http;

using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;


public enum HttpVersion
{
    Test = -3,
    Unknown = -2,
    Http09 = -1,
    Http10 = 0,
    Http11 = 1,
    Http2 = 2,
    Http3 = 3,
}

public abstract class ADualSocket : IDualSocket
{
    abstract protected Stream Stream { get; }
    abstract public bool IsSecure { get; }
    // abstract public EndPoint? EndPoint { get; }

    public int Read(Span<byte> bytes) => Stream.Read(bytes);
    public int Read(byte[] bytes, int offset, int size) => Stream.Read(bytes, offset, size);
    public void Write(Span<byte> bytes) => Stream.Write(bytes);
    public void Write(byte[] bytes, int offset, int size) => Stream.Write(bytes, offset, size);
    public void Write(Stream stream) => stream.CopyTo(Stream);
    public async Task<int> ReadAsync(Memory<byte> bytes) => await Stream.ReadAsync(bytes);
    public async Task<int> ReadAsync(byte[] bytes, int offset, int size) => await Stream.ReadAsync(bytes, offset, size);
    public async Task WriteAsync(Memory<byte> bytes) => await Stream.WriteAsync(bytes);
    public async Task WriteAsync(Stream stream) => await stream.CopyToAsync(Stream);
    public async Task WriteAsync(byte[] bytes, int offset, int size) => await Stream.WriteAsync(bytes, offset, size);

    public void Flush() => Stream.Flush();
    public async Task FlushAsync() => await Stream.FlushAsync();
    public void Close() => Stream.Close();
    public void Dispose() => Stream.Dispose();
    public async ValueTask DisposeAsync() => await Stream.DisposeAsync();
    public bool CanRead { get { return Stream.CanRead; } }
    public bool CanWrite { get { return Stream.CanWrite; } }
    public bool CanSeek { get { return Stream.CanSeek; } }
    public bool CanTimeout { get { return Stream.CanTimeout; } }


    public List<byte> ReadAll()
    {
        List<byte> all = [];
        byte[] buff = new byte[4096];

        while (true)
        {
            int s = Read(buff);
            all.AddRange(buff[..s]);
            if (s < buff.Length) break;
        }

        return all;
    }
    public async Task<List<byte>> ReadAllAsync()
    {
        List<byte> all = [];
        byte[] buff = new byte[4096];

        while (true)
        {
            int s = await ReadAsync(buff);
            all.AddRange(buff[..s]);
            if (s < buff.Length) break;
        }

        return all;
    }

    public byte[] ReadCertain(int size)
    {
        byte[] bytes = new byte[size];
        int last = 0;

        while (last < bytes.Length)
        {
            int s = Read(bytes, last, bytes.Length - last);
            if (s <= 0) throw new HttpException.ConnectionClosed(null);
            last += s;
        }

        return bytes;
    }
    public async Task<byte[]> ReadCertainAsync(int size)
    {
        byte[] bytes = new byte[size];
        int last = 0;

        while (last < bytes.Length)
        {
            int s = await ReadAsync(bytes, last, bytes.Length - last);
            if (s <= 0) throw new HttpException.ConnectionClosed(null);
            last += s;
        }

        return bytes;
    }

    private static bool EndsWith(List<byte> source, byte[] stop)
    {
        int start = source.Count - stop.Length;
        if (start < 0) return false;

        for (int i = 0; i < stop.Length; i++)
        {
            if (source[start + i] != stop[i]) return false;
        }

        return true;
    }

    public List<byte> ReadUntil(byte stop)
    {
        List<byte> total = [];
        byte[] buff = new byte[1];

        while (true)
        {
            int s = Read(buff);

            if (s <= 0) throw new HttpException.ConnectionClosed(null);

            total.Add(buff[0]);

            if (buff[0] == stop) break;
        }

        return total;
    }
    public List<byte> ReadUntil(byte[] stop)
    {
        List<byte> total = [];
        byte[] buff = new byte[1];

        while (true)
        {
            int s = Read(buff);

            if (s <= 0) throw new HttpException.ConnectionClosed(null);

            total.Add(buff[0]);
            // total.AddRange(buff[..s]);
            if (total.Count < stop.Length) continue;

            if (EndsWith(total, stop)) break;
        }

        return total;
    }
    public List<byte> ReadUntil(params byte[][] stops)
    {
        List<byte> total = [];
        byte[] buff = new byte[1];

        while (true)
        {
            int s = Read(buff);

            if (s <= 0) throw new HttpException.ConnectionClosed(null);

            total.Add(buff[0]);
            // total.AddRange(buff[..s]);
            foreach (var stop in stops) if (total.Count < stop.Length) goto cont;

            foreach (var stop in stops) if (EndsWith(total, stop)) goto end;
                cont: continue; // continue not nessecary
        }

    end: return total;
    }
    public async Task<List<byte>> ReadUntilAsync(byte stop)
    {
        List<byte> total = [];
        byte[] buff = new byte[1];

        while (true)
        {
            int s = await ReadAsync(buff);

            if (s <= 0) throw new HttpException.ConnectionClosed(null);
            
            total.Add(buff[0]);

            if (buff[0] == stop) break;
        }

        return total;
    }
    public async Task<List<byte>> ReadUntilAsync(byte[] stop)
    {
        List<byte> total = [];
        byte[] buff = new byte[1];

        while (true)
        {
            int s = await ReadAsync(buff);

            if (s <= 0) throw new HttpException.ConnectionClosed(null);

            total.Add(buff[0]);
            // total.AddRange(buff[..s]);
            if (total.Count < stop.Length) continue;

            if (EndsWith(total, stop)) break;
        }

        return total;
    }
    public async Task<List<byte>> ReadUntilAsync(params byte[][] stops)
    {
        List<byte> total = [];
        byte[] buff = new byte[1];

        while (true)
        {
            int s = await ReadAsync(buff);

            if (s <= 0) throw new HttpException.ConnectionClosed(null);

            total.Add(buff[0]);
            // total.AddRange(buff[..s]);
            foreach (var stop in stops) if (total.Count < stop.Length) goto cont;

            foreach (var stop in stops) if (EndsWith(total, stop)) goto end;
            cont: continue;
        }

        end: return total;
    }
}

public class HttpClient : IHttpClient
{
    public bool IsValid { get; set; } = false;
    public Dictionary<string, List<string>> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Host { get; set; } = "about:blank";
    public string Method { get; set; } = "NILL";
    public string Path { get; set; } = "/";
    public HttpVersion Version { get; set; } = HttpVersion.Unknown;
    public string VersionString { get => Version switch { HttpVersion.Test => "Test", HttpVersion.Unknown => "Unknown", HttpVersion.Http09 => "HTTP/0.9", HttpVersion.Http10 => "HTTP/1.0", HttpVersion.Http11 => "HTTP/1.1", HttpVersion.Http2 => "HTTP/2", HttpVersion.Http3 => "HTTP/3", _ => "Impossible" }; }
    public List<byte> Body { get; set; } = [];

    public bool HeadersComplete { get; set; } = false;
    public bool BodyComplete { get; set; } = false;
}

public class HttpException(string? message = null, Exception? source = null) : Exception(message)
{
    public readonly Exception? source = source;
    public sealed class ConnectionClosed(string? message) : HttpException(message);
    public sealed class HeadersSent(string? message) : HttpException(message);
    public sealed class MalformedRequest(string? message) : HttpException(message);
    public sealed class UnsupportedVersion(string? message) : HttpException(message);
}

