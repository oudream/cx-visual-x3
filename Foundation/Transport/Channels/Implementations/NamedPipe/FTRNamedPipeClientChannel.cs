using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRNamedPipeClientChannel : FTRSingleChannelBase
    {
        private readonly FTRNamedPipeClientChannelConfiguration _config;
        private NamedPipeClientStream _pipe;
        private CancellationTokenSource _stop;
        private Task _reader;
        private readonly SemaphoreSlim _write = new SemaphoreSlim(1, 1);
        public FTRNamedPipeClientChannel(FTRChannelConfiguration config)
            : base((config ?? throw new ArgumentNullException("config")).Id)
        {
            config.Validate();
            _config = config as FTRNamedPipeClientChannelConfiguration ??
                throw new ArgumentException("NamedPipeClient 需要专用配置");
        }
        protected override async Task OnOpenAsync(CancellationToken ct)
        {
            NamedPipeClientStream pipe = new NamedPipeClientStream(_config.ServerName,
                _config.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            _pipe = pipe;
            await pipe.ConnectAsync(_config.ConnectTimeoutMs, ct).ConfigureAwait(false);
            _stop = new CancellationTokenSource();
            _reader = Task.Run(() => ReadLoop(pipe, _stop.Token));
        }
        private async Task ReadLoop(NamedPipeClientStream pipe, CancellationToken ct)
        {
            try
            {
                byte[] buffer = new byte[8192];
                while (!ct.IsCancellationRequested)
                {
                    int n = await pipe.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                    if (n == 0) throw new IOException("NamedPipe 对端断开");
                    byte[] received = new byte[n];
                    Buffer.BlockCopy(buffer, 0, received, 0, n);
                    Receive(received);
                }
            }
            catch (Exception e) { if (!ct.IsCancellationRequested) Fault(e); }
        }
        protected override async Task OnSendAsync(byte[] bytes, FTRSubChannelAddress target, CancellationToken ct)
        {
            if (target.HasSubChannel) throw new ArgumentException("NamedPipe Client 不使用子通道");
            await _write.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                NamedPipeClientStream pipe = _pipe;
                if (pipe == null || !pipe.IsConnected) throw new IOException("NamedPipe 未连接");
                await pipe.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                await pipe.FlushAsync(ct).ConfigureAwait(false);
            }
            finally { _write.Release(); }
        }
        protected override async Task OnCloseAsync(CancellationToken ct)
        {
            if (_stop != null) _stop.Cancel();
            if (_pipe != null) _pipe.Dispose();
            if (_reader != null) await _reader.ConfigureAwait(false);
            if (_stop != null) _stop.Dispose();
            _stop = null; _pipe = null; _reader = null;
        }
    }
}
