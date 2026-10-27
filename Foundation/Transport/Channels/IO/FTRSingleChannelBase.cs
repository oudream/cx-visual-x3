using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public abstract class FTRSingleChannelBase : FTRChannelBase
    {
        protected FTRSingleChannelBase(string id) : base(id, FTRChannelTopology.SinglePeer) { }
        // 重连为显式生命周期操作；退避/重试计划仍由 MRK/F19 与正式配置策略管理。
        public async Task ReconnectAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await CloseAsync(ct).ConfigureAwait(false);
            await OpenAsync(ct).ConfigureAwait(false);
        }
        protected void Receive(byte[] bytes, string correlation = null)
        { ReceiveBytes(new FTRSubChannelAddress(Id), bytes, correlation); }
    }
}
