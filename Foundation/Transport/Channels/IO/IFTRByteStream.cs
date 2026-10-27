using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public interface IFTRByteStream
    {
        Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
        Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
    }
}
