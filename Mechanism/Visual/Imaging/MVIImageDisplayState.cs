using System;

namespace CxVisual.Mechanism.Visual
{
    /// <summary>
    /// 从ReferTo/ImageDisplayState迁移的图像呈现参数：不持有、不写回原始像素。
    /// 窗口以原始灰度单位表示；亮度使用[-1,1]归一化量，Gamma采用正数。
    /// </summary>
    public sealed class MVIImageDisplayState
    {
        public event EventHandler Changed;
        public double WindowCenter { get; private set; }
        public double WindowWidth { get; private set; }
        public bool IsAutoWindowLevel { get; private set; }
        public bool IsAutoContrast { get; private set; }
        public bool IsInverted { get; private set; }
        public double Gamma { get; private set; }
        public double Brightness { get; private set; }
        public double Contrast { get; private set; }

        public MVIImageDisplayState() { ResetCore(); }
        public bool IsIdentity
        {
            get
            {
                return IsAutoWindowLevel && !IsAutoContrast && !IsInverted &&
                    Gamma == 1 && Brightness == 0 && Contrast == 1;
            }
        }
        private static double Finite(double value, string param)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(param);
            return value;
        }
        private void Notify()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }
        public void SetWindowLevel(double center, double width)
        {
            Finite(center, "center"); Finite(width, "width");
            if (width <= 0) throw new ArgumentOutOfRangeException("width");
            if (WindowCenter == center && WindowWidth == width && !IsAutoWindowLevel) return;
            WindowCenter = center; WindowWidth = width; IsAutoWindowLevel = false;
            Notify();
        }
        public void SetAutoWindowLevel(bool enabled)
        {
            if (IsAutoWindowLevel == enabled) return;
            IsAutoWindowLevel = enabled; Notify();
        }
        public void SetAutoContrast(bool enabled)
        {
            if (IsAutoContrast == enabled) return;
            IsAutoContrast = enabled; Notify();
        }
        public void SetInverted(bool enabled)
        {
            if (IsInverted == enabled) return;
            IsInverted = enabled; Notify();
        }
        /// <summary>
        /// 一次提交整组灰阶控制参数，避免大图在多个属性Change事件中重复生成显示缓冲。
        /// 任何参数非法时保持旧状态并且不发通知。
        /// </summary>
        public void SetTone(double gamma,double brightness,double contrast,
            bool inverted,bool autoContrast)
        {
            Finite(gamma,"gamma");
            Finite(brightness,"brightness");
            Finite(contrast,"contrast");
            if(gamma<=0 || gamma>20)throw new ArgumentOutOfRangeException("gamma");
            if(brightness< -1 || brightness>1)
                throw new ArgumentOutOfRangeException("brightness");
            if(contrast<0 || contrast>16)
                throw new ArgumentOutOfRangeException("contrast");
            if(Gamma==gamma && Brightness==brightness && Contrast==contrast &&
                IsInverted==inverted && IsAutoContrast==autoContrast)return;
            Gamma=gamma;Brightness=brightness;Contrast=contrast;
            IsInverted=inverted;IsAutoContrast=autoContrast;
            Notify();
        }
        public void SetGamma(double gamma)
        {
            Finite(gamma, "gamma");
            if (gamma <= 0 || gamma > 20) throw new ArgumentOutOfRangeException("gamma");
            if (Gamma == gamma) return;
            Gamma = gamma; Notify();
        }
        public void SetBrightness(double value)
        {
            Finite(value, "brightness");
            if (value < -1 || value > 1) throw new ArgumentOutOfRangeException("value");
            if (Brightness == value) return;
            Brightness = value; Notify();
        }
        public void SetContrast(double value)
        {
            Finite(value, "contrast");
            if (value < 0 || value > 16) throw new ArgumentOutOfRangeException("value");
            if (Contrast == value) return;
            Contrast = value; Notify();
        }
        public void Reset()
        {
            if (IsIdentity) return;
            ResetCore(); Notify();
        }
        private void ResetCore()
        {
            WindowCenter = 0; WindowWidth = 1;
            IsAutoWindowLevel = true;
            IsAutoContrast = false;
            IsInverted = false; Gamma = 1;
            Brightness = 0; Contrast = 1;
        }
    }
}
