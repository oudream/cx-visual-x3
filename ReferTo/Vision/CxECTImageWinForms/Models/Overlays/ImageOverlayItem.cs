using System;
using System.Collections.Generic;

namespace CxECTImageWinForms.Models.Overlays
{
    public enum ImageOverlayLayer
    {
        Roi,
        Measurement,
        Annotation,
        Defect
    }

    public enum ImageOverlayKind
    {
        RectangleRoi,
        CircleRoi,
        EllipseRoi,
        PolygonRoi,
        DistanceMeasurement,
        HorizontalMeasurement,
        VerticalMeasurement,
        AngleMeasurement,
        DiameterMeasurement,
        AreaMeasurement,
        LineProfileMeasurement,
        PointAnnotation,
        RectangleAnnotation,
        PolygonAnnotation,
        TextAnnotation,
        ArrowAnnotation,
        DefectBox
    }

    /// <summary>
    /// 可持久化的叠加项。Points、Bounds 与半径始终是原图坐标。
    /// </summary>
    public sealed class ImageOverlayItem
    {
        public ImageOverlayItem(ImageOverlayKind kind, ImageOverlayLayer layer)
        {
            Id = Guid.NewGuid();
            Kind = kind;
            Layer = layer;
            IsVisible = true;
            Points = new List<ImagePoint>();
            Style = new ImageOverlayStyle();
        }

        public Guid Id { get; private set; }

        public ImageOverlayKind Kind { get; private set; }

        public ImageOverlayLayer Layer { get; private set; }

        public bool IsVisible { get; set; }

        public string Text { get; set; }

        public string ExternalId { get; set; }

        public ImageRectangle Bounds { get; set; }

        public double RadiusX { get; set; }

        public double RadiusY { get; set; }

        public IList<ImagePoint> Points { get; private set; }

        public ImageOverlayStyle Style { get; private set; }
    }

    public sealed class ImageOverlayStyle
    {
        public ImageOverlayStyle()
        {
            Argb = unchecked((int)0xFFFF4040);
            LineWidth = 1.5;
        }

        public int Argb { get; set; }

        public double LineWidth { get; set; }
    }
}
