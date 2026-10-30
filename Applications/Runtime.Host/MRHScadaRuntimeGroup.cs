using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
        private readonly Func<bool> _deploymentApproval;
        private readonly string _projectPath, _configRoot;
        private readonly DateTime _projectStamp;
        private readonly MRHScadaDeviceLaunch[] _launches;
        private readonly string[] _connectorVersions, _channelIds;
        private readonly object _devicesGate = new object();
        private bool _started, _stopped, _disposed;
        private Task _stopTask;

        private MRHScadaRuntimeGroup(List<MRHScadaRuntime> devices,string deploymentVersion,
            Func<bool> approval,string projectPath,string configRoot,
            MRHScadaDeviceLaunch[] launches)
        {
            _devices=devices;_deploymentVersion=deploymentVersion;
            _deploymentApproval=approval;_projectPath=Path.GetFullPath(projectPath);
            _configRoot=configRoot;_projectStamp=File.GetLastWriteTimeUtc(_projectPath);
            _launches=(MRHScadaDeviceLaunch[])launches.Clone();
            _connectorVersions=devices.Select(x=>x.ConfigVersion).ToArray();
            _channelIds=devices.Select(x=>x.Device.ChannelId).ToArray();
        }

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
            // MHSConfigStorage.Initialize使用独占write.lock，即使只读OpenExisting亦如此。
            // 多PLC的FCR周期回调会同时检查同一项目级Deployment；若不串行化，
            // 互相抢占同一FileShare.None锁并把暂时的IOException误认为版本漂移。
            // 只串行化这一项目Effective核验，不锁设备扫描、协议或MDS Current。
            object deploymentReadGate=new object();
            Func<bool> approved=()=>
            {
                lock(deploymentReadGate)
                {
                    try
                    {
                        var live=new MHSConfigService(configRoot)
                            .OpenExisting<PADScadaDeploymentConfiguration>(identity);
                        return !live.HasPendingApply &&
                            string.Equals(live.EffectiveVersion,version,StringComparison.Ordinal);
                    }
                    catch {return false;}
                }
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
                // 检查每台设备的Connector Effective和MEM文件戳是否仍属于
                // 同一次有效装配；不能仅看项目级部署Effective。
                if(prepared.Any(x=>!x.IsStillApproved))
                    throw new InvalidOperationException(
                        "SCADA设备组准备期间Connector或MEM工程版本漂移，拒绝混版启动");
                return new MRHScadaRuntimeGroup(prepared,deploymentVersion,
                    additionalApproval,projectPath,configRoot,configs);
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
        internal bool IsDeploymentApproved {get{return !_disposed &&
            (_deploymentApproval==null||_deploymentApproval());}}
        internal MRHScadaRuntime[] SnapshotDevices()
        {
            lock(_devicesGate)return _devices.Where(x=>x!=null).ToArray();
        }
        // 在只读Pipe读取当前MDS与设备身份时禁止并发卸载此设备。
        // 不锁FC03采集热路径，也不持有第二份Current。
        internal TResult ReadDevices<TResult>(Func<MRHScadaRuntime[],TResult> reader)
        {
            if(reader==null)throw new ArgumentNullException("reader");
            lock(_devicesGate)
                return reader(_devices.Where(x=>x!=null).ToArray());
        }
        internal int DetachFaulted(MRHScadaRuntime failed)
        {
            if(failed==null)throw new ArgumentNullException("failed");
            int index;
            lock(_devicesGate)
            {
                if(!_started||_stopped||_disposed)
                    throw new InvalidOperationException("SCADA设备组已停止，不允许热恢复");
                index=_devices.IndexOf(failed);
                if(index<0||!failed.Completion.IsCompleted)
                    throw new InvalidOperationException("只能卸载组内真实结束的故障世代");
                _devices[index]=null; // Pipe发现期间设备暂时不可用，不复用旧Good。
            }
            failed.Dispose(); // 在读Pipe临界区之后释放旧MDS/Channel
            return index;
        }
        internal async Task<MRHScadaRuntime> StartReplacementAsync(int index,
            string oldGeneration,CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MRHScadaDeviceLaunch launch;
            lock(_devicesGate)
            {
                if(!_started||_stopped||_disposed||index<0||index>=_devices.Count||
                    _devices[index]!=null)
                    throw new InvalidOperationException("SCADA替换设备槽位尚未就绪");
                launch=_launches[index];
            }
            if(!IsDeploymentApproved||!File.Exists(_projectPath)||
                File.GetLastWriteTimeUtc(_projectPath)!=_projectStamp)
                throw new InvalidOperationException("SCADA工程部署或保存时间变化，不能自动恢复");
            MRHScadaRuntime fresh=MRHScadaRuntime.Open(_projectPath,_configRoot,
                launch.DeviceNodeId,launch.ConnectorModuleId,launch.ScopeId,
                launch.IntervalMs,launch.Contiguous,_deploymentApproval);
            try
            {
                if(fresh.ConfigVersion!=_connectorVersions[index]||
                    fresh.Device.ChannelId!=_channelIds[index])
                    throw new InvalidOperationException(
                        "SCADA Connector Effective或Channel身份变化，拒绝单设备恢复");
                await fresh.StartAsync(cancellationToken).ConfigureAwait(false);
                if(fresh.Generation==oldGeneration||!fresh.IsRunning||
                    !fresh.IsStillApproved||!IsDeploymentApproved||
                    File.GetLastWriteTimeUtc(_projectPath)!=_projectStamp)
                    throw new InvalidOperationException(
                        "SCADA新设备世代或正式工程配置尚未通过校验");
                lock(_devicesGate)
                {
                    if(_stopped||_disposed||_devices[index]!=null)
                        throw new InvalidOperationException("SCADA替换期间设备组已变化");
                    _devices[index]=fresh;
                }
                return fresh;
            }
            catch
            {
                fresh.Dispose();
                throw;
            }
        }
        internal MRHScadaRuntime Find(Guid deviceNodeId)
        {
            string key=deviceNodeId.ToString("N");
            MRHScadaRuntime device;
            lock(_devicesGate)device=_devices.FirstOrDefault(x=>x!=null&&x.DeviceRef==key);
            if(device==null)throw new KeyNotFoundException("SCADA工程设备不存在: "+key);
            return device;
        }
        internal async Task StartAsync(CancellationToken cancellationToken)
        {
            lock(_devicesGate)
            {
                if(_started||_stopped||_disposed)
                    throw new InvalidOperationException("SCADA设备组不可重复启动");
                _started=true;
            }
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

        internal Task StopAsync()
        {
            lock(_devicesGate)
            {
                // 多线程同一Stop共享完成任务，不能让第二个Caller在IO结束前返回。
                if(_stopTask!=null)return _stopTask;
                _stopped=true;
                _stopTask=StopCoreAsync(_devices.Where(x=>x!=null).ToArray());
                return _stopTask;
            }
        }
        private static async Task StopCoreAsync(MRHScadaRuntime[] devices)
        {
            List<Exception> failures=new List<Exception>();
            for(int i=devices.Length-1;i>=0;i--)
                try{await devices[i].StopAsync().ConfigureAwait(false);}
                catch(Exception error){failures.Add(error);}
            if(failures.Count>0)
                throw new AggregateException("SCADA设备组停止时存在故障",failures);
        }
        public void Dispose()
        {
            lock(_devicesGate)
            {
                if(_disposed)return;
                _disposed=true;
            }
            try{StopAsync().GetAwaiter().GetResult();}catch{}
            MRHScadaRuntime[] devices=SnapshotDevices();
            for(int i=devices.Length-1;i>=0;i--)
                devices[i].Dispose();
        }
        internal static int Serve(string projectPath,string configRoot,string readPipeName = null)
        {
            using(MRHScadaRuntimeGroup group=OpenFromEngineering(projectPath,configRoot))
            using(CancellationTokenSource signal=new CancellationTokenSource())
            {
                ConsoleCancelEventHandler onCancel=(sender,e)=>
                {
                    e.Cancel=true;signal.Cancel();
                };
                Console.CancelKeyPress+=onCancel;
                Task pipeTask=null;
                try
                {
                    group.StartAsync(signal.Token).GetAwaiter().GetResult();
                    if(readPipeName!=null)
                        pipeTask=MRHScadaReadOnlyPipe.RunAsync(group,readPipeName,signal.Token);
                    Console.WriteLine("SCADA Group online. Devices="+group.Count+
                        " DeploymentEffective="+group.DeploymentVersion+
                        ". Ctrl+C stops all. Read-only; no command replay.");
                    Task cancellation=Task.Delay(Timeout.Infinite,signal.Token);
                    Task[] watchers=_devicesCompletion(group);
                    Task[] all=watchers.Concat(new[]{cancellation})
                        .Concat(pipeTask==null?new Task[0]:new[]{pipeTask}).ToArray();
                    Task terminal=Task.WhenAny(all).GetAwaiter().GetResult();
                    if(terminal==pipeTask)
                    {
                        try{pipeTask.GetAwaiter().GetResult();}catch(Exception error)
                        {Console.Error.WriteLine("SCADA Pipe异常: "+error.Message);}
                        return 3;
                    }
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
                finally
                {
                    signal.Cancel();
                    if(pipeTask!=null)
                        try{pipeTask.GetAwaiter().GetResult();}
                        catch(OperationCanceledException){}
                        catch(ObjectDisposedException){}
                    Console.CancelKeyPress-=onCancel;
                }
            }
        }
        private static Task[] _devicesCompletion(MRHScadaRuntimeGroup group)
        {
            return group.SnapshotDevices().Select(x=>x.Completion).ToArray();
        }

    }
}
