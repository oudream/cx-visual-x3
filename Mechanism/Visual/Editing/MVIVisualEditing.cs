using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Visual
{
    public enum MVIVisualHandleKind { Anchor=1, Control1=2, Control2=3, Resize=4, Rotate=5, ArcCenter=6, ArcRadiusX=7, ArcRadiusY=8, ArcStart=9, ArcEnd=10 }

    /// <summary>只描述一次场景投影中的编辑柄；真正修改由MEM工程事务提交。</summary>
    public sealed class MVIVisualHandle
    {
        public MVIVisualHandle(Guid elementId,MVIVisualHandleKind kind,int commandIndex,FGEPoint2D worldPoint)
        {ElementId=elementId;Kind=kind;CommandIndex=commandIndex;WorldPoint=worldPoint;}
        public Guid ElementId{get;private set;}
        public MVIVisualHandleKind Kind{get;private set;}
        public int CommandIndex{get;private set;}
        public FGEPoint2D WorldPoint{get;private set;}
    }

    public static class MVIVisualEditing
    {
        /// <summary>输出世界坐标Handle，同样适用于嵌套Group和旋转/剪切后的Path。</summary>
        public static IReadOnlyList<MVIVisualHandle> Handles(MVISceneItem item)
        {
            if(item==null)throw new ArgumentNullException("item");
            var result=new List<MVIVisualHandle>();
            if(!item.IsSelectable)return result.AsReadOnly();
            if(item.Shape!=null&&item.Shape.Kind!=MRCShapeKind.Arc&&
                item.Shape.Kind!=MRCShapeKind.Circle&&item.Shape.Kind!=MRCShapeKind.Ellipse)
            {
                FGETransform2D matrix=item.GeometryToWorld;
                var commands=item.Shape.Geometry.Commands;
                for(int i=0;i<commands.Count&&result.Count<512;i++)
                {
                    FGEPathCommand cmd=commands[i];
                    if((cmd.Verb==FGEPathVerb.Move&&
                        (i+1>=commands.Count||commands[i+1].Verb!=FGEPathVerb.Arc))||
                        cmd.Verb==FGEPathVerb.Line||
                        cmd.Verb==FGEPathVerb.Quadratic||cmd.Verb==FGEPathVerb.Cubic)
                        result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.Anchor,i,
                            matrix.TransformPoint(cmd.End)));
                    if(cmd.Verb==FGEPathVerb.Quadratic||cmd.Verb==FGEPathVerb.Cubic)
                        result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.Control1,i,
                            matrix.TransformPoint(cmd.Control1)));
                    if(cmd.Verb==FGEPathVerb.Cubic)
                        result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.Control2,i,
                            matrix.TransformPoint(cmd.Control2)));
                }
            }
            if(item.Shape!=null&&(item.Shape.Kind==MRCShapeKind.Arc||
                item.Shape.Kind==MRCShapeKind.Circle||item.Shape.Kind==MRCShapeKind.Ellipse))
            {
                var commands=item.Shape.Geometry.Commands;
                for(int i=0;i<commands.Count;i++)
                    if(commands[i].Verb==FGEPathVerb.Arc)
                    {
                        FGEArc2D arc=commands[i].Arc;
                        FGETransform2D transform=item.GeometryToWorld;
                        result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.ArcCenter,i,
                            transform.TransformPoint(arc.Center)));
                        double rotation=arc.RotationRadians;
                        result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.ArcRadiusX,i,
                            transform.TransformPoint(new FGEPoint2D(
                                arc.Center.X+arc.RadiusX*Math.Cos(rotation),
                                arc.Center.Y+arc.RadiusX*Math.Sin(rotation)))));
                        if(item.Shape.Kind!=MRCShapeKind.Circle)
                            result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.ArcRadiusY,i,
                                transform.TransformPoint(new FGEPoint2D(
                                    arc.Center.X-arc.RadiusY*Math.Sin(rotation),
                                    arc.Center.Y+arc.RadiusY*Math.Cos(rotation)))));
                        if(item.Shape.Kind==MRCShapeKind.Arc)
                        {
                            result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.ArcStart,i,
                                transform.TransformPoint(arc.PointAt(0))));
                            result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.ArcEnd,i,
                                transform.TransformPoint(arc.PointAt(1))));
                        }
                        break;
                    }
            }
            // 将缩放手柄移到角点外侧，避免与路径末端重叠。
            result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.Resize,-1,
                new FGEPoint2D(item.BottomRight.X+12,item.BottomRight.Y+12)));
            FGEPoint2D top=new FGEPoint2D((item.TopLeft.X+item.TopRight.X)/2,
                (item.TopLeft.Y+item.TopRight.Y)/2);
            FGEPoint2D center=new FGEPoint2D((item.TopLeft.X+item.BottomRight.X)/2,
                (item.TopLeft.Y+item.BottomRight.Y)/2);
            FGEVector2D direction=top-center;
            double length=direction.Length;
            // 旋转柄始终可用；零高度情况下使用上方固定偏移。
            FGEPoint2D rotate=length<1e-9?new FGEPoint2D(top.X,top.Y-28):
                top+direction*(28/length);
            result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.Rotate,-1,rotate));
            return result.AsReadOnly();
        }


        /// <summary>编辑视图使用互斥的控制柄组；旧Handles(item)仅为已有SDK/测试保留。</summary>
        public static IReadOnlyList<MVIVisualHandle> Handles(MVISceneItem item,bool geometryEditing,double zoom)
        {
            if(item==null)throw new ArgumentNullException("item");
            if(double.IsNaN(zoom)||double.IsInfinity(zoom)||zoom<=0)
                throw new ArgumentOutOfRangeException("zoom");
            var result=new List<MVIVisualHandle>();
            if(!item.IsSelectable)return result.AsReadOnly();
            if(geometryEditing)
            {
                foreach(MVIVisualHandle h in Handles(item))
                    if(h.Kind!=MVIVisualHandleKind.Resize&&h.Kind!=MVIVisualHandleKind.Rotate)
                        result.Add(h);
                return result.AsReadOnly();
            }
            // Eight local bounds handles, transformed by the same matrix as the shape.
            double w=item.LocalWidth,visualHeight=item.LocalHeight;
            double[] xs={0,w/2,w,w,w,w/2,0,0};
            double[] ys={0,0,0,visualHeight/2,visualHeight,visualHeight,visualHeight,visualHeight/2};
            FGETransform2D transform=item.WorldTransform.ToGeometry();
            for(int i=0;i<8;i++)
                result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.Resize,i,
                    transform.TransformPoint(new FGEPoint2D(xs[i],ys[i]))));
            FGEPoint2D corner=transform.TransformPoint(new FGEPoint2D(w,0));
            FGEPoint2D center=transform.TransformPoint(new FGEPoint2D(w/2,visualHeight/2));
            double vx=corner.X-center.X,vy=corner.Y-center.Y;
            double length=Math.Sqrt(vx*vx+vy*vy);
            if(length<1e-8){vx=1;vy=-1;length=Math.Sqrt(2);}
            double offset=28/zoom;
            result.Add(new MVIVisualHandle(item.Id,MVIVisualHandleKind.Rotate,-1,
                new FGEPoint2D(corner.X+vx*offset/length,corner.Y+vy*offset/length)));
            return result.AsReadOnly();
        }

        public static MVIVisualHandle HitHandle(MVISceneItem item,double x,double y,
            double radius,bool geometryEditing,double zoom)
        {
            if(radius<=0||double.IsInfinity(radius)||double.IsNaN(radius))
                throw new ArgumentOutOfRangeException("radius");
            MVIVisualHandle closest=null;
            double distanceMin=radius*radius;
            foreach(MVIVisualHandle h in Handles(item,geometryEditing,zoom))
            {
                double dx=h.WorldPoint.X-x,dy=h.WorldPoint.Y-y;
                double distance=dx*dx+dy*dy;
                if(distance<=distanceMin){closest=h;distanceMin=distance;}
            }
            return closest;
        }

        /// <summary>计算图元实时变换预览；不修改工程Revision。</summary>
        public static MVISceneItem PreviewTransform(MVISceneItem projected,
            MVIVisualHandle handle,double worldX,double worldY)
        {
            if(projected==null||handle==null)throw new ArgumentNullException(
                projected==null?"projected":"handle");
            if(projected.Id!=handle.ElementId||!projected.IsSelectable)
                throw new InvalidOperationException("visual.preview.target_mismatch");
            FGEValidation.Finite(worldX,"worldX");FGEValidation.Finite(worldY,"worldY");
            if(handle.Kind==MVIVisualHandleKind.Resize&&
                handle.CommandIndex>=0&&handle.CommandIndex<=7)
            {
                double localX,localY,width,height;
                ResizeCoordinates(projected,handle.CommandIndex,worldX,worldY,
                    out localX,out localY,out width,out height);
                MRCTransform world=projected.WorldTransform;
                MRCTransform shifted=new MRCTransform(world.M11,world.M12,
                    world.M21,world.M22,
                    world.X+world.M11*localX+world.M12*localY,
                    world.Y+world.M21*localX+world.M22*localY);
                return projected.WithVisualLayout(shifted,width,height);
            }
            if(handle.Kind==MVIVisualHandleKind.Rotate)
            {
                FGETransform2D parent=projected.ParentWorldTransform.ToGeometry();
                MRCTransform local=MRCTransform.FromGeometry(parent.Inverse()
                    .Compose(projected.WorldTransform.ToGeometry()));
                MRCTransform rotated=RotateFromHandle(local,parent,
                    projected.LocalWidth,projected.LocalHeight,handle,worldX,worldY);
                return projected.WithVisualLayout(
                    MRCTransform.FromGeometry(parent.Compose(rotated.ToGeometry())),
                    projected.LocalWidth,projected.LocalHeight);
            }
            throw new ArgumentException("visual.preview.unsupported_handle","handle");
        }

        /// <summary>预览与提交共用同一局部几何计算，支持旋转/嵌套Group。</summary>
        private static void ResizeCoordinates(MVISceneItem projected,int position,
            double worldX,double worldY,out double localX,out double localY,
            out double width,out double height)
        {
            if(position<0||position>7)throw new ArgumentOutOfRangeException("position");
            FGEPoint2D local=projected.WorldTransform.ToGeometry().Inverse()
                .TransformPoint(new FGEPoint2D(worldX,worldY));
            double oldW=projected.LocalWidth,oldH=projected.LocalHeight;
            bool left=position==0||position==6||position==7;
            bool right=position==2||position==3||position==4;
            bool top=position==0||position==1||position==2;
            bool bottom=position==4||position==5||position==6;
            double x0=left?Math.Min(local.X,oldW-1):0;
            double x1=right?Math.Max(local.X,1):oldW;
            double y0=top?Math.Min(local.Y,oldH-1):0;
            double y1=bottom?Math.Max(local.Y,1):oldH;
            width=x1-x0;height=y1-y0;
            if(projected.Shape!=null&&projected.Shape.Kind==MRCShapeKind.Circle)
            {
                double size=(left||right)&&(top||bottom)?
                    Math.Max(width,height):(left||right?width:height);
                if(left)x0=x1-size;
                else if(!right)x0=(oldW-size)/2;
                if(top)y0=y1-size;
                else if(!bottom)y0=(oldH-size)/2;
                width=size;height=size;
            }
            localX=x0;localY=y0;
        }

        private static MRCTransform ResizeFromHandle(MRCVisualItem item,
            MVISceneItem projected,int position,double worldX,double worldY,
            out double width,out double height)
        {
            double localX,localY;
            ResizeCoordinates(projected,position,worldX,worldY,
                out localX,out localY,out width,out height);
            MRCTransform previous=item.Transform;
            return new MRCTransform(previous.M11,previous.M12,previous.M21,previous.M22,
                previous.X+previous.M11*localX+previous.M12*localY,
                previous.Y+previous.M21*localX+previous.M22*localY);
        }

        private static MRCTransform RotateFromHandle(MRCTransform local,
            FGETransform2D parent,double width,double height,MVIVisualHandle handle,
            double worldX,double worldY)
        {
            FGETransform2D inverse=parent.Inverse();
            FGEPoint2D target=inverse.TransformPoint(new FGEPoint2D(worldX,worldY));
            FGEPoint2D initial=inverse.TransformPoint(handle.WorldPoint);
            FGEPoint2D center=local.ToGeometry().TransformPoint(
                new FGEPoint2D(width/2,height/2));
            double a0=Math.Atan2(initial.Y-center.Y,initial.X-center.X);
            double a1=Math.Atan2(target.Y-center.Y,target.X-center.X);
            double radians=a1-a0,c=Math.Cos(radians),s=Math.Sin(radians);
            double m11=c*local.M11-s*local.M21,m12=c*local.M12-s*local.M22;
            double m21=s*local.M11+c*local.M21,m22=s*local.M12+c*local.M22;
            double x=center.X-m11*width/2-m12*height/2;
            double y=center.Y-m21*width/2-m22*height/2;
            return new MRCTransform(m11,m12,m21,m22,x,y);
        }

        /// <summary>
        /// 命中可分割的线/二次/三次曲线段，返回原参数空间t。所有距离均在
        /// 世界坐标中计算（WPF按DIP/Zoom给定半径），保持旋转与非均匀缩放准确。
        /// 曲线采样仅用于寻找命中参数；最终工程分割仍由FGE de Casteljau精确执行。
        /// </summary>
        public static bool TryHitPathSplitSegment(MVISceneItem item,
            double worldX,double worldY,double worldTolerance,
            out int commandIndex,out double fraction)
        {
            commandIndex=-1;fraction=0;
            if(item==null||item.Shape==null||!item.IsSelectable||
                double.IsNaN(worldX)||double.IsNaN(worldY)||
                double.IsInfinity(worldX)||double.IsInfinity(worldY)||
                double.IsNaN(worldTolerance)||double.IsInfinity(worldTolerance)||
                worldTolerance<=0)return false;
            MRCShapeKind kind=item.Shape.Kind;
            if(kind!=MRCShapeKind.Path&&kind!=MRCShapeKind.Bezier&&
                kind!=MRCShapeKind.Polyline&&kind!=MRCShapeKind.Polygon)return false;
            FGETransform2D world=item.GeometryToWorld;
            var commands=item.Shape.Geometry.Commands;
            FGEPoint2D start=new FGEPoint2D(0,0),previous=start;
            var target=new FGEPoint2D(worldX,worldY);
            double toleranceSq=worldTolerance*worldTolerance;
            double nearest=toleranceSq;
            for(int i=0;i<commands.Count;i++)
            {
                FGEPathCommand c=commands[i];
                if(c.Verb==FGEPathVerb.Move)
                {previous=world.TransformPoint(c.End);start=previous;continue;}
                if(c.Verb==FGEPathVerb.Close){previous=start;continue;}
                if(c.Verb!=FGEPathVerb.Line&&c.Verb!=FGEPathVerb.Quadratic&&
                    c.Verb!=FGEPathVerb.Cubic)
                {previous=world.TransformPoint(c.End);continue;}
                FGEPoint2D end=world.TransformPoint(c.End);
                FGEPoint2D control1=c.Verb==FGEPathVerb.Line?previous:
                    world.TransformPoint(c.Control1);
                FGEPoint2D control2=c.Verb==FGEPathVerb.Cubic?
                    world.TransformPoint(c.Control2):end;
                // Bezier凸包给出保守世界AABB，远处的段无需做曲线最近点搜索。
                double minX=Math.Min(Math.Min(previous.X,end.X),Math.Min(control1.X,control2.X));
                double maxX=Math.Max(Math.Max(previous.X,end.X),Math.Max(control1.X,control2.X));
                double minY=Math.Min(Math.Min(previous.Y,end.Y),Math.Min(control1.Y,control2.Y));
                double maxY=Math.Max(Math.Max(previous.Y,end.Y),Math.Max(control1.Y,control2.Y));
                if(worldX<minX-worldTolerance||worldX>maxX+worldTolerance||
                    worldY<minY-worldTolerance||worldY>maxY+worldTolerance)
                {previous=end;continue;}
                double candidate=0,distance=double.PositiveInfinity;
                if(c.Verb==FGEPathVerb.Line)
                {
                    double dx=end.X-previous.X,dy=end.Y-previous.Y;
                    double len=dx*dx+dy*dy;
                    if(len>1e-20)
                    {
                        candidate=Math.Max(0,Math.Min(1,
                            ((worldX-previous.X)*dx+(worldY-previous.Y)*dy)/len));
                        FGEPoint2D position=CurvePoint(previous,control1,control2,end,
                            FGEPathVerb.Line,candidate);
                        distance=DistanceSquared(target,position);
                    }
                }
                else
                {
                    // 有界世界空间搜索：64个参数采样定位谷值，20次三分收敛。
                    // 允许曲线回环，多局部极小按最近距离而非按第一个命中返回。
                    const int count=64;
                    for(int k=0;k<=count;k++)
                    {
                        double t=(double)k/count;
                        double d=DistanceSquared(target,CurvePoint(previous,control1,
                            control2,end,c.Verb,t));
                        if(d>=distance)continue;
                        double left=Math.Max(0,(double)(k-1)/count);
                        double right=Math.Min(1,(double)(k+1)/count);
                        for(int j=0;j<20;j++)
                        {
                            double a=left+(right-left)/3,b=right-(right-left)/3;
                            double da=DistanceSquared(target,CurvePoint(previous,
                                control1,control2,end,c.Verb,a));
                            double db=DistanceSquared(target,CurvePoint(previous,
                                control1,control2,end,c.Verb,b));
                            if(da<db)right=b;else left=a;
                        }
                        double refined=(left+right)/2;
                        double rd=DistanceSquared(target,CurvePoint(previous,control1,
                            control2,end,c.Verb,refined));
                        if(rd<distance){distance=rd;candidate=refined;}
                        else if(d<distance){distance=d;candidate=t;}
                    }
                }
                if(candidate>1e-6&&candidate<1-1e-6&&distance<=nearest)
                {nearest=distance;commandIndex=i;fraction=candidate;}
                previous=end;
            }
            return commandIndex>=0;
        }
        private static double DistanceSquared(FGEPoint2D a,FGEPoint2D b)
        {double x=a.X-b.X,y=a.Y-b.Y;return x*x+y*y;}
        private static FGEPoint2D CurvePoint(FGEPoint2D p,FGEPoint2D c1,
            FGEPoint2D c2,FGEPoint2D end,FGEPathVerb verb,double t)
        {
            double u=1-t;
            if(verb==FGEPathVerb.Line)return new FGEPoint2D(u*p.X+t*end.X,u*p.Y+t*end.Y);
            if(verb==FGEPathVerb.Quadratic)return new FGEPoint2D(
                u*u*p.X+2*u*t*c1.X+t*t*end.X,
                u*u*p.Y+2*u*t*c1.Y+t*t*end.Y);
            double a=u*u*u,b=3*u*u*t,c=3*u*t*t,d=t*t*t;
            return new FGEPoint2D(a*p.X+b*c1.X+c*c2.X+d*end.X,
                a*p.Y+b*c1.Y+c*c2.Y+d*end.Y);
        }

        public static MVIVisualHandle HitHandle(MVISceneItem item,double x,double y,double radius=8)
        {
            if(radius<=0||double.IsInfinity(radius)||double.IsNaN(radius))
                throw new ArgumentOutOfRangeException("radius");
            IReadOnlyList<MVIVisualHandle> handles=Handles(item);
            MVIVisualHandle closest=null;double min=radius*radius;
            foreach(MVIVisualHandle h in handles)
            {
                double dx=h.WorldPoint.X-x,dy=h.WorldPoint.Y-y;
                double distance=dx*dx+dy*dy;
                if(distance<=min){min=distance;closest=h;}
            }
            return closest;
        }

        public static MTCEditResult<bool> Edit(MVIVisualEditor editor,MVIVisualHandle handle,
            double worldX,double worldY)
        {
            if(editor==null)throw new ArgumentNullException("editor");
            if(handle==null)throw new ArgumentNullException("handle");
            FGEValidation.Finite(worldX,"worldX");FGEValidation.Finite(worldY,"worldY");
            int revision=editor.Document.Revision;
            MRCVisualItem item=editor.Document.Find(handle.ElementId) as MRCVisualItem;
            if(item==null)
                return MTCEditResult<bool>.Rejected(revision,"visual.definition_required",
                    "Open the Symbol definition before editing its geometry");
            MVISceneItem projected=null;
            foreach(MVISceneItem scene in editor.Scene)
                if(scene.Id==handle.ElementId){projected=scene;break;}
            if(projected==null||!projected.IsSelectable)
                return MTCEditResult<bool>.Rejected(revision,"visual.not_editable","Selected visual item is hidden or locked");

            if(handle.Kind==MVIVisualHandleKind.Resize)
            {
                if(handle.CommandIndex>=0&&handle.CommandIndex<=7)
                {
                    double w,h;
                    MRCTransform moved=ResizeFromHandle(item,projected,handle.CommandIndex,
                        worldX,worldY,out w,out h);
                    return editor.Document.Edit("八点缩放图元",revision,e=>
                    {
                        e.Resize(item,w,h);
                        e.SetTransform(item,moved);
                        return true;
                    });
                }
                if(handle.CommandIndex!=-1)
                    return MTCEditResult<bool>.Rejected(revision,"visual.resize.invalid_handle",
                        "Unsupported resize handle");
                // 兼容旧版本SDK：右下外侧唯一缩放柄偏移(12,12)。
                FGETransform2D inv=item.Transform.ToGeometry().Inverse();
                FGETransform2D parent=projected.WorldTransform.ToGeometry()
                    .Compose(item.Transform.ToGeometry().Inverse());
                FGEPoint2D point=parent.Inverse().TransformPoint(
                    new FGEPoint2D(worldX-12,worldY-12));
                FGEPoint2D local=inv.TransformPoint(point);
                double legacyW=Math.Max(1,local.X),legacyH=Math.Max(1,local.Y);
                return editor.Document.Edit("缩放可视图元",revision,e=>
                {e.Resize(item,legacyW,legacyH);return true;});
            }
            if(handle.Kind==MVIVisualHandleKind.Rotate)
            {
                FGETransform2D parent=projected.WorldTransform.ToGeometry()
                    .Compose(item.Transform.ToGeometry().Inverse());
                MRCTransform rotated=RotateFromHandle(item.Transform,parent,
                    item.Width,item.Height,handle,worldX,worldY);
                return editor.Document.Edit("旋转可视图元",revision,e=>
                {e.SetTransform(item,rotated);return true;});
            }

            var shape=item as MRCShapeElement;
            if(shape==null||projected.Shape==null)
                return MTCEditResult<bool>.Rejected(revision,"visual.path_required","Only Shape paths expose geometry handles");
            if(handle.Kind>=MVIVisualHandleKind.ArcCenter)
            {
                FGEPath2D path=shape.Shape.Geometry;
                if(handle.CommandIndex<0||handle.CommandIndex>=path.Commands.Count||
                    path.Commands[handle.CommandIndex].Verb!=FGEPathVerb.Arc)
                    return MTCEditResult<bool>.Rejected(revision,"visual.arc.invalid_handle","Arc not found");
                FGEArc2D arc=path.Commands[handle.CommandIndex].Arc;
                FGEPoint2D destination=projected.GeometryToWorld.Inverse().TransformPoint(
                    new FGEPoint2D(worldX,worldY));
                FGEArc2D changed=EditArc(arc,handle.Kind,destination,shape.Shape.Kind);
                FGEPath2D arcPath=FGEPathEditing.UpdateArc(path,handle.CommandIndex,changed);
                return CommitGeometry(editor,shape,arcPath);
            }
            if(handle.Kind!=MVIVisualHandleKind.Anchor&&
                handle.Kind!=MVIVisualHandleKind.Control1&&
                handle.Kind!=MVIVisualHandleKind.Control2)
                return MTCEditResult<bool>.Rejected(revision,"visual.path.invalid_handle","Unsupported handle");
            FGEPathHandleKind kind=handle.Kind==MVIVisualHandleKind.Anchor?
                FGEPathHandleKind.Anchor:handle.Kind==MVIVisualHandleKind.Control1?
                FGEPathHandleKind.Control1:FGEPathHandleKind.Control2;
            FGEPoint2D pointOnPath=projected.GeometryToWorld.Inverse().TransformPoint(
                new FGEPoint2D(worldX,worldY));
            FGEPath2D oldPath=shape.Shape.Geometry;
            FGEPath2D newPath=FGEPathEditing.MoveHandle(oldPath,handle.CommandIndex,kind,pointOnPath);
            return CommitGeometry(editor,shape,newPath);
        }

        private static FGEArc2D EditArc(FGEArc2D arc,MVIVisualHandleKind handle,
            FGEPoint2D position,MRCShapeKind shape)
        {
            double centerX=arc.Center.X,centerY=arc.Center.Y;
            double rx=arc.RadiusX,ry=arc.RadiusY,start=arc.StartRadians,sweep=arc.SweepRadians;
            double cos=Math.Cos(arc.RotationRadians),sin=Math.Sin(arc.RotationRadians);
            double dx=position.X-centerX,dy=position.Y-centerY;
            double localX=cos*dx+sin*dy,localY=-sin*dx+cos*dy;
            switch(handle)
            {
                case MVIVisualHandleKind.ArcCenter:
                    centerX=position.X;centerY=position.Y;break;
                case MVIVisualHandleKind.ArcRadiusX:
                    rx=Math.Max(0.01,Math.Abs(localX));
                    if(shape==MRCShapeKind.Circle)ry=rx;
                    break;
                case MVIVisualHandleKind.ArcRadiusY:
                    ry=Math.Max(0.01,Math.Abs(localY));break;
                case MVIVisualHandleKind.ArcStart:
                    {
                        double end=start+sweep;
                        double begin=Math.Atan2(localY/ry,localX/rx);
                        double change=NormalizeAngle(end-begin);
                        if(sweep<0&&change>0)change-=Math.PI*2;
                        if(sweep>0&&change<0)change+=Math.PI*2;
                        start=begin;
                        sweep=change;
                        break;
                    }
                case MVIVisualHandleKind.ArcEnd:
                    {
                        double end=Math.Atan2(localY/ry,localX/rx);
                        double change=NormalizeAngle(end-start);
                        if(sweep<0&&change>0)change-=Math.PI*2;
                        if(sweep>0&&change<0)change+=Math.PI*2;
                        sweep=change;
                        break;
                    }
                default:throw new ArgumentException("visual.arc.handle_unsupported");
            }
            return new FGEArc2D(new FGEPoint2D(centerX,centerY),rx,ry,start,
                sweep,arc.RotationRadians);
        }
        private static double NormalizeAngle(double a)
        {
            double two=Math.PI*2;
            while(a>Math.PI)a-=two;
            while(a< -Math.PI)a+=two;
            return a;
        }

        /// <summary>修改任意路径命令后，重算布局并保留未修改端点世界坐标。</summary>
        public static MTCEditResult<bool> CommitGeometry(MVIVisualEditor editor,
            MRCShapeElement item,FGEPath2D newPath)
        {
            if(editor==null||item==null||newPath==null)throw new ArgumentNullException(
                editor==null?"editor":item==null?"item":"newPath");
            int revision=editor.Document.Revision;
            FGEPath2D oldPath=item.Shape.Geometry;
            FGERect2D oldBounds=oldPath.Bounds,newBounds=newPath.Bounds;
            double ratioX=oldBounds.Width==0?1:item.Width/oldBounds.Width;
            double ratioY=oldBounds.Height==0?1:item.Height/oldBounds.Height;
            double width=Math.Max(1,newBounds.Width*ratioX);
            double height=Math.Max(1,newBounds.Height*ratioY);
            double localShiftX=(newBounds.Left-oldBounds.Left)*ratioX;
            double localShiftY=(newBounds.Top-oldBounds.Top)*ratioY;
            MRCTransform original=item.Transform;
            var compensated=new MRCTransform(original.M11,original.M12,original.M21,original.M22,
                original.X+original.M11*localShiftX+original.M12*localShiftY,
                original.Y+original.M21*localShiftX+original.M22*localShiftY);
            // 三次状态写入属于一次工程事务，同一次Undo严格恢复Shape/布局/世界坐标。
            var updated=new MRCShapeDefinition(item.Shape.Kind,newPath);
            return editor.Document.Edit("编辑路径几何",revision,e=>
            {
                e.SetShape(item,updated);
                e.Resize(item,width,height);
                e.SetTransform(item,compensated);
                return true;
            });
        }
    }
    /// <summary>
    /// 多选共享世界变换：画布控制柄、实时预览与最终MEM提交均使用此计算，
    /// 不在WPF内另存一套缩放/旋转规则。变换直接作用于每个对象的世界矩阵。
    /// </summary>
    public static class MVISelectionTransform
    {
        public static FGERect2D Bounds(IEnumerable<MVISceneItem> items)
        {
            if(items==null)throw new ArgumentNullException("items");
            MVISceneItem[] selected=items.Where(x=>x!=null&&x.IsSelectable).ToArray();
            if(selected.Length==0)throw new ArgumentException("visual.selection.empty","items");
            return new FGERect2D(selected.Min(x=>x.Bounds.Left),
                selected.Min(x=>x.Bounds.Top),
                selected.Max(x=>x.Bounds.Right)-selected.Min(x=>x.Bounds.Left),
                selected.Max(x=>x.Bounds.Bottom)-selected.Min(x=>x.Bounds.Top));
        }
        public static FGEPoint2D Handle(FGERect2D box,int index,double zoom)
        {
            if(index<0||index>8||zoom<=0||double.IsNaN(zoom)||double.IsInfinity(zoom))
                throw new ArgumentOutOfRangeException("zoom");
            double[] xs={box.Left,(box.Left+box.Right)/2,box.Right,box.Right,
                box.Right,(box.Left+box.Right)/2,box.Left,box.Left};
            double[] ys={box.Top,box.Top,box.Top,(box.Top+box.Bottom)/2,
                box.Bottom,box.Bottom,box.Bottom,(box.Top+box.Bottom)/2};
            if(index==8)return new FGEPoint2D(box.Right+28/zoom,box.Top-28/zoom);
            return new FGEPoint2D(xs[index],ys[index]);
        }
        /// <summary>
        /// P1：共用选区在世界坐标中缩放或旋转。Shift等比缩放/15度旋转吸附，
        /// Alt绕选区中心缩放；反向拖动不得隐式镜像或制造奇异矩阵。
        /// 预览和最终MEM事务调用同一函数，消除视觉与工程结果的分歧。
        /// </summary>
        public static MRCTransform Delta(FGERect2D box,int index,
            FGEPoint2D point,double zoom,bool keepAspect=false,
            bool resizeFromCenter=false,bool snapRotation=false)
        {
            if(index<0||index>8||zoom<=0||double.IsNaN(zoom)||double.IsInfinity(zoom))
                throw new ArgumentOutOfRangeException("zoom");
            FGEValidation.Finite(point.X,"x");FGEValidation.Finite(point.Y,"y");
            double cx=(box.Left+box.Right)/2,cy=(box.Top+box.Bottom)/2;
            if(index==8)
            {
                FGEPoint2D original=Handle(box,8,zoom);
                double angle=Math.Atan2(point.Y-cy,point.X-cx)-
                    Math.Atan2(original.Y-cy,original.X-cx);
                if(snapRotation)
                {
                    // 按鼠标相对初始旋转柄的角度增量吸附，而不是覆盖对象已有角度。
                    double step=Math.PI/12;
                    angle=Math.Round(angle/step)*step;
                }
                double c=Math.Cos(angle),s=Math.Sin(angle);
                return new MRCTransform(c,-s,s,c,cx-c*cx+s*cy,cy-s*cx-c*cy);
            }
            bool left=index==0||index==6||index==7;
            bool right=index==2||index==3||index==4;
            bool top=index==0||index==1||index==2;
            bool bottom=index==4||index==5||index==6;
            bool horizontal=left||right,vertical=top||bottom;
            // 边中点等比缩放：只取当前拖动轴的倍率，非拖动轴绕自身中心伸展。
            double px=resizeFromCenter?cx:left?box.Right:right?box.Left:cx;
            double py=resizeFromCenter?cy:top?box.Bottom:bottom?box.Top:cy;
            FGEPoint2D start=Handle(box,index,zoom);
            double dx=start.X-px,dy=start.Y-py;
            double sx=horizontal&&Math.Abs(dx)>1e-9?
                Math.Max(.01,(point.X-px)/dx):1;
            double sy=vertical&&Math.Abs(dy)>1e-9?
                Math.Max(.01,(point.Y-py)/dy):1;
            if(keepAspect)
            {
                double scale=horizontal&&!vertical?sx:
                    vertical&&!horizontal?sy:
                    Math.Abs(sx-1)>=Math.Abs(sy-1)?sx:sy;
                // 选区某一轴宽高为0时，禁止该轴驱动等比奇异变换。
                if((horizontal&&Math.Abs(dx)<1e-9)||
                   (vertical&&Math.Abs(dy)<1e-9))scale=1;
                sx=scale;sy=scale;
            }
            return new MRCTransform(sx,0,0,sy,px*(1-sx),py*(1-sy));
        }
    }

}
