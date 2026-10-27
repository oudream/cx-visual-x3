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
        [FCMConfigItem("Provider配置（JSON对象）", Order = 10, Category = "扩展")]
        [StringLength(65536)]
        public string SettingsJson { get; set; } = "{}";
        public override void Validate()
        {
            base.Validate();
            if (string.IsNullOrWhiteSpace(CustomTypeId)) throw new ArgumentException("Custom TypeId 不能为空");
            if (SettingsJson == null || SettingsJson.Length > 65536)
                throw new ArgumentException("Provider配置不能为空或超过65536字符");
            try { FCMConfigValueCodec.GetObjectPropertyNames(SettingsJson); }
            catch (FormatException error) { throw new ArgumentException("Provider配置必须是JSON对象", error); }
        }
    }
}
