using System.Collections.Generic;

namespace CommonInterfaces
{
    // 常量名录
    public static class ConstHelper
    {
        public const string VariableNameValue = "Value";

        public const ushort ManualServerPort = 7342;

        public const ushort LineEdgeServerPort = 7344;
        
        public const ushort VisionHttpPort = 7346;

        public const string FontArial = "微软雅黑";
        //public const string FontArial = "Arial";
        public const string FontHelvetica = "Helvetica";
        // courier
        public const string FontCourier = "Courier";

        // 图像文件扩展名
        public const string ImageExtTif = ".tif";
        public const string ImageExtPng = ".png";
        public const string ImageExtJpg = ".jpg";

        // 判定方式
        public const string S_ManualDetermination = "人工判定";
        public const string S_AIDetermination = "AI判定";
        public const string S_AIPlusManualDetermination = "AI+人工判定";

        // 图像尺寸
        public const int ImageWidth3072 = 3072;
        public const int ImageHeight3072 = 3072;
        public const int ImageWidth1536 = 1536;
        public const int ImageHeight1536 = 1536;
        public const int ImageDefaultHalfHeight3072 = 1536;
        public const int ImageWidth6144 = 6144;

        // 磁盘空间最小值GB
        public const int DiskSpaceMinGB = 5;

        public const int ImageSingleProduct = 1;

        // 穴位、产品起始位置
        public const int StartPosition = 1;

        // 探测器图像尺寸
        public static int DetectorImageDefaultWidth { get; private set; } = ImageWidth3072;
        public static int DetectorImageDefaultHeight { get; private set; } = ImageHeight3072;
        public static int DetectorImageWidth_0505 { get; private set; } = ImageWidth1536;
        public static int DetectorImageHeight_0505 { get; private set; } = ImageHeight1536;
        public static int DetectorImageWidth_CMOS { get; private set; } = 2304;
        public static int DetectorImageHeight_CMOS { get; private set; } = 2930;

        // 可视化图像尺寸
        public static int VisualImageDefaultWidth { get; private set; } = ImageWidth3072;
        public static int VisualImageDefaultHeight { get; private set; } = ImageHeight3072;
        public static int VisualImageDefaultHalfHeight { get; private set; } = ImageDefaultHalfHeight3072;

        // 图像的镜像方式
        public const string ImageMirrorModeColumn = "column";
        public const string ImageMirrorModeRow = "row";

        // 默认日期格式
        public const string DefaultDateFormat = "yyyy/MM/dd HH:mm:ss";

        // 命令行中的功能码
        public const string FunctionCodeSelfcheckPdf = "SelfcheckPdf";
        public const string FunctionStatus = "Status";
        public const string FunctionStatusSuccess = "Success";

        public const int RemoteDisplayAIOn = 1;

        public const string S_OK = "OK";

        /// <summary>
        /// 探测器：默认像素尺寸（mm / pixel）
        /// </summary>
        // 0.139 = 139um
        public const double DefaultPixelSize139 = 0.139f;
        // 0.100 = 100um
        public const double DefaultPixelSize100 = 0.100f;

        // 设置默认图像尺寸（探测器图像尺寸、可视化图像尺寸）
        public static void Init(int detectorImageWidth, int detectorImageHeight, int visualImageWidth, int visualImageHeight)
        {
            //探测器图像尺寸
            DetectorImageDefaultWidth = detectorImageWidth;
            DetectorImageDefaultHeight = detectorImageHeight;
            
            //显示尺寸
            VisualImageDefaultWidth = visualImageWidth;
            VisualImageDefaultHeight = visualImageHeight;
            VisualImageDefaultHalfHeight = visualImageHeight / 2;
        }

    }

}
