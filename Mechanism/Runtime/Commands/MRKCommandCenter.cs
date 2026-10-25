using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Data.RuntimeData;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Mechanism.Runtime.Commands
{
    /// <summary>每个Target+CommandCode只能有一个活跃Handler；注销令牌不误删新代次。</summary>
    public sealed class MRKCommandRegistry : IDisposable
    {
        internal sealed class Entry
        {
            internal readonly MRKCommandDescriptor Descriptor;
            internal readonly IMRKCommandHandler Handler;
            internal int Active;
            internal bool Enabled = true;
            internal Entry(MRKCommandDescriptor descriptor, IMRKCommandHandler handler)
            { Descriptor = descriptor; Handler = handler; }
            internal bool TryEnter()
            {
                while (true)
                {
                    int current = Volatile.Read(ref Active);
                    if (current >= Descriptor.Policy.MaxConcurrent) return false;
                    if (Interlocked.CompareExchange(ref Active, current + 1, current) == current) return true;
                }
            }
            internal void Exit() { Interlocked.Decrement(ref Active); }
        }
        private sealed class Registration : IDisposable
        {
            private MRKCommandRegistry _registry;
            private readonly Entry _entry;
            internal Registration(MRKCommandRegistry registry, Entry entry)
            { _registry = registry; _entry = entry; }
            public void Dispose()
            {
                MRKCommandRegistry owner = Interlocked.Exchange(ref _registry, null);
                if (owner != null) owner.Unregister(_entry);
            }
        }
        private readonly object _gate = new object();
        private readonly Dictionary<string, Entry> _entries =
            new Dictionary<string, Entry>(StringComparer.Ordinal);
        private bool _disposed;
        private static string Key(string target, string code) { return target + "\n" + code; }
        public IDisposable Register(MRKCommandDescriptor descriptor, IMRKCommandHandler handler)
        {
            if (descriptor == null || handler == null) throw new ArgumentNullException("descriptor/handler");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MRKCommandRegistry");
                string key = Key(descriptor.Target, descriptor.Definition.CommandCode);
                if (_entries.ContainsKey(key)) throw new InvalidOperationException("命令处理器重复注册：" + key);
                if (_entries.Values.Any(e => e.Descriptor.Target == descriptor.Target &&
                    e.Descriptor.Definition.CommandId == descriptor.Definition.CommandId))
                    throw new InvalidOperationException("同一目标的命令ID必须唯一：" +
                        descriptor.Definition.CommandId);
                Entry item = new Entry(descriptor, handler); _entries.Add(key, item);
                return new Registration(this, item);
            }
        }
        /// <summary>热更新代次：旧受理调用继续使用旧Handler，旧注册令牌不得注销新代次。</summary>
        public IDisposable Replace(MRKCommandDescriptor descriptor, IMRKCommandHandler handler)
        {
            if (descriptor == null) throw new ArgumentNullException("descriptor");
            if (handler == null) throw new ArgumentNullException("handler");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MRKCommandRegistry");
                string key = Key(descriptor.Target, descriptor.Definition.CommandCode);
                Entry previous;
                if (!_entries.TryGetValue(key, out previous))
                    throw new KeyNotFoundException("待更新命令不存在：" + key);
                if (_entries.Values.Any(e => !object.ReferenceEquals(e, previous) &&
                    e.Descriptor.Target == descriptor.Target &&
                    e.Descriptor.Definition.CommandId == descriptor.Definition.CommandId))
                    throw new InvalidOperationException("更新后命令ID与现有目标命令冲突");
                Entry updated = new Entry(descriptor, handler);
                previous.Enabled = false;
                _entries[key] = updated;
                return new Registration(this, updated);
            }
        }
        internal Entry Resolve(string target, string code)
        {
            lock (_gate)
            {
                Entry e;
                if (_disposed || !_entries.TryGetValue(Key(target, code), out e) || !e.Enabled) return null;
                return e;
            }
        }
        public MRKCommandDescriptor Find(string target, string code)
        { Entry e = Resolve(target, code); return e == null ? null : e.Descriptor; }
        internal Entry ResolveById(string target, long commandId)
        {
            if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("目标不能为空", "target");
            if (commandId <= 0) throw new ArgumentOutOfRangeException("commandId");
            lock (_gate)
            {
                if (_disposed) return null;
                return _entries.Values.FirstOrDefault(e => e.Enabled &&
                    e.Descriptor.Target == target && e.Descriptor.Definition.CommandId == commandId);
            }
        }
        public MRKCommandDescriptor FindById(string target, long commandId)
        {
            Entry found = ResolveById(target, commandId);
            return found == null ? null : found.Descriptor;
        }
        public IReadOnlyList<MRKCommandDescriptor> Discover(string target = null,
            MRKCommandKind? kind = null, string capability = null,
            string name = null, string commandCode = null,
            long? commandId = null, int? schemaVersion = null)
        {
            lock (_gate) return Array.AsReadOnly(_entries.Values.Where(e => e.Enabled &&
                (target == null || e.Descriptor.Target == target) &&
                (!kind.HasValue || e.Descriptor.Kind == kind.Value) &&
                (capability == null || e.Descriptor.Capabilities.Contains(capability)) &&
                (name == null || e.Descriptor.Definition.Name == name) &&
                (commandCode == null || e.Descriptor.Definition.CommandCode == commandCode) &&
                (!commandId.HasValue || e.Descriptor.Definition.CommandId == commandId.Value) &&
                (!schemaVersion.HasValue || e.Descriptor.Definition.SchemaVersion == schemaVersion.Value))
                .Select(e => e.Descriptor).OrderBy(d => d.Target, StringComparer.Ordinal)
                .ThenBy(d => d.Definition.CommandCode, StringComparer.Ordinal).ToArray());
        }
        public bool SetEnabled(string target, string code, bool enabled)
        {
            lock (_gate)
            {
                Entry item;
                if (_disposed || !_entries.TryGetValue(Key(target, code), out item)) return false;
                item.Enabled = enabled; return true;
            }
        }
        private void Unregister(Entry entry)
        {
            lock (_gate)
            {
                string key = Key(entry.Descriptor.Target, entry.Descriptor.Definition.CommandCode);
                Entry current;
                if (_entries.TryGetValue(key, out current) && object.ReferenceEquals(entry, current))
                { entry.Enabled = false; _entries.Remove(key); }
            }
        }
        public void Dispose()
        { lock (_gate) { _disposed = true; foreach (Entry e in _entries.Values) e.Enabled = false; _entries.Clear(); } }
    }

    public sealed class MRKCommandSubmission
    {
        internal MRKCommandSubmission(MRKCommandResponse response, MRKCommandExecution operation)
        { Response = response; Operation = operation; }
        public MRKCommandResponse Response { get; private set; }
        public MRKCommandExecution Operation { get; private set; }
    }

    /// <summary>Handler目标路由。真正跨进程必须由独立认证Transport承担，本类不会静默回退到本地。</summary>
    public sealed class MRKCommandRouter
    {
        private readonly MRKCommandRegistry _registry;
        public MRKCommandRouter(MRKCommandRegistry registry)
        { _registry = registry ?? throw new ArgumentNullException("registry"); }
        public MRKCommandDescriptor Resolve(string target, string code)
        { return _registry.Find(target, code); }
        public MRKCommandDescriptor ResolveById(string target, long commandId)
        { return _registry.FindById(target, commandId); }
        public IReadOnlyList<MRKCommandDescriptor> Discover(string target = null,
            MRKCommandKind? kind = null, string capability = null,
            string name = null, string commandCode = null,
            long? commandId = null, int? schemaVersion = null)
        { return _registry.Discover(target, kind, capability, name, commandCode, commandId, schemaVersion); }
    }

    /// <summary>
    /// MRK命令入口：Foundation.Data.Commands为唯一静态定义、MRK Tracker/Lease管理副作用及资源，
    /// 本地有界受理、不建第二流程执行引擎、不通过MessageBus隐式执行设备命令。
    /// </summary>
    public sealed class MRKCommandCenter : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Guid, MRKCommandExecution> _operations =
            new Dictionary<Guid, MRKCommandExecution>();
        private readonly MRKCommandRegistry _registry = new MRKCommandRegistry();
        private readonly MRKExternalOperationTracker _tracker;
        private readonly MRKResourceLeaseCoordinator _leases;
        private readonly MRKCommandExecutor _executor;
        private readonly MRKCommandDispatcher _dispatcher;
        private readonly bool _ownsDispatcher;
        private readonly Func<MRKCommandContext, bool> _authorize;
        private readonly FDTDataAdapterRegistry _adapters;
        private readonly List<IMRKCommandInterceptor> _interceptors =
            new List<IMRKCommandInterceptor>();
        private readonly int _maxOperations;
        private readonly int _hostTimeoutMs;
        private bool _disposed;
        public MRKCommandCenter(MRKExternalOperationTracker tracker = null,
            MRKResourceLeaseCoordinator leases = null,
            Func<MRKCommandContext, bool> authorize = null,
            FDTDataAdapterRegistry adapters = null,
            int maxOperations = 4096, int defaultTimeoutMilliseconds = 30000,
            MRKCommandDispatcher dispatcher = null,
            int queuedConcurrency = 4, int queueCapacity = 64)
        {
            if (maxOperations < 1 || defaultTimeoutMilliseconds < 1)
                throw new ArgumentOutOfRangeException("maxOperations/defaultTimeoutMilliseconds");
            _tracker = tracker ?? new MRKExternalOperationTracker();
            _leases = leases ?? new MRKResourceLeaseCoordinator();
            _authorize = authorize; _adapters = adapters;
            _maxOperations = maxOperations; _hostTimeoutMs = defaultTimeoutMilliseconds;
            Router = new MRKCommandRouter(_registry);
            _dispatcher = dispatcher ?? new MRKCommandDispatcher(queuedConcurrency, queueCapacity);
            _ownsDispatcher = dispatcher == null;
            _executor = new MRKCommandExecutor(_tracker, _leases, _adapters,
                () => { lock (_gate) return _interceptors.ToArray(); });
        }
        public MRKCommandRegistry Registry { get { return _registry; } }
        public MRKCommandRouter Router { get; private set; }
        public FCRWorkerSnapshot GetQueueSnapshot() { return _dispatcher.Snapshot(); }
        public Task StopQueueAsync(bool cancelPending = false)
        { return _dispatcher.StopAsync(cancelPending); }
        public IDisposable Register(MRKCommandDescriptor descriptor, IMRKCommandHandler handler)
        { return _registry.Register(descriptor, handler); }
        public IDisposable Replace(MRKCommandDescriptor descriptor, IMRKCommandHandler handler)
        { return _registry.Replace(descriptor, handler); }
        /// <summary>统计来自唯一运行操作账本；不额外保存业务结果或日志。</summary>
        public MRKCommandMetrics GetMetrics()
        {
            lock (_gate)
            {
                MRKCommandMetrics metrics = new MRKCommandMetrics
                {
                    Registered = _registry.Discover().Count,
                    TotalOperations = _operations.Count
                };
                foreach (MRKCommandExecution operation in _operations.Values)
                {
                    switch (operation.Snapshot().State)
                    {
                        case MRKCommandState.Pending: ++metrics.Pending; break;
                        case MRKCommandState.Queued: ++metrics.Queued; break;
                        case MRKCommandState.PauseRequested: ++metrics.PauseRequested; break;
                        case MRKCommandState.Paused: ++metrics.Paused; break;
                        case MRKCommandState.Running: ++metrics.Running; break;
                        case MRKCommandState.Completed: ++metrics.Completed; break;
                        case MRKCommandState.Failed: ++metrics.Failed; break;
                        case MRKCommandState.Cancelled: ++metrics.Cancelled; break;
                        default: ++metrics.Unknown; break;
                    }
                }
                return metrics;
            }
        }
        public void AddInterceptor(IMRKCommandInterceptor interceptor)
        {
            if (interceptor == null) throw new ArgumentNullException("interceptor");
            lock (_gate) { if (_disposed) throw new ObjectDisposedException("MRKCommandCenter"); _interceptors.Add(interceptor); }
        }

        private static MRKCommandResponse Reject(MRKCommandRequest request, string code, string detail)
        { return new MRKCommandResponse(request.RequestId, request.OperationId,
            MRKCommandStage.Rejected, MRKCommandState.Failed,
            errorCode: code, error: detail); }

        public MRKCommandSubmission Submit(MRKCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            lock (_gate) if (_disposed) throw new ObjectDisposedException("MRKCommandCenter");
            if (request.Options.Remote)
                return new MRKCommandSubmission(Reject(request, "RemoteUnsupported",
                    "跨进程/跨PC transport尚未接入，拒绝伪装为本地成功"), null);
            MRKCommandRegistry.Entry entry = string.IsNullOrWhiteSpace(request.CommandCode)
                ? _registry.ResolveById(request.Target, request.CommandId)
                : _registry.Resolve(request.Target, request.CommandCode);
            if (entry == null)
                return new MRKCommandSubmission(Reject(request, "HandlerNotFound", "处理器未注册或已禁用"), null);
            if (request.CommandId > 0 &&
                entry.Descriptor.Definition.CommandId != request.CommandId)
                return new MRKCommandSubmission(Reject(request, "CommandIdentityMismatch",
                    "CommandId与命令Code解析出的正式命令不一致"), null);
            if (request.ExpectedSchemaVersion > 0 &&
                request.ExpectedSchemaVersion != entry.Descriptor.Definition.SchemaVersion)
                return new MRKCommandSubmission(Reject(request, "SchemaVersionMismatch",
                    "请求版本与目标已发布命令Schema不一致"), null);
            if (entry.Descriptor.Kind == MRKCommandKind.Device &&
                entry.Descriptor.Policy.RetryPolicy.MaxAttempts > 1)
                return new MRKCommandSubmission(Reject(request, "DeviceAutoRetryForbidden",
                    "设备副作用命令禁止本地自动重试；必须通过专业Provider重新确认状态"), null);
            IReadOnlyDictionary<string, object> inputs;
            try
            {
                inputs = FDCCommandValueSnapshot.Capture(
                    entry.Descriptor.Definition.BindInputs(request.Inputs), _adapters);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            { return new MRKCommandSubmission(Reject(request, "InvalidInput", ex.Message), null); }
            // 设备命令和有权限键的命令必须由接收Host明确授权；Caller字符串不可代替认证。
            MRKCommandContext ctx = new MRKCommandContext(request, entry.Descriptor, (p, m) => { });
            if ((entry.Descriptor.Kind == MRKCommandKind.Device ||
                 !string.IsNullOrWhiteSpace(entry.Descriptor.Definition.PermissionKey)) &&
                _authorize == null)
                return new MRKCommandSubmission(Reject(request, "Unauthorized", "没有目标侧授权器"), null);
            try
            {
                if (_authorize != null && !_authorize(ctx))
                    return new MRKCommandSubmission(Reject(request, "Unauthorized", "目标侧拒绝授权"), null);
            }
            catch (Exception ex)
            { return new MRKCommandSubmission(Reject(request, "AuthorizationError", ex.Message), null); }
            MRKCommandExecution operation;
            FCRWorkHandle<bool> queuedHandle = null;
            TaskCompletionSource<bool> queueReady = null;
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MRKCommandCenter");
                if (_operations.TryGetValue(request.OperationId, out operation))
                {
                    if (operation.Request.Caller != request.Caller ||
                        operation.Request.Target != request.Target ||
                        operation.Request.CommandCode != request.CommandCode ||
                        operation.Request.CommandId != request.CommandId ||
                        !EqualInputs(operation.Inputs, inputs))
                        return new MRKCommandSubmission(Reject(request, "OperationConflict",
                            "OperationId已经绑定不同调用身份、目标或参数"), null);
                    return new MRKCommandSubmission(operation.Snapshot().Copy(_adapters, request.RequestId), operation);
                }
                if (_operations.Count >= _maxOperations)
                    return new MRKCommandSubmission(Reject(request, "HistoryFull",
                        "进程内操作记录容量已满，请完成受控归档后重建Host"), null);
                bool queued = entry.Descriptor.Policy.Dispatch == MRKCommandDispatch.Queued;
                if (!queued && !entry.TryEnter())
                    return new MRKCommandSubmission(Reject(request, "Busy", "命令并发容量已满"), null);
                operation = new MRKCommandExecution(request, entry.Descriptor, inputs,
                    request.Target + "\n" + entry.Descriptor.Definition.CommandCode, _adapters);
                _operations.Add(request.OperationId, operation);
                if (queued)
                {
                    operation.Queued();
                    // 队列作业在MRK Tracker完成注册前不允许调用Handler。
                    queueReady = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    Task ready = queueReady.Task;
                    try
                    {
                        queuedHandle = _dispatcher.Enqueue(async token =>
                        {
                            await ready.ConfigureAwait(false);
                            token.ThrowIfCancellationRequested();
                            if (!entry.TryEnter())
                                throw new InvalidOperationException("队列串行槽异常：命令仍在执行");
                            await _executor.ExecuteAsync(operation, entry).ConfigureAwait(false);
                            return true;
                        }, operation.Key, request.Options.Priority, operation.Cancellation.Token);
                    }
                    catch (Exception ex)
                    {
                        _operations.Remove(request.OperationId);
                        queueReady.TrySetCanceled();
                        return new MRKCommandSubmission(Reject(request, "DispatchFailure", ex.Message), null);
                    }
                    if (!queuedHandle.Accepted)
                    {
                        _operations.Remove(request.OperationId);
                        queueReady.TrySetCanceled();
                        return new MRKCommandSubmission(Reject(request, "QueueFull",
                            "F19命令队列已停止或达到有界容量"), null);
                    }
                }
                bool created;
                try
                {
                    _tracker.Register(request.OperationId, request.RequestId.ToString("N"), out created);
                    if (!created) throw new InvalidOperationException("MRK Tracker与命令账本不一致");
                }
                catch
                {
                    _operations.Remove(request.OperationId);
                    if (queued)
                    {
                        operation.Cancellation.Cancel();
                        queueReady.TrySetCanceled();
                    }
                    else entry.Exit();
                    throw;
                }
            }
            if (queuedHandle != null)
            {
                queueReady.TrySetResult(true);
                Task observer = ObserveQueuedAsync(operation, queuedHandle);
            }
            else if (entry.Descriptor.Policy.Dispatch == MRKCommandDispatch.ThreadPool)
                Task.Run(() => _executor.ExecuteAsync(operation, entry));
            else
            {
                Task pending = _executor.ExecuteAsync(operation, entry);
            }
            return new MRKCommandSubmission(operation.Snapshot(), operation);
        }

        public async Task<MRKCommandResponse> InvokeAsync(MRKCommandRequest request,
            CancellationToken cancellation = default(CancellationToken))
        {
            MRKCommandSubmission submitted = Submit(request);
            if (submitted.Operation == null) return submitted.Response;
            MRKCommandExecution operation = submitted.Operation;
            if (operation.IsCompleted) return (await operation.Completion.ConfigureAwait(false)).Copy(_adapters, request.RequestId);
            int timeout = request.Options.TimeoutMilliseconds != 0 ? request.Options.TimeoutMilliseconds :
                (operation.Descriptor.Definition.TimeoutMilliseconds != 0 ?
                    operation.Descriptor.Definition.TimeoutMilliseconds : _hostTimeoutMs);
            using (CancellationTokenSource wait = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                Task delay = Task.Delay(timeout, wait.Token);
                Task winner = await Task.WhenAny(operation.Completion, delay).ConfigureAwait(false);
                if (winner == operation.Completion || operation.IsCompleted)
                { wait.Cancel(); return (await operation.Completion.ConfigureAwait(false)).Copy(_adapters, request.RequestId); }
                if (operation.Descriptor.Policy.SupportsCancellation) operation.Cancellation.Cancel();
                return new MRKCommandResponse(request.RequestId, request.OperationId,
                    MRKCommandStage.Accepted, MRKCommandState.Unknown,
                    errorCode: cancellation.IsCancellationRequested ? "CallerCancelled" : "TimeoutUnknown",
                    error: "等待已终止，外部副作用是否结束尚不能确认；使用OperationId查询");
            }
        }

        public MRKCommandResponse Query(Guid operationId)
        {
            lock (_gate)
            {
                MRKCommandExecution execution;
                return _operations.TryGetValue(operationId, out execution) ? execution.Snapshot().Copy(_adapters) : null;
            }
        }
        /// <summary>仅请求协作暂停；状态PauseRequested不是设备安全停止。Paused表示Handler确实到达检查点。</summary>
        public bool Pause(Guid operationId)
        {
            MRKCommandExecution operation;
            lock (_gate) if (!_operations.TryGetValue(operationId, out operation)) return false;
            return operation.TryPause();
        }
        public bool Resume(Guid operationId)
        {
            MRKCommandExecution operation;
            lock (_gate) if (!_operations.TryGetValue(operationId, out operation)) return false;
            return operation.TryResume();
        }

        public bool Cancel(Guid operationId)
        {
            MRKCommandExecution operation;
            lock (_gate)
            {
                if (!_operations.TryGetValue(operationId, out operation) || operation.IsCompleted ||
                    !operation.Descriptor.Policy.SupportsCancellation) return false;
            }
            // 不在中心锁内运行外部Cancellation回调；取消是请求，不是物理安全停机。
            operation.Cancellation.Cancel();
            return true;
        }

        public async Task<IReadOnlyList<MRKCommandResponse>> InvokeBatchAsync(
            IEnumerable<MRKCommandRequest> requests, CancellationToken cancellation = default(CancellationToken))
        {
            if (requests == null) throw new ArgumentNullException("requests");
            MRKCommandRequest[] items = requests.ToArray();
            if (items.Length > 256) throw new ArgumentException("批量命令最多256条");
            return Array.AsReadOnly(await Task.WhenAll(items.Select(x => InvokeAsync(x, cancellation)))
                .ConfigureAwait(false));
        }

        /// <summary>同步调用只适用于无UI线程等待的SDK调用者；后台/Studio应优先使用InvokeAsync。</summary>
        public MRKCommandResponse Invoke(MRKCommandRequest request,
            CancellationToken cancellation = default(CancellationToken))
        { return InvokeAsync(request, cancellation).GetAwaiter().GetResult(); }

        /// <summary>
        /// 根据当前启用的正式Registry枚举目标进行显式广播。
        /// 默认禁止设备广播，避免把单点运动命令无意变成群控；所有目标独立OperationId与结果。
        /// </summary>
        public Task<IReadOnlyList<MRKCommandResponse>> InvokeBroadcastAsync(
            string commandCode, IReadOnlyDictionary<string, object> inputs = null,
            MRKCommandKind? kind = null, string capability = null,
            string caller = null, bool allowDeviceBroadcast = false,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(commandCode))
                throw new ArgumentException("命令编码不能为空", "commandCode");
            MRKCommandDescriptor[] descriptors = Router.Discover(kind: kind,
                capability: capability, commandCode: commandCode).ToArray();
            if (descriptors.Length > 256)
                throw new ArgumentException("广播命令目标超过256，需分批处理");
            if (!allowDeviceBroadcast && descriptors.Any(d => d.Kind == MRKCommandKind.Device))
                throw new UnauthorizedAccessException("设备广播必须显式启用并经过逐目标授权");
            return InvokeBatchAsync(descriptors.Select(d => new MRKCommandRequest(
                d.Target, commandCode, inputs, caller)), cancellation);
        }

        /// <summary>由现有MDS/变量/端口Owner提供读写，不复制Heap或变量表；只在成功执行后映射输出。</summary>
        /// <summary>兼容旧调用；若执行已经完成但输出写回失败，异常携带独立执行回执。</summary>
        public async Task<MRKCommandResponse> InvokeBoundAsync(MRKCommandRequest request,
            IFDCCommandValueBinding owner,
            IEnumerable<MRKCommandParameterBinding> inputBindings,
            IEnumerable<MRKCommandParameterBinding> outputBindings,
            CancellationToken cancellation = default(CancellationToken))
        {
            MRKCommandBindingResult result = await InvokeBoundResultAsync(request, owner,
                inputBindings, outputBindings, cancellation).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result.MappingErrorCode))
                throw new MRKCommandOutputMappingException(result);
            return result.Execution;
        }

        /// <summary>
        /// 结果分层：Execution已完成≠数据Owner写回成功；任何局部写入均显式回报地址。
        /// 输出映射无法跨多个外部Owner提供事务回滚，严禁无条件自动重试设备动作。
        /// </summary>
        public async Task<MRKCommandBindingResult> InvokeBoundResultAsync(MRKCommandRequest request,
            IFDCCommandValueBinding owner,
            IEnumerable<MRKCommandParameterBinding> inputBindings,
            IEnumerable<MRKCommandParameterBinding> outputBindings,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (request == null) throw new ArgumentNullException("request");
            MRKCommandParameterBinding[] inputs = (inputBindings ??
                Enumerable.Empty<MRKCommandParameterBinding>()).ToArray();
            MRKCommandParameterBinding[] outputs = (outputBindings ??
                Enumerable.Empty<MRKCommandParameterBinding>()).ToArray();
            if (owner == null && (inputs.Length > 0 || outputs.Length > 0))
                throw new ArgumentNullException("owner", "存在数据地址映射时必须提供正式数据Owner");
            if (outputs.Any(b => b == null) ||
                outputs.GroupBy(b => b.Parameter, StringComparer.Ordinal).Any(g => g.Count() > 1) ||
                outputs.GroupBy(b => b.DataAddress, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new ArgumentException("输出参数或目标地址重复/无效");
            MRKCommandDescriptor expected = string.IsNullOrWhiteSpace(request.CommandCode)
                ? Router.ResolveById(request.Target, request.CommandId)
                : Router.Resolve(request.Target, request.CommandCode);
            if (expected != null)
                foreach (MRKCommandParameterBinding binding in outputs)
                    if (!expected.Definition.Outputs.Any(p => p.Code == binding.Parameter))
                        throw new ArgumentException("未声明的命令输出：" + binding.Parameter);
            Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
            if (request.Inputs != null)
                foreach (KeyValuePair<string, object> pair in request.Inputs) values.Add(pair.Key, pair.Value);
            foreach (MRKCommandParameterBinding binding in inputs)
            {
                if (binding == null) throw new ArgumentException("输入绑定不能为空");
                if (values.ContainsKey(binding.Parameter))
                    throw new ArgumentException("输入参数绑定重复：" + binding.Parameter);
                values.Add(binding.Parameter,
                    await owner.ReadAsync(binding.DataAddress, cancellation).ConfigureAwait(false));
            }
            MRKCommandRequest actual = new MRKCommandRequest(request.Target, request.CommandCode,
                values, request.Caller, request.OperationId, request.RequestId, request.Options,
                request.ExpectedSchemaVersion, request.CommandId);
            MRKCommandResponse response = await InvokeAsync(actual, cancellation).ConfigureAwait(false);
            if (response.State != MRKCommandState.Completed)
                return new MRKCommandBindingResult(response, false, false, null);
            if (outputs.Length == 0)
                return new MRKCommandBindingResult(response, false, true, null);

            IReadOnlyDictionary<string, object> final = response.Outputs;
            List<object> readyValues = new List<object>(outputs.Length);
            foreach (MRKCommandParameterBinding binding in outputs)
            {
                object value;
                if (!final.TryGetValue(binding.Parameter, out value))
                    return new MRKCommandBindingResult(response, true, false, null,
                        "OutputMissing", "命令已执行，但结果未包含映射端口：" + binding.Parameter);
                readyValues.Add(value);
            }
            List<string> mapped = new List<string>();
            for (int i = 0; i < outputs.Length; i++)
            {
                try
                {
                    await owner.WriteAsync(outputs[i].DataAddress, readyValues[i], cancellation)
                        .ConfigureAwait(false);
                    mapped.Add(outputs[i].DataAddress);
                }
                catch (Exception ex)
                {
                    return new MRKCommandBindingResult(response, true, false, mapped,
                        ex is OperationCanceledException ? "OutputMappingCancelled" : "OutputMappingFailed",
                        "命令已执行，输出地址写回失败：" + outputs[i].DataAddress + ": " + ex.Message);
                }
            }
            return new MRKCommandBindingResult(response, true, true, mapped);
        }

        /// <summary>
        /// 已有 MDS 绑定描述直接映射到命令参数：Constant/CurrentSlot/NodeOutput/
        /// FlowHeap/Variable/EngineeringResource/Expression；表达式由已有Resolver解释，
        /// 不执行字符串代码，也不创建新的参数仓库。无输出映射时outputOwner可为null。
        /// </summary>
        public async Task<MRKCommandBindingResult> InvokeConfiguredResultAsync(
            MRKCommandRequest request, IMDSDataContext data,
            IEnumerable<MRKCommandInputSource> inputSources,
            IFDCCommandValueBinding outputOwner = null,
            IEnumerable<MRKCommandParameterBinding> outputBindings = null,
            IMDSExternalBindingResolver external = null,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (request == null) throw new ArgumentNullException("request");
            if (data == null) throw new ArgumentNullException("data");
            cancellation.ThrowIfCancellationRequested();
            // 先查正式Command定义/版本；无效目标或远程请求不得触发表达式解析。
            if (request.Options.Remote)
                return new MRKCommandBindingResult(Reject(request, "RemoteUnsupported",
                    "远程Transport尚未接入，不允许本地代执行"), false, false, null);
            MRKCommandDescriptor descriptor = string.IsNullOrWhiteSpace(request.CommandCode)
                ? Router.ResolveById(request.Target, request.CommandId)
                : Router.Resolve(request.Target, request.CommandCode);
            if (descriptor == null)
                return new MRKCommandBindingResult(Reject(request, "HandlerNotFound",
                    "命令未注册或已禁用"), false, false, null);
            if (request.CommandId > 0 && request.CommandId != descriptor.Definition.CommandId)
                return new MRKCommandBindingResult(Reject(request, "CommandIdentityMismatch",
                    "CommandId与命令Code不一致"), false, false, null);
            if (request.ExpectedSchemaVersion > 0 &&
                request.ExpectedSchemaVersion != descriptor.Definition.SchemaVersion)
                return new MRKCommandBindingResult(Reject(request, "SchemaVersionMismatch",
                    "请求版本与命令Schema不一致"), false, false, null);
            IReadOnlyDictionary<string, object> direct =
                FDCCommandValueSnapshot.Capture(request.Inputs, _adapters);
            Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> item in direct) values.Add(item.Key, item.Value);
            // 先完整预检所有端口，再调用任何外部变量/表达式解析器。
            MRKCommandInputSource[] sources = (inputSources ??
                Enumerable.Empty<MRKCommandInputSource>()).ToArray();
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (MRKCommandInputSource source in sources)
            {
                cancellation.ThrowIfCancellationRequested();
                if (source == null) throw new ArgumentException("输入绑定不得为空", "inputSources");
                if (!names.Add(source.Parameter) || values.ContainsKey(source.Parameter))
                    throw new ArgumentException("输入端口重复绑定：" + source.Parameter, "inputSources");
                FDCCommandParameterDefinition parameter = descriptor.Definition.Inputs
                    .FirstOrDefault(p => p.Code == source.Parameter);
                if (parameter == null)
                    throw new ArgumentException("未声明的命令输入端口：" + source.Parameter, "inputSources");
                if (!parameter.DataType.IsAssignableFrom(source.Binding.ValueType))
                    throw new InvalidCastException("命令输入类型与MDS来源类型不兼容：" + source.Parameter);
            }
            foreach (MRKCommandInputSource source in sources)
            {
                cancellation.ThrowIfCancellationRequested();
                object value = MDSDataBindingResolver.Resolve<object>(data,
                    source.Binding, external);
                values.Add(source.Parameter, value);
            }
            // 原Center统一做最终Schema校验、授权、幂等去重以及输出映射。
            MRKCommandRequest resolved = new MRKCommandRequest(request.Target,
                request.CommandCode, values, request.Caller, request.OperationId, request.RequestId,
                request.Options, request.ExpectedSchemaVersion, request.CommandId);
            return await InvokeBoundResultAsync(resolved, outputOwner, null,
                outputBindings, cancellation).ConfigureAwait(false);
        }

        /// <summary>F19结算未执行的排队命令；不留下永久Queued票据。</summary>
        private async Task ObserveQueuedAsync(MRKCommandExecution operation, FCRWorkHandle<bool> queued)
        {
            FCRWorkResult<bool> result;
            try { result = await queued.Completion.ConfigureAwait(false); }
            catch (Exception ex)
            {
                FinishQueuedWithoutHandler(operation, MRKCommandState.Failed,
                    "QueueFailure", ex.Message);
                return;
            }
            if (operation.IsCompleted) return;
            bool cancelled = result.State == FCRWorkState.Cancelled;
            FinishQueuedWithoutHandler(operation,
                cancelled ? MRKCommandState.Cancelled : MRKCommandState.Failed,
                cancelled ? "CancelledBeforeExecute" : "QueueExecutionFailure",
                result.Error == null ? result.TerminalReason.ToString() : result.Error.Message);
        }
        private void FinishQueuedWithoutHandler(MRKCommandExecution operation, MRKCommandState state,
            string errorCode, string message)
        {
            if (operation.IsCompleted) return;
            operation.Finish(new MRKCommandResponse(operation.Request.RequestId,
                operation.OperationId, MRKCommandStage.Accepted, state,
                errorCode: errorCode, error: message, durationMs: operation.DurationMs));
            MRKExternalOperation tracked;
            if (_tracker.TryGet(operation.OperationId, out tracked)) tracked.Fail();
        }

        private static bool EqualInputs(IReadOnlyDictionary<string, object> a,
            IReadOnlyDictionary<string, object> b)
        {
            if (a.Count != b.Count) return false;
            foreach (KeyValuePair<string, object> pair in a)
            {
                object candidate;
                if (!b.TryGetValue(pair.Key, out candidate) || !EqualValue(pair.Value, candidate)) return false;
            }
            return true;
        }
        private static bool EqualValue(object a, object b)
        {
            if (a == null || b == null) return a == b;
            if (a.GetType() != b.GetType()) return false;
            if (FDTDataValueSafety.IsSimpleImmutable(a.GetType())) return a.Equals(b);
            Array x = a as Array, y = b as Array;
            if (x != null && y != null)
            {
                if (x.Length != y.Length) return false;
                for (int i = 0; i < x.Length; i++)
                    if (!EqualValue(x.GetValue(i), y.GetValue(i))) return false;
                return true;
            }
            Dictionary<string, object> da = a as Dictionary<string, object>,
                db = b as Dictionary<string, object>;
            if (da != null && db != null) return EqualInputs(da, db);
            List<object> la = a as List<object>, lb = b as List<object>;
            if (la != null && lb != null)
                return la.Count == lb.Count && !la.Where((v, i) => !EqualValue(v, lb[i])).Any();
            // Adapter DTO若无明确内容比较契约，拒绝重放而不是误判幂等。
            return false;
        }

        public void Dispose()
        {
            MRKCommandExecution[] pending;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _registry.Dispose();
                pending = _operations.Values.Where(o => !o.IsCompleted).ToArray();
            }
            // 对外Cancellation回调不允许在中心锁内执行。
            foreach (MRKCommandExecution operation in pending)
                operation.Cancellation.Cancel();
            if (_ownsDispatcher)
            {
                // 已执行的设备动作只发协作取消；不能在Dispose中阻塞调用者。
                Task stopping = _dispatcher.StopAsync(true);
            }
        }
    }
}
