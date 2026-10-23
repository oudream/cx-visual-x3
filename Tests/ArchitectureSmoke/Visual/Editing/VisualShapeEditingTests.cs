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
        private static FGEPoint2D[] RouteVertices(MVISceneItem line)
        {
            return line.Shape.Geometry.Commands.Select(command=>
                line.GeometryToWorld.TransformPoint(command.End)).ToArray();
        }

        private static bool RouteAvoids(FGEPoint2D[] route,FGERect2D shapeBounds)
        {
            // 8个世界单位是路由目标；这里保守检查7.5单位的实体间距。
            FGERect2D obstacle=shapeBounds.Inflate(7.5);
            for(int i=1;i<route.Length;i++)
            {
                FGEPoint2D a=route[i-1],b=route[i];
                if(Math.Abs(a.X-b.X)<1e-6)
                {
                    if(a.X>obstacle.Left&&a.X<obstacle.Right&&
                        Math.Min(a.Y,b.Y)<obstacle.Bottom&&
                        Math.Max(a.Y,b.Y)>obstacle.Top)return false;
                }
                else if(Math.Abs(a.Y-b.Y)<1e-6)
                {
                    if(a.Y>obstacle.Top&&a.Y<obstacle.Bottom&&
                        Math.Min(a.X,b.X)<obstacle.Right&&
                        Math.Max(a.X,b.X)>obstacle.Left)return false;
                }
                else return false;
            }
            return true;
        }

        /// <summary>绕开实体、障碍移动自动重排、Undo/Redo及持久化均依赖同一个MPC身份。</summary>
        private static void ObstacleRoutingTests()
        {
            string file=Path.Combine(Path.GetTempPath(),
                "cxvisual-route-obstacle-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                using(var doc=MEMEngineering.Create("Obstacle routing"))
                {
                    try
                    {
                    var page=doc.AddPage("Page");
                    var source=doc.AddRectangle(page.RootContent,"Source",0,20,40,40);
                    var target=doc.AddRectangle(page.RootContent,"Target",260,20,40,40);
                    var blocker=doc.AddRectangle(page.RootContent,"Obstacle",125,5,50,70);
                    using(var editor=new MVIVisualEditor(doc))
                    {
                        editor.SetPage(page.Id);
                        Check(editor.ConnectVisual(source.Id,"east",target.Id,"west").Success,
                            "visual connection cannot be created for obstacle routing");
                        Guid wire=editor.Scene.Single(item=>
                            item.ElementTypeId=="core.visual.connector").Id;
                        int before=doc.Revision;
                        Check(editor.SetVisualRoute(wire,MPCVisualRouteKind.Orthogonal).Success&&
                            doc.Revision==before+1,
                            "obstacle route mode should commit one MEM transaction");
                        MVISceneItem obstacle=editor.Scene.Single(x=>x.Id==blocker.Id);
                        FGEPoint2D[] route=RouteVertices(editor.Scene.Single(x=>x.Id==wire));
                        Check(route.Length>=4&&RouteAvoids(route,obstacle.Bounds),
                            "orthogonal connector must detour around occupied body with clearance");
                        Check(Near(route[0],MVIVisualAnchors.At(
                            editor.Scene.Single(x=>x.Id==source.Id),"east"))&&
                            Near(route[route.Length-1],MVIVisualAnchors.At(
                                editor.Scene.Single(x=>x.Id==target.Id),"west")),
                            "obstacle detour changed persisted port anchors");
                        // P0：画面上真正的自动避障折线必须能按任意可见线段索引插点，
                        // 而不是编辑器内部重新计算出的简化三段直线。
                        int insertion=route.Length-2;
                        FGEPoint2D targetPoint=new FGEPoint2D(
                            (route[insertion].X+route[insertion+1].X)/2,
                            (route[insertion].Y+route[insertion+1].Y)/2);
                        int beforeInsert=doc.Revision;
                        Check(editor.InsertVisualWaypoint(wire,insertion,
                            targetPoint.X,targetPoint.Y).Success&&
                            doc.Revision==beforeInsert+1,
                            "P0 visible auto-routed last segment cannot be edited");
                        MPCConnection manual=doc.FindConnection(wire);
                        Check(manual.VisualRouteKind==MPCVisualRouteKind.Manual&&
                            manual.VisualWaypoints.Count==route.Length-1,
                            "P0 conversion to manual route lost visible bend points");
                        FGEPoint2D[] afterInsert=RouteVertices(editor.Scene.Single(x=>x.Id==wire));
                        Check(afterInsert.Length==route.Length+1&&
                            Near(afterInsert[insertion+1],targetPoint),
                            "P0 clicked visible segment created the waypoint at wrong position");
                        Check(editor.Undo().Success&&
                            doc.FindConnection(wire).VisualRouteKind==
                                MPCVisualRouteKind.Orthogonal,
                            "P0 auto-route conversion Undo did not restore original strategy");
                        Check(editor.Redo().Success&&
                            doc.FindConnection(wire).VisualWaypoints.Count==route.Length-1,
                            "P0 auto-route conversion Redo lost the visible route");
                        Check(editor.Undo().Success,
                            "P0 failed to restore orthogonal mode before dynamic route tests");
                        int routeRevision=doc.Revision;
                        FGEPoint2D[] repeat=RouteVertices(editor.Scene.Single(x=>x.Id==wire));
                        Check(repeat.Length==route.Length&&
                            Enumerable.Range(0,route.Length).All(i=>Near(route[i],repeat[i]))&&
                            doc.Revision==routeRevision,
                            "re-projection must be deterministic and read-only");
                        doc.Move(blocker,125,170);
                        FGEPoint2D[] cleared=RouteVertices(editor.Scene.Single(x=>x.Id==wire));
                        Check(cleared.Length==2&&doc.FindConnection(wire).VisualRouteKind==
                            MPCVisualRouteKind.Orthogonal,
                            "moving blocker away should automatically straighten the same connection");
                        Check(editor.Undo().Success&&
                            RouteVertices(editor.Scene.Single(x=>x.Id==wire)).Length>=4,
                            "obstacle move Undo failed to re-route");
                        Check(editor.Redo().Success&&
                            RouteVertices(editor.Scene.Single(x=>x.Id==wire)).Length==2,
                            "obstacle move Redo failed to re-route");
                        // P0：障碍恰好覆盖源端口出口时允许显示退化线路，
                        // 但必须有可查询诊断，且不能污染MEM Revision或持久化连接。
                        var crowded=doc.AddRectangle(page.RootContent,"Blocked exit",38,32,30,16);
                        MVISceneItem conflict=editor.Scene.Single(x=>x.Id==wire);
                        Check(conflict.VisualRouteDiagnostic=="visual.route.obstacle_collision",
                            "P0 collision fallback was silently displayed as successful routing");
                        int diagnosticRevision=doc.Revision;
                        Check(editor.Scene.Single(x=>x.Id==wire).VisualRouteDiagnostic!=null&&
                            doc.Revision==diagnosticRevision,
                            "P0 routing diagnostics must be read-only scene metadata");
                        doc.Move(crowded,80,190);
                        Check(editor.Scene.Single(x=>x.Id==wire).VisualRouteDiagnostic==null,
                            "P0 route diagnostic did not clear after obstacle moved away");
                        Check(editor.Undo().Success&&
                            editor.Scene.Single(x=>x.Id==wire).VisualRouteDiagnostic!=null,
                            "P0 diagnostic must follow MEM undo of obstacle position");
                        Check(editor.Redo().Success&&
                            editor.Scene.Single(x=>x.Id==wire).VisualRouteDiagnostic==null,
                            "P0 diagnostic must follow MEM redo");
                        doc.SaveAs(file);
                    }
                    doc.Close();
                    }
                    catch
                    {
                        // 测试中途断言失败时显式放弃临时工程，保留原异常堆栈。
                        doc.Close(true);
                        throw;
                    }
                }
                using(var reopened=MEMEngineering.Open(file))
                {
                    var wire=reopened.GetSnapshot().Nodes.SelectMany(n=>n.Connections)
                        .Single(x=>x.VisualRouteKind==MPCVisualRouteKind.Orthogonal);
                    Check(RouteVertices(MVISceneProjection.Project(reopened).Single(x=>
                        x.Id==wire.Id)).Length==2,
                        "save/reopen must recompute orthogonal path from live geometry");
                }
            }
            finally
            {
                if(File.Exists(file))File.Delete(file);
                if(File.Exists(file+".cx-lock"))File.Delete(file+".cx-lock");
            }
        }

        /// <summary>
        /// 多障碍物及固定端口方向验证：东侧输出必须向东离开源，
        /// 东侧输入则须从目标东侧进入，线路不能为了求最短路穿过端点所属图元。
        /// 同时检查旋转图元的世界法线占优方向。
        /// </summary>
        private static void DirectionalMultiObstacleRoutingTests()
        {
            using(var doc=MEMEngineering.Create("Directional multi-obstacle routing"))
            {
                try
                {
                    MEMPage page=doc.AddPage("Ports");
                    var source=doc.AddRectangle(page.RootContent,"Source",0,20,40,40);
                    var target=doc.AddRectangle(page.RootContent,"Target",240,20,40,40);
                    var blocker1=doc.AddRectangle(page.RootContent,"Blocker1",85,10,35,65);
                    var blocker2=doc.AddRectangle(page.RootContent,"Blocker2",143,0,33,80);
                    var blocker3=doc.AddRectangle(page.RootContent,"Blocker3",193,15,30,65);
                    using(var editor=new MVIVisualEditor(doc))
                    {
                        editor.SetPage(page.Id);
                        Check(editor.ConnectVisual(source.Id,"east",target.Id,"east").Success,
                            "east-to-east fixed port connection failed");
                        Guid wire=editor.Scene.Single(x=>x.ElementTypeId==
                            "core.visual.connector").Id;
                        int before=doc.Revision;
                        Check(editor.SetVisualRoute(wire,MPCVisualRouteKind.Orthogonal).Success&&
                            doc.Revision==before+1,
                            "directional route must commit a single MEM transaction");
                        FGEPoint2D[] points=RouteVertices(editor.Scene.Single(x=>x.Id==wire));
                        Check(points.Length>=4&&
                            Near(points[0],MVIVisualAnchors.At(
                                editor.Scene.Single(x=>x.Id==source.Id),"east"))&&
                            Near(points[points.Length-1],MVIVisualAnchors.At(
                                editor.Scene.Single(x=>x.Id==target.Id),"east")),
                            "auto route lost real fixed source or target port");
                        Check(points[1].X>points[0].X+7&&
                            Math.Abs(points[1].Y-points[0].Y)<1e-6,
                            "east source port did not initially route outward");
                        Check(points[points.Length-2].X>points[points.Length-1].X+7&&
                            Math.Abs(points[points.Length-2].Y-
                                points[points.Length-1].Y)<1e-6,
                            "east target port must be approached from outside its right side");
                        Check(RouteAvoids(points,editor.Scene.Single(x=>x.Id==blocker1.Id).Bounds)&&
                            RouteAvoids(points,editor.Scene.Single(x=>x.Id==blocker2.Id).Bounds)&&
                            RouteAvoids(points,editor.Scene.Single(x=>x.Id==blocker3.Id).Bounds),
                            "multi-obstacle route intersects expanded visual obstacle");

                        // 改变中间图元的位置只影响下一次场景投影，不改变连接身份/Revision。
                        int revision=doc.Revision;
                        var original=RouteVertices(editor.Scene.Single(x=>x.Id==wire));
                        Check(doc.FindConnection(wire).VisualRouteKind==
                            MPCVisualRouteKind.Orthogonal,
                            "orthogonal route unexpectedly stored manual points");
                        Check(original.Length==RouteVertices(editor.Scene.Single(x=>x.Id==wire)).Length&&
                            doc.Revision==revision,
                            "repeated route projection mutated engineering revision");

                        var rotated=doc.AddRectangle(page.RootContent,"Rotated",20,230,50,30);
                        var destination=doc.AddRectangle(page.RootContent,"Destination",270,250,40,40);
                        double angle=Math.PI/3;
                        doc.SetTransform(rotated,new MRCTransform(Math.Cos(angle),
                            -Math.Sin(angle),Math.Sin(angle),Math.Cos(angle),20,230));
                        Check(editor.ConnectVisual(rotated.Id,"east",
                            destination.Id,"west").Success,
                            "rotated shape connector was not accepted");
                        Guid rotationWire=editor.Scene.Where(x=>x.ElementTypeId==
                            "core.visual.connector").Select(x=>x.Id)
                            .Single(x=>x!=wire);
                        Check(editor.SetVisualRoute(rotationWire,
                            MPCVisualRouteKind.Orthogonal).Success,
                            "rotated connector routing rejected");
                        FGEPoint2D[] rotatedPath=RouteVertices(editor.Scene.Single(x=>
                            x.Id==rotationWire));
                        FGEPoint2D normal=MVIVisualAnchors.OutwardNormal(
                            editor.Scene.Single(x=>x.Id==rotated.Id),"east");
                        double dx=rotatedPath[1].X-rotatedPath[0].X;
                        double dy=rotatedPath[1].Y-rotatedPath[0].Y;
                        Check(dx*normal.X+dy*normal.Y>0&&
                            Math.Abs(dx)<1e-6&&dy>0,
                            "rotated east port must exit along positive world normal");
                    }
                    doc.Close(true);
                }
                catch
                {
                    doc.Close(true);
                    throw;
                }
            }
        }

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
                        // P0：跨不同旋转Group的两个对象共享选区缩放/旋转，
                        // 各自父矩阵回推且仅一次事务；锁定对象必须拒绝且不可局部修改。
                        MVISceneItem sceneA=editor.Scene.Single(x=>x.Id==a.Id);
                        MVISceneItem sceneB=editor.Scene.Single(x=>x.Id==b.Id);
                        FGERect2D selectedBounds=MVISelectionTransform.Bounds(
                            new[]{sceneA,sceneB});
                        int beforeScale=doc.Revision;
                        Check(editor.TransformSelection(new[]{a.Id,b.Id},4,
                            selectedBounds.Right+35,selectedBounds.Bottom+20,1).Success&&
                            doc.Revision==beforeScale+1,
                            "P0 multi-selection resize was not one MEM revision");
                        FGERect2D resizedA=editor.Scene.Single(x=>x.Id==a.Id).Bounds;
                        Check(resizedA.Width>sceneA.Bounds.Width&&
                            resizedA.Height>sceneA.Bounds.Height,
                            "P0 group child did not scale through world transform");
                        Check(editor.Undo().Success&&
                            Math.Abs(editor.Scene.Single(x=>x.Id==a.Id).Bounds.Left-
                                sceneA.Bounds.Left)<1e-5&&
                            Math.Abs(editor.Scene.Single(x=>x.Id==b.Id).Bounds.Top-
                                sceneB.Bounds.Top)<1e-5,
                            "P0 group scale Undo did not restore both objects");
                        Check(editor.Redo().Success,"P0 group scale Redo failed");
                        selectedBounds=MVISelectionTransform.Bounds(new[]{
                            editor.Scene.Single(x=>x.Id==a.Id),
                            editor.Scene.Single(x=>x.Id==b.Id)});
                        FGEPoint2D rotateHandle=MVISelectionTransform.Handle(
                            selectedBounds,8,1);
                        int beforeRotate=doc.Revision;
                        Check(editor.TransformSelection(new[]{a.Id,b.Id},8,
                            rotateHandle.X+30,rotateHandle.Y+50,1).Success&&
                            doc.Revision==beforeRotate+1,
                            "P0 multi-selection rotate must be one transaction");
                        Check(editor.Undo().Success,"P0 multi-rotation Undo failed");
                        Check(editor.Redo().Success,"P0 multi-rotation Redo failed");
                        doc.SetLocked(a,true);
                        int lockedRevision=doc.Revision;
                        Check(!editor.TransformSelection(new[]{a.Id,b.Id},4,
                            selectedBounds.Right+10,selectedBounds.Bottom+10,1).Success&&
                            doc.Revision==lockedRevision,
                            "P0 locked child must reject entire multi-transform without partial edit");
                        doc.SetLocked(a,false);
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
            ObstacleRoutingTests();
            DirectionalMultiObstacleRoutingTests();
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
                    // 真正的视觉连接使用现有MPCPortKind.Visual与MPCConnection。
                    int beforeVisualLink=doc.Revision;
                    Check(editor.ConnectVisual(shape.Id,"east",neighbor.Id,"west").Success&&
                        doc.Revision==beforeVisualLink+1,
                        "visual link should be one engineering transaction");
                    MVISceneItem linkScene=editor.Scene.Single(x=>
                        x.ElementTypeId=="core.visual.connector");
                    MVISceneItem srcScene=editor.Scene.Single(x=>x.Id==shape.Id);
                    MVISceneItem dstScene=editor.Scene.Single(x=>x.Id==neighbor.Id);
                    Check(Near(linkScene.GeometryToWorld.TransformPoint(
                            linkScene.Shape.Geometry.Commands[0].End),
                            MVIVisualAnchors.At(srcScene,"east"))&&
                        Near(linkScene.GeometryToWorld.TransformPoint(
                            linkScene.Shape.Geometry.Commands[1].End),
                            MVIVisualAnchors.At(dstScene,"west")),
                        "link endpoints should follow real source/target anchors");
                    Check(!editor.ConnectVisual(shape.Id,"east",neighbor.Id,"west").Success&&
                        doc.Revision==beforeVisualLink+1,
                        "duplicate visual link must be rejected atomically");
                    FGEPoint2D oldTarget=MVIVisualAnchors.At(dstScene,"west");
                    doc.Move(neighbor,neighbor.X+30,neighbor.Y+10);
                    MVISceneItem movedLine=editor.Scene.Single(x=>
                        x.ElementTypeId=="core.visual.connector");
                    MVISceneItem movedTarget=editor.Scene.Single(x=>x.Id==neighbor.Id);
                    Check(Near(movedLine.GeometryToWorld.TransformPoint(
                            movedLine.Shape.Geometry.Commands[1].End),
                            MVIVisualAnchors.At(movedTarget,"west"))&&
                        !Near(oldTarget,MVIVisualAnchors.At(movedTarget,"west")),
                        "nested target movement should update visual connector projection");
                    Check(editor.Undo().Success&&editor.Undo().Success&&
                        !editor.Scene.Any(x=>x.ElementTypeId=="core.visual.connector"),
                        "Undo must remove both independent move and visual connection");
                    Check(editor.Redo().Success&&editor.Redo().Success&&
                        editor.Scene.Count(x=>x.ElementTypeId=="core.visual.connector")==1,
                        "Redo should restore stable attached visual connector");
                    // 连接线具有独立可选身份，但不属于普通MRC图元；修改端点必须保留连接Id。
                    Guid visualConnectionId=editor.Scene.Single(x=>
                        x.ElementTypeId=="core.visual.connector").Id;
                    MVISceneItem selectedLine=editor.Scene.Single(x=>x.Id==visualConnectionId);
                    Check(selectedLine.IsSelectable&&
                        selectedLine.Contains(
                            (MVIVisualAnchors.At(editor.Scene.Single(x=>x.Id==shape.Id),
                                "east").X+
                             MVIVisualAnchors.At(editor.Scene.Single(x=>x.Id==neighbor.Id),
                                "west").X)/2,
                            (MVIVisualAnchors.At(editor.Scene.Single(x=>x.Id==shape.Id),
                                "east").Y+
                             MVIVisualAnchors.At(editor.Scene.Single(x=>x.Id==neighbor.Id),
                                "west").Y)/2),
                        "visual connector must be selectable by actual line stroke");
                    Check(editor.Select(visualConnectionId),
                        "visual connector cannot enter the unified selection context");
                    int revisionBeforeRewire=doc.Revision;
                    Check(editor.ReconnectVisual(visualConnectionId,false,neighbor.Id,"north").Success&&
                        doc.Revision==revisionBeforeRewire+1&&
                        doc.FindConnection(visualConnectionId)!=null,
                        "endpoint reattachment must preserve identity and use one transaction");
                    string newTargetPort=doc.FindPort(
                        doc.FindConnection(visualConnectionId).Target.PortId).MemberId;
                    Check(newTargetPort=="visual.north",
                        "reattachment did not persist chosen fixed anchor");
                    int beforeInvalidRewire=doc.Revision;
                    Check(!editor.ReconnectVisual(visualConnectionId,false,shape.Id,"west").Success&&
                        doc.Revision==beforeInvalidRewire,
                        "self-link should reject without engineering mutation");
                    Check(editor.Undo().Success&&doc.FindConnection(visualConnectionId)!=null&&
                        doc.FindPort(doc.FindConnection(visualConnectionId).Target.PortId)
                            .MemberId=="visual.west",
                        "rewire Undo failed to recover old endpoint under same connection Id");
                    Check(editor.Redo().Success&&doc.FindConnection(visualConnectionId)!=null&&
                        doc.FindPort(doc.FindConnection(visualConnectionId).Target.PortId)
                            .MemberId=="visual.north",
                        "rewire Redo failed to recover new endpoint");
                    int beforeVisualDelete=doc.Revision;
                    Check(editor.Remove(visualConnectionId).Success&&
                        doc.Revision==beforeVisualDelete+1&&
                        doc.FindConnection(visualConnectionId)==null&&
                        !editor.Scene.Any(x=>x.Id==visualConnectionId),
                        "connector deletion must update MEM and scene in one transaction");
                    Check(editor.Undo().Success&&
                        doc.FindConnection(visualConnectionId)!=null&&
                        editor.Scene.Any(x=>x.Id==visualConnectionId),
                        "connector deletion Undo must restore original stable identity");
                    // V1路径：自动正交线无需存几何；人工折点持久化为端点相对偏移。
                    int beforeRoute=doc.Revision;
                    Check(editor.SetVisualRoute(visualConnectionId,
                        MPCVisualRouteKind.Orthogonal).Success&&
                        doc.Revision==beforeRoute+1&&
                        doc.FindConnection(visualConnectionId).VisualRouteKind==
                            MPCVisualRouteKind.Orthogonal,
                        "automatic orthogonal routing must commit as one edit");
                    MVISceneItem orthogonal=editor.Scene.Single(x=>x.Id==visualConnectionId);
                    FGEPoint2D autoStart=orthogonal.GeometryToWorld.TransformPoint(
                        orthogonal.Shape.Geometry.Commands[0].End);
                    FGEPoint2D autoEnd=orthogonal.GeometryToWorld.TransformPoint(
                        orthogonal.Shape.Geometry.Commands[
                            orthogonal.Shape.Geometry.Commands.Count-1].End);
                    Check(Near(autoStart,MVIVisualAnchors.At(
                            editor.Scene.Single(x=>x.Id==shape.Id),"east"))&&
                        Near(autoEnd,MVIVisualAnchors.At(
                            editor.Scene.Single(x=>x.Id==neighbor.Id),"north")),
                        "automatic route endpoints must stay attached");
                    double bendX=(autoStart.X+autoEnd.X)/2+12;
                    double bendY=(autoStart.Y+autoEnd.Y)/2+17;
                    int beforeInsert=doc.Revision;
                    Check(editor.InsertVisualWaypoint(visualConnectionId,0,bendX,bendY).Success&&
                        doc.Revision==beforeInsert+1&&
                        doc.FindConnection(visualConnectionId).VisualRouteKind==
                            MPCVisualRouteKind.Manual,
                        "insert waypoint did not convert auto route to saved manual route");
                    MPCConnection manualLink=doc.FindConnection(visualConnectionId);
                    int waypointCount=manualLink.VisualWaypoints.Count;
                    Check(waypointCount>=1&&
                        editor.Scene.Single(x=>x.Id==visualConnectionId).Shape.Geometry
                            .Commands.Count==waypointCount+2,
                        "manual route scene has wrong number of waypoint vertices");
                    MVISceneItem manualScene=editor.Scene.Single(x=>x.Id==visualConnectionId);
                    FGEPoint2D firstWaypoint=manualScene.GeometryToWorld.TransformPoint(
                        manualScene.Shape.Geometry.Commands[1].End);
                    FGEPoint2D movePoint=new FGEPoint2D(firstWaypoint.X+13,firstWaypoint.Y-9);
                    int beforeMoveWaypoint=doc.Revision;
                    Check(editor.MoveVisualWaypoint(visualConnectionId,0,
                        movePoint.X,movePoint.Y).Success&&
                        doc.Revision==beforeMoveWaypoint+1,
                        "dragging a waypoint should create one transaction");
                    MVISceneItem adjusted=editor.Scene.Single(x=>x.Id==visualConnectionId);
                    Check(Near(adjusted.GeometryToWorld.TransformPoint(
                        adjusted.Shape.Geometry.Commands[1].End),movePoint),
                        "manual waypoint projection differs from intended world coordinate");
                    Check(editor.Undo().Success&&Near(
                        editor.Scene.Single(x=>x.Id==visualConnectionId)
                            .GeometryToWorld.TransformPoint(
                                editor.Scene.Single(x=>x.Id==visualConnectionId)
                                    .Shape.Geometry.Commands[1].End),firstWaypoint),
                        "waypoint movement Undo failed");
                    Check(editor.Redo().Success&&Near(
                        editor.Scene.Single(x=>x.Id==visualConnectionId)
                            .GeometryToWorld.TransformPoint(
                                editor.Scene.Single(x=>x.Id==visualConnectionId)
                                    .Shape.Geometry.Commands[1].End),movePoint),
                        "waypoint movement Redo failed");
                    FGEPoint2D priorEnd=MVIVisualAnchors.At(
                        editor.Scene.Single(x=>x.Id==neighbor.Id),"north");
                    doc.Move(neighbor,neighbor.X+26,neighbor.Y+18);
                    FGEPoint2D nextEnd=MVIVisualAnchors.At(
                        editor.Scene.Single(x=>x.Id==neighbor.Id),"north");
                    FGEPoint2D movedWaypoint=editor.Scene.Single(x=>
                        x.Id==visualConnectionId).GeometryToWorld.TransformPoint(
                            editor.Scene.Single(x=>x.Id==visualConnectionId)
                                .Shape.Geometry.Commands[1].End);
                    double fraction=doc.FindConnection(visualConnectionId)
                        .VisualWaypoints[0].Fraction;
                    Check(Near(movedWaypoint,new FGEPoint2D(
                        movePoint.X+(nextEnd.X-priorEnd.X)*fraction,
                        movePoint.Y+(nextEnd.Y-priorEnd.Y)*fraction)),
                        "manual waypoint failed to follow endpoint movement proportionally");
                    Check(editor.RemoveVisualWaypoint(visualConnectionId,0).Success&&
                        doc.FindConnection(visualConnectionId).VisualWaypoints.Count==
                            waypointCount-1,
                        "remove waypoint failed");
                    Check(editor.Undo().Success&&
                        doc.FindConnection(visualConnectionId).VisualWaypoints.Count==
                            waypointCount,
                        "remove waypoint Undo failed");
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
                    Check(MVISceneProjection.Project(restored).Count(x=>
                        x.ElementTypeId=="core.visual.connector")==1,
                        "visual connector not restored on disk roundtrip");
                    MPCConnection savedVisual=restored.GetSnapshot().Nodes
                        .SelectMany(n=>n.Connections).Single(c=>
                            c.VisualRouteKind==MPCVisualRouteKind.Manual);
                    Check(savedVisual.VisualWaypoints.Count>=1&&
                        MVISceneProjection.Project(restored).Single(x=>
                            x.Id==savedVisual.Id).Shape.Geometry.Commands.Count==
                                savedVisual.VisualWaypoints.Count+2,
                        "saved waypoint route was not restored from MEM project");
                }
                Console.WriteLine("PASS Visual editing A: quadratic/cubic handles, nested transforms, world geometry stability, resize/rotate, 1-step Undo, Redo and disk reopen");
            }
            finally{if(File.Exists(filename))File.Delete(filename);}
        }
    }
}
