namespace Samicpp.Http.Http2;

using System.Collections.Concurrent;
using Samicpp.Http;
using hpack; // TODO: implement my own hpack decoder/encoder

public class Http2Status(
    int streamID,
    bool end_headers, bool end_stream,
    bool self_end_headers, bool self_end_stream,
    Dictionary<string, List<string>> headers
) {
    public int streamID = streamID;
    public bool end_headers = end_headers;
    public bool end_stream = end_stream;
    public bool self_end_headers = self_end_headers;
    public bool self_end_stream = self_end_stream;
    public List<byte> body = [];
    public List<byte> head = [];
    public Dictionary<string, List<string>> headers = headers;
}

public class Http2Connection(IDualSocket socket, Http2Settings settings)
{
    public readonly ConcurrentDictionary<int, Http2Status> streams = new();
    readonly IDualSocket socket = socket;
    public Http2Settings settings = settings;
    private int window = settings.initial_window_size ?? 16384;
    public bool goaway = false;


    protected void SendFrame(int streamID, byte type, byte flags, byte[] priority, byte[] payload, byte[] padding) => socket.Write(Http2Frame.Create(streamID, type, flags, priority, payload, padding));
    protected async Task SendFrameAsync(int streamID, byte type, byte flags, byte[] priority, byte[] payload, byte[] padding) => await socket.WriteAsync(Http2Frame.Create(streamID, type, flags, priority, payload, padding));

    public void SendData(int streamID, bool end, byte[] payload) { }

    public void SendHeaders(int streamID, bool end, byte[] headers) { }

    // public void SendPriority() { }

    public void SendRstStream(int streamID, int code) { }

    public void SendSettings() { } // ack

    public void SendSettings(Http2Settings settings) { }

    // public void SendPushPromise() { }

    public void SendPing(byte[] payload) { }

    public void SendPong(byte[] payload) { }

    public void SendGoaway(int streamID, int code, byte[] message) { }

    public void SendWindowUpdate(int streamID, int size) { }
    
    // public void SendContinuation() { }
}
