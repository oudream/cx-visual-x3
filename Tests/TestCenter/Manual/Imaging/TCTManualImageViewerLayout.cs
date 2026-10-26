using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using CxVisual.Foundation.Imaging;
using CxVisual.Mechanism.Visual;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 二维视觉工作台的唯一WPF布局。和业务动作在同一partial类型中，
    /// 复用原FIM/MVI/MPW操作，不另造Viewer、命令转发或独立工程。
    /// 高/低频操作分离，右侧两组Tab承接成熟图窗功能面板。
    /// </summary>
    public sealed partial class TCTManualImageViewerFrame
    {
        private readonly TabControl _displayTabs = new TabControl();
        private readonly System.Collections.Generic.List<Button> _quickButtons =
            new System.Collections.Generic.List<Button>();
        private readonly CheckBox _showRoi = new CheckBox { Content = "ROI", IsChecked = true };
        private readonly CheckBox _showMeasurements = new CheckBox { Content = "测量", IsChecked = true };
        private readonly CheckBox _showAnnotations = new CheckBox { Content = "标注", IsChecked = true };
        private readonly CheckBox _showDefects = new CheckBox { Content = "缺陷", IsChecked = true };
        private readonly TextBlock _overlayCount = new TextBlock();
        public int QuickActionCount { get { return _quickButtons.Count; } }
        public int DisplayTabCount { get { return _displayTabs.Items.Count; } }
        public int AnalysisTabCount { get { return _inspectTabs.Items.Count; } }
        public string[] DisplayTabTitles
        { get { return _displayTabs.Items.OfType<TabItem>().Select(t => (string)t.Header).ToArray(); } }
        public string[] AnalysisTabTitles
        { get { return _inspectTabs.Items.OfType<TabItem>().Select(t => (string)t.Header).ToArray(); } }

        private static SolidColorBrush Paint(byte r, byte g, byte b)
        { return new SolidColorBrush(Color.FromRgb(r,g,b)); }

        private void BuildWorkbenchLayout()
        {
            FontSize = 14;
            Background = Paint(245,248,252);
            Grid root = new Grid { Background = Paint(245,248,252) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1,GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(29) });
            Content = root;

            // 独立且不换行的高频工具栏；低频参数归右侧Tab，窄工作区只横向滚动。
            Border bar = new Border { Background = Paint(251,253,255),
                BorderBrush = Paint(205,217,231), BorderThickness = new Thickness(0,0,0,1) };
            ScrollViewer horizontal = new ScrollViewer {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false };
            StackPanel commands = new StackPanel { Orientation = Orientation.Horizontal,
                Margin = new Thickness(9,2,9,2), VerticalAlignment = VerticalAlignment.Center };
            Quick(commands,"▣","打开图像","Open",OpenWithDialog);
            Quick(commands,"⤢","适应窗口","Fit",()=>_viewer.FitToWindow());
            Quick(commands,"1:1","实际像素","Actual",()=>_viewer.ActualSize());
            Quick(commands,"＋","放大","ZoomIn",()=>_viewer.Zoom(1.25));
            Quick(commands,"－","缩小","ZoomOut",()=>_viewer.Zoom(0.8));
            Divider(commands);
            Quick(commands,"⌖","导航/平移","Navigate",()=>SelectTool(MVIImageToolMode.Navigate));
            Quick(commands,"□","矩形ROI","RectangleRoi",()=>SelectTool(MVIImageToolMode.RectangleRoi));
            Quick(commands,"◯","圆形ROI","CircleRoi",()=>SelectTool(MVIImageToolMode.CircleRoi));
            Quick(commands,"✎","测距","Distance",()=>SelectTool(MVIImageToolMode.Distance));
            Quick(commands,"↻","观察旋转","ViewRotate",()=>_viewer.RotateViewClockwise());
            Quick(commands,"⇄","观察镜像","ViewMirrorX",()=>_viewer.FlipViewHorizontal());
            Divider(commands);
            Quick(commands,"◧","原图","Original",()=>_viewer.SelectOriginal());
            Quick(commands,"▧","结果图","Result",()=>_viewer.SelectResult());
            Quick(commands,"◉","统计","Analyze",Calculate);
            Quick(commands,"▤","保存图像","Save",Save);
            horizontal.Content = commands; bar.Child = horizontal;
            root.Children.Add(bar);

            Grid work = new Grid { Background = Paint(228,234,243),
                Margin = new Thickness(4,4,4,3) };
            work.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star),
                MinWidth = 220 });
            work.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            work.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(344),
                MinWidth = 280, MaxWidth = 500 });
            Grid.SetRow(work,1);
            root.Children.Add(work);
            Border image = new Border { BorderBrush = Paint(193,207,225),
                BorderThickness = new Thickness(1), Background = Paint(28,36,48),
                Child = _viewer };
            work.Children.Add(image);
            GridSplitter resizer = new GridSplitter { Width = 5,
                Background = Paint(216,226,238), HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch, ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                ResizeDirection = GridResizeDirection.Columns };
            Grid.SetColumn(resizer,1); work.Children.Add(resizer);

            Grid right = new Grid { Background = Paint(249,251,254) };
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48,GridUnitType.Star), MinHeight = 160 });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52,GridUnitType.Star), MinHeight = 160 });
            Grid.SetColumn(right,2);
            work.Children.Add(right);
            StyleTabs(_displayTabs,"TCT.Imaging.PrimaryTabs");
            StyleTabs(_inspectTabs,"TCT.Imaging.SecondaryTabs");

            _displayTabs.Items.Add(Titled("显示",BuildDisplayPage()));
            _displayTabs.Items.Add(Titled("窗位",BuildWindowPage()));
            _displayTabs.Items.Add(Titled("灰阶",BuildGrayPage()));
            _displayTabs.Items.Add(Titled("测量",BuildMeasurePage()));
            _displayTabs.Items.Add(Titled("标注",BuildAnnotatePage()));
            _displayTabs.SelectedIndex = 0;

            _inspectTabs.Items.Add(Titled("区域",BuildRegionPage()));
            _inspectTabs.Items.Add(Titled("统计",BuildStatisticsPage()));
            _inspectTabs.Items.Add(Titled("剖面",BuildProfilePage()));
            _inspectTabs.Items.Add(Titled("变换",BuildTransformPage()));
            _inspectTabs.Items.Add(Titled("输出",BuildOutputPage()));
            _inspectTabs.SelectedIndex = 0;
            Grid.SetRow(_inspectTabs,2); right.Children.Add(_inspectTabs);
            right.Children.Add(_displayTabs);
            GridSplitter rowSplitter = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center, Background = Paint(219,227,239),
                ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext };
            Grid.SetRow(rowSplitter,1); right.Children.Add(rowSplitter);

            Border foot = new Border { Background = Paint(238,246,255),
                BorderBrush = Paint(192,211,232), BorderThickness = new Thickness(0,1,0,0),
                Padding = new Thickness(8,2,8,2), Child = _status };
            _status.FontSize = 13;
            Grid.SetRow(foot,2); root.Children.Add(foot);
            AutomationProperties.SetAutomationId(this,"TCT.Imaging.Workbench");
            AutomationProperties.SetAutomationId(_displayTabs,"TCT.Imaging.PrimaryTabs");
            AutomationProperties.SetAutomationId(_inspectTabs,"TCT.Imaging.SecondaryTabs");

            _showRoi.Checked += (s,e)=>_viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Roi,true);
            _showRoi.Unchecked += (s,e)=>_viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Roi,false);
            _showMeasurements.Checked += (s,e)=>_viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Measurement,true);
            _showMeasurements.Unchecked += (s,e)=>_viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Measurement,false);
            _showAnnotations.Checked += (s,e)=>_viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Annotation,true);
            _showAnnotations.Unchecked += (s,e)=>_viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Annotation,false);
            _showDefects.Checked += (s,e)=>_viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Defect,true);
            _showDefects.Unchecked += (s,e)=>_viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Defect,false);
            _viewer.Overlays.Changed += (s,e)=>
                _overlayCount.Text = "对象 " + _viewer.Overlays.Items.Count +
                    "  ·  可见 " + _viewer.VisibleOverlayCount;
            _overlayCount.Text = "对象 0  ·  可见 0";
        }

        private void Quick(Panel host,string symbol,string label,string id,Action action)
        {
            Button button = AddButton(host,label,id,action);
            button.Margin = new Thickness(1,2,1,2);
            button.Width = 84; button.Height = 51; button.MinHeight = 51;
            button.Padding = new Thickness(2,2,2,2);
            button.BorderThickness = new Thickness(0);
            button.Background = Brushes.Transparent;
            button.ToolTip = label;
            StackPanel content = new StackPanel { VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(Glyph(id,symbol,23));
            content.Children.Add(new TextBlock { Text = label, FontSize = 12.5,
                Foreground = Paint(48,66,91), HorizontalAlignment = HorizontalAlignment.Center });
            button.Content = content;
            _quickButtons.Add(button);
        }

        /// <summary>WPF原生线形矢量图标，不依赖旧WinForms ImageList或外置Icon字体。</summary>
        private static UIElement Glyph(string id,string fallback,double size)
        {
            string path = IconPath(id);
            if (path == null) return new TextBlock {
                Text = fallback, FontSize = size, FontWeight = FontWeights.SemiBold,
                Foreground = Paint(31,119,219), TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center };
            System.Windows.Shapes.Path icon = new System.Windows.Shapes.Path {
                Data = Geometry.Parse(path), Width = size, Height = size,
                Stroke = Paint(31,119,219), StrokeThickness = 1.9,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = Brushes.Transparent, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center };
            return icon;
        }
        private static string IconPath(string id)
        {
            switch (id)
            {
                case "Open": return "M3,7 L3,4 L10,4 L12,7 L21,7 L19,20 L4,20 Z M3,9 L19,9";
                case "Fit": return "M4,10 L4,4 L10,4 M14,4 L20,4 L20,10 M20,14 L20,20 L14,20 M10,20 L4,20 L4,14";
                case "Actual": return "M5,5 L19,5 L19,19 L5,19 Z M9,10 L11,8 L11,16 M14,12 L14,16 M17,12 L17,16";
                case "ZoomIn": return "M10,3 A7,7 0 1 1 10,17 A7,7 0 1 1 10,3 M15,15 L21,21 M7,10 L13,10 M10,7 L10,13";
                case "ZoomOut": return "M10,3 A7,7 0 1 1 10,17 A7,7 0 1 1 10,3 M15,15 L21,21 M7,10 L13,10";
                case "Navigate": return "M3,12 L21,12 M12,3 L12,21 M3,12 L7,8 M3,12 L7,16 M21,12 L17,8 M21,12 L17,16";
                case "RectangleRoi": case "RegionRectangle":
                    return "M4,5 L20,5 L20,19 L4,19 Z M2,3 L6,3 L6,7 L2,7 Z M18,17 L22,17 L22,21 L18,21 Z";
                case "CircleRoi": case "RegionCircle": return "M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3";
                case "RegionEllipse": return "M12,6 A10,6 0 1 1 12,18 A10,6 0 1 1 12,6";
                case "RegionPolygon": return "M12,3 L21,10 L18,20 L6,20 L3,10 Z M12,3 L12,4 M21,10 L20,10";
                case "Distance": case "MeasureDistance": return "M4,18 L20,5 M3,15 L7,21 M17,2 L23,8 M5,18 L20,5";
                case "MeasureAngle": return "M3,19 L11,5 L21,19 M5,16 A6,6 0 0 1 14,13";
                case "MeasureHorizontal": return "M3,12 L21,12 M3,8 L3,16 M21,8 L21,16 M4,12 L7,9 M4,12 L7,15";
                case "MeasureVertical": return "M12,3 L12,21 M8,3 L16,3 M8,21 L16,21 M12,4 L9,7 M12,4 L15,7";
                case "MeasureDiameter": return "M12,3 A9,9 0 1 1 12,21 A9,9 0 1 1 12,3 M3,12 L21,12";
                case "MeasureArea": return "M4,4 L20,4 L20,20 L4,20 Z M7,14 L7,17 L10,17";
                case "MeasurePolygonArea": return "M12,3 L21,10 L18,20 L6,20 L3,10 Z M9,14 L15,14";
                case "ViewRotate": return "M19,6 A9,9 0 1 0 21,13 M16,3 L20,7 L21,2";
                case "ViewMirrorX": return "M12,2 L12,22 M3,7 L8,4 L8,20 L3,17 Z M21,7 L16,4 L16,20 L21,17 Z";
                case "Original": case "OriginalBottom": return "M4,4 L20,4 L20,20 L4,20 Z M6,17 L11,11 L14,14 L17,9 L20,13";
                case "Result": case "ResultBottom": return "M4,4 L20,4 L20,20 L4,20 Z M7,12 L11,16 L18,8";
                case "Analyze": case "AnalyzeStatistics": return "M4,20 L4,12 L7,12 L7,20 M10,20 L10,5 L13,5 L13,20 M16,20 L16,9 L19,9 L19,20";
                case "Save": case "SaveRawPng": case "SaveViewPng": return "M4,3 L18,3 L21,6 L21,21 L3,21 L3,3 Z M7,3 L7,10 L17,10 L17,3 M8,21 L8,14 L17,14 L17,21";
                case "DefectBox": return "M4,5 L20,5 L20,19 L4,19 Z M12,8 L12,14 M12,17 L12,17.5";
                case "Point": return "M12,3 L12,21 M3,12 L21,12 M12,9 A3,3 0 1 1 12,15 A3,3 0 1 1 12,9";
                case "Text": return "M3,5 L21,5 M12,5 L12,20 M7,20 L17,20";
                case "Arrow": return "M4,20 L20,4 M13,4 L20,4 L20,11";
                case "MoveOverlay": return "M12,2 L12,22 M2,12 L22,12 M12,2 L9,5 M12,2 L15,5";
                case "DeleteOverlay": case "DeleteOverlayTool": return "M5,6 L19,6 M8,6 L9,20 L16,20 L17,6 M9,3 L15,3 M10,10 L10,16 M14,10 L14,16";
                case "ViewReset": return "M4,8 L4,4 L8,4 M4,4 L9,9 M5,13 A8,8 0 1 0 13,4";
                default: return null;
            }
        }

        private static void Divider(Panel host)
        { host.Children.Add(new Border { Width = 1, Background = Paint(208,221,236),
            Margin = new Thickness(7,9,7,9) }); }

        private static void StyleTabs(TabControl tabs,string id)
        {
            tabs.Background = Paint(251,253,255);
            tabs.BorderBrush = Paint(207,219,234);
            tabs.BorderThickness = new Thickness(1);
            tabs.FontSize = 14;
            tabs.Padding = new Thickness(0);
            Style item = new Style(typeof(TabItem));
            item.Setters.Add(new Setter(TabItem.PaddingProperty,new Thickness(10,6,10,6)));
            item.Setters.Add(new Setter(TabItem.FontSizeProperty,14.0));
            item.Setters.Add(new Setter(TabItem.FontWeightProperty,FontWeights.SemiBold));
            item.Setters.Add(new Setter(TabItem.ForegroundProperty,Paint(53,79,110)));
            item.Setters.Add(new Setter(TabItem.BackgroundProperty,Paint(247,250,254)));
            Trigger selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(TabItem.ForegroundProperty,Paint(29,112,213)));
            selected.Setters.Add(new Setter(TabItem.BackgroundProperty,Paint(229,241,255)));
            item.Triggers.Add(selected);
            tabs.ItemContainerStyle = item;
        }
        private static TabItem Titled(string title, UIElement content)
        { return new TabItem { Header = title, Content = content }; }

        private static StackPanel Column()
        { return new StackPanel { Margin = new Thickness(10,11,10,10) }; }
        private static TextBlock Heading(string title)
        { return new TextBlock { Text = title, FontWeight = FontWeights.SemiBold,
            FontSize = 14, Foreground = Paint(45,73,109), Margin = new Thickness(2,3,2,9) }; }
        private static TextBlock Hint(string title)
        { return new TextBlock { Text = title, Foreground = Paint(110,128,149),
            FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2,6,2,8) }; }
        private static ScrollViewer Scroll(UIElement content)
        { return new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Background = Paint(251,253,255) }; }

        private static WrapPanel Row(params UIElement[] elements)
        {
            WrapPanel row = new WrapPanel { Margin = new Thickness(0,3,0,5) };
            foreach (UIElement e in elements) row.Children.Add(e);
            return row;
        }
        private static TextBlock Label(string text)
        { return new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13, Foreground = Paint(67,91,119), Margin = new Thickness(2,1,5,1) }; }

        private Button Tile(Panel host,string symbol,string label,string id,Action action)
        {
            Button button = AddButton(host,label,id,action);
            button.MinWidth = 89; button.Width = 93; button.Height = 72;
            button.Margin = new Thickness(3);
            button.Padding = new Thickness(3,4,3,4);
            button.BorderBrush = Paint(207,222,239);
            StackPanel content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(Glyph(id,symbol,27));
            content.Children.Add(new TextBlock { Text = label, FontSize = 13.5,
                Foreground = Paint(52,72,98), HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0,3,0,0) });
            button.Content = content;
            return button;
        }
        private Button Action(Panel host,string label,string id,Action action)
        {
            Button button = AddButton(host,label,id,action);
            button.FontSize = 13; button.MinHeight = 33;
            button.Background = Brushes.White; button.BorderBrush = Paint(205,223,244);
            button.Foreground = Paint(42,91,149);
            button.Margin = new Thickness(3,3,3,3);
            return button;
        }

        private UIElement BuildDisplayPage()
        {
            StackPanel content = Column();
            content.Children.Add(Heading("叠加图层  ·  显示与选择"));
            WrapPanel colors = Row();
            foreach (Tuple<string,Brush> c in new[] {
                Tuple.Create("ROI",(Brush)Paint(13,166,195)),
                Tuple.Create("测量",(Brush)Paint(230,164,34)),
                Tuple.Create("标注",(Brush)Paint(185,48,110)),
                Tuple.Create("缺陷",(Brush)Paint(233,71,61)) })
            {
                Border badge = new Border { Background = c.Item2, Margin = new Thickness(3,2,3,2),
                    Width = 69, Height = 25, CornerRadius = new CornerRadius(2) };
                badge.Child = new TextBlock { Text = c.Item1, Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12 };
                colors.Children.Add(badge);
            }
            content.Children.Add(colors);
            WrapPanel visibility = Row(_showRoi,_showMeasurements,_showAnnotations,_showDefects);
            foreach (CheckBox check in visibility.Children.OfType<CheckBox>())
                check.Margin = new Thickness(8,5,10,5);
            content.Children.Add(visibility);
            content.Children.Add(_overlayCount);
            content.Children.Add(Hint("编辑选择、测量和缺陷会保留原图坐标；隐藏图层不参与鼠标命中。"));
            content.Children.Add(Heading("当前交互工具"));
            foreach (MVIImageToolMode mode in Enum.GetValues(typeof(MVIImageToolMode)))
                _imageTool.Items.Add(new ComboBoxItem { Content = ToolTitle(mode), Tag = mode });
            _imageTool.SelectedIndex = 0;
            _imageTool.MinHeight = 32; _imageTool.Margin = new Thickness(3,0,3,7);
            content.Children.Add(_imageTool);
            content.Children.Add(Hint("工具、ROI和标注直接操作既有 MVI OverlayStore。"));
            return Scroll(content);
        }
        private UIElement BuildWindowPage()
        {
            StackPanel content = Column();
            content.Children.Add(Heading("原始灰度窗宽 / 窗位"));
            content.Children.Add(Row(Label("窗下限"),_low));
            content.Children.Add(Row(Label("窗上限"),_high));
            WrapPanel actions = Row();
            Action(actions,"应用窗宽窗位","Window",Window);
            Action(actions,"还原显示","ResetWindow",ResetDisplay);
            content.Children.Add(actions);
            content.Children.Add(Hint("窗参数改变显示映射，不修改原始8/16/32位像素。"));
            content.Children.Add(Heading("灰度参考与快速检查"));
            WrapPanel more = Row();
            Action(more,"灰度统计","AnalyzeWindow",Calculate);
            Action(more,"灰度剖面","ProfileWindow",ShowLineProfile);
            content.Children.Add(more);
            return Scroll(content);
        }
        private UIElement BuildGrayPage()
        {
            StackPanel content = Column();
            content.Children.Add(Heading("灰阶与显示增强"));
            content.Children.Add(Row(Label("Gamma"),_displayGamma));
            content.Children.Add(Row(Label("亮度"),_displayBrightness));
            content.Children.Add(Row(Label("对比度"),_displayContrast));
            content.Children.Add(Row(_displayInverted,_autoContrast));
            WrapPanel buttons = Row();
            Action(buttons,"应用灰阶参数","ApplyDisplay",ApplyDisplay);
            Action(buttons,"全部复位","ResetDisplay",ResetDisplay);
            content.Children.Add(buttons);
            content.Children.Add(Hint("显示修改后，原始灰度统计、标定和ROI测量值均保持不变。"));
            return Scroll(content);
        }
        private UIElement BuildMeasurePage()
        {
            StackPanel content = Column();
            content.Children.Add(Heading("图像测量  ·  原图坐标"));
            WrapPanel tiles = Row();
            Tile(tiles,"↗","距离","MeasureDistance",()=>SelectTool(MVIImageToolMode.Distance));
            Tile(tiles,"∠","角度","MeasureAngle",()=>SelectTool(MVIImageToolMode.Angle));
            Tile(tiles,"↔","水平距离","MeasureHorizontal",()=>SelectTool(MVIImageToolMode.HorizontalDistance));
            Tile(tiles,"↕","垂直距离","MeasureVertical",()=>SelectTool(MVIImageToolMode.VerticalDistance));
            Tile(tiles,"◯","直径","MeasureDiameter",()=>SelectTool(MVIImageToolMode.Diameter));
            Tile(tiles,"□","矩形面积","MeasureArea",()=>SelectTool(MVIImageToolMode.Area));
            Tile(tiles,"⬡","多边形面积","MeasurePolygonArea",()=>SelectTool(MVIImageToolMode.PolygonArea));
            content.Children.Add(tiles);
            content.Children.Add(Hint("线段拖拽得到距离；角度测量依次点击三个点。计算与叠加显示均调用已有 FIM/MVI。"));
            content.Children.Add(Heading("标尺与物理单位"));
            content.Children.Add(Row(Label("X mm/px"),_spacingX));
            content.Children.Add(Row(Label("Y mm/px"),_spacingY));
            WrapPanel units = Row();
            Action(units,"启用毫米标尺","RulerMm",ApplyPixelSpacing);
            Action(units,"像素标尺","RulerPixel",ResetPixelSpacing);
            content.Children.Add(units);
            content.Children.Add(Hint("所有工具直接创建MVI测量对象；矩形/圆拖拽完成，多边形双击完成。未标定只显示像素，非等距标定的圆显示X/Y两个物理直径。"));
            return Scroll(content);
        }
        private UIElement BuildAnnotatePage()
        {
            StackPanel content = Column();
            content.Children.Add(Heading("标注  ·  直接操作图层"));
            WrapPanel tiles = Row();
            Tile(tiles,"✣","点标注","Point",()=>SelectTool(MVIImageToolMode.Point));
            Tile(tiles,"T","文字","Text",()=>SelectTool(MVIImageToolMode.Text));
            Tile(tiles,"↗","箭头","Arrow",()=>SelectTool(MVIImageToolMode.Arrow));
            Tile(tiles,"▣","缺陷框","DefectBox",()=>SelectTool(MVIImageToolMode.DefectBox));
            Tile(tiles,"✋","移动","MoveOverlay",()=>SelectTool(MVIImageToolMode.MoveOverlay));
            Tile(tiles,"⌫","删除","DeleteOverlayTool",()=>SelectTool(MVIImageToolMode.DeleteOverlay));
            content.Children.Add(tiles);
            content.Children.Add(Row(Label("标注文字"),_annotationText));
            Action(content,"清空所有标注及ROI","ClearOverlays",()=>_viewer.ClearOverlays());
            return Scroll(content);
        }
        private UIElement BuildRegionPage()
        {
            StackPanel content = Column();
            content.Children.Add(Heading("ROI  ·  绘制与图层对象"));
            WrapPanel tiles = Row();
            Tile(tiles,"□","矩形","RegionRectangle",()=>SelectTool(MVIImageToolMode.RectangleRoi));
            Tile(tiles,"◯","圆形","RegionCircle",()=>SelectTool(MVIImageToolMode.CircleRoi));
            Tile(tiles,"⬭","椭圆","RegionEllipse",()=>SelectTool(MVIImageToolMode.EllipseRoi));
            Tile(tiles,"⬡","多边形","RegionPolygon",()=>SelectTool(MVIImageToolMode.PolygonRoi));
            content.Children.Add(tiles);
            WrapPanel buttons = Row();
            Action(buttons,"清除选框","RemoveRoi",()=>_viewer.ClearSelection());
            Action(buttons,"删除选中","DeleteOverlay",DeleteSelectedOverlay);
            Action(buttons,"左移 1px","MoveOverlay",()=>MoveSelectedOverlay(-1,0));
            Action(buttons,"隐藏/显示层","LayerVisibility",ToggleSelectedLayer);
            Action(buttons,"显示所有层","ShowAllLayers",ShowAllLayers);
            content.Children.Add(buttons);
            content.Children.Add(_overlayList);
            content.Children.Add(Hint("下方对象列表按稳定ID管理；多边形通过双击结束绘制。测量数据可在“输出”页导出CSV。"));
            return Scroll(content);
        }
        private UIElement BuildStatisticsPage()
        {
            Grid content = new Grid { Margin = new Thickness(9,7,9,7) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1,GridUnitType.Star),
                MinHeight = 70 });
            WrapPanel buttons = Row(_withRoi);
            Action(buttons,"计算灰度统计 / 直方图","AnalyzeStatistics",Calculate);
            content.Children.Add(buttons);
            Grid.SetRow(_histogramPlot,1); content.Children.Add(_histogramPlot);
            _info.FontSize = 12.5;
            _info.Margin = new Thickness(0,5,0,0);
            Grid.SetRow(_info,2); content.Children.Add(_info);
            return content;
        }
        private UIElement BuildProfilePage()
        {
            DockPanel content = new DockPanel { Margin = new Thickness(9,8,9,9) };
            StackPanel top = new StackPanel();
            top.Children.Add(Heading("灰度剖面  ·  当前测量线 / ROI"));
            Action(top,"更新灰度剖面","LineProfile",ShowLineProfile);
            top.Children.Add(Hint("优先使用选中测量线或ROI对角线；否则取图像中心线。"));
            DockPanel.SetDock(top,Dock.Top); content.Children.Add(top);
            content.Children.Add(_profilePlot);
            return content;
        }
        private UIElement BuildTransformPage()
        {
            StackPanel content = Column();
            content.Children.Add(Heading("显示变换  ·  原始图像不变"));
            WrapPanel view = Row();
            Tile(view,"↻","顺时针90°","ViewRotate",()=>_viewer.RotateViewClockwise());
            Tile(view,"⇄","水平镜像","ViewMirrorX",()=>_viewer.FlipViewHorizontal());
            Tile(view,"⇅","垂直镜像","ViewMirrorY",()=>_viewer.FlipViewVertical());
            Tile(view,"⟲","复位观察","ViewReset",()=>_viewer.ResetViewTransform());
            content.Children.Add(view);
            content.Children.Add(Heading("生成结果图  ·  保留原图"));
            WrapPanel pixels = Row();
            Action(pixels,"旋转90°","Rotate",()=>ProcessImage(i=>FIMImageOperations.Rotate90Clockwise(i),"旋转90°"));
            Action(pixels,"水平翻转","FlipHorizontal",()=>ProcessImage(i=>FIMImageOperations.FlipHorizontal(i),"水平翻转"));
            Action(pixels,"垂直翻转","FlipVertical",()=>ProcessImage(i=>FIMImageOperations.FlipVertical(i),"垂直翻转"));
            Action(pixels,"ROI裁剪","Crop",CropSelection);
            Action(pixels,"归一化","Normalize",Normalize);
            content.Children.Add(pixels);
            content.Children.Add(Heading("基础处理 / 去噪 / 锐化"));
            content.Children.Add(Row(Label("半径"),_filterRadius,Label("Sigma"),_sigma,Label("锐化量"),_amount));
            _filterRadius.Items.Add(1); _filterRadius.Items.Add(2); _filterRadius.Items.Add(3);
            _filterRadius.SelectedItem = 1;
            WrapPanel filters = Row();
            Action(filters,"均值","Mean",()=>Filter(FIMNeighborhoodFilter.Mean));
            Action(filters,"中值","Median",()=>Filter(FIMNeighborhoodFilter.Median));
            Action(filters,"腐蚀","Erode",()=>Filter(FIMNeighborhoodFilter.Minimum));
            Action(filters,"膨胀","Dilate",()=>Filter(FIMNeighborhoodFilter.Maximum));
            Action(filters,"高斯降噪","Gaussian",Gaussian);
            Action(filters,"锐化","Sharpen",Sharpen);
            content.Children.Add(filters);
            content.Children.Add(Row(Label("阈值范围使用窗位页的上下限")));
            Action(content,"阈值掩码","Threshold",Threshold);
            return Scroll(content);
        }
        private UIElement BuildOutputPage()
        {
            StackPanel content = Column();
            content.Children.Add(Heading("图像 / 结果 / 比较"));
            WrapPanel tabs = Row();
            Action(tabs,"原图","OriginalBottom",()=>_viewer.SelectOriginal());
            Action(tabs,"结果图","ResultBottom",()=>_viewer.SelectResult());
            Action(tabs,"差分图","Difference",Difference);
            content.Children.Add(tabs);
            content.Children.Add(Row(Label("融合比例"),_blendRatio));
            WrapPanel comparisons = Row();
            Action(comparisons,"透明叠合","Blend",Blend);
            Action(comparisons,"退出叠合","BlendOff",()=>_viewer.DisableBlend());
            content.Children.Add(comparisons);
            content.Children.Add(Heading("导出  ·  区分原像素与视口截图"));
            WrapPanel export = Row();
            Action(export,"保存图像PNG","SaveRawPng",Save);
            Action(export,"保存含标注视口","SaveViewPng",SaveViewPng);
            Action(export,"复制图像","Copy",()=>_viewer.CopyToClipboard());
            Action(export,"导出测量/标注CSV","ExportMeasurements",ExportMeasurements);
            content.Children.Add(export);
            content.Children.Add(Heading("RAW / 原始图像输入"));
            content.Children.Add(Row(Label("宽"),_rawWidth,Label("高"),_rawHeight));
            foreach (FIMPixelFormat format in new[] { FIMPixelFormat.Gray8,
                FIMPixelFormat.Gray16, FIMPixelFormat.GrayFloat32,
                FIMPixelFormat.Bgr24, FIMPixelFormat.Bgra32 })
                _rawFormat.Items.Add(format);
            _rawFormat.SelectedItem = FIMPixelFormat.Gray16;
            content.Children.Add(Row(Label("格式"),_rawFormat,_bigEndian));
            Action(content,"读取RAW文件","RawOpen",OpenRawWithDialog);
            content.Children.Add(Hint("RAW须指定尺寸/位深/字节序，最大512MiB；不得从文件名猜测原始格式。"));
            return Scroll(content);
        }
        private void SelectTool(MVIImageToolMode mode)
        {
            foreach (ComboBoxItem item in _imageTool.Items.OfType<ComboBoxItem>())
                if (item.Tag is MVIImageToolMode && (MVIImageToolMode)item.Tag == mode)
                { _imageTool.SelectedItem = item; _displayTabs.SelectedIndex =
                    mode == MVIImageToolMode.Distance || mode == MVIImageToolMode.Angle ||
                    mode == MVIImageToolMode.HorizontalDistance ||
                    mode == MVIImageToolMode.VerticalDistance ||
                    mode == MVIImageToolMode.Diameter || mode == MVIImageToolMode.Area ||
                    mode == MVIImageToolMode.PolygonArea ? 3 :
                    mode == MVIImageToolMode.Point || mode == MVIImageToolMode.Text ||
                    mode == MVIImageToolMode.Arrow || mode == MVIImageToolMode.DefectBox ||
                    mode == MVIImageToolMode.MoveOverlay || mode == MVIImageToolMode.DeleteOverlay ? 4 : 0;
                    return; }
            throw new InvalidOperationException("二维交互工具尚未注册：" + mode);
        }
        private void SaveViewPng()
        {
            Microsoft.Win32.SaveFileDialog save = new Microsoft.Win32.SaveFileDialog {
                Filter = "包含视口与标注的PNG|*.png", DefaultExt = ".png",
                Title = "保存当前视口（含图层）" };
            if (save.ShowDialog() != true) return;
            _viewer.SaveViewPng(save.FileName);
            Show("已保存包含标注的视口截图：" + save.FileName);
        }
    }
}
