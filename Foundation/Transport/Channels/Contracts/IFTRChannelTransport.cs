using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public interface IFTRChannelTransport : IFTRChannel
    {
        FTRChannelTopology Topology { get; }
        event Action<FTRChannelReceiveContext> Received;
        event Action<FTRReceiveBuffer> BufferReceived;
        event Action<FTRChannelState> StateChanged;
        FTRChannelDiagnosticsSnapshot Diagnostics {get;}
    }
}
