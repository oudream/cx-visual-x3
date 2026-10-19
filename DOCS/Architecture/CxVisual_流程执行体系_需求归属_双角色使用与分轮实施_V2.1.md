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

