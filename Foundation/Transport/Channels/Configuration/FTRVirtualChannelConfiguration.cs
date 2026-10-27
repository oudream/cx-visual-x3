using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Foundation.Transport
{
public sealed class FTRVirtualChannelConfiguration : FTRChannelConfiguration
    {
        public FTRVirtualChannelConfiguration() : base(FTRChannelKind.Virtual) { }
        public override void Validate()
        {
            base.Validate();
            // Virtual 无特有配置
        }
    }
}
