using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    public sealed class MHSConfigReferenceValue
    {
        internal MHSConfigReferenceValue(FCMConfigReference reference, string json,
            string version, FCMConfigValuePresence presence, FCMConfigIssue issue)
        { Reference = reference; Json = json; Version = version; Presence = presence; Issue = issue; }
        public FCMConfigReference Reference { get; private set; }
        public string Json { get; private set; }
        public string Version { get; private set; }
        public FCMConfigValuePresence Presence { get; private set; }
        public FCMConfigIssue Issue { get; private set; }
        public bool HasValue
        {
            get { return Presence == FCMConfigValuePresence.Present ||
                Presence == FCMConfigValuePresence.Defaulted || Presence == FCMConfigValuePresence.ExplicitNull; }
        }
        public T Read<T>()
        {
            if (!HasValue) throw new InvalidOperationException("CONFIG_REFERENCE_NOT_RESOLVED");
            return FCMJsonValueCodec.Deserialize<T>(Json);
        }
    }

    internal sealed class MHSConfigReferenceSource
    {
        internal FCMConfigIdentity Identity;
        internal Func<string, MHSConfigReferenceValue> Read;
    }

    /// <summary>
    /// 基础配置/资源引用目录，带目标存在性诊断与循环依赖检测；
    /// 只索引已打开的配置实例，不创建第二套配置存储。
    /// </summary>
    public sealed class MHSConfigReferenceRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, MHSConfigReferenceSource> _sources =
            new Dictionary<string, MHSConfigReferenceSource>(StringComparer.Ordinal);
        private readonly Dictionary<string, IFCMConfigResourceReferenceProvider> _resources =
            new Dictionary<string, IFCMConfigResourceReferenceProvider>(StringComparer.Ordinal);
        private readonly FCMConfigDependencyGraph _dependencies = new FCMConfigDependencyGraph();
        private readonly MHSConfigService _service;
        public MHSConfigReferenceRegistry(MHSConfigService service)
        { _service = service ?? throw new ArgumentNullException("service"); }

        public void Register<T>(FCMConfigIdentity identity) where T : new()
        {
            if (identity == null) throw new ArgumentNullException("identity");
            MHSConfigSection<T> section = _service.Open<T>(identity);
            MHSConfigReferenceSource registration = new MHSConfigReferenceSource
            {
                Identity = identity,
                Read = path =>
                {
                    using (MHSConfigSnapshot<T> snapshot = section.CaptureForTask("config-ref"))
                    {
                        string json;
                        bool exists = path.Length == 0;
                        if (exists) json = FCMJsonValueCodec.Serialize(snapshot.Get());
                        else exists = MVBConfigLayerPath.TryReadJson(snapshot.Get(), path, out json);
                        FCMConfigValuePresence kind = !exists ? FCMConfigValuePresence.Missing :
                            string.Equals(json, "null", StringComparison.Ordinal)
                            ? FCMConfigValuePresence.ExplicitNull :
                            snapshot.Version == "v1" ? FCMConfigValuePresence.Defaulted
                            : FCMConfigValuePresence.Present;
                        return new MHSConfigReferenceValue(new FCMConfigReference(identity, path),
                            json, snapshot.Version, kind, null);
                    }
                }
            };
            lock (_gate)
            {
                if (_sources.ContainsKey(identity.CanonicalKey))
                    throw new InvalidOperationException("CONFIG_REFERENCE_DUPLICATE_SOURCE: " + identity);
                _sources.Add(identity.CanonicalKey, registration);
            }
        }

        public void RegisterDependency(FCMConfigIdentity dependent, FCMConfigIdentity target)
        {
            if (dependent == null || target == null) throw new ArgumentNullException("reference identity");
            lock (_gate) _dependencies.Register(dependent.CanonicalKey, target.CanonicalKey);
        }

        public IList<string> DependencyOrder()
        { lock (_gate) return _dependencies.TopologicalOrder(); }

        public MHSConfigReferenceValue Resolve(FCMConfigReference reference)
        {
            if (reference == null) throw new ArgumentNullException("reference");
            MHSConfigReferenceSource source;
            lock (_gate) _sources.TryGetValue(reference.Target.CanonicalKey, out source);
            if (source == null)
                return Missing(reference, "CONFIG_REFERENCE_SOURCE_MISSING",
                    "Configuration target has not been registered");
            try
            {
                MHSConfigReferenceValue item = source.Read(reference.Path);
                if (item.HasValue)
                    return new MHSConfigReferenceValue(reference, item.Json, item.Version, item.Presence, null);
                return Missing(reference, "CONFIG_REFERENCE_PATH_MISSING", "Target property does not exist");
            }
            catch (Exception ex)
            {
                return new MHSConfigReferenceValue(reference, null, null,
                    FCMConfigValuePresence.Invalid, FCMConfigIssue.Error(reference.Path,
                        "CONFIG_REFERENCE_RESOLVE_FAILED", ex.Message));
            }
        }

        public void RegisterResourceProvider(IFCMConfigResourceReferenceProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.ResourceType))
                throw new ArgumentException("Provider requires a resource type");
            lock (_gate)
            {
                if (_resources.ContainsKey(provider.ResourceType))
                    throw new InvalidOperationException("CONFIG_RESOURCE_PROVIDER_DUPLICATE: " + provider.ResourceType);
                _resources.Add(provider.ResourceType, provider);
            }
        }
        public FCMConfigIssue ValidateResource(FCMConfigResourceRef reference)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.ResourceType) ||
                string.IsNullOrWhiteSpace(reference.ResourceId))
                return FCMConfigIssue.Error("", "CONFIG_RESOURCE_INVALID", "Resource reference is incomplete");
            IFCMConfigResourceReferenceProvider provider;
            lock (_gate) _resources.TryGetValue(reference.ResourceType, out provider);
            if (provider == null)
                return FCMConfigIssue.Error("", "CONFIG_RESOURCE_PROVIDER_MISSING",
                    "No resolver for resource type " + reference.ResourceType);
            try
            {
                string reason;
                return provider.Exists(reference.ResourceId, out reason) ? null :
                    FCMConfigIssue.Error("", "CONFIG_RESOURCE_NOT_FOUND", reason ?? reference.ResourceId);
            }
            catch (Exception ex)
            {
                return FCMConfigIssue.Error("", "CONFIG_RESOURCE_RESOLVE_FAILED", ex.Message);
            }
        }
        private static MHSConfigReferenceValue Missing(FCMConfigReference reference, string code, string reason)
        {
            return new MHSConfigReferenceValue(reference, null, null, FCMConfigValuePresence.Missing,
                reference.Optional ? null : FCMConfigIssue.Error(reference.Path, code, reason));
        }
    }
}
