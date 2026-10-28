using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CxVisual.Mechanism.Core.Modules;

namespace CxVisual.Mechanism.Presentation.Wpf
{
    /// <summary>Pure UI projection of the existing five configuration schemas; no new registry.</summary>
    public sealed class MPWModuleCenterPage
    {
        public MPWModuleCenterPage(string id,string label,string[] fields,string[] categories=null)
        {
            if(String.IsNullOrWhiteSpace(id)||String.IsNullOrWhiteSpace(label)||fields==null)
                throw new ArgumentException("module.center.page");
            Id=id;Label=label;
            Fields=fields.ToArray();
            Categories=categories==null?new string[0]:categories.ToArray();
        }
        public string Id {get;private set;}
        public string Label {get;private set;}
        public string[] Fields {get;private set;}
        public string[] Categories {get;private set;}
    }

    public static class MPWModuleCenterPages
    {
        public static IReadOnlyList<MPWModuleCenterPage> Create(MFMModuleKind kind,Type modelType)
        {
            if(modelType==null)throw new ArgumentNullException("modelType");
            MPWModuleCenterPage[] pages;
                switch (kind)
                {
                    case MFMModuleKind.Connector:
                        pages = new[] {
                            new MPWModuleCenterPage("identity", "模块信息",
                                new[] { "DisplayName", "Enabled", "CapabilityIds" }),
                            new MPWModuleCenterPage("channels", "通道配置", new[] { "Channel" }),
                            new MPWModuleCenterPage("protocols", "规约配置", new[] {
                                "Protocol.Id", "Protocol.Kind", "Protocol.CustomTypeId",
                                "Protocol.ProtocolAddress" }),
                            new MPWModuleCenterPage("points", "规约点表", new[] { "Protocol.Points" }),
                            new MPWModuleCenterPage("mapping", "数据映射", new[] { "Protocol.Mappings" }),
                            new MPWModuleCenterPage("runtime", "运行参数", new[] {
                                "Protocol.ProcessingMode", "Protocol.SessionScope",
                                "Protocol.MaxConcurrency", "Protocol.QueueCapacity",
                                "Protocol.QueuedBytesLimit", "Protocol.TimeoutMs",
                                "Protocol.ExecutionGroupRef" })
                        };
                        break;
                    case MFMModuleKind.Driver:
                        pages = new[] {
                            new MPWModuleCenterPage("identity", "模块信息",
                                new[] { "DisplayName", "Enabled", "CapabilityIds" }),
                            new MPWModuleCenterPage("provider", "设备驱动",
                                new[] { "DriverTypeId", "DeviceRef", "Backend" }),
                            new MPWModuleCenterPage("resources", "内部资源引用",
                                new[] { "InternalResourceRefs" })
                        };
                        break;
                    case MFMModuleKind.Compute:
                        pages = new[] {
                            new MPWModuleCenterPage("identity", "模块信息",
                                new[] { "DisplayName", "Enabled", "CapabilityIds" }),
                            new MPWModuleCenterPage("provider", "计算能力",
                                new[] { "ProviderTypeId", "Device" }),
                            new MPWModuleCenterPage("parameters", "运行参数",
                                new[] { "MaxConcurrency", "MemoryBudgetMb",
                                    "TimeoutMs", "ExecutionGroupRef" })
                        };
                        break;
                    case MFMModuleKind.Simulation:
                        pages = new[] {
                            new MPWModuleCenterPage("identity", "模块信息",
                                new[] { "DisplayName", "Enabled", "CapabilityIds" }),
                            new MPWModuleCenterPage("model", "仿真模型",
                                new[] { "ModelId", "ProviderTypeId", "ConnectorRef" }),
                            new MPWModuleCenterPage("clock", "时钟与运行模式",
                                new[] { "Mode", "StepMilliseconds", "Seed" })
                        };
                        break;
                    case MFMModuleKind.Service:
                        pages = new[] {
                            new MPWModuleCenterPage("identity", "模块信息",
                                new[] { "DisplayName", "Enabled", "CapabilityIds" }),
                            new MPWModuleCenterPage("provider", "服务能力与依赖",
                                new[] { "ProviderTypeId", "DependencyRefs" }),
                            new MPWModuleCenterPage("lifecycle", "生命周期与并发",
                                new[] { "StartupTimeoutMs", "ShutdownTimeoutMs",
                                    "MaxConcurrentRequests" })
                        };
                        break;
                    default: throw new InvalidOperationException("未知模块类型");
                }

            string[] missing=MPWConfigWorkspaceLayout.UnassignedLeafPaths(modelType,
                pages.Select(page=>(IEnumerable<string>)page.Fields));
            if(missing.Length>0)
                pages=pages.Concat(new[]{new MPWModuleCenterPage("extras","扩展配置",missing)}).ToArray();
            return new ReadOnlyCollection<MPWModuleCenterPage>(pages);
        }
    }
}
