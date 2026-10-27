using System;
using System.Globalization;
using System.IO;
using System.Threading;
using CxVisual.Foundation.Imaging;

namespace CxVisual.Foundation.Volume
{
    /// <summary>
    /// 通用任意切Float32原始标量导出；不依赖WPF、测试界面或专业GPU实现。
    /// 原始体素与显示降采样严格分离，同时写出空间几何描述文件。
    /// </summary>
    public static class FVOObliqueSliceExporter
    {
        public static void SaveFloat32Raw(FVOVolumeData volume,
            FVOObliqueSliceDefinition definition, string filePath,
            CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (volume == null) throw new ArgumentNullException("volume");
            if (definition == null) throw new ArgumentNullException("definition");
            if (String.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException("filePath");
            if (!definition.Visible)
                throw new InvalidOperationException("请先启用任意切。");
            if (!volume.HasHighResolutionVoxels &&
                (volume.Width != volume.OriginalWidth ||
                 volume.Height != volume.OriginalHeight ||
                 volume.Depth != volume.OriginalDepth))
                throw new InvalidOperationException(
                    "原始RAW高精度缓存尚未就绪，不能把降采样预览冒充为高清原始结果。");

            FIMImageBuffer slice = FVOObliqueSliceBuilder.BuildScalar(
                volume, definition, cancellation);
            if (slice.PixelFormat != FIMPixelFormat.GrayFloat32)
                throw new InvalidOperationException("任意切原始输出必须为Float32格式。");
            cancellation.ThrowIfCancellationRequested();

            string fullPath = Path.GetFullPath(filePath);
            string folder = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string geometry = String.Format(CultureInfo.InvariantCulture,
                "Format=Float32 LittleEndian\nWidth={0}\nHeight={1}\nPixelSpacingInVoxels={2:R}\n" +
                "CenterX={3:R}\nCenterY={4:R}\nCenterZ={5:R}\n" +
                "AngleXDegrees={6:R}\nAngleYDegrees={7:R}\nAngleZDegrees={8:R}\nInterpolation={9}\n",
                definition.Width, definition.Height, definition.PixelSpacingInVoxels,
                definition.CenterX, definition.CenterY, definition.CenterZ,
                definition.AngleXDegrees, definition.AngleYDegrees,
                definition.AngleZDegrees, definition.Interpolation);

            File.WriteAllBytes(fullPath, slice.CopyPixels());
            File.WriteAllText(fullPath + ".txt", geometry);
        }
    }
}
