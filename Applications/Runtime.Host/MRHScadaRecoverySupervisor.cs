using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Professional.Automation.Plc;

namespace CxVisual.Runtime.Host
{
    /// <summary>
    /// 只读SCADA Host的有界连接恢复策略：不拥有Timer/Channel/Provider/MDS仓。
    /// 每次重建必须重新通过MEM/MHS正式Open并保持最初工程和Effective版本；
    /// 仅采集不可用或Socket连接失败可自动新建会话，设备写命令绝不自动重发。
    /// 持久进程守护、跨宿主租约及报警审计仍归已存在Runtime/Supervision Owner。
    /// </summary>
    internal sealed class MRHScadaRecoverySupervisor
    {
        private readonly string _projectPath, _configRoot, _moduleId, _scopeId;
        private readonly Guid _deviceId;
        private readonly int _intervalMs, _maxRecoveryAttempts;
        private readonly bool _contiguous;
        private readonly TimeSpan _retryDelay;
        private int _started, _connectedGenerations, _recoveries;
        private string _pinnedVersion, _lastGeneration;
        private DateTime _projectStamp;
        private string _lastFailure;

        internal MRHScadaRecoverySupervisor(string projectPath, string configRoot,
            Guid deviceId, string moduleId, string scopeId,
            int intervalMs, bool contiguous, int maxRecoveryAttempts = 3,
            int retryDelayMilliseconds = 250)
        {
            if(string.IsNullOrWhiteSpace(projectPath) ||
                string.IsNullOrWhiteSpace(configRoot) ||
                string.IsNullOrWhiteSpace(moduleId) ||
                string.IsNullOrWhiteSpace(scopeId) || deviceId==Guid.Empty)
                throw new ArgumentException("SCADA恢复必须固定有效工程、DeviceRef、配置和Scope");
            if(maxRecoveryAttempts<0||maxRecoveryAttempts>12 ||
                retryDelayMilliseconds<20||retryDelayMilliseconds>10000)
                throw new ArgumentOutOfRangeException("maxRecoveryAttempts/retryDelayMilliseconds");
            _projectPath=Path.GetFullPath(projectPath);
            _configRoot=configRoot;_deviceId=deviceId;_moduleId=moduleId;
            _scopeId=scopeId;_intervalMs=intervalMs;_contiguous=contiguous;
            _maxRecoveryAttempts=maxRecoveryAttempts;
            _retryDelay=TimeSpan.FromMilliseconds(retryDelayMilliseconds);
        }
        internal int ConnectedGenerations { get { return Volatile.Read(ref _connectedGenerations); } }
        internal int RecoveryAttempts { get { return Volatile.Read(ref _recoveries); } }
        internal string LastGeneration { get { return Volatile.Read(ref _lastGeneration); } }
        internal string PinnedEffectiveVersion { get { return _pinnedVersion; } }
        internal string LastFailure { get { return _lastFailure; } }

        internal async Task RunAsync(CancellationToken cancellationToken,
            Action<MRHScadaRuntime> onConnected = null)
        {
            if(Interlocked.Exchange(ref _started,1)!=0)
                throw new InvalidOperationException("恢复Supervisor每次只能运行一次");
            cancellationToken.ThrowIfCancellationRequested();
            _projectStamp=File.GetLastWriteTimeUtc(_projectPath);
            while(true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using(MRHScadaRuntime current=MRHScadaRuntime.Open(
                    _projectPath,_configRoot,_deviceId,_moduleId,_scopeId,
                    _intervalMs,_contiguous))
                {
                    if(_pinnedVersion==null)_pinnedVersion=current.ConfigVersion;
                    if(!string.Equals(current.ConfigVersion,_pinnedVersion,
                        StringComparison.Ordinal) ||
                        File.GetLastWriteTimeUtc(_projectPath)!=_projectStamp)
                        throw new InvalidOperationException(
                            "SCADA恢复发现工程或MHS Effective版本漂移：禁止自动重新审批和重连");
                    try
                    {
                        await current.StartAsync(cancellationToken).ConfigureAwait(false);
                        string next=current.Generation;
                        if(string.Equals(next,_lastGeneration,StringComparison.Ordinal))
                            throw new InvalidOperationException(
                                "SCADA重连没有建立新Channel Session Generation");
                        Volatile.Write(ref _lastGeneration,next);
                        Interlocked.Increment(ref _connectedGenerations);
                        if(onConnected!=null)onConnected(current);
                        using(CancellationTokenSource wait=CancellationTokenSource
                            .CreateLinkedTokenSource(cancellationToken))
                        {
                            Task cancelled=Task.Delay(Timeout.Infinite,wait.Token);
                            Task winner=await Task.WhenAny(current.Completion,cancelled)
                                .ConfigureAwait(false);
                            if(winner==cancelled)
                            {
                                await current.StopAsync().ConfigureAwait(false);
                                return;
                            }
                            wait.Cancel();
                            await current.Completion.ConfigureAwait(false);
                            throw new InvalidOperationException(
                                "SCADA周期会话在没有显式Stop时意外正常结束");
                        }
                    }
                    catch(PADPlcAcquisitionUnavailableException error)
                        when(!cancellationToken.IsCancellationRequested &&
                            RecoveryAttempts<_maxRecoveryAttempts)
                    {
                        _lastFailure=error.Message;
                        Interlocked.Increment(ref _recoveries);
                        // using先Dispose旧Channel、Provider、Scope再等待/重建。
                    }
                    catch(SocketException error)
                        when(!cancellationToken.IsCancellationRequested &&
                            RecoveryAttempts<_maxRecoveryAttempts)
                    {
                        _lastFailure=error.Message;
                        Interlocked.Increment(ref _recoveries);
                    }
                }
                // 恢复只调度新一代只读连接。不会重发非幂等MRK命令，也不在本层建Timer。
                int exponent=Math.Min(RecoveryAttempts-1,4);
                TimeSpan delay=TimeSpan.FromMilliseconds(
                    Math.Min(10000,_retryDelay.TotalMilliseconds*(1<<exponent)));
                await Task.Delay(delay,cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
