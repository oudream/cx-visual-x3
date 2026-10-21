using System;

namespace CxECTImageWinForms.Models
{
    /// <summary>
    /// 原图坐标系中的矩形区域，构造时统一规范化，供 ROI 与统计共用。
    /// Right/Bottom 是包含的最后一个像素坐标，因此 Width/Height 包含两个边界像素。
    /// </summary>
    public struct ImageRectangle
    {
        public ImageRectangle(double firstX, double firstY, double secondX, double secondY)
        {
            Left = Math.Min(firstX, secondX);
            Top = Math.Min(firstY, secondY);
            Right = Math.Max(firstX, secondX);
            Bottom = Math.Max(firstY, secondY);
        }

        public double Left { get; private set; }

        public double Top { get; private set; }

        public double Right { get; private set; }

        public double Bottom { get; private set; }

        public double Width { get { return Right - Left + 1.0; } }

        public double Height { get { return Bottom - Top + 1.0; } }

        public ImageRectangle Offset(double deltaX, double deltaY)
        {
            return new ImageRectangle(Left + deltaX, Top + deltaY, Right + deltaX, Bottom + deltaY);
        }

        public ImageRectangle Intersect(ImageRectangle other)
        {
            ImageRectangle intersection;
            if (!TryIntersect(other, out intersection))
            {
                throw new ArgumentException("两个矩形没有交集。", "other");
            }

            return intersection;
        }

        public bool TryIntersect(ImageRectangle other, out ImageRectangle intersection)
        {
            var left = Math.Max(Left, other.Left);
            var top = Math.Max(Top, other.Top);
            var right = Math.Min(Right, other.Right);
            var bottom = Math.Min(Bottom, other.Bottom);
            if (right < left || bottom < top)
            {
                intersection = new ImageRectangle();
                return false;
            }

            intersection = new ImageRectangle(left, top, right, bottom);
            return true;
        }
    }
}
