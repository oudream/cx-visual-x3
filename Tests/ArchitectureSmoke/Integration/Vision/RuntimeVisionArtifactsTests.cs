using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using System.Linq;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Geometry;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Mechanism.Visual;
using CxVisual.Professional.Vision;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>V2D-12/20/21：真实MRK Image+Mask产物、安全快照及默认流程兼容回归。</summary>
    internal static class RuntimeVisionArtifactsTests
    {
        private static void Check(bool ok, string why)
        {
            if (!ok) throw new InvalidOperationException("VISION_ARTIFACTS: " + why);
        }

        private static CxVisionInspectionOptions Options(CxVisionPixelDepth depth, int threshold)
        {
            return new CxVisionInspectionOptions {
                PixelDepth = depth, GrayThreshold = threshold, MinDefectArea = 2,
                MaxDefectArea = 100, UseAi = false
            };
        }

        private static IDictionary<string, object> Inputs(byte[] pixels, int width, int height)
        {
            return new Dictionary<string, object> {
                { "RawPixels", new FDTReadOnlyBuffer(pixels) },
                { "Width", width }, { "Height", height }
            };
        }

        private static void Reject<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("VISION_ARTIFACTS: " + message);
        }

        internal static void Run()
        {
            OptInGray8ThresholdArtifacts();
            Gray16FIMStrideArtifacts();
            LegacyDefaultsAndAtomicFailure();
            ConcurrentFlowOwnership();
            MeasuredArtifactsRemainSourceCorrect();
            SharedHeadlessPreviewApi();
            ProjectBackedMeasuredInspection();
            ProjectBackedMedianEnhancement();
            ProjectBackedSeparatedNodes();
            SeparatedGray16AndInputSafety();
            Gray16MedianEnhancement();
            MviImageMaskProjection();
        }

        private static void OptInGray8ThresholdArtifacts()
        {
            // 三个相连像素、一个孤立像素：阈值Mask是4个命中点，
            // Inspection的最小面积过滤只保留面积3的Region。
            byte[] input = { 210,210,0,0,0, 210,0,0,0,0, 0,0,0,0,250 };
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 16));
            try
            {
                var plan = CxVisionDataFlow.CreateCpuInspectionPlanWithArtifacts(
                    Options(CxVisionPixelDepth.Bits8, 200), 55);
                MRKFlowResult run = engine.StartFlow(plan, Inputs(input, 5, 3))
                    .Completion.GetAwaiter().GetResult();
                Check(run.State == MRKFlowState.Completed,
                    "真实MRK带图像产物CPU流程失败：" +
                    (run.Error == null ? "" : run.Error.ToString()));
                FIMImageBuffer image = run.GetCommittedOutput<FIMImageBuffer>("PreparedImage");
                FIMImageBuffer mask = run.GetCommittedOutput<FIMImageBuffer>("ThresholdMask");
                Check(image.Width == 5 && image.Height == 3 &&
                    image.PixelFormat == FIMPixelFormat.Gray8 &&
                    image.GetGrayValue(0, 0) == 210 && image.GetGrayValue(4, 2) == 250 &&
                    image.GetGrayValue(2, 0) == 0, "预处理灰度图与实际CPU算法不一致");
                Check(mask.PixelFormat == FIMPixelFormat.Gray8 &&
                    mask.Width == 5 && mask.Height == 3 &&
                    mask.GetGrayValue(0, 0) == 255 && mask.GetGrayValue(1, 0) == 255 &&
                    mask.GetGrayValue(0, 1) == 255 && mask.GetGrayValue(4, 2) == 255 &&
                    mask.GetGrayValue(3, 1) == 0, "二值Mask不得混同区域面积过滤结果");
                var inspected = run.GetCommittedOutput<CxVisionInspectionResult>("Inspection");
                Check(inspected.ThresholdPixelCount == 4 &&
                    inspected.Regions.Count == 1 && inspected.Regions[0].PixelArea == 3,
                    "阈值命中与接受区域必须有不同专业语义");

                CxVisionRunSummary result = CxVisionRunSummary.FromCpuFlow(run, "source-A/frame-1");
                var photo = result.Items.Single(x => x.Kind == CxVisionResultKind.Image);
                var binary = result.Items.Single(x => x.Kind == CxVisionResultKind.Mask);
                Check(result.Items.Count == 7 && photo.OutputPort == "PreparedImage" &&
                    binary.OutputPort == "ThresholdMask" &&
                    photo.RunId == run.FlowId && binary.RunId == run.FlowId &&
                    photo.SourceId == "source-A/frame-1" && binary.SourceId == photo.SourceId &&
                    photo.ItemId != binary.ItemId &&
                    photo.Image.GetGrayValue(0, 0) == 210 &&
                    binary.Image.GetGrayValue(4, 2) == 255,
                    "多类型结果缺少真实像素、具名端口或源/Run身份");
                Check(result.Items.Select(x => x.ItemId).Distinct().Count() == 7,
                    "ResultItem必须唯一");
                // V2D-12/21：既有MRK提交的Mask按需提取轮廓，不能改变默认结果或筛选后的Region。
                CxVisionRunSummary traced=result.WithThresholdMaskContours();
                var boundaries=traced.Items.Where(x=>x.Kind==CxVisionResultKind.Contour).ToList();
                Check(result.Items.Count==7 && traced.Items.Count==9 && boundaries.Count==2 &&
                    boundaries.All(x=>x.SourceId==result.SourceId && x.RunId==run.FlowId &&
                        x.OutputPort=="ThresholdMask" && x.Contour!=null &&
                        x.Image==null && x.Unit=="px²") &&
                    Math.Abs(boundaries.Sum(x=>x.Contour.SignedAreaPixels)-4)<1e-9 &&
                    boundaries.All(x=>!x.Contour.IsHole) &&
                    traced.Items.Select(x=>x.ItemId).Distinct().Count()==traced.Items.Count &&
                    object.ReferenceEquals(traced.WithThresholdMaskContours(),traced),
                    "真实阈值Mask应产生两个独立闭环，不能冒充面积过滤后的3px²");
                IList<FGEPoint2D> readonlyPoints=boundaries[0].Contour.Points as IList<FGEPoint2D>;
                Check(readonlyPoints!=null && readonlyPoints.IsReadOnly,
                    "Contour顶点集合不得对外可变");
                Reject<NotSupportedException>(()=>readonlyPoints.Clear(),
                    "调用者不得修改历史轮廓顶点");
                Reject<InvalidOperationException>(()=>result.WithThresholdMaskContours(4),
                    "边界预算超限不得返回截断的假轮廓");
                Reject<ArgumentOutOfRangeException>(()=>result.WithThresholdMaskContours(3),
                    "非法边界预算必须被拒绝");
                using(var stoppedContours=new CancellationTokenSource())
                {
                    stoppedContours.Cancel();
                    Reject<OperationCanceledException>(()=>result.WithThresholdMaskContours(
                        cancellationToken:stoppedContours.Token),
                        "已取消的轮廓投影不得提交任何结果");
                }
                var ringRun=engine.StartFlow(plan,Inputs(new byte[] {
                    255,255,255,255,0,255,255,255,255 },3,3))
                    .Completion.GetAwaiter().GetResult();
                Check(ringRun.State==MRKFlowState.Completed,
                    "真实带孔洞Mask的MRK流程失败");
                var ringResult=CxVisionRunSummary.FromCpuFlow(ringRun,"donut/frame")
                    .WithThresholdMaskContours();
                var ringContours=ringResult.Items.Where(x=>x.Kind==CxVisionResultKind.Contour).ToList();
                Check(ringContours.Count==2 &&
                    ringContours.Any(x=>Math.Abs(x.Contour.SignedAreaPixels-9)<1e-9 && !x.Contour.IsHole) &&
                    ringContours.Any(x=>Math.Abs(x.Contour.SignedAreaPixels+1)<1e-9 && x.Contour.IsHole) &&
                    Math.Abs(ringContours.Sum(x=>x.Contour.SignedAreaPixels)-8)<1e-9,
                    "真实MRK Mask的外环与孔洞必须分开保留，净面积为8");

                byte[] changed = binary.Image.CopyPixels();
                changed[0] = 0;
                input[0] = 0;
                Check(binary.Image.GetGrayValue(0, 0) == 255 &&
                    photo.Image.GetGrayValue(0, 0) == 210 &&
                    run.GetCommittedOutput<FIMImageBuffer>("ThresholdMask").GetGrayValue(0, 0) == 255,
                    "源数组/显示方副本不允许修改已提交的Mask或处理图");
                long declared = (long)image.Stride * image.Height +
                    (long)mask.Stride * mask.Height;
                long estimatedBytes;
                Check(declared > 0 && run.CommittedOutputs.Count == 5 &&
                    run.TryEstimateCommittedBytes(out estimatedBytes) &&
                    estimatedBytes >= declared,
                    "Image/Mask必须由MRK正式导出，并通过FDT专业大小估算计入可观测载荷");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void Gray16FIMStrideArtifacts()
        {
            byte[] source = {
                0xF4,0x01, 0xF4,0x01, 0xEE,0xEE,
                0,0, 0,0, 0xEE,0xEE
            };
            var fim = new FIMImageBuffer(2, 2, FIMPixelFormat.Gray16, source, 6);
            var inputs = CxVisionImageInterop.ToCpuInspectionInputs(
                fim, CxVisionPixelDepth.Bits16);
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 10));
            try
            {
                var run = engine.StartFlow(
                    CxVisionDataFlow.CreateCpuInspectionPlanWithArtifacts(
                        Options(CxVisionPixelDepth.Bits16, 400), 72), inputs)
                    .Completion.GetAwaiter().GetResult();
                Check(run.State == MRKFlowState.Completed, "Gray16带Stride专业产物执行失败");
                var photo = run.GetCommittedOutput<FIMImageBuffer>("PreparedImage");
                var mask = run.GetCommittedOutput<FIMImageBuffer>("ThresholdMask");
                Check(photo.PixelFormat == FIMPixelFormat.Gray16 && photo.Stride == 4 &&
                    photo.GetGrayValue(0, 0) == 500 && photo.GetGrayValue(1, 0) == 500 &&
                    photo.GetGrayValue(0, 1) == 0 &&
                    mask.PixelFormat == FIMPixelFormat.Gray8 && mask.Stride == 2 &&
                    mask.GetGrayValue(0, 0) == 255 && mask.GetGrayValue(1, 0) == 255 &&
                    mask.GetGrayValue(1, 1) == 0, "16位小端、Stride或Mask灰度格式错误");
                var summary = CxVisionRunSummary.FromCpuFlow(run, "XRay-C/frame-2");
                Check(summary.Items.Count(x => x.Kind == CxVisionResultKind.Image ||
                        x.Kind == CxVisionResultKind.Mask) == 2 &&
                    summary.Items.Single(x => x.Kind == CxVisionResultKind.Image)
                        .Image.PixelFormat == FIMPixelFormat.Gray16 &&
                    summary.Items.Single(x => x.Kind == CxVisionResultKind.Mask)
                        .Image.PixelFormat == FIMPixelFormat.Gray8 &&
                    fim.GetGrayValue(0, 0) == 500, "产物投影不允许隐式把原16位降为8位");
                source[0] = 0;
                Check(photo.GetGrayValue(0, 0) == 500 &&
                    summary.Items.Single(x => x.Kind == CxVisionResultKind.Image)
                        .Image.GetGrayValue(0, 0) == 500, "外部源数组不能污染真实产物");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void LegacyDefaultsAndAtomicFailure()
        {
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 10));
            try
            {
                var options = Options(CxVisionPixelDepth.Bits8, 200);
                var legacy = engine.StartFlow(CxVisionDataFlow.CreateCpuInspectionPlan(options, 73),
                    Inputs(new byte[] { 210,210,0,0,0, 210,0,0,0,0, 0,0,0,0,250 }, 5, 3))
                    .Completion.GetAwaiter().GetResult();
                Check(legacy.State == MRKFlowState.Completed &&
                    legacy.CommittedOutputs.Count == 3 &&
                    !legacy.CommittedOutputs.ContainsKey("PreparedImage") &&
                    !legacy.CommittedOutputs.ContainsKey("ThresholdMask") &&
                    CxVisionRunSummary.FromCpuFlow(legacy, "legacy").Items.Count == 5,
                    "旧CPU Plan必须保持原数据出口及默认内存开销");
                Reject<InvalidOperationException>(()=>
                    CxVisionRunSummary.FromCpuFlow(legacy,"legacy")
                        .WithThresholdMaskContours(),
                    "无Mask的旧流程不得伪造轮廓");
                var bad = engine.StartFlow(
                    CxVisionDataFlow.CreateCpuInspectionPlanWithArtifacts(options, 74),
                    Inputs(new byte[] { 1, 2 }, 5, 3))
                    .Completion.GetAwaiter().GetResult();
                Check(bad.State == MRKFlowState.Failed && bad.CommittedOutputs.Count == 0,
                    "错误输入的Image/Mask不能越过MRK原子提交屏障");
                Reject<InvalidOperationException>(
                    () => CxVisionRunSummary.FromCpuFlow(bad, "bad"),
                    "执行失败不得生成有效Run结果");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        /// <summary>
        /// CASE-ARCH-01测量首包：真正MRK下游节点消费面积筛选后的Inspection；
        /// 不把筛选前Mask命中像素或包围盒面积误称为接受区域测量。
        /// </summary>
        private static void MeasuredArtifactsRemainSourceCorrect()
        {
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(4,16));
            try
            {
                byte[] raw = { 210,210,0,0,0, 210,0,0,0,0, 0,0,0,0,250 };
                var plan = CxVisionDataFlow.CreateCpuInspectionPlanWithMeasuredArtifacts(
                    Options(CxVisionPixelDepth.Bits8,200),91);
                MRKFlowResult flow=engine.StartFlow(plan,Inputs(raw,5,3))
                    .Completion.GetAwaiter().GetResult();
                Check(flow.State==MRKFlowState.Completed,
                    "带测量节点的MRK实际执行失败："+(flow.Error==null?"":flow.Error.ToString()));
                Check(flow.CommittedOutputs.Count==6 &&
                    flow.GetCommittedOutput<long>("AcceptedRegionPixelArea")==3 &&
                    flow.GetCommittedOutput<CxVisionInspectionResult>("Inspection")
                        .ThresholdPixelCount==4 &&
                    flow.GetCommittedOutput<FIMImageBuffer>("ThresholdMask")
                        .GetGrayValue(4,2)==255,
                    "接受区域像素面积3不得冒充阈值Mask命中总量4");
                var evidence=CxVisionRunSummary.FromCpuFlow(flow,"camera-1/frame-1");
                var area=evidence.Items.Single(x=>x.Kind==CxVisionResultKind.Measurement);
                var region=evidence.Items.Single(x=>x.Kind==CxVisionResultKind.Region);
                Check(area.IntegerValue==3 && area.Unit=="px²" &&
                    area.OutputPort=="AcceptedRegionPixelArea" &&
                    area.RunId==flow.FlowId && area.SourceId==evidence.SourceId &&
                    region.Region.PixelArea==3 &&
                    evidence.Items.Select(x=>x.ItemId).Distinct().Count()==evidence.Items.Count,
                    "测量Item必须归属真实Run/端口/Source且ID不冲突");
                Check(!evidence.Items.Any(x=>x.Kind==CxVisionResultKind.Measurement &&
                      x.Unit=="mm²"),"未标定不得冒充物理面积");
                raw[0]=0;
                Check(area.IntegerValue==3 &&
                    flow.GetCommittedOutput<FIMImageBuffer>("PreparedImage")
                        .GetGrayValue(0,0)==210,
                    "外部源数组被修改后正式测量与FIM原像素不可变化");

                var clean=engine.StartFlow(plan,Inputs(new byte[15],5,3))
                    .Completion.GetAwaiter().GetResult();
                Check(clean.State==MRKFlowState.Completed &&
                    clean.GetCommittedOutput<long>("AcceptedRegionPixelArea")==0,
                    "无候选区域仍须合法提交面积0而非运行失败");
                var cleanEvidence=CxVisionRunSummary.FromCpuFlow(clean,"camera-1/frame-2");
                Check(cleanEvidence.HasNoRegions &&
                    cleanEvidence.Items.Single(x=>x.Kind==CxVisionResultKind.Measurement)
                        .IntegerValue==0 &&
                    cleanEvidence.RunId!=evidence.RunId,
                    "空结果应保留独立Run身份并发布明确0面积");

                var invalid=engine.StartFlow(plan,Inputs(new byte[] { 1 },5,3))
                    .Completion.GetAwaiter().GetResult();
                Check(invalid.State==MRKFlowState.Failed &&
                    invalid.CommittedOutputs.Count==0,
                    "输入损坏不能发布面积或部分Image/Mask结果");
                Reject<InvalidOperationException>(
                    ()=>CxVisionRunSummary.FromCpuFlow(invalid,"invalid"),
                    "失败Run不得伪造Measurement项");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        /// <summary>Studio/SDK无需WPF即可调用和TestCenter相同的专业预览链。</summary>
        private static void SharedHeadlessPreviewApi()
        {
            // 带行Padding的FIM源图，末尾孤立命中点必须计入Mask但不计入已接受区域。
            var source = new FIMImageBuffer(5,3,FIMPixelFormat.Gray8,new byte[] {
                210,210,0,0,0,99,99,
                210,0,0,0,0,99,99,
                0,0,0,0,250,99,99
            },7);
            CxVisionInspectionOptions options=Options(CxVisionPixelDepth.Bits8,200);
            var run=CxVisionDataFlow.RunCpuInspectionPreviewAsync(
                source,options,"source/headless",System.Threading.CancellationToken.None)
                .GetAwaiter().GetResult();
            var accepted=run.Items.Single(i=>i.Kind==CxVisionResultKind.Measurement);
            var mask=run.Items.Single(i=>i.Kind==CxVisionResultKind.Mask);
            Check(run.RunId!=Guid.Empty && run.SourceId=="source/headless" &&
                accepted.IntegerValue==3 && accepted.Unit=="px²" &&
                mask.Image.GetGrayValue(4,2)==255 &&
                run.Items.Single(i=>i.Name=="阈值命中像素数").IntegerValue==4 &&
                source.GetGrayValue(0,0)==210,
                "无界面API未复用真实测量合同或有Stride的FIM像素");
            Reject<ArgumentException>(()=>CxVisionDataFlow.RunCpuInspectionPreviewAsync(
                source,Options(CxVisionPixelDepth.Bits16,200),"bad",
                System.Threading.CancellationToken.None).GetAwaiter().GetResult(),
                "位深不匹配必须在MRK运行之前拒绝");
            Reject<ArgumentException>(()=>CxVisionDataFlow.RunCpuInspectionPreviewAsync(
                source,options," ",System.Threading.CancellationToken.None).GetAwaiter().GetResult(),
                "空SourceId不可形成有效证据");
            using(var cts=new System.Threading.CancellationTokenSource())
            {
                cts.Cancel();
                Reject<OperationCanceledException>(()=>
                    CxVisionDataFlow.RunCpuInspectionPreviewAsync(
                        source,options,"cancel",cts.Token).GetAwaiter().GetResult(),
                    "已取消请求不得发布结果");
            }
        }

        /// <summary>
        /// 真正MEM/MOL持久定义→MOC编译→授权MRK计划→独立测量节点→
        /// SourceMap事件映射。关闭工程重新打开验证原来的三块定义仍存在，
        /// 不伪称已具备脱离RAW来源的生产重放与Artifact持久化。
        /// </summary>
        private static void ProjectBackedMeasuredInspection()
        {
            string name="vision-moc-"+Guid.NewGuid().ToString("N");
            string rawPath=Path.Combine(Path.GetTempPath(),name+".raw");
            string resizedPath=Path.Combine(Path.GetTempPath(),name+"-resized.raw");
            string projectPath=Path.Combine(Path.GetTempPath(),name+".cxproj");
            File.WriteAllBytes(rawPath,new byte[] {
                210,210,0,0,0, 210,0,0,0,0, 0,0,0,0,250 });
            // Separate malformed RAW length from a valid 6x3 RAW whose dimensions
            // conflict with the saved 5x3 engineering parameters.
            File.WriteAllBytes(resizedPath,new byte[18]);
            try
            {
                var provider=new CxVisionThresholdProvider(
                    Options(CxVisionPixelDepth.Bits8,200),17);
                MRKResolvedCapability inspectCapability;
                Check(provider.TryResolve(CxVisionThresholdProvider.CapabilityId,
                        out inspectCapability) &&
                    inspectCapability.DataOutputs.ContainsKey("Inspection") &&
                    inspectCapability.DataOutputs["Inspection"]==
                        typeof(CxVisionInspectionResult) &&
                    provider.DescribeCapabilities().Single().Ports.Any(x=>
                        x.MemberId=="Inspection" &&
                        x.TypeId==CxVisionAcceptedRegionMeasureProvider.InspectionTypeId),
                    "专业Inspection必须正式声明并冻结MDS具名结果，不能只用CLR StepResult");
                var measure=new CxVisionAcceptedRegionMeasureProvider(provider);
                var declaration=measure.DescribeCapabilities().Single();
                Check(declaration.Id==CxVisionAcceptedRegionMeasureProvider.CapabilityId &&
                    declaration.Ports.Single(x=>x.MemberId=="Inspection").TypeId==
                        CxVisionAcceptedRegionMeasureProvider.InspectionTypeId &&
                    declaration.Ports.Single(x=>x.MemberId=="Area").TypeId=="long",
                    "测量MOC能力未声明专业输入及强类型面积输出");
                using(var project=MEMEngineering.Create("Vision工程测量"))
                {
                    try
                    {
                    CxVisionCpuRun result=CxVisionRawCpuRunner.ExecuteMeasuredAsync(
                        project,provider,rawPath,5,3,CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(result.FlowId!=Guid.Empty && result.InspectBlockId!=Guid.Empty &&
                        result.MeasureBlockId!=Guid.Empty &&
                        result.MeasureBlockId!=result.InspectBlockId &&
                        result.AcceptedRegionPixelArea==3 &&
                        result.Evidence.Items.Single(x=>x.Kind==CxVisionResultKind.Measurement)
                            .IntegerValue==3 &&
                        result.Evidence.RunId==result.FlowId &&
                        result.Trace.Any(t=>t.BlockId==result.MeasureBlockId &&
                            t.State=="Completed") &&
                        !string.IsNullOrWhiteSpace(result.Fingerprint),
                        "MOC三节点工程运行/Trace/面积证据不一致");
                    var snapshot=project.GetSnapshot();
                    var content=snapshot.Nodes.Single(n=>n.ParentId==result.PageId &&
                        n.Kind==MEMNodeKind.Content);
                    MOLWiringDefinition wiring=MOLWiringCodec.Get(content);
                    Check(wiring!=null && wiring.Blocks.Count==3 &&
                        wiring.FlowOutputs.ContainsKey("AcceptedRegionPixelArea") &&
                        wiring.Links.Count(x=>x.Kind==MPCPortKind.Data)==2 &&
                        wiring.Blocks.Any(x=>x.Id==result.MeasureBlockId &&
                            x.CapabilityId==CxVisionAcceptedRegionMeasureProvider.CapabilityId),
                        "正式三节点并未持久编入唯一MEM/MOL工程内容");
                    project.SaveAs(projectPath);
                    }
                    finally { project.Close(true); }
                }
                using(var reopened=MEMEngineering.Open(projectPath))
                {
                    var saved=reopened.GetSnapshot();
                    var content=saved.Nodes.Single(n=>n.Kind==MEMNodeKind.Content &&
                        MOLWiringCodec.Get(n)!=null);
                    var wiring=MOLWiringCodec.Get(content);
                    Check(wiring.Blocks.Count==3 &&
                        wiring.Blocks.Any(x=>x.CapabilityId==
                            CxVisionAcceptedRegionMeasureProvider.CapabilityId) &&
                        wiring.Links.Count(x=>x.Kind==MPCPortKind.Data)==2,
                        "Save/Open丢失测量端口连接或能力身份");

                    // The saved definition can run again after explicit RAW/Provider
                    // rebinding; it must not add a second page or mutate Revision.
                    int savedRevision=saved.Revision;
                    var rebinding=CxVisionRawCpuRunner.ExecuteSavedMeasuredAsync(
                        reopened,content.Id,provider,rawPath,5,3,CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(rebinding.PageId==content.ParentId &&
                        rebinding.InspectBlockId!=Guid.Empty &&
                        rebinding.MeasureBlockId!=Guid.Empty &&
                        rebinding.AcceptedRegionPixelArea==3 &&
                        rebinding.Evidence.Items.Single(x=>
                            x.Kind==CxVisionResultKind.Measurement).IntegerValue==3 &&
                        rebinding.Trace.Any(x=>x.BlockId==rebinding.MeasureBlockId &&
                            x.State=="Completed") &&
                        reopened.GetSnapshot().Revision==savedRevision && !reopened.IsDirty,
                        "重新打开的MEM工程未能无编辑重绑RAW后执行正式MOC测量");
                    Reject<InvalidOperationException>(()=>
                        CxVisionRawCpuRunner.ExecuteSavedMeasuredAsync(
                            reopened,content.Id,provider,resizedPath,6,3,CancellationToken.None)
                        .GetAwaiter().GetResult(),
                        "重绑输入尺寸与已保存的工程参数冲突必须拒绝");
                    Check(reopened.GetSnapshot().Revision==savedRevision,
                        "保存工程拒绝不匹配RAW后不可偷偷更改定义");
                }
                using(var legacy=MEMEngineering.Create("保留两步旧调用"))
                {
                    try
                    {
                    var old=CxVisionRawCpuRunner.ExecuteAsync(
                        legacy,provider,rawPath,5,3,CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(old.MeasureBlockId==Guid.Empty &&
                        !old.AcceptedRegionPixelArea.HasValue &&
                        !old.Evidence.Items.Any(x=>x.Kind==CxVisionResultKind.Measurement),
                        "旧MOC两节点调用被强制增加测量节点");
                    }
                    finally { legacy.Close(true); }
                }
            }
            finally
            {
                if(File.Exists(rawPath))File.Delete(rawPath);
                if(File.Exists(resizedPath))File.Delete(resizedPath);
                if(File.Exists(projectPath))File.Delete(projectPath);
            }
        }


        /// <summary>
        /// V2D D05: 真正的4节点RAW→FIM中值→Inspection→Measure。
        /// 数据对照唯一FIM，原始输入保持、增强中间产物公开、SourceMap可追踪。
        /// 保存/重开/重绑仍使用同一个MEM Content，Radius变更需明确拒绝。
        /// </summary>
        private static void ProjectBackedMedianEnhancement()
        {
            string basename="vision-median-"+Guid.NewGuid().ToString("N");
            string rawPath=Path.Combine(Path.GetTempPath(),basename+".raw");
            string projectPath=Path.Combine(Path.GetTempPath(),basename+".cxproj");
            byte[] raw=new byte[49];
            for(int y=2;y<=4;y++)
                for(int x=2;x<=4;x++)raw[y*7+x]=210;
            raw[0]=250; // 单独噪点，1像素
            File.WriteAllBytes(rawPath,raw);
            try
            {
                var provider=new CxVisionThresholdProvider(
                    Options(CxVisionPixelDepth.Bits8,200),48);
                var enhance=new CxVisionMedianEnhanceProvider(
                    CxVisionPixelDepth.Bits8,1,provider);
                var descriptor=enhance.DescribeCapabilities().Single();
                Check(descriptor.Ports.Any(x=>x.MemberId=="EnhancedPixels" &&
                    x.TypeId=="byte[]" && x.Direction==MPCPortDirection.Output) &&
                    descriptor.Ports.Any(x=>x.MemberId=="Radius" && x.Required) &&
                    enhance.ReleaseVersion!=new CxVisionMedianEnhanceProvider(
                        CxVisionPixelDepth.Bits8,2).ReleaseVersion,
                    "FIM增强的能力目录、工程参数与Pinned版本不正确");
                Reject<ArgumentOutOfRangeException>(()=>
                    new CxVisionMedianEnhanceProvider(CxVisionPixelDepth.Bits8,0),
                    "中值半径0必须在工程创建前拒绝");
                Reject<ArgumentOutOfRangeException>(()=>
                    new CxVisionMedianEnhanceProvider(CxVisionPixelDepth.Bits8,4),
                    "超出限定滤波半径必须拒绝");

                Guid contentId;
                using(var session=MEMEngineering.Create("FIM四节点视觉工程"))
                {
                    try
                    {
                        var result=CxVisionRawCpuRunner.ExecuteEnhancedMeasuredAsync(
                            session,provider,rawPath,7,7,1,CancellationToken.None)
                            .GetAwaiter().GetResult();
                        var expected=FIMImageFilters.Apply(new FIMImageBuffer(7,7,
                            FIMPixelFormat.Gray8,raw),FIMNeighborhoodFilter.Median,1);
                        Check(result.EnhanceBlockId!=Guid.Empty &&
                            result.EnhanceBlockId!=result.InspectBlockId &&
                            result.MeasureBlockId!=result.EnhanceBlockId &&
                            result.Inspection.ThresholdPixelCount==5 &&
                            result.Inspection.Regions.Count==1 &&
                            result.AcceptedRegionPixelArea==5 &&
                            result.EnhancedImage!=null &&
                            result.EnhancedImage.PixelFormat==FIMPixelFormat.Gray8 &&
                            result.EnhancedImage.GetGrayValue(0,0)==0 &&
                            result.EnhancedImage.GetGrayValue(3,3)==210 &&
                            result.EnhancedImage.CopyPixels().SequenceEqual(expected.CopyPixels()) &&
                            result.Evidence.Items.Single(x=>
                                x.Kind==CxVisionResultKind.Measurement).IntegerValue==5 &&
                            result.Evidence.Items.Single(x=>x.Kind==CxVisionResultKind.Image &&
                                x.OutputPort=="EnhancedPixels").Image
                                .CopyPixels().SequenceEqual(expected.CopyPixels()) &&
                            result.Evidence.Items.All(x=>x.RunId==result.FlowId &&
                                x.SourceId==result.Evidence.SourceId) &&
                            result.Trace.Any(x=>x.BlockId==result.EnhanceBlockId &&
                                x.State=="Completed"),
                            "四节点真实FIM中值处理/阈值/区域面积/Trace不一致");
                        Check(File.ReadAllBytes(rawPath).SequenceEqual(raw),
                            "FIM专业滤波修改了源RAW文件");
                        var root=session.GetSnapshot();
                        var content=root.Nodes.Single(n=>n.Kind==MEMNodeKind.Content &&
                            n.ParentId==result.PageId);
                        contentId=content.Id;
                        var wiring=MOLWiringCodec.Get(content);
                        Check(wiring.Blocks.Count==4 &&
                            wiring.Links.Count(x=>x.Kind==MPCPortKind.Data)==3 &&
                            wiring.FlowOutputs.ContainsKey("EnhancedPixels") &&
                            wiring.FlowOutputs.ContainsKey("AcceptedRegionPixelArea") &&
                            wiring.Blocks.Any(x=>x.Id==result.EnhanceBlockId &&
                                x.CapabilityId==CxVisionMedianEnhanceProvider.CapabilityId),
                            "增强节点未保存成正式MEM/MOL多类型连线与公开产物");
                        session.SaveAs(projectPath);
                    }
                    finally {session.Close(true);}
                }
                using(var reopened=MEMEngineering.Open(projectPath))
                {
                    var before=reopened.GetSnapshot();
                    Check(before.Find(contentId)!=null,
                        "重开后增强Content身份丢失");
                    var run=CxVisionRawCpuRunner.ExecuteSavedEnhancedMeasuredAsync(
                        reopened,contentId,provider,rawPath,7,7,1,CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(run.AcceptedRegionPixelArea==5 &&
                        run.EnhancedImage!=null &&
                        run.Trace.Any(x=>x.BlockId==run.EnhanceBlockId &&
                            x.State=="Completed") &&
                        reopened.GetSnapshot().Revision==before.Revision && !reopened.IsDirty,
                        "已保存四节点工程重绑RAW再执行失败或篡改工程Revision");
                    Reject<InvalidOperationException>(()=>
                        CxVisionRawCpuRunner.ExecuteSavedEnhancedMeasuredAsync(
                            reopened,contentId,provider,rawPath,7,7,2,CancellationToken.None)
                        .GetAwaiter().GetResult(),
                        "已保存半径与新Provider版本不一致却仍执行");
                    Check(reopened.GetSnapshot().Revision==before.Revision,
                        "错误半径运行污染了正式工程定义");
                }
            }
            finally
            {
                if(File.Exists(rawPath))File.Delete(rawPath);
                if(File.Exists(projectPath))File.Delete(projectPath);
            }
        }


        /// <summary>
        /// V2D-12/18/19/21/23：同一MEM/MOC/MRK上，阈值和区域是独立强类型节点；
        /// 保存重开后重绑RAW，参数漂移明确拒绝且工程定义不变。
        /// </summary>
        private static void ProjectBackedSeparatedNodes()
        {
            string label="vision-separated-"+Guid.NewGuid().ToString("N");
            string rawPath=Path.Combine(Path.GetTempPath(),label+".raw");
            string projectPath=Path.Combine(Path.GetTempPath(),label+".cxproj");
            byte[] pixels={210,210,0,0,0,210,0,0,0,0,0,0,0,0,250};
            File.WriteAllBytes(rawPath,pixels);
            CxVisionInspectionOptions options=Options(CxVisionPixelDepth.Bits8,200);
            Guid contentId=Guid.Empty;
            try
            {
                var segmentation=new CxVisionBinaryThresholdProvider(options.PixelDepth,200);
                var regions=new CxVisionMaskRegionsProvider(options,101);
                Check(segmentation.DescribeCapabilities().Single().Ports.Any(x=>
                        x.MemberId=="ThresholdMask" && x.Direction==MPCPortDirection.Output &&
                        x.TypeId==CxVisionBinaryThresholdProvider.MaskTypeId) &&
                    regions.DescribeCapabilities().Single().Ports.Any(x=>
                        x.MemberId=="ThresholdMask" && x.Direction==MPCPortDirection.Input &&
                        x.TypeId==CxVisionBinaryThresholdProvider.MaskTypeId) &&
                    regions.DescribeCapabilities().Single().Ports.Any(x=>
                        x.MemberId=="RuleVersion" && x.Required),
                    "独立Mask/Region正式端口与冻结规则合同不一致");
                Reject<ArgumentOutOfRangeException>(()=>
                    new CxVisionBinaryThresholdProvider(CxVisionPixelDepth.Bits8,256),
                    "Gray8阈值越界必须拒绝");
                using(var session=MEMEngineering.Create("独立分割区域工程"))
                {
                    try
                    {
                        var result=CxVisionRawCpuRunner.ExecuteSeparatedMeasuredAsync(
                            session,options,101,rawPath,5,3,0,CancellationToken.None)
                            .GetAwaiter().GetResult();
                        var mask=result.ThresholdMask;
                        Check(result.SegmentBlockId!=Guid.Empty &&
                            result.InspectBlockId!=Guid.Empty &&
                            result.MeasureBlockId!=Guid.Empty &&
                            result.SegmentBlockId!=result.InspectBlockId &&
                            result.InspectBlockId!=result.MeasureBlockId &&
                            result.EnhanceBlockId==Guid.Empty &&
                            mask!=null && mask.PixelFormat==FIMPixelFormat.Gray8 &&
                            mask.GetGrayValue(0,0)==255 && mask.GetGrayValue(4,2)==255 &&
                            mask.GetGrayValue(1,1)==0 &&
                            result.Inspection.ThresholdPixelCount==4 &&
                            result.Inspection.Regions.Count==1 &&
                            result.Inspection.Regions[0].PixelArea==3 &&
                            result.AcceptedRegionPixelArea==3 &&
                            result.Evidence.Items.Single(x=>x.Kind==CxVisionResultKind.Mask)
                                .Image.GetGrayValue(4,2)==255 &&
                            result.Evidence.Items.Single(x=>x.Kind==CxVisionResultKind.Measurement)
                                .IntegerValue==3 &&
                            result.Trace.Any(x=>x.BlockId==result.SegmentBlockId &&
                                x.State=="Completed"),
                            "独立阈值Mask→区域→面积没有得到真实数值和来源");
                        var boundaries=result.Evidence.WithThresholdMaskContours();
                        Check(boundaries.Items.Count(x=>x.Kind==CxVisionResultKind.Contour)==2 &&
                            boundaries.Items.Where(x=>x.Kind==CxVisionResultKind.Contour)
                                .Sum(x=>x.Contour.SignedAreaPixels)==4,
                            "独立Mask轮廓必须反映筛选前4像素而非接受区域3像素");
                        var snapshot=session.GetSnapshot();
                        var content=snapshot.Nodes.Single(x=>x.Kind==MEMNodeKind.Content &&
                            x.ParentId==result.PageId);
                        contentId=content.Id;
                        var wiring=MOLWiringCodec.Get(content);
                        Check(wiring.Blocks.Count==4 &&
                            wiring.Links.Count(x=>x.Kind==MPCPortKind.Data)==3 &&
                            wiring.FlowOutputs.ContainsKey("ThresholdMask") &&
                            wiring.FlowOutputs.ContainsKey("AcceptedRegionPixelArea") &&
                            wiring.Blocks.Any(x=>x.Id==result.SegmentBlockId &&
                                x.CapabilityId==CxVisionBinaryThresholdProvider.CapabilityId) &&
                            wiring.Blocks.Any(x=>x.Id==result.InspectBlockId &&
                                x.CapabilityId==CxVisionMaskRegionsProvider.CapabilityId),
                            "独立四节点未在MEM/MOL持久保存强类型连接");
                        session.SaveAs(projectPath);
                    }
                    finally {session.Close(true);}
                }
                using(var reopened=MEMEngineering.Open(projectPath))
                {
                    var before=reopened.GetSnapshot();
                    var run=CxVisionRawCpuRunner.ExecuteSavedSeparatedMeasuredAsync(
                        reopened,contentId,options,101,rawPath,5,3,0,CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(run.AcceptedRegionPixelArea==3 &&
                        run.ThresholdMask.GetGrayValue(4,2)==255 &&
                        run.PageId!=Guid.Empty &&
                        reopened.GetSnapshot().Revision==before.Revision && !reopened.IsDirty,
                        "已保存独立工程无法重绑复跑或篡改工程Revision");
                    var changed=Options(CxVisionPixelDepth.Bits8,201);
                    Reject<InvalidOperationException>(()=>
                        CxVisionRawCpuRunner.ExecuteSavedSeparatedMeasuredAsync(
                            reopened,contentId,changed,101,rawPath,5,3,0,CancellationToken.None)
                            .GetAwaiter().GetResult(),
                        "修改阈值仍能绕过持久工程参数版本");
                    var changedArea=Options(CxVisionPixelDepth.Bits8,200);
                    changedArea.MinDefectArea=4;
                    Reject<InvalidOperationException>(()=>
                        CxVisionRawCpuRunner.ExecuteSavedSeparatedMeasuredAsync(
                            reopened,contentId,changedArea,101,rawPath,5,3,0,
                            CancellationToken.None).GetAwaiter().GetResult(),
                        "修改区域面积范围仍能绕过规则版本");
                    Check(reopened.GetSnapshot().Revision==before.Revision && !reopened.IsDirty,
                        "参数拒绝不能修改已保存的工程");
                }
                // 五节点，真实FIM中值先清理孤立噪点再进入分割与测量。
                byte[] large=new byte[49];
                for(int y=2;y<=4;y++)for(int x=2;x<=4;x++)large[y*7+x]=210;
                large[0]=250;
                File.WriteAllBytes(rawPath,large);
                using(var project=MEMEngineering.Create("独立五节点"))
                {
                    try
                    {
                        var run=CxVisionRawCpuRunner.ExecuteSeparatedMeasuredAsync(
                            project,options,102,rawPath,7,7,1,CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Check(run.EnhanceBlockId!=Guid.Empty &&
                            run.SegmentBlockId!=Guid.Empty &&
                            run.EnhancedImage!=null &&
                            run.EnhancedImage.GetGrayValue(0,0)==0 &&
                            run.ThresholdMask.GetGrayValue(0,0)==0 &&
                            run.ThresholdMask.GetGrayValue(3,3)==255 &&
                            run.Inspection.ThresholdPixelCount==5 &&
                            run.AcceptedRegionPixelArea==5 &&
                            project.GetSnapshot().Nodes.Any(n=>n.Kind==MEMNodeKind.Content &&
                                n.ParentId==run.PageId &&
                                MOLWiringCodec.Get(n).Blocks.Count==5),
                            "五节点FIM增强→Mask→连通区域→面积真实执行失败");
                    }
                    finally {project.Close(true);}
                }
                Check(File.ReadAllBytes(rawPath).SequenceEqual(large),
                    "独立算子不得改变用户RAW文件");
            }
            finally
            {
                if(File.Exists(rawPath))File.Delete(rawPath);
                if(File.Exists(projectPath))File.Delete(projectPath);
            }
        }

        /// <summary>独立拆分节点的Gray16、小端、预取消与坏RAW原子性反证。</summary>
        private static void SeparatedGray16AndInputSafety()
        {
            string path=Path.Combine(Path.GetTempPath(),
                "vision-split16-"+Guid.NewGuid().ToString("N")+".raw");
            byte[] raw={0xF4,0x01,0xF4,0x01,0,0,0,0};
            File.WriteAllBytes(path,raw);
            try
            {
                CxVisionInspectionOptions options=Options(CxVisionPixelDepth.Bits16,400);
                using(var project=MEMEngineering.Create("独立分割16位"))
                {
                    try
                    {
                        var result=CxVisionRawCpuRunner.ExecuteSeparatedMeasuredAsync(
                            project,options,103,path,2,2,0,CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Check(result.ThresholdMask!=null &&
                            result.ThresholdMask.PixelFormat==FIMPixelFormat.Gray8 &&
                            result.ThresholdMask.GetGrayValue(0,0)==255 &&
                            result.ThresholdMask.GetGrayValue(1,0)==255 &&
                            result.ThresholdMask.GetGrayValue(0,1)==0 &&
                            result.Inspection.ThresholdPixelCount==2 &&
                            result.AcceptedRegionPixelArea==2 &&
                            result.Evidence.Items.Single(x=>x.Kind==CxVisionResultKind.Mask)
                                .Image.GetGrayValue(1,0)==255 &&
                            File.ReadAllBytes(path).SequenceEqual(raw),
                            "独立Gray16小端阈值与二值Mask/接受区域不一致");
                        long before=project.GetSnapshot().Revision;
                        using(var cancelled=new CancellationTokenSource())
                        {
                            cancelled.Cancel();
                            Reject<OperationCanceledException>(()=>
                                CxVisionRawCpuRunner.ExecuteSeparatedMeasuredAsync(
                                    project,options,103,path,2,2,0,cancelled.Token)
                                    .GetAwaiter().GetResult(),
                                "预取消操作不得写入新工程节点");
                        }
                        File.WriteAllBytes(path,new byte[]{1,2,3});
                        Reject<InvalidDataException>(()=>
                            CxVisionRawCpuRunner.ExecuteSeparatedMeasuredAsync(
                                project,options,103,path,2,2,0,CancellationToken.None)
                                .GetAwaiter().GetResult(),
                            "损坏RAW不得编译/污染工程或产生正式Mask");
                        Check(project.GetSnapshot().Revision==before,
                            "无效RAW或已取消操作不允许新增工程节点");
                    }
                    finally{project.Close(true);}
                }
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }

        /// <summary>相同四节点真实算法以Gray16小端RAW执行，不得降到8位。</summary>
        private static void Gray16MedianEnhancement()
        {
            string path=Path.Combine(Path.GetTempPath(),
                "vision-median16-"+Guid.NewGuid().ToString("N")+".raw");
            byte[] bytes=new byte[98];
            for(int y=2;y<=4;y++)
                for(int x=2;x<=4;x++)
                {
                    int offset=(y*7+x)*2;
                    bytes[offset]=(byte)(4000&255);
                    bytes[offset+1]=(byte)(4000>>8);
                }
            bytes[0]=(byte)(60000&255);
            bytes[1]=(byte)(60000>>8);
            File.WriteAllBytes(path,bytes);
            try
            {
                var provider=new CxVisionThresholdProvider(
                    Options(CxVisionPixelDepth.Bits16,2000),58);
                var depth8=new CxVisionMedianEnhanceProvider(CxVisionPixelDepth.Bits8,1);
                var depth16=new CxVisionMedianEnhanceProvider(CxVisionPixelDepth.Bits16,1);
                Check(depth8.ReleaseVersion!=depth16.ReleaseVersion,
                    "中值增强Provider发布版本没有固定像素位深");
                using(var project=MEMEngineering.Create("四节点16位FIM"))
                {
                    try
                    {
                        var outcome=CxVisionRawCpuRunner.ExecuteEnhancedMeasuredAsync(
                            project,provider,path,7,7,1,CancellationToken.None)
                            .GetAwaiter().GetResult();
                        Check(outcome.EnhancedImage!=null &&
                            outcome.EnhancedImage.PixelFormat==FIMPixelFormat.Gray16 &&
                            outcome.EnhancedImage.GetGrayValue(0,0)==0 &&
                            outcome.EnhancedImage.GetGrayValue(3,3)==4000 &&
                            outcome.Inspection.ThresholdPixelCount==5 &&
                            outcome.AcceptedRegionPixelArea==5 &&
                            outcome.Evidence.Items.Any(x=>
                                x.Kind==CxVisionResultKind.Image &&
                                x.Image.PixelFormat==FIMPixelFormat.Gray16) &&
                            File.ReadAllBytes(path).SequenceEqual(bytes),
                            "16位RAW在四节点增强期间被降位深/污染或统计错误");
                    }
                    finally {project.Close(true);}
                }
            }
            finally {if(File.Exists(path))File.Delete(path);}
        }

        /// <summary>
        /// 验证专业Run的FIM产物可直接交给现有MVI双槽；不需要第二Viewer状态。
        /// 此处是UI无关状态集成，WPF视觉展示仍由Studio/TestCenter后续验收。
        /// </summary>
        private static void MviImageMaskProjection()
        {
            byte[] pixels = {
                0xF4,0x01, 0xF4,0x01, 0xEE,0xEE,
                0,0, 0,0, 0xEE,0xEE
            };
            FIMImageBuffer original = new FIMImageBuffer(2, 2,
                FIMPixelFormat.Gray16, pixels, 6);
            var inputs = CxVisionImageInterop.ToCpuInspectionInputs(original,
                CxVisionPixelDepth.Bits16);
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 10));
            try
            {
                var run = engine.StartFlow(
                    CxVisionDataFlow.CreateCpuInspectionPlanWithArtifacts(
                        Options(CxVisionPixelDepth.Bits16, 400), 76), inputs)
                    .Completion.GetAwaiter().GetResult();
                Check(run.State == MRKFlowState.Completed, "Viewer映射用的真实Run失败");
                var result = CxVisionRunSummary.FromCpuFlow(run, "XRay-Viewer/source-1");
                var binary = result.Items.Single(i => i.Kind == CxVisionResultKind.Mask);
                var session = new MVIImageSession();
                session.SetOriginal(original);
                // 上层选择必须先检查SourceId/RunId后，才向Viewer提交显示。
                Check(binary.RunId == result.RunId &&
                    binary.SourceId == result.SourceId &&
                    binary.Image.Width == session.Original.Width &&
                    binary.Image.Height == session.Original.Height,
                    "产物来源、Run身份及坐标尺寸不一致");
                session.SetResult(binary.Image);
                session.Select(MVIImageSlot.Result);
                Check(session.HasResult &&
                    session.ActiveImage.PixelFormat == FIMPixelFormat.Gray8 &&
                    session.ActiveImage.GetGrayValue(0, 0) == 255 &&
                    session.Original.PixelFormat == FIMPixelFormat.Gray16 &&
                    session.Original.GetGrayValue(0, 0) == 500,
                    "Result Mask在MVI中投影错误或破坏原16位像素");
                session.Select(MVIImageSlot.Original);
                Check(session.ActiveImage.GetGrayValue(0, 0) == 500,
                    "从Result返回Original时丢失原图位深");
                session.SetOriginal(new FIMImageBuffer(2, 2, FIMPixelFormat.Gray8,
                    new byte[] { 5, 6, 7, 8 }));
                Check(!session.HasResult && session.Result == null &&
                    session.ActiveSlot == MVIImageSlot.Original &&
                    session.ActiveImage.GetGrayValue(0, 0) == 5,
                    "新原图加载后必须清理旧Run可视结果；历史Result留在专业Run而非Viewer");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void ConcurrentFlowOwnership()
        {
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 16));
            try
            {
                var plan = CxVisionDataFlow.CreateCpuInspectionPlanWithArtifacts(
                    Options(CxVisionPixelDepth.Bits8, 200), 75);
                var a = engine.StartFlow(plan, Inputs(
                    new byte[] { 220,220,0,0,0, 220,0,0,0,0, 0,0,0,0,0 }, 5, 3));
                var b = engine.StartFlow(plan, Inputs(
                    new byte[] { 0,0,0,0,0, 0,0,0,0,0, 0,0,0,0,0 }, 5, 3));
                var ra = a.Completion.GetAwaiter().GetResult();
                var rb = b.Completion.GetAwaiter().GetResult();
                var sa = CxVisionRunSummary.FromCpuFlow(ra, "a");
                var sb = CxVisionRunSummary.FromCpuFlow(rb, "b");
                var ma = sa.Items.Single(x => x.Kind == CxVisionResultKind.Mask);
                var mb = sb.Items.Single(x => x.Kind == CxVisionResultKind.Mask);
                Check(sa.RunId != sb.RunId && ma.ItemId != mb.ItemId &&
                    ma.Image.GetGrayValue(0, 0) == 255 &&
                    mb.Image.GetGrayValue(0, 0) == 0 &&
                    !object.ReferenceEquals(ma.Image, mb.Image) &&
                    sb.HasNoRegions, "并行Flow不能共享Mask或误归属Run身份");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }
    }
}
