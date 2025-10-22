namespace Samicpp.Tests;

using System;
using System.Threading.Tasks;
// using Samicpp.Http.Debug;
using Xunit;
using Samicpp.Http;
using Samicpp.Http.Http2;
using Samicpp.Http.Http2.Hpack;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using Samicpp.Http.Http1;
using Samicpp.Http.WebSocket;
using System.Collections.Generic;
using Http2Connection = Samicpp.Http.Http2.Http2Session;
using Samicpp.Http.Debug;
using System.Linq;

public class TcpSocket(NetworkStream stream) : ANetSocket
{
    override protected NetworkStream Stream { get { return stream; } }
    override public bool IsSecure { get { return false; } }
}

public class Tests
{
    [Fact]
    public void test1()
    {
        Console.WriteLine("run test");
    }

    [Fact]
    public void frameParseTest()
    {
        Http2Frame frame = Http2Frame.Parse([0, 0, 14, 0, 0, 0, 1, 0, 49, 5, 0, 0, 0, 1, 5, 97, 98, 99, 1, 2, 3, 4, 5,]);
        Console.WriteLine(frame.payload);
        Console.WriteLine(frame.priority);
        Console.WriteLine(frame.padding);
        Console.Write("frame = [ ");
        foreach (byte b in frame.ToBytes()) Console.Write($"0x{b:X}, ");
        Console.WriteLine("]");
    }

    [Fact]
    public void biteratorTest() // name is intentional, not spelling mistake
    {
        // byte[] data = [ 0b1010_0110, 0b1111_0000 ];
        byte[] data = "hello world text"u8.ToArray();

        var bitIter = new BitIterator(data);

        Console.WriteLine(data);

        foreach (var bit in bitIter) Console.Write(bit ? '1' : '0');

        Console.WriteLine();
    }

    [Fact]
    public void dehpacker()
    {
        // 2 5
        Http.Http2.Hpack.Encoder hpacke = new(4096);
        var enc = hpacke.Encode([
            new HeaderEntry(":method"u8.ToArray(), "GET"u8.ToArray()),
            new HeaderEntry(":path"u8.ToArray(), "/index.html"u8.ToArray()),
            new HeaderEntry("indexed"u8.ToArray(),"header"u8.ToArray(), true),
            new HeaderEntry("not"u8.ToArray(),"indexed"u8.ToArray(), false),
            new HeaderEntry("never"u8.ToArray(),"indexed"u8.ToArray(), false, true),
            new HeaderEntry("huffman"u8.ToArray(),"encoded"u8.ToArray(), false, false, true),
            new HeaderEntry("not huffman"u8.ToArray(),"encoded"u8.ToArray(), false, false, false),
        ]);

        Console.Write("hpack encoded = [ ");
        foreach (var b in enc) Console.Write($"0x{b:X}, ");
        Console.WriteLine("]");
    }

    [Fact]
    public void hpacker()
    {
        // 2 5
        Http.Http2.Hpack.Decoder hpackd = new(4096);
        var dec = hpackd.Decode([0x82, 0x85, 0x40, 0x85, 0x35, 0x52, 0x17, 0xC9, 0x64, 0x85, 0x9C, 0xA3, 0x90, 0xB6, 0x7F, 0x40, 0x88, 0xA8, 0xE9, 0x50, 0xD5, 0x48, 0x5F, 0x25, 0x93, 0x85, 0x9C, 0xA3, 0x90, 0xB6, 0x7F,]);
        foreach (var (h, v) in dec)
        {
            var header = Encoding.UTF8.GetString(h);
            var value = Encoding.UTF8.GetString(v);
            Console.WriteLine($"{header}: {value}");
        }
    }


    /*[Fact]
    [Trait("Category", "Network")]
    public async Task TcpExampleServer()
    { // https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/sockets/socket-services
        IPEndPoint ipEndPoint = new(IPAddress.Parse("0.0.0.0"), 512);
        using Socket listener = new(
            ipEndPoint.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);

        listener.Bind(ipEndPoint);
        listener.Listen(100);

        var handler = await listener.AcceptAsync();
        while (true)
        {
            // Receive message.
            var buffer = new byte[1_024];
            var received = await handler.ReceiveAsync(buffer, SocketFlags.None);
            var response = Encoding.UTF8.GetString(buffer, 0, received);
            
            var eom = "<|EOM|>";
            if (response.IndexOf(eom) > -1 /* is end of message * /)
            {
                Console.WriteLine(
                    $"Socket server received message: \"{response.Replace(eom, "")}\"");

                var ackMessage = "<|ACK|>";
                var echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                await handler.SendAsync(echoBytes, 0);
                Console.WriteLine(
                    $"Socket server sent acknowledgment: \"{ackMessage}\"");

                break;
            }
            // Sample output:
            //    Socket server received message: "Hi friends 👋!"
            //    Socket server sent acknowledgment: "<|ACK|>"
        }
        await Task.Delay(0);
    }*/

    [Fact]
    public void FakeSocketCorrect()
    {
        HttpClient sourceClient = new();
        sourceClient.Body = "client body here"u8.ToArray().ToList();
        sourceClient.Method = "METHOD";
        sourceClient.Version = "FakeSocket";
        sourceClient.Path = "/file.html";
        sourceClient.Host = "fake.localhost:12345";
        sourceClient.Headers = new()
        {
            { "Host", [sourceClient.Host] },
            { "User-Agent", ["curl/8.16.0"] },
            { "Accept", ["*/*"] },
            { "Content-Type", ["text/plain"] },
            { "Header", ["value"] },
        };

        using FakeHttpSocket sock = new(sourceClient);


        var client = sock.ReadClient();
        while (!client.BodyComplete) client = sock.ReadClient();

        // Console.WriteLine("fake client sent headers");
        // foreach (var (h, vs) in client.Headers) foreach (var v in vs) Console.WriteLine($"{h}: {v}");
        // Console.WriteLine("");

        sock.SetHeader("Content-Type", "text/plain");
        sock.SetHeader("Server", "SomeServerName");

        sock.AddHeader("Set-Cookie", "sessionId=abc123; Path=/; HttpOnly; Secure; SameSite=Strict");
        sock.AddHeader("Set-Cookie", "theme=dark; Path=/; Max-Age=3600");
        sock.AddHeader("Set-Cookie", "lang=en-US; Path=/; Expires=Wed, 22 Oct 2025 20:00:00 GMT");

        sock.Close("Hello World");
    }

    [Fact]
    public void FakeSocketWrong()
    {
        HttpClient sourceClient = new();
        sourceClient.Body = "client body here"u8.ToArray().ToList();
        sourceClient.Method = "METHOD";
        sourceClient.Version = "FakeSocket";
        sourceClient.Path = "/file.html";
        sourceClient.Host = "fake.localhost:12345";
        sourceClient.Headers = new()
        {
            { "Host", [sourceClient.Host] },
            { "User-Agent", ["curl/8.16.0"] },
            { "Accept", ["*/*"] },
            { "Content-Type", ["text/plain"] },
            { "Header", ["value"] },
        };


        void Wrapper()
        {
            FakeHttpSocket sock = new(sourceClient);

            var client = sock.ReadClient();
            while (!client.BodyComplete) client = sock.ReadClient();
            sock.ReadClient(); sock.ReadClient();

            sock.SetHeader("Content-Type", "text/plain");
            sock.SetHeader("Server", "SomeServerName");

            sock.Close("Hello World");

            sock.AddHeader("Set-Cookie", "sessionId=abc123; Path=/; HttpOnly; Secure; SameSite=Strict");
            sock.AddHeader("Set-Cookie", "theme=dark; Path=/; Max-Age=3600");
            sock.AddHeader("Set-Cookie", "lang=en-US; Path=/; Expires=Wed, 22 Oct 2025 20:00:00 GMT");

            sock.Close("Hello again");
            sock.Write("Hello");
        }
        Wrapper();

        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    [Fact]
    public void FakeSocketError()
    {
        HttpClient sourceClient = new();

        using FakeHttpSocket sock = new(sourceClient);
    }

    [Fact]
    // [Fact(Skip = "long test")]
    [Trait("Category", "Network")]
    public async Task TcpEchoServer()
    {
        IPEndPoint address = new(IPAddress.Parse("0.0.0.0"), 1024);
        using Socket listener = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        listener.Bind(address);
        listener.Listen(10);

        Console.WriteLine("tcp echo server listening on 1024");

        while (true)
        {
            var shandler = await listener.AcceptAsync();
            Console.WriteLine($"\e[32m{shandler.RemoteEndPoint}\e[0m");


            using NetworkStream stream = new(shandler, ownsSocket: true);
            using TcpSocket socket = new(stream);

            while (socket.CanRead)
            {
                try
                {
                    var data = await socket.ReadUntilAsync([(byte)'.']);
                    var text = Encoding.UTF8.GetString([.. data]);

                    Console.WriteLine($"received message \e[36m{text.Trim()}\e[0m");
                    // Console.ForegroundColor = ConsoleColor.Blue;
                    // Console.WriteLine(text);
                    // Console.ResetColor();
                    if (socket.CanWrite) await socket.WriteAsync(Encoding.UTF8.GetBytes($"<| {text.Trim()} |>\n"));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    break;
                }
            }

            break;
        }
    }

    [Fact]
    // [Fact(Skip = "long test")]
    [Trait("Category", "Network")]
    public async Task HttpEchoServer()
    {
        IPEndPoint address = new(IPAddress.Parse("0.0.0.0"), 2048);
        using Socket listener = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        listener.Bind(address);
        listener.Listen(10);

        Console.WriteLine("http echo server listening on 2048");
        // Console.WriteLine("listening on " + address);

        while (true)
        {
            var shandler = await listener.AcceptAsync();
            Console.WriteLine($"\e[32m{shandler.RemoteEndPoint}\e[0m");


            using NetworkStream stream = new(shandler, ownsSocket: true);
            using Http1Socket socket = new(new TcpSocket(stream));

            Console.WriteLine("continuing");

            var data = await socket.ReadClientAsync();
            while (!data.BodyComplete) data = await socket.ReadClientAsync();

            if (data.Headers.TryGetValue("accept-encoding", out List<string> encoding))
            {
                foreach (string s in encoding[0].Split(","))
                {
                    socket.Compression = s switch
                    {
                        "gzip" => Compression.Gzip,
                        "deflate" => Compression.Deflate,
                        "br" => Compression.Brotli,
                        _ => Compression.None,
                    };
                    if (socket.Compression != Compression.None) break;
                }
                Console.WriteLine("using compression " + socket.Compression);
            }
            else
            {
                Console.WriteLine("no compression");
            }

            Console.WriteLine(data);
            Console.WriteLine($"received {data.Body.Count} bytes");

            var text = Encoding.UTF8.GetString([.. data.Body]);

            Console.WriteLine($"received request with body[{text.Length}] \e[36m{text.Trim()}\e[0m");
            await socket.CloseAsync(Encoding.UTF8.GetBytes($"<| {text.Trim()} |>\n"));

            break;
            // await Task.Delay(100);
        }
    }

    [Fact]
    // [Fact(Skip = "long test")]
    [Trait("Category", "Network")]
    public async Task WSEchoServer()
    {
        IPEndPoint address = new(IPAddress.Parse("0.0.0.0"), 4096);
        using Socket listener = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        listener.Bind(address);
        listener.Listen(10);

        Console.WriteLine("ws echo server listening on 4096");
        // Console.WriteLine("listening on " + address);

        while (true)
        {
            var shandler = await listener.AcceptAsync();
            Console.WriteLine($"\e[32m{shandler.RemoteEndPoint}\e[0m");


            try
            {
                using NetworkStream stream = new(shandler, ownsSocket: true);
                using Http1Socket socket = new(new TcpSocket(stream));

                Console.WriteLine("continuing");

                var client = await socket.ReadClientAsync();
                while (!client.HeadersComplete) client = await socket.ReadClientAsync();

                var ht = "";
                Console.WriteLine("client headers");
                Console.WriteLine(client.Headers);
                foreach (var (h, vs) in client.Headers) foreach (var v in vs) ht += $"{h}: {v}\r\n";
                Console.WriteLine(ht);

                if (client.Headers.TryGetValue("upgrade", out List<string> up) && up[0] == "websocket")
                {
                    using var websocket = await socket.WebSocketAsync();
                    var closed = false;
                    while (!closed)
                    {
                        var frames = await websocket.IncomingAsync();
                        Console.WriteLine("upgrade succesfull");
                        foreach (var frame in frames)
                        {
                            Console.WriteLine("received frame " + frame.type);
                            Console.WriteLine("payload = " + frame.payload);
                            Console.WriteLine("frame size = " + frame.raw.Length);

                            var payload = frame.GetPayload();

                            if (frame.type == WebSocketFrameType.Text || frame.type == WebSocketFrameType.Continuation)
                            {
                                await websocket.SendTextAsync(payload);
                                Console.WriteLine("ehco payload " + Encoding.UTF8.GetString(payload));
                            }
                            else if (frame.type == WebSocketFrameType.Ping)
                            {
                                await websocket.SendPingAsync(frame.GetPayload());
                                Console.WriteLine("pong");
                            }
                            else if (frame.type == WebSocketFrameType.ConnectionClose)
                            {
                                await websocket.SendCloseConnectionAsync(frame.GetPayload());
                                Console.WriteLine("close");
                                closed = true;
                                break;
                            }
                        }
                    }

                    break;
                }
                else
                {
                    Console.WriteLine("didnt wanna upgrade");
                    await socket.CloseAsync("use websocket");
                    await socket.DisposeAsync();
                }
                // await Task.Delay(100);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }
    }

    [Fact]
    // [Fact(Skip = "long test")]
    [Trait("Category", "Network")]
    public async Task H2TestServer()
    {
        IPEndPoint address = new(IPAddress.Parse("0.0.0.0"), 8192);
        using Socket listener = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        listener.Bind(address);
        listener.Listen(10);

        Console.WriteLine("h2 test server listening on 8192");

        // Console.WriteLine("listening on " + address);

        while (true)
        {
            var shandler = await listener.AcceptAsync();
            Console.WriteLine($"\e[32m{shandler.RemoteEndPoint}\e[0m");


            try
            {
                using NetworkStream nstream = new(shandler, ownsSocket: true);
                using Http2Connection socket = new(new TcpSocket(nstream), Http2Settings.Default());

                Console.WriteLine("continuing");

                await socket.InitAsync();
                await socket.SendSettingsAsync(Http2Settings.Default());

                while (socket.goaway == null)
                {
                    List<Http2Frame> frames = [await socket.ReadOneAsync()];
                    foreach (var frame in frames)
                    {
                        Console.Write($"received \x1b[36m{frame.type}\x1b[0m [ ");
                        foreach (byte b in frame.raw) Console.Write($"0x{b:X}, ");
                        Console.WriteLine("]");
                    }

                    var opened = await socket.HandleAsync(frames);
                    foreach (var sid in opened)
                    {
                        Console.WriteLine($"stream opened {sid}");
                        // throw new Exception("test");
                        // var stream = socket.streams[sid];

                        // Console.WriteLine("client sent headers \x1b[32m");
                        // foreach (var (h, v) in stream.headers)
                        // {
                        //     var header = Encoding.UTF8.GetString(h);
                        //     var value = Encoding.UTF8.GetString(v);
                        //     Console.WriteLine($"{header}: {value}");
                        // }
                        // Console.Write("\x1b[0m");

                        // await socket.SendHeadersAsync(sid, false, [
                        //     (":status"u8.ToArray(), "200"u8.ToArray()),
                        //     ("content-type"u8.ToArray(), "text/plain"u8.ToArray()),
                        //     ("content-length"u8.ToArray(), "11"u8.ToArray()),
                        // ]);
                        // await socket.SendDataAsync(sid, true, "hello world"u8.ToArray());
                        using var stream = new Http2Stream(sid, socket);

                        var client = await stream.ReadClientAsync();
                        Console.WriteLine("client sent headers \x1b[32m");
                        foreach (var (header, vs) in client.Headers)
                        {
                            foreach (var value in vs) Console.WriteLine($"{header}: {value}");
                        }
                        Console.Write("\x1b[0m");

                        stream.SetHeader("content-type", "text/plain");
                        stream.SetHeader("content-length", "12");
                        await stream.CloseAsync("hello world\n");
                        await Task.Delay(50);
                    }
                }

                // curl doesnt like closing connection when done
                // await Task.Delay(1000); 
                // if (socket.goaway != null) { Console.WriteLine("goaway received"); } 

                break;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }
    }

    [Fact]
    // [Fact(Skip = "long test")]
    [Trait("Category", "Network")]
    public async Task H2CPUgrade()
    {
        IPEndPoint address = new(IPAddress.Parse("0.0.0.0"), 16384);
        using Socket listener = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        listener.Bind(address);
        listener.Listen(10);

        Console.WriteLine("h2c upgrade server listening on 16384");

        // Console.WriteLine("listening on " + address);

        while (true)
        {
            var shandler = await listener.AcceptAsync();
            Console.WriteLine($"\e[32m{shandler.RemoteEndPoint}\e[0m");


            try
            {
                using NetworkStream nstream = new(shandler, ownsSocket: true);
                using Http1Socket socket = new(new TcpSocket(nstream));

                Console.WriteLine("continuing");

                var client = await socket.ReadClientAsync();
                while (!client.HeadersComplete) client = await socket.ReadClientAsync();

                if (client.Headers.TryGetValue("upgrade", out List<string> up) && up[0] == "h2c")
                {
                    Console.WriteLine("upgrading");
                    using Http2Connection h2c = await socket.H2CAsync();
                    Console.WriteLine("succesfull upgrade");

                    await h2c.SendSettingsAsync(Http2Settings.Default());
                    Console.WriteLine("sent settings");

                    int sid = 1;
                    Console.WriteLine($"stream opened {sid}");

                    var stream = h2c.streams[sid];

                    Console.WriteLine("client sent headers \x1b[32m");
                    foreach (var (h, v) in stream.headers)
                    {
                        var header = Encoding.UTF8.GetString(h);
                        var value = Encoding.UTF8.GetString(v);
                        Console.WriteLine($"{header}: {value}");
                    }
                    Console.Write("\x1b[0m");

                    await h2c.SendHeadersAsync(sid, false, [
                        (":status"u8.ToArray(), "200"u8.ToArray()),
                        ("content-type"u8.ToArray(), "text/plain"u8.ToArray()),
                        ("content-length"u8.ToArray(), "11"u8.ToArray()),
                    ]);
                    await h2c.SendDataAsync(sid, true, "hello world"u8.ToArray());


                    await Task.Delay(100);

                    break;
                }
                else
                {
                    await socket.CloseAsync("come back when you're ready to use http2");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

        }
    }

}
