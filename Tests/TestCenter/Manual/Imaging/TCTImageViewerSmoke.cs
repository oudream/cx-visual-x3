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
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Visual;

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
                FindControl<Button>(frame,"TCT.Imaging.Gaussian") == null)
                throw new InvalidOperationException("测量/变换功能页缺少真实操作按钮。");
            primary.SelectedIndex = 4;
            secondary.SelectedIndex = 4;
            window.UpdateLayout();
            if (FindControl<Button>(frame,"TCT.Imaging.DefectBox") == null ||
                FindControl<Button>(frame,"TCT.Imaging.SaveViewPng") == null ||
                FindControl<Button>(frame,"TCT.Imaging.RawOpen") == null)
                throw new InvalidOperationException("标注/输出页遗失旧操作入口。");
            primary.SelectedIndex = 0;
            secondary.SelectedIndex = 0;
            window.UpdateLayout();
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
                LayoutAnchorable inspector = host.TestDock.Layout.Descendents()
                    .OfType<LayoutAnchorable>().FirstOrDefault(p => p.ContentId == "test.inspector");
                if (inspector == null) throw new InvalidOperationException("TestCenter inspector not present");
                TCTManualImageViewerFrame imaging = host.DocumentHost.OpenWorkspace(
                    registry.Find("manual.imaging")) as TCTManualImageViewerFrame;
                if (imaging == null || inspector.IsVisible)
                    throw new InvalidOperationException("TestCenter failed to focus imaging workbench and hide duplicate inspector");
                imaging.SetOriginal(new FIMImageBuffer(900,650,FIMPixelFormat.Gray8,
                    BuildSyntheticPixels(900,650)));
                host.UpdateLayout();
                string path = Environment.GetEnvironmentVariable("CXVISUAL_IMAGING_UI_SCREENSHOT");
                if (!string.IsNullOrWhiteSpace(path))
                {
                    string full = Path.Combine(Path.GetDirectoryName(path),"testcenter-workbench.png");
                    CaptureWindow(host,full);
                }
                host.DocumentHost.OpenWorkspace(registry.Find("manual.root"));
                host.UpdateLayout();
                if (!inspector.IsVisible)
                    throw new InvalidOperationException("TestCenter Inspector not restored after leaving imaging page");
                Console.WriteLine("PASS TCT Imaging Shell: AvalonDock workbench, focus, auto-collapse/restore and actual UI");
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
            frame.SetOriginal(new FIMImageBuffer(width,height,FIMPixelFormat.Gray8,
                BuildSyntheticPixels(width,height)));
            frame.Viewer.FitToWindow();
            CaptureWindow(window,path);
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
                if (frame.Viewer.ActiveImage != image)
                    throw new InvalidOperationException("FIM image not attached to real MPW viewport");
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
                    !polygonArea.Contains("4.5 px²") || !polygonArea.Contains("1.8 mm²") ||
                    frame.Viewer.OverlayVisualCount < 10)
                    throw new InvalidOperationException("WPF complete measurement labels, non-square calibration or polygon geometry failed");
                string csvFile = Path.Combine(Path.GetTempPath(),
                    "CxVisual-Imaging-Measurements-"+Guid.NewGuid().ToString("N")+".csv");
                try
                {
                    frame.ExportMeasurementsCsv(csvFile);
                    string csv = File.ReadAllText(csvFile);
                    if (!csv.Contains(horiz.Id.ToString("D")) || !csv.Contains("1.8 mm²") ||
                        !csv.Contains("Vertices") || !csv.Contains("0:0"))
                        throw new InvalidOperationException("Measurement CSV omitted GUID/calibration/vertices");
                }
                finally { if (File.Exists(csvFile)) File.Delete(csvFile); }
                frame.Viewer.PixelSpacing = null;
                if (frame.Viewer.DescribeMeasurement(horiz).Contains("mm"))
                    throw new InvalidOperationException("Uncalibrated image falsely exports millimeters");
                frame.Viewer.ClearOverlays();
                if (frame.Viewer.VisibleOverlayCount != 0 || frame.Viewer.OverlayVisualCount != 0)
                    throw new InvalidOperationException("Overlay clear must remove all native WPF shapes");
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
