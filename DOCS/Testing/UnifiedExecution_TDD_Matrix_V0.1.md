# CxVisual 统一运行体系：从六角色期望倒推测试与最小实现（TDD V0.6.1，原路径持续更新）

> **性质**：实现/测试事实 + 继续深化的验收清单。需求来源：[统一运行体系综合讨论稿](../Architecture/CxVisual_统一运行体系_六角色期望与并发异步编排执行_综合设计讨论稿_V0.1.md)。  
> **范围**：现有 \`Foundation/Infrastructure/Concurrency/\`（F19）+ \`Mechanism/Runtime/Execution/\`（执行引擎），在现有FoundationSmoke/ArchitectureSmoke测试工程扩展。不新增项目、第二套编排模型或独立进程。  
> **状态**：首个真实运行闭环；源代码/测试用例存在不等于全部E01～E18验收完毕。Windows x64 CI应以**本次提交对应工作流的实际结论**作为编译和自动测试证据；实机、负载、跨进程、WPF交互未由这些测试覆盖。  
> **边界**：属于可以继续优化的分阶段实现，**不是把V6.4全部候选API或设备安全机制一并定案**。

## 00. 本次TDD路线与阶段范围

从六角色期望倒推：R1软件开发者提交/等待/取消；R2专业Provider仅实现\`Func<CancellationToken,Task<T>>\`；R3工程编排用户的基本DAG连线与All汇合；R4调试人员查看Work/Flow状态；R5操作员观察明确的失败/超时事实；R6平台开发者保持两个源码集中目录、没有框架复制。

**测试场景驱动架构**（实际验收入口如下）：从首批10项扩展到20项以上可执行方法；场景代码与对应实现一同迭代，严格以Windows CI日志为准，未记录的红灯阶段不能声称已跑过；\`Tests/FoundationSmoke/Program.cs --concurrency\`单独运行F19测试，不依赖其他仍有已知资源竞争风险的F16同步日志测试；ArchitectureSmoke仍整体执行。

### 00.1 可执行命令

~~~powershell
# Windows + Visual Studio 2022/.NET Framework 4.8 Developer Pack
msbuild.exe .\CxVisual.Unified.sln /t:Rebuild /m:1 /p:Configuration=Release /p:Platform=x64 /nologo /v:minimal
.\Output\Release\x64\CxVisual.Tests.FoundationSmoke.exe --concurrency
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe
# 或观察 .github/workflows/config-l1-ci.yml 受保护测试步骤
~~~

不得用Linux静态XML检查替代上述编译/执行；本轮没有实机测试。编排可用测试是headless DAG计划，不是WPF画布已经可见、可编辑、可发布的验收证明。

## 01. 各角色期望→当前测试

| 角色 | 直接可验证的期望 | 已有自动测试 | 当前缺口 |
|---|---|---|---|
| R1 软件开发 | 提交带类型结果、按Key串行、背压、取消/超时不误判 | F19五项测试 | 跨进程Async、更多执行策略 |
| R2 专业Provider | 使用普通Task方法，异常不吞、可跨代码/流程复用 | ErrorIsObservable、DirectSdkAndFlowShareTheWorker | 能力Schema注册、Provider生命周期 |
| R3 工程编排 | 顺序依赖、条件、并行和汇合 | DAG、Condition、InlineSubflow、All/Any/FirstSuccess | Studio可视画布、活动图编译、动态子流程和高级Join |
| R4 工程调试 | 任务、节点、Flow及等待事实 | Flow/Step快照、WorkId、headless断点/单步、有限Trace | 正式Studio交互、权限隔离、持久Trace与远程订阅 |
| R5 运行操作 | 超时不等于硬件停止，失败不得继续后续节点 | TimeoutDoesNotStopUnderlyingWork、FailedStepDoesNotAdvanceDependent | 真实命令结果未确认/联锁/安全停止 |
| R6 平台维护 | 少对象、单一执行引擎、项目引用不反向 | 全解决方案Windows编译及两组自动测试 | 压力、race、长时间、现场部署 |

## 02. 已实现的核心类与接口（不是“每能力建一个Manager”）

| 源文件 | 类型 | 责任与重要边界 |
|---|---|---|
| F19/\`FCRWorkContracts.cs\` | \`FCRWorkerOptions\`、\`FCRWorkHandle<T>\`、\`FCRWorkResult<T>\`、\`FCRWaitObservation<T>\`、\`FCRWorkerSnapshot\`、相关状态枚举 | 接纳/等待观测/状态与结果；WaitAsync超时不结束真实任务 |
| F19/\`FCRWorker.cs\` | **\`FCRWorker\`** | 一套有界等待队列、MaxConcurrency、KeyedSerial、DropOldestDroppable、协调取消、StopAsync(排空/取消排队) |
| Runtime/\`MRKExecutionPlan.cs\` | \`MRKExecutionStep\`、\`MRKExecutionPlan\` | **运行态内存DAG**，只接受已解析动作；检查重复ID、缺失依赖和环；不是工程持久化Graph |
| Runtime/\`MRKExecutionEngine.cs\` | **\`IMRKExecutionEngine\`、\`MRKExecutionEngine\`** | 代码直接Submit、StartFlow、StopAsync统一使用同一F19 Worker |
| Runtime/\`MRKExecutionEngine.cs\` | \`MRKExecutionContext\`、\`MRKFlowHandle\`、\`MRKFlowResult\`、状态枚举 | 按Flow隔离输入、步骤结果与状态快照；取消需协作，不强制杀线程 |

命名遵循已有FCR与MRK前缀作为**首个可运行候选**。V6.4与旧V7.8的完整签名查重、对外二进制兼容、其他子方面Owner归属仍要逐项审核；禁止凭本文件把候选升级为永久的全平台唯一类型定义。

### 02.1 简单API（当前代码）

~~~csharp
// F19便捷入口：同Key有序，不同Key可并发。
FCRWorker worker = new FCRWorker(new FCRWorkerOptions(2, 12));
FCRWorkHandle<int> job = worker.Submit<int>(ct => ComputeAsync(ct), "Line1");
FCRWorkResult<int> result = await job.Completion;
await worker.StopAsync();

// Runtime：现阶段可接受已解析的内存计划及专业Task委托。
IMRKExecutionEngine engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 16));
MRKFlowHandle flow = engine.StartFlow(plan, inputs);
MRKFlowResult flowResult = await flow.Completion;
await engine.StopAsync();
~~~

专业用户无需每节点自己建Task.Run/Thread/Lock。F19的MaxConcurrency**计数包含等待中的逻辑任务**，不会在PLC请求等待时提前放过同一会话的第二条控制命令；但等待真正异步Task时已释放工作线程。

### 02.2 本次明确不做的伪实现

- 不新增“设备资源租约通过”空壳，不声称硬件实际停止或已通过安全联锁。
- 不复制M18～M25已有编排定义归属；当前运行DAG没有工程保存/加载/Studio编辑UI。
- 不把\`Task.Run(阻塞IO)\`当作真正异步IO，也不制造虚假的网络/PLC设备回包。
- 不把\`StartFlow\`的All汇合当作Any/FirstSuccess、循环、动态子流程、补偿事务均已实现。
- 不新增第二个Worker/TaskScheduler给专业模块或Orchestration。

## 03. 实际自动测试清单（当前纳入CI）

| ID | 所在测试方法 | 输入与断言 | 对应需求 |
|---|---|---|---|
| T01 | KeyedSerialAndAsyncWaiting | Line1第1项异步等待，第2项不得开始，Line2完成；首项Waiting可观察 | E03/E04/E05/E13 |
| T02 | QueueOverflowAndDrop | 运行1+队列1，替换显式Droppable旧工作；超额不可丢新项Rejected，计数正确 | E01/E06 |
| T03 | CancellationAndGracefulStop | 取消排队项、拒绝停止后的新项、正在执行项能排空完成 | E09/E12 |
| T04 | TimeoutDoesNotStopUnderlyingWork | 25ms等待超时，不强迫Completion结束；之后真实完成仍得到结果 | E09 |
| T05 | ErrorIsObservable | 业务委托抛错→Failed及原异常可见 | E11/E13 |
| T06 | ParallelJoinAndResultContext | Capture→CPU/AI并行→Join；AI未完成时不得汇合；两分支数据可读 | E02/E03/E07/E08 |
| T07 | InvalidGraphFailsBeforeExecution | DAG循环、缺失依赖在运行前明确拒绝 | E02/E17(只验证计划，不是发布) |
| T08 | FailedStepDoesNotAdvanceDependent | 上游Provider抛错，依赖后续不得实际执行，Flow Failed | E02/E11 |
| T09 | CancellationIsCooperative | Flow请求取消，Token传至等待Task，确认退出后Flow才Cancelled | E09/E12 |
| T10 | DirectSdkAndFlowShareTheWorker | 代码直接任务与DAG节点共同按Key受同一Worker控制 | E01/E05 |

测试使用TaskCompletionSource作显式时序门，避免靠Thread.Sleep“碰巧通过”；每项异步等待有8秒测试超时保护。T04的短计时仅用于验证等待超时语义，不用来证明毫秒级工业时序。

## 04. E01～E18需求覆盖矩阵（2026-10-15滚动复核）

| ID | 当前状态 | 本次已落实 | 仍未完成/不得宣称 |
|---|---|---|---|
| E01 任务提交 | **基础实现** | FCRWorker.Submit、Post、Invoke、Accepted、结果票据 | 分布式持久任务/跨进程提交 |
| E02 顺序执行 | **基础实现** | DAG/条件分支/有限循环与失败阻断 | 动态活动图编译与复杂循环状态恢复 |
| E03 并发并行 | **基础实现** | MaxConcurrency、跨Key并行/优先级、200任务四产线FIFO | 专有线程亲和与全局资源预算 |
| E04 非阻塞异步 | **基础实现** | Task等待不占Worker线程，逻辑串行槽继续持有 | 真实PLC/IoT协议收发及设备时序 |
| E05 执行组 | **部分** | KeyedSerial与跨Key并行 | 整流程/阶段/资源作用域自动映射 |
| E06 背压 | **部分可执行** | 任务数/预计字节双预算、拒绝、Droppable淘汰、取消与Stop清算 | 异步等待背压、运行内存/GPU预算、专业失败记录 |
| E07 数据交接 | **内存作用域基础实现** | Flow/节点DataScope、父子隔离、只写一次、拥有资源回收与FCRSharedLease | 完整DataSpace堆栈、多进程Buffer所有权与深层不可变性 |
| E08 等待归并 | **基础实现** | DAG All、FirstSuccessful、取消剩余分支、AllSettled | 动态分支补偿及外部可靠归并 |
| E09 超时取消 | **部分** | F19超时不误杀、取消协作、Correlation、Operation Unknown/late confirm | 外部操作持久化及硬件停止确认 |
| E10 资源互斥 | **进程内实现** | RequiredResources声明式仲裁、固定资源顺序、Worker外异步等锁、Lease自动释放 | 跨进程设备Owner与物理联锁 |
| E11 异常恢复 | **进程内基础实现** | 原异常、失败阻断、幂等有限重试、协作超时、完成步骤逆序显式补偿及失败报告 | 持久任务恢复、设备状态核验与人工安全授权 |
| E12 生命周期 | **进程内基础实现** | Worker/Flow/Session启停排空、计划版本引用、数据作用域释放与失败报告 | Host进程崩溃和设备Session实际停止 |
| E13 可观察 | **基础headless** | Flow/Worker状态、Queued/Waiting/Paused/WaitingResources、有限Trace | Studio调试面板、持久追踪和授权 |
| E14 子流程 | **基础内联实现** | InlineSubflow编译展开、相对作用域、单Worker CT流程无死锁 | ContentRef实数据绑定、子流程动态递归与循环/参数类型 |
| E15 事件定时 | **基础实现** | FCRAsyncGate/PeriodicJob、MRKExecutionTrigger、在途上限与拒绝计数 | 工业事件Bus、定时精度/持久订阅 |
| E16 调试重放 | **调试基础实现** | 断点、暂停、StepOnce、Resume与取消，状态轨迹 | 历史数据录制/安全重放/WPF面板 |
| E17 发布计划 | **运行侧部分** | MRKExecutionPlanCompiler瞬时能力解析/静态诊断、PlanSlot版本固定与软件切换 | 工程CxNode映射、正式授权编译发布、跨进程Safe Publish与Rollback |
| E18 执行策略 | **部分可执行** | 共享Worker、KeyedSerial、Group/Priority、幂等Policy、专用STA/MTA同步SDK执行线程 | GPU/CPU亲和、可靠外部Executor与公平性 |

**状态口径**：“基础实现/部分”仅在真实CI运行通过对应行为用例后成立；所有PLC、X-Ray、CT等在本轮都是模拟委托测试，未进行真实硬件安全验收。

## 05. 按测试驱动继续反推后续最少接口（目前仅设计候选，不创建空类）

| 测试优先的用户案例 | 建议下一步测试（红灯） | 最少的接口角色（待V6.4查重） |
|---|---|---|
| 一台PLC只能同时接受一个写控制，Studio不得抢写 | T11 不同Flow/线程竞争同DeviceId，至多1个持有者；取消等待且无泄露 | ResourceLease acquire/release、运行端Owner判定 |
| 设备命令发出后应答丢失 | T12 Caller TimedOut、外部Operation Unknown且不得自动重发 | OperationTracker/ExternalOperationReceipt |
| Vision连续产品积压超过容量，必须报业务失败 | T13 淘汰可丢Pending图像，关联产品缺陷/失败记录且无图像早释放 | Submission/Drop通知、DataLease |
| 分支FirstSuccess返回时其他分支仍运行 | T14 剩余分支取消/等待/清理策略，迟到结果不污染下游 | JoinPolicy/BranchScope |
| Studio仅观察正式Runtime，不能写设备 | T15 只读Observe允许、控制权限拒绝、模拟Provider隔离 | ExecutionObserver/AuthorityScope |
| 运行流程跨多个子流程、子子流程 | T16 参数作用域隔离、失败传播、重入/循环控制 | CompiledSubflowCall |
| 定时采集不能重入、异步应答匹配请求ID | T17 Timer触发、单会话串行、匹配应答、超时恢复 | EventWaiter/PeriodicTrigger与M14 |
| 配置草案修改不能改变已经执行的Plan | T18 版本切换成功/失败/回退，无Plan运行中途半修改 | PlanPublisher/ExecutionRevision |
| CT停止时没有安全确认不能释放设备租约 | T19 StopRequested≠SafeStopped，设备专业Interlock负责最终确认 | ExternalOperationStatus/ResourceLease |
| AI服务掉线时流程不误完成、不重复推理 | T20 CorrelationId、幂等/重试策略与未知结果明确 | ProviderInvocation/DeliveryPolicy |

其中T11、T12、T14、T16（内联子流程）、T17（请求关联）、T18（软件Plan切换）、T19（模拟资源保持）、T20（进程内关联去重）已提交对应可执行测试；T13仅覆盖可淘汰工作状态与共享数据租约，尚未自动写入Vision业务失败记录；T15仍未实现。未来G3/G4仍需额外测试，也不要求新建10个独立Manager。真实接口应放回M09/M12/M14/M15/M26/M28/M29/M30/M31/M36等唯一Owner内；**执行实现仍主要收敛在两个目录**，不做跨项目反向依赖。

## 06. 构建、性能和安全验收门槛

### 06.1 自动化测试层级

- **G0编译与结构**：.NET48传统csproj显式Compile Include；引用仅Runtime→F19；no-WPF Foundation/Runtime；无新的常驻进程或Graph树。
- **G1基础行为**：T01～T12及已落地的T14、T16～T20对应基础测试必须实际运行通过；CI保持Foundation/F19和Architecture两道独立失败门禁。
- **G2压力/竞态**：已覆盖200项四产线顺序压力测试；未来另需高并发随机Key、百万级提交压测（内存和时延预算经实测制定）、重复取消/Stop竞争、队列饱和和丢弃通知、长时间资源检查。
- **G3专业模拟**：Vision双产线真实积压业务报告、IoT协议会话异步应答、AI跨进程超时及迟到。
- **G4实机/现场**：PLC/运控/X-Ray联锁、紧急停止、设备安全状态及数据/历史正确性。

G0/G1通过不能代替G2/G3/G4。CI工作流故障时应分清本次新增测试与旧F16文件锁等独立问题，不得将非相关基础故障静默归咎于新F19。

### 06.2 源码与工程依赖

- \`Foundation/Infrastructure/CxVisual.Foundation.Infrastructure.csproj\` 显式纳入FCRWorker、FCRWorkContracts、FCRSharedLease等F19源文件。
- \`Mechanism/Runtime/CxVisual.Mechanism.Runtime.csproj\` 显式纳入执行计划、引擎、协作/租约、Join与PlanSlot等Execution文件，并新增**单向** \`Foundation.Infrastructure\` 项目引用。
- FoundationSmoke包含FCRWorkerTests与FCRSharedLeaseTests，ArchitectureSmoke包含ExecutionFlowTests与RuntimeCoordinationTests（已有项目，无新csproj）。
- Windows工作流将\`FoundationSmoke.exe --concurrency\`作为必须成功的测试；旧F16日志全量Smoke继续作为单独诊断。
- 不更动Professional、Orchestration、Studio的代码接口与运行路径；将来通过明确能力适配接入。

## 07. 实施与沟通规范

1. 本文保留测试驱动架构的**需求与证据映射**，不覆盖先前的六角色需求讨论稿。
2. 后续每轮只把当前确有实现与成功测试的条目标记“已验证”；新增行为先增加失败测试，再实现，避免空壳类。
3. 当实现新增执行类/API时继续审V6.4候选与旧V7.8完整签名；如发现冲突先迁移/统一Owner。
4. 真实编排、跨进程、安全联锁不做假通过；测试、现场条件与失败原因应可复查。
5. 更新本档案路径或状态后同步检查\`README.md\`、\`AGENTS.md\`、\`DOCS/00\`入口。尽量一项功能对应少数小文件和一次可审查提交。


## 08. 本轮新增运行代码与测试核查清单（2026-10-15）

在既有两个源码目录下实现，均已登记旧式csproj的显式Compile Include：

| 文件 | 对外核心能力 | 对应测试与证据 |
|---|---|---|
| \`Foundation/Infrastructure/Concurrency/FCRWorker.cs\` | 异步有界Worker、KeyedSerial、Post/Invoke便捷入口、Stop/Cancel/Overflow | FCRWorkerTests：顺序/等待/超时/丢弃/异常/200任务四产线 |
| \`Foundation/Infrastructure/Concurrency/FCRSharedLease.cs\` | 图像/体数据引用计数，最后一份票据释放回调一次 | FCRSharedLeaseTests：多分支并发释放 |
| \`Mechanism/Runtime/Execution/MRKExecutionEngine.cs\` | 单一执行引擎、分支调度、状态快照、StartFromSlot | ExecutionFlowTests和RuntimeCoordinationTests |
| \`Mechanism/Runtime/Execution/MRKExecutionPlan.cs\` | 静态DAG校验、子流程内联展开及相对作用域 | CT单Worker嵌套流程 |
| \`Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs\` | 进程内资源独占、Operation Unknown/late confirm、请求应答关联 | T11/T12/T17/T19/T20 |
| \`Mechanism/Runtime/Execution/MRKWorkJoin.cs\` | FirstSuccessful及剩余分支的AllSettled | T14 |
| \`Mechanism/Runtime/Execution/MRKExecutionPlanSlot.cs\` | 运行中锁定旧Plan版本、暂存/空闲切换新Plan | T18 |

**显式限制**：MRKResourceLeaseCoordinator仅保护同一Runtime进程内的逻辑资源，不是分布式锁或物理安全联锁；MRKExecutionPlanSlot不是M39正式发布平台；MRKCorrelationWaiter不是可靠网络消息投递；InlineSubflow只是运行计划层展开，不取代现有CxNode工程结构和Orchestration定义。

**主仓Git工作方式**：用户明确要求未发布阶段只在\`master\`开发、中文提交；已写入\`AGENTS.md\`和协同入口。由于存在同时进行的配置模块开发，每笔修改使用最新主分支HEAD作为父提交和预期引用，避免覆盖其他模块。

**Windows CI入口**：[\`.github/workflows/config-l1-ci.yml\`](../../.github/workflows/config-l1-ci.yml)，检查完整Release/x64解决方案编译、独立F19并发测试、ArchitectureSmoke与WPF基础交互。最终验证请以当前主分支HEAD对应Actions绿色结论为准，不能把历史某次绿色结果引用为以后所有提交的证明。

**尚待下一轮真实TDD**：T13业务失败持久化、T15只读Studio权限与模拟Provider隔离、T16子流程类型/动态引用、T17周期不重入、T18跨进程安全发布/回滚、T19真实PLC/运动/X-Ray联锁、T20进程重启持久幂等/跨网络可靠交付。无实机时不能声称这些已验收。

## 09. 运行内核补全批次（详见专项验收文档V0.3）

本轮不等待专业接入，集中完成基础体系内的周期/事件触发、条件循环、优先级、幂等重试、协作超时、声明式资源仲裁、headless暂停/单步和执行Trace、Runtime停止排空、图像引用票据竞态保护。

- **详细实现、源码清单与测试口径**：[CxVisual统一运行体系内核V0.3](CxVisual_统一运行体系内核_实现与验收_V0.3.md)。
- **F19新增**：\`FCRScheduling.cs\`中的\`FCRAsyncGate\`和\`FCRPeriodicJob\`；Worker增加跨Key优先级（同Key仍严格FIFO）；共享票据完善Retain/Dispose同步。
- **Runtime新增**：\`MRKExecutionControls.cs\`（Condition/Loop、调试控制、Trace）、\`MRKExecutionPolicy.cs\`（显式幂等重试/超时）、\`MRKExecutionTrigger.cs\`（手动/周期触发与在途上限）；ExecutionStep增加\`RequiredResources\`并在Worker外部异步申请。
- **Runtime完善**：ExecutionEngine跟踪活动Flow，支持排空/协作取消Stop；节点状态包含Paused和WaitingResources；所有运行操作共享F19并发基础。
- **测试扩展**：FoundationSmoke增加FCRSchedulingTests、优先级调度测试及共享数据竞态；ArchitectureSmoke增加ExecutionControlTests、ExecutionPolicyTests、ExecutionTriggerTests及资源/停止交叉测试。
- **明确尚未交付**：真正的Studio运行可视调试UI、跨进程Runtime高可靠投递、外部硬件安全联锁、Data堆栈的统一工程接入、动态插件编译/正式发布，不能以当前纯基础测试证明它们已实现。

最新源码与Windows CI状态以实时GitHub主分支为准，**不要把文档写成已经验收所有工业系统**。


## 10. 显式补偿功能与TDD（持续完善）

本轮将失败处理从“可观察Failed”继续推进至**显式申请、有序结算、只补偿真正完成的节点**。核心在\`Mechanism/Runtime/Execution/MRKExecutionCompensation.cs\`及原有Step/Engine中，测试在\`Tests/ArchitectureSmoke/ExecutionCompensationTests.cs\`。

- T21：A→B成功、C失败，默认不会自动执行回滚；显式CompensateAsync后执行B→A逆序补偿。
- T22：同一Flow二次CompensateAsync拒绝，防止外部副作用重复。
- T23：B补偿失败时，A的合法补偿继续，且MRKCompensationReport保存逐项异常。
- 明确限制：仍是本进程内运行补偿基础，不应把补偿“运行过”误认为外部设备完成安全恢复；生产控制需独立权限、联锁及设备反馈判定。


## 11. 六角色原始需求回查与运行内核V0.5

- 本轮重新逐项审阅原始612行六角色文档，不以当前已实现类反向裁剪需求。
- 实际增构：执行组与容量、端口类型绑定、身份与详细观察、独立RuntimeSession、Any与跳过传播、条件守卫及显式Join。
- **逐项差距、候选API与测试T24～T38**：见[六角色差距审计与内核增构V0.5](../Architecture/CxVisual_统一运行体系_六角色差距审计与内核增构_V0.5.md)。
- 剩余Data堆栈、M29工程编译、M35 Journal、M39安全发布、M28权限、Dedicated执行位置、M31/M36可靠协作目前仍不应标已实现。

## 12. 运行体系V0.6实际落地及证据

对照[六角色差距审计V0.6](../Architecture/CxVisual_统一运行体系_六角色差距审计与内核增构_V0.5.md)：

- **编译**：`MRKExecutionPlanCompiler`和`IMRKCapabilityResolver`，`ExecutionCompilerTests`；仍须M18/M29既有工程编排树的真实定义适配。
- **数据**：Foundation.Data接口、Mechanism.Data层级作用域、Runtime节点/Flow注入与回收，`ExecutionDataScopeTests`；全平台数据堆栈未完成。
- **可靠性**：`MRKFileExecutionJournal`、`MRKJournalRecovery`、真实节点事件落盘，`ExecutionJournalTests`；崩溃恢复仅保守核对，不能自动重播未确认外部操作。
- **并发/资源**：F19预计字节待执行预算、专用同步SDK线程、Runtime映射、三轮共1800提交与停机交叉测试；专用线程不承诺async续体亲和，尚无GPU/CPU物理调度。
- **证据规则**：以上仅以各自对应commit的Windows Actions编译和基础自动测试成功为G0/G1证据；不等价于生产实机和跨进程交付。

## 13. V0.6.1最后一轮正确性修补

- **Journal中途失效**：Engine返回`MRKFlowResult.Failed`并暴露异常，不能让流程在任务已完成时伪装为持续Running；日志异常同时意味着持久事实可能不完整，恢复端必须核对。
- **预算预检**：新任务预估大小已经超过整个Pending预算时，无论队列中是否有Droppable项，必须直接拒绝，不产生误淘汰。
- **增量Trace**：`MRKExecutionTrace.ReadSince`通过`NextCursor/LostCount/HasGap`报告历史窗口覆盖；监视端必须在HasGap时重新获取完整快照。
- **证据**：分别由`ExecutionJournalTests`、`FCRWorkerTests`、`ExecutionControlTests`执行；后续源码修改是否仍通过，以对应master HEAD的Windows CI为准。


## V0.6.2 外部操作身份与原计划补偿安全回归

- 同一Step可能含多次外部命令，Journal恢复仅以**OperationId + PlanId + Revision + StepId**核销Unknown，业务CorrelationId不代替OperationId。
- 新Journal v2保存OperationId，仍可读v1旧帧；缺OperationId的旧Unknown一律要求人工/Provider核对，新写入外部事实缺少OperationId直接拒绝。
- CompensateAsync必须提供Flow启动时固定的**原始MRKExecutionPlan实例**，同名不同实现不可代替；拒绝错误版本不占用补偿次数。
- 新TDD：OperationIdentityNeverCrossConfirms、LegacyExternalFactsRequireReconciliation、SamePlanNameCannotSubstituteOriginalVersion。
- 仍未完成：补偿调用期已释放数据作用域的明确交接协议、持久Checkpoint、权限授权发布、Host级恢复和跨进程资源安全。本次仅是进程内基础安全修复；Windows构建及自动测试证据以本次提交后的master CI为准。

## 本批追加：原始Engine资源仲裁隔离

- 验收用例：`ExecutionCompensationTests.AnotherEngineCannotTakeOverCompensation`（同一Plan和已失败Flow，由第二Engine调用`CompensateAsync`必须拒绝、不得执行副作用、不消耗原始Engine合法补偿名额）。
- 契约：补偿不仅绑定执行当时的Plan**对象身份**，也绑定启动Flow的原始Engine实例。跨进程/重启后恢复必须经正式恢复授权与重新建账，不能借另一实例绕开进程内资源仲裁。
- 与运行数据Scope相关的补偿输入生命周期、生产安全接管仍是独立待解决问题；本次测试不宣称已修复它们。

### 补偿数据作用域隔离回归

`ExecutionCompensationTests.CompensationPortsRemainAvailableAfterDataScopeDisposal`：
失败Flow完成并释放本地数据之后，显式补偿回调获得原Plan类型化输入的**重新绑定视图**以及已结算结果；已经关闭的`DataScope`不再暴露给补偿回调。验证`PutOwned`资源释放恰好一次，补偿能够真正执行。该测试仅使用值类型端口，不证明可变或大对象的保留所有权；后续仍需设计显式补偿数据交接租约和过期处理。

### 非协作操作超时的可见性和资源租约测试

`ExecutionPolicyTests.NonCooperativeTimeoutRemainsUnknownAndKeepsResources`：不响应取消的原始工作在策略超时后仍继续运行；`MRKStepState.ResultUnknown`能被Flow快照观察，原软件资源租约依然排他，下游不会启动，Stop不能提前完成；底层结算后才允许释放。诊断回调故障也必须先等待真实工作结算。尚未实现安全隔离接管、OperationId持久确认；进程停止不可强制结束不合作的外部动作。

### ResultUnknown诊断故障禁止重试

`TimeoutDiagnosticFailureCannotTriggerRetryOrEarlyRelease`：策略虽标记Idempotent和3次尝试，但超时诊断写入失败也必须等待正在运行的实际工作结算；以`TimeoutException`（`InnerException`保存诊断失败）返回，不得按普通异常重新触发具有不确定外部结果的工作。

## 2026-10-16 后续四批增量交付（按对应Commit的真实Windows CI核定）

- B1：`MRKRuntimeSession.StopAsync(false)`后可以重复调用`StopAsync(true)`，必须升级为真实取消，而非因已有排空任务忽略升级；测试`RuntimeSessionTests.CancelEscalatesAnExistingDrain`。
- B1：Journal的重复FlowId、孤儿FlowFinished与无OperationId的Step.ResultUnknown保守判定为RequiresReconciliation；错误版本的ExternalConfirmed只忽略，不永久污染同Flow合法确认。已强化`ExecutionJournalTests`及`TCT.MRK.014`。
- B2：Step/CompileRequest的Priority及EstimatedPendingBytes传递给FCRWorker，并由内联子流程保留；`ExecutionFlowTests.WorkerPriorityAndPendingBudgetPropagateFromStep`验证实际排队顺序和预计字节预算。尚未实现全局Flow预算和实际内存/GPU用量核算。
- B3：`MRKExecutionPlanPublisher`把Compile、Authorize、Stage、安全点Activate和单调修订回滚组织成发布门面。生产保护模式的`MRKExecutionPlanSlot`拒绝公开Stage/Activate捷径；`ExecutionCompilerTests.AuthorizedPublishingAndSafeRollback`验证在途Flow旧版本、授权拒绝、受保护Slot及回滚。模拟策略**不代表真实操作者身份认证或硬件安全联锁**。
- B4：`Applications/Runtime.Host/UnifiedRuntimeHost.cs`支持`runtime-run`、`runtime-serve`、`runtime-recover`。Run使用实际编译能力、受保护Plan发布、RuntimeSession、Flow、持久Journal；重启扫描必须拒绝未结案的Flow，不自动重放；Windows工作流验证正常执行、扫描及第二次干净重启。只执行纯模拟能力，无设备控制。
- **仍未完成的实质内容**：CxNode/ContentRef真实可视活动图与多级动态子流程编译；整流程/阶段级策略与全局配额；Checkpoint持久化与授权再入/数据快照恢复；正式身份认证与远程Host控制/隔离接管；运行时数据体系及跨进程/硬件验收。这些不能因为上述批次通过冒称已全部成熟。

## 2026-10-16 四批增量验收追记（具体结果以GitHub对应commit CI为准）

| 主题 | 已添加的真实可执行测试/入口 | 剩余重要缺口 |
|---|---|---|
| Stop与补偿竞态 | `StopWaitsForAdmittedMultiStepCompensation`、`StopRejectsNewCompensationBeforeClaimingEligibility`：获准的多步逆序补偿完成前不停止Worker；停止后的新补偿拒绝且不消耗资格 | 持久补偿步骤账本、部分失败后的安全续补、长时间不协作工作安全隔离接管 |
| 并发容量与取消 | `GlobalFlowQuotaRejectsBeforeAdmission`：`maxActiveFlows`保证准入限制；`ExternalCancellationFreesQueuedCapacity`：外部Token取消排队Work即时退队/返还PendingByte预算 | 可取消的等待式异步背压、公平性及真实执行中内存/GPU预算 |
| 多级子流程 | `NestedOrchestrationRunsAndSkipsCorrectly`：具名多级子流程递归展开到唯一ExecutionPlan，条件入口、出口Join、递归环/超大图拒绝；无隐式数据/资源强行映射 | CxNode正式编排Aspect适配、多节点循环及跨层参数映射、持久活动图状态 |
| 运行隔离 | `RoleBasedSessionGatewayDeniesUnauthorizedActions`：只有Host注入`IMRKRuntimeAccessPolicy`的入口可以公开给外部使用，分别要求Observe/Start/Debug/Cancel/Stop/Compensate权限 | 真实用户身份认证、跨进程授权和Production物理安全确认；不得把模拟principal视为安全证明 |
| 持久Host恢复扫描 | `AllFlowRecoveryInventoryFailsClosed`：有已结案、在途和孤儿终态的混合Journal时分Flow判定；Runtime.Host在运行前拒绝任何未安全结案旧Flow | 完整Checkpoint、可恢复工作快照/数据快照、外部设备Owner再建账、授权续运行 |
| Windows完整构建 | `config-l1-ci.yml`在Release全量重建前恢复TestCenter NUnit包，保留FoundationSmoke/ArchitectureSmoke/CLI真实验收 | 独立TestCenter NUnit Engine回归须按每次Git SHA查看，不从其他成功工作流推断通过 |

**版本状态规则**：上述均指内存/单宿主基础实现，不代表E01～E18全覆盖。数据运行时DataSpace/Buffer是Foundation.Data与Mechanism.Data唯一Owner；Runtime不得建立平行数据管理。此文档是差距矩阵，必须结合master代码与GitHub Actions结果使用。
