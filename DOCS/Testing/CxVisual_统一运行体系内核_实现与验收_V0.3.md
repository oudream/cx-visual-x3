# CxVisual统一运行体系内核：实现、协作语义与测试驱动验收 V0.6.1（沿用V0.3文件路径）

> 2026-10-15。**当前阶段优先把Foundation/Mechanism运行内核独立实现到可编译、可使用、可验证，不等待业务或专业接入，也不以迁移旧项目作为本阶段前置条件。** 本文记录真实C#代码和纯基础行为测试。  
> 设计前提来自[六角色综合需求](../Architecture/CxVisual_统一运行体系_六角色期望与并发异步编排执行_综合设计讨论稿_V0.1.md)；滚动验收见[统一TDD矩阵](UnifiedExecution_TDD_Matrix_V0.1.md)。**本文不是宣称所有E01～E18、生产设备安全、跨进程可靠执行均已交付。**

## 01. 目标、对外使用者与组织约束

六类角色：R1代码开发者、R2专业能力开发者、R3编排工程师、R4调试工程师、R5操作员、R6平台维护者。目标是**同一执行能力定义通过代码API和编排运行计划复用**，不需要每个专业建一套Worker/Queue/Executor/Monitor。

只有两处集中核心源码目录，且全部保留在**既有28项目**中：

| 集中位置 | 作用 | 不负责 |
|---|---|---|
| \`Foundation/Infrastructure/Concurrency/\` F19 | Worker、并发上限、KeyedSerial、有界队列、优先级、异步门、周期任务、数据票据 | 不解释Flow、CxNode、设备权限、UI和业务语义 |
| \`Mechanism/Runtime/Execution/\` M26～M34执行实现 | 已验证执行计划、统一Runtime引擎、节点依赖/分支/循环、调试状态、资源仲裁、流程触发及停止 | 不新增持久Graph树，不绕过M14协议/M15 Provider和硬件安全，不自行搭建第二事件Bus |
| \`Tests/FoundationSmoke/\`、\`Tests/ArchitectureSmoke/\` | 已有测试项目，分别验证基础行为和运行编排语义 | 不模拟证明实机安全和跨进程可靠投递 |

仍保留现有\`Mechanism.Orchestration\`对**流程定义与编排契约**的Owner；Runtime执行编译后内存计划，不把用户在Studio画的工程元素重新存成另一个项目结构。实际设备资源最终由Runtime.Host及对应专业Provider负责，**本阶段不进行专业接入**。

## 02. Foundation F19：核心可执行能力

| 源文件 | 类型与API | 已有行为 |
|---|---|---|
| \`FCRWorkContracts.cs\` | FCRWorkerOptions、FCRWorkHandle<T>、FCRWorkResult<T>、FCRWaitObservation<T>、FCRWorkerSnapshot | 任务接纳结果、类型化结果、超时仅为等待观测、不强杀工作 |
| \`FCRWorker.cs\` | FCRWorker.Submit/Post/Invoke、StopAsync、GetSnapshot | 共享线程池、有界任务队列、不同Key并发/同Key FIFO、可选优先级、拒绝/淘汰、协作取消、运行统计 |
| \`FCRSharedLease.cs\` | FCRSharedLease<T>.Retain/Dispose | 图像或体数据多分支共享；最后一个票据释放才通知回收；票据并发续借/释放互斥 |
| \`FCRScheduling.cs\` | FCRAsyncGate、FCRPeriodicJob | 异步重置广播门、独立取消；周期回调不重入、后台启动、取消/故障传播 |

**需明确的运行约定**：

1. **业务串行≠独占线程**：同Key Task在真正异步IO期间释放线程，但继续占据逻辑执行槽，不允许后续同Key命令乱序。
2. **队列容量统计待执行项**：运行中/等待中的任务占并发槽，队列满返回明确结果；只能淘汰显式标记可淘汰的Pending项。
3. **优先级不能打破同Key顺序**：不同Key可按优先级调度；同Key必须按提交顺序；饥饿公平性需要后续长期负载评估。
4. **超时不是停止**：Handle.WaitAsync超时不会将真正的设备/算法任务强制完成；取消为协作语义。
5. **共享数据票据是本进程内内存引用**：不能称为跨进程共享内存Owner，也不能替代对象引用期的使用协议。
6. **异步周期**是完成后再延迟，不是硬实时恒定周期，不应被用于硬件采图同步触发。

## 03. Runtime.Execution：统一执行能力

| 源文件 | 类型与API | 已有行为/边界 |
|---|---|---|
| \`MRKExecutionPlan.cs\` | MRKExecutionPlan、MRKExecutionStep、InlineSubflow | DAG依赖检查、重复ID及环拒绝、已解析动作和子流程内联、可选串行Key/Policy/RequiredResources |
| \`MRKExecutionEngine.cs\` | IMRKExecutionEngine、MRKExecutionEngine、MRKExecutionContext、MRKFlowHandle、MRKFlowResult | Submit、StartFlow、StartFlowWithControl、StartFromSlot、Flow状态与结果、活动实例登记、停止接单、排空/请求取消 |
| \`MRKExecutionPolicy.cs\` | MRKExecutionPolicy | 可选显式幂等有限重试、重试条件/退避、请求协作超时、待工作结算后才结束；结果不确定时不盲目自动重试 |
| \`MRKExecutionControls.cs\` | MRKExecutionOperations.Condition/Loop、MRKExecutionDebugController、MRKExecutionTrace | 条件分支、限定次数循环、断点暂停/单步/继续、可取消的断点等待、有界状态轨迹 |
| \`MRKRuntimeCoordination.cs\` | MRKResourceLeaseCoordinator、MRKResourceLease、MRKExternalOperationTracker、MRKCorrelationWaiter | 进程内资源独占；未知结果/迟到确认；请求与应答关联及超时/迟到保护 |
| \`MRKWorkJoin.cs\` | MRKWorkJoin.FirstSuccessfulAsync、MRKJoinResult.AllSettled | FirstSuccess归并；可请求取消未胜出分支；结果先返回但剩余工作生命周期仍可等待 |
| \`MRKExecutionPlanSlot.cs\` | MRKExecutionPlanSlot、MRKExecutionPlanLease | 单Host内存Plan版本固定、候选暂存和无在途时切换；不是跨进程正式发布 |
| \`MRKExecutionTrigger.cs\` | MRKExecutionTrigger.TryTrigger/StartPeriodic/StopAsync | 同一Plan支持事件调用和定期启动，在途容量限制、超额明确拒绝、停止排空 |
| \`MRKExecutionCompensation.cs\` | MRKCompensationReport、ExecutionEngine.CompensateAsync、Step.CompensateAsync | 仅在失败流程全分支结束后，由调用者显式执行逆序补偿；逐项保存失败原因，无默认设备回滚 |

### 03.1 一次流程的标准路径

~~~text
Studio/代码/Trigger
 → MRKExecutionPlan [已完成依赖与无环校验]
 → MRKExecutionEngine.StartFlow(...)
 → MRKExecutionContext [FlowId、输入快照、步骤状态、trace]
 → RunStepAsync:
       等待上游依赖
       可选调试断点等待（不占Worker）
       等待所需资源租约（不占Worker，多资源稳定排序）
       向统一FCRWorker.Submit提交
       Worker执行同步函数 / 异步Task / 可选显式幂等重试
       等待真实结果；更新当前步骤结果和状态
       释放资源租约
 → 分支All汇合/显式FirstSuccessful
 → MRKFlowResult [Completed/Failed/Cancelled + 结果与状态]
 → 移除活动Flow记录，最终释放PlanSlot引用
~~~

逻辑任务和线程不是一对一。CancellationToken只请求协作停止；设备写操作、运动、曝光等最终是否停止，需要下层Provider/物理安全系统确认。

### 03.2 控制流与数据作用域

- **顺序**：节点依赖；上游失败时下游不会被错误执行。
- **并行**：无依赖的步骤由同一Worker并发执行；结果按Flow/Step ID隔离。
- **分支**：可以通过Condition在一个节点内选择分支；动态分支图编译器尚待与Orchestration定义集成。
- **循环**：当前提供有界异步循环，防止无条件无限循环；尚不是完整的可视节点活动图解释器。
- **子流程**：编译时内联已知子流程，保留本子流程相对结果名，跨子流程访问使用显式完整键；不创建新Worker。
- **归并**：DAG All和FirstSuccessful；FirstSuccessful完成只是观察者得到结果，不等于所有未获胜外部工作已停止，释放共享数据前需等待AllSettled。
- **数据**：Flow的输入与各步骤结果是独立作用域；目前并非完整的数据堆/栈、版本化数据集与跨进程大对象生命周期机制，后续统一数据体系仍需连接。
- **资源**：节点声明RequiredResources，无需用户显式手写Lock；在Worker提交前异步获取，避免堵住运行槽；资源租约仅具有进程内软件语义。

### 03.3 失败流程的受控补偿

- \`MRKExecutionStep.CompensateAsync\`是**明确的可选委托**；缺省没有补偿，不会自动重发运动、曝光、PLC写指令或其他可能产生外部副作用的操作。
- \`MRKExecutionEngine.CompensateAsync(plan, flow)\`只允许在原Flow全部分支结算且结果非Completed时调用；同一Flow只能启动一次补偿，禁止重复执行副作用。
- 补偿按照**已完成节点的实际完成顺序逆向遍历**，而非仅按声明顺序；未成功完成的节点没有自动补偿。
- 补偿回调仍使用同一F19 Worker、原节点串行Key和RequiredResources，结果与原流程诊断分开。
- 某个补偿失败时继续记录其他已声明补偿，并返回\`MRKCompensationReport.Failures\`。取消可以中止尚未开始的补偿；**不承诺恢复外部设备真实状态**，需要Provider/现场授权与确认。

### 03.4 运行调试与诊断

- Trace存Step状态变化和UTC时间及递增序号，不保存图像/密钥；
- 队列深度、运行中数量、拒绝/淘汰统计可通过F19查询；
- FlowContext支持StatesSnapshot/ResultsSnapshot，能区分Queued、Running、Waiting、WaitingResources及Paused；
- DebugController支持Pause、Resume、StepOnce和节点断点；这实现**headless调试控制**，并不声称Studio WPF界面已经连接；
- 运行引擎StopAsync支持停止新提交、等待当前Flow排空；传入取消策略时请求取消，不强杀不合作的Provider；
- ExecutionPlanSlot不在流运行期间静默替换其Plan；只有无在途时允许软件版本切换。

## 04. 测试驱动验收：角色、场景、代码位置

| 维度 | 当前基础测试 |
|---|---|
| R1 SDK易用性 | FCRWorkerTests：Post/Invoke、WaitAsync超时观察、任务状态/异常和停止 |
| R2 Provider契约 | ExecutionPolicyTests：Task回调、幂等有限重试、故障传播、协作超时 |
| R3 编排 | ExecutionFlowTests：顺序、DAG并行/All、子流程、依赖校验；ExecutionControlTests：条件/循环 |
| R4 调试 | ExecutionControlTests：断点/单步/Resume、暂停时取消、状态轨迹；Flow状态快照 |
| R5 运行可靠性 | RuntimeCoordinationTests：软件资源互斥、操作未知与迟到确认、Plan切换、租约保留 |
| R6 维护/性能 | FoundationSmoke：200任务四产线FIFO、优先级、不重入周期、共享数据并发释放；ArchitectureSmoke：事件与周期在途上限、停止排空 |

现有源码测试文件：

- \`Tests/FoundationSmoke/FCRWorkerTests.cs\`
- \`Tests/FoundationSmoke/FCRSharedLeaseTests.cs\`
- \`Tests/FoundationSmoke/FCRSchedulingTests.cs\`
- \`Tests/ArchitectureSmoke/ExecutionFlowTests.cs\`
- \`Tests/ArchitectureSmoke/RuntimeCoordinationTests.cs\`
- \`Tests/ArchitectureSmoke/ExecutionControlTests.cs\`
- \`Tests/ArchitectureSmoke/ExecutionPolicyTests.cs\`
- \`Tests/ArchitectureSmoke/ExecutionTriggerTests.cs\`
- \`Tests/ArchitectureSmoke/ExecutionCompensationTests.cs\`

### 04.1 Windows CI命令与门禁

~~~powershell
msbuild.exe .\CxVisual.Unified.sln /m:1 /t:Rebuild /p:Configuration=Release /p:Platform=x64 /nologo /v:minimal
.\Output\Release\x64\CxVisual.Tests.FoundationSmoke.exe --concurrency
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe
~~~

Windows Workflow：[\`config-l1-ci.yml\`](../../.github/workflows/config-l1-ci.yml)。**只有实际对应HEAD的CI完成且显示success，才算G0/G1本轮通过**。完整共享测试仍含独立的F16历史诊断步骤，当前Workflow对该步骤采用continue-on-error；不得把全量诊断通过作为事实，除非另有独立日志。

## 05. 完成度：仅评价内核，不拿专业接入作为前置阻塞

| 范围 | 当前 |
|---|---|
| 基础工作执行与异步 | 已有真实代码和自动测试；串行/并行/优先级/有界队列/取消/异常/周期 |
| 内存流程编排执行 | 已有真实代码和自动测试；DAG、条件、有限循环、子流程内联、归并 |
| 数据与资源生命周期 | 本进程内引用票据和资源互斥已测试；大对象分布式Owner/数据栈未集成 |
| 运行控制及观测 | headless暂停/单步/断点、状态快照和有限Trace已测试；Studio UI未接 |
| 执行恢复/容错 | 幂等重试、协作超时、未知外部操作跟踪以及**显式失败逆序补偿**可在进程内使用；持久化恢复与设备实际状态回滚未交付 |
| 事件触发/定时 | Manual/Periodic Trigger和在途上限已测试；事件Bus及持久投递属独立机制 |
| 计划切换 | 内存版本固定/软件空闲切换已测试；工程编译发布、设备安全点、跨进程一致性未交付 |

**尚需完善但不要求现在先做业务迁移的系统级测试与能力**：

1. 代码级逆向压力测试：随机任务取消与Stop交错、优先级饥饿、公平性、毫秒级竞态、长期内存/句柄泄露、百万任务可观测性能；
2. M29/M39工程计划编译发布与正式Runtime进程之间的权威交接、授权和故障回滚；
3. 事件与数据堆栈/变量机制的协作、历史记录及持久化断点恢复；
4. 分布式/跨进程Executor、AI任务幂等/断线后结果未确认、Runtime.Host崩溃恢复；
5. 可视化Studio调试映射与权限：ReadOnly Observe、独占操作与生产会话安全保护；
6. 高级线程执行位置，如设备SDK专有线程亲和、GPU工作调度及实际硬实时边界；
7. 正式Studio WPF交互测试、真实设备/现场资源锁与物理联锁验证。

以上是**已实现基础与剩余边界的诚实划分**；绝不因为当前单元/模拟测试通过，声称已经具备全工业系统生产交付能力。

## 06. 长期实施规范

项目未发布阶段直接在\`master\`开发，新增提交统一用中文，少而清晰；当前不需要为本任务开分支。不要引入新项目或平行的Workflow Engine；修改代码时优先补可复现失败测试、在既有两处目录实现、让Windows CI门禁真实通过。设计稿可以更新，但不得要求先做专业迁移才完善运行内核。

版本与状态应以本文件、实际GitHub源码及对应HEAD CI为准；与旧V7.8或另几仓的映射是后续可做的事情，不能取代独立基础运行体系的测试与实现。

## 09. V0.6增构：计划编译、数据生命周期、持久审计与执行位置

在原有Concurency+Runtime的执行核心之外，数据使用既定两个Owner：`Foundation.Data/Execution/`只含公共IFDEExecutionDataScope；`Mechanism.Data/Execution/`实现父子隔离、一次写入、拥有引用与生命周期。Runtime只通过Foundation接口消费数据，不反向依赖Mechanism.Data实现。

Runtime新增`MRKExecutionCompiler.cs`将能力ID与依赖、端口绑定等瞬时声明转换成经过图校验的`MRKExecutionPlan`；真正业务委托由`IMRKCapabilityResolver`解析，不复制专业能力。新增`MRKExecutionJournal.cs`提供单写者文件Journal，记录Flow与节点事实，未知外部操作只产生人工/Provider核对判定。F19增加待执行数据字节预算及专用STA/MTA同步SDK线程，Runtime同一Worker可把SDK调用派送至专用线程。

独立测试：`ExecutionCompilerTests`、`ExecutionDataScopeTests`、`ExecutionJournalTests`、`ExecutionPlacementTests`、`FCRDedicatedExecutorTests`，以及`FCRWorkerTests`中新增的预计字节预算和1800任务取消/停止交叉测试。

详细R1～R6体验与T24～T38覆盖/缺口以[六角色差距审计V0.6](../Architecture/CxVisual_统一运行体系_六角色差距审计与内核增构_V0.5.md)为准。**上述是独立内核的代码与基础验证，不是完整的跨进程可靠恢复、正式发布、Studio UI或硬件安全验收。**

## 10. V0.6.1可靠性测试与观察契约

Journal运行中故障强制明确Failed终态，任务超预算无损拒绝其他已接纳项，Trace新增游标式`ReadSince`（缺口检测与重同步）。所有新增代码仍位于原Owner项目和既定执行目录，无新工程、无第二Flow引擎。远程Studio/实际设备确认仍属于更高等级验收。


## V0.6.2 外部操作身份与原计划补偿安全回归

- 同一Step可能含多次外部命令，Journal恢复仅以**OperationId + PlanId + Revision + StepId**核销Unknown，业务CorrelationId不代替OperationId。
- 新Journal v2保存OperationId，仍可读v1旧帧；缺OperationId的旧Unknown一律要求人工/Provider核对，新写入外部事实缺少OperationId直接拒绝。
- CompensateAsync必须提供Flow启动时固定的**原始MRKExecutionPlan实例**，同名不同实现不可代替；拒绝错误版本不占用补偿次数。
- 新TDD：OperationIdentityNeverCrossConfirms、LegacyExternalFactsRequireReconciliation、SamePlanNameCannotSubstituteOriginalVersion。
- 仍未完成：补偿调用期已释放数据作用域的明确交接协议、持久Checkpoint、权限授权发布、Host级恢复和跨进程资源安全。本次仅是进程内基础安全修复；Windows构建及自动测试证据以本次提交后的master CI为准。
