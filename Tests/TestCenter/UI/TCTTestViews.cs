using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.UI
{
    public interface ITCTTestViewer
    {
        string Kind { get; }
        bool CanDisplay(TCTTestCaseResult result);
        void Bind(TCTTestCaseResult result);
        void Clear();
    }

    // Viewer从Result/Observation只读呈现，不参与Pass/Fail计算。
    public abstract class TCTTestViewBase : UserControl, ITCTTestViewer
    {
        protected readonly StackPanel ContentPanel = new StackPanel { Margin = new Thickness(15) };
        protected TCTTestViewBase()
        {
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = ContentPanel };
            Content = scroll;
        }
        public abstract string Kind { get; }
        public virtual bool CanDisplay(TCTTestCaseResult result)
        {
            return result != null && result.Observations.Any(o =>
                string.Equals(o.Kind, Kind, StringComparison.OrdinalIgnoreCase));
        }
        public virtual void Clear() { ContentPanel.Children.Clear(); }
        public abstract void Bind(TCTTestCaseResult result);

        protected void Header(string title, string subtitle)
        {
            ContentPanel.Children.Add(new TextBlock { Text = title, FontSize = 18,
                FontWeight = FontWeights.SemiBold, Foreground = Brushes.DarkSlateBlue,
                Margin = new Thickness(0, 0, 0, 8) });
            ContentPanel.Children.Add(new TextBlock { Text = subtitle, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14), Foreground = Brushes.DimGray });
        }
        protected void Observations(TCTTestCaseResult result, string kind)
        {
            foreach (TCTTestObservation observation in result.Observations.Where(o =>
                string.Equals(o.Kind, kind, StringComparison.OrdinalIgnoreCase)))
            {
                ContentPanel.Children.Add(new TextBlock { Text = "[" + observation.Origin + "] " + observation.Title,
                    FontWeight = FontWeights.Bold, Margin = new Thickness(0, 7, 0, 5) });
                ContentPanel.Children.Add(new TextBlock { Text = observation.Details,
                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 9) });
            }
        }
        protected void Assertions(TCTTestCaseResult result)
        {
            foreach (TCTAssertionResult assertion in result.Assertions)
            {
                ContentPanel.Children.Add(new TextBlock {
                    Text = (assertion.Passed ? "✓ " : "✗ ") + assertion.Name +
                        "   expected=" + assertion.Expected + "   actual=" + assertion.Actual,
                    Foreground = assertion.Passed ? Brushes.DarkGreen : Brushes.DarkRed,
                    Margin = new Thickness(0, 3, 0, 3), TextWrapping = TextWrapping.Wrap });
            }
        }
    }

    public sealed class TCTGeometryTestView : TCTTestViewBase
    {
        public override string Kind { get { return "Geometry"; } }
        public override void Bind(TCTTestCaseResult result)
        {
            Clear();
            Header("Geometry / Expected vs Actual", "以下几何与数值均来自实际断言或真实观察结果。");
            Canvas canvas = new Canvas { Width = 520, Height = 235,
                Background = Brushes.AliceBlue, ClipToBounds = true };
            Line x = new Line { X1 = 20, Y1 = 195, X2 = 490, Y2 = 195,
                Stroke = Brushes.Gray, StrokeThickness = 1.2 };
            Line y = new Line { X1 = 35, Y1 = 20, X2 = 35, Y2 = 215,
                Stroke = Brushes.Gray, StrokeThickness = 1.2 };
            canvas.Children.Add(x); canvas.Children.Add(y);
            // 只有存在真实数值断言时才能画点；其它Geometry样例保留文本和断言。
            TCTAssertionResult ax = result.Assertions.FirstOrDefault(a => a.Name.Contains("变换X"));
            TCTAssertionResult ay = result.Assertions.FirstOrDefault(a => a.Name.Contains("变换Y"));
            double ex, ey, actualX, actualY;
            if (ax != null && ay != null &&
                double.TryParse(ax.Expected, NumberStyles.Float, CultureInfo.InvariantCulture, out ex) &&
                double.TryParse(ay.Expected, NumberStyles.Float, CultureInfo.InvariantCulture, out ey) &&
                double.TryParse(ax.Actual, NumberStyles.Float, CultureInfo.InvariantCulture, out actualX) &&
                double.TryParse(ay.Actual, NumberStyles.Float, CultureInfo.InvariantCulture, out actualY))
            {
                double maxX = Math.Max(Math.Abs(ex), Math.Abs(actualX)) + 5;
                double maxY = Math.Max(Math.Abs(ey), Math.Abs(actualY)) + 5;
                DrawPoint(canvas, 35 + ex / maxX * 430, 195 - ey / maxY * 150,
                    Brushes.ForestGreen, "Expected");
                DrawPoint(canvas, 35 + actualX / maxX * 430, 195 - actualY / maxY * 150,
                    Brushes.Crimson, "Actual");
            }
            else
            {
                TextBlock hint = new TextBlock { Text = "该用例未提供二维数值坐标：不生成模拟图形",
                    Foreground = Brushes.DimGray };
                Canvas.SetLeft(hint, 75); Canvas.SetTop(hint, 100); canvas.Children.Add(hint);
            }
            ContentPanel.Children.Add(canvas);
            Observations(result, Kind); Assertions(result);
        }
        private static void DrawPoint(Canvas canvas, double x, double y, Brush color, string label)
        {
            Ellipse dot = new Ellipse { Width = 10, Height = 10, Fill = color };
            Canvas.SetLeft(dot, x - 5); Canvas.SetTop(dot, y - 5); canvas.Children.Add(dot);
            TextBlock text = new TextBlock { Text = label, Foreground = color, FontSize = 11 };
            Canvas.SetLeft(text, x + 8); Canvas.SetTop(text, y - 8); canvas.Children.Add(text);
        }
    }

    public sealed class TCTObjectTestView : TCTTestViewBase
    {
        public override string Kind { get { return "ObjectTree"; } }
        public override void Bind(TCTTestCaseResult result)
        {
            Clear();
            Header("Object / Ownership / Reference", "树结构只使用被测Mechanism生成的观察；不把Reference误当Ownership。");
            foreach (TCTTestObservation observation in result.Observations.Where(o => o.Kind == Kind))
            {
                TreeView tree = new TreeView { Height = 160, Background = Brushes.AliceBlue };
                string[] nodes = observation.Details.Split(new[] { "→" }, StringSplitOptions.RemoveEmptyEntries);
                if (nodes.Length > 1)
                {
                    TreeViewItem parent = null;
                    foreach (string name in nodes)
                    {
                        TreeViewItem item = new TreeViewItem { Header = name.Trim(), IsExpanded = true };
                        if (parent == null) tree.Items.Add(item);
                        else parent.Items.Add(item);
                        parent = item;
                    }
                }
                else tree.Items.Add(new TreeViewItem { Header = observation.Details });
                ContentPanel.Children.Add(tree);
            }
            Observations(result, Kind); Assertions(result);
        }
    }

    public sealed class TCTGraphTestView : TCTTestViewBase
    {
        public override string Kind { get { return "Graph"; } }
        public override void Bind(TCTTestCaseResult result)
        {
            Clear(); Header("Graph / Port / Connection / Compile",
                "仅呈现真实验证、编译诊断和图连接事件。不复制Mechanism的Graph定义。");
            Observations(result, Kind); Assertions(result);
        }
    }
    public sealed class TCTChannelTestView : TCTTestViewBase
    {
        public override string Kind { get { return "Channel"; } }
        public override void Bind(TCTTestCaseResult result)
        {
            Clear(); Header("Channel / Protocol / TX-RX",
                "只显示已采集到的Frame、Session及错误事件。原始报文和解码字段由真实Adapter提供。");
            Observations(result, Kind); Assertions(result);
        }
    }
    public sealed class TCTRuntimeTestView : TCTTestViewBase
    {
        public override string Kind { get { return "Runtime"; } }
        public override void Bind(TCTTestCaseResult result)
        {
            Clear(); Header("Execution / Resource / Timeline",
                "仅显示真实Instance、Lease、WaitingReason、Trace和Timeline事件。");
            Observations(result, Kind); Assertions(result);
        }
    }

    public sealed class TCTTestDocumentHostControl : UserControl
    {
        private readonly TabControl _tabs = new TabControl();
        private readonly TextBlock _empty = new TextBlock { Text = "选择用例运行后显示真实观察数据。",
            Margin = new Thickness(15), Foreground = Brushes.DimGray };
        private readonly List<ITCTTestViewer> _viewers = new List<ITCTTestViewer> {
            new TCTGeometryTestView(), new TCTObjectTestView(), new TCTGraphTestView(),
            new TCTChannelTestView(), new TCTRuntimeTestView()
        };
        public TCTTestDocumentHostControl()
        {
            Content = _tabs;
            Clear();
        }
        public void Clear()
        {
            _tabs.Items.Clear();
            foreach (ITCTTestViewer viewer in _viewers) viewer.Clear();
            _tabs.Items.Add(new TabItem { Header = "概览", Content = _empty });
        }
        public void ShowResult(TCTTestCaseResult result)
        {
            Clear();
            if (result == null) return;
            _empty.Text = result.Status + " · " + result.Descriptor.Name +
                "\n观察数：" + result.Observations.Count +
                "\n断言数：" + result.Assertions.Count +
                "\n诊断数：" + result.Diagnostics.Count +
                "\n产物数：" + result.Artifacts.Count +
                "\n\n没有专业观察时不生成演示数据。";
            foreach (ITCTTestViewer viewer in _viewers)
            {
                if (!viewer.CanDisplay(result)) continue;
                viewer.Bind(result);
                _tabs.Items.Add(new TabItem { Header = viewer.Kind, Content = (UserControl)viewer });
            }
            if (_tabs.Items.Count > 1) _tabs.SelectedIndex = 1;
        }
    }
}
