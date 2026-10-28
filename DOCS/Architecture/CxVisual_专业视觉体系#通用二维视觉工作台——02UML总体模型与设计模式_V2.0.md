# CxVisual 通用二维视觉工作台——02 UML总体模型与OOP/GoF/AOP设计 V2.0

> **性质**：全类架构的UML关系主文档，2026-10-28。**26张关键图：用例3、组件3、类8、状态4、时序6、部署2。** 采用GitHub可渲染Mermaid；U01—U03、DP01—DP02是用例/部署的Mermaid逻辑投影，非标准UML符号。严格符号版本可依同一对象模型导出PlantUML。所有图需要保持对象与Owner语义一致。
>
> **读图状态**：E=源码存在（不代表所有场景已验证）；R=扩展现有对象；N=尚未实现且必须审查的新类型候选；P=设计目标。图中的N/P不允许当公开C# API引用。候选名字不能绕过DOCS/02～04和现有MPM/MRK/MDS契约。实际代码基线为2026-10-28 master审查，CI未在本轮执行。

## 0. 统一架构不变量

1. Foundation.Imaging提供像素及通用数值；Mechanism.Visual只管UI无关会话/交互/临时叠加；MPW管WPF输入输出；Professional.Vision管专业算法与Vendor SDK。
2. MEM/MRC/MTC为持久资源及编辑真源，MOC负责解析编译，MRK负责执行，MDS负责运行数据，FLD负责观察。不新增并行工程树、历史栈、数据堆或通用执行器。
3. 单帧数据、图像序列、运行帧流、Mask、Region、ROI、算法结果、工程资源各有身份与生命周期。原像素与显示LUT不同；像素/图形/物理坐标不同。
4. UI输入、异步计算、数据发布、保存事务、跨专业调用使用明示取消/版本/权限/背压策略。Studio退出后生产Runtime必须能自治运行。
5. 符合依赖DAG；Mermaid箭头表达职责意图，不自动授权新的程序集直接依赖。


## 用户场景关系

### U01 用户交互用例

~~~mermaid
flowchart LR
  E((工程师)) --> A[浏览图像]
  E --> B[ROI/标注/测量]
  E --> C[处理/分析]
  B --> D[结果复核与导出]
  C --> D
  S((开发者)) --> F[嵌入控件和API]
  F --> A
~~~

**责任/验收约束：** 交互和SDK必须共用数据语义。交叉能力索引详见01全类目录和03—08专题设计。

### U02 算法试验用例

~~~mermaid
flowchart LR
  A((算法工程师)) --> B[参数/能力选择]
  B --> C[类型端口/流程定义]
  C --> D[验证编译]
  D --> E[单步/执行/取消]
  E --> F[中间结果/比较/复现]
  F --> B
~~~

**责任/验收约束：** 设计期定义与运行期快照分离。交叉能力索引详见01全类目录和03—08专题设计。

### U03 工业在线用例

~~~mermaid
flowchart LR
  O((操作员)) --> A[发布版本加载]
  P((设备采集)) --> B[图像流与时间戳]
  A --> C[专业运行]
  B --> C
  C --> D[检测证据]
  D --> E[Inspection判定]
  C --> F[诊断/性能]
~~~

**责任/验收约束：** 工业软实时与控制器硬实时严格分开。交叉能力索引详见01全类目录和03—08专题设计。


## Owner组件与协作

### P01 基础Owner与组件

~~~mermaid
flowchart TB
  H[Studio/TestCenter/API] --> MPW[MPW]
  MPW --> MVI[MVI]
  MVI --> FIM[FIM/FGE]
  H --> Vision[Vision专业能力]
  Vision --> PVC[PVC]
  Vision --> MEM[MEM/MRC/MTC]
  Vision --> MRK[MRK/MDS/F19]
  Vision --> FLD[FLD诊断]
~~~

**责任/验收约束：** 图仅表达逻辑调用意图，物理csproj以DAG批准关系为准。交叉能力索引详见01全类目录和03—08专题设计。

### P02 工作台组件

~~~mermaid
flowchart TB
  S[共用Dock Shell] --> E[资源与对象树]
  S --> W[多视口/图窗]
  S --> P[参数/属性]
  S --> G[算法/流程]
  S --> R[结果表/曲线/报告]
  S --> D[任务/诊断]
  W --> X[统一选择/坐标上下文]
  P --> X
  R --> X
~~~

**责任/验收约束：** 不建立TestCenter专用业务内核；多面板共享选择状态。交叉能力索引详见01全类目录和03—08专题设计。

### P03 跨专业合同

~~~mermaid
flowchart LR
  A[Automation采集] -->|Frame/Stream| V[Vision专业调用]
  V -->|Result/Artifact| I[Inspection]
  C[CT3D] <-->|ProjectionRef/CalibrationRef| V
  H[HMI] -->|Command/ResourceRef| V
  V -->|State/Event| H
  S[Simulation] -->|虚拟Provider| A
~~~

**责任/验收约束：** 禁止跨专业传Vendor原生句柄或WPF控件。交叉能力索引详见01全类目录和03—08专题设计。


## 核心领域类

### C01 像素、区域及元数据

~~~mermaid
classDiagram
  class FIMImageBuffer {
    <<E>>
    +Width
    +Height
    +Stride
    +PixelFormat
    +CopyPixels()
  }
  class FIMPixelRegion {
    <<E>>
  }
  class FIMImageOperations {
    <<E>>
  }
  class ImageMetadata {
    <<N_candidate>>
    +SourceId
    +FrameId
    +PixelSpacing
  }
  class MaskContract {
    <<N_candidate>>
    +CoordinateSpace
  }
  FIMImageOperations ..> FIMImageBuffer
  FIMImageOperations ..> FIMPixelRegion
  ImageMetadata ..> FIMImageBuffer
  MaskContract ..> FIMImageBuffer
~~~

**责任/验收约束：** 原像素与显示映射分离；候选元数据与Mask先审已有FDT/FIM。交叉能力索引详见01全类目录和03—08专题设计。

### C02 多帧与数据句柄

~~~mermaid
classDiagram
  class FIMImageBuffer {
    <<E>>
  }
  class IMDSDataContext {
    <<E>>
    +Get()
    +Return()
  }
  class ImageFrameRef {
    <<N_candidate>>
    +FrameId
    +Timestamp
  }
  class ImageSequenceRef {
    <<N_candidate>>
  }
  class ArtifactRef {
    <<P_contract>>
  }
  ImageSequenceRef o-- ImageFrameRef
  ImageFrameRef --> ArtifactRef
  IMDSDataContext ..> FIMImageBuffer
~~~

**责任/验收约束：** 多图、大数据与持久工程采用引用或受控句柄，不深嵌像素。交叉能力索引详见01全类目录和03—08专题设计。

### C03 视口、会话、临时叠加

~~~mermaid
classDiagram
  class MVIImageSession {
    <<E>>
    +Original
    +Result
    +SourceVersion
    +Changed
  }
  class MVIImageViewport {
    <<E>>
  }
  class MVIImageOverlayStore {
    <<E>>
    +AddRange()
    +Changed
  }
  class MVIImageOverlayItem {
    <<E>>
    +Id
    +Kind
    +Layer
  }
  class VisionSceneProjection {
    <<N_candidate>>
  }
  VisionSceneProjection o-- MVIImageViewport
  VisionSceneProjection --> MVIImageSession
  VisionSceneProjection --> MVIImageOverlayStore
  MVIImageOverlayStore o-- MVIImageOverlayItem
~~~

**责任/验收约束：** Overlay是临时视图对象而非工程Undo或结果仓库。交叉能力索引详见01全类目录和03—08专题设计。

### C04 工具交互与阶段

~~~mermaid
classDiagram
  class MPWImageViewerControl {
    <<E>>
    +ShowOriginal()
    +AddOverlays()
  }
  class MVIImageLinePairGesture {
    <<E>>
    +Stage
    +Append()
    +Reset()
  }
  class ToolGestureContext {
    <<N_candidate>>
  }
  class MVIImageOverlayStore {
    <<E>>
  }
  MPWImageViewerControl *-- MVIImageLinePairGesture
  MPWImageViewerControl ..> ToolGestureContext
  ToolGestureContext ..> MVIImageOverlayStore : commit only
~~~

**责任/验收约束：** 已有单独四点状态，其他工具统一须先证实公共变化点。交叉能力索引详见01全类目录和03—08专题设计。

### C05 专业算法描述和参数

~~~mermaid
classDiagram
  class VisionOperationDescriptor {
    <<N_candidate>>
    +OperationId
    +Version
    +Capabilities
  }
  class VisionTypedPort {
    <<N_candidate>>
    +Name
    +TypeId
  }
  class VisionParameterSchema {
    <<N_candidate>>
    +Key
    +Unit
    +Range
  }
  class FIMImageFilters {
    <<E_static>>
  }
  class CxVisionInspectionRuntime {
    <<E>>
  }
  VisionOperationDescriptor o-- VisionTypedPort
  VisionOperationDescriptor o-- VisionParameterSchema
  VisionOperationDescriptor ..> FIMImageFilters
  VisionOperationDescriptor ..> CxVisionInspectionRuntime
~~~

**责任/验收约束：** 不要为了描述符迫使FIM每个静态方法创建空类。交叉能力索引详见01全类目录和03—08专题设计。

### C06 正式Pipeline与统一执行

~~~mermaid
classDiagram
  class MEMEngineeringSession {
    <<E>>
    +Edit()
    +Undo()
  }
  class VisionPipelineDefinition {
    <<N_candidate_resource>>
  }
  class VisionNodeDefinition {
    <<N_candidate_embedded>>
  }
  class MRKExecutionPlan {
    <<E>>
  }
  class MRKExecutionStep {
    <<E>>
  }
  class IMDSDataContext {
    <<E>>
  }
  MEMEngineeringSession --> VisionPipelineDefinition
  VisionPipelineDefinition *-- VisionNodeDefinition
  VisionPipelineDefinition ..> MRKExecutionPlan : compile via MOC
  MRKExecutionPlan *-- MRKExecutionStep
  MRKExecutionStep ..> IMDSDataContext
~~~

**责任/验收约束：** MEM持久唯一、MOC解析编译、MRK执行、MDS存值；无第二Graph/Executor。交叉能力索引详见01全类目录和03—08专题设计。

### C07 运行结果与产物

~~~mermaid
classDiagram
  class VisionRunResult {
    <<N_candidate>>
    +RunId
    +PlanVersion
    +Status
  }
  class VisionResultItem {
    <<N_candidate>>
    +Kind
    +SourceRef
  }
  class VisionImageArtifact {
    <<N_candidate>>
  }
  class VisionMeasurementResult {
    <<N_candidate>>
  }
  class VisionDetectionEvidence {
    <<N_candidate>>
  }
  class CxVisionInspectionResult {
    <<E_limited>>
  }
  VisionRunResult o-- VisionResultItem
  VisionResultItem <|-- VisionImageArtifact
  VisionResultItem <|-- VisionMeasurementResult
  VisionResultItem <|-- VisionDetectionEvidence
  VisionDetectionEvidence ..> CxVisionInspectionResult
~~~

**责任/验收约束：** 类继承仅为类型语义候选；正式评审优先组合/判别联合/Ref。交叉能力索引详见01全类目录和03—08专题设计。

### C08 工程标注唯一真源

~~~mermaid
classDiagram
  class MEMEngineeringSession {
    <<E>>
    +Edit()
    +Undo()
    +Redo()
  }
  class MVIVisualEditor {
    <<E>>
  }
  class MRCVisualItem {
    <<E>>
  }
  class MVIImageOverlayItem {
    <<E_temporary>>
  }
  class VisionAnnotationDefinition {
    <<N_candidate>>
  }
  MEMEngineeringSession --> MVIVisualEditor
  MVIVisualEditor --> MRCVisualItem
  VisionAnnotationDefinition --> MRCVisualItem
  MVIImageOverlayItem ..> VisionAnnotationDefinition : explicit promote
~~~

**责任/验收约束：** 临时轮廓必须显式授权转工程标注，方可持久化。交叉能力索引详见01全类目录和03—08专题设计。


## 状态与生命周期

### ST01 两线测量状态

~~~mermaid
stateDiagram-v2
  [*] --> FirstStart
  FirstStart --> FirstEnd: L1起点
  FirstEnd --> SecondStart: L1合法终点
  FirstEnd --> FirstEnd: 退化/越界拒绝
  SecondStart --> SecondEnd: L2起点
  SecondEnd --> Complete: L2合法终点
  SecondEnd --> SecondEnd: 退化拒绝
  Complete --> FirstStart: 提交Reset
  FirstEnd --> FirstStart: Esc/换图
  SecondStart --> FirstStart: Esc/换工具
  SecondEnd --> FirstStart: 取消/失去捕获
~~~

**责任/验收约束：** MVIImageLinePairGesture已有主要阶段，真实物理鼠标多DPI待验。交叉能力索引详见01全类目录和03—08专题设计。

### ST02 算法任务状态

~~~mermaid
stateDiagram-v2
  [*] --> Created
  Created --> Validating
  Validating --> Rejected: 合同非法
  Validating --> Queued: 资源/版本有效
  Queued --> Running
  Running --> Completed: 输出提交
  Queued --> Cancelled: 取消
  Running --> Cancelled: 协作取消
  Running --> Failed: 异常
  Completed --> [*]
  Failed --> [*]
  Rejected --> [*]
  Cancelled --> [*]
~~~

**责任/验收约束：** 目标状态模型非MRK当前公开枚举；失败不能部分提交。交叉能力索引详见01全类目录和03—08专题设计。

### ST03 工程编辑状态

~~~mermaid
stateDiagram-v2
  [*] --> Viewing
  Viewing --> Draft: BeginEdit
  Draft --> Preview: Preview
  Preview --> Draft: 继续编辑
  Draft --> Validating: Commit
  Preview --> Validating: Commit
  Validating --> Committed: 版本/权限通过
  Validating --> Rejected: 冲突
  Draft --> Cancelled: Esc
  Preview --> Cancelled: 取消
  Committed --> Viewing
  Rejected --> Viewing
  Cancelled --> Viewing
~~~

**责任/验收约束：** 工程编辑采用现有MEM/MTC；Vision专业映射未实现。交叉能力索引详见01全类目录和03—08专题设计。

### ST04 图像流生命周期

~~~mermaid
stateDiagram-v2
  [*] --> Stopped
  Stopped --> Starting: 验证源/预算
  Starting --> Streaming: 成功
  Starting --> Faulted: 失败
  Streaming --> Paused: 暂停
  Paused --> Streaming: 恢复
  Streaming --> Draining: 停止
  Draining --> Stopped: 完整释放
  Streaming --> Faulted: 设备故障
  Faulted --> Starting: 允许重连
  Faulted --> Stopped: 关闭
~~~

**责任/验收约束：** NoDrop必须可背压/节流/持久化，不许无限Queue。交叉能力索引详见01全类目录和03—08专题设计。


## 运行与交互时序

### SQ01 异步加载过期拒绝

~~~mermaid
sequenceDiagram
  actor U as 用户
  participant W as WPF
  participant S as MVIImageSession
  participant R as Reader
  U->>W: 加载A
  W->>S: BeginOriginalLoad()
  W->>R: 解码A
  U->>W: 加载B
  W->>S: BeginOriginalLoad()
  R-->>W: A完成
  W->>S: TryCompleteOriginalLoad(oldVersion,A)
  S-->>W: false
  R-->>W: B完成
  W->>S: TryCompleteOriginalLoad(newVersion,B)
  S-->>W: true/Changed
~~~

**责任/验收约束：** SourceVersion也随SetResult变化，不能视作纯原图Revision。交叉能力索引详见01全类目录和03—08专题设计。

### SQ02 ROI草稿与临时提交

~~~mermaid
sequenceDiagram
  actor U as 用户
  participant W as MPW
  participant G as MVI手势
  participant S as MVIOverlayStore
  U->>W: 画ROI
  W->>G: 原图坐标
  G-->>W: 草稿快照
  W-->>U: 视口投影
  U->>W: 完成
  W->>S: AddRange/Replace
  S-->>W: Changed一次
  W-->>U: 属性和画面更新
~~~

**责任/验收约束：** 通知异常可能在成功提交后出现；不能虚称事务回滚。交叉能力索引详见01全类目录和03—08专题设计。

### SQ03 算法定义编译和执行

~~~mermaid
sequenceDiagram
  actor U as 工程师
  participant V as Vision工作台
  participant E as MEM定义
  participant C as MOC编译
  participant R as MRK执行
  participant D as MDS数据
  participant P as PVC/FIM
  U->>V: 执行参数/流程
  V->>E: 读取固定Revision
  E->>C: 定义与引用
  C-->>V: 执行计划/错误
  V->>R: Invocation
  R->>D: 注入类型输入
  R->>P: 调用专业运算
  P-->>D: 具名输出
  D-->>V: 结果/Artifact引用
~~~

**责任/验收约束：** 正式VisionPipeline到MOC的桥仍是目标，并非已交付。交叉能力索引详见01全类目录和03—08专题设计。

### SQ04 结构化结果复核

~~~mermaid
sequenceDiagram
  actor U as 用户
  participant T as 结果列表
  participant R as VisionRunResult
  participant S as 场景投影
  participant W as MPW
  U->>T: 选结果
  T->>R: Lookup(ResultItemId)
  R-->>T: SourceRef/Geometry/Artifact
  T->>S: 定位原图坐标
  S->>W: 聚焦/高亮
  W-->>U: 展示图和属性
  U->>T: 复核
  T->>R: 派生复核记录
~~~

**责任/验收约束：** 复核记录不得静默篡改原算法结果。交叉能力索引详见01全类目录和03—08专题设计。

### SQ05 正式工程ROI和Undo

~~~mermaid
sequenceDiagram
  actor U as 工程师
  participant W as 工作台
  participant V as MVIVisualEditor
  participant E as MEMEngineeringSession
  U->>W: 转为正式工程标注
  W->>V: 创建编辑意图
  V->>E: BeginEdit/Preview/Commit
  E-->>W: Revision/冲突
  U->>W: Undo
  W->>E: Undo()
  E-->>W: ChangeSet
  U->>W: 保存
  W->>E: 保存正式工程快照
  E-->>W: 保存收据
~~~

**责任/验收约束：** 工程保存应复用MEM拥有的Store，不由图窗直接写XML。交叉能力索引详见01全类目录和03—08专题设计。

### SQ06 在线流、背压和发布

~~~mermaid
sequenceDiagram
  participant A as Automation
  participant Q as 有界队列
  participant V as Vision计算
  participant R as MRK/MDS
  participant I as Inspection
  A->>Q: FrameId/Time/Handle
  alt 容量许可
    Q->>V: 受控Frame
    V->>R: 运行节点
    R-->>V: Result/Artifact
    V->>I: Evidence
  else 满载
    Q-->>A: 背压/显式Drop政策
  end
  V-->>A: 释放租约
  V-->>I: Trace/关联ID
~~~

**责任/验收约束：** 生产流吞吐与HIL在真实设备测量；系统不可无限缓存。交叉能力索引详见01全类目录和03—08专题设计。


## 部署与运行形态

### DP01 独立与嵌入部署

~~~mermaid
flowchart TB
  subgraph Desktop["Windows .NET48桌面进程"]
    A[Studio/TestCenter/独立宿主] --> W[MPW共用控件]
    W --> M[MVI会话]
    A --> V[Vision专业API]
    V --> F[FIM算法]
    V --> R[MRK执行入口]
  end
  P[相机/SDK Provider] --> R
~~~

**责任/验收约束：** 正式独立Vision.exe不是已建项目；需要时复用宿主。交叉能力索引详见01全类目录和03—08专题设计。

### DP02 Runtime与Worker部署

~~~mermaid
flowchart LR
  subgraph Design["Studio"]
    D[定义/编译/发布]
  end
  subgraph Target["现场PC"]
    R[独立Runtime] --> M[MDS Frame]
    M --> W[CPU/GPU/SDK Worker]
    R --> L[FLD诊断]
  end
  D -->|ResolvedManifest| R
  A[相机/控制器] --> W
  W -->|专业结果| I[Inspection/HMI]
~~~

**责任/验收约束：** 不把软实时宣传为硬实时；SDK许可和设备现场必须专项验证。交叉能力索引详见01全类目录和03—08专题设计。

## 7. OOP、GoF、AOP模式裁决

| 模式/原则 | 真实落点与扩展要求 | 禁止事项及证据 |
|---|---|---|
| OOP与组合 | FIMImageBuffer像素自持有；MVI会话/Overlay；MPW图窗；MEM工程；PVC专业计算 | 不把状态、算法、工程与UI揉成VisionManager；跨层依赖审计 |
| Observer | E：MVI会话Changed、MVIOverlayStore.Changed | AddRange验证失败零提交、成功一次通知；监听器异常发生在已提交后 |
| State | E：MVIImageLinePairGesture；复杂ROI/运行状态需按图扩展 | 不为空状态引入一套类；取消/失捕/退化无脏提交 |
| Command | E：MEM/MTC工程编辑和Undo/Redo；运行命令另由平台机制 | 禁止ImageUndoStack；版本冲突/保存重开反证 |
| Composite | E：MRC Group/VisualItem；ROI组合需专业校验 | 不复制工程对象层级 |
| Strategy | 只在真实多个等价可换算法/SDK时评审 | 单算法不得建空IAlgorithmStrategy |
| Registry/Factory | 复用Capability/Provider及现有类型注册机制 | 不造第二套插件中心 |
| AOP | FLDLogScope、操作Trace、耗时/资源、权限与审计 | 热像素循环不得反射织入；CorrelateId跨任务一致 |

## 8. 六条审查门禁

- **身份**：图片、Frame、Mask、ROI、标注、ResultItem、工程Resource、RunId都要求定义相等性和稳定ID；类型之间不靠裸Guid猜语义。
- **坐标**：图像像素中心/ROI边界/物理Spacing/配准后的局部系必须可逆、可测试，未标定不能给毫米。
- **线程**：MVIImageSession自身有锁，但MVIImageOverlayStore注明应由Owner UI线程调用；WPF Dispatcher属于MPW；算法后台线程不能直接写UI。
- **提交**：无效输入、取消或晚到旧请求不可污染有效结果；AddRange的监听器故障不能冒充回滚；工程编辑走MEM事务。
- **运行**：MRK ExecutionPlan与MDS typed ports复用，Vision只定义专业端口与调试语义；帧队列有上限和策略。
- **版本**：模型、算法、参数、校准、采集与产物之间建立完整Ref/Hash/Trace；不能由一个SourceVersion替代所有版本维度。

## 9. 候选对象签名批准前的核对顺序

ImageMetadata/FrameRef/MaskContract先审FIM/FDT；VisionOperationDescriptor/TypedPort先审MPM能力与MRK端口；VisionPipelineDefinition先审MEM/MOL/MOC Resource；VisionRunResult/ArtifactRef先审MDS/FDT/Inspection已有结果类型；SceneProjection/ToolGestureContext先从现有MVI/MPW拆分而不引入新Provider链。每项候选至少需要两处真实消费用例、唯一Owner、替代方案比较、参数与线程/内存边界、测试和ADR。设计图与实际源码不同必须持续更正，不得倒推伪API。

## 10. 版本与测试

U/P/C/ST/SQ/DP各组图号固定；变动对象图必须同步修改01能力契约、相关03—08专题和实施/验收台账。采用C01单图、C02算法、C03配准、C04批次、C05在线、C06 X-Ray/CT、C07工程、C08 AI预留八个反向Case验证；任何通过声明都要求对应Git SHA及Windows/设备实证。
