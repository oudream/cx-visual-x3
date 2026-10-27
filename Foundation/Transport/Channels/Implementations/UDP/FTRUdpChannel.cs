using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRUdpChannel : FTRSingleChannelBase
    {
        private readonly FTRUdpChannelConfiguration _config;
        private UdpClient _udp;
        private CancellationTokenSource _shutdown;
        private Task _receiveTask;
        public FTRUdpChannel(FTRChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            if (configuration.Kind != FTRChannelKind.Udp) throw new ArgumentException("不是 UDP 单对端配置");
            _config = configuration as FTRUdpChannelConfiguration ?? throw new ArgumentException("要求 FTRUdpChannelConfiguration");
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _udp = new UdpClient();
            _udp.Connect(_config.Host, _config.Port);
            _shutdown = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_shutdown.Token));
            return Task.CompletedTask;
        }
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                await Task.Yield();
                while (!ct.IsCancellationRequested)
                {
                    UdpReceiveResult packet = await _udp.ReceiveAsync().ConfigureAwait(false);
                    Receive(packet.Buffer);
                }
            }
            catch (Exception error) { if (!ct.IsCancellationRequested) Fault(error); }
        }
        protected override async Task OnSendAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        {
            if (address.HasSubChannel) throw new ArgumentException("UDP 单对端无 SubChannel");
            ct.ThrowIfCancellationRequested();
            UdpClient client = _udp;
            if (client == null) throw new InvalidOperationException("UDP 通道未启动");
            int n = await client.SendAsync(bytes, bytes.Length).ConfigureAwait(false);
            if (n != bytes.Length) throw new IOException("UDP 发送字节数不完整");
        }
        protected override async Task OnCloseAsync(CancellationToken ct)
        {
            if (_shutdown != null) _shutdown.Cancel();
            if (_udp != null) _udp.Close();
            if (_receiveTask != null) await _receiveTask.ConfigureAwait(false);
            if (_shutdown != null) _shutdown.Dispose();
            _shutdown = null; _udp = null; _receiveTask = null;
        }
    }
}
