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
            ExternalCancellationFreesQueuedCapacity();
            TimeoutDoesNotStopUnderlyingWork();
            ErrorIsObservable();
            DeveloperConvenienceAndKeyedStress();
            PriorityDoesNotBreakPerKeyFIFO();
            PriorityFairnessBoundedAcrossKeys();
            ByteBudgetAndOverflowAccounting();
            R08_AsyncProducerBackpressureAndKeyFIFO();
            R08_WaiterCancellationAndStopAreBounded();
            R08_ProducerBurstCancelAndDrain();
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


        private static void ExternalCancellationFreesQueuedCapacity()
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(
                1, 1, pendingByteBudget: 32));
            TaskCompletionSource<bool> entered = Gate(), release = Gate();
            CancellationTokenSource queuedCancel = new CancellationTokenSource();
            FCRWorkHandle<int> first = worker.Submit<int>(async ct =>
            {
                entered.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                return 1;
            }, "line");
            try
            {
                Await(entered.Task);
                int unintended = 0;
                FCRWorkHandle<int> queued = worker.Submit<int>(ct =>
                {
                    Interlocked.Increment(ref unintended);
                    return Task.FromResult(2);
                }, "line", cancellation:queuedCancel.Token, estimatedBytes:24);
                Check(queued.Accepted && worker.GetSnapshot().Pending == 1 &&
                    worker.GetSnapshot().PendingBytes == 24,
                    "外部取消前没有正确预留待运行预算");
                queuedCancel.Cancel();
                Check(Await(queued.Completion).State == FCRWorkState.Cancelled &&
                    worker.GetSnapshot().Pending == 0 &&
                    worker.GetSnapshot().PendingBytes == 0 && unintended == 0,
                    "外部Token取消未立即移除排队项/释放字节预算");
                FCRWorkHandle<int> successor = worker.Submit<int>(
                    ct => Task.FromResult(3), "line", estimatedBytes:30);
                Check(successor.Accepted && worker.GetSnapshot().PendingBytes == 30,
                    "已取消工作阻塞了后续新工作的准入");
                release.TrySetResult(true);
                Check(Await(first.Completion).Succeeded &&
                    Await(successor.Completion).Value == 3,
                    "取消后续工作时错误影响正在运行和后续任务");
            }
            finally
            {
                release.TrySetResult(true);
                queuedCancel.Dispose();
                Await(worker.StopAsync(true));
            }
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

        /// <summary>
        /// R08：持续高优先级流不得无限饿死更早排队的低优先级工作；
        /// 同Key原FIFO、等待配额、停止排空与PendingBytes不改变。
        /// </summary>
        private static void PriorityFairnessBoundedAcrossKeys()
        {
            bool invalid = false;
            try { new FCRWorkerOptions(1, 12, priorityFairnessBurst: 0); }
            catch (ArgumentOutOfRangeException) { invalid = true; }
            Check(invalid, "priorityFairnessBurst=0应在配置时拒绝");

            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(1, 16,
                pendingByteBudget: 200, priorityFairnessBurst: 3));
            TaskCompletionSource<bool> entered = Gate(), release = Gate();
            FCRWorkHandle<int> barrier = worker.Submit<int>(async ct =>
            {
                entered.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                return 0;
            }, "barrier");
            Await(entered.Task);
            System.Collections.Generic.List<string> order =
                new System.Collections.Generic.List<string>();
            FCRWorkHandle<int> low = worker.Submit<int>(ct =>
            {
                order.Add("low");
                return Task.FromResult(1);
            }, "slow-key", priority: -100, estimatedBytes: 10);
            System.Collections.Generic.List<FCRWorkHandle<int>> high =
                new System.Collections.Generic.List<FCRWorkHandle<int>>();
            for (int i = 0; i < 10; i++)
            {
                int number = i;
                high.Add(worker.Submit<int>(ct =>
                {
                    order.Add("high-" + number);
                    return Task.FromResult(number);
                }, "high-key", priority: 100, estimatedBytes: 10));
            }
            Check(worker.GetSnapshot().Pending == 11 &&
                worker.GetSnapshot().PendingBytes == 110,
                "公平性检查不能绕过原队列和字节背压");
            release.TrySetResult(true);
            Await(barrier.Completion);
            Check(Await(low.Completion).Succeeded, "低优先级工作饥饿或拒绝");
            foreach (FCRWorkHandle<int> item in high)
                Check(Await(item.Completion).Succeeded, "高优先级工作丢失");
            Check(order.Count == 11 && order[0] == "high-0" &&
                order[1] == "high-1" && order[2] == "high-2" &&
                order[3] == "low",
                "高优先级连续超车必须有界且同Key仍FIFO");
            for (int i = 0; i < high.Count; i++)
                Check(order.IndexOf("high-" + i) < 0 ? false :
                    (i == 0 || order.IndexOf("high-" + (i - 1)) <
                        order.IndexOf("high-" + i)), "跨Key公平调度破坏同Key FIFO");
            Await(worker.StopAsync());
            Check(worker.GetSnapshot().Active == 0 &&
                worker.GetSnapshot().Pending == 0 &&
                worker.GetSnapshot().PendingBytes == 0,
                "公平调度停止后Worker资源/容量泄漏");
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


        /// <summary>R08：异步等待既不Drop现有业务也不突破原字节预算，同Key仍FIFO。</summary>
        private static void R08_AsyncProducerBackpressureAndKeyFIFO()
        {
            FCRWorker worker = new FCRWorker(new FCRWorkerOptions(1,1,
                FCROverflowPolicy.DropOldestDroppable, pendingByteBudget:8,
                maxWaitingProducers:2));
            TaskCompletionSource<bool> entered=Gate(),release=Gate();
            System.Collections.Generic.List<int> order=new System.Collections.Generic.List<int>();
            FCRWorkHandle<int> active=worker.Submit<int>(async ct=>{
                entered.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                order.Add(0);
                return 0;
            },"lane");
            try
            {
                Await(entered.Task);
                FCRWorkHandle<int> queued=worker.Submit<int>(ct=>{
                    order.Add(1);
                    return Task.FromResult(1);
                },"lane",droppable:true,estimatedBytes:8);
                Task<FCRWorkHandle<int>> first=worker.SubmitWhenCapacityAsync<int>(
                    ct=>{order.Add(2);return Task.FromResult(2);},
                    "lane",estimatedBytes:4);
                Task<FCRWorkHandle<int>> second=worker.SubmitWhenCapacityAsync<int>(
                    ct=>{order.Add(3);return Task.FromResult(3);},
                    "lane",estimatedBytes:4);
                FCRWorkerSnapshot waiting=worker.GetSnapshot();
                Check(!first.IsCompleted&&!second.IsCompleted &&
                    waiting.Pending==1 && waiting.PendingBytes==8 &&
                    waiting.WaitingProducers==2 && waiting.AdmittedProducers==0,
                    "R08满队列生产者应异步等待，尚未入队不能占用Pending或字节预算");
                FCRWorkHandle<int> full=Await(worker.SubmitWhenCapacityAsync<int>(
                    ct=>Task.FromResult(99),"lane",estimatedBytes:2));
                FCRWorkHandle<int> oversize=Await(worker.SubmitWhenCapacityAsync<int>(
                    ct=>Task.FromResult(98),"lane",estimatedBytes:9));
                Check(Await(full.Completion).State==FCRWorkState.Rejected &&
                    Await(oversize.Completion).State==FCRWorkState.Rejected &&
                    !queued.Completion.IsCompleted,
                    "R08等待预算满/单任务过大必须拒绝，不能淘汰已有Droppable业务");
                Check(queued.RequestCancel(),"R08已排队任务取消未生效");
                FCRWorkHandle<int> admitted=Await(first);
                Check(admitted.Accepted && !second.IsCompleted &&
                    worker.GetSnapshot().PendingBytes==4 &&
                    worker.GetSnapshot().WaitingProducers==1,
                    "R08释放容量只应让FIFO第一名生产者原子入队");
                release.TrySetResult(true);
                FCRWorkHandle<int> next=Await(second);
                Check(next.Accepted && Await(active.Completion).Succeeded &&
                    Await(admitted.Completion).Value==2 &&
                    Await(next.Completion).Value==3 &&
                    order.Count==3&&order[0]==0&&order[1]==2&&order[2]==3,
                    "R08等待者未按原Key FIFO执行或取消的Job被执行");
                FCRWorkerSnapshot end=worker.GetSnapshot();
                Check(end.PendingBytes==0&&end.WaitingProducers==0&&
                    end.AdmittedProducers==2&&end.PeakPendingBytes==8 &&
                    end.Dropped==0&&end.Rejected==2,
                    "R08背压统计、峰值字节或Drop被错误修改");
            }
            finally
            {
                release.TrySetResult(true);
                Await(worker.StopAsync(true));
            }
        }

        private static void R08_WaiterCancellationAndStopAreBounded()
        {
            bool invalid=false;
            try{new FCRWorkerOptions(1,1,maxWaitingProducers:-1);}
            catch(ArgumentOutOfRangeException){invalid=true;}
            Check(invalid,"R08非法生产者等待上限必须拒绝");
            FCRWorker worker=new FCRWorker(new FCRWorkerOptions(1,1,
                pendingByteBudget:10,maxWaitingProducers:1));
            TaskCompletionSource<bool> entered=Gate(),release=Gate();
            FCRWorkHandle<int> active=worker.Submit<int>(async ct=>{
                entered.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                return 1;
            },"line");
            try
            {
                Await(entered.Task);
                FCRWorkHandle<int> queued=worker.Submit<int>(
                    ct=>Task.FromResult(2),"line",estimatedBytes:10);
                using(CancellationTokenSource cts=new CancellationTokenSource())
                {
                    Task<FCRWorkHandle<int>> waiting=worker.SubmitWhenCapacityAsync<int>(
                        ct=>Task.FromResult(3),"line",cts.Token,estimatedBytes:1);
                    Check(worker.GetSnapshot().WaitingProducers==1 &&
                        !waiting.IsCompleted,"R08等待取消用例未真正排队");
                    cts.Cancel();
                    bool cancelled=false;
                    try
                    {
                        Task winner=Task.WhenAny(waiting,Task.Delay(7000)).GetAwaiter().GetResult();
                        Check(object.ReferenceEquals(winner,waiting),"R08等待生产者取消未及时完成");
                        waiting.GetAwaiter().GetResult();
                    }
                    catch(OperationCanceledException){cancelled=true;}
                    Check(cancelled&&worker.GetSnapshot().WaitingProducers==0 &&
                        worker.GetSnapshot().PendingBytes==10,
                        "R08生产者取消未释放等待名额或篡改已排队工作预算");
                }
                Task<FCRWorkHandle<int>> stopWaiter=worker.SubmitWhenCapacityAsync<int>(
                    ct=>Task.FromResult(4),"line",estimatedBytes:1);
                Task stop=worker.StopAsync(false);
                FCRWorkHandle<int> refused=Await(stopWaiter);
                Check(!refused.Accepted &&
                    Await(refused.Completion).State==FCRWorkState.Rejected &&
                    worker.GetSnapshot().WaitingProducers==0 &&
                    !stop.IsCompleted,"R08 Stop须唤醒生产者但等待已接受工作排空");
                release.TrySetResult(true);
                Await(stop);
                Check(Await(active.Completion).Succeeded &&
                    Await(queued.Completion).Succeeded &&
                    worker.GetSnapshot().Pending==0 &&
                    worker.GetSnapshot().PendingBytes==0 &&
                    worker.GetSnapshot().WaitingProducers==0,
                    "R08 Stop(false)结束后背压/队列预算泄漏");
            }
            finally
            {
                release.TrySetResult(true);
                Await(worker.StopAsync(true));
            }
        }

        /// <summary>R08：排队与异步准入并发竞争，不丢失等待、容量或结束信号。</summary>
        private static void R08_ProducerBurstCancelAndDrain()
        {
            for (int round = 0; round < 8; round++)
            {
                FCRWorker worker = new FCRWorker(new FCRWorkerOptions(
                    2, 3, FCROverflowPolicy.DropOldestDroppable,
                    pendingByteBudget:30, maxWaitingProducers:12));
                TaskCompletionSource<bool> entered = Gate(), release = Gate();
                System.Collections.Generic.List<CancellationTokenSource> tokens =
                    new System.Collections.Generic.List<CancellationTokenSource>();
                FCRWorkHandle<int> activeA = worker.Submit<int>(async ct =>
                {
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    return 1;
                }, "key-a");
                FCRWorkHandle<int> activeB = worker.Submit<int>(async ct =>
                {
                    await release.Task.ConfigureAwait(false);
                    return 2;
                }, "key-b");
                try
                {
                    Await(entered.Task);
                    System.Collections.Generic.List<FCRWorkHandle<int>> queued =
                        new System.Collections.Generic.List<FCRWorkHandle<int>>();
                    for(int i=0;i<3;i++)
                        queued.Add(worker.Submit<int>(ct=>Task.FromResult(3),
                            "key-a",estimatedBytes:10));
                    System.Collections.Generic.List<Task<FCRWorkHandle<int>>> waiting =
                        new System.Collections.Generic.List<Task<FCRWorkHandle<int>>>();
                    for(int i=0;i<12;i++)
                    {
                        CancellationTokenSource cancel = new CancellationTokenSource();
                        tokens.Add(cancel);
                        waiting.Add(worker.SubmitWhenCapacityAsync<int>(
                            ct=>Task.FromResult(4),"key-a",cancel.Token,
                            estimatedBytes:10));
                    }
                    FCRWorkerSnapshot saturated=worker.GetSnapshot();
                    Check(saturated.Pending==3&&saturated.PendingBytes==30&&
                        saturated.WaitingProducers==12&&saturated.Active==2,
                        "R08压力下已接纳和待准入容量不正确");
                    FCRWorkHandle<int> excess=Await(worker.SubmitWhenCapacityAsync<int>(
                        ct=>Task.FromResult(5),"key-a",estimatedBytes:10));
                    Check(Await(excess.Completion).State==FCRWorkState.Rejected,
                        "R08第13名等待生产者未被明确拒绝");
                    for(int i=0;i<6;i++)tokens[i].Cancel();
                    for(int i=0;i<6;i++)
                    {
                        Task task=waiting[i];
                        Task done=Task.WhenAny(task,Task.Delay(7000)).GetAwaiter().GetResult();
                        Check(object.ReferenceEquals(task,done),"R08等待者取消风暴未及时完成");
                        bool cancelled=false;
                        try{task.GetAwaiter().GetResult();}
                        catch(OperationCanceledException){cancelled=true;}
                        Check(cancelled,"R08取消后等待者仍被当作正常业务受理");
                    }
                    Check(worker.GetSnapshot().WaitingProducers==6 &&
                        worker.GetSnapshot().PendingBytes==30,
                        "R08等待取消错误改变已接纳任务预算");
                    for(int i=0;i<3;i++)Check(queued[i].RequestCancel(),
                        "R08正在排队工作取消未生效");
                    for(int i=6;i<9;i++)
                        Check(Await(waiting[i]).Accepted,"R08释放容量时等待者未FIFO获得席位");
                    Check(worker.GetSnapshot().WaitingProducers==3 &&
                        worker.GetSnapshot().PendingBytes==30,
                        "R08等待转入已接纳状态的数量/字节预算错误");
                    for(int i=9;i<12;i++)tokens[i].Cancel();
                    for(int i=9;i<12;i++)
                    {
                        bool cancelled=false;
                        try
                        {
                            Task done=Task.WhenAny(waiting[i],Task.Delay(7000))
                                .GetAwaiter().GetResult();
                            Check(object.ReferenceEquals(done,waiting[i]),
                                "R08取消剩余生产者未及时完成");
                            waiting[i].GetAwaiter().GetResult();
                        }
                        catch(OperationCanceledException){cancelled=true;}
                        Check(cancelled,"R08取消剩余生产者后仍得到Accepted Handle");
                    }
                    release.TrySetResult(true);
                    Await(activeA.Completion);
                    Await(activeB.Completion);
                    for(int i=6;i<9;i++)
                        Check(Await(Await(waiting[i]).Completion).Succeeded,
                            "R08获得容量的生产者未按原F19 Worker正常执行");
                    FCRWorkerSnapshot final=worker.GetSnapshot();
                    Check(final.Pending==0&&final.Active==0 &&
                        final.WaitingProducers==0&&final.PendingBytes==0 &&
                        final.AdmittedProducers==3&&final.Rejected==1&&final.Dropped==0,
                        "R08生产者压力场景存在预算泄漏、误Drop或统计不一致");
                }
                finally
                {
                    release.TrySetResult(true);
                    Await(worker.StopAsync(true));
                    foreach(CancellationTokenSource source in tokens)source.Dispose();
                }
            }

            // Stop(true)只能取消未开始的排队工作，不得把非合作中的运行任务当作已停止。
            FCRWorker stopWorker=new FCRWorker(new FCRWorkerOptions(
                1,1,pendingByteBudget:2,maxWaitingProducers:3));
            TaskCompletionSource<bool> firstEntered=Gate(),finish=Gate();
            FCRWorkHandle<int> nonCooperative=stopWorker.Submit<int>(async ct=>{
                firstEntered.TrySetResult(true);
                await finish.Task.ConfigureAwait(false);
                return 9;
            },"machine");
            try
            {
                Await(firstEntered.Task);
                FCRWorkHandle<int> queued=stopWorker.Submit<int>(
                    ct=>Task.FromResult(10),"machine",estimatedBytes:2);
                Task<FCRWorkHandle<int>> waiter=stopWorker.SubmitWhenCapacityAsync<int>(
                    ct=>Task.FromResult(11),"machine",estimatedBytes:1);
                Task stopped=stopWorker.StopAsync(true);
                FCRWorkHandle<int> refused=Await(waiter);
                Check(Await(refused.Completion).State==FCRWorkState.Rejected &&
                    Await(queued.Completion).State==FCRWorkState.Cancelled &&
                    !stopped.IsCompleted&&stopWorker.GetSnapshot().Active==1 &&
                    stopWorker.GetSnapshot().WaitingProducers==0,
                    "R08 Stop(true)错误释放在途工作或未终结等待者");
                finish.TrySetResult(true);
                Await(stopped);
                Check(Await(nonCooperative.Completion).Succeeded &&
                    stopWorker.GetSnapshot().PendingBytes==0,
                    "R08 Stop(true)应等非合作工作真实结算后才完成");
            }
            finally{finish.TrySetResult(true);Await(stopWorker.StopAsync(true));}
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
