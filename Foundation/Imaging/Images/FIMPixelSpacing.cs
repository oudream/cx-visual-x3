using System;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>两个方向的像素物理间距（mm/pixel）；不存在标定时请使用nullable。</summary>
    public struct FIMPixelSpacing
    {
        public FIMPixelSpacing(double millimetersPerPixelX, double millimetersPerPixelY)
        {
            if (double.IsNaN(millimetersPerPixelX) || double.IsInfinity(millimetersPerPixelX) ||
                millimetersPerPixelX <= 0)
                throw new ArgumentOutOfRangeException("millimetersPerPixelX");
            if (double.IsNaN(millimetersPerPixelY) || double.IsInfinity(millimetersPerPixelY) ||
                millimetersPerPixelY <= 0)
                throw new ArgumentOutOfRangeException("millimetersPerPixelY");
            MillimetersPerPixelX = millimetersPerPixelX;
            MillimetersPerPixelY = millimetersPerPixelY;
        }

        public double MillimetersPerPixelX { get; private set; }
        public double MillimetersPerPixelY { get; private set; }
    }
}
