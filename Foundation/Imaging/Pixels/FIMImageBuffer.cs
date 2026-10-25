using System;

namespace CxVisual.Foundation.Imaging
{
    /// <summary>
    /// 自持有的二维原始像素缓冲区。公共构造函数复制输入，避免外部修改和悬挂引用。
    /// 与HALCON、OpenCV、WinForms、WPF无关；显式转8位显示不改变原图。
    /// </summary>
    public sealed class FIMImageBuffer
    {
        private readonly byte[] _pixels;

        public FIMImageBuffer(int width, int height, FIMPixelFormat pixelFormat, byte[] pixels, int stride = 0)
            : this(width, height, pixelFormat, pixels, stride, false)
        {
        }

        private FIMImageBuffer(int width, int height, FIMPixelFormat format,
            byte[] pixels, int stride, bool takeOwnership)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException("width");
            if (height <= 0) throw new ArgumentOutOfRangeException("height");
            if (pixels == null) throw new ArgumentNullException("pixels");
            int minimumStride = checked(width * GetBytesPerPixel(format));
            if (stride == 0) stride = minimumStride;
            if (stride < minimumStride) throw new ArgumentOutOfRangeException("stride");
            int required = checked(stride * height);
            if (pixels.Length < required) throw new ArgumentException("像素数据长度不足。", "pixels");

            Width = width; Height = height; PixelFormat = format; Stride = stride;
            if (takeOwnership && pixels.Length == required) _pixels = pixels;
            else
            {
                _pixels = new byte[required];
                Buffer.BlockCopy(pixels, 0, _pixels, 0, required);
            }
        }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Stride { get; private set; }
        public FIMPixelFormat PixelFormat { get; private set; }
        public int BytesPerPixel { get { return GetBytesPerPixel(PixelFormat); } }

        public byte[] CopyPixels()
        {
            byte[] copy = new byte[_pixels.Length];
            Buffer.BlockCopy(_pixels, 0, copy, 0, _pixels.Length);
            return copy;
        }

        /// <summary>原始灰度。彩色图使用BT.601亮度，不使用窗宽窗位等显示状态。</summary>
        public double GetGrayValue(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException("x");
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException("y");
            int position = checked(y * Stride + x * BytesPerPixel);
            switch (PixelFormat)
            {
                case FIMPixelFormat.Gray8: return _pixels[position];
                case FIMPixelFormat.Gray16:
                    return (ushort)(_pixels[position] | (_pixels[position + 1] << 8));
                case FIMPixelFormat.GrayFloat32:
                    if (BitConverter.IsLittleEndian) return BitConverter.ToSingle(_pixels, position);
                    byte[] bytes = { _pixels[position + 3], _pixels[position + 2],
                        _pixels[position + 1], _pixels[position] };
                    return BitConverter.ToSingle(bytes, 0);
                case FIMPixelFormat.Bgr24:
                case FIMPixelFormat.Bgra32:
                    return _pixels[position] * 0.114 +
                        _pixels[position + 1] * 0.587 + _pixels[position + 2] * 0.299;
                default: throw new InvalidOperationException("未知像素格式。");
            }
        }

        internal void CopyRow(int y, int firstPixel, int pixelCount, byte[] destination, int destinationOffset)
        {
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException("y");
            if (pixelCount < 0 || firstPixel < 0 || firstPixel > Width - pixelCount)
                throw new ArgumentOutOfRangeException("firstPixel");
            if (destination == null) throw new ArgumentNullException("destination");
            int count = checked(pixelCount * BytesPerPixel);
            if (destinationOffset < 0 || destinationOffset > destination.Length - count)
                throw new ArgumentOutOfRangeException("destinationOffset");
            Buffer.BlockCopy(_pixels, y * Stride + firstPixel * BytesPerPixel,
                destination, destinationOffset, count);
        }

        internal static FIMImageBuffer FromOwnedArray(int width, int height,
            FIMPixelFormat format, byte[] pixels, int stride)
        {
            return new FIMImageBuffer(width, height, format, pixels, stride, true);
        }

        public static int GetBytesPerPixel(FIMPixelFormat format)
        {
            switch (format)
            {
                case FIMPixelFormat.Gray8: return 1;
                case FIMPixelFormat.Gray16: return 2;
                case FIMPixelFormat.GrayFloat32: return 4;
                case FIMPixelFormat.Bgr24: return 3;
                case FIMPixelFormat.Bgra32: return 4;
                default: throw new ArgumentOutOfRangeException("format");
            }
        }
    }
}
