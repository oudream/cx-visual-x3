using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace CxVisual.Foundation.Geometry
{
    public enum FGEPathVerb { Move=1, Line=2, Quadratic=3, Cubic=4, Arc=5, Close=6 }
    /// <summary>通用二维路径段；椭圆弧保留精确参数，不以显示近似作为工程真源。</summary>
    public struct FGEPathCommand
    {
        private FGEPathCommand(FGEPathVerb verb,FGEPoint2D end,FGEPoint2D c1,FGEPoint2D c2,FGEArc2D arc)
        {Verb=verb;End=end;Control1=c1;Control2=c2;Arc=arc;}
        public FGEPathVerb Verb{get;private set;}
        public FGEPoint2D End{get;private set;}
        public FGEPoint2D Control1{get;private set;}
        public FGEPoint2D Control2{get;private set;}
        public FGEArc2D Arc{get;private set;}
        public static FGEPathCommand Move(FGEPoint2D p){return new FGEPathCommand(FGEPathVerb.Move,p,default(FGEPoint2D),default(FGEPoint2D),default(FGEArc2D));}
        public static FGEPathCommand Line(FGEPoint2D p){return new FGEPathCommand(FGEPathVerb.Line,p,default(FGEPoint2D),default(FGEPoint2D),default(FGEArc2D));}
        public static FGEPathCommand Quadratic(FGEPoint2D c,FGEPoint2D end){return new FGEPathCommand(FGEPathVerb.Quadratic,end,c,default(FGEPoint2D),default(FGEArc2D));}
        public static FGEPathCommand Cubic(FGEPoint2D c1,FGEPoint2D c2,FGEPoint2D end){return new FGEPathCommand(FGEPathVerb.Cubic,end,c1,c2,default(FGEArc2D));}
        public static FGEPathCommand ArcTo(FGEArc2D arc){return new FGEPathCommand(FGEPathVerb.Arc,arc.PointAt(1),default(FGEPoint2D),default(FGEPoint2D),arc);}
        public static FGEPathCommand Close(){return new FGEPathCommand(FGEPathVerb.Close,default(FGEPoint2D),default(FGEPoint2D),default(FGEPoint2D),default(FGEArc2D));}
    }
    public sealed class FGEPath2D
    {
        private readonly ReadOnlyCollection<FGEPathCommand> _commands;
        public FGEPath2D(IEnumerable<FGEPathCommand> commands)
        {
            if(commands==null)throw new ArgumentNullException("commands");
            var all=new List<FGEPathCommand>(commands);
            if(all.Count<2||all.Count>100000||all[0].Verb!=FGEPathVerb.Move)throw new ArgumentException("geometry.invalid_path");
            bool active=false;
            foreach(var command in all)
            {
                if(!Enum.IsDefined(typeof(FGEPathVerb),command.Verb))throw new ArgumentException("geometry.invalid_verb");
                if(command.Verb==FGEPathVerb.Move)active=true;
                else if(!active)throw new ArgumentException("geometry.missing_move");
                else if(command.Verb==FGEPathVerb.Close)active=false;
            }
            _commands=all.AsReadOnly();
        }
        public IReadOnlyList<FGEPathCommand> Commands{get{return _commands;}}
        public FGERect2D Bounds
        {
            get
            {
                var vertices=new List<FGEPoint2D>();
                foreach(var cmd in _commands)
                {
                    if(cmd.Verb==FGEPathVerb.Close)continue;
                    vertices.Add(cmd.End);
                    if(cmd.Verb==FGEPathVerb.Quadratic||cmd.Verb==FGEPathVerb.Cubic)vertices.Add(cmd.Control1);
                    if(cmd.Verb==FGEPathVerb.Cubic)vertices.Add(cmd.Control2);
                    if(cmd.Verb==FGEPathVerb.Arc)
                    {
                        // 保守圆包围：旋转的椭圆也被完全包围，不错误截断可视区域。
                        double r=Math.Max(cmd.Arc.RadiusX,cmd.Arc.RadiusY);
                        vertices.Add(new FGEPoint2D(cmd.Arc.Center.X-r,cmd.Arc.Center.Y-r));
                        vertices.Add(new FGEPoint2D(cmd.Arc.Center.X+r,cmd.Arc.Center.Y+r));
                    }
                }
                return FGEGeometry2D.Bounds(vertices);
            }
        }
        /// <summary>按独立子路径采样，不把多个MoveTo起始的轮廓错误连接起来。</summary>
        public IReadOnlyList<IReadOnlyList<FGEPoint2D>> FlattenContours(int subdivisions=48)
        {
            var result=new List<IReadOnlyList<FGEPoint2D>>();
            var segment=new List<FGEPathCommand>();
            foreach(var cmd in _commands)
            {
                if(cmd.Verb==FGEPathVerb.Move && segment.Count>0)
                {
                    if(segment.Count>1)result.Add(new FGEPath2D(segment).Flatten(subdivisions));
                    segment.Clear();
                }
                segment.Add(cmd);
                if(cmd.Verb==FGEPathVerb.Close)
                {
                    result.Add(new FGEPath2D(segment).Flatten(subdivisions));
                    segment.Clear();
                }
            }
            if(segment.Count>1)result.Add(new FGEPath2D(segment).Flatten(subdivisions));
            return result.AsReadOnly();
        }
        public static FGEPath2D Line(FGEPoint2D a,FGEPoint2D b)
        {return new FGEPath2D(new[]{FGEPathCommand.Move(a),FGEPathCommand.Line(b)});}
        public static FGEPath2D Rectangle(double w,double h)
        {
            FGEValidation.Finite(w,"w");FGEValidation.Finite(h,"h");if(w<0||h<0)throw new ArgumentOutOfRangeException("size");
            return new FGEPath2D(new[]{FGEPathCommand.Move(new FGEPoint2D(0,0)),FGEPathCommand.Line(new FGEPoint2D(w,0)),FGEPathCommand.Line(new FGEPoint2D(w,h)),FGEPathCommand.Line(new FGEPoint2D(0,h)),FGEPathCommand.Close()});
        }
        public static FGEPath2D Circle(FGEPoint2D center,double radius){return Ellipse(center,radius,radius);}
        public static FGEPath2D Ellipse(FGEPoint2D center,double rx,double ry)
        {var arc=new FGEArc2D(center,rx,ry,0,Math.PI*2);return new FGEPath2D(new[]{FGEPathCommand.Move(arc.PointAt(0)),FGEPathCommand.ArcTo(arc),FGEPathCommand.Close()});}
        public static FGEPath2D Polyline(IEnumerable<FGEPoint2D> points,bool closed=false)
        {
            if(points==null)throw new ArgumentNullException("points");
            var list=new List<FGEPoint2D>(points);if(list.Count<(closed?3:2)||list.Count>100000)throw new ArgumentException("geometry.invalid_points");
            var commands=new List<FGEPathCommand>{FGEPathCommand.Move(list[0])};
            for(int i=1;i<list.Count;i++)commands.Add(FGEPathCommand.Line(list[i]));
            if(closed)commands.Add(FGEPathCommand.Close());
            return new FGEPath2D(commands);
        }
        public static FGEPath2D Arc(FGEArc2D arc)
        {return new FGEPath2D(new[]{FGEPathCommand.Move(arc.PointAt(0)),FGEPathCommand.ArcTo(arc)});}
        public static FGEPath2D Bezier(FGEBezier2D curve)
        {return new FGEPath2D(new[]{FGEPathCommand.Move(curve.Start),FGEPathCommand.Cubic(curve.Control1,curve.Control2,curve.End)});}
        /// <summary>采样仅供预览；精确路径仍保存在 Commands 中。</summary>
        public IReadOnlyList<FGEPoint2D> Flatten(int subdivisions=32)
        {
            if(subdivisions<2||subdivisions>4096)throw new ArgumentOutOfRangeException("subdivisions");
            var points=new List<FGEPoint2D>();FGEPoint2D current=default(FGEPoint2D),start=default(FGEPoint2D);
            foreach(var c in _commands)
            {
                switch(c.Verb)
                {
                    case FGEPathVerb.Move:current=c.End;start=current;points.Add(current);break;
                    case FGEPathVerb.Line:current=c.End;points.Add(current);break;
                    case FGEPathVerb.Quadratic:
                        FGEPoint2D from=current;
                        for(int i=1;i<=subdivisions;i++){double t=(double)i/subdivisions,u=1-t;points.Add(new FGEPoint2D(u*u*from.X+2*u*t*c.Control1.X+t*t*c.End.X,u*u*from.Y+2*u*t*c.Control1.Y+t*t*c.End.Y));}
                        current=c.End;break;
                    case FGEPathVerb.Cubic:
                        var curve=new FGEBezier2D(current,c.Control1,c.Control2,c.End);
                        for(int i=1;i<=subdivisions;i++)points.Add(curve.PointAt((double)i/subdivisions));
                        current=c.End;break;
                    case FGEPathVerb.Arc:
                        for(int i=1;i<=subdivisions;i++)points.Add(c.Arc.PointAt((double)i/subdivisions));
                        current=c.End;break;
                    case FGEPathVerb.Close: if(!current.Equals(start))points.Add(start);current=start;break;
                }
            }
            return points.AsReadOnly();
        }
    }
}
