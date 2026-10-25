using System;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>纯数学视口。图像像素坐标到WPF DIP，缩放以鼠标锚点保持不动。</summary>
    public sealed class MVIImageViewport
    {
        private double _scale = 1, _offsetX, _offsetY;
        public double Scale { get { return _scale; } }
        public double OffsetX { get { return _offsetX; } }
        public double OffsetY { get { return _offsetY; } }
        public bool IsFit { get; private set; }
        public void Fit(int imageWidth, int imageHeight, double viewWidth, double viewHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0 || viewWidth <= 0 || viewHeight <= 0)
                throw new ArgumentOutOfRangeException("imageWidth");
            _scale = Math.Min(viewWidth / imageWidth, viewHeight / imageHeight);
            _offsetX = (viewWidth - imageWidth * _scale) / 2;
            _offsetY = (viewHeight - imageHeight * _scale) / 2;
            IsFit = true;
        }
        public void ActualSize(int imageWidth, int imageHeight, double viewWidth, double viewHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0 || viewWidth <= 0 || viewHeight <= 0)
                throw new ArgumentOutOfRangeException("imageWidth");
            _scale = 1;
            _offsetX = (viewWidth - imageWidth) / 2;
            _offsetY = (viewHeight - imageHeight) / 2;
            IsFit = false;
        }
        public void Pan(double dx, double dy)
        {
            if (double.IsNaN(dx) || double.IsInfinity(dx) || double.IsNaN(dy) || double.IsInfinity(dy))
                throw new ArgumentOutOfRangeException("dx");
            _offsetX += dx; _offsetY += dy;
            IsFit = false;
        }
        public void ZoomAt(double factor, double anchorX, double anchorY)
        {
            if (double.IsNaN(factor) || double.IsInfinity(factor) || factor <= 0)
                throw new ArgumentOutOfRangeException("factor");
            double next = Math.Max(0.015625, Math.Min(64, _scale * factor));
            _offsetX = anchorX - (anchorX - _offsetX) * next / _scale;
            _offsetY = anchorY - (anchorY - _offsetY) * next / _scale;
            _scale = next;
            IsFit = false;
        }
        public double ImageX(double displayX) { return (displayX - _offsetX) / _scale; }
        public double ImageY(double displayY) { return (displayY - _offsetY) / _scale; }
        public double DisplayX(double imageX) { return imageX * _scale + _offsetX; }
        public double DisplayY(double imageY) { return imageY * _scale + _offsetY; }
    }
}
