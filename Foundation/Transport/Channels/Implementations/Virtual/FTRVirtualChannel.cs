using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRVirtualChannel : FTRSingleChannelBase
    {
        private FTRVirtualChannel _peer;
        private FTRVirtualChannel(string id) : base(id) { }
        public static FTRVirtualChannel[] CreatePair(string left, string right)
        {
            if (left == right) throw new ArgumentException("对端 Id 不可相同");
            FTRVirtualChannel a = new FTRVirtualChannel(left);
            FTRVirtualChannel b = new FTRVirtualChannel(right);
            a._peer = b; b._peer = a;
            return new[] { a, b };
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            if (_peer == null) throw new InvalidOperationException("虚拟对端尚未装配");
            return Task.CompletedTask;
        }
        protected override Task OnCloseAsync(CancellationToken ct) { return Task.CompletedTask; }
        protected override Task OnSendAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        {
            if (address.HasSubChannel) throw new ArgumentException("SinglePeer 不能指定子通道");
            if (_peer == null || _peer.State != FTRChannelState.Open)
                throw new IOException("虚拟接收方已断开");
            _peer.Receive(bytes);
            return Task.CompletedTask;
        }
    }
}
