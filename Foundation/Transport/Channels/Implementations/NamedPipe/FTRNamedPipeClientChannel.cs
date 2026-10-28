using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    public sealed class FTRNamedPipeClientChannel : FTRSingleChannelBase
    {
        private readonly FTRNamedPipeClientChannelConfiguration _config;
        private readonly object _write = new object();
        private NamedPipeClientStream _pipe;
        private CancellationTokenSource _stop;
        private Thread _reader;
        public FTRNamedPipeClientChannel(FTRChannelConfiguration config)
            : base((config ?? throw new ArgumentNullException("config")).Id)
        {
            config.Validate(); _config = config as FTRNamedPipeClientChannelConfiguration ??
                throw new ArgumentException("NamedPipeClient需要专用配置");
        }
        protected override async Task OnOpenAsync(CancellationToken ct)
        {
            _pipe = new NamedPipeClientStream(_config.ServerName, _config.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await _pipe.ConnectAsync(_config.ConnectTimeoutMs, ct).ConfigureAwait(false);
            _stop = new CancellationTokenSource(); StartBufferedReceive(_config);
            _reader = new Thread(ReadLoop) { IsBackground = true, Name = "FTR.Receive." + Id };
            _reader.Start();
        }
        private void ReadLoop()
        {
            var ct = _stop.Token; long lastReceive = Stopwatch.GetTimestamp(); EnterReceiveThread();
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int available = FTRPipeIO.Available(_pipe);
                    if (available < 0)
                    {
                        AfterReceived(() => { if (!ct.IsCancellationRequested) Fault(new IOException("NamedPipe peer disconnected")); },ct); return;
                    }
                    if (available == 0)
                    {
                        if (FTRPipeIO.Idle(lastReceive, _config.ReceiveIdleTimeoutMs)) throw new TimeoutException("NamedPipe receive idle");
                        FTRPipeIO.WaitForProgress(ct, lastReceive); continue;
                    }
                    FTRReceiveBuffer buffer = null;
                    try
                    {
                        buffer = RentReceiveBuffer(ct);
                        int count = _pipe.Read(buffer.WritableBuffer, 0, Math.Min(available, buffer.WritableBuffer.Length));
                        if (count == 0) continue;
                        lastReceive = Stopwatch.GetTimestamp();
                        PublishReceiveBuffer(new FTRSubChannelAddress(Id), buffer, count, "pipe:" + _config.PipeName); buffer = null;
                    }
                    finally { if (buffer != null) buffer.Dispose(); }
                }
            }
            catch (Exception error) { if (!ct.IsCancellationRequested) Fault(error); }
        }
        protected override void OnSendSynchronous(byte[] bytes, int offset, int count,
            FTRSubChannelAddress address, CancellationToken ct, int segmentBytes)
        {
            if (address.HasSubChannel) throw new ArgumentException("NamedPipe Client不使用子通道");
            FTRPipeIO.Write(_pipe, _write, bytes, offset, count, segmentBytes, _config.SendTimeoutMs, ct);
        }
        protected override Task OnSendAsync(byte[] bytes, FTRSubChannelAddress target, CancellationToken ct)
        { return Task.Run(() => OnSendSynchronous(bytes, 0, bytes.Length, target, ct, 0), ct); }
        protected override Task OnCloseAsync(CancellationToken ct)
        {
            return Task.Run(() => {
                var timer = Stopwatch.StartNew();
                if (_stop != null) _stop.Cancel(); if (_pipe != null) _pipe.Dispose();
                FTRTransportThread.Join(_reader, timer, _config.StopTimeoutMs, ct);
                WaitForPendingSends(FTRTransportThread.Remaining(timer, _config.StopTimeoutMs), ct);
                DrainBufferedReceive(FTRTransportThread.Remaining(timer, _config.StopTimeoutMs), ct);
                if (_stop != null) _stop.Dispose(); _stop = null; _pipe = null; _reader = null;
            });
        }
    }
}
