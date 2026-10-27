using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    public sealed class FTRUdpMulticastChannel : FTRChannelBase
    {
        private readonly FTRUdpMulticastChannelConfiguration _config;
        private readonly object _write = new object();
        private UdpClient _udp;
        private CancellationTokenSource _stop;
        private Thread _reader;
        public FTRUdpMulticastChannel(FTRChannelConfiguration config)
            : base((config ?? throw new ArgumentNullException("config")).Id, FTRChannelTopology.SharedBus)
        { config.Validate(); _config = config as FTRUdpMulticastChannelConfiguration ?? throw new ArgumentException("要求FTRUdpMulticastChannelConfiguration"); }
        protected override bool SupportsEmptySend { get { return true; } }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); var udp = new UdpClient(AddressFamily.InterNetwork);
            try
            {
                udp.ExclusiveAddressUse = false;
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, _config.Port));
                udp.MulticastLoopback = _config.Loopback; udp.Ttl = (short)_config.TimeToLive;
                IPAddress local = IPAddress.Parse(_config.LocalInterface);
                if (!local.Equals(IPAddress.Any))
                    udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
                udp.JoinMulticastGroup(IPAddress.Parse(_config.GroupAddress), local);
            }
            catch { udp.Close(); throw; }
            _udp = udp; _stop = new CancellationTokenSource(); StartBufferedReceive(_config);
            _reader = new Thread(ReceiveLoop) { IsBackground = true, Name = "FTR.Receive." + Id };
            _reader.Start(); return Task.CompletedTask;
        }
        private void ReceiveLoop()
        {
            var token = _stop.Token;
            try
            {
                FTRUdpIO.Receive(_udp.Client, _config, token, RentReceiveBuffer,
                    (buffer, count, endpoint) => PublishReceiveBuffer(new FTRSubChannelAddress(Id), buffer, count, endpoint.ToString()),
                    RecordReceiveError, null, true);
            }
            catch (Exception error) { if (!token.IsCancellationRequested) Fault(error); }
        }
        protected override void OnSendSynchronous(byte[] bytes, int offset, int count,
            FTRSubChannelAddress address, CancellationToken ct, int segmentBytes)
        {
            if (address.HasSubChannel) throw new ArgumentException("组播目标由配置决定，不能使用SubChannel");
            FTRUdpIO.Send(_udp.Client, _write, bytes, offset, count, new IPEndPoint(IPAddress.Parse(_config.GroupAddress), _config.Port),
                segmentBytes, _config.SendTimeoutMs, ct);
        }
        protected override Task OnSendAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        { return Task.Run(() => OnSendSynchronous(bytes, 0, bytes.Length, address, ct, 0), ct); }
        protected override Task OnCloseAsync(CancellationToken ct)
        {
            return Task.Run(() => {
                var timer = Stopwatch.StartNew(); if (_stop != null) _stop.Cancel();
                if (_udp != null)
                {
                    try { _udp.DropMulticastGroup(IPAddress.Parse(_config.GroupAddress)); } catch (SocketException) { }
                    _udp.Close();
                }
                FTRTransportThread.Join(_reader, timer, _config.StopTimeoutMs, ct);
                WaitForPendingSends(FTRTransportThread.Remaining(timer, _config.StopTimeoutMs), ct);
                DrainBufferedReceive(FTRTransportThread.Remaining(timer, _config.StopTimeoutMs), ct);
                if (_stop != null) _stop.Dispose(); _stop = null; _udp = null; _reader = null;
            });
        }
    }
}
