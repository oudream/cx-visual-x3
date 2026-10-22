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
        private static void BatchVisualMoveTests()
        {
            string path=Path.Combine(Path.GetTempPath(),
                "cxvisual-batch-move-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                using(var doc=MEMEngineering.Create("A 多选拖动"))
                {
                    var page=doc.AddPage("Page");
                    var firstGroup=doc.AddGroup(page.RootContent,"G1");
                    var secondGroup=doc.AddGroup(page.RootContent,"G2");
                    double rotation=Math.PI/6;
                    doc.SetTransform(firstGroup,new MRCTransform(1,0,0,1,15,25));
                    doc.SetTransform(secondGroup,new MRCTransform(Math.Cos(rotation),
                        -Math.Sin(rotation),Math.Sin(rotation),Math.Cos(rotation),90,45));
                    var a=doc.AddRectangle(firstGroup,"A",5,7,35,25);
                    var b=doc.AddRectangle(secondGroup,"B",15,20,40,30);
                    var c=doc.AddRectangle(firstGroup,"C",60,5,30,20);
                    doc.ClearHistory();
                    using(var editor=new MVIVisualEditor(doc))
                    {
                        Check(firstGroup.Children.First().Id==a.Id,
                            "A initial draw order unexpectedly changed");
                        Check(editor.SetZOrder(a.Id,true).Success&&
                            firstGroup.Children.Last().Id==a.Id,
                            "A bring-to-front failed to reorder siblings");
                        Check(editor.Undo().Success&&firstGroup.Children.First().Id==a.Id,
                            "A z-order Undo failed");
                        Check(editor.Redo().Success&&firstGroup.Children.Last().Id==a.Id,
                            "A z-order Redo failed");
                        Check(editor.SetZOrder(a.Id,false).Success&&
                            firstGroup.Children.First().Id==a.Id,
                            "A send-to-back failed");
                        doc.ClearHistory();
                        var originA=editor.Scene.Single(x=>x.Id==a.Id).Bounds;
                        var originB=editor.Scene.Single(x=>x.Id==b.Id).Bounds;
                        int originalRevision=doc.Revision;
                        Check(editor.MoveSelection(new[]{a.Id,b.Id},48,27).Success,
                            "multi selection drag rejected two different parents");
                        var movedA=editor.Scene.Single(x=>x.Id==a.Id).Bounds;
                        var movedB=editor.Scene.Single(x=>x.Id==b.Id).Bounds;
                        Check(Math.Abs(movedA.Left-originA.Left-48)<1e-5&&
                            Math.Abs(movedA.Top-originA.Top-27)<1e-5&&
                            Math.Abs(movedB.Left-originB.Left-48)<1e-5&&
                            Math.Abs(movedB.Top-originB.Top-27)<1e-5&&
                            doc.Revision==originalRevision+1,
                            "multi select drag lost world coordinates or was not one transaction");
                        Check(editor.Undo().Success&&
                            Math.Abs(editor.Scene.Single(x=>x.Id==a.Id).Bounds.Left-originA.Left)<1e-5&&
                            Math.Abs(editor.Scene.Single(x=>x.Id==b.Id).Bounds.Top-originB.Top)<1e-5,
                            "multi select must restore all items with one Undo");
                        Check(editor.Redo().Success,"multi select Redo rejected");
                        doc.SaveAs(path);
                    }
                }
                using(var reopened=MEMEngineering.Open(path))
                {
                    var scene=MVISceneProjection.Project(reopened);
                    Check(scene.Count>=2,"batch move save/open lost the shapes");
                }
            }
            finally
            {
                if(File.Exists(path))File.Delete(path);
                if(File.Exists(path+".cx-lock"))File.Delete(path+".cx-lock");
            }
        }
        public static void Run()
        {
            BatchVisualMoveTests();
            var plain=new FGEPath2D(new[]{
                FGEPathCommand.Move(new FGEPoint2D(0,0)),
                FGEPathCommand.Quadratic(new FGEPoint2D(10,25),new FGEPoint2D(40,10)),
                FGEPathCommand.Cubic(new FGEPoint2D(55,75),
                    new FGEPoint2D(85,60),new FGEPoint2D(110,15))});
            // de Casteljau 在任意t拆分曲线必须保持曲线原形，而非离散近似。
            var split=FGEPathEditing.SplitSegment(plain,2,0.35);
            Check(split.Commands.Count==4&&split.Commands[2].Verb==FGEPathVerb.Cubic,
                "Bezier exact split did not create two cubic segments");
            FGEBezier2D originalCurve=new FGEBezier2D(plain.Commands[1].End,
                plain.Commands[2].Control1,plain.Commands[2].Control2,plain.Commands[2].End);
            FGEBezier2D firstCurve=new FGEBezier2D(split.Commands[1].End,
                split.Commands[2].Control1,split.Commands[2].Control2,split.Commands[2].End);
            FGEBezier2D secondCurve=new FGEBezier2D(split.Commands[2].End,
                split.Commands[3].Control1,split.Commands[3].Control2,split.Commands[3].End);
            for(int step=0;step<=20;step++)
            {
                double t=step/20.0;
                FGEPoint2D expected=originalCurve.PointAt(t);
                FGEPoint2D actual=t<=0.35?firstCurve.PointAt(t/0.35):
                    secondCurve.PointAt((t-0.35)/0.65);
                Check(Near(expected,actual),"de Casteljau changed the original Bezier shape");
            }
            // 二次曲线升阶为三次Bezier必须精确保形（含非直线控制点）。
            var elevated=FGEPathEditing.ConvertSegment(plain,1,FGEPathVerb.Cubic);
            Check(elevated.Commands[1].Verb==FGEPathVerb.Cubic,
                "quadratic degree elevation didn't create a cubic");
            for(int step=0;step<=30;step++)
            {
                double f=step/30.0,u=1-f;
                FGEPoint2D from=plain.Commands[0].End;
                FGEPoint2D control=plain.Commands[1].Control1;
                FGEPoint2D end=plain.Commands[1].End;
                var expected=new FGEPoint2D(
                    u*u*from.X+2*u*f*control.X+f*f*end.X,
                    u*u*from.Y+2*u*f*control.Y+f*f*end.Y);
                var curve=new FGEBezier2D(from,elevated.Commands[1].Control1,
                    elevated.Commands[1].Control2,elevated.Commands[1].End);
                Check(Near(curve.PointAt(f),expected),
                    "quadratic-to-cubic conversion changed the curve");
            }
            // 第二轮廓的闭合与断开不能触及第一轮廓，反向操作恢复原定义。
            var contours=new FGEPath2D(new[]{
                FGEPathCommand.Move(new FGEPoint2D(0,0)),
                FGEPathCommand.Line(new FGEPoint2D(20,0)),
                FGEPathCommand.Line(new FGEPoint2D(20,20)),
                FGEPathCommand.Close(),
                FGEPathCommand.Move(new FGEPoint2D(100,100)),
                FGEPathCommand.Cubic(new FGEPoint2D(105,70),
                    new FGEPoint2D(130,140),new FGEPoint2D(140,120)),
                FGEPathCommand.Line(new FGEPoint2D(150,130))});
            var closed=FGEPathEditing.SetContourClosed(contours,5,true);
            Check(closed.Commands.Count==8&&closed.Commands[3].Verb==FGEPathVerb.Close&&
                closed.Commands[7].Verb==FGEPathVerb.Close&&
                closed.FlattenContours().Count==2,
                "closing the second contour damaged the first");
            var opened=FGEPathEditing.SetContourClosed(closed,6,false);
            Check(opened.Commands.Count==contours.Commands.Count&&
                opened.Commands[5].Control1.Equals(contours.Commands[5].Control1)&&
                opened.Commands[3].Verb==FGEPathVerb.Close,
                "reopening contour did not preserve other geometry");
            Check(object.ReferenceEquals(contours,
                FGEPathEditing.SetContourClosed(contours,5,false)),
                "unchanged open contour should not allocate or commit");
            var converted=FGEPathEditing.ConvertSegment(plain,1,FGEPathVerb.Line);
            Check(converted.Commands[1].Verb==FGEPathVerb.Line&&
                converted.Commands[1].End.Equals(plain.Commands[1].End),
                "segment conversion altered endpoint");
            var deletion=FGEPathEditing.DeleteAnchor(converted,1);
            Check(deletion.Commands.Count==2&&deletion.Commands[1].Verb==FGEPathVerb.Cubic,
                "line vertex deletion damaged remaining contour");
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
            FGEArc2D updatedArc=new FGEArc2D(new FGEPoint2D(24,25),30,16,0.4,2.3);
            FGEPath2D revisedArc=FGEPathEditing.UpdateArc(
                FGEPath2D.Arc(new FGEArc2D(new FGEPoint2D(24,25),20,14,0,2)),1,updatedArc);
            Check(Near(revisedArc.Commands[0].End,updatedArc.PointAt(0))&&
                Near(revisedArc.Commands[1].End,updatedArc.PointAt(1)),
                "Arc start/end points did not derive from exact parameters");

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
                using(var editor=new MVIVisualEditor(doc))
                {
                    MVISceneItem projected=editor.Scene.Single(x=>x.Id==id);
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
                    MVISceneItem edited=editor.Scene.Single(x=>x.Id==id);
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

                    // UI变换态必须恰好八点缩放+右上旋转，内部几何态不得混入变换柄。
                    var objectHandles=MVIVisualEditing.Handles(edited,false,1);
                    Check(objectHandles.Count(h=>h.Kind==MVIVisualHandleKind.Resize)==8&&
                        objectHandles.Count(h=>h.Kind==MVIVisualHandleKind.Rotate)==1&&
                        !objectHandles.Any(h=>h.Kind==MVIVisualHandleKind.Anchor),
                        "transform mode must show eight resize and one rotate only");
                    var geometryHandles=MVIVisualEditing.Handles(edited,true,1);
                    Check(geometryHandles.Any(h=>h.Kind==MVIVisualHandleKind.Anchor)&&
                        !geometryHandles.Any(h=>h.Kind==MVIVisualHandleKind.Resize||
                            h.Kind==MVIVisualHandleKind.Rotate),
                        "geometry mode must not show object transform handles");
                    Check(objectHandles.Where(h=>h.Kind==MVIVisualHandleKind.Resize)
                        .Select(h=>h.CommandIndex).Distinct().Count()==8,
                        "eight resize handles must have distinct semantic positions");

                    // 左中缩放柄按旋转后的本地X轴拖动，固定右侧世界位置，并可单步撤销。
                    var leftHandle=objectHandles.Single(x=>
                        x.Kind==MVIVisualHandleKind.Resize&&x.CommandIndex==7);
                    double oldWidth=shape.Width;
                    FGEPoint2D rightBefore=edited.WorldTransform.ToGeometry()
                        .TransformPoint(new FGEPoint2D(shape.Width,shape.Height/2));
                    FGEPoint2D leftTarget=edited.WorldTransform.ToGeometry()
                        .TransformPoint(new FGEPoint2D(-15,shape.Height/2));
                    int beforeResizeRevision=doc.Revision;
                    MVISceneItem leftPreview=MVIVisualEditing.PreviewTransform(
                        edited,leftHandle,leftTarget.X,leftTarget.Y);
                    Check(doc.Revision==beforeResizeRevision&&
                        Math.Abs(shape.Width-oldWidth)<1e-6,
                        "preview must not mutate the engineering definition or revision");
                    Check(Near(leftPreview.WorldTransform.ToGeometry().TransformPoint(
                        new FGEPoint2D(leftPreview.LocalWidth,leftPreview.LocalHeight/2)),
                        rightBefore),"left resize preview moved opposite anchor");
                    Check(editor.EditHandle(leftHandle,leftTarget.X,leftTarget.Y).Success,
                        "left-middle resize handle rejected");
                    Check(Math.Abs(shape.Width-(oldWidth+15))<1e-5&&
                        doc.Revision==beforeResizeRevision+1,
                        "left-middle resize did not update width in one MEM transaction");
                    MVISceneItem leftResized=editor.Scene.Single(x=>x.Id==shape.Id);
                    FGEPoint2D rightAfter=leftResized.WorldTransform.ToGeometry()
                        .TransformPoint(new FGEPoint2D(shape.Width,shape.Height/2));
                    Check(Near(rightBefore,rightAfter),
                        "left-edge drag moved the opposite world anchor");
                    Check(Math.Abs(leftPreview.LocalWidth-leftResized.LocalWidth)<1e-5&&
                        Near(leftPreview.TopLeft,leftResized.TopLeft)&&
                        Near(leftPreview.BottomRight,leftResized.BottomRight),
                        "final resize differs from preview");
                    Check(editor.Undo().Success&&Math.Abs(shape.Width-oldWidth)<1e-5,
                        "left-edge resize should be undone in one step");

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
                    MVISceneItem rotatedPreview=MVIVisualEditing.PreviewTransform(edited,rotate,
                        center.X-dy,center.Y+dx);
                    int beforeRotateRevision=doc.Revision;
                    Check(editor.EditHandle(rotate,center.X-dy,center.Y+dx).Success,"rotate failed");
                    MVISceneItem rotatedScene=editor.Scene.Single(x=>x.Id==id);
                    Check(doc.Revision==beforeRotateRevision+1&&
                        Near(rotatedPreview.TopLeft,rotatedScene.TopLeft)&&
                        Near(rotatedPreview.BottomRight,rotatedScene.BottomRight),
                        "rotation preview and final commit must be identical");
                    FGEPoint2D centerAfter=editor.Scene.Single(x=>x.Id==id).WorldTransform
                        .ToGeometry().TransformPoint(new FGEPoint2D(shape.Width/2,shape.Height/2));
                    Check(Near(centerAfter,center),"rotation moved the visual center");
                    Check(editor.Undo().Success&&editor.Undo().Success&&editor.Undo().Success,
                        "three independent edits not undone");
                    Check(shape.Shape.Geometry.Commands[2].Control1.Equals(plain.Commands[2].Control1),
                        "undo stack did not restore original Path");
                    Check(editor.Redo().Success&&editor.Redo().Success&&editor.Redo().Success,
                        "redo stack did not replay precise Path + resize + rotate");
                    // Arc半径/端点编辑采用精确FGEArc2D而非折线重建。
                    FGEArc2D originalArc=new FGEArc2D(new FGEPoint2D(60,60),
                        42,27,0,Math.PI);
                    var arcShape=doc.AddShape(group,"Arc",MRCShapeDefinition.Arc(originalArc),
                        160,140);
                    var arcItem=editor.Scene.Single(x=>x.Id==arcShape.Id);
                    var radiusHandle=MVIVisualEditing.Handles(arcItem).First(x=>
                        x.Kind==MVIVisualHandleKind.ArcRadiusX);
                    FGEPoint2D radiusTarget=arcItem.GeometryToWorld.TransformPoint(
                        new FGEPoint2D(originalArc.Center.X+52,originalArc.Center.Y));
                    Check(editor.EditHandle(radiusHandle,radiusTarget.X,radiusTarget.Y).Success,
                        "Arc radius handle edit failed");
                    Check(Math.Abs(arcShape.Shape.Geometry.Commands[1].Arc.RadiusX-52)<1e-5,
                        "Arc radius did not update exact parameter");
                    Check(editor.Undo().Success&&
                        Math.Abs(arcShape.Shape.Geometry.Commands[1].Arc.RadiusX-42)<1e-5,
                        "Arc radius undo did not restore exact parameter");
                    Check(editor.Redo().Success,"Arc radius redo failed");

                    // 多层旋转组合中的两个图元对齐，必须使用世界坐标而非局部X。
                    var neighbor=doc.AddRectangle(group,"Neighbor",330,140,50,40);
                    MVISceneItem beforeAlignA=editor.Scene.Single(x=>x.Id==shape.Id);
                    MVISceneItem beforeAlignB=editor.Scene.Single(x=>x.Id==neighbor.Id);
                    Check(editor.Align(new[]{shape.Id,neighbor.Id},"left").Success,
                        "world-space multi-selection alignment failed");
                    MVISceneItem afterAlignA=editor.Scene.Single(x=>x.Id==shape.Id);
                    MVISceneItem afterAlignB=editor.Scene.Single(x=>x.Id==neighbor.Id);
                    Check(Math.Abs(afterAlignA.Bounds.Left-afterAlignB.Bounds.Left)<1e-6,
                        "alignment did not match left edge in world coordinates");
                    Check(editor.Undo().Success,"alignment undo failed");
                    Check(Math.Abs(editor.Scene.Single(x=>x.Id==neighbor.Id).Bounds.Left-
                        beforeAlignB.Bounds.Left)<1e-6,"alignment Undo did not restore original");
                    Check(editor.Redo().Success,"alignment redo failed");
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
