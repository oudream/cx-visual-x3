using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Transport
{
    /// <summary>传输故障后的可选连接恢复协调器；只重建连接，绝不重发未知结果的业务命令。</summary>
    public sealed class FTRReconnectController : IDisposable
    {
        private readonly FTRSingleChannelBase _channel;
        private readonly FTRChannelConfiguration _policy;
        private readonly object _gate = new object();
        // 恢复任务的异步上下文：事件回调中停止不能等待自己的恢复任务。
        private readonly AsyncLocal<bool> _inRecovery = new AsyncLocal<bool>();
        private CancellationTokenSource _stop;
        private Task _runner;
        private Task _stopTask;
        private int _attempts;
        private bool _active, _exhausted, _disposed;
        private Exception _lastError;
        private FTRFailureInfo _lastFailure;
        private long _failedAttemptsTotal,_successfulRecoveries;
        private long _lastRecoveryDurationMs;
        private DateTime? _lastRecoveredUtc;
        public event Action<int, Exception> AttemptFailed;
        public event Action Reconnected;
        public event Action Exhausted;

        public FTRReconnectController(FTRSingleChannelBase channel, FTRChannelConfiguration policy)
        {
            _channel = channel ?? throw new ArgumentNullException("channel");
            _policy = policy ?? throw new ArgumentNullException("policy");
            policy.Validate();
            if (channel.Id != policy.Id)
                throw new ArgumentException("恢复策略的通道Id必须与实例一致");
        }
        public FTRReconnectSnapshot Snapshot()
        {
            lock (_gate) return new FTRReconnectSnapshot(_active,_exhausted,_attempts,
                _runner != null && !_runner.IsCompleted,
                _lastError == null ? (_lastFailure == null ? null : _lastFailure.Message) : _lastError.Message)
            {
                LastFailure=_lastFailure, FailedAttemptsTotal=_failedAttemptsTotal,
                SuccessfulRecoveries=_successfulRecoveries,
                LastRecoveryDurationMs=_lastRecoveryDurationMs, LastRecoveredUtc=_lastRecoveredUtc
            };
        }
        /// <summary>显式启动维护。若初始为Closed/Faulted则发起连接；已Open时只侦听后续Faulted。</summary>
        public void Start()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("FTRReconnectController");
                if (_active) throw new InvalidOperationException("恢复维护已经启动");
                if (_stopTask != null && !_stopTask.IsCompleted)
                    throw new InvalidOperationException("上次恢复维护尚未停止完成");
                _stop = new CancellationTokenSource();
                _active = true; _attempts = 0; _exhausted = false; _lastError = null; _lastFailure=null;
                _channel.StateChanged += OnStateChanged;
                if (_channel.State == FTRChannelState.Closed || _channel.State == FTRChannelState.Faulted)
                    ScheduleLocked();
            }
        }
        private void OnStateChanged(FTRChannelState state)
        {
            if (state != FTRChannelState.Faulted) return;
            lock (_gate)
            {
                if (!_active || (_runner != null && !_runner.IsCompleted)) return;
                _attempts=0;_lastError=null;_exhausted=false;
                ScheduleLocked();
            }
        }
        private void ScheduleLocked()
        {
            if (!_active || _exhausted || (_runner != null && !_runner.IsCompleted)) return;
            CancellationToken token=_stop.Token;
            Task running=Task.Run(() => RecoverAsync(token));
            _runner=running;
            // 新故障若恰好发生在恢复成功的尾部，继续排队；不丢失Faulted状态。
            running.ContinueWith(done =>
            {
                lock (_gate)
                {
                    if (!ReferenceEquals(_runner,done)) return;
                    _runner=null;
                    if (_active && !_exhausted && _channel.State == FTRChannelState.Faulted)
                        ScheduleLocked();
                }
            },TaskScheduler.Default);
        }
        private async Task RecoverAsync(CancellationToken cancellation)
        {
            _inRecovery.Value = true;
            try
            {
                int retries=0;
                while (!cancellation.IsCancellationRequested)
                {
                    if (_channel.State==FTRChannelState.Open) return;
                    if (!_policy.CanReconnect(retries))
                    {
                        lock (_gate) _exhausted=true;
                        NotifyExhausted();
                        return;
                    }
                    // 遵循配置的初始重连延迟与指数退避，避免持续故障时高速打满设备。
                    double computed=_policy.ReconnectInitialDelayMs==0 ? 0 :
                        _policy.ReconnectInitialDelayMs*Math.Pow(_policy.ReconnectBackoff,retries);
                    int delay=double.IsInfinity(computed) || computed>=_policy.ReconnectMaxDelayMs
                        ? _policy.ReconnectMaxDelayMs : (int)computed;
                    if(delay>0) await Task.Delay(delay,cancellation).ConfigureAwait(false);
                    cancellation.ThrowIfCancellationRequested();
                    Stopwatch recoveryTimer=Stopwatch.StartNew();
                    try
                    {
                        if(_channel.State!=FTRChannelState.Closed)
                            await _channel.CloseAsync(cancellation).ConfigureAwait(false);
                        cancellation.ThrowIfCancellationRequested();
                        await _channel.OpenAsync(cancellation).ConfigureAwait(false);
                        lock (_gate)
                        {
                            _attempts=retries+1;_lastError=null;
                            _successfulRecoveries++;
                            _lastRecoveryDurationMs=recoveryTimer.ElapsedMilliseconds;
                            _lastRecoveredUtc=DateTime.UtcNow;
                        }
                        NotifyReconnected();
                        return;
                    }
                    catch(OperationCanceledException) when(cancellation.IsCancellationRequested){return;}
                    catch(Exception error)
                    {
                        retries++;
                        FTRFailureInfo failure=FTRFailureClassifier.Classify(error,"Reconnect",FTRFaultScope.Channel);
                        bool retryable=CanRetryFailure(failure.Kind);
                        lock (_gate)
                        {
                            _attempts=retries;_lastError=error;_lastFailure=failure;
                            _failedAttemptsTotal++;
                            if(!retryable)_exhausted=true;
                        }
                        NotifyFailure(retries,error);
                        // 权限/非法参数/资源耗尽无需重复刺激设备；未知结果的命令也绝不重放。
                        if(!retryable){NotifyExhausted();return;}
                    }
                }
            }
            catch(OperationCanceledException) when(cancellation.IsCancellationRequested) {}
            catch(Exception error)
            {
                var failure=FTRFailureClassifier.Classify(error,"Reconnect",FTRFaultScope.Channel);
                lock (_gate)
                {
                    _lastError=error;_lastFailure=failure;_exhausted=true;
                    _failedAttemptsTotal++;
                }
                NotifyFailure(Snapshot().Attempts,error);
                NotifyExhausted();
            }
            finally { _inRecovery.Value = false; }
        }
        private static bool CanRetryFailure(FTRFailureKind kind)
        {
            return kind!=FTRFailureKind.Cancelled &&
                kind!=FTRFailureKind.PermissionDenied &&
                kind!=FTRFailureKind.InvalidConfiguration &&
                kind!=FTRFailureKind.ResourceExhausted;
        }
        private void NotifyFailure(int attempt,Exception error)
        {
            var handlers=AttemptFailed;if(handlers==null)return;
            foreach(Action<int,Exception> callback in handlers.GetInvocationList())
                try{callback(attempt,error);}catch{ /* 诊断观察者不能破坏连接恢复 */ }
        }
        private void NotifyReconnected()
        {
            var handlers=Reconnected;if(handlers==null)return;
            foreach(Action callback in handlers.GetInvocationList())
                try{callback();}catch{ /* 隔离诊断观察者 */ }
        }
        private void NotifyExhausted()
        {
            var handlers=Exhausted;if(handlers==null)return;
            foreach(Action callback in handlers.GetInvocationList())
                try{callback();}catch{ /* 隔离诊断观察者 */ }
        }
        /// <summary>
        /// 取消恢复并等待完成，不关闭调用者拥有的Channel。
        /// 在恢复事件回调内调用时只请求停止，避免等待当前恢复任务造成死锁；
        /// 其它调用者仍可等待 StopAsync 返回的任务完成清理。
        /// </summary>
        public Task StopAsync()
        {
            Task stopping;
            lock (_gate)
            {
                if (_active)
                {
                    _active=false;
                    _channel.StateChanged -= OnStateChanged;
                    CancellationTokenSource stop=_stop;
                    _stop=null;
                    Task runner=_runner;
                    if (stop!=null) stop.Cancel();
                    _stopTask=CompleteStopAsync(runner,stop);
                }
                stopping=_stopTask ?? Task.CompletedTask;
            }
            // AttemptFailed / Reconnected / Exhausted 是恢复任务中的同步回调。
            // 此处不能同步等待正在执行自身回调的 runner。
            return _inRecovery.Value ? Task.CompletedTask : stopping;
        }
        private async Task CompleteStopAsync(Task runner,CancellationTokenSource stop)
        {
            try {if(runner!=null) await runner.ConfigureAwait(false);}
            finally
            {
                if(stop!=null)stop.Dispose();
                lock(_gate)if(ReferenceEquals(_runner,runner))_runner=null;
            }
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed=true;
            }
            StopAsync().GetAwaiter().GetResult();
        }
    }
    public sealed class FTRReconnectSnapshot
    {
        public bool Active {get;private set;}
        public bool Exhausted {get;private set;}
        public int Attempts {get;private set;}
        public bool Recovering {get;private set;}
        public string LastError {get;private set;}
        public FTRFailureInfo LastFailure {get;internal set;}
        public long FailedAttemptsTotal {get;internal set;}
        public long SuccessfulRecoveries {get;internal set;}
        public long LastRecoveryDurationMs {get;internal set;}
        public DateTime? LastRecoveredUtc {get;internal set;}
        public FTRReconnectSnapshot(bool active,bool exhausted,int attempts,bool recovering,string lastError)
        {Active=active;Exhausted=exhausted;Attempts=attempts;Recovering=recovering;LastError=lastError;}
    }
}
