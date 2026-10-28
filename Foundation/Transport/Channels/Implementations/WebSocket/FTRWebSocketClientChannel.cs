using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRWebSocketClientChannel : FTRSingleChannelBase, IFTRMessageIO
    {
        private readonly FTRWebSocketClientChannelConfiguration _config;
        private readonly SemaphoreSlim _sendGate = new SemaphoreSlim(1, 1);
        private ClientWebSocket _socket;
        private CancellationTokenSource _stop;
        private Task _reader;

        public FTRWebSocketClientChannel(FTRChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            _config = configuration as FTRWebSocketClientChannelConfiguration ??
                throw new ArgumentException("必须使用 FTRWebSocketClientChannelConfiguration");
        }

        protected override async Task OnOpenAsync(CancellationToken ct)
        {
            ClientWebSocket socket = new ClientWebSocket();
            _socket = socket;
            using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(_config.ConnectTimeoutMs);
                await socket.ConnectAsync(new Uri(_config.Url), timeout.Token).ConfigureAwait(false);
            }
            _stop = new CancellationTokenSource();
            _reader = Task.Run(() => ReceiveLoop(socket, _stop.Token));
        }

        private async Task ReceiveLoop(ClientWebSocket socket, CancellationToken ct)
        {
            try
            {
                byte[] buffer = new byte[8192];
                using (MemoryStream incoming = new MemoryStream())
                {
                    while (!ct.IsCancellationRequested)
                    {
                        WebSocketReceiveResult result = await socket.ReceiveAsync(
                            new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                            throw new IOException("WebSocket 对端关闭了连接");
                        if (result.MessageType != WebSocketMessageType.Binary)
                            throw new NotSupportedException("此 Channel 仅传输二进制消息");
                        if (incoming.Length + result.Count > _config.MaxMessageBytes)
                            throw new IOException("WebSocket 消息超过接收预算");
                        incoming.Write(buffer, 0, result.Count);
                        if (result.EndOfMessage)
                        {
                            Receive(incoming.ToArray());
                            incoming.SetLength(0);
                        }
                    }
                }
            }
            catch (Exception error) { if (!ct.IsCancellationRequested) Fault(error); }
        }

        public Task SendMessageAsync(byte[] bytes,FTRSubChannelAddress target,CancellationToken ct)
        {return SendAsync(bytes,target,ct);}

        protected override async Task OnSendAsync(byte[] bytes, FTRSubChannelAddress target, CancellationToken ct)
        {
            if (target.HasSubChannel) throw new ArgumentException("WebSocket Client 不使用子通道");
            await _sendGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ClientWebSocket socket = _socket;
                if (socket == null || socket.State != WebSocketState.Open)
                    throw new IOException("WebSocket 尚未连接");
                await socket.SendAsync(new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Binary, true, ct).ConfigureAwait(false);
            }
            finally { _sendGate.Release(); }
        }

        protected override async Task OnCloseAsync(CancellationToken ct)
        {
            if (_stop != null) _stop.Cancel();
            if (_socket != null)
            {
                // 中断挂起的读取，禁止 CloseAsync 无限等待网络对端响应。
                _socket.Abort();
                _socket.Dispose();
            }
            if (_reader != null) await _reader.ConfigureAwait(false);
            if (_stop != null) _stop.Dispose();
            _socket = null; _stop = null; _reader = null;
        }
    }
}
