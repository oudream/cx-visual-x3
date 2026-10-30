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
    public sealed class PADPlcPointBridge
    {
        private readonly MCNConnectorModule _connector;
        private readonly PADDeviceVariableCatalog _catalog;
        private readonly IMPADataTargetAccess _dataOwner;
        private readonly FTRSubChannelAddress _destination;
        private readonly Dictionary<string, string> _bindings;
        private readonly Dictionary<string, string> _approvedSignatures;
        private readonly SemaphoreSlim _writeGate = new SemaphoreSlim(1, 1);
        private readonly string _scopeId;

        public PADPlcPointBridge(MCNConnectorModule connector, PADDeviceVariableCatalog catalog,
            IMPADataTargetAccess dataOwner, FTRSubChannelAddress destination,
            string scopeId, IEnumerable<PADPlcPointBinding> bindings)
        {
            _connector = connector ?? throw new ArgumentNullException("connector");
            _catalog = catalog ?? throw new ArgumentNullException("catalog");
            _dataOwner = dataOwner ?? throw new ArgumentNullException("dataOwner");
            _destination = destination ?? throw new ArgumentNullException("destination");
            if (destination.ChannelId != connector.Channel.Id)
                throw new ArgumentException("PLC目标与Connector Channel不一致");
            if (string.IsNullOrWhiteSpace(scopeId) || bindings == null)
                throw new ArgumentException("MDS Scope与PLC绑定不能为空");
            if (connector.Protocol.Id != "modbus.tcp")
                throw new NotSupportedException("当前闭环仅验证Modbus TCP Holding单寄存器");
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
        }

        /// <summary>真实规约读点并通过MPA入站映射提交到MDS；不重复调用Provider.Emit。</summary>
        public Task<object> ReadAndMapAsync(string stableId, CancellationToken cancellationToken)
        {
            string point = Resolve(stableId);
            return _connector.ReadDevicePointAsync(point, _dataOwner, _destination, cancellationToken);
        }

        /// <summary>沿用PAD现有授权接口，默认无授权回调时拒绝所有写入。</summary>
        public PADDeviceVariableProvider CreateWriteProvider(
            Func<string, PADDeviceVariableDefinition, bool> authorizeWrite)
        {
            return new PADDeviceVariableProvider(_catalog, new VerifiedWriteBackend(this), authorizeWrite);
        }

        private string Resolve(string stableId)
        {
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
            string pointName = Resolve(variable.StableId);
            // 作用域内串行：避免两个写+回读交错。不同宿主必须由正式MRK提供跨调用资源租约。
            await _writeGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();
                string generation = _connector.Channel.SessionId.ToString("N");
                MPAProtocolOperationResult acknowledgement;
                try
                {
                    acknowledgement = await _connector.WriteDevicePointAsync(pointName,
                        new StagedWriteAccess(nativeValue, _scopeId + "/" + variable.Address), _destination, ct).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    throw new PADPlcOutcomeUnknownException(pointName,
                        "PLC写请求可能已发送，协议结果未知；必须现场核实", error);
                }
                if (acknowledgement == null || !acknowledgement.IsCompleted)
                    throw new PADPlcOutcomeUnknownException(pointName,
                        "PLC协议未确认完成；禁止将写请求标记为物理成功");
                object observed;
                try
                {
                    if (_connector.Channel.SessionId.ToString("N") != generation)
                        throw new InvalidOperationException("PLC连接代次已改变");
                    observed = await _connector.ReadDevicePointAsync(pointName,
                        _dataOwner, _destination, ct).ConfigureAwait(false);
                    if (_connector.Channel.SessionId.ToString("N") != generation)
                        throw new InvalidOperationException("PLC回读过程中连接代次已改变");
                }
                catch (Exception error)
                {
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
