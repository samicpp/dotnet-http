namespace Samicpp.Http.Quic;

using System.Threading.Tasks;

public class QuicStream(QuicSession quicSession, long streamId, bool bidirectional, bool owner) //: Stream
{
    public bool Bidirectional { get; } = bidirectional;
    readonly bool owner = owner;
    readonly QuicSession session = quicSession;
    readonly long streamId = streamId;
}