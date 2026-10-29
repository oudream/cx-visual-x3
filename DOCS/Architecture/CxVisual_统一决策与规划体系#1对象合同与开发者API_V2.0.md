# CxVisual Decision——#1 对象合同与开发者 API V2.0

> **唯一公开合同规格**：所有新 `MDC*` 和下列示例是**拟议签名**，尚未加入 master；旧[#1 V1.0](CxVisual_统一决策与规划体系%231对象合同与开发者API_V1.0.md)为历史。总纲[#0](CxVisual_统一决策与规划体系%230总体架构与全类需求_V2.0.md)，约束真源[#2](CxVisual_统一决策与规划体系%232约束目标与策略_V2.0.md)，真实类型映射以[Migration](../Migration/CxVisual_Decision_Logic与Orchestration融合及Runtime解耦_V1.0.md)为准。
>
> **设计意图**：一个简单问题只需要相关输入，一次调用返回结构化答案；复杂工程可以按 Assess→Compare→Plan→Compile 组合。禁止为了简单端口检查构建完整MEM工程/Runtime/Worker。

## 01. 三种自然入口

| 用法 | 示例意图 | 输入要求 | 返回 |
|---|---|---|---|
| A 轻量检查 | 两个端口能连吗？Gray16算法接受吗？ | 两个Descriptor、已知环境/规则 | 三态结果及可定位原因 |
| B 筛选比较 | 这张图接下来可用哪些能力？CPU还是GPU？ | 输出端口、Capability快照、目标与资源 | 适用/未知/拒绝候选、推荐与证据 |
| C 完整规划 | X-Ray多节点检测/CT流水线如何构建并发布？ | Core Revision、需求、能力/策略/环境快照 | 验证→静态Plan→Runtime受控装载 |

**当前真实可用 API 示例**：`MOCCapabilityCatalog.Find/Search`、`MOCBuildService.Build(MEMProjectSnapshot,...)`、`MRKCodeRuntimeBuilder.Flow/Build`、`MRKDevelopmentRuntime.RunAsync/RunValueAsync`、`MRKExecutionDebugController` 等。**这些并不是本文拟议MDC的实现**。

## 02. 目标类型与关系（原则上同一个Decision DLL）

~~~mermaid
classDiagram
 class IMDCDecisionService {
   +Assess(request)
   +FindCandidates(request)
   +Compare(request)
   +Plan(request)
   +Compile(request)
   +Explain(request)
   +AnalyzeImpact(request)
 }
 class MDCDecisionContext {
   +RequestId
   +DefinitionRevision
   +CapabilitySnapshotId
   +PolicyRevision
   +EnvironmentEpoch
   +EvidenceSet
 }
 class MDCConstraintSet {
   +Hard
   +Soft
   +RequiredRuntimeChecks
 }
 class MDCAssessment {
   +Verdict
   +Diagnostics
   +UnknownConditions
 }
 class MDCCandidateResult {
   +Applicable
   +Rejected
   +Unknown
   +Recommended
 }
 class MDCPlanningResult {
   +Structure
   +Strategies
   +Resources
   +Diagnostics
 }
 class MDCPlanArtifact {
   +PlanId
   +Fingerprint
   +SourceMap
   +Capabilities
 }
 class MDCExplanation {
   +ReasonCodes
   +EvidenceRefs
   +SourceIds
 }
 IMDCDecisionService --> MDCDecisionContext
 MDCDecisionContext --> MDCConstraintSet
 IMDCDecisionService --> MDCAssessment
 IMDCDecisionService --> MDCCandidateResult
 IMDCDecisionService --> MDCPlanningResult
 IMDCDecisionService --> MDCExplanation
 MDCPlanningResult --> MDCPlanArtifact : pure compile
~~~

**唯一Owner**：Core owns MEM/MOL/MFM/MPC身份和持久定义；Decision owns不可变请求/评估/候选/策略/纯Plan；Runtime owns MRKExecutionPlan/Executor/Trace/RunContext；Professional owns专业实现及成本模型。只引用静态合同，不得把Runtime真实委托传给Decision。

## 03. 关键数据结构与可判定不变量

| 对象（拟议） | 必填语义 | 严格规则 |
|---|---|---|
| `MDCDecisionContext` | Scope、RequestId、SourceRevision、CatalogRevision、PolicyRevision、EnvironmentEvidence、Cancellation/TimeBudget | 只读快照，必须可确定地失效旧结果；无Studio线程依赖 |
| `MDCConstraint` | Id、Origin、Kind(Hard/Soft/Conditional)、PredicateVersion、Unit、Value/Threshold、Scope、EvidenceRequirement | Null/过期/无法验证→Unknown，不伪装Pass |
| `MDCObjective` | MetricId、Min/Max/Target、Unit、Priority、Weight、Tolerance、Baseline | 无共同单位/不可比较不能计算合法总分 |
| `MDCCapabilityContract` | CapabilityId/Version、Input/Output、Traits、SideEffects、Idempotency、ResourceHints、ProviderOrigin | 引用MFM/MPC规范投影，不能第二份可写目录 |
| `MDCAssessment` | Allowed/Rejected/Unknown、ConstraintOutcomes、Diagnostics、RequiredChecks | Hard Fail先于Unknown；Allowed只代表本次静态条件已证明 |
| `MDCCandidateResult` | Feasible/Unknown/Rejected、Scores、ParetoSet、Selected、Alternatives、Reasons | 无 Feasible 则 Selected=null |
| `MDCPlanningResult` | StructuralPlan、StrategySet、ResourceEstimate、SourceMap、Assumptions | 结构不受“最大循环迭代次数”线性展开 |
| `MDCPlanArtifact` | Plan/SchemaVersion、Fingerprint、DAG/Regions、Capabilities、Policies、SourceMap | **纯可序列化数据**，不得包含Delegate/DeviceHandle/Task/LiveFrame |
| `MDCExplanation` | DecisionId、SourceRefs、RuleId/Version、Evidence、Why/WhyNot、ImpactSet | 每项可归因至规则+工程SourceId；不凭空捏造根因 |

**身份链**：`ProjectId/Revision → ContentId/BlockId/LinkId/PortId → SourceMap → PlanFingerprint → RunId/InvocationId/StepId/AttemptId → Trace/Evidence`；变更影响分析沿真实依赖传播。未知外部副作用必须保守标记。

## 04. SDK（设计签名，非已编译源码）

~~~csharp
// Proposed: namespace CxVisual.Mechanism.Decision
public enum MDCVerdict { Allowed, Rejected, Unknown }
public interface IMDCDecisionService
{
    MDCAssessment Assess(MDCAssessmentRequest request,
        CancellationToken cancellation = default(CancellationToken));
    MDCCandidateResult FindCandidates(MDCCandidateRequest request,
        CancellationToken cancellation = default(CancellationToken));
    MDCCandidateResult Compare(MDCComparisonRequest request,
        CancellationToken cancellation = default(CancellationToken));
    MDCPlanningResult Plan(MDCPlanningRequest request,
        CancellationToken cancellation = default(CancellationToken));
    MDCCompileResult Compile(MDCCompileRequest request,
        CancellationToken cancellation = default(CancellationToken));
    MDCExplanation Explain(MDCExplanationRequest request);
    MDCImpactResult AnalyzeImpact(MDCImpactRequest request,
        CancellationToken cancellation = default(CancellationToken));
}
~~~

请求细分为**最小上下文**，不强迫每个小调用传完整项目；`FindCandidates`允许仅提供当前端口+CatalogSnapshot+ContextConstraints；`Plan`必须提供版本化Core定义。简单便捷门面不得返回未经证据支持的“最优”。

### 示例 A：轻量连线、明确原因

~~~csharp
// Proposed illustrative code, not current SDK
var result = decision.Assess(MDCAssessmentRequest.ForConnection(
    sourcePort, targetPort, capabilitySnapshot, policies), token);
if (result.Verdict != MDCVerdict.Allowed)
    ShowDiagnostics(result.Diagnostics, result.RequiredRuntimeChecks);
// Unknown不可自动连接；Studio可提供说明或等待复核。
~~~

### 示例 B：有硬约束的候选与备选

~~~csharp
// Proposed illustrative code
var result = decision.Compare(MDCComparisonRequest.Create(
    candidates, hardConstraints, softObjectives,
    capabilitySnapshot, measuredEnvironment), token);
if (result.Selected == null)
    ShowNoFeasibleCandidates(result.Rejected, result.Unknown);
else
    ShowChoice(result.Selected, result.Alternatives, result.Explanation);
~~~

### 示例 C：Plan交付与执行职责分开

~~~csharp
// Proposed illustrative code
var assessment = decision.Assess(projectRequest, token);
if (assessment.Verdict != MDCVerdict.Allowed) return;
var planned = decision.Plan(MDCPlanningRequest.From(projectRequest), token);
var artifact = decision.Compile(MDCCompileRequest.From(planned), token);
if (!artifact.Succeeded) return;
var admission = runtimeHost.ValidateAndLoad(artifact.Plan, authorization);
if (!admission.Allowed) return; // 现场资源、安全、版本必须复核
runtimeHost.Start(admission);   // 属于Runtime/Host，不是Decision
~~~

即使接口名字简单，也必须把**真实版本、错误、安全边界、取消**留给对应Owner。


## 05. 协同对象与版本固定

~~~mermaid
sequenceDiagram
  actor Dev as SDK/Studio
  participant Core as Core Snapshot
  participant D as Decision
  participant Host as Host/Lifecycle
  participant R as MRK
  participant Pro as Provider
  Dev->>Core: Capture(Revision)
  Core-->>Dev: Immutable engineering/constraints
  Dev->>D: Assess/FindCandidates/Compare
  D-->>Dev: verdict/reasons/unknowns/recommendations
  Dev->>Core: 用户确认修改，经原Edit事务提交
  Dev->>D: Plan/Compile(固定Revision)
  D-->>Dev: PurePlan+SourceMap+Fingerprint
  Dev->>Host: RequestPublishedRun(Plan)
  Host->>R: ValidateAndLoad(许可/现场版本/资源)
  R->>Pro: Provider真实绑定与前置核验
  alt 接受
    R->>Pro: 受控执行
    Pro-->>R: 执行结果/专业证据
    R-->>Dev: FlowResult/Trace/Journal
  else 拒绝或未知
    R-->>Dev: Rejected/Unknown + 现场原因
  end
~~~

Source/Plan/Capability/Policy/Environment版本与每份结果绑定；Core正式修改产生新Revision并让旧推荐失效。Runtime不得在活动Plan上按Decision的异步建议直接热改；应进入新版本审批/激活流程。

## 06. 最小可扩展接口而非十个 Manager

- **判定扩展**：`IMDCConstraintEvaluator`用于纯硬条件，可提供算法ID/版本、数据来源、单位、可重复性。复用FCM Validator/MPM/MOL逻辑的单一判定真源，不创建复制RuleEngine。
- **比较扩展**：`IMDCCandidateRanker`用于可替换的Pareto、词典式优先级、受控加权法；同输入/种子输出稳定。
- **资源扩展**：`IMDCResourceEstimator`只能评估能力的资源描述/实际测量样本，不能占用真设备作推断。
- **情景扩展**：`IMDCScenarioEvaluator`可消费纯模型/仿真证据，但不实现运行调度；仿真运行必须由MRK+Professional.Simulation完成。
- **兼容扩展**：MOC→MDC只允许单向Adapter和明确退役计划，不得长期留MOC与MDC双编译器。
- **横切**：Decorator做审计、限时、指标、缓存/脱敏；禁止异常时把Unknown或Rejected自动更改为Allowed。
- **返回合同**：人可读消息 + 稳定机器错误码 + SourceId/MemberId + EvidenceId；一般编排错误不能表现为无上下文InvalidOperationException。程序缺陷与业务规则拒绝必须区分。

## 07. 使用时机、取消与性能约束

- 连接/参数变化：轻量纯检查，无副作用，缓存按 `Revision/CapabilityVersion/PolicyVersion/EnvironmentEpoch` 键；高频拖动节流并取消上一请求，UI不被阻塞。
- 多候选比较：支持候选上限、评估预算、增量结果但只能在完整约束检查完成后发布Selected；超时返回Incomplete/Unknown而不做软评分选高分者。
- 完整工程：节点数量100/1000/10000基准；Loop/Foreach/子流程动态展开由MRK执行，静态计划只表达Region。测试PeakAllocatedBytes/CPU/p50/p95以及冷/热缓存。
- 单次请求资源上限、日志体积与诊断数量必须受控；同时评估多个工程不共享可变环境对象或设备句柄。
- 所有示例使用`.NET Framework 4.8`可表达的API形状，不以未证实的C#版本特性作为最低条件。

## 08. 现状和实现次序

**实际存在**：MEM/MOL定义、MFM模块描述、MOC目录/Build/SourceMap、MRK执行/调试/Trace、FCM注解/配置；**提议新增**：完整MDC服务及接口/纯Plan/三态约束/候选排序/按需路由；**未验证**：在当前SHA上V2 SDK集成或全类专业真实设备就绪。

落地顺序：①先保证最小连接Assess+Diagnostics不需要Runtime；②接入现有Capability目录的只读快照/FCM Schema；③增加FindCandidates（适用/不适用/未知）；④在MOC/MRK静态与委托分离后提供Compile+受控Loader；⑤扩展目标协调、策略、资源和仿真优化；⑥开发者示例在Windows CI上按签名真实编译。验收以[Testing](../Testing/CxVisual_Decision_功能与迁移验收矩阵_V1.0.md)为准。

---

## 09. V2.0 OOP/GoF/AOP对象合同反审与实现细化（2026-10-29）

> 依据[审评报告ARC-01～12](CxVisual_统一决策与规划体系%23V2全类反推与OOP-GoF-AOP架构审评_V1.0.md)；以下补充只收敛拟议公开合同，不能据此认为MDC类型存在或C#例程已通过编译。

### 9.1 最小领域对象：有职责才建类

| 合同 | 不变量及生命周期 | 实施提示 |
|---|---|---|
| CapabilitySnapshot（只读） | 同一CapabilityId+Version+SchemaFingerprint对应唯一规范化Ports/Config/Traits；纯声明可离线读取 | 由MFM/MPC/FCM/专业目录投影，不是Decision第二个Registry |
| DecisionContext | DefinitionRevision、CatalogVersion、PolicyRevision、EnvironmentEpoch与Evidence固定于一次请求，纯只读 | 不持有Studio控件、MRK Flow Handle、设备对象 |
| ConstraintOutcome/Assessment | 每条规则Pass/Fail/Unknown、ReasonCode/SourceRef/Evidence；聚合HardFail优先于Unknown | 返回值必须携RequiredRuntimeChecks且不自动执行它 |
| CandidateEvaluation | Feasible/Rejected/Unknown分别列示，推荐只能来自Feasible，适用条件和目标证据可追溯 | 排序可用Strategy，不引入第二套评分真源 |
| PurePlan | 版本化DAG/Regions/PortSchema/SourceMap/策略声明/依赖清单及确定性指纹 | 不含Delegate、CLR设备句柄、Task、MDS活Frame；MRK Loader才可绑定 |
| Explanation/RunEvidenceReference | 决策原因与真实Run/Step/Attempt/SourceMap引用分离 | Trace事实仍归MRK，不生成第二份运行状态 |

**当前源码差异**：`MOCCapabilityDescriptor` 存MPC字符串TypeId端口，`MRKResolvedCapability` 存CLR `Type` 输入和Delegate；两者不能依显示名匹配，也不可直接把MRKResolvedCapability引用进Decision。需要经Core/基础TypeId契约作确定性 `TypeId↔CLR Type`/方向/必填/输出/版本映射，并在不一致时给出稳定诊断码。FCM Schema/Effective版本不能被新属性覆盖。

### 9.2 目标实现边界图

~~~mermaid
flowchart LR
  C["Core/MFM+MPC/FCM：权威定义"] --> P["静态能力只读投影"]
  P --> D["Decision：Constraint/Goal/Candidate/PurePlan"]
  D --> X["MDCPlanArtifact：纯数据指纹"]
  X --> L["MRK Loader：校验+真实Delegate绑定"]
  L --> E["MRKExecutionPlan/Engine"]
  C --> L
  T["Professional：实际Provider"] --> L
~~~

图是目标业务/装配协作；工程引用必须`Runtime → Decision → Core`，同时`Decision ↛ Runtime`。迁移前不能强制沿用已有MOC编译器里的`IMRKCapabilityResolver`，否则会产生项目依赖环。

### 9.3 OOP/GoF/AOP选择记录

| 变化点 | 选择 | 反对的实现 |
|---|---|---|
| 多目标排序和不同专业资源模型 | Strategy/可选注入Ranker/Estimator（确有两种策略才抽象） | 十个DxxManager或无意义工厂 |
| 约束AND/OR/NOT | Composite/领域Specification复用MOL表达与FCM校验 | 第二套持久MOL规则语言 |
| 旧MOC和MRK投影 | 单向Adapter兼容窗口+Golden | 两套Compiler长期并行 |
| 编辑建议被应用 | 仅调用Core唯一Edit/Undo Command | Decision修改MEM工程或隐藏更新Config |
| 跨调用审计/计时/缓存/脱敏 | Decorator/AOP在纯评估边界；版本键完整 | 以AOP安全拦截代替Host/PLC显式安全门禁 |

**结果层分离**：`MDCVerdict`是已完成判定的三态；Cancelled/TimedOut/Incomplete应是**独立求解完成状态**，不可被折叠进Allowed。应先确定完整DTO和旧API映射，再固定公开签名并编写Windows可编译SDK样例；当前示例仍为拟议形状。

### 9.4 五项接口收敛门槛

1. 最小Assess两端口+Context即可调用，无MEM/Runtime；硬Fail/Unknown有稳定ReasonCode。
2. FindCandidates/Compare读取同一快照，无可行者Selected=null，先硬过滤后软比较；用户可显示被拒/未知候选。
3. 对FCM、MPC、MOC、MRK四方类型/端口/版本差异设置拒绝与兼容测试，不靠CLR Type.FullName随意转TypeId。
4. PurePlan对包含动态Loop/Foreach/Try/子Content的结构不按迭代次数展开，跨进程序列化没有Delegate。
5. 所有公开MDC目标API保留Proposed状态，实际可用SDK须列出代码位置、签名、消费测试SHA及CI。


---

## 10. 首批正式 API 代码切片——离线发现与局部三态评估（2026-10-29）

> 本节对应实际新增源码 `Mechanism/Orchestration/MDCDecisionService.cs`，程序集当前仍为 **CxVisual.Mechanism.Orchestration**，C# 命名空间预先使用 `CxVisual.Mechanism.Decision`。**这是过渡式静态功能落点，并非已完成Decision项目合并或Runtime断环。** 先前#1完整`IMDCDecisionService`、PurePlan/Compare/Compile仍属目标设计；本文下列简洁方法是已编写源码，验证等级以对应CI结果为准。

| API | 当前行为（严格范围） | 不支持/需要现场验证的部分 |
|---|---|---|
| `new MOCCapabilityCatalog(providers)` | 离线发现MPC能力及端口，绝不查询IMRK Resolver | `AvailabilityKnown=false`、`RuntimeAvailable=false`并不表示设备不可用 |
| `new MOCCapabilityCatalog(providers,resolver)` | 保持原运行解析语义，`AvailabilityKnown=true` | 仅已解析，不是Host授权和生产Ready |
| `MDCDecisionService.AssessConnection(source,target)` | 基于现有MPC静态Direction/Kind/TypeId/MTU单位、坐标、时间基与校准合同返回三态和稳定ReasonCode | 不执行用户编写的转换、真实设备权限、动态资源/AI质量 |
| `MDCDecisionService.FindCandidates(source,catalog,query,limit)` | 复用原MOC目录搜索候选，保留每一项已知Allowed/Rejected/Unknown与匹配端口 | 不按性能自动排序，不构建Plan，不执行Provider |
| `MDCDecisionService.AssessHardConstraints(outcomes)` | 聚合已有规则的三态结果，已证实Fail优先于Unknown；重复RuleId拒绝 | 不运行未注册专业规则，不取实时设备事实 |

~~~csharp
// 已新增源码示例：使用现有专业Provider的DescribeCapabilities，静态检查不启动MRK。
var catalog = new MOCCapabilityCatalog(new[] { provider }); // 离线、只读
var decision = new CxVisual.Mechanism.Decision.MDCDecisionService();
var selected = decision.FindCandidates(sourceOutputPort, catalog);
foreach (var candidate in selected)
    Console.WriteLine(candidate.CapabilityId + ": " +
        candidate.Assessment.Verdict);
// 判断不等于正式编辑；Studio仍由Core事务插入/连接。
~~~

**边界**：只有项目实际编译与专项/默认回归成功后才称为Build/Automated。后续纯Decision程序集仍必须先拆旧MOC对MRK的依赖；本轮保留既有28工程，不提前伪称27工程/单DLL完成。


## 11. 首批可实施的多目标比较（已编写源码；Windows专项验证待CI）

\`Mechanism/Orchestration/MDCObjectiveSelection.cs\` 提供 \`MDCChoiceService.Compare(options,objectives)\`、\`MDCObjective\`、\`MDCMeasuredMetric\`、\`MDCOption\`、\`MDCComparisonResult\`。输入为调用者明确的硬约束评估结果、带单位/来源的数值指标和明确的Minimize/Maximize及Priority；先Hard过滤，再用**完整且已实测的可比证据**构建Pareto前沿，按业务优先级稳定选择一个首选（可同时显示其他非支配方案）。这个首选是指定目标下的词典序选择，不宣称全局最优。

~~~csharp
// 已编写源码形状；前提是业务方提供真实有效的指标与硬约束评估。
var goals = new [] {
    new MDCObjective("latency","ms",MDCObjectiveDirection.Minimize,0),
    new MDCObjective("quality","score",MDCObjectiveDirection.Maximize,1)
};
MDCComparisonResult comparison = new MDCChoiceService().Compare(options,goals);
if (comparison.Selected == null) ShowNoVerifiedFeasibleOption(comparison.Candidates);
else ShowSelectionAndPareto(comparison.Selected,comparison.Pareto);
~~~

缺指标、单位冲突、仅估计/虚拟证据和Hard Unknown会进入Unknown而不自动选；违反Hard的候选即使更快也Rejected。首批比较使用有界256候选、16目标，无硬件设备查询，未来提供可信模型估计的咨询式排序须另订独立证据级别/授权，不能悄当实测。


## 12. 静态MPC↔MRK运行签名差异检查（接口实装阶段）

在现有 Orchestration 过渡项目新增 `MOCBindingContractInspector.Assess(MOCCapabilityDescriptor, MRKResolvedCapability, domainTypes?)`，仅用于 **Host/Resolver已提供运行签名时**检查类型/端口是否可信。该适配器属于运行边界，不得迁入最终纯Decision核心，更不能令最终Decision反向引用MRK。

检查覆盖：MPC必填声明与MRK输入必填、一方缺少或多余的输入、输入CLR类型、MPC具名输出与MRK Data.Return类型、具名输出缺失、专业语义TypeId与Host明确映射。标准原语按严格已知映射；专业TypeId须由Owner明确提供CLR Type，**不做任意反射Type.GetType或隐式转换**。Legacy Provider未声明输入合同/用`object`接受专业输入时返回`Unknown`或对应Reject原因；不能用运行可解析代替兼容性证明。

该检查与纯`MDCDecisionService`是两个关注点：前者在MRK绑定边界，后者在不接触Runtime的编排阶段。现阶段仍在Orchestration.dll中共同编译以便迁移保真，并不意味着物理Decison工程已完成。当前Windows验证结果以新提交的Decision CI为准。


## 13. 最小资源判断SDK（新增源码）

`MDCResourceBudgetPlanner`只需要Host只读预算与专业需求区间，不必先建工程/Worker：
~~~csharp
// 已新增的纯静态接口；以下数值仅演示调用，不是任何设备的实测结果。
var resources = new MDCResourceBudgetPlanner().Assess(
    new[] { new MDCResourceBudget("GPU_MEMORY","MiB",4096,1024,"host-snapshot") },
    new[] { new MDCResourceRequirement("GPU_MEMORY","MiB",2800,3500,"ct-model") });
// 结果Unknown：剩余3072MiB落在[2800,3500]需求区间，需验证。
~~~
成功条件：有证明的UpperBound≤剩余容量；确定不可行：LowerBound>剩余容量。实际Runtime资源获取仍由F19/MRK负责。本接口与有界MDCChoiceService组合时作为硬约束输入；SourceMap、Core编辑和发布无需更改。


## 14. 静态能力快照、版本封印与增量影响判断（新增源码，CI待本提交验证）

**现阶段API落点**：`Mechanism/Orchestration/MDCStaticCatalogSnapshot.cs`、`MDCDecisionService.FindCandidatesStamped()`。这是现有MOC描述的纯只读投影，仍处于Orchestration过渡程序集；完整Decision物理合并尚未执行。它不是第二ModuleCatalog、Runtime Resolver或持久化Graph。

| 对象/API | 输入/输出 | 作用与边界 |
|---|---|---|
| `MDCStaticCatalogSnapshot(catalog)` | MOC离线目录 → 描述只读集合、每能力SHA-256、整体指纹 | 依据静态端口结构求指纹，不查询运行Resolver/权限 |
| `MDCDecisionVersionStamp` | ProjectId、工程Revision、目录Fingerprint、策略Revision、环境Epoch | 将某次建议绑定到不可变评估上下文 |
| `FindCandidatesStamped(source,catalog,stamp)` | 当前端口/目录/完整Stamp → StampedCandidates | 目录指纹必须匹配，否则拒绝计算 |
| `batch.RequireCurrent(actualStamp)` | 当前实际版本 | 在Core Edit事务前由调用方验证；失效抛出稳定错误 |
| `MDCCatalogImpactAnalyzer.Compare(old,new)` | 两版目录快照 → Added/Removed/Modified ID列表 | 只报告影响候选，不自动修改工程或运行Plan |

**指纹确定性**：采用SHA-256、UTF8长度前缀、按CapabilityId和Port MemberId序列化排序；覆盖ID/名称/分类/整数版本、方向、类型、Kind、多重性、可见性、必填、单位/坐标系/时间基。与运行中的`RuntimeAvailable`无关。所有新增类不包含Delegate、Provider或真实设备句柄。

~~~csharp
// 已编写源码签名；必须在提交与Windows CI通过后才标记为Automated。
var catalog = new MOCCapabilityCatalog(new[] { provider });
var snapshot = new MDCStaticCatalogSnapshot(catalog);
var stamp = new MDCDecisionVersionStamp(projectId, revision,
    snapshot.Fingerprint, policyRevision, environmentEpoch);
var batch = new MDCDecisionService().FindCandidatesStamped(
    sourcePort, catalog, stamp);
// 用户确认后、调用Core正式Edit事务前：
batch.RequireCurrent(currentStamp);
~~~

**重要限制**：Stamp携带的是调用方实际提供的版本，Decision不凭空感知工程/策略/环境变化；Studio应在真正提交Core编辑事务前重新获取当前Stamp并调用RequireCurrent。此功能不能替代Host在生产发布时复核权限、资源和专业Provider的Effective版次。

## 15. 开发者一调用完成静态能力推荐（已编写源码，Windows CI待本提交验证）

`MDCRecommendationService.Recommend(source,catalog,stamp,objectives,budgets,professionalEvidence)` 是**组合门面（Facade）**而非第二套求解器：内部复用现有端口判断、资源上下界计算和多目标 Pareto 选择；返回`MDCRecommendationResult.Candidates`（各候选三态与原因）、`Comparison`（Feasible/Pareto/Selected）以及`InputsFingerprint`（本次完整输入指纹）。无MEM工程也能调用，无Provider执行、设备操作或MRK线程副作用。

`MDCProfessionalCandidateEvidence`由专业Owner提交**明确**的能力Id、资源需求区间、实测指标和外部硬规则。没有这个专业记录的能力返回Unknown，不会自动推荐；ResourceHardFail和PortHardFail不能因低延迟而被选中。此门面不自动验证EvidenceId的可信签名，工业发布必须由Host再确认配置、权限与真实资源。

~~~csharp
// 真实新增API形状；参数须来自已认可的专业/Host证据。
var catalog = new MOCCapabilityCatalog(providers);
var snap = new MDCStaticCatalogSnapshot(catalog);
var stamp = new MDCDecisionVersionStamp(projectId, revision,
    snap.Fingerprint, policyRevision, environmentEpoch);
var recommendation = new MDCRecommendationService().Recommend(
    sourcePort, catalog, stamp, goals, hostBudgets, professionalEvidence);
if (recommendation.Comparison.Selected != null) {
    // 真正调用Core Edit之前，重新读取全部版本及专业证据：
    string currentInputHash = MDCRecommendationService.ComputeInputsFingerprint(
        currentSourcePort, currentGoals, currentBudgets, currentEvidence);
    recommendation.RequireCurrent(currentStamp, currentInputHash);
    // 用户确认，才经原Core事务修改工程；不触发任何真实设备。
}
~~~

**版本门禁**包含完整Source Port、Goals、Budget、每个专业资源需求/指标/硬规则及其证据Id，调用方必须在应用前重新计算InputsFingerprint；即使工程Revision不变，资源预留量或测量数据变化也拒绝旧推荐。所有排序只是给定目标及证据下的非支配集/词典序首选，不能声称现场最优。

## 16. MOLWiring与专业Capability纯静态Build预检（已编写源码）

`MDCStaticBuildPreflight.Assess(MOLWiringDefinition, MOCCapabilityCatalog, requireExecutable=true)`是已有Core/MOL与MOC静态描述的无MRK适配。它首先复用MOL自己的`Validate/ValidateExecutable`，然后只核对原BlockId对应的CapabilityId、已声明端口的MemberId/Kind/方向/TypeId/必填、多重性与MTU合同。无描述的专业能力返回Unknown；必需端口缺失、静态类型不一致返回Rejected+BlockId/PortName；只为普通专业能力检查，不创建或序列化MRK Plan，保留Invoke/StateMachine为Core原Owner。

本接口只证明当前MOLWiring与当前静态目录的局部一致性；完整跨Content编译、真实Provider绑定、Runtime资源、执行授权与SourceMap仍需原MOC/MRK/Host进一步验证。编辑草稿可传`requireExecutable=false`，正式Build必须`true`，不允许用预检成功冒充Plan可执行。


## 17. P0静态来源清单：在真实Plan拆分前固化Core源身份（新增源码）

`MDCStaticSourceManifestBuilder.Create(MEMProjectSnapshot, rootContentId, MDCStaticCatalogSnapshot)`从**真实Core只读快照**和已批准的MOL V1/V2 Codec获得一个子流程引用闭包，完全不接收MRK Resolver、执行Delegate、设备句柄或运行态数据。返回`MDCStaticSourceManifest`：`ProjectId`/`EngineeringRevision`/`RootContentId`/`CatalogFingerprint`/`Fingerprint`、`Contents`（每Content原Logic SchemaVersion与Payload的SHA-256、原BlockId对应CapabilityId、引用的子ContentId）及`UnknownCapabilityIds`。

`HasCompleteStaticMetadata=false`表示目录未提供某项专业能力的静态合同，**不表示可运行**。碰到缺失Content、无/非法Logic Aspect、子流程引用环、超预算时明确抛出带`decision.source_manifest.*`前缀的失败，而不是凭空补一个“成功”Plan。`RequireCurrent(project,catalog)`重新计算根Content及其子流程闭包内容指纹，能拒绝工程Revision、能力目录以及被调子流程定义变更后旧建议。

~~~csharp
// 本轮新增的真实API：来源清单，不是最终可执行Plan。
var snapshot = session.GetSnapshot();
var catalogSnapshot = new MDCStaticCatalogSnapshot(capabilityCatalog);
var manifest = new MDCStaticSourceManifestBuilder().Create(
    snapshot, contentId, catalogSnapshot);
if (!manifest.HasCompleteStaticMetadata)
    throw new InvalidOperationException("专业能力静态元数据不足，不能自动发布");
manifest.RequireCurrent(session.GetSnapshot(),
    new MDCStaticCatalogSnapshot(capabilityCatalog));
// 之后仍由唯一MOC/MRK原编译/绑定/Host授权体系完成运行。
~~~

**明确停止线**：此产物只有来源与依赖校验，没有控制DAG、参数、执行策略或可执行委托，不可命名为完整`MDCPlanArtifact`，更不可绕开`MOCBuildService`。其价值是为后续真正的声明Plan编译和MRK Loader拆分提供无重复Core数据的稳定源身份。

### 静态来源合同补充：必须有Core正式子流程引用

`MDCStaticSourceManifestBuilder`不仅确认Invoke目标Content存在，还复用原MOC Compiler已执行的Core真源规则：**父Content必须有对应`MRIReferenceKind.Content`且TargetId完全匹配**。未声明引用时返回`decision.source_manifest.missing_content_reference`；目标不存在时返回`decision.source_manifest.missing_content`。因此不得凭一个`moc.invoke:GUID`字符串或`MOLControlBlock.InvokeContent`就声称静态来源可被合法调用。

这仍只是源身份和引用前置条件，不能替代原MOC的子流程PublicPorts、跨层依赖、MRK真实能力绑定、发布权限校验。

## 18. Manifest到原MRK编译器的严格交接适配（D05 P0增量）

新增`Mechanism/Orchestration/MOCManifestRuntimeBinding.cs`（**运行集成侧，不得迁入未来无MRK引用的Decision.dll**）。`Bind(MEMProjectSnapshot,MDCStaticSourceManifest,MDCStaticCatalogSnapshot,IMRKCapabilityResolver,professionalTypes?,allowUnverifiedLegacyContracts=false)`按以下顺序完成：先重算并验证Core来源/目录版本 → Reject未知静态Capability → 使用单次编译作用域内的Resolver缓存固定同一CapabilityId的真实委托身份 → 对每项已使用能力进行MPC/CLR输入与具名输出合同检查 → 严格拒绝Rejected和Unknown（除非调用方显式选用遗留兼容开关）→ **调用原`MOCEngineeringCompiler.Compile`生成唯一MRK Plan及原SourceMap** → 校验编译结果ProjectId/Revision。

`allowUnverifiedLegacyContracts=true`不是自动授权或安全完成证明，仅为明确的旧Provider兼容通路；静态Hard错仍不可绕过。若实际Resolver实现`IMRKVersionedCapabilityResolver`，编译作用域缓存桥还会透传版本查询接口，不以缓存破坏现有MRK Version Gate。编译过程不得调用任何`ExecuteAsync`，未发布/运行的Plan不能获得设备资源租约。必须由Host独立执行权限、能力有效性、执行组与设备安全检查。

这是未来`Runtime.LoadAndValidate`的可迁移原型，不是已经完成的`MDCDeclarativePlan`或独立Decision程序集；最终项目融合前还必须把所有MOC静态控制编译从运行委托/Context中分离。

### D05运行交接增加第三层静态Core端口校验

`MOCManifestRuntimeBinding.Bind`在查询任何MRK Resolver之前，针对Manifest中全部V2 Content复用现有`MDCStaticBuildPreflight.Assess(wiring, MDCStaticCatalogSnapshot)`。因此交接链现在为**Core/MOL版本与正式引用→工程节点MPC端口与专业静态Schema→专业静态Schema与MRK CLR运行签名→原MOC编译器**。静态Port硬冲突立即拒绝并附原ContentId/RuleId，不能由于后续执行签名正确就放行过时工程定义；未验证静态描述只有显式Legacy兼容开关可继续原执行约束检查。该接口不复制MOL Graph，不执行委托，不触达设备；Studio/Headless使用同一个静态预检。


## 19. D05首批无Runtime的描述式Plan（静态编译阶段，非最终MRK lowering）

`MDCDeclarativePlanCompiler.Compile(MEMProjectSnapshot,rootContentId,MDCStaticCatalogSnapshot)`现在从已实现的`MDCStaticSourceManifest`收敛出**瞬时不可编辑Plan IR**：`MDCDeclarativePlan`（工程Revision、Root、目录/来源SHA-256）、`MDCPlanContent`、`MDCPlanBlock`、`MDCPlanLink`、`MDCPlanPort`、`MDCPlanNamedPort`、`MDCPlanLiteral`和`MDCPlanPolicy`。无MRK类型、执行委托或设备句柄。

- V1结构化原`BlockId/Kind`、Branch的Case顺序与父子关系、Loop/Foreach的动态最大迭代数、Try/Catch/Finally、Invoke目标身份均保留；**绝不按迭代次数静态展开循环**。
- V2保留原`EntryBlockId`、Control/Data/Stream的原LinkId、端口端点、ConditionId和Order，以及业务端口TypeId、单位、坐标/时基、公开FlowInputs/FlowOutputs、字面量与声明式重试/超时/执行组策略。
- 对复杂表达式AST、StateMachine状态与转移、Loop条件、具名Catch精细语义，本切片同时保留一份**防御性复制的原MOL规范化Logic Aspect负载**；它是不可变来源证据而非另一个工程真源，不用半完成的私有规则解释器代替Core。对同一工程、目录产生稳定来源指纹，`RequireCurrent`使Core/目录变更后旧Plan失效。

示例：
~~~csharp
var plan = new MDCDeclarativePlanCompiler().Compile(
    session.GetSnapshot(), contentId, new MDCStaticCatalogSnapshot(catalog));
plan.RequireCurrent(session.GetSnapshot(), new MDCStaticCatalogSnapshot(catalog));
// 过渡阶段仍经已有MOC/MRK唯一编译、绑定与执行体系：
var originalCompiled = new MOCManifestRuntimeBinding().Bind(
    session.GetSnapshot(), plan, new MDCStaticCatalogSnapshot(catalog), resolver);
~~~

**严格分层声明**：目前这是无Runtime的源描述与索引IR（Static Phase 1），尚未将V1表达式、V2 StateMachine/Guard与资源策略全部降低成**自足、可被MRK独立加载的执行IR**；桥接器虽接收此Plan，仍委托原MOCEngineeringCompiler完成最终lowering。不能宣称P0纯Plan迁移、项目解环或完整State/Loop行为等价证明已完成。

## 20. D05语义提取第二阶段：强类型表达式、条件、状态机

新增`MDCPlanSemantics.cs`，将`MDCPlanSemanticIndex`作为每个`MDCPlanContent.Semantics`的只读强类型投影，**不建立第二种规则解析或求值引擎**：

- `MDCPlanExpression`：保留原`MOLLogicalOperator`（Scalar/All/Any/Not）、Source、Name、Comparison、Expected以及有界递归Terms；任一Expr深度≤16、总节点≤128，只有原值语义，不执行C#脚本。
- `MDCPlanBranchArm`：原BranchId、Case顺序、BodyId、Label与默认Else（Condition=null）；`MDCPlanLoopRule`：原LoopId、Mode、动态预算、CollectionInput与完整条件树。
- `MDCPlanCatchRule`、`MDCPlanWaitRule`、`MDCPlanExitRule`：原具名/兜底异常处理、Wait时间与事件合同、Throw错误码。
- `MDCPlanStateRule`、`MDCPlanTransitionRule`：原V2状态机Block、Region、层级、Entry/Exit Capability、EventId、目标状态、Priority及复合Guard。
- `MDCPlanBlockGuard`：原结构Block独立条件表达式。

这些对象供Decision筛选、解释和静态路径推导复用；`MOLLogicCodec`与`MOLWiringCodec`仍是唯一序列化定义真源，`MRK`仍是实际条件求值、运行等待、状态转移和资源权限Owner。第二阶段的强类型IR不是现场仿真或设备执行授权；现有`MOCManifestRuntimeBinding`仍调用原MOC做最终lowering。
