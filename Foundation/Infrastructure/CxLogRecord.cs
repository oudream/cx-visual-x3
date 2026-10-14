using System;
using System.Globalization;

namespace CxVisual.Foundation.Infrastructure
{
    /// <summary>不可通过属性赋值修改的日志记录；兼容旧三参数构造函数。</summary>
    public sealed class CxLogRecord
    {
        public CxLogRecord(string category, string action, string message)
            : this(category, action, message, CxLogLevel.Information, category,
                   string.Empty, false, null, string.Empty)
        {
        }

        public CxLogRecord(
            string category, string action, string message, CxLogLevel level,
            string moduleId, string correlationId, bool isImportant,
            Exception exception, string source = "")
        {
            if (!Enum.IsDefined(typeof(CxLogLevel), level))
                throw new ArgumentOutOfRangeException("level");
            TimeUtc = DateTime.UtcNow;
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
            Action = string.IsNullOrWhiteSpace(action) ? "Unknown" : action;
            Message = message ?? string.Empty;
            Level = level;
            ModuleId = string.IsNullOrWhiteSpace(moduleId) ? Category : moduleId;
            CorrelationId = correlationId ?? string.Empty;
            IsImportant = isImportant;
            Source = source ?? string.Empty;
            ExceptionText = exception == null ? string.Empty : exception.ToString();
        }

        public DateTime TimeUtc { get; private set; }
        public string Category { get; private set; }
        public string Action { get; private set; }
        public string Message { get; private set; }
        public CxLogLevel Level { get; private set; }
        public string ModuleId { get; private set; }
        public string CorrelationId { get; private set; }
        public bool IsImportant { get; private set; }
        public string Source { get; private set; }
        public string ExceptionText { get; private set; }

        /// <summary>保留旧版四字段文本格式，供现有简单Sink和调用方使用。</summary>
        public override string ToString()
        {
            return TimeUtc.ToString("o", CultureInfo.InvariantCulture) + " | "
                + Encode(Category) + " | " + Encode(Action) + " | " + Encode(Message);
        }

        /// <summary>
        /// 结构化文本为可读日志，不是可无损反序列化的数据协议；
        /// 要求机器查询时应使用记录属性或独立结构化格式。
        /// </summary>
        public string ToStructuredString()
        {
            return string.Join(" | ", new[]
            {
                TimeUtc.ToString("o", CultureInfo.InvariantCulture), Level.ToString(),
                Encode(ModuleId), Encode(Category), Encode(Action), Encode(CorrelationId),
                IsImportant ? "Important" : "Normal", Encode(Source),
                Encode(Message), Encode(ExceptionText)
            });
        }

        private static string Encode(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\")
                .Replace("|", "\\|").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
