using System;
using System.IO;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>
    /// 工业RAW单帧加载。调用方必须提供宽、高、像素格式和步长；
    /// 不猜测像素布局，不默默裁掉文件尾部，原始16/32位精度不变。
    /// </summary>
    public static class FIMRawImageReader
    {
        public static FIMImageBuffer LoadFile(string path, int width, int height,
            FIMPixelFormat format, int stride = 0, bool bigEndian = false)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("RAW文件路径为空。", "path");
            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                return Load(file, width, height, format, stride, bigEndian);
        }

        public static FIMImageBuffer Load(Stream stream, int width, int height,
            FIMPixelFormat format, int stride = 0, bool bigEndian = false)
        {
            if (stream == null) throw new ArgumentNullException("stream");
            if (!stream.CanRead) throw new ArgumentException("RAW输入流不可读。", "stream");
            if (width <= 0) throw new ArgumentOutOfRangeException("width");
            if (height <= 0) throw new ArgumentOutOfRangeException("height");
            int pixelBytes = FIMImageBuffer.GetBytesPerPixel(format);
            int rowBytes = checked(width * pixelBytes);
            if (stride == 0) stride = rowBytes;
            if (stride < rowBytes) throw new ArgumentOutOfRangeException("stride");
            long total = (long)stride * height;
            if (total > 512L * 1024 * 1024)
                throw new InvalidDataException("RAW大于512MiB单帧限制，请使用后续分块/共享内存路径。");
            if (stream.CanSeek && stream.Length - stream.Position != total)
                throw new InvalidDataException("RAW文件字节数与指定尺寸/格式/Stride不一致。");
            byte[] buffer = new byte[(int)total];
            int read = 0;
            while (read < buffer.Length)
            {
                int count = stream.Read(buffer, read, buffer.Length - read);
                if (count <= 0) throw new EndOfStreamException("RAW文件提前结束。");
                read += count;
            }
            if (!stream.CanSeek && stream.ReadByte() >= 0)
                throw new InvalidDataException("RAW非定位流含额外数据；请使用明确的单帧数据流。");
            if (bigEndian && (format == FIMPixelFormat.Gray16 || format == FIMPixelFormat.GrayFloat32))
            {
                for (int y = 0; y < height; y++)
                {
                    int start = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int offset = start + x * pixelBytes;
                        for (int a = 0, b = pixelBytes - 1; a < b; a++, b--)
                        {
                            byte t = buffer[offset + a];
                            buffer[offset + a] = buffer[offset + b];
                            buffer[offset + b] = t;
                        }
                    }
                }
            }
            else if (bigEndian && format != FIMPixelFormat.Gray8)
                throw new NotSupportedException("大端多通道RAW需明确每通道字节布局，本阶段禁止猜测。");
            return new FIMImageBuffer(width, height, format, buffer, stride);
        }
    }
}
