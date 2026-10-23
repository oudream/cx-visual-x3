using System;
using System.ComponentModel.DataAnnotations;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Professional.Simulation.Modules
{
    [FCMConfigSection("CxVisual.Module.Simulation", SchemaVersion = 1)]
    public sealed class PSMSimulationEditableConfig : MFMModuleEditableConfig
    {
        [FCMConfigItem("仿真模型 Id", Order = 10, Category = "模型")]
        [Required]
        public string ModelId { get; set; } = "axis";
        [FCMConfigItem("运行模式", Order = 11, Category = "模型")]
        public PSMSimulationRunMode Mode { get; set; } = PSMSimulationRunMode.Step;
        [FCMConfigItem("固定步长", Order = 12, Category = "时间", Unit = "ms")]
        [Range(1, 3600000)]
        public int StepMilliseconds { get; set; } = 10;
        [FCMConfigItem("可复现随机种子", Order = 13, Category = "时间")]
        public int Seed { get; set; } = 1;
        [FCMConfigItem("模型 Provider TypeId", Order = 14, Category = "模型")]
        public string ProviderTypeId { get; set; } = "";
        [FCMConfigItem("关联通道资源 Id（可为空）", Order = 15, Category = "连接")]
        public string ConnectorRef { get; set; } = "";

        public PSMSimulationDefinition ToDefinition()
        {
            return new PSMSimulationDefinition(ModelId, TimeSpan.FromMilliseconds(StepMilliseconds), Seed, Mode);
        }
    }
}
