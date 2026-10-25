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
    public sealed class MVIImageOverlayItem
    {
        private readonly FGEPoint2D[] _points;
        private readonly IReadOnlyList<FGEPoint2D> _readonlyPoints;

        public MVIImageOverlayItem(MVIImageOverlayKind kind, MVIImageOverlayLayer layer,
            FGERect2D bounds, IEnumerable<FGEPoint2D> points = null,
            string text = null, string externalId = null,
            int argb = unchecked((int)0xFFFF4040), double lineWidth = 1.5,
            Guid? id = null, bool isVisible = true)
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
            Argb = argb; LineWidth = lineWidth; IsVisible = isVisible;
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

        /// <summary>按原图坐标缩放边界与顶点，不能只改Bounds而令测量线/多边形顶点停在原位。</summary>
        public MVIImageOverlayItem WithBounds(FGERect2D bounds)
        {
            double scaleX = Bounds.Width <= 0 ? 1 : bounds.Width / Bounds.Width;
            double scaleY = Bounds.Height <= 0 ? 1 : bounds.Height / Bounds.Height;
            FGEPoint2D[] points = _points.Select(p => new FGEPoint2D(
                bounds.X + (p.X - Bounds.X) * scaleX,
                bounds.Y + (p.Y - Bounds.Y) * scaleY)).ToArray();
            return new MVIImageOverlayItem(Kind, Layer, bounds, points, Text, ExternalId,
                Argb, LineWidth, Id, IsVisible);
        }
        public MVIImageOverlayItem WithVisibility(bool visible)
        {
            return new MVIImageOverlayItem(Kind, Layer, Bounds, _points, Text, ExternalId,
                Argb, LineWidth, Id, visible);
        }
        public MVIImageOverlayItem WithText(string text)
        {
            return new MVIImageOverlayItem(Kind, Layer, Bounds, _points, text, ExternalId,
                Argb, LineWidth, Id, IsVisible);
        }
        public MVIImageOverlayItem WithStyle(int argb, double lineWidth)
        {
            return new MVIImageOverlayItem(Kind, Layer, Bounds, _points, Text, ExternalId,
                argb, lineWidth, Id, IsVisible);
        }
        public MVIImageOverlayItem Translate(double dx, double dy)
        {
            FGEValidation.Finite(dx, "dx"); FGEValidation.Finite(dy, "dy");
            FGERect2D rect = new FGERect2D(Bounds.X + dx, Bounds.Y + dy, Bounds.Width, Bounds.Height);
            FGEPoint2D[] translated = _points.Select(p => new FGEPoint2D(p.X + dx, p.Y + dy)).ToArray();
            return new MVIImageOverlayItem(Kind, Layer, rect, translated, Text, ExternalId,
                Argb, LineWidth, Id, IsVisible);
        }
    }
}
