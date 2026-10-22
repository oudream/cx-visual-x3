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
        private WrapPanel _sideContexts;
        private ITCTTestCase _selectedContext;
        private Action<string> _showContextMode;
        private Action _showAssertions, _showSource, _showHistory, _showDiff;
        private readonly List<string> _activeContextCommands = new List<string>();
        public IReadOnlyList<string> ActiveContextCommands
        { get { return _activeContextCommands.AsReadOnly(); } }
        private readonly TextBlock _context = new TextBlock { VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0), Foreground = Brushes.SteelBlue };
        public FrameworkElement BuildSideActions(Action runAll, Action runSelected, Action cancel,
            Action export, Action rerunFailed, Action copyId, Action<string> mode)
        {
            ToolBar built = Build(runAll, runSelected, cancel, export, rerunFailed, copyId, mode);
            WrapPanel panel = new WrapPanel();
            foreach (UIElement item in built.Items.OfType<UIElement>().ToArray())
            {
                built.Items.Remove(item);
                if (!(item is Separator)) panel.Children.Add(item);
            }
            return panel;
        }
        public FrameworkElement BuildSideContexts(Action<string> mode, Action assertions,
            Action source, Action history, Action diff)
        {
            BuildContext(mode, assertions, source, history, diff);
            _sideContexts = new WrapPanel();
            RebuildContextCommands();
            return _sideContexts;
        }
        public ToolBar Build(Action runAll, Action runSelected, Action cancel, Action export,
            Action rerunFailed = null, Action copyTestId = null,
            Action<string> showMode = null)
        {
            ToolBar ribbon = new ToolBar { Background = Brushes.White, Band = 0, BandIndex = 0, Padding = new Thickness(5, 3, 5, 3) };
            RunAll = Create("▶ 运行全部", runAll, true);
            RunSelected = Create("▶ 运行选中", runSelected, true);
            System.Windows.Automation.AutomationProperties.SetAutomationId(RunSelected, "TCT.RunSelected");
            Cancel = Create("■ 取消", cancel, true); Cancel.IsEnabled = false;
            Export = Create("导出报告", export, true);
            ribbon.Items.Add(RunAll); ribbon.Items.Add(RunSelected);
            ribbon.Items.Add(Cancel); ribbon.Items.Add(Export);
            if (rerunFailed != null)
            {
                RerunFailed = Create("↻ 重跑未通过", rerunFailed, true);
                ribbon.Items.Add(RerunFailed);
            }
            if (copyTestId != null)
            {
                CopyTestId = Create("复制TestId", copyTestId, true);
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
            if (_sideContexts == null) _contextActions.Items.Add(button);
            else _sideContexts.Children.Add(button);
            _activeContextCommands.Add(id);
        }
        private void RebuildContextCommands()
        {
            if (_contextActions == null) return;
            _contextActions.Items.Clear();
            if (_sideContexts != null) _sideContexts.Children.Clear();
            _activeContextCommands.Clear();
            ITCTTestCase test = _selectedContext;
            if (test == null)
            {
                TextBlock empty = new TextBlock { Text = "选择用例后显示上下文命令",
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
                if (_sideContexts == null) _contextActions.Items.Add(empty);
                else _sideContexts.Children.Add(empty);
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
            if (_sideContexts != null) _sideContexts.IsEnabled = !running;
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
