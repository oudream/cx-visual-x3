using System;
using System.Collections.Generic;

using CxVisual.Foundation.Infrastructure.Logging;

namespace CxVisual.Foundation.Infrastructure.Logging.Dedicated
{
    /// <summary>同步专有文件事件记录，不依赖异步日志和实时订阅。</summary>
    public sealed class FLDImportantEventLogEntry
    {
        public FLDImportantEventLogEntry()
        {
            Timestamp = DateTime.Now;
            Level = FLDLogLevel.Information;
        }

        public DateTime Timestamp { get; set; }
        public FLDLogLevel Level { get; set; }
        public string EventType { get; set; }
        public string Source { get; set; }
        public string Description { get; set; }
        public string User { get; set; }
        public string MemberName { get; set; }
        public Dictionary<string, string> ExtraData { get; set; }
    }
}
