using System;
using CxVisual.Foundation.Infrastructure.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Foundation.Infrastructure.Concurrency
{
    /// <summary>工作状态；Waiting占有逻辑执行槽，但不表示独占工作线程。</summary>
    public enum FCRWorkState { Queued, Running, Waiting, Completed, Failed, Cancelled, Dropped, Rejected }
    public enum FCROverflowPolicy { RejectNew, DropOldestDroppable }
    /// <summary>F19业务终态原因，必须依据同一工作最终结果而非队列计数推断。</summary>
    public enum FCRWorkTerminalReason
    {
        None=0, WorkerStopping=1, CancelledBeforeAdmission=2,
        EstimatedBytesExceedBudget=3, QueueCapacityExceeded=4,
        PendingByteBudgetExceeded=5, WaitingProducerLimitExceeded=6,
        OldestDroppableEvicted=7, CancelledWhileQueued=8,
        CancelledByStop=9, CancelledWhileWaitingAdmission=10
    }

    /// <summary>队列容量仅计算尚未开始的工作，正在运行或异步等待的工作占并发槽。</summary>
    public sealed class FCRWorkerOptions
    {
        public int MaxConcurrency { get; private set; }
        public int QueueCapacity { get; private set; }
        public FCROverflowPolicy OverflowPolicy { get; private set; }
        public long PendingByteBudget { get; private set; }
        /// <summary>跨Key高优先级连续超过较早工作时的最大次数；仍保证单Key FIFO。</summary>
        public int PriorityFairnessBurst { get; private set; }
        public int MaxWaitingProducers { get; private set; }
        public FCRWorkerOptions(int maxConcurrency, int queueCapacity,
            FCROverflowPolicy overflowPolicy = FCROverflowPolicy.RejectNew,
            long pendingByteBudget = long.MaxValue, int priorityFairnessBurst = 16,
            int maxWaitingProducers = 64)
        {
            if (maxConcurrency < 1) throw new ArgumentOutOfRangeException("maxConcurrency");
            if (queueCapacity < 0) throw new ArgumentOutOfRangeException("queueCapacity");
            if (pendingByteBudget < 0) throw new ArgumentOutOfRangeException("pendingByteBudget");
            if (priorityFairnessBurst < 1)
                throw new ArgumentOutOfRangeException("priorityFairnessBurst");
            if (maxWaitingProducers < 0) throw new ArgumentOutOfRangeException("maxWaitingProducers");
            MaxWaitingProducers = maxWaitingProducers;
            PriorityFairnessBurst = priorityFairnessBurst;
            PendingByteBudget = pendingByteBudget;
            MaxConcurrency = maxConcurrency;
            QueueCapacity = queueCapacity;
            OverflowPolicy = overflowPolicy;
        }
    }

    public sealed class FCRWorkResult<T>
    {
        internal FCRWorkResult(FCRWorkState state, T value, Exception error, bool cancelRequested,
            FCRWorkTerminalReason terminalReason = FCRWorkTerminalReason.None)
        { State = state; Value = value; Error = error;
          CancellationRequested = cancelRequested || state == FCRWorkState.Cancelled;
          TerminalReason = terminalReason; }
        public FCRWorkState State { get; private set; }
        public T Value { get; private set; }
        public Exception Error { get; private set; }
        public bool CancellationRequested { get; private set; }
        public bool Succeeded { get { return State == FCRWorkState.Completed; } }
        public FCRWorkTerminalReason TerminalReason { get; private set; }
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

    /// <summary>
    /// 复用原F16 FLD重要事件文件Sink，无额外持久库。只能在F19调度锁外
    /// 由Host对同一WorkHandle已结算的业务丢失/拒绝调用。
    /// Write=true仅代表日志服务接受，持久化须Flush并确认LastFailure。
    /// </summary>
    public static class FCRWorkAudit
    {
        public static bool LogLoss<T>(FLDLogService log, FCRWorkHandle<T> handle,
            FCRWorkResult<T> result, string module = "F19.Worker")
        {
            if (log == null) throw new ArgumentNullException("log");
            if (handle == null) throw new ArgumentNullException("handle");
            if (result == null) throw new ArgumentNullException("result");
            if (result.State != FCRWorkState.Dropped && result.State != FCRWorkState.Rejected)
                throw new ArgumentException("仅真实Drop/Reject可记录为丢失事实", "result");
            if (!handle.Completion.IsCompleted ||
                !object.ReferenceEquals(handle.Completion.GetAwaiter().GetResult(), result))
                throw new ArgumentException("结果不属于原WorkHandle", "result");
            if (string.IsNullOrWhiteSpace(module))
                throw new ArgumentException("module不能为空", "module");
            string message = "WorkId=" + handle.Id.ToString("N") +
                ";Key=" + handle.Key + ";State=" + result.State +
                ";Reason=" + result.TerminalReason;
            return log.Write(new FLDLogRecord(FLDLogLevel.Warning, module,
                "F19." + result.State, message, null,
                handle.Id.ToString("N"), true, result.Error));
        }
    }

    public sealed class FCRWorkerSnapshot
    {
        internal FCRWorkerSnapshot(int pending, int active, long finished,
            long rejected, long dropped, bool stopping, long pendingBytes, long byteBudget,
            int waitingProducers = 0, long admittedProducers = 0, long peakPendingBytes = 0,
            int peakPending = 0, int peakWaitingProducers = 0,
            long settledProducerWaits = 0, double maxProducerWaitMilliseconds = 0)
        { Pending = pending; Active = active; Finished = finished; Rejected = rejected;
          Dropped = dropped; Stopping = stopping; PendingBytes = pendingBytes;
          PendingByteBudget = byteBudget; WaitingProducers = waitingProducers;
          AdmittedProducers = admittedProducers; PeakPendingBytes = peakPendingBytes;
          PeakPending = peakPending; PeakWaitingProducers = peakWaitingProducers;
          SettledProducerWaits = settledProducerWaits;
          MaxProducerWaitMilliseconds = maxProducerWaitMilliseconds; }
        public int Pending { get; private set; }
        public long PendingBytes { get; private set; }
        public long PendingByteBudget { get; private set; }
        /// <summary>尚未入队的异步背压生产者数量，不计入Pending或字节预算。</summary>
        public int WaitingProducers { get; private set; }
        public long AdmittedProducers { get; private set; }
        public long PeakPendingBytes { get; private set; }
        public int PeakPending { get; private set; }
        public int PeakWaitingProducers { get; private set; }
        /// <summary>正式准入、等待取消、Stop拒绝合计次数；不含未等待的即时拒绝。</summary>
        public long SettledProducerWaits { get; private set; }
        public double MaxProducerWaitMilliseconds { get; private set; }
        /// <summary>活动的逻辑工作数，包括异步等待；不等于正在运行的线程数。</summary>
        public int Active { get; private set; }
        public long Finished { get; private set; }
        public long Rejected { get; private set; }
        public long Dropped { get; private set; }
        public bool Stopping { get; private set; }
    }
}
