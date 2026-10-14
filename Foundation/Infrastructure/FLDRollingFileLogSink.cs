using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CxVisual.Foundation.Infrastructure
{
    /// <summary>
    /// 同步滚动文件Sink：UTC日期、文件大小分卷、独立保留策略。
    /// 严格只清理当前名字前缀的文件，不扫描删除其它专有或公共日志。
    /// </summary>
    public sealed class FLDRollingFileLogSink : IFLDLogSink, IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _directory;
        private readonly string _name;
        private readonly long _maxBytes;
        private readonly int _retentionDays;
        private StreamWriter _writer;
        private FileStream _stream;
        private string _dateKey;
        private int _part;
        private bool _disposed;

        public FLDRollingFileLogSink(string directory, string name, long maxBytes, int retentionDays)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Directory is required.", "directory");
            ValidateName(name);
            if (maxBytes < 4096) throw new ArgumentOutOfRangeException("maxBytes");
            if (retentionDays < 1) throw new ArgumentOutOfRangeException("retentionDays");
            _directory = Path.GetFullPath(directory);
            _name = name;
            _maxBytes = maxBytes;
            _retentionDays = retentionDays;
        }

        public static void ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 80 || name == "." || name == "..")
                throw new ArgumentException("Invalid log name.", "name");
            foreach (char c in name)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.'))
                    throw new ArgumentException("Log names may use only ASCII letters, digits, '.', '_' and '-'.", "name");
        }

        public void Write(FLDLogRecord record)
        {
            if (record == null) throw new ArgumentNullException("record");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException("FLDRollingFileLogSink");
                EnsureFile(record.TimeUtc);
                string line = record.ToStructuredString();
                long required = Encoding.UTF8.GetByteCount(line) + 2;
                if (_stream.Length + required > _maxBytes && _stream.Length > 0)
                {
                    CloseFile();
                    ++_part;
                    OpenFile();
                }
                _writer.WriteLine(line);
                _writer.Flush();
            }
        }

        private void EnsureFile(DateTime nowUtc)
        {
            string day = nowUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            if (_writer != null && _dateKey == day) return;
            CloseFile();
            _dateKey = day;
            _part = 0;
            Directory.CreateDirectory(_directory);
            // 仅在初次打开或UTC跨日时清理，不在每次写入时扫描目录。
            Cleanup(nowUtc.Date);
            OpenFile();
        }

        private string FilePath(int part)
        {
            return Path.Combine(_directory, _name + "-" + _dateKey + "-"
                + part.ToString("D3", CultureInfo.InvariantCulture) + ".log");
        }

        private void OpenFile()
        {
            Directory.CreateDirectory(_directory);
            // 重启后继续在最后一个分卷追加，不能覆盖既有日志。
            while (File.Exists(FilePath(_part))
                && new FileInfo(FilePath(_part)).Length >= _maxBytes) ++_part;
            _stream = new FileStream(FilePath(_part), FileMode.Append, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(_stream, new UTF8Encoding(false));
            _writer.AutoFlush = true;
        }

        private void Cleanup(DateTime todayUtc)
        {
            foreach (string file in Directory.GetFiles(_directory, _name + "-*.log"))
            {
                string raw = Path.GetFileName(file);
                string prefix = _name + "-";
                if (!raw.StartsWith(prefix, StringComparison.Ordinal)
                    || raw.Length != prefix.Length + 8 + 1 + 3 + 4) continue;
                string dayText = raw.Substring(prefix.Length, 8);
                DateTime day;
                if (!DateTime.TryParseExact(dayText, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out day)) continue;
                string tail = raw.Substring(prefix.Length + 8);
                if (tail.Length != 8 || tail[0] != '-' || !tail.EndsWith(".log", StringComparison.Ordinal))
                    continue;
                int part;
                if (!int.TryParse(tail.Substring(1, 3), NumberStyles.None,
                    CultureInfo.InvariantCulture, out part)) continue;
                if (day < todayUtc.AddDays(-_retentionDays + 1))
                    File.Delete(file); // 如果失败则向上反馈，不能悄悄忽略。
            }
        }

        private void CloseFile()
        {
            if (_writer != null) { _writer.Dispose(); _writer = null; _stream = null; }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                CloseFile();
            }
        }
    }
}
