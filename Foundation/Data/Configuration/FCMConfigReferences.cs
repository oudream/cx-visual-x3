using System;
using System.Text.RegularExpressions;

namespace CxVisual.Foundation.Data.Configuration
{
    /// <summary>
    /// 机密配置只能保存引用标识，不能放实际密码或密钥内容。
    /// 机密解析由未来独立密钥服务提供，配置系统永不读取实际机密。
    /// </summary>
    [FCMConfigConverter(typeof(FCMConfigSecretRefCodec))]
    public sealed class FCMConfigSecretRef
    {
        public FCMConfigSecretRef(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference) ||
                !Regex.IsMatch(reference, @"\A[A-Za-z0-9][A-Za-z0-9_./:-]{0,255}\z"))
                throw new ArgumentException("Invalid secret reference identity", "reference");
            Reference = reference;
        }
        public string Reference { get; private set; }
        public override string ToString() { return "[SecretRef]"; }
    }
    public sealed class FCMConfigSecretRefCodec : IFCMConfigValueConverter<FCMConfigSecretRef>
    {
        public string Write(FCMConfigSecretRef value)
        {
            if (value == null) return "null";
            return FCMJsonValueCodec.Quote(value.Reference);
        }
        public FCMConfigSecretRef Read(string json)
        {
            string identity = FCMJsonValueCodec.Deserialize<string>(json);
            return identity == null ? null : new FCMConfigSecretRef(identity);
        }
    }

    /// <summary>图像、CT 体数据等大型对象使用外部句柄，仅持久化引用和版本。</summary>
    public sealed class FCMConfigDataHandle
    {
        public string ProviderId { get; set; }
        public string ResourceId { get; set; }
        public string Version { get; set; }
    }

    /// <summary>资源引用不展开资源对象，也不复制共享内存或巨大数组。</summary>
    public sealed class FCMConfigResourceRef
    {
        public string ResourceType { get; set; }
        public string ResourceId { get; set; }
    }
}
