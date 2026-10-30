using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using CxVisual.Foundation.Geometry;
using CxVisual.Foundation.Imaging;
using CxVisual.Mechanism.Runtime.Execution;

namespace CxVisual.Professional.Vision
{
    /// <summary>现有CPU检测结果实际可提供的类型；流程标志并非产品级OK/NG。</summary>
    public enum CxVisionResultKind { DetectionSummary, Region, Statistic, FlowDecision, Image, Mask, Measurement, Contour }

    /// <summary>
    /// 结果项对于某一当前视图/工程上下文的可用状态；不等于MRK Flow执行状态。
    /// 空候选集合仍可来自成功完成的Run；Stale不修改历史Run事实。
    /// </summary>
    public enum CxVisionResultValidity { Valid, Empty, Stale, MissingArtifact, Invalid }

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

    /// <summary>
    /// 从真实FIM二值Mask提取的一个闭合像素边缘轮廓快照。正面积为外环、负面积为孔洞；
    /// 顶点属于原图像素网格边界，不是像素中心或毫米坐标。
    /// </summary>
    public sealed class CxVisionContourSnapshot
    {
        public IReadOnlyList<FGEPoint2D> Points { get; private set; }
        public double SignedAreaPixels { get; private set; }
        public bool IsHole { get { return SignedAreaPixels < 0; } }

        internal CxVisionContourSnapshot(IReadOnlyList<FGEPoint2D> points)
        {
            if (points == null) throw new ArgumentNullException("points");
            var copy = new List<FGEPoint2D>(points);
            SignedAreaPixels = FIMImageMaskContours.SignedArea(copy);
            if (SignedAreaPixels == 0)
                throw new ArgumentException("轮廓面积不能为零。", "points");
            Points = copy.AsReadOnly();
        }
    }

    /// <summary>
    /// 每次Run的具名专业结果项。图像使用自持有不可变FIM像素快照，
    /// 只支持本Run内临时引用，不是磁盘Artifact或持久ImageSet。
    /// </summary>
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
        /// <summary>仅Kind为Image/Mask时存在；FIM公开像素访问均不可修改内部数据。</summary>
        public FIMImageBuffer Image { get; private set; }
        /// <summary>仅Kind为Contour时存在，来自同Run的阈值Mask像素边缘。</summary>
        public CxVisionContourSnapshot Contour { get; private set; }

        internal CxVisionResultItem(Guid runId, string sourceId, string suffix,
            string outputPort, CxVisionResultKind kind, string name, string unit,
            long? number = null, bool? boolean = null, CxVisionRegionSnapshot region = null,
            FIMImageBuffer image = null, CxVisionContourSnapshot contour = null)
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
            Image = image;
            Contour = contour;
        }
    }

    /// <summary>
    /// 现有MRK及MEM/MOC CPU检测的轻量只读结果投影。
    /// 不取代MRKFlowResult、MDS DataStack或工程/Artifact存储；
    /// 仅显式开启的CPU计划能携带Run内的Image/Mask内存产物。
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

        /// <summary>
        /// 仅正式ROI验证过的原图内容指纹；普通预览为null。
        /// SHA256基于有效像素紧排字节，不代表持久Artifact地址。
        /// </summary>
        public string ValidatedSourceHash { get; private set; }
        public Guid? DefinitionRoiId { get; private set; }
        /// <summary>正式工程运行时的MEM Revision，区别于算法ConfigRevision。</summary>
        public int? DefinitionRevision { get; private set; }

        /// <summary>
        /// CxVisionRunSummary仅在MRK已成功提交后构造；失败、取消或拒绝由
        /// 原MRKFlowResult.State/Trace记录，不伪造有产物的失败Run。
        /// </summary>
        public MRKFlowState ExecutionState { get { return MRKFlowState.Completed; } }

        private CxVisionRunSummary() { }

        /// <summary>
        /// 只读检查某一Run结果是否可投影到当前图像/工程。
        /// 在外部来源身份未确认时从严返回Stale；不变更旧Item或MRK输出。
        /// 当前字节型结果随Run在内存中存在，未引入磁盘Artifact仓库。
        /// </summary>
        public CxVisionResultValidity AssessItemValidity(CxVisionResultItem item,
            string currentSourceId,string validatedSourceHash = null,
            int? currentDefinitionRevision = null)
        {
            if(item==null || Items==null ||
               !System.Linq.Enumerable.Any(Items,x=>Object.ReferenceEquals(x,item)) ||
               item.RunId!=RunId || item.SourceId!=SourceId)
                return CxVisionResultValidity.Invalid;
            if(string.IsNullOrWhiteSpace(currentSourceId) ||
               !string.Equals(currentSourceId,SourceId,StringComparison.Ordinal) ||
               (ValidatedSourceHash!=null &&
                !string.Equals(ValidatedSourceHash,validatedSourceHash,StringComparison.Ordinal)) ||
               (DefinitionRevision.HasValue &&
                (!currentDefinitionRevision.HasValue ||
                 DefinitionRevision.Value!=currentDefinitionRevision.Value)))
                return CxVisionResultValidity.Stale;
            if(((item.Kind==CxVisionResultKind.Image ||
                 item.Kind==CxVisionResultKind.Mask) && item.Image==null) ||
               (item.Kind==CxVisionResultKind.Contour && item.Contour==null))
                return CxVisionResultValidity.MissingArtifact;
            if(item.Kind==CxVisionResultKind.DetectionSummary &&
               item.IntegerValue.HasValue && item.IntegerValue.Value==0)
                return CxVisionResultValidity.Empty;
            return CxVisionResultValidity.Valid;
        }

        /// <summary>在已验证正式工程边界生成独立只读证据，不篡改原Run结果。</summary>
        internal CxVisionRunSummary WithValidatedRoiEvidence(Guid roiId,
            string sourceHash,int definitionRevision)
        {
            if(roiId==Guid.Empty)throw new ArgumentException("ROI身份不能为空","roiId");
            if(definitionRevision<0)throw new ArgumentOutOfRangeException("definitionRevision");
            if(string.IsNullOrEmpty(sourceHash)||sourceHash.Length!=64 ||
               !System.Linq.Enumerable.All(sourceHash,
                   c=>(c>='0'&&c<='9')||(c>='a'&&c<='f')))
                throw new ArgumentException("原图SHA256必须为64位小写十六进制","sourceHash");
            if(RunId==Guid.Empty||Items==null || string.IsNullOrWhiteSpace(SourceId))
                throw new InvalidOperationException("vision.roi.run_evidence_invalid");
            return new CxVisionRunSummary{
                RunId=RunId,SourceId=SourceId,CompletedUtc=CompletedUtc,
                ConfigRevision=ConfigRevision,Width=Width,Height=Height,
                PlanFingerprint=PlanFingerprint,CapabilityVersion=CapabilityVersion,
                HasNoRegions=HasNoRegions,Items=Items,
                DefinitionRoiId=roiId,DefinitionRevision=definitionRevision,
                ValidatedSourceHash=sourceHash
            };
        }

        /// <summary>
        /// 按需将已提交的ThresholdMask提取为Contour结果项。默认结果不自动扫描大Mask；
        /// 不更改原Run、MRK输出、原始Image或结果集合。面积是原图像素边界带符号面积，
        /// 不能混同Inspection过滤后的接受区域面积，也没有校准单位。
        /// </summary>
        public CxVisionRunSummary WithThresholdMaskContours(int maxBoundaryEdges = 100000,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (maxBoundaryEdges < 4 || maxBoundaryEdges > 2000000)
                throw new ArgumentOutOfRangeException("maxBoundaryEdges");
            cancellationToken.ThrowIfCancellationRequested();
            CxVisionResultItem mask = null;
            foreach (CxVisionResultItem item in Items)
            {
                if (item.Kind == CxVisionResultKind.Contour &&
                    item.OutputPort == "ThresholdMask") return this;
                if (item.Kind == CxVisionResultKind.Mask &&
                    item.OutputPort == "ThresholdMask")
                {
                    if (mask != null)
                        throw new InvalidOperationException("同一Run包含多个ThresholdMask结果项");
                    mask = item;
                }
            }
            if (mask == null || mask.Image == null)
                throw new InvalidOperationException("此Run没有已提交的ThresholdMask，不能生成轮廓");
            var loops = FIMImageMaskContours.Trace(mask.Image, maxBoundaryEdges, cancellationToken);
            var items = new List<CxVisionResultItem>(Items);
            for (int i = 0; i < loops.Count; ++i)
            {
                cancellationToken.ThrowIfCancellationRequested();
                items.Add(new CxVisionResultItem(RunId, SourceId,
                    "ThresholdMask.Contour." + i.ToString(CultureInfo.InvariantCulture),
                    "ThresholdMask", CxVisionResultKind.Contour,
                    "阈值Mask像素边界轮廓", "px²",
                    contour: new CxVisionContourSnapshot(loops[i])));
            }
            return new CxVisionRunSummary
            {
                RunId = RunId, SourceId = SourceId, CompletedUtc = CompletedUtc,
                ConfigRevision = ConfigRevision, Width = Width, Height = Height,
                PlanFingerprint = PlanFingerprint, CapabilityVersion = CapabilityVersion,
                HasNoRegions = HasNoRegions, Items = items.AsReadOnly(),
                DefinitionRoiId=DefinitionRoiId,DefinitionRevision=DefinitionRevision,
                ValidatedSourceHash=ValidatedSourceHash
            };
        }

        /// <summary>直接消费真实MRK成功提交的Inspection/GraySum/Passed；失败不冒充结果。</summary>
        public static CxVisionRunSummary FromCpuFlow(MRKFlowResult flow, string sourceId)
        {
            if (flow == null) throw new ArgumentNullException("flow");
            if (flow.State != MRKFlowState.Completed)
                throw new InvalidOperationException("MRK未完成，不能投影Pending数据");
            CxVisionRunSummary result = FromInspection(flow.FlowId, sourceId,
                flow.GetCommittedOutput<CxVisionInspectionResult>("Inspection"),
                flow.GetCommittedOutput<long>("GraySum"),
                flow.GetCommittedOutput<bool>("Passed"),
                completedUtc: flow.CompletedUtc);
            // 只从MRK正式提交出口取值；失败/取消时不会读任何Pending产物。
            // FIMImageBuffer持有只读像素，MDS现有FDT Adapter对其计费。
            IDictionary<string, object> exports = flow.CommittedOutputs;
            object prepared, mask;
            bool hasImage = exports.TryGetValue("PreparedImage", out prepared);
            bool hasMask = exports.TryGetValue("ThresholdMask", out mask);
            if (hasImage != hasMask)
                throw new InvalidOperationException("处理图与阈值Mask必须成对正式提交");
            if (hasImage)
            {
                FIMImageBuffer image = prepared as FIMImageBuffer;
                FIMImageBuffer binary = mask as FIMImageBuffer;
                if (image == null || binary == null ||
                    (image.PixelFormat != FIMPixelFormat.Gray8 &&
                     image.PixelFormat != FIMPixelFormat.Gray16) ||
                    binary.PixelFormat != FIMPixelFormat.Gray8 ||
                    image.Width != result.Width || image.Height != result.Height ||
                    binary.Width != result.Width || binary.Height != result.Height)
                    throw new InvalidOperationException("CPU图像产物格式、尺寸或类型不匹配");
                var items = new List<CxVisionResultItem>(result.Items);
                items.Add(new CxVisionResultItem(result.RunId, result.SourceId,
                    "PreparedImage", "PreparedImage", CxVisionResultKind.Image,
                    "预处理灰度图", null, image: image));
                items.Add(new CxVisionResultItem(result.RunId, result.SourceId,
                    "ThresholdMask", "ThresholdMask", CxVisionResultKind.Mask,
                    "阈值二值Mask（面积筛选前）", null, image: binary));
                result.Items = items.AsReadOnly();
            }
            object measurement;
            if (exports.TryGetValue("AcceptedRegionPixelArea", out measurement))
            {
                if (!hasImage || !(measurement is long))
                    throw new InvalidOperationException("缺陷面积测量必须来自带图像与Mask的正式MRK计划");
                long area = (long)measurement;
                long regionTotal = 0;
                foreach (CxVisionResultItem item in result.Items)
                    if (item.Kind == CxVisionResultKind.Region)
                        regionTotal = checked(regionTotal + item.Region.PixelArea);
                CxVisionResultItem pixelHits = null;
                foreach (CxVisionResultItem item in result.Items)
                    if (item.Name == "阈值命中像素数") { pixelHits = item; break; }
                if (area < 0 || area > (long)result.Width * result.Height ||
                    pixelHits == null || !pixelHits.IntegerValue.HasValue ||
                    area > pixelHits.IntegerValue.Value || area != regionTotal)
                    throw new InvalidOperationException("MRK面积测量与经面积筛选的区域或Mask来源不一致");
                var measuredItems = new List<CxVisionResultItem>(result.Items);
                measuredItems.Add(new CxVisionResultItem(result.RunId, result.SourceId,
                    "AcceptedRegionPixelArea", "AcceptedRegionPixelArea",
                    CxVisionResultKind.Measurement, "已接受缺陷区域总像素面积", "px²",
                    number: area));
                result.Items = measuredItems.AsReadOnly();
            }
            return result;
        }

        /// <summary>
        /// 兼容Studio通过MOC编译得到的检测StepResult：只记录真实存在的可选输出。
        /// 没有GraySum/Passed导出时不从Inspection.Passed伪造软件流程级标志。
        /// </summary>
        public static CxVisionRunSummary FromInspection(Guid runId, string sourceId,
            CxVisionInspectionResult inspection, long? graySum = null,
            bool? flowPassed = null, string planFingerprint = null,
            string capabilityVersion = null, DateTime? completedUtc = null,
            long? acceptedRegionPixelArea = null,
            FIMImageBuffer enhancedImage = null, FIMImageBuffer thresholdMask = null)
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
            if (acceptedRegionPixelArea.HasValue)
            {
                long measuredArea=acceptedRegionPixelArea.Value,regionSum=0;
                foreach (CxVisionResultItem item in values)
                    if (item.Kind==CxVisionResultKind.Region)
                        regionSum=checked(regionSum+item.Region.PixelArea);
                if (measuredArea<0 || measuredArea!=regionSum ||
                    measuredArea>inspection.ThresholdPixelCount ||
                    measuredArea>(long)inspection.Width*inspection.Height)
                    throw new ArgumentException("测量面积与已接受区域及阈值命中数不一致",
                        "acceptedRegionPixelArea");
                values.Add(new CxVisionResultItem(runId,source,
                    "AcceptedRegionPixelArea","AcceptedRegionPixelArea",
                    CxVisionResultKind.Measurement,"已接受缺陷区域总像素面积","px²",
                    number:measuredArea));
            }
            if (enhancedImage != null)
            {
                if (enhancedImage.Width != inspection.Width ||
                    enhancedImage.Height != inspection.Height ||
                    (enhancedImage.PixelFormat != FIMPixelFormat.Gray8 &&
                     enhancedImage.PixelFormat != FIMPixelFormat.Gray16))
                    throw new ArgumentException("增强图像尺寸与检测来源不匹配或格式不受支持",
                        "enhancedImage");
                values.Add(new CxVisionResultItem(runId,source,
                    "EnhancedImage","EnhancedPixels",CxVisionResultKind.Image,
                    "FIM中值滤波增强图",null,image:enhancedImage));
            }
            if (thresholdMask != null)
            {
                if (thresholdMask.Width != inspection.Width ||
                    thresholdMask.Height != inspection.Height ||
                    thresholdMask.PixelFormat != FIMPixelFormat.Gray8)
                    throw new ArgumentException("阈值Mask格式或尺寸与专业结果不匹配",
                        "thresholdMask");
                values.Add(new CxVisionResultItem(runId, source,
                    "ThresholdMask", "ThresholdMask", CxVisionResultKind.Mask,
                    "独立阈值分割Mask（区域面积筛选前）", null, image: thresholdMask));
            }
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
