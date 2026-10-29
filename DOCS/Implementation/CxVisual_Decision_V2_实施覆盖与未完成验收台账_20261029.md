# CxVisual Decision V2.0——实施覆盖与未完成验收台账（2026-10-29）
> **2026-10-29 工程实装更新**：已将 MOC/MRK Resolver、编译、Release/Stage 等运行集成代码移至 `Mechanism/Runtime/Execution/DecisionBinding/`，将纯静态 `MOCFlowBuilder` 保留在 Decision，工程依赖改为 Runtime→Decision。详见[单向依赖实施记录](CxVisual_Decision_Runtime单向依赖实装与验收_20261029.md)。下文旧“仍依赖Runtime/28项目”的描述为历史快照，不再代表当前 `master`；**完整纯 Plan Loader/工业 HIL 仍未完成，CI 以此次 SHA 实际记录为准**。


> 本文件为**事实/未完成事项追溯入口**，不是第八份权威技术规格。架构仍以[#0](../Architecture/CxVisual_统一决策与规划体系%230总体架构与全类需求_V2.0.md)与[#1～#6](../Architecture/CxVisual_统一决策与规划体系%231对象合同与开发者API_V2.0.md)为准，测试母表仍为[Decision验收矩阵](../Testing/CxVisual_Decision_功能与迁移验收矩阵_V1.0.md)。源码现状以仓库最新master为准；以下CI仅证明指定SHA下的软件构建与回归。

## 01. 已提交的连续功能切片与固定Windows证据

| 功能切片 | 真实源类型/入口 | 提交及Windows证据 | 等级 |
|---|---|---|---|
| 静态离线能力发现/端口判断/候选 | `MOCCapabilityCatalog`、`MDCDecisionService` | [738e5cc4](https://github.com/oudream/cx-visual-x3/commit/738e5cc4ae028828cab670ce6db9c4925231ec7a)、[Run 40813421838](https://github.com/oudream/cx-visual-x3/actions/runs/40813421838) | Automated |
| 目标证据、Pareto与稳定选型 | `MDCChoiceService` | [57d99a3](https://github.com/oudream/cx-visual-x3/commit/57d99a3ad6414ab95136550470698803b802e8fd)、[Run 40813828127](https://github.com/oudream/cx-visual-x3/actions/runs/40813828127) | Automated |
| MPC描述与MRK CLR签名边界校验 | `MOCBindingContractInspector`、Vision Provider | [8b979e69](https://github.com/oudream/cx-visual-x3/commit/8b979e69a688e87f212f80d0f5ddd76071c0fc71)、[Run 40820560531](https://github.com/oudream/cx-visual-x3/actions/runs/40820560531) | Automated |
| Studio加入工程主流程前的签名门禁 | 原`StudioShell.ModuleCenter` Core编辑事务前调用 | [a6f43b8d](https://github.com/oudream/cx-visual-x3/commit/a6f43b8d0016c4ca08e6aa95e7a6c1428d74fcec)、[Decision Run 40821146732](https://github.com/oudream/cx-visual-x3/actions/runs/40821146732)、[ModuleCenter Run 40821146764](https://github.com/oudream/cx-visual-x3/actions/runs/40821146764) | 该入口的自动化回归；非全部Studio交互 |
| 资源区间可行性/硬预算与择优联动 | `MDCResourceBudgetPlanner` | [4f4fc3ee](https://github.com/oudream/cx-visual-x3/commit/4f4fc3ee4da2ac266c1ea78069ad170d68e6d7c7)、[Run 40821840370](https://github.com/oudream/cx-visual-x3/actions/runs/40821840370) | Automated |
| 能力目录稳定指纹/过期推荐拒绝 | `MDCStaticCatalogSnapshot`、`MDCDecisionVersionStamp`、`FindCandidatesStamped` | [21d1fb8d](https://github.com/oudream/cx-visual-x3/commit/21d1fb8d0ee6bc9605d0b090f4ab1c73b6ebdaa6)、[Run 40822268636](https://github.com/oudream/cx-visual-x3/actions/runs/40822268636) | Automated |
| 一调用静态能力/预算/目标比较 | `MDCRecommendationService` | [1cbfab7b](https://github.com/oudream/cx-visual-x3/commit/1cbfab7bcfc8a1998f1cc56fb5835fe1ba5e47eb)、[Run 40822851898](https://github.com/oudream/cx-visual-x3/actions/runs/40822851898) | Automated |
| 能力增删改→工程Content/Block影响 | `MDCCatalogImpactAnalyzer`、`MDCProjectImpactAnalyzer` | [cd2c012e](https://github.com/oudream/cx-visual-x3/commit/cd2c012e8e1851c0da39ea5ea89fead57b2a2a13)、[Run 40823510242](https://github.com/oudream/cx-visual-x3/actions/runs/40823510242) | Automated |

**验证边界**：以上为原.NET Framework 4.8解决方案构建、`ArchitectureSmoke.exe --decision`及默认回归成功记录；不是CT/X-Ray现场算法精度、GPU实际性能、PLC安全/权限、HIL或生产部署证明。`MDC*`当前仍编译在`CxVisual.Mechanism.Orchestration`过渡程序集，不等于已存在独立`CxVisual.Mechanism.Decision.dll`。

## 02. 十大能力覆盖与完整目标差距

| D能力 | 本轮已有可验证实现 | 未完成或仍归其他Owner的职责 | 当前Decision等级 |
|---|---|---|---|
| D01语义理解推导 | MPC TypeId/单位/坐标与Core逻辑读取 | 完整专业语义/约束传播/跨专业适配依据 | 部分Automated |
| D02合法性与可行性 | 三态端口/MRK签名/资源Hard判断 | 全部权限、依赖、设备安全门禁和有效证据核验 | 部分Automated |
| D03目标约束协调 | 目标方向/优先级、硬过滤后软比较 | 多来源规则冲突、可变目标权衡及证据时效管理 | 部分Automated |
| D04能力匹配方案选择 | 目录筛选、合规候选、Pareto与版本封印 | Studio全量上下文工具箱/复杂专业能力多级兼容 | 部分Automated |
| D05逻辑协作规划 | Core/MOL已有定义、MOC现有编译可复用 | **Decision纯静态Plan与无Runtime引用、跨流程约束规划** | Decision目标未完成 |
| D06策略制定协调 | 可以从现有选项给出首选与可比较备用候选 | 故障降级/重试/恢复决策与实际Host安全流程协作 | 目标未完成 |
| D07资源性能规划 | 有证据的静态资源需求区间预算与候选过滤 | 真正的专业资源估算器、并行/批次调优、动态负载预测 | 部分Automated |
| D08预测推演优化 | 有证据静态Pareto与区间判断 | 专业性能/质量预测模型、策略优化、置信区间和实测闭环 | 部分Automated |
| D09验证仿真影响 | MOL/MRK/PSM各自已有验证/虚拟基础可复用 | Decision静态路径推演、模型适用性核验和多场景仿真证明 | Decision目标未完成 |
| D10决策解释反馈 | RuleId、SourceMap相关元数据、目录与Core Block影响集 | 基于真实RunId/Trace/MDS的根因证据与反馈优化 | 部分Automated |

## 03. 工程归属与项目迁移P0：尚未解除的真实阻断项

1. **当前仍28项目/Mechanism 9**：Logic只含AssemblyInfo，Orchestration持有MOC编译与新MDC静态类；项目还未融合为`Mechanism/Decision/`、27项目。不得因Namespace提前为Decision就说物理合并完成。
2. `MOCCapabilityCatalog(providers,resolver)`历史兼容入口、`MOCBuildService.Build(...,IMRKCapabilityResolver)`、`MOCEngineeringCompiler`生成`MRKExecutionPlan`、`MOCCompiledPlan`持有运行委托合同，因此`Orchestration.csproj`仍引用`Mechanism.Runtime`。
3. **安全迁移顺序**：明确静态Snapshot/Port/Plan的不可变序列化规格与MRK Binder接口 → 逐类拆MOC纯编译、运行装配及Trace视图 → 保留旧API和SourceMap/动态Loop/Foreach/命名数据/发布Goldens → 修改引用与`.sln`/GUID/命名空间 → Windows全量CI及Studio/TestCenter → 专业Owner和HIL；不能先改名后形成循环。
4. 本轮未核实`DOCS/02～04` XLSX逐Sheet与`ReferTo`全部专业源码，后续实施前必须按类型/Owner核对；不可将聊天或文档设计作合并的唯一依据。

## 04. 不能误报为已完成的工业目标

- Studio画布并未全面接入候选过滤、参数引导、局部增量约束求解、复杂流程诊断/优化；已有ModuleCenter Vision入口的签名预检只是局部集成。
- 专业Provider/真实CPU算法与MRK有可用软件案例，但CT3D、Automation、Inspection、HMI和Simulation的全部真实生产闭环均未逐能力验收。
- Decision不能以注解或静态预算直接替代设备Host权限、PLC联锁、MRK真正租约、异常对账、未知状态处置或生产发布。
- 预测值、Virtual/Replay/CI与真实GPU/CT/X-Ray/运动设备的HIL/Field证据必须分级；不得自动提升。

## 05. 后续开发优先门禁

| 优先级 | 必须完成的开发及验证 | 可通过条件 |
|---|---|---|
| P0 | 静态纯Plan与MRK执行绑定无环拆分 | Decision↛Runtime；序列化不包含Delegate/Frame/设备句柄；既有MRK/SourceMap/Loop功能Goldens一致 |
| P0 | Logic + Orchestration安全融合并落地唯一Decision项目 | 无额外项目、全量调用方/编译/Windows CI通过，可回退 |
| P1 | Studio场景化选Symbol/Content/Capability及约束引导 | 同一SDK/Studio判定；Core Edit/Undo一致；旧异步建议不可应用 |
| P1 | D06安全策略、D09静态路径及Simulation模型联动 | Unknown/危险副作用安全门禁，模拟不触达真实设备，正反测试 |
| P1 | 六专业完整轻量注册与真实例程 | 各专业最少一个声明→发现→编排→运行→调试→解释闭环及证据 |
| P1 | 完整性能、并发、压力、设备验证 | 固定硬件/工作量基线，p50/p95/PeakMemory、取消/稳定性、HIL签署 |

**完成定义**：需求→真实对象/Owner→开发者API→实际使用→自动测试→WindowsCI→适用专业真实联调/现场证据齐备，才可对对应能力宣称完整。文档或几个正向Smoke的存在不能替代最终验收。

## 06. D06外部副作用安全策略首批切片（软件阶段）

`MDCRecoverySafetyAssessor`纯静态判断专业OperationEffect与实际ACK收据，拒绝非幂等Unknown重试、保留未验证Idempotency/Host权限为Unknown，对无外部副作用或已证明的合格操作给出**策略资格**。ArchitectureSmoke新增MDC-50～55，真实设备动作数为0。完整“策略制定与协调”（设备主备切换、执行窗口、动态恢复、资源重新规划）与专业HIL仍未完成。本批提交/Windows验收状态按实际CI登记，不因存在此类就将D06标记完整。

## 07. P0静态MOLWiring与专业Schema预检的工程切片

新增`Mechanism/Orchestration/MDCStaticBuildPreflight.cs`，不需要MRK Resolver，对既有`MOLWiringDefinition`和`MOCCapabilityCatalog`进行单一Content端口预检并返回可精确定位的RuleId。复用Core原来的草稿/正式Build校验，不另建工程Graph、第二种Compiler或MRK流程执行器。新增MDC-56～60正反测试。此次只是迈向纯Decision Plan的静态前置检查，`MOCBuildService.Build`与`MOCEngineeringCompiler`仍存在对Runtime的依赖，物理融合P0未解除。

## 08. D09静态路径推演（软件试点，并非仿真引擎）

新增`MDCStaticPathExplorer.cs`，从Core权威MOLWiring定义计算可能控制路径，支持有/无`ConditionId`假设、未知与错误分支说明，不执行任何Provider、MRK、设备动作。测试采用真实`MOCFlowBuilder.Edit`生成四节点真假分支与Join；待Windows验收后可记静态路径能力Automated。真正的PSM虚拟模型、实时Clock/设备状态、安全隔离、历史Replay、HIL和专业验证仍是D09的未完成核心要求。

## 09. 静态预检对MOC真实画布控制端口的兼容修复

专业Capability目录通常只声明Data/Stream业务端口，MOL的`Start/Done`由原`MOCFlowBuilder.Step`附加，属于Core控制连线合同，不应被`MDCStaticBuildPreflight`误报为未声明的专业端口。保留强校验：专业必需端口缺失/类型冲突拒绝，额外业务Data端口Unknown；排除Control/其他非业务端口作为额外专业端口的误判。新增真实`MOCFlowBuilder.Edit`产生的画布节点正例及额外Data负例，结果应由Windows CI核验。完整跨Content与MRK Binder的静态拆分仍未完成。


## 10. P0纯静态来源与原MOL引用闭包（首批实现）

增加`MDCStaticSourceManifest.cs`，只读`MEMProjectSnapshot`及MOL正式Codec，生成Content/Block/Capability子流程来源闭包及稳定指纹；对不完整的专业目录保留Unknown，缺失Content/引用环拒绝。公共V1遍历抽入`MDCLogicTraversal`，D10 Impact与来源提取使用同一实现。真实工程正反例包含V1调用V2、目录版本和子流程变更、缺失Content及环。本产物**不是执行Plan**；`Orchestration.csproj`仍有MRK Runtime引用、未合并27项目；Windows结果待对应SHA/Run核定。

## 11. 静态目录与MRK探测分文件解耦（本阶段仍28项目）

调整原`MOCCapabilityCatalog.cs`为无MRK类型导入的纯目录实现；新增`MOCCapabilityCatalog.Runtime.cs`保存旧`IMRKCapabilityResolver`构造和`GetAvailability`。复用原`MDCDecisionTests.OfflineCatalogAndLegacyResolver`证明旧API/Resolver探测次数、离线目录不触达Runtime的既有行为。没有变更正式程序集名、GUID、工程引用或专业Provider；属于向Decision无Runtime依赖迁移的一项**源码前置措施**，P0工程DAG尚未通过，测试状态以本提交Windows Actions为准。

## 12. 修复静态来源闭包的Core ContentReference合法性缺口

审计原`MOCEngineeringCompiler`确认：无论V1 Invoke还是V2 `moc.invoke:GUID`，除了目标Content存在，还必须由父Content正式声明`MRIReferenceKind.Content`。因此强化`MDCStaticSourceManifestBuilder`的显式引用验证（不存在Content和存在但缺Core引用分别报错），测试用真实`MEMEngineeringSession.Edit(...Reference(...))`构造合法父子闭包，新增目标存在但没正式引用的负例。修复前的来源闭包自动CI成功不等于此安全语义已得到覆盖；以本修复提交CI为准。

## 13. P0静态源码层的Runtime防回流工作流门禁

Decision Windows CI新增`Verify static Decision source files are Runtime-free`步骤，覆盖已拆出的12个静态目录/判断/规划源文件，禁止静态侧再导入Runtime命名空间或执行器、Resolver、Plan、FlowHandle等正式MRK类型；原Resolver兼容入口独立留在`MOCCapabilityCatalog.Runtime.cs`。这保护的是**当前部分源码边界**，尚不能证明整个Orchestration程序集已移除Runtime引用、27项目融合或全局依赖无环。该工作流本身的成功与否以本提交的Windows Run为准。

## 14. 补齐V2 Invoke的真实Core引用正例

ArchitectureSmoke在V1父调用V2子之外，再由原`MOCFlowBuilder.InvokeContent`生成V2父调用V2子，正式登记`MRIReferenceKind.Content`，验证来源Manifest只列原父、子Content和引用目标，不复制子流程、不将Invoke伪装成专业Provider。没有MRK执行或模拟HIL；完整V2跨层参数、具名数据、循环和真实Plan Loader仍未完成。具体Windows记录以该提交的Run为准。

## 15. D05可实际交付的静态来源→原运行编译器边界（P0）

新增`MOCManifestRuntimeBinding`：只在过渡Orchestration/Runtime集成边界访问MRK，要求Core/目录版本和MPC/CLR契约一致后，复用原`MOCEngineeringCompiler`生成现有Plan/SourceMap。支持编译作用域内一次Resolver身份缓存与版本接口透传，未知输入签名默认拒绝，旧版兼容须主动显式启用。新增MDC-82～88正反测试；不创建第二个Compiler，也不修改旧的`Compile`公开入口。最终物理Decision项目尚未完成Runtime类型解耦。Windows构建/回归结果须以后续固定SHA记录，不能因为源文件存在就宣称P0融合完成。

## 16. D05三段式合同检查与静态预检复用

`MDCStaticBuildPreflight`补充接收`MDCStaticCatalogSnapshot`的重载，和现有MOC目录共用同一`AssessCore`，不增加新规则Owner。`MOCManifestRuntimeBinding`在任何Resolver探测之前逐Content验证原MOL业务端口，与MPC静态描述不一致时拒绝；再通过已有`MOCBindingContractInspector`验证MRK CLR签名，最后用唯一原编译器绑定。增加MDC-89反例，真实提交SHA/Windows CI是通过证据；物理Logic/Orchestration合并、纯执行无关Plan尚未完成。

## 17. D05受控来源到原MOC/MRK编译器：实际Windows固定证据（2026-10-29）

| 提交 | 变更与Windows结果 | 等级 |
|---|---|---|
| [5451819a](https://github.com/oudream/cx-visual-x3/commit/5451819a80b077e26d29a1d061115b7bca30c8be) | 增加`MOCManifestRuntimeBinding`，版本/合同核对、编译作用域内Resolver固定、原MOC编译与SourceMap复用；[Decision Run 40831471885](https://github.com/oudream/cx-visual-x3/actions/runs/40831471885) **Success** | Automated |
| [59d95b0a](https://github.com/oudream/cx-visual-x3/commit/59d95b0a0125104d4b0777fa7bbae508bc5669fc) | 复用静态V2端口预检；[Decision Run 40831977051](https://github.com/oudream/cx-visual-x3/actions/runs/40831977051) **Failure**：`MDCStaticBuildPreflight.cs`残留`found.Definition.Ports`，对`MOCCapabilityDescriptor`访问错误CS1061 | 构建失败，已修 |
| [efecd87c](https://github.com/oudream/cx-visual-x3/commit/efecd87cc99aab6a19bbf59845dc032aafc4c013) | 修正为`found.Ports`；新增原`MOCFlowBuilder`真实V2 Action→Manifest→绑定→SourceMap正例。[Decision Run 40832214673](https://github.com/oudream/cx-visual-x3/actions/runs/40832214673) **Success**；[ModuleCenter CPU RAW Run 40832214907](https://github.com/oudream/cx-visual-x3/actions/runs/40832214907) **Success** | Automated（软件） |

**未完成**：唯一`Mechanism.Decision`物理工程、Orchestration→Runtime的项目依赖解除、独立可序列化纯Plan及MRK Loader、六专业完整资源/仿真/现场HIL。软件CI不证明外部设备安全或发布授权。


## 18. D05静态描述式Plan Phase 1 实际交付与剩余差距

新增`MDCDeclarativePlanCompiler`及不可编辑Plan DTO，在原V1/V2 Core/MOL单一工程真源上构建SourceMap可追溯的原Content/Block索引。V1包含Branch、Loop/Foreach与Try结构关系，V2包含原控制与数据连线、端口、命名输入输出、字面量及步骤策略；完整原MOL规范化负载保留Complex Expression/StateMachine等尚未独立降低语义。不携带Delegate/MRKExecutionPlan/设备句柄、不产生第二套持久工程Graph。过渡`MOCManifestRuntimeBinding`增加Plan重载，重检版本后仍由原MOC Compiler做唯一最终lowering。测试以真实`MEMEngineering`和`MOCFlowBuilder`编排正例及已提交运行编译器回归为准。

**关键未完成**：这是Phase 1“描述式IR”，不是全部控制语义已lowering的Runtime独立执行Plan；MRK Loader、工厂、Policy/State/Loop完整静态编译、27项目融合和多专业真实HIL均不在此批完成范围。WindowsCI状态须以后续Run登记。

## 19. D05强类型控制表达式与状态机语义抽取（Phase 2A）

`MDCPlanSemantics.cs`提供`MDCPlanExpression/BranchArm/LoopRule/CatchRule/WaitRule/ExitRule/StateRule/TransitionRule/BlockGuard`及只读`MDCPlanSemanticIndex`。复用唯一Core/MOL源，保留V1复合条件和结构化异常/动态循环规则、V2状态机正交区域/转移Guard的全部主要静态属性；不提供运行求值或第二种解释器，仍保留完整Codec payload作为无损溯源。测试用真正Core工程V1 If/Loop/Foreach/具名Catch/Wait/Throw、V2新StateMachine+复合Guard以及原有真实连线，验证只读内容与执行隔离。独立纯Plan lowering与项目合并仍待后续阶段，Windows结果须以后续固定SHA验收。

## 20. SourceMap静态对象从原MOC运行编译器文件中无损分离

将`MOCStepOrigin`和`MOCSourceMap`原样迁入`MOCSourceMap.cs`，保留历史namespace、构造与公开属性；原`MOCEngineeringCompiler`及`MOCTraceView`继续使用它们，不引入新映射真源。新增`MDCDecisionTests`正向TryResolve/Find一致性测试，并将其加入Decision Windows CI Runtime-free检查清单。此举降低后续27项目融合的源码耦合，但仍是同程序集的文件级重构，不能当成实际DLL分离和工程无环完成。

## 21. D05静态Plan、控制语义与SourceMap解耦的固定Windows证据（2026-10-29）

| 提交 | 实现范围 | Decision .NET48 | ModuleCenter CPU RAW |
|---|---|---|---|
| [c34e679d](https://github.com/oudream/cx-visual-x3/commit/c34e679db3771aaf10d63d9e902caf332b5182ba) | `MDCDeclarativePlan`首批Runtime-free结构索引、V1动态Loop/Branch/Try、V2边/端口/参数/策略、兼容绑定器 | [40835077828 成功](https://github.com/oudream/cx-visual-x3/actions/runs/40835077828) | [40835077779 成功](https://github.com/oudream/cx-visual-x3/actions/runs/40835077779) |
| [f0af7af8](https://github.com/oudream/cx-visual-x3/commit/f0af7af8194a621f614cdc286ff63e9bccb98f34) | `MDCPlanSemanticIndex`结构化表达式/Branch/Loop/Catch/Wait/Exit/StateMachine强类型投影 | [40835739253 成功](https://github.com/oudream/cx-visual-x3/actions/runs/40835739253) | [40835739229 成功](https://github.com/oudream/cx-visual-x3/actions/runs/40835739229) |
| [192a97eb](https://github.com/oudream/cx-visual-x3/commit/192a97ebbb372379925c17406658007f48a53ac0) | 原`MOCSourceMap`静态DTO原样独立源文件、保持SourceMap与Trace兼容 | [40836202294 成功](https://github.com/oudream/cx-visual-x3/actions/runs/40836202294) | [40836202283 成功](https://github.com/oudream/cx-visual-x3/actions/runs/40836202283) |

验证证据为Windows 2022 .NET Framework 4.8 x64构建、Decision专项和默认ArchitectureSmoke，并包含ModuleCenter CPU RAW已有WPF回归。真实CT/GPU/PLC/HIL/Field验收和跨项目运行实验未因此通过。

**下一依赖门禁**：将目前仍由原MOC Compiler处理的复杂语义最终lowering变成完全自足、可版本化的执行描述；由MRK真实Loader消费并与现有Loop/Foreach/State/Trace/SourceMap Goldens等价；删除Orchestration对MRK的工程引用。之后才可安全实施27项目的`Logic+Orchestration→Decision`物理合并。当前仍28项目，勿将静态DTO拆文件误报为解环。
