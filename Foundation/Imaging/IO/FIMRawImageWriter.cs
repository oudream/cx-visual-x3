using System;
using System.IO;
using System.Threading;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>
    /// 无HALCON/WPF依赖的工业原像素RAW导出。只导出FIMImageBuffer像素，
    /// 不将窗宽窗位、视口或Overlay误写入原数据。默认紧排小端；
    /// 需要严格保留源Stride/填充时可显式指定preserveStride。
    /// </summary>
    public static class FIMRawImageWriter
    {
        private const long MaxBytes = 512L * 1024 * 1024;

        /// <summary>
        /// 导出到调用者拥有的可写流；不关闭流。取消、写入错误会抛出异常，
        /// 因此需要原子文件保存时应调用SaveFile而非自行覆盖现有文件。
        /// </summary>
        public static void Write(Stream stream, FIMImageBuffer image,
            bool bigEndian = false, bool preserveStride = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (image == null) throw new ArgumentNullException("image");
            if (!stream.CanWrite)
                throw new ArgumentException("RAW输出流不可写", "stream");
            if (bigEndian && image.PixelFormat != FIMPixelFormat.Gray8 &&
                image.PixelFormat != FIMPixelFormat.Gray16 &&
                image.PixelFormat != FIMPixelFormat.GrayFloat32)
                throw new NotSupportedException("多通道大端RAW缺少明确通道布局，不允许猜测");
            int bytesPerPixel = image.BytesPerPixel;
            int rowBytes = checked(image.Width * bytesPerPixel);
            int writeStride = preserveStride ? image.Stride : rowBytes;
            long total = (long)writeStride * image.Height;
            if (total > MaxBytes)
                throw new InvalidDataException("RAW输出超过512MiB单帧限制");
            cancellationToken.ThrowIfCancellationRequested();

            // 每次只持有一行的快照，不因大图导出复制整帧。
            byte[] row = new byte[writeStride];
            for (int y = 0; y < image.Height; ++y)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (preserveStride)
                    image.CopyStorageRow(y, row);
                else
                    image.CopyRow(y, 0, image.Width, row, 0);
                if (bigEndian && bytesPerPixel > 1)
                {
                    for (int x = 0; x < rowBytes; x += bytesPerPixel)
                    {
                        for (int a = x, b = x + bytesPerPixel - 1; a < b; ++a, --b)
                        {
                            byte swap = row[a]; row[a] = row[b]; row[b] = swap;
                        }
                    }
                }
                stream.Write(row, 0, writeStride);
            }
        }

        /// <summary>
        /// 同目录临时文件+原子替换，避免取消、磁盘满或输出失败破坏已有RAW。
        /// overwrite=false时禁止覆盖；不假定文件名包含尺寸/位深/Stride元数据。
        /// </summary>
        public static void SaveFile(string path, FIMImageBuffer image,
            bool bigEndian = false, bool preserveStride = false,
            bool overwrite = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("RAW文件路径不能为空", "path");
            if (image == null) throw new ArgumentNullException("image");
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Path.GetFullPath(path);
            string folder = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                throw new DirectoryNotFoundException("RAW输出文件夹不存在：" + folder);
            if (!overwrite && File.Exists(fullPath))
                throw new IOException("RAW文件已存在，未允许覆盖");
            string temporary = Path.Combine(folder, "." + Path.GetFileName(fullPath) +
                "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                {
                    Write(output, image, bigEndian, preserveStride, cancellationToken);
                    output.Flush(true);
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (overwrite && File.Exists(fullPath))
                    File.Replace(temporary, fullPath, null);
                else
                    File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
