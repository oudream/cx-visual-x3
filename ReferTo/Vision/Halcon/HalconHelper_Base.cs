using CommonInterfaces;
using HalconDotNet;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule
{
    public static partial class HalconHelper
    {
        // format Default value: 'tiff'
        // List of values: 'bigtiff alpha', 'bigtiff deflate 9 alpha', 'bigtiff deflate 9', 'bigtiff jpeg 90',
        //                 'bigtiff lzw alpha ', 'bigtiff lzw', 'bigtiff mask', 'bigtiff packbits', 'bigtiff',
        //                 'bmp', 'hobj', 'ima', 'jp2 20', 'jp2 30', 'jp2 40', 'jp2 50', 'jp2', 'jpeg 100', 'jpeg 20',
        //                 'jpeg 40', 'jpeg 60', 'jpeg 80', 'jpeg', 'jpegxr 20', 'jpegxr 30', 'jpegxr 40', 'jpegxr 50', 'jpegxr',
        //                 'png best', 'png fastest', 'png none', 'png',
        //                 'tiff alpha', 'tiff deflate 9 alpha', 'tiff deflate 9', 'tiff jpeg 90', 'tiff lzw alpha ',
        //                 'tiff lzw', 'tiff mask', 'tiff packbits', 'tiff'
        public static void SaveImage(HObject image, string imageFilePath, string format)
        {
            HOperatorSet.WriteImage(image, format, 0, imageFilePath);
        }

        public static void SaveImage(HObject image, string imageFilePath, int quality = 0)
        {
            string format = GetFormatByExtension(imageFilePath, quality);
            HOperatorSet.WriteImage(image, format, 0, imageFilePath);
        }

        public static HObject ReadImage(string imageFilePath)
        {
            HOperatorSet.ReadImage(out HObject hImage, imageFilePath);
            return hImage;
        }

        // 把图片切成多份
        public static List<HObject> VerticalPartVector(HObject hImage, int imageByProductCount)
        {
            if (imageByProductCount <= 1)
            {
                return new List<HObject> { hImage.Clone() };
            }
            else
            {
                List<HObject> result = new List<HObject>();
                HOperatorSet.GetImageSize(hImage, out HTuple width, out HTuple height);
                for (int i = 0; i < imageByProductCount; i++)
                {
                    int pos_y = (height.I / imageByProductCount) * i;
                    HOperatorSet.CropPart(hImage, out var OutHbject, pos_y, 0, width, height.I / imageByProductCount);
                    result.Add(OutHbject);
                }
                return result;
            }
        }

        /// <summary>
        /// 将纵向拆分得到的 parts（从上到下）合并回一张图。
        /// - parts 中每张图宽/通道/类型应一致（高度可不同）
        /// - 合并位置由 OffsetRow 累加每块高度得到
        /// - 等价于 HDevelop: tile_images_offset(parts, ..., OffsetRow/OffsetCol, row1/col1/row2/col2, widthOut, heightOut)
        /// </summary>
        /// <summary>
        /// 将纵向拆分的 parts 合并为一张图（自上而下）
        /// - 不抛异常
        /// - 成功返回 ""，失败返回错误信息
        /// - outImage 成功时为合并结果，失败时为 null
        /// </summary>
        public static string VerticalMergeVector(IList<HObject> parts, out HObject outImage)
        {
            outImage = null;

            // ---------- 0) 基本检查 ----------
            if (parts == null || parts.Count == 0)
                return "输入图像列表为空";

            if (parts.Count == 1)
            {
                outImage = parts[0].Clone();
                return string.Empty;
            }

            int n = parts.Count;

            try
            {
                // ---------- 1) 基准信息（以第 0 张为准） ----------
                HOperatorSet.GetImageSize(parts[0], out HTuple baseW, out HTuple baseH);
                HOperatorSet.CountChannels(parts[0], out HTuple baseC);
                HOperatorSet.GetImageType(parts[0], out HTuple baseT);

                // ---------- 2) 校验一致性（不抛异常，直接返回错误） ----------
                for (int i = 1; i < n; i++)
                {
                    HOperatorSet.GetImageSize(parts[i], out HTuple wi, out HTuple hi);
                    HOperatorSet.CountChannels(parts[i], out HTuple ci);
                    HOperatorSet.GetImageType(parts[i], out HTuple ti);

                    if (wi.I != baseW.I)
                        return $"宽度不一致：idx=0({baseW.I}) vs idx={i}({wi.I})";

                    if (ci.I != baseC.I)
                        return $"通道数不一致：idx=0({baseC.I}) vs idx={i}({ci.I})";

                    if (ti.S != baseT.S)
                        return $"图像类型不一致：idx=0({baseT.S}) vs idx={i}({ti.S})";
                }

                // ---------- 3) 组织 HObject 向量（tuple of images） ----------
                HObject vec = parts[0].Clone();
                for (int i = 1; i < n; i++)
                {
                    HOperatorSet.ConcatObj(vec, parts[i], out HObject tmp);
                    vec.Dispose();
                    vec = tmp;
                }

                // ---------- 4) OffsetRow / OffsetCol（tuple，长度 = N） ----------
                HTuple offsetRow = new HTuple();
                HTuple offsetCol = new HTuple();

                int rowAcc = 0;
                for (int i = 0; i < n; i++)
                {
                    HOperatorSet.GetImageSize(parts[i], out _, out HTuple hi);

                    offsetRow = offsetRow.TupleConcat(rowAcc);
                    offsetCol = offsetCol.TupleConcat(0);

                    rowAcc += hi.I;
                }

                // ---------- 5) row1 / col1 / row2 / col2（全部 tuple，长度 = N） ----------
                HTuple row1 = new HTuple();
                HTuple col1 = new HTuple();
                HTuple row2 = new HTuple();
                HTuple col2 = new HTuple();

                for (int i = 0; i < n; i++)
                {
                    row1 = row1.TupleConcat(-1);
                    col1 = col1.TupleConcat(-1);
                    row2 = row2.TupleConcat(-1);
                    col2 = col2.TupleConcat(-1);
                }

                // ---------- 6) 输出画布尺寸 ----------
                HTuple widthOut = baseW;
                HTuple heightOut = rowAcc;

                // ---------- 7) tile_images_offset ----------
                HOperatorSet.TileImagesOffset(
                    vec,
                    out outImage,
                    offsetRow,
                    offsetCol,
                    row1,
                    col1,
                    row2,
                    col2,
                    widthOut,
                    heightOut
                );

                vec.Dispose();
                return string.Empty; // 成功
            }
            catch (Exception ex)
            {
                // HALCON / C# 任何异常都兜住
                outImage?.Dispose();
                outImage = null;
                return ex.Message;
            }
        }

        public static string TryReadImage(string imageFilePath, out HObject ho_image)
        {
            ho_image = null;
            try
            {
                HOperatorSet.ReadImage(out ho_image, imageFilePath);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return string.Empty;
        }

        public static HObject ImageToRGB(HObject dst)
        {
            HOperatorSet.Compose3(dst, dst, dst, out HObject result);
            return result;
        }

        public static bool GetImageInfo(string imageFilePath, out int width, out int height, out int channels, out int bitDepth)
        {
            width = 0;
            height = 0;
            channels = 0;
            bitDepth = 0;
            if (File.Exists(imageFilePath))
            {
                HObject image = null;
                try
                {
                    image = ReadImage(imageFilePath);
                    var r = GetImageInfo(image, out width, out height, out channels, out bitDepth, out _);
                    return r;
                }
                finally
                {
                    image?.Dispose();
                }
            }
            return false;
        }

        public static bool GetImageInfo(HObject image, out int width, out int height, out int channels, out int bitDepth, out IntPtr dataPtr)
        {
            if (image != null)
            {
                try
                {
                    // 获取图像的基本信息
                    HOperatorSet.GetImagePointer1(image, out HTuple hPointer, out HTuple hType, out HTuple hWidth, out HTuple hHeight);
                    //Console.WriteLine($"图像宽度: {width}");
                    //Console.WriteLine($"图像高度: {height}");
                    //Console.WriteLine($"图像类型: {type}");
                    width = hWidth.I;
                    height = hHeight.I;

                    dataPtr = hPointer.IP;

                    // 获取图像的通道数
                    HOperatorSet.CountChannels(image, out HTuple hChannels);
                    //Console.WriteLine($"图像通道数: {channels}");
                    channels = hChannels.I;

                    // 获取图像的位深（根据图像类型判断）
                    bitDepth = GetBitDepth(hType);
                    //Console.WriteLine($"图像位深: {bitDepth}");

                    return true;
                }
                catch (Exception)
                {
                }
            }
            width = 0;
            height = 0;
            channels = 0;
            bitDepth = 0;
            dataPtr = IntPtr.Zero;
            return false;
        }

        public static int GetBitDepth(HTuple type)
        {
            // 根据图像类型判断位深
            switch (type.S)
            {
                case "byte":
                case "uint1":
                    return 8;
                case "int2":
                case "uint2":
                    return 16; // HALCON does not have a direct uint2 type, usually it's 10-bit packed.
                case "int4":
                case "uint4":
                    return 32; // HALCON does not have a direct uint4 type, usually it's 12-bit packed.
                case "real":
                    return 32;
                default:
                    return -1; // 未知类型
            }
        }

        public static int GetByteCount(HTuple type)
        {
            // 根据图像类型判断位深
            switch (type.S)
            {
                case "byte":
                case "uint1":
                    return 1;
                case "int2":
                case "uint2":
                    return 2; // HALCON does not have a direct uint2 type, usually it's 10-bit packed.
                case "int4":
                case "uint4":
                    return 4; // HALCON does not have a direct uint4 type, usually it's 12-bit packed.
                case "real":
                    return 4;
                default:
                    return -1; // 未知类型
            }
        }

        public static string GetType(int byteCount)
        {
            switch (byteCount)
            {
                case 1:
                    return "byte"; // 单通道灰度图像
                case 2:
                    return "uint2"; // 假设三通道图像为 16 位（可以根据实际需求调整）
                case 4:
                    return "int4"; // 假设四通道图像为 32 位（可以根据实际需求调整）
                default:
                    return string.Empty;
            }
        }

        public static string GetImageInfoAsString(HObject image)
        {
            if (HalconHelper.GetImageInfo(image, out int imgWidth, out int imgHeight, out int imgChannels, out int imgBigDepth, out _))
            {
                var info = $"Width: {imgWidth}, Height: {imgHeight}, Channels: {imgChannels}, imgBigDepth: {imgBigDepth}";
                return info;
            }
            return "";
        }

        public static void ConvertRectangleToRegion(Rectangle rect, out HObject ho_Region)
        {
            // 矩形的左上角和右下角坐标
            int row1 = rect.Top;    // 矩形顶部行
            int col1 = rect.Left;   // 矩形左侧列
            int row2 = rect.Bottom - 1; // 矩形底部行（减 1）
            int col2 = rect.Right - 1;  // 矩形右侧列（减 1）

            // 使用 Halcon 函数生成矩形区域
            HOperatorSet.GenRectangle1(out ho_Region, row1, col1, row2, col2);
        }

        /// <summary>
        /// 根据文件扩展名匹配图像格式
        /// </summary>
        /// <param name="extension">文件扩展名</param>
        /// <returns>图像格式字符串</returns>
        public static string GetFormatByExtension(string imageFilePath, int quality = 0)
        {
            string extension = Path.GetExtension(imageFilePath).ToLower();
            switch (extension)
            {
                case ".tiff":
                case ".tif":
                    return "tiff";
                case ".jpeg":
                case ".jpg":
                    if (quality != 0)
                    {
                        quality = Math.Max(1, Math.Min(100, quality)); // 限制范围1~100
                        return $"jpeg {quality}";
                    }
                    else
                        return "jpeg";
                case ".png":
                    return "png fastest";
                case ".bmp":
                    return "bmp";
                case ".jp2":
                    return "jp2";
                case ".hobj":
                    return "hobj";
                // 添加更多需要支持的格式
                default:
                    return null;
            }
        }

        /// <summary>
        /// 根据输入图像文件创建一张空白样图：
        /// 宽、高、像素类型、通道数都与输入图像一致。
        /// 空白图像的像素值默认为 0。
        /// </summary>
        /// <param name="imageFile">输入图像文件路径</param>
        /// <returns>空白样图</returns>
        public static HObject CreateBlankImageLikeFile(string imageFile)
        {
            if (!File.Exists(imageFile))
            {
                return null;
            }

            HObject srcImage = null;
            HObject blankImage = null;

            try
            {
                // 读取输入图像
                HOperatorSet.ReadImage(out srcImage, imageFile);

                // 获取宽高
                HOperatorSet.GetImageSize(srcImage, out HTuple imgWidth, out HTuple imgHeight);

                // 获取像素类型，如 byte / int2 / uint2 / real
                HOperatorSet.GetImageType(srcImage, out HTuple imgType);

                // 获取通道数
                HOperatorSet.CountChannels(srcImage, out HTuple channelCountTuple);
                int channelCount = channelCountTuple.I;

                // 先创建第一通道空白图
                HOperatorSet.GenImageConst(out blankImage, imgType, imgWidth, imgHeight);

                // 多通道则继续追加空白通道
                for (int i = 1; i < channelCount; i++)
                {
                    HOperatorSet.GenImageConst(out HObject nextChannel, imgType, imgWidth, imgHeight);
                    HOperatorSet.AppendChannel(blankImage, nextChannel, out HObject mergedImage);

                    blankImage.Dispose();
                    nextChannel.Dispose();

                    blankImage = mergedImage;
                }

                return blankImage;
            }
            catch
            {
                if (blankImage != null)
                {
                    blankImage.Dispose();
                    blankImage = null;
                }

                return null;
            }
            finally
            {
                if (srcImage != null)
                {
                    srcImage.Dispose();
                    srcImage = null;
                }
            }
        }
        public static void DrawDashedLine(
            HObject srcImage,
            out HObject resultImage,
            double x1,
            double y1,
            double x2,
            double y2,
            double value1 = 0,
            double value2 = 0,
            double value3 = 255,
            int dashLength = 12,
            int gapLength = 4,
            int lineWidth = 1)
        {
            resultImage = null;

            HObject mergedRegion = null;
            HObject workImage = null;
            HObject c1 = null;
            HObject c2 = null;
            HObject c3 = null;

            try
            {
                double row1 = y1;
                double col1 = x1;
                double row2 = y2;
                double col2 = x2;

                double dRow = row2 - row1;
                double dCol = col2 - col1;
                double totalLength = Math.Sqrt(dRow * dRow + dCol * dCol);

                if (totalLength <= 0.001)
                {
                    resultImage = srcImage.CopyObj(1, 1);
                    return;
                }

                double unitRow = dRow / totalLength;
                double unitCol = dCol / totalLength;

                int step = dashLength + gapLength;
                int segmentCount = (int)Math.Ceiling(totalLength / step);

                HOperatorSet.GenEmptyRegion(out mergedRegion);

                for (int i = 0; i < segmentCount; i++)
                {
                    double startDist = i * step;
                    double endDist = Math.Min(startDist + dashLength, totalLength);

                    if (endDist <= startDist)
                        continue;

                    double sr = row1 + unitRow * startDist;
                    double sc = col1 + unitCol * startDist;
                    double er = row1 + unitRow * endDist;
                    double ec = col1 + unitCol * endDist;

                    HObject segRegion;
                    HOperatorSet.GenRegionLine(out segRegion, sr, sc, er, ec);

                    if (lineWidth > 1)
                    {
                        HObject segDilated;
                        HOperatorSet.DilationCircle(segRegion, out segDilated, Math.Max(1, lineWidth / 2.0));
                        segRegion.Dispose();
                        segRegion = segDilated;
                    }

                    HObject unionRegion;
                    HOperatorSet.Union2(mergedRegion, segRegion, out unionRegion);

                    mergedRegion.Dispose();
                    segRegion.Dispose();
                    mergedRegion = unionRegion;
                }

                HTuple channels;
                HOperatorSet.CountChannels(srcImage, out channels);

                if (channels.I == 1)
                {
                    HOperatorSet.PaintRegion(mergedRegion, srcImage, out resultImage, value1, "fill");
                }
                else if (channels.I == 3)
                {
                    HOperatorSet.Decompose3(srcImage, out c1, out c2, out c3);

                    HObject newC1, newC2, newC3;
                    HOperatorSet.PaintRegion(mergedRegion, c1, out newC1, value1, "fill");
                    HOperatorSet.PaintRegion(mergedRegion, c2, out newC2, value2, "fill");
                    HOperatorSet.PaintRegion(mergedRegion, c3, out newC3, value3, "fill");

                    c1.Dispose();
                    c2.Dispose();
                    c3.Dispose();

                    c1 = newC1;
                    c2 = newC2;
                    c3 = newC3;

                    HOperatorSet.Compose3(c1, c2, c3, out resultImage);
                }
                else
                {
                    throw new NotSupportedException($"不支持的通道数: {channels.I}");
                }
            }
            finally
            {
                mergedRegion?.Dispose();
                workImage?.Dispose();
                c1?.Dispose();
                c2?.Dispose();
                c3?.Dispose();
            }
        }

        public static HObject OverlayImage2(List<HObject> images16bit)
        {
            if (images16bit == null || images16bit.Count == 0)
            {
                throw new ArgumentException("图像列表为空或未提供图像。");
            }
            // 获取第一张图像的尺寸和类型作为参考
            HTuple imgWidth, imgHeight;
            HOperatorSet.GetImageSize(images16bit[0], out imgWidth, out imgHeight);
            HOperatorSet.GetImageType(images16bit[0], out HTuple imgType);

            // 检查所有图像是否具有相同的尺寸和类型
            for (int i = 1; i < images16bit.Count; i++)
            {
                HTuple hWidth, hHeight;
                HOperatorSet.GetImageSize(images16bit[i], out hWidth, out hHeight);
                HOperatorSet.GetImageType(images16bit[i], out HTuple hType);
                if (hWidth.D != imgWidth.D || hHeight.D != imgHeight.D || hType.S != imgType.S)
                {
                    throw new ArgumentException("所有图像必须具有相同的尺寸和类型。");
                }
                hWidth.Dispose();
                hHeight.Dispose();
                hType.Dispose();
            }

            // 创建累积图像
            HOperatorSet.GenImageConst(out HObject overlayImage, "uint2", imgWidth, imgHeight);

            int queueNumber = images16bit.Count;
            for (int i = 0; i < queueNumber; i++)
            {
                HTuple multCrose = 1.0d / queueNumber;
                HObject imageTampe = images16bit[i];
                HOperatorSet.ScaleImage(imageTampe, out HObject imageScaled, multCrose, 0);
                HOperatorSet.AddImage(overlayImage, imageScaled, out HObject imageResult, 1.0d, 0);
                overlayImage.Dispose();
                imageScaled.Dispose();
                overlayImage = imageResult;
                multCrose.Dispose();
            }

            imgWidth.Dispose();
            imgHeight.Dispose();
            imgType.Dispose();

            return overlayImage;
        }

        public static HObject OverlayImage3(List<HObject> images16bit)
        {
            if (images16bit == null || images16bit.Count == 0)
            {
                throw new ArgumentException("图像列表为空或未提供图像。");
            }

            // 获取第一张图像的尺寸和类型作为参考
            HObject image0 = images16bit[0];
            HTuple imgWidth, imgHeight;
            HOperatorSet.GetImageSize(image0, out imgWidth, out imgHeight);
            HOperatorSet.GetImageType(image0, out HTuple imgType);

            // 检查所有图像是否具有相同的尺寸和类型
            for (int i = 1; i < images16bit.Count; i++)
            {
                HObject img = images16bit[i];
                HTuple hWidth, hHeight;
                HOperatorSet.GetImageSize(img, out hWidth, out hHeight);
                HOperatorSet.GetImageType(img, out HTuple hType);
                if (hWidth.D != imgWidth.D || hHeight.D != imgHeight.D || hType.S != imgType.S)
                {
                    throw new ArgumentException("所有图像必须具有相同的尺寸和类型。");
                }
            }

            // 创建累积图像
            HOperatorSet.GenImageConst(out HObject overlayImage, "int4", imgWidth, imgHeight);

            int queueNumber = images16bit.Count;
            for (int i = 0; i < queueNumber; i++)
            {
                HObject imageTampe = images16bit[i];
                HOperatorSet.ConvertImageType(imageTampe, out HObject imgFloat, "int4");
                HOperatorSet.AddImage(overlayImage, imgFloat, out HObject imageResult, 1.0d, 0);
                imgFloat.Dispose();
                overlayImage.Dispose();
                overlayImage = imageResult;
            }

            // 计算平均图像
            //HObject averagedImage = overlayImage / images16bit.Count;
            HOperatorSet.ScaleImage(overlayImage, out HObject averagedImage, 1.0d / images16bit.Count, 0);

            // 将平均图像转换回16位以查看或保存
            HOperatorSet.ConvertImageType(averagedImage, out HObject averagedImage16bit, "uint2");

            averagedImage.Dispose();
            overlayImage.Dispose();

            return averagedImage16bit;
        }

        public static HObject WLWWTransferWithParallel(HObject sourceImg, int wl, int ww)
        {
            var imageCopy = sourceImg.Clone();
            HOperatorSet.GetImagePointer1(imageCopy, out HTuple ptrImage, out HTuple hType, out HTuple hWidth, out HTuple hHeight);

            short[] imgData = new short[hWidth.I * hHeight.I];
            Marshal.Copy(ptrImage.IP, imgData, 0, imgData.Length);
            SetImageWL(ptrImage.IP, imgData, hWidth.I, hHeight.I, out HObject hImageSetWL, wl, ww);
            imageCopy.Dispose();
            return hImageSetWL;
        }

        // 设置窗宽窗位
        private static void SetImageWL(IntPtr ptrImage, short[] img_data, int imgWidth, int img_height, out HObject hImageSetWL, int wl, int ww)
        {
            var min = wl - ww / 2.0;
            var max = wl + ww / 2.0;
            var len = imgWidth * img_height;
            short[] data = new short[img_data.Length];

            Parallel.ForEach(Partitioner.Create(0, len), (range) =>
            {
                for (int i = range.Item1; i < range.Item2; i++)
                {
                    var val = (ushort)img_data[i];

                    if (val < min) val = 0;
                    else if (val > max) val = 255;
                    else val = (ushort)((val - min) / ww * 255);
                    data[i] = (short)val;
                }
            });
            Marshal.Copy(data, 0, ptrImage, img_data.Length);
            HOperatorSet.GenImage1(out hImageSetWL, "uint2", imgWidth, img_height, ptrImage);
        }

        public static HObject ScaleImageMax(HObject sourceImg)
        {
            try
            {
                HOperatorSet.ScaleImageMax(sourceImg, out HObject grayImage8);
                return grayImage8;
            }
            catch (Exception)
            {
            }
            return sourceImg.Clone();
        }

        /// <summary>
        /// 等比缩放图像，长边缩放到 zoomSize
        /// </summary>
        /// <param name="srcImage">输入图像</param>
        /// <param name="dstImage">输出图像</param>
        /// <param name="zoomSize">目标长边尺寸</param>
        /// <param name="interpolation">插值方式（默认 constant）</param>
        /// <returns>是否成功</returns>
        public static bool ZoomImageSize(HObject srcImage, out HObject dstImage, int zoomSize, string interpolation = "constant")
        {
            dstImage = null;
            try
            {
                if (srcImage == null || !srcImage.IsInitialized())
                    throw new ArgumentNullException(nameof(srcImage));

                // 获取原图尺寸
                HOperatorSet.GetImageSize(srcImage, out HTuple srcWidth, out HTuple srcHeight);

                // 计算等比缩放比例
                double scale = zoomSize / Math.Max(srcWidth.D, srcHeight.D);

                // 计算新尺寸
                HTuple newWidth = (int)Math.Round(srcWidth.D * scale);
                HTuple newHeight = (int)Math.Round(srcHeight.D * scale);

                // 缩放
                HOperatorSet.ZoomImageSize(srcImage, out dstImage, newWidth, newHeight, interpolation);

                srcWidth.Dispose();
                srcHeight.Dispose();

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error($"ZoomImageSize 失败: {ex.Message}");
                return false;
            }
        }

        public static HObject Gray16ToRgb8(HObject grayImage16)
        {
            // 将16位灰度图像归一化为8位灰度图像
            HOperatorSet.ScaleImageMax(grayImage16, out HObject grayImage8);
            HOperatorSet.Compose3(grayImage8, grayImage8, grayImage8, out HObject rgbImage8);
            grayImage8.Dispose();

            return rgbImage8;
        }

        public static void DispImageFit(HObject image, HWindowControl hWindowControl)
        {
            if (image == null) return;
            try
            {
                HOperatorSet.GetImageSize(image, out HTuple img_width, out HTuple img_height);
                //int win_width = hw_ctrl.ImagePart.Width;
                //int win_height = hw_ctrl.ImagePart.Height;
                int win_width = hWindowControl.Size.Width;
                int win_height = hWindowControl.Size.Height;

                double w_ratio = 1.0 * img_width / win_width;
                double h_ratio = 1.0 * img_height / win_height;
                double ratio = (w_ratio > h_ratio ? w_ratio : h_ratio);
                double column1 = -(ratio * win_width - img_width) / 2.0;
                double row1 = -(ratio * win_height - img_height) / 2.0;
                double column2 = (ratio * win_width - img_width) / 2.0 + img_width;
                double row2 = (ratio * win_height - img_height) / 2.0 + img_height;

                img_width.Dispose();
                img_height.Dispose();

                HOperatorSet.ClearWindow(hWindowControl.HalconWindow);

                HOperatorSet.SetPart(hWindowControl.HalconWindow, row1 - 1, column1 - 1, row2 - 1, column2 - 1);

                HOperatorSet.DispObj(image, hWindowControl.HalconWindow);

            }
            catch (Exception)
            {
                //LogHelper.Error(ex.Message);
            }
        }

        public static string PaintImage(HObject image, List<HObject> paintObjects, out HObject outputImage)
        {
            outputImage = null;
            try
            {
                // 初始化 HALCON 窗口
                // 获取图像宽度和高度
                HOperatorSet.GetImagePointer1(image, out HTuple hPointer, out HTuple hType, out HTuple hWidth, out HTuple hHeight);

                // 创建虚拟 HALCON 窗口并设置为图像大小
                HTuple windowID;
                HOperatorSet.OpenWindow(0, 0, hWidth, hHeight, "root", "buffer", "", out windowID);

                // 设置绘制颜色和模式
                //HOperatorSet.SetColor(windowID, "green");
                HOperatorSet.SetDraw(windowID, "margin");

                // 绘制多个矩形
                for (int i = 0; i < paintObjects.Count; i++)
                {
                    var paintObject = paintObjects[i];
                    HOperatorSet.DispObj(paintObject, windowID);
                }

                // 从虚拟窗口获取绘制后的图像
                HOperatorSet.DumpWindowImage(out outputImage, windowID);

                HOperatorSet.CloseWindow(windowID);
                return string.Empty;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static HObject ConvertTo8Bit(HObject image16Bit)
        {
            // 获取图像的最小值和最大值
            HOperatorSet.MinMaxGray(image16Bit, image16Bit, 0, out HTuple minVal, out HTuple maxVal, out _);

            // 线性缩放图像到 8 位
            HOperatorSet.ScaleImage(image16Bit, out HObject image8Bit, 255.0 / (maxVal.D - minVal.D), -minVal.D * 255.0 / (maxVal.D - minVal.D));

            // 将图像强制转换为8位
            HOperatorSet.ConvertImageType(image8Bit, out image8Bit, "byte");
            return image8Bit;
        }

        /// <summary>
        /// 2024.3.7新加CT找球心
        /// </summary>
        public static (HTuple, HTuple, HTuple, HTuple, HTuple, HObject) GetCirclePoint(HObject image, int ballGray)
        {
            // Local iconic variables
            HObject ho_Image1, ho_Image3, ho_Image = null;
            HObject ho_Region, ho_ConnectedRegions, ho_RegionOpening;
            HObject ho_SortedRegions, ho_ObjectSelected, ho_Circle;

            // Local control variables
            HTuple hv_Area = new HTuple(), hv_Row = new HTuple(), hv_Column = new HTuple();
            HTuple hv_Row2 = new HTuple(), hv_Column2 = new HTuple(), hv_Radius = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Image1);
            HOperatorSet.GenEmptyObj(out ho_Image3);
            HOperatorSet.GenEmptyObj(out ho_Image);
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_RegionOpening);
            //HOperatorSet.GenEmptyObj(out ho_SortedRegions);
            HOperatorSet.GenEmptyObj(out ho_ObjectSelected);
            HOperatorSet.GenEmptyObj(out ho_Circle);
            ho_Image.Dispose();
            ho_Image = new HObject(image);

            ho_Region.Dispose();
            HOperatorSet.Threshold(ho_Image, out ho_Region, 0, ballGray);//22000
            //if (Form1.form.hWindowTool.detect_steel_ball)
            //{
            //HOperatorSet.DispObj(ho_Region, Form1.form.hWindowTool.hw_ctrl.HalconWindow);
            //}
            ho_ConnectedRegions.Dispose();
            HOperatorSet.Connection(ho_Region, out ho_ConnectedRegions);
            ho_RegionOpening.Dispose();
            HOperatorSet.OpeningCircle(ho_ConnectedRegions, out ho_RegionOpening, 10.5);

            //选出所有圆形
            HOperatorSet.SelectShape(ho_RegionOpening, out ho_RegionOpening, "roundness", "and", 0.8, 2);
            //排序
            //ho_SortedRegions.Dispose();
            HOperatorSet.SortRegion(ho_RegionOpening, out ho_SortedRegions, "upper_left", "true", "row");
            //获取圆心和半径
            hv_Area.Dispose(); hv_Row.Dispose(); hv_Column.Dispose();
            HOperatorSet.AreaCenter(ho_SortedRegions, out hv_Area, out hv_Row, out hv_Column);
            HOperatorSet.SmallestCircle(ho_SortedRegions, out hv_Row2, out hv_Column2, out hv_Radius);

            ho_Image1.Dispose();
            ho_Image3.Dispose();
            ho_Image.Dispose();
            //ho_Region.Dispose();
            ho_ConnectedRegions.Dispose();
            ho_RegionOpening.Dispose();
            ho_SortedRegions.Dispose();
            ho_ObjectSelected.Dispose();
            hv_Area.Dispose();

            return (hv_Row, hv_Column, hv_Row2, hv_Column2, hv_Radius, ho_Region);
        }

        public static void scale_image_range(HObject ho_Image, out HObject ho_ImageScaled, HTuple hv_Min, HTuple hv_Max)
        {
            // Stack for temporary objects 
            HObject[] OTemp = new HObject[20];

            // Local iconic variables 

            HObject ho_ImageSelected = null, ho_SelectedChannel = null;
            HObject ho_LowerRegion = null, ho_UpperRegion = null, ho_ImageSelectedScaled = null;

            // Local copy input parameter variables 
            HObject ho_Image_COPY_INP_TMP;
            ho_Image_COPY_INP_TMP = new HObject(ho_Image);

            // Local control variables 

            HTuple hv_LowerLimit = new HTuple(), hv_UpperLimit = new HTuple();
            HTuple hv_Mult = new HTuple(), hv_Add = new HTuple(), hv_NumImages = new HTuple();
            HTuple hv_ImageIndex = new HTuple(), hv_Channels = new HTuple();
            HTuple hv_ChannelIndex = new HTuple(), hv_MinGray = new HTuple();
            HTuple hv_MaxGray = new HTuple(), hv_Range = new HTuple();
            HTuple hv_Max_COPY_INP_TMP = new HTuple(hv_Max);
            HTuple hv_Min_COPY_INP_TMP = new HTuple(hv_Min);

            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_ImageScaled);
            HOperatorSet.GenEmptyObj(out ho_ImageSelected);
            HOperatorSet.GenEmptyObj(out ho_SelectedChannel);
            HOperatorSet.GenEmptyObj(out ho_LowerRegion);
            HOperatorSet.GenEmptyObj(out ho_UpperRegion);
            HOperatorSet.GenEmptyObj(out ho_ImageSelectedScaled);
            //Convenience procedure to scale the gray values of the
            //input image Image from the interval [Min,Max]
            //to the interval [0,255] (default).
            //Gray values < 0 or > 255 (after scaling) are clipped.
            //
            //If the image shall be scaled to an interval different from [0,255],
            //this can be achieved by passing tuples with 2 values [From, To]
            //as Min and Max.
            //Example:
            //scale_image_range(Image:ImageScaled:[100,50],[200,250])
            //maps the gray values of Image from the interval [100,200] to [50,250].
            //All other gray values will be clipped.
            //
            //input parameters:
            //Image: the input image
            //Min: the minimum gray value which will be mapped to 0
            //     If a tuple with two values is given, the first value will
            //     be mapped to the second value.
            //Max: The maximum gray value which will be mapped to 255
            //     If a tuple with two values is given, the first value will
            //     be mapped to the second value.
            //
            //Output parameter:
            //ImageScale: the resulting scaled image.
            //
            if ((int)(new HTuple((new HTuple(hv_Min_COPY_INP_TMP.TupleLength())).TupleEqual(2))) != 0)
            {
                hv_LowerLimit.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_LowerLimit = hv_Min_COPY_INP_TMP.TupleSelect(1);
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_Min = hv_Min_COPY_INP_TMP.TupleSelect(
                            0);
                        hv_Min_COPY_INP_TMP.Dispose();
                        hv_Min_COPY_INP_TMP = ExpTmpLocalVar_Min;
                    }
                }
            }
            else
            {
                hv_LowerLimit.Dispose();
                hv_LowerLimit = 0.0;
            }
            if ((int)(new HTuple((new HTuple(hv_Max_COPY_INP_TMP.TupleLength())).TupleEqual(2))) != 0)
            {
                hv_UpperLimit.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_UpperLimit = hv_Max_COPY_INP_TMP.TupleSelect(
                        1);
                }
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    {
                        HTuple
                          ExpTmpLocalVar_Max = hv_Max_COPY_INP_TMP.TupleSelect(
                            0);
                        hv_Max_COPY_INP_TMP.Dispose();
                        hv_Max_COPY_INP_TMP = ExpTmpLocalVar_Max;
                    }
                }
            }
            else
            {
                hv_UpperLimit.Dispose();
                hv_UpperLimit = 255.0;
            }
            //
            //Calculate scaling parameters.
            //Only scale if the scaling range is not zero.
            if ((int)((new HTuple(((((hv_Max_COPY_INP_TMP - hv_Min_COPY_INP_TMP)).TupleAbs()
                )).TupleLess(1.0E-6))).TupleNot()) != 0)
            {
                hv_Mult.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_Mult = (((hv_UpperLimit - hv_LowerLimit)).TupleReal()
                        ) / (hv_Max_COPY_INP_TMP - hv_Min_COPY_INP_TMP);
                }
                hv_Add.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_Add = ((-hv_Mult) * hv_Min_COPY_INP_TMP) + hv_LowerLimit;
                }
                //Scale image.
                {
                    HObject ExpTmpOutVar_0;
                    HOperatorSet.ScaleImage(ho_Image_COPY_INP_TMP, out ExpTmpOutVar_0, hv_Mult,
                        hv_Add);
                    ho_Image_COPY_INP_TMP.Dispose();
                    ho_Image_COPY_INP_TMP = ExpTmpOutVar_0;
                }
            }
            //
            //Clip gray values if necessary.
            //This must be done for each image and channel separately.
            ho_ImageScaled.Dispose();
            HOperatorSet.GenEmptyObj(out ho_ImageScaled);
            hv_NumImages.Dispose();
            HOperatorSet.CountObj(ho_Image_COPY_INP_TMP, out hv_NumImages);
            HTuple end_val51 = hv_NumImages;
            HTuple step_val51 = 1;
            for (hv_ImageIndex = 1; hv_ImageIndex.Continue(end_val51, step_val51); hv_ImageIndex = hv_ImageIndex.TupleAdd(step_val51))
            {
                ho_ImageSelected.Dispose();
                HOperatorSet.SelectObj(ho_Image_COPY_INP_TMP, out ho_ImageSelected, hv_ImageIndex);
                hv_Channels.Dispose();
                HOperatorSet.CountChannels(ho_ImageSelected, out hv_Channels);
                HTuple end_val54 = hv_Channels;
                HTuple step_val54 = 1;
                for (hv_ChannelIndex = 1; hv_ChannelIndex.Continue(end_val54, step_val54); hv_ChannelIndex = hv_ChannelIndex.TupleAdd(step_val54))
                {
                    ho_SelectedChannel.Dispose();
                    HOperatorSet.AccessChannel(ho_ImageSelected, out ho_SelectedChannel, hv_ChannelIndex);
                    hv_MinGray.Dispose(); hv_MaxGray.Dispose(); hv_Range.Dispose();
                    HOperatorSet.MinMaxGray(ho_SelectedChannel, ho_SelectedChannel, 0, out hv_MinGray,
                        out hv_MaxGray, out hv_Range);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_LowerRegion.Dispose();
                        HOperatorSet.Threshold(ho_SelectedChannel, out ho_LowerRegion, ((hv_MinGray.TupleConcat(
                            hv_LowerLimit))).TupleMin(), hv_LowerLimit);
                    }
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_UpperRegion.Dispose();
                        HOperatorSet.Threshold(ho_SelectedChannel, out ho_UpperRegion, hv_UpperLimit,
                            ((hv_UpperLimit.TupleConcat(hv_MaxGray))).TupleMax());
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.PaintRegion(ho_LowerRegion, ho_SelectedChannel, out ExpTmpOutVar_0,
                            hv_LowerLimit, "fill");
                        ho_SelectedChannel.Dispose();
                        ho_SelectedChannel = ExpTmpOutVar_0;
                    }
                    {
                        HObject ExpTmpOutVar_0;
                        HOperatorSet.PaintRegion(ho_UpperRegion, ho_SelectedChannel, out ExpTmpOutVar_0,
                            hv_UpperLimit, "fill");
                        ho_SelectedChannel.Dispose();
                        ho_SelectedChannel = ExpTmpOutVar_0;
                    }
                    if ((int)(new HTuple(hv_ChannelIndex.TupleEqual(1))) != 0)
                    {
                        ho_ImageSelectedScaled.Dispose();
                        HOperatorSet.CopyObj(ho_SelectedChannel, out ho_ImageSelectedScaled, 1, 1);
                    }
                    else
                    {
                        {
                            HObject ExpTmpOutVar_0;
                            HOperatorSet.AppendChannel(ho_ImageSelectedScaled, ho_SelectedChannel,
                                out ExpTmpOutVar_0);
                            ho_ImageSelectedScaled.Dispose();
                            ho_ImageSelectedScaled = ExpTmpOutVar_0;
                        }
                    }
                }
                {
                    HObject ExpTmpOutVar_0;
                    HOperatorSet.ConcatObj(ho_ImageScaled, ho_ImageSelectedScaled, out ExpTmpOutVar_0
                        );
                    ho_ImageScaled.Dispose();
                    ho_ImageScaled = ExpTmpOutVar_0;
                }
            }
            ho_Image_COPY_INP_TMP.Dispose();
            ho_ImageSelected.Dispose();
            ho_SelectedChannel.Dispose();
            ho_LowerRegion.Dispose();
            ho_UpperRegion.Dispose();
            ho_ImageSelectedScaled.Dispose();

            hv_Max_COPY_INP_TMP.Dispose();
            hv_Min_COPY_INP_TMP.Dispose();
            hv_LowerLimit.Dispose();
            hv_UpperLimit.Dispose();
            hv_Mult.Dispose();
            hv_Add.Dispose();
            hv_NumImages.Dispose();
            hv_ImageIndex.Dispose();
            hv_Channels.Dispose();
            hv_ChannelIndex.Dispose();
            hv_MinGray.Dispose();
            hv_MaxGray.Dispose();
            hv_Range.Dispose();

            return;
        }

        public static bool SetHalconFont(HTuple windowHandle, int font_size, bool isBold = false)
        {
            try
            {
                font_size = font_size > 0 ? font_size : 1;
                string inputFont = ConstHelper.FontArial + $"-{font_size}";
                HOperatorSet.SetFont(windowHandle, inputFont);
            }
            catch (Exception ex)
            {
                LogHelper.Debug($"字体设置异常: {ex.Message}");
                return false;
            }
            return true;
        }

        public static bool CalibrationConvertImage(HObject ho_image, out HObject ho_ImageScaleMax, int iMin, int iMax)
        {
            HObject ho_ImageScaled;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_ImageScaled);
            HOperatorSet.GenEmptyObj(out ho_ImageScaleMax);
            try
            {
                ho_ImageScaled.Dispose();
                scale_image_range(ho_image, out ho_ImageScaled, iMin, iMax);
                ho_ImageScaleMax.Dispose();
                HOperatorSet.ScaleImageMax(ho_ImageScaled, out ho_ImageScaleMax);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                ho_ImageScaled.Dispose();
            }
            return true;
        }

        public static bool CalibrationSaveImage(HObject ho_image, string imageFilePath, int iMin, int iMax)
        {
            HObject ho_ImageScaled, ho_ImageScaleMax;
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_ImageScaled);
            HOperatorSet.GenEmptyObj(out ho_ImageScaleMax);
            try
            {
                ho_ImageScaled.Dispose();
                scale_image_range(ho_image, out ho_ImageScaled, iMin, iMax);
                ho_ImageScaleMax.Dispose();
                HOperatorSet.ScaleImageMax(ho_ImageScaled, out ho_ImageScaleMax);
                HOperatorSet.WriteImage(ho_ImageScaleMax, "png", 0, imageFilePath);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                ho_ImageScaled.Dispose();
                ho_ImageScaleMax.Dispose();
            }
            return true;
        }

        public static bool HObjectIsValid(HObject obj)
        {
            if (obj == null || obj.IsInitialized() == false)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 把 obj 的所有权“转移”出去：返回 obj，并把引用置空（避免 finally Dispose 掉）
        /// </summary>
        public static HObject Take(ref HObject obj)
        {
            var tmp = obj;
            obj = null;
            return tmp;
        }

        public static HObject ToHObjectVector(List<HObject> list)
        {
            HObject startImage = list[0].Clone();
            for (int i = 0; i < list.Count - 1; i++)
            {
                HObject image = list[i];
                HObject tmp;
                HOperatorSet.ConcatObj(startImage, list[i + 1], out tmp);
                startImage = tmp.Clone();
            }
            return startImage;
        }

        // HObject 转换为 byte[]
        public static byte[] HobjectToBytes(HObject img)
        {
            HOperatorSet.CountChannels(img, out HTuple channels);
            HOperatorSet.GetImageSize(img, out HTuple width, out HTuple height);

            if (channels == 3)
            {
                HOperatorSet.Decompose3(img, out HObject hChannel1, out HObject hChannel2, out HObject hChannel3);
                HOperatorSet.GetImagePointer1(hChannel1, out HTuple pointer1, out _, out width, out height);
                HOperatorSet.GetImagePointer1(hChannel2, out HTuple pointer2, out _, out _, out _);
                HOperatorSet.GetImagePointer1(hChannel3, out HTuple pointer3, out _, out _, out _);
                byte[] by = new byte[width * height * channels];
                Marshal.Copy(pointer1, by, 0, width * height);
                Marshal.Copy(pointer2, by, width * height, width * height);
                Marshal.Copy(pointer3, by, width * height * 2, width * height);
                return by;
            }
            else
            {

                HOperatorSet.GetImageSize(img, out width, out height);
                HOperatorSet.GetImagePointer1(img, out HTuple pointer, out HTuple type, out width, out height);
                int bytePerPixel = GetBitDepth(type) / 8;
                byte[] by = new byte[width * height * bytePerPixel];
                Marshal.Copy(pointer, by, 0, width * height * bytePerPixel);
                return by;
            }
        }

        // byte[] 转换为 HObject
        public static string BytesToHObject(byte[] byteArray, int width, int height, int channels, out HObject image)
        {
            image = null;
            if (byteArray == null || byteArray.Length == 0)
                return $"数据内容无效，(byteArray == null || byteArray.Length == 0)";

            if (width <= 0 || height <= 0)
                return $"宽高不正确，[{width} - {height}](width <= 0 || height <= 0)";

            if (channels <= 0 || channels > 4)
                return $"通道数不正确，[{channels}](channels <= 0 || channels > 4)";

            // 计算每像素的字节数
            int bytesPerPixel = byteArray.Length / (width * height * channels);

            if (byteArray.Length != width * height * channels * bytesPerPixel)
                return $"数据长度不正确，数据长度：{byteArray.Length} width:{width} height:{height} channels:{channels} (byteArray.Length != width * height * channels * bytesPerPixel)";

            // 根据每像素的字节数确定图像类型
            string type = GetType(bytesPerPixel);
            if (string.IsNullOrEmpty(type))
                return $"数据类型不正确，type:{type} bytesPerPixel:{bytesPerPixel}";

            int channelSize = width * height;
            if (channels == 3)
            {
                IntPtr ptr1 = Marshal.AllocHGlobal(channelSize);
                IntPtr ptr2 = Marshal.AllocHGlobal(channelSize);
                IntPtr ptr3 = Marshal.AllocHGlobal(channelSize);
                try
                {
                    Marshal.Copy(byteArray, 0, ptr1, channelSize);
                    Marshal.Copy(byteArray, channelSize, ptr2, channelSize);
                    Marshal.Copy(byteArray, channelSize * 2, ptr3, channelSize);
                    HOperatorSet.GenImage1(out var image1, type, width, height, ptr1);
                    HOperatorSet.GenImage1(out var image2, type, width, height, ptr2);
                    HOperatorSet.GenImage1(out var image3, type, width, height, ptr3);
                    HOperatorSet.Compose3(image1, image2, image3, out image);
                    image1.Dispose();
                    image2.Dispose();
                    image3.Dispose();
                    return string.Empty;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr1);
                    Marshal.FreeHGlobal(ptr2);
                    Marshal.FreeHGlobal(ptr3);
                }
            }
            else
            {
                IntPtr ptr = Marshal.AllocHGlobal(byteArray.Length);
                try
                {
                    Marshal.Copy(byteArray, 0, ptr, byteArray.Length);
                    HOperatorSet.GenImage1(out image, type, width, height, ptr);
                    return string.Empty;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }

        public static Mat HObjectToMat(HObject hObject)
        {
            try
            {
                // 检查图像是否是多通道
                HTuple channels;
                HOperatorSet.CountChannels(hObject, out channels);

                if (channels == 1)
                {
                    // 单通道图像处理
                    HOperatorSet.GetImagePointer1(hObject, out var pointer, out var type, out var width, out var height);

                    if (type.S == "uint2")
                    {
                        // 处理 16-bit 单通道图像，转为 8-bit 单通道
                        Mat mat16 = new Mat(height.I, width.I, MatType.CV_16UC1, pointer.IP);
                        Mat mat8 = new Mat();
                        mat16.ConvertTo(mat8, MatType.CV_8UC1, 1.0 / 256.0); // 缩放到 8 位范围
                        return mat8;
                    }
                    else
                    {
                        // 默认 8-bit 单通道图像处理
                        Mat mat = new Mat(height.I, width.I, MatType.CV_8UC1, pointer.IP);
                        return mat;
                    }
                }
                else if (channels == 3)
                {
                    // 三通道图像处理
                    HObject ho_R, ho_G, ho_B;
                    HOperatorSet.Decompose3(hObject, out ho_R, out ho_G, out ho_B);

                    // 将每个通道转换为 Mat
                    Mat matR = HObjectToMat(ho_R);
                    Mat matG = HObjectToMat(ho_G);
                    Mat matB = HObjectToMat(ho_B);

                    Mat matRGB = null;
                    if (matR != null && matG != null && matB != null)
                    {
                        matRGB = new Mat();
                        // 合并通道为 BGR 顺序
                        Cv2.Merge(new[] { matB, matG, matR }, matRGB);
                    }

                    ho_R.Dispose();
                    ho_G.Dispose();
                    ho_B.Dispose();

                    return matRGB;
                }
                else
                {
                    throw new NotSupportedException("当前仅支持单通道和三通道图像");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error($"HObjectToMat 异常：{ex.Message}");
            }
            return null;
        }

        public static HObject MatToHObject(Mat mat)
        {
            if (mat == null || mat.Empty())
                throw new ArgumentException("mat is null or empty");

            Mat src = mat.IsContinuous() ? mat : mat.Clone();

            try
            {
                HObject image;

                if (src.Type() == MatType.CV_8UC1)
                {
                    HOperatorSet.GenImage1(
                        out image,
                        "byte",
                        src.Cols,
                        src.Rows,
                        src.Data);

                    return image;
                }

                if (src.Type() == MatType.CV_8UC3)
                {
                    Mat[] bgr = Cv2.Split(src);
                    try
                    {
                        HOperatorSet.GenImage3(
                            out image,
                            "byte",
                            src.Cols,
                            src.Rows,
                            bgr[2].Data,
                            bgr[1].Data,
                            bgr[0].Data);

                        return image;
                    }
                    finally
                    {
                        foreach (var ch in bgr)
                            ch.Dispose();
                    }
                }

                throw new NotSupportedException(
                    $"Unsupported MatType: {src.Type()}, only CV_8UC1/CV_8UC3 are supported.");
            }
            finally
            {
                if (!ReferenceEquals(src, mat))
                    src.Dispose();
            }
        }
        public static uint GetHobjectRows(HObject img)
        {
            HOperatorSet.GetImageSize(img, out HTuple width, out HTuple height);
            return (uint)height;
        }
        public static uint GetHobjectCols(HObject img)
        {
            HOperatorSet.GetImageSize(img, out HTuple width, out HTuple height);
            return (uint)width;
        }

        public static uint GetHobjectChannels(HObject img)
        {
            HOperatorSet.CountChannels(img, out HTuple channels);
            return (uint)channels;
        }

        public static byte[] SerializeHObjectToBytes(HObject hObject)
        {
            try
            {
                // 1) 调用 HALCON 的 SerializeObject 算子，将 HObject 序列化到 HTuple
                HOperatorSet.SerializeObject(hObject, out HTuple serializedItem);

                // 2) 通过 GetSerializedItemPtr 获取序列化指针及大小
                HTuple dataLength;  // dataLength 是序列化数据的字节数
                HOperatorSet.GetSerializedItemPtr(serializedItem, out var hv_ptr, out dataLength);
                int length = dataLength.I;

                // 3) 拷贝到托管的 byte 数组
                byte[] result = new byte[length];
                Marshal.Copy(hv_ptr.IP, result, 0, length);

                // 4) 释放指针资源（非常重要，否则可能导致内存泄漏）
                HOperatorSet.ClearSerializedItem(serializedItem);
                serializedItem.Dispose();

                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static HObject DeserializeWithCreateSerializedItemPtr(byte[] data)
        {
            try
            {
                // 1) 分配非托管内存并拷贝 data
                IntPtr pData = Marshal.AllocHGlobal(data.Length);
                Marshal.Copy(data, 0, pData, data.Length);

                // 2) 调用 CreateSerializedItemPtr
                //    copy = "true" 表示会在内部拷贝这块内存
                HTuple serializedItemHandle;
                HOperatorSet.CreateSerializedItemPtr(
                    new HTuple(pData),
                    new HTuple(data.Length),
                    new HTuple("true"),  // "true" -> 会拷贝
                    out serializedItemHandle
                );

                // 3) 此时 HALCON 已经有了内部副本，可以安全地释放本地分配的非托管内存
                Marshal.FreeHGlobal(pData);

                // 4) 反序列化得到 HObject
                HObject hObject;
                HOperatorSet.DeserializeObject(out hObject, serializedItemHandle);

                // 5) 不用的句柄要释放，否则会内存泄漏
                HOperatorSet.ClearSerializedItem(serializedItemHandle);
                serializedItemHandle.Dispose();

                return hObject;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string SerializeDiskTextDataList(List<DispTextData> textDataList)
        {
            var serializedList = textDataList.Select(data =>
                $"{data.Text},{data.Row:F3},{data.Col:F3},{data.Color}");
            return string.Join("|", serializedList);
        }

        public static List<DispTextData> DeserializeDispTextDataList(string serializedData)
        {
            var fontDataList = new List<DispTextData>();
            try
            {
                var items = serializedData.Split('|'); // 每个对象用竖线分隔
                foreach (var item in items)
                {
                    var fields = item.Split(','); // 每个字段用逗号分隔
                    if (fields.Length >= 4)
                    {
                        fontDataList.Add(new DispTextData
                        {
                            Text = fields[0],
                            Row = double.Parse(fields[1]),
                            Col = double.Parse(fields[2]),
                            Color = fields[3]
                        });
                    }
                    else if (fields.Length >= 3)
                    {
                        fontDataList.Add(new DispTextData
                        {
                            Text = fields[0],
                            Row = double.Parse(fields[1]),
                            Col = double.Parse(fields[2]),
                        });
                    }
                }
            }
            catch (Exception)
            {
            }
            return fontDataList;
        }

        // 合并 region
        public static HObject MergeRegions(HObject region1, HObject region2)
        {
            try
            {
                HOperatorSet.Union2(region1, region2, out HObject mergedRegion);
                return mergedRegion;
            }
            catch (Exception)
            {
            }
            return null;
        }

        // 生成空对象
        public static HObject GenEmptyHObject()
        {
            HOperatorSet.GenEmptyObj(out var emptyObject);
            return emptyObject;
        }

        // 计算两点间距离（像素）
        public static double CalculateDistance(System.Drawing.Point a, System.Drawing.Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // 返回两条线段的最近点（各在线段上）及像素距离。
        // 支持相交(距离=0)、近似平行、退化(点)等情况。
        public static double ClosestPointsOnSegments(System.Drawing.Point a0, System.Drawing.Point a1, System.Drawing.Point b0, System.Drawing.Point b1, out System.Drawing.Point pA, out System.Drawing.Point pB)
        {
            // 转 double 向量
            double ax = a0.X, ay = a0.Y, bx = a1.X, by = a1.Y;
            double cx = b0.X, cy = b0.Y, dx = b1.X, dy = b1.Y;

            double ux = bx - ax, uy = by - ay;               // L1 方向
            double vx = dx - cx, vy = dy - cy;               // L2 方向
            double wx = ax - cx, wy = ay - cy;               // C->A

            double uu = ux * ux + uy * uy;
            double vv = vx * vx + vy * vy;
            double uv = ux * vx + uy * vy;
            double uw = ux * wx + uy * wy;
            double vw = vx * wx + vy * wy;

            // 退化：任一条线段长度近0 -> 退化为点-线段距离
            const double EPS = 1e-9;
            if (uu < EPS && vv < EPS)
            {
                pA = new System.Drawing.Point((int)Math.Round(ax), (int)Math.Round(ay));
                pB = new System.Drawing.Point((int)Math.Round(cx), (int)Math.Round(cy));
                return Math.Sqrt((ax - cx) * (ax - cx) + (ay - cy) * (ay - cy));
            }
            if (uu < EPS)
            {
                // A 是点，投影到 B 段
                double t = (-vw) / vv; t = Math.Max(0, Math.Min(1, t));
                double pbx = cx + t * vx, pby = cy + t * vy;
                pA = new System.Drawing.Point((int)Math.Round(ax), (int)Math.Round(ay));
                pB = new System.Drawing.Point((int)Math.Round(pbx), (int)Math.Round(pby));
                return Math.Sqrt((ax - pbx) * (ax - pbx) + (ay - pby) * (ay - pby));
            }
            if (vv < EPS)
            {
                // B 是点，投影到 A 段
                double s = (-uw) / uu; s = Math.Max(0, Math.Min(1, s));
                double pax = ax + s * ux, pay = ay + s * uy;
                pA = new System.Drawing.Point((int)Math.Round(pax), (int)Math.Round(pay));
                pB = new System.Drawing.Point((int)Math.Round(cx), (int)Math.Round(cy));
                return Math.Sqrt((pax - cx) * (pax - cx) + (pay - cy) * (pay - cy));
            }

            // 一般情况
            double denom = uu * vv - uv * uv;
            if (denom < EPS)
            {
                // 近似平行：固定 s 到 [0,1] 的端点再找最优 t
                // 试 s=0 与 s=1 两个位置，取更短者
                Func<double, (double s, double t, double dist, double pax, double pay, double pbx, double pby)> Eval =
                    (_s) =>
                    {
                        double pax = ax + _s * ux, pay = ay + _s * uy;
                        double t0 = ((pax - cx) * vx + (pay - cy) * vy) / vv;
                        t0 = Math.Max(0, Math.Min(1, t0));
                        double pbx = cx + t0 * vx, pby = cy + t0 * vy;
                        double d = Math.Sqrt((pax - pbx) * (pax - pbx) + (pay - pby) * (pay - pby));
                        return (_s, t0, d, pax, pay, pbx, pby);
                    };
                var e0 = Eval(0.0);
                var e1 = Eval(1.0);
                var best = (e0.dist <= e1.dist) ? e0 : e1;
                pA = new System.Drawing.Point((int)Math.Round(best.pax), (int)Math.Round(best.pay));
                pB = new System.Drawing.Point((int)Math.Round(best.pbx), (int)Math.Round(best.pby));
                return best.dist;
            }
            else
            {
                double s, t;
                s = (uv * vw - vv * uw) / denom;
                t = (uu * vw - uv * uw) / denom;

                // clamp 到 [0,1]，并在边界上再求一次对侧最优（防止越界）
                s = Math.Max(0, Math.Min(1, s));
                t = Math.Max(0, Math.Min(1, t));

                // 重新按固定 s 求 t
                double pax = ax + s * ux, pay = ay + s * uy;
                t = ((pax - cx) * vx + (pay - cy) * vy) / vv; t = Math.Max(0, Math.Min(1, t));
                double pbx = cx + t * vx, pby = cy + t * vy;

                // 再按固定 t 求 s
                double pax2, pay2, s2;
                {
                    double bx2 = cx + t * vx, by2 = cy + t * vy;
                    s2 = ((bx2 - ax) * ux + (by2 - ay) * uy) / uu; s2 = Math.Max(0, Math.Min(1, s2));
                    pax2 = ax + s2 * ux; pay2 = ay + s2 * uy;
                }

                // 取两种配对里更短者
                double d1 = Math.Sqrt((pax - pbx) * (pax - pbx) + (pay - pby) * (pay - pby));
                double d2 = Math.Sqrt((pax2 - (cx + t * vx)) * (pax2 - (cx + t * vx)) + (pay2 - (cy + t * vy)) * (pay2 - (cy + t * vy)));
                if (d1 <= d2)
                {
                    pA = new System.Drawing.Point((int)Math.Round(pax), (int)Math.Round(pay));
                    pB = new System.Drawing.Point((int)Math.Round(pbx), (int)Math.Round(pby));
                    return d1;
                }
                else
                {
                    pA = new System.Drawing.Point((int)Math.Round(pax2), (int)Math.Round(pay2));
                    pB = new System.Drawing.Point((int)Math.Round(cy + t * 0).GetHashCode()); // dummy to avoid CS0162 (this line won't run)
                    pB = new System.Drawing.Point((int)Math.Round(cx + t * vx), (int)Math.Round(cy + t * vy));
                    return d2;
                }
            }
        }

        // 两线段方向夹角（返回 0~180 度的非负值）
        public static double AngleBetweenSegmentsDeg(System.Drawing.Point a0, System.Drawing.Point a1, System.Drawing.Point b0, System.Drawing.Point b1)
        {
            double ux = a1.X - a0.X, uy = a1.Y - a0.Y;
            double vx = b1.X - b0.X, vy = b1.Y - b0.Y;
            double du = Math.Sqrt(ux * ux + uy * uy), dv = Math.Sqrt(vx * vx + vy * vy);
            if (du < 1e-9 || dv < 1e-9) return 0;
            ux /= du; uy /= du; vx /= dv; vy /= dv;
            double dot = ux * vx + uy * vy;
            dot = Math.Max(-1.0, Math.Min(1.0, dot));
            return Math.Acos(dot) * 180.0 / Math.PI; // 始终非负
        }

        /// <summary>
        /// 增加中心裁剪函数（width=visualW, height=visualH/2）
        /// </summary>
        /// <param name="ho_Image"></param>
        /// <param name="visualW"></param>
        /// <param name="visualH"></param>
        public static void CenterCropToVisualWidthHalfHeight(ref HObject ho_Image, int visualW, int visualH)
        {
            if (ho_Image == null) return;
            if (visualW <= 0 || visualH <= 0) return;

            // 目标尺寸 = visual
            int targetW = visualW;
            // 目标尺寸 = visual 的一半
            int targetH = visualH / 2;
            if (targetW <= 0 || targetH <= 0) return;

            HOperatorSet.GetImageSize(ho_Image, out HTuple hv_W, out HTuple hv_H);
            int srcW = hv_W.I;
            int srcH = hv_H.I;

            // 防御：目标不能超过原图
            targetW = Math.Min(targetW, srcW);
            targetH = Math.Min(targetH, srcH);

            // ★ 关键：严格以中心线对称裁剪 ★
            int col0 = (srcW - targetW) / 2;
            int row0 = (srcH - targetH) / 2;

            HOperatorSet.CropPart(ho_Image, out HObject ho_Crop, row0, col0, targetW, targetH);

            var old = ho_Image;
            ho_Image = ho_Crop;
            old.Dispose();

            hv_W.Dispose();
            hv_H.Dispose();
        }

        /// <summary>
        /// 标准中心裁剪，裁剪到指定的目标宽度和高度
        /// </summary>
        /// <param name="ho_Image">待裁剪图像（引用传递，会自动释放旧对象）</param>
        /// <param name="targetW">目标宽度（若大于原图宽度，则取原图宽度）</param>
        /// <param name="targetH">目标高度（若大于原图高度，则取原图高度）</param>
        public static void CenterCropToVisualWidthHeight(ref HObject ho_Image, int targetW, int targetH)
        {
            if (ho_Image == null) return;
            if (targetW <= 0 || targetH <= 0) return;

            // 获取原图尺寸
            HOperatorSet.GetImageSize(ho_Image, out HTuple hv_W, out HTuple hv_H);
            int srcW = hv_W.I;
            int srcH = hv_H.I;

            // 目标尺寸不能超出原图
            int cropW = Math.Min(targetW, srcW);
            int cropH = Math.Min(targetH, srcH);
            if (cropW <= 0 || cropH <= 0) return;

            // 计算中心对齐的起始坐标（确保对称）
            int col0 = (srcW - cropW) / 2;   // 列方向（宽度）
            int row0 = (srcH - cropH) / 2;   // 行方向（高度）

            // 执行裁剪
            HOperatorSet.CropPart(ho_Image, out HObject ho_Cropped, row0, col0, cropW, cropH);

            // 替换原对象并释放旧对象
            ho_Image.Dispose();
            ho_Image = ho_Cropped;

            // 释放临时变量
            hv_W.Dispose();
            hv_H.Dispose();
        }

        /// <summary>
        /// 保存 Halcon 字典到文件（自动处理后缀名）
        /// </summary>
        /// <param name="dict"></param>
        /// <param name="filePath"></param>
        public static void SaveHalconDict(HTuple dict, string filePath)
        {
            // 只去掉后缀名、不动路径、不动文件名主体
            string dictFile = FileSystemHelper.RemoveExtensionKeepPath(filePath);
            HOperatorSet.WriteDict(dict, dictFile, new HTuple(), new HTuple());
        }


        public static List<ImageDataInfo> GetImageDataInfos(List<HObject> images)
        {

            var items = new List<ImageDataInfo>();

            for (int i = 0; i < images.Count; i++)
            {

                if (images[i] != null)
                {
                    HalconHelper.GetImageInfo(images[i], out int cols, out int rows, out int channels, out int bitDepth, out IntPtr dataPtr);
                    byte[] secondImageData = HalconHelper.HobjectToBytes(images[i]);
                    var info = new ImageDataInfo
                    {
                        Rows = (uint)rows,
                        Cols = (uint)cols,
                        Channels = (uint)channels,
                        ChannelBytes = (uint)((bitDepth + 7) / 8),
                        Data = secondImageData
                    };
                    ulong expected = (ulong)info.Rows * info.Cols * info.Channels * info.ChannelBytes;

                    if (info.IsValidSize())
                    {
                        items.Add(info);
                    }
                    else
                    {
                        LogHelper.Error($"Image Pack Bytes({expected}) 与 Data.Length({info.Data.Length}) 不一致。默认剔除此图像。");
                        items.RemoveAt(i);
                    }


                }
            }
            return items;
        }

    }

}
