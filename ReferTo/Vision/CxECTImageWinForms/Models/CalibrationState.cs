using System;

namespace CxECTImageWinForms.Models
{
    /// <summary>
    /// 像素间距以毫米为单位；未标定时物理尺寸返回空值而非伪造数值。
    /// </summary>
    public sealed class CalibrationState
    {
        public double? MillimetersPerPixelX { get; private set; }

        public double? MillimetersPerPixelY { get; private set; }

        public bool IsCalibrated
        {
            get { return MillimetersPerPixelX.HasValue && MillimetersPerPixelY.HasValue; }
        }

        public void SetPixelSpacing(double millimetersPerPixelX, double millimetersPerPixelY)
        {
            if (millimetersPerPixelX <= 0 || millimetersPerPixelY <= 0)
            {
                throw new ArgumentOutOfRangeException("millimetersPerPixelX", "像素间距必须大于零。");
            }

            MillimetersPerPixelX = millimetersPerPixelX;
            MillimetersPerPixelY = millimetersPerPixelY;
        }

        public void Clear()
        {
            MillimetersPerPixelX = null;
            MillimetersPerPixelY = null;
        }

        public double? PixelToWorld(double pixelX, double pixelY)
        {
            if (!IsCalibrated)
            {
                return null;
            }

            return Math.Sqrt(
                pixelX * pixelX * MillimetersPerPixelX.Value * MillimetersPerPixelX.Value +
                pixelY * pixelY * MillimetersPerPixelY.Value * MillimetersPerPixelY.Value);
        }
    }
}
