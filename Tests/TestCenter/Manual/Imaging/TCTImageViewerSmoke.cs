using System;
using System.IO;
using System.Linq;
using AvalonDock.Layout;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Infrastructure.Logging;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Visual;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 真实Windows STA/WPF控件构造、窗口挂载、FIM/MVI状态、PNG/ROI像素回环。
    /// 不模拟工业相机/HALCON运行，不以此冒充人工交互或设备验收。
    /// </summary>
    internal static class TCTImageViewerSmoke
    {
        private static void AssertVisualLayout(TCTManualImageViewerFrame frame, Window window)
        {
            string[] top = {"显示","窗位","灰阶","测量","标注"};
            string[] bottom = {"区域","统计","剖面","变换","输出"};
            if (!top.SequenceEqual(frame.DisplayTabTitles) ||
                !bottom.SequenceEqual(frame.AnalysisTabTitles) ||
                frame.QuickActionCount < 14)
                throw new InvalidOperationException("二维工作台顶栏或上下五页签配置不完整。");

            TabControl primary = FindControl<TabControl>(frame,"TCT.Imaging.PrimaryTabs");
            TabControl secondary = FindControl<TabControl>(frame,"TCT.Imaging.SecondaryTabs");
            if (primary == null || secondary == null)
                throw new InvalidOperationException("真实WPF双区页签不存在。");
            Button fit = FindControl<Button>(frame,"TCT.Imaging.Fit");
            if (fit == null) throw new InvalidOperationException("高频适应窗口操作缺失。");
            // 真正触发按钮Click及原先异常处理，而不是直接调用业务层。
            fit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            primary.SelectedIndex = 3;
            secondary.SelectedIndex = 3;
            window.UpdateLayout();
            if (FindControl<Button>(frame,"TCT.Imaging.MeasureAngle") == null ||
                FindControl<Button>(frame,"TCT.Imaging.MeasureDiameter") == null ||
                FindControl<Button>(frame,"TCT.Imaging.MeasurePolygonArea") == null ||
                FindControl<Button>(frame,"TCT.Imaging.Gaussian") == null)
                throw new InvalidOperationException("测量/变换功能页缺少真实操作按钮。");
            primary.SelectedIndex = 4;
            secondary.SelectedIndex = 4;
            window.UpdateLayout();
            if (FindControl<Button>(frame,"TCT.Imaging.DefectBox") == null ||
                FindControl<Button>(frame,"TCT.Imaging.SideBySide") == null ||
                FindControl<CheckBox>(frame,"TCT.Imaging.ComparisonSync") == null ||
                FindControl<Button>(frame,"TCT.Imaging.SaveViewPng") == null ||
                FindControl<Button>(frame,"TCT.Imaging.RawOpen") == null)
                throw new InvalidOperationException("标注/输出页遗失旧操作入口。");
            primary.SelectedIndex = 0;
            secondary.SelectedIndex = 0;
            window.UpdateLayout();
            Button rectTool=FindControl<Button>(frame,"TCT.Imaging.RegionRectangle");
            Button circleTool=FindControl<Button>(frame,"TCT.Imaging.RegionCircle");
            Button vertexTool=FindControl<Button>(frame,"TCT.Imaging.EditPolygonVertex");
            if(rectTool==null || circleTool==null || vertexTool==null ||
                rectTool.Width>72 || circleTool.Width>72 || vertexTool.Width>72)
                throw new InvalidOperationException(
                    "ROI工具仍是拥挤的三列大按钮，未切换紧凑工作台布局");
            rectTool.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(frame.Viewer.ToolMode!=MVIImageToolMode.RectangleRoi ||
                rectTool.BorderThickness.Left<1.7)
                throw new InvalidOperationException("点击矩形工具没有真实激活或选中反馈");
            circleTool.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(frame.Viewer.ToolMode!=MVIImageToolMode.CircleRoi ||
                circleTool.BorderThickness.Left<1.7 ||
                rectTool.BorderThickness.Left>1.2)
                throw new InvalidOperationException("区域工具互斥切换未清除旧高亮状态");
            primary.SelectedIndex=0;
            window.UpdateLayout();
            if(FindControl<Button>(frame,"TCT.Imaging.SelectMode.ROI")!=null)
                throw new InvalidOperationException("显示页仍保留重复的绘图类别按钮");
            foreach(string layerName in new[]{"ROI","测量","标注","缺陷"})
                if(FindControl<Button>(frame,"TCT.Imaging.OverlayColor."+layerName)==null)
                    throw new InvalidOperationException("原WinForms四图层颜色选择按钮未移植："+layerName);
            ComboBox selector=FindControl<ComboBox>(frame,"TCT.Imaging.ImageTool");
            if(selector==null)
                throw new InvalidOperationException("精简显示页后未保留完整工具选择控件");
            fit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Console.WriteLine("PASS TCT Imaging UI: 15+ one-row commands, dual five-tab panels, controls and action access");
        }

        private static T FindControl<T>(DependencyObject root,string id) where T : DependencyObject
        {
            T control = root as T;
            if (control != null && AutomationProperties.GetAutomationId(control) == id)
                return control;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                T child = FindControl<T>(VisualTreeHelper.GetChild(root,i),id);
                if (child != null) return child;
            }
            return null;
        }

        /// <summary>
        /// 实际运行TestCenter Shell+AvalonDock，校验二维专业区聚焦、
        /// 通用Inspector自动折叠/切页恢复与宿主真实尺寸，并保存合成截图。
        /// </summary>
        private static void VerifyTestCenterShell()
        {
            TCTMainWindow host = new TCTMainWindow { Width = 1600, Height = 900,
                ShowInTaskbar = false, WindowStyle = WindowStyle.ToolWindow };
            try
            {
                host.Show();
                host.UpdateLayout();
                TCTWorkspaceRegistry registry = TCTWorkspaceRegistry.CreateDefaults(
                    id => new UserControl());
                if (host.IsTestConsoleVisible)
                    throw new InvalidOperationException("专业工作台默认不应有全局测试控制台");
                TCTWorkspaceDescriptor architectureInstance = new TCTWorkspaceDescriptor(
                    "auto.architecture-smoke.smoke", "auto.architecture-smoke",
                    "ArchitectureSmoke测试实例", "独立进程架构回归实例",
                    TCTWorkspaceNodeKind.Leaf, () => new TCTArchitectureSmokeFrame(),
                    TCTWorkspaceOpenPolicy.SingleInstance, "auto.architecture-smoke");
                TCTArchitectureSmokeFrame architecture = host.DocumentHost.OpenWorkspace(
                    architectureInstance) as TCTArchitectureSmokeFrame;
                host.UpdateLayout();
                if (architecture == null || !host.IsTestConsoleVisible ||
                    !host.TestConsolePanel.IsVisible || !host.OperationDock.IsVisible ||
                    host.ManualActionHost.Visibility != Visibility.Visible ||
                    string.IsNullOrEmpty(host.Inspector.ArchitectureSmokeSummary) ||
                    !host.Inspector.ArchitectureSmokeSummary.Contains("尚未运行") ||
                    FindControl<Button>(host,"TCT.Automated.Action.architecture.run") == null)
                    throw new InvalidOperationException("ArchitectureSmoke应在同一个大Frame中使用真实测试控制台");
                string path = Environment.GetEnvironmentVariable("CXVISUAL_IMAGING_UI_SCREENSHOT");
                if (!string.IsNullOrWhiteSpace(path))
                    CaptureWindow(host,Path.Combine(Path.GetDirectoryName(path),"testcenter-auto-console.png"));
                // 使用真实TCTTestPlanControl工作区，而不借自动测试根目录代替计划页。
                TCTTestPlanControl plan = host.DocumentHost.OpenWorkspace(
                    new TCTWorkspaceDescriptor("auto.plan.smoke", "auto.catalog", "NUnit/TCT测试计划验收",
                        "测试工作台计划", TCTWorkspaceNodeKind.Leaf,
                        () => new TCTTestPlanControl(),
                        TCTWorkspaceOpenPolicy.SingleInstance, "auto.catalog")) as TCTTestPlanControl;
                host.UpdateLayout();
                if (plan == null || !host.IsTestConsoleVisible ||
                    host.AutoActionHost.Visibility != Visibility.Visible ||
                    host.ManualActionHost.Visibility != Visibility.Visible ||
                    FindControl<Button>(host,"TCT.Manual.Action.plan.runselected") == null ||
                    host.Inspector.ArchitectureSmokeSummary != null ||
                    !host.TestConsolePanel.IsVisible)
                    throw new InvalidOperationException("NUnit/TCT计划未与ArchitectureSmoke共享测试工作台Frame");
                host.DocumentHost.OpenWorkspace(registry.Find("auto.root"));
                host.UpdateLayout();
                if (!host.IsTestConsoleVisible ||
                    host.AutoActionHost.Visibility != Visibility.Visible)
                    throw new InvalidOperationException("自动测试根工作区应使用测试控制台");
                TCTManualImageViewerFrame imaging = host.DocumentHost.OpenWorkspace(
                    registry.Find("manual.imaging")) as TCTManualImageViewerFrame;
                LayoutDocumentPane documentPane = host.TestDock.Layout.Descendents()
                    .OfType<LayoutDocumentPane>().FirstOrDefault(p => p.Children
                        .OfType<LayoutDocument>().Any(d => d.ContentId == "test.documents"));
                host.UpdateLayout();
                if (imaging == null || host.IsTestConsoleVisible ||
                    host.TestConsolePanel.IsVisible || documentPane == null ||
                    documentPane.ShowHeader)
                    throw new InvalidOperationException("二维专业工作区不得受测试控制台或冗余文档标签挤压");
                imaging.SetOriginal(new FIMImageBuffer(900,650,FIMPixelFormat.Gray8,
                    BuildSyntheticPixels(900,650)));
                host.UpdateLayout();
                if (!string.IsNullOrWhiteSpace(path))
                    CaptureWindow(host,Path.Combine(Path.GetDirectoryName(path),"testcenter-workbench.png"));
                host.MenuShowOperations.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                host.UpdateLayout();
                if (!host.IsTestConsoleVisible)
                    throw new InvalidOperationException("专业工作区应允许按需打开测试控制台");
                host.HideTestConsoleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                host.UpdateLayout();
                if (host.IsTestConsoleVisible)
                    throw new InvalidOperationException("手动收起测试控制台后工作区没有释放宽度");
                host.DocumentHost.OpenWorkspace(architectureInstance);
                host.UpdateLayout();
                if (!host.IsTestConsoleVisible)
                    throw new InvalidOperationException("切回ArchitectureSmoke时未恢复控制台");
                // 模拟用户拖动右侧GridSplitter，切出再切入不能恢复到固定320px。
                host.TestConsoleColumn.Width = new GridLength(412);
                host.DocumentHost.ShowMode("概览");
                host.UpdateLayout();
                if (host.IsTestConsoleVisible)
                    throw new InvalidOperationException("切回概览且没有正在执行的Run时，测试控制台不应持续占宽度");
                host.DocumentHost.OpenWorkspace(architectureInstance);
                host.UpdateLayout();
                if (!host.IsTestConsoleVisible ||
                    Math.Abs(host.TestConsoleColumn.Width.Value - 412.0) > 0.1)
                    throw new InvalidOperationException("自动测试页切换后没有恢复用户调整的测试控制台宽度");
                Console.WriteLine("PASS TCT automated Frame: NUnit/TCT and ArchitectureSmoke console; professional focus, menu toggle, actual UI");
            }
            finally
            {
                host.Close();
            }
        }
        private static byte[] BuildSyntheticPixels(int width,int height)
        {
            byte[] pixels = new byte[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    double dx = (x - width * 0.47) / (width * .31);
                    double dy = (y - height * .47) / (height * .33);
                    double outer = dx*dx + dy*dy;
                    int value = (int)(45 + x*0.09 + y*0.07);
                    if (outer < 1.0) value += 65;
                    if (Math.Abs(dx) < 0.45 && Math.Abs(dy) < 0.38) value -= 35;
                    pixels[y*width+x] = (byte)Math.Max(0,Math.Min(255,value));
                }
            return pixels;
        }
        private static void CaptureWindow(Window window,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            window.UpdateLayout();
            int w = (int)Math.Ceiling(window.ActualWidth);
            int h = (int)Math.Ceiling(window.ActualHeight);
            RenderTargetBitmap bitmap = new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);
            bitmap.Render(window);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream file = new FileStream(path,FileMode.Create,FileAccess.Write))
                encoder.Save(file);
            Console.WriteLine("PASS TCT Imaging UI screenshot (synthetic, no factory image): "+path);
        }

        private static void CaptureWorkbenchIfRequested(TCTManualImageViewerFrame frame, Window window)
        {
            string path = Environment.GetEnvironmentVariable("CXVISUAL_IMAGING_UI_SCREENSHOT");
            if (string.IsNullOrWhiteSpace(path)) return;
            int width = 900, height = 650;
            FIMImageBuffer sample = new FIMImageBuffer(width,height,FIMPixelFormat.Gray8,
                BuildSyntheticPixels(width,height));
            frame.SetOriginal(sample);
            frame.Viewer.FitToWindow();
            CaptureWindow(window,path);
            // 功能验收附图：真实图层测量及曲线，不生成伪运行结果。
            TabControl upper = FindControl<TabControl>(frame,"TCT.Imaging.PrimaryTabs");
            TabControl lower = FindControl<TabControl>(frame,"TCT.Imaging.SecondaryTabs");
            frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                MVIImageOverlayKind.DistanceMeasurement,MVIImageOverlayLayer.Measurement,
                new FGERect2D(210,190,350,170),new[]{
                new FGEPoint2D(210,190),new FGEPoint2D(560,360)}));
            frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                MVIImageOverlayKind.AreaMeasurement,MVIImageOverlayLayer.Measurement,
                new FGERect2D(380,230,120,95)));
            frame.ProfilePlot.SetLineProfile(FIMImageAnalyzer.GetLineProfile(sample,0,0,width-1,height-1));
            upper.SelectedIndex = 3;
            lower.SelectedIndex = 2;
            CaptureWindow(window,Path.Combine(Path.GetDirectoryName(path),"workbench-measure-profile.png"));
            upper.SelectedIndex = 2;
            lower.SelectedIndex = 1;
            frame.HistogramPlot.SetHistogram(FIMImageAnalyzer.GetHistogram(sample,256));
            CaptureWindow(window,Path.Combine(Path.GetDirectoryName(path),"workbench-gray-histogram.png"));
            frame.Viewer.ClearOverlays();
            upper.SelectedIndex = 0; lower.SelectedIndex = 0;
        }

        public static void Run()
        {
            TCTManualImageViewerFrame frame = new TCTManualImageViewerFrame();
            Window window = new Window { Width = 900, Height = 650,
                WindowStyle = WindowStyle.ToolWindow, ShowInTaskbar = false, Content = frame };
            string file = Path.Combine(Path.GetTempPath(),
                "CxVisual-Imaging-Wpf-"+Guid.NewGuid().ToString("N")+".png");
            string screenshot = Path.Combine(Path.GetTempPath(),
                "CxVisual-Imaging-Overlay-"+Guid.NewGuid().ToString("N")+".png");
            try
            {
                window.Show();
                window.UpdateLayout();
                AssertVisualLayout(frame, window);
                CaptureWorkbenchIfRequested(frame, window);
                VerifyTestCenterShell();
                FIMImageBuffer image = new FIMImageBuffer(4, 4, FIMPixelFormat.Gray16,
                    new byte[] { 1,0, 2,0, 3,0, 4,0, 5,0, 6,0, 7,0, 8,0,
                                 9,0, 10,0, 11,0, 12,0, 13,0, 14,0, 15,0, 16,0 });
                frame.SetOriginal(image);
                // 统计页切入时自动计算当前原始灰度，不需手动点击按钮。
                TabControl autoStatsTabs=FindControl<TabControl>(frame,
                    "TCT.Imaging.SecondaryTabs");
                if(autoStatsTabs.SelectedIndex!=0)
                    throw new InvalidOperationException("初始统计页状态不正确");
                window.UpdateLayout();
                System.Diagnostics.Stopwatch autoHistogramWait=
                    System.Diagnostics.Stopwatch.StartNew();
                while(frame.HistogramPlot.PointCount!=256 &&
                    autoHistogramWait.ElapsedMilliseconds<10000)
                {
                    var loop=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                if(frame.HistogramPlot.PointCount!=256 ||
                    autoStatsTabs.SelectedIndex!=0 ||
                    frame.Viewer.Session.Original.GetGrayValue(1,2)!=10)
                    throw new InvalidOperationException(
                        "统计页未自动生成256档直方图或改写了源像素");
                autoStatsTabs.SelectedIndex=4;
                window.UpdateLayout();
                Button exportRawAction=FindControl<Button>(frame,"TCT.Imaging.ExportActiveRaw");
                if(exportRawAction==null || !exportRawAction.IsEnabled)
                    throw new InvalidOperationException("RAW无损导出真实工作台按钮缺失");
                string exported=Path.Combine(Path.GetTempPath(),
                    "CxVisual-FIM-WpfRaw-"+Guid.NewGuid().ToString("N")+".raw");
                try
                {
                    frame.ExportActiveRawFile(exported);
                    FIMImageBuffer restoredRaw=FIMRawImageReader.LoadFile(exported,4,4,
                        FIMPixelFormat.Gray16);
                    if(!restoredRaw.CopyPixels().SequenceEqual(image.CopyPixels()) ||
                        restoredRaw.GetGrayValue(3,3)!=16 ||
                        frame.Viewer.Session.Original.GetGrayValue(3,3)!=16)
                        throw new InvalidOperationException(
                            "TestCenter活动原图RAW导出与Gray16原像素回环不一致");
                    var resultRaw=new FIMImageBuffer(4,4,FIMPixelFormat.Gray16,
                        Enumerable.Repeat((byte)0,32).ToArray());
                    frame.Viewer.ShowResult(resultRaw);
                    frame.ExportActiveRawFile(exported,overwrite:true);
                    FIMImageBuffer outputRaw=FIMRawImageReader.LoadFile(exported,4,4,
                        FIMPixelFormat.Gray16);
                    if(outputRaw.GetGrayValue(3,3)!=0 ||
                       frame.Viewer.Session.Original.GetGrayValue(3,3)!=16 ||
                       frame.Viewer.Session.Result.GetGrayValue(3,3)!=0)
                        throw new InvalidOperationException(
                            "TestCenter结果槽RAW导出错误读取原图或改变原图");
                    frame.Viewer.SelectOriginal();
                }
                finally {if(File.Exists(exported))File.Delete(exported);}
                autoStatsTabs.SelectedIndex=0;
                // 大图不允许用Image.DesiredSize撑大WPF Grid而裁掉边界：
                // Fit必须严格匹配实际可见ViewportRoot，含正确的中心平移。
                FIMImageBuffer fitProbe=new FIMImageBuffer(4096,2048,
                    FIMPixelFormat.Gray8,new byte[4096*2048]);
                frame.SetOriginal(fitProbe);
                window.UpdateLayout();
                frame.Viewer.FitToWindow();
                FrameworkElement viewport=frame.Viewer.FindName("ViewportRoot") as FrameworkElement;
                MatrixTransform fitMatrix=frame.Viewer.FindName("ImageTransform") as MatrixTransform;
                if(viewport==null || fitMatrix==null ||
                    viewport.ActualWidth<=0 || viewport.ActualHeight<=0 ||
                    viewport.ActualWidth>frame.Viewer.ActualWidth+1 ||
                    viewport.ActualHeight>frame.Viewer.ActualHeight+1)
                    throw new InvalidOperationException(
                        "大图Image固有尺寸挤占/撑大了WPF实际视口布局");
                double expectedFit=Math.Min(viewport.ActualWidth/fitProbe.Width,
                    viewport.ActualHeight/fitProbe.Height);
                double fitScale=frame.Viewer.PrimaryViewportScale;
                if(Math.Abs(fitScale-expectedFit)>1e-7 ||
                    Math.Abs(fitMatrix.Matrix.M11-fitScale)>1e-7 ||
                    Math.Abs(fitMatrix.Matrix.OffsetX-
                        (viewport.ActualWidth-fitProbe.Width*fitScale)/2)>1e-5 ||
                    Math.Abs(fitMatrix.Matrix.OffsetY-
                        (viewport.ActualHeight-fitProbe.Height*fitScale)/2)>1e-5)
                    throw new InvalidOperationException(
                        "大图没有按真正可见视口居中完整Fit，或被WPF原生Image尺寸撑开");
                frame.SetOriginal(image);
                // 用户反馈：窗位、灰阶不能只存在底层API；必须通过可见WPF入口生效。
                TabControl displayTabs=FindControl<TabControl>(frame,"TCT.Imaging.PrimaryTabs");
                displayTabs.SelectedIndex=1;
                window.UpdateLayout();
                TextBox windowWidth=FindControl<TextBox>(frame,"TCT.Imaging.WindowWidth");
                TextBox windowCenter=FindControl<TextBox>(frame,"TCT.Imaging.WindowCenter");
                Slider widthSlider=FindControl<Slider>(frame,"TCT.Imaging.WindowWidthSlider");
                Slider centerSlider=FindControl<Slider>(frame,"TCT.Imaging.WindowCenterSlider");
                TextBlock windowRange=FindControl<TextBlock>(frame,"TCT.Imaging.WindowRange");
                TextBlock windowWidthMax=FindControl<TextBlock>(frame,"TCT.Imaging.WindowWidthMax");
                TextBlock windowCenterMax=FindControl<TextBlock>(frame,"TCT.Imaging.WindowCenterMax");
                CheckBox roiWindow=FindControl<CheckBox>(frame,"TCT.Imaging.WindowRoi");
                CheckBox windowEnabled=FindControl<CheckBox>(frame,"TCT.Imaging.WindowEnabled");
                Button windowApply=FindControl<Button>(frame,"TCT.Imaging.Window");
                Button autoWindow=FindControl<Button>(frame,"TCT.Imaging.AutoWindow");
                if(windowWidth==null||windowCenter==null||windowApply==null||autoWindow==null||
                   widthSlider==null||centerSlider==null||roiWindow==null||windowEnabled==null||
                   windowRange==null||!windowRange.Text.Contains("65535")||
                   windowWidthMax==null||windowWidthMax.Text!="65535" ||
                   windowCenterMax==null||windowCenterMax.Text!="65535")
                    throw new InvalidOperationException("窗宽/窗位缺少数字与真实WPF滑条");
                windowWidth.Text="15";
                windowCenter.Text="8.5";
                windowApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.DisplayState.IsAutoWindowLevel ||
                    frame.Viewer.DisplayState.WindowWidth!=15 ||
                    frame.Viewer.Session.Original.GetGrayValue(1,2)!=10)
                    throw new InvalidOperationException("WPF窗位操作未改变显示状态或损伤原始像素");
                frame.Viewer.SavePng(file);
                var manualWindowPreview=MPWImageViewerControl.ReadImageFile(file);
                double originalMappedValue=manualWindowPreview.GetGrayValue(1,2);
                if(originalMappedValue<=0 || originalMappedValue>=255)
                    throw new InvalidOperationException("手动窗宽窗位没有生成有效灰度动态范围");
                windowWidth.Text="b";
                windowApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.DisplayState.WindowWidth!=15 ||
                    !AutomationProperties.GetHelpText(windowApply).Contains("窗宽/窗位"))
                    throw new InvalidOperationException("错误窗位输入没有明确拒绝并保留旧显示");
                windowWidth.Text="12";
                windowCenter.Text="8";
                windowApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.DisplayState.WindowWidth!=12 ||
                   Math.Abs(widthSlider.Value-12)>0.001 ||
                   windowEnabled.IsChecked!=true)
                    throw new InvalidOperationException("窗宽数值没有同步真实滑条或手动显示");
                centerSlider.Value=6;
                centerSlider.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(
                    0,0,false) {
                    RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragCompletedEvent });
                if(frame.Viewer.DisplayState.WindowCenter!=6 ||
                   windowCenter.Text!="6")
                    throw new InvalidOperationException("窗位滑条没有更新数值或显示状态");
                roiWindow.IsChecked=true;
                frame.Viewer.SelectRegion(new FIMPixelRegion(0,0,1,1));
                autoWindow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.DisplayState.IsAutoWindowLevel ||
                   frame.Viewer.DisplayState.WindowWidth!=5 ||
                   frame.Viewer.DisplayState.WindowCenter!=3.5)
                    throw new InvalidOperationException("框选自动取窗未按ROI实际像素1至6计算");
                roiWindow.IsChecked=false;
                frame.Viewer.ClearSelection();
                autoWindow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(!frame.Viewer.DisplayState.IsAutoWindowLevel ||
                    !frame.Viewer.DisplayState.IsAutoContrast)
                    throw new InvalidOperationException("全图自动灰度拉伸没有真正生效");
                displayTabs.SelectedIndex=2;
                window.UpdateLayout();
                TextBox gammaField=FindControl<TextBox>(frame,"TCT.Imaging.Gamma");
                TextBox brightnessField=FindControl<TextBox>(frame,"TCT.Imaging.Brightness");
                TextBox contrastField=FindControl<TextBox>(frame,"TCT.Imaging.Contrast");
                CheckBox invertedBox=FindControl<CheckBox>(frame,"TCT.Imaging.Inverted");
                Button applyGray=FindControl<Button>(frame,"TCT.Imaging.ApplyDisplay");
                Button resetGray=FindControl<Button>(frame,"TCT.Imaging.ResetDisplay");
                if(gammaField==null||brightnessField==null||contrastField==null||
                   invertedBox==null||applyGray==null||resetGray==null)
                    throw new InvalidOperationException("灰阶增强真实WPF控件缺失");
                gammaField.Text="2"; brightnessField.Text="26"; contrastField.Text="1.5";
                applyGray.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.DisplayState.Gamma!=2 ||
                    Math.Abs(frame.Viewer.DisplayState.Brightness-26.0/255)>1e-10 ||
                    frame.Viewer.DisplayState.Contrast!=1.5)
                    throw new InvalidOperationException("WPF Gamma/亮度/对比度未实际提交");
                Slider sliderGamma=FindControl<Slider>(frame,"TCT.Imaging.Slider.Gamma");
                if(sliderGamma==null)
                    throw new InvalidOperationException("Gamma WPF滑条不可见");
                sliderGamma.Value=2.5;
                sliderGamma.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(
                    0,0,false) {
                    RoutedEvent=System.Windows.Controls.Primitives.Thumb.DragCompletedEvent });
                if(Math.Abs(frame.Viewer.DisplayState.Gamma-2.5)>1e-10 ||
                   gammaField.Text!="2.5")
                    throw new InvalidOperationException("Gamma滑条拖动松开未更新图像与输入框");
                // 后续非法输入保留最新合法的Gamma=2.5，而不是恢复过期状态。
                frame.Viewer.SavePng(file);
                var enhancedPreview=MPWImageViewerControl.ReadImageFile(file);
                if(enhancedPreview.GetGrayValue(1,2)==originalMappedValue)
                    throw new InvalidOperationException("WPF灰阶参数没有真正改变显示像素");
                invertedBox.IsChecked=true;
                if(!frame.Viewer.DisplayState.IsInverted)
                    throw new InvalidOperationException("反色勾选后未应用实际显示状态");
                gammaField.Text="invalid";
                applyGray.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.DisplayState.Gamma!=2.5 ||
                   !AutomationProperties.GetHelpText(applyGray).Contains("Gamma"))
                    throw new InvalidOperationException("非法灰阶输入未保留原状态或缺少错误提示");
                resetGray.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(!frame.Viewer.DisplayState.IsIdentity ||
                    frame.Viewer.Session.Original.GetGrayValue(1,2)!=10)
                    throw new InvalidOperationException("显示复位没有恢复状态或修改了原图");
                Slider gammaSlider=FindControl<Slider>(frame,"TCT.Imaging.Slider.Gamma");
                Slider brightnessSlider=FindControl<Slider>(frame,"TCT.Imaging.Slider.亮度");
                Slider contrastSlider=FindControl<Slider>(frame,"TCT.Imaging.Slider.对比度");
                if(gammaSlider==null || brightnessSlider==null || contrastSlider==null ||
                   gammaSlider.Value!=1 || brightnessSlider.Value!=0 || contrastSlider.Value!=1)
                    throw new InvalidOperationException("显示复位后滑块仍停留在旧Gamma/亮度/对比度");
                // 图像位深变化时默认手动窗范围须随之变化，不能让Gray8沿用65535。
                FIMImageBuffer gray8WindowProbe=new FIMImageBuffer(2,1,
                    FIMPixelFormat.Gray8,new byte[]{0,255});
                frame.SetOriginal(gray8WindowProbe);
                if(windowWidth.Text!="255" || windowCenter.Text!="127.5" ||
                   widthSlider.Maximum!=255 || centerSlider.Maximum!=255 ||
                   !windowRange.Text.Contains("0～255") ||
                   windowWidthMax.Text!="255" || windowCenterMax.Text!="255")
                    throw new InvalidOperationException("Gray8载入后仍沿用Gray16默认窗范围");
                windowApply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                frame.Viewer.SavePng(file);
                FIMImageBuffer gray8WindowPreview=MPWImageViewerControl.ReadImageFile(file);
                if(gray8WindowPreview.GetGrayValue(0,0)!=0 ||
                   gray8WindowPreview.GetGrayValue(1,0)!=255 ||
                   frame.Viewer.Session.Original.GetGrayValue(1,0)!=255)
                    throw new InvalidOperationException("Gray8自动窗范围和原始像素不一致");
                frame.SetOriginal(image);
                // 实际点击第六张二维工作台的专业CPU检测按钮，MRK产生Image/Mask
                // 并经共用ResultItem投影到现有MPW/MVI结果槽。
                TabControl cpuTabs=FindControl<TabControl>(frame,"TCT.Imaging.SecondaryTabs");
                cpuTabs.SelectedIndex=3;
                window.UpdateLayout();
                Button cpuRun=FindControl<Button>(frame,"TCT.Imaging.CpuVisionRun");
                Button cpuCancel=FindControl<Button>(frame,"TCT.Imaging.CpuVisionCancel");
                TextBox cpuThreshold=FindControl<TextBox>(frame,"TCT.Imaging.CpuThreshold");
                TextBox cpuArea=FindControl<TextBox>(frame,"TCT.Imaging.CpuMinArea");
                if(cpuRun==null||cpuCancel==null||cpuThreshold==null||cpuArea==null)
                    throw new InvalidOperationException("专业CPU检测按钮、参数或取消入口未加入正式二维工作区");
                cpuThreshold.Text="10";
                cpuArea.Text="2";
                cpuRun.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                System.Diagnostics.Stopwatch cpuWait=System.Diagnostics.Stopwatch.StartNew();
                while(frame.CpuEvidence==null && cpuWait.ElapsedMilliseconds<15000)
                {
                    var loop=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                var evidence=frame.CpuEvidence;
                if(evidence==null||evidence.RunId==Guid.Empty||
                    evidence.SourceId==null||
                    evidence.Items.Count(i=>i.Kind==CxVisual.Professional.Vision.CxVisionResultKind.Mask)!=1||
                    evidence.Items.Count(i=>i.Kind==CxVisual.Professional.Vision.CxVisionResultKind.Image)!=1||
                    evidence.Items.Count(i=>i.Kind==CxVisual.Professional.Vision.CxVisionResultKind.Region)!=1||
                    evidence.Items.Count(i=>i.Kind==CxVisual.Professional.Vision.CxVisionResultKind.Measurement)!=1||
                    evidence.Items.Single(i=>i.Kind==CxVisual.Professional.Vision.CxVisionResultKind.Measurement)
                        .IntegerValue!=7||
                    evidence.Items.Single(i=>i.Kind==CxVisual.Professional.Vision.CxVisionResultKind.Region)
                        .Region.PixelArea!=7||
                    frame.Viewer.Session.Result==null||
                    frame.Viewer.Session.Result.PixelFormat!=FIMPixelFormat.Gray8||
                    frame.Viewer.Session.Result.GetGrayValue(0,0)!=0||
                    frame.Viewer.Session.Result.GetGrayValue(1,2)!=255||
                    frame.Viewer.Session.Original.PixelFormat!=FIMPixelFormat.Gray16||
                    frame.Viewer.Session.Original.GetGrayValue(0,0)!=1)
                    throw new InvalidOperationException("点击专业CPU按钮未得到MRK真实Run、16位原图/阈值Mask和区域");
                // 真正点击输出页多类型结果项，不借另一个Viewer或自己生成假图。
                TabControl outputTabs=FindControl<TabControl>(frame,"TCT.Imaging.SecondaryTabs");
                outputTabs.SelectedIndex=4;
                window.UpdateLayout();
                ListBox cpuResults=FindControl<ListBox>(frame,"TCT.Imaging.CpuResultItems");
                if(cpuResults==null || cpuResults.Items.Count!=evidence.Items.Count)
                    throw new InvalidOperationException("CPU Run结果项未在WPF输出页显示");
                Button exportRunCsv=FindControl<Button>(frame,
                    "TCT.Imaging.ExportCpuRunCsv");
                if(exportRunCsv==null || !exportRunCsv.IsEnabled)
                    throw new InvalidOperationException(
                        "R1工作台当前Run的结构化CSV导出按钮缺失");
                string runCsv=Path.Combine(Path.GetTempPath(),
                    "CxVisual-RunCsv-"+Guid.NewGuid().ToString("N")+".csv");
                try
                {
                    frame.ExportCpuRunCsvFile(runCsv);
                    string csv=File.ReadAllText(runCsv);
                    if(!csv.StartsWith("schema_version,run_id,source_id,") ||
                       !csv.Contains(evidence.RunId.ToString("N")) ||
                       !csv.Contains(evidence.SourceId) ||
                       !csv.Contains("\"Measurement\"") ||
                       !csv.Contains("\"Region\"") ||
                       !csv.Contains("\"7\"") || !csv.Contains("\"px²\""))
                        throw new InvalidOperationException(
                            "WPF当前Run导出的结构化CSV身份或区域/测量数值不正确");
                }
                finally {if(File.Exists(runCsv))File.Delete(runCsv);}

                var imageRow=cpuResults.Items.OfType<ListBoxItem>().Single(row =>
                    ((CxVisual.Professional.Vision.CxVisionResultItem)row.Tag).Kind ==
                    CxVisual.Professional.Vision.CxVisionResultKind.Image);
                var maskRow=cpuResults.Items.OfType<ListBoxItem>().Single(row =>
                    ((CxVisual.Professional.Vision.CxVisionResultItem)row.Tag).Kind ==
                    CxVisual.Professional.Vision.CxVisionResultKind.Mask);
                var regionRow=cpuResults.Items.OfType<ListBoxItem>().Single(row =>
                    ((CxVisual.Professional.Vision.CxVisionResultItem)row.Tag).Kind ==
                    CxVisual.Professional.Vision.CxVisionResultKind.Region);
                var measurementRow=cpuResults.Items.OfType<ListBoxItem>().Single(row =>
                    ((CxVisual.Professional.Vision.CxVisionResultItem)row.Tag).Kind ==
                    CxVisual.Professional.Vision.CxVisionResultKind.Measurement);
                cpuResults.SelectedItem=imageRow;
                if(frame.Viewer.Session.Result==null ||
                    frame.Viewer.Session.Result.PixelFormat!=FIMPixelFormat.Gray16 ||
                    frame.Viewer.Session.Result.GetGrayValue(1,2)!=10 ||
                    frame.Viewer.Session.Original.GetGrayValue(1,2)!=10)
                    throw new InvalidOperationException("点击Run处理图项未投影真实Gray16图像");
                cpuResults.SelectedItem=maskRow;
                FIMImageBuffer selectedMask=frame.Viewer.Session.Result;
                if(selectedMask==null || selectedMask.PixelFormat!=FIMPixelFormat.Gray8 ||
                    selectedMask.GetGrayValue(1,2)!=255)
                    throw new InvalidOperationException("点击Run Mask项未切回真实二值掩码");
                cpuResults.SelectedItem=regionRow;
                if(!ReferenceEquals(frame.Viewer.Session.Result,selectedMask) ||
                    frame.CpuEvidence==null)
                    throw new InvalidOperationException("选中区域证据时不应改写图像结果或丢失Run");
                cpuResults.SelectedItem=measurementRow;
                var measurement=(CxVisual.Professional.Vision.CxVisionResultItem)measurementRow.Tag;
                if(measurement.IntegerValue!=7 || measurement.Unit!="px²" ||
                    measurement.OutputPort!="AcceptedRegionPixelArea" ||
                    measurement.RunId!=evidence.RunId ||
                    !ReferenceEquals(frame.Viewer.Session.Result,selectedMask))
                    throw new InvalidOperationException(
                        "点击真实MRK面积测量项不能丢失Run身份或替换Mask显示");
                // 复用已存在的FIM.MaskContours：实际点击按钮，从本次MRK
                // ThresholdMask抽取原图格点轮廓，并验证带符号面积等于像素面积。
                outputTabs.SelectedIndex=3;
                window.UpdateLayout();
                Button cpuContour=FindControl<Button>(frame,"TCT.Imaging.ExtractMaskContours");
                if(cpuContour==null)
                    throw new InvalidOperationException("专业Mask无法访问现有真实轮廓分析操作");
                int contoursBefore=frame.Viewer.Overlays.Items.Count;
                cpuContour.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var contourWait=System.Diagnostics.Stopwatch.StartNew();
                while(frame.Viewer.Overlays.Items.Count==contoursBefore &&
                    contourWait.ElapsedMilliseconds<15000)
                {
                    var loop=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                var contours=frame.Viewer.Overlays.Items
                    .Where(i=>i.Kind==MVIImageOverlayKind.PolygonAnnotation).ToArray();
                if(contours.Length!=1 ||
                    Math.Abs(FIMImageMaskContours.SignedArea(contours[0].Points)-7)>1e-9 ||
                    frame.CpuEvidence==null ||
                    !ReferenceEquals(frame.Viewer.Session.Result,selectedMask) ||
                    frame.Viewer.Session.Original.PixelFormat!=FIMPixelFormat.Gray16)
                    throw new InvalidOperationException(
                        "真实CPU Mask→FIM轮廓→像素面积7链路错误或破坏原图");
                frame.Viewer.ClearOverlays();
                // 真实点击“从本次Run生成轮廓结果项”：必须继承同一个Run，
                // 只新增只读Contour，且点击条目时仅在唯一MVI Store产生临时标注。
                outputTabs.SelectedIndex=4;
                window.UpdateLayout();
                Button generateRunContours=FindControl<Button>(frame,
                    "TCT.Imaging.CpuGenerateContours");
                if(generateRunContours==null)
                    throw new InvalidOperationException("CPU Run轮廓结果生成入口不存在");
                generateRunContours.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var runContourWait=System.Diagnostics.Stopwatch.StartNew();
                while((frame.CpuEvidence==null || !frame.CpuEvidence.Items.Any(x=>
                    x.Kind==CxVisual.Professional.Vision.CxVisionResultKind.Contour)) &&
                    runContourWait.ElapsedMilliseconds<20000)
                {
                    var loop=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                var withRunContour=frame.CpuEvidence;
                if(withRunContour==null || withRunContour.RunId!=evidence.RunId ||
                    withRunContour.SourceId!=evidence.SourceId ||
                    withRunContour.Items.Count!=evidence.Items.Count+1 ||
                    cpuResults.Items.Count!=withRunContour.Items.Count)
                    throw new InvalidOperationException("Run轮廓未按需生成或失去身份与列表同步");
                var runContour=withRunContour.Items.Single(x=>
                    x.Kind==CxVisual.Professional.Vision.CxVisionResultKind.Contour);
                if(runContour.Contour==null || runContour.Contour.IsHole ||
                    Math.Abs(runContour.Contour.SignedAreaPixels-7)>1e-9 ||
                    runContour.RunId!=evidence.RunId || runContour.SourceId!=evidence.SourceId)
                    throw new InvalidOperationException("正式Contour面积7/原Run来源不正确");
                var projectedRow=cpuResults.Items.OfType<ListBoxItem>().Single(row=>
                    ((CxVisual.Professional.Vision.CxVisionResultItem)row.Tag).ItemId==
                        runContour.ItemId);
                cpuResults.SelectedItem=projectedRow;
                Guid? projectedOverlayId=frame.Viewer.SelectedOverlayId;
                var projected=projectedOverlayId.HasValue ?
                    frame.Viewer.Overlays.Find(projectedOverlayId.Value):null;
                if(projected==null || !projected.IsLocked ||
                    projected.ExternalId!=runContour.ItemId ||
                    projected.Kind!=MVIImageOverlayKind.PolygonAnnotation ||
                    frame.Viewer.Session.ActiveSlot!=MVIImageSlot.Original ||
                    frame.Viewer.Session.Original.PixelFormat!=FIMPixelFormat.Gray16)
                    throw new InvalidOperationException(
                        "点选Contour未产生带ItemId的只读MVI标注或污染Gray16原图");
                // 再点同一条目不可生成两个相同轮廓；主动保留用户手工标注。
                cpuResults.SelectedItem=null;
                cpuResults.SelectedItem=projectedRow;
                if(frame.Viewer.Overlays.Items.Count(x=>x.ExternalId==runContour.ItemId)!=1)
                    throw new InvalidOperationException("重复选择同一Contour生成了重复标注");
                var manual=new MVIImageOverlayItem(MVIImageOverlayKind.PolygonAnnotation,
                    MVIImageOverlayLayer.Annotation,new FGERect2D(0,0,1,1),
                    new[]{new FGEPoint2D(0,0),new FGEPoint2D(1,0),
                        new FGEPoint2D(0,1)},"用户标注");
                frame.Viewer.AddOverlay(manual);
                // 修改参数必须清空属于旧Run的显示槽和证据；无效参数不能重用旧结果。
                cpuThreshold.Text="70000";
                bool staleRunCsvDenied=false;
                string invalidCsvPath=Path.Combine(Path.GetTempPath(),
                    "CxVisual-StaleRun-"+Guid.NewGuid().ToString("N")+".csv");
                try
                {
                    frame.ExportCpuRunCsvFile(invalidCsvPath);
                }
                catch(InvalidOperationException){staleRunCsvDenied=true;}
                finally
                {
                    if(File.Exists(invalidCsvPath))File.Delete(invalidCsvPath);
                }
                if(!staleRunCsvDenied)
                    throw new InvalidOperationException(
                        "修改专业参数后旧Run仍能被导出为有效CSV");
                if(frame.Viewer.Overlays.Find(projectedOverlayId.Value)!=null ||
                    frame.Viewer.Overlays.Find(manual.Id)==null)
                    throw new InvalidOperationException(
                        "检测参数失效未删除旧Run临时轮廓或误删用户标注");
                if(frame.CpuEvidence!=null || cpuResults.Items.Count!=0 ||
                    frame.Viewer.Session.Result!=null ||
                    frame.Viewer.Session.ActiveSlot!=MVIImageSlot.Original ||
                    !ReferenceEquals(frame.Viewer.Session.Original,image))
                    throw new InvalidOperationException("CPU参数修改未取消旧Run或清理仅属于CPU的结果槽");
                cpuRun.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.CpuEvidence!=null || frame.Viewer.Session.Result!=null)
                    throw new InvalidOperationException("超出Gray16范围的参数竟然重新发布旧CPU结果");
                // 同一原图的新参数必须产生新的MRK RunId和6像素Mask，不能复用7像素结果。
                cpuThreshold.Text="11";
                cpuRun.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var rerunWait=System.Diagnostics.Stopwatch.StartNew();
                while(frame.CpuEvidence==null && rerunWait.ElapsedMilliseconds<15000)
                {
                    var loop=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                var secondRun=frame.CpuEvidence;
                if(secondRun==null || secondRun.RunId==evidence.RunId ||
                    secondRun.Items.Single(i=>i.Kind==
                        CxVisual.Professional.Vision.CxVisionResultKind.Region).Region.PixelArea!=6 ||
                    frame.Viewer.Session.Result==null ||
                    frame.Viewer.Session.Result.GetGrayValue(1,2)!=0 ||
                    frame.Viewer.Session.Original.GetGrayValue(1,2)!=10)
                    throw new InvalidOperationException("CPU参数重新运行未产生新Run/6像素掩码或改写了原图");
                // 任务尚在进行时修改参数，旧Run即使晚到也不得回写。
                cpuRun.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                cpuThreshold.Text="12";
                var invalidatedWait=System.Diagnostics.Stopwatch.StartNew();
                while(invalidatedWait.ElapsedMilliseconds<200)
                {
                    var loop=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                if(frame.CpuEvidence!=null || frame.Viewer.Session.Result!=null ||
                    cpuResults.Items.Count!=0)
                    throw new InvalidOperationException("CPU参数变动后晚到的Run污染当前图像或结果项");
                // 旧运行证据不得留在下一张图的工作台状态。
                frame.SetOriginal(image);
                if(frame.CpuEvidence!=null || frame.Viewer.Session.Result!=null ||
                    cpuResults.Items.Count!=0)
                    throw new InvalidOperationException("重新打开原图后旧Vision Run、Mask或浏览项没有清除");
                cpuCancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                // 拆分式按钮必须经过FIM内存图→MEM/MOC工程→MRK提交，且原始Gray16不变。
                // 前面的RAW/结果输出验收切到了“输出”页；先按用户路径回到变换页，
                // 再查找真实可见按钮，不能把未激活Tab的视觉树误报为功能缺失。
                cpuTabs.SelectedItem=cpuTabs.Items.OfType<TabItem>().Single(t=>(string)t.Header=="变换");
                window.UpdateLayout();
                Button splitCpu=FindControl<Button>(frame,"TCT.Imaging.CpuVisionSplitRun");
                ComboBox splitRadius=FindControl<ComboBox>(frame,"TCT.Imaging.CpuMedianRadius");
                if(splitCpu==null || splitRadius==null || !splitCpu.IsVisible || !splitRadius.IsVisible)
                    throw new InvalidOperationException("独立Mask/Region的真实WPF操作入口缺失：button="+(splitCpu!=null)+
                        ", radius="+(splitRadius!=null)+", buttonVisible="+(splitCpu!=null&&splitCpu.IsVisible)+
                        ", radiusVisible="+(splitRadius!=null&&splitRadius.IsVisible)+", tab="+((TabItem)cpuTabs.SelectedItem).Header);
                cpuThreshold.Text="10";
                cpuArea.Text="2";
                splitRadius.SelectedItem=0;
                splitCpu.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var splitWait=System.Diagnostics.Stopwatch.StartNew();
                while(frame.CpuEvidence==null && splitWait.ElapsedMilliseconds<20000)
                {
                    var loop=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                var splitEvidence=frame.CpuEvidence;
                if(splitEvidence==null || splitEvidence.RunId==Guid.Empty ||
                    splitEvidence.Items.Count(x=>x.Kind==
                        CxVisual.Professional.Vision.CxVisionResultKind.Mask)!=1 ||
                    splitEvidence.Items.Single(x=>x.Kind==
                        CxVisual.Professional.Vision.CxVisionResultKind.Measurement).IntegerValue!=7 ||
                    frame.Viewer.Session.Result==null ||
                    frame.Viewer.Session.Result.GetGrayValue(1,2)!=255 ||
                    frame.Viewer.Session.Original.PixelFormat!=FIMPixelFormat.Gray16 ||
                    frame.Viewer.Session.Original.GetGrayValue(0,0)!=1)
                    throw new InvalidOperationException(
                        "WPF拆分式MEM/MOC/MRK执行未生成真实7px²检测与原图Mask");
                // 变更增强半径须使旧Run立即失效，下一Run才可发布增强图。
                splitRadius.SelectedItem=1;
                if(frame.CpuEvidence!=null || frame.Viewer.Session.Result!=null)
                    throw new InvalidOperationException("修改中值半径后未撤销旧拆分式Run");
                splitCpu.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var enhancedWait=System.Diagnostics.Stopwatch.StartNew();
                while(frame.CpuEvidence==null && enhancedWait.ElapsedMilliseconds<20000)
                {
                    var loop=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                var enhancedRun=frame.CpuEvidence;
                if(enhancedRun==null || enhancedRun.RunId==splitEvidence.RunId ||
                    !enhancedRun.Items.Any(x=>x.Kind==
                        CxVisual.Professional.Vision.CxVisionResultKind.Image) ||
                    !enhancedRun.Items.Any(x=>x.Kind==
                        CxVisual.Professional.Vision.CxVisionResultKind.Mask) ||
                    frame.Viewer.Session.Original.GetGrayValue(0,0)!=1)
                    throw new InvalidOperationException(
                        "WPF五节点增强→独立Mask→Region→面积未真实执行或污染原图");
                cpuCancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                frame.SetOriginal(image);
                splitRadius.SelectedItem=0;
                // 真实变换页：矩形ROI中心种子 -> 后台生长 -> Gray8结果掩码和原像素反证。
                frame.Viewer.SelectRegion(new FIMPixelRegion(0,1,3,1));
                TabControl growthTabs=FindControl<TabControl>(frame,"TCT.Imaging.SecondaryTabs");
                growthTabs.SelectedIndex=3;
                window.UpdateLayout();
                Button growthButton=FindControl<Button>(frame,"TCT.Imaging.RegionGrow");
                TextBox growTolerance=FindControl<TextBox>(frame,"TCT.Imaging.GrowTolerance");
                CheckBox growEight=FindControl<CheckBox>(frame,"TCT.Imaging.GrowEightConnected");
                if(growthButton==null || growTolerance==null || growEight==null)
                    throw new InvalidOperationException("旧区域生长有菜单无可操作参数或按钮");
                growTolerance.Text="1";
                growEight.IsChecked=false;
                growthButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                System.Diagnostics.Stopwatch growWait=System.Diagnostics.Stopwatch.StartNew();
                while((frame.Viewer.Session.Result==null ||
                    frame.Viewer.Session.Result.PixelFormat!=FIMPixelFormat.Gray8 ||
                    frame.Viewer.Session.Result.GetGrayValue(0,1)!=255) &&
                    growWait.ElapsedMilliseconds<10000)
                {
                    System.Windows.Threading.DispatcherFrame loop=
                        new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                FIMImageBuffer growthMask=frame.Viewer.Session.Result;
                if(growthMask==null || growthMask.PixelFormat!=FIMPixelFormat.Gray8 ||
                    growthMask.GetGrayValue(0,1)!=255 ||
                    growthMask.GetGrayValue(1,1)!=255 ||
                    growthMask.GetGrayValue(2,1)!=255 ||
                    growthMask.GetGrayValue(3,1)!=0 ||
                    growthMask.GetGrayValue(1,0)!=0 ||
                    frame.Viewer.Session.Original.GetGrayValue(1,1)!=6)
                    throw new InvalidOperationException(
                        "点击区域生长未得到限域三像素Gray8掩码或修改原图");
                // 真实WPF操作：选择原图ROI、设置独立阈值、点击按钮，
                // 验证像素掩码而非通过底层API制造一张测试图。
                frame.Viewer.SelectRegion(new FIMPixelRegion(0,1,3,2));
                Button regionThreshold=FindControl<Button>(frame,
                    "TCT.Imaging.ThresholdSelectedRoi");
                TextBox thresholdLow=FindControl<TextBox>(frame,
                    "TCT.Imaging.ThresholdLow");
                TextBox thresholdHigh=FindControl<TextBox>(frame,
                    "TCT.Imaging.ThresholdHigh");
                if(regionThreshold==null || thresholdLow==null || thresholdHigh==null)
                    throw new InvalidOperationException("旧阈值ROI分割缺少WPF操作入口及参数");
                thresholdLow.Text="6";thresholdHigh.Text="10";
                regionThreshold.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                System.Diagnostics.Stopwatch thresholdWait=
                    System.Diagnostics.Stopwatch.StartNew();
                while(ReferenceEquals(frame.Viewer.Session.Result,growthMask) &&
                    thresholdWait.ElapsedMilliseconds<10000)
                {
                    System.Windows.Threading.DispatcherFrame loop=
                        new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                FIMImageBuffer boundedMask=frame.Viewer.Session.Result;
                if(boundedMask==null || ReferenceEquals(boundedMask,growthMask) ||
                    boundedMask.PixelFormat!=FIMPixelFormat.Gray8 ||
                    boundedMask.GetGrayValue(1,1)!=255 ||
                    boundedMask.GetGrayValue(0,1)!=0 ||
                    boundedMask.GetGrayValue(1,2)!=255 ||
                    boundedMask.GetGrayValue(2,2)!=0 ||
                    frame.Viewer.Session.Original.GetGrayValue(1,1)!=6)
                    throw new InvalidOperationException(
                        "真实阈值ROI按钮未生成限域原灰度Gray8掩码");
                // 三角ROI包围框内有10个以上候选像素，真实掩膜只应包含6个中心点。
                MVIImageOverlayItem thresholdPolygon=frame.Viewer.AddPolygonRoi(new[]{
                    new FGEPoint2D(0,0),new FGEPoint2D(3.6,0),
                    new FGEPoint2D(0,3.6)});
                frame.Viewer.SelectOverlay(thresholdPolygon.Id);
                thresholdLow.Text="1";thresholdHigh.Text="16";
                regionThreshold.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                thresholdWait.Restart();
                while(ReferenceEquals(frame.Viewer.Session.Result,boundedMask) &&
                    thresholdWait.ElapsedMilliseconds<10000)
                {
                    System.Windows.Threading.DispatcherFrame loop=
                        new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                FIMImageBuffer polygonMask=frame.Viewer.Session.Result;
                int included=0;
                if(polygonMask!=null && polygonMask.PixelFormat==FIMPixelFormat.Gray8)
                    for(int yy=0;yy<4;yy++)
                        for(int xx=0;xx<4;xx++)
                            if(polygonMask.GetGrayValue(xx,yy)==255)included++;
                if(polygonMask==null || ReferenceEquals(polygonMask,boundedMask) ||
                    included!=6 || polygonMask.GetGrayValue(3,0)!=0 ||
                    polygonMask.GetGrayValue(0,2)!=255 ||
                    polygonMask.GetGrayValue(2,2)!=0)
                    throw new InvalidOperationException(
                        "三角形阈值掩码错误地使用外接矩形或整个图像");
                // R1 CASE-01/02：在同一真实WPF工作台上选中三角ROI，
                // FIM阈值精确选6像素，再交给MRK连通域与面积；不使用外接框代替。
                cpuTabs.SelectedIndex=3;
                window.UpdateLayout();
                Button roiCpu=FindControl<Button>(frame,"TCT.Imaging.CpuVisionRoiRun");
                if(roiCpu==null || !roiCpu.IsEnabled)
                    throw new InvalidOperationException("专业ROI→MRK工作台按钮缺失");
                cpuThreshold.Text="1";cpuArea.Text="1";
                roiCpu.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var roiWait=System.Diagnostics.Stopwatch.StartNew();
                while(frame.CpuEvidence==null && roiWait.ElapsedMilliseconds<20000)
                {
                    var loop=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                var roiRun=frame.CpuEvidence;
                FIMImageBuffer roiMask=roiRun==null?null:
                    roiRun.Items.Single(x=>x.Kind==
                        CxVisual.Professional.Vision.CxVisionResultKind.Mask).Image;
                if(roiRun==null || roiMask==null ||
                   roiMask.PixelFormat!=FIMPixelFormat.Gray8 ||
                   roiMask.GetGrayValue(0,2)!=255 ||
                   roiMask.GetGrayValue(3,0)!=0 ||
                   roiMask.GetGrayValue(2,2)!=0 ||
                   roiRun.Items.Single(x=>x.Name=="阈值命中像素数").IntegerValue!=6 ||
                   roiRun.Items.Single(x=>x.Kind==
                       CxVisual.Professional.Vision.CxVisionResultKind.Measurement)
                       .IntegerValue!=6 ||
                   frame.Viewer.Session.Original.GetGrayValue(3,3)!=16)
                    throw new InvalidOperationException(
                        "R1三角形ROI→MRK专业Run误用外接框、面积不为6或修改原图");
                frame.Viewer.SelectOverlay(null);
                if(frame.CpuEvidence!=null ||
                   frame.Viewer.Session.Result!=null)
                    throw new InvalidOperationException(
                        "R1 ROI选择改变后旧MRK Run/Mask未自动撤销");
                frame.Viewer.SelectOverlay(thresholdPolygon.Id);
                frame.Viewer.ShowResult(polygonMask);
                // 真实轮廓按钮必须产出可见MVI PolygonAnnotation，而非仅得到模型/外接框。
                Button contourButton=FindControl<Button>(frame,
                    "TCT.Imaging.ExtractMaskContours");
                if(contourButton==null)
                    throw new InvalidOperationException("分割掩码缺少可操作轮廓提取按钮");
                int beforeContours=frame.Viewer.Overlays.Items.Count;
                contourButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                thresholdWait.Restart();
                while(frame.Viewer.Overlays.Items.Count==beforeContours &&
                    thresholdWait.ElapsedMilliseconds<10000)
                {
                    System.Windows.Threading.DispatcherFrame loop=
                        new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                MVIImageOverlayItem contour=frame.Viewer.Overlays.Items
                    .LastOrDefault(x=>x.Kind==MVIImageOverlayKind.PolygonAnnotation &&
                        x.Text.StartsWith("分割外轮廓",StringComparison.Ordinal));
                Canvas contourCanvas=frame.Viewer.FindName("OverlayCanvas") as Canvas;
                if(contour==null || contour.Points.Count<3 ||
                    Math.Abs(FIMImageMaskContours.SignedArea(contour.Points)-6)>1e-8 ||
                    contourCanvas==null ||
                    !contourCanvas.Children.OfType<System.Windows.Shapes.Polygon>().Any() ||
                    !ReferenceEquals(frame.Viewer.Session.Result,polygonMask))
                    throw new InvalidOperationException(
                        "轮廓按钮没有真实产生面积6的原图多边形及WPF渲染");
                // AOP真实验收：同一次算法请求的开始与完成必须保留关联ID、版本和耗时。
                if(!FLDLogHelper.IsInitialized)
                    throw new InvalidOperationException("TestCenter宿主未初始化正式FLD日志");
                FLDLogHelper.Flush(5000);
                var audit=FLDLogHelper.Snapshot()
                    .Where(x=>x.ModuleId=="TCT.Imaging.MaskContours").ToArray();
                var success=audit.LastOrDefault(x=>x.Message.StartsWith(
                    "轮廓完成；",StringComparison.Ordinal));
                if(success==null || string.IsNullOrWhiteSpace(success.CorrelationId) ||
                    !success.Message.Contains("耗时毫秒=") ||
                    !success.Message.Contains("源版本=") ||
                    !audit.Any(x=>x.CorrelationId==success.CorrelationId &&
                        x.Message.StartsWith("开始二值轮廓提取；",StringComparison.Ordinal)))
                    throw new InvalidOperationException(
                        "统一FLD日志未保留本次轮廓操作的关联ID/版本/耗时与开始结束链");
                string contourCsv=Path.Combine(Path.GetTempPath(),
                    "CxVisual-MaskContour-"+Guid.NewGuid().ToString("N")+".csv");
                try
                {
                    frame.ExportMeasurementsCsv(contourCsv);
                    string csv=File.ReadAllText(contourCsv);
                    if(!csv.Contains(contour.Id.ToString("D")) ||
                        !csv.Contains("PolygonAnnotation") ||
                        !csv.Contains("分割外轮廓") || !csv.Contains("Vertices"))
                        throw new InvalidOperationException("分割轮廓未包含在正式CSV导出中");
                }
                finally {if(File.Exists(contourCsv))File.Delete(contourCsv);}
                // 双轮廓批量WPF入口：两个正式PolygonAnnotation只引发一次Store事件。
                MVIImageOverlayItem contourA=new MVIImageOverlayItem(
                    MVIImageOverlayKind.PolygonAnnotation,MVIImageOverlayLayer.Annotation,
                    new FGERect2D(0,0,2,2),new[]{
                        new FGEPoint2D(0,0),new FGEPoint2D(2,0),new FGEPoint2D(0,2)});
                MVIImageOverlayItem contourB=new MVIImageOverlayItem(
                    MVIImageOverlayKind.PolygonAnnotation,MVIImageOverlayLayer.Annotation,
                    new FGERect2D(1,1,2,2),new[]{
                        new FGEPoint2D(1,1),new FGEPoint2D(3,1),new FGEPoint2D(1,3)});
                int batchChanges=0;
                EventHandler onBatchChanged=(s,e)=>batchChanges++;
                frame.Viewer.Overlays.Changed+=onBatchChanged;
                try
                {
                    int priorCount=frame.Viewer.Overlays.Items.Count;
                    if(frame.Viewer.AddOverlays(new[]{contourA,contourB})!=2 ||
                        batchChanges!=1 ||
                        frame.Viewer.Overlays.Items.Count!=priorCount+2 ||
                        frame.Viewer.Overlays.Find(contourA.Id)==null ||
                        frame.Viewer.Overlays.Find(contourB.Id)==null ||
                        contourCanvas.Children.OfType<System.Windows.Shapes.Polygon>().Count()<2)
                        throw new InvalidOperationException(
                            "双轮廓批量WPF添加未一次发布并完整渲染两个多边形");
                    bool batchDuplicateRejected=false;
                    try {frame.Viewer.AddOverlays(new[]{contourA});}
                    catch(InvalidOperationException) {batchDuplicateRejected=true;}
                    if(!batchDuplicateRejected || batchChanges!=1 ||
                        frame.Viewer.Overlays.Items.Count!=priorCount+2)
                        throw new InvalidOperationException(
                            "双轮廓重复ID没有原子拒绝或污染WPF观察通知");
                }
                finally
                {
                    frame.Viewer.Overlays.Changed-=onBatchChanged;
                    frame.Viewer.Overlays.Remove(contourA.Id);
                    frame.Viewer.Overlays.Remove(contourB.Id);
                }
                Console.WriteLine("PASS TCT Imaging: 双轮廓批量WPF单次Observer通知与拒绝反证");
                thresholdLow.Text="20";thresholdHigh.Text="10";
                regionThreshold.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(!ReferenceEquals(frame.Viewer.Session.Result,polygonMask))
                    throw new InvalidOperationException("非法阈值改变了原有分割结果");
                thresholdLow.Text="0";thresholdHigh.Text="65535";
                frame.Viewer.SelectOverlay(null);
                frame.Viewer.Overlays.Remove(contour.Id);
                frame.Viewer.Overlays.Remove(thresholdPolygon.Id);
                frame.Viewer.SelectRegion(new FIMPixelRegion(0,1,3,1));
                growTolerance.Text="-2";
                growthButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(!ReferenceEquals(polygonMask,frame.Viewer.Session.Result))
                    throw new InvalidOperationException("非法生长参数仍修改当前结果图");
                frame.Viewer.SelectOriginal();
                frame.Viewer.ClearSelection();
                growthTabs.SelectedIndex=0;
                window.UpdateLayout();
                frame.Viewer.FitToWindow(); // 四像素样本确保线命中容差转为亚像素，验证不存在的桥线
                TabControl lineTabs=FindControl<TabControl>(frame,"TCT.Imaging.PrimaryTabs");
                lineTabs.SelectedIndex=3;
                window.UpdateLayout();
                Button linePairTool=FindControl<Button>(frame,"TCT.Imaging.MeasureLinePair");
                if(linePairTool==null)
                    throw new InvalidOperationException("旧Ls2Ls四点测量缺少真正可操作按钮");
                linePairTool.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.ToolMode!=MVIImageToolMode.LinePair)
                    throw new InvalidOperationException("Ls2Ls工具按钮未触发MPW互斥模式");
                MVIImageOverlayItem linePair=frame.Viewer.AddLinePairMeasurement(new[]{
                    new FGEPoint2D(.5,.5),new FGEPoint2D(3.5,.5),
                    new FGEPoint2D(1.5,2.5),new FGEPoint2D(3.5,2.5)});
                frame.Viewer.SelectOverlay(linePair.Id);
                string lineResult=frame.Viewer.DescribeMeasurement(linePair);
                if(!lineResult.Contains("最短 2 px") ||
                    !lineResult.Contains("夹角 0°") ||
                    frame.Viewer.Overlays.Find(linePair.Id)!=linePair ||
                    frame.Viewer.OverlayVisualCount<4 ||
                    frame.Viewer.HitTestOverlay(2.5,1.5)!=null)
                    throw new InvalidOperationException("Ls2Ls距离夹角/原图对象或独立两线显示错误："+lineResult);
                string lineCsv=Path.Combine(Path.GetTempPath(),
                    "CxVisual-Ls2Ls-"+Guid.NewGuid().ToString("N")+".csv");
                try
                {
                    frame.ExportMeasurementsCsv(lineCsv);
                    string csv=File.ReadAllText(lineCsv);
                    if(!csv.Contains(linePair.Id.ToString("D")) ||
                        !csv.Contains("LinePairMeasurement") ||
                        !csv.Contains("最短 2 px") ||
                        !csv.Contains("LinePairDistancePixels") ||
                        !csv.Contains("ClosestOnFirstX") ||
                        !csv.Contains("ClosestOnSecondY") ||
                        !csv.Contains("0.5:0.5;3.5:0.5;1.5:2.5;3.5:2.5"))
                        throw new InvalidOperationException("Ls2Ls四点结果未按稳定ID正确导出CSV");
                }
                finally { if(File.Exists(lineCsv))File.Delete(lineCsv); }
                frame.Viewer.Overlays.Remove(linePair.Id);
                frame.Viewer.SelectOverlay(null);
                // 验证与正式WPF鼠标共用的四点阶段状态机，不靠直接构造对象伪装交互。
                MVIImageOverlayItem pending;
                pending=frame.Viewer.AppendLinePairPoint(new FGEPoint2D(.5,.5));
                if(pending!=null)throw new InvalidOperationException("首个点错误提前完成Ls2Ls");
                pending=frame.Viewer.AppendLinePairPoint(new FGEPoint2D(3.5,.5));
                if(pending!=null)throw new InvalidOperationException("首条线未等待第二条线");
                pending=frame.Viewer.AppendLinePairPoint(new FGEPoint2D(.5,3.5));
                System.Windows.Shapes.Polyline draft2=
                    frame.Viewer.FindName("DraftLinePair") as System.Windows.Shapes.Polyline;
                if(pending!=null || draft2==null ||
                    draft2.Visibility!=Visibility.Visible)
                    throw new InvalidOperationException("Ls2Ls第三点没有显示独立第二线预览");
                pending=frame.Viewer.AppendLinePairPoint(new FGEPoint2D(3.5,3.5));
                if(pending==null || pending.Points.Count!=4 ||
                    pending.Kind!=MVIImageOverlayKind.LinePairMeasurement ||
                    frame.Viewer.SelectedOverlayId!=pending.Id ||
                    draft2.Visibility!=Visibility.Collapsed ||
                    !frame.Viewer.DescribeMeasurement(pending).Contains("最短 3 px"))
                    throw new InvalidOperationException("Ls2Ls第四点未提交唯一Store或清理临时预览");
                frame.Viewer.Overlays.Remove(pending.Id);
                frame.Viewer.SelectOverlay(null);
                // 交点可为两条线段共同端点：第二点等于第三点是合法输入。
                if(frame.Viewer.AppendLinePairPoint(new FGEPoint2D(.5,.5))!=null ||
                   frame.Viewer.AppendLinePairPoint(new FGEPoint2D(2.5,.5))!=null ||
                   frame.Viewer.AppendLinePairPoint(new FGEPoint2D(2.5,.5))!=null)
                    throw new InvalidOperationException("共享端点的两线段被提前提交");
                MVIImageOverlayItem shared=frame.Viewer.AppendLinePairPoint(
                    new FGEPoint2D(2.5,2.5));
                FIMLinePairResult sharedResult=FIMImageMeasurement.MeasureLinePair(
                    shared.Points[0],shared.Points[1],shared.Points[2],shared.Points[3]);
                if(sharedResult.DistancePixels!=0 ||
                    Math.Abs(sharedResult.AngleDegrees-90)>1e-8 ||
                    frame.Viewer.SelectedOverlayId!=shared.Id)
                    throw new InvalidOperationException("Ls2Ls共享端点相交测量错误");
                frame.Viewer.Overlays.Remove(shared.Id);
                frame.Viewer.SelectOverlay(null);
                // 同一条线段的起止端点仍必须拒绝重复，不可生成零长度测量。
                frame.Viewer.AppendLinePairPoint(new FGEPoint2D(.5,.5));
                bool duplicateRejected=false;
                try {frame.Viewer.AppendLinePairPoint(new FGEPoint2D(.5,.5));}
                catch(ArgumentException){duplicateRejected=true;}
                if(!duplicateRejected)
                    throw new InvalidOperationException("Ls2Ls错误接受同一线段零长度");
                frame.Viewer.ToolMode=MVIImageToolMode.Navigate;
                frame.Viewer.ToolMode=MVIImageToolMode.LinePair;
                bool invalidLinePoint=false;
                try {frame.Viewer.AppendLinePairPoint(new FGEPoint2D(-1,1));}
                catch(ArgumentOutOfRangeException){invalidLinePoint=true;}
                if(!invalidLinePoint)
                    throw new InvalidOperationException("Ls2Ls接受图像外测量点");
                frame.Viewer.ToolMode=MVIImageToolMode.Navigate;
                if(draft2.Visibility!=Visibility.Collapsed)
                    throw new InvalidOperationException("退出测量模式后仍保留第二条预览线");
                lineTabs.SelectedIndex=0;
                window.UpdateLayout();
                if (frame.Viewer.ActiveImage != image)
                    throw new InvalidOperationException("FIM image not attached to real MPW viewport");
                // 实际选中多边形ROI并点击统计按钮；不能用手工SetHistogram冒充按钮功能。
                MVIImageOverlayItem statsPolygon=frame.Viewer.AddPolygonRoi(new[]{
                    new FGEPoint2D(0,0),new FGEPoint2D(3.6,0),
                    new FGEPoint2D(0,3.6)});
                frame.Viewer.SelectOverlay(statsPolygon.Id);
                // 四图层色板与已选对象颜色分开：未选对象也可修改图层预设；
                // MPW必须保证已存在图元保留Id并影响后续默认绘制。
                int layerColor=unchecked((int)0xff348abb);
                frame.Viewer.SetOverlayLayerColor(MVIImageOverlayLayer.Roi,layerColor);
                if(frame.Viewer.GetOverlayLayerColor(MVIImageOverlayLayer.Roi)!=layerColor ||
                   frame.Viewer.Overlays.Find(statsPolygon.Id).Argb!=layerColor)
                    throw new InvalidOperationException("修改ROI图层色未影响现有图元或丢失身份");
                var nextColoredRoi=frame.Viewer.AddPolygonRoi(new[]{
                    new FGEPoint2D(0,0),new FGEPoint2D(2,0),new FGEPoint2D(0,2)});
                if(nextColoredRoi.Argb!=layerColor)
                    throw new InvalidOperationException("用户设置的图层色未用于后续绘制");
                frame.Viewer.Overlays.Remove(nextColoredRoi.Id);
                frame.Viewer.SelectOverlay(statsPolygon.Id);
                TabControl propertyTabs=FindControl<TabControl>(frame,"TCT.Imaging.SecondaryTabs");
                propertyTabs.SelectedIndex=0;
                window.UpdateLayout();
                Button colorDialogButton=FindControl<Button>(frame,"TCT.Imaging.ChooseOverlayColor");
                TextBox selectedColorArgbField=FindControl<TextBox>(frame,"TCT.Imaging.OverlayArgb");
                Button selectedColorApplyButton=FindControl<Button>(frame,"TCT.Imaging.ApplyOverlayProperties");
                if(colorDialogButton==null || selectedColorArgbField==null || selectedColorApplyButton==null)
                    throw new InvalidOperationException("选中图元没有真实颜色对话框或属性编辑入口");
                selectedColorArgbField.Text="FF0DA6C3";
                selectedColorApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                MVIImageOverlayItem recolored=frame.Viewer.Overlays.Find(statsPolygon.Id);
                if(recolored==null || recolored.Id!=statsPolygon.Id ||
                    recolored.Argb!=unchecked((int)0xff0da6c3) ||
                    recolored.Argb==layerColor)
                    throw new InvalidOperationException(
                        "点击ROI颜色按钮后没有真正改变原OverlayStore对象样式");
                TabControl statsTabs=FindControl<TabControl>(frame,"TCT.Imaging.SecondaryTabs");
                statsTabs.SelectedIndex=1;
                window.UpdateLayout();
                Button analyzeButton=FindControl<Button>(frame,"TCT.Imaging.AnalyzeStatistics");
                TextBox statsSummary=FindControl<TextBox>(frame,"TCT.Imaging.StatisticsSummary");
                CheckBox useSelectedRoi=FindControl<CheckBox>(frame,"TCT.Imaging.UseSelectedRoi");
                if(analyzeButton==null || statsSummary==null || useSelectedRoi==null)
                    throw new InvalidOperationException("真实ROI统计按钮、状态输入或输出不可用");
                analyzeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                System.Diagnostics.Stopwatch statsWatch=System.Diagnostics.Stopwatch.StartNew();
                while((!statsSummary.Text.Contains("像素数：6") ||
                    !statsSummary.Text.Contains("PolygonRoi") ||
                    frame.HistogramPlot.PointCount!=256) &&
                    statsWatch.ElapsedMilliseconds<10000)
                {
                    System.Windows.Threading.DispatcherFrame loop=
                        new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                if(!statsSummary.Text.Contains("像素数：6") ||
                    !statsSummary.Text.Contains("PolygonRoi") ||
                    frame.HistogramPlot.PointCount!=256)
                    throw new InvalidOperationException(
                        "真实统计按钮未按三角形ROI精确计算6个像素："+statsSummary.Text);
                // 关闭ROI分析必须明确恢复全图，而不是继续显示上次局部统计。
                useSelectedRoi.IsChecked=false;
                analyzeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                statsWatch.Restart();
                while(!statsSummary.Text.Contains("像素数：16") &&
                    statsWatch.ElapsedMilliseconds<10000)
                {
                    System.Windows.Threading.DispatcherFrame loop=
                        new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                if(!statsSummary.Text.Contains("像素数：16") ||
                    !statsSummary.Text.Contains("全图"))
                    throw new InvalidOperationException(
                        "ROI开关关闭后未恢复全图16像素统计："+statsSummary.Text);
                useSelectedRoi.IsChecked=true;
                frame.Viewer.SelectOverlay(null);
                frame.Viewer.Overlays.Remove(statsPolygon.Id);
                statsTabs.SelectedIndex=0;
                window.UpdateLayout();
                frame.Viewer.FitToWindow();
                frame.Viewer.PixelSpacing = new FIMPixelSpacing(0.2, 0.3);
                frame.Viewer.ShowPhysicalRulers = true;
                frame.Viewer.ActualSize();
                if (frame.Viewer.HorizontalRulerControl.Unit != "mm" ||
                    frame.Viewer.VerticalRulerControl.Unit != "mm" ||
                    frame.Viewer.HorizontalRulerControl.EndValue <= frame.Viewer.HorizontalRulerControl.StartValue)
                    throw new InvalidOperationException("Physical ruler not bound to native WPF viewport");
                double startRuler = frame.Viewer.HorizontalRulerControl.EndValue -
                    frame.Viewer.HorizontalRulerControl.StartValue;
                frame.Viewer.FlipViewHorizontal();
                double mirroredRuler = frame.Viewer.HorizontalRulerControl.EndValue -
                    frame.Viewer.HorizontalRulerControl.StartValue;
                if (startRuler * mirroredRuler >= 0)
                    throw new InvalidOperationException("Mirror must reverse the native ruler coordinates");
                frame.Viewer.FlipViewHorizontal();
                bool badSpacing = false;
                try { frame.Viewer.PixelSpacing = default(FIMPixelSpacing); }
                catch (ArgumentOutOfRangeException) { badSpacing = true; }
                if (!badSpacing) throw new InvalidOperationException("Uninitialized physical spacing accepted");
                frame.Viewer.PixelSpacing = null;
                if (frame.Viewer.HorizontalRulerControl.Unit != "px")
                    throw new InvalidOperationException("Removing calibration must restore pixel units");
                frame.Viewer.Zoom(1.25);
                frame.Viewer.ActualSize();
                frame.Viewer.SelectRegion(new FIMPixelRegion(1,1,2,2));
                if (frame.Viewer.SelectedRegion.Value.Width != 2)
                    throw new InvalidOperationException("Interactive ROI state did not persist");
                frame.Viewer.SetDisplayWindow(1,16);
                frame.Viewer.SavePng(file);
                FIMImageBuffer loaded = Mechanism.Presentation.Wpf.Imaging.MPWImageViewerControl.ReadImageFile(file);
                if (loaded.Width != 4 || loaded.Height != 4 || loaded.GetGrayValue(0,0) != 0)
                    throw new InvalidOperationException("PNG roundtrip unexpectedly changed pixel dimensions or window");
                frame.Viewer.SetDisplayWindow(null,null);
                // 显式给微小灰度样本设置窗口，否则16位全范围配合高对比会饱和到255，
                // 反例断言只能测到剪裁，不是显示状态是否真实生效。
                frame.Viewer.SetDisplayWindow(1, 16);
                frame.Viewer.DisplayState.SetInverted(true);
                frame.Viewer.DisplayState.SetGamma(2.0);
                frame.Viewer.DisplayState.SetBrightness(0.1);
                frame.Viewer.DisplayState.SetContrast(1.4);
                frame.Viewer.SavePng(file);
                FIMImageBuffer effects = Mechanism.Presentation.Wpf.Imaging.MPWImageViewerControl.ReadImageFile(file);
                if (effects.Width != 4 || effects.Height != 4 || effects.GetGrayValue(2,2) >= 255)
                    throw new InvalidOperationException("MPW tone controls did not update rendered PNG");
                frame.Viewer.DisplayState.Reset();
                if (!frame.Viewer.DisplayState.IsIdentity || image.GetGrayValue(0,0) != 1)
                    throw new InvalidOperationException("Display state leaked into raw image");

                frame.Viewer.ShowResult(FIMImageOperations.NormalizeToGray8(image));
                frame.Viewer.SelectOriginal();
                if (frame.Viewer.ActiveImage != image)
                    throw new InvalidOperationException("Original/result session identity not preserved");
                // 真实MPW控件的异步结果提交：旧版本绝不覆盖新结果或切换页签。
                long resultGeneration=frame.Viewer.Session.SourceVersion;
                FIMImageBuffer acceptedResult=FIMImageOperations.FlipHorizontal(image);
                if(!frame.Viewer.TryShowResult(resultGeneration,acceptedResult) ||
                    frame.Viewer.ActiveImage!=acceptedResult)
                    throw new InvalidOperationException("MPW current-generation result was not displayed");
                frame.Viewer.SelectOriginal();
                if(frame.Viewer.TryShowResult(resultGeneration,image) ||
                    frame.Viewer.ActiveImage!=image ||
                    frame.Viewer.Session.Result!=acceptedResult)
                    throw new InvalidOperationException("MPW stale result overwrote committed image or selection");
                // 将真实统计和像素剖面数据送入可复用WPF绘图控件，不只输出文字。
                frame.HistogramPlot.SetHistogram(FIMImageAnalyzer.GetHistogram(image, 16));
                frame.ProfilePlot.SetLineProfile(FIMImageAnalyzer.GetLineProfile(image, 0, 0, 3, 3));
                window.UpdateLayout();
                if (frame.HistogramPlot.PointCount != 16 || frame.ProfilePlot.PointCount != 4)
                    throw new InvalidOperationException("WPF histogram or profile curve missing FIM data");
                FIMImageStatistics stats = FIMImageAnalyzer.Calculate(frame.Viewer.ActiveImage);
                if (stats.PixelCount != 16 || Math.Abs(stats.Mean - 8.5) > 1e-9)
                    throw new InvalidOperationException("Raw pixel statistics changed after windowing");
                // 浏览旋转只改变输出方向，不丢失原图数据或原图ROI。
                frame.Viewer.RotateViewClockwise();
                frame.Viewer.SavePng(file);
                FIMImageBuffer turn90 = Mechanism.Presentation.Wpf.Imaging.MPWImageViewerControl.ReadImageFile(file);
                if (turn90.Width != 4 || turn90.Height != 4 || turn90.GetGrayValue(0,0) != 13)
                    throw new InvalidOperationException("WPF 90 degree view pixels inconsistent with source coordinates");
                frame.Viewer.FlipViewHorizontal();
                frame.Viewer.SavePng(file);
                FIMImageBuffer mirrored = Mechanism.Presentation.Wpf.Imaging.MPWImageViewerControl.ReadImageFile(file);
                if (mirrored.GetGrayValue(0,0) != 1 || image.GetGrayValue(0,0) != 1)
                    throw new InvalidOperationException("WPF view-only mirror modified raw pixels or projected incorrectly");
                frame.Viewer.ResetViewTransform();
                if (!frame.Viewer.ViewTransform.IsIdentity)
                    throw new InvalidOperationException("WPF view transform did not reset");
                // 同精度双图混合：原图1..16 与水平翻转的16..13，各像素保持Gray16。
                frame.Viewer.ShowResult(FIMImageOperations.FlipHorizontal(image));
                frame.Viewer.BlendOriginalAndResult(0.5);
                frame.Viewer.SavePng(file);
                FIMImageBuffer blended = Mechanism.Presentation.Wpf.Imaging.MPWImageViewerControl.ReadImageFile(file);
                if (blended.PixelFormat != FIMPixelFormat.Gray16 ||
                    blended.GetGrayValue(0,0) != 3 ||
                    frame.Viewer.Session.Original.GetGrayValue(0,0) != 1 ||
                    frame.Viewer.Session.Result.GetGrayValue(0,0) != 4)
                    throw new InvalidOperationException("WPF original/result Gray16 alpha blend mismatch or modified source");
                frame.Viewer.DisableBlend();
                frame.Viewer.SelectOriginal();
                frame.Viewer.SwipeOriginalAndResult(0.5);
                frame.Viewer.SavePng(file);
                FIMImageBuffer swipePixels = Mechanism.Presentation.Wpf.Imaging.MPWImageViewerControl.ReadImageFile(file);
                if (swipePixels.PixelFormat != FIMPixelFormat.Gray16 ||
                    swipePixels.GetGrayValue(0,0) != image.GetGrayValue(0,0) ||
                    swipePixels.GetGrayValue(3,0) != 1 ||
                    frame.Viewer.Session.Original.GetGrayValue(0,0) != 1)
                    throw new InvalidOperationException("WPF original/result synchronized swipe lost 16-bit pixels");
                frame.Viewer.DisableBlend();

                // P2真正并排：同一MVI会话/原图坐标Store，两个真实WPF PixelImage。
                // 右侧必须显示结果图的原始Gray16像素，而不是Blend/Swipe单图伪分屏。
                TabControl operationTabs = FindControl<TabControl>(frame,
                    "TCT.Imaging.SecondaryTabs");
                operationTabs.SelectedIndex = 4;
                window.UpdateLayout();
                Button sideAction = FindControl<Button>(frame,"TCT.Imaging.SideBySide");
                CheckBox syncControl = FindControl<CheckBox>(frame,"TCT.Imaging.ComparisonSync");
                if (sideAction == null || syncControl == null || syncControl.IsChecked != true)
                    throw new InvalidOperationException("输出页缺少真实左右双视图入口和同步开关");
                sideAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();
                frame.Viewer.FitToWindow();
                window.UpdateLayout();
                BitmapSource rightImage = frame.Viewer.SecondaryImageSource;
                if (!frame.Viewer.IsSideBySideVisible || rightImage == null ||
                    rightImage.PixelWidth != 4 || rightImage.PixelHeight != 4 ||
                    rightImage.Format != PixelFormats.Gray16 ||
                    frame.Viewer.ComparisonState.Mode != MVIImageComparisonMode.SideBySide)
                    throw new InvalidOperationException("SideBySide没有真正构造左右WPF双视图");
                byte[] rightPixels = new byte[4 * 4 * 2];
                rightImage.CopyPixels(rightPixels,8,0);
                if (BitConverter.ToUInt16(rightPixels,0) != 4 ||
                    frame.Viewer.Session.Original.GetGrayValue(0,0) != 1 ||
                    frame.Viewer.Session.Result.GetGrayValue(0,0) != 4)
                    throw new InvalidOperationException("SideBySide结果图灰度16位或原始数据身份错误");
                // 在右侧结果Pane真实屏幕坐标点选四次，必须回写原图中心，
                // 两画布分别绘制草稿而不是右Pane只能看、不能测。
                frame.Viewer.ToolMode=MVIImageToolMode.LinePair;
                FGEPoint2D[] targetPoints={
                    new FGEPoint2D(.5,.5),new FGEPoint2D(3.5,.5),
                    new FGEPoint2D(.5,2.5),new FGEPoint2D(3.5,2.5)
                };
                System.Windows.Shapes.Polyline secondaryFirst=
                    frame.Viewer.FindName("SecondaryDraftPolyline") as
                        System.Windows.Shapes.Polyline;
                System.Windows.Shapes.Polyline secondarySecond=
                    frame.Viewer.FindName("SecondaryDraftLinePair") as
                        System.Windows.Shapes.Polyline;
                if(secondaryFirst==null || secondarySecond==null)
                    throw new InvalidOperationException("右Pane缺少测量双线实时预览画布");
                MVIImageOverlayItem rightPair=null;
                for(int step=0;step<4;step++)
                {
                    Point position=frame.Viewer.SourceToSecondaryViewport(targetPoints[step]);
                    rightPair=frame.Viewer.AppendLinePairAtSecondaryViewport(position);
                    if(step<3 && rightPair!=null)
                        throw new InvalidOperationException("右Pane未完成四点却提前创建两线测量");
                    if(step==2 && (secondaryFirst.Visibility!=Visibility.Visible ||
                        secondarySecond.Visibility!=Visibility.Visible ||
                        secondaryFirst.Points.Count!=2))
                        throw new InvalidOperationException("右Pane未实时显示第一段/第二段草稿");
                }
                if(rightPair==null || rightPair.Points.Count!=4 ||
                    rightPair.Id!=frame.Viewer.SelectedOverlayId ||
                    !frame.Viewer.DescribeMeasurement(rightPair).Contains("最短 2 px") ||
                    frame.Viewer.SecondaryOverlayVisualCount<4 ||
                    secondaryFirst.Visibility!=Visibility.Collapsed ||
                    secondarySecond.Visibility!=Visibility.Collapsed)
                    throw new InvalidOperationException(
                        "右Pane四点回写原图、双Pane完成显示或最短距离错误");
                for(int pointIndex=0;pointIndex<4;pointIndex++)
                    if(rightPair.Points[pointIndex].DistanceTo(targetPoints[pointIndex])>1e-7)
                        throw new InvalidOperationException("右Pane视口逆变换偏离原图像素中心");
                frame.Viewer.Overlays.Remove(rightPair.Id);
                frame.Viewer.SelectOverlay(null);
                frame.Viewer.ToolMode=MVIImageToolMode.Navigate;

                // 真实PNG输出必须覆盖双Pane；否则旧SaveViewPng只截左侧属虚假导出。
                FrameworkElement leftPane = frame.Viewer.FindName("ViewportRoot") as FrameworkElement;
                if (leftPane == null || leftPane.ActualWidth <= 0)
                    throw new InvalidOperationException("左图实际布局尺寸不可用");
                frame.Viewer.SaveViewPng(screenshot);
                using (FileStream fileStream = File.OpenRead(screenshot))
                {
                    PngBitmapDecoder capture = new PngBitmapDecoder(fileStream,
                        BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (capture.Frames.Count != 1 ||
                        capture.Frames[0].PixelWidth < leftPane.ActualWidth * 1.8)
                        throw new InvalidOperationException("左右双视图没有完整写入视口PNG");
                }
                frame.Viewer.ActualSize();
                if (Math.Abs(frame.Viewer.PrimaryViewportScale -
                    frame.Viewer.SecondaryViewportScale) > 1e-9 ||
                    Math.Abs(frame.Viewer.PrimaryViewportCenterX -
                    frame.Viewer.SecondaryViewportCenterX) > 1e-6)
                    throw new InvalidOperationException("双视图同步初始中心/缩放未对齐");
                frame.Viewer.PanComparisonViewport(25,0,false);
                if (Math.Abs(frame.Viewer.PrimaryViewportCenterX -
                    frame.Viewer.SecondaryViewportCenterX) > 1e-6)
                    throw new InvalidOperationException("左视口平移未同步到结果视口");
                frame.Viewer.Zoom(1.25);
                if (Math.Abs(frame.Viewer.PrimaryViewportScale -
                    frame.Viewer.SecondaryViewportScale) > 1e-9 ||
                    Math.Abs(frame.Viewer.PrimaryViewportCenterX -
                    frame.Viewer.SecondaryViewportCenterX) > 1e-6)
                    throw new InvalidOperationException("左图Zoom没有同步倍率与中心到右图");
                syncControl.IsChecked = false;
                if (frame.Viewer.ComparisonState.IsSyncViewEnabled)
                    throw new InvalidOperationException("双视图同步开关未真正更新MVI状态");
                double primaryCenter = frame.Viewer.PrimaryViewportCenterX;
                frame.Viewer.PanComparisonViewport(16,0,true);
                if (Math.Abs(frame.Viewer.PrimaryViewportCenterX-primaryCenter) > 1e-6 ||
                    Math.Abs(frame.Viewer.PrimaryViewportCenterX -
                    frame.Viewer.SecondaryViewportCenterX) < 1.0)
                    throw new InvalidOperationException("关闭同步仍强制锁住两个视口");
                syncControl.IsChecked = true;
                if (!frame.Viewer.ComparisonState.IsSyncViewEnabled)
                    throw new InvalidOperationException("重新启用同步没有更新MVI正式状态");
                if (Math.Abs(frame.Viewer.PrimaryViewportCenterX -
                    frame.Viewer.SecondaryViewportCenterX) > 1e-6)
                    throw new InvalidOperationException("重新开启同步未恢复同一原图中心");
                MVIImageOverlayItem sideRoi = frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.RectangleRoi,MVIImageOverlayLayer.Roi,
                    new FGERect2D(1,1,2,2)));
                if (frame.Viewer.SecondaryOverlayVisualCount < 1 ||
                    frame.Viewer.Overlays.Find(sideRoi.Id) == null)
                    throw new InvalidOperationException("SideBySide右视图未使用统一原图ROI Store");
                string comparisonCapture = Environment.GetEnvironmentVariable(
                    "CXVISUAL_IMAGING_UI_SCREENSHOT");
                if (!string.IsNullOrWhiteSpace(comparisonCapture))
                    CaptureWindow(window,Path.Combine(Path.GetDirectoryName(comparisonCapture),
                        "workbench-side-by-side.png"));
                frame.Viewer.Overlays.Remove(sideRoi.Id);
                Button leaveCompare = FindControl<Button>(frame,"TCT.Imaging.BlendOff");
                if (leaveCompare == null)
                    throw new InvalidOperationException("输出页缺少退出并排比较入口");
                leaveCompare.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (frame.Viewer.IsSideBySideVisible ||
                    frame.Viewer.SecondaryImageSource != null)
                    throw new InvalidOperationException("退出比较没有释放第二视口像素缓冲");
                syncControl.IsChecked = false;
                bool missingComparison = false;
                frame.Viewer.ShowResult(new FIMImageBuffer(2,2,FIMPixelFormat.Gray16,new byte[8]));
                try { frame.Viewer.SideBySideOriginalAndResult(true); }
                catch (InvalidOperationException) { missingComparison = true; }
                if (!missingComparison || frame.Viewer.IsSideBySideVisible)
                    throw new InvalidOperationException("双图尺寸不一致未拒绝或污染比较状态");
                frame.Viewer.ShowResult(FIMImageOperations.FlipHorizontal(image));
                frame.Viewer.SelectOriginal();
                syncControl.IsChecked = true;
                operationTabs.SelectedIndex = 0;
                window.UpdateLayout();

                // 通过区域页按钮与对象列表选择真实ROI，再验证八柄显示、
                // 原图边界限制、显示旋转镜像以及双视口共用同一Store身份。
                TabControl roiTabs=FindControl<TabControl>(frame,"TCT.Imaging.SecondaryTabs");
                roiTabs.SelectedIndex=0;
                window.UpdateLayout();
                Button editAction=FindControl<Button>(frame,"TCT.Imaging.ResizeOverlay");
                ListBox overlayList=FindControl<ListBox>(frame,"TCT.Imaging.OverlayList");
                if (editAction==null || overlayList==null)
                    throw new InvalidOperationException("区域页缺失ROI精细编辑按钮或对象列表");
                MVIImageOverlayItem editable=frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.RectangleRoi,MVIImageOverlayLayer.Roi,
                    new FGERect2D(.5,.5,2.5,2.5)));
                ListBoxItem editRow=overlayList.Items.OfType<ListBoxItem>()
                    .FirstOrDefault(x=>(Guid)x.Tag==editable.Id);
                if (editRow==null)
                    throw new InvalidOperationException("真实ROI创建后未进入区域对象列表");
                overlayList.SelectedItem=editRow;
                TextBox argbField=FindControl<TextBox>(frame,"TCT.Imaging.OverlayArgb");
                TextBox widthField=FindControl<TextBox>(frame,"TCT.Imaging.OverlayLineWidth");
                TextBox labelField=FindControl<TextBox>(frame,"TCT.Imaging.OverlayLabel");
                Button applyProperties=FindControl<Button>(frame,"TCT.Imaging.ApplyOverlayProperties");
                if(argbField==null || widthField==null || labelField==null ||
                    applyProperties==null)
                    throw new InvalidOperationException("ROI对象完整属性编辑入口缺失");
                argbField.Text="FF00AA66";
                widthField.Text="3.25";
                labelField.Text="审计标记";
                applyProperties.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                MVIImageOverlayItem styled=frame.Viewer.Overlays.Find(editable.Id);
                if(styled==null || styled.Id!=editable.Id ||
                    styled.Argb!=unchecked((int)0xff00aa66) ||
                    Math.Abs(styled.LineWidth-3.25)>1e-8 ||
                    styled.Text!="审计标记" ||
                    editable.Argb==styled.Argb)
                    throw new InvalidOperationException(
                        "WPF属性页未同步MVI色彩、线宽、文字或污染原快照");
                Canvas overlayCanvas=frame.Viewer.FindName("OverlayCanvas") as Canvas;
                TextBlock renderedLabel=overlayCanvas==null ? null :
                    overlayCanvas.Children.OfType<TextBlock>()
                        .FirstOrDefault(x=>x.Text=="审计标记");
                if(renderedLabel==null)
                    throw new InvalidOperationException(
                        "ROI文字属性仅更新列表，未在原图画布渲染，操作仍有名无实");
                argbField.Text="无效ARGB";
                applyProperties.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.Overlays.Find(editable.Id).Argb!=unchecked((int)0xff00aa66) ||
                    applyProperties.BorderThickness.Left<1.5 ||
                    !AutomationProperties.GetHelpText(applyProperties).Contains("颜色"))
                    throw new InvalidOperationException(
                        "无效ARGB必须维持旧对象、呈现非模态错误反馈，不得弹窗阻塞WPF");
                // 任意8位ARGB不仅正确写入Store，还应真实参与画布文字透明度绘制。
                argbField.Text="8000AA66";
                applyProperties.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                MVIImageOverlayItem translucent=frame.Viewer.Overlays.Find(editable.Id);
                renderedLabel=overlayCanvas.Children.OfType<TextBlock>()
                    .FirstOrDefault(x=>x.Text=="审计标记");
                SolidColorBrush labelBrush=renderedLabel==null ? null :
                    renderedLabel.Foreground as SolidColorBrush;
                if(translucent==null ||
                    translucent.Argb!=unchecked((int)0x8000aa66) ||
                    labelBrush==null || labelBrush.Color.A!=128 ||
                    labelBrush.Color.G!=170 || labelBrush.Color.B!=102)
                    throw new InvalidOperationException(
                        "半透明ARGB虽写入对象但未呈现在实际WPF文字标签上");
                string styleCsv=Path.Combine(Path.GetTempPath(),
                    "CxVisual-OverlayStyle-"+Guid.NewGuid().ToString("N")+".csv");
                try
                {
                    frame.ExportMeasurementsCsv(styleCsv);
                    string csv=File.ReadAllText(styleCsv);
                    if(!csv.Contains("Argb,LineWidthPx,Locked") ||
                        !csv.Contains("LinePairDistancePixels") ||
                        !csv.Contains("8000AA66") ||
                        !csv.Contains("3.25") || !csv.Contains("审计标记"))
                        throw new InvalidOperationException(
                            "Overlay属性虽显示在WPF但未正确导出CSV结构化字段");
                }
                finally { if(File.Exists(styleCsv))File.Delete(styleCsv); }
                editAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (frame.Viewer.ToolMode!=MVIImageToolMode.ResizeOverlay ||
                    frame.Viewer.SelectedOverlayId!=editable.Id ||
                    frame.Viewer.ResizeHandleVisualCount!=8)
                    throw new InvalidOperationException("真实工具按钮/列表选中没有激活八方向编辑柄");
                MVIImageOverlayItem resizedTop=frame.Viewer.ResizeSelectedOverlay(
                    MVIImageResizeHandle.TopLeft,new FGEPoint2D(-8,-8));
                if (resizedTop.Id!=editable.Id || resizedTop.Bounds.Left!=0 ||
                    resizedTop.Bounds.Top!=0 || resizedTop.Bounds.Right!=3 ||
                    resizedTop.Bounds.Bottom!=3)
                    throw new InvalidOperationException("左上ROI编辑柄未按原图空间约束边界");
                MVIImageOverlayItem resizedBottom=frame.Viewer.ResizeSelectedOverlay(
                    MVIImageResizeHandle.BottomRight,new FGEPoint2D(200,200));
                if (resizedBottom.Bounds.Right!=4 || resizedBottom.Bounds.Bottom!=4 ||
                    resizedBottom.Id!=editable.Id)
                    throw new InvalidOperationException("右下编辑柄没有钳位于原图边界");
                frame.Viewer.RotateViewClockwise();
                frame.Viewer.FlipViewHorizontal();
                if (frame.Viewer.ResizeHandleVisualCount!=8 ||
                    frame.Viewer.Overlays.Find(editable.Id).Bounds.Right!=4)
                    throw new InvalidOperationException("显示旋转镜像破坏ROI手柄或原图几何");
                frame.Viewer.ResetViewTransform();
                frame.Viewer.ShowResult(FIMImageOperations.FlipHorizontal(image));
                frame.Viewer.SideBySideOriginalAndResult(true);
                window.UpdateLayout();
                frame.Viewer.FitToWindow();
                if (frame.Viewer.ResizeHandleVisualCount!=8 ||
                    frame.Viewer.SecondaryResizeHandleVisualCount!=8)
                    throw new InvalidOperationException("两幅WPF图窗没有投影同一ROI八柄");
                frame.Viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Roi,false);
                if (frame.Viewer.ResizeHandleVisualCount!=0 ||
                    frame.Viewer.SecondaryResizeHandleVisualCount!=0)
                    throw new InvalidOperationException("隐藏ROI层仍残留交互控制柄");
                bool hiddenRejected=false;
                try { frame.Viewer.ResizeSelectedOverlay(
                    MVIImageResizeHandle.Right,new FGEPoint2D(3,3)); }
                catch (InvalidOperationException) { hiddenRejected=true; }
                if (!hiddenRejected)
                    throw new InvalidOperationException("隐藏ROI图层仍可被API修改");
                frame.Viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Roi,true);
                frame.Viewer.DisableBlend();
                MVIImageOverlayItem polyEdit=frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.PolygonRoi,MVIImageOverlayLayer.Roi,
                    new FGERect2D(.5,.5,2,2),
                    new[]{new FGEPoint2D(.5,.5),new FGEPoint2D(2.5,.5),
                          new FGEPoint2D(1.5,2.5)}));
                frame.Viewer.SelectOverlay(polyEdit.Id);
                MVIImageOverlayItem editedPolygon=frame.Viewer.ResizeSelectedOverlay(
                    MVIImageResizeHandle.Right,new FGEPoint2D(3.5,2.5));
                if (editedPolygon.Id!=polyEdit.Id ||
                    Math.Abs(editedPolygon.Points[1].X-3.5)>1e-9 ||
                    polyEdit.Points[1].X!=2.5)
                    throw new InvalidOperationException("多边形边柄编辑未保留原图顶点/对象身份");
                // 区域页真实按钮切换逐顶点编辑，验证同一GUID和原图顶点结果。
                Button vertexAction=FindControl<Button>(frame,"TCT.Imaging.EditPolygonVertex");
                if(vertexAction==null)
                    throw new InvalidOperationException("区域页缺少多边形逐顶点编辑入口");
                vertexAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.ToolMode!=MVIImageToolMode.EditPolygonVertex ||
                    frame.Viewer.SelectedOverlayId!=polyEdit.Id ||
                    frame.Viewer.PolygonVertexHandleVisualCount!=3 ||
                    frame.Viewer.ResizeHandleVisualCount!=0)
                    throw new InvalidOperationException("逐顶点模式没有显示实际金色顶点柄");
                MVIImageOverlayItem vertexResult=frame.Viewer.MoveSelectedPolygonVertex(
                    2,new FGEPoint2D(2.6,3.3));
                if(vertexResult.Id!=polyEdit.Id ||
                    Math.Abs(vertexResult.Points[2].X-2.6)>1e-9 ||
                    Math.Abs(vertexResult.Points[2].Y-3.3)>1e-9 ||
                    Math.Abs(vertexResult.Points[0].X-.5)>1e-9 ||
                    Math.Abs(vertexResult.Bounds.Bottom-3.3)>1e-9 ||
                    polyEdit.Points[2].Y!=2.5)
                    throw new InvalidOperationException("顶点编辑没有按原图坐标正确回写唯一Store");
                bool degenerateRejected=false;
                try { frame.Viewer.MoveSelectedPolygonVertex(2,new FGEPoint2D(.5,.5)); }
                catch(ArgumentException) { degenerateRejected=true; }
                if(!degenerateRejected ||
                    frame.Viewer.Overlays.Find(polyEdit.Id).Points[2].Y!=3.3)
                    throw new InvalidOperationException("无效顶点变更不得污染原始多边形");
                // 真实区域页索引输入+WPF按钮：插点、删点、唯一Store与锁状态。
                TextBox vertexIndexBox=FindControl<TextBox>(frame,
                    "TCT.Imaging.PolygonVertexIndex");
                Button insertVertexButton=FindControl<Button>(frame,
                    "TCT.Imaging.InsertPolygonVertex");
                Button removeVertexButton=FindControl<Button>(frame,
                    "TCT.Imaging.RemovePolygonVertex");
                if(vertexIndexBox==null || insertVertexButton==null ||
                    removeVertexButton==null)
                    throw new InvalidOperationException("区域页缺少真实顶点索引输入与插删按钮");
                vertexIndexBox.Text="0";
                insertVertexButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                MVIImageOverlayItem insertedPolygon=frame.Viewer.Overlays.Find(polyEdit.Id);
                if(insertedPolygon==null || insertedPolygon.Points.Count!=4 ||
                    insertedPolygon.Id!=polyEdit.Id ||
                    Math.Abs(insertedPolygon.Points[1].X-2)>1e-8 ||
                    frame.Viewer.PolygonVertexHandleVisualCount!=4)
                    throw new InvalidOperationException("真实WPF插点按钮未更新原图顶点与四个控制柄");
                // 编辑后的ROI必须能以相同Guid和更新后的原图顶点导出结构化CSV。
                string topologyCsvPath=Path.Combine(Path.GetTempPath(),
                    "CxVisual-Imaging-PolygonTopology-"+Guid.NewGuid().ToString("N")+".csv");
                try
                {
                    frame.ExportMeasurementsCsv(topologyCsvPath);
                    string topologyCsv=File.ReadAllText(topologyCsvPath);
                    if(!topologyCsv.Contains(polyEdit.Id.ToString("D")) ||
                        !topologyCsv.Contains("0.5:0.5;2:0.5;3.5:0.5") ||
                        !topologyCsv.Contains("PolygonRoi"))
                        throw new InvalidOperationException(
                            "插入顶点后的唯一ID/原图角点未正确导出CSV");
                }
                finally
                {
                    if(File.Exists(topologyCsvPath)) File.Delete(topologyCsvPath);
                }
                vertexIndexBox.Text="1";
                removeVertexButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                MVIImageOverlayItem removedPolygon=frame.Viewer.Overlays.Find(polyEdit.Id);
                if(removedPolygon==null || removedPolygon.Points.Count!=3 ||
                    frame.Viewer.PolygonVertexHandleVisualCount!=3 ||
                    Math.Abs(removedPolygon.Points[2].Y-3.3)>1e-8)
                    throw new InvalidOperationException("真实WPF删点按钮未恢复三角形");
                bool minPolygonRejected=false;
                try {frame.Viewer.RemoveSelectedPolygonVertex(0);}
                catch(InvalidOperationException) {minPolygonRejected=true;}
                if(!minPolygonRejected ||
                    frame.Viewer.Overlays.Find(polyEdit.Id).Points.Count!=3)
                    throw new InvalidOperationException("最少三顶点约束没有保护唯一Store");
                bool invalidEdgeRejected=false;
                try {frame.Viewer.InsertSelectedPolygonVertex(99,
                    new FGEPoint2D(1,1));}
                catch(ArgumentOutOfRangeException) {invalidEdgeRejected=true;}
                if(!invalidEdgeRejected || frame.Viewer.Overlays.Find(polyEdit.Id).Points.Count!=3)
                    throw new InvalidOperationException("非法插点索引污染了图层");
                frame.Viewer.ShowResult(FIMImageOperations.FlipHorizontal(image));
                frame.Viewer.SideBySideOriginalAndResult(true);
                window.UpdateLayout();
                frame.Viewer.FitToWindow();
                if(frame.Viewer.PolygonVertexHandleVisualCount!=3 ||
                    frame.Viewer.SecondaryPolygonVertexHandleVisualCount!=3)
                    throw new InvalidOperationException("左右双视口未各自投影三个真实多边形顶点");
                vertexIndexBox.Text="0";
                insertVertexButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.PolygonVertexHandleVisualCount!=4 ||
                    frame.Viewer.SecondaryPolygonVertexHandleVisualCount!=4)
                    throw new InvalidOperationException("双视图插点没有同步WPF顶点柄");
                vertexIndexBox.Text="1";
                removeVertexButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.PolygonVertexHandleVisualCount!=3 ||
                    frame.Viewer.SecondaryPolygonVertexHandleVisualCount!=3)
                    throw new InvalidOperationException("双视图删点没有恢复顶点柄");
                frame.Viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Roi,false);
                if(frame.Viewer.PolygonVertexHandleVisualCount!=0 ||
                    frame.Viewer.SecondaryPolygonVertexHandleVisualCount!=0)
                    throw new InvalidOperationException("隐藏ROI仍残留多边形顶点编辑柄");
                bool hiddenVertexRejected=false;
                try { frame.Viewer.MoveSelectedPolygonVertex(1,new FGEPoint2D(2,1)); }
                catch(InvalidOperationException) { hiddenVertexRejected=true; }
                if(!hiddenVertexRejected)
                    throw new InvalidOperationException("隐藏ROI仍允许修改顶点");
                frame.Viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Roi,true);
                frame.Viewer.DisableBlend();
                editAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.ToolMode!=MVIImageToolMode.ResizeOverlay ||
                    frame.Viewer.PolygonVertexHandleVisualCount!=0 ||
                    frame.Viewer.ResizeHandleVisualCount!=8)
                    throw new InvalidOperationException("从逐顶点切回八柄未正确重置编辑显示");
                // 用实际区域页按钮触发锁定/层序，复验同一MVI Store被WPF投影消费。
                Button lockButton=FindControl<Button>(frame,"TCT.Imaging.ToggleOverlayLock");
                Button forwardButton=FindControl<Button>(frame,"TCT.Imaging.BringOverlayForward");
                Button backwardButton=FindControl<Button>(frame,"TCT.Imaging.SendOverlayBackward");
                if (lockButton==null || forwardButton==null || backwardButton==null)
                    throw new InvalidOperationException("区域页未提供对象锁定及前后顺序入口");
                MVIImageOverlayItem zRoi=frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.RectangleRoi,MVIImageOverlayLayer.Roi,
                    new FGERect2D(.5,.5,2.2,2.2)));
                frame.Viewer.SelectOverlay(polyEdit.Id);
                forwardButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (frame.Viewer.Overlays.Items.Last().Id!=polyEdit.Id)
                    throw new InvalidOperationException("前移层级没有真正改变OverlayStore的顶层对象");
                backwardButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (frame.Viewer.Overlays.Items.Last().Id!=zRoi.Id)
                    throw new InvalidOperationException("后移层级没有恢复原顶层对象");
                lockButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (!frame.Viewer.Overlays.Find(polyEdit.Id).IsLocked ||
                    frame.Viewer.ResizeHandleVisualCount!=0)
                    throw new InvalidOperationException("锁定后仍允许显示八柄或没有保存锁状态");
                bool lockedRejected=false;
                try { frame.Viewer.ResizeSelectedOverlay(
                    MVIImageResizeHandle.Right,new FGEPoint2D(3,3)); }
                catch (InvalidOperationException) { lockedRejected=true; }
                if (!lockedRejected)
                    throw new InvalidOperationException("锁定ROI仍允许API缩放");
                bool lockedClearRejected=false;
                try { frame.Viewer.ClearOverlays(MVIImageOverlayLayer.Roi); }
                catch (InvalidOperationException) { lockedClearRejected=true; }
                if (!lockedClearRejected || frame.Viewer.Overlays.Find(polyEdit.Id)==null)
                    throw new InvalidOperationException("普通用户清空操作越过锁定保护");
                lockButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (frame.Viewer.Overlays.Find(polyEdit.Id).IsLocked ||
                    frame.Viewer.ResizeHandleVisualCount!=8)
                    throw new InvalidOperationException("解锁后编辑柄没有恢复");
                frame.Viewer.Overlays.Remove(zRoi.Id);
                frame.Viewer.Overlays.Remove(editable.Id);
                frame.Viewer.Overlays.Remove(polyEdit.Id);
                frame.Viewer.SelectOverlay(null);
                frame.Viewer.ToolMode=MVIImageToolMode.Navigate;
                if (frame.Viewer.ResizeHandleVisualCount!=0 ||
                    frame.Viewer.SecondaryResizeHandleVisualCount!=0)
                    throw new InvalidOperationException("退出编辑模式未清除WPF编辑柄");
                Console.WriteLine("PASS TCT ROI edit: toolbar/list, eight handles, clipping, mirror, dual pane and polygon geometry");
                // 旋转矩形：真实区域页按钮→正交四角→角度编辑→双Pane投影→锁/隐藏反证。
                roiTabs.SelectedIndex=0;
                window.UpdateLayout();
                Button rotatedTool=FindControl<Button>(frame,"TCT.Imaging.RegionRotatedRectangle");
                Button rotationTool=FindControl<Button>(frame,"TCT.Imaging.RotateOverlay");
                if(rotatedTool==null || rotationTool==null)
                    throw new InvalidOperationException("旋转矩形/旋转角柄的真实操作按钮缺失");
                rotatedTool.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.ToolMode!=MVIImageToolMode.RotatedRectangleRoi)
                    throw new InvalidOperationException("旋转矩形按钮未设置MVI正式工具模式");
                MVIImageOverlayItem rotationBox=frame.Viewer.AddRotatedRectangle(
                    new FGERect2D(1,1,2,1),0);
                frame.Viewer.SelectOverlay(rotationBox.Id);
                rotationTool.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.ToolMode!=MVIImageToolMode.RotateOverlay ||
                    frame.Viewer.RotationHandleVisualCount!=1)
                    throw new InvalidOperationException("旋转角柄未实际绘制金色WPF控制柄");
                MVIImageOverlayItem angleBox=frame.Viewer.RotateSelectedRectangle(45);
                if(angleBox.Id!=rotationBox.Id ||
                    Math.Abs(angleBox.RotatedRectangleAngleDegrees-45)>1e-8 ||
                    Math.Abs(angleBox.RotatedRectangleWidth-2)>1e-8 ||
                    Math.Abs(angleBox.RotatedRectangleHeight-1)>1e-8 ||
                    Math.Abs(angleBox.RotatedRectangleArea-2)>1e-8 ||
                    rotationBox.RotatedRectangleAngleDegrees!=0)
                    throw new InvalidOperationException("旋转后原图角度/正交宽高面积或身份错误");
                if(frame.Viewer.HitTestOverlay(2,1.5)==null ||
                    frame.Viewer.HitTestOverlay(2,1.5).Id!=rotationBox.Id)
                    throw new InvalidOperationException("旋转矩形内部命中错误");
                // 已有区域页“八柄缩放”按钮编辑旋转矩形尺寸，但局部轴必须保持正交。
                editAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.ToolMode!=MVIImageToolMode.ResizeOverlay ||
                    frame.Viewer.ResizeHandleVisualCount!=8)
                    throw new InvalidOperationException("旋转矩形没有实际显示八个局部轴编辑柄");
                FGEPoint2D rightMid=new FGEPoint2D(
                    (angleBox.Points[1].X+angleBox.Points[2].X)/2,
                    (angleBox.Points[1].Y+angleBox.Points[2].Y)/2);
                FGEVector2D axis=(angleBox.Points[1]-angleBox.Points[0])/2.0;
                MVIImageOverlayItem sized=frame.Viewer.ResizeSelectedOverlay(
                    MVIImageResizeHandle.Right,rightMid+axis*(-0.25));
                if(sized.Id!=rotationBox.Id ||
                    Math.Abs(sized.RotatedRectangleWidth-1.75)>1e-8 ||
                    Math.Abs(sized.RotatedRectangleHeight-1)>1e-8 ||
                    Math.Abs(sized.RotatedRectangleAngleDegrees-45)>1e-8 ||
                    sized.Points[0].DistanceTo(angleBox.Points[0])>1e-8 ||
                    sized.Points[3].DistanceTo(angleBox.Points[3])>1e-8)
                    throw new InvalidOperationException("旋转矩形八柄未保持局部轴正交或对侧锚定");
                bool resizedOutOfRange=false;
                try {frame.Viewer.ResizeSelectedOverlay(
                    MVIImageResizeHandle.Right,new FGEPoint2D(100,100));}
                catch(ArgumentOutOfRangeException){resizedOutOfRange=true;}
                if(!resizedOutOfRange ||
                    Math.Abs(frame.Viewer.Overlays.Find(rotationBox.Id).RotatedRectangleWidth-1.75)>1e-8)
                    throw new InvalidOperationException("尺寸越界不得更改原有旋转矩形");
                rotationTool.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.RotationHandleVisualCount!=1)
                    throw new InvalidOperationException("旋转矩形尺寸编辑后切回角柄失败");
                // 真正检验WPF坐标投影，不能只验证控制柄数量。
                Canvas firstCanvas=frame.Viewer.FindName("OverlayCanvas") as Canvas;
                if(firstCanvas==null)
                    throw new InvalidOperationException("二维原图OverlayCanvas不可访问");
                System.Windows.Shapes.Ellipse rotationDot=firstCanvas.Children
                    .OfType<System.Windows.Shapes.Ellipse>()
                    .FirstOrDefault(x=>(x.Tag as string)=="RotationHandle");
                if(rotationDot==null)
                    throw new InvalidOperationException("金色旋转柄未生成WPF Shape");
                double beforeX=Canvas.GetLeft(rotationDot);
                double beforeY=Canvas.GetTop(rotationDot);
                frame.Viewer.RotateViewClockwise();
                frame.Viewer.FlipViewHorizontal();
                rotationDot=firstCanvas.Children.OfType<System.Windows.Shapes.Ellipse>()
                    .FirstOrDefault(x=>(x.Tag as string)=="RotationHandle");
                if(rotationDot==null ||
                    (Math.Abs(Canvas.GetLeft(rotationDot)-beforeX)<1e-6 &&
                     Math.Abs(Canvas.GetTop(rotationDot)-beforeY)<1e-6) ||
                    Math.Abs(frame.Viewer.Overlays.Find(rotationBox.Id)
                        .RotatedRectangleAngleDegrees-45)>1e-8)
                    throw new InvalidOperationException(
                        "视图旋转/镜像后的角柄未重新投影或错误修改ROI真实角度");
                frame.Viewer.ResetViewTransform();
                if(frame.Viewer.RotationHandleVisualCount!=1)
                    throw new InvalidOperationException("复位显示状态后角柄未恢复");
                // 用真正超出原图的另一对象验证非法创建不会污染当前Store。
                bool boundsRejected=false;
                try { frame.Viewer.AddRotatedRectangle(new FGERect2D(0,0,3,3),45); }
                catch(ArgumentOutOfRangeException) { boundsRejected=true; }
                if(!boundsRejected || Math.Abs(
                    frame.Viewer.Overlays.Find(rotationBox.Id).RotatedRectangleAngleDegrees-45)>1e-8)
                    throw new InvalidOperationException("越界旋转修改了已保存对象");
                frame.Viewer.ShowResult(FIMImageOperations.FlipHorizontal(image));
                frame.Viewer.SideBySideOriginalAndResult(true);
                window.UpdateLayout();
                frame.Viewer.FitToWindow();
                if(frame.Viewer.RotationHandleVisualCount!=1 ||
                    frame.Viewer.SecondaryRotationHandleVisualCount!=1 ||
                    frame.Viewer.Overlays.Find(rotationBox.Id).Id!=rotationBox.Id)
                    throw new InvalidOperationException("双Pane未在各自视口投影同一旋转矩形角柄");
                editAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(frame.Viewer.ResizeHandleVisualCount!=8 ||
                    frame.Viewer.SecondaryResizeHandleVisualCount!=8)
                    throw new InvalidOperationException("双Pane八柄没有投影在旋转角点和边中点");
                rotationTool.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                frame.Viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Roi,false);
                if(frame.Viewer.RotationHandleVisualCount!=0 ||
                    frame.Viewer.SecondaryRotationHandleVisualCount!=0)
                    throw new InvalidOperationException("隐藏旋转矩形图层仍显示角柄");
                frame.Viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Roi,true);
                frame.Viewer.Overlays.SetLocked(rotationBox.Id,true);
                if(frame.Viewer.RotationHandleVisualCount!=0)
                    throw new InvalidOperationException("锁定对象仍保留可操作的旋转控制柄");
                bool lockedAngleRejected=false;
                try {frame.Viewer.RotateSelectedRectangle(0);}
                catch(InvalidOperationException) {lockedAngleRejected=true;}
                if(!lockedAngleRejected ||
                    Math.Abs(frame.Viewer.Overlays.Find(rotationBox.Id)
                        .RotatedRectangleAngleDegrees-45)>1e-8)
                    throw new InvalidOperationException("锁定旋转矩形未防止几何修改");
                frame.Viewer.Overlays.SetLocked(rotationBox.Id,false);
                frame.Viewer.DisableBlend();
                frame.Viewer.Overlays.Remove(rotationBox.Id);
                frame.Viewer.SelectOverlay(null);
                frame.Viewer.ToolMode=MVIImageToolMode.Navigate;
                if(frame.Viewer.RotationHandleVisualCount!=0)
                    throw new InvalidOperationException("退出旋转编辑后没有清空控制柄");
                Console.WriteLine("PASS TCT RotatedRectangle: WPF tools, orthogonal corners, dual panes, clipping and lock");



                // 程序化创建真实叠加项，让MPW的WPF Canvas实际绘制，并检查层级隐藏/删除。
                MVIImageOverlayItem rect = frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.RectangleRoi, MVIImageOverlayLayer.Roi,
                    new FGERect2D(0, 0, 2, 2)));
                MVIImageOverlayItem distance = frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.DistanceMeasurement, MVIImageOverlayLayer.Measurement,
                    new FGERect2D(0, 0, 3, 3),
                    new [] { new FGEPoint2D(0.5,0.5), new FGEPoint2D(3.5,3.5) }));
                MVIImageOverlayItem polygon = frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.PolygonRoi, MVIImageOverlayLayer.Roi,
                    new FGERect2D(0, 0, 3, 3),
                    new [] { new FGEPoint2D(0,0), new FGEPoint2D(3,0),
                             new FGEPoint2D(2,3) }));
                MVIImageOverlayItem defect = frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.DefectBox, MVIImageOverlayLayer.Defect,
                    new FGERect2D(1,1,2,2), text:"D-01"));
                if (frame.Viewer.VisibleOverlayCount != 4 || frame.Viewer.OverlayVisualCount < 4)
                    throw new InvalidOperationException("Expected overlay geometries not drawn by native WPF Canvas");
                if (frame.Viewer.HitTestOverlay(2.5, 2.5).Kind != MVIImageOverlayKind.DefectBox)
                    throw new InvalidOperationException("Topmost defect hit testing failed");
                if (frame.Viewer.HitTestOverlay(-20, -20) != null)
                    throw new InvalidOperationException("Out-of-image point must not select overlay");
                frame.Viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Roi, false);
                if (frame.Viewer.HitTestOverlay(0.5, 0.5).Kind != MVIImageOverlayKind.DistanceMeasurement)
                    throw new InvalidOperationException("Hidden ROI layer must be excluded from hit test");
                if (frame.Viewer.VisibleOverlayCount != 2 || frame.Viewer.OverlayVisualCount < 2)
                    throw new InvalidOperationException("ROI layer hide did not preserve measurement/defect");
                frame.Viewer.SetOverlayLayerVisible(MVIImageOverlayLayer.Roi, true);
                frame.Viewer.Overlays.Replace(rect.Translate(1, 0));
                if (frame.Viewer.Overlays.Find(rect.Id).Bounds.Left != 1)
                    throw new InvalidOperationException("Overlay move did not update original coordinates");
                frame.Viewer.RotateViewClockwise();
                if (frame.Viewer.VisibleOverlayCount != 4 || frame.Viewer.OverlayVisualCount < 4)
                    throw new InvalidOperationException("Overlay projected objects lost after WPF view rotation");
                frame.Viewer.ResetViewTransform();
                window.UpdateLayout();
                frame.Viewer.SaveViewPng(screenshot);
                if (!File.Exists(screenshot) || new FileInfo(screenshot).Length <= 0)
                    throw new InvalidOperationException("WPF viewport with overlays not exported");
                if (!frame.Viewer.Overlays.Remove(distance.Id) ||
                    frame.Viewer.VisibleOverlayCount != 3)
                    throw new InvalidOperationException("Overlay delete/repaint not reflected");
                MVIImageOverlayItem programPolygon = frame.Viewer.AddPolygonRoi(new[] {
                    new FGEPoint2D(0.5, 0.5), new FGEPoint2D(3.5,0.5),
                    new FGEPoint2D(2.0,3.5) });
                MVIImageOverlayItem angle = frame.Viewer.AddAngleMeasurement(new[] {
                    new FGEPoint2D(0.5,1.5), new FGEPoint2D(1.5,1.5),
                    new FGEPoint2D(1.5,2.5) });
                double degrees = FIMImageMeasurement.GetAngleDegrees(
                    angle.Points[0],angle.Points[1],angle.Points[2]);
                if (Math.Abs(degrees-90) > 0.000001 ||
                    frame.Viewer.VisibleOverlayCount != 5 || frame.Viewer.OverlayVisualCount < 6)
                    throw new InvalidOperationException("WPF polygon/three-point angle failed to render");
                bool badAngle = false;
                try
                {
                    frame.Viewer.AddAngleMeasurement(new[] {
                        new FGEPoint2D(1,1), new FGEPoint2D(1,1), new FGEPoint2D(3,3) });
                }
                catch (ArgumentException) { badAngle = true; }
                if (!badAngle || frame.Viewer.Overlays.Find(programPolygon.Id) == null)
                    throw new InvalidOperationException("Invalid angle geometry must not modify overlay store");
                Console.WriteLine("PASS TCT WPF polygon/angle: geometry, 90 degrees and degenerate rejection");
                frame.Viewer.PixelSpacing = new FIMPixelSpacing(0.2,0.4);
                MVIImageOverlayItem horiz = frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.HorizontalMeasurement,MVIImageOverlayLayer.Measurement,
                    new FGERect2D(.5,1.5,3,0),
                    new[]{new FGEPoint2D(.5,1.5),new FGEPoint2D(3.5,1.5)}));
                MVIImageOverlayItem vert = frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.VerticalMeasurement,MVIImageOverlayLayer.Measurement,
                    new FGERect2D(1.5,.5,0,3),
                    new[]{new FGEPoint2D(1.5,.5),new FGEPoint2D(1.5,3.5)}));
                MVIImageOverlayItem diameter = frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.DiameterMeasurement,MVIImageOverlayLayer.Measurement,
                    new FGERect2D(0,0,4,4)));
                MVIImageOverlayItem area = frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.AreaMeasurement,MVIImageOverlayLayer.Measurement,
                    new FGERect2D(0,0,3,3),new[]{
                    new FGEPoint2D(0,0), new FGEPoint2D(3,0), new FGEPoint2D(0,3)}));
                string length = frame.Viewer.DescribeMeasurement(horiz);
                string elevation = frame.Viewer.DescribeMeasurement(vert);
                string dia = frame.Viewer.DescribeMeasurement(diameter);
                string polygonArea = frame.Viewer.DescribeMeasurement(area);
                if (!length.Contains("0.6 mm") || !elevation.Contains("1.2 mm") ||
                    !dia.Contains("X 0.8 mm / Y 1.6 mm") ||
                    !polygonArea.Contains("4.5 px²") || !polygonArea.Contains("0.36 mm²") ||
                    frame.Viewer.OverlayVisualCount < 10)
                    throw new InvalidOperationException("WPF complete measurement labels, non-square calibration or polygon geometry failed");
                string csvFile = Path.Combine(Path.GetTempPath(),
                    "CxVisual-Imaging-Measurements-"+Guid.NewGuid().ToString("N")+".csv");
                try
                {
                    frame.ExportMeasurementsCsv(csvFile);
                    string csv = File.ReadAllText(csvFile);
                    if (!csv.Contains(horiz.Id.ToString("D")) || !csv.Contains("0.36 mm²") ||
                        !csv.Contains("Vertices") || !csv.Contains("0:0"))
                        throw new InvalidOperationException("Measurement CSV omitted GUID/calibration/vertices");
                }
                finally { if (File.Exists(csvFile)) File.Delete(csvFile); }
                frame.Viewer.PixelSpacing = null;
                if (frame.Viewer.DescribeMeasurement(horiz).Contains("mm"))
                    throw new InvalidOperationException("Uncalibrated image falsely exports millimeters");
                MVIImageOverlayItem profileLine = frame.Viewer.AddOverlay(new MVIImageOverlayItem(
                    MVIImageOverlayKind.LineProfileMeasurement,MVIImageOverlayLayer.Measurement,
                    new FGERect2D(0,0,3,3),new []{
                        new FGEPoint2D(0.5,0.5),new FGEPoint2D(3.5,3.5)}));
                TabControl secondaryTabs = FindControl<TabControl>(frame,"TCT.Imaging.SecondaryTabs");
                secondaryTabs.SelectedIndex = 2;
                window.UpdateLayout();
                Button updateProfile = FindControl<Button>(frame,"TCT.Imaging.LineProfile");
                if (updateProfile == null)
                    throw new InvalidOperationException("No interactive line profile action");
                updateProfile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (frame.ProfilePlot.PointCount != 4 ||
                    frame.Viewer.Overlays.Find(profileLine.Id) == null)
                    throw new InvalidOperationException("Actual profile action did not evaluate drawn line");
                frame.Viewer.ClearOverlays();
                if (frame.Viewer.VisibleOverlayCount != 0 || frame.Viewer.OverlayVisualCount != 0)
                    throw new InvalidOperationException("Overlay clear must remove all native WPF shapes");
                // 真实CPU 3×3环形Mask→FIM有符号外环/孔洞→非等距FIM面积测量→WPF图元。
                FIMImageBuffer donutSource=new FIMImageBuffer(3,3,FIMPixelFormat.Gray16,
                    new byte[]{20,0,20,0,20,0,20,0,0,0,20,0,20,0,20,0,20,0});
                frame.SetOriginal(donutSource);
                frame.Viewer.PixelSpacing=new FIMPixelSpacing(0.2,0.4);
                secondaryTabs.SelectedIndex=3;
                window.UpdateLayout();
                cpuThreshold.Text="10";
                cpuArea.Text="1";
                cpuRun.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var donutWait=System.Diagnostics.Stopwatch.StartNew();
                while(frame.CpuEvidence==null && donutWait.ElapsedMilliseconds<15000)
                {
                    var loop=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                var donutRun=frame.CpuEvidence;
                FIMImageBuffer donutMask=frame.Viewer.Session.Result;
                if(donutRun==null || donutMask==null ||
                    donutRun.Items.Single(i=>i.Name=="阈值命中像素数").IntegerValue!=8 ||
                    donutRun.Items.Single(i=>i.Kind==
                        CxVisual.Professional.Vision.CxVisionResultKind.Region).Region.PixelArea!=8 ||
                    donutMask.GetGrayValue(1,1)!=0 ||
                    donutMask.GetGrayValue(0,0)!=255)
                    throw new InvalidOperationException("CPU环形阈值Mask/候选区域未保留中心孔洞");
                Button donutContoursButton=FindControl<Button>(frame,"TCT.Imaging.ExtractMaskContours");
                if(donutContoursButton==null)
                    throw new InvalidOperationException("环形CPU Mask缺少真实轮廓按钮");
                donutContoursButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                donutWait.Restart();
                while(frame.Viewer.Overlays.Items.Count<2 && donutWait.ElapsedMilliseconds<15000)
                {
                    var loop=new System.Windows.Threading.DispatcherFrame();
                    System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(()=>loop.Continue=false),
                        System.Windows.Threading.DispatcherPriority.Background);
                    System.Windows.Threading.Dispatcher.PushFrame(loop);
                    System.Threading.Thread.Sleep(5);
                }
                var donutRings=frame.Viewer.Overlays.Items.Where(i=>
                    i.Kind==MVIImageOverlayKind.PolygonAnnotation).ToArray();
                if(donutRings.Length!=2 ||
                    Math.Abs(donutRings.Sum(i=>FIMImageMaskContours.SignedArea(i.Points))-8)>1e-9 ||
                    !donutRings.Any(i=>FIMImageMaskContours.SignedArea(i.Points)==9) ||
                    !donutRings.Any(i=>FIMImageMaskContours.SignedArea(i.Points)==-1) ||
                    Math.Abs(donutRings.Sum(i=>
                        Math.Sign(FIMImageMaskContours.SignedArea(i.Points))*
                        FIMImageMeasurement.GetPolygonAreaSquareMillimeters(
                            i.Points,frame.Viewer.PixelSpacing).Value)-0.64)>1e-9 ||
                    !ReferenceEquals(frame.Viewer.Session.Result,donutMask) ||
                    frame.Viewer.Session.Original.GetGrayValue(1,1)!=0)
                    throw new InvalidOperationException(
                        "环形Mask→孔洞轮廓→0.64mm²非等距测量或原图/结果槽不一致");
                Console.WriteLine("PASS TCT CPU Mask donut: outer 9 minus hole 1 = 8 px² / 0.64 mm²");
                Console.WriteLine("PASS TCT WPF overlays: rectangle/line/polygon/defect, layer hide, move, screenshot and release");
                Console.WriteLine("PASS TCT WPF ImageViewer: real control/layout, 16-bit, ROI, zoom, window, PNG and FIM/MVI");
            }
            finally
            {
                window.Close();
                if (File.Exists(file)) File.Delete(file);
                if (File.Exists(screenshot)) File.Delete(screenshot);
            }
        }
    }
}
