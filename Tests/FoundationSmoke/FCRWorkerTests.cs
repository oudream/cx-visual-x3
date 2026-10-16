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
            DeveloperConvenienceAndKeyedStress();
            PriorityDoesNotBreakPerKeyFIFO();
            ByteBudgetAndOverflowAccounting();
            RandomizedCancellationAndStopStress();
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
        private static void DeveloperConvenienceAndKeyedStress()
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(4, 400));
            int posts = 0;
            FCRWorkHandle<bool> posted = worker.Post(() => Interlocked.Increment(ref posts));
            FCRWorkHandle<int> calculated = worker.Invoke(() => 6 * 7);
            Check(Await(posted.Completion).Succeeded && posts == 1 &&
                Await(calculated.Completion).Value == 42, "developer convenience APIs failed");

            System.Collections.Generic.List<int>[] observed =
                new System.Collections.Generic.List<int>[4];
            for (int key = 0; key < observed.Length; key++)
                observed[key] = new System.Collections.Generic.List<int>();
            System.Collections.Generic.List<FCRWorkHandle<int>> handles =
                new System.Collections.Generic.List<FCRWorkHandle<int>>();
            for (int i = 0; i < 200; i++)
            {
                int seq = i / 4, lane = i % 4;
                handles.Add(worker.Submit<int>(async token =>
                {
                    await Task.Yield();
                    observed[lane].Add(seq); // 每Key应独立串行，无需锁。
                    return seq;
                }, "Line" + lane.ToString()));
            }
            foreach (FCRWorkHandle<int> handle in handles)
                Check(Await(handle.Completion).Succeeded, "keyed stress lost work");
            for (int lane = 0; lane < 4; lane++)
            {
                Check(observed[lane].Count == 50, "unexpected lane work count");
                for (int n = 0; n < 50; n++)
                    Check(observed[lane][n] == n, "keyed FIFO order violated");
            }
            Await(worker.StopAsync());
        }

        private static void PriorityDoesNotBreakPerKeyFIFO()
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(1, 5));
            TaskCompletionSource<bool> entered = Gate(), release = Gate();
            FCRWorkHandle<int> blocking = worker.Submit<int>(async ct =>
            {
                entered.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                return 0;
            }, "block");
            Await(entered.Task);
            System.Collections.Generic.List<string> order = new System.Collections.Generic.List<string>();
            FCRWorkHandle<int> low = worker.Submit<int>(ct =>
            { order.Add("low"); return Task.FromResult(1); }, "line-a", priority: -1);
            FCRWorkHandle<int> sameKeyHigher = worker.Submit<int>(ct =>
            { order.Add("same-key"); return Task.FromResult(2); }, "line-a", priority: 100);
            FCRWorkHandle<int> high = worker.Submit<int>(ct =>
            { order.Add("high"); return Task.FromResult(3); }, "line-b", priority: 50);
            release.TrySetResult(true);
            Await(blocking.Completion);
            Await(Task.WhenAll(low.Completion, sameKeyHigher.Completion, high.Completion));
            Check(order.Count == 3 && order[0] == "high" &&
                order[1] == "low" && order[2] == "same-key",
                "priority broke FIFO in same key or other-key priority");
            Await(worker.StopAsync());
        }

        private static void ByteBudgetAndOverflowAccounting()
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(
                1, 4, FCROverflowPolicy.DropOldestDroppable, pendingByteBudget:100));
            TaskCompletionSource<bool> entered = Gate(), release = Gate();
            FCRWorkHandle<int> active = worker.Submit<int>(async ct =>
            {
                entered.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                return 1;
            }, "line");
            Await(entered.Task);
            FCRWorkHandle<int> old = worker.Submit<int>(ct => Task.FromResult(2),
                "line", droppable:true, estimatedBytes:70);
            Check(worker.GetSnapshot().PendingBytes == 70, "pending bytes not reserved");
            FCRWorkHandle<int> newer = worker.Submit<int>(ct => Task.FromResult(3),
                "line", estimatedBytes:60);
            Check(Await(old.Completion).State == FCRWorkState.Dropped &&
                worker.GetSnapshot().PendingBytes == 60,
                "eviction did not free the declared byte budget");
            FCRWorkHandle<int> impossible = worker.Submit<int>(
                ct => Task.FromResult(4), "line", estimatedBytes:200);
            Check(Await(impossible.Completion).State == FCRWorkState.Rejected &&
                newer.Accepted && worker.GetSnapshot().PendingBytes == 60,
                "oversize submission should reject without evicting unrelated work");
            FCRWorkHandle<int> expendable = worker.Submit<int>(
                ct => Task.FromResult(88), "line", droppable: true, estimatedBytes:30);
            FCRWorkHandle<int> oversize = worker.Submit<int>(
                ct => Task.FromResult(89), "line", estimatedBytes:200);
            Check(Await(oversize.Completion).State == FCRWorkState.Rejected &&
                expendable.Accepted && !expendable.Completion.IsCompleted &&
                worker.GetSnapshot().PendingBytes == 90,
                "oversize request evicted a previously accepted droppable job");
            Check(newer.RequestCancel() && worker.GetSnapshot().PendingBytes == 30,
                "pending cancellation leaked reserved bytes");
            Check(expendable.RequestCancel() && worker.GetSnapshot().PendingBytes == 0,
                "second pending cancellation leaked reserved bytes");
            release.TrySetResult(true);
            Await(active.Completion);
            Await(worker.StopAsync());
            Check(worker.GetSnapshot().PendingBytes == 0, "drain retained pending byte budget");

            FCRWorker second = new FCRWorker(new FCRWorkerOptions(1, 3,
                pendingByteBudget:50));
            TaskCompletionSource<bool> gate = Gate(), ready = Gate();
            second.Submit<int>(async ct =>
            { ready.TrySetResult(true); await gate.Task.ConfigureAwait(false); return 1; }, "x");
            Await(ready.Task);
            second.Submit<int>(ct => Task.FromResult(1), "x", estimatedBytes:40);
            Task closing = second.StopAsync(true);
            Check(second.GetSnapshot().PendingBytes == 0, "stop(cancel) leaked byte accounting");
            gate.TrySetResult(true);
            Await(closing);
        }

        private static void RandomizedCancellationAndStopStress()
        {
            for (int round = 0; round < 3; round++)
            {
                FCRWorker worker = new FCRWorker(new FCRWorkerOptions(6, 70,
                    FCROverflowPolicy.DropOldestDroppable, pendingByteBudget:8000));
                TaskCompletionSource<bool> ready = Gate(), release = Gate();
                System.Collections.Generic.List<FCRWorkHandle<int>> all =
                    new System.Collections.Generic.List<FCRWorkHandle<int>>();
                for (int j = 0; j < 6; j++)
                {
                    all.Add(worker.Submit<int>(async ct =>
                    {
                        ready.TrySetResult(true);
                        await release.Task.ConfigureAwait(false);
                        return 1;
                    }, "running-" + j));
                }
                Await(ready.Task);
                for (int i = 0; i < 600; i++)
                {
                    int iteration = i;
                    FCRWorkHandle<int> item = worker.Submit<int>(async ct =>
                    {
                        await Task.Delay(iteration % 3, ct).ConfigureAwait(false);
                        return iteration;
                    }, "lane-" + (i % 12), droppable:(i % 3 == 0),
                       estimatedBytes:100 + i % 50);
                    all.Add(item);
                    if (i % 5 == 0) item.RequestCancel();
                    FCRWorkerSnapshot mid = worker.GetSnapshot();
                    Check(mid.Pending >= 0 && mid.Pending <= 70 &&
                        mid.PendingBytes >= 0 && mid.PendingBytes <= 8000,
                        "byte/count budget exceeded during cancel and overflow race");
                }
                Task closing = worker.StopAsync(true);
                release.TrySetResult(true);
                Await(closing);
                foreach (FCRWorkHandle<int> item in all)
                {
                    FCRWorkResult<int> outcome = Await(item.Completion);
                    Check(outcome.State == FCRWorkState.Completed ||
                        outcome.State == FCRWorkState.Cancelled ||
                        outcome.State == FCRWorkState.Dropped ||
                        outcome.State == FCRWorkState.Rejected,
                        "work ended in nonterminal state after shutdown");
                }
                FCRWorkerSnapshot end = worker.GetSnapshot();
                Check(end.Active == 0 && end.Pending == 0 && end.PendingBytes == 0 &&
                    end.Stopping, "stop/cancel left live work or reserved memory bytes");
            }
        }

    }
}
