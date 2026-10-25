using System;
using System.IO;
using System.Windows;
using CxVisual.Foundation.Imaging;

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
                frame.Viewer.ShowResult(FIMImageOperations.NormalizeToGray8(image));
                frame.Viewer.SelectOriginal();
                if (frame.Viewer.ActiveImage != image)
                    throw new InvalidOperationException("Original/result session identity not preserved");
                FIMImageStatistics stats = FIMImageAnalyzer.Calculate(frame.Viewer.ActiveImage);
                if (stats.PixelCount != 16 || Math.Abs(stats.Mean - 8.5) > 1e-9)
                    throw new InvalidOperationException("Raw pixel statistics changed after windowing");
                Console.WriteLine("PASS TCT WPF ImageViewer: real control/layout, 16-bit, ROI, zoom, window, PNG and FIM/MVI");
            }
            finally
            {
                window.Close();
                if (File.Exists(file)) File.Delete(file);
            }
        }
    }
}
