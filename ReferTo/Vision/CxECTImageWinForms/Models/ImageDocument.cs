using System;

namespace CxECTImageWinForms.Models
{
    public enum ImageSlotKind
    {
        Original,
        Result
    }

    /// <summary>
    /// 当前工作文档。SourceVersion 用于阻止旧的异步加载结果覆盖新图。
    /// </summary>
    public sealed class ImageDocument
    {
        public ImageSlot Original { get; private set; }

        public ImageSlot Result { get; private set; }

        public ImageSlotKind ActiveSlot { get; private set; }

        public long SourceVersion { get; private set; }

        public ImageSlot ActiveImage
        {
            get
            {
                return ActiveSlot == ImageSlotKind.Original ? Original : Result;
            }
        }

        public bool HasImage
        {
            get { return ActiveImage != null; }
        }

        public bool HasResult
        {
            get { return Result != null; }
        }

        public long SetOriginal(ImageSlot image)
        {
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            Original = image;
            Result = null;
            ActiveSlot = ImageSlotKind.Original;
            return ++SourceVersion;
        }

        public long SetResult(ImageSlot image)
        {
            if (image == null)
            {
                throw new ArgumentNullException("image");
            }

            Result = image;
            return ++SourceVersion;
        }

        public void Select(ImageSlotKind slot)
        {
            if (slot == ImageSlotKind.Original && Original == null)
            {
                throw new InvalidOperationException("尚未加载原图。");
            }

            if (slot == ImageSlotKind.Result && Result == null)
            {
                throw new InvalidOperationException("尚未加载结果图。");
            }

            ActiveSlot = slot;
        }

        public long Clear()
        {
            Original = null;
            Result = null;
            ActiveSlot = ImageSlotKind.Original;
            return ++SourceVersion;
        }
    }
}
