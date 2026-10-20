# CxVisual 流程执行体系 · 三方面架构与分轮实施主基线 V2.1

> **性质**：需求、类型归属、双角色入口、30分钟工作包与实测证据的持续更新基线。本文不是全功能已完成声明。
>
> **实施约束**：\`master\` 直接提交；中文 Commit；C# 7.3 / .NET Framework 4.8 / WPF；沿用现有三方面/项目组织，原则上不增加工程项目。
>
> **核查基线**：本轮启动于 \`65c967ab3ea21fa443a77e9e12822f2396b0ae52\`；需以本次提交的准确 SHA 和 Windows CI 判断验收，禁止引用历史绿灯替代本次结果。
>
> **继承关系**：先前《流程执行体系三方面完整需求、双角色使用与全类架构 V2.0》含 E01～E18 的 **199 子项、32 项使用体验、21 项质量要求、10 个案例**，完整保留为详细需求母版；原《统一运行体系》六角色讨论、差距审计、开发者简洁入口三份文档持续有效。此次增量不删减既定需求。本文只承接执行体系本身，不覆盖工程组织、数据、构建或可视化的权威规格。

## 0. 最终目标与三方面边界

**一句话：软件开发者和工程编排用户都能够简单、自然地让定义好的流程可靠执行、观察、调试和恢复。**

- **EC 执行控制**：顺序、依赖、条件、分支、循环、并行、Join、主子多级流程、结构化退出、控制区域。
- **ES 执行调度**：任务提交、同步/异步、队列、背压、调度、执行组、并发度、优先级、公平性、线程位置、资源仲裁、触发/等待。
- **EM 执行管理**：Flow/Work/Invocation 身份、上下文、生命周期、状态、取消/超时、异常/重试/补偿、Trace/调试、Journal/Checkpoint/恢复与版本固定。

**唯一内核**：\`MRKExecutionEngine\`。EC/ES/EM 是职责切分，不是三个项目或三套执行器。专业业务永远通过已有 Capability/Provider 运行，不将专业 SDK 复制进 MRK。

**协作 Owner**：MEM/MOL 在 Mechanism.Core 保存权威工程与逻辑；MOC 在 Mechanism.Orchestration 校验、编译、生成 SourceMap；MDS 在 Foundation.Data/Mechanism.Data 管 Heap/Stack/Frame/数据生命周期；F19 在 Foundation.Infrastructure 提供 Worker/Queue/Gate/Dedicated；Studio/Visual/Presentation.Wpf 管 UI；Runtime.Host/构建负责生产承载、发布包和可信凭证；专业 Provider 负责设备真实状态及联锁。

**不可混淆**：工程定义/编译计划/Flow 实例；Work/线程；取消请求/安全停止；调用超时/外部设备未执行；未提交输出/已发布 Flow 输出；编辑草稿/生产快照；编辑层 Group/执行组。

## 1. E01～E18 需求归位（维持原 ID，细项不减少）

| ID | 功能群 | 主责 | 主要配合 |
|---|---|---|---|
| E01 | 任务提交与接纳 | ES | F19、EM |
| E02 | 顺序/依赖/结构化控制 | EC | MOL/MOC |
| E03 | 并发、并行、公平性 | ES | F19、EC |
| E04 | 异步/外部等待/唤醒 | ES | Provider、EM |
| E05 | 执行组和串行范围 | ES | F19 |
| E06 | 队列、容量、背压 | ES | F19、MDS |
| E07 | 运行数据交接、端口契约 | EC（消费契约） | MDS（存储 Owner）、MOC |
| E08 | 分支汇合和尾部结算 | EC | ES/EM |
| E09 | 暂停、恢复、取消、超时 | EM | ES/Provider |
| E10 | 资源租约及仲裁 | ES | Host/Provider |
| E11 | 异常、重试、补偿、恢复 | EM | MDS/Host/Provider |
| E12 | 会话、生命周期、长稳 | EM | F19、Host |
| E13 | 状态、快照、Trace | EM | MOC、Studio |
| E14 | 子流程、调用实例、复用 | EC | MEM/MOC/MDS |
| E15 | 事件、触发、定时 | ES | F19、事件机制 |
| E16 | 断点、单步、仿真、重放 | EM | Studio、Simulation |
| E17 | 版本固定、发布、安全切换 | EM（运行版本） | 构建、Host |
| E18 | 策略、执行位置、扩展 | ES | F19、Provider |

**完成判据**：每项功能的真实结果能由运行引擎、MDS、Host 和 Studio 的同一份运行事实证明。不能因为名字相似就判定完成。

## 2. 软件开发者和工程编排用户：同一语义，两种自然入口

### R1 软件开发者（常见场景少概念）

- **普通计算**：直接 C# 方法或 F19 \`Post/Invoke/Submit\`，不强制先创建工程和流程。
- **需要执行语义**：\`Capability → Flow → Build/Freeze → RunAsync\`，需要正式执行则经唯一 Publisher/RuntimeAccessGateway，不走开发捷径。
- **输出必须显式**：单步结果与 Flow 对外结果有区别；具名 \`Export\` 之后，便捷读取才有合法来源。
- **L0/L1 容易用，L2/L3 功能够**：默认 1～3 行调用；资源、执行组、异常策略、数据所有权、远程权限只在需要时显式声明，不做隐式危险猜测。

**R01 新增的便捷 API（仅开发/模拟入口）**：

~~~csharp
MRKDevelopmentRuntime runtime = new MRKCodeRuntimeBuilder()
    .DataCapability("twice", async (ctx, ct) =>
    {
        ct.ThrowIfCancellationRequested();
        ctx.Data.Return("Value", ctx.Data.Get<int>("N") * 2);
        return (object)true;
    }, new Dictionary<string, Type> { { "Value", typeof(int) } },
       new[] { new MRKCapabilityInput("N", typeof(int)) })
    .Flow("double", f => f.StepData("calc", "twice", new[] {
        new MRKExecutionInput("N", MRKInputSource.FlowInput, "request", typeof(int))
    }).Export("answer", "calc.Value"))
    .Build();

try
{
    int answer = await runtime.RunValueAsync<int>("double", (object)6, "answer");
    // answer == 12
}
finally { await runtime.StopAsync(); }
~~~

- \`RunValueAsync<TResult>(flowId, request, outputName, cancellation)\`：单请求 DTO 简写。
- \`RunValueAsync<TResult>(flowId, inputs, outputName, cancellation)\`：显式多端口输入简写。
- **失败不能吞**：Failed 显式异常携带 FlowId 和底层异常；Cancelled 用取消异常表达；缺失或类型错误 Export 由既有 \`GetCommittedOutput<T>\` 报错；原 \`RunAsync\` 完整结果入口不变。
- R01 不支持以“最后一个 Step”猜测输出，不向生产远程客户端暴露未授权 \`MRKFlowHandle\`，也不凭快捷 API 认定外部 Unknown 已恢复。

### R3 工程编排用户（自然的图形操作）

工程组织和 Studio 仍拥有画布；流程执行体系提供可靠执行事实。正常工作步骤：**选择 Content/Flow → 搜能力 → 放节点与 Control/Data 连线 → 设置输入/输出及高级约束 → 检查构建 → CPU仿真 → 运行观察 → 同图断点定位**。

- 日常只展示能力、参数、端口、超时、失败路由；进阶才展示执行组、线程亲和、缓存预算和安全授权。
- 带数据的子流程：必须显式公开参数和输出，不能读取子 Content 内部私有 Step；多个实例需明确 Invocation/实例路径。
- V1 结构化逻辑与 V2 自由连线共用逻辑语义目标，**尚未完成的复杂 V2 Region 不能以 CPU Action DAG 模拟成功代替**。
- 运行中编辑产生新工程 Revision，不改变已经 Pin 的 Plan；Trace + SourceMap 定位真实运行节点，不复制第二套运行状态。

## 3. 全类归属：重构（R）/加强（S）/增构（N）

| 三方面/协作 | 类型 | 动作 | 下一行为与验收 |
|---|---|---|---|
| EC·MRK | \`MRKExecutionEngine\`、\`MRKExecutionPlan\`、\`MRKExecutionContext\` | R | 同一节点多轮 Invocation、结构化 Region、调用帧复用；原 DAG 不回归 |
| EC·MRK | \`MRKExecutionStep\`、\`MRKExecutionPlanCompiler\`、\`MRKExecutionOrchestrationCompiler\`、\`MRKStepCompileRequest\`、\`MRKExecutionInput/Output\` | S | 控制结构编译、类型端口、公开输出及编译拒绝 |
| EC·MRK | \`MRKCodeFlowBuilder\`、\`MRKWorkJoin\`、\`MRKExecutionOperations\` | S | 代码表达简单、Join 尾部语义明确；不新增独立引擎 |
| EC·MRK | **\`MRKControlRegion\`**、**\`MRKInvocationContext\`**、必要的内部退出枚举 | N（候选） | 先写“同Step多轮+Frame隔离”失败用例，确认无法由既有 Context 自然承载后才增构 |
| ES·F19 | \`FCRWorker\`、\`FCRWorkerOptions\`、\`FCRWorkHandle/Result\`、\`FCRDedicatedExecutor\`、\`FCRAsyncGate\`、\`FCRPeriodicJob\` | S | 公平调度、取消背压、线程位置、明确接纳结果、长稳容量 |
| ES·MRK | \`MRKExecutionGroups/GroupDefinition\`、\`MRKResourceLeaseCoordinator\`、\`MRKCorrelationWaiter\`、\`MRKExecutionTrigger\` | S | WholeFlow/Stage/Resource、租约顺序和迟到应答；暂不增加调度 Manager |
| EM·MRK | \`MRKFlowHandle/Result/Snapshot\`、\`MRKStepObservation\`、\`MRKExecutionTrace\`、\`MRKExecutionDebugController\`、\`MRKExecutionPolicy\` | S | Invocation、失败/取消/异常、Trace Gap、断点安全点 |
| EM·MRK | \`MRKRuntimeSession\`、\`MRKExecutionPlanSlot/Publisher\`、\`MRKRuntimeAccessGateway\`、\`MRKExternalOperationTracker\` | S | 可信准入、版本固定、受控切换、Unknown 核对 |
| EM·MRK | \`MRKFileExecutionJournal\`、\`MRKJournalRecovery\`、\`MRKRecoveryDecision\` | R/S | Journal 因果、安全核对、故障恢复；保守拒绝不等于可续跑 |
| EM·MRK | \`MRKDevelopmentRuntime\` | S（**R01**） | \`RunValueAsync<T>\` 薄入口；成功、失败、未导出、类型错误均自动化验证 |
| EM·MRK | **\`MRKExecutionCheckpoint\`**、**\`IMRKExecutionCheckpointStore\`** | N（候选） | 可恢复的稳定执行位置，不将设备 Unknown 当成已执行成功 |
| EM·MRK | **\`MRKRecoveryCoordinator\`**、**\`IMRKExternalOperationReconciler\`** | N（待反证） | 现有 Journal/Tracker/Host 无法满足恢复协调时再建，不提前堆类 |
| 协作·Core | \`MOLLogicDefinition\`、\`MOLWiringDefinition\`、\`MOLControlBlock\`、\`MOLStepPolicy\`、\`MOLStateMachineDefinition\` | R/S | 持久 Logic Aspect 单一真源，V1/V2 控制流语义逐步统一 |
| 协作·MOC | \`MOCEngineeringCompiler\`、\`MOCSourceMap\`、\`MOCFlowBuilder\`、\`MOCBuildService\`、\`MOCCapabilityCatalog\` | R/S | 工程到 Plan + SourceMap，同一内容编排与程序化编辑 |
| 协作·MDS | \`MDSDataContext/Stack/Frame/Heap/Budget\` | S | 每轮数据隔离、公开输出、安全所有权及大数据引用 |
| 协作·UI/Host | \`MOLFlowCanvas\`、\`MOLRuntimeOverlay\`、现有 Host | S | 同图观察/调试、可信远程控制、关闭 Studio 后继续执行 |

**枚举**：优先保留 \`MRKFlowState\`、\`MRKStepState\`、\`MRKRecoveryDisposition\`、\`MRKExternalOperationState\`、\`MRKInputSource\`、\`FCRWorkState\`、\`MOLControlKind\`，新增前必须通过不可表达场景证明必要性。

## 4. 30 分钟一个工作包：依赖顺序与门槛

每轮约 30 分钟只是**开发切片预算**，不保证所有复杂功能在 30 分钟内完成。**一轮至少“正例 + 反例 + 原 Owner 代码 + 自动化 + 文档结论”**，红灯时先修复原轮，不能提前宣称进入下一轮。必要时同主题拆 A/B/C 小轮。

| 轮次 | 本轮纵向结果（不按文件数凑工作量） | 原 Owner | 完成证据 |
|---|---|---|---|
| **R01** | 开发者单值 Export 返回，成功/失败/私有输出/类型错误 | MRK + ArchitectureSmoke | 两种输入均可用；原 \`RunAsync\` 保持；Runtime Windows CI |
| R02 | V1/V2 控制语义差距、If/Else/Switch/Join 首批同语义 | Core/MOC/MRK | 代码/Studio 两入口的确定性输出一致 |
| R03 | ControlRegion 编译基础，DAG 向 Region 兼容 | MRK + MOC | 老流程不回归，非法控制结构编译拒绝 |
| R04 | Invocation 身份 + 每轮 MDS Frame | MRK + MDS | 同一 Step 复用、多次调用无串数据 |
| R05 | For/While/DoWhile/Foreach 动态多节点循环 | MRK + MOC | 高迭代不静态展开、取消、边界、Break/Continue |
| R06 | 多级真实子 Content 的参数/返回/实例复用 | Core/MOC/MDS/MRK | 保存重开、三层参数、双实例不串账 |
| R07 | Join/条件失败/异步事件与补偿边界 | MRK + F19 | All/Any/FirstSuccess 尾部行为和外部 Unknown |
| R08 | 调度公平、执行组 WholeFlow/Stage、资源与容量 | F19 + MRK | 随机取消、配额、饥饿/性能证明 |
| R09 | Invocation 级 Trace、MOC SourceMap 与 Studio 同图调试 | MRK/MOC/Studio | 精确到同名节点不同调用、Trace Gap |
| R10 | Journal/Checkpoint 最小持久恢复制品 | MRK + MDS + Host | 完结快照可核验；未结案不得危险自动重放 |
| R11 | Provider 核对与安全授权恢复、Plan固定/切换 | MRK/Host/Provider | Unknown 无重复外部动作，人工授权可追溯 |
| R12 | 双角色集成、TestCenter、长稳与交付清算 | TestCenter + 全部Owner | 四级自动化、性能基线、不可验证处明确声明 |

## 5. 第一轮 R01 具体完成标准

目标路径：\`Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs\`、\`Tests/ArchitectureSmoke/RuntimeDeveloperExperienceTests.cs\`。**不新建项目、不修改公开生产 Host 安全入口、不创建包装引擎**。

测试矩阵：

1. 单请求 \`RunValueAsync<int>\` 成功，值来自 Flow \`Export\`，而不是末步猜测；
2. 显式 \`IDictionary<string,object>\` 输入等价成功；
3. 未声明的输出名 → \`KeyNotFoundException\`；
4. 输出类型不符 → \`InvalidCastException\`；
5. 未公开的内部 Step 数据不得作为 Flow 输出读取；
6. Flow 本体失败 → \`InvalidOperationException\` 携带 FlowId、失败状态与原始异常；
7. 输出名空白 → 运行前拒绝；
8. 原完整 \`RunAsync\`、原代码 Blueprint/正式发布、既有 NUnit/TestCenter 和 Windows Rebuild 不回归。

验收命令（Windows Server 2022 / VS2022 / .NET48）：

~~~powershell
msbuild.exe .\CxVisual.Unified.sln /m:1 /t:Rebuild /p:Configuration=Release /p:Platform=x64 /nologo /v:minimal
.\Output\Release\x64\CxVisual.Tests.FoundationSmoke.exe --concurrency
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe
~~~

CI：\`.github/workflows/unified-runtime-windows.yml\` 记录必须对应本轮提交 SHA；涉及工程组织/Studio/全局 TestCenter 的验收不能拿 Runtime 绿灯代替。

## 6. 统一验收分级和下一轮进入条件

- S0：有需求或文档；
- S1：源文件或类型存在；
- S2：指定 Windows 目标编译通过；
- S3：真实自动测试正反例通过；
- S4：开发者 API + Studio/Host 跨进程/交互闭环；
- S5：压力、故障、权限、资源与必要现场设备验收。

**状态：R01 已准备源码增量和自动测试，待本轮提交后按真实 Windows CI 更新；R02～R12 均为规划，未因本次文档创建而完成。**

## 7. 本轮需要保护的技术红线

- 编译一个不可变 Plan，但不从 Designer 对象直接启动生产；
- 长时间任务不制造第二 Worker/Graph/Runtime，跨专业仅用稳定 Capability；
- 软件取消 ≠ 设备真正停机；设备 Unknown 不能因异常重试；
- MDS 唯一数据 Owner，图像/Volume 仅按受控 Handle/引用流动；
- Producer 与消费者通过明确 Export/类型和 SourceMap 追踪；
- 工程 Scope、Invocation、Flow、Operation 身份独立；调试不阻塞真实设备安全；
- 每轮中文提交尽量一次；经真实 Windows CI 验收，红灯就留在本轮修复。



### R01 初次 CI 反证修复（固定 SHA 证据）

- 初次提交 \`204ca58ba0b280d9e077663d0008f8b1b7833652\` 已在 Windows Release x64 编译成功，但 ArchitectureSmoke 的新增失败反证未通过。
- 失败并非编译错误或流程返回成功：测试假定原始业务异常直接处于第一层 \`InnerException\`，而 MRK 既有 Work/Step 错误屏障会保留多级包装。
- 修复仅调整测试：要求完整错误链最深处仍为原业务异常、外层便捷 API 显式包含 \`FlowId\` 和 \`Failed\`。不删掉失败用例，不吞异常，不改动引擎错误传播。
- 初次实测证据：[Runtime CI](https://github.com/oudream/cx-visual-x3/actions/runs/39273211753)（failure）、[工程组织 CI](https://github.com/oudream/cx-visual-x3/actions/runs/39273211810)（failure）、[TestCenter CI](https://github.com/oudream/cx-visual-x3/actions/runs/39273211756)（success）。同一根因可能造成共享 ArchitectureSmoke 的多条流水线红灯。
- 修复轮次必须等待新的 \`master\` SHA 所对应 CI 真实结果，再判断 R01 是否达到 S3。


## R02：V1/V2 条件与多路选择首批一致性（2026-10-19）

**归属与事实**：现有 \`MOLControlBlock\` 是 Core 的持久结构定义，V2 \`MOLLink.ConditionId\` 是相同 Logic Aspect 的连线条件；\`MOCEngineeringCompiler\` 编译到现有 MRK Plan。G05a 原已实现 bool:true/false、case:/default 及多源 Join，本轮不复制执行器。

**本轮加强**：
- Core 原类增加 \`MOLControlBlock.Branch(params MOLBranchCase[])\` 便捷表达多级 ElseIf/Switch，第一个 Case 命中、最多一个末尾 Default，错误定义立即拒绝；
- MOC 将 V1表达式与 V2 case 的标量比较统一为原有布尔、数字、字符串比较语义；例如运行值 \`1\` 与字面量 \`1.0\` 匹配；
- V2 重叠但不同字面量的等价 case 按工程连线的稳定保存顺序只执行第一个，与 V1 有序 Case 一致；default 只有无 case 命中才执行；
- V2 仍严格拒绝错误数据类型、丢失源结果、跨源非法 Join、环与无效端口；动态 Region/循环仍归 R03～R05，不假装 R02 已经实现；
- ArchitectureSmoke 增加真实工程 V1/V2 Save/Open、相同 Provider/输入的真值、选中 Case、Join、SourceMap/Trace 和无效定义/原子回滚反证。

**第一轮一次提交前的静态检查**：新增的辅助方法均位于原 Core/MOC 类型；未建立 Graph/Runtime 第二模型、未新增工程项目或不必要的枚举。最终通过与否仍只以本轮 SHA 的 Windows Runtime 和工程组织 CI 为依据。

## R02 真实验收（S3）

- Commit：`a63e978a98328b1498d4fa6e11d4c61507d22abd`，直接 master 单次中文提交。
- 真实 Windows CI 均成功：Runtime `39276599886`；工程组织 `39276599766`；TestCenter `39276599684`；Config L1 `39276599773`；配置公共底座 `39276599691`。
- 原 MOL/MOC/MRK 不重复设计；V1/V2 数值比较、互斥有序 Case、Save/Open、Join 与 SourceMap 已纳入 ArchitectureSmoke。

## R03 控制区域编译基础：范围与验收

**本轮交付范围**：现有 `MRKExecutionPlan` 携带不可变 `MRKControlRegion`（入口、出口、成员）；旧式 DAG 不传 Region 时行为不变；编译必须拒绝未知成员、外部中间依赖、不可达节点、缺失 Join、交叉重叠；允许合法嵌套。没有增加第二套引擎。

**MOC 接入**：V1 结构化 Branch 创建原有 noop 能力入口 Gate，以已有 Case/Join 机制编译形成 Region；运行继续由同一 MRK Engine 执行。V2 自由连线维持现有 DAG 路径，后续阶段再让 V2 的复杂控制块自然生成 Region。

**真实观察**：`MRKFlowHandle.GetSnapshot().RegionStates` 从 Plan 定义与已存在的 Step 状态实时推导，状态只读，不单独产生第二套状态真源。

**正反例**：Region 真实执行成功、原 DAG 结果不变、区域完成可观察，错误边界被编译拒绝、MOC 真工程的 Branch 产生 Region。

**明确边界**：R03 是可执行 Region 的静态编译基石，不代表 R04 Invocation 数据帧、R05 动态重入循环、R10 Checkpoint 已完成。

**本轮状态**：源码与测试准备中，固定 Commit/Windows CI 需在提交后补证据；不得凭本段文字标记已完成。


## R04 · Invocation身份与MDS帧隔离

- **原Owner**：MRK执行身份位于Mechanism.Runtime；MDSDataStack/MDSDataFrame保持Mechanism.Data唯一数据Owner；复用现有Frame，不增加新引擎/数据容器/项目。
- 新增不可变 \`MRKInvocationContext\` 作为真正执行某个Step时的身份（FlowId、StepId、ActivationIndex、FrameKey、InvocationId），并通过现有 \`MRKExecutionContext.Invocation\` 暴露给专业Capability。根Flow无Invocation，派生BoundInputs/Data/Subflow上下文保持当前身份。
- \`MRKExecutionEngine.RunStepAsync\` 每次调度建立Invocation上下文，并使用FrameKey创建既有MDS作用域/Frame；静态DAG默认FrameKey仍为StepId，确保旧StepOutput/DataExports协议不回归。
- \`MRKStepObservation.InvocationId\` 来源于真实MRK运行节点，不由Studio伪造第二套状态；既有状态、Trace、WorkId沿用原状态真源。
- **测试**：并行运行两个相同编译Plan但不同FlowId的请求，验证独立Invocation、Frame输入、CommittedOutput、观察快照；MDS同一逻辑Step不同激活帧分别提交且互不污染；Abort后的输出禁止读取。
- **边界**：R04建立调用实例身份和数据帧的运行协议，不把当前静态DAG声称为已经动态重入；MRK在单Flow内部为同一Step生成二次激活并正确重定位StepOutput属于R05。
- **验收**：以本轮唯一中文Commit所触发的真实Windows Runtime/工程组织等CI为准，未验证前不记S3。


## R04 最终 Windows CI 事实（已完成）

- 唯一提交 \`ca37a212200512dd93204dd14800d448bc31ab82\`，MRK Invocation 与 MDSFrame 隔离。
- 当前提交固定 SHA 的五项 CI 均已 success：Runtime \`39358389277\`、工程组织 \`39358389234\`、TestCenter \`39358389230\`、Config L1 \`39358389253\`、配置公共底座 \`39358389231\`。
- R04 判定 S3，R05 不重建既有 Invocation/Frame。

## R05 · 动态多节点 Loop 与执行安全（准备验收）

**本轮目标**：For/While/DoWhile/ForEach 由同一 MOM/MOC 工程逻辑编译，真实 Body Plan 编译一次，运行时依次激活多轮；MRK 仍使用同一 F19 Worker，不生成第二个 Graph、线程调度器或数据堆。

- 原 \`MRKExecutionPlanCompiler\` 支持已编译的 \`MRKLoopRegion\`（Body/MaxIterations/条件/ForEach输入）；当动作没有循环时，旧DAG和R01～R04入口不变。
- MOC 在原 \`MOLControlKind.Loop/ForEach\` 分支将索引、Item、多个Body节点与Join编译为一个嵌套 MRK Plan，外层只增加一个动态区域调用Step。不得将 MaxIterations 展开成数十万个Plan.Step。
- MRK 接收带迭代前缀的真实 \`MRKExecutionContext\`；MDS保持唯一Frame Owner，\`Invocation.FrameKey\`、Step结果、NodeState和Trace使用每轮限定路径，StepOutput只能读取正确的帧。
- Break/Continue/Return 由原 MOL.Exit 编译成受控执行信号，在节点安全点跳过未执行的依赖后续节点，先等待在途 Worker 结算并释放资源，再退出或开始下一轮；Return 同时阻止主Flow后续Step。
- 保持 MOC SourceMap 只有一份原节点定义，由运行时限定路径映射原Block ID；准确 Iteration 位置与历史回放的高级策略继续在R09收敛。
- 正例：巨大 MaxIterations 但Plan.Step总数常量、多个Body节点多轮真实执行、For/While/DoWhile/Foreach、Break/Continue/Return、并发不同 Flow 不串结果；反例：错配条件、无效集合、取消、作用域、历史数据滥用。
- 不合并已失败的CI：只有 R05 单次中文 Commit 对应 Windows Runtime/工程组织/TestCenter/Config等检查全部通过，才算S3。未通过禁止进入R06。

## R06 · 多级Content与公开输入输出（本轮R05门禁通过后实施）

- 使用真实MEM持久Content、MOL LogicAspect、MOC编译与SourceMap、现有MRK/MDS；多个调用点、三层子Content、输入/输出端口和实例路径必须独立。
- 禁止将子Content私有Step结果冒充公开Export；保存→关闭→重开→编译→执行→Trace完整闭环；不同调用者及并发Flow的数据不得混淆。
- R06必须在 R05 固定SHA的Windows CI全部通过后独立完成一次中文Commit和真实验收，不与R05合并到一个提交。

### R05修复轮：旧1024次静态展开上限清除与边界反证

- 首次提交 `9b5e4e28f32bd5505ac02cffa90279a85c60ee09` 由 Windows 全解决方案编译通过，但 ArchitectureSmoke 的 `R05DynamicLoopAndExit` 在 `MOLLoopDefinition` 构造阶段触发 `maxIterations>1024` 的旧上限，不能认定动态区域已完成。
- 经用户明确批准，本轮允许为 CI 缺陷增加修复 Commit。现取消旧1024上限，保留 `maxIterations>=1` 正数约束；动态循环迭代由 MRK 运行时执行与取消令牌管控，循环体不按上限静态复制。
- 同时新增零次边界反例。后续必须核验固定修复SHA的 Windows Runtime、工程组织、Config/TestCenter CI，不能用首次提交的结果代替。

### R05修复：区域入口祖先数据校验（Windows失败反证）

- 修复前真实 CI 揭示 `moc.loop_body_compile:INVALID_GRAPH:region.external_dependency`；根因是内部节点读取迭代索引时，其来源虽在Region的Entry之前，却被R03不分情况视为非法外部边。
- 继续保留唯一MRKPlan依赖校验：Region内部成员只可额外依赖Entry在DAG上的传递祖先（已先于Entry完成），不是任意侧入节点；缺失汇合、旁路控制、未知节点、交叉区域仍拒绝。
- 新增允许上游共享数据与拒绝独立旁路的正反例；严禁通过关闭Region验证掩盖问题。
- 此修复需以新SHA的真实Windows CI进一步验证动态循环的全链路。

### R05修复：每轮限定节点状态与分支跳过传播

- Windows 实测：循环已真实执行但 Break/Continue 第一轮即中止（`state=Completed, visit=1, after=1`），并非迭代预算错误。
- 根因：循环的 Step State 使用 `loop.iN.step` 限定身份储存，`RunStepAsync` 却按不带前缀的依赖ID读取，导致分支Gate被跳过后其子动作依旧执行（包括不该执行的 Break）。
- MRK 增加现有执行上下文上的 `TryGetScopedState`，RunStepAsync 的跳过依赖判断与 MOC BranchJoin 条件都复用这一入口；不改全局快照、不创建第二StateStore。R05 原有Break/Continue/Return测试继续作为反证。

## R05最终验收 · 动态循环与结构化退出

- 初始提交 `9b5e4e28f32bd5505ac02cffa90279a85c60ee09` 及用户授权的必要修复提交；最终通过的提交是 `07af5d1dd1d8ae40ce29a1c6abd606f58822bd1b`。
- 五项真实 Windows CI：Runtime `39364338476`、工程组织 `39364338474`、Config L1 `39364338454`、TestCenter `39364338451`、配置公共底座 `39364338430` 均 success。
- 错误反证及修复：旧1024静态预算、Region祖先数据边、动态Invocation跳过分支状态三项均有明确源码修复，未移除坏例；R05=S3，现场生产级恢复不归此轮。

## R06：可复用Content自然调用与多实例隔离

- **唯一真源**：MEM Content + MOLWiringDefinition FlowInputs/FlowOutputs。MOCFlowBuilder新增 `InvokeContent(MEMContent)` 便捷入口，从实际工程快照推导类型化的公开输入输出端口，禁止手写 `moc.invoke:Guid` 及复制调用端口Schema；真实控制/Data连线和ContentRef仍由现有MOC/MEM负责。
- **两类用户**：软件开发者使用 `MOCFlowBuilder.Edit(session, parent, flow=>{ Guid id=flow.InvokeContent(child); ... });`，工程编排用户在Studio选择Content后用同一Logic Aspect持久化调用，不另造Workflow DSL或图形运行器。
- **真实测试**：一份子Content被同一主Content两次调用，不同输入独立Double后在主Flow汇总；工程保存重开后通过MOCBuildService/MRK执行并检查公开结果和跨调用SourceMap、Trace；改变两路业务输入后重跑不可读到旧输出；断开必要输入必须编译拒绝；空白Content直接被InvokeContent拒绝且无半成品事务。
- 已有 `V2ThreeLevelNamedData`、`G04ParameterSubflowIsolation` 等三层/独立参数场景持续回归，本轮不声称实现未定义的递归Content循环调用，也不取消Depth与安全权限校验。
- **验收门槛**：本轮中文一次提交，等固定 SHA Windows Runtime/工程组织/TestCenter CI 全部通过后才能记R06=S3。

### R06 Windows修复及同SHA再验收

- 首次R06提交 `b851ed2417639f5390d09e4cc1aeb2d4a50a73e2` 在Windows Release/x64出现ArchitectureSmoke测试的局部变量与lambda参数重名 `CS0136`，属于测试代码命名缺陷；业务接口 `MOCFlowBuilder.InvokeContent` 未改变。
- 修复提交 `7ae5044fd02a884f83ff8ede207c529306849726` 后，Runtime Windows TDD `39365158186`、工程组织 `39365158564`、Config L1 `39365158422`、配置公共底座 `39365158238` 全部成功。
- TestCenter工作流基于路径触发，仅监听Mechanism/Foundation/Tests.TestCenter等；上述仅修改ArchitectureSmoke的修复没有触发TestCenter。此次补充原MOC源码注释与验收文档，目的是让最终同一源版本重新触发TestCenter及其相关Windows门禁；全部完成前不宣称R06 S3最终验收闭环。

### R06 最终增强验收：自然三层调用与同Plan并发隔离

- 将既有三层 Root→Mid→Leaf 的两个调用改为 `MOCFlowBuilder.InvokeContent(content)`，明确验证公开端口Schema由真实Content生成，不再要求开发者手写 `moc.invoke:Guid` 与输入输出端口。
- 在 R06 双调用Content测试中，Seed 能力从运行请求 `ctx.Inputs` 读取每个Flow的独立数据，不依赖闭包全局变量；对同一已编译Plan同时提交两套输入，验证 20/34 两个具名结果、不同Flow身份、唯一对外Export、SourceMap/Trace的原始Content定位。
- 保持已有 Save/Close/Open、必填输入断开→构建拒绝、私有子输出不公开、三层 SourceMap CallPath、空白Content不可调用等既有反证。
- 验收条件：本增强提交的 Windows Runtime TDD、Engineering Organization、TestCenter及 Config 系列真实CI全部成功，才登记R06 S3；如有失败不得从测试中删除场景。
