using System;
using System.Collections.Generic;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;

namespace CxVisual.Mechanism.Visual
{
    public enum MVIVisualHandleKind { Anchor=1, Control1=2, Control2=3, Resize=4, Rotate=5 }

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
        public static IReadOnlyList<MVIVisualHandle> Handles(CxSceneItem item)
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

        public static MVIVisualHandle HitHandle(CxSceneItem item,double x,double y,double radius=8)
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

        public static MTCEditResult<bool> Edit(CxVisualEditor editor,MVIVisualHandle handle,
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
            CxSceneItem projected=null;
            foreach(CxSceneItem scene in editor.Scene)
                if(scene.Id==handle.ElementId){projected=scene;break;}
            if(projected==null||!projected.IsSelectable)
                return MTCEditResult<bool>.Rejected(revision,"visual.not_editable","Selected visual item is hidden or locked");

            if(handle.Kind==MVIVisualHandleKind.Resize)
            {
                FGETransform2D inv=item.Transform.ToGeometry().Inverse();
                // Canvas world -> current parent -> element local, never use AABB to resize a rotated shape.
                FGETransform2D parent=projected.WorldTransform.ToGeometry()
                    .Compose(item.Transform.ToGeometry().Inverse());
                FGEPoint2D point=parent.Inverse().TransformPoint(
                    new FGEPoint2D(worldX-12,worldY-12));
                FGEPoint2D local=inv.TransformPoint(point);
                double w=Math.Max(1,local.X),h=Math.Max(1,local.Y);
                return editor.Document.Edit("缩放可视图元",revision,e=>
                {e.Resize(item,w,h);return true;});
            }
            if(handle.Kind==MVIVisualHandleKind.Rotate)
            {
                FGETransform2D parent=projected.WorldTransform.ToGeometry()
                    .Compose(item.Transform.ToGeometry().Inverse());
                FGETransform2D inverse=parent.Inverse();
                FGEPoint2D worldTarget=new FGEPoint2D(worldX,worldY);
                FGEPoint2D target=inverse.TransformPoint(worldTarget);
                FGEPoint2D initial=inverse.TransformPoint(handle.WorldPoint);
                FGEPoint2D center=item.Transform.ToGeometry().TransformPoint(
                    new FGEPoint2D(item.Width/2,item.Height/2));
                double a0=Math.Atan2(initial.Y-center.Y,initial.X-center.X);
                double a1=Math.Atan2(target.Y-center.Y,target.X-center.X);
                double radians=a1-a0,c=Math.Cos(radians),s=Math.Sin(radians);
                MRCTransform m=item.Transform;
                double m11=c*m.M11-s*m.M21,m12=c*m.M12-s*m.M22;
                double m21=s*m.M11+c*m.M21,m22=s*m.M12+c*m.M22;
                double x=center.X-m11*item.Width/2-m12*item.Height/2;
                double y=center.Y-m21*item.Width/2-m22*item.Height/2;
                MRCTransform rotated=new MRCTransform(m11,m12,m21,m22,x,y);
                return editor.Document.Edit("旋转可视图元",revision,e=>
                {e.SetTransform(item,rotated);return true;});
            }

            var shape=item as MRCShapeElement;
            if(shape!=null&&(shape.Shape.Kind==MRCShapeKind.Arc||
                shape.Shape.Kind==MRCShapeKind.Circle||shape.Shape.Kind==MRCShapeKind.Ellipse))
                return MTCEditResult<bool>.Rejected(revision,"visual.arc_parameter_required",
                    "Ellipse/arc control points are derived; use resize until arc parameter handles are implemented");
            if(shape==null||projected.Shape==null)
                return MTCEditResult<bool>.Rejected(revision,"visual.path_required","Only Shape paths expose geometry handles");
            FGEPathHandleKind kind=handle.Kind==MVIVisualHandleKind.Anchor?
                FGEPathHandleKind.Anchor:handle.Kind==MVIVisualHandleKind.Control1?
                FGEPathHandleKind.Control1:FGEPathHandleKind.Control2;
            FGEPoint2D pointOnPath=projected.GeometryToWorld.Inverse().TransformPoint(
                new FGEPoint2D(worldX,worldY));
            FGEPath2D oldPath=shape.Shape.Geometry;
            FGEPath2D newPath=FGEPathEditing.MoveHandle(oldPath,handle.CommandIndex,kind,pointOnPath);
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
            var updated=new MRCShapeDefinition(shape.Shape.Kind,newPath);
            return editor.Document.Edit("修改路径控制点",revision,e=>
            {
                e.SetShape(shape,updated);
                e.Resize(shape,width,height);
                e.SetTransform(shape,compensated);
                return true;
            });
        }
    }
}
