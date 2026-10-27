using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public interface IFTRDatagramIO
    {
        Task<FTRDatagram> ReceiveFromAsync(CancellationToken cancellationToken);
        Task SendToAsync(FTRDatagram datagram, CancellationToken cancellationToken);
    }
}
