using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CxVisual.Foundation.Volume
{
    // 一次性持有 RAW 原始字节，避免预览抽样和高分辨率切片重复触发磁盘随机读取。
    /// <summary>使用非托管连续内存保存完整 RAW，并在释放时归还内存压力计数。</summary>
    /// <summary>完整 RAW 的非托管内存句柄，由 <see cref="FVOVolumeData" /> 持有和释放。</summary>
    public sealed class FVOUnmanagedRawVolumeBuffer : SafeHandleZeroOrMinusOneIsInvalid
    {
        private const int CopyBufferBytes = 32 * 1024 * 1024;
        private readonly long _length;
        private bool _memoryPressureAdded;

        private FVOUnmanagedRawVolumeBuffer(IntPtr pointer, long length)
            : base(true)
        {
            _length = length;
            SetHandle(pointer);
        }

        internal static FVOUnmanagedRawVolumeBuffer Load(string filePath, long expectedLength, FVORawVolumeLoadTiming timing)
        {
            if (IntPtr.Size != 8)
            {
                throw new PlatformNotSupportedException("加载超过 2GB 的 RAW 体数据需要 64 位进程。");
            }

            if (expectedLength <= 0)
            {
                throw new ArgumentOutOfRangeException("expectedLength");
            }

            // --- #加速加载-3：用单块非托管内存容纳完整 RAW，避免大托管数组造成 GC 压力，也避免切片时二次读盘。
            var allocationStopwatch = Stopwatch.StartNew();
            var buffer = Allocate(expectedLength);
            allocationStopwatch.Stop();
            if (timing != null)
            {
                timing.UnmanagedAllocationMilliseconds = allocationStopwatch.ElapsedMilliseconds;
            }
            try
            {
                var copyBuffer = new byte[(int)Math.Min(CopyBufferBytes, expectedLength)];
                var totalRead = 0L;
                // --- #加速加载-4：使用顺序扫描和 32MB 传输块，提高大 RAW 的吞吐；目标内存仅分配一次。
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferBytes, FileOptions.SequentialScan))
                {
                    while (totalRead < expectedLength)
                    {
                        var requested = (int)Math.Min(copyBuffer.Length, expectedLength - totalRead);
                        var readStopwatch = Stopwatch.StartNew();
                        var read = stream.Read(copyBuffer, 0, requested);
                        readStopwatch.Stop();
                        if (read <= 0)
                        {
                            throw new EndOfStreamException("RAW 文件在读取完成前结束。");
                        }

                        var copyStopwatch = Stopwatch.StartNew();
                        Marshal.Copy(copyBuffer, 0, AddPointer(buffer.DangerousGetHandle(), totalRead), read);
                        copyStopwatch.Stop();
                        if (timing != null)
                        {
                            timing.UnmanagedFileReadMilliseconds += readStopwatch.ElapsedMilliseconds;
                            timing.UnmanagedCopyMilliseconds += copyStopwatch.ElapsedMilliseconds;
                        }
                        totalRead += read;
                    }
                }

                return buffer;
            }
            catch
            {
                buffer.Dispose();
                throw;
            }
        }

        internal unsafe float ReadValue(long voxelIndex, FVORawVoxelFormat format)
        {
            var source = (byte*)DangerousGetHandle().ToPointer();
            switch (format)
            {
                case FVORawVoxelFormat.Byte:
                    return source[voxelIndex];
                case FVORawVoxelFormat.UInt16:
                    return *((ushort*)(source + voxelIndex * 2));
                case FVORawVoxelFormat.Float32:
                    return *((float*)(source + voxelIndex * 4));
                default:
                    throw new ArgumentOutOfRangeException("format", format, null);
            }
        }

        protected override bool ReleaseHandle()
        {
            try
            {
                Marshal.FreeHGlobal(handle);
            }
            finally
            {
                if (_memoryPressureAdded)
                {
                    GC.RemoveMemoryPressure(_length);
                    _memoryPressureAdded = false;
                }
            }

            return true;
        }

        private static FVOUnmanagedRawVolumeBuffer Allocate(long length)
        {
            var buffer = new FVOUnmanagedRawVolumeBuffer(Marshal.AllocHGlobal(new IntPtr(length)), length);
            try
            {
                GC.AddMemoryPressure(length);
                buffer._memoryPressureAdded = true;
                return buffer;
            }
            catch
            {
                buffer.Dispose();
                throw;
            }
        }

        private static IntPtr AddPointer(IntPtr pointer, long offset)
        {
            return new IntPtr(checked(pointer.ToInt64() + offset));
        }
    }
}
