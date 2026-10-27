using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace CxVisual.Mechanism.Data.Variables
{
    /// <summary>策略选择，不代表四种策略都具备持久化或ExactlyOnce语义。</summary>
    public enum MDSVariableDispatchPolicy
    {
        Immediate,
        Batch,
        LatestOnly,
        Reliable
    }

    /// <summary>
    /// 在唯一MDS变量表上选择既有派发机制的生命周期适配器，不建设第二个消息中心。
    /// Immediate在提交线程同步回调；Batch仅针对UpdateBatch提交整批；
    /// LatestOnly是异步有界合并；Reliable只对批次提供显式ReadPending/Acknowledge。
    /// </summary>
    public sealed class MDSVariableDispatchRoute : IDisposable
    {
        private readonly MDSVariableTable _table;
        private readonly Action<MDSVariableChange> _immediate;
        private readonly Action<MDSVariableChangeBatch> _batch;
        private readonly MDSVariableChangeDispatcher _dispatcher;
        private readonly MDSVariableSubscription _subscription;
        private readonly MDSVariableReliableChangeJournal _journal;
        private int _disposed;

        private MDSVariableDispatchRoute(MDSVariableTable table, MDSVariableDispatchPolicy policy,
            Action<MDSVariableChange> immediate, Action<MDSVariableChangeBatch> batch,
            MDSVariableChangeDispatcher dispatcher, MDSVariableSubscription subscription,
            MDSVariableReliableChangeJournal journal)
        {
            _table = table; Policy = policy; _immediate = immediate; _batch = batch;
            _dispatcher = dispatcher; _subscription = subscription; _journal = journal;
        }

        public MDSVariableDispatchPolicy Policy { get; private set; }

        public static MDSVariableDispatchRoute Subscribe(MDSVariableTable table,
            MDSVariableDispatchPolicy policy,
            Action<IReadOnlyList<MDSVariableChange>> callback,
            MDSVariableSubscriptionOptions options = null, int capacity = 4096)
        {
            if (table == null) throw new ArgumentNullException("table");
            if (callback == null) throw new ArgumentNullException("callback");
            if (capacity < 1) throw new ArgumentOutOfRangeException("capacity");
            if (policy == MDSVariableDispatchPolicy.Reliable)
                throw new InvalidOperationException("Reliable需要逐批Ack；请使用CreateReliable而非回调自动确认");
            if (policy != MDSVariableDispatchPolicy.Immediate &&
                policy != MDSVariableDispatchPolicy.Batch &&
                policy != MDSVariableDispatchPolicy.LatestOnly)
                throw new ArgumentOutOfRangeException("policy");
            MDSVariableSubscriptionOptions filter = options ?? new MDSVariableSubscriptionOptions();
            if (policy == MDSVariableDispatchPolicy.LatestOnly)
            {
                MDSVariableChangeDispatcher worker = table.CreateDispatcher(capacity);
                try
                {
                    MDSVariableSubscription sub = worker.SubscribeWithOptions(callback, filter);
                    return new MDSVariableDispatchRoute(table, policy, null, null, worker, sub, null);
                }
                catch { worker.Dispose(); throw; }
            }
            if (policy == MDSVariableDispatchPolicy.Immediate)
            {
                Action<MDSVariableChange> observer = change =>
                {
                    if (Selects(change, filter))
                        callback(Array.AsReadOnly(new[] { change }));
                };
                table.ChangeCaptured += observer;
                return new MDSVariableDispatchRoute(table, policy, observer, null, null, null, null);
            }
            Action<MDSVariableChangeBatch> onBatch = batch =>
            {
                MDSVariableChange[] chosen = batch.Changes.Where(change => Selects(change, filter)).ToArray();
                if (chosen.Length != 0) callback(Array.AsReadOnly(chosen));
            };
            table.BatchCommitted += onBatch;
            return new MDSVariableDispatchRoute(table, policy, null, onBatch, null, null, null);
        }

        public static MDSVariableDispatchRoute CreateReliable(MDSVariableTable table, int capacity = 1024)
        {
            if (table == null) throw new ArgumentNullException("table");
            MDSVariableReliableChangeJournal journal = new MDSVariableReliableChangeJournal(table, capacity);
            return new MDSVariableDispatchRoute(table, MDSVariableDispatchPolicy.Reliable,
                null, null, null, null, journal);
        }

        private static bool Selects(MDSVariableChange change, MDSVariableSubscriptionOptions filter)
        {
            return (filter.Ids == null || filter.Ids.Contains(change.VariableId)) &&
                (change.Kind & filter.Kinds) != MDSVariableChangeKind.None;
        }

        public bool Drain(TimeSpan timeout)
        {
            DemandActive();
            return _dispatcher == null || _dispatcher.Drain(timeout);
        }

        public MDSVariableChangeBatch[] ReadPending(int max = 128)
        {
            DemandReliable();
            return _journal.ReadPending(max);
        }

        public bool Acknowledge(long sequence)
        {
            DemandReliable();
            return _journal.Acknowledge(sequence);
        }

        public bool TryReplay(MDSVariableChangeBatch batch)
        {
            DemandReliable();
            return _journal.TryAppend(batch);
        }

        public long LastConfirmedSequence
        {
            get { DemandReliable(); return _journal.LastConfirmedSequence; }
        }
        public int PendingCount
        {
            get { DemandReliable(); return _journal.PendingCount; }
        }

        private void DemandActive()
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) != 0)
                throw new ObjectDisposedException("MDSVariableDispatchRoute");
        }
        private void DemandReliable()
        {
            DemandActive();
            if (_journal == null) throw new InvalidOperationException("仅Reliable可读取和确认批次");
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (_immediate != null) _table.ChangeCaptured -= _immediate;
            if (_batch != null) _table.BatchCommitted -= _batch;
            if (_subscription != null) _subscription.Dispose();
            if (_dispatcher != null) _dispatcher.Dispose();
            if (_journal != null) _journal.Dispose();
        }
    }
}
