namespace Samicpp.Http;

using System.Net.Sockets;

public class TcpSocket(NetworkStream stream) : ANetSocket
{
    override protected NetworkStream Stream { get { return stream; } }
    override public bool IsSecure { get { return false; } }
}