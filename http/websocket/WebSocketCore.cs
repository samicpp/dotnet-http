namespace Samicpp.Http.WebSocket;

public readonly struct WebSocketFrame(
    byte[] raw,
    bool fin, int rsv, int opcode,
    bool masked, int len,
    ulong ext, Range mask,
    Range payload, WebSocketFrameType type
) {
    public readonly byte[] raw = raw;

    public readonly bool fin = fin;
    public readonly int rsv = rsv;
    public readonly int opcode = opcode;

    public readonly bool masked = masked;
    public readonly int len = len;

    public readonly ulong ext = ext;
    public readonly Range mask = mask;
    public readonly Range payload = payload;

    public readonly WebSocketFrameType type = type;

    // method instead of getter cause to not confuse users since it induces overhead
    public readonly byte[] GetPayload()
    {
        var pay = raw[payload];

        if (masked)
        {
            var key = raw[mask];
            for (int i = 0; i < pay.Length; i++) pay[i] ^= key[i % 4];
        }

        return pay;
    }

    public static List<byte[]> Split(byte[] bytes)
    {
        int index = 0;
        List<byte[]> frames = [];

        try
        {
            while (index < bytes.Length - 1)
            {
                int len = bytes[index] & 0x7f;
                bool masked = (bytes[index + 1] & 0x80) != 0;
                ulong ext = 0;
                if (len == 126) ext = (ulong)bytes[2] << 8 | (ulong)bytes[3];
                else if (len == 127) ext = (ulong)bytes[2] << 56 | (ulong)bytes[3] << 48 | (ulong)bytes[4] << 40 | (ulong)bytes[5] << 32 | (ulong)bytes[6] << 24 | (ulong)bytes[7] << 16 | (ulong)bytes[8] << 8 | (ulong)bytes[9];

                int length = 2;
                if (masked) length += 4;
                if (ext != 0) length += (int)ext;
                else length += len;

                byte[] frame = new byte[length];
                frames.Add(frame);

                index += length;
            }
        }
        catch (Exception)
        {
            //
        }
        return frames;
    }
    public static WebSocketFrame Parse(byte[] bytes)
    {
        bool fin = (bytes[0] & 0x80) != 0;
        int rsv = bytes[0] & 0x70;
        int opcode = bytes[0] & 0x0f;
        bool masked = (bytes[1] & 0x80) != 0;
        int len = bytes[1] & 0x7f;
        ulong ext = 0;
        var mask = 0..0;
        var payload = 0..0;

        int start = 2;

        if (len == 126)
        {
            ext = (ulong)bytes[start] << 8 | (ulong)bytes[start + 1];
            start += 2;
        }
        else if (len == 127)
        {
            ext = (ulong)bytes[start] << 56 | (ulong)bytes[start + 1] << 48 | (ulong)bytes[start + 2] << 40 | (ulong)bytes[start + 3] << 32 |
                  (ulong)bytes[start + 4] << 24 | (ulong)bytes[start + 5] << 16 | (ulong)bytes[start + 6] << 8 | (ulong)bytes[start + 7];
            start += 8;
        }

        if (masked)
        {
            mask = start..(start + 4);
            start += 4;
        }

        // no nuint indexing available
        if (ext != 0) payload = start..(start + (int)ext);
        else payload = start..len;

        var type = opcode switch
        {
            < 10 => (WebSocketFrameType)opcode,
            _ => WebSocketFrameType.Other,
        };

        return new(bytes, fin, rsv, opcode, masked, len, ext, mask, payload, type);
    }
    public static byte[] Create(bool fin, byte opcode, Span<byte> payload)
    {
        byte[] raw;
        int offset = 0;
        if (payload.Length > 0xffff)
        {
            raw = new byte[10 + payload.Length];
            raw[offset++] = (byte)((fin ? 0x80 : 0x00) & 0x70 & opcode);
            raw[offset++] = 127;

            raw[offset++] = (byte)(payload.Length >> 56);
            raw[offset++] = (byte)(payload.Length >> 48);
            raw[offset++] = (byte)(payload.Length >> 40);
            raw[offset++] = (byte)(payload.Length >> 32);
            raw[offset++] = (byte)(payload.Length >> 24);
            raw[offset++] = (byte)(payload.Length >> 16);
            raw[offset++] = (byte)(payload.Length >> 8);
            raw[offset++] = (byte)payload.Length;

        }
        else if (payload.Length > 125)
        {
            raw = new byte[4 + payload.Length];
            raw[offset++] = (byte)((fin ? 0x80 : 0x00) & 0x70 & opcode);
            raw[offset++] = 126;

            raw[offset++] = (byte)(payload.Length >> 8);
            raw[offset++] = (byte)payload.Length;

        }
        else
        {
            raw = new byte[2 + payload.Length];
            raw[offset++] = (byte)((fin ? 0x80 : 0x00) & 0x70 & opcode);
            raw[offset++] = (byte)payload.Length;
        }

        payload.CopyTo(raw.AsSpan(offset));
        
        return raw;
    }
}

public enum WebSocketFrameType{
    Continuation,         // 0x0
    Text,                 // 0x1
    Binary,               // 0x2
    ConnectionClose,      // 0x8
    Ping,                 // 0x9
    Pong,                 // 0xA
    Other,                // 
}
