using System;
using System.Collections.Generic;
using System.IO;
using CxVisual.Mechanism.Runtime.Execution;
namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class ExecutionJournalTests
    {
        internal static void Run()
        {
            ReopenAndConservativeRecovery();
            TruncatedTailIsRecoverableButCorruptionIsNot();
            RuntimeRecordsRealFlowTransitions();
            JournalAdmissionFailureIsFailClosed();
        }
        private static void Check(bool value, string msg)
        { if (!value) throw new InvalidOperationException("运行Journal：" + msg); }
        private static string PathForTest()
        { return Path.Combine(Path.GetTempPath(), "cx-run-" + Guid.NewGuid().ToString("N") + ".bin"); }
        private static void ReopenAndConservativeRecovery()
        {
            string path = PathForTest();
            Guid id = Guid.NewGuid();
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
                        "CT", 9, "exposure", "Unknown"));
                    journal.Append(new MRKJournalRecord(MRKJournalKind.FlowFinished, id,
                        "CT", 9, "", "Failed"));
                    IList<MRKJournalRecord> history = journal.ReadAll();
                    Check(history.Count == 4 && history[3].Sequence == 4,
                        "重开Journal序列号没有延续");
                    Check(MRKJournalRecovery.Evaluate(id, history).Disposition ==
                        MRKRecoveryDisposition.RequiresReconciliation,
                        "未知设备操作被错误地认定为安全终态");
                    journal.Append(new MRKJournalRecord(MRKJournalKind.ExternalConfirmed, id,
                        "CT", 9, "exposure", "Confirmed"));
                    Check(MRKJournalRecovery.Evaluate(id, journal.ReadAll()).Disposition ==
                        MRKRecoveryDisposition.Closed, "已确认且完整的Flow未正常结案");
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

    }
}
