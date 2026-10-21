using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// TestCenter首页。只读取Discovery和真实Attempt；效果图中的示意指标不得成为默认值。
    /// 此处不持有工程定义，也不执行任何被测Owner代码。
    /// </summary>
    public sealed class TCTTestOverviewControl : UserControl
    {
        private static readonly Brush Navy = Brush("#183C68");
        private static readonly Brush Blue = Brush("#1976D2");
        private static readonly Brush Green = Brush("#16976C");
        private static readonly Brush Red = Brush("#D84B58");
        private static readonly Brush Amber = Brush("#CE8A16");
        private readonly TextBlock _discovered = MetricValue("TCT.Overview.Discovered");
        private readonly TextBlock _passed = MetricValue("TCT.Overview.Passed");
        private readonly TextBlock _failed = MetricValue("TCT.Overview.Failed");
        private readonly TextBlock _pending = MetricValue("TCT.Overview.Pending");
        private readonly TextBlock _runCaption = new TextBlock { Foreground = Brush("#687F9D"), Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
        private readonly StackPanel _aspectRows = new StackPanel();
        private readonly DataGrid _recent = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            CanUserDeleteRows = false, CanUserResizeRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            AlternatingRowBackground = Brush("#F2F7FD"),
            RowHeight = 31, MinHeight = 170, BorderThickness = new Thickness(0)
        };
        private TCTTestCatalog _catalog;
        private IReadOnlyList<TCTTestCaseResult> _attempts = new List<TCTTestCaseResult>();
        private string _runId;
        public int DiscoveredCount { get; private set; }
        public int DisplayedAttemptCount { get { return _attempts.Count; } }
        public event Action RunSelectedRequested;
        public event Action RunAllRequested;
        public event Action HistoryRequested;
        public event Action ReportRequested;

        public TCTTestOverviewControl()
        {
            Background = Brushes.White;
            ScrollViewer scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            StackPanel content = new StackPanel { Margin = new Thickness(24, 20, 24, 25) };
            content.Children.Add(new TextBlock {
                Text = "CxVisual 平台基础与机制测试中心", FontWeight = FontWeights.Bold,
                FontSize = 25, Foreground = Navy });
            content.Children.Add(new TextBlock {
                Text = "按 Capability → Test Case → 真实 Owner 执行 → Assertion → Evidence 组织验证",
                FontSize = 13, Foreground = Brush("#627D9D"), Margin = new Thickness(0, 5, 0, 18) });
            UniformGrid actions = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 14) };
            actions.Children.Add(ActionCard("▶", "运行选中测试", "运行 Explorer 中勾选或选中的用例", true,
                () => { if (RunSelectedRequested != null) RunSelectedRequested(); }));
            actions.Children.Add(ActionCard("▤", "运行全部测试", "按当前 Profile 发起真实 Runner", false,
                () => { if (RunAllRequested != null) RunAllRequested(); }));
            actions.Children.Add(ActionCard("◷", "查看运行历史", "核验已归档 Run / Attempt", false,
                () => { if (HistoryRequested != null) HistoryRequested(); }));
            actions.Children.Add(ActionCard("▣", "查看测试报告", "从真实执行结果生成报告", false,
                () => { if (ReportRequested != null) ReportRequested(); }));
            content.Children.Add(actions);
            UniformGrid stats = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 17) };
            stats.Children.Add(Metric("已发现用例", _discovered, Blue, "当前 Catalog · 非能力完成率"));
            stats.Children.Add(Metric("通过", _passed, Green, "当前显示 Run 的 Attempt"));
            stats.Children.Add(Metric("失败 / 错误 / 超时", _failed, Red, "当前显示 Run 的 Attempt"));
            stats.Children.Add(Metric("未执行用例", _pending, Amber, "当前 Catalog · 按唯一 TestId"));
            content.Children.Add(stats);
            // 设计稿以宽屏四列为基准，窄屏自动两列；不截断操作卡或伪造数据。
            SizeChanged += (sender, args) => {
                int columns = args.NewSize.Width < 820 ? 2 : 4;
                if (actions.Columns != columns) actions.Columns = columns;
                if (stats.Columns != columns) stats.Columns = columns;
            };
            Border section = Surface();
            StackPanel body = new StackPanel { Margin = new Thickness(14, 13, 14, 15) };
            body.Children.Add(new TextBlock {
                Text = "测试执行覆盖 / 最近运行", Foreground = Navy,
                FontWeight = FontWeights.SemiBold, FontSize = 16 });
            body.Children.Add(_runCaption);
            body.Children.Add(_aspectRows);
            section.Child = body;
            content.Children.Add(section);
            Border recent = Surface();
            recent.Margin = new Thickness(0, 13, 0, 0);
            DockPanel recentBody = new DockPanel { Margin = new Thickness(12, 10, 12, 12) };
            TextBlock heading = new TextBlock {
                Text = "最近 Attempt · 真实执行记录", FontWeight = FontWeights.SemiBold,
                FontSize = 16, Foreground = Navy, Margin = new Thickness(1, 0, 0, 8) };
            DockPanel.SetDock(heading, Dock.Top);
            recentBody.Children.Add(heading);
            _recent.Columns.Add(new DataGridTextColumn { Header = "测试用例", Binding = new Binding("Descriptor.Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            _recent.Columns.Add(new DataGridTextColumn { Header = "状态", Binding = new Binding("Status"), Width = new DataGridLength(90) });
            _recent.Columns.Add(new DataGridTextColumn { Header = "耗时 (ms)", Binding = new Binding("Elapsed.TotalMilliseconds") { StringFormat = "F2" }, Width = new DataGridLength(108) });
            _recent.Columns.Add(new DataGridTextColumn { Header = "Capability", Binding = new Binding("Descriptor.CapabilityId"), Width = new DataGridLength(155) });
            recentBody.Children.Add(_recent);
            recent.Child = recentBody;
            content.Children.Add(recent);
            scroller.Content = content;
            Content = scroller;
            AutomationProperties.SetAutomationId(this, "TCT.Overview");
            Update(null, null, null);
        }

        public void Update(TCTTestCatalog catalog, IEnumerable<TCTTestCaseResult> attempts, string runId)
        {
            if (catalog != null) _catalog = catalog;
            _attempts = attempts == null ? new List<TCTTestCaseResult>() : attempts.Where(x => x != null).ToList();
            _runId = runId;
            DiscoveredCount = _catalog == null ? 0 : _catalog.Cases.Count;
            int pass = _attempts.Count(x => x.Status == TCTTestStatus.Passed);
            int failures = _attempts.Count(x => x.Status == TCTTestStatus.Failed ||
                x.Status == TCTTestStatus.Error || x.Status == TCTTestStatus.TimedOut);
            int uniqueExecuted = _catalog == null ? 0 : _catalog.Cases.Count(test =>
                _attempts.Any(x => string.Equals(x.Descriptor.Id, test.Descriptor.Id, StringComparison.Ordinal)));
            _discovered.Text = DiscoveredCount.ToString();
            _passed.Text = pass.ToString();
            _failed.Text = failures.ToString();
            _pending.Text = Math.Max(0, DiscoveredCount - uniqueExecuted).ToString();
            _runCaption.Text = string.IsNullOrWhiteSpace(_runId) ?
                "尚无本次运行。执行覆盖只反映当前显示的 Run，并非所有能力的验收完成度。" :
                "当前 Run：" + _runId + " · " + _attempts.Count +
                " Attempt · Pass " + pass + " · Fail/Error/Timeout " + failures +
                " · 未执行与跳过需分别核验";
            _aspectRows.Children.Clear();
            if (_catalog != null)
                foreach (TCTTestAspect aspect in new[] { TCTTestAspect.Foundation, TCTTestAspect.Mechanism, TCTTestAspect.Integration })
                {
                    List<ITCTTestCase> cases = _catalog.Cases.Where(x => x.Descriptor.Aspect == aspect).ToList();
                    if (cases.Count == 0) continue;
                    int executed = cases.Count(test => _attempts.Any(a =>
                        string.Equals(a.Descriptor.Id, test.Descriptor.Id, StringComparison.Ordinal)));
                    Grid line = new Grid { Margin = new Thickness(2, 13, 2, 2) };
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(166) });
                    TextBlock name = new TextBlock { Text = aspect.ToString(), FontWeight = FontWeights.SemiBold, Foreground = Navy, VerticalAlignment = VerticalAlignment.Center };
                    line.Children.Add(name);
                    ProgressBar progress = new ProgressBar {
                        Minimum = 0, Maximum = cases.Count, Value = executed,
                        Height = 13, Foreground = Blue, Background = Brush("#E3EDF8"),
                        Margin = new Thickness(10, 0, 13, 0), VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(progress, 1); line.Children.Add(progress);
                    TextBlock detail = new TextBlock {
                        Text = executed + " / " + cases.Count + " 已执行 (" +
                        (100.0 * executed / cases.Count).ToString("F1") + "%)",
                        Foreground = Brush("#647D9B"), VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(detail, 2); line.Children.Add(detail);
                    _aspectRows.Children.Add(line);
                }
            _recent.ItemsSource = _attempts.Reverse().Take(7).ToArray();
        }

        private static TextBlock MetricValue(string id)
        {
            TextBlock value = new TextBlock { Text = "0", FontSize = 30, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 9, 0, 4) };
            AutomationProperties.SetAutomationId(value, id);
            return value;
        }
        private static Border Metric(string label, TextBlock value, Brush color, string caption)
        {
            Border border = Surface();
            border.Margin = new Thickness(0, 0, 10, 0);
            StackPanel stack = new StackPanel { Margin = new Thickness(14, 12, 8, 12) };
            stack.Children.Add(new TextBlock { Text = label, Foreground = Brush("#72849A"), FontSize = 13 });
            value.Foreground = color; stack.Children.Add(value);
            stack.Children.Add(new TextBlock { Text = caption, Foreground = Brush("#77899E"), FontSize = 11, TextWrapping = TextWrapping.Wrap });
            border.Child = stack; return border;
        }
        private static Border ActionCard(string symbol, string title, string caption, bool accented, Action action)
        {
            Border border = Surface();
            border.Margin = new Thickness(0, 0, 10, 0);
            if (accented) border.Background = Brush("#E7F2FF");
            Button button = new Button {
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(14, 12, 8, 13),
                Cursor = System.Windows.Input.Cursors.Hand };
            StackPanel stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = symbol + "  " + title, Foreground = Blue, FontWeight = FontWeights.SemiBold, FontSize = 15 });
            stack.Children.Add(new TextBlock { Text = caption, Foreground = Brush("#697F9A"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
            button.Content = stack;
            button.Click += (s, e) => action();
            AutomationProperties.SetAutomationId(button, "TCT.Overview.Action." + title);
            border.Child = button; return border;
        }
        private static Border Surface()
        {
            return new Border { Background = Brushes.White, BorderBrush = Brush("#CADBED"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5) };
        }
        private static Brush Brush(string color) { return (Brush)new BrushConverter().ConvertFromString(color); }
    }
}
