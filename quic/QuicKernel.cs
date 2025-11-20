namespace Samicpp.Http.Quic;

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

// handles read loop etc., to ensure proper connection management
// will also support rfc 9221 https://datatracker.ietf.org/doc/html/rfc9221
public class QuicKernel(Socket socket, X509Certificate2 cert): IDisposable
{
    readonly Socket udp = socket;
    readonly byte[] window = new byte[65565];
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

    
    static readonly byte[] InitialSalt = [
        0x38, 0x76, 0x2c, 0xf7, 0xf5, 0x59, 0x34, 0xb3,
        0x4d, 0x17, 0x9a, 0xe6, 0xa4, 0xc8, 0x0c, 0xad,
        0xcc, 0xbb, 0x7f, 0x0a
    ];
    /*public static (byte[] key, byte[] iv, byte[] hp) DeriveInitialKeys(byte[] dcid)
    {
        using HMACSHA256 hmac = new(InitialSalt);
        byte[] initialSecret = hmac.ComputeHash(dcid);

        return 
        (
            HkdfExpandLabel(initialSecret, "client in", 16),
            HkdfExpandLabel(initialSecret, "quic iv", 12),
            HkdfExpandLabel(initialSecret, "quic hp", 16)
        );
    }*/
    private static byte[] HkdfExpandLabel(byte[] secret, string label, int length)
    {
        byte[] fullLabel = Encoding.ASCII.GetBytes("tls13 " + label);
        byte[] info = new byte[fullLabel.Length + 4];

        info[0] = (byte)(length >> 8);
        info[1] = (byte)(length & 0xff);

        info[2] = (byte)fullLabel.Length;

        Buffer.BlockCopy(fullLabel, 0, info, 3, fullLabel.Length);

        info[info.Length - 1] = 0;

        using var hkdf = new HMACSHA256(secret);
        byte[] prk = hkdf.ComputeHash(info);
        return prk[..length];
    }

    public void HandlePacket(IQuicPacket packet)
    {
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
    }
    public async Task HandlePacketAsync(IQuicPacket packet)
    {
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
        sanBuilder.AddDnsName("localhost");
        req.CertificateExtensions.Add(sanBuilder.Build());

        X509Certificate2 cert = req.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1)
        );

        return new()
        {
            Address = endPoint,
            Certificate = cert,
        };
    }
    public static QuicConfig SelfSigned(ushort port) => SelfSigned(new IPEndPoint(IPAddress.Any, port));
    public static QuicConfig SelfSigned(string address) => SelfSigned(IPEndPoint.Parse(address));
}