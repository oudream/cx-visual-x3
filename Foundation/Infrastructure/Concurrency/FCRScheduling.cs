using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Infrastructure.Concurrency
{
    /// <summary>可重置的异步广播门。等待不占线程，取消一个等待者不会取消其他等待者。</summary>
    public sealed class FCRAsyncGate
    {
        private readonly object _sync = new object();
        private TaskCompletionSource<bool> _opened =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _isOpen;
        public FCRAsyncGate(bool initiallyOpen = false)
        { if (initiallyOpen) Open(); }
        public bool IsOpen { get { lock (_sync) return _isOpen; } }
        public void Open()
        {
            lock (_sync)
            {
                if (_isOpen) return;
                _isOpen = true;
                _opened.TrySetResult(true);
            }
        }
        public void Close()
        {
            lock (_sync)
            {
                if (!_isOpen) return;
                _isOpen = false;
                _opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }
        public async Task WaitAsync(CancellationToken cancellation = default(CancellationToken))
        {
            Task wait;
            lock (_sync) wait = _opened.Task;
            cancellation.ThrowIfCancellationRequested();
            if (wait.IsCompleted)
            {
                await wait.ConfigureAwait(false);
                return;
            }
            using (CancellationTokenSource pending =
                CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                Task cancelled = Task.Delay(Timeout.Infinite, pending.Token);
                Task winner = await Task.WhenAny(wait, cancelled).ConfigureAwait(false);
                if (winner != wait) cancellation.ThrowIfCancellationRequested();
                pending.Cancel();
                await wait.ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 一个生命周期内不重入的异步周期任务，采用“本次完成后再延时”的节奏；
    /// 回调失败会结束循环并让Completion传播异常，不会静默丢错。
    /// </summary>
    public sealed class FCRPeriodicJob
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly Task _completion;
        private long _executions;
        public FCRPeriodicJob(TimeSpan interval,
            Func<CancellationToken, Task> callback, bool runImmediately = false,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (interval <= TimeSpan.Zero || interval.TotalMilliseconds > uint.MaxValue)
                throw new ArgumentOutOfRangeException("interval");
            if (callback == null) throw new ArgumentNullException("callback");
            Interval = interval;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            _completion = ExecuteAsync(callback, runImmediately, _cancellation.Token);
        }
        public TimeSpan Interval { get; private set; }
        public long Executions { get { return Interlocked.Read(ref _executions); } }
        public Task Completion { get { return _completion; } }
        private async Task ExecuteAsync(Func<CancellationToken, Task> callback,
            bool runImmediately, CancellationToken token)
        {
            if (!runImmediately) await Task.Delay(Interval, token).ConfigureAwait(false);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                Task task = callback(token);
                if (task == null) throw new InvalidOperationException("周期回调返回空Task");
                await task.ConfigureAwait(false);
                Interlocked.Increment(ref _executions);
                await Task.Delay(Interval, token).ConfigureAwait(false);
            }
        }
        /// <summary>停止不会强杀回调；若回调不接受取消，需等待回调自行退出。</summary>
        public async Task StopAsync()
        {
            if (!_cancellation.IsCancellationRequested) _cancellation.Cancel();
            try { await _completion.ConfigureAwait(false); }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        }
    }
}
