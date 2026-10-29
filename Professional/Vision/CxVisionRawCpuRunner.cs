using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Foundation.Imaging;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Professional.Vision
{
    public sealed class CxVisionCpuTrace
    {
        public long Sequence {get;internal set;}
        public string StepId {get;internal set;}
        public Guid BlockId {get;internal set;}
        public string State {get;internal set;}
        public DateTime TimestampUtc {get;internal set;}
    }
    public sealed class CxVisionCpuRun
    {
        public Guid PageId {get;internal set;}
        public Guid FlowId {get;internal set;}
        public Guid InspectBlockId {get;internal set;}
        /// <summary>Only present for the independent Mask segmentation node.</summary>
        public Guid SegmentBlockId {get;internal set;}
        public FIMImageBuffer ThresholdMask {get;internal set;}
        /// <summary>Only present for opt-in FIM median enhancement flow.</summary>
        public Guid EnhanceBlockId {get;internal set;}
        public FIMImageBuffer EnhancedImage {get;internal set;}
        /// <summary>Only populated for opt-in project measurement.</summary>
        public Guid MeasureBlockId {get;internal set;}
        public long? AcceptedRegionPixelArea {get;internal set;}
        public int EngineeringRevision {get;internal set;}
        public string Fingerprint {get;internal set;}
        public string ConfigVersion {get;internal set;}
        public CxVisionInspectionResult Inspection {get;internal set;}
        public CxVisionRunSummary Evidence {get;internal set;}
        public int CompletedTraceEvents {get;internal set;}
        public IReadOnlyList<CxVisionCpuTrace> Trace {get;internal set;}
    }
    /// <summary>
    /// Reusable in-process CPU-only pipeline: actual RAW bytes -> MEM -> MOC -> MRK ->
    /// professional Vision result + SourceMap trace. No hardware/production authorization.
    /// TestCenter and Studio use this same professional Owner, not two executors.
    /// </summary>
    public static class CxVisionRawCpuRunner
    {
        private const string InputId="vision.cpu.local.raw";
        private const string Principal="vision.cpu.local.sandbox";
        private sealed class RawProvider : IMOCCapabilityProvider,IMRKVersionedCapabilityResolver
        {
            private readonly byte[] _bytes;
            private readonly string _version;
            private readonly IMRKVersionedCapabilityResolver _fallback;
            private readonly MRKResolvedCapability _resolved;
            public RawProvider(byte[] bytes,IMRKVersionedCapabilityResolver fallback)
            {
                _bytes=(byte[])bytes.Clone();
                _fallback=fallback;
                using(var hash=SHA256.Create())
                    _version="raw.sha256."+BitConverter.ToString(hash.ComputeHash(_bytes)).Replace("-","");
                _resolved=new MRKResolvedCapability((context,token)=>{
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult((object)((byte[])_bytes.Clone()));
                },new MRKExecutionOutput(typeof(byte[])));
            }
            public IEnumerable<MOCCapabilityDescriptor> DescribeCapabilities()
            {
                return new[]{new MOCCapabilityDescriptor(InputId,"本次RAW输入","Vision/Input",
                    new[]{new MPCPortDescriptor("Pixels",MPCPortDirection.Output,
                        "byte[]",MPCPortKind.Data)})};
            }
            public bool TryResolve(string id,out MRKResolvedCapability result)
            {
                if(id==InputId){result=_resolved;return true;}
                return _fallback.TryResolve(id,out result);
            }
            public bool TryGetCapabilityVersion(string id,out string version)
            {
                if(id==InputId){version=_version;return true;}
                return _fallback.TryGetCapabilityVersion(id,out version);
            }
        }
        private sealed class CpuOnlyPolicy : IMRKPlanPublicationPolicy
        {
            public bool Authorize(string principal,MRKPlanPublishAction action,
                MRKExecutionPlan plan,long revision)
            {
                return principal==Principal && revision==1 && plan!=null &&
                    (plan.Steps.Count>=2 && plan.Steps.Count<=5);
            }
            public bool IsSafeToActivate(MRKExecutionPlan plan,long revision)
            {
                return revision==1 && plan!=null &&
                    (plan.Steps.Count>=2 && plan.Steps.Count<=5);
            }
        }
        public static byte[] ReadRaw(string path,int width,int height,
            CxVisionPixelDepth depth,CancellationToken token)
        {
            if(String.IsNullOrWhiteSpace(path))throw new ArgumentException("RAW路径必填","path");
            long count=(long)width*height;
            if(width<=0||height<=0||count>32000000L)
                throw new ArgumentOutOfRangeException("width","RAW尺寸不合法");
            if(depth!=CxVisionPixelDepth.Bits8&&depth!=CxVisionPixelDepth.Bits16)
                throw new ArgumentException("RAW位深不支持");
            int len=checked((int)(count*(depth==CxVisionPixelDepth.Bits8?1:2)));
            token.ThrowIfCancellationRequested();
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(file.Length!=len)
                    throw new InvalidDataException("RAW字节数与宽高/Effective位深不符："+
                        "fileBytes="+file.Length+
                        " expectedBytes="+len+
                        " width="+width+" height="+height+
                        " pixelDepth="+(int)depth);
                byte[] data=new byte[len];int n=0;
                while(n<len)
                {
                    token.ThrowIfCancellationRequested();
                    int read=file.Read(data,n,len-n);
                    if(read<=0)throw new EndOfStreamException("RAW提前结束");
                    n+=read;
                }
                if(file.ReadByte()!=-1)throw new InvalidDataException("RAW读取时文件增长");
                return data;
            }
        }
        public static Task<CxVisionCpuRun> ExecuteAsync(
            MEMEngineeringSession session,CxVisionThresholdProvider vision,
            string path,int width,int height,CancellationToken cancellation)
        {
            return ExecuteCoreAsync(session,vision,path,width,height,cancellation,false);
        }

        /// <summary>
        /// Opt-in project-backed RAW→Inspect→Measure with MOC source map and pinned versions.
        /// No new graph or executor; original two-step API remains unchanged.
        /// </summary>
        public static Task<CxVisionCpuRun> ExecuteMeasuredAsync(
            MEMEngineeringSession session,CxVisionThresholdProvider vision,
            string path,int width,int height,CancellationToken cancellation)
        {
            return ExecuteCoreAsync(session,vision,path,width,height,cancellation,true);
        }

        /// <summary>
        /// 四节点真实工程：RAW → FIM中值增强 → CPU阈值/区域 → 已接受面积。
        /// 半径和位深固定在Provider版本及MOL参数中；不改变已有两/三节点API。
        /// </summary>
        public static Task<CxVisionCpuRun> ExecuteEnhancedMeasuredAsync(
            MEMEngineeringSession session,CxVisionThresholdProvider vision,
            string path,int width,int height,int medianRadius,CancellationToken cancellation)
        {
            if(medianRadius<1 || medianRadius>3)
                throw new ArgumentOutOfRangeException("medianRadius");
            return ExecuteCoreAsync(session,vision,path,width,height,cancellation,true,
                medianRadius:medianRadius);
        }

        /// <summary>
        /// Re-execute an already saved and reopened three-node MEM/MOL definition,
        /// explicitly rebinding the RAW input and CPU Provider for this invocation.
        /// Never mutates the saved engineering tree; the new RunId is independent,
        /// while the same source and pinned versions retain the definition fingerprint.
        /// </summary>
        public static Task<CxVisionCpuRun> ExecuteSavedMeasuredAsync(
            MEMEngineeringSession session,Guid contentId,CxVisionThresholdProvider vision,
            string path,int width,int height,CancellationToken cancellation)
        {
            if(contentId==Guid.Empty)
                throw new ArgumentException("必须指定已保存的工程Content","contentId");
            return ExecuteCoreAsync(session,vision,path,width,height,cancellation,true,contentId);
        }

        /// <summary>重开四节点工程后显式重绑新RAW，校验半径和工程修订。</summary>
        public static Task<CxVisionCpuRun> ExecuteSavedEnhancedMeasuredAsync(
            MEMEngineeringSession session,Guid contentId,CxVisionThresholdProvider vision,
            string path,int width,int height,int medianRadius,CancellationToken cancellation)
        {
            if(contentId==Guid.Empty)throw new ArgumentException("必须指定工程Content","contentId");
            if(medianRadius<1 || medianRadius>3)
                throw new ArgumentOutOfRangeException("medianRadius");
            return ExecuteCoreAsync(session,vision,path,width,height,cancellation,true,
                contentId,medianRadius);
        }

        /// <summary>
        /// 真实可编排拆分链：RAW → [FIM中值] → 阈值Mask → 独立四邻域Region → 面积。
        /// 不更改旧一体化检测入口；配置与阈值随Provider版本固定。
        /// </summary>
        public static Task<CxVisionCpuRun> ExecuteSeparatedMeasuredAsync(
            MEMEngineeringSession session,CxVisionInspectionOptions options,long revision,
            string path,int width,int height,int medianRadius,CancellationToken cancellation)
        {
            if(options==null)throw new ArgumentNullException("options");
            if(medianRadius<0 || medianRadius>3)
                throw new ArgumentOutOfRangeException("medianRadius");
            var vision=new CxVisionThresholdProvider(options,revision);
            return ExecuteCoreAsync(session,vision,path,width,height,cancellation,true,
                medianRadius:medianRadius,splitNodes:true,splitOptions:options);
        }

        /// <summary>同一已保存MEM/MOL拆分工作链，重新绑定RAW/Provider并验证节点参数。</summary>
        public static Task<CxVisionCpuRun> ExecuteSavedSeparatedMeasuredAsync(
            MEMEngineeringSession session,Guid contentId,CxVisionInspectionOptions options,
            long revision,string path,int width,int height,int medianRadius,
            CancellationToken cancellation)
        {
            if(contentId==Guid.Empty)throw new ArgumentException("工程Content不能为空","contentId");
            if(options==null)throw new ArgumentNullException("options");
            if(medianRadius<0 || medianRadius>3)
                throw new ArgumentOutOfRangeException("medianRadius");
            var vision=new CxVisionThresholdProvider(options,revision);
            return ExecuteCoreAsync(session,vision,path,width,height,cancellation,true,
                contentId,medianRadius,true,options);
        }

        private static async Task<CxVisionCpuRun> ExecuteCoreAsync(
            MEMEngineeringSession session,CxVisionThresholdProvider vision,
            string path,int width,int height,CancellationToken cancellation,
            bool includeMeasurement,Guid? existingContentId=null,int medianRadius=0,
            bool splitNodes=false,CxVisionInspectionOptions splitOptions=null)
        {
            if(session==null)throw new ArgumentNullException("session");
            if(vision==null)throw new ArgumentNullException("vision");
            cancellation.ThrowIfCancellationRequested();
            string version;
            if(!vision.TryGetCapabilityVersion(CxVisionThresholdProvider.CapabilityId,out version)||
                String.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException("CPU Vision配置版本不可用");
            // Validate before modifying the engineering project.
            byte[] pixels=ReadRaw(path,width,height,vision.PixelDepth,cancellation);
            cancellation.ThrowIfCancellationRequested();
            CxVisionAcceptedRegionMeasureProvider measureProvider=includeMeasurement ?
                new CxVisionAcceptedRegionMeasureProvider(vision) : null;
            IMRKVersionedCapabilityResolver resolver=includeMeasurement ?
                (IMRKVersionedCapabilityResolver)measureProvider : vision;
            CxVisionMaskRegionsProvider regions=splitNodes ?
                new CxVisionMaskRegionsProvider(splitOptions,vision.ConfigurationRevision,resolver) : null;
            if(regions!=null)resolver=regions;
            CxVisionBinaryThresholdProvider segment=splitNodes ?
                new CxVisionBinaryThresholdProvider(vision.PixelDepth,
                    splitOptions.GrayThreshold,resolver) : null;
            if(segment!=null)resolver=segment;
            CxVisionMedianEnhanceProvider enhancement=medianRadius>0 ?
                new CxVisionMedianEnhanceProvider(vision.PixelDepth,medianRadius,resolver) : null;
            if(enhancement!=null)resolver=enhancement;
            var source=new RawProvider(pixels,resolver);
            Guid pageId=Guid.Empty,entryContentId=Guid.Empty;
            Guid inspect=Guid.Empty,measure=Guid.Empty,enhance=Guid.Empty,segmentation=Guid.Empty;
            if(existingContentId.HasValue)
            {
                // Validate persisted definition before any MOC publication or MRK work.
                var saved=session.GetSnapshot();
                MEMNodeSnapshot content=saved.Find(existingContentId.Value);
                MEMNodeSnapshot parent=content==null||!content.ParentId.HasValue ?
                    null:saved.Find(content.ParentId.Value);
                if(content==null || content.Kind!=MEMNodeKind.Content ||
                    parent==null || parent.Kind!=MEMNodeKind.Page)
                    throw new InvalidOperationException("vision.saved.invalid_content");
                MOLWiringDefinition wiring=MOLWiringCodec.Get(content);
                if(wiring==null || wiring.Blocks.Count!=(splitNodes?(medianRadius>0?5:4):(medianRadius>0?4:3)) ||
                    wiring.Links.Count(x=>x.Kind==MPCPortKind.Data)!=(splitNodes?(medianRadius>0?4:3):(medianRadius>0?3:2)) ||
                    !wiring.FlowOutputs.ContainsKey("AcceptedRegionPixelArea") ||
                    (medianRadius>0 && !wiring.FlowOutputs.ContainsKey("EnhancedPixels")))
                    throw new InvalidOperationException("vision.saved.requires_three_node_wiring");
                var rawBlocks=wiring.Blocks.Where(x=>x.CapabilityId==InputId).ToArray();
                var inspectBlocks=wiring.Blocks.Where(x=>x.CapabilityId==
                    (splitNodes?CxVisionMaskRegionsProvider.CapabilityId:
                        CxVisionThresholdProvider.CapabilityId)).ToArray();
                var segmentBlocks=wiring.Blocks.Where(x=>
                    x.CapabilityId==CxVisionBinaryThresholdProvider.CapabilityId).ToArray();
                var measureBlocks=wiring.Blocks.Where(x=>
                    x.CapabilityId==CxVisionAcceptedRegionMeasureProvider.CapabilityId).ToArray();
                var enhanceBlocks=wiring.Blocks.Where(x=>
                    x.CapabilityId==CxVisionMedianEnhanceProvider.CapabilityId).ToArray();
                if(rawBlocks.Length!=1 || inspectBlocks.Length!=1 || measureBlocks.Length!=1 ||
                    enhanceBlocks.Length!=(medianRadius>0?1:0) ||
                    segmentBlocks.Length!=(splitNodes?1:0))
                    throw new InvalidOperationException("vision.saved.capability_mismatch");
                inspect=inspectBlocks[0].Id;
                measure=measureBlocks[0].Id;
                if(splitNodes)segmentation=segmentBlocks[0].Id;
                if(medianRadius>0)
                {
                    enhance=enhanceBlocks[0].Id;
                    IReadOnlyDictionary<string,string> enhanceParams;
                    string savedRadius,savedWidth,savedHeight;
                    int actualRadius,actualWidth,actualHeight;
                    if(!wiring.Parameters.TryGetValue(enhance,out enhanceParams) ||
                        !enhanceParams.TryGetValue("Radius",out savedRadius) ||
                        !enhanceParams.TryGetValue("Width",out savedWidth) ||
                        !enhanceParams.TryGetValue("Height",out savedHeight) ||
                        !int.TryParse(savedRadius,NumberStyles.Integer,CultureInfo.InvariantCulture,
                            out actualRadius) ||
                        !int.TryParse(savedWidth,NumberStyles.Integer,CultureInfo.InvariantCulture,
                            out actualWidth) ||
                        !int.TryParse(savedHeight,NumberStyles.Integer,CultureInfo.InvariantCulture,
                            out actualHeight) ||
                        actualRadius!=medianRadius || actualWidth!=width || actualHeight!=height)
                        throw new InvalidOperationException("vision.saved.enhance_parameters_changed");
                }
                IReadOnlyDictionary<string,string> parameters;
                string declaredWidth,declaredHeight;
                int expectedWidth,expectedHeight;
                if(!wiring.Parameters.TryGetValue(splitNodes?segmentation:inspect,out parameters) ||
                    !parameters.TryGetValue("Width",out declaredWidth) ||
                    !parameters.TryGetValue("Height",out declaredHeight) ||
                    !int.TryParse(declaredWidth,NumberStyles.Integer,
                        CultureInfo.InvariantCulture,out expectedWidth) ||
                    !int.TryParse(declaredHeight,NumberStyles.Integer,
                        CultureInfo.InvariantCulture,out expectedHeight) ||
                    expectedWidth!=width || expectedHeight!=height)
                    throw new InvalidOperationException("vision.saved.image_dimensions_changed");
                if(splitNodes)
                {
                    string thresholdText;
                    int savedThreshold;
                    if(!parameters.TryGetValue("Threshold",out thresholdText) ||
                       !int.TryParse(thresholdText,NumberStyles.Integer,
                           CultureInfo.InvariantCulture,out savedThreshold) ||
                       savedThreshold!=splitOptions.GrayThreshold)
                        throw new InvalidOperationException("vision.saved.threshold_changed");
                }
                pageId=parent.Id;
                entryContentId=content.Id;
            }
            else
            {
                var page=session.AddPage((medianRadius>0 ?
                    "FIM增强与CPU区域面积测量 · " : includeMeasurement ?
                    "CPU RAW检测与面积测量 · " : "CPU RAW检测 · ")+Path.GetFileName(path));
                pageId=page.Id;
                entryContentId=page.RootContent.Id;
                MOCFlowBuilder.Edit(session,page.RootContent,b=>{
                    Guid raw=b.Step(source.DescribeCapabilities().Single());
                    inspect=b.Step(splitNodes?
                        regions.DescribeCapabilities().Single():vision.DescribeCapabilities().Single());
                    if(splitNodes)
                    {
                        segmentation=b.Step(segment.DescribeCapabilities().Single());
                        b.SetParameter(segmentation,"Width",width.ToString(CultureInfo.InvariantCulture));
                        b.SetParameter(segmentation,"Height",height.ToString(CultureInfo.InvariantCulture));
                        b.SetParameter(segmentation,"Threshold",
                            splitOptions.GrayThreshold.ToString(CultureInfo.InvariantCulture));
                        b.ExposeOutput("ThresholdMask",segmentation,"ThresholdMask");
                    }
                    else
                    {
                        b.SetParameter(inspect,"Width",width.ToString(CultureInfo.InvariantCulture));
                        b.SetParameter(inspect,"Height",height.ToString(CultureInfo.InvariantCulture));
                    }
                    if(enhancement!=null)
                    {
                        enhance=b.Step(enhancement.DescribeCapabilities().Single());
                        b.SetParameter(enhance,"Width",width.ToString(CultureInfo.InvariantCulture));
                        b.SetParameter(enhance,"Height",height.ToString(CultureInfo.InvariantCulture));
                        b.SetParameter(enhance,"Radius",medianRadius.ToString(CultureInfo.InvariantCulture));
                        b.Connect(raw,"Done",enhance,"Start",MPCPortKind.Control);
                        b.Connect(raw,"Pixels",enhance,"RawPixels",MPCPortKind.Data);
                        b.Connect(enhance,"Done",splitNodes?segmentation:inspect,
                            "Start",MPCPortKind.Control);
                        b.Connect(enhance,"EnhancedPixels",splitNodes?segmentation:inspect,
                            "RawPixels",MPCPortKind.Data);
                        b.ExposeOutput("EnhancedPixels",enhance,"EnhancedPixels");
                    }
                    else
                    {
                        b.Connect(raw,"Done",splitNodes?segmentation:inspect,
                            "Start",MPCPortKind.Control);
                        b.Connect(raw,"Pixels",splitNodes?segmentation:inspect,
                            "RawPixels",MPCPortKind.Data);
                    }
                    if(splitNodes)
                    {
                        b.Connect(segmentation,"Done",inspect,"Start",MPCPortKind.Control);
                        b.Connect(segmentation,"ThresholdMask",inspect,"ThresholdMask",MPCPortKind.Data);
                    }
                    if(includeMeasurement)
                    {
                        measure=b.Step(measureProvider.DescribeCapabilities().Single());
                        b.Connect(inspect,"Done",measure,"Start",MPCPortKind.Control);
                        b.Connect(inspect,"Inspection",measure,"Inspection",MPCPortKind.Data);
                        b.ExposeOutput("AcceptedRegionPixelArea",measure,"Area");
                    }
                });
            }
            string rawVersion;
            source.TryGetCapabilityVersion(InputId,out rawVersion);
            var versions=new Dictionary<string,string>(StringComparer.Ordinal) {
                {InputId,rawVersion}};
            if(splitNodes)
            {
                versions.Add(CxVisionBinaryThresholdProvider.CapabilityId,segment.ReleaseVersion);
                versions.Add(CxVisionMaskRegionsProvider.CapabilityId,regions.ReleaseVersion);
            }
            else versions.Add(CxVisionThresholdProvider.CapabilityId,version);
            if(includeMeasurement)
                versions.Add(CxVisionAcceptedRegionMeasureProvider.CapabilityId,
                    CxVisionAcceptedRegionMeasureProvider.Version);
            if(enhancement!=null)
                versions.Add(CxVisionMedianEnhanceProvider.CapabilityId,enhancement.ReleaseVersion);
            MEMProjectSnapshot snapshot=session.GetSnapshot();
            var candidate=MOCProjectRelease.Prepare(snapshot,entryContentId,source,versions);
            cancellation.ThrowIfCancellationRequested();
            var baseline=new MRKExecutionPlan("vision.cpu.inert",
                new[]{new MRKExecutionStep("inert",new string[0],
                    (context,token)=>Task.FromResult((object)0))});
            var slot=new MRKExecutionPlanSlot(baseline,0,true);
            var publisher=new MRKExecutionPlanPublisher(slot,baseline,0,new CpuOnlyPolicy());
            var receipt=MOCProjectRelease.Stage(candidate,session.GetSnapshot(),
                source,versions,publisher,Principal,1);
            if(!receipt.Accepted||!publisher.TryActivate(Principal))
                throw new InvalidOperationException("CPU软件发布未获许可");
            using(var active=slot.Capture())
                if(!Object.ReferenceEquals(active.Plan,candidate.Plan.Plan)||active.Revision!=1)
                    throw new InvalidOperationException("MRK软件激活版本不一致");
            var compiled=candidate.Plan;
            var engine=new MRKExecutionEngine(new FCRWorkerOptions(2,16));
            try
            {
                var defaults=compiled.InputDefaults.ToDictionary(x=>x.Key,x=>x.Value,
                    StringComparer.Ordinal);
                var flow=engine.StartFlow(compiled.Plan,defaults);
                using(cancellation.Register(()=>flow.RequestCancel()))
                {
                    var result=await flow.Completion.ConfigureAwait(false);
                    if(result.State!=MRKFlowState.Completed)
                        throw new InvalidOperationException("MRK未完成："+result.State+
                            (result.Error==null?"":" / "+result.Error.ToString()),
                            result.Error);
                    object raw;
                    if(!result.Results.TryGetValue("w"+inspect.ToString("N"),out raw))
                        throw new InvalidOperationException("MRK结果缺失");
                    var inspection=raw as CxVisionInspectionResult;
                    if(inspection==null||inspection.Width!=width||inspection.Height!=height||
                        inspection.ConfigRevision!=vision.ConfigurationRevision)
                        throw new InvalidOperationException("检测结果与配置或输入尺寸不一致");
                    long? acceptedArea=null;
                    if(includeMeasurement)
                    {
                        object measured;
                        if(!result.Results.TryGetValue("w"+measure.ToString("N"),out measured) ||
                           !(measured is long))
                            throw new InvalidOperationException("工程测量节点结果缺失或类型错误");
                        acceptedArea=(long)measured;
                        if(acceptedArea.Value!=result.GetCommittedOutput<long>(
                                "AcceptedRegionPixelArea"))
                            throw new InvalidOperationException(
                                "工程具名Area出口与MOC测量节点结果不一致");
                        if(acceptedArea.Value!=CxVisionDataFlow.MeasureAcceptedPixelArea(
                            inspection,cancellation))
                            throw new InvalidOperationException("工程区域面积与算法结果不一致");
                    }
                    FIMImageBuffer thresholdMask=null;
                    if(splitNodes)
                    {
                        thresholdMask=result.GetCommittedOutput<FIMImageBuffer>("ThresholdMask");
                        if(thresholdMask.PixelFormat!=FIMPixelFormat.Gray8 ||
                           thresholdMask.Width!=width || thresholdMask.Height!=height)
                            throw new InvalidOperationException("工程二值Mask格式或尺寸不正确");
                    }
                    FIMImageBuffer enhancedImage=null;
                    if(enhancement!=null)
                    {
                        byte[] imageBytes=result.GetCommittedOutput<byte[]>("EnhancedPixels");
                        int bytesPerPixel=vision.PixelDepth==CxVisionPixelDepth.Bits8?1:2;
                        if(imageBytes==null || imageBytes.Length!=(long)width*height*bytesPerPixel)
                            throw new InvalidOperationException("FIM中值输出尺寸或位深与RAW输入不符");
                        enhancedImage=new FIMImageBuffer(width,height,
                            bytesPerPixel==1?FIMPixelFormat.Gray8:FIMPixelFormat.Gray16,
                            imageBytes);
                    }
                    var trace=compiled.ReadTrace(flow,0);
                    if(splitNodes && !trace.Events.Any(e=>
                        e.Origin.BlockId==segmentation &&
                        e.Transition.State==MRKStepState.Completed))
                        throw new InvalidOperationException("独立分割节点完成Trace缺失");
                    if(enhancement!=null && !trace.Events.Any(e=>
                        e.Origin.BlockId==enhance &&
                        e.Transition.State==MRKStepState.Completed))
                        throw new InvalidOperationException("工程增强节点Trace缺失");
                    if(includeMeasurement && !trace.Events.Any(e=>
                        e.Origin.BlockId==measure &&
                        e.Transition.State==MRKStepState.Completed))
                        throw new InvalidOperationException("工程测量节点完成Trace缺失");
                    int completed=trace.Events.Count(e=>e.Origin.BlockId==inspect&&
                        e.Transition.State==MRKStepState.Completed);
                    if(completed==0||trace.LostCount!=0)
                        throw new InvalidOperationException("MRK真实完成Trace缺失");
                    return new CxVisionCpuRun {
                        PageId=pageId,FlowId=result.FlowId,InspectBlockId=inspect,
                        SegmentBlockId=segmentation,ThresholdMask=thresholdMask,
                        EnhanceBlockId=enhance,EnhancedImage=enhancedImage,
                        MeasureBlockId=measure,AcceptedRegionPixelArea=acceptedArea,
                        EngineeringRevision=snapshot.Revision,Fingerprint=candidate.Fingerprint,
                        ConfigVersion=version,Inspection=inspection,CompletedTraceEvents=completed,
                        Evidence=CxVisionRunSummary.FromInspection(result.FlowId,path,inspection,
                            planFingerprint:candidate.Fingerprint,capabilityVersion:version,
                            completedUtc:result.CompletedUtc,
                            acceptedRegionPixelArea:acceptedArea,
                            enhancedImage:enhancedImage,
                             thresholdMask:thresholdMask),
                        Trace=trace.Events.Take(300).Select(e=>new CxVisionCpuTrace {
                            Sequence=e.Transition.Sequence,StepId=e.Transition.StepId,
                            BlockId=e.Origin.BlockId,State=e.Transition.State.ToString(),
                            TimestampUtc=e.Transition.TimestampUtc}).ToArray()
                    };
                }
            }
            finally{await engine.StopAsync(true).ConfigureAwait(false);}
        }
    }
}
