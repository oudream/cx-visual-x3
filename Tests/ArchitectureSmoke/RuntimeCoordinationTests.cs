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
            T20_UnrelatedOrLateReplyCannotCompleteOtherRequest();
        }
        private static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("Runtime协作测试: " + message); }
        private static T Await<T>(Task<T> task)
        {
            if (!task.Wait(TimeSpan.FromSeconds(8)))
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
    }
}
