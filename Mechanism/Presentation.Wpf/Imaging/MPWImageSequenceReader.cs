using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CxVisual.Mechanism.Presentation.Wpf.Imaging
{
    /// <summary>
    /// WPF BMP序列解码：只负责二维像素读取和Gray8连续排列，不拥有FVO三维体状态。
    /// 测试宿主及正式应用均可复用；不会依赖TestCenter、CT3D或WinForms。
    /// </summary>
    public static class MPWImageSequenceReader
    {
        /// <summary>
        /// 将目录中按名称排序的BMP图像转为连续Gray8层（X最快、其次Y、最后Z）。
        /// 返回数组由调用方持有，可直接构造现有FVO体数据；不隐式创建额外状态。
        /// </summary>
        public static byte[] ReadBmpGray8Stack(string directory,
            CancellationToken cancellation, out int width, out int height, out int depth)
        {
            cancellation.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException("directory");
            string[] files = Directory.GetFiles(directory, "*.bmp")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0)
                throw new InvalidDataException("目录内没有BMP切片。");

            width = 0;
            height = 0;
            depth = files.Length;
            byte[] pixels = null;
            for (int z = 0; z < files.Length; z++)
            {
                cancellation.ThrowIfCancellationRequested();
                BitmapFrame frame = BitmapFrame.Create(new Uri(files[z]),
                    BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                FormatConvertedBitmap gray = new FormatConvertedBitmap(
                    frame, PixelFormats.Gray8, null, 0);
                int w = gray.PixelWidth;
                int h = gray.PixelHeight;
                if (w <= 0 || h <= 0)
                    throw new InvalidDataException("BMP切片宽高必须为正。");
                if (z == 0)
                {
                    width = w;
                    height = h;
                    long count = checked((long)width * height * files.Length);
                    if (count > 512L * 1024 * 1024)
                        throw new InvalidDataException("BMP目录超过512MiB上限，请改用RAW。");
                    pixels = new byte[checked((int)count)];
                }
                else if (w != width || h != height)
                {
                    throw new InvalidDataException(
                        "BMP序列包含尺寸不一致的切片：" + files[z]);
                }

                // BitmapSource.CopyPixels(Array, stride, offset)：行跨度与层偏移均以字节计算。
                gray.CopyPixels(pixels, width, checked(width * height * z));
            }
            cancellation.ThrowIfCancellationRequested();
            return pixels;
        }
    }
}
