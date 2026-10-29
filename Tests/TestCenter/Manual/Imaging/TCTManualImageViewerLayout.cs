using System;
using System.Globalization;
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
        private Slider _gammaSlider, _brightnessSlider, _contrastSlider;
        private readonly CheckBox _comparisonSync = new CheckBox {
            Content = "双视图同步缩放/平移", IsChecked = true, Margin = new Thickness(3) };
        private bool _changingGraySliders;
        private readonly System.Collections.Generic.List<Button> _quickButtons =
            new System.Collections.Generic.List<Button>();
        // 仅保存可视按钮映射；真实工具状态仍以唯一MPW.ToolMode为准。
        private readonly System.Collections.Generic.Dictionary<MVIImageToolMode,Button>
            _regionToolButtons=new System.Collections.Generic.Dictionary<MVIImageToolMode,Button>();
        private readonly System.Collections.Generic.Dictionary<MVIImageOverlayLayer,Button>
            _layerColorButtons=new System.Collections.Generic.Dictionary<MVIImageOverlayLayer,Button>();
        private Slider _windowWidthSlider,_windowCenterSlider;
        private TextBlock _windowWidthMaxLabel,_windowCenterMaxLabel;
        private readonly TextBlock _windowRangeHint=new TextBlock {
            Text="窗宽范围：1～65535；窗位范围：0～65535",
            FontSize=12, Foreground=Brushes.SteelBlue,
            TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,2,4,5) };
        private readonly TextBox _windowWidthInput=new TextBox { MinWidth=85,Width=90,
            Text="65535",Margin=new Thickness(2),HorizontalContentAlignment=HorizontalAlignment.Right };
        private readonly TextBox _windowCenterInput=new TextBox { MinWidth=85,Width=90,
            Text="32767.5",Margin=new Thickness(2),HorizontalContentAlignment=HorizontalAlignment.Right };
        private readonly CheckBox _windowEnabled=new CheckBox { Content="应用窗宽窗位",IsChecked=false,Margin=new Thickness(4) };
        private readonly CheckBox _windowRoi=new CheckBox { Content="框选自动取窗",IsChecked=false,Margin=new Thickness(4) };
        private bool _syncingWindowControls;
        private double _windowLimit=65535;
        private bool _syncingDisplayInputs;
        private readonly TextBlock _windowMode = new TextBlock {
            Text = "当前显示：自动窗宽/窗位", Foreground = Brushes.SteelBlue,
            Margin = new Thickness(5,4,5,4), TextWrapping = TextWrapping.Wrap };
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
            // 工业工作台右侧约344DIP：压紧每项以便四列操作，避免大半功能隐藏于滚动。
            button.MinWidth = 68; button.Width = 68; button.Height = 64;
            button.Margin = new Thickness(3);
            button.Padding = new Thickness(2,3,2,3);
            button.BorderBrush = Paint(207,222,239);
            StackPanel content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(Glyph(id,symbol,23));
            content.Children.Add(new TextBlock { Text = label, FontSize = 12.0,
                Foreground = Paint(52,72,98), HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0,3,0,0) });
            button.Content = content;
            return button;
        }
        private Button RegionTool(Panel host,string symbol,string label,string id,
            MVIImageToolMode mode)
        {
            Button button=Tile(host,symbol,label,id,()=>SelectTool(mode));
            button.ToolTip="点击选择："+label+"。工具会在原图上实际绘制/编辑。";
            _regionToolButtons[mode]=button;
            return button;
        }
         private void UpdateRegionToolHighlight()
        {
             foreach(var entry in _regionToolButtons)
            {
                bool selected=_viewer.ToolMode==entry.Key;
                entry.Value.Background=selected ? Paint(218,237,255) : Brushes.White;
                entry.Value.BorderBrush=selected ? Paint(30,121,217) : Paint(207,222,239);
                entry.Value.BorderThickness=new Thickness(selected ? 1.8 : 1);
                AutomationProperties.SetHelpText(entry.Value,selected ?
                    "当前已选择，下一步在图像上操作。" : "点击切换操作工具。");
            }
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
            content.Children.Add(Heading("图层显示与交互"));
            content.Children.Add(Hint("工具在【区域】【测量】【标注】等专业页签及顶部快捷栏操作，左侧图像直接绘制。"));
            content.Children.Add(Heading("图层默认颜色 · 点击弹出系统色板"));
            WrapPanel palettes=Row();
            foreach(var entry in new[] {
                Tuple.Create("ROI",MVIImageOverlayLayer.Roi),
                Tuple.Create("测量",MVIImageOverlayLayer.Measurement),
                Tuple.Create("标注",MVIImageOverlayLayer.Annotation),
                Tuple.Create("缺陷",MVIImageOverlayLayer.Defect) })
            {
                MVIImageOverlayLayer layer=entry.Item2;
                Button pick=Action(palettes,"设"+entry.Item1+"色","OverlayColor."+entry.Item1,
                    ()=>ChooseOverlayLayerColor(layer));
                pick.Width=69;pick.MinHeight=32;pick.Height=32;
                pick.Padding=new Thickness(1,2,1,2);
                uint value=unchecked((uint)_viewer.GetOverlayLayerColor(layer));
                pick.Background=new SolidColorBrush(Color.FromArgb(
                    (byte)(value>>24),(byte)(value>>16),(byte)(value>>8),(byte)value));
                pick.Foreground=Brushes.White;
                pick.ToolTip="点击打开系统颜色对话框，修改"+entry.Item1+"图层默认色及未锁定图元颜色";
                _layerColorButtons[layer]=pick;
            }
            content.Children.Add(palettes);
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
            AutomationProperties.SetAutomationId(_imageTool,"TCT.Imaging.ImageTool");
            _imageTool.MinHeight = 32; _imageTool.Margin = new Thickness(3,0,3,7);
            content.Children.Add(_imageTool);
            content.Children.Add(Hint("工具、ROI和标注直接操作既有 MVI OverlayStore。"));
            return Scroll(content);
        }
        private UIElement BuildWindowPage()
        {
            StackPanel content=Column();
            content.Children.Add(Heading("窗宽 / 窗位 · 原始灰度映射"));
            AutomationProperties.SetAutomationId(_windowEnabled,"TCT.Imaging.WindowEnabled");
            AutomationProperties.SetAutomationId(_windowRoi,"TCT.Imaging.WindowRoi");
            content.Children.Add(Row(_windowEnabled,_windowRoi));
            content.Children.Add(WindowSliderRow("窗宽",_windowWidthInput,true,out _windowWidthSlider));
            content.Children.Add(WindowSliderRow("窗位",_windowCenterInput,false,out _windowCenterSlider));
            AutomationProperties.SetAutomationId(_windowRangeHint,"TCT.Imaging.WindowRange");
            content.Children.Add(_windowRangeHint);
            _windowEnabled.Checked+=(sender,args)=>{if(!_syncingWindowControls) ApplyWindowFromSlider();};
            _windowEnabled.Unchecked+=(sender,args)=>{if(!_syncingWindowControls) ResetWindowOnly();};
            WrapPanel actions=Row();
            Action(actions,"应用窗宽窗位","Window",Window);
            Action(actions,"刷新窗值","AutoWindow",AutoWindow);
            Action(actions,"恢复自动","ResetWindow",ResetWindowOnly);
            content.Children.Add(actions);
            content.Children.Add(_windowMode);
            content.Children.Add(Hint("拖动滑条松开即生效；可输入窗宽/窗位按回车。勾选框选自动取窗后点刷新窗值。"));
            Expander advanced=new Expander { Header="高级：直接设置窗下限与上限",Margin=new Thickness(3),IsExpanded=false };
            StackPanel limits=Column();
            AutomationProperties.SetAutomationId(_low,"TCT.Imaging.WindowLow");
            AutomationProperties.SetAutomationId(_high,"TCT.Imaging.WindowHigh");
            limits.Children.Add(Row(Label("下限"),_low,Label("上限"),_high));
            Action(limits,"按上下限应用","WindowLimits",ApplyWindowFromLimits);
            advanced.Content=limits; content.Children.Add(advanced);
            content.Children.Add(Heading("灰度参考与快速检查"));
            WrapPanel more=Row();
            Action(more,"灰度统计","AnalyzeWindow",Calculate);
            Action(more,"灰度剖面","ProfileWindow",ShowLineProfile);
            content.Children.Add(more);
            return Scroll(content);
        }
        private UIElement WindowSliderRow(string label,TextBox field,bool isWidth,out Slider slider)
        {
            Grid row=new Grid { Margin=new Thickness(2,9,2,7) };
            row.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(37) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(94) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star),
                MinWidth=50 });
            row.Children.Add(Label(label));
            slider=new Slider {
                Minimum=isWidth?1:0, Maximum=_windowLimit,
                Value=isWidth?_windowLimit:_windowLimit/2,
                SmallChange=1,LargeChange=100,
                AutoToolTipPlacement=System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft,
                AutoToolTipPrecision=1,Margin=new Thickness(4,0,6,0),
                VerticalAlignment=VerticalAlignment.Center,MinWidth=74 };
            Slider current=slider;
            AutomationProperties.SetAutomationId(current,isWidth?
                "TCT.Imaging.WindowWidthSlider":"TCT.Imaging.WindowCenterSlider");
            AutomationProperties.SetAutomationId(field,isWidth?
                "TCT.Imaging.WindowWidth":"TCT.Imaging.WindowCenter");
            current.ValueChanged+=(sender,args)=>{
                if(_syncingWindowControls)return;
                field.Text=current.Value.ToString("0.###",CultureInfo.InvariantCulture);
                SyncWindowLimitsFromCenterWidth();
            };
            current.AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
                new System.Windows.Controls.Primitives.DragCompletedEventHandler(
                    (sender,args)=>ApplyWindowFromSlider()));
            current.PreviewMouseLeftButtonUp+=(sender,args)=>ApplyWindowFromSlider();
            current.KeyUp+=(sender,args)=>ApplyWindowFromSlider();
            field.KeyDown+=(sender,args)=>{
                if(args.Key==Key.Enter){ApplyWindowFromSlider();args.Handled=true;}
            };
            Grid.SetColumn(field,1);row.Children.Add(field);
            Grid.SetColumn(current,2);row.Children.Add(current);
            // 端点刻度贴在对应滑条下方，随Gray8/Gray16像素范围更新。
            Grid ticks=new Grid { Margin=new Thickness(6,0,7,0) };
            ticks.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
            ticks.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            TextBlock minimum=new TextBlock {
                Text=isWidth?"1":"0",FontSize=11,Foreground=Paint(100,119,141),
                HorizontalAlignment=HorizontalAlignment.Left };
            TextBlock maximum=new TextBlock {
                Text=_windowLimit.ToString("0.###",CultureInfo.InvariantCulture),
                FontSize=11,Foreground=Paint(100,119,141),
                HorizontalAlignment=HorizontalAlignment.Right };
            if(isWidth)_windowWidthMaxLabel=maximum;else _windowCenterMaxLabel=maximum;
            AutomationProperties.SetAutomationId(maximum,isWidth?
                "TCT.Imaging.WindowWidthMax":"TCT.Imaging.WindowCenterMax");
            ticks.Children.Add(minimum);
            Grid.SetColumn(maximum,1);ticks.Children.Add(maximum);
            Grid.SetRow(ticks,1);Grid.SetColumn(ticks,2);row.Children.Add(ticks);
            return row;
        }
        private UIElement BuildGrayPage()
        {
            StackPanel content = Column();
            content.Children.Add(Heading("灰度拉伸与显示增强"));
            content.Children.Add(Row(_autoContrast,_displayInverted));
            content.Children.Add(GraySliderRow("Gamma",_displayGamma,0.1,3.0,1.0,out _gammaSlider));
            content.Children.Add(GraySliderRow("亮度",_displayBrightness,-255,255,0.0,out _brightnessSlider));
            content.Children.Add(GraySliderRow("对比度",_displayContrast,0.0,3.0,1.0,out _contrastSlider));
            AutomationProperties.SetAutomationId(_displayGamma,"TCT.Imaging.Gamma");
            AutomationProperties.SetAutomationId(_displayBrightness,"TCT.Imaging.Brightness");
            AutomationProperties.SetAutomationId(_displayContrast,"TCT.Imaging.Contrast");
            AutomationProperties.SetAutomationId(_displayInverted,"TCT.Imaging.Inverted");
            AutomationProperties.SetAutomationId(_autoContrast,"TCT.Imaging.AutoContrast");
            foreach (CheckBox check in new[] { _displayInverted,_autoContrast })
            {
                check.Checked += (sender,args) => { if (!_syncingDisplayInputs) ApplyGraySliderChange(); };
                check.Unchecked += (sender,args) => { if (!_syncingDisplayInputs) ApplyGraySliderChange(); };
            }
            WrapPanel buttons = Row();
            Action(buttons,"应用灰阶参数","ApplyDisplay",ApplyDisplay);
            Action(buttons,"全部复位","ResetDisplay",ResetDisplay);
            content.Children.Add(buttons);
            content.Children.Add(Hint("Gamma 0.1～3.0；亮度 -255～255；对比度 0～3.0。滑动松开或输入回车后更新，不改原图。"));
            return Scroll(content);
        }
        /// <summary>滑块只在鼠标释放/键盘确认时提交处理，避免大图频繁同步重绘。</summary>
        private UIElement GraySliderRow(string title, TextBox field, double min, double max,
            double initial, out Slider slider)
        {
            Grid row = new Grid { Margin = new Thickness(2,7,2,7) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1,GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
            TextBlock label = Label(title);
            row.Children.Add(label);
            slider = new Slider { Minimum = min, Maximum = max, Value = initial,
                SmallChange = title == "亮度" ? 1 : .1,
                LargeChange = title == "亮度" ? 10 : .2,
                AutoToolTipPlacement = System.Windows.Controls.Primitives.AutoToolTipPlacement.TopLeft,
                AutoToolTipPrecision = 2, Margin = new Thickness(7,0,8,0),
                VerticalAlignment = VerticalAlignment.Center, MinWidth = 100,
                IsSnapToTickEnabled = false };
            Slider current = slider;
            AutomationProperties.SetAutomationId(current,"TCT.Imaging.Slider."+title);
            current.ValueChanged += (sender,args) =>
            {
                if (!_changingGraySliders)
                    field.Text = current.Value.ToString(title=="亮度"?"0":"0.###",CultureInfo.InvariantCulture);
            };
            current.AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
                new System.Windows.Controls.Primitives.DragCompletedEventHandler(
                    (sender,args) => ApplyGraySliderChange()));
            current.PreviewMouseLeftButtonUp += (sender,args) => ApplyGraySliderChange();
            current.KeyUp += (sender,args) => ApplyGraySliderChange();
            field.KeyDown += (sender,args) =>
            {
                if(args.Key==Key.Enter) { ApplyGraySliderChange(); args.Handled=true; }
            };
            Grid.SetColumn(current,3);row.Children.Add(current);
            field.Width=50;field.FontSize=12;
            Grid.SetColumn(field,1);row.Children.Add(field);
            TextBlock minimum=Label(min.ToString("0.##",CultureInfo.InvariantCulture));
            minimum.FontSize=11;minimum.Foreground=Brushes.Gray;
            Grid.SetColumn(minimum,2);row.Children.Add(minimum);
            TextBlock maximum=Label(max.ToString("0.##",CultureInfo.InvariantCulture));
            maximum.FontSize=11;maximum.Foreground=Brushes.Gray;
            Grid.SetColumn(maximum,4);row.Children.Add(maximum);
            return row;
        }
        private void ApplyGraySliderChange()
        {
            try { ApplyDisplay(); }
            catch (Exception error) { Show("灰阶参数错误：" + error.Message); }
        }
        private void SyncGraySliders()
        {
            if (_gammaSlider == null || _brightnessSlider == null || _contrastSlider == null) return;
            _changingGraySliders = true;
            try
            {
                _gammaSlider.Value = Math.Max(_gammaSlider.Minimum,
                    Math.Min(_gammaSlider.Maximum,_viewer.DisplayState.Gamma));
                _brightnessSlider.Value = Math.Max(_brightnessSlider.Minimum,
                    Math.Min(_brightnessSlider.Maximum,_viewer.DisplayState.Brightness*255));
                _contrastSlider.Value = Math.Max(_contrastSlider.Minimum,
                    Math.Min(_contrastSlider.Maximum,_viewer.DisplayState.Contrast));
            }
            finally { _changingGraySliders = false; }
        }

        private UIElement BuildMeasurePage()
        {
            StackPanel content = Column();
            content.Children.Add(Heading("图像测量  ·  原图坐标"));
            WrapPanel tiles = Row();
            Tile(tiles,"↗","距离","MeasureDistance",()=>SelectTool(MVIImageToolMode.Distance));
            Tile(tiles,"∠","角度","MeasureAngle",()=>SelectTool(MVIImageToolMode.Angle));
            Tile(tiles,"⋈","两线测量","MeasureLinePair",()=>SelectTool(MVIImageToolMode.LinePair));
            Tile(tiles,"↔","水平距离","MeasureHorizontal",()=>SelectTool(MVIImageToolMode.HorizontalDistance));
            Tile(tiles,"↕","垂直距离","MeasureVertical",()=>SelectTool(MVIImageToolMode.VerticalDistance));
            Tile(tiles,"◯","直径","MeasureDiameter",()=>SelectTool(MVIImageToolMode.Diameter));
            Tile(tiles,"□","矩形面积","MeasureArea",()=>SelectTool(MVIImageToolMode.Area));
            Tile(tiles,"⬡","多边形面积","MeasurePolygonArea",()=>SelectTool(MVIImageToolMode.PolygonArea));
            content.Children.Add(tiles);
            content.Children.Add(Hint("线段拖拽得到距离；角度依次点击三点；两线测量依次点击第一线起点/终点、第二线起点/终点四点，自动绘制最短连线和夹角。"));
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
            RegionTool(tiles,"□","矩形","RegionRectangle",MVIImageToolMode.RectangleRoi);
            RegionTool(tiles,"◇","旋转矩形","RegionRotatedRectangle",MVIImageToolMode.RotatedRectangleRoi);
            RegionTool(tiles,"↻","旋转角柄","RotateOverlay",MVIImageToolMode.RotateOverlay);
            RegionTool(tiles,"◯","圆形","RegionCircle",MVIImageToolMode.CircleRoi);
            RegionTool(tiles,"⬭","椭圆","RegionEllipse",MVIImageToolMode.EllipseRoi);
            RegionTool(tiles,"⬡","多边形","RegionPolygon",MVIImageToolMode.PolygonRoi);
            RegionTool(tiles,"↔","八柄缩放","ResizeOverlay",MVIImageToolMode.ResizeOverlay);
            RegionTool(tiles,"⬡","逐顶点编辑","EditPolygonVertex",MVIImageToolMode.EditPolygonVertex);
            content.Children.Add(tiles);
            AutomationProperties.SetAutomationId(_polygonVertexIndex,
                "TCT.Imaging.PolygonVertexIndex");
            content.Children.Add(Row(Label("多边形边/顶点索引（从0开始）"),_polygonVertexIndex));
            WrapPanel topology = Row();
            Action(topology,"边中点插入","InsertPolygonVertex",InsertPolygonVertexAtIndex);
            Action(topology,"删除此顶点","RemovePolygonVertex",RemovePolygonVertexAtIndex);
            content.Children.Add(topology);
            WrapPanel buttons = Row();
            Action(buttons,"清除选框","RemoveRoi",()=>_viewer.ClearSelection());
            Action(buttons,"删除选中","DeleteOverlay",DeleteSelectedOverlay);
            Action(buttons,"左移 1px","MoveOverlay",()=>MoveSelectedOverlay(-1,0));
            Action(buttons,"锁定/解锁","ToggleOverlayLock",ToggleSelectedOverlayLock);
            Action(buttons,"上移层级","BringOverlayForward",()=>MoveSelectedZOrder(1));
            Action(buttons,"下移层级","SendOverlayBackward",()=>MoveSelectedZOrder(-1));
            Action(buttons,"隐藏/显示层","LayerVisibility",ToggleSelectedLayer);
            Action(buttons,"显示所有层","ShowAllLayers",ShowAllLayers);
            content.Children.Add(buttons);
            AutomationProperties.SetAutomationId(_overlayList,"TCT.Imaging.OverlayList");
            content.Children.Add(_overlayList);
            content.Children.Add(Heading("选中对象属性（实际保存）"));
            AutomationProperties.SetAutomationId(_overlayArgb,"TCT.Imaging.OverlayArgb");
            AutomationProperties.SetAutomationId(_overlayLineWidth,"TCT.Imaging.OverlayLineWidth");
            AutomationProperties.SetAutomationId(_overlayLabel,"TCT.Imaging.OverlayLabel");
            content.Children.Add(Row(Label("ARGB"),_overlayArgb,Label("线宽(px)"),_overlayLineWidth));
            content.Children.Add(Row(Label("文字/标签"),_overlayLabel));
            Action(content,"选择颜色…","ChooseOverlayColor",ChooseOverlayColor);
            Action(content,"应用对象属性","ApplyOverlayProperties",ApplyOverlayProperties);
            content.Children.Add(Hint("先在图像或列表选中图元，点“选择颜色…”打开系统颜色对话框；取消不修改对象。"));
            content.Children.Add(Hint("逐顶点模式：直接拖金色点移动；Shift＋点击边线插点；Alt＋点击金色点删点，或按索引使用上方按钮。锁定对象和自交/退化图形禁止修改；所有工具仍共享MVI原图坐标Store。"));
            return Scroll(content);
        }
        private UIElement BuildStatisticsPage()
        {
            Grid content = new Grid { Margin = new Thickness(9,7,9,7) };
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160) });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1,GridUnitType.Star),
                MinHeight = 70 });
            AutomationProperties.SetAutomationId(_withRoi,"TCT.Imaging.UseSelectedRoi");
            AutomationProperties.SetAutomationId(_info,"TCT.Imaging.StatisticsSummary");
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
            Action(top,"绘制剖面线","DrawLineProfile",()=>SelectTool(MVIImageToolMode.LineProfile));
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
            content.Children.Add(Heading("阈值分割  ·  原图灰度 / 独立结果"));
            AutomationProperties.SetAutomationId(_thresholdLow,"TCT.Imaging.ThresholdLow");
            AutomationProperties.SetAutomationId(_thresholdHigh,"TCT.Imaging.ThresholdHigh");
            content.Children.Add(Row(Label("下限"),_thresholdLow,Label("上限"),_thresholdHigh));
            WrapPanel thresholdActions=Row();
            Action(thresholdActions,"全图阈值掩码","Threshold",Threshold);
            Action(thresholdActions,"按选中ROI分割","ThresholdSelectedRoi",ThresholdSelectedRoi);
            content.Children.Add(thresholdActions);
            Action(content,"提取结果掩码轮廓","ExtractMaskContours",ExtractResultMaskContours);
            content.Children.Add(Hint("选矩形、圆、椭圆、旋转矩形或多边形ROI后按阈值生成掩码；严格按原图像素中心判断，不受显示窗位影响。提取轮廓会把外环、孔洞分别保存为可选中的标注图元，像素面积仍以原掩码为准。"));
            content.Children.Add(Heading("专业CPU检测  ·  FIM → MRK → ResultItem"));
            AutomationProperties.SetAutomationId(_cpuThreshold,"TCT.Imaging.CpuThreshold");
            AutomationProperties.SetAutomationId(_cpuMinArea,"TCT.Imaging.CpuMinArea");
            content.Children.Add(Row(Label("灰度阈值"),_cpuThreshold,
                Label("最小面积(px²)"),_cpuMinArea));
            _cpuMedianRadius.Items.Clear();
            for(int radius=0;radius<=3;radius++)_cpuMedianRadius.Items.Add(radius);
            _cpuMedianRadius.SelectedItem=0;
            AutomationProperties.SetAutomationId(_cpuMedianRadius,"TCT.Imaging.CpuMedianRadius");
            content.Children.Add(Row(Label("拆分式增强半径(0=关闭)"),_cpuMedianRadius));
            WrapPanel cpuActions=Row();
            Action(cpuActions,"运行CPU检测 → Mask","CpuVisionRun",RunCpuVision);
            Action(cpuActions,"运行拆分式工程链","CpuVisionSplitRun",RunCpuSeparatedVision);
            Action(cpuActions,"取消CPU检测","CpuVisionCancel",CancelCpuVision);
            content.Children.Add(cpuActions);
            content.Children.Add(Hint("仅接受Gray8/Gray16原图。原CPU按钮保留旧MRK结果；拆分式按钮使用真实MEM→MOC→MRK链，"
                +"将FIM内存图像作为输入，可选FIM中值增强，再独立阈值Mask→连通区域→面积测量。"
                +"两者共用结果列表、原图和图窗，切图/改参数会取消旧任务与拒绝过期结果。"));

            content.Children.Add(Heading("区域生长  ·  原图矩形ROI"));
            AutomationProperties.SetAutomationId(_growTolerance,"TCT.Imaging.GrowTolerance");
            AutomationProperties.SetAutomationId(_growEightConnected,"TCT.Imaging.GrowEightConnected");
            content.Children.Add(Row(Label("灰度容差"),_growTolerance,_growEightConnected));
            Action(content,"按ROI中心种子生长","RegionGrow",RegionGrow);
            content.Children.Add(Hint("先在“区域”页画/选矩形ROI，以其中点为种子；按原图灰度和4/8连通生长，生成独立Gray8掩码与像素范围，可在输出页保存。"));
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
            content.Children.Add(Row(Label("融合/分界比例 0～1"),_blendRatio));
            WrapPanel comparisons = Row();
            Action(comparisons,"透明叠合","Blend",Blend);
            Action(comparisons,"滑动比较","Swipe",Swipe);
            Action(comparisons,"左右双视图","SideBySide",SideBySide);
            Action(comparisons,"退出叠合","BlendOff",()=>_viewer.DisableBlend());
            content.Children.Add(comparisons);
            AutomationProperties.SetAutomationId(_comparisonSync,"TCT.Imaging.ComparisonSync");
            _comparisonSync.Checked += (s,e) => _viewer.SetComparisonSynchronization(true);
            _comparisonSync.Unchecked += (s,e) => _viewer.SetComparisonSynchronization(false);
            content.Children.Add(Row(_comparisonSync));
            content.Children.Add(Heading("本次CPU视觉结果  ·  Image / Mask / Region / Contour / Measurement"));
            Action(content,"从本次Run生成轮廓结果项","CpuGenerateContours",GenerateCpuContourResults);
            AutomationProperties.SetAutomationId(_cpuResultItems,"TCT.Imaging.CpuResultItems");
            content.Children.Add(_cpuResultItems);
            content.Children.Add(Hint("点选Image/Mask显示真实图像；点选Contour在原图上高亮只读外环或孔洞，"
                +"仍保留RunId与SourceId。切图、改检测参数或重新运行会清理旧Run临时轮廓，"
                +"用户手绘ROI不受影响。轮廓不是正式工程ROI或标定毫米面积。"));
            content.Children.Add(Heading("导出  ·  区分原像素与视口截图"));
            WrapPanel export = Row();
            Action(export,"保存图像PNG","SaveRawPng",Save);
            Action(export,"导出无损原像素RAW","ExportActiveRaw",ExportActiveRawWithDialog);
            Action(export,"保存含标注视口","SaveViewPng",SaveViewPng);
            Action(export,"复制图像","Copy",()=>_viewer.CopyToClipboard());
            Action(export,"导出测量/标注CSV","ExportMeasurements",ExportMeasurements);
            content.Children.Add(export);
            content.Children.Add(Hint("RAW导出保存当前活动原图或结果图的真实像素，不包含窗位、伪彩、ROI或视口叠加。默认紧排小端；请记录宽高/像素格式/Stride后再导入，"
                +"彩色通道保持BGR/BGRA原序。PNG视图与原像素是两种不同导出用途。"));
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
                {
                    _imageTool.SelectedItem = item;
                    if (_viewer.ToolMode != mode) _viewer.ToolMode = mode;
                    _displayTabs.SelectedIndex =
                    mode == MVIImageToolMode.Distance || mode == MVIImageToolMode.Angle ||
                    mode == MVIImageToolMode.HorizontalDistance ||
                    mode == MVIImageToolMode.VerticalDistance ||
                    mode == MVIImageToolMode.LinePair ||
                    mode == MVIImageToolMode.LineProfile ||
                    mode == MVIImageToolMode.Diameter || mode == MVIImageToolMode.Area ||
                    mode == MVIImageToolMode.PolygonArea ? 3 :
                    mode == MVIImageToolMode.Point || mode == MVIImageToolMode.Text ||
                    mode == MVIImageToolMode.Arrow || mode == MVIImageToolMode.DefectBox ||
                    mode == MVIImageToolMode.MoveOverlay || mode == MVIImageToolMode.DeleteOverlay ? 4 : 0;
                    UpdateRegionToolHighlight();
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
