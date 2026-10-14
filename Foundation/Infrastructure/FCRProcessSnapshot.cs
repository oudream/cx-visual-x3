using System;

namespace CxVisual.Foundation.Infrastructure
{
    /// <summary>不可变的进程观测值，采样失败时相应数据使用null而不是假设为零。</summary>
    public sealed class FCRProcessSnapshot
    {
        public FCRProcessSnapshot(int id, string name, long? startedUtcTicks, double? cpuPercent,
            long workingSetBytes, int threadCount, int? handleCount)
        {
            Id = id;
            Name = name ?? string.Empty;
            StartedUtcTicks = startedUtcTicks;
            CpuPercent = cpuPercent;
            WorkingSetBytes = workingSetBytes;
            ThreadCount = threadCount;
            HandleCount = handleCount;
        }
        public int Id { get; private set; }
        public string Name { get; private set; }
        public long? StartedUtcTicks { get; private set; }
        public double? CpuPercent { get; private set; }
        public long WorkingSetBytes { get; private set; }
        public int ThreadCount { get; private set; }
        public int? HandleCount { get; private set; }
    }
}
