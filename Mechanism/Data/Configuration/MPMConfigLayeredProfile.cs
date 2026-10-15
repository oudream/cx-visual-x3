using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CxVisual.Foundation.Data;

namespace CxVisual.Mechanism.Core
{
    /// <summary>L2配置层说明；优先级越大越晚覆盖；所有Layer共用原有L1持久化/版本体系。</summary>
    public sealed class MPMConfigLayerSpec
    {
        public MPMConfigLayerSpec(string id, int priority)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Layer ID required", "id");
            Id = id;
            Priority = priority;
        }
        public string Id { get; private set; }
        public int Priority { get; private set; }
    }

    /// <summary>仅保存显式覆盖字段（每个值为JSON）；未出现字段继续继承基础或模板。</summary>
    public sealed class MPMConfigLayerOverrides
    {
        public string TemplateId { get; set; }
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class MPMResolvedConfig<T> where T : class
    {
        public T Value { get; internal set; }
        public Dictionary<string, string> Origins { get; internal set; }
        public Dictionary<string, long> SourceRevisions { get; internal set; }
    }

    /// <summary>
    /// L2作用域/模板解析：公共ConfigManager仍是唯一注册、版本、保存入口。
    /// 模板仅引用已注册的覆盖层；每个专业仅声明自己的强类型T和业务Validator。
    /// </summary>
    public sealed class MPMConfigLayeredProfile<T> where T : class
    {
        private readonly MTCConfigManager _manager;
        private readonly string _baseId;
        private readonly MPMConfigLayerSpec[] _layers;
        private readonly Dictionary<string, MPMConfigLayerSpec> _known;
        private readonly Func<T, string> _validator;
        private readonly Dictionary<string, MPMConfigProperty> _properties;

        private MPMConfigLayeredProfile(MTCConfigManager manager, string baseId,
            MPMConfigLayerSpec[] layers, Func<T, string> validator)
        {
            _manager = manager;
            _baseId = baseId;
            _layers = layers.OrderBy(x => x.Priority).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
            _known = _layers.ToDictionary(x => x.Id, x => x, StringComparer.Ordinal);
            _validator = validator;
            _properties = MPMConfigDescription.Describe<T>()
                .ToDictionary(x => x.Name, x => x, StringComparer.Ordinal);
            if (_known.ContainsKey(baseId)) throw new ArgumentException("Base ID must be distinct from all layer IDs");
        }

        public static MPMConfigLayeredProfile<T> Register(MTCConfigManager manager, string baseId,
            Func<T, string> validator, params MPMConfigLayerSpec[] layers)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (layers == null || layers.Length == 0) throw new ArgumentException("At least one layer required", "layers");
            if (layers.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != layers.Length)
                throw new ArgumentException("Duplicate layer ID");
            MPMConfigLayeredProfile<T> result = new MPMConfigLayeredProfile<T>(manager, baseId, layers, validator);
            manager.Register<T>(baseId, validator: validator);
            foreach (MPMConfigLayerSpec layer in result._layers)
                manager.Register<MPMConfigLayerOverrides>(layer.Id);
            return result;
        }

        public MPMResolvedConfig<T> Resolve()
        {
            MPMResolvedConfig<T> result = ResolveWith(null, null);
            ValidateResolved(result);
            return result;
        }

        public void SetTemplate(string layerId, string templateId)
        {
            RequireLayer(layerId);
            if (!string.IsNullOrEmpty(templateId)) RequireLayer(templateId);
            MPMConfigLayerOverrides existing = _manager.Get<MPMConfigLayerOverrides>(layerId);
            MPMConfigLayerOverrides proposed = FCMConfigJsonFile.Clone(existing);
            proposed.TemplateId = string.IsNullOrEmpty(templateId) ? null : templateId;
            ValidateResolved(ResolveWith(layerId, proposed));
            long revision = _manager.GetRevision(layerId);
            _manager.UpdateAndSave<MPMConfigLayerOverrides>(layerId, x => x.TemplateId = proposed.TemplateId, revision);
        }

        public void UpdateLayer(string layerId, Action<T> change)
        {
            RequireLayer(layerId);
            if (change == null) throw new ArgumentNullException("change");
            // 编辑目标层的独立有效值，不能按最高优先级的最终值计算差异。
            // 否则低优先级字段被后续层遮蔽时，编辑可能悄然丢失。
            T before = ResolveWith(null, null, layerId).Value;
            T draft = FCMConfigJsonFile.Clone(before);
            change(draft);
            MPMConfigLayerOverrides overrideNow = _manager.Get<MPMConfigLayerOverrides>(layerId);
            MPMConfigLayerOverrides updated = FCMConfigJsonFile.Clone(overrideNow);
            if (updated.Values == null) updated.Values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (MPMConfigProperty property in _properties.Values)
            {
                object previous = property.Property.GetValue(before, null);
                object next = property.Property.GetValue(draft, null);
                if (FCMConfigJsonFile.Serialize(previous) != FCMConfigJsonFile.Serialize(next))
                    updated.Values[property.Name] = FCMConfigJsonFile.Serialize(next);
            }
            ValidateResolved(ResolveWith(layerId, updated));
            long revision = _manager.GetRevision(layerId);
            _manager.UpdateAndSave<MPMConfigLayerOverrides>(layerId, x =>
            {
                x.TemplateId = updated.TemplateId;
                x.Values = new Dictionary<string, string>(updated.Values, StringComparer.Ordinal);
            }, revision);
        }

        public void ClearProperty(string layerId, string propertyName)
        {
            RequireLayer(layerId);
            if (!_properties.ContainsKey(propertyName)) throw new ArgumentException("Unknown property", "propertyName");
            MPMConfigLayerOverrides updated = _manager.Get<MPMConfigLayerOverrides>(layerId);
            if (updated.Values == null || !updated.Values.Remove(propertyName)) return;
            ValidateResolved(ResolveWith(layerId, updated));
            long revision = _manager.GetRevision(layerId);
            _manager.UpdateAndSave<MPMConfigLayerOverrides>(layerId,
                x => x.Values.Remove(propertyName), revision);
        }

        private MPMResolvedConfig<T> ResolveWith(string substituteId, MPMConfigLayerOverrides substitute,
            string stopAfterLayerId = null)
        {
            MTCConfigSnapshot<T> baseSnapshot = _manager.GetSnapshot<T>(_baseId);
            T result = baseSnapshot.Value;
            Dictionary<string, string> origins = _properties.Keys
                .ToDictionary(x => x, x => _baseId, StringComparer.Ordinal);
            Dictionary<string, long> versions = new Dictionary<string, long>(StringComparer.Ordinal);
            versions.Add(_baseId, baseSnapshot.Revision);
            // 同一模板在一轮解析中只采样一次，避免值来自vN而记录成vN+1。
            Dictionary<string, MTCConfigSnapshot<MPMConfigLayerOverrides>> sampled =
                new Dictionary<string, MTCConfigSnapshot<MPMConfigLayerOverrides>>(StringComparer.Ordinal);
            foreach (MPMConfigLayerSpec layer in _layers)
            {
                ApplyLayer(layer.Id, result, origins, versions, new HashSet<string>(StringComparer.Ordinal),
                    substituteId, substitute, sampled);
                if (layer.Id == stopAfterLayerId) break;
            }
            return new MPMResolvedConfig<T>
            {
                Value = result, Origins = origins, SourceRevisions = versions
            };
        }

        private void ApplyLayer(string id, T value, Dictionary<string, string> origins,
            Dictionary<string, long> versions, HashSet<string> visiting,
            string substituteId, MPMConfigLayerOverrides substitute,
            Dictionary<string, MTCConfigSnapshot<MPMConfigLayerOverrides>> sampled)
        {
            RequireLayer(id);
            if (!visiting.Add(id)) throw new InvalidOperationException("CONFIG_TEMPLATE_CYCLE: " + id);
            MTCConfigSnapshot<MPMConfigLayerOverrides> snapshot;
            if (!sampled.TryGetValue(id, out snapshot))
            {
                snapshot = _manager.GetSnapshot<MPMConfigLayerOverrides>(id);
                sampled.Add(id, snapshot);
            }
            MPMConfigLayerOverrides layer = id == substituteId ? substitute : snapshot.Value;
            if (layer == null) throw new InvalidDataException("Missing template " + id);
            versions[id] = snapshot.Revision;
            if (!string.IsNullOrWhiteSpace(layer.TemplateId))
                ApplyLayer(layer.TemplateId, value, origins, versions, visiting, substituteId, substitute, sampled);
            if (layer.Values != null)
                foreach (KeyValuePair<string, string> pair in layer.Values)
                {
                    MPMConfigProperty property;
                    if (!_properties.TryGetValue(pair.Key, out property))
                        throw new InvalidDataException("CONFIG_UNKNOWN_PROPERTY: " + pair.Key);
                    try
                    {
                        object converted = FCMConfigJsonFile.Deserialize(pair.Value, property.PropertyType);
                        property.Property.SetValue(value, converted, null);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidDataException("Invalid layer property " + pair.Key + " in " + id, ex);
                    }
                    origins[pair.Key] = id;
                }
            visiting.Remove(id);
        }

        private void ValidateResolved(MPMResolvedConfig<T> resolved)
        {
            string message = MPMConfigDescription.Validate(resolved.Value);
            if (message == null && _validator != null) message = _validator(resolved.Value);
            if (!string.IsNullOrEmpty(message)) throw new ArgumentException("CONFIG_INVALID: " + message);
        }
        private void RequireLayer(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || !_known.ContainsKey(id))
                throw new ArgumentException("Unknown configuration layer: " + id);
        }
    }
}
