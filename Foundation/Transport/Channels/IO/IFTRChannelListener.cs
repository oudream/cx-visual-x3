using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public interface IFTRChannelListener
    {
        Task StartListeningAsync(CancellationToken cancellationToken);
        Task<FTRSubChannelAddress> AcceptAsync(CancellationToken cancellationToken);
        Task StopListeningAsync(CancellationToken cancellationToken);
    }
}
