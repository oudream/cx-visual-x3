# CxVisual 统一运行体系：从六角色期望倒推测试与最小实现（TDD V0.1）

> **性质**：实现/测试事实 + 继续深化的验收清单。需求来源：[统一运行体系综合讨论稿](../Architecture/CxVisual_统一运行体系_六角色期望与并发异步编排执行_综合设计讨论稿_V0.1.md)。  
> **范围**：现有 \`Foundation/Infrastructure/Concurrency/\`（F19）+ \`Mechanism/Runtime/Execution/\`（执行引擎），在现有FoundationSmoke/ArchitectureSmoke测试工程扩展。不新增项目、第二套编排模型或独立进程。  
> **状态**：首个真实运行闭环；源代码/测试用例存在不等于全部E01～E18验收完毕。Windows x64 CI应以**本次提交对应工作流的实际结论**作为编译和自动测试证据；实机、负载、跨进程、WPF交互未由这些测试覆盖。  
> **边界**：属于可以继续优化的分阶段实现，**不是把V6.4全部候选API或设备安全机制一并定案**。

## 00. 本次TDD路线与阶段范围

从六角色期望倒推：R1软件开发者提交/等待/取消；R2专业Provider仅实现\`Func<CancellationToken,Task<T>>\`；R3工程编排用户的基本DAG连线与All汇合；R4调试人员查看Work/Flow状态；R5操作员观察明确的失败/超时事实；R6平台开发者保持两个源码集中目录、没有框架复制。

**先红后绿的测试驱动目标**（实际验收入口如下），核心是10个独立功能测试方法，纳入既有Windows CI必过门禁；\`Tests/FoundationSmoke/Program.cs --concurrency\`单独运行F19测试，不依赖其他仍有已知资源竞争风险的F16同步日志测试；ArchitectureSmoke仍整体执行。

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
| R3 工程编排 | 顺序依赖、并行工作、All汇合 | ParallelJoinAndResultContext | Studio可视画布、循环、子流程、Any/FirstSuccess |
| R4 工程调试 | 任务状态、工作统计、Flow/Step快照 | KeyedSerialAndAsyncWaiting、ParallelJoinAndResultContext | 单步断点、Trace关联、实时节点高亮 |
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

## 04. E01～E18需求覆盖矩阵（不可高估完成率）

| ID | 本次状态 | 已覆盖或待补内容 |
|---|---|---|
| E01 提交 | **基础已覆盖** | 类型化Handle/接纳状态；尚无跨进程发布 |
| E02 顺序 | **基础已覆盖** | DAG依赖；暂无条件/循环语义 |
| E03 并发 | **基础已覆盖** | 并发上限/分支；无线程亲和 |
| E04 异步 | **基础已覆盖** | Task非阻塞等待；IO协议属于M14 |
| E05 执行组 | **部分** | 同Key串行、不同Key并发；无WholeFlow/Stage选择器 |
| E06 背压 | **部分** | 数量容量、拒绝、可丢旧项；无字节预算及动态优先级 |
| E07 数据交接 | **部分** | Flow输入快照和按步结果；未实现大图像跨线程所有权/租约 |
| E08 汇合 | **部分** | All依赖完成；Any/FirstSuccess、剩余分支清理待设计 |
| E09 超时/取消 | **部分** | 本地等待超时不误杀、Token协作取消；无外部命令结果确认 |
| E10 互斥资源 | **未实现** | Runtime设备租约、专属资源安全仲裁 |
| E11 异常恢复 | **部分** | 可观察失败，依赖节点不执行；重试/补偿/人工恢复待设计 |
| E12 生命周期 | **部分** | Worker安全停止接单、排空/清理队列；Runtime计划切换未做 |
| E13 可观察 | **部分** | Work状态、Worker队列计数、Step/Flow快照；缺Trace/调试UI |
| E14 子流程 | **未实现** | 嵌套、参数绑定、生命周期 |
| E15 事件定时 | **未实现** | 设备事件/Timer驱动等待、周期执行 |
| E16 调试重放 | **未实现** | 断点、单步、数据录制、可控重放 |
| E17 发布计划 | **未实现** | 当前仅有内存DAG校验；Draft→Publish及安全切换仍归独立生命周期机制 |
| E18 策略扩展 | **未实现** | Dedicated/Shared/External策略、设备SDK亲和性 |

**注意：** “基础已覆盖”仅意味着当前表列出的基本行为测试有代码，且CI成功时可证明这些用例通过；不能推断长期稳定/现场正确。

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

这些是**下一轮TDD要先写出的失败用例**，不是当前已提交的可运行测试，也不要求新建10个独立Manager。真实接口应放回M09/M12/M14/M15/M26/M28/M29/M30/M31/M36等唯一Owner内；**执行实现仍主要收敛在两个目录**，不做跨项目反向依赖。

## 06. 构建、性能和安全验收门槛

### 06.1 自动化测试层级

- **G0编译与结构**：.NET48传统csproj显式Compile Include；引用仅Runtime→F19；no-WPF Foundation/Runtime；无新的常驻进程或Graph树。
- **G1基础行为**：T01～T10必须实际运行通过；CI保持Foundation/F19和Architecture两道独立失败门禁。
- **G2压力/竞态**：高并发随机Key、百万级提交压测（内存和时延预算经实测制定）、重复取消/Stop竞争、队列饱和和丢弃通知、长时间资源检查。
- **G3专业模拟**：Vision双产线真实积压业务报告、IoT协议会话异步应答、AI跨进程超时及迟到。
- **G4实机/现场**：PLC/运控/X-Ray联锁、紧急停止、设备安全状态及数据/历史正确性。

G0/G1通过不能代替G2/G3/G4。CI工作流故障时应分清本次新增测试与旧F16文件锁等独立问题，不得将非相关基础故障静默归咎于新F19。

### 06.2 源码与工程依赖

- \`Foundation/Infrastructure/CxVisual.Foundation.Infrastructure.csproj\` 显式纳入两个F19源文件。
- \`Mechanism/Runtime/CxVisual.Mechanism.Runtime.csproj\` 显式纳入两个Execution文件，并新增**单向** \`Foundation.Infrastructure\` 项目引用。
- FoundationSmoke新增FCRWorkerTests，ArchitectureSmoke新增ExecutionFlowTests（已有项目，无新csproj）。
- Windows工作流将\`FoundationSmoke.exe --concurrency\`作为必须成功的测试；旧F16日志全量Smoke继续作为单独诊断。
- 不更动Professional、Orchestration、Studio的代码接口与运行路径；将来通过明确能力适配接入。

## 07. 实施与沟通规范

1. 本文保留测试驱动架构的**需求与证据映射**，不覆盖先前的六角色需求讨论稿。
2. 后续每轮只把当前确有实现与成功测试的条目标记“已验证”；新增行为先增加失败测试，再实现，避免空壳类。
3. 当实现新增执行类/API时继续审V6.4候选与旧V7.8完整签名；如发现冲突先迁移/统一Owner。
4. 真实编排、跨进程、安全联锁不做假通过；测试、现场条件与失败原因应可复查。
5. 更新本档案路径或状态后同步检查\`README.md\`、\`AGENTS.md\`、\`DOCS/00\`入口。尽量一项功能对应少数小文件和一次可审查提交。
