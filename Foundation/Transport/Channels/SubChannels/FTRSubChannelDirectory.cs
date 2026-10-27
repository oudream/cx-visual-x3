using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRSubChannelDirectory : IFTRSubChannelAware
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, FTRSubChannelInfo> _items =
            new Dictionary<string, FTRSubChannelInfo>(StringComparer.Ordinal);
        private long _generation;
        public FTRSubChannelDirectory(string parentChannelId)
        {
            if (string.IsNullOrWhiteSpace(parentChannelId)) throw new ArgumentException("父通道 Id 必填");
            ParentChannelId = parentChannelId.Trim();
        }
        public string ParentChannelId { get; private set; }
        public FTRSubChannelInfo Register(string subChannelId, string remoteEndpoint)
        {
            if (string.IsNullOrWhiteSpace(subChannelId)) throw new ArgumentException("子通道 Id 必填");
            lock (_gate)
            {
                string id = subChannelId.Trim();
                if (_items.ContainsKey(id)) throw new InvalidOperationException("子通道仍然存在: " + id);
                if (_generation == long.MaxValue) throw new InvalidOperationException("子通道代次已经用尽");
                FTRSubChannelInfo item = new FTRSubChannelInfo(
                    new FTRSubChannelAddress(ParentChannelId, id, ++_generation), remoteEndpoint, FTRSubChannelState.Open);
                _items.Add(id, item);
                return item;
            }
        }
        public FTRSubChannelInfo Find(FTRSubChannelAddress address) { return FindSubChannel(address); }
        public FTRSubChannelInfo FindSubChannel(FTRSubChannelAddress address)
        {
            if (address == null || !string.Equals(address.ChannelId, ParentChannelId, StringComparison.Ordinal)) return null;
            lock (_gate)
            {
                FTRSubChannelInfo result;
                return address.HasSubChannel && _items.TryGetValue(address.SubChannelId, out result) &&
                    result.Address.Equals(address) ? result : null;
            }
        }
        public bool Remove(FTRSubChannelAddress address)
        {
            if (address == null) return false;
            lock (_gate)
            {
                FTRSubChannelInfo existing;
                if (!string.Equals(address.ChannelId, ParentChannelId, StringComparison.Ordinal) ||
                    !address.HasSubChannel || !_items.TryGetValue(address.SubChannelId, out existing) ||
                    !existing.Address.Equals(address)) return false;
                return _items.Remove(address.SubChannelId);
            }
        }
        public FTRSubChannelInfo[] SnapshotSubChannels()
        {
            lock (_gate) return _items.Values.ToArray();
        }
    }
}
