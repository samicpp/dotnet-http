namespace Samicpp.Http.Http2;

using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using Samicpp.Http;
using Samicpp.Http.Http2.Hpack;
// using Samicpp.Http.Http2.Hpack;

public class Http2Exception(string? message, Exception? other) : HttpException(message, other)
{
    public sealed class StreamClosed(string? message) : Http2Exception(message, null);
    public sealed class StreamDoesntExist(string? message) : Http2Exception(message, null);
    public sealed class MalformedFrame(Exception? err = null) : Http2Exception(null, err);
    public sealed class HeadersNotSent(string? message) : Http2Exception(message, null);
    public sealed class InvalidMagicSequence(string? message) : Http2Exception(message, null);
    public sealed class ProtocolError(string? message, Exception? err = null) : Http2Exception(message, err);
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

public class Http2Session(IDualSocket socket, Http2Settings settings, EndPoint? endPoint = null) : IDisposable, IAsyncDisposable
{
    protected readonly ConcurrentDictionary<int, Http2Status> streams = new();
    readonly IDualSocket socket = socket;
    // public IDualSocket Conn { get => socket; }
    public EndPoint? EndPoint => endPoint;
    public Http2Settings settings = settings;
    private int window = settings.initial_window_size ?? 16384;
    public Http2Frame? goaway = null;
    public Hpack.Encoder hpacke = new(settings.header_table_size ?? 4096);
    public Hpack.Decoder hpackd = new(settings.header_table_size ?? 4096);
    private readonly ConcurrentQueue<Http2Frame> que = new();

    public bool IsSecure { get => socket.IsSecure; }

    readonly SemaphoreSlim sendLock = new(1, 1);
    readonly SemaphoreSlim handleLock = new(1, 1);
    readonly SemaphoreSlim streamLock = new(1, 1);
    readonly SemaphoreSlim hpackeLock = new(1, 1);
    readonly SemaphoreSlim hpackdLock = new(1, 1);
    readonly SemaphoreSlim writeLock = new(1, 1);
    readonly SemaphoreSlim readLock = new(1, 1);


    public static readonly byte[] MAGIC = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"u8.ToArray();
    public void Init()
    {
        readLock.Wait();
        try
        {
            var mag = socket.ReadCertain(MAGIC.Length);
            if (MAGIC.SequenceEqual(mag)) { }
            else throw new Http2Exception.InvalidMagicSequence("");
        }
        finally
        {
            readLock.Release();
        }
    }
    public async Task InitAsync()
    {
        await readLock.WaitAsync();
        try
        {
            var mag = await socket.ReadCertainAsync(MAGIC.Length);
            if (MAGIC.SequenceEqual(mag)) { }
            else throw new Http2Exception.InvalidMagicSequence("");
        }
        finally
        {
            readLock.Release();
        }
    }

    private Http2Frame Readone(bool useLock = true)
    {
        if (useLock) readLock.Wait();
        try
        {
            byte[] head = socket.ReadCertain(9);
            int len = head[0] << 16 | head[1] << 8 | head[2];
            byte[] payl = socket.ReadCertain(len);
            byte[] frame = new byte[head.Length + payl.Length];
            int i = 0;
            foreach (var b in head) frame[i++] = b;
            foreach (var b in payl) frame[i++] = b;
            return Http2Frame.Parse(frame);
        }
        finally
        {
            if (useLock) readLock.Release();
        }
    }
    private async Task<Http2Frame> ReadoneAsync(bool useLock = true)
    {
        if (useLock) await readLock.WaitAsync();
        try
        {
            byte[] head = await socket.ReadCertainAsync(9);
            int len = head[0] << 16 | head[1] << 8 | head[2];
            byte[] payl = await socket.ReadCertainAsync(len);
            byte[] frame = new byte[head.Length + payl.Length];
            int i = 0;
            foreach (var b in head) frame[i++] = b;
            foreach (var b in payl) frame[i++] = b;
            return Http2Frame.Parse(frame);
        }
        finally
        {
            if (useLock) readLock.Release();
        }
    }

    public Http2Frame ReadOne()
    {
        if (!que.IsEmpty)
        {
            que.TryDequeue(out var f);
            return f;
        }
        else
        {
            return Readone();
        }
    }
    public async Task<Http2Frame> ReadOneAsync()
    {
        if (!que.IsEmpty)
        {
            que.TryDequeue(out var f);
            return f;
        }
        else
        {
            return await ReadoneAsync();
        }
    }

    public List<Http2Frame> ReadAll()
    {
        List<Http2Frame> frames = [];

        while (!que.IsEmpty)
        {
            que.TryDequeue(out var f);
            frames.Add(f);
        }
        if (frames.Count > 0) return frames;

        readLock.Wait();
        try
        {
            var rframes = Http2Frame.Split(socket.ReadAll().ToArray());
            foreach (var rframe in rframes)
            {
                var frame = Http2Frame.Parse(rframe);
                frames.Add(frame);
            }
        }
        finally
        {
            readLock.Release();
        }

        return frames;
    }
    public async Task<List<Http2Frame>> ReadAllAsync()
    {
        List<Http2Frame> frames = [];

        while (!que.IsEmpty)
        {
            que.TryDequeue(out var f);
            frames.Add(f);
        }
        if (frames.Count > 0) return frames;

        await readLock.WaitAsync();
        try
        {
            var rframes = Http2Frame.Split((await socket.ReadAllAsync()).ToArray());
            foreach (var rframe in rframes)
            {
                var frame = Http2Frame.Parse(rframe);
                frames.Add(frame);
            }
        }
        finally
        {
            readLock.Release();
        }

        return frames;
    }

    public Http2Status? GetStream(int streamID, bool locking = true)
    {
        if (locking) streamLock.Wait();
        try
        {
            return streams.GetValueOrDefault(streamID);
        }
        finally
        {
            if (locking) streamLock.Release();
        }
    }
    public async Task<Http2Status?> GetStreamAsync(int streamID, bool locking = true)
    {
        if (locking) await streamLock.WaitAsync();
        try
        {
            return streams.GetValueOrDefault(streamID);
        }
        finally
        {
            if (locking) streamLock.Release();
        }
    }
    public void SetStream(Http2Status status)
    {
        streamLock.Wait();
        try
        {
            streams[status.streamID] = status;
        }
        finally
        {
            streamLock.Release();
        }
    }
    public async Task SetStreamAsync(Http2Status status)
    {
        await streamLock.WaitAsync();
        try
        {
            streams[status.streamID] = status;
        }
        finally
        {
            streamLock.Release();
        }
    }

    public List<int> Handle(List<Http2Frame> frames)
    {
        List<int> opened = [];
        foreach (var frame in frames)
        {
            int? id = Handle(frame);
            if (id != null) opened.Add((int)id);
        }
        return opened;
    }
    public async Task<List<int>> HandleAsync(List<Http2Frame> frames)
    {
        List<int> opened = [];
        foreach (var frame in frames)
        {
            int? id = await HandleAsync(frame);
            if (id != null) opened.Add((int)id);
        }
        return opened;
    }
    public int? Handle(Http2Frame frame)
    {
        int? opened = null;
        handleLock.Wait();
        streamLock.Wait();
        try
        {
            Http2Status? stream;
            switch (frame.type)
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

                        SendWindowUpdate(0, frame.Payload.Length);
                        SendWindowUpdate(frame.streamID, frame.Payload.Length);
                    }
                    else
                    {
                        throw new Http2Exception.ProtocolError("Data frame sent for nonexistent stream");
                    }
                    break;

                case Http2FrameType.Headers:
                    if (streams.TryGetValue(frame.streamID, out stream) && !stream.end_stream)
                    {
                        // throw new Http2Exception.ProtocolError("Header frame sent for existing stream");
                        stream.head.Clear();
                        stream.head.AddRange(frame.Payload);

                        if (stream.end_headers)
                        {
                            stream.headers = hpackd.Decode([.. stream.head]);
                        }

                        streams[frame.streamID] = stream;
                        opened = frame.streamID;
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
                        opened = frame.streamID;
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
                        throw new Http2Exception.ProtocolError("Continuation frame sent for nonexistent stream");
                    }
                    break;

                case Http2FrameType.Settings:
                    if (frame.streamID == 0 && frame.flags == 0)
                    {
                        settings = new(
                            frame.settings.header_table_size ?? settings.header_table_size,
                            frame.settings.enable_push ?? settings.enable_push,
                            frame.settings.max_concurrent_streams ?? settings.max_concurrent_streams,
                            frame.settings.initial_window_size ?? settings.initial_window_size,
                            frame.settings.max_frame_size ?? settings.max_frame_size,
                            frame.settings.max_header_list_size ?? settings.max_header_list_size
                        );
                        SendSettings();
                    }
                    break;

                case Http2FrameType.Goaway: goaway = frame; break;

                case Http2FrameType.RstStream:
                    if (streams.TryGetValue(frame.streamID, out stream))
                    {
                        stream.reset = true;
                        streams[frame.streamID] = stream;
                    }
                    else
                    {
                        throw new Http2Exception.ProtocolError("Cant reset stream that doesnt exist");
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
                        throw new Http2Exception.ProtocolError("Window Update was not for connection or existing stream");
                    }
                    break;

                default: break;
            }
        }
        finally
        {
            handleLock.Release();
            streamLock.Release();
        }

        return opened;
    }
    public async Task<int?> HandleAsync(Http2Frame frame)
    {
        int? opened = null;
        await handleLock.WaitAsync();
        await streamLock.WaitAsync();
        try
        {
            Http2Status? stream;
            switch (frame.type)
            {
                case Http2FrameType.Ping:
                    if ((frame.flags & 1) == 0) await SendPongAsync(frame.Payload.ToArray());
                    break;

                case Http2FrameType.Data:
                    if (streams.TryGetValue(frame.streamID, out stream) && !stream.end_stream)
                    {
                        stream.body.AddRange(frame.Payload);
                        stream.end_stream = (frame.flags & 1) != 0;
                        streams[frame.streamID] = stream;

                        await SendWindowUpdateAsync(0, frame.Payload.Length);
                        await SendWindowUpdateAsync(frame.streamID, frame.Payload.Length);
                    }
                    else
                    {
                        throw new Http2Exception.ProtocolError("Data frame sent for nonexistent stream");
                    }
                    break;

                case Http2FrameType.Headers:
                    if (streams.TryGetValue(frame.streamID, out stream))
                    {
                        // throw new Http2Exception.ProtocolError("Header frame sent for existing stream");
                        stream.head.Clear();
                        stream.head.AddRange(frame.Payload);

                        if (stream.end_headers)
                        {
                            stream.headers = hpackd.Decode([.. stream.head]);
                        }

                        streams[frame.streamID] = stream;
                        opened = frame.streamID;
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
                        opened = frame.streamID;
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
                        throw new Http2Exception.ProtocolError("Continuation frame sent for nonexistent stream");
                    }
                    break;

                case Http2FrameType.Settings:
                    if (frame.streamID == 0 && frame.flags == 0)
                    {
                        settings = new(
                            frame.settings.header_table_size ?? settings.header_table_size,
                            frame.settings.enable_push ?? settings.enable_push,
                            frame.settings.max_concurrent_streams ?? settings.max_concurrent_streams,
                            frame.settings.initial_window_size ?? settings.initial_window_size,
                            frame.settings.max_frame_size ?? settings.max_frame_size,
                            frame.settings.max_header_list_size ?? settings.max_header_list_size
                        );
                        await SendSettingsAsync();
                    }
                    break;

                case Http2FrameType.Goaway: goaway = frame; break;

                case Http2FrameType.RstStream:
                    if (streams.TryGetValue(frame.streamID, out stream))
                    {
                        stream.reset = true;
                        streams[frame.streamID] = stream;
                    }
                    else
                    {
                        throw new Http2Exception.ProtocolError("Cant reset stream that doesnt exist");
                    }
                    break;

                case Http2FrameType.WindowUpdate:
                    var pay = frame.Payload;
                    if (pay.Length != 4) break;
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
                        throw new Http2Exception.ProtocolError("Window Update was not for connection or existing stream");
                    }
                    break;

                default: break;
            }
        }
        finally
        {
            handleLock.Release();
            streamLock.Release();
        }

        return opened;
    }

    protected void SendFrame(int streamID, byte type, byte flags, byte[] priority, byte[] payload, byte[] padding)
    {
        writeLock.Wait();
        try
        {
            socket.Write(Http2Frame.Create(streamID, type, flags, priority, payload, padding));
        }
        finally
        {
            writeLock.Release();
        }
    }
    protected async Task SendFrameAsync(int streamID, byte type, byte flags, byte[] priority, byte[] payload, byte[] padding)
    {
        await writeLock.WaitAsync();
        try
        {
            await socket.WriteAsync(Http2Frame.Create(streamID, type, flags, priority, payload, padding));
        }
        finally
        {
            writeLock.Release();
        }
    }

    public void SendData(int streamID, bool end, byte[] payload, bool priority = false)
    {
        // Http2Status stream;
        sendLock.Wait(); bool sendLocked = true;
        streamLock.Wait(); bool streamLocked = true;
        // readLock.Wait(); bool readLocked = true;
        try
        {
            if (streams.TryGetValue(streamID, out var status))
            {
                if (!status.self_end_headers) throw new Http2Exception.HeadersNotSent("");
                else if (status.self_end_stream) throw new Http2Exception.StreamClosed("");
                status.self_end_stream = end;
                // stream = status;
                streams[streamID] = status;
            }
            else
            {
                throw new Http2Exception.StreamDoesntExist("");
            }



            if (payload.Length <= 0)
            {
                if (end) SendFrame(streamID, 0, 1, [], [], []);
                return;
            }

            int index = 0;

            while (payload.Length - index > 0)
            {
                var mfs = settings.max_frame_size ?? 16384;
                var min = Math.Min(mfs, Math.Min(window, status.window));
                if (payload.Length - index <= min) break;

                if (min > 0)
                {
                    SendFrame(streamID, 0, 0, [], payload[index..(index + min)], []);
                }

                index += min;
                window -= min;
                status.window -= min;
                streams[streamID] = status;

                if (window <= 0 || status.window <= 0)
                {
                    streamLock.Release(); streamLocked = false;
                    if (!priority)
                    {
                        sendLock.Release(); sendLocked = false;
                    }
                    
                    while (window <= 0 || status.window <= 0 || goaway != null)
                    {
                        // await Task.Yield();
                        status = streams[streamID];
                        if (!status.reset) throw new Http2Exception.StreamClosed("reset while still sending data");
                    }
                    if (goaway != null) throw new Http2Exception.ConnectionClosed("closed while still sending data");

                    streamLock.Wait(); streamLocked = true;
                    status = streams[streamID];
                }

                // min = Math.Min(settings.max_frame_size ?? 16384, Math.Min(window, status.window));
            }

            int rem = payload.Length - index;
            SendFrame(streamID, 0, end ? (byte)1 : (byte)0, [], payload[index..], []);
            window -= rem;
            status.window -= rem;
            streams[streamID] = status;
        }
        finally
        {
            // if (readLocked) readLock.Release();
            if (sendLocked) sendLock.Release();
            if (streamLocked) streamLock.Release();
        }
    }
    public async Task SendDataAsync(int streamID, bool end, byte[] payload, bool priority = false)
    {
        // Http2Status stream;
        await sendLock.WaitAsync(); bool sendLocked = true;
        await streamLock.WaitAsync(); bool streamLocked = true;
        // readLock.Wait(); bool readLocked = true;
        try
        {
            if (streams.TryGetValue(streamID, out var status))
            {
                if (!status.self_end_headers) throw new Http2Exception.HeadersNotSent("");
                else if (status.self_end_stream) throw new Http2Exception.StreamClosed("");
                status.self_end_stream = end;
                // stream = status;
                streams[streamID] = status;
            }
            else
            {
                throw new Http2Exception.StreamDoesntExist("");
            }



            if (payload.Length <= 0)
            {
                if (end) await SendFrameAsync(streamID, 0, 1, [], [], []);
                return;
            }

            int index = 0;

            while (payload.Length - index > 0)
            {
                var mfs = settings.max_frame_size ?? 16384;
                var min = Math.Min(mfs, Math.Min(window, status.window));
                if (payload.Length - index <= min) break;

                if (min > 0)
                {
                    await SendFrameAsync(streamID, 0, 0, [], payload[index..(index + min)], []);
                }

                index += min;
                window -= min;
                status.window -= min;
                streams[streamID] = status;

                if (window <= 0 || status.window <= 0)
                {
                    streamLock.Release(); streamLocked = false;
                    if (!priority)
                    {
                        sendLock.Release(); sendLocked = false;
                    }
                    
                    while (window <= 0 || status.window <= 0 || goaway != null)
                    {
                        await Task.Yield();
                        status = streams[streamID];
                        if (status.reset) throw new Http2Exception.StreamClosed("reset while still sending data");
                    }
                    if (goaway != null) throw new Http2Exception.ConnectionClosed("closed while still sending data");
                    
                    await streamLock.WaitAsync(); streamLocked = true;
                    if (!sendLocked) { await sendLock.WaitAsync(); sendLocked = true; }
                    status = streams[streamID];
                }

                // min = Math.Min(settings.max_frame_size ?? 16384, Math.Min(window, status.window));
            }

            int rem = payload.Length - index;
            await SendFrameAsync(streamID, 0, end ? (byte)1 : (byte)0, [], payload[index..], []);
            window -= rem;
            status.window -= rem;
            streams[streamID] = status;
        }
        finally
        {
            // if (readLocked) readLock.Release();
            if (sendLocked) sendLock.Release();
            if (streamLocked) streamLock.Release();
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
                streams[streamID] = status;
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

        sendLock.Wait();
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
            sendLock.Release();
        }
    }
    private async Task FlowHeadersAsync(int streamID, bool end, byte[] headers)
    {
        // Console.WriteLine("sending headers");
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

        await sendLock.WaitAsync();
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
            sendLock.Release();
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

    public void SendGoaway(int streamID, int code, byte[] message) => SendFrame(0, 7, 0, [], [
        (byte)(streamID >> 24), (byte)(streamID >> 16), (byte)(streamID >> 8), (byte)streamID,
        (byte)(code >> 24), (byte)(code >> 16), (byte)(code >> 8), (byte)code,
        .. message,
    ], []);
    public async Task SendGoawayAsync(int streamID, int code, byte[] message) => await SendFrameAsync(0, 7, 0, [], [
        (byte)(streamID >> 24), (byte)(streamID >> 16), (byte)(streamID >> 8), (byte)streamID,
        (byte)(code >> 24), (byte)(code >> 16), (byte)(code >> 8), (byte)code,
        .. message,
    ], []);

    public void SendWindowUpdate(int streamID, int size) => SendFrame(streamID, 8, 0, [], [(byte)(size >> 24), (byte)(size >> 16), (byte)(size >> 8), (byte)size], []);
    public async Task SendWindowUpdateAsync(int streamID, int size) => await SendFrameAsync(streamID, 8, 0, [], [(byte)(size >> 24), (byte)(size >> 16), (byte)(size >> 8), (byte)size], []);

    // public void SendContinuation() { }

    public void Dispose()
    {
        try
        {
            // if (socket.CanWrite && goaway == null) SendGoaway(0, 0x2, "Dispose called before connection closed"u8.ToArray());
            // if (socket.CanWrite && goaway == null) SendGoaway(0, 0x0, []);
        }
        // catch (Exception) { }
        finally
        {
            socket.Dispose();
        }

    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            // if (socket.CanWrite && goaway == null) await SendGoawayAsync(0, 0x2, "Dispose called before connection closed"u8.ToArray());
            // if (socket.CanWrite && goaway == null) await SendGoawayAsync(0, 0x0, []);
        }
        // catch (Exception) { }
        finally
        {
            await socket.DisposeAsync();
        }

    }
}
