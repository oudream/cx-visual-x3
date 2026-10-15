using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;

namespace CxVisual.Tests.FoundationSmoke
{
    internal static class FCRSchedulingTests
    {
        internal static void Run()
        {
            GateBroadcastAndCancellation();
            PeriodicNeverReentersAndStops();
            PeriodicFailureIsVisible();
        }
        private static T Await<T>(Task<T> work)
        {
            Task winner = Task.WhenAny(work, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, work)) throw new TimeoutException("F19调度测试超时");
            return work.GetAwaiter().GetResult();
        }
        private static void Await(Task work)
        {
            Task winner = Task.WhenAny(work, Task.Delay(6000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, work)) throw new TimeoutException("F19调度测试超时");
            work.GetAwaiter().GetResult();
        }
        private static TaskCompletionSource<bool> Gate()
        { return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); }
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("F19调度：" + message); }
        private static void GateBroadcastAndCancellation()
        {
            FCRAsyncGate gate = new FCRAsyncGate();
            using (CancellationTokenSource cancel = new CancellationTokenSource())
            {
                Task one = gate.WaitAsync();
                Task two = gate.WaitAsync(cancel.Token);
                Check(!one.IsCompleted && !two.IsCompleted, "关闭的门意外放行");
                cancel.Cancel();
                bool cancelled = false;
                try { Await(two); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && !one.IsCompleted, "取消单个等待者影响其他等待者");
                gate.Open();
                Await(one);
                Await(gate.WaitAsync());
                gate.Close();
                Task three = gate.WaitAsync();
                Check(!three.IsCompleted, "重关未生效");
                gate.Open();
                Await(three);
            }
        }
        private static void PeriodicNeverReentersAndStops()
        {
            int executing = 0, max = 0, ticks = 0;
            TaskCompletionSource<bool> twice = Gate();
            FCRPeriodicJob job = new FCRPeriodicJob(TimeSpan.FromMilliseconds(10),
                async token =>
                {
                    int active = Interlocked.Increment(ref executing);
                    if (active > max) max = active;
                    await Task.Delay(15, token).ConfigureAwait(false);
                    if (Interlocked.Increment(ref ticks) >= 2) twice.TrySetResult(true);
                    Interlocked.Decrement(ref executing);
                }, true);
            Await(twice.Task);
            Await(job.StopAsync());
            Check(max == 1 && executing == 0 && job.Executions >= 2,
                "周期任务重入或停止不完整");
            Check(job.Completion.IsCompleted, "停止后任务未完成");
        }
        private static void PeriodicFailureIsVisible()
        {
            FCRPeriodicJob job = new FCRPeriodicJob(TimeSpan.FromMilliseconds(10),
                token => { throw new InvalidOperationException("故障可见"); }, true);
            bool failed = false;
            try { Await(job.Completion); }
            catch (InvalidOperationException ex) { failed = ex.Message == "故障可见"; }
            Check(failed, "周期任务故障被吞掉");
        }
    }
}
