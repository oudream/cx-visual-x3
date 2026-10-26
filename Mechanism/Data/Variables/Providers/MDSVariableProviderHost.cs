using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CxVisual.Mechanism.Data.Variables
{
    // Provider属于数据来源和生命周期，不拥有独立变量仓库。
    public interface IMDSVariableProvider : IDisposable
    {
        string ProviderId { get; }
        void Start(Action<MDSVariableProviderSample> publish);
        void Stop();
    }

    public sealed class MDSVariableProviderSample
    {
        private readonly object _value;
        public MDSVariableProviderSample(string pointId, object value, MDSVariableQuality quality = MDSVariableQuality.Good)
        {
            if (string.IsNullOrWhiteSpace(pointId)) throw new ArgumentException("点位名不能为空", "pointId");
            if (!Enum.IsDefined(typeof(MDSVariableQuality), quality)) throw new ArgumentOutOfRangeException("quality");
            PointId = pointId.Trim(); _value = MDSVariableValueRules.Copy(value); Quality = quality;
        }
        public string PointId { get; private set; }
        public object Value { get { return MDSVariableValueRules.Copy(_value); } }
        public MDSVariableQuality Quality { get; private set; }
    }

    public sealed class MDSVariableProviderBinding
    {
        internal readonly MDSVariableBase Target;
        internal MDSVariableProviderBinding(string providerId, string pointId, string scopeId,
            string variableName, MDSVariableBase target)
        {
            ProviderId = providerId; PointId = pointId; ScopeId = scopeId;
            VariableName = variableName; Target = target;
        }
        public string ProviderId { get; private set; }
        public string PointId { get; private set; }
        public string ScopeId { get; private set; }
        public string VariableName { get; private set; }
        public string VariableId { get { return Target.EntryDefinition.VariableId; } }
    }

    public sealed class MDSVariableProviderHealth
    {
        public bool Running { get; internal set; }
        public long Received { get; internal set; }
        public long Applied { get; internal set; }
        public long Rejected { get; internal set; }
        public string LastError { get; internal set; }
    }

    /// <summary>对x2 DeviceVariableProvider/RuntimeHost做x3兼容迁移：唯一MDS Owner写入。</summary>
    public sealed class MDSVariableProviderHost : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MDSVariableRegistry _registry;
        private readonly Dictionary<string, IMDSVariableProvider> _providers =
            new Dictionary<string, IMDSVariableProvider>(StringComparer.Ordinal);
        private readonly Dictionary<string, MDSVariableProviderBinding> _bindings =
            new Dictionary<string, MDSVariableProviderBinding>(StringComparer.Ordinal);
        private bool _running, _disposed;
        private int _generation;
        private long _received, _applied, _rejected;
        private string _lastError;

        public MDSVariableProviderHost(MDSVariableRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException("registry");
        }
        private static string Key(string provider, string point)
        {
            return provider + "\n" + point;
        }
        public void Register(IMDSVariableProvider provider)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.ProviderId))
                throw new ArgumentException("Provider及身份不能为空", "provider");
            lock (_gate)
            {
                RequireOpen();
                if (_running) throw new InvalidOperationException("运行中不能改Provider注册");
                if (_providers.ContainsKey(provider.ProviderId)) throw new ArgumentException("Provider重复");
                _providers.Add(provider.ProviderId, provider);
            }
        }
        public MDSVariableProviderBinding Bind(string providerId, string pointId, string scopeId, string variableName)
        {
            if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(pointId) ||
                string.IsNullOrWhiteSpace(scopeId) || string.IsNullOrWhiteSpace(variableName))
                throw new ArgumentException("Provider映射引用无效");
            lock (_gate)
            {
                RequireOpen();
                if (_running) throw new InvalidOperationException("运行中不能更改映射");
                if (!_providers.ContainsKey(providerId)) throw new KeyNotFoundException("Provider不存在");
                MDSVariableTable table = _registry.Find(scopeId);
                if (table == null) throw new KeyNotFoundException("变量Scope不存在");
                MDSVariableBase variable = table.ResolveEntry(variableName);
                string key = Key(providerId, pointId);
                if (_bindings.ContainsKey(key)) throw new ArgumentException("映射已存在: " + key);
                MDSVariableProviderBinding binding = new MDSVariableProviderBinding(providerId,
                    pointId, scopeId, variableName, variable);
                _bindings.Add(key, binding);
                return binding;
            }
        }
        public MDSVariableProviderBinding[] Bindings()
        {
            lock (_gate) return _bindings.Values.ToArray();
        }
        public void Start()
        {
            IMDSVariableProvider[] sources;
            int generation;
            lock (_gate)
            {
                RequireOpen();
                if (_running) throw new InvalidOperationException("Provider Host已启动");
                _running = true; generation = ++_generation;
                sources = _providers.Values.ToArray();
            }
            List<IMDSVariableProvider> started = new List<IMDSVariableProvider>();
            try
            {
                foreach (IMDSVariableProvider provider in sources)
                {
                    string providerId = provider.ProviderId;
                    // Provider只能写入为它显式授权的Point -> MDS绑定。
                    provider.Start(sample => Accept(providerId, generation, sample));
                    started.Add(provider);
                }
            }
            catch
            {
                lock (_gate) { _running = false; _generation++; }
                foreach (IMDSVariableProvider provider in started)
                    try { provider.Stop(); } catch { }
                throw;
            }
        }
        private void Accept(string source, int generation, MDSVariableProviderSample sample)
        {
            if (sample == null) return;
            MDSVariableProviderBinding binding;
            lock (_gate)
            {
                if (_disposed || !_running || generation != _generation) return;
                _received++;
                if (!_bindings.TryGetValue(Key(source, sample.PointId), out binding))
                {
                    _rejected++; _lastError = "未授权或不存在的Provider Point映射";
                    return;
                }
            }
            try
            {
                // 真实变量对象可供只读业务定义从设备Provider刷新；不要求调用方把它改成Writable。
                binding.Target.RefreshProviderBoxed(sample.Value, sample.Quality, source);
                Interlocked.Increment(ref _applied);
            }
            catch (Exception error)
            {
                Interlocked.Increment(ref _rejected);
                lock (_gate) _lastError = error.GetType().Name + ": " + error.Message;
            }
        }
        // 供固定Point百万级热路径一次绑定；以后每次采样不经过字符串/反射/目录查找。
        public MDSVariableTypedPointBinding<T> ResolveTyped<T>(string providerId, string pointId)
        {
            lock (_gate)
            {
                RequireOpen();
                MDSVariableProviderBinding binding;
                if (!_bindings.TryGetValue(Key(providerId, pointId), out binding))
                    throw new KeyNotFoundException("Provider Point映射不存在");
                MDSVariable<T> typed = binding.Target as MDSVariable<T>;
                if (typed == null) throw new InvalidCastException("绑定变量类型不匹配");
                int generation = _generation;
                return new MDSVariableTypedPointBinding<T>(typed, providerId,
                    () => IsActive(generation));
            }
        }
        private bool IsActive(int generation)
        {
            lock (_gate) return !_disposed && _running && _generation == generation;
        }
        public MDSVariableProviderHealth Health()
        {
            lock (_gate)
                return new MDSVariableProviderHealth { Running = _running, Received = _received,
                    Applied = Interlocked.Read(ref _applied), Rejected = Interlocked.Read(ref _rejected),
                    LastError = _lastError };
        }
        public void Stop()
        {
            IMDSVariableProvider[] sources;
            lock (_gate)
            {
                if (!_running) return;
                _running = false; _generation++;
                sources = _providers.Values.ToArray();
            }
            foreach (IMDSVariableProvider provider in sources) provider.Stop();
        }
        public void Dispose()
        {
            Stop();
            IMDSVariableProvider[] providers;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                providers = _providers.Values.ToArray();
                _providers.Clear(); _bindings.Clear();
            }
            foreach (IMDSVariableProvider provider in providers) provider.Dispose();
        }
        private void RequireOpen()
        {
            if (_disposed) throw new ObjectDisposedException("MDSVariableProviderHost");
        }
    }

    public sealed class MDSVariableTypedPointBinding<T>
    {
        private readonly MDSVariable<T> _target;
        private readonly string _source;
        private readonly Func<bool> _active;
        internal MDSVariableTypedPointBinding(MDSVariable<T> target, string source, Func<bool> active)
        { _target = target; _source = source; _active = active; }
        public void Apply(T value, MDSVariableQuality quality = MDSVariableQuality.Good)
        {
            if (!_active()) throw new InvalidOperationException("Provider不处于当前活动世代");
            _target.RefreshFromProvider(value, quality, _source);
        }
        public MDSVariableSnapshot<T> Snapshot() { return _target.Snapshot(); }
    }
}
