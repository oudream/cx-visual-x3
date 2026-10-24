using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Foundation.Data.Types;
using CxVisual.Mechanism.Logic.Commands;
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
                Entry item = new Entry(descriptor, handler); _entries.Add(key, item);
                return new Registration(this, item);
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
        public IReadOnlyList<MRKCommandDescriptor> Discover(string target = null,
            MRKCommandKind? kind = null, string capability = null)
        {
            lock (_gate) return Array.AsReadOnly(_entries.Values.Where(e => e.Enabled &&
                (target == null || e.Descriptor.Target == target) &&
                (!kind.HasValue || e.Descriptor.Kind == kind.Value) &&
                (capability == null || e.Descriptor.Capabilities.Contains(capability)))
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
        public IReadOnlyList<MRKCommandDescriptor> Discover(string target = null,
            MRKCommandKind? kind = null, string capability = null)
        { return _registry.Discover(target, kind, capability); }
    }

    /// <summary>
    /// MRK命令入口：MCE为唯一静态定义、MRK Tracker/Lease管理副作用及资源，
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
            int maxOperations = 4096, int defaultTimeoutMilliseconds = 30000)
        {
            if (maxOperations < 1 || defaultTimeoutMilliseconds < 1)
                throw new ArgumentOutOfRangeException("maxOperations/defaultTimeoutMilliseconds");
            _tracker = tracker ?? new MRKExternalOperationTracker();
            _leases = leases ?? new MRKResourceLeaseCoordinator();
            _authorize = authorize; _adapters = adapters;
            _maxOperations = maxOperations; _hostTimeoutMs = defaultTimeoutMilliseconds;
            Router = new MRKCommandRouter(_registry);
        }
        public MRKCommandRegistry Registry { get { return _registry; } }
        public MRKCommandRouter Router { get; private set; }
        public IDisposable Register(MRKCommandDescriptor descriptor, IMRKCommandHandler handler)
        { return _registry.Register(descriptor, handler); }
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
            MRKCommandRegistry.Entry entry = _registry.Resolve(request.Target, request.CommandCode);
            if (entry == null)
                return new MRKCommandSubmission(Reject(request, "HandlerNotFound", "处理器未注册或已禁用"), null);
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
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MRKCommandCenter");
                if (_operations.TryGetValue(request.OperationId, out operation))
                {
                    if (operation.Request.Caller != request.Caller ||
                        operation.Request.Target != request.Target ||
                        operation.Request.CommandCode != request.CommandCode ||
                        !EqualInputs(operation.Inputs, inputs))
                        return new MRKCommandSubmission(Reject(request, "OperationConflict",
                            "OperationId已经绑定不同调用身份、目标或参数"), null);
                    return new MRKCommandSubmission(operation.Snapshot().Copy(_adapters, request.RequestId), operation);
                }
                if (_operations.Count >= _maxOperations)
                    return new MRKCommandSubmission(Reject(request, "HistoryFull",
                        "进程内操作记录容量已满，请完成受控归档后重建Host"), null);
                if (!entry.TryEnter())
                    return new MRKCommandSubmission(Reject(request, "Busy", "命令并发容量已满"), null);
                operation = new MRKCommandExecution(request, entry.Descriptor, inputs,
                    request.Target + "\n" + request.CommandCode);
                _operations.Add(request.OperationId, operation);
                bool created;
                try
                {
                    _tracker.Register(request.OperationId, request.RequestId.ToString("N"), out created);
                    if (!created) throw new InvalidOperationException("MRK Tracker与命令账本不一致");
                }
                catch
                {
                    _operations.Remove(request.OperationId); entry.Exit(); throw;
                }
            }
            // ThreadPool只负责异步发起；资源/并发由正式MRK ResourceLease和Entry控制。
            if (entry.Descriptor.Policy.Dispatch == MRKCommandDispatch.ThreadPool)
                Task.Run(() => ExecuteAsync(operation, entry));
            else
            {
                // Direct同样保证异步结果；同步阻塞Handler由专业Owner自行承担。
                Task pending = ExecuteAsync(operation, entry);
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
                { wait.Cancel(); return (await operation.Completion.ConfigureAwait(false)).Copy(_adapters); }
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
        public bool Cancel(Guid operationId)
        {
            lock (_gate)
            {
                MRKCommandExecution operation;
                if (!_operations.TryGetValue(operationId, out operation) || operation.IsCompleted ||
                    !operation.Descriptor.Policy.SupportsCancellation) return false;
                operation.Cancellation.Cancel();
                return true;
            }
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

        /// <summary>由现有MDS/变量/端口Owner提供读写，不复制Heap或变量表；只在成功执行后映射输出。</summary>
        public async Task<MRKCommandResponse> InvokeBoundAsync(MRKCommandRequest request,
            IFDCCommandValueBinding owner,
            IEnumerable<MRKCommandParameterBinding> inputBindings,
            IEnumerable<MRKCommandParameterBinding> outputBindings,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (owner == null) throw new ArgumentNullException("owner");
            Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
            if (request.Inputs != null)
                foreach (KeyValuePair<string, object> pair in request.Inputs) values.Add(pair.Key, pair.Value);
            foreach (MRKCommandParameterBinding binding in inputBindings ??
                Enumerable.Empty<MRKCommandParameterBinding>())
            {
                if (values.ContainsKey(binding.Parameter)) throw new ArgumentException("输入参数绑定重复：" + binding.Parameter);
                values.Add(binding.Parameter, await owner.ReadAsync(binding.DataAddress, cancellation).ConfigureAwait(false));
            }
            MRKCommandRequest actual = new MRKCommandRequest(request.Target, request.CommandCode, values,
                request.Caller, request.OperationId, request.RequestId, request.Options);
            MRKCommandResponse response = await InvokeAsync(actual, cancellation).ConfigureAwait(false);
            if (response.State == MRKCommandState.Completed)
                foreach (MRKCommandParameterBinding binding in outputBindings ??
                    Enumerable.Empty<MRKCommandParameterBinding>())
                {
                    object value;
                    if (!response.Outputs.TryGetValue(binding.Parameter, out value))
                        throw new ArgumentException("未返回指定输出：" + binding.Parameter);
                    await owner.WriteAsync(binding.DataAddress, value, cancellation).ConfigureAwait(false);
                }
            return response;
        }

        private async Task ExecuteAsync(MRKCommandExecution operation, MRKCommandRegistry.Entry entry)
        {
            MRKCommandRequest request = operation.Request;
            MRKCommandContext context = new MRKCommandContext(request, operation.Descriptor, operation.Report);
            MRKCommandResponse response;
            MRKResourceLease lease = null;
            bool handlerCompleted = false;
            try
            {
                operation.Cancellation.Token.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(operation.Descriptor.Policy.ResourceId))
                    lease = await _leases.AcquireAsync(operation.Descriptor.Policy.ResourceId,
                        operation.OperationId.ToString("N"), operation.Cancellation.Token).ConfigureAwait(false);
                operation.Running();
                IMRKCommandInterceptor[] interceptors;
                lock (_gate) interceptors = _interceptors.ToArray();
                foreach (IMRKCommandInterceptor interceptor in interceptors)
                    await interceptor.BeforeAsync(context, operation.Inputs, operation.Cancellation.Token)
                        .ConfigureAwait(false);
                Task<MRKCommandHandlerResult> pending = entry.Handler.ExecuteAsync(context,
                    operation.Inputs, operation.Cancellation.Token);
                if (pending == null) throw new InvalidOperationException("Handler返回空Task");
                MRKCommandHandlerResult result = await pending.ConfigureAwait(false);
                if (result == null) throw new InvalidOperationException("Handler未返回业务结果");
                IReadOnlyDictionary<string, object> outputs =
                    operation.Descriptor.Definition.ValidateOutputs(result.Outputs);
                response = new MRKCommandResponse(request.RequestId, operation.OperationId,
                    MRKCommandStage.Accepted, MRKCommandState.Completed,
                    outputs, result.BusinessSuccess, result.BusinessCode,
                    durationMs: operation.DurationMs, adapters: _adapters);
                handlerCompleted = true;
                // 完成拦截器故障不可误称设备未动作：返回Unknown供上层独立核对。
                foreach (IMRKCommandInterceptor interceptor in interceptors)
                    await interceptor.AfterAsync(context, response).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (operation.Cancellation.IsCancellationRequested)
            {
                response = new MRKCommandResponse(request.RequestId, operation.OperationId,
                    MRKCommandStage.Accepted, MRKCommandState.Cancelled,
                    errorCode: "Cancelled", error: "Handler协作取消；设备状态须另外确认",
                    durationMs: operation.DurationMs);
            }
            catch (Exception ex)
            {
                response = new MRKCommandResponse(request.RequestId, operation.OperationId,
                    MRKCommandStage.Accepted,
                    handlerCompleted ? MRKCommandState.Unknown : MRKCommandState.Failed,
                    errorCode: handlerCompleted ? "PostExecutionHookFailure" :
                        (ex is UnauthorizedAccessException ? "Unauthorized" : "HandlerFailure"),
                    error: ex.Message, durationMs: operation.DurationMs);
            }
            finally
            {
                if (lease != null) lease.Dispose();
                entry.Exit();
            }
            operation.Finish(response);
            MRKExternalOperation tracked;
            if (_tracker.TryGet(operation.OperationId, out tracked))
            {
                if (response.State == MRKCommandState.Completed) tracked.Confirm();
                else if (response.State != MRKCommandState.Unknown) tracked.Fail(); // MRK.Failed为Provider结算，不等于现场安全停止。
            }
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
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _registry.Dispose();
                foreach (MRKCommandExecution operation in _operations.Values)
                    if (!operation.IsCompleted) operation.Cancellation.Cancel();
            }
        }
    }
}
