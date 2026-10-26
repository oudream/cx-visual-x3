using System;
using CxVisual.Foundation.Imaging;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class MVIImageComparisonTests
    {
        public static void Run()
        {
            MVIImageComparisonState compare = new MVIImageComparisonState();
            int changed = 0; compare.Changed += (s,e) => changed++;
            compare.SetMode(MVIImageComparisonMode.Blend);
            compare.SetBlendRatio(.25);
            compare.SetSyncView(true);
            Ensure(compare.Mode == MVIImageComparisonMode.Blend &&
                compare.BlendRatio == .25 && compare.IsSyncViewEnabled && changed == 3,
                "比较状态/Observer");
            compare.SetBlendRatio(.25);
            Ensure(changed == 3,"同值不重发");
            compare.Reset();
            Ensure(compare.Mode == MVIImageComparisonMode.Single &&
                compare.BlendRatio == .5 && !compare.IsSyncViewEnabled, "复位");
            Fail<ArgumentOutOfRangeException>(() => compare.SetBlendRatio(-.1),"错误混合率");
            Fail<ArgumentOutOfRangeException>(() => compare.SetMode((MVIImageComparisonMode)100),"未知比较方式");
            FIMImageBuffer a = new FIMImageBuffer(1,1,FIMPixelFormat.Gray16,new byte[]{0,4});
            FIMImageBuffer b = new FIMImageBuffer(1,1,FIMPixelFormat.Gray16,new byte[]{0,8});
            FIMImageBuffer avg = FIMImageBlend.Blend(a,b,.25);
            Ensure(avg.PixelFormat == FIMPixelFormat.Gray16 && avg.GetGrayValue(0,0) == 1280 &&
                a.GetGrayValue(0,0) == 1024 && b.GetGrayValue(0,0) == 2048, "16位数据叠合与源图不变");
            Ensure(FIMImageBlend.Blend(a,b,0).GetGrayValue(0,0) == 1024 &&
                FIMImageBlend.Blend(a,b,1).GetGrayValue(0,0) == 2048, "权重边界");
            FIMImageBuffer colorA = new FIMImageBuffer(1,1,FIMPixelFormat.Bgra32,new byte[]{0,50,200,100});
            FIMImageBuffer colorB = new FIMImageBuffer(1,1,FIMPixelFormat.Bgra32,new byte[]{200,150,0,200});
            byte[] mixed = FIMImageBlend.Blend(colorA,colorB,.5).CopyPixels();
            Ensure(mixed[0] == 100 && mixed[1] == 100 && mixed[2] == 100 && mixed[3] == 150,
                "Bgra四通道按权重合成");
            Fail<ArgumentException>(()=> FIMImageBlend.Blend(a,colorA,.5), "格式不同");
            Fail<ArgumentOutOfRangeException>(()=> FIMImageBlend.Blend(a,b,double.NaN), "NaN权重");
            Console.WriteLine("PASS MVI ImageComparison/FIM Blend Gray16/BGRA");
        }
        private static void Ensure(bool cond,string description)
        {if(!cond)throw new InvalidOperationException("MVI图像比较："+description);}
        private static void Fail<T>(Action action,string description) where T:Exception
        {try{action();}catch(T){return;}throw new InvalidOperationException("未抛异常："+description);}
    }
}
