using System;

namespace CxECTImageWinForms.Models
{
    public enum ImageComparisonMode
    {
        Single,
        Toggle,
        SideBySide,
        Blend
    }

    /// <summary>
    /// 对比模式只描述显示策略，具体第二窗口由图窗适配器按需管理。
    /// </summary>
    public sealed class ImageComparisonState
    {
        public ImageComparisonState()
        {
            Mode = ImageComparisonMode.Single;
            BlendRatio = 0.5;
        }

        public ImageComparisonMode Mode { get; private set; }

        public bool IsSyncViewEnabled { get; private set; }

        public double BlendRatio { get; private set; }

        public void SetMode(ImageComparisonMode mode)
        {
            Mode = mode;
        }

        public void SetSyncView(bool enabled)
        {
            IsSyncViewEnabled = enabled;
        }

        public void SetBlendRatio(double ratio)
        {
            if (ratio < 0 || ratio > 1)
            {
                throw new ArgumentOutOfRangeException("ratio", "融合比例必须在 0 到 1 之间。");
            }

            BlendRatio = ratio;
        }
    }
}
