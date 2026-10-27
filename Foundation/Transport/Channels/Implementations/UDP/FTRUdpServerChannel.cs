using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private sealed class Peer
        {
            internal FTRSubChannelAddress Address;
            internal long LastReceive;
            internal bool Ending;
        }
        private readonly FTRUdpServerChannelConfiguration _config;
        private readonly object _gate = new object(), _write = new object();
        private readonly Dictionary<string, Peer> _addresses = new Dictionary<string, Peer>(StringComparer.Ordinal);
        private UdpClient _udp;
        private CancellationTokenSource _shutdown;
        private Thread _receiver;
        public FTRUdpServerChannel(FTRChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            _config = configuration as FTRUdpServerChannelConfiguration ?? throw new ArgumentException("要求FTRUdpServerChannelConfiguration");
        }
        protected override bool SupportsEmptySend { get { return true; } }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _udp = new UdpClient(new IPEndPoint(FTRNetworkAddress.Resolve(_config.Host), _config.Port));
            _shutdown = new CancellationTokenSource(); StartBufferedReceive(_config);
            _receiver = new Thread(ReceiveLoop) { IsBackground = true, Name = "FTR.Receive." + Id };
            _receiver.Start(); return Task.CompletedTask;
        }
        private void ReceiveLoop()
        {
            CancellationToken token = _shutdown.Token;
            try { FTRUdpIO.Receive(_udp.Client, _config, token, RentReceiveBuffer, DeliverDatagram, RecordReceiveError, SweepIdle, false); }
            catch (Exception error) { if (!token.IsCancellationRequested) Fault(error); }
        }
        private void DeliverDatagram(FTRReceiveBuffer buffer, int count, IPEndPoint source)
        {
            string endpoint = source.ToString(); Peer peer;
            lock (_gate)
            {
                if (!_addresses.TryGetValue(endpoint, out peer) && _addresses.Count < _config.MaxClients)
                {
                    peer = new Peer { Address = RegisterPeer(endpoint, endpoint).Address };
                    _addresses.Add(endpoint, peer);
                }
                if (peer != null && !peer.Ending) peer.LastReceive = Stopwatch.GetTimestamp();
                else peer = null;
            }
            if (peer == null)
            {
                buffer.Dispose(); RecordReceiveError(new IOException("UDP MaxClients reached or source generation is ending; datagram discarded")); return;
            }
            PublishReceiveBuffer(peer.Address, buffer, count, endpoint);
        }
        private void SweepIdle()
        {
            if (_config.ReceiveIdleTimeoutMs == 0) return;
            Peer[] expired;
            lock (_gate)
            {
                long now = Stopwatch.GetTimestamp();
                expired = _addresses.Values.Where(peer => !peer.Ending &&
                    (now - peer.LastReceive) * 1000.0 / Stopwatch.Frequency >= _config.ReceiveIdleTimeoutMs).ToArray();
                foreach (Peer peer in expired) peer.Ending = true;
            }
            foreach (Peer peer in expired)
            {
                RecordReceiveError(new TimeoutException("UDP peer receive idle"));
                AfterReceived(() => RemovePeer(peer.Address));
            }
        }
        public override bool RemovePeer(FTRSubChannelAddress address)
        {
            if (address == null) return false;
            lock (_gate)
            {
                FTRSubChannelInfo info = FindSubChannel(address);
                if (info == null) return false;
                if (!RemovePeerDirectory(address)) return false;
                Peer current;
                if (_addresses.TryGetValue(info.RemoteEndpoint, out current) && current.Address.Equals(address))
                    _addresses.Remove(info.RemoteEndpoint);
            }
            NotifyPeerRemoved(address);
            return true;
        }
        protected override void OnSendSynchronous(byte[] bytes, int offset, int count,
            FTRSubChannelAddress address, CancellationToken ct, int segmentBytes)
        {
            FTRSubChannelInfo peer = FindSubChannel(address);
            if (peer == null) throw new InvalidOperationException("UDP旧代次或不存在的SubChannel");
            FTRUdpIO.Send(_udp.Client, _write, bytes, offset, count, ParseEndpoint(peer.RemoteEndpoint), segmentBytes, _config.SendTimeoutMs, ct);
        }
        protected override Task SendToPeerAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        { return Task.Run(() => OnSendSynchronous(bytes, 0, bytes.Length, address, ct, 0), ct); }
        private static IPEndPoint ParseEndpoint(string endpoint)
        {
            int colon = endpoint.LastIndexOf(':');
            if (colon <= 0) throw new FormatException("UDP对端地址无效");
            return new IPEndPoint(IPAddress.Parse(endpoint.Substring(0, colon).Trim('[', ']')), int.Parse(endpoint.Substring(colon + 1)));
        }
        protected override Task OnCloseAsync(CancellationToken ct)
        {
            return Task.Run(() => {
                var timer = Stopwatch.StartNew(); if (_shutdown != null) _shutdown.Cancel(); if (_udp != null) _udp.Close();
                FTRTransportThread.Join(_receiver, timer, _config.StopTimeoutMs, ct);
                WaitForPendingSends(FTRTransportThread.Remaining(timer, _config.StopTimeoutMs), ct);
                DrainBufferedReceive(FTRTransportThread.Remaining(timer, _config.StopTimeoutMs), ct);
                foreach (FTRSubChannelInfo peer in SnapshotSubChannels()) RemovePeer(peer.Address);
                lock (_gate) _addresses.Clear();
                if (_shutdown != null) _shutdown.Dispose(); _shutdown = null; _udp = null; _receiver = null;
            });
        }
    }
}
