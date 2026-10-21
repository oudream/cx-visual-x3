using HalconDotNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule
{
    // 灰度值相关
    public static partial class HalconHelper
    {

        /// <summary>
        /// 获取图像的基本信息，包括宽度、高度、通道数、最小灰度值和最大灰度值
        /// </summary>
        /// <param name="image">输入的HObject对象</param>
        /// <returns>包含宽度、高度、通道数、最小灰度值和最大灰度值的元组</returns>
        public static (double, double) GetMinMaxGray(HObject image)
        {
            // 获取图像的最小和最大灰度值
            HTuple minGray, maxGray;
            HOperatorSet.MinMaxGray(image, image, 0, out minGray, out maxGray, out _);

            return (minGray.D, maxGray.D);
        }

        /// <summary>
        /// 计算：左上/右上/左下/右下/中心 五个矩形ROI的平均灰度
        /// </summary>
        /// <param name="image">输入灰度图（建议单通道，如 uint2/int2/byte 等）</param>
        /// <param name="boxWidth">ROI宽（像素）</param>
        /// <param name="boxHeight">ROI高（像素）</param>
        /// <param name="margin">距离边缘的内缩（像素）</param>
        /// <param name="cornerMean">输出：4个角均值，顺序 LU, RU, LD, RD</param>
        /// <param name="centerMean">输出：中心均值</param>
        /// <param name="roiLU">可选输出：左上ROI</param>
        /// <param name="roiRU">可选输出：右上ROI</param>
        /// <param name="roiLD">可选输出：左下ROI</param>
        /// <param name="roiRD">可选输出：右下ROI</param>
        /// <param name="roiC">可选输出：中心ROI</param>
        public static void Calc5BoxMeanGray(
            HObject image,
            int boxWidth,
            int boxHeight,
            int margin,
            out double[] cornerMean,
            out double centerMean,
            out HObject roiLU,
            out HObject roiRU,
            out HObject roiLD,
            out HObject roiRD,
            out HObject roiC)
        {
            cornerMean = new double[4];
            centerMean = 0;

            roiLU = null; roiRU = null; roiLD = null; roiRD = null; roiC = null;

            if (image == null || !image.IsInitialized())
            {
                throw new ArgumentException("image is null or not initialized.");
            }
            if (boxWidth <= 0 || boxHeight <= 0)
            {
                throw new ArgumentException("boxWidth/boxHeight must be > 0.");
            }
            if (margin < 0) margin = 0;

            HTuple w, h;
            HOperatorSet.GetImageSize(image, out w, out h);

            // Halcon 坐标：row(0..h-1), col(0..w-1)
            double imgW = w.D;
            double imgH = h.D;

            double halfW = boxWidth / 2.0;
            double halfH = boxHeight / 2.0;

            // 角点中心位置（再由中心推 rectangle1 的上下左右）
            // 左上
            double luRow = margin + halfH;
            double luCol = margin + halfW;

            // 右上
            double ruRow = margin + halfH;
            double ruCol = (imgW - 1 - margin) - halfW;

            // 左下
            double ldRow = (imgH - 1 - margin) - halfH;
            double ldCol = margin + halfW;

            // 右下
            double rdRow = (imgH - 1 - margin) - halfH;
            double rdCol = (imgW - 1 - margin) - halfW;

            // 中心
            double cRow = (imgH - 1) / 2.0;
            double cCol = (imgW - 1) / 2.0;

            // clamp，避免 ROI 出界
            ClampCenter(ref luRow, ref luCol, imgH, imgW, halfH, halfW);
            ClampCenter(ref ruRow, ref ruCol, imgH, imgW, halfH, halfW);
            ClampCenter(ref ldRow, ref ldCol, imgH, imgW, halfH, halfW);
            ClampCenter(ref rdRow, ref rdCol, imgH, imgW, halfH, halfW);
            ClampCenter(ref cRow, ref cCol, imgH, imgW, halfH, halfW);

            // 生成ROI
            roiLU = GenRectByCenter(luRow, luCol, halfH, halfW);
            roiRU = GenRectByCenter(ruRow, ruCol, halfH, halfW);
            roiLD = GenRectByCenter(ldRow, ldCol, halfH, halfW);
            roiRD = GenRectByCenter(rdRow, rdCol, halfH, halfW);
            roiC = GenRectByCenter(cRow, cCol, halfH, halfW);

            // 计算均值
            cornerMean[0] = MeanGray(image, roiLU);
            cornerMean[1] = MeanGray(image, roiRU);
            cornerMean[2] = MeanGray(image, roiLD);
            cornerMean[3] = MeanGray(image, roiRD);
            centerMean = MeanGray(image, roiC);
        }

        private static void ClampCenter(ref double row, ref double col, double imgH, double imgW, double halfH, double halfW)
        {
            // row in [halfH, (imgH-1)-halfH]
            double minRow = halfH;
            double maxRow = (imgH - 1) - halfH;
            if (row < minRow) row = minRow;
            if (row > maxRow) row = maxRow;

            // col in [halfW, (imgW-1)-halfW]
            double minCol = halfW;
            double maxCol = (imgW - 1) - halfW;
            if (col < minCol) col = minCol;
            if (col > maxCol) col = maxCol;
        }

        private static HObject GenRectByCenter(double centerRow, double centerCol, double halfH, double halfW)
        {
            HObject rect;
            HOperatorSet.GenRectangle1(out rect,
                centerRow - halfH,
                centerCol - halfW,
                centerRow + halfH,
                centerCol + halfW);
            return rect;
        }

        private static double MeanGray(HObject image, HObject roi)
        {
            HObject reduced;
            HOperatorSet.ReduceDomain(image, roi, out reduced);

            HTuple mean, dev;
            HOperatorSet.Intensity(roi, reduced, out mean, out dev);

            // 释放
            reduced.Dispose();
            dev.Dispose();

            double v = 0;
            if (mean != null && mean.Length > 0) v = mean.D;
            mean.Dispose();
            return v;
        }

        /// <summary>
        /// 自动根据图像大小计算 boxWidth/boxHeight/margin，然后计算 4角+中心 五个矩形ROI的平均灰度。
        /// 注意：本函数不输出ROI，内部会生成ROI并在函数结束前释放。
        /// </summary>
        /// <param name="image">输入灰度图</param>
        /// <param name="cornerMean">输出：4个角均值，顺序 LU, RU, LD, RD</param>
        /// <param name="centerMean">输出：中心均值</param>
        public static string Calc5BoxMeanGrayAutoParam(
            HObject image,
            out double[] cornerMean,
            out double centerMean)
        {
            cornerMean = new double[4];
            centerMean = 0;

            if (image == null || !image.IsInitialized())
                return "image is null or not initialized.";

            // 1) 取图像大小
            HTuple wT, hT;
            HOperatorSet.GetImageSize(image, out wT, out hT);
            int imgW = wT.I;
            int imgH = hT.I;

            // 2) 自动参数（比例 + clamp）
            // 你可以按现场再调这三个比例：ROI大小与边距
            int boxW = Clamp((int)Math.Round(imgW * 0.08), min: 80, max: 260);
            int boxH = Clamp((int)Math.Round(imgH * 0.08), min: 60, max: 220);
            int margin = Clamp((int)Math.Round(Math.Min(imgW, imgH) * 0.04), min: 30, max: 180);

            // 3) 生成ROI并算均值（ROI不对外输出，内部释放）
            HObject roiLU = null, roiRU = null, roiLD = null, roiRD = null, roiC = null;
            try
            {
                Calc5BoxMeanGray(
                    image,
                    boxW, boxH, margin,
                    out cornerMean,
                    out centerMean,
                    out roiLU, out roiRU, out roiLD, out roiRD, out roiC);
                return string.Empty;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                roiLU?.Dispose();
                roiRU?.Dispose();
                roiLD?.Dispose();
                roiRD?.Dispose();
                roiC?.Dispose();
                wT?.Dispose();
                hT?.Dispose();
            }
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

    }

}
