using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Connectivity;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Data.Configuration;
using CxVisual.Mechanism.Data.Variables;
using CxVisual.Professional.Automation.Scada;
using CxVisual.Professional.Automation.DeviceVariables;
using CxVisual.Mechanism.Runtime.Commands;

namespace CxVisual.Runtime.Host
{
    /// <summary>
    /// 独立于Studio/HMI的SCADA读取宿主：按MEM工程Guid选中持久资源，
    /// 以MHS正式Effective装配PLC，发布到唯一MDS实例，并在后台连续采样。
    /// 控制默认关闭；不使用MRHSimulationPublishPolicy、不接受不可信自动发布。
    /// 现有受信MRK Plan/Journaling仍在独立既有Host中，不能据此称为已合并内核。
    /// </summary>
    internal sealed class MRHScadaRuntime : IDisposable
    {
        private readonly string _projectPath;
        private readonly DateTime _projectStamp;
        private readonly MHSConfigSection<MCNConnectorConfiguration> _section;
        private readonly string _effectiveVersion;
        private readonly PADScadaDeviceSession _device;
        private readonly Func<bool> _stillApproved;
        private bool _stopped;

        private MRHScadaRuntime(string projectPath,DateTime stamp,
            MHSConfigSection<MCNConnectorConfiguration> section,
            string version,PADScadaDeviceSession device,Func<bool> stillApproved)
        {
            _projectPath=projectPath;_projectStamp=stamp;
            _section=section;_effectiveVersion=version;_device=device;
            _stillApproved=stillApproved;
        }

        internal static MRHScadaRuntime Open(string projectPath,string configRoot,
            Guid deviceNodeId,string connectorModuleId,string scopeId,
            int intervalMs,bool contiguous,Func<bool> additionalApproval = null,
            Func<string,PADDeviceVariableDefinition,bool> authorizeWrite = null)
        {
            if(authorizeWrite!=null && additionalApproval==null)
                throw new UnauthorizedAccessException(
                    "scada.command.trusted_project_deployment_approval_required");
            if(string.IsNullOrWhiteSpace(projectPath)||!File.Exists(projectPath))
                throw new FileNotFoundException("SCADA需要由Studio正式保存的MEM工程");
            if(deviceNodeId==Guid.Empty)
                throw new ArgumentException("SCADA需要真实MEM Device资源Guid");
            if(string.IsNullOrWhiteSpace(configRoot)||
                string.IsNullOrWhiteSpace(connectorModuleId)||
                string.IsNullOrWhiteSpace(scopeId))
                throw new ArgumentException("SCADA缺少配置根、Connector身份或MDS Scope");
            string source=Path.GetFullPath(projectPath);
            DateTime stamp=File.GetLastWriteTimeUtc(source);
            string deviceRef;
            using(MEMEngineeringSession engineering=MEMEngineering.Open(source))
            {
                MEMNode node=engineering.Find(deviceNodeId);
                if(node==null||!(node is IMEMResource))
                    throw new InvalidOperationException(
                        "DeviceRef未绑定MEM工程中的真实持久资源，拒绝启动");
                deviceRef=node.Id.ToString("N");
            }
            MHSConfigService config=new MHSConfigService(configRoot);
            MHSConfigSection<MCNConnectorConfiguration> section=
                config.OpenExisting<MCNConnectorConfiguration>(new FCMConfigIdentity(
                    "CxVisual.Module.Connector","module",connectorModuleId));
            if(section.HasPendingApply)
                throw new InvalidOperationException("SCADA Connector存在未生效草稿，禁止启动");
            MCNConnectorConfiguration effective;
            string version;
            using(MHSConfigSnapshot<MCNConnectorConfiguration> snapshot=
                section.CaptureForTask("scada:"+deviceRef))
            {
                version=snapshot.Version;
                effective=snapshot.Get();
            }
            if(stamp!=File.GetLastWriteTimeUtc(source))
                throw new InvalidOperationException("SCADA装载期间工程文件已变化");
            // 每次FC03发包前复核正式MHS Effective与MEM工程存盘时间。
            // 不是重建MHS版本管理器；只调用它提供的生效版本快照。
            // FCM/MHS版本可能由另一个Studio进程提交，不能仅检查本Host
            // 已缓存section对象。每轮通过正式MHS OpenExisting重新读磁盘版本；
            // 首批正确性优先，频繁批读的缓存/版本事件优化由MHS Owner裁决。
            // 同一个Connector可同时被采集轮询、Pipe诊断和恢复守卫读取。
            // MHS存储对OpenExisting会取独占文件锁；同Host内这些只读核验
            // 必须串行，避免把竞争锁失败误认为Connector配置真的发生漂移。
            object connectorReadGate=new object();
            Func<bool> stillApproved=()=>
            {
                lock(connectorReadGate)
                {
                    try
                    {
                        MHSConfigSection<MCNConnectorConfiguration> live=
                            new MHSConfigService(configRoot)
                                .OpenExisting<MCNConnectorConfiguration>(new FCMConfigIdentity(
                                    "CxVisual.Module.Connector","module",connectorModuleId));
                        return !live.HasPendingApply &&
                            string.Equals(live.EffectiveVersion,version,StringComparison.Ordinal) &&
                            File.Exists(source) && File.GetLastWriteTimeUtc(source)==stamp &&
                            (additionalApproval==null || additionalApproval());
                    }
                    catch { return false; }
                }
            };
            PADScadaDeviceSession session=new PADScadaDeviceSession(
                deviceRef,scopeId,connectorModuleId,version,effective,
                TimeSpan.FromMilliseconds(intervalMs),contiguous,
                authorizeWrite:authorizeWrite,effectiveStillApproved:stillApproved);
            return new MRHScadaRuntime(source,stamp,section,version,session,stillApproved);
        }

        internal string DeviceRef {get{return _device.DeviceRef;}}
        internal string ScopeId {get{return _device.ScopeId;}}
        internal string ConfigVersion {get{return _effectiveVersion;}}
        internal string Generation {get{return _device.SessionGeneration;}}
        internal bool IsRunning {get{return _device.IsRunning;}}
        internal bool IsStillApproved {get{return _stillApproved();}}
        internal long CompletedCycles {get{return _device.CompletedCycles;}}
        internal long PartialCycles {get{return _device.PartialCycles;}}
        internal PADScadaDeviceSession Device {get{return _device;}}
        /// <summary>
        /// Only an already authenticated and deployment-approved Host may opt
        /// into PLC commands before StartAsync. Default group/CLI/Smoke remain
        /// read-only. The existing MRK center owns authentication and leases;
        /// this method neither creates one nor opens a write IPC endpoint.
        /// </summary>
        internal IDisposable RegisterTrustedWriteCommands(MRKCommandCenter center,long commandId)
        {
            if(_stopped||!_stillApproved())
                throw new InvalidOperationException(
                    "scada.command.deployment_or_session_stale");
            return _device.RegisterWriteCommands(center,commandId);
        }
        internal Task Completion {get{return _device.Completion;}}
        internal Task StartAsync(CancellationToken cancellationToken)
        {
            if(_stopped)throw new InvalidOperationException("SCADA Host已关闭");
            return _device.StartAsync(cancellationToken);
        }
        internal Task StopAsync()
        {
            _stopped=true;
            return _device.StopAsync();
        }
        public void Dispose()
        {
            _stopped=true;
            _device.Dispose();
        }

        internal static int Serve(string projectPath,string configRoot,
            string guid,string moduleId,string scopeId,string intervalText,bool contiguous,
            bool serviceMode = false, bool recoveryMode = false)
        {
            Guid deviceId; int intervalMs;
            if(!Guid.TryParse(guid,out deviceId)||
                !int.TryParse(intervalText,System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,out intervalMs))
                throw new ArgumentException("SCADA需要有效的工程Device GUID和采样间隔毫秒整数");
            if(recoveryMode)
            {
                if(!serviceMode)
                    throw new ArgumentException("--recover requires --service (never replays a write)");
                return MRHScadaRecoverySupervisor.Serve(projectPath,configRoot,deviceId,
                    moduleId,scopeId,intervalMs,contiguous);
            }
            using(MRHScadaRuntime host=Open(projectPath,configRoot,deviceId,
                moduleId,scopeId,intervalMs,contiguous))
            {
                host.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine("SCADA running DeviceRef="+host.DeviceRef+
                    " Scope="+host.ScopeId+" Effective="+host.ConfigVersion+
                    " Generation="+host.Generation+" (read-only)");
                if(serviceMode)
                {
                    Console.WriteLine("SCADA service mode: no stdin dependency; Ctrl+C to stop.");
                    using(CancellationTokenSource signal=new CancellationTokenSource())
                    {
                        ConsoleCancelEventHandler onCancel=(sender,e)=>
                        {
                            e.Cancel=true;
                            signal.Cancel();
                        };
                        Console.CancelKeyPress+=onCancel;
                        try
                        {
                            Task shutdown=Task.Delay(Timeout.Infinite,signal.Token);
                            Task winner=Task.WhenAny(host.Completion,shutdown)
                                .GetAwaiter().GetResult();
                            if(winner==host.Completion)
                            {
                                try{host.Completion.GetAwaiter().GetResult();}
                                catch(Exception error)
                                {
                                    Console.Error.WriteLine("SCADA collection fault: "+error.Message);
                                    return 3;
                                }
                                return 0;
                            }
                            host.StopAsync().GetAwaiter().GetResult();
                            Console.WriteLine("SCADA service stopped");
                            return 0;
                        }
                        finally { Console.CancelKeyPress-=onCancel; }
                    }
                }
                Console.WriteLine("Commands: status | point <stablePointId> | stop");
                // Headless进程拥有TCP/MDS/采集，不依赖Studio窗口/进程。
                // 同时监听采样故障；不能因等待Console输入而无声继续报告Ready。
                while(true)
                {
                    Task<string> input=Task.Run(()=>Console.ReadLine());
                    Task winner=Task.WhenAny(input,host.Completion).GetAwaiter().GetResult();
                    if(winner==host.Completion)
                    {
                        try{host.Completion.GetAwaiter().GetResult();}
                        catch(Exception error)
                        {
                            Console.Error.WriteLine("SCADA collection stopped: "+error.Message);
                            return 3;
                        }
                        return 0;
                    }
                    string line=input.GetAwaiter().GetResult();
                    if(line==null || line.Trim()=="stop")
                    {
                        host.StopAsync().GetAwaiter().GetResult();
                        Console.WriteLine("SCADA stopped");
                        return 0;
                    }
                    if(line.Trim()=="status")
                    {
                        Console.WriteLine("Device="+host.DeviceRef+
                            " Running="+host.IsRunning+
                            " Generation="+host.Generation+
                            " Cycles="+host.CompletedCycles+
                            " Partial="+host.PartialCycles);
                    }
                    else if(line.StartsWith("point ",StringComparison.Ordinal))
                    {
                        string id=line.Substring(6).Trim();
                        IMDSVariableSnapshot sample=host.Device.Inspect(id);
                        Console.WriteLine("Point="+id+" Value="+
                            (sample.HasValue?Convert.ToString(sample.CurrentObject):"NoSample")+
                            " Quality="+sample.Quality+
                            " Revision="+sample.Revision+
                            " Time="+sample.RefreshUtc.ToString("O"));
                    }
                    else Console.WriteLine("Unknown command (read-only)");
                }
            }
        }
    }
}
