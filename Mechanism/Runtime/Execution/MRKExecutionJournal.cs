using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace CxVisual.Mechanism.Runtime.Execution
{
    /// <summary>日志只记运行事实与关联ID，不写入图像、密钥、输入输出负载。</summary>
    public enum MRKJournalKind
    {
        FlowStarted = 1,
        StepTransition = 2,
        FlowFinished = 3,
        ExternalUnknown = 4,
        ExternalConfirmed = 5
    }
    public sealed class MRKJournalRecord
    {
        public MRKJournalRecord(MRKJournalKind kind, Guid flowId, string planId,
            long revision, string stepId, string state, string correlationId = null)
        {
            if (flowId == Guid.Empty) throw new ArgumentException("Flow ID不能为空", "flowId");
            if (string.IsNullOrWhiteSpace(planId)) throw new ArgumentException("Plan ID不能为空", "planId");
            if (revision < 0) throw new ArgumentOutOfRangeException("revision");
            if (!Enum.IsDefined(typeof(MRKJournalKind), kind))
                throw new ArgumentOutOfRangeException("kind");
            Kind = kind; FlowId = flowId; PlanId = planId;
            Revision = revision; StepId = stepId ?? string.Empty;
            State = state ?? string.Empty; CorrelationId = correlationId ?? string.Empty;
            TimestampUtc = DateTime.UtcNow;
        }
        public long Sequence { get; internal set; }
        public MRKJournalKind Kind { get; private set; }
        public Guid FlowId { get; private set; }
        public string PlanId { get; private set; }
        public long Revision { get; private set; }
        public string StepId { get; private set; }
        public string State { get; private set; }
        public string CorrelationId { get; private set; }
        public DateTime TimestampUtc { get; internal set; }
    }
    public interface IMRKExecutionJournal
    {
        void Append(MRKJournalRecord record);
        IList<MRKJournalRecord> ReadAll();
    }
    public enum MRKRecoveryDisposition { NoRecord, Closed, RequiresReconciliation }
    public sealed class MRKRecoveryDecision
    {
        internal MRKRecoveryDecision(Guid flow, MRKRecoveryDisposition disposition,
            string reason)
        { FlowId = flow; Disposition = disposition; Reason = reason; }
        public Guid FlowId { get; private set; }
        public MRKRecoveryDisposition Disposition { get; private set; }
        public string Reason { get; private set; }
    }

    /// <summary>保守恢复：未知外部动作和无终态流程都需要人工/Provider核对，不自动重播。</summary>
    public static class MRKJournalRecovery
    {
        public static MRKRecoveryDecision Evaluate(Guid flowId, IEnumerable<MRKJournalRecord> history)
        {
            if (history == null) throw new ArgumentNullException("history");
            bool seen = false, finished = false;
            HashSet<string> unknown = new HashSet<string>(StringComparer.Ordinal);
            foreach (MRKJournalRecord record in history)
            {
                if (record == null || record.FlowId != flowId) continue;
                seen = true;
                if (record.Kind == MRKJournalKind.FlowFinished) finished = true;
                if (record.Kind == MRKJournalKind.ExternalUnknown) unknown.Add(record.StepId);
                if (record.Kind == MRKJournalKind.ExternalConfirmed) unknown.Remove(record.StepId);
            }
            if (!seen) return new MRKRecoveryDecision(flowId, MRKRecoveryDisposition.NoRecord, "没有运行记录");
            if (unknown.Count != 0)
                return new MRKRecoveryDecision(flowId, MRKRecoveryDisposition.RequiresReconciliation,
                    "存在结果未确认的外部操作");
            if (!finished)
                return new MRKRecoveryDecision(flowId, MRKRecoveryDisposition.RequiresReconciliation,
                    "没有Flow终态，不能自动重新执行");
            return new MRKRecoveryDecision(flowId, MRKRecoveryDisposition.Closed, "运行已结算");
        }
    }

    /// <summary>
    /// .NET48本地单写入者二进制Journal。帧长度+校验，Flush(true)保证每次Append尝试落盘；
    /// 只允许修剪意外断电产生的截断尾帧，完整帧校验损坏直接报错而不跳过。
    /// 跨进程所有权/主备接管属于Host机制，本实现禁止多写者共用文件。
    /// </summary>
    public sealed class MRKFileExecutionJournal : IMRKExecutionJournal, IDisposable
    {
        private const int MaxPayload = 1024 * 1024;
        private readonly object _gate = new object();
        private readonly FileStream _file;
        private long _sequence;
        private bool _closed;
        public MRKFileExecutionJournal(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("日志路径不能为空", "path");
            _file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            try
            {
                long validLength;
                IList<MRKJournalRecord> history = ReadFrames(true, out validLength);
                if (validLength != _file.Length) _file.SetLength(validLength);
                if (history.Count > 0) _sequence = history[history.Count - 1].Sequence;
                _file.Seek(0, SeekOrigin.End);
            }
            catch { _file.Dispose(); throw; }
        }
        public void Append(MRKJournalRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            lock (_gate)
            {
                EnsureOpen();
                long next = checked(_sequence + 1);
                byte[] payload = Encode(record, next);
                if (payload.Length > MaxPayload)
                    throw new ArgumentException("Journal记录过大", "record");
                uint checksum = Checksum(payload);
                _file.Seek(0, SeekOrigin.End);
                using (BinaryWriter writer = new BinaryWriter(_file, Encoding.UTF8, true))
                {
                    writer.Write(payload.Length);
                    writer.Write(payload);
                    writer.Write(checksum);
                    writer.Flush();
                }
                _file.Flush(true);
                _sequence = next;
                record.Sequence = next;
            }
        }
        public IList<MRKJournalRecord> ReadAll()
        {
            lock (_gate)
            {
                EnsureOpen();
                long length;
                IList<MRKJournalRecord> history = ReadFrames(false, out length);
                return new ReadOnlyCollection<MRKJournalRecord>(
                    new List<MRKJournalRecord>(history));
            }
        }
        private IList<MRKJournalRecord> ReadFrames(bool allowTruncatedTail, out long length)
        {
            List<MRKJournalRecord> items = new List<MRKJournalRecord>();
            _file.Seek(0, SeekOrigin.Begin);
            using (BinaryReader reader = new BinaryReader(_file, Encoding.UTF8, true))
            {
                long expectedSequence = 1;
                while (_file.Position < _file.Length)
                {
                    long start = _file.Position;
                    if (_file.Length - start < 4)
                    { if (!allowTruncatedTail) throw new InvalidDataException("Journal长度字段截断");
                      length = start; return items; }
                    int size = reader.ReadInt32();
                    if (size <= 0 || size > MaxPayload)
                        throw new InvalidDataException("Journal帧长度非法");
                    if (_file.Length - _file.Position < (long)size + 4)
                    { if (!allowTruncatedTail) throw new InvalidDataException("Journal尾帧截断");
                      length = start; return items; }
                    byte[] payload = reader.ReadBytes(size);
                    uint checksum = reader.ReadUInt32();
                    if (Checksum(payload) != checksum)
                        throw new InvalidDataException("Journal校验失败，不能跳过损坏的完整记录");
                    MRKJournalRecord record = Decode(payload);
                    if (record.Sequence != expectedSequence++)
                        throw new InvalidDataException("Journal序号不连续");
                    items.Add(record);
                }
            }
            length = _file.Position;
            return items;
        }
        private static byte[] Encode(MRKJournalRecord record, long sequence)
        {
            using (MemoryStream buffer = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(buffer, Encoding.UTF8, true))
            {
                writer.Write((byte)1);
                writer.Write(sequence);
                writer.Write(record.TimestampUtc.Ticks);
                writer.Write((int)record.Kind);
                writer.Write(record.FlowId.ToByteArray());
                writer.Write(record.PlanId);
                writer.Write(record.Revision);
                writer.Write(record.StepId);
                writer.Write(record.State);
                writer.Write(record.CorrelationId);
                writer.Flush();
                return buffer.ToArray();
            }
        }
        private static MRKJournalRecord Decode(byte[] bytes)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    if (reader.ReadByte() != 1) throw new InvalidDataException("Journal版本无效");
                    long seq = reader.ReadInt64(), ticks = reader.ReadInt64();
                    MRKJournalKind kind = (MRKJournalKind)reader.ReadInt32();
                    Guid flowId = new Guid(reader.ReadBytes(16));
                    MRKJournalRecord result = new MRKJournalRecord(kind, flowId,
                        reader.ReadString(), reader.ReadInt64(), reader.ReadString(),
                        reader.ReadString(), reader.ReadString());
                    result.Sequence = seq;
                    result.TimestampUtc = new DateTime(ticks, DateTimeKind.Utc);
                    if (stream.Position != stream.Length)
                        throw new InvalidDataException("Journal包含非预期尾部数据");
                    return result;
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException ||
                ex is ArgumentException || ex is ArgumentOutOfRangeException)
            { throw new InvalidDataException("Journal记录内容损坏", ex); }
        }
        private static uint Checksum(byte[] bytes)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (byte value in bytes) hash = (hash ^ value) * 16777619;
                return hash;
            }
        }
        private void EnsureOpen()
        { if (_closed) throw new ObjectDisposedException("MRKFileExecutionJournal"); }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_closed) return;
                _closed = true;
                _file.Dispose();
            }
        }
    }
}
