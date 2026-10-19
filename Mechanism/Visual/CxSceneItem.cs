using System;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>一次工程快照投影所得的技术无关场景项；保留定义身份及实例地址。</summary>
    public sealed class CxSceneItem
    {
        private MRCTransform _world, _moveParent;
        private double _width, _height, _moveX, _moveY;

        public CxSceneItem(MRCElement source)
        {
            if(source==null)throw new ArgumentNullException("source");
            double x,y,xx,xy,yx,yy;
            source.TransformPoint(0,0,out x,out y);
            source.TransformPoint(1,0,out xx,out xy);
            source.TransformPoint(0,1,out yx,out yy);
            var world=new MRCTransform(xx-x,yx-x,xy-y,yy-y,x,y);
            var parent=world.Compose(Inverse(source.Transform));
            var shape=source as MRCShapeElement;
            var text=source as MRCTextElement;
            Initialize(source.Id,source.Name,null,world,source.Width,source.Height,
                source.IsSelectable&&!source.IsLocked,parent,source.X,source.Y,
                source.TypeId,shape==null?null:shape.Shape,
                MRCVisualAppearance.StyleOf(source),text==null?"":text.Text,null,null,null);
        }
        internal CxSceneItem(Guid selectionId,string name,MRIObjectAddress address,
            MRCTransform world,double width,double height,bool selectable,
            MRCTransform moveParent,double moveX,double moveY,
            string typeId=null,MRCShapeDefinition shape=null,MRCVisualStyle style=null,string text=null,
            Guid? assetId=null,string mediaType=null,MPMValue runtimeValue=null)
        {
            Initialize(selectionId,name,address,world,width,height,selectable,moveParent,
                moveX,moveY,typeId,shape,style,text,assetId,mediaType,runtimeValue);
        }
        private void Initialize(Guid id,string name,MRIObjectAddress address,MRCTransform world,
            double width,double height,bool selectable,MRCTransform moveParent,
            double moveX,double moveY,string typeId,MRCShapeDefinition shape,
            MRCVisualStyle style,string text,Guid? assetId,string mediaType,MPMValue runtimeValue)
        {
            Id=id;Name=name;Address=address;_world=world;_width=width;_height=height;
            IsSelectable=selectable;_moveParent=moveParent;_moveX=moveX;_moveY=moveY;
            ElementTypeId=typeId??"core.rectangle";Shape=shape;
            Style=style??MRCVisualStyle.Default;Text=text??"";
            AssetId=assetId;MediaType=mediaType;RuntimeValue=runtimeValue;
            double x1,y1,x2,y2,x3,y3,x4,y4;
            world.TransformPoint(0,0,out x1,out y1);world.TransformPoint(width,0,out x2,out y2);
            world.TransformPoint(0,height,out x3,out y3);world.TransformPoint(width,height,out x4,out y4);
            TopLeft=new FGEPoint2D(x1,y1);TopRight=new FGEPoint2D(x2,y2);
            BottomLeft=new FGEPoint2D(x3,y3);BottomRight=new FGEPoint2D(x4,y4);
            double left=Math.Min(Math.Min(x1,x2),Math.Min(x3,x4)),top=Math.Min(Math.Min(y1,y2),Math.Min(y3,y4));
            Bounds=new FGERect2D(left,top,Math.Max(Math.Max(x1,x2),Math.Max(x3,x4))-left,
                Math.Max(Math.Max(y1,y2),Math.Max(y3,y4))-top);
        }
        internal static MRCTransform Inverse(MRCTransform transform){return transform.Inverse();}
        public Guid Id{get;private set;}
        public string Name{get;private set;}
        public MRIObjectAddress Address{get;private set;}
        public bool IsSelectable{get;private set;}
        public double LocalWidth{get{return _width;}}
        public double LocalHeight{get{return _height;}}
        public string ElementTypeId{get;private set;}
        public MRCShapeDefinition Shape{get;private set;}
        public MRCVisualStyle Style{get;private set;}
        public string Text{get;private set;}
        public Guid? AssetId{get;private set;}
        public string MediaType{get;private set;}
        public MPMValue RuntimeValue{get;private set;}
        public MRCTransform WorldTransform{get{return _world;}}
        public FGERect2D Bounds{get;private set;}
        public FGEPoint2D TopLeft{get;private set;}
        public FGEPoint2D TopRight{get;private set;}
        public FGEPoint2D BottomRight{get;private set;}
        public FGEPoint2D BottomLeft{get;private set;}

        /// <summary>精确路径与工程布局之间的仿射桥梁；退化线段保持线本身的几何语义。</summary>
        public FGETransform2D GeometryToWorld
        {
            get
            {
                FGETransform2D world=_world.ToGeometry();
                if(Shape==null)return world;
                FGERect2D b=Shape.Bounds;
                double sx=b.Width==0?1:_width/b.Width,sy=b.Height==0?1:_height/b.Height;
                FGETransform2D local=FGETransform2D.Scale(sx,sy)
                    .Compose(FGETransform2D.Translation(-b.Left,-b.Top));
                return world.Compose(local);
            }
        }
        public bool Contains(double x,double y)
        {
            if(!IsSelectable)return false;
            if(Shape!=null)
            {
                FGETransform2D inv;
                if(!GeometryToWorld.TryInverse(out inv))return false;
                FGEPoint2D point=inv.TransformPoint(new FGEPoint2D(x,y));
                FGETransform2D transform=GeometryToWorld;
                double sx=Math.Sqrt(transform.M11*transform.M11+transform.M21*transform.M21);
                double sy=Math.Sqrt(transform.M12*transform.M12+transform.M22*transform.M22);
                double pixels=Math.Min(sx,sy);
                double tolerance=Math.Max(3,Style.StrokeWidth/2)/Math.Max(1e-8,pixels);
                if(Style.Fill.Kind==MRCBrushKind.None)
                    return FGEGeometry2D.DistanceToPath(point,Shape.Geometry)<=tolerance;
                return Shape.HitTest(point,tolerance);
            }
            double lx,ly;
            Inverse(_world).TransformPoint(x,y,out lx,out ly);
            return lx>=0&&ly>=0&&lx<=_width&&ly<=_height;
        }
        public void PositionAfterWorldDelta(double dx,double dy,out double x,out double y)
        {
            var inverse=Inverse(_moveParent);
            x=_moveX+inverse.M11*dx+inverse.M12*dy;
            y=_moveY+inverse.M21*dx+inverse.M22*dy;
        }
    }
}
