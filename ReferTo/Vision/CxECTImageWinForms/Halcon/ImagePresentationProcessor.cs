using System;
using System.Collections.Generic;
using CxECTImageWinForms.Models;
using HalconDotNet;

namespace CxECTImageWinForms.Halcon
{
    /// <summary>
    /// 将显示状态转换为临时 HALCON 图像。该类不持有图像句柄，调用方负责释放返回值。
    /// 原始帧由 ImageFrameStore 持有，因此复位不会损失采集数据。
    /// </summary>
    internal sealed class ImagePresentationProcessor : IDisposable
    {
        private const int CacheCapacity = 3;
        private readonly Dictionary<string, HObject> _cache = new Dictionary<string, HObject>(StringComparer.Ordinal);
        private readonly Queue<string> _cacheOrder = new Queue<string>();

        /// <summary>
        /// 生成用于显示的临时图像。相同源帧与相同显示状态直接返回缓存副本，
        /// 避免刷新、切换视图等操作重复执行大图旋转和灰阶处理。
        /// </summary>
        public HObject CreatePresentation(HObject source, ImageTransformState transform, ImageDisplayState display, string cacheKey)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (transform == null)
            {
                throw new ArgumentNullException("transform");
            }

            if (display == null)
            {
                throw new ArgumentNullException("display");
            }

            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("显示缓存键不能为空。", "cacheKey");
            }

            HObject cached;
            if (_cache.TryGetValue(cacheKey, out cached))
            {
                return cached.Clone();
            }

            HObject current = source.Clone();
            try
            {
                if (Math.Abs(transform.RotationDegrees) > 0.000001)
                {
                    current = ReplaceWithRotation(current, transform.RotationDegrees);
                }

                if (transform.IsFlippedHorizontally)
                {
                    current = ReplaceWithMirror(current, "column");
                }

                if (transform.IsFlippedVertically)
                {
                    current = ReplaceWithMirror(current, "row");
                }

                if (display.IsAutoContrast)
                {
                    current = ReplaceWithScaleMax(current);
                }
                else if (Math.Abs(display.Contrast - 1.0) > 0.000001 || Math.Abs(display.Brightness) > 0.000001)
                {
                    current = ReplaceWithScale(current, display.Contrast, display.Brightness);
                }

                if (Math.Abs(display.Gamma - 1.0) > 0.000001)
                {
                    current = ReplaceWithGamma(current, display.Gamma);
                }

                if (display.IsInverted)
                {
                    current = ReplaceWithInvert(current);
                }

                var result = current;
                current = null;
                AddToCache(cacheKey, result);
                return result;
            }
            finally
            {
                if (current != null)
                {
                    current.Dispose();
                }
            }
        }

        public void Dispose()
        {
            ClearCache();
        }

        public void ClearCache()
        {
            foreach (var item in _cache.Values)
            {
                item.Dispose();
            }

            _cache.Clear();
            _cacheOrder.Clear();
        }

        private void AddToCache(string key, HObject presentation)
        {
            if (_cache.Count >= CacheCapacity)
            {
                var expiredKey = _cacheOrder.Dequeue();
                _cache[expiredKey].Dispose();
                _cache.Remove(expiredKey);
            }

            // 缓存保存自己的句柄，调用方仍可按 using 释放本次返回值。
            _cache.Add(key, presentation.Clone());
            _cacheOrder.Enqueue(key);
        }

        private static HObject ReplaceWithRotation(HObject source, double degrees)
        {
            HOperatorSet.RotateImage(source, out HObject result, degrees, "constant");
            source.Dispose();
            return result;
        }

        private static HObject ReplaceWithMirror(HObject source, string direction)
        {
            HOperatorSet.MirrorImage(source, out HObject result, direction);
            source.Dispose();
            return result;
        }

        private static HObject ReplaceWithScaleMax(HObject source)
        {
            HOperatorSet.ScaleImageMax(source, out HObject result);
            source.Dispose();
            return result;
        }

        private static HObject ReplaceWithScale(HObject source, double factor, double offset)
        {
            HOperatorSet.ScaleImage(source, out HObject result, factor, offset);
            source.Dispose();
            return result;
        }

        private static HObject ReplaceWithGamma(HObject source, double gamma)
        {
            HOperatorSet.GammaImage(source, out HObject result, gamma, 0, 0.0031308, 255, "true");
            source.Dispose();
            return result;
        }

        private static HObject ReplaceWithInvert(HObject source)
        {
            HOperatorSet.InvertImage(source, out HObject result);
            source.Dispose();
            return result;
        }
    }
}
