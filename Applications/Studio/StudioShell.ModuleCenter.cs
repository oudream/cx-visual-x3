using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;
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
            ProjectStudioModuleCenterCapabilities(control,vision,
                "CPU计算Provider：开发预设参数；尚未载入MHS Effective");
            var header=new StackPanel();
            header.Children.Add(new TextBlock {
                Text="工程："+_viewModel.ProjectSession.Project.Name+
                    " · Compute：真实CPU阈值算法，开发预设参数，尚未绑定MEM资源/MHS Effective。" +
                    " 其余采图/AI/输出仍是CPU流程演示，不代表驱动、设备或业务执行授权。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,4,4,6)
            });
            // Navigate to the original MEM/FCM/MHS/MOL owners; do not build private forms
            // or silently bind the selected capability to an unrelated project resource.
            var actions=new WrapPanel {Margin=new Thickness(1,2,1,6)};
            var configure=new Button {Content="打开工程专业配置",Padding=new Thickness(12,5,12,5),
                Margin=new Thickness(3,1,6,1),
                ToolTip="打开原Studio FCM/MHS配置页；当前不会自动将开发CPU预设绑定到MEM资源"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(configure,
                "Studio.ModuleCenter.OpenConfiguration");
            configure.Click+=(sender,args)=>SafeStudioAction(()=>OpenStudioDocument("doc.config"));
            actions.Children.Add(configure);
            var loadEffective=new Button {
                Content="读取当前生效配置",Padding=new Thickness(12,5,12,5),
                Margin=new Thickness(3,1,6,1),
                ToolTip="仅在点击时由原MHS读取当前Scope的Effective版本；遵守配置授权，不保存/Apply、不运行设备"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(loadEffective,
                "Studio.ModuleCenter.LoadEffective");
            var effectiveStatus=new TextBlock {
                Text="当前为开发预设；读取生效配置需要显式操作。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,2,4,5)};
            loadEffective.Click+=(sender,args)=>{
                try {
                    var loaded=LoadStudioEffectiveVisionProvider();
                    ProjectStudioModuleCenterCapabilities(control,loaded.Item1,
                        "MHS Effective "+loaded.Item2+"（真实配置快照，仅软件能力发现）");
                    effectiveStatus.Text="已从正式MHS读取 Effective "+loaded.Item2+
                        "；CPU Provider版本 "+loaded.Item1.ReleaseVersion+
                        "。未运行、未授权发布，也未创建MEM模块资源。";
                }
                catch(UnauthorizedAccessException) {
                    effectiveStatus.Text="当前身份没有所选Scope的Effective配置读取权限。";
                }
                catch(Exception ex) {
                    effectiveStatus.Text="生效配置未加载："+ex.Message+
                        "；仍保留原开发预设能力，未执行任何算法或设备动作。";
                }
            };
            actions.Children.Add(loadEffective);
            var flow=new Button {Content="打开主流程编排",Padding=new Thickness(12,5,12,5),
                Margin=new Thickness(3,1,6,1),
                ToolTip="打开原MOL流程编辑器；不会跳过MOC构建、MRK执行和授权"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(flow,
                "Studio.ModuleCenter.OpenFlow");
            flow.Click+=(sender,args)=>SafeStudioAction(()=>OpenStudioDocument("doc.flow"));
            actions.Children.Add(flow);
            header.Children.Add(actions);
            header.Children.Add(effectiveStatus);
            control.HeaderContent=header;
            return control;
        }

        /// <summary>
        /// Explicit Studio host read: the same config root and selected scope as
        /// doc.config. An authenticated host uses only the MHS authorized session;
        /// forbidden reads must not fall back to the raw service.
        /// </summary>
        private Tuple<CxVisionThresholdProvider,string> LoadStudioEffectiveVisionProvider()
        {
            if(HasStudioUnsavedConfiguration())
                throw new InvalidOperationException("请先保存或放弃专业配置草稿");
            EnsureStudioConfigurationDefaults();
            var options=StudioConfigurationScopeChoices();
            var scope=options.FirstOrDefault(x=>
                x.Kind==_studioConfigurationScopeKind &&
                x.Id==_studioConfigurationScopeId)??options.FirstOrDefault();
            if(scope==null)throw new InvalidOperationException("请先打开有效工程");
            string sectionId=FCMConfigSchemaBuilder.Describe<CxVisionInspectionOptions>().SectionId;
            StudioConfigurationContribution contribution;
            if(!_studioConfigurations.TryGetValue(sectionId,out contribution)||
                !ConfigurationMatchesScope(contribution,scope))
                throw new InvalidOperationException("所选资源Scope没有Vision配置");
            string root=Path.Combine(GetDemoDataRoot(),"Configs","V24","Projects");
            var service=new MHSConfigService(root);
            var identity=new FCMConfigIdentity(sectionId,scope.Kind,scope.Id);
            CxVisionThresholdProvider provider;
            string version;
            if(_studioConfigPrincipal!=null)
            {
                // Authorized capture and subsequent version check stay policy-bound.
                var section=service.Authorize(_studioConfigPrincipal,_studioConfigAccessPolicy)
                    .Open<CxVisionInspectionOptions>(identity);
                provider=CxVisionThresholdProvider.FromEffectiveConfiguration(section,_simulation);
                version=section.Revision.Effective;
            }
            else
            {
                // Raw service is used only by Studio's trusted local host in the
                // same manner as the existing doc.config when no IAM is installed.
                var section=service.Open<CxVisionInspectionOptions>(identity);
                provider=CxVisionThresholdProvider.FromEffectiveConfiguration(section,_simulation);
                version=section.EffectiveVersion;
            }
            return Tuple.Create(provider,scope.Label+" / "+version);
        }

        private void ProjectStudioModuleCenterCapabilities(MPWModuleCenterControl control,
            CxVisionThresholdProvider vision,string description)
        {
            // One MOC catalog, no parallel registration or execution dispatcher.
            var catalog=new MOCCapabilityCatalog(
                new IMOCCapabilityProvider[]{_simulation,vision},vision);
            var module=new MFMModuleDescriptor("studio.development.cpu.vision",
                "CPU视觉阈值检测（未绑定MEM模块资源）",MFMModuleKind.Compute,
                capabilityIds:new[]{CxVisionThresholdProvider.CapabilityId});
            var declared=catalog.DescribeModules(new[]{module});
            var ids=new HashSet<string>(declared.Select(x=>x.CapabilityId),
                StringComparer.Ordinal);
            var professional=declared.Select(entry=>new MPWModuleCapabilityView(
                entry.CapabilityId,
                entry.CatalogItem==null?entry.CapabilityId:entry.CatalogItem.Definition.Name,
                entry.CatalogItem==null?"仅模块声明":entry.CatalogItem.Definition.Category,
                entry.Module.Kind,
                entry.CatalogItem==null?entry.Module.Version:entry.CatalogItem.Definition.Version,
                entry.RuntimeResolved,false,entry.Module.Id,
                described:entry.Described,resolverChecked:true));
            var demonstrations=catalog.All.Where(x=>!ids.Contains(x.Definition.Id))
                .Select(x=>new MPWModuleCapabilityView(
                    x.Definition.Id,x.Definition.Name,x.Definition.Category,
                    null,x.Definition.Version,x.RuntimeAvailable,true));
            control.SetCapabilities(professional.Concat(demonstrations),
                description+"；Runtime解析不等于授权或真实设备就绪");
        }
    }
}
