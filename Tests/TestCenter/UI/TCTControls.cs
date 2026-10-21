using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Models;

namespace CxVisual.TestCenter.UI
{
    public sealed class TCTTestExplorerControl : UserControl
    {
        private readonly TextBox _search = new TextBox { Margin = new Thickness(7, 5, 7, 7),
            Padding = new Thickness(7, 5, 7, 5), MinHeight = 31,
            BorderBrush = new SolidColorBrush(Color.FromRgb(195, 215, 235)),
            ToolTip = "按ID/名称/能力检索" };
        private readonly TreeView _tree = new TreeView { Margin = new Thickness(5) };
        private TCTTestCatalog _catalog;
        private readonly HashSet<string> _checkedIds = new HashSet<string>(StringComparer.Ordinal);
        public string[] CheckedIds { get { return _checkedIds.OrderBy(x => x, StringComparer.Ordinal).ToArray(); } }
        public event Action<ITCTTestCase> TestSelected;
        public ITCTTestCase SelectedTest { get; private set; }
        public TCTTestExplorerControl()
        {
            System.Windows.Automation.AutomationProperties.SetAutomationId(_search, "TCT.Explorer.Search");
            DockPanel panel = new DockPanel { Background = Brushes.White };
            StackPanel searchPanel = new StackPanel { Background =
                new SolidColorBrush(Color.FromRgb(246, 250, 254)) };
            searchPanel.Children.Add(new TextBlock {
                Text = "搜索测试 / Capability / ObjectId", FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(108, 131, 156)),
                Margin = new Thickness(9, 9, 0, 0) });
            searchPanel.Children.Add(_search);
            DockPanel.SetDock(searchPanel, Dock.Top);
            panel.Children.Add(searchPanel);
            panel.Children.Add(_tree);
            Content = panel;
            _search.TextChanged += (s, e) => Refresh();
            _tree.SelectedItemChanged += (s, e) =>
            {
                TreeViewItem item = _tree.SelectedItem as TreeViewItem;
                SelectedTest = item == null ? null : item.Tag as ITCTTestCase;
                if (TestSelected != null) TestSelected(SelectedTest);
            };
        }
        public void Bind(TCTTestCatalog catalog) { _catalog = catalog; Refresh(); }
        private void Refresh()
        {
            _tree.Items.Clear();
            SelectedTest = null;
            if (_catalog == null) return;
            string filter = _search.Text.Trim();
            foreach (TCTTestAspect aspect in new[] { TCTTestAspect.Foundation, TCTTestAspect.Mechanism, TCTTestAspect.Integration })
            {
                var items = _catalog.Cases.Where(t => t.Descriptor.Aspect == aspect &&
                    (filter.Length == 0 ||
                     t.Descriptor.Id.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     t.Descriptor.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     t.Descriptor.CapabilityAreaId.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     t.Descriptor.Group.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                     .GroupBy(t => t.Descriptor.Group).ToList();
                TreeViewItem root = new TreeViewItem { Header = aspect.ToString(), IsExpanded = true };
                foreach (var group in items)
                {
                    TreeViewItem category = new TreeViewItem { Header = group.Key, IsExpanded = true };
                    foreach (ITCTTestCase test in group)
                    {
                        string id = test.Descriptor.Id;
                        CheckBox check = new CheckBox
                        {
                            Content = test.Descriptor.Name, IsChecked = _checkedIds.Contains(id),
                            ToolTip = id + " / " + test.Descriptor.CapabilityId,
                            Margin = new Thickness(0, 2, 0, 2)
                        };
                        check.Checked += (sender, args) => _checkedIds.Add(id);
                        check.Unchecked += (sender, args) => _checkedIds.Remove(id);
                        TreeViewItem entry = new TreeViewItem { Header = check, Tag = test,
                            ToolTip = test.Descriptor.Id + " / " + test.Descriptor.CapabilityId };
                        System.Windows.Automation.AutomationProperties.SetAutomationId(entry,
                            "TCT.Case." + id);
                        category.Items.Add(entry);
                    }
                    root.Items.Add(category);
                }
                if (root.Items.Count > 0) _tree.Items.Add(root);
            }
        }
    }

    public sealed class TCTTestInspectorControl : UserControl
    {
        private readonly TextBlock _metadata = new TextBlock { Margin = new Thickness(10),
            TextWrapping = TextWrapping.Wrap, LineHeight = 24 };
        private readonly DataGrid _assertions = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false,
            CanUserAddRows = false, Margin = new Thickness(3) };
        private readonly TextBlock _source = new TextBlock { Margin = new Thickness(12),
            TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas") };
        private readonly DataGrid _artifacts = new DataGrid { IsReadOnly = true,
            AutoGenerateColumns = true, CanUserAddRows = false };
        private TCTTestSourceLocation _currentSource;
        private readonly Button _openSource = new Button {
            Content = "跳转源码（若本机存在）", Margin = new Thickness(8), Padding = new Thickness(8, 4, 8, 4) };
        private readonly TabControl _tabs = new TabControl();
        private readonly TabItem _observationTab = new TabItem { Header = "观察Owner" };
        private readonly TabItem _objectTab = new TabItem { Header = "工程对象" };
        private readonly TextBlock _objectDetail = new TextBlock
        {
            Margin = new Thickness(12), FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap
        };
        public string SelectedObjectId { get; private set; }
        public string SelectedObjectDetails { get; private set; }
        private readonly TextBlock _observation = new TextBlock { Margin = new Thickness(12),
            FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap };
        // 供WPF Smoke核实真实选择链，不通过UI推导Pass/Fail。
        public string SelectedObservationTitle { get; private set; }
        private readonly TextBlock _error = new TextBlock { Margin = new Thickness(12),
            FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap };
        public TCTTestInspectorControl()
        {
            _assertions.Columns.Add(new DataGridTextColumn { Header = "断言", Binding = new Binding("Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _assertions.Columns.Add(new DataGridTextColumn { Header = "Expected", Binding = new Binding("Expected") });
            _assertions.Columns.Add(new DataGridTextColumn { Header = "Actual", Binding = new Binding("Actual") });
            _assertions.Columns.Add(new DataGridCheckBoxColumn { Header = "Pass", Binding = new Binding("Passed") });
            TabControl tabs = _tabs;
            tabs.Items.Add(new TabItem { Header = "属性", Content = new ScrollViewer { Content = _metadata } });
            tabs.Items.Add(new TabItem { Header = "断言", Content = _assertions });
            tabs.Items.Add(new TabItem { Header = "异常/Trace", Content = new ScrollViewer { Content = _error } });
            DockPanel sourcePanel = new DockPanel();
            DockPanel.SetDock(_openSource, Dock.Top);
            sourcePanel.Children.Add(_openSource);
            sourcePanel.Children.Add(new ScrollViewer { Content = _source });
            _openSource.IsEnabled = false;
            _openSource.Click += (s, e) =>
            {
                if (_currentSource == null || !System.IO.File.Exists(_currentSource.File)) return;
                try
                {
                    System.Diagnostics.Process.Start("code",
                        "--goto \"" + _currentSource.File + ":" + _currentSource.Line + "\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("无法自动打开编辑器，源码位置：" + _currentSource.File +
                        ":" + _currentSource.Line + "\n" + ex.Message, "TestCenter源码定位");
                }
            };
            tabs.Items.Add(new TabItem { Header = "源码定位", Content = sourcePanel });
            tabs.Items.Add(new TabItem { Header = "证据产物", Content = _artifacts });
            _observationTab.Content = new ScrollViewer { Content = _observation };
            tabs.Items.Add(_observationTab);
            _objectTab.Content = new ScrollViewer { Content = _objectDetail };
            tabs.Items.Add(_objectTab);
            Content = tabs;
        }
        public void ShowCase(ITCTTestCase test)
        {
            _assertions.ItemsSource = null;
            _artifacts.ItemsSource = null;
            SelectedObservationTitle = null;
            SelectedObjectId = null;
            SelectedObjectDetails = null;
            _objectDetail.Text = "选择真实工程快照中的节点或引用以查看稳定身份。";
            _tabs.SelectedIndex = 0;
            _observation.Text = "请选择真实观察项以查看Owner、身份与证据引用。";
            _currentSource = null; _openSource.IsEnabled = false;
            _source.Text = "尚无PDB源码位置；绝不猜测源码行号。";
            _error.Text = "未运行";
            if (test == null) { _metadata.Text = "请选择测试用例"; return; }
            TCTTestCaseDescriptor d = test.Descriptor;
            _metadata.Text = "Test ID: " + d.Id + "\n名称: " + d.Name +
                "\n方面: " + d.Aspect + "\n组: " + d.Group +
                "\n能力方面: " + d.CapabilityAreaId +
                "\n能力: " + d.CapabilityId + "\n对象: " + d.ObjectId +
                "\n当前状态: NotRun";
        }
        public void ShowResult(TCTTestCaseResult result)
        {
            if (result == null) return;
            SelectedObservationTitle = null;
            SelectedObjectId = null;
            SelectedObjectDetails = null;
            _objectDetail.Text = "选择真实工程快照中的节点或引用以查看稳定身份。";
            _tabs.SelectedIndex = 0;
            _observation.Text = "本Attempt有 " + result.Observations.Count +
                " 条观察。请在专业Viewer点击观察项，查看真实Owner和SHA256。";
            TCTTestCaseDescriptor d = result.Descriptor;
            _metadata.Text = "Test ID: " + d.Id + "\n状态: " + result.Status +
                "\n耗时: " + result.Elapsed.TotalMilliseconds.ToString("F2") +
                " ms\nRunId: " + result.RunId + "\nAttemptId: " + result.AttemptId +
                "\n方面: " + d.Aspect + "\n组: " + d.Group +
                "\n能力方面: " + d.CapabilityAreaId +
                "\n能力: " + d.CapabilityId + "\n对象: " + d.ObjectId +
                "\n断言: " + result.Assertions.Count + " 项" +
                "\n诊断: " + result.Diagnostics.Count + " 项" +
                "\n产物: " + result.Artifacts.Count + " 项";
            _assertions.ItemsSource = result.Assertions;
            _artifacts.ItemsSource = result.Artifacts;
            _currentSource = result.SourceLocation;
            _openSource.IsEnabled = _currentSource != null &&
                System.IO.File.Exists(_currentSource.File);
            _source.Text = _currentSource == null ?
                "该结果未包含可证实的PDB源文件与行号。可查看原始堆栈与诊断。" :
                "真实来源: " + _currentSource.Method +
                "\n文件: " + _currentSource.File + "\n行号: " + _currentSource.Line +
                (_openSource.IsEnabled ? "\n点击按钮跳转到VS Code" :
                    "\n源码不在本机，无法自动打开；不会跳到错误位置。");
            _error.Text = (string.IsNullOrEmpty(result.Error) ? "无异常" : result.Error) +
                string.Join("", result.Diagnostics.Select(diag =>
                    "\n[" + diag.Severity + "] " + diag.Code + " @" + diag.Stage + ": " + diag.Message)) +
                string.Join("", result.Artifacts.Select(a =>
                    "\n[Artifact] " + a.RelativePath + " SHA256=" + a.Sha256));
        }

        public void FocusAssertions() { _tabs.SelectedIndex = 1; }
        public void FocusSource() { _tabs.SelectedIndex = 3; }
        public void ShowObject(string objectId, string verifiedDetails)
        {
            SelectedObjectId = objectId;
            SelectedObjectDetails = string.IsNullOrWhiteSpace(objectId) ?
                "未选择工程对象。" : (verifiedDetails ?? objectId);
            _objectDetail.Text = SelectedObjectDetails;
            _tabs.SelectedItem = _objectTab;
        }

        public void ShowObservation(TCTTestObservation observation)
        {
            if (observation == null)
            {
                SelectedObservationTitle = null;
                _observation.Text = "没有选择观察项。";
                return;
            }
            SelectedObservationTitle = observation.Title;
            _tabs.SelectedItem = _observationTab;
            TCTObservationEnvelope e = observation.Envelope;
            _observation.Text = "Kind: " + observation.Kind +
                "\nTitle: " + observation.Title +
                "\nOrigin: " + observation.Origin +
                "\nDetails: " + observation.Details +
                (e == null ? "\nLegacy: 无结构化Owner身份（不能推断实例或伪造证据）" :
                    "\nSchema: " + e.SchemaVersion +
                    "\nRunId: " + e.RunId +
                    "\nAttemptId: " + e.AttemptId +
                    "\nSourceObjectId: " + e.SourceObjectId +
                    "\nDefinitionAddress: " + e.DefinitionAddress +
                    "\nRuntimeInstanceId: " + (string.IsNullOrEmpty(e.RuntimeInstanceId) ? "未提供" : e.RuntimeInstanceId) +
                    "\nPayload SHA256: " + (e.PayloadRef == null ? "无" : e.PayloadRef.Sha256)) +
                "\nEvidence SHA256: " +
                    (observation.Evidence == null ? "无" : observation.Evidence.Sha256);
        }
    }

    public sealed class TCTTestResultsControl : UserControl
    {
        private readonly ObservableCollection<TCTTestCaseResult> _results =
            new ObservableCollection<TCTTestCaseResult>();
        private readonly DataGrid _grid = new DataGrid { AutoGenerateColumns = false,
            IsReadOnly = true, CanUserAddRows = false };
        public event Action<TCTTestCaseResult> ResultSelected;
        public TCTTestResultsControl()
        {
            _grid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding("Descriptor.Id"), Width = new DataGridLength(165) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "测试用例", Binding = new Binding("Descriptor.Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Attempt", Binding = new Binding("AttemptId"), Width = new DataGridLength(125) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "结果", Binding = new Binding("Status"), Width = new DataGridLength(95) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "毫秒", Binding = new Binding("Elapsed.TotalMilliseconds") { StringFormat = "F2" }, Width = new DataGridLength(90) });
            _grid.Columns.Add(new DataGridTextColumn { Header = "诊断", Binding = new Binding("Error"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _grid.SelectionChanged += (s, e) =>
            {
                if (ResultSelected != null) ResultSelected(_grid.SelectedItem as TCTTestCaseResult);
            };
            _grid.ItemsSource = _results; Content = _grid;
        }
        public int ResultCount { get { return _results.Count; } }
        public void Clear() { _results.Clear(); }
        public void Add(TCTTestCaseResult result)
        {
            _results.Add(result);
            _grid.SelectedItem = result;
            _grid.ScrollIntoView(result);
        }
    }

    // 统一工具栏的命令出口；上下文工具随选中方面切换，不复制多套Ribbon。
    public sealed class TCTTestRibbonController
    {
        public Button RunAll { get; private set; }
        public Button RunSelected { get; private set; }
        public Button Cancel { get; private set; }
        public Button Export { get; private set; }
        public Button RerunFailed { get; private set; }
        public Button CopyTestId { get; private set; }
        public ComboBox ModeSelector { get; private set; }
        private ToolBar _contextActions;
        private ITCTTestCase _selectedContext;
        private Action<string> _showContextMode;
        private Action _showAssertions, _showSource, _showHistory, _showDiff;
        private readonly List<string> _activeContextCommands = new List<string>();
        public IReadOnlyList<string> ActiveContextCommands
        { get { return _activeContextCommands.AsReadOnly(); } }
        private readonly TextBlock _context = new TextBlock { VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0), Foreground = Brushes.SteelBlue };
        public ToolBar Build(Action runAll, Action runSelected, Action cancel, Action export,
            Action rerunFailed = null, Action copyTestId = null,
            Action<string> showMode = null)
        {
            ToolBar ribbon = new ToolBar { Background = Brushes.White, Band = 0, BandIndex = 0, Padding = new Thickness(5, 3, 5, 3) };
            RunAll = Create("▶ 运行全部", runAll);
            RunSelected = Create("▶ 运行选中", runSelected);
            System.Windows.Automation.AutomationProperties.SetAutomationId(RunSelected, "TCT.RunSelected");
            Cancel = Create("■ 取消", cancel); Cancel.IsEnabled = false;
            Export = Create("导出报告", export);
            ribbon.Items.Add(RunAll); ribbon.Items.Add(RunSelected);
            ribbon.Items.Add(Cancel); ribbon.Items.Add(Export);
            if (rerunFailed != null)
            {
                RerunFailed = Create("↻ 重跑未通过", rerunFailed);
                ribbon.Items.Add(RerunFailed);
            }
            if (copyTestId != null)
            {
                CopyTestId = Create("复制TestId", copyTestId);
                CopyTestId.IsEnabled = false;
                ribbon.Items.Add(CopyTestId);
            }
            if (showMode != null)
            {
                ribbon.Items.Add(new Separator());
                ribbon.Items.Add(new TextBlock { Text = "场景", Margin = new Thickness(8, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center });
                ModeSelector = new ComboBox
                {
                    Width = 160, MinHeight = 27, Margin = new Thickness(0, 0, 4, 0),
                    ToolTip = "六场景共用TestCenter，不存在真实Owner证据时显示NoEvidence"
                };
                foreach (string mode in new[] { "概览", "Geometry", "ObjectTree", "Graph", "Channel", "Runtime" })
                    ModeSelector.Items.Add(mode);
                ModeSelector.SelectedIndex = 0;
                ModeSelector.SelectionChanged += (s, e) =>
                {
                    if (ModeSelector.SelectedItem is string)
                        showMode((string)ModeSelector.SelectedItem);
                };
                ribbon.Items.Add(ModeSelector);
            }
            ribbon.Items.Add(_context);
            return ribbon;
        }
        /// <summary>Actual contextual commands; their visibility follows the selected Catalog case.</summary>
        public ToolBar BuildContext(Action<string> showMode, Action showAssertions,
            Action showSource, Action showHistory, Action showDiff)
        {
            _showContextMode = showMode ?? throw new ArgumentNullException("showMode");
            _showAssertions = showAssertions ?? throw new ArgumentNullException("showAssertions");
            _showSource = showSource ?? throw new ArgumentNullException("showSource");
            _showHistory = showHistory ?? throw new ArgumentNullException("showHistory");
            _showDiff = showDiff ?? throw new ArgumentNullException("showDiff");
            _contextActions = new ToolBar { Background = new SolidColorBrush(Color.FromRgb(242, 248, 254)),
                Band = 1, BandIndex = 0, Padding = new Thickness(5, 1, 5, 1) };
            RebuildContextCommands();
            return _contextActions;
        }
        private void AddContextCommand(string id, string text, Action action)
        {
            Button button = Create(text, action, true);
            System.Windows.Automation.AutomationProperties.SetAutomationId(button, "TCT.Context." + id);
            _contextActions.Items.Add(button);
            _activeContextCommands.Add(id);
        }
        private void RebuildContextCommands()
        {
            if (_contextActions == null) return;
            _contextActions.Items.Clear();
            _activeContextCommands.Clear();
            ITCTTestCase test = _selectedContext;
            if (test == null)
            {
                _contextActions.Items.Add(new TextBlock { Text = "选择用例后显示Geometry/Object等上下文命令",
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) });
                return;
            }
            string group = test.Descriptor.Group ?? "";
            bool geometry = group.IndexOf("Geometry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                group.IndexOf("Visual", StringComparison.OrdinalIgnoreCase) >= 0;
            bool objects = group.IndexOf("Object", StringComparison.OrdinalIgnoreCase) >= 0 ||
                group.IndexOf("Ownership", StringComparison.OrdinalIgnoreCase) >= 0 ||
                group.IndexOf("Symbol", StringComparison.OrdinalIgnoreCase) >= 0 ||
                group.IndexOf("Visual", StringComparison.OrdinalIgnoreCase) >= 0;
            if (geometry)
                AddContextCommand("Geometry", "Geometry证据", () => _showContextMode("Geometry"));
            if (objects)
                AddContextCommand("Object", "Object/Symbol", () => _showContextMode("ObjectTree"));
            AddContextCommand("Assertions", "定位断言", _showAssertions);
            AddContextCommand("Source", "源码", _showSource);
            AddContextCommand("History", "历史", _showHistory);
            AddContextCommand("Diff", "证据Diff", _showDiff);
        }
        public void SetContext(ITCTTestCase test)
        {
            _selectedContext = test;
            if (CopyTestId != null) CopyTestId.IsEnabled = test != null;
            _context.Text = test == null ? "Foundation / Mechanism" :
                test.Descriptor.Aspect + "  ›  " + test.Descriptor.Group;
            RebuildContextCommands();
        }
        public void SetRunning(bool running)
        {
            RunAll.IsEnabled = !running; RunSelected.IsEnabled = !running;
            Cancel.IsEnabled = running; Export.IsEnabled = !running;
            if (_contextActions != null) _contextActions.IsEnabled = !running;
            if (RerunFailed != null) RerunFailed.IsEnabled = !running;
        }
        private static Button Create(string text, Action action, bool compact = false)
        {
            Button button = new Button {
                MinWidth = compact ? 64 : 78, Height = compact ? 31 : 65,
                Margin = new Thickness(3, 2, 3, 2),
                Padding = compact ? new Thickness(8, 3, 8, 3) : new Thickness(8, 5, 8, 5),
                Foreground = new SolidColorBrush(Color.FromRgb(22, 105, 194)),
                Background = new SolidColorBrush(Color.FromRgb(248, 251, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(199, 217, 237)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand };
            System.Windows.Automation.AutomationProperties.SetName(button, text);
            if (compact)
                button.Content = text;
            else
            {
                string icon = "▣", caption = text;
                if (text.StartsWith("▶ ", StringComparison.Ordinal))
                { icon = "▶"; caption = text.Substring(2); }
                else if (text.StartsWith("■ ", StringComparison.Ordinal))
                { icon = "■"; caption = text.Substring(2); }
                else if (text.StartsWith("↻ ", StringComparison.Ordinal))
                { icon = "↻"; caption = text.Substring(2); }
                else if (text.Contains("报告")) icon = "▤";
                else if (text.Contains("复制")) icon = "◇";
                StackPanel vertical = new StackPanel {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center };
                vertical.Children.Add(new TextBlock {
                    Text = icon, FontSize = 20, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(23, 119, 213)),
                    HorizontalAlignment = HorizontalAlignment.Center });
                vertical.Children.Add(new TextBlock {
                    Text = caption, FontSize = 11, Foreground =
                        new SolidColorBrush(Color.FromRgb(26, 64, 107)),
                    HorizontalAlignment = HorizontalAlignment.Center });
                button.Content = vertical;
            }
            button.Click += (s, e) => action();
            return button;
        }
    }
}
