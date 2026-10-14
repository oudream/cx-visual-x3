using System;
using System.IO;
using System.Text;

namespace CxVisual.Foundation.Infrastructure
{
    /// <summary>线程安全的同步文件Sink。轮转、保留、异步写入属于后续独立验收范围。</summary>
    public sealed class CxFileLogSink : ICxLogSink, IDisposable
    {
        private readonly object _gate = new object();
        private readonly StreamWriter _writer;
        private readonly bool _structured;
        private bool _disposed;

        public CxFileLogSink(string path) : this(path, false) { }

        public CxFileLogSink(string path, bool structured)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Log file path must not be empty.", "path");
            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            _writer = new StreamWriter(
                new FileStream(full, FileMode.Append, FileAccess.Write, FileShare.Read),
                new UTF8Encoding(false));
            _writer.AutoFlush = true;
            _structured = structured;
        }

        public void Write(CxLogRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("CxFileLogSink");
                _writer.WriteLine(_structured ? record.ToStructuredString() : record.ToString());
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _writer.Dispose();
                _disposed = true;
            }
        }
    }
}
