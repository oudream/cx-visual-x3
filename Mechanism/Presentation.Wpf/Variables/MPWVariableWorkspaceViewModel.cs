using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CxVisual.Mechanism.Data.Variables;

namespace CxVisual.Mechanism.Presentation.Wpf.Variables
{
    public sealed class UVariableWatchViewModel
    {
        public string Path { get; set; }
        public string VariableId { get; set; }
        public string Type { get; set; }
        public string Current { get; set; }
        public string Previous { get; set; }
        public string Default { get; set; }
        public string Quality { get; set; }
        public string Source { get; set; }
        public string Revision { get; set; }
        public string LastRefresh { get; set; }
    }

    public sealed class UVariableChangesViewModel
    {
        public string VariableId { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Revision { get; set; }
        public string Time { get; set; }
        public string Current { get; set; }
    }

    public sealed class UVariableProviderViewModel
    {
        public string Provider { get; set; }
        public string Point { get; set; }
        public string Scope { get; set; }
        public string Variable { get; set; }
    }

    /// <summary>
    /// WPF只读投影：直接读MDS唯一Owner，订阅后台变化不触及UI线程；
    /// 只有外部可信宿主选择开启运行态写入，才会允许编辑动作。
    /// </summary>
    public sealed class MPWVariableWorkspaceViewModel : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Queue<MDSVariableChange> _pending = new Queue<MDSVariableChange>();
        private MDSVariableTable _table;
        private MDSVariableProviderHost _host;
        private MDSVariableChangeDispatcher _dispatcher;
        private MDSVariableSubscription _subscription;
        private long _dropped;
        public ObservableCollection<UVariableWatchViewModel> WatchRows { get; private set; } =
            new ObservableCollection<UVariableWatchViewModel>();
        public ObservableCollection<UVariableChangesViewModel> ChangeRows { get; private set; } =
            new ObservableCollection<UVariableChangesViewModel>();
        public ObservableCollection<UVariableProviderViewModel> ProviderRows { get; private set; } =
            new ObservableCollection<UVariableProviderViewModel>();

        public MDSVariableTable Table { get { return _table; } }
        public bool IsBound { get { return _table != null; } }
        public string ScopeId { get { return _table == null ? null : _table.ScopeId; } }
        public long DroppedUiHistory { get { lock (_gate) return _dropped; } }

        public void Bind(MDSVariableTable table, MDSVariableProviderHost host = null)
        {
            if (table == null) throw new ArgumentNullException("table");
            Dispose();
            _table = table;
            _host = host;
            _dispatcher = table.CreateDispatcher(1024);
            _subscription = _dispatcher.Subscribe(changes =>
            {
                lock (_gate)
                {
                    foreach (MDSVariableChange change in changes)
                    {
                        if (_pending.Count >= 256) { _pending.Dequeue(); _dropped++; }
                        _pending.Enqueue(change);
                    }
                }
            });
            Refresh(null);
        }

        public MDSVariableNode[] Roots()
        {
            return _table == null ? new MDSVariableNode[0] : _table.Tree.Root.Children;
        }

        public void Refresh(string filter)
        {
            WatchRows.Clear();
            ProviderRows.Clear();
            if (_table == null) return;
            IEnumerable<MDSVariableDefinition> defs = _table.Definitions()
                .OrderBy(x => x.Name, StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(filter))
            {
                string term = filter.Trim();
                defs = defs.Where(x => x.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    x.VariableId.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            foreach (MDSVariableDefinition definition in defs.Take(350))
            {
                IMDSVariableSnapshot sample;
                try { sample = _table.Inspect(definition.Name); }
                catch (InvalidOperationException) { continue; }
                MDSVariableNode node = _table.Tree.FindById(definition.VariableId);
                WatchRows.Add(new UVariableWatchViewModel
                {
                    Path = node == null ? definition.Name : node.VariablePath,
                    VariableId = definition.VariableId, Type = definition.ValueType.Name,
                    Current = sample.HasValue ? Format(sample.CurrentObject) : "未初始化",
                    Previous = sample.HasPreviousValue ? Format(sample.PreviousObject) : "—",
                    Default = sample.HasDefaultValue ? Format(sample.DefaultObject) : "—",
                    Quality = sample.Quality.ToString(), Source = sample.Source ?? "—",
                    Revision = sample.Revision.ToString(CultureInfo.InvariantCulture),
                    LastRefresh = sample.RefreshUtc == DateTime.MinValue ? "—" :
                        sample.RefreshUtc.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)
                });
            }
            if (_host != null)
                foreach (MDSVariableProviderBinding bind in _host.Bindings().Take(350))
                    ProviderRows.Add(new UVariableProviderViewModel {
                        Provider = bind.ProviderId, Point = bind.PointId,
                        Scope = bind.ScopeId, Variable = bind.VariableName
                    });
            MDSVariableChange[] changed;
            lock (_gate) { changed = _pending.ToArray(); _pending.Clear(); }
            foreach (MDSVariableChange change in changed)
            {
                ChangeRows.Insert(0, new UVariableChangesViewModel {
                    VariableId = change.VariableId, Name = change.Name, Kind = change.Kind.ToString(),
                    Revision = change.Revision.ToString(CultureInfo.InvariantCulture),
                    Time = change.UpdatedUtc.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    Current = Format(change.CurrentValue)
                });
            }
            while (ChangeRows.Count > 350) ChangeRows.RemoveAt(ChangeRows.Count - 1);
        }
        private static string Format(object value)
        {
            if (value == null) return "null";
            byte[] bytes = value as byte[];
            if (bytes != null) return "byte[" + bytes.Length.ToString(CultureInfo.InvariantCulture) + "]";
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            return text.Length > 120 ? text.Substring(0, 117) + "..." : text;
        }
        public string DispatchSummary()
        {
            if (_dispatcher == null) return "未绑定运行态Scope";
            MDSVariableDispatchMetrics m = _dispatcher.Metrics();
            return "Scope " + ScopeId + " | Received " + m.Received + " | Delivered " + m.Delivered +
                " | Coalesced " + m.Coalesced + " | Overflow " + m.Overflow +
                " | Stale " + m.StaleRejected + " | UI历史覆盖 " + DroppedUiHistory;
        }
        public void Dispose()
        {
            if (_subscription != null) { _subscription.Dispose(); _subscription = null; }
            if (_dispatcher != null) { _dispatcher.Dispose(); _dispatcher = null; }
            _table = null; _host = null;
            lock (_gate) { _pending.Clear(); _dropped = 0; }
            WatchRows.Clear(); ChangeRows.Clear(); ProviderRows.Clear();
        }
    }
}
