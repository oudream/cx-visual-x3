using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Professional.Vision;

namespace CxVisual.Studio
{
    /// <summary>
    /// P0: join the existing MFM descriptor and MOC catalog without a second registry.
    /// This first professional provider is a genuine CPU pixel algorithm, but uses
    /// developer preset options. No MHS Effective config, MEM resource, equipment,
    /// production readiness or invocation authorization is implied.
    /// </summary>
    public partial class MainWindow
    {
        private void OnOpenStudioModuleCenter(object sender,RoutedEventArgs args)
        { SafeStudioAction(()=>OpenStudioDocument("doc.modules")); }

        private MPWModuleCenterControl CreateStudioModuleCenterDocument()
        {
            var control=new MPWModuleCenterControl();
            var visionOptions=new CxVisionInspectionOptions();
            // Professional code remains the algorithm Owner. The Studio host only composes
            // this software provider for discovery; no background worker or device is started.
            var vision=new CxVisionThresholdProvider(visionOptions,0,_simulation);
            var catalog=new MOCCapabilityCatalog(
                new IMOCCapabilityProvider[]{_simulation,vision},vision);
            var module=new MFMModuleDescriptor("studio.development.cpu.vision",
                "CPU视觉阈值检测（开发预设）",MFMModuleKind.Compute,
                capabilityIds:new[]{CxVisionThresholdProvider.CapabilityId});
            var declared=catalog.DescribeModules(new[]{module});
            var boundIds=new HashSet<string>(
                declared.Select(x=>x.CapabilityId),StringComparer.Ordinal);
            var real=declared.Select(entry=>new MPWModuleCapabilityView(
                entry.CapabilityId,
                entry.CatalogItem==null?entry.CapabilityId:entry.CatalogItem.Definition.Name,
                entry.CatalogItem==null?"仅模块声明":entry.CatalogItem.Definition.Category,
                entry.Module.Kind,
                entry.CatalogItem==null?entry.Module.Version:entry.CatalogItem.Definition.Version,
                entry.RuntimeResolved,false,entry.Module.Id));
            var unbound=catalog.All.Where(x=>!boundIds.Contains(x.Definition.Id))
                .Select(x=>new MPWModuleCapabilityView(
                    x.Definition.Id,x.Definition.Name,x.Definition.Category,
                    null,x.Definition.Version,x.RuntimeAvailable,true));
            control.SetCapabilities(real.Concat(unbound),
                "实际CPU视觉软件Provider + 原Studio CPU流程演示（只读；非工业运行就绪）");
            control.HeaderContent=new TextBlock {
                Text="工程："+_viewModel.ProjectSession.Project.Name+
                    " · Compute：真实CPU阈值算法，开发预设参数，尚未绑定MEM资源/MHS Effective。" +
                    " 其余采图/AI/输出仍是CPU流程演示，不代表驱动、设备或业务执行授权。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,4,4,6)
            };
            return control;
        }
    }
}
