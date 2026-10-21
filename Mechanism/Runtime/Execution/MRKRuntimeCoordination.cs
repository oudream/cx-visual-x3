using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>软件互斥凭证。只在同一Runtime进程内仲裁；绝不能代替硬件联锁。</summary>
    public sealed class MRKResourceLease : IDisposable
    {
        private Action _release;
        internal MRKResourceLease(string resourceId, string ownerId, Action release)
        { ResourceId = resourceId; OwnerId = ownerId; _release = release; }
        public string ResourceId { get; private set; }
        public string OwnerId { get; private set; }
        public void Dispose()
        {
            Action callback = Interlocked.Exchange(ref _release, null);
            if (callback != null) callback();
        }
    }

    /// <summary>
    /// 进程内单点资源租约；同资源只能有一位持有者，等待可协作取消。
    /// Runtime.Host需要是唯一设备仲裁权威；这里不是跨进程锁，也不是硬件安全确认。
    /// </summary>
    public sealed class MRKResourceLeaseCoordinator
    {
        private sealed class Resource
        {
            internal readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1);
            internal string Owner;
        }
        private readonly object _gate = new object();
        private readonly Dictionary<string, Resource> _resources =
            new Dictionary<string, Resource>(StringComparer.Ordinal);

        private Resource GetResource(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("资源ID不能为空", "id");
            lock (_gate)
            {
                Resource resource;
                if (!_resources.TryGetValue(id, out resource))
                {
                    resource = new Resource();
                    _resources.Add(id, resource);
                }
                return resource;
            }
        }
        private static void ValidateOwner(string owner)
        {
            if (string.IsNullOrWhiteSpace(owner))
                throw new ArgumentException("资源持有者不能为空", "owner");
        }
        public async Task<MRKResourceLease> AcquireAsync(
            string resourceId, string owner, CancellationToken cancellation = default(CancellationToken))
        {
            ValidateOwner(owner);
            Resource resource = GetResource(resourceId);
            await resource.Semaphore.WaitAsync(cancellation).ConfigureAwait(false);
            lock (_gate) resource.Owner = owner;
            return new MRKResourceLease(resourceId, owner, () => Release(resource));
        }
        /// <summary>
        /// 多资源取得使用统一字典序，避免不同节点按相反顺序持有部分租约而死锁。
        /// 取消或任一申请失败时逆序释放已持有租约；不能把部分成功交给调用者。
        /// 软件进程内租约不代表设备的真实硬件安全停机。
        /// </summary>
        public async Task<IReadOnlyList<MRKResourceLease>> AcquireManyAsync(
            IEnumerable<string> resourceIds, string owner,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (resourceIds == null) throw new ArgumentNullException("resourceIds");
            ValidateOwner(owner);
            string[] requested = resourceIds.ToArray();
            if (requested.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("资源ID不能为空", "resourceIds");
            string[] ordered = requested.Distinct(StringComparer.Ordinal)
                .OrderBy(x => x, StringComparer.Ordinal).ToArray();
            cancellation.ThrowIfCancellationRequested();
            List<MRKResourceLease> leases = new List<MRKResourceLease>();
            try
            {
                foreach (string resource in ordered)
                    leases.Add(await AcquireAsync(resource, owner, cancellation)
                        .ConfigureAwait(false));
                return leases.AsReadOnly();
            }
            catch
            {
                for (int i = leases.Count - 1; i >= 0; i--)
                    leases[i].Dispose();
                throw;
            }
        }

        public MRKResourceLease TryAcquire(string resourceId, string owner)
        {
            ValidateOwner(owner);
            Resource resource = GetResource(resourceId);
            if (!resource.Semaphore.Wait(0)) return null;
            lock (_gate) resource.Owner = owner;
            return new MRKResourceLease(resourceId, owner, () => Release(resource));
        }
        public string CurrentOwner(string resourceId)
        {
            Resource resource = GetResource(resourceId);
            lock (_gate) return resource.Owner;
        }
        private void Release(Resource resource)
        {
            lock (_gate) resource.Owner = null;
            resource.Semaphore.Release();
        }
        // Registry is Runtime.Host scoped. Entries live until that host is shut down;
        // future dynamic/untrusted resource IDs need an eviction/retirement strategy.
    }

    public enum MRKExternalOperationState { Pending, Unknown, Confirmed, Failed }

    public sealed class MRKExternalOperationObservation
    {
        internal MRKExternalOperationObservation(bool timedOut, MRKExternalOperationState state)
        { TimedOut = timedOut; State = state; }
        /// <summary>观察者等超时不表示设备停止，也不等于操作失败。</summary>
        public bool TimedOut { get; private set; }
        public MRKExternalOperationState State { get; private set; }
    }

    /// <summary>一个外部命令的本地跟踪票据；Provider应明确调用Confirm/Fail。</summary>
    public sealed class MRKExternalOperation
    {
        private readonly object _gate = new object();
        private readonly TaskCompletionSource<MRKExternalOperationState> _completion =
            new TaskCompletionSource<MRKExternalOperationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        private MRKExternalOperationState _state;
        internal MRKExternalOperation(Guid id, string correlationId)
        { Id = id; CorrelationId = correlationId; _state = MRKExternalOperationState.Pending; }
        public Guid Id { get; private set; }
        public string CorrelationId { get; private set; }
        public MRKExternalOperationState State { get { lock (_gate) return _state; } }
        public Task<MRKExternalOperationState> Completion { get { return _completion.Task; } }

        public bool Confirm() { return Finish(MRKExternalOperationState.Confirmed); }
        public bool Fail() { return Finish(MRKExternalOperationState.Failed); }
        private bool Finish(MRKExternalOperationState terminal)
        {
            lock (_gate)
            {
                if (_state == MRKExternalOperationState.Confirmed ||
                    _state == MRKExternalOperationState.Failed) return false;
                _state = terminal;
                _completion.TrySetResult(terminal);
                return true;
            }
        }
        private void MarkUnknown()
        {
            lock (_gate)
                if (_state == MRKExternalOperationState.Pending)
                    _state = MRKExternalOperationState.Unknown;
        }

        public async Task<MRKExternalOperationObservation> ObserveAsync(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException("timeout");
            if (_completion.Task.IsCompleted)
                return new MRKExternalOperationObservation(false,
                    await _completion.Task.ConfigureAwait(false));
            using (CancellationTokenSource timer = new CancellationTokenSource())
            {
                Task wait = Task.Delay(timeout, timer.Token);
                Task first = await Task.WhenAny(_completion.Task, wait).ConfigureAwait(false);
                if (_completion.Task.IsCompleted || first == _completion.Task)
                {
                    timer.Cancel();
                    return new MRKExternalOperationObservation(false,
                        await _completion.Task.ConfigureAwait(false));
                }
                MarkUnknown();
                return new MRKExternalOperationObservation(true, State);
            }
        }
    }

    /// <summary>按OperationId去重建账，不负责网络重试、发包或设备控制。</summary>
    public sealed class MRKExternalOperationTracker
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Guid, MRKExternalOperation> _items =
            new Dictionary<Guid, MRKExternalOperation>();

        public MRKExternalOperation Register(Guid id, string correlationId, out bool created)
        {
            if (id == Guid.Empty) throw new ArgumentException("操作ID不能为空", "id");
            if (string.IsNullOrWhiteSpace(correlationId))
                throw new ArgumentException("关联ID不能为空", "correlationId");
            lock (_gate)
            {
                MRKExternalOperation existing;
                if (_items.TryGetValue(id, out existing))
                {
                    if (!string.Equals(existing.CorrelationId, correlationId,
                        StringComparison.Ordinal))
                        throw new InvalidOperationException("同一操作ID的关联ID发生冲突");
                    created = false;
                    return existing;
                }
                existing = new MRKExternalOperation(id, correlationId);
                _items.Add(id, existing);
                created = true;
                return existing;
            }
        }
        public bool TryGet(Guid id, out MRKExternalOperation operation)
        { lock (_gate) return _items.TryGetValue(id, out operation); }
        // 当前进程内历史随Host保留。持久化、TTL与重启后的幂等由M35/M36及Provider后续实现。
    }

    public enum MRKCorrelationState { Received, TimedOut, Cancelled }

    public sealed class MRKCorrelationResult
    {
        internal MRKCorrelationResult(MRKCorrelationState state, object payload)
        { State = state; Payload = payload; }
        public MRKCorrelationState State { get; private set; }
        public object Payload { get; private set; }
    }

    /// <summary>先Register再发请求，避免快速设备应答抢先到达导致丢失关联。</summary>
    public sealed class MRKCorrelationTicket : IDisposable
    {
        private readonly MRKCorrelationWaiter _owner;
        private readonly TaskCompletionSource<MRKCorrelationResult> _completion =
            new TaskCompletionSource<MRKCorrelationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal MRKCorrelationTicket(MRKCorrelationWaiter owner, Guid id)
        { _owner = owner; RequestId = id; }
        public Guid RequestId { get; private set; }
        public Task<MRKCorrelationResult> Completion { get { return _completion.Task; } }
        internal void Complete(MRKCorrelationState state, object payload)
        { _completion.TrySetResult(new MRKCorrelationResult(state, payload)); }
        public async Task<MRKCorrelationResult> WaitAsync(TimeSpan timeout,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException("timeout");
            if (Completion.IsCompleted) return await Completion.ConfigureAwait(false);
            using (CancellationTokenSource timer = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                Task deadline = Task.Delay(timeout, timer.Token);
                Task winner = await Task.WhenAny(Completion, deadline).ConfigureAwait(false);
                if (Completion.IsCompleted || winner == Completion)
                {
                    timer.Cancel();
                    return await Completion.ConfigureAwait(false);
                }
                _owner.Finish(RequestId, this,
                    cancellation.IsCancellationRequested ? MRKCorrelationState.Cancelled
                        : MRKCorrelationState.TimedOut, null);
                return await Completion.ConfigureAwait(false);
            }
        }
        public void Dispose() { _owner.Finish(RequestId, this, MRKCorrelationState.Cancelled, null); }
    }

    /// <summary>进程内一次性请求-响应关联；协议编解码、权限及跨进程投递不归它负责。</summary>
    public sealed class MRKCorrelationWaiter
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Guid, MRKCorrelationTicket> _tickets =
            new Dictionary<Guid, MRKCorrelationTicket>();
        public MRKCorrelationTicket Register(Guid requestId)
        {
            if (requestId == Guid.Empty) throw new ArgumentException("请求ID不能为空", "requestId");
            lock (_gate)
            {
                if (_tickets.ContainsKey(requestId))
                    throw new InvalidOperationException("重复注册仍在等待的请求ID");
                MRKCorrelationTicket ticket = new MRKCorrelationTicket(this, requestId);
                _tickets.Add(requestId, ticket);
                return ticket;
            }
        }
        public bool Receive(Guid requestId, object payload)
        { return Finish(requestId, null, MRKCorrelationState.Received, payload); }
        // 请求自身超时/Dispose只能终结当前Ticket实例，避免晚到的清理回调
        // 撤销同ID新订阅；Provider实际请求须使用全局唯一RequestId辨别迟到网络应答。
        // expected==null专用于外部Receive；外部无法用同ID辨别不同代的应答。
        internal bool Finish(Guid requestId, MRKCorrelationTicket expected,
            MRKCorrelationState state, object payload)
        {
            MRKCorrelationTicket ticket;
            lock (_gate)
            {
                if (!_tickets.TryGetValue(requestId, out ticket) ||
                    (expected != null && !object.ReferenceEquals(expected, ticket))) return false;
                _tickets.Remove(requestId);
            }
            ticket.Complete(state, payload);
            return true;
        }
        public int PendingCount { get { lock (_gate) return _tickets.Count; } }
    }
}
