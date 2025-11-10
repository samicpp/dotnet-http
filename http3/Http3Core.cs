namespace Samicpp.Http.Http3;

// https://datatracker.ietf.org/doc/html/rfc9114


// 11.2.1 #name-frame-types
public enum Http3FrameType : byte
{
    Data = 0x0,         // 7.2.1
    Headers = 0x1,      // 7.2.2
    // Reserved 0x2     
    CancelPush = 0x3,   // 7.2.3
    Settings = 0x4,     // 7.2.4
    PushPromise = 0x5,  // 7.2.5
    // Reserved 0x6
    Goaway = 0x7,       // 7.2.6
    // Reserved 0x8
    // Reserved 0x9
    MaxPushId = 0xd,    // 7.2.7

    Unknown,
}
public struct Http3Frame { }
