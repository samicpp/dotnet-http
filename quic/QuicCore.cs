// http://datatracker.ietf.org/doc/html/rfc9000


namespace Samicpp.Http.Quic
{

    // 17.2 #name-long-header-packets
    public readonly struct QuicLongPacket
    {
        public const byte HeaderForm = 1;
        public const byte FixedBit = 1;
        public readonly QuicPacketType Type;
        public readonly byte TypeSpecific; // Type-Specific Bits
        public readonly uint Version;
        public readonly byte DciLength; // Destination Connection ID Length
        public readonly byte[] Dci; // Destination Connection ID
        public readonly byte SciLength; // Destination Connection ID
        public readonly byte[] Sci; // Destination Connection ID
        public readonly byte[] TsPayload; // Type-Specific Payload 
    }

    // 17.3.1 #name-1-rtt-packet
    public readonly struct QuicShortPacket
    {
        public const byte HeaderForm = 0;
        public const byte FixedBit = 1;
        public readonly byte SpinBit;
        public readonly byte Reserved;
        public readonly byte KeyPhase;
        public readonly byte PacketNumberLength;
        public readonly byte[] Dci; // Destination Connection ID
        public readonly uint PacketNumber;
        public readonly byte[] PacketPayload;
    }

    // 17.2 Table 5 #long-packet-types
    public enum QuicPacketType : byte
    {
        Initial = 0b00,    // 17.2.2
        ZeroRtt = 0b01,    // 17.2.3
        Handshake = 0b10,  // 17.2.4
        Retry = 0b11,      // 17.2.5
        Invalid,
    }

    // 12.4 #section-12.4
    public enum QuicFrameType : byte
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
        ConnectionCloseQuic = 0x1c,     // 19.19 | IH01 | N
        ConnectionCloseApp = 0x1d,      // 19.19 | IH01 | N
        HandshakeDone = 0x1e,           // 19.20 | ___1
        Invalid,
    }


    // entire parsing semantics subject to change
    
    public interface IQuicFrame // ????
    {
        QuicFrameType Type { get; }
    }

    public static class QuicFrame
    {
        public static List<IQuicFrame> ParseAll(byte[] bytes)
        {
            return [];
        }
        public static IQuicFrame? Parse(QuicFrameType type, byte[] bytes)
        {
            return type switch
            {
                QuicFrameType.Padding => Frames.Padding.Parse(),
                _ => null,
            };
        }
    }
}

namespace Samicpp.Http.Quic.Frames
{
    public readonly struct Padding() : IQuicFrame
    {
        public static Padding? Parse()
        {
            return new();
        }

        public QuicFrameType Type { get; } = QuicFrameType.Padding;
    }
}