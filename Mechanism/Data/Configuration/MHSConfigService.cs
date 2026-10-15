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
        public MVBConfigScopeResolver ForMachine(string machineId)
        { return new MVBConfigScopeResolver(this, "machine", machineId); }
        public MVBConfigScopeResolver ForProject(string projectId)
        { return new MVBConfigScopeResolver(this, "project", projectId); }

        public int RegisterAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException("assembly");
            Type[] types = assembly.GetTypes();
            int registered = 0;
            lock (_gate)
            {
                foreach (Type type in types)
                {
                    if (type.IsAbstract || type.GetCustomAttributes(typeof(FCMConfigSectionAttribute), false).Length == 0) continue;
                    FCMConfigSchema schema = FCMConfigSchemaBuilder.Describe(type);
                    foreach (Type existing in _registered)
                    {
                        if (existing != type && FCMConfigSchemaBuilder.Describe(existing).SectionId == schema.SectionId)
                            throw new InvalidOperationException("Duplicate SectionId: " + schema.SectionId);
                    }
                    if (_registered.Add(type)) registered++;
                }
            }
            return registered;
        }
        internal MHSConfigSection<T> Open<T>(string scopeKind, string scopeId) where T : new()
        {
            FCMConfigSchema schema = FCMConfigSchemaBuilder.Describe<T>();
            string key = scopeKind + ":" + scopeId + ":" + schema.SectionId;
            lock (_gate)
            {
                object item;
                if (!_states.TryGetValue(key, out item))
                {
                    string folder = Path.Combine(_root, Safe(scopeKind + "_" + scopeId), Safe(schema.SectionId));
                    item = new MHSConfigSection<T>(folder, schema, key);
                    _states.Add(key, item);
                }
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