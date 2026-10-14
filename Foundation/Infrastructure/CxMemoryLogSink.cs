using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
namespace CxVisual.Foundation.Infrastructure
{
    public sealed class CxMemoryLogSink : ICxLogSink
    {
        private readonly List<CxLogRecord> _records = new List<CxLogRecord>();
        public void Write(CxLogRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            lock (_records) { _records.Add(record); }
        }
        public int Count { get { lock (_records) { return _records.Count; } } }
        public CxLogRecord Get(int index) { lock (_records) { return _records[index]; } }
    }
}
