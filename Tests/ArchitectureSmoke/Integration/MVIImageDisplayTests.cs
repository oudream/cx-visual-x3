using System;
using CxVisual.Foundation.Imaging;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class MVIImageDisplayTests
    {
        public static void Run()
        {
            MVIImageDisplayState display = new MVIImageDisplayState();
            int events = 0;
            display.Changed += (s, a) => events++;
            Ensure(display.IsIdentity && display.IsAutoWindowLevel, "初始原始显示");
            display.SetWindowLevel(1000, 2000);
            Ensure(!display.IsIdentity && !display.IsAutoWindowLevel &&
                display.WindowCenter == 1000 && display.WindowWidth == 2000 && events == 1,
                "手动窗宽窗位");
            display.SetInverted(true);
            display.SetGamma(2);
            display.SetBrightness(0.1);
            display.SetContrast(1.5);
            Ensure(events == 5, "属性变更逐次通知");
            display.SetContrast(1.5);
            Ensure(events == 5, "相同设置不得多次刷新");
            display.Reset();
            Ensure(display.IsIdentity && events == 6, "重置所有显示状态");
            display.SetTone(2,0.1,1.5,true,true);
            Ensure(display.Gamma==2 && display.Brightness==0.1 &&
                display.Contrast==1.5 && display.IsInverted &&
                display.IsAutoContrast && events==7,"灰阶整组参数仅通知一次");
            display.SetTone(2,0.1,1.5,true,true);
            Ensure(events==7,"重复整组参数不得重绘");
            Throws<ArgumentOutOfRangeException>(()=>
                display.SetTone(2,0.1,double.NaN,false,false),"非法整组灰阶拒绝");
            Ensure(display.Gamma==2 && display.IsInverted &&
                display.IsAutoContrast && events==7,
                "整组非法设置不允许部分写入或发送通知");
            display.Reset();
            Ensure(display.IsIdentity && events==8,"整组设置可以复位");
            Throws<ArgumentOutOfRangeException>(() => display.SetGamma(0), "gamma");
            Throws<ArgumentOutOfRangeException>(() => display.SetBrightness(double.NaN), "NaN");
            Throws<ArgumentOutOfRangeException>(() => display.SetWindowLevel(0, -1), "非法窗宽");

            FIMImageBuffer gray16 = new FIMImageBuffer(3,1,FIMPixelFormat.Gray16,
                new byte[] { 0,0, 0,4, 0,8 });
            FIMImageBuffer mapped = FIMImageToneMapping.MapGray(gray16, 0, 2048, 1,0,1,false);
            Ensure(mapped.PixelFormat == FIMPixelFormat.Gray8 &&
                mapped.GetGrayValue(0,0) == 0 && mapped.GetGrayValue(1,0) == 128 &&
                mapped.GetGrayValue(2,0) == 255, "原始16位到8位呈现");
            FIMImageBuffer inverse = FIMImageToneMapping.MapGray(gray16, 0, 2048, 1,0,1,true);
            Ensure(inverse.GetGrayValue(0,0) == 255 && inverse.GetGrayValue(2,0) == 0,
                "反色");
            Ensure(gray16.GetGrayValue(1,0) == 1024 && gray16.GetGrayValue(2,0) == 2048,
                "源16位像素未被任何显示影响");
            FIMImageBuffer color = new FIMImageBuffer(1,1,FIMPixelFormat.Bgra32,new byte[] { 10,90,250,77 });
            FIMImageBuffer colorMapped = FIMImageToneMapping.MapColor(color, 1,0,1,true);
            byte[] channels = colorMapped.CopyPixels();
            Ensure(channels[0] == 245 && channels[1] == 165 && channels[2] == 5 &&
                channels[3] == 77, "彩色BGR及alpha保留");
            FIMImageBuffer darker = FIMImageToneMapping.MapGray(gray16,0,2048,1,-0.3,1,false);
            Ensure(darker.GetGrayValue(1,0) < mapped.GetGrayValue(1,0), "亮度");
            FIMImageBuffer gamma = FIMImageToneMapping.MapGray(gray16,0,2048,2,0,1,false);
            Ensure(gamma.GetGrayValue(1,0) > mapped.GetGrayValue(1,0), "Gamma");
            Throws<ArgumentOutOfRangeException>(() =>
                FIMImageToneMapping.MapGray(gray16,0,0,1,0,1,false), "非法窗口端点");
            Console.WriteLine("PASS MVI DisplayState/FIM ToneMapping 16bit/color/alpha/gamma/window");
        }

        private static void Ensure(bool result,string description)
        { if(!result) throw new InvalidOperationException("MVI显示状态失败："+description); }
        private static void Throws<T>(Action action,string description) where T:Exception
        { try { action(); } catch(T){ return; } throw new InvalidOperationException("未抛出预期异常："+description); }
    }
}
