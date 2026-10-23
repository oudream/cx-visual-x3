using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    public enum MPAProtocolSessionState { Created, Active, Stopped, Faulted }

    public interface IMPAProtocolSession : IDisposable
    {
        string SessionId { get; }
        string BindingId { get; }
        MPAProtocolSessionState State { get; }
        void Start();
        void Stop();
    }

    // 仅负责规约运行会话的身份和状态。I/O、事务执行在具体 Protocol 内直接实现；
    // 不重复创建 Runtime Scheduler，也不在此实现第二套 Socket 或 Channel Adapter。
    public sealed class MPAProtocolSession : IMPAProtocolSession
    {
        private readonly object _gate = new object();
        private MPAProtocolSessionState _state;
        private bool _disposed;
        private readonly Dictionary<string, TaskCompletionSource<object>> _pending =
            new Dictionary<string, TaskCompletionSource<object>>(StringComparer.Ordinal);
        public MPAProtocolSession(MPAProtocolConfiguration binding, MCCSubChannelAddress channelAddress = null)
        {
            if (binding == null) throw new ArgumentNullException("binding");
            binding.Validate();
            // Connector 唯一绑定 Channel，Session 仅保留其运行地址；配置不保存第二份 ChannelRef。
            BindingId = binding.Id; ChannelAddress = channelAddress;
            SessionId = Guid.NewGuid().ToString("N"); _state = MPAProtocolSessionState.Created;
        }
        public string SessionId { get; private set; }
        public string BindingId { get; private set; }
        public MCCSubChannelAddress ChannelAddress { get; private set; }
        public MPAProtocolSessionState State { get { lock (_gate) return _state; } }
        public void Start()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MPAProtocolSession");
                if (_state != MPAProtocolSessionState.Created && _state != MPAProtocolSessionState.Stopped)
                    throw new InvalidOperationException("规约会话无法从当前状态启动");
                _state = MPAProtocolSessionState.Active;
            }
        }
        public void Stop()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MPAProtocolSession");
                if (_state == MPAProtocolSessionState.Active) _state = MPAProtocolSessionState.Stopped;
                CancelPending("规约会话已停止");
            }
        }
        // 请求/响应关联只保存待完成事务，不接管 MRK/F19 调度或创建工作线程。
        public Task<object> WaitResponseAsync(string correlationId, TimeSpan timeout, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(correlationId)) throw new ArgumentException("CorrelationId 必填");
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException("timeout");
            ct.ThrowIfCancellationRequested();
            TaskCompletionSource<object> pending = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MPAProtocolSession");
                if (_state != MPAProtocolSessionState.Active)
                    throw new InvalidOperationException("规约会话没有运行");
                if (_pending.ContainsKey(correlationId))
                    throw new ArgumentException("重复请求关联 Id：" + correlationId);
                _pending.Add(correlationId, pending);
            }
            return AwaitCompletionAsync(correlationId, pending, timeout, ct);
        }
        private async Task<object> AwaitCompletionAsync(string key,
            TaskCompletionSource<object> completion, TimeSpan timeout, CancellationToken ct)
        {
            using (CancellationTokenSource delayCancel = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                try
                {
                    Task delay = Task.Delay(timeout, delayCancel.Token);
                    Task finished = await Task.WhenAny(completion.Task, delay).ConfigureAwait(false);
                    if (finished == delay)
                    {
                        ct.ThrowIfCancellationRequested();
                        throw new TimeoutException("规约请求响应超时：" + key);
                    }
                    return await completion.Task.ConfigureAwait(false);
                }
                finally
                {
                    lock (_gate)
                    {
                        TaskCompletionSource<object> existing;
                        if (_pending.TryGetValue(key, out existing) && ReferenceEquals(existing, completion))
                            _pending.Remove(key);
                    }
                    delayCancel.Cancel();
                }
            }
        }
        public bool CompleteResponse(string correlationId, object value)
        {
            if (string.IsNullOrWhiteSpace(correlationId)) return false;
            TaskCompletionSource<object> pending;
            lock (_gate)
            {
                if (_state != MPAProtocolSessionState.Active ||
                    !_pending.TryGetValue(correlationId, out pending)) return false;
                _pending.Remove(correlationId);
            }
            return pending.TrySetResult(value is byte[] ? ((byte[])value).Clone() : value);
        }
        // 无效设备响应也必须唤醒原请求，而不能让其一直等到超时。
        public bool FailResponse(string correlationId, Exception error)
        {
            if (string.IsNullOrWhiteSpace(correlationId)) return false;
            if (error == null) throw new ArgumentNullException("error");
            TaskCompletionSource<object> pending;
            lock (_gate)
            {
                if (_state != MPAProtocolSessionState.Active ||
                    !_pending.TryGetValue(correlationId, out pending)) return false;
                _pending.Remove(correlationId);
            }
            return pending.TrySetException(error);
        }
        public int PendingRequests { get { lock (_gate) return _pending.Count; } }
        private void CancelPending(string reason)
        {
            foreach (TaskCompletionSource<object> item in _pending.Values)
                item.TrySetException(new InvalidOperationException(reason));
            _pending.Clear();
        }

        public void Dispose()
        {
            lock (_gate) { _state = MPAProtocolSessionState.Stopped; _disposed = true;
                CancelPending("规约会话已经释放"); }
        }
    }

    public sealed class MPAProtocolContext
    {
        public MPAProtocolContext(string bindingId, string sessionId,
            MCCSubChannelAddress channelAddress = null, string correlationId = null,
            MPAProtocolConfiguration configuration = null, IMPADataTargetAccess dataAccess = null,
            IMCCChannelTransport transport = null, string dataScopeId = null,
            MPAProtocolAddress protocolAddress = null)
        {
            if (string.IsNullOrWhiteSpace(bindingId)) throw new ArgumentException("BindingId 必填");
            BindingId = bindingId.Trim(); SessionId = sessionId;
            ChannelAddress = channelAddress; CorrelationId = correlationId;
            Configuration = configuration; DataAccess = dataAccess; Transport = transport; DataScopeId = dataScopeId;
            ProtocolAddress = protocolAddress;
        }
        public string BindingId { get; private set; }
        public string SessionId { get; private set; }
        public MCCSubChannelAddress ChannelAddress { get; private set; }
        public string CorrelationId { get; private set; }
        public string ChannelId { get { return ChannelAddress == null ? null : ChannelAddress.ChannelId; } }
        public string SubChannelId { get { return ChannelAddress == null ? null : ChannelAddress.SubChannelId; } }
        public MPAProtocolConfiguration Configuration { get; private set; }
        public IMPADataTargetAccess DataAccess { get; private set; }
        public IMCCChannelTransport Transport { get; private set; }
        public string DataScopeId { get; private set; }
        public MPAProtocolAddress ProtocolAddress { get; private set; }
        // Owner 是依赖引用，不是 Protocol 自己维护数据仓库或消息中心。
    }
}
