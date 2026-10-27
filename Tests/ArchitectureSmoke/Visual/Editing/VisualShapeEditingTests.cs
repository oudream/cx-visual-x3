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
        /// <summary>
        /// 共性吸附不修改工程Revision：单选、多选、网格、世界参考线和Zoom共用一次纯计算。
        /// 特别验证预览所得位移可直接提交MEM而不再次网格量化。
        /// </summary>
        private static void CheckUnifiedSnapping()
        {
            var session=MEMEngineering.Create("图元统一吸附测试");
            try
            {
                var page=session.AddPage("P");
                var a=session.AddRectangle(page.RootContent,"A",12,16,80,40);
                var b=session.AddRectangle(page.RootContent,"B",180,16,80,40);
                var c=session.AddRectangle(page.RootContent,"C",10,220,80,40);
                var scene=MVISceneProjection.Project(session,page.Id);
                int revision=session.Revision;
                MVIVisualSnapResult guides=MVIVisualSnapping.Resolve(scene,
                    new[]{a.Id},87,0,1,false,true);
                Check(Math.Abs(guides.WorldDx-88)<1e-9&&
                    guides.GuideX.HasValue&&Math.Abs(guides.GuideX.Value-180)<1e-9,
                    "P1 smart reference line failed to align right-to-left edges");
                MVIVisualSnapResult zoom=MVIVisualSnapping.Resolve(scene,
                    new[]{a.Id},84,0,2,false,true);
                Check(Math.Abs(zoom.WorldDx-84)<1e-9&&!zoom.GuideX.HasValue,
                    "P1 reference-line tolerance must be in screen DIP, not world units");
                MVIVisualSnapResult grid=MVIVisualSnapping.Resolve(scene,
                    new[]{a.Id},11,0,1,true,false);
                Check(Math.Abs(grid.WorldDx-13)<1e-9&&
                    !grid.GuideX.HasValue,
                    "P1 grid did not snap the same world-space selection left edge");
                MVIVisualSnapResult multi=MVIVisualSnapping.Resolve(scene,
                    new[]{a.Id,c.Id},11,0,1,true,false);
                Check(Math.Abs(multi.WorldDx-15)<1e-9,
                    "P1 multi-selection grid must keep relative distances");
                MVIVisualSnapResult priority=MVIVisualSnapping.Resolve(scene,
                    new[]{a.Id},87,0,1,true,true);
                Check(Math.Abs(priority.WorldDx-88)<1e-9&&priority.GuideX.HasValue,
                    "P1 object guide must take precedence over grid on the same axis");
                Check(session.Revision==revision,
                    "P1 visual snap helper must not modify engineering revision");
            }
            finally
            {
                session.Close(true);
            }
        }

        /// <summary>不经过WPF的选区修饰键正反测试；预览与提交共用MVI变换计算。</summary>
        private static void CheckConstrainedSelectionTransform()
        {
            FGERect2D box=new FGERect2D(10,20,100,50);
            MRCTransform aspect=MVISelectionTransform.Delta(box,4,
                new FGEPoint2D(160,90),1,true,false,false);
            Check(Math.Abs(aspect.M11-1.5)<1e-9&&Math.Abs(aspect.M22-1.5)<1e-9,
                "P1 Shift corner resize did not preserve proportions");
            MRCTransform center=MVISelectionTransform.Delta(box,4,
                new FGEPoint2D(135,85),1,true,true,false);
            Check(Near(center.ToGeometry().TransformPoint(new FGEPoint2D(60,45)),
                new FGEPoint2D(60,45))&&Math.Abs(center.M11-center.M22)<1e-9,
                "P1 Alt+Shift resize must preserve center and aspect");
            FGEPoint2D h=MVISelectionTransform.Handle(box,8,1);
            double theta=17*Math.PI/180,dx=h.X-60,dy=h.Y-45;
            FGEPoint2D target=new FGEPoint2D(60+dx*Math.Cos(theta)-dy*Math.Sin(theta),
                45+dx*Math.Sin(theta)+dy*Math.Cos(theta));
            MRCTransform snapped=MVISelectionTransform.Delta(box,8,
                target,1,false,false,true);
            Check(Math.Abs(snapped.M11-Math.Cos(Math.PI/12))<1e-9&&
                Math.Abs(snapped.M21-Math.Sin(Math.PI/12))<1e-9,
                "P1 Shift rotation not snapped to 15 degrees");
            MRCTransform backwards=MVISelectionTransform.Delta(box,4,
                new FGEPoint2D(-200,-200),1,true,false,false);
            Check(backwards.M11>=.01&&backwards.M22>=.01,
                "P1 reverse drag must not silently mirror selection");
        }
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
                        MTCEditResult<bool> inserted=editor.InsertVisualWaypoint(wire,
                            insertion,targetPoint.X,targetPoint.Y);
                        Check(inserted.Success&&doc.Revision==beforeInsert+1,
                            "P0 visible auto-routed last segment cannot be edited: "+
                            inserted.ErrorCode+" "+inserted.Message);
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
                        // P1：等比缩放作用于跨Group选区，MEM提交/Undo仍只有一个事务。
                        var constrainedBox=MVISelectionTransform.Bounds(new[]{
                            editor.Scene.Single(x=>x.Id==a.Id),
                            editor.Scene.Single(x=>x.Id==b.Id)});
                        int beforeConstrained=doc.Revision;
                        Check(editor.TransformSelection(new[]{a.Id,b.Id},4,
                            constrainedBox.Right+24,constrainedBox.Bottom+10,1,
                            true,false,false).Success&&doc.Revision==beforeConstrained+1,
                            "P1 Shift multi-scale was not a single MEM transaction");
                        Check(editor.Undo().Success,"P1 Shift multi-scale Undo failed");
                        Check(editor.Redo().Success,"P1 Shift multi-scale Redo failed");
                        Check(editor.Undo().Success,"P1 Shift multi-scale cleanup failed");
                        doc.SetLocked(a,true);
                        int lockedRevision=doc.Revision;
                        Check(!editor.TransformSelection(new[]{a.Id,b.Id},4,
                            selectedBounds.Right+10,selectedBounds.Bottom+10,1).Success&&
                            doc.Revision==lockedRevision,
                            "P0 locked child must reject entire multi-transform without partial edit");
                        doc.SetLocked(a,false);
                        // P0：旋转Group中的两个对象须以选区世界坐标为基准吸附，
                        // 不能分别吸附各自的局部X/Y，否则相互距离会被改变。
                        var snapBeforeA=editor.Scene.Single(x=>x.Id==a.Id).Bounds;
                        var snapBeforeB=editor.Scene.Single(x=>x.Id==b.Id).Bounds;
                        var snapBox=MVISelectionTransform.Bounds(new[]{
                            editor.Scene.Single(x=>x.Id==a.Id),
                            editor.Scene.Single(x=>x.Id==b.Id)});
                        double expectedDx=Math.Round((snapBox.Left+33)/25)*25-snapBox.Left;
                        double expectedDy=Math.Round((snapBox.Top+19)/25)*25-snapBox.Top;
                        int beforeSnap=doc.Revision;
                        Check(editor.MoveSelection(new[]{a.Id,b.Id},33,19,true).Success&&
                            doc.Revision==beforeSnap+1,
                            "P0 world-snap group move must be one transaction");
                        var snapAfterA=editor.Scene.Single(x=>x.Id==a.Id).Bounds;
                        var snapAfterB=editor.Scene.Single(x=>x.Id==b.Id).Bounds;
                        Check(Math.Abs(snapAfterA.Left-snapBeforeA.Left-expectedDx)<1e-5&&
                            Math.Abs(snapAfterB.Left-snapBeforeB.Left-expectedDx)<1e-5&&
                            Math.Abs(snapAfterA.Top-snapBeforeA.Top-expectedDy)<1e-5&&
                            Math.Abs(snapAfterB.Top-snapBeforeB.Top-expectedDy)<1e-5,
                            "P0 rotation Group world snap changed inter-object distance");
                        Check(editor.Undo().Success&&
                            Math.Abs(editor.Scene.Single(x=>x.Id==a.Id).Bounds.Left-
                                snapBeforeA.Left)<1e-5&&
                            Math.Abs(editor.Scene.Single(x=>x.Id==b.Id).Bounds.Top-
                                snapBeforeB.Top)<1e-5,
                            "P0 world snapping must Undo both rotated children together");
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
        /// <summary>
        /// 世界Bounds边缘等间距：不同宽高图元按真实间隔排布，
        /// 一次编辑一次Undo；负间距、两个图元及无变化均保持Revision。
        /// </summary>
        private static void CheckVisualGapDistribution()
        {
            var session=MEMEngineering.Create("世界几何布局反证");
            string path=Path.Combine(Path.GetTempPath(),
                "cx-visual-gap-"+Guid.NewGuid().ToString("N")+".cxproj");
            try
            {
                var page=session.AddPage("P");
                var a=session.AddRectangle(page.RootContent,"A",10,10,20,20);
                var b=session.AddRectangle(page.RootContent,"B",95,60,45,50);
                var c=session.AddRectangle(page.RootContent,"C",230,180,70,40);
                using(var editor=new MVIVisualEditor(session))
                {
                    editor.SetPage(page.Id);
                    int revision=session.Revision;
                    Check(editor.Align(new[]{a.Id,b.Id},"distributeGapX").Success==false&&
                        session.Revision==revision,"GapX should require three objects");
                    Check(editor.Align(new[]{a.Id,b.Id,c.Id},"distributeGapX").Success&&
                        session.Revision==revision+1,
                        "gap distribution should produce one MEM transaction");
                    var ordered=editor.Scene.Where(x=>new[]{a.Id,b.Id,c.Id}.Contains(x.Id))
                        .OrderBy(x=>x.Bounds.Left).ToArray();
                    double gap1=ordered[1].Bounds.Left-ordered[0].Bounds.Right;
                    double gap2=ordered[2].Bounds.Left-ordered[1].Bounds.Right;
                    Check(Math.Abs(gap1-gap2)<1e-6&&Math.Abs(gap1-77.5)<1e-6,
                        "gap algorithm incorrectly divided origins rather than visible edges");
                    int unchanged=session.Revision;
                    Check(!editor.Align(new[]{a.Id,b.Id,c.Id},"distributeGapX").Success&&
                        session.Revision==unchanged,
                        "no-op gap distribution must not create empty Undo");
                    Check(editor.Undo().Success&&Math.Abs(b.X-95)<1e-6,
                        "gap Undo failed to restore original world position");
                    Check(editor.Redo().Success&&Math.Abs(b.X-107.5)<1e-6,
                        "gap Redo failed to restore real edge spacing");
                    Check(editor.Align(new[]{a.Id,b.Id,c.Id},"distributeGapY").Success,
                        "gapY world distribution failed");
                    var vertical=editor.Scene.Where(x=>new[]{a.Id,b.Id,c.Id}.Contains(x.Id))
                        .OrderBy(x=>x.Bounds.Top).ToArray();
                    Check(Math.Abs((vertical[1].Bounds.Top-vertical[0].Bounds.Bottom)-
                        (vertical[2].Bounds.Top-vertical[1].Bounds.Bottom))<1e-6,
                        "vertical gap sizes differ");
                    int beforeInvalid=session.Revision;
                    var overlap1=session.AddRectangle(page.RootContent,"Overlap A",5,350,100,20);
                    var overlap2=session.AddRectangle(page.RootContent,"Overlap B",35,370,100,20);
                    var overlap3=session.AddRectangle(page.RootContent,"Overlap C",65,390,100,20);
                    beforeInvalid=session.Revision;
                    Check(!editor.Align(new[]{overlap1.Id,overlap2.Id,overlap3.Id},
                            "distributeGapX").Success&&session.Revision==beforeInvalid,
                        "negative gap must reject atomically");
                    Check(editor.Align(new[]{a.Id,b.Id},"right").Success,
                        "new right-edge alignment missing");
                    MVISceneItem ar=editor.Scene.Single(x=>x.Id==a.Id);
                    MVISceneItem br=editor.Scene.Single(x=>x.Id==b.Id);
                    Check(Math.Abs(ar.Bounds.Right-br.Bounds.Right)<1e-6,
                        "right alignment did not align real world edges");
                }
                Guid projectId=session.Project.Id;Guid bId=b.Id;
                session.SaveAs(path);
                using(var loaded=MEMEngineering.Open(path))
                    Check(loaded.Project.Id==projectId&&loaded.Find(bId)!=null,
                        "advanced layout lost engineering identity on disk roundtrip");
                Console.WriteLine("PASS Visual Gap: edge spacing X/Y, no-op, negative gap, Undo/Redo, save/reopen");
            }
            finally
            {
                session.Close(true);
                if(File.Exists(path))File.Delete(path);
            }
        }

        /// <summary>
        /// GE-11：真正的对象和端口重映射。两端均在复制选区内的视觉连接
        /// 应同时克隆；跨选区连接保持原样。所有修改一次Undo，重开工程身份不丢。
        /// </summary>
        private static void CheckVisualDuplicateAndInternalConnections()
        {
            string path=Path.Combine(Path.GetTempPath(),
                "cx-visual-duplicate-"+Guid.NewGuid().ToString("N")+".cxproj");
            var session=MEMEngineering.Create("图元复制连接测试");
            try
            {
                var page=session.AddPage("P");
                var a=session.AddRectangle(page.RootContent,"A",20,20,110,75);
                var b=session.AddRectangle(page.RootContent,"B",290,25,90,95);
                var c=session.AddRectangle(page.RootContent,"Outside",460,150,70,70);
                using(var editor=new MVIVisualEditor(session))
                {
                    editor.SetPage(page.Id);
                    Check(editor.ConnectVisual(a.Id,"east",b.Id,"west").Success,
                        "internal visual connection creation failed");
                    Check(editor.ConnectVisual(a.Id,"north",c.Id,"south").Success,
                        "external visual connection creation failed");
                    MPCConnection internalWire=page.RootContent.Connections
                        .Single(w=>w.Source.Address.ObjectId==a.Id&&
                            w.Target.Address.ObjectId==b.Id);
                    MPCConnection externalWire=page.RootContent.Connections
                        .Single(w=>w.Source.Address.ObjectId==a.Id&&
                            w.Target.Address.ObjectId==c.Id);
                    var before=session.Revision;
                    MTITransferResult copied=editor.DuplicateSelection(
                        new[]{a.Id,b.Id},35,20);
                    Check(session.Revision==before+1,
                        "duplicate must produce exactly one engineering revision");
                    Check(copied.RootIds.Count==2&&copied.IdMap[a.Id]!=a.Id&&
                        copied.IdMap[b.Id]!=b.Id,"duplicate identities were not remapped");
                    var duplicateA=(MRCVisualItem)session.Find(copied.IdMap[a.Id]);
                    var duplicateB=(MRCVisualItem)session.Find(copied.IdMap[b.Id]);
                    Check(Math.Abs(duplicateA.X-(a.X+35))<1e-7&&
                        Math.Abs(duplicateB.Y-(b.Y+20))<1e-7,
                        "duplicate placement did not apply parent-local translation");
                    Check(page.RootContent.Connections.Count==3,
                        "internal visual connection was lost or external link was cloned");
                    var copiedLink=page.RootContent.Connections.Single(w=>
                        w.Source.Address.ObjectId==duplicateA.Id&&
                        w.Target.Address.ObjectId==duplicateB.Id);
                    Check(copied.IdMap.ContainsKey(internalWire.Id)&&
                        copied.IdMap[internalWire.Id]==copiedLink.Id&&
                        !copied.IdMap.ContainsKey(externalWire.Id),
                        "result identity map lacks internal MPC link or incorrectly maps boundary link");
                    Check(copiedLink.Id!=internalWire.Id&&
                        copiedLink.Source.PortId==copied.IdMap[internalWire.Source.PortId]&&
                        copiedLink.Target.PortId==copied.IdMap[internalWire.Target.PortId]&&
                        copiedLink.VisualRouteKind==internalWire.VisualRouteKind,
                        "copied connection did not preserve route and remap MPC ports");
                    Check(page.RootContent.Connections.Contains(externalWire)&&
                        session.Validate().IsValid,
                        "visual copy modified external MPC connection or corrupted engineering");
                    int failedBefore=session.Revision;
                    bool rejected=false;
                    try {editor.DuplicateSelection(new[]{a.Id,a.Id},35,20);}
                    catch(InvalidOperationException){rejected=true;}
                    Check(rejected&&session.Revision==failedBefore,
                        "duplicate invalid roots must reject without MEM history");
                    Check(editor.Undo().Success&&
                        session.Find(duplicateA.Id)==null&&
                        session.Find(duplicateB.Id)==null&&
                        page.RootContent.Connections.Count==2,
                        "duplicate Undo did not atomically restore original graph");
                    Check(editor.Redo().Success&&
                        session.Find(duplicateA.Id)!=null&&
                        page.RootContent.Connections.Count==3,
                        "duplicate Redo did not restore both nodes and internal visual link");
                    Guid copiedId=duplicateA.Id;
                    session.SaveAs(path);
                    using(var loaded=MEMEngineering.Open(path))
                    {
                        var loadedPage=loaded.Project.Pages[0];
                        Check(loaded.Find(copiedId)!=null&&
                            loadedPage.RootContent.Connections.Count==3&&
                            loaded.Validate().IsValid,
                            "duplicate did not survive persisted project roundtrip");
                    }
                }
                Console.WriteLine("PASS Visual Duplicate GE-11: MPC port remap, internal link, boundary, one Undo, disk");
            }
            finally
            {
                session.Close(true);
                if(File.Exists(path))File.Delete(path);
            }
        }

        /// <summary>
        /// GE-10/11：Group是工程容器而非Scene Shape。复制整个Group时
        /// 内部图元端口与父Content持有的视觉连线必须整体新建且能单次Undo。
        /// </summary>
        private static void CheckNestedGroupVisualDuplicate()
        {
            var session=MEMEngineering.Create("组复制连接测试");
            try
            {
                var page=session.AddPage("P");
                var a=session.AddRectangle(page.RootContent,"A",20,20,80,60);
                var b=session.AddRectangle(page.RootContent,"B",170,30,80,60);
                var group=session.Group(new MRCVisualItem[]{a,b},"原始Group");
                using(var editor=new MVIVisualEditor(session))
                {
                    editor.SetPage(page.Id);
                    Check(editor.ConnectVisual(a.Id,"east",b.Id,"west").Success,
                        "nested group internal MPC connection missing");
                    var before=session.Revision;
                    MPCConnection original=page.RootContent.Connections.Single();
                    var copied=editor.DuplicateSelection(new[]{group.Id},26,14);
                    Check(session.Revision==before+1&&
                        session.Find(copied.IdMap[group.Id]) is MRCGroup&&
                        session.Find(copied.IdMap[a.Id])!=null&&
                        session.Find(copied.IdMap[b.Id])!=null,
                        "group transfer did not clone the entire engineering subtree");
                    Check(page.RootContent.Connections.Count==2&&
                        copied.IdMap.ContainsKey(original.Id)&&
                        page.RootContent.Connections.Any(w=>
                            w.Id==copied.IdMap[original.Id]&&
                            w.Source.Address.ObjectId==copied.IdMap[a.Id]&&
                            w.Target.Address.ObjectId==copied.IdMap[b.Id]),
                        "group transfer did not clone the Content-owned internal MPC connection");
                    Check(editor.Undo().Success&&page.RootContent.Connections.Count==1&&
                        session.Find(copied.IdMap[group.Id])==null,
                        "group duplicate must have one atomic Undo");
                    Check(editor.Redo().Success&&page.RootContent.Connections.Count==2&&
                        session.Validate().IsValid,"group duplicate Redo damaged engineering graph");
                }
            }
            finally{session.Close(true);}
        }

        /// <summary>九种绘制工具共用的纯几何验收：拖动正反方向、正圆约束、连续点绘与非法输入。</summary>
        private static void CheckDrawnGeometryContract()
        {
            var start=new FGEPoint2D(210,190);
            var end=new FGEPoint2D(35,60);
            double x,y;
            MRCShapeDefinition rectangle=MVIShapeCatalog.CreateDrawn(MRCShapeKind.Rect,
                new[]{start,end},false,out x,out y);
            Check(Math.Abs(x-35)<1e-9&&Math.Abs(y-60)<1e-9&&
                Math.Abs(rectangle.Bounds.Width-175)<1e-9&&
                Math.Abs(rectangle.Bounds.Height-130)<1e-9,
                "反方向矩形拖绘的World包围盒错误");
            MRCShapeDefinition circle=MVIShapeCatalog.CreateDrawn(MRCShapeKind.Circle,
                new[]{start,end},false,out x,out y);
            Check(Math.Abs(circle.Bounds.Width-circle.Bounds.Height)<1e-9&&
                Math.Abs(circle.Bounds.Width-175)<1e-9,
                "圆形工具必须保持正圆");
            MRCShapeDefinition square=MVIShapeCatalog.CreateDrawn(MRCShapeKind.Rect,
                new[]{start,end},true,out x,out y);
            Check(Math.Abs(square.Bounds.Width-square.Bounds.Height)<1e-9,
                "Shift拖绘不能生成非正方形");
            var points=new[]{new FGEPoint2D(20,40),
                new FGEPoint2D(90,40),new FGEPoint2D(80,105)};
            foreach(MRCShapeKind kind in new[]{
                MRCShapeKind.Polyline,MRCShapeKind.Polygon,MRCShapeKind.Path})
            {
                MRCShapeDefinition path=MVIShapeCatalog.CreateDrawn(kind,points,
                    false,out x,out y);
                Check(path.Kind==kind&&Math.Abs(x-20)<1e-9&&
                    Math.Abs(y-40)<1e-9&&path.Geometry.Commands.Count>=3,
                    "连续点绘几何未保留三顶点与对象类别："+kind);
            }
            foreach(MRCShapeKind kind in new[]{
                MRCShapeKind.Line,MRCShapeKind.Rect,MRCShapeKind.Circle,
                MRCShapeKind.Ellipse,MRCShapeKind.Arc,MRCShapeKind.Bezier})
            {
                var definition=MVIShapeCatalog.CreateDrawn(kind,new[]{start,end},
                    false,out x,out y);
                Check(definition.Kind==kind&&definition.Geometry.Commands.Count>=2,
                    "拖绘路径生成失败："+kind);
            }
            // GE-03 Path多轮廓同时保存各自Move/Line命令，不能跨空白区连边。
            var outlines=new IReadOnlyList<FGEPoint2D>[]{
                new[]{new FGEPoint2D(10,25),new FGEPoint2D(25,35),
                    new FGEPoint2D(42,20)},
                new[]{new FGEPoint2D(250,125),new FGEPoint2D(275,145)}};
            var multi=MVIShapeCatalog.CreateDrawnContours(outlines,out x,out y);
            Check(multi.Kind==MRCShapeKind.Path&&multi.Geometry.Commands.Count==5&&
                multi.Geometry.Commands[0].Verb==FGEPathVerb.Move&&
                multi.Geometry.Commands[3].Verb==FGEPathVerb.Move&&
                Math.Abs(x-10)<1e-9&&Math.Abs(y-20)<1e-9&&
                multi.Geometry.FlattenContours().Count==2,
                "Multi-contour Path must keep two independent MoveTo origins");
            bool badContour=false;
            try{MVIShapeCatalog.CreateDrawnContours(
                new IReadOnlyList<FGEPoint2D>[]{
                    outlines[0],new[]{new FGEPoint2D(15,15)}},out x,out y);}
            catch(ArgumentException){badContour=true;}
            Check(badContour,"Unfinished Path contour must not create any shape");
            bool rejected=false;
            try{MVIShapeCatalog.CreateDrawn(MRCShapeKind.Polygon,points.Take(2).ToArray(),
                false,out x,out y);}
            catch(ArgumentException){rejected=true;}
            Check(rejected,"只有两个点的Polygon必须拒绝");
            rejected=false;
            try{MVIShapeCatalog.CreateDrawn(MRCShapeKind.Line,
                new[]{start,start},false,out x,out y);}
            catch(ArgumentException){rejected=true;}
            Check(rejected,"零长度绘图不得进入工程历史");
            // Widget拖绘与文字框共享合法性判定，但单击使用控件自己的默认尺寸。
            double width,height;
            MVIShapeCatalog.WidgetBounds(new[]{start,start},
                out x,out y,out width,out height);
            Check(Math.Abs(x-210)<1e-9&&Math.Abs(y-190)<1e-9&&
                Math.Abs(width-120)<1e-9&&Math.Abs(height-36)<1e-9,
                "Widget单击应原位放置120×36控件");
            MVIShapeCatalog.WidgetBounds(new[]{start,end},
                out x,out y,out width,out height);
            Check(Math.Abs(x-35)<1e-9&&Math.Abs(y-60)<1e-9&&
                Math.Abs(width-175)<1e-9&&Math.Abs(height-130)<1e-9,
                "Widget反方向拖绘应保留世界外框");
            MVIShapeCatalog.WidgetBounds(new[]{start,new FGEPoint2D(220,210)},
                out x,out y,out width,out height);
            Check(Math.Abs(width-40)<1e-9&&Math.Abs(height-24)<1e-9,
                "Widget短拖绘不应生成零尺寸控件");
            rejected=false;
            try{MVIShapeCatalog.WidgetBounds(new[]{start},
                out x,out y,out width,out height);}
            catch(ArgumentException){rejected=true;}
            Check(rejected,"Widget缺少结束点应明确拒绝");
            Console.WriteLine("PASS Visual Draw: nine Shape geometry, reversed drag, shift/circle, multi-click and Widget bounds/rejection");
        }
        public static void Run()
        {
            CheckDrawnGeometryContract();
            CheckNestedGroupVisualDuplicate();
            CheckVisualDuplicateAndInternalConnections();
            CheckVisualGapDistribution();
            CheckUnifiedSnapping();
            CheckConstrainedSelectionTransform();
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
            var openSegment=FGEPath2D.Line(new FGEPoint2D(0,0),
                new FGEPoint2D(20,0));
            bool shortCloseRejected=false;
            try{FGEPathEditing.SetContourClosed(openSegment,1,true);}
            catch(ArgumentException){shortCloseRejected=true;}
            Check(shortCloseRejected&&openSegment.Commands.Count==2,
                "Two-point open line must not be closed into a degenerate contour");
            var converted=FGEPathEditing.ConvertSegment(plain,1,FGEPathVerb.Line);
            Check(converted.Commands[1].Verb==FGEPathVerb.Line&&
                converted.Commands[1].End.Equals(plain.Commands[1].End),
                "segment conversion altered endpoint");
            var deletion=FGEPathEditing.DeleteAnchor(converted,1);
            Check(deletion.Commands.Count==2&&deletion.Commands[1].Verb==FGEPathVerb.Cubic,
                "line vertex deletion damaged remaining contour");
            // 闭合三角形不能删除任何角点后变成两点闭合；拒绝不得修改源路径。
            var minPolygon=FGEPath2D.Polyline(new[]{
                new FGEPoint2D(0,0),new FGEPoint2D(20,0),new FGEPoint2D(0,20)},true);
            bool rejected=false;
            try{FGEPathEditing.DeleteAnchor(minPolygon,1);}
            catch(ArgumentException){rejected=true;}
            Check(rejected&&minPolygon.Commands.Count==4,
                "Closed triangle must not allow a two-point contour");
            var quadPolygon=FGEPath2D.Polyline(new[]{
                new FGEPoint2D(0,0),new FGEPoint2D(20,0),
                new FGEPoint2D(20,20),new FGEPoint2D(0,20)},true);
            var reducedPolygon=FGEPathEditing.DeleteAnchor(quadPolygon,1);
            Check(reducedPolygon.Commands.Count==4&&
                reducedPolygon.Commands[3].Verb==FGEPathVerb.Close,
                "Four-vertex closed contour should delete one anchor to a triangle");
            var modified=FGEPathEditing.MoveHandle(plain,2,FGEPathHandleKind.Control2,
                new FGEPoint2D(95,100));
            Check(modified.Commands[2].Control2.Equals(new FGEPoint2D(95,100)),
                "control2 was not changed precisely");
            Check(plain.Commands[2].Control2.Equals(new FGEPoint2D(85,60)),
                "immutable original path mutated");
            Check(modified.Commands[2].End.Equals(plain.Commands[2].End),
                "editing a handle moved the endpoint");
            rejected=false;
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
                    // GE-03: 曲线命中求世界空间t，不向MVI存屏幕采样点。
                    FGEBezier2D lastCubic=new FGEBezier2D(plain.Commands[1].End,
                        plain.Commands[2].Control1,plain.Commands[2].Control2,plain.Commands[2].End);
                    FGEPoint2D seek=projected.GeometryToWorld.TransformPoint(lastCubic.PointAt(0.65));
                    int hitIndex;double hitFraction;
                    Check(MVIVisualEditing.TryHitPathSplitSegment(projected,
                        seek.X,seek.Y,0.5,out hitIndex,out hitFraction)&&
                        hitIndex==2&&Math.Abs(hitFraction-0.65)<0.02,
                        "World-space Bezier hit must recover original t inside rotated group");
                    Check(!MVIVisualEditing.TryHitPathSplitSegment(projected,
                        seek.X+2000,seek.Y-2000,8,out hitIndex,out hitFraction),
                        "Far curve hit must not insert an invisible segment");
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
                    // GE-03：整圆弧端点重合不能因normalize(2π)=0而直接坍缩。
                    var fullArc=new FGEArc2D(new FGEPoint2D(30,25),
                        27,19,5.7,Math.PI*2);
                    var fullShape=doc.AddShape(group,"FullArc",
                        MRCShapeDefinition.Arc(fullArc),215,215);
                    MVISceneItem fullScene=editor.Scene.Single(x=>x.Id==fullShape.Id);
                    MVIVisualHandle fullEnd=MVIVisualEditing.Handles(fullScene)
                        .Single(x=>x.Kind==MVIVisualHandleKind.ArcEnd);
                    int beforeFull=doc.Revision;
                    MTCEditResult<bool> fullNoOp=editor.EditHandle(fullEnd,
                        fullEnd.WorldPoint.X,fullEnd.WorldPoint.Y);
                    // 原几何未变，MEM会正确消除等价SetAspect/Transform/Resize，不造Undo。
                    Check(fullNoOp.Success&&doc.Revision==beforeFull&&
                        Math.Abs(fullShape.Shape.Geometry.Commands[1].Arc.SweepRadians-
                            Math.PI*2)<1e-8,
                        "full circle same-end edit must preserve sweep with no-op revision");
                    // 实际移动末端跨越0度：保留正扫角并创建一次可撤销MEM事务。
                    FGEPoint2D nearEnd=new FGEPoint2D(
                        fullArc.Center.X+fullArc.RadiusX*Math.Cos(fullArc.StartRadians-0.13),
                        fullArc.Center.Y+fullArc.RadiusY*Math.Sin(fullArc.StartRadians-0.13));
                    FGEPoint2D worldNearEnd=fullScene.GeometryToWorld.TransformPoint(nearEnd);
                    Check(editor.EditHandle(fullEnd,worldNearEnd.X,worldNearEnd.Y).Success&&
                        doc.Revision==beforeFull+1&&
                        Math.Abs(fullShape.Shape.Geometry.Commands[1].Arc.SweepRadians-
                            (Math.PI*2-0.13))<1e-8,
                        "full circle end crossing zero must keep directed near-full sweep");
                    Check(editor.Undo().Success&&
                        Math.Abs(fullShape.Shape.Geometry.Commands[1].Arc.SweepRadians-
                            Math.PI*2)<1e-8,"full circle Undo did not restore full sweep");
                    Check(editor.Redo().Success&&
                        Math.Abs(fullShape.Shape.Geometry.Commands[1].Arc.SweepRadians-
                            (Math.PI*2-0.13))<1e-8,"full circle Redo lost edited sweep");
                    Check(editor.Undo().Success,"restore full circle before editing start failed");
                    MVISceneItem fullAfter=editor.Scene.Single(x=>x.Id==fullShape.Id);
                    MVIVisualHandle fullStart=MVIVisualEditing.Handles(fullAfter)
                        .Single(x=>x.Kind==MVIVisualHandleKind.ArcStart);
                    Check(editor.EditHandle(fullStart,fullStart.WorldPoint.X,
                        fullStart.WorldPoint.Y).Success&&
                        Math.Abs(fullShape.Shape.Geometry.Commands[1].Arc.SweepRadians-
                            Math.PI*2)<1e-8,
                        "full circle start handle must preserve signed full sweep");
                    var crossingArc=new FGEArc2D(new FGEPoint2D(26,22),
                        20,17,5.9,0.8);
                    var crossing=doc.AddShape(group,"WrapArc",
                        MRCShapeDefinition.Arc(crossingArc),275,205);
                    MVISceneItem crossingScene=editor.Scene.Single(x=>x.Id==crossing.Id);
                    MVIVisualHandle wrapEnd=MVIVisualEditing.Handles(crossingScene)
                        .Single(x=>x.Kind==MVIVisualHandleKind.ArcEnd);
                    FGEPoint2D endLocal=new FGEPoint2D(
                        crossingArc.Center.X+crossingArc.RadiusX*Math.Cos(0.2),
                        crossingArc.Center.Y+crossingArc.RadiusY*Math.Sin(0.2));
                    FGEPoint2D wrapTarget=crossingScene.GeometryToWorld.TransformPoint(endLocal);
                    Check(editor.EditHandle(wrapEnd,wrapTarget.X,wrapTarget.Y).Success,
                        "Arc crossing zero endpoint edit failed");
                    Check(Math.Abs(crossing.Shape.Geometry.Commands[1].Arc.SweepRadians-
                        (0.2+Math.PI*2-5.9))<1e-8,
                        "Arc across 2π must preserve positive directed sweep");

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
