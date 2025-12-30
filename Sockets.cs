namespace Samicpp.Http;

using System.Net.Security;
using System.Net.Sockets;

// maybe quic socket in here in the future

public class TcpSocket(NetworkStream stream) : ADualSocket
{
    public TcpSocket(Socket socket): this(new NetworkStream(socket, true)) {}
    override protected NetworkStream Stream { get; } = stream;
    override public bool IsSecure { get; } = false;
}
public class TlsSocket(SslStream stream) : ADualSocket
{
    override protected SslStream Stream { get; } = stream;
    override public bool IsSecure { get; } = true;
}
public class UnkownSocket(Stream stream) : ADualSocket
{
    override protected Stream Stream { get; } = stream;
    override public bool IsSecure { get; } = false; // unknown
}