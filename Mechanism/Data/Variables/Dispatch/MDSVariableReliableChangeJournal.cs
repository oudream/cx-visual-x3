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
        private long _lastConfirmed, _accepted, _rejected;
        private bool _disposed;

        public MDSVariableReliableChangeJournal(MDSVariableTable table, int capacity = 1024)
        {
            if (table == null) throw new ArgumentNullException("table");
            if (capacity < 1) throw new ArgumentOutOfRangeException("capacity");
            _table = table; _capacity = capacity;
            ScopeId = table.ScopeId;
            table.BatchCommitted += AcceptCommitted;
        }
        public string ScopeId { get; private set; }
        public int Capacity { get { return _capacity; } }
        public long Accepted { get { lock (_gate) return _accepted; } }
        public long Rejected { get { lock (_gate) return _rejected; } }
        public long LastConfirmedSequence { get { lock (_gate) return _lastConfirmed; } }
        public int PendingCount { get { lock (_gate) return _pending.Count; } }

        private void AcceptCommitted(MDSVariableChangeBatch batch)
        {
            if (!TryAppend(batch))
                throw new MDSVariableReliableBackpressureException(ScopeId, _capacity);
        }
        // 手动补投BatchResult.CommittedChanges也走此入口，重复提交同一批次幂等。
        public bool TryAppend(MDSVariableChangeBatch batch)
        {
            if (batch == null) throw new ArgumentNullException("batch");
            if (!string.Equals(batch.ScopeId, ScopeId, StringComparison.Ordinal))
                throw new InvalidOperationException("可靠队列Scope不匹配");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("MDSVariableReliableChangeJournal");
                if (batch.BatchSequence <= _lastConfirmed || _pending.ContainsKey(batch.BatchSequence))
                    return true;
                if (_pending.Count >= _capacity) { _rejected++; return false; }
                _pending.Add(batch.BatchSequence, batch);
                _accepted++;
                return true;
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
                if (batchSequence != oldest)
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
