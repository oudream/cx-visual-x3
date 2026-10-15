using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Infrastructure.Concurrency
{
    /// <summary>
    /// F19：有界、按Key串行的共享异步Worker。没有业务/图形/设备知识。
    /// 不在持锁期间运行用户回调；异步等待释放线程但保留Key与并发槽。
    /// </summary>
    public sealed class FCRWorker
    {
        private abstract class Job
        {
            internal readonly Guid Id = Guid.NewGuid();
            internal readonly string Key;
            internal readonly bool Droppable;
            internal readonly CancellationTokenSource Source;
            internal int State = (int)FCRWorkState.Queued;
            protected Job(string key, bool droppable, CancellationToken cancellation)
            {
                Key = key ?? Id.ToString("N");
                Droppable = droppable;
                Source = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            }
            internal abstract Task InvokeAsync();
            internal abstract void FinishWithoutRun(FCRWorkState state);
        }

        private sealed class Job<T> : Job
        {
            private readonly Func<CancellationToken, Task<T>> _action;
            private readonly TaskCompletionSource<FCRWorkResult<T>> _completion =
                new TaskCompletionSource<FCRWorkResult<T>>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal readonly FCRWorkHandle<T> Handle;
            internal Job(FCRWorker owner, string key, bool droppable,
                CancellationToken cancellation, Func<CancellationToken, Task<T>> action)
                : base(key, droppable, cancellation)
            {
                _action = action;
                Handle = new FCRWorkHandle<T>(Id, Key, _completion.Task,
                    () => (FCRWorkState)Volatile.Read(ref State), () => owner.RequestCancel(this));
            }
            private void Finish(FCRWorkState state, T value, Exception error)
            {
                Interlocked.Exchange(ref State, (int)state);
                _completion.TrySetResult(new FCRWorkResult<T>(
                    state, value, error, Source.IsCancellationRequested));
            }
            internal override void FinishWithoutRun(FCRWorkState state)
            { Finish(state, default(T), null); }

            internal override async Task InvokeAsync()
            {
                try
                {
                    Source.Token.ThrowIfCancellationRequested();
                    Task<T> task = _action(Source.Token);
                    if (task == null) throw new InvalidOperationException("WORK_NULL_TASK");
                    if (!task.IsCompleted)
                        Interlocked.Exchange(ref State, (int)FCRWorkState.Waiting);
                    T value = await task.ConfigureAwait(false);
                    Interlocked.Exchange(ref State, (int)FCRWorkState.Running);
                    Finish(FCRWorkState.Completed, value, null);
                }
                catch (OperationCanceledException ex)
                {
                    Finish(Source.IsCancellationRequested ? FCRWorkState.Cancelled : FCRWorkState.Failed,
                        default(T), ex);
                }
                catch (Exception ex) { Finish(FCRWorkState.Failed, default(T), ex); }
            }
        }

        private readonly object _gate = new object();
        private readonly LinkedList<Job> _pending = new LinkedList<Job>();
        private readonly HashSet<string> _activeKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly FCRWorkerOptions _options;
        private int _active;
        private long _finished, _rejected, _dropped;
        private bool _stopping;
        private TaskCompletionSource<bool> _stopped;

        public FCRWorker(FCRWorkerOptions options)
        { _options = options ?? throw new ArgumentNullException("options"); }

        public FCRWorkerSnapshot GetSnapshot()
        {
            lock (_gate)
                return new FCRWorkerSnapshot(_pending.Count, _active, _finished,
                    _rejected, _dropped, _stopping);
        }

        public FCRWorkHandle<T> Submit<T>(Func<CancellationToken, Task<T>> action,
            string key = null, bool droppable = false, CancellationToken cancellation = default(CancellationToken))
        {
            if (action == null) throw new ArgumentNullException("action");
            Job<T> job = new Job<T>(this, key, droppable, cancellation, action);
            List<Job> launch;
            lock (_gate)
            {
                if (_stopping)
                {
                    _rejected++;
                    job.FinishWithoutRun(FCRWorkState.Rejected);
                    job.Source.Dispose();
                    return job.Handle;
                }
                if (cancellation.IsCancellationRequested)
                {
                    _finished++;
                    job.FinishWithoutRun(FCRWorkState.Cancelled);
                    job.Source.Dispose();
                    return job.Handle;
                }
                // 若当前已满，先尝试启动一个不同Key的待处理工作，腾出待处理容量。
                launch = PumpLocked();
                if (_pending.Count >= _options.QueueCapacity && !CanStartImmediatelyLocked(job))
                {
                    Job old = FindDroppableLocked();
                    if (_options.OverflowPolicy == FCROverflowPolicy.DropOldestDroppable
                        && old != null)
                    {
                        _pending.Remove(old);
                        old.FinishWithoutRun(FCRWorkState.Dropped);
                        old.Source.Dispose();
                        _finished++;
                        _dropped++;
                    }
                    else
                    {
                        _rejected++;
                        job.FinishWithoutRun(FCRWorkState.Rejected);
                        job.Source.Dispose();
                        LaunchInstance(launch);
                        return job.Handle;
                    }
                }
                _pending.AddLast(job);
                launch.AddRange(PumpLocked());
            }
            LaunchInstance(launch);
            return job.Handle;
        }

        private bool CanStartImmediatelyLocked(Job job)
        { return _active < _options.MaxConcurrency && !_activeKeys.Contains(job.Key); }

        private Job FindDroppableLocked()
        {
            for (LinkedListNode<Job> node = _pending.First; node != null; node = node.Next)
                if (node.Value.Droppable) return node.Value;
            return null;
        }

        private List<Job> PumpLocked()
        {
            List<Job> launch = new List<Job>();
            while (_active < _options.MaxConcurrency)
            {
                LinkedListNode<Job> eligible = _pending.First;
                while (eligible != null && _activeKeys.Contains(eligible.Value.Key))
                    eligible = eligible.Next;
                if (eligible == null) break;
                Job job = eligible.Value;
                _pending.Remove(eligible);
                _active++;
                _activeKeys.Add(job.Key);
                Interlocked.Exchange(ref job.State, (int)FCRWorkState.Running);
                launch.Add(job);
            }
            return launch;
        }

        private async Task ExecuteAsync(Job job)
        {
            try { await job.InvokeAsync().ConfigureAwait(false); }
            finally
            {
                List<Job> launch;
                lock (_gate)
                {
                    _active--;
                    _activeKeys.Remove(job.Key);
                    _finished++;
                    launch = PumpLocked();
                    SignalStopLocked();
                }
                job.Source.Dispose();
                LaunchInstance(launch);
            }
        }
        private void LaunchInstance(List<Job> jobs)
        {
            foreach (Job job in jobs)
            {
                Job scheduled = job;
                Task.Run(() => ExecuteAsync(scheduled));
            }
        }

        private bool RequestCancel(Job job)
        {
            List<Job> launch = null;
            lock (_gate)
            {
                FCRWorkState state = (FCRWorkState)Volatile.Read(ref job.State);
                if (state == FCRWorkState.Completed || state == FCRWorkState.Failed
                    || state == FCRWorkState.Cancelled || state == FCRWorkState.Dropped
                    || state == FCRWorkState.Rejected) return false;
                LinkedListNode<Job> node = _pending.Find(job);
                if (node != null)
                {
                    _pending.Remove(node);
                    job.FinishWithoutRun(FCRWorkState.Cancelled);
                    _finished++;
                    launch = PumpLocked();
                    SignalStopLocked();
                }
            }
            try { job.Source.Cancel(); }
            catch (ObjectDisposedException) { /* 任务恰好结束 */ }
            if (launch != null)
            {
                job.Source.Dispose();
                LaunchInstance(launch);
            }
            return true;
        }

        public Task StopAsync(bool cancelPending = false)
        {
            List<Job> cancel = new List<Job>();
            Task completion;
            lock (_gate)
            {
                _stopping = true;
                if (_stopped == null)
                    _stopped = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                if (cancelPending)
                {
                    while (_pending.Count > 0)
                    {
                        Job j = _pending.First.Value;
                        _pending.RemoveFirst();
                        j.FinishWithoutRun(FCRWorkState.Cancelled);
                        _finished++;
                        cancel.Add(j);
                    }
                }
                SignalStopLocked();
                completion = _stopped.Task;
            }
            foreach (Job job in cancel)
            {
                try { job.Source.Cancel(); } catch (ObjectDisposedException) { }
                job.Source.Dispose();
            }
            return completion;
        }

        private void SignalStopLocked()
        {
            if (_stopping && _active == 0 && _pending.Count == 0 && _stopped != null)
                _stopped.TrySetResult(true);
        }
    }
}
