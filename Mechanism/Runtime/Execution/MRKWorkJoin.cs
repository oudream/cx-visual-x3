using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>已通过统一Worker提交的分支之间进行归并，不再启动第二套调度器。</summary>
    public static class MRKWorkJoin
    {
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
