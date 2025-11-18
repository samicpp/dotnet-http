namespace Samicpp.Http.Quic;

using System.Threading.Tasks;

// allows to manage connection like open streams, closing connection, opening streams, etc.
public class QuicSession(QuicServer server, byte[] connectionId)
{
    readonly QuicServer quic = server;
    readonly byte[] connectionId = connectionId;
}