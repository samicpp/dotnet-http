namespace Samicpp.Http.Quic;

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

// handles read loop etc., to ensure proper connection management
// will also support rfc 9221 https://datatracker.ietf.org/doc/html/rfc9221

// https://datatracker.ietf.org/doc/html/rfc9001
// https://datatracker.ietf.org/doc/html/rfc9369


public static class Version
{
    public static readonly byte V1 = 1;
    public static readonly byte V2 = 2;
    public static readonly byte Unsupported = 127;


    public static byte From(uint version)
    {
        if (version == 0x000001) return V1;         // wireshark
        else if (version == 0x6b3343cf) return V2;  // rfc9369#name-version-field
        else return Unsupported;
    }
    public static byte[] GetSalt(byte ver)
    {
        return ver switch
        {
            1 => [ 0x38, 0x76, 0x2c, 0xf7, 0xf5, 0x59, 0x34, 0xb3, 0x4d, 0x17, 0x9a, 0xe6, 0xa4, 0xc8, 0x0c, 0xad, 0xcc, 0xbb, 0x7f, 0x0a, ], // rfc9001#name-initial-secrets
            2 => [ 0x0d, 0xed, 0xe3, 0xde, 0xf7, 0x00, 0xa6, 0xdb, 0x81, 0x93, 0x81, 0xbe, 0x6e, 0x26, 0x9d, 0xcb, 0xf9, 0xbd, 0x2e, 0xd9, ], // rfc9369#name-initial-salt
            _ => throw new ArgumentException("unknown version"),
        };
    }
}

public readonly struct InitialKeys()
{
    public readonly static byte[] ClientLabel = "client in"u8.ToArray(); // 32
    public readonly static byte[] ServerLabel = "server in"u8.ToArray(); // 32

    public readonly static byte[] KeyLabel = "quic key"u8.ToArray();     // 16
    public readonly static byte[] IvLabel = "quic iv"u8.ToArray();       // 12
    public readonly static byte[] HpLabel = "quic hp"u8.ToArray();       // 16
    public readonly static byte[] KuLabel = "quic ku"u8.ToArray();       // placeholder
    
    public readonly static byte[] KeyLabel2 = "quicv2 key"u8.ToArray();     // 16
    public readonly static byte[] IvLabel2 = "quicv2 iv"u8.ToArray();       // 12
    public readonly static byte[] HpLabel2 = "quicv2 hp"u8.ToArray();       // 16
    public readonly static byte[] KuLabel2 = "quicv2 ku"u8.ToArray();       // placeholder
    


    public byte[] InitialSecret { get; init; } = [];
    public byte[] Server { get; init; } = [];
    public byte[] Client { get; init; } = [];

    public byte[] ServerKey { get; init; } = [];
    public byte[] ServerIv { get; init; } = [];
    public byte[] ServerHp { get; init; } = [];

    public byte[] ClientKey { get; init; } = [];
    public byte[] ClientIv { get; init; } = [];
    public byte[] ClientHp { get; init; } = [];



    public static InitialKeys Derive(byte ver, byte[] dcid)
    {
        if (ver == Version.V1)
        {
            byte[] ext = HKDF.Extract(HashAlgorithmName.SHA256, dcid, Version.GetSalt(1));
            byte[] server = HKDF.Expand(HashAlgorithmName.SHA256, ext, 32, ServerLabel);
            byte[] client = HKDF.Expand(HashAlgorithmName.SHA256, ext, 32, ClientLabel);

            byte[] skey = HKDF.Expand(HashAlgorithmName.SHA256, server, 16, KeyLabel);
            byte[] siv = HKDF.Expand(HashAlgorithmName.SHA256, server, 12, IvLabel);
            byte[] shp = HKDF.Expand(HashAlgorithmName.SHA256, server, 16, HpLabel);

            byte[] ckey = HKDF.Expand(HashAlgorithmName.SHA256, client, 16, KeyLabel);
            byte[] civ = HKDF.Expand(HashAlgorithmName.SHA256, client, 12, IvLabel);
            byte[] chp = HKDF.Expand(HashAlgorithmName.SHA256, client, 16, HpLabel);

            return new()
            {
                InitialSecret = ext,
                Server = server,
                Client = client,

                ServerKey = skey,
                ServerIv = siv,
                ServerHp = shp,

                ClientKey = ckey,
                ClientIv = civ,
                ClientHp = chp,
            };
        }
        else if (ver == Version.V2)
        {
            byte[] ext = HKDF.Extract(HashAlgorithmName.SHA256, dcid, Version.GetSalt(2));
            byte[] server = HKDF.Expand(HashAlgorithmName.SHA256, ext, 32, ServerLabel);
            byte[] client = HKDF.Expand(HashAlgorithmName.SHA256, ext, 32, ClientLabel);

            byte[] skey = HKDF.Expand(HashAlgorithmName.SHA256, server, 16, KeyLabel2);
            byte[] siv = HKDF.Expand(HashAlgorithmName.SHA256, server, 12, IvLabel2);
            byte[] shp = HKDF.Expand(HashAlgorithmName.SHA256, server, 16, HpLabel2);

            byte[] ckey = HKDF.Expand(HashAlgorithmName.SHA256, client, 16, KeyLabel2);
            byte[] civ = HKDF.Expand(HashAlgorithmName.SHA256, client, 12, IvLabel2);
            byte[] chp = HKDF.Expand(HashAlgorithmName.SHA256, client, 16, HpLabel2);

            return new()
            {
                InitialSecret = ext,
                Server = server,
                Client = client,

                ServerKey = skey,
                ServerIv = siv,
                ServerHp = shp,

                ClientKey = ckey,
                ClientIv = civ,
                ClientHp = chp,
            };
        }
        else throw new Exception("irregular version");
    }
    public static byte[] DeriveHp(byte[] sample, InitialKeys keys)
    {
        byte[] block = new byte[16];

        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = keys.ClientHp;

        using var enc = aes.CreateEncryptor();
        enc.TransformBlock(
            sample, 0, 16,
            block, 0
        );

        return block;
    }
}

public class QuicKernel(Socket socket, X509Certificate2 cert): IDisposable
{
    readonly Socket udp = socket;
    // readonly byte[] window = new byte[64*1024];
    public EndPoint? Address { get => udp.LocalEndPoint; }
    readonly X509Certificate2 cert = cert;
    public int ScidLength
    {
        get;
        init
        {
            if (1 < value && value < 20)
            {
                field = value;
            }
            else
            {
                throw new Exception($"value {value} out of bounds");
            }
        }
    } = 12;
    readonly Random random = new((int)DateTimeOffset.Now.ToUnixTimeMilliseconds());
    private byte[] RandomScid(byte[]? dcid = null)
    {
        dcid ??= [0];
        int dl = dcid.Length;

        byte[] rand = new byte[ScidLength];
        for (int i = 0; i < ScidLength; i++)
        {
            rand[i] = (byte)(random.Next(0, 256) ^ dcid[i%dl]);
        }
        return rand;
    }

    public QuicKernel(QuicConfig config): this(new Socket(config.AddressFamily, config.SocketType, config.ProtocolType), config.Certificate)
    {
        if (config.DualMode) udp.DualMode = true;
        udp.Bind(config.Address);
    }


    protected (EndPoint,byte[]) ReceiveDgram()
    {
        Span<byte> window = stackalloc byte[64*1024];
        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        int l = udp.ReceiveFrom(window, ref from);
        return (from, [.. window[..l]]);
    }
    protected async Task<(EndPoint,byte[])> ReceiveDgramAsync()
    {
        byte[] window = new byte[64*1024];
        SocketReceiveFromResult dgram = await udp.ReceiveFromAsync(window, new IPEndPoint(IPAddress.Any, 0));
        return (dgram.RemoteEndPoint, [.. window[..dgram.ReceivedBytes]]);
    }

    // public (EndPoint,List<IQuicPacket>) Receive()
    // {
    //     var (from, bytes) = ReceiveDgram();
    //     return (from, IQuicPacket.ParseAll(ScidLength, bytes));
    // }
    // public async Task<(EndPoint,List<IQuicPacket>)> ReceiveAsync()
    // {
    //     var (from, bytes) = await ReceiveDgramAsync();
    //     return (from, IQuicPacket.ParseAll(ScidLength, bytes));
    // }


    public void Incoming()
    {
        var (endPoint, bytes) = ReceiveDgram();
        int pos = 0;
        bool done = false;
        
        while (!done && pos < bytes.Length)
        {
            int bpos = pos;

            if ((bytes[pos] & 128) != 0) // headerform bit
            {
                bool fixedBit = (bytes[pos] & 0b0100_0000) != 0;
                // byte typeSpecific = (byte)(bytes[pos++] & 0b1111);
                uint version = (uint)bytes[pos++] << 24 | (uint)bytes[pos++] << 16 | (uint)bytes[pos++] << 8 | bytes[pos++];
                byte dcil = bytes[pos++];
                byte[] dcid = bytes[pos..(pos + dcil)]; pos += dcil;
                byte scil = bytes[pos++];
                byte[] scid = bytes[pos..(pos + scil)]; pos += scil;

                byte ver = Version.From(version);

                QuicPacketType type = QuicPacketType.Invalid; // impossible
                int v = (bytes[pos] & 0b0011_0000) >> 4;
                if ((ver == Version.V1 && v == 0b00) || (ver == Version.V2 && v == 0b01)) type = QuicPacketType.Initial;
                else if ((ver == Version.V1 && v == 0b01) || (ver == Version.V2 && v == 0b10)) type = QuicPacketType.ZeroRtt;
                else if ((ver == Version.V1 && v == 0b10) || (ver == Version.V2 && v == 0b11)) type = QuicPacketType.Handshake;
                else if ((ver == Version.V1 && v == 0b11) || (ver == Version.V2 && v == 0b00)) type = QuicPacketType.Retry;

                if (!fixedBit)
                {
                    QuicVersionPacket vers = QuicVersionPacket.Parse(ref bpos, bytes);
                }
                else if (type == QuicPacketType.Initial)
                {
                    long tlen = IQuicFrame.VarintFrom(ref pos, bytes);
                    // Span<byte> token = bytes.AsSpan(pos..(pos + (int)tlen));
                    pos += (int)tlen;
                    long length = IQuicFrame.VarintFrom(ref pos, bytes);
                    Span<byte> payload = bytes.AsSpan(pos..(pos + (int)length));
                    pos += (int)length;

                    InitialKeys keys = InitialKeys.Derive(ver: ver, dcid: dcid);
                    byte[] block = InitialKeys.DeriveHp(payload[4..19].ToArray(), keys);

                    Span<byte> mask = block.AsSpan(0..5);

                    bytes[bpos] ^= (byte)(mask[0] & 0x0f);
                    byte pnLength = (byte)((bytes[bpos] & 0b0011) + 1);


                    for (int i = 0; i < pnLength; i++) payload[i] ^= mask[(i % 4) + 1];

                    uint pn = 0;
                    for (int i = 0; i < pnLength; i++) pn |= (uint)payload[pos++] << (8 * (pnLength - 1 - i));

                    byte[] nonce = [..keys.ClientIv];
                    for (int i = 0; pnLength != 0; i++)
                    {
                        nonce[11 - i] ^= (byte)(pn & 0xff);
                        pn >>= 8;
                    }
                    
                    

                    // decrypt / unmask

                    QuicInitialPacket initial = QuicInitialPacket.Parse(ref bpos, bytes);
                }
                else if (type == QuicPacketType.ZeroRtt)
                {
                    // decrypt
                    QuicZeroRttPacket zero = QuicZeroRttPacket.Parse(ref bpos, bytes);
                }
                else if (type == QuicPacketType.Handshake)
                {
                    // decrypt
                    QuicHandshakePacket handshake = QuicHandshakePacket.Parse(ref bpos, bytes);
                }
                else if (type == QuicPacketType.Retry)
                {
                    QuicRetryPacket retry = QuicRetryPacket.Parse(ref bpos, bytes);
                    done = true;
                    break;
                }

                pos = bpos;
            }
            else
            {
                // shortpacket
                // decrypt
                QuicShortPacket shor = QuicShortPacket.Parse(ref pos, ScidLength, bytes);
                done = true;
                break;
            }
        }
    }
    public async Task IncomingAsync()
    {
        int scidLength = ScidLength;
        await Task.CompletedTask;
        return;
    }
    

    public void Dispose() => udp.Dispose();
}

public class QuicConfig()
{
    public required EndPoint Address { get; init; }
    public required X509Certificate2 Certificate { get; init; }
    public AddressFamily AddressFamily { get; init; } = AddressFamily.InterNetworkV6;
    public SocketType SocketType { get; init; } = SocketType.Dgram;
    public ProtocolType ProtocolType { get; init; } = ProtocolType.Udp;
    public bool DualMode { get; init; } = true;

    public static QuicConfig FromPKCS12((string path, string password) pkcs12, EndPoint endPoint)
    {
        return new(){
            Address = endPoint,
            Certificate = X509CertificateLoader.LoadPkcs12FromFile(pkcs12.path, pkcs12.password)
        };
    }
    public static QuicConfig FromPKCS12((string,string) pkcs12, ushort port) => FromPKCS12(pkcs12, new IPEndPoint(IPAddress.IPv6Any, port));
    public static QuicConfig FromPKCS12((string,string) pkcs12, string address) => FromPKCS12(pkcs12, IPEndPoint.Parse(address));

    public static QuicConfig SelfSigned(EndPoint endPoint)
    {
        // using RSA rsa = RSA.Create(2048);
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        X500DistinguishedName subject = new("CN=localhost");
        // CertificateRequest req = new(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        CertificateRequest req = new(subject, ecdsa, HashAlgorithmName.SHA256);
        
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));

        SubjectAlternativeNameBuilder sanBuilder = new();
        sanBuilder.AddDnsName("*.localhost");
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddDnsName("127.0.0.1");
        sanBuilder.AddDnsName("::1");
        req.CertificateExtensions.Add(sanBuilder.Build());

        X509Certificate2 cert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1)
        );

        return new()
        {
            Address = endPoint,
            Certificate = X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx, ""), ""),
        };
    }
    public static QuicConfig SelfSigned(ushort port) => SelfSigned(new IPEndPoint(IPAddress.Any, port));
    public static QuicConfig SelfSigned(string address) => SelfSigned(IPEndPoint.Parse(address));
}