using System;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    public sealed partial class MDSDataHeap
    {
        /// <summary>原始元信息快照。只从当前Heap的真实条目投影，不生成第二份数据真源。</summary>
        internal void AppendSnapshots(List<MDSDataSnapshot> results)
        {
            if (results == null) throw new ArgumentNullException("results");
            lock (_gate)
            {
                EnsureOpen();
                foreach (KeyValuePair<string, MDSDataEntry> pair in _values)
                    results.Add(new MDSDataSnapshot(pair.Key, pair.Value.Type,
                        MDSDataLocation.FlowHeap, MDSDataLoadState.NotFile,
                        null, MDSDataQueryLogic.SizeOf(pair.Value.Value), 1));
                foreach (KeyValuePair<string, MDSDataFileEntry> pair in _files)
                    results.Add(new MDSDataSnapshot(pair.Key, pair.Value.Type,
                        MDSDataLocation.FlowHeap, pair.Value.State,
                        pair.Value.Path,
                        pair.Value.State == MDSDataLoadState.Loaded
                            ? MDSDataQueryLogic.SizeOf(pair.Value.Cache)
                            : (long?)pair.Value.Length,
                        pair.Value.Generation));
            }
        }

        public IList<MDSDataSnapshot> Query(MDSDataQuery query = null)
        {
            List<MDSDataSnapshot> current = new List<MDSDataSnapshot>();
            AppendSnapshots(current);
            return MDSDataQueryLogic.Filter(current, query);
        }

        public MDSDataSnapshot Inspect(string name)
        {
            CheckName(name);
            IList<MDSDataSnapshot> candidates = Query(new MDSDataQuery {
                NamePrefix = name, Take = 500
            });
            foreach (MDSDataSnapshot snapshot in candidates)
                if (string.Equals(snapshot.Name, name, StringComparison.Ordinal))
                    return snapshot;
            throw new KeyNotFoundException("找不到数据：" + name);
        }
    }
}
