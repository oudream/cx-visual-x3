using System;
using CxECTImageWinForms.Models;
using HalconDotNet;

namespace CxECTImageWinForms.Halcon
{
    /// <summary>
    /// 原图和结果图的唯一所有者。所有对外传入或取出的 HObject 都以克隆交接，
    /// 防止调用方释放句柄后破坏浏览状态。
    /// </summary>
    internal sealed class ImageFrameStore : IDisposable
    {
        private HObject _original;
        private HObject _result;

        public bool HasOriginal { get { return _original != null; } }

        public bool HasResult { get { return _result != null; } }

        public void SetOriginal(HObject image)
        {
            Replace(ref _original, image);
            DisposeResult();
        }

        public void SetResult(HObject image)
        {
            Replace(ref _result, image);
        }

        public HObject GetClone(ImageSlotKind slot)
        {
            var image = slot == ImageSlotKind.Original ? _original : _result;
            if (image == null)
            {
                throw new InvalidOperationException(slot == ImageSlotKind.Original ? "尚未加载原图。" : "尚未加载结果图。");
            }

            return image.Clone();
        }

        public HObject CreateBlend(double resultRatio)
        {
            if (_original == null || _result == null)
            {
                throw new InvalidOperationException("图像融合需要同时加载原图和结果图。");
            }

            if (resultRatio < 0 || resultRatio > 1)
            {
                throw new ArgumentOutOfRangeException("resultRatio", "融合比例必须在 0 到 1 之间。");
            }

            EnsureSameSize();
            HObject originalPart = null;
            HObject resultPart = null;
            HObject blend = null;
            try
            {
                HOperatorSet.ScaleImage(_original, out originalPart, 1.0 - resultRatio, 0.0);
                HOperatorSet.ScaleImage(_result, out resultPart, resultRatio, 0.0);
                HOperatorSet.AddImage(originalPart, resultPart, out blend, 1.0, 0.0);
                return blend;
            }
            catch
            {
                if (blend != null)
                {
                    blend.Dispose();
                }

                throw;
            }
            finally
            {
                if (originalPart != null)
                {
                    originalPart.Dispose();
                }

                if (resultPart != null)
                {
                    resultPart.Dispose();
                }
            }
        }

        public void Clear()
        {
            DisposeOriginal();
            DisposeResult();
        }

        public void Dispose()
        {
            Clear();
        }

        private static void Replace(ref HObject target, HObject image)
        {
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            if (target != null)
            {
                target.Dispose();
            }

            target = image.Clone();
        }

        private void EnsureSameSize()
        {
            HOperatorSet.GetImageSize(_original, out HTuple originalWidth, out HTuple originalHeight);
            HOperatorSet.GetImageSize(_result, out HTuple resultWidth, out HTuple resultHeight);
            if (originalWidth.I != resultWidth.I || originalHeight.I != resultHeight.I)
            {
                throw new InvalidOperationException("原图和结果图的尺寸不同，不能直接融合。");
            }
        }

        private void DisposeOriginal()
        {
            if (_original != null)
            {
                _original.Dispose();
                _original = null;
            }
        }

        private void DisposeResult()
        {
            if (_result != null)
            {
                _result.Dispose();
                _result = null;
            }
        }
    }
}
