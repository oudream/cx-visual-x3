using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Mechanism.Data.Messages
{
    public enum MNMDispatchMode { Inline, Queued }
    public enum MNMMessageQoS { AtMostOnce = 0, AtLeastOnceInProcess = 1 }
    public enum MNMOverflowPolicy { Reject, DropOldest }
    public enum MNMMessageState { Running, Paused, Stopped }
    public enum MNMPublishStage { Rejected, Accepted, Delivered, Failed }

    /// <summary>系统约束由Host设置；不暗示存在磁盘持久化或真实MQTT Broker。</summary>
    public sealed class MNMMessageOptions
    {
        public int QueueCapacity { get; set; } = 1024;
        public int RetainedCapacity { get; set; } = 256;
        public int DeadLetterCapacity { get; set; } = 256;
        public int MaxAttempts { get; set; } = 3;
        public int RetryDelayMilliseconds { get; set; } = 10;
        public MNMOverflowPolicy OverflowPolicy { get; set; } = MNMOverflowPolicy.Reject;
        public Func<string, string, bool> AuthorizePublish { get; set; }
        public Func<string, string, bool> AuthorizeSubscribe { get; set; }
        public void Validate()
        {
            if (QueueCapacity < 1 || RetainedCapacity < 0 || DeadLetterCapacity < 0 ||
                MaxAttempts < 1 || MaxAttempts > 100 || RetryDelayMilliseconds < 0)
                throw new ArgumentOutOfRangeException("MNMMessageOptions");
            if (!Enum.IsDefined(typeof(MNMOverflowPolicy), OverflowPolicy))
                throw new ArgumentOutOfRangeException("OverflowPolicy");
        }
    }

    public sealed class MNMPublishOptions
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public string TypeId { get; set; }
        public MNMDispatchMode DispatchMode { get; set; } = MNMDispatchMode.Queued;
        public MNMMessageQoS QoS { get; set; } = MNMMessageQoS.AtMostOnce;
        public bool Retain { get; set; }
        public bool RequireSubscriber { get; set; }
        public TimeSpan? TimeToLive { get; set; }
        public Guid CorrelationId { get; set; }
        public string ReplyTo { get; set; }
        public IReadOnlyDictionary<string, string> Headers { get; set; }
    }

    public sealed class MNMSubscriptionOptions
    {
        /// <summary>同SubscriberId同Filter再次订阅将替换旧订阅；不同SubscriberId独立消费。</summary>
        public string SubscriberId { get; set; }
        public bool ReplayRetained { get; set; } = true;
        public string SourceId { get; set; }
        /// <summary>仅接收特定发布来源。SourceFilter不是授权证明。</summary>
        public string SourceFilter { get; set; }
        public string TypeId { get; set; }
        public bool NoLocal { get; set; }
        public Func<MNMMessage, bool> Predicate { get; set; }
        public SynchronizationContext SynchronizationContext { get; set; }
    }

    public sealed class MNMMessageHeader
    {
        public Guid MessageId { get; private set; }
        public string Topic { get; private set; }
        public string TypeId { get; private set; }
        public string SourceId { get; private set; }
        public string TargetId { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public Guid CorrelationId { get; private set; }
        public string ReplyTo { get; private set; }
        public DateTime? ExpiresUtc { get; private set; }
        public MNMMessageQoS QoS { get; private set; }
        public IReadOnlyDictionary<string,string> Headers { get; private set; }
        internal MNMMessageHeader(string topic, MNMPublishOptions options)
        {
            MessageId = Guid.NewGuid();
            Topic = MNMTopicFilter.ValidateTopic(topic);
            TypeId = options.TypeId;
            SourceId = options.SourceId;
            TargetId = options.TargetId;
            CreatedUtc = DateTime.UtcNow;
            CorrelationId = options.CorrelationId;
            ReplyTo = options.ReplyTo;
            QoS = options.QoS;
            ExpiresUtc = options.TimeToLive.HasValue ? CreatedUtc.Add(options.TimeToLive.Value) : (DateTime?)null;
            Dictionary<string,string> headers = new Dictionary<string,string>(StringComparer.Ordinal);
            if (options.Headers != null)
                foreach (KeyValuePair<string,string> item in options.Headers)
                    headers.Add(item.Key,item.Value);
            Headers = new ReadOnlyDictionary<string,string>(headers);
        }
    }

    /// <summary>消息体和格式/类型分开。Binary数据入列前复制以防发布者修改。</summary>
    public sealed class MNMMessagePayload
    {
        private readonly object _value;
        public object Value
        {
            get
            {
                byte[] bytes = _value as byte[];
                return bytes == null ? _value : (object)((byte[])bytes.Clone());
            }
        }
        public Type PayloadType { get; private set; }
        public MNMPayloadKind Kind { get; private set; }
        public string ContentType { get; private set; }
        internal MNMMessagePayload(object value, MNMMessageTypeDefinition definition)
        {
            Kind = definition == null ? InferKind(value) : definition.PayloadKind;
            PayloadType = value.GetType();
            ContentType = definition == null ? DefaultContentType(Kind) : definition.ContentType;
            byte[] bytes = value as byte[];
            _value = bytes == null ? value : (object)((byte[])bytes.Clone());
        }
        private static MNMPayloadKind InferKind(object value)
        {
            if (value is string) return MNMPayloadKind.Text;
            if (value is byte[]) return MNMPayloadKind.Binary;
            return MNMPayloadKind.Structured;
        }
        private static string DefaultContentType(MNMPayloadKind kind)
        {
            return kind == MNMPayloadKind.Text ? "text/plain; charset=utf-8" :
                kind == MNMPayloadKind.Binary ? "application/octet-stream" : string.Empty;
        }
        /// <summary>外部读取Buffer时再复制；与原始Publisher隔离。</summary>
        public T Get<T>()
        {
            if (!(_value is T)) throw new InvalidCastException("消息Payload数据类型不一致");
            byte[] bytes = _value as byte[];
            return bytes == null ? (T)_value : (T)(object)((byte[])bytes.Clone());
        }
    }

    public sealed class MNMMessage
    {
        public MNMMessageHeader Header { get; private set; }
        public MNMMessagePayload Payload { get; private set; }
        internal MNMMessage(string topic, object value, MNMPublishOptions options,
            MNMMessageTypeDefinition definition)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (definition != null) { definition.ValidateTopic(topic); definition.Validate(value); }
            Header = new MNMMessageHeader(topic, options);
            Payload = new MNMMessagePayload(value, definition);
        }
    }

    /// <summary>发布受理 ≠ 订阅者业务完成；可选Completion异步等待实际分发结果。</summary>
    public sealed class MNMPublishReceipt
    {
        public Guid MessageId { get; internal set; }
        public MNMPublishStage Stage { get; internal set; }
        public int MatchedCount { get; internal set; }
        public int DeliveredCount { get; internal set; }
        public int FailedCount { get; internal set; }
        public string Error { get; internal set; }
        public bool Accepted { get { return Stage != MNMPublishStage.Rejected; } }
    }

    public sealed class MNMMessageContext<T>
    {
        private readonly MNMMessageCenter _owner;
        public MNMMessage Message { get; private set; }
        public MNMMessageHeader Header { get { return Message.Header; } }
        public T Payload { get { return Message.Payload.Get<T>(); } }
        internal MNMMessageContext(MNMMessageCenter owner, MNMMessage message)
        { _owner = owner; Message = message; }
        /// <summary>回复自动保持CorrelationId；消息回复不是M12命令Handler的执行确认。</summary>
        public Task<MNMPublishReceipt> ReplyAsync<TReply>(TReply response,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(Header.ReplyTo))
                throw new InvalidOperationException("原消息没有ReplyTo");
            return _owner.PublishAsync(Header.ReplyTo, response,
                new MNMPublishOptions {
                    CorrelationId = Header.CorrelationId,
                    DispatchMode = MNMDispatchMode.Inline
                },
                cancellationToken);
        }
    }

    public sealed class MNMDeadLetter
    {
        public Guid MessageId { get; internal set; }
        public string Topic { get; internal set; }
        public string SubscriberId { get; internal set; }
        public string Error { get; internal set; }
        public DateTime OccurredUtc { get; internal set; }
    }

    public sealed class MNMMessageMetrics
    {
        public long Published { get; internal set; }
        public long Accepted { get; internal set; }
        public long Delivered { get; internal set; }
        public long Failed { get; internal set; }
        public long Retried { get; internal set; }
        public long Rejected { get; internal set; }
        public long Dropped { get; internal set; }
        public int QueueDepth { get; internal set; }
        public int SubscriptionCount { get; internal set; }
        public int RetainedCount { get; internal set; }
        public int DeadLetterCount { get; internal set; }
    }

    public interface IMNMMessagePublisher
    {
        MNMPublishReceipt Publish<T>(string topic, T payload, MNMPublishOptions options = null);
        Task<MNMPublishReceipt> PublishAsync<T>(string topic, T payload,
            MNMPublishOptions options = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
    public interface IMNMMessageSubscriber
    {
        IDisposable Subscribe<T>(string topicFilter, Action<MNMMessageContext<T>> handler,
            MNMSubscriptionOptions options = null);
        IDisposable SubscribeAsync<T>(string topicFilter, Func<MNMMessageContext<T>,Task> handler,
            MNMSubscriptionOptions options = null);
    }
}
