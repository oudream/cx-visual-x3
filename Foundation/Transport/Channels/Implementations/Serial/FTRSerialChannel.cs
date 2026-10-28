using System;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRSerialChannel : FTRChannelBase
    {
        private readonly FTRSerialChannelConfiguration _config;
        private readonly SemaphoreSlim _writer = new SemaphoreSlim(1, 1);
        private SerialPort _port;
        private CancellationTokenSource _stop;
        private Task _reader;
        public FTRSerialChannel(FTRChannelConfiguration configuration)
            : base((configuration ?? throw new ArgumentNullException("configuration")).Id,
                FTRChannelTopology.SharedBus)
        {
            configuration.Validate();
            if (configuration.Kind != FTRChannelKind.Serial)
                throw new ArgumentException("必须提供 Serial 通道配置");
            _config = configuration as FTRSerialChannelConfiguration ?? throw new ArgumentException("要求 FTRSerialChannelConfiguration");
        }
        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Parity parity = (Parity)Enum.Parse(typeof(Parity), _config.Parity);
            StopBits stopBits = (StopBits)Enum.Parse(typeof(StopBits), _config.StopBits);
            SerialPort port = new SerialPort(_config.PortName, _config.BaudRate, parity,
                _config.DataBits, stopBits) { ReadTimeout = 500, WriteTimeout = 5000 };
            port.Open();
            _port = port;
            ConfigureSendAdmission(_config);
            _stop = new CancellationTokenSource();
            _reader = Task.Run(() => ReadLoop(_stop.Token));
            return Task.CompletedTask;
        }
        private void ReadLoop(CancellationToken ct)
        {
            try
            {
                byte[] buffer = new byte[8192];
                while (!ct.IsCancellationRequested)
                {
                    int n;
                    try { n = _port.Read(buffer, 0, buffer.Length); }
                    catch (TimeoutException) { continue; }
                    if (n < 1) continue;
                    byte[] data = new byte[n];
                    Buffer.BlockCopy(buffer, 0, data, 0, n);
                    ReceiveBytes(new FTRSubChannelAddress(Id), data);
                }
            }
            catch (Exception error)
            {
                if (!ct.IsCancellationRequested) Fault(error);
            }
        }
        protected override async Task OnSendAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        {
            if (address.HasSubChannel) throw new ArgumentException("串口站号属于 Protocol，不是 SubChannel");
            await _writer.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                SerialPort port = _port;
                if (port == null || !port.IsOpen) throw new IOException("Serial 尚未打开");
                await port.BaseStream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
            }
            finally { _writer.Release(); }
        }
        protected override async Task OnCloseAsync(CancellationToken ct)
        {
            if (_stop != null) _stop.Cancel();
            if (_port != null) _port.Close();
            Task reader = _reader;
            if (reader != null) await reader.ConfigureAwait(false);
            if (_stop != null) _stop.Dispose();
            if (_port != null) _port.Dispose();
            _port = null; _stop = null; _reader = null;
        }
    }
}
