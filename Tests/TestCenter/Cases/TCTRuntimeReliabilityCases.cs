using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;
using CxVisual.TestCenter.Fixtures;

namespace CxVisual.TestCenter.Cases
{
    // Industrial runtime contract tests. They observe real MRK state and never drive hardware.
    public static class TCTRuntimeReliabilityCases
    {
        private static MRKExecutionPlan Plan(string id)
        {
            return new MRKExecutionPlan(id, new[]
            {
                new MRKExecutionStep("step", new string[0],
                    (execution, cancellation) => Task.FromResult((object)7))
            });
        }

        [TCTTest("TCT.MRK.006", "Journal持久化、Unknown外部动作及恢复判定", TCTTestAspect.Mechanism,
            "Runtime / Journal", ObjectId = "MRKFileExecutionJournal")]
        public static void JournalRecovery(TCTTestContext context)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                string file = sandbox.PathFor("journal.bin");
                Guid flow = Guid.NewGuid();
                using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(file))
                {
                    journal.Append(new MRKJournalRecord(MRKJournalKind.FlowStarted,
                        flow, "Plan-A", 3, "", "Running"));
                    journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalUnknown,
                        flow, "Plan-A", 3, "Axis", "Unknown", "operation-1"));
                    journal.Append(new MRKJournalRecord(MRKJournalKind.FlowFinished,
                        flow, "Plan-A", 3, "", "Completed"));
                    context.AssertEqual(MRKRecoveryDisposition.RequiresReconciliation,
                        MRKJournalRecovery.Evaluate(flow, journal.ReadAll()).Disposition,
                        "有Unknown即使Flow结束也不能自动重播");
                    journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalConfirmed,
                        flow, "Plan-A", 3, "Axis", "Confirmed", "operation-1"));
                    context.AssertEqual(MRKRecoveryDisposition.Closed,
                        MRKJournalRecovery.Evaluate(flow, journal.ReadAll()).Disposition,
                        "Unknown确认后才能Closed");
                }
                using (MRKFileExecutionJournal reopened = new MRKFileExecutionJournal(file))
                {
                    var records = reopened.ReadAll();
                    context.AssertEqual(4, records.Count, "关闭后完整重读4条");
                    context.AssertSequenceEqual(new long[] {1,2,3,4},
                        records.Select(x => x.Sequence), "Journal连续序号");
                    context.AssertEqual("operation-1", records[3].CorrelationId,
                        "关联ID与持久记录一致");
                    context.Observe("Runtime", "MRK真实Journal",
                        string.Join(" | ", records.Select(r =>
                            "#" + r.Sequence + " " + r.Kind + " step=" + r.StepId)));
                }
            }
        }

        [TCTTest("TCT.MRK.007", "断电截断尾帧只能安全裁剪", TCTTestAspect.Mechanism,
            "Runtime / Journal", ObjectId = "MRKFileExecutionJournal")]
        public static void JournalTruncatedTail(TCTTestContext context)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                string file = sandbox.PathFor("tail.bin");
                Guid flow = Guid.NewGuid();
                using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(file))
                    journal.Append(new MRKJournalRecord(MRKJournalKind.FlowStarted,
                        flow, "Plan-A", 1, "", "Running"));
                long validLength = new FileInfo(file).Length;
                using (FileStream tail = new FileStream(file, FileMode.Append, FileAccess.Write))
                {
                    tail.WriteByte(0x12);
                    tail.WriteByte(0x34);
                }
                context.AssertTrue(new FileInfo(file).Length > validLength,
                    "构造截断尾部", true, true);
                using (MRKFileExecutionJournal recovered = new MRKFileExecutionJournal(file))
                {
                    context.AssertEqual(1, recovered.ReadAll().Count, "前面有效帧不能丢失");
                    context.AssertEqual(validLength, new FileInfo(file).Length, "仅修剪不完整尾部");
                    recovered.Append(new MRKJournalRecord(MRKJournalKind.FlowFinished,
                        flow, "Plan-A", 1, "", "Completed"));
                    context.AssertSequenceEqual(new long[] { 1, 2 },
                        recovered.ReadAll().Select(x => x.Sequence), "恢复后序列可继续");
                }
            }
        }

        [TCTTest("TCT.MRK.008", "完整Journal帧损坏绝不能被忽略", TCTTestAspect.Mechanism,
            "Runtime / Journal", ObjectId = "MRKFileExecutionJournal")]
        public static void JournalCorruptionDetected(TCTTestContext context)
        {
            using (TCTTestSandbox sandbox = new TCTTestSandbox())
            {
                string file = sandbox.PathFor("corrupt.bin");
                using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(file))
                    journal.Append(new MRKJournalRecord(MRKJournalKind.FlowStarted,
                        Guid.NewGuid(), "Plan-A", 1, "", "Running"));
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite))
                {
                    stream.Position = 7; // inside payload; keep length and checksum untouched
                    int original = stream.ReadByte();
                    if (original < 0) throw new InvalidDataException("unexpected empty journal");
                    stream.Position = 7;
                    stream.WriteByte((byte)(original ^ 0x01));
                }
                context.AssertThrows<InvalidDataException>(() =>
                {
                    using (MRKFileExecutionJournal ignored = new MRKFileExecutionJournal(file))
                    { ignored.ReadAll(); }
                }, "校验损坏必须拒绝打开，不能跳过");
            }
        }

        [TCTTest("TCT.MRK.009", "软件资源租约独占、取消及幂等归还", TCTTestAspect.Mechanism,
            "Runtime / ResourceLease", ObjectId = "MRKResourceLeaseCoordinator")]
        public static void ResourceLeaseAndCancellation(TCTTestContext context)
        {
            MRKResourceLeaseCoordinator coordinator = new MRKResourceLeaseCoordinator();
            MRKResourceLease first = coordinator.TryAcquire("GPU-0", "Flow-A");
            context.AssertTrue(first != null, "首次获取租约", true, first != null);
            try
            {
                context.AssertEqual("Flow-A", coordinator.CurrentOwner("GPU-0"), "持有者可见");
                context.AssertTrue(coordinator.TryAcquire("GPU-0", "Flow-B") == null,
                    "不能被另一Flow抢占", false, false);
                using (CancellationTokenSource cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    context.AssertThrows<OperationCanceledException>(() =>
                        coordinator.AcquireAsync("GPU-0", "Flow-B", cancelled.Token)
                            .GetAwaiter().GetResult(), "等待取消不能取得租约");
                }
                context.AssertEqual("Flow-A", coordinator.CurrentOwner("GPU-0"), "取消不改变持有者");
            }
            finally { first.Dispose(); first.Dispose(); }
            context.AssertEqual((string)null, coordinator.CurrentOwner("GPU-0"), "归还后无人持有");
            using (MRKResourceLease next = coordinator.TryAcquire("GPU-0", "Flow-C"))
                context.AssertTrue(next != null, "可再次安全获取", true, next != null);
        }

        [TCTTest("TCT.MRK.010", "外部命令观察超时不等于操作失败", TCTTestAspect.Mechanism,
            "Runtime / ExternalOperation", ObjectId = "MRKExternalOperationTracker")]
        public static void ExternalUnknownThenConfirmed(TCTTestContext context)
        {
            MRKExternalOperationTracker tracker = new MRKExternalOperationTracker();
            Guid id = Guid.NewGuid();
            bool created;
            MRKExternalOperation original = tracker.Register(id, "XRay-Trigger-42", out created);
            context.AssertTrue(created, "新Operation首次注册", true, created);
            MRKExternalOperation again = tracker.Register(id, "XRay-Trigger-42", out created);
            context.AssertTrue(!created && object.ReferenceEquals(original, again),
                "同ID幂等注册仍是同一票据", true, true);
            context.AssertThrows<InvalidOperationException>(() =>
                tracker.Register(id, "different-correlation", out created), "同ID不同Correlation必须报错");
            MRKExternalOperationObservation timeout = original.ObserveAsync(TimeSpan.Zero)
                .GetAwaiter().GetResult();
            context.AssertTrue(timeout.TimedOut, "观察零等待触发超时", true, timeout.TimedOut);
            context.AssertEqual(MRKExternalOperationState.Unknown, original.State,
                "未知不能直接标为Failed");
            context.AssertTrue(original.Confirm(), "后到的明确确认有效", true, true);
            context.AssertEqual(MRKExternalOperationState.Confirmed,
                original.Completion.GetAwaiter().GetResult(), "完成事实可回读");
            context.AssertTrue(!original.Fail(), "Confirmed以后不能翻转Failed", false, false);
            context.Observe("Runtime", "外部操作真实状态",
                "Operation=" + id + " correlation=XRay-Trigger-42; Pending -> Unknown -> Confirmed");
        }

        [TCTTest("TCT.MRK.011", "请求应答必须先注册，迟到回复不可错配", TCTTestAspect.Mechanism,
            "Runtime / Correlation", ObjectId = "MRKCorrelationWaiter")]
        public static void CorrelationMatchAndLateReply(TCTTestContext context)
        {
            MRKCorrelationWaiter queue = new MRKCorrelationWaiter();
            Guid id = Guid.NewGuid();
            using (MRKCorrelationTicket ticket = queue.Register(id))
            {
                context.AssertEqual(1, queue.PendingCount, "注册即进入待应答表");
                context.AssertThrows<InvalidOperationException>(() =>
                    queue.Register(id), "重复请求标识拒绝");
                context.AssertTrue(queue.Receive(id, "ACK"), "快速响应真实匹配", true, true);
                MRKCorrelationResult result = ticket.WaitAsync(TimeSpan.FromSeconds(1))
                    .GetAwaiter().GetResult();
                context.AssertEqual(MRKCorrelationState.Received, result.State, "已收到");
                context.AssertEqual("ACK", (string)result.Payload, "响应Payload一致");
                context.AssertEqual(0, queue.PendingCount, "匹配即从队列清除");
                context.AssertTrue(!queue.Receive(id, "stale-ACK"), "旧应答不能再次写入", false, false);
            }
            using (MRKCorrelationTicket expired = queue.Register(Guid.NewGuid()))
            {
                Guid expiredId = expired.RequestId;
                MRKCorrelationResult result = expired.WaitAsync(TimeSpan.Zero)
                    .GetAwaiter().GetResult();
                context.AssertEqual(MRKCorrelationState.TimedOut, result.State, "无应答超时");
                context.AssertTrue(!queue.Receive(expiredId, "late"),
                    "超时后迟到应答不可混入下一轮", false, false);
                context.AssertEqual(0, queue.PendingCount, "票据不能泄漏");
            }
        }

        [TCTTest("TCT.MRK.012", "运行Plan热切换必须等待全部旧租约释放", TCTTestAspect.Mechanism,
            "Runtime / PlanSlot", ObjectId = "MRKExecutionPlanSlot")]
        public static void PlanActivationSafety(TCTTestContext context)
        {
            MRKExecutionPlanSlot slot = new MRKExecutionPlanSlot(Plan("old"), 1);
            MRKExecutionPlanLease lease = slot.Capture();
            try
            {
                slot.StageValidated(Plan("new"), 2);
                context.AssertEqual(1L, slot.ActiveRevision, "旧Flow仍使用旧版本");
                context.AssertEqual((long?)2, slot.PendingRevision, "新计划保持待生效");
                context.AssertEqual("old", lease.Plan.Id, "已开始流程的Plan引用不变");
                context.AssertTrue(!slot.TryActivatePending(), "持有旧租约时不得热切换", false, false);
                context.AssertThrows<InvalidOperationException>(() =>
                    slot.StageValidated(Plan("stale"), 2), "不能用旧Revision覆盖候选");
            }
            finally { lease.Dispose(); lease.Dispose(); }
            context.AssertEqual(0, slot.RunningFlows, "Lease幂等关闭");
            context.AssertTrue(slot.TryActivatePending(), "无运行实例后可切换", true, true);
            context.AssertEqual(2L, slot.ActiveRevision, "激活Version");
            context.AssertEqual((long?)null, slot.PendingRevision, "待切换版本已清除");
            using (MRKExecutionPlanLease fresh = slot.Capture())
                context.AssertEqual("new", fresh.Plan.Id, "新Flow使用新计划");
            context.Observe("Runtime", "软件Plan原子切换",
                "Revision 1 -> 2; old flow remains on revision 1 until lease exit");
        }

        [TCTTest("TCT.MRK.013", "执行组超额必须拒绝而不是隐式无限排队", TCTTestAspect.Mechanism,
            "Runtime / ExecutionGroup", ObjectId = "MRKExecutionGroups")]
        public static void GroupQuota(TCTTestContext context)
        {
            MRKExecutionGroups groups = new MRKExecutionGroups(new[] {
                new MRKExecutionGroupDefinition("GPU", 1, 0)
            });
            using (MRKExecutionGroupLease first = groups.AcquireAsync("GPU")
                       .GetAwaiter().GetResult())
            {
                MRKExecutionGroupSnapshot snapshot = groups.Snapshot().Single();
                context.AssertEqual(1, snapshot.Active, "一个运行席位");
                context.AssertEqual(0, snapshot.Waiting, "零等待配额");
                context.AssertThrows<MRKExecutionGroupRejectedException>(() =>
                    groups.AcquireAsync("GPU").GetAwaiter().GetResult(), "多余执行必须拒绝");
                context.AssertEqual(1L, groups.Snapshot().Single().Rejected, "拒绝数真实可见");
            }
            using (MRKExecutionGroupLease second = groups.AcquireAsync("GPU")
                       .GetAwaiter().GetResult())
                context.AssertEqual(1, groups.Snapshot().Single().Active, "席位可重新获得");
            context.AssertEqual(0, groups.Snapshot().Single().Active, "归还后没有泄漏");
        }

        [TCTTest("TCT.MRK.014", "缺失运行记录不得推断流程已经完成", TCTTestAspect.Mechanism,
            "Runtime / Journal", ObjectId = "MRKJournalRecovery")]
        public static void JournalWithoutTerminal(TCTTestContext context)
        {
            Guid flow = Guid.NewGuid(), unrelated = Guid.NewGuid();
            MRKJournalRecord[] records = {
                new MRKJournalRecord(MRKJournalKind.FlowStarted, flow, "Plan", 1, "", "Running"),
                new MRKJournalRecord(MRKJournalKind.FlowFinished, unrelated, "Other", 1, "", "Completed")
            };
            context.AssertEqual(MRKRecoveryDisposition.RequiresReconciliation,
                MRKJournalRecovery.Evaluate(flow, records).Disposition, "无终态仍需恢复审查");
            context.AssertEqual(MRKRecoveryDisposition.Closed,
                MRKJournalRecovery.Evaluate(unrelated, records).Disposition, "独立流程不交叉");
            context.AssertEqual(MRKRecoveryDisposition.NoRecord,
                MRKJournalRecovery.Evaluate(Guid.NewGuid(), records).Disposition, "无记录不能假绿");
        }
    }
}