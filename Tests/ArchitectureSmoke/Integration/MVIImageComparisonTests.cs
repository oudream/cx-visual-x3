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
            FIMImageBuffer slideA = new FIMImageBuffer(4,1,FIMPixelFormat.Gray16,
                new byte[]{1,0,2,0,3,0,4,0});
            FIMImageBuffer slideB = new FIMImageBuffer(4,1,FIMPixelFormat.Gray16,
                new byte[]{10,0,20,0,30,0,40,0});
            FIMImageBuffer swipe = FIMImageBlend.Swipe(slideA,slideB,.5);
            Ensure(swipe.GetGrayValue(0,0)==1 && swipe.GetGrayValue(1,0)==2 &&
                swipe.GetGrayValue(2,0)==30 && swipe.GetGrayValue(3,0)==40,
                "滑动比较左右均使用原图坐标、保持Gray16位深");
            Ensure(FIMImageBlend.Swipe(slideA,slideB,0).GetGrayValue(0,0)==10 &&
                FIMImageBlend.Swipe(slideA,slideB,1).GetGrayValue(3,0)==4,
                "滑动分界边界");
            Ensure(slideA.GetGrayValue(0,0)==1 && slideB.GetGrayValue(0,0)==10,
                "滑动比较不修改原图");
            Fail<ArgumentException>(()=> FIMImageBlend.Swipe(slideA,a,.5),"滑动格式尺寸错误");
            Fail<ArgumentOutOfRangeException>(()=>FIMImageBlend.Swipe(slideA,slideB,double.NaN),
                "滑动比例错误");
            compare.SetMode(MVIImageComparisonMode.Swipe);
            Ensure(compare.Mode == MVIImageComparisonMode.Swipe,"滑动模式状态");
            MVIImageViewport originalView = new MVIImageViewport();
            MVIImageViewport resultView = new MVIImageViewport();
            originalView.ActualSize(200,100,400,200);
            originalView.Pan(25,-15);
            resultView.SynchronizeFrom(originalView,400,200,260,180);
            Ensure(Math.Abs(resultView.Scale-originalView.Scale)<1e-10 &&
                Math.Abs(resultView.ImageX(130)-originalView.ImageX(200))<1e-10 &&
                Math.Abs(resultView.ImageY(90)-originalView.ImageY(100))<1e-10,
                "左右不同视口尺寸下以图像中心同步缩放/平移");
            resultView.ZoomAt(1.3,130,90);
            originalView.SynchronizeFrom(resultView,260,180,400,200);
            Ensure(Math.Abs(resultView.ImageX(130)-originalView.ImageX(200))<1e-10 &&
                Math.Abs(resultView.Scale-originalView.Scale)<1e-10,
                "结果视口反向同步原图坐标和倍率");
            Fail<ArgumentNullException>(()=>resultView.SynchronizeFrom(null,400,200,260,180),
                "空源视口必须拒绝");
            Fail<ArgumentOutOfRangeException>(()=>resultView.SynchronizeFrom(originalView,0,200,260,180),
                "零视口宽度必须拒绝");
            Console.WriteLine("PASS MVI ImageComparison/FIM Blend+Swipe Gray16/BGRA");
        }
        private static void Ensure(bool cond,string description)
        {if(!cond)throw new InvalidOperationException("MVI图像比较："+description);}
        private static void Fail<T>(Action action,string description) where T:Exception
        {try{action();}catch(T){return;}throw new InvalidOperationException("未抛异常："+description);}
    }
}
