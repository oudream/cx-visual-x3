using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CxVisual.TestCenter.UI
{
    /// <summary>
    /// 必须在真实OpenGL 3.3驱动的Windows工作站执行；不可用时显式失败，绝不把WPF切面当体绘。
    /// 验证GPU context、真实3D纹理、旧GLSL编译、Raymarch帧非空、重复装载和释放。
    /// </summary>
    internal static class TCTVolumeGpuHardwareSmoke
    {
        public static void Run()
        {
            TCTManualVolumeViewerFrame frame=new TCTManualVolumeViewerFrame();
            Window window=new Window { Width=1400,Height=960,
                Title="CxVisual GPU real acceptance (OpenGL 3.3)",
                Content=frame, WindowStyle=WindowStyle.ToolWindow,ShowInTaskbar=false };
            Stopwatch clock=Stopwatch.StartNew();
            try
            {
                window.Show();window.UpdateLayout();
                frame.LoadDemo();
                frame.SetObliqueForTest(true,22,13,40);
                frame.TryEnableGpu();
                window.UpdateLayout();
                frame.Dispatcher.Invoke(DispatcherPriority.Background,new Action(delegate {}));
                if(!frame.IsGpuVolumeRendering)
                    throw new NotSupportedException("GPU实机验收失败（禁止回退后宣称通过）："+frame.GpuStatusText);
                string driver=frame.GpuStatusText;
                BitmapSource pixels=frame.CaptureGpuFrameForTest();
                if(pixels.PixelWidth<200 || pixels.PixelHeight<150)
                    throw new InvalidOperationException("GPU framebuffer宽高异常。");
                int stride=checked(pixels.PixelWidth*4);
                byte[] rgba=new byte[checked(stride*pixels.PixelHeight)];
                pixels.CopyPixels(rgba,stride,0);
                int nonBackground=0;
                for(int i=0;i<rgba.Length;i+=Math.Max(4,(rgba.Length/25000)&~3))
                    if(rgba[i]>35 || rgba[i+1]>35 || rgba[i+2]>35) nonBackground++;
                if(nonBackground<200)
                    throw new InvalidOperationException("GPU帧只有清屏色，没有真实Raymarch/切面像素。");
                string path=Environment.GetEnvironmentVariable("CXVISUAL_GPU_UI_SCREENSHOT");
                if(string.IsNullOrEmpty(path))
                    path=Path.Combine(Path.GetTempPath(),"CxVisual-GPU-Hardware-Raymarch.png");
                string folder=Path.GetDirectoryName(Path.GetFullPath(path));
                if(!Directory.Exists(folder))Directory.CreateDirectory(folder);
                PngBitmapEncoder encoder=new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(pixels));
                using(FileStream output=File.Create(path))encoder.Save(output);
                if(new FileInfo(path).Length<4096)
                    throw new InvalidOperationException("真实GPU PNG文件长度过小。");
                for(int cycle=0;cycle<3;cycle++)
                {
                    frame.LoadDemo();
                    if(!frame.IsGpuVolumeRendering)
                        throw new InvalidOperationException("重复载入后GPU上下文丢失: "+cycle);
                    BitmapSource frameImage=frame.CaptureGpuFrameForTest();
                    if(frameImage.PixelWidth!=pixels.PixelWidth)
                        throw new InvalidOperationException("重载后GPU视口宽度异常。");
                }
                clock.Stop();
                Console.WriteLine("PASS real OpenGL hardware; "+driver+"; PNG="+path+
                    "; replay=3; elapsedMs="+clock.ElapsedMilliseconds);
            }
            finally
            {
                frame.OnWorkspaceClosed();
                window.Close();
            }
        }
    }
}
