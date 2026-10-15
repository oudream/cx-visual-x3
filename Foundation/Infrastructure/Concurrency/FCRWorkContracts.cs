using System;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Infrastructure.Concurrency
{
    /// <summary>工作状态；Waiting占有逻辑执行槽，但不表示独占工作线程。</summary>
    public enum FCRWorkState { Queued, Running, Waiting, Completed, Failed, Cancelled, Dropped, Rejected }
    public enum FCROverflowPolicy { RejectNew, DropOldestDroppable }

    /// <summary>队列容量仅计算尚未开始的工作，正在运行或异步等待的工作占并发槽。</summary>
    public sealed class FCRWorkerOptions
    {
        public int MaxConcurrency { get; private set; }
        public int QueueCapacity { get; private set; }
        public FCROverflowPolicy OverflowPolicy { get; private set; }
        public FCRWorkerOptions(int maxConcurrency, int queueCapacity,
            FCROverflowPolicy overflowPolicy = FCROverflowPolicy.RejectNew)
        {
            if (maxConcurrency < 1) throw new ArgumentOutOfRangeException("maxConcurrency");
            if (queueCapacity < 0) throw new ArgumentOutOfRangeException("queueCapacity");
            MaxConcurrency = maxConcurrency;
            QueueCapacity = queueCapacity;
            OverflowPolicy = overflowPolicy;
        }
    }

    public sealed class FCRWorkResult<T>
    {
        internal FCRWorkResult(FCRWorkState state, T value, Exception error, bool cancelRequested)
        { State = state; Value = value; Error = error; CancellationRequested = cancelRequested || state == FCRWorkState.Cancelled; }
        public FCRWorkState State { get; private set; }
        public T Value { get; private set; }
        public Exception Error { get; private set; }
        public bool CancellationRequested { get; private set; }
        public bool Succeeded { get { return State == FCRWorkState.Completed; } }
    }

    /// <summary>等待超时只是观察结果，不改变底层工作状态；设备操作是否执行必须由Provider确认。</summary>
    public sealed class FCRWaitObservation<T>
    {
        internal FCRWaitObservation(bool timedOut, FCRWorkResult<T> result)
        { TimedOut = timedOut; Result = result; }
        public bool TimedOut { get; private set; }
        public FCRWorkResult<T> Result { get; private set; }
    }

    public sealed class FCRWorkHandle<T>
    {
        private readonly Func<FCRWorkState> _readState;
        private readonly Func<bool> _cancel;
        internal FCRWorkHandle(Guid id, string key, Task<FCRWorkResult<T>> completion,
            Func<FCRWorkState> readState, Func<bool> cancel)
        { Id = id; Key = key; Completion = completion; _readState = readState; _cancel = cancel; }
        public Guid Id { get; private set; }
        public string Key { get; private set; }
        public Task<FCRWorkResult<T>> Completion { get; private set; }
        /// <summary>本次工作是否被Worker接受排队或执行，Rejected/预取消为false。</summary>
        public bool Accepted { get; internal set; }
        public FCRWorkState State { get { return _readState(); } }
        /// <summary>请求协作取消；仅排队任务能够立即确认取消，运行中的工作需自己响应Token。</summary>
        public bool RequestCancel() { return _cancel(); }

        public async Task<FCRWaitObservation<T>> WaitAsync(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException("timeout");
            if (Completion.IsCompleted)
                return new FCRWaitObservation<T>(false, await Completion.ConfigureAwait(false));
            using (CancellationTokenSource timer = new CancellationTokenSource())
            {
                Task delay = Task.Delay(timeout, timer.Token);
                Task first = await Task.WhenAny(Completion, delay).ConfigureAwait(false);
                if (first != Completion)
                    return new FCRWaitObservation<T>(true, null);
                timer.Cancel();
                return new FCRWaitObservation<T>(false, await Completion.ConfigureAwait(false));
            }
        }
    }

    public sealed class FCRWorkerSnapshot
    {
        internal FCRWorkerSnapshot(int pending, int active, long finished,
            long rejected, long dropped, bool stopping)
        { Pending = pending; Active = active; Finished = finished; Rejected = rejected;
          Dropped = dropped; Stopping = stopping; }
        public int Pending { get; private set; }
        /// <summary>活动的逻辑工作数，包括异步等待；不等于正在运行的线程数。</summary>
        public int Active { get; private set; }
        public long Finished { get; private set; }
        public long Rejected { get; private set; }
        public long Dropped { get; private set; }
        public bool Stopping { get; private set; }
    }
}
