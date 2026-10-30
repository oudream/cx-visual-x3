using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CxVisual.Mechanism.Runtime.Execution;
namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionJournalTests
    {
        internal static void Run()
        {
            ReopenAndConservativeRecovery();
            OperationIdentityNeverCrossConfirms();
            LegacyExternalFactsRequireReconciliation();
            TruncatedTailIsRecoverableButCorruptionIsNot();
            RuntimeRecordsRealFlowTransitions();
            JournalAdmissionFailureIsFailClosed();
            MidFlowJournalFailureIsTypedFailure();
            RepeatedFlowIdentityAndUnboundTimeoutRequireReconciliation();
            AllFlowRecoveryInventoryFailsClosed();
            TerminalSequenceMustBeCausalAndOrdered();
            BulkRecoveryInventoryMaintainsAllIdentities();
        }
        private static void Check(bool value, string msg)
        { if (!value) throw new InvalidOperationException("运行Journal：" + msg); }
        private static string PathForTest()
        { return Path.Combine(Path.GetTempPath(), "cx-run-" + Guid.NewGuid().ToString("N") + ".bin"); }
        /// <summary>
        /// B6: exercise the actual standalone Host executable across process boundaries.
        /// The Journal is written with the production codec, never by corrupting bytes.
        /// Unsettled device work must not be replayed or implicitly acknowledged.
        /// </summary>
        internal static void RunHostProcessFailClosed()
        {
            string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "CxVisual.Runtime.Host.exe");
            Check(File.Exists(executable), "B6真实Host可执行文件缺失");
            for (int scenario = 0; scenario != 2; scenario++)
            {
                string root = Path.Combine(Path.GetTempPath(),
                    "cx-host-b6-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                string journalPath = Path.Combine(root, "runtime.journal");
                try
                {
                    Guid flowId = Guid.NewGuid();
                    using (var journal = new MRKFileExecutionJournal(journalPath))
                    {
                        journal.Append(new MRKJournalRecord(MRKJournalKind.FlowStarted,
                            flowId, "b6.unsettled", 2, "", "Running"));
                        if (scenario == 1)
                            journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalUnknown,
                                flowId, "b6.unsettled", 2, "device.write", "Unknown",
                                operationId: Guid.NewGuid()));
                    }
                    long originalLength = new FileInfo(journalPath).Length;
                    string recoveryText;
                    int recoveryCode = HostProcess(executable, "runtime-recover",
                        journalPath, out recoveryText);
                    Check(recoveryCode == 3 &&
                        recoveryText.Contains("Recovery blocked") &&
                        recoveryText.Contains(flowId.ToString()),
                        "B6 Host未拒绝未结案/外部状态未知的Journal恢复");
                    string runText;
                    int runCode = HostProcess(executable, "runtime-run",
                        journalPath, out runText);
                    Check(runCode == 1 && runText.Contains("宿主启动隔离"),
                        "B6 未结案Journal被独立Host错误接管");
                    Check(new FileInfo(journalPath).Length == originalLength &&
                        !Directory.Exists(Path.Combine(root, "F19-Audit")),
                        "B6 Host拒绝接管时仍改写运行记录或创建审计会话");
                    using (var verify = new MRKFileExecutionJournal(journalPath))
                    {
                        IList<MRKJournalRecord> recorded = verify.ReadAll();
                        Check(recorded.Count == (scenario == 0 ? 1 : 2) &&
                            MRKJournalRecovery.Evaluate(flowId, recorded).Disposition ==
                                MRKRecoveryDisposition.RequiresReconciliation,
                            "B6 Host拒绝后Journal事实或人工核对状态丢失");
                    }
                }
                finally { Directory.Delete(root, true); }
            }
        }

        private static int HostProcess(string executable, string command,
            string journalPath, out string output)
        {
            var start = new System.Diagnostics.ProcessStartInfo(executable,
                command + " \"" + journalPath + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(executable)
            };
            using (var process = System.Diagnostics.Process.Start(start))
            {
                if (process == null) throw new InvalidOperationException("B6 Host无法启动");
                if (!process.WaitForExit(15000))
                {
                    try { process.Kill(); } catch (InvalidOperationException) { }
                    throw new TimeoutException("B6 Host进程拒绝/恢复测试超时");
                }
                output = process.StandardOutput.ReadToEnd() +
                    process.StandardError.ReadToEnd();
                return process.ExitCode;
            }
        }

        private static void RepeatedFlowIdentityAndUnboundTimeoutRequireReconciliation()
        {
            Guid id = Guid.NewGuid();
            MRKJournalRecord[] records = {
                new MRKJournalRecord(MRKJournalKind.FlowStarted,id,"v",1,"","Running"),
                new MRKJournalRecord(MRKJournalKind.FlowFinished,id,"v",1,"","Completed"),
                new MRKJournalRecord(MRKJournalKind.FlowStarted,id,"v",1,"","Running"),
                new MRKJournalRecord(MRKJournalKind.FlowFinished,id,"v",1,"","Completed")
            };
            Check(MRKJournalRecovery.Evaluate(id, records).Disposition ==
                MRKRecoveryDisposition.RequiresReconciliation, "重复FlowId被误判安全关闭");
            records = new[] {
                new MRKJournalRecord(MRKJournalKind.FlowStarted,id,"v",1,"","Running"),
                new MRKJournalRecord(MRKJournalKind.StepTransition,id,"v",1,"sdk","ResultUnknown"),
                new MRKJournalRecord(MRKJournalKind.StepTransition,id,"v",1,"sdk","Failed"),
                new MRKJournalRecord(MRKJournalKind.FlowFinished,id,"v",1,"","Failed")
            };
            Check(MRKJournalRecovery.Evaluate(id, records).Disposition ==
                MRKRecoveryDisposition.RequiresReconciliation, "未关联OperationId的超时被误认安全");
        }
        private static void AllFlowRecoveryInventoryFailsClosed()
        {
            Guid good=Guid.NewGuid(), running=Guid.NewGuid(), orphan=Guid.NewGuid();
            MRKJournalRecord[] records={
                new MRKJournalRecord(MRKJournalKind.FlowStarted,good,"plan",1,"","Running"),
                new MRKJournalRecord(MRKJournalKind.FlowFinished,good,"plan",1,"","Completed"),
                new MRKJournalRecord(MRKJournalKind.FlowStarted,running,"plan",1,"","Running"),
                new MRKJournalRecord(MRKJournalKind.FlowFinished,orphan,"plan",1,"","Completed")
            };
            IList<MRKRecoveryDecision> decisions=MRKJournalRecovery.EvaluateAll(records);
            Check(decisions.Count==3 &&
                decisions[0].FlowId==good &&
                decisions[0].Disposition==MRKRecoveryDisposition.Closed &&
                decisions[1].FlowId==running &&
                decisions[1].Disposition==MRKRecoveryDisposition.RequiresReconciliation &&
                decisions[2].FlowId==orphan &&
                decisions[2].Disposition==MRKRecoveryDisposition.RequiresReconciliation,
                "Host扫描未区分安全Flow、未终结Flow和孤儿终态");
            Check(MRKJournalRecovery.EvaluateAll(new MRKJournalRecord[0]).Count==0,
                "空日志不应伪造已运行Flow");
        }

        private static void TerminalSequenceMustBeCausalAndOrdered()
        {
            Guid id = Guid.NewGuid(), external = Guid.NewGuid();
            MRKJournalRecord start = new MRKJournalRecord(
                MRKJournalKind.FlowStarted,id,"P",8,"","Running");
            MRKJournalRecord finish = new MRKJournalRecord(
                MRKJournalKind.FlowFinished,id,"P",8,"","Completed");
            MRKJournalRecord transition = new MRKJournalRecord(
                MRKJournalKind.StepTransition,id,"P",8,"io","Running");
            Check(MRKJournalRecovery.Evaluate(id,new[]{finish,start}).Disposition ==
                MRKRecoveryDisposition.RequiresReconciliation,
                "FlowFinished早于FlowStarted被误认为安全完成");
            Check(MRKJournalRecovery.Evaluate(id,new[]{start,finish,finish}).Disposition ==
                MRKRecoveryDisposition.RequiresReconciliation,
                "重复FlowFinished未被拒绝");
            Check(MRKJournalRecovery.Evaluate(id,new[]{start,finish,transition}).Disposition ==
                MRKRecoveryDisposition.RequiresReconciliation,
                "Flow结束后仍有执行状态变化被忽略");
            Check(MRKJournalRecovery.Evaluate(id,new[]{transition,start,finish}).Disposition ==
                MRKRecoveryDisposition.RequiresReconciliation,
                "运行事实发生在FlowStarted前却判安全");
            MRKJournalRecord unknown = new MRKJournalRecord(
                MRKJournalKind.ExternalUnknown,id,"P",8,"io","Unknown",operationId:external);
            MRKJournalRecord confirmed = new MRKJournalRecord(
                MRKJournalKind.ExternalConfirmed,id,"P",8,"io","Confirmed",operationId:external);
            Check(MRKJournalRecovery.Evaluate(id,new[]{start,unknown,finish,confirmed}).Disposition ==
                MRKRecoveryDisposition.Closed,
                "FlowFinished后合法的迟到外部Operation确认被错误拒绝");
            Check(MRKJournalRecovery.Evaluate(id,new[]{start,finish,unknown,confirmed}).Disposition ==
                MRKRecoveryDisposition.RequiresReconciliation,
                "Flow结束以后又启动未知外部操作仍被判安全");
        }
        private static void BulkRecoveryInventoryMaintainsAllIdentities()
        {
            List<MRKJournalRecord> log = new List<MRKJournalRecord>();
            const int count = 512;
            for (int i = 0; i < count; i++)
            {
                Guid id = Guid.NewGuid();
                log.Add(new MRKJournalRecord(MRKJournalKind.FlowStarted,id,"batch",1,"","Running"));
                if (i % 3 == 0)
                    log.Add(new MRKJournalRecord(MRKJournalKind.FlowFinished,id,"batch",1,"","Completed"));
            }
            IList<MRKRecoveryDecision> result = MRKJournalRecovery.EvaluateAll(log);
            Check(result.Count == count, "大量Flow恢复索引丢失身份");
            for (int i = 0; i < count; i++)
                Check(result[i].Disposition == (i % 3 == 0
                    ? MRKRecoveryDisposition.Closed : MRKRecoveryDisposition.RequiresReconciliation),
                    "大量Flow扫描顺序或恢复状态错误：" + i);
        }
        private static void ReopenAndConservativeRecovery()
        {
            string path = PathForTest();
            Guid id = Guid.NewGuid();
            Guid operationId = Guid.NewGuid();
            try
            {
                using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(path))
                {
                    journal.Append(new MRKJournalRecord(MRKJournalKind.FlowStarted, id, "CT", 9, "", ""));
                    journal.Append(new MRKJournalRecord(MRKJournalKind.StepTransition, id, "CT", 9,
                        "reconstruct", "Running", "req1"));
                    Check(MRKJournalRecovery.Evaluate(id, journal.ReadAll()).Disposition ==
                        MRKRecoveryDisposition.RequiresReconciliation, "未结束流程被误判可重放");
                }
                using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(path))
                {
                    journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalUnknown, id,
                        "CT", 9, "exposure", "Unknown", null, operationId));
                    journal.Append(new MRKJournalRecord(MRKJournalKind.FlowFinished, id,
                        "CT", 9, "", "Failed"));
                    IList<MRKJournalRecord> history = journal.ReadAll();
                    Check(history.Count == 4 && history[3].Sequence == 4,
                        "重开Journal序列号没有延续");
                    Check(MRKJournalRecovery.Evaluate(id, history).Disposition ==
                        MRKRecoveryDisposition.RequiresReconciliation,
                        "未知设备操作被错误地认定为安全终态");
                    journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalConfirmed, id,
                        "CT", 9, "exposure", "Confirmed", null, operationId));
                    Check(MRKJournalRecovery.Evaluate(id, journal.ReadAll()).Disposition ==
                        MRKRecoveryDisposition.Closed, "已确认且完整的Flow未正常结案");
                }
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private static void OperationIdentityNeverCrossConfirms()
        {
            Guid flow = Guid.NewGuid(), first = Guid.NewGuid(), second = Guid.NewGuid();
            string path = PathForTest();
            try
            {
                using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(path))
                {
                    journal.Append(new MRKJournalRecord(MRKJournalKind.FlowStarted, flow, "plan", 5, "", "Running"));
                    journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalUnknown, flow, "plan", 5,
                        "write", "Unknown", "REQ", first));
                    journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalUnknown, flow, "plan", 5,
                        "write", "Unknown", "REQ", second));
                    journal.Append(new MRKJournalRecord(MRKJournalKind.FlowFinished, flow, "plan", 5, "", "Failed"));
                    journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalConfirmed, flow, "plan", 5,
                        "write", "Confirmed", "REQ", first));
                    journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalConfirmed, flow, "plan", 6,
                        "write", "Confirmed", "REQ", second));
                    journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalConfirmed, flow, "plan", 5,
                        "other-step", "Confirmed", "REQ", second));
                    Check(MRKJournalRecovery.Evaluate(flow, journal.ReadAll()).Disposition ==
                        MRKRecoveryDisposition.RequiresReconciliation,
                        "同Step多个操作、错版本或错步骤确认导致误结案");
                }
                using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(path))
                {
                    IList<MRKJournalRecord> history = journal.ReadAll();
                    Check(history[1].OperationId == first && history[2].OperationId == second,
                        "重启后OperationId未正确还原");
                    journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalConfirmed, flow, "plan", 5,
                        "write", "Confirmed", "REQ", second));
                    Check(MRKJournalRecovery.Evaluate(flow, journal.ReadAll()).Disposition ==
                        MRKRecoveryDisposition.Closed, "全部操作确认后应能安全关闭");
                    bool refused = false;
                    try { journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalUnknown,
                        flow, "plan", 5, "write", "Unknown")); }
                    catch (ArgumentException) { refused = true; }
                    Check(refused, "缺少OperationId的外部事实仍可写入");
                }
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        // 读取已有v1格式，但无OperationId的外部事实必须保守核对。
        private static void LegacyExternalFactsRequireReconciliation()
        {
            Guid flow = Guid.NewGuid();
            string path = PathForTest();
            try
            {
                MRKJournalRecord[] facts = {
                    new MRKJournalRecord(MRKJournalKind.FlowStarted, flow, "old", 3, "", "Running"),
                    new MRKJournalRecord(MRKJournalKind.ExternalUnknown, flow, "old", 3, "write", "Unknown"),
                    new MRKJournalRecord(MRKJournalKind.ExternalConfirmed, flow, "old", 3, "write", "Confirmed"),
                    new MRKJournalRecord(MRKJournalKind.FlowFinished, flow, "old", 3, "", "Failed")
                };
                using (FileStream fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                using (BinaryWriter file = new BinaryWriter(fs, Encoding.UTF8))
                {
                    for (int i = 0; i < facts.Length; i++)
                    {
                        byte[] bytes;
                        using (MemoryStream buffer = new MemoryStream())
                        using (BinaryWriter w = new BinaryWriter(buffer, Encoding.UTF8, true))
                        {
                            MRKJournalRecord r = facts[i];
                            w.Write((byte)1);
                            w.Write((long)(i + 1));
                            w.Write(r.TimestampUtc.Ticks);
                            w.Write((int)r.Kind);
                            w.Write(r.FlowId.ToByteArray());
                            w.Write(r.PlanId);
                            w.Write(r.Revision);
                            w.Write(r.StepId);
                            w.Write(r.State);
                            w.Write(r.CorrelationId);
                            w.Flush();
                            bytes = buffer.ToArray();
                        }
                        uint hash = 2166136261;
                        unchecked { foreach (byte value in bytes) hash = (hash ^ value) * 16777619; }
                        file.Write(bytes.Length);
                        file.Write(bytes);
                        file.Write(hash);
                    }
                }
                using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(path))
                {
                    IList<MRKJournalRecord> restored = journal.ReadAll();
                    Check(restored.Count == 4 && restored[1].OperationId == Guid.Empty,
                        "旧版Journal文件未成功读取");
                    Check(MRKJournalRecovery.Evaluate(flow, restored).Disposition ==
                        MRKRecoveryDisposition.RequiresReconciliation,
                        "历史同Step确认被错误视为独立Operation确认");
                }
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private static void TruncatedTailIsRecoverableButCorruptionIsNot()
        {
            string path = PathForTest();
            Guid id = Guid.NewGuid();
            try
            {
                using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(path))
                    journal.Append(new MRKJournalRecord(MRKJournalKind.FlowStarted, id, "P", 1,"",""));
                using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write))
                    stream.Write(new byte[] { 9, 1 }, 0, 2);
                using (MRKFileExecutionJournal reopened = new MRKFileExecutionJournal(path))
                {
                    Check(reopened.ReadAll().Count == 1, "断电截断尾帧没有处理");
                    reopened.Append(new MRKJournalRecord(MRKJournalKind.FlowFinished, id,"P",1,"","Completed"));
                    Check(reopened.ReadAll().Count == 2, "截断修复后不可继续写入");
                }
                using (FileStream damage = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    damage.Seek(-5, SeekOrigin.End);
                    int prior = damage.ReadByte();
                    damage.Seek(-1, SeekOrigin.Current);
                    damage.WriteByte((byte)(prior ^ 0xff));
                }
                bool corrupted = false;
                try { using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(path)) { } }
                catch (InvalidDataException) { corrupted = true; }
                Check(corrupted, "完整Journal内容损坏被静默忽略");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        private sealed class RejectingJournal : IMRKExecutionJournal
        {
            public void Append(MRKJournalRecord record) { throw new IOException("磁盘写入失败"); }
            public IList<MRKJournalRecord> ReadAll() { return new List<MRKJournalRecord>(); }
        }
        private static void JournalAdmissionFailureIsFailClosed()
        {
            int ran = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("strict-journal", new[]
            {
                new MRKExecutionStep("work",new string[0],(ctx,ct)=>
                { ran++; return System.Threading.Tasks.Task.FromResult((object)1); })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(
                new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkerOptions(1,2),
                journal:new RejectingJournal());
            bool rejected = false;
            try { engine.StartFlow(plan); }
            catch (IOException) { rejected = true; }
            Check(rejected && ran == 0 && engine.ActiveFlowCount == 0,
                "日志写入前置失败却执行了业务委托");
            Await(engine.StopAsync());
        }
        private static void RuntimeRecordsRealFlowTransitions()
        {
            string file = PathForTest();
            try
            {
                using (MRKFileExecutionJournal journal = new MRKFileExecutionJournal(file))
                {
                    MRKExecutionEngine engine = new MRKExecutionEngine(
                        new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkerOptions(1,2),
                        journal:journal);
                    MRKExecutionPlan plan = new MRKExecutionPlan("production", new[]
                    {
                        new MRKExecutionStep("step",new string[0],
                            (ctx,ct)=>System.Threading.Tasks.Task.FromResult((object)99))
                    });
                    MRKFlowHandle handle = engine.StartFlowWithMetadata(plan,
                        new MRKFlowMetadata(12,"serial","REQ-12","local"));
                    MRKFlowResult finished = Await(handle.Completion);
                    IList<MRKJournalRecord> history = journal.ReadAll();
                    Check(finished.State == MRKFlowState.Completed && history.Count >= 4 &&
                        history[0].Kind == MRKJournalKind.FlowStarted &&
                        history[history.Count-1].Kind == MRKJournalKind.FlowFinished &&
                        history[history.Count-1].Revision == 12 &&
                        history[history.Count-1].CorrelationId == "REQ-12" &&
                        MRKJournalRecovery.Evaluate(handle.FlowId,history).Disposition ==
                            MRKRecoveryDisposition.Closed,
                        "引擎运行事实没有写入Journal或身份丢失");
                    Await(engine.StopAsync());
                }
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }
        private static T Await<T>(System.Threading.Tasks.Task<T> task)
        {
            System.Threading.Tasks.Task winner = System.Threading.Tasks.Task.WhenAny(
                task, System.Threading.Tasks.Task.Delay(7000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner,task))
                throw new TimeoutException("Journal运行测试超时");
            return task.GetAwaiter().GetResult();
        }
        private static void Await(System.Threading.Tasks.Task task)
        {
            System.Threading.Tasks.Task winner = System.Threading.Tasks.Task.WhenAny(
                task, System.Threading.Tasks.Task.Delay(7000)).GetAwaiter().GetResult();
            if (!object.ReferenceEquals(winner,task))
                throw new TimeoutException("Journal运行测试超时");
            task.GetAwaiter().GetResult();
        }

        private sealed class MidFlowFailingJournal : IMRKExecutionJournal
        {
            private int _entries;
            public void Append(MRKJournalRecord record)
            {
                if (System.Threading.Interlocked.Increment(ref _entries) > 1)
                    throw new IOException("journal unavailable after admission");
            }
            public IList<MRKJournalRecord> ReadAll() { return new List<MRKJournalRecord>(); }
        }
        private static void MidFlowJournalFailureIsTypedFailure()
        {
            int performed = 0;
            MRKExecutionPlan plan = new MRKExecutionPlan("journal-fault", new[]
            {
                new MRKExecutionStep("write",new string[0],(ctx,ct)=>
                { performed++; return System.Threading.Tasks.Task.FromResult((object)1); })
            });
            MRKExecutionEngine engine = new MRKExecutionEngine(
                new CxVisual.Foundation.Infrastructure.Concurrency.FCRWorkerOptions(1,2),
                journal: new MidFlowFailingJournal());
            MRKFlowHandle flow = engine.StartFlow(plan);
            MRKFlowResult outcome = Await(flow.Completion);
            Check(outcome.State == MRKFlowState.Failed && outcome.Error != null &&
                flow.GetSnapshot().State == MRKFlowState.Failed &&
                performed == 0, "中途审计失效被吞掉或Flow永远Running");
            Await(engine.StopAsync());
        }

    }
}
