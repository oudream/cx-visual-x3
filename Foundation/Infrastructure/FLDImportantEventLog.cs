using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CxVisual.Foundation.Infrastructure
{
    /// <summary>
    /// F16 独立同步专有日志，不经过异步日志队列或实时事件订阅。
    /// 默认调用线程写入并刷新 StreamWriter；可选请求 FileStream.Flush(true)。
    /// </summary>
    public sealed class FLDImportantEventLog : IDisposable
    {
        private const int FormatVersion = 1;
        private readonly object _gate = new object();
        private readonly string _root;
        private readonly string _fileName;
        private readonly int _retentionDays;
        private readonly bool _flushOnWrite;
        private readonly Func<DateTime> _clock;
        private FileStream _stream;
        private StreamWriter _writer;
        private string _dayKey;
        private string _path;
        private bool _disposed;

        public FLDImportantEventLog(string logDirectory, string fileName,
            int retentionDays = 70, bool flushOnWrite = true, Func<DateTime> clock = null)
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
                throw new ArgumentException("Log directory cannot be empty.", "logDirectory");
            ValidateName(fileName);
            if (retentionDays < 1) throw new ArgumentOutOfRangeException("retentionDays");
            _root = Path.GetFullPath(logDirectory);
            _fileName = fileName;
            _retentionDays = retentionDays;
            _flushOnWrite = flushOnWrite;
            _clock = clock ?? (() => DateTime.Now);
        }

        public static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 80 || name == "." || name == "..")
                throw new ArgumentException("Invalid dedicated log name.", "name");
            foreach (char ch in name)
            {
                if (!((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') ||
                      (ch >= '0' && ch <= '9') || ch == '.' || ch == '_' || ch == '-'))
                    throw new ArgumentException(
                        "Dedicated log name may contain only ASCII letters, numbers, '.', '_' and '-'.", "name");
            }
        }

        public string CurrentFilePath
        {
            get { lock (_gate) { return _path; } }
        }

        public void Write(FLDImportantEventLogEntry entry, bool? flush = null,
            bool flushToDisk = false)
        {
            if (entry == null) throw new ArgumentNullException("entry");
            lock (_gate)
            {
                ThrowIfDisposed();
                EnsureWriter(_clock());
                _writer.WriteLine(Format(entry));
                if ((flush ?? _flushOnWrite) || flushToDisk)
                    _writer.Flush();
                if (flushToDisk)
                    _stream.Flush(true);
            }
        }

        public void Flush(bool flushToDisk = false)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_writer != null)
                {
                    _writer.Flush();
                    if (flushToDisk) _stream.Flush(true);
                }
            }
        }

        private void EnsureWriter(DateTime now)
        {
            string key = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            if (_writer != null && string.Equals(key, _dayKey, StringComparison.Ordinal))
                return;
            // 日切换时必须先释放旧Writer；切换、写入、Flush由同一把锁保护。
            CloseWriter();
            Directory.CreateDirectory(_root);
            CleanupOwnHistory(now.Date);
            string nextPath = Path.Combine(_root, _fileName + "-" + key + ".log");
            FileStream nextStream = null;
            StreamWriter nextWriter = null;
            try
            {
                nextStream = new FileStream(nextPath, FileMode.Append, FileAccess.Write, FileShare.Read);
                nextWriter = new StreamWriter(nextStream, new UTF8Encoding(false));
                _stream = nextStream;
                _writer = nextWriter;
                _path = nextPath;
                _dayKey = key;
            }
            catch
            {
                if (nextWriter != null) nextWriter.Dispose();
                else if (nextStream != null) nextStream.Dispose();
                throw;
            }
        }

        private void CleanupOwnHistory(DateTime today)
        {
            string prefix = _fileName + "-";
            foreach (string file in Directory.GetFiles(_root, prefix + "*.log"))
            {
                string name = Path.GetFileName(file);
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Length != prefix.Length + 8 + 4) continue;
                if (!name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)) continue;
                string dateText = name.Substring(prefix.Length, 8);
                DateTime fileDay;
                if (!DateTime.TryParseExact(dateText, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out fileDay)) continue;
                if (fileDay.Date < today.AddDays(-_retentionDays + 1))
                    File.Delete(file); // 只清理当前类别；IO异常直接反馈。
            }
        }

        private static string Format(FLDImportantEventLogEntry entry)
        {
            StringBuilder sb = new StringBuilder(256);
            sb.Append('v').Append(FormatVersion).Append(' ')
                .Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture))
                .Append(" | ").Append(entry.Level)
                .Append(" | ").Append(Escape(entry.EventType))
                .Append(" | ").Append(Escape(entry.MemberName))
                .Append(" | ").Append(Escape(entry.Source))
                .Append(" | ").Append(Escape(entry.Description))
                .Append(" | ").Append(Escape(entry.User))
                .Append(" | ");
            if (entry.ExtraData != null)
            {
                bool first = true;
                foreach (KeyValuePair<string, string> item in entry.ExtraData)
                {
                    if (!first) sb.Append(';');
                    first = false;
                    sb.Append(Escape(item.Key)).Append('=').Append(Escape(item.Value));
                }
            }
            return sb.ToString();
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\")
                .Replace("\r", "\\r").Replace("\n", "\\n")
                .Replace("|", "\\|").Replace(";", "\\;").Replace("=", "\\=");
        }

        private void CloseWriter()
        {
            if (_writer != null)
            {
                try { _writer.Dispose(); }
                finally { _writer = null; _stream = null; }
            }
            else if (_stream != null)
            {
                try { _stream.Dispose(); }
                finally { _stream = null; }
            }
            _dayKey = null;
            _path = null;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException("FLDImportantEventLog");
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                CloseWriter();
                _disposed = true;
            }
        }
    }
}
