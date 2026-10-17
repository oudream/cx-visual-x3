using System;

namespace CxVisual.Foundation.Data.Types
{
    /// <summary>专业Adapter可选的托管有效载荷大小估算，不宣称统计CLR对象头或GPU资源。</summary>
    public interface IFDTDataSizeEstimator
    {
        bool TryEstimateBytes(object value, out long bytes);
    }

    public static class FDTDataSize
    {
        public static bool TryEstimateBytes(object value,
            FDTDataAdapterRegistry adapters, out long bytes)
        {
            if (value == null) { bytes = 0; return true; }
            if (value is byte[]) { bytes = ((byte[])value).LongLength; return true; }
            FDTReadOnlyBuffer buffer = value as FDTReadOnlyBuffer;
            if (buffer != null) { bytes = buffer.Length; return true; }
            if (FDTDataValueSafety.IsSimpleImmutable(value.GetType()))
            {
                // 标量的保守逻辑计费，不代表精确CLR内存大小。
                string text = value as string;
                bytes = text == null ? 32L : checked(32L + text.Length * 2L);
                return true;
            }
            if (adapters != null && adapters.TryEstimateBytes(value, out bytes))
                return true;
            bytes = 0;
            return false;
        }
    }
}
