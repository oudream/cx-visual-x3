using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Transport;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Mechanism.Runtime.Commands;
using CxVisual.Professional.Automation.DeviceVariables;
using CxVisual.Professional.Automation.Plc;

namespace CxVisual.Professional.Automation.Scada
{
    /// <summary>
    /// 工程PLC设备的单一运行装配：一套正式Channel/Protocol/Connector，
    /// 一套MDS Scope/ProviderHost，一套受控PAD桥接和FCR采样。
    /// 配置真源是Host传入的MHS Effective快照；此类不保存第二套PointTable/Current。
    /// 设备身份由MEM工程资源固定，软件连接不证明真实硬件身份/安全状态。
    /// </summary>
    public sealed class PADScadaDeviceSession : IDisposable
    {
        private readonly string _deviceRef;
        private readonly string _scopeId;
        private readonly string _configVersion;
        private readonly MCNConnectorModule _connector;
        private readonly PADDeviceVariableCatalog _catalog;
        private readonly MDSVariableRegistry _registry;
        private readonly MDSVariableTable _variables;
        private readonly MDSVariableProviderHost _providerHost;
        private readonly PADPlcPointBridge _bridge;
        private readonly PADPlcPollingSession _polling;
        private readonly PADDeviceVariableProvider _provider;
        private readonly bool _writeAuthorizationConfigured;
        private readonly Func<bool> _effectiveStillApproved;
        private readonly object _gate = new object();
        private IDisposable _commandsBinding;
        private MDSVariableChangeDispatcher _observations;
        private bool _running;
        private bool _started;
        private bool _disposed;

        public PADScadaDeviceSession(string deviceRef, string scopeId,
            string moduleId, string configVersion, MCNConnectorConfiguration effective,
            TimeSpan interval, bool contiguous = false,
            Func<string,PADDeviceVariableDefinition,bool> authorizeWrite = null,
            Func<bool> effectiveStillApproved = null)
        {
            if (string.IsNullOrWhiteSpace(deviceRef) || string.IsNullOrWhiteSpace(scopeId) ||
                string.IsNullOrWhiteSpace(moduleId) || string.IsNullOrWhiteSpace(configVersion))
                throw new ArgumentException("SCADA必须固定工程DeviceRef、Scope、Module及EffectiveVersion");
            if (effective == null) throw new ArgumentNullException("effective");
            effective.Validate();
            if (!effective.Enabled || effective.Protocol.ProtocolTypeId != "modbus.tcp" ||
                !(effective.Channel is FTRTcpClientChannelConfiguration))
                throw new NotSupportedException("当前首批只支持已生效的Modbus TCP/TCP Client装配");
            _deviceRef=deviceRef.Trim();
            _writeAuthorizationConfigured=authorizeWrite!=null;
            _effectiveStillApproved=effectiveStillApproved;
            _scopeId=scopeId.Trim();
            _configVersion=configVersion.Trim();
            _catalog=new PADDeviceVariableCatalog("scada/"+_deviceRef);
            List<PADPlcPointBinding> bindings = new List<PADPlcPointBinding>();
            foreach (MPAPointDefinition point in effective.Protocol.Points)
            {
                if (!point.CanRead) continue;
                Type valueType=point.ValueType=="UInt16" ? typeof(ushort) :
                    (point.ValueType=="Int16" ? typeof(short) : null);
                if(valueType==null)
                    throw new NotSupportedException("SCADA首批不支持点类型: "+point.ValueType);
                MPADataMapping[] mapped=effective.Protocol.Mappings
                    .Where(m=>m.PointRef==point.Name).ToArray();
                MPADataMapping[] inbound=mapped.Where(m=>
                    m.TargetKind==MPADataTargetKind.Variable &&
                    m.Direction==MPADataDirection.Inbound &&
                    m.Operation==MPADataOperation.WriteValue &&
                    m.Transform==MPADataTransformKind.None && m.Required).ToArray();
                if(inbound.Length!=1)
                    throw new InvalidOperationException("SCADA点必须有唯一已批准MDS入站映射: "+point.Name);
                string prefix=_scopeId+"/";
                if(!inbound[0].TargetRef.StartsWith(prefix,StringComparison.Ordinal))
                    throw new InvalidOperationException("SCADA点映射越过已批准MDS Scope: "+point.Name);
                string variableAddress=inbound[0].TargetRef.Substring(prefix.Length);
                int dot=variableAddress.IndexOf('.');
                if(dot<=0||dot==variableAddress.Length-1 ||
                    variableAddress.IndexOf('.',dot+1)>=0)
                    throw new InvalidOperationException("SCADA MDS目标应为设备名.变量名: "+point.Name);
                string deviceName=variableAddress.Substring(0,dot);
                string variableName=variableAddress.Substring(dot+1);
                _catalog.Register(new PADDeviceVariableDefinition(point.Name,
                    deviceName,variableName,valueType,null,point.CanWrite));
                bindings.Add(new PADPlcPointBinding(point.Name,point.Name));
            }
            if(bindings.Count==0)throw new ArgumentException("SCADA生效点表无任何可采集点");
            if(contiguous && bindings.Count>64)
                throw new ArgumentException("连续FC03扫描不能超过64点");
            if(bindings.Count>64)
                throw new ArgumentException("首批单设备最多64个点，须显式拆分采集组");

            _registry=new MDSVariableRegistry();
            _variables=new MDSVariableTable(_scopeId);
            _catalog.InstallDefinitions(_variables);
            _registry.Register(_variables);
            _providerHost=new MDSVariableProviderHost(_registry);
            _connector=new MCNConnectorModule(
                effective.Describe(moduleId,MFMModuleKind.Connector),effective,
                new FTRTcpClientChannel(effective.Channel),new MPAModbusTcpProtocol());
            _bridge=new PADPlcPointBridge(_connector,_catalog,
                new FTRSubChannelAddress(effective.Channel.Id),_scopeId,bindings);
            // 首次装配只读。命令写入必须由外部真实授权Host另行显式装配。
            _provider=_bridge.CreateWriteProvider(authorizeWrite);
            _providerHost.Register(_provider);
            _catalog.Bind(_providerHost,_scopeId);
            _polling=new PADPlcPollingSession(_bridge,bindings.Select(b=>b.StableId),
                interval,contiguous,effectiveStillApproved);
        }

        public string DeviceRef {get{return _deviceRef;}}
        public string ScopeId {get{return _scopeId;}}
        public string ConfigVersion {get{return _configVersion;}}
        public string SessionGeneration {get{return _connector.Channel.SessionId.ToString("N");}}
        public bool IsRunning {get{lock(_gate)return _running&&_polling.IsStarted;}}
        public long CompletedCycles {get{return _polling.CompletedCycles;}}
        public long SuccessfulCycles {get{return _polling.SuccessfulCycles;}}
        public long PartialCycles {get{return _polling.PartialCycles;}}
        public Task Completion {get{return _polling.Completion;}}
        public MDSVariableRegistry DataRegistry {get{return _registry;}}
        public MDSVariableTable DataTable {get{return _variables;}}
        public PADDeviceVariableDefinition[] PointDefinitions {get{return _catalog.Snapshot();}}
        public IMDSVariableSnapshot Inspect(string stablePointId)
        {
            PADDeviceVariableDefinition def=_catalog.FindById(stablePointId);
            if(def==null)throw new KeyNotFoundException("PLC稳定点不存在: "+stablePointId);
            return _variables.Inspect(def.Address);
        }
        /// <summary>
        /// HMI与专业观察者复用唯一MDS的有界LatestOnly派发器；
        /// 按稳定VariableId过滤，不创建第二份Current，也不在PLC IO线程运行UI回调。
        /// 回调并非WPF UI线程；用完必须Dispose订阅。
        /// </summary>
        public IDisposable Subscribe(Action<IReadOnlyList<MDSVariableChange>> onChanges,
            IEnumerable<string> stablePointIds = null)
        {
            if(onChanges==null)throw new ArgumentNullException("onChanges");
            string[] ids=stablePointIds==null?null:stablePointIds.ToArray();
            if(ids!=null)
                foreach(string id in ids)
                    if(_catalog.FindById(id)==null)
                        throw new KeyNotFoundException("SCADA订阅了未绑定的点: "+id);
            lock(_gate)
            {
                if(_disposed)throw new ObjectDisposedException("PADScadaDeviceSession");
                if(_observations==null)_observations=_variables.CreateDispatcher(1024);
                return _observations.Subscribe(onChanges,ids);
            }
        }

        /// <summary>
        /// HMI与专业服务只能向已配置可信授权器的MRK提交命令。空授权器没有降级入口。
        /// 本会话启动前才可注册控制；只读宿主永不创建此绑定。
        /// </summary>
        public IDisposable RegisterWriteCommands(MRKCommandCenter hostCenter,long commandId)
        {
            if(hostCenter==null)throw new ArgumentNullException("hostCenter");
            lock(_gate)
            {
                if(_started||_disposed||_commandsBinding!=null)
                    throw new InvalidOperationException("SCADA会话命令装配已冻结");
                if(!_writeAuthorizationConfigured)
                    throw new UnauthorizedAccessException(
                        "SCADA未注入专业写入授权器，禁止注册设备命令");
                // Host须创建具有真实身份授权的MRKCommandCenter；本层不创建第二个MRK。
                _commandsBinding=PADPlcMrkCommandRegistration.RegisterUInt16Write(
                    hostCenter,_provider,_deviceRef,commandId,_configVersion,
                    _effectiveStillApproved);
                return _commandsBinding;
            }
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            lock(_gate)
            {
                if(_started||_disposed)throw new InvalidOperationException("SCADA会话不可二次启动");
                _started=true;
            }
            try
            {
                await _connector.StartAsync(cancellationToken).ConfigureAwait(false);
                _providerHost.Start();
                _polling.Start(true,cancellationToken);
                _polling.Completion.ContinueWith(task =>
                {
                    // 配置漂移、连接/会话失效均必须令旧样本Stale。
                    // 仅质量变更，不伪造新采样/RefreshUtc。
                    if(task.IsFaulted) _bridge.InvalidateSamples();
                },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,
                  TaskScheduler.Default);
                lock(_gate)_running=true;
            }
            catch
            {
                try {_providerHost.Stop();}catch{}
                if(_connector.IsStarted)
                    try {await _connector.StopAsync(CancellationToken.None).ConfigureAwait(false);}catch{}
                throw;
            }
        }
        public async Task StopAsync()
        {
            lock(_gate)_running=false;
            try { await _polling.StopAsync().ConfigureAwait(false); }
            finally
            {
                // Channel Stop时Provider仍活跃，桥接才可将旧MDS样本标Stale；
                // 不允许先Stop Provider丢弃故障质量事件。
                try
                {
                    if(_connector.IsStarted)
                        await _connector.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
                finally
                {
                    _bridge.InvalidateSamples();
                    _providerHost.Stop();
                }
            }
        }
        public void Dispose()
        {
            lock(_gate)
            {
                if(_disposed)return;
                _disposed=true;
            }
            // 运行故障已由Completion向Host暴露；Dispose要负责彻底释放资源，
            // 不能因重复抛出既有故障而跳过Provider/Scope注销。
            try {StopAsync().GetAwaiter().GetResult();}
            catch (Exception) { /* 轮询故障由Completion保留，清理继续 */ }
            finally
            {
                _bridge.Dispose();
                if(_observations!=null)_observations.Dispose();
                _providerHost.Dispose();
                if(_commandsBinding!=null)_commandsBinding.Dispose();
                _registry.Remove(_scopeId);
            }
        }
    }
}
