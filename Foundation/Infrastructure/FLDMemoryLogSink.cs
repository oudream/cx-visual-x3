using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
namespace CxVisual.Foundation.Infrastructure
{
    public sealed class FLDMemoryLogSink : IFLDLogSink
    {
        private readonly List<FLDLogRecord> _records = new List<FLDLogRecord>();
        public void Write(FLDLogRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            lock (_records) { _records.Add(record); }
        }
        public int Count { get { lock (_records) { return _records.Count; } } }
        public FLDLogRecord Get(int index) { lock (_records) { return _records[index]; } }
    }
}
