using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Types;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Professional.Vision
{
    /// <summary>
    /// 真实CPU Vision算法通过既有MRK端口及MDS DataContext运行。
    /// 不新建调度器、数据仓库或模拟检测算法。源图像由安全只读Buffer持有。
    /// </summary>
    public static class CxVisionDataFlow
    {
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

        /// <summary>仅支持单通道8-bit RAW，不将SDK、AI或机台功能冒充已实现。</summary>
        public static MRKExecutionPlan CreateCpuInspectionPlan(
            CxVisionInspectionOptions options, long revision = 1)
        {
            if (options == null) throw new ArgumentNullException("options");
            CxVisionThresholdPipeline pipeline = new CxVisionThresholdPipeline(options);
            if (options.PixelDepth != CxVisionPixelDepth.Bits8)
                throw new NotSupportedException("本案例只支持8-bit原始像素");
            Dictionary<string, Type> imageOutput = new Dictionary<string, Type>
            { { "Frame", typeof(CxVisionGrayFrame) } };
            MRKExecutionInput[] rawInputs = {
                new MRKExecutionInput("Raw", MRKInputSource.FlowInput,
                    "RawPixels", typeof(FDTReadOnlyBuffer)),
                new MRKExecutionInput("Width", MRKInputSource.FlowInput,
                    "Width", typeof(int)),
                new MRKExecutionInput("Height", MRKInputSource.FlowInput,
                    "Height", typeof(int))
            };
            var steps = new []
            {
                new MRKExecutionStep("Decode", new string[0], (ctx, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    // 注册当前Flow的专业安全类型；图像自身为真正不可变类型。
                    ctx.Data.Adapters.Register<CxVisionGrayFrame>(null,
                        immutable: true, kind: FDTDataKind.Image);
                    ctx.Data.Adapters.Register<CxVisionInspectionResult>(
                        CopyResult, kind: FDTDataKind.Object);
                    var raw = ctx.Data.Get<FDTReadOnlyBuffer>("Raw");
                    int width = ctx.Data.Get<int>("Width");
                    int height = ctx.Data.Get<int>("Height");
                    if (width <= 0 || height <= 0 ||
                        (long)width * height != raw.Length)
                        throw new ArgumentException("RAW图像尺寸和字节数不符");
                    ushort[] pixels = new ushort[raw.Length];
                    for (int i = 0; i < pixels.Length; i++)
                        pixels[i] = raw[i];
                    var image = new CxVisionGrayFrame(width, height,
                        CxVisionPixelDepth.Bits8, pixels);
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
                        filtered[i] = image.PixelAt(i) < 10 ? (ushort)0 : image.PixelAt(i);
                    var prepared = new CxVisionGrayFrame(image.Width, image.Height,
                        image.PixelDepth, filtered);
                    ctx.Data.Return("Frame", prepared);
                    return Task.FromResult((object)prepared.Width);
                }, inputs: new[] {
                    new MRKExecutionInput("FrameInput", MRKInputSource.StepOutput,
                        "Decode.Frame", typeof(CxVisionGrayFrame))
                }, dataOutputs: imageOutput),

                new MRKExecutionStep("Inspect", new [] { "Preprocess" }, (ctx, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    var frame = ctx.Data.Get<CxVisionGrayFrame>("FrameInput");
                    CxVisionInspectionResult result = pipeline.Inspect(frame, revision);
                    ct.ThrowIfCancellationRequested();
                    ctx.Data.Return("Result", result);
                    return Task.FromResult((object)result.Regions.Count);
                }, inputs: new[] {
                    new MRKExecutionInput("FrameInput", MRKInputSource.StepOutput,
                        "Preprocess.Frame", typeof(CxVisionGrayFrame))
                }, dataOutputs: new Dictionary<string, Type> {
                    { "Result", typeof(CxVisionInspectionResult) }
                }),

                new MRKExecutionStep("Statistics", new [] { "Preprocess" }, (ctx, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    var frame = ctx.Data.Get<CxVisionGrayFrame>("FrameInput");
                    long sum = 0;
                    for (int i = 0; i < frame.Width * frame.Height; i++)
                        sum += frame.PixelAt(i);
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
            return new MRKExecutionPlan("Vision.CpuInspection", steps,
                new Dictionary<string, string> {
                    { "Inspection", "Inspect.Result" },
                    { "GraySum", "Statistics.GraySum" },
                    { "Passed", "Decision.Passed" }
                });
        }
    }
}
