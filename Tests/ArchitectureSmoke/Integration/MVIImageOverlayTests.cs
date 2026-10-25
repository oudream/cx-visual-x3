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

            MVIImageOverlayItem defect = overlays.Add(new MVIImageOverlayItem(
                MVIImageOverlayKind.DefectBox, MVIImageOverlayLayer.Defect,
                new FGERect2D(1, 2, 3, 4), text:"Defect-12"));
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
            Console.WriteLine("PASS MVI ImageOverlay: 旧分类/增改删/图层/移动/防脏模型");
        }

        private static void Ensure(bool condition,string description)
        { if(!condition)throw new InvalidOperationException("MVI叠加项测试失败："+description); }
        private static void Fail<T>(Action action,string description) where T:Exception
        {try{action();}catch(T){return;}throw new InvalidOperationException("预期错误未发生："+description);}
    }
}
