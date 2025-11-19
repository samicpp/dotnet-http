namespace Samicpp.Http.Quic;

using System.Threading.Tasks;

// allows to manage connection like open streams, closing connection, opening streams, etc.
public class QuicSession(QuicKernel server, byte[] connectionId)
{
    readonly QuicKernel quic = server;
    readonly byte[] connectionId = connectionId;
}