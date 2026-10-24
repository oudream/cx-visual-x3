using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;
namespace CxVisual.Mechanism.Connectivity
{
    public sealed class MCCCustomChannelConfiguration : MCCChannelConfiguration
    {
        public MCCCustomChannelConfiguration() : base(MCCChannelKind.Custom) { }
        [FCMConfigItem("专业通道 TypeId", Order = 3, Category = "身份")]
        public string CustomTypeId { get; set; } = "";
        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrWhiteSpace(CustomTypeId)) throw new ArgumentException("Custom TypeId 不能为空");
        }
    }
}
