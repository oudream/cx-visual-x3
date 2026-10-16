using System;
using System.Collections.Generic;
using System.Threading;

namespace CxVisual.Mechanism.Data.Configuration
{
    /// <summary>
    /// 从持久审计游标重放，然后衔接同进程的新提交。
    /// 后台按需调度；固定批量、单消费者，无无限内存队列。
    /// 客户端在成功处理事件后将 Cursor 持久化，即可跨进程重连重放。
    /// 错误时不前移游标，需由用户 Resume；可能重复投递，消费方应按 Revision 幂等。
    /// </summary>
    public sealed class MHSConfigCommitFeed<T> : IDisposable where T : new()
    {
        private readonly object _gate = new object();
        private readonly MHSConfigSection<T> _section;
        private readonly Action<MHSConfigCommittedRecord> _consumer;
        private readonly int _batchSize;
        private IDisposable _live;
        private string _cursor;
        private bool _running;
        private bool _rescan;
        private bool _disposed;
        private Exception _lastError;
        public event Action<Exception> DeliveryError;
        internal MHSConfigCommitFeed(MHSConfigSection<T> section, string cursor,
            int batchSize, Action<MHSConfigCommittedRecord> consumer)
        {
            if (section == null) throw new ArgumentNullException("section");
            if (consumer == null) throw new ArgumentNullException("consumer");
            if (batchSize < 1 || batchSize > 1000) throw new ArgumentOutOfRangeException("batchSize");
            _section = section; _consumer = consumer; _batchSize = batchSize;
            _cursor = cursor;
            // Subscribe first, then replay. The rescan flag closes the snapshot/live transition race.
            _live = section.SubscribeAll(Foundation.Data.Configuration.FCMConfigChangeStage.Committed,
                change => Schedule());
            Schedule();
        }
        public string Cursor { get { lock (_gate) return _cursor; } }
        public Exception LastError { get { lock (_gate) return _lastError; } }
        public void Resume()
        {
            lock (_gate) _lastError = null;
            Schedule();
        }
        private void Schedule()
        {
            bool start = false;
            lock (_gate)
            {
                if (_disposed) return;
                _rescan = true;
                if (!_running) { _running = true; start = true; }
            }
            if (start) ThreadPool.QueueUserWorkItem(state => Drain());
        }
        private void Drain()
        {
            while (true)
            {
                string cursor;
                lock (_gate)
                {
                    if (_disposed) { _running = false; return; }
                    cursor = _cursor;
                    _rescan = false;
                }
                IList<MHSConfigCommittedRecord> records;
                try { records = _section.ReadCommittedAfter(cursor, _batchSize); }
                catch (Exception ex) { StopOnError(ex); return; }
                if (records.Count == 0)
                {
                    lock (_gate)
                    {
                        if (_rescan && !_disposed) continue;
                        _running = false;
                        return;
                    }
                }
                foreach (MHSConfigCommittedRecord record in records)
                {
                    lock (_gate)
                    {
                        if (_disposed) { _running = false; return; }
                    }
                    try { _consumer(record); }
                    catch (Exception ex) { StopOnError(ex); return; }
                    lock (_gate) _cursor = record.Cursor;
                }
            }
        }
        private void StopOnError(Exception error)
        {
            Action<Exception> listener;
            lock (_gate) { _lastError = error; _running = false; listener = DeliveryError; }
            if (listener != null) { try { listener(error); } catch { /* Diagnostic callbacks never throw into publishers. */ } }
        }
        public void Dispose()
        {
            IDisposable live;
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                live = _live;
                _live = null;
            }
            if (live != null) live.Dispose();
            // A callback already executing when Dispose starts may finish. No new callback starts afterwards.
        }
    }
}
