using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;
namespace CxVisual.Foundation.Transport
{
    public enum FTRSharedMemorySide { A=0, B=1 }
    public sealed class FTRSharedMemoryChannelConfiguration : FTRChannelConfiguration
    {
        public FTRSharedMemoryChannelConfiguration():base(FTRChannelKind.SharedMemory){}
        [FCMConfigItem("共享区名称",Order=10,Category="SharedMemory")]
        [Required]
        public string MapName {get;set;}="cxvisual";
        [FCMConfigItem("通信端",Order=11,Category="SharedMemory")]
        public FTRSharedMemorySide Side {get;set;}=FTRSharedMemorySide.A;
        [FCMConfigItem("共享环槽数",Order=12,Category="SharedMemory")]
        [Range(2,64)]
        public int Slots {get;set;}=8;
        [FCMConfigItem("单槽最大字节数",Order=13,Category="SharedMemory",Unit="Bytes")]
        [Range(1,1048576)]
        public int SlotBytes {get;set;}=65536;
        public override void Validate()
        {
            base.Validate();
            if(string.IsNullOrWhiteSpace(MapName) || MapName.Length>80 ||
                !System.Text.RegularExpressions.Regex.IsMatch(MapName,@"^[A-Za-z0-9_.-]+$") ||
                !Enum.IsDefined(typeof(FTRSharedMemorySide),Side) || Slots<2 || Slots>64 ||
                SlotBytes<1 || SlotBytes>1048576 || SlotBytes>ReceiveBufferBytes ||
                (long)SlotBytes*Slots>ReceiveByteBudget)
                throw new ArgumentException("共享内存名称、端点与有界接收池预算必须有效");
        }
    }
}
