# CxVisual 流程执行体系——三方面完整需求、双角色使用与全类架构 V2.0

> **文档类型**：正式设计评审工作稿（完整功能需求 + 使用体验 + 全类归属设计），不是功能已交付声明，更不是自动生成代码的授权。  
> **评审基线**：2026-10-19；仓库 `oudream/cx-visual-x3`，本次源级审查快照 `b8daf3847b9c0241b3d511ba012676446539d400`；原需求清单 V1.0 固定源 `0440790ef04cabbd18f6756d9e9e71b192ab649c`。  
> **技术约束**：C# 7.3、.NET Framework 4.8、WPF；Foundation / Mechanism / Professional 三方面与既有项目总体结构不变；MRK 唯一流程执行引擎；直接复用 F19、MEM/MOL、MOC、MDS、TestCenter。  
> **设计原则**：**各方面尽能力展现，便捷友好支撑各专业达成目标**。同一能力契约、同一执行语义、同一真实运行事实；常见情况最简，复杂情况有完整高级能力。  
> **引用原则**：`【实】`源码已有（不自动等于验证通过）、`【基】`已有局部机制但目标不足、`【设】`建议设计未实现、`【协】`他体系 Owner、`【待核】`未获得足够源码/CI/实机证据。区分**现有类名**、**已有行为**和**未来目标 API**。

---

## 阅读导航

- **第 1～4 章**：原文继承、边界、三方面职责和完整功能细目（保持 E01～E18、199 子项）。
- **第 5～7 章**：软件开发者期望、真实 C# 代码、工程编排用户完整操作和一套语义的验收。
- **第 8～10 章**：从需求反推全类架构：现有核心清单、必需增构、接口/方法与类依赖。
- **第 11～14 章**：端到端案例、失败反证、验收矩阵、规划闭环和来源索引。

## 0. 先定目标与非目标

**目标**：让专业能力和工程逻辑以**明确的依赖关系、确定的控制语义、合理的执行调度、可信的过程与结果**真正执行；执行能观察、停止、错误结算、按安全规则处理故障。软件开发者写少量自然 C#，工程用户在同一张流程图上选择能力、连线、配置、执行和定位错误。

**主责三方面（只设这三个）**：

1. **执行控制（Execution Control）**：执行哪一个步骤、何时分支、何时循环、谁先谁后、子流程如何调用、如何汇合。
2. **执行调度（Execution Scheduling）**：工作何时入队、在哪个 Worker/执行位置运行、并行多少、需要哪些资源、等待如何唤醒。
3. **执行管理（Execution Management）**：运行身份/上下文、状态、生命周期、取消超时、异常重试补偿、观察与恢复事实。

**明确不是本体系的东西**：持久化工程树/Logic 设计归 MEM/MOL；能力目录、编译诊断和 SourceMap 归 MOC；数据值的所有权/Heap/Stack/Frame 归 MDS；Worker/Queue 原语归 F19；WPF 图形编辑归 Visual/Presentation/Studio；正式包部署与可信权限归工程构建/Host；PLC/运动/X-Ray/CT/AI 的专业命令、安全联锁归专业 Provider 与硬件。MRK 负责配合它们，不另建一套同义体系。

**贯穿性约束**：定义 ≠ 已编译计划 ≠ 运行实例；Work ≠ Thread；取消请求 ≠ 已停止；本地超时 ≠ 外部命令未执行；逻辑节点 ≠ WPF 图形节点；`Data.Return` ≠ 已提交。以后所有功能和 API 均以此为底线。

## 1. 原文和四大文档的继承与裁决

| 来源 | 本文实际继承的内容 | 不可以错误推断 |
|---|---|---|
| 原《统一运行体系·六角色期望与并发异步编排执行 V0.1》 | R1～R6、L0～L4、E01～E18、五个核心模型、四种状态、执行组/异步/Join/安全默认行为 | 旧稿中 API 示意不等于现有签名 |
| 原《六角色差距审计与内核增构》（文件名 V0.5，正文 V0.6.1+） | MRK/F19 实现事实、T24～T38、已知薄弱环节、权限/Journal/发布/补偿边界 | 有测试入口不等于已完成安全恢复 |
| 原《开发者简洁入口与编排共源 V0.1》 | MRKCodeRuntimeBuilder → Blueprint → DevelopmentRuntime，以及正式授权发布共用编译语义 | 开发门面不能直接成为生产权限入口 |
| 现《流程执行体系·完整功能需求清单 V1.0》 | **199 条 E 子需求 + 32 条用户体验 + 21 条质量要求 + 10 场景，全部继承且 ID 不变** | 收敛三方面不是删减其他系统参与的功能 |
| **01 平台灵魂与架构宪法 V1.0-Review** | Capability 优先、Definition/Resolve/Compile/Runtime/Provider 分离、工程与生产隔离、Command/State/Event/Stream 与版本追溯 | 其历史 Graph 用语不得导致再造独立 Graph 树/万能 Runtime |
| **02 统一规格基线 V4.9** | Foundation/Mechanism/Professional 及能力/职责分层；此前工程组织审查指出 Mechanism 原 Owner 划分 | 本轮仅核对仓库文件及已有交叉审计记录，**未逐行读取整个 Excel**，不可声称全行一致 |
| **03 三方面逐阶段 V2.01** | 子方面阶段与原归属/复用原则；存量项目优先 | 未逐行读取，待确认原表后方可回填项目状态 |
| **04 Foundation 与 Mechanism 对象 API 规格 V6.4** | M_Core、M_Visual、M_Orchestration、M_Runtime 既有归属体系；查重增构原则 | 不能把表中全部候选对象视为已实现或必须新建 |
| 《统一工程组织体系 V1.1》§7.4、G01～G04 | 同一 Content Logic、MOC 编译、SourceMap、Studio 同图投影、旧 V1 结构化与 V2 自由连线 | 旧稿的“候选”若后续源码已落地，应以当前源码为准；V2 仍不等于完整控制流 |

**约定的冲突裁决顺序**：先遵守三方面/唯一 Owner/唯一对象树，再用最新真实源码区分实现事实；专项成熟契约覆盖历史候选名称；真正调整公共 ABI/依赖须另行 ADR 与实际测试，不靠本文默改四大 Excel。

### 1.1 统一运行模型（逻辑模型，不要求逐一新增类）

`已校验工程定义或代码流程 → MOC/MRK 编译 → 稳定 MRKExecutionPlan → MRKExecutionEngine 运行 Flow → Step / Invocation → F19 Worker 与租约 → Provider 实际行为 → MDS 数据提交 → MRK 运行事实 → Studio/Host 观察或控制`。

必须明确：`PlanId + Revision`、`FlowId`、`StepId`、`InvocationId`（目标：循环/复用时唯一）、`WorkId`、`OperationId`、`CorrelationId`、`SourceAddress/InstancePath` 各有不同用途，不能互换。若 Plan 由代码瞬时构建，不强制额外先创建 MEM 工程；若由 Studio 编辑，定义必须落在现有 MEM Content Logic 中，不另造持久树。

## 2. 三方面定位、相互关系与完整职责

### 2.1 执行控制（EC）——“下一步应该执行谁”

**输入**：通过 MOC/MRK 验证的 Plan、明确的控制流/端口依赖、能力契约及流程输入。**输出**：可执行步骤的激活/跳过/归并决定、子流程调用关系、控制流结果与源节点身份。**不承担**：WPF 画布绘制、持久定义编辑、线程队列细节、数据 Heap 所有权。

| 能力群 | 业务语义 | 完成条件与常见坑 |
|---|---|---|
| EC-01 步骤和依赖 | 顺序、DAG、有条件的依赖、入口与出口 | 前置失败不误执行后继；布局顺序不能隐式当执行顺序 |
| EC-02 条件/分支 | If/ElseIf/Else、Switch/Case、默认支路、短路与错误语义 | 只执行命中支路；未选支有明确 Skipped；不重复外部副作用 |
| EC-03 循环/迭代 | For/While/DoWhile/Foreach、边界上限、每轮值和迭代身份 | 多 Step 循环真正复用 BodyPlan；Break/Continue/Return 正确关闭区域 |
| EC-04 并行控制 | Fork/Join、All/Any/FirstSuccess/ConditionalJoin、剩余分支结算 | 谁先胜出、剩余任务怎么处理、失败/Skipped 如何汇合均确定 |
| EC-05 多层子流程 | 主/子/子子 Content 或瞬时 Flow、递归保护、参数/结果契约 | 同一子流程多次引用互不串数据；真实公开端口与实例路径可追溯 |
| EC-06 异常区控制 | Try/Catch/Finally、失败出口、业务补偿分支、区域退出 | finally 和取消策略不矛盾；不要用 Catch 掩盖未知外部动作 |
| EC-07 事件/等待逻辑 | Await/Wait/Delay/Trigger 对流程依赖的推进 | 不忙等、超时/取消可辨、唤醒不重复激活 |
| EC-08 数据控制边界 | 绑定到哪一输出/输入、什么时候可读已提交值 | 由 MDS 管实际存储；未提交 Pending 不对后继暴露 |

**层次切分**：MOL 描述 If/Loop 等持久逻辑；MOC 将工程定义投影成编译输入及 SourceMap；MRK 对**已编译**的 Region/Step 做实际控制决策。旧 V1 有界静态展开是过渡实现，不是大循环最终形态。V2 自由连线当前偏 Action DAG；二者语义要收敛，但不能强行“把所有专业都解释成单个万能 Graph”。

### 2.2 执行调度（ES）——“怎样高效且安全地执行”

**输入**：EC 产生的可就绪 Work、能力执行约束、资源需求、优先级和限额。**输出**：接受/拒绝/等待/派发、任务执行位置、资源持有与释放、吞吐与队列观察。**不承担**：工程编排语义与专业硬件物理联锁。

| 能力群 | 关键规则 | 高级场景 |
|---|---|---|
| ES-01 提交与容量 | 提交、受理、拒绝、排队、可取消等待、明确 WorkId | 拥塞时拒绝或受界限的背压，绝不无界堆积 |
| ES-02 同步与真正异步 | 普通 Action/Func、Task、IO、SDK 同步入口分别适配 | `Task.Run(阻塞SDK)` 不冒充异步 IO；Waiting 不应长期占 Worker |
| ES-03 并行配额 | 并发上限、优先级、饥饿防护、公平性、动态负载 | 同产线的顺序约束和两产线吞吐同时保持 |
| ES-04 执行组 | WholeFlow、Stage、Resource 级串行与限流 | 不要用“整条线一个全局 Lock”扼杀流水并行 |
| ES-05 队列背压 | 容量、字节预算、Reject/Drop（仅允许被声明可淘汰项） | 可淘汰帧有审计与产品结果，控制命令不得静默丢失 |
| ES-06 资源仲裁 | 设备/卡/通道/GPU 的独占、共享、租约顺序与等待取消 | 超时但外部设备未确认时不能提前释放所有权 |
| ES-07 执行位置 | ThreadPool、STA/MTA 专用线程、外部服务、亲和限制 | Vendor SDK 必须在正确执行位置；物理 CPU/GPU 策略须单列验收 |
| ES-08 时间与事件 | Delay、Periodic、外部应答唤醒、Correlation 去重 | IoT 同 Session 等待释放线程但保留协议逻辑顺序 |
| ES-09 调度健康 | QueueDepth、WaitReason、Worker/Group 配额、资源等待与吞吐 | 可定位饥饿、积压、预算耗尽及长期泄漏 |

**Owner**：F19 提供 Worker/Queue/Gate/Timer/Dedicated 的**基础原语**；MRK 提供懂 Flow/Step/Group/Resource 的**业务调度与租约协调**。不可在 MRK 重做另一个通用 Worker，也不可让 F19 理解 Content、专业设备命令或数据业务含义。

### 2.3 执行管理（EM）——“运行中发生了什么、如何控制与恢复”

**输入**：Flow/Work/Operation 执行事实、控制请求、Plan Revision、Host/Provider 权威确认。**输出**：生命周期、状态、结果、错误诊断、审计/Trace、受控的取消/恢复行为。

| 能力群 | 关键规则 | 验收重点 |
|---|---|---|
| EM-01 身份上下文 | Plan/Flow/Work/Invocation/Correlation/Operation；Run 元数据 | 两产线同名节点、三层子流程不串账 |
| EM-02 生命周期 | 接单、运行、等待、Drain/Stop、关闭与资源释放 | Stop 不等于直接丢弃在途工作；资源无泄漏 |
| EM-03 状态与结果 | Accepted/Queued/Running/Waiting/Skipped/Completed/Failed/Cancelled/Unknown | 状态与结果相符，失败不能以成功 Task 冒充成功业务 |
| EM-04 控制请求 | Pause/Resume/Step/RequestCancel/Stop；逻辑安全点 | 授权、取消时序、避免在设备动作中强制终止线程 |
| EM-05 错误与策略 | Exception、Timeout、Retry(仅安全幂等)、Compensate、Manual | 非幂等命令不得因本地超时自动重发 |
| EM-06 观察/定位 | Step/Flow Snapshot、Trace 序号、SourceMap 追溯、等待原因 | Trace Gap 后重同步；日志不能成为大数据搬运管线 |
| EM-07 持久事实 | Journal、OperationId、因果顺序、断帧检测、未知判定 | 现有可保守检测，但不等于完整 Checkpoint 恢复 |
| EM-08 安全恢复 | 执行检查点、重启判定、Provider/人工核对、有限授权续跑 | 可证明安全的步骤才恢复；设备 Unknown 必须隔离确认 |
| EM-09 计划固定/切换 | 运行固定 Revision、授权 Stage/Activate/Rollback 与安全点 | 发布包由构建/Host 提供；MRK 不直接热改运行中的 Plan |
| EM-10 多环境治理 | 开发模拟、生产运行、只读观察、受授权调试 | Studio 断开不影响 Host；不暴露可越权的原始 Handle |

**Owner**：MRK 管执行身份与执行事实；MDS 管数据版本/Frame/持有；Host 管运行承载、可信凭证与跨进程进出；Studio 只消费观察契约并经授权请求控制。现有 `MRKJournalRecovery` 对无终态/Unknown 保守判定需核对——不是“能够任意崩溃续跑”。

### 2.4 三方面之间的执行关系

```text
            MOC 校验后的 ExecutionPlan + SourceMap
                             |
                             v
             EC 执行控制 ——选出就绪 Work/Region
                             |
                             v
             ES 执行调度 ——F19派发/等待/租约
                             |
                             v
              Provider/专业工作 ——获得真实结果
                             |
                  +----------+-----------+
                  |                      |
                  v                      v
             MDS 数据提交           EM 状态与审计
                  |                      |
                  +-----------+----------+
                              |
                              v
                    EC 推进后继/结束流程
```

**三方面相互协作，不是三个独立执行引擎或三个新增项目**；每次激活的控制决策只结算一次，每次 Work 激活的调度只能由 MRK→F19 提交一次，失败与资源释放必须能够确定终态。

## 3. 全量功能索引：原 E01～E18 映射到三个方面（不删减）

这里采用**一个主责 + 可选协作**：同一 E 功能群不因为跨方面就复制对象。后面保留 199 个原有 `E##-##` 标识、要求、验收边界与旧状态。

| 原ID | 原能力 | 主方面 | 协作 Owner / 方面 | 细项数 |
| --- | --- | --- | --- | --- |
| E01 | 任务提交与接纳 | ES | EM | 10 |
| E02 | 顺序、依赖与结构化控制 | EC | MOL/MOC | 14 |
| E03 | 并发、并行与执行公平性 | ES | EC | 10 |
| E04 | 异步、外部等待与恢复唤醒 | ES | EC/Provider | 10 |
| E05 | 执行组与串行范围 | ES | EC | 10 |
| E06 | 队列、容量与背压 | ES | F19 | 10 |
| E07 | 运行数据交接与端口契约 | EC | MDS/EM | 13 |
| E08 | 分支汇合与尾部结算 | EC | ES/EM | 10 |
| E09 | 暂停、恢复、取消与超时 | EM | ES/Provider | 11 |
| E10 | 资源租约与执行仲裁 | ES | Host/Provider | 10 |
| E11 | 异常处理、重试、补偿与恢复 | EM | EC/Host/Provider | 13 |
| E12 | 执行会话、生命周期与长时间运行 | EM | ES/Host | 11 |
| E13 | 状态、快照、Trace 与运行观察 | EM | Studio/MOC | 13 |
| E14 | 子流程、调用实例与复用 | EC | MOC/MDS | 12 |
| E15 | 事件、触发与定时协作 | ES | EC/Event Owner | 9 |
| E16 | 断点、单步、仿真与重放 | EM | Studio/Simulation | 12 |
| E17 | 版本固定、发布与安全切换 | EM | MOC/Host | 11 |
| E18 | 执行策略、位置与扩展 | ES | F19/Provider | 10 |

**说明**：E07 数据交接是“执行控制对端口/提交的消费”与 MDS 的联合需求；E17 版本切换是“执行管理执行中固定 Plan”与工程构建/Host 的联合需求。它们的用户目标不能删除，但不能因此在 MRK 新建第二数据管理体系或发行仓库。

### 3.1 执行控制 EC——逐项需求与可验证行为（来源原清单）

#### E02 · 顺序、依赖与结构化控制（14 项）
**功能意图：**执行先后由明确依赖与逻辑条件决定，工程树位置不代表执行顺序。  **责任：**MRK；逻辑定义 MOC/MOL。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E02-01 | 顺序步骤 A→B 按完成依赖推进 | B 不早于 A 完成执行 | 基 |
| E02-02 | 无前置依赖步骤可独立安排 | 调度与可视布局无关 | 基 |
| E02-03 | 依赖失败/取消/跳过时传播清晰 | 后继不误执行且理由可追踪 | 基 |
| E02-04 | If/ElseIf/Else 互斥选择及默认分支 | 条件求值仅进入命中分支，未选支 Skipped | 部 |
| E02-05 | Switch/Case/Default 类型或表达式分派 | 多个候选规则顺序、默认与冲突可验证 | 待 |
| E02-06 | 结构化 For/While/DoWhile 重复执行多个步骤 | 循环体可逐轮重新激活，不依赖固定静态展开 | 待 |
| E02-07 | Foreach 顺序迭代、集合项绑定 | 不同迭代有独立结果和身份 | 部 |
| E02-08 | Foreach 有界并行执行 | 可限制并发、排队及结束时清理未完成项 | 待 |
| E02-09 | Break/Continue/Return 按区域和调用层级生效 | 不误中断兄弟流程或遗漏清理 | 待 |
| E02-10 | Try/Catch/Finally 能处理流程级异常 | 异常区域、恢复路径、Finally 执行条件确定 | 待 |
| E02-11 | Wait / Await 作为结构化控制而非忙等 | 节点正确进入 Waiting、被唤醒或终止 | 部 |
| E02-12 | 统一出口和结构化 Region 行为 | 不能靠在 DAG 画返回边实现循环 | 待 |
| E02-13 | 旧 V1 结构化逻辑与 V2 自由连线目标语义一致 | 可构建同一可比较执行事实，转换不丢功能 | 待 |
| E02-14 | 表达式有类型、缺失值与错误处理规则 | 非法表达式在构建阶段定位 | 部 |

#### E07 · 运行数据交接与端口契约（13 项）
**功能意图：**执行层传递的只是正确数据事实，Heap/Stack/Buffer 的权威归 MDS。  **责任：**MDS 主；MRK/MOC 协同。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E07-01 | 流程可以声明类型化输入和输出端口 | 缺失、错类型在构建或执行阶段给出清晰错误 | 基 |
| E07-02 | Step 可以读取上游具名输出或 FlowInput | 连线配置和代码绑定结果相同 | 部 |
| E07-03 | 节点普通数据提供 Get/Set/Return 使用方式 | 开发者无需手工 Push/Pop | 基 |
| E07-04 | 输出先暂存，成功后才提交给下游 | 失败分支不读到半提交输出 | 部 |
| E07-05 | 子流程具有明确参数和返回映射 | 调用者只关心入参、出参，不画数据栈节点 | 部 |
| E07-06 | 主/子/子子流程数据作用域独立 | 并行调用不能共享错局部值 | 部 |
| E07-07 | 流程具有独立 FlowHeap/Stack/Frame | 同名 Key 不跨 Flow 偷共享实例 | 基 |
| E07-08 | 数据复制、移动、只读共享语义确定 | 并行算法修改不能污染原始图像 | 部 |
| E07-09 | 大型 Image/Volume/Tensor 按引用或文件定位交接 | 不能默认深复制海量数据 | 部 |
| E07-10 | 异步外部操作迟到时数据不得提前释放 | 需要安全租约、超时与回收约束 | 待 |
| E07-11 | 跨进程 SHM/GPU 缓冲区具有明确所有权 | 取消/崩溃/重试不悬空或二次回收 | 待 |
| E07-12 | 可配置数据来源：常量、FlowInput、上游端口、变量、配置 | 前端统一选择器并验证类型与寿命 | 部 |
| E07-13 | 运行中的节点可以按授权检查输入/局部/输出/Heap 元数据 | 大数据默认只查看摘要不拷贝全部内容 | 部 |

#### E08 · 分支汇合与尾部结算（10 项）
**功能意图：**Join 的成功不等于其他分支或外部动作已经安全终止。  **责任：**MRK；MDS 协作。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E08-01 | 支持等待全部分支完成 WaitAll | 结果顺序和失败传播明确 | 基 |
| E08-02 | 支持先完成分支 WaitAny | 未胜出分支处理策略有明确选项 | 基 |
| E08-03 | 支持第一个成功 FirstSuccess | 全失败时生成可解释失败结果 | 基 |
| E08-04 | 支持条件满足才合并 ConditionalJoin | 依赖值、跳过、重复事件处理确定 | 部 |
| E08-05 | 失败分支与 Skipped 不被混为同一种异常 | 汇合不会因跳过误死锁 | 部 |
| E08-06 | 某个分支先完成后，可显式继续、取消或等待尾部 | 尾部任务不能意外写入完成流程 | 部 |
| E08-07 | Late Result 属于原 Flow/Invocation | 迟到结果不会写回同名新实例 | 部 |
| E08-08 | 汇合后的数据提交遵循一致性策略 | 多生产者同时写入同一槽位需显式聚合 | 待 |
| E08-09 | 超时/取消 Join 后资源仍有安全交接 | 外部未知操作保持可核对事实 | 部 |
| E08-10 | 跨子流程/并行 Foreach 的归并路径有独立身份 | 能定位哪一次调用/迭代的结果 | 待 |

#### E14 · 子流程、调用实例与复用（12 项）
**功能意图：**开发者和编排用户复用同一 Content/Flow，只指定公开参数与返回，不操作栈。  **责任：**MOC/MOL 定义；MRK 调用执行；MDS 数据。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E14-01 | 支持主/子/子子流程调用 | 三级及以上调用具备唯一来源路径 | 基 |
| E14-02 | 子流程引用已存在的工程 Content | 不复制独立业务定义 | 部 |
| E14-03 | 代码 Blueprint 可引用已有子流程声明 | 冻结后与其它流程共享能力解析 | 基 |
| E14-04 | 每次子流程调用有独立 Invocation 身份 | 相同定义多次并行调用不串结果 | 待 |
| E14-05 | 子流程输入/输出有类型化公开端口 | 缺少/错误绑定在编译时诊断 | 部 |
| E14-06 | 父输入到子输入、子输出到父输出显式映射 | 不能靠偷偷读取父 Local 传参 | 部 |
| E14-07 | 递归/循环引用检测及最大调用深度 | 错误定义不启动 Worker | 基 |
| E14-08 | 调用点可传递适用的执行策略、资源与预算 | 不支持项编译拒绝而非静默丢弃 | 部 |
| E14-09 | 提取选中线性控制段为子 Content | 一次工程事务替换调用且保存重开 | 部 |
| E14-10 | 支持有数据边界、复杂分支的子流程提取 | 端口自动推导且用户可复核 | 待 |
| E14-11 | 跨专业子流程调用共享公共契约 | 专业 Provider 由目标 Workbench 实现 | 待 |
| E14-12 | 调试时可进入子流程再返回父调用点 | 同名子节点来源、帧和数据准确 | 待 |

### 3.2 执行调度 ES——逐项需求与可验证行为（来源原清单)

#### E01 · 任务提交与接纳（10 项）
**功能意图：**一个真实提交必须被接受、明确拒绝或按策略等待，绝不悄悄消失。  **责任：**MRK + F19。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E01-01 | 支持提交普通同步计算或真正异步任务 | 开发者无需自行创建线程，获取明确返回或句柄 | 基 |
| E01-02 | 一次提交可标识 WorkId、FlowId、业务键及关联信息 | 相同业务请求与不同实例不会混淆 | 部 |
| E01-03 | 任务接纳与真正开始运行分开表示 | 队列内工作不误标 Running | 基 |
| E01-04 | 容量不足时明确拒绝，返回可区分错误 | 没有“调用成功但任务未入队”的静默情况 | 基 |
| E01-05 | 支持可取消的有界等待接纳 | 队列拥塞时可等待或取消，内存不无限膨胀 | 部 |
| E01-06 | 支持提交任务的优先级和业务标签 | 查询得到接受原因、执行阶段和关联来源 | 部 |
| E01-07 | 同步调用便捷入口与异步结果入口并存 | 常用业务操作不要求先画 Flow | 部 |
| E01-08 | 重复/重投提交必须声明幂等及去重边界 | 异常重投不能引起危险副作用重复执行 | 待 |
| E01-09 | 同一个应用复用既有 Worker 和执行引擎 | 不因每个业务建立独立线程池 | 基 |
| E01-10 | 单任务失败也能获得失败事实 | 非观察型 Fire-and-forget 异常不静默 | 部 |

#### E03 · 并发、并行与执行公平性（10 项）
**功能意图：**并发是按能力声明和预算组织的受控行为，不等于任意创建线程。  **责任：**MRK + F19。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E03-01 | 独立步骤能并发执行 | 多个无依赖节点可同时推进 | 基 |
| E03-02 | 能为执行组和 Worker 设置最大并发 | 并发峰值不突破配额 | 基 |
| E03-03 | 可按资源、能力、流程设置并行边界 | 非线程安全 SDK 不被错误并发调用 | 部 |
| E03-04 | 并行分支有隔离上下文 | 分支私有结果不会相互覆盖 | 部 |
| E03-05 | 并行任务支持取消、异常及加入结算 | 分支失败后剩余任务按规则处理 | 部 |
| E03-06 | 同 Key 顺序与不同 Key 并发兼容 | 同产线串行不强行锁住其他产线 | 基 |
| E03-07 | 长期高优先级负载下公平性可保证或明确策略 | 低优先级不发生无限饥饿 | 待 |
| E03-08 | 并发执行不阻塞 WPF 主线程 | Studio 操作仍响应 | 部 |
| E03-09 | 并行度可由能力默认值与工程覆盖共同决定 | 覆盖遵守能力的最严格线程安全约束 | 待 |
| E03-10 | 并行调度具有可观测指标 | 能定位线程池耗尽、待队列、配额等待 | 部 |

#### E04 · 异步、外部等待与恢复唤醒（10 项）
**功能意图：**真正 IO/事件等待要释放不必要的工作线程，但保留正确的业务顺序。  **责任：**MRK + F19；Provider/Connectivity。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E04-01 | 可直接消费 Task 异步能力 | 等待任务不强制占用 Dedicated Thread | 基 |
| E04-02 | 支持设备请求-异步应答的 Correlation | 快速到达的应答也能准确匹配 | 部 |
| E04-03 | 外部事件/应答匹配发生在发送前登记 | 避免响应先到时丢失 | 部 |
| E04-04 | 等待超时、取消与应答成功三方竞争可确定结算 | 仅允许一个有效结果 | 部 |
| E04-05 | 等待状态与保留资源/逻辑槽位分离 | 能释放线程但保持协议次序 | 部 |
| E04-06 | 支持一个执行流程中多个非阻塞等待点 | 独立等待不会共享错 Correlation | 部 |
| E04-07 | 异步回调发生异常后转为结果和 Trace | 不会悬挂在永久 Running | 部 |
| E04-08 | 对无协作取消的外部任务记录未知执行状态 | 超时后不宣称外部操作停止 | 部 |
| E04-09 | 长等待跨进程重连或 Host 重启后的继续 | 满足身份、持久记录、重新订阅与安全确认 | 待 |
| E04-10 | 严格区分 Task.Run 包装与真正异步 IO | 配置界面不承诺同步 SDK 变为非阻塞协议 | 协 |

#### E05 · 执行组与串行范围（10 项）
**功能意图：**串行必须明示是整流程、阶段还是特定资源，不使用全局锁糊住整机。  **责任：**MRK + F19。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E05-01 | 提供按 Key 串行执行组 | 相同 Key 保序、不同行为键可并发 | 基 |
| E05-02 | 支持组级最大并发与等待队列容量 | 超额等待/拒绝具有可见原因 | 基 |
| E05-03 | 组约束能够作用到指定步骤 | 同一阶段的采图受控，其后 AI 仍可并行 | 部 |
| E05-04 | 整流程串行范围可表达与执行 | 多个产品整条流程严格有序 | 待 |
| E05-05 | 阶段级串行范围可表达与执行 | 指定 Stage 顺序且其他 Stage 能重叠 | 待 |
| E05-06 | 资源级串行与执行组可组合 | 设备独占与业务排序不互相矛盾 | 部 |
| E05-07 | 多资源申请次序明确、避免死锁 | 形成可测试的固定顺序或失败回退 | 部 |
| E05-08 | 组等待取消时及时撤销资格 | 预算与队列计数全部返还 | 部 |
| E05-09 | 组配额、持有者和等待列表可观察 | 调试者能解释为何排队 | 部 |
| E05-10 | 组规则随版本发布固定，不受草稿修改影响 | 运行中的旧 Flow 不混入新策略 | 待 |

#### E06 · 队列、容量与背压（10 项）
**功能意图：**高吞吐不能依赖无限队列；数据体积和任务个数都要算账。  **责任：**F19 主；MRK 协同。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E06-01 | 有界任务队列与容量拒绝 | 满队列可得到明确理由 | 基 |
| E06-02 | 允许声明 FIFO 与优先级策略 | 可重复验证出队顺序 | 基 |
| E06-03 | 有明确的等待/拒绝/淘汰策略 | 不提供无声丢弃默认值 | 部 |
| E06-04 | 仅未开始且明确可淘汰的工作允许 Drop | 已经运行的设备指令不会被 DropOldest | 部 |
| E06-05 | 被淘汰工作有业务可见结果和审计 | 图像对应产品不会被默判成功 | 待 |
| E06-06 | 提交容量同时支持预计字节预算 | 大图任务按估算大小拒绝或等待 | 部 |
| E06-07 | 多阶段/全流程数据预算统一归属 MDS | 不能让队列与 Heap 各自无限增长 | 协 |
| E06-08 | 提供生产者可取消的异步背压等待 | 等待不占业务 Worker，取消立即结算 | 部 |
| E06-09 | 任务结束、取消、淘汰后预算归零 | 无漏计费和重复释放 | 部 |
| E06-10 | 统计拒绝、丢弃、峰值占用与等待时长 | 可分析 UPH/吞吐瓶颈 | 部 |

#### E10 · 资源租约与执行仲裁（10 项）
**功能意图：**每次设备、SDK、GPU、内存/共享对象占用都要有可解释 Owner。  **责任：**MRK；专业 Provider/Host。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E10-01 | 独占资源不能被两个运行节点同时获得 | 冲突任务明确等待或拒绝 | 基 |
| E10-02 | 支持声明节点 RequiredResources | 流程不直接通过散落 lock 控制设备 | 基 |
| E10-03 | 资源租约与组级约束联合生效 | 不会绕过串行 Key 抢占设备 | 部 |
| E10-04 | 等待资源期间可取消 | 取消后能释放请求和预算 | 基 |
| E10-05 | 支持可安全共享的只读资源策略 | 多个消费者同时读不会造成写冲突 | 部 |
| E10-06 | 不同资源按稳定顺序获取 | 避免 ABBA 型死锁 | 部 |
| E10-07 | 资源租约跨多阶段传递有确定释放点 | 完成、失败、补偿、取消都能清理 | 部 |
| E10-08 | 远程 Host 是生产权限与会话最终协调者 | Studio 不直接拥有实际设备操作权 | 待 |
| E10-09 | 跨进程资源 Owner、主备接管与 fencing | 崩溃恢复不双重控制设备 | 待 |
| E10-10 | 真实安全联锁由设备/安全控制系统执行 | 软件租约不能代替硬件急停 | 协 |

#### E15 · 事件、触发与定时协作（9 项）
**功能意图：**MRK 处理等待/触发执行，事件总线及可靠投递属于既有事件机制。  **责任：**MRK + F19；事件系统另有 Owner。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E15-01 | 支持手动触发 Flow | 触发与 FlowId/Result 关联 | 基 |
| E15-02 | 支持周期触发且防止意外重入 | 上一轮未完成时策略确定 | 部 |
| E15-03 | 支持延迟、计时与受取消的等待 | 取消后 Timer 不泄漏 | 基 |
| E15-04 | 可由 Event/Command 消息唤醒流程 | 不需要为每种事件新建执行器 | 协 |
| E15-05 | 事件与外部应答有唯一 Correlation | 重复和迟到可被识别 | 部 |
| E15-06 | 可靠事件交付具有顺序、去重与持久化语义 | 断线/重启不能无声丢失关键命令 | 协 |
| E15-07 | 可配置事件过滤与触发条件 | 无效表达式/类型被明确拒绝 | 待 |
| E15-08 | 跨进程时间基准及误差在场景中明确 | 不得将 PC 定时当作硬实时控制 | 协 |
| E15-09 | 人工等待/审批步骤能持久挂起并恢复 | 审批完成后准确恢复原 Flow | 待 |

#### E18 · 执行策略、位置与扩展（10 项）
**功能意图：**高级选项可按需配置，不逼迫所有开发者理解 SDK 线程、资源和队列。  **责任：**MRK + F19；专业 Provider。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E18-01 | 同步/异步、串行/并行、优先级为正交选项 | 不会被混成巨大执行模式枚举 | 部 |
| E18-02 | 策略默认取自能力声明 | 普通用户不重复配置所有策略 | 部 |
| E18-03 | 允许按调用点明确覆盖可覆盖策略 | 越权或违背能力约束的配置被拒绝 | 待 |
| E18-04 | 支持共享 WorkerPool 执行位置 | 普通任务无需独占线程 | 基 |
| E18-05 | 支持专用 STA/MTA 同步 SDK 线程 | 实际执行线程符合线程亲和要求 | 基 |
| E18-06 | 支持 External Service 异步执行 | 返回结果、取消、OperationId 可关联 | 部 |
| E18-07 | GPU/CPU/硬件加速执行位置可表达 | 资源限制和结果契约一致 | 待 |
| E18-08 | 支持运行策略组合校验 | 资源/Group/优先级/配额冲突时有明确诊断 | 部 |
| E18-09 | 支持不同业务配置性能预算与超时 | 默认不是隐式无限排队/无限等待 | 部 |
| E18-10 | 性能/公平性变化可通过回归比较 | 不同策略可测吞吐、分配、延时与失败行为 | 待 |

### 3.3 执行管理 EM——逐项需求与可验证行为（来源原清单）

#### E09 · 暂停、恢复、取消与超时（11 项）
**功能意图：**把请求、观察事实、外部设备最终确认严格分开。  **责任：**MRK；Provider/Host 最终确认。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E09-01 | 执行中请求取消可传递 CancellationToken | 已协作节点及时停止、等待结算 | 基 |
| E09-02 | CancelRequested 与 Cancelled 明确区分 | 请求送达不假设立即取消成功 | 基 |
| E09-03 | 等待队列的任务取消立即摘除 | 队列、预算和票据正确释放 | 基 |
| E09-04 | 取消在断点/资源/事件等待过程中有效 | 不会永远卡在 Wait | 部 |
| E09-05 | 可设置节点与流程超时策略 | 超时错误码/发生阶段可观察 | 部 |
| E09-06 | 超时任务非协作运行时以 ResultUnknown 观察 | 保留实际设备结果不确定性 | 部 |
| E09-07 | 暂停、单步、恢复在提交 Worker 前安全点发生 | 调试不会占着已申请设备资源冻结线程 | 基 |
| E09-08 | UI/远端暂停请求必须经过身份和安全策略 | 无权限不得操纵生产执行 | 待 |
| E09-09 | 恢复前重新检查资源可用性及版本安全点 | 不能延续已过期的设备运行假设 | 待 |
| E09-10 | 流程/Session 停止接单与 CancelPending 可区分 | 新提交拒绝，旧任务按策略排空或取消 | 基 |
| E09-11 | 已超时外部操作允许迟到确认和审计更新 | 不因本地调用结束丢失 OperationId | 部 |

#### E11 · 异常处理、重试、补偿与恢复（13 项）
**功能意图：**失败不可沉默，未确认外部副作用禁止盲目重试。  **责任：**MRK；MDS/Host/Provider 协作。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E11-01 | 节点异常转为结构化 Failed 结果 | 异常、StepId、关联请求可查看 | 基 |
| E11-02 | 失败后下游阻断或进入显式错误分支 | 不得沿正常路径误继续 | 基 |
| E11-03 | 为幂等工作配置 Retry 次数、间隔等策略 | 验证重试次数、异常条件、取消 | 部 |
| E11-04 | 非幂等硬件操作默认不自动重试 | 同一命令不会因为本地超时再发一次 | 部 |
| E11-05 | 支持显式补偿及真实执行逆序 | 仅补偿已经成功的步骤且报告失败项 | 基 |
| E11-06 | 补偿使用原 Plan/Engine 归属与安全输入 | 不能传递已释放 Scope | 部 |
| E11-07 | 多分支失败后等待相关工作结束才补偿 | 避免补偿与原工作竞争同一资源 | 部 |
| E11-08 | 补偿不能擅自承诺恢复实际设备状态 | Provider 必须确认真实外部状态 | 协 |
| E11-09 | 外部 OperationId 与业务 CorrelationId 分开维护 | 异步未知状态可迟到核销，不能串账 | 部 |
| E11-10 | 持久 Journal 记录运行关键事实及其因果顺序 | 写入失败能导致明确失败/拒绝执行 | 基 |
| E11-11 | 崩溃后识别未结案与未知外部操作 | 必须进入核对，不自动冒险重播 | 基 |
| E11-12 | 持久 Checkpoint 保存足以安全恢复的执行状态 | 重启后符合条件才可授权续跑 | 待 |
| E11-13 | 补偿/恢复过程有独立审计、失败处理与重复提交保护 | 补偿自身失败不被隐藏 | 待 |

#### E12 · 执行会话、生命周期与长时间运行（11 项）
**功能意图：**Worker/Flow/Session/数据/设备各有寿命，不能统一用一条 Dispose 结束。  **责任：**MRK；F19/MDS/Host。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E12-01 | 运行会话可创建、就绪、停止接单、停止 | Session 状态可查询 | 基 |
| E12-02 | 一个 Session 可承载多个 Flow | 不同 Flow 隔离运行身份与资源 | 基 |
| E12-03 | Stop 排空在途工作 | 没有未结算任务被默默遗弃 | 基 |
| E12-04 | Stop 可升级为 CancelPending | 已开始 Stop 后依然能请求更强取消 | 部 |
| E12-05 | Flow 持有固定 Plan Revision 直至终态 | 运行时不被草稿/新计划覆盖 | 基 |
| E12-06 | 支持长期服务连续运行并报告健康状态 | 故障可定位且有上限资源开销 | 部 |
| E12-07 | Flow/Step/Invocation/Frame 生命周期准确嵌套 | 每轮数据与资源释放一次 | 待 |
| E12-08 | 异步外部工作结束后的迟到回调仍能正确归属 | 不复活已销毁的流程对象 | 部 |
| E12-09 | 终止状态一次性结算且不可逆改 | 重复终态/越序日志拒绝 | 部 |
| E12-10 | 进程异常退出后 Host 可盘点历史与待核对项 | 不把存在文件误认为已恢复工作 | 基 |
| E12-11 | 跨进程恢复/接管具有可信防重机制 | 新旧 Host 不同时驱动同一机台 | 待 |

#### E13 · 状态、快照、Trace 与运行观察（13 项）
**功能意图：**每个执行事实都能追到“哪个定义、哪个实例、哪一轮、为何等待”。  **责任：**MRK 事实；Studio/TestCenter 展示。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E13-01 | 可查看 Flow 总状态、时间和完成统计 | Running/Completed/Failed/Cancelled 清楚 | 基 |
| E13-02 | 可查看 Step 状态、WorkId、等待原因 | Queued、WaitingResources、Skipped 等可区分 | 基 |
| E13-03 | Trace 记录有序状态迁移与 UTC 时间 | 状态改变可按序列回看 | 基 |
| E13-04 | Trace 有界并支持增量游标和 Gap 检测 | 漏事件后能重新获取快照 | 基 |
| E13-05 | Journal 保存可恢复审核的事实而非大对象 | 断帧、篡改、顺序错误有明确处理 | 部 |
| E13-06 | 运行实例身份可反查工程 Content/Block/Link | 跳到出错的原始图形节点 | 部 |
| E13-07 | 显示工程修订、Plan修订、FlowId、调用链与迭代 | 重复子流程调用不会串到错误实例 | 待 |
| E13-08 | 可查看执行组、队列、资源租约、耗时和容量 | 等待瓶颈可解释 | 部 |
| E13-09 | 提供错误码、异常链、输入输出摘要与来源 | 调试不需要全文搜索日志猜原因 | 部 |
| E13-10 | 同图运行状态叠加不修改工程定义 | 运行/观察关闭仍可独立继续执行 | 部 |
| E13-11 | 可切换观察级别，控制追踪开销和敏感信息 | 生产默认低开销、有界缓存、授权读取 | 待 |
| E13-12 | 支持远程增量只读订阅与断线恢复 | 重连后从快照+游标恢复显示 | 待 |
| E13-13 | 历史运行记录可以按 Run/产品/流程/版本查询 | 可定位历史异常执行事实 | 待 |

#### E16 · 断点、单步、仿真与重放（12 项）
**功能意图：**同一工程定义在编辑、模拟、观察、调试中表达同一执行语义。  **责任：**MRK Debug；Studio 操作；Simulation Provider。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E16-01 | 支持指定 Step 断点与移除 | 命中前停止提交下一步执行 | 基 |
| E16-02 | 支持暂停、继续及单步推进 | 一次 StepOnce 只释放一个待执行节点 | 基 |
| E16-03 | 等待断点时取消能及时退出 | 不会无限积累已取消的等待者 | 基 |
| E16-04 | 生产调试需要 Observe/Debug/Control 不同权限 | 只读观察不能持有 Cancel 控制权 | 部 |
| E16-05 | 断点安全点不占用设备 Worker/租约 | 危险动作不中途冻结线程 | 部 |
| E16-06 | Studio 在同一张图显示执行高亮 | 不维护第二套逻辑图真源 | 部 |
| E16-07 | 编译诊断可以定位 Content/Block/Link | 用户点击后跳转到准确错误位置 | 部 |
| E16-08 | 支持跨子流程下钻与调用栈返回 | 第 N 次 Invocation/Iteration 仍能准确定位 | 待 |
| E16-09 | 运行中查看输入/输出/局部值与来源 | 默认只获取受限摘要不拷贝图片体数据 | 部 |
| E16-10 | 模拟模式复用正式能力契约和逻辑定义 | 换 Provider 而不是复制一个 SimulationFlow | 部 |
| E16-11 | 支持固定版本的历史 Replay/比较 | 重放不会触发真实设备危险动作 | 待 |
| E16-12 | 可授权附加调试会话，断线后安全回收 | Studio 崩溃不会让生产机卡在断点 | 待 |

#### E17 · 版本固定、发布与安全切换（11 项）
**功能意图：**本体系负责“如何执行固定计划、何时切换”；发行制品/权限来源由构建及 Host 拥有。  **责任：**构建/发布 + Host 主；MRK 执行。

| 原子项 | 用户可感知要求 | 验收行为/失败边界 | 原 V1.0 状态 |
|---|---|---|:---:|
| E17-01 | 工程草稿验证、解析、编译后才能进入运行 | 未验证草稿不能运行生产流程 | 协 |
| E17-02 | 冻结不可变 ExecutionPlan 和来源映射 | 编辑态修改不影响在途实例 | 基 |
| E17-03 | 编译不合法时返回诊断且不启动执行 | 部分编译失败不能留下半启动流程 | 基 |
| E17-04 | 支持软件级 Stage/Activate/Rollback | 运行中旧 Flow 继续使用旧修订 | 基 |
| E17-05 | 计划激活遵循软件空闲/安全点约束 | 有在途租约时拒绝错误切换 | 部 |
| E17-06 | 生产发布需要可信主体授权 | 模拟字符串权限不得替代实际身份认证 | 协 |
| E17-07 | 发布包包含 Provider/Schema/配置/模型等依赖版本 | 运行结果可追溯 Manifest | 待 |
| E17-08 | 正式 Plan/SourceMap/Revision 的持久仓库 | 重启时能找回经过审核的固定版本 | 待 |
| E17-09 | 不兼容新版本回滚具有可核对证据 | 新版本失败时旧计划不被破坏 | 部 |
| E17-10 | 工程运行与 Studio 关闭/网络断开解耦 | 已发布 Flow 继续独立自治运行 | 部 |
| E17-11 | 发布前目标设备状态和联锁由 Host/Provider 判断 | 软件切换成功不意味着硬件安全点成立 | 协 |

### 3.4 原需求状态解释

来源清单中 `基/部/待/协` 是**V1.0 审查时点的需求成熟度标签**，并不表示本次已逐条复测。`基` 有对应源码或测试线索，`部` 目标部分覆盖，`待` 尚缺关键行为或证据，`协` 核心依赖在别的 Owner。来源 ID 和原句逐项保留；本次重排只是改变显示分组与三方面主责。

## 4. 双角色与六角色期望：先定义易用性，再决定 API

### 4.1 使用深度 L0～L4

| 级别 | 软件开发者 | 工程编排用户 | 内部承载 |
|---|---|---|---|
| L0 无感 | 普通 C# 业务函数直接调用/await | 拉入节点、默认连线、无需额外调度概念 | 能力契约、F19/Provider 默认约束 |
| L1 一行/少量 | 简单 Run、Post、Wait、Cancel | 顺序、条件、并行、子流程菜单 | MRK Builder/Plan/Engine |
| L2 配少量业务参数 | 超时、Key、数据输入、业务错误策略 | 业务属性面板、变量与端口来源 | MOC/MDS 与 MRK策略 |
| L3 高级 | ExecutionGroup、资源、执行位置、容量、Trace | 高级调度、数据 Inspector、断点与权限 | MRK/F19/MDS/Studio |
| L4 基础/扩展 | 自定义 Provider/专业 SDK 与底层 Worker | 专家诊断、策略扩展 | Professional/Connectivity 与原有模块 |

**期望指标（待实测，不是当前成绩）**：常见执行/等待 1～3 行；工程大部分常用编排从 L0～L2 完成；常用节点属性不强迫设置 Queue/Lease/DataStack；初学者与专家共用同一项目文件而不是两个编辑器。不得通过省略权限、隐藏设备 Unknown 或静默重复执行来追求“更简单”。

### 4.2 R1 软件开发者最在意的十二项

1. **先写业务再谈流程**：普通方法用原生 `await`，只有要统一控制/调度/审计时才使用 MRK；不强制先持久化工程树。
2. **常见业务直接接入**：普通同步方法、`Task<T>` 和 Provider 都可接入，不额外自己创建线程、锁、队列。
3. **注册一次专业能力**：同一业务实现能被 C# 流程和 Studio 通过同一 CapabilityId/Schema 找到。
4. **流程表达自然**：`Flow`、`Step`、`Subflow`、依赖/并行/分支/循环语义清楚；默认不手造 `MRKStepCompileRequest`。
5. **单输入自然**：业务 DTO 直接传入；复杂输入/多个结果才显式端口和数据绑定。
6. **输出直接可用**：简单场景不用每次读结果字典/类型强转；高级场景仍可查询完整 Result/Trace/FlowId。
7. **失败绝不藏起来**：Flow Failed、Cancelled、Unknown 不可以因 `Task` 正常返回而被判断成功。
8. **异步是真异步**：等待外部设备/AI结果不盲目保留工作线程；阻塞 SDK 清楚声明执行位置。
9. **资源默认可靠**：专业能力声明不可重入/资源依赖，普通业务无需随处 `lock`。
10. **高级能力不消失**：一处按需启用优先级、Group、重试、超时、资源和 Trace，而不是另造一套 API。
11. **能够独立测试**：开发/模拟流程与生产共用执行语义，测试能注入虚拟 Provider 和故障。
12. **生命周期清楚**：Host 长期运行时统一拥有引擎和资源；开发端显式 Stop，不在每次任务中创建新的 Engine。

### 4.3 R3 工程编排用户最在意的十四项

1. 新建 Content/流程或引用既有流程，能保存/重开/复用。
2. 搜索/筛选专业能力，拖到画布即得到业务端口和参数模板。
3. **控制线**表达“谁在谁后”，**数据线**表达“值从哪里来”，二者不能混淆。
4. 日常 UI 只见能力名、业务输入/输出、必要的 Timeout/失败策略；专家才展开调度参数。
5. 类型不匹配、缺入参、重复连线、非法循环等编译期就指出节点/Port/Link。
6. 一键构造并行/Join、条件分支、循环和子流程；界面结构与执行语义一致。
7. 一键把选择的连续片段提取为子 Content，并能查看公开参数、结果与多级调用路径。
8. 参数来源选择一套：常量、上游输出、Flow输入、变量、配置/外部引用；同一字段只显一个有权威值。
9. 从编辑到仿真到正式运行**用同一份定义**，环境的 Provider/授权不同，业务流程不复制。
10. 保存、重开、撤销/重做后仍保持 ID、Revision、端口和连线语义一致。
11. 运行时直接在**同一张图**看 Completed/Running/Waiting/Failed/Skipped 和真实等待原因。
12. 调试可以点击具体节点，看该 Flow/Invocation 的输入/局部/输出摘要、资源与异常；支持跨子 Content 下钻。
13. 暂停/取消/重新启动/发布只经授权；操作员不需要知道 Token/Worker 和线程细节。
14. 运行中改草稿不影响已固定的 Plan；错误日志可以回到确切的原始定义，而不是靠名字猜。

### 4.4 其他四类角色的显式期望

- **R2 专业能力开发者**：一次实现+声明 Schema/版本/资源/线程限制，供代码/编排共用；C++/Go/SDK 的实际实现可以作为 Provider，不强制改写。
- **R4 调试工程师**：能精确回答“哪个 Plan 版本、哪个产品/产线、哪个调用层和第几轮、等哪条资源/应答、哪条数据、为何卡住”；只读观察与授权单步严格区分。
- **R5 生产操作员**：明确显示启动许可、安全停机确认、外部 Unknown、产品级异常结果；不能把`StopRequested`显示为`DeviceStopped`。
- **R6 平台维护者**：统一 Worker、统一执行内核、统一 Runtime 数据 Owner，负载与诊断有界、可压测、可审计；不因每个专业产生一个新 Manager。

### 4.5 完整 UDEV/UENG/UOPS 与质量需求（原编号全保留）

原文 L0～L4 的简易度分层继续有效：L0 普通业务无感执行；L1 常用操作一行/少量操作；L2 少量业务配置；L3 执行组/资源/调试高级选项；L4 为能力开发者或平台开发者暴露底层。80%～90% 场景覆盖 L0～L2 是**待验证目标**，不是现有达成数据。

#### UDEV · 软件开发者与专业开发者：简洁直接

| 编号 | 体验/功能要求 | 必须怎样验收 |
|---|---|---|
| UDEV-01 | 可直接执行一次业务函数，必要时走 F19 提交 | 无需先写完整工程 Flow 或创建线程管理器 |
| UDEV-02 | 声明一次专业能力，既能 C# 调用也能 Studio 选择 | 业务代码、类型和行为约束不复制实现 |
| UDEV-03 | 常见流程可以简单表达 Step/After/Subflow | 减少原始 CompileRequest、Engine、Resolver 的显式组合 |
| UDEV-04 | 输入单一请求对象时直接执行并取结果 | 不强迫操作 Context 字典或多层数据容器 |
| UDEV-05 | 多输入/多输出时使用类型化端口/DTO | 直接得到业务值、执行身份与错误 |
| UDEV-06 | 默认失败不会被错误当作 C# 成功返回 | 失败、取消、未知结果有明确可判断路径 |
| UDEV-07 | 普通同步算法和真正异步操作分别接入 | 不强行 Task.Run 假装异步 |
| UDEV-08 | 高级线程、资源、策略设置按需出现 | 80%日常任务不理解 Group/Lease 即可使用（目标，需测量） |
| UDEV-09 | 每个常用功能有可直接复制的 .NET48 示例 | 示例必须可编译并跑通真实测试 |
| UDEV-10 | 代码创建的 Flow 与工程编辑 Flow 共用执行语义 | 同输入/Provider 得到等价结果、Trace 与错误 |
| UDEV-11 | 高级 API 保留，不能因简化门面损失能力 | 使用简单入口时也能逐步引入高级选项 |

#### UENG · 工程编排用户：拖、连、配、测、看

| 编号 | 体验/功能要求 | 必须怎样验收 |
|---|---|---|
| UENG-01 | 从工程 Content 创建或引用执行流程 | 工程保存、重开、Revision 保持稳定 |
| UENG-02 | 从能力目录搜索/拖入节点 | 能力名称、用途、入出端口和可用性清楚 |
| UENG-03 | 直接从类型化端口拖出 Control/Data 连线 | 不合法类型、重复边、环路及入度被即时拒绝 |
| UENG-04 | 常见顺序/并行/条件/循环在画布直接表达 | 用户无需手写 C# Task/Lock |
| UENG-05 | 节点属性面板默认只显示业务参数与必要超时/失败处理 | 特殊资源/调度放高级设置 |
| UENG-06 | 输入值只有一套来源选择器 | 常量、上游输出、配置、变量来源一致 |
| UENG-07 | 一键提取或引用可复用子 Content | 自动形成可复核入/出参并支持多层下钻 |
| UENG-08 | 编排支持拖动/缩放/框选/复制/撤销重做 | 编辑与操作行为保持工程事务一致 |
| UENG-09 | 编译诊断点击后定位到准确节点/端口/连线 | 用户不必搜索内部 StepId |
| UENG-10 | CPU/虚拟 Provider 仿真与生产使用同一个定义 | 变更的是环境与权限，不是复制业务流程 |
| UENG-11 | 运行同一张图显示状态、等待原因与结果 | 运行事实来源于 MRK，不另建 UI Executor |
| UENG-12 | 调试同一张图支持断点、单步、子调用定位 | 能看到迭代和真实数据摘要 |
| UENG-13 | 工程从保存、重开到运行的闭环可验证 | 重开后编译结果与运行语义一致 |
| UENG-14 | 新手配置与专家调度模式分层展示 | 多数正常业务保持少量点击即可编排（目标，需实测） |

#### UOPS · 调试者、操作员与平台维护者

| 编号 | 体验/功能要求 | 必须怎样验收 |
|---|---|---|
| UOPS-01 | 调试人员能回答为何排队、等待或失败 | 状态包含 Group/资源/应答/依赖理由 |
| UOPS-02 | 调试能定位准确 Product/Line/Flow/Plan/Invocation | 相同子流程多实例并行不串账 |
| UOPS-03 | 操作员能够区分正常/故障/未知 | 未确认设备命令不能显示为安全已停 |
| UOPS-04 | 生产启停/发布/授权各有权限等级 | 只读用户不能借调试接口获得控制句柄 |
| UOPS-05 | 断线后 Runtime 自治，恢复后观察能追上状态 | 不依赖 Studio 长期在线 |
| UOPS-06 | 维护者能看到吞吐、延时、队列、内存、Worker 等指标 | 长稳和容量有证据而不是猜测 |
| UOPS-07 | 系统只保留一套公共执行内核与唯一工程真源 | 专业流程共享机制，不复制通用 Executor |

### 4.6 横向约束与质量要求（原 Q 编号）

#### QREL · 一致性、可靠性与异常证明

| 编号 | 要求 | 验收或禁止事项 |
|---|---|---|
| QREL-01 | 原工程快照与执行中 Plan 分离，Revision 固定 | 旧 Flow 不会读取新草稿定义 |
| QREL-02 | 执行状态机的转移与终态因果严格校验 | 重复终态、结果逆序与未知操作会被发现 |
| QREL-03 | 故障注入覆盖取消、Stop、资源冲突、磁盘写失败 | 都可以结算和清理，不永远 Running |
| QREL-04 | 完整崩溃恢复依赖持久身份、Plan、数据和幂等证据 | 恢复不可盲目重复危险外部操作 |
| QREL-05 | 业务能力边界出错时诊断精准 | 缺 Provider、错端口、数据寿命有定位 |

#### QPERF · 性能、资源与实时边界

| 编号 | 要求 | 验收或禁止事项 |
|---|---|---|
| QPERF-01 | 有界队列、执行组、Trace、Heap Budget 有可测阈值 | 高吞吐负载下内存与句柄不无限增长 |
| QPERF-02 | 代码入口和 Studio 入口执行额外开销可比较 | 不因画布而更改调度语义 |
| QPERF-03 | 无 WPF 依赖的 Runtime 和 Worker 可独立运行 | 关闭 Studio 不影响生产 Work |
| QPERF-04 | CT/Image/Tensor 等大数据避免未声明深复制 | 复制、持有和释放成本可追踪 |
| QPERF-05 | 诊断级别带来的吞吐/延时损失要可测试 | 生产默认采用低开销等级 |
| QPERF-06 | 强实时/安全动作交给控制卡、PLC 或专业控制闭环 | Windows Task/Timer 不宣称硬实时 |

#### QSEC · 安全、权限、版本与审计

| 编号 | 要求 | 验收或禁止事项 |
|---|---|---|
| QSEC-01 | 权限校验贯穿开始、取消、调试、补偿、发布 | 不同主体不能通过遗留 Handle 绕过网关 |
| QSEC-02 | 模拟、只读、生产控制明显隔离 | 模拟批准策略不得用于现场生产 |
| QSEC-03 | 所有外部命令有身份和可核对事实 | 取消、超时、未知状态不等于安全完成 |
| QSEC-04 | 必要版本含工程、配置、算法/模型、Provider、Schema | 每次实际执行能回溯使用版本 |
| QSEC-05 | 敏感数据及大对象不直接进入普通日志与 Trace | 默认保存摘要、引用和受限查看 |

#### QTEST · 开发验收与证据分级

| 编号 | 要求 | 验收或禁止事项 |
|---|---|---|
| QTEST-01 | 需求、源码、测试、CI、实机事实分层记录 | 不会把“有类”写成“功能验收完成” |
| QTEST-02 | Windows .NET Framework 4.8 x64 可编译 | 变更后以对应提交的 Actions 判断 |
| QTEST-03 | 已有 FoundationSmoke/ArchitectureSmoke/TestCenter 复用 | 不新建测试版执行器 |
| QTEST-04 | 测试含正确路径、负例、竞态、长稳与跨进程场景 | 不会只验证 happy path |
| QTEST-05 | 跨专业案例检验通用与专业边界 | 不为通过样例写入硬编码流程分支 |

## 5. 软件开发者：现有真实 API、理想使用方式和实际场景

### 5.1 【实】单个请求、单个能力、取业务结果——最小可运行示例

以下示例依照现有 `MRKCodeRuntimeBuilder.CapabilityForRequest<TRequest,TResult>()`、`Flow().Step()`、`Build()`、`RunAsync()`、`StopAsync()` 的公开签名撰写；使用开发/模拟运行，不绕过正式 Host 的权限与安全点要求。只为了说明接口组合，不能把假的图像算法认为已验收。

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Mechanism.Runtime.Execution;

public sealed class InspectRequest
{
    public string Sn { get; set; }
}

public sealed class InspectVerdict
{
    public bool Passed { get; set; }
}

public static class ExistingRuntimeExample
{
    public static async Task<InspectVerdict> RunAsync()
    {
        MRKCodeRuntimeBuilder builder = new MRKCodeRuntimeBuilder();
        builder.CapabilityForRequest<InspectRequest, InspectVerdict>(
            "vision.inspect", (request, cancellation) =>
            {
                cancellation.ThrowIfCancellationRequested();
                return Task.FromResult(new InspectVerdict {
                    Passed = !string.IsNullOrWhiteSpace(request.Sn)
                });
            });
        builder.Flow("Inspect", f => f.Step("InspectNode", "vision.inspect"));
        MRKDevelopmentRuntime runtime = builder.Build();
        try
        {
            MRKFlowResult result = await runtime.RunAsync(
                "Inspect", (object)new InspectRequest { Sn = "SN001" });
            if (result.State != MRKFlowState.Completed)
                throw new InvalidOperationException("流程执行失败", result.Error);
            return (InspectVerdict)result.Results["InspectNode"];
        }
        finally
        {
            await runtime.StopAsync();
        }
    }
}
```

**已有优点**：不必显式实例化 Worker/Plan/Resolver/Engine；一次注册并冻结，多个 Flow 可以复用同一 Worker；Plan 编译不通过时在创建运行资源前失败。**当前痛点**：一次输出仍需手工判断 Flow 状态并从字典强转；新手容易误将 Task 正常返回当作 Flow 成功。

### 5.2 【设】最值得做的“短入口”——不新增新执行器

**目标（不是现有 API）**：在已有 `MRKDevelopmentRuntime` 或其小型扩展方法上增加便捷的**单值结果读法**，如 `RunValueAsync<TResult>(flowId, request, outputName, cancellation)`：成功返回 `TResult`，失败抛携带 FlowId、原异常与错误码的结构化异常，取消按调用方语义呈现，外部 Unknown 保留专有诊断。仍可通过现有 `RunAsync` 取完整 `MRKFlowResult`。实现前须明确源码中结果是命名 Export 还是 Step Result，不能按最后一步“猜”输出名。

```csharp
// 【目标用法草案，非当前可编译签名】
InspectVerdict value = await runtime.RunValueAsync<InspectVerdict>(
    "Inspect", new InspectRequest { Sn = "SN001" }, "verdict", cancellation);

// 要看 FlowId、各节点状态、Trace、异常细节，则继续用现有：
MRKFlowResult full = await runtime.RunAsync("Inspect", (object)request);
```

**简化边界**：一个方法的薄封装，不创建 `SimpleExecutionEngine`、`EasyRuntimeManager`、全局静态单例，也不让执行引擎重复保存业务数据。目标以“典型操作需要的概念数和业务代码行数减少”为验收，不以新增门面数量为验收。

### 5.3 【实】多步骤与二级子流程——现有 Builder 的真实操作

```csharp
MRKCodeRuntimeBuilder builder = new MRKCodeRuntimeBuilder();
// 前提：能力 ID 已注册真实回调/Provider。这里仅示意流程控制，
// 不是完整的数据端口绑定程序。
builder.Flow("Preprocess", f => f
    .Step("Correct", "vision.correct")
    .Step("Enhance", "vision.enhance", "Correct"));

builder.Flow("Inspect", f => f
    .Step("Acquire", "vision.acquire")
    .Subflow("PreprocessCall", "Preprocess", "Acquire")
    .Step("Judge", "inspection.judge", "PreprocessCall"));

MRKCodeRuntimeBlueprint blueprint = builder.Freeze();
MRKDevelopmentRuntime runtime = blueprint.BuildForDevelopment();
// runtime.RunAsync(...)；结束后 await runtime.StopAsync()。
```

**真实数据必须显式绑定**：`Step("Enhance",...,"Correct")` 只有控制依赖，并未自动把 Correct 的输出塞给 Enhance。使用现有 `StepData(...)`、`MRKExecutionInput`、`DataCapability(...)`、`Export(...)` 和 MDS 数据机制说明真实入出参。开发者日常不应手写 Push/Pop/Frame；如果一个 API 迫使日常用户操作栈，应回到 MOC/MDS 封装层修正设计。

### 5.3A 【实 + 已有自动化测试】三层子流程：真实类型端口、参数与最终返回值

前一个示例只说明了顺序。以下示例则来自仓库 `RuntimeDataCompilerUnificationTests.ThreeLevelNamedDataContract()` 的真实契约：**输入从父 Flow 显式映射到子 Flow；子 Flow 只公开声明过的输出；后继使用 `StepOutput` 连接；最终使用 `GetCommittedOutput<T>()` 直接获取命名业务结果。** 因而不用自己 Push/Pop 数据栈，且可以真正在 .NET Framework 4.8 流程中调用。

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CxVisual.Mechanism.Runtime.Execution;

public static class ThreeLevelDataExample
{
    public static async Task<int> ExecuteAsync(int seed)
    {
        Dictionary<string, Type> d = new Dictionary<string, Type>
        { { "Doubled", typeof(int) } };
        Dictionary<string, Type> p = new Dictionary<string, Type>
        { { "Raised", typeof(int) } };
        Dictionary<string, Type> f = new Dictionary<string, Type>
        { { "Answer", typeof(int) } };

        MRKCodeRuntimeBuilder builder = new MRKCodeRuntimeBuilder()
            .DataCapability("Double", (ctx, token) =>
            {
                ctx.Data.Return("Doubled", ctx.Data.Get<int>("N") * 2);
                return Task.FromResult((object)true);
            }, d, new[] { new MRKCapabilityInput("N", typeof(int)) })
            .DataCapability("Plus", (ctx, token) =>
            {
                ctx.Data.Return("Raised", ctx.Data.Get<int>("Value") + 1);
                return Task.FromResult((object)true);
            }, p, new[] { new MRKCapabilityInput("Value", typeof(int)) })
            .DataCapability("Final", (ctx, token) =>
            {
                ctx.Data.Return("Answer", ctx.Data.Get<int>("FromMid") * 3);
                return Task.FromResult((object)true);
            }, f, new[] { new MRKCapabilityInput("FromMid", typeof(int)) })
            .Flow("Child", child => child
                .StepData("scale", "Double", new[] {
                    new MRKExecutionInput("N", MRKInputSource.FlowInput,
                        "inputNumber", typeof(int))
                })
                .Export("LeafValue", "scale.Doubled"))
            .Flow("Mid", mid => mid
                .Subflow("leaf", "Child", new[] {
                    new MRKExecutionInput("inputNumber", MRKInputSource.FlowInput,
                        "middleSeed", typeof(int))
                })
                .StepData("plus", "Plus", new[] {
                    new MRKExecutionInput("Value", MRKInputSource.StepOutput,
                        "leaf.LeafValue", typeof(int))
                }, "leaf")
                .Export("MidResult", "plus.Raised"))
            .Flow("Top", top => top
                .Subflow("mid", "Mid", new[] {
                    new MRKExecutionInput("middleSeed", MRKInputSource.FlowInput,
                        "seed", typeof(int))
                })
                .StepData("check", "Final", new[] {
                    new MRKExecutionInput("FromMid", MRKInputSource.StepOutput,
                        "mid.MidResult", typeof(int))
                }, "mid")
                .Export("FinalResult", "check.Answer"));

        MRKDevelopmentRuntime runtime = builder.Build();
        try
        {
            MRKFlowResult result = await runtime.RunAsync("Top",
                new Dictionary<string, object> { { "seed", seed } });
            if (result.State != MRKFlowState.Completed)
                throw new InvalidOperationException("三层执行未成功", result.Error);
            return result.GetCommittedOutput<int>("FinalResult");
        }
        finally
        {
            await runtime.StopAsync();
        }
    }
}
```

**结果推导**：`seed = 4` 时，Child 得 `8`，Mid 得 `9`，Top 得 `27`。子流程的 `LeafValue` 和 `MidResult` 不会未经声明直接泄露到 Top 的公开输出；同一冻结流程再输入 `seed=10`，输出为 `63` 且不同运行实例数据应隔离。这些正是现有测试中已覆盖的基础行为。**注意**：它验证的是现有瞬时 `MRKCodeRuntimeBuilder` 三层数据契约，不意味着 Studio V2 中任意复杂子 Content 的类型化提取与发布已经完成。

### 5.4 【实】同步/异步/长任务三种使用强度

| 场景 | 推荐用户入口 | 设计原因 |
|---|---|---|
| 普通一次性业务函数 | 直接 C# 调用或原生 `await`，必要时 F19 `Post`/工作句柄 | 不为一行函数强制建 Flow |
| 需要流程状态/分支/统一资源 | `MRKCodeRuntimeBuilder` → 开发运行，或已发布 Plan → MRK Engine | 统一上下文/状态/诊断 |
| 真实设备长任务与正式生产 | Host 下经可信身份、能力/资源准入、安全发布和 Provider 运行 | Studio/开发运行不能自行成为生产主控 |

**已有 F19 示例（真实类名与已核对 API）**：

```csharp
// 【实】F19 层仅作普通工作，不需要工程 Content。
// worker 为由应用统一创建并托管生命周期的 FCRWorker 对象。
FCRWorkHandle<bool> task = worker.Post(() => ProcessLog());
await worker.StopAsync();
// 需要跟踪工作结果时检查 Handle 和对应 FCRWorkResult/WaitObservation。
```

必须区分 Worker 的任务接纳/排队与 Flow 的专业执行语义。专用 SDK 同步线程通过既有 `FCRDedicatedExecutor` / MRK 执行位置协作，不简单在同步 SDK 外套 `Task.Run`。

### 5.5 【设】复杂业务流程在 C# 中的自然写法——语义草案

**期望**：开发者能在同一流程声明 `Acquire → Preprocess → Parallel(AI,CV) → Join → Judge → If NG → Recheck`，按需要附加资源/Key/重试/超时策略。普通业务层不直接写 `TaskCompletionSource`、`SemaphoreSlim`、`Lock`、线程启动、数据栈控制。控制流 API 必须复用 MOC/MRK 同一编译语义；以下表达**只是目标 DSL 示意**，不是现有已交付方法：

```csharp
// 【目标设计，非现有签名】
flow.Step("Acquire", "vision.acquire").UsingResource("DetectorA");
flow.Subflow("Preprocess", "CT.Preprocess").After("Acquire");
flow.Parallel(p => p
    .Step("AI", "vision.ai").After("Preprocess")
    .Step("CV", "vision.cv").After("Preprocess"))
    .Join(JoinPolicy.All);
flow.Step("Judge", "inspection.judge").After("AI", "CV");
flow.If("Judge.IsNG", yes => yes.Subflow("Recheck", "inspection.recheck"));
```

**不要直接照此批量创建类型**。设计评审阶段应优先讨论是否只扩充现有 `MRKCodeFlowBuilder` 的几个表达式/控制组合方法，以及复用 MOL/MOC 的结构控制类型，避免产生跟 Studio 不一致的第二 DSL。专业 Provider 对大型图像的传递应走 MDS/数据引用/端口合同，不借控制线隐式转值。

### 5.6 开发者常见异常情境与希望的结果

| 情况 | API 应看到 | 禁止发生 |
|---|---|---|
| 能力未注册/端口类型不匹配 | 冻结/编译返回诊断，包含源步骤/端口 | 部分工作已经运行后才发现无能力 |
| 并发超额/队列满 | 可识别 Rejected/容量诊断 | 静默丢任务或无限排队 |
| 在途取消 | 先记录 RequestCancel，协作结算/外部 Unknown | 把 Token 请求当作真实设备已停 |
| AI 超时后迟到 | 迟到回执按 Flow/Operation 关联，已关闭的数据不得再写 | 写入下一个产品或释放已使用的 Buffer |
| 失败后补偿 | 只处理已成功阶段，按真实完成逆序，保留补偿失败详情 | 自动假设硬件回滚成功 |
| 运行中更新工程 | 已运行 Flow 固定 Plan/Revision，新稿不得污染 | 动态引用编辑中的 Content 节点 |
| Host 异常重启 | 已可确认安全的才恢复，不安全的必须核对 | 盲目重发外部运动/PLC 指令 |

## 6. 工程编排用户：同一图完成“选、连、配、测、运行、调试”

### 6.1 两个入口应表达同一工作，而不是强求相同的表面 API

| 操作意图 | C# 用户看到 | Studio 用户看到 | 同一语义需证明 |
|---|---|---|---|
| 声明可复用业务能力 | `CapabilityId + Schema + Provider` | 能力库条目、名称、入出端口 | 同一专业实现/参数契约 |
| 创建主流程 | `Flow`/代码流程或使用工程编译 | 新建/选择 Content→Logic | 同一有效的执行定义 |
| 顺序 | `Step(... after...)` | 控制线 A→B | 相同依赖、Skipped/失败传播 |
| 传递数据 | `StepData`、`Export` | 类型化 Data Port 连线 | MDS 数据引用/提交与类型一致 |
| 条件/循环 | 目标控制 API/MOL | If/Switch/Loop 控制块及属性 | 相同区域边界/迭代/退出 |
| 子流程 | `Subflow` | 引用或提取 Content、双击下钻 | 实例路径、入参/出参、独立数据 Frame |
| 并行 | 多个无依赖步骤/Join 规则 | 并行分叉/汇合 | 胜出/尾部清理与资源约束一致 |
| 仿真/生产 | Provider 环境及 Host 策略 | 选择仿真或已发布运行模式 | 工程版本固定、权限和安全隔离 |
| 观察/调试 | FlowResult、Snapshot、Trace/授权 Debug | 原图实时着色、端口检查、断点单步 | 源 StepId/InvocationId/SourceMap 一致 |

### 6.2 【实】目前在 Studio 已具备的编辑基础

已有 MEM Content Logic V2、MOCFlowBuilder/Edit、能力目录、Control/Data 类型端口、直接拖线、节点布局、缩放、框选、多选、对齐、复制/粘贴、组与撤销重做。G04-F 在读取快照时还具备**无数据边界的线性 Control 片段提取为真实子 Content**，并通过 MOC 编译映射为原 MRK 计划。

**严格限制**：`MOLWiringDefinition` 的普通 V2 自由编排仍以 Action DAG 为基础；旧 V1 的 Loop/Branch 是另一路结构化定义，目前不能声称 V2 已有任意动态 Region、Try/Catch/Finally、复杂 Switch。抽取线性 Control Content ≠ 已实现跨 Content 的任意类型化数据参数提取。界面结构、MOC 实现和 CI 验收都需要按当前 SHA 分开看。

### 6.3 目标用户操作路径（含界面责任归属）

1. **选择工程**：打开 Project，选 Page 或复用 Content；Logic 的权威定义由 MEM Session 管，草稿状态可撤销重做。
2. **选择能力**：从 MOCCapabilityCatalog 搜索“采图/矫正/AI检测/传统检测/结果判定/MES”；拖放得到业务名与 Port Schema。没有安装/不可用的 Provider 要立即提示。
3. **连接控制**：A→B；对于并行，选择“并行开始/汇合”模板；对于 If/Loop，用专门语义块及约束，不绘制一个以圆圈掩盖的“无限 DAG 回边”。
4. **配置数据**：在属性面板选 `来源=常量/Flow输入/上游输出/配置/变量`，只显示与类型/业务有关的选项；DataPort 连线与 MDS 绑定对齐。
5. **配置执行约束**：通常只看 Timeout、失败处理；“资源、串行 Key、ExecutionGroup、优先级、队列容量、线程位置”默认折叠为高级设置，并优先继承 Capability 的安全限制。
6. **校验与构建**：MOC 检查能力缺失、无入口、未连接必填 Port、类型错误、递归引用、循环预算；点击诊断回到准确的源 Block/Port/Link。
7. **模拟运行**：同一 Content、同一逻辑、换 Virtual Provider；图上实时显示当前分支、NodeState/等待原因，失败可点开业务详情。
8. **正式发布与运行**：仍经 Host/权限/版本固定/目标检查/安全切换；运行结果回到同一工程图；关闭 Studio 不影响生产运行。
9. **调试与复用**：可单步或按授权安全点暂停；子流程双击进入其独立编辑区；检查迭代数据与调用路径；提取可复用 Content 后仍保证参数合同可验证。

### 6.4 工程用户完整案例：X-Ray 多工位检测（目标流程）

```text
工程 XRayLineProject
  Page：ProductionMain (显示工位状态与结果)
  Content：ProductionInspect（多工位公共主流程）
    ├─ 入口：产品SN、产线ID、工位列表、配方引用
    ├─ 采图准备 → 子Content：AcquireCTOrXRay
    │    ├─ 请求探测器/运动资源、启动曝光、异步等待回执
    │    └─ 输出：原始图像定位/引用、时间戳、图像规格
    ├─ ForEach (工位/图像，按上限与预算)
    │    ├─ 子Content：Preprocess
    │    │    └─ 黑白校正 → ROI → 图像增强
    │    ├─ Parallel
    │    │    ├─ 子Content：AIInspection
    │    │    └─ 子Content：CVInspection
    │    ├─ Join(All 或明确的业务策略)
    │    └─ 子Content：Judge → If NG 则 Recheck
    └─ 结果汇总 → 输出产品判定 → MES/结果服务
```

**工程用户看见的默认参数**：`输入 SN/工位/图像`、`输出 JudgeResult`、`超时`、`是否继续下一个工位`；**只有专家看见**单产线采图串行 Key、AI 并发上限、GPU/显存预算、探测器租约、失败补偿、优先级等。若检测面向真实产线，任何跳帧/失败不得无痕吞掉，必须形成产品级结果或明确未检测。

**一套定义，多实例使用**：`LineA`、`LineB` 引用同一 Content（可有实例级合法参数覆盖），运行时是不同 `FlowId + InstancePath`；因共享探测器/算法资源而需要串行的范围按 Stage/Resource 声明，不以“整个 Flow 全局锁”牺牲吞吐。

**失败演示**：LineB 第 37 张图 AI 超时；在原图对应 `AIInspection` 节点显示 `Waiting/ResultUnknown/Failed` 的真实情况，点击能看到 `PlanRevision/FlowId/InvocationId/Iteration=37/OperationId` 和该次数据摘要；如果 AI 请求外部状态未知，不能误显示“推理已经取消”，也不得默认重放外部动作。

### 6.5 编排用户的六个必须避免的“不自然”

1. 为了并行让用户手工添加“开线程、join 线程、销毁线程”的虚拟节点。
2. 为了传递图像强制绘制 Push/Pop/DataHeap/Lease/Commit 控制节点。
3. 控制线连起来就偷偷把前一步 DTO 猜作下一个输入。
4. 用户选择“超时”却无法区分“本地等待超时”和“外部命令未确认”。
5. 生产和仿真维护两个不同 Flow，或编辑与运行各画一张权威图。
6. UI 保存一套 Block 坐标/连接关系，MEM 又保存另一套逻辑真源。

## 7. 反推统一使用 API：仅三层，不另起一套门面

### 7.1 L0：直接调用专业业务

`await ai.DetectAsync(image, token)` 或 `worker.Post(action)`。只有需要受控串行、资源、流程诊断时再进 MRK。项目外的原生业务函数可以照常存在；“统一”指被纳入 Flow 时使用同一能力/执行契约，而非所有代码都要继承 `MRKNode`。

### 7.2 L1：现有最自然入口

- 一次注册能力：`MRKCodeRuntimeBuilder.Capability(...)` / `.CapabilityForRequest(...)` / `.DataCapability(...)`。
- 一次定义：`Flow(id, builder => builder.Step(...).Subflow(...).Export(...))`。
- 一次冻结：`Freeze()`；启动开发进程：`BuildForDevelopment()` 或 `Build()`。
- 一次运行：`RunAsync(flowId, request)` 或 `StartFlow(...)` 获取 Handle。
- 完成后 `MRKFlowResult.State + Results/CommittedOutputs + Error`；使用方控制 Runtime 会话生命周期 `StopAsync()`。

简化方向不应否定这些已经可用的入口；让错误/类型/结果更自然，**薄扩展优先**。

### 7.3 L2：高级选项不占默认认知负担

把 `Timeout、RetryIfSafe、SerialKey、Group、RequiredResources、DedicatedExecutorId、Priority、EstimatedPendingBytes` 作为某次执行或能力契约的**可选、显式**配置。先服从 Provider 本身最严格的资源安全声明，工程覆盖只能进一步收紧不能放宽危险约束。底层仍用 `MRKExecutionPolicy`、`MRKExecutionGroups`、`MRKResourceLeaseCoordinator` 等现有类型，不强造“统一万能Options大对象”。

### 7.4 L3：专业调试/恢复的最小安全入口

- **观察**：看 `MRKRuntimeFlowTicket` / `MRKFlowSnapshot` / `MRKExecutionTrace.ReadSince`，与 SourceMap 定位到工程定义。
- **授权控制**：依赖 `MRKRuntimeAccessGateway`，对某些模拟或安全暂停点使用 `MRKExecutionDebugController`。
- **恢复**：先核对 `MRKJournalRecovery` 的状态、外部 `OperationId`、持久 Checkpoint 与权威 Provider 确认；不能因为有 `RequestCancel` 方法就绕开权限网关。

### 7.5 六项统一语义的不变量

| 不变量 | 需要证明的等价性 |
|---|---|
| I1 能力 | 同一 CapabilityId/版本、Provider 和类型合同被两个入口复用 |
| I2 控制 | 同一逻辑的 If/Loop/Parallel/Join 得到相同分支、迭代、跳过与错误传播 |
| I3 数据 | 同一端口绑定后，输入、待提交、已提交、生命周期与输出一致 |
| I4 调度 | 相同 Group/资源/队列/Key 策略下行为一致（不要求线程时间点完全相同） |
| I5 运行事实 | `PlanRevision、FlowId、StepId、InvocationId、Trace、SourceMap` 可以对应到准确来源 |
| I6 发布与安全 | 开发/模拟可便利；生产必须独立授权、版本固定、设备安全确认，不能走捷径 |

验收这些不变量的方法是**行为与结果对照测试**，不是比对两种 UI 看起来是不是一样。

## 8. 全类架构：按三方面组织，项目归属始终唯一

### 8.1 为什么不能“每条需求建一个 Manager”

类是为了稳定职责、所有权与真实可复用行为存在，不是为了补齐名词。所谓“全类架构”，要清点**已有可复用类型族+缺能力时最少的新增契约**：每一能力清楚由哪一现有类承接、哪些枚举在用、哪些方法欠缺。E01～E18 可以需要数十个类共同实现，但并非 18 套独立引擎。

**项目约束**：

- 执行内核都落 `Mechanism/Runtime/Execution/`（既有 `CxVisual.Mechanism.Runtime`），不再新建 `FlowControl` / `ExecutionScheduler` / `ExecutionManager` 三个项目；三方面是**能力组织维度**。
- F19 所有通用并发/队列原语仍落 `Foundation/Infrastructure/Concurrency/`，不挪进 MRK。
- `MOLLogicDefinition` 和 V2 `MOLWiringDefinition` 是 `Mechanism.Core` 唯一 Logic Aspect 权威模型；`MOC` 负责工程侧 DSL/解析与 SourceMap；`MDS` 保持数据体系的唯一 Owner；Studio 仅编辑与展示。
- 不强制一套现有 `MRKExecutionEngine` 拆分成三个 Manager；它作为**执行协调根**，内部调用三方面能力，同一运行会话只有一个权威运行事实。
- 列表中类型名标为 `【实】` 代表本轮读取到对应源级声明；不等于它已经支持全部目标行为。`【设】` 则是需先过正反例/查重评审的设计提案。

### 8.2 类型计数口径

**计数仅用于查漏，不作为开发任务指标**：同一类型只能算在一个唯一 Owner；纯内部 helper 不要求每个都对外公开；枚举是契约不是子系统；性能开销和复杂度是控制类型数量的重要约束。本文附录列出了已读取的 MRK、F19、MOC/MOL/MDS 和 Studio 类型族，**并不声称把整个 CxVisual 四大 Excel 的候选类型逐行复核过**。

### 8.3 执行控制 · MRK 内核（本轮已存在的类型）

| 已有类型 | 形态 | 唯一职责 | 现有主要属性/方法或能力 | 对应需求 |
| --- | --- | --- | --- | --- |
| MRKExecutionPlan | 类 | 已编译稳定 Plan、依赖验证、子流程内联 | Id/Steps/DataExports；GetStep、InlineSubflow | E02/E08/E14 |
| MRKExecutionStep | 类 | 实际可调度步骤、依赖、端口与能力委托 | Dependencies/Inputs/Output/ExecuteAsync/Policy/RequiredResources | E02/E07/E08 |
| MRKExecutionPlanCompiler | 类 | 瞬时请求→Plan；缺能力、依赖和端口诊断 | Compile(planId,requests,resolver) | E02/E07/E14 |
| MRKStepCompileRequest | 类 | 编译步骤请求与策略/端口/子流程标识 | Id/CapabilityId/Dependencies/Inputs/Condition等 | E02/E14 |
| MRKCapabilityInput | 类 | 能力端输入定义 | Name/ValueType/Required等 | E07 |
| MRKResolvedCapability | 类 | 实际 Provider 委托和数据端口/输出合同 | ExecuteAsync/Inputs/Output/DataOutputs/CompensateAsync | E01/E07 |
| IMRKCapabilityResolver | 接口 | CapabilityId→实际能力 | TryResolve | E14 |
| MRKCompileDiagnostic | 类 | MRK 编译诊断 | Code/StepId/Message | E02/E14 |
| MRKCompileResult | 类 | Plan+诊断及成功判断 | Succeeded/Plan/Diagnostics | E02/E14 |
| MRKFlowCompileDefinition | 类 | 瞬时 Flow 步骤与出口描述，不替代 Content | Id/Steps/DataExports | E14 |
| IMRKFlowCompileCatalog | 接口 | 瞬时/冻结 Flow 定义提供方 | TryGet | E14 |
| MRKExecutionOrchestrationCompiler | 类 | 多级瞬时子流程递归编译/展开 | Compile(rootId,catalog,resolver) | E14 |
| MRKExecutionInput | 类 | FlowInput/StepResult/StepOutput 的端口输入 | Name/Source/SourceName/ValueType/Required | E07 |
| MRKExecutionOutput | 类 | 步骤输出的类型合同 | ValueType | E07 |
| MRKExecutionPortException | 类 | 端口绑定异常，防止静默隐式转换 | 结构化异常 | E07 |
| MRKWorkJoin | 静态类 | 现有工作句柄 FirstCompleted/FirstSuccessful 的结算 | FirstCompletedAsync/FirstSuccessfulAsync | E08 |
| MRKJoinResult<T> | 类 | 工作汇合的胜出与剩余结算 | Winner/WinningResult/AllSettled | E08 |
| MRKExecutionOperations | 静态类 | 单 Step 内 Condition/Loop 组合算子 | Condition、Loop；不是多 Step Region | E02 |

### 8.3 执行调度 · MRK（本轮已存在的类型）

| 已有类型 | 形态 | 唯一职责 | 现有主要属性/方法或能力 | 对应需求 |
| --- | --- | --- | --- | --- |
| MRKExecutionGroups | 类 | 按执行组 ID 受控排队与并发配额 | AcquireAsync、配额与快照 | E03/E05 |
| MRKExecutionGroupDefinition | 类 | 执行组限制规则 | Id/MaxConcurrency/WaitingCapacity等 | E05 |
| MRKExecutionGroupSnapshot | 类 | 执行组运行统计 | Active/Waiting/Rejected等 | E05/E13 |
| MRKExecutionGroupLease | 类 | 组槽位持有与归还 | Dispose | E05/E10 |
| MRKExecutionGroupRejectedException | 类 | 组配额拒绝的明确结果 | 异常原因 | E05/E06 |
| MRKResourceLeaseCoordinator | 类 | 资源请求排序、等待和受控独占 | AcquireAsync | E10 |
| MRKResourceLease | 类 | 释放 MRK 资源占用 | Dispose | E10 |
| MRKExecutionTrigger | 类 | 触发计划、限流和周期启动 | TryTrigger/StartPeriodic/StopAsync | E01/E15 |
| MRKCorrelationWaiter | 类 | 请求回执关联等待原语 | Register | E04/E15 |
| MRKCorrelationTicket | 类 | 单次请求关联等待 | WaitAsync/Dispose | E04/E09 |
| MRKCorrelationResult | 类 | 收到/超时/取消的可识别关联结果 | State与结果 | E04 |
| MRKExternalOperationTracker | 类 | 外部 OperationId 与未知状态登记 | Register/TryGet | E04/E09 |
| MRKExternalOperation | 类 | 一次外部副作用操作的身份/确认 | OperationId/状态 | E09/E10 |
| MRKExternalOperationObservation | 类 | 外部运行事实快照 | 状态/时间/原因 | E09/E13 |

### 8.3 执行调度 · F19 基础原语（本轮已存在的类型）

| 已有类型 | 形态 | 唯一职责 | 现有主要属性/方法或能力 | 对应需求 |
| --- | --- | --- | --- | --- |
| FCRWorker | 类 | 通用有界工作提交/Worker 生命周期 | Post/Invoke<T>/Submit<T>/GetSnapshot/StopAsync | E01/E03/E06 |
| FCRWorkerOptions | 类 | Worker 并发、队列、溢出及 Byte 预算 | MaxConcurrency/QueueCapacity/OverflowPolicy/PendingByteBudget | E03/E06 |
| FCRWorkHandle<T> | 类 | 通用任务接纳、取消、等待与身份 | Id/Key/Accepted/Completion/RequestCancel/WaitAsync | E01/E09 |
| FCRWorkResult<T> | 类 | 普通 Work 实际状态/异常/业务值 | State/Value/Error/Succeeded | E01/E11 |
| FCRWaitObservation<T> | 类 | 本地等待超时是否发生 | TimedOut/Result | E04/E09 |
| FCRWorkerSnapshot | 类 | 待队列/字节/运行数等指标 | Pending/Active/Rejected/Dropped/Stopping | E06/E13 |
| FCRDedicatedExecutor | 类 | 专用 STA/MTA 的同步 SDK 执行位置 | 执行与 StopAsync | E18 |
| FCRAsyncGate | 类 | 可取消的异步门 | Open/Close/WaitAsync | E04/E15 |
| FCRPeriodicJob | 类 | 周期任务及重入控制 | StopAsync | E15 |
| FCRSharedLease<T> | 类 | 同进程受控共享引用票据 | Retain/Dispose | E07/E10 |

### 8.3 执行管理 · MRK 核心（本轮已存在的类型）

| 已有类型 | 形态 | 唯一职责 | 现有主要属性/方法或能力 | 对应需求 |
| --- | --- | --- | --- | --- |
| IMRKExecutionEngine | 接口 | 统一执行入口基础合同 | StartFlow/Stop/运行观察 | E01/E12 |
| MRKExecutionEngine | 类 | 唯一 Plan/Flow 运行协调者 | StartFlow*/CompensateAsync/GetSnapshot/StopAsync | E01/E02/E11/E12 |
| MRKExecutionContext | 类 | 运行身份、结果与 MDS 上下文视图 | FlowId/Inputs/Data/BoundInputs/TryGetResult/StatesSnapshot | E07/E13 |
| MRKFlowHandle | 类 | 一次 Flow 完成等待、快照、取消 | Completion/GetSnapshot/RequestCancel | E09/E13 |
| MRKFlowResult | 类 | 一次 Flow 的结果、异常、步骤状态 | State/FlowId/Results/CommittedOutputs/GetCommittedOutput | E11/E13 |
| MRKFlowCapacityExceededException | 类 | 在途 Flow 容量限额失败 | 容量异常 | E01/E06 |
| MRKFlowMetadata | 类 | 业务键与 Run 元数据 | Plan/Revision/Business/Correlation等 | E13/E17 |
| MRKStepObservation | 类 | 步骤状态、WorkId、时间和等待理由 | StepId/WorkId/State/Reason | E13 |
| MRKFlowSnapshot | 类 | 流程运行中的步骤计数和状态快照 | FlowId/状态/时间与统计 | E13 |
| MRKExecutionTransition | 类 | 单步状态变迁记录 | StepId/State/Sequence/Timestamp | E13 |
| MRKTraceBatch | 类 | 有界 Trace 增量读取结果 | Transitions/HasGap等 | E13/E16 |
| MRKExecutionTrace | 类 | 运行事实变化的有界轨迹 | ReadSince(lastSequence) | E13/E16 |
| MRKExecutionDebugController | 类 | headless 断点/暂停/单步/继续 | AddBreakpoint/RemoveBreakpoint/Pause/StepOnce/Resume | E09/E16 |
| MRKExecutionPolicy | 类 | 重试/幂等/超时执行策略 | MaxAttempts/Timeout/Idempotent/ExecuteAsync | E09/E11 |
| MRKCompensationReport | 类 | 显式补偿结算与失败详情 | SucceededSteps/Failures/Succeeded | E11 |
| MRKRuntimeSession | 类 | 长期运行会话承载计划与活动 Flow | TryStartFlow/TryActivatePendingPlan/StopAsync | E12/E17 |
| MRKRuntimeSessionSnapshot | 类 | 会话状态观察 | State/活动与负载 | E12 |
| MRKExecutionPlanSlot | 类 | 运行期间固定计划、待激活修订 | Capture/TryActivatePending | E17 |
| MRKExecutionPlanLease | 类 | 运行持有固定计划与修订引用 | Plan/Revision/Dispose | E17 |
| MRKExecutionPlanPublisher | 类 | 受策略约束的 Stage/Activate/Rollback | CompileAndStage*/StageRollback/TryActivate | E17 |
| IMRKPlanPublicationPolicy | 接口 | 发布授权与计划切换安全点 | Authorize/IsSafeToActivate | E17 |
| MRKPlanPublicationReceipt | 类 | 发布结果与诊断 | Accepted/Revision/Diagnostics | E17 |
| MRKRuntimeAccessGateway | 类 | 生产级入口的受控启动/取消/补偿边界 | TryStart/TryStartDebug/RequestCancel/CompensateAsync | E09/E16 |
| IMRKRuntimeAccessPolicy | 接口 | 操作身份授权/能力限制 | Authorize | E16/E17 |
| MRKRuntimeFlowTicket | 类 | 只读或受限观察票据，不暴露裸句柄 | FlowId/Completion/GetSnapshot | E13/E16 |
| MRKFileExecutionJournal | 类 | 单写者本地运行审计文件 | Append/ReadAll/Dispose | E11/E13 |
| IMRKExecutionJournal | 接口 | 执行事件追加和读取 | Append/ReadAll | E11/E13 |
| MRKJournalRecord | 类 | 运行事件、操作身份和因果事实 | FlowId/OperationId/Plan/Step/State等 | E11/E13 |
| MRKJournalRecovery | 静态类 | 运行后判定 Closed/待核对 | Evaluate/EvaluateAll | E11 |
| MRKRecoveryDecision | 类 | 恢复判断和原因 | Disposition/FlowId/Reason | E11 |
| MRKCodeFlowBuilder | 类 | 代码端单 Flow 简洁定义 | Step/StepData/Subflow/Export/Add | UDEV |
| MRKCodeRuntimeBuilder | 类 | 代码端注册能力和流程 | Capability/CapabilityForRequest/DataCapability/Flow/Freeze/Build | UDEV |
| MRKCodeRuntimeBlueprint | 类 | 冻结的多流程编译快照和能力解析 | BuildForDevelopment/TryGet/TryResolve | UDEV/E17 |
| MRKDevelopmentRuntime | 类 | 开发运行门面、结果/停止 | RunAsync/StartFlow/Snapshot/StopAsync | UDEV/E12 |
| MRKCodeRuntimeBuildException | 类 | 开发门面的编译诊断异常 | FlowId/Diagnostics | UDEV/E02 |

### 8.4 执行内核现有枚举总览（**不得重复定义状态枚举**）

| 所在 Owner | 已有枚举 | 使用含义 |
|---|---|---|
| MRK | `MRKFlowState` | 流程终态与运行态：Running、Completed、Failed、Cancelled（注意外部 Unknown 另建状态） |
| MRK | `MRKStepState` | 等依赖、排队、运行、等待、暂停、资源/组等待、完成、失败、取消、Skipped、ResultUnknown |
| MRK | `MRKInputSource` | FlowInput、StepResult、StepOutput；输入绑定的来源 |
| MRK | `MRKExternalOperationState` | Pending、Unknown、Confirmed、Failed；真实外部副作用状态 |
| MRK | `MRKCorrelationState` | Received、TimedOut、Cancelled；一次关联等待结果 |
| MRK | `MRKRuntimeSessionState` | Created、Ready、Stopping、Stopped |
| MRK | `MRKJournalKind` | Journal 事实记录类别，不能作为普通业务事件总线 |
| MRK | `MRKRecoveryDisposition` | NoRecord、Closed、RequiresReconciliation；目前的保守核对结果 |
| MRK | `MRKPlanPublishAction` | Stage、Activate、Rollback 的发布操作类别 |
| MRK | `MRKRuntimeAccessAction` | 授权启动、取消、调试、补偿等受控行为 |
| F19 | `FCRWorkState` | Queued、Running、Waiting、Completed、Failed、Cancelled、Dropped、Rejected |
| F19 | `FCROverflowPolicy` | RejectNew 与 DropOldestDroppable |

**状态对照要求**：F19 Work 状态、MRK Step 状态、Flow 状态、外部 Operation 状态并存且含义不同；例如 `ResultUnknown` 不能简单归并成 `Cancelled`，`Failed` 不能悄然转换成业务 OK。流程总终态如果要新增“需要人工核对”，必须定义与当前 `MRKFlowState` 的兼容规则，而非私建新枚举。

### 8.5 协作项目：Mechanism.Core · MEM/MOL（权威工程与逻辑定义）

| 已存在或源级归属的类型 | 形态 | 唯一责任 / 给 MRK 的契约 | 状态 |
| --- | --- | --- | --- |
| MEMProject / MEMPage / MEMContent / MEMSymbol / MEMGroup / MEMElement | 组织对象 | 唯一工程/复用资源/引用/实例；不得挪到 MRK | 【协】 |
| MEMEngineeringSession / MEMProjectSnapshot | 服务/快照 | 原子编辑、Revision、保存/加载与编译快照 | 【协】 |
| MOLLogicDefinition / MOLControlBlock | 类 | V1结构化 Root/Children/ControlKind；Validate | 【实】 |
| MOLLogicCodec | 静态类 | V1 Logic Aspect Get/Set/Serialize/Deserialize | 【实】 |
| MOLWiringDefinition / MOLLink / MOLBlockEndpoint | 类 | V2自由 Control/Data连接/校验/入口 | 【实】 |
| MOLWiringCodec | 静态类 | V2 Logic 统一 Aspect Get/Set/序列化 | 【实】 |
| MOLBranchCase / MOLLoopDefinition / MOLExpression | 类 | 分支/循环/表达式定义，运行内核不再复制定义树 | 【实】 |
| MOLBlockPosition / MOLSelectionGroup | 类 | 布局/编辑选组数据，非执行节点 | 【实】 |

### 8.5 协作项目：Mechanism.Orchestration · MOC（编译与用户表达）

| 已存在或源级归属的类型 | 形态 | 唯一责任 / 给 MRK 的契约 | 状态 |
| --- | --- | --- | --- |
| MOCEngineeringCompiler | 类 | 固定 Snapshot/入口 Content → MRK Plan + SourceMap | 【实】 |
| MOCCompiledPlan / MOCSourceMap | 类 | Plan与工程 Revision/Step 来源映射 | 【实】 |
| MOCStepOrigin / MOCObservedStep / MOCTraceView | 类 | Block+InstancePath+迭代来源与 Trace 展示 | 【实】 |
| MOCFlowBuilder | 类 | Step/Connect/SetParameter/Move/Edit、ExtractLinearContent | 【实】 |
| MOCWiringFragment / MOCBlockAlignment | 类/枚举 | 复制选择、粘贴/对齐、非执行层 | 【实】 |
| MOCBuildService / MOCBuildResult / MOCDiagnostic | 类 | 校验构建与定位错误；不建立第二 Plan Compiler | 【实】 |
| MOCCapabilityCatalog / MOCCapabilityDescriptor / IMOCCapabilityProvider | 类/接口 | 业务能力搜索和 Schema/版本/Provider 入口 | 【实】 |

### 8.5 协作项目：Foundation.Data + Mechanism.Data · MDS（唯一运行数据 Owner）

| 已存在或源级归属的类型 | 形态 | 唯一责任 / 给 MRK 的契约 | 状态 |
| --- | --- | --- | --- |
| FDEExecutionDataContracts / FDTDataKey<T> | 契约 | 公开类型安全 DataKey 和作用域边界 | 【实】 |
| IMDSDataContext / MDSDataContext | 接口/类 | Get/Set/Return、Search、Inspect；普通节点首选 | 【实】 |
| MDSDataHeap / MDSDataStack / MDSDataFrame | 类 | Heap/多调用作用域/一次提交或放弃输出 | 【实】 |
| MDSDataBindings / MDSDataBindingResolver | 类族 | 输入输出来源、类型与运行映射 | 【实】 |
| MDSDataBudget / MDSDataQuery / MDSDataSnapshot | 类 | 预算、状态/大小、搜索与Inspector后端 | 【实】 |
| MDSDataFiles / MDSDataException | 类族 | 按需文件加载卸载及数据错误 | 【实】 |

### 8.5 协作项目：Visual / Presentation.Wpf / Applications.Studio 与 Host

| 已存在或源级归属的类型 | 形态 | 唯一责任 / 给 MRK 的契约 | 状态 |
| --- | --- | --- | --- |
| MOLFlowLayout / MOLFlowCanvas | 类 | 逻辑画布位置计算、WPF连线/选择/编辑投影 | 【实】 |
| MOLRuntimeOverlay / MOLBlockViewModel | 类 | MRK Trace/状态在当前图上的展示 | 【实】 |
| Applications/Runtime.Host/UnifiedRuntimeHost | Host源码 | 运行持有、runtime-run/serve/recover、正式授权与重启审查接口 | 【实/基】 |
| Professional.* / Connectivity Provider | 专业实现 | 具体设备SDK、检测算法、异步RPC、真实停止确认 | 【协】 |

### 8.6 全类架构必须避免的重复命名

| 不建议新建 | 既有来源/应做改造 | 原因 |
|---|---|---|
| `FlowScheduler`/`ProcessWorkerManager`/`UniversalExecutionService` | `MRKExecutionEngine + FCRWorker` | 三方面不是三个执行器 |
| 第二套 Graph/Flow 的持久对象树 | `MEMContent Logic + MOLWiringDefinition` | 工程只有一个定义真源 |
| `RuntimeDataManager2`、第二 DataStack/Heap | `MDSDataContext/Heap/Stack/Frame` | 数据 Ownership/生命周期只认 MDS |
| WPF 自己的 NodeRuntime/状态机 | `MRKExecutionTrace + MOCSourceMap` | UI 是投影，不是执行真相 |
| 新 `CompileAndRunManager` | `MOCBuildService + MRKCompiler/Publisher` | 同一条编译发布链路 |
| 通用万能 `EventBus` | 现有 Channel/事件 Owner + MRKTrigger/Correlation | Command/State/Event/Stream 语义不同 |
| 各专业的第二 F19 Worker 框架 | F19 共享机制 + Capability 声明资源约束 | 不因 Vision/CT/PLC 自建线程池总管 |
| 全局 Runtime 单例 | Host/会话明确持有 Runtime 实例 | 生命周期、权限、测试和多产线隔离需要实例级 Owner |

## 9. 缺口反推：建议的最小新增契约与现有类改造点

本章全部属于**设计候选**，不能因为出现在目录中就判作已实现，也不能直接让 Codex 一次批量创建空壳类。**先写红灯反证，证明现有类无法承接，再采用最小增构**。

### 9.1 执行控制的关键新增候选

| 候选/修改位置 | 必要性、职责与关键字段/方法（目标） | 复用什么 | 必须先验证 |
|---|---|---|---|
| `MRKControlRegion` **【设；可能内部类】** | 已编译一次可多轮复用的子 Plan/Body，RegionId、BodyPlan、Mode/ExitPolicy/MaxIterations；激活当前轮、退出/继续和分支清理 | 现有 `MRKExecutionPlan`、Engine 调度、MOL Kind/MOC 编译 | 相同多步骤节点能第 N 轮重复执行、不中途重复释放资源 |
| `MRKInvocationContext` **【设；可用现有 Context 扩充替代】** | InvocationId、ParentInvocationId、Iteration、CallPath、Frame；分支/子流程/迭代实例唯一 | 现有 `MRKExecutionContext`、`MDSDataStack.OpenFrame`、`MOCStepOrigin` | 相同 StepId 在不同轮/不同 Content 使用实例绝不串结果 |
| `MRKExecutionEngine` **【实，需改造】** | Region/Invocation 激活、完成/异常结算、继续/退出时资源清理、Stop/补偿归属 | 已有 Flow/Step/依赖/Worker/策略 | 新能力**不能**再造第二 Engine 或旁路 F19/MDS |
| `MRKExecutionPlan` **【实，需扩展合同】** | 明确 Action/DAG 与 Region 的可编译表示；Plan 冻结后不变 | 同一 Compiler | 大 Foreach 不静态构造百万 Step |
| `MRKExecutionOrchestrationCompiler` / MOC **【实，需扩展】** | V1/V2 结构化控制、跨层参数、条件 Join、Try/Exit 的确定语义；统一诊断 | MOL+MOC/SourceMap | 同一逻辑的代码/画布输入得到同义 Plan |

**优先级判据**：Region/Invocation 是目前最可能无法用“只改参数”解决的真缺口；但是否需要两个 public 类、还是在内部扩展当前 Context，要由源级设计与实际 TDD 决定。`If`/`Switch` 可共用 Branch 机制，`For`/`While`/`DoWhile` 共用 Loop，所有专业动作仍通过 Capability/Provider。

### 9.2 执行调度的修改优先于新增类

| 既有类 | 需要补齐的行为 | 原则 |
|---|---|---|
| `FCRWorker` / `FCRWorkerOptions` | 公平性、低优先级不无限饥饿、严格在途字节预算、可取消等待接纳 | 仍只负责 Work，不能解释 Content |
| `MRKExecutionGroups` | WholeFlow/Stage/Resource 串行作用域的编译落地、资源与组配额的稳定顺序 | 不能用全局 Lock 代替细粒度限额 |
| `MRKResourceLeaseCoordinator` | 外部 Unknown 的所有权转移与确认、跨进程安全边界由 Host 提供 | 软件租约不代替实体联锁 |
| `MRKCorrelationWaiter` / `MRKExecutionTrigger` | 去重、晚到、周期触发的长期在途容量和取消清理 | 事件传输真源仍归事件/协议机制 |
| `FCRDedicatedExecutor` | STA/MTA/同步 SDK 真实线程亲和压力验证 | 不许声称 Task.Run 保证同步 SDK 安全 |

### 9.3 执行管理的必要可靠性契约

| 候选/现有对象 | 需要补齐 | 唯一 Owner / 安全边界 |
|---|---|---|
| `MRKExecutionTrace` / `MRKFlowSnapshot`【实，扩展】 | 关联每轮 Invocation、精确 Reason、Gap/增量观察、低开销分级 | MRK 产生运行事实；Studio 观察，不复制引擎 |
| `MRKExecutionDebugController`【实，扩展】 | Region 内单步、Break/Continue/Return 等安全点、取消中受控撤销断点 | Debug 仅逻辑/专业安全点；真实设备安全交给 Provider |
| `MRKExecutionPolicy`【实，扩展】 | Try/Catch/Finally 区域与补偿的确定结算、幂等重试/ResultUnknown 禁止盲重试 | 不能以默认 Retry 重发非幂等 PLC/运动命令 |
| `MRKFileExecutionJournal`【实，扩展】 | 大量 Flow 的稳定索引、压缩/归档、Checkpoint 事件、崩溃记录因果 | 本地单写者；跨进程写权由 Host 负责 |
| `IMRKExecutionCheckpointStore`【设，接口候选】 | Save/Load/Validate 已验证的流程执行游标与 Revision/Operation 事实 | MDS 持有 DataSnapshot/可持久引用；Host 选物理存储，MRK 定义恢复所需控制状态 |
| `MRKExecutionCheckpoint`【设，数据契约候选】 | FlowId、Plan/ManifestRevision、Region/InvocationCursor、CommittedOutputRefs、ExternalOperations 与校验/SchemaVersion | 不能序列化活 CLR 委托/线程/失效 Lease |
| `MRKJournalRecovery`【实，改造】 | 在 `RequiresReconciliation` 保守判定基础上，追加可证明幂等时安全恢复的决策与授权 | 现有 `EvaluateAll` 只是安全核对，不是断点续跑 |
| `MRKExecutionPlanSlot/Publisher`【实，扩展】 | 正式 Plan 仓库、已发布 Manifest 校验、可恢复激活版本 | Release/审批/持久仓库由工程构建/Host 保管；MRK 负责固定 Revision 和安全点 |
| `MRKRuntimeAccessGateway`【实，扩展】 | 可信用户身份/租约角色区分、远程 ReadOnly Observe、审计并发控制 | 不能把字符串角色模拟批准器直接用在生产 |
| `MRKDevelopmentRuntime`【实，薄扩展】 | `RunValueAsync<T>`（候选）单结果轻便入口和结构化失败 | 不增加第二个 Engine/全局 Manager |

**Checkpoint 最小风险策略**：起步只允许经过验证的纯计算/可证明幂等步骤从安全边界恢复；外部 `OperationId` 未经 Provider 确认，一律保留 Unknown 并拒绝重播。保存数据用 MDS 可持久化引用或明确值快照，不能把 `IntPtr`、图像显存指针或临时数组引用写进 Journal 当作可跨进程恢复。

### 9.4 建议新增枚举：能不用就不用

目前确有需要的未来语义：Region/Invocation 的退出原因（Normal/Break/Continue/Return/Error/Cancel），可由内部有限状态/既有 Result 表示。**禁止先设计一大串新状态枚举再找场景**。只有完成真实状态机反证与 ABI 评审后，才考虑内部 `MRKRegionExitKind` 或观察级别 `MRKObservationLevel`；不将当前 `MRKFlowState`、`MRKStepState` 直接废弃重建。

### 9.5 必须坚持的依赖方向

```text
 Foundation.Infrastructure(F19)  ←  Mechanism.Runtime(MRK)
 Foundation.Data 契约          ←  Mechanism.Runtime(MRK)
 Foundation.Data 契约          ←  Mechanism.Data(MDS实现)
 Mechanism.Core/MOL           ←  Mechanism.Orchestration(MOC)
 Mechanism.Runtime(MRK)       ←  Mechanism.Orchestration(MOC编译适配)
 Mechanism.Orchestration/MRK  ←  Applications.Studio(编排/观察)
 Mechanism.Runtime/MRK       ←  Applications.Runtime.Host(运行承载)
 Professional/Provider       →  通过注册和运行委托被 MRK 消费
```

箭头表示主要“消费已有能力/契约”的方向（不是完整 csproj 强制重排图）；所有具体依赖改动需要现有 `Verify-OwnerDependencies.ps1` / `CxVisual.Unified.sln` 实测。禁止 MRK 反向引用 Studio，禁止 F19 认识 MEM/CxNode，禁止 Core 直接依赖运行引擎，禁止 MRK 直接静态依赖全部 Professional 程序集。

## 10. 关键类接口规格：真实属性与目标改造分开看

### 10.1 类族职责与常用方法（现有可调用）

| 类/接口 | 当前直接可用的典型签名/成员 | 使用条件、验收边界 |
|---|---|---|
| `MRKCodeRuntimeBuilder` | `Capability(...)`、`CapabilityForRequest<TRequest,TResult>(...)`、`DataCapability(...)`、`Flow(string,Action<MRKCodeFlowBuilder>)`、`Freeze()`、`Build()` | 开发/模拟入口；能力必须是真回调，编译失败不能启动 Flow |
| `MRKCodeFlowBuilder` | `Step(id,capabilityId,params after)`、`StepData(...)`、`Subflow(...)`、`Export(name,stepOutput)`、`Add(request)` | 控制依赖与数据端口语义不同，不自动猜值 |
| `MRKDevelopmentRuntime` | `RunAsync(flowId,object request,token)`、`StartFlow(...)`、`Snapshot()`、`StopAsync(...)` | 完成 Task 不等于 FlowState.Completed；开发门面非生产授权入口 |
| `MRKFlowResult` | `State`、`Error`、`Results`、`CommittedOutputs`、`GetCommittedOutput<T>(name)` | 只在成功提交时允许读取命名输出；大对象复制策略由 MDS 安全适配决定 |
| `MRKExecutionEngine` | `StartFlow(...)`、`StartFlowWithData(...)`、`StartFlowWithControl(...)`、`CompensateAsync(...)`、`StopAsync(...)` | 保留唯一 Engine；原计划归属和 DataScope 释放必须严格验证 |
| `MRKExecutionDebugController` | `Pause()`、`Resume()`、`StepOnce()`、`AddBreakpoint(id)` | 现有暂停点在派发前，不能把运行线程冻在带锁的设备 SDK 调用中 |
| `MRKExecutionTrace` | `ReadSince(long lastSequence)` | 每次增量观察需要处理 Gap；不默认复制图像/体数据 |
| `MRKExecutionPlanPublisher` | `CompileAndStage(...)`、`CompileAndStageOrchestration(...)`、`CompileAndStageBlueprint(...)`、`TryActivate(principal)`、`StageRollback(...)` | 只控制软件发布版本和受保护 Slot；物理安全点由受信 Host/Provider 决定 |
| `MRKJournalRecovery` | `Evaluate(flowId,history)`、`EvaluateAll(history)` | 只能判断 Closed/RequiresReconciliation 等，不宣称已完成执行续跑 |
| `FCRWorker` | `Post(...)`、`Invoke<T>(...)`、`Submit<T>(...)`、`GetSnapshot()`、`StopAsync()` | Work 语义不包含完整 Flow 逻辑，Backpressure 和资源保活要独立检验 |
| `MOCFlowBuilder` | `Step(...)`、`Connect(...)`、`SetParameter(...)`、`Edit(...)`、`ExtractLinearContent(...)` | 只修改 MEM Content Logic；旧 V1 不能无提示改写成 V2 |
| `MOCEngineeringCompiler` | `Compile(MEMProjectSnapshot,entryContentId,resolver,instancePath)` | 工程→MRKPlan+SourceMap；不重复造执行计划解释器 |
| `MDSDataContext` | `Get<T>`、`Set<T>`、`Return<T>`、`Search(...)`、`Inspect(...)` | Return 是 pending，成功提交后才给下游读；大对象要有明确 Owner |

### 10.2 执行控制的 Region/Invocation（目标伪代码，不是现有公开签名）

```csharp
// 【目标内部设计示意：不要求新公开 API】
// 由 MOC 将 Loop/Branch/Try 的权威 Logic 编译成计划的控制区域。
// 同一 RegionBody 编译一次，每轮新建InvocationId和MDS Frame。
// 调度仍走唯一MRKExecutionEngine → F19 worker。
// EC 激活 Work，ES 派发，EM 写Trace/Journals；本轮正常/Break/
// Continue/Return/Cancel时必须结算Frame、租约、补偿责任。
```

必须明确三种交接契约：

- `ControlDecision` 不直接携带大数据，只引用明示的端口与来源。
- `Invocation` 持有自身的 Frame/运行身份，子流程退出时由 MDS 成功 Commit 或 Abort。
- `Stop/Cancel` 请求能被执行过程观察，但“资源已可复用”必须经本地任务实际结算或 Provider 权威安全确认。

### 10.3 实施前必须审查的现有类限制

| 事实/限制 | 源码位置 | 不允许作出的推断 |
|---|---|---|
| V2自由 Wiring 校验要求以 Action 为主 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` `MOLWiringDefinition.Validate()` | 不能因 V1 已支持 Loop 而认定 V2 已支持可任意拖动的动态循环 |
| V1部分多步循环有 `MaxSteps=4096`、编译期展开 | `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | 不能把静态展开当作高迭代可复用 Region |
| `ExtractLinearContent` 仅线性 Control 且拒绝 Data 边界 | `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | 不等于任意端口完整提取与参数重映射 |
| `MRKJournalRecovery` 遇无终态/Unknown 要求核对 | `Mechanism/Runtime/Execution/MRKExecutionJournal.cs` | 不等于已可从 Checkpoint 自动/授权续跑 |
| `MRKExecutionPlanPublisher` 用策略与软件 PlanSlot | `Mechanism/Runtime/Execution/MRKExecutionPublishing.cs` | 不等于正式持久 Manifest、可信用户服务、真实硬件安全点均已完成 |
| `MRKDevelopmentRuntime` 回 `MRKFlowResult` | `Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs` | 不等于已实现拟议的 `RunValueAsync<T>` 单结果薄入口 |

## 11. 三类完整业务样例：从用户操作反推真实公共能力

### CASE-FE01：软件开发者单请求、按 Key 串行的普通业务

**开发者期望**：后台算法普通提交，单产线同 Key 顺序执行；多产线共享 Worker 并行。默认只传业务函数和业务 Key，有界队列、明确拒绝或等待策略，失败返回事实。当前 F19 已有 `Post/Invoke/Submit`、`FCRWorkerOptions` 等基础；不要为了单个结果强制用户编写 MEM 工程。正反例：A Key 内先后顺序、B Key 并行、队列满明确拒绝、取消未开始工作后预算返还、同步 SDK 不能假装异步。

### CASE-FE02：X-Ray 双产线三层主/子流程（R1 与 R3 统一标杆）

**软件开发者表达**：注册 `vision.acquire/correct/ai/cv`、`inspection.judge/recheck`、`mes.report`；用 `Flow/Step/Subflow/StepData/Export` 构造能力编排；高级选项只声明 `Detector`、`LineKey`、AI 组并发上限/队列/显存预算。复杂 If/Loop/Join/多子 Content Data 映射未闭合部分须先落地，不能用示例伪装已完成。

**工程编排用户表达**：在 Studio 引用同一专业能力和子 Content，以 Control/Data 线、并行模板、条件/循环区域定义逻辑；采用来源选择器自动提示类型化端口；保存/重开，同图仿真与运行观察、Trace 下钻。

**统一结果条件**：两个入口对同样输入+同一 Provider/配置必须相同的输出/错误类型、控制选择、Iteration/Invocation 来源、StepId/BlockId 反查；不要求线程时序完全重合。换 LineA/LineB 时权限/资源/业务身份不混淆；对重复相同 SN 仍有不同 FlowId/OperationId；外部 AI 超时迟到结果不得写到下一产品。

### CASE-FE03：CT 长任务 + PLC/运动 + AI跨进程（可靠性反证）

**目标流程**：设备启动前安全准入 → 持有运动/X-Ray资源 → CT 采图（专业时序由运动卡控制）→ 重建 GPU 任务 → 结果/3D 处理 → 检测 → 上传。中间 AI/重建可为长时外部 Job，Studio 可断开。设备外部操作需 OperationId；停止请求后若设备仍执行，租约必须保留或转移到可信 Host/Provider 接管，不能早释放。

**故障注入**：数据文件损坏/SDK线程阻塞、GPU队列满、网络断线、结果迟到、Host 被杀进程、Journal断尾、正在发布新版本、重启时遇到未知设备命令。可确认纯计算部分允许安全恢复；不能确认的硬件阶段需要人工/Provider 核对。验证 CT 大体数据只记录引用/大小/来源，不被 Trace 默认复制。

### CASE-FE04：Automation/IoT/Inspection 跨专业等待链

**目标流程**：PLC/IoT事件触发→读取状态→下发动作命令→按Correlation等待回执→视觉结果→业务分支→Inspection记录。分离 Command、State、Event、Stream：可靠事件投递归既有事件机制，专业设备实际动作归 Provider，MRK 负责等待、关联、控制/资源/异常结算。重复回执、请求先发后注册、迟到/断线和非幂等命令重试都是必须测试的负例。

## 12. 完整验收：用户体验、核心正确性、TDD、性能与安全

### 12.1 验收等级，不混淆

`S0 文档设计 → S1 源码存在且通过编译 → S2 组件正反例 → S3 开发者/Studio/Host 端到端 → S4 对应 SHA 的 Windows CI → S5 长稳/跨进程/必要现场硬件与安全确认`。E01～E18 的来源标签不能代替这些验收等级。目标完成并非“类数达到 XXX”。

### 12.2 EC 执行控制反证

| 测试 | 成功行为 | 失败/恶意行为反证 |
|---|---|---|
| EC-T01 Sequence/DAG | A→B→C 严格依赖，不相互插队 | A Failed、后继不误执行；提交/数据端口不误判断 |
| EC-T02 Branch/Switch | 命中一个 Case，Default 正确，未命中 Skipped | 非法表达式、多个 True、缺 Default 等能给确定诊断 |
| EC-T03 Loop/Foreach | 多步循环 N 次，按轮拥有独立 Frame | 100万迭代不静态百万 Step；Break/Continue/Return、非法条件、取消正确 |
| EC-T04 Parallel/Join | All/Any/FirstSuccess 在明确策略下正确结算 | 失败 Winner、尾部晚到、取消与资源释放无串账 |
| EC-T05 三层子 Content | 公开输入输出/Result，实例路径正确 | 缺 Port、递归引用、不同产线共享写、失效 Frame 拒绝 |
| EC-T06 V1/V2/代码同义 | 输入、控制分支、结果和 SourceMap 可比 | 无提示 Schema 转换或忽略不支持 Kind 不通过 |

### 12.3 ES 执行调度反证

| 测试 | 成功行为 | 失败反证 |
|---|---|---|
| ES-T01 提交/背压 | 任务/Byte 限额、Rejected/等待可取消 | 队列满静默丢弃、预算泄露、无限排队不通过 |
| ES-T02 组/资源 | 同 Key FIFO、不同 Key 并行，资源租约独占 | Stop/Timeout 后错误重入资源不通过 |
| ES-T03 异步请求 | 回执快速到达/断线/重复/晚到都能正确关联 | 等待期间 Worker 被错误持满或回执错配不通过 |
| ES-T04 公平和吞吐 | 长稳下低优先级能按约定获得机会，指标有界 | 高优先级无限注入导致永久饥饿不通过 |
| ES-T05 SDK位置 | 专用 STA/MTA 按真实 SDK 约束执行 | 在线程错误或 UI 线程执行危险调用不通过 |

### 12.4 EM 执行管理反证

| 测试 | 成功行为 | 失败反证 |
|---|---|---|
| EM-T01 取消/Stop | 请求状态可观察，等待者释放，最终结算/Unknown 明确 | 把 RequestCancel 当 DeviceStopped 不通过 |
| EM-T02 重试/补偿 | 幂等重试；显式逆序补偿并记录失败 | 未经核对自动重复运动指令不通过 |
| EM-T03 Snapshot/Trace | 50+ 并发 Flow 正确关联、断档重同步 | 不同 Invocation 混用输出、Gap 被忽略不通过 |
| EM-T04 Journal | 因果顺序、断尾修补、完整帧损坏拒绝 | 重复终态、跨Flow的 OperationId 误确认不通过 |
| EM-T05 Checkpoint | 安全幂等纯计算可恢复，外部 Unknown 待核对 | 自动重放不可判定硬件动作不通过 |
| EM-T06 计划切换 | 旧 Flow 固定旧 Revision，新稿授权安全激活 | 调试者越权绕过网关/编辑态直接修改 Running Plan 不通过 |
| EM-T07 独立自治 | 关闭 Studio，Host 继续运行；重新连接正确观察 | UI 退出、网络断线即停止生产不通过 |

### 12.5 双角色简便性指标（真正体现“便捷友好”）

| 指标 | 建议目标（待实践量化，非当前成绩） | 检查方法 |
|---|---|---|
| 单次普通 C# 工作 | 常见 Submit/Wait/Result 在 1～3 行完成，不手动 Thread/Lock | 与直接 Task 方式比较样板代码行数 |
| 典型 Flow 建立 | 除业务能力注册和 Flow 声明外，不额外创建5个 Manager | 用真实示例统计 API 对象/步骤 |
| 单输入单输出 | 支持业务 DTO 直传、直取结果的**薄入口** | 与当前字典结果读取 API 并列验收 |
| 80%～90% 常见工程操作 | 期望使用默认/业务参数面板完成（目标非承诺） | 对实际案例做任务完成率测量 |
| 同图调试定位 | R3/R4 点击出错节点即可看到真实 Flow/调用/原因 | 两产线、多轮、错配和 Trace Gap 反证 |
| 开发/编排一致 | 同能力、同配置、同逻辑，结果/错误可对照 | 自动化差分回归，不比较线程的偶然顺序 |
| 执行性能开销 | UI/Trace 的附加吞吐、GC、内存有测量基线 | headless / Observe / Debug 三模式对照 |

### 12.6 已有测试入口、持续集成和提交判定

- `Tests/FoundationSmoke`：F19 的 Post/Invoke/Submit/Queue/专用执行线程/Key 串行/取消。
- `Tests/ArchitectureSmoke`：ExecutionFlow、Control、Compiler、Group、Policy、Port、Compensation、Journal、Trigger、Identity、Placement、RuntimeSession、DeveloperExperience；另有 MOC/MDS/Organization Logic 与 Studio WPF 回归。
- `Tests/TestCenter/Cases/TCTRuntimeReliabilityCases.cs` 等：真实生产 Owner 黑盒、故障注入、证据输出，不能自己写第二个测试版 Engine。
- 工作流：`.github/workflows/unified-runtime-windows.yml`、`engineering-organization-windows.yml`、`testcenter-windows.yml`。每次变更按**同一提交 SHA** 与独立 Workflow 判断。

**本轮读取 CI 为历史切片**：仓库 `master` 在审查过程中仍持续推送；可能出现 Runtime 成功而工程组织失败、TestCenter 成功等不同结论，不能合并宣称整仓最新 SHA 全绿。文档只描述源码事实、测试入口和需证明的结果，不标记本次未亲自执行的 S4/S5。

## 13. 收敛次序：先双角色闭环，再改类，不为数量而开发

| 顺序 | 对象与用户价值 | 主要 Owner | 完成判据 |
|---|---|---|---|
| P0-0 需求/源码查重 | 逐类对照本文与现有源码/四大 Excel 的实际行和 owner | 架构审计与 TestCenter | 一条需求一个主责、没有同名并行实现；准确状态 |
| P0-1 轻便入口 | 现有 Builder/Runtime 上自然的单输出、错误与专业能力复用 | MRK + MOC/Capability | 最小样例编译+测试，代码/编排同源 |
| P0-2 统一控制 | V1/V2 的 If/Switch/Loop/Join/Invoke/异常域建立同义语义 | MOL/MOC + MRK | 保存重开→编译→执行→定位；编译拒绝不支持语义 |
| P0-3 Region/Invocation | 多轮重用 BodyPlan、每轮独立身份/数据、并行控制安全 | MRK + MDS + SourceMap | 高迭代/多实例测试，取消/异常不漏资源 |
| P0-4 子 Content 数据接口 | 非线性、多输入/输出、数据 Port 参数映射与实例复用 | MEM/MOL/MOC/MDS | 可从 Studio 提取/调用并重开、代码等价 |
| P1-1 调度长稳 | 公平/预算/串行范围/真实 SDK 线程及资源未知处理 | F19 + MRK + Provider | 高吞吐/随机取消及多设备冲突反证 |
| P1-2 运行事实+同图调试 | 每轮 Trace/断点/SourceMap/MDS Inspector，远程只读观察 | MRK + MOC + Studio | 精确定位 LineB 第37轮、Trace断线重同步 |
| P1-3 正式恢复 | 版本固定、Checkpoint/Journal、可信授权、外部确认/人工核对 | MRK + MDS + Host/构建 | 可恢复者续跑；不可确定者不重发 |
| P2 最终交付 | 确定性 CI、开发者/Studio/Host、压力与专业设备测试 | TestCenter + 各原 Owner | 每项明确 S0～S5 等级、没有伪通过 |

**不要分成 9 套子体系**：以上是增量切入点而不是新增项目；各项工作采用已有 Core/Orchestration/Runtime/Data/F19/Studio Owner，新增类须说明不可被现有对象承接的具体失效测试。

## 14. 最终架构判断与来源索引

### 14.1 三方面是否已经够完整？

**够作为一级划分**。执行控制负责“定义好的控制逻辑在运行时如何推进”，执行调度负责“已就绪工作如何派发与等待”，执行管理负责“运行身份/状态/失败与可靠性”。原 E01～E18、199 细项、R1～R6、32 项使用体验、21 项质量目标都能一一找到主责或协作，不需要第四个“大运行体系”。

### 14.2 “少类”不等于阉割能力

对用户展现完整能力（单步、并发、条件、区域、组、资源、错误、观察、补偿、恢复），但缺省只暴露业务最必要的 3～5 个概念：**能力、流程、输入、输出、执行结果**。**底层复杂性由平台公共机制承担**，并提供进阶显式设置；不能反过来把内部资源/状态全部暴露给每个专业用户。

### 14.3 如何继续裁决

下一轮评审建议依次看：①三方面功能是否完整、是否重复；②R1/R3 完整样例是否自然可实现；③现有类型是否已有相同能力；④真正需要增构哪些控制区域/执行身份/Checkpoint 契约；⑤逐类对照四大 Excel 和最新源码后定最终签名与落地包。**需求先于类型，真实测试先于“已完成”标签。**

### 14.4 固定来源索引

- GitHub 仓库：[oudream/cx-visual-x3](https://github.com/oudream/cx-visual-x3)，审查快照 `b8daf3847b9c0241b3d511ba012676446539d400`。
- **四大文档**：`DOCS/01_CxVisual_平台灵魂与架构宪法_V1.0_Review.md`；`DOCS/02_CxVisual_01至05_统一规格基线_V4.9_*.xlsx`；`DOCS/03_CxVisual_三方面逐阶段 · 原结构归属完善 V2.01.xlsx`；`DOCS/04_CxVisual Foundation与Mechanism 统一架构与对象API设计规格 V6.4.xlsx`。其中 02/03/04 本轮未逐格核查，不能对未读表格作新证据声明。
- 原三份运行体系：`DOCS/Architecture/CxVisual_统一运行体系_六角色期望与并发异步编排执行_综合设计讨论稿_V0.1.md`、`...六角色差距审计与内核增构_V0.5.md`（正文 V0.6.1+）、`...开发者简洁入口与编排共源_V0.1.md`。
- 重要落地记录：`DOCS/Testing/UnifiedExecution_TDD_Matrix_V0.1.md`、`DOCS/Testing/CxVisual_统一运行体系内核_实现与验收_V0.3.md`、`DOCS/Testing/CxVisual_工程构建体系_G01至G04_真实实施与验收记录_V1.0.md`、`DOCS/Architecture/CxVisual_统一工程组织体系_完整架构设计与开发使用指南_V1.1.md` §7.4。
- 本次需求继承源：当前对话提供的 `/mnt/data/CxVisual_流程执行体系_完整功能需求清单_V1.0.md`（E01～E18 原子编号稳定）。

---

**文档总结**：**一套 MRK 执行内核，三个能力方面，两个友好入口，一份工程真源，一套执行语义，一个可追溯运行事实**。原则是功能不缺、体验自然、边界清楚、类不泛滥，最终用真实 Windows 与跨专业案例证明。



---

# V2.1 增补：三方面类归属、30分钟工作包与第一轮实施证据

> 增补日期：2026-10-19；保留前述 V2.0 全文、E01～E18 的 199 条需求、双角色用法和全类分析，不替换、不缩减。仓库：`oudream/cx-visual-x3`。本增补为实施事实与持续跟踪工作表，未来按对应提交 SHA 更新，而不把未完成目标改写为“已实现”。

## A. 三方面最终架构职责

| 范畴 | 作用 | 执行 Owner | 仅协同不接管的 Owner |
|---|---|---|---|
| EC 执行控制 | 顺序/依赖/条件/分支/循环/并行/子流程/Join/区域退出 | Mechanism.Runtime / Execution，唯一 MRKExecutionEngine | Mechanism.Core 的 MOL 定义、Mechanism.Orchestration 的 MOC 编译、Data 的 Frame |
| ES 执行调度 | 异步/任务/队列/优先级/执行组/资源/背压/位置/触发 | MRK 管 Flow 级调度，Foundation.Infrastructure / Concurrency 的 F19 管原语 | 专业 Provider 约束、Host 真实运行条件 |
| EM 执行管理 | Context/Session/状态/结果/取消/异常/重试/补偿/Trace/日志/恢复 | Mechanism.Runtime / Execution | MDS 数据、MOC SourceMap、Studio UI、Host 持久服务/身份 |

不为 EC/ES/EM 新建三个工程项目；不重建 WorkflowEngine、GraphRuntime、DataManager 或另一套 Worker。公共机制必须完全提供能力，但日常用户只面对能力、流程、输入、输出、运行结果五个核心概念。

## B. 全类重构 / 加强 / 增构的稳定裁决（保留细节见原第8~10章）

| 所属 | 重构 R | 加强 S | 最小必要增构 N，未实装前标候选 |
|---|---|---|---|
| EC / MRK | MRKExecutionEngine、MRKExecutionPlan、MRKExecutionContext、MRKExecutionOrchestrationCompiler | MRKExecutionStep、MRKExecutionPlanCompiler、MRKStepCompileRequest、MRKExecutionInput/Output、MRKCodeFlowBuilder、MRKWorkJoin、MRKExecutionOperations | MRKControlRegion、MRKInvocationContext、必要的内部 MRKRegionExitKind |
| ES / F19 | 首先不强制整体重构 | FCRWorker、FCRWorkerOptions、FCRWorkHandle/Result、FCRDedicatedExecutor、FCRAsyncGate、FCRPeriodicJob、FCRWorkerSnapshot | 原则上不新建公开调度器，必须有现有类不能承载的失败测试才讨论 |
| ES / MRK | 优先保留现有执行组、资源模型 | MRKExecutionGroups、MRKExecutionGroupDefinition、MRKResourceLeaseCoordinator、MRKCorrelationWaiter、MRKExecutionTrigger | 不增加第二套调度管理器 |
| EM / MRK | MRKJournalRecovery、必要的 MRKExecutionContext 调用层次调整 | MRKFlowResult/Handle/Snapshot、MRKStepObservation、MRKExecutionTrace、MRKExecutionDebugController、MRKExecutionPolicy、MRKCompensationReport、MRKRuntimeSession、MRKExecutionPlanSlot/Publisher、MRKRuntimeAccessGateway、MRKExternalOperationTracker、MRKFileExecutionJournal、MRKDevelopmentRuntime | MRKExecutionCheckpoint、IMRKExecutionCheckpointStore；MRKRecoveryCoordinator/IMRKExternalOperationReconciler 保留待反证候选 |
| 协作：Core/MOC | MOLLogicDefinition、MOLWiringDefinition、MOCEngineeringCompiler | MOLControlBlock/BranchCase/LoopDefinition/ExitKind、MOLStepPolicy、MOLStateMachineDefinition、MOCSourceMap/Origin、MOCFlowBuilder、MOCBuildService、MOCCapabilityCatalog | 暂不创建第二套持久 Flow/Graph 树 |
| 协作：Data/UI/Host | 不为 MRK 移走数据所有权 | MDSDataContext/Stack/Frame/Heap/Budget、MOLFlowCanvas/RuntimeOverlay、Host 观察/可信身份 | 仅协作契约，具体增构归属原 Owner |

已有 `MRKFlowState`、`MRKStepState`、`MRKRecoveryDisposition`、`MRKExternalOperationState`、`MRKInputSource`、`FCRWorkState`、`MOLControlKind` 等枚举不得重复发明；新增 Region 内部退出枚举必须先验证必要性。实体是否需要新类，以测试能够证明的职责为依据，而非按三个方面机械新增。

## C. 每轮约30分钟的明确工作包和验收门槛

| 轮次 | 主攻目标 | 完成的最小闭环 | 关键复用与禁止 | 预期 Owner |
|---|---|---|---|---|
| R01 | 开发者单值返回 | 具名 Export 成功、字典输入、失败/私有输出/类型错反证 | 保留 RunAsync，全局不新增引擎 | MRK、ArchitectureSmoke |
| R02 | V1/V2 结构控制编译 | If/Else、Switch、Join 行为对齐；同一能力一致输出 | 不能把无语义边当循环 | Core/MOC/MRK |
| R03 | 结构化控制 Region 骨架 | 既有 DAG 兼容，非法结构编译拒绝，区域生命周期 | 不建立第二执行器 | MRK/MOC |
| R04 | Invocation & 每轮 Frame | 同一 Step 多次运行，身份/数据隔离和取消清理 | MDS 唯一数据 Owner | MRK/MDS |
| R05 | 动态多节点 Loop | 运行时复用 BodyPlan；For/While/DoWhile/Foreach、Break/Continue/Return | 不能静态展开数十万节点 | MRK/MOC |
| R06 | 多层 Content 可复用调用 | 参数/返回/实例路径/多调用者数据隔离、保存重开 | 不偷读子流程私有输出 | Core/MOC/MDS/MRK |
| R07 | Join + 等待 + 失败边界 | All/Any/FirstSuccess 尾部归还、异步取消与异常/补偿 | 外部 Unknown 不误判停止 | MRK/F19/Provider |
| R08 | 调度配额与长稳 | WholeFlow/Stage/Resource、背压、优先级公平性、资源有序租约 | 不复制 F19 Worker | F19/MRK |
| R09 | 运行观察/同图调试 | Invocation Trace、SourceMap、迭代/子流程断点、Gap 重同步 | Studio 只消费 MRK 事实 | MRK/MOC/Studio |
| R10 | Journal + Checkpoint | 确定性持久写入、数据引用与版本核对、崩溃未结案拒绝重放 | 不擅自恢复危险硬件 | MRK/MDS/Host |
| R11 | 授权与安全恢复 | Provider Unknown 核对、安全检查与可证明步骤的授权续跑 | 生产身份不使用模拟字符串 | MRK/Host/Provider |
| R12 | 综合验收和性能 | 开发者、Studio、Host、TestCenter、长期故障注入和回归 | 编译、单测、UI、现场级别分开记证据 | TestCenter/各 Owner |

一轮约30分钟是**目标工作量**，不是复杂控制流工程必须30分钟完成的保证。跨体系改造可细分 R0x-A/B/C；任何一轮不得仅创建类型壳/写测试名称就标完成。下一轮准入必须满足：①原 Owner 代码落地 ②正反例自动化 ③文档与变更摘要 ④固定 SHA 的 Windows CI 验收。若依赖的其他会话持续推进 master，使用乐观版本检查并核对其引入的真实功能，不对已落地部分重复设计。

## D. 软件开发者与工程编排用户的最终验收路径

**软件开发者**：简单工作可直接 F19.Post/Invoke/Submit；进入流程时只需 `MRKCodeRuntimeBuilder` 声明 Capability/Flow/Export，`MRKDevelopmentRuntime.RunValueAsync<T>` 得到强类型业务结果；需要完整诊断时仍使用 `RunAsync()` 的 `MRKFlowResult`；生产必须走发布和授权入口。高级特性按需配置执行组、资源、重试、Trace，而不是成为每个业务函数的强制参数。

**工程编排用户**：创建/打开 MEM Content → 选择 Capability → 在 WPF Studio 同图放置节点与 Control/Data Port → 设置参数、条件、策略 → MOC 诊断与构建 → MRK 仿真 → 在原图观察、断点、单步、下钻子流程 → 保存、重开、回归。V1/V2 同义控制、复杂子流程跨层数据和结构化循环仍需逐轮真实交付；不得把已有 Action DAG 和有限 CPU 演示称为完整工业编排。

两者必须使用同一能力契约、同一个 MRK Plan 语义以及相同的真实执行结果。大体积 Image/Volume 通过 MDS 管理引用/Handle，绝不复制进入全部 Trace/Event。

## E. R01 第一轮：已提交的具体实现

- **原基础**：`MRKCodeRuntimeBuilder.CapabilityForRequest<TRequest,TResult>`、`Flow().StepData().Export()`、`MRKFlowResult.GetCommittedOutput<T>` 已存在，但通常开发者要手写 `State == Completed`、`result.Results` 字典查找和强制转换。
- **改动**：在 **现有** `MRKDevelopmentRuntime` 添加两个泛型重载：`RunValueAsync<TResult>(flowId, request, outputName, cancellation)` 和 `RunValueAsync<TResult>(flowId, inputs, outputName, cancellation)`；复用 `RunAsync` 返回的真实 `MRKFlowResult`，并强制只从具名已提交 Flow Export 取值。没有新建 Runtime/Manager/Engine，也没有绕过正式 Host 授权。
- **正例**：单请求与字典输入分别返回既有 `Export("answer", "step.Result")` 的 int 值。
- **反例**：未声明 Export、私有 Step 输出、输出类型不符、空白输出名称、真实 Flow 失败。失败须保留 FlowId、状态和原始异常；Cancelled 明确以 `OperationCanceledException` 表达，不能冒充业务成功。
- **源码**：`Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs`；`Tests/ArchitectureSmoke/RuntimeDeveloperExperienceTests.cs`。
- **文档**：`DOCS/Architecture/CxVisual_流程执行体系_需求归属_双角色使用与分轮实施_V2.1.md`。
- **提交**：[`204ca58ba0b280d9e077663d0008f8b1b7833652`](https://github.com/oudream/cx-visual-x3/commit/204ca58ba0b280d9e077663d0008f8b1b7833652)。
- **验收**：本机未配置 Windows .NET48 构建；固定 SHA 需通过 `.github/workflows/unified-runtime-windows.yml`，以及对应工程组织/TestCenter 相关工作流，不得以历史 success 取代。最终状态见本增补末尾 CI 核查记录。

**R01 目标用法（真实签名，开发/模拟入口）**：

```csharp
// 已通过 DataCapability + Flow().StepData().Export("answer", "step.Result") 注册和 Build
int value = await runtime.RunValueAsync<int>("double", (object)6, "answer");

// 明确多个 Flow 输入时，使用 IDictionary<string, object> 重载
int value2 = await runtime.RunValueAsync<int>("double",
    new Dictionary<string, object> { { "request", 6 } }, "answer");

// 需要完整运行事实和诊断，继续使用原生 API
MRKFlowResult full = await runtime.RunAsync("double", (object)6);
```

**成功定义**：新 API 的 `Task<TResult>` 仅在 Flow **Completed 且具名 Export 有效**时返回结果；失败/取消不会被当作成功值；生产网关维持原安全边界。

## F. 验收及状态更新规则

| 等级 | 定义 |
|---|---|
| S0 | 需求/设计已写下；未证明有真实代码 |
| S1 | 源码实现已提交，签名与路径可查 |
| S2 | 固定 Commit Windows/.NET48 Release x64 编译通过 |
| S3 | 指定自动化测试通过，正反例已核实 |
| S4 | 开发者与 Studio/Host 交互/跨进程闭环通过 |
| S5 | 压力、故障、权限、长稳及必要实机验收通过 |

第一轮是否能标 **S3** 由本轮 SHA 对应 Windows Runtime CI 决定，不以“方法存在”或“此前 CI 绿”替代。至 R12 仍未签收的安全/分布式特性必须显式保留未完成状态，不得以软件模拟成功替代现场验收。



### R01 首次 Windows 反证与修订记录

- 初次提交：`204ca58ba0b280d9e077663d0008f8b1b7833652`。Windows `.NET48 Release x64` 构建成功，但新增 ArchitectureSmoke 异常链测试失败：测试把业务根因错误地限定为第一层 `InnerException`，与既有 MRK Step/Work 异常包装行为不一致。
- 修订提交：`cafd52a0cec1d936e79f540ed28f36cc34a14ad4`。修改测试，使其验证异常链最深处保留原始业务异常，同时检查外层携带 `FlowId` 和 `Failed`，**不删测试、不吞异常、不修改生产执行语义**。
- 首次对应 CI：[Runtime](https://github.com/oudream/cx-visual-x3/actions/runs/39273211753) = failure；[工程组织](https://github.com/oudream/cx-visual-x3/actions/runs/39273211810) = failure；[TestCenter](https://github.com/oudream/cx-visual-x3/actions/runs/39273211756) = success。
- 修订后必须依据 `cafd52a0...` 所对应 Windows CI 判断，不得用首次失败前的旧绿色记录代替。


### R01 修订后的 Windows 验收快照（固定 SHA）

固定提交：[`cafd52a0cec1d936e79f540ed28f36cc34a14ad4`](https://github.com/oudream/cx-visual-x3/commit/cafd52a0cec1d936e79f540ed28f36cc34a14ad4)。

| 工作流 | Run | 本次核查结果 |
|---|---|---|
| Unified Runtime Windows TDD | [39273928919](https://github.com/oudream/cx-visual-x3/actions/runs/39273928919) | **success**，R01 执行入口与 ArchitectureSmoke 通过 |
| 配置公共底座 | [39273929580](https://github.com/oudream/cx-visual-x3/actions/runs/39273929580) | **success** |
| Config L1 Windows | [39273929047](https://github.com/oudream/cx-visual-x3/actions/runs/39273929047) | **success** |
| Engineering Organization Windows | [39273929316](https://github.com/oudream/cx-visual-x3/actions/runs/39273929316) | **success**，工程回归与 WPF 相关验收通过 |

由对应 Runtime CI 支持 R01 的 **S3（Windows 编译与自动化测试）**，不代表 R02～R12 或专业设备 S5 已完成。


**最终 R01 验收结论**：固定 SHA `cafd52a0...` 的上述四项独立 Windows CI 均成功。R01 可标记 **S3（代码+自动测试验收）**；本次修订提交未单独触发 TestCenter Windows 工作流，不能把它列为同 SHA 已通过。R02 仍为下一轮，不在 R01 中冒充交付。
