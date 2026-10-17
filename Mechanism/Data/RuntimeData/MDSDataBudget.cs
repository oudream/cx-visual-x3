using System;
using CxVisual.Foundation.Data.Types;

namespace CxVisual.Mechanism.Data.RuntimeData
{
    /// <summary>
    /// 单次运行的托管数据持有预算：只计FlowHeap缓存及DataStack已提交输出。
    /// 未知专业类型需要尺寸估算器。暂存/CLR对象头/GPU显存不宣称纳入此账。
    /// </summary>
    public sealed class MDSDataBudget
    {
        private readonly object _gate = new object();
        private long _used;
        public long LimitBytes { get; private set; }
        public long RetainedBytes { get { lock (_gate) return _used; } }
        public MDSDataBudget(long limitBytes)
        {
            if (limitBytes < 1) throw new ArgumentOutOfRangeException("limitBytes");
            LimitBytes = limitBytes;
        }

        internal long Measure(object value, FDTDataAdapterRegistry adapters)
        {
            long bytes;
            if (!FDTDataSize.TryEstimateBytes(value, adapters, out bytes))
                throw new MDSDataException(MDSDataErrorCode.QuotaExceeded,
                    "专业数据未声明载荷尺寸，无法证明容量安全");
            return bytes;
        }
        internal void Reserve(long bytes)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException("bytes");
            lock (_gate)
            {
                if (bytes > LimitBytes - _used)
                    throw new MDSDataException(MDSDataErrorCode.QuotaExceeded,
                        "FlowHeap/DataStack数据载荷预算不足");
                _used += bytes;
            }
        }
        internal void Release(long bytes)
        {
            if (bytes < 0) throw new ArgumentOutOfRangeException("bytes");
            lock (_gate)
            {
                if (bytes > _used) throw new InvalidOperationException("容量账目重复释放");
                _used -= bytes;
            }
        }
    }
}
