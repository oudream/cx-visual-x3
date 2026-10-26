using System;
using System.IO;
using System.Windows;
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
                FIMImageBuffer image = new FIMImageBuffer(4, 4, FIMPixelFormat.Gray16,
                    new byte[] { 1,0, 2,0, 3,0, 4,0, 5,0, 6,0, 7,0, 8,0,
                                 9,0, 10,0, 11,0, 12,0, 13,0, 14,0, 15,0, 16,0 });
                frame.SetOriginal(image);
                if (frame.Viewer.ActiveImage != image)
                    throw new InvalidOperationException("FIM image not attached to real MPW viewport");
                frame.Viewer.FitToWindow();
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
