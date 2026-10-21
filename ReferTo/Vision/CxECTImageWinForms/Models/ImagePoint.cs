using System;

namespace CxECTImageWinForms.Models
{
    /// <summary>
    /// 图像坐标点。所有业务几何均以原图像素坐标保存。
    /// </summary>
    public struct ImagePoint
    {
        public ImagePoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; private set; }

        public double Y { get; private set; }
    }
}
