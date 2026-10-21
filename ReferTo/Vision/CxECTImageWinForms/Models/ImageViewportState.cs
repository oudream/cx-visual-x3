using System;

namespace CxECTImageWinForms.Models
{
    /// <summary>
    /// 视口状态：缩放和平移只影响当前观看位置。
    /// </summary>
    public sealed class ImageViewportState
    {
        public const double DefaultMinZoom = 0.05;
        public const double DefaultMaxZoom = 64.0;

        public ImageViewportState()
        {
            MinZoom = DefaultMinZoom;
            MaxZoom = DefaultMaxZoom;
            Reset();
        }

        public double Zoom { get; private set; }

        public double MinZoom { get; private set; }

        public double MaxZoom { get; private set; }

        public ImagePoint CenterInDisplayCoordinates { get; private set; }

        public bool IsFitToWindow { get; private set; }

        public void SetZoomRange(double minZoom, double maxZoom)
        {
            if (minZoom <= 0 || maxZoom < minZoom)
            {
                throw new ArgumentOutOfRangeException("minZoom", "缩放范围无效。");
            }

            MinZoom = minZoom;
            MaxZoom = maxZoom;
            Zoom = Clamp(Zoom);
        }

        public void SetZoom(double zoom)
        {
            Zoom = Clamp(zoom);
            IsFitToWindow = false;
        }

        public void SetFit(double fitZoom, ImagePoint center)
        {
            Zoom = Clamp(fitZoom);
            CenterInDisplayCoordinates = center;
            IsFitToWindow = true;
        }

        public void Pan(double deltaX, double deltaY)
        {
            CenterInDisplayCoordinates = new ImagePoint(
                CenterInDisplayCoordinates.X + deltaX,
                CenterInDisplayCoordinates.Y + deltaY);
            IsFitToWindow = false;
        }

        public void ResetPan()
        {
            CenterInDisplayCoordinates = new ImagePoint(0, 0);
        }

        public void Reset()
        {
            // 总复位才恢复倍率；ResetPan 仅恢复位置，避免覆盖用户当前缩放。
            Zoom = 1.0;
            CenterInDisplayCoordinates = new ImagePoint(0, 0);
            IsFitToWindow = false;
        }

        private double Clamp(double zoom)
        {
            return Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
        }
    }
}
