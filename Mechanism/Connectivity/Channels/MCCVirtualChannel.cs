using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // 统一 Virtual 通道：只进行原始字节传输；供 Connector/Protocol、TestCenter 和无硬件 CI 使用。
    // 长度帧、CRC、点表与映射一律交给 Protocol。
    public sealed class MCCVirtualChannel : MCCSingleChannelBase
    {
        private MCCVirtualChannel _peer;
        private MCCVirtualChannel(string id) : base(id) { }
        public static MCCVirtualChannel[] CreatePair(string left, string right)
        {
            if (left == right) throw new ArgumentException("对端 Id 不可相同");
            MCCVirtualChannel a = new MCCVirtualChannel(left);
            MCCVirtualChannel b = new MCCVirtualChannel(right);
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
