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
public sealed class FTRUdpServerChannel : FTRMultiChannelBase
    {
        private readonly FTRUdpServerChannelConfiguration _config;
        private readonly object _gate = new object();
        private readonly Dictionary<string, FTRSubChannelAddress> _addresses =
            new Dictionary<string, FTRSubChannelAddress>(StringComparer.Ordinal);
        private UdpClient _udp;
        private CancellationTokenSource _shutdown;
        private Task _receiveTask;
        public FTRUdpServerChannel(FTRChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            if (configuration.Kind != FTRChannelKind.UdpServer) throw new ArgumentException("不是 UDP Server 配置");
            _config = configuration as FTRUdpServerChannelConfiguration ?? throw new ArgumentException("要求 FTRUdpServerChannelConfiguration");
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _udp = new UdpClient(new IPEndPoint(FTRNetworkAddress.Resolve(_config.Host), _config.Port));
            _shutdown = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_shutdown.Token));
            return Task.CompletedTask;
        }
        public override bool RemovePeer(FTRSubChannelAddress address)
        {
            if (address == null) return false;
            lock (_gate)
            {
                FTRSubChannelInfo peer = FindSubChannel(address);
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
                    FTRSubChannelAddress address;
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
        protected override async Task SendToPeerAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            FTRSubChannelInfo peer = FindSubChannel(address);
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
            foreach (FTRSubChannelInfo peer in SnapshotSubChannels()) RemovePeer(peer.Address);
            if (_shutdown != null) _shutdown.Dispose();
            _shutdown = null; _udp = null; _receiveTask = null;
        }
    }
}
