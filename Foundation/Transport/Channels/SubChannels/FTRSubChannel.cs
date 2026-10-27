using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRSubChannel
    {
        private readonly IFTRMultiPeerChannel _owner;
        public FTRSubChannel(IFTRMultiPeerChannel owner, FTRSubChannelAddress address)
        {
            _owner = owner ?? throw new ArgumentNullException("owner");
            Address = address ?? throw new ArgumentNullException("address");
            if (!address.HasSubChannel || _owner.FindSubChannel(address) == null)
                throw new ArgumentException("子通道必须属于一个存活的 MultiPeer Channel");
        }
        public FTRSubChannelAddress Address { get; private set; }
        public FTRSubChannelInfo Info
        {
            get { return _owner.FindSubChannel(Address); }
        }
        public FTRSubChannelState State
        {
            get { FTRSubChannelInfo info = Info; return info == null ? FTRSubChannelState.Closed : info.State; }
        }
        public System.Threading.Tasks.Task SendAsync(byte[] data, System.Threading.CancellationToken cancellationToken)
        {
            if (State != FTRSubChannelState.Open)
                throw new InvalidOperationException("子通道已断开或路由代次过期");
            return _owner.SendAsync(data, Address, cancellationToken);
        }
    }
}
