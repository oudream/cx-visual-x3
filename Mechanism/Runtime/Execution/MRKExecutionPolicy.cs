using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Data.RuntimeData;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>
    /// 节点执行策略。重试只允许显式声明幂等的工作。
    /// 超时先请求协作取消，并等待原任务结算后才允许后续节点运行；
    /// 超时本身不证明外部设备命令没有执行。
    /// </summary>
    public sealed class MRKExecutionPolicy
    {
        public int MaxAttempts { get; private set; }
        public TimeSpan RetryDelay { get; private set; }
        public TimeSpan? Timeout { get; private set; }
        public bool Idempotent { get; private set; }
        public Func<Exception, bool> RetryCondition { get; private set; }

        public MRKExecutionPolicy(int maxAttempts = 1,
            TimeSpan? retryDelay = null, TimeSpan? timeout = null,
            bool idempotent = false, Func<Exception, bool> retryCondition = null)
        {
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException("maxAttempts");
            if (maxAttempts > 1 && !idempotent)
                throw new ArgumentException("非幂等任务不得配置自动重试", "idempotent");
            TimeSpan backoff = retryDelay ?? TimeSpan.Zero;
            if (backoff < TimeSpan.Zero) throw new ArgumentOutOfRangeException("retryDelay");
            if (timeout.HasValue && timeout.Value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("timeout");
            MaxAttempts = maxAttempts;
            RetryDelay = backoff;
            Timeout = timeout;
            Idempotent = idempotent;
            RetryCondition = retryCondition;
        }

        public async Task<object> ExecuteAsync(MRKExecutionContext context,
            Func<MRKExecutionContext, CancellationToken, Task<object>> action,
            CancellationToken cancellation, Action onTimeout = null)
        {
            if (action == null) throw new ArgumentNullException("action");
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    using (CancellationTokenSource scoped =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                    {
                        // 重试只改变尝试身份，不创建第二个MDS Frame或改变Invocation。
                        // 独立使用策略的旧API允许null或根上下文；仅真实MRK节点拥有Invocation。
                        MRKExecutionContext attemptContext = context != null &&
                            context.Invocation != null ? context.WithAttempt(attempt) : context;
                        Task<object> operation = action(attemptContext, scoped.Token);
                        if (operation == null)
                            throw new InvalidOperationException("节点执行器返回空任务");
                        if (Timeout.HasValue && !operation.IsCompleted)
                        {
                            Task winner = await Task.WhenAny(operation,
                                Task.Delay(Timeout.Value)).ConfigureAwait(false);
                            if (winner != operation && !operation.IsCompleted)
                            {
                                // 先公开Unknown。诊断失败也不能提前释放尚未结算的资源。
                                Exception observationFailure = null;
                                try { if (onTimeout != null) onTimeout(); }
                                catch (Exception ex) { observationFailure = ex; }
                                try { scoped.Cancel(); }
                                catch (Exception ex)
                                {
                                    observationFailure = observationFailure == null ? ex :
                                        new AggregateException(observationFailure, ex);
                                }
                                try { await operation.ConfigureAwait(false); }
                                catch (Exception) { /* 实际结算之前仍保持原资源租约 */ }
                                if (observationFailure != null)
                                    // 诊断失败仍属于结果不确定的超时，不允许幂等重试分支误捕。
                                    throw new TimeoutException(
                                        "超时诊断或取消通知失败，底层任务已结算",
                                        observationFailure);
                                throw new TimeoutException("节点执行超过策略超时，已等待原工作结束");
                            }
                        }
                        return await operation.ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                { throw; }
                catch (OperationCanceledException)
                { throw; }
                catch (TimeoutException)
                {
                    // 即便显式幂等，也不自动重试结果可能不确定的外部动作。
                    throw;
                }
                catch (Exception ex)
                {
                    if (attempt >= MaxAttempts || !Idempotent ||
                        (RetryCondition != null && !RetryCondition(ex)))
                        throw;
                    // 每次重试都是新逻辑尝试；旧PendingOutput和Local不得延续。
                    MDSDataContext managed = context.Data as MDSDataContext;
                    if (managed != null) managed.PrepareRetry();
                    if (RetryDelay > TimeSpan.Zero)
                        await Task.Delay(RetryDelay, cancellation).ConfigureAwait(false);
                }
            }
            throw new InvalidOperationException("节点执行重试策略未返回结果");
        }
    }
}
