using System;

namespace CxVisual.Foundation.Volume
{
    /// <summary>
    /// 任意切平面的纯数据定义。中心、尺寸和步长均以原始体素坐标为单位，
    /// 使 3D 平面与 RAW 高精度采样使用同一套坐标语义。
    /// </summary>
    public enum FVOObliqueInterpolation
    {
        Nearest=0,
        Linear=1
    }

    public sealed class FVOObliqueSliceDefinition
    {
        public bool Visible { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double CenterZ { get; set; }
        public double AngleXDegrees { get; set; }
        public double AngleYDegrees { get; set; }
        public double AngleZDegrees { get; set; }
        public FVOObliqueInterpolation Interpolation { get; set; } = FVOObliqueInterpolation.Linear;
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 512;
        public double PixelSpacingInVoxels { get; set; } = 1.0;

        public FVOObliqueSliceDefinition Clone()
        {
            return new FVOObliqueSliceDefinition
            {
                Visible = Visible,
                CenterX = CenterX,
                CenterY = CenterY,
                CenterZ = CenterZ,
                AngleXDegrees = AngleXDegrees,
                AngleYDegrees = AngleYDegrees,
                AngleZDegrees = AngleZDegrees,
                Interpolation = Interpolation,
                Width = Width,
                Height = Height,
                PixelSpacingInVoxels = PixelSpacingInVoxels
            };
        }
    }

    /// <summary>任意切采样与 3D 平面共用的旋转和角点计算。</summary>
    public static class FVOObliqueSliceGeometry
    {
        public static void GetAxes(FVOObliqueSliceDefinition definition,
                                   out FVOObliqueSliceVector axisU,
                                   out FVOObliqueSliceVector axisV)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            axisU = Rotate(new FVOObliqueSliceVector(1.0, 0.0, 0.0), definition);
            axisV = Rotate(new FVOObliqueSliceVector(0.0, 1.0, 0.0), definition);
        }

        public static FVOObliqueSliceVector GetCorner(FVOObliqueSliceDefinition definition,
                                                    FVOObliqueSliceVector axisU,
                                                    FVOObliqueSliceVector axisV,
                                                    double uSign,
                                                    double vSign)
        {
            double halfWidth = (Math.Max(1, definition.Width) - 1) * definition.PixelSpacingInVoxels * 0.5;
            double halfHeight = (Math.Max(1, definition.Height) - 1) * definition.PixelSpacingInVoxels * 0.5;
            return new FVOObliqueSliceVector(
                definition.CenterX + axisU.X * halfWidth * uSign + axisV.X * halfHeight * vSign,
                definition.CenterY + axisU.Y * halfWidth * uSign + axisV.Y * halfHeight * vSign,
                definition.CenterZ + axisU.Z * halfWidth * uSign + axisV.Z * halfHeight * vSign);
        }

        private static FVOObliqueSliceVector Rotate(FVOObliqueSliceVector vector, FVOObliqueSliceDefinition definition)
        {
            FVOObliqueSliceVector rotated = RotateX(vector, ToRadians(definition.AngleXDegrees));
            rotated = RotateY(rotated, ToRadians(definition.AngleYDegrees));
            return RotateZ(rotated, ToRadians(definition.AngleZDegrees));
        }

        private static FVOObliqueSliceVector RotateX(FVOObliqueSliceVector vector, double radians)
        {
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new FVOObliqueSliceVector(vector.X, vector.Y * cos - vector.Z * sin, vector.Y * sin + vector.Z * cos);
        }

        private static FVOObliqueSliceVector RotateY(FVOObliqueSliceVector vector, double radians)
        {
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new FVOObliqueSliceVector(vector.X * cos + vector.Z * sin, vector.Y, -vector.X * sin + vector.Z * cos);
        }

        private static FVOObliqueSliceVector RotateZ(FVOObliqueSliceVector vector, double radians)
        {
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            return new FVOObliqueSliceVector(vector.X * cos - vector.Y * sin, vector.X * sin + vector.Y * cos, vector.Z);
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }
    }

    public struct FVOObliqueSliceVector
    {
        public FVOObliqueSliceVector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
    }
}
