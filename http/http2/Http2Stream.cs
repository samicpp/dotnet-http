namespace Samicpp.Http.Http2;

using Samicpp.Http;

public class Http2Stream(int streamID) //: IDualHttpSocket
{
    protected int streamID = streamID;
}