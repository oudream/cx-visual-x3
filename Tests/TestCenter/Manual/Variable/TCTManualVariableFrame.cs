using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CxVisual.Mechanism.Data.Variables;

namespace CxVisual.TestCenter.UI
{
    /// <summary>真实MDS变量及变化机制的人工工作台；不伪称连接真实PLC。</summary>
    public sealed class TCTManualVariableFrame : UserControl, ITCTWorkspaceFrame
    {
        private sealed class VariableRow
        {
            public string Path { get; set; }
            public string Id { get; set; }
            public string Current { get; set; }
            public string Previous { get; set; }
            public string DefaultValue { get; set; }
            public string Quality { get; set; }
            public string Revision { get; set; }
            public string RefreshTime { get; set; }
        }
        private readonly MDSVariableRegistry _registry = new MDSVariableRegistry();
        private readonly MDSVariableTable _variables = new MDSVariableTable("TestCenter.Variables");
        private readonly MDSVariableChangeDispatcher _dispatcher;
        private readonly MDSVariableSubscription _subscription;
        private readonly ObservableCollection<VariableRow> _rows = new ObservableCollection<VariableRow>();
        private readonly TreeView _tree = new TreeView { MinWidth = 215, Margin = new Thickness(4) };
        private readonly DataGrid _grid = new DataGrid { AutoGenerateColumns = true, IsReadOnly = true,
            SelectionMode = DataGridSelectionMode.Single, Margin = new Thickness(4) };
        private readonly TextBox _name = new TextBox { Text = "NewPressure", Width = 150, Margin = new Thickness(4) };
        private readonly TextBox _value = new TextBox { Text = "0.125", Width = 110, Margin = new Thickness(4) };
        private readonly TextBox _groupName = new TextBox { Text = "Station1", Width = 140, Margin = new Thickness(4) };
        private readonly TextBlock _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(5),
            Foreground = Brushes.SteelBlue, FontSize = 14 };
        private readonly TextBlock _metrics = new TextBlock { Margin = new Thickness(5),
            Foreground = Brushes.SlateGray, FontSize = 13 };
        private readonly Button _bench;
        private MDSVariableNode _selected;
        private bool _closed, _busy;
        private int _pendingUiRefresh;
        private long _observed;
        public string WorkspaceId { get { return "manual.variable"; } }
        public event Action StateChanged;
        public IEnumerable<TCTWorkspaceAction> Commands
        {
            get
            {
                return new[] {
                    new TCTWorkspaceAction("variable.define", "定义变量", Define, () => !_closed),
                    new TCTWorkspaceAction("variable.update", "更新变量", UpdateSelected, () => !_closed),
                    new TCTWorkspaceAction("variable.group", "创建分组", CreateGroup, () => !_closed),
                    new TCTWorkspaceAction("variable.move", "移动到分组", MoveSelected, () => !_closed),
                    new TCTWorkspaceAction("variable.refresh", "刷新表格", Refresh, () => !_closed),
                    new TCTWorkspaceAction("variable.benchmark", "执行50K热路径基准", () => { if (!_busy) Benchmark(); },
                        () => !_closed && !_busy)
                };
            }
        }

        public TCTManualVariableFrame()
        {
            _registry.Register(_variables);
            _variables.Define(new MDSVariableDefinition("Pressure", typeof(double), "MPa", true, 0.0, "pressure"));
            _variables.Define(new MDSVariableDefinition("Temperature", typeof(double), "C", true, 0.0, "temperature"));
            _variables.Define(new MDSVariableDefinition("Ready", typeof(bool), null, true, false, "ready"));
            _variables.Update<double>("Pressure", 0.12);
            _variables.Update<double>("Temperature", 26.5);
            _variables.Update<bool>("Ready", true);
            _dispatcher = _variables.CreateDispatcher(2048);
            _subscription = _dispatcher.Subscribe(OnChanges);
            _tree.SelectedItemChanged += (sender, args) => {
                TreeViewItem item = args.NewValue as TreeViewItem;
                _selected = item == null ? null : item.Tag as MDSVariableNode;
                if (_selected != null && _selected is MDSVariableLeaf) _name.Text = _selected.Name;
            };
            _grid.ItemsSource = _rows;
            _bench = Button("运行50K强类型更新", "TCT.Variable.Benchmark", Benchmark);
            Build();
            Refresh();
        }

        private static Button Button(string label, string id, Action action)
        {
            Button result = new Button { Content = label, Margin = new Thickness(4),
                Padding = new Thickness(10, 6), MinHeight = 32, FontSize = 13 };
            AutomationProperties.SetAutomationId(result, id);
            result.Click += (sender, args) => action();
            return result;
        }
        private static TextBlock Text(string value)
        {
            return new TextBlock { Text = value, Margin = new Thickness(6),
                VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.SlateGray };
        }
        private static StackPanel Horizontal()
        { return new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) }; }
        private void Build()
        {
            DockPanel root = new DockPanel();
            StackPanel top = new StackPanel { Margin = new Thickness(7) };
            top.Children.Add(new TextBlock { Text = "变量体系 · 真实MDS对象 / 变量组织树 / 变化派发 / Provider",
                FontSize = 19, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 5, 6, 8) });
            StackPanel inputs = Horizontal();
            inputs.Children.Add(Text("名称")); inputs.Children.Add(_name);
            inputs.Children.Add(Text("值")); inputs.Children.Add(_value);
            inputs.Children.Add(Button("定义Double", "TCT.Variable.Define", Define));
            inputs.Children.Add(Button("更新所选", "TCT.Variable.Update", UpdateSelected));
            inputs.Children.Add(Button("刷新", "TCT.Variable.Refresh", Refresh));
            top.Children.Add(inputs);
            StackPanel groups = Horizontal();
            groups.Children.Add(Text("组名")); groups.Children.Add(_groupName);
            groups.Children.Add(Button("建立分组", "TCT.Variable.Group", CreateGroup));
            groups.Children.Add(Button("将所选变量移入组", "TCT.Variable.Move", MoveSelected));
            groups.Children.Add(_bench);
            top.Children.Add(groups);
            top.Children.Add(_status); top.Children.Add(_metrics);
            DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(_tree, 0); Grid.SetColumn(_grid, 1);
            body.Children.Add(_tree); body.Children.Add(_grid);
            root.Children.Add(body);
            Content = root;
        }
        private TreeViewItem Item(MDSVariableNode node)
        {
            TreeViewItem item = new TreeViewItem { Header = node.Name + "  [" + node.VariableId + "]", Tag = node };
            MDSVariableGroup group = node as MDSVariableGroup;
            if (group != null)
                foreach (MDSVariableNode child in group.Children) item.Items.Add(Item(child));
            return item;
        }
        private void Refresh()
        {
            if (_closed) return;
            _tree.Items.Clear();
            foreach (MDSVariableNode child in _variables.Tree.Root.Children) _tree.Items.Add(Item(child));
            _rows.Clear();
            foreach (MDSVariableLeaf leaf in _variables.Tree.Snapshot().OfType<MDSVariableLeaf>())
            {
                MDSVariableDefinition def = leaf.Definition;
                string current = "未初始化", previous = "—", fallback = "—", quality = "—";
                string revision = "0", time = "—";
                if (def.ValueType == typeof(double))
                    Fill(leaf.Resolve<double>().Snapshot(), out current, out previous, out fallback, out quality, out revision, out time);
                else if (def.ValueType == typeof(bool))
                    Fill(leaf.Resolve<bool>().Snapshot(), out current, out previous, out fallback, out quality, out revision, out time);
                _rows.Add(new VariableRow { Path = leaf.VariablePath, Id = leaf.VariableId, Current = current,
                    Previous = previous, DefaultValue = fallback, Quality = quality, Revision = revision, RefreshTime = time });
            }
            MDSVariableDispatchMetrics metrics = _dispatcher.Metrics();
            _metrics.Text = "变化派发 Received=" + metrics.Received + " / Delivered=" + metrics.Delivered +
                " / Coalesced=" + metrics.Coalesced + " / Overflow=" + metrics.Overflow +
                " / SubscriberErrors=" + metrics.SubscriberFailures + " / UI观察批次=" + _observed;
            if (StateChanged != null) StateChanged();
        }
        private static void Fill<T>(MDSVariableSnapshot<T> s, out string now, out string prev,
            out string fallback, out string quality, out string revision, out string time)
        {
            now = s.HasValue ? Convert.ToString(s.CurrentValue) : "未初始化";
            prev = s.HasPreviousValue ? Convert.ToString(s.PreviousValue) : "—";
            fallback = s.HasDefaultValue ? Convert.ToString(s.DefaultValue) : "—";
            quality = s.Quality.ToString();
            revision = s.Revision.ToString();
            time = s.RefreshUtc == DateTime.MinValue ? "—" : s.RefreshUtc.ToString("HH:mm:ss.fff");
        }
        private void OnChanges(IReadOnlyList<MDSVariableChange> changes)
        {
            // 高频变量不直接生成高频WPF消息：同一时刻最多排队一次UI刷新。
            if (_closed || Interlocked.CompareExchange(ref _pendingUiRefresh, 1, 0) != 0) return;
            Dispatcher.BeginInvoke(new Action(() => {
                try
                {
                    if (_closed) return;
                    _observed++;
                    Refresh();
                }
                finally { Interlocked.Exchange(ref _pendingUiRefresh, 0); }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        private void Guard(Action execute)
        {
            if (_closed) return;
            try { execute(); Refresh(); }
            catch (Exception error) { _status.Text = error.GetType().Name + ": " + error.Message; }
        }
        private void Define()
        {
            Guard(() => {
                string name = _name.Text.Trim();
                _variables.Define(new MDSVariableDefinition(name, typeof(double)));
                _status.Text = "已定义变量：" + name;
            });
        }
        private void UpdateSelected()
        {
            Guard(() => {
                MDSVariableLeaf leaf = _selected as MDSVariableLeaf;
                if (leaf == null) throw new InvalidOperationException("请先选择变量");
                if (leaf.Definition.ValueType == typeof(double))
                    _variables.Update<double>(leaf.Definition.Name, double.Parse(_value.Text,
                        System.Globalization.CultureInfo.InvariantCulture));
                else if (leaf.Definition.ValueType == typeof(bool))
                    _variables.Update<bool>(leaf.Definition.Name, bool.Parse(_value.Text));
                else throw new NotSupportedException("本演示图窗仅提供Double/Bool编辑");
                _status.Text = "已更新：" + leaf.VariablePath;
            });
        }
        private void CreateGroup()
        {
            Guard(() => {
                MDSVariableGroup group = _variables.Tree.AddGroup(_groupName.Text.Trim());
                _status.Text = "新增组：" + group.VariablePath;
            });
        }
        private void MoveSelected()
        {
            Guard(() => {
                MDSVariableLeaf leaf = _selected as MDSVariableLeaf;
                if (leaf == null) throw new InvalidOperationException("请先选择变量");
                MDSVariableGroup group = _variables.Tree.Root.Children.OfType<MDSVariableGroup>()
                    .FirstOrDefault(x => x.Name == _groupName.Text.Trim());
                if (group == null) throw new InvalidOperationException("目标组不存在，请先新建");
                _variables.Tree.Move(leaf.VariableId, group.VariableId);
                _status.Text = "移动后路径：" + leaf.VariablePath;
            });
        }
        private async void Benchmark()
        {
            if (_busy || _closed) return;
            _busy = true; _bench.IsEnabled = false;
            _status.Text = "执行50,000次进程内Double变量更新（非设备性能认证）…";
            try
            {
                double seconds = await Task.Run(() => {
                    MDSVariable<double> variable = _variables.Resolve<double>("Pressure");
                    Stopwatch clock = Stopwatch.StartNew();
                    for (int i = 0; i < 50000; ++i) variable.Update(i * 0.0001);
                    clock.Stop();
                    return clock.Elapsed.TotalSeconds;
                });
                if (!_closed) _status.Text = "本机50K写入耗时：" + seconds.ToString("F3") +
                    " s；粗略吞吐：" + (50000.0 / Math.Max(seconds, 0.000001)).ToString("F0") +
                    " /s。非百万级持续性能验收。";
            }
            catch (Exception error) { if (!_closed) _status.Text = error.ToString(); }
            finally { _busy = false; _bench.IsEnabled = !_closed; if (!_closed) Refresh(); }
        }
        public void OnWorkspaceClosed()
        {
            if (_closed) return;
            _closed = true;
            _subscription.Dispose();
            _dispatcher.Dispose();
            _tree.Items.Clear(); _rows.Clear();
        }
    }
}
