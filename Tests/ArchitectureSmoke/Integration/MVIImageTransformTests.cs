using System;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class MVIImageTransformTests
    {
        public static void Run()
        {
            MVIImageTransformState t = new MVIImageTransformState();
            int changes = 0;
            t.Changed += (s,e) => changes++;
            FGEPoint2D p = new FGEPoint2D(0.5, 0.5);
            for (int turn = 0; turn < 4; turn++)
            {
                t.SetQuarterTurns(turn);
                for (int h = 0; h < 2; h++)
                {
                    for (int v = 0; v < 2; v++)
                    {
                        if (h == 1) t.FlipHorizontal();
                        if (v == 1) t.FlipVertical();
                        FGESize2D size = t.GetDisplaySize(4, 3);
                        Ensure(size.Width == (turn % 2 == 0 ? 4 : 3) &&
                            size.Height == (turn % 2 == 0 ? 3 : 4), "旋转图像尺寸");
                        foreach (FGEPoint2D source in new[] {
                            p, new FGEPoint2D(3.5,2.5),
                            new FGEPoint2D(0,0), new FGEPoint2D(4,3) })
                        {
                            FGEPoint2D rendered = t.SourceToDisplay(source, 4, 3);
                            FGEPoint2D roundtrip = t.DisplayToSource(rendered, 4, 3);
                            Ensure(Math.Abs(source.X - roundtrip.X) < 1e-9 &&
                                Math.Abs(source.Y - roundtrip.Y) < 1e-9, "坐标正逆可逆");
                        }
                        if (v == 1) t.FlipVertical();
                        if (h == 1) t.FlipHorizontal();
                    }
                }
            }
            t.SetQuarterTurns(1);
            FGEPoint2D rotated = t.SourceToDisplay(p, 4, 3);
            Ensure(rotated.X == 2.5 && rotated.Y == 0.5, "90度图像像素中心映射");
            t.Reset();
            Ensure(t.IsIdentity && changes > 0, "通知与复位");
            t.SetQuarterTurns(-1);
            Ensure(t.QuarterTurns == 3 && t.RotationDegrees == 270, "负旋转归一化");
            Console.WriteLine("PASS MVI ImageTransform 4x rotations/mirrors/roundtrip");
        }
        private static void Ensure(bool ok, string description)
        { if (!ok) throw new InvalidOperationException("MVI图像变换：" + description); }
    }
}
