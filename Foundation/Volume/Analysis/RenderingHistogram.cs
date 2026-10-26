using CxECTVolumeCore.Utils;
using CxECTVolumeCore.Volume;
using System;
using System.Linq;
using System.Threading;

namespace CxECTVolumeCore.Rendering
{
    // 诊断数据与渲染算法解耦，便于后续替换直方图采样策略。
    /// <summary>保存标量与梯度直方图，为传递函数编辑器提供背景参考数据。</summary>
    public sealed class RenderingHistogram
    {
        private const int MaximumHistogramSamples = 4_000_000;

        public RenderingHistogram(float[] scalar, float[] gradient)
        {
            Scalar = scalar;
            Gradient = gradient;
        }

        public float[] Scalar { get; private set; }

        public float[] Gradient { get; private set; }

        public static RenderingHistogram FromVolume(VolumeData volume)
        {
            return FromVolume(volume, CancellationToken.None);
        }

        public static RenderingHistogram FromVolume(VolumeData volume, CancellationToken cancellationToken)
        {
            var scalarBins = new long[256];
            // 直方图只作为传递函数背景参考；限制采样数可避免大体积在 UI 显示前再次全量扫描。
            var scalarStep = Math.Max(1, (int)Math.Ceiling(volume.Voxels.Length / (double)MaximumHistogramSamples));
            var sampledCount = 0;
            for (var index = 0; index < volume.Voxels.Length; index += scalarStep)
            {
                if ((sampledCount++ & 0xFFFF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                scalarBins[volume.Voxels[index]]++;
            }

            var gradientBins = BuildGradientHistogram(volume, cancellationToken);
            return new RenderingHistogram(Normalize(scalarBins), Normalize(gradientBins));
        }

        private static long[] BuildGradientHistogram(VolumeData volume, CancellationToken cancellationToken)
        {
            var bins = new long[256];
            var width = volume.Width;
            var height = volume.Height;
            var depth = volume.Depth;
            var voxels = volume.Voxels;

            // 大数据只抽样计算梯度直方图，避免加载后 UI 长时间等待；图表参考不需要逐体素精确。
            var total = (long)Math.Max(1, width - 2) * Math.Max(1, height - 2) * Math.Max(1, depth - 2);
            var step = Math.Max(1, (int)Math.Ceiling(Math.Pow(total / (double)MaximumHistogramSamples, 1D / 3D)));

            for (var z = 1; z < depth - 1; z += step)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var y = 1; y < height - 1; y += step)
                {
                    for (var x = 1; x < width - 1; x += step)
                    {
                        var index = x + y * width + z * width * height;
                        var dx = voxels[index + 1] - voxels[index - 1];
                        var dy = voxels[index + width] - voxels[index - width];
                        var dz = voxels[index + width * height] - voxels[index - width * height];
                        var magnitude = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) / 441.7f;
                        var bin = (int)Math.Round(FrameworkCompat.Clamp(magnitude, 0.0f, 1.0f) * 255.0f);
                        bins[bin]++;
                    }
                }
            }

            return bins;
        }

        private static float[] Normalize(long[] bins)
        {
            var normalized = new float[bins.Length];
            var max = bins.Max();
            if (max <= 0)
            {
                return normalized;
            }

            for (var i = 0; i < bins.Length; i++)
            {
                // 平方根压缩让小峰也能看见，不至于被主峰完全盖住。
                normalized[i] = (float)Math.Sqrt(bins[i] / (float)max);
            }

            return normalized;
        }
    }
}
