namespace Samicpp.Http.Quic;

using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

// handles read loop etc., to ensure proper connection management
// will also support rfc 9221 https://datatracker.ietf.org/doc/html/rfc9221
public class QuicServer(Socket socket, X509Certificate2 cert): IDisposable
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

    public QuicServer(QuicServerConfig config): this(new Socket(config.AddressFamily, config.SocketType, config.ProtocolType), config.Certificate)
    {
        if (config.DualMode) udp.DualMode = true;
        udp.Bind(config.Address);
    }
    // public QuicServer(X509Certificate2 cert): this(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)) { }
    // public QuicServer(AddressFamily addressFamily): this(new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp)){}
    // public QuicServer(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType): this(new Socket(addressFamily, socketType, protocolType)){}

    private void Bind(EndPoint endPoint) => udp.Bind(endPoint);
    private void Bind() => udp.Bind(new IPEndPoint(IPAddress.Any, 0));
    private void Bind(ushort port) => udp.Bind(new IPEndPoint(IPAddress.Any, port));
    private void Bind(string address) => udp.Bind(IPEndPoint.Parse(address));
    private void Bind(string ipAddress, ushort port) => udp.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), port));


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

    protected (EndPoint,List<IQuicPacket>) Receive()
    {
        var (from, bytes) = ReceiveDgram();
        return (from, IQuicPacket.ParseAll(ScidLength, bytes));
    }
    protected async Task<(EndPoint,List<IQuicPacket>)> ReceiveAsync()
    {
        var (from, bytes) = await ReceiveDgramAsync();
        return (from, IQuicPacket.ParseAll(ScidLength, bytes));
    }

    
    

    public void Dispose() => udp.Dispose();
}

public class QuicServerConfig()
{
    public required EndPoint Address { get; init; }
    public required X509Certificate2 Certificate { get; init; }
    public AddressFamily AddressFamily { get; init; } = AddressFamily.InterNetwork;
    public SocketType SocketType { get; init; } = SocketType.Dgram;
    public ProtocolType ProtocolType { get; init; } = ProtocolType.Udp;
    public bool DualMode { get; init; } = true;

    public static QuicServerConfig FromPKCS12((string path, string password) pkcs12, EndPoint endPoint)
    {
        return new(){
            Address = endPoint,
            Certificate = X509CertificateLoader.LoadPkcs12FromFile(pkcs12.path, pkcs12.password)
        };
    }
    public static QuicServerConfig FromPKCS12((string,string) pkcs12, ushort port) => FromPKCS12(pkcs12, new IPEndPoint(IPAddress.Any, port));
    public static QuicServerConfig FromPKCS12((string,string) pkcs12, string address) => FromPKCS12(pkcs12, IPEndPoint.Parse(address));

    public static QuicServerConfig SelfSigned(EndPoint endPoint)
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
    public static QuicServerConfig SelfSigned(ushort port) => SelfSigned(new IPEndPoint(IPAddress.Any, port));
    public static QuicServerConfig SelfSigned(string address) => SelfSigned(IPEndPoint.Parse(address));
}