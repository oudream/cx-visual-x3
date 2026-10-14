using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CxVisual.Foundation.Infrastructure
{
    /// <summary>
    /// 主机级日志实例：公共日志 + 可选专有日志 + 重要事件；
    /// 有界队列与可等待Drain；不使用进程全局可变单例。
    /// </summary>
    public sealed class CxLogHub : IDisposable
    {
        private readonly BlockingCollection<object> _queue;
        private readonly Thread _worker;
        private readonly object _sinkGate = new object();
        private readonly CxRollingFileLogSink _common;
        private readonly CxRollingFileLogSink _important;
        private readonly Dictionary<string, CxRollingFileLogSink> _dedicated
            = new Dictionary<string, CxRollingFileLogSink>(StringComparer.OrdinalIgnoreCase);
        private readonly string _root;
        private readonly int _historyLimit;
        private readonly Queue<CxLogRecord> _history = new Queue<CxLogRecord>();
        private long _dropped;
        private Exception _lastFailure;
        private volatile bool _disposed;
        private readonly int _maxDedicatedSinks;

        public CxLogHub(string logRoot, int queueCapacity = 2048, int historyLimit = 500,
            int maxDedicatedSinks = 32)
        {
            if (string.IsNullOrWhiteSpace(logRoot)) throw new ArgumentException("Missing log root.", "logRoot");
            if (queueCapacity < 8) throw new ArgumentOutOfRangeException("queueCapacity");
            if (historyLimit < 1) throw new ArgumentOutOfRangeException("historyLimit");
            if (maxDedicatedSinks < 1) throw new ArgumentOutOfRangeException("maxDedicatedSinks");
            _root = Path.GetFullPath(logRoot);
            _historyLimit = historyLimit;
            _maxDedicatedSinks = maxDedicatedSinks;
            _common = new CxRollingFileLogSink(Path.Combine(_root, "Common"), "runtime", 8 * 1024 * 1024, 7);
            _important = new CxRollingFileLogSink(Path.Combine(_root, "Important"), "important", 8 * 1024 * 1024, 70);
            _queue = new BlockingCollection<object>(queueCapacity);
            _worker = new Thread(ProcessQueue) { IsBackground = true, Name = "CxVisual.LogWriter" };
            _worker.Start();
        }

        public event Action<CxLogRecord> RecordReceived;
        public long DroppedCount { get { return Interlocked.Read(ref _dropped); } }
        public Exception LastFailure { get { lock (_sinkGate) { return _lastFailure; } } }

        /// <summary>写入记录。队列满返回false且统计丢弃；Critical/Important多等待2秒。</summary>
        public bool Write(CxLogRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            if (!string.IsNullOrEmpty(record.DedicatedName))
                CxRollingFileLogSink.ValidateName(record.DedicatedName);
            if (_disposed) throw new ObjectDisposedException("CxLogHub");
            bool critical = record.IsImportant || record.Level == CxLogLevel.Critical;
            try
            {
                if (_queue.TryAdd(record, critical ? 2000 : 0)) return true;
            }
            catch (InvalidOperationException)
            {
                if (_disposed) throw new ObjectDisposedException("CxLogHub");
            }
            Interlocked.Increment(ref _dropped);
            return false;
        }

        public bool Log(CxLogLevel level, string module, string message,
            string dedicatedName = null, string action = null, bool important = false,
            string correlationId = null, Exception exception = null)
        {
            return Write(new CxLogRecord(level, module, action, message,
                dedicatedName, correlationId, important, exception));
        }

        /// <summary>获取小型专有日志入口，调用者无需创建或管理文件Writer。</summary>
        public CxDedicatedLogger ForDedicated(string module, string dedicatedName)
        {
            CxRollingFileLogSink.ValidateName(dedicatedName);
            if (_disposed) throw new ObjectDisposedException("CxLogHub");
            return new CxDedicatedLogger(this, module, dedicatedName);
        }

        public CxLogRecord[] Snapshot()
        {
            lock (_sinkGate) return _history.ToArray();
        }

        /// <summary>确保已入队记录处理完毕；不得从RecordReceived回调的Writer线程调用。</summary>
        public void Flush(int millisecondsTimeout = 10000)
        {
            if (Thread.CurrentThread == _worker)
                throw new InvalidOperationException("Flush from log writer is not allowed.");
            if (_disposed) throw new ObjectDisposedException("CxLogHub");
            using (ManualResetEventSlim done = new ManualResetEventSlim(false))
            {
                Barrier barrier = new Barrier(done);
                if (!_queue.TryAdd(barrier, millisecondsTimeout))
                    throw new TimeoutException("Log queue full while flushing.");
                if (!done.Wait(millisecondsTimeout))
                    throw new TimeoutException("Log writer did not flush in time.");
            }
            if (LastFailure != null)
                throw new IOException("Log writer reported a failure.", LastFailure);
        }

        private void ProcessQueue()
        {
            foreach (object work in _queue.GetConsumingEnumerable())
            {
                Barrier barrier = work as Barrier;
                if (barrier != null) { barrier.Signal(); continue; }
                CxLogRecord record = (CxLogRecord)work;
                try
                {
                    lock (_sinkGate)
                    {
                        _common.Write(record);
                        if (record.IsImportant) _important.Write(record);
                        if (record.DedicatedName.Length != 0)
                        {
                            CxRollingFileLogSink sink;
                            if (!_dedicated.TryGetValue(record.DedicatedName, out sink))
                            {
                                if (_dedicated.Count >= _maxDedicatedSinks)
                                    throw new InvalidOperationException("Dedicated log sink limit exceeded.");
                                sink = new CxRollingFileLogSink(
                                    Path.Combine(_root, "Dedicated"),
                                    record.DedicatedName, 8 * 1024 * 1024, 7);
                                _dedicated.Add(record.DedicatedName, sink);
                            }
                            sink.Write(record);
                        }
                        _history.Enqueue(record);
                        while (_history.Count > _historyLimit) _history.Dequeue();
                    }
                }
                catch (Exception ex)
                {
                    lock (_sinkGate) _lastFailure = ex; // 不从日志异常再次递归写日志。
                }
                Action<CxLogRecord> handlers = RecordReceived;
                if (handlers == null) continue;
                foreach (Delegate item in handlers.GetInvocationList())
                {
                    try { ((Action<CxLogRecord>)item)(record); }
                    catch (Exception ex) { lock (_sinkGate) _lastFailure = ex; }
                }
            }
        }

        public void Dispose()
        {
            if (Thread.CurrentThread == _worker)
                throw new InvalidOperationException("Dispose must not be called from the log writer callback.");
            if (_disposed) return;
            _disposed = true;
            _queue.CompleteAdding();
            if (Thread.CurrentThread != _worker)
                _worker.Join();
            lock (_sinkGate)
            {
                _common.Dispose();
                _important.Dispose();
                foreach (CxRollingFileLogSink sink in _dedicated.Values) sink.Dispose();
                _dedicated.Clear();
            }
            _queue.Dispose();
        }

        private sealed class Barrier
        {
            private readonly ManualResetEventSlim _event;
            public Barrier(ManualResetEventSlim signaled) { _event = signaled; }
            public void Signal() { _event.Set(); }
        }
    }

    public sealed class CxDedicatedLogger
    {
        private readonly CxLogHub _hub;
        private readonly string _module;
        private readonly string _name;
        internal CxDedicatedLogger(CxLogHub hub, string module, string name)
        {
            _hub = hub;
            _module = module;
            _name = name;
        }

        public bool Debug(string message) { return _hub.Log(CxLogLevel.Debug, _module, message, _name); }
        public bool Information(string message) { return _hub.Log(CxLogLevel.Information, _module, message, _name); }
        public bool Warning(string message) { return _hub.Log(CxLogLevel.Warning, _module, message, _name); }
        public bool Error(string message, Exception error = null)
        {
            return _hub.Log(CxLogLevel.Error, _module, message, _name, exception: error);
        }
    }
}
