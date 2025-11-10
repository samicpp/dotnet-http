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


public interface IQuicFrame
{
    QuicFrameType Type { get; }

    public static List<IQuicFrame> ParseAll(byte[] bytes)
    {
        return [];
    }
    public static IQuicFrame? Parse(QuicFrameType type, byte[] bytes) // this will be integrated into ParseAll
    {
        return type switch
        {
            QuicFrameType.Padding => new QuicPadding { },
            QuicFrameType.Ping => new QuicPing { },
            QuicFrameType.Ack => new QuicAck(false) { },
            QuicFrameType.AckEcn => new QuicAck(true) { },
            _ => null,
        };
    }
}


// 19.1 #name-padding-frames
public readonly struct QuicPadding() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.Padding;
}

// 19.2 #name-ping-frames
public readonly struct QuicPing() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.Ping;
}

// 19.3 #name-ack-frames
public readonly struct QuicAck(bool ecn) : IQuicFrame
{
    public QuicFrameType Type { get; init; } = ecn ? QuicFrameType.AckEcn : QuicFrameType.Ack;
    public long Largest { get; init; } // varint
    public long Delay { get; init; } // varint
    public long RangeCount { get; init; } // varint
    public long FirstRange { get; init; } // varint
    public QuicAckRange[] Ranges { get; init; } = [];

    // 19.3.2 #name-ecn-counts
    public long? Ect0 { get; init; } = null;
    public long? Ect1 { get; init; } = null;
    public long? EctCe { get; init; } = null;
}
// 19.3.1 #section-19.3.1
public readonly struct QuicAckRange
{
    public readonly long Gap; // varint
    public readonly long RangeLength; // varint
}

// 19.4 #name-reset_stream-frames
public readonly struct QuicResetStream() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.ResetStream;
    public long StreamId { get; init; } // varint
    public long ErrorCode { get; init; } // varint // Application Protocol Error Code
    public long FinalSize { get; init; } // varint
}

// 19.5 #name-stop_sending-frames
public readonly struct QuicStopSending() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.StopSending;
    public long StreamId { get; init; } // varint
    public long ErrorCode { get; init; } // varint // Application Protocol Error Code
}

// 19.6 #name-crypto-frames
public readonly struct QuicCrypto() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.Crypto;
    public long Offset { get; init; } // varint
    public long Length { get; init; } // varint
    public byte[] Data { get; init; } = [];
}

// 19.7 #name-new_token-frames
public readonly struct QuicNewToken() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.NewToken;
    public long Length { get; init; } // varint
    public byte[] Token { get; init; } = [];
}

// 19.8 #name-stream-frames
public readonly struct QuicStreamFrame(byte bits) : IQuicFrame
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
public readonly struct QuicMaxData() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.MaxData;
    public long Maximum { get; init; } // varint
}

// 19.10 #name-max_stream_data-frames
public readonly struct QuicMaxStreamData() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.MaxStreamData;
    public long StreamId { get; init; } // varint
    public long Maximum { get; init; } // varint
}

// 19.11 #name-max_streams-frames
public readonly struct QuicMaxStreams(bool bidi) : IQuicFrame
{
    public QuicFrameType Type { get; } = bidi ? QuicFrameType.MaxStreamsBidi : QuicFrameType.MaxStreamsUni;
    public long Maximum { get; init; } // varint
}

// 19.12 #name-data_blocked-frames
public readonly struct QuicDataBlocked() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.DataBlocked;
    public long Maximum { get; init; } // varint
}

// 19.13 #name-stream_data_blocked-frames
public readonly struct QuicStreamDataBlocked() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.StreamDataBlocked;
    public long StreamId { get; init; } // varint
    public long Maximum { get; init; } // varint
}

// 19.14 #name-streams_blocked-frames
public readonly struct QuicStreamBlocked(bool bidi) : IQuicFrame
{
    public QuicFrameType Type { get; } = bidi ? QuicFrameType.MaxStreamsBidi : QuicFrameType.StreamsBlockedUni;
    public long Maximum { get; init; } // varint // Maximum Streams
}

// 19.15 #name-new_connection_id-frames
public readonly struct QuicNewConnectionId() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.NewConnectionId;
    public long SequenceNumber { get; init; } // varint
    public long RetirePriorTo { get; init; } // varint
    public byte Length { get; init; } // 1 - 20
    public byte[] ConnectionId { get; init; } = [];
    public byte[] StatelessResetToken { get; init; } = []; // 16 bytes | 128 bits
}

// 19.16 #name-retire_connection_id-frames
public readonly struct QuicRetireConnectionId() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.RetireConnectionId;
    public long SequenceNumber { get; init; } // varint
}

// 19.17 #name-path_challenge-frames
public readonly struct QuicPathChallenge() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.PathChallenge;
    public byte[] Data { get; init; } = []; // 64 bits
}

// 19.18 #name-path_response-frames
public readonly struct QuicPathResponse() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.PathResponse;
    public byte[] Data { get; init; } = []; // 64 bits
}

// 19.19 #name-connection_close-frames
public readonly struct QuicConnectionClose(bool app) : IQuicFrame
{
    public QuicFrameType Type { get; } = !app ? QuicFrameType.ConnectionCloseQuic : QuicFrameType.ConnectionCloseApp;
    public long ErrorCode { get; init; } // varint
    public long FrameType { get; init; } // varint
    public long PhraseLength { get; init; } // varint
    public byte[] ReasonPhrase { get; init; } = []; // 64 bits
}

// 19.20 #name-handshake_done-frames
public readonly struct QuicHandshakeDone() : IQuicFrame
{
    public QuicFrameType Type { get; } = QuicFrameType.HandshakeDone;
}