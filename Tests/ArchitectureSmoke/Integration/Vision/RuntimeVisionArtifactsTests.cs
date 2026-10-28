using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
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
