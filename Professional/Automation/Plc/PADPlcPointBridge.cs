using System;
using System.Collections.Generic;
using System.IO;
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

    /// <summary>单个已绑定PLC点的本次扫描结果；历史值仍只有MDS拥有。</summary>
    public sealed class PADPlcPointScanResult
    {
        internal PADPlcPointScanResult(string stableId, object value, string failureCode)
        {
            StableId = stableId;
            Value = value;
            FailureCode = failureCode ?? string.Empty;
        }
        public string StableId { get; private set; }
        public object Value { get; private set; }
        public string FailureCode { get; private set; }
        public bool Succeeded { get { return FailureCode.Length == 0; } }
    }

    /// <summary>有界顺序扫描报告；不是PLC多寄存器单报文或跨点原子快照。</summary>
    public sealed class PADPlcScanResult
    {
        internal PADPlcScanResult(IList<PADPlcPointScanResult> items)
        {
            PADPlcPointScanResult[] copy = items.ToArray();
            Points = Array.AsReadOnly(copy);
            SucceededCount = copy.Count(x => x.Succeeded);
        }
        public IReadOnlyList<PADPlcPointScanResult> Points { get; private set; }
        public int SucceededCount { get; private set; }
        public int FailedCount { get { return Points.Count - SucceededCount; } }
        public bool AllSucceeded { get { return FailedCount == 0; } }
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
        /// 多点顺序轮询：限制数量、预检全部点位和映射、共享现有写互斥，
        /// 各点独立FC03事务，成功写入MDS，超时/协议失败只标Stale并继续后续点。
        /// 调用取消、连接代次变化、运行配置不合法时立即失败；之前已完成的
        /// 单点采样不能回滚，不返回虚假的跨点原子快照。
        /// </summary>
        public async Task<PADPlcScanResult> ScanAsync(
            IEnumerable<string> stableIds, CancellationToken cancellationToken)
        {
            if (stableIds == null) throw new ArgumentNullException("stableIds");
            string[] selected = stableIds.ToArray();
            if (selected.Length == 0 || selected.Length > 64)
                throw new ArgumentOutOfRangeException("stableIds", "单次扫描必须包含1～64个点");
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string stableId in selected)
            {
                if (string.IsNullOrWhiteSpace(stableId) || !unique.Add(stableId))
                    throw new ArgumentException("PLC扫描点位不能为空或重复", "stableIds");
                Resolve(stableId);
                if (_catalog.FindById(stableId) == null)
                    throw new InvalidOperationException("PLC扫描设备定义已不存在: " + stableId);
            }

            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string generation = _connector.Channel.SessionId.ToString("N");
                List<PADPlcPointScanResult> records =
                    new List<PADPlcPointScanResult>(selected.Length);
                foreach (string stableId in selected)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_connector.Channel.SessionId.ToString("N") != generation)
                        throw new InvalidOperationException("PLC扫描过程连接代次变化，须重新建立会话");
                    try
                    {
                        object value = await ReadAndMapAsync(stableId, cancellationToken)
                            .ConfigureAwait(false);
                        records.Add(new PADPlcPointScanResult(stableId, value, null));
                    }
                    catch (Exception error) when (
                        error is TimeoutException || error is IOException ||
                        error is PADPlcOutcomeUnknownException)
                    {
                        // 上游 ReadAndMapAsync 已按真实失败标记 Stale，不生成新的Good采样。
                        // 只隔离允许恢复的单点协议失败；配置漂移、权限和取消仍中断扫描。
                        if (_connector.Channel.SessionId.ToString("N") != generation)
                            throw new InvalidOperationException("PLC扫描过程连接代次变化，禁止继续请求", error);
                        string code = error is TimeoutException ? "Timeout" :
                            (error is IOException ? "ProtocolIo" : "OutcomeUnknown");
                        records.Add(new PADPlcPointScanResult(stableId, null, code));
                    }
                }
                return new PADPlcScanResult(records);
            }
            finally
            {
                _writeGate.Release();
            }
        }

        /// <summary>
        /// 同一PLC站点、按地址连续且已批准的点通过单个FC03帧采样。
        /// MPA负责协议地址与回包，PAD负责稳定ID/配置/Session二次校验，
        /// 通过后才将逐点数据提交给唯一MDS ProviderHost。
        /// 不使用另一套Current，逐点MDS发布不承诺跨点原子事务。
        /// </summary>
        public async Task<PADPlcScanResult> ScanContiguousAsync(
            IEnumerable<string> stableIds, CancellationToken cancellationToken)
        {
            if (stableIds == null) throw new ArgumentNullException("stableIds");
            string[] selected = stableIds.ToArray();
            if (selected.Length < 1 || selected.Length > 64)
                throw new ArgumentOutOfRangeException("stableIds", "单帧扫描必须包含1～64个已批准点");
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
            string[] pointNames = new string[selected.Length];
            PADDeviceVariableDefinition[] definitions =
                new PADDeviceVariableDefinition[selected.Length];
            for (int i = 0; i < selected.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(selected[i]) || !unique.Add(selected[i]))
                    throw new ArgumentException("连续扫描点位不能为空或重复", "stableIds");
                pointNames[i] = Resolve(selected[i]);
                definitions[i] = _catalog.FindById(selected[i]);
                if (definitions[i] == null)
                    throw new InvalidOperationException("设备变量定义不存在: " + selected[i]);
            }

            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string generation = _connector.Channel.SessionId.ToString("N");
                // 协议Owner在发包前校验同一Station/地址严格连续/不越界。
                // 数据仅以局部数组返回，任意报文错误不会发布半批MDS值。
                IReadOnlyList<object> values;
                try
                {
                    values = await _connector.ReadContiguousPointsAsync(
                        pointNames, _destination, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (generation != _connector.Channel.SessionId.ToString("N") ||
                        values == null || values.Count != selected.Length)
                        throw new PADPlcOutcomeUnknownException(pointNames[0],
                            "PLC连续批读响应的会话或点数量已变化");
                    for (int i = 0; i < selected.Length; i++)
                    {
                        if (Resolve(selected[i]) != pointNames[i] ||
                            !definitions[i].ValueType.IsInstanceOfType(values[i]))
                            throw new PADPlcOutcomeUnknownException(pointNames[i],
                                "PLC连续批读配置或返回类型发生漂移，禁止提交MDS");
                    }
                }
                catch
                {
                    // 未能可信确认该FC03批读时，旧样本保留但不能继续展示Good。
                    // 质量不是新采样，不修改MDS的Current/RefreshUtc。
                    foreach (string id in selected) MarkStale(id);
                    throw;
                }
                PADDeviceVariableProvider publisher = Volatile.Read(ref _qualityProvider);
                if (publisher == null)
                    throw new InvalidOperationException("PLC设备采样Provider未装配");
                List<PADPlcPointScanResult> results =
                    new List<PADPlcPointScanResult>(selected.Length);
                for (int i = 0; i < selected.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!publisher.Emit(selected[i], values[i],
                        CxVisual.Mechanism.Data.Variables.MDSVariableQuality.Good))
                        throw new InvalidOperationException("PLC设备Provider未运行: " + selected[i]);
                    results.Add(new PADPlcPointScanResult(selected[i], values[i], null));
                }
                return new PADPlcScanResult(results);
            }
            finally { _writeGate.Release(); }
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

        /// <summary>宿主在整个采集会话失效时标记已采样数据不可供控制使用。</summary>
        public bool TransportOpen { get { return _transport.State == FTRChannelState.Open; } }

        public void InvalidateSamples()
        {
            foreach (string id in _bindings.Keys) MarkStale(id);
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
            {
                // 首轮扫描预检也应传播质量告警，而不是留下上次Good供控制读取。
                // 不产生新的物理采样、不覆盖MDS Current或RefreshUtc。
                MarkStale(stableId);
                throw new InvalidOperationException("PLC运行点表已变化，须停止会话并重新审批装配: " + point);
            }
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
