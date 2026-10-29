using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using System.Linq;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Imaging;
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
            string projectPath=Path.Combine(Path.GetTempPath(),name+".cxproj");
            File.WriteAllBytes(rawPath,new byte[] {
                210,210,0,0,0, 210,0,0,0,0, 0,0,0,0,250 });
            try
            {
                var provider=new CxVisionThresholdProvider(
                    Options(CxVisionPixelDepth.Bits8,200),17);
                var measure=new CxVisionAcceptedRegionMeasureProvider(provider);
                var declaration=measure.DescribeCapabilities().Single();
                Check(declaration.Id==CxVisionAcceptedRegionMeasureProvider.CapabilityId &&
                    declaration.Ports.Single(x=>x.MemberId=="Inspection").TypeId==
                        CxVisionAcceptedRegionMeasureProvider.InspectionTypeId &&
                    declaration.Ports.Single(x=>x.MemberId=="Area").TypeId=="long",
                    "测量MOC能力未声明专业输入及强类型面积输出");
                using(var project=MEMEngineering.Create("Vision工程测量"))
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
                        wiring.Links.Count(x=>x.Kind==MPCPortKind.Data)==2 &&
                        wiring.Blocks.Any(x=>x.Id==result.MeasureBlockId &&
                            x.CapabilityId==CxVisionAcceptedRegionMeasureProvider.CapabilityId),
                        "正式三节点并未持久编入唯一MEM/MOL工程内容");
                    project.SaveAs(projectPath);
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
                }
                using(var legacy=MEMEngineering.Create("保留两步旧调用"))
                {
                    var old=CxVisionRawCpuRunner.ExecuteAsync(
                        legacy,provider,rawPath,5,3,CancellationToken.None)
                        .GetAwaiter().GetResult();
                    Check(old.MeasureBlockId==Guid.Empty &&
                        !old.AcceptedRegionPixelArea.HasValue &&
                        !old.Evidence.Items.Any(x=>x.Kind==CxVisionResultKind.Measurement),
                        "旧MOC两节点调用被强制增加测量节点");
                    legacy.Close(true);
                }
            }
            finally
            {
                if(File.Exists(rawPath))File.Delete(rawPath);
                if(File.Exists(projectPath))File.Delete(projectPath);
            }
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
