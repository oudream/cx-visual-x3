# CxVisual Decision V2.0——全类需求反推与 OOP / GoF / AOP 架构审评 V1.0

> **文档性质：正式审评与修订依据，不是第八份并列架构真源，不是源码实现或工业验收。** 取样 master：`564b5d7d9516646cd668f165c3cb96226b42eb89`（2026-10-29）；提交时应复核新 HEAD。V2.0唯一技术权威仍是 [#0总纲](CxVisual_统一决策与规划体系%230总体架构与全类需求_V2.0.md) 与 [#1～#6](CxVisual_统一决策与规划体系%231对象合同与开发者API_V2.0.md)；本报告给出审核事实、未决点、缺口、反证与验收门槛。重要修正会回填原专题，不在本报告发布第二套接口。
>
> **本轮边界**：不修改正式C#源码、csproj、sln，不执行Logic/Orchestration项目合并。不把DOCS/02～04 XLSX的Sheet内容视为已读；进一步Owner争议必须在落地前逐Sheet核验。复用ReferTo须逐真实源码查证，不把参考代码记作已实现。

## 00. 审查为什么存在

判定目标不是“十大能力名称齐全/文档行数足够”，而是**六专业 × 两类用户 × 工程全生命周期 × 三个判断 × 十项能力 × 安全/效率/可解释性**能否形成最少代码与操作的真实闭环。任何抽象必须通过“如果移除此类/服务，哪条用例受损”反证；任何需求必须落到入口、输入、Owner、输出、实际应用、异常/资源边界及测试。基本原则仍为：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

**五个不允许混淆的等级**：设计存在 / 有源码 / 可由调用方实际使用 / 已有匹配SHA的自动测试 / 实机HIL或现场经过专业Owner批准。新增本报告只提高“设计完整度”，不提高任何功能的实现证据级别。

## 01. 按六层反推并双向审计

~~~mermaid
flowchart TB
  A["专业真实任务与开发者期望"] --> B["完整使用链与七问"]
  B --> C["必需功能、约束/策略与D01-D10"]
  C --> D["最小领域对象/接口/Owner/GoF/AOP"]
  D --> E["真实源码/依赖DAG/旧API兼容"]
  E --> F["正例/反例/压力/UX/实机证据"]
  F -.->|"验证不成立，重新收敛"| A
  D -.->|"无真实场景则删除抽象"| C
~~~

**七问字段**：做什么、何时、在哪里、谁调用、怎样调用、输出给谁/由谁采纳、实际效果如何。每一项计划实现都必须回答并通过反向映射：`ScenarioId → RequirementId → Dxx → Type/Owner → API/Entry → TestId → Evidence`，反向不存在孤儿类/重复Owner。

### 1.1 六专业的真实代表任务与必须体现的差异

| 专业用例 | 简便接入与关键约束 | 要演示的真实链 | 反向质疑 |
|---|---|---|---|
| P-V 16bit X-Ray/Vision | 一处能力声明+FCM配置；像素格式、ROI坐标、P95、GPU | 选算法/连线→运行→Mask/NG→SourceMap故障→改选型 | 无Decision是否仍需手写多处端口/筛选和诊断？ |
| P-C CT3D体重建 | Projection/Volume几何、Spacing、显存/数据寿命、分块 | 候选/预算→描述Plan→MRK运行→资源实测→修正方案 | 资源估算不能在Decision复制GB数据 |
| P-A PLC/运动设备 | 幂等/外部副作用/授权/联锁/独占资源 | 候选/顺序与排他→安全发布→故障Unknown对账 | 超时自动重发一次也不能接受 |
| P-I Inspection规则 | 质量证据、批次/规格版本、OK/NG/Review | 结果端口→判定→Trace还原规则/产品身份 | Decision不能取代专业质量判定器 |
| P-H HMI Symbol/变量绑定 | MEMSymbol≠Capability，数据质量、权限、类型 | 选择变量→过滤适用Symbol→显示/按钮命令授权 | “能画按钮”不等于“能写PLC” |
| P-S Simulation虚拟运行 | 模型/场景/时钟/Seed/兼容能力/故障 | 静态路径→Virtual MRK→Replay/故障→比较实测 | 没有模型不能自动宣称已仿真 |

**要求**：每例分别测试纯专业 L0、轻量 Flow L1、Studio L2、目标决策 L3，记录跨层升级成本；不能把六个专业都写成同一“调用算法”的平庸例子。目前 Inspection/HMI 正式工程源码仍很薄，目标案例应标记 Design，不能推断具备相应Provider。

### 1.2 生命周期/使用入口反推矩阵

| 阶段 | 入口与真实Owner | Decision什么时候介入 | 必需输出 | 本轮发现 |
|---|---|---|---|---|
| 直接调用 | 专业SDK | **默认不介入** | TResult/错误 | 防止所有函数必须创建Project/Decision |
| 声明/注册 | 专业+MFM/MPC/FCM/MOC/Host | 注册签名一致性或只读描述归一 | 可被查询的能力签名 | 缺一处注册→多消费者的实施合同 |
| 发现/选择 | Studio、SDK、ModuleCenter | 根据Port/目标/策略筛选 | Applicable/Rejected/Unknown+原因 | MOC目录目前依赖MRK resolver |
| 连线/配置 | Core Edit+FCM | 即时类型、单位、依赖/参数验证 | 非法阻断/合法引导/可选适配 | 必须保留撤销和Owner编辑事务 |
| 编排/Build | MEM/MOL/MOC | DAG/Region/Policy/SourceMap、全局可行 | Diagnostic+纯描述Plan | 现有编译与MRK绑定耦合 |
| 试运行/发布 | Host/Lifecycle/MRK | Decision仅给静态评价/方案 | NeedRuntimeChecks+PlanVersion | 发布/真实资源/权限不得由Decision执行 |
| 运行/观察 | MRK/MDS/专业 | **默认不在每帧关键路径** | Trace/Result/StepSnapshot事实 | 不能再造Trace/Journal/调度 |
| 调试/模拟 | MRK+PSM+Studio | 基于证据解释/挑场景/影响分析 | 源节点/尝试/模拟级别及理由 | TraceGap不可伪造丢失历史 |
| 故障后优化 | 用户/Host显式评估 | 基线/目标/候选/推演/建议 | 新Revision方案与对照 | 异步建议不得修改活动Plan |

## 02. 源码核实：当前真实承载体与P0/P1缺口

| 锚点（真实源文件/类型） | 已有事实 | 需要保留/拆分的内容 |
|---|---|---|
| `Mechanism/Core/Modules/MFMModuleContracts.cs`：MFMModuleDescriptor | ID/Name/Kind/Version/CapabilityIds，普通函数不要求Module实例 | 模块身份仍唯一Owner，Decision不能建立ModuleRegistry |
| `Mechanism/Orchestration/MOCCapabilityCatalog.cs`：MOCCapabilityDescriptor/MOCCapabilityCatalog | Id/Name/Category/整数Version/MPCPort；Catalog构造**要求IMRKCapabilityResolver**，TryResolve检查并产生RuntimeAvailable | **P0**：纯描述查询必须先从运行Resolver脱开；当前RuntimeAvailable仅是解析成功，不是设备Ready/授权 |
| `Mechanism/Runtime/Execution/MRKExecutionCompiler.cs`：MRKResolvedCapability | ExecuteAsync委托、CLR Input Type、具名输出/Compensation；MRKExecutionPlanCompiler使用resolver | **P0**：static Port TypeId与CLR Type真正有版本化映射/转换，不得将Func/CLR Handle序列化进Decision Plan |
| `Professional/Vision/CxVisionThresholdProvider.cs` | 同类实现IMOCCapabilityProvider、IMRKVersionedCapabilityResolver，MPC文本TypeId与MRK CLR端口分别声明 | **P1**：现有双描述模式用于迁移Golden；“一次注册、一致合同”仍未实现 |
| `Foundation/Data/Configuration/FCMConfigContracts.cs` + `FCMConfigSchemaBuilder.cs` | Section/Item/DependsOn/Validator/Converter/ApplyPolicy、同步异步校验，Reflection缓存 | 不重做Config Schema/ValidationEngine；Decision只读映射规则，草稿/Committed/Effective/版本语义不可混淆 |
| `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | MOL表达式/分支/Loop/ForEach/Try/Invoke/Codec及Validate；MOLScalarExpression返回bool | **P1**：组合语义可复用，但不能把旧bool用“false即未知”偷换为Decision三态；未知必须显式建模 |
| `Mechanism/Orchestration/MOCBuildService.cs` | 现有Build(snapshot, content, **resolver**)并调用MOCEngineeringCompiler | **P0**：轻量Assess不能强迫编整张Plan或引用Runtime |
| `Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs` | MRKCodeRuntimeBuilder/CapabilityForRequest/Flow/Run快捷调用实际存在 | 轻量开发使用保留，不让新MDC门面倒逼代码膨胀 |
| `Mechanism/Runtime/Execution/MRKExecutionControls.cs` | 有界Trace/Dropped和DebugController的Pause/Resume/StepOnce | 保持唯一运行调试真源；事故复盘使用Snapshot而不是伪造TraceGap |
| `Mechanism/Logic/` 与 `Mechanism/Orchestration/` | Logic现无实质功能主体，Orchestration编译和目录存在 | 先拆Static/Binder，后变工程依赖方向，最后才合并为Decision |

**未做的实证**：本轮未在Windows编译/跑专业设备；未逐Sheet审计DOCS/02～04；未核对ReferTo所有成熟算法/设备代码。因此不能把新接口、建议逻辑或HIL标记为已实现。


## 03. 关键对象怎样从真实任务反推（OOP）

**领域规则**：只创建有不变量、真正行为、责任边界或稳定扩展需求的类；请求/只读结果优先使用小型不可变DTO。不要因为D01～D10有十类能力就创建十个Manager、第二套Registry或第二套Rule DSL。

~~~mermaid
classDiagram
  class MPCPortDescriptor {
    +MemberId
    +Direction
    +TypeId
  }
  class MOCCapabilityDescriptor {
    +Id
    +Version
    +Ports
  }
  class MRKResolvedCapability {
    +ExecuteAsync
    +Inputs
    +DataOutputs
  }
  class MDCContext {
    +DefinitionRevision
    +CatalogFingerprint
    +PolicyRevision
    +EnvironmentEpoch
  }
  class MDCConstraintAssessment {
    +Verdict
    +Outcomes
    +RequiredChecks
  }
  class MDCCandidateEvaluation {
    +Feasible
    +Rejected
    +Unknown
    +Alternatives
  }
  class MDCPlanArtifact {
    +DefinitionFingerprint
    +DAG
    +SourceMap
  }
  MOCCapabilityDescriptor --> MPCPortDescriptor : existing port definitions
  MOCCapabilityDescriptor ..> MDCContext : read-only projection
  MDCContext --> MDCConstraintAssessment : evaluates
  MDCConstraintAssessment --> MDCCandidateEvaluation : eligibility
  MDCCandidateEvaluation --> MDCPlanArtifact : optional planning
  MDCPlanArtifact ..> MRKResolvedCapability : runtime bind only
~~~

**图注**：MPC/MOC/MRK为源代码已有名称；MDC为未来目标合同，虚线表示投影/运行交接而非直接程序集引用。最终MOC描述要向不依赖Runtime的纯合同迁移，正式类型名由#1统一裁决。

| 必需概念 | 单一Owner/职责 | 不变量/边界 | 先复用的真实来源 | 抽象必要性 |
|---|---|---|---|---|
| Module identity | Core/MFM，五类Module+可选实例 | ModuleId/CapabilityIds唯一，不持有第二个运行器 | MFMModuleDescriptor | 已有，**禁新建** |
| Port/Parameter Schema | Core MPC、FCM/MHS及专业契约 | TypeId/Unit/Shape/配置Effective身份、真实类型适配可证明 | MPCPortDescriptor、FCMConfigSchema | 已有；只加薄桥接 |
| Capability snapshot | 持有原始描述的现有目录/Host，Decision只读消费 | Descriptor/Version/Fingerprint/Origin/Availability的多个层级分离 | MOCCapabilityDescriptor（需拆Resolver） | 必需的**纯DTO投影**，不建第二注册真源 |
| Decision request/context | Decision，一次请求生命周期 | Project/Policy/Catalog/Environment版本Pin，取消/预算，纯只读 | Core/MFM/FCM快照 | 必需；不复制Project |
| Constraint normalized view | Decision，对来源约束做只读归一 | PredicateId/Version/Unit/Scope/Evidence/HardOrSoft | MOL/FCM/专业合同 | 必需；不是另一份权威规则库存 |
| Constraint evaluator | Decision，对声明条件求三态 | Fail>Unknown>Pass，纯、可取消、可复现，详细来源 | MOL/FCM校验适配 | 仅在多个真实可替换规则族时加接口 |
| Candidate scorer | Decision，对可行候选作有证据目标比较 | 未知不能“排名最高”；归一单位、稳定择优/Pareto | MOC目录/专业模型 | Strategy可选，不建统一万能Optimizer |
| StructuralPlan | Decision纯静态工件；Core定义仍唯一真源 | DAG/Region/SourceMap/指纹不可变，无执行委托 | MOC静态部分 / MRK编译前部分 | 必需用于无环解耦 |
| BoundExecutionPlan | Runtime，真实Delegate/版本/资源/授权 | 不可直接持久化/跨进程复制句柄 | MRKExecutionPlan | 已有，**禁新建** |
| Evidence/Explanation | Decision证据引用与推理解释，原始Trace归MRK | 决策原因与事实来源分开，区分Static/Virtual/Field | MOCDiagnostic、SourceMap、Trace DTO | 必需；不另存真实运行状态 |

**必须回答的OOP反证**：若设计某`MDCxxxManager`，请逐条说明其业务不变量、谁拥有可变数据、为什么无状态函数/现有Owner不能完成、是否让Context依赖具体WPF或MRK；答不出即删除拟议抽象。优先组合优于继承，业务对象不继承Host/Runtime/UI类型。

## 04. GoF具体用于何处，哪些不该使用

| GoF/领域模式 | 真实变化点/解决的问题 | 推荐边界及使用时机 | 反模式/否决条件 |
|---|---|---|---|
| **Strategy（GoF）** | 候选的词典序/Pareto/有界加权、CPU/GPU资源估算差异 | `CandidateRanker`或`ResourceEstimator`在D03/04/07/08确实有第二种策略时注入 | 一个稳定比较函数就建接口/工厂/管理器 |
| **Composite（GoF）** | 多级Hard谓词AND/OR、参数依赖、条件适用范围 | 在可复用的中性表达树上组合纯谓词，复用MOL表达能力 | 并行维护第二套MOL控制语言 |
| **Adapter（GoF）** | FCM验证报告、MOC目录、MPC字符串TypeId与MRK CLR类型的差异 | 用专用薄适配器转成同一只读语义与Diagnostics，迁移期单向 | 两套Schema/诊断引擎、Adapter内执行设备命令 |
| **Factory Method（GoF）** | 同能力多Provider/虚实替换、生命周期明确的装配 | Host/CompositionRoot受控产生Provider绑定或Descriptor构造；简单函数不需要 | Decision自行new实际PLC Driver/GPU Worker |
| **Observer（GoF）** | 工程Revision、FCM Effective版本、能力目录/运行证据变化 | 订阅现有事件/变量/消息机制，仅通知失效与新请求 | 新建Decision消息总线或重新持久化运行状态 |
| **Command（GoF）** | Studio的Undo/Redo、可取消正式编辑 | 调用Core唯一Edit事务/已有命令合同，Decision返回建议对象 | Decision持有另一棵工程Graph或直接InsertSymbol |
| **Decorator（GoF）** | 通用计时、审计、脱敏、有限缓存 | 包围纯Decision调用或交接边界，不改核心结果 | 把硬安全校验变成可绕开的装饰器 |
| **State（GoF）** | 专业Device/MRK已有状态机语义 | 仅参照已有Runtime状态/静态分析状态转移 | 新建“DecisionRuntimeStateMachine”执行真实状态 |
| **Specification（领域模式，非GoF 23）** | 可复用硬/软条件与组合 | 只作为静态约束语义/版本化纯计算，和Composite衔接 | 在权限规则外自造不受控表达式解释器 |

**GoF验收判据**：每一种使用模式必须绑定一个具体变化点、至少两个现实实现或明确迁移兼容用例、一个可测扩展案例；如只会增接口层和转发类，不改善变更局部性，便删掉该模式。

## 05. AOP横切应服务全部调用，绝不改变业务真相

| 横切关注 | 放在哪里 | 能够审计的保证 |
|---|---|---|
| Context/Correlation | SDK/Studio→Decision入口，传RequestId/Revision/RunId来源 | 跨编排与Trace定位一致，生产敏感数据脱敏 |
| TimeBudget/Cancellation | 每次纯评估/外部模型调用入口 | 超时/取消不可返回Allowed或已选中；耗时求解不得卡Studio UI |
| Cache/失效 | 纯候选与约束评估外部Decorator | 键包含定义/策略/能力/环境版本；过期结果不可被Core提交 |
| Structured Diagnostics | 值对象+受控异常边界 | 业务Reject、证据Unknown、程序异常分别呈现；不吞原始根异常 |
| Metrics | 评估/编排/编译阶段 | p50/p95、PeakAllocated/命中率、候选数、ReasonCode分布可复核 |
| Auditing/Security | Host/专业真门禁 + Decision结果审计 | AOP只记录和限流；**绝不可将PLC联锁/权限做成可跳过的Advice** |
| Redaction/Privacy | 查询和Trace展示边界 | 不暴露设备凭据/客户原始数据；问题诊断保留受控证据引用 |

**实施推荐**：用可组合Decorator/显式pipeline步骤，不要求动态代理框架或编译期织入才能工作。对性能敏感的每帧流程，绝不自动附加耗时Decision推演。

## 06. 约束、策略与“最好”如何形成可证明求解

~~~mermaid
flowchart TD
  A["Goal+Baseline+RequiredConstraints+CatalogSnapshot"] --> B["来源合并/单位标准化/版本/证据时效"]
  B --> C["逐候选Hard Predicate Pass/Fail/Unknown"]
  C --> D{"结果"}
  D -->|Fail| X["Rejected + Conflict/Reason"]
  D -->|Unknown| Y["Unknown + RequiredChecks"]
  D -->|Pass| E["Feasible Set"]
  E --> F["比较目标/资源/风险/置信范围"]
  F --> G["Pareto + 词典优先级/明确策略"]
  G --> H["选中/备选/适用条件/证据"]
  H --> I["影响分析+显式确认/新Revision"]
  I -.->|"新实测证据"| A
~~~

- **约束等级**：硬规则由其Owner拥有变更权；可优化软目标带权重/优先级/单位；动态授权要由Runtime/Host重检。不同来源规则做受控合取，而非“最后一条覆盖”；未证实的硬条件只得Unknown。
- **Rule与Evidence**：Result至少包括`ConstraintId/SourceRef/PredicateVersion/EvidenceId/EnvironmentEpoch/ReasonCode`；拒绝要列出最小有用失败集合；非确定性/超时要有预算结果，而不是伪装通过。
- **比较**：目标方向、单位和基线声明不可缺；先有可行域才谈“最好”，并允许“没有被证明的可行候选/多个不可比Pareto候选”。
- **策略**：Decision制定可应用的选择、串并行、超时/重试/降级建议及触发条件；真实MRK执行、设备互锁、人工审批仍原Owner。非幂等动作不因优化收益高而自动重试。
- **缓存**：参数草稿与Effective配置不是同一时态；静态分析可从draft生成编辑期建议，但生产Plan必须Pin批准后的Effective和发布版本。
- **反证**：没有实测精度/耗时样本不能说明AI算法最好；没有虚拟模型不能宣称“模拟走向已验证”。

## 07. 组合协作的两种入口

~~~mermaid
sequenceDiagram
  actor Dev as 专业开发者/Studio
  participant Core as Core/MFM+MPC/FCM
  participant D as Decision（纯）
  participant Ed as Core Edit事务
  participant Host as Host/Lifecycle
  participant MRK as Runtime MRK
  participant Pro as 专业Provider
  Dev->>Core: 声明/查询Capability/Port/配置Revision
  Core-->>Dev: 只读静态合同（可能部分未知）
  Dev->>D: FindCandidates/Assess(当前Port+约束+目标)
  D-->>Dev: Applicable/Rejected/Unknown+Reason/Evidence
  opt 用户确认添加/调整
    Dev->>Ed: 正式编辑+Undo/Revision
    Ed-->>Dev: NewRevision
  end
  opt 完整工程构建/发布
    Dev->>D: Assess/Plan/Compile（固定Revision）
    D-->>Dev: 静态Plan/SourceMap/RequiredRuntimeChecks
    Dev->>Host: 发布授权请求
    Host->>MRK: LoadAndValidate(许可/资源/版本)
    MRK->>Pro: 真实Provider绑定与运行
    Pro-->>MRK: Output/专业结果
    MRK-->>Dev: RunId/Trace/Runtime事实
  end
~~~

**不强制流经Decision**：纯算法/L0可直接调用Professional SDK；普通L1可直接使用现有MRKCodeRuntimeBuilder；L2是上下文编辑辅助；L3才是资源/策略/仿真/优化。接口名保持拟议，实际签名权威仅在#1。所有版本化变更后重新请求，不能将旧DecisionResult自动应用到活动Plan。


## 08. 严重度审查清单（有证据的事实 vs 设计风险）

| ID/级别 | 实际观察或合理缺口 | 为什么重要 | 修订/完成门禁 | 当前状态 |
|---|---|---|---|---|
| ARC-01 **P0** | Orchestration依赖Runtime；MOC目录与Build依赖IMRKResolver | 先改项目名再让MRK引用Decision会环依赖 | 先纯静态Contract/Compiler与RuntimeBinder分离；csproj真实DAG无环，行为Golden一致 | 源码确认，未迁移 |
| ARC-02 **P0** | MRKResolvedCapability含委托/CLR Type，MOC Descriptor仅MPC TypeId | “同一能力一次注册”容易变两套Schema和不安全Plan | 明确TypeId↔CLR Type映射、Id/Version/Schema指纹/输入输出方向/可空/数据所有权、注册差异拒绝 | 源码确认，目标待设计/实现 |
| ARC-03 **P0** | Studio推荐/评估可能包含动态安全未知 | 容易把静态Allowed错当设备许可 | 静态三态与Host真实许可分层，非幂等/外部未知不自动重试，Sim真机调用0 | 设计有原则，待完整实现 |
| ARC-04 **P1** | V2#1公开MDC门面尚无真实实现或最小完整签名样例 | AI无法据此直接写出兼容代码 | 输入DTO/输出DTO/错误码/取消、签名/命名、旧新映射、Windows SDK示例编译 | 拟议 |
| ARC-05 **P1** | V2#3“注解/注册一次”仍是目标，无完整元数据归一/Binding生命周期 | 各专业必须继续手写描述+执行合同 | 一能力三形式注解/Fluent/显式统一投影，重复ID/Type冲突即失败；不能在Decision引用MRK执行DLL | 拟议 |
| ARC-06 **P1** | V2#2约束合并只给原则，缺跨来源Conflict/Unknown细算法及策略条件模型 | “该不该/最优”可能退化成默认打分 | 最小Hard冲突集、单位规则、证据时效、Pareto/策略同输入可重现、业务基线和解释 | 拟议 |
| ARC-07 **P1** | V2#4编辑期过滤/引导是目标，没有精确定义增量/过期结果处理路径 | 操作卡顿、旧建议污染新工程 | Port/Block/Policy依赖图缓存、Revision门禁、Core事务/Undo一致、异步取消测试 | 拟议 |
| ARC-08 **P1** | V2#5 SourceMap/Trace存在，但统一RunEvidence、命名数据/跨子流程定位仍有集成缺口 | 开发者无法一键定位真实失败 | Run/Invocation/Step/Attempt/Block/Data版本一致且TraceGap不伪造；Studio/SDK相同事实 | 部分源代码，闭环待验 |
| ARC-09 **P1** | V2#6对Inspection/HMI多为目标案例 | “六专业全类实现”易被误报 | 代表用例接入代码、API/Studio实际执行、否定场景、证据等级；不以目录存在视为功能 | 主要Design |
| ARC-10 **P1** | DOCS/02～04 XLSX全文逐Sheet未审 | Owner/依赖/类型与已定规格可能冲突 | 实施前出Sheet/Row/Old/New/Owner/Decision差异表，平台最终裁决明确 | 未核实，禁止宣称一致 |
| ARC-11 **P2** | OOP/GoF/AOP之前更多是原则，缺绑定用例/可删除条件 | 产生空壳Manager/重复规则引擎 | 每个拟议抽象写责任/变体/模式/不使用时损失/单测 | 本轮已补审评，代码待定 |
| ARC-12 **P2** | V2测试以测试设计为主，量化性能硬件尚未批准 | 把文档/CI绿灯当价值提升 | 对照实验与性能预算，Windows/设备证据按SHA归档 | 目标测试 |

**P0是工程/安全硬门禁；P1代表不可照文档无歧义完整开发；P2是体验/扩展/运维成熟度。** “评审建议”不是自动批准调整项目Owner；用户已经批准的Core/Decision/Runtime总体职责与十大能力不随本报告改变。

## 09. 六专业×能力覆盖检查：找真正的缺口

| 任务 / 核心需求 | D01/02 | D03/04 | D05/06 | D07/08 | D09/10 | Owner状态 |
|---|---|---|---|---|---|---|
| Vision：Gray16/8、ROI、AI模型选型 | 必须 | 质量/时延取舍 | 检测/NG策略 | GPU/精度 | Trace/影响 | Vision部分源码；推荐未完成 |
| CT3D：投影几何、FDK/SART、分块 | 必须 | 目标精度 | 分块/取消 | 显存/并发 | Simulation/回放 | 专业部分源码；完整CT链待验 |
| Automation：PLC/运动/多设备 | 必须 | 主备权衡 | 互斥/重试 | 时间/资源 | HIL证据 | 现场动作需要专业安全验证 |
| Inspection：质检规则/Review/NG | 必须 | 误报漏报 | 子流程/复检 | 吞吐风险 | 规则版本追溯 | 工程骨架不能视作已实现 |
| HMI：数据绑定/权限/状态 | 必须 | Symbol候选 | 仅合法命令策略 | UI负载分析按需 | RootCause视图 | Symbol≠Capability，待专业接入 |
| Simulation：虚拟故障/Replay | 必须 | 方案选择 | 时间/事件 | 情景预算 | 静态/虚拟/实机分级 | PSM部分源码；模型覆盖未证实 |

**用法全类检查**：上述矩阵不能证明“D01～D10在每一专业都必须实现”。矩阵证明“每个专业代表任务有可检验的跨能力工作链”；可选阶段应显式标为不需要/未支持，不能自动填满。最终的全类验收由需求ID→入口→状态→测试全量查漏，而不是单靠本表勾选。

## 10. 反向破坏性测试和应提交的证据

| Gate/TestID | 正常路径 | 反例/强制停止 | 所需证据 |
|---|---|---|---|
| REV-01 去掉Decision | L0专业调用及原MRK开发Flow保持可用 | 如简单函数被迫创建MEM/Decision，架构失败 | SDK示例编译、对象/初始化计数 |
| REV-02 换专业 | 同一轻量Capability模型接Vision和CT3D、Automation三类 | 新增专业需要在Studio/MOC硬编码Switch，架构失败 | 实际注册代码/目录+端口测试 |
| REV-03 描述/执行签名分歧 | MPC TypeId映射到MRK CLR输入，Schema指纹一致 | 同Id双端口类型冲突/旧配置版本，应明确拒绝而不是任取一边 | 专业Provider对照测试 |
| REV-04 Hard/Unknown | 硬Fail→Rejected、未知→Unknown，有RuleId和SourceMap | GPU超限还因速度快获选，或未知PLC授权被放行 | 约束性质/安全反例 |
| REV-05 编辑与运行隔离 | Core事务、Undo/Redo、Revision更新与异步结果失效 | 过期推荐自动连线/插入转换/修改正在运行Plan | Studio/WPF STA与Headless SDK测试 |
| REV-06 Plan与Runtime断环 | PurePlan无Delegate；Runtime加载后才能绑定 | Decision有Runtime ProjectReference/Plan序列化委托 | csproj拓扑+序列化/回归 |
| REV-07 状态追踪 | 同一节点多Invocation、动态Loop/三层子流程精确定位 | TraceGap伪造历史、数据误从别的Invocation读取 | SourceMap与MDS黄金测试 |
| REV-08 模拟不冒充现场 | Static/Virtual/Replay/HIL/Field每一级有不同证据 | Virtual执行真实设备指令、无模型却报告仿真通过 | 虚拟模型Seed+设备调用计数 |
| REV-09 OOP/模式必要性 | 每个接口/Manager绑定至少一实际变化用例 | 移除空壳不影响任何行为/测试仍保留该类，审查不通过 | 类型增量差异/依赖与维护成本 |
| REV-10 规模/性能 | 10/100/1000候选，100/1000/10000节点，频繁取消与WPF操作 | CPU/UI锁死、无界缓存、深循环按MaxIterations静态展开 | 固定硬件与p50/p95/峰值内存 |
| REV-11 六专业闭环 | 每专业最少一个“声明→编排→运行→解释/调试”案例 | 只存在空项目或图示即宣称通过 | 真实类、样例、Studio/TestCenter/CI和HIL |
| REV-12 工业安全 | 不可逆动作/设备未知必须待对账 | 未经许可自动重试、取消即宣称设备已停 | Host/设备Owner签名与HIL证据 |

**验证边界**：本轮只提出以上测试合同，不宣称运行了新测试。所有性能指标需先做固定硬件冷/热基线；不得为了表格漂亮编造统一毫秒阈值。

## 11. 本轮审评对权威文档的回填要求与顺序

1. **#0**：补“六层双向反推门禁”、五等级状态、六专业×使用链映射；不改变已定三大判断与D01～D10。
2. **#1**：补最小对象职责/GoF/AOP必要性、纯CapabilitySnapshot与真实Binder的桥接不变量、拟议API签名收敛；不得从本报告另开第二公共API。
3. **#2**：补来源/时效/硬软冲突/Unknown/Pareto算法合同和策略条件、验证与解释，落实“凭什么知道该不该/最好”。
4. **#3**：补注解/Fluent/Descriptor优先级、TypeId↔CLR类型映射、一次声明真正的注册/消费者路径；与MFM/MOC/FCM Owner核对。
5. **#4**：补编辑时的候选状态分层、局部增量依赖、版本失效、UI→Core事务；静态路径预演/虚拟运行绝不混淆。
6. **#5**：补Trace/SourceMap/RunEvidence追踪链、观察与Decision推断的证据分离、模拟与真实硬件安全隔离。
7. **#6**：六专业每类必须演示真实最小接入与失败反例，列出现有与拟议的严格区别；空工程不误报。
8. **Migration/Testing**：把ARC-01～12和REV-01～12加入实际迁移及验收Gate；实施前逐Sheet核验02～04及源码消费者。
9. **DOCS/00**：登记本报告为V2.0审评索引，仍以#0～#6为唯一技术真源，不与它们并行维护。

**审评判定**：整体方向可保留并深化；P0/P1仍在，不能评价为“架构完整且可直接编码无歧义”。下阶段首先做纯合同、TypeId映射、MOC/MRK静态与执行解耦的最小可运行切片，而不是先物理改名或批量建类。
