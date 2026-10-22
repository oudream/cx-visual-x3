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


## R07 · Join / 异步关联与尾部结算（本轮增量，验收中）

- 基线：从固定SHA `002ca9428b85cb841189f7615d937372f11c0f44` 开始，仅修改既有 MRK / F19 路径与 ArchitectureSmoke；没有新增执行引擎、Worker、Graph、工程项目或第二数据真源。
- 已有行为：`MRKWorkJoin.FirstCompletedAsync` 是 Any（首个完成可失败）；`FirstSuccessfulAsync` 是 FirstSuccess（只认首个成功，全失败不伪造胜者）；`AllSettled` 必须等所有分支的原始 FCR 结果结算，提前成功不等于可释放底层资源。
- 本轮增量：原类补充 `MRKWorkJoin.AllAsync`，按分支提交顺序等待全部结算，返回原始成功/失败/取消结果供调用者判断；不得将任一失败变成All成功，也不得隐式取消或重放外部操作。
- 本轮修复：`MRKCorrelationTicket` 的超时和Dispose只能结束当前Ticket实例；同ID前一个Ticket已终结并重新注册后，旧Ticket晚到的Dispose不能误移除新Ticket。实际外部请求必须使用新的唯一RequestId，否则网络层同ID迟到应答无法辨认，不能因此宣称提供跨重启应答去重。
- 自动反证：All正常/失败/取消有序结算、重复分支拒绝、FirstSuccess全失败没有虚假胜者、旧Ticket与新Ticket的注销竞态；继续运行现有Any、FirstSuccess余支取消、Unknown操作、租约、显式补偿等旧测试。
- **验收状态**：本增量需要固定Commit SHA的Windows Release/x64 Runtime、TestCenter、工程组织和配置CI实际结果；在全部核验之前本段为S1源码增量，不能将R07判定完成。R07尚需更多条件汇合、异步取消竞态、真实Studio路径、多级失败追踪和补偿边界的整体闭环反证，未通过不得进入R08。

### R07 连续增强：条件汇合与安全尾部（待本增强SHA CI）

- 原`MRKWorkJoin`增加`FirstMatchingAsync`：仅在用户明确的谓词满足时提前汇合；未命中全部结算后返回空Winner；谓词抛异常时对其余可取消分支发送协作取消并等待全部结算，绝不把异常变成命中。
- `AllAsync`新增显式`requestCancelRemainingOnFailure`：默认false继续全部等待；选择true后，任一失败/取消/拒绝才请求其他分支协作取消，但仍等待全部Completion，不强行断开真实外部动作。
- 新增反证：条件命中/未命中、取消尾部分支、非协作任务仍占资源直至真实结束、64轮异步应答与取消竞争、并行子任务失败时补偿不可提前发生且确认结算后可显式执行一次。
- 继续保留之前All/Any/FirstSuccess、Unknown外部动作与显式补偿原有测试；高级Try/Catch/Finally结构化异常控制、Studio同图调试、持久恢复及真实设备联锁不能因本次headless测试通过而冒充已验收。是否达到R07整轮门槛还需按`DOCS/Architecture/CxVisua_ 运行体系#流程执行体系 V2.1.md`各项反证清算。

### R07 Windows固定SHA与三级Content失败反证（2026-10-20）

- Commit `ba19232ea78c329d856860d4ce98e460fa9ef4f9` 的Windows CI五项均成功：Runtime 39399995301；工程组织 39399995286；TestCenter 39399995217；Config L1 39399995208；配置公共底座 39399995182。
- Commit `a859f5a89ab85b2435afb166671a7f7962d73abb` 的Windows CI五项均成功：Runtime 39400696836；工程组织 39400696785；TestCenter 39400696731；Config L1 39400696671；配置公共底座 39400696692。
- 本增量沿用R06真实Root→Mid→Leaf工程保存重开及同Plan复用案例，注入Leaf原始业务异常，要求Root失败、未提交Export不公开，Trace保持原Leaf与两级CallPath；另修复FCR Completion与Worker活动槽释放的测试时序假设。
- 增量当前仍待本提交固定SHA的Windows CI，R07尚未完成全部E08原子项和Studio安全交互，不提前进入R08。

### R07 三级失败反证CI及条件谓词异常反证（2026-10-20）

- 固定提交 `49c6d7d254870c3bc96eea906bb07bd6c8a57cbb` 已取得相同SHA的五项Windows CI success：Runtime `39401722909`、工程组织 `39401722742`、TestCenter `39401722699`、Config L1 `39401722773`、配置公共底座 `39401722757`。这证实R06三层真实工程在Leaf注入业务异常后的MRK失败传播和MOC SourceMap/Trace仍保留调用层级。
- 本次新增`FirstMatchingAsync`谓词本身失败时抛出错误、不伪造任何结果的回归测试，仍须等本次修订SHA的Windows验证。
- **R07阶段性状态：S3增量通过，不等于整轮完成**。E08-05/07/08/10以及Studio完整交互、正式权限/硬件安全、各种不可控外部动作仍存在验收缺口；此时仍不得进入R08，更不能声称R08～R12完成。

### R07 最终同一SHA TestCenter门禁补充（持续验收）

- 追加修订 `a886b859a9ac05c33bb4642291311fd2fab1f602` 的四项实际Windows工作流均success：Runtime `39402361803`、工程组织 `39402362086`、Config L1 `39402362032`、配置公共底座 `39402361826`；由于该SHA只变动ArchitectureSmoke与文档，原TestCenter路径过滤没有触发。
- 本提交同步补原 `MRKWorkJoin.FirstMatchingAsync` 条件谓词异常处理的公开安全说明，以触发TestCenter，测试仍保留完整R07正反例（含谓词异常），不删除任何失败场景。
- 只有这个新提交的固定SHA对应Runtime/工程组织/TestCenter和配置检查实际绿色才登记最终增量S3；**整轮R07仍留有Studio工业级交互、多生产者/Invocation资源交接、物理外部设备安全证据缺口，不能凭CI绿色标记R07整轮完成或直接开R08。**

### R07 同一SHA运行/工程/TestCenter触发补充

- 固定SHA `a886b859a9ac05c33bb4642291311fd2fab1f602` 的Windows Runtime `39402361803`、工程组织 `39402362086`、Config L1 `39402362032`、配置公共底座 `39402361826`均success；该提交未修改TestCenter监听路径，因此没有同SHA的TestCenter运行记录。
- 前一提交 `7b5274f8e5e066d7aec7aadc4b1d04f854c5dd74` 对原MRK条件汇合公开契约补充条件谓词错误与安全尾部的说明，相关Windows门禁需独立看固定SHA，不能借其他SHA替代。
- 本次加强反证：谓词异常发生时，即使还有不协作取消的尾部任务，`FirstMatchingAsync`必须等该任务真实结算再传播错误；并确认已请求取消，绝不把本地退出当设备安全停机。此次`Tests/ArchitectureSmoke`修改能同时触发工程组织Windows回归；与原MRK修改一道保证最终SHA五项门禁均触发。

### R07 请求超时重登记回归与工作流路径闭环

- 之前同ID旧票据在应答完成后Dispose的反证已实现；本次补充更危险的**旧请求自然超时→新票据重登记→旧票据Dispose**反证，要求旧回调不得注销新订阅，确认后PendingCount=0。网络层复用同RequestId并不安全，必须给每次外部请求唯一ID，MRK不能伪造跨代网络应答识别。
- 同提交修改原MRK协调器的公开注释与ArchitectureSmoke运行反证，因此同时触发TestCenter（监听Mechanism）与工程组织（监听ArchitectureSmoke）及Runtime/Config对应门禁。**本最终增量不以触发成功代替实际通过**；需固定提交SHA所有门禁success才算这部分S3通过。R07阶段性未闭合风险仍保留，R08尚未开始。

### R07 Windows CI 竞态反证修复（同轮失败例外，2026-10-20）

- 固定提交`4d033d216d62ebb7b37fb79bb34e587441e65f23`的Windows Runtime `39403015208` **失败**，编译成功、运行到`R07_ConditionalJoinAndExplicitFailureCancel`，报`条件异常未传播或未向仍在运行的外部任务发出协作取消`。失败用例保留。
- 根因：测试在观察输入工作Completion之后立刻放开非协作尾部，未同步等待谓词执行或Token取消通知；Windows线程时序可能让尾部先于取消结算，从而`FCRWorkResult.CancellationRequested=false`。此失败不能证明运行时未实现取消。
- 修复：测试中增加`predicateEvaluated`和`predicateTailCancelled`两个RunContinuationsAsynchronously信号，只有两信号均到达才释放真实尾部工作；继续断言原异常传递、取消通知、在途资源不能提前结算、Worker排空。本次同时修改MRK原类的安全注释与ArchitectureSmoke反证，确保TestCenter与工程CI同一SHA触发，**不能把失败提交改写成成功**。


### R07 持续实施：Join等待观察与真实执行结算分离（2026-10-20）

- 复核最新master `59f538a1c7e33f1e4fb40a21bace3dff65844564`，五条Windows CI全部success（Runtime 39403347486、TestCenter 39403347453、工程组织39403347434、Config L1 39403347549、配置公共底座39403347439）。上次R07处于局部S3，未宣布完整结束。
- 沿用现有 `MRKWorkJoin` / `FCRWorkHandle` / F19，不引入第二引擎：新增只读 `MRKJoinWaitObservation<T>` 和 `MRKWorkJoin.ObserveAllAsync`。外部超时/取消仅结束观察者等待，`Results==null`代表未结算；只有`AllSettled`完成之后才允许回收有关资源。可选的`requestCancelRemainingOnAbort`仅发送协作取消，未确认硬件安全停机。
- 正反例：非协作工作、零等待超时、不主动取消、显式Token取消但尾部继续、之后真正结算且成功态与取消请求共存、已结算优先、非法超时拒绝、Worker排空无活动槽；使用源项目ArchitectureSmoke，保持全部既有失败用例。
- **当前状态：新增源码待对应提交SHA的Windows x64 CI确认。** 这只补足E08-06/09的观察者与执行分支生命周期边界；E08-05/07/08/10及Studio完整可视路径仍未达整轮闭环，不能宣称R07已全部完成或自动进入R08。


### R07 Join观察首次CI失败与修复（2026-10-20）

- 首次提交 `14eb2201db6576f3dbe5ff7828fd7710b1903ac5`：Windows Release/x64已编译成功，但Runtime `39408745128`、工程组织 `39408745134`、Config L1 `39408745193`均在ArchitectureSmoke反例 `非法Join等待时间未拒绝` 失败；失败被保留，不能据此判定该SHA通过。
- 根因：`ObserveAllAsync`为`async Task` API，非法参数异常被存入 Faulted Task 而非在取得Task时同步抛出；原测试仅调用但未等待。修复测试使用已有`Await`来观察`ArgumentOutOfRangeException`，且原MRK注释明确异步验证契约。无须删除反例或伪造通过。
- **本修复后继续按固定新SHA的Runtime/Engineering/TestCenter/Config实际结果验收**；未绿灯之前R07仍是局部S3，R08不启动。


### R07 持续实施：多生产者输出显式聚合与MDS提交边界（2026-10-20）

- 既有MDSDataStack对单一Invocation保留已提交输出，节点Pending由MDSDataContext统一提交，未发现可直接复用的跨FCR分支同名输出聚合API。因此仅在现有MRKWorkJoin增加`MergeNamedOutputsAsync<T>`，不新增Stack/Graph，也不替代MDS存储。
- 所有FCR分支真正结算且成功之后，按调用方分支输入顺序合并具名输出；同名键默认抛错，只有显式`mergeConflicts`才允许聚合。任一分支失败或某分支返回null，则不执行聚合、不发布任何部分结果；结果字典只读，实际值的深拷贝/安全校验仍归MDS Data.Return/CommitOutputs。
- ArchitectureSmoke增加正反例：第二分支未结算时拒绝提前聚合、显式Count=2+3且其他名字不丢失、无聚合策略禁止同名覆盖、失败分支禁止局部发布、MDS Commit前不可读/Commit后可读；不启用任何隐式设备重试。
- **这一增量须对应新SHA的五项Windows CI通过后才能登记完成。** 它只证明具名软件结果聚合的受控边界，不等于直接支持任意Studio多生产者Port物理绑定与跨Flow分布式原子提交；R07整体仍需剩余E08反证。


### R07 持续实施：Skipped与失败区分、旧Flow迟到结果隔离（2026-10-20）

- 实际源代码继续使用唯一MRKExecutionEngine和MDSDataStack；添加ArchitectureSmoke真实执行测试而不是模拟Join图。普通Skipped不执行Body；显式AllowSkippedDependencies允许跳过的控制依赖参与汇合，但真正Failed依赖必须失败传播，不能误成功。验证E08-05。
- 同一已编译MRKPlan同时启动旧/新Flow、重复同名Step：旧Flow取消后仍有不合作Provider执行，新Flow可先提交202；旧Provider迟到Return(101)后旧Flow只能Cancelled且无CommittedOutputs，两个Invocation身份不同，不能污染新Flow。验证E08-07的进程内执行与数据隔离，不冒充物理设备已停止。
- 该轮先前 `5dbaa58da86da3845661d1cbc333c42ea93d4aa3` 的Windows五项已全部success（Runtime 39409470439、TestCenter 39409470103、工程组织39409470272、Config L1 39409470408、配置公共底座39409470060）。随后有独立的Studio/AvalonDock图片及PoC并行提交，已确认未改动本次MRK/Test源或主文档，故在最新master继续快进提交。
- 此增量尚需新SHA的Windows五项验收；Studio、跨进程/真实硬件未知状态与完整并行ForEach仍未全面验证，不能将R07整轮判定完成，也不进入R08。


### 并行PoC导致Windows结构检查误计数——R07同轮CI修复例外（2026-10-20）

- 提交`8936665a2b4ffb6d9070828b46ae21040244f3b6`的TestCenter `39410272485`在`Verify project structure`失败：`Expected 31 projects, got 32`，尚未执行R07新业务反证。原因是并行新建的`Hello/AvalonDock/src/CxVisual.AvalonDock.PoC`被验证脚本全仓库递归枚举；该独立PoC不在正式Owner六方面与原31个产品项目中。
- 最小修复：只修改`Build/Verify-Structure.ps1`，项目发现限于既有`Foundation/Mechanism/Professional/Applications/Intelligence/Tests`六个正式顶层Owner，后续依旧严格验证31个数量、目录深度、NET48、程序集名、所有源码与ProjectReference和正式sln；不改PoC/历史或降低产品侧验证门槛。
- 补强旧/新Flow迟到输出反证的FlowId一致性；以新Commit SHA的Runtime、Engineering、TestCenter、Config结果为准，未过不得宣布本次增量完成。


### R07 本轮已核验固定提交（2026-10-20）

- **最终源码及修复提交**：`16482d6810cc093ff480a82691c67b1ace888441`，是包含全部R07本轮代码、正反例、Build结构验证范围修复的固定SHA。
- 已再次查询GitHub Actions，五项同SHA Windows CI `completed/success`：Runtime TDD [39410511650](https://github.com/oudream/cx-visual-x3/actions/runs/39410511650)；Engineering Organization [39410511695](https://github.com/oudream/cx-visual-x3/actions/runs/39410511695)；TestCenter [39410511642](https://github.com/oudream/cx-visual-x3/actions/runs/39410511642)；Config L1 [39410511637](https://github.com/oudream/cx-visual-x3/actions/runs/39410511637)；配置公共底座 [39410511673](https://github.com/oudream/cx-visual-x3/actions/runs/39410511673)。
- 本轮经Windows证实：Join等待观察超时/取消不释放非协作Work、具名输出显式同名聚合且通过MDS提交、Skipped/Failed依赖区分、同Plan旧Flow迟到Return不会污染新Flow、PoC不误计正式项目数量。上述属于**已通过源码增量**，不能等同R07全部需求已完成。
- 尚缺或待扩展：Studio工程编排用户对高级Join/异常/补偿的可见可编辑操作、跨Invocation与Parallel Foreach的归并完整身份、多生产者真实专业连接、跨进程外部设备Unknown与硬件安全交接及整体长稳反证。**R07整轮状态：持续实施，未封轮；R08～R12仍待推进**。此次仅追加文档证据，不修改最后已经CI验证过的源码。


### R07 · E08-10 分层循环真实Invocation输出归并（2026-10-20，待CI）

- 基线HEAD `28c011b53a616e75ff527df89988367e25901f6e`；继续复用唯一MRKExecutionEngine、MRKExecutionContext、MDSDataStack。已有R05动态循环每轮独立Frame，但后继步骤没有一个无需拼接字符串且保证完整性的一站式读取入口。
- 在现有`MRKExecutionContext`增补`CollectLoopOutputs<T>(loopStepId, bodyStepId, outputName)`和只读身份值`MRKLoopOutput<T>`。从Loop执行结果取得真实完成迭代数，按输入迭代次序读取原MDS已提交数据；每项包含FlowId、IterationIndex、FrameKey、InvocationId。必须检查Loop已Completed及各迭代节点Completed；任一缺失/Skipped/Failed均拒绝整个聚合，不伪造部分成功；实际导出仍使用原MDS的Return/CommitOutputs屏障。
- 实际执行反证：同一Plan并发两个Flow不同输入、同名循环/同名输出不串账；跳过中间迭代拒绝局部提交；两层嵌套Loop先内层归并再外层归并且可精确定位原FrameKey，验证原有Trace/Invocation身份而不创建第二运行数据仓库。
- 本轮不更改R05循环本身串行迭代语义，也**不声称已具备并行Foreach调度**、Studio画布编辑、持久恢复、跨进程联锁或外部设备安全。仅为E08-10补完整的运行态归并基础，待本次提交的真实Windows五项CI核验；R07未封轮，R08未启动。


### R07 E08-10 有界并行 Foreach 增构与反证（2026-10-20，待固定SHA CI）

- 上一提交`bc684e89f632100bb47872c6b86e3237899409cc`的Windows五项CI均成功：Runtime 39443570540、工程组织39443570457、TestCenter39443570484、Config L1 39443570604、配置公共底座39443570603。
- 沿用唯一MRK引擎和F19 Worker，`MRKLoopRegion`新增`maxParallelIterations=1`（默认完全兼容原R05串行）；显式大于1仅支持声明了IList集合、无前轮依赖谓词的Foreach，限额调度在途迭代，仍用原MDS Frame与原Flow身份。
- 任一迭代失败仅请求其余分支协作取消并等待真实在途任务结算；不合作Provider可能导致长期等待，这不是硬件停止确认。并行Break/Continue/Return运行时拒绝，避免未定义的跨迭代流程控制。所有迭代成功后仍按原MDS/DataExports统一发布。
- ArchitectureSmoke验证两迭代真实重叠、峰值上限2、4轮原Invocation有序输出、最终Sum=10；以及未声明集合/跨轮谓词非法，另一迭代失败而非合作尾部仍占资源，完成后Flow失败且无导出，并行Break被拒绝。**本提交待Windows x64真实CI，R07未封轮；设备硬件与Studio完整交互仍需独立验收，R08尚未开始。**


### R07 有界并行Foreach：固定SHA全项Windows验收（2026-10-20）

- **固定源码SHA：** `cdde9ff13d283117d06ecccc566e355529ddc273`。GitHub Actions同SHA的五项工作流均已完成且`success`：Runtime TDD [39445017744](https://github.com/oudream/cx-visual-x3/actions/runs/39445017744)、工程组织 [39445017789](https://github.com/oudream/cx-visual-x3/actions/runs/39445017789)、TestCenter [39445018101](https://github.com/oudream/cx-visual-x3/actions/runs/39445018101)、Config L1 [39445017995](https://github.com/oudream/cx-visual-x3/actions/runs/39445017995)、配置公共底座 [39445017976](https://github.com/oudream/cx-visual-x3/actions/runs/39445017976)。本条提交只追加CI证据，不改变已验证源码。
- 本轮实际完成：E08-10进程内串行/有界并行Foreach的迭代数据身份、两级嵌套归并、按迭代序输出、同Plan不同Flow隔离；`maxParallelIterations`显式限额且默认串行兼容；失败协作取消并等待非合作尾部真正结算，拒绝并行Break/Continue/Return。
- **尚未验收，不得冒充已完成：** Studio自由编排画布完整入口与原图Join/调试，Try/Catch/Finally高级结构化异常，真实专业Provider跨进程安全/联锁与重启恢复、正式用户授权和规模化性能。R07仍为S3局部功能验收状态，**整轮未闭合，R08尚未启动**。


### R07 Studio真实Join编辑与工程一致性（2026-10-21，本轮待Windows验收）

- 复核新Studio已有V2 Content自由连线、MOC能力目录、真实MRK仿真及SourceMap Trace，避免再建Flow Graph。增加Studio实际按钮与“节点”上下文Ribbon中的插入Join命令；选中至少两个具有Done控制输出的真实分支，在一次MOCFlowBuilder.Edit/MEM工程事务中创建Judge Join节点并连到其Start（Many）控制端口。
- 反例：未选两个不同来源、来源无Done、能力不可用，均拒绝且不会产生孤儿Join或提升工程Revision。真实Windows WPF STA StudioShellSelfTests验证按钮、原工程Aspect里双控制边、MEM事务、保存重开身份一致、重复来源拒绝不污染工程。
- 此Join是现有V2 DAG多依赖汇合，**不是**将FCR任意外部操作的Any/FirstSuccess中途胜者语义伪装成V2内建控制。Any/FirstSuccess软件API此前已验证；高级结构化异常和物理设备Unknown仍须另行验收。此提交需要核对Windows Studio门禁和Runtime/TestCenter并与同一SHA匹配，未过不得封轮或进入R08。


### R07 · Studio Join 首轮CI缺陷与原验收保留（2026-10-21）

- 固定提交 `34bd8b7a76a5ffccfa41fbc435c22cd4adffb86b` 的 Studio Shell `39513096869` 真实红灯为既有 `S1-02` 即时切换上下文菜单断言；Config L1 `39513096835` 的全量Rebuild则因只恢复TestCenter包，未恢复Studio AvalonDock包而发生XAML MC3074。不能借其他SHA成功记录替代这两个红灯。
- Studio更改：OpenStudioDocument以明确用户请求的ContentId直接刷新动态菜单，之后仍由原Dock事件驱动后续变化，保留S1-02不降级；StudioShell验证期望增至9项，新增真实Join/工程保存重开回归。
- CI更改：所有需要未预热状态Rebuild的Windows工作流显式Restore Studio.csproj的AvalonDock 5.0.1依赖，继续严格进行全量Rebuild、Runtime、TestCenter正反例，不禁用XAML。
- 并行开发者已于`9befd165ed450f331f675842e15f2210b1aee6f4`完善Foreach超预算行为，本次仅触及Studio、CI和主文档，不覆盖其源码或测试。当前R07仍待修复SHA真正Windows验收，未封轮，R08暂不启动。


## R07 阶段封轮记录：Join、异步等待、失败与补偿安全边界（2026-10-21）

**裁决：R07在“进程内MRK/F19/MDS软件执行 + MEM/MOC工程定义 + Windows Studio真实编排/CPU仿真”范围内封轮，达到对应S3代码验收与已实现Studio交互路径的S4本机验证。** R07不包含实机安全停止授权、跨进程可靠恢复、生产现场长稳；这些仍是R10/R11/R12专项，不能被本次封轮冒充通过。正式设备写入仍不得绕过专业Provider的授权和联锁。

| R07原子要求 | 已实现且经回归的证据 | 范围 |
|---|---|---|
| E08-01 All | 原MRKWorkJoin.AllAsync，失败/取消仍等待全部原FCR结果结算 | 进程内 |
| E08-02 Any | FirstCompletedAsync返回实际先完成者；AllSettled保留其余分支 | 进程内 |
| E08-03 FirstSuccess | FirstSuccessfulAsync失败不伪造胜者，成功后可显式取消余支 | 进程内 |
| E08-04 ConditionalJoin | FirstMatchingAsync带真实谓词、无匹配、谓词异常及取消尾部反证 | 进程内 |
| E08-05 Skipped/Failed | 真实MRK Step不同终态，允许跳过依赖但不把失败当成功 | 进程内 |
| E08-06 尾部策略 | 显式取消/等待、非协作Provider后置结算、资源不可提前回收 | 进程内 |
| E08-07 LateResult | 同Plan多Flow、Invocation隔离、旧取消结果不得污染新Flow | 进程内 |
| E08-08 多生产者数据 | MergeNamedOutputsAsync缺省拒绝同名覆盖，显式聚合，MDS Commit屏障 | 进程内 |
| E08-09 超时/外部Unknown | ObserveAllAsync区分观察超时与真实执行；Unknown使用现有操作账本及明确安全补偿 | 软件事实，不是物理停机凭证 |
| E08-10 多层归并 | CollectLoopOutputs/真实分层Frame、有界并行Foreach、失败等待尾部排空 | 进程内 |
| 工程编排双角色 | Studio实际“选中分支→插入Join”以MOCFlowBuilder/MEM事务建立多控制输入；Studio STA保存重开、无效输入回滚、原MRK仿真Trace通路 | Windows真实WPF，本机CPU仿真 |

**实际Windows验收：**
- R07并行Foreach/数据身份：固定源码`cdde9ff13d283117d06ecccc566e355529ddc273`的 Runtime [39445017744](https://github.com/oudream/cx-visual-x3/actions/runs/39445017744)、Engineering [39445017789](https://github.com/oudream/cx-visual-x3/actions/runs/39445017789)、TestCenter [39445018101](https://github.com/oudream/cx-visual-x3/actions/runs/39445018101)、Config L1 [39445017995](https://github.com/oudream/cx-visual-x3/actions/runs/39445017995)、配置公共底座 [39445017976](https://github.com/oudream/cx-visual-x3/actions/runs/39445017976)，五项success。
- R07 Studio实际事务与9项STA：固定源码`babd23c4ac26d686cd2cb7d79511992c0cd982a9`对应 Studio Shell [39514050239](https://github.com/oudream/cx-visual-x3/actions/runs/39514050239) success；其同SHA Runtime [39514050194](https://github.com/oudream/cx-visual-x3/actions/runs/39514050194)、Config L1 [39514050176](https://github.com/oudream/cx-visual-x3/actions/runs/39514050176)、配置公共底座 [39514050316](https://github.com/oudream/cx-visual-x3/actions/runs/39514050316)均success。工程组织在该SHA出现历史H06回归超时，因此不把该失败改成绿灯。
- H06的原测试时限/旧页签兼容修复后，`11cbc3ab90efdca70d9ef42d442a5b36a461a434`对应 Engineering Organization [39514735744](https://github.com/oudream/cx-visual-x3/actions/runs/39514735744)、Runtime [39514735933](https://github.com/oudream/cx-visual-x3/actions/runs/39514735933)、Config L1 [39514735773](https://github.com/oudream/cx-visual-x3/actions/runs/39514735773)、配置公共底座 [39514735887](https://github.com/oudream/cx-visual-x3/actions/runs/39514735887)均success。该SHA只改ArchitectureSmoke，因此不要求在该SHA重复触发路径过滤的Studio流水线；Studio源码与已验收版本一致。

**封轮不覆盖且必须保留的真实门槛：** Studio目前明确支持V2 Control的多依赖All汇合、原图跟踪；Any/FirstSuccess在MRK开发者API已验证，不把它们写成完整可编辑的V2 DSL。Try/Catch/Finally复杂结构化控制与全量生产级调试不因本轮封轮视为实现；物理设备停止与授权、跨进程未知执行与恢复、可靠Checkpoint以及24小时长稳分别进入R09/R10/R11/R12。未通过现场联锁测试前禁止自动重放或宣称设备已停止。

**下一轮门槛已满足：** R07范围内已明确的代码与Studio软件验收通过，可进入R08（F19公平、组/资源仲裁与容量/背压）；R08仍必须分别实现与Windows CI验收，不沿用本轮旧绿色。

## R08 实施启动：F19跨Key有界公平调度（2026-10-21）

- **前置验收**：R07进程内软件执行及Studio Join事务/CPU仿真范围已封轮；设备安全及生产级持久恢复各有R10～R12独立准入门槛，不得被R07/R08的普通Worker测试替代。
- **本次R08增量**：仍在`Foundation.Infrastructure/Concurrency/FCRWorker`原有`PumpLocked`内调度，不新增队列或执行器；`FCRWorkerOptions`加可选`priorityFairnessBurst=16`，旧构造函数兼容。不同Key优先选高Priority，但连续跳过较早已就绪工作达到设定上限，至少给最老可执行Key一次机会；相同Key始终优先最早提交者，既有ActiveKey、QueueCapacity、PendingBytes、Cancel与Stop逻辑保持。
- **可验证反证**：显式Burst=3时，实际高优先级连续三个先执行，之后低优先级被安排；持续高优先级仍按Key FIFO，且队列字节预算、容量、Stop排空完全归还。非法Burst=0拒绝；原队列溢出、KeyedSerial及200任务压力测试不删除。
- **验收边界**：此为R08的第一项（调度公平性）真实源码及回归，需新SHA Windows FoundationSmoke/Runtime/TestCenter/Config验证后才记本增量完成。Stage/WholeFlow组仲裁、多资源取消/背压的复杂竞态及长稳性能仍在R08剩余任务，不能据此把R08整轮标为完成。


### R08组等待容量取消回归（2026-10-21，待CI）

- 前一提交 `b39a708109164cbe49afdf81b19d4fa5819ea9de` 对应Windows Runtime [39515316490](https://github.com/oudream/cx-visual-x3/actions/runs/39515316490)、TestCenter [39515316520](https://github.com/oudream/cx-visual-x3/actions/runs/39515316520)、Config L1 [39515316500](https://github.com/oudream/cx-visual-x3/actions/runs/39515316500)、配置公共底座 [39515316510](https://github.com/oudream/cx-visual-x3/actions/runs/39515316510)，全部success。
- 本次保留原MRKExecutionGroups实现不增新调度器，ArchitectureSmoke增加64轮取消等待者、Waiting释放、Active持有者不受干扰、拒绝配额统计、另一组独立运行及重新准入的执行反证。待本提交固定SHA的Windows实际回归；R08仍有Stage/WholeFlow、资源交接与长稳压测待完成。


### R08执行组容量反证：固定Windows证据（2026-10-21）

- 固定源码SHA：`79470098f18d90a87d5eada9a01ea3018b4553e0`。Windows Release/x64同SHA四项工作流均`completed/success`：Runtime [39515895209](https://github.com/oudream/cx-visual-x3/actions/runs/39515895209)、工程组织 [39515895189](https://github.com/oudream/cx-visual-x3/actions/runs/39515895189)、Config L1 [39515895258](https://github.com/oudream/cx-visual-x3/actions/runs/39515895258)、配置公共底座 [39515895187](https://github.com/oudream/cx-visual-x3/actions/runs/39515895187)。本SHA仅修改Tests/ArchitectureSmoke和此文档，TestCenter工作流的路径过滤未触发，不能冒称该SHA独立运行了TestCenter；前次Foundation源改动SHA `b39a708109164cbe49afdf81b19d4fa5819ea9de` 的TestCenter实际成功。
- 经证实：原执行组等待配额满足限制，64轮等待Token取消后不占用Waiting，已经占用Active的Holder不被抢占，独立cpu组正常，拒绝统计64次，最终许可归还可重新接纳。
- **R08状态仍为进行中**：已完成并验证F19跨Key有界优先级公平性，以及MRK组等待取消配额归还；尚需WholeFlow/Stage组调度语义、真实多资源公平仲裁、背压冲突/压力与长稳数据。不得将普通Windows软件测试当作实机或跨进程独占证据。

### R08 WholeFlow组作用域：跨Flow共享原许可证（2026-10-21，待固定SHA Windows验收）

- 根因：原MRKExecutionGroups仅按每个Step获取/释放许可；即使多个Step属于同组，也会在节点交界释放席位，无法表达“完整流水线/设备会话期间独占执行组”。
- 在原MRKExecutionGroupDefinition增加可选Scope，默认Step完全兼容；WholeFlow由原MRKExecutionEngine在任何节点启动之前按GroupId确定顺序取得席位，并保留到全部节点、动态Loop、DataScope与Journal终态结算之后；原Step组按原规则获取，不产生第二套Worker/Session或资源管理器。
- 动态Loop内部组在正式准入前递归验证及归集，防止Loop内WholeFlow重复获取导致自死锁；未知嵌套组/执行位置在执行副作用之前拒绝。整条Flow等待仍消耗原有ActiveFlow配额且不占Worker线程。取消排队WholeFlow必须即时归还Waiting，不改变已占有者，所有成功/失败/超时/停止出口必须归还Active。
- 当前仅实现Step和WholeFlow。Stage作用域必须有真实Stage边界/Stage结束信号再独立实现；不得作为Step别名静默接受。跨进程硬件Owner/安全停机不属于软件Group许可证明。

### R08 WholeFlow可执行反证（2026-10-21，待Windows CI）

- ArchitectureSmoke: `R08_WholeFlowSerializesAcrossFlowsAndCancelsQueue` 使用真实MRKFlow，验证完整Flow占组、后继Flow排队、第三Flow超组等待容量在任何业务SideEffect前失败、队列取消即时返还组配额且不抢占第一Flow、第一Flow两个同组Step不发生自死锁、释放后新Flow再次可进入。
- `R08_WholeFlowLoopUsesOneLeaseAndRejectsUnknownInnerGroups` 在单Worker内执行含WholeFlow组的多轮动态Loop，预期循环2次且只在父Flow获取组许可证；嵌套未知组在StartFlow前拒绝、零副作用；非法组Scope拒绝，不将尚未实现的Stage隐式降级。
- 仍属软件单宿主组机制：此批提交待真实Windows x64工程编译、Runtime、TestCenter及工程组织CI；不代表Stage、资源物理联锁或跨进程独占已完成。

### R08 原资源协调器多资源排序与取消原子回滚（2026-10-21，待Windows CI）

- 仍复用`MRKResourceLeaseCoordinator`：新增`AcquireManyAsync`，全部资源Id先验证、排序去重，按相同次序取得原有`SemaphoreSlim`许可；若排队阶段取消/异常，逆序Dispose已拿到的资源租约，不向调用者公开部分成功。
- 原MRK Engine的业务Step和显式补偿共用该多资源申请入口，Worker提交时已经持有完整软件租约，尾部任务真正结算之后才逆序释放。无第二套资源服务/组系统；旧单资源AcquireAsync/TryAcquire仍保留。
- ArchitectureSmoke反证：先已有其他Owner持有B，再按B,A,A申请；验证A先取得并等待B、取消后A归还且B持有者不变；资源A可被新申请者使用；成功申请排序去重；非法资源名在零副作用阶段拒绝。
- 仍未实现物理设备Owner或硬件停止确认、多进程分布式锁、无限等待的自动公平性证明；本增量按真实Windows CI结果单独验收。

### R08 WholeFlow取消后非合作尾部的真实执行边界（2026-10-21）

- 增加`R08_CancelledNonCooperativeWorkRetainsWholeFlowPermit`。一个真实MRK Flow中的Provider忽略CancellationToken，仍异步等待外部终态；用户请求取消后Flow保持在途、组Active=1。此时第二Flow必须处于组Waiting状态，绝不能在前者尾部真正返回前执行。收到迟到成功结果后原Flow应是Cancelled而不是Completed，结算后组许可释放、第二Flow成功进入。
- 该测试验证软件Worker/Flow生命周期和执行组许可的正确结合，不得解读为真实设备已经安全停止。须以本提交Windows x64 Runtime/Engineering测试结果为准。

### R08 Stage组作用域：按阶段生命周期共享许可（2026-10-21，待CI）

- 与WholeFlow不同：`MRKExecutionGroupScope.Stage`按同一Flow内声明相同`ExecutionGroup`的静态DAG节点集合形成一段明确Stage，同组节点共用一次组入场任务，Stage内多个并行节点可同时执行；必须等所有成员（包括Skipped/Failed/Cancelled）真实结算才释放许可。之后其他Flow可进入同Stage，即使上一Flow尚在执行后续无组节点，从而允许不同阶段重叠。
- 作用域仍由`MRKExecutionGroups`管理原有组容量，`MRKExecutionEngine.StageLeaseBatch`只是当前Flow内的引用计数/共享等待，不是第二个Worker或业务调度器。Step/WholeFlow默认语义及现有串行Key/资源取得顺序不变。
- 动态循环内部的Stage需要真实Iteration级Stage身份，否则成员数量无法静态确定，**当前必须在正式StartFlow前拒绝**；不能静默退化为Step。MOL/MOC在编译Plan后使用相同组声明，未实现的Studio Stage参数UI不能冒充交付。
- 执行反证：两节点同Stage后续尾部非合作等待时另一个Flow能完成阶段；两个并行Stage节点共享一组席位且都实际开始；Loop内Stage必须在任何业务副作用前拒绝。均需新提交固定SHA Windows CI确认，尚不等于R08整体长稳/生产安全封轮。

### R08 Stage初始状态Journal失败的资源清算（2026-10-21）

- `RunStepAsync`的Invocation身份和`WaitingDependencies`状态初始化改入既有try/catch/finally，确保即使首个StepTransition的Journal Append抛异常，Stage成员仍可进入计数结算的finally；不静默吞Journal异常。
- ArchitectureSmoke的`R08_StageJournalInitialTransitionFailureReleasesPermit`在同Stage两并行节点中向一个节点的第一条StepTransition注入一次真实Journal失败，要求Flow最终Failed且全部Stage Active/Waiting为0，不得造成永久席位泄漏。测试将在本SHA的Windows Runtime/Engineering/Config门禁核验。

### R08 细化Stage至动态Loop/并行Foreach的逐Invocation生命周期（2026-10-21）

- 原版本保守拒绝所有Loop内Stage。本轮在`ExecuteLoopBodyAsync`为每个真实Iteration建立原`StageLeaseBatch`（只是生命周期计数，不建调度器）；每轮都沿用原MDS Frame和MRK Invocation路径，同阶段多个节点共享一次组许可证。对R05串行Loop及并行Foreach均适用，作用域完成才归还许可证，不会把不同迭代错误合成整条Flow锁。
- 原`ValidateExecutionGroups`先完整检查当前Plan，再递归Loop；若**外层和内层同时存在Stage**，跨组反向持有可能死锁，当前仍在StartFlow前拒绝，不执行任何业务副作用（以后若需要可由专业声明明确的无环嵌套锁顺序）。
- ArchitectureSmoke反证：两轮串行Loop每轮2个Stage Step，四轮并行Foreach两Stage Step/迭代，组MaxConcurrency=1、MaxWaiting=4，全部真实执行并归还组Active/Waiting；外层Stage持有期间进入内层Stage被提前拒绝。该增量须对应本提交固定SHA的Windows Runtime/工程组织与配置CI成功后才记已验证。


### R08核心软件组/资源能力：固定SHA Windows综合验收记录（2026-10-21）

| 提交SHA | 实际完成/修复 | 同SHA Windows CI |
|---|---|---|
| `6b7d341c27edd90e51fe2e26a7aede03cf88d308` | WholeFlow多Step/多Flow配额、嵌套Loop、排队取消、拒绝后无业务副作用 | Runtime [39558808764](https://github.com/oudream/cx-visual-x3/actions/runs/39558808764)、工程组织 [39558808698](https://github.com/oudream/cx-visual-x3/actions/runs/39558808698)、Config L1 [39558808769](https://github.com/oudream/cx-visual-x3/actions/runs/39558808769)、配置公共底座 [39558808754](https://github.com/oudream/cx-visual-x3/actions/runs/39558808754)：全部success |
| `fce93a7d4fe275f4f8af4bb17ca1484839b00584` | 同一资源协调器批量排序去重、取消时回滚部分租约、节点与补偿复用 | Runtime [39559118199](https://github.com/oudream/cx-visual-x3/actions/runs/39559118199)、工程组织 [39559118146](https://github.com/oudream/cx-visual-x3/actions/runs/39559118146)、TestCenter [39559118106](https://github.com/oudream/cx-visual-x3/actions/runs/39559118106)、Config L1 [39559118177](https://github.com/oudream/cx-visual-x3/actions/runs/39559118177)、配置公共底座 [39559118147](https://github.com/oudream/cx-visual-x3/actions/runs/39559118147)：全部success |
| `f93b225c0bd0d3415cba56f667c5f9323819c0f3` | WholeFlow取消后非合作Provider继续运行期间组席位不得提前释放 | Runtime [39559362802](https://github.com/oudream/cx-visual-x3/actions/runs/39559362802)、工程组织 [39559362864](https://github.com/oudream/cx-visual-x3/actions/runs/39559362864)、Config L1 [39559363036](https://github.com/oudream/cx-visual-x3/actions/runs/39559363036)、配置公共底座 [39559362811](https://github.com/oudream/cx-visual-x3/actions/runs/39559362811)：全部success |
| `a09dfc38f30c5913562022ed5903b30e70ea008e` | Stage阶段级共享组许可、跨Flow流水重叠、阶段并行节点共用1份租约 | Runtime [39559996970](https://github.com/oudream/cx-visual-x3/actions/runs/39559996970)、工程组织 [39559996861](https://github.com/oudream/cx-visual-x3/actions/runs/39559996861)、TestCenter [39559996904](https://github.com/oudream/cx-visual-x3/actions/runs/39559996904)、Config L1 [39559996852](https://github.com/oudream/cx-visual-x3/actions/runs/39559996852)、配置公共底座 [39559996929](https://github.com/oudream/cx-visual-x3/actions/runs/39559996929)：全部success |
| `c1f30f650ce5c6766d0e3b297ecd78bfb09359bb` | Journal首条Step状态写入失败的Stage引用计数结算与真实失败传播 | Runtime [39560601149](https://github.com/oudream/cx-visual-x3/actions/runs/39560601149)、工程组织 [39560601169](https://github.com/oudream/cx-visual-x3/actions/runs/39560601169)、TestCenter [39560601153](https://github.com/oudream/cx-visual-x3/actions/runs/39560601153)、Config L1 [39560601146](https://github.com/oudream/cx-visual-x3/actions/runs/39560601146)、配置公共底座 [39560601199](https://github.com/oudream/cx-visual-x3/actions/runs/39560601199)：全部success |
| `7766955e06c23d226ac55fbee5fab6d2e0db0be9` | Stage深入串行Loop和并行Foreach，按真实Invocation逐迭代租约，不可安全嵌套提前拒绝 | Runtime [39561224357](https://github.com/oudream/cx-visual-x3/actions/runs/39561224357)、工程组织 [39561224209](https://github.com/oudream/cx-visual-x3/actions/runs/39561224209)、TestCenter [39561224220](https://github.com/oudream/cx-visual-x3/actions/runs/39561224220)、Config L1 [39561224364](https://github.com/oudream/cx-visual-x3/actions/runs/39561224364)、配置公共底座 [39561224222](https://github.com/oudream/cx-visual-x3/actions/runs/39561224222)：全部success |

**当前R08裁决：软件内核S3核心增量已通过，整轮仍未正式封闭。**
- 现有`MRKExecutionGroupScope.Step/WholeFlow/Stage`、F19跨Key有界优先级公平、MRK进程内执行组配额、等待Token取消、原协调器多资源字典序申请/异常回滚、动态Foreach逐Iteration Stage均已实装并经对应Windows自动回归；不新增工程/Worker/数据所有权。
- **继续验收项**：E06可等待式生产者背压、拒绝/Drop业务可追溯与高峰指标、资源排队公平的持续压力证明、全量软件长稳及真实工业生产参数；MOL/MOC高级Stage属性与Studio的真实工程编辑界面目前不能按软件API验收替代。
- 外部物理设备Owner、硬件联锁与未知状态恢复仍分别交给专业Provider/Host及后续R10～R12；以上Windows绿灯不是生产硬件安全证明。R08下一轮优先推进可取消生产者背压与可观察统计，继续沿用F19/原MRK owner。


### R08 F19可取消生产者异步背压增量（2026-10-21，待Windows验收）

- `FCRWorker.SubmitWhenCapacityAsync<T>` 将生产者等待与已接纳待执行工作严格分离；`maxWaitingProducers`默认64并强制非负，拒绝超预算项/超额生产者，取消等待会终止Admission且不会悄悄加入Worker队列；Stop(false/true)均唤醒未接纳者，原已接纳工作按原语义排空/取消。
- 等待者在同一F19 `PumpLocked` 内FIFO受理；每受理一名即先执行既有Pump再考虑下一名，以避免并发空槽时绕过队列/字节预算。`WaitingProducers`、`AdmittedProducers`、`PeakPendingBytes`对外可观察，不与Worker等待中的Active混同。异步背压不调用DropOldest，现有普通Submit仍支持原有Reject/Drop。
- 运行反例：真实满队列+8字节预算下2名生产者等待，第三名/超字节单项明确Rejected，先行已接纳Droppable不能被异步入口淘汰；取消队列工作后仅第一生产者受理、同Key任务FIFO执行；原排队/字节容量归零且不误计Drop。另验证等待Token取消立即归还名额、非法配置拒绝、Stop(false)唤醒等待者但等已入队工作结束。**以新SHA对应Windows CI实际结果为准，不提前宣布R08封轮。**


### R08异步生产者容量风暴与Stop(true)真实反证（2026-10-21，待Windows CI）

- FoundationSmoke新增8轮重复压力：2个运行中异步工作、3个已排队任务恰好占满30字节、12个异步等待生产者（上限12）、第13名Rejected；取消前6名等待者立即释放Admission、取消原已接纳队列的3个任务后由FIFO前3名生产者准入、取消剩余等待者、释放在途工作并断言Pending/Active/Waiting/Bytes均为0。检查Admitted=3/Rejected=1/Dropped=0，不能将等待生产者混入原队列统计。
- 增加Stop(true)的非合作在途任务案例：取消已接纳Pending、拒绝尚未接纳的生产者，同时仍等待运行中非合作业务真正返回后才能完成Stop。全部仍采用唯一FCR Worker及原有Token/Handle。待固定SHA真实Windows CI验收。


### R08压力回归首轮红灯与真实完成屏障（2026-10-21）

- 固定提交 `7b42a94545e0fe92251e9e35f92f0c331ba99964` 的 Runtime [39576596104](https://github.com/oudream/cx-visual-x3/actions/runs/39576596104) 与工程组织 [39576596130](https://github.com/oudream/cx-visual-x3/actions/runs/39576596130) 失败于新增8轮压力测试的“预算泄漏、误Drop或统计不一致”最终断言；该原断言在只等待业务Handle.Completion后就要求Worker.Active为0，而原Worker将结果写入Handle与`ExecuteAsync`的finally释放Active/Key分为两个先后阶段。
- 保留8轮全部容量、取消、FIFO、拒绝、Drop和Stop(true)业务反证；在最终Active==0快照前增加`Await(worker.StopAsync())`作为正式排空屏障。若修复SHA仍不成功，应继续排查真实计费/回调竞争，不得删减或弱化压力要求。


### R08 统一MRK开发者API复用F19异步生产者背压（2026-10-21，待CI）

- `IMRKExecutionEngine` 与原实现增加 `SubmitWhenCapacityAsync<T>(action, serialKey, cancellation, priority, estimatedBytes)`，仅转发到现有F19 Worker并受原Runtime`_lifecycleGate`停止接单约束；普通`Submit`仍即刻受理/拒绝，没有第二套Executor和状态仓库。等待准入与Worker最终完成分成两段，开发者收到`FCRWorkHandle<T>`后按原`Completion`获取真实结果。
- ArchitectureSmoke执行反证：底层单线程/单队列/4字节预算时，已有运行工作+待执行任务占住槽位，第三个通过MRK开发者异步API等待；Engine.Stop(false)唤醒第三名返回Rejected，同时等待前两项真实完成；停止后再提交明确拒绝，最终Pending与Bytes归零。按新提交SHA真实Windows验收，不以源码声明代替测试。


### R08背压峰值与等待耗时统计（2026-10-21，待CI）

- 原`FCRWorkerSnapshot`新增只读`PeakPending`、`PeakWaitingProducers`、`SettledProducerWaits`、`MaxProducerWaitMilliseconds`，并保留旧Snapshot构造调用的可选尾参数和全部原字段；使用`Stopwatch.GetTimestamp`单调计时，避免系统时钟调整导致负等待时间。
- 统计由同一Worker锁保护：生产者成功准入、取消等待、Stop拒绝均只计算一次等待结算；即时超额拒绝不伪装成已排队耗时。压力反例同时断言峰值Pending=3、等待者高峰=12、全部等待结算=12、原Admitted=3/Rejected=1/Dropped=0，不以业务Completion之前的Active瞬态判泄漏。
- 本轮仅为软件调度可观察性，最终通过标准仍为固定SHA Windows Runtime/工程组织/TestCenter/Studio与Config检查；现场吞吐UPH和24小时长稳另行验收。


### R08生产者异步背压与可观测性：固定SHA Windows验收（2026-10-21）

本项已从F19底层能力贯通到统一MRK开发者API。所有下述“通过”都来自**对应SHA**的实际Windows工作流，不能将历史单项绿灯替代当前全量状态。

| 固定源码SHA | 源码/测试实际进展 | 固定SHA Windows结论 |
|---|---|---|
| `106836f5c0546f9803a64e15349d3b8de6e86bed` | 首版F19有界Waiter与SubmitWhenCapacityAsync | Runtime [39575665045](https://github.com/oudream/cx-visual-x3/actions/runs/39575665045) **失败**：CS1061抽象Job错误访问泛型Handle。保留失败证据。 |
| `c1410705511713c12594fa15871322adec10260c` | 原Job接口增加类型安全MarkAccepted；保留旧提交/取消/溢出语义；已有基础背压正反例 | Runtime [39576040354](https://github.com/oudream/cx-visual-x3/actions/runs/39576040354)、Studio [39576040381](https://github.com/oudream/cx-visual-x3/actions/runs/39576040381)、TestCenter [39576040522](https://github.com/oudream/cx-visual-x3/actions/runs/39576040522)、Config L1 [39576040321](https://github.com/oudream/cx-visual-x3/actions/runs/39576040321)、配置底座 [39576040765](https://github.com/oudream/cx-visual-x3/actions/runs/39576040765)：**5/5 success**。 |
| `7b42a94545e0fe92251e9e35f92f0c331ba99964` | 八轮12等待者+3队列满载压力与Stop(true)非合作反证 | Runtime [39576596104](https://github.com/oudream/cx-visual-x3/actions/runs/39576596104) 和工程组织 [39576596130](https://github.com/oudream/cx-visual-x3/actions/runs/39576596130) **失败**：Handle.Completion完成后、Worker finally排空前过早读取Active快照。没有删除测试。 |
| `cced0bf73de28ee6b53ae96b24630b5f1625da89` | 用原Worker.StopAsync等待实际排空，再验收Pending/Active/Bytes/Waiters及拒绝/Drop | Runtime [39576910724](https://github.com/oudream/cx-visual-x3/actions/runs/39576910724)、工程组织 [39576910476](https://github.com/oudream/cx-visual-x3/actions/runs/39576910476)、Config L1 [39576910612](https://github.com/oudream/cx-visual-x3/actions/runs/39576910612)、配置底座 [39576910746](https://github.com/oudream/cx-visual-x3/actions/runs/39576910746)：**4/4 success**。 |
| `c8c7aebe0f40a2c223eed24f6a658c85c3382b05` | `IMRKExecutionEngine.SubmitWhenCapacityAsync`复用唯一F19 Worker与原生命周期门闩；MRK Stop唤醒未接纳者 | Runtime [39577236384](https://github.com/oudream/cx-visual-x3/actions/runs/39577236384)、工程组织 [39577236604](https://github.com/oudream/cx-visual-x3/actions/runs/39577236604)、Studio [39577236535](https://github.com/oudream/cx-visual-x3/actions/runs/39577236535)、TestCenter [39577236596](https://github.com/oudream/cx-visual-x3/actions/runs/39577236596)、Config L1 [39577236414](https://github.com/oudream/cx-visual-x3/actions/runs/39577236414)、配置底座 [39577236579](https://github.com/oudream/cx-visual-x3/actions/runs/39577236579)：**6/6 success**。 |
| `4ac570695e7e4b6c1e522900f2545c7ba3e5a6b3` | 背压诊断加入PeakPending、PeakWaitingProducers、SettledProducerWaits、MaxProducerWaitMilliseconds；原压力测试校验单调统计和最终账务 | Runtime [39577577696](https://github.com/oudream/cx-visual-x3/actions/runs/39577577696)、工程组织 [39577577721](https://github.com/oudream/cx-visual-x3/actions/runs/39577577721)、Studio [39577577717](https://github.com/oudream/cx-visual-x3/actions/runs/39577577717)、TestCenter [39577577650](https://github.com/oudream/cx-visual-x3/actions/runs/39577577650)、Config L1 [39577577769](https://github.com/oudream/cx-visual-x3/actions/runs/39577577769)、配置底座 [39577577840](https://github.com/oudream/cx-visual-x3/actions/runs/39577577840)：**6/6 success**。 |

**当前裁决：R08软件内核与背压增量S3通过；整轮仍未封闭。**
- 已经真实实现并验收：F19有界跨Key优先级公平性、Step/WholeFlow/Stage范围、动态迭代Stage、资源有序租约与异常回滚、生产者可取消容量等待、Stop拒绝未准入请求而排空已准入工作、MRK便捷调用和背压快照；无第二套Worker/工程/运行数据Owner。
- 剩余独立任务：产品级Drop业务审计与追溯、连续高吞吐真实吞吐量/峰值长稳、资源与队列等待的现场容量标定、MOL/MOC及Studio暴露的专业阶段配额属性和完整工程编辑闭环。**R08未获得生产设备联锁或跨进程资源所有权的证明**；不将上述Windows软件自动测试当成实机安全凭据。


### R08 拒绝/Drop原因与FLD重要审计接入（2026-10-21，待CI）

- F19原`FCRWorkResult<T>`增加只读`TerminalReason`，在原`Job<T>`已知终态路径赋值：停止、前置取消、单项预算不可能满足、队列数量/字节超额、生产者等待超限、DropOldest淘汰、排队/停止/等待阶段取消。业务可从相同Handle的最终Result追溯原因，不再根据累计Rejected/Dropped猜测。旧构造签名保留可选Reason默认None。
- `FCRWorkAudit.LogLoss`复用原F16 `FLDLogService` 的Important Writer。仅允许同一Handle的真实Rejected/Dropped结果，记录WorkId/Key/状态/Reason，传原WorkId作CorrelationId；由Host在F19调度锁外调用，返回false或FLD `Flush`异常不得冒充持久化成功。暂不新增第二套审计线程或文件仓库。
- 原FoundationSmoke下一批将测试Drop/Reject/Stop不同原因、拒绝冒用其他Handle、重要文件日志及原CorrelationId一致，待固定SHA Windows CI确认。


### R08 F19终态审计真实可执行反证（2026-10-21，待CI）

- 新`R08_TerminalReasonAndOriginalFLDAudit()`采用现有单FCRWorker、既有DropOldest策略：一个运行中任务+满队列，验证淘汰原业务Handle、队列数量拒绝、Pending字节超预算、单项超整体字节预算、生产者名额拒绝以及Stop等待者拒绝各自的Term​​inalReason。日志通过原F16 `FLDLogService` 的Important Sink写文件，并Flush核验原WorkId/Key/关联ID/最终Reason和审计条数。
- 反例拒绝把其它Handle完成结果冒用该WorkId；所有审计在Worker调度锁外执行，不影响原业务正常排空。重要事件`Write`返回false必须被Host视为审计未接纳，不得误作成功。仍需固定SHA Windows CI验收。


### R08 F19拒绝与Drop审计：Windows固定SHA证据（2026-10-21）

- **源码提交`74a7348903fdd80b19f0eda3ff2fc7367fab931d`**：FCRWorkResult<T>真实终态原因、FCRWorkAudit.LogLoss接入原FLD重要文件日志；同SHA Windows Runtime [39595012557](https://github.com/oudream/cx-visual-x3/actions/runs/39595012557)、Studio S1 [39595012615](https://github.com/oudream/cx-visual-x3/actions/runs/39595012615)、TestCenter [39595012619](https://github.com/oudream/cx-visual-x3/actions/runs/39595012619)、Config L1 [39595012591](https://github.com/oudream/cx-visual-x3/actions/runs/39595012591)、配置公共底座 [39595012613](https://github.com/oudream/cx-visual-x3/actions/runs/39595012613)，**5/5 success**。
- **真实落盘回归提交`1f08a9b73a0a6fa59c23adc6857daa89fbf07036`**：原F19 Worker中真实Dropped/Rejected、排队数量/字节/等待名额、停止及越界估算分别保留原因；真实FLD重要文件Flush与CorrelationId/WorkId/Key/原因一致，并拒绝把其他WorkHandle结果冒用当前身份。同SHA Windows Runtime [39595270550](https://github.com/oudream/cx-visual-x3/actions/runs/39595270550)、工程组织 [39595270169](https://github.com/oudream/cx-visual-x3/actions/runs/39595270169)、Config L1 [39595270321](https://github.com/oudream/cx-visual-x3/actions/runs/39595270321)、配置公共底座 [39595270341](https://github.com/oudream/cx-visual-x3/actions/runs/39595270341)，**4/4 success**。该SHA仅修改ArchitectureSmoke/文档，Studio/TestCenter路径过滤未单独触发，不能冒称已运行。
- **边界**：审计仍由Host显式调用`FCRWorkAudit.LogLoss`，返回true仅表示原F16日志队列接纳；持久化须Flush并核实LastFailure。Host自动调用/失败闭环、专业Drop策略审批、连续生产长稳、MOL/MOC与Studio组配额完整编辑、硬件安全均未由本次软件验收证明，**R08整轮未封闭**。


### R08 Host自动审计与故障拒绝（2026-10-22，待Windows验收）

- 现有Runtime.Host创建唯一F16 `FLDLogService`并注入原MRK/F19执行体系；FCRWorker每个Job在创建时订阅原Handle.Completion，异步写入Dropped/Rejected到原F16 Important Sink，含被后续Submit淘汰的旧业务。无第二个Worker或审计Writer。
- 原Worker调度锁之外运行审计回调，保留首个审计失败原因，后续Submit/SubmitWhenCapacityAsync及Host新Flow明确拒绝；`Worker.StopAsync`在原Worker排空后等待已注册回调、F16 Flush并将日志异常抛出，旧无审计实例流程兼容。`AuditFailure`与`AuditedLosses`可观测；不能把业务已完成视为重要审计已经落盘。须对本提交补正反例与固定SHA Windows CI。


### R08 自动审计与故障保护执行反证（2026-10-22，待Windows CI）

- 原FoundationSmoke增加`R08_HostAutoAuditDropRejectAndDurableStop`：唯一F19 Worker注入FLD后无需Host逐项LogLoss，真正Drop/Reject两项、正常业务两项，Stop等待审计完成+Flush，证据在原F16 Important滚动文件及WorkId关联中一致。
- `R08_AuditWriterFailureRejectsLaterSubmissions`通过实际Dispose现有FLD来注入日志不可用，观察`AuditFailure`，证明普通Submit和异步SubmitWhenCapacityAsync都拒绝下一条工作；Stop仍真正排空既有任务，但以IOException明确拒绝报告“审计已经可靠完成”。
- MRK ArchitectureSmoke新增`R08_RuntimeAuditUsesTheSameF19WorkerAndStopBarrier`验证原运行引擎传入F16实例后自动记录原WorkId及Stop异步排空，不建立第二个Worker。以上需要新固定SHA的Windows CI实际成功方能记已验证。


### R08 MOL/MOC执行组ID在统一工程中的保存、编译与运行（2026-10-22，待CI）

- 原`MOLStepPolicy`增加可选`ExecutionGroup`字符串，保留重试/超时等已有策略属性；原V2 `MOLWiringCodec` 的`Policy`可选`group`属性完成向后兼容保存/读取，旧V2有效载荷没有该属性时仍按未配置处理。不构建第二份工程组树。
- 原`MOCFlowBuilder.SetExecutionGroup(Guid,string)`按原MEM事务操作节点Policy，清除空值/无其他策略后删除记录；MOC普通节点、嵌套V2 Content编译传入原`MRKStepCompileRequest.executionGroup`，实际Step/WholeFlow/Stage容量仍由原MRKExecutionGroups拥有，不从节点中复制。
- ArchitectureSmoke新增`R08_EngineeringStageGroupIdentitySurvivesBuildAndReload`：真MEM创建/编辑/保存重开，V2Codec身份与MOC Plan组名一致，真实MRK Stage Flow运行并释放许可、非法组ID事务零修改、清除与Undo恢复。Stage配额工程级全定义和Studio可视编辑仍是R08下一增量，不可凭这次组ID持久化宣称完整编辑器已完成。


### R08 MOC编译自测首次Windows构建失败与修复（2026-10-22）

- 固定SHA `414f72ef74e200c5a8fd8b4f6908be232e244a54` 首次Windows Runtime [39719145827](https://github.com/oudream/cx-visual-x3/actions/runs/39719145827)、工程组织 [39719145808](https://github.com/oudream/cx-visual-x3/actions/runs/39719145808) 因测试错将`MOCBuildResult.Plan`（类型`MOCCompiledPlan`）直接传给MRKEngine并读取Steps，产生CS1061/CS1503。旧MOCCompiledPlan真正可执行计划是`build.Plan.Plan`。仅修正测试取值路径，不弱化保存重开、真实Stage执行、Undo及非法组身份反例。
- 此错误是实际Windows CI红灯，不能将上一提交标为编译通过；须在本修复SHA重新取得Windows x64实际成功证据。


### R08 Studio独立MOL Inspector执行组编辑（2026-10-22，待Windows CI）

- 使用原Studio V2逻辑面板新增运行组ID输入、应用和清除控件，选中的独立Content Tab通过`ApplyFlowProperty`调用唯一`MOCFlowBuilder.SetExecutionGroup`/MEM事务，刷新面板读原Wiring.Policies而非另建Studio缓存。用户填写的是已由Host配置的组ID，Step/Stage/WholeFlow额度与MaxWaiting仍由现有MRKGroup定义管理。
- `StudioShellSelfTests`补真实WPF点击反证：活动独立AI Content而旧ActivePage无关、MEM Revision恰好增加一次、Undo/Redo、非法超长组ID零修改及清空撤回。源码不能直接证明所有工业配置UI已经完成，仍以同SHA Studio Windows STA为准。


### R08 Studio 57项真实STA测试与CI门禁计数修复（2026-10-22）

- 固定提交`40fae6cff93eb4a4b7e476b90f9deb9fd84f79e3`的Studio [39719825990](https://github.com/oudream/cx-visual-x3/actions/runs/39719825990) **57 PASS/0 FAIL**，含新R08活动Content执行组编辑、Undo/Redo和非法组ID拒绝；工作流由于期待旧的56 PASS而判失败。保留这条失败证据。
- 本次仅将`studio-shell-windows.yml`的严格成功数提升至57，仍要求`FAIL=0`，不删除WPF用例，也不降级源码构建/主题/布局门禁。待修复SHA的真实Windows成功证明。


### R08 代码编排执行组配置闭环（2026-10-22，待Windows CI）

- `MRKCodeRuntimeBuilder.ExecutionGroup(id,maxConcurrency,maxWaiting,scope)`直接注册原`MRKExecutionGroupDefinition`；`MRKCodeFlowBuilder.StepInGroup`使用原`MRKStepCompileRequest.ExecutionGroup`而非第二套编排图。`Freeze`构建的Blueprint保留不可变执行组注册表并在运行前拒绝所有未知组；`BuildForDevelopment`将同一组配置传给原`MRKExecutionEngine`，实际按Step/Stage/WholeFlow仲裁。
- ArchitectureSmoke真实两Flow流水反证：Stage组容量1，同组两个连续节点不会自己排队死锁，Stage结束但第一个Flow尚在执行无组尾部时，第二个Flow可完整进入并完成；重复组ID拒绝、未知组Freeze前拒绝、冻结后Builder新注册组不修改已有Blueprint。
- 仅为开发者与CPU仿真的运行组配置；生产级Host设备容量必须独立授权，MOL/MOC现有工程组引用与Studio运行预览接入仍需专项。不以新增开发者API代替工程持久化或Studio控件交互验收。
