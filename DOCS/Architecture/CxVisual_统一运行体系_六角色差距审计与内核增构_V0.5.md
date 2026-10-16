# CxVisual统一运行体系：六角色需求逐项审计、内核增构与剩余架构契约 V0.6.1（沿用V0.5路径）

> **2026-10-16，工作版；设计仍可调整。**
> 需求原始依据：[《统一运行体系·六角色期望与并发异步编排执行·综合设计讨论稿V0.1》](CxVisual_统一运行体系_六角色期望与并发异步编排执行_综合设计讨论稿_V0.1.md)。
> 已实施代码：\`Foundation/Infrastructure/Concurrency/\`与\`Mechanism/Runtime/Execution/\`；测试在现有FoundationSmoke/ArchitectureSmoke工程。**此文件将原文需求、已经实现并由CI验证的代码，以及尚未实现的架构提案明确分开。**
>
> **本阶段主张**：优先独立做完整、可测试的通用运行内核，不等待旧V7.8迁移，不要求Vision/IoT/CT/AI业务接入验证。不因运行体系新增工程；并发执行主体仍集中在两个既有目录，数据遵守Foundation.Data与Mechanism.Data归属，不创建第二套Worker、并行Graph或新常驻进程。
> **验收口径**：每次提交需由对应GitHub Windows x64 CI成功证实；不能将源码存在、讨论稿和测试通过当成同一种证据。

## 01. 原文的结构与不可偏离的边界

原文第01～02层定义R1代码开发者、R2专业能力开发者、R3工程编排用户、R4调试工程师、R5生产操作用户、R6平台维护者，以及L0～L4易用性。第03层要求E01～E18；第04～07层要求Plan/Flow/Work/Context/Group、Session、资源/外部Operation、独立生命周期、数据与分支；第08～10层规定代码/编排双入口、两个实现中心、Studio/Runtime.Host/AI三个逻辑主进程；第11～13层要求Vision/PLC/CT/AI场景和真实证据。

**本次源码中的概念不应重新创建权威数据树**：CxNode与工程资源归Core；编排定义归Orchestration；变量/数据堆栈归Mechanism.Data；协议/Provider归Connectivity；Plan执行、运行会话和执行事实归Runtime.Execution；F19只负责通用并发基础。

### 01.1 角色期望→实际可见API→剩余缺口

| 角色 | 用户期望（原文） | 当前已有可运行基础 | 仍缺少的公共能力 |
|---|---|---|---|
| R1软件开发 | 1～3行提交/等待；无需手工队列/Lock；超时取消清晰 | \`FCRWorker.Submit/Post/Invoke\`、有界按Key队列、优先级、Cancel/Stop、WaitAsync | 用户友好的\`InvokeAsync\`组合简写、独立背压等待、统一业务错误码、专用线程位置 |
| R2能力开发 | 一次声明能力，代码与编排复用；输入输出、版本、资源/线程约束 | MRKStep委托、类型端口绑定、策略/组/资源声明 | 正式M04 Schema/M15 Capability适配器、版本能力约束编译检查、Provider装载/卸载 |
| R3编排用户 | 连线表达依赖、条件分支、循环、多层子流程、归并、异常 | DAG、守卫条件、Skipped传播、All/Any/FirstSuccess、有限循环、内联子流程、组级配额 | 动态/跨类型子流程、活动图/时序图编译、条件Join高级策略、循环节点跨步状态、类型化参数绑定到ContentRef |
| R4调试工程师 | Flow/Work/Plan/关联ID；准确知道等谁；断点/单步/追踪 | FlowMetadata/FlowSnapshot、WorkId/StepObservation、WaitingGroup/Resources、Trace、DebugController | 实际Trace持久化、远程只读Observe、断点权限、历史模拟重放、运行事件增量订阅 |
| R5操作员 | 安全启停、故障分因、请求停止≠设备停止 | RuntimeSession容量/停止/排空；Operation Unknown；请求关联；Flow诊断 | 运行端启动前置条件/健康模型、暂停/恢复安全点、Provider安全确认策略和授权恢复UI |
| R6平台维护 | 两个代码集中点、一个Engine、可压测 | F19+Runtime，原项目原测试扩展、GitHub Windows CI | 长时间竞态/公平性/性能基线、边界契约清理、队列/等待对象自动清理和可预测资源上限 |

## 02. 本轮新增的关键概念及代码（以前只有部分概念）

### A. ExecutionGroup：真正的“组”，不再只有字符串串行Key

\`MRKExecutionGroups.cs\`定义：
- \`MRKExecutionGroupDefinition(Id, MaxConcurrency, MaxWaiting)\`：组级并发配额和最大等待数量。
- \`MRKExecutionGroups.AcquireAsync\`：异步取得组席位，不占Worker线程；容量已满明确Rejected。
- \`MRKExecutionGroupSnapshot\`：活动、等待、拒绝的运行指标。
- \`MRKExecutionStep.ExecutionGroup\`：节点声明所属组，运行前校验所有引用；先拿Group席位→按固定顺序拿资源→提交统一F19 Worker，避免组与资源反向获得形成循环锁。

**区别**：ExecutionGroup管理业务容量；SerialKey保持同Key FIFO；RequiredResources控制进程内软件资源；F19 MaxConcurrency约束全局Worker槽位。四者正交、不能互相替代。下一阶段仍需整Flow/Stage/Resource串行范围的显式作用域策略、按字节背压、公平调度与长期指标。

### B. 能力端口和绑定：不是另建M04 Schema

\`MRKExecutionPorts.cs\`提供\`MRKExecutionInput\`、\`MRKExecutionOutput\`与\`MRKExecutionPortBinder\`：
- Flow输入、前置Step结果两个来源；要求直接依赖，发现悬空端口立即拒绝Plan；
- 输入类型预检在任何节点启动前执行，防止“前半流程产生副作用，后半发现原始输入类型错误”；
- 每个节点取得独立只读绑定视图\`BoundInputs\`，避免并发分支复用一个可变参数Map；
- 结果输出在放入Flow结果空间前做类型验证；类型错误会使节点失败、后继阻断；
- 子流程内联时改写内部端口引用为展开后的节点名，不创建新执行树。

**不能误认**：现在是内存Plan端口执行契约，不是M04全局类型系统、资源引用语义、完整DataStack/堆栈或设备属性模型；未来应由正确Owner完成一次性契约适配，而非复制Schema。

### C. 统一身份与观察

\`MRKExecutionIdentity.cs\`、\`MRKExecutionEngine.cs\`：
- \`MRKFlowMetadata\`：PlanRevision、BusinessKey、CorrelationId、SessionId、ParentFlowId；
- \`MRKStepObservation\`：StepId、WorkId、阶段、原因、UTC状态变更时间；
- \`MRKFlowSnapshot\`：Flow身份、时间、取消请求、完成/等待/失败/跳过数量；
- 保留\`MRKFlowResult.CompletedUtc\`的真实终态时间，多次查询快照不再虚构不断变化的完成时间；
- 节点因上游失败而未执行时为\`Skipped\`，不再误报“本节点执行失败”。

**缺口**：有界Trace仍是内存快照而非Journal；跨进程一致身份映射与增量观察契约，尚未由M31/M35/M36提供。

### D. 长期Runtime Session

\`MRKRuntimeSession.cs\`：显式Created→Ready→Stopping→Stopped；独立SessionId、活动Flow数量、容量/拒绝统计、计划Revision。多个会话共用一个\`MRKExecutionEngine\`，停止单个会话不终止其他会话或共享Worker。Session的本地排空/取消不等于生产设备已达到安全状态。

### E. 真正的编排条件分支和汇合

\`MRKExecutionStep.Condition\`负责节点守卫；条件false时节点\`Skipped\`，普通下游传播跳过。汇合节点可显式\`AllowSkippedDependencies=true\`，等待已经结算的所有依赖后继续。不同于旧版“在一个节点内写if”。

\`MRKWorkJoin.FirstCompletedAsync\`实现Any（首个完成可失败），\`FirstSuccessfulAsync\`实现FirstSuccess（只接受首个成功），DAG依赖实现WaitAll。先返回Any/FirstSuccess不代表其他分支已停止：所有尾部工作仍须通过\`AllSettled\`结算并安全释放数据。

## 03. 运行核心事实表：E01～E18

| 需求 | 基础内核状态 | 当前支撑 | 尚未闭合的概念 |
|---|---|---|---|
| E01提交 | 基础可执行 | FCR Handle/Accepted、Runtime Submit | 可靠重投及统一工作项历史 |
| E02顺序 | 基础可执行 | DAG依赖、Guard、Skipped | 动态活动图/版本化执行入口 |
| E03并发 | 基础可执行 | Worker上限、Priority、Group配额 | 公平调度/专用线程亲和 |
| E04异步 | 基础可执行 | Task真正等待、事件关联和恢复 | 外部ReliableExecutor/消息契约 |
| E05执行组 | 组级基础可执行 | Group并发/等待限额 + SerialKey | WholeFlow/Stage执行组编译 |
| E06背压 | 数量级可用 | QueueCapacity、Rejected、Droppable | 字节预算和等待容量策略 |
| E07数据 | 进程内部分 | FlowContext、Port绑定、共享Lease | DataSpace/堆栈/引用与跨进程所有权 |
| E08归并 | 主要基础实现 | All/Any/FirstSuccess/Guard Join | ConditionalJoin完整表达、尾部补偿策略 |
| E09取消/超时 | 进程内基础 | Token、超时观察、Operation.Unknown | 外部Operation耐久确认与恢复 |
| E10资源 | 进程内基础 | Lease、RequiredResources、Group顺序 | Runtime.Host权威分配和硬件联锁 |
| E11失败/恢复 | 进程内基础 | 幂等Retry、显式逆序Compensate | Journal/崩溃恢复/安全人工确认 |
| E12生命周期 | 基础可执行 | Worker、Flow、Trigger、Session Stop/Drain | 设备Session健康、分布式接管 |
| E13可观察 | headless可用 | Work/Flow/Step快照、Trace | 持久日志、远程订阅、指标追踪 |
| E14子流程 | 已知计划可运行 | InlineSubflow、作用域与端口 | 真正参数/返回Schema、动态引用与跨类型 |
| E15事件/定时 | 进程内基础 | AsyncGate、PeriodicJob、Correlation、Trigger | M31/M36事件总线、可靠队列 |
| E16调试/重放 | headless调试部分 | Pause/Breakpoint/StepOnce/Trace | 授权调试、安全重放、生产隔离 |
| E17计划切换 | 运行内存部分 | PlanSlot、Session固定版本 | Draft→Compile→Authorize→Publish→Rollback |
| E18策略 | 可组合部分 | SharedPool/Group/Key/Priority/Retry | Dedicated/External、CPU/GPU亲和、执行位置 |

“基础可执行”是小规模内存单测的功能事实，不是G2长期性能或G3/G4设备/跨进程验收结论。本次不以业务迁移作为实现前提，不能据此忽略上述公共体系核心缺口。

## 04. 下一批应先写的“红灯”测试，不按每条测试创建一个Manager

| ID | 运行概念/拟定Owner | 应证明的具体行为 |
|---|---|---|
| T24 | F19等待背压 | 一万提交中队列容量恒定，有界等待者按Token取消，拒绝均有明确票据 |
| T25 | F19执行组公平策略 | 高优先级持续到来时低优先级不得永久饥饿；同Key必须仍FIFO |
| T26 | Runtime全流程分组 | WholeFlow/Stage/Resource三种串行范围，同一引擎、不同吞吐，避免全局大锁 |
| T27 | M09运行数据栈契约 | 主/子/子子流程有独立作用域、引用、数据堆寿命和并发冲突规则 |
| T28 | M29计划编译 | 来自同一工程编排定义的多版本Plan可重复编译、静态校验错误定位精确 |
| T29 | M18/M30条件活动图 | 分支/Join/循环/跳转有精确的进入次数与退出条件，无重复副作用 |
| T30 | Runtime长任务恢复 | 主进程异常后只恢复可安全重放工作，外部状态未知不得自动重复执行 |
| T31 | M35 Journal | 保存Flow/Work/Plan/Correlation/Operation关键状态；重启后幂等恢复且日志可压缩 |
| T32 | M28权限/租约 | Observe默认只读，模拟与生产分离，抢租约/越权命令被拒绝 |
| T33 | M39正式发布 | 草稿不影响运行；发布失败自动回退；安全切换点来自运行端而非Studio |
| T34 | M31/M36可靠协作 | Event/Command订阅、顺序、超时、去重和跨进程关联不会静默丢工作 |
| T35 | F19线程位置 | 不可重入SDK在专用执行上下文的线程亲和和停止行为可测试 |
| T36 | Trace+UI | 50个并行Flow均能追踪实际等待者、资源、异常和关键业务关联，不重复或错配 |
| T37 | 大数据寿命 | 图像/CT Buffer在取消、超时、迟到和并发分支中只释放一次、不泄漏 |
| T38 | Foundation/Runtime压力 | 停止、取消、超时、重试和Plan切换随机交错百万次后无死锁、无越界或无限膨胀 |

**划分注意**：M09数据堆栈等属其原Owner，不应因为本运行体系设计而在Runtime另起第二套数据管理项目；上述是**职责契约和验收计划**，不是声称对应接口已经提交或设备实际验证通过。

## 05. 相关类与接口设计建议（未实施的仅是候选）

| 逻辑契约 | 建议Owner | 设计意图 |
|---|---|---|
| \`IExecutionPlanCompiler\` / \`CompileDiagnostics\` | M29/Runtime | 读取既有编排定义→校验→稳定Plan，输出所有编译错误与源节点定位 |
| \`IExecutionGroupPolicy\` / \`ExecutionScope\` | Runtime/Execution | WholeFlow/Stage/Resource约束编译与隔离，不能强迫SDK重复建Worker |
| \`IExecutionDataView\` / \`IDataLease\` | M09 + Foundation.Data | 运行节点按生命周期取变量/对象/Map/列表，不复制全局数据栈 |
| \`IExecutionRunJournal\` / \`RecoveryDecision\` | M35/Runtime | 记录可恢复事实；已发生外部副作用必须明确标记Unknown |
| \`IExecutionObserver\` / \`ExecutionTraceCursor\` | M31/M35/M36 + Runtime快照 | 增量订阅，有界流量、授权只读观测、关联与审计 |
| \`IExecutionAuthority\` / \`DebugLease\` | M28/Runtime | 区分模拟、Observe、授权控制、受控安全暂停 |
| \`IExecutionPublisher\` / \`PlanRevision\` | M39 + M29/Runtime | Publish原子性、兼容性检查、安全点、回滚 |
| \`IExecutionPlacement\` / \`DedicatedExecutor\` | F19 + Runtime调度策略 | 共享/专用/外部执行位置、亲和、并发、取消与诊断 |

类型名为候选，不应为了满足类数量指标提前创建空接口；需要与V6.4类型Owner及已有能力去重复后确定，**测试和实现可以直接推进，不用先做历史业务搬迁**。

## 06. 本轮自动测试新增清单

| 文件 | 验收关注点 |
|---|---|
| \`Tests/ArchitectureSmoke/ExecutionGroupTests.cs\` | 组级MaxConcurrency/MaxWaiting、拒绝、组与资源顺序 |
| \`Tests/ArchitectureSmoke/ExecutionPortTests.cs\` | 输入运行前验证、跨节点类型端口、并发绑定隔离 |
| \`Tests/ArchitectureSmoke/ExecutionIdentityTests.cs\` | PlanRevision/Correlation/WorkId、Waiting快照、Flow取消与终态时间 |
| \`Tests/ArchitectureSmoke/RuntimeSessionTests.cs\` | 多会话共享Worker、容量/拒绝、独立排空、PlanRevision固定 |
| \`Tests/ArchitectureSmoke/ExecutionFlowTests.cs\`（扩充） | Failed与Skipped区分、条件守卫、跳过传播、显式Join |
| \`Tests/ArchitectureSmoke/RuntimeCoordinationTests.cs\`（扩充） | Any允许失败Winner、迟到分支在AllSettled中继续结算 |

此前T01～T23继续保留，F19自身已有200任务四产线和共享数据并发释放的基础测试。**实际通过情况以每个commit对应Windows CI为唯一证据；未做的系统测试不得写已通过。**

## 07. 持续实施规则

- 用户要求：项目未发布期间**只在master直接开发，不创建分支，中文Commit，少量且可审阅**；同时有其他开发者提交时每笔以最新HEAD作为父提交，使用expected_sha避免覆盖。
- 在既有Foundation.Infrastructure/Concurrency和Mechanism.Runtime/Execution增构，类名/API可调整；不要为了兼容V7.8而牺牲运行核心正确性。
- 不让任何真实硬件动作依赖软件Lease作为唯一安全措施；即使模拟设备测试通过，也不把“请求停止”记成“设备已安全停止”。
- 文档是可迭代需求/设计审核，不是一次讨论后冻结。每次重大修改同步检查README、AGENTS、DOCS/00三个入口。


## 08. 运行体系V0.6：从六角色缺口直接反推的实现（2026-10-16）

本节是**V0.5增量实现记录**，保留上面原始差距与测试假设便于审查。正式源代码均保存在现有csproj并由Windows x64 CI验证；历史表中“待实现”的T项，以下仅标记**确有测试的部分能力**，不因单一API就称完整。

### 08.1 面向R1～R6，直接调用方式与维护Owner

| 角色 | 本轮新增的直接可用入口 | 已通过的基础语义 | 仍需完成 |
|---|---|---|---|
| R1代码开发者 | \`FCRWorkerOptions(..., pendingByteBudget:...)\`，Submit(...,estimatedBytes:...）；FCRDedicatedExecutor.InvokeAsync | 有界待执行图像字节估算；SDK同步函数固定物理线程；队列取消/停止 | 等待式背压、超时策略协调、资源权属统一 |
| R2能力开发者 | \`IMRKCapabilityResolver\`、\`MRKResolvedCapability\`、\`MRKStepCompileRequest\` | 一份实际能力委托被代码或编译Plan复用；能力缺失错误定位、同一SDK线程实现 | 与M04/M15真实能力注册表联动、Schema版本与权限 |
| R3编排工程师 | \`MRKExecutionPlanCompiler.Compile\`、原有条件/循环/子流程/类型端口 | 稳定生成Plan；聚合重复ID、未知能力、断依赖、环路；不部分执行 | 工程树到请求的编译适配、动态活动/时序图、子流程参数/返回端口 |
| R4调试工程师 | \`IMRKExecutionJournal\`、\`MRKFileExecutionJournal.ReadAll\`、\`MRKJournalRecovery.Evaluate\` | Flow/Step事实可落盘，记录序列/校验，读取与保守判定 | Trace增量订阅、跨进程观察、完整断点/模拟重放 |
| R5运行操作员 | \`MRKRecoveryDisposition.RequiresReconciliation\`、Session/Flow快照 | 外部Operation.Unknown或中断流程不自动重新执行；运行请求与设备确认分开 | 正式主机授权、安全点、硬件实际停止反馈 |
| R6平台维护者 | 两处执行目录＋Data两个原Owner、现有Smoke测试 | 不新增执行器、可构建、1800任务停止/取消交叉测试、Journal断帧验证 | 真正长稳/百万压测、Journal滚动归档、健康告警与分布式状态恢复 |

### 08.2 实现清单与可核对源码

**计划编译：** \`Mechanism/Runtime/Execution/MRKExecutionCompiler.cs\`

- 编排侧提供瞬时节点声明，不另存工程权威树；
- \`IMRKCapabilityResolver\`将CapabilityId解析为真实委托与输出类型；
- \`MRKExecutionPlanCompiler\`验证缺失、重复、环路、类型来源并生成稳定排序计划；
- Compile失败返回诊断列表与null Plan；不会开始任何流程；
- 测试：\`ExecutionCompilerTests.cs\`。

**执行数据作用域：** \`Foundation/Data/Execution/FDEExecutionDataContracts.cs\` + \`Mechanism/Data/Execution/MDEExecutionDataScope.cs\`

- 接口属于基础Data；父子作用域、只写一次、引用可见、资源拥有与逆序释放由机制Data实现；
- Runtime的\`StartFlowWithData\`**依赖Foundation.Data接口，不静态依赖Mechanism.Data程序集**；Flow创建独立作用域，节点创建局部作用域，结束结算后回收；
- 父级在子作用域存活时禁止释放；释放回调即使抛错也继续释放其他项并聚合异常；
- 对象内部深层不可变性仍由数据拥有者负责，大型Buffer应传入受控引用，不把字典快照当深拷贝；
- 测试：\`ExecutionDataScopeTests.cs\`。

**运行Journal与恢复判定：** \`Mechanism/Runtime/Execution/MRKExecutionJournal.cs\`

- \`MRKFileExecutionJournal\`：本地单写者、带长度/校验/单调序号的二进制帧，每条同步Flush；
- 断电造成的**截断尾帧**可修剪恢复，完整帧校验坏不能被静默忽略；
- Engine可选注入Journal，记录FlowStarted、StepTransition、FlowFinished，包含PlanRevision和CorrelationId，默认不写图像、密码和业务负载；
- 起始Journal写入失败将拒绝启动委托，避免不受追踪的副作用；
- \`MRKJournalRecovery.Evaluate\`仅做**保守裁决**：Flow没终态或外部操作未知必须先核对，不提供“自动重发设备指令”伪实现；
- 测试：\`ExecutionJournalTests.cs\`。

**字节背压：** \`Foundation/Infrastructure/Concurrency/FCRWorkContracts.cs\`和\`FCRWorker.cs\`

- \`QueueCapacity\`与\`PendingByteBudget\`分开，任务\`estimatedBytes\`需由提交方明确提供；没声明估算值的任务无法被系统猜测真实内存；
- 拒绝、可淘汰工作替换、待执行取消、Stop均维护字节计数；只统计Pending，不是运行中GPU内存占用；
- 测试：\`FCRWorkerTests.ByteBudgetAndOverflowAccounting\`与三轮共1800任务交叉停止/取消压力。

**专用同步SDK执行位置：** \`Foundation/Infrastructure/Concurrency/FCRDedicatedExecutor.cs\` + Runtime ExecutionStep

- 每个专用Executor拥有独立同步线程和有限排队；可声明STA/MTA；
- 运行节点可选提供DedicatedExecutorId+SynchronousSdk，Runtime经同一Worker提交Task并持有WorkId、错误与状态；
- 编译器可传递该能力声明；Plan启动前验证专用Executor已配置；
- 停止不会Thread.Abort，同步SDK回调不得在专用线程上等待自己；**async/await的续体不保证仍在专用线程**；
- 测试：\`FCRDedicatedExecutorTests.cs\`及\`ExecutionPlacementTests.cs\`。

### 08.3 对照T24～T38：实测进展与剩余核心事项

| 用例 | 本轮可确认的进展 | 不可省略的未完成部分 |
|---|---|---|
| T24 | 任务数及显式估算的Pending字节预算、拒绝、Drop/Cancel/Stop释放 | 生产者可取消的异步等待背压、严格全流程数据内存预算 |
| T25 | 同Key FIFO与跨Key Priority继续回归 | 高优先级连续注入下的公平性/不饥饿 |
| T26 | ExecutionGroup配额/资源租约/专用同步执行位置 | WholeFlow、Stage串行策略统一编译 |
| T27 | 基础Data作用域与机制Data隔离/释放、Runtime实接入 | 全量DataSpace堆栈/引用列表Map/订阅、跨进程Buffer Owner |
| T28 | Capability解析与稳定Plan编译、聚合诊断 | Orchestration/CxNode完整定义到编译请求的统一适配 |
| T29 | Guard、Skipped、条件汇合、限定循环 | 真正可视活动图复杂控制流与循环跨节点状态 |
| T30 | 对未闭合Flow/Unknown外部操作严格判定禁止自动重播 | Checkpoint、可证明安全的幂等断点恢复/启动核对 |
| T31 | 磁盘Journal帧、顺序校验、断尾修复、引擎事件贯通 | 跨进程Journal Owner、外部操作自动关联、Compaction、索引和重启恢复 |
| T32 | 进程内租约约束仍保留 | Observe/Debug/Control正式授权与Host隔离 |
| T33 | PlanSlot运行中固定版本、软件空闲原子切换 | 安全授权发布、跨进程发布、失败回滚与真正硬件安全点 |
| T34 | 既有应答Correlation机制有基础测试 | 跨进程可靠投递、事件/命令的持久关联 |
| T35 | STA/MTA专用同步SDK线程、有限队列、Runtime统一Work跟踪 | CPU/GPU物理调度、带线程亲和SDK的真实驱动实测 |
| T36 | Flow/Work/Step快照、有限Trace和可读Journal | 50并行Flow端到端远程观察与Studio高亮 |
| T37 | FCRSharedLease、子作用域拥有资源、并发释放测试 | 跨进程/跨GPU buffer引用，长时异常压力 |
| T38 | 三轮1800任务取消/关闭/预算清零测试 | 百万任务、长稳、随机资源时序及内存泄漏评估 |

**验收关键**：本轮不因没有专业接入而停止基础运行体系开发；也不通过制造接口空壳来把“未实现”伪装成“完整”。Data、M29、M35、M39等原本的机制Owner应在其范围内继续完成，Runtime负责使用它们，不复制第二套权威管理器。

### 08.4 V0.6.1并发故障边界与增量观察

- `MRKExecutionEngine.RunFlowAsync`：中途审计失效不允许让用户看到始终Running的流程；完成Journal持久写入失败须使Flow以明确Failed/Exception结算。Flow开始日志写入失败时，不启动任何副作用委托。
- `FCRWorker.Submit`：单个待执行任务超过整个`PendingByteBudget`时，必须直接Rejected；不得先淘汰其他被允许Drop的旧任务，再发现新任务永远无法满足配额。
- `MRKExecutionTrace.ReadSince(cursor)`：有界轨迹支持递增游标、`LostCount`与`HasGap`；监视端若错过环形窗口，须重新读取`StatesSnapshot`而不能假设事件流连续。
- 自动测试：`ExecutionJournalTests.MidFlowJournalFailureIsTypedFailure`、`FCRWorkerTests.ByteBudgetAndOverflowAccounting`、`ExecutionControlTests.TraceCursorDetectsLostTransitions`。此增构只完成headless内存/文件观察基础，远程订阅、审计权限和持久Trace压缩仍未实现。


## V0.6.2 外部操作身份与原计划补偿安全回归

- 同一Step可能含多次外部命令，Journal恢复仅以**OperationId + PlanId + Revision + StepId**核销Unknown，业务CorrelationId不代替OperationId。
- 新Journal v2保存OperationId，仍可读v1旧帧；缺OperationId的旧Unknown一律要求人工/Provider核对，新写入外部事实缺少OperationId直接拒绝。
- CompensateAsync必须提供Flow启动时固定的**原始MRKExecutionPlan实例**，同名不同实现不可代替；拒绝错误版本不占用补偿次数。
- 新TDD：OperationIdentityNeverCrossConfirms、LegacyExternalFactsRequireReconciliation、SamePlanNameCannotSubstituteOriginalVersion。
- 仍未完成：补偿调用期已释放数据作用域的明确交接协议、持久Checkpoint、权限授权发布、Host级恢复和跨进程资源安全。本次仅是进程内基础安全修复；Windows构建及自动测试证据以本次提交后的master CI为准。

## 2026-10-16 追加：补偿执行归属安全

- 进一步的代码审计发现：仅有原始Plan实例比对不足以阻止其他Engine接管同一Flow的补偿；不同Engine的资源协调器/Worker相互独立，可能绕开设备软件互斥。
- 在原有Runtime项目的MRKFlowHandle内部固定启动它的Engine实例；CompensateAsync在任何副作用及补偿名额占用前检查归属。新增`AnotherEngineCannotTakeOverCompensation`真实回归用例。
- 保留缺口：已释放的节点/Flow DataScope不能被补偿回调安全地直接重用；补偿输入需定义明确的持久化/租约交接与超时释放。外部Unknown资源隔离也不能用该内存归属校验替代。
