# CxVisual 通用二维视觉工作台——00 总体架构与设计总纲 V2.0

> **身份**：架构总纲（批准的目标方向；候选对象尚未批准为可编译API）。日期：2026-10-28。用户确认：以UML、全类能力为中心的11文档体系。源码审查时点：cx-visual-x3/master 约 6d0767a；持续开发中的master会变化，实际实施必须重新核实SHA。  
> **最高约束**：DOCS/01《平台灵魂与架构宪法》Review基线、AGENTS.md、工程Skill、DOCS/02～04 Owner与规格；其中大型XLSX本次没有逐Sheet验证，冲突需按其正式Owner复审。  
> **边界**：本次只提交文档；未新增功能、未运行Windows CI、不宣称SDK或真实硬件验收。ReferTo只读。已有V1.0功能矩阵、V1.1 UML及Implementation/Testing历史证据继续有效但须按版本理解。

## 1. 产品定位、约束和目标

CxVisual = 1 Platform + 6 Workbenches。Vision是已有专业Workbench；通用二维视觉是Vision中的专业能力体系，不是第七个Workbench或一个新的公共平台。Vision服务于工业视觉、X-Ray/AOI、CT投影、科研图像、AI预留，可独立查看/实验，可嵌入Studio/HMI，也能被Inspection/CT3D稳定调用。**全类能力可发现、可调用、可组合、可观察、可复核、可工程化、可运行**，不能把“有按钮”当成完成。

四条使用主线：①单图/多图查看与交互；②传统及专业算法实验；③批量/实时工业图像处理；④工程资源、结构化结果和专业协作。三种正交视角：Application（用户任务）/ Resource（持久定义）/ Runtime（执行方式），避免一套类承担全部视角。

## 2. 六群26类能力（唯一索引）

| 群 | 能力类别 | 目的 |
|---|---|---|
| A 数据 | 01输入、02数据模型、03序列数据集、04大图/生命周期 | 接入与安全保存像素、句柄、流 |
| B 呈现 | 05显示、06视口/坐标、07多图比较、08 ROI/标注、09编辑交互 | 多视口、多图层、正确原图坐标 |
| C 算法 | 10基础处理、11增强修复、12分割、13分析、14测量、15匹配配准、16工业检测、17 AI | 通用运算与专业语义严格分离 |
| D 执行 | 18能力/参数、19算法实验/流程、20批量实时 | 组合可发现算法，接入正式运行 |
| E 工程 | 21结果复核、22输出报告、23工程资源 | 结构化结果、版本、复现、统一Undo |
| F 产品 | 24工作台/API、25跨专业、26质量安全性能 | 多形态用户体验与可验证交付 |

逐类二三级规格、编号、端口、状态见[01 全类能力目录](CxVisual_专业视觉体系%23通用二维视觉工作台——01全类能力目录与功能契约_V2.0.md)。02为UML权威设计、03—08专题为详细不变量、Implementation/Testing为实际证据。

## 3. 逻辑架构与唯一Owner

~~~mermaid
flowchart TB
  subgraph Host["产品使用入口"]
    H1["Studio Vision Workspace"] H2["TestCenter"] H3["独立宿主/嵌入WPF/开发者API"]
  end
  subgraph Vision["Vision专业职责"]
    UI["MPW WPF显示与事件"] VI["MVI会话/坐标/工具/临时叠加"]
    PR["PVC 专业算法/检测/SDK"]
    V["Vision能力描述/类型化结果/专业流程语义【候选】"]
  end
  subgraph Platform["已确定共享Owner"]
    F["FIM/FGE 像素/通用几何与算法"]
    E["MEM/MRC/MTC/MPM 工程定义/图元/事务/资源"]
    R["MRK/MDS/F19 执行/数据与并发"]
    O["FLD/配置/命令/权限/诊断"]
  end
  subgraph Peers["专业协作"]
    A["Automation采图/触发"]
    C["CT3D投影/重建"]
    I["Inspection质量判定/复核"]
  end
  H1 --> UI
  H2 --> UI
  H3 --> UI
  UI --> VI
  VI --> F
  UI --> V
  V --> PR
  V --> F
  V --> E
  V --> R
  V --> O
  A --> V
  C <--> V
  V --> I
~~~

**物理工程约束**：28项目/135子方面 Owner为准；V2.0目录里的“控制器”“描述符”“结果”等是职责，不等于需新增项目/接口。具体引用方向以实际csproj DAG和平台规范验证后确定。不得让Foundation反向依赖Professional、MPW或Runtime。

| 对象/责任 | 当前真实锚点 | 需要补足，不得伪称已实现 |
|---|---|---|
| 基础像素 | FIMImageBuffer、FIMImageOperations/Filters/Analyzer/Measurement | 多帧元数据、句柄、缓存、完整算法覆盖 |
| 会话/临时标注 | MVIImageSession Original/Result/SourceVersion、MVIImageOverlayStore | 多产物、多工作文档、正式工程标注投影 |
| 图窗/编辑 | MPWImageViewerControl、MVIImageLinePairGesture | 减少2300+行控件内责任、多工具统一态 |
| 专业检测 | CxVisionInspectionRuntime、CxVisionGrayFrame、CxVisionDataFlow | 与FIM公共像素语义对齐；不存在已批准的通用VisionResult |
| 运行 | MRKExecutionPlan、MDSDataContext及已有F19 | 类型化专业算法/节点、流背压/Worker实证 |
| 工程 | MEMEngineeringSession、MVIVisualEditor、MRC/MTC | ROI/标定/Pipeline正式Resource及保存重开 |
| 可观察性 | FLDLogScope及平台诊断 | 从宿主日志走向任务级追踪、预算、审计 |

## 4. 关键对象语义和数据边界

- **Image / Mask / Region**：图像为不可混淆的像素数据；Mask为限定格式且声明坐标的区域数据；Region为几何或像素集合，变换必须明确；显示LUT与原始像素不可混写。
- **ImageSet / Frame / Stream**：批量数据、持续帧流与内存缓冲各有自己的容量和时间语义。一次帧显示不等于完整实时流系统。
- **ROI / Annotation / Overlay**：MVI的OverlayStore暂存用户会话叠加项；工程标注持有正式MEM身份与修订；算法结果轮廓通过投影显示，禁止静默写回工程。
- **Operation / Parameter / Pipeline**：基础算法函数可以保持静态；只有注册、组合、审计的专业能力需要Descriptor和类型端口；永久定义归MEM，计划运行借MRK/MDS。
- **Result / Artifact**：一次运行的结构化结果包含运行身份、输入引用、参数版本、来源、状态、数据产物及错误信息；巨大像素与中间数据通过ArtifactRef/DataHandle，不能内嵌到工程JSON/XML。
- **Definition / Runtime**：设计期草稿、已解析/编译不可变计划、运行中状态、产物必须不同对象及生命周期。Studio关闭时生产Runtime仍应独立运行。

## 5. 主生命周期与协同

~~~mermaid
flowchart LR
  D["MEM Vision资源定义"] --> V["校验/解析/编译"]
  V --> P["MRK执行计划"]
  P --> X["MDS类型化数据/专业Provider"]
  X --> R["VisionRunResult/Artifact【候选】"]
  R --> W["MVI/MPW可视复核"]
  R --> O["报告/Inspection/CT3D"]
  W -. "用户确认编辑 → MEM事务/新Revision" .-> D
~~~

完整操作应包括输入验证、参数校验、取消与异常、结果身份、界面反馈、导出及复现。失败结果不得覆盖已提交的有效结果；晚到的旧请求不得覆盖新结果；只有工程提交会更新持久定义。命令/状态/事件/流严格分义，不形成万能消息总线。

## 6. OOP / GoF / AOP原则与成本

1. **OOP**：类有唯一职责和所有权，组合优先；小功能不造空Facade、Adapter、Manager；同一个运行概念只保留一份真源。
2. **GoF**：State在多阶段工具/运行任务；Observer在会话/结果通知；Command落已有MTC/MEM编辑历史；Composite复用MRC Group；Strategy只在真实的可替换算法存在后引入；Factory/Registry优先现有Capability/Provider注册。每个模式必须绑定真实变化点。
3. **AOP**：共用FLD、Trace、权限、性能、计量、资源审计，埋点设在执行边界/任务生命周期；不在逐像素热循环做反射、代理或日志。每个运行带CorrelationId、输入和算法版本/Hash、帧身份、耗时、内存预算及结果引用。
4. **负面门禁**：不建第二工程树、第二Undo、第二DataStack、第二通用Executor、第二CommandCenter、第二Provider中转层；不将厂商SDK私有句柄放入Foundation。

## 7. 用户界面与开发者交付

统一Studio/TestCenter视觉底座：资源/对象树、主图窗多视口/算法流程、上下文属性与操作、结果表/曲线/图表、诊断/任务面板。Ribbon基于上下文与Capability显示适用功能，快捷键/右键/搜索一致。独立程序与嵌入式WPF控件复用同一功能控制与正式API，不直接复制TestCenter宿主页面；TCT属于验收宿主而非业务内核。

## 8. 工业级质量门禁与目标指标

需要真实基准：输入格式矩阵（Gray8/16/Float32/BGR等）、坐标/Spacing/多DPI、复杂ROI、CPU/GPU显存、图像从百万像素至约4300²及更大目标、重复加载、连续帧背压/掉帧/NoDrop、长时间不泄露、异步取消/晚到结果、工程保存重开、日志追踪与正式设备HIL。**任何具体吞吐、延迟、GB/S、内存上限均需测量后定阈值；现阶段不得冒称达成。** 以TestCenter专项与Windows .NET48 x64真实编译、CI、专业同样本、实机验证层层升格。

## 9. 八个反向攻击案例（必须做成真实端到端Case）

| Case | 闭环 | 关键反证 |
|---|---|---|
| CASE-01 单图 | 图→ROI→灰度/测量→导出 | 位深/原始灰度、异常ROI |
| CASE-02 算法试验 | 增强→分割→轮廓→拟合→重跑 | 输入输出类型、旧结果不污染 |
| CASE-03 配准比较 | 双图→变换→差异→定量 | 坐标/Spacing误用 |
| CASE-04 序列批处理 | 帧集→同Pipeline→批量结果 | 顺序、取消、背压 |
| CASE-05 在线视觉 | 设备→有界队列→算法→Inspection | 机台掉线、持续运行、非硬实时 |
| CASE-06 X-Ray/CT | 投影序列→ROI分析→CT3D | 多来源身份/坐标引用 |
| CASE-07 正式工程 | 定义→Preview→Commit→Undo→保存重开 | 版本冲突/旧引用/变更授权 |
| CASE-08 AI组合预留 | 传统预处理→模型推理→结果 | 模型版本/可回退；当前不实施AI引擎 |

## 10. 文档治理与状态

- **全类目录**：01唯一功能ID与意图，禁止在专题重新分配号码。
- **UML总规范**：02统一类、关系、图号与E/R/N/P状态；专题只细化，不颠倒Owner。
- **专题设计**：03数据、04交互、05算法、06结果/工程、07运行、08工作台；设计接口均为候选直到源码与Owner评审。
- **事实台账**：Implementation逐SHA记录代码和旧功能复用；Testing逐SHA记编译、数值、WPF及实机，历史证据不得推及最新master。
- 变更26类定义或跨Owner对象须更新01/02、写ADR、检查对应专题和验收；本版批准的是整体目标和组织方式，不预批准每个新类名和签名。


## 11. 架构深化：四平面、唯一真源和Owner裁决（2026-10-28）

> **性质：设计深化，不是已实现功能。** 此章节细化前述V2.0主设计，编译API和新增类型须在真实源码及DOCS/02—04 Owner审核后定案。重点是先把基础对象与关系设计好，之后再扩张260项能力。

### 11.1 同一Vision工作台的四个逻辑平面

| 平面 | 正式持有者 | 核心对象和生命周期 | 禁止 |
|---|---|---|---|
| Definition 工程定义 | MEM/MOL/MPM/MRC/MTC | MEMContent、MPMTypeDescriptor、MOLWiringDefinition；Edit/Undo/Save，版本化且持久 | 不新建Vision第二工程树/第二Undo |
| Execution 运行执行 | MOC/MRK/MDS/F19 + PVC算法 | MOCCompiledPlan→MRKExecutionPlan→MDS DataContext→专业计算；版本固定、可取消 | 不新建VisionGraphExecutor/第二DataStack |
| Evidence 结果证据 | Vision专业结果语义 + 平台Artifact机制 | RunId、具名类型输出、Image/Mask/Measurement/Defect/Trace与引用；发布后不可原地改写 | 不把结果塞进Viewer双槽或工程定义 |
| Presentation 可视交互 | MVI + MPW + 正式宿主 | Viewer当前Original/Result、Viewport、临时Overlay、用户草稿；按工作台结果Ref投影 | 不把MPW控件当专业算法/持久数据仓 |

**贯穿身份链**：ResourceRef/DefinitionRevision → InputRef或FrameId → Invocation/RunId → ResultItemId/ArtifactRef → Review/ReportRef，CorrelationId贯穿诊断链。UI可重新选择任意结果但不能因此修改原算法结果；正式编辑必须通过MEM事务。

~~~mermaid
flowchart LR
  D["MEMContent / MPM Type<br/>MOL定义/ROI引用"] --> C["MOC解析编译<br/>SourceMap"]
  C --> X["MRK Plan / MDS<br/>固定Revision/预算"]
  X --> A["FIM/PVC<br/>专业计算"]
  A --> R["Vision Run结果<br/>TypedOutput/Artifact"]
  R --> V["MVI/MPW<br/>按Ref显示"]
  R --> I["Inspection/CT3D/HMI"]
  V -. "确认编辑意图" .-> D
~~~

### 11.2 真实既有类型与新增对象的判定

| 需求职责 | 必须首先复用的真实类型 | Vision可以增加的部分；仍须核实 |
|---|---|---|
| 视觉工程资源 | MEMContent、MEMEngineeringSession、MPMTypeDescriptor、MPCPortDescriptor、IMPMTypeProvider | Vision专用TypeId、参数字段、图像格式和校准约束；不单独持久化VisionPipelineDefinition |
| 可执行流程 | MOLWiringDefinition、MOCEngineeringCompiler、MOCCompiledPlan、MOCSourceMap | 专业节点的能力语义、类型/图像/ROI校验、专业调试投影；不复制Graph |
| 算法调度和端口 | MRKExecutionPlan、MRKExecutionInput/Output、MRKExecutionStep | Image/Mask/Geometry的专业Type约束与版本/结果契约；不复制端口基础 |
| 运行数据/资源 | FDTDataAdapterRegistry、MDSDataContext、MDSDataBudget、MDSBoundedByteStream | 专业像素安全捕获、字节估算、FrameLease/Backpressure；不是新的Heap/Stack |
| 图像像素 | FIMImageBuffer、CxVisionGrayFrame（两种现存不同用途的数据表达） | 互通映射、元数据/来源、位深/Stride/复制及性能契约；未审核不建第三种基础像素类型 |
| 视觉会话 | MVIImageSession双槽、MVIOverlayStore、MPWViewer | 多资产索引与结果选择属于工作台；正式ROI经MEM/MRC，不让Result2/Result3无限增长 |
| 专业计算 | CxVisionDataFlow、CxVisionThresholdProvider、IPVCComputeModule | 算法能力发现、专业执行、结果映射与第三方SDK；不强迫所有FIM静态算法成为模块 |

**容量语义限制**：当前MDSBoundedByteStream是本机字节块流，每块有上限，不是含采集时间戳/FrameLease/DropPolicy的工业帧流。MDSDataBudget目前只覆盖FlowHeap和DataStack已提交托管载荷，尚未包含算法临时工作区、额外图像副本和GPU显存。设计必须复用其机制，但不能虚称完整流/大图已经验收。

### 11.3 六项架构闭环必须先行

1. **Image→TypedOperation**：采用现有FIM像素与PVC专业输入，将参数Schema/能力版本/类型合同映射MPM/端口而非自建注册库。
2. **Operation→Result**：每次Run产生稳定RunId及多具名结果，图、Mask、轮廓、测量与Trace可单独访问，错误/空/取消各有状态。
3. **Result→View**：工作台依据ResultItem/SourceRef选图，向MVI双槽投影；图窗不持久化算法输出。
4. **View→Engineering**：临时ROI显式转正式MEM/MRC对象，Edit/Preview/Commit/Undo/Save/Open一致；算法结果默认不改工程。
5. **Definition→Runtime**：MEM/MOL→MOC编译→MRK执行→MDS存值；运行版本固定且Studio关闭后仍能使用已发布Plan。
6. **Stream→BoundedRun**：帧身份、来源、字节预算、背压、丢帧策略、设备断开和Buffer租约明示；软件软实时不能替代硬实时控制。

### 11.4 十二个架构决议及下一道门禁

| 决议 | 确定的方向 | 证据条件 |
|---|---|---|
| D2-01 | 全类是能力分类，不是26个Manager或项目 | 26→UML→Owner双向追溯 |
| D2-02 | 正式视觉流程表达优先MEMContent/MOL/MPM | 创建、保存、重开类型化内容 |
| D2-03 | Vision使用MPC/MRK现有端口合同 | Image/Mask/Measure类型反例 |
| D2-04 | 正式流程用MOC编译，MRK执行，MDS存值 | 真实2—4节点异步Trace |
| D2-05 | 通用FIM与专业CxVisionGrayFrame先明确互通 | 8/16位、复制/所有权/性能测试 |
| D2-06 | MVI双槽保留Viewer职责，不充当结果仓 | 三个以上产物切换不丢身份 |
| D2-07 | 结果与工程标注分离 | 结果选择→显式Promote→Undo/Save |
| D2-08 | Result以RunId+ItemId+SourceRef索引 | 多类型结果复核和一致导出 |
| D2-09 | 时间戳/FrameId/SequenceId独立于图窗状态 | 序列/乱序/设备断开反证 |
| D2-10 | 大图有明确句柄、容量及释放责任 | CPU/GPU峰值和压力测试 |
| D2-11 | Vision检测候选≠Inspection最终质量判定 | 跨专业Invocation与权限 |
| D2-12 | 运行权限、日志、版本、诊断是横切合同 | 失败/取消/重试/过期可追踪 |

**以上决议确定架构方向，不代表批准新类的具体名称和公开签名**。每个候选新增类型都必须通过真实Owner复用审计、两个独立消费者、生命周期/并发反证以及依赖DAG检查。

### 11.5 先设计完成的四个Gate

- **Gate-A 核心对象**：八个核心概念（Image、FrameRef、ViewState、ROI、Operation、Pipeline、Result、Resource）有明确身份/写入权/生命周期；每条跨域关系有Owner。
- **Gate-B 数据与交互**：原图、结果、Mask、标注的坐标/版本/所有权统一；异步切图旧结果无法污染新图；图窗仍使用同一MVI/MPW。
- **Gate-C 流程与工程**：Vision专业类型→已有MPM/MOL→MOC/MRK/MDS→结果/Trace；工程保存和发布保持单一真源。
- **Gate-D 开放及工业**：通用/专业SDK的扩展点、真实批次/流、资源预算、安全与跨专业协议均具反证测试设计，未验证项不写为通过。

*在A/B/C Gate的UML、契约、典型用例审定前，不应一次性批量创建260组实现类型。*

## 12. 核心契约设计收敛与评审结论（2026-10-28）

> 这是§11四平面/十二决议的进一步实施级解释，以下结论属于**架构方向已收敛、合同仍待代码验证**。正式API签名、新增C#类型、性能阈值和工业现场可用性不能仅凭本节宣称已批准/实现。02§13的Y01—Y04是这三条主线的跨体系UML，03§9、05§11、06§10提供详细对象与反证。

### 12.1 “全类能力”需要共享三个可持续的核心合同

| 纵向合同 | 输入到输出的明确对象链 | 当前已存在的Owner与真实类 | 仍待确认的最小专业对象 |
|---|---|---|---|
| **数据合同（Data）** | 源设备/文件→像素快照→Frame/ImageSet引用→Mask/Artifact | FIMImageBuffer/FIMPixelSpacing、FDTReadOnlyBuffer、MDSDataContext/数据预算、MVIImageSession | Frame身份/时间戳、Mask空间、Artifact/Lease和大图生命周期 |
| **算法合同（Operation）** | 工程TypeId/端口/参数→专业算法Capability→MOC编译→MRK/MDS运行 | MPMTypeDescriptor/MPCPortDescriptor、MOCCapabilityDescriptor/MOCFlowBuilder、MOLWiringDefinition、MOCCompiledPlan、MRKExecutionInput/Output、PVC Provider | TypeId→CLR Type映射、Format/ROI/单位校验和专业结果关联 |
| **结果合同（Evidence）** | RunId/固定输入→多具名类型结果→ArtifactRef/Trace→图窗/报告/Inspection | CxVisionInspectionResult、MDS数据提交机制、MVIOverlayStore、MEM/MRC/MTC工程编辑 | 专业RunRecord/ResultItem/ReviewRecord与Artifact存储归属 |

**关键依赖**：这三条合同并非三个独立Project，互相靠稳定TypeId/SourceRef/ResourceRef/RunId衔接；所有数据大对象走已有数据机制，工程持久定义仍由MEM唯一管理。

### 12.2 对对象架构的“可定/暂缓/禁止”裁决

| 决策 | 状态 | 理由 |
|---|---|---|
| 26类260项能力编号与四逻辑平面 | **方向已定** | 全类目标与真实平台Owner对应，无新增项目需求 |
| 工程型VisionPipeline优先MEMContent+MOL | **方向已定** | 已有工程/Logic唯一真源、MOC编译/SourceMap |
| Operation端口优先MPM/MPC/MRK现有合同 | **方向已定** | 已有完整工程端口与运行期CLR端口；Vision只补专业校验 |
| FIM与PVC双像素类型的互通策略 | **待数值/内存验证** | Gray8/16语义可对照，但不同Stride/格式和复制策略未全部实测 |
| 专业FrameRef/MaskRef/ArtifactRef具体CLR结构 | **暂缓具体类名** | 需检查FDT/MDS资产引用/内存生命周期，防止并行对象 |
| VisionRunRecord/ResultItem具体结果Schema | **方向明确、签名暂缓** | 需验证与现有专业Result、MDS产物/Report/Inspection兼容 |
| SceneProjection/WorkbenchController类 | **非必须** | MVI/MPW+宿主先组合，只有跨两个以上真实消费方才抽象 |
| 第二Graph/Undo/数据堆/调度器/注册库 | **禁止** | 违反宪法与28项目135子方面唯一Owner |
| 软件运行调度等于设备硬实时安全 | **禁止** | Runtime与PLC/控制器权限和工业安全边界不同 |

### 12.3 完整专业工作台的第一条验收场景

**CASE-ARCH-01（Gray16专业算法实验）**：

1. 文件/内存引入含Stride的Gray16，保留原像素、SourceId、Format、Spacing状态。
2. 在MPW中使用MVI原图坐标画ROI，生成临时草稿，尚不写工程。
3. 使用专业TypeId/Capability目录确定增强、阈值、连通域和测量四类Operation，参数经过MPM Schema校验，端口映射已有MPC/MRK。
4. 如果用户选择保存算法流程，则通过MEMContent+MOL写入正式资源，MOC按固定Revision编译；若只做单算法快速预览，可直接调用既有FIM函数，但也应使用相同输入/结果语义。
5. MRK执行，MDS/FDT安全捕获数据、管理已提交载荷预算，PVC/FIM计算获得Image/Mask/Region/Measurement不同输出。
6. 专业Run结果带RunId、ResultItemId、SourceRef、参数/算法版本、单位、Trace，结果与中间Mask可逐项查看。
7. 用户切换图像/参数后旧Run不得更新当前图窗，历史Run依然可查询；结果投影不能把另一图的Mask叠到当前图。
8. 用户显式把ROI写为工程标注时走MEM/MTC事务、Undo和保存重开；人工复核作为派生记录，不改写原始算法结果。
9. 未标定时不能输出毫米数值；异常、取消、空结果、失效引用和无效权限得到明确不同状态。

**其价值**：一个用例同时检验数据、算法、结果、Viewer、工程、调试、API及数值正确性；这才是验证“一个架构支撑26类”的最小代表性闭环，不能只开发一个大型UI工具面板。

### 12.4 下一阶段开始编码前，必须完成的评审清单

- 重新核对最新master、DOCS/02—04中相应Owner Sheet和代码DAG，确认候选专业类型确实没有重名实现。
- 为CASE-ARCH-01填完整的输入/输出类型表和每个节点的MPM TypeId、MOC CapabilityId、MRK DataOutput映射；当前示例ID不是已注册项目代码。
- 决定Image/Mask、RunResult及Artifact的可序列化范围和安全复制/借用模式，给出线程、内存、失效/取消与异常处理。
- 定义同样本数值真值与Windows .NET Framework4.8 x64的专项构建、FIM/MVI/MRK/MDS测试、真实WPF/UIA证据。
- 对八个端到端CASE-01—08检查共用合同的缺口，允许专业扩展但拒绝重复平台机制。

**明确当前进展**：完成架构主线和UML关系设计，未把这套新增候选编译成正式公共API；对应Windows CI、生产吞吐、设备与SDK专项验收仍待实际代码阶段完成。
