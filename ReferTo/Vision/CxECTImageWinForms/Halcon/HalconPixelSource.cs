using System;
using System.Runtime.InteropServices;
using CxECTImageWinForms.Contracts;
using CxECTImageWinForms.Models;
using HalconDotNet;

namespace CxECTImageWinForms.Halcon
{
    /// <summary>
    /// 原图的只读像素快照。构造阶段从 HALCON 图像复制连续内存，
    /// 后续统计在托管内存中运行，避免在 UI 线程反复调用 GetGrayval。
    /// </summary>
    public sealed class HalconPixelSource : IImagePixelSource, IDisposable
    {
        private byte[] _pixels;
        private byte[] _redPixels;
        private byte[] _greenPixels;
        private byte[] _bluePixels;
        // 由构造阶段的统一尺寸初始化方法赋值，之后不再改变。
        private string _pixelType;
        private int _bytesPerPixel;

        public HalconPixelSource(HObject image)
        {
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            HOperatorSet.CountChannels(image, out HTuple channelCount);
            if (channelCount.I != 1 && channelCount.I != 3)
            {
                throw new NotSupportedException("不支持的 HALCON 图像通道数：" + channelCount.I);
            }

            ChannelCount = channelCount.I;
            HTuple type;
            HTuple width;
            HTuple height;
            if (ChannelCount == 3)
            {
                HOperatorSet.GetImagePointer3(
                    image,
                    out HTuple redPointer,
                    out HTuple greenPointer,
                    out HTuple bluePointer,
                    out type,
                    out width,
                    out height);
                InitializeDimensions(type, width, height);
                var byteCount = checked(Width * Height * _bytesPerPixel);
                _redPixels = new byte[byteCount];
                _greenPixels = new byte[byteCount];
                _bluePixels = new byte[byteCount];
                Marshal.Copy(redPointer, _redPixels, 0, byteCount);
                Marshal.Copy(greenPointer, _greenPixels, 0, byteCount);
                Marshal.Copy(bluePointer, _bluePixels, 0, byteCount);
                return;
            }

            HOperatorSet.GetImagePointer1(image, out HTuple pointer, out type, out width, out height);
            InitializeDimensions(type, width, height);
            _pixels = new byte[checked(Width * Height * _bytesPerPixel)];
            Marshal.Copy(pointer, _pixels, 0, _pixels.Length);
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public int ChannelCount { get; private set; }

        public bool IsColor
        {
            get { return ChannelCount == 3; }
        }

        public double GetGrayValue(int x, int y)
        {
            var offset = GetPixelOffset(x, y);
            if (!IsColor)
            {
                return GetChannelValue(_pixels, offset);
            }

            // 统计和直方图需要单值；彩色图按行业常用的亮度权重折算，不再误把红通道当灰度。
            return GetChannelValue(_redPixels, offset) * 0.299
                + GetChannelValue(_greenPixels, offset) * 0.587
                + GetChannelValue(_bluePixels, offset) * 0.114;
        }

        /// <summary>
        /// 获取原始 RGB 分量。单通道图返回 null，调用方可继续使用 <see cref="GetGrayValue"/>。
        /// </summary>
        public ImagePixelColor GetPixelColor(int x, int y)
        {
            if (!IsColor)
            {
                return null;
            }

            var offset = GetPixelOffset(x, y);
            return new ImagePixelColor(
                GetChannelValue(_redPixels, offset),
                GetChannelValue(_greenPixels, offset),
                GetChannelValue(_bluePixels, offset));
        }

        public void Dispose()
        {
            _pixels = null;
            _redPixels = null;
            _greenPixels = null;
            _bluePixels = null;
        }

        private void InitializeDimensions(HTuple type, HTuple width, HTuple height)
        {
            Width = width.I;
            Height = height.I;
            _pixelType = type.S;
            _bytesPerPixel = GetBytesPerPixel(_pixelType);
        }

        private int GetPixelOffset(int x, int y)
        {
            if (_pixels == null && _redPixels == null)
            {
                throw new ObjectDisposedException("HalconPixelSource");
            }

            if (x < 0 || x >= Width || y < 0 || y >= Height)
            {
                throw new ArgumentOutOfRangeException("x", "像素坐标超出图像范围。");
            }

            return checked((y * Width + x) * _bytesPerPixel);
        }

        private double GetChannelValue(byte[] pixels, int offset)
        {
            switch (_pixelType)
            {
                case "byte": return pixels[offset];
                case "int1": return unchecked((sbyte)pixels[offset]);
                case "int2": return BitConverter.ToInt16(pixels, offset);
                case "uint2": return BitConverter.ToUInt16(pixels, offset);
                case "int4": return BitConverter.ToInt32(pixels, offset);
                case "uint4": return BitConverter.ToUInt32(pixels, offset);
                case "real": return BitConverter.ToSingle(pixels, offset);
                default: throw new NotSupportedException("不支持的 HALCON 像素类型：" + _pixelType);
            }
        }

        private static int GetBytesPerPixel(string pixelType)
        {
            switch (pixelType)
            {
                case "byte":
                case "int1":
                    return 1;
                case "int2":
                case "uint2":
                    return 2;
                case "int4":
                case "uint4":
                case "real":
                    return 4;
                default:
                    throw new NotSupportedException("不支持的 HALCON 像素类型：" + pixelType);
            }
        }
    }
}
