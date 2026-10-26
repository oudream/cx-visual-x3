using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>
    /// 原图坐标系中的 ROI、标注或测量覆盖项。与旧ImageOverlayItem一致保持Kind/Layer/
    /// Bounds/Points/Style等语义，但使用现有FGE几何并以不可变快照更新，避免异步渲染脏状态。
    /// 属于图像会话覆盖数据，不是另一个MEM工程/Undo真源。
    /// </summary>
    /// <summary>ROI八方向编辑柄，仅描述原图边界几何，不依赖WPF或任何厂商SDK。</summary>
    public enum MVIImageResizeHandle
    {
        TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left
    }

    public sealed class MVIImageOverlayItem
    {
        private readonly FGEPoint2D[] _points;
        private readonly IReadOnlyList<FGEPoint2D> _readonlyPoints;

        public MVIImageOverlayItem(MVIImageOverlayKind kind, MVIImageOverlayLayer layer,
            FGERect2D bounds, IEnumerable<FGEPoint2D> points = null,
            string text = null, string externalId = null,
            int argb = unchecked((int)0xFFFF4040), double lineWidth = 1.5,
            Guid? id = null, bool isVisible = true, bool isLocked = false)
        {
            if (!Enum.IsDefined(typeof(MVIImageOverlayKind), kind))
                throw new ArgumentOutOfRangeException("kind");
            if (!Enum.IsDefined(typeof(MVIImageOverlayLayer), layer))
                throw new ArgumentOutOfRangeException("layer");
            if (double.IsNaN(lineWidth) || double.IsInfinity(lineWidth) || lineWidth <= 0 || lineWidth > 100)
                throw new ArgumentOutOfRangeException("lineWidth");
            _points = points == null ? new FGEPoint2D[0] : points.ToArray();
            if (_points.Length > 100000) throw new ArgumentException("叠加几何点数超限。", "points");
            if ((kind == MVIImageOverlayKind.PolygonRoi || kind == MVIImageOverlayKind.PolygonAnnotation)
                && _points.Length < 3)
                throw new ArgumentException("多边形至少需要三个点。", "points");
            if ((kind == MVIImageOverlayKind.DistanceMeasurement ||
                 kind == MVIImageOverlayKind.HorizontalMeasurement ||
                 kind == MVIImageOverlayKind.VerticalMeasurement ||
                 kind == MVIImageOverlayKind.LineProfileMeasurement ||
                 kind == MVIImageOverlayKind.ArrowAnnotation) && _points.Length < 2)
                throw new ArgumentException("线段测量或箭头至少需要两个点。", "points");
            if (kind == MVIImageOverlayKind.AngleMeasurement && _points.Length < 3)
                throw new ArgumentException("角度测量至少需要三个点。", "points");
            Id = id ?? Guid.NewGuid();
            if (Id == Guid.Empty) throw new ArgumentException("叠加项ID不能为Empty。", "id");
            Kind = kind; Layer = layer; Bounds = bounds;
            _readonlyPoints = Array.AsReadOnly(_points);
            Text = text ?? string.Empty; ExternalId = externalId ?? string.Empty;
            Argb = argb; LineWidth = lineWidth; IsVisible = isVisible; IsLocked = isLocked;
        }

        public Guid Id { get; private set; }
        public MVIImageOverlayKind Kind { get; private set; }
        public MVIImageOverlayLayer Layer { get; private set; }
        /// <summary>连续空间外框，和FIMPixelRegion闭区间像素统计ROI必须显式转换。</summary>
        public FGERect2D Bounds { get; private set; }
        public IReadOnlyList<FGEPoint2D> Points { get { return _readonlyPoints; } }
        public string Text { get; private set; }
        public string ExternalId { get; private set; }
        public int Argb { get; private set; }
        public double LineWidth { get; private set; }
        public bool IsVisible { get; private set; }
        public bool IsLocked { get; private set; }

        /// <summary>按原图坐标缩放边界与顶点，不能只改Bounds而令测量线/多边形顶点停在原位。</summary>
        public MVIImageOverlayItem WithBounds(FGERect2D bounds)
        {
            if (IsLocked) throw new InvalidOperationException("ROI已锁定，禁止缩放。");
            double scaleX = Bounds.Width <= 0 ? 1 : bounds.Width / Bounds.Width;
            double scaleY = Bounds.Height <= 0 ? 1 : bounds.Height / Bounds.Height;
            FGEPoint2D[] points = _points.Select(p => new FGEPoint2D(
                bounds.X + (p.X - Bounds.X) * scaleX,
                bounds.Y + (p.Y - Bounds.Y) * scaleY)).ToArray();
            return new MVIImageOverlayItem(Kind, Layer, bounds, points, Text, ExternalId,
                Argb, LineWidth, Id, IsVisible, IsLocked);
        }
        /// <summary>
        /// 基于拖动开始时的不可变快照编辑ROI边界，保持Guid、图层、样式和多边形顶点对应。
        /// 锚定对侧边/角，不允许倒置或超出原图；最小尺寸为1/4原始像素。
        /// </summary>
        public MVIImageOverlayItem WithResizedRoiHandle(MVIImageResizeHandle handle,
            FGEPoint2D position, int imageWidth, int imageHeight)
        {
            if (IsLocked) throw new InvalidOperationException("ROI已锁定，不能拖动编辑柄。");
            if (Layer != MVIImageOverlayLayer.Roi ||
                (Kind != MVIImageOverlayKind.RectangleRoi &&
                 Kind != MVIImageOverlayKind.CircleRoi &&
                 Kind != MVIImageOverlayKind.EllipseRoi &&
                 Kind != MVIImageOverlayKind.PolygonRoi))
                throw new InvalidOperationException("仅几何ROI支持当前八方向缩放；标注及测量不应被悄悄修改。");
            if (!Enum.IsDefined(typeof(MVIImageResizeHandle), handle))
                throw new ArgumentOutOfRangeException("handle");
            if (imageWidth <= 0 || imageHeight <= 0)
                throw new ArgumentOutOfRangeException("imageWidth");
            if (Bounds.Width <= 0 || Bounds.Height <= 0 || Bounds.Left < 0 || Bounds.Top < 0 ||
                Bounds.Right > imageWidth || Bounds.Bottom > imageHeight)
                throw new InvalidOperationException("原ROI边界不合法，不能编辑。");
            double x = Math.Max(0, Math.Min(imageWidth,FGEValidation.Finite(position.X,"position")));
            double y = Math.Max(0, Math.Min(imageHeight,FGEValidation.Finite(position.Y,"position")));
            double left = Bounds.Left, right = Bounds.Right;
            double top = Bounds.Top, bottom = Bounds.Bottom;
            const double minimum = 0.25;
            switch (handle)
            {
                case MVIImageResizeHandle.TopLeft:
                case MVIImageResizeHandle.Left:
                case MVIImageResizeHandle.BottomLeft:
                    left = Math.Min(right - minimum,x); break;
                case MVIImageResizeHandle.TopRight:
                case MVIImageResizeHandle.Right:
                case MVIImageResizeHandle.BottomRight:
                    right = Math.Max(left + minimum,x); break;
            }
            switch (handle)
            {
                case MVIImageResizeHandle.TopLeft:
                case MVIImageResizeHandle.Top:
                case MVIImageResizeHandle.TopRight:
                    top = Math.Min(bottom - minimum,y); break;
                case MVIImageResizeHandle.BottomLeft:
                case MVIImageResizeHandle.Bottom:
                case MVIImageResizeHandle.BottomRight:
                    bottom = Math.Max(top + minimum,y); break;
            }
            return WithBounds(new FGERect2D(left,top,right-left,bottom-top));
        }

        public MVIImageOverlayItem WithVisibility(bool visible)
        {
            return new MVIImageOverlayItem(Kind, Layer, Bounds, _points, Text, ExternalId,
                Argb, LineWidth, Id, visible);
        }
        public MVIImageOverlayItem WithLocked(bool locked)
        {
            return new MVIImageOverlayItem(Kind,Layer,Bounds,_points,Text,ExternalId,
                Argb,LineWidth,Id,IsVisible,locked);
        }
        public MVIImageOverlayItem WithText(string text)
        {
            if (IsLocked) throw new InvalidOperationException("叠加对象已锁定，不能修改文字。");
            return new MVIImageOverlayItem(Kind, Layer, Bounds, _points, text, ExternalId,
                Argb, LineWidth, Id, IsVisible, IsLocked);
        }
        public MVIImageOverlayItem WithStyle(int argb, double lineWidth)
        {
            if (IsLocked) throw new InvalidOperationException("叠加对象已锁定，不能修改样式。");
            return new MVIImageOverlayItem(Kind, Layer, Bounds, _points, Text, ExternalId,
                argb, lineWidth, Id, IsVisible);
        }
        public MVIImageOverlayItem Translate(double dx, double dy)
        {
            if (IsLocked) throw new InvalidOperationException("叠加对象已锁定，不能移动。");
            FGEValidation.Finite(dx, "dx"); FGEValidation.Finite(dy, "dy");
            FGERect2D rect = new FGERect2D(Bounds.X + dx, Bounds.Y + dy, Bounds.Width, Bounds.Height);
            FGEPoint2D[] translated = _points.Select(p => new FGEPoint2D(p.X + dx, p.Y + dy)).ToArray();
            return new MVIImageOverlayItem(Kind, Layer, rect, translated, Text, ExternalId,
                Argb, LineWidth, Id, IsVisible, IsLocked);
        }
    }
}
