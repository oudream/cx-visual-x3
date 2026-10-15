using System;
using System.Threading;
using System.Threading.Tasks;

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
            CancellationToken cancellation)
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
                        Task<object> operation = action(context, scoped.Token);
                        if (operation == null)
                            throw new InvalidOperationException("节点执行器返回空任务");
                        if (Timeout.HasValue && !operation.IsCompleted)
                        {
                            Task winner = await Task.WhenAny(operation,
                                Task.Delay(Timeout.Value)).ConfigureAwait(false);
                            if (winner != operation && !operation.IsCompleted)
                            {
                                scoped.Cancel();
                                // 等待真实工作完成或响应取消，防止超时后仍在运行的外部工作
                                // 与下一个节点并发访问同一资源。
                                try { await operation.ConfigureAwait(false); }
                                catch (OperationCanceledException) { }
                                catch (Exception) { }
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
                    if (RetryDelay > TimeSpan.Zero)
                        await Task.Delay(RetryDelay, cancellation).ConfigureAwait(false);
                }
            }
            throw new InvalidOperationException("节点执行重试策略未返回结果");
        }
    }
}
