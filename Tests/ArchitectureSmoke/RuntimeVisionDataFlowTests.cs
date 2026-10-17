using System;
using System.Collections.Generic;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Vision;

namespace CxVisual.Tests.ArchitectureSmoke
{
    internal static class RuntimeVisionDataFlowTests
    {
        private static void Check(bool good, string why)
        { if (!good) throw new InvalidOperationException("VISION_MDS: " + why); }

        private static CxVisionInspectionOptions Options(int threshold)
        {
            return new CxVisionInspectionOptions {
                PixelDepth = CxVisionPixelDepth.Bits8,
                GrayThreshold = threshold,
                MinDefectArea = 2,
                MaxDefectArea = 100,
                UseAi = false
            };
        }

        private static IDictionary<string, object> Inputs(byte[] raw, int width = 5, int height = 3)
        {
            return new Dictionary<string, object> {
                {"RawPixels", new FDTReadOnlyBuffer(raw)},
                {"Width", width}, {"Height", height}
            };
        }

        internal static void Run()
        {
            RealPixelDetectionAndFanout();
            RealPixelNoDefectAndFailure();
            SixteenBitRealPixelInspection();
            ConcurrentFlowOwnershipIsolation();
        }

        private static void RealPixelDetectionAndFanout()
        {
            byte[] source = { 210, 210, 0, 0, 0, 210, 0, 0, 0, 0, 0, 0, 0, 0, 250 };
            MRKExecutionPlan plan = CxVisionDataFlow.CreateCpuInspectionPlan(Options(200), 17);
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 16));
            try
            {
                var handle = engine.StartFlow(plan, Inputs(source));
                source[0] = 0;
                MRKFlowResult result = handle.Completion.GetAwaiter().GetResult();
                Check(result.State == MRKFlowState.Completed,
                    "CPU检测流程实际失败：" + (result.Error == null ? "" : result.Error.ToString()));
                CxVisionInspectionResult inspected =
                    result.GetCommittedOutput<CxVisionInspectionResult>("Inspection");
                Check(inspected.Width == 5 && inspected.Height == 3 &&
                    inspected.ConfigRevision == 17 &&
                    inspected.ThresholdPixelCount == 4,
                    "真实逐像素计算输出不符合预期");
                Check(inspected.Regions.Count == 1 && inspected.Regions[0].PixelArea == 3,
                    "四邻接连通域检测未正确执行");
                Check(result.GetCommittedOutput<long>("GraySum") == 880 &&
                    !result.GetCommittedOutput<bool>("Passed"),
                    "并行统计与最终判定未正确汇合");
                // 输出须是深度隔离的专业结果，而非共享一个可变缺陷Region。
                CxVisionInspectionResult repeated =
                    result.GetCommittedOutput<CxVisionInspectionResult>("Inspection");
                Check(!object.ReferenceEquals(inspected, repeated) &&
                    !object.ReferenceEquals(inspected.Regions[0], repeated.Regions[0]) &&
                    repeated.Regions[0].PixelArea == 3,
                    "专业检测结果出现可变别名");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void RealPixelNoDefectAndFailure()
        {
            byte[] source = { 210, 210, 0, 0, 0, 210, 0, 0, 0, 0, 0, 0, 0, 0, 250 };
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 16));
            try
            {
                var clean = engine.StartFlow(
                    CxVisionDataFlow.CreateCpuInspectionPlan(Options(230)), Inputs(source))
                    .Completion.GetAwaiter().GetResult();
                Check(clean.State == MRKFlowState.Completed &&
                    clean.GetCommittedOutput<CxVisionInspectionResult>("Inspection").Passed &&
                    clean.GetCommittedOutput<bool>("Passed"),
                    "切换阈值后真实检测结果未按算法变化");
                var invalid = engine.StartFlow(
                    CxVisionDataFlow.CreateCpuInspectionPlan(Options(200)),
                    Inputs(new byte[] { 1, 2 }, 5, 3))
                    .Completion.GetAwaiter().GetResult();
                Check(invalid.State == MRKFlowState.Failed &&
                    invalid.CommittedOutputs.Count == 0,
                    "无效图像尺寸仍允许导出半成品");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void SixteenBitRealPixelInspection()
        {
            // 500/500的两个相邻灰度像素，真实16位Little-Endian RAW。
            byte[] raw = { 0xF4, 0x01, 0xF4, 0x01, 0, 0, 0, 0 };
            var options = new CxVisionInspectionOptions {
                PixelDepth = CxVisionPixelDepth.Bits16, GrayThreshold = 400,
                MinDefectArea = 2, MaxDefectArea = 100, UseAi = false
            };
            var plan = CxVisionDataFlow.CreateCpuInspectionPlan(options, 23);
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 10));
            try
            {
                MRKFlowResult result = engine.StartFlow(plan, Inputs(raw, 2, 2))
                    .Completion.GetAwaiter().GetResult();
                Check(result.State == MRKFlowState.Completed,
                    "16位CPU Vision失败：" + (result.Error == null ? "" : result.Error.ToString()));
                CxVisionInspectionResult inspected =
                    result.GetCommittedOutput<CxVisionInspectionResult>("Inspection");
                Check(inspected.ConfigRevision == 23 &&
                    inspected.Regions.Count == 1 && inspected.Regions[0].PixelArea == 2 &&
                    inspected.ThresholdPixelCount == 2 &&
                    result.GetCommittedOutput<long>("GraySum") == 1000,
                    "16位RAW解码或真实阈值算法返回错误");
                MRKFlowResult bad = engine.StartFlow(plan, Inputs(new byte[]{0xF4,0x01,0xF4}, 2,2))
                    .Completion.GetAwaiter().GetResult();
                Check(bad.State == MRKFlowState.Failed && bad.CommittedOutputs.Count == 0,
                    "16位截断文件仍导出了未完成结果");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void ConcurrentFlowOwnershipIsolation()
        {
            MRKExecutionPlan plan = CxVisionDataFlow.CreateCpuInspectionPlan(Options(200));
            MRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 16));
            try
            {
                var a = engine.StartFlow(plan, Inputs(
                    new byte[] { 255, 255, 0, 0, 0, 0 }, 3, 2));
                var b = engine.StartFlow(plan, Inputs(
                    new byte[] { 0, 0, 0, 0, 0, 0 }, 3, 2));
                var ra = a.Completion.GetAwaiter().GetResult();
                var rb = b.Completion.GetAwaiter().GetResult();
                Check(ra.State == MRKFlowState.Completed &&
                    rb.State == MRKFlowState.Completed &&
                    ra.FlowId != rb.FlowId &&
                    ra.GetCommittedOutput<CxVisionInspectionResult>("Inspection").Regions.Count == 1 &&
                    rb.GetCommittedOutput<CxVisionInspectionResult>("Inspection").Regions.Count == 0,
                    "并行真实图像Flow发生跨Run数据污染");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }
    }
}
