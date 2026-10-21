using CommonInterfaces;
using HalconDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule
{
    public static partial class HalconHelper
    {
        public static HObject DumpEmptyImage(int width, int height)
        {
            try
            {
                HOperatorSet.GenImageConst(out var emptyImage, "byte", width, height);

                // 创建虚拟 HALCON 窗口并设置为图像大小
                HOperatorSet.OpenWindow(0, 0, width, height, "root", "buffer", "", out HTuple windowID);

                // 显示图像
                HOperatorSet.ClearWindow(windowID);
                HOperatorSet.SetPart(windowID, 0, 0, height - 1, width - 1);
                HOperatorSet.DispObj(emptyImage, windowID);

                // 设置字体
                HOperatorSet.SetDraw(windowID, "margin");
                // 设置线宽
                HOperatorSet.SetLineWidth(windowID, 1);

                HTuple font48 = ConstHelper.FontArial + $"-48";
                HOperatorSet.SetFont(windowID, font48);
                HOperatorSet.DispText(windowID, "empty", "image", 50, 100, "blue", "box", "false");
                HOperatorSet.DumpWindowImage(out var outputImage, windowID);
                HOperatorSet.CloseWindow(windowID);
                emptyImage.Dispose();
                return outputImage;
            }
            catch (Exception ex)
            {
                LogHelper.Error($"视觉 Main处理 合成结果图失败 DetectionPaintSN：{ex.Message}");
                return null;
            }
        }

        public static List<(string ImageName, double MeanGray)> GetProductMeaGgray(string dirPath, int productType)
        {
            var results = new List<(string ImageName, double MeanGray)>();

            if (!System.IO.Directory.Exists(dirPath))
                throw new DirectoryNotFoundException(dirPath);

            // 常见图像格式
            var files = System.IO.Directory.GetFiles(dirPath)
                .Where(f =>
                    f.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var file in files)
            {
                HObject ho_Image = null;
                HTuple hv_InDict = new HTuple();
                HTuple hv_OutDict = new HTuple();
                HTuple hv_ErrMsg = new HTuple();

                try
                {
                    // 读图
                    HOperatorSet.ReadImage(out ho_Image, file);

                    // 组织输入字典
                    HOperatorSet.CreateDict(out hv_InDict);
                    HOperatorSet.SetDictObject(ho_Image, hv_InDict, "Image");
                    HOperatorSet.SetDictTuple(hv_InDict, "ProductType", productType);

                    // 调你原来的 HALCON 过程
                    get_product_meangray(hv_InDict, out hv_OutDict, out hv_ErrMsg);

                    if (hv_ErrMsg.Length > 0 && hv_ErrMsg.S != "")
                    {
                        // 有需要可以记录日志
                        continue;
                    }

                    // 取结果
                    HTuple hv_MeanGray = hv_OutDict.TupleGetDictTuple("MeanGray");

                    results.Add((
                        System.IO.Path.GetFileName(file),
                        hv_MeanGray.D
                    ));
                }
                finally
                {
                    ho_Image?.Dispose();
                    hv_InDict.Dispose();
                    hv_OutDict.Dispose();
                    hv_ErrMsg.Dispose();
                }
            }

            return results;
        }

        public static void GetProductMeaGgray()
        {
            // Local iconic variables 

            HObject ho_Image;

            // Local control variables 

            HTuple hv_ProductType = new HTuple(), hv_InDict = new HTuple();
            HTuple hv_OutDict = new HTuple(), hv_ErrMsg = new HTuple();
            HTuple hv_MeanGray = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Image);
            try
            {

                hv_ProductType.Dispose();
                hv_ProductType = 3;
                ho_Image.Dispose();
                HOperatorSet.ReadImage(out ho_Image, "C:/Users/CYG/Desktop/测试图集/03_V63_9600/002__1101_0_22772_13_20251204113341.tif");
                hv_InDict.Dispose();
                HOperatorSet.CreateDict(out hv_InDict);
                HOperatorSet.SetDictObject(ho_Image, hv_InDict, "Image");
                HOperatorSet.SetDictTuple(hv_InDict, "ProductType", hv_ProductType);
                hv_OutDict.Dispose(); hv_ErrMsg.Dispose();
                get_product_meangray(hv_InDict, out hv_OutDict, out hv_ErrMsg);
                hv_MeanGray.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_MeanGray = hv_OutDict.TupleGetDictTuple(
                        "MeanGray");
                }
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Image.Dispose();

                hv_ProductType.Dispose();
                hv_InDict.Dispose();
                hv_OutDict.Dispose();
                hv_ErrMsg.Dispose();
                hv_MeanGray.Dispose();

                throw HDevExpDefaultException;
            }
            ho_Image.Dispose();

            hv_ProductType.Dispose();
            hv_InDict.Dispose();
            hv_OutDict.Dispose();
            hv_ErrMsg.Dispose();
            hv_MeanGray.Dispose();

        }

        // Procedures 
        // Local procedures 
        private static void get_product_meangray(HTuple hv_InDict, out HTuple hv_OutDict, out HTuple hv_ErrMsg)
        {
            // Local iconic variables 

            HObject ho_Image = null, ho_Region_product = null;
            HObject ho_Region_background = null, ho_Region = null, ho_ImageReduced1 = null;
            HObject ho_Region_light = null, ho_Region_dark = null, ho_Region_dark_FillUp = null;
            HObject ho_RegionOpening = null, ho_ConnectedRegions = null;
            HObject ho_SelectedRegions = null;

            // Local control variables 

            HTuple hv_ProductType = new HTuple(), hv_Means = new HTuple();
            HTuple hv_Width = new HTuple(), hv_Height = new HTuple();
            HTuple hv_UsedThreshold = new HTuple(), hv_Mean = new HTuple();
            HTuple hv__ = new HTuple(), hv_R = new HTuple(), hv_NewThreshold = new HTuple();
            HTuple hv_Area = new HTuple(), hv_Row = new HTuple(), hv_Col = new HTuple();
            HTuple hv_Exception = new HTuple(), hv_Ex = new HTuple();
            HTuple hv_DirName = new HTuple();
            // Initialize local and output iconic variables 
            HOperatorSet.GenEmptyObj(out ho_Image);
            HOperatorSet.GenEmptyObj(out ho_Region_product);
            HOperatorSet.GenEmptyObj(out ho_Region_background);
            HOperatorSet.GenEmptyObj(out ho_Region);
            HOperatorSet.GenEmptyObj(out ho_ImageReduced1);
            HOperatorSet.GenEmptyObj(out ho_Region_light);
            HOperatorSet.GenEmptyObj(out ho_Region_dark);
            HOperatorSet.GenEmptyObj(out ho_Region_dark_FillUp);
            HOperatorSet.GenEmptyObj(out ho_RegionOpening);
            HOperatorSet.GenEmptyObj(out ho_ConnectedRegions);
            HOperatorSet.GenEmptyObj(out ho_SelectedRegions);
            hv_OutDict = new HTuple();
            hv_ErrMsg = new HTuple();
            try
            {
                hv_ErrMsg.Dispose();
                hv_ErrMsg = "";
                ho_Image.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    ho_Image = hv_InDict.TupleGetDictObject(
                        "Image");
                }
                hv_ProductType.Dispose();
                using (HDevDisposeHelper dh = new HDevDisposeHelper())
                {
                    hv_ProductType = hv_InDict.TupleGetDictTuple(
                        "ProductType");
                }
                try
                {

                    hv_Means.Dispose();
                    hv_Means = new HTuple();
                    ho_Region_product.Dispose();
                    HOperatorSet.GenEmptyRegion(out ho_Region_product);
                    ho_Region_background.Dispose();
                    ho_Region_background = new HObject(ho_Image);

                    hv_Width.Dispose(); hv_Height.Dispose();
                    HOperatorSet.GetImageSize(ho_Image, out hv_Width, out hv_Height);
                    ho_Region.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region, 1, 65535);
                    ho_ImageReduced1.Dispose();
                    HOperatorSet.ReduceDomain(ho_Image, ho_Region, out ho_ImageReduced1);
                    ho_Region_light.Dispose(); hv_UsedThreshold.Dispose();
                    HOperatorSet.BinaryThreshold(ho_ImageReduced1, out ho_Region_light, "max_separability",
                        "light", out hv_UsedThreshold);
                    hv_Mean.Dispose(); hv__.Dispose();
                    HOperatorSet.Intensity(ho_Region_light, ho_Image, out hv_Mean, out hv__);
                    if ((int)(new HTuple(hv_ProductType.TupleEqual(1))) != 0)
                    {
                        hv_R.Dispose();
                        hv_R = 0.42;
                    }
                    else if ((int)(new HTuple(hv_ProductType.TupleEqual(0))) != 0)
                    {
                        hv_R.Dispose();
                        hv_R = 0.50;
                    }
                    else if ((int)((new HTuple(hv_ProductType.TupleEqual(2))).TupleOr(
                        new HTuple(hv_ProductType.TupleEqual(7)))) != 0)
                    {
                        hv_R.Dispose();
                        hv_R = 0.62;
                    }
                    else if ((int)((new HTuple((new HTuple((new HTuple(hv_ProductType.TupleEqual(
                        3))).TupleOr(new HTuple(hv_ProductType.TupleEqual(11))))).TupleOr(new HTuple(hv_ProductType.TupleEqual(
                        12))))).TupleOr(new HTuple(hv_ProductType.TupleEqual(13)))) != 0)
                    {
                        hv_R.Dispose();
                        hv_R = 1.0;
                    }
                    else if ((int)(new HTuple(hv_ProductType.TupleEqual(4))) != 0)
                    {
                        hv_R.Dispose();
                        hv_R = 1.0;
                    }
                    else if ((int)((new HTuple(hv_ProductType.TupleEqual(5))).TupleOr(
                        new HTuple(hv_ProductType.TupleEqual(9)))) != 0)
                    {
                        hv_R.Dispose();
                        hv_R = 0.59;
                    }
                    else if ((int)(new HTuple(hv_ProductType.TupleEqual(6))) != 0)
                    {
                        hv_R.Dispose();
                        hv_R = 0.55;
                    }
                    else if ((int)((new HTuple((new HTuple((new HTuple(hv_ProductType.TupleEqual(
                        8))).TupleOr(new HTuple(hv_ProductType.TupleEqual(10))))).TupleOr(new HTuple(hv_ProductType.TupleEqual(
                        14))))).TupleOr(new HTuple(hv_ProductType.TupleEqual(15)))) != 0)
                    {
                        hv_R.Dispose();
                        hv_R = 0.55;
                    }
                    else
                    {
                        hv_R.Dispose();
                        hv_R = 0.55;
                    }

                    hv_NewThreshold.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_NewThreshold = (hv_Mean * (1 - hv_R)) + (hv_UsedThreshold * hv_R);
                    }
                    ho_Region_dark.Dispose();
                    HOperatorSet.Threshold(ho_Image, out ho_Region_dark, 1, hv_NewThreshold);
                    ho_Region_dark_FillUp.Dispose();
                    HOperatorSet.FillUp(ho_Region_dark, out ho_Region_dark_FillUp);
                    ho_RegionOpening.Dispose();
                    HOperatorSet.OpeningCircle(ho_Region_dark_FillUp, out ho_RegionOpening, 1.5);
                    ho_ConnectedRegions.Dispose();
                    HOperatorSet.Connection(ho_RegionOpening, out ho_ConnectedRegions);
                    hv_Area.Dispose(); hv_Row.Dispose(); hv_Col.Dispose();
                    HOperatorSet.AreaCenter(ho_ConnectedRegions, out hv_Area, out hv_Row, out hv_Col);
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        ho_SelectedRegions.Dispose();
                        HOperatorSet.SelectShape(ho_ConnectedRegions, out ho_SelectedRegions, ((new HTuple("area")).TupleConcat(
                            "row")).TupleConcat("column"), "and", (((((hv_Area.TupleMax()) * 0.7)).TupleConcat(
                            hv_Height / 3))).TupleConcat(hv_Width / 3), ((((hv_Area.TupleMax())).TupleConcat(
                            (2 * hv_Height) / 3))).TupleConcat((2 * hv_Width) / 3));
                    }
                    ho_Region_light.Dispose();
                    HOperatorSet.Difference(ho_Image, ho_SelectedRegions, out ho_Region_light
                        );
                    ho_Region_product.Dispose();
                    ho_Region_product = new HObject(ho_SelectedRegions);

                    ho_Region_background.Dispose();
                    ho_Region_background = new HObject(ho_Region_light);
                    //获取每个位置的背景均值
                    hv_Means.Dispose(); hv__.Dispose();
                    HOperatorSet.Intensity(ho_Region_background, ho_Image, out hv_Means, out hv__);

                    hv_Mean.Dispose(); hv__.Dispose();
                    HOperatorSet.Intensity(ho_Region_product, ho_Image, out hv_Mean, out hv__);

                    hv_OutDict.Dispose();
                    HOperatorSet.CreateDict(out hv_OutDict);
                    HOperatorSet.SetDictTuple(hv_OutDict, "MeanGray", hv_Mean);
                }
                // catch (Exception) 
                catch (HalconException HDevExpDefaultException1)
                {
                    HDevExpDefaultException1.ToHTuple(out hv_Exception);
                    hv_Ex.Dispose();
                    hv_Ex = new HTuple(hv_Exception);
                    hv_DirName.Dispose();
                    HOperatorSet.GetCurrentDir(out hv_DirName);
                    hv_ErrMsg.Dispose();
                    using (HDevDisposeHelper dh = new HDevDisposeHelper())
                    {
                        hv_ErrMsg = ((((((((hv_DirName + (hv_Ex.TupleSelect(
                            3))) + ":") + (hv_Ex.TupleSelect(2))) + ".发生在") + (hv_Ex.TupleSelect(7))) + "的行") + (hv_Ex.TupleSelect(
                            4))) + "-->") + (hv_Ex.TupleSelect(5));
                    }
                    // stop(...); only in hdevelop
                }
                ho_Image.Dispose();
                ho_Region_product.Dispose();
                ho_Region_background.Dispose();
                ho_Region.Dispose();
                ho_ImageReduced1.Dispose();
                ho_Region_light.Dispose();
                ho_Region_dark.Dispose();
                ho_Region_dark_FillUp.Dispose();
                ho_RegionOpening.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_SelectedRegions.Dispose();

                hv_ProductType.Dispose();
                hv_Means.Dispose();
                hv_Width.Dispose();
                hv_Height.Dispose();
                hv_UsedThreshold.Dispose();
                hv_Mean.Dispose();
                hv__.Dispose();
                hv_R.Dispose();
                hv_NewThreshold.Dispose();
                hv_Area.Dispose();
                hv_Row.Dispose();
                hv_Col.Dispose();
                hv_Exception.Dispose();
                hv_Ex.Dispose();
                hv_DirName.Dispose();

                return;
            }
            catch (HalconException HDevExpDefaultException)
            {
                ho_Image.Dispose();
                ho_Region_product.Dispose();
                ho_Region_background.Dispose();
                ho_Region.Dispose();
                ho_ImageReduced1.Dispose();
                ho_Region_light.Dispose();
                ho_Region_dark.Dispose();
                ho_Region_dark_FillUp.Dispose();
                ho_RegionOpening.Dispose();
                ho_ConnectedRegions.Dispose();
                ho_SelectedRegions.Dispose();

                hv_ProductType.Dispose();
                hv_Means.Dispose();
                hv_Width.Dispose();
                hv_Height.Dispose();
                hv_UsedThreshold.Dispose();
                hv_Mean.Dispose();
                hv__.Dispose();
                hv_R.Dispose();
                hv_NewThreshold.Dispose();
                hv_Area.Dispose();
                hv_Row.Dispose();
                hv_Col.Dispose();
                hv_Exception.Dispose();
                hv_Ex.Dispose();
                hv_DirName.Dispose();

                throw HDevExpDefaultException;
            }
        }
    }
}
