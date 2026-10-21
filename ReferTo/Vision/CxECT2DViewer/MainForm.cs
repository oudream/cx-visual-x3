using System;
using System.IO;
using System.Windows.Forms;
using CxECTImageWinForms.Models;
using HalconDotNet;

namespace CxECT2DViewer
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            // 窗口句柄建立后再创建 HALCON 控件，避免设计器和启动期句柄问题。
            imageViewerControl.EnsureRuntimeViewer();
        }

        private void openImageButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "图像文件|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|所有文件|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    LoadImageFile(dialog.FileName);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(this, exception.Message, "加载图像失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 供工具栏与自动验收共用的真实文件加载路径，避免两套读取逻辑出现行为差异。
        /// </summary>
        internal void LoadImageFile(string fileName)
        {
            HObject image = null;
            try
            {
                HOperatorSet.ReadImage(out image, fileName);
                HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
                HOperatorSet.CountChannels(image, out HTuple channels);
                HOperatorSet.GetImageType(image, out HTuple type);
                imageViewerControl.LoadImage(image, new ImageSlot(
                    System.IO.Path.GetFileName(fileName),
                    width.I,
                    height.I,
                    channels.I,
                    GetBitDepth(type.S),
                    type.S));
            }
            finally
            {
                if (image != null)
                {
                    image.Dispose();
                }
            }
        }

        internal bool HasLoadedImage
        {
            get { return imageViewerControl.Document.HasImage; }
        }

        /// <summary>
        /// 未加载图像时工具栏仍会显示；此处保证位置复位是安全空操作，不会调用 HALCON 空对象。
        /// </summary>
        internal void VerifyNoImageNavigation()
        {
            if (HasLoadedImage)
            {
                throw new InvalidOperationException("无图像导航验收必须在加载图像前执行。");
            }

            imageViewerControl.ResetPan();
            if (imageViewerControl.RefreshWindowLevelFromImage())
            {
                throw new InvalidOperationException("未加载图像时不应成功刷新窗宽窗位。");
            }
        }

        /// <summary>
        /// 自动验收使用的核心功能检查。所有操作复用真实控件 API，
        /// 覆盖显示、统计、变换、对比和常用保存格式，而不是只检查图像是否能打开。
        /// </summary>
        internal void VerifyImageViewerCoreFunctions(string resultImageFileName)
        {
            if (!HasLoadedImage)
            {
                throw new InvalidOperationException("尚未加载图像，不能执行看图器验收。");
            }

            var origin = new ImagePoint(0, 0);
            imageViewerControl.GetPixelValue(origin);
            imageViewerControl.GetPixelColor(origin);
            var histogram = imageViewerControl.GetHistogram(false, 32);
            if (histogram.Bins == null || histogram.Bins.Length != 32)
            {
                throw new InvalidOperationException("直方图分箱数量不正确。");
            }

            imageViewerControl.CalculateStatistics(false);
            imageViewerControl.FitToWindow();
            imageViewerControl.ActualSize();
            imageViewerControl.ResetPan();
            imageViewerControl.SetAutoContrast(true);
            imageViewerControl.SetAutoContrast(false);
            imageViewerControl.SetInverted(true);
            imageViewerControl.SetInverted(false);
            imageViewerControl.SetGamma(1.15);
            imageViewerControl.SetBrightness(8);
            imageViewerControl.SetContrast(1.1);
            imageViewerControl.Rotate90Clockwise();
            imageViewerControl.FlipHorizontal();
            imageViewerControl.FlipVertical();
            imageViewerControl.SetRotation(12.5);
            imageViewerControl.ResetTransform();
            bool refreshedWindowLevel = imageViewerControl.RefreshWindowLevelFromImage();
            if (imageViewerControl.Document.ActiveImage.BitDepth == 16 && !refreshedWindowLevel)
            {
                throw new InvalidOperationException("16 位图像刷新窗宽窗位失败。");
            }

            imageViewerControl.AutoWindowLevel();

            VerifyResultComparison(resultImageFileName);
            VerifyResetImage();
            VerifyImageSave(".tif");
            VerifyImageSave(".png");
            VerifyImageSave(".jpg");
        }

        private void VerifyResultComparison(string fileName)
        {
            HObject result = null;
            try
            {
                HOperatorSet.ReadImage(out result, fileName);
                HOperatorSet.GetImageSize(result, out HTuple width, out HTuple height);
                HOperatorSet.CountChannels(result, out HTuple channels);
                HOperatorSet.GetImageType(result, out HTuple type);
                imageViewerControl.LoadResultImage(result, new ImageSlot(
                    "verify_result",
                    width.I,
                    height.I,
                    channels.I,
                    GetBitDepth(type.S),
                    type.S));
                imageViewerControl.ToggleImage();
                imageViewerControl.BlendImages(0.5);
                imageViewerControl.SetSyncView(true);
                imageViewerControl.SetSyncView(false);
            }
            finally
            {
                if (result != null)
                {
                    result.Dispose();
                }
            }
        }

        private void VerifyResetImage()
        {
            imageViewerControl.ResetImage();
            if (imageViewerControl.Document.ActiveSlot != ImageSlotKind.Original ||
                imageViewerControl.ComparisonState.IsSyncViewEnabled ||
                imageViewerControl.ComparisonState.Mode != ImageComparisonMode.Single ||
                imageViewerControl.ToolMode != ImageToolMode.Navigate ||
                !imageViewerControl.DisplayState.IsAutoWindowLevel)
            {
                throw new InvalidOperationException("重置图像未恢复原图的初始显示状态。");
            }

            // 重置图像只恢复显示，不应破坏已加载的结果图；结果对比功能必须仍可继续使用。
            if (!imageViewerControl.Document.HasResult)
            {
                throw new InvalidOperationException("重置图像错误地移除了结果图。");
            }

            imageViewerControl.ToggleImage();
            if (imageViewerControl.Document.ActiveSlot != ImageSlotKind.Result)
            {
                throw new InvalidOperationException("重置图像后无法继续切换结果图。");
            }

            imageViewerControl.ResetImage();
        }

        private void VerifyImageSave(string extension)
        {
            var fileName = Path.Combine(Path.GetTempPath(), "CxECT2DViewer-verify-" + Guid.NewGuid().ToString("N") + extension);
            try
            {
                if (!imageViewerControl.SaveCurrentImage(fileName) || !File.Exists(fileName) || new FileInfo(fileName).Length == 0)
                {
                    throw new InvalidOperationException("图像保存验证失败：" + extension);
                }
            }
            finally
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
        }

        private static int GetBitDepth(string imageType)
        {
            switch (imageType)
            {
                case "byte":
                    return 8;
                case "int1":
                case "int2":
                case "uint2":
                    return 16;
                case "int4":
                case "real":
                    return 32;
                default:
                    return 8;
            }
        }
    }
}
