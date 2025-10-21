namespace Samicpp.Http.Http2;

using System.Collections.Concurrent;
using System.Threading.Tasks;
using Samicpp.Http;
using Samicpp.Http.Http2.Hpack;

// using Samicpp.Http.Http2.Hpack;

public class Http2Exception(string? message, Exception? other) : HttpException(message, other)
{
    public sealed class StreamClosed(string? message) : Http2Exception(message, null);
    public sealed class StreamDoesntExist(string? message) : Http2Exception(message, null);
    public sealed class MalformedFrame(Exception? err = null): Http2Exception(null, err);
    public sealed class HeadersNotSent(string? message) : Http2Exception(message, null);
}

public class Http2Status(
    int window, int streamID,
    bool end_headers, bool end_stream,
    bool self_end_headers, bool self_end_stream
) {
    public int window = window;
    public int streamID = streamID;
    public bool end_headers = end_headers;
    public bool end_stream = end_stream;
    public bool self_end_headers = self_end_headers;
    public bool self_end_stream = self_end_stream;
    public bool reset = false;
    public List<byte> body = [];
    public List<byte> head = [];
    // public Dictionary<string, List<string>> headers = headers;
    public List<(byte[] name, byte[] value)> headers = [];
}

public class Http2Connection(IDualSocket socket, Http2Settings settings)
{
    public readonly ConcurrentDictionary<int, Http2Status> streams = new();
    readonly IDualSocket socket = socket;
    public Http2Settings settings = settings;
    private int window = settings.initial_window_size ?? 16384;
    public Http2Frame? goaway = null;
    public Hpack.Encoder hpacke = new(settings.header_table_size ?? 4096);
    public Hpack.Decoder hpackd = new(settings.header_table_size ?? 4096);

    readonly SemaphoreSlim sendLock = new(1, 1);
    readonly SemaphoreSlim handleLock = new(1, 1);
    readonly SemaphoreSlim streamLock = new(1, 1);
    readonly SemaphoreSlim hpackeLock = new(1, 1);
    readonly SemaphoreSlim hpackdLock = new(1, 1);
    readonly SemaphoreSlim writeLock = new(1, 1);
    readonly SemaphoreSlim readLock = new(1, 1);


    public void Handle(Http2Frame[] frames)
    {
        handleLock.Wait();
        try
        {
            foreach(var frame in frames)
            {
                Http2Status? stream;
                switch(frame.type)
                {
                    case Http2FrameType.Ping:
                        if ((frame.flags & 1) == 0) SendPong(frame.Payload.ToArray());
                        break;
                    
                    case Http2FrameType.Data:
                        if (streams.TryGetValue(frame.streamID, out stream) && !stream.end_stream)
                        {
                            stream.body.AddRange(frame.Payload);
                            stream.end_stream = (frame.flags & 1) != 0;
                            streams[frame.streamID] = stream;
                        }
                        break;
                    
                    case Http2FrameType.Headers:
                        if (streams.TryGetValue(frame.streamID, out stream))
                        {

                        }
                        else
                        {
                            stream = new(settings.initial_window_size ?? 65535, frame.streamID, (frame.flags & 4) != 0, (frame.flags & 1) != 0, false, false);
                            stream.head.AddRange(frame.Payload);

                            if (stream.end_headers)
                            {
                                stream.headers = hpackd.Decode([.. stream.head]);
                            }

                            streams[frame.streamID] = stream;
                        }
                        break;

                    case Http2FrameType.Continuation:
                        if (streams.TryGetValue(frame.streamID, out stream) && !stream.end_headers && !stream.end_stream)
                        {
                            stream.head.AddRange(frame.Payload);
                            stream.end_stream = (frame.flags & 1) != 0;
                            stream.end_headers = (frame.flags & 4) != 0;

                            if (stream.end_headers)
                            {
                                stream.headers = hpackd.Decode([.. stream.head]);
                            }

                            streams[frame.streamID] = stream;
                        }
                        else
                        {
                            
                        }
                        break;

                    case Http2FrameType.Settings:
                        settings = new(
                            frame.settings.header_table_size ?? settings.header_table_size,
                            frame.settings.enable_push ?? settings.enable_push,
                            frame.settings.max_concurrent_streams ?? settings.max_concurrent_streams,
                            frame.settings.initial_window_size ?? settings.initial_window_size,
                            frame.settings.max_frame_size ?? settings.max_frame_size,
                            frame.settings.max_header_list_size ?? settings.max_header_list_size
                        );
                        SendSettings();
                        break;
                    
                    case Http2FrameType.Goaway: goaway = frame; break;

                    case Http2FrameType.RstStream: 
                        if (streams.TryGetValue(frame.streamID, out stream))
                        {
                            stream.reset = true;
                            streams[frame.streamID] = stream;
                        }
                        break;

                    case Http2FrameType.WindowUpdate:
                        var pay = frame.Payload;
                        int size = pay[0] << 24 | pay[1] << 16 | pay[2] << 8 | pay[3];

                        if (frame.streamID == 0)
                        {
                            window += size;
                        }
                        else if (streams.TryGetValue(frame.streamID, out stream))
                        {
                            stream.window += size;
                            streams[frame.streamID] = stream;
                        }
                        else
                        {
                            
                        }
                        break;
                    
                    default: break;
                };
            }
        }
        finally
        {
            handleLock.Release();
        }
    }

    protected void SendFrame(int streamID, byte type, byte flags, byte[] priority, byte[] payload, byte[] padding) => socket.Write(Http2Frame.Create(streamID, type, flags, priority, payload, padding));
    protected async Task SendFrameAsync(int streamID, byte type, byte flags, byte[] priority, byte[] payload, byte[] padding) => await socket.WriteAsync(Http2Frame.Create(streamID, type, flags, priority, payload, padding));

    public void SendData(int streamID, bool end, byte[] payload)
    {
        Http2Status stream;
        streamLock.Wait();
        try
        {
            if (streams.TryGetValue(streamID, out var status))
            {
                if (!status.self_end_headers) throw new Http2Exception.HeadersNotSent("");
                else if (status.self_end_stream) throw new Http2Exception.StreamClosed("");
                status.self_end_stream = end;
                stream = status;
            }
            else
            {
                throw new Http2Exception.StreamDoesntExist("");
            }
        }
        finally
        {
            // streamLock.Release();
        }
        
        sendLock.Wait();
        try
        {
            if (payload.Length <= 0)
            {
                if (end) SendFrame(streamID, 0, 1, [], [], []);
                return;
            }
            
            // ... something
        }
        finally
        {
            sendLock.Release();
            streamLock.Release();
        }
    }

    private void FlowHeaders(int streamID, bool end, byte[] headers)
    {
        int mfs = settings.max_frame_size ?? 16384;
        byte flags = end ? (byte)5 : (byte)4;
        
        streamLock.Wait();
        try
        {
            if (streams.TryGetValue(streamID, out var status))
            {
                if (status.self_end_headers) throw new Http2Exception.HeadersSent("");
                else if (status.self_end_stream) throw new Http2Exception.StreamClosed("");
                status.self_end_headers = true;
                status.self_end_stream = end;
            }
            else
            {
                throw new Http2Exception.StreamDoesntExist("");
            }
        }
        finally
        {
            streamLock.Release();
        }

        writeLock.Wait();
        try
        {
            if (headers.Length > mfs)
            {
                int index = 0;
                // while (headers.Length - index > mfs) ;
                SendFrame(streamID, 1, 0, [], headers[index..(index + mfs)], []);
                index += mfs;
                while (index + mfs < headers.Length)
                {
                    SendFrame(streamID, 9, 0, [], headers[index..(index + mfs)], []);
                    index += mfs;
                }
                SendFrame(streamID, 9, flags, [], headers[index..(index + mfs)], []);
            }
            else
            {
                SendFrame(streamID, 1, flags, [], headers, []);
            }
        }
        finally
        {
            writeLock.Release();
        }
    }
    private async Task FlowHeadersAsync(int streamID, bool end, byte[] headers)
    {
        int mfs = settings.max_frame_size ?? 16384;
        byte flags = end ? (byte)5 : (byte)4;
        
        await streamLock.WaitAsync();
        try
        {
            if (streams.TryGetValue(streamID, out var status))
            {
                if (status.self_end_headers) throw new Http2Exception.HeadersSent("");
                else if (status.self_end_stream) throw new Http2Exception.StreamClosed("");
                status.self_end_headers = true;
                status.self_end_stream = end;
            }
            else
            {
                throw new Http2Exception.StreamDoesntExist("");
            }
        }
        finally
        {
            streamLock.Release();
        }

        await writeLock.WaitAsync();
        try
        {
            if (headers.Length > mfs)
            {
                int index = 0;
                // while (headers.Length - index > mfs) ;
                await SendFrameAsync(streamID, 1, 0, [], headers[index..(index + mfs)], []);
                index += mfs;
                while (index + mfs < headers.Length)
                {
                    await SendFrameAsync(streamID, 9, 0, [], headers[index..(index + mfs)], []);
                    index += mfs;
                }
                await SendFrameAsync(streamID, 9, flags, [], headers[index..(index + mfs)], []);
            }
            else
            {
                await SendFrameAsync(streamID, 1, flags, [], headers, []);
            }
        }
        finally
        {
            writeLock.Release();
        }
    }
    public void SendHeaders(int streamID, bool end, HeaderEntry[] headers)
    {
        hpackeLock.Wait();
        try
        {
            byte[] head = hpacke.Encode(headers);
            FlowHeaders(streamID, end, head);
        }
        finally
        {
            hpackeLock.Release();
        }
    }
    public void SendHeaders(int streamID, bool end, (byte[], byte[])[] headers)
    {
        hpackeLock.Wait();
        try
        {
            byte[] head = hpacke.Encode(headers);
            FlowHeaders(streamID, end, head);
        }
        finally
        {
            hpackeLock.Release();
        }
    }
    public async Task SendHeadersAsync(int streamID, bool end, HeaderEntry[] headers)
    {
        await hpackeLock.WaitAsync();
        try
        {
            byte[] head = hpacke.Encode(headers);
            await FlowHeadersAsync(streamID, end, head);
        }
        finally
        {
            hpackeLock.Release();
        }
    }
    public async Task SendHeadersAsync(int streamID, bool end, (byte[], byte[])[] headers)
    {
        await hpackeLock.WaitAsync();
        try
        {
            byte[] head = hpacke.Encode(headers);
            await FlowHeadersAsync(streamID, end, head);
        }
        finally
        {
            hpackeLock.Release();
        }
    }


    // public void SendPriority() { }

    public void SendRstStream(int streamID, int code) => SendFrame(streamID, 8, 0, [], [(byte)(code >> 24), (byte)(code >> 16), (byte)(code >> 8), (byte)code], []);
    public async Task SendRstStreamAsync(int streamID, int code) => await SendFrameAsync(streamID, 8, 0, [], [(byte)(code >> 24), (byte)(code >> 16), (byte)(code >> 8), (byte)code], []);

    public void SendSettings() => SendFrame(0, 4, 1, [], [], []); // ack
    public async Task SendSettingsAsync() => await SendFrameAsync(0, 4, 1, [], [], []); // ack

    public void SendSettings(Http2Settings settings) => SendFrame(0, 4, 0, [], [.. settings.ToBytes()], []);
    public async Task SendSettingsAsync(Http2Settings settings) => await SendFrameAsync(0, 4, 0, [], [.. settings.ToBytes()], []);

    // public void SendPushPromise() { }

    public void SendPing(byte[] payload) => SendFrame(0, 6, 0, [], payload, []);
    public async Task SendPingAsync(byte[] payload) => await SendFrameAsync(0, 6, 0, [], payload, []);

    public void SendPong(byte[] payload) => SendFrame(0, 6, 1, [], payload, []);
    public async Task SendPongAsync(byte[] payload) => await SendFrameAsync(0, 6, 1, [], payload, []);

    public void SendGoaway(int streamID, int code, byte[] message) => SendFrame(0, 8, 0, [], [
        (byte)(streamID >> 24), (byte)(streamID >> 16), (byte)(streamID >> 8), (byte)streamID,
        (byte)(code >> 24), (byte)(code >> 16), (byte)(code >> 8), (byte)code,
        .. message,
    ], []);
    public async Task SendGoawayAsync(int streamID, int code, byte[] message) => await SendFrameAsync(0, 8, 0, [], [
        (byte)(streamID >> 24), (byte)(streamID >> 16), (byte)(streamID >> 8), (byte)streamID,
        (byte)(code >> 24), (byte)(code >> 16), (byte)(code >> 8), (byte)code,
        .. message,
    ], []);

    public void SendWindowUpdate(int streamID, int size) => SendFrame(streamID, 8, 0, [], [(byte)(size >> 24), (byte)(size >> 16), (byte)(size >> 8), (byte)size], []);
    public async Task SendWindowUpdateAsync(int streamID, int size) => await SendFrameAsync(streamID, 8, 0, [], [(byte)(size >> 24), (byte)(size >> 16), (byte)(size >> 8), (byte)size], []);
    
    // public void SendContinuation() { }
}
