# CxVisual Decision P0——离线能力发现与静态约束筛选 实现验收 V1.0

> 状态：**首批静态判断与多目标Pareto已完成软件源码、Windows .NET Framework 4.8编译、Decision专项及默认ArchitectureSmoke回归；Studio/HIL/完整Decision项目迁移仍未完成。** 起点`master@6bb99a5e293ed54b1db78b53ffd3b2e8b1aa9132`（2026-10-29），后续以提交SHA为准。当前工程数仍28，Orchestration仍引用Runtime，**未进行Decision物理合并或Runtime整体断环**。

## 一、真实需求与已经实现的切片

目的：专业能力在尚未初始化真实Resolver和设备的情况下，Studio/SDK应能读取已声明的能力/输入端口，验证简单静态连线，区别可用、硬拒绝、未知条件。用户不要求此场景必须创建MEM工程或启动MRK执行引擎。

| 文件 | 真实增量 | Owner/保持的边界 |
|---|---|---|
| `Mechanism/Orchestration/MOCCapabilityCatalog.cs` | 新增`MOCCapabilityCatalog(providers)`离线构造，`AvailabilityKnown`区分未检查/已检查；保留原`(providers,resolver)`兼容 | **MOC唯一只读目录**，无第二个能力注册中心；运行解析仍归MRK |
| `Mechanism/Orchestration/MDCDecisionService.cs` | 新增`MDCVerdict`、`MDCConstraintOutcome`、`MDCAssessment`、`MDCCandidate`与`MDCDecisionService` | 过渡命名空间`CxVisual.Mechanism.Decision`，工程暂属Orchestration。只读评估，无设备/编辑/运行副作用 |
| `Tests/ArchitectureSmoke/Orchestration/Compilation/MDCDecisionTests.cs` | 离线目录、旧Resolver、三态连接、筛选候选、硬约束优先级、重复Id拒绝 | 专项`--decision`与默认回归 |
| `Tests/ArchitectureSmoke/Program.cs` / `Orchestration.csproj` | 登记测试入口和明确编译文件 | 不修改SLN，不引入新依赖 |

## 二、输入/输出/行为合同

~~~mermaid
flowchart LR
    P["Professional Descriptor/Provider"] --> C["MOCCapabilityCatalog（offline）"]
    S["源MPC端口"] --> D["MDCDecisionService"]
    C --> D
    D --> A["Allowed/Rejected/Unknown + RuleId + TargetPort"]
    A --> U["Studio/SDK消费（Studio接入待做）"]
    C -.->|"旧构造可按需查询"| R["MRK CapabilityResolver"]
~~~

- `AssessConnection`要求明确端口方向、Kind、TypeId完全相等，MTU单位、坐标和时间基对应一致；不隐式做转换。缺一侧物理单位/坐标/时间基时给Unknown；硬不匹配给Rejected。校准身份不一致或缺失时Unknown。
- `FindCandidates`复用原目录Search并保留不可用候选的原因，不软评分、不实例化MRK，不宣称运行许可。
- `AssessHardConstraints`合并已计算单项规则；Hard Fail优先于Unknown；重复RuleId非法；完整规则求解与动态授权仍属待开发。
- 原`RuntimeAvailable`保持旧语义，新增`AvailabilityKnown`区分“未查询”与“查询后不可解析”。离线目录不接入真实执行器。
- `MDCVerdict.Allowed`仅代表此静态合同通过，**不等于设备已授权、内存预算满足或生产系统可执行**。

## 三、实际测试入口与状态

- Windows x64预期：构建`CxVisual.Unified.sln`，再运行`CxVisual.Tests.ArchitectureSmoke.exe --decision`与默认ArchitectureSmoke。
- 单元正反例：离线发现无Resolver、旧构造查询次数、重复CapabilityId、类型位深模拟的TypeId不匹配、单位Unknown/不等、Input→Input非法、缺输入能力、HardFail/Unknown优先级、重复ConstraintId。
- **实际运行结果**：本轮提交前在当前Linux环境没有.NET Framework 4.8 MSBuild/csc/mono；无法本地构建。本文件暂标“源码已编写，未验证”，之后以Windows Actions日志更新，不可预填成功。
- **仍未做**：完整MPC TypeId→MRK CLR映射、FCM自动属性投影、Attribute/Fluent注册、WPF过滤、静态纯Plan拆分、资源/多目标策略、Simulation/HIL、真实六专业链；大型DOCS/02～04逐SheetOwner核对仍有门禁。

## 四、下一批最优先实现

P0-Next：让真正的纯静态Descriptor合同独立于Orchestration Runtime项目引用（先去除MOC/MRK循环依赖），用TypeId/CLR显式映射验证同一Provider的描述与运行端口，再完善功能的真实Studio消费。只有经过Windows编译/Smoke和前后Golden后，才执行Logic/Orchestration物理合并及项目数从28到27的结构迁移。


## 五、P0之后增构：有实测依据的多目标选择（同一过渡工程）

新增\`Mechanism/Orchestration/MDCObjectiveSelection.cs\`，沿用\`MDCAssessment\`实现HardFail优先、Unknown禁止自动选；\`MDCObjective\`显式方向/优先级/单位，\`MDCMeasuredMetric\`提供数值/EvidenceTier/来源ID，\`MDCChoiceService\`生成候选状态、Pareto与稳定首选。完全不运行设备/重用执行器，也不建立第二Capability目录。测试\`MDCDecisionTests.MultiObjectiveSelection\`覆盖Pareto两方案、Hard GPU超限、仅估计、单位冲突、缺指标与稳定平局。状态仍为“源码已编写；以本批Windows提交/CI为准”，不把EvidenceId自动视为经过签署的真实工业测量。


## 六、真实Windows提交与验收证据（2026-10-29）

| 证据项 | GitHub SHA/Run | 实际结论 |
|---|---|---|
| 首批离线MOC/三态端口/候选 | [738e5cc4](https://github.com/oudream/cx-visual-x3/commit/738e5cc4ae028828cab670ce6db9c4925231ec7a) | C#正式源码、专项测试已提交 |
| Decision独立Windows工作流 | [74277727](https://github.com/oudream/cx-visual-x3/commit/74277727d93903618cd19c47c3765fc0d9bd27c1) / [Run 40813421838](https://github.com/oudream/cx-visual-x3/actions/runs/40813421838) | **Success**：全解决方案.NET48 x64 Build、--decision、默认ArchitectureSmoke |
| 多目标Pareto与反例 | [57d99a3](https://github.com/oudream/cx-visual-x3/commit/57d99a3ad6414ab95136550470698803b802e8fd) / [Run 40813828127](https://github.com/oudream/cx-visual-x3/actions/runs/40813828127) | **Success**：全解决方案.NET48 x64 Build、--decision、默认ArchitectureSmoke；专项现含HardFail、估计证据、单位、Pareto、稳定平局 |

**证据边界**：以上证明代码在上述SHA的Windows构建、Smoke用例通过；没有提供真实CPU/GPU的策略优化效果、六专业全链闭环、Studio实时交互、跨进程Plan安全发布、PLC运动/CT设备HIL或生产可靠性。因此只能记为相应软件能力 \`Automated\`，不能记为 \`Integrated/Field\`。早前本文“提交前Linux无MSBuild/CI待验”的描述属于编写阶段历史，现在以本节固定SHA的实际证据为准。


## 七、MPC静态描述与MRK绑定签名验证（实施中）

新增`Mechanism/Orchestration/MOCBindingContractInspector.cs`：不调用`ExecuteAsync`，对正式MPC端口与MRK输入/具名输出进行差异检查，包括缺少/多余端口、Required与CLR类型冲突、未映射专业TypeId、宽泛object Legacy契约、具名输出与StepResult不同语义。这是确保新Decision未来可纯静态运行的**迁移边界适配器**；最终应留在Runtime/Host侧，不得制造`Decision → Runtime`依赖。新增`MDCDecisionTests.RuntimeBindingContractConsistency`（含真实`CxVisionAcceptedRegionMeasureProvider`）检验正常与错误组合，具体软件验证以本批实际Windows提交和Run为准。旧MOC构建未被悄悄加上强制阻断，避免Legacy Provider未经迁移即破坏产线。


## 八、专业真实Contract与Decision轻量候选合流验证（新增软件案例）

扩展`MDCDecisionTests.RuntimeBindingContractConsistency`：使用真实`CxVisionThresholdProvider(new CxVisionInspectionOptions(),0)`，取得同一Provider的MPC静态`RawPixels/Width/Height/Inspection`以及MRK运行CLR输入/具名输出，以专业显式`vision.cpu.inspection.result → CxVisionInspectionResult`映射核对合同；同时从真实MOC离线目录输入一个`byte[]`输出端口，验证Decision可找到`RawPixels`适配候选。**不读取相机、不执行检测或改动工程**，证明已有专业Provider可以在编排期零设备调用地共用同一能力身份；与需要Legacy`object`的RegionMeasure仍为Unknown形成反例。验证结果以包含该测试的实际Windows CI为准。


## 九、Studio正式编排入口的实际接入（新增）

在现有`Applications/Studio/StudioShell.ModuleCenter.cs`“加入当前主流程”操作中，继续先执行已有`MHS Effective`版本/权限、当前Scope、运行中禁止编辑检查，**再调用`MOCBindingContractInspector.Assess`核对真实Vision Threshold的MPC端口与MRK具名CLR合同**。静态检查不通过（Rejected/Unknown）显示规则原因并阻止`MOCFlowBuilder.Edit`，通过后仍走原来的Core编辑事务和Undo，未直接执行专业算法，也不能将这一步视为生产发布许可。

后续Windows软件证据需同时包含Decision专项和ModuleCenter CPU RAW WPF闭环。当前只为Vision已知软件Provider示例，不宣称所有专业Provider与Studio画布智能过滤都已接入。完整Attribute/Fluent及动态策略仍待开发。


## 十、静态资源预算与方案优化的第一次组合实现

新增`Mechanism/Orchestration/MDCResourceBudgetPlanner.cs`，资源容量快照由Host提供、需求上下界由专业模型提供，两者均需证据Id。判断可用容量与需求区间为Allowed/Rejected/Unknown；结果可以直接进入已有多目标选择的Hard门禁，阻止资源已证实超限的极速GPU方案被推荐。新增`MDCDecisionTests.ResourcePlanningFeedsSelection`覆盖资源区间、单位、证据缺失、预算冲突与CPU/GPU选择反例；当前是静态软件能力，真实运行资源租约归F19/MRK不变。具体Windows通过状态应在实际Run后记录。


## 十一、能力目录内容指纹、版本封印与变更影响集合

新增`MDCStaticCatalogSnapshot`、`MDCDecisionVersionStamp`、`MDCStampedCandidates`、`MDCCatalogImpactAnalyzer`及`FindCandidatesStamped`入口。消费原`MOCCapabilityCatalog`的MPC端口，只读计算稳定SHA-256；现有Source/Runtime/Config Owner不变。旧候选只能在Project Revision、Catalog Fingerprint、PolicyRevision、EnvironmentEpoch全部一致时由使用方显式应用，避免因WPF异步刷新造成过期决策结果变更Core。新增MDC-29～36测试覆盖乱序稳定、类型单位变化、增删改、版本过期和重复Id。完整WPF端事务集成仍待下一工程切片；本批软件编译结果必须引用实际Windows Actions SHA，不得提前标成通过。

## 十二、专业开发者轻量一调用推荐门面

新增`MDCRecommendationService.cs`；将同一`MOCCapabilityCatalog`的MPC候选、已实现`MDCResourceBudgetPlanner`硬预算及`MDCChoiceService` Pareto组合，返回统一`MDCRecommendationResult`，无需创建新的执行引擎/模块注册表。完整输入指纹进一步纳入专业指标和资源证据，使工程版本不变时改变资源预算或实测结果也能拒绝旧推荐。`MDCDecisionTests.EndToEndStaticRecommendation`检验端口错、显存超限、缺证据、前沿首选、版本/指标变化。此阶段还未让六专业Studio全部自动使用门面，仍待专门WPF/生产用例。
