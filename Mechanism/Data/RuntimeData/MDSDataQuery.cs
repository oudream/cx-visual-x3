using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    public enum MDSDataLocation { Any, FlowHeap, Input, Local, PendingOutput, CommittedOutput }

    /// <summary>统一元数据查询条件；不携带大数据实体和可写引用。</summary>
    public sealed class MDSDataQuery
    {
        private int _take = 100;
        public string NamePrefix { get; set; }
        public Type DataType { get; set; }
        public MDSDataLocation Location { get; set; }
        public MDSDataLoadState? LoadState { get; set; }
        public string RunId { get; set; }
        public string ScopeId { get; set; }
        public int Skip { get; set; }
        public int Take
        {
            get { return _take; }
            set
            {
                if (value < 1 || value > 500)
                    throw new ArgumentOutOfRangeException("Take", "最大每页500项");
                _take = value;
            }
        }
    }

    /// <summary>纯粹的状态摘要，不持有原始Buffer；文件路径仅在授权可见范围内公开。</summary>
    public sealed class MDSDataSnapshot
    {
        public MDSDataSnapshot(string name, Type dataType, MDSDataLocation location,
            MDSDataLoadState loadState, string source, long? sizeBytes, long version,
            string runId = null, string scopeId = null)
        {
            Name = name;
            DataType = dataType;
            Location = location;
            LoadState = loadState;
            Source = source;
            SizeBytes = sizeBytes;
            Version = version;
            RunId = runId;
            ScopeId = scopeId;
        }
        public string Name { get; private set; }
        public Type DataType { get; private set; }
        public MDSDataLocation Location { get; private set; }
        public MDSDataLoadState LoadState { get; private set; }
        public string Source { get; private set; }
        public long? SizeBytes { get; private set; }
        public long Version { get; private set; }
        public string RunId { get; private set; }
        public string ScopeId { get; private set; }
    }

    internal static class MDSDataQueryLogic
    {
        internal static bool Matches(MDSDataSnapshot item, MDSDataQuery q)
        {
            if (q == null) return true;
            if (q.NamePrefix != null &&
                !item.Name.StartsWith(q.NamePrefix, StringComparison.Ordinal)) return false;
            if (q.DataType != null && !q.DataType.IsAssignableFrom(item.DataType)) return false;
            if (q.Location != MDSDataLocation.Any && item.Location != q.Location) return false;
            if (q.LoadState.HasValue && item.LoadState != q.LoadState.Value) return false;
            if (q.RunId != null && !string.Equals(q.RunId, item.RunId, StringComparison.Ordinal))
                return false;
            if (q.ScopeId != null && !string.Equals(q.ScopeId, item.ScopeId, StringComparison.Ordinal))
                return false;
            return true;
        }

        internal static IList<MDSDataSnapshot> Filter(List<MDSDataSnapshot> source, MDSDataQuery query)
        {
            MDSDataQuery q = query ?? new MDSDataQuery();
            if (q.Skip < 0) throw new ArgumentOutOfRangeException("Skip");
            source.Sort((a,b) =>
            {
                int location = a.Location.CompareTo(b.Location);
                return location != 0 ? location :
                    StringComparer.Ordinal.Compare(a.Name, b.Name);
            });
            List<MDSDataSnapshot> result = new List<MDSDataSnapshot>();
            int matched = 0;
            foreach (MDSDataSnapshot item in source)
            {
                if (!Matches(item, q)) continue;
                if (matched++ < q.Skip) continue;
                if (result.Count >= q.Take) break;
                result.Add(item);
            }
            return new ReadOnlyCollection<MDSDataSnapshot>(result);
        }

        internal static long? SizeOf(object value)
        {
            if (value is byte[]) return ((byte[])value).LongLength;
            var immutable = value as CxVisual.Foundation.Data.Types.FDTReadOnlyBuffer;
            if (immutable != null) return immutable.Length;
            return null;
        }
    }
}
