using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;
using System.Windows;
using System.Windows.Controls;
using CxVisual.Mechanism.Core.Modules;
using CxVisual.Mechanism.Core;
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
        private sealed class StudioRawPixelSource : IMOCCapabilityProvider,IMRKVersionedCapabilityResolver
        {
            internal const string Id="vision.studio.raw.pixels";
            private readonly byte[] _pixels;
            private readonly string _version;
            private readonly IMRKVersionedCapabilityResolver _vision;
            private readonly MRKResolvedCapability _resolved;
            internal StudioRawPixelSource(byte[] pixels,IMRKVersionedCapabilityResolver vision)
            {
                if(pixels==null||pixels.Length==0)throw new ArgumentException("empty RAW input");
                _pixels=(byte[])pixels.Clone();_vision=vision;
                using(var sha=SHA256.Create())
                    _version="raw.sha256."+BitConverter.ToString(sha.ComputeHash(_pixels)).Replace("-","");
                _resolved=new MRKResolvedCapability((ctx,token)=>{
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult((object)((byte[])_pixels.Clone()));
                },new MRKExecutionOutput(typeof(byte[])));
            }
            public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
            {
                return new[]{new MOCCapabilityDescriptor(Id,"本次RAW文件数据","Vision/Input",
                    new[]{new MPCPortDescriptor("Pixels",MPCPortDirection.Output,"byte[]",
                        MPCPortKind.Data)})};
            }
            public bool TryResolve(string id,out MRKResolvedCapability result)
            {
                if(id==Id){result=_resolved;return true;}
                return _vision.TryResolve(id,out result);
            }
            public bool TryGetCapabilityVersion(string id,out string version)
            {
                if(id==Id){version=_version;return true;}
                return _vision.TryGetCapabilityVersion(id,out version);
            }
        }
        internal sealed class StudioCpuVisionReport
        {
            public Guid PageId,FlowId,InspectId;
            public int EngineeringRevision,CompletedTraceEvents;
            public string PlanFingerprint,ConfigVersion;
            public CxVisionInspectionResult Inspection;
        }
        private static byte[] ReadStudioRaw(string path,int width,int height,
            CxVisionPixelDepth depth,CancellationToken token)
        {
            long count=(long)width*height;
            if(width<=0||height<=0||count>32000000L)
                throw new ArgumentOutOfRangeException("width/height");
            if(depth!=CxVisionPixelDepth.Bits8&&depth!=CxVisionPixelDepth.Bits16)
                throw new ArgumentException("Invalid Effective pixel depth");
            int length=checked((int)(count*(depth==CxVisionPixelDepth.Bits8?1:2)));
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length!=length)throw new InvalidDataException(
                    "RAW长度与宽、高、当前Effective位深不匹配");
                byte[] bytes=new byte[length];int done=0;
                while(done<length)
                {
                    token.ThrowIfCancellationRequested();
                    int read=stream.Read(bytes,done,length-done);
                    if(read<=0)throw new EndOfStreamException("RAW truncated");
                    done+=read;
                }
                if(stream.ReadByte()!=-1)throw new InvalidDataException("RAW grew during read");
                return bytes;
            }
        }
        /// <summary>
        /// Software-only full path: real RAW -> MEM page/typed MOC graph -> version
        /// attested MRK authorized software release -> CPU inspection -> SourceMap trace.
        /// The manual RAW bytes live only during this call; on reopen they must be
        /// supplied again. No device, hidden scheduler or new result owner.
        /// </summary>
        internal static async Task<StudioCpuVisionReport> ExecuteStudioCpuVisionFileAsync(
            MEMEngineeringSession session,CxVisionThresholdProvider provider,
            string path,int width,int height,CancellationToken token)
        {
            if(session==null||provider==null)throw new ArgumentNullException("session/provider");
            token.ThrowIfCancellationRequested();
            string version;
            if(!provider.TryGetCapabilityVersion(CxVisionThresholdProvider.CapabilityId,out version))
                throw new InvalidOperationException("CPU Provider version missing");
            // Fail validation before modifying the MEM engineering project.
            byte[] input=ReadStudioRaw(path,width,height,provider.PixelDepth,token);
            var source=new StudioRawPixelSource(input,provider);
            token.ThrowIfCancellationRequested();
            var page=session.AddPage("CPU RAW检测 · "+Path.GetFileName(path));
            Guid inspect=Guid.Empty;
            MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                Guid inputBlock=b.Step(source.DescribeCapabilities().Single());
                inspect=b.Step(provider.DescribeCapabilities().Single());
                b.SetParameter(inspect,"Width",width.ToString(CultureInfo.InvariantCulture));
                b.SetParameter(inspect,"Height",height.ToString(CultureInfo.InvariantCulture));
                b.Connect(inputBlock,"Done",inspect,"Start",MPCPortKind.Control);
                b.Connect(inputBlock,"Pixels",inspect,"RawPixels",MPCPortKind.Data);
            });
            string sourceVersion;
            source.TryGetCapabilityVersion(StudioRawPixelSource.Id,out sourceVersion);
            var versions=new Dictionary<string,string>(StringComparer.Ordinal){
                {StudioRawPixelSource.Id,sourceVersion},
                {CxVisionThresholdProvider.CapabilityId,version}};
            MEMProjectSnapshot snapshot=session.GetSnapshot();
            var candidate=MOCProjectRelease.Prepare(snapshot,page.RootContent.Id,source,versions);
            token.ThrowIfCancellationRequested();
            var compiled=StageCpuSoftwareRelease(candidate,session.GetSnapshot(),source,
                versions,new StudioCpuSoftwarePolicy());
            var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,16));
            try
            {
                var defaults=compiled.InputDefaults.ToDictionary(x=>x.Key,x=>x.Value,
                    StringComparer.Ordinal);
                MRKFlowHandle flow=engine.StartFlow(compiled.Plan,defaults);
                using(token.Register(()=>flow.RequestCancel()))
                {
                    MRKFlowResult result=await flow.Completion.ConfigureAwait(false);
                    if(result.State!=MRKFlowState.Completed)
                        throw new InvalidOperationException("MRK未完成："+result.State+
                            (result.Error==null?"":" / "+result.Error.Message));
                    object rawResult;
                    if(!result.Results.TryGetValue("w"+inspect.ToString("N"),out rawResult))
                        throw new InvalidOperationException("MRK缺少检测StepResult");
                    var inspection=rawResult as CxVisionInspectionResult;
                    if(inspection==null||inspection.Width!=width||inspection.Height!=height||
                        inspection.ConfigRevision!=provider.ConfigurationRevision)
                        throw new InvalidOperationException("MRK结果尺寸、类型或配置版本不匹配");
                    var trace=compiled.ReadTrace(flow,0);
                    int completed=trace.Events.Count(e=>e.Origin.BlockId==inspect&&
                        e.Transition.State==MRKStepState.Completed);
                    if(completed==0||trace.LostCount!=0)
                        throw new InvalidOperationException("检测完成Trace缺失或丢失");
                    return new StudioCpuVisionReport {
                        PageId=page.Id,FlowId=result.FlowId,InspectId=inspect,
                        EngineeringRevision=snapshot.Revision,
                        PlanFingerprint=candidate.Fingerprint,ConfigVersion=version,
                        Inspection=inspection,CompletedTraceEvents=completed
                    };
                }
            }
            finally{await engine.StopAsync(true).ConfigureAwait(false);}
        }

        private void OnOpenStudioModuleCenter(object sender,RoutedEventArgs args)
        { SafeStudioAction(()=>OpenStudioDocument("doc.modules")); }

        private MPWModuleCenterControl CreateStudioModuleCenterDocument()
        {
            var control=new MPWModuleCenterControl();
            var visionOptions=new CxVisionInspectionOptions();
            // Professional code remains the algorithm Owner. The Studio host only composes
            // this software provider for discovery; no background worker or device is started.
            var vision=new CxVisionThresholdProvider(visionOptions,0,_simulation);
            CxVisionThresholdProvider selectedProvider=vision;
            bool effectiveLoaded=false;
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
                ToolTip="只读取该Scope已经持久化的MHS Effective；尚无配置时提示先配置，不创建默认数据、不Apply或运行设备"};
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
                    selectedProvider=loaded.Item1;
                    effectiveLoaded=true;
                    effectiveStatus.Text="已从正式MHS读取 Effective "+loaded.Item2+
                        "；CPU Provider版本 "+loaded.Item1.ReleaseVersion+
                        "。未运行、未授权发布，也未创建MEM模块资源。";
                }
                catch(UnauthorizedAccessException) {
                    effectiveStatus.Text="当前身份没有所选Scope的Effective配置读取权限。";
                }
                catch(FileNotFoundException) {
                    effectiveStatus.Text="该Scope还没有持久化的Vision配置；请先打开工程专业配置、完成验证和保存，再读取Effective。";
                }
                catch(Exception ex) {
                    effectiveStatus.Text="生效配置未加载："+ex.Message+
                        "；仍保留原开发预设能力，未执行任何算法或设备动作。";
                }
            };
            actions.Children.Add(loadEffective);
            var addFlow=new Button {
                Content="加入当前主流程",Padding=new Thickness(12,5,12,5),
                Margin=new Thickness(3,1,6,1),
                ToolTip="选中正式Compute能力并读取MHS Effective后才写入已有MEM主Content；必须继续连线、MOC构建和MRK授权"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(addFlow,
                "Studio.ModuleCenter.AddToFlow");
            addFlow.Click+=(sender,args)=>{
                try {
                    MPWModuleCapabilityView selected=control.SelectedCapability;
                    if(selected==null||selected.Demonstration||
                        selected.Kind!=MFMModuleKind.Compute||
                        selected.Id!=CxVisionThresholdProvider.CapabilityId||
                        !selected.Described||!selected.Resolved)
                        throw new InvalidOperationException("请先选中已注册的真实Compute算法能力");
                    if(!effectiveLoaded)
                        throw new InvalidOperationException("请先读取当前MHS生效配置，再加入工程流程");
                    if(_logicRunning)throw new InvalidOperationException("运行中的主流程不可编辑");
                    // Re-check MHS version and permissions on the pinned Provider.
                    string version;
                    if(!selectedProvider.TryGetCapabilityVersion(selected.Id,out version))
                        throw new InvalidOperationException("当前Provider版本无法确认");
                    var page=_viewModel.ActivePage;
                    if(page==null)throw new InvalidOperationException("请先选择有效工程页面");
                    var catalog=new MOCCapabilityCatalog(
                        new IMOCCapabilityProvider[]{_simulation,selectedProvider},selectedProvider);
                    var capability=catalog.Find(selected.Id);
                    if(capability==null||!capability.RuntimeAvailable)
                        throw new InvalidOperationException("MOC当前无法解析此软件能力");
                    MOCFlowBuilder.Edit(_viewModel.ProjectSession,page.RootContent,
                        builder=>builder.Step(capability.Definition));
                    RefreshScene();
                    OpenStudioDocument("doc.flow");
                    effectiveStatus.Text="已加入当前MEM主流程："+selected.Id+
                        " / "+version+"。这是未连线的能力节点，需完成输入绑定、MOC构建与MRK授权后才能执行。";
                }
                catch(UnauthorizedAccessException) {
                    effectiveStatus.Text="配置读取权限已撤销，不允许新增该版本的流程能力。";
                }
                catch(Exception ex) {
                    effectiveStatus.Text="加入流程被拒绝："+ex.Message;
                }
            };
            actions.Children.Add(addFlow);
            var rawRow=new WrapPanel {Margin=new Thickness(4,3,4,2)};
            rawRow.Children.Add(new TextBlock {Text="RAW宽",VerticalAlignment=VerticalAlignment.Center});
            var imageWidth=new TextBox {Text="5",Width=64,Margin=new Thickness(5,0,10,0)};
            System.Windows.Automation.AutomationProperties.SetAutomationId(imageWidth,
                "Studio.ModuleCenter.RawWidth");
            rawRow.Children.Add(imageWidth);
            rawRow.Children.Add(new TextBlock {Text="高",VerticalAlignment=VerticalAlignment.Center});
            var imageHeight=new TextBox {Text="3",Width=64,Margin=new Thickness(5,0,10,0)};
            System.Windows.Automation.AutomationProperties.SetAutomationId(imageHeight,
                "Studio.ModuleCenter.RawHeight");
            rawRow.Children.Add(imageHeight);
            var runRaw=new Button {Content="选择RAW并执行CPU检测",Padding=new Thickness(10,5,10,5),
                Margin=new Thickness(4,0,0,0),
                ToolTip="真实本地RAW文件，仅CPU软件验证；新建独立MEM检测页，MOC/MRK版本锁定执行并返回缺陷和Trace"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(runRaw,
                "Studio.ModuleCenter.RunRaw");
            rawRow.Children.Add(runRaw);
            System.Threading.CancellationTokenSource activeRun=null;
            var cancelRun=new Button {Content="取消检测",Padding=new Thickness(8,5,8,5),
                Margin=new Thickness(5,0,0,0),IsEnabled=false};
            System.Windows.Automation.AutomationProperties.SetAutomationId(cancelRun,
                "Studio.ModuleCenter.CancelRaw");
            cancelRun.Click+=(sender,args)=>{if(activeRun!=null)activeRun.Cancel();};
            rawRow.Children.Add(cancelRun);
            var rawStatus=new TextBlock {Text="等待用户选择RAW文件；位深来自已读取的MHS Effective。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,3,4,5)};
            runRaw.Click+=async (sender,args)=>{
                if(_logicRunning||activeRun!=null){rawStatus.Text="已有流程运行，禁止并行检测";return;}
                if(!effectiveLoaded){rawStatus.Text="请先读取MHS Effective配置";return;}
                int w,h;
                if(!Int32.TryParse(imageWidth.Text,out w)||
                    !Int32.TryParse(imageHeight.Text,out h)||w<=0||h<=0||
                    (long)w*h>32000000L)
                {rawStatus.Text="RAW图像宽高不合法";return;}
                var dialog=new Microsoft.Win32.OpenFileDialog {
                    Title="选择真实RAW灰度图像",Filter="RAW images (*.raw)|*.raw|全部文件 (*.*)|*.*",
                    CheckFileExists=true};
                if(dialog.ShowDialog(this)!=true)return;
                activeRun=new System.Threading.CancellationTokenSource();
                cancelRun.IsEnabled=true;runRaw.IsEnabled=false;_logicRunning=true;
                try
                {
                    rawStatus.Text="真实RAW数据检查 / MEM-MOC-MRK CPU执行中…";
                    var report=await ExecuteStudioCpuVisionFileAsync(
                        _viewModel.ProjectSession,selectedProvider,dialog.FileName,w,h,
                        activeRun.Token);
                    var page=_viewModel.ProjectSession.Find(report.PageId) as MEMPage;
                    if(page!=null)_viewModel.ActivePage=page;
                    RefreshScene();OpenStudioDocument("doc.flow");
                    rawStatus.Text="实际CPU检测"+(report.Inspection.Passed?"通过":"发现缺陷")+
                        "；阈值像素="+report.Inspection.ThresholdPixelCount+
                        "，缺陷区域="+report.Inspection.Regions.Count+
                        "，配置="+report.ConfigVersion+
                        "，MRK FlowId="+report.FlowId.ToString("N")+
                        "，工程Revision="+report.EngineeringRevision+
                        "，Trace完成事件="+report.CompletedTraceEvents+
                        "，MOC指纹="+report.PlanFingerprint.Substring(0,12)+
                        "。MEM已生成独立检测页；RAW输入仅供本次运行，重开需重新绑定。";
                }
                catch(OperationCanceledException){rawStatus.Text="用户取消，未报告执行成功";}
                catch(Exception ex){rawStatus.Text="真实CPU检测失败："+ex.Message;}
                finally{
                    _logicRunning=false;runRaw.IsEnabled=true;cancelRun.IsEnabled=false;
                    activeRun.Dispose();activeRun=null;
                }
            };
            var flow=new Button {Content="打开主流程编排",Padding=new Thickness(12,5,12,5),
                Margin=new Thickness(3,1,6,1),
                ToolTip="打开原MOL流程编辑器；不会跳过MOC构建、MRK执行和授权"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(flow,
                "Studio.ModuleCenter.OpenFlow");
            flow.Click+=(sender,args)=>SafeStudioAction(()=>OpenStudioDocument("doc.flow"));
            actions.Children.Add(flow);
            header.Children.Add(actions);
            header.Children.Add(rawRow);
            header.Children.Add(effectiveStatus);
            header.Children.Add(rawStatus);
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
                    .OpenExisting<CxVisionInspectionOptions>(identity);
                provider=CxVisionThresholdProvider.FromEffectiveConfiguration(section,_simulation);
                version=section.Revision.Effective;
            }
            else
            {
                // Raw service is used only by Studio's trusted local host in the
                // same manner as the existing doc.config when no IAM is installed.
                var section=service.OpenExisting<CxVisionInspectionOptions>(identity);
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
                described:entry.Described,resolverChecked:true,
                inputPorts:entry.CatalogItem==null?null:
                    entry.CatalogItem.Definition.Ports
                        .Where(x=>x.Direction==MPCPortDirection.Input)
                        .Select(x=>x.MemberId+" : "+x.TypeId+(x.Required?" (必填)":"")),
                outputPorts:entry.CatalogItem==null?null:
                    entry.CatalogItem.Definition.Ports
                        .Where(x=>x.Direction==MPCPortDirection.Output)
                        .Select(x=>x.MemberId+" : "+x.TypeId)));
            var demonstrations=catalog.All.Where(x=>!ids.Contains(x.Definition.Id))
                .Select(x=>new MPWModuleCapabilityView(
                    x.Definition.Id,x.Definition.Name,x.Definition.Category,
                    null,x.Definition.Version,x.RuntimeAvailable,true,
                    inputPorts:x.Definition.Ports
                        .Where(port=>port.Direction==MPCPortDirection.Input)
                        .Select(port=>port.MemberId+" : "+port.TypeId),
                    outputPorts:x.Definition.Ports
                        .Where(port=>port.Direction==MPCPortDirection.Output)
                        .Select(port=>port.MemberId+" : "+port.TypeId)));
            control.SetCapabilities(professional.Concat(demonstrations),
                description+"；Runtime解析不等于授权或真实设备就绪");
        }
    }
}
