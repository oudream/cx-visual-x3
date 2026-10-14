using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Infrastructure
{
    /// <summary>
    /// 实例级日志分发器。接收者在锁外执行，订阅者异常会聚合反馈，不会被静默吞掉。
    /// 传入的Sink由调用方管理生命周期；本服务Dispose不自动释放外部Sink。
    /// </summary>
    public sealed class CxLogService : IDisposable
    {
        private readonly object _gate = new object();
        private readonly List<ICxLogSink> _sinks = new List<ICxLogSink>();
        private readonly Dictionary<long, Action<CxLogRecord>> _subscribers
            = new Dictionary<long, Action<CxLogRecord>>();
        private long _nextSubscriberId;
        private CxLogLevel _minimumLevel;
        private bool _disposed;

        public CxLogService(IEnumerable<ICxLogSink> sinks, CxLogLevel minimumLevel = CxLogLevel.Debug)
        {
            if (sinks == null) throw new ArgumentNullException("sinks");
            foreach (ICxLogSink sink in sinks)
            {
                if (sink == null) throw new ArgumentException("Sink cannot be null.", "sinks");
                _sinks.Add(sink);
            }
            if (_sinks.Count == 0) throw new ArgumentException("At least one sink is required.", "sinks");
            MinimumLevel = minimumLevel;
        }

        public CxLogLevel MinimumLevel
        {
            get { lock (_gate) { return _minimumLevel; } }
            set
            {
                if (!Enum.IsDefined(typeof(CxLogLevel), value))
                    throw new ArgumentOutOfRangeException("value");
                lock (_gate)
                {
                    if (_disposed) throw new ObjectDisposedException("CxLogService");
                    _minimumLevel = value;
                }
            }
        }

        /// <summary>订阅实时记录。回调不在内部锁内执行；必须释放返回的订阅句柄。</summary>
        public IDisposable Subscribe(Action<CxLogRecord> callback)
        {
            if (callback == null) throw new ArgumentNullException("callback");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("CxLogService");
                long id = ++_nextSubscriberId;
                _subscribers.Add(id, callback);
                return new Subscription(this, id);
            }
        }

        /// <summary>
        /// 按最低级别过滤，然后向所有Sink与订阅者广播。
        /// 若任何接收方失败，继续分发其他接收方，最后抛出AggregateException。
        /// </summary>
        public void Publish(CxLogRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            ICxLogSink[] sinks;
            Action<CxLogRecord>[] callbacks;
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("CxLogService");
                if (record.Level < _minimumLevel) return;
                sinks = _sinks.ToArray();
                callbacks = new Action<CxLogRecord>[_subscribers.Count];
                _subscribers.Values.CopyTo(callbacks, 0);
            }

            List<Exception> failures = null;
            foreach (ICxLogSink sink in sinks)
            {
                try { sink.Write(record); }
                catch (Exception ex)
                {
                    if (failures == null) failures = new List<Exception>();
                    failures.Add(ex);
                }
            }
            foreach (Action<CxLogRecord> callback in callbacks)
            {
                try { callback(record); }
                catch (Exception ex)
                {
                    if (failures == null) failures = new List<Exception>();
                    failures.Add(ex);
                }
            }
            if (failures != null)
                throw new AggregateException("One or more log consumers failed.", failures);
        }

        private void RemoveSubscriber(long id)
        {
            lock (_gate) { _subscribers.Remove(id); }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _subscribers.Clear();
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly CxLogService _owner;
            private readonly long _id;
            private bool _disposed;

            public Subscription(CxLogService owner, long id)
            {
                _owner = owner;
                _id = id;
            }

            public void Dispose()
            {
                lock (this)
                {
                    if (_disposed) return;
                    _disposed = true;
                }
                _owner.RemoveSubscriber(_id);
            }
        }
    }
}
