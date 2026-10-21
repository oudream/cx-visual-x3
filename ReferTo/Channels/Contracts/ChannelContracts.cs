using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Platform.Channels
{
    public enum ChannelHealthState
    {
        Closed,
        Connecting,
        Online,
        Degraded,
        Faulted
    }

    public sealed class ChannelEndpoint
    {
        public ChannelEndpoint(string scheme, string host, int port)
        {
            if (string.IsNullOrWhiteSpace(scheme)) throw new ArgumentException("scheme不能为空。", nameof(scheme));
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host不能为空。", nameof(host));
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            Scheme = scheme.Trim().ToLowerInvariant();
            Host = host.Trim();
            Port = port;
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public string Scheme { get; }
        public string Host { get; }
        public int Port { get; }
        public IDictionary<string, string> Settings { get; }

        public override string ToString()
        {
            return Scheme + "://" + Host + ":" + Port;
        }
    }

    public sealed class ChannelHealth
    {
        public ChannelHealthState State { get; internal set; }
        public DateTime ChangedUtc { get; internal set; }
        public string LastError { get; internal set; }
        public bool IsOnline => State == ChannelHealthState.Online;
    }

    public sealed class ChannelDiagnostics
    {
        public long ConnectCount { get; internal set; }
        public long DisconnectCount { get; internal set; }
        public long SendCount { get; internal set; }
        public long ReceiveCount { get; internal set; }
        public long SentBytes { get; internal set; }
        public long ReceivedBytes { get; internal set; }
        public long FailureCount { get; internal set; }
        public DateTime? LastSendUtc { get; internal set; }
        public DateTime? LastReceiveUtc { get; internal set; }
        public string LastError { get; internal set; }
    }

    /// <summary>协议与传输实现之间的公共字节通道边界。</summary>
    public interface IChannel : IDisposable
    {
        string Id { get; }
        CxChannelDescriptor Descriptor { get; }
        CxChannelState State { get; }
        CxChannelHealth HealthSnapshot { get; }
        CxChannelDiagnostics FrameDiagnostics { get; }
        Task OpenAsync(CancellationToken cancellationToken);
        Task CloseAsync(CancellationToken cancellationToken);
        Task<byte[]> RequestAsync(byte[] request, int responseLength, CancellationToken cancellationToken);
        string Subscribe(Action<byte[]> callback);
        bool Unsubscribe(string subscriptionId);
        string ChannelId { get; }
        ChannelEndpoint Endpoint { get; }
        ChannelHealth Health { get; }
        ChannelDiagnostics Diagnostics { get; }
        event Action<ChannelHealth> HealthChanged;
        Task ConnectAsync(CancellationToken cancellationToken);
        Task DisconnectAsync(CancellationToken cancellationToken);
        Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
        Task<int> ReceiveAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);
        void Close();
    }
}
