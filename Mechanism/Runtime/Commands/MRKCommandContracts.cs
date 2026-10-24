using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Logic.Commands;
using CxVisual.Foundation.Data.Commands;
using CxVisual.Foundation.Data.Types;

namespace CxVisual.Mechanism.Runtime.Commands
{
    public enum MRKCommandKind { System, Module, Object, Device }
    public enum MRKCommandStage { Rejected, Accepted }
    public enum MRKCommandState { Pending, Running, Completed, Failed, Cancelled, Unknown }
    public enum MRKCommandDispatch { Direct, ThreadPool }

    /// <summary>描述执行约束而不是复制MCE参数Schema。MaxConcurrent是拒绝式容量，不使用无限队列。</summary>
    public sealed class MRKCommandPolicy
    {
        public MRKCommandPolicy(int maxConcurrent = 1, string resourceId = null,
            MRKCommandDispatch dispatch = MRKCommandDispatch.ThreadPool, bool supportsCancellation = true)
        {
            if (maxConcurrent < 1 || maxConcurrent > 1024) throw new ArgumentOutOfRangeException("maxConcurrent");
            MaxConcurrent = maxConcurrent; ResourceId = resourceId; Dispatch = dispatch;
            SupportsCancellation = supportsCancellation;
        }
        public int MaxConcurrent { get; private set; }
        public string ResourceId { get; private set; }
        public MRKCommandDispatch Dispatch { get; private set; }
        public bool SupportsCancellation { get; private set; }
    }

    public sealed class MRKCommandDescriptor
    {
        public MRKCommandDescriptor(string target, MCECommandDefinition definition,
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
        public MCECommandDefinition Definition { get; private set; }
        public MRKCommandKind Kind { get; private set; }
        public MRKCommandPolicy Policy { get; private set; }
        public IReadOnlyList<string> Capabilities { get; private set; }
    }

    public sealed class MRKCommandOptions
    {
        public MRKCommandOptions(int timeoutMilliseconds = 0, bool remote = false)
        {
            if (timeoutMilliseconds < 0) throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            TimeoutMilliseconds = timeoutMilliseconds; Remote = remote;
        }
        /// <summary>0取Definition默认值，仍为0则取Host默认值。</summary>
        public int TimeoutMilliseconds { get; private set; }
        /// <summary>预留桥接。P0不把同进程调用冒充IPC。</summary>
        public bool Remote { get; private set; }
    }

    public sealed class MRKCommandRequest
    {
        public MRKCommandRequest(string target, string commandCode,
            IReadOnlyDictionary<string, object> inputs = null, string caller = null,
            Guid operationId = default(Guid), Guid requestId = default(Guid),
            MRKCommandOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(commandCode))
                throw new ArgumentException("命令目标和编码不能为空");
            Target = target; CommandCode = commandCode; Inputs = inputs;
            Caller = caller ?? string.Empty;
            OperationId = operationId == Guid.Empty ? Guid.NewGuid() : operationId;
            RequestId = requestId == Guid.Empty ? Guid.NewGuid() : requestId;
            Options = options ?? new MRKCommandOptions();
        }
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
            Outputs = FDCCommandValueSnapshot.Capture(outputs, adapters);
            BusinessSuccess = businessSuccess; BusinessCode = businessCode ?? string.Empty;
            ErrorCode = errorCode ?? string.Empty; Error = error ?? string.Empty;
            DurationMilliseconds = durationMs;
        }
        public Guid RequestId { get; private set; }
        public Guid OperationId { get; private set; }
        public MRKCommandStage Stage { get; private set; }
        public MRKCommandState State { get; private set; }
        public IReadOnlyDictionary<string, object> Outputs { get; private set; }
        public bool? BusinessSuccess { get; private set; }
        public string BusinessCode { get; private set; }
        public string ErrorCode { get; private set; }
        public string Error { get; private set; }
        public double DurationMilliseconds { get; private set; }
        internal MRKCommandResponse Copy(FDTDataAdapterRegistry adapters, Guid? requestId = null)
        { return new MRKCommandResponse(requestId ?? RequestId, OperationId, Stage, State, Outputs,
            BusinessSuccess, BusinessCode, ErrorCode, Error, DurationMilliseconds, adapters); }
    }

    public sealed class MRKCommandContext
    {
        internal MRKCommandContext(MRKCommandRequest request, MRKCommandDescriptor descriptor,
            Action<double, string> report)
        {
            RequestId = request.RequestId; OperationId = request.OperationId;
            Caller = request.Caller; Target = request.Target;
            Descriptor = descriptor; _report = report;
        }
        private readonly Action<double, string> _report;
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
        private MRKCommandResponse _latest;
        private double _progress;
        private string _message = string.Empty;
        private readonly DateTime _created = DateTime.UtcNow;
        internal MRKCommandExecution(MRKCommandRequest request,
            MRKCommandDescriptor descriptor, IReadOnlyDictionary<string, object> inputs, string key)
        {
            Request = request; Descriptor = descriptor; Inputs = inputs; Key = key;
            _latest = new MRKCommandResponse(request.RequestId, request.OperationId,
                MRKCommandStage.Accepted, MRKCommandState.Pending);
        }
        public Guid OperationId { get { return Request.OperationId; } }
        public Task<MRKCommandResponse> Completion { get { return _completion.Task; } }
        public bool IsCompleted { get { return Completion.IsCompleted; } }
        public double Progress { get { lock (_gate) return _progress; } }
        public string ProgressMessage { get { lock (_gate) return _message; } }
        public MRKCommandResponse Snapshot()
        { lock (_gate) return _latest; }
        internal double DurationMs { get { return (DateTime.UtcNow - _created).TotalMilliseconds; } }
        internal void Running()
        {
            lock (_gate) _latest = new MRKCommandResponse(Request.RequestId, OperationId,
                MRKCommandStage.Accepted, MRKCommandState.Running);
        }
        internal void Report(double percent, string message)
        { lock (_gate) { _progress = percent; _message = message; } }
        internal void Finish(MRKCommandResponse response)
        {
            lock (_gate) _latest = response;
            _completion.TrySetResult(response);
        }
    }
}
