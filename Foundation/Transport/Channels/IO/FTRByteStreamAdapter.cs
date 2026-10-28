using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    /// <summary>
    /// 已有接收管线的逻辑字节流视图；订阅BufferReceived，不建立第二个OS读取者。
    /// 每个适配器仅绑定一个Peer/Generation，容量满时阻塞交付并向FTR接收池传递背压。
    /// 适用于正式提供BufferReceived的ByteStream通道；高级大流消费者可直接使用租约。
    /// </summary>
    public sealed class FTRByteStreamAdapter : IFTRByteStream, IDisposable
    {
        private readonly IFTRChannelTransport _channel;
        private readonly FTRSubChannelAddress _address;
        private readonly BlockingCollection<FTRReceiveBuffer> _pending;
        private readonly SemaphoreSlim _readGate = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private FTRReceiveBuffer _active;
        private int _consumed;
        private int _disposed;
        public FTRByteStreamAdapter(IFTRChannelTransport channel, FTRChannelTypeDescriptor descriptor,
            FTRSubChannelAddress address = null, int queueCapacity = 2)
        {
            if (channel == null) throw new ArgumentNullException("channel");
            if (descriptor == null || (descriptor.IO & FTRIOCapabilities.ByteStream) == 0)
                throw new ArgumentException("只有真实ByteStream能力的通道可建立字节流视图");
            if (queueCapacity < 1 || queueCapacity > 1024) throw new ArgumentOutOfRangeException("queueCapacity");
            _channel = channel;
            _address = address ?? new FTRSubChannelAddress(channel.Id);
            if (_address.ChannelId != channel.Id ||
                (channel.Topology == FTRChannelTopology.MultiPeer && !_address.HasSubChannel) ||
                (channel.Topology != FTRChannelTopology.MultiPeer && _address.HasSubChannel))
                throw new ArgumentException("必须指定对应的Channel与Peer/Generation");
            _pending = new BlockingCollection<FTRReceiveBuffer>(
                new ConcurrentQueue<FTRReceiveBuffer>(), queueCapacity);
            _channel.BufferReceived += Receive;
            _channel.StateChanged += StateChanged;
        }
        private void Receive(FTRReceiveBuffer incoming)
        {
            if (!_address.Equals(incoming.Address) || Volatile.Read(ref _disposed) != 0) return;
            FTRReceiveBuffer lease = incoming.Retain();
            try
            {
                // 背压必须阻止池块继续交付，绝不可静默丢字节或建立无界回调队列。
                _pending.Add(lease, _stop.Token);
                lease = null;
            }
            catch (OperationCanceledException) when (Volatile.Read(ref _disposed) != 0) { }
            catch (InvalidOperationException) when (Volatile.Read(ref _disposed) != 0 || _pending.IsAddingCompleted) { }
            finally { if (lease != null) lease.Dispose(); }
        }
        private void StateChanged(FTRChannelState state)
        {
            if (state == FTRChannelState.Closed || state == FTRChannelState.Faulted)
                _pending.CompleteAdding(); // 适配器绑定一次运行会话；重连需新建视图
        }
        public async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException("offset");
            if (count == 0) return 0;
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stop.Token))
            {
                await _readGate.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    if (_active == null)
                    {
                        // 仅桥接易用API的异步等待；物理接收仍由唯一FTR后台线程执行。
                        try { _active = await Task.Run(() => _pending.Take(linked.Token), linked.Token).ConfigureAwait(false); }
                        catch (InvalidOperationException) when (_pending.IsCompleted) { return 0; }
                        _consumed = 0;
                    }
                    var view = _active.Segment;
                    int n = Math.Min(count, view.Count - _consumed);
                    Buffer.BlockCopy(view.Array, view.Offset + _consumed, buffer, offset, n);
                    _consumed += n;
                    if (_consumed == view.Count)
                    {
                        _active.Dispose();
                        _active = null;
                        _consumed = 0;
                    }
                    return n;
                }
                finally { _readGate.Release(); }
            }
        }
        public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException("offset");
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException("FTRByteStreamAdapter");
            byte[] owned = new byte[count];
            Buffer.BlockCopy(buffer, offset, owned, 0, count);
            return _channel.SendAsync(owned, _address, cancellationToken);
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _channel.BufferReceived -= Receive;
            _channel.StateChanged -= StateChanged;
            _stop.Cancel();
            _pending.CompleteAdding();
            _readGate.Wait();
            try
            {
                if (_active != null) { _active.Dispose(); _active = null; }
                FTRReceiveBuffer queued;
                while (_pending.TryTake(out queued)) queued.Dispose();
            }
            finally { _readGate.Release(); }
            _pending.Dispose();
            _stop.Dispose();
            // Channel本身仍由原Owner关闭及释放；适配器不夺取Socket所有权。
        }
    }
}
