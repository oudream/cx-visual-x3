using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRSerialChannelConfiguration : FTRChannelConfiguration
    {
        public FTRSerialChannelConfiguration() : base(FTRChannelKind.Serial) { }
        [FCMConfigItem("串口名称", Order = 20, Category = "串口")]
        public string PortName { get; set; } = "COM1";
        [FCMConfigItem("波特率", Order = 21, Category = "串口")]
        [Range(1, 10000000)]
        public int BaudRate { get; set; } = 9600;
        [FCMConfigItem("数据位", Order = 22, Category = "串口")]
        [Range(5, 8)]
        public int DataBits { get; set; } = 8;
        [FCMConfigItem("校验位", Order = 23, Category = "串口")]
        public string Parity { get; set; } = "None";
        [FCMConfigItem("停止位", Order = 24, Category = "串口")]
        public string StopBits { get; set; } = "One";
        [FCMConfigItem("串口硬件/软件流控", Order = 25, Category = "串口")]
        public string Handshake { get; set; } = "None";
        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrWhiteSpace(PortName) || BaudRate < 1 || DataBits < 5 || DataBits > 8 || Array.IndexOf(new[] {"None","Even","Odd","Mark","Space"}, Parity) < 0 || Array.IndexOf(new[] {"One","OnePointFive","Two"}, StopBits) < 0 ||
                Array.IndexOf(new[] {"None","XOnXOff","RequestToSend","RequestToSendXOnXOff"}, Handshake) < 0) throw new ArgumentException("无效串口参数");
        }
    }
}
