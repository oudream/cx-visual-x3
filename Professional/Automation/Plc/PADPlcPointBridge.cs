using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Professional.Automation.DeviceVariables;

namespace CxVisual.Professional.Automation.Plc
{
    /// <summary>仅绑定现有PAD稳定变量与MPA协议点，不维护第二份实时值或协议地址表。</summary>
    public sealed class PADPlcPointBinding
    {
        public PADPlcPointBinding(string stableId, string protocolPointName)
        {
            if (string.IsNullOrWhiteSpace(stableId) || string.IsNullOrWhiteSpace(protocolPointName))
                throw new ArgumentException("PLC变量稳定ID与协议点名不能为空");
            StableId = stableId.Trim();
            ProtocolPointName = protocolPointName.Trim();
        }
        public string StableId { get; private set; }
        public string ProtocolPointName { get; private set; }
    }

    /// <summary>
    /// 协议应答不等于物理写入完成。发生超时、读回不一致、会话改变时，
    /// 物理结果未知；不得直接重发非幂等控制，也不伪造MDS成功值。
    /// </summary>
    public sealed class PADPlcOutcomeUnknownException : Exception
    {
        public PADPlcOutcomeUnknownException(string pointName, string message, Exception inner = null)
            : base(message, inner)
        {
            ProtocolPointName = pointName;
        }
        public string ProtocolPointName { get; private set; }
    }

    /// <summary>
    /// P02 首批 PLC 寄存器闭环。复用正式MCN/MPA和MDS DataAccess，不拥有Channel、
    /// Protocol、Current或命令中心；写入口只能通过PADDeviceVariableProvider的授权回调。
    /// 当前仅支持可回读的Modbus Holding Int16/UInt16点。
    /// </summary>
    public sealed class PADPlcPointBridge : IDisposable
    {
        private readonly MCNConnectorModule _connector;
        private readonly IFTRChannelTransport _transport;
        private readonly PADDeviceVariableCatalog _catalog;
        private readonly FTRSubChannelAddress _destination;
        private readonly Dictionary<string, string> _bindings;
        private readonly Dictionary<string, string> _approvedSignatures;
        private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);
        private readonly string _scopeId;
        private PADDeviceVariableProvider _qualityProvider;
        private int _disposed;

        public PADPlcPointBridge(MCNConnectorModule connector, PADDeviceVariableCatalog catalog,
            FTRSubChannelAddress destination, string scopeId, IEnumerable<PADPlcPointBinding> bindings)
        {
            _connector = connector ?? throw new ArgumentNullException("connector");
            _catalog = catalog ?? throw new ArgumentNullException("catalog");
            _destination = destination ?? throw new ArgumentNullException("destination");
            if (destination.ChannelId != connector.Channel.Id)
                throw new ArgumentException("PLC目标与Connector Channel不一致");
            if (string.IsNullOrWhiteSpace(scopeId) || bindings == null)
                throw new ArgumentException("MDS Scope与PLC绑定不能为空");
            if (connector.Protocol.Id != "modbus.tcp")
                throw new NotSupportedException("当前闭环仅验证Modbus TCP Holding单寄存器");
            _transport = connector.Channel as IFTRChannelTransport;
            if (_transport == null)
                throw new NotSupportedException("PLC回读与断线质量通知要求可执行IFTRChannelTransport");
            _scopeId = scopeId.Trim();
            string prefix = _scopeId + "/";
            _bindings = new Dictionary<string, string>(StringComparer.Ordinal);
            _approvedSignatures = new Dictionary<string, string>(StringComparer.Ordinal);
            HashSet<string> points = new HashSet<string>(StringComparer.Ordinal);
            foreach (PADPlcPointBinding binding in bindings)
            {
                if (binding == null || !_bindings.TryAddCompat(binding.StableId, binding.ProtocolPointName) ||
                    !points.Add(binding.ProtocolPointName))
                    throw new ArgumentException("PLC稳定ID或协议点重复");
                PADDeviceVariableDefinition variable = catalog.FindById(binding.StableId);
                MPAPointDefinition point = connector.Points.FirstOrDefault(x => x.Name == binding.ProtocolPointName);
                if (variable == null || point == null || !point.CanRead ||
                    (variable.ValueType != typeof(ushort) && variable.ValueType != typeof(short)) ||
                    point.ValueType != variable.ValueType.Name || (variable.Writable && !point.CanWrite))
                    throw new ArgumentException("PLC点/变量类型、读写能力不匹配: " + binding.StableId);
                string target = prefix + variable.Address;
                // 这是可用于直接操作真实PLC的专业桥接，必须fail-closed：
                // 禁止额外的消息/命令/跨变量映射在读取传感器时产生隐含副作用。
                MPADataMapping[] mappings = connector.Configuration.Protocol.Mappings
                    .Where(x => x.PointRef == point.Name).ToArray();
                MPADataMapping[] inbound = mappings.Where(x =>
                    x.TargetKind == MPADataTargetKind.Variable &&
                    x.TargetRef == target && x.Direction == MPADataDirection.Inbound &&
                    x.Operation == MPADataOperation.WriteValue &&
                    x.Transform == MPADataTransformKind.None && x.Required).ToArray();
                MPADataMapping[] outbound = mappings.Where(x =>
                    x.TargetKind == MPADataTargetKind.Variable &&
                    x.TargetRef == target && x.Direction == MPADataDirection.Outbound &&
                    x.Operation == MPADataOperation.ReadValue &&
                    x.Transform == MPADataTransformKind.None && x.Required).ToArray();
                if (inbound.Length != 1 || outbound.Length != (variable.Writable ? 1 : 0) ||
                    mappings.Length != inbound.Length + outbound.Length)
                    throw new ArgumentException(
                        "PLC点只允许唯一、必需且目标一致的MDS入站映射与授权出站映射: " + point.Name);
                _approvedSignatures.Add(binding.StableId, Signature(point, mappings));
            }
            if (_bindings.Count == 0) throw new ArgumentException("至少绑定一个有效PLC测点");
            _transport.StateChanged += OnChannelStateChanged;
        }

        /// <summary>
        /// 规约回包先进入操作局部暂存。会话世代和点表经二次核验后，才向唯一MDS仓提交；
        /// 对超时、配置漂移、迟到回包均不将未经确认的数据发布为Good。
        /// </summary>
        public async Task<object> ReadAndMapAsync(string stableId, CancellationToken cancellationToken)
        {
            try
            {
                return await ReadConfirmedAsync(stableId, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                MarkStale(stableId);
                throw;
            }
        }

        // Host/Session仍是唯一运行Owner。桥接只消费现有F17状态变化，避免断线后继续展示Good。
        private void OnChannelStateChanged(FTRChannelState state)
        {
            if (state == FTRChannelState.Open || Volatile.Read(ref _disposed) != 0) return;
            foreach (string id in _bindings.Keys) MarkStale(id);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _transport.StateChanged -= OnChannelStateChanged;
        }

        private void MarkStale(string stableId)
        {
            PADDeviceVariableProvider provider = Volatile.Read(ref _qualityProvider);
            if (provider == null) return;
            try { provider.EmitQuality(stableId, CxVisual.Mechanism.Data.Variables.MDSVariableQuality.Stale); }
            catch (ObjectDisposedException) { /* 正在Stop的Host不再接受该世代质量信号 */ }
        }

        private async Task<object> ReadConfirmedAsync(string stableId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string point = Resolve(stableId);
            PADDeviceVariableDefinition variable = _catalog.FindById(stableId);
            if (variable == null) throw new KeyNotFoundException("工业变量定义已经失效: " + stableId);
            string generation = _connector.Channel.SessionId.ToString("N");
            string target = _scopeId + "/" + variable.Address;
            StagedReadAccess staging = new StagedReadAccess(target);
            object received = await _connector.ReadDevicePointAsync(
                point, staging, _destination, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _connector.Channel.SessionId.ToString("N") ||
                !string.Equals(point, Resolve(stableId), StringComparison.Ordinal))
                throw new PADPlcOutcomeUnknownException(point,
                    "PLC读回期间会话或点表变化，禁止提交未经确认的数据");
            if (!staging.HasValue || received == null || !object.Equals(received, staging.Value) ||
                !variable.ValueType.IsInstanceOfType(staging.Value))
                throw new PADPlcOutcomeUnknownException(point,
                    "PLC协议读回与映射结果不一致，禁止写入MDS");
            // 正式ProviderHost以设备来源身份提交，允许DI等只读工程变量接收采样。
            // 与普通MDS变量Update(受Writable约束)不同，始终保留唯一MDS Current。
            PADDeviceVariableProvider publisher = Volatile.Read(ref _qualityProvider);
            if (publisher == null || !publisher.Emit(stableId, staging.Value,
                CxVisual.Mechanism.Data.Variables.MDSVariableQuality.Good))
                throw new InvalidOperationException("PLC设备Provider未启动，禁止宣称采样已发布");
            return received;
        }

        /// <summary>沿用PAD现有授权接口，默认无授权回调时拒绝所有写入。</summary>
        public PADDeviceVariableProvider CreateWriteProvider(
            Func<string, PADDeviceVariableDefinition, bool> authorizeWrite)
        {
            PADDeviceVariableProvider provider =
                new PADDeviceVariableProvider(_catalog, new VerifiedWriteBackend(this), authorizeWrite);
            RegisterQualityProvider(provider);
            return provider;
        }

        /// <summary>只读PLC可只注册采样与质量源；没有装配写入后端或授权时写入默认拒绝。</summary>
        public PADDeviceVariableProvider CreateReadProvider()
        {
            PADDeviceVariableProvider provider = new PADDeviceVariableProvider(_catalog);
            RegisterQualityProvider(provider);
            return provider;
        }

        private void RegisterQualityProvider(PADDeviceVariableProvider provider)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                provider.Dispose(); throw new ObjectDisposedException("PADPlcPointBridge");
            }
            if (Interlocked.CompareExchange(ref _qualityProvider, provider, null) != null)
            {
                provider.Dispose();
                throw new InvalidOperationException("PLC会话已装配Provider，不允许二次创建");
            }
        }

        private string Resolve(string stableId)
        {
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException("PADPlcPointBridge");
            string point;
            if (stableId == null || !_bindings.TryGetValue(stableId, out point))
                throw new KeyNotFoundException("PLC变量未绑定：" + stableId);
            MPAPointDefinition live = _connector.Points.FirstOrDefault(x => x.Name == point);
            MPADataMapping[] mappings = _connector.Configuration.Protocol.Mappings
                .Where(x => x != null && x.PointRef == point).ToArray();
            string approved;
            if (!_approvedSignatures.TryGetValue(stableId, out approved) ||
                !string.Equals(approved, Signature(live, mappings), StringComparison.Ordinal))
                throw new InvalidOperationException("PLC运行点表已变化，须停止会话并重新审批装配: " + point);
            return point;
        }

        // 记录当前会话被批准的点描述与映射；不是加密签名或跨线程原子配置版本。
        private static string Signature(MPAPointDefinition point, MPADataMapping[] mappings)
        {
            if (point == null || mappings == null) return "";
            StringBuilder builder = new StringBuilder();
            Action<string> append = value => {
                string text = value ?? "";
                builder.Append(text.Length).Append(':').Append(text);
            };
            append(point.Name); append(point.Address); append(point.StationAddress);
            append(point.ValueType); append(point.CanRead ? "1" : "0");
            append(point.CanWrite ? "1" : "0");
            append(mappings.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (MPADataMapping item in mappings)
            {
                append(item.PointRef); append(item.TargetRef);
                append(((int)item.TargetKind).ToString(System.Globalization.CultureInfo.InvariantCulture));
                append(((int)item.Direction).ToString(System.Globalization.CultureInfo.InvariantCulture));
                append(((int)item.Operation).ToString(System.Globalization.CultureInfo.InvariantCulture));
                append(((int)item.Transform).ToString(System.Globalization.CultureInfo.InvariantCulture));
                append(item.Required ? "1" : "0");
            }
            return builder.ToString();
        }

        private sealed class VerifiedWriteBackend : IPADDeviceVariableWriteBackend
        {
            private readonly PADPlcPointBridge _owner;
            public VerifiedWriteBackend(PADPlcPointBridge owner) { _owner = owner; }
            public Task<bool> WriteAsync(PADDeviceVariableDefinition variable, object nativeValue,
                CancellationToken cancellationToken)
            {
                return _owner.WriteVerifiedAsync(variable, nativeValue, cancellationToken);
            }
        }

        private async Task<bool> WriteVerifiedAsync(PADDeviceVariableDefinition variable,
            object nativeValue, CancellationToken ct)
        {
            if (variable == null || !variable.Writable) throw new InvalidOperationException("PLC点只读");
            // 作用域内串行：避免两个写+回读交错。不同宿主必须由正式MRK提供跨调用资源租约。
            await _writeGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();
                string pointName = Resolve(variable.StableId);
                string generation = _connector.Channel.SessionId.ToString("N");
                MPAProtocolOperationResult acknowledgement;
                try
                {
                    acknowledgement = await _connector.WriteDevicePointAsync(pointName,
                        new StagedWriteAccess(nativeValue, _scopeId + "/" + variable.Address), _destination, ct).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    MarkStale(variable.StableId);
                    throw new PADPlcOutcomeUnknownException(pointName,
                        "PLC写请求可能已发送，协议结果未知；必须现场核实", error);
                }
                if (acknowledgement == null || !acknowledgement.IsCompleted)
                {
                    MarkStale(variable.StableId);
                    throw new PADPlcOutcomeUnknownException(pointName,
                        "PLC协议未确认完成；禁止将写请求标记为物理成功");
                }
                object observed;
                try
                {
                    if (_connector.Channel.SessionId.ToString("N") != generation)
                        throw new InvalidOperationException("PLC连接代次已改变");
                    observed = await ReadConfirmedAsync(variable.StableId, ct).ConfigureAwait(false);
                    if (_connector.Channel.SessionId.ToString("N") != generation)
                        throw new InvalidOperationException("PLC写入至回读期间连接代次已改变");
                }
                catch (Exception error)
                {
                    MarkStale(variable.StableId);
                    throw new PADPlcOutcomeUnknownException(pointName,
                        "PLC写入已获协议应答，但回读缺失或失效；物理结果未知", error);
                }
                if (!object.Equals(nativeValue, observed))
                    throw new PADPlcOutcomeUnknownException(pointName,
                        "PLC协议已应答，但实际寄存器值与命令不符；物理结果未知");
                return true;
            }
            finally { _writeGate.Release(); }
        }

        /// <summary>仅保存单次规约读回；不是缓存、变量仓或第二份运行Current。</summary>
        private sealed class StagedReadAccess : IMPADataTargetAccess
        {
            private readonly string _expectedTarget;
            public StagedReadAccess(string target) { _expectedTarget = target; }
            public bool HasValue { get; private set; }
            public object Value { get; private set; }
            public Task<object> ReadAsync(MPADataTargetKind kind, string targetRef, CancellationToken ct)
            { throw new NotSupportedException("PLC读回暂存不支持获取其他MDS变量"); }
            public Task WriteAsync(MPADataTargetKind kind, string targetRef, object value, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                if (kind != MPADataTargetKind.Variable ||
                    !string.Equals(targetRef, _expectedTarget, StringComparison.Ordinal) || HasValue)
                    throw new InvalidOperationException("PLC读回不允许跨目标写入或重复提交");
                Value = value;
                HasValue = true;
                return Task.CompletedTask;
            }
            public Task PublishAsync(string targetRef, object value, CancellationToken ct)
            { throw new NotSupportedException("PLC读回暂存不能发布事件"); }
            public Task InvokeAsync(string targetRef, object value, CancellationToken ct)
            { throw new NotSupportedException("PLC读回暂存不能执行命令"); }
        }

        /// <summary>一次写入请求的临时数据源，不预先覆盖MDS当前值。</summary>
        private sealed class StagedWriteAccess : IMPADataTargetAccess
        {
            private readonly object _value;
            private readonly string _expectedTarget;
            public StagedWriteAccess(object value, string expectedTarget)
            { _value = value; _expectedTarget = expectedTarget; }
            public Task<object> ReadAsync(MPADataTargetKind kind, string targetRef, CancellationToken ct)
            {
                ct.ThrowIfCancellationRequested();
                if (kind != MPADataTargetKind.Variable ||
                    !string.Equals(targetRef, _expectedTarget, StringComparison.Ordinal))
                    throw new NotSupportedException("PLC临时写源仅允许已验证的变量映射");
                return Task.FromResult(_value);
            }
            public Task WriteAsync(MPADataTargetKind kind, string targetRef, object value, CancellationToken ct)
            { throw new NotSupportedException("PLC临时写源不允许写入MDS"); }
            public Task PublishAsync(string targetRef, object value, CancellationToken ct)
            { throw new NotSupportedException("PLC临时写源不允许发布事件"); }
            public Task InvokeAsync(string targetRef, object value, CancellationToken ct)
            { throw new NotSupportedException("PLC临时写源不允许调用命令"); }
        }
    }

    internal static class PADPlcDictionaryCompat
    {
        // .NET Framework 4.8 / 传统C#字典显式Add，不依赖TryAdd新API。
        public static bool TryAddCompat(this Dictionary<string, string> target, string key, string value)
        {
            if (target.ContainsKey(key)) return false;
            target.Add(key, value);
            return true;
        }
    }
}
