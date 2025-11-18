namespace Samicpp.Http.Http3;

using Samicpp.Http.Quic;

// needed for qpack encoder decoder stream etc. and allows finer grade control over the whole connection
public class Http3Hub(QuicSession quic)
{
    readonly QuicSession quic = quic;
}
