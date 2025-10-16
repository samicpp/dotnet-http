namespace Samicpp.Tests;

using System;
using System.Threading.Tasks;
// using Samicpp.Http.Debug;
using Xunit;
using Samicpp.Http;
using Samicpp.Http.Http2;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using Samicpp.Http.Http1;
using System.Collections.Generic;

public class Tests
{
    [Fact]
    public void test1()
    {
        Console.WriteLine("run test");
    }

    [Fact]
    public void test2()
    {
        // FakeHttpSocket sock = new();

    }

    [Fact]
    public void frameParseTest()
    {
        Http2Frame frame = Http2Frame.Parse([0, 0, 14, 0, 0, 0, 1, 0, 49, 5, 0, 0, 0, 1, 5, 97, 98, 99, 1, 2, 3, 4, 5,]);
        Console.WriteLine(frame.payload);
        Console.WriteLine(frame.priority);
        Console.WriteLine(frame.padding);
        Console.Write("frame = [ ");
        foreach (byte b in frame.ToBytes()) Console.Write($"{b}, ");
        Console.WriteLine("]");
    }


    /*[Fact]
    [Trait("Category", "Network")]
    public async Task TcpExampleServer()
    { // https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/sockets/socket-services
        IPEndPoint ipEndPoint = new(IPAddress.Parse("0.0.0.0"), 1024);
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

    [Fact(Skip = "wont end")]
    [Trait("Category", "Network")]
    public async Task TcpEchoServer()
    {
        IPEndPoint address = new(IPAddress.Parse("0.0.0.0"), 1024);
        using Socket listener = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        listener.Bind(address);
        listener.Listen(10);

        while (true)
        {
            var shandler = await listener.AcceptAsync();
            Console.WriteLine($"\e[32m{shandler.RemoteEndPoint}\e[0m");

            var _ = Task.Run(async () =>
            {
                using NetworkStream stream = new(shandler, ownsSocket: true);
                using TcpSocket socket = new(stream);

                while (true)
                {
                    var data = await socket.ReadUntilAsync([(byte)'.']);
                    var text = Encoding.UTF8.GetString([.. data]);

                    Console.WriteLine($"received message \e[36m{text.Trim()}\e[0m");
                    // Console.ForegroundColor = ConsoleColor.Blue;
                    // Console.WriteLine(text);
                    // Console.ResetColor();
                    await socket.WriteAsync(Encoding.UTF8.GetBytes($"<| {text.Trim()} |>\n"));
                }
            });
        }
    }

    [Fact]
    // [Fact(Skip = "wont end")]
    [Trait("Catogory", "Network")]
    public async Task HttpEchoServer()
    {
        IPEndPoint address = new(IPAddress.Parse("0.0.0.0"), 2048);
        using Socket listener = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        listener.Bind(address);
        listener.Listen(10);

        Console.WriteLine("listening on " + address);

        while (true)
        {
            var shandler = await listener.AcceptAsync();
            Console.WriteLine($"\e[32m{shandler.RemoteEndPoint}\e[0m");

            var _ = Task.Run(async () =>
            {
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
                // await Task.Delay(100);
            });
        }
    }
}
