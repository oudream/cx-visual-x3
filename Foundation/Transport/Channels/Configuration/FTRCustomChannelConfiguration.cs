using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRCustomChannelConfiguration : FTRChannelConfiguration
    {
        public FTRCustomChannelConfiguration() : base(FTRChannelKind.Custom) { }
        [FCMConfigItem("专业通道 TypeId", Order = 3, Category = "身份")]
        public string CustomTypeId { get; set; } = "";
        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrWhiteSpace(CustomTypeId)) throw new ArgumentException("Custom TypeId 不能为空");
        }
    }
}
