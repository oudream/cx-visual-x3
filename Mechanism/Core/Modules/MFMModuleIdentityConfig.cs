using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Core.Modules
{
    // 同一个 module Scope 只能登记一种模块分类；这是配置真源的一部分，非全局 ModuleManager。
    [FCMConfigSection("CxVisual.Module.Identity", SchemaVersion = 1)]
    public sealed class MFMModuleIdentityConfig
    {
        [FCMConfigItem("模块类别", Order = 1, ApplyPolicy = FCMConfigApplyPolicy.Immediate)]
        public MFMModuleKind? Kind { get; set; }
    }
}
