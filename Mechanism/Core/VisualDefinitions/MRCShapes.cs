using System;
using System.Collections.Generic;
using System.IO;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Mechanism.Core
{
    /// <summary>九种用户基础绘图入口，圆角矩形是 Rect 的样式/几何参数，不形成第十个基础种类。</summary>
    public enum MRCShapeKind { Line=1, Rect=2, Circle=3, Ellipse=4, Polyline=5, Polygon=6, Arc=7, Bezier=8, Path=9 }

    /// <summary>不可变图形定义，几何命令与形状种类分离；可由其他专业复用，不持有 WPF 对象。</summary>
    public sealed class MRCShapeDefinition
    {
        public const string ProviderId = "core.visual.shape";
        private const int Magic = 0x4D534831;
        public MRCShapeDefinition(MRCShapeKind kind,FGEPath2D geometry)
        {
            if(!Enum.IsDefined(typeof(MRCShapeKind),kind))throw new ArgumentOutOfRangeException("kind");
            Kind=kind;Geometry=geometry??throw new ArgumentNullException("geometry");
        }
        public MRCShapeKind Kind{get;private set;}
        public FGEPath2D Geometry{get;private set;}
        public FGERect2D Bounds{get{return Geometry.Bounds;}}
        public static MRCShapeDefinition Line(FGEPoint2D a,FGEPoint2D b){return new MRCShapeDefinition(MRCShapeKind.Line,FGEPath2D.Line(a,b));}
        public static MRCShapeDefinition Rect(double w,double h){return new MRCShapeDefinition(MRCShapeKind.Rect,FGEPath2D.Rectangle(w,h));}
        public static MRCShapeDefinition Circle(FGEPoint2D center,double r){return new MRCShapeDefinition(MRCShapeKind.Circle,FGEPath2D.Circle(center,r));}
        public static MRCShapeDefinition Ellipse(FGEPoint2D center,double rx,double ry){return new MRCShapeDefinition(MRCShapeKind.Ellipse,FGEPath2D.Ellipse(center,rx,ry));}
        public static MRCShapeDefinition Polyline(IEnumerable<FGEPoint2D> points){return new MRCShapeDefinition(MRCShapeKind.Polyline,FGEPath2D.Polyline(points));}
        public static MRCShapeDefinition Polygon(IEnumerable<FGEPoint2D> points){return new MRCShapeDefinition(MRCShapeKind.Polygon,FGEPath2D.Polyline(points,true));}
        public static MRCShapeDefinition Arc(FGEArc2D arc){return new MRCShapeDefinition(MRCShapeKind.Arc,FGEPath2D.Arc(arc));}
        public static MRCShapeDefinition Bezier(FGEBezier2D bezier){return new MRCShapeDefinition(MRCShapeKind.Bezier,FGEPath2D.Bezier(bezier));}
        public static MRCShapeDefinition Path(FGEPath2D path){return new MRCShapeDefinition(MRCShapeKind.Path,path);}

        internal MPMAspect ToAspect()
        {
            using(var memory=new MemoryStream())
            using(var writer=new BinaryWriter(memory))
            {
                writer.Write(Magic);writer.Write(1);writer.Write((int)Kind);
                writer.Write(Geometry.Commands.Count);
                foreach(var cmd in Geometry.Commands)
                {
                    writer.Write((int)cmd.Verb);WritePoint(writer,cmd.End);WritePoint(writer,cmd.Control1);WritePoint(writer,cmd.Control2);
                    WritePoint(writer,cmd.Arc.Center);writer.Write(cmd.Arc.RadiusX);writer.Write(cmd.Arc.RadiusY);
                    writer.Write(cmd.Arc.StartRadians);writer.Write(cmd.Arc.SweepRadians);writer.Write(cmd.Arc.RotationRadians);
                }
                writer.Flush();return new MPMAspect(MPMAspectKind.Visual,ProviderId,1,memory.ToArray());
            }
        }
        private static void WritePoint(BinaryWriter w,FGEPoint2D p){w.Write(p.X);w.Write(p.Y);}
        private static FGEPoint2D ReadPoint(BinaryReader r){return new FGEPoint2D(r.ReadDouble(),r.ReadDouble());}
        public static MRCShapeDefinition FromAspect(MPMAspect aspect)
        {
            if(aspect==null||aspect.Kind!=MPMAspectKind.Visual||aspect.ProviderId!=ProviderId||aspect.SchemaVersion!=1)
                throw new InvalidDataException("visual.shape.aspect_mismatch");
            byte[] bytes=aspect.Payload;
            using(var stream=new MemoryStream(bytes,false))using(var r=new BinaryReader(stream))
            {
                if(r.ReadInt32()!=Magic||r.ReadInt32()!=1)throw new InvalidDataException("visual.shape.version");
                MRCShapeKind kind=(MRCShapeKind)r.ReadInt32();
                int count=r.ReadInt32();if(count<2||count>100000||bytes.Length>4*1024*1024)throw new InvalidDataException("visual.shape.budget");
                var all=new List<FGEPathCommand>(count);
                for(int i=0;i<count;i++)
                {
                    FGEPathVerb verb=(FGEPathVerb)r.ReadInt32();
                    FGEPoint2D end=ReadPoint(r),c1=ReadPoint(r),c2=ReadPoint(r),center=ReadPoint(r);
                    double rx=r.ReadDouble(),ry=r.ReadDouble(),start=r.ReadDouble(),sweep=r.ReadDouble(),rotation=r.ReadDouble();
                    switch(verb)
                    {
                        case FGEPathVerb.Move:all.Add(FGEPathCommand.Move(end));break;
                        case FGEPathVerb.Line:all.Add(FGEPathCommand.Line(end));break;
                        case FGEPathVerb.Quadratic:all.Add(FGEPathCommand.Quadratic(c1,end));break;
                        case FGEPathVerb.Cubic:all.Add(FGEPathCommand.Cubic(c1,c2,end));break;
                        case FGEPathVerb.Arc:
                            var arc=new FGEArc2D(center,rx,ry,start,sweep,rotation);
                            if(arc.PointAt(1).DistanceTo(end)>1e-7*Math.Max(1,Math.Max(rx,ry)))throw new InvalidDataException("visual.shape.arc_end");
                            all.Add(FGEPathCommand.ArcTo(arc));break;
                        case FGEPathVerb.Close:all.Add(FGEPathCommand.Close());break;
                        default:throw new InvalidDataException("visual.shape.verb");
                    }
                }
                if(stream.Position!=stream.Length)throw new InvalidDataException("visual.shape.trailing_bytes");
                return new MRCShapeDefinition(kind,new FGEPath2D(all));
            }
        }
        public static MRCShapeDefinition Read(MRCVisualItem item)
        {
            if(item==null)throw new ArgumentNullException("item");
            foreach(MPMAspect aspect in item.Aspects)
                if(aspect.Kind==MPMAspectKind.Visual&&aspect.ProviderId==ProviderId)
                    return FromAspect(aspect);
            throw new InvalidOperationException("visual.shape.missing_definition");
        }
    }

    public sealed class MRCShapeElement : MRCElement
    {
        // 装载器先建对象，随后通过同一工程声明协议附着形状定义。
        public MRCShapeElement(Guid id,string name,double x,double y,double width,double height)
            :base(id,name,"core.shape",x,y,width,height){}
        public MRCShapeDefinition Shape{get{return MRCShapeDefinition.Read(this);}}
        internal static MRCShapeElement Create(Guid id,string name,MRCShapeDefinition shape,double x,double y)
        {
            if(shape==null)throw new ArgumentNullException("shape");
            FGERect2D bounds=shape.Bounds;
            // 布局包围盒必须可被原有组编辑系统接受；路径保留独立的真实零宽几何。
            var item=new MRCShapeElement(id,name,x,y,Math.Max(1,bounds.Right-Math.Min(0,bounds.Left)),Math.Max(1,bounds.Bottom-Math.Min(0,bounds.Top)));
            return item;
        }
    }

    public sealed partial class MTCEditSession
    {
        public MRCShapeElement AddShape(MEMNode parent,string name,MRCShapeDefinition shape,double x=0,double y=0)
        {
            var item=(MRCShapeElement)Add(parent,MRCShapeElement.Create(Guid.NewGuid(),name,shape,x,y));
            SetAspect(item,shape.ToAspect());
            return item;
        }
        public void SetShape(MRCShapeElement element,MRCShapeDefinition shape)
        {if(shape==null)throw new ArgumentNullException("shape");SetAspect(element,shape.ToAspect());}
    }
    public sealed partial class MEMEngineeringSession
    {
        public MRCShapeElement AddShape(MEMNode parent,string name,MRCShapeDefinition shape,double x=0,double y=0)
        {return Edit("添加形状",e=>e.AddShape(parent,name,shape,x,y)).RequireValue();}
        public void SetShape(MRCShapeElement element,MRCShapeDefinition shape)
        {Edit("修改形状",e=>e.SetShape(element,shape)).RequireValue();}
    }
}
