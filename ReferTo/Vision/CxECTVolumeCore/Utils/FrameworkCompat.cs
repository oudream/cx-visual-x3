using System;
using System.IO;

namespace CxECTVolumeCore.Utils
{
    /// <summary>集中封装 .NET Framework 4.8 环境下的数值、流读取兼容辅助方法。</summary>
    public static class FrameworkCompat
    {
        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static void ReadExactly(Stream stream, byte[] buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read == 0)
                {
                    throw new EndOfStreamException("RAW 文件提前结束，无法读取完整体数据行。");
                }

                offset += read;
            }
        }
    }
}
