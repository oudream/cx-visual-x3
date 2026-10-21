using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HalconDriverModule
{
    public static class HalconHelperTester
    {
        // 画
        public static void DrawPaintDisp()
        {
            HOperatorSet.ReadImage(out HObject image, @"E:\solder-ball-tif\Org_11AS.tif");

            // 测量执行时间
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // 初始化 HALCON 窗口
            // 获取图像宽度和高度
            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);

            // 创建虚拟 HALCON 窗口并设置为图像大小
            HTuple windowID;
            HOperatorSet.OpenWindow(0, 0, width, height, "root", "buffer", "", out windowID);
            HDevWindowStack.Push(windowID);

            // 显示图像到虚拟窗口
            HOperatorSet.DispObj(image, windowID);

            // 设置绘制颜色和模式
            HOperatorSet.SetColor(windowID, "green");
            HOperatorSet.SetDraw(windowID, "fill");

            // 绘制多个矩形
            for (int i = 0; i < 10; i++)
            {
                HOperatorSet.SetColor(windowID, "green");
                HTuple row1 = 100 + i * 50, col1 = 100 + i * 50, row2 = 150 + i * 50, col2 = 150 + i * 50;
                HOperatorSet.DispRectangle1(windowID, row1, col1, row2, col2);
            }

            // 绘制多个圆形
            for (int i = 0; i < 10; i++)
            {
                HOperatorSet.SetColor(windowID, "blue");
                HTuple row = 300 + i * 50, col = 300 + i * 50, radius = 30;
                HOperatorSet.DispCircle(windowID, row, col, radius);
            }

            // 绘制多个多边形
            for (int i = 0; i < 10; i++)
            {
                HOperatorSet.SetColor(windowID, "yellow");
                HTuple rows = new HTuple(new double[] { 500 + i * 50, 520 + i * 50, 540 + i * 50 });
                HTuple cols = new HTuple(new double[] { 500 + i * 50, 520 + i * 50, 500 + i * 50 });
                HOperatorSet.DispPolygon(windowID, rows, cols);
            }

            // 绘制多个文本
            for (int i = 0; i < 10; i++)
            {
                HOperatorSet.SetColor(windowID, "red");
                HTuple rowText = 50 + i * 30, colText = 50;
                string text = $"Text {i + 1}";
                HOperatorSet.DispText(windowID, text, "image", rowText, colText, "red", "box", "false");
            }

            // 从虚拟窗口获取绘制后的图像
            HOperatorSet.DumpWindowImage(out HObject outputImage, windowID);

            stopwatch.Stop();
            Console.WriteLine($"画图耗时: {stopwatch.ElapsedMilliseconds} 毫秒"); // 36毫秒

            // 保存最终的图像
            HOperatorSet.WriteImage(outputImage, "jpg", 0, "output_image.jpg");

            // 关闭虚拟窗口并清理资源
            HOperatorSet.CloseWindow(windowID);
            image.Dispose();
            outputImage.Dispose();
        }

        // HObject转HImage 图像转换
        public static void HObjectToHImage()
        {
            // 创建一个示例图像
            HImage image = new HImage();
            image.ReadImage(@"E:\solder-ball-tif\Org_11AS.tif"); // 替换为实际的图像路径

            // 测量执行时间
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < 1000; i++)
            {
                // 将图像转换为 HObject
                HObject hObject = image;

                // 再从 HObject 转换回 HImage
                HImage convertedImage = new HImage(hObject);

                // 检查转换后的图像是否有效
                //Console.WriteLine(convertedImage);
                // 打印通道数、位深、Size、获取图像的最小和最大灰度值、获取图像的颜色空间
                if (i % 100 == 0)
                {
                    Console.WriteLine($"{HalconHelper.GetImageInfoAsString(image)}");
                    Console.WriteLine($"{HalconHelper.GetImageInfoAsString(convertedImage)}");
                }

                // 注意！！！注意！！！注意！！！
                // hObject 不可以释放
                // hObject.Dispose(); // 会导致内存泄漏 // new HImage(hObject) 这样是引用了HObject

                // convertedImage 可以释放
                convertedImage.Dispose();
            }

            stopwatch.Stop();
            Console.WriteLine($"图像转换耗时: {stopwatch.ElapsedMilliseconds} 毫秒"); // 16毫秒

            image.Dispose();
        }

        public static void OverlayImage()
        {
            // 加载图像列表
            HOperatorSet.ReadImage(out HObject img1, @"E:\\solder-ball-tif\\Org_14AS.tif");
            HOperatorSet.ReadImage(out HObject img2, @"E:\\solder-ball-tif\\Org_15AS.tif");
            HOperatorSet.ReadImage(out HObject img3, @"E:\\solder-ball-tif\\Org_16AS.tif");
            HOperatorSet.ReadImage(out HObject img4, @"E:\\solder-ball-tif\\Org_17AS.tif");
            HOperatorSet.ReadImage(out HObject img5, @"E:\\solder-ball-tif\\Org_18AS.tif");
            List<HObject> images16bit = new List<HObject>
            {
                img1,
                img2,
                img3,
                img4,
                img5,
            };

            // 测量执行时间
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // 调用叠加函数
            HObject resultImage = HalconHelper.OverlayImage3(images16bit);

            stopwatch.Stop();
            Console.WriteLine($"图像叠加耗时: {stopwatch.ElapsedMilliseconds} 毫秒");

            // 保存结果图像
            HOperatorSet.WriteImage(resultImage, "tiff", 0, "averaged_image.tif");

            // 释放资源
            resultImage.Dispose();
            foreach (var img in images16bit)
            {
                img.Dispose();
            }
        }
    }

}
