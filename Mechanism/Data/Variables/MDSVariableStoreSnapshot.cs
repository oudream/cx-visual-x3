using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Mechanism.Data.Variables
{
    /// <summary>只读运行态快照，用于在相同定义的全新Scope中恢复；绝非第二个可写Store。</summary>
    public sealed class MDSVariableStoreSnapshot
    {
        internal MDSVariableStoreSnapshot(string scopeId, Dictionary<string, object> snapshots)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) throw new ArgumentException("Scope不能为空");
            ScopeId = scopeId;
            Variables = new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>(snapshots, StringComparer.Ordinal));
            CapturedUtc = DateTime.UtcNow;
        }
        public string ScopeId { get; private set; }
        public DateTime CapturedUtc { get; private set; }
        // 每个值是已存在的MDSVariableSnapshot<T>不可变视图，不存新的可写Entry。
        public IReadOnlyDictionary<string, object> Variables { get; private set; }
    }
}
