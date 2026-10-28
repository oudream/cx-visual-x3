using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    public sealed class FTRUdpChannel : FTRSingleChannelBase
    {
        private readonly FTRUdpChannelConfiguration _config;
        private readonly object _write = new object();
        private UdpClient _udp;
        private CancellationTokenSource _shutdown;
        private Thread _receiver;
        public FTRUdpChannel(FTRChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id)
        {
            configuration.Validate();
            _config = configuration as FTRUdpChannelConfiguration ?? throw new ArgumentException("要求FTRUdpChannelConfiguration");
        }
        protected override bool SupportsEmptySend { get { return true; } }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); _udp = new UdpClient(); _udp.Connect(_config.Host, _config.Port);
            _shutdown = new CancellationTokenSource(); StartBufferedReceive(_config);
            _receiver = new Thread(ReceiveLoop) { IsBackground = true, Name = "FTR.Receive." + Id };
            _receiver.Start(); return Task.CompletedTask;
        }
        private void ReceiveLoop()
        {
            CancellationToken token = _shutdown.Token;
            EnterReceiveThread();
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
            if (address.HasSubChannel) throw new ArgumentException("UDP单对端无SubChannel");
            FTRUdpIO.Send(_udp.Client, _write, bytes, offset, count, null, segmentBytes, _config.SendTimeoutMs, ct);
        }
        protected override Task OnSendAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        { return Task.Run(() => OnSendSynchronous(bytes, 0, bytes.Length, address, ct, 0), ct); }
        protected override Task OnCloseAsync(CancellationToken ct)
        {
            return Task.Run(() => {
                var timer = Stopwatch.StartNew();
                if (_shutdown != null) _shutdown.Cancel(); if (_udp != null) _udp.Close();
                FTRTransportThread.Join(_receiver, timer, _config.StopTimeoutMs, ct);
                WaitForPendingSends(FTRTransportThread.Remaining(timer, _config.StopTimeoutMs), ct);
                DrainBufferedReceive(FTRTransportThread.Remaining(timer, _config.StopTimeoutMs), ct);
                if (_shutdown != null) _shutdown.Dispose(); _shutdown = null; _udp = null; _receiver = null;
            });
        }
    }
}
