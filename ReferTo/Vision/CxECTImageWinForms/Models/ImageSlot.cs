using System;

namespace CxECTImageWinForms.Models
{
    /// <summary>
    /// 图像资源的元数据；HALCON 句柄由适配层持有，避免泄漏到业务模型。
    /// </summary>
    public sealed class ImageSlot
    {
        public ImageSlot(string name, int width, int height, int channelCount, int bitDepth)
            : this(name, width, height, channelCount, bitDepth, string.Empty)
        {
        }

        public ImageSlot(string name, int width, int height, int channelCount, int bitDepth, string pixelType)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("图像名称不能为空。", "name");
            }

            if (width <= 0 || height <= 0 || channelCount <= 0 || bitDepth <= 0)
            {
                throw new ArgumentOutOfRangeException("width", "图像元数据无效。");
            }

            Id = Guid.NewGuid();
            Name = name;
            Size = new ImageSize(width, height);
            ChannelCount = channelCount;
            BitDepth = bitDepth;
            PixelType = pixelType ?? string.Empty;
        }

        public Guid Id { get; private set; }

        public string Name { get; private set; }

        public ImageSize Size { get; private set; }

        public int ChannelCount { get; private set; }

        public int BitDepth { get; private set; }

        /// <summary>
        /// HALCON 像素类型，例如 byte、uint2、real；为空时表示调用方未提供。
        /// </summary>
        public string PixelType { get; private set; }
    }
}
