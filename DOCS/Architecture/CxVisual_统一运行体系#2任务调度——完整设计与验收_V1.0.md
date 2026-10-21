# CxVisual · 统一运行体系 · #2 任务调度——完整设计与验收 V1.0

> **性质**：完整需求、架构边界、现有 API 审计、目标契约、双角色完整使用案例、实施与验收主规格（设计工作稿，待架构评审）。**完整设计 ≠ 功能全部实现或验收。**  
> **版本**：V1.0 完整设计稿；2026-10-21。承接本仓原《#2任务调度——完整设计与验收 V1.0》（初简版），保留其全部需求并深化；不另外设立第二份权威需求。  
> **源码审查基线**：`oudream/cx-visual-x3`、`master`，起始核验提交 `87225ccfff74832ea6617b10d69ac326bb6f14cd`；每轮实际状态与 Windows CI 以**新提交 SHA**再次核验。  
> **适配**：.NET Framework 4.8 / C# 7.3 / x64，Studio WPF；现有 Foundation / Mechanism / Professional 层和项目结构不变。  
> **最高原则**：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。** 能力完整、职责明确，普通场景自然简洁，高级场景配置充分；软件开发者与工程编排者共用同一运行语义。  
> **证据标签**：`【实】`当前源码中可定位；`【验】`在对应历史 SHA 存在 Windows CI/回归证据；`【部】`部分实现，行为范围有限；`【设】`目标方案/候选 API，未实现；`【协】`别的 Owner 负责；`【待核】`目前没有足够源码、测试或现场证据。**出现于本文代码块中的“目标 API”不能当作当前 SDK 使用。**

## 导读与五大部分

- **第一部分（0～4）**：定位和证据、目标与完整职责、需求场景及作用、软件开发者与工程编排者期望、总体架构和 Owner 边界。
- **第二部分（5～12）**：身份/状态、提交接纳、队列与公平、并发/组作用域、资源仲裁、容量背压、时间/异步/执行位置、故障与回收，覆盖实际工作规则和反例。
- **第三部分（13～16）**：现有/待加强/候选的类全貌、真实 API 与目标契约、可直接编译的开发者示例、Studio 从编辑到运行的完整使用路径。
- **第四部分（17～18）**：多个专业实例、独立和跨体系验收矩阵（单元、集成、Windows/WPF、故障、长稳、实机分级）。
- **第五部分（19～20）**：R08 分轮落地、工作包及验收门禁、需求→Owner→API→用例→证据追踪台账与未决事项。

---

# 第一部分｜目标、职责、需求场景与总体架构

## 0. 文档定位、约束、证据与验收级别

### 0.1 本文回答的六个问题

1. 为什么需要统一任务调度、会在哪些工业场景发挥作用？
2. 调度到底拥有哪一层任务/资源/策略，不能越过哪些 Owner？
3. 六类主要功能（接纳、排序、公平、执行组、资源、时间）怎样在一个运行实例中协同？
4. 软件开发者只写少量 C# 怎么用；工程编排者不懂线程/信号量也怎样用？
5. 现有类与真实可调用 API 到了什么程度，下一步增构到底修改哪里？
6. 如何通过正常、失败、竞态、规模化、专业/设备案例证明“能用”，而不只是“有类”？

### 0.2 规范来源与裁决次序

1. 仓库根 `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md`。
2. `DOCS/01～04` 统一架构基线、`DOCS/Architecture/CxVisual_统一运行体系#0总体架构——完整设计与职责划分_V1.0.md`。
3. `DOCS/Architecture/CxVisual_统一运行体系#1流程执行体系_需求归属_双角色使用与分轮实施_V2.1.md`（含 R07/R08 实施历史和固定 SHA 证据）；统一运行体系其余 #3～#6 边界文档。
4. 实际 `Foundation/Infrastructure/Concurrency/`、`Mechanism/Runtime/Execution/`、`Mechanism/Orchestration/`、`Mechanism/Data/`、`Applications/Studio/`、既有测试与 Windows CI。

文档冲突时，已明确批准的架构约束优先；**现有源码只用于证明目前实现事实**，不能默默推翻已定目标。新接口/行为须完成评审和真实回归后再称已实现。

### 0.3 完成度五级

| 等级 | 定义 | 不能越级表述 |
|---|---|---|
| S0 需求 | 功能行为、边界、指标和反证已明确 | “设计完成”不能称“代码完成” |
| S1 源码 | 真实类型与实现路径已定位，API 存在 | 不等于成功编译或正确行为 |
| S2 测试 | 正反例写成真实可运行测试 | 不等于测试运行通过 |
| S3 Windows | 固定 SHA 的 Windows x64 编译、回归、日志、ExitCode 全绿 | 不代表真实 Studio 全量交互或实机 |
| S4 用户/专业 | Studio 真操作 + 一项或多项专业模拟 E2E、性能及失败验证通过 | 不等于真实设备安全合规 |
| S5 现场 | 目标硬件、真实驱动/授权/联锁、长稳和运维签收 | 只对通过的设备/现场/版本有效 |

### 0.4 不可突破的原则

- 唯一执行内核：`MRKExecutionEngine`；调度器**不能成为第二套运行引擎**。
- 只使用唯一 MEM/MOL 工程定义、MOC 计划与 SourceMap、MDS 的 Heap/Stack/Frame；不得再造 Graph、运行数据仓或 UI 专属状态机。
- MRK 负责调度策略和运行级约束；F19 提供 Worker/Queue/Gate/Dedicated 等可复用并发原语；Professional 的真实算法、GPU 驱动、PLC/运动安全属于专业 Owner。
- 不能把 `Accepted` 当成已运行，`Timeout` 当成外部未执行，`CancelRequested` 当成设备已停止，`Lease` 当成授权或安全联锁。
- 计划、运行实例、策略、资源申请、观察值都应有固定身份与可追踪来源；安全相关的未知状态默认保守拒绝。

## 1. 目标与完整职责

### 1.1 目标

**任务调度负责：对已准入且依赖条件满足的工作，在有限容量、并发、资源、优先级、时效及安全约束下，确定何时接纳、何时等待、何时取得资源、何时交给执行位置、何时释放，并给出可解释的结果与指标。**

衡量目标：

- **完整**：同时支撑简单后台任务、多 SN 并行、GPU/相机共享、专用 SDK 线程、周期任务、长等待、高并发与复杂资源约束。
- **便捷**：默认任务调用不要求显式操作线程池/信号量；Studio 通过组/资源/策略卡配置，不要求填写内部 GUID。
- **确定**：排队、准入、拒绝、取消、超时、完成的语义无歧义；公平性不以无限吞吐换饥饿。
- **可控**：队列长度、未执行字节预算、组等待配额、运行槽、数据预算有边界；过载以明确状态/事件反映。
- **可观测**：可从 SN/Flow/Step/资源回到队列状态、策略选择、实际时序、等待原因、拒绝代码和原工程定义。
- **可验证**：高负载、随机取消、重入、交叉资源、跨 Flow、重启及现场授权，正反例分层验收。

### 1.2 完整职责地图

| 职责域 | 必须发挥的能力 | 与其他体系的分工 |
|---|---|---|
| A. 接纳与任务入口 | 提交、身份、准入条件、重复/冲突策略、拒绝原因、取消尚未开始工作 | MRK 管 Flow/Task 身份，F19 管 Work Handle |
| B. 排队与排序 | 有界队列、同 Key 顺序、优先级、FIFO、公平、截止期和饥饿保护 | 不是 MOL 的业务节点顺序 |
| C. 并发与执行组 | Worker/全局/Flow/Stage/Step/资源池作用域、互斥及等待限制 | 运行管理固定实例身份，运行控制发 Pause/Cancel |
| D. 资源仲裁 | 设备、SDK、CPU/GPU 槽位、容量单位、租约、原子/有序多资源申请、释放 | 资源软件仲裁 != 现场设备联锁/跨进程所有权 |
| E. 背压与负载 | 多级容量、字节预算、等待预算、拒绝/延缓/可丢弃策略、节流与过载恢复 | MDS 对数据内存负责，不能双计/漏计 |
| F. 时间与唤醒 | 延迟、周期、截止时间、取消信号、外部事件等待、迟到和唤醒边界 | 事件来源/Join 因果归运行协同，硬实时归设备 |
| G. 执行位置 | 线程池 Worker、串行 Key、专用同步 SDK 线程、线程亲和、Host/Provider 的位置能力约束 | 不越过专业 SDK 真实设备安全语义 |
| H. 观察和诊断 | 任务/组/资源快照，指标、队列延迟、Trace 关联、拒绝/等待原因 | MRK 出运行事实，诊断和 Studio 只呈现 |
| I. 生命周期协作 | 工作排空、停止接纳、在途尾部结算、租约与数据释放、重试前重新准入 | 控制/管理/保障负责请求、实例和错误处理策略 |
| J. 策略治理 | 缺省值、继承/覆盖、版本固定、权限边界、仿真和生产不同准入 | MOC 构建验证、Runtime.Host 授权部署 |

### 1.3 明确不负责

- 不决定“采图之后才能检测”等控制流拓扑，不修改 MOL 图，也不自行创建业务步骤；
- 不保存运行值/图片体积、数据所有权和输出提交；
- 不拥有真实 PLC/X-Ray/运动卡急停、安全门、曝光联锁，不保证在指定毫秒停机；
- 不用排队优先级篡改业务依赖、抢占已运行不可中断设备动作；
- 不建第二套跨进程任务队列、设备资源 Owner、执行引擎和权限中心；
- 不把无限等待、存储失败、设备状态 `Unknown` 包装成普通成功。

## 2. 需求场景：什么场景、怎样发挥作用

本章是**需求起点**：每个场景明确输入、资源冲突、调度决策、异常行为和用户可见反馈，而不止罗列“有某能力”。案例 S01～S14 作为后文规格与测试共同索引。

| 场景 ID / 业务情境 | 问题 / 输入 | 调度发挥的作用 | 必须可观察/反证 |
|---|---|---|---|
| S01 X-Ray 10 SN 并行 | 采图→AI/CV→判定；仅 2 个 AI GPU 槽 | 不改变每个 SN 流程；AI 槽按组/资源限额 2，空槽唤醒下一项 | 峰值 ≤2；各 SN 独立；全部正常任务完成 |
| S02 3 探测器采图 | 同时到达的多个工位共享采集资源 | 按相机/采集卡串行 Key、阶段配额及明确占用期安排工作 | 不交叉抢占设备、工作可并行于其他 GPU 分析 |
| S03 AI+CV 混合负载 | AI 使用 GPU、传统算法使用 CPU，Join 后汇总 | 分类资源池限流，CPU 工作不被 GPU 长等待阻塞 | GPU/CPU 独立限额，Join 仍由流程执行/协同负责 |
| S04 高优先级返修插单 | 返修 SN 插队，普通 SN 长时间堆积 | 优先级及公平预算；同 SN/同 Key 严格 FIFO | 高优先级优先但低优先级有限时间获得机会 |
| S05 CT 大体积重建 | 重建任务大内存、有限 GPU/CPU，队列突增 | 并发+待执行字节预算+内存配额协同，超限拒绝或等待 | 内存上界、申请与释放，不能用任务数代替数据字节 |
| S06 PLC 同步 SDK | 厂商 SDK 需固定线程、调用时间不可控 | 选择已有 Dedicated Executor，受控排队与取消 | 线程亲和真实；等待/暂停不伪装物理停止 |
| S07 MES 应答超时 | 提交后 3 秒未收到 ACK | 等待释放执行线程但保留相关性与必要资源；超时可见 | 超时 != 未提交，重复请求须幂等/核对 |
| S08 传输/网络拥塞 | 网络队列、远端服务受限 | 有界排队、降载、失败回馈及恢复加速 | 队列满拒绝不静默丢 SN；数据生命周期归 MDS |
| S09 分批重建 + 在线检测 | 在线检测优先、后台 CT 吞吐占满系统 | 多执行组配额与优先级，后台占用有最低保障 | 前台时延不因后台无限堆积；后台不饿死 |
| S10 多个子 Content 调用 | 两个父 Flow 复用相同子流程 | 相同定义、不同 Invocation 的任务/租约与等待互不混淆 | 取消 A 不影响 B；来源与子节点可追踪 |
| S11 运行过程中人工暂停 | 按 Pause 时节点正在 SDK 内执行 | 运行控制发请求；调度仅阻止后续准入/协同到安全点 | 不能把暂停请求回显为“设备已暂停” |
| S12 任务取消风暴 | 64～1000 个待资源/待执行任务先后取消 | 原子移除排队任务、归还等待计数/字节预算，不伤已有 Holder | 无负数、无租约泄漏、最终可重新接纳 |
| S13 故障后恢复 | 断电前队列尚有设备动作，重启后 Journal 有 Unknown | 运行保障/治理先核对；调度不得自动重放危险任务 | 未核对时拒绝投放；安全恢复路径留有原因 |
| S14 Studio 配置与观察 | 编排者选中 AI 节点，配置组和资源 | 按 Schema 展示可用策略、验证冲突、构建与仿真看真实队列 | 不使用第二图或本地假运行状态，诊断能定位节点 |

### 2.1 核心场景 S01：10 SN 与 2 个 GPU 并发槽

```text
10 个独立 SN/Flow（每个有自己的输入/Frame/Trace）
  └─ Capture（相机/运动硬件安全由专业 Provider 负责）
      ├─ AI（ExecutionGroup: AI-GPU；MaxConcurrency: 2）──┐
      └─ CV（CPU 资源组，可与 AI 并行）───────────────────────┤
                                                           ↓
                                                      Join/判定 → MES

组配额/资源取得成功 → F19 Worker 受理 → 专业 Provider 执行
      ↑            等待组/资源/Worker队列时，指标可见，取消可归还
```

**正确的观测**：`AI-GPU.Active ≤ 2`、`AI-GPU.Waiting` 在负荷时增长但不超限、`Rejected` 与来源可查；Flow 10 个结果/身份互不串账。等待时间和实际业务耗时必须分开统计。

**三重容量问题**：这里的 “2” 可能是两个**逻辑 AI 槽**，不是两个物理 GPU、不是两个 CPU 线程、也不等于图像体积预算。如果某单次推理会消耗大显存，必须同时受专业模型/资源预算和 MDS 生命周期约束。

### 2.2 S02/S03：同一设备资源与不同资源的区别

- 相机 `Camera-1`：互斥持有一次曝光/采图专业事务；业务流程可以并行准备下一 SN，却不能双重使用同一个设备会话。
- GPU `GPU0/AI`：理想上用容量为 N 的**资源池许可**，和 `AI-GPU` 执行组配额各自表达的业务约束不同；必要时合并策略避免无意义的双重排队。
- CPU `CV`：执行组可与 GPU 分开，且应在共享 F19 Worker 并发中获得公平机会。
- 设备停机、超温、互锁是专业 Provider/Host 对准入的进一步限制，不能只通过资源名称阻止。

### 2.3 S07/S13：不能把业务成功与排队完成混为一谈

- 队列受理只是 `Accepted`；工作执行结束不等于 MES ACK 成功。
- 超时只是“观察期限届满”，需要运行保障依据 `OperationId/CorrelationId` 和真实服务器 ACK 核对。
- 进程重启后未核对的外部副作用应标记 `Unknown` 或等待恢复协调器处理，不直接重试运动/下料。

## 3. 软件开发者与工程编排者的使用期望

本章**只设两个角色**。相同编译计划/运行语义，界面和 API 可以不同，但必须看到相同身份、结果、原因和策略。

### 3.1 软件开发者（DEV01～DEV14）

| ID | 实际期望 | 验收可见行为 |
|---|---|---|
| DEV01 | 普通异步工作 1～3 行就能提交 | 不强制先建立 MEM 工程 |
| DEV02 | 有限并发用一个清楚的参数或组 | 只需声明额度，不写散落 SemaphoreSlim |
| DEV03 | 支持串行 Key 和高优先级 | 同 Key FIFO；不同 Key 有明确排序 |
| DEV04 | 提交时立即知道是否受理 | 有 `Accepted`/拒绝错误，不伪造正在运行 |
| DEV05 | 轻松取消排队工作 | 不启动业务回调、不泄漏配额 |
| DEV06 | 等待结果与等待超时分开 | Timeout 后能继续查询真实工作状态 |
| DEV07 | 查看 Queue/Active/Rejected/Bytes | 指标来自原 Owner 快照，非第二套统计 |
| DEV08 | 需要复杂工程时复用 MOC/MRK | 一个真实计划和 Flow，兼容直接编码和 Studio |
| DEV09 | GPU/设备共享可声明资源 | 不用用户自行向每个 Provider 注入散落锁 |
| DEV10 | 复杂任务能指定 Deadline、Priority、Location | 默认简单，进阶参数有完整含义 |
| DEV11 | 全部 API 支持取消/异常/资源释放 | 失败、迟到成功、设备 Unknown 均可区分 |
| DEV12 | 可测试，无需连接 PLC | 注入模拟 Provider/故障/时钟，真实引擎运行 |
| DEV13 | 记录 SN、CorrelationId、Run/Invocation 来源 | 查询/Trace 能追溯到业务输入 |
| DEV14 | 旧 API/示例兼容或明确迁移 | 不静默改变历史 FIFO、容量、停止行为 |

**典型使用深度**：L0 `FCRWorker.Post/Invoke`；L1 `FCRWorker.Submit + handle`；L2 `MRKExecutionEngine` 的组/资源/Step 编排；L3 MOC + Runtime.Host + 授权 Provider。L0/L1 的便捷入口不得自动变为生产设备授权入口。

### 3.2 工程编排者（ENG01～ENG14）

| ID | 实际期望 | 验收可见行为 |
|---|---|---|
| ENG01 | 不需要理解 Task/Thread/锁 | 选业务能力后选择执行组与资源即可 |
| ENG02 | 画布和面板显示真实资源能力目录 | 名称、单位、容量、所有者和安全说明可见 |
| ENG03 | 选择 AI 节点便能配置“最多并发 2” | 组/执行位置/资源策略可校验且可保存 |
| ENG04 | 组作用范围容易辨认 | Step/Stage/WholeFlow 显示占用起止点 |
| ENG05 | 能一眼区分“排队/等组/等资源/运行” | 状态颜色/文本有原因，不能全部显示“运行中” |
| ENG06 | 缺资源、重复定义、超限当场诊断 | 构建拒绝并定位工程节点 |
| ENG07 | 启动/取消/暂停有明确反馈 | 已接受与已生效分开显示 |
| ENG08 | 队列排序/占用可追踪到 SN | 按组、工位、时间和 Flow 查询 |
| ENG09 | 高级选项不挤占常用界面 | 简单/高级编辑器共享同 Schema |
| ENG10 | 同一图支持调试与仿真 | SourceMap/Trace 回到当前节点及子调用位置 |
| ENG11 | 能预演 10 SN / GPU2 压测 | 使用同一 MRK/F19 与模拟 Provider，不用假调度器 |
| ENG12 | 保存/关闭/重开行为一致 | 策略与版本固定，无第二份 UI JSON 工程真源 |
| ENG13 | 浅色/深色、大量节点下仍可用 | 定位、过滤、分组、停靠与诊断可测 |
| ENG14 | 无授权不能触发真实设备 | 仿真和生产通道明确；Host/Provider 最终拒绝 |

**状态声明**：现有 Studio 已有工程/逻辑编辑、自由连线、部分 Join/CPU 仿真/Trace，**完整任务调度属性面板、Stage/WholeFlow 图形编辑、组队列观测和专业运行配置仍属【设/部】**；第 16 章为目标完整用户路径，不冒充已实现的操作步骤。

## 4. 总体架构、Owner 和端到端工作原理

### 4.1 唯一链路

```text
MEM Project / Content + MOL Logic（工程持久真源）
    ↓ 定义：能力/资源/端口/策略意图及稳定ID
MOC Validate / Resolve / Compile（唯一构建桥，SourceMap）
    ↓ 不可变 MRKExecutionPlan / Step / Group / Resource 契约
MRK 运行实例管理（FlowId、Invocation、Revision、权限/Plan固定）
    ↓ 依赖满足、控制允许运行
MRK 任务调度（准入→等待组→获取资源→排入F19→执行→归还）
    ├── F19 Worker / KeyedSerial / PriorityFairness / Queue / Dedicated
    ├── MDS Frame / Stack / Heap（数据值、字节预算与隔离）
    ├── MRK 协同：Event/Wait/Join/Correlation
    ├── MRK 控制：Pause/Resume/Cancel 等协作请求
    ├── MRK 保障：Timeout/Retry/Unknown/Compensation/Journal
    └── Professional：Vision/AI/CT/PLC/运动/通信真实 Provider
    ↓ 真实结果、工作状态、Trace 和 SourceMap
运行诊断 / Studio（只消费事实；不另建执行器/任务仓库）
```

### 4.2 主要 Owner 和禁止越界表

| Owner | 真正拥有 | 调度可以做 | 不可以做 |
|---|---|---|---|
| `Foundation.Infrastructure.Concurrency`（F19） | `FCRWorker`/Worker 选队、队列、Key、公平算法、普通 AsyncGate、DedicatedExecutor | 配置/消费原语 | 持久保存工程 Flow 身份或模拟设备安全 |
| `Mechanism.Core`（MEM/MOL） | 工程、节点、引用、配置意图、Port | 按固定定义读取 | 在调度中编辑业务连线/工作区 |
| `Mechanism.Orchestration`（MOC） | 构建校验、能力/资源契约解析、SourceMap | 提交静态可执行计划 | 在构建器中启动业务线程 |
| `Mechanism.Runtime`（MRK） | 流程实例、调度策略、执行组、运行级资源仲裁、运行事实 | 调度与协调、拒绝不合法计划 | 保存第二份 Logic/业务 Provider |
| `Foundation.Data` / `Mechanism.Data`（MDS） | 数据容器、预算、Frame/Scope/提交屏障 | 消费已许可的数据句柄/配额 | 私建 Heap/复制运行图像以当队列缓存 |
| `Applications.Runtime.Host` / 治理 | 发布激活/授权宿主、目标环境与可信执行边界 | 提供已授权能力与资源注册 | 假定 MRK 进程内 Lease 可以保护多进程硬件 |
| `Professional.*` | 设备会话、真实服务容量、SDK 原语、设备状态和安全联锁 | 报告容量/可用性与真实结果 | 把硬实时保护交给 WPF 或普通 Worker |
| `Studio` / 运行诊断 | 调度配置编辑体验和真实运行观察 | 调用同一 API、显示来源 | 创建平行组/队列真源或虚构执行成功 |

### 4.3 阶段顺序和资源等待协议

**当前【实】步骤调度链**：校验组存在 → 依赖/条件满足 → （控制区域单独推进）→ 等待 Group Lease → 按资源名排序逐个获取 Resource Lease → 绑定 MDS 输入/Frame → F19 Worker 提交 → 等待实际完成 → 验证/提交输出 → 释放数据、资源、组许可。组/资源等待不占 F19 工作线程；`MRKExecutionStep.RequiredResources` 会去重并按 `StringComparer.Ordinal` 排序。

**未来【设】增加 Stage/WholeFlow**：要保留原有相同顺序，防止 Stage 持有 Worker 在内部自等待；在进入作用域前一次性取得或采用不阻塞 Worker 的异步准入，并严格定义子流/重入/取消与嵌套持有方式。不能只增加一个 `Scope` 枚举却不修改生命周期。

### 4.4 身份、时序和不可混淆概念

| 项目 | 当前/目标身份 | 明确区别 |
|---|---|---|
| 工程定义 | ProjectId/ContentId/BlockId/Revision | 可编辑、持久，不能因调度改变 |
| 编译产物 | PlanId/StepId/SourceMap | 可多 Flow 共用、版本固定 |
| 运行实例 | FlowId/InvocationId/Attempt/Session | 每次执行独立；同 Plan 不等于同实例 |
| 调度任务 | WorkId（F19 Handle）/计划 Step | 一个 Step 可多轮激活；不能把 WorkId 用作业务 SN |
| 串行键 | F19 Key/SerialKey | 同 Key 顺序，与 FlowId/业务主键不等价 |
| 资源组 | ExecutionGroupId/ResourceId | 配额域 != 物理设备身份/专业授权 |
| 观察事实 | State/TraceSequence/Origin | 不是 Studio 缓存中的可修改真源 |

---

# 第二部分｜完整功能、行为规则和原理

## 5. 任务模型、生命周期、策略与身份

### 5.1 三层模型，而不是一个万能 Task

1. **业务 Flow**：工程业务的一次执行，拥有 FlowId、固定 Plan、SN/Correlation、业务结果、独立 MDS 空间。归 MRK 运行管理。
2. **调度工作 Work**：F19 Worker 的一次可执行回调，关联一个 Step Activation/Attempt；其排队、Accepted、Finished、Cancelled 归 F19 实现，MRK 负责与 Flow/Step 关联。
3. **专业操作**：相机曝光、GPU 推理、MES 请求等真实操作，可能跨进程、跨网络、不可瞬断；由 Provider/Host 返回状态，不能因 Work Timeout 就认定安全结束。

### 5.2 运行与调度的合法转换

```text
计划校验/权限/组存在 → Admitted(已受理) / Rejected
  → WaitingDependencies（等待业务前置，不是F19排队）
  → WaitingGroup → WaitingResources → Scheduled(F19)
  → Queued(F19) → Running / AsyncWaiting
  → Completed / Failed / Cancelled / Dropped / Rejected
  → 确认输出提交或失败阻断 → Lease/Frame清理 → Flow结果
```

`WaitingDependencies/Group/Resources/Scheduled` 是**层级化状态解释**，具体枚举应以已有 `MRKStepState/FCRWorkState` 为准，不强行把上图全部扩张成新枚举。`Dropped` 仅允许已显式声明可丢弃的非生产关键工作；重要设备业务不得在默认策略下无声丢弃。

### 5.3 最小功能规格（TS05）

| ID | 必须行为与反证 |
|---|---|
| TS05-01 | Flow 固定计划版本、身份和输入，运行期间工程编辑不改变本 Flow |
| TS05-02 | WorkId 与 FlowId/Step/Invocation 能双向关联，但不能混作同一对象 |
| TS05-03 | 同 Plan 运行 10 SN，状态、输入、取消、已提交输出强隔离 |
| TS05-04 | 重入 Loop/子 Content 多次激活不复用错误的 Frame/资源 Owner |
| TS05-05 | 调度配置必须固定有效范围/快照；不允许执行中从 Studio 热改当前计划 |
| TS05-06 | 任务超时/取消请求、Worker 终态、外部设备结果独立存证 |
| TS05-07 | 实例生命周期关闭时必须清算在途工作、订阅、资源和数据作用域 |
| TS05-08 | 调度变化不得修改 MEM Revision，除非用户显式编辑并提交工程策略 |

## 6. 任务提交、接纳、拒绝与取消

### 6.1 接纳的完整阶段

1. **配置静态验证（MOC）**：Capability/Port、组名、资源契约、执行位置、作用域和安全约束合法。
2. **运行实例准入（MRK）**：Host 授权、固定计划、Flow 容量、版本/会话状态合格；应返回清楚的 FlowId 或拒绝原因。
3. **步骤调度准入（MRK）**：依赖达成、Guard 真、当前控制/取消策略允许；否则等待或 Skip。
4. **组和资源准入（MRK）**：异步等待配额及设备软件租约，未获得前不消耗普通 Worker 执行槽。
5. **Worker 接纳（F19）**：检验队列项数、待执行字节预算、停止状态、Drop 资格，`Accepted` 为真或明确终态。
6. **Provider 执行（专业）**：已授权的真实执行，完成和输出需最终验证；被取消不代表外部副作用不存在。

**去重不同于串行**：`SerialKey="Camera-1"` 只是按同 Key 控制执行顺序，不自动拒绝两个相同 SN 的业务请求。业务幂等键/重复 SN 的提交判定属于运行管理及专业业务契约；需要带固定版本和窗口的明确策略。不能用同 Key FIFO 假冒业务去重。

### 6.2 接纳错误结构（【设】契约，而非现有 API）

`AdmissionResult { AdmissionId, FlowId?, WorkId?, Outcome, ReasonCode, SourceId, SnapshotRevision, RequestedAt, PolicyRef, RetryHint? }`。

建议错误域：`SCHED.PLAN_INVALID`、`SCHED.CAPACITY_FLOW`、`SCHED.QUEUE_FULL`、`SCHED.PENDING_BYTES`、`SCHED.GROUP_UNKNOWN`、`SCHED.GROUP_WAIT_FULL`、`SCHED.RESOURCE_UNAVAILABLE`、`SCHED.CANCELLED_BEFORE_START`、`SCHED.LOCATION_UNSUPPORTED`、`SCHED.HOST_NOT_AUTHORIZED`、`SCHED.STOPPING`。错误结构应接入现有诊断 Owner；实施前统一代码和 Schema，**不为错误名新增一套异常体系**。

### 6.3 TS06 必须行为

| ID | 必须行为与反证 |
|---|---|
| TS06-01 | 合法提交返回可查询身份和确定接纳结果；未运行不能称 Running |
| TS06-02 | 无效组/资源/专用 Executor、计划版本错误在执行前拒绝 |
| TS06-03 | 业务去重键明确作用域、有效期、冲突处理和幂等性，不能仅依赖 SerialKey |
| TS06-04 | QueueCapacity/PendingByteBudget/MaxActiveFlows 独立校验并保留原因 |
| TS06-05 | 排队取消立即停止回调启动并归还计数；已运行取消必须协作确认 |
| TS06-06 | 重复 Cancel 不重复释放；取消与准入竞态最终状态唯一 |
| TS06-07 | Runtime/Host Stop 后拒绝新工作、排空或按策略协作取消 |
| TS06-08 | Overflow Drop 仅对可丢弃工作，且旧工作以 Dropped 完成、有真实记录 |
| TS06-09 | 下游拒绝必须传回 Flow/Step/SN 来源，不吞成“成功且无结果” |

## 7. 队列、优先级、公平性和调度决策

### 7.1 已有调度算法的含义

【实】`FCRWorker` 内使用有界待执行队列、`ActiveKey` 串行约束和 `priorityFairnessBurst`。不同 Key 的待执行工作优先选高 `Priority`，但连续超越较老已就绪工作到阈值后需给予最老可执行 Key 机会；同 Key 必须保留提交 FIFO。新增公平性发生在原 Worker 的 `PumpLocked`，不是新增一个外部队列。

**重要限制**：这是*同一个 FCRWorker 实例内部*的公平保护，不证明跨 Runtime.Host、跨物理 GPU、跨组/多资源等待也具有相同公平性。`PriorityFairnessBurst` 不是实时延迟 SLA，不能据此保证具体毫秒数。

### 7.2 目标公平政策

- 简单默认：FIFO（同 Key）+ 有界高优先级让渡（跨 Key）；在现有 Worker 机制上演进。
- 高级模式：业务优先级、等待年龄、紧急度、服务等级、资源权重可配置，但需每种策略明确比较顺序与稳定性。
- 防饿死：不保证绝对等待秒数，除非负载、服务时长、容量、取消条件等假设明确。
- 决策必须可解释：记录所选 Work、优先级、所应用策略、被跳过原因/限额和调度时刻。
- **不允许业务流程越依赖抢跑**：优先级仅比较*已就绪*工作，而不是取消前置条件。

### 7.3 TS07 必须行为

| ID | 必须行为与反证 |
|---|---|
| TS07-01 | 不同 Key 按显式优先级选择，优先级相同时有稳定 FIFO 定义 |
| TS07-02 | 同 Key 永远先提交者先执行，即使后者优先级更高 |
| TS07-03 | 有界公平让渡，低优先级就绪任务在持续高优先级压力下得到服务 |
| TS07-04 | 已运行工作不做未经安全协议的硬抢占；只管理待执行选择 |
| TS07-05 | Key 活动时，其他可运行 Key 不被无效等待占满 Worker |
| TS07-06 | FairnessBurst 的非法值/极值被验证，默认保持旧行为兼容 |
| TS07-07 | 修改排序算法不得破坏 DropOldestDroppable、PendingBytes、Stop |
| TS07-08 | 100/1000 项高并发+取消+提交竞态不出现负配额、重复开始或未结算工作 |
| TS07-09 | 调度决策可用事件/快照追查，但指标不能无限增长或产生热点锁 |

## 8. 并发与执行组：Global / WholeFlow / Stage / Step

### 8.1 必须区分的五种约束

| 范围 | 意图与典型场景 | 取得/释放点 | 当前状态 |
|---|---|---|---|
| Worker 全局并发 | 单个 F19 Worker 最多同时执行 N 份逻辑工作 | Worker 开始/完成一份 Work | 【实】【验】 |
| Flow 准入上限 | Runtime 同时容纳多少个独立 Flow | 创建 Flow/Flow 全终态 | 【实】【验】`maxActiveFlows` |
| **WholeFlow 执行组** | 一批全流程在共享业务资源池同时运行不超过 N（如整线校验机） | Flow 真正进入业务作用域 → 最后一个在途任务结算 | 【设】需要增强 |
| **Stage 执行组** | 采图阶段最多 1、AI 阶段最多 2，后续工序资源可重叠 | 某阶段入口 → 阶段所有被调度动作结算并退出 | 【设】需要增强 |
| Step 执行组 | 某个 AI/SDK 节点独立限额 | 进入节点调度 → 回调实际结算后释放 | 【实】【验】`MRKExecutionStep.ExecutionGroup` |

`WholeFlow` 的组席位**不等于** Worker 活动线程，也不等于 Flow 准入计数；Stage 必须由已编译的稳定结构区域表达，不能依靠 Studio 屏幕分组或随意的“开始/结束”字符串拼凑。

### 8.2 Scope 生命周期与嵌套规则（目标规范）

- **WholeFlow**：同一 Flow 全局只取得一次组席位；同一组下内部子步骤不得重复取得本组席位，否则 `maxConcurrency=1` 会自锁；多个不同 Flow 按策略竞争。
- **Stage**：Stage 持有期跨越区域内并行/等待节点；需要定义嵌套区域合法性（无交叉重叠、有唯一入口/出口、有界等待），不同 Stage 可并行，但如共享真实资源仍由资源仲裁阻止冲突。
- **Step**：保持当前独立组租约；嵌套同组时需统一“共享父作用域持有”还是“禁止重复占用”的语义，**不应暗中额外占两席**。
- **子 Content**：父 Flow 调用共享 Content 时，Stage/WholeFlow 归属 `FlowId + InvocationPath + ScopeId`；两个 Flow 即使同定义也不可共用释放标识。
- **循环 Region**：同一 Stage 是否覆盖整轮或每轮重新申请必须是明确声明；并行 Foreach 更不能为每个迭代重复占用父 Stage 许可。
- **暂停/等待**：如果 Stage 持有设备会话，不能在暂停时偷偷放租约；可释放的 GPU 工作槽必须标明安全的释放与重取点。
- **失败/取消**：退出 Stage/Flow 前等待仍在途的不可协作操作真正结算，或进入 `Unknown` 隔离策略；严禁早释放导致第二工作进入同一设备。

### 8.3 R08-A 的执行语义矩阵

| 场景 | 正确表现 | 错误表现 |
|---|---|---|
| WholeFlow 组额度 1，同 Plan 两个 Flow | 第一 Flow 在作用域内时第二等待，前者完成并释放后进入 | 第二 Flow 部分 Step 提前运行/组重复获取自锁 |
| Stage A 限 1、Stage B 限 2 | 两 Stage 可按各自额度交叠，且入口/出口精确 | 锁整 Flow 导致本来可并行的 B 被无故阻塞 |
| 子 Content 被多处调用 | 每个 Invocation 的 Scope 身份唯一 | 子流程返回释放了父流程组席位 |
| Stage 内 Loop/并行节点 | 作用域租约可跨多子节点与迭代正确共享 | 同组内二次 Acquire、提前 Release、负计数 |
| 取消等待 Stage | 等待计数归还，原持有者不受影响 | 取消 A 影响持有者 B 或留 Waiting=1 |
| Host 关闭 | 停止接纳，持有和等待状态按真实结算收尾 | 强制清零软件计数并声称设备已停 |

### 8.4 TS08 必须行为

| ID | 必须行为与反证 |
|---|---|
| TS08-01 | GroupDefinition 额度/等待上限验证、未知/重复组拒绝 |
| TS08-02 | Step 当前流程的组许可先于资源和 F19 Worker，等待不占线程 |
| TS08-03 | WholeFlow/Stage/Step 作用域明确，租约取得/释放时点唯一 |
| TS08-04 | 内层相同组不重复持有导致自等待；非法嵌套构建阶段拒绝 |
| TS08-05 | 跨子 Content、Loop、Parallel、ForEach 的 Scope/Invocation 隔离 |
| TS08-06 | Stage/WholeFlow 配额与 Worker/Flow 全局准入上限同时正确 |
| TS08-07 | Pause、Cancel、Timeout、Failed、Unknown 下租约不能提前释放 |
| TS08-08 | 64+ 轮等待取消后 Active/Waiting/Rejected 精确，原持有者不被抢占 |
| TS08-09 | GroupSnapshot/资源等待/Flow 状态/Trace 之间不出现自相矛盾 |
| TS08-10 | 显式无组/默认 Step 路径与旧 DAG/R01～R07 行为兼容 |

## 9. 资源管理、容量池、多资源申请与死锁防护

### 9.1 资源至少区分四类

1. **互斥资源（Mutex）**：同一硬件会话、相机、独占 SDK、校准工具；额度 1。
2. **计数/容量资源（Semaphore Pool）**：例如 GPU AI 并发槽 2、GPU 算力单元/重建槽、多个相同设备实例；可用额度 N，申请 units ≥1。
3. **数据预算（Bytes/Memory）**：MDS Heap、图像缓存、CT Volume 句柄、Host GPU 显存预算；不是 `MRKResourceLeaseCoordinator` 的互斥锁可代替的；需明确预算 Owner 和退还时机。
4. **外部权威资源（Device/Host Fencing）**：设备会话、跨进程/主备控制权；必须由 Runtime.Host/Provider 提供跨进程协调、租约代数或防旧主控命令，不可以本地 `SemaphoreSlim` 充当。

### 9.2 已有实现和目标增量

【实】`MRKResourceLeaseCoordinator` 为 `ResourceId -> SemaphoreSlim(1,1)` 的进程内独占仲裁，`AcquireAsync(resourceId, owner, cancellation)`/`TryAcquire`/`CurrentOwner`。`MRKExecutionStep` 资源字符串去重且序号排序，执行时依次申请，释放逆序；Lease `Dispose()` 幂等。

【设】要支持 S01/S05 的**真实“两个 GPU 槽”容量语义**，可以先用现有 `MRKExecutionGroups(maxConcurrency:2)` 实现 **Step 级逻辑配额**，但不能将其假称为跨多个组可复用的全局多单位资源池。正式扩展应在 MRK 当前资源协调 Owner 中添加类型化资源容量注册与额度申请，避免让每个专业私建一个 Semaphore。

### 9.3 多资源全局顺序与风险

- 按稳定全局资源 ID 排序申请，释放逆序，确保所有使用者遵守同一顺序；非法别名和动态资源清单必须在编译期验证。
- `Group → Resource A → Resource B → Worker` 与其它入口一致；不得让 Worker 回调在持锁状态下再次请求自身需要的同组同资源（递归死锁）。
- 若资源集合可在运行时变更，必须使用全组资源“预申请/一次提交”或失败回退，不得只持有 A 无限等 B；优先级提升和等待者公平须明确。
- 取消/超时途中的部分已取得 Lease 必须逆序释放；资源等待过程不占普通 Worker 线程。
- 超过 `ResourceMaxWait` 只能取消**等待**，不得推断已经在运行的设备副作用已完成。

### 9.4 TS09 必须行为

| ID | 必须行为与反证 |
|---|---|
| TS09-01 | 同资源互斥、不同资源允许并行、进程内 Owner 明确 |
| TS09-02 | 资源类型/容量/单位、Scope/回收策略具有稳定契约，默认互斥兼容 |
| TS09-03 | 多资源申请全局顺序、取消/超时/失败释放、无 ABBA 死锁 |
| TS09-04 | 同组重入与跨组/跨 Flow 申请明确禁止/共享/升级规则 |
| TS09-05 | 未注册/错误单位/重复资源/资源撤销时拒绝且无半份持有 |
| TS09-06 | 一个 SN 超时或 Cancel 不误释放另一个 SN 的租约 |
| TS09-07 | 资源 Owner 日志与 Trace 可关联到 Flow/Step/Invocation、持有与等待时间 |
| TS09-08 | GPU 槽和数据字节不是同一配额，不能双重扣减或漏回收 |
| TS09-09 | 跨进程重启时旧主控失效/未知副作用核对归 Host/Provider，不能用进程锁验证代替 |

## 10. 有界队列、背压、资源预算与过载策略

### 10.1 容量分层和调度的预算定义

| 层次 | 计数/预算 | 当前事实 | 目标防护 |
|---|---|---|---|
| F19 Worker | `QueueCapacity`、`PendingByteBudget` | 【实】只计未开始的工作 | 溢出拒绝或只丢可丢任务，带状态/指标 |
| MRK Flow | `maxActiveFlows` | 【实】引擎活动 Flow 上限 | 防整线无限 Flow/数据作用域增长 |
| MRK Group | `MaxConcurrency`、`MaxWaiting` | 【实】进程内步骤组 | 区分 Active/Waiting/Rejected、Scope 全生命周期 |
| MRK Resource | 当前独占许可 | 【实】单资源互斥 | 目标增加池容量、等待配额、多资源仲裁 |
| MDS Data | `MDSDataBudget` 等真实数据空间预算 | 【实】与节点运行有关 | 预算 Owner 负责图像/Volume 数据，不在 Work 复制大对象 |
| Professional | SDK 工作槽、相机/GPU 显存、远端服务额度 | 【协】由真实 Provider/Host 提供 | 注册能力与动态健康/准入策略 |

### 10.2 拒绝/降载行为

- `RejectNew`：默认适用于不能损失生产产品结果的工业流程，返回明确失败并等待上层报警/重试条件；**不是静默丢弃 SN**。
- `DropOldestDroppable`：只允许预先标记为 `droppable` 的 UI 预览/可重取数据，不允许自动丢关键 MES 写入/曝光/结果。
- `Backoff/Throttle`：【设】可延迟生产者准入，必须有最大等待/取消与明确的结果，不能私建无限缓冲。
- **重要字节定义**：`estimatedPendingBytes` 是*尚未运行任务预计排队占用*，不等于真实 MDS Frame/CT 体积内存；预算应以 Owner 定义的生命周期计量。

### 10.3 TS10 必须行为

| ID | 必须行为与反证 |
|---|---|
| TS10-01 | 队列数量与待执行字节双预算，一项超预算直接拒绝且不误丢旧任务 |
| TS10-02 | 排队取消、Drop、Stop、执行开始后准确归还 PendingBytes |
| TS10-03 | 正常任务默认 Reject，重要生产步骤不自动 Dropped |
| TS10-04 | 排队容量、组等待、资源池等待、Flow 活动数互不混淆 |
| TS10-05 | 背压向上游/编排用户反馈原因、建议、时间戳和拒绝计数 |
| TS10-06 | 同一计划并发时按业务租约、MDS 预算与专业容量合理协调 |
| TS10-07 | 负载恢复后 Worker/组重新准入，不能永久停在虚假 Waiting |
| TS10-08 | 1000～10000 项重复取消/过载的内存、时间与配额数值可回归 |

## 11. 定时、异步、执行位置与线程亲和

### 11.1 三种不同时间语义

- `NotBefore/Delay`：未到期不准入，并不保证到期立刻运行；可受组/资源/Worker 等待影响。
- `Deadline/QueueTimeout`：判定是否继续排队或准入；超过期限应拒绝/取消待执行，不代表已运行设备可被强停。
- `ExecutionTimeout/ObservationTimeout`：运行保障规定节点执行/观察范围。超时通知与原工作结束要分别结算，防止资源提前释放。

**时间源**：排队相对时间/等待年龄原则上用单调时钟/可注入时钟；跨服务绝对时间和审计用 UTC，并记录偏移/时钟同步状态。虚拟时钟方案【设】，不可将 `DateTime.Now` 作为全部公平性与 Deadline 的唯一证据。

### 11.2 异步和线程位置

- `await` 不应占住没有意义的 OS 线程，但根据当前 F19 策略，异步 Waiting 仍可能占有**逻辑并发槽**；需明确是否释放/重取组/资源，以免无界等待独占容量。
- `FCRDedicatedExecutor` 面向要求线程亲和的**同步 SDK**。异步 `await` 续体并无自然线程亲和保证，不能把 PLC SDK 改成普通 `async` 后仍假称一直在一条 SDK 线程。
- 外部 MES ACK/人工审批属于运行协同/保障的事件等待协议，必须有 Correlation、回收、超时和迟到拒绝语义；不得绑死一个普通 F19 Worker 线程。
- `MRKExecutionStep.DedicatedExecutorId` 必须与 `SynchronousSdk` 配对；无注册执行位置在 Flow 启动前拒绝。

### 11.3 TS11 必须行为

| ID | 必须行为与反证 |
|---|---|
| TS11-01 | 延迟/周期/截止时间分别定义；延迟不是保证实时性 |
| TS11-02 | 周期任务防重入，停止后不会再启动新周期 |
| TS11-03 | 等待、执行、观察超时分开，超时不伪造外部失败或未执行 |
| TS11-04 | 时钟跳变、截止期已过、取消先到、迟到事件可重复测试 |
| TS11-05 | 异步等待不占 OS 线程；逻辑并发槽占用策略明确 |
| TS11-06 | 专用 SDK 任务始终在同一指定线程，同步调用内真实受控 |
| TS11-07 | 不支持的 Location/设备参数由 MOC/Host 拒绝，不假设回退到随意线程 |
| TS11-08 | 长期等待/跨进程恢复须有 Journal/幂等/授权，不靠内存 TaskCompletionSource 冒充可靠恢复 |

## 12. 异常、取消、结算、隔离及资源清理

### 12.1 失败类别与责任

| 失败类别 | 主导体系 | 调度必须如何配合 |
|---|---|---|
| 拒绝准入/组队列满 | 调度 | 无业务执行，返回拒绝和指标 |
| 排队中用户取消 | 控制 + 调度 | 移除/标记取消，计数归还，不影响持有者 |
| 资源等待超时 | 调度 + 保障 | 释放已获租约；不能当作设备命令没执行 |
| Worker/Provider 抛异常 | 保障 + 专业 | 失败传播，实际完成后才释放资源 |
| 非合作 SDK 超时 | 保障 + 专业 | 标记 Unknown，等待/隔离真实操作，禁止提前许可释放 |
| Flow 取消/宿主关闭 | 控制/管理 + 调度 | 停新单、清等待、在途终结、会话排空 |
| 日志/Journal 写失败 | 保障/Host | 不得默认继续执行危险操作；保留保守错误 |
| 设备不安全/未授权 | Professional/Host | 拒绝投放执行，不能从开发者捷径绕过 |

### 12.2 回收优先序及资源持有事实

1. 先禁止新的危险执行及不受控重试；
2. 对可取消的等待者撤销准入，立即归还排队/等待配额；
3. 已进入 Provider 的操作请求协作取消或执行补偿协议，不冒充强制终止；
4. 必须等待真实运行/在途尾部结算，已完成/未知副作用分别处理；
5. 关闭/提交或撤销 MDS Frame，再以**逆序**释放 Resource、Group Lease；
6. 记录原始异常、Attempt/Invocation/Flow、何时释放、是否需要人工核对。

**当前【实】** `MRKExecutionEngine.RunStepAsync` 在 `finally` 中清理数据 Frame、逆序 `MRKResourceLease`、最终释放 `MRKExecutionGroupLease`；异步 Provider/超时策略具有软件端结算语义。但它没有证明现场设备本体急停或跨进程故障接管。

### 12.3 TS12 必须行为

| ID | 必须行为与反证 |
|---|---|
| TS12-01 | 请求取消、实际取消、执行失败、未知副作用分别计入状态 |
| TS12-02 | Group/Resource/Worker/MDS 各自收尾时序清楚，不能先释放后确认 |
| TS12-03 | 取消等待者不影响正在执行 Holder；多次取消/Dispose 幂等 |
| TS12-04 | 分支失败/Join 尾部必须等安全结算或显式 Unknown 隔离 |
| TS12-05 | 任务超时后迟到成功不可把旧 Flow 已阻断输出重新写入新 Flow |
| TS12-06 | 资源取得一半时取消/异常，所有已得许可无泄漏 |
| TS12-07 | 生产发布/控制权限失败时不触发真实设备动作 |
| TS12-08 | 恶劣日志/磁盘/断网/SDK 长阻塞反证保留完整可追溯原因链 |
| TS12-09 | Stop 排空后 Active/Waiting/Pending/PendingBytes/租约归零，特殊 Unknown 隔离另计 |

---
# 第三部分｜全类架构、公共契约与双角色完整例子

## 13. 全类架构、职责归属、复用与必要增构

### 13.1 归类原则

- **类按唯一 Owner 放置**：并发通用原语属 F19；运行级实例/组/资源/策略属 MRK；工程持久配置属 MEM/MOL；编译规则属 MOC；数据内存及 Frame 属 MDS；专业 Provider 负责真实设备。
- **真实已存在先复用**，若要增构，说明源代码不能覆盖的行为、为何不能扩展已有类、避免新增类的替代办法。不得为每个需求建一个 Manager。
- **对象规格要完整到 API/属性/错误/生命周期/线程安全和测试**，纯算法可无状态静态工具，不把有状态 Runtime 对象变为全局单例。
- **结构升级优先增量兼容**：原 `MRKExecutionStep`、`MRKExecutionEngine` 构造函数已有大量调用方，不为 Scope 扩展破坏既有二进制/源代码契约；新增重载/选项默认应与原 Step 策略完全一致。

### 13.2 F19：现有公共原语和项目落位

| 类型/文件（准确名称） | 目前职能与关键属性/方法 | 本体系动作 | 状态 |
|---|---|---|---|
| `FCRWorker` / `Concurrency/FCRWorker.cs` | `Post`、`Invoke<T>`、`Submit<T>`、`GetSnapshot`、`StopAsync`；原 Key/Queue/公平调度 | 复用、必要时小范围修正排序/队列竞态；不新建 Worker | 【实】R08 优先级公平有 Windows 验收 |
| `FCRWorkerOptions` / `FCRWorkContracts.cs` | `MaxConcurrency/QueueCapacity/OverflowPolicy/PendingByteBudget/PriorityFairnessBurst` | 复用；保留默认 Burst=16 与构造签名 | 【实】【验】 |
| `FCRWorkHandle<T>` / 同上 | `Id/Key/Accepted/Completion/State/RequestCancel/WaitAsync` | 复用 Work 结果与取消观察 | 【实】【验】 |
| `FCRWorkState` / 同上 | Queued/Running/Waiting/Completed/Failed/Cancelled/Dropped/Rejected | 复用，不复制 MRK Step 状态 | 【实】 |
| `FCRWorkerSnapshot` / 同上 | Pending/Active/Finished/Rejected/Dropped/PendingBytes/Budget/Stopping | 复用原计数，不在 Studio 发明第二来源 | 【实】 |
| `FCROverflowPolicy` / 同上 | RejectNew、DropOldestDroppable | 复用；重要任务必须禁用无声 Drop | 【实】 |
| `FCRAsyncGate` / `FCRScheduling.cs` | `Open/Close/WaitAsync` | 复用等待原语；Event/ACK 因果归协同 | 【实】 |
| `FCRPeriodicJob` / `FCRScheduling.cs` | 周期回调、异步停止/防重入 | 复用；复杂持久定时尚需 Owner 判定 | 【实/部】 |
| `FCRDedicatedExecutor` / `FCRDedicatedExecutor.cs` | 同步 SDK 专用线程/有界调用队列 | 复用线程亲和；不复制 SDK 到 MRK | 【实】【验】 |
| `FCRSharedLease` 等既有并发原语 | 进程内共享租约辅助 | 只有需求和语义一致才复用 | 【实/待核】 |

所有上面 F19 类型留在 `CxVisual.Foundation.Infrastructure.Concurrency`，不另建 `Scheduling` 项目。源路径以当前仓库为准。

### 13.3 MRK：现有运行与调度 Owner

| 类型/文件 | 已存在的核心成员/职责 | R08 或后续处理 | 状态 |
|---|---|---|---|
| `MRKExecutionEngine` / `MRKExecutionEngine.cs` | F19 Worker、资源/组、`StartFlow`、`StartFlowWithMetadata`、`GroupSnapshot`、`GetSnapshot`、`StopAsync` | 唯一执行内核；容纳 Scope/组与资源增强入口 | 【实】 |
| `MRKExecutionPlan` / `MRKExecutionPlan.cs` | 不可变计划、Steps、Regions、DataExports | 编译后调度元数据固定、作用域静态检查 | 【实】 |
| `MRKExecutionStep` / 同上 | `Priority/EstimatedPendingBytes/ExecutionGroup/RequiredResources/SerialKey/DedicatedExecutorId/Policy` | 保持 Step 兼容；必要时关联作用域 Descriptor | 【实】 |
| `MRKExecutionGroups` / `MRKExecutionGroups.cs` | `AcquireAsync`、`Contains`、`Snapshot`；组 Semaphore/等待额度 | 扩展作用域的持有管理，优先复用原 Group Lease | 【实/部】 |
| `MRKExecutionGroupDefinition` / 同上 | `Id/MaxConcurrency/MaxWaiting` | 保留 Step 默认，新增 Scope 规则需受控版本 | 【实】 |
| `MRKExecutionGroupLease` / 同上 | 幂等 `Dispose` 归还组配额 | 复用释放语义，避免嵌套重复取组 | 【实】 |
| `MRKExecutionGroupSnapshot` / 同上 | Active/Waiting/Rejected | 在同一真源增强 Scope/等待原因观察 | 【实/部】 |
| `MRKResourceLeaseCoordinator` / `MRKRuntimeCoordination.cs` | 独占 `AcquireAsync/TryAcquire/CurrentOwner` | 资源池容量/多单位申请及复杂取消归原 Owner 增强 | 【实/部】 |
| `MRKResourceLease` / 同上 | ResourceId/OwnerId/Dispose | 复用原身份和幂等释放 | 【实】 |
| `MRKRuntimeSession` / `MRKRuntimeSession.cs` | 会话/活动 Flow 与终止排空 | 调度仅消费管理事实，不新建 Session | 【实】 |
| `MRKFlowMetadata` / `MRKExecutionIdentity.cs` | PlanRevision、BusinessKey、CorrelationId、SessionId、ParentFlowId | 映射业务与审计身份 | 【实】 |
| `MRKInvocationContext` / 同上 | Flow/Step/Activation/Frame/Invocation 身份 | 重入 Stage/Loop/子内容时唯一定位 | 【实】 |
| `MRKExecutionContext` / `MRKExecutionEngine.cs` | FlowId、Step 状态、数据 Frame/Trace、观察关联 | 不建立第二个 Work 状态仓 | 【实】 |
| `MRKExecutionPolicy` / `MRKExecutionPolicy.cs` | MaxAttempts、RetryDelay、Timeout、Idempotent | 执行保障 Owner；调度只接其安全等待/重试结论 | 【实】 |
| `MRKExecutionTrace` / `MRKExecutionControls.cs` | 实际状态变迁/序号观察 | 向运行诊断提供调度状态来源 | 【实】 |
| `MRKExecutionPlanSlot` / `MRKExecutionPlanSlot.cs` | 运行固定 Plan 与热切换门槛 | 防策略热改影响在途工作 | 【实】 |
| `MRKWorkJoin` / `MRKWorkJoin.cs` | All/Any/FirstSuccess/条件观察与尾部结算 | 属运行协同/流程汇合，调度配合，不新建 Join | 【实】 |
| `MRKCorrelationWaiter` / `MRKRuntimeCoordination.cs` | Register/Receive/Wait/Cancel | 事件等待归协同，不在调度保存重复订阅 | 【实】 |
| `MRKExecutionJournal` / `MRKExecutionJournal.cs` | 持久执行事实/恢复入口 | 保障/管理 Owner；调度交付事实，不独自恢复设备 | 【实/部】 |

### 13.4 建议加强的对象契约（不是已存在 API）

只在实际 R08-A/B 测试表明现有 API 无法表达时实现，**建议形态、正式命名、属性和方法都需源码评审**：

| 需求缺口 | 优先实现方案 | 仅在必要时增加的概念对象 |
|---|---|---|
| WholeFlow/Stage 生命周期 | 扩展现有组规则、在 Engine/Plan 中携带 Scope 描述与租约引用 | `GroupScopeDefinition`（概念）、`GroupScopeLease`（概念） |
| 多实例重入/嵌套同组 | 用 `FlowId/InvocationPath/ScopeId` 关联真实持有，规则无歧义 | `ScopeActivationKey`（概念值类型） |
| GPU 多单位资源池 | 扩展现有 `MRKResourceLeaseCoordinator` 的容量契约和 Acquire 重载 | `ResourceCapacityDefinition/Requirement`（概念） |
| 同时申请多个资源 | 统一排序、失败回退、合规重入；优先由原协调器提供批量入口 | `ResourceAcquisitionPlan`（概念） |
| 调度决策可解释 | 现有 Trace/Snapshot 加必要字段，不建平行存储 | `ScheduleObservation`（只读 DTO 候选） |
| 虚拟时钟/可测 Deadline | 在 F19 通用调度原语上抽象受控时间源 | `ISchedulingClock`（候选） |
| Studio 调度 Schema | 使用原 MPM Descriptor/MOL Aspect 与 MOC 校验 | `SchedulingPolicyDescriptor`（Schema 候选，不是新工程树） |

**增构门槛**：先列出至少一个用原代码无法完成的正例、一个失败反例、需要保存/往返的配置、新 API 的调用者以及删除候选对象的替代方案。大对象数目不是架构质量指标。

### 13.5 明确不归任务调度的对象

- MOL Logic/Wiring、ControlRegion 定义与业务步骤编译；
- MDS DataStack/DataHeap 与 GPU Image/Volume 专业数据对象；
- 生产授权 Gate、发布包、交付/回滚/设备 Owner fencing；
- 外部事件可靠投递、Join 因果链、补偿审批/幂等恢复；
- WPF Docking、工程层 Group、可视图元状态与设备操作控件。

## 14. API 级详细设计：真实签名、目标契约、错误和线程安全

### 14.1 现有真实 API（可以按当前源码调用）

```csharp
// 项目：CxVisual.Foundation.Infrastructure；命名空间：Concurrency
public FCRWorkerOptions(int maxConcurrency, int queueCapacity,
    FCROverflowPolicy overflowPolicy = FCROverflowPolicy.RejectNew,
    long pendingByteBudget = long.MaxValue, int priorityFairnessBurst = 16);

public FCRWorkHandle<T> FCRWorker.Submit<T>(
    Func<CancellationToken, Task<T>> action, string key = null,
    bool droppable = false, CancellationToken cancellation = default(CancellationToken),
    int priority = 0, long estimatedBytes = 0);
public FCRWorkHandle<bool> FCRWorker.Post(Action action, string key = null,
    bool droppable = false, CancellationToken cancellation = default(CancellationToken),
    int priority = 0, long estimatedBytes = 0);
public FCRWorkHandle<T> FCRWorker.Invoke<T>(Func<T> action, string key = null,
    bool droppable = false, CancellationToken cancellation = default(CancellationToken),
    int priority = 0, long estimatedBytes = 0);
public FCRWorkerSnapshot FCRWorker.GetSnapshot();
public Task FCRWorker.StopAsync(bool cancelPending = false);

// 命名空间：CxVisual.Mechanism.Runtime.Execution
public MRKExecutionGroupDefinition(string id, int maxConcurrency, int maxWaiting);
public Task<MRKExecutionGroupLease> MRKExecutionGroups.AcquireAsync(
    string id, CancellationToken cancellation = default(CancellationToken));
public IList<MRKExecutionGroupSnapshot> MRKExecutionGroups.Snapshot();
public Task<MRKResourceLease> MRKResourceLeaseCoordinator.AcquireAsync(
    string resourceId, string owner,
    CancellationToken cancellation = default(CancellationToken));
public MRKResourceLease MRKResourceLeaseCoordinator.TryAcquire(string resourceId,
    string owner);
public string MRKResourceLeaseCoordinator.CurrentOwner(string resourceId);

public MRKExecutionEngine(FCRWorkerOptions options,
    MRKResourceLeaseCoordinator coordinator = null,
    IEnumerable<MRKExecutionGroupDefinition> groups = null,
    IMRKExecutionJournal journal = null,
    IDictionary<string, FCRDedicatedExecutor> dedicatedExecutors = null,
    int traceCapacity = 2048, int maxActiveFlows = int.MaxValue,
    long dataPayloadBudgetBytes = 0);
public MRKFlowHandle MRKExecutionEngine.StartFlow(MRKExecutionPlan plan,
    IDictionary<string, object> inputs = null,
    CancellationToken cancellation = default(CancellationToken));
public IList<MRKExecutionGroupSnapshot> MRKExecutionEngine.GroupSnapshot();
```

> 以上摘录接口对应当前源码；为精简展示，使用 `类名.方法` 样式说明从属，**不是可直接粘贴到类外进行编译的接口声明文件**。完整构造参数以第 15 章可运行例子和现有源码为准。

### 14.2 真实 `MRKExecutionStep` 的调度相关入口

当前构造已支持以下可选命名参数（均为【实】）：

```csharp
executionGroup: "AI-GPU",          // 现有步骤级组ID
requiredResources: new [] { "camera-1" }, // 现有进程内互斥资源
serialKey: "camera-1",             // F19 KeyedSerial
priority: 10,                       // F19 Worker 的候选优先级
estimatedPendingBytes: 4096,        // F19 待执行队列估计字节
policy: someExistingPolicy,         // 由运行保障解释
// dedicatedExecutorId / synchronousSdk 必须同时提供，否则构造拒绝
```

**必须严守**：现在 `executionGroup` 字符串仅用于 Step 级取得组；没有真实 `groupScope: Stage` 或 `AcquireManyAsync` API。若调用方填入“Stage”等未经编译的字符串，不代表已获得 Stage 生命周期。

### 14.3 常用参数、默认值和边界

| 属性/参数 | 当前值/范围 | 行为与失败 |
|---|---|---|
| `FCRWorkerOptions.MaxConcurrency` | 整数 ≥1 | F19 同时占用逻辑工作槽上限 |
| `QueueCapacity` | 整数 ≥0 | 未开始工作的数量预算；0 表示不允许排队 |
| `PendingByteBudget` | 长整数 ≥0，默认 `long.MaxValue` | 未开始工作估计字节上限，不是 MDS 真实内存 |
| `PriorityFairnessBurst` | 整数 ≥1，默认 16 | 防不同 Key 长期饥饿；不等于 Deadline |
| `FCROverflowPolicy` | `RejectNew` / `DropOldestDroppable` | 后者只会丢具有 `droppable` 标记的工作 |
| `MRKExecutionGroupDefinition.MaxConcurrency` | 整数 ≥1 | 同组逻辑工作活动配额 |
| `MRKExecutionGroupDefinition.MaxWaiting` | 整数 ≥0 | 本组并发席位不足时允许等待数 |
| `MRKExecutionEngine.maxActiveFlows` | 整数 ≥1，默认 `int.MaxValue` | 运行实例准入容量，不等于 Worker 活动线程 |
| `MRKExecutionStep.RequiredResources` | 资源名列表 | 自动去重、按 Ordinal 顺序获取独占资源 |
| `FCRWorkHandle.Accepted` | Boolean | 是否接纳排队/执行，与完成结果不同 |
| `FCRWorkHandle.WaitAsync(timeout)` | 观察结果 | `TimedOut=true` 时原 `Completion` 仍可能在途 |

### 14.4 待实现的完整公共契约（草案）

**以下是设计伪代码，不是可编译的已实现方法**。实施时对齐现有 F19、MRK、MOC/MOL DTO、统一错误体系，优先原类新增非破坏性重载。

```csharp
// 【设】工程 Scope 定义（概念数据契约，不表示存在该类）
GroupScopeDefinition {
    GroupId, ScopeKind /* Step | Stage | WholeFlow */,
    SourceContentId, EntryBlockId, ExitBlockId,
    MaxConcurrency, MaxWaiting, ReentryPolicy, ReleasePolicy
}

// 【设】容量资源定义（不同于当前独占资源）
ResourceCapacityDefinition {
    ResourceId, Kind /* Exclusive | Counted | External */,
    CapacityUnits, UnitName, Scope, Owner, SafeReleasePolicy
}
ResourceRequirement {
    ResourceId, RequestedUnits, AcquisitionMode, WaitPolicy
}

// 【设】统一查询只读视图（不额外存储事实）
SchedulingSnapshot {
    FlowId, StepId, InvocationPath, WorkId,
    QueueState, GroupState, ResourceState, WaitingReason,
    QueuedAtUtc, StartedAtUtc, FinishedAtUtc,
    GroupId, ResourceIds, RejectedReason, TraceSequence
}

// 【设】面向开发者的便捷入口意向：以现有 MRK/F19 API 包装，不自建引擎
ExecuteWithSchedulingAsync(capability, input, policy, cancellation);
```

### 14.5 线程安全、错误与 API 演化规则

- 并发修改组计数、等待队列、资源 Owner 都必须原子一致；取消回调不在 Worker/Group 内部锁中执行外部用户代码。
- `Lease` 释放幂等；一次申请失败/取消不产生半完成状态，资源/组 `Active/Waiting/Rejected` 快照不应负值或回滚错误。
- `GroupSnapshot` 与 F19 `GetSnapshot` 是**不同粒度的瞬时观察**，必要时带统一采样时间/序号；不能把两个异步快照拼成“严格一致的硬件状态”。
- 结构扩展优先可选参数、只读描述符及能力协商；老工程保存/重开应保持旧 Step 语义，不默认把所有 Step 自动提升为 WholeFlow。
- 未注册资源/无权限/不支持的执行位置只能显式诊断并拒绝，不得静默降级到危险的默认设备调用。

## 15. 软件开发者的完整使用例子

下述两段完整 C# 均只使用**当前真实存在的类型与方法**。它们可作为独立 Console 应用源码在 Windows .NET Framework 4.8/C# 7.3 工程中编译，需添加对应项目/程序集引用；本文件本身并不冒充已执行编译。示例中没有设备授权，全部为 CPU 仿真。

### 15.1 例子 A：普通后台工作（F19）——最短使用、串行 Key、结果、取消、观察

适用：计算、非设备业务后台任务，不需要业务流程结构时不要强迫创建 MEM/MOL 项目。

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;

public static class BasicSchedulingExample
{
    public static int Main(string[] args)
    {
        try { RunAsync().GetAwaiter().GetResult(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static async Task RunAsync()
    {
        FCRWorkerOptions options = new FCRWorkerOptions(
            maxConcurrency: 2, queueCapacity: 8,
            overflowPolicy: FCROverflowPolicy.RejectNew,
            pendingByteBudget: 1024 * 1024,
            priorityFairnessBurst: 3);
        FCRWorker worker = new FCRWorker(options);
        try
        {
            FCRWorkHandle<int> handle = worker.Submit<int>(async token =>
            {
                await Task.Delay(20, token).ConfigureAwait(false);
                return 21 * 2;
            }, key: "product-001", priority: 5, estimatedBytes: 1024);

            if (!handle.Accepted)
                throw new InvalidOperationException("任务未获接纳：" + handle.State);

            FCRWorkResult<int> result = await handle.Completion.ConfigureAwait(false);
            if (!result.Succeeded || result.Value != 42)
                throw new InvalidOperationException("执行失败：" + result.State, result.Error);

            FCRWorkerSnapshot snapshot = worker.GetSnapshot();
            Console.WriteLine("Value=" + result.Value);
            Console.WriteLine("Pending=" + snapshot.Pending + ",Active=" + snapshot.Active);

            // 正式取消已排队的工作：handle.RequestCancel()。
            // 对已运行操作只是请求协作取消，仍须 await Completion 才可判断最终结果。
            // WaitAsync(timeout) 返回观察超时，不会隐式杀死真实业务操作。
        }
        finally
        {
            await worker.StopAsync(cancelPending: true).ConfigureAwait(false);
        }
    }
}
```

**为什么这段简单**：工作使用一个 Worker 入口，调度策略由 Options 管理，结果从 Handle 读取；开发者不用自己组合 `Thread/lock/SemaphoreSlim/Timer`。如需真实设备，必须进入 Runtime.Host/Provider 权限路径而非直接把 `Post` 改为曝光指令。

**必须补测的反例**：QueueCapacity=0 且当前槽满 → 第二项 `Accepted=false`；`WaitAsync` 超时后仍在执行；等待取消后 `PendingBytes=0`；持续高优先级提交时低优先级不永久饥饿。

### 15.2 例子 B：10 个 SN，共享 GPU 并发 2（现有 MRK Step Group）

下面演示**当前已实现**的“Step 执行组并发 2”，并对 10 个 Flow 使用同一个不可变 Plan，要求最终每个 Flow 成功、AI 实际峰值 ≤2。它不宣称已实现可复用 GPU 多单位资源池或 WholeFlow/Stage。

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Runtime.Execution;

public static class TenSnGpuExample
{
    private static int _active;
    private static int _peak;

    public static int Main(string[] args)
    {
        try { RunAsync().GetAwaiter().GetResult(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static async Task RunAsync()
    {
        MRKExecutionStep aiStep = new MRKExecutionStep(
            "AI", new string[0], async (ctx, token) =>
            {
                int current = Interlocked.Increment(ref _active);
                int prior;
                do
                {
                    prior = Volatile.Read(ref _peak);
                    if (current <= prior) break;
                }
                while (Interlocked.CompareExchange(ref _peak, current, prior) != prior);
                try
                {
                    await Task.Delay(40, token).ConfigureAwait(false);
                    return (object)"OK";
                }
                finally { Interlocked.Decrement(ref _active); }
            }, executionGroup: "AI-GPU");

        MRKExecutionPlan plan = new MRKExecutionPlan("XRay-AI-Plan", new[] { aiStep });
        MRKExecutionEngine engine = new MRKExecutionEngine(
            new FCRWorkerOptions(maxConcurrency: 8, queueCapacity: 24),
            groups: new[] { new MRKExecutionGroupDefinition("AI-GPU", 2, 10) },
            maxActiveFlows: 16);

        try
        {
            MRKFlowHandle[] flows = new MRKFlowHandle[10];
            for (int i = 0; i < flows.Length; i++)
            {
                MRKFlowMetadata metadata = new MRKFlowMetadata(
                    businessKey: "SN-" + i.ToString("D3"),
                    correlationId: "XRAY-BATCH-001");
                flows[i] = engine.StartFlowWithMetadata(plan, metadata);
            }
            Task<MRKFlowResult>[] results = new Task<MRKFlowResult>[flows.Length];
            for (int i = 0; i < flows.Length; i++)
                results[i] = flows[i].Completion;
            MRKFlowResult[] completed = await Task.WhenAll(results).ConfigureAwait(false);
            for (int i = 0; i < completed.Length; i++)
                if (completed[i].State != MRKFlowState.Completed)
                    throw new InvalidOperationException("Flow失败：" + i);

            if (_peak > 2 || _peak < 1)
                throw new InvalidOperationException("GPU并发限制失效：" + _peak);
            Console.WriteLine("10 SN completed; observed GPU peak=" + _peak);
        }
        finally
        {
            await engine.StopAsync(cancelPending: true).ConfigureAwait(false);
        }
    }
}
```

**示例可证明**：10 Flow 同 Plan，组限制 2，回调实际完成后组租约释放；可拿 `engine.GroupSnapshot()` 观测组配额。**示例不能证明**：专业 SDK 的真实 GPU 显存、设备安全/授权、Stage/WholeFlow、数据端口及 MES ACK。生产流程还需 MOC Build 和 Provider/Host 等完整链路。

**真实产品补法**：把一个 AI 的单节点 Plan 换成已构建的 MEM Content + MOL Control/Data + MOC SourceMap，业务输入应由 Flow 的公开 Input/Port/MDS Frame 提供；专业算法由已注册 Capability/Provider 执行，不把 AI 逻辑写死在 MRK 核心。

### 15.3 例子 C：独占设备资源与可取消等待（现有 MRK）

下例侧重真实 API 的资源使用方式，不复写设备任务执行：

```csharp
MRKResourceLeaseCoordinator coordinator = new MRKResourceLeaseCoordinator();
using (CancellationTokenSource source = new CancellationTokenSource())
{
    using (MRKResourceLease lease =
        await coordinator.AcquireAsync("Camera-1", "Flow-A", source.Token))
    {
        // 调用经过授权的模拟 Provider。
        // 真实设备要求 Runtime.Host 与 Professional 的会话/联锁确认。
    }
}
```

这是**进程内互斥资源**，不是跨进程/真实板卡 Owner 证明。若资源申请改成两个资源，尽可能在已编译 Step 的 `requiredResources` 中声明，由 Engine 保证排序获取与 finally 回收；不要每个业务回调自己嵌套锁。

### 15.4 例子 D：开发者如何观察失败而不误判

1. `handle.Accepted=false`：意味着未接纳，不要等着它自动运行；查看 `State/Rejected`。
2. `WaitAsync(timeout).TimedOut=true`：意味着观察逾时；原 `Completion` 可能仍在途，不得立即再次执行非幂等曝光/MES 提交。
3. `RequestCancel()`：提交取消请求；若原工作已运行，需等待协作响应。业务可能已产生真实副作用，不能凭 Boolean 推断未发生。
4. `MRKFlowResult.State != Completed`：不得读取/发布当次未提交业务 Export；真实外部副作用须依 OperationId 核对。
5. `GroupSnapshot` 的 Active/Waiting 与 Worker 的 Active/Pending 是不同指标；不要把等待组的人数计入 Worker 排队。
6. 生产 `StartFlow` 的权限约束应由 Runtime.Host/治理网关执行，开发示例不能直接作为对外无鉴权生产接口。

### 15.5 目标开发者 API 的完整使用姿态（设计，不是已实现调用）

```text
L0  普通计算：worker.Invoke(fn, key, priority) → Handle/Result
L1  简单流程：已存在 Capability + Builder/编译 → RunAsync → Export
L2  资源调度：Plan + SchedulingPolicy(GroupScope/ResourceRequirements)
    → Build/Validate → Runtime.Submit → RunHandle + SchedulingSnapshot
L3  生产受控：Authorized Runtime.Host + 资源注册 + Plan Pin
    → 提交/观察/取消请求/核对 Unknown → 审计/恢复
```

目标便捷 API 不持久化第二份 Plan，不跳过 MOC 校验，也不能因“写法短”而绕过发布与硬件联锁。

## 16. 工程编排者的完整使用例子：Studio 同一工程、同一图、同一运行事实

> **性质**：本章是**完整目标用户路径【设】**，结合目前已存在的 Studio 流程/Join/仿真薄入口【实/部】。若 UI 按钮尚未落地，不得将下列动作称为已可操作；应依照第 19 章分轮实施与 WPF STA 验收。

### 16.1 真实工程示例与资源准备

- 项目 `XRayLine-6X`，资源 `InspectionLine`，两个专业子 Content：`AI_Detection`、`CV_Detection`；主 Content `InspectionMain`。
- 由专业/Host 注册 `AI_GPU`（同类 AI 逻辑槽 2）、`Camera-1`（独占 1）、`CV_CPU`（可配置 CPU 额度）、`MES-Service`（外部 ACK 幂等策略）、设备在线/授权状态。
- 工程真源仍是 MEM Project/Content，资源/参数/策略定义由相应 Descriptor/Logic Aspect 承载；布局、停靠/主题保存在 Studio 用户工作区，不提升工程 Revision。
- 运行模型含 `Capture`、`AI`、`CV`、`Join`、`Decision`、`MES`；两支 AI/CV 并行汇合，原图保留稳定 BlockId。

### 16.2 工程编排完整操作（目标体验）

| 步骤 | 用户操作 | 公共能力必须做什么 | 完成证据 |
|---|---|---|---|
| 1 选择工程 | 资源树打开 `InspectionMain`，中央“流程编辑”页 | Studio 从 MEM 同一 Content 加载，不造平行 Graph | 资源/BlockId 一致 |
| 2 添加能力 | 工具箱拖入 Capture/AI/CV/MES Provider 节点 | 查询真实 Capability Descriptor，检查可用性与权限 | 能力不存在明确占位/错误 |
| 3 连接逻辑 | Capture→AI+CV→Join→Decision→MES | MOL 保存 Control/Data 连线，MOC 做类型/必填/依赖校验 | 保存重开不变 |
| 4 配置调度 | 选 AI，在属性面板“调度”选择组 `AI_GPU` | 使用统一 Schema；MaxConcurrency=2、MaxWaiting=10 的策略意图可见 | 无内部 GUID 手输 |
| 5 配置资源 | 选 Capture→`Camera-1`；AI 指明需要 GPU/预算 | 设备资源所有者与容量来源可见，冲突即诊断 | 有效资源表/校验 |
| 6 配置高级 | 展开设置优先级、估计队列字节、等待截止期、Retry/Drop | 默认不展示线程/Lease 内部术语；拒绝无效策略组合 | 正反例、来源清楚 |
| 7 Build | 点击验证/构建 | 由 MOC 解析组 Scope 与资源、生成固定 Plan+SourceMap | 可定位错误码/BlockId |
| 8 CPU 仿真 | 输入 10 个 SN，设 AI 模拟耗时/故障注入 | 同一 MRK/F19 执行，模拟 Provider 不接硬件 | AI 实际峰值≤2，输出隔离 |
| 9 观察执行 | 切 Run/Debug，队列面板按 SN 查看 | 从 MRK/F19 快照和 Trace 投影，不维护第二状态存储 | WaitingGroup 与 Running 分清 |
| 10 故障定位 | 让 GPU 组满、取消 SN-003、模拟 MES Timeout | 观察 Group 等待/Rejected、资源与 Flow 状态，并导航原节点 | 原待执行任务不误执行 |
| 11 调试修复 | 点 Trace/诊断定位 `AI_Detection` → 父调用点 | MOC SourceMap 复原 Content/Block/Invocation；修改进入新 Revision | 旧运行计划不变 |
| 12 保存/恢复 | 保存工程、关闭 Studio、重新打开 | 工程策略依同一 MEM/MOL 保存；UI Docking 布局单独保存 | 相同工程再 Build/运行 |
| 13 生产启动 | 切生产目标，提交授权请求 | Host/Professional 校验发布版本、角色和设备安全，MRK才执行 | 未授权必须拒绝，不发生曝光 |

### 16.3 建议 Studio 布局（沿用现有统一界面基线）

```text
顶部：工程 | 保存 | 撤销/重做 | 构建 | 模拟 | 运行/停止 | 调试 | 模式 | 版本
左侧：资源树、对象树、专业能力工具箱 / 组与资源目录
中央：配置类组合面板 / 图元编辑 / MOL流程编辑（当前内容决定Ribbon）
右侧：属性 | Port | 绑定 | 事件 | [调度]（常用/高级） | 专业参数
底部：诊断 | Flow/SN | Group/Resource Queue | Trace | Watch | 日志 | 测试
```

**“调度”属性最小字段**：`ExecutionGroup`、作用域（Step/Stage/WholeFlow，只有目标实施后才显示）、`MaxConcurrency`、`MaxWaiting`、`RequiredResources`、Priority、默认拒绝策略。当前适用部分应该只显示已实现的 Step 组与有限真实 API；未实现 Scope 应禁用并明确提示，而不是保存无法编译的配置。

### 16.4 运行观察细节（例：AI 占用 2/2）

| 面板字段 | 示例值 | 真正含义 |
|---|---|---|
| SN / Flow | SN-005 / FlowId | 一个业务运行实例，不能用屏幕行号充当身份 |
| Current Step | AI_Detection / BlockId | 原业务节点及子调用路径 |
| Waiting Reason | `WaitingGroup: AI_GPU` | 没得到组席位，不是“GPU SDK 正在计算” |
| Group | `Active=2, Waiting=8, Rejected=0` | 配额快照，不等于 GPU 显存或系统全部线程 |
| Worker | `Active/Pending/PendingBytes` | F19 原 Worker 的逻辑工作与待执行预算 |
| Resource | GPU/CAMERA 当前软件 Owner | 进程内租约，不显示为“安全联锁通过” |
| Command | Cancel Requested / Applied? | 明确受理与生效时间，不强行认为已停止 |
| Trace | `FlowId + InvocationPath + Step` | 追踪到原子 Content/Block，不以名字模糊匹配 |

### 16.5 故障用户路径（必须真测）

1. **错误组**：用户将 AI 组写成 `AI-UNKNOWN`，Build 或启动前明确拒绝，节点高亮并显示可选组，不应静默改成默认组。
2. **容量满**：`AI_GPU.Active=2, Waiting=10`，新项应按策略 Reject 并反馈对应 SN、BlockId、错误原因；不丢旧 SN。
3. **取消等待者**：在 `WaitingGroup` 取消 SN-003；等待计数马上回落，两个已运行 SN 不受影响，实际设备不接收到额外命令。
4. **跨专业故障**：MES ACK 迟到，Trace 保留 ACK 与业务实际完成差异；不能因为某按钮取消就宣布远端没有收到。
5. **热修改**：调度配置从 2 改为 3，Studio 新 Revision 必须重新 MOC Build/受控发布；已经运行的旧 Flow 仍按原固定计划和版本准入。
6. **关闭页面**：用户关闭流程页时，Runtime.Host 上已有授权 Flow 不应被 UI 任意终止；取消订阅以防 WPF 资源泄漏。

### 16.6 双角色等价性验收

同一个 `InspectionMain` 工程：开发者调用 MOC/MRK，工程编排者从 Studio Build/模拟，均得到相同原节点依赖、组配额、资源申请顺序、Flow 结果和 Trace SourceMap。**不能以 Studio 示例画出来的绿色动画代替实际引擎检查。**

---
# 第四部分｜专业业务应用、完整测试与可审计验收

## 17. 典型专业使用案例（从场景反推能力组合）

本章的“案例达标”必须同时满足：**原本业务流程正确、调度发挥真实作用、软件运行有事实来源、失败有明确行为**。所有涉及现场设备的动作须由授权 Provider 执行；模拟通过不代表实机通过。案例编号 C01～C08 用于第 18、20 章追踪。

### C01. X-Ray 多工位检测线（核心综合验收）

**真实业务**：10 个不同 SN，各经历上料/运动到位/采图/AI 检测/CV 检测/综合判定/MES 提交，AI 与 CV 可并行，判定要依赖全部必要结果。可扩展 3 探测器、复判及子流程。

**调度在何处发挥作用**：

1. 同一个相机/曝光会话按互斥资源获准；不同工位支持独立摄取并发；运动与采图的真实顺序由 MOL/MOC/MRK 执行控制保证。
2. `AI_GPU` 组容量 2，不因 10 个 SN 同时准备好而并发启动 10 个推理；CPU `CV` 可使用独立组或 F19 合理并行。
3. 待 AI 的步骤展示 `WaitingGroup`，获得许可前不占 Worker 线程；GPU/相机资源按统一顺序取得与释放。
4. MES ACK 作为专业外部操作，超时进入核对/Unknown，不因 F19 回调结束就认定服务端事务完成。
5. SourceMap + Trace 保留 `SN → Flow → AI/CV子Content → Step/Invocation`，两个调用点不串数据。

**正常验收**：10 SN 全部完成；`peakAI≤2`，相机持有者唯一；每 SN 有自己的最终结果/Trace；等待、运行、业务状态不混淆。

**异常验收**：组容量满、取消排队 SN、任一 AI Provider 失败、另一 SN 成功、MES 超时、软件进程关闭、设备授权失效。无错误结果上传；未知设备副作用不被重试；所有可回收软件租约可见归还。

### C02. 三探测器相机/采集卡协调

**业务**：设备有三个探测器，不同视角采图可各自执行，但同一传输链路/采集卡有独占阶段，采集的组合结果进入统一 X-Ray 检测流程。

**调度决策**：为独立探测器分配必要的 Key/互斥资源；共享链路建立特定 Stage/资源组合；待所有所需采图动作完成才允许后续业务节点——后者由流程执行/Join 判断，调度不自行猜测图像完整性。

**反证**：同设备重复曝光不能抢占、不合作采图超时不可释放“设备可再次曝光”许可、传输链路冲突不能导致 Worker 槽被等待占满；设备侧采样与曝光同步必须由硬件/控制卡负责。

### C03. AI 多模型/多 GPU 推理调度

**业务**：缺陷检测可能存在多模型推理，GPU0、GPU1 不同显存、耗时和模型装载状态。

**调度决策**：专业注册可用 GPU/模型能力、资源容量和执行位置；F19 基于工作就绪/优先级进行选择，MRK 对组和 GPU 许可限额；模型生命周期/加载缓存与显存来自专业 Owner 和 MDS/数据服务，而非调度器简单复制张量。

**反证**：`GPU0` 掉线/模型不存在时构建或准入明确失败；在一个 GPU 工作已进入不可中断 SDK 时取消不代表安全释放；跨 Host 需真实 fencing 协调；组容量 2 不等于显存一定够。

### C04. CT 重建与 MPR/3D Viewer 并存

**业务**：CT 重建任务处理大 Volume；同时 Studio Viewer 保持流畅，后台批量重建不得吃光内存/线程或阻塞重要采图。

**调度决策**：重建与交互预览不同优先级/组；大体积数据采用 MDS/专业 DataHandle 与预算，任务只携带句柄/引用，不复制整块 Volume 到 Worker 队列；专业 Executor 决定 GPU/CPU 实际并行度。

**验收**：多体积重建、动态取消、预览限频、DataHandle 生命周期、Frame 释放；统计 Worker PendingBytes 与真实内存预算各自符合含义。异常时不出现“任务结束但大体积句柄永不释放”。

### C05. PLC、运动控制与同步 SDK 专用线程

**业务**：工位上下料、运动定位、扫码、采图触发，部分厂商 SDK 只可在固定调用线程执行。

**调度决策**：使用已存在的 `FCRDedicatedExecutor`，为同步 SDK 设置专用线程、可限制队列；在进入硬件动作前通过专业 Host/Provider 校验当前授权和联锁；控制和调度在安全点协作，而非抢占 SDK 线程。

**验收**：所有 SDK 回调在指定线程执行；断开/取消/超时的报告与真实回执分离；被安全门阻止的动作从未发送；硬件急停在设备/PLC 层独立验证。

### C06. MES/IoT 通信、请求/应答与长时间等待

**业务**：流程提交结果到 MES，等待带 CorrelationId 的 ACK；或在 IoT 场景中长连接失效和自动重连。

**调度决策**：控制网络请求并发、待发送队列容量；消息等待由运行协同/保障管理相关性；正常的异步等待不占无意义的 OS 线程。

**反证**：断线、迟到 ACK、重复 ACK、观察超时、服务端已处理但客户端不知道；相同 OperationId 不可盲目二次提交；队列满需向上游返回有因果的失败，不能无记录丢 SN。

### C07. 生产高峰：在线检测与后台批处理竞争

**业务**：线上实时检测持续进入，后台离线图像重检、统计或数据转换大量占用 CPU/GPU。

**调度决策**：通过 Worker 优先级+公平预算、专业执行组、每资源预算保护在线流畅和后台最低吞吐；单 Key FIFO 保护同产品顺序。

**反证**：连续高优先级到达下，已准备好的低优先级后台任务始终得不到执行即不合格；在线任务被 10000 个大待执行任务淹没即不合格；过载恢复后要能继续准入而不是永久锁死队列。

### C08. Simulation / TestCenter 故障注入与无设备验收

**业务**：没有 X-Ray 机台时，仍能验证调度的完整业务语义，并在 Windows CI 重复执行。

**实现原则**：用相同 `MRKExecutionEngine/FCRWorker/MDS`，专业 Provider 注入延迟、失败、不可取消、外部 Unknown、资源竞争；TestCenter/ArchitectureSmoke 测真实状态与数据，而非建立测试专用 Scheduler。

**验收**：重放相同输入和故障种子，错误/结果可复现；取消风暴、容量拒绝、资源顺序、等待尾部均有测试；模拟通过只记 S3/S4，不自动记生产设备 S5。

### 17.1 五专业 / 两角色对照矩阵

| 案例 | 用户从 Studio 主要看什么 | 开发者主要使用什么 | 主要 Owner |
|---|---|---|---|
| X-Ray/Inspection | AI-GPU 组、SN 队列、Flow Trace、资源等待 | MRK Group/Step + MOC Build + Provider | MRK/MOC/MDS/Inspection |
| Vision/AI | GPU 能力、并发槽、模型可用性、优先级 | Group + Resource + 专业 AI 服务 | F19/MRK/Vision |
| CT3D | 重建队列、体积句柄预算、前后台并行 | F19 + MDS DataHandle + CT Provider | MDS/CT3D/MRK |
| Automation | 设备可用、专用线程、取消安全点 | Dedicated Executor + Resource Lease + Host | MRK/Automation/Host |
| IoT/MES | 请求队列、ACK 状态、迟到/失败原因 | F19/协同 Wait + 外部幂等/Journal | MRK/Host/Connectivity |
| Simulation | 模拟压力、故障注入、真实 Trace | 真实引擎 + Fault Provider + TestCenter | TestCenter/MRK/Professional |

## 18. 完整验收：测试矩阵、门槛、指标与反证

### 18.1 测试分层

- **T0 静态**：代码 Owner、项目依赖 DAG、工程序列化、Schema、构建拒绝、接口签名和资源名称规则。
- **T1 单元**：F19 Key/FIFO/公平/容量/取消、MRK 组/资源状态、异常路径、定时与租约的确定性测试。
- **T2 集成**：同一个 MRK Engine + MOC Plan + MDS Scope + 虚拟 Provider 的业务闭环；不模拟一个第二队列。
- **T3 Windows**：当前源码提交 SHA 的 Release/x64 全方案、FoundationSmoke/ArchitectureSmoke/TestCenter，以及真实 WPF STA。
- **T4 Studio/专业软件**：真鼠键/面板、组属性编辑、保存重开、CPU 仿真、Trace 来源、运行后 UI 状态；可有模拟 Provider。
- **T5 现场设备**：目标 GPU/相机/运动卡/PLC/安全授权、设备软件版本、权限、异常处理与长稳；需要设备签收证据。

### 18.2 核心自动测试表（AT01～AT45）

| ID | 场景/操作 | 必须断言 | 等级/Owner |
|---|---|---|---|
| AT01 | Worker `maxConcurrency=1/2/8` | 峰值不超额、全部结算 | T1 F19 |
| AT02 | QueueCapacity=0/1/8 | 满载明确拒绝，不启动拒绝项 | T1 F19 |
| AT03 | PendingByteBudget 预算刚好/超额 | 精确计数、超额不误淘汰旧项 | T1 F19 |
| AT04 | Key A 连续提交高低优先级 | 同 Key 严格 FIFO | T1 F19 |
| AT05 | 多 Key 不同优先级 | 高优先级先于其他已就绪项 | T1 F19 |
| AT06 | 连续高优先级+老低优先级 | Burst 后低优先级可调度 | T1 F19 |
| AT07 | 错误 `priorityFairnessBurst=0` | 输入拒绝，旧构造默认兼容 | T1 F19 |
| AT08 | DropOldestDroppable | 只 Drop 标记可丢项，完成态 Dropped | T1 F19 |
| AT09 | 先取消队列中的 Work | 无用户回调、配额/字节归还 | T1 F19 |
| AT10 | 已运行任务请求取消 | 不强杀，观察实际响应/尾部 | T1 F19 |
| AT11 | WaitAsync 观察超时 | 只结束观察、原 Completion 可继续完成 | T1 F19 |
| AT12 | Stop(cancelPending=false/true) | 排空/取消行为各自有明确定义 | T1 F19 |
| AT13 | 同时提交、取消、Stop 1000 次 | 终态唯一，无负计数或未完成句柄 | T1 F19 |
| AT14 | 定时 Gate/周期多次唤醒 | 不重复重入、停止后不回调 | T1 F19 |
| AT15 | Dedicated 同步 SDK | 同线程、队列有界、Stop 真实退出 | T1 F19 |
| AT16 | `ExecutionGroup` 未注册 | Flow 开始前抛明确错误、无 Provider 回调 | T2 MRK |
| AT17 | Group(1,1) 三个申请 | 1 Active/1 Waiting/第三拒绝 | T1 MRK |
| AT18 | Group 等待取消 64 次 | 等待归零，持有者未受影响 | T1 MRK |
| AT19 | Group 释放后重新申请 | 无配额泄漏/重复释放 | T1 MRK |
| AT20 | Step 带组+设备资源 | 先 Group 后 Resource 再 F19 | T2 MRK |
| AT21 | Resource A 互斥、B 并行 | A 持有者唯一，B 可并行 | T1 MRK |
| AT22 | A+B、B+A 交叉申请 | 无锁反转死锁，安全释放 | T1/T2 MRK |
| AT23 | 部分资源取得后取消 | 已得资源逆序归还，未获不释放 | T1/T2 MRK |
| AT24 | 资源持有者任务失败 | 原工作真实结算后配额归零 | T2 MRK |
| AT25 | 10 SN，共享 GPU Step 组 2 | 峰值≤2、10 Flow 隔离且正常完成 | T2 MRK |
| AT26 | 同一 Plan 输入不同 SN 两次调用 | FlowId/Invocation/MDS/输出不串 | T2 MRK/MDS |
| AT27 | Group 与 Worker 容量同时满 | 两级等待/拒绝原因不同且可见 | T2 MRK/F19 |
| AT28 | 高优先级 Step 与依赖约束 | 不越过前驱业务节点运行 | T2 MOC/MRK |
| AT29 | Loop Region 多轮执行带 Group | 每轮 Scope 身份、组释放正确 | T2 MRK/MDS |
| AT30 | 并行 Foreach+失败尾部 | 失败取消并等待在途，输出无污染 | T2 MRK |
| AT31 | WholeFlow 组限制 1【设】 | 两 Flow 互斥，内层不会二次取许可 | T2 MRK |
| AT32 | Stage A=1/B=2【设】 | Stage 持有/释放边界准确，Stage 间可交叠 | T2 MRK/MOC |
| AT33 | 嵌套同组/交叉 Stage【设】 | 合法共享一次，非法在编译前拒绝 | T0/T2 MOC/MRK |
| AT34 | 资源池容量 N、多单位申请【设】 | 按单位扣除、合法部分回滚 | T1/T2 MRK |
| AT35 | 动态资源撤销/不可用【设】 | 新申请拒绝，在途按安全策略处理 | T2 MRK/Host |
| AT36 | Deadline 已过/时钟跳变【设】 | 状态与原因稳定，不误执行过期等待项 | T1/T2 F19/MRK |
| AT37 | 工作超时，非合作 Provider | 不提前复用设备资源，状态 Unknown | T2 MRK/保障 |
| AT38 | 外部 MES 已处理，ACK 迟到 | 不因观察超时重复产生副作用 | T2 协同/保障/专业 |
| AT39 | Runtime 停止时多 Flow 并发 | 不接新任务，在途有序排空 | T2 MRK |
| AT40 | 数据预算耗尽 / 组余额正常 | MDS 拒绝独立可观察，无第二数据仓 | T2 MDS/MRK |
| AT41 | Studio 调度属性保存重开【设】 | 同 MEM Revision/BlockId/策略一致 | T3/T4 Studio/MOC |
| AT42 | Studio 选 AI→配置组→Build【设】 | 权威 MOC 检查、SourceMap 精确定位 | T3/T4 Studio/MOC |
| AT43 | Studio 仿真+取消等待 SN【设】 | 原 MRK Trace/组计数回落、旧 Flow 保持 | T3/T4 Studio/MRK |
| AT44 | 无授权生产请求 | Host/专业拒绝，真正设备从未动作 | T5 Host/Professional |
| AT45 | 真实设备断链/断电/主备切换 | Fencing/Unknown/恢复经核对，无双重控制 | T5 Host/Professional/保障 |

### 18.3 Windows CI 真实验收入口

| 验证入口 | 已存在的执行/回归关注点 | 本文对应 |
|---|---|---|
| `Build/Verify-Structure.ps1` | 工程、文件、.NET48 项目一致性 | T0 |
| `Build/Verify-OwnerDependencies.ps1` | Owner DAG、跨层依赖 | T0 |
| `CxVisual.Tests.FoundationSmoke.exe --concurrency` | F19 Worker、FairnessBurst、周期/专用 Executor、容量 | AT01～15 |
| `CxVisual.Tests.ArchitectureSmoke.exe` | 真实 MRK Flow/Group/Resource/控制/数据、负例 | AT16～40 |
| `CxVisual.Tests.ArchitectureSmoke.exe --organization-wpf` | 工程组织/WPF 相关回归（非全量调度 UI） | 基础 Studio |
| `CxVisual.TestCenter` 的现有 Windows 工作流 | NUnit/Owner、隔离 Worker、JSON/报告和回归 | 跨体系证据 |
| `CxVisual Studio S1 Windows Shell Acceptance`（若改 Studio） | 真实 WPF Shell/STA/交互，依实际 workflow 范围 | AT41～43 |
| `CxVisual Unified Runtime Windows TDD` | 全方案 x64、执行/Host/Journal 安全回归 | 运行软件门槛 |
| `CxVisual Engineering Organization Windows Acceptance` | 全方案 x64、组织、WPF/Benchmark、实例使用 | 工程保存编排门槛 |
| Config L1 + 配置底座 | 配置侧兼容与真实工程编译 | Schema/配置回归 |

**路径触发不能冒充未运行**：如果这次 Commit 只改某些 `DOCS/`，部分 workflow 可能不会触发；判断依据始终是该 SHA 实际触发/手工补跑的工作流和真实日志。只有 `completed/success` 才可标记该入口 S3。

### 18.4 既有 R08 真实软件证据（历史来源，不是本文新增通过）

当前《#1流程执行体系 V2.1》所登记的 R08 固定证据包括：

| 已验证增量 | 固定源码/证据 | 正确完成度 |
|---|---|---|
| F19 跨 Key `PriorityFairnessBurst` 与队列回归 | `b39a708109164cbe49afdf81b19d4fa5819ea9de` 的 Runtime/ TestCenter/ Config 等 Windows 记录（分别为 `39515316490`、`39515316520`、`39515316500`、`39515316510`） | R08 公平性软件能力局部【验】 |
| Group 等待配额与 64 轮取消归还 | `79470098f18d90a87d5eada9a01ea3018b4553e0`：Runtime `39515895209`、Engineering `39515895189`、Config L1 `39515895258`、配置底座 `39515895187` | R08 执行组等待取消局部【验】；该 SHA 未单独触发 TestCenter |
| Group/Resource/Step 执行现有路径 | `MRKExecutionGroups`、`MRKRuntimeCoordination`、`MRKExecutionEngine` 及 `ExecutionGroupTests` 源码 | 【实】有实际逻辑及软件测试路径，但不能由文件存在推出全部目标完成 |
| WholeFlow/Stage、复杂多资源公平仲裁、容量资源池、全面压力/长稳 | 尚无与本规格相同范围的全部固定证据 | 【设/待验】，不能把 R08 标记为全完成 |

证据锚点：
- F19 R08：<https://github.com/oudream/cx-visual-x3/actions/runs/39515316490>
- MRK Group R08：<https://github.com/oudream/cx-visual-x3/actions/runs/39515895209>
- 主实施档：`DOCS/Architecture/CxVisual_统一运行体系#1流程执行体系_需求归属_双角色使用与分轮实施_V2.1.md`。

### 18.5 性能、容量与长稳指标（应测，不提前虚构成绩）

| 指标 | 必须测量方法 | 验收表达 |
|---|---|---|
| 排队到开始的等待延迟 | `QueuedAt`→`StartedAt` 的分位数 P50/P95/P99，按优先级/组分层 | 给出机器配置、负载和实际统计，不造默认 SLA |
| 任务吞吐 | 稳态 SN/s、回调/s，说明业务耗时分布 | 基准和改动后比较，功能正确前不以吞吐合格 |
| 公平性 | 老低优先级工作等待轮次、实际选中顺序 | 合同条件内证明不会持续饿死，不能承诺不受任何限制 |
| 内存/字节 | PendingBytes、MDS 预算、进程工作集/GPU 使用 | 分开统计，长期无无界增长 |
| 并发峰值 | Group/Worker/资源池/真实 Provider 的峰值 | 每层按自身额度限制；设备实测需 S5 |
| 取消恢复 | 随机取消后 Active/Waiting/Pending/Lease 清理时间 | 限额无泄漏，正常新工作可再准入 |
| 长稳 | 1h 压测、8h 并发回归、24h 或产线完整工况 | 每次记录设备/驱动/软件版本、故障与资源趋势；未测不写 PASS |
| Studio 性能 | 节点/组数量增长下的 UI 延迟与绘制线程 | 真实 WPF 采样，不用 Mock 图替代 |

### 18.6 反证优先原则

- 不允许通过禁用坏例、放宽超时、扩大容量掩盖死锁、把失败测试跳过来使 CI 变绿。
- 对取消竞态，要固定随机种子 + 多轮重试压力，用时间窗口避免将机器偶然调度顺序当成系统保证。
- 对实际资源总量要给出计量单位，不把“组配额 2”用作“GPU2 张卡”或“显存可用”的替代证明。
- 对不合作 Provider 要明确隔离/等待或保守 Unknown，不能假装 `CancellationToken` 能强行制动硬件。
- 现场测试必须有操作许可和安全测试环境，禁止为了验证调度越过 PLC/设备联锁。

---

# 第五部分｜分轮实施、状态维护、契约追踪与交付准则

## 19. R08 开发实施计划、工作包与持续联动

### 19.1 实施原则与顺序

**优先完成尚未闭合的 R08，不推翻 R01～R07 已稳定的流程、MDS、MOC 与 MRK 内核。** 每个子工作包完成真实源码、正反例、Windows 固定 SHA 证据后才进入下一项；出现红灯在原包修复，不跨包堆出更多未验收变更。

```text
R08-0（短期契约复核）
   ↓
R08-A：WholeFlow / Stage / Step 作用域和重入安全
   ↓
R08-B：资源池容量、批量资源、取消和死锁反证
   ↓
R08-C：多级背压/公平/执行位置/诊断统一验收
   ↓
R08-D：双角色体验（API、MOC/Studio、真实模拟及 10 SN 案例）
   ↓
R08 收口：固定 SHA Windows 与 TestCenter + 未验收现场清单
   ↓
R09 控制/管理/调试 → R10/R11 保障与协同 → R12 六体系联合现场分级验收
```

**工作包不是按“30 分钟硬凑提交”**：保持主题独立、可运行、可审阅，小范围但完整的纵向闭环。文档大规模补全不证明 R08 实现了新能力。

### 19.2 工作包详细内容与修改边界

| 包 | 先证明的行为 | 首选原项目/文件 | 必须的正反例与门禁 |
|---|---|---|---|
| **R08-0** 事实和身份核对 | 组/资源/Flow/Work/Stage 语义表、现有 API 与测试覆盖差距真实一致 | `MRKExecutionPlan/Engine/Groups`、F19、MOC、既有测试 | 形成源码差距清单，无代码大搬迁；核对 Owner，不新增平行数据 |
| **R08-A1** Scope 描述及编译拒绝 | WholeFlow/Stage/Step 绑定到可识别 Plan/Region；不接受交叉/悬空 Scope | `Mechanism.Core` 既有 Logic Aspect、`Mechanism.Orchestration` MOC、`MRKExecutionPlan` | 保存/重开、合法嵌套/非法交叉、无组默认兼容 |
| **R08-A2** Scope 运行持有与释放 | 引擎支持整个 Flow/Stage 有效占用；相同组嵌套不二次占座 | `MRKExecutionEngine`、`MRKExecutionGroups`、`MRKExecutionIdentity` | 2 Flow/Group1；多 Stage 重叠；取消/子流程/多轮 Loop 无死锁/泄漏 |
| **R08-B1** 资源池容量 | 当前互斥资源之外，支持 count units/N，同步正确回收 | 原 `MRKResourceLeaseCoordinator`、现有资源声明/Descriptor | Counted 资源和独占旧兼容；非法单位拒绝；GPU2 场景 |
| **R08-B2** 多资源安全 | 批量/有序申请、部分回滚、取消/Deadline 语义，不占 Worker | 同原协调器、`MRKExecutionEngine` 与测试 | A+B/B+A、随机中断、资源 Owner 精确、无 ABBA 死锁 |
| **R08-C1** 完整背压 | Group/Resource/Worker/Flow/MDS 分层限制、不丢重要工作 | `FCRWorkerOptions`、`MRKExecutionGroups`、MDS 现有接口 | Waiting 与 Pending 区分；超预算拒绝；Stop 不漏计 |
| **R08-C2** 公平与性能 | 已有 Burst 不回归，验证多组/多资源条件下合理公平与吞吐 | `FCRWorker`、既有 Worker 测试、MRK 集成测试 | 持续高优先级、1000 随机任务、端到端性能记录 |
| **R08-D1** 开发者体验 | 直接 Worker 与构建 Flow 的常用路径都简单、示例编译通过 | 原 F19/MRK SDK/`Examples` | 不重复创建 Builder/Runtime，参数默认符合预期 |
| **R08-D2** 工程编排体验 | 组/资源 Schema、Build 验证、仿真/Observe 原 Trace 与 MOC SourceMap | `Mechanism.Core`、MOC、`Studio`、WPF STA | 保存/重开、工程身份、错误回滚、未授权拒绝 |
| **R08-FINAL** 验收收口 | 所有本轮需求→源码→测试→固定 SHA CI，明确现场缺口 | 既有 Testing/Implementation 文档；不复制历史真源 | 只认证已真实运行的等级；R09 前留出明确约束 |

### 19.3 每轮的“七件实物”

1. **规格编号/业务结果**：指向 TSxx-yy、DEV/ENG、S/C、AT 和失败边界；
2. **原 Owner 修改**：所有源码改变列文件与方法、原因、是否公开 API/序列化兼容；
3. **真实开发者/Studio 入口**：至少 1 条完整纵向路径，不能只有内部类或样式；
4. **正反例**：至少一个业务成功、一个取消/非法、一个并发/资源场景；
5. **Windows x64 实测**：Commit SHA、测试名、工作流链接、日志和 ExitCode，不能沿用旧成功；
6. **回归**：Flow/Loop/子 Content/MDS/权限/Studio 原有用例不退化；
7. **结果与残留**：明确 S1/S2/S3/S4/S5 级别，留下未测试的设备与性能条件。

### 19.4 改造冲突与避免返工

| 常见返工点 | 为什么产生 | 约束和缓解 |
|---|---|---|
| 把 Scope 新建成运行引擎 | MRK 原 Engine 已持有真实 Work/Flow 状态 | 扩展已存在内核，复用唯一 F19 Worker/数据 Frame |
| 每种组自己维护 Active/Waiting | 重复状态仓、取消时数字不一致 | 组计数的唯一 Owner 还是 `MRKExecutionGroups` |
| Studio 私自存一份调度 JSON | 二次工程真源、Revision 与实际编译分离 | 只经 MEM/MPM/MOL 权威事务；Studio 布局单独保存 |
| 把组当设备物理资源 | 模型容量错配，导致并发/显存超配 | Group 是逻辑额度，Pool/Host/专业安全另有契约 |
| 在 Worker 内阻塞等待子流程 | 小并发时自等待死锁 | Scope/Region 入口异步拿许可，不占 Worker 自等 |
| 未经规范动态申请第二资源 | A 等 B/B 等 A，跨组循环等待 | 全局资源排序或原子批量 + 构建拒绝不合法依赖 |
| 为测试复制简化 Queue | 测试绿灯不代表生产行为 | TestCenter 使用原 F19/MRK 生产类型和真实日志 |
| 修改 MOC Schema 导致旧工程破损 | 旧 JSON/XML V1/V2 语义不同 | 显式版本兼容、迁移确认、保存重开反证 |
| 单看 Worker Throughput 优化 | 忽视设备尾部/Unknown/数据内存 | 在 X-Ray/CT 综合场景里联合验收 |

## 20. 规格—源码—测试—证据矩阵与最终交付标准

### 20.1 需求映射总表（大组索引）

| 规格范围 | 核心目标 | 主 Owner / 真实类型 | 证据和下一步 |
|---|---|---|---|
| TS05-01～08 | 身份与生命周期 | `MRKExecutionIdentity/Engine/RuntimeSession`、MDS | 现有 Flow/Invocation【实】，补跨 Scope 例 |
| TS06-01～09 | 受理、拒绝、取消 | `FCRWorker/FCRWorkHandle`、`MRKExecutionEngine` | Worker 已有实现；业务级幂等【设】 |
| TS07-01～09 | FIFO、优先级、公平性 | `FCRWorkerOptions/FCRWorker` | R08 有固定 SHA 软件验收；跨资源公平尚需证据 |
| TS08-01～10 | Step/Stage/WholeFlow 组作用域 | `MRKExecutionGroups/Plan/Engine` + MOC | Step/等待取消【实/验】；Stage/WholeFlow【设】 |
| TS09-01～09 | 资源互斥、池容量、组合申请 | `MRKResourceLeaseCoordinator/Lease` | 独占【实】，N 容量/多资源公平【设】 |
| TS10-01～08 | 队列与多级背压 | `FCRWorkerOptions`、MRK Groups、MDS | F19/Groups【实】，跨层联合验收待做 |
| TS11-01～08 | 时间、异步、执行位置 | `FCRAsyncGate/PeriodicJob/DedicatedExecutor`、MRK 等待 | 基础【实】；统一截止期/持久等待【设/协】 |
| TS12-01～09 | 异常与安全清理 | `MRKExecutionPolicy/Engine/Journal` + Provider | 软件收尾【实】，设备/跨进程【协/待验】 |
| DEV01～14 | 软件开发者期望 | F19/MRK SDK + `Examples` | 15 章真实 API 样例；编译/行为以新 SHA 验收 |
| ENG01～14 | 工程编排者期望 | Studio/MEM/MOC/MRK/诊断 | 完整用户路径为【设】，按 R08-D 分轮完成 |
| S01～S14 | 场景发挥作用 | 各 Owner 联合；S01 最小纵向闭环 | 示例/反证见 2、17、18 章 |
| C01～C08 | 专业应用案例 | Inspection/Vision/CT3D/Automation/IoT/Simulation | 逐个按 T2～T5 分级签收 |
| AT01～45 | 独立与联合验收 | FoundationSmoke/ArchitectureSmoke/TestCenter/Studio | 禁止仅按文件名判已运行或按旧 SHA 判全绿 |

### 20.2 R08 当前状态与不可误写结论

- **【实/验】已存在**：F19 Worker 的 Key、队列容量与估计字节、优先级和跨 Key 有界公平；MRK 的 Step 组配额/等待上限与取消归还；引擎 Flow、资源独占、MDS 生命周期、部分诊断事实及已有 Windows 回归记录。
- **【部】边界明确**：现有 `MRKResourceLeaseCoordinator` 仅进程内独占 1；执行组实际挂在 Step；等待/组/资源与真实硬件状态不是同一实体。
- **【设】本轮要实现**：WholeFlow/Stage 组生命周期、嵌套共享/重入安全、计数资源池、多资源组合/优先级/容量协调，完整调度 Schema、Studio 同源体验。
- **【待核】**：所有目标用例的同一固定提交 Windows 全量门禁、10 SN 专业综合压力数据、1h/8h/24h 长稳、真实 GPU/PLC/X-Ray/运动安全、跨进程资源 Owner。

### 20.3 一条需求的可追踪记录格式

建议第 19 章每一轮保留一行或一段：

| 字段 | 示例（只作模板，非通过记录） |
|---|---|
| RequirementId | `TS08-03` |
| SourceScenario | `S01、S10` |
| UserExpectation | `DEV02、ENG04` |
| Owner + RealType | `Mechanism.Runtime / MRKExecutionGroups` |
| Existing/Change | `Step有代码；Stage/WholeFlow待改` |
| API/Schema | 编译后 GroupScope Descriptor（待定） |
| TestId | `AT31/AT32/AT33` |
| Evidence SHA | `待此次实际提交` |
| Windows CI | `未运行/红灯/通过` 三值严格区分 |
| S-Level | `S0/S1/S2/S3/S4/S5` |
| Limitation/Next | `Studio/现场未验收` |

**历史通过的固定 SHA 仅证明当时变更范围**。新的聚合型验收应重新运行受影响工作流并逐项归档，不通过改旧文档文本来让当前源码显得已经通过。

### 20.4 交付签收清单

- [ ] **完整职责**：TS05～TS12 全部需求有 Owner/源码/行为、未实现有明确计划。
- [ ] **软件开发者**：普通任务、具名 Flow、组/资源、取消、超时、观察与失败示例在独立 .NET48 工程真实编译运行。
- [ ] **工程编排者**：Studio 不另建真源，可图形配置/保存/构建/仿真/观察调度；真实 WPF STA 与操作证据存在。
- [ ] **Scope 完整**：Step/WholeFlow/Stage 的入口、退出、嵌套、重入、并行 Foreach、取消与异常全部通过反证。
- [ ] **资源正确**：同资源/多资源、GPU 容量、互斥、部分回滚、取消与死锁、异常释放有实测。
- [ ] **容量和公平**：Worker/Group/Resource/MDS 层有明确定义和压力结果，过载不丢关键生产结果。
- [ ] **来源一致**：MOC SourceMap、MRK Trace、Studio 画布/父子 Content 与 SN/Invocation 的定位一致。
- [ ] **Windows 证据**：固定 SHA 的相关工作流编译/测试 ExitCode 全绿；路径未触发的单独标记并补跑。
- [ ] **专业模拟**：C01 10 SN / AI2、C04 CT 大体积、C05 专用 SDK 等明确选择的纵向场景可复现。
- [ ] **实机限制**：未通过的 PLC/X-Ray/运动联锁、GPU 真实容量、跨进程故障恢复、长稳必须在交付说明中醒目列出，不冒称生产可用。

### 20.5 待评审裁决（不阻碍已有 Step R08 继续验收）

| 编号 | 必须裁决的问题 | 默认建议及依据 |
|---|---|---|
| D01 | WholeFlow 是包含流程外的等待/人工审批，还是仅执行阶段？ | 显式 Scope 生命周期；不能由名字推断是否持续占位 |
| D02 | Stage 如何标识范围？ | 优先绑定现有 MOC ControlRegion/稳定 BlockId；禁止第二 Graph |
| D03 | 同组父/子嵌套重入的许可语义？ | 默认共享父 Scope 持有，非法跨 Scope 先拒绝；避免 max=1 自死锁 |
| D04 | GPU “并发 2”使用 Group 还是 ResourcePool？ | 先用 Step Group 做软件闭环；真实 GPU 多单位与显存容量用专业+资源池清晰表达 |
| D05 | 多资源是原子批量还是稳定排序顺序获取？ | 简单先规范全局顺序与异常回滚；复杂资源/动态集合才增加批量入口 |
| D06 | 延迟/Deadline 使用哪一层抽象时钟？ | F19 公用时钟优先；只需可注入、可重复，不引入分布式调度基础设施 |
| D07 | Pause 后是否释放组席位？ | 按专业安全点/策略明确配置，不默认释放设备或原在途任务 |
| D08 | 提交去重与同 Key FIFO 的边界？ | 业务幂等属于管理/专业；F19 Key 只负责执行顺序 |
| D09 | 队列与 GPU 数据内存预算如何衔接？ | MDS/专业保留内存数据 Owner，调度保留任务/组/许可与引用，不造二次 Heap |
| D10 | Studio 哪些策略常用/高级？ | 常用：组、资源、优先级；高级：Scope、Deadline、Budget、重试安全，全部 Schema 驱动 |

### 20.6 最终架构约束与下一步

**先完成 R08 软件调度的完整闭环，再推进六个子体系联合验收。** 可先用现有 F19/MRK 做真实 S01 10 SN/AI2，从可重复的 Step Group 开始，逐包增强 Scope/容量/多资源而不推翻既有引擎。每一步都让开发者 API、工程编排行为、原工程身份和真实 Windows CI 一起前进。

> **本文的“完整版”指需求、场景、理论、职责、类、API、两个角色的完整使用例子、实施与验收规则已收敛为一份主规格；并不表示 R08 所有条目已编码或现场可运行。后续开发以该文档为目标，以主仓真实源码与每轮固定 SHA 测试为事实依据。**
