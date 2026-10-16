using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;

namespace CxVisual.Tests.FoundationSmoke
{
    internal static class FCRDedicatedExecutorTests
    {
        internal static void Run()
        {
            ThreadAffinityAndBoundedAdmission();
            CancelledPendingNeverInvokesSdk();
        }
        private static void Check(bool ok, string msg)
        { if (!ok) throw new InvalidOperationException("专用执行线程：" + msg); }
        private static T Await<T>(Task<T> task)
        {
            Task first = Task.WhenAny(task, Task.Delay(7000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(first, task)) throw new TimeoutException("专用线程测试超时");
            return task.GetAwaiter().GetResult();
        }
        private static void Await(Task task)
        {
            Task first = Task.WhenAny(task, Task.Delay(7000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(first, task)) throw new TimeoutException("专用线程测试超时");
            task.GetAwaiter().GetResult();
        }
        private static void ThreadAffinityAndBoundedAdmission()
        {
            FCRDedicatedExecutor executor = new FCRDedicatedExecutor("SDK.MTA", 1);
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using (ManualResetEventSlim release = new ManualResetEventSlim(false))
            {
                Task<int> first = executor.InvokeAsync(ct =>
                {
                    entered.TrySetResult(true);
                    release.Wait(3000);
                    return Thread.CurrentThread.ManagedThreadId;
                });
                Await(entered.Task);
                Task<int> second = executor.InvokeAsync(ct =>
                    Thread.CurrentThread.ManagedThreadId);
                bool rejected = false;
                try { Await(executor.InvokeAsync(ct => 123)); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected && executor.Pending == 1, "专用线程队列容量无效");
                release.Set();
                Check(Await(first) == executor.ThreadId && Await(second) == executor.ThreadId,
                    "同步SDK回调未在同一专用线程执行");
            }
            Await(executor.StopAsync());
            bool refused = false;
            try { Await(executor.InvokeAsync(ct => 1)); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused, "停止后仍接收专用线程任务");
        }
        private static void CancelledPendingNeverInvokesSdk()
        {
            FCRDedicatedExecutor executor = new FCRDedicatedExecutor("SDK.Cancellation", 2);
            using (ManualResetEventSlim release = new ManualResetEventSlim(false))
            {
                TaskCompletionSource<bool> started = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                Task<int> running = executor.InvokeAsync(ct =>
                { started.TrySetResult(true); release.Wait(3000); return 1; });
                Await(started.Task);
                int called = 0;
                using (CancellationTokenSource cancel = new CancellationTokenSource())
                {
                    Task<int> queued = executor.InvokeAsync(ct =>
                    { Interlocked.Increment(ref called); return 2; }, cancel.Token);
                    cancel.Cancel();
                    release.Set();
                    Await(running);
                    bool cancelled = false;
                    try { Await(queued); } catch (TaskCanceledException) { cancelled = true; }
                    Check(cancelled && called == 0, "取消的排队SDK动作仍执行");
                }
            }
            Await(executor.StopAsync());
        }
    }
}
