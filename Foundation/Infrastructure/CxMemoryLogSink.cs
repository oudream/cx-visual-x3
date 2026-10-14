using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Infrastructure
{
    /// <summary>按顺序保留记录的内存Sink。新实例可指定容量，避免界面日志无限增长。</summary>
    public sealed class CxMemoryLogSink : ICxLogSink
    {
        private readonly List<CxLogRecord> _records = new List<CxLogRecord>();
        private readonly int _maxRecords;
        private long _droppedCount;

        /// <summary>兼容原有无容量限制行为；新调用方应明确指定最大记录数。</summary>
        public CxMemoryLogSink() : this(int.MaxValue) { }

        public CxMemoryLogSink(int maxRecords)
        {
            if (maxRecords <= 0) throw new ArgumentOutOfRangeException("maxRecords");
            _maxRecords = maxRecords;
        }

        public void Write(CxLogRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            lock (_records)
            {
                if (_records.Count >= _maxRecords)
                {
                    _records.RemoveAt(0);
                    ++_droppedCount;
                }
                _records.Add(record);
            }
        }

        public int Count { get { lock (_records) { return _records.Count; } } }
        public long DroppedCount { get { lock (_records) { return _droppedCount; } } }

        public CxLogRecord Get(int index)
        {
            lock (_records) { return _records[index]; }
        }

        /// <summary>返回快照副本，避免把内部集合暴露给界面线程。</summary>
        public CxLogRecord[] Snapshot()
        {
            lock (_records) { return _records.ToArray(); }
        }
    }
}
