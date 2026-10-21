namespace CxECTImageWinForms.Models
{
    /// <summary>
    /// 图窗输入的互斥工具状态，避免平移、ROI 与测量同时处理同一次鼠标事件。
    /// </summary>
    public enum ImageToolMode
    {
        Navigate,
        RectangleRoi,
        CircleRoi,
        PolygonRoi,
        MoveRoi,
        ResizeRoi,
        DeleteOverlay,
        MeasureDistance,
        MeasureHorizontal,
        MeasureVertical,
        MeasureAngle,
        MeasureDiameter,
        MeasureArea,
        MeasureLineProfile,
        AddPointAnnotation,
        AddRectangleAnnotation,
        AddPolygonAnnotation,
        AddTextAnnotation,
        AddArrowAnnotation,
        AddDefectBox
    }
}
