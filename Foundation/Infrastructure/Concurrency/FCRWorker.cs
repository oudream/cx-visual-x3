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
            internal readonly int Priority;
            internal readonly long EstimatedBytes;
            internal readonly CancellationTokenSource Source;
            internal int State = (int)FCRWorkState.Queued;
            protected Job(string key, bool droppable, int priority, long estimatedBytes,
                CancellationToken cancellation)
            {
                Key = key ?? Id.ToString("N");
                Droppable = droppable;
                Priority = priority;
                EstimatedBytes = estimatedBytes;
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
                int priority, long estimatedBytes, CancellationToken cancellation,
                Func<CancellationToken, Task<T>> action)
                : base(key, droppable, priority, estimatedBytes, cancellation)
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

        private sealed class ProducerAdmission
        {
            internal readonly Job Work;
            internal readonly TaskCompletionSource<bool> Admitted =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            internal ProducerAdmission(Job work) { Work = work; }
        }

        private readonly object _gate = new object();
        private readonly LinkedList<ProducerAdmission> _waitingProducers =
            new LinkedList<ProducerAdmission>();
        private readonly LinkedList<Job> _pending = new LinkedList<Job>();
        private readonly HashSet<string> _activeKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly FCRWorkerOptions _options;
        private int _active;
        // R08：跨Key高优先级可优先，但不能永久压制排队更久的任务。
        private int _priorityOvertakes;
        private long _finished, _rejected, _dropped, _pendingBytes;
        private long _admittedProducers, _peakPendingBytes;
        private bool _stopping;
        private TaskCompletionSource<bool> _stopped;

        public FCRWorker(FCRWorkerOptions options)
        { _options = options ?? throw new ArgumentNullException("options"); }

        /// <summary>便捷提交同步业务函数；执行在线程池Worker上，异常仍可从Handle结果查看。</summary>
        public FCRWorkHandle<bool> Post(Action action, string key = null,
            bool droppable = false, CancellationToken cancellation = default(CancellationToken),
            int priority = 0, long estimatedBytes = 0)
        {
            if (action == null) throw new ArgumentNullException("action");
            return Submit<bool>(token =>
            {
                token.ThrowIfCancellationRequested();
                action();
                return Task.FromResult(true);
            }, key, droppable, cancellation, priority, estimatedBytes);
        }

        public FCRWorkHandle<T> Invoke<T>(Func<T> action, string key = null,
            bool droppable = false, CancellationToken cancellation = default(CancellationToken),
            int priority = 0, long estimatedBytes = 0)
        {
            if (action == null) throw new ArgumentNullException("action");
            return Submit<T>(token =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(action());
            }, key, droppable, cancellation, priority, estimatedBytes);
        }

        public FCRWorkerSnapshot GetSnapshot()
        {
            lock (_gate)
                return new FCRWorkerSnapshot(_pending.Count, _active, _finished,
                    _rejected, _dropped, _stopping, _pendingBytes, _options.PendingByteBudget,
                    _waitingProducers.Count, _admittedProducers, _peakPendingBytes);
        }

        /// <summary>高Priority先执行不同Key的待执行工作；同Key始终维持原提交顺序。</summary>
        public FCRWorkHandle<T> Submit<T>(Func<CancellationToken, Task<T>> action,
            string key = null, bool droppable = false,
            CancellationToken cancellation = default(CancellationToken), int priority = 0,
            long estimatedBytes = 0)
        {
            if (action == null) throw new ArgumentNullException("action");
            if (estimatedBytes < 0) throw new ArgumentOutOfRangeException("estimatedBytes");
            Job<T> job = new Job<T>(this, key, droppable, priority, estimatedBytes,
                cancellation, action);
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
                if (!CanStartImmediatelyLocked(job))
                {
                    // 超出整个待执行预算的单项不可能通过淘汰其他任务获得接纳。
                    // 先直接拒绝，保证已有可淘汰工作不会遭到无意义删除。
                    if (job.EstimatedBytes > _options.PendingByteBudget)
                    {
                        _rejected++;
                        job.FinishWithoutRun(FCRWorkState.Rejected);
                        job.Source.Dispose();
                        LaunchInstance(launch);
                        return job.Handle;
                    }
                    while (WouldOverflowLocked(job) &&
                        _options.OverflowPolicy == FCROverflowPolicy.DropOldestDroppable)
                    {
                        Job old = FindDroppableLocked();
                        if (old == null) break;
                        _pending.Remove(old);
                        _pendingBytes -= old.EstimatedBytes;
                        old.FinishWithoutRun(FCRWorkState.Dropped);
                        old.Source.Dispose();
                        _finished++;
                        _dropped++;
                    }
                    if (WouldOverflowLocked(job))
                    {
                        _rejected++;
                        job.FinishWithoutRun(FCRWorkState.Rejected);
                        job.Source.Dispose();
                        LaunchInstance(launch);
                        return job.Handle;
                    }
                }
                _pending.AddLast(job);
                _pendingBytes = checked(_pendingBytes + job.EstimatedBytes);
                if (_pendingBytes > _peakPendingBytes) _peakPendingBytes = _pendingBytes;
                job.Handle.Accepted = true;
                launch.AddRange(PumpLocked());
            }
            // 外部Token取消排队工作时，立即归还队列数量与字节预算。
            // 在锁外注册回调，防止已取消Token的同步回调重入_gate。
            if (cancellation.CanBeCanceled)
            {
                CancellationTokenRegistration registration =
                    cancellation.Register(() => RequestCancel(job));
                // 不在完成工作/Worker锁中同步等待取消回调注销。
                Task cleanup = job.Handle.Completion.ContinueWith(done =>
                    registration.Dispose(), CancellationToken.None,
                    TaskContinuationOptions.None, TaskScheduler.Default);
            }
            LaunchInstance(launch);
            return job.Handle;
        }

        /// <summary>
        /// 等待容量时不占用Worker槽位或PendingBytes；只在正式入队后返回Handle。
        /// 等待生产者有独立上限，且此入口不执行DropOldest，确保不可静默丢业务。
        /// 取消等待抛OperationCanceledException；停止/过量提交返回Rejected Handle。
        /// </summary>
        public async Task<FCRWorkHandle<T>> SubmitWhenCapacityAsync<T>(
            Func<CancellationToken, Task<T>> action, string key = null,
            CancellationToken cancellation = default(CancellationToken),
            int priority = 0, long estimatedBytes = 0)
        {
            if (action == null) throw new ArgumentNullException("action");
            if (estimatedBytes < 0) throw new ArgumentOutOfRangeException("estimatedBytes");
            cancellation.ThrowIfCancellationRequested();
            Job<T> job = new Job<T>(this, key, false, priority, estimatedBytes,
                cancellation, action);
            ProducerAdmission admission = new ProducerAdmission(job);
            List<Job> launch;
            lock (_gate)
            {
                if (_stopping || job.EstimatedBytes > _options.PendingByteBudget ||
                    _waitingProducers.Count >= _options.MaxWaitingProducers)
                {
                    _rejected++;
                    job.FinishWithoutRun(FCRWorkState.Rejected);
                    job.Source.Dispose();
                    return job.Handle;
                }
                _waitingProducers.AddLast(admission);
                launch = PumpLocked();
            }
            // 外部取消回调须在门闩之外注册，否则已取消Token可能同步重入。
            CancellationTokenRegistration registration =
                cancellation.Register(() => RequestCancel(job));
            Task cleanup = job.Handle.Completion.ContinueWith(
                completed => registration.Dispose(), CancellationToken.None,
                TaskContinuationOptions.None, TaskScheduler.Default);
            LaunchInstance(launch);
            await admission.Admitted.Task.ConfigureAwait(false);
            return job.Handle;
        }

        private bool CanStartImmediatelyLocked(Job job)
        { return _active < _options.MaxConcurrency && !_activeKeys.Contains(job.Key); }
        private bool WouldOverflowLocked(Job job)
        {
            return _pending.Count >= _options.QueueCapacity ||
                job.EstimatedBytes > _options.PendingByteBudget - _pendingBytes;
        }

        private Job FindDroppableLocked()
        {
            for (LinkedListNode<Job> node = _pending.First; node != null; node = node.Next)
                if (node.Value.Droppable) return node.Value;
            return null;
        }

        private List<Job> PumpLocked()
        {
            List<Job> launch = new List<Job>();
            while (true)
            {
                while (_active < _options.MaxConcurrency)
                {
                    LinkedListNode<Job> preferred = null;
                    LinkedListNode<Job> oldest = null;
                    HashSet<string> observedKeys = new HashSet<string>(StringComparer.Ordinal);
                    for (LinkedListNode<Job> candidate = _pending.First;
                         candidate != null; candidate = candidate.Next)
                    {
                        if (!observedKeys.Add(candidate.Value.Key)) continue;
                        if (_activeKeys.Contains(candidate.Value.Key)) continue;
                        if (oldest == null) oldest = candidate;
                        if (preferred == null || candidate.Value.Priority > preferred.Value.Priority)
                            preferred = candidate;
                    }
                    if (preferred == null) break;
                    bool oldestWins = _priorityOvertakes >= _options.PriorityFairnessBurst;
                    LinkedListNode<Job> eligible = oldestWins ? oldest : preferred;
                    if (object.ReferenceEquals(eligible, oldest))
                        _priorityOvertakes = 0;
                    else
                        _priorityOvertakes++;
                    Job job = eligible.Value;
                    _pending.Remove(eligible);
                    _pendingBytes -= job.EstimatedBytes;
                    _active++;
                    _activeKeys.Add(job.Key);
                    Interlocked.Exchange(ref job.State, (int)FCRWorkState.Running);
                    launch.Add(job);
                }
                // 只从等待生产者头部准入，容量计费与入队原子化。
                // Waiter还不是业务Job，故等待者不消耗Worker和PendingBytes。
                bool promoted = false;
                while (_waitingProducers.Count > 0)
                {
                    ProducerAdmission admission = _waitingProducers.First.Value;
                    Job job = admission.Work;
                    if (!CanStartImmediatelyLocked(job) && WouldOverflowLocked(job))
                        break;
                    _waitingProducers.RemoveFirst();
                    _pending.AddLast(job);
                    _pendingBytes = checked(_pendingBytes + job.EstimatedBytes);
                    if (_pendingBytes > _peakPendingBytes)
                        _peakPendingBytes = _pendingBytes;
                    job.Handle.Accepted = true;
                    _admittedProducers++;
                    admission.Admitted.TrySetResult(true);
                    promoted = true;
                    // 先通过原调度循环消耗本次容量，再考虑下一位等待者。
                    // 避免MaxConcurrency尚有空位时一口气准入超额队列。
                    break;
                }
                if (!promoted) break;
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
                LinkedListNode<ProducerAdmission> waiter = _waitingProducers.First;
                while (waiter != null && !object.ReferenceEquals(waiter.Value.Work, job))
                    waiter = waiter.Next;
                if (waiter != null)
                {
                    ProducerAdmission admission = waiter.Value;
                    _waitingProducers.Remove(waiter);
                    job.FinishWithoutRun(FCRWorkState.Cancelled);
                    admission.Admitted.TrySetCanceled();
                    _finished++;
                    launch = PumpLocked();
                    SignalStopLocked();
                }
                else
                {
                LinkedListNode<Job> node = _pending.Find(job);
                if (node != null)
                {
                    _pending.Remove(node);
                    _pendingBytes -= job.EstimatedBytes;
                    job.FinishWithoutRun(FCRWorkState.Cancelled);
                    _finished++;
                    launch = PumpLocked();
                    SignalStopLocked();
                }
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
                // 停止接单时永远结束尚未正式入队的生产者等待。
                while (_waitingProducers.Count > 0)
                {
                    ProducerAdmission admission = _waitingProducers.First.Value;
                    _waitingProducers.RemoveFirst();
                    _rejected++;
                    admission.Work.FinishWithoutRun(FCRWorkState.Rejected);
                    admission.Admitted.TrySetResult(false);
                    cancel.Add(admission.Work);
                }
                if (cancelPending)
                {
                    while (_pending.Count > 0)
                    {
                        Job j = _pending.First.Value;
                        _pending.RemoveFirst();
                        _pendingBytes -= j.EstimatedBytes;
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
            if (_stopping && _active == 0 && _pending.Count == 0 &&
                _waitingProducers.Count == 0 && _stopped != null)
                _stopped.TrySetResult(true);
        }
    }
}
