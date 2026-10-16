using System;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>运行时数据故障分类；与Runtime任务/硬件状态错误分离。</summary>
    public enum MDSDataErrorCode
    {
        InvalidName,
        NotFound,
        NameConflict,
        TypeMismatch,
        InputReadOnly,
        OutputNotDeclared,
        OutputMissing,
        OutputConflict,
        Busy,
        InvalidState,
        ScopeClosed,
        UnsafeValue,
        Unsupported,
        OperationCancelled
    }

    /// <summary>携带数据槽与调用上下文的结构化故障，便于工程编排界面定位。</summary>
    public sealed class MDSDataException : InvalidOperationException
    {
        public MDSDataException(MDSDataErrorCode code, string message,
            string slot = null, string invocationId = null, Exception inner = null)
            : base(message, inner)
        {
            Code = code;
            Slot = slot;
            InvocationId = invocationId;
        }
        public MDSDataErrorCode Code { get; private set; }
        public string Slot { get; private set; }
        public string InvocationId { get; private set; }
    }
}
