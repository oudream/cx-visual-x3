using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    public enum MPAProtocolOperationKind { Interpret, Read, Write, Send, Request, Publish, Subscribe, Command }
    public enum MPAProtocolOperationStage { Created, Sent, Acknowledged, Completed, Failed }

    public sealed class MPAProtocolOperationRequest
    {
        public MPAProtocolOperationRequest(MPAProtocolOperationKind kind, MPAProtocolAddress target, object value = null)
        {
            if (!Enum.IsDefined(typeof(MPAProtocolOperationKind), kind))
                throw new ArgumentOutOfRangeException("kind");
            Kind = kind; Target = target; Value = value is byte[] ? ((byte[])value).Clone() : value;
            CorrelationId = Guid.NewGuid().ToString("N");
        }
        public MPAProtocolOperationKind Kind { get; private set; }
        public MPAProtocolAddress Target { get; private set; }
        public object Value { get; private set; }
        public string CorrelationId { get; private set; }
    }

    public sealed class MPAProtocolOperationResult
    {
        public MPAProtocolOperationResult(MPAProtocolOperationStage stage, object value = null, string error = null)
        {
            if (!Enum.IsDefined(typeof(MPAProtocolOperationStage), stage)) throw new ArgumentOutOfRangeException("stage");
            if (stage == MPAProtocolOperationStage.Failed && string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("失败结果需要错误信息");
            Stage = stage; Value = value is byte[] ? ((byte[])value).Clone() : value; Error = error;
        }
        public MPAProtocolOperationStage Stage { get; private set; }
        public object Value { get; private set; }
        public string Error { get; private set; }
        public bool IsCompleted { get { return Stage == MPAProtocolOperationStage.Completed; } }
    }

    // 同步解析可直接在已有 F19 Worker 的任务中运行；阻塞 I/O 操作保持 Task 语义。
    public interface IMPADataInterpreter
    {
        object Interpret(byte[] frame, MPAProtocolContext context);
    }
    public interface IMPAProtocolReader
    {
        Task<MPAProtocolOperationResult> ReadAsync(MPAProtocolOperationRequest request,
            MPAProtocolContext context, CancellationToken cancellationToken);
    }
    public interface IMPAProtocolWriter
    {
        Task<MPAProtocolOperationResult> WriteAsync(MPAProtocolOperationRequest request,
            MPAProtocolContext context, CancellationToken cancellationToken);
    }
    public interface IMPAProtocolSender
    {
        Task<MPAProtocolOperationResult> SendAsync(MPAProtocolOperationRequest request,
            MPAProtocolContext context, CancellationToken cancellationToken);
    }
    public interface IMPAProtocolRequester
    {
        Task<MPAProtocolOperationResult> RequestAsync(MPAProtocolOperationRequest request,
            MPAProtocolContext context, CancellationToken cancellationToken);
    }
    // 可选点表设备操作能力：由具体工业 Protocol 解释地址和数据类型，
    // Connector 只负责装配和传递正式配置与 MDS 数据 Owner。
    public interface IMPAProtocolPointAccess
    {
        Task<object> ReadDevicePointAsync(string pointName,
            MPAProtocolContext context, CancellationToken cancellationToken);
        Task<MPAProtocolOperationResult> WriteDevicePointAsync(string pointName,
            MPAProtocolContext context, CancellationToken cancellationToken);
    }
    public interface IMPAProtocolReceiver
    {
        void HandleReceived(MCCChannelReceiveContext received, MPAProtocolContext context);
    }
}
