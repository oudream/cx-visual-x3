using System;
using System.Collections.Generic;
using System.Linq;

namespace CxVisual.Mechanism.Data.Variables
{
    /// <summary>独立一次写入定义；输入byte[]在构造时复制，避免事务校验后被调用方修改。</summary>
    public sealed class MDSVariableBatchItem
    {
        private readonly object _value;
        public MDSVariableBatchItem(string name, object value, MDSVariableUpdateOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("变量名不能为空", "name");
            Name = name.Trim(); _value = MDSVariableValueRules.Copy(value);
            Options = options ?? new MDSVariableUpdateOptions();
        }
        public string Name { get; private set; }
        public object Value { get { return MDSVariableValueRules.Copy(_value); } }
        public MDSVariableUpdateOptions Options { get; private set; }
    }

    /// <summary>
    /// 一个Scope中的显式事务请求，捕获固定条目集合；既有params UpdateBatch仍保持兼容。
    /// 批次标识仅用于诊断，不是新运行值仓或工程持久化对象。
    /// </summary>
    public sealed class MDSVariableBatch
    {
        public MDSVariableBatch(string scopeId, IEnumerable<MDSVariableBatchItem> items, string batchId = null)
        {
            if (string.IsNullOrWhiteSpace(scopeId)) throw new ArgumentException("Scope不能为空", "scopeId");
            if (items == null) throw new ArgumentNullException("items");
            MDSVariableBatchItem[] captured = items.ToArray();
            if (captured.Length == 0 || captured.Any(x => x == null))
                throw new ArgumentException("事务必须至少包含一条有效更新");
            if (captured.GroupBy(x => x.Name, StringComparer.Ordinal).Any(g => g.Count() != 1))
                throw new ArgumentException("单个批次不允许同名变量重复写入");
            ScopeId = scopeId.Trim();
            BatchId = string.IsNullOrWhiteSpace(batchId) ? Guid.NewGuid().ToString("N") : batchId.Trim();
            Items = Array.AsReadOnly(captured);
        }
        public string ScopeId { get; private set; }
        public string BatchId { get; private set; }
        public IReadOnlyList<MDSVariableBatchItem> Items { get; private set; }
    }

    public sealed class MDSVariableBatchResult
    {
        internal MDSVariableBatchResult(MDSVariableUpdateResult[] results, Exception[] notifications)
        {
            Results = Array.AsReadOnly(results);
            NotificationFailures = Array.AsReadOnly(notifications);
        }
        public IReadOnlyList<MDSVariableUpdateResult> Results { get; private set; }
        // 所有变量提交成功后，观察者异常单独记录，不能将成功提交伪装成事务回滚。
        public IReadOnlyList<Exception> NotificationFailures { get; private set; }
        public bool NotificationsSucceeded { get { return NotificationFailures.Count == 0; } }
    }

    internal sealed class MDSVariableBatchCommit
    {
        internal readonly MDSVariableUpdateResult Result;
        internal readonly Action Notify;
        internal MDSVariableBatchCommit(MDSVariableUpdateResult result, Action notify)
        { Result = result; Notify = notify; }
    }
}
