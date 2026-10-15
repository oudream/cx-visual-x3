using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;

namespace CxVisual.Tests.FoundationSmoke
{
    internal static class FCRWorkerTests
    {
        internal static void Run()
        {
            KeyedSerialAndAsyncWaiting();
            QueueOverflowAndDrop();
            CancellationAndGracefulStop();
            TimeoutDoesNotStopUnderlyingWork();
            ErrorIsObservable();
        }
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("F19 " + message); }
        private static T Await<T>(Task<T> task)
        {
            if (!task.Wait(TimeSpan.FromSeconds(8))) throw new TimeoutException("F19 test hang");
            return task.GetAwaiter().GetResult();
        }
        private static void Await(Task task)
        {
            if (!task.Wait(TimeSpan.FromSeconds(8))) throw new TimeoutException("F19 test hang");
            task.GetAwaiter().GetResult();
        }
        private static TaskCompletionSource<bool> Gate()
        { return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); }

        private static void KeyedSerialAndAsyncWaiting()
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(2, 3));
            TaskCompletionSource<bool> gate = Gate();
            TaskCompletionSource<bool> entered = Gate();
            int sameKeyStarted = 0;
            FCRWorkHandle<int> first = worker.Submit<int>(async ct =>
            {
                entered.TrySetResult(true);
                await gate.Task.ConfigureAwait(false);
                return 1;
            }, "Line1");
            Await(entered.Task);
            FCRWorkHandle<int> second = worker.Submit<int>(ct =>
            {
                Interlocked.Increment(ref sameKeyStarted);
                return Task.FromResult(2);
            }, "Line1");
            FCRWorkHandle<int> other = worker.Submit<int>(ct => Task.FromResult(3), "Line2");
            Check(Await(other.Completion).Value == 3, "different keys should progress while one awaits");
            Check(sameKeyStarted == 0, "same key ran while earlier async work awaited");
            Check(first.State == FCRWorkState.Waiting, "async wait should be observable");
            gate.TrySetResult(true);
            Check(Await(first.Completion).Succeeded && Await(second.Completion).Succeeded,
                "keyed completions lost");
            Check(sameKeyStarted == 1, "same key never resumed");
            Await(worker.StopAsync());
        }

        private static void QueueOverflowAndDrop()
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(
                1, 1, FCROverflowPolicy.DropOldestDroppable));
            TaskCompletionSource<bool> gate = Gate(), entered = Gate();
            FCRWorkHandle<int> running = worker.Submit<int>(async ct =>
            { entered.TrySetResult(true); await gate.Task.ConfigureAwait(false); return 10; }, "lane");
            Await(entered.Task);
            FCRWorkHandle<int> old = worker.Submit<int>(ct => Task.FromResult(11), "lane", true);
            FCRWorkHandle<int> fresh = worker.Submit<int>(ct => Task.FromResult(12), "lane");
            Check(Await(old.Completion).State == FCRWorkState.Dropped, "old droppable not reported");
            FCRWorkHandle<int> rejected = worker.Submit<int>(ct => Task.FromResult(13), "lane");
            Check(Await(rejected.Completion).State == FCRWorkState.Rejected,
                "queue full must reject non-droppable request");
            Check(fresh.Accepted && old.Accepted && !rejected.Accepted,
                "admission result must not be inferred from eventual completion");
            FCRWorkerSnapshot snap = worker.GetSnapshot();
            Check(snap.Pending == 1 && snap.Active == 1 && snap.Dropped == 1 && snap.Rejected == 1,
                "queue counters wrong");
            gate.TrySetResult(true);
            Check(Await(running.Completion).Value == 10 && Await(fresh.Completion).Value == 12,
                "wrong work executed after overflow");
            Await(worker.StopAsync());
        }

        private static void CancellationAndGracefulStop()
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(1, 2));
            TaskCompletionSource<bool> gate = Gate(), entered = Gate();
            FCRWorkHandle<int> running = worker.Submit<int>(async ct =>
            { entered.TrySetResult(true); await gate.Task.ConfigureAwait(false); return 1; }, "a");
            Await(entered.Task);
            FCRWorkHandle<int> queued = worker.Submit<int>(ct => Task.FromResult(2), "a");
            Check(queued.RequestCancel(), "queued cancel not accepted");
            FCRWorkResult<int> cancelled = Await(queued.Completion);
            Check(cancelled.State == FCRWorkState.Cancelled && cancelled.CancellationRequested,
                "queued cancellation not confirmed");
            Task stop = worker.StopAsync();
            Check(!stop.IsCompleted, "drain must wait for running task");
            Check(Await(worker.Submit<int>(ct => Task.FromResult(99)).Completion).State
                == FCRWorkState.Rejected, "stop should refuse submissions");
            gate.TrySetResult(true);
            Await(stop);
            Check(Await(running.Completion).Succeeded, "graceful drain interrupted running work");
        }

        private static void TimeoutDoesNotStopUnderlyingWork()
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(1, 1));
            TaskCompletionSource<bool> gate = Gate(), entered = Gate();
            FCRWorkHandle<int> work = worker.Submit<int>(async ct =>
            { entered.TrySetResult(true); await gate.Task.ConfigureAwait(false); return 42; });
            Await(entered.Task);
            FCRWaitObservation<int> observation = Await(work.WaitAsync(TimeSpan.FromMilliseconds(25)));
            Check(observation.TimedOut && observation.Result == null
                && !work.Completion.IsCompleted, "timeout incorrectly completed or cancelled operation");
            gate.TrySetResult(true);
            Check(Await(work.Completion).Value == 42, "late genuine result was discarded");
            Await(worker.StopAsync());
        }

        private static void ErrorIsObservable()
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(1, 1));
            FCRWorkHandle<int> bad = worker.Submit<int>(ct =>
            { throw new InvalidOperationException("expected-failure"); });
            FCRWorkResult<int> result = Await(bad.Completion);
            Check(result.State == FCRWorkState.Failed
                && result.Error != null && result.Error.Message == "expected-failure",
                "worker exception swallowed");
            Await(worker.StopAsync());
        }
    }
}
