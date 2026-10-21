namespace CxECTImageWinForms.Models
{
    /// <summary>
    /// 不依赖 WinForms 的图像尺寸值对象。
    /// </summary>
    public struct ImageSize
    {
        public ImageSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; private set; }

        public int Height { get; private set; }
    }
}
