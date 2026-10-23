using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Connectivity
{
    // 未来 Message/Command Owner 完成前，只保留规约侧可选能力，不引入第二套消息/命令中心。
    public interface IMPAProtocolDataMapper
    {
        object MapInbound(object value, MPAProtocolContext context);
        object MapOutbound(object value, MPAProtocolContext context);
    }
    public interface IMPAProtocolRouteResolver
    {
        MPAProtocolRoute Resolve(MPAProtocolOperationRequest request, MPAProtocolContext context);
    }
    public interface IMPAProtocolSubscriber
    {
        Task<string> SubscribeAsync(string source, MPAProtocolContext context, CancellationToken cancellationToken);
        Task UnsubscribeAsync(string subscriptionId, CancellationToken cancellationToken);
    }
    public interface IMPAProtocolEventHandler
    {
        void HandleEvent(object protocolEvent, MPAProtocolContext context);
    }
    public interface IMPAProtocolHealthProvider
    {
        MPAProtocolDiagnosticsSnapshot GetDiagnostics();
    }

    public sealed class MPAProtocolDiagnostics
    {
        private readonly object _gate = new object();
        private long _requestCount, _responseCount, _decodeErrors;
        private string _lastError;
        public void RecordRequest() { lock (_gate) _requestCount++; }
        public void RecordResponse() { lock (_gate) _responseCount++; }
        public void RecordDecodeError(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("规约解析错误原因必填");
            lock (_gate) { _decodeErrors++; _lastError = reason; }
        }
        public MPAProtocolDiagnosticsSnapshot Snapshot()
        {
            lock (_gate) return new MPAProtocolDiagnosticsSnapshot(_requestCount, _responseCount, _decodeErrors, _lastError);
        }
    }
    public sealed class MPAProtocolDiagnosticsSnapshot
    {
        public MPAProtocolDiagnosticsSnapshot(long requests, long responses, long decodeErrors, string lastError)
        {
            Requests = requests; Responses = responses; DecodeErrors = decodeErrors; LastError = lastError;
        }
        public long Requests { get; private set; }
        public long Responses { get; private set; }
        public long DecodeErrors { get; private set; }
        public string LastError { get; private set; }
    }
}
