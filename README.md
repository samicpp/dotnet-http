# dotnet-http
A http library in c# <br/> <br/>

This library is a low-level RFC implementation of HTTP. 
It is expected that you implement logic like content serving yourself. 
You can consult the [test code](/tests/Program.cs) for code examples/usage. <br/> <br/>

This library contains both sync and async methods allowing you to use it in both contexts. 
For every blocking/waiting method theres an async/sync version. <br/> <br/>

H2C and WebSocket upgrades can in the library. <br/> <br/>

In the future I might make another library that uses this one to support a ready to use Web framework with middleware and more.

## TODO::Features
- [x] allow both sync and async code
- [x] implement HTTP/0.9 (utterly useless, for educational purposes only)
- [x] implement HTTP/1.1
- [x] implement WebSocket
- [x] implement HPACK
- [x] implement HTTP/2
- [ ] implement QUIC
- [ ] implement QPACK
- [ ] implement HTTP/3

## TODO::Improvements
- [x] improve compressor
- [x] support use of `Stream` instead of `byte[]`
- [x] support trailing headers
- [ ] support HTTP/1.1 pipelining
- [ ] add comments/documentation
- [ ] change client data type from List<byte> to MemoryStream

## Examples

Here is the HTTP echo server example from the [test code](/tests/Program.cs).

HTTP/1.1 echo server
```c#
using Samicpp.Http;
using Samicpp.Http.Http1;

using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

public class Program
{
    public async Task Main()
    {
        // creating listener
        IPEndPoint address = new(IPAddress.Parse("0.0.0.0"), 2048);
        using Socket listener = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        // start listener
        listener.Bind(address);
        listener.Listen(10);

        Console.WriteLine("http echo server listening on http://127.0.0.1:2048");

        // connection loop
        while (true)
        {
            var shandler = await listener.AcceptAsync();
            Console.WriteLine($"\e[32m{shandler.RemoteEndPoint}\e[0m");

            var _ = Task.Run(async () =>
            {
                // first we need to convert it to something we can pass to Samicpp.Http.TcpSocket
                using NetworkStream stream = new(shandler, ownsSocket: true);

                // then we use it to construct `Samicpp.Http.Http1.Http1Socket`
                using Http1Socket socket = new(new TcpSocket(stream)); 
                // interface `Samicpp.Http.IDualHttpSocket` can also be used as data type, since the class implements this.
                // individual H2 streams also implement this

                Console.WriteLine("constructed protocol handler");

                // when the client uses `Transfer-Encoding: chunked` each read will only add 1 chunk to the body buffer
                // if `Content-Length: n` was provided the library will only read the full body on the second read invocation 
                // to ensure not enforcing body read
                // this is also usefull for Http2Streams where reading client doesnt block
                var client = await socket.ReadClientAsync();

                // ensures full client has been read
                while (!client.HeadComplete || !client.BodyComplete) client = await socket.ReadClientAsync();

                // the framework allows for headers to appear multiple times
                if (client.Headers.TryGetValue("accept-encoding", out List<string> encoding))
                {
                    foreach (string s in encoding[0].Split(","))
                    {
                        // setting `Samicpp.Http.IDualSocket.Compression` automatically ensures the appropriate compression type is used
                        // the framework does not verify if client accepts the encoding, this was done on purpose to give the code full 
                        // the framework also doesnt set a Content-Encoding header
                        switch(s)
                        {
                            case "gzip":
                                socket.Compression = Compression.Gzip;
                                socket.SetHeader("Content-Encoding", "gzip");
                                break;
                            
                            case "deflate":
                                socket.Compression = Compression.Deflate;
                                socket.SetHeader("Content-Encoding", "deflate");
                                break;

                            case "br":
                                socket.Compression = Compression.Brotli;
                                socket.SetHeader("Content-Encoding", "br");
                                break;

                            default:
                                socket.Compression = Compression.None;
                                socket.SetHeader("Content-Encoding", "identity");
                                break;
                        };
                        if (socket.Compression != Compression.None) break;
                    }
                    Console.WriteLine("using compression " + socket.Compression);
                }
                else
                {
                    socket.SetHeader("Content-Encoding", "identity");
                    Console.WriteLine("no compression");
                }

                Console.WriteLine(client);
                Console.WriteLine($"received {client.Body.Count} bytes");

                // the server doesnt decode the client body automatically, it also doesnt decompress it. this is the code's responsibility.
                // for decompression you can use `Samicpp.Http.Compressor.Decompress`
                var text = Encoding.UTF8.GetString([.. client.Body]);

                Console.WriteLine($"received request with body[{text.Length}] \e[36m{text.Trim()}\e[0m");

                // the server does ensure you cannot attempt to send data after connection has been closed
                // nor does it allow you to send headers after
                await socket.CloseAsync(Encoding.UTF8.GetBytes($"<| {text.Trim()} |>\n"));
            });
        }
    }
}
```

HTTP/2 echo server
```c#
using Samicpp.Http;
using Samicpp.Http.Http2;

using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

public class Program
{
    public async Task Main()
    {
        // creating listener
        IPEndPoint address = new(IPAddress.Parse("0.0.0.0"), 2048);
        using Socket listener = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

        // start listener
        listener.Bind(address);
        listener.Listen(10);

        Console.WriteLine("http echo server listening on http://127.0.0.1:2048");

        // connection loop
        while (true)
        {
            var shandler = await listener.AcceptAsync();
            Console.WriteLine($"\e[32m{shandler.RemoteEndPoint}\e[0m");

            var _ = Task.Run(async () =>
            {
                // first we need to convert it to something we can pass to class Samicpp.Http.TcpSocket
                using NetworkStream stream = new(shandler, ownsSocket: true);

                // then we use it to construct `Samicpp.Http.Http2.Http2Session`
                using Http2Session h2 = new(new TcpSocket(stream), Http2Settings.Default()); 
                

                // the framework doesnt automatically read and check the preface, so we have to invoke it manually
                await h2.InitAsync();

                Console.WriteLine("initialized http2 connection");

                // we also need to send our settings
                await h2.SendSettingsAsync(Http2Settings.Default());
                // by default it uses the constructor provided settings for the encoder header table size
                // if you change this you will need to modify this manually with
                // `Samicpp.Http.Http2.Http2Session.hpacke.TableSize = 4096`

                // when the library handles a Goaway frame it will store this in `Samicpp.Http.Http2.Http2Session.goaway`
                // we can use this as an indicator for open connections
                while(h2.goaway == null)
                {
                    // first you need to read a http2 frame
                    // avoid using ReadAllAsync
                    Http2Frame frame = await socket.ReadAsync();
                    // this reads all available buffer, which can contain incomplete frames
                    // it is recommended you use `Samicpp.Http.Http2.Http2Session.ReadOneAsync` which does wait until it receives a whole frame
                    // you can handle these manually if you want, but that is not necessary

                    // we then pass the frame to the handler, which automatically updates stream states and more
                    int? openedStream = await socket.HandleAsync(frame);
                    // this returns a stream id if a stream was opened 
                    // this method also has an overload for multiple frames which returns `List<int>`


                    foreach (int streamID in openedStreams)
                    {
                        var _ = Task.Run(async () => 
                        {
                            // we can directly use `Samicpp.Http.Http2.Http2Session` to send headers/data but that is not necessary
                            // we can use the single-stream handler `Samicpp.Http.Http2.Http2Stream`
                            using Http2Stream stream = new(streamID, h2);
                            // this implements `Samicpp.Http.IDualHttpSocket` allowing for interopibility 
                            // with functions that accept both Samicpp.Http.Http1.Http1Socket and `Samicpp.Http.Http2.Http2Stream`

                            // in h2 it is much more realistic that not the whole client has been read
                            // furthermore the library doesnt block for reading the client
                            // it queries the stream state to retrieve client data
                            var client = await stream.ReadClientAsync();
                            while (!client.HeadComplete || !client.BodyComplete) client = await stream.ReadClientAsync();

                            
                            // we can use `Samicpp.Http.Http2.Http2Stream` like it is `Samicpp.Http.Http1.Http1Socket`
                            stream.SetHeader("content-type", "text/plain");

                            // the client body is also of type `List<byte>`
                            var text = Encoding.UTF8.GetString([.. client.Body]);

                            // `Samicpp.Http.Http2.Http2Stream.CloseAsync` and its sync version both include header `Content-Length` 
                            // in the response if the headers havent yet been sent
                            await stream.CloseAsync(Encoding.UTF8.GetBytes($"<| {text.Trim()} |>\n"));
                        });
                    }
                }
            });
        }
    }
}
```
