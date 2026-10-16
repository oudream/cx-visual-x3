using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    internal interface IMHSConfigInstance
    {
        FCMConfigIdentity Identity { get; }
        Type ConfigType { get; }
    }

    public sealed class MHSConfigService
    {
        private readonly string _root;
        private readonly Dictionary<string, object> _states = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly object _gate = new object();
        private readonly HashSet<Type> _registered = new HashSet<Type>();

        public MHSConfigService(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory)) throw new ArgumentException("Missing storage root.", "rootDirectory");
            _root = Path.GetFullPath(rootDirectory);
        }
        /// <summary>安全会话入口，非可信模块只能持有返回的会话，不能获得原始服务。</summary>
        public MHSConfigAuthorizedSession Authorize(FCMConfigPrincipal principal, IFCMConfigAccessPolicy policy)
        {
            if (principal == null) throw new ArgumentNullException("principal");
            if (policy == null) throw new ArgumentNullException("policy");
            return new MHSConfigAuthorizedSession(this, principal, policy);
        }
        public MVBConfigScopeResolver ForMachine(string machineId)
        { return new MVBConfigScopeResolver(this, "machine", machineId); }
        public MVBConfigScopeResolver ForProject(string projectId)
        { return new MVBConfigScopeResolver(this, "project", projectId); }
        /// <summary>物理单实例聚合事务：一次提交/生效多种配置，不逐个写不同文件。</summary>
        public MHSConfigAtomicGroup OpenAtomicGroup(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId)) throw new ArgumentException("GroupId required", "groupId");
            return new MHSConfigAtomicGroup(ForScope("config-group", groupId).Open<MHSConfigGroupDocument>());
        }

        public MHSConfigSection<T> Open<T>(FCMConfigIdentity identity) where T : new()
        {
            if (identity == null) throw new ArgumentNullException("identity");
            if (identity.SectionId != FCMConfigSchemaBuilder.Describe<T>().SectionId)
                throw new InvalidOperationException("CONFIG_IDENTITY_SECTION_MISMATCH");
            return Open<T>(identity.ScopeKind, identity.ScopeId);
        }
        /// <summary>供显式多层配置使用；按范围类型与身份隔离原有配置存储。</summary>
        public MVBConfigScopeResolver ForScope(string scopeKind, string scopeId)
        {
            if (string.IsNullOrWhiteSpace(scopeKind) || scopeKind.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
                throw new ArgumentException("Invalid scope kind", "scopeKind");
            return new MVBConfigScopeResolver(this, scopeKind, scopeId);
        }

        /// <summary>插件程序集配置发现：先全部校验，再原子注册。</summary>
        public int RegisterAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException("assembly");
            Type[] types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract &&
                    t.GetCustomAttributes(typeof(FCMConfigSectionAttribute), false).Length > 0)
                .OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
            Dictionary<string, Type> candidates = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (Type type in types)
            {
                FCMConfigSchema schema = FCMConfigSchemaBuilder.Describe(type);
                Type existing;
                if (candidates.TryGetValue(schema.SectionId, out existing) && existing != type)
                    throw new InvalidOperationException("Duplicate SectionId in assembly: " + schema.SectionId);
                candidates[schema.SectionId] = type;
            }
            lock (_gate)
            {
                foreach (KeyValuePair<string, Type> candidate in candidates)
                {
                    Type registered = _registered.FirstOrDefault(t =>
                        FCMConfigSchemaBuilder.Describe(t).SectionId == candidate.Key);
                    if (registered != null && registered != candidate.Value)
                        throw new InvalidOperationException("Duplicate SectionId: " + candidate.Key);
                    foreach (IMHSConfigInstance active in _states.Values.Cast<IMHSConfigInstance>())
                    {
                        if (active.Identity.SectionId == candidate.Key && active.ConfigType != candidate.Value)
                            throw new InvalidOperationException("Active section conflicts: " + candidate.Key);
                    }
                }
                int count = 0;
                foreach (Type type in types)
                    if (_registered.Add(type)) count++;
                return count;
            }
        }

        /// <summary>注册表快照，供 Studio 查询、定位及自动文档生成。</summary>
        public IList<FCMConfigSchema> GetRegisteredSections()
        {
            lock (_gate)
                return _registered.Select(FCMConfigSchemaBuilder.Describe)
                    .OrderBy(s => s.SectionId, StringComparer.Ordinal).ToArray();
        }

        public FCMConfigSchema FindRegisteredSection(string sectionId)
        {
            if (string.IsNullOrWhiteSpace(sectionId)) throw new ArgumentException("Missing SectionId", "sectionId");
            lock (_gate)
                return _registered.Select(FCMConfigSchemaBuilder.Describe)
                    .FirstOrDefault(s => s.SectionId == sectionId);
        }

        /// <summary>已有活跃配置实例时拒绝卸载，避免悬空类型和失效缓存。</summary>
        public int UnregisterAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException("assembly");
            lock (_gate)
            {
                Type[] matched = _registered.Where(t => t.Assembly == assembly).ToArray();
                foreach (Type type in matched)
                {
                    string sectionId = FCMConfigSchemaBuilder.Describe(type).SectionId;
                    if (_states.Values.Cast<IMHSConfigInstance>().Any(x => x.Identity.SectionId == sectionId))
                        throw new InvalidOperationException("CONFIG_REGISTRY_SECTION_IN_USE: " + sectionId);
                }
                foreach (Type type in matched) _registered.Remove(type);
                return matched.Length;
            }
        }

        internal MHSConfigSection<T> Open<T>(string scopeKind, string scopeId) where T : new()
        {
            FCMConfigSchema schema = FCMConfigSchemaBuilder.Describe<T>();
            FCMConfigIdentity identity = new FCMConfigIdentity(schema.SectionId, scopeKind, scopeId);
            string key = identity.CanonicalKey;
            lock (_gate)
            {
                Type registered = _registered.FirstOrDefault(t =>
                    FCMConfigSchemaBuilder.Describe(t).SectionId == schema.SectionId);
                if (registered != null && registered != typeof(T))
                    throw new InvalidOperationException("CONFIG_REGISTRY_TYPE_CONFLICT: " + schema.SectionId);
                object item;
                if (!_states.TryGetValue(key, out item))
                {
                    string folder = Path.Combine(_root, Safe(identity.ScopeKind + ":" + identity.ScopeId), Safe(schema.SectionId));
                    item = new MHSConfigSection<T>(folder, schema, key, identity);
                    _states.Add(key, item);
                }
                IMHSConfigInstance active = (IMHSConfigInstance)item;
                if (active.ConfigType != typeof(T))
                    throw new InvalidOperationException("CONFIG_IDENTITY_TYPE_CONFLICT: " + identity);
                return (MHSConfigSection<T>)item;
            }
        }
        private static string Safe(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 24).ToLowerInvariant();
            }
        }
    }

    // This is a scope selector, not a second independent configuration service.
    public sealed class MVBConfigScopeResolver
    {
        private readonly MHSConfigService _service;
        private readonly string _kind;
        private readonly string _id;
        internal MVBConfigScopeResolver(MHSConfigService service, string kind, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Missing scope identity.", "id");
            _service = service; _kind = kind; _id = id;
        }
        public MHSConfigSection<T> Open<T>() where T : new() { return _service.Open<T>(_kind, _id); }
    }
}