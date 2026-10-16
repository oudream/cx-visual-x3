using System;

namespace CxVisual.Foundation.Data.Configuration
{
    /// <summary>跨配置实例的强类型身份引用；目标字段允许指向某个深层路径。</summary>
    public sealed class FCMConfigReference
    {
        public FCMConfigReference(FCMConfigIdentity target, string path, bool optional = false)
        {
            Target = target ?? throw new ArgumentNullException("target");
            Path = path ?? "";
            Optional = optional;
        }
        public FCMConfigIdentity Target { get; private set; }
        public string Path { get; private set; }
        public bool Optional { get; private set; }
        public override string ToString() { return Target.CanonicalKey + "::" + Path; }
    }

    /// <summary>资源存在性由真实 Provider 判断；配置底座不加载数据内容。</summary>
    public interface IFCMConfigResourceReferenceProvider
    {
        string ResourceType { get; }
        bool Exists(string resourceId, out string reason);
    }
}
