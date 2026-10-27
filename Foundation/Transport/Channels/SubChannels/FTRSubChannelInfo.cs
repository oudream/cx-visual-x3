using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRSubChannelInfo
    {
        public FTRSubChannelInfo(FTRSubChannelAddress address, string remoteEndpoint, FTRSubChannelState state)
        {
            Address = address ?? throw new ArgumentNullException("address");
            if (!address.HasSubChannel) throw new ArgumentException("子通道必须有子通道身份");
            RemoteEndpoint = remoteEndpoint; State = state; CreatedUtc = DateTime.UtcNow;
        }
        public FTRSubChannelAddress Address { get; private set; }
        public string RemoteEndpoint { get; private set; }
        public FTRSubChannelState State { get; private set; }
        public DateTime CreatedUtc { get; private set; }
    }
}
