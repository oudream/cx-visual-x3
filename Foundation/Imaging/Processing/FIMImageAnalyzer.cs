using System;
using System.Collections.Generic;
using System.Threading;
using CxVisual.Foundation.Numerics;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>原始像素统计、直方图、灰度剖面与五区域灰度值。</summary>
    public static class FIMImageAnalyzer
    {
        public static FIMImageStatistics Calculate(FIMImageBuffer image, FIMPixelRegion? roi = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (image == null) throw new ArgumentNullException("image");
            // ROI窗值/统计只需要统计量，Gray8/16也复用一次扫描整数频数，
            // 避免6144x3072大图在UI上走逐像素Welford慢路径。
            if (image.PixelFormat==FIMPixelFormat.Gray8 ||
                image.PixelFormat==FIMPixelFormat.Gray16)
                return AnalyzeInteger(image,256,roi,cancellationToken).Statistics;
            FIMPixelRegion r = roi.HasValue ? roi.Value.ClipTo(image) : FIMPixelRegion.Entire(image);
            var accumulator = new FNURunningStatistics();
            for (int y = r.Top; y <= r.Bottom; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = r.Left; x <= r.Right; x++)
                {
                    if ((x & 2047) == 0) cancellationToken.ThrowIfCancellationRequested();
                    double value = image.GetGrayValue(x, y);
                    if (double.IsNaN(value) || double.IsInfinity(value))
                        throw new InvalidOperationException("图像包含非有限灰度值。");
                    accumulator.Add(value);
                }
            }
            FNUStatisticsSnapshot result = accumulator.Snapshot();
            return new FIMImageStatistics(result.Count, result.Min, result.Max,
                result.Mean, result.PopulationStandardDeviation);
        }

        public static FIMImageAnalysisResult Analyze(FIMImageBuffer image, int binCount,
            FIMPixelRegion? roi = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (binCount < 1 || binCount > 65536) throw new ArgumentOutOfRangeException("binCount");
            if (image == null) throw new ArgumentNullException("image");
            // 大型Gray8/16单次扫描：65536项整数精确频表代替对图像逐像素
            // 两次GetGrayValue+Welford。保留原始灰度、Stride/ROI及取消语义。
            if (image.PixelFormat == FIMPixelFormat.Gray8 ||
                image.PixelFormat == FIMPixelFormat.Gray16)
                return AnalyzeInteger(image, binCount, roi, cancellationToken);
            FIMImageStatistics stats = Calculate(image, roi, cancellationToken);
            FIMPixelRegion r = roi.HasValue ? roi.Value.ClipTo(image) : FIMPixelRegion.Entire(image);
            long[] bins = new long[binCount];
            double span = stats.Max - stats.Min;
            for (int y = r.Top; y <= r.Bottom; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = r.Left; x <= r.Right; x++)
                {
                    if ((x & 2047) == 0) cancellationToken.ThrowIfCancellationRequested();
                    double value = image.GetGrayValue(x, y);
                    int index = span <= 0 ? 0 : (int)((value - stats.Min) / span * binCount);
                    bins[Math.Max(0, Math.Min(binCount - 1, index))]++;
                }
            }
            return new FIMImageAnalysisResult(stats, new FIMImageHistogram(stats.Min, stats.Max, bins));
        }

        /// <summary>
        /// Gray8/Gray16一次扫描精确原始灰度整数直方图。均值和总体方差从
        /// 频数而非抽样/显示窗计算，ROI边界、行Padding和非8位灰度保持正确。
        /// O(W*H + 65536)，与分箱数量无关；不缓存跨图像的像素或统计。
        /// </summary>
        private static FIMImageAnalysisResult AnalyzeInteger(FIMImageBuffer image,
            int binCount,FIMPixelRegion? roi,CancellationToken cancellationToken)
        {
            FIMPixelRegion r=roi.HasValue?roi.Value.ClipTo(image):
                FIMPixelRegion.Entire(image);
            int depth=image.PixelFormat==FIMPixelFormat.Gray8?256:65536;
            long[] frequency=new long[depth];
            int bpp=image.PixelFormat==FIMPixelFormat.Gray8?1:2;
            byte[] row=new byte[checked(r.Width*bpp)];
            for(int y=r.Top;y<=r.Bottom;y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                image.CopyRow(y,r.Left,r.Width,row,0);
                if(bpp==1)
                {
                    for(int p=0;p<row.Length;p++)
                        frequency[row[p]]++;
                }
                else
                {
                    for(int p=0;p<row.Length;p+=2)
                        frequency[row[p] | (row[p+1]<<8)]++;
                }
            }
            int first=0,last=depth-1;
            while(first<depth && frequency[first]==0)first++;
            while(last>=0 && frequency[last]==0)last--;
            if(first==depth)throw new InvalidOperationException("ROI没有像素。");
            long count=0;
            double sum=0,mean;
            // 先按精确整数频数求均值，再按中心化值求方差，
            // 避免大背景Gray16 E[X²]-E[X]²的灾难性抵消。
            for(int i=first;i<=last;i++)
            {
                if((i&4095)==0)cancellationToken.ThrowIfCancellationRequested();
                long n=frequency[i];
                count=checked(count+n);
                sum+=(double)i*n;
            }
            mean=sum/count;
            double squared=0;
            long[] bins=new long[binCount];
            double span=last-first;
            for(int i=first;i<=last;i++)
            {
                if((i&4095)==0)cancellationToken.ThrowIfCancellationRequested();
                long n=frequency[i];
                if(n==0)continue;
                double d=i-mean;
                squared+=d*d*n;
                int index=span<=0?0:(int)((i-first)/span*binCount);
                bins[Math.Max(0,Math.Min(binCount-1,index))]+=n;
            }
            return new FIMImageAnalysisResult(
                new FIMImageStatistics(count,first,last,mean,Math.Sqrt(squared/count)),
                new FIMImageHistogram(first,last,bins));
        }

        /// <summary>
        /// 按精确ROI掩膜统计原始灰度，包含形状中的像素中心。
        /// 掩膜判断来自上层MVI几何，Foundation不引用WPF/HALCON或OverlayStore。
        /// </summary>
        public static FIMImageAnalysisResult AnalyzeMasked(FIMImageBuffer image,int binCount,
            FIMPixelRegion bounds,Func<int,int,bool> includesPixel,
            CancellationToken cancellationToken=default(CancellationToken))
        {
            if(image==null)throw new ArgumentNullException("image");
            if(includesPixel==null)throw new ArgumentNullException("includesPixel");
            if(binCount<1 || binCount>65536)
                throw new ArgumentOutOfRangeException("binCount");
            FIMPixelRegion r=bounds.ClipTo(image);
            var maskStatistics = new FNURunningStatistics();
            for(int y=r.Top;y<=r.Bottom;y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for(int x=r.Left;x<=r.Right;x++)
                {
                    if((x & 2047)==0)cancellationToken.ThrowIfCancellationRequested();
                    if(!includesPixel(x,y))continue;
                    double value=image.GetGrayValue(x,y);
                    if(double.IsNaN(value)||double.IsInfinity(value))
                        throw new InvalidOperationException("ROI包含非有限灰度值。");
                    maskStatistics.Add(value);
                }
            }
            if(maskStatistics.Count==0)
                throw new InvalidOperationException("所选ROI没有有效像素中心，无法进行灰度统计。");
            FNUStatisticsSnapshot summary=maskStatistics.Snapshot();
            double min=summary.Min,max=summary.Max;
            long[] bins=new long[binCount];
            double span=max-min;
            for(int y=r.Top;y<=r.Bottom;y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for(int x=r.Left;x<=r.Right;x++)
                {
                    if((x & 2047)==0)cancellationToken.ThrowIfCancellationRequested();
                    if(!includesPixel(x,y))continue;
                    double value=image.GetGrayValue(x,y);
                    int index=span<=0?0:(int)((value-min)/span*binCount);
                    bins[Math.Max(0,Math.Min(binCount-1,index))]++;
                }
            }
            return new FIMImageAnalysisResult(
                new FIMImageStatistics(summary.Count,min,max,summary.Mean,
                    summary.PopulationStandardDeviation),
                new FIMImageHistogram(min,max,bins));
        }

        public static FIMImageHistogram GetHistogram(FIMImageBuffer image, int binCount,
            FIMPixelRegion? roi = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Analyze(image, binCount, roi, cancellationToken).Histogram;
        }

        public static IReadOnlyList<double> GetLineProfile(FIMImageBuffer image,
            int startX, int startY, int endX, int endY)
        {
            if (image == null) throw new ArgumentNullException("image");
            long dx = (long)endX - startX, dy = (long)endY - startY;
            long steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            if (steps > 10000000) throw new ArgumentOutOfRangeException("endX");
            int segments = (int)Math.Max(1, steps);
            double[] values = new double[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double ratio = (double)i / segments;
                int x = (int)Math.Max(0, Math.Min(image.Width - 1, Math.Round(startX + ratio * dx)));
                int y = (int)Math.Max(0, Math.Min(image.Height - 1, Math.Round(startY + ratio * dy)));
                values[i] = image.GetGrayValue(x, y);
            }
            return Array.AsReadOnly(values);
        }

        /// <summary>左上、右上、左下、右下、中心；继承旧HALCON五区域通用计算语义。</summary>
        public static FIMFiveRegionResult CalculateFiveRegions(FIMImageBuffer image,
            int boxWidth, int boxHeight, int margin,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (image == null) throw new ArgumentNullException("image");
            if (boxWidth <= 0 || boxWidth > image.Width) throw new ArgumentOutOfRangeException("boxWidth");
            if (boxHeight <= 0 || boxHeight > image.Height) throw new ArgumentOutOfRangeException("boxHeight");
            if (margin < 0) throw new ArgumentOutOfRangeException("margin");
            int maxX = image.Width - boxWidth, maxY = image.Height - boxHeight;
            int nearX = Math.Min(margin, maxX / 2), nearY = Math.Min(margin, maxY / 2);
            int[] xs = { nearX, maxX - nearX, nearX, maxX - nearX, maxX / 2 };
            int[] ys = { nearY, nearY, maxY - nearY, maxY - nearY, maxY / 2 };
            FIMPixelRegion[] regions = new FIMPixelRegion[5];
            double[] means = new double[5];
            for (int i = 0; i < 5; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                regions[i] = new FIMPixelRegion(xs[i], ys[i],
                    xs[i] + boxWidth - 1, ys[i] + boxHeight - 1);
                means[i] = Calculate(image, regions[i], cancellationToken).Mean;
            }
            return new FIMFiveRegionResult(regions, means);
        }
    }
}
