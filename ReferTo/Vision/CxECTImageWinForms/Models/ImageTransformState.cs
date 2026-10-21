using System;

namespace CxECTImageWinForms.Models
{
    /// <summary>
    /// 只描述原图到显示图的几何关系，不直接修改原始图像数据。
    /// </summary>
    public sealed class ImageTransformState
    {
        public double RotationDegrees { get; private set; }

        public bool IsFlippedHorizontally { get; private set; }

        public bool IsFlippedVertically { get; private set; }

        public bool IsIdentity
        {
            get
            {
                return Math.Abs(RotationDegrees) < 0.000001 &&
                       !IsFlippedHorizontally &&
                       !IsFlippedVertically;
            }
        }

        /// <summary>
        /// 仅 0/90/180/270 度旋转会保持矩形边与图像坐标轴平行。
        /// 任意角度下矩形选择会变成四边形，不能静默降级为错误的原图矩形 ROI。
        /// </summary>
        public bool PreservesAxisAlignedGeometry
        {
            get
            {
                var remainder = RotationDegrees % 90.0;
                return Math.Abs(remainder) < 0.000001 || Math.Abs(remainder - 90.0) < 0.000001;
            }
        }

        public void Rotate90Clockwise()
        {
            SetRotation(RotationDegrees + 90.0);
        }

        /// <summary>
        /// 逆时针旋转保持与顺时针相同的角度归一化规则。
        /// </summary>
        public void Rotate90Counterclockwise()
        {
            SetRotation(RotationDegrees - 90.0);
        }

        public void SetRotation(double degrees)
        {
            if (double.IsNaN(degrees) || double.IsInfinity(degrees))
            {
                throw new ArgumentOutOfRangeException("degrees", "旋转角度必须是有限数值。");
            }

            RotationDegrees = NormalizeDegrees(degrees);
        }

        public void FlipHorizontal()
        {
            IsFlippedHorizontally = !IsFlippedHorizontally;
        }

        public void FlipVertical()
        {
            IsFlippedVertically = !IsFlippedVertically;
        }

        public void Reset()
        {
            RotationDegrees = 0;
            IsFlippedHorizontally = false;
            IsFlippedVertically = false;
        }

        public ImagePoint SourceToDisplay(ImagePoint sourcePoint, int imageWidth, int imageHeight)
        {
            ValidateImageSize(imageWidth, imageHeight);
            // 渲染链路先 RotateImage，再 MirrorImage；坐标链路必须严格同序。
            var rotated = RotateAroundCenter(sourcePoint, RotationDegrees, imageWidth, imageHeight);
            var bounds = GetRotatedBounds(imageWidth, imageHeight);
            return ApplyFlip(
                new ImagePoint(rotated.X - bounds.MinX, rotated.Y - bounds.MinY),
                (int)Math.Ceiling(bounds.MaxX - bounds.MinX + 1.0),
                (int)Math.Ceiling(bounds.MaxY - bounds.MinY + 1.0));
        }

        public ImagePoint DisplayToSource(ImagePoint displayPoint, int imageWidth, int imageHeight)
        {
            ValidateImageSize(imageWidth, imageHeight);
            var bounds = GetRotatedBounds(imageWidth, imageHeight);
            var unflipped = ApplyFlip(
                displayPoint,
                (int)Math.Ceiling(bounds.MaxX - bounds.MinX + 1.0),
                (int)Math.Ceiling(bounds.MaxY - bounds.MinY + 1.0));
            var rotatedPoint = new ImagePoint(unflipped.X + bounds.MinX, unflipped.Y + bounds.MinY);
            return RotateAroundCenter(rotatedPoint, -RotationDegrees, imageWidth, imageHeight);
        }

        public ImageSize GetDisplaySize(int imageWidth, int imageHeight)
        {
            ValidateImageSize(imageWidth, imageHeight);
            var bounds = GetRotatedBounds(imageWidth, imageHeight);
            return new ImageSize(
                (int)Math.Ceiling(bounds.MaxX - bounds.MinX + 1.0),
                (int)Math.Ceiling(bounds.MaxY - bounds.MinY + 1.0));
        }

        private ImagePoint ApplyFlip(ImagePoint point, int imageWidth, int imageHeight)
        {
            var x = IsFlippedHorizontally ? imageWidth - 1 - point.X : point.X;
            var y = IsFlippedVertically ? imageHeight - 1 - point.Y : point.Y;
            return new ImagePoint(x, y);
        }

        private static ImagePoint RotateAroundCenter(ImagePoint point, double degrees, int imageWidth, int imageHeight)
        {
            var radians = degrees * Math.PI / 180.0;
            var cosine = Math.Cos(radians);
            var sine = Math.Sin(radians);
            var centerX = (imageWidth - 1) / 2.0;
            var centerY = (imageHeight - 1) / 2.0;
            var x = point.X - centerX;
            var y = point.Y - centerY;
            return new ImagePoint(
                x * cosine - y * sine + centerX,
                x * sine + y * cosine + centerY);
        }

        private RotatedBounds GetRotatedBounds(int imageWidth, int imageHeight)
        {
            var topLeft = RotateAroundCenter(new ImagePoint(0, 0), RotationDegrees, imageWidth, imageHeight);
            var topRight = RotateAroundCenter(new ImagePoint(imageWidth - 1, 0), RotationDegrees, imageWidth, imageHeight);
            var bottomLeft = RotateAroundCenter(new ImagePoint(0, imageHeight - 1), RotationDegrees, imageWidth, imageHeight);
            var bottomRight = RotateAroundCenter(new ImagePoint(imageWidth - 1, imageHeight - 1), RotationDegrees, imageWidth, imageHeight);
            return new RotatedBounds(topLeft, topRight, bottomLeft, bottomRight);
        }

        private static void ValidateImageSize(int imageWidth, int imageHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                throw new ArgumentOutOfRangeException("imageWidth", "图像尺寸必须大于零。");
            }
        }

        private static double NormalizeDegrees(double degrees)
        {
            var normalized = degrees % 360.0;
            normalized = normalized < 0 ? normalized + 360.0 : normalized;
            return Math.Abs(normalized) < 0.000001 || Math.Abs(normalized - 360.0) < 0.000001
                ? 0.0
                : normalized;
        }

        private struct RotatedBounds
        {
            public RotatedBounds(ImagePoint first, ImagePoint second, ImagePoint third, ImagePoint fourth)
            {
                MinX = Math.Min(Math.Min(first.X, second.X), Math.Min(third.X, fourth.X));
                MaxX = Math.Max(Math.Max(first.X, second.X), Math.Max(third.X, fourth.X));
                MinY = Math.Min(Math.Min(first.Y, second.Y), Math.Min(third.Y, fourth.Y));
                MaxY = Math.Max(Math.Max(first.Y, second.Y), Math.Max(third.Y, fourth.Y));
            }

            public double MinX { get; private set; }

            public double MaxX { get; private set; }

            public double MinY { get; private set; }

            public double MaxY { get; private set; }
        }
    }
}
