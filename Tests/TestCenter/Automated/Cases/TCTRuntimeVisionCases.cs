using System;
using System.Collections.Generic;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Vision;
using CxVisual.TestCenter.Core;
using CxVisual.TestCenter.Discovery;

namespace CxVisual.TestCenter.Cases
{
    public static class TCTRuntimeVisionCases
    {
        private static CxVisionInspectionOptions Configuration(CxVisionPixelDepth depth, int threshold)
        {
            return new CxVisionInspectionOptions {
                PixelDepth = depth, GrayThreshold = threshold,
                MinDefectArea = 2, MaxDefectArea = 100, UseAi = false
            };
        }
        private static IDictionary<string, object> Input(byte[] bytes, int width, int height)
        {
            return new Dictionary<string,object> {
                {"RawPixels", new FDTReadOnlyBuffer(bytes)},{"Width",width},{"Height",height}
            };
        }

        [TCTTest("TCT.MDS.023", "真实8位图像经过MRK五节点、并行统计和最终判定",
            TCTTestAspect.Mechanism, "Data / Vision", ObjectId = "CxVisionDataFlow")]
        public static void VisionReal8BitFlow(TCTTestContext t)
        {
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 12));
            try
            {
                var plan = CxVisionDataFlow.CreateCpuInspectionPlan(
                    Configuration(CxVisionPixelDepth.Bits8, 200), 31);
                byte[] source = { 210,210,0,0,0,210,0,0,0,0,0,0,0,0,250 };
                MRKFlowResult result = engine.StartFlow(plan, Input(source,5,3))
                    .Completion.GetAwaiter().GetResult();
                if (result.State != MRKFlowState.Completed)
                    throw new InvalidOperationException("Vision流程失败：" +
                        (result.Error == null ? "" : result.Error.ToString()));
                CxVisionInspectionResult inspected =
                    result.GetCommittedOutput<CxVisionInspectionResult>("Inspection");
                t.AssertEqual(3, inspected.Regions[0].PixelArea, "真实连通域面积");
                t.AssertEqual(4, inspected.ThresholdPixelCount, "真实阈值像素统计");
                t.AssertEqual(880L, result.GetCommittedOutput<long>("GraySum"), "并行图像统计");
                t.AssertEqual(false, result.GetCommittedOutput<bool>("Passed"), "检测判定");
                t.AssertEqual(31L, inspected.ConfigRevision, "运行结果保留配置版本");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        [TCTTest("TCT.MDS.024", "真实16位小端RAW图像与错误输入拒绝",
            TCTTestAspect.Mechanism, "Data / Vision", ObjectId = "CxVisionDataFlow")]
        public static void VisionReal16BitFlow(TCTTestContext t)
        {
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 12));
            try
            {
                var plan = CxVisionDataFlow.CreateCpuInspectionPlan(
                    Configuration(CxVisionPixelDepth.Bits16, 400));
                byte[] source = { 0xF4,0x01,0xF4,0x01,0,0,0,0 };
                MRKFlowResult valid = engine.StartFlow(plan, Input(source,2,2))
                    .Completion.GetAwaiter().GetResult();
                t.AssertEqual(MRKFlowState.Completed, valid.State, "16位真实CPU检测执行完成");
                t.AssertEqual(1000L, valid.GetCommittedOutput<long>("GraySum"), "16位像素和");
                t.AssertEqual(2, valid.GetCommittedOutput<CxVisionInspectionResult>("Inspection")
                    .Regions[0].PixelArea, "16位连通域检测");
                MRKFlowResult invalid = engine.StartFlow(plan, Input(new byte[]{1,2,3},2,2))
                    .Completion.GetAwaiter().GetResult();
                t.AssertEqual(MRKFlowState.Failed, invalid.State, "截断数据必须失败");
                t.AssertEqual(0, invalid.CommittedOutputs.Count, "失败不得导出半成品");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        [TCTTest("TCT.MDS.025", "真实Vision受Flow数据预算限制，超额不能发布部分Output",
            TCTTestAspect.Mechanism, "Data / Vision", ObjectId = "MDSDataBudget")]
        public static void VisionQuotaEndToEnd(TCTTestContext t)
        {
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 10),
                dataPayloadBudgetBytes: 48);
            try
            {
                var plan = CxVisionDataFlow.CreateCpuInspectionPlan(
                    Configuration(CxVisionPixelDepth.Bits8, 200));
                MRKFlowResult result = engine.StartFlow(plan,
                    Input(new byte[]{250,250,0,0,0,0},3,2))
                    .Completion.GetAwaiter().GetResult();
                t.AssertEqual(MRKFlowState.Failed, result.State, "超预算必须失败");
                t.AssertEqual(0, result.CommittedOutputs.Count, "预算不足无部分结果");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }
    }
}
