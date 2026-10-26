using System;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>
    /// 成熟ImageTransformState的原图↔显示图正逆变换，按图像连续像素边界坐标[0,w]计算。
    /// 当前只支持0/90/180/270与双轴镜像，任意旋转角明确留待插值与ROI投影验收。
    /// WPF、统计和几何测量统一使用此同一个变换，不将浏览旋转写回源像素。
    /// </summary>
    public sealed class MVIImageTransformState
    {
        private int _quarterTurns;
        private bool _flipX, _flipY;
        public event EventHandler Changed;
        public int QuarterTurns { get { return _quarterTurns; } }
        public double RotationDegrees { get { return _quarterTurns * 90.0; } }
        public bool IsFlippedHorizontally { get { return _flipX; } }
        public bool IsFlippedVertically { get { return _flipY; } }
        public bool IsIdentity { get { return _quarterTurns == 0 && !_flipX && !_flipY; } }
        public void Rotate90Clockwise() { SetQuarterTurns(_quarterTurns + 1); }
        public void Rotate90Counterclockwise() { SetQuarterTurns(_quarterTurns + 3); }
        public void SetQuarterTurns(int turns)
        {
            int normalized = ((turns % 4) + 4) % 4;
            if (normalized == _quarterTurns) return;
            _quarterTurns = normalized; Notify();
        }
        public void FlipHorizontal()
        {
            _flipX = !_flipX; Notify();
        }
        public void FlipVertical()
        {
            _flipY = !_flipY; Notify();
        }
        public void Reset()
        {
            if (IsIdentity) return;
            _quarterTurns = 0; _flipX = _flipY = false; Notify();
        }
        public FGESize2D GetDisplaySize(int width, int height)
        {
            Validate(width, height);
            return _quarterTurns % 2 == 0 ?
                new FGESize2D(width, height) : new FGESize2D(height, width);
        }
        public FGEPoint2D SourceToDisplay(FGEPoint2D source, int width, int height)
        {
            Validate(width, height);
            double x, y;
            switch (_quarterTurns)
            {
                case 1: x = height - source.Y; y = source.X; break;
                case 2: x = width - source.X; y = height - source.Y; break;
                case 3: x = source.Y; y = width - source.X; break;
                default: x = source.X; y = source.Y; break;
            }
            FGESize2D size = GetDisplaySize(width, height);
            return new FGEPoint2D(_flipX ? size.Width - x : x,
                _flipY ? size.Height - y : y);
        }
        public FGEPoint2D DisplayToSource(FGEPoint2D point, int width, int height)
        {
            Validate(width, height);
            FGESize2D size = GetDisplaySize(width, height);
            double x = _flipX ? size.Width - point.X : point.X;
            double y = _flipY ? size.Height - point.Y : point.Y;
            switch (_quarterTurns)
            {
                case 1: return new FGEPoint2D(y, height - x);
                case 2: return new FGEPoint2D(width - x, height - y);
                case 3: return new FGEPoint2D(width - y, x);
                default: return new FGEPoint2D(x, y);
            }
        }
        private static void Validate(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("width");
        }
        private void Notify()
        {
            EventHandler h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
