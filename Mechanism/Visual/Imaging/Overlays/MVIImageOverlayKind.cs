namespace CxVisual.Mechanism.Visual
{
    /// <summary>逐项承接 ReferTo/CxECTImageWinForms/Models/Overlays/ImageOverlayItem 的成熟分类。</summary>
    public enum MVIImageOverlayKind
    {
        RectangleRoi, CircleRoi, EllipseRoi, PolygonRoi,
        DistanceMeasurement, HorizontalMeasurement, VerticalMeasurement,
        AngleMeasurement, DiameterMeasurement, AreaMeasurement, LineProfileMeasurement,
        PointAnnotation, RectangleAnnotation, PolygonAnnotation, TextAnnotation,
        ArrowAnnotation, DefectBox
    }

    public enum MVIImageOverlayLayer
    {
        Roi, Measurement, Annotation, Defect
    }
}
