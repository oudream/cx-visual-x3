using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Volume;

namespace CxVisual.Professional.CT3D
{
    /// <summary>
    /// CT3D 体数据读取策略：RAW交互预览、完整原始缓存和大文件按需映射。
    /// 只负责文件与FVO数据，不拥有UI、三刀状态或渲染句柄。
    /// </summary>
    public static class CT3DVolumeLoadService
    {
        private const long MappingThresholdBytes = 512L * 1024 * 1024;

        public static Task<FVOVolumeData> LoadRawPreviewAsync(
            string filePath, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException("filePath");
            if (!File.Exists(filePath))
                throw new FileNotFoundException("RAW文件不存在。", filePath);
            long fileSize = new FileInfo(filePath).Length;
            int axis = fileSize >= MappingThresholdBytes ? 128 : 512;
            return Task.Run(() => FVORawVolumeLoader.Load(filePath, axis, cancellation),
                cancellation);
        }

        /// <summary>源体需要单独的无损缓存时返回true，已持有原值或全分辨率8位无需重复读取。</summary>
        public static bool NeedsOriginalCache(FVOVolumeData volume)
        {
            if (volume == null || !volume.HasRawSource || volume.HasOriginalRawData)
                return false;
            return !(volume.SourceFormat == FVORawVoxelFormat.Byte &&
                volume.Width == volume.OriginalWidth &&
                volume.Height == volume.OriginalHeight &&
                volume.Depth == volume.OriginalDepth);
        }

        public static bool UsesMappedOriginalCache(FVOVolumeData volume)
        {
            if (volume == null || !volume.HasRawSource)
                throw new ArgumentException("体数据没有原始RAW文件来源。", "volume");
            return new FileInfo(volume.SourceFilePath).Length > MappingThresholdBytes;
        }

        /// <summary>
        /// 只构建完整原始缓存并将所有权交给调用方；
        /// UI必须确认数据尚未过期后再调用FVOVolumeData.SetRawBuffer。
        /// </summary>
        public static Task<FVOUnmanagedRawVolumeBuffer> LoadOriginalCacheAsync(
            FVOVolumeData volume, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!NeedsOriginalCache(volume))
                throw new InvalidOperationException("当前体数据无需重新读取原始RAW缓存。");
            bool mapped = UsesMappedOriginalCache(volume);
            return Task.Run(() => mapped
                ? FVORawVolumeLoader.MapRawBuffer(volume, cancellation)
                : FVORawVolumeLoader.LoadRawBuffer(volume,
                    new FVORawVolumeLoadTiming(), cancellation), cancellation);
        }
    }
}
