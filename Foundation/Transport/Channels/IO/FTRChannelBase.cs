using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
public abstract class FTRChannelBase : IFTRChannelTransport
    {
        private readonly object _gate = new object();
        // 通道只保存有限的诊断证据，不占用生产接收队列或协议缓存。
        // 大报文仍完整进入收发及上层事件，超过单条证据上限时仅省略诊断副本。
        private const int MaxEvidenceFrames = 10000;
        private const int MaxEvidencePayloadBytes = 256 * 1024;
        private const int MaxEvidenceBytes = 4 * 1024 * 1024;
        private readonly Queue<FTRChannelFrame> _frames = new Queue<FTRChannelFrame>();
        private int _retainedEvidenceBytes;
        private long _sequence;
        private FTRChannelState _state = FTRChannelState.Closed;
        protected FTRChannelBase(string id, FTRChannelTopology topology)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("通道 Id 不可为空");
            Id = id.Trim(); Topology = topology; SessionId = Guid.NewGuid();
        }
        public string Id { get; private set; }
        public Guid SessionId { get; private set; }
        public FTRChannelTopology Topology { get; private set; }
        public FTRChannelState State { get { lock (_gate) return _state; } }
        public event Action<FTRChannelReceiveContext> Received;
        public event Action<FTRChannelFrame> FrameRecorded;
        public void Open() { OpenAsync(CancellationToken.None).GetAwaiter().GetResult(); }
        public void Close() { CloseAsync(CancellationToken.None).GetAwaiter().GetResult(); }
        public async Task OpenAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_state != FTRChannelState.Closed)
                    throw new InvalidOperationException("通道只能从 Closed 打开");
                // 先改变状态，再启动异步接收，避免高速回环包在 Closed 状态被丢弃。
                _state = FTRChannelState.Opening;
            }
            try
            {
                await OnOpenAsync(ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                lock (_gate)
                {
                    if (_state != FTRChannelState.Opening)
                        throw new IOException("通道打开期间收发任务已发生故障");
                    _state = FTRChannelState.Open;
                }
            }
            catch
            {
                // 打开部分成功时也必须释放监听 Socket、串口与后台任务。
                lock (_gate) _state = FTRChannelState.Faulted;
                try { await OnCloseAsync(CancellationToken.None).ConfigureAwait(false); }
                catch { /* 保留最初的 Open 异常，不覆盖诊断根因 */ }
                throw;
            }
        }
        public async Task CloseAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_state == FTRChannelState.Closed) return;
                if (_state == FTRChannelState.Opening || _state == FTRChannelState.Closing)
                    throw new InvalidOperationException("通道正在生命周期转换中");
                // 允许 Faulted 通道清理与恢复，而非卡死在永久 Faulted。
                _state = FTRChannelState.Closing;
            }
            try
            {
                await OnCloseAsync(ct).ConfigureAwait(false);
                lock (_gate) _state = FTRChannelState.Closed;
            }
            catch
            {
                lock (_gate) _state = FTRChannelState.Faulted;
                throw;
            }
        }
        public void Send(byte[] bytes, string correlationId = null, int segmentBytes = 0)
        {
            if (segmentBytes < 0) throw new ArgumentOutOfRangeException("segmentBytes");
            SendAsync(bytes, new FTRSubChannelAddress(Id), CancellationToken.None).GetAwaiter().GetResult();
        }
        public async Task SendAsync(byte[] bytes, FTRSubChannelAddress address, CancellationToken ct)
        {
            if (bytes == null || bytes.Length == 0) throw new ArgumentException("数据不能为空");
            if (address == null || address.ChannelId != Id) throw new ArgumentException("通道身份不匹配");
            ct.ThrowIfCancellationRequested();
            if (State != FTRChannelState.Open) throw new InvalidOperationException("通道尚未打开");
            byte[] owned = (byte[])bytes.Clone();
            try
            {
                await OnSendAsync(owned, address, ct).ConfigureAwait(false);
                Record(FTRFrameDirection.Tx, owned, null, null);
            }
            catch (Exception e)
            {
                Record(FTRFrameDirection.Error, owned, null, e.Message);
                throw;
            }
        }
        public IList<FTRChannelFrame> ReadFrames()
        {
            lock (_gate) return _frames.ToArray();
        }
        protected void ReceiveBytes(FTRSubChannelAddress address, byte[] bytes, string correlationId = null)
        {
            FTRChannelState current = State;
            if (current != FTRChannelState.Open && current != FTRChannelState.Opening)
                throw new IOException("接收通道未打开");
            if (address == null || address.ChannelId != Id) throw new ArgumentException("接收地址不属于本通道");
            FTRChannelReceiveContext context = new FTRChannelReceiveContext(address, bytes, correlationId);
            Record(FTRFrameDirection.Rx, bytes, correlationId, null);
            Action<FTRChannelReceiveContext> handler = Received;
            if (handler != null) handler(context);
        }
        protected void Fault(Exception error)
        {
            if (error == null) throw new ArgumentNullException("error");
            lock (_gate) _state = FTRChannelState.Faulted;
            Record(FTRFrameDirection.Error, new byte[0], null, error.Message);
        }
        private void Record(FTRFrameDirection direction, byte[] bytes, string correlation, string error)
        {
            byte[] evidence = bytes ?? new byte[0];
            if (evidence.Length > MaxEvidencePayloadBytes)
            {
                // 只限制留存证据，不改变真实网络/串口/虚拟通道的字节数据。
                string omission = "诊断载荷超限，仅保留元数据；原始字节数=" + evidence.Length;
                error = string.IsNullOrEmpty(error) ? omission : error + "；" + omission;
                evidence = new byte[0];
            }
            FTRChannelFrame entry;
            lock (_gate)
            {
                entry = new FTRChannelFrame(++_sequence, SessionId, Id, direction, evidence,
                    correlation, error, null);
                while (_frames.Count > 0 &&
                       (_frames.Count >= MaxEvidenceFrames ||
                        _retainedEvidenceBytes + entry.StoredByteCount > MaxEvidenceBytes))
                {
                    FTRChannelFrame removed = _frames.Dequeue();
                    _retainedEvidenceBytes -= removed.StoredByteCount;
                }
                _frames.Enqueue(entry);
                _retainedEvidenceBytes += entry.StoredByteCount;
            }
            Action<FTRChannelFrame> handler = FrameRecorded;
            if (handler != null) handler(entry);
        }
        protected abstract Task OnOpenAsync(CancellationToken ct);
        protected abstract Task OnCloseAsync(CancellationToken ct);
        protected abstract Task OnSendAsync(byte[] bytes, FTRSubChannelAddress destination, CancellationToken ct);
    }
}
