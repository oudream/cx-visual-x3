using System;
using System.Globalization;

namespace CxVisual.Foundation.Infrastructure
{
    /// <summary>不可变的结构化日志记录，兼容既有三参数构造。</summary>
    public sealed class FLDLogRecord
    {
        public FLDLogRecord(string category, string action, string message)
            : this(FLDLogLevel.Information, category, action, message, null, null, false, null)
        {
        }

        public FLDLogRecord(FLDLogLevel level, string moduleId, string action, string message,
            string dedicatedName, string correlationId, bool important, Exception exception)
        {
            if (!Enum.IsDefined(typeof(FLDLogLevel), level))
                throw new ArgumentOutOfRangeException("level");
            TimeUtc = DateTime.UtcNow;
            Level = level;
            Category = string.IsNullOrWhiteSpace(moduleId) ? "General" : moduleId;
            ModuleId = Category;
            Action = action ?? string.Empty;
            Message = message ?? string.Empty;
            DedicatedName = dedicatedName ?? string.Empty;
            CorrelationId = correlationId ?? string.Empty;
            IsImportant = important;
            ExceptionText = exception == null ? string.Empty : exception.ToString();
            ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess())
                ProcessId = process.Id;
        }

        public DateTime TimeUtc { get; private set; }
        public FLDLogLevel Level { get; private set; }
        public string Category { get; private set; }
        public string ModuleId { get; private set; }
        public string Action { get; private set; }
        public string Message { get; private set; }
        public string DedicatedName { get; private set; }
        public string CorrelationId { get; private set; }
        public string ExceptionText { get; private set; }
        public bool IsImportant { get; private set; }
        public int ProcessId { get; private set; }
        public int ThreadId { get; private set; }

        /// <summary>保留旧FileSink使用的四字段文本入口。</summary>
        public override string ToString()
        {
            return TimeUtc.ToString("o", CultureInfo.InvariantCulture) + " | " + Category
                + " | " + Action + " | " + Message.Replace("\r", " ").Replace("\n", " ");
        }

        /// <summary>新滚动文件使用结构化文本，包含级别/专有文件/关联ID等。</summary>
        public string ToStructuredString()
        {
            return TimeUtc.ToString("o", CultureInfo.InvariantCulture)
                + " | " + Level + " | " + Escape(ModuleId) + " | "
                + Escape(Action) + " | " + Escape(DedicatedName) + " | "
                + Escape(CorrelationId) + " | " + ProcessId + "/" + ThreadId
                + " | " + Escape(Message) + (ExceptionText.Length == 0 ? ""
                    : " | " + Escape(ExceptionText));
        }

        private static string Escape(string text)
        {
            return (text ?? string.Empty).Replace("\\", "\\\\")
                .Replace("|", "\\|").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
