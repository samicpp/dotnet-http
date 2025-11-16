namespace Samicpp.Http.Quic;

using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

public class QuicServer(Socket socket): IDisposable
{
    readonly Socket udp = socket;
    readonly byte[] window = new byte[65565];
    public EndPoint? Address { get => udp.LocalEndPoint; }
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

    public QuicServer(): this(new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)){}
    public QuicServer(AddressFamily addressFamily): this(new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp)){}
    public QuicServer(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType): this(new Socket(addressFamily, socketType, protocolType)){}

    public void Bind(EndPoint endPoint) => udp.Bind(endPoint);
    public void Bind() => udp.Bind(new IPEndPoint(IPAddress.Any, 0));
    public void Bind(ushort port) => udp.Bind(new IPEndPoint(IPAddress.Any, port));
    public void Bind(string address) => udp.Bind(IPEndPoint.Parse(address));
    public void Bind(string ipAddress, ushort port) => udp.Bind(new IPEndPoint(IPAddress.Parse(ipAddress), port));


    public (EndPoint,byte[]) ReceiveDgram()
    {
        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        int l = udp.ReceiveFrom(window, ref from);
        return (from, [.. window[..l]]);
    }
    public async Task<(EndPoint,byte[])> ReceiveDgramAsync()
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


    public void Dispose() => udp.Dispose();
}