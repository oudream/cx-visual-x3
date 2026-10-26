using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CxVisual.Foundation.Volume;
using CxVisual.Mechanism.Visual;

namespace CxVisual.TestCenter.UI
{
    /// <summary>在真实Windows WPF窗口中执行四视图/三刀/三维切片及PNG可视回归，不冒充GPU体绘。</summary>
    internal static class TCTVolumeViewerSmoke
    {
        public static void Run()
        {
            TCTManualVolumeViewerFrame frame = new TCTManualVolumeViewerFrame();
            Window window = new Window {
                Title = "CxVisual 3D Volume Workbench Acceptance",
                Width = 1280, Height = 850,
                WindowStyle = WindowStyle.ToolWindow, ShowInTaskbar = false,
                Content = frame
            };
            try
            {
                window.Show();
                window.UpdateLayout();
                frame.LoadDemo();
                window.UpdateLayout();
                Check(frame.Volume != null && frame.ViewState != null,"真实FVO与MVI状态绑定");
                Check(frame.XyViewer.ActiveImage != null &&
                      frame.XzViewer.ActiveImage != null &&
                      frame.YzViewer.ActiveImage != null, "三个MPW WPF图窗均须实际显示图像");
                frame.UpdateCrosshairs();
                Check(frame.VisibleCrosshairCount == 6, "三个二维图窗必须显示六条十字线");
                Check(frame.Scene.SlicePlaneCount == 3, "三张WPF空间贴图切面");
                Check(frame.Scene.CameraDistance > 0, "三维相机被初始化");
                frame.SetCrosshairFromImage(FVOSlicePlane.XY,6,9);
                Check(frame.ViewState.SliceX == 6 && frame.ViewState.SliceY == 9,
                    "二维切片坐标必须反算为MVI三刀索引");
                frame.MoveSlices(11,19,23);
                Check(frame.ViewState.SliceX == 11 && frame.ViewState.SliceY == 19 &&
                      frame.ViewState.SliceZ == 23, "三刀共用MVI状态");
                Check(frame.Scene.SlicePlaneCount == 3 &&
                      frame.XyViewer.ActiveImage != null, "移动三刀后四视图联动更新");
                frame.Center();
                Check(frame.ViewState.SliceX == frame.Volume.OriginalWidth/2 &&
                      frame.ViewState.SliceZ == frame.Volume.OriginalDepth/2,
                    "三刀中心复位");
                FVOVolumeData before = frame.Volume;
                frame.LoadDemo();
                Check(before != frame.Volume && !Object.ReferenceEquals(before,frame.Volume),
                    "更换体数据必须更新FVO对象与纹理贴图");
                string file = Environment.GetEnvironmentVariable("CXVISUAL_VOLUME_UI_SCREENSHOT");
                if (!String.IsNullOrWhiteSpace(file))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    window.UpdateLayout();
                    RenderTargetBitmap bitmap = new RenderTargetBitmap(
                        (int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),
                        96,96,PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    PngBitmapEncoder png = new PngBitmapEncoder();
                    png.Frames.Add(BitmapFrame.Create(bitmap));
                    using (FileStream stream = File.Create(file)) png.Save(stream);
                    Check(new FileInfo(file).Length > 8000, "输出实际WPF窗口截图不能为空");
                }
                string empty = Path.Combine(Path.GetTempPath(),
                    "cxvisual-volume-empty-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(empty);
                try
                {
                    bool rejected = false;
                    try { TCTManualVolumeViewerFrame.LoadBmpStack(empty); }
                    catch (InvalidDataException) { rejected = true; }
                    Check(rejected, "无BMP切片目录必须明确拒绝");
                }
                finally { Directory.Delete(empty); }
                frame.OnWorkspaceClosed();
                Check(frame.Volume == null && frame.Scene.SlicePlaneCount == 0 &&
                      frame.VisibleCrosshairCount == 0, "关闭时应解除MVI状态、释放体数据及切面可视对象");
                Console.WriteLine("PASS TCT Volume native WPF 4-view, FVO/MVI, crosshair, slice scene and screenshot");
            }
            finally
            {
                window.Close();
            }
        }
        private static void Check(bool ok, string subject)
        {
            if (!ok) throw new InvalidOperationException("三维WPF验收失败：" + subject);
        }
    }
}
