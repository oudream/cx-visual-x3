using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Foundation.Data.Types;

namespace CxVisual.Mechanism.Runtime.Commands
{
    public enum MRKCommandKind { System, Module, Object, Device }
    public enum MRKCommandStage { Rejected, Accepted }
    public enum MRKCommandState { Pending, Running, Completed, Failed, Cancelled, Unknown, Queued, PauseRequested, Paused }
    public enum MRKCommandDispatch { Direct, ThreadPool, Queued }

    /// <summary>描述执行约束而不是复制MCE参数Schema。MaxConcurrent是拒绝式容量，不使用无限队列。</summary>
    public sealed class MRKCommandPolicy
    {
        public MRKCommandPolicy(int maxConcurrent = 1, string resourceId = null,
            MRKCommandDispatch dispatch = MRKCommandDispatch.ThreadPool, bool supportsCancellation = true,
            bool supportsPause = false)
        {
            if (maxConcurrent < 1 || maxConcurrent > 1024) throw new ArgumentOutOfRangeException("maxConcurrent");
            if (dispatch == MRKCommandDispatch.Queued && maxConcurrent != 1)
                throw new ArgumentException("Queued对同一目标按Key串行；MaxConcurrent必须为1", "maxConcurrent");
            MaxConcurrent = maxConcurrent; ResourceId = resourceId; Dispatch = dispatch;
            SupportsCancellation = supportsCancellation; SupportsPause = supportsPause;
        }
        public bool SupportsPause { get; private set; }
        public int MaxConcurrent { get; private set; }
        public string ResourceId { get; private set; }
        public MRKCommandDispatch Dispatch { get; private set; }
        public bool SupportsCancellation { get; private set; }
    }

    public sealed class MRKCommandDescriptor
    {
        public MRKCommandDescriptor(string target, FDCCommandDefinition definition,
            MRKCommandKind kind = MRKCommandKind.Module, MRKCommandPolicy policy = null,
            IEnumerable<string> capabilities = null)
        {
            if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("命令目标不能为空", "target");
            if (definition == null) throw new ArgumentNullException("definition");
            Target = target; Definition = definition; Kind = kind;
            Policy = policy ?? new MRKCommandPolicy();
            string[] items = (capabilities ?? Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
            if (items.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("能力名称不能为空");
            Capabilities = Array.AsReadOnly(items);
        }
        public string Target { get; private set; }
        public FDCCommandDefinition Definition { get; private set; }
        public MRKCommandKind Kind { get; private set; }
        public MRKCommandPolicy Policy { get; private set; }
        public IReadOnlyList<string> Capabilities { get; private set; }
    }

    public sealed class MRKCommandOptions
    {
        public MRKCommandOptions(int timeoutMilliseconds = 0, bool remote = false, int priority = 0)
        {
            if (timeoutMilliseconds < 0) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            if (priority < -1000 || priority > 1000)
                throw new ArgumentOutOfRangeException("priority");
            TimeoutMilliseconds = timeoutMilliseconds; Remote = remote; Priority = priority;
        }
        /// <summary>0取Definition默认值，仍为0则取Host默认值。</summary>
        public int TimeoutMilliseconds { get; private set; }
        /// <summary>预留桥接。P0不把同进程调用冒充IPC。</summary>
        public bool Remote { get; private set; }
        /// <summary>仅Queued分发读取本优先级；不同Target按优先级调度，同Target保持提交顺序。</summary>
        public int Priority { get; private set; }
    }

    public sealed class MRKCommandRequest
    {
        /// <summary>兼容Code调用，也可以附加CommandId同时校验两种身份。</summary>
        public MRKCommandRequest(string target, string commandCode,
            IReadOnlyDictionary<string, object> inputs = null, string caller = null,
            Guid operationId = default(Guid), Guid requestId = default(Guid),
            MRKCommandOptions options = null, int expectedSchemaVersion = 0,
            long commandId = 0)
        {
            if (expectedSchemaVersion < 0) throw new ArgumentOutOfRangeException("expectedSchemaVersion");
            if (commandId < 0) throw new ArgumentOutOfRangeException("commandId");
            if (string.IsNullOrWhiteSpace(target) ||
                (string.IsNullOrWhiteSpace(commandCode) && commandId == 0))
                throw new ArgumentException("必须提供目标和命令Code或CommandId");
            Target = target; CommandCode = commandCode ?? string.Empty;
            CommandId = commandId; Inputs = inputs;
            Caller = caller ?? string.Empty;
            OperationId = operationId == Guid.Empty ? Guid.NewGuid() : operationId;
            RequestId = requestId == Guid.Empty ? Guid.NewGuid() : requestId;
            Options = options ?? new MRKCommandOptions();
            ExpectedSchemaVersion = expectedSchemaVersion;
        }
        /// <summary>仅通过稳定CommandId寻址；目标仍必须是明确Target。</summary>
        public MRKCommandRequest(string target, long commandId,
            IReadOnlyDictionary<string, object> inputs = null, string caller = null,
            Guid operationId = default(Guid), Guid requestId = default(Guid),
            MRKCommandOptions options = null, int expectedSchemaVersion = 0)
            : this(target, string.Empty, inputs, caller, operationId, requestId,
                options, expectedSchemaVersion, commandId)
        { }
        public int ExpectedSchemaVersion { get; private set; }
        public long CommandId { get; private set; }
        public string Target { get; private set; }
        public string CommandCode { get; private set; }
        public IReadOnlyDictionary<string, object> Inputs { get; private set; }
        public string Caller { get; private set; }
        public Guid OperationId { get; private set; }
        public Guid RequestId { get; private set; }
        public MRKCommandOptions Options { get; private set; }
    }

    /// <summary>业务结果与命令执行状态分离。BusinessSuccess=false也可以是Completed。</summary>
    public sealed class MRKCommandHandlerResult
    {
        public MRKCommandHandlerResult(IReadOnlyDictionary<string, object> outputs = null,
            bool businessSuccess = true, string businessCode = null)
        {
            Outputs = outputs; BusinessSuccess = businessSuccess;
            BusinessCode = businessCode ?? string.Empty;
        }
        public IReadOnlyDictionary<string, object> Outputs { get; private set; }
        public bool BusinessSuccess { get; private set; }
        public string BusinessCode { get; private set; }
    }

    public sealed class MRKCommandResponse
    {
        internal MRKCommandResponse(Guid requestId, Guid operationId,
            MRKCommandStage stage, MRKCommandState state,
            IReadOnlyDictionary<string, object> outputs = null,
            bool? businessSuccess = null, string businessCode = null,
            string errorCode = null, string error = null, double durationMs = 0,
            FDTDataAdapterRegistry adapters = null)
        {
            RequestId = requestId; OperationId = operationId; Stage = stage; State = state;
            _adapters = adapters;
            _outputs = FDCCommandValueSnapshot.Capture(outputs, adapters);
            BusinessSuccess = businessSuccess; BusinessCode = businessCode ?? string.Empty;
            ErrorCode = errorCode ?? string.Empty; Error = error ?? string.Empty;
            DurationMilliseconds = durationMs;
        }
        public Guid RequestId { get; private set; }
        public Guid OperationId { get; private set; }
        public MRKCommandStage Stage { get; private set; }
        public MRKCommandState State { get; private set; }
        private readonly IReadOnlyDictionary<string, object> _outputs;
        private readonly FDTDataAdapterRegistry _adapters;
        /// <summary>每次读取返回隔离快照，禁止消费者修改集合或数组污染后续Query/重放。</summary>
        public IReadOnlyDictionary<string, object> Outputs
        { get { return FDCCommandValueSnapshot.Capture(_outputs, _adapters); } }
        public bool? BusinessSuccess { get; private set; }
        public string BusinessCode { get; private set; }
        public string ErrorCode { get; private set; }
        public string Error { get; private set; }
        public double DurationMilliseconds { get; private set; }
        internal MRKCommandResponse Copy(FDTDataAdapterRegistry adapters, Guid? requestId = null)
        { return new MRKCommandResponse(requestId ?? RequestId, OperationId, Stage, State, Outputs,
            BusinessSuccess, BusinessCode, ErrorCode, Error, DurationMilliseconds, adapters); }
    }

    /// <summary>进程内有界操作账本的只读统计，不表示远程设备已经安全停止。</summary>
    public sealed class MRKCommandMetrics
    {
        public int Registered { get; internal set; }
        public int TotalOperations { get; internal set; }
        public int Pending { get; internal set; }
        public int Queued { get; internal set; }
        public int PauseRequested { get; internal set; }
        public int Paused { get; internal set; }
        public int Running { get; internal set; }
        public int Completed { get; internal set; }
        public int Failed { get; internal set; }
        public int Cancelled { get; internal set; }
        public int Unknown { get; internal set; }
    }

    public sealed class MRKCommandContext
    {
        internal MRKCommandContext(MRKCommandRequest request, MRKCommandDescriptor descriptor,
            Action<double, string> report,
            Func<CancellationToken, Task> checkpoint = null,
            Action<IReadOnlyDictionary<string, object>> intermediate = null)
        {
            RequestId = request.RequestId; OperationId = request.OperationId;
            Caller = request.Caller; Target = request.Target;
            Descriptor = descriptor; _report = report;
            _checkpoint = checkpoint; _intermediate = intermediate;
        }
        private readonly Action<double, string> _report;
        private readonly Func<CancellationToken, Task> _checkpoint;
        private readonly Action<IReadOnlyDictionary<string, object>> _intermediate;
        public Guid RequestId { get; private set; }
        public Guid OperationId { get; private set; }
        public string Caller { get; private set; }
        public string Target { get; private set; }
        public MRKCommandDescriptor Descriptor { get; private set; }
        public void ReportProgress(double percent, string message = null)
        {
            if (double.IsNaN(percent) || percent < 0 || percent > 100)
                throw new ArgumentOutOfRangeException("percent");
            _report(percent, message ?? string.Empty);
        }
        /// <summary>仅协作式暂停：Handler主动到达检查点才视为Paused，不代表硬件停止。</summary>
        public Task WaitWhilePausedAsync(CancellationToken cancellation = default(CancellationToken))
        {
            if (_checkpoint == null) { cancellation.ThrowIfCancellationRequested(); return Task.CompletedTask; }
            return _checkpoint(cancellation);
        }
        /// <summary>最新中间值快照；不改变Final Outputs与业务执行结果。</summary>
        public void ReportIntermediate(IReadOnlyDictionary<string, object> outputs)
        {
            if (_intermediate == null) throw new NotSupportedException("当前上下文不支持中间值");
            _intermediate(outputs);
        }
    }

    public interface IMRKCommandHandler
    {
        Task<MRKCommandHandlerResult> ExecuteAsync(MRKCommandContext context,
            IReadOnlyDictionary<string, object> inputs, CancellationToken cancellation);
    }

    public sealed class MRKDelegateCommandHandler : IMRKCommandHandler
    {
        private readonly Func<MRKCommandContext, IReadOnlyDictionary<string, object>,
            CancellationToken, Task<MRKCommandHandlerResult>> _callback;
        public MRKDelegateCommandHandler(
            Func<MRKCommandContext, IReadOnlyDictionary<string, object>,
            CancellationToken, Task<MRKCommandHandlerResult>> callback)
        { _callback = callback ?? throw new ArgumentNullException("callback"); }
        public Task<MRKCommandHandlerResult> ExecuteAsync(MRKCommandContext context,
            IReadOnlyDictionary<string, object> inputs, CancellationToken cancellation)
        { return _callback(context, inputs, cancellation); }
    }

    public interface IMRKCommandInterceptor
    {
        Task BeforeAsync(MRKCommandContext context, IReadOnlyDictionary<string, object> inputs,
            CancellationToken cancellation);
        Task AfterAsync(MRKCommandContext context, MRKCommandResponse response);
    }

    public sealed class MRKCommandParameterBinding
    {
        public MRKCommandParameterBinding(string parameter, string dataAddress)
        {
            if (string.IsNullOrWhiteSpace(parameter) || string.IsNullOrWhiteSpace(dataAddress))
                throw new ArgumentException("参数名称和数据地址不能为空");
            Parameter = parameter; DataAddress = dataAddress;
        }
        public string Parameter { get; private set; }
        public string DataAddress { get; private set; }
    }

    /// <summary>将设备/算法执行结算与数据Owner回写结算分开，不混淆业务副作用。</summary>
    public sealed class MRKCommandBindingResult
    {
        internal MRKCommandBindingResult(MRKCommandResponse execution, bool mappingAttempted,
            bool mappingSucceeded, IEnumerable<string> mappedAddresses,
            string mappingErrorCode = null, string mappingError = null)
        {
            Execution = execution ?? throw new ArgumentNullException("execution");
            MappingAttempted = mappingAttempted; MappingSucceeded = mappingSucceeded;
            MappedAddresses = Array.AsReadOnly((mappedAddresses ??
                Enumerable.Empty<string>()).ToArray());
            MappingErrorCode = mappingErrorCode ?? string.Empty;
            MappingError = mappingError ?? string.Empty;
        }
        public MRKCommandResponse Execution { get; private set; }
        /// <summary>只有Execution.Completed才会进行Output绑定；Unknown时绝不可假设未执行。</summary>
        public bool MappingAttempted { get; private set; }
        public bool MappingSucceeded { get; private set; }
        /// <summary>已经成功写入的地址，后续地址失败时不隐式回滚前面的副作用。</summary>
        public IReadOnlyList<string> MappedAddresses { get; private set; }
        public string MappingErrorCode { get; private set; }
        public string MappingError { get; private set; }
    }

    /// <summary>兼容旧InvokeBoundAsync异常模式，同时将已经完成的执行回执携带给调用方。</summary>
    public sealed class MRKCommandOutputMappingException : Exception
    {
        public MRKCommandOutputMappingException(MRKCommandBindingResult result)
            : base(result == null ? "命令输出映射异常" : result.MappingError)
        { Result = result ?? throw new ArgumentNullException("result"); }
        public MRKCommandBindingResult Result { get; private set; }
    }

    /// <summary>单次操作的可查询票据。超时仅为调用方Unknown；底层Operation继续保留直至结算。</summary>
    public sealed class MRKCommandExecution
    {
        private readonly object _gate = new object();
        private readonly TaskCompletionSource<MRKCommandResponse> _completion =
            new TaskCompletionSource<MRKCommandResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
        internal readonly MRKCommandRequest Request;
        internal readonly MRKCommandDescriptor Descriptor;
        internal readonly IReadOnlyDictionary<string, object> Inputs;
        internal readonly string Key;
        internal readonly FDTDataAdapterRegistry Adapters;
        private MRKCommandResponse _latest;
        private double _progress;
        private string _message = string.Empty;
        private IReadOnlyDictionary<string, object> _intermediateOutputs;
        private bool _pauseRequested;
        private TaskCompletionSource<bool> _resumeSignal;
        private readonly DateTime _created = DateTime.UtcNow;
        internal MRKCommandExecution(MRKCommandRequest request,
            MRKCommandDescriptor descriptor, IReadOnlyDictionary<string, object> inputs,
            string key, FDTDataAdapterRegistry adapters)
        {
            Request = request; Descriptor = descriptor; Inputs = inputs; Key = key;
            Adapters = adapters;
            _latest = new MRKCommandResponse(request.RequestId, request.OperationId,
                MRKCommandStage.Accepted, MRKCommandState.Pending);
        }
        public Guid OperationId { get { return Request.OperationId; } }
        public Task<MRKCommandResponse> Completion { get { return _completion.Task; } }
        public bool IsCompleted { get { return Completion.IsCompleted; } }
        public double Progress { get { lock (_gate) return _progress; } }
        public string ProgressMessage { get { lock (_gate) return _message; } }
        public IReadOnlyDictionary<string, object> IntermediateOutputs
        {
            get
            {
                IReadOnlyDictionary<string, object> value;
                lock (_gate) value = _intermediateOutputs;
                return FDCCommandValueSnapshot.Capture(value, Adapters);
            }
        }
        public MRKCommandResponse Snapshot()
        { lock (_gate) return _latest.Copy(Adapters); }
        internal double DurationMs { get { return (DateTime.UtcNow - _created).TotalMilliseconds; } }
        internal void Queued()
        {
            lock (_gate) _latest = new MRKCommandResponse(Request.RequestId, OperationId,
                MRKCommandStage.Accepted, MRKCommandState.Queued);
        }
        internal void Running()
        {
            lock (_gate) _latest = new MRKCommandResponse(Request.RequestId, OperationId,
                MRKCommandStage.Accepted, MRKCommandState.Running);
        }
        internal void Report(double percent, string message)
        { lock (_gate) { if (!IsCompleted) { _progress = percent; _message = message; } } }
        internal void ReportIntermediate(IReadOnlyDictionary<string, object> values)
        {
            IReadOnlyDictionary<string, object> snapshot = FDCCommandValueSnapshot.Capture(values, Adapters);
            lock (_gate) if (!IsCompleted) _intermediateOutputs = snapshot;
        }
        internal bool TryPause()
        {
            lock (_gate)
            {
                if (!Descriptor.Policy.SupportsPause || _pauseRequested ||
                    _latest.State != MRKCommandState.Running) return false;
                _pauseRequested = true;
                _resumeSignal = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _latest = new MRKCommandResponse(Request.RequestId, OperationId,
                    MRKCommandStage.Accepted, MRKCommandState.PauseRequested);
                return true;
            }
        }
        internal bool TryResume()
        {
            TaskCompletionSource<bool> resume;
            lock (_gate)
            {
                if (!_pauseRequested ||
                    (_latest.State != MRKCommandState.PauseRequested &&
                     _latest.State != MRKCommandState.Paused)) return false;
                _pauseRequested = false; resume = _resumeSignal; _resumeSignal = null;
                _latest = new MRKCommandResponse(Request.RequestId, OperationId,
                    MRKCommandStage.Accepted, MRKCommandState.Running);
            }
            resume.TrySetResult(true);
            return true;
        }
        internal async Task WaitWhilePausedAsync(CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            Task gate;
            lock (_gate)
            {
                if (!_pauseRequested) return;
                _latest = new MRKCommandResponse(Request.RequestId, OperationId,
                    MRKCommandStage.Accepted, MRKCommandState.Paused);
                gate = _resumeSignal.Task;
            }
            TaskCompletionSource<bool> stopped = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellation.Register(() => stopped.TrySetResult(true)))
            {
                Task finished = await Task.WhenAny(gate, stopped.Task).ConfigureAwait(false);
                if (finished == stopped.Task) cancellation.ThrowIfCancellationRequested();
                await gate.ConfigureAwait(false);
            }
        }
        internal void Finish(MRKCommandResponse response)
        {
            lock (_gate) _latest = response;
            _completion.TrySetResult(response);
        }
    }
}
