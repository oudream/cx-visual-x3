using System;
using System.Drawing;

namespace CxECTImageWinForms.Models.Overlays
{
    /// <summary>
    /// 叠加层统一颜色。颜色按图层即时取用，修改配置后历史对象与新对象同步更新。
    /// </summary>
    public sealed class OverlayColorPalette
    {
        public Color Roi { get; set; } = Color.FromArgb(0, 184, 217);

        public Color Measurement { get; set; } = Color.FromArgb(255, 152, 0);

        public Color Annotation { get; set; } = Color.FromArgb(194, 24, 91);

        public Color Defect { get; set; } = Color.FromArgb(244, 67, 54);

        public Color Selected { get; set; } = Color.FromArgb(57, 211, 83);

        public Color GetLayerColor(ImageOverlayLayer layer)
        {
            switch (layer)
            {
                case ImageOverlayLayer.Roi: return Roi;
                case ImageOverlayLayer.Measurement: return Measurement;
                case ImageOverlayLayer.Annotation: return Annotation;
                case ImageOverlayLayer.Defect: return Defect;
                default: throw new ArgumentOutOfRangeException("layer");
            }
        }

        public string GetHalconColor(ImageOverlayLayer layer)
        {
            return ToHalconColor(GetLayerColor(layer));
        }

        public static string ToHalconColor(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }
    }
}
