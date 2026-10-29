using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Imaging;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Professional.Vision
{
    /// <summary>
    /// 真实CPU Vision算法通过既有MRK端口及MDS DataContext运行。
    /// 不新建调度器、数据仓库或模拟检测算法。源图像由安全只读Buffer持有。
    /// </summary>
    public static class CxVisionDataFlow
    {
        /// <summary>
        /// 统一的CPU已接受区域面积算法，供直接MRK Plan与MOC可编排
        /// Measurement能力共享；不计算筛选前Mask面积，也不推断物理mm²。
        /// </summary>
        internal static long MeasureAcceptedPixelArea(
            CxVisionInspectionResult result, CancellationToken cancellation)
        {
            if (result == null || result.Regions == null || result.Width <= 0 ||
                result.Height <= 0 || result.ThresholdPixelCount < 0 ||
                result.ThresholdPixelCount > (long)result.Width * result.Height)
                throw new ArgumentException("无效的专业区域检测结果", "result");
            long area = 0;
            foreach (CxVisionDefectRegion region in result.Regions)
            {
                cancellation.ThrowIfCancellationRequested();
                if (region == null || region.PixelArea <= 0)
                    throw new InvalidOperationException("有效缺陷区域面积不合法");
                area = checked(area + region.PixelArea);
            }
            if (area > result.ThresholdPixelCount ||
                area > (long)result.Width * result.Height)
                throw new InvalidOperationException("筛选后面积大于阈值命中像素数");
            return area;
        }

        private static CxVisionInspectionResult CopyResult(CxVisionInspectionResult result)
        {
            List<CxVisionDefectRegion> regions = new List<CxVisionDefectRegion>();
            foreach (CxVisionDefectRegion r in result.Regions)
                regions.Add(new CxVisionDefectRegion
                {
                    PixelArea = r.PixelArea, MinX = r.MinX, MinY = r.MinY,
                    MaxX = r.MaxX, MaxY = r.MaxY
                });
            return new CxVisionInspectionResult
            {
                ConfigRevision = result.ConfigRevision,
                Width = result.Width,
                Height = result.Height,
                ThresholdPixelCount = result.ThresholdPixelCount,
                Regions = regions.AsReadOnly()
            };
        }

        /// <summary>单通道8/16-bit RAW（16位小端）；不将SDK、AI或机台功能冒充已实现。</summary>
        public static MRKExecutionPlan CreateCpuInspectionPlan(
            CxVisionInspectionOptions options, long revision = 1)
        {
            return BuildCpuInspectionPlan(options, revision, false, false);
        }

        /// <summary>
        /// 显式要求发布专业图像产物时才增加灰度处理图与0/255阈值Mask；
        /// 不改变原CreateCpuInspectionPlan签名、已有调用方或默认内存开销。
        /// Mask是面积筛选之前的逐像素阈值结果，不能当作已验收缺陷Region集合。
        /// </summary>
        public static MRKExecutionPlan CreateCpuInspectionPlanWithArtifacts(
            CxVisionInspectionOptions options, long revision = 1)
        {
            return BuildCpuInspectionPlan(options, revision, true, false);
        }

        /// <summary>
        /// CASE-ARCH-01首个测量节点：保留处理图和筛选前Mask，另由独立MRK节点
        /// 对Inspect正式筛选的候选区域求像素面积。结果是px²而非未标定的mm²。
        /// 原有两个工厂签名和默认导出均不变。
        /// </summary>
        public static MRKExecutionPlan CreateCpuInspectionPlanWithMeasuredArtifacts(
            CxVisionInspectionOptions options, long revision = 1)
        {
            return BuildCpuInspectionPlan(options, revision, true, true);
        }

        /// <summary>
        /// Headless/single-image fast preview for WPF, CLI and SDK callers.
        /// Reuses the one MRK/MDS pipeline and its formal committed outputs; it is NOT
        /// a substitute for project-backed MEM/MOL/MOC compilation and publication.
        /// The caller owns source generation validation before presenting late results.
        /// </summary>
        public static async Task<CxVisionRunSummary> RunCpuInspectionPreviewAsync(
            FIMImageBuffer image, CxVisionInspectionOptions options, string sourceId,
            CancellationToken cancellation)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (options == null) throw new ArgumentNullException("options");
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("来源身份不能为空", "sourceId");
            cancellation.ThrowIfCancellationRequested();
            CxVisionPixelDepth depth = CxVisionImageInterop.GetPixelDepth(image);
            if (depth != options.PixelDepth)
                throw new ArgumentException("专业检测参数位深与FIM原图不一致", "options");
            var inputs = CxVisionImageInterop.ToCpuInspectionInputs(image, depth);
            var plan = CreateCpuInspectionPlanWithMeasuredArtifacts(options);
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(3,10));
            try
            {
                cancellation.ThrowIfCancellationRequested();
                MRKFlowHandle handle = engine.StartFlow(plan, inputs);
                using (cancellation.Register(() => handle.RequestCancel()))
                {
                    MRKFlowResult run = await handle.Completion.ConfigureAwait(false);
                    cancellation.ThrowIfCancellationRequested();
                    if (run.State != MRKFlowState.Completed)
                        throw new InvalidOperationException("MRK运行失败：" + run.State +
                            (run.Error == null ? "" : " / " + run.Error.Message));
                    return CxVisionRunSummary.FromCpuFlow(run, sourceId);
                }
            }
            finally { await engine.StopAsync().ConfigureAwait(false); }
        }

        private static MRKExecutionPlan BuildCpuInspectionPlan(
            CxVisionInspectionOptions options, long revision,
            bool publishArtifacts, bool publishMeasurements)
        {
            if (options == null) throw new ArgumentNullException("options");
            CxVisionThresholdPipeline pipeline = new CxVisionThresholdPipeline(options);
            CxVisionPixelDepth pixelDepth = options.PixelDepth;
            int threshold = options.GrayThreshold;
            if (pixelDepth != CxVisionPixelDepth.Bits8 &&
                pixelDepth != CxVisionPixelDepth.Bits16)
                throw new NotSupportedException("只支持8/16位灰度原始像素");
            Dictionary<string, Type> imageOutput = new Dictionary<string, Type>
            { { "Frame", typeof(CxVisionGrayFrame) } };
            // 增量专业产物只增加当前MRK计划的具名数据端口，仍由MDS统一捕获/预算。
            Dictionary<string, Type> preprocessOutputs =
                new Dictionary<string, Type>(imageOutput);
            Dictionary<string, Type> inspectOutputs = new Dictionary<string, Type>
            { { "Result", typeof(CxVisionInspectionResult) } };
            if (publishArtifacts)
            {
                preprocessOutputs.Add("Image", typeof(FIMImageBuffer));
                inspectOutputs.Add("Mask", typeof(FIMImageBuffer));
            }
            MRKExecutionInput[] rawInputs = {
                new MRKExecutionInput("Raw", MRKInputSource.FlowInput,
                    "RawPixels", typeof(FDTReadOnlyBuffer)),
                new MRKExecutionInput("Width", MRKInputSource.FlowInput,
                    "Width", typeof(int)),
                new MRKExecutionInput("Height", MRKInputSource.FlowInput,
                    "Height", typeof(int))
            };
            var steps = new List<MRKExecutionStep>
            {
                new MRKExecutionStep("Decode", new string[0], (ctx, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    // 注册当前Flow的专业安全类型；图像自身为真正不可变类型。
                    ctx.Data.Adapters.Register<CxVisionGrayFrame>(null,
                        immutable: true, kind: FDTDataKind.Image,
                        sizeEstimator: frame => checked((long)frame.Width * frame.Height * 2L));
                    if (publishArtifacts)
                        ctx.Data.Adapters.Register<FIMImageBuffer>(null,
                            immutable: true, kind: FDTDataKind.Image,
                            sizeEstimator: imagePixels => checked((long)imagePixels.Stride * imagePixels.Height));
                    ctx.Data.Adapters.Register<CxVisionInspectionResult>(
                        CopyResult, kind: FDTDataKind.Object,
                        sizeEstimator: result => checked(128L + result.Regions.Count * 64L));
                    var raw = ctx.Data.Get<FDTReadOnlyBuffer>("Raw");
                    int width = ctx.Data.Get<int>("Width");
                    int height = ctx.Data.Get<int>("Height");
                    int bytesPerPixel = pixelDepth == CxVisionPixelDepth.Bits16 ? 2 : 1;
                    if (width <= 0 || height <= 0 ||
                        (long)width * height * bytesPerPixel != raw.Length)
                        throw new ArgumentException("RAW图像尺寸、位深和字节数不符");
                    ushort[] pixels = new ushort[checked(width * height)];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        if ((i & 4095) == 0) ct.ThrowIfCancellationRequested();
                        pixels[i] = bytesPerPixel == 1 ? raw[i] :
                            (ushort)(raw[i * 2] | (raw[i * 2 + 1] << 8));
                    }
                    var image = new CxVisionGrayFrame(width, height,
                        pixelDepth, pixels);
                    ctx.Data.Return("Frame", image);
                    return Task.FromResult((object)image.Width);
                }, inputs: rawInputs, dataOutputs: imageOutput),

                new MRKExecutionStep("Preprocess", new [] { "Decode" }, (ctx, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    var image = ctx.Data.Get<CxVisionGrayFrame>("FrameInput");
                    // 真实逐像素背景清理；不修改Decode输出。
                    ushort[] filtered = new ushort[checked(image.Width * image.Height)];
                    for (int i = 0; i < filtered.Length; i++)
                    {
                        if ((i & 4095) == 0) ct.ThrowIfCancellationRequested();
                        filtered[i] = image.PixelAt(i) < 10 ? (ushort)0 : image.PixelAt(i);
                    }
                    var prepared = new CxVisionGrayFrame(image.Width, image.Height,
                        image.PixelDepth, filtered);
                    ctx.Data.Return("Frame", prepared);
                    if (publishArtifacts)
                    {
                        ct.ThrowIfCancellationRequested();
                        // FIM像素自持有；由专业转换已有像素，不暴露ushort[]可变别名。
                        ctx.Data.Return("Image", CxVisionImageInterop.ToImageBuffer(prepared));
                    }
                    return Task.FromResult((object)prepared.Width);
                }, inputs: new[] {
                    new MRKExecutionInput("FrameInput", MRKInputSource.StepOutput,
                        "Decode.Frame", typeof(CxVisionGrayFrame))
                }, dataOutputs: preprocessOutputs),

                new MRKExecutionStep("Inspect", new [] { "Preprocess" }, (ctx, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    var frame = ctx.Data.Get<CxVisionGrayFrame>("FrameInput");
                    CxVisionInspectionResult result = pipeline.Inspect(frame, revision, ct);
                    ct.ThrowIfCancellationRequested();
                    ctx.Data.Return("Result", result);
                    if (publishArtifacts)
                    {
                        // 与Inspection.ThresholdPixelCount相同语义：经Preprocess后的全图
                        // 阈值命中0/255 Mask，未应用缺陷区域的面积上下限。
                        byte[] mask = new byte[checked(frame.Width * frame.Height)];
                        int hits = 0;
                        for (int i = 0; i < mask.Length; ++i)
                        {
                            if ((i & 4095) == 0) ct.ThrowIfCancellationRequested();
                            if (frame.PixelAt(i) >= threshold) { mask[i] = 255; ++hits; }
                        }
                        if (hits != result.ThresholdPixelCount)
                            throw new InvalidOperationException("阈值Mask与检测像素数不一致");
                        ctx.Data.Return("Mask", new FIMImageBuffer(frame.Width,
                            frame.Height, FIMPixelFormat.Gray8, mask));
                    }
                    return Task.FromResult((object)result.Regions.Count);
                }, inputs: new[] {
                    new MRKExecutionInput("FrameInput", MRKInputSource.StepOutput,
                        "Preprocess.Frame", typeof(CxVisionGrayFrame))
                }, dataOutputs: inspectOutputs),

                new MRKExecutionStep("Statistics", new [] { "Preprocess" }, (ctx, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    var frame = ctx.Data.Get<CxVisionGrayFrame>("FrameInput");
                    long sum = 0;
                    for (int i = 0; i < frame.Width * frame.Height; i++)
                    {
                        if ((i & 4095) == 0) ct.ThrowIfCancellationRequested();
                        sum += frame.PixelAt(i);
                    }
                    ctx.Data.Return("GraySum", sum);
                    return Task.FromResult((object)sum);
                }, inputs: new[] {
                    new MRKExecutionInput("FrameInput", MRKInputSource.StepOutput,
                        "Preprocess.Frame", typeof(CxVisionGrayFrame))
                }, dataOutputs: new Dictionary<string, Type> {
                    { "GraySum", typeof(long) }
                }),

                new MRKExecutionStep("Decision", new [] { "Inspect", "Statistics" }, (ctx, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    var result = ctx.Data.Get<CxVisionInspectionResult>("Inspection");
                    long sum = ctx.Data.Get<long>("GraySumInput");
                    // 空图像不会被误判为成功的采图质量；检测通过与质量判断分开。
                    bool passed = result.Passed && sum > 0;
                    ctx.Data.Return("Passed", passed);
                    return Task.FromResult((object)passed);
                }, inputs: new [] {
                    new MRKExecutionInput("Inspection", MRKInputSource.StepOutput,
                        "Inspect.Result", typeof(CxVisionInspectionResult)),
                    new MRKExecutionInput("GraySumInput", MRKInputSource.StepOutput,
                        "Statistics.GraySum", typeof(long))
                }, dataOutputs: new Dictionary<string, Type> {
                    { "Passed", typeof(bool) }
                })
            };
            if (publishMeasurements)
            {
                steps.Add(new MRKExecutionStep("MeasureAcceptedRegions", new [] { "Inspect" },
                    (ctx, ct) =>
                    {
                        ct.ThrowIfCancellationRequested();
                        CxVisionInspectionResult result =
                            ctx.Data.Get<CxVisionInspectionResult>("AcceptedRegions");
                        long acceptedArea = MeasureAcceptedPixelArea(result, ct);
                        ctx.Data.Return("Area", acceptedArea);
                        return Task.FromResult((object)acceptedArea);
                    }, inputs: new[] {
                        new MRKExecutionInput("AcceptedRegions", MRKInputSource.StepOutput,
                            "Inspect.Result", typeof(CxVisionInspectionResult))
                    }, dataOutputs: new Dictionary<string, Type> {
                        { "Area", typeof(long) }
                    }));
            }
            Dictionary<string, string> exports = new Dictionary<string, string>
            {
                { "Inspection", "Inspect.Result" },
                { "GraySum", "Statistics.GraySum" },
                { "Passed", "Decision.Passed" }
            };
            if (publishArtifacts)
            {
                exports.Add("PreparedImage", "Preprocess.Image");
                exports.Add("ThresholdMask", "Inspect.Mask");
            }
            if (publishMeasurements)
                exports.Add("AcceptedRegionPixelArea", "MeasureAcceptedRegions.Area");
            return new MRKExecutionPlan("Vision.CpuInspection", steps, exports);
        }
    }
}
