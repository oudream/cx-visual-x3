using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // Channel 只拥有物理/虚拟数据传输，不解释 Frame 或应用 Protocol。
    public interface IMCCChannelTransport : IMCCChannel
    {
        MCCChannelTopology Topology { get; }
        event Action<MCCChannelReceiveContext> Received;
    }
    public interface IMCCMultiPeerChannel : IMCCChannelTransport, IMCCSubChannelAware
    {
        MCCSubChannelInfo RegisterPeer(string peerId, string remoteEndpoint);
        bool RemovePeer(MCCSubChannelAddress address);
    }

    public abstract class MCCChannelBase : IMCCChannelTransport
    {
        private readonly object _gate = new object();
        private readonly List<MCCChannelFrame> _frames = new List<MCCChannelFrame>();
        private long _sequence;
        private MCCChannelState _state = MCCChannelState.Closed;
        protected MCCChannelBase(string id, MCCChannelTopology topology)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("通道 Id 不可为空");
            Id = id.Trim(); Topology = topology; SessionId = Guid.NewGuid();
        }
        public string Id { get; private set; }
        public Guid SessionId { get; private set; }
        public MCCChannelTopology Topology { get; private set; }
        public MCCChannelState State { get { lock (_gate) return _state; } }
        public event Action<MCCChannelReceiveContext> Received;
        public event Action<MCCChannelFrame> FrameRecorded;
        public void Open() { OpenAsync(CancellationToken.None).GetAwaiter().GetResult(); }
        public void Close() { CloseAsync(CancellationToken.None).GetAwaiter().GetResult(); }
        public async Task OpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate) if (_state != MCCChannelState.Closed)
                throw new InvalidOperationException("通道不是 Closed");
            try { await OnOpenAsync(ct).ConfigureAwait(false); lock (_gate) _state = MCCChannelState.Open; }
            catch { lock (_gate) _state = MCCChannelState.Faulted; throw; }
        }
        public async Task CloseAsync(CancellationToken ct)
        {
            // Faulted 仍需执行 Close 清理，不能把 Faulted 当作关闭成功。
            await OnCloseAsync(ct).ConfigureAwait(false);
            lock (_gate) _state = MCCChannelState.Closed;
        }
        public void Send(byte[] bytes, string correlationId = null, int segmentBytes = 0)
        {
            if (segmentBytes < 0) throw new ArgumentOutOfRangeException("segmentBytes");
            SendAsync(bytes, new MCCSubChannelAddress(Id), CancellationToken.None).GetAwaiter().GetResult();
        }
        public async Task SendAsync(byte[] bytes, MCCSubChannelAddress address, CancellationToken ct)
        {
            if (bytes == null || bytes.Length == 0) throw new ArgumentException("数据不能为空");
            if (address == null || address.ChannelId != Id) throw new ArgumentException("通道身份不匹配");
            ct.ThrowIfCancellationRequested();
            if (State != MCCChannelState.Open) throw new InvalidOperationException("通道尚未打开");
            byte[] owned = (byte[])bytes.Clone();
            try
            {
                await OnSendAsync(owned, address, ct).ConfigureAwait(false);
                Record(MCCFrameDirection.Tx, owned, null, null);
            }
            catch (Exception e)
            {
                Record(MCCFrameDirection.Error, owned, null, e.Message);
                throw;
            }
        }
        public IList<MCCChannelFrame> ReadFrames()
        {
            lock (_gate) return _frames.ToArray();
        }
        protected void ReceiveBytes(MCCSubChannelAddress address, byte[] bytes, string correlationId = null)
        {
            if (State != MCCChannelState.Open) throw new IOException("接收通道未打开");
            if (address == null || address.ChannelId != Id) throw new ArgumentException("接收地址不属于本通道");
            MCCChannelReceiveContext context = new MCCChannelReceiveContext(address, bytes, correlationId);
            Record(MCCFrameDirection.Rx, bytes, correlationId, null);
            Action<MCCChannelReceiveContext> handler = Received;
            if (handler != null) handler(context);
        }
        protected void Fault(Exception error)
        {
            if (error == null) throw new ArgumentNullException("error");
            lock (_gate) _state = MCCChannelState.Faulted;
            Record(MCCFrameDirection.Error, new byte[0], null, error.Message);
        }
        private void Record(MCCFrameDirection direction, byte[] bytes, string correlation, string error)
        {
            MCCChannelFrame entry;
            lock (_gate)
            {
                entry = new MCCChannelFrame(++_sequence, SessionId, Id, direction, bytes,
                    correlation, error, null);
                if (_frames.Count == 10000) _frames.RemoveAt(0);
                _frames.Add(entry);
            }
            Action<MCCChannelFrame> handler = FrameRecorded;
            if (handler != null) handler(entry);
        }
        protected abstract Task OnOpenAsync(CancellationToken ct);
        protected abstract Task OnCloseAsync(CancellationToken ct);
        protected abstract Task OnSendAsync(byte[] bytes, MCCSubChannelAddress destination, CancellationToken ct);
    }

    public abstract class MCCSingleChannelBase : MCCChannelBase
    {
        protected MCCSingleChannelBase(string id) : base(id, MCCChannelTopology.SinglePeer) { }
        // 重连为显式生命周期操作；退避/重试计划仍由 MRK/F19 与正式配置策略管理。
        public async Task ReconnectAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await CloseAsync(ct).ConfigureAwait(false);
            await OpenAsync(ct).ConfigureAwait(false);
        }
        protected void Receive(byte[] bytes, string correlation = null)
        { ReceiveBytes(new MCCSubChannelAddress(Id), bytes, correlation); }
    }

    public abstract class MCCMultiChannelBase : MCCChannelBase, IMCCMultiPeerChannel
    {
        private readonly MCCSubChannelDirectory _peers;
        protected MCCMultiChannelBase(string id) : base(id, MCCChannelTopology.MultiPeer)
        { _peers = new MCCSubChannelDirectory(id); }
        public MCCSubChannelInfo RegisterPeer(string peerId, string endpoint)
        { return _peers.Register(peerId, endpoint); }
        public virtual bool RemovePeer(MCCSubChannelAddress address) { return _peers.Remove(address); }
        public MCCSubChannelInfo FindSubChannel(MCCSubChannelAddress address) { return _peers.FindSubChannel(address); }
        public MCCSubChannelInfo[] SnapshotSubChannels() { return _peers.SnapshotSubChannels(); }
        protected void ReceiveFrom(MCCSubChannelAddress address, byte[] data, string correlation = null)
        {
            if (FindSubChannel(address) == null)
                throw new InvalidOperationException("旧代次、非法或断开的 SubChannel");
            ReceiveBytes(address, data, correlation);
        }
        protected sealed override Task OnSendAsync(byte[] bytes, MCCSubChannelAddress address, CancellationToken ct)
        {
            if (FindSubChannel(address) == null)
                throw new InvalidOperationException("多对端通道必须提供存活的 SubChannel 地址");
            return SendToPeerAsync(bytes, address, ct);
        }
        protected abstract Task SendToPeerAsync(byte[] bytes, MCCSubChannelAddress address, CancellationToken ct);
    }
}
