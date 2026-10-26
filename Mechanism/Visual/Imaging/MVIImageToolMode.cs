namespace CxVisual.Mechanism.Visual
{
    /// <summary>
    /// 继承旧ImageToolMode互斥工具语义。目前开放已完成交互的工具；新增类型需先验证鼠标语义。
    /// </summary>
    public enum MVIImageToolMode
    {
        Navigate,
        RectangleRoi,
        CircleRoi,
        EllipseRoi,
        Distance,
        Point,
        Text,
        Arrow,
        DefectBox,
        MoveOverlay,
        ResizeOverlay,
        DeleteOverlay,
        PolygonRoi,
        Angle,
        HorizontalDistance,
        VerticalDistance,
        Diameter,
        Area,
        PolygonArea,
        LineProfile
    }
}
