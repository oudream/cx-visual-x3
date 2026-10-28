using System;
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
        private CancellationTokenSource _stop;
        private Task _runner;
        private int _attempts;
        private bool _active, _exhausted, _disposed;
        private Exception _lastError;
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
                _runner != null && !_runner.IsCompleted,_lastError == null ? null : _lastError.Message);
        }
        /// <summary>显式启动维护。若初始为Closed/Faulted则发起连接；已Open时只侦听后续Faulted。</summary>
        public void Start()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("FTRReconnectController");
                if (_active) throw new InvalidOperationException("恢复维护已经启动");
                _stop = new CancellationTokenSource();
                _active = true; _attempts = 0; _exhausted = false; _lastError = null;
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
                if (!_active) return;
                _attempts=0;_lastError=null;_exhausted=false;
                ScheduleLocked();
            }
        }
        private void ScheduleLocked()
        {
            if (!_active || _exhausted || (_runner != null && !_runner.IsCompleted)) return;
            CancellationToken token=_stop.Token;
            _runner=Task.Run(() => RecoverAsync(token));
        }
        private async Task RecoverAsync(CancellationToken cancellation)
        {
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
                    try
                    {
                        if(_channel.State!=FTRChannelState.Closed)
                            await _channel.CloseAsync(cancellation).ConfigureAwait(false);
                        cancellation.ThrowIfCancellationRequested();
                        await _channel.OpenAsync(cancellation).ConfigureAwait(false);
                        lock (_gate){_attempts=retries+1;_lastError=null;}
                        NotifyReconnected();
                        return;
                    }
                    catch(OperationCanceledException) when(cancellation.IsCancellationRequested){return;}
                    catch(Exception error)
                    {
                        retries++;
                        lock (_gate){_attempts=retries;_lastError=error;}
                        NotifyFailure(retries,error);
                    }
                }
            }
            catch(OperationCanceledException) when(cancellation.IsCancellationRequested) {}
            catch(Exception error)
            {
                lock (_gate){_lastError=error;_exhausted=true;}
                NotifyFailure(Snapshot().Attempts,error);
                NotifyExhausted();
            }
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
        /// <summary>先解除故障订阅并取消工作，不关闭归调用者拥有的Channel。</summary>
        public async Task StopAsync()
        {
            Task runner;CancellationTokenSource stop;
            lock (_gate)
            {
                if (!_active) return;
                _active=false;
                _channel.StateChanged -= OnStateChanged;
                stop=_stop;_stop=null;runner=_runner;
                if (stop != null) stop.Cancel();
            }
            try {if(runner!=null) await runner.ConfigureAwait(false);}
            finally
            {
                if(stop!=null) stop.Dispose();
                lock (_gate) if(ReferenceEquals(_runner,runner))_runner=null;
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
        public FTRReconnectSnapshot(bool active,bool exhausted,int attempts,bool recovering,string lastError)
        {Active=active;Exhausted=exhausted;Attempts=attempts;Recovering=recovering;LastError=lastError;}
    }
}
