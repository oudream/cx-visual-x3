using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public interface IFTRSubChannelAware
    {
        FTRSubChannelInfo FindSubChannel(FTRSubChannelAddress address);
        FTRSubChannelInfo[] SnapshotSubChannels();
    }
}
