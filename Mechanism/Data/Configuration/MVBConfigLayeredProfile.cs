using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    // 只保存显式覆盖字段。其它字段绝不因 POCO 默认值而覆盖低优先级来源。
    [FCMConfigSection("Mechanism.LayerOverrides")]
    public sealed class MVBConfigLayerOverrides
    {
        [FCMConfigItem("显式覆盖", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
    }

    public sealed class MVBConfigLayerSpec
    {
        public MVBConfigLayerSpec(string name, string kind, string id, int priority)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Layer name, kind and id required");
            Name = name; Kind = kind; Id = id; Priority = priority;
        }
        public string Name { get; private set; }
        public string Kind { get; private set; }
        public string Id { get; private set; }
        public int Priority { get; private set; }
    }

    public sealed class MVBConfigLayerOrigin
    {
        internal MVBConfigLayerOrigin(string name, string path, string version)
        { Name = name; Path = path; Version = version; }
        public string Name { get; private set; }
        public string Path { get; private set; }
        public string Version { get; private set; }
    }

    public sealed class MVBConfigLayerValueInfo<V>
    {
        public bool HasValue { get; internal set; }
        public bool IsExplicitNull { get; internal set; }
        public V Value { get; internal set; }
        public string EffectiveSource { get; internal set; }
        public IList<MVBConfigLayerOrigin> SourceChain { get; internal set; }
    }

    public sealed class MVBConfigResolved<T> : IFCMConfigView<T> where T : new()
    {
        private readonly T _model;
        private readonly IList<MVBConfigLayerOrigin> _origins;
        internal MVBConfigResolved(T model, IList<MVBConfigLayerOrigin> origins, IDictionary<string, string> versions,
            FCMConfigRevisionVector revisionVector)
        {
            _model = Clone(model);
            RevisionVector = revisionVector ?? throw new ArgumentNullException("revisionVector");
            _origins = new List<MVBConfigLayerOrigin>(origins).AsReadOnly();
            Versions = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(versions, StringComparer.Ordinal));
        }
        public IDictionary<string, string> Versions { get; private set; }
        public FCMConfigRevisionVector RevisionVector { get; private set; }
        public T Get() { return Clone(_model); }
        public V Value<V>(Expression<Func<T, V>> selector)
        { return FCMJsonValueCodec.Deserialize<V>(FCMJsonValueCodec.Serialize(selector.Compile()(_model))); }
        public V Section<V>(Expression<Func<T, V>> selector) { return Value(selector); }
        public MVBConfigLayerValueInfo<V> Explain<V>(Expression<Func<T, V>> selector)
        {
            if (selector == null) throw new ArgumentNullException("selector");
            string path = FCMConfigPathUtil.FromExpression(selector);
            V value = default(V);
            bool present = true;
            try { value = Value(selector); }
            catch (KeyNotFoundException) { present = false; }
            catch (IndexOutOfRangeException) { present = false; }
            catch (ArgumentOutOfRangeException) { present = false; }
            catch (NullReferenceException) { present = false; }
            List<MVBConfigLayerOrigin> chain = _origins
                .Where(x => MVBConfigLayerPath.IsAncestor(x.Path, path)).ToList();
            return new MVBConfigLayerValueInfo<V>
            {
                HasValue = present, Value = value,
                IsExplicitNull = present && object.ReferenceEquals(value, null),
                EffectiveSource = !present ? null : (chain.Count == 0 ? "Default" : chain[chain.Count - 1].Name),
                SourceChain = chain.AsReadOnly()
            };
        }
        public FCMConfigValueState<V> ExplainState<V>(Expression<Func<T, V>> selector)
        {
            MVBConfigLayerValueInfo<V> info = Explain(selector);
            if (!info.HasValue) return FCMConfigValueState<V>.Missing();
            return FCMConfigValueState<V>.Of(info.Value, info.EffectiveSource,
                string.Equals(info.EffectiveSource, "Default", StringComparison.Ordinal));
        }
        private static T Clone(T model)
        { return FCMJsonValueCodec.Deserialize<T>(FCMJsonValueCodec.Serialize(model)); }
    }

    /// <summary>最终有效值的一次变化；不会把未生效的保存值冒充生效通知。</summary>
    public sealed class MVBConfigLayerChange<V>
    {
        public V OldValue { get; internal set; }
        public V NewValue { get; internal set; }
        public bool HasDefaultValue { get; internal set; }
        public V DefaultValue { get; internal set; }
        public string OldSource { get; internal set; }
        public string NewSource { get; internal set; }
        public IDictionary<string, string> OldVersions { get; internal set; }
        public IDictionary<string, string> NewVersions { get; internal set; }
    }

    /// <summary>一次任务固定整组有效配置与每层版本，后续保存和生效不会影响当前任务。</summary>
    public sealed class MVBConfigTaskSnapshot<T> : IFCMConfigView<T>, IDisposable where T : new()
    {
        private MVBConfigResolved<T> _fixed;
        internal MVBConfigTaskSnapshot(string taskId, MVBConfigResolved<T> result)
        { TaskId = taskId; _fixed = result; }
        public string TaskId { get; private set; }
        public IDictionary<string, string> Versions { get { return Active.Versions; } }
        public FCMConfigRevisionVector RevisionVector { get { return Active.RevisionVector; } }
        public T Get() { return Active.Get(); }
        public V Value<V>(Expression<Func<T, V>> selector) { return Active.Value(selector); }
        public V Section<V>(Expression<Func<T, V>> selector) { return Active.Section(selector); }
        public MVBConfigLayerValueInfo<V> Explain<V>(Expression<Func<T, V>> selector)
        { return Active.Explain(selector); }
        private MVBConfigResolved<T> Active
        {
            get
            {
                MVBConfigResolved<T> value = _fixed;
                if (value == null) throw new ObjectDisposedException("MVBConfigTaskSnapshot");
                return value;
            }
        }
        public void Dispose() { System.Threading.Interlocked.Exchange(ref _fixed, null); }
    }

    /// <summary>
    /// 基于已有 MHSConfigSection 存储/事务的多层显式覆盖。层级保存与生效严格分离。
    /// 一次 ResolveEffective 固定各层有效版本，跨层原子生效仍需 Runtime 专用协调。
    /// </summary>
    public sealed class MVBConfigLayeredProfile<T> where T : new()
    {
        private sealed class Layer
        {
            public MVBConfigLayerSpec Spec;
            public MHSConfigSection<MVBConfigLayerOverrides> Store;
        }
        private readonly List<Layer> _layers;
        public MVBConfigLayeredProfile(MHSConfigService service, params MVBConfigLayerSpec[] specs)
        {
            if (service == null) throw new ArgumentNullException("service");
            if (specs == null || specs.Length == 0 || specs.Any(x => x == null))
                throw new ArgumentException("Nonempty layers required", "specs");
            if (specs.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != specs.Length ||
                specs.Select(x => x.Priority).Distinct().Count() != specs.Length ||
                specs.Select(x => x.Kind + "\0" + x.Id).Distinct(StringComparer.Ordinal).Count() != specs.Length)
                throw new ArgumentException("Duplicate layer name, priority or scope");
            string modelSectionId = FCMConfigSchemaBuilder.Describe<T>().SectionId;
            _layers = specs.OrderBy(x => x.Priority).Select(x => new Layer {
                Spec = x, Store = service.ForScope(x.Kind, x.Id + "|" + modelSectionId)
                    .Open<MVBConfigLayerOverrides>()
            }).ToList();
        }
        public MHSConfigSection<MVBConfigLayerOverrides> GetLayer(string name) { return Find(name).Store; }
        /// <summary>用于长时间采图、重建、检测任务的配置版本隔离。</summary>
        public MVBConfigTaskSnapshot<T> CaptureForTask(string taskId)
        {
            if (string.IsNullOrWhiteSpace(taskId)) throw new ArgumentException("TaskId required", "taskId");
            return new MVBConfigTaskSnapshot<T>(taskId, ResolveEffective());
        }

        /// <summary>订阅全部来源层的最终有效参数变化，用户不必重复监听每一层。</summary>
        public IDisposable Subscribe<V>(Expression<Func<T, V>> selector, Action<MVBConfigLayerChange<V>> observer)
        {
            if (selector == null) throw new ArgumentNullException("selector");
            if (observer == null) throw new ArgumentNullException("observer");
            return new LayerSubscription<V>(this, selector, observer);
        }

        private sealed class LayerSubscription<V> : IDisposable
        {
            private readonly object _gate = new object();
            private readonly MVBConfigLayeredProfile<T> _profile;
            private readonly Expression<Func<T, V>> _selector;
            private readonly Action<MVBConfigLayerChange<V>> _observer;
            private readonly List<IDisposable> _listeners = new List<IDisposable>();
            private MVBConfigResolved<T> _last;
            private bool _disposed;
            internal LayerSubscription(MVBConfigLayeredProfile<T> profile,
                Expression<Func<T, V>> selector, Action<MVBConfigLayerChange<V>> observer)
            {
                _profile = profile; _selector = selector; _observer = observer;
                _last = profile.ResolveEffective();
                try
                {
                    foreach (Layer layer in profile._layers)
                        _listeners.Add(layer.Store.SubscribeAll(FCMConfigChangeStage.Effective, change => Changed()));
                    Changed(); // close startup race between initial capture and event subscriptions
                }
                catch { Dispose(); throw; }
            }
            private void Changed()
            {
                MVBConfigLayerChange<V> output = null;
                lock (_gate)
                {
                    if (_disposed) return;
                    MVBConfigResolved<T> next = _profile.ResolveEffective();
                    MVBConfigLayerValueInfo<V> oldValue = _last.Explain(_selector);
                    MVBConfigLayerValueInfo<V> newValue = next.Explain(_selector);
                    if (oldValue.HasValue != newValue.HasValue ||
                        FCMJsonValueCodec.Serialize(oldValue.Value) != FCMJsonValueCodec.Serialize(newValue.Value) ||
                        oldValue.EffectiveSource != newValue.EffectiveSource)
                    {
                        V originalDefault = default(V);
                        bool hasDefault = true;
                        try { originalDefault = _selector.Compile()(new T()); }
                        catch (KeyNotFoundException) { hasDefault = false; }
                        catch (NullReferenceException) { hasDefault = false; }
                        output = new MVBConfigLayerChange<V>
                        {
                            OldValue = oldValue.Value, NewValue = newValue.Value,
                            OldSource = oldValue.EffectiveSource, NewSource = newValue.EffectiveSource,
                            OldVersions = _last.Versions, NewVersions = next.Versions,
                            HasDefaultValue = hasDefault,
                            DefaultValue = hasDefault ? FCMJsonValueCodec.Deserialize<V>(
                                FCMJsonValueCodec.Serialize(originalDefault)) : default(V)
                        };
                    }
                    _last = next;
                }
                // User callbacks are always outside the internal subscription lock.
                if (output != null) _observer(output);
            }
            public void Dispose()
            {
                lock (_gate)
                {
                    if (_disposed) return;
                    _disposed = true;
                    foreach (IDisposable listener in _listeners) listener.Dispose();
                    _listeners.Clear();
                }
            }
        }


        public async Task<MHSConfigCommitResult> SetOverrideAsync<V>(string layerName,
            Expression<Func<T, V>> selector, V value, CancellationToken token)
        {
            string path = FCMConfigPathUtil.FromExpression(selector);
            string json = FCMJsonValueCodec.Serialize(value);
            MVBConfigLayerPath.Validate<T>(path, json);
            using (MHSConfigEdit<MVBConfigLayerOverrides> edit = Find(layerName).Store.BeginEdit("配置层覆盖：" + path))
            {
                edit.WorkingCopy.Values[path] = json;
                return await edit.CommitAsync(token).ConfigureAwait(false);
            }
        }

        public async Task<MHSConfigCommitResult> RemoveOverrideAsync<V>(string layerName,
            Expression<Func<T, V>> selector, CancellationToken token)
        {
            string path = FCMConfigPathUtil.FromExpression(selector);
            using (MHSConfigEdit<MVBConfigLayerOverrides> edit = Find(layerName).Store.BeginEdit("移除覆盖：" + path))
            {
                edit.WorkingCopy.Values.Remove(path);
                return await edit.CommitAsync(token).ConfigureAwait(false);
            }
        }

        public MVBConfigResolved<T> ResolveEffective()
        {
            for (int attempt = 0; attempt < 4; ++attempt)
            {
                List<Tuple<Layer, string, Dictionary<string, string>>> snapshots =
                    new List<Tuple<Layer, string, Dictionary<string, string>>>();
                foreach (Layer layer in _layers)
                {
                    using (MHSConfigSnapshot<MVBConfigLayerOverrides> shot =
                        layer.Store.CaptureForTask("layer-" + Guid.NewGuid().ToString("N")))
                    {
                        snapshots.Add(Tuple.Create(layer, shot.Version, shot.Get().Values));
                    }
                }
                if (snapshots.Any(x => x.Item1.Store.EffectiveVersion != x.Item2)) continue;
                T model = new T();
                List<MVBConfigLayerOrigin> origins = new List<MVBConfigLayerOrigin>();
                Dictionary<string, string> versions = new Dictionary<string, string>(StringComparer.Ordinal);
                List<FCMConfigRevision> revisions = new List<FCMConfigRevision>();
                foreach (Tuple<Layer, string, Dictionary<string, string>> layer in snapshots)
                {
                    versions.Add(layer.Item1.Spec.Name, layer.Item2);
                    revisions.Add(new FCMConfigRevision(layer.Item1.Store.Identity, layer.Item2, layer.Item2, 1));
                    foreach (KeyValuePair<string, string> item in layer.Item3
                        .OrderBy(x => MVBConfigLayerPath.Depth(x.Key)).ThenBy(x => x.Key, StringComparer.Ordinal))
                    {
                        MVBConfigLayerPath.Apply(model, item.Key, item.Value);
                        origins.Add(new MVBConfigLayerOrigin(layer.Item1.Spec.Name, item.Key, layer.Item2));
                    }
                }
                FCMConfigValidationReport report = FCMConfigValidationEngine.Validate(model, "layered");
                if (!report.IsValid)
                    throw new InvalidOperationException("CONFIG_LAYER_INVALID: " +
                        string.Join("; ", report.Issues.Where(x => x.Severity == FCMConfigIssueSeverity.Error)
                            .Select(x => x.Path + ":" + x.Message)));
                if (snapshots.Any(x => x.Item1.Store.EffectiveVersion != x.Item2)) continue;
                return new MVBConfigResolved<T>(model, origins, versions, new FCMConfigRevisionVector(revisions));
            }
            throw new InvalidOperationException("CONFIG_LAYER_VERSION_CONFLICT: source versions changed during resolution");
        }
        private Layer Find(string name)
        {
            Layer layer = _layers.FirstOrDefault(x => x.Spec.Name == name);
            if (layer == null) throw new KeyNotFoundException("Unknown layer: " + name);
            return layer;
        }
    }

    internal static class MVBConfigLayerPath
    {
        /// <summary>提取单路径 JSON。不存在返回 false，显式 null 返回 true + "null"。</summary>
        internal static bool TryReadJson(object model, string path, out string json)
        {
            json = null;
            try
            {
                object current = model;
                foreach (object key in Split(path))
                    current = Read(current, key);
                json = FCMJsonValueCodec.Serialize(current);
                return true;
            }
            catch (KeyNotFoundException) { return false; }
            catch (IndexOutOfRangeException) { return false; }
            catch (ArgumentOutOfRangeException) { return false; }
            catch (NullReferenceException) { return false; }
        }
        internal static bool IsAncestor(string ancestor, string path)
        {
            return ancestor == path || (path.StartsWith(ancestor, StringComparison.Ordinal) &&
                path.Length > ancestor.Length && (path[ancestor.Length] == '.' || path[ancestor.Length] == '['));
        }
        internal static int Depth(string path) { return Split(path).Count; }
        internal static void Validate<T>(string path, string json)
        { FCMJsonValueCodec.Deserialize(json, TargetType(typeof(T), Split(path))); }
        internal static void Apply<T>(T model, string path, string json)
        {
            List<object> keys = Split(path);
            Type target = TargetType(typeof(T), keys);
            object value = FCMJsonValueCodec.Deserialize(json, target);
            object parent = model;
            for (int i = 0; i < keys.Count - 1; i++) parent = Read(parent, keys[i]);
            if (parent == null) throw new InvalidOperationException("Null config subtree: " + path);
            IDictionary dict = parent as IDictionary;
            if (dict != null) { dict[keys[keys.Count - 1]] = value; return; }
            IList list = parent as IList;
            if (list != null) { list[(int)keys[keys.Count - 1]] = value; return; }
            PropertyInfo prop = parent.GetType().GetProperty((string)keys[keys.Count - 1]);
            if (prop == null || !prop.CanWrite) throw new InvalidOperationException("Invalid config path: " + path);
            prop.SetValue(parent, value, null);
        }
        private static object Read(object owner, object key)
        {
            if (owner == null) throw new InvalidOperationException("Null config parent");
            IDictionary dict = owner as IDictionary;
            if (dict != null)
            {
                if (!dict.Contains(key)) throw new KeyNotFoundException("Unknown dict key: " + key);
                return dict[key];
            }
            IList list = owner as IList;
            if (list != null) return list[(int)key];
            PropertyInfo prop = owner.GetType().GetProperty((string)key);
            if (prop == null || !prop.CanRead) throw new InvalidOperationException("Invalid config member " + key);
            return prop.GetValue(owner, null);
        }
        private static Type TargetType(Type type, IList<object> keys)
        {
            foreach (object key in keys)
            {
                if (key is int)
                {
                    if (type.IsArray) type = type.GetElementType();
                    else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                        type = type.GetGenericArguments()[0];
                    else throw new NotSupportedException("Only arrays and List<T> accept indices");
                }
                else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    if (type.GetGenericArguments()[0] != typeof(string))
                        throw new NotSupportedException("Only string dictionary keys");
                    type = type.GetGenericArguments()[1];
                }
                else
                {
                    PropertyInfo p = type.GetProperty((string)key);
                    if (p == null || !p.CanRead || !p.CanWrite)
                        throw new InvalidOperationException("Unknown or readonly config member " + key);
                    type = p.PropertyType;
                }
            }
            return type;
        }
        private static List<object> Split(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Root override is not allowed");
            List<object> keys = new List<object>();
            for (int i = 0; i < path.Length;)
            {
                if (path[i] == '.')
                {
                    i++;
                    if (i == path.Length) throw new FormatException("Trailing dot");
                }
                if (path[i] == '[')
                {
                    ++i;
                    if (i < path.Length && path[i] == '"')
                    {
                        int start = i++;
                        bool escape = false;
                        while (i < path.Length)
                        {
                            char c = path[i++];
                            if (escape) { escape = false; continue; }
                            if (c == '\\') { escape = true; continue; }
                            if (c == '"') break;
                        }
                        if (i >= path.Length || path[i] != ']') throw new FormatException("Invalid quoted index");
                        keys.Add(FCMJsonValueCodec.Deserialize<string>(path.Substring(start, i - start)));
                        i++;
                    }
                    else
                    {
                        int start = i;
                        while (i < path.Length && char.IsDigit(path[i])) i++;
                        int index;
                        if (start == i || i == path.Length || path[i] != ']' ||
                            !int.TryParse(path.Substring(start, i - start), NumberStyles.None,
                                CultureInfo.InvariantCulture, out index)) throw new FormatException("Invalid index");
                        keys.Add(index); i++;
                    }
                }
                else
                {
                    int start = i;
                    while (i < path.Length && path[i] != '.' && path[i] != '[') i++;
                    if (i == start) throw new FormatException("Empty member");
                    keys.Add(path.Substring(start, i - start));
                }
            }
            return keys;
        }
    }
}
