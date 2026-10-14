using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
namespace CxVisual.Foundation.Infrastructure
{
    public sealed class CxLogRecord
    {
        public CxLogRecord(string category, string action, string message)
        {
            TimeUtc = DateTime.UtcNow;
            Category = category ?? "General";
            Action = action ?? "Unknown";
            Message = message ?? string.Empty;
        }
        public DateTime TimeUtc { get; private set; }
        public string Category { get; private set; }
        public string Action { get; private set; }
        public string Message { get; private set; }
        public override string ToString()
        {
            return TimeUtc.ToString("o", CultureInfo.InvariantCulture) + " | " + Category + " | "
                + Action + " | " + Message.Replace("\r", " ").Replace("\n", " ");
        }
    }
}
