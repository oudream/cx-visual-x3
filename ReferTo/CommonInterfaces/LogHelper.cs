using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using Serilog.Core;
using System.Runtime.CompilerServices;

namespace CommonInterfaces
{
    public static class LogHelper
    {
        public delegate void LogEventHandler(long time, LogLevel level, string logMessage, string logTag);
        public static event LogEventHandler LogEvent;

        private static LogLevel currentLogLevel = LogLevel.Verbose;

        private static ConcurrentStack<LogEntry> _logStack = new ConcurrentStack<LogEntry>();
        private static int _logId = 0;

        private static bool isRunning;

        private static Thread _logThread;
        private static AutoResetEvent _updateSignal;
        private static readonly object _lockObj = new object();

        /// <summary>
        /// 检查当前程序运行目录下是否有Logs文件夹,没有则创建
        /// </summary>
        /// <returns>返回Log文件夹路径</returns>
        private static string LogDirectoryCheck()
        {
            string logDirPath = ConfigHelper.LogFilePath("");
            DirectoryInfo DirInfo = new DirectoryInfo(logDirPath);
            if (!DirInfo.Exists)
            {
                try
                {
                    Directory.CreateDirectory(logDirPath);
                }
                catch (Exception ex)
                {
                    // 记录异常
                    Console.WriteLine($"Exception : {ex.Message}");
                    logDirPath = null;
                    throw;
                }
            }
            return logDirPath;
        }

        public static string GetLogPath(string logFileName)
        {
            // 启动时检测Logs文件夹是否存在，创建失败会退出程序
            string logDir = LogDirectoryCheck();
            //防止Logs文件夹创建失败,不存在时则生成到当前程序运行目录
            string logPath = AppHelper.JoinFullPath(logDir, logFileName);
            return logPath;
        }

        public static void Run(string LogFileName)
        {
            lock (_lockObj)
            {
                if (isRunning)
                {
                    // 已经在运行，忽略重复调用
                    return;
                }


                string logPath = GetLogPath($"{LogFileName}-log-.txt");
                Serilog.Log.Logger = new LoggerConfiguration()
                  .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                  .MinimumLevel.Verbose()
                  .CreateLogger();

                // 初始化定时器，每50毫秒执行一次
                _updateSignal = new AutoResetEvent(false);
                isRunning = true;
                _logThread = new Thread(() =>
                {
                    try
                    {
                        while (isRunning)
                        {
                            // 等待一小段时间或直到收到更新信号
                            //_updateSignal.WaitOne(); // 等待更新信号
                            _updateSignal.WaitOne(TimeSpan.FromMilliseconds(50));
                            // 
                            ProcessLoglist();
                        }

                        ProcessLoglist();
                        Serilog.Log.CloseAndFlush();
                    }
                    catch (Exception ex)
                    {
                        // 记录异常
                        Console.WriteLine($"Exception : {ex.Message}");
                        throw;
                    }

                })
                { IsBackground = true };

                _logThread.Start();
            }
        }

        public static void Stop()
        {
            lock (_lockObj)
            {
                if (!isRunning)
                {
                    // 已经停止，忽略重复调用
                    return;
                }

                isRunning = false;
                _updateSignal?.Set(); // 唤醒线程
            }

            // 这里要等在锁外，不要锁住Join，防止死锁
            _logThread?.Join();

            lock (_lockObj)
            {
                _logThread = null;
                _updateSignal?.Dispose();
                _updateSignal = null;
            }
        }

        private static void ProcessLoglist()
        {
            List<LogEntry> logList = new List<LogEntry>();
            while (_logStack.TryPop(out var logEntry))
            {
                logList.Add(logEntry);
            }
            if (logList.Count <= 0) { return; }
            // 注意：在这里 logStack 已经为空
            logList.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (var logEntry in logList)
            {
                var logLevel = logEntry.Level;
                var logTimestamp = logEntry.Timestamp;
                var message = logEntry.Message;
                //var logMessage = message.Replace("\r\n", "  ");
                var logTag = logEntry.Tag;
                var logMessage = message.Replace("\r\n", "  ");
                logMessage = $"[{logTag}] [{logEntry.MemberName}] {logMessage}";
                //string logLevelString = "信息";
                if (logLevel >= currentLogLevel)
                {
                    // 使用 Serilog 输出日志
                    switch (logLevel)
                    {
                        case LogLevel.Verbose:
                            //logLevelString = "空白";
                            Serilog.Log.Logger.Verbose(logMessage);
                            break;
                        case LogLevel.Debug:
                            //logLevelString = "调试";
                            Serilog.Log.Logger.Debug(logMessage);
                            break;
                        case LogLevel.Information:
                            //logLevelString = "信息";
                            Serilog.Log.Logger.Information(logMessage);
                            break;
                        case LogLevel.Warning:
                            //logLevelString = "警告";
                            Serilog.Log.Logger.Warning(logMessage);
                            break;
                        case LogLevel.Error:
                            //logLevelString = "错误";
                            Serilog.Log.Logger.Error(logMessage);
                            break;
                        case LogLevel.Fatal:
                            //logLevelString = "致命错误";
                            Serilog.Log.Logger.Fatal(logMessage);
                            break;
                        default:
                            Serilog.Log.Logger.Information(logMessage);
                            break;
                    }
                    //
                    if (LogEvent != null)
                    {
                        //logMessage = $"[{DateTime.Now:dd/HH:mm:ss.fff}] {logLevelString}:{logTag}> {message}\r\n";
                        LogEvent?.Invoke(logTimestamp, logLevel, logMessage, logTag);
                    }
                }
            }
        }

        public static Dictionary<string, LogLevel> LogLevelTranslations = new Dictionary<string, LogLevel>
        {
            {"空白", LogLevel.Verbose},
            {"调试", LogLevel.Debug},
            {"信息", LogLevel.Information},
            {"警告", LogLevel.Warning},
            {"错误", LogLevel.Error},
            {"致命错误", LogLevel.Fatal},
        };

        public static string GetLogLevelTranslation(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Verbose:
                    return "空白";
                case LogLevel.Debug:
                    return "调试";
                case LogLevel.Information:
                    return "信息";
                case LogLevel.Warning:
                    return "警告";
                case LogLevel.Error:
                    return "错误";
                case LogLevel.Fatal:
                    return "致命错误";
                default:
                    return "";
            }
        }

        public static void SetLogLevel(LogLevel logLevel)
        {
            currentLogLevel = logLevel;
        }

        public static void Information(string message, string tag = "", [CallerMemberName] string memberName = "")
        {
            Log(LogLevel.Information, message, tag, memberName);
        }

        public static void Warning(string message, string tag = "", [CallerMemberName] string memberName = "")
        {
            Log(LogLevel.Warning, message, tag, memberName);
        }

        public static void Error(string message, string tag = "", [CallerMemberName] string memberName = "")
        {
            Log(LogLevel.Error, message, tag, memberName);
        }

        public static void Fatal(string message, string tag = "", [CallerMemberName] string memberName = "")
        {
            Log(LogLevel.Fatal, message, tag, memberName);
        }

        public static void Debug(string message, string tag = "", [CallerMemberName] string memberName = "")
        {
            Log(LogLevel.Debug, message, tag, memberName);
        }

        public static void Exception(Exception ex, string message, string tag = "", [CallerMemberName] string memberName = "")
        {
            if (ex == null)
            {
                Log(LogLevel.Error, message, tag, memberName);
                return;
            }

            string prefix = string.IsNullOrWhiteSpace(message) ? "Unhandled exception." : message.Trim();
            Log(LogLevel.Error, prefix + Environment.NewLine + ex, tag, memberName);
        }

        public static void Log(LogLevel logLevel, string message, string tag = "", string memberName = "")
        {
            Interlocked.Increment(ref _logId);
            var logEntry = new LogEntry { Id = _logId, Level = logLevel, Timestamp = TimeHelper.GetNow(), Message = string.Copy(message), Tag = string.Copy(tag), MemberName = string.Copy(memberName) };
            // 推入新的日志消息
            _logStack.Push(logEntry);
            _updateSignal?.Set();
        }
    }

    public enum LogLevel
    {
        Verbose,
        Debug,
        Information,
        Warning,
        Error,
        Fatal
    }

    public class LogEntry
    {
        public int Id { get; set; }
        public LogLevel Level { get; set; }
        public long Timestamp { get; set; }
        public string Message { get; set; }
        public string Tag { get; set; }
        public string MemberName { get; set; }

    }

    public class LogItem
    {
        public string Text { get; set; }
        public Color Color { get; set; }
        public LogLevel Level { get; set; }
        public long Timestamp { get; set; }
        public List<string> Tags { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }


    public static class EventLogHelper
    {
        static List<ImportantEventLog> logEvents = new List<ImportantEventLog>();

        static bool initFinish = false;

        static object mx_lock = new object();
        public static void Init(bool append)
        {
            lock (mx_lock)
            {
                if (initFinish == false)
                {
                    for (int i = 0; i < Enum.GetNames(typeof(LOG_TYPE)).Length; i++)
                    {
                        var eventName = Enum.GetValues(typeof(LOG_TYPE)).Cast<LOG_TYPE>().Skip(i).First().ToString();
                        logEvents.Add(new ImportantEventLog(append: true, eventName));
                    }
                    initFinish = true;
                    SetLevel(LogLevel.Debug);
                }
            }
        }

        public static void Relase()
        {
            for (int i = 0; i < logEvents.Count; i++)
            {
                logEvents[i]?.Dispose();
            }
        }

        public static void SetLevel(LOG_TYPE log_type,LogLevel level)
        {
            if (initFinish == false)
            {
                Init(true);
            }

            logEvents[(int)log_type].logLevel = level;
        }
        public static void SetLevel(LogLevel level)
        {
            if (initFinish == false)
            {
                Init(true);
            }
            for (int i = 0; i < logEvents.Count; i++)
            {
                logEvents[i].logLevel = level;
            }
        }

        public static void Debug(LOG_TYPE log_type, string eventType, string source = "", string description = "", Dictionary<string, string> ExtraData = null, [CallerMemberName] string memberName = "")
        {
            Log(log_type, LogLevel.Debug, eventType, source, description, ExtraData, memberName);
        }
        public static void Information(LOG_TYPE log_type, string eventType, string source = "", string description = "", Dictionary<string, string> ExtraData = null, [CallerMemberName] string memberName = "")
        {
            Log(log_type, LogLevel.Information, eventType, source, description, ExtraData, memberName);
        }
        public static void Warning(LOG_TYPE log_type, string eventType, string source = "", string description = "", Dictionary<string, string> ExtraData = null, [CallerMemberName] string memberName = "")
        {
            Log(log_type, LogLevel.Warning, eventType, source, description, ExtraData, memberName);
        }

        public static void Error(LOG_TYPE log_type, string eventType, string source = "", string description = "", Dictionary<string, string> ExtraData = null, [CallerMemberName] string memberName = "")
        {
            Log(log_type, LogLevel.Error, eventType, source, description, ExtraData, memberName);
        }

        public static void Log(LOG_TYPE log_type, LogLevel logLevel, string eventType, string source, string description, Dictionary<string, string> ExtraData = null, string memberName = "")
        {
            if (initFinish == false)
            {
                Init(append: true);
            }

            if (logLevel >= logEvents[(int)log_type].logLevel)
            {
                var user = "admin";
                logEvents[(int)log_type].Write(
                        new ImportantEventLogEntry
                        {
                            Timestamp = DateTime.Now,
                            Level = logLevel,
                            EventType = eventType,
                            Source = source,
                            Description = description,
                            ExtraData = ExtraData,
                            User = user,
                            MemberName = memberName
                        }
                        , bForce: true
                    );
            }
        }
    }
    public enum LOG_TYPE
    {
        Important,
        Mes,
        Panda,
        Statistics,
        PLC,
        HLib,
        Radiation,
        ResFeeback
    }
}
