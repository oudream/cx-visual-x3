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
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd,uint message,UIntPtr wParam,IntPtr lParam);
        private static IntPtr CursorPoint(int x,int y)
        {
            return new IntPtr((y<<16)|(x&0xffff));
        }
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
                // 必须给真实OpenGL子窗口发送Win32鼠标消息，而不是直接调用相机API。
                CxVisual.Mechanism.Presentation.Wpf.Volume.Rendering.MPWVolumeOpenGlControl gl=
                    frame.GpuControlForHardwareTest;
                if(gl==null||gl.NativeHandleForHardwareTest==IntPtr.Zero)
                    throw new InvalidOperationException("GPU子窗口未实际创建。");
                float yaw=gl.CameraYaw,distance=gl.CameraDistance;
                SendMessage(gl.NativeHandleForHardwareTest,0x0201,new UIntPtr(1),CursorPoint(60,60));
                SendMessage(gl.NativeHandleForHardwareTest,0x0200,new UIntPtr(1),CursorPoint(110,85));
                SendMessage(gl.NativeHandleForHardwareTest,0x0202,UIntPtr.Zero,CursorPoint(110,85));
                if(Math.Abs(gl.CameraYaw-yaw)<.02f)
                    throw new InvalidOperationException("真实GPU窗口左键拖动未旋转相机。");
                SendMessage(gl.NativeHandleForHardwareTest,0x0204,new UIntPtr(2),CursorPoint(65,60));
                SendMessage(gl.NativeHandleForHardwareTest,0x0200,new UIntPtr(2),CursorPoint(105,80));
                SendMessage(gl.NativeHandleForHardwareTest,0x0205,UIntPtr.Zero,CursorPoint(105,80));
                if(gl.CameraTarget.Length()<.001f)
                    throw new InvalidOperationException("真实GPU窗口右键拖动未平移相机。");
                SendMessage(gl.NativeHandleForHardwareTest,0x020A,
                    new UIntPtr((uint)(120<<16)),CursorPoint(100,100));
                if(Math.Abs(gl.CameraDistance-distance)<.05f || gl.NativePointerEvents<5)
                    throw new InvalidOperationException("真实GPU窗口滚轮缩放或原生鼠标事件未接通。");
                // 旧版操作应实时：材质、采样及切面显隐不得每次扫描体素构造新LUT。
                int oldUploads=frame.GpuTransferUploadCount;
                frame.SetOpacityFromPanelForTest(.61);
                frame.SetSliceVisibilityFromPanelForTest(false);
                if(frame.GpuTransferUploadCount!=oldUploads)
                    throw new InvalidOperationException("GPU灯光/透明度滑杆触发了不必要的RAW全体扫描/LUT上传。");
                frame.SetColorCurveEnabledFromPanelForTest(true);
                if(frame.GpuTransferUploadCount<=oldUploads)
                    throw new InvalidOperationException("GPU颜色曲线改变后必须上传真实LUT。");
                frame.SetColorCurveEnabledFromPanelForTest(false);
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
