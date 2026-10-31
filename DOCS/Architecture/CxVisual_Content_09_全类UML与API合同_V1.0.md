# CxVisual Content 体系 #9——全类对象视图、UML、Owner 与开发 API 合同 V1.0

> 状态：审评稿/实施指导，不是全部 API 已发布。配合 #0～#8、DOCS/04 V6.4、Symbol #7 V2.0。正式新增任何类型/公共成员前必须逐 Sheet 校对 DOCS/02～04，检查当前 master 的实际类签名和 csproj；避免同时并行改造的冲突。当前已核查部分重要源码在 Mechanism/Core/Engineering、Mechanism/Decision、Mechanism/Runtime、Applications/Studio。

## 01. 对象设计的方法和禁令

从 CNT-G01～G08 的“用户动作→工程变化→公开合同→构建/运行→证据/回滚”反推最小对象。先检索是否已有类/方法/Owner，优先组合与增强，不用“每个功能造一类”的方式满足类清单。候选类名只描述可能职责，不能直接作为开发任务清单。

- OOP：MEM/MRC Composite 负责包含；定义/实例/运行状态分层；ISP 保留纯视觉、纯数据等小需求可用。
- GoF：Facade（Content 公开能力）、Adapter（专业/历史 Provider/Viewer）、Strategy（资源/绑定/算法）、Command（MTC 原事务）、Factory/Builder（可信 Descriptor/MOL 编辑）、Observer（既有运行数据/消息）、Composite（拥有结构）、Flyweight/引用（共享定义多实例）。
- AOP：Schema 校验、版本、权限/安全、日志审计、Trace、质量/预算和异常边界跨面统一施加；不得在 UI Button 或 Symbol Action 中绕过受控 Host。

## 02. 对象与引用完整 UML

~~~mermaid
classDiagram
 MEMNode <|-- MEMProject
 MEMNode <|-- MEMPage
 MEMNode <|-- MEMContent
 MEMNode <|-- MEMSymbol
 MEMNode <|-- MRCVisualItem
 MRCVisualItem <|-- MRCGroup
 MRCVisualItem <|-- MRCElement
 MEMProject *-- MEMPage
 MEMProject *-- MEMContent
 MEMProject *-- MEMSymbol
 MEMPage *-- MEMContent : RootContent
 MEMContent *-- MEMContent : embedded
 MEMContent *-- MRCVisualItem : visual
 MEMContent o-- MRIResourceReference
 MRIResourceReference --> MEMContent : TargetId
 MRIResourceReference --> MEMSymbol : TargetId
 MEMSymbol *-- MRCGroup : RootGroup
 MEMContent o-- MPMAspect
 MEMContent o-- MPMProperty
 MEMContent o-- MPCPort
 MEMContent o-- MPCConnection
 MEMContent o-- MRCPublicMapping
~~~

本图表达持有/关联，不暗示 MPMProperty 真正作为 MEMNode 子节点；MPC Port/Aspect 是从属于定义的记录。实际类名/父类必须以源码为准。

## 03. 逻辑、能力、运行、数据 UML（角色协作非 DLL 依赖）

~~~mermaid
classDiagram
 MEMContent --> MOLLogicDefinition : schema1
 MEMContent --> MOLWiringDefinition : schema2 exclusive
 MOLWiringDefinition o-- MOLControlBlock
 MOLWiringDefinition o-- MOLLink
 MOLWiringDefinition o-- MPCPortDescriptor
 MOCFlowBuilder --> MEMContent : Edit/Invoke
 MOCEngineeringCompiler --> MEMContent : Read snapshot
 MOCBuildService --> MOCEngineeringCompiler
 MOCBuildService --> MOCSourceMap
 MOCBuildService --> MRKExecutionPlan : result
 MRKExecutionEngine --> MRKExecutionPlan
 MRKExecutionEngine --> MDSDataContext : scope
 MRKExecutionEngine --> IMRKCapabilityResolver : runtime resolution
 MOCCapabilityCatalog --> MOCCapabilityDescriptor : static
~~~

允许逻辑 schema1/schema2 二选一；图中两条线表示支持两种定义格式，**不是同时拥有两份载荷**。Runtime Resolver 与 MOC 静态描述必须可对应但不共用活动委托。

## 04. 已有且优先复用的真实类/API 清单

| 类型/对象族（现有） | 位置/Owner | Content 使用职责 |
|---|---|---|
| MEMProject / MEMPage / MEMContent / MEMSymbol / MEMNode | Mechanism.Core Engineering / MEM | 工程根、RootContent、定义、内嵌、共享 |
| MRCVisualItem / MRCGroup / MRCElement / MRCRectangle | Core Engineering / MRC | Content 内视觉结构和 Symbol RootGroup |
| MRIResourceReference / MRIReferenceKind / MRIPlacement | Core Engineering / MRI | 独立 Reference/Target/Override/Placement |
| MEMEngineering / MEMEngineeringSession | Core Engineering / MEM | Create/Open/Edit/Save/Change/引用与事务 |
| MTCEditSession / EditResult / ChangeSet | Core Engineering / MTC | 一次合法事务、Revision、Undo/Redo 接入 |
| MPMDefinitionState / MPMAspect / MPMAspectKind | Core Engineering / MPM | 四方面单一持久声明 |
| MPMProperty / MPMBinding / MPMTypeDescriptor | Core Engineering / MPM | 公开属性、类型与值来源 |
| IMPMTypeProvider | Core Engineering / MPM | 可信类型注册入口 |
| MPCPort / MPCPortDescriptor / MPCConnection | Core Engineering / MPC | 端口、类型、方向、合法连接 |
| MRCPublicMapping | Core Engineering / MRC/MPM | 内外成员公开映射 |
| MOLLogicDefinition / MOLControlBlock | Core Engineering / MOL | schema1 结构化语义 |
| MOLWiringDefinition / MOLLink / MOLBlockEndpoint | Core Engineering / MOL | schema2 Block/Link/输入输出语义 |
| MOLExpression / MOLStepPolicy / MOLStateMachineDefinition | Core Engineering / MOL | 条件/策略/状态逻辑声明 |
| MOLLogicCodec / MOLWiringCodec | Core Engineering / MOL | 同一 Logic Aspect 读写/版本与 Schema |
| MTITransferPlan / MTIExtractionResult / IMTIAspectRemapper | Core Engineering / MTI | 模板、包、提取、身份与 Aspect 映射 |
| MFMModuleDescriptor / MFMModuleKind | Core Modules / MFM | Connector/Driver/Compute/Simulation/Service |
| MOCCapabilityDescriptor / IMOCCapabilityProvider | Mechanism.Decision / MOC | 静态能力及端口描述 |
| MOCCapabilityCatalog / MOCFlowBuilder | Mechanism.Decision / MOC | 只读发现、便捷逻辑编排 |
| MOCEngineeringCompiler / MOCBuildService / MOCSourceMap | Mechanism.Decision / MOC | 解析、校验、编译、来源映射 |
| MRKExecutionPlan / MRKExecutionEngine / MRKRuntimeSession | Mechanism.Runtime / MRK | 固定计划、执行/控制与运行身份 |
| IMRKCapabilityResolver / MRKFlowHandle / MRKExecutionTrace | Mechanism.Runtime / MRK | 实际 Provider 解析、Flow/Trace |
| MDSDataContext / MDSDataStack / Heap/Frame 相关族 | Mechanism.Data / MDS | 运行数据隔离与生命周期 |
| FCMConfigSectionAttribute / FCM Schema、MHS 相关族 | 现有配置 Owner | 默认/保存/生效/审计配置 |
| MVIVisualEditor / MVISceneProjection / SceneView | Mechanism.Visual/Presentation.Wpf | 图元/场景/交互呈现 |
| StudioShell.SharedResourceCenters / StudioShell.ResourceDocuments | Applications.Studio | Content 与 Symbol 双中心和编辑导航 |
| StudioShellSelfTests / ArchitectureSmoke | Tests/Studio | STA、工程、编排与回归证据 |

目录或文件名可能随并行开发变化；表中并非承诺每个类型都具备完整 Content 一站式功能。对缺失或未验证的方法，不能由现有类名直接推定“完成”。

## 05. 现有可核对的开发者 API 示例

~~~csharp
// 使用现有 MEM 会话创建共享定义，并在 Page 中放置引用
MEMContent inspection = session.AddContent(session.Project, "Inspection");
MRIResourceReference station = session.PlaceContent(page.RootContent, inspection, "InspectionStation");
// 注意：此处仅定义和放置，不自动创建一次运行 Invocation。

// MOC 编辑期提供 InvokeContent(MEMContent)；创建逻辑之前需拥有
// 合法 Logic Aspect / 公开 FlowInputs/FlowOutputs 或 V1 可支持的公开端口。
~~~

对外 Port/Property/Aspect 应使用既有 MTCEditSession 合同方法 DefineProperty、DefinePort、ExposePort、SetAspect、Connect、SetOverride（以当前源码精确签名为准），而不是在 Content 类外直接追加自定义 XML 字段。保存重开、版本检查、未知 Schema、Undo 是 API 的验收部分。

## 06. 候选增量：只在真实缺口被反证后批准

| 角色（候选，不是已发布类名） | 优先实施 Owner | 优先复用/触发条件 |
|---|---|---|
| ContentCreationPreview/Result | MEM/MTC/Studio 薄视图 | 现有 Session/MTI 无法表达七类创作差异时 |
| ContentExtractionPreview/Result | MEM/MTI/Studio | 现有 ExtractReusable 不能完整校验跨界 Logic/Port/Aspect 时 |
| ContentPublicContractProjection | MEM/MPM/MPC 只读查询 | 公共合同目前需在多源重复遍历且不一致时 |
| ContentCapabilityAssociation（值记录） | MPM/MPC + MOC/专业 Owner | 现有 Type/Capability 显式关联无法保真时 |
| ContentCompositionValidation（服务行为） | 原 MEM/MOC/Decision | 无法由现有 Validate/Build 聚合错误定位时 |
| ContentAuthoringWorkspace（UI 角色） | Studio/MPW 复用原 Editor | 现有 ResourceDocuments 不满足完整四方面体验时 |
| ContentRuntimeObservation（只读投影） | Studio + MRK/MDS 现有 API | 无法回溯 Definition→InstancePath→Run 时 |

**明确禁止**：ContentRegistry、ContentGraphStore、ContentFlowEngine、ContentDataHeap、ContentConfigStore、ContentCommandBus、ContentRuntimeManager、ContentViewerEngine、ContentUndoStack。若有人提议，先提出真实无法由既有 Owner 组合解决的失败用例，再经 ADR 才可能变更。

## 07. API 行为合同矩阵

| 操作 | 前置 | 输出/异常 | 持久化与线程 |
|---|---|---|---|
| AddContent/AddTypedContent | 合法 Parent、类型/Revision/权限 | 新 Content Id；非法 Kind 拒绝 | 一次 MEM 编辑事务 |
| PlaceContent | 目标 Shared Definition/合法 Owner | MRI RefId/Target；冲突拒绝 | 不复制子定义 |
| Set/Expose Port/Property | 公开名、类型、方向/映射合法 | 稳定 MemberId/PortId | 原 MTC Revision/Undo |
| MOCFlowBuilder.Edit | 固定工程快照、合法 Logic Scheme | 同一 MOL Aspect 修改 | 不独立保存 Graph |
| InvokeContent | 子内容/可公开 Port、版本/引用合法 | BlockId/必要端口推导；不合法拒绝 | 需独立的 Reference 与真实调用边 |
| PreviewExtract | 合法子树/作用域/无损映射 | IdMap+诊断/受影响依赖 | 只读，不改 Revision |
| CommitExtract | Preview 未过期/授权 | 共享定义+Ref+一次 Undo | 现有 MTI/MTC，跨 Owner 分阶段 |
| Validate/Build | Snapshot+能力目录/Config/专业约束 | 诊断/Plan/SourceMap | Decision 只读 |
| Publish/Run | Host 准入、安全/有效配置/实际 Resolver | PlanPin/RunId/Trace/Outcome | Runtime/专业，不回写 MEM |
| Upgrade/Retire | 版本影响/已发布依赖、权限 | 迁移预览/新版本/回滚 | 不热改在途 Run |

表中 PreviewExtract、ContentContractProjection 等为**要求的行为语义**，并非宣称已有同名公开方法；实际新增签名必须与 Owner 对齐后冻结。

## 08. 四个必须可实现的时序

### 8.1 创建/配置

~~~mermaid
sequenceDiagram
 actor U as Author
 participant UI as Studio
 participant C as MEM/MTC
 participant CAT as MPM/MOC catalog
 U->>UI: choose source/type
 UI->>CAT: Describe/Find (read only)
 CAT-->>UI: schema/known/unknown
 U->>UI: confirm create/override
 UI->>C: Edit (single transaction)
 C-->>UI: Definition/RefId+Revision
~~~

### 8.2 从业务组合提取

~~~mermaid
sequenceDiagram
 actor U as Author
 participant UI as Studio
 participant MTI as MTI+MTC
 U->>UI: select structure/logic
 UI->>MTI: Preview IdMap/ports/aspects/dependencies
 MTI-->>UI: exact loss/errors
 alt can convert
 U->>UI: approve replace/copy
 UI->>MTI: Commit guarded by Revision
 MTI-->>UI: DefId+RefId/Undo token
 else loss cannot be proven
 UI-->>U: Rejected with reason (project unchanged)
 end
~~~

### 8.3 真正调用/回流

~~~mermaid
sequenceDiagram
 participant C as MEM Content
 participant D as Decision/MOC
 participant H as Host/MRK
 participant P as Professional
 participant M as MDS/Trace
 C->>D: fixed snapshot + MOL
 D-->>H: Plan+SourceMap
 H->>H: version/config/permission gates
 H->>P: invoke capability (if allowed)
 P-->>M: real result/state/quality
 M-->>H: committed output/trace
 H-->>C: observation projection only
~~~

### 8.4 升级与在途计划
作者编辑 Definition 新 Revision → MTI/MEM 分析引用/端口影响 → MOC 重新编译候选 → Host 等待授权安全点发布 → 新 Flow 用新计划，旧 Flow 使用固定旧版本完成/受控停止。任何失败均不抹去旧 Trace 和配置证据。

## 09. 对 DOCS/04 的逐项审核与质量门禁

实际开发前制作由“需求编号→既有类/实际行号→成员/签名→目标增强→Owner→UML关系→正反例→CI”组成的追踪表。核实 C#7.3/.NET Framework4.8/WPF 兼容、线程/异常/内存边界、稳定 GUID、Schema 迁移和性能；没有真实源码的候选不得标 Implemented。每个组件需要至少一条正证和一条破坏性负例，不因简单 demo 能跑而宣布完整功能。
