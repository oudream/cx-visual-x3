using System;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Data.Configuration
{
    /// <summary>
    /// 每次成功提交的不可变审计证据。
    /// 不保存机密实际值；操作者身份需由接入层的授权审计后续提供。
    /// </summary>
    public sealed class MHSConfigAuditEntry
    {
        public string Revision { get; set; }
        public string Scope { get; set; }
        public string Reason { get; set; }
        public DateTimeOffset RecordedAtUtc { get; set; }
        public string JsonSha256 { get; set; }
        public List<string> ChangedPaths { get; set; } = new List<string>();
    }
}
