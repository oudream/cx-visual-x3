using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // 简单可验证文件格式：8字节 ASCII "MCCRPL01"，重复 little-endian 4字节长度和原始报文。
    // Replay 是真正的文件读入通道；不会冒充具备外部发送/设备控制能力。
    public sealed class MCCReplayChannel : MCCSingleChannelBase
    {
        private readonly MCCReplayChannelConfiguration _config;
        private FileStream _file;
        private CancellationTokenSource _stop;
        private Task _reader;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("MCCRPL01");

        public MCCReplayChannel(MCCChannelConfiguration config)
            : base((config ?? throw new ArgumentNullException("config")).Id)
        {
            config.Validate();
            _config = config as MCCReplayChannelConfiguration ??
                throw new ArgumentException("必须使用 MCCReplayChannelConfiguration");
        }

        protected override Task OnOpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            FileStream stream = new FileStream(_config.FilePath, FileMode.Open,
                FileAccess.Read, FileShare.Read, 8192, true);
            try
            {
                byte[] signature = new byte[8];
                int count = stream.Read(signature, 0, signature.Length);
                if (count != signature.Length ||
                    !System.Linq.Enumerable.SequenceEqual(signature, Magic))
                    throw new InvalidDataException("回放文件魔数或版本不匹配");
            }
            catch { stream.Dispose(); throw; }
            _file = stream;
            _stop = new CancellationTokenSource();
            _reader = Task.Run(() => ReadLoop(stream, _stop.Token));
            return Task.CompletedTask;
        }

        private async Task ReadLoop(FileStream stream, CancellationToken ct)
        {
            try
            {
                byte[] size = new byte[4];
                while (!ct.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(size, 0, 4, ct).ConfigureAwait(false);
                    if (read == 0) break; // 正常完成回放，由 Host 决定何时关闭/重放
                    while (read < 4)
                    {
                        int n = await stream.ReadAsync(size, read, 4 - read, ct).ConfigureAwait(false);
                        if (n == 0) throw new InvalidDataException("回放记录长度被截断");
                        read += n;
                    }
                    int length = size[0] | (size[1] << 8) |
                        (size[2] << 16) | (size[3] << 24);
                    if (length < 1 || length > _config.MaxRecordBytes)
                        throw new InvalidDataException("回放记录超过约定的字节预算");
                    byte[] payload = new byte[length];
                    for (int offset = 0; offset < length;)
                    {
                        int n = await stream.ReadAsync(payload, offset,
                            length - offset, ct).ConfigureAwait(false);
                        if (n == 0) throw new InvalidDataException("回放记录载荷被截断");
                        offset += n;
                    }
                    Receive(payload);
                    if (_config.RecordIntervalMs > 0)
                        await Task.Delay(_config.RecordIntervalMs, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e) { if (!ct.IsCancellationRequested) Fault(e); }
        }

        protected override Task OnSendAsync(byte[] bytes,
            MCCSubChannelAddress address, CancellationToken ct)
        {
            throw new NotSupportedException("Replay 为只读文件输入通道，禁止模拟外部设备发送成功");
        }

        protected override async Task OnCloseAsync(CancellationToken ct)
        {
            if (_stop != null) _stop.Cancel();
            if (_file != null) _file.Dispose();
            if (_reader != null) await _reader.ConfigureAwait(false);
            if (_stop != null) _stop.Dispose();
            _reader = null; _stop = null; _file = null;
        }
    }
}
