using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Connectivity
{
    // Generation 每次登记递增，防止断线重连后旧路由意外发到新客户端。
    public sealed class MCCSubChannelAddress : IEquatable<MCCSubChannelAddress>
    {
        public MCCSubChannelAddress(string channelId, string subChannelId = null, long generation = 0)
        {
            if (string.IsNullOrWhiteSpace(channelId)) throw new ArgumentException("父通道 Id 必填", "channelId");
            if (string.IsNullOrWhiteSpace(subChannelId) && generation != 0) throw new ArgumentException("无子通道时 Generation 必须为 0");
            if (!string.IsNullOrWhiteSpace(subChannelId) && generation < 1) throw new ArgumentOutOfRangeException("generation");
            ChannelId = channelId.Trim(); SubChannelId = string.IsNullOrWhiteSpace(subChannelId) ? null : subChannelId.Trim(); Generation = generation;
        }
        public string ChannelId { get; private set; }
        public string SubChannelId { get; private set; }
        public long Generation { get; private set; }
        public bool HasSubChannel { get { return SubChannelId != null; } }
        public bool Equals(MCCSubChannelAddress other)
        {
            return other != null && string.Equals(ChannelId, other.ChannelId, StringComparison.Ordinal) &&
                string.Equals(SubChannelId, other.SubChannelId, StringComparison.Ordinal) && Generation == other.Generation;
        }
        public override bool Equals(object value) { return Equals(value as MCCSubChannelAddress); }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(ChannelId);
                hash = hash * 31 + (SubChannelId == null ? 0 : StringComparer.Ordinal.GetHashCode(SubChannelId));
                return hash * 31 + Generation.GetHashCode();
            }
        }
        public override string ToString() { return HasSubChannel ? ChannelId + "/" + SubChannelId + "@" + Generation : ChannelId; }
    }

    public enum MCCSubChannelState { Open, Closed, Faulted }

    public sealed class MCCSubChannelInfo
    {
        public MCCSubChannelInfo(MCCSubChannelAddress address, string remoteEndpoint, MCCSubChannelState state)
        {
            Address = address ?? throw new ArgumentNullException("address");
            if (!address.HasSubChannel) throw new ArgumentException("子通道必须有子通道身份");
            RemoteEndpoint = remoteEndpoint; State = state; CreatedUtc = DateTime.UtcNow;
        }
        public MCCSubChannelAddress Address { get; private set; }
        public string RemoteEndpoint { get; private set; }
        public MCCSubChannelState State { get; private set; }
        public DateTime CreatedUtc { get; private set; }
    }

    // SubChannel 是父通道的运行期对端视图；不持有第二份 Socket/注册表。
    public sealed class MCCSubChannel
    {
        private readonly IMCCMultiPeerChannel _owner;
        public MCCSubChannel(IMCCMultiPeerChannel owner, MCCSubChannelAddress address)
        {
            _owner = owner ?? throw new ArgumentNullException("owner");
            Address = address ?? throw new ArgumentNullException("address");
            if (!address.HasSubChannel || _owner.FindSubChannel(address) == null)
                throw new ArgumentException("子通道必须属于一个存活的 MultiPeer Channel");
        }
        public MCCSubChannelAddress Address { get; private set; }
        public MCCSubChannelInfo Info
        {
            get { return _owner.FindSubChannel(Address); }
        }
        public MCCSubChannelState State
        {
            get { MCCSubChannelInfo info = Info; return info == null ? MCCSubChannelState.Closed : info.State; }
        }
        public System.Threading.Tasks.Task SendAsync(byte[] data, System.Threading.CancellationToken cancellationToken)
        {
            if (State != MCCSubChannelState.Open)
                throw new InvalidOperationException("子通道已断开或路由代次过期");
            return _owner.SendAsync(data, Address, cancellationToken);
        }
    }

    // 通道内部使用的线程安全子通道表：不是新的全局 Manager。
    public sealed class MCCSubChannelDirectory : IMCCSubChannelAware
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MCCSubChannelInfo> _items =
            new Dictionary<string, MCCSubChannelInfo>(StringComparer.Ordinal);
        private long _generation;
        public MCCSubChannelDirectory(string parentChannelId)
        {
            if (string.IsNullOrWhiteSpace(parentChannelId)) throw new ArgumentException("父通道 Id 必填");
            ParentChannelId = parentChannelId.Trim();
        }
        public string ParentChannelId { get; private set; }
        public MCCSubChannelInfo Register(string subChannelId, string remoteEndpoint)
        {
            if (string.IsNullOrWhiteSpace(subChannelId)) throw new ArgumentException("子通道 Id 必填");
            lock (_gate)
            {
                string id = subChannelId.Trim();
                if (_items.ContainsKey(id)) throw new InvalidOperationException("子通道仍然存在: " + id);
                if (_generation == long.MaxValue) throw new InvalidOperationException("子通道代次已经用尽");
                MCCSubChannelInfo item = new MCCSubChannelInfo(
                    new MCCSubChannelAddress(ParentChannelId, id, ++_generation), remoteEndpoint, MCCSubChannelState.Open);
                _items.Add(id, item);
                return item;
            }
        }
        public MCCSubChannelInfo Find(MCCSubChannelAddress address) { return FindSubChannel(address); }
        public MCCSubChannelInfo FindSubChannel(MCCSubChannelAddress address)
        {
            if (address == null || !string.Equals(address.ChannelId, ParentChannelId, StringComparison.Ordinal)) return null;
            lock (_gate)
            {
                MCCSubChannelInfo result;
                return address.HasSubChannel && _items.TryGetValue(address.SubChannelId, out result) &&
                    result.Address.Equals(address) ? result : null;
            }
        }
        public bool Remove(MCCSubChannelAddress address)
        {
            if (address == null) return false;
            lock (_gate)
            {
                MCCSubChannelInfo existing;
                if (!string.Equals(address.ChannelId, ParentChannelId, StringComparison.Ordinal) ||
                    !address.HasSubChannel || !_items.TryGetValue(address.SubChannelId, out existing) ||
                    !existing.Address.Equals(address)) return false;
                return _items.Remove(address.SubChannelId);
            }
        }
        public MCCSubChannelInfo[] SnapshotSubChannels()
        {
            lock (_gate) return _items.Values.ToArray();
        }
    }
}
