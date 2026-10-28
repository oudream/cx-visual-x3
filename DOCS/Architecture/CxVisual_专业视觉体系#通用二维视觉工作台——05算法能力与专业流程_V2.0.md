# CxVisual 通用二维视觉工作台——05 算法能力、参数、专业流程与调试设计 V2.0

> 覆盖V2D-10—19，连接17 AI预留、20执行、21结果。当前源码可核实FIMImageOperations/Filters/Analyzer/Measurement/MaskContours、CxVisionInspectionRuntime、CxVisionDataFlow.CreateCpuInspectionPlan、MRKExecutionStep和MDS DataContext；**专业统一算法Descriptor、VisionPipeline资源、完整实验调试均为目标，未实现**。关联02 C05/C06/SQ03。

## 1. 处理、分析、测量、检测与智能推理的分界

| 能力种类 | 首选Owner | 输入与输出 | 核心区别 |
|---|---|---|---|
| 基础处理 V2D-10 | FIM | Image→Image/Mask | 通用像素/数学，不读取专业Recipe |
| 增强修复 V2D-11 | FIM通用 / PVC特定 | Image+Params→Image+Quality | 不将传统增强与AI推理混为一体 |
| 分割 V2D-12 | FIM基础 / PVC高级 | Image+ROI→Mask/Region | 源坐标、孔洞、有效区域明确 |
| 分析 V2D-13 | FIM通用 / PVC专业 | Image+Region→Statistics/Features | 结果是可解释数值，不等于修改图像 |
| 几何计量 V2D-14 | FIM/FGE / PVC专业 | Geometry/Image+Calibration→Measurement | 单位、Spacing、置信/质量 |
| 匹配配准 V2D-15 | PVC/FGE及复用FIM运算 | Images+TransformHint→Transform+RegisteredImage | 匹配失败/几何无解需显式报告 |
| 工业检测 V2D-16 | PVC | Image+ProfessionalRecipe→DefectEvidence | 只产生Vision检测事实，产品OK/NG归Inspection |
| AI V2D-17 | PVC/模型Provider | Image+ModelRef→TypedAIResult | 模型/推理版本、权重和设备能力先验证 |

**禁止做法**：在TCT按钮复制FIM算法、在FIM直接依赖HALCON/ML模型、把灰度阈值连通域结果伪称行业完整缺陷判定、直接输出物理毫米而没有有效标定。

## 2. 算法能力Descriptor的最小必要规范

“Descriptor”属于待裁决的Vision专业语义，不能抢MPM已存在的类型/Capability注册Owner。每项需要被搜索、配置、组合或发布的算法，最少具备以下字段：

| 字段 | 语义 | 不变量 |
|---|---|---|
| OperationId, ApiVersion, Version | 稳定身份与兼容性 | 不能依赖显示名称定位算法 |
| Capability/ProviderRef | 所需CPU/GPU/SDK、算法实现能力 | 缺能力在运行前明确拒绝 |
| InputPorts/OutputPorts | 命名端口、TypeId、必填、数量/可空 | Image/Mask/Geometry/Measurement不得靠object盲猜 |
| ParameterSchema | key、Type、范围、单位、默认、枚举、条件约束 | 参数非法在运行前拒绝，无默认的不得隐式补值 |
| ExecutionPolicy | Cancel/Thread/Resource/Timeout/EstimatedBytes | 与MRK已有策略映射，不另建Runner |
| Determinism/Precision | 随机种子、输出容差、依赖版本 | GPU、AI结果不可不经评估承诺逐位相等 |
| ResultSchema | 结果类型与结构、ErrorCode、Quality | 空结果与失败要明确区分 |
| Diagnostics | CorrelationId、Trace、输入/输出尺寸、耗时 | 不写每个像素日志 |

接口若无注册和发现需求，则保留已有FIM静态API；只有经过复用证据确认才增设能力Descriptor/Factory/Strategy。已有MRKExecutionInput/Output及MPM类型元数据优先复用，不重新定义通用端口基础类型。

## 3. 算法调用合同（逻辑伪示意，不是现有C#签名）

~~~text
OperationRequest {
  OperationRef = {stableId, version, providerRef};
  Inputs = { ImageRef, OptionalRoiRef, CalibrationRef, NamedTypedInputs };
  Parameters = { schemaVersion, validatedValues };
  Context = { RunId, CorrelationId, Cancellation, ResourceBudget, Permission };
}
OperationOutcome {
  Status = Success | Empty | Failed | Cancelled | Rejected;
  Outputs = NamedTypedResults / ArtifactRefs;
  Diagnostics = { trace, warnings, elapsed, resourceUsage, provenance };
}
~~~

- 成功但未检测到目标：Success/Empty + 合法空结果，不得当异常。
- 失败：保留错误码/发生节点/原始输入Ref；不得发布部分有效结果为已完成结果。
- 取消：向算法传播Token，允许明确不可中断SDK Worker声明及超时隔离。
- 不同算法的阈值等参数必须有数值域和物理单位；UI参数滑块、配置存档、SDK同用同一Schema。
- 大输出Image/Mask通过DataHandle/ArtifactRef并保留输出端口名称，非仅保存一张Result Bitmap。

## 4. VisionPipeline是专业Resource，但执行使用已有体系

~~~mermaid
flowchart LR
  D[MEM VisionPipeline定义] --> V[参数/类型/资源引用验证]
  V --> C[MOC解析/专业扩展编译]
  C --> E[MRKExecutionPlan]
  E --> M[MDS DataContext]
  M --> P[FIM/PVC Operation]
  P --> R[Typed Outputs/Artifact]
  R --> T[结果复核/Trace]
~~~

设计期定义：节点（OperationRef）、有类型的输入端口与绑定、参数/配方/ROI引用、边与顺序、输出Schema、Revision。内部节点作为EmbeddedObject，不上升为数百独立Resource。专业编译器复用MOC/MRK的控制/数据基础，但校验Vision特有的图像格式、Mask几何、Calibration、CPU/GPU资源、输出兼容性。

运行期采用固定Plan/Manifest，Studio修改参数要生成新草稿/版本/新Run，不直接修改进行中的Plan。运行历史只有运行产物和Trace，不拥有可编辑工程Graph。

## 5. 实验与专业调试

| 用户动作 | 内核行为 | 结果呈现 |
|---|---|---|
| 选择一张图及ROI | 校验SourceRef+ROI坐标+格式 | 高亮范围与可用算子 |
| 选择算法 | 通过Capability/参数Schema发现 | 参数/单位/错误提示 |
| 预览或执行 | 版本Pinned、后台计算、取消与资源策略 | 状态、进度、耗时、原图/结果 |
| 逐节点执行 | 按正式Plan/数据依赖取输入 | 中间Image/Mask/Features树 |
| 调整参数重跑 | 创建新Request/RunId，旧请求不可覆盖 | 两次结果并排、差异与版本 |
| 断点/单步 | 调试宿主控制已批准的专业运行Hook | 当前节点/数据/SourceMap/诊断 |
| 保存实验 | 把参数与Pipeline写回MEM Revision | 可复现Resource与引用 |
| 发布 | 解析、编译、测试证据、Manifest/权限 | 独立运行的发布包 |

高阶流、循环、条件和子流程应使用平台已有控制与运行表达，不重新实现VisionGraphExecutor；同时Vision可以定义独立于Automation的图像DataFlow专业语义。当前CxVisionDataFlow已用MRKExecutionPlan跑真实CPU阈值检测链，**它是专业执行集成存在的证据，不是完整可视化VisionPipeline已完成**。

## 6. 专业算法质量与数值语义

FIM基础灰度统计以原像素数值为准，窗宽窗位只影响显示；ROI要按真实形状逐像素判定而不是用BoundingRect伪装；Gray16/Float32应保留类型与动态范围；图像变换的插值、边界、NaN/Infinity/溢出政策明确。非等距sx/sy需在物理空间重新计算几何量（特别是两线最近点和角度），不得直接对像素距离乘平均Spacing。

匹配/配准必须输出变换矩阵、源/目标图Ref、质量/内点/残差与失败状态；用户可查看是否适合用于后续ROI投影。拟合必须输出模型参数、残差/误差与有效点数；手绘圆的直径不等于根据真实边缘拟合的圆直径。

专业HALCON/OpenCV迁移验收需同一真实样本、ROI/阈值/边界、算法版本、误差容限和性能对照；不同算法数值不必要求位级相同，但须定义可接受标准。

## 7. AI预留和模型生命周期

AI归Vision专业能力；输入/输出仍使用通用Image/Mask/Geometry/Measurement/ResultRef及Context。ModelRef必须包含模型Hash、版本、预处理与后处理版本、设备能力、精度声明与许可。模型推理需可降级/禁用，不影响传统算法运行。训练、标注服务、模型仓库可以作为独立Capability/专业服务，不直接塞入FIM。**此文档不宣称AI去噪、超分或推理引擎已经接入。**

## 8. GoF/AOP裁决

- Strategy：至少两个同一结果语义、可替换的真实实现才引入，如CPU与专业SDK分割；不同专业算法不强行抽象为同一个Strategy。
- Command：平台工程事务和Runtime命令不同；观察执行结果不意味着编辑工程。
- Observer：算法Result事件发布不可塞在WPF重绘线程执行重计算。
- Factory/Registry：先审正式MPM/Capability/Provider，杜绝第二注册中心。
- AOP：在操作/节点/Run边界记录参数Hash、输入输出句柄、耗时、错误和取消状态；日志过多不能成为长时实时瓶颈。

## 9. D05验收门禁

| ID | 必须用真实计算证明的结果 |
|---|---|
| D05-01 | 任意一个FIM基础算子从Descriptor/按钮/API得到同一数值结果 |
| D05-02 | Gray16/Float32参数、原始灰度、ROI实际像素数正确 |
| D05-03 | 一条2—4节点类型化Pipeline可验证、取消、输出中间产物 |
| D05-04 | 错误端口/非法参数/缺SDK能力编译或运行前明确拒绝 |
| D05-05 | 改ROI/参数重跑旧Run不得覆盖最新结果，结果可比较 |
| D05-06 | MEM保存重开、版本固定、复用MRK/MDS执行并输出Trace |
| D05-07 | 同样本与旧HALCON/专业算法边界、质量和容差对照 |
| D05-08 | 大图内存/并发/性能与GPU回退、Vendor SDK线程测试 |
| D05-09 | AI预留只测试契约兼容，不冒充已有模型或现场效果 |

**注意**：测试和实施状态以对应新提交及Windows CI为准；本次仅形成目标规格。

## 10. 以现有正式类型构成Vision专业算法体系（架构深化）

**本节优先于此前候选类名。** VisionOperationDescriptor、VisionTypedPort、VisionPipelineDefinition目前仅是设计角色，不自动批准为公共API或第二套持久化模型。已核实真实源码拥有MPMTypeDescriptor、MPCPortDescriptor、IMPMTypeProvider、MEMContent、MOLWiringDefinition、MOCEngineeringCompiler、MOCCompiledPlan、MRKExecutionInput/Output、MDSDataContext、FDTDataAdapterRegistry、CxVisionThresholdProvider及IPVCComputeModule。

| 需要的能力 | 原有权威Owner与类型 | Vision只补充 |
|---|---|---|
| 算法/参数类型 | MPMTypeDescriptor/IMPMTypeProvider | vision.*专业TypeId、像素格式、ROI、单位、标定规则 |
| 输入输出端口 | MPCPortDescriptor、MRKExecutionInput/Output | Mask/Geometry/Measurement的专业约束，不新增第二端口注册表 |
| 正式可编辑流程 | MEMContent + MOLWiringDefinition | 专业节点Capability、参数、ROI及结果引用；不新建可持久化Graph |
| 解析编译 | MOCCompiledPlan/SourceMap | 专业格式/能力验证、调试来源投影 |
| 执行/数据 | MRKExecutionPlan + MDSDataContext/FDT适配 | 安全像素捕获、尺寸估算、执行和类型化结果 |
| 专业SDK与算法 | PVC Provider/ComputeModule，FIM静态算子 | 有真实算法差异时才引入最小策略合同 |

~~~mermaid
flowchart LR
  T["MPM专业Type/Ports"] --> D["MEMContent + MOL Logic"]
  D --> C["MOC解析编译 / SourceMap"]
  C --> R["MRK Plan / MDS typed data"]
  R --> P["FIM/PVC专业算法"]
  P --> O["Vision Result/Artifact【待实现】"]
~~~

**批准新增类型之前的最小验证**：增强→阈值→区域分析→测量的2—4节点纵向链；定义各节点TypeId、参数、输入输出、错误/取消、Frame来源、参数和标定Revision；能从WPF和无头SDK以同一语义运行、输出Image+Mask+Measurement且保留RunId/Trace。仅当已有MPM/MPC/MRK契约无法表示且两种真实算法同时需要时，才审专业扩展，而非创建新框架。

## 11. 算法能力定型：工程TypeId与执行CLR Type的双层合同

> 2026-10-28再核实的E源码：MPMTypeDescriptor（TypeId/SchemaVersion/Properties/Ports/Aspects）、MPCPortDescriptor（MemberId/TypeId/Direction/Multiplicity/Contract）、MOCCapabilityDescriptor及MOCCapabilityCatalog、MOCFlowBuilder.Step/Connect/SetParameter/Build、MOCEngineeringCompiler.Compile(snapshot, entryContentId, resolver, instancePath)、MOCCompiledPlan、MRKExecutionInput/Output、CxVisionThresholdProvider、CxVisionDataFlow。以下是专业组合契约设计；不声称完整Vision工程已接线。

### 11.1 工程期与执行期两种类型

| 项目 | 现有工程层 | 现有运行层 | Vision应补足 |
|---|---|---|---|
| 算法身份 | MOCCapabilityDescriptor.Id/Version及Catalog（可发现但Provider可能Unavailable） | IMRKCapabilityResolver解析真实能力/版本 | 专业分类、输入像素、Provider约束、使用说明 |
| 数据端口 | MPCPortDescriptor.TypeId和Direction/Contract | MRKExecutionInput.ValueType/Source/SourceName及Output Type | 图像格式、Mask坐标、CalibrationRef与所有权 |
| 参数 | MPMPropertyDescriptor/MPMValue：String/Number/Integer/Boolean/Decimal/Resource等标量/引用 | 已校验运行时参数与绑定 | 类型依赖范围/单位/错误码，复杂数据走Ref |
| 专业Graph | MEMContent的MOLWiringDefinition、Blocks/Links及参数/端口 | MOCCompiledPlan→MRKExecutionPlan | Vision专业规则检查、节点Result投影 |
| 数据处理 | 工程静态资源与Hash，不包含运行像素 | MDS/FDT专业类型的安全捕获/尺寸估算 | 图像→Mask→Geometry→Measure合同 |
| 算法实现 | Catalog描述而不含Vendor算法本体 | FIM/PVC真正执行算法 | 专业质量与失败/空/取消区分 |

**重要**：MPC工程端口的TypeId是字符串，而MRK执行端口检查CLR Type。必须有可审计的TypeId→实际CLR ValueType/Adapter/版本映射；MRK已能检查CLR类型但不能自动理解Mask坐标或毫米单位。禁止额外新建VisionTypedPort通用框架。

### 11.2 参数资源化原则

MPMValue是工程期固定字面值，不能把Image/Mask大块数组、AI模型权重、SDK指针或FrameStream直接塞进去。Gray16阈值应是合法数值与格式相关的范围；ROI与Calibration由ResourceRef+专业坐标校验；Template或Model由带Hash/版本的AssetRef供运行时解析。复杂算法配置优先现有MPM属性、Aspect或专业Resource扩展点，不允许靠无版本随意JsonBag绕过Schema。

### 11.3 一条四节点完整能力验证场景

| 步骤 | 能力标识（设计候选非已注册ID） | 输入 | 具名输出 | 错误/边界 |
|---|---|---|---|---|
| A 增强 | vision.image.enhance | Gray16+KernelParams | EnhancedImage | 格式/边界/Cancel/预算 |
| B 分割 | vision.image.threshold | EnhancedImage+ROI+Threshold | Mask | 阈值范围0—65535、ROI空间 |
| C 区域 | vision.mask.components | Mask+Connectivity+MinArea | RegionSet | 空区域是Empty而非失败 |
| D 测量 | vision.geometry.measure | RegionSet+CalibrationRef? | Measurements | 无标定不得输出mm，结果须带SourceRef |

**规范**：四个CapabilityId只是待验证的专业算法命名草案，当前不得用作已存在API；实现前检查FIM/PVC算子的实际返回类型、MOC端口支持和参数映射。

~~~mermaid
sequenceDiagram
  actor U as 工程师
  participant E as MEMContent/MOL
  participant T as MPM/MPC
  participant C as MOC Compiler
  participant R as MRK Plan
  participant D as MDS Data
  participant V as Vision FIM/PVC
  U->>E: 定义四节点及参数引用
  E->>T: 校验TypeId/Ports/Schema
  E->>C: 固定Snapshot和入口Content
  C-->>R: CompiledPlan/SourceMap
  R->>D: 捕获Image/ROI/参数
  R->>V: 增强/阈值/区域/测量
  V-->>D: Image/Mask/Regions/Measures
  D-->>R: Commit或Abort
  R-->>U: 运行结果/Trace或拒绝
~~~

### 11.4 生产与实验都必须通过的七层校验

1. Capability/Provider和Version可解析；目录显示不可用时明确拒绝而不伪造执行成功。
2. 工程MPM TypeId与MRK CLR Type正确映射，Port Required、Source、Multiplicity及依赖顺序合法。
3. 参数/单位/范围/NaN/Infinity/默认值/动态绑定合法，16位灰度不因显示窗位变成8位。
4. 输入、输出FDT适配器遵守只读、深复制或不可变约定，载荷大小可估算。
5. Mask/ROI/Measurement来源同一ImageRef和CoordinateSpace，或提供明确且合法的TransformRef。
6. 单步/调试经MOCSourceMap回到专业节点ID；取消/过期/SDK失败可关联RunId并回收临时资源。
7. 输出按ProducerNodeId/OutputPort/ResultKind/SourceRef发布，成功Empty、Cancelled、Failed、Stale互相区分。

### 11.5 模式/实现退让规则

优先MPMTypeDescriptor+MPCPortDescriptor+MOCCapabilityDescriptor；其次利用MPMAspect/Property/Resource绑定表达专业约束；然后在PVC中补最小专业校验/算法实现；仅在两个以上真实消费方都无法表达时新增专业类型。不得从概念UML直接创建并行算法图、参数库、调度器或结果堆。

### 11.6 审查门禁ALG-G01—G06

ALG-G01能力可发现和Unavailable正确提示；G02单算法参数/类型/异常；G03四节点Flow可编译并能取中间产物/SourceMap；G04非法格式/参数/ROI/端口和取消不污染已发布结果；G05无头API与WPF工作台同输入同数值；G06将CPU/专业SDK/AI能力替换时保持输入输出/版本/追溯合同。未执行Windows CI及真实完整四节点验证前不得提升验收等级。
