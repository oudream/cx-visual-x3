using System;
using CxECTImageWinForms.Models;

namespace CxECTImageWinForms.Services
{
    /// <summary>
    /// 所有测量从原图坐标计算，显示旋转或缩放不会改变数值。
    /// </summary>
    public sealed class ImageMeasurementService
    {
        public double GetDistancePixels(ImagePoint start, ImagePoint end)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        public double? GetDistanceMillimeters(ImagePoint start, ImagePoint end, CalibrationState calibration)
        {
            if (calibration == null)
            {
                throw new ArgumentNullException("calibration");
            }

            return calibration.PixelToWorld(end.X - start.X, end.Y - start.Y);
        }

        public double GetAngleDegrees(ImagePoint first, ImagePoint vertex, ImagePoint third)
        {
            var firstX = first.X - vertex.X;
            var firstY = first.Y - vertex.Y;
            var secondX = third.X - vertex.X;
            var secondY = third.Y - vertex.Y;
            var firstLength = Math.Sqrt(firstX * firstX + firstY * firstY);
            var secondLength = Math.Sqrt(secondX * secondX + secondY * secondY);
            if (firstLength == 0 || secondLength == 0)
            {
                throw new ArgumentException("角度测量的两条边不能为零长度。");
            }

            var cosine = (firstX * secondX + firstY * secondY) / (firstLength * secondLength);
            cosine = Math.Max(-1, Math.Min(1, cosine));
            return Math.Acos(cosine) * 180.0 / Math.PI;
        }

        public double GetAreaPixels(ImageRectangle rectangle)
        {
            return rectangle.Width * rectangle.Height;
        }

        public double? GetAreaSquareMillimeters(ImageRectangle rectangle, CalibrationState calibration)
        {
            if (calibration == null)
            {
                throw new ArgumentNullException("calibration");
            }

            if (!calibration.IsCalibrated)
            {
                return null;
            }

            return GetAreaPixels(rectangle) * calibration.MillimetersPerPixelX.Value * calibration.MillimetersPerPixelY.Value;
        }
    }
}
