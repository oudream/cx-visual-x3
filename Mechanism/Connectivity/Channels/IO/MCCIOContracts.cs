using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // I/O 行为是正交能力：TCP/Serial=ByteStream, UDP=Datagram；不把应用规约 Request 强制放进通道。
    [Flags]
    public enum MCCIOCapabilities
    {
        None = 0, ByteStream = 1, Datagram = 2, Message = 4,
        RequestResponse = 8, SharedBuffer = 16, Listener = 32
    }
    public enum MCCChannelTopology { SinglePeer, MultiPeer, SharedBus }
    public enum MCCChannelIOMode { AsyncIO, DedicatedBlocking }

    public interface IMCCByteStream
    {
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    }

    public interface IMCCDatagramIO
    {
        Task<MCCDatagram> ReceiveFromAsync(CancellationToken cancellationToken);
        Task SendToAsync(MCCDatagram datagram, CancellationToken cancellationToken);
    }

    public interface IMCCChannelListener
    {
        Task StartListeningAsync(CancellationToken cancellationToken);
        Task<MCCSubChannelAddress> AcceptAsync(CancellationToken cancellationToken);
        Task StopListeningAsync(CancellationToken cancellationToken);
    }

    // 子通道选择按通道内部的具体路由能力执行；一个 Channel 并不必然有多个实际 Socket。
    public interface IMCCSubChannelAware
    {
        MCCSubChannelInfo FindSubChannel(MCCSubChannelAddress address);
        MCCSubChannelInfo[] SnapshotSubChannels();
    }

    public sealed class MCCDatagram
    {
        private readonly byte[] _data;
        public MCCDatagram(string remoteEndpoint, byte[] data)
        {
            if (string.IsNullOrWhiteSpace(remoteEndpoint)) throw new ArgumentException("数据报目标不能为空");
            if (data == null) throw new ArgumentNullException("data");
            RemoteEndpoint = remoteEndpoint.Trim(); _data = (byte[])data.Clone();
        }
        public string RemoteEndpoint { get; private set; }
        public byte[] Data { get { return (byte[])_data.Clone(); } }
    }

    public sealed class MCCChannelReceiveContext
    {
        private readonly byte[] _data;
        public MCCChannelReceiveContext(MCCSubChannelAddress address, byte[] data, string correlationId = null)
        {
            if (address == null) throw new ArgumentNullException("address");
            if (data == null) throw new ArgumentNullException("data");
            Address = address; _data = (byte[])data.Clone();
            CorrelationId = correlationId; TimestampUtc = DateTime.UtcNow;
        }
        public MCCSubChannelAddress Address { get; private set; }
        public byte[] Data { get { return (byte[])_data.Clone(); } }
        public string CorrelationId { get; private set; }
        public DateTime TimestampUtc { get; private set; }
    }
}
