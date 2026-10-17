using System;
using System.Collections.Generic;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    public sealed partial class MDSDataHeap
    {
        /// <summary>原始元信息快照。只从当前Heap的真实条目投影，不生成第二份数据真源。</summary>
        internal void AppendSnapshots(List<MDSDataSnapshot> results,
            MDSDataQuery query = null)
        {
            if (results == null) throw new ArgumentNullException("results");
            lock (_gate)
            {
                EnsureOpen();
                foreach (KeyValuePair<string, MDSDataEntry> pair in _values)
                {
                    if (query != null && query.ExactName != null &&
                        !string.Equals(pair.Key, query.ExactName, StringComparison.Ordinal)) continue;
                    if (query != null && query.NamePrefix != null &&
                        !pair.Key.StartsWith(query.NamePrefix, StringComparison.Ordinal)) continue;
                    var item = new MDSDataSnapshot(pair.Key, pair.Value.Type,
                        MDSDataLocation.FlowHeap, MDSDataLoadState.NotFile,
                        null, MDSDataQueryLogic.SizeOf(pair.Value.Value), 1, RunId, "FlowHeap");
                    if (MDSDataQueryLogic.Matches(item, query)) results.Add(item);
                }
                foreach (KeyValuePair<string, MDSDataFileEntry> pair in _files)
                {
                    if (query != null && query.ExactName != null &&
                        !string.Equals(pair.Key, query.ExactName, StringComparison.Ordinal)) continue;
                    if (query != null && query.NamePrefix != null &&
                        !pair.Key.StartsWith(query.NamePrefix, StringComparison.Ordinal)) continue;
                    var item = new MDSDataSnapshot(pair.Key, pair.Value.Type,
                        MDSDataLocation.FlowHeap, pair.Value.State, pair.Value.Path,
                        pair.Value.State == MDSDataLoadState.Loaded
                            ? MDSDataQueryLogic.SizeOf(pair.Value.Cache)
                            : (long?)pair.Value.Length,
                        pair.Value.Generation, RunId, "FlowHeap");
                    if (MDSDataQueryLogic.Matches(item, query)) results.Add(item);
                }
            }
        }

        public IList<MDSDataSnapshot> Query(MDSDataQuery query = null)
        {
            List<MDSDataSnapshot> current = new List<MDSDataSnapshot>();
            AppendSnapshots(current, query);
            return MDSDataQueryLogic.Filter(current, query);
        }

        public MDSDataSnapshot Inspect(string name)
        {
            CheckName(name);
            lock (_gate)
            {
                EnsureOpen();
                MDSDataEntry value;
                if (_values.TryGetValue(name, out value))
                    return new MDSDataSnapshot(name, value.Type, MDSDataLocation.FlowHeap,
                        MDSDataLoadState.NotFile, null, MDSDataQueryLogic.SizeOf(value.Value),
                        1, RunId, "FlowHeap");
                MDSDataFileEntry file;
                if (_files.TryGetValue(name, out file))
                    return new MDSDataSnapshot(name, file.Type, MDSDataLocation.FlowHeap,
                        file.State, file.Path, file.State == MDSDataLoadState.Loaded
                            ? MDSDataQueryLogic.SizeOf(file.Cache) : (long?)file.Length,
                        file.Generation, RunId, "FlowHeap");
                throw new KeyNotFoundException("找不到数据：" + name);
            }
        }
    }
}
