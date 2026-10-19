using System;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>默认九种工程绘图造型；仅创建定义，真实图元通过MEM事务加入工程。</summary>
    public static class MVIShapeCatalog
    {
        public static MRCShapeDefinition CreateDefault(MRCShapeKind kind)
        {
            switch(kind)
            {
                case MRCShapeKind.Line:return MRCShapeDefinition.Line(
                    new FGEPoint2D(0,0),new FGEPoint2D(120,75));
                case MRCShapeKind.Rect:return MRCShapeDefinition.Rect(120,75);
                case MRCShapeKind.Circle:return MRCShapeDefinition.Circle(new FGEPoint2D(40,40),40);
                case MRCShapeKind.Ellipse:return MRCShapeDefinition.Ellipse(new FGEPoint2D(60,40),60,40);
                case MRCShapeKind.Polyline:return MRCShapeDefinition.Polyline(new[]{
                    new FGEPoint2D(0,80),new FGEPoint2D(30,20),new FGEPoint2D(60,70),
                    new FGEPoint2D(95,0),new FGEPoint2D(120,45)});
                case MRCShapeKind.Polygon:return MRCShapeDefinition.Polygon(new[]{
                    new FGEPoint2D(60,0),new FGEPoint2D(120,42),new FGEPoint2D(95,100),
                    new FGEPoint2D(25,100),new FGEPoint2D(0,42)});
                case MRCShapeKind.Arc:return MRCShapeDefinition.Arc(new FGEArc2D(
                    new FGEPoint2D(60,75),58,58,Math.PI,Math.PI));
                case MRCShapeKind.Bezier:return MRCShapeDefinition.Bezier(new FGEBezier2D(
                    new FGEPoint2D(0,75),new FGEPoint2D(25,-30),
                    new FGEPoint2D(90,140),new FGEPoint2D(120,10)));
                case MRCShapeKind.Path:return MRCShapeDefinition.Path(new FGEPath2D(new[]{
                    FGEPathCommand.Move(new FGEPoint2D(10,95)),
                    FGEPathCommand.Cubic(new FGEPoint2D(15,40),new FGEPoint2D(40,0),
                        new FGEPoint2D(60,20)),
                    FGEPathCommand.Quadratic(new FGEPoint2D(110,50),new FGEPoint2D(120,90)),
                    FGEPathCommand.Line(new FGEPoint2D(55,105)),FGEPathCommand.Close()}));
                default:throw new ArgumentOutOfRangeException("kind");
            }
        }
    }
}
