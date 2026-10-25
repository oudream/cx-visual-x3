using System;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>像素坐标闭区间，Right/Bottom为最后一个像素；区别于连续几何矩形。</summary>
    public struct FIMPixelRegion
    {
        public FIMPixelRegion(int left, int top, int right, int bottom)
        {
            if (left < 0 || top < 0 || right < left || bottom < top)
                throw new ArgumentOutOfRangeException("left", "像素区域无效。");
            int checkedWidth = checked(right - left + 1);
            int checkedHeight = checked(bottom - top + 1);
            Left = left; Top = top; Right = right; Bottom = bottom;
        }

        public int Left { get; private set; }
        public int Top { get; private set; }
        public int Right { get; private set; }
        public int Bottom { get; private set; }
        public int Width { get { return checked(Right - Left + 1); } }
        public int Height { get { return checked(Bottom - Top + 1); } }

        public static FIMPixelRegion Entire(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            return new FIMPixelRegion(0, 0, image.Width - 1, image.Height - 1);
        }

        public FIMPixelRegion ClipTo(FIMImageBuffer image)
        {
            if (image == null) throw new ArgumentNullException("image");
            int right = Math.Min(Right, image.Width - 1);
            int bottom = Math.Min(Bottom, image.Height - 1);
            if (right < Left || bottom < Top)
                throw new ArgumentOutOfRangeException("image", "ROI与图像不相交。");
            return new FIMPixelRegion(Left, Top, right, bottom);
        }
    }
}
