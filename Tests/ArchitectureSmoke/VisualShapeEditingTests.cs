using System;
using System.IO;
using System.Linq;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>路径编辑必须保留其余点的世界坐标，使用原工程Undo和真正的磁盘往返。</summary>
    internal static class VisualShapeEditingTests
    {
        private static void Check(bool valid,string context)
        {if(!valid)throw new InvalidOperationException("Visual editing A: "+context);}
        private static bool Near(FGEPoint2D a,FGEPoint2D b,double eps=1e-6)
        {return a.DistanceTo(b)<eps;}
        public static void Run()
        {
            var plain=new FGEPath2D(new[]{
                FGEPathCommand.Move(new FGEPoint2D(0,0)),
                FGEPathCommand.Quadratic(new FGEPoint2D(10,25),new FGEPoint2D(40,10)),
                FGEPathCommand.Cubic(new FGEPoint2D(55,75),
                    new FGEPoint2D(85,60),new FGEPoint2D(110,15))});
            var modified=FGEPathEditing.MoveHandle(plain,2,FGEPathHandleKind.Control2,
                new FGEPoint2D(95,100));
            Check(modified.Commands[2].Control2.Equals(new FGEPoint2D(95,100)),
                "control2 was not changed precisely");
            Check(plain.Commands[2].Control2.Equals(new FGEPoint2D(85,60)),
                "immutable original path mutated");
            Check(modified.Commands[2].End.Equals(plain.Commands[2].End),
                "editing a handle moved the endpoint");
            bool rejected=false;
            try{FGEPathEditing.MoveHandle(plain,1,FGEPathHandleKind.Control2,new FGEPoint2D(1,1));}
            catch(ArgumentException){rejected=true;}
            Check(rejected,"invalid control handle should be rejected");
            var circlePath=FGEPath2D.Circle(new FGEPoint2D(20,20),20);
            rejected=false;
            try{FGEPathEditing.MoveHandle(circlePath,0,FGEPathHandleKind.Anchor,
                new FGEPoint2D(50,50));}
            catch(ArgumentException){rejected=true;}
            Check(rejected,"arc start should stay constrained to exact center/radius/angle");

            string filename=Path.Combine(Path.GetTempPath(),"cxvisual-edit-a-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                var doc=MEMEngineering.Create("Geometry Editing A");
                var page=doc.AddPage("Drawing");
                var group=doc.AddGroup(page.RootContent,"Rotated Group");
                double rg=Math.PI/9;
                doc.SetTransform(group,new MRCTransform(Math.Cos(rg),-Math.Sin(rg),
                    Math.Sin(rg),Math.Cos(rg),75,65));
                var shape=doc.AddShape(group,"Bezier",MRCShapeDefinition.Path(plain),32,27);
                double angle=Math.PI/7;
                doc.SetTransform(shape,new MRCTransform(Math.Cos(angle),-Math.Sin(angle),
                    Math.Sin(angle),Math.Cos(angle),32,27));
                doc.ClearHistory();

                Guid id=shape.Id;
                using(var editor=new CxVisualEditor(doc))
                {
                    CxSceneItem projected=editor.Scene.Single(x=>x.Id==id);
                    var handles=MVIVisualEditing.Handles(projected);
                    Check(handles.Any(x=>x.Kind==MVIVisualHandleKind.Anchor)&&
                          handles.Any(x=>x.Kind==MVIVisualHandleKind.Control1)&&
                          handles.Any(x=>x.Kind==MVIVisualHandleKind.Control2)&&
                          handles.Any(x=>x.Kind==MVIVisualHandleKind.Rotate)&&
                          handles.Any(x=>x.Kind==MVIVisualHandleKind.Resize),
                        "handles not discoverable in rotated group");
                    var selected=handles.Single(x=>x.Kind==MVIVisualHandleKind.Control1&&x.CommandIndex==2);
                    Check(MVIVisualEditing.HitHandle(projected,
                        selected.WorldPoint.X,selected.WorldPoint.Y).Kind==MVIVisualHandleKind.Control1,
                        "handle hit test incorrect");
                    FGEPoint2D otherBefore=projected.GeometryToWorld.TransformPoint(
                        plain.Commands[2].End);
                    FGEPoint2D target=new FGEPoint2D(selected.WorldPoint.X+27,
                        selected.WorldPoint.Y-31);
                    int baseRevision=doc.Revision;
                    EventHandler<MVIInteractionEventArgs> deny=(sender,args)=>
                    {
                        if(args.Kind==MVIInteractionKind.Move&&
                            args.Phase==MVIEventPhase.Target)args.PreventDefault();
                    };
                    editor.Interaction+=deny;
                    Check(!editor.EditHandle(selected,target.X,target.Y).Success&&
                        doc.Revision==baseRevision,
                        "capture/target route veto must block handle edit without committing");
                    editor.Interaction-=deny;
                    Check(editor.EditHandle(selected,target.X,target.Y).Success,"control handle edit failed");
                    Check(doc.Revision==baseRevision+1,"edit should be a single MEM transaction");
                    CxSceneItem edited=editor.Scene.Single(x=>x.Id==id);
                    FGEPoint2D controlAfter=edited.GeometryToWorld.TransformPoint(
                        shape.Shape.Geometry.Commands[2].Control1);
                    FGEPoint2D otherAfter=edited.GeometryToWorld.TransformPoint(
                        shape.Shape.Geometry.Commands[2].End);
                    Check(Near(controlAfter,target),"drag target not reflected in world coordinates");
                    Check(Near(otherAfter,otherBefore),
                        "moving control changed unrelated Bezier endpoint");
                    Check(editor.Undo().Success,"path undo failed");
                    Check(shape.Shape.Geometry.Commands[2].Control1.Equals(plain.Commands[2].Control1),
                        "undo did not restore original exact path");
                    Check(editor.Redo().Success,"path redo failed");
                    Check(Near(editor.Scene.Single(x=>x.Id==id).GeometryToWorld.TransformPoint(
                        shape.Shape.Geometry.Commands[2].Control1),target),"redo did not restore geometry");

                    // 缩放：鼠标传入真正世界位置，允许对象处于两级旋转父子变换中。
                    edited=editor.Scene.Single(x=>x.Id==id);
                    var resize=MVIVisualEditing.Handles(edited).Single(x=>x.Kind==MVIVisualHandleKind.Resize);
                    double expectedWidth=shape.Width+55,expectedHeight=shape.Height+25;
                    FGEPoint2D bottom=edited.WorldTransform.ToGeometry().TransformPoint(
                        new FGEPoint2D(expectedWidth,expectedHeight));
                    Check(editor.EditHandle(resize,bottom.X+12,bottom.Y+12).Success,"resize failed");
                    Check(Math.Abs(shape.Width-expectedWidth)<1e-6&&
                        Math.Abs(shape.Height-expectedHeight)<1e-6,"resize wrong local dimensions");

                    // 旋转：绕世界中心且保留原来由Group带来的位置，不漂移。
                    edited=editor.Scene.Single(x=>x.Id==id);
                    FGEPoint2D center=edited.WorldTransform.ToGeometry().TransformPoint(
                        new FGEPoint2D(shape.Width/2,shape.Height/2));
                    var rotate=MVIVisualEditing.Handles(edited).Single(x=>x.Kind==MVIVisualHandleKind.Rotate);
                    double dx=rotate.WorldPoint.X-center.X,dy=rotate.WorldPoint.Y-center.Y;
                    Check(editor.EditHandle(rotate,center.X-dy,center.Y+dx).Success,"rotate failed");
                    FGEPoint2D centerAfter=editor.Scene.Single(x=>x.Id==id).WorldTransform
                        .ToGeometry().TransformPoint(new FGEPoint2D(shape.Width/2,shape.Height/2));
                    Check(Near(centerAfter,center),"rotation moved the visual center");
                    Check(editor.Undo().Success&&editor.Undo().Success&&editor.Undo().Success,
                        "three independent edits not undone");
                    Check(shape.Shape.Geometry.Commands[2].Control1.Equals(plain.Commands[2].Control1),
                        "undo stack did not restore original Path");
                    Check(editor.Redo().Success&&editor.Redo().Success&&editor.Redo().Success,
                        "redo stack did not replay precise Path + resize + rotate");
                    doc.SaveAs(filename);
                }
                doc.Close();
                using(var restored=MEMEngineering.Open(filename))
                {
                    var shapeBack=restored.Find(id) as MRCShapeElement;
                    Check(shapeBack!=null&&Near(shapeBack.Shape.Geometry.Commands[2].Control1,
                        shape.Shape.Geometry.Commands[2].Control1),"path lost precision on save/reopen");
                    Check(Math.Abs(shapeBack.Width-shape.Width)<1e-6&&
                        Math.Abs(shapeBack.Rotation-shape.Rotation)<1e-6,
                        "layout transform not persisted");
                }
                Console.WriteLine("PASS Visual editing A: quadratic/cubic handles, nested transforms, world geometry stability, resize/rotate, 1-step Undo, Redo and disk reopen");
            }
            finally{if(File.Exists(filename))File.Delete(filename);}
        }
    }
}
