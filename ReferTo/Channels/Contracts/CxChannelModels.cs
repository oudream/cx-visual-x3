using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CxVisual.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CxVisual.Platform.Channels
{
    public enum CxChannelState { Closed, Opening, Open, Reconnecting, Faulted, Closing }
    public sealed class CxChannelEndpoint
    {
        public string Address { get; set; }
        public int Port { get; set; }
        public string Path { get; set; }
        public IDictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public CxValidationResult Validate()
        {
            var result = new CxValidationResult();
            if (string.IsNullOrWhiteSpace(Address) || Port < 0 || Port > 65535 || Parameters == null)
                CxChannelValidation.Error(result, "CHANNEL_ENDPOINT_INVALID", "Address, port and parameters are required.");
            return result;
        }
        public override string ToString() => (Address != null && Address.Contains(":" ) ? "[" + Address + "]" : Address) + (Port == 0 ? "" : ":" + Port.ToString(CultureInfo.InvariantCulture)) + (Path ?? "");
        public CxChannelEndpoint Clone() => new CxChannelEndpoint { Address = Address, Port = Port, Path = Path, Parameters = Parameters == null ? null : new Dictionary<string, string>(Parameters, StringComparer.Ordinal) };
    }
    public sealed class CxBufferPolicy
    {
        public int Capacity { get; set; } = 65536;
        public string Backpressure { get; set; } = "Wait";
        public string DropPolicy { get; set; } = "None";
        public int BatchSize { get; set; } = 4096;
        public CxValidationResult Validate()
        {
            var result = new CxValidationResult();
            if (Capacity < 1 || BatchSize < 1 || BatchSize > Capacity || (Backpressure != "Wait" && Backpressure != "Reject" && Backpressure != "Drop") ||
                (Backpressure == "Drop" ? DropPolicy != "DropOldest" && DropPolicy != "DropNewest" : DropPolicy != "None"))
                CxChannelValidation.Error(result, "CHANNEL_BUFFER_POLICY_INVALID", "Invalid capacity, batch, backpressure or drop policy.");
            return result;
        }
        public CxBufferPolicy Clone() => new CxBufferPolicy { Capacity = Capacity, Backpressure = Backpressure, DropPolicy = DropPolicy, BatchSize = BatchSize };
    }
    public sealed class CxReconnectPolicy
    {
        public bool Enabled { get; set; }
        public int MaxAttempts { get; set; } = 3;
        public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(1);
        public double Backoff { get; set; } = 2;
        public bool ShouldRetry(int attemptsMade)
        { Validate(); if (attemptsMade < 0) throw new ArgumentOutOfRangeException(nameof(attemptsMade)); return Enabled && attemptsMade < MaxAttempts; }
        public TimeSpan GetDelay(int retryIndex)
        {
            Validate(); if (retryIndex < 0) throw new ArgumentOutOfRangeException(nameof(retryIndex));
            if (Delay.Ticks == 0) return TimeSpan.Zero;
            var ticks = Delay.Ticks * Math.Pow(Backoff, retryIndex);
            return TimeSpan.FromTicks(double.IsInfinity(ticks) || ticks >= TimeSpan.MaxValue.Ticks ? TimeSpan.MaxValue.Ticks : (long)ticks);
        }
        public void Validate() { if (MaxAttempts < 0 || Delay < TimeSpan.Zero || double.IsNaN(Backoff) || double.IsInfinity(Backoff) || Backoff < 1) throw new ArgumentException("CHANNEL_RECONNECT_POLICY_INVALID"); }
        public CxReconnectPolicy Clone() => new CxReconnectPolicy { Enabled = Enabled, MaxAttempts = MaxAttempts, Delay = Delay, Backoff = Backoff };
    }
    public sealed class CxChannelDescriptor
    {
        public string Id { get; set; }
        public string Kind { get; set; } = "TCP";
        public CxChannelEndpoint Endpoint { get; set; } = new CxChannelEndpoint();
        public CxBufferPolicy BufferPolicy { get; set; } = new CxBufferPolicy();
        public CxReconnectPolicy ReconnectPolicy { get; set; } = new CxReconnectPolicy();
        public string SecurityProfile { get; set; }
        public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
        public CxValidationResult Validate()
        {
            var result = new CxValidationResult();
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Kind) || Endpoint == null || BufferPolicy == null || ReconnectPolicy == null || Metadata == null)
                CxChannelValidation.Error(result, "CHANNEL_DESCRIPTOR_REQUIRED", "Identity, kind and policies are required.");
            if (Endpoint != null) result.Merge(Endpoint.Validate()); if (BufferPolicy != null) result.Merge(BufferPolicy.Validate());
            if (string.Equals(Kind, "TCP", StringComparison.OrdinalIgnoreCase) && (Endpoint == null || Endpoint.Port < 1)) CxChannelValidation.Error(result, "CHANNEL_TCP_PORT_REQUIRED", "TCP requires a port.");
            try { ReconnectPolicy?.Validate(); if (Metadata != null) CxVariableValueRules.CheckJson(JObject.FromObject(Metadata, CxVariableValueRules.Serializer())); }
            catch (Exception error) { CxChannelValidation.Error(result, "CHANNEL_DESCRIPTOR_VALUE_INVALID", error.Message); }
            return result;
        }
        public CxChannelDescriptor Clone()
        {
            Validate().ThrowIfInvalid();
            return new CxChannelDescriptor { Id = Id, Kind = Kind, Endpoint = Endpoint.Clone(), BufferPolicy = BufferPolicy.Clone(), ReconnectPolicy = ReconnectPolicy.Clone(),
                SecurityProfile = SecurityProfile, Metadata = JObject.FromObject(Metadata, CxVariableValueRules.Serializer()).ToObject<Dictionary<string, object>>(CxVariableValueRules.Serializer()) };
        }
        internal static CxChannelDescriptor FromLegacy(string id, ChannelEndpoint endpoint) => new CxChannelDescriptor { Id = id, Kind = endpoint.Scheme.ToUpperInvariant(),
            Endpoint = new CxChannelEndpoint { Address = endpoint.Host, Port = endpoint.Port, Parameters = new Dictionary<string, string>(endpoint.Settings, StringComparer.Ordinal) } };
    }
    public sealed class CxChannelHealth
    {
        public CxChannelState State { get; set; }
        public DateTime? LastSuccess { get; set; }
        public string LastError { get; set; }
        public TimeSpan Latency { get; set; }
        public double SendRate { get; set; }
        public double ReceiveRate { get; set; }
        public long ReconnectCount { get; set; }
        public bool IsHealthy() => State == CxChannelState.Open && LastError == null;
        public CxChannelHealth Clone() => (CxChannelHealth)MemberwiseClone();
    }
    public sealed class CxMessageEnvelope
    {
        public string MessageId { get; set; } = Guid.NewGuid().ToString("N");
        public string CorrelationId { get; set; }
        public string CausationId { get; set; }
        public DateTime Timestamp { get; set; }
        public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public object Payload { get; set; }
        public CxMessageEnvelope Clone()
        {
            if (string.IsNullOrWhiteSpace(MessageId) || Headers == null) throw new ArgumentException("CHANNEL_ENVELOPE_INVALID");
            var token = CxVariableValueRules.Token(Payload); CxVariableValueRules.CheckJson(token);
            return new CxMessageEnvelope { MessageId = MessageId, CorrelationId = CorrelationId, CausationId = CausationId, Timestamp = Timestamp,
                Headers = new Dictionary<string, string>(Headers, StringComparer.Ordinal), Payload = Payload is byte[] bytes ? (object)bytes.Clone() : token.DeepClone() };
        }
        public string GetHeader(string name, string fallback = null) { if (name == null) throw new ArgumentNullException(nameof(name)); string value; return Headers != null && Headers.TryGetValue(name, out value) ? value : fallback; }
    }
    public sealed class CxChannelDiagnostics
    {
        private readonly object _gate = new object();
        private readonly Queue<CxMessageEnvelope> _frames = new Queue<CxMessageEnvelope>();
        private readonly Queue<string> _errors = new Queue<string>();
        private readonly Dictionary<string, double> _metrics = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly int _capacity;
        public CxChannelDiagnostics(int capacity = 128) { if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity)); _capacity = capacity; }
        public IReadOnlyList<CxMessageEnvelope> Frames { get { lock (_gate) return _frames.Select(x => x.Clone()).ToList().AsReadOnly(); } }
        public IReadOnlyDictionary<string, double> Metrics { get { lock (_gate) return new System.Collections.ObjectModel.ReadOnlyDictionary<string, double>(new Dictionary<string, double>(_metrics)); } }
        public IReadOnlyList<string> Errors { get { lock (_gate) return _errors.ToList().AsReadOnly(); } }
        public void TraceSend(CxMessageEnvelope envelope) => Trace(envelope, "send");
        public void TraceReceive(CxMessageEnvelope envelope) => Trace(envelope, "receive");
        private void Trace(CxMessageEnvelope envelope, string direction)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope)); var frame = envelope.Clone(); frame.Headers["direction"] = direction;
            lock (_gate) { _frames.Enqueue(frame); while (_frames.Count > _capacity) _frames.Dequeue(); double count; _metrics.TryGetValue(direction, out count); _metrics[direction] = count + 1; }
        }
        public void TraceError(Exception error)
        { if (error == null) throw new ArgumentNullException(nameof(error)); lock (_gate) { _errors.Enqueue(error.Message); while (_errors.Count > _capacity) _errors.Dequeue(); double count; _metrics.TryGetValue("errors", out count); _metrics["errors"] = count + 1; } }
        public CxChannelDiagnostics Snapshot()
        { lock (_gate) { var copy = new CxChannelDiagnostics(_capacity); foreach (var frame in _frames) copy._frames.Enqueue(frame.Clone()); foreach (var error in _errors) copy._errors.Enqueue(error); foreach (var metric in _metrics) copy._metrics.Add(metric.Key, metric.Value); return copy; } }
    }
    internal static class CxChannelValidation
    { internal static void Error(CxValidationResult result, string code, string message) => result.Add(new CxValidationIssue { Code = code, Message = message, Severity = CxValidationSeverity.Error }); }
}
