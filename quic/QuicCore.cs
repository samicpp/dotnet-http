namespace Samicpp.Http.Quic;

// http://datatracker.ietf.org/doc/html/rfc9000


// 17.2 Table 5 #long-packet-types
public enum QuicPacketType : byte
{
    Initial = 0b00,    // 17.2.2
    ZeroRtt = 0b01,    // 17.2.3
    Handshake = 0b10,  // 17.2.4
    Retry = 0b11,      // 17.2.5
    Invalid,
}

// 17.2 #name-long-header-packets
public readonly struct QuicLongPacket()
{
    public byte HeaderForm { get; } = 1;
    public byte FixedBit { get; } = 1;
    public QuicPacketType Type { get; init; }
    public byte TypeSpecific { get; init; } // Type-Specific Bits
    public uint Version { get; init; }
    public byte DciLength { get; init; } // Destination Connection ID Length
    public byte[] Dci { get; init; } = []; // Destination Connection ID
    public byte SciLength { get; init; } // Destination Connection ID
    public byte[] Sci { get; init; } = []; // Destination Connection ID
    public byte[] TsPayload { get; init; } = []; // Type-Specific Payload 
}

// 17.3.1 #name-1-rtt-packet
public readonly struct QuicShortPacket()
{
    public byte HeaderForm { get; } = 0;
    public byte FixedBit { get; } = 1;
    public byte SpinBit { get; init; }
    public byte Reserved { get; init; }
    public byte KeyPhase { get; init; }
    public byte PacketNumberLength { get; init; }
    public byte[] Dci { get; init; } = []; // Destination Connection ID
    public uint PacketNumber { get; init; }
    public byte[] PacketPayload { get; init; } = [];
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

    public static (int, long) ParseVarint(int offset, byte[] bytes)
    {
        int first = bytes[offset++];
        int len = (first & 0b1100_0000) >> 6;
        long varint = len switch
        {
            0 => first & 0b0011_1111,
            1 => (first & 0b0011_1111) << 8 | bytes[offset++],
            2 => (first & 0b0011_1111) << 24 | bytes[offset++] << 16 | bytes[offset++] << 8 | bytes[offset++],
            4 => (first & 0b0011_1111) << 56 | bytes[offset++] << 48 | bytes[offset++] << 40 | bytes[offset++] << 32 | bytes[offset++] << 24 | bytes[offset++] << 16 | bytes[offset++] << 8 | bytes[offset++],
            _ => 0, // impossible outcome
        };
        return (offset, varint);
    }
    public static List<IQuicFrame> ParseAll(byte[] bytes)
    {
        int offset = 0;
        List<IQuicFrame> frames = [];
        
        while (offset < bytes.Length)
        {
            (offset, IQuicFrame frame) = Parse(offset, bytes) ?? throw new InvalidDataException("Frame(s) not valid");
            frames.Add(frame);
        }

        return frames;
    }
    public static (int, IQuicFrame)? Parse(int offset, byte[] bytes) // this will be integrated into ParseAll
    {
        (offset, long varint) = ParseVarint(offset, bytes);
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
            0 => QuicPadding.Parse(offset, bytes),
            1 => QuicPing.Parse(offset, bytes),
            // 2 => new QuicAck(false) { },
            // 3 => new QuicAck(true) { },
            // >= 8 and <= 15 => QuicFrameType.Stream,

            _ => null,
        };
    }
}


// 19.1 #name-padding-frames
public readonly struct QuicPadding() : IQuicFrame // 0x00 -> 0
{
    public QuicFrameType Type { get; } = QuicFrameType.Padding;

    public static (int, QuicPadding) Parse(int offset, byte[] bytes)
    {
        return (offset, new());
    }
}

// 19.2 #name-ping-frames
public readonly struct QuicPing() : IQuicFrame // 0x01 -> 1
{
    public QuicFrameType Type { get; } = QuicFrameType.Ping;

    public static (int, QuicPing) Parse(int offset, byte[] bytes)
    {
        return (offset, new());
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

    public static (int, QuicAck) Parse(int offset, byte[] bytes, bool ecn)
    {
        (offset, long largest) = IQuicFrame.ParseVarint(offset, bytes);
        (offset, long delay) = IQuicFrame.ParseVarint(offset, bytes);
        (offset, long rangeCount) = IQuicFrame.ParseVarint(offset, bytes);
        (offset, long firstRange) = IQuicFrame.ParseVarint(offset, bytes);
        List<QuicAckRange> ranges = [];

        for (long i = 0; i < rangeCount; i++)
        {
            (offset, long gap) = IQuicFrame.ParseVarint(offset, bytes);
            (offset, long rangeLength) = IQuicFrame.ParseVarint(offset, bytes);
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
            (offset, ect0) = IQuicFrame.ParseVarint(offset, bytes);
            (offset, ect1) = IQuicFrame.ParseVarint(offset, bytes);
            (offset, ectce) = IQuicFrame.ParseVarint(offset, bytes);
        }
        
        return (offset, new(ecn)
        {
            Largest = largest,
            Delay = delay,
            RangeCount = rangeCount,
            FirstRange = firstRange,

            Ect0 = ect0,
            Ect1 = ect1,
            EctCe = ectce,
        });
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
}

// 19.5 #name-stop_sending-frames
public readonly struct QuicStopSending() : IQuicFrame // 0x05 -> 5
{
    public QuicFrameType Type { get; } = QuicFrameType.StopSending;
    public long StreamId { get; init; } // varint
    public long ErrorCode { get; init; } // varint // Application Protocol Error Code
}

// 19.6 #name-crypto-frames
public readonly struct QuicCrypto() : IQuicFrame // 0x06 -> 6
{
    public QuicFrameType Type { get; } = QuicFrameType.Crypto;
    public long Offset { get; init; } // varint
    public long Length { get; init; } // varint
    public byte[] Data { get; init; } = [];
}

// 19.7 #name-new_token-frames
public readonly struct QuicNewToken() : IQuicFrame // 0x07 -> 7
{
    public QuicFrameType Type { get; } = QuicFrameType.NewToken;
    public long Length { get; init; } // varint
    public byte[] Token { get; init; } = [];
}

// 19.8 #name-stream-frames
public readonly struct QuicStreamFrame(byte bits) : IQuicFrame // 0x08 - 0x0f -> 8 - 15
{
    public QuicFrameType Type { get; } = QuicFrameType.Stream;
    public bool Fin { get; } = (bits & 0b001) != 0;
    public bool Len { get; } = (bits & 0b010) != 0;
    public bool Off { get; } = (bits & 0b100) != 0;
    public long StreamId { get; init; } // varint
    public long Offset { get; init; } // varint
    public long Length { get; init; } // varint
    public byte[] Data { get; init; } = [];
}

// 19.9 #name-max_data-frames
public readonly struct QuicMaxData() : IQuicFrame // 0x10 -> 16
{
    public QuicFrameType Type { get; } = QuicFrameType.MaxData;
    public long Maximum { get; init; } // varint
}

// 19.10 #name-max_stream_data-frames
public readonly struct QuicMaxStreamData() : IQuicFrame // 0x11 -> 17
{
    public QuicFrameType Type { get; } = QuicFrameType.MaxStreamData;
    public long StreamId { get; init; } // varint
    public long Maximum { get; init; } // varint
}

// 19.11 #name-max_streams-frames
public readonly struct QuicMaxStreams(bool bidi) : IQuicFrame // 0x12 - 0x13 -> 18 - 19
{
    public QuicFrameType Type { get; } = bidi ? QuicFrameType.MaxStreamsBidi : QuicFrameType.MaxStreamsUni;
    public long Maximum { get; init; } // varint
}

// 19.12 #name-data_blocked-frames
public readonly struct QuicDataBlocked() : IQuicFrame // 0x14 -> 20
{
    public QuicFrameType Type { get; } = QuicFrameType.DataBlocked;
    public long Maximum { get; init; } // varint
}

// 19.13 #name-stream_data_blocked-frames
public readonly struct QuicStreamDataBlocked() : IQuicFrame // 0x15 -> 21
{
    public QuicFrameType Type { get; } = QuicFrameType.StreamDataBlocked;
    public long StreamId { get; init; } // varint
    public long Maximum { get; init; } // varint
}

// 19.14 #name-streams_blocked-frames
public readonly struct QuicStreamBlocked(bool bidi) : IQuicFrame // 0x16 - 0x17 -> 22 - 23
{
    public QuicFrameType Type { get; } = bidi ? QuicFrameType.StreamsBlockedBidi : QuicFrameType.StreamsBlockedUni;
    public long Maximum { get; init; } // varint // Maximum Streams
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
}

// 19.16 #name-retire_connection_id-frames
public readonly struct QuicRetireConnectionId() : IQuicFrame // 0x19 -> 25
{
    public QuicFrameType Type { get; } = QuicFrameType.RetireConnectionId;
    public long SequenceNumber { get; init; } // varint
}

// 19.17 #name-path_challenge-frames
public readonly struct QuicPathChallenge() : IQuicFrame // 0x1a -> 26
{
    public QuicFrameType Type { get; } = QuicFrameType.PathChallenge;
    public byte[] Data { get; init; } = []; // 64 bits
}

// 19.18 #name-path_response-frames
public readonly struct QuicPathResponse() : IQuicFrame // 0x1b -> 27
{
    public QuicFrameType Type { get; } = QuicFrameType.PathResponse;
    public byte[] Data { get; init; } = []; // 64 bits
}

// 19.19 #name-connection_close-frames
public readonly struct QuicConnectionClose(bool app) : IQuicFrame // 0x1c - 0x1d -> 28 - 29
{
    public QuicFrameType Type { get; } = !app ? QuicFrameType.ConnectionCloseQuic : QuicFrameType.ConnectionCloseApp;
    public long ErrorCode { get; init; } // varint
    public long FrameType { get; init; } // varint
    public long PhraseLength { get; init; } // varint
    public byte[] ReasonPhrase { get; init; } = []; // 64 bits
}

// 19.20 #name-handshake_done-frames
public readonly struct QuicHandshakeDone() : IQuicFrame // 0x1e -> 30
{
    public QuicFrameType Type { get; } = QuicFrameType.HandshakeDone;

    public static (int, QuicHandshakeDone) Parse(int offset, byte[] bytes)
    {
        return (offset, new());
    }
}