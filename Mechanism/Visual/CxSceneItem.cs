using System;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
namespace CxVisual.Mechanism.Visual
{
    public sealed class CxSceneItem
    {
        private MRCTransform _world,_moveParent;
        private double _width,_height,_moveX,_moveY;
        public CxSceneItem(MRCElement source)
        {
            if(source==null)throw new ArgumentNullException("source");double x,y,xx,xy,yx,yy;source.TransformPoint(0,0,out x,out y);source.TransformPoint(1,0,out xx,out xy);source.TransformPoint(0,1,out yx,out yy);
            var world=new MRCTransform(xx-x,yx-x,xy-y,yy-y,x,y);var parent=world.Compose(Inverse(source.Transform));
            Initialize(source.Id,source.Name,null,world,source.Width,source.Height,source.IsSelectable&&!source.IsLocked,parent,source.X,source.Y);
        }
        internal CxSceneItem(Guid selectionId,string name,MRIObjectAddress address,MRCTransform world,double width,double height,bool selectable,MRCTransform moveParent,double moveX,double moveY)
        {Initialize(selectionId,name,address,world,width,height,selectable,moveParent,moveX,moveY);}
        private void Initialize(Guid id,string name,MRIObjectAddress address,MRCTransform world,double width,double height,bool selectable,MRCTransform moveParent,double moveX,double moveY)
        {
            Id=id;Name=name;Address=address;_world=world;_width=width;_height=height;IsSelectable=selectable;_moveParent=moveParent;_moveX=moveX;_moveY=moveY;
            double x1,y1,x2,y2,x3,y3,x4,y4;world.TransformPoint(0,0,out x1,out y1);world.TransformPoint(width,0,out x2,out y2);world.TransformPoint(0,height,out x3,out y3);world.TransformPoint(width,height,out x4,out y4);
            TopLeft=new CxPoint2D(x1,y1);TopRight=new CxPoint2D(x2,y2);BottomLeft=new CxPoint2D(x3,y3);BottomRight=new CxPoint2D(x4,y4);
            double left=Math.Min(Math.Min(x1,x2),Math.Min(x3,x4)),top=Math.Min(Math.Min(y1,y2),Math.Min(y3,y4));Bounds=new CxRect2D(left,top,Math.Max(Math.Max(x1,x2),Math.Max(x3,x4))-left,Math.Max(Math.Max(y1,y2),Math.Max(y3,y4))-top);
        }
        internal static MRCTransform Inverse(MRCTransform transform)
        {
            double d=transform.M11*transform.M22-transform.M12*transform.M21;if(d==0||double.IsNaN(d)||double.IsInfinity(d))throw new InvalidOperationException("visual.singular_transform");
            double a=transform.M22/d,b=-transform.M12/d,c=-transform.M21/d,e=transform.M11/d;return new MRCTransform(a,b,c,e,-a*transform.X-b*transform.Y,-c*transform.X-e*transform.Y);
        }
        public Guid Id {get;private set;} public string Name {get;private set;}public MRIObjectAddress Address {get;private set;}public bool IsSelectable {get;private set;}
        public CxRect2D Bounds {get;private set;}public CxPoint2D TopLeft {get;private set;}public CxPoint2D TopRight {get;private set;}public CxPoint2D BottomRight {get;private set;}public CxPoint2D BottomLeft {get;private set;}
        public bool Contains(double x,double y)
        {if(!IsSelectable)return false;double lx,ly;Inverse(_world).TransformPoint(x,y,out lx,out ly);return lx>=0&&ly>=0&&lx<=_width&&ly<=_height;}
        public void PositionAfterWorldDelta(double dx,double dy,out double x,out double y)
        {var inverse=Inverse(_moveParent);x=_moveX+inverse.M11*dx+inverse.M12*dy;y=_moveY+inverse.M21*dx+inverse.M22*dy;}
    }
}
