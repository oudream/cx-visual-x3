using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>
    /// 提前汇合只结束观察者等待，不隐式结束尚在运行的外部操作。
    /// AllSettled必须用在分支资源回收前；不合作的Provider可能使它长期未完成。
    /// </summary>
    public sealed class MRKJoinResult<T>
    {
        internal MRKJoinResult(FCRWorkHandle<T> winner, FCRWorkResult<T> winningResult,
            Task<FCRWorkResult<T>[]> allSettled)
        { Winner = winner; WinningResult = winningResult; AllSettled = allSettled; }
        public bool Succeeded { get { return WinningResult != null && WinningResult.Succeeded; } }
        public FCRWorkHandle<T> Winner { get; private set; }
        public FCRWorkResult<T> WinningResult { get; private set; }
        public Task<FCRWorkResult<T>[]> AllSettled { get; private set; }
    }


    /// <summary>
    /// Join等待的观察结果，不是设备完成凭证。
    /// 超时/观察者取消时 Results==null，必须另等 AllSettled 才能回收工作资源。
    /// </summary>
    public sealed class MRKJoinWaitObservation<T>
    {
        internal MRKJoinWaitObservation(bool timedOut, bool cancelled,
            FCRWorkResult<T>[] results, Task<FCRWorkResult<T>[]> allSettled)
        {
            TimedOut = timedOut;
            Cancelled = cancelled;
            Results = results == null ? null :
                (IReadOnlyList<FCRWorkResult<T>>)Array.AsReadOnly((FCRWorkResult<T>[])results.Clone());
            AllSettled = allSettled;
        }
        public bool TimedOut { get; private set; }
        public bool Cancelled { get; private set; }
        public bool Settled { get { return Results != null; } }
        public IReadOnlyList<FCRWorkResult<T>> Results { get; private set; }
        public Task<FCRWorkResult<T>[]> AllSettled { get; private set; }
    }

    /// <summary>已通过统一Worker提交的分支之间进行归并，不再启动第二套调度器。</summary>
    public static class MRKWorkJoin
    {
        /// <summary>
        /// All：按调用方输入顺序等待所有分支结算，保留失败、取消和拒绝的原结果；
        /// 调用方仅可在结果全部到达后释放共享资源，不隐式重放或取消外部操作。
        /// 全部Succeeded才表示All成功，任何非成功均不能当作成功Join。
        /// </summary>
        public static Task<FCRWorkResult<T>[]> AllAsync<T>(
            IEnumerable<FCRWorkHandle<T>> source, bool requestCancelRemainingOnFailure = false)
        {
            if (source == null) throw new ArgumentNullException("source");
            List<FCRWorkHandle<T>> handles = source.ToList();
            if (handles.Count == 0 || handles.Any(x => x == null))
                throw new ArgumentException("必须提供有效的执行分支", "source");
            if (handles.Select(x => x.Id).Distinct().Count() != handles.Count)
                throw new ArgumentException("执行分支不可重复", "source");
            // 默认不取消外部操作；只有调用方明确选择失败时协作取消。
            return requestCancelRemainingOnFailure ? AllCancelOnFailureAsync(handles) :
                Task.WhenAll(handles.Select(x => x.Completion));
        }



        /// <summary>
        /// 仅约束调用方等待时间；超时或取消观察，不会假装未完成的外部操作已经停止。
        /// requestCancelRemainingOnAbort 必须由调用方明确选择，且仅发送协作取消。
        /// 若未全部结算，Results为null，AllSettled始终保留原始所有分支的结果。
        /// 参数错误会使返回的Task失败，调用方须await以观察异常；不创建第二等待线程。
        /// </summary>
        public static async Task<MRKJoinWaitObservation<T>> ObserveAllAsync<T>(
            IEnumerable<FCRWorkHandle<T>> source, TimeSpan timeout,
            CancellationToken cancellation = default(CancellationToken),
            bool requestCancelRemainingOnAbort = false)
        {
            if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException("timeout");
            if (source == null) throw new ArgumentNullException("source");
            // AllAsync 对空分支、重复WorkId和null分支做统一校验。
            List<FCRWorkHandle<T>> handles = source.ToList();
            Task<FCRWorkResult<T>[]> allSettled = AllAsync(handles);
            if (allSettled.IsCompleted)
                return new MRKJoinWaitObservation<T>(false, false,
                    await allSettled.ConfigureAwait(false), allSettled);
            if (cancellation.IsCancellationRequested)
            {
                if (requestCancelRemainingOnAbort)
                    foreach (FCRWorkHandle<T> handle in handles) handle.RequestCancel();
                return new MRKJoinWaitObservation<T>(false, true, null, allSettled);
            }
            using (CancellationTokenSource delayCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                Task deadline = Task.Delay(timeout, delayCancellation.Token);
                Task winner = await Task.WhenAny(allSettled, deadline).ConfigureAwait(false);
                // 若二者在同一时间完成，以真实的已结算结果优先，不制造虚假超时。
                if (allSettled.IsCompleted || object.ReferenceEquals(winner, allSettled))
                {
                    delayCancellation.Cancel();
                    return new MRKJoinWaitObservation<T>(false, false,
                        await allSettled.ConfigureAwait(false), allSettled);
                }
                bool aborted = cancellation.IsCancellationRequested;
                if (requestCancelRemainingOnAbort)
                    foreach (FCRWorkHandle<T> handle in handles)
                        if (!handle.Completion.IsCompleted) handle.RequestCancel();
                return new MRKJoinWaitObservation<T>(!aborted, aborted, null, allSettled);
            }
        }

        private static async Task<FCRWorkResult<T>[]> AllCancelOnFailureAsync<T>(
            IList<FCRWorkHandle<T>> handles)
        {
            List<FCRWorkHandle<T>> remaining = new List<FCRWorkHandle<T>>(handles);
            bool cancelSent = false;
            while (remaining.Count > 0)
            {
                Task<FCRWorkResult<T>> done =
                    await Task.WhenAny(remaining.Select(x => x.Completion)).ConfigureAwait(false);
                FCRWorkHandle<T> finished =
                    remaining.Find(x => object.ReferenceEquals(x.Completion, done));
                FCRWorkResult<T> outcome = await done.ConfigureAwait(false);
                remaining.Remove(finished);
                if (!outcome.Succeeded && !cancelSent)
                {
                    cancelSent = true;
                    // 只发送协作取消：不强杀工作、不释放尚在执行的租约。
                    foreach (FCRWorkHandle<T> stillRunning in remaining)
                        stillRunning.RequestCancel();
                }
            }
            // 即使取消不合作，也必须等待底层完成才把All交给后继。
            return await Task.WhenAll(handles.Select(x => x.Completion)).ConfigureAwait(false);
        }

        /// <summary>
        /// ConditionalJoin：第一个满足明确谓词的已结算分支胜出。
        /// 未命中不视为成功；无命中返回空Winner并通过AllSettled保留失败原因。
        /// 若提前返回，调用者仍须先等待AllSettled才能释放共享资源。
        /// 条件谓词自身抛错时，优先通知剩余分支协作取消，等待全部实际结算后重抛原错误；
        /// 不把条件异常或本地取消误当成外部设备已经停止。
        /// </summary>
        public static async Task<MRKJoinResult<T>> FirstMatchingAsync<T>(
            IEnumerable<FCRWorkHandle<T>> source,
            Func<FCRWorkResult<T>, bool> condition,
            bool requestCancelRemaining = false)
        {
            if (condition == null) throw new ArgumentNullException("condition");
            if (source == null) throw new ArgumentNullException("source");
            List<FCRWorkHandle<T>> handles = source.ToList();
            if (handles.Count == 0 || handles.Any(x => x == null))
                throw new ArgumentException("必须提供有效的执行分支", "source");
            if (handles.Select(x => x.Id).Distinct().Count() != handles.Count)
                throw new ArgumentException("执行分支不可重复", "source");
            Task<FCRWorkResult<T>[]> allSettled =
                Task.WhenAll(handles.Select(x => x.Completion));
            List<FCRWorkHandle<T>> remaining = new List<FCRWorkHandle<T>>(handles);
            while (remaining.Count > 0)
            {
                Task<FCRWorkResult<T>> done =
                    await Task.WhenAny(remaining.Select(x => x.Completion)).ConfigureAwait(false);
                FCRWorkHandle<T> finished =
                    remaining.Find(x => object.ReferenceEquals(x.Completion, done));
                FCRWorkResult<T> outcome = await done.ConfigureAwait(false);
                remaining.Remove(finished);
                bool matched;
                try { matched = condition(outcome); }
                catch
                {
                    // 谓词本身出错先请求协作取消，再等待尾部实际结算。
                    // 取消通知与真正结算是两个独立时点：不强行释放仍在执行的设备资源。
                    foreach (FCRWorkHandle<T> pending in remaining) pending.RequestCancel();
                    await allSettled.ConfigureAwait(false);
                    throw;
                }
                if (!matched) continue;
                if (requestCancelRemaining)
                    foreach (FCRWorkHandle<T> pending in remaining) pending.RequestCancel();
                return new MRKJoinResult<T>(finished, outcome, allSettled);
            }
            return new MRKJoinResult<T>(null, null, allSettled);
        }

        /// <summary>
        /// Any：第一个完成的分支即返回，允许返回失败结果；与FirstSuccessful语义不同。
        /// 剩余分支的生命周期通过AllSettled显式结算，不能提前回收共享数据。
        /// </summary>
        public static async Task<MRKJoinResult<T>> FirstCompletedAsync<T>(
            IEnumerable<FCRWorkHandle<T>> source, bool requestCancelRemaining = false)
        {
            if (source == null) throw new ArgumentNullException("source");
            List<FCRWorkHandle<T>> handles = source.ToList();
            if (handles.Count == 0 || handles.Any(x => x == null))
                throw new ArgumentException("必须提供有效的执行分支", "source");
            if (handles.Select(x => x.Id).Distinct().Count() != handles.Count)
                throw new ArgumentException("执行分支不可重复", "source");

            Task<FCRWorkResult<T>[]> allSettled =
                Task.WhenAll(handles.Select(x => x.Completion));
            Task<FCRWorkResult<T>> first = await Task.WhenAny(
                handles.Select(x => x.Completion)).ConfigureAwait(false);
            FCRWorkHandle<T> winner = handles.Find(x =>
                object.ReferenceEquals(x.Completion, first));
            FCRWorkResult<T> result = await first.ConfigureAwait(false);
            if (requestCancelRemaining)
                foreach (FCRWorkHandle<T> handle in handles)
                    if (!object.ReferenceEquals(handle, winner)) handle.RequestCancel();
            return new MRKJoinResult<T>(winner, result, allSettled);
        }

        public static async Task<MRKJoinResult<T>> FirstSuccessfulAsync<T>(
            IEnumerable<FCRWorkHandle<T>> source, bool requestCancelRemaining = true)
        {
            if (source == null) throw new ArgumentNullException("source");
            List<FCRWorkHandle<T>> handles = source.ToList();
            if (handles.Count == 0 || handles.Any(x => x == null))
                throw new ArgumentException("必须提供有效的执行分支", "source");
            if (handles.Select(x => x.Id).Distinct().Count() != handles.Count)
                throw new ArgumentException("执行分支不可重复", "source");
            Task<FCRWorkResult<T>[]> allSettled =
                Task.WhenAll(handles.Select(x => x.Completion));
            List<FCRWorkHandle<T>> remaining = new List<FCRWorkHandle<T>>(handles);
            while (remaining.Count > 0)
            {
                Task<FCRWorkResult<T>> finished =
                    await Task.WhenAny(remaining.Select(x => x.Completion)).ConfigureAwait(false);
                FCRWorkHandle<T> handle = remaining.Find(x => object.ReferenceEquals(x.Completion, finished));
                FCRWorkResult<T> outcome = await finished.ConfigureAwait(false);
                remaining.Remove(handle);
                if (outcome.Succeeded)
                {
                    if (requestCancelRemaining)
                        foreach (FCRWorkHandle<T> loser in remaining) loser.RequestCancel();
                    return new MRKJoinResult<T>(handle, outcome, allSettled);
                }
            }
            // 所有分支均失败/取消/拒绝，不伪造成功结果。
            return new MRKJoinResult<T>(null, null, allSettled);
        }
    }
}
