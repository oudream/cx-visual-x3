using System;
using System.Collections.Generic;
using System.IO;
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
            FIMImageAnalysisResult stats = FIMImageAnalyzer.Analyze(image, 16);
            Assert(stats.Statistics.PixelCount == 16 && stats.Statistics.Min == 1 &&
                stats.Statistics.Max == 16 && Near(stats.Statistics.Mean, 8.5), "原始统计");
            Assert(Near(stats.Statistics.StandardDeviation, Math.Sqrt(21.25)), "Welford方差");
            long total = 0;
            foreach (long count in stats.Histogram.Bins) total += count;
            Assert(total == 16 && stats.Histogram.Bins[0] == 1 &&
                stats.Histogram.Bins[15] == 1, "直方图末区间");
            FIMImageBuffer constant = new FIMImageBuffer(2, 2, FIMPixelFormat.Gray8, new byte[] { 5, 5, 5, 5 });
            Assert(FIMImageAnalyzer.GetHistogram(constant, 4).Bins[0] == 4, "常量直方图");
            FIMImageStatistics roi = FIMImageAnalyzer.Calculate(image, new FIMPixelRegion(1, 1, 2, 2));
            Assert(roi.PixelCount == 4 && Near(roi.Mean, 8.5), "离散ROI");
            Assert(new FIMPixelRegion(3, 3, 9, 9).ClipTo(image).Width == 1, "ROI裁剪");
            FIMFiveRegionResult five = FIMImageAnalyzer.CalculateFiveRegions(image, 1, 1, 0);
            Assert(five.Means[0] == 1 && five.Means[1] == 4 && five.Means[2] == 13 &&
                five.Means[3] == 16 && five.Means[4] == 6, "HALCON五区域顺序");
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
