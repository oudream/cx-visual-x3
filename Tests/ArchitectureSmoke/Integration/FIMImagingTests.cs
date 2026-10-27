using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Geometry;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class FIMImagingTests
    {
        public static void Run()
        {
            byte[] source = { 1,2,3,4, 5,6,7,8, 9,10,11,12, 13,14,15,16 };
            FIMImageBuffer image = new FIMImageBuffer(4, 4, FIMPixelFormat.Gray8, source);
            source[0] = 90;
            Assert(image.GetGrayValue(0, 0) == 1, "输入缓冲所有权");
            byte[] copy = image.CopyPixels();
            copy[1] = 80;
            Assert(image.GetGrayValue(1, 0) == 2, "复制结果隔离");
            FIMThresholdRegionResult threshold=FIMImageOperations.ThresholdInRegion(
                image,6,10,new FIMPixelRegion(0,1,3,2));
            Assert(threshold.PixelCount==5 && threshold.SampledPixelCount==8 &&
                threshold.Bounds.HasValue && threshold.Bounds.Value.Left==0 &&
                threshold.Bounds.Value.Right==3 && threshold.Bounds.Value.Top==1 &&
                threshold.Bounds.Value.Bottom==2 &&
                threshold.Mask.PixelFormat==FIMPixelFormat.Gray8 &&
                threshold.Mask.GetGrayValue(1,1)==255 &&
                threshold.Mask.GetGrayValue(0,1)==0 &&
                threshold.Mask.GetGrayValue(1,2)==255 &&
                threshold.Mask.GetGrayValue(2,2)==0 &&
                image.GetGrayValue(1,1)==6,
                "限定原图ROI阈值分割必须返回真实掩码/像素数并保留原图");
            FIMThresholdRegionResult shape=FIMImageOperations.ThresholdInRegion(
                image,5,14,new FIMPixelRegion(0,1,3,2),(x,y)=>x==1);
            Assert(shape.SampledPixelCount==2 && shape.PixelCount==2 &&
                shape.Mask.GetGrayValue(1,1)==255 && shape.Mask.GetGrayValue(1,2)==255 &&
                shape.Mask.GetGrayValue(0,1)==0 && shape.Mask.GetGrayValue(2,2)==0,
                "非矩形ROI必须按精确像素中心筛选，不能借外接框扩张阈值");
            FIMThresholdRegionResult empty=FIMImageOperations.ThresholdInRegion(
                image,100,200,new FIMPixelRegion(1,1,2,2));
            Assert(empty.PixelCount==0 && !empty.Bounds.HasValue &&
                empty.Mask.GetGrayValue(1,1)==0,"空阈值区域不能伪造面积或Bounds");
            Throws<ArgumentOutOfRangeException>(()=>FIMImageOperations.ThresholdInRegion(
                image,10,5,new FIMPixelRegion(0,0,1,1)),"阈值上下限倒置必须失败");
            Throws<ArgumentOutOfRangeException>(()=>FIMImageOperations.ThresholdInRegion(
                image,double.NaN,10,new FIMPixelRegion(0,0,1,1)),"NaN阈值必须失败");
            using(CancellationTokenSource thresholdCancelled=new CancellationTokenSource())
            {
                thresholdCancelled.Cancel();
                Throws<OperationCanceledException>(()=>FIMImageOperations.ThresholdInRegion(
                    image,1,16,new FIMPixelRegion(0,0,3,3),null,
                    thresholdCancelled.Token),"已取消阈值分割不能提交掩码");
            }
            FIMRegionGrowingResult grown=FIMImageOperations.RegionGrow(
                image,new FIMPixelRegion(0,1,3,1),1,1,1);
            Assert(grown.PixelCount==3 && grown.Bounds.Left==0 &&
                grown.Bounds.Right==2 && grown.Bounds.Top==1 &&
                grown.Mask.PixelFormat==FIMPixelFormat.Gray8 &&
                grown.Mask.GetGrayValue(0,1)==255 &&
                grown.Mask.GetGrayValue(3,1)==0 &&
                grown.Mask.GetGrayValue(1,0)==0 &&
                image.GetGrayValue(1,1)==6,
                "区域生长以原图种子5/6/7三像素连通而不污染源图或扩大ROI");
            IReadOnlyList<IReadOnlyList<FGEPoint2D>> lineContours=FIMImageMaskContours.Trace(grown.Mask);
            Assert(lineContours.Count==1 && lineContours[0].Count==4 &&
                Near(FIMImageMaskContours.SignedArea(lineContours[0]),3),
                "三像素横线应有面积3的四角真实轮廓");
            FIMImageBuffer donut=new FIMImageBuffer(3,3,FIMPixelFormat.Gray8,
                new byte[]{255,255,255,255,0,255,255,255,255});
            IReadOnlyList<IReadOnlyList<FGEPoint2D>> rings=FIMImageMaskContours.Trace(donut);
            Assert(rings.Count==2 &&
                Near(rings.Sum(x=>FIMImageMaskContours.SignedArea(x)),8) &&
                rings.Any(x=>Near(FIMImageMaskContours.SignedArea(x),9)) &&
                rings.Any(x=>Near(FIMImageMaskContours.SignedArea(x),-1)),
                "孔洞内外轮廓须分别保留，真实面积为8非9");
            FIMImageBuffer cornersOnly=new FIMImageBuffer(2,2,FIMPixelFormat.Gray8,
                new byte[]{255,0,0,255});
            IReadOnlyList<IReadOnlyList<FGEPoint2D>> separate=FIMImageMaskContours.Trace(cornersOnly);
            Assert(separate.Count==2 && separate.All(x=>
                x.Count==4 && Near(FIMImageMaskContours.SignedArea(x),1)),
                "只在角接触的区域不能错误合成自交轮廓");
            Assert(FIMImageMaskContours.Trace(empty.Mask).Count==0,
                "零命中掩码没有轮廓");
            FIMImageBuffer gray16Contour=new FIMImageBuffer(
                1,1,FIMPixelFormat.Gray16,new byte[]{1,0});
            Throws<ArgumentException>(()=>FIMImageMaskContours.Trace(gray16Contour),
                "非Gray8不能追踪掩码边界");
            FIMImageBuffer invalidBinary=new FIMImageBuffer(
                1,1,FIMPixelFormat.Gray8,new byte[]{128});
            Throws<ArgumentException>(()=>FIMImageMaskContours.Trace(invalidBinary),
                "只有0/255二值掩码可以追踪轮廓");
            Throws<InvalidOperationException>(()=>FIMImageMaskContours.Trace(donut,8),
                "边界边数限制须拒绝过度复杂ROI");
            using(CancellationTokenSource cancelledContour=new CancellationTokenSource())
            {
                cancelledContour.Cancel();
                Throws<OperationCanceledException>(()=>FIMImageMaskContours.Trace(
                    donut,100000,cancelledContour.Token),"轮廓取消");
            }
            FIMImageBuffer diagonalSeeds=new FIMImageBuffer(3,3,FIMPixelFormat.Gray8,
                new byte[]{1,99,99,99,1,99,99,99,1});
            FIMPixelRegion nine=new FIMPixelRegion(0,0,2,2);
            Assert(FIMImageOperations.RegionGrow(diagonalSeeds,nine,1,1,0).PixelCount==1 &&
                FIMImageOperations.RegionGrow(diagonalSeeds,nine,1,1,0,true).PixelCount==3,
                "四邻域/八邻域区域生长连通性必须真实不同");
            Throws<ArgumentOutOfRangeException>(()=>FIMImageOperations.RegionGrow(
                image,new FIMPixelRegion(0,0,1,1),2,2,0),
                "种子越出ROI必须拒绝");
            Throws<ArgumentOutOfRangeException>(()=>FIMImageOperations.RegionGrow(
                image,new FIMPixelRegion(0,0,3,3),1,1,-1),
                "负容差不允许假结果");
            using(CancellationTokenSource cancelledGrowth=new CancellationTokenSource())
            {
                cancelledGrowth.Cancel();
                Throws<OperationCanceledException>(()=>FIMImageOperations.RegionGrow(
                    image,new FIMPixelRegion(0,0,3,3),1,1,1,false,
                    cancelledGrowth.Token),"已取消的区域生长不应分配有效结果");
            }
            FIMImageAnalysisResult stats = FIMImageAnalyzer.Analyze(image, 16);
            Assert(stats.Statistics.PixelCount == 16 && stats.Statistics.Min == 1 &&
                stats.Statistics.Max == 16 && Near(stats.Statistics.Mean, 8.5), "原始统计");
            Assert(Near(stats.Statistics.StandardDeviation, Math.Sqrt(21.25)), "Welford方差");
            long total = 0;
            foreach (long count in stats.Histogram.Bins) total += count;
            Assert(total == 16 && stats.Histogram.Bins[0] == 1 &&
                stats.Histogram.Bins[15] == 1, "直方图末区间");
            // 非矩形ROI：只统计精确掩膜包含的原图像素中心，而不是整块外接矩形。
            FIMImageAnalysisResult diagonal=FIMImageAnalyzer.AnalyzeMasked(
                image,16,new FIMPixelRegion(0,0,3,3),(x,y)=>x==y);
            Assert(diagonal.Statistics.PixelCount==4 &&
                Near(diagonal.Statistics.Mean,8.5) &&
                diagonal.Statistics.Min==1 && diagonal.Statistics.Max==16,
                "精确掩膜只包含主对角四个原始像素");
            long maskedBins=0;
            foreach(long count in diagonal.Histogram.Bins)maskedBins+=count;
            Assert(maskedBins==4,"ROI统计像素数与直方图计数必须一致");
            Throws<InvalidOperationException>(()=>FIMImageAnalyzer.AnalyzeMasked(
                image,16,new FIMPixelRegion(0,0,3,3),(x,y)=>false),
                "空区域拒绝伪造全图统计结果");
            Throws<ArgumentNullException>(()=>FIMImageAnalyzer.AnalyzeMasked(
                image,16,new FIMPixelRegion(0,0,3,3),null),
                "空ROI过滤器拒绝");
            using(CancellationTokenSource stopped=new CancellationTokenSource())
            {
                stopped.Cancel();
                Throws<OperationCanceledException>(()=>FIMImageAnalyzer.AnalyzeMasked(
                    image,16,new FIMPixelRegion(0,0,3,3),
                    (x,y)=>true,stopped.Token),"ROI耗时任务支持取消");
            }
            FIMImageBuffer constant = new FIMImageBuffer(2, 2, FIMPixelFormat.Gray8, new byte[] { 5, 5, 5, 5 });
            Assert(FIMImageAnalyzer.GetHistogram(constant, 4).Bins[0] == 4, "常量直方图");
            FIMImageStatistics roi = FIMImageAnalyzer.Calculate(image, new FIMPixelRegion(1, 1, 2, 2));
            Assert(roi.PixelCount == 4 && Near(roi.Mean, 8.5), "离散ROI");
            Assert(new FIMPixelRegion(3, 3, 9, 9).ClipTo(image).Width == 1, "ROI裁剪");
            FIMFiveRegionResult five = FIMImageAnalyzer.CalculateFiveRegions(image, 1, 1, 0);
            Assert(five.Means[0] == 1 && five.Means[1] == 4 && five.Means[2] == 13 &&
                five.Means[3] == 16 && five.Means[4] == 6, "HALCON五区域顺序");
            FIMLinePairResult parallel=FIMImageMeasurement.MeasureLinePair(
                new FGEPoint2D(0,0),new FGEPoint2D(4,0),
                new FGEPoint2D(1,3),new FGEPoint2D(3,3),
                new FIMPixelSpacing(0.2,0.5));
            Assert(Near(parallel.DistancePixels,3) &&
                Near(parallel.DistanceMillimeters.Value,1.5) &&
                Near(parallel.AngleDegrees,0) &&
                Near(parallel.ClosestOnFirst.Y,0) &&
                Near(parallel.ClosestOnSecond.Y,3),
                "旧Ls2Ls平行有限线段最近点及非等距标定");
            FIMPixelSpacing anisotropicSpacing=new FIMPixelSpacing(10,1);
            FIMLinePairResult anisotropic=FIMImageMeasurement.MeasureLinePair(
                new FGEPoint2D(0,0),new FGEPoint2D(10,10),
                new FGEPoint2D(0,6),new FGEPoint2D(2,6),anisotropicSpacing);
            double incorrectConversion=FIMImageMeasurement.GetDistanceMillimeters(
                anisotropic.ClosestOnFirst,anisotropic.ClosestOnSecond,
                anisotropicSpacing).Value;
            Assert(anisotropic.DistancePixels>2.8 &&
                anisotropic.DistancePixels<2.9 &&
                anisotropic.DistanceMillimeters.Value>3.9 &&
                anisotropic.DistanceMillimeters.Value<4.1 &&
                incorrectConversion>15,
                "非等距标定必须重新搜索物理最近点，不允许换算像素最近点冒充毫米最短距离");
            FIMLinePairResult anglePair=FIMImageMeasurement.MeasureLinePair(
                new FGEPoint2D(0,0),new FGEPoint2D(1,1),
                new FGEPoint2D(0,0),new FGEPoint2D(1,-1),
                new FIMPixelSpacing(2,1));
            Assert(Near(anglePair.AngleDegrees,90) &&
                anglePair.PhysicalAngleDegrees.HasValue &&
                Math.Abs(anglePair.PhysicalAngleDegrees.Value-53.1301023542)<1e-6,"非等距标定必须区分像素90°和物理53.13°");
            FIMLinePairResult crossing=FIMImageMeasurement.MeasureLinePair(
                new FGEPoint2D(0,0),new FGEPoint2D(4,4),
                new FGEPoint2D(0,4),new FGEPoint2D(4,0));
            Assert(Near(crossing.DistancePixels,0) &&
                Near(crossing.AngleDegrees,90) &&
                Near(crossing.ClosestOnFirst.X,2) &&
                Near(crossing.ClosestOnSecond.Y,2) &&
                !crossing.DistanceMillimeters.HasValue,"相交线段返回真实交点、距离0和夹角90°");
            FIMLinePairResult separated=FIMImageMeasurement.MeasureLinePair(
                new FGEPoint2D(0,0),new FGEPoint2D(1,0),
                new FGEPoint2D(3,-1),new FGEPoint2D(3,1));
            Assert(Near(separated.DistancePixels,2) &&
                Near(separated.ClosestOnFirst.X,1) &&
                Near(separated.ClosestOnSecond.X,3) &&
                Near(separated.AngleDegrees,90),
                "分离有限线段需按端点最近距离，不能按无限直线交点");
            Throws<ArgumentException>(()=>FIMImageMeasurement.MeasureLinePair(
                new FGEPoint2D(1,1),new FGEPoint2D(1,1),
                new FGEPoint2D(0,0),new FGEPoint2D(2,2)),"退化直线拒绝伪造角度");
            Throws<ArgumentOutOfRangeException>(()=>FIMImageMeasurement.MeasureLinePair(
                new FGEPoint2D(0,0),new FGEPoint2D(1,0),
                new FGEPoint2D(0,1),new FGEPoint2D(1,1),default(FIMPixelSpacing)),
                "未初始化物理标定不能伪造mm测量");
            IReadOnlyList<double> line = FIMImageAnalyzer.GetLineProfile(image, 0, 0, 3, 0);
            Assert(line.Count == 4 && line[3] == 4, "灰度剖面");
            FIMImageBuffer crop = FIMImageOperations.Crop(image, new FIMPixelRegion(1, 1, 2, 2));
            Assert(crop.Width == 2 && crop.Height == 2 &&
                crop.GetGrayValue(0, 0) == 6 && crop.GetGrayValue(1, 1) == 11, "裁剪");
            FIMImageBuffer merged = FIMImageOperations.MergeVertically(FIMImageOperations.SplitVertically(image, 3));
            Assert(merged.Height == 4 && merged.GetGrayValue(3, 3) == 16, "余数行拼接");
            FIMImageBuffer gray16 = new FIMImageBuffer(2, 1, FIMPixelFormat.Gray16, new byte[] { 0, 0, 0, 4 });
            Assert(gray16.GetGrayValue(1, 0) == 1024 &&
                FIMImageOperations.WindowToGray8(gray16, 0, 1024).GetGrayValue(1, 0) == 255 &&
                gray16.GetGrayValue(1, 0) == 1024, "16位数据与窗口隔离");
            FIMImageBuffer normalized = FIMImageOperations.NormalizeToGray8(gray16);
            Assert(normalized.GetGrayValue(0, 0) == 0 && normalized.GetGrayValue(1, 0) == 255,
                "Gray16全范围归一化");
            FIMImageBuffer bgr = FIMImageOperations.Gray16ToBgr24(gray16);
            Assert(bgr.PixelFormat == FIMPixelFormat.Bgr24 && bgr.GetGrayValue(1, 0) == 255,
                "Gray16转BGR24");
            FIMImageBuffer mono = FIMImageOperations.Gray8ToBgr24(
                new FIMImageBuffer(1, 1, FIMPixelFormat.Gray8, new byte[] { 27 }));
            Assert(Near(mono.GetGrayValue(0, 0), 27), "Gray8三通道复制");
            FIMImageBuffer allSame = FIMImageOperations.NormalizeToGray8(constant);
            Assert(allSame.GetGrayValue(0, 0) == 0, "常量图不除零");
            FIMImageBuffer gray1000 = new FIMImageBuffer(1, 1, FIMPixelFormat.Gray16, new byte[] { 232, 3 });
            FIMImageBuffer gray2000 = new FIMImageBuffer(1, 1, FIMPixelFormat.Gray16, new byte[] { 208, 7 });
            FIMImageBuffer averaged = FIMImageOperations.AverageGray(new[] { gray1000, gray2000 });
            Assert(averaged.PixelFormat == FIMPixelFormat.Gray16 && averaged.GetGrayValue(0, 0) == 1500,
                "Gray16均值融合保留位深");
            Throws<ArgumentException>(() => FIMImageOperations.AverageGray(new[] { gray16, gray1000 }),
                "均值融合尺寸不一致");
            Assert(gray1000.GetGrayValue(0, 0) == 1000 && gray2000.GetGrayValue(0, 0) == 2000,
                "图像融合不修改输入");

            FIMImageBuffer padded = new FIMImageBuffer(2, 2, FIMPixelFormat.Gray8, new byte[] { 1,2,0,3,4,0 }, 3);
            Assert(Near(FIMImageAnalyzer.Calculate(padded).Mean, 2.5), "Stride");
            FIMImageBuffer red = new FIMImageBuffer(1, 1, FIMPixelFormat.Bgr24, new byte[] { 0, 0, 255 });
            Assert(Near(red.GetGrayValue(0, 0), 76.245), "BGR亮度顺序");
            FIMImageBuffer nan = new FIMImageBuffer(1, 1, FIMPixelFormat.GrayFloat32, BitConverter.GetBytes(float.NaN));
            Throws<InvalidOperationException>(() => FIMImageAnalyzer.Calculate(nan), "NaN");
            Throws<ArgumentOutOfRangeException>(() => FIMImageAnalyzer.Analyze(image, 0), "非法bin");
            Throws<ArgumentOutOfRangeException>(() => FIMImageAnalyzer.Calculate(image, new FIMPixelRegion(8, 8, 9, 9)), "越界ROI");
            Throws<ArgumentException>(() => new FIMImageBuffer(2, 2, FIMPixelFormat.Gray8, new byte[3]), "短缓冲");
            var cancel = new CancellationTokenSource();
            cancel.Cancel();
            Throws<OperationCanceledException>(() => FIMImageAnalyzer.Calculate(image, null, cancel.Token), "取消");
            FGEPoint2D a = new FGEPoint2D(0, 0);
            FGEPoint2D b = new FGEPoint2D(3, 4);
            FIMPixelSpacing spacing = new FIMPixelSpacing(0.2, 0.4);
            Assert(Near(FIMImageMeasurement.GetDistancePixels(a, b), 5), "像素距离");
            Assert(Near(FIMImageMeasurement.GetDistanceMillimeters(a, b, spacing).Value,
                Math.Sqrt(9 * 0.04 + 16 * 0.16)), "非等距标定测量");
            Assert(!FIMImageMeasurement.GetDistanceMillimeters(a, b, null).HasValue,
                "未标定不造物理量");
            Assert(Near(FIMImageMeasurement.GetAngleDegrees(
                new FGEPoint2D(1, 0), a, new FGEPoint2D(0, 1)), 90), "直角测量");
            Assert(FIMImageMeasurement.GetAreaPixels(new FIMPixelRegion(0, 0, 1, 1)) == 4,
                "闭区间面积");
            Assert(Near(FIMImageMeasurement.GetAreaSquareMillimeters(
                new FIMPixelRegion(0, 0, 1, 1), spacing).Value, 0.32), "物理面积");
            Assert(!FIMImageMeasurement.GetAreaSquareMillimeters(
                new FIMPixelRegion(0, 0, 1, 1), null).HasValue, "无标定面积为空");
            Assert(Near(FIMImageMeasurement.GetHorizontalDistancePixels(a,b),3) &&
                Near(FIMImageMeasurement.GetVerticalDistancePixels(a,b),4), "水平垂直像素");
            Assert(Near(FIMImageMeasurement.GetHorizontalDistanceMillimeters(a,b,spacing).Value,.6) &&
                Near(FIMImageMeasurement.GetVerticalDistanceMillimeters(a,b,spacing).Value,1.6),
                "各轴独立物理标定");
            FGERect2D geometry = new FGERect2D(1,2,3,4);
            Assert(Near(FIMImageMeasurement.GetRectangleAreaPixels(geometry),12) &&
                Near(FIMImageMeasurement.GetRectangleAreaSquareMillimeters(geometry,spacing).Value,.96),
                "几何面积与非等距标定");
            FGEPoint2D[] triangle = { new FGEPoint2D(0,0),new FGEPoint2D(4,0),
                new FGEPoint2D(0,3) };
            Assert(Near(FIMImageMeasurement.GetPolygonAreaPixels(triangle),6) &&
                Near(FIMImageMeasurement.GetPolygonAreaSquareMillimeters(triangle,spacing).Value,.48),
                "多边形原图鞋带面积");
            Throws<ArgumentException>(()=>FIMImageMeasurement.GetPolygonAreaPixels(new []{a,b}),
                "顶点数不足");
            Throws<ArgumentOutOfRangeException>(() => new FIMPixelSpacing(0, 0.1), "非法间距");
            Throws<ArgumentException>(() => FIMImageMeasurement.GetAngleDegrees(a, a, b), "退化角度");
            Throws<ArgumentOutOfRangeException>(() => FIMImageMeasurement.GetDistanceMillimeters(a, b, default(FIMPixelSpacing)), "default标定间距");
            Throws<ArgumentOutOfRangeException>(() => FIMImageMeasurement.GetAreaSquareMillimeters(new FIMPixelRegion(0, 0, 1, 1), default(FIMPixelSpacing)), "default面积标定");
            // 工业RAW负例：长度、位深、大端、非Seek流边界
            using (MemoryStream raw16 = new MemoryStream(new byte[] { 1, 0, 0, 4 }))
            {
                FIMImageBuffer rawImage = FIMRawImageReader.Load(raw16, 2, 1, FIMPixelFormat.Gray16);
                Assert(rawImage.GetGrayValue(0, 0) == 1 && rawImage.GetGrayValue(1, 0) == 1024,
                    "RAW16保留原精度");
            }
            using (MemoryStream bigEndian = new MemoryStream(new byte[] { 0, 1, 4, 0 }))
            {
                FIMImageBuffer rawImage = FIMRawImageReader.Load(bigEndian, 2, 1,
                    FIMPixelFormat.Gray16, 0, true);
                Assert(rawImage.GetGrayValue(0, 0) == 1 && rawImage.GetGrayValue(1, 0) == 1024,
                    "大端RAW16转换");
            }
            Throws<InvalidDataException>(() =>
                FIMRawImageReader.Load(new MemoryStream(new byte[5]), 2, 1, FIMPixelFormat.Gray16),
                "RAW多余字节");
            Throws<InvalidDataException>(() =>
                FIMRawImageReader.Load(new MemoryStream(new byte[3]), 2, 1, FIMPixelFormat.Gray16),
                "RAW短文件");
            Throws<ArgumentOutOfRangeException>(() =>
                FIMRawImageReader.Load(new MemoryStream(new byte[4]), 2, 1, FIMPixelFormat.Gray16, 3),
                "RAW错误Stride");
            FIMImageBuffer small = new FIMImageBuffer(2, 2, FIMPixelFormat.Gray8,
                new byte[] { 1,2,3,4 });
            FIMImageBuffer rotated = FIMImageOperations.Rotate90Clockwise(small);
            Assert(rotated.GetGrayValue(0,0) == 3 && rotated.GetGrayValue(1,0) == 1 &&
                rotated.GetGrayValue(0,1) == 4, "顺时针旋转映射");
            FIMImageBuffer rotated180 = FIMImageOperations.Rotate180(small);
            Assert(rotated180.GetGrayValue(0,0) == 4 && rotated180.GetGrayValue(1,1) == 1,
                "180度一次像素映射");
            FIMImageBuffer restored = FIMImageOperations.Rotate90Counterclockwise(rotated);
            Assert(restored.GetGrayValue(0,0) == 1 && restored.GetGrayValue(1,1) == 4,
                "顺逆变换");
            Assert(FIMImageOperations.FlipHorizontal(small).GetGrayValue(0,0) == 2 &&
                FIMImageOperations.FlipVertical(small).GetGrayValue(0,0) == 3,
                "双轴镜像");
            FIMImageBuffer mask = FIMImageOperations.Threshold(gray16, 1024, 1024);
            Assert(mask.GetGrayValue(0,0) == 0 && mask.GetGrayValue(1,0) == 255,
                "16位原灰度阈值");
            FIMImageBuffer difference = FIMImageOperations.DifferenceGray(gray16,
                new FIMImageBuffer(2,1,FIMPixelFormat.Gray16,new byte[] { 0,0,255,3 }));
            Assert(difference.PixelFormat == FIMPixelFormat.Gray16 &&
                difference.GetGrayValue(1,0) == 1, "16位图差分");
            Throws<ArgumentException>(() =>
                FIMImageOperations.DifferenceGray(gray16,small), "差分尺寸位深不同");
            Assert(small.GetGrayValue(0,0) == 1, "几何变换不修改原图");
            FIMImageBuffer impulse = new FIMImageBuffer(3, 3, FIMPixelFormat.Gray8,
                new byte[] { 0,0,0, 0,90,0, 0,0,0 });
            FIMImageBuffer mean = FIMImageFilters.Apply(impulse, FIMNeighborhoodFilter.Mean, 1);
            FIMImageBuffer median = FIMImageFilters.Apply(impulse, FIMNeighborhoodFilter.Median, 1);
            FIMImageBuffer maxFilter = FIMImageFilters.Apply(impulse, FIMNeighborhoodFilter.Maximum, 1);
            Assert(mean.GetGrayValue(1,1) == 10 && median.GetGrayValue(1,1) == 0 &&
                maxFilter.GetGrayValue(0,0) == 90, "去噪均值中值与膨胀");
            FIMImageBuffer blurred = FIMImageFilters.GaussianBlur(impulse, 1.0, 1);
            Assert(blurred.GetGrayValue(1,1) < 90 && blurred.GetGrayValue(1,1) > 0,
                "高斯卷积");
            FIMImageBuffer sharp = FIMImageFilters.UnsharpMask(blurred, 1.0, 1, 0.5);
            Assert(sharp.GetGrayValue(1,1) >= blurred.GetGrayValue(1,1), "锐化");
            Assert(impulse.GetGrayValue(1,1) == 90, "滤波保持源不变");
            Throws<ArgumentOutOfRangeException>(() =>
                FIMImageFilters.GaussianBlur(impulse, 0, 1), "非法高斯参数");
            Throws<NotSupportedException>(() =>
                FIMImageFilters.Apply(red, FIMNeighborhoodFilter.Mean, 1), "彩色未支持拒绝");
            Throws<ArgumentOutOfRangeException>(() =>
                FIMImageFilters.Apply(impulse, FIMNeighborhoodFilter.Mean, 20), "非法大半径");
            Console.WriteLine("PASS FIM Imaging 像素/ROI/统计/五区域/裁剪/异常");
        }

        private static bool Near(double x, double y) { return Math.Abs(x - y) < 0.0000001; }
        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FIM失败：" + name);
        }
        private static void Throws<T>(Action action, string name) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("FIM未触发预期异常：" + name);
        }
    }
}
