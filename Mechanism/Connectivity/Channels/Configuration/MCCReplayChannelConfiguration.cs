using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;
namespace CxVisual.Mechanism.Connectivity
{
    // 回放原始采集报文的文件输入通道，与真实设备连接明确区分。
    public sealed class MCCReplayChannelConfiguration : MCCChannelConfiguration
    {
        public MCCReplayChannelConfiguration() : base(MCCChannelKind.Replay) { }

        [FCMConfigItem("回放文件完整路径", Order = 10, Category = "回放")]
        [Required]
        public string FilePath { get; set; } = "channel-replay.bin";

        [FCMConfigItem("记录之间间隔", Order = 11, Category = "回放", Unit = "ms")]
        [Range(0, 3600000)]
        public int RecordIntervalMs { get; set; } = 0;

        [FCMConfigItem("单记录最大字节数", Order = 12, Category = "回放", Unit = "Bytes")]
        [Range(1, int.MaxValue)]
        public int MaxRecordBytes { get; set; } = 4 * 1024 * 1024;

        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrWhiteSpace(FilePath) || RecordIntervalMs < 0 || MaxRecordBytes < 1)
                throw new ArgumentException("回放文件、记录间隔或字节预算无效");
        }
    }
}
