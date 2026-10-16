using System;
using System.Collections.Generic;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Data.Configuration
{
    /// <summary>
    /// 每次成功提交的不可变审计证据。
    /// 不保存机密实际值；操作者身份需由接入层的授权审计后续提供。
    /// </summary>
    /// <summary>持久提交事件；Cursor 是单实例的修订游标，而不是进程内回调计数。</summary>
    public sealed class MHSConfigCommittedRecord
    {
        public FCMConfigIdentity Identity { get; internal set; }
        public string Cursor { get; internal set; }
        public long Sequence { get; internal set; }
        public string TransactionId { get; internal set; }
        public string Revision { get; internal set; }
        public DateTimeOffset RecordedAtUtc { get; internal set; }
        public string Reason { get; internal set; }
        public string BodySha256 { get; internal set; }
        public IList<string> ChangedPaths { get; internal set; }
    }

    public sealed class MHSConfigAuditEntry
    {
        public string Revision { get; set; }
        public string Scope { get; set; }
        public string Reason { get; set; }
        public string TransactionId { get; set; }
        public DateTimeOffset RecordedAtUtc { get; set; }
        public string JsonSha256 { get; set; }
        public List<string> ChangedPaths { get; set; } = new List<string>();
    }
}
