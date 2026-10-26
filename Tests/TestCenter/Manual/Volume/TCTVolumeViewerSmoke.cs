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
                Check(frame.LegacyTopTabCount==4 && frame.LegacyBottomTabCount==5,
                    "原版上下4/5页签完整恢复");
                frame.ToggleViewForTest(0);
                Check(frame.IsLegacySingleView,"四视图单窗最大化");
                frame.ToggleViewForTest(0);
                Check(!frame.IsLegacySingleView,"单窗可原位恢复2x2");
                frame.ApplyWindowCenterWidth();
                frame.SetThresholdRangeForTest(.22f,.78f);
                Check(Math.Abs(frame.CurrentThresholdLower-.22f)<.001 &&
                    Math.Abs(frame.CurrentThresholdUpper-.78f)<.001,
                    "旧版阈值上下界拖拽必须回写FVO并触发CT3D LUT");
                Check(frame.Volume != null && frame.ViewState != null,"真实FVO与MVI状态绑定");
                Check(frame.XyViewer.ActiveImage != null &&
                      frame.XzViewer.ActiveImage != null &&
                      frame.YzViewer.ActiveImage != null, "三个MPW WPF图窗均须实际显示图像");
                frame.UpdateCrosshairs();
                Check(frame.VisibleCrosshairCount == 6, "三个二维图窗必须显示六条十字线");
                Check(frame.Scene.SlicePlaneCount == 3, "三张WPF空间贴图切面");
                Check(frame.SliceLegendCount == 3, "旧HALCON三二维图窗均显示灰阶/伪彩色标");
                Check(frame.Scene.CameraDistance > 0 && frame.Scene.OrientationAxisCount==3,
                    "原版三维相机和红绿蓝方向标记应正常初始化");
                frame.ViewState.SetSliceViewport(FVOSlicePlane.XY,0,1,0,1);
                double fullPlaneSpan=frame.Scene.GetPlaneHorizontalSpan(0);
                frame.ViewState.SetSliceViewport(FVOSlicePlane.XY,.2f,.8f,-.1f,1.1f);
                double zoomedSpan=frame.Scene.GetPlaneHorizontalSpan(0);
                Check(fullPlaneSpan>0 &&
                    Math.Abs(zoomedSpan/fullPlaneSpan-.6)<.001,
                    "二维缩放可视窗口必须同步改变WPF三维切面尺寸");
                frame.ViewState.SetSliceViewport(FVOSlicePlane.XY,0,1,0,1);
                int zBeforeWheel=frame.ViewState.SliceZ;
                frame.ScrollSlice(FVOSlicePlane.XY,2);
                Check(frame.ViewState.SliceZ==Math.Min(frame.Volume.OriginalDepth-1,zBeforeWheel+2),
                    "旧HALCON普通鼠标滚轮应切换当前二维平面，而非误缩放");
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
                int scalarCount=frame.ScalarStopCount,colorCount=frame.ColorStopCount;
                frame.AddScalarStopForTest(.72f,.6f);
                frame.AddColorStopForTest(.38f);
                Check(frame.ScalarStopCount==scalarCount+1 &&
                      frame.ColorStopCount==colorCount+1,
                    "WPF交互曲线须直接修改唯一FVO传递函数控制点");
                frame.SetObliqueForTest(true,20,15,45);
                Check(frame.Scene.SlicePlaneCount==4 &&
                      frame.ObliqueViewer.ActiveImage != null &&
                      frame.ViewState.ObliqueSlice.Visible,
                    "任意切FVO三线性结果/3D空间平面与MVI状态必须联动");
                string obliqueRaw=Path.Combine(Path.GetTempPath(),
                    "CxVisual-Oblique-"+Guid.NewGuid().ToString("N")+".raw");
                try
                {
                    frame.SaveHighResolutionObliqueRaw(obliqueRaw);
                    Check(File.Exists(obliqueRaw) && File.Exists(obliqueRaw+".txt") &&
                        new FileInfo(obliqueRaw).Length ==
                        frame.ViewState.ObliqueSlice.Width *
                        frame.ViewState.ObliqueSlice.Height * sizeof(float),
                        "高清任意切原始Float32尺寸与几何元数据必须完整");
                }
                finally
                {
                    if(File.Exists(obliqueRaw))File.Delete(obliqueRaw);
                    if(File.Exists(obliqueRaw+".txt"))File.Delete(obliqueRaw+".txt");
                }
                frame.SetObliqueForTest(false,0,0,0);
                Check(frame.Scene.SlicePlaneCount==3,
                    "关闭任意切不能残留3D空间贴图");
                string file = Environment.GetEnvironmentVariable("CXVISUAL_VOLUME_UI_SCREENSHOT");
                if (!String.IsNullOrWhiteSpace(file))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    window.UpdateLayout();
                    string legacyPng=Path.Combine(Path.GetDirectoryName(file),"four-views.png");
                    frame.SaveFourViewScreenshot(legacyPng);
                    Check(new FileInfo(legacyPng).Length>5000, "旧版四视图完整PNG导出");
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
                // 真实文件从后台FVO加载并回到UI线程；验证不依赖文件对话框。
                string rawFolder=Path.Combine(Path.GetTempPath(),"cxvisual-volume-async-"+
                    Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(rawFolder);
                try
                {
                    string rawPath=Path.Combine(rawFolder,"sample_8x8x8_sp1x1x1.raw");
                    byte[] samples=new byte[512];
                    for(int v=0;v<samples.Length;v++)samples[v]=(byte)(v%255);
                    File.WriteAllBytes(rawPath,samples);
                    System.Threading.Tasks.Task task=frame.LoadRawFileAsync(rawPath,
                        System.Threading.CancellationToken.None);
                    if(!task.IsCompleted)
                    {
                        System.Windows.Threading.DispatcherFrame pump=
                            new System.Windows.Threading.DispatcherFrame();
                        task.ContinueWith(t=>frame.Dispatcher.BeginInvoke(
                            new Action(delegate {pump.Continue=false;})));
                        System.Windows.Threading.Dispatcher.PushFrame(pump);
                    }
                    task.GetAwaiter().GetResult();
                    Check(frame.Volume.OriginalWidth==8 && !frame.IsRawLoading,
                        "旧版RAW异步预览与界面状态回收");
                    frame.LoadDemo();
                }
                finally { Directory.Delete(rawFolder,true); }
                // 真正尝试 WGL Context。CI若只有OpenGL1.1，必须回退而不是伪报GPU成功。
                frame.TryEnableGpu();
                window.UpdateLayout();
                frame.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                    new Action(delegate { }));
                if (!frame.IsGpuVolumeRendering &&
                    !frame.GpuStatusText.Contains("GPU不可用"))
                    throw new InvalidOperationException("GPU驱动不足时没有给出显式失败原因");
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
