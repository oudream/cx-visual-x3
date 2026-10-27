using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Data.Variables;

namespace CxVisual.Professional.Automation.DeviceVariables
{
    /// <summary>设备真实写入必须由被授权的专业实现承接；写变量值本身不等于PLC执行完成。</summary>
    public interface IPADDeviceVariableWriteBackend
    {
        Task<bool> WriteAsync(PADDeviceVariableDefinition variable, object nativeValue,
            CancellationToken cancellationToken);
    }

    /// <summary>承接DeviceVariable实时更新，值实际保存于MDS；目录中绝无Current副本。</summary>
    public sealed class PADDeviceVariableProvider : IMDSVariableProvider
    {
        private readonly object _gate = new object();
        private readonly PADDeviceVariableCatalog _catalog;
        private Action<MDSVariableProviderSample> _sink;
        private bool _disposed;
        public PADDeviceVariableProvider(PADDeviceVariableCatalog catalog)
        { _catalog = catalog ?? throw new ArgumentNullException("catalog"); }
        public string ProviderId { get { return _catalog.ProviderId; } }
        public void Start(Action<MDSVariableProviderSample> publish)
        {
            if (publish == null) throw new ArgumentNullException("publish");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("PADDeviceVariableProvider");
                if (_sink != null) throw new InvalidOperationException("Provider已经启动");
                _sink = publish;
            }
        }
        public void Stop() { lock (_gate) _sink = null; }
        public void Dispose() { lock (_gate) { _disposed = true; _sink = null; } }
        public bool Emit(string stableId, object value, MDSVariableQuality quality = MDSVariableQuality.Good)
        {
            PADDeviceVariableDefinition definition = _catalog.FindById(stableId);
            if (definition == null) return false;
            if (value == null && definition.ValueType.IsValueType &&
                Nullable.GetUnderlyingType(definition.ValueType) == null)
                throw new InvalidCastException("非Nullable工业变量不能写null");
            if (value != null && !definition.ValueType.IsInstanceOfType(value))
                throw new InvalidCastException("工业变量类型不匹配: " + stableId);
            Action<MDSVariableProviderSample> sink;
            lock (_gate) { if (_disposed) throw new ObjectDisposedException("PADDeviceVariableProvider"); sink = _sink; }
            if (sink == null) return false;
            sink(new MDSVariableProviderSample(stableId, value, quality));
            return true;
        }
    }

    public sealed class PADLegacyVariableChange
    {
        public PADLegacyVariableChange(string deviceName, string variableName)
        {
            if (string.IsNullOrWhiteSpace(deviceName) || string.IsNullOrWhiteSpace(variableName))
                throw new ArgumentException("DeviceVariable事件的设备名和变量名不能为空");
            DeviceName = deviceName; VariableName = variableName;
        }
        public string DeviceName { get; private set; }
        public string VariableName { get; private set; }
    }

    /// <summary>
    /// 原VariableManager事件只提供设备/变量名，不能假装携带旧值对应时刻快照。
    /// 读取最新状态后汇入LatestOnly路径；逐笔工业采样应直接调用Provider.Emit。
    /// </summary>
    public sealed class PADLegacyVariableManagerBridge
    {
        private readonly PADDeviceVariableCatalog _catalog;
        private readonly PADDeviceVariableProvider _provider;
        private readonly Func<PADDeviceVariableDefinition, object> _read;
        public PADLegacyVariableManagerBridge(PADDeviceVariableCatalog catalog,
            PADDeviceVariableProvider provider, Func<PADDeviceVariableDefinition, object> readCurrent)
        {
            _catalog = catalog ?? throw new ArgumentNullException("catalog");
            _provider = provider ?? throw new ArgumentNullException("provider");
            _read = readCurrent ?? throw new ArgumentNullException("readCurrent");
            if (catalog.ProviderId != provider.ProviderId) throw new InvalidOperationException("Provider身份不匹配");
        }
        public int AcceptChanges(IEnumerable<PADLegacyVariableChange> changes)
        {
            if (changes == null) throw new ArgumentNullException("changes");
            int published = 0;
            // ReferTo VariableManager每次刷新都可能追加一条；这里按稳定ID读一次最新值。
            HashSet<string> processed = new HashSet<string>(StringComparer.Ordinal);
            foreach (PADLegacyVariableChange change in changes)
            {
                if (change == null) continue;
                PADDeviceVariableDefinition definition = _catalog.Find(change.DeviceName, change.VariableName);
                if (definition != null && processed.Add(definition.StableId) &&
                    _provider.Emit(definition.StableId, _read(definition)))
                    published++;
            }
            return published;
        }
    }
}
