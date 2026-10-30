using System;
using System.Collections.Generic;
using System.Linq;
using CxVisual.Foundation.Data.Types;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;
using CxVisual.Professional.Vision;

namespace CxVisual.Tests.ArchitectureSmoke
{
    /// <summary>使用真实MRK CPU检测值验证多类型结果投影、来源及只读契约。</summary>
    internal static class RuntimeVisionResultTests
    {
        private static void Check(bool good, string message)
        {
            if (!good) throw new InvalidOperationException("VISION_RESULT: " + message);
        }

        private static void Reject<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException("VISION_RESULT: " + message);
        }

        private static CxVisionInspectionOptions Options(int threshold)
        {
            return new CxVisionInspectionOptions {
                PixelDepth = CxVisionPixelDepth.Bits8, GrayThreshold = threshold,
                MinDefectArea = 2, MaxDefectArea = 100, UseAi = false
            };
        }

        private static IDictionary<string, object> Inputs(byte[] raw)
        {
            return new Dictionary<string, object> {
                { "RawPixels", new FDTReadOnlyBuffer(raw) },
                { "Width", 5 }, { "Height", 3 }
            };
        }

        private static byte[] Sample()
        {
            return new byte[] { 210,210,0,0,0, 210,0,0,0,0, 0,0,0,0,250 };
        }

        internal static void Run()
        {
            RealMrkResultProjection();
            EmptyAndFailedFlow();
            IndependentRunsAndStudioProjection();
        }

        private static void RealMrkResultProjection()
        {
            byte[] source = Sample();
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 10));
            try
            {
                MRKFlowResult flow = engine.StartFlow(
                    CxVisionDataFlow.CreateCpuInspectionPlan(Options(200), 68),
                    Inputs(source)).Completion.GetAwaiter().GetResult();
                Check(flow.State == MRKFlowState.Completed, "真实MRK检测未完成");
                CxVisionRunSummary summary =
                    CxVisionRunSummary.FromCpuFlow(flow, "Camera-A/frame-01");
                Check(summary.RunId == flow.FlowId && summary.CompletedUtc == flow.CompletedUtc &&
                      summary.ConfigRevision == 68 && summary.Width == 5 && summary.Height == 3 &&
                      summary.SourceId == "Camera-A/frame-01" && !summary.HasNoRegions,
                    "FlowId/SourceId/Revision/尺寸丢失");
                Check(summary.DefinitionRoiId==null &&
                      summary.DefinitionRevision==null &&
                      summary.ValidatedSourceHash==null,
                    "普通MRK预览不能伪造正式MEM ROI来源与Revision");
                Check(summary.ExecutionState==MRKFlowState.Completed &&
                      summary.AssessItemValidity(summary.Items[0],"Camera-A/frame-01")==
                          CxVisionResultValidity.Valid &&
                      summary.AssessItemValidity(summary.Items[0],"Camera-B/frame-01")==
                          CxVisionResultValidity.Stale,
                    "已提交Run完成态与来源错误的Item过期判定不一致");
                Check(summary.Items.Count == 5 &&
                      summary.Items.Select(i => i.ItemId).Distinct().Count() == 5 &&
                      summary.Items.All(i => i.SourceId == summary.SourceId &&
                                                i.RunId == flow.FlowId),
                    "具名证据项未与同一Run和Source关联");
                var region = summary.Items.Single(i => i.Kind == CxVisionResultKind.Region);
                Check(region.Region.PixelArea == 3 && region.Region.MinX == 0 &&
                      region.Region.MaxX == 1 && region.Region.MinY == 0 &&
                      region.Region.MaxY == 1, "真实区域快照不正确");
                Check(summary.Items.Single(i => i.Kind == CxVisionResultKind.DetectionSummary)
                          .IntegerValue == 1 &&
                      summary.Items.Single(i => i.Name == "阈值命中像素数").IntegerValue == 4 &&
                      summary.Items.Single(i => i.Name == "灰度累加").IntegerValue == 880 &&
                      summary.Items.Single(i => i.Kind == CxVisionResultKind.FlowDecision)
                          .BooleanValue == false, "多类型数值与MRK实际输出不同");
                Check(!object.ReferenceEquals(region.Region,
                    flow.GetCommittedOutput<CxVisionInspectionResult>("Inspection").Regions[0]),
                    "区域暴露可变的专业原始对象");
                var list = summary.Items as IList<CxVisionResultItem>;
                Check(list != null && list.IsReadOnly, "结果集合不是只读");
                Reject<NotSupportedException>(() => list.Clear(), "仍可变更ResultItem集合");
                source[0] = 0;
                Check(region.Region.PixelArea == 3 && flow.GetCommittedOutput<long>("GraySum") == 880,
                    "调用者更改输入后结果被污染");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void EmptyAndFailedFlow()
        {
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 10));
            try
            {
                MRKFlowResult clean = engine.StartFlow(
                    CxVisionDataFlow.CreateCpuInspectionPlan(Options(255), 69),
                    Inputs(Sample())).Completion.GetAwaiter().GetResult();
                var noRegions = CxVisionRunSummary.FromCpuFlow(clean, "Batch-A/Frame-02");
                Check(noRegions.ExecutionState==MRKFlowState.Completed &&
                      noRegions.AssessItemValidity(noRegions.Items.Single(i=>
                          i.Kind==CxVisionResultKind.DetectionSummary),
                          "Batch-A/Frame-02")==CxVisionResultValidity.Empty,
                    "成功无缺陷必须是Completed Run + Empty Item，不能冒充执行失败");
                Check(noRegions.AssessItemValidity(noRegions.Items.Single(i=>
                          i.Kind==CxVisionResultKind.Statistic),
                          "Batch-A/Frame-02")==CxVisionResultValidity.Valid &&
                      noRegions.AssessItemValidity(noRegions.Items.Single(i=>
                          i.Kind==CxVisionResultKind.DetectionSummary),
                          null)==CxVisionResultValidity.Stale,
                    "合法零统计值仍是Valid，缺失当前SourceId必须拒绝投影");
                Check(noRegions.HasNoRegions && noRegions.Items.Count == 4 &&
                      noRegions.Items.All(i => i.Kind != CxVisionResultKind.Region) &&
                      noRegions.Items.Single(i => i.Kind == CxVisionResultKind.DetectionSummary)
                          .IntegerValue == 0, "无缺陷应产生合法的空区域结果");
                MRKFlowResult failed = engine.StartFlow(
                    CxVisionDataFlow.CreateCpuInspectionPlan(Options(200), 70),
                    Inputs(new byte[] { 1, 2 })).Completion.GetAwaiter().GetResult();
                Check(failed.State == MRKFlowState.Failed &&
                      failed.CommittedOutputs.Count == 0, "损坏输入仍被提交");
                Reject<InvalidOperationException>(() =>
                    CxVisionRunSummary.FromCpuFlow(failed, "broken"),
                    "失败的MRK结果仍生成有效专业结果");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }

        private static void IndependentRunsAndStudioProjection()
        {
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(3, 10));
            try
            {
                MRKExecutionPlan plan = CxVisionDataFlow.CreateCpuInspectionPlan(Options(200), 71);
                MRKFlowResult a = engine.StartFlow(plan, Inputs(Sample()))
                    .Completion.GetAwaiter().GetResult();
                MRKFlowResult b = engine.StartFlow(plan, Inputs(Sample()))
                    .Completion.GetAwaiter().GetResult();
                var x = CxVisionRunSummary.FromCpuFlow(a, "same-source");
                var y = CxVisionRunSummary.FromCpuFlow(b, "same-source");
                Check(x.AssessItemValidity(y.Items[0],"same-source")==
                          CxVisionResultValidity.Invalid &&
                      x.AssessItemValidity(null,"same-source")==
                          CxVisionResultValidity.Invalid,
                    "同源不同Run或空Item不得被视为有效的当前Run产物");
                Check(x.RunId != y.RunId && x.Items[0].ItemId != y.Items[0].ItemId,
                    "同源重跑必须生成独立RunId与ResultItemId");

                var studio = CxVisionRunSummary.FromInspection(a.FlowId, "file-A.raw",
                    a.GetCommittedOutput<CxVisionInspectionResult>("Inspection"),
                    planFingerprint: "a-fixed-plan",
                    capabilityVersion: "provider-v1", completedUtc: a.CompletedUtc);
                Check(studio.Items.Count == 3 && studio.PlanFingerprint == "a-fixed-plan" &&
                      studio.CapabilityVersion == "provider-v1" &&
                      studio.Items.All(i => i.OutputPort == "Inspection") &&
                      !studio.Items.Any(i => i.Kind == CxVisionResultKind.FlowDecision),
                    "MOC/Studio只有Inspection输出时不得伪造GraySum/Passed");
                Reject<ArgumentException>(() =>
                    CxVisionRunSummary.FromInspection(Guid.Empty, "a",
                        a.GetCommittedOutput<CxVisionInspectionResult>("Inspection")),
                    "空RunId未拒绝");
                Reject<ArgumentException>(() => CxVisionRunSummary.FromCpuFlow(a, "  "),
                    "没有来源标识仍获得可追踪结果");
                Reject<ArgumentNullException>(() =>
                    CxVisionRunSummary.FromInspection(a.FlowId, "a", null),
                    "null检测结果未拒绝");
                Reject<ArgumentException>(() =>
                    CxVisionRunSummary.FromInspection(a.FlowId, "a",
                        a.GetCommittedOutput<CxVisionInspectionResult>("Inspection"),
                        completedUtc: DateTime.Now), "非UTC时间未拒绝");
                Reject<ArgumentNullException>(() =>
                    CxVisionRunSummary.FromCpuFlow(null, "a"), "空MRK对象未拒绝");
            }
            finally { engine.StopAsync().GetAwaiter().GetResult(); }
        }
    }
}
