namespace Samicpp.Http.Http2;


public class Http2Exception(string? message, Exception? other) : HttpException(message, other)
{
    public sealed class MalformedFrame(Exception? err = null): Http2Exception(null, err);
}
public readonly struct Http2Frame(
    byte[] raw,
    int length, int streamID, byte opcode, byte flags,
    Range priority, Range payload, Range padding,
    Http2FrameType type, Http2Settings settings
)
{
    public readonly byte[] raw = raw;

    public readonly int length = length;
    public readonly int streamID = streamID;
    public readonly byte opcode = opcode;
    public readonly byte flags = flags;

    public readonly Range priority = priority;
    public readonly Range payload = payload;
    public readonly Range padding = padding;

    public readonly Http2FrameType type = type;
    public readonly Http2Settings settings = settings;

    public readonly Span<byte> Priority { get => raw[priority]; }
    public readonly Span<byte> Payload { get => raw[payload]; }
    public readonly Span<byte> Padding { get => raw[padding]; }

    public static List<byte[]> Split(Span<byte> bytes)
    {
        List<byte[]> frames = [];

        int pos = 0;
        while (pos < bytes.Length)
        {
            int length = (bytes[pos] << 16 | bytes[pos + 1] << 8 | bytes[pos + 2]) + 9;
            byte[] frame = new byte[length];
            bytes[pos..length].CopyTo(frame);
            frames.Add(frame);
            pos += 9 + length;
        }

        return frames;
    }
    // TODO: add checks
    // TODO: make Parse return null instead of throw error
    public static Http2Frame Parse(byte[] bytes)
    {
        try
        {
            int length = bytes[0] << 16 | bytes[1] << 8 | bytes[2];
            int streamID = bytes[3] << 24 | bytes[4] << 16 | bytes[5] << 8 | bytes[6];
            byte opcode = bytes[7];
            byte flags = bytes[8];

            int offset = 0;

            int padLength = 0;
            if ((flags & 8) != 0)
            {
                padLength = bytes[offset];
                offset += 1;
            }

            Range priority = 0..0;
            if ((flags & 32) != 0) priority = (9 + offset)..(9 + offset + 5);

            Range payload = (9 + offset)..(9 + length - padLength);

            Range padding = (9 + length - padLength)..(9 + length);

            var type = opcode switch
            {
                < 10 => (Http2FrameType)opcode,
                _ => Http2FrameType.Unknown,
            };

            Http2Settings settings;
            if (opcode == 4) settings = Http2Settings.Parse(bytes[payload]) ?? new();
            else settings = new();


            return new(bytes, length, streamID, opcode, flags, priority, payload, padding, type, settings);
        }
        catch (Exception e)
        {
            throw new Http2Exception.MalformedFrame(e);
        }
    }

    public static byte[] Create(
        int streamID, byte opcode, byte flags, Span<byte> priority, Span<byte> payload, Span<byte> padding
    ){
        // List<byte> raw = [];

        int length = payload.Length;
        if ((flags & 8) != 0) length += padding.Length + 1;
        if ((flags & 32) != 0) length += priority.Length;
        byte[] raw = new byte[length];

        int offset = 0;

        raw[offset++] = (byte)(length >> 16);
        raw[offset++] = (byte)(length >> 8);
        raw[offset++] = (byte)length;

        raw[offset++] = (byte)(streamID >> 24);
        raw[offset++] = (byte)(streamID >> 16);
        raw[offset++] = (byte)(streamID >> 8);
        raw[offset++] = (byte)streamID;


        if ((flags & 8) != 0) raw[offset++] = (byte)padding.Length;
        if ((flags & 32) != 0) {
            priority.CopyTo(raw.AsSpan(offset));
            offset += priority.Length;
        }

        payload.CopyTo(raw.AsSpan(offset));
        offset += payload.Length;

        if ((flags & 8) != 0)
        {
            padding.CopyTo(raw.AsSpan(offset));
            // offset += padding.Length;
        }

        return raw;
    }

    public readonly byte[] ToBytes() => Create(streamID, opcode, flags, Priority, Payload, Padding);
}

public readonly struct Http2Settings(int? headerTableSize, int? enablePush, int? maxConcurrentStreams, int? initialWindowSize, int? maxFrameSize, int? maxHeaderListSize)
{
    public readonly int? header_table_size = headerTableSize;            //  0x1
    public readonly int? enable_push = enablePush;                       //  0x2
    public readonly int? max_concurrent_streams = maxConcurrentStreams;  //  0x3
    public readonly int? initial_window_size = initialWindowSize;        //  0x4
    public readonly int? max_frame_size = maxFrameSize;                  //  0x5
    public readonly int? max_header_list_size = maxHeaderListSize;       //  0x6

    public static Http2Settings? Parse(Span<byte> bytes)
    {
        if ((bytes.Length % 6) != 0) return null;
        // var s = new { h = new int?(), p = new int?(), s = new int?(), w = new int?(), f = new int?(), l = new int?() };
        int?[] s = new int?[6];

        for (int i = 0; i < bytes.Length; i += 6)
        {
            int name = bytes[i] << 8 | bytes[i + 1];
            int value = bytes[i + 2] << 24 | bytes[i + 3] << 16 | bytes[i + 4] << 8 | bytes[i + 5];
            if (name < 6) s[name] = value;
        }

        // return new(s.h, s.p, s.s, s.w, s.f, s.l);
        return new(s[0], s[1], s[2], s[3], s[4], s[5]);
    }
    public readonly List<byte> ToBytes()
    {
        List<byte> raw = [];

        if (header_table_size != null) raw.AddRange([0, 1, (byte)(header_table_size >> 24), (byte)(header_table_size >> 16), (byte)(header_table_size >> 8), (byte)header_table_size]);
        if (enable_push != null) raw.AddRange([0, 1, (byte)(enable_push >> 24), (byte)(enable_push >> 16), (byte)(enable_push >> 8), (byte)enable_push]);
        if (max_concurrent_streams != null) raw.AddRange([0, 1, (byte)(max_concurrent_streams >> 24), (byte)(max_concurrent_streams >> 16), (byte)(max_concurrent_streams >> 8), (byte)max_concurrent_streams]);
        if (initial_window_size != null) raw.AddRange([0, 1, (byte)(initial_window_size >> 24), (byte)(initial_window_size >> 16), (byte)(initial_window_size >> 8), (byte)initial_window_size]);
        if (max_frame_size != null) raw.AddRange([0, 1, (byte)(max_frame_size >> 24), (byte)(max_frame_size >> 16), (byte)(max_frame_size >> 8), (byte)max_frame_size]);
        if (max_header_list_size != null) raw.AddRange([0, 1, (byte)(max_header_list_size >> 24), (byte)(max_header_list_size >> 16), (byte)(max_header_list_size >> 8), (byte)max_header_list_size]);

        return raw;
    }
}

public enum Http2FrameType
{
    Data,          // 0x0
    Headers,       // 0x1
    Priority,      // 0x2
    RstStream,     // 0x3
    Settings,      // 0x4
    PushPromise,   // 0x5
    Ping,          // 0x6
    Goaway,        // 0x7
    WindowUpdate,  // 0x8
    Continuation,  // 0x9
    Unknown,       // >0x9
}