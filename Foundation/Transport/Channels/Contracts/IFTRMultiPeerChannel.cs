using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public interface IFTRMultiPeerChannel : IFTRChannelTransport, IFTRSubChannelAware
    {
        FTRSubChannelInfo RegisterPeer(string peerId, string remoteEndpoint);
        bool RemovePeer(FTRSubChannelAddress address);
    }
}
