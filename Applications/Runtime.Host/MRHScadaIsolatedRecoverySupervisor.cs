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
    /// 已存在SCADA组的单PLC隔离恢复模式：FCR负责每台设备周期采集，
    /// 此类只监督完成任务及有界替换；其他设备不Stop、不重建MDS/Channel。
    /// 可选的同账户只读Pipe保持一条监听，故障设备从发现目录暂时下线，
    /// 恢复成功后以新SessionGeneration重新出现，旧HMI不得复用旧世代。
    /// </summary>
    internal sealed class MRHScadaIsolatedRecoverySupervisor
    {
        private readonly string _projectPath, _configRoot, _pipeName;
        private readonly int _maxAttempts;
        private readonly TimeSpan _baseDelay;
        private readonly Dictionary<string,int> _attempts =
            new Dictionary<string,int>(StringComparer.Ordinal);
        private readonly object _gate=new object();
        private int _started, _connectedGenerations, _totalAttempts;
        private string _lastFailure;

        internal MRHScadaIsolatedRecoverySupervisor(string projectPath,string configRoot,
            string pipeName=null,int maxRecoveryAttempts=3,int retryDelayMs=250)
        {
            if(String.IsNullOrWhiteSpace(projectPath)||!File.Exists(projectPath)||
                String.IsNullOrWhiteSpace(configRoot))
                throw new ArgumentException("SCADA隔离恢复要求保存工程和MHS配置根");
            if(maxRecoveryAttempts<0||maxRecoveryAttempts>12||
                retryDelayMs<20||retryDelayMs>10000)
                throw new ArgumentOutOfRangeException("恢复次数或退避间隔越界");
            _projectPath=Path.GetFullPath(projectPath);
            _configRoot=configRoot;_pipeName=pipeName;
            _maxAttempts=maxRecoveryAttempts;
            _baseDelay=TimeSpan.FromMilliseconds(retryDelayMs);
        }

        internal int ConnectedGenerations {get{return Volatile.Read(ref _connectedGenerations);}}
        internal int RecoveryAttempts {get{return Volatile.Read(ref _totalAttempts);}}
        internal string LastFailure {get{return Volatile.Read(ref _lastFailure);}}
        internal int AttemptsFor(string deviceRef)
        {
            lock(_gate)
            {
                int count;
                return deviceRef!=null&&_attempts.TryGetValue(deviceRef,out count)?count:0;
            }
        }

        private static bool MayRetry(Exception error)
        {
            // 协议读取失效才有自动重新建立只读连接的许可。
            // 配置漂移/安全拒绝/非法报文/未知写入结果均不得进入重连。
            return error is PADPlcAcquisitionUnavailableException ||
                error is SocketException;
        }
        private int RecordAttempt(string deviceRef,Exception error)
        {
            lock(_gate)
            {
                int previous;
                _attempts.TryGetValue(deviceRef,out previous);
                if(previous>=_maxAttempts)return -1;
                int next=previous+1;
                _attempts[deviceRef]=next;
                Interlocked.Increment(ref _totalAttempts);
                Volatile.Write(ref _lastFailure,error.Message);
                return next;
            }
        }

        private async Task<MRHScadaRuntime> RestoreAsync(
            MRHScadaRuntimeGroup group,int slot,string deviceRef,
            string oldGeneration,Exception original,CancellationToken cancellationToken)
        {
            Exception failure=original;
            while(true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if(!MayRetry(failure))
                    throw failure;
                int attempt=RecordAttempt(deviceRef,failure);
                if(attempt<0)throw failure;
                // 每PLC各自有界重试预算。等待期间其他设备继续原FC03周期。
                int shift=Math.Min(attempt-1,4);
                int delay=(int)Math.Min(10000,_baseDelay.TotalMilliseconds*(1<<shift));
                await Task.Delay(delay,cancellationToken).ConfigureAwait(false);
                try
                {
                    return await group.StartReplacementAsync(slot,oldGeneration,
                        cancellationToken).ConfigureAwait(false);
                }
                catch(Exception error) when(MayRetry(error) &&
                    !cancellationToken.IsCancellationRequested)
                {
                    failure=error;
                }
            }
        }

        internal async Task RunAsync(CancellationToken cancellationToken,
            Action<MRHScadaRuntime> onConnected=null)
        {
            if(Interlocked.Exchange(ref _started,1)!=0)
                throw new InvalidOperationException("SCADA隔离恢复监督器每次只能运行一次");
            cancellationToken.ThrowIfCancellationRequested();
            using(MRHScadaRuntimeGroup group=
                MRHScadaRuntimeGroup.OpenFromEngineering(_projectPath,_configRoot))
            using(CancellationTokenSource lifetime=
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                Task pipeTask=null;
                try
                {
                    await group.StartAsync(lifetime.Token).ConfigureAwait(false);
                    foreach(MRHScadaRuntime device in group.SnapshotDevices())
                    {
                        Interlocked.Increment(ref _connectedGenerations);
                        if(onConnected!=null)onConnected(device);
                    }
                    if(_pipeName!=null)
                        pipeTask=MRHScadaReadOnlyPipe.RunAsync(group,_pipeName,lifetime.Token);
                    Task ending=Task.Delay(Timeout.Infinite,lifetime.Token);
                    while(true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        MRHScadaRuntime[] devices=group.SnapshotDevices();
                        if(devices.Length==0)
                            throw new InvalidOperationException("SCADA没有任何有效PLC运行会话");
                        Task[] all=devices.Select(x=>x.Completion)
                            .Concat(new[]{ending})
                            .Concat(pipeTask==null?new Task[0]:new[]{pipeTask})
                            .ToArray();
                        Task completed=await Task.WhenAny(all).ConfigureAwait(false);
                        if(completed==ending)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            throw new InvalidOperationException("SCADA运行会话意外终止");
                        }
                        if(completed==pipeTask)
                        {
                            await completed.ConfigureAwait(false);
                            throw new InvalidOperationException("SCADA本机只读Pipe意外关闭");
                        }
                        MRHScadaRuntime failed=devices.First(x=>x.Completion==completed);
                        Exception failure=null;
                        try{await completed.ConfigureAwait(false);}
                        catch(Exception error){failure=error;}
                        if(failure==null)
                            throw new InvalidOperationException("PLC未经Stop正常结束，禁止静默重连");
                        if(!MayRetry(failure))
                            throw failure;
                        // 只摘除失效PLC槽位，其他设备Channel和MDS继续运行。
                        string deviceRef=failed.DeviceRef;
                        string oldGeneration=failed.Generation;
                        int slot=group.DetachFaulted(failed);
                        MRHScadaRuntime fresh=await RestoreAsync(group,slot,deviceRef,
                            oldGeneration,failure,lifetime.Token).ConfigureAwait(false);
                        Interlocked.Increment(ref _connectedGenerations);
                        if(onConnected!=null)onConnected(fresh);
                    }
                }
                catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested)
                {return;}
                finally
                {
                    lifetime.Cancel();
                    if(pipeTask!=null)
                        try{await pipeTask.ConfigureAwait(false);}
                        catch(OperationCanceledException){}
                        catch(ObjectDisposedException){}
                        catch(IOException){}
                }
            }
        }

        internal static int Serve(string projectPath,string configRoot,string pipeName)
        {
            using(CancellationTokenSource signal=new CancellationTokenSource())
            {
                ConsoleCancelEventHandler handler=(sender,e)=>{e.Cancel=true;signal.Cancel();};
                Console.CancelKeyPress+=handler;
                try
                {
                    MRHScadaIsolatedRecoverySupervisor supervisor=
                        new MRHScadaIsolatedRecoverySupervisor(projectPath,configRoot,pipeName);
                    Console.WriteLine("SCADA单PLC隔离恢复启用；其它PLC不停采，写命令绝不重放。");
                    try
                    {
                        supervisor.RunAsync(signal.Token,device=>
                            Console.WriteLine("SCADA PLC DeviceRef="+device.DeviceRef+
                                " Generation="+device.Generation+
                                " Scope="+device.ScopeId+
                                " Recoveries="+supervisor.AttemptsFor(device.DeviceRef)))
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
