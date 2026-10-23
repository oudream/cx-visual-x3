using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // 新的正式纯传输 Virtual；供 Connector/Protocol、TestCenter 和无硬件 CI 使用。
    // 不包含 Parser/Codec。旧 MCCVirtualChannel 的证据会话将独立迁移后统一替换。
    public sealed class MCCVirtualRawChannel : MCCSingleChannelBase
    {
        private MCCVirtualRawChannel _peer;
        private MCCVirtualRawChannel(string id) : base(id) { }
        public static MCCVirtualRawChannel[] CreatePair(string left, string right)
        {
            if (left == right) throw new ArgumentException("对端 Id 不可相同");
            MCCVirtualRawChannel a = new MCCVirtualRawChannel(left);
            MCCVirtualRawChannel b = new MCCVirtualRawChannel(right);
            a._peer = b; b._peer = a;
            return new[] { a, b };
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            if (_peer == null) throw new InvalidOperationException("虚拟对端尚未装配");
            return Task.CompletedTask;
        }
        protected override Task OnCloseAsync(CancellationToken ct) { return Task.CompletedTask; }
        protected override Task OnSendAsync(byte[] bytes, MCCSubChannelAddress address, CancellationToken ct)
        {
            if (address.HasSubChannel) throw new ArgumentException("SinglePeer 不能指定子通道");
            if (_peer == null || _peer.State != MCCChannelState.Open)
                throw new IOException("虚拟接收方已断开");
            _peer.Receive(bytes);
            return Task.CompletedTask;
        }
    }
}
