using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Professional.Automation.Plc;

namespace CxVisual.Runtime.Host
{
    /// <summary>
    /// 项目级多PLC的显式只读有界恢复。每次由MEM/MHS Effective重新装配已有Group，
    /// 不创建新轮询器、Channel、MDS仓或命令中心。组中任意PLC通信失败时，
    /// 关闭整个旧组再重建，确保旧会话/订阅/IPC身份不会跨代次复用。
    /// 这是组级恢复，非单设备无中断热切换；其它设备会短暂中断采样。
    /// </summary>
    internal sealed class MRHScadaGroupRecoverySupervisor
    {
        private readonly string _projectPath;
        private readonly string _configRoot;
        private readonly string _readPipeName;
        private readonly int _maxAttempts;
        private readonly TimeSpan _baseDelay;
        private readonly object _identityGate = new object();
        private DateTime _pinnedProjectStamp;
        private string _pinnedDeploymentVersion;
        private Dictionary<string,string> _pinnedConnectors;
        private Dictionary<string,string> _lastGenerations;
        private int _started;
        private int _connectedGenerations;
        private int _recoveryAttempts;
        private string _lastFailure;

        internal MRHScadaGroupRecoverySupervisor(string projectPath,string configRoot,
            string readPipeName = null,int maxRecoveryAttempts = 3,
            int retryDelayMs = 250)
        {
            if(String.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath) ||
                String.IsNullOrWhiteSpace(configRoot))
                throw new ArgumentException("组恢复需要保存的MEM工程和MHS根目录");
            if(maxRecoveryAttempts<0 || maxRecoveryAttempts>12 ||
                retryDelayMs<20 || retryDelayMs>10000)
                throw new ArgumentOutOfRangeException("maxRecoveryAttempts/retryDelayMs");
            _projectPath=Path.GetFullPath(projectPath);
            _configRoot=configRoot;
            _readPipeName=readPipeName;
            _maxAttempts=maxRecoveryAttempts;
            _baseDelay=TimeSpan.FromMilliseconds(retryDelayMs);
        }
        internal int ConnectedGenerations
        {get{return Volatile.Read(ref _connectedGenerations);}}
        internal int RecoveryAttempts
        {get{return Volatile.Read(ref _recoveryAttempts);}}
        internal string LastFailure
        {get{return Volatile.Read(ref _lastFailure);}}

        private void VerifyGroupIdentity(MRHScadaRuntimeGroup group)
        {
            DateTime stamp=File.GetLastWriteTimeUtc(_projectPath);
            if(!group.IsDeploymentApproved)
                throw new InvalidOperationException("SCADA组部署Effective已失效，拒绝恢复");
            Dictionary<string,string> connectors=group.SnapshotDevices()
                .ToDictionary(x=>x.DeviceRef,x=>x.ConfigVersion,StringComparer.Ordinal);
            lock(_identityGate)
            {
                if(_pinnedConnectors==null)
                {
                    _pinnedConnectors=connectors;
                    _pinnedDeploymentVersion=group.DeploymentVersion;
                    _pinnedProjectStamp=stamp;
                }
                else if(stamp!=_pinnedProjectStamp ||
                    !String.Equals(group.DeploymentVersion,_pinnedDeploymentVersion,
                        StringComparison.Ordinal) ||
                    connectors.Count!=_pinnedConnectors.Count ||
                    connectors.Any(pair=>!_pinnedConnectors.ContainsKey(pair.Key) ||
                        _pinnedConnectors[pair.Key]!=pair.Value))
                    throw new InvalidOperationException(
                        "SCADA组恢复检测到MEM/MHS部署或Connector Effective漂移，禁止自动批准");
            }
        }

        // F17在Channel.Open时重新生成SessionId，必须在StartAsync结束后取真实世代。
        // 构造阶段的初始Guid不是已经打开的PLC连接身份证据。
        private void RecordActiveGenerations(MRHScadaRuntimeGroup group)
        {
            if(!group.IsDeploymentApproved ||
                group.SnapshotDevices().Any(x=>!x.IsRunning || !x.IsStillApproved))
                throw new InvalidOperationException("SCADA组世代尚未真实启动或已失效");
            Dictionary<string,string> generations=group.SnapshotDevices()
                .ToDictionary(x=>x.DeviceRef,x=>x.Generation,StringComparer.Ordinal);
            lock(_identityGate)
            {
                if(_lastGenerations!=null &&
                    generations.Any(pair=>_lastGenerations.ContainsKey(pair.Key) &&
                        _lastGenerations[pair.Key]==pair.Value))
                    throw new InvalidOperationException(
                        "SCADA组恢复复用了已打开的旧设备SessionGeneration");
                _lastGenerations=generations;
            }
        }

        internal async Task RunAsync(CancellationToken cancellationToken,
            Action<MRHScadaRuntimeGroup> onConnected = null)
        {
            if(Interlocked.Exchange(ref _started,1)!=0)
                throw new InvalidOperationException("SCADA组恢复监督器不可重复启动");
            cancellationToken.ThrowIfCancellationRequested();
            // 初始化冻结工程时间：装载之前修改也应被Host本身的Open守卫拒绝。
            DateTime initialStamp=File.GetLastWriteTimeUtc(_projectPath);
            while(true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    using(MRHScadaRuntimeGroup group=
                        MRHScadaRuntimeGroup.OpenFromEngineering(_projectPath,_configRoot))
                    using(CancellationTokenSource generationStop=
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        if(_pinnedConnectors==null && initialStamp!=
                            File.GetLastWriteTimeUtc(_projectPath))
                            throw new InvalidOperationException("初次装配期间MEM工程已变更");
                        VerifyGroupIdentity(group);
                        Task pipeTask=null;
                        try
                        {
                            await group.StartAsync(generationStop.Token).ConfigureAwait(false);
                            RecordActiveGenerations(group);
                            if(_readPipeName!=null)
                                pipeTask=MRHScadaReadOnlyPipe.RunAsync(
                                    group,_readPipeName,generationStop.Token);
                            Interlocked.Increment(ref _connectedGenerations);
                            if(onConnected!=null)onConnected(group);
                            Task cancellation=Task.Delay(Timeout.Infinite,generationStop.Token);
                            Task[] watchers=group.SnapshotDevices()
                                .Select(x=>x.Completion).ToArray();
                            Task[] all=watchers.Concat(new[]{cancellation})
                                .Concat(pipeTask==null?new Task[0]:new[]{pipeTask}).ToArray();
                            Task terminal=await Task.WhenAny(all).ConfigureAwait(false);
                            if(terminal==cancellation)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                throw new InvalidOperationException("SCADA组世代意外取消");
                            }
                            if(terminal==pipeTask)
                            {
                                await terminal.ConfigureAwait(false);
                                throw new InvalidOperationException("SCADA只读Pipe意外结束");
                            }
                            await terminal.ConfigureAwait(false);
                            throw new InvalidOperationException("SCADA设备采集世代意外结束");
                        }
                        finally
                        {
                            generationStop.Cancel();
                            if(pipeTask!=null)
                            {
                                try{await pipeTask.ConfigureAwait(false);}
                                catch(OperationCanceledException){}
                                catch(ObjectDisposedException){}
                                catch(IOException){}
                            }
                            // StopAsync可能重抛旧Completion故障，不能妨碍其余设备Dispose。
                            try{await group.StopAsync().ConfigureAwait(false);}
                            catch(Exception){}
                        }
                    }
                }
                catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                {return;}
                catch(PADPlcAcquisitionUnavailableException error)
                    when(!cancellationToken.IsCancellationRequested &&
                        RecoveryAttempts<_maxAttempts)
                {
                    Volatile.Write(ref _lastFailure,error.Message);
                    Interlocked.Increment(ref _recoveryAttempts);
                }
                catch(SocketException error)
                    when(!cancellationToken.IsCancellationRequested &&
                        RecoveryAttempts<_maxAttempts)
                {
                    Volatile.Write(ref _lastFailure,error.Message);
                    Interlocked.Increment(ref _recoveryAttempts);
                }
                // 走到这里意味着允许恢复的通信失败且旧Group已经完全释放。
                int shift=Math.Min(RecoveryAttempts-1,4);
                int delay=(int)Math.Min(10000,_baseDelay.TotalMilliseconds*(1<<shift));
                await Task.Delay(delay,cancellationToken).ConfigureAwait(false);
            }
        }

        internal static int Serve(string projectPath,string configRoot,string readPipeName)
        {
            using(CancellationTokenSource signal=new CancellationTokenSource())
            {
                ConsoleCancelEventHandler handler=(sender,e)=>{e.Cancel=true;signal.Cancel();};
                Console.CancelKeyPress+=handler;
                try
                {
                    MRHScadaGroupRecoverySupervisor supervisor=
                        new MRHScadaGroupRecoverySupervisor(projectPath,configRoot,readPipeName);
                    Console.WriteLine("SCADA多PLC组只读恢复启用：最多3次；每次重建整个组，绝不重放写命令。");
                    try
                    {
                        supervisor.RunAsync(signal.Token,group=>
                            Console.WriteLine("SCADA Group generation="+supervisor.ConnectedGenerations+
                                " Devices="+group.Count+" Effective="+group.DeploymentVersion+
                                " Recoveries="+supervisor.RecoveryAttempts))
                            .GetAwaiter().GetResult();
                        return 0;
                    }
                    catch(OperationCanceledException) when(signal.IsCancellationRequested)
                    {return 0;}
                }
                finally{Console.CancelKeyPress-=handler;}
            }
        }
    }
}
