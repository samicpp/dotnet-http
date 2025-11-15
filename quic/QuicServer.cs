namespace Samicpp.Http.Quic;

using System.Net;
using System.Net.Sockets;


public class QuicServer
{
    public EndPoint from;
    public readonly EndPoint listen;
    readonly Socket udp;
    
    public QuicServer(EndPoint listen, EndPoint? from = null)
    {
        from ??= new IPEndPoint(IPAddress.Any, 0);
        this.listen = listen;
        this.from = from;

        udp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        udp.Bind(listen);
    }
}