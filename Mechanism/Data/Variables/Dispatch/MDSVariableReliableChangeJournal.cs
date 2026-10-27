using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Data.Variables
{
    public sealed class MDSVariableReliableBackpressureException : InvalidOperationException
    {
        public MDSVariableReliableBackpressureException(string scopeId, int capacity)
            : base("可靠变化队列容量已满，当前批次未入队，Scope=" + scopeId +
                ", Capacity=" + capacity + "；已提交变量值不回滚，请根据BatchResult.CommittedChanges补投") { }
    }

    public sealed class MDSVariableReliableSequenceGapException : InvalidOperationException
    {
        public MDSVariableReliableSequenceGapException(string scopeId, long expected, long received)
            : base("可靠变化队列发现未补齐的批次：Scope=" + scopeId +
                ", Expected=" + expected + ", Received=" + received +
                "；请先补投缺失批次，不得跳过事件") { }
    }

    /// <summary>
    /// 进程内批次可靠观察队列：已接受事件保留到顺序Ack，ReadPending可重复读取用于重放。
    /// 绝不因容量不足静默丢弃，明确产生Backpressure错误并由BatchResult带回捕获快照。
    /// 不提供磁盘崩溃恢复，不保证跨进程/远程ExactlyOnce；也不改变MDS单变量高速写入。
    /// </summary>
    public sealed class MDSVariableReliableChangeJournal : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MDSVariableTable _table;
        private readonly SortedDictionary<long, MDSVariableChangeBatch> _pending =
            new SortedDictionary<long, MDSVariableChangeBatch>();
        private readonly int _capacity;
        private long _lastConfirmed, _accepted, _rejected, _capacityRejected, _gapRejected;
        private bool _disposed;

        private enum AppendStatus { Accepted, CapacityFull, SequenceGap, Disposed }

        public MDSVariableReliableChangeJournal(MDSVariableTable table, int capacity = 1024)
        {
            if (table == null) throw new ArgumentNullException("table");
            if (capacity < 1) throw new ArgumentOutOfRangeException("capacity");
            _table = table; _capacity = capacity;
            ScopeId = table.ScopeId;
            // 同一Table锁下初始化起始序列并注册，避免热插入时遗漏或接收旧批次。
            table.SubscribeReliableBatches(AcceptCommitted, baseline =>
            {
                lock (_gate) _lastConfirmed = baseline;
            });
        }
        public string ScopeId { get; private set; }
        public int Capacity { get { return _capacity; } }
        public long Accepted { get { lock (_gate) return _accepted; } }
        public long Rejected { get { lock (_gate) return _rejected; } }
        public long LastConfirmedSequence { get { lock (_gate) return _lastConfirmed; } }
        public int PendingCount { get { lock (_gate) return _pending.Count; } }
        public long ExpectedNextSequence
        {
            get { lock (_gate) return checked(_lastConfirmed + _pending.Count + 1); }
        }
        public long CapacityRejected { get { lock (_gate) return _capacityRejected; } }
        public long GapRejected { get { lock (_gate) return _gapRejected; } }

        private void AcceptCommitted(MDSVariableChangeBatch batch)
        {
            AppendStatus status = Append(batch);
            // 在注销后到达的已捕获回调不应破坏已经成功的变量事务。
            if (status == AppendStatus.Disposed || status == AppendStatus.Accepted) return;
            if (status == AppendStatus.CapacityFull)
                throw new MDSVariableReliableBackpressureException(ScopeId, _capacity);
            throw new MDSVariableReliableSequenceGapException(
                ScopeId, ExpectedNextSequence, batch.BatchSequence);
        }
        // 失败一律返回false且不入队；调用方保留BatchResult.CommittedChanges补投。
        public bool TryAppend(MDSVariableChangeBatch batch)
        {
            AppendStatus status = Append(batch);
            if (status == AppendStatus.Disposed)
                throw new ObjectDisposedException("MDSVariableReliableChangeJournal");
            return status == AppendStatus.Accepted;
        }
        private AppendStatus Append(MDSVariableChangeBatch batch)
        {
            if (batch == null) throw new ArgumentNullException("batch");
            if (!string.Equals(batch.ScopeId, ScopeId, StringComparison.Ordinal))
                throw new InvalidOperationException("可靠队列Scope不匹配");
            lock (_gate)
            {
                if (_disposed) return AppendStatus.Disposed;
                if (batch.BatchSequence <= _lastConfirmed) return AppendStatus.Accepted;
                MDSVariableChangeBatch duplicate;
                if (_pending.TryGetValue(batch.BatchSequence, out duplicate))
                {
                    if (duplicate.BatchId != batch.BatchId)
                        throw new InvalidOperationException("同一批次序号对应不同BatchId，拒绝伪造重放");
                    return AppendStatus.Accepted;
                }
                long expected = checked(_lastConfirmed + _pending.Count + 1);
                // 先拒绝缺口，再判断容量；否则Ack释放容量后可能直接跳过原拒绝批次。
                if (batch.BatchSequence != expected)
                {
                    _rejected++; _gapRejected++;
                    return AppendStatus.SequenceGap;
                }
                if (_pending.Count >= _capacity)
                {
                    _rejected++; _capacityRejected++;
                    return AppendStatus.CapacityFull;
                }
                _pending.Add(batch.BatchSequence, batch);
                _accepted++;
                return AppendStatus.Accepted;
            }
        }
        public MDSVariableChangeBatch[] ReadPending(int max = 128)
        {
            if (max < 1) throw new ArgumentOutOfRangeException("max");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MDSVariableReliableChangeJournal");
                return _pending.Values.Take(max).ToArray(); // 不出队；需要显式Ack。
            }
        }
        public bool Acknowledge(long batchSequence)
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MDSVariableReliableChangeJournal");
                if (_pending.Count == 0) return false;
                long oldest = _pending.Keys.First();
                if (batchSequence != oldest || oldest != checked(_lastConfirmed + 1))
                    throw new InvalidOperationException("确认必须按BatchSequence连续推进");
                _pending.Remove(oldest);
                _lastConfirmed = oldest;
                return true;
            }
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
            }
            _table.BatchCommitted -= AcceptCommitted;
            lock (_gate) _pending.Clear();
        }
    }
}
