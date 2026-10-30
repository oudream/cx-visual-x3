using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Professional.Automation.Scada;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Runtime.Host
{
    /// <summary>MEM持久DeviceRef与MHS Connector/Scope的一次不可变部署装配指令。
    /// 不存规约地址、采样值或另一个工程树；点表仍来自MHS Effective。</summary>
    internal sealed class MRHScadaDeviceLaunch
    {
        internal MRHScadaDeviceLaunch(Guid deviceNodeId,string connectorModuleId,
            string scopeId,int intervalMs,bool contiguous)
        {
            if(deviceNodeId==Guid.Empty||string.IsNullOrWhiteSpace(connectorModuleId)||
                string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("SCADA组装配身份或Scope不合法");
            if(intervalMs<20||intervalMs>3600000)
                throw new ArgumentOutOfRangeException("intervalMs");
            DeviceNodeId=deviceNodeId;ConnectorModuleId=connectorModuleId.Trim();
            ScopeId=scopeId.Trim();IntervalMs=intervalMs;Contiguous=contiguous;
        }
        internal Guid DeviceNodeId {get;private set;}
        internal string ConnectorModuleId {get;private set;}
        internal string ScopeId {get;private set;}
        internal int IntervalMs {get;private set;}
        internal bool Contiguous {get;private set;}
    }

    /// <summary>
    /// 一个进程装配多套已经存在的SCADA设备Session。唯一数据Owner仍是每个MDS Registry，
    /// 连接所有权仍在各自MCN/F17；本类仅负责身份冲突门禁及启动/停止补偿。
    /// 不管理报警/趋势、第二套调度器或跨设备事务。
    /// </summary>
    internal sealed class MRHScadaRuntimeGroup : IDisposable
    {
        private readonly List<MRHScadaRuntime> _devices;
        private readonly string _deploymentVersion;
        private bool _started, _stopped, _disposed;

        private MRHScadaRuntimeGroup(List<MRHScadaRuntime> devices,string deploymentVersion)
        { _devices=devices;_deploymentVersion=deploymentVersion; }

        /// <summary>正式入口：MEM ProjectId关联MHS已持久发布的SCADA部署配置。</summary>
        internal static MRHScadaRuntimeGroup OpenFromEngineering(
            string projectPath,string configRoot)
        {
            string projectId;
            using(MEMEngineeringSession project=MEMEngineering.Open(projectPath))
                projectId=project.Project.Id.ToString("N");
            FCMConfigIdentity identity=new FCMConfigIdentity(
                "CxVisual.Automation.ScadaDeployment","project",projectId);
            MHSConfigService mhs=new MHSConfigService(configRoot);
            var section=mhs.OpenExisting<PADScadaDeploymentConfiguration>(identity);
            if(section.HasPendingApply)
                throw new InvalidOperationException(
                    "SCADA工程部署存在待生效变更，禁止以旧点表装配");
            PADScadaDeploymentConfiguration deployment;
            string version;
            using(var pinned=section.CaptureForTask("scada-group:"+projectId))
            {
                deployment=pinned.Get();
                version=pinned.Version;
            }
            deployment.Validate();
            // 重新OpenExisting而不是读取缓存Section：另一个Studio进程提交也必须在发包前拒绝。
            Func<bool> approved=()=>
            {
                try
                {
                    var live=new MHSConfigService(configRoot)
                        .OpenExisting<PADScadaDeploymentConfiguration>(identity);
                    return !live.HasPendingApply &&
                        string.Equals(live.EffectiveVersion,version,StringComparison.Ordinal);
                }
                catch {return false;}
            };
            MRHScadaDeviceLaunch[] items=deployment.Devices.Where(x=>x.Enabled)
                .Select(x=>new MRHScadaDeviceLaunch(Guid.Parse(x.DeviceNodeGuid),
                    x.ConnectorModuleId,x.ScopeId,x.IntervalMs,x.Contiguous)).ToArray();
            return Open(projectPath,configRoot,items,approved,version);
        }

        internal static MRHScadaRuntimeGroup Open(string projectPath,string configRoot,
            IEnumerable<MRHScadaDeviceLaunch> definitions,
            Func<bool> additionalApproval = null,string deploymentVersion = null)
        {
            if(definitions==null)throw new ArgumentNullException("definitions");
            MRHScadaDeviceLaunch[] configs=definitions.ToArray();
            if(configs.Length<1||configs.Length>32||configs.Any(x=>x==null))
                throw new ArgumentException("运行Host必须有1～32个明确设备装配");
            if(configs.Select(x=>x.DeviceNodeId).Distinct().Count()!=configs.Length ||
                configs.Select(x=>x.ScopeId).Distinct(StringComparer.Ordinal).Count()!=configs.Length ||
                configs.Select(x=>x.ConnectorModuleId).Distinct(StringComparer.Ordinal).Count()!=configs.Length)
                throw new InvalidOperationException(
                    "SCADA双设备不能复用DeviceRef、MDS Scope或Connector配置身份");
            if(additionalApproval!=null&&!additionalApproval())
                throw new InvalidOperationException("SCADA工程部署Effective已变化");
            List<MRHScadaRuntime> prepared=new List<MRHScadaRuntime>();
            try
            {
                HashSet<string> channels=new HashSet<string>(StringComparer.Ordinal);
                foreach(MRHScadaDeviceLaunch config in configs)
                {
                    MRHScadaRuntime runtime=MRHScadaRuntime.Open(projectPath,configRoot,
                        config.DeviceNodeId,config.ConnectorModuleId,
                        config.ScopeId,config.IntervalMs,config.Contiguous,
                        additionalApproval);
                    prepared.Add(runtime);
                    if(!channels.Add(runtime.Device.ChannelId))
                        throw new InvalidOperationException(
                            "SCADA两个设备复用了同一F17 ChannelId，拒绝装配");
                }
                if(additionalApproval!=null&&!additionalApproval())
                    throw new InvalidOperationException("SCADA设备组准备期间部署版本漂移");
                return new MRHScadaRuntimeGroup(prepared,deploymentVersion);
            }
            catch
            {
                for(int i=prepared.Count-1;i>=0;i--)
                    try{prepared[i].Dispose();}catch{}
                throw;
            }
        }

        internal string DeploymentVersion {get{return _deploymentVersion;}}
        internal int Count {get{return _devices.Count;}}
        internal MRHScadaRuntime Find(Guid deviceNodeId)
        {
            string key=deviceNodeId.ToString("N");
            MRHScadaRuntime device=_devices.FirstOrDefault(x=>x.DeviceRef==key);
            if(device==null)throw new KeyNotFoundException("SCADA工程设备不存在: "+key);
            return device;
        }
        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            if(_started||_stopped||_disposed)
                throw new InvalidOperationException("SCADA设备组不可重复启动");
            _started=true;
            try
            {
                foreach(MRHScadaRuntime device in _devices)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await device.StartAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                try{await StopAsync().ConfigureAwait(false);}catch{}
                throw;
            }
        }

        internal async Task StopAsync()
        {
            if(_stopped)return;
            _stopped=true;
            List<Exception> failures=new List<Exception>();
            // 先对所有在途采集发停止，再释放，单个Device故障不阻断其余设备关闭。
            for(int i=_devices.Count-1;i>=0;i--)
                try{await _devices[i].StopAsync().ConfigureAwait(false);}
                catch(Exception error){failures.Add(error);}
            if(failures.Count>0)
                throw new AggregateException("SCADA设备组停止时存在故障",failures);
        }
        public void Dispose()
        {
            if(_disposed)return;
            _disposed=true;
            try{StopAsync().GetAwaiter().GetResult();}catch{}
            for(int i=_devices.Count-1;i>=0;i--)
                _devices[i].Dispose();
        }
        internal static int Serve(string projectPath,string configRoot)
        {
            using(MRHScadaRuntimeGroup group=OpenFromEngineering(projectPath,configRoot))
            using(CancellationTokenSource signal=new CancellationTokenSource())
            {
                ConsoleCancelEventHandler onCancel=(sender,e)=>
                {
                    e.Cancel=true;signal.Cancel();
                };
                Console.CancelKeyPress+=onCancel;
                try
                {
                    group.StartAsync(signal.Token).GetAwaiter().GetResult();
                    Console.WriteLine("SCADA Group online. Devices="+group.Count+
                        " DeploymentEffective="+group.DeploymentVersion+
                        ". Ctrl+C stops all. Read-only; no command replay.");
                    Task cancellation=Task.Delay(Timeout.Infinite,signal.Token);
                    Task[] watchers=_devicesCompletion(group);
                    Task[] all=watchers.Concat(new[]{cancellation}).ToArray();
                    Task terminal=Task.WhenAny(all).GetAwaiter().GetResult();
                    if(terminal==cancellation)
                    {
                        group.StopAsync().GetAwaiter().GetResult();
                        return 0;
                    }
                    try{terminal.GetAwaiter().GetResult();}
                    catch(Exception error)
                    {
                        Console.Error.WriteLine("SCADA device fault: "+error.Message);
                        return 3;
                    }
                    Console.Error.WriteLine("SCADA device unexpectedly ended its collection.");
                    return 3;
                }
                catch(OperationCanceledException) when(signal.IsCancellationRequested)
                {
                    return 0;
                }
                finally {Console.CancelKeyPress-=onCancel;}
            }
        }
        private static Task[] _devicesCompletion(MRHScadaRuntimeGroup group)
        {
            return group._devices.Select(x=>x.Completion).ToArray();
        }

    }
}
