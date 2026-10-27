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
public sealed class FTRTcpServerChannel : FTRMultiChannelBase
    {
        private readonly FTRTcpServerChannelConfiguration _config;
        private readonly object _gate = new object();
        private readonly Dictionary<string, TcpClient> _clients = new Dictionary<string, TcpClient>(StringComparer.Ordinal);
        private TcpListener _listener;
        private CancellationTokenSource _shutdown;
        private Task _acceptTask;

        public FTRTcpServerChannel(FTRChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            if (configuration.Kind != FTRChannelKind.TcpServer) throw new ArgumentException("不是 TCP Server 配置");
            _config = configuration as FTRTcpServerChannelConfiguration ?? throw new ArgumentException("要求 FTRTcpServerChannelConfiguration");
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _shutdown = new CancellationTokenSource();
            _listener = new TcpListener(FTRNetworkAddress.Resolve(_config.Host), _config.Port);
            _listener.Start(_config.MaxClients);
            _acceptTask = Task.Run(() => AcceptLoopAsync(_shutdown.Token));
            return Task.CompletedTask;
        }
        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    if (ct.IsCancellationRequested) { client.Close(); break; }
                    FTRSubChannelAddress address;
                    lock (_gate)
                    {
                        if (_clients.Count >= _config.MaxClients) { client.Close(); continue; }
                        FTRSubChannelInfo peer = RegisterPeer(Guid.NewGuid().ToString("N"),
                            client.Client.RemoteEndPoint.ToString());
                        address = peer.Address;
                        _clients.Add(address.ToString(), client);
                    }
                    Task ignored = Task.Run(() => ReadPeerAsync(client, address, ct));
                }
            }
            catch (Exception error)
            {
                if (!ct.IsCancellationRequested) Fault(error);
            }
        }
        private async Task ReadPeerAsync(TcpClient client, FTRSubChannelAddress address, CancellationToken ct)
        {
            try
            {
                byte[] buffer = new byte[32768];
                NetworkStream stream = client.GetStream();
                while (!ct.IsCancellationRequested)
                {
                    int n = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                    if (n <= 0) break;
                    byte[] data = new byte[n]; Buffer.BlockCopy(buffer, 0, data, 0, n);
                    ReceiveFrom(address, data);
                }
            }
            catch (Exception)
            {
                // 一个 TCP 对端断开不应让整个 MultiPeer Server 进入 Faulted。
            }
            finally
            {
                lock (_gate) _clients.Remove(address.ToString());
                RemovePeer(address); client.Close();
            }
        }
        public override bool RemovePeer(FTRSubChannelAddress address)
        {
            if (address == null) return false;
            TcpClient connection;
            lock (_gate)
            {
                if (!base.RemovePeer(address)) return false;
                if (_clients.TryGetValue(address.ToString(), out connection))
                    _clients.Remove(address.ToString());
                else connection = null;
            }
            // 移除逻辑路由的同时释放真实 Socket，禁止后台接收再接纳旧代次数据。
            if (connection != null) connection.Close();
            return true;
        }
        protected override async Task SendToPeerAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        {
            TcpClient client;
            lock (_gate)
                if (!_clients.TryGetValue(address.ToString(), out client))
                    throw new InvalidOperationException("对端不存在或已断线");
            await client.GetStream().WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
        }
        protected override async Task OnCloseAsync(CancellationToken ct)
        {
            if (_shutdown != null) _shutdown.Cancel();
            if (_listener != null) _listener.Stop();
            lock (_gate)
            {
                foreach (TcpClient client in _clients.Values) client.Close();
                _clients.Clear();
            }
            Task accept = _acceptTask;
            if (accept != null) await accept.ConfigureAwait(false);
            foreach (FTRSubChannelInfo peer in SnapshotSubChannels()) RemovePeer(peer.Address);
            if (_shutdown != null) _shutdown.Dispose();
            _shutdown = null; _listener = null; _acceptTask = null;
        }
    }
}
