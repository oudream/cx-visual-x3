using System;
using System.Collections.Generic;

namespace CxVisual.Foundation.Infrastructure
{
    /// <summary>系统采样快照，不依赖UI、设备和Runtime。</summary>
    public sealed class CxSystemSnapshot
    {
        public CxSystemSnapshot(DateTime timeUtc, string machine, string operatingSystem,
            int logicalProcessors, double? totalCpuPercent, ulong? memoryTotalBytes,
            ulong? memoryAvailableBytes, long diskTotalBytes, long diskAvailableBytes,
            int activeNetworkInterfaces, IList<CxProcessSnapshot> processes, string warning)
        {
            TimeUtc = timeUtc;
            Machine = machine;
            OperatingSystem = operatingSystem;
            LogicalProcessors = logicalProcessors;
            TotalCpuPercent = totalCpuPercent;
            MemoryTotalBytes = memoryTotalBytes;
            MemoryAvailableBytes = memoryAvailableBytes;
            DiskTotalBytes = diskTotalBytes;
            DiskAvailableBytes = diskAvailableBytes;
            ActiveNetworkInterfaces = activeNetworkInterfaces;
            Processes = new System.Collections.ObjectModel.ReadOnlyCollection<CxProcessSnapshot>(
                new List<CxProcessSnapshot>(processes));
            Warning = warning ?? string.Empty;
        }
        public DateTime TimeUtc { get; private set; }
        public string Machine { get; private set; }
        public string OperatingSystem { get; private set; }
        public int LogicalProcessors { get; private set; }
        public double? TotalCpuPercent { get; private set; }
        public ulong? MemoryTotalBytes { get; private set; }
        public ulong? MemoryAvailableBytes { get; private set; }
        public long DiskTotalBytes { get; private set; }
        public long DiskAvailableBytes { get; private set; }
        public int ActiveNetworkInterfaces { get; private set; }
        public IList<CxProcessSnapshot> Processes { get; private set; }
        public string Warning { get; private set; }
    }
}
