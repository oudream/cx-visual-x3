using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class RuntimeCoordinationTests
    {
        internal static void Run()
        {
            T11_ResourceLeaseAndCancellation();
            T12_UnknownOperationLateConfirmationAndIdempotency();
            T17_CorrelatedReplyAndTimeout();
            T18_ImmutablePlanAcrossRuns();
            T18_StagedPlanSwitchDoesNotMutateRunningFlow();
            T20_UnrelatedOrLateReplyCannotCompleteOtherRequest();
            T14_FirstSuccessRequestsCancelAndTracksRemaining();
            AnyReturnsFailedWinnerWithoutKillingOtherBranch();
            T19_UnknownPhysicalOperationDoesNotReleaseSoftwareLease();
            R07_OldCorrelationTicketCannotCancelNewRequest();
            R07_AllJoinRetainsFailedCancelledAndSettlesResources();
            R07_FirstSuccessfulAllFailedIsNotSuccess();
        }
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("Runtime协作测试: " + message); }
        private static T Await<T>(Task<T> task)
        {
            Task winner = Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(8))).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner, task))
                throw new TimeoutException("Runtime协作测试等待超时");
            return task.GetAwaiter().GetResult();
        }
        private static void Await(Task task)
        {
            if (!task.Wait(TimeSpan.FromSeconds(8))) throw new TimeoutException("协作任务未结束");
            task.GetAwaiter().GetResult();
        }
        private static void T11_ResourceLeaseAndCancellation()
        {
            MRKResourceLeaseCoordinator coordinator = new MRKResourceLeaseCoordinator();
            MRKResourceLease first = coordinator.TryAcquire("PLC1", "RuntimeA");
            Check(first != null && coordinator.CurrentOwner("PLC1") == "RuntimeA",
                "租约持有者错误");
            Check(coordinator.TryAcquire("PLC1", "Studio") == null, "出现双重设备所有权");
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Task<MRKResourceLease> pending = coordinator.AcquireAsync("PLC1", "Studio", cts.Token);
                Check(!pending.IsCompleted, "第二持有人未等待");
                cts.Cancel();
                bool cancelled = false;
                try { Await(pending); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled, "申请租约取消不生效");
            }
            first.Dispose();
            first.Dispose();
            using (MRKResourceLease next = Await(coordinator.AcquireAsync("PLC1", "RuntimeB")))
            {
                Check(coordinator.CurrentOwner("PLC1") == "RuntimeB", "释放后不能重新申请");
                using (MRKResourceLease independent = coordinator.TryAcquire("PLC2", "Other"))
                    Check(independent != null, "不同资源不能并发租用");
            }
            Check(coordinator.CurrentOwner("PLC1") == null, "租约释放后Owner仍存在");
        }
        private static void T12_UnknownOperationLateConfirmationAndIdempotency()
        {
            MRKExternalOperationTracker tracker = new MRKExternalOperationTracker();
            Guid id = Guid.NewGuid();
            bool created;
            MRKExternalOperation operation = tracker.Register(id, "product-01", out created);
            Check(created, "首次操作注册失败");
            MRKExternalOperation repeated = tracker.Register(id, "product-01", out created);
            Check(!created && object.ReferenceEquals(operation, repeated), "重复操作没有复用身份");
            MRKExternalOperationObservation seen =
                Await(operation.ObserveAsync(TimeSpan.FromMilliseconds(20)));
            Check(seen.TimedOut && seen.State == MRKExternalOperationState.Unknown,
                "本地超时没有形成结果未知状态");
            Check(!operation.Completion.IsCompleted, "不能将本地超时冒充设备停止");
            Check(operation.Confirm() && !operation.Fail(), "迟到确认或终态幂等错误");
            Check(Await(operation.Completion) == MRKExternalOperationState.Confirmed,
                "迟到设备确认丢失");
            bool conflict = false;
            try { tracker.Register(id, "another-request", out created); }
            catch (InvalidOperationException) { conflict = true; }
            Check(conflict, "同一操作ID不应允许改变关联ID");
        }
        private static void T17_CorrelatedReplyAndTimeout()
        {
            MRKCorrelationWaiter waiter = new MRKCorrelationWaiter();
            Guid id = Guid.NewGuid();
            using (MRKCorrelationTicket ticket = waiter.Register(id))
            {
                // 先登记、再模拟发送，快速返回的数据也能被匹配。
                Check(waiter.Receive(id, "PLC-ACK"), "已经注册的应答没有匹配");
                MRKCorrelationResult reply = Await(ticket.WaitAsync(TimeSpan.FromSeconds(1)));
                Check(reply.State == MRKCorrelationState.Received &&
                    (string)reply.Payload == "PLC-ACK", "快速应答丢失");
            }
            Guid slow = Guid.NewGuid();
            using (MRKCorrelationTicket ticket = waiter.Register(slow))
            {
                MRKCorrelationResult reply = Await(ticket.WaitAsync(TimeSpan.FromMilliseconds(20)));
                Check(reply.State == MRKCorrelationState.TimedOut, "应答超时未识别");
                Check(!waiter.Receive(slow, "LATE-ACK"), "超时应答污染状态");
            }
            Check(waiter.PendingCount == 0, "已完成/超时应答没有清理登记");
        }
        private static void T18_ImmutablePlanAcrossRuns()
        {
            MRKExecutionEngine engine = new MRKExecutionEngine(
                new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkerOptions(2, 4));
            MRKExecutionPlan oldPlan = new MRKExecutionPlan("vision-revision-1", new[]
            {
                new MRKExecutionStep("a", new string[0],
                    (c, ct) => Task.FromResult((object)"旧计划"))
            });
            MRKExecutionPlan newPlan = new MRKExecutionPlan("vision-revision-2", new[]
            {
                new MRKExecutionStep("a", new string[0],
                    (c, ct) => Task.FromResult((object)"新计划"))
            });
            MRKFlowResult a = Await(engine.StartFlow(oldPlan).Completion);
            MRKFlowResult b = Await(engine.StartFlow(newPlan).Completion);
            Check(a.State == MRKFlowState.Completed && b.State == MRKFlowState.Completed &&
                (string)a.Results["a"] == "旧计划" && (string)b.Results["a"] == "新计划",
                "运行实例未持有独立的编译计划");
            Await(engine.StopAsync());
        }
        private static void T20_UnrelatedOrLateReplyCannotCompleteOtherRequest()
        {
            MRKCorrelationWaiter waiter = new MRKCorrelationWaiter();
            Guid a = Guid.NewGuid(), b = Guid.NewGuid();
            using (MRKCorrelationTicket first = waiter.Register(a))
            using (MRKCorrelationTicket second = waiter.Register(b))
            {
                bool duplicate = false;
                try { waiter.Register(a); } catch (InvalidOperationException) { duplicate = true; }
                Check(duplicate, "相同RequestId被多次订阅");
                Check(!waiter.Receive(Guid.NewGuid(), "foreign"), "未关联消息不应匹配");
                Check(waiter.Receive(b, "B"), "B应答未完成");
                Check(Await(second.WaitAsync(TimeSpan.FromSeconds(1))).Payload.Equals("B"),
                    "B应答不一致");
                Check(!first.Completion.IsCompleted, "B响应错误完成A请求");
                Check(waiter.Receive(a, "A"), "A应答未完成");
                Check(Await(first.WaitAsync(TimeSpan.FromSeconds(1))).Payload.Equals("A"),
                    "A应答不一致");
            }
        }
        private static void T14_FirstSuccessRequestsCancelAndTracksRemaining()
        {
            CxVisual.Foundation.Infrastructure.Concurrency.FCRWorker worker =
                new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorker(
                    new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkerOptions(2, 3));
            TaskCompletionSource<bool> entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkHandle<int> never =
                worker.Submit<int>(async ct =>
                {
                    entered.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                    return 1;
                }, "ai");
            Await(entered.Task);
            CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkHandle<int> fast =
                worker.Submit<int>(ct => Task.FromResult(99), "cpu");
            MRKJoinResult<int> joined = Await(MRKWorkJoin.FirstSuccessfulAsync(new[] { never, fast }));
            Check(joined.Succeeded && joined.Winner.Id == fast.Id &&
                joined.WinningResult.Value == 99, "首个成功结果归并错误");
            CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkResult<int>[] all =
                Await(joined.AllSettled);
            Check(all.Length == 2 &&
                all[0].State == CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkState.Cancelled &&
                all[1].State == CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkState.Completed,
                "余下分支未协作取消及安全结束");
            Await(worker.StopAsync());
        }

        private static void T18_StagedPlanSwitchDoesNotMutateRunningFlow()
        {
            TaskCompletionSource<bool> entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> gate =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            MRKExecutionPlan oldPlan = new MRKExecutionPlan("old", new[]
            {
                new MRKExecutionStep("value", new string[0], async (c, ct) =>
                {
                    entered.TrySetResult(true);
                    await gate.Task.ConfigureAwait(false);
                    return (object)10;
                })
            });
            MRKExecutionPlan newPlan = new MRKExecutionPlan("new", new[]
            {
                new MRKExecutionStep("value", new string[0],
                    (c, ct) => Task.FromResult((object)20))
            });
            MRKExecutionPlanSlot slot = new MRKExecutionPlanSlot(oldPlan, 1);
            MRKExecutionEngine engine = new MRKExecutionEngine(
                new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkerOptions(2, 4));
            MRKFlowHandle running = engine.StartFromSlot(slot);
            Await(entered.Task);
            slot.StageValidated(newPlan, 2);
            Check(!slot.TryActivatePending() && slot.ActiveRevision == 1 &&
                slot.PendingRevision == 2 && slot.RunningFlows == 1,
                "运行中的旧计划不允许部分切换");
            gate.TrySetResult(true);
            MRKFlowResult prior = Await(running.Completion);
            Check((int)prior.Results["value"] == 10 && prior.State == MRKFlowState.Completed,
                "运行中任务被新计划污染");
            Check(SpinWait.SpinUntil(() => slot.RunningFlows == 0, 3000),
                "旧计划运行引用未释放");
            Check(slot.TryActivatePending() && slot.ActiveRevision == 2 &&
                !slot.PendingRevision.HasValue, "空闲时无法切换已验证计划");
            MRKFlowResult next = Await(engine.StartFromSlot(slot).Completion);
            Check((int)next.Results["value"] == 20, "新执行没有使用新计划");
            Await(engine.StopAsync());
        }

        private static void T19_UnknownPhysicalOperationDoesNotReleaseSoftwareLease()
        {
            MRKResourceLeaseCoordinator leases = new MRKResourceLeaseCoordinator();
            MRKExternalOperationTracker tracker = new MRKExternalOperationTracker();
            MRKResourceLease exposure = leases.TryAcquire("XRay.Source", "Runtime.Host");
            Check(exposure != null, "exposure lease not acquired");
            bool created;
            MRKExternalOperation request = tracker.Register(
                Guid.NewGuid(), "scan-001", out created);
            Check(created, "scan operation duplicate");
            MRKExternalOperationObservation observation =
                Await(request.ObserveAsync(TimeSpan.FromMilliseconds(20)));
            Check(observation.TimedOut && request.State == MRKExternalOperationState.Unknown,
                "no unknown state for unconfirmed physical action");
            Check(leases.TryAcquire("XRay.Source", "Studio") == null,
                "local timeout released physical resource ownership unsafely");
            Check(request.Confirm(), "provider cannot explicitly confirm");
            Check(leases.TryAcquire("XRay.Source", "Studio") == null,
                "operation confirmation alone should not auto-reassign lease");
            exposure.Dispose();
            using (MRKResourceLease next = leases.TryAcquire("XRay.Source", "RuntimeNext"))
                Check(next != null, "resource lease not released after explicit action");
        }

        private static void AnyReturnsFailedWinnerWithoutKillingOtherBranch()
        {
            CxVisual.Foundation.Infrastructure.Concurrency.FCRWorker worker =
                new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorker(
                    new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkerOptions(2, 2));
            TaskCompletionSource<bool> entered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> release =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkHandle<int> pending =
                worker.Submit<int>(async ct =>
                {
                    entered.TrySetResult(true);
                    await release.Task.ConfigureAwait(false);
                    return 10;
                });
            Await(entered.Task);
            CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkHandle<int> failed =
                worker.Submit<int>(ct =>
                { throw new InvalidOperationException("本分支失败"); });
            MRKJoinResult<int> first = Await(MRKWorkJoin.FirstCompletedAsync(
                new[] { pending, failed }));
            Check(first.Winner.Id == failed.Id && !first.Succeeded &&
                !pending.Completion.IsCompleted, "Any把失败改成成功或提前取消其他分支");
            release.TrySetResult(true);
            CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkResult<int>[] outcomes =
                Await(first.AllSettled);
            Check(outcomes.Length == 2 && outcomes[0].Succeeded &&
                outcomes[1].State == CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkState.Failed,
                "Any的余下结果没有完整结算");
            Await(worker.StopAsync());
        }

        private static void R07_OldCorrelationTicketCannotCancelNewRequest()
        {
            MRKCorrelationWaiter waiter = new MRKCorrelationWaiter();
            Guid id = Guid.NewGuid();
            MRKCorrelationTicket old = waiter.Register(id);
            Check(waiter.Receive(id, "old"), "旧票据没有先结算");
            MRKCorrelationTicket current = waiter.Register(id);
            try
            {
                old.Dispose();
                Check(waiter.PendingCount == 1 && !current.Completion.IsCompleted,
                    "旧票据Dispose错误取消了复用ID的新请求");
                Check(waiter.Receive(id, "new"), "新请求的应答被旧票据吞掉");
                MRKCorrelationResult result = Await(current.WaitAsync(TimeSpan.FromSeconds(1)));
                Check(result.State == MRKCorrelationState.Received &&
                    (string)result.Payload == "new", "新请求的应答关联不正确");
            }
            finally { old.Dispose(); current.Dispose(); }
            Check(waiter.PendingCount == 0, "旧票据与新票据没有全部释放");
            // 一般外部请求仍必须使用每次唯一ID，避免网络层无法判别同ID的迟到应答。
        }

        private static void R07_AllJoinRetainsFailedCancelledAndSettlesResources()
        {
            var worker = new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorker(
                new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkerOptions(3, 4));
            TaskCompletionSource<bool> entered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var passed = worker.Submit<int>(ct => Task.FromResult(12), "r07-ok");
            var failed = worker.Submit<int>(ct =>
            { throw new InvalidOperationException("预期分支失败"); }, "r07-fail");
            var pending = worker.Submit<int>(async ct =>
            {
                entered.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return 30;
            }, "r07-pending");
            Await(entered.Task);
            Task<CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkResult<int>[]> settling =
                MRKWorkJoin.AllAsync(new[] { passed, failed, pending });
            Check(!settling.IsCompleted, "All尚有未完成分支却提前释放等待");
            Check(pending.RequestCancel(), "执行分支无法接收显式取消请求");
            var results = Await(settling);
            Check(results.Length == 3 && results[0].Succeeded &&
                results[0].Value == 12 &&
                results[1].State == CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkState.Failed &&
                results[2].State == CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkState.Cancelled,
                "All把失败/取消误判为成功或没有按原分支顺序结算");
            Await(worker.StopAsync());
            Check(worker.GetSnapshot().Active == 0,
                "All结算后仍有Worker正在占用资源");
            bool rejected = false;
            try { MRKWorkJoin.AllAsync(new[] { passed, passed }); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "All没有拒绝重复执行分支");
        }

        private static void R07_FirstSuccessfulAllFailedIsNotSuccess()
        {
            var worker = new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorker(
                new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkerOptions(2, 3));
            var bad = worker.Submit<int>(ct =>
            { throw new InvalidOperationException("预期错误"); }, "r07-bad");
            var canceled = worker.Submit<int>(ct =>
            { throw new OperationCanceledException(ct); }, "r07-cancel");
            var joined = Await(MRKWorkJoin.FirstSuccessfulAsync(new[] { bad, canceled }));
            Check(!joined.Succeeded && joined.Winner == null && joined.WinningResult == null,
                "FirstSuccess全失败时不应伪造Winner");
            var outcomes = Await(joined.AllSettled);
            Check(outcomes.Length == 2 && !outcomes[0].Succeeded && !outcomes[1].Succeeded,
                "FirstSuccess错误丢失未成功分支的真实结算");
            Await(worker.StopAsync());
        }

    }
}
