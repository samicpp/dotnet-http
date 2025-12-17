namespace Samicpp.Http.Quic;

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

// handles read loop etc., to ensure proper connection management
// will also support rfc 9221 https://datatracker.ietf.org/doc/html/rfc9221


public static class Version
{
    public static readonly byte V1 = 1;
    public static readonly byte V2 = 2;

    public static byte[] GetSalt(byte ver)
    {
        return ver switch
        {
            1 => [ 0x38, 0x76, 0x2c, 0xf7, 0xf5, 0x59, 0x34, 0xb3, 0x4d, 0x17, 0x9a, 0xe6, 0xa4, 0xc8, 0x0c, 0xad, 0xcc, 0xbb, 0x7f, 0x0a, ], // https://datatracker.ietf.org/doc/html/rfc9001#name-initial-secrets
            2 => [ 0x0d, 0xed, 0xe3, 0xde, 0xf7, 0x00, 0xa6, 0xdb, 0x81, 0x93, 0x81, 0xbe, 0x6e, 0x26, 0x9d, 0xcb, 0xf9, 0xbd, 0x2e, 0xd9, ], // https://datatracker.ietf.org/doc/html/rfc9369#name-initial-salt
            _ => throw new ArgumentException("unknown version"),
        };
    }
    static InitialKeys DeriveInitial(byte ver, byte[] dcid)
    {
        byte[] ext = HKDF.Extract(HashAlgorithmName.SHA256, dcid, GetSalt(ver));
        byte[] server = HKDF.Expand(HashAlgorithmName.SHA256, ext, 32, InitialKeys.ServerLabel);
        byte[] client = HKDF.Expand(HashAlgorithmName.SHA256, ext, 32, InitialKeys.ClientLabel);

        byte[] skey = HKDF.Expand(HashAlgorithmName.SHA256, server, 16, InitialKeys.KeyLabel);
        byte[] siv = HKDF.Expand(HashAlgorithmName.SHA256, server, 12, InitialKeys.IvLabel);
        byte[] shp = HKDF.Expand(HashAlgorithmName.SHA256, server, 16, InitialKeys.HpLabel);

        byte[] ckey = HKDF.Expand(HashAlgorithmName.SHA256, client, 16, InitialKeys.KeyLabel);
        byte[] civ = HKDF.Expand(HashAlgorithmName.SHA256, client, 12, InitialKeys.IvLabel);
        byte[] chp = HKDF.Expand(HashAlgorithmName.SHA256, client, 16, InitialKeys.HpLabel);

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
}

public readonly struct InitialKeys()
{
    public readonly static byte[] ClientLabel = "client in"u8.ToArray(); // 32
    public readonly static byte[] ServerLabel = "server in"u8.ToArray(); // 32
    public readonly static byte[] KeyLabel = "quic key"u8.ToArray();     // 16
    public readonly static byte[] IvLabel = "quic iv"u8.ToArray();       // 12
    public readonly static byte[] HpLabel = "quic hp"u8.ToArray();       // 16


    public byte[] InitialSecret { get; init; } = [];
    public byte[] Server { get; init; } = [];
    public byte[] Client { get; init; } = [];

    public byte[] ServerKey { get; init; } = [];
    public byte[] ServerIv { get; init; } = [];
    public byte[] ServerHp { get; init; } = [];

    public byte[] ClientKey { get; init; } = [];
    public byte[] ClientIv { get; init; } = [];
    public byte[] ClientHp { get; init; } = [];
}

public class QuicKernel(Socket socket, X509Certificate2 cert): IDisposable
{
    readonly Socket udp = socket;
    readonly byte[] window = new byte[64*1024];
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
    readonly Random random = new();
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
        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        int l = udp.ReceiveFrom(window, ref from);
        return (from, [.. window[..l]]);
    }
    protected async Task<(EndPoint,byte[])> ReceiveDgramAsync()
    {
        SocketReceiveFromResult dgram = await udp.ReceiveFromAsync(window, new IPEndPoint(IPAddress.Any, 0));
        return (dgram.RemoteEndPoint, [.. window[..dgram.ReceivedBytes]]);
    }

    public (EndPoint,List<IQuicPacket>) Receive()
    {
        var (from, bytes) = ReceiveDgram();
        return (from, IQuicPacket.ParseAll(ScidLength, bytes));
    }
    public async Task<(EndPoint,List<IQuicPacket>)> ReceiveAsync()
    {
        var (from, bytes) = await ReceiveDgramAsync();
        return (from, IQuicPacket.ParseAll(ScidLength, bytes));
    }


    public void Incoming()
    {
        var (endPoint, bytes) = ReceiveDgram();
        int pos = 0;
        
        while (pos < bytes.Length)
        {
            int bpos = pos;
            var (done, packet) = IQuicPacket.Parse(ref pos, ScidLength, bytes);

            if (packet is QuicShortPacket shor)
            {
                
            }
            else if (packet is QuicVersionPacket version)
            {
                
            }
            else if (packet is QuicInitialPacket initial)
            {
                
            }
            else if (packet is QuicZeroRttPacket zero)
            {
                
            }
            else if (packet is QuicHandshakePacket handshake)
            {
                
            }
            else if (packet is QuicRetryPacket retry)
            {
                
            }

            if (done) break;
        }
    }
    public async Task IncomingAsync()
    {
        var (endPoint, bytes) = await ReceiveDgramAsync();
        int pos = 0;
        
        while (pos < bytes.Length)
        {
            var (done, packet) = IQuicPacket.Parse(ref pos, ScidLength, bytes);
            
            if (packet is QuicShortPacket shor)
            {
                
            }
            else if (packet is QuicVersionPacket version)
            {
                
            }
            else if (packet is QuicInitialPacket initial)
            {
                
            }
            else if (packet is QuicZeroRttPacket zero)
            {
                
            }
            else if (packet is QuicHandshakePacket handshake)
            {
                
            }
            else if (packet is QuicRetryPacket retry)
            {
                
            }

            if (done) break;
        }
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