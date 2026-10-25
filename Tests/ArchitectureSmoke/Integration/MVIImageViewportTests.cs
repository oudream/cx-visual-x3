using System;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class MVIImageViewportTests
    {
        public static void Run()
        {
            MVIImageViewport view = new MVIImageViewport();
            view.Fit(100, 50, 200, 200);
            Check(Near(view.Scale, 2) && Near(view.OffsetY, 50), "Fit");
            double beforeX = view.ImageX(80), beforeY = view.ImageY(100);
            view.ZoomAt(1.5, 80, 100);
            Check(Near(view.ImageX(80), beforeX) && Near(view.ImageY(100), beforeY), "鼠标锚点");
            view.Pan(5, -2);
            Check(!view.IsFit && Near(view.ImageX(85), beforeX), "平移逆映射");
            view.ActualSize(100, 50, 200, 200);
            Check(Near(view.Scale, 1), "1:1");
            bool invalid = false;
            try { view.ZoomAt(0, 0, 0); } catch (ArgumentOutOfRangeException) { invalid = true; }
            Check(invalid, "非法缩放");
            Console.WriteLine("PASS MVI ImageViewport Fit/Pan/ZoomAt");
        }
        private static bool Near(double a,double b) { return Math.Abs(a-b)<1e-8; }
        private static void Check(bool ok,string caseName)
        { if (!ok) throw new InvalidOperationException("MVI viewport: "+caseName); }
    }
}
