using CommonInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CxVisual.Platform.Protocols.S7
{
    public class S7ReadDbGroup
    {
        public int DBArea { get; set; }
        public int StartAdr { get; set; }
        public int Count { get; set; }
        public List<S7PointItem> Points { get; set; } = new List<S7PointItem>();
        public byte[] Data { get; set; }

    }

    public class S7ReadIntervalGroup
    {
        public int ReadInterval { get; set; }
        public List<S7ReadDbGroup> DbGroups { get; set; } = new List<S7ReadDbGroup>();

        public long LastReadTime { get; set; }

        public static List<S7ReadIntervalGroup> GroupPointsByReadIntervalAndDbArea(List<S7PointItem> items, bool? canRead = null)
        {
            var intervalGroups = new List<S7ReadIntervalGroup>();

            IEnumerable<S7PointItem> sourceItems = items;

            // 如果指定了 canRead，则进行过滤
            if (canRead.HasValue)
            {
                sourceItems = sourceItems.Where(p => p.CanRead == canRead.Value);
            }

            // 按读取周期间隔分组
            var groupsByInterval = sourceItems.GroupBy(item => item.ReadInterval);

            foreach (var intervalGroup in groupsByInterval)
            {
                var newIntervalGroup = new S7ReadIntervalGroup { ReadInterval = intervalGroup.Key };

                // 按 DBArea 分组
                var groupsByDbArea = intervalGroup.GroupBy(item => item.DBArea);

                foreach (var dbAreaGroup in groupsByDbArea)
                {
                    var minByteOffset = dbAreaGroup.Min(item => item.ByteOffset);
                    var maxByteOffset = dbAreaGroup.Max(item => item.ByteOffset);
                    // 选择一个具有最大 ByteOffset 的 S7PointItem
                    var maxByteOffsetPoint = dbAreaGroup.FirstOrDefault(item => item.ByteOffset == maxByteOffset);
                    var newDbGroup = new S7ReadDbGroup
                    {
                        DBArea = dbAreaGroup.Key,
                        StartAdr = minByteOffset,
                        Count = maxByteOffset - minByteOffset + maxByteOffsetPoint.GetLength(),
                        Points = dbAreaGroup.ToList()
                    };

                    newIntervalGroup.DbGroups.Add(newDbGroup);
                }

                intervalGroups.Add(newIntervalGroup);
            }

            return intervalGroups;
        }

        public static List<string> PrintGroupPoints(List<S7ReadIntervalGroup> intervalGroups)
        {
            List<string> result = new List<string>();
            foreach (var intervalGroup in intervalGroups)
            {
                result.Add($"Read Interval: {intervalGroup.ReadInterval}ms");
                foreach (var dbGroup in intervalGroup.DbGroups)
                {
                    result.Add($"\tDBArea: {dbGroup.DBArea}, Start Address: {dbGroup.StartAdr}, Count: {dbGroup.Count}");
                    foreach (var point in dbGroup.Points)
                    {
                        result.Add($"\t\tPoint: {point.DeviceName}, Byte Offset: {point.ByteOffset}, Length: {point.GetLength()}, CanRead: {point.CanRead}");
                    }
                }
            }
            return result;
        }

    }

}
