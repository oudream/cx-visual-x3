using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Infrastructure.Concurrency
{
    /// <summary>
    /// 仅承载要求线程亲和的同步设备SDK函数，不对异步await续体提供虚假线程亲和保证。
    /// 队列有界，StopAsync完成时专用线程已退出，运行中的同步函数不会被强杀。
    /// </summary>
    public sealed class FCRDedicatedExecutor
    {
        private interface IJob { void Run(); }
        private sealed class Job<T> : IJob
        {
            private readonly Func<CancellationToken, T> _execute;
            private readonly CancellationToken _token;
            internal readonly TaskCompletionSource<T> Completion =
                new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal Job(Func<CancellationToken, T> execute, CancellationToken token)
            { _execute = execute; _token = token; }
            public void Run()
            {
                if (_token.IsCancellationRequested) { Completion.TrySetCanceled(); return; }
                try { Completion.TrySetResult(_execute(_token)); }
                catch (OperationCanceledException ex) when (_token.IsCancellationRequested)
                { Completion.TrySetCanceled(); }
                catch (Exception ex) { Completion.TrySetException(ex); }
            }
        }
        private readonly object _gate = new object();
        private readonly BlockingCollection<IJob> _queue;
        private readonly Thread _thread;
        private readonly TaskCompletionSource<bool> _stopped =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        private bool _stopping;
        public FCRDedicatedExecutor(string name, int queueCapacity = 64,
            ApartmentState apartment = ApartmentState.MTA)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("线程名不能为空","name");
            if (queueCapacity < 1) throw new ArgumentOutOfRangeException("queueCapacity");
            if (apartment != ApartmentState.MTA && apartment != ApartmentState.STA)
                throw new ArgumentOutOfRangeException("apartment");
            Name = name;
            _queue = new BlockingCollection<IJob>(new ConcurrentQueue<IJob>(), queueCapacity);
            _thread = new Thread(ExecuteLoop) { IsBackground = true, Name = name };
            _thread.SetApartmentState(apartment);
            _thread.Start();
        }
        public string Name { get; private set; }
        public int ThreadId { get { return _thread.ManagedThreadId; } }
        public int Pending { get { return _queue.Count; } }
        public int Active { get { return Volatile.Read(ref _active); } }
        public bool Stopping { get { lock(_gate) return _stopping; } }
        public Task<T> InvokeAsync<T>(Func<CancellationToken, T> action,
            CancellationToken cancellation = default(CancellationToken))
        {
            if (action == null) throw new ArgumentNullException("action");
            if (Thread.CurrentThread.ManagedThreadId == _thread.ManagedThreadId)
                throw new InvalidOperationException("专用线程内禁止等待自身队列，避免重入死锁");
            Job<T> work = new Job<T>(action, cancellation);
            lock (_gate)
            {
                if (_stopping || !_queue.TryAdd(work))
                    work.Completion.TrySetException(
                        new InvalidOperationException("专用线程队列已满或已停止：" + Name));
            }
            return work.Completion.Task;
        }
        private void ExecuteLoop()
        {
            try
            {
                foreach (IJob job in _queue.GetConsumingEnumerable())
                {
                    Interlocked.Exchange(ref _active, 1);
                    try { job.Run(); }
                    finally { Interlocked.Exchange(ref _active, 0); }
                }
                _stopped.TrySetResult(true);
            }
            catch (Exception ex) { _stopped.TrySetException(ex); }
            finally { _queue.Dispose(); }
        }
        public Task StopAsync()
        {
            lock (_gate)
            {
                if (!_stopping)
                {
                    _stopping = true;
                    _queue.CompleteAdding();
                }
                return _stopped.Task;
            }
        }
    }
}
