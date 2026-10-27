using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>默认九种工程绘图造型；仅创建定义，真实图元通过MEM事务加入工程。</summary>
    public static class MVIShapeCatalog
    {
        /// <summary>
        /// 鼠标/触控/笔输入使用同一纯几何求解；坐标为世界坐标。
        /// 仅返回局部Path与创建位置，不触碰WPF、MEM或工程Revision。
        /// 拖绘按两个端点；折线/多边形/Path按已确认的多个顶点。
        /// </summary>
        public static MRCShapeDefinition CreateDrawn(MRCShapeKind kind,
            IReadOnlyList<FGEPoint2D> worldPoints,bool constrained,
            out double x,out double y)
        {
            if(worldPoints==null||worldPoints.Count<2||worldPoints.Count>4096)
                throw new ArgumentException("visual.draw.point_count");
            foreach(FGEPoint2D p in worldPoints)
            {
                if(double.IsNaN(p.X)||double.IsInfinity(p.X)||
                    double.IsNaN(p.Y)||double.IsInfinity(p.Y))
                    throw new ArgumentException("visual.draw.non_finite");
            }
            if(kind==MRCShapeKind.Polygon&&worldPoints.Count<3)
                throw new ArgumentException("visual.draw.polygon_requires_three");
            x=worldPoints.Min(p=>p.X);y=worldPoints.Min(p=>p.Y);
            // 从左上角开始的形状支持Shift正方形约束，Circle永远正圆。
            FGEPoint2D first=worldPoints[0],last=worldPoints[worldPoints.Count-1];
            double dx=last.X-first.X,dy=last.Y-first.Y;
            if(worldPoints.Count==2&&(kind==MRCShapeKind.Circle||
                constrained&&(kind==MRCShapeKind.Rect||kind==MRCShapeKind.Ellipse)))
            {
                double side=kind==MRCShapeKind.Circle?
                    Math.Max(Math.Abs(dx),Math.Abs(dy)):
                    Math.Max(Math.Abs(dx),Math.Abs(dy));
                dx=Math.Sign(dx==0?1:dx)*side;
                dy=Math.Sign(dy==0?1:dy)*side;
                last=new FGEPoint2D(first.X+dx,first.Y+dy);
                x=Math.Min(first.X,last.X);y=Math.Min(first.Y,last.Y);
            }
            double width=Math.Abs(last.X-first.X),height=Math.Abs(last.Y-first.Y);
            // 避免零长度路径生成不可选中、不可逆的工程图元。
            if(worldPoints.Count==2&&Math.Sqrt(dx*dx+dy*dy)<1e-6)
                throw new ArgumentException("visual.draw.too_small");
            switch(kind)
            {
                case MRCShapeKind.Rect:
                    if(width<1e-6||height<1e-6)throw new ArgumentException("visual.draw.invalid_rect");
                    return MRCShapeDefinition.Rect(width,height);
                case MRCShapeKind.Circle:
                {
                    double radius=width/2;
                    if(radius<1e-6)throw new ArgumentException("visual.draw.invalid_circle");
                    return MRCShapeDefinition.Circle(new FGEPoint2D(radius,radius),radius);
                }
                case MRCShapeKind.Ellipse:
                    if(width<1e-6||height<1e-6)throw new ArgumentException("visual.draw.invalid_ellipse");
                    return MRCShapeDefinition.Ellipse(new FGEPoint2D(width/2,height/2),
                        width/2,height/2);
                case MRCShapeKind.Line:
                    return MRCShapeDefinition.Line(new FGEPoint2D(first.X-x,first.Y-y),
                        new FGEPoint2D(last.X-x,last.Y-y));
                case MRCShapeKind.Polyline:
                case MRCShapeKind.Polygon:
                case MRCShapeKind.Path:
                {
                    double originX=x,originY=y;
                    FGEPoint2D[] local=worldPoints.Select(point=>
                        new FGEPoint2D(point.X-originX,point.Y-originY)).ToArray();
                    if(kind==MRCShapeKind.Polygon)return MRCShapeDefinition.Polygon(local);
                    if(kind==MRCShapeKind.Polyline)return MRCShapeDefinition.Polyline(local);
                    var commands=new List<FGEPathCommand>{FGEPathCommand.Move(local[0])};
                    for(int i=1;i<local.Length;i++)commands.Add(FGEPathCommand.Line(local[i]));
                    return MRCShapeDefinition.Path(new FGEPath2D(commands));
                }
                case MRCShapeKind.Arc:
                    if(width<1e-6||height<1e-6)throw new ArgumentException("visual.draw.invalid_arc");
                    return MRCShapeDefinition.Arc(new FGEArc2D(
                        new FGEPoint2D(width/2,height),width/2,height,Math.PI,Math.PI));
                case MRCShapeKind.Bezier:
                    // 首期拖动定义起终点和默认切线；之后进入GeometryEdit精修控制点。
                    return MRCShapeDefinition.Bezier(new FGEBezier2D(
                        new FGEPoint2D(first.X-x,first.Y-y),
                        new FGEPoint2D(first.X-x+dx/3,first.Y-y),
                        new FGEPoint2D(first.X-x+2*dx/3,first.Y-y+dy),
                        new FGEPoint2D(last.X-x,last.Y-y)));
                default:throw new ArgumentOutOfRangeException("kind");
            }
        }
        /// <summary>文字框纯布局规则，与SceneView Ghost共用；短点击按默认尺寸放置。</summary>
        public static void TextBounds(IReadOnlyList<FGEPoint2D> points,
            out double x,out double y,out double width,out double height)
        {
            if(points==null||points.Count!=2)throw new ArgumentException("visual.draw.text_points");
            foreach(var pt in points)
                if(double.IsNaN(pt.X)||double.IsInfinity(pt.X)||
                    double.IsNaN(pt.Y)||double.IsInfinity(pt.Y))
                    throw new ArgumentException("visual.draw.non_finite");
            double dx=points[1].X-points[0].X,dy=points[1].Y-points[0].Y;
            if(Math.Sqrt(dx*dx+dy*dy)<3)
            {
                x=points[0].X;y=points[0].Y;
                width=150;height=30;return;
            }
            x=Math.Min(points[0].X,points[1].X);
            y=Math.Min(points[0].Y,points[1].Y);
            width=Math.Max(40,Math.Abs(dx));
            height=Math.Max(24,Math.Abs(dy));
        }
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
