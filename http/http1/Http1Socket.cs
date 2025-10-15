namespace Samicpp.Http.Http1;

using System.Text;
using System.Threading.Tasks;
using Samicpp.Http;


public class Http1Client : IHttpClient
{
    public Dictionary<string, List<string>> Headers { get; set; } = [];
    public string Host { get; set; } = "about:blank";
    public string Method { get; set; } = "NILL";
    public string Path { get; set; } = "/";
    public string Version { get; } = "HTTP/1.1";
    public List<byte> Body { get; set; } = [];

    public bool HeadersComplete { get; set; } 
    public bool BodyComplete { get; set; } 
}
public class Http1Socket(ANetSocket socket) : ISyncHttpSocket, IAsyncHttpSocket, IDisposable
{
    public bool IsHttps { get => socket.IsSecure; }
    private readonly ANetSocket socket = socket;
    public void Dispose()
    {
        socket.Dispose();
        GC.SuppressFinalize(this);
    }
    
    private Http1Client client = new();
    public IHttpClient Client { get => client; }
    public bool IsClosed { get; set; }
    public bool HeadSent { get; set; }

    public int Status { get; set; } = 200;
    public string StatusMessage { get; set; } = "OK";

    private Dictionary<string, List<string>> headers = new() { { "Connection", ["close"] } };
    public void SetHeader(string name, string value) => headers[name] = [value];
    public void AddHeader(string name, string value)
    {
        if (headers.TryGetValue(name, out List<string>? ls)) ls.Add(value);
        else headers[name] = [value];
    }
    public List<string> DelHeader(string name)
    {
        var head = headers[name];
        if (head == null) return [];
        headers.Remove(name);
        return head;
    }


    public IHttpClient ReadClient()
    {
        bool justReadHeaders = false; // technically redundant due to `else if`
        if (!client.HeadersComplete)
        {
            var buff = socket.ReadUntil([13, 10, 13, 10]);
            var text = Encoding.UTF8.GetString([.. buff]);
            var lines = text.Split("\r\n");
            var mpv = lines[0].Split(" ", 3);

            client.Method = mpv[0];
            client.Path = mpv[1];

            foreach (var header in lines[1..])
            {
                if (string.IsNullOrWhiteSpace(header)) continue;
                var hv = header.Split(":", 2);
                var h = hv[0].Trim().ToLower();
                var v = hv[1].Trim();

                if (client.Headers.TryGetValue(h, out List<string>? c)) c.Add(v);
                else client.Headers[h] = [v];
            }

            client.HeadersComplete =
            justReadHeaders = true;
        }
        else if (!client.BodyComplete)
        {
            if (client.Headers.TryGetValue("content-length", out List<string>? slength))
            {
                var _ = int.TryParse(slength[0], out int length);
                client.Body = [.. socket.ReadCertain(length)];
                client.BodyComplete = true;
            }
            else if (!justReadHeaders && client.Headers.TryGetValue("transfer-encoding", out List<string>? te) && te[0] == "chunked")
            {
                var len = socket.ReadUntil([13, 10]);
                var slen = Encoding.UTF8.GetString([.. len]);
                var length = Convert.ToInt32(slen, 16);

                if (length <= 0)
                {
                    client.BodyComplete = true;
                }
                else
                {
                    var chunk = socket.ReadCertain(length + 2);
                    client.Body.AddRange(chunk);
                }
            }
            else
            {
                client.BodyComplete = true;
            }
        }
        return Client;
    }
    public async Task<IHttpClient> ReadClientAsync()
    {
        bool justReadHeaders = false; // technically unnececary due to `else if`
        if (!client.HeadersComplete)
        {
            var buff = await socket.ReadUntilAsync([13, 10, 13, 10]);
            var text = Encoding.UTF8.GetString([.. buff]);
            var lines = text.Split("\r\n");
            var mpv = lines[0].Split(" ", 3);

            client.Method = mpv[0];
            client.Path = mpv[1];

            foreach (var header in lines[1..])
            {
                if (header == null) continue;
                var hv = header.Split(":", 2);
                var h = hv[0].Trim().ToLower();
                var v = hv[1].Trim();

                if (client.Headers.TryGetValue(h, out List<string>? c)) c.Add(v);
                else client.Headers[h] = [v];
            }

            client.HeadersComplete =
            justReadHeaders = true;
        }
        else if (!client.BodyComplete)
        {
            if (client.Headers.TryGetValue("content-length", out List<string>? slength))
            {
                var _ = int.TryParse(slength[0], out int length);
                client.Body = [.. socket.ReadCertain(length)];
                client.BodyComplete = true;
            }
            else if (!justReadHeaders && client.Headers.TryGetValue("transfer-encoding", out List<string>? te) && te[0] == "chunked")
            {
                var len = await socket.ReadUntilAsync([13, 10]);
                var slen = Encoding.UTF8.GetString([.. len]);
                var length = Convert.ToInt32(slen, 16);

                if (length <= 0)
                {
                    client.BodyComplete = true;
                }
                else
                {
                    var chunk = await socket.ReadCertainAsync(length + 2);
                    client.Body.AddRange(chunk);
                }
            }
            else
            {
                client.BodyComplete = true;
            }
        }
        return Client;
    }

    private void SendHead()
    {
        if (!HeadSent)
        {
            string text = $"HTTP/1.1 {Status} {StatusMessage}\r\n";
            foreach (var (h, v) in headers) text += $"{h}: {v}\r\n";
            text += "\r\n";
            var buff = Encoding.UTF8.GetBytes(text);
            socket.Write(buff);
            HeadSent = true;
        }
    }
    private async Task SendHeadAsync()
    {
        if (!HeadSent)
        {
            string text = $"HTTP/1.1 {Status} {StatusMessage}\r\n";
            foreach (var (h, v) in headers) text += $"{h}: {v}\r\n";
            text += "\r\n";
            var buff = Encoding.UTF8.GetBytes(text);
            await socket.WriteAsync(buff);
            HeadSent = true;
        }
    }


    public void Close(string text) => Close(Encoding.UTF8.GetBytes(text));
    public void Close(Span<byte> bytes)
    {
        if (!IsClosed && !HeadSent)
        {
            headers["Content-Length"] = [bytes.Length.ToString()];
            SendHead();
            socket.Write(bytes);
            IsClosed = true;
        }
        else if (!IsClosed)
        {
            // TODO: add support for sending final headers
            socket.Write(Encoding.UTF8.GetBytes(bytes.Length.ToString("X")+"\r\n"));
            socket.Write(bytes);
            socket.Write([13, 10, 48, 13, 10, 13, 10]);
        }
    }

    public async Task CloseAsync(string text) => await CloseAsync(Encoding.UTF8.GetBytes(text));
    public async Task CloseAsync(Memory<byte> bytes)
    {
        if (!IsClosed && !HeadSent)
        {
            headers["Content-Length"] = [bytes.Length.ToString()];
            await SendHeadAsync();
            await socket.WriteAsync(bytes);
            IsClosed = true;
        }
        else
        {
            // TODO: add support for sending final headers
            byte[] term = [13, 10, 48, 13, 10, 13, 10];
            await socket.WriteAsync(Encoding.UTF8.GetBytes(bytes.Length.ToString("X") + "\r\n"));
            await socket.WriteAsync(bytes);
            await socket.WriteAsync(term);
        }
    }

    public void Write(string text) => Write(Encoding.UTF8.GetBytes(text));
    public void Write(Span<byte> bytes)
    {
        if (!IsClosed)
        {
            if (!HeadSent)
            {
                headers["Transfer-Encoding"] = ["chunked"];
                SendHead();
            }
            socket.Write(Encoding.UTF8.GetBytes(bytes.Length.ToString("X") + "\r\n"));
            socket.Write(bytes);
            socket.Write([13, 10]);
        }
    }

    public async Task WriteAsync(string text) => await WriteAsync(Encoding.UTF8.GetBytes(text));
    public async Task WriteAsync(Memory<byte> bytes)
    {
        if (!IsClosed)
        {
            if (!HeadSent)
            {
                headers["Transfer-Encoding"] = ["chunked"];
                await SendHeadAsync();
            }
            await socket.WriteAsync(Encoding.UTF8.GetBytes(bytes.Length.ToString("X") + "\r\n"));
            await socket.WriteAsync(bytes);
            await socket.WriteAsync("\r\n"u8.ToArray());
        }
    } 
}