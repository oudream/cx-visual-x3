using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CxVisual.Mechanism.Data.Messages
{
    /// <summary>消息Payload的编码策略；不同Codec不改变M36内存分发语义。</summary>
    public interface IMNMMessageCodec
    {
        string ContentType { get; }
        byte[] Encode(object payload, Type declaredType);
        object Decode(byte[] bytes, Type declaredType);
    }

    /// <summary>严格UTF-8文本Codec，使用替换异常回退以拒绝无效字节。</summary>
    public sealed class MNMTextMessageCodec : IMNMMessageCodec
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false,true);
        public string ContentType { get { return "text/plain; charset=utf-8"; } }
        public byte[] Encode(object payload, Type declaredType)
        {
            if (declaredType != typeof(string) || !(payload is string))
                throw new ArgumentException("Text Codec需要string类型");
            return StrictUtf8.GetBytes((string)payload);
        }
        public object Decode(byte[] bytes, Type declaredType)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            if (declaredType != typeof(string)) throw new ArgumentException("Text Codec需要string类型");
            return StrictUtf8.GetString(bytes);
        }
    }

    /// <summary>二进制BufferCodec，在出入站都复制，避免消息生命周期外的修改。</summary>
    public sealed class MNMBinaryMessageCodec : IMNMMessageCodec
    {
        public string ContentType { get { return "application/octet-stream"; } }
        public byte[] Encode(object payload, Type declaredType)
        {
            if (declaredType != typeof(byte[]) || !(payload is byte[]))
                throw new ArgumentException("Binary Codec需要byte[]");
            return (byte[])((byte[])payload).Clone();
        }
        public object Decode(byte[] bytes, Type declaredType)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            if (declaredType != typeof(byte[])) throw new ArgumentException("Binary Codec需要byte[]");
            return (byte[])bytes.Clone();
        }
    }

    /// <summary>
    /// .NET Framework 4.8 的JSON DTO Codec。类型必须来自可信Schema；
    /// 不允许根据远端JSON的任意CLR类型名反射创建对象。
    /// </summary>
    public sealed class MNMJsonMessageCodec : IMNMMessageCodec
    {
        public string ContentType { get { return "application/json"; } }
        public byte[] Encode(object payload, Type declaredType)
        {
            if (payload == null) throw new ArgumentNullException("payload");
            ValidateType(declaredType);
            if (!declaredType.IsInstanceOfType(payload))
                throw new ArgumentException("DTO与声明类型不一致");
            using (MemoryStream stream = new MemoryStream())
            {
                new DataContractJsonSerializer(declaredType).WriteObject(stream,payload);
                return stream.ToArray();
            }
        }
        public object Decode(byte[] bytes, Type declaredType)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            ValidateType(declaredType);
            using (MemoryStream stream = new MemoryStream(bytes,false))
                return new DataContractJsonSerializer(declaredType).ReadObject(stream);
        }
        private static void ValidateType(Type type)
        {
            if (type == null || type == typeof(void) || type.IsPointer ||
                type.ContainsGenericParameters)
                throw new ArgumentException("Codec类型无效","type");
        }
    }

    /// <summary>
    /// 按Definition的PayloadKind/ContentType选择可信Codec；不要求应用使用JSON，
    /// 也不会把ContentType自动当作已经完成编码。
    /// </summary>
    public sealed class MNMMessageSerialization
    {
        private readonly IMNMMessageCodec _text = new MNMTextMessageCodec();
        private readonly IMNMMessageCodec _binary = new MNMBinaryMessageCodec();
        private readonly IMNMMessageCodec _json = new MNMJsonMessageCodec();
        public byte[] Serialize(MNMMessageTypeDefinition definition, object payload)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            definition.Validate(payload);
            byte[] bytes = Resolve(definition).Encode(payload,definition.PayloadType);
            if (definition.MaxPayloadBytes > 0 && bytes.Length > definition.MaxPayloadBytes)
                throw new ArgumentOutOfRangeException("payload","编码消息体超过允许大小");
            return bytes;
        }
        public object Deserialize(MNMMessageTypeDefinition definition, byte[] bytes)
        {
            if (definition == null) throw new ArgumentNullException("definition");
            if (bytes == null) throw new ArgumentNullException("bytes");
            if (definition.MaxPayloadBytes > 0 && bytes.Length > definition.MaxPayloadBytes)
                throw new ArgumentOutOfRangeException("bytes","编码消息体超过允许大小");
            object value = Resolve(definition).Decode(bytes,definition.PayloadType);
            definition.Validate(value);
            return value;
        }
        private IMNMMessageCodec Resolve(MNMMessageTypeDefinition definition)
        {
            if (definition.PayloadKind == MNMPayloadKind.Text &&
                (string.IsNullOrEmpty(definition.ContentType) ||
                 definition.ContentType.StartsWith("text/plain",StringComparison.OrdinalIgnoreCase)))
                return _text;
            if (definition.PayloadKind == MNMPayloadKind.Binary &&
                (string.IsNullOrEmpty(definition.ContentType) ||
                 definition.ContentType.Equals("application/octet-stream",StringComparison.OrdinalIgnoreCase)))
                return _binary;
            if (definition.PayloadKind == MNMPayloadKind.Structured &&
                definition.ContentType.Equals("application/json",StringComparison.OrdinalIgnoreCase))
                return _json;
            throw new NotSupportedException("该PayloadKind/ContentType尚无安全的Codec；请在未来Bridge中显式适配");
        }
    }
}
