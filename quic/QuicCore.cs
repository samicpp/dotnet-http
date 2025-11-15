namespace Samicpp.Http.Quic;

// http://datatracker.ietf.org/doc/html/rfc9000


// 17 #name-packet-formats
public interface IQuicPacket
{
    public byte HeaderForm { get; }
    public bool FixedBit { get; init; }
}

// 17.2 Table 5 #long-packet-types
public enum QuicPacketType : byte
{
    Initial = 0b00,    // 17.2.2
    ZeroRtt = 0b01,    // 17.2.3
    Handshake = 0b10,  // 17.2.4
    Retry = 0b11,      // 17.2.5
}

// 17.2 #name-long-header-packets
public readonly struct QuicLongPacket(): IQuicPacket
{
    public byte HeaderForm { get; } = 1;
    public bool FixedBit { get; init; } // 17.2.1 // if 0 then version negotiation packet
    public QuicPacketType Type { get; init; }
    public byte TypeSpecific { get; init; } // Type-Specific Bits
    public uint Version { get; init; }
    public byte DciLength { get; init; } // Destination Connection ID Length
    public byte[] Dci { get; init; } = []; // Destination Connection ID
    public byte SciLength { get; init; } // Source Connection ID
    public byte[] Sci { get; init; } = []; // Source Connection ID
    public byte[] TsPayload { get; init; } = []; // Type-Specific Payload 

    public static QuicLongPacket Parse(byte[] bytes)
    {
        bool fixedBit = (bytes[0] & 0b0100_0000) != 0;
        QuicPacketType type = (QuicPacketType)((bytes[0] & 0b0011_0000) >> 4);
        byte typeSpecific = (byte)(bytes[0] & 0b1111);
        uint version = (uint)bytes[1] << 24 | (uint)bytes[2] << 16 | (uint)bytes[3] << 8 | bytes[4];
        byte dcil = bytes[5];
        byte[] dci = bytes[6..(dcil + 6)];
        byte scil = bytes[dcil + 6];
        byte[] sci = bytes[(dcil + 7)..(scil + dcil + 7)];
        byte[] payload = bytes[(scil + dcil + 7)..];

        return new()
        {
            FixedBit = fixedBit,
            Type = type,
            TypeSpecific = typeSpecific,
            Version = version,
            DciLength = dcil,
            Dci = dci,
            SciLength = scil,
            Sci = sci,
            TsPayload = payload,
        };
    }
    public static byte[] Create(bool fixedBit, QuicPacketType type, byte typeSpecific, uint version, byte[] dci, byte[] sci, byte[] tsPayload)
    {
        byte[] packet = new byte[tsPayload.Length + sci.Length + dci.Length + 7];
        int pos = 0;

        packet[pos] |= 0b1000_0000;
        packet[pos] |= fixedBit ? (byte)0b0100_0000 : (byte)0;
        packet[pos] |= (byte)((int)type << 4);
        packet[pos] |= (byte)(typeSpecific & 0b1111);
        pos +=1 ;
        packet[pos++] = (byte)(version >> 24);
        packet[pos++] = (byte)(version >> 16);
        packet[pos++] = (byte)(version >> 8);
        packet[pos++] = (byte)version;
        packet[pos++] = (byte)dci.Length;
        foreach (var b in dci) packet[pos++] = b;
        packet[pos++] = (byte)sci.Length;
        foreach (var b in sci) packet[pos++] = b;
        foreach (var b in tsPayload) packet[pos++] = b;

        return packet;
    }
}

// 17.3.1 #name-1-rtt-packet
public readonly struct QuicShortPacket(): IQuicPacket
{
    public byte HeaderForm { get; } = 0;
    public bool FixedBit { get; init; }
    public bool SpinBit { get; init; }
    public byte Reserved { get; init; }
    public bool KeyPhase { get; init; }
    public byte PacketNumberLength { get; init; }
    public byte[] Dci { get; init; } = []; // Destination Connection ID
    public uint PacketNumber { get; init; }
    public byte[] PacketPayload { get; init; } = [];

    public static QuicShortPacket Parse(int DciLength, byte[] bytes)
    {
        bool spin = (bytes[0] & 0b0010_0000) != 0;
        byte reserved = (byte)((bytes[0] & 0b0001_1000) >> 3);
        bool keyphase = (bytes[0] & 0b0000_0100) != 0;
        int pnLength = (bytes[0] & 0b0000_0011) + 1;
        byte[] dci = bytes[1..(DciLength + 1)];
        uint pn = 0;
        for (int i = 0; i < pnLength; i++) pn |= (uint)bytes[i + DciLength + 1] << (8 * (pnLength - 1 - i));
        byte[] payload = bytes[(DciLength + pnLength + 1)..];

        return new()
        {
            FixedBit = true,
            SpinBit = spin,
            Reserved = reserved,
            KeyPhase = keyphase,
            PacketNumberLength = (byte)pnLength,
            Dci = dci,
            PacketNumber = pn,
            PacketPayload = payload,
        };
    }

    public static byte[] Create(bool spin, byte reserved, bool keyphase, uint packetNumber, byte[] dci, byte[] payload)
    {
        byte[] pn;
        
        var pn0 = (byte)(packetNumber >> 24);
        var pn1 = (byte)(packetNumber >> 16);
        var pn2 = (byte)(packetNumber >> 8);
        var pn3 = (byte)packetNumber;

        if (pn0 != 0) pn = [pn0, pn1, pn2, pn3];
        else if (pn1 != 0) pn = [pn1, pn2, pn3];
        else if (pn2 != 0) pn = [pn2, pn3];
        else pn = [pn3];

        byte[] packet = new byte[payload.Length + dci.Length + pn.Length + 1];
        int pos = 0;

        packet[pos] |= spin ? (byte)0b0010_0000 : (byte)0;
        packet[pos] |= (byte)((reserved & 0b11) << 3);
        packet[pos] |= keyphase ? (byte)0b0000_0100 : (byte)0;
        packet[pos] |= (byte)(pn.Length - 1);
        pos += 1;
        foreach (var b in dci) packet[pos++] = b;
        foreach (var b in pn) packet[pos++] = b;
        foreach (var b in payload) packet[pos++] = b;

        return packet;
    }
}



// 12.4 #section-12.4

// I: Initial (Section 17.2.2)
// H: Handshake (Section 17.2.4)
// 0: 0-RTT (Section 17.2.3)
// 1: 1-RTT (Section 17.3.1)
// ih: Only a CONNECTION_CLOSE frame of type 0x1c can appear in Initial or Handshake packets.

// N: Packets containing only frames with this marking are not ack-eliciting; see Section 13.2.
// C: Packets containing only frames with this marking do not count toward bytes in flight for congestion control purposes; see [QUIC-RECOVERY].
// P: Packets containing only frames with this marking can be used to probe new network paths during connection migration; see Section 9.1.
// F: The contents of frames with this marking are flow controlled; see Section 4.

public enum QuicFrameType : ulong
{
    Padding = 0x00,                 // 19.1  | IH01 | NP
    Ping = 0x01,                    // 19.2  | IH01
    Ack = 0x02,                     // 19.3  | IH_1 | NC
    AckEcn = 0x03,                  // 19.3  | IH_1 | NC
    ResetStream = 0x04,             // 19.4  | __01
    StopSending = 0x05,             // 19.5  | __01
    Crypto = 0x06,                  // 19.6  | IH_1
    NewToken = 0x07,                // 19.7  | ___1
    Stream = 0x08, // 0x08 - 0x0f   // 19.8  | __01 | F
    MaxData = 0x10,                 // 19.9  | __01
    MaxStreamData = 0x11,           // 19.10 | __01
    MaxStreamsBidi = 0x12,          // 19.11 | __01
    MaxStreamsUni = 0x13,           // 19.11 | __01
    DataBlocked = 0x14,             // 19.12 | __01
    StreamDataBlocked = 0x15,       // 19.13 | __01
    StreamsBlockedBidi = 0x16,      // 19.14 | __01
    StreamsBlockedUni = 0x17,       // 19.14 | __01
    NewConnectionId = 0x18,         // 19.15 | __01 | P
    RetireConnectionId = 0x19,      // 19.16 | __01
    PathChallenge = 0x1a,           // 19.17 | __01 | P
    PathResponse = 0x1b,            // 19.18 | ___1 | P
    ConnectionCloseQuic = 0x1c,     // 19.19 | ih01 | N
    ConnectionCloseApp = 0x1d,      // 19.19 | ih01 | N
    HandshakeDone = 0x1e,           // 19.20 | ___1
    Unsupported,
}

// 12.4 Figure 12 #name-generic-frame-layout
public interface IQuicFrame
{
    QuicFrameType Type { get; }

    public static long VarintFrom(ref int offset, byte[] bytes)
    {
        long first = bytes[offset++];
        long len = (first & 0b1100_0000) >> 6;
        long varint = len switch
        {
            0 => first & 0b0011_1111L,
            1 => (first & 0b0011_1111L) << 8 | bytes[offset++],
            2 => (first & 0b0011_1111L) << 24 | (long)bytes[offset++] << 16 | (long)bytes[offset++] << 8 | bytes[offset++],
            4 => (first & 0b0011_1111L) << 56 | (long)bytes[offset++] << 48 | (long)bytes[offset++] << 40 | (long)bytes[offset++] << 32 | (long)bytes[offset++] << 24 | (long)bytes[offset++] << 16 | (long)bytes[offset++] << 8 | bytes[offset++],
            _ => 0, // impossible outcome
        };
        return varint;
    }
    public static byte[] EncodeVarint(ulong number)
    {
        // number &= 0b00111111_11111111_11111111_11111111_11111111_11111111_11111111_11111111L;
        byte[] varint;

        if (number < 0b0011_1111) varint = [(byte)number];
        else if (number < 0b0011_1111_1111_1111) varint = [(byte)((number >> 8) | 0b0100_000), (byte)number];
        else if (number < 0b0011_1111_1111_1111_1111_1111_1111_1111) varint = [(byte)((number >> 24) | 0b1000_000), (byte)(number >> 16), (byte)(number >> 8), (byte)number];
        else varint = [(byte)((number >> 56) | 0b1100_000), (byte)(number >> 48), (byte)(number >> 40), (byte)(number >> 32), (byte)(number >> 24), (byte)(number >> 16), (byte)(number >> 8), (byte)number];

        return varint;
    }
    public static List<IQuicFrame> ParseAll(byte[] bytes)
    {
        int offset = 0;
        List<IQuicFrame> frames = [];
        
        while (offset < bytes.Length)
        {
            IQuicFrame frame = Parse(ref offset, bytes) ?? throw new InvalidDataException("Frame(s) not valid");
            frames.Add(frame);
        }

        return frames;
    }
    public static IQuicFrame? Parse(ref int offset, byte[] bytes) // this will be integrated into ParseAll
    {
        long varint = VarintFrom(ref offset, bytes);
        /*QuicFrameType type = varint switch
        {
            0 => QuicFrameType.Padding,
            1 => QuicFrameType.Ping,
            2 => QuicFrameType.Ack,
            3 => QuicFrameType.AckEcn,
            >= 8 and <= 15 => QuicFrameType.Stream,

            _ => QuicFrameType.Unsupported,
        };*/

        return varint switch
        {
            0 => QuicPadding.Parse(ref offset, bytes),
            1 => QuicPing.Parse(ref offset, bytes),
            2 => QuicAck.Parse(ref offset, bytes, false),
            3 => QuicAck.Parse(ref offset, bytes, true),
            4 => QuicResetStream.Parse(ref offset, bytes),
            5 => QuicStopSending.Parse(ref offset, bytes),
            6 => QuicCrypto.Parse(ref offset, bytes),
            7 => QuicNewToken.Parse(ref offset, bytes),
            >= 8 and <= 15 => QuicStreamFrame.Parse(ref offset, bytes, (byte)varint),
            16 => QuicMaxData.Parse(ref offset, bytes),
            17 => QuicMaxStreamData.Parse(ref offset, bytes),
            18 => QuicMaxStreams.Parse(ref offset, bytes, true),
            19 => QuicMaxStreams.Parse(ref offset, bytes, false),
            20 => QuicDataBlocked.Parse(ref offset, bytes),
            21 => QuicStreamDataBlocked.Parse(ref offset, bytes),
            22 => QuicStreamBlocked.Parse(ref offset, bytes, true),
            23 => QuicStreamBlocked.Parse(ref offset, bytes, false),
            24 => QuicNewConnectionId.Parse(ref offset, bytes),
            25 => QuicRetireConnectionId.Parse(ref offset, bytes),
            26 => QuicPathChallenge.Parse(ref offset, bytes),
            27 => QuicPathResponse.Parse(ref offset, bytes),
            28 => QuicConnectionClose.Parse(ref offset, bytes, false),
            29 => QuicConnectionClose.Parse(ref offset, bytes, true),
            30 => QuicHandshakeDone.Parse(ref offset, bytes),

            _ => null,
        };
    }
}


// 19.1 #name-padding-frames
public readonly struct QuicPadding() : IQuicFrame // 0x00 -> 0
{
    public QuicFrameType Type { get; } = QuicFrameType.Padding;

    public static QuicPadding Parse(ref int offset, byte[] bytes)
    {
        return new();
    }
}

// 19.2 #name-ping-frames
public readonly struct QuicPing() : IQuicFrame // 0x01 -> 1
{
    public QuicFrameType Type { get; } = QuicFrameType.Ping;

    public static QuicPing Parse(ref int offset, byte[] bytes)
    {
        return new();
    }
}

// 19.3 #name-ack-frames
public readonly struct QuicAck(bool ecn) : IQuicFrame // 0x02 - 0x03 -> 2 - 3
{
    public QuicFrameType Type { get; init; } = !ecn ? QuicFrameType.Ack : QuicFrameType.AckEcn;
    public long Largest { get; init; } // varint
    public long Delay { get; init; } // varint
    public long RangeCount { get; init; } // varint
    public long FirstRange { get; init; } // varint
    public List<QuicAckRange> Ranges { get; init; } = [];

    // 19.3.2 #name-ecn-counts
    public long? Ect0 { get; init; } = null;
    public long? Ect1 { get; init; } = null;
    public long? EctCe { get; init; } = null;

    public static QuicAck Parse(ref int offset, byte[] bytes, bool ecn)
    {
        long largest = IQuicFrame.VarintFrom(ref offset, bytes);
        long delay = IQuicFrame.VarintFrom(ref offset, bytes);
        long rangeCount = IQuicFrame.VarintFrom(ref offset, bytes);
        long firstRange = IQuicFrame.VarintFrom(ref offset, bytes);
        List<QuicAckRange> ranges = [];

        for (long i = 0; i < rangeCount; i++)
        {
            long gap = IQuicFrame.VarintFrom(ref offset, bytes);
            long rangeLength = IQuicFrame.VarintFrom(ref offset, bytes);
            ranges.Add(new()
            {
                Gap = gap,
                RangeLength = rangeLength,
            });
        }

        long? ect0 = null;
        long? ect1 = null;
        long? ectce = null;

        if (ecn)
        {
            ect0 = IQuicFrame.VarintFrom(ref offset, bytes);
            ect1 = IQuicFrame.VarintFrom(ref offset, bytes);
            ectce = IQuicFrame.VarintFrom(ref offset, bytes);
        }
        
        return new(ecn)
        {
            Largest = largest,
            Delay = delay,
            RangeCount = rangeCount,
            FirstRange = firstRange,

            Ect0 = ect0,
            Ect1 = ect1,
            EctCe = ectce,
        };
    }
}
// 19.3.1 #section-19.3.1
public readonly struct QuicAckRange()
{
    public long Gap { get; init; } // varint
    public long RangeLength { get; init; } // varint
}

// 19.4 #name-reset_stream-frames
public readonly struct QuicResetStream() : IQuicFrame // 0x04 -> 4
{
    public QuicFrameType Type { get; } = QuicFrameType.ResetStream;
    public long StreamId { get; init; } // varint
    public long ErrorCode { get; init; } // varint // Application Protocol Error Code
    public long FinalSize { get; init; } // varint

    public static QuicResetStream Parse(ref int offset, byte[] bytes)
    {
        long streamid = IQuicFrame.VarintFrom(ref offset, bytes);
        long error = IQuicFrame.VarintFrom(ref offset, bytes);
        long size = IQuicFrame.VarintFrom(ref offset, bytes);

        return new()
        {
            StreamId = streamid,
            ErrorCode = error,
            FinalSize = size,
        };
    }
}

// 19.5 #name-stop_sending-frames
public readonly struct QuicStopSending() : IQuicFrame // 0x05 -> 5
{
    public QuicFrameType Type { get; } = QuicFrameType.StopSending;
    public long StreamId { get; init; } // varint
    public long ErrorCode { get; init; } // varint // Application Protocol Error Code

    public static QuicStopSending Parse(ref int offset, byte[] bytes)
    {
        long streamid = IQuicFrame.VarintFrom(ref offset, bytes);
        long error = IQuicFrame.VarintFrom(ref offset, bytes);

        return new()
        {
            StreamId = streamid,
            ErrorCode = error,
        };
    }
}

// 19.6 #name-crypto-frames
public readonly struct QuicCrypto() : IQuicFrame // 0x06 -> 6
{
    public QuicFrameType Type { get; } = QuicFrameType.Crypto;
    public long Offset { get; init; } // varint
    public long Length { get; init; } // varint
    public byte[] Data { get; init; } = [];

    public static QuicCrypto Parse(ref int offset, byte[] bytes)
    {
        long off = IQuicFrame.VarintFrom(ref offset, bytes);
        long length = IQuicFrame.VarintFrom(ref offset, bytes);
        byte[] data = bytes[offset..(int)(length + offset)];

        offset += (int)length;
        
        return new()
        {
            Offset = off,
            Length = length,
            Data = data,
        };
    }
}

// 19.7 #name-new_token-frames
public readonly struct QuicNewToken() : IQuicFrame // 0x07 -> 7
{
    public QuicFrameType Type { get; } = QuicFrameType.NewToken;
    public long Length { get; init; } // varint
    public byte[] Token { get; init; } = [];

    public static QuicNewToken Parse(ref int offset, byte[] bytes)
    {
        long length = IQuicFrame.VarintFrom(ref offset, bytes);
        byte[] data = bytes[offset..(int)(length + offset)];

        offset += (int)length;
        
        return new()
        {
            Length = length,
            Token = data,
        };
    }
}

// 19.8 #name-stream-frames
public readonly struct QuicStreamFrame() : IQuicFrame // 0x08 - 0x0f -> 8 - 15
{
    public QuicFrameType Type { get; } = QuicFrameType.Stream;
    public bool Fin { get; init; } //= (bits & 0b001) != 0;
    public bool Len { get; init; } //= (bits & 0b010) != 0;
    public bool Off { get; init; } //= (bits & 0b100) != 0;
    public long StreamId { get; init; } // varint
    public long Offset { get; init; } // varint
    public long Length { get; init; } // varint
    public byte[] Data { get; init; } = [];

    public static QuicStreamFrame Parse(ref int offset, byte[] bytes, byte bits)
    {
        bool fin = (bits & 0b001) != 0;
        bool len = (bits & 0b010) != 0;
        bool off = (bits & 0b100) != 0;

        long streamId = IQuicFrame.VarintFrom(ref offset, bytes);
        long offs = 0;
        long length = 0;

        if (off) offs = IQuicFrame.VarintFrom(ref offset, bytes);
        if (len) length = IQuicFrame.VarintFrom(ref offset, bytes);
        
        return new()
        {
            Fin = fin,
            Len = len,
            Off = off,
            StreamId = streamId,
            Offset = offs,
            Length = length,
        };
    }
}

// 19.9 #name-max_data-frames
public readonly struct QuicMaxData() : IQuicFrame // 0x10 -> 16
{
    public QuicFrameType Type { get; } = QuicFrameType.MaxData;
    public long Maximum { get; init; } // varint

    public static QuicMaxData Parse(ref int offset, byte[] bytes)
    {
        long max = IQuicFrame.VarintFrom(ref offset, bytes);
        
        return new()
        {
            Maximum = max,
        };
    }
}

// 19.10 #name-max_stream_data-frames
public readonly struct QuicMaxStreamData() : IQuicFrame // 0x11 -> 17
{
    public QuicFrameType Type { get; } = QuicFrameType.MaxStreamData;
    public long StreamId { get; init; } // varint
    public long Maximum { get; init; } // varint

    public static QuicMaxStreamData Parse(ref int offset, byte[] bytes)
    {
        long sid = IQuicFrame.VarintFrom(ref offset, bytes);
        long max = IQuicFrame.VarintFrom(ref offset, bytes);
        
        return new()
        {
            StreamId = sid,
            Maximum = max,
        };
    }
}

// 19.11 #name-max_streams-frames
public readonly struct QuicMaxStreams(bool bidi) : IQuicFrame // 0x12 - 0x13 -> 18 - 19
{
    public QuicFrameType Type { get; } = bidi ? QuicFrameType.MaxStreamsBidi : QuicFrameType.MaxStreamsUni;
    public long Maximum { get; init; } // varint

    public static QuicMaxStreams Parse(ref int offset, byte[] bytes, bool bidi)
    {
        long max = IQuicFrame.VarintFrom(ref offset, bytes);
        
        return new(bidi)
        {
            Maximum = max,
        };
    }
}

// 19.12 #name-data_blocked-frames
public readonly struct QuicDataBlocked() : IQuicFrame // 0x14 -> 20
{
    public QuicFrameType Type { get; } = QuicFrameType.DataBlocked;
    public long Maximum { get; init; } // varint

    public static QuicDataBlocked Parse(ref int offset, byte[] bytes)
    {
        long max = IQuicFrame.VarintFrom(ref offset, bytes);
        
        return new()
        {
            Maximum = max,
        };
    }
}

// 19.13 #name-stream_data_blocked-frames
public readonly struct QuicStreamDataBlocked() : IQuicFrame // 0x15 -> 21
{
    public QuicFrameType Type { get; } = QuicFrameType.StreamDataBlocked;
    public long StreamId { get; init; } // varint
    public long Maximum { get; init; } // varint

    public static QuicStreamDataBlocked Parse(ref int offset, byte[] bytes)
    {
        long sid = IQuicFrame.VarintFrom(ref offset, bytes);
        long max = IQuicFrame.VarintFrom(ref offset, bytes);
        
        return new()
        {
            StreamId = sid,
            Maximum = max,
        };
    }
}

// 19.14 #name-streams_blocked-frames
public readonly struct QuicStreamBlocked(bool bidi) : IQuicFrame // 0x16 - 0x17 -> 22 - 23
{
    public QuicFrameType Type { get; } = bidi ? QuicFrameType.StreamsBlockedBidi : QuicFrameType.StreamsBlockedUni;
    public long Maximum { get; init; } // varint // Maximum Streams

    public static QuicStreamBlocked Parse(ref int offset, byte[] bytes, bool bidi)
    {
        long max = IQuicFrame.VarintFrom(ref offset, bytes);
        
        return new(bidi)
        {
            Maximum = max,
        };
    }
}

// 19.15 #name-new_connection_id-frames
public readonly struct QuicNewConnectionId() : IQuicFrame // 0x18 -> 24
{
    public QuicFrameType Type { get; } = QuicFrameType.NewConnectionId;
    public long SequenceNumber { get; init; } // varint
    public long RetirePriorTo { get; init; } // varint
    public byte Length { get; init; } // 1 - 20
    public byte[] ConnectionId { get; init; } = [];
    public byte[] StatelessResetToken { get; init; } = []; // 16 bytes | 128 bits

    public static QuicNewConnectionId Parse(ref int offset, byte[] bytes)
    {
        long seq = IQuicFrame.VarintFrom(ref offset, bytes);
        long ret = IQuicFrame.VarintFrom(ref offset, bytes);

        byte length = bytes[offset++];
        byte[] cid = bytes[offset..(offset + length)];
        byte[] rst = bytes[offset..(offset + 16)];
        offset += length + 16;
        
        return new()
        {
            SequenceNumber = seq,
            RetirePriorTo = ret,
            Length = length,
            ConnectionId = cid,
            StatelessResetToken = rst,
        };
    }
}

// 19.16 #name-retire_connection_id-frames
public readonly struct QuicRetireConnectionId() : IQuicFrame // 0x19 -> 25
{
    public QuicFrameType Type { get; } = QuicFrameType.RetireConnectionId;
    public long SequenceNumber { get; init; } // varint

    public static QuicRetireConnectionId Parse(ref int offset, byte[] bytes)
    {
        long sequence = IQuicFrame.VarintFrom(ref offset, bytes);
        
        return new()
        {
            SequenceNumber = sequence,
        };
    }
}

// 19.17 #name-path_challenge-frames
public readonly struct QuicPathChallenge() : IQuicFrame // 0x1a -> 26
{
    public QuicFrameType Type { get; } = QuicFrameType.PathChallenge;
    public byte[] Data { get; init; } = []; // 64 bits

    public static QuicPathChallenge Parse(ref int offset, byte[] bytes)
    {
        byte[] data = bytes[offset..(offset + 8)];
        offset += 8;
        
        return new()
        {
            Data = data,
        };
    }
}

// 19.18 #name-path_response-frames
public readonly struct QuicPathResponse() : IQuicFrame // 0x1b -> 27
{
    public QuicFrameType Type { get; } = QuicFrameType.PathResponse;
    public byte[] Data { get; init; } = []; // 64 bits

    public static QuicPathResponse Parse(ref int offset, byte[] bytes)
    {
        byte[] data = bytes[offset..(offset + 8)];
        offset += 8;
        
        return new()
        {
            Data = data,
        };
    }
}

// 19.19 #name-connection_close-frames
public readonly struct QuicConnectionClose(bool app) : IQuicFrame // 0x1c - 0x1d -> 28 - 29
{
    public QuicFrameType Type { get; } = !app ? QuicFrameType.ConnectionCloseQuic : QuicFrameType.ConnectionCloseApp;
    public long ErrorCode { get; init; } // varint
    public long FrameType { get; init; } // varint
    public long PhraseLength { get; init; } // varint
    public byte[] ReasonPhrase { get; init; } = [];

    public static QuicConnectionClose Parse(ref int offset, byte[] bytes, bool app)
    {
        long code = IQuicFrame.VarintFrom(ref offset, bytes);
        long type = IQuicFrame.VarintFrom(ref offset, bytes);
        long length = IQuicFrame.VarintFrom(ref offset, bytes);
        byte[] reason = bytes[offset..(int)(offset + length)];
        offset += (int)length;
        
        return new(app)
        {
            ErrorCode = code,
            FrameType = type,
            PhraseLength = length,
            ReasonPhrase = reason,
        };
    }
}

// 19.20 #name-handshake_done-frames
public readonly struct QuicHandshakeDone() : IQuicFrame // 0x1e -> 30
{
    public QuicFrameType Type { get; } = QuicFrameType.HandshakeDone;

    public static QuicHandshakeDone Parse(ref int offset, byte[] bytes)
    {
        return new();
    }
}