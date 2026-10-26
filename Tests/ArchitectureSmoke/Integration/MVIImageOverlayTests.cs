using System;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class MVIImageOverlayTests
    {
        public static void Run()
        {
            MVIImageOverlayStore overlays = new MVIImageOverlayStore();
            int changed = 0;
            overlays.Changed += (sender, args) => changed++;
            MVIImageOverlayItem roi = overlays.Add(new MVIImageOverlayItem(
                MVIImageOverlayKind.RectangleRoi, MVIImageOverlayLayer.Roi,
                new FGERect2D(2, 3, 7, 9)));
            Ensure(overlays.Items.Count == 1 && overlays.Find(roi.Id) != null &&
                overlays.GetVisibleItems().Count == 1 && changed == 1, "首次添加与可见性");

            MVIImageOverlayItem moved = roi.Translate(4, 5);
            Ensure(moved.Id == roi.Id && moved.Bounds.Left == 6 &&
                roi.Bounds.Left == 2, "移动不改变原对象及身份");
            Ensure(overlays.Replace(moved) && overlays.Find(roi.Id).Bounds.Top == 8 &&
                changed == 2, "替换刷新");

            overlays.SetLayerVisible(MVIImageOverlayLayer.Roi, false);
            Ensure(overlays.GetVisibleItems().Count == 0 && changed == 3, "隐藏ROI图层");
            overlays.SetLayerVisible(MVIImageOverlayLayer.Roi, false);
            Ensure(changed == 3, "重复隐藏不通知");
            overlays.SetLayerVisible(MVIImageOverlayLayer.Roi, true);

            MVIImageOverlayItem polygon = overlays.Add(new MVIImageOverlayItem(
                MVIImageOverlayKind.PolygonRoi, MVIImageOverlayLayer.Roi,
                new FGERect2D(0, 0, 8, 8),
                new[]{new FGEPoint2D(0,0),new FGEPoint2D(8,0),new FGEPoint2D(4,8)}));
            Ensure(polygon.Points.Count == 3 && overlays.GetVisibleItems().Count == 2,
                "多边形保留顶点");
            MVIImageOverlayItem translated = polygon.Translate(1,2);
            Ensure(translated.Points[0].X == 1 && translated.Points[0].Y == 2 &&
                polygon.Points[0].X == 0, "移动顶点不破坏原图坐标");
            Ensure(overlays.Replace(translated), "更新多边形");
            MVIImageOverlayItem scaled = translated.WithBounds(new FGERect2D(1,2,16,16));
            Ensure(scaled.Id == translated.Id && scaled.Points[1].X == 17 &&
                scaled.Points[2].Y == 18 &&
                translated.Points[1].X == 9, "缩放同步顶点，不改变原模型");

            // 精细ROI八方向编辑：边界、反向拖动、顶点、对象身份/样式都由MVI保证。
            MVIImageOverlayItem resized = polygon.WithResizedRoiHandle(
                MVIImageResizeHandle.BottomRight,new FGEPoint2D(12,10),20,20);
            Ensure(resized.Id == polygon.Id && resized.Bounds.Width == 12 &&
                resized.Bounds.Height == 10 && resized.Points[1].X == 12 &&
                resized.Points[2].Y == 10 && polygon.Bounds.Width == 8,
                "右下手柄更新多边形顶点而不改变原对象");
            MVIImageOverlayItem clamped = resized.WithResizedRoiHandle(
                MVIImageResizeHandle.TopLeft,new FGEPoint2D(999,999),20,20);
            Ensure(Math.Abs(clamped.Bounds.Width-0.25)<1e-9 &&
                Math.Abs(clamped.Bounds.Height-0.25)<1e-9,
                "拖越对侧角时保留合法最小ROI，不能发生翻转");
            MVIImageOverlayItem top = roi.WithResizedRoiHandle(
                MVIImageResizeHandle.Top,new FGEPoint2D(-20,-20),30,30);
            Ensure(top.Bounds.Top == 0 && top.Bounds.Left == roi.Bounds.Left &&
                top.Bounds.Width == roi.Bounds.Width, "边中柄只编辑对应维度且钳位");
            MVIImageOverlayItem right = roi.WithResizedRoiHandle(
                MVIImageResizeHandle.Right,new FGEPoint2D(100,3),30,30);
            Ensure(right.Bounds.Right == 30 && right.Bounds.Bottom == roi.Bounds.Bottom,
                "图像边界钳位、不改变其他边");
            Ensure(overlays.Replace(resized) && overlays.Find(polygon.Id).Id == polygon.Id,
                "拖动结果写回同一Store和稳定ID");
            Fail<ArgumentOutOfRangeException>(()=>roi.WithResizedRoiHandle(
                (MVIImageResizeHandle)99,new FGEPoint2D(1,1),30,30),"未知编辑柄");
            Fail<ArgumentOutOfRangeException>(()=>roi.WithResizedRoiHandle(
                MVIImageResizeHandle.Right,new FGEPoint2D(1,1),0,30),"零图宽");
            // 多边形按顶点编辑，与八方向整体缩放是两个独立可复用操作。
            MVIImageOverlayItem quad=overlays.Add(new MVIImageOverlayItem(
                MVIImageOverlayKind.PolygonRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(1,1,8,8),new[]{
                    new FGEPoint2D(1,1),new FGEPoint2D(9,1),
                    new FGEPoint2D(9,9),new FGEPoint2D(1,9)}));
            MVIImageOverlayItem vertexEdited=quad.WithMovedPolygonVertex(
                1,new FGEPoint2D(12,2),20,20);
            Ensure(vertexEdited.Id==quad.Id && vertexEdited.Points[1].X==12 &&
                vertexEdited.Points[0].X==1 && vertexEdited.Points[2].Y==9 &&
                vertexEdited.Bounds.Right==12 && quad.Bounds.Right==9,
                "逐顶点更新精确Bounds并保持对象身份及其他顶点");
            MVIImageOverlayItem clipped=quad.WithMovedPolygonVertex(
                1,new FGEPoint2D(200,-10),20,20);
            Ensure(clipped.Points[1].X==20 && clipped.Points[1].Y==0,
                "越过图像边界时顶点被正确约束");
            Fail<ArgumentOutOfRangeException>(()=>quad.WithMovedPolygonVertex(
                4,new FGEPoint2D(3,3),20,20),"不存在的顶点索引");
            Fail<ArgumentException>(()=>quad.WithMovedPolygonVertex(
                1,new FGEPoint2D(1,1),20,20),"顶点重合退化必须拒绝");
            Fail<ArgumentException>(()=>quad.WithMovedPolygonVertex(
                1,new FGEPoint2D(4,12),20,20),"顶点移过对边导致多边形自交必须拒绝");
            Fail<InvalidOperationException>(()=>roi.WithMovedPolygonVertex(
                0,new FGEPoint2D(1,1),20,20),"矩形ROI不能假装多边形顶点编辑");
            int beforeVertexNotify=changed;
            Ensure(overlays.Replace(vertexEdited) && changed==beforeVertexNotify+1 &&
                overlays.Find(quad.Id).Points[1].X==12,
                "逐顶点编辑经唯一Store观察通知更新");
            MVIImageOverlayItem vertexLocked=vertexEdited.WithLocked(true);
            Fail<InvalidOperationException>(()=>vertexLocked.WithMovedPolygonVertex(
                1,new FGEPoint2D(10,2),20,20),"锁定多边形不得编辑顶点");
            Ensure(vertexLocked.WithVisibility(false).IsLocked &&
                vertexLocked.WithLocked(false).WithStyle(unchecked((int)0xff223344),2).IsLocked==false,
                "不可变快照保留锁定状态并支持显式解锁");
            overlays.Remove(quad.Id);
            // 长多边形拖动：几何检测只核对变动两条边，避免O(n²)冻结WPF。
            FGEPoint2D[] dense=new FGEPoint2D[2048];
            for (int i=0;i<dense.Length;i++)
            {
                double angle=2*Math.PI*i/dense.Length;
                dense[i]=new FGEPoint2D(120+100*Math.Cos(angle),
                    120+100*Math.Sin(angle));
            }
            MVIImageOverlayItem densePolygon=new MVIImageOverlayItem(
                MVIImageOverlayKind.PolygonRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(20,20,200,200),dense);
            MVIImageOverlayItem denseEdited=densePolygon.WithMovedPolygonVertex(
                0,new FGEPoint2D(220.01,120),256,256);
            Ensure(denseEdited.Points.Count==2048 &&
                Math.Abs(denseEdited.Points[0].X-220.01)<1e-8 &&
                denseEdited.Bounds.Right>220,
                "长多边形顶点编辑保留点数及精确外框");
            MVIImageOverlayItem defect = overlays.Add(new MVIImageOverlayItem(
                MVIImageOverlayKind.DefectBox, MVIImageOverlayLayer.Defect,
                new FGERect2D(1, 2, 3, 4), text:"Defect-12"));
            Fail<InvalidOperationException>(()=>defect.WithResizedRoiHandle(
                MVIImageResizeHandle.Right,new FGEPoint2D(8,8),30,30),
                "缺陷业务框不是ROI不能被编辑器越权缩放");
            overlays.SetLayerVisible(MVIImageOverlayLayer.Roi, false);
            Ensure(overlays.GetVisibleItems().Count == 1 && overlays.GetVisibleItems()[0].Id == defect.Id,
                "隐藏ROI不影响缺陷层");
            Ensure(overlays.Remove(roi.Id) && !overlays.Remove(roi.Id), "删除幂等");
            overlays.Clear(MVIImageOverlayLayer.Roi);
            Ensure(overlays.Items.Count == 1 && overlays.Items[0].Kind == MVIImageOverlayKind.DefectBox,
                "按层清理");
            overlays.Clear();
            Ensure(overlays.Items.Count == 0, "清空覆盖项");
            overlays.Add(defect);
            Fail<InvalidOperationException>(() => overlays.Add(defect), "相同ID重复添加");
            Fail<ArgumentException>(() => new MVIImageOverlayItem(
                MVIImageOverlayKind.PolygonRoi, MVIImageOverlayLayer.Roi,
                new FGERect2D(0,0,1,1), new[] { new FGEPoint2D(0,0) }), "少于三个顶点");
            Fail<ArgumentOutOfRangeException>(() => new MVIImageOverlayItem(
                MVIImageOverlayKind.RectangleRoi, MVIImageOverlayLayer.Roi,
                new FGERect2D(0,0,1,1), lineWidth:0), "无效线宽");
            // 对象锁定、栈顺序和Observer均复用唯一MVI Store，不另建Layer Manager。
            MVIImageOverlayStore stacking=new MVIImageOverlayStore();
            int orderedChanges=0;
            stacking.Changed += (s,e)=>orderedChanges++;
            MVIImageOverlayItem back=stacking.Add(new MVIImageOverlayItem(
                MVIImageOverlayKind.RectangleRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(1,1,4,4)));
            MVIImageOverlayItem front=stacking.Add(new MVIImageOverlayItem(
                MVIImageOverlayKind.RectangleRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(1,1,4,4)));
            Ensure(stacking.Items[1].Id==front.Id &&
                stacking.MoveZOrder(back.Id,1) && stacking.Items[1].Id==back.Id,
                "叠加项置前影响真实绘制/命中顺序");
            Ensure(stacking.MoveZOrder(back.Id,-1) && stacking.Items[0].Id==back.Id,
                "叠加项置后恢复先前顺序");
            Ensure(!stacking.MoveZOrder(back.Id,-1),"边界层级为幂等");
            Fail<ArgumentOutOfRangeException>(()=>stacking.MoveZOrder(back.Id,0),"无效层级方向");
            Ensure(stacking.SetLocked(back.Id,true) && stacking.Find(back.Id).IsLocked,
                "锁定记录在唯一Store中");
            int beforeInvalid=orderedChanges;
            Fail<InvalidOperationException>(()=>stacking.MoveZOrder(front.Id,-1),
                "不得绕过锁定对象重新层叠");
            Fail<InvalidOperationException>(()=>stacking.Find(back.Id).Translate(1,1),
                "锁定ROI拒绝几何移动");
            Fail<InvalidOperationException>(()=>stacking.Remove(back.Id),
                "锁定ROI拒绝普通Store删除");
            Fail<InvalidOperationException>(()=>stacking.Find(back.Id).WithResizedRoiHandle(
                MVIImageResizeHandle.Right,new FGEPoint2D(8,2),16,16),
                "锁定ROI拒绝编辑柄");
            Fail<InvalidOperationException>(()=>stacking.Replace(
                stacking.Find(back.Id).WithVisibility(false)),"唯一Store不能覆盖锁定对象");
            Ensure(stacking.Items[0].Id==back.Id && orderedChanges==beforeInvalid,
                "被拒绝操作不得污染Z序/状态/通知");
            Ensure(stacking.SetLocked(back.Id,false) && !stacking.Find(back.Id).IsLocked &&
                stacking.MoveZOrder(front.Id,-1) && stacking.Items[0].Id==front.Id,
                "显式解锁后允许正常变更层级");
            Console.WriteLine("PASS MVI ImageOverlay: 旧分类/增改删/八柄/锁定/Z序/原图几何");
        }

        private static void Ensure(bool condition,string description)
        { if(!condition)throw new InvalidOperationException("MVI叠加项测试失败："+description); }
        private static void Fail<T>(Action action,string description) where T:Exception
        {try{action();}catch(T){return;}throw new InvalidOperationException("预期错误未发生："+description);}
    }
}
