namespace CxECTImageWinForms.Models
{
    /// <summary>
    /// 彩色图像原始像素值。分量保持原始灰度范围，8 位图为 0-255，16 位图为 0-65535。
    /// </summary>
    public sealed class ImagePixelColor
    {
        public ImagePixelColor(double red, double green, double blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
        }

        public double Red { get; private set; }

        public double Green { get; private set; }

        public double Blue { get; private set; }
    }
}
