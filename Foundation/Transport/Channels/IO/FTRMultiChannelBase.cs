using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public abstract class FTRMultiChannelBase : FTRChannelBase, IFTRMultiPeerChannel
    {
        private readonly FTRSubChannelDirectory _peers;
        public event Action<FTRSubChannelAddress> PeerRemoved;
        protected FTRMultiChannelBase(string id) : base(id, FTRChannelTopology.MultiPeer)
        { _peers = new FTRSubChannelDirectory(id); }
        public FTRSubChannelInfo RegisterPeer(string peerId, string endpoint)
        { return _peers.Register(peerId, endpoint); }
        public virtual bool RemovePeer(FTRSubChannelAddress address)
        {
            if (!_peers.Remove(address)) return false;
            // 同步通知唯一 Protocol 清理分帧/事务状态，防止断线重连继承旧半包。
            Action<FTRSubChannelAddress> listener = PeerRemoved;
            if (listener != null) listener(address);
            return true;
        }
        public FTRSubChannelInfo FindSubChannel(FTRSubChannelAddress address) { return _peers.FindSubChannel(address); }
        public FTRSubChannelInfo[] SnapshotSubChannels() { return _peers.SnapshotSubChannels(); }
        protected void ReceiveFrom(FTRSubChannelAddress address, byte[] data, string correlation = null)
        {
            if (FindSubChannel(address) == null)
                throw new InvalidOperationException("旧代次、非法或断开的 SubChannel");
            ReceiveBytes(address, data, correlation);
        }
        protected sealed override Task OnSendAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        {
            if (FindSubChannel(address) == null)
                throw new InvalidOperationException("多对端通道必须提供存活的 SubChannel 地址");
            return SendToPeerAsync(bytes, address, ct);
        }
        protected abstract Task SendToPeerAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct);
    }
}
