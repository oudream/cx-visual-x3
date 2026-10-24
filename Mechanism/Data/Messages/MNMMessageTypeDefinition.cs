using System;
using System.Text;

namespace CxVisual.Mechanism.Data.Messages
{
    /// <summary>
    /// Payload在应用层的形态。与传输编码(ContentType/Codec)分离。
    /// Structured在进程内是强类型DTO，跨进程必须由Bridge选取真实Codec。
    /// </summary>
    public enum MNMPayloadKind { Text, Binary, Structured, Reference }

    /// <summary>
    /// M36消息类型定义（可选择绑定具体Topic）。
    /// Topic是发布地址；订阅Filter单独由MNMTopicFilter表达；实际Payload由消息实例携带。
    /// </summary>
    public sealed class MNMMessageTypeDefinition
    {
        /// <summary>
        /// 老的三参数构造保留：没有Topic时作为可重用类型模板；实际Publish前需绑定或提供并校验Topic。
        /// </summary>
        public MNMMessageTypeDefinition(string typeId, int schemaVersion, Type payloadType,
            string topicCode = null, string name = null, string description = null,
            string topic = null, MNMPayloadKind? payloadKind = null,
            string contentType = null, int maxPayloadBytes = 0)
        {
            if (!MNMTopicFilter.IsCode(typeId))
                throw new ArgumentException("消息类型ID无效", "typeId");
            if (schemaVersion < 1) throw new ArgumentOutOfRangeException("schemaVersion");
            if (payloadType == null || payloadType == typeof(void) ||
                payloadType.IsPointer || payloadType.ContainsGenericParameters)
                throw new ArgumentException("消息负载类型无效", "payloadType");
            if (maxPayloadBytes < 0) throw new ArgumentOutOfRangeException("maxPayloadBytes");
            if (contentType != null && (contentType.Length > 128 ||
                contentType.IndexOfAny(new char[] { '\r', '\n', '\0' }) >= 0))
                throw new ArgumentException("无效ContentType", "contentType");

            string code = topicCode;
            if (string.IsNullOrEmpty(code))
            {
                int pos = typeId.LastIndexOf('.');
                code = pos < 0 ? typeId : typeId.Substring(pos + 1);
            }
            if (!MNMTopicFilter.IsCode(code))
                throw new ArgumentException("消息TopicCode无效", "topicCode");

            MNMPayloadKind kind = payloadKind ??
                (payloadType == typeof(string) ? MNMPayloadKind.Text :
                (payloadType == typeof(byte[]) ? MNMPayloadKind.Binary : MNMPayloadKind.Structured));
            if (!Enum.IsDefined(typeof(MNMPayloadKind), kind))
                throw new ArgumentOutOfRangeException("payloadKind");
            if (kind == MNMPayloadKind.Text && payloadType != typeof(string))
                throw new ArgumentException("Text只能声明string", "payloadType");
            if (kind == MNMPayloadKind.Binary && payloadType != typeof(byte[]))
                throw new ArgumentException("Binary只能声明byte[]", "payloadType");
            if (kind == MNMPayloadKind.Structured &&
                (payloadType == typeof(string) || payloadType == typeof(byte[])))
                throw new ArgumentException("Structured需要结构化数据类型", "payloadType");

            TypeId = typeId;
            SchemaVersion = schemaVersion;
            PayloadType = payloadType;
            PayloadKind = kind;
            TopicCode = code;
            Name = string.IsNullOrWhiteSpace(name) ? typeId : name;
            Description = description ?? string.Empty;
            ContentType = !string.IsNullOrWhiteSpace(contentType) ? contentType :
                (kind == MNMPayloadKind.Text ? "text/plain; charset=utf-8" :
                (kind == MNMPayloadKind.Binary ? "application/octet-stream" : string.Empty));
            MaxPayloadBytes = maxPayloadBytes;

            if (!string.IsNullOrEmpty(topic))
            {
                string valid = MNMTopicFilter.ValidateTopic(topic);
                if (!string.Equals(valid.Substring(valid.LastIndexOf('/') + 1),
                    TopicCode, StringComparison.Ordinal))
                    throw new ArgumentException("Topic末段必须与TopicCode一致", "topic");
                Topic = valid;
            }
        }

        public string TypeId { get; private set; }
        public int SchemaVersion { get; private set; }
        public string Topic { get; private set; }
        public string TopicCode { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public Type PayloadType { get; private set; }
        public MNMPayloadKind PayloadKind { get; private set; }
        public string ContentType { get; private set; }
        /// <summary>
        /// 0=采用宿主/投递策略配额；对Text/Binary当场校验，DTO需编码后再校验传输大小。
        /// </summary>
        public int MaxPayloadBytes { get; private set; }

        /// <summary>把可复用类型绑定到一台机器/模块的具体Topic，不修改原定义。</summary>
        public MNMMessageTypeDefinition WithTopic(string topic)
        {
            return new MNMMessageTypeDefinition(TypeId, SchemaVersion, PayloadType,
                TopicCode, Name, Description, topic, PayloadKind, ContentType, MaxPayloadBytes);
        }

        /// <summary>严格校验实际发布Topic；已绑定的定义不能改用其他机器的Topic。</summary>
        public void ValidateTopic(string topic)
        {
            string valid = MNMTopicFilter.ValidateTopic(topic);
            if (!string.Equals(valid.Substring(valid.LastIndexOf('/') + 1),
                TopicCode, StringComparison.Ordinal) ||
                (Topic != null && !string.Equals(valid, Topic, StringComparison.Ordinal)))
                throw new ArgumentException("消息Topic与定义或绑定实例不一致", "topic");
        }

        public void Validate(object payload)
        {
            if (payload == null || !PayloadType.IsInstanceOfType(payload))
                throw new ArgumentException("消息负载类型与定义不一致", "payload");

            if (MaxPayloadBytes > 0)
            {
                // 不在MessageTypeDefinition里偷偷做JSON/Protobuf序列化；
                // Structured/Reference的编码大小必须由实际Codec在入队/Bridge前验证。
                int size = -1;
                if (PayloadKind == MNMPayloadKind.Text)
                    size = Encoding.UTF8.GetByteCount((string)payload);
                else if (PayloadKind == MNMPayloadKind.Binary)
                    size = ((byte[])payload).Length;
                if (size > MaxPayloadBytes)
                    throw new ArgumentOutOfRangeException("payload", "消息内联负载超出上限");
            }
        }
    }
}
