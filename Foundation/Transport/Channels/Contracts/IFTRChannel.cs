using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public interface IFTRChannel : IDisposable
    {
        string Id { get; }
        Guid SessionId { get; }
        FTRChannelState State { get; }
        void Open();
        void Close();
        Task OpenAsync(CancellationToken ct);
        Task CloseAsync(CancellationToken ct);
        Task SendAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct);
        void Send(byte[] bytes, string correlationId = null, int segmentBytes = 0);
        void Send(byte[] bytes,int offset,int count,FTRSubChannelAddress address,CancellationToken ct);
        IList<FTRChannelFrame> ReadFrames();
    }
}
