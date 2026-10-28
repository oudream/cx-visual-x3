using System;
using System.Collections.Generic;
using System.Globalization;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Professional.Vision
{
    /// <summary>现有CPU检测结果实际可提供的类型；流程标志并非产品级OK/NG。</summary>
    public enum CxVisionResultKind { DetectionSummary, Region, Statistic, FlowDecision }

    /// <summary>不可变候选区域快照，不泄漏原CxVisionDefectRegion的可变属性。</summary>
    public sealed class CxVisionRegionSnapshot
    {
        public int PixelArea { get; private set; }
        public int MinX { get; private set; }
        public int MinY { get; private set; }
        public int MaxX { get; private set; }
        public int MaxY { get; private set; }

        internal CxVisionRegionSnapshot(CxVisionDefectRegion region, int width, int height)
        {
            if (region == null || region.MinX < 0 || region.MinY < 0 ||
                region.MaxX < region.MinX || region.MaxY < region.MinY ||
                region.MaxX >= width || region.MaxY >= height ||
                region.PixelArea <= 0 ||
                region.PixelArea > (long)(region.MaxX - region.MinX + 1) *
                                   (region.MaxY - region.MinY + 1))
                throw new ArgumentException("检测候选区域边界或面积不合法", "region");
            PixelArea = region.PixelArea;
            MinX = region.MinX;
            MinY = region.MinY;
            MaxX = region.MaxX;
            MaxY = region.MaxY;
        }
    }

    /// <summary>每次Run中稳定可引用的只读小值项；无object/大图像持久副本。</summary>
    public sealed class CxVisionResultItem
    {
        public string ItemId { get; private set; }
        public Guid RunId { get; private set; }
        public string SourceId { get; private set; }
        public string OutputPort { get; private set; }
        public CxVisionResultKind Kind { get; private set; }
        public string Name { get; private set; }
        public string Unit { get; private set; }
        public long? IntegerValue { get; private set; }
        public bool? BooleanValue { get; private set; }
        public CxVisionRegionSnapshot Region { get; private set; }

        internal CxVisionResultItem(Guid runId, string sourceId, string suffix,
            string outputPort, CxVisionResultKind kind, string name, string unit,
            long? number = null, bool? boolean = null, CxVisionRegionSnapshot region = null)
        {
            RunId = runId;
            SourceId = sourceId;
            ItemId = runId.ToString("N") + ":" + suffix;
            OutputPort = outputPort;
            Kind = kind;
            Name = name;
            Unit = unit;
            IntegerValue = number;
            BooleanValue = boolean;
            Region = region;
        }
    }

    /// <summary>
    /// 现有MRK及MEM/MOC CPU检测的轻量只读结果投影。
    /// 不取代MRKFlowResult、MDS DataStack或工程/Artifact存储；未实现Mask/Image结果。
    /// </summary>
    public sealed class CxVisionRunSummary
    {
        public Guid RunId { get; private set; }
        public string SourceId { get; private set; }
        public DateTime? CompletedUtc { get; private set; }
        public long ConfigRevision { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public string PlanFingerprint { get; private set; }
        public string CapabilityVersion { get; private set; }
        public bool HasNoRegions { get; private set; }
        public IReadOnlyList<CxVisionResultItem> Items { get; private set; }

        private CxVisionRunSummary() { }

        /// <summary>直接消费真实MRK成功提交的Inspection/GraySum/Passed；失败不冒充结果。</summary>
        public static CxVisionRunSummary FromCpuFlow(MRKFlowResult flow, string sourceId)
        {
            if (flow == null) throw new ArgumentNullException("flow");
            if (flow.State != MRKFlowState.Completed)
                throw new InvalidOperationException("MRK未完成，不能投影Pending数据");
            return FromInspection(flow.FlowId, sourceId,
                flow.GetCommittedOutput<CxVisionInspectionResult>("Inspection"),
                flow.GetCommittedOutput<long>("GraySum"),
                flow.GetCommittedOutput<bool>("Passed"),
                completedUtc: flow.CompletedUtc);
        }

        /// <summary>
        /// 兼容Studio通过MOC编译得到的检测StepResult：只记录真实存在的可选输出。
        /// 没有GraySum/Passed导出时不从Inspection.Passed伪造软件流程级标志。
        /// </summary>
        public static CxVisionRunSummary FromInspection(Guid runId, string sourceId,
            CxVisionInspectionResult inspection, long? graySum = null,
            bool? flowPassed = null, string planFingerprint = null,
            string capabilityVersion = null, DateTime? completedUtc = null)
        {
            if (runId == Guid.Empty) throw new ArgumentException("必须有真实FlowId", "runId");
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("源身份不能为空", "sourceId");
            if (inspection == null) throw new ArgumentNullException("inspection");
            if (inspection.Width <= 0 || inspection.Height <= 0 || inspection.Regions == null ||
                inspection.ThresholdPixelCount < 0 ||
                inspection.ThresholdPixelCount > (long)inspection.Width * inspection.Height)
                throw new ArgumentException("检测结果尺寸或计数不合法", "inspection");
            if (completedUtc.HasValue && completedUtc.Value.Kind != DateTimeKind.Utc)
                throw new ArgumentException("完成时间须为UTC", "completedUtc");

            string source = sourceId.Trim();
            var values = new List<CxVisionResultItem>();
            values.Add(new CxVisionResultItem(runId, source, "Inspection.Summary",
                "Inspection", CxVisionResultKind.DetectionSummary, "候选区域数", "count",
                number: inspection.Regions.Count));
            values.Add(new CxVisionResultItem(runId, source, "Inspection.ThresholdPixelCount",
                "Inspection", CxVisionResultKind.Statistic, "阈值命中像素数", "px",
                number: inspection.ThresholdPixelCount));
            for (int i = 0; i < inspection.Regions.Count; ++i)
                values.Add(new CxVisionResultItem(runId, source,
                    "Inspection.Region." + i.ToString(CultureInfo.InvariantCulture),
                    "Inspection", CxVisionResultKind.Region, "检测候选区域", "px",
                    region: new CxVisionRegionSnapshot(inspection.Regions[i],
                        inspection.Width, inspection.Height)));
            if (graySum.HasValue)
                values.Add(new CxVisionResultItem(runId, source, "GraySum",
                    "GraySum", CxVisionResultKind.Statistic, "灰度累加", "gray-sum",
                    number: graySum.Value));
            if (flowPassed.HasValue)
                values.Add(new CxVisionResultItem(runId, source, "Passed", "Passed",
                    CxVisionResultKind.FlowDecision, "CPU流程标志（非产品质量判定）",
                    null, boolean: flowPassed.Value));

            return new CxVisionRunSummary
            {
                RunId = runId, SourceId = source, CompletedUtc = completedUtc,
                ConfigRevision = inspection.ConfigRevision,
                Width = inspection.Width, Height = inspection.Height,
                PlanFingerprint = planFingerprint, CapabilityVersion = capabilityVersion,
                HasNoRegions = inspection.Regions.Count == 0,
                Items = values.AsReadOnly()
            };
        }
    }
}
