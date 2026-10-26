using CxECTVolumeCore.Utils;
using CxECTVolumeCore.Volume;
using System;

namespace CxECTVolumeCore.Rendering
{
    /// <summary>描述当前体数据中有信息的灰度区间，供默认渲染预设自适应展开低对比度数据。</summary>
    public sealed class RenderingIntensityProfile
    {
        private const int MaximumSamples = 4_000_000;

        public RenderingIntensityProfile(float lower, float upper)
        {
            Lower = FrameworkCompat.Clamp(lower, 0.0f, 254.0f / 255.0f);
            Upper = FrameworkCompat.Clamp(upper, Lower + 1.0f / 255.0f, 1.0f);
        }

        public float Lower { get; private set; }

        public float Upper { get; private set; }

        public static RenderingIntensityProfile FullRange
        {
            get { return new RenderingIntensityProfile(0.0f, 1.0f); }
        }

        public float Normalize(float scalar)
        {
            return FrameworkCompat.Clamp((scalar - Lower) / Math.Max(1.0f / 255.0f, Upper - Lower), 0.0f, 1.0f);
        }

        public static RenderingIntensityProfile FromVolume(VolumeData volume)
        {
            return FromVolume(volume, new RenderingIntensitySettings());
        }

        public static RenderingIntensityProfile FromVolume(
            VolumeData volume,
            RenderingIntensitySettings settings)
        {
            if (volume == null)
            {
                throw new ArgumentNullException("volume");
            }

            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            if (!settings.AutoRange)
            {
                return FullRange;
            }

            var bins = new long[256];
            int step = Math.Max(1, (int)Math.Ceiling(volume.Voxels.Length / (double)MaximumSamples));
            for (int index = 0; index < volume.Voxels.Length; index += step)
            {
                bins[volume.Voxels[index]]++;
            }

            int backgroundUpper = FrameworkCompat.Clamp(settings.BackgroundGrayUpper, 0, 254);
            int firstForegroundBin = backgroundUpper + 1;
            long foregroundCount = 0;
            for (int i = firstForegroundBin; i < bins.Length; i++)
            {
                foregroundCount += bins[i];
            }

            if (foregroundCount == 0)
            {
                return FullRange;
            }

            float lowerPercentile = FrameworkCompat.Clamp(settings.LowerPercentile, 0.0f, 0.999f);
            float minimumUpperPercentile = Math.Min(1.0f, lowerPercentile + 0.001f);
            float upperPercentile = FrameworkCompat.Clamp(settings.UpperPercentile, minimumUpperPercentile, 1.0f);
            int lowerBin = FindPercentile(bins, foregroundCount, lowerPercentile, firstForegroundBin);
            int upperBin = FindPercentile(bins, foregroundCount, upperPercentile, firstForegroundBin);
            lowerBin = Math.Max(backgroundUpper, lowerBin - 1);

            // 单灰度或极窄灰度数据也保留最少 16 个灰阶，避免映射区间退化。
            if (upperBin - lowerBin < 16)
            {
                int center = (lowerBin + upperBin) / 2;
                lowerBin = Math.Max(backgroundUpper, center - 8);
                upperBin = Math.Min(255, lowerBin + 16);
                lowerBin = Math.Max(backgroundUpper, upperBin - 16);
            }

            return new RenderingIntensityProfile(lowerBin / 255.0f, upperBin / 255.0f);
        }

        private static int FindPercentile(long[] bins, long total, float percentile, int firstBin)
        {
            long target = Math.Max(1L, (long)Math.Ceiling(total * percentile));
            long cumulative = 0;
            for (int i = firstBin; i < bins.Length; i++)
            {
                cumulative += bins[i];
                if (cumulative >= target)
                {
                    return i;
                }
            }

            return 255;
        }
    }
}
