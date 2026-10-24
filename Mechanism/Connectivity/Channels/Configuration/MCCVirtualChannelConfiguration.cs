using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;
namespace CxVisual.Mechanism.Connectivity
{
    public sealed class MCCVirtualChannelConfiguration : MCCChannelConfiguration
    {
        public MCCVirtualChannelConfiguration() : base(MCCChannelKind.Virtual) { }
        public override void Validate()
        {
            base.Validate();
            // Virtual 无特有配置
        }
    }
}
