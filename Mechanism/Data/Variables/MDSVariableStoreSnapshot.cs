using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Mechanism.Data.Variables
{
    /// <summary>只读运行态快照，用于在相同定义的全新Scope中恢复；绝非第二个可写Store。</summary>
    public sealed class MDSVariableStoreSnapshot
    {
        internal MDSVariableStoreSnapshot(string scopeId, Dictionary<string, object> snapshots,
            Dictionary<string, MDSVariableDefinition> definitions)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) throw new ArgumentException("Scope不能为空");
            ScopeId = scopeId;
            Variables = new ReadOnlyDictionary<string, object>(
                new Dictionary<string, object>(snapshots, StringComparer.Ordinal));
            Definitions = new ReadOnlyDictionary<string, MDSVariableDefinition>(
                new Dictionary<string, MDSVariableDefinition>(definitions, StringComparer.Ordinal));
            CapturedUtc = DateTime.UtcNow;
        }
        public string ScopeId { get; private set; }
        public DateTime CapturedUtc { get; private set; }
        // 每个值是已存在的MDSVariableSnapshot<T>不可变视图，不存新的可写Entry。
        public IReadOnlyDictionary<string, object> Variables { get; private set; }
        // 捕获Schema，避免“同名同类型但单位/读写规则不同”被误当成兼容恢复。
        public IReadOnlyDictionary<string, MDSVariableDefinition> Definitions { get; private set; }
    }
}
