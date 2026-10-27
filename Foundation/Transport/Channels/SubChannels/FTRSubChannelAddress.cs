using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRSubChannelAddress : IEquatable<FTRSubChannelAddress>
    {
        public FTRSubChannelAddress(string channelId, string subChannelId = null, long generation = 0)
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
        public bool Equals(FTRSubChannelAddress other)
        {
            return other != null && string.Equals(ChannelId, other.ChannelId, StringComparison.Ordinal) &&
                string.Equals(SubChannelId, other.SubChannelId, StringComparison.Ordinal) && Generation == other.Generation;
        }
        public override bool Equals(object value) { return Equals(value as FTRSubChannelAddress); }
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
}
