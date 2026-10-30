using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Professional.Automation.Scada
{
    /// <summary>MEM工程资源与MHS Connector的稳定引用，不复制设备规约地址/运行值。</summary>
    public sealed class PADScadaDeviceInstallation
    {
        [FCMConfigItem("MEM设备资源Guid",Order=1)]
        [Required]
        public string DeviceNodeGuid {get;set;} = "";
        [FCMConfigItem("Connector模块身份",Order=2)]
        [Required]
        public string ConnectorModuleId {get;set;} = "";
        [FCMConfigItem("MDS Scope",Order=3)]
        [Required]
        public string ScopeId {get;set;} = "";
        [FCMConfigItem("采样周期",Order=4,Unit="ms")]
        [Range(20,3600000)]
        public int IntervalMs {get;set;} = 1000;
        [FCMConfigItem("连续Holding单FC03批读",Order=5)]
        public bool Contiguous {get;set;} = true;
        [FCMConfigItem("是否部署",Order=6)]
        public bool Enabled {get;set;} = true;
    }

    /// <summary>
    /// 本配置段的身份是正式MEM Project.Guid，存储/修订/审批/Effective由MHS Owner提供。
    /// 仅保存DeviceRef→ConnectorRef/Scope与采集规则，不建第二份工程树、PointTable或Current。
    /// </summary>
    [FCMConfigSection("CxVisual.Automation.ScadaDeployment",SchemaVersion=1)]
    [FCMConfigValidator(typeof(PADScadaDeploymentValidator))]
    public sealed class PADScadaDeploymentConfiguration
    {
        [FCMConfigItem("工站设备运行装配",Order=1)]
        public List<PADScadaDeviceInstallation> Devices {get;set;} =
            new List<PADScadaDeviceInstallation>();

        public void Validate()
        {
            FCMConfigIssue error=new PADScadaDeploymentValidator()
                .Validate(this,null).FirstOrDefault();
            if(error!=null)throw new ArgumentException(error.Code+": "+error.Message);
        }
    }

    public sealed class PADScadaDeploymentValidator :
        IFCMConfigValidator<PADScadaDeploymentConfiguration>
    {
        public IEnumerable<FCMConfigIssue> Validate(
            PADScadaDeploymentConfiguration value,FCMConfigValidationContext context)
        {
            if(value==null||value.Devices==null||
                value.Devices.Count<1||value.Devices.Count>32)
            {
                yield return FCMConfigIssue.Error("Devices","SCADA_DEVICE_COUNT",
                    "必须配置1～32个明确的工程设备");
                yield break;
            }
            HashSet<Guid> devices=new HashSet<Guid>();
            HashSet<string> scopes=new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> modules=new HashSet<string>(StringComparer.Ordinal);
            int enabled=0;
            for(int i=0;i<value.Devices.Count;i++)
            {
                PADScadaDeviceInstallation item=value.Devices[i];
                Guid device;
                if(item==null || !Guid.TryParse(item.DeviceNodeGuid,out device) ||
                    device==Guid.Empty ||
                    string.IsNullOrWhiteSpace(item.ConnectorModuleId) ||
                    string.IsNullOrWhiteSpace(item.ScopeId) ||
                    item.IntervalMs<20||item.IntervalMs>3600000)
                {
                    yield return FCMConfigIssue.Error("Devices["+i+"]",
                        "SCADA_DEVICE_INVALID","工程设备、Module、Scope或采集周期无效");
                    continue;
                }
                if(!devices.Add(device) || !scopes.Add(item.ScopeId.Trim()) ||
                    !modules.Add(item.ConnectorModuleId.Trim()))
                    yield return FCMConfigIssue.Error("Devices["+i+"]",
                        "SCADA_DEVICE_DUPLICATE","DeviceRef、Scope、Connector必须在项目内唯一");
                if(item.Enabled)enabled++;
            }
            if(enabled<1)
                yield return FCMConfigIssue.Error("Devices","SCADA_NO_ENABLED",
                    "至少有一台设备明确启用采集");
        }
    }
}
