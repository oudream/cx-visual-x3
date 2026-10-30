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
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Presentation.Wpf.Imaging;
using CxVisual.Mechanism.Visual;
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
            public FIMImageBuffer SourceImage;
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
                SourceImage=report.SourceImage,
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
        /// <summary>
        /// 显式重绑已保存ROI的RAW原图。只借用FIM读入器和PVC指纹验证，
        /// 不创建工程页、Run、结果或MHS配置；原图文件步长由用户明确指定。
        /// </summary>
        internal static FIMImageBuffer LoadStudioSavedRoiRaw(
            MEMEngineeringSession session,MRIObjectAddress address,
            string path,int stride,CancellationToken cancellation)
        {
            if(session==null)throw new ArgumentNullException("session");
            cancellation.ThrowIfCancellationRequested();
            int expectedRevision=session.Revision;
            CxVisionRoiRecipe recipe=CxVisionRoiEngineering.Open(session,address);
            FIMImageBuffer source=FIMRawImageReader.LoadFile(path,recipe.Width,
                recipe.Height,recipe.PixelFormat,stride,false);
            CxVisionRoiEngineering.VerifySource(recipe,source,cancellation);
            if(session.Revision!=expectedRevision)
                throw new InvalidOperationException("vision.roi.definition_changed_during_bind");
            return source;
        }

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

        /// <summary>
        /// Studio与自测共用同一MPW真实图窗和PVC只读Run产物。
        /// 不通过显示文件二次读取RAW：原图必须来自同一次MRK执行字节快照。
        /// </summary>
        /// <summary>
        /// Studio一次有效图像绑定由对象身份和MVI原图代次共同确定。
        /// 外部SDK可对相同Buffer实例再次SetOriginal；旧Run不能仅因引用相等而成为当前结果。
        /// </summary>
        internal static bool IsStudioVisionSourceCurrent(
            MPWImageViewerControl viewer,FIMImageBuffer source,long generation)
        {
            if(viewer==null)throw new ArgumentNullException("viewer");
            return source!=null &&
                Object.ReferenceEquals(source,viewer.Session.Original) &&
                viewer.Session.OriginalGeneration==generation;
        }

        internal static void PopulateStudioVisionResults(
            StudioCpuVisionReport report,MPWImageViewerControl viewer,ListBox list)
        {
            if(report==null)throw new ArgumentNullException("report");
            if(viewer==null)throw new ArgumentNullException("viewer");
            if(list==null)throw new ArgumentNullException("list");
            if(report.Evidence==null || report.FlowId!=report.Evidence.RunId)
                throw new InvalidOperationException("studio.vision.run_identity_mismatch");
            viewer.Clear();
            list.Items.Clear();
            if(report.SourceImage!=null)
            {
                if(report.Evidence.Width!=report.SourceImage.Width ||
                   report.Evidence.Height!=report.SourceImage.Height)
                    throw new InvalidOperationException("studio.vision.source_dimensions_changed");
                viewer.ShowOriginal(report.SourceImage);
            }
            foreach(CxVisionResultItem item in report.Evidence.Items)
            {
                string value=item.Contour!=null ?
                    item.Contour.SignedAreaPixels.ToString("0.###",CultureInfo.InvariantCulture)+
                        " px²"+(item.Contour.IsHole?" (孔洞)":" (外环)") :
                    item.Region!=null ? item.Region.PixelArea.ToString(
                        CultureInfo.InvariantCulture)+" px²" :
                    item.IntegerValue.HasValue ? item.IntegerValue.Value.ToString(
                        CultureInfo.InvariantCulture)+(item.Unit==null?"":" "+item.Unit) :
                    item.BooleanValue.HasValue ? item.BooleanValue.Value.ToString() :
                    item.Image!=null ?
                        item.Image.Width.ToString(CultureInfo.InvariantCulture)+"×"+
                        item.Image.Height.ToString(CultureInfo.InvariantCulture)+" "+
                        item.Image.PixelFormat : "";
                list.Items.Add(new ListBoxItem {
                    Tag=item,Content=item.Kind+" · "+item.Name+" · "+value
                });
            }
        }

        /// <summary>
        /// 单个ResultItem的显示操作始终验证Run、Source及图窗原图会话，
        /// 临时轮廓借用唯一MVI Store，不把Contour误作为工程ROI持久化。
        /// </summary>
        internal static string SelectStudioVisionResult(
            CxVisionRunSummary run,FIMImageBuffer source,
            MPWImageViewerControl viewer,CxVisionResultItem item)
        {
            if(run==null||viewer==null||item==null ||
               run.RunId!=item.RunId || run.SourceId!=item.SourceId ||
               !run.Items.Any(x=>Object.ReferenceEquals(x,item)))
                throw new InvalidOperationException("studio.vision.stale_result");
            bool requiresImage=item.Kind==CxVisionResultKind.Image ||
                item.Kind==CxVisionResultKind.Mask ||
                item.Kind==CxVisionResultKind.Contour;
            if(requiresImage && (source==null ||
               !Object.ReferenceEquals(source,viewer.Session.Original)))
                throw new InvalidOperationException("studio.vision.stale_source");
            if(item.Kind==CxVisionResultKind.Image || item.Kind==CxVisionResultKind.Mask)
            {
                if(item.Image==null || item.Image.Width!=source.Width ||
                   item.Image.Height!=source.Height)
                    throw new InvalidOperationException("studio.vision.invalid_image");
                viewer.ShowResult(item.Image);
                return item.Kind+"；Item="+item.ItemId+"；Run="+run.RunId.ToString("N");
            }
            if(item.Kind==CxVisionResultKind.Contour)
            {
                CxVisionContourSnapshot contour=item.Contour;
                if(contour==null || contour.Points.Count<3 ||
                   contour.Points.Count>100000)
                    throw new InvalidOperationException("studio.vision.contour_limit");
                MVIImageOverlayItem overlay=viewer.Overlays.Items.FirstOrDefault(x=>
                    x.ExternalId==item.ItemId &&
                    x.Kind==MVIImageOverlayKind.PolygonAnnotation && x.IsLocked);
                viewer.SelectOriginal();
                if(overlay==null)
                {
                    var points=contour.Points;
                    double x0=points.Min(p=>p.X),x1=points.Max(p=>p.X);
                    double y0=points.Min(p=>p.Y),y1=points.Max(p=>p.Y);
                    overlay=viewer.AddOverlay(new MVIImageOverlayItem(
                        MVIImageOverlayKind.PolygonAnnotation,
                        MVIImageOverlayLayer.Annotation,
                        new FGERect2D(x0,y0,x1-x0,y1-y0),
                        points,(contour.IsHole?"Run孔洞":"Run外环")+" · "+item.ItemId,
                        item.ItemId,
                        unchecked((int)(contour.IsHole?0xFF00D4C7u:0xFFFFD157u)),
                        1.5,isLocked:true));
                }
                viewer.SelectOverlay(overlay.Id);
                return (contour.IsHole?"孔洞":"外轮廓")+" · "+
                    contour.SignedAreaPixels.ToString("0.###",CultureInfo.InvariantCulture)+
                    " px²；Run="+run.RunId.ToString("N");
            }
            if(source!=null && Object.ReferenceEquals(source,viewer.Session.Original))
                viewer.SelectOriginal();
            return item.Kind+" · "+item.Name+"；Item="+item.ItemId+
                (item.IntegerValue.HasValue?"；值="+
                    item.IntegerValue.Value.ToString(CultureInfo.InvariantCulture)+
                    (item.Unit==null?"":" "+item.Unit):"");
        }

        /// <summary>
        /// 只创建一次真实MEM Page/Content：内部三段已连通，外部RAW数据由
        /// 既有MOL FlowInput端口绑定；用户仍可使用Studio原FlowCanvas更改拓扑。
        /// 不自动创建Raw Provider或隐式绑定设备，不是新的Graph状态。
        /// </summary>
        internal sealed class StudioVisionFlowTemplate
        {
            public Guid PageId {get;internal set;}
            public Guid MaskBlockId {get;internal set;}
            public Guid RegionBlockId {get;internal set;}
            public Guid MeasureBlockId {get;internal set;}
            public long ConfigurationRevision {get;internal set;}
        }

        internal static StudioVisionFlowTemplate CreateStudioSeparatedVisionFlow(
            MEMEngineeringSession session,CxVisionThresholdProvider provider)
        {
            if(session==null)throw new ArgumentNullException("session");
            if(provider==null)throw new ArgumentNullException("provider");
            StudioVisionCapabilitySet set=CreateStudioVisionCapabilities(provider);
            CxVisionInspectionOptions options=provider.CaptureInspectionOptions();
            // 在触及工程之前核对所有真实专业能力及已生效版本。
            foreach(string id in new[]{
                CxVisionBinaryThresholdProvider.CapabilityId,
                CxVisionMaskRegionsProvider.CapabilityId,
                CxVisionAcceptedRegionMeasureProvider.CapabilityId})
            {
                MRKResolvedCapability action;
                string pinnedVersion;
                if(!set.Resolver.TryResolve(id,out action)||action==null ||
                   !set.Resolver.TryGetCapabilityVersion(id,out pinnedVersion) ||
                   string.IsNullOrWhiteSpace(pinnedVersion))
                    throw new InvalidOperationException("vision.template.capability_unavailable:"+id);
            }
            // 校验MHS版本/权限在原事务创建前仍有效。
            provider.CaptureInspectionOptions();
            var page=session.AddPage("二维视觉 · Mask→Region→Measurement 可编辑流程");
            Guid mask=Guid.Empty,regions=Guid.Empty,measure=Guid.Empty;
            MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                mask=b.Step(set.Threshold.DescribeCapabilities().Single());
                b.SetParameter(mask,"Threshold",
                    options.GrayThreshold.ToString(CultureInfo.InvariantCulture));
                regions=b.Step(set.Regions.DescribeCapabilities().Single());
                b.SetParameter(regions,"RuleVersion",set.Regions.ReleaseVersion);
                measure=b.Step(set.Measurement.DescribeCapabilities().Single());
                b.Connect(mask,"Done",regions,"Start",MPCPortKind.Control);
                b.Connect(mask,"ThresholdMask",regions,"ThresholdMask",MPCPortKind.Data);
                b.Connect(regions,"Done",measure,"Start",MPCPortKind.Control);
                b.Connect(regions,"Inspection",measure,"Inspection",MPCPortKind.Data);
                // 此子流程不持有RAW设备或文件路径，外部调用者显式输入三值。
                b.ExposeInput("RawPixels",mask,"RawPixels");
                b.ExposeInput("Width",mask,"Width");
                b.ExposeInput("Height",mask,"Height");
                b.ExposeOutput("ThresholdMask",mask,"ThresholdMask");
                b.ExposeOutput("Inspection",regions,"Inspection");
                b.ExposeOutput("AcceptedRegionPixelArea",measure,"Area");
            });
            var saved=session.GetSnapshot().Find(page.RootContent.Id);
            var wiring=saved==null?null:MOLWiringCodec.Get(saved);
            if(wiring==null)throw new InvalidOperationException("vision.template.not_persisted");
            wiring.ValidateExecutable();
            return new StudioVisionFlowTemplate {
                PageId=page.Id,MaskBlockId=mask,
                RegionBlockId=regions,MeasureBlockId=measure,
                ConfigurationRevision=provider.ConfigurationRevision
            };
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
            Action invalidatePreview=()=>{};
            Action invalidateEffective=()=>{
                invalidatePreview();
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
                    invalidatePreview();
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
                    // AddStudioVisionCapabilityToFlow已在正式MEM编辑之前核验生效版本。
                    // 提交后只显示本次固定快照身份，不再抛出“拒绝”误报已提交的事务。
                    string version=selectedProvider.ReleaseVersion;
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
            var newVisionFlow=new Button {
                Content="新建已连线视觉流程",Padding=new Thickness(11,5,11,5),
                Margin=new Thickness(3,1,6,1),
                ToolTip="使用当前MHS Effective创建独立Mask→Region→Measurement工程页，公开RAW、尺寸与结果端口，之后可在原MOL画布自由编辑"
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(newVisionFlow,
                "Studio.ModuleCenter.CreateVisionFlow");
            newVisionFlow.Click+=(sender,args)=>{
                try
                {
                    if(!effectiveLoaded || selectedProvider==null)
                        throw new InvalidOperationException("请先读取MHS Effective配置");
                    if(_logicRunning)
                        throw new InvalidOperationException("运行中的工程不能插入视觉流程");
                    if(loadedProjectId!=_viewModel.ProjectSession.Project.Id ||
                       loadedScopeKey!=CurrentStudioVisionScopeKey())
                        throw new InvalidOperationException("工程或配置Scope已变化");
                    StudioVisionFlowTemplate created=CreateStudioSeparatedVisionFlow(
                        _viewModel.ProjectSession,selectedProvider);
                    var page=_viewModel.ProjectSession.Find(created.PageId) as MEMPage;
                    if(page==null)throw new InvalidOperationException("新建工程页丢失");
                    _viewModel.ActivePage=page;
                    RefreshScene();
                    OpenStudioDocument("doc.flow");
                    effectiveStatus.Text="已创建可编辑三节点Vision流程页；Block="+
                        created.MaskBlockId.ToString("N").Substring(0,8)+"/"+
                        created.RegionBlockId.ToString("N").Substring(0,8)+"/"+
                        created.MeasureBlockId.ToString("N").Substring(0,8)+
                        "。RAW/宽/高为公开输入，结果已公开；打开MOL即可拖线/编辑/保存。";
                }
                catch(UnauthorizedAccessException)
                {
                    effectiveStatus.Text="MHS配置授权已撤销，禁止建立Vision流程。";
                }
                catch(Exception ex)
                {
                    effectiveStatus.Text="视觉流程创建失败："+ex.Message;
                }
            };
            actions.Children.Add(newVisionFlow);
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
            var bindRoiButton=new Button {
                Content="重绑工程ROI原图",Padding=new Thickness(10,5,10,5),
                Margin=new Thickness(4,0,0,0),
                ToolTip="选当前MEM页保存的ROI配方；从本地RAW文件读取原始位深与尺寸并验证SHA-256，不创建新Run或工程页"
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(bindRoiButton,
                "Studio.ModuleCenter.BindSavedRoiRaw");
            rawRow.Children.Add(bindRoiButton);
            rawRow.Children.Add(new TextBlock {
                Text="行步长(0=紧排)",VerticalAlignment=VerticalAlignment.Center,
                Margin=new Thickness(7,0,0,0)
            });
            var bindRoiStride=new TextBox {Text="0",Width=60,Margin=new Thickness(4,0,4,0),
                ToolTip="单行实际RAW字节数；0表示根据已存像素格式计算紧排行步长"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(bindRoiStride,
                "Studio.ModuleCenter.BindSavedRoiStride");
            rawRow.Children.Add(bindRoiStride);
            System.Threading.CancellationTokenSource activeRun=null;
            System.Threading.CancellationTokenSource savedRoiRun=null;
            System.Threading.CancellationTokenSource bindingOperation=null;
            var cancelRun=new Button {Content="取消检测",Padding=new Thickness(8,5,8,5),
                Margin=new Thickness(5,0,0,0),IsEnabled=false};
            System.Windows.Automation.AutomationProperties.SetAutomationId(cancelRun,
                "Studio.ModuleCenter.CancelRaw");
            cancelRun.Click+=(sender,args)=>{
                if(activeRun!=null)activeRun.Cancel();
                if(savedRoiRun!=null)savedRoiRun.Cancel();
                if(bindingOperation!=null)bindingOperation.Cancel();
            };
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
            // 已有MPW Viewer/MVI Store仅作为当前MRK Run的临时可视投影，
            // 不持久化第二套资源，不二次读取原RAW文件。
            var resultPreview=new MPWImageViewerControl {
                Height=240,Visibility=Visibility.Collapsed,
                Margin=new Thickness(4,2,4,5)
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(resultPreview,
                "Studio.ModuleCenter.ResultViewer");
            var resultList=new ListBox {
                Height=120,Visibility=Visibility.Collapsed,
                Margin=new Thickness(4,3,4,5)
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(resultList,
                "Studio.ModuleCenter.ResultItems");
            var sourceButton=new Button {
                Content="显示本Run原图",Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(3),IsEnabled=false
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(sourceButton,
                "Studio.ModuleCenter.ShowRunOriginal");
            var contourButton=new Button {
                Content="按需生成Contour",Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(3),IsEnabled=false
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(contourButton,
                "Studio.ModuleCenter.GenerateRunContours");
            var runExportButton=new Button {
                Content="导出Run CSV",Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(3),IsEnabled=false
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(runExportButton,
                "Studio.ModuleCenter.ExportRunCsv");
            var rawExportButton=new Button {
                Content="导出Run图像RAW",Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(3),IsEnabled=false,
                ToolTip="选择Image/Mask结果后导出该产物；未选择时导出本Run原图。RAW保留原位深及Stride。"
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(rawExportButton,
                "Studio.ModuleCenter.ExportRunRaw");
            var saveRoiButton=new Button {
                Content="保存选中ROI到MEM",Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(3),IsEnabled=false,
                ToolTip="把当前图窗选中的面积ROI通过MEM Data/Visual同一事务保存；请再使用Studio保存工程"
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(saveRoiButton,
                "Studio.ModuleCenter.CommitSelectedRoi");
            var runSavedRoiButton=new Button {
                Content="运行工程ROI",Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(3),IsEnabled=false,
                ToolTip="验证工程中已保存的ROI、原图SHA-256及算法参数后重新执行MRK"
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(runSavedRoiButton,
                "Studio.ModuleCenter.RunSavedRoi");
            var resultDetail=new TextBlock {
                Text="本区域仅显示已完成Run的只读产物；不代表生产检测授权。",
                TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,2,4,4)
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(resultDetail,
                "Studio.ModuleCenter.ResultDetails");
            var resultControls=new WrapPanel();
            resultControls.Children.Add(sourceButton);
            resultControls.Children.Add(contourButton);
            resultControls.Children.Add(runExportButton);
            resultControls.Children.Add(rawExportButton);
            resultControls.Children.Add(saveRoiButton);
            resultControls.Children.Add(runSavedRoiButton);
            // Studio仅切换已有MPW/MVI工具，不复制图元数据与MEM工程事务。
            var roiTools=new WrapPanel {Margin=new Thickness(3,2,3,4)};
            var rectRoiTool=new Button {Content="矩形ROI",Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(3),ToolTip="在原图拖动绘制，松开后自动选中"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(rectRoiTool,
                "Studio.ModuleCenter.DrawRectangleRoi");
            var polygonRoiTool=new Button {Content="多边形ROI",Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(3),ToolTip="依次点击顶点，双击结束"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(polygonRoiTool,
                "Studio.ModuleCenter.DrawPolygonRoi");
            var moveRoiTool=new Button {Content="选择/移动ROI",Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(3),ToolTip="选择或拖动已有ROI"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(moveRoiTool,
                "Studio.ModuleCenter.SelectRoi");
            var navigateRoiTool=new Button {Content="导航",Padding=new Thickness(8,4,8,4),
                Margin=new Thickness(3),ToolTip="退出绘制并返回图窗平移"};
            System.Windows.Automation.AutomationProperties.SetAutomationId(navigateRoiTool,
                "Studio.ModuleCenter.NavigateRoi");
            roiTools.Children.Add(rectRoiTool);
            roiTools.Children.Add(polygonRoiTool);
            roiTools.Children.Add(moveRoiTool);
            roiTools.Children.Add(navigateRoiTool);
            var resultStack=new StackPanel();
            resultStack.Children.Add(roiTools);
            resultStack.Children.Add(resultControls);
            resultStack.Children.Add(resultList);
            resultStack.Children.Add(resultPreview);
            resultStack.Children.Add(resultDetail);
            var resultExpander=new Expander {
                Header="本Run视觉结果 · Image / Mask / Contour / Measurement",
                IsExpanded=false,Content=resultStack,
                Margin=new Thickness(3,3,3,4),Visibility=Visibility.Collapsed
            };
            System.Windows.Automation.AutomationProperties.SetAutomationId(resultExpander,
                "Studio.ModuleCenter.ResultPanel");
            CxVisionRunSummary displayedRun=null;
            FIMImageBuffer displayedOriginal=null;
            long displayedOriginalGeneration=-1;
            Guid displayedProject=Guid.Empty;
            Guid displayedPage=Guid.Empty;
            int displayedRevision=-1;
            CancellationTokenSource contourOperation=null;
            long previewEpoch=0;
            bool bindingSavedRoi=false;
            invalidatePreview=()=>{
                ++previewEpoch;
                if(contourOperation!=null)contourOperation.Cancel();
                displayedRun=null;displayedOriginal=null;
                displayedOriginalGeneration=-1;
                displayedProject=Guid.Empty;
                displayedPage=Guid.Empty;displayedRevision=-1;
                contourButton.IsEnabled=false;sourceButton.IsEnabled=false;
                runExportButton.IsEnabled=false;rawExportButton.IsEnabled=false;
                saveRoiButton.IsEnabled=false;runSavedRoiButton.IsEnabled=false;
                resultList.Items.Clear();resultList.Visibility=Visibility.Collapsed;
                resultPreview.Clear();
                resultPreview.ToolMode=MVIImageToolMode.Navigate;
                resultPreview.Visibility=Visibility.Collapsed;
                resultExpander.Visibility=Visibility.Collapsed;
                resultDetail.Text="旧Run已经失效；不会保留之前的图像和轮廓投影。";
            };
            Func<bool> hasCurrentSource=()=>displayedOriginal!=null &&
                displayedProject==_viewModel.ProjectSession.Project.Id &&
                _viewModel.ActivePage!=null &&
                displayedPage==_viewModel.ActivePage.Id &&
                IsStudioVisionSourceCurrent(resultPreview,displayedOriginal,
                    displayedOriginalGeneration);
            Func<bool> hasCurrentRun=()=>displayedRun!=null &&
                displayedProject==_viewModel.ProjectSession.Project.Id &&
                _viewModel.ActivePage!=null &&
                displayedPage==_viewModel.ActivePage.Id &&
                displayedRevision==_viewModel.ProjectSession.Revision &&
                (displayedOriginal==null ||
                 IsStudioVisionSourceCurrent(resultPreview,displayedOriginal,
                     displayedOriginalGeneration));
            Action<MVIImageToolMode,string> selectRoiTool=(mode,instruction)=>{
                if(!hasCurrentSource())
                {
                    resultDetail.Text="当前没有同源有效原图，不能进入ROI绘制工具。";
                    return;
                }
                resultPreview.SelectOriginal();
                resultPreview.ToolMode=mode;
                resultDetail.Text=instruction;
            };
            rectRoiTool.Click+=(sender,args)=>selectRoiTool(
                MVIImageToolMode.RectangleRoi,
                "矩形ROI：原图拖动完成即选中；点击“保存选中ROI到MEM”提交工程。");
            polygonRoiTool.Click+=(sender,args)=>selectRoiTool(
                MVIImageToolMode.PolygonRoi,
                "多边形ROI：点击顶点、双击完成并选中；Esc取消草稿。");
            moveRoiTool.Click+=(sender,args)=>selectRoiTool(
                MVIImageToolMode.MoveOverlay,"单击选择或拖动已有ROI；编辑工程需显式提交。");
            navigateRoiTool.Click+=(sender,args)=>selectRoiTool(
                MVIImageToolMode.Navigate,"已退出ROI工具，恢复原图平移。");
            // 正式工程重开后不需要先运行一条新的CPU流程；
            // 只绑定用户指定的RAW原像素，原工程Revision和Run证据保持不变。
            bindRoiButton.Click+=async (sender,args)=>{
                if(bindingSavedRoi || _logicRunning || savedRoiRun!=null || activeRun!=null)
                {
                    rawStatus.Text="已有图像绑定或流程运行，请先完成/取消。";
                    return;
                }
                var session=_viewModel.ProjectSession;
                var page=_viewModel.ActivePage;
                if(session==null||page==null)
                {rawStatus.Text="请先打开保存有二维ROI的正式工程页。";return;}
                var address=session.GetAddress(page.RootContent);
                CxVisionRoiRecipe boundRecipe;
                try{boundRecipe=CxVisionRoiEngineering.Open(session,address);}
                catch(Exception ex){rawStatus.Text="当前页没有可恢复的正式ROI："+ex.Message;return;}
                int stride;
                if(!Int32.TryParse(bindRoiStride.Text,out stride)||stride<0)
                {rawStatus.Text="RAW行步长必须是非负整数。";return;}
                var dialog=new Microsoft.Win32.OpenFileDialog {
                    Title="重新绑定工程ROI原图（必须与保存的像素SHA-256一致）",
                    Filter="RAW images (*.raw)|*.raw|全部文件 (*.*)|*.*",
                    CheckFileExists=true
                };
                if(dialog.ShowDialog(this)!=true)return;
                Guid projectId=session.Project.Id;
                int revision=session.Revision;
                long epoch=previewEpoch;
                bindingSavedRoi=true;bindRoiButton.IsEnabled=false;
                runSavedRoiButton.IsEnabled=false;
                var bindWork=new CancellationTokenSource();
                bindingOperation=bindWork;cancelRun.IsEnabled=true;
                try
                {
                    rawStatus.Text="正在读取RAW并核验MEM保存的原图指纹…";
                    FIMImageBuffer restored=await Task.Run(()=>
                        LoadStudioSavedRoiRaw(session,address,dialog.FileName,
                            stride,bindWork.Token),bindWork.Token);
                    bindWork.Token.ThrowIfCancellationRequested();
                    if(!Object.ReferenceEquals(_viewModel.ProjectSession,session)||
                       !Object.ReferenceEquals(_viewModel.ActivePage,page)||
                       session.Project.Id!=projectId || session.Revision!=revision ||
                       epoch!=previewEpoch)
                    {
                        rawStatus.Text="工程、页面或显示会话已变化，拒绝发布旧原图。";
                        return;
                    }
                    invalidatePreview();
                    resultPreview.ShowOriginal(restored);
                    resultPreview.ToolMode=MVIImageToolMode.Navigate;
                    // 工程Visual仅投影到当前MVI图窗，重绑无需先执行MRK。
                    resultPreview.AddOverlay(boundRecipe.Roi);
                    resultPreview.SelectOverlay(boundRecipe.Roi.Id);
                    displayedOriginal=restored;
                    displayedOriginalGeneration=resultPreview.Session.OriginalGeneration;
                    displayedProject=projectId;
                    displayedPage=page.Id;
                    resultPreview.Visibility=Visibility.Visible;
                    resultExpander.Visibility=Visibility.Visible;
                    resultExpander.IsExpanded=true;
                    runSavedRoiButton.IsEnabled=true;
                    saveRoiButton.IsEnabled=true;
                    resultDetail.Text="已重绑并验证正式ROI原图及工程ROI几何（尚未生成新Run）。"
                        +"可用ROI工具编辑后显式保存到MEM，或直接运行工程ROI。";
                    rawStatus.Text="原图校验通过；未修改工程Revision或建立新检测页。";
                }
                catch(OperationCanceledException)
                {
                    rawStatus.Text="ROI原图重绑已取消，未发布新图像。";
                }
                catch(Exception ex)
                {
                    rawStatus.Text="ROI原图重绑失败（旧图与结果保持原状）："+ex.Message;
                }
                finally
                {
                    if(Object.ReferenceEquals(bindingOperation,bindWork))
                        bindingOperation=null;
                    bindWork.Dispose();
                    bindingSavedRoi=false;bindRoiButton.IsEnabled=true;
                    cancelRun.IsEnabled=activeRun!=null||savedRoiRun!=null;
                    runSavedRoiButton.IsEnabled=hasCurrentSource();
                }
            };
            // 只有用户明确确认才从MVI临时ROI提升为MEM正式定义。
            // 不在Viewer OverlayStore创建新的工程历史；MEM自身负责Undo/Redo。
            saveRoiButton.Click+=(sender,args)=>{
                if(!hasCurrentSource()||bindingSavedRoi||savedRoiRun!=null||
                   activeRun!=null||_logicRunning)
                {
                    resultDetail.Text="没有同源原图或仍有操作执行中，不能保存ROI。";
                    return;
                }
                if(resultPreview.Session.ActiveSlot!=MVIImageSlot.Original)
                {
                    resultDetail.Text="Mask/Result是本Run只读证据；请先显示原图再保存ROI。";
                    return;
                }
                try
                {
                    Guid? selectedId=resultPreview.SelectedOverlayId;
                    MVIImageOverlayItem roi=selectedId.HasValue?
                        resultPreview.Overlays.Find(selectedId.Value):null;
                    if(roi==null||!roi.IsAreaRoi)
                        throw new InvalidOperationException("请先在原图中创建并选择面积ROI");
                    var session=_viewModel.ProjectSession;
                    var page=_viewModel.ActivePage;
                    if(page==null)throw new InvalidOperationException("没有有效工程页面");
                    var address=session.GetAddress(page.RootContent);
                    // 已存配方只更新工程几何：保留原SourceId、配置快照与Revision，
                    // 无需重新读取MHS；但必须再次核对已绑定RAW像素SHA-256。
                    CxVisionRoiRecipe previous=null;
                    if(page.RootContent.Aspects.Any(x=>
                        x.ProviderId==CxVisionRoiEngineering.ProviderId))
                    {
                        previous=CxVisionRoiEngineering.Open(session,address);
                        CxVisionRoiEngineering.VerifySource(
                            previous,displayedOriginal,CancellationToken.None);
                    }
                    else if(displayedRun==null || !effectiveLoaded ||
                        selectedProvider==null ||
                        loadedProjectId!=session.Project.Id ||
                        loadedScopeKey!=CurrentStudioVisionScopeKey())
                        throw new InvalidOperationException(
                            "新建工程ROI需要当前同源Run及已授权MHS Effective");
                    CxVisionInspectionOptions options=previous==null?
                        selectedProvider.CaptureInspectionOptions():previous.Options;
                    string sourceId=previous==null?
                        displayedRun.SourceId:previous.SourceId;
                    long configRevision=previous==null?
                        selectedProvider.ConfigurationRevision:previous.ConfigurationRevision;
                    CxVisionRoiEngineering.Commit(session,page.RootContent,
                        session.Revision,displayedOriginal,sourceId,roi,
                        options,configRevision,CancellationToken.None);
                    // MEM新Revision使旧Run全部失效：不仅屏蔽操作按钮，也须
                    // 清理MVI Result像素和本Run投影的只读轮廓，不能留下可误认
                    // 为当前结果的视觉证据。保留用户面积ROI/其他Annotation。
                    CxVisionRunSummary invalidatedRun=displayedRun;
                    if(invalidatedRun!=null)
                    {
                        var contourIds=new HashSet<string>(
                            invalidatedRun.Items.Where(item=>
                                item.Kind==CxVisionResultKind.Contour)
                                .Select(item=>item.ItemId),StringComparer.Ordinal);
                        foreach(MVIImageOverlayItem projected in
                            resultPreview.Overlays.Items.Where(item=>
                                item.Kind==MVIImageOverlayKind.PolygonAnnotation &&
                                item.IsLocked && contourIds.Contains(item.ExternalId))
                                .ToArray())
                            resultPreview.Overlays.Remove(projected.Id);
                    }
                    resultPreview.Session.ClearResult();
                    displayedRun=null;displayedRevision=-1;
                    resultList.Items.Clear();resultList.Visibility=Visibility.Collapsed;
                    sourceButton.IsEnabled=false;contourButton.IsEnabled=false;
                    runExportButton.IsEnabled=false;rawExportButton.IsEnabled=false;
                    resultPreview.SelectOriginal();
                    resultPreview.ToolMode=MVIImageToolMode.Navigate;
                    resultDetail.Text="已提交正式MEM Data/Visual ROI="+roi.Id+
                        "；Revision="+session.Revision+
                        "。旧Run已失效，可Undo/Redo、保存工程或重新运行。";
                    runSavedRoiButton.IsEnabled=true;
                }
                catch(Exception ex){resultDetail.Text="正式ROI提交失败："+ex.Message;}
            };
            runSavedRoiButton.Click+=async (sender,args)=>{
                if(bindingSavedRoi||savedRoiRun!=null||activeRun!=null)return;
                if(!hasCurrentSource()) {resultDetail.Text="原图来源失效，无法恢复ROI。";return;}
                var project=_viewModel.ProjectSession;
                var page=_viewModel.ActivePage;
                if(page==null){resultDetail.Text="请先选择保存ROI的工程页。";return;}
                var address=project.GetAddress(page.RootContent);
                var original=displayedOriginal;
                Guid projectId=project.Project.Id;
                long originalGeneration=resultPreview.Session.OriginalGeneration;
                int expectedRevision=project.Revision;
                CxVisionRoiRecipe recipe;
                try {recipe=CxVisionRoiEngineering.Open(project,address);}
                catch(Exception ex){resultDetail.Text="无法读取工程ROI："+ex.Message;return;}
                runSavedRoiButton.IsEnabled=false;
                var work=new CancellationTokenSource();
                savedRoiRun=work;cancelRun.IsEnabled=true;
                try
                {
                    resultDetail.Text="正在验证ROI原图SHA-256并按工程定义重新运行MRK…";
                    var completed=await CxVisionRoiEngineering.RunSavedAsync(
                        project,address,original,work.Token);
                    if(project.Project.Id!=projectId ||
                       !Object.ReferenceEquals(_viewModel.ProjectSession,project) ||
                       !Object.ReferenceEquals(_viewModel.ActivePage,page) ||
                       project.Revision!=expectedRevision ||
                       !Object.ReferenceEquals(displayedOriginal,original) ||
                       originalGeneration!=resultPreview.Session.OriginalGeneration)
                    {
                        resultDetail.Text="工程或图像在运行中变化，拒绝发布旧ROI结果。";
                        return;
                    }
                    var report=new StudioCpuVisionReport {
                        FlowId=completed.RunId,Evidence=completed,SourceImage=original
                    };
                    PopulateStudioVisionResults(report,resultPreview,resultList);
                    resultPreview.ToolMode=MVIImageToolMode.Navigate;
                    resultExpander.Visibility=Visibility.Visible;
                    resultExpander.IsExpanded=true;
                    resultList.Visibility=Visibility.Visible;
                    resultPreview.Visibility=Visibility.Visible;
                    sourceButton.IsEnabled=true;
                    runExportButton.IsEnabled=true;
                    rawExportButton.IsEnabled=true;
                    contourButton.IsEnabled=completed.Items.Any(x=>
                        x.Kind==CxVisionResultKind.Mask &&
                        x.OutputPort=="ThresholdMask");
                    // 工程几何只投影到当前MVI图窗，绝不从临时Overlay反写MEM。
                    resultPreview.AddOverlay(recipe.Roi);
                    displayedRun=completed;
                    displayedOriginal=original;
                    displayedOriginalGeneration=resultPreview.Session.OriginalGeneration;
                    displayedProject=projectId;
                    displayedPage=page.Id;
                    displayedRevision=expectedRevision;
                    resultDetail.Text="工程ROI复算完成：Run="+completed.RunId.ToString("N")+
                        "，ROI="+recipe.RoiId+"；结果已连接原MPW图窗。";
                }
                catch(OperationCanceledException)
                {resultDetail.Text="工程ROI复算已取消，未发布新Run。";}
                catch(Exception ex){resultDetail.Text="工程ROI复算失败："+ex.Message;}
                finally
                {
                    if(Object.ReferenceEquals(savedRoiRun,work))savedRoiRun=null;
                    cancelRun.IsEnabled=activeRun!=null;
                    work.Dispose();
                    runSavedRoiButton.IsEnabled=hasCurrentSource();
                }
            };
            runExportButton.Click+=(sender,args)=>{
                if(!hasCurrentRun())
                {
                    resultDetail.Text="本Run已失效，禁止将旧结果导出为当前工程证据。";
                    return;
                }
                var dialog=new Microsoft.Win32.SaveFileDialog {
                    Filter="Vision Run结构化CSV|*.csv",DefaultExt=".csv",
                    Title="导出本次视觉Run（不包含Image/Mask二进制）",
                    OverwritePrompt=true
                };
                if(dialog.ShowDialog()!=true)return;
                try
                {
                    CxVisionRunCsvExporter.SaveFile(dialog.FileName,displayedRun,
                        overwrite:true);
                    resultDetail.Text="已导出Run="+displayedRun.RunId.ToString("N")+
                        " 的结构化CSV；图像原像素另用FIM/RAW导出。";
                }
                catch(Exception ex){resultDetail.Text="Run CSV导出失败："+ex.Message;}
            };
            rawExportButton.Click+=(sender,args)=>{
                if(!hasCurrentRun() || !hasCurrentSource())
                {
                    resultDetail.Text="当前没有同源有效Run，禁止将其他图像导出为当前证据。";
                    return;
                }
                FIMImageBuffer output=displayedOriginal;
                var selected=resultList.SelectedItem as ListBoxItem;
                CxVisionResultItem item=selected==null?null:
                    selected.Tag as CxVisionResultItem;
                if(item!=null)
                {
                    if(!displayedRun.Items.Any(x=>Object.ReferenceEquals(x,item)) ||
                       item.RunId!=displayedRun.RunId ||
                       item.SourceId!=displayedRun.SourceId)
                    {
                        resultDetail.Text="选择的结果不属于当前Run。";
                        return;
                    }
                    if(item.Kind!=CxVisionResultKind.Image &&
                       item.Kind!=CxVisionResultKind.Mask)
                    {
                        resultDetail.Text="请选中Image/Mask产物，或取消选择导出原图。";
                        return;
                    }
                    output=item.Image;
                    if(output==null)
                    {
                        resultDetail.Text="所选结果没有可导出的原始像素。";
                        return;
                    }
                }
                var dialog=new Microsoft.Win32.SaveFileDialog{
                    Title="导出本Run的无损原始像素",
                    Filter="RAW images (*.raw)|*.raw",DefaultExt=".raw",
                    OverwritePrompt=true
                };
                if(dialog.ShowDialog(this)!=true)return;
                try
                {
                    FIMRawImageWriter.SaveFile(dialog.FileName,output,
                        preserveStride:true,overwrite:true);
                    resultDetail.Text="已导出Run="+displayedRun.RunId.ToString("N")+
                        (item==null?" 原图":" / "+item.OutputPort)+
                        "，"+output.Width+"×"+output.Height+" "+output.PixelFormat+
                        "，Stride="+output.Stride+"。RAW不包含尺寸元数据。";
                }
                catch(Exception ex){resultDetail.Text="Run RAW导出失败："+ex.Message;}
            };
            sourceButton.Click+=(sender,args)=>{
                if(!hasCurrentRun()||!hasCurrentSource())
                {resultDetail.Text="结果来源失效或当前为非可视Run。";return;}
                resultPreview.SelectOriginal();
                resultDetail.Text="正在查看同一Run的原始像素快照，未二次读取RAW。";
            };
            resultList.SelectionChanged+=(sender,args)=>{
                var selected=resultList.SelectedItem as ListBoxItem;
                if(selected==null)return;
                if(!hasCurrentRun())
                {
                    resultDetail.Text="当前工程页或Revision已变化，拒绝将旧Run显示为当前结果。";
                    return;
                }
                try {
                    CxVisionResultItem item=selected.Tag as CxVisionResultItem;
                    CxVisionResultValidity validity=displayedRun.AssessItemValidity(
                        item,displayedRun.SourceId,displayedRun.ValidatedSourceHash,
                        _viewModel.ProjectSession.Revision);
                    if(validity!=CxVisionResultValidity.Valid &&
                       validity!=CxVisionResultValidity.Empty)
                        throw new InvalidOperationException("vision.result."+validity);
                    resultDetail.Text=SelectStudioVisionResult(displayedRun,
                        displayedOriginal,resultPreview,item);
                    if(validity==CxVisionResultValidity.Empty)
                        resultDetail.Text+="；本次运行正常完成，但候选集合为空。";
                }
                catch(Exception ex){resultDetail.Text="结果显示被拒绝："+ex.Message;}
            };
            contourButton.Click+=async (sender,args)=>{
                if(!hasCurrentRun()||!hasCurrentSource()||
                   displayedRun.Items.Any(x=>
                    x.Kind==CxVisionResultKind.Contour))return;
                CxVisionRunSummary expected=displayedRun;
                FIMImageBuffer image=displayedOriginal;
                Guid project=displayedProject;
                long generation=resultPreview.Session.OriginalGeneration;
                var work=new CancellationTokenSource();
                contourOperation=work;
                contourButton.IsEnabled=false;
                try
                {
                    resultDetail.Text="正在按需提取本Run真实Mask的轮廓…";
                    var projected=await Task.Run(()=>
                        expected.WithThresholdMaskContours(100000,work.Token),work.Token);
                    if(work.IsCancellationRequested || !Object.ReferenceEquals(displayedRun,expected) ||
                       !Object.ReferenceEquals(displayedOriginal,image) ||
                       project!=_viewModel.ProjectSession.Project.Id ||
                       generation!=resultPreview.Session.OriginalGeneration)return;
                    int contourCount=projected.Items.Count(x=>x.Kind==CxVisionResultKind.Contour);
                    if(contourCount>256)throw new InvalidOperationException(
                        "单Run轮廓超过256项，需缩小区域");
                    var projection=new StudioCpuVisionReport {
                        FlowId=projected.RunId,Evidence=projected,SourceImage=image
                    };
                    displayedRun=null;
                    PopulateStudioVisionResults(projection,resultPreview,resultList);
                    displayedRun=projected;
                    displayedOriginalGeneration=resultPreview.Session.OriginalGeneration;
                    resultDetail.Text="已从本Run Mask提取"+contourCount+
                        "条只读轮廓；点击Contour项可在原图叠加孔洞/外环。";
                }
                catch(OperationCanceledException){}
                catch(Exception ex)
                {
                    if(Object.ReferenceEquals(displayedRun,expected))
                    {
                        resultDetail.Text="轮廓提取失败："+ex.Message;
                        contourButton.IsEnabled=true;
                    }
                }
                finally
                {
                    if(Object.ReferenceEquals(contourOperation,work))contourOperation=null;
                    work.Dispose();
                }
            };
            Func<bool,Task> runSelectedRaw=async separated=>{
                if(_logicRunning||activeRun!=null||bindingSavedRoi||savedRoiRun!=null)
                {rawStatus.Text="已有流程或原图绑定正在执行，禁止并行检测";return;}
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
                invalidatePreview();
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
                    PopulateStudioVisionResults(report,resultPreview,resultList);
                    resultPreview.ToolMode=MVIImageToolMode.Navigate;
                    displayedRun=report.Evidence;
                    displayedOriginal=report.SourceImage;
                    displayedOriginalGeneration=resultPreview.Session.OriginalGeneration;
                    displayedProject=_viewModel.ProjectSession.Project.Id;
                    displayedPage=report.PageId;
                    displayedRevision=_viewModel.ProjectSession.Revision;
                    resultExpander.Visibility=Visibility.Visible;
                    resultExpander.IsExpanded=true;
                    resultList.Visibility=Visibility.Visible;
                    resultPreview.Visibility=report.SourceImage==null?
                        Visibility.Collapsed:Visibility.Visible;
                    sourceButton.IsEnabled=report.SourceImage!=null;
                    runExportButton.IsEnabled=true;
                    rawExportButton.IsEnabled=report.SourceImage!=null;
                    saveRoiButton.IsEnabled=report.SourceImage!=null;
                    runSavedRoiButton.IsEnabled=report.SourceImage!=null;
                    contourButton.IsEnabled=report.SourceImage!=null &&
                        report.Evidence.Items.Any(x=>x.Kind==CxVisionResultKind.Mask &&
                            x.OutputPort=="ThresholdMask");
                    resultDetail.Text=report.SourceImage==null?
                        "该Run没有原图快照；可查阅Region和Measurement，" +
                        "只有拆分式软件Run提供本次执行图像和Mask可视化。" :
                        "Run="+report.FlowId.ToString("N")+"；Source="+
                        report.Evidence.SourceId+"。点击产物进行可视选择。";
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
            header.Children.Add(resultExpander);
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
