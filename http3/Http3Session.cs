namespace Samicpp.Http.Http3;

using Samicpp.Http.Quic;

public class Http3Client : HttpClient
{
    public Http3Client()
    {
        IsValid = true;
        Version = HttpVersion.Http3;
    }
    public string Scheme = "";
}

// one h3 "request"
public class Http3Session(Stream stream) //: IDualHttpSocket
{
    readonly Stream stream = stream;
}