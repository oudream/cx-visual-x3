using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CxVisual.Foundation.Data;

namespace CxVisual.Mechanism.Core
{
    /// <summary>L2配置层说明；优先级越大越晚覆盖；所有Layer共用原有L1持久化/版本体系。</summary>
    public sealed class CxConfigLayerSpec
    {
        public CxConfigLayerSpec(string id, int priority)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Layer ID required", "id");
            Id = id;
            Priority = priority;
        }
        public string Id { get; private set; }
        public int Priority { get; private set; }
    }

    /// <summary>仅保存显式覆盖字段（每个值为JSON）；未出现字段继续继承基础或模板。</summary>
    public sealed class CxConfigLayerOverrides
    {
        public string TemplateId { get; set; }
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public sealed class CxResolvedConfig<T> where T : class
    {
        public T Value { get; internal set; }
        public Dictionary<string, string> Origins { get; internal set; }
        public Dictionary<string, long> SourceRevisions { get; internal set; }
    }

    /// <summary>
    /// L2作用域/模板解析：公共ConfigManager仍是唯一注册、版本、保存入口。
    /// 模板仅引用已注册的覆盖层；每个专业仅声明自己的强类型T和业务Validator。
    /// </summary>
    public sealed class CxConfigLayeredProfile<T> where T : class
    {
        private readonly CxConfigManager _manager;
        private readonly string _baseId;
        private readonly CxConfigLayerSpec[] _layers;
        private readonly Dictionary<string, CxConfigLayerSpec> _known;
        private readonly Func<T, string> _validator;
        private readonly Dictionary<string, CxConfigProperty> _properties;

        private CxConfigLayeredProfile(CxConfigManager manager, string baseId,
            CxConfigLayerSpec[] layers, Func<T, string> validator)
        {
            _manager = manager;
            _baseId = baseId;
            _layers = layers.OrderBy(x => x.Priority).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
            _known = _layers.ToDictionary(x => x.Id, x => x, StringComparer.Ordinal);
            _validator = validator;
            _properties = CxConfigDescription.Describe<T>()
                .ToDictionary(x => x.Name, x => x, StringComparer.Ordinal);
            if (_known.ContainsKey(baseId)) throw new ArgumentException("Base ID must be distinct from all layer IDs");
        }

        public static CxConfigLayeredProfile<T> Register(CxConfigManager manager, string baseId,
            Func<T, string> validator, params CxConfigLayerSpec[] layers)
        {
            if (manager == null) throw new ArgumentNullException("manager");
            if (layers == null || layers.Length == 0) throw new ArgumentException("At least one layer required", "layers");
            if (layers.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != layers.Length)
                throw new ArgumentException("Duplicate layer ID");
            CxConfigLayeredProfile<T> result = new CxConfigLayeredProfile<T>(manager, baseId, layers, validator);
            manager.Register<T>(baseId, validator: validator);
            foreach (CxConfigLayerSpec layer in result._layers)
                manager.Register<CxConfigLayerOverrides>(layer.Id);
            return result;
        }

        public CxResolvedConfig<T> Resolve()
        {
            CxResolvedConfig<T> result = ResolveWith(null, null);
            ValidateResolved(result);
            return result;
        }

        public void SetTemplate(string layerId, string templateId)
        {
            RequireLayer(layerId);
            if (!string.IsNullOrEmpty(templateId)) RequireLayer(templateId);
            CxConfigLayerOverrides existing = _manager.Get<CxConfigLayerOverrides>(layerId);
            CxConfigLayerOverrides proposed = CxConfigJsonFile.Clone(existing);
            proposed.TemplateId = string.IsNullOrEmpty(templateId) ? null : templateId;
            ValidateResolved(ResolveWith(layerId, proposed));
            long revision = _manager.GetRevision(layerId);
            _manager.UpdateAndSave<CxConfigLayerOverrides>(layerId, x => x.TemplateId = proposed.TemplateId, revision);
        }

        public void UpdateLayer(string layerId, Action<T> change)
        {
            RequireLayer(layerId);
            if (change == null) throw new ArgumentNullException("change");
            // 编辑目标层的独立有效值，不能按最高优先级的最终值计算差异。
            // 否则低优先级字段被后续层遮蔽时，编辑可能悄然丢失。
            T before = ResolveWith(null, null, layerId).Value;
            T draft = CxConfigJsonFile.Clone(before);
            change(draft);
            CxConfigLayerOverrides overrideNow = _manager.Get<CxConfigLayerOverrides>(layerId);
            CxConfigLayerOverrides updated = CxConfigJsonFile.Clone(overrideNow);
            if (updated.Values == null) updated.Values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (CxConfigProperty property in _properties.Values)
            {
                object previous = property.Property.GetValue(before, null);
                object next = property.Property.GetValue(draft, null);
                if (CxConfigJsonFile.Serialize(previous) != CxConfigJsonFile.Serialize(next))
                    updated.Values[property.Name] = CxConfigJsonFile.Serialize(next);
            }
            ValidateResolved(ResolveWith(layerId, updated));
            long revision = _manager.GetRevision(layerId);
            _manager.UpdateAndSave<CxConfigLayerOverrides>(layerId, x =>
            {
                x.TemplateId = updated.TemplateId;
                x.Values = new Dictionary<string, string>(updated.Values, StringComparer.Ordinal);
            }, revision);
        }

        public void ClearProperty(string layerId, string propertyName)
        {
            RequireLayer(layerId);
            if (!_properties.ContainsKey(propertyName)) throw new ArgumentException("Unknown property", "propertyName");
            CxConfigLayerOverrides updated = _manager.Get<CxConfigLayerOverrides>(layerId);
            if (updated.Values == null || !updated.Values.Remove(propertyName)) return;
            ValidateResolved(ResolveWith(layerId, updated));
            long revision = _manager.GetRevision(layerId);
            _manager.UpdateAndSave<CxConfigLayerOverrides>(layerId,
                x => x.Values.Remove(propertyName), revision);
        }

        private CxResolvedConfig<T> ResolveWith(string substituteId, CxConfigLayerOverrides substitute,
            string stopAfterLayerId = null)
        {
            T result = _manager.Get<T>(_baseId);
            Dictionary<string, string> origins = _properties.Keys
                .ToDictionary(x => x, x => _baseId, StringComparer.Ordinal);
            Dictionary<string, long> versions = new Dictionary<string, long>(StringComparer.Ordinal);
            versions.Add(_baseId, _manager.GetRevision(_baseId));
            foreach (CxConfigLayerSpec layer in _layers)
            {
                ApplyLayer(layer.Id, result, origins, versions, new HashSet<string>(StringComparer.Ordinal),
                    substituteId, substitute);
                if (layer.Id == stopAfterLayerId) break;
            }
            return new CxResolvedConfig<T>
            {
                Value = result, Origins = origins, SourceRevisions = versions
            };
        }

        private void ApplyLayer(string id, T value, Dictionary<string, string> origins,
            Dictionary<string, long> versions, HashSet<string> visiting,
            string substituteId, CxConfigLayerOverrides substitute)
        {
            RequireLayer(id);
            if (!visiting.Add(id)) throw new InvalidOperationException("CONFIG_TEMPLATE_CYCLE: " + id);
            CxConfigLayerOverrides layer = id == substituteId ? substitute : _manager.Get<CxConfigLayerOverrides>(id);
            if (layer == null) throw new InvalidDataException("Missing template " + id);
            versions[id] = _manager.GetRevision(id);
            if (!string.IsNullOrWhiteSpace(layer.TemplateId))
                ApplyLayer(layer.TemplateId, value, origins, versions, visiting, substituteId, substitute);
            if (layer.Values != null)
                foreach (KeyValuePair<string, string> pair in layer.Values)
                {
                    CxConfigProperty property;
                    if (!_properties.TryGetValue(pair.Key, out property))
                        throw new InvalidDataException("CONFIG_UNKNOWN_PROPERTY: " + pair.Key);
                    try
                    {
                        object converted = CxConfigJsonFile.Deserialize(pair.Value, property.PropertyType);
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

        private void ValidateResolved(CxResolvedConfig<T> resolved)
        {
            string message = CxConfigDescription.Validate(resolved.Value);
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
