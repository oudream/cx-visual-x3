using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Abstractions;
using CxVisual.Platform.Protocols;

namespace CxVisual.Platform.Protocols.Loopback
{
    /// <summary>
    /// 无厂商 SDK 的回环 ICS Driver。用于工程联调、SCADA/运控 Case、CI 和离线调试；
    /// 与真实驱动使用完全相同的 IIcsDevice/IVariableProvider 边界。
    /// </summary>
    public sealed class LoopbackIcsDeviceProvider : IIcsDeviceProvider
    {
        public string ProviderId { get { return "cxv.loopback.ics"; } }
        public string ProviderVersion { get { return "1.0.0"; } }
        public bool CanCreate(string deviceType)
        {
            return string.Equals(deviceType, "loopback.plc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(deviceType, "loopback.io", StringComparison.OrdinalIgnoreCase)
                || string.Equals(deviceType, "loopback.controller", StringComparison.OrdinalIgnoreCase);
        }
        public IIcsDevice Create(IcsDeviceDefinition definition, IcsConnectionProfile connection)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (!CanCreate(definition.DeviceType)) throw new NotSupportedException(definition.DeviceType);
            return new LoopbackIcsDevice(definition, connection);
        }
        public IReadOnlyCollection<CapabilityDescriptor> GetCapabilities()
        {
            return new[]
            {
                new CapabilityDescriptor(IcsCapabilityIds.DeviceLifecycle),
                new CapabilityDescriptor(IcsCapabilityIds.Variables),
                new CapabilityDescriptor(IcsCapabilityIds.MaintenanceRawAccess),
                new CapabilityDescriptor(IcsCapabilityIds.Diagnostics)
            };
        }
    }

    public sealed class LoopbackIcsDevice : IIcsDevice, IIcsMaintenanceAccess
    {
        private readonly object _gate = new object();
        private readonly IcsDeviceDefinition _definition;
        private readonly IcsConnectionProfile _connection;
        private readonly LoopbackVariableProvider _variables;
        private DeviceLifecycleState _lifecycle = DeviceLifecycleState.Unconfigured;
        private IcsConnectionState _connectionState = IcsConnectionState.Offline;
        private bool _disposed;
        private string _lastError;

        internal LoopbackIcsDevice(IcsDeviceDefinition definition, IcsConnectionProfile connection)
        {
            _definition = definition;
            _connection = connection;
            _variables = new LoopbackVariableProvider("device." + definition.DeviceId, definition.Tags);
        }

        public string DeviceId { get { return _definition.DeviceId; } }
        public string DisplayName { get { return _definition.DisplayName; } }
        public string ConnectionId { get { return _definition.ConnectionId; } }
        public DeviceLifecycleState LifecycleState { get { lock (_gate) return _lifecycle; } }
        public IcsConnectionState ConnectionState { get { lock (_gate) return _connectionState; } }
        public IVariableProvider VariableProvider { get { return _variables; } }
        public IIcsMaintenanceAccess MaintenanceAccess { get { return this; } }

        public Task ConfigureAsync(IndustrialDeviceProfile profile, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ThrowIfDisposed();
                if (profile == null) throw new ArgumentNullException(nameof(profile));
                if (!string.Equals(profile.DeviceId, DeviceId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Loopback ICS Profile DeviceId不一致。");
                _lifecycle = DeviceLifecycleState.Inactive;
                _connectionState = IcsConnectionState.Offline;
                _lastError = null;
            }
            _variables.SetConnection(false);
            return Task.CompletedTask;
        }

        public async Task ActivateAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_lifecycle == DeviceLifecycleState.Unconfigured) throw new InvalidOperationException("ICS设备未Configure：" + DeviceId);
                if (_lifecycle == DeviceLifecycleState.Faulted) throw new InvalidOperationException("ICS设备Faulted，需先ResetFault：" + DeviceId);
                if (_lifecycle == DeviceLifecycleState.Active) return;
                _connectionState = IcsConnectionState.Connecting;
            }
            if (_connection.ReconnectMinimum > TimeSpan.Zero) await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(25d, _connection.ReconnectMinimum.TotalMilliseconds)), token).ConfigureAwait(false);
            lock (_gate)
            {
                _connectionState = IcsConnectionState.Online;
                _lifecycle = DeviceLifecycleState.Active;
                _lastError = null;
            }
            _variables.SetConnection(true);
        }

        public Task DeactivateAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_disposed) return Task.CompletedTask;
                _connectionState = IcsConnectionState.Offline;
                if (_lifecycle != DeviceLifecycleState.Finalized) _lifecycle = DeviceLifecycleState.Inactive;
            }
            _variables.SetConnection(false);
            return Task.CompletedTask;
        }

        public Task ResetFaultAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_lifecycle == DeviceLifecycleState.Faulted)
                {
                    _lifecycle = DeviceLifecycleState.Inactive;
                    _connectionState = IcsConnectionState.Offline;
                    _lastError = null;
                }
            }
            return Task.CompletedTask;
        }

        public IndustrialDeviceStatus GetDeviceStatus()
        {
            lock (_gate)
            {
                return new IndustrialDeviceStatus
                {
                    DeviceId = DeviceId,
                    LifecycleState = _lifecycle,
                    IsReadable = _lifecycle == DeviceLifecycleState.Active,
                    IsWritable = _lifecycle == DeviceLifecycleState.Active,
                    Quality = _connectionState == IcsConnectionState.Online ? VariableQuality.Good : VariableQuality.Disconnected,
                    ErrorCode = _lifecycle == DeviceLifecycleState.Faulted ? "CXV-ICS-LOOPBACK-FAULT" : null,
                    Message = _lastError,
                    TimestampUtc = DateTime.UtcNow
                };
            }
        }

        public IReadOnlyCollection<CapabilityDescriptor> GetCapabilities()
        {
            return new[]
            {
                new CapabilityDescriptor(IcsCapabilityIds.DeviceLifecycle),
                new CapabilityDescriptor(IcsCapabilityIds.Variables),
                new CapabilityDescriptor(IcsCapabilityIds.MaintenanceRawAccess),
                new CapabilityDescriptor(ScadaCapabilityIds.RealtimeRead),
                new CapabilityDescriptor(ScadaCapabilityIds.ControlledWrite)
            };
        }

        /// <summary>联调/Replay 注入设备值；真实驱动对应 PLC 周期采集回调。</summary>
        public void Publish(string tagId, object value, VariableQuality quality = VariableQuality.Good)
        {
            DemandActive();
            _variables.Publish(tagId, value, quality);
        }

        /// <summary>故障注入：用于验证断线、报警、恢复和 HMI 质量显示。</summary>
        public void InjectConnectionLoss(string message = "loopback connection lost")
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                _connectionState = IcsConnectionState.Faulted;
                _lifecycle = DeviceLifecycleState.Faulted;
                _lastError = message;
            }
            _variables.SetConnection(false);
        }

        public Task<object> ReadRawAsync(string address, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); DemandActive();
            return Task.FromResult(_variables.ReadByAddress(address));
        }

        public Task WriteRawAsync(string address, object value, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); DemandActive();
            _variables.WriteByAddress(address, value);
            return Task.CompletedTask;
        }

        private void DemandActive()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_lifecycle != DeviceLifecycleState.Active || _connectionState != IcsConnectionState.Online)
                    throw new InvalidOperationException("ICS设备未在线：" + DeviceId);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _lifecycle = DeviceLifecycleState.Finalized;
                _connectionState = IcsConnectionState.Offline;
            }
            _variables.Dispose();
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(LoopbackIcsDevice)); }
    }

    internal sealed class LoopbackVariableProvider : IVariableProvider, IVariableProviderCapabilities
    {
        private sealed class Subscription : IVariableSubscription
        {
            private readonly LoopbackVariableProvider _owner;
            private readonly Guid _id;
            private bool _disposed;
            public Subscription(LoopbackVariableProvider owner, Guid id, IReadOnlyCollection<string> ids) { _owner = owner; _id = id; VariableIds = ids; }
            public IReadOnlyCollection<string> VariableIds { get; private set; }
            public void Dispose() { if (_disposed) return; _disposed = true; _owner.Remove(_id); }
        }
        private sealed class Sink
        {
            public HashSet<string> Ids;
            public Action<IReadOnlyList<VariableChange>> Callback;
        }

        private readonly object _gate = new object();
        private readonly Dictionary<string, VariableMetadata> _metadata = new Dictionary<string, VariableMetadata>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, VariableValue> _values = new Dictionary<string, VariableValue>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _addressToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, Sink> _subscriptions = new Dictionary<Guid, Sink>();
        private bool _connected;
        private bool _disposed;
        private long _sequence;
        private DateTime? _lastUpdateUtc;

        public LoopbackVariableProvider(string providerId, IEnumerable<IcsTagDefinition> tags)
        {
            ProviderId = providerId;
            foreach (IcsTagDefinition tag in tags ?? Enumerable.Empty<IcsTagDefinition>())
            {
                tag.Validate();
                Type type = ResolveType(tag.DataType);
                VariableMetadata metadata = new VariableMetadata
                {
                    Id = tag.TagId,
                    ProviderId = ProviderId,
                    ProviderIdentityKey = ProviderId,
                    Name = tag.Name,
                    Path = tag.TagId,
                    Address = tag.Address,
                    ValueType = type,
                    Unit = tag.Unit,
                    Description = tag.Description,
                    Access = tag.Access,
                    Minimum = tag.Minimum,
                    Maximum = tag.Maximum
                };
                _metadata.Add(tag.TagId, metadata);
                _addressToId.Add(tag.Address, tag.TagId);
                _values.Add(tag.TagId, new VariableValue(DefaultValue(type), VariableQuality.Disconnected, DateTime.UtcNow, 0, ProviderId));
            }
        }

        public string ProviderId { get; private set; }

        public IReadOnlyList<VariableMetadata> GetMetadata()
        {
            lock (_gate) { ThrowIfDisposed(); return new ReadOnlyCollection<VariableMetadata>(_metadata.Values.OrderBy(x => x.Id).ToList()); }
        }

        public Task<VariableValue> ReadAsync(string variableId, CancellationToken token) => CxVariableProviderObservations.ReadCacheAsync(() => Read(variableId), token);
        public CxVariableProviderState GetState() { lock (_gate) return _disposed ? CxVariableProviderState.Stopped : CxVariableProviderObservations.GetState(GetDiagnostics()); }
        public Task<ProviderDiagnostics> GetHealthAsync(CancellationToken token) { lock (_gate) return CxVariableProviderObservations.GetHealthAsync(GetDiagnostics, token, GetState()); }

        public VariableValue Read(string variableId)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                VariableValue value;
                if (!_values.TryGetValue(variableId, out value)) throw new KeyNotFoundException("Tag不存在：" + variableId);
                return value;
            }
        }

        public Task<OperationResult> WriteAsync(string variableId, object value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VariableMetadata metadata;
            lock (_gate)
            {
                ThrowIfDisposed();
                if (!_connected) return Task.FromResult(OperationResult.Fail("CXV-ICS-OFFLINE", "ICS", "设备离线，拒绝写入。"));
                if (!_metadata.TryGetValue(variableId, out metadata)) return Task.FromResult(OperationResult.Fail("CXV-ICS-TAG-NOTFOUND", "ICS", "Tag不存在：" + variableId));
                if (metadata.Access != VariableAccess.ReadWrite) return Task.FromResult(OperationResult.Fail("CXV-ICS-READONLY", "ICS", "Tag只读：" + variableId));
            }
            try { Publish(variableId, ConvertValue(value, metadata.ValueType), VariableQuality.Good); return Task.FromResult(OperationResult.Ok()); }
            catch (Exception ex) { return Task.FromResult(OperationResult.Fail("CXV-ICS-WRITE", "ICS", ex.GetBaseException().Message)); }
        }

        public IVariableSubscription Subscribe(IEnumerable<string> variableIds, Action<IReadOnlyList<VariableChange>> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            HashSet<string> ids = new HashSet<string>((variableIds ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)), StringComparer.OrdinalIgnoreCase);
            lock (_gate)
            {
                ThrowIfDisposed();
                foreach (string id in ids) if (!_metadata.ContainsKey(id)) throw new KeyNotFoundException("Tag不存在：" + id);
                Guid key = Guid.NewGuid();
                _subscriptions.Add(key, new Sink { Ids = ids, Callback = callback });
                return new Subscription(this, key, new ReadOnlyCollection<string>(ids.ToList()));
            }
        }

        public ProviderDiagnostics GetDiagnostics()
        {
            lock (_gate)
            {
                return new ProviderDiagnostics
                {
                    ProviderId = ProviderId,
                    IsConnected = _connected,
                    VariableCount = _metadata.Count,
                    SubscriptionCount = _subscriptions.Count,
                    LastUpdateUtc = _lastUpdateUtc,
                    LastError = _connected ? null : "Disconnected"
                };
            }
        }

        public IReadOnlyCollection<CapabilityDescriptor> GetCapabilities()
        {
            return new[] { new CapabilityDescriptor(ScadaCapabilityIds.RealtimeRead), new CapabilityDescriptor(ScadaCapabilityIds.ControlledWrite) };
        }

        public void SetConnection(bool connected)
        {
            List<VariableChange> changes = new List<VariableChange>();
            lock (_gate)
            {
                ThrowIfDisposed();
                _connected = connected;
                foreach (string id in _values.Keys.ToList())
                {
                    VariableValue old = _values[id];
                    VariableValue next = new VariableValue(old.Value, connected ? VariableQuality.Good : VariableQuality.Disconnected, DateTime.UtcNow, ++_sequence, ProviderId);
                    _values[id] = next;
                    changes.Add(new VariableChange { VariableId = id, Value = next });
                }
                _lastUpdateUtc = DateTime.UtcNow;
            }
            Notify(changes);
        }

        public void Publish(string variableId, object value, VariableQuality quality)
        {
            List<VariableChange> changes = new List<VariableChange>();
            lock (_gate)
            {
                ThrowIfDisposed();
                VariableMetadata metadata;
                if (!_metadata.TryGetValue(variableId, out metadata)) throw new KeyNotFoundException("Tag不存在：" + variableId);
                object converted = ConvertValue(value, metadata.ValueType);
                VariableValue next = new VariableValue(converted, _connected ? quality : VariableQuality.Disconnected, DateTime.UtcNow, ++_sequence, ProviderId);
                _values[variableId] = next;
                _lastUpdateUtc = next.TimestampUtc;
                changes.Add(new VariableChange { VariableId = variableId, Value = next });
            }
            Notify(changes);
        }

        public object ReadByAddress(string address)
        {
            string id;
            lock (_gate) { if (!_addressToId.TryGetValue(address ?? string.Empty, out id)) throw new KeyNotFoundException("Address不存在：" + address); }
            return Read(id).Value;
        }

        public void WriteByAddress(string address, object value)
        {
            string id;
            lock (_gate) { if (!_addressToId.TryGetValue(address ?? string.Empty, out id)) throw new KeyNotFoundException("Address不存在：" + address); }
            VariableMetadata metadata = _metadata[id];
            Publish(id, ConvertValue(value, metadata.ValueType), VariableQuality.Good);
        }

        private void Notify(IReadOnlyList<VariableChange> changes)
        {
            List<Sink> sinks;
            lock (_gate) sinks = _subscriptions.Values.ToList();
            foreach (Sink sink in sinks)
            {
                List<VariableChange> filtered = changes.Where(x => sink.Ids.Contains(x.VariableId)).ToList();
                if (filtered.Count > 0)
                {
                    try { sink.Callback(new ReadOnlyCollection<VariableChange>(filtered)); } catch { }
                }
            }
        }

        private void Remove(Guid id) { lock (_gate) { if (!_disposed) _subscriptions.Remove(id); } }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _subscriptions.Clear();
                _metadata.Clear();
                _values.Clear();
                _addressToId.Clear();
            }
        }

        private static Type ResolveType(string dataType)
        {
            string key = (dataType ?? "Double").Trim().ToLowerInvariant();
            if (key == "bool" || key == "boolean") return typeof(bool);
            if (key == "int" || key == "int32") return typeof(int);
            if (key == "long" || key == "int64") return typeof(long);
            if (key == "float" || key == "single") return typeof(float);
            if (key == "string") return typeof(string);
            return typeof(double);
        }

        private static object DefaultValue(Type type)
        {
            if (type == typeof(string)) return string.Empty;
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static object ConvertValue(object value, Type type)
        {
            if (value == null) return null;
            if (type.IsInstanceOfType(value)) return value;
            return Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
        }

        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(LoopbackVariableProvider)); }
    }
}
