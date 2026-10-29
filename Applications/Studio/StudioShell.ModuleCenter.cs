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
using CxVisual.Mechanism.Decision;
using CxVisual.Mechanism.Presentation.Wpf;
using CxVisual.Foundation.Imaging;
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
        internal sealed class StudioCpuTraceRecord
        {
            public long Sequence {get;set;}
            public string StepId {get;set;}
            public Guid BlockId {get;set;}
            public string State {get;set;}
            public DateTime TimestampUtc {get;set;}
        }
        internal sealed class StudioCpuVisionReport
        {
            public IReadOnlyList<StudioCpuTraceRecord> TraceRecords;
            public Guid PageId,FlowId,InspectId,MeasureId,SegmentId,EnhanceId;
            public long? AcceptedRegionPixelArea;
            public int EngineeringRevision,CompletedTraceEvents;
            public string PlanFingerprint,ConfigVersion;
            public CxVisionInspectionResult Inspection;
            public CxVisionRunSummary Evidence;
        }
        /// <summary>
        /// Studio delegates the real CPU operation to the same professional runner used by
        /// TestCenter. MEM/MOC/MRK algorithm/release logic must have only one implementation.
        /// </summary>
        internal static async Task<StudioCpuVisionReport> ExecuteStudioCpuVisionFileAsync(
            MEMEngineeringSession session,CxVisionThresholdProvider provider,
            string path,int width,int height,CancellationToken cancellation)
        {
            CxVisionCpuRun report=await CxVisionRawCpuRunner.ExecuteMeasuredAsync(session,provider,
                path,width,height,cancellation).ConfigureAwait(false);
            return ToStudioCpuVisionReport(report);
        }

        /// <summary>使用同一MHS Effective Provider实际创建可保存的四/五节点工程。</summary>
        internal static async Task<StudioCpuVisionReport> ExecuteStudioSeparatedCpuVisionFileAsync(
            MEMEngineeringSession session,CxVisionThresholdProvider provider,
            string path,int width,int height,int medianRadius,CancellationToken cancellation)
        {
            CxVisionCpuRun report=await CxVisionRawCpuRunner
                .ExecuteSeparatedMeasuredProviderAsync(session,provider,path,width,height,
                    medianRadius,cancellation).ConfigureAwait(false);
            // 本次算法完成后仍要求MHS Effective有效，撤销/变更不能发布成功证据。
            provider.CaptureInspectionOptions();
            return ToStudioCpuVisionReport(report);
        }

        private static StudioCpuVisionReport ToStudioCpuVisionReport(CxVisionCpuRun report)
        {
            return new StudioCpuVisionReport {
                PageId=report.PageId,FlowId=report.FlowId,InspectId=report.InspectBlockId,
                MeasureId=report.MeasureBlockId,SegmentId=report.SegmentBlockId,
                EnhanceId=report.EnhanceBlockId,
                AcceptedRegionPixelArea=report.AcceptedRegionPixelArea,
                EngineeringRevision=report.EngineeringRevision,
                CompletedTraceEvents=report.CompletedTraceEvents,
                PlanFingerprint=report.Fingerprint,ConfigVersion=report.ConfigVersion,
                Inspection=report.Inspection,Evidence=report.Evidence,
                TraceRecords=report.Trace.Select(x=>new StudioCpuTraceRecord {
                    Sequence=x.Sequence,StepId=x.StepId,BlockId=x.BlockId,
                    State=x.State,TimestampUtc=x.TimestampUtc
                }).ToArray()
            };
        }

        /// <summary>
        /// 单一MOC目录只组装已有专业Provider，所有参数来自同一MHS Effective。
        /// 对自定义数据端口仍由专业运行时实施具体类型验证。
        /// </summary>
        private sealed class StudioVisionCapabilitySet
        {
            public IMOCCapabilityProvider[] Providers;
            public IMRKVersionedCapabilityResolver Resolver;
            public CxVisionBinaryThresholdProvider Threshold;
            public CxVisionMaskRegionsProvider Regions;
            public CxVisionAcceptedRegionMeasureProvider Measurement;
        }

        private static StudioVisionCapabilitySet CreateStudioVisionCapabilities(
            CxVisionThresholdProvider vision)
        {
            if(vision==null)throw new ArgumentNullException("vision");
            CxVisionInspectionOptions options=vision.CaptureInspectionOptions();
            var measure=new CxVisionAcceptedRegionMeasureProvider(vision);
            var regions=new CxVisionMaskRegionsProvider(
                options,vision.ConfigurationRevision,measure);
            var threshold=new CxVisionBinaryThresholdProvider(
                options.PixelDepth,options.GrayThreshold,regions);
            return new StudioVisionCapabilitySet {
                Providers=new IMOCCapabilityProvider[]{vision,threshold,regions,measure},
                Resolver=threshold,Threshold=threshold,Regions=regions,Measurement=measure
            };
        }

        /// <summary>
        /// Studio模块中心的唯一可复用编辑入口：先核对MOC声明/MRK运行绑定、
        /// MHS生效参数与版本，再由原MOCFlowBuilder提交一笔MEM事务。
        /// 自定义object输入仍须由PVC已存在的运行时强类型规则校验；
        /// 未知的其它签名不允许绕过。
        /// </summary>
        internal static Guid AddStudioVisionCapabilityToFlow(
            MEMEngineeringSession session,MEMContent content,
            CxVisionThresholdProvider provider,string capabilityId)
        {
            if(session==null)throw new ArgumentNullException("session");
            if(content==null)throw new ArgumentNullException("content");
            if(provider==null)throw new ArgumentNullException("provider");
            if(string.IsNullOrWhiteSpace(capabilityId))
                throw new ArgumentException("必须提供专业能力ID","capabilityId");
            var composition=CreateStudioVisionCapabilities(provider);
            string version;
            if(!composition.Resolver.TryGetCapabilityVersion(capabilityId,out version))
                throw new InvalidOperationException("当前Provider版本无法确认");
            var catalog=MOCCapabilityCatalogRuntimeFactory.Create(
                composition.Providers,composition.Resolver);
            var capability=catalog.Find(capabilityId);
            if(capability==null || !capability.RuntimeAvailable)
                throw new InvalidOperationException("MOC当前无法解析此软件能力");
            MRKResolvedCapability action;
            if(!composition.Resolver.TryResolve(capabilityId,out action)||action==null)
                throw new InvalidOperationException("当前Provider运行签名不可解析");
            var proof=new MOCBindingContractInspector().Assess(
                capability.Definition,action,new Dictionary<string,Type>{
                    {CxVisionAcceptedRegionMeasureProvider.InspectionTypeId,
                        typeof(CxVisionInspectionResult)},
                    {CxVisionBinaryThresholdProvider.MaskTypeId,
                        typeof(FIMImageBuffer)}
                });
            string[] codes=proof.ReasonCodes.ToArray();
            // 这两类专业数据通过现有PVC执行器动态检查，其他Unknown均拒绝。
            bool guardedObjectInput=proof.Verdict==MDCVerdict.Unknown &&
                codes.Length==1 &&
                ((capabilityId==CxVisionMaskRegionsProvider.CapabilityId &&
                  codes[0]=="MDC_BIND_OBJECT_UNVERIFIED:INPUT:ThresholdMask") ||
                 (capabilityId==CxVisionAcceptedRegionMeasureProvider.CapabilityId &&
                  codes[0]=="MDC_BIND_OBJECT_UNVERIFIED:INPUT:Inspection"));
            if(proof.Verdict!=MDCVerdict.Allowed && !guardedObjectInput)
                throw new InvalidOperationException(
                    "能力声明与执行端口不一致或待验证："+string.Join(", ",codes));
            CxVisionInspectionOptions options=provider.CaptureInspectionOptions();
            Guid created=Guid.Empty;
            MOCFlowBuilder.Edit(session,content,builder=>{
                created=builder.Step(capability.Definition);
                if(capabilityId==CxVisionBinaryThresholdProvider.CapabilityId)
                    builder.SetParameter(created,"Threshold",
                        options.GrayThreshold.ToString(CultureInfo.InvariantCulture));
                else if(capabilityId==CxVisionMaskRegionsProvider.CapabilityId)
                    builder.SetParameter(created,"RuleVersion",composition.Regions.ReleaseVersion);
            });
            return created;
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
            Guid loadedProjectId=Guid.Empty;
            string loadedScopeKey=null;
            Action invalidateEffective=()=>{
                effectiveLoaded=false;loadedProjectId=Guid.Empty;loadedScopeKey=null;
                selectedProvider=vision;
                ProjectStudioModuleCenterCapabilities(control,vision,
                    "当前仍为开发预设；请重新读取正确Scope的MHS Effective");
            };
            ProjectStudioModuleCenterCapabilities(control,vision,
                "CPU视觉四类Provider：开发预设参数；尚未载入MHS Effective");
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
            System.Windows.Automation.AutomationProperties.SetAutomationId(effectiveStatus,
                "Studio.ModuleCenter.EffectiveStatus");
            loadEffective.Click+=(sender,args)=>{
                try {
                    var loaded=LoadStudioEffectiveVisionProvider();
                    ProjectStudioModuleCenterCapabilities(control,loaded.Item1,
                        "MHS Effective "+loaded.Item2+"（真实配置快照，仅软件能力发现）");
                    selectedProvider=loaded.Item1;
                    effectiveLoaded=true;
                    loadedProjectId=_viewModel.ProjectSession.Project.Id;
                    loadedScopeKey=loaded.Item3+"/"+loaded.Item4;
                    effectiveStatus.Text="已从正式MHS读取 Effective "+loaded.Item2+
                        "；CPU Provider版本 "+loaded.Item1.ReleaseVersion+
                        "。未运行、未授权发布，也未创建MEM模块资源。";
                }
                catch(UnauthorizedAccessException) {
                    invalidateEffective();
                    effectiveStatus.Text="当前身份没有所选Scope的Effective配置读取权限。";
                }
                catch(FileNotFoundException) {
                    invalidateEffective();
                    effectiveStatus.Text="该Scope还没有持久化的Vision配置；请先打开工程专业配置、完成验证和保存，再读取Effective。";
                }
                catch(Exception ex) {
                    invalidateEffective();
                    effectiveStatus.Text="生效配置未加载："+ex.Message+
                        "；旧Effective已解除绑定，未执行任何算法或设备动作。";
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
                        !selected.Described||!selected.Resolved ||
                        (selected.Id!=CxVisionThresholdProvider.CapabilityId &&
                         selected.Id!=CxVisionBinaryThresholdProvider.CapabilityId &&
                         selected.Id!=CxVisionMaskRegionsProvider.CapabilityId &&
                         selected.Id!=CxVisionAcceptedRegionMeasureProvider.CapabilityId))
                        throw new InvalidOperationException("请先选中已注册的真实Vision Compute能力");
                    if(!effectiveLoaded)
                        throw new InvalidOperationException("请先读取当前MHS生效配置，再加入工程流程");
                    if(loadedProjectId!=_viewModel.ProjectSession.Project.Id||
                        loadedScopeKey!=CurrentStudioVisionScopeKey())
                        throw new InvalidOperationException("工程或配置Scope已切换，请重新读取Effective");
                    if(_logicRunning)throw new InvalidOperationException("运行中的主流程不可编辑");
                    // Re-check MHS version and permissions on the pinned Provider.
                    var page=_viewModel.ActivePage;
                    if(page==null)throw new InvalidOperationException("请先选择有效工程页面");
                    Guid inserted=AddStudioVisionCapabilityToFlow(
                        _viewModel.ProjectSession,page.RootContent,selectedProvider,selected.Id);
                    string version;
                    if(!selectedProvider.TryGetCapabilityVersion(
                        CxVisionThresholdProvider.CapabilityId,out version))
                        throw new InvalidOperationException("MHS配置版本已经变化");
                    RefreshScene();
                    OpenStudioDocument("doc.flow");
                    effectiveStatus.Text="已加入当前MEM主流程："+selected.Id+
                        "；Block="+inserted.ToString("N")+" / 生效配置 "+version+
                        "。专业参数已锁定，图像/Mask/Region仍需连线、MOC构建和MRK授权。";
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
            var medianRadius=new ComboBox {Width=66,Margin=new Thickness(7,0,3,0)};
            for(int radius=0;radius<=3;++radius)medianRadius.Items.Add(radius);
            medianRadius.SelectedItem=0;
            System.Windows.Automation.AutomationProperties.SetAutomationId(medianRadius,
                "Studio.ModuleCenter.MedianRadius");
            rawRow.Children.Add(new TextBlock {
                Text="拆分增强半径",VerticalAlignment=VerticalAlignment.Center});
            rawRow.Children.Add(medianRadius);
            var runSeparated=new Button {
                Content="执行拆分式四/五节点",Padding=new Thickness(10,5,10,5),
                Margin=new Thickness(4,0,0,0),
                ToolTip="MHS Effective → RAW → [FIM中值] → Mask → Region → Area；正式MEM/MOC/MRK软件工程，无设备授权"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(runSeparated,
                "Studio.ModuleCenter.RunSeparatedRaw");
            rawRow.Children.Add(runSeparated);
            System.Threading.CancellationTokenSource activeRun=null;
            var cancelRun=new Button {Content="取消检测",Padding=new Thickness(8,5,8,5),
                Margin=new Thickness(5,0,0,0),IsEnabled=false};
            System.Windows.Automation.AutomationProperties.SetAutomationId(cancelRun,
                "Studio.ModuleCenter.CancelRaw");
            cancelRun.Click+=(sender,args)=>{if(activeRun!=null)activeRun.Cancel();};
            rawRow.Children.Add(cancelRun);
            var rawStatus=new TextBlock {Text="等待用户选择RAW文件；位深来自已读取的MHS Effective。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,3,4,5)};
            System.Windows.Automation.AutomationProperties.SetAutomationId(rawStatus,
                "Studio.ModuleCenter.RawResultStatus");
            var regionResults=new DataGrid {IsReadOnly=true,AutoGenerateColumns=true,
                MaxHeight=168,Visibility=Visibility.Collapsed,Margin=new Thickness(3,2,3,4),
                CanUserAddRows=false,CanUserDeleteRows=false};
            var executionTrace=new DataGrid {IsReadOnly=true,AutoGenerateColumns=true,
                MaxHeight=160,Visibility=Visibility.Collapsed,Margin=new Thickness(3,2,3,4),
                CanUserAddRows=false,CanUserDeleteRows=false};
            System.Windows.Automation.AutomationProperties.SetAutomationId(executionTrace,
                "Studio.ModuleCenter.ExecutionTrace");
            System.Windows.Automation.AutomationProperties.SetAutomationId(regionResults,
                "Studio.ModuleCenter.ResultRegions");
            Func<bool,Task> runSelectedRaw=async separated=>{
                if(_logicRunning||activeRun!=null){rawStatus.Text="已有流程运行，禁止并行检测";return;}
                if(!effectiveLoaded){rawStatus.Text="请先读取MHS Effective配置";return;}
                if(loadedProjectId!=_viewModel.ProjectSession.Project.Id||
                    loadedScopeKey!=CurrentStudioVisionScopeKey())
                {rawStatus.Text="工程或配置Scope已变化，请重新读取MHS Effective";return;}
                int w,h;
                if(!Int32.TryParse(imageWidth.Text,out w)||
                    !Int32.TryParse(imageHeight.Text,out h)||w<=0||h<=0||
                    (long)w*h>32000000L)
                {rawStatus.Text="RAW图像宽高不合法";return;}
                var dialog=new Microsoft.Win32.OpenFileDialog {
                    Title="选择真实RAW灰度图像",Filter="RAW images (*.raw)|*.raw|全部文件 (*.*)|*.*",
                    CheckFileExists=true};
                if(dialog.ShowDialog(this)!=true)return;
                int selectedRadius=medianRadius.SelectedItem is int ?
                    (int)medianRadius.SelectedItem : 0;
                activeRun=new System.Threading.CancellationTokenSource();
                cancelRun.IsEnabled=true;runRaw.IsEnabled=false;
                runSeparated.IsEnabled=false;_logicRunning=true;
                try
                {
                    rawStatus.Text=separated ?
                        "真实RAW → 独立分割/Region/面积工程执行中…" :
                        "真实RAW数据检查 / MEM-MOC-MRK CPU执行中…";
                    var report=separated ?
                        await ExecuteStudioSeparatedCpuVisionFileAsync(
                            _viewModel.ProjectSession,selectedProvider,dialog.FileName,w,h,
                            selectedRadius,activeRun.Token) :
                        await ExecuteStudioCpuVisionFileAsync(
                            _viewModel.ProjectSession,selectedProvider,dialog.FileName,w,h,
                            activeRun.Token);
                    if(loadedProjectId!=_viewModel.ProjectSession.Project.Id ||
                       loadedScopeKey!=CurrentStudioVisionScopeKey())
                        throw new InvalidOperationException("检测期间工程或配置Scope改变，拒绝显示旧Run");
                    var page=_viewModel.ProjectSession.Find(report.PageId) as MEMPage;
                    if(page!=null)_viewModel.ActivePage=page;
                    RefreshScene();
                    // Keep the result pane visible; the existing "打开主流程编排"
                    // action navigates to the newly selected MEM page on request.
                    regionResults.ItemsSource=report.Evidence.Items
                        .Where(item=>item.Kind==CxVisionResultKind.Region)
                        .Select((item,index)=>new {Index=index+1,AreaPixels=item.Region.PixelArea,
                            MinX=item.Region.MinX,MinY=item.Region.MinY,
                            MaxX=item.Region.MaxX,MaxY=item.Region.MaxY}).ToArray();
                    regionResults.Visibility=report.Inspection.Regions.Count==0?
                        Visibility.Collapsed:Visibility.Visible;
                    executionTrace.ItemsSource=report.TraceRecords;
                    executionTrace.Visibility=Visibility.Visible;
                    rawStatus.Text=(separated?"独立四/五节点工程":"实际CPU检测")+
                        (report.Inspection.Passed?"通过":"发现缺陷")+
                        "；阈值像素="+report.Inspection.ThresholdPixelCount+
                        "，缺陷区域="+report.Inspection.Regions.Count+
                        "，接受区域面积="+report.AcceptedRegionPixelArea+" px²"+
                        "，配置="+report.ConfigVersion+
                        (separated?", Mask/Region/Measurement同Run；分割节点="+
                            report.SegmentId.ToString("N")+
                            (report.EnhanceId==Guid.Empty?"":", 增强节点="+
                                report.EnhanceId.ToString("N")):"")+
                        "，MRK FlowId="+report.FlowId.ToString("N")+
                        "，工程Revision="+report.EngineeringRevision+
                        "，Trace完成事件="+report.CompletedTraceEvents+
                        "，MOC指纹="+report.PlanFingerprint.Substring(0,12)+
                        "。MEM已生成独立检测页；RAW输入仅供本次运行，重开需重新绑定。";
                }
                catch(OperationCanceledException){rawStatus.Text="用户取消，未报告执行成功";}
                catch(Exception ex){rawStatus.Text="真实CPU检测失败："+ex.Message;}
                finally{
                    _logicRunning=false;runRaw.IsEnabled=true;
                    runSeparated.IsEnabled=true;cancelRun.IsEnabled=false;
                    activeRun.Dispose();activeRun=null;
                }
            };
            runRaw.Click+=async (sender,args)=>await runSelectedRaw(false);
            runSeparated.Click+=async (sender,args)=>await runSelectedRaw(true);
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
            header.Children.Add(regionResults);
            header.Children.Add(executionTrace);
            control.HeaderContent=header;
            return control;
        }

        /// <summary>
        /// Explicit Studio host read: the same config root and selected scope as
        /// doc.config. An authenticated host uses only the MHS authorized session;
        /// forbidden reads must not fall back to the raw service.
        /// </summary>
        private string CurrentStudioVisionScopeKey()
        {
            var choices=StudioConfigurationScopeChoices();
            var selected=choices.FirstOrDefault(x=>
                x.Kind==_studioConfigurationScopeKind&&
                x.Id==_studioConfigurationScopeId)??choices.FirstOrDefault();
            return selected==null?null:selected.Kind+"/"+selected.Id;
        }

        private Tuple<CxVisionThresholdProvider,string,string,string>
            LoadStudioEffectiveVisionProvider()
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
            return Tuple.Create(provider,scope.Label+" / "+version,scope.Kind,scope.Id);
        }

        private void ProjectStudioModuleCenterCapabilities(MPWModuleCenterControl control,
            CxVisionThresholdProvider vision,string description)
        {
            // One MOC catalog, no parallel registration or execution dispatcher.
            var composition=CreateStudioVisionCapabilities(vision);
            var catalog=MOCCapabilityCatalogRuntimeFactory.Create(
                composition.Providers.Concat(new IMOCCapabilityProvider[]{_simulation}),
                composition.Resolver);
            var module=new MFMModuleDescriptor("studio.development.cpu.vision",
                "CPU视觉检测及独立分割测量（未绑定MEM模块资源）",MFMModuleKind.Compute,
                capabilityIds:new[]{
                    CxVisionThresholdProvider.CapabilityId,
                    CxVisionBinaryThresholdProvider.CapabilityId,
                    CxVisionMaskRegionsProvider.CapabilityId,
                    CxVisionAcceptedRegionMeasureProvider.CapabilityId});
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
