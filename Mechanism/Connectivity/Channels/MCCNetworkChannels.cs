using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // 直接按 ReferTo TCP/UDP 成熟收发语义重构到唯一 Channel Owner；不保留 Legacy Adapter。
    // 网络接收由 Socket 异步完成，不为每个连接创建一个阻塞线程。
    internal static class MCCNetworkAddress
    {
        internal static IPAddress Resolve(string host)
        {
            IPAddress ip;
            if (IPAddress.TryParse(host, out ip)) return ip;
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            ip = addresses.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
            if (ip == null) throw new ArgumentException("无可用 IPv4 地址：" + host);
            return ip;
        }
    }

    public sealed class MCCTcpClientChannel : MCCSingleChannelBase
    {
        private readonly MCCChannelConfiguration _config;
        private TcpClient _tcp;
        private CancellationTokenSource _shutdown;
        private Task _receiveTask;
        public MCCTcpClientChannel(MCCChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            if (configuration.Kind != MCCChannelKind.TcpClient) throw new ArgumentException("不是 TCP Client 配置");
            _config = configuration;
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
        protected override async Task OnSendAsync(byte[] bytes, MCCSubChannelAddress address, CancellationToken ct)
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

    public sealed class MCCTcpServerChannel : MCCMultiChannelBase
    {
        private readonly MCCChannelConfiguration _config;
        private readonly object _gate = new object();
        private readonly Dictionary<string, TcpClient> _clients = new Dictionary<string, TcpClient>(StringComparer.Ordinal);
        private TcpListener _listener;
        private CancellationTokenSource _shutdown;
        private Task _acceptTask;

        public MCCTcpServerChannel(MCCChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            if (configuration.Kind != MCCChannelKind.TcpServer) throw new ArgumentException("不是 TCP Server 配置");
            _config = configuration;
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _shutdown = new CancellationTokenSource();
            _listener = new TcpListener(MCCNetworkAddress.Resolve(_config.Host), _config.Port);
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
                    MCCSubChannelAddress address;
                    lock (_gate)
                    {
                        if (_clients.Count >= _config.MaxClients) { client.Close(); continue; }
                        MCCSubChannelInfo peer = RegisterPeer(Guid.NewGuid().ToString("N"),
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
        private async Task ReadPeerAsync(TcpClient client, MCCSubChannelAddress address, CancellationToken ct)
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
        public override bool RemovePeer(MCCSubChannelAddress address)
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
        protected override async Task SendToPeerAsync(byte[] bytes, MCCSubChannelAddress address, CancellationToken ct)
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
            foreach (MCCSubChannelInfo peer in SnapshotSubChannels()) RemovePeer(peer.Address);
            if (_shutdown != null) _shutdown.Dispose();
            _shutdown = null; _listener = null; _acceptTask = null;
        }
    }

    public sealed class MCCUdpChannel : MCCSingleChannelBase
    {
        private readonly MCCChannelConfiguration _config;
        private UdpClient _udp;
        private CancellationTokenSource _shutdown;
        private Task _receiveTask;
        public MCCUdpChannel(MCCChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            if (configuration.Kind != MCCChannelKind.Udp) throw new ArgumentException("不是 UDP 单对端配置");
            _config = configuration;
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
        protected override async Task OnSendAsync(byte[] bytes, MCCSubChannelAddress address, CancellationToken ct)
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

    // UDP Server 的 SubChannel 是远端 Endpoint 的逻辑身份，不存在 TCP Accept。
    public sealed class MCCUdpServerChannel : MCCMultiChannelBase
    {
        private readonly MCCChannelConfiguration _config;
        private readonly object _gate = new object();
        private readonly Dictionary<string, MCCSubChannelAddress> _addresses =
            new Dictionary<string, MCCSubChannelAddress>(StringComparer.Ordinal);
        private UdpClient _udp;
        private CancellationTokenSource _shutdown;
        private Task _receiveTask;
        public MCCUdpServerChannel(MCCChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            if (configuration.Kind != MCCChannelKind.UdpServer) throw new ArgumentException("不是 UDP Server 配置");
            _config = configuration;
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _udp = new UdpClient(new IPEndPoint(MCCNetworkAddress.Resolve(_config.Host), _config.Port));
            _shutdown = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_shutdown.Token));
            return Task.CompletedTask;
        }
        public override bool RemovePeer(MCCSubChannelAddress address)
        {
            if (address == null) return false;
            lock (_gate)
            {
                MCCSubChannelInfo peer = FindSubChannel(address);
                if (peer == null) return false;
                _addresses.Remove(peer.RemoteEndpoint);
                return base.RemovePeer(address);
            }
        }
        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                await Task.Yield();
                while (!ct.IsCancellationRequested)
                {
                    UdpReceiveResult packet = await _udp.ReceiveAsync().ConfigureAwait(false);
                    string endpoint = packet.RemoteEndPoint.ToString();
                    MCCSubChannelAddress address;
                    lock (_gate)
                    {
                        if (!_addresses.TryGetValue(endpoint, out address))
                        {
                            // UDP 没有 TCP Accept；仍须限制逻辑对端数量，防止任意源地址耗尽内存。
                            if (_addresses.Count >= _config.MaxClients) continue;
                            address = RegisterPeer(endpoint, endpoint).Address;
                            _addresses.Add(endpoint, address);
                        }
                    }
                    ReceiveFrom(address, packet.Buffer);
                }
            }
            catch (Exception error) { if (!ct.IsCancellationRequested) Fault(error); }
        }
        protected override async Task SendToPeerAsync(byte[] bytes, MCCSubChannelAddress address, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            MCCSubChannelInfo peer = FindSubChannel(address);
            if (peer == null) throw new InvalidOperationException("UDP 逻辑对端已经断开");
            IPEndPoint endpoint = ParseEndpoint(peer.RemoteEndpoint);
            UdpClient udp = _udp;
            if (udp == null) throw new InvalidOperationException("UDP 尚未启动");
            int n = await udp.SendAsync(bytes, bytes.Length, endpoint).ConfigureAwait(false);
            if (n != bytes.Length) throw new IOException("UDP 发送不完整");
        }
        private static IPEndPoint ParseEndpoint(string endpoint)
        {
            int colon = endpoint.LastIndexOf(':');
            if (colon <= 0) throw new FormatException("UDP 对端地址无效");
            string host = endpoint.Substring(0, colon);
            int port = int.Parse(endpoint.Substring(colon + 1));
            if (host.StartsWith("[") && host.EndsWith("]")) host = host.Substring(1, host.Length - 2);
            return new IPEndPoint(IPAddress.Parse(host), port);
        }
        protected override async Task OnCloseAsync(CancellationToken ct)
        {
            if (_shutdown != null) _shutdown.Cancel();
            if (_udp != null) _udp.Close();
            if (_receiveTask != null) await _receiveTask.ConfigureAwait(false);
            lock (_gate) _addresses.Clear();
            foreach (MCCSubChannelInfo peer in SnapshotSubChannels()) RemovePeer(peer.Address);
            if (_shutdown != null) _shutdown.Dispose();
            _shutdown = null; _udp = null; _receiveTask = null;
        }
    }
}
