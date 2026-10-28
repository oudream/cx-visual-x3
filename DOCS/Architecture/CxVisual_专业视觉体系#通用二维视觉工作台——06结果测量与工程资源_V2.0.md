# CxVisual 通用二维视觉工作台——06 结构化结果、测量、报告与正式工程资源设计 V2.0

> 覆盖V2D-14、16、21—23，连接08 ROI、19 Pipeline及Inspection。真实基础：FIMImageMeasurement、MVIOverlayStore、CxVisionInspectionResult、MDSDataContext、MEMEngineeringSession、MVIVisualEditor/MRCVisualItem与MPW显示。**VisionRunResult/多类型结果合同、工程型二维ROI正式资源映射为待评审和实现内容**。参考02 C07/C08/SQ04/SQ05。

## 1. 先区分五种对象

| 类别 | 含义及拥有者 | 可否当工程定义？ |
|---|---|---|
| MeasurementValue（候选通用合同） | Value/Unit/Quality/Uncertainty/Source，由Foundation通用数据或Vision专业结果组合 | 数值本身可作为属性，但一次测量结果不是编辑中的Resource |
| VisionDetectionEvidence（专业结果） | 区域、特征、置信、算法来源，由PVC/Vision产出 | 不；Inspection负责产品级判定 |
| VisionResultSet（候选专业结果） | 某RunId的多个具名Image/Mask/Geometry/Measurement/Diagnostics组合 | 不；是Run Artifact，不是Definition |
| MVIImageOverlayItem（E） | 供图窗即时显示、点选、编辑的临时几何投影 | 不；不能作为唯一持久定义 |
| MEM工程视觉定义（已有MEM/MRC扩展候选） | 有StableId/Schema/Revision/权限/ResourceRef的正式ROI/标注/算法定义 | 是；只能通过MEM事务编辑 |

**关键决议**：一次Run可能有N个中间Image、Mask、Contour、Measure、Defect和Trace。不能只用MVIImageSession.Original/Result两槽保存所有产物，更不能靠图窗Overlay数量当算法的完整结果。

## 2. 专业结果最小可复现合同

建议定义一次运行头部与类型化结果项（逻辑Schema，非当前公开C#类型）：

~~~text
RunHeader:
  RunId, CorrelationId, Status
  InputRefs[], FrameId/SourceId, Timestamp
  PipelineRef/PlanRevision/ResolvedManifestHash
  AlgorithmRefs/ModelRefs/ProviderVersions
  ParameterSnapshotHash, RoiRef/CalibrationRef/CoordinateSpace
  Elapsed, ResourceMetrics, Warnings, Errors
ResultItem:
  ItemId, ResultKind, ProducerNodeId, OutputPort
  SourceRef, CoordinateSpace, Unit/Quality/Validity
  InlineSmallValue OR ArtifactRef
  Relations[] (input, parent, defect, measurement, review)
~~~

要求1：原像素或大Mask以ArtifactRef/DataHandle引用，结果项不携带大块数组深拷贝；内嵌小数值要限制大小。要求2：即使无缺陷也有完整运行身份与明确空结果；算法失败、取消与未运行不是同一状态。要求3：运行时审计固定版本与来源，不允许运行后悄悄覆盖原RunId的结果。要求4：需要人工复核时建立派生ReviewRecord，保留操作者、时间、原始结果引用、意见、权限与审计，不篡改检测事实。

## 3. 测量与分析数据语义

| 数值/结构 | 必要内容 | 不得虚报 |
|---|---|---|
| Distance | Px距离、可选mm距离、两端/最近点、坐标版本 | 无Spacing时不得填物理量 |
| Angle | 两线定义、图像角与可选物理角、坐标系 | 非等距Spacing不能复用像素角 |
| Area/Perimeter | 精确几何/像素区域、单位、实际计算ROI | 不能以外包框面积伪装多边形面积 |
| FittedGeometry | 模型类型、参数、有效点、残差/容差 | 手绘椭圆不是专业边缘拟合 |
| Statistics/Profile | 原始数据域、采样算法、Bin、ROI和样本数 | 显示调窗与图像分析不可混淆 |
| Detection | 缺陷ID/位置/Mask或Contour/特征/置信 | Vision候选不代替Inspection最终OK/NG |

一次测量有源图ID、ROI/Geometry ID、算法版本、标定引用与数值精度；用户编辑源图元后应生成新测量结果或标记旧结果Stale，不得把历史测量值悄悄变成新值。区域的孔洞/多轮廓/布尔组合需要保留真实拓扑和来源。

## 4. 结果可视复核与证据

用户选择ResultItem后：验证SourceRef可用→加载对应图及可能的Transform→在原始图像空间投影Mask/Geometry→聚焦或高亮→展示属性/Measure/Trace→可人工批注→按权限导出结果。结果列表、图窗、曲线和报告共享ResultItemId/RunId，不应为不同Pane复制新的业务身份。无图可加载或参考资源失效时显示明确BrokenRef状态，不用前次有效图像误导用户。

## 5. 临时Overlay转正式工程资源的唯一通路

~~~mermaid
sequenceDiagram
  actor U as 用户
  participant W as MPW工作台
  participant O as MVI临时Overlay
  participant E as MVIVisualEditor
  participant M as MEMEngineeringSession
  U->>W: 选中ROI并选择保存为工程对象
  W->>O: 读取稳定Guid/SourceRef/Geometry
  W->>E: 建立专业编辑意图
  E->>M: 版本检查+BeginEdit/Preview
  alt 验证/授权均成功
    E->>M: Commit
    M-->>W: 新Revision/ChangeSet
    W-->>U: 工程对象投影
  else 拒绝
    M-->>W: ErrorCode/原事务未变
  end
~~~

这里的BeginEdit/Preview/Commit是**按现有MEM/MTC能力表述目标协作**，具体公开方法签名需用实际MEM代码确定；避免把SQ图中的描述当现成SDK。Undo/Redo调用既有MEMEngineeringSession，不新增图像历史数组。保存由正式MEM Store负责快照/版本/并发保护、SaveAs及恢复，不由MPW编写个人XML。正式映射时必须解决：
- 图像ROI在源图坐标上的位置和单位，是否依赖标定及转换矩阵；
- MRCVisualItem与Vision ROI在“可绘图对象/检测区域”专业语义上的差异，避免滥用MRC仅保存任意可变业务Blob；
- StableId、SourceRef、内部Contour/洞、图层/样式、锁定、算法绑定、Schema迁移；
- 工程打开时遗失图片/模型/Provider的缺失引用降级；Undo撤销时同步重投影视图。

## 6. 报告、导出和交换

输出至少区分原始像素、经显示映射的PNG、Mask、叠加截图、几何JSON/CSV、测量表、检测证据及正式报告。导出须声明像素格式、坐标空间、单位、SchemaVersion、字符编码及数据安全；CSV公式注入、目录遍历、XML外部实体和敏感图像泄露要作为测试风险。PDF/Excel可以通过专业报告模块实现，不强制Foundation.Imaging依赖PDF/Office库。报告基于固定RunId、Manifest与ArtifactRef构造，不能在导出期间用变化中的图窗状态伪造历史结果。

## 7. OOP、GoF、AOP与状态

- 领域对象：RunResult与ReviewRecord分开；Value Object承载测量值，ResultItem组合产物引用而不是巨型继承树；资源定义与显示投影分离。
- GoF：MEM Command+Transaction、MRC Composite、MVI Observer；不复制Editor/Store/Registry。
- AOP：Result发布/导出/人工复核记录CorrelationId、用户权限、原运行版本、导出文件Hash、失败重试信息；读者访问的历史证据不可无声覆盖。
- 状态：Valid/Empty/Failed/Cancelled/Stale/BrokenReference/Reviewed有不同语义；具体枚举可由专业Owner审查后收敛，不能作为虚构现成类使用。

## 8. 验收 D06

| ID | 验收门禁 |
|---|---|
| D06-01 | 一次运行产生两张图、Mask、轮廓、测量值和Trace，均具稳定Run/Item身份 |
| D06-02 | 图窗/结果表/曲线选择同一Item定位一致，换图不得错位 |
| D06-03 | 空结果、失败、取消、过期、BrokenRef可区分并正确提示 |
| D06-04 | 原始结果与人工复核记录隔离，权限/审计可追踪 |
| D06-05 | 临时ROI显式变为工程Resource，稳定ID/坐标/样式保真 |
| D06-06 | 统一Undo/Redo、Save/SaveAs/重新打开、并发版本冲突反证 |
| D06-07 | 非等距标定角度/距离和单位，手绘与拟合严格分离 |
| D06-08 | 导出CSV/JSON/图像/报告回读同一Run及校验，安全输入反证 |
| D06-09 | 历史工程资源Schema迁移与缺资源显式提示 |

**本版为设计与候选对象裁决，不等于二维ROI已经持久工程集成通过。**

## 9. 结果、临时叠加、工程资源的不可混写规则（深化）

Vision的正式结果体系应按**RunId→具名ResultItem→SourceRef/ArtifactRef→类型/单位/质量/Trace**组织；成功无缺陷与失败、取消、过期应区分。现有CxVisionInspectionResult只覆盖部分专业CPU检测，不是全类结果模型。VisionRunRecord/ResultItem仍属专业语义**P候选**，应优先组合/判别Kind并复用FDT/MDS的真实数据适配与容量机制，不预先批准深继承类树。

| 身份 | 唯一Owner | 变更方式 |
|---|---|---|
| ResultItemId/RunId | 专业运行结果 | 一次Run完成后不原地覆写；重跑新RunId |
| MVIImageOverlayItem.Id | 临时图像会话 | 选择/显示/临时编辑，可清空，不是工程Undo |
| MEM/MRC StableId/Revision | 工程Session | 用户明确Promote→MTC事务→Undo/Redo/Save/Open |
| ReviewRecordId/OriginalRunId | 派生人工复核 | 保留原始检测事实/审核人/时间/审计 |

最重要的纵向验收：算法Run产生Image/Mask/Geometry/Measure多结果→结果表定位图窗→修改临时显示不改变Result→明确保存ROI→MEM撤销/重做/保存重开→旧Run证据仍可追溯。

## 10. 结果、证据与工程资源的定型草案

> **设计审查（P）**：专业统一RunRecord/ResultItem/ReviewRecord目前仍是逻辑模型，不是已实现的CLR类。已核实的真实基础是CxVisionInspectionResult、FDTDataAdapterRegistry、MDSDataContext、MVIImageOverlayStore/Item、MEMEngineeringSession/MRC视觉对象及MTC编辑机制。必须在已存在机制上完成结果聚合，而不是新增ResultHeap或第二工程历史。

### 10.1 七种结果类型，共用一次Run身份

| 结果Kind（P目标语义） | 值结构/引用 | 保证 |
|---|---|---|
| Image | PixelFormat/Width/Height/ArtifactRef | 原图与处理图可区分且不可误覆写 |
| Mask | MaskRef、Origin、CoordinateSpace、有效范围 | 与输入图尺寸/坐标变换可校验 |
| Geometry | ROI/轮廓/拟合模型、SourceRef及TransformRef | 不丢退化/残差/拟合质量 |
| Measurement | Value/Unit/CalibrationRef/Uncertainty | 无有效校准时物理单位为空；像素量可用 |
| FeatureTable/Statistics | 样本量、参数、列Schema、ROI/SourceRef | 空表与失败结果分开 |
| DetectionEvidence | 缺陷候选、定位、类型、置信或质量 | Vision只产生检测事实，不代替Inspection最终判定 |
| Diagnostic/Trace | StepId/SourceMap/Duration/Warnings/Error | 来源可查，运行取消与错误留证据 |

一份Run可以同时有多个同Kind Item。Item应有稳定的ItemId、ProducerNodeId、OutputPort和InputRefs；输入源ID不是ItemId，用户的选择状态也不是ItemId。显示层根据Kind选择Renderer/Inspector，但不修改原结果对象。

### 10.2 运行头、结果项、关联和审查记录的概念Schema

~~~text
RunRecord [P] = {
  RunId, CorrelationId, Status,
  StartedUtc, EndedUtc,
  InputRefs[{SourceId, FrameId?, ImageRevision?, Hash?}],
  DefinitionRef{ProjectId, ContentId, Revision},
  ResolvedPlan{Fingerprint?, CapabilityVersions?, ProviderVersions?},
  ParameterSnapshotRef, CalibrationRef?,
  Items[ResultItemId], DiagnosticsRef
}

ResultItem [P] = {
  ItemId, RunId, ProducerNodeId, OutputPort,
  Kind, PayloadOrArtifactRef, SourceImageRef,
  CoordinateSpace?, Unit?, Validity, Quality?,
  ParentItemIds[], SchemaVersion
}

ReviewRecord [P] = {
  ReviewId, OriginalRunId, RelatedResultItemIds[],
  ReviewerRef, TimeUtc, Decision, Note, Revision
}
~~~

**这只是审查用逻辑Schema，不是已经存在的文件格式、C#公开签名或已获批的持久存储协议。** 一次Run的最小证据不要求每个字段都存在，但“引用不知道”“模型版本不知道”等情况必须显式Unknown，不能默认为正确。Hash/签名和完整Release Manifest需依真实平台发布能力确认；当前MOC软件Plan Fingerprint不等于设备侧签名。

### 10.3 状态分层

Run总体状态与Item有效性独立管理；ResultItem可以是有效空RegionSet，不应把它等同于整个Run失败。至少区分：
- **Run：Created、Running、Completed、Failed、Cancelled、Rejected**（P状态，具体命名待实际实现审核）。
- **ResultItem：Valid、Empty、Stale、MissingArtifact、Invalid**；来源版本不符时不可被展示成Valid。
- **Review：Unreviewed、Reviewed、Superseded**（P），人工复核不会改变原始算法输出。

~~~mermaid
stateDiagram-v2
  [*] --> Created
  Created --> Validating
  Validating --> Rejected: schema/capability/reference invalid
  Validating --> Running: accepted
  Running --> Committing: all required outputs ready
  Running --> Failed: error/invalid output
  Running --> Cancelled: cooperative cancel
  Committing --> Completed: atomic publication
  Committing --> Failed: publish failed
  Completed --> [*]
  Failed --> [*]
  Rejected --> [*]
  Cancelled --> [*]
~~~

已发布的Run记录原则上不可原地编辑；报告导出/人工复核通过派生记录与引用表达。不同输出端口的部分成功是否允许需在Operation Schema显式声明，不得默许将部分Pending数据当作完整成功。MDS现有Pending/Commit/Abort机制可以服务执行输出，但不直接等价为长期结果仓库的事务。

### 10.4 结果与工程对象三权分离

| 视角 | 谁写入 | 允许的变化 | 不允许的变化 |
|---|---|---|---|
| Run/ResultItem | 运行发布与专业结果Owner | 新Run、新Item、带版本的新Artifact | 从WPF修改原历史Item或重新使用旧RunId覆盖数据 |
| MVIOverlay | MVI临时会话 | 高亮、移动、编辑草稿、清除、图层切换 | 当作AlgorithmResultStore或工程Undo |
| MEM/MRC | MEM/MTC唯一工程Session | 显式编辑/事务/Undo/Redo/保存重开 | 算法线程直接修改正式ROI和Revision |
| ReviewRecord | 人工复核/Inspection授权角色 | 新增复核版本、意见、状态和审计 | 用复核意见直接篡改检测候选原始数值 |

选择Run Item时，图窗应获取该Item的SourceImageRef，确认ViewImage与SourceId、坐标空间/Transform相匹配，然后渲染Mask/Geometry Overlay；SourceRef无法解析须显示BrokenReference而非继续画在当前另一张图上。

### 10.5 工程ROI显式转换的完整交互

~~~mermaid
sequenceDiagram
  actor U as 用户
  participant V as MPW/MVI
  participant R as ResultStore
  participant E as MEMEngineeringSession
  U->>V: 选择临时ROI或结果轮廓
  V->>R: 确认SourceRef/Geometry/Kind
  R-->>V: 几何或缺失引用错误
  U->>V: 明确保存为工程标注
  V->>E: 授权+版本校验+Edit
  alt 有效Revision及Schema
    E-->>V: 稳定工程Id+新Revision
    V-->>U: 工程对象投影
  else 冲突或非法
    E-->>V: Rejected；旧工程不变
  end
  U->>E: Undo或Save
~~~

该图是目标交互流程，真实代码只能通过平台现有MEM/MTC方法签名实现，不能把图中ResultStore当现成类。工程对象使用MRC现有Shape/VisualItem机制时，需明确源图坐标、标定版本和图层/样式以及运行/结果关联；专业Mask不能无条件变成没有洞信息的多边形。

### 10.6 结果契约的先行反证

| ID | 对象/用户验收 |
|---|---|
| RES-01 | 同一个Run含Image+Mask+Geometry+Measurement+Trace不同Kind，唯一RunId与ItemId |
| RES-02 | 新参数产生新RunId，旧结果、旧图与人工复核不受影响 |
| RES-03 | 结果表定位到原图正确坐标，另一Source/Spacing/Transform时拒绝错位显示 |
| RES-04 | Empty、Failed、Cancelled、Stale、MissingArtifact有不同状态与用户提示 |
| RES-05 | FDT捕获可变结果安全，MDS输出Abort不留下虚假的已提交证据 |
| RES-06 | 临时Overlay→MEM对象必须由用户明确Promote；Undo/Redo/Save/Open一致 |
| RES-07 | 导出CSV/JSON/图片/报告能回查同一个Run和参数/版本/单位，敏感数据受控 |
| RES-08 | 用户复核作为派生记录，原始算法结果及缺陷质量保持不变 |
| RES-09 | 失败报告包含Node/Port/错误码/Trace，不得因空RegionSet伪造异常 |

这些反证的实测代码、SHA、CI与设备环境须记Testing文档；本轮只是架构契约，不提升G2—G6等级。
