using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CommonInterfaces
{
    /// <summary>
    /// 实现 PLCProcessTask 的二进制流式存储和读取
    /// 支持写入完整记录（带记录长度前缀）以及反向扫描最新100条记录，并以 (LineNumber, BatchNumber) 为 Key 过滤，只保留最新一条。
    /// </summary>
    public class ImportantEventLog : IDisposable
    {
        private const int VERSION = 1;
  
        private FileStream _fileStream;
        private StreamWriter _writer;
        private readonly object _syncRoot = new object();
        private readonly bool _forWriting;
        private long _todayEndMs;
        private string _file_name;
        private string _file_path;
        public LogLevel logLevel = LogLevel.Error;

        #region 构造函数

        /// <summary>
        /// 构造函数（写入模式）
        /// </summary>
        /// <param name="append">是否追加写入</param>
        public ImportantEventLog(bool append, string file_name)
        {
            _todayEndMs = TimeHelper.GetTodayEndMs();
            _file_name = file_name;
            _file_path = ConfigHelper.LogFilePath(GetNowFileNameBasedOnUnixDay(DateTime.Now.ToString("yyyyMMdd"), file_name));
            EnsureDirectoryExists(_file_path);
            if (!File.Exists(_file_path)) append = false;

            //删除历史之前的文件
            DeleteFile();

            _fileStream = new FileStream(_file_path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(_fileStream, Encoding.UTF8);
            _forWriting = true;
        }

        private void DeleteFile()
        {
            try
            {
                foreach (var file in Directory.GetFiles(ConfigHelper.LogPath()))
                {
                    string fileName = Path.GetFileName(file);

                    string pattern = @"-(\d{8})\.log"; // 匹配日期部分
                    Match match = Regex.Match(fileName, pattern);
                    if (match.Success)
                    {
                        TimeSpan difference = DateTime.Now - DateTime.ParseExact(match.Groups[1].Value, "yyyyMMdd", CultureInfo.InvariantCulture); // 获取第一个括号内的匹配项
                        if (difference.Days > 70)
                        {
                            //删除文件
                            File.Delete(file);
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
        }


        private void EnsureDirectoryExists(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
        #endregion

        #region 只保留2天，交替覆盖写入文件
        // 根据 Unix 时间的天数计算文件名（0、1 交替）
        private static string GetNowFileNameBasedOnUnixDay(string day, string _file_name)
        {
            return _file_name + "-" + day.ToString() + ".log";
        }
        #endregion

        #region 写入方法
        public void Write(ImportantEventLogEntry entry, bool bForce = false)
        {
            var path = ConfigHelper.LogFilePath(GetNowFileNameBasedOnUnixDay(DateTime.Now.ToString("yyyyMMdd"), _file_name));
            EnsureDirectoryExists(path);
            bool append=true;
            if (!File.Exists(path))
            {
                append = false;
            }
            if (_file_path != path) 
            {
                _file_path = path;
                _fileStream = new FileStream(_file_path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
                _writer = new StreamWriter(_fileStream, Encoding.UTF8);
            }
            if (_writer == null) return;
            var sb = new System.Text.StringBuilder();

            sb.Append($"v{VERSION} {entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss:fff")} ->");
            sb.Append(" | ").Append(entry.Level);
            sb.Append(" | ").Append(entry.EventType);
            sb.Append(" | ").Append(entry.MemberName);
            sb.Append(" | ").Append(entry.Source);
            sb.Append(" | ").Append(entry.Description?.Replace('\n', ' ').Replace('\r', ' '));
            sb.Append(" | ").Append(entry.User ?? "");
            sb.Append(" | ");

            if (entry.ExtraData != null && entry.ExtraData.Count > 0)
            {
                sb.Append(string.Join(";", entry.ExtraData.Select(kv => $"{kv.Key}={kv.Value}")));
            }

            lock (_syncRoot)
            {
                _writer.WriteLine(sb.ToString());

                // 检查是否需要强制刷新
                if (bForce)
                {
                    _writer.Flush();
                }
            }
        }

        /// <summary>
        /// 由调用者主动刷新缓冲区到磁盘
        /// </summary>
        public void Flush()
        {
            if (!_forWriting)
                throw new InvalidOperationException("Sink opened in read mode");

            lock (_syncRoot)
            {
                _writer?.Flush();
            }
        }

        #endregion

        #region IDisposable Support

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_forWriting)
                {
                    _writer?.Dispose();
                    _writer = null;
                }
                else
                {
                    //_reader?.Dispose();
                }
                _fileStream?.Dispose();
                _fileStream = null;
            }
        }

        #endregion

    }

    public class ImportantEventLogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string EventType { get; set; }        // 例如 "SystemStart", "LoginFailed"
        public string Source { get; set; }           // 来源模块，如类名或方法
        public string Description { get; set; }      // 事件描述
        public string User { get; set; }             // 用户信息（可为空）
        public string MemberName { get; set; }
        public Dictionary<string, string> ExtraData { get; set; } = null; // 可选扩展数据
    }

}
