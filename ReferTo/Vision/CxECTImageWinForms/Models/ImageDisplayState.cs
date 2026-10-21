using System;

namespace CxECTImageWinForms.Models
{
    /// <summary>
    /// 显示参数与源图分离，便于复位及保证统计结果不受显示效果影响。
    /// </summary>
    public sealed class ImageDisplayState
    {
        public double WindowCenter { get; private set; }

        public double WindowWidth { get; private set; }

        public bool IsAutoWindowLevel { get; private set; }

        public bool IsAutoContrast { get; private set; }

        public bool IsInverted { get; private set; }

        public double Gamma { get; private set; }

        public double Brightness { get; private set; }

        public double Contrast { get; private set; }

        public ImageDisplayState()
        {
            Reset();
        }

        public void SetWindowLevel(double center, double width)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException("width", "窗宽必须大于零。");
            }

            WindowCenter = center;
            WindowWidth = width;
            IsAutoWindowLevel = false;
        }

        public void SetAutoWindowLevel(bool enabled)
        {
            IsAutoWindowLevel = enabled;
        }

        public void SetAutoContrast(bool enabled)
        {
            IsAutoContrast = enabled;
        }

        public void SetInverted(bool inverted)
        {
            IsInverted = inverted;
        }

        public void SetGamma(double gamma)
        {
            if (gamma <= 0)
            {
                throw new ArgumentOutOfRangeException("gamma", "Gamma 必须大于零。");
            }

            Gamma = gamma;
        }

        public void SetBrightness(double brightness)
        {
            Brightness = brightness;
        }

        public void SetContrast(double contrast)
        {
            if (contrast < 0)
            {
                throw new ArgumentOutOfRangeException("contrast", "对比度不能小于零。");
            }

            Contrast = contrast;
        }

        public void Reset()
        {
            WindowCenter = 0;
            WindowWidth = 1;
            IsAutoWindowLevel = true;
            IsAutoContrast = false;
            IsInverted = false;
            Gamma = 1.0;
            Brightness = 0;
            Contrast = 1.0;
        }
    }
}
