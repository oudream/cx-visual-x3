using System;
using System.Collections.Generic;
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Visual;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class MVIImageOverlayTests
    {
        public static void Run()
        {
            MVIImageOverlayItem pairItem=new MVIImageOverlayItem(
                MVIImageOverlayKind.LinePairMeasurement,MVIImageOverlayLayer.Measurement,
                new FGERect2D(0,0,4,3),new[]{
                    new FGEPoint2D(0,0),new FGEPoint2D(4,0),
                    new FGEPoint2D(1,3),new FGEPoint2D(3,3)});
            Ensure(pairItem.Points.Count==4 &&
                pairItem.Layer==MVIImageOverlayLayer.Measurement &&
                pairItem.Points[2].Y==3,
                "四点两线测量对象保留原图真值");
            Fail<ArgumentException>(()=>new MVIImageOverlayItem(
                MVIImageOverlayKind.LinePairMeasurement,MVIImageOverlayLayer.Measurement,
                new FGERect2D(0,0,4,3),new[]{
                    new FGEPoint2D(0,0),new FGEPoint2D(4,0),
                    new FGEPoint2D(1,3)}),"三点无法冒充完整Ls2Ls");
            Fail<ArgumentException>(()=>new MVIImageOverlayItem(
                MVIImageOverlayKind.LinePairMeasurement,MVIImageOverlayLayer.Roi,
                new FGERect2D(0,0,4,3),pairItem.Points),"Ls2Ls不得进入ROI层");
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
            // ROI分析必须以真实形状命中，不得把外接Bounds当成任意ROI。
            MVIImageOverlayItem sampleTriangle=new MVIImageOverlayItem(
                MVIImageOverlayKind.PolygonRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(0,0,3.6,3.6),new[]{
                    new FGEPoint2D(0,0),new FGEPoint2D(3.6,0),
                    new FGEPoint2D(0,3.6)});
            Ensure(sampleTriangle.IsAreaRoi &&
                sampleTriangle.ContainsRoiPixelCenter(0,0) &&
                sampleTriangle.ContainsRoiPixelCenter(1,1) &&
                !sampleTriangle.ContainsRoiPixelCenter(3,3) &&
                !sampleTriangle.ContainsRoiPixelCenter(2,2),
                "非矩形ROI以原图像素中心命中，不误采包围盒外角");
            MVIImageOverlayItem sampleEllipse=new MVIImageOverlayItem(
                MVIImageOverlayKind.EllipseRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(0,0,4,4));
            Ensure(sampleEllipse.ContainsRoiPixelCenter(1,1) &&
                !sampleEllipse.ContainsRoiPixelCenter(0,0) &&
                !sampleEllipse.ContainsRoiPixelCenter(3,3),
                "椭圆ROI不能错误地按矩形四角统计");
            MVIImageOverlayItem noArea=new MVIImageOverlayItem(
                MVIImageOverlayKind.DefectBox,MVIImageOverlayLayer.Defect,
                new FGERect2D(0,0,4,4));
            Ensure(roi.IsAreaRoi && roi.ContainsRoiPixelCenter(2,3) &&
                !roi.ContainsRoiPixelCenter(1,1) &&
                !noArea.IsAreaRoi && !noArea.ContainsRoiPixelCenter(1,1),
                "矩形ROI与缺陷标记的统计身份不能混用");
            // 单边插点与单点删除采用原来的唯一Store与O(n)拓扑校验。
            MVIImageOverlayItem inserted=quad.WithInsertedPolygonVertex(
                0,new FGEPoint2D(5,0.5),20,20);
            Ensure(inserted.Id==quad.Id && inserted.Points.Count==5 &&
                inserted.Points[1].Equals(new FGEPoint2D(5,0.5)) &&
                inserted.Bounds.Top==0.5 && quad.Points.Count==4 &&
                inserted.Argb==quad.Argb && inserted.LineWidth==quad.LineWidth,
                "插入顶点应保持对象身份样式、精确外框及原快照");
            int beforeInsertNotify=changed;
            Ensure(overlays.Replace(inserted) && changed==beforeInsertNotify+1 &&
                overlays.Find(quad.Id).Points.Count==5,
                "插点写入唯一Store且仅通知一次");
            MVIImageOverlayItem restored=inserted.WithRemovedPolygonVertex(1);
            Ensure(restored.Id==quad.Id && restored.Points.Count==4 &&
                restored.Bounds.Equals(quad.Bounds) &&
                restored.Points[1].Equals(quad.Points[1]),
                "删点应恢复原始四边形几何");
            Ensure(overlays.Replace(restored) && overlays.Find(quad.Id).Points.Count==4,
                "删除顶点刷新观察者");
            Fail<ArgumentOutOfRangeException>(()=>quad.WithInsertedPolygonVertex(
                4,new FGEPoint2D(5,0.5),20,20),"不存在的边索引");
            Fail<ArgumentOutOfRangeException>(()=>quad.WithInsertedPolygonVertex(
                0,new FGEPoint2D(-1,2),20,20),"插点越界");
            Fail<ArgumentException>(()=>quad.WithInsertedPolygonVertex(
                0,quad.Points[1],20,20),"与邻接顶点重合的插入必须拒绝");
            Fail<ArgumentException>(()=>quad.WithInsertedPolygonVertex(
                0,new FGEPoint2D(5,12),20,20),"穿越非邻接边的插点必须拒绝");
            Fail<ArgumentOutOfRangeException>(()=>quad.WithRemovedPolygonVertex(
                4),"非法顶点索引");
            Fail<InvalidOperationException>(()=>polygon.WithRemovedPolygonVertex(0),
                "三角形不得删除至两个顶点");
            MVIImageOverlayItem collinear=new MVIImageOverlayItem(
                MVIImageOverlayKind.PolygonRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(1,1,4,2),new[]{
                    new FGEPoint2D(1,1),new FGEPoint2D(3,1),
                    new FGEPoint2D(5,1),new FGEPoint2D(3,3)});
            Fail<ArgumentException>(()=>collinear.WithRemovedPolygonVertex(3),
                "删除顶点形成面积为零的三角形必须拒绝");
            Fail<InvalidOperationException>(()=>quad.WithLocked(true)
                .WithInsertedPolygonVertex(0,new FGEPoint2D(5,0.5),20,20),
                "锁定后不允许插点");
            Fail<InvalidOperationException>(()=>quad.WithLocked(true)
                .WithRemovedPolygonVertex(1),"锁定后不允许删点");
            Fail<InvalidOperationException>(()=>roi.WithInsertedPolygonVertex(
                0,new FGEPoint2D(5,2),20,20),"非多边形ROI不可插点");
            Ensure(overlays.Find(quad.Id).Points.Count==4 &&
                quad.Points.Count==4,"失败操作不污染唯一Store或旧快照");
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
            FGEPoint2D denseMidpoint=new FGEPoint2D(
                (dense[0].X+dense[1].X)/2.0,(dense[0].Y+dense[1].Y)/2.0);
            MVIImageOverlayItem denseInserted=densePolygon.WithInsertedPolygonVertex(
                0,denseMidpoint,256,256);
            MVIImageOverlayItem denseRestored=denseInserted.WithRemovedPolygonVertex(1);
            Ensure(denseInserted.Points.Count==2049 &&
                denseRestored.Points.Count==2048 &&
                denseRestored.Id==densePolygon.Id &&
                denseRestored.Points[128].Equals(densePolygon.Points[128]),
                "2048顶点长轮廓插删只更新受影响的边且保持原图几何");
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
            MVIImageOverlayItem rot=MVIImageOverlayItem.CreateRotatedRectangle(
                new FGERect2D(11,14,10,4),0,40,40);
            Ensure(rot.Points.Count==4 && rot.RotatedRectangleWidth==10 &&
                rot.RotatedRectangleHeight==4 && rot.RotatedRectangleArea==40 &&
                rot.RotatedRectangleAngleDegrees==0,
                "旋转矩形初始四角原图数据和宽高面积");
            MVIImageOverlayItem rotated=rot.WithRotatedRectangleAngle(45,40,40);
            Ensure(rotated.Id==rot.Id && Math.Abs(rotated.RotatedRectangleAngleDegrees-45)<1e-8 &&
                Math.Abs(rotated.RotatedRectangleArea-40)<1e-8 &&
                rotated.Bounds.Width>rot.Bounds.Width*0.7 &&
                rot.RotatedRectangleAngleDegrees==0,
                "角度编辑保留身份面积及原不可变快照");
            Ensure(FGEGeometry2D.PointInPolygon(new FGEPoint2D(16,16),rotated.Points) &&
                !FGEGeometry2D.PointInPolygon(new FGEPoint2D(11,11),rotated.Points),
                "倾斜矩形实际区域命中不能退化为Bounds");
            Ensure(rotated.Translate(1,1).Points[0].X==rotated.Points[0].X+1 &&
                rotated.Translate(1,1).Id==rot.Id,
                "旋转矩形移动保持顶点位置与ID");
            Fail<InvalidOperationException>(()=>rot.WithBounds(new FGERect2D(0,0,2,2)),
                "非等比Bounds编辑不能破坏旋转矩形正交性");
            Fail<InvalidOperationException>(()=>rot.WithLocked(true).WithRotatedRectangleAngle(
                20,40,40),"锁定旋转矩形不可修改角度");
            Fail<ArgumentOutOfRangeException>(()=>rot.WithRotatedRectangleAngle(
                45,18,18),"超出原图边界不得写入");
            Fail<ArgumentOutOfRangeException>(()=>MVIImageOverlayItem.CreateRotatedRectangle(
                new FGERect2D(0,0,0,4),0,40,40),"零宽旋转矩形拒绝");
            Ensure(!rot.IsLocked && !rot.WithRotatedRectangleAngle(40,40,40).IsLocked,
                "正常几何操作不会错误锁定");
            MVIImageOverlayItem wider=rot.WithResizedRotatedHandle(
                MVIImageResizeHandle.Right,new FGEPoint2D(24,16),40,40);
            Ensure(Math.Abs(wider.RotatedRectangleWidth-13)<1e-8 &&
                Math.Abs(wider.RotatedRectangleHeight-4)<1e-8 &&
                Math.Abs(wider.RotatedRectangleArea-52)<1e-8 &&
                wider.Points[0].Equals(rot.Points[0]) &&
                wider.Points[3].Equals(rot.Points[3]) &&
                wider.Id==rot.Id && rot.RotatedRectangleWidth==10,
                "右边柄仅沿局部X轴扩宽并锚定左边");
            MVIImageOverlayItem cornerChanged=rot.WithResizedRotatedHandle(
                MVIImageResizeHandle.TopLeft,new FGEPoint2D(10,13),40,40);
            Ensure(Math.Abs(cornerChanged.RotatedRectangleWidth-11)<1e-8 &&
                Math.Abs(cornerChanged.RotatedRectangleHeight-5)<1e-8 &&
                cornerChanged.Points[2].Equals(rot.Points[2]),
                "左上角柄锚定右下角而不破坏垂直度");
            FGEPoint2D centerRight=new FGEPoint2D(
                (rotated.Points[1].X+rotated.Points[2].X)/2,
                (rotated.Points[1].Y+rotated.Points[2].Y)/2);
            FGEVector2D localWidth=(rotated.Points[1]-rotated.Points[0])/10.0;
            MVIImageOverlayItem rotatedWider=rotated.WithResizedRotatedHandle(
                MVIImageResizeHandle.Right,
                centerRight+localWidth*2,40,40);
            Ensure(Math.Abs(rotatedWider.RotatedRectangleAngleDegrees-45)<1e-8 &&
                Math.Abs(rotatedWider.RotatedRectangleWidth-12)<1e-8 &&
                Math.Abs(rotatedWider.RotatedRectangleHeight-4)<1e-8 &&
                Math.Abs(FGEVector2D.Dot(
                    rotatedWider.Points[1]-rotatedWider.Points[0],
                    rotatedWider.Points[2]-rotatedWider.Points[1]))<1e-8,
                "倾斜状态下八柄必须保持正交且不改变角度");
            Fail<ArgumentOutOfRangeException>(()=>rot.WithResizedRotatedHandle(
                MVIImageResizeHandle.Right,new FGEPoint2D(100,16),40,40),
                "尺寸编辑超出原图时不可修改原对象");
            Fail<InvalidOperationException>(()=>rot.WithLocked(true).WithResizedRotatedHandle(
                MVIImageResizeHandle.Right,new FGEPoint2D(23,16),40,40),
                "锁定矩形禁止尺寸编辑");
            Ensure(Math.Abs(rot.RotatedRectangleWidth-10)<1e-8 &&
                Math.Abs(rot.RotatedRectangleArea-40)<1e-8,
                "编辑反例不会污染初始不可变对象");
            Fail<ArgumentException>(()=>new MVIImageOverlayItem(
                MVIImageOverlayKind.RotatedRectangleRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(1,1,4,4),new [] {
                    new FGEPoint2D(1,1),new FGEPoint2D(5,1),
                    new FGEPoint2D(4,5),new FGEPoint2D(1,5)}),
                "SDK直接创建非正交四边形必须拒绝");
            Fail<ArgumentException>(()=>new MVIImageOverlayItem(
                MVIImageOverlayKind.RotatedRectangleRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(0,0,1,1),new [] {
                    new FGEPoint2D(1,1),new FGEPoint2D(5,1),
                    new FGEPoint2D(5,5),new FGEPoint2D(1,5)}),
                "SDK输入错误Bounds不应写入非法矩形");

            // GoF Observer + OOP唯一Store：多对象提交只发送一个完整状态通知。
            MVIImageOverlayStore batch=new MVIImageOverlayStore();
            int batchNotifications=0;
            int seenOnNotify=-1;
            batch.Changed += (sender,args) =>
            {
                batchNotifications++;
                seenOnNotify=batch.Items.Count;
            };
            MVIImageOverlayItem first=new MVIImageOverlayItem(
                MVIImageOverlayKind.RectangleRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(0,0,3,3));
            MVIImageOverlayItem second=new MVIImageOverlayItem(
                MVIImageOverlayKind.RectangleRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(4,4,3,3));
            Ensure(batch.AddRange(new[]{first,second})==2 &&
                batchNotifications==1 && seenOnNotify==2 &&
                batch.Find(first.Id)!=null && batch.Find(second.Id)!=null,
                "原子批量叠加应在完整写入后仅发布一次Observer通知");
            Ensure(batch.AddRange(new MVIImageOverlayItem[0])==0 &&
                batchNotifications==1,"空批次不改变Store或发送通知");
            Fail<ArgumentNullException>(()=>batch.AddRange(null),"null批次拒绝");
            Fail<ArgumentException>(()=>batch.AddRange(new MVIImageOverlayItem[]{
                new MVIImageOverlayItem(MVIImageOverlayKind.RectangleRoi,
                    MVIImageOverlayLayer.Roi,new FGERect2D(8,8,2,2)),null}),
                "批中null拒绝且不部分提交");
            Fail<InvalidOperationException>(()=>batch.AddRange(new[]{first}),
                "与已有覆盖项重复身份拒绝");
            MVIImageOverlayItem candidate=new MVIImageOverlayItem(
                MVIImageOverlayKind.RectangleRoi,MVIImageOverlayLayer.Roi,
                new FGERect2D(10,10,2,2));
            Fail<InvalidOperationException>(()=>batch.AddRange(new[]{candidate,candidate}),
                "同批重复身份拒绝");
            Fail<InvalidOperationException>(()=>batch.AddRange(ThrowAfter(candidate)),
                "枚举中途异常不得部分写入");
            Ensure(batch.Items.Count==2 && batchNotifications==1 &&
                batch.Find(candidate.Id)==null,
                "所有前置异常保留原来的两个对象及一次通知");
            MVIImageOverlayItem third=new MVIImageOverlayItem(
                MVIImageOverlayKind.PolygonAnnotation,MVIImageOverlayLayer.Annotation,
                new FGERect2D(0,0,2,2),new[]{
                    new FGEPoint2D(0,0),new FGEPoint2D(2,0),new FGEPoint2D(0,2)});
            Ensure(batch.AddRange(new[]{third})==1 &&
                batchNotifications==2 && seenOnNotify==3,
                "单元素批提交与常规Add一致，且保持已存在对象");
            Console.WriteLine("PASS MVI ImageOverlay: 原子批量叠加、完整通知、重复/空项/枚举异常反证");

            Console.WriteLine("PASS MVI ImageOverlay: 旋转矩形正交几何/角度/命中/异常与原有ROI");
        }

        private static IEnumerable<MVIImageOverlayItem> ThrowAfter(MVIImageOverlayItem item)
        {
            yield return item;
            throw new InvalidOperationException("模拟枚举器中途失败");
        }

        private static void Ensure(bool condition,string description)
        { if(!condition)throw new InvalidOperationException("MVI叠加项测试失败："+description); }
        private static void Fail<T>(Action action,string description) where T:Exception
        {try{action();}catch(T){return;}throw new InvalidOperationException("预期错误未发生："+description);}
    }
}
