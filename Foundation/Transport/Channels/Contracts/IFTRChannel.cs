using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public interface IFTRChannel
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
        IList<FTRChannelFrame> ReadFrames();
    }
}
