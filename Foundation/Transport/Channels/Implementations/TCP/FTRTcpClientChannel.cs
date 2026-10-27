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
public sealed class FTRTcpClientChannel : FTRSingleChannelBase
    {
        private readonly FTRTcpClientChannelConfiguration _config;
        private TcpClient _tcp;
        private CancellationTokenSource _shutdown;
        private Task _receiveTask;
        public FTRTcpClientChannel(FTRChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            if (configuration.Kind != FTRChannelKind.TcpClient) throw new ArgumentException("不是 TCP Client 配置");
            _config = configuration as FTRTcpClientChannelConfiguration ?? throw new ArgumentException("要求 FTRTcpClientChannelConfiguration");
        }
        protected override async Task OnOpenAsync(CancellationToken ct)
        {
            TcpClient client = new TcpClient();
            // 连接超时/取消只作用于 Open，不应把启动时调用方 Token 当作长期运行 Token。
            CancellationTokenSource shutdown = new CancellationTokenSource();
            try
            {
                using (ct.Register(() => client.Close()))
                    await client.ConnectAsync(_config.Host, _config.Port).ConfigureAwait(false);
                _tcp = client; _shutdown = shutdown;
                _receiveTask = Task.Run(() => ReceiveLoopAsync(shutdown.Token));
            }
            catch { client.Close(); shutdown.Dispose(); throw; }
        }
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                await Task.Yield();
                NetworkStream stream = _tcp.GetStream();
                byte[] buffer = new byte[32768];
                while (!ct.IsCancellationRequested)
                {
                    int count = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                    if (count <= 0) throw new IOException("TCP 对端关闭");
                    byte[] bytes = new byte[count];
                    Buffer.BlockCopy(buffer, 0, bytes, 0, count);
                    Receive(bytes);
                }
            }
            catch (Exception error)
            {
                if (!ct.IsCancellationRequested) Fault(error);
            }
        }
        protected override async Task OnSendAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        {
            if (address.HasSubChannel) throw new ArgumentException("TCP Client 是一对一");
            TcpClient client = _tcp;
            if (client == null || !client.Connected) throw new IOException("TCP 尚未连接");
            await client.GetStream().WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
        }
        protected override async Task OnCloseAsync(CancellationToken ct)
        {
            CancellationTokenSource closing = _shutdown;
            if (closing != null) closing.Cancel();
            TcpClient client = _tcp;
            if (client != null) client.Close();
            Task task = _receiveTask;
            if (task != null) await task.ConfigureAwait(false);
            if (closing != null) closing.Dispose();
            _receiveTask = null; _tcp = null; _shutdown = null;
        }
    }
}
