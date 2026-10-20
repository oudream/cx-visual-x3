using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CxVisual.Foundation.Infrastructure.Logging;

namespace CxVisual.Foundation.Infrastructure.Logging.Sinks
{
    public sealed class FLDFileLogSink : IFLDLogSink, IDisposable
    {
        private readonly object _gate = new object();
        private readonly StreamWriter _writer;
        private bool _disposed;
        public FLDFileLogSink(string path)
        {
            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            _writer = new StreamWriter(new FileStream(full, FileMode.Append, FileAccess.Write, FileShare.Read),
                new UTF8Encoding(false));
            _writer.AutoFlush = true;
        }
        public void Write(FLDLogRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("FLDFileLogSink");
                _writer.WriteLine(record.ToString());
            }
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (!_disposed) { _writer.Dispose(); _disposed = true; }
            }
        }
    }
    /// <summary>可独立测试的分类日志内存Sink，不依赖Studio或Runtime。</summary>
}
