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
    }
}
