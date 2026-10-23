using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using CxVisual.Foundation.Data.Configuration;

namespace CxVisual.Mechanism.Core.Modules
{
    // Driver 是第五种功能模块：公开设备行为；内部可以是 SDK、专业 I/O 或混合实现。
    // 不强制有 Channel/Protocol，不创建 EmptyChannel；专业驱动可直接继承本配置以增加专用参数。
    public interface IMFMDriverModule : IMFMModule
    {
        string DeviceRef { get; }
    }

    public enum MFMDriverBackendKind { Sdk, InternalTransport, Hybrid }

    [FCMConfigSection("CxVisual.Module.Driver", SchemaVersion = 1)]
    [FCMConfigValidator(typeof(MFMDriverModuleConfigurationValidator))]
    public class MFMDriverModuleConfiguration : MFMModuleConfiguration
    {
        [FCMConfigItem("驱动 Provider TypeId", Order = 10, Category = "驱动")]
        [Required]
        public string DriverTypeId { get; set; } = "";

        [FCMConfigItem("设备资源引用", Order = 11, Category = "设备",
            Description = "引用 MEM 中的设备身份；不是在 Driver 内复制第二棵设备树")]
        public string DeviceRef { get; set; } = "";

        [FCMConfigItem("驱动实现方式", Order = 12, Category = "驱动",
            Description = "SDK、内部通信或混合均由驱动直接实现；不要求外部 Connector 或 EmptyChannel")]
        public MFMDriverBackendKind Backend { get; set; } = MFMDriverBackendKind.Sdk;

        [FCMConfigItem("内部资源引用", Order = 13, Category = "驱动",
            Description = "可引用已有通信/SDK资源；具体内部通道参数由专业驱动配置提供")]
        public List<string> InternalResourceRefs { get; set; } = new List<string>();

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(DriverTypeId) ||
                !Enum.IsDefined(typeof(MFMDriverBackendKind), Backend) || InternalResourceRefs == null ||
                InternalResourceRefs.Any(string.IsNullOrWhiteSpace) ||
                InternalResourceRefs.Distinct(StringComparer.Ordinal).Count() != InternalResourceRefs.Count)
                throw new ArgumentException("Driver 类型、名称或内部资源引用无效");
        }
    }

    public sealed class MFMDriverModuleConfigurationValidator : IFCMConfigValidator<MFMDriverModuleConfiguration>
    {
        public IEnumerable<FCMConfigIssue> Validate(MFMDriverModuleConfiguration value, FCMConfigValidationContext context)
        {
            if (value == null) return new[] { FCMConfigIssue.Error("", "DRIVER_NULL", "驱动配置不存在") };
            List<FCMConfigIssue> issues = new List<FCMConfigIssue>();
            if (string.IsNullOrWhiteSpace(value.DriverTypeId))
                issues.Add(FCMConfigIssue.Error("DriverTypeId", "DRIVER_PROVIDER_REQUIRED", "必须选择真实驱动 Provider"));
            if (!Enum.IsDefined(typeof(MFMDriverBackendKind), value.Backend))
                issues.Add(FCMConfigIssue.Error("Backend", "DRIVER_BACKEND_INVALID", "未知驱动实现方式"));
            if (value.InternalResourceRefs == null || value.InternalResourceRefs.Any(string.IsNullOrWhiteSpace) ||
                (value.InternalResourceRefs != null &&
                 value.InternalResourceRefs.Distinct(StringComparer.Ordinal).Count() != value.InternalResourceRefs.Count))
                issues.Add(FCMConfigIssue.Error("InternalResourceRefs", "DRIVER_RESOURCES_INVALID", "内部资源引用不能为空或重复"));
            return issues;
        }
    }
}
