using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

using CxVisual.Foundation.Infrastructure.Logging;

namespace CxVisual.Foundation.Infrastructure.Logging.Dedicated
{
    /// <summary>
    /// F16 独立同步专有文件日志入口；不使用 FLDLogService、
    /// 后台线程、队列或 FLDLogHelper.LogEvent。
    /// </summary>
    public static class FLDEventLogHelper
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, FLDImportantEventLog> Writers =
            new Dictionary<string, FLDImportantEventLog>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, FLDLogLevel> Levels =
            new Dictionary<string, FLDLogLevel>(StringComparer.OrdinalIgnoreCase);
        private static string _directory;
        private static int _retentionDays;
        private static bool _flushOnWrite;
        private static FLDLogLevel _defaultLevel = FLDLogLevel.Debug;

        public static bool IsInitialized
        {
            get { lock (Gate) { return _directory != null; } }
        }

        public static void Init(string logRoot, int retentionDays = 70, bool flushOnWrite = true)
        {
            if (string.IsNullOrWhiteSpace(logRoot))
                throw new ArgumentException("Log root must be specified.", "logRoot");
            if (retentionDays < 1) throw new ArgumentOutOfRangeException("retentionDays");
            string full = Path.GetFullPath(logRoot);
            lock (Gate)
            {
                if (_directory != null)
                {
                    if (string.Equals(_directory, full, StringComparison.OrdinalIgnoreCase)
                        && _retentionDays == retentionDays && _flushOnWrite == flushOnWrite)
                        return;
                    throw new InvalidOperationException(
                        "Synchronous log already initialized with different options.");
                }
                _directory = full;
                _retentionDays = retentionDays;
                _flushOnWrite = flushOnWrite;
                _defaultLevel = FLDLogLevel.Debug;
                Levels.Clear();
            }
        }

        public static void SetLevel(FLDLogLevel level)
        {
            ValidateLevel(level);
            lock (Gate)
            {
                EnsureInitialized();
                _defaultLevel = level;
                Levels.Clear();
            }
        }

        public static void SetLevel(FLDLOG_TYPE category, FLDLogLevel level)
        {
            SetLevel(category.ToString(), level);
        }

        public static void SetLevel(string category, FLDLogLevel level)
        {
            FLDImportantEventLog.ValidateName(category);
            ValidateLevel(level);
            lock (Gate)
            {
                EnsureInitialized();
                Levels[category] = level;
            }
        }

        public static bool Debug(FLDLOG_TYPE category, string eventType, string source = "",
            string description = "", Dictionary<string, string> extraData = null,
            [CallerMemberName] string memberName = "")
        {
            return Log(category.ToString(), FLDLogLevel.Debug, eventType,
                source, description, extraData, memberName);
        }

        public static bool Information(FLDLOG_TYPE category, string eventType, string source = "",
            string description = "", Dictionary<string, string> extraData = null,
            [CallerMemberName] string memberName = "")
        {
            return Log(category.ToString(), FLDLogLevel.Information, eventType,
                source, description, extraData, memberName);
        }

        public static bool Warning(FLDLOG_TYPE category, string eventType, string source = "",
            string description = "", Dictionary<string, string> extraData = null,
            [CallerMemberName] string memberName = "")
        {
            return Log(category.ToString(), FLDLogLevel.Warning, eventType,
                source, description, extraData, memberName);
        }

        public static bool Error(FLDLOG_TYPE category, string eventType, string source = "",
            string description = "", Dictionary<string, string> extraData = null,
            [CallerMemberName] string memberName = "")
        {
            return Log(category.ToString(), FLDLogLevel.Error, eventType,
                source, description, extraData, memberName);
        }

        public static bool Information(string category, string eventType, string source = "",
            string description = "", Dictionary<string, string> extraData = null,
            [CallerMemberName] string memberName = "")
        {
            return Log(category, FLDLogLevel.Information, eventType,
                source, description, extraData, memberName);
        }

        public static bool Warning(string category, string eventType, string source = "",
            string description = "", Dictionary<string, string> extraData = null,
            [CallerMemberName] string memberName = "")
        {
            return Log(category, FLDLogLevel.Warning, eventType,
                source, description, extraData, memberName);
        }

        public static bool Error(string category, string eventType, string source = "",
            string description = "", Dictionary<string, string> extraData = null,
            [CallerMemberName] string memberName = "")
        {
            return Log(category, FLDLogLevel.Error, eventType,
                source, description, extraData, memberName);
        }

        public static bool Log(FLDLOG_TYPE category, FLDLogLevel level, string eventType,
            string source = "", string description = "",
            Dictionary<string, string> extraData = null,
            [CallerMemberName] string memberName = "", bool flushToDisk = false)
        {
            return Log(category.ToString(), level, eventType, source, description,
                extraData, memberName, flushToDisk);
        }

        /// <summary>
        /// 直接在调用线程写文件；返回true表示已执行所配置的Write/Flush，
        /// 返回false表示被等级过滤；IO异常直接向调用方报告。
        /// </summary>
        public static bool Log(string category, FLDLogLevel level, string eventType,
            string source = "", string description = "",
            Dictionary<string, string> extraData = null,
            [CallerMemberName] string memberName = "", bool flushToDisk = false)
        {
            FLDImportantEventLog.ValidateName(category);
            ValidateLevel(level);
            lock (Gate)
            {
                EnsureInitialized();
                FLDLogLevel threshold;
                if (!Levels.TryGetValue(category, out threshold)) threshold = _defaultLevel;
                if (level < threshold) return false;
                FLDImportantEventLog writer = GetOrCreateWriter(category);
                FLDImportantEventLogEntry entry = new FLDImportantEventLogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = level,
                    EventType = eventType,
                    Source = source,
                    Description = description,
                    User = string.Empty,
                    MemberName = memberName,
                    ExtraData = extraData
                };
                writer.Write(entry, flushToDisk: flushToDisk);
                return true;
            }
        }

        public static bool Write(FLDLOG_TYPE category, FLDImportantEventLogEntry entry,
            bool flushToDisk = false)
        {
            return Write(category.ToString(), entry, flushToDisk);
        }

        public static bool Write(string category, FLDImportantEventLogEntry entry,
            bool flushToDisk = false)
        {
            if (entry == null) throw new ArgumentNullException("entry");
            FLDImportantEventLog.ValidateName(category);
            ValidateLevel(entry.Level);
            lock (Gate)
            {
                EnsureInitialized();
                FLDLogLevel threshold;
                if (!Levels.TryGetValue(category, out threshold)) threshold = _defaultLevel;
                if (entry.Level < threshold) return false;
                GetOrCreateWriter(category).Write(entry, flushToDisk: flushToDisk);
                return true;
            }
        }

        private static FLDImportantEventLog GetOrCreateWriter(string category)
        {
            FLDImportantEventLog writer;
            if (!Writers.TryGetValue(category, out writer))
            {
                writer = new FLDImportantEventLog(_directory, category,
                    _retentionDays, _flushOnWrite);
                Writers.Add(category, writer);
            }
            return writer;
        }

        public static void Flush(bool flushToDisk = false)
        {
            lock (Gate)
            {
                EnsureInitialized();
                foreach (FLDImportantEventLog writer in Writers.Values)
                    writer.Flush(flushToDisk);
            }
        }

        public static void Release()
        {
            List<Exception> failures = null;
            lock (Gate)
            {
                foreach (FLDImportantEventLog writer in Writers.Values)
                {
                    try { writer.Dispose(); }
                    catch (Exception ex)
                    {
                        if (failures == null) failures = new List<Exception>();
                        failures.Add(ex);
                    }
                }
                Writers.Clear();
                Levels.Clear();
                _directory = null;
                _defaultLevel = FLDLogLevel.Debug;
            }
            if (failures != null)
                throw new AggregateException("Failed to close synchronous logs.", failures);
        }

        [Obsolete("Use Release().")]
        public static void Relase() { Release(); }

        private static void EnsureInitialized()
        {
            if (_directory == null)
                throw new InvalidOperationException("Call FLDEventLogHelper.Init(logRoot) on startup.");
        }

        private static void ValidateLevel(FLDLogLevel level)
        {
            if (!Enum.IsDefined(typeof(FLDLogLevel), level))
                throw new ArgumentOutOfRangeException("level");
        }
    }
}
