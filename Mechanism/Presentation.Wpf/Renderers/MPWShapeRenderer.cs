using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>九种基础Shape共用路径绘制器；曲线保持WPF原生Bezier/Arc指令，不将图元转成AABB。</summary>
    public static class MPWShapeRenderer
    {
        private static Point PointOf(FGEPoint2D p){return new Point(p.X,p.Y);}
        public static Geometry BuildGeometry(MVISceneItem item)
        {
            if(item==null||item.Shape==null)throw new ArgumentException("Scene Shape required.","item");
            return BuildGeometry(item.Shape.Geometry,item.GeometryToWorld);
        }
        /// <summary>拖拽预览复用同一原生路径渲染；传入纯几何副本，不修改MEM工程。</summary>
        public static Geometry BuildGeometry(FGEPath2D geometry,FGETransform2D transform)
        {
            if(geometry==null)throw new ArgumentNullException("geometry");
            var path=new StreamGeometry{FillRule=FillRule.Nonzero};
            using(StreamGeometryContext dc=path.Open())
            {
                bool hasFigure=false;
                var commands=geometry.Commands;
                for(int i=0;i<commands.Count;i++)
                {
                    FGEPathCommand command=commands[i];
                    switch(command.Verb)
                    {
                        case FGEPathVerb.Move:
                            bool closed=false;
                            for(int k=i+1;k<commands.Count&&commands[k].Verb!=FGEPathVerb.Move;k++)
                                if(commands[k].Verb==FGEPathVerb.Close){closed=true;break;}
                            dc.BeginFigure(PointOf(command.End),true,closed);
                            hasFigure=true;
                            break;
                        case FGEPathVerb.Line:
                            if(!hasFigure)throw new InvalidOperationException("visual.path.missing_move");
                            dc.LineTo(PointOf(command.End),true,false);
                            break;
                        case FGEPathVerb.Quadratic:
                            dc.QuadraticBezierTo(PointOf(command.Control1),PointOf(command.End),true,false);
                            break;
                        case FGEPathVerb.Cubic:
                            dc.BezierTo(PointOf(command.Control1),PointOf(command.Control2),PointOf(command.End),true,false);
                            break;
                        case FGEPathVerb.Arc:
                        {
                            FGEArc2D arc=command.Arc;
                            double rotation=arc.RotationRadians*180/Math.PI;
                            var size=new Size(arc.RadiusX,arc.RadiusY);
                            SweepDirection direction=arc.SweepRadians>=0?
                                SweepDirection.Clockwise:SweepDirection.Counterclockwise;
                            if(Math.Abs(arc.SweepRadians)>=Math.PI*2-1e-9)
                            {
                                dc.ArcTo(PointOf(arc.PointAt(0.5)),size,rotation,false,direction,true,false);
                                dc.ArcTo(PointOf(arc.PointAt(1)),size,rotation,false,direction,true,false);
                            }
                            else dc.ArcTo(PointOf(arc.PointAt(1)),size,rotation,
                                Math.Abs(arc.SweepRadians)>Math.PI,direction,true,false);
                            break;
                        }
                        case FGEPathVerb.Close:
                            hasFigure=false;
                            break;
                        default:throw new InvalidOperationException("visual.path.verb_not_supported");
                    }
                }
            }
            var m=transform;
            // WPF Matrix参数为列基: x' = M11*x + M21*y + OffsetX。
            path.Transform=new MatrixTransform(new Matrix(m.M11,m.M21,m.M12,m.M22,m.DX,m.DY));
            path.Freeze();
            return path;
        }

        public static Brush BrushOf(MRCBrushDefinition definition,Brush fallback=null,
            Func<string,Brush> resourceResolver=null)
        {
            if(definition==null)return fallback??Brushes.Transparent;
            switch(definition.Kind)
            {
                case MRCBrushKind.None:return Brushes.Transparent;
                case MRCBrushKind.Solid:return new SolidColorBrush(Color.FromArgb(
                    definition.Color.A,definition.Color.R,definition.Color.G,definition.Color.B));
                case MRCBrushKind.LinearGradient:
                {
                    var brush=new LinearGradientBrush();
                    foreach(MRCGradientStop stop in definition.Stops)
                        brush.GradientStops.Add(new GradientStop(
                            Color.FromArgb(stop.Color.A,stop.Color.R,stop.Color.G,stop.Color.B),stop.Offset));
                    brush.StartPoint=new Point(0,0);brush.EndPoint=new Point(1,0);brush.Freeze();
                    return brush;
                }
                case MRCBrushKind.Resource:
                {
                    Brush value=resourceResolver==null?null:resourceResolver(definition.ResourceId);
                    return value??fallback??Brushes.Transparent;
                }
                default:throw new InvalidOperationException("visual.brush.invalid_kind");
            }
        }
        public static void Draw(DrawingContext dc,MVISceneItem item,double dx=0,double dy=0,
            Func<string,Brush> resourceResolver=null)
        {
            if(dc==null)throw new ArgumentNullException("dc");
            if(item==null||item.Shape==null)throw new ArgumentException("Shape required.","item");
            MRCVisualStyle style=item.Style;
            Geometry geometry=BuildGeometry(item);
            Brush fill=style.Fill.Kind==MRCBrushKind.None?null:
                BrushOf(style.Fill,Brushes.Transparent,resourceResolver);
            Brush stroke=BrushOf(style.Stroke,Brushes.SteelBlue,resourceResolver);
            Pen pen=style.Stroke.Kind==MRCBrushKind.None?null:new Pen(stroke,style.StrokeWidth);
            dc.PushOpacity(style.Opacity);
            if(dx!=0||dy!=0)dc.PushTransform(new TranslateTransform(dx,dy));
            dc.DrawGeometry(fill,pen,geometry);
            if(dx!=0||dy!=0)dc.Pop();
            dc.Pop();
        }
    }
}
