namespace Samicpp.Http.Http3;

using Samicpp.Http.Quic;

// one h3 "request"
public class Http3Session(Stream stream) //: IDualHttpSocket
{
    readonly Stream stream = stream;
}