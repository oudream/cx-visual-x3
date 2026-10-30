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
        public bool IsQualityOnly { get; private set; }

        /// <summary>质量通知不包含新的测量值；只允许标记已采样值不可信。</summary>
        public static MDSVariableProviderSample QualityOnly(string pointId, MDSVariableQuality quality)
        {
            if (quality == MDSVariableQuality.Good || !Enum.IsDefined(typeof(MDSVariableQuality), quality))
                throw new ArgumentOutOfRangeException("quality");
            MDSVariableProviderSample sample = new MDSVariableProviderSample(pointId, null, quality);
            sample.IsQualityOnly = true;
            return sample;
        }
    }

    public sealed class MDSVariableProviderBinding
    {
        internal readonly MDSVariableBase Target;
        internal readonly MDSVariableScope Scope;
        internal MDSVariableProviderBinding(string providerId, string pointId, string scopeId,
            string variableName, MDSVariableBase target, MDSVariableScope scope)
        {
            ProviderId = providerId; PointId = pointId; ScopeId = scopeId;
            VariableName = variableName; Target = target;
            Scope = scope ?? throw new ArgumentNullException("scope");
        }
        public string ProviderId { get; private set; }
        public string PointId { get; private set; }
        public string ScopeId { get; private set; }
        public string VariableName { get; private set; }
        public string VariableId { get { return Target.EntryDefinition.VariableId; } }
    }

    /// <summary>Provider只读注册描述；映射在唯一Host注册表，描述不持有当前变量值。</summary>
    public sealed class MDSVariableProviderDescriptor
    {
        internal MDSVariableProviderDescriptor(string id, string implementation, int bindings, bool running)
        { ProviderId = id; ImplementationType = implementation; BindingCount = bindings; Running = running; }
        public string ProviderId { get; private set; }
        public string ImplementationType { get; private set; }
        public int BindingCount { get; private set; }
        public bool Running { get; private set; }
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
        private readonly object _lifecycleGate = new object();
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
                MDSVariableScope scope = _registry.FindScope(scopeId);
                if (scope == null || !scope.IsActive)
                    throw new KeyNotFoundException("变量Scope不存在或已被注销");
                MDSVariableTable table = _registry.Find(scopeId);
                if (table == null) throw new KeyNotFoundException("变量Scope不存在");
                MDSVariableBase variable = table.ResolveEntry(variableName);
                string key = Key(providerId, pointId);
                if (_bindings.ContainsKey(key)) throw new ArgumentException("映射已存在: " + key);
                MDSVariableProviderBinding binding = new MDSVariableProviderBinding(providerId,
                    pointId, scopeId, variableName, variable, scope);
                _bindings.Add(key, binding);
                return binding;
            }
        }
        public MDSVariableProviderBinding[] Bindings()
        {
            lock (_gate) return _bindings.Values.ToArray();
        }
        public MDSVariableProviderDescriptor[] Descriptors()
        {
            lock (_gate)
            {
                RequireOpen();
                return _providers.Values.OrderBy(x => x.ProviderId, StringComparer.Ordinal)
                    .Select(x => new MDSVariableProviderDescriptor(x.ProviderId,
                        x.GetType().FullName, _bindings.Values.Count(y => y.ProviderId == x.ProviderId),
                        _running)).ToArray();
            }
        }
        public bool Unregister(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId)) return false;
            IMDSVariableProvider provider;
            lock (_gate)
            {
                RequireOpen();
                if (_running) throw new InvalidOperationException("运行中不能注销Provider");
                if (!_providers.TryGetValue(providerId, out provider)) return false;
                _providers.Remove(providerId);
                foreach (string key in _bindings.Where(x => x.Value.ProviderId == providerId)
                    .Select(x => x.Key).ToArray())
                    _bindings.Remove(key);
            }
            // Dispose位于锁外，避免专业Provider回调死锁。
            provider.Dispose();
            return true;
        }

        public void Start()
        {
            lock (_lifecycleGate)
            {
            IMDSVariableProvider[] sources;
            int generation;
            lock (_gate)
            {
                RequireOpen();
                if (_running) throw new InvalidOperationException("Provider Host已启动");
                if (_bindings.Values.Any(x => !x.Scope.IsActive))
                    throw new InvalidOperationException("存在过期的变量Scope映射；需重新绑定");
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
                if (!binding.Scope.IsActive)
                {
                    _rejected++; _lastError = "Provider绑定的运行Scope已注销";
                    return;
                }
            }
            try
            {
                // 真实变量对象可供只读业务定义从设备Provider刷新；不要求调用方把它改成Writable。
                using (binding.Scope.Acquire())
                {
                    if (sample.IsQualityOnly)
                        binding.Target.MarkProviderQualityBoxed(sample.Quality, source);
                    else
                        binding.Target.RefreshProviderBoxed(sample.Value, sample.Quality, source);
                }
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
                if (!binding.Scope.IsActive)
                    throw new InvalidOperationException("Provider绑定的Scope已注销");
                MDSVariable<T> typed = binding.Target as MDSVariable<T>;
                if (typed == null) throw new InvalidCastException("绑定变量类型不匹配");
                int generation = _generation;
                return new MDSVariableTypedPointBinding<T>(typed, providerId,
                    () => IsActive(generation, binding.Scope), binding.Scope);
            }
        }
        private bool IsActive(int generation, MDSVariableScope scope)
        {
            lock (_gate)
                return !_disposed && _running && _generation == generation && scope.IsActive;
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
            lock (_lifecycleGate)
            {
                IMDSVariableProvider[] sources;
                lock (_gate)
                {
                    if (!_running) return;
                    _running = false; _generation++;
                    sources = _providers.Values.ToArray();
                }
                // 一个工业设备Stop故障不能阻止其他设备释放资源。
                List<Exception> errors = new List<Exception>();
                foreach (IMDSVariableProvider provider in sources)
                {
                    try { provider.Stop(); }
                    catch (Exception error) { errors.Add(error); }
                }
                if (errors.Count > 0)
                {
                    lock (_gate) _lastError = "Provider Stop: " + errors[0].Message;
                    throw new AggregateException("部分Provider停止失败，其余Provider仍已尝试停止", errors);
                }
            }
        }
        public void Dispose()
        {
            lock (_lifecycleGate)
            {
                List<Exception> errors = new List<Exception>();
                try { Stop(); }
                catch (AggregateException error) { errors.AddRange(error.InnerExceptions); }
                IMDSVariableProvider[] providers;
                lock (_gate)
                {
                    if (_disposed)
                    {
                        if (errors.Count > 0) throw new AggregateException(errors);
                        return;
                    }
                    _disposed = true;
                    providers = _providers.Values.ToArray();
                    _providers.Clear(); _bindings.Clear();
                }
                foreach (IMDSVariableProvider provider in providers)
                    try { provider.Dispose(); }
                    catch (Exception error) { errors.Add(error); }
                if (errors.Count > 0)
                    throw new AggregateException("Provider宿主关闭存在错误，已尝试释放全部Provider", errors);
            }
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
        private readonly MDSVariableScope _scope;
        internal MDSVariableTypedPointBinding(MDSVariable<T> target, string source,
            Func<bool> active, MDSVariableScope scope)
        { _target = target; _source = source; _active = active; _scope = scope; }
        public void Apply(T value, MDSVariableQuality quality = MDSVariableQuality.Good)
        {
            // Scope注销时与普通Handle共用入场门禁，避免检查后写入的竞态。
            try
            {
                using (_scope.Acquire())
                {
                    if (!_active()) throw new InvalidOperationException("Provider不处于当前活动世代");
                    _target.RefreshFromProvider(value, quality, _source);
                }
            }
            catch (ObjectDisposedException error)
            {
                // 保持公开Provider热绑定原有的失效异常合同。
                throw new InvalidOperationException("Provider不处于当前活动世代", error);
            }
        }
        public MDSVariableSnapshot<T> Snapshot() { return _target.Snapshot(); }
    }
}
