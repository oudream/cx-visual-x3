using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CxVisual.DockingPoC.Views
{
    /// <summary>
    /// 只构建演示控件视图；每次反序列化通过 ContentId 重新构建视图，
    /// 不能把任意 XAML 或对象图从布局文件当作可执行内容导入。
    /// </summary>
    public static class DemoViewFactory
    {
        public static FrameworkElement Create(string id, DemoEngineeringState state)
        {
            if (!WorkspaceIds.IsKnown(id))
                throw new ArgumentException("未注册面板 ContentId: " + id, "id");

            FrameworkElement view;
            switch (id)
            {
                case WorkspaceIds.Resources: view = CreateResourceTree(); break;
                case WorkspaceIds.Objects: view = CreateObjectTree(); break;
                case WorkspaceIds.Toolbox: view = CreateToolbox(); break;
                case WorkspaceIds.Properties: view = CreateProperties(state); break;
                case WorkspaceIds.Diagnostics:
                    view = CreateCollectionView("编译 / 校验 / 运行诊断（演示）", state.Diagnostics); break;
                case WorkspaceIds.Logs:
                    view = CreateCollectionView("UI 操作日志（非工业审计日志）", state.Logs); break;
                case WorkspaceIds.Configuration: view = CreateConfiguration(); break;
                case WorkspaceIds.Visual: view = CreateVisual(); break;
                case WorkspaceIds.Flow: view = CreateFlow(); break;
                default: throw new ArgumentException("未知 ContentId", "id");
            }

            Border border = new Border { Child = view, Padding = new Thickness(10) };
            border.SetResourceReference(Border.BackgroundProperty, "PanelBrush");
            return border;
        }

        private static TextBlock Label(string text, double size = 13, bool strong = false)
        {
            TextBlock label = new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = strong ? FontWeights.SemiBold : FontWeights.Normal,
                Margin = new Thickness(3, 4, 3, 4),
                TextWrapping = TextWrapping.Wrap
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            return label;
        }

        private static StackPanel Vertical(string title)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(Label(title, 16, true));
            return panel;
        }

        private static TreeViewItem Node(string title, params TreeViewItem[] children)
        {
            TreeViewItem item = new TreeViewItem { Header = title, IsExpanded = true };
            foreach (TreeViewItem child in children) item.Items.Add(child);
            return item;
        }

        private static FrameworkElement CreateResourceTree()
        {
            TreeView tree = new TreeView();
            tree.Items.Add(Node("CxVisual 检测线工程",
                Node("页面", Node("主操作 HMI"), Node("诊断页面")),
                Node("内容资源", Node("检测主流程"), Node("AI 子流程"), Node("传统算法子流程")),
                Node("Symbol", Node("电机组件")),
                Node("配置", Node("Automation"), Node("Vision"), Node("CT3D"))));
            return tree;
        }

        private static FrameworkElement CreateObjectTree()
        {
            TreeView tree = new TreeView();
            tree.Items.Add(Node("当前页面 · HMI",
                Node("Group 01", Node("标题 Text"), Node("电机 Symbol A"), Node("电机 Symbol B")),
                Node("Elements", Node("Rectangle"), Node("StatusLamp"), Node("Start Button"))));
            return tree;
        }

        private static FrameworkElement CreateToolbox()
        {
            StackPanel panel = Vertical("专业工具箱");
            panel.Children.Add(new ListBox
            {
                ItemsSource = new[] { "基础图元 / Shape", "Text / Image / SVG", "Button / TextBox", "Symbol / Group",
                    "Control Flow", "Data Port", "条件 / 并行 / 循环", "AI 检测", "CV 检测", "设备事件" }
            });
            panel.Children.Add(Label("这里演示工具箱布局；不向工程树自动添加对象。", 11));
            return panel;
        }

        private static FrameworkElement CreateProperties(DemoEngineeringState state)
        {
            StackPanel panel = Vertical("属性与绑定（示例）");
            panel.Children.Add(Label("对象：MotorSymbol-A"));
            panel.Children.Add(Label("名称"));
            panel.Children.Add(new TextBox { Text = "Motor_A", IsReadOnly = true });
            panel.Children.Add(Label("数据绑定"));
            panel.Children.Add(new TextBox { Text = "MDS://Line01/MotorA/Running", IsReadOnly = true });
            panel.Children.Add(Label("实例覆盖"));
            panel.Children.Add(new CheckBox { Content = "允许显示状态动画（仅预览）", IsChecked = true });
            panel.Children.Add(Label("当前工程 Revision：" + state.Revision));
            panel.Children.Add(Label("正式 Studio 应从 MPM/MEM 读取 Schema，Docking 不保存属性值。", 11));
            return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel };
        }

        private static FrameworkElement CreateCollectionView(string title,
            System.Collections.ObjectModel.ObservableCollection<string> items)
        {
            DockPanel panel = new DockPanel();
            TextBlock label = Label(title, 14, true);
            DockPanel.SetDock(label, Dock.Top);
            panel.Children.Add(label);
            ListBox list = new ListBox { ItemsSource = items, FontFamily = new FontFamily("Consolas") };
            panel.Children.Add(list);
            return panel;
        }

        private static Border ConfigCard(string title, string detail)
        {
            Border card = new Border { Margin = new Thickness(7), Padding = new Thickness(14),
                CornerRadius = new CornerRadius(4), MinHeight = 120, BorderThickness = new Thickness(1) };
            card.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
            StackPanel panel = Vertical(title);
            panel.Children.Add(Label(detail));
            panel.Children.Add(new Button { Content = "编辑配置（示意）", HorizontalAlignment = HorizontalAlignment.Left,
                IsEnabled = false, ToolTip = "此按钮不写入真实工程" });
            card.Child = panel;
            return card;
        }

        private static FrameworkElement CreateConfiguration()
        {
            DockPanel root = new DockPanel();
            TextBlock header = Label("资源配置 · 2×N 组合面板", 18, true);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            UniformGrid cards = new UniformGrid { Columns = 2 };
            cards.Children.Add(ConfigCard("Automation", "设备、点位、PLC 与安全参数"));
            cards.Children.Add(ConfigCard("Vision", "采图、定位与模型选择"));
            cards.Children.Add(ConfigCard("CT3D", "重建几何与数据集"));
            cards.Children.Add(ConfigCard("Inspection", "产品、工位、复判"));
            cards.Children.Add(ConfigCard("Runtime", "执行组、并发与追踪策略"));
            cards.Children.Add(ConfigCard("HMI", "绑定、主题与页面入口"));
            scroll.Content = cards;
            root.Children.Add(scroll);
            return root;
        }

        private static FrameworkElement CreateVisual()
        {
            DockPanel root = new DockPanel();
            TextBlock header = Label("HMI · 图元编辑画布（静态示意，不是实际设计器）", 18, true);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            Canvas canvas = new Canvas { Width = 900, Height = 520, Background = Brushes.WhiteSmoke };
            Rectangle sheet = new Rectangle { Width = 780, Height = 420,
                Fill = Brushes.White, Stroke = Brushes.LightSteelBlue, StrokeThickness = 2 };
            Canvas.SetLeft(sheet, 45); Canvas.SetTop(sheet, 24); canvas.Children.Add(sheet);
            AddCanvasText(canvas, "X-Ray 检测线 · HMI", 82, 56, 22);
            AddCanvasText(canvas, "上料站", 110, 130, 15);
            AddCanvasText(canvas, "采图站", 320, 130, 15);
            AddCanvasText(canvas, "AI 判断", 530, 130, 15);
            for (int i = 0; i < 3; i++)
            {
                Rectangle device = new Rectangle { Width = 125, Height = 100,
                    Fill = new SolidColorBrush(Color.FromRgb(226, 236, 248)),
                    Stroke = Brushes.SteelBlue, StrokeThickness = 2, RadiusX = 5, RadiusY = 5 };
                Canvas.SetLeft(device, 90 + i * 210); Canvas.SetTop(device, 168);
                canvas.Children.Add(device);
                Ellipse led = new Ellipse { Width = 29, Height = 29, Fill = Brushes.ForestGreen };
                Canvas.SetLeft(led, 139 + i * 210); Canvas.SetTop(led, 206);
                canvas.Children.Add(led);
            }
            root.Children.Add(new ScrollViewer { Content = canvas,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            return root;
        }

        private static void AddCanvasText(Canvas canvas, string text, double x, double y, double size)
        {
            TextBlock label = new TextBlock { Text = text, Foreground = Brushes.DarkSlateGray,
                FontSize = size, FontWeight = FontWeights.SemiBold };
            Canvas.SetLeft(label, x); Canvas.SetTop(label, y); canvas.Children.Add(label);
        }

        private static FrameworkElement CreateFlow()
        {
            DockPanel root = new DockPanel();
            TextBlock header = Label("MOL → MOC → MRK · 主流程 + AI/CV 子流程（静态布局示例）", 18, true);
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);
            Canvas canvas = new Canvas { Width = 960, Height = 540, Background = Brushes.WhiteSmoke };
            string[] blocks = { "开始", "采图", "AI 子 Content", "NG?", "CV 子 Content", "结束" };
            for (int i = 0; i < blocks.Length; i++)
            {
                double x = 32 + i * 150;
                Rectangle rectangle = new Rectangle { Width = 132, Height = 78, RadiusX = 7, RadiusY = 7,
                    Stroke = Brushes.SteelBlue, StrokeThickness = 1.6,
                    Fill = i == 3 ? Brushes.LemonChiffon : new SolidColorBrush(Color.FromRgb(228, 238, 249)) };
                Canvas.SetLeft(rectangle, x); Canvas.SetTop(rectangle, 110); canvas.Children.Add(rectangle);
                AddCanvasText(canvas, blocks[i], x + 12, 133, 14);
                if (i != blocks.Length - 1)
                {
                    Line line = new Line { X1 = x + 132, Y1 = 149, X2 = x + 150, Y2 = 149,
                        Stroke = Brushes.DimGray, StrokeThickness = 2 };
                    canvas.Children.Add(line);
                }
            }
            AddCanvasText(canvas, "Root.Content → AI.Content / CV.Content", 38, 270, 17);
            AddCanvasText(canvas, "本 PoC 只验证流程页能正常停靠/浮动/恢复；不执行真实 MRK 或设备。", 38, 316, 13);
            root.Children.Add(new ScrollViewer { Content = canvas,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            return root;
        }
    }
}
