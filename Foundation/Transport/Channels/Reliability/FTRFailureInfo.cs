using System;

namespace CxVisual.Foundation.Transport
{
    /// <summary>传输故障类型；不将取消或业务结果不确定等同于可安全重放。</summary>
    public enum FTRFailureKind
    {
        Unknown,
        Timeout,
        Cancelled,
        RemoteClosed,
        NetworkUnavailable,
        PermissionDenied,
        InvalidConfiguration,
        ResourceExhausted,
        TransportIO
    }

    /// <summary>故障影响范围，不能因为单个Peer失败便默认重启整个Server。</summary>
    public enum FTRFaultScope
    {
        Operation,
        SubChannel,
        Channel
    }

    /// <summary>额度耗尽是可观察的资源拒绝，不伪装为OutOfMemoryException。</summary>
    internal class FTRBudgetExceededException : InvalidOperationException
    {
        internal FTRBudgetExceededException(string message):base(message){}
    }
    /// <summary>跨客户端共享额度不足；区别于单条请求超过MaxRequestBytes。</summary>
    internal sealed class FTRSharedBudgetExceededException : FTRBudgetExceededException
    {
        internal FTRSharedBudgetExceededException():base("Channel多对端在途内存预算已满"){}
    }

    /// <summary>诊断值对象，仅记录有限的非载荷证据，不持有原始异常或大数据缓存。</summary>
    public sealed class FTRFailureInfo
    {
        public FTRFailureKind Kind { get; private set; }
        public FTRFaultScope Scope { get; private set; }
        public string Stage { get; private set; }
        public string Message { get; private set; }
        public DateTime TimestampUtc { get; private set; }

        internal FTRFailureInfo(FTRFailureKind kind,FTRFaultScope scope,string stage,string message)
        {
            Kind=kind;Scope=scope;Stage=stage;Message=message;TimestampUtc=DateTime.UtcNow;
        }
    }
}
