# CxVisual Supervision——整体架构、全类对象与 UML 完整设计 V2.0

> **版本**：V2.0（2026-10-30）｜**性质**：【目标设计＋现有源码对照＋可执行验收合同】，**非功能实装证明**。  
> **目标**：对现行 Supervision 的 S01～S12、五级监督、SX-A～D 四大闭环反推系统部署、职责边界、全类对象、主要交互、GoF/OOP/AOP、API、安全、验收。  
> **原则**：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。  
> **注意**：Go 使用 struct/interface/函数而非 C# 的 class；本文件的“全类”泛指**实体、接口、策略、DTO、组件及关键函数**。候选类只是设计上的最小职责分解，**不等于全部必须创建物理源文件**。  
> **状态标签**：S＝已核实当前 master 有对应源码（非 CI 通过）；R＝其他 CxVisual Owner 的已核实真实类型（复用，不迁入 Go）；A＝建议新增或深化的目标职责（尚无同名正式源码，命名仅候选，实施前二次查重）；G1/G2/G3 分别代表软件验证、真实集成/HIL、长期工业运行。  
> **本轮**：只修改架构文档和入口导航，不创建类、接口、Go 包、数据库表、API 或空目录；实际开发按固定提交 SHA 与独立验收进行。

## 0. 权威依据与文档分工

1. [12 大功能域与实施验收矩阵 V1.0](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md)：**唯一完整目标与状态验收索引**，本文件承接 S01～S12、SX-A～D、SR-01～08，不反向制造第二张完成度表。
2. [整体架构与目录设计 V1.0](CxVisual_Supervision_整体架构与目录设计_V1.0.md)：现有两 Go 进程、HTTP 路由、兼容边界。
3. [Go 规范 V1.0](CxVisual_Supervision_Go编码与工程规范_V1.0.md)、[Web 规范 V1.0](CxVisual_Supervision_Web前端架构与编码规范_V1.0.md)：工程实施、跨平台和质量门禁。
4. [device_communicator 资产档案](../../../DOCS/Codebases/02_device_communicator.md)、[Service 目标 V1.1](../../../DOCS/Architecture/CxVisual_统一功能模块体系%235Service——服务模块完整设计与验收_V1.1.md)、[Automation 监控/边缘 V1.1](../../../DOCS/Architecture/CxVisual_专业自动化体系%2306工业监控与边缘系统_V1.1.md)、[统一交互 V1.5](../../../DOCS/Architecture/CxVisual_公共机制体系%23统一交互——四大能力与双层总线完整设计及验收_V1.5.md)、[命令 Owner 裁决](../../../DOCS/Decisions/CxVisual_统一命令中心双项目归属裁决_V1.0.md)；与 AGENTS、开发 Skill、DOCS/00～04 共同裁决跨体系 Owner。
5. 本轮查到现有 Go Program/MonFrame/MonC/MonA/MonS、Daemon RunProgram、supervision Program/Overview、events Record、alerts Incident、httpapi 的三个 Provider；C# MLCServiceModuleBase/IMLCServiceModule/Configuration、MFM、MOC、MRK；Web 类型、Store、四页。**尚未穷尽 55 份 Go 文件、DOCS/02～04 二进制 Sheet、跨机实测与所有 CI；清单中的 S/R 是源码级事实，不提升为 G1/G2/G3。**

## 1. 功能目标：一个核心，五级监督，十二大能力

**核心产品**：从“进程还活着”发展为“机器、服务、任务、专业、业务全部可查；故障有证据；恢复有授权与真实确认”的统一监督运维平台。客户界面可以是 H5、Studio 或 TestCenter，**唯一运行事实继续由各自 Owner 产生**。

| 层级 | 范围 | 唯一 Owner | Supervision 实际职责 |
|---|---|---|---|
| L0 Process | PID、CPU/RSS、Heartbeat、退出、守护 | Go Monitor/Daemon/Program | 本机进程观察与进程操作治理 |
| L1 Service | Service/Instance、依赖、Liveness、Readiness | Mechanism.Core Service + Host/专业服务 | 版本化只读观察，协助诊断 |
| L2 Runtime | Run/Flow/Task、Queue、Trace、DataRef、结果 | MRK/MDS/Decision | 关联投影，不调度任务 |
| L3 Professional | PLC/X-Ray/运动/Vision/CT3D/AI/Inspection | 专业 Provider、F17/MCN/MPA | 读取专业状态与质量，不执行硬件控制 |
| L4 Business | SN、工位、批次、业务成功、MES 确认 | Inspection/MES/相关专业 | 看业务结果/影响，不改业务判定 |

十二大方面保持原编号：S01 总览、S02 进程、S03 服务、S04 运行、S05 资源、S06 设备通信、S07 专业、S08 事件告警、S09 日志诊断、S10 部署版本、S11 安全审计、S12 跨机。它们是**功能域与 UI 导航**，不强制一域一个 Manager、一个项目或一个数据库。

## 2. 部署与模块协作总图（物理进程必须与逻辑类分开）

~~~mermaid
flowchart TB
 subgraph CLIENT["客户端（无执行 Owner）"]
   WEB["Vue 3 + TypeScript H5"]
   STUDIO["CxVisual Studio / TestCenter"]
 end
 subgraph GO["Services/Supervision · 独立 Go 模块"]
   MON["Monitor / Gin\n旧状态唯一真源 + 查询投影 + 安全入口"]
   DAEMON["Daemon\n程序启动 / Wait / 故障退避 / 退出事件"]
   MON <-->|"旧兼容 UDP/Status/Event；未来可审计操作回执"| DAEMON
 end
 subgraph CX["CxVisual 主运行与专业体系"]
   HOST["Runtime.Host + MRK / MDS / Decision"]
   SVC["Mechanism.Core.Service + 专业 Service"]
   PROF["Professional + F17 / MCN / MPA"]
   BIZ["Inspection / MES 业务真源"]
   HOST --- SVC
   HOST --- PROF
   PROF --- BIZ
 end
 WEB -->|"已有只读 HTTP；未来安全操作"| MON
 STUDIO -->|"同一版本化只读 API"| MON
 HOST -.->|"待实现：权威只读 Observation Adapter"| MON
 SVC -.->|"待实现：Readiness / Dependency"| MON
 PROF -.->|"待实现：设备质量/专业状态"| MON
 BIZ -.->|"待实现：业务结果投影"| MON
~~~

**实线**表示已存在的技术边界/逻辑联系，但不是全链路实现证明；**虚线**是待开发的显式观测接口。Go Monitor 和 Daemon **仍为两个进程**；不为 H5 再起第三个 Backend。旧资产档案中的“编排、Runtime、AI 三主进程”是另一层历史候选部署讨论，不覆盖当前 Go 迁移裁决。

## 3. 工程包与六层职责（不增 .NET 项目，不空造 Go 目录）

~~~text
Services/Supervision/
  cmd/monitor               ← 进程组合根；原 Gin + UDP Monitor 业务状态
  cmd/daemon                ← 进程组合根；进程真正 Start/Wait/Stop
  internal/program          ← 原 SQLite/Program/Status/Event 真源（保留）
  internal/monitorc         ← 原协议兼容与收发（保留）
  internal/daemonc          ← 原守护执行者（保留）
  internal/supervision      ← 已有不可变快照；未来只读观测关联
  internal/events           ← 已有事件历史有界读取
  internal/alerts           ← 已有故障历史；未来监督告警生命周期
  internal/httpapi          ← Gin 版本化 API；读/写分门禁
  internal/security         ← 现有 LocalOnly；未来身份/RBAC/审计
  internal/health           ← 候选：无副作用探针与分层健康投影
  internal/metrics          ← 候选：真实性能采样与有限历史
  internal/diagnostics      ← 候选：Trace/时间线/证据导出
  internal/operations       ← 候选：仅 L0 进程受控操作协调
  internal/adapters         ← 候选：连接 CxVisual 权威只读提供者
  internal/deployment       ← 候选：发布观察与真实部署 Owner 协同
  internal/fleet            ← 候选：有实际多机场景后启用
  common/                   ← 原本地依赖，渐进兼容，不平行再造 F17
  web/src/{api,types,stores,views,components,layouts,router,...}
Applications/Runtime.Host/（现有 C# 工程）
  Supervision/（候选源码目录，仅在真实接入开发时创建）
Mechanism/Core/Modules/Service/（现有 C# Service Owner，原位深化）
~~~

约束：候选包**只有在至少一条可编译运行的能力闭环开发时才允许创建**。Go API 处理器不拥有 Monitor 状态，跨域 Adapter 不拥有 MDS/Service/Professional 的业务状态；业务历史数据不写入 Go 的第二套真源。Supervision 自己产生的**审计、告警变迁和监控采样历史**可以拥有专有持久化，但必须与旧 Program 表及专业业务历史严格区分。

## 4. 五条不可打破的对象关系

1. **Program 定义 ≠ ProgramStatus ≠ supervision.Program**：分别是旧 SQLite 配置、Monitor 运行态、只读 API DTO。禁止为新增页面再造 ProgramStateDB。
2. **ServiceModule ≠ OS Process**：现有 MLCServiceModuleBase 管服务生命周期；Daemon 管 OS 进程；ServiceReady 不能从 PID 或 Running 直接推断。
3. **Run/Task ≠ Go 守护作业**：MRK 仍是 Run/Task 和专业命令执行 Owner；Supervision 不增加通用 FlowEngine、JobScheduler、CommandCenter。
4. **历史 Incident ≠ ActiveAlert**：现有 Incident 只从事件尾部提取历史故障线索；正式告警须有规则、确认、恢复、抑制、通知与不可变审计。
5. **请求已受理 ≠ 执行成功 ≠ 目标状态核实**：HTTP/UDP ACK 不是业务确认；Timeout 可能为 Unknown，不能盲重试危险设备命令。

## 5. 全类对象地图与源码归属

**对象地图条目数：142 项（S/R 源码存在或复用 65 项，A 候选职责 77 项）。** 此数字包含 Go 函数和 TS 组件，**不是 142 个应新建的 class 或源文件**。候选应以用途、Owner、测试和源码查重收敛，允许合并或裁撤。

表列的“现有”包含 Go 函数与 TS 组件，其语言形态须保留；R 为跨 Owner 正式复用；A 仅为设计候选。候选的初始名称用于讨论职责与避免重复，**在已有 C# FQN、Go 类型和 DOCS/02～04 逐项查重以前不视为公开 API 定案**。以下清单是产品级逻辑全类，不是本期要批量自动创建的文件清单。

### E1 Go 守护与监控已实现对象（原资产及开发增量）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| S | Go internal/program | Program | 受管程序配置；SQLite 表、路径、启动策略 | 保持原字段/ID/DB |
| S | Go internal/program | Heartbeat | 程序上报的心跳帧结构 | 保留旧线协议 |
| S | Go internal/program | Status | Daemon 的 PID/运行/错误计数 | 守护状态真源 |
| S | Go internal/program | Event | 守护事件与附属状态 | 不等于活动告警 |
| S | Go internal/monitorc | MessageA | 旧 Monitor 消息载体 | 帧兼容 |
| S | Go internal/monitorc | DealMonFrame | 旧消息处理接口 | 已有接口 |
| S | Go internal/monitorc | MonFrame | 旧 UDP 帧编解码 | CRC 不是身份验证 |
| S | Go internal/monitorc | MonC | 发送心跳/事件及通道检查 | 旧 Channel 行为 |
| S | Go cmd/monitor | MonA | Monitor 帧消息处理 | 沿用原状态 Owner |
| S | Go cmd/monitor | MonS | Monitor UDP 收发与转发 | 不作为远程授权通道 |
| S | Go cmd/monitor | ProgramStatus | 守护状态、心跳与进程资源组合 | 唯一运行状态来源 |
| S | Go cmd/monitor | HeartbeatStatus | 心跳观察状态 | 不推导 Ready |
| S | Go cmd/monitor | ProcessStatus | 进程资源采样 | 单位需显式 |
| S | Go internal/daemonc | RunProgram [函数] | 循环启动/Wait/失败与重启 | 渐进治理退避、停止 |
| S | Go internal/daemonc | RunProgramContextWithStop [函数] | 原 Worker 的 exec.Cmd.Wait 承接停止结果 | 原入口和旧事件合同保持兼容 |
| S | Go internal/daemonc | StopController | 单一 Worker 代次绑定原始 os.Process 句柄发出停止请求 | 不依靠裸 PID 重新发现目标 |
| S | Go internal/daemonc | StopReceipt | 拒绝、请求失败、未知、Wait 已确认结果分级 | 本地诊断；不等于业务 Ready |
| S | Go cmd/daemon | ownedStopOutcome / queueOwnedStop | 主循环异步收集原 Worker 停止回执 | 无新增 HTTP/UDP 控制协议 |
| S | Go cmd/daemon | terminalLatchFile / readTerminalLatches / writeTerminalLatches | 原 Daemon 内守护上限闭锁本地持久化 | 不修改 Program SQLite 表 |
| S | Go cmd/daemon | orphanObservationFile / writeOrphanObservation | 启动隔离结果带时间戳原子投影，供 Monitor 本机只读展示 | 不是活跃隔离真源或安全许可 |
| S | Go cmd/daemon | verifyManagedExecutablePID [函数] | 无活跃 Worker 的旧 PID 文件最小身份校验 | 不消除 PID 复用竞态 |
| S | Go cmd/daemon | programWorkerGate / launchProgramWorker | 守护协程同步占用及退出释放，防止同 ID 重复启动 | 不复制 Program 状态、PID 或 Runtime 调度 |
| S | Go internal/program | GetProgramRunList [函数] | 读取待运行的受管程序配置 | 保持原 DB |
| S | Go cmd/daemon | recoveryPrograms [函数] | 旧程序恢复装配入口 | 保留旧守护兼容 |

### E2 已有 Go 只读投影/API/事件（均已存在）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| S | Go internal/supervision | Program | 不可变进程快照 DTO | ID 输出为十进制字符串 |
| S | Go internal/supervision | Overview | 进程统计结果 DTO | CPU 求和非整机 CPU |
| S | Go internal/supervision | Snapshot/Find/Summarize [函数] | 快照复制、按 ID 查找、汇总 | 无第二运行态 map |
| S | Go internal/events | Record | 原 NDJSON 事件记录 | 历史源事实 |
| S | Go internal/events | ReadRecent/ReadRecentWhere [函数] | 有界尾读和条件筛选 | 现有限额 |
| S | Go internal/alerts | Incident | 由历史事件提取的故障信号 | 不是活动告警 |
| S | Go internal/alerts | Classify/ReadHistory [函数] | 按旧事件分类并读取故障历史 | 启动失败筛选需复核 |
| S | Go internal/httpapi | SnapshotProvider | 现有进程快照注入函数类型 | Read-only |
| S | Go internal/httpapi | EventProvider | 现有事件读取注入函数类型 | 本地访问限制 |
| S | Go internal/httpapi | AlertHistoryProvider | 现有故障历史读取函数类型 | 本地访问限制 |
| S | Go internal/httpapi | Register [函数] | GET health/overview/programs 路由 | 不注册控制接口 |
| S | Go internal/httpapi | RegisterAlertHistory [函数] | 注册异常历史查询路由 | 本机只读 |
| S | Go internal/guardview | Item / Source / Report / Read | 有界解码、独立来源状态、精确 Program ID 投影 | 不读取实时 Daemon 内存或执行控制 |
| S | Go internal/httpapi | GuardProvider / RegisterGuardDiagnostics | 只读 GET /api/v1/guard | security.LocalOnly；无写路由 |
| S | Go internal/security | LocalOnly [函数] | 本地访问限制中间件 | 不是身份/RBAC |

### E3 现有跨体系 C# Owner（复用，不迁入 Go）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| R | Mechanism.Core | IMLCServiceModule | Service 标准模块接口 | 不重建服务生命周期 |
| R | Mechanism.Core | MLCServiceModuleBase | Start/Stop/Faulted 基类 | 当前 IsReady 仅由 Running 推断，需深化 |
| R | Mechanism.Core | MLCServiceModuleState | 现有五态枚举 | 不要新建等价公共枚举 |
| R | Mechanism.Core | MLCServiceModuleConfiguration | Provider/超时/并发/依赖配置 | FCM 真源 |
| R | Mechanism.Core | MFMModuleDescriptor | 已有模块身份/能力描述 | 不建服务全局注册表 |
| R | Mechanism.Core | MFMModuleKind | 五类模块枚举 | Service 为五类之一 |
| R | Mechanism.Decision | MOCCapabilityCatalog | 能力发现/查找与描述 | 不宣称命中就是 Ready |
| R | Mechanism.Runtime | MRKCommandCenter | 命令受理执行/查询、取消 | 专业命令唯一 Owner |
| R | Mechanism.Runtime | MRKCommandPipeClient | 已有命令 Named Pipe 客户端 | 非 Supervision 观察 API |
| R | Mechanism.Runtime | MRKCommandPipeServer | 已有命令 Named Pipe 服务端 | 不绕过目标授权 |
| R | Tests/TestCenter | TCTLocalEchoService | 本地 Echo Service 实际请求处理 | 仅测试样本不是跨进程监督 |
| R | Tests/TestCenter | TCTServiceRequestPanel | Start/Request/Stop 和回执 UI | 可复用交互思想 |

### E4 现有 Web 类型/视图（均已有源码）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| S | web/src/types | Program [TS interface] | 进程快照类型 | 与 Go JSON 对齐 |
| S | web/src/types | Overview [TS interface] | 进程汇总类型 | 只用于现有快照 |
| S | web/src/types | GuardIssue / GuardSource / GuardReport [TS interface] | 守护文件观测、缺失与来源时间契约 | 不把未知当作健康 |
| S | web/src/api | fetchOverview/fetchPrograms/fetchProgram [函数] | 当前只读获取进程数据 | 统一 client.ts |
| S | web/src/api | fetchEvents/fetchAlertHistory [函数] | 事件与历史故障查询 | 原访问限制 |
| S | web/src/stores | useMonitorStore [Pinia] | 程序与总览快照/过期错误态 | 不持有服务真源 |
| S | web/src/stores | useEventsStore [Pinia] | 事件查询页面状态 | 只读 |
| S | web/src/stores | useAlertHistoryStore [Pinia] | 异常历史页面状态 | 不等于活动告警 |
| S | web/src/stores | useThemeStore [Pinia] | 浅色/深色 Token | 保留双主题 |
| S | web/src/views | DashboardView/ProgramsView [Vue] | 运行总览与程序详情 | 已有真实接口 |
| S | web/src/views | GuardView [Vue] | 双来源守护诊断、筛选、时间及原因 | 只读，S11 安全操作未开放 |
| S | web/src/stores,utils | useGuardStore / guardVerdict | 缓存旧证据标记过期、推导保护状态 | 仅双来源可用才显示无记录 |
| S | web/src/views | EventsView/AlertsView [Vue] | 事件记录与异常历史 | 已有真实接口 |
| S | web/src/components | CpuChart [Vue] | CPU 数据图组件 | 不得伪造趋势数据 |

### C1 Go 观察与跨层关联（建议，在 internal/supervision 扩展）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| A | Go internal/supervision | ScopeFilter | 按既有工程/机器/租户/对象 Scope 限定只读查询 | 跨域查询参数，不创建第二工程身份；C |
| A | Go internal/supervision | ObservationRef | 对象的 Source/Owner/Scope/Kind/Id 引用 | 值对象；不创建平台第二身份；C |
| A | Go internal/supervision | ObservationMeta | 采集/接收时间、质量、版本和 Generation | 所有跨域快照附带元数据；C |
| A | Go internal/supervision | SourceDescriptor | Provider 名称、SchemaVersion、受支持范围与能力 | 声明不代表实际 Ready；C |
| A | Go internal/supervision | ObservationBatch | 同一来源同一采样版本的只读观测集 | 批次身份/容量/过期控制；C |
| A | Go internal/supervision | ObservationSource [接口] | Describe/Snapshot，以已授权来源交付事实 | Adapter/Provider 模式；C |
| A | Go internal/supervision | SourceSession | 来源连通性、最后成功与时钟偏移 | 仅观察会话，不保存业务数据；C |
| A | Go internal/supervision | SnapshotCoordinator | 并发调用来源、超时隔离、快照聚合 | 不能阻塞 Monitor 进程采样；C |
| A | Go internal/supervision | StalenessPolicy | 根据时间/来源质量计算过期解释 | Strategy/纯规则；A |
| A | Go internal/supervision | CorrelationProjection | 按 Source/Trace/Run/Device 的只读关联索引 | 非持久化 Graph/新工程树；C |
| A | Go internal/supervision | CoverageSummary | 每级当前有/缺来源及原因 | 不因为无数据显示健康；C |

### C2 Go 健康与运行性能（建议，按需扩展 internal/health、metrics）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| A | Go internal/health | ProbeResult | Liveness/Readiness/Quality 的权威观察结果 | 来源、时间、过期、证据；B |
| A | Go internal/health | HealthProbe [接口] | 安全检查单一来源健康，不修改目标 | 无副作用、上下文超时；B |
| A | Go internal/health | HealthProjection | 按 L0～L4 展示聚合健康及未知缺口 | 纯投影，不写 Owner 状态；C |
| A | Go internal/health | DependencyEvaluator | 解释依赖失败导致的不可用原因 | 不得将进程存活等同 Ready；C |
| A | Go internal/metrics | MetricSample | 资源/服务/任务指标的值、单位和时间 | 不得捏造缺失值；A |
| A | Go internal/metrics | MetricReader [接口] | 从现有采样器或专业 Adapter 获取指标 | 分平台 Capability 标注；A |
| A | Go internal/metrics | MetricWindow | 有限时窗聚合及分位统计 | P95/P99 必须真实样本；C |
| A | Go internal/metrics | MetricRetentionPolicy | 采样频率/窗口/体积/清理约束 | 只管理监督采样数据；C |
| A | Go internal/metrics | MetricQueryService | 按 Scope/TimeRange/MetricKey 提供历史查询 | 有界查询与访问控制；C |

### C3 Go 告警、事件和诊断（建议，internal/alerts、events、diagnostics）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| A | Go internal/alerts | AlertRule | 监督域报警条件、持续时间、严重级与抑制规则 | 不复制 PLC/Automation 专业报警；B |
| A | Go internal/alerts | ActiveAlert | 实例身份、当前告警阶段、关联来源与证据 | 不同于现有 Incident；B |
| A | Go internal/alerts | AlertTransition | Active/Ack/Returned/Shelved 等变迁与操作者 | 全量留痕可回放；B |
| A | Go internal/alerts | AlertEvaluator | 规则判定、去重/防抖/恢复 | 仅监督规则策略；B |
| A | Go internal/alerts | AlertJournal | 持久化告警状态变迁/确认记录 | 独立于旧 Program 表且可回溯；B |
| A | Go internal/alerts | AlertNotificationSink [接口] | 通知通道插件与升级记录 | 不得失联静默丢告警；D |
| A | Go internal/diagnostics | DiagnosticTimeline | 将历史事件/Trace/指标准确按时间排序 | 注明源时钟/接收时钟；C |
| A | Go internal/diagnostics | EvidenceBundle | 关联日志、版本、时间线、SHA 与来源清单 | 脱敏/授权导出；C |

### C4 Go 安全、受控进程操作与审计（建议，internal/security、operations）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| A | Go internal/security | Principal | 认证主体身份、范围、会话/有效期 | 不让前端自报身份；B |
| A | Go internal/security | Permission | 进程/服务/站点/操作粒度权限 | 最小授权；B |
| A | Go internal/security | AuthorizationPolicy | 目标与动作的可审计授权判断 | Strategy；目标再次核验；B |
| A | Go internal/security | Authenticator [接口] | 令牌/证书会话验证接口 | 不保存明文秘密；B |
| A | Go internal/security | Authorizer [接口] | 按 Principal/Scope/Operation 判断授权 | 拒绝先于 Handler；B |
| A | Go internal/security | AuditRecord | 操作主体、目标、版本、决定、回执与时间 | 不可变且可追溯；B |
| A | Go internal/security | AuditSink [接口] | 可信持久审计写入/失败处理 | 不能失败后悄悄继续高危动作；B |
| A | Go internal/operations | ProcessActionRequest | Start/Stop/Restart、目标、截止期、幂等键 | 仅 L0 进程操作；B |
| A | Go internal/operations | OperationReceipt | Accepted/Rejected/Executing/Verified/Unknown 证据 | 拒绝混淆网络ACK和执行；B |
| A | Go internal/operations | OperationJournal | 操作去重与未决结果查询 | 不是 MRK 任务数据库；B |
| A | Go internal/operations | ProcessActionGateway [接口] | 向 Daemon 发送经授权的进程操作 | Bridge/Adapter；可回执；B |
| A | Go internal/operations | DaemonActionAdapter | 兼容旧协议并增加强认证边界/结果对账 | 不把旧 UDP 暴露前端；B |
| A | Go internal/operations | TargetStateVerifier | 执行后核实 PID/心跳/服务 Ready 状态 | 区分 Restarted 与 Ready；B |
| A | Go internal/operations | RecoveryPolicy | 退避、次数上限、条件、升级/人工接管 | 复用原 Program 策略优先；B |
| A | Go internal/operations | ProcessActionService | 串联鉴权/幂等/下发/回执/核验和审计 | 不直接调用 OS 不碰专业命令；B |

### C5 Go CxVisual 观察接入与多机（建议，internal/adapters、fleet）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| A | Go internal/adapters | RuntimeObservationSource | 把 Runtime/Run/Task 的只读接口投影为 ObservationBatch | 不写 MRK 状态；C |
| A | Go internal/adapters | ServiceObservationSource | 获取 Service/Capability/依赖与 Ready | 不建 ServiceRegistry；C |
| A | Go internal/adapters | ProfessionalObservationSource | 获取 Device/Driver/CT/AI/Inspection 权威健康 | 专业指标原样保留含义；C |
| A | Go internal/adapters | ObservationTransport | 版本化只读 Bridge/IPC/HTTP 连接边界 | 复用已验证 Transport/安全；C |
| A | Go internal/adapters | ObservationSchemaValidator | 来源版本、身份和字段质量校验 | 未知版本拒绝假解读；C |
| A | Go internal/adapters | SourceReconnectPolicy | 超时/退避/抖动/恢复快照对账 | 不能重放危险写命令；C |
| A | Go internal/fleet | NodeDescriptor | 节点/站点/平台/版本与可用来源声明 | 不是第二工程定义；D |
| A | Go internal/fleet | NodeHeartbeat | 机器级心跳/延迟/代次与断线质量 | 节点 Alive≠业务 Ready；D |
| A | Go internal/fleet | FleetInventory | 经授权的节点清单和现场分组 | 独立监督目录，不含业务真源；D |
| A | Go internal/fleet | NodeObservationClient | 远程节点的只读查询与重连 | TLS/Scope/版本门禁；D |
| A | Go internal/fleet | FleetProjection | 多节点健康/异常/容量聚合 | 断线不抹除来源；D |

### C6 Go 部署/升级观察、HTTP 对外合同（建议，不是第二套部署器）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| A | Go internal/deployment | DeploymentManifestRef | 发布包/配置/模型/程序版本的只读引用 | 真实发布 Owner 在 Host/部署方；D |
| A | Go internal/deployment | DeploymentIntent | 经授权提交给真实部署执行者的请求 | 不能本地擅自替换运行文件；D |
| A | Go internal/deployment | DeploymentReceipt | 已受理/分发/验证/回滚证据 | HTTP 200 不代表部署完成；D |
| A | Go internal/deployment | ReleaseVerifier | 发布后采集真实 Ready/业务状态 | 版本与指纹强核对；D |
| A | Go internal/httpapi | ObservationRoutes | 新增只读 L1～L4 的稳定版本化路由 | 沿用 Gin/Register 模式；C |
| A | Go internal/httpapi | OperationRoutes | 受控 L0 操作和异步状态查询 | 只有 B 安全链闭合才注册；B |
| A | Go internal/httpapi | ErrorEnvelope | 稳定错误码/Trace/时间/安全原因 | 隐藏敏感内部路径；B |

### C7 C# 运行端只读适配（候选归 Applications/Runtime.Host / 现有 Owner 扩展）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| A | Applications/Runtime.Host | RuntimeObservationEndpoint | 封装现有 MRK/MDS 的只读快照入口 | Host 装配，不重造 Runtime；C |
| A | Applications/Runtime.Host | RunSnapshotProjector | 从 MRK Run/Flow/Task 查询生成版本化只读 DTO | 禁止持有并修改运行状态；C |
| A | Applications/Runtime.Host | ServiceHealthProjector | 从现有 Service 模块/Provider 读取实际依赖/Readiness | 不得仅用 IsReady=Running；C |
| A | Applications/Runtime.Host | ResourceSnapshotProjector | 当前 Run/专业资源租约与队列指标的只读投影 | 不复制资源 Manager；C |
| A | Professional Provider | ProfessionalHealthContributor [合同职责] | 专业真实健康/采样/故障事实输出 | 经现有专业接口扩展，非共用 DLL；C |
| A | Applications/Runtime.Host | ObservationAccessGuard | 源端校验只读 Scope、Principal、版本和限流 | 源端不能信任 Go 前端声明；C |

### C8 Web 目标 UI 模型/视图（建议，待 API 才启用）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
| A | web/src/types | ObservationEnvelope [TS interface] | 带来源、时间、质量和 Schema 的统一只读响应 | 不作为后端真源；C |
| A | web/src/types | HealthNode [TS interface] | L0～L4 导航/依赖投影 | 不重建工程树；C |
| A | web/src/types | OperationReceipt [TS interface] | 控制受理、执行、Unknown、核验回执 | 与 Go 协议一致；B |
| A | web/src/stores | useServicesStore [Pinia] | Service/依赖/Ready 的可取消查询与错误旧态 | 真实 API 接通后开放；C |
| A | web/src/stores | useRuntimeStore [Pinia] | Run/Task/Trace 查询状态 | 真实 API 接通后开放；C |
| A | web/src/stores | useOperationsStore [Pinia] | 授权操作/回执/人工确认 | 权限闭合前不渲染操作按钮；B |
| A | web/src/views | ServiceView [Vue] | 服务实例、能力、依赖、健康及诊断 | 只投影真实 L1 API；C |
| A | web/src/views | RuntimeView [Vue] | 运行任务与 Trace 详情 | 只投影真实 L2 API；C |
| A | web/src/views | DiagnosticsView [Vue] | 故障时间线、证据关联及导出 | S09 有数据才上线；C |
| A | web/src/views | FleetView [Vue] | 跨节点/站点分组与失联概览 | D 阶段才启用；D |


## 6. UML：对象分工、依赖与主要生命周期

### 6.1 L0 守护和只读状态：物理已有对象的关系

~~~mermaid
classDiagram
 direction LR
 class Program {
  +int64 ID
  +string Path
  +int MaxCount
  +int MaxError
  +int Enabled
}
 class Status {
  +int64 Id
  +int Pid
  +bool IsRun
  +int StartCount
}
 class Heartbeat {
  +int64 ProgramId
  +int PId
  +int Healthy
}
 class ProgramStatus {
  +DaemonStatus
  +HeartbeatStatus
  +ProcessStatus
}
 class SnapshotProgram {
  +string id
  +bool running
  +float cpuPercent
  +uint64 memoryRssBytes
}
 class Overview {
  +int total
  +int running
  +int stopped
}
 class MonFrame {
  +Unmarshal(data)
  +UMarshal(code,data)
}
 class MonC {
  +SendProgramHeartbeat
  +SendProgramEvent
  +CheckChannel
}
 Program --> Status : 配置/守护
 MonC --> MonFrame : UDP 兼容消息
 Status --> ProgramStatus : 状态汇聚
 Heartbeat --> ProgramStatus : 心跳
 ProgramStatus --> SnapshotProgram : 投影
 SnapshotProgram --> Overview : 汇总
~~~

类图里的 SnapshotProgram 指已有 Go internal/supervision.Program，**仅为图示避免与旧 program.Program 同名**；Go 包与字段定义以真实源码为准。避免将 ProgramStatus 当前可变运行态直接暴露给 H5；返回深度受控的只读拷贝。

### 6.2 扩展对象：Adapter + 策略 + 告警/命令边界（仅设计目标）

~~~mermaid
classDiagram
 direction TB
 class ObservationSource {
  <<interface>>
  +Describe(ctx)
  +Snapshot(ctx,scope)
}
 class RuntimeObservationSource {
  +Snapshot(ctx,scope)
}
 class ServiceObservationSource {
  +Snapshot(ctx,scope)
}
 class ProfessionalObservationSource {
  +Snapshot(ctx,scope)
}
 class SnapshotCoordinator {
  +Collect(ctx,scope)
  +GetCoverage()
}
 class ObservationBatch {
  +source
  +revision
  +quality
  +items
}
 class HealthProjection {
  +Analyze(batch)
  +UnavailableReasons()
}
 class StalenessPolicy {
  +Evaluate(now,meta)
}
 class AlertEvaluator {
  +Evaluate(observation)
  +Apply(rule)
}
 class AlertJournal {
  +Append(transition)
  +LoadActive()
}
 class ProcessActionService {
  +Submit(ctx,request)
  +Query(id)
}
 class Authorizer {
  <<interface>>
  +Allow(principal,scope,action)
}
 class ProcessActionGateway {
  <<interface>>
  +Submit(ctx,request)
}
 class TargetStateVerifier {
  +Verify(ctx,receipt)
}
 ObservationSource <|.. RuntimeObservationSource
 ObservationSource <|.. ServiceObservationSource
 ObservationSource <|.. ProfessionalObservationSource
 SnapshotCoordinator o--> ObservationSource
 SnapshotCoordinator --> ObservationBatch
 ObservationBatch --> HealthProjection
 HealthProjection --> StalenessPolicy
 ObservationBatch --> AlertEvaluator
 AlertEvaluator --> AlertJournal
 ProcessActionService --> Authorizer
 ProcessActionService --> ProcessActionGateway
 ProcessActionService --> TargetStateVerifier
~~~

**聚合非继承优先**。各专业具体 Provider 通过组合 Adapter 接入，不要求全部专业继承一个“万能 CxSupervisionBase”；GoF：Adapter 用于接入，Strategy 用于重试/过期/规则，State 用于活动告警与控制回执，Observer 用于有界刷新，Facade 用于简明 API，Middleware/Decorator 用于日志/审计/认证/速率等 AOP 横切。

### 6.3 健康状态独立：不要向现有 C# Enum 乱加同义状态

~~~mermaid
flowchart LR
 P["ProcessAlive"] --> HC["HealthProjection\n多层状态组合，含来源/时间"]
 HB["HeartbeatSample"] --> HC
 SR["Service Readiness"] --> HC
 DR["Device Readiness"] --> HC
 BS["Business Outcome"] --> HC
 HC --> UI["UI\nHealthy / Degraded / Fault / Stale / Unknown"]
~~~

这里是**UI 状态解释的投影**，不是修改 MLCServiceModuleState 或 MRK 的生命周期。健康维度还须区分 Liveness、Readiness、Quality、Confidence、SourceStaleness、Authorization；比如没有 Service Provider 时 L1 = Unobserved，而不是 Healthy。

### 6.4 受控进程恢复序列（B 阶段，非现有 API）

~~~mermaid
sequenceDiagram
 actor U as 现场运维/Studio
 participant W as Web/API
 participant A as Auth+Audit
 participant O as ProcessActionService
 participant D as Daemon
 participant M as Monitor/Health
 U->>W: 请求重启 ProgramId + OperationId
 W->>A: 验证身份、Scope、权限与确认策略
 alt 越权/安全条件不满足
   A-->>U: Rejected（不触达 Daemon）
 else 获得授权
   W->>O: Submit(有效期、幂等键)
   O->>D: 兼容且已认证的进程操作请求
   D-->>O: Accepted/Rejected/执行状态
   O-->>U: Receipt + 可查询的 OperationId
   O->>M: 校验 PID/心跳与依赖/Service Ready
   M-->>O: Verified/Degraded/Unknown + 证据
   O->>A: 固化审计、回执、版本与恢复结论
   O-->>U: 真实验证结果
 end
~~~

**危险设备控制从来不走这条进程操作捷径**：必须走目标 Runtime/MRK Command/专业 Handler/真实硬件联锁。接入 L1～L4 时只读优先，不允许 Go 通过旧未认证 UDP 协议远程操作硬件。

### 6.5 告警生命周期（B 阶段；与旧 Incident 分离）

~~~mermaid
stateDiagram-v2
 [*] --> Normal
 Normal --> ActiveUnacknowledged: RuleTriggered
 ActiveUnacknowledged --> ActiveAcknowledged: Ack
 ActiveUnacknowledged --> ReturnedUnacknowledged: ConditionCleared
 ActiveAcknowledged --> Normal: ConditionCleared
 ReturnedUnacknowledged --> Normal: Ack
 ActiveUnacknowledged --> Suppressed: ApprovedSuppression
 Suppressed --> ActiveUnacknowledged: RestoreIfActive
~~~

实际发布应以专业报警 Owner/审核批准的状态合同为准；状态图是常见期望模型，未穷尽 Shelved/OutOfService/BadQuality。AlertTransition 记录 SourceAt/ReceivedAt/Actor/Reason/CorrelationId；不把历史文件的一行直接生成“当前 Active”。

## 7. 对外合同：少而完整、源码不重复

### 7.1 只读观察 Provider（Go 候选签名，非当前代码）

~~~go
// 以下接口是设计草案；ObservationRef/Batch 等需查重与定稿。
type ObservationSource interface {
    Describe(ctx context.Context) (SourceDescriptor, error)
    Snapshot(ctx context.Context, scope ScopeFilter) (ObservationBatch, error)
}
type MetricReader interface {
    Read(ctx context.Context, scope ScopeFilter, keys []string) ([]MetricSample, error)
}
type HealthProbe interface {
    Check(ctx context.Context, scope ScopeFilter) (ProbeResult, error)
}
~~~

- ScopeFilter 是候选查询参数类型，**不建第二套 Scope/工程真源**：优先复用平台既有身份或在传输边界稳定映射。
- ObservationBatch 必须包含 Provider/SchemaVersion、SourceEpoch/Generation、SnapshotRevision、SourceAt/ReceivedAt/GeneratedAt、Quality/Staleness、Items/Errors、Coverage；Item 引用现有 Owner 的稳定 ID、Trace/Run/Device Ref。
- JSON/HTTP/IPC 是边界而非内部对象模型；大图像和 CT Volume 不经此观察通道传输，只返回合法引用、大小、版本和权限受控的链接。
- 容量/超时/取消/缓存/旧快照策略需有实测；一个 C# Provider 断开不能阻塞本地进程守护。

### 7.2 API 形状：现有与拟议严格分开

| 类型 | 入口 | 说明 |
|---|---|---|
| 现有 | GET /programs_status | 原结构兼容，不擅自更名 |
| 现有 | GET /api/v1/health | HTTP Monitor 可响应，不代表全平台健康 |
| 现有 | GET /api/v1/overview、/programs、/programs/:id | L0 只读、原运行态投影 |
| 现有 | GET /api/v1/events、/alerts/history | 本地限制、有界历史，不是活动告警 |
| 候选 | GET /api/v2/sources、/health、/services、/runs、/devices | L1～L4 只读投影；具体路径以 API 版本审查定案 |
| 候选 | GET /api/v2/metrics、/traces、/diagnostics | 有真实历史/Trace Provider 才注册 |
| 候选 | GET /api/v2/alerts/active、/alerts/history | 仅在真实 AlertJournal/ACK 生命周期实现后开放 |
| 候选 | POST /api/v2/process-operations、GET /api/v2/process-operations/:id | B 阶段认证＋RBAC＋审计＋Daemon 回执＋状态验证齐备后开启 |
| 候选 | GET /api/v2/fleet/nodes、/releases | D 阶段、按真实节点部署合同启用 |

API 返回错误必须有稳定的 Code、Operation/Trace、来源/时间/授权可见字段，不把 SQL 路径/口令写入响应。参数校验、列表分页、最大负载、Trace 与错误码由共用 HTTP 中间件处理，而不是每个 Handler 复制一套校验器。

### 7.3 受控操作接口的生命周期，不得与 MRK 冲突

命令处理期望：Received → Authenticated → Authorized → Admitted/Rejected → Accepted → Executing → ExecutionResult → TargetStateVerified / Unknown。Go OperationJournal **只保存 L0 进程控制和审计的关联状态**；它不能替代 MRKCommandCenter、MRK 运行 Operation 或专业设备指令的幂等和生命周期。过期/未知操作先查询/对账，不重复执行可能有副作用的动作。

### 7.4 持久化与资源所有权

- 原 Program/Status/Event 的 SQLite/NDJSON/UDP 兼容属于迁移红线；修订另立兼容性 ADR。
- 新监督告警/审计/样本历史可以拥有单独受限存储，**不导入设备变量/流程数据作为第二真源**；治理轮转、上限、腐败恢复、权限和迁移。
- DataRef/VolumeRef/ResultRef 只能指向相应 Owner 的合法资源；跨域查询必须有作用域、授权、版本、租约/过期、安全错误。
- 多 PC 节点注册只代表运维观察目录，不制造跨机器的工程组织第二棵树。

## 8. 12 大功能域到类簇和工作闭环的追溯

| 功能域 | 主要对象/复用入口 | 完整闭环 |
|---|---|---|
| S01 总览 | supervision.Program/Overview、SnapshotCoordinator、HealthProjection、CoverageSummary | SX-A |
| S02 进程 | program.Program/Status/Event、daemonc.RunProgram、ProcessActionService、TargetStateVerifier | SX-A/SX-B |
| S03 服务 | MLCServiceModuleBase、ServiceHealthProjector、ServiceObservationSource | SX-A/SX-B |
| S04 运行 | MRK/MDS/Decision、RunSnapshotProjector、RuntimeObservationSource | SX-A/SX-C |
| S05 性能 | ProcessStatus、MetricReader、MetricSample/Window/QueryService | SX-C |
| S06 通信设备 | F17/MCN/MPA、ProfessionalHealthContributor、ProfessionalObservationSource | SX-A/SX-C |
| S07 专业 | Professional 各 Provider、ObservationBatch/CorrelationProjection | SX-A/SX-C |
| S08 告警 | events.Record、alerts.Incident、AlertRule/Evaluator/Journal/Transition | SX-A/SX-B |
| S09 诊断 | DiagnosticTimeline、EvidenceBundle、MRK Trace/SourceMap | SX-A/SX-C |
| S10 部署 | DeploymentManifestRef、DeploymentIntent/Receipt、ReleaseVerifier | SX-D |
| S11 安全 | Principal、Authenticator/Authorizer、AuditRecord/Sink、OperationJournal | SX-B/SX-D |
| S12 多机 | NodeDescriptor/Heartbeat/ObservationClient、FleetInventory/Projection | SX-A/SX-D |

## 9. 工程师/开发者/测试人员的最短使用体验

**软件开发者**：原专业 Provider 提供只读、版本化、可限制范围的 Snapshot/Health（必要时由 Runtime.Host 适配）；不必继承 Go 基类、不必再实现 CxVisual 命令或数据堆。**工程用户**：Studio/H5 按机台/层级搜索，观察实际值和来源，故障可一键查看 Run/Trace/版本/上下游，未验证时不能执行动作。**测试者**：TestCenter 复用正式 Provider 和相同 DTO、API/授权链，额外注入断线/崩溃/坏时钟/坏权限/数据过期；不得用模拟通过冒充设备已确认。现场操作、监控诊断与安全恢复应是一个连贯界面流程，不靠十几个独立演示页面。

## 10. OOP / GoF / AOP 的具体运用（不是为模式而模式）

| 模式/思想 | 本设计的对象位置 | 具体价值与禁止事项 |
|---|---|---|
| OOP 封装＋组合优先 | Program 配置/Runtime 状态/快照投影、ObservationSource | 配置与运行事实分开，不做 GodManager / 万能基类 |
| Adapter | Runtime/Service/ProfessionalObservationSource、DaemonActionAdapter | 跨语言、跨 Owner 边界映射，不复制原业务实现 |
| Strategy | StalenessPolicy、AuthorizationPolicy、RecoveryPolicy、MetricRetentionPolicy | 可替换故障与容量规则，不硬编码在 Gin Handler |
| State | ActiveAlert/AlertTransition、OperationReceipt | 告警/受控操作各自有可验证生命周期 |
| Observer | 有界刷新、变更订阅/告警通知 | 只订阅必要数据，有 Backpressure/取消，不建第二全局 Bus |
| Facade | 现有 httpapi.Register、未来 SnapshotCoordinator | 对 Web/Studio 简明入口，背后依旧各 Owner 负责 |
| Dependency Injection | 现有 SnapshotProvider、EventProvider、AlertHistoryProvider | 真实/伪源隔离注入，测试可复现，无静态共享测试仓 |
| AOP / Middleware | Authenticator/Authorizer、AuditSink、错误与请求限流 | 认证→授权→审计→执行顺序和 Trace 一致，不靠每页自律 |

**拒绝的反模式**：监控一项指标就建一类、一个 S 域就开一项目、Go ServiceRegistry 与 MFM 目录平行、在 Supervision 重建 MRK Job、把 UDP 心跳当权限证明、靠前端隐藏按钮做授权、用 Mapper 把所有数据变成 object/any 无模式。

## 11. 已查明真实实现与设计差距（针对源码的审评）

| 证据 | 已核查事实 | 必须进一步解决的问题 |
|---|---|---|
| cmd/monitor/app_status.go | ProgramStatus 组合旧心跳/守护/进程资源 | 心跳的“曾有记录”与新鲜度/实际 Ready 相区分 |
| internal/supervision/snapshot.go | Program/Overview、Snapshot/Find/Summarize 只读快照 | 原 DTO 保持兼容，不把它替换成跨层万能对象 |
| internal/httpapi/routes.go | 只读 GET；health 仅 HTTP 响应 | 绝不宣称全系统 Ready，安全写路由待门禁 |
| internal/events/history.go | ReadRecentWhere 有界文件尾窗 | 2 MiB 没有事件不表示无活动告警 |
| internal/alerts/history.go | EventStart 只有消息含“启动失败”才产生 Incident | Daemon 启动失败事件可能直接保存 err.Error()；需配套代码核对和回归 |
| internal/daemonc/daemon.go | 原 RunProgram 管子进程 Wait/重启/失败次数 | 防快速重启、全局运行开关/取消、进程树释放与隔离测试 |
| Mechanism/Core/Modules/Service/MLCServiceModuleBase.cs | 现有 IsReady 为 State==Running | 目标需增加依赖/Provider Readiness 的独立事实，不在 Go 复制状态 |
| Tests/TestCenter/Manual/Module/TCTServiceRequestPanel.cs | 已有真实本地 Echo Service 请求、Start/Stop 与 TestCenter 入口 | 仅证明本地测试闭环，不是远程 Supervision Service 实装 |
| Web client.ts、stores、router | 四个真实只读页面，失败时保留旧快照并标出错 | L1～L4、写控制页面必须等真实 API 后才开放 |

**不在本设计阶段直接修复**上述风险；SR-03 的真实 EventStart 分类遗漏、默认监听/pprof 安全、Ready 语义及 Daemon 生命周期应成为最先源码审评点。

## 12. 分阶段真实实施与验收（完成一段闭环，再推进下一段）

| 阶段 | 建议最小工作包（按依赖，不为类数量开发） | 必测正反 | 证据门槛 |
|---|---|---|---|
| A/P0 | 旧进程快照、历史故障准确分类、过期/错误态、隔离访问、Web/API 基本修复 | 启动失败/事件筛选、日志>2MiB、恶意/非法 ID、断线及过期、Go 三平台构建 | G1 Go test、Web typecheck/Vitest/build、固定 SHA；浏览器/真实 SQLite 独立记录 |
| B/P0 | 最小 Auth/RBAC/Audit，ProcessActionRequest→Daemon 回执→目标核验，告警真实状态机 | 未授权 Handler 零执行、重放/重复、竞态/重启上限、Unknown、告警恢复/确认 | G1 安全与假受管进程集成 + G2 独立服务隔离执行 |
| C/P1 | Runtime.Host/Service/专业只读 ObservationSource，L1～L4 来源/质量/版本/关联 | Service 失依赖、MRK 运行故障、PLC/AI 虚实/断网、跨机/跨进程延迟 | G1/ G2；真实设备只能依据 HIL |
| D/P2～P3 | 多节点、部署与版本治理、统一诊断、告警通知、资源趋势、长稳 | 节点分区/重连、升级失败回滚、日志/磁盘满、跨平台服务管理器、性能/泄漏 | G2 + G3 长稳及安全审计 |

- 每包：**最新 master SHA → 查重现有对象与 02～04 → 小范围正式实现 → 负例测试 → Go/Web/C# CI → TestCenter/联调 → 回填原 12 大功能矩阵 → 中文提交**。
- 只有 G0 设计/对象合同不算完成；有 S 源码也不等于 G1；Windows/macOS/Linux 仅构建成功不等于生产服务长期守护稳定。
- 任何新目录/类如果没有真实消费者、测试和明确 Owner，不要为满足“全类数量”而提前创建。

## 13. 待下一轮裁决的设计边界（默认不阻断本机继续开发）

1. 进程操作链的认证方式、Daemon 结果相关机制：优先在保留旧 UDP/DB 兼容基础上选择可测的最小安全路径，不直接公开旧危险接口。
2. 监控/服务的只读观察跨域方式：先考虑单机显式 IPC/已验证 HTTP 边界，再延伸跨 PC；不得假设现有 MRK Pipe 命令接口可以直接作为 Supervision 全量状态服务。
3. 运营告警与 Automation 专业工艺报警的 Owner 细分：Supervision 负责“监督域运维报警”的独有生命周期，专业 AlarmSource/History 由原 Owner 持有，仅聚合投影。
4. 资源历史与审计存储选型及保留周期：不能未经评估迁动旧 Program SQLite，也不能让 H5 前端缓存成为唯一真源。
5. 实机/现场安全与工业网络隔离：Supervision 是运维软件，不宣称具备 PLC/DCS/SIS/E-Stop 的安全功能。


## 14. 2026-10-30 开发闭环与全类设计实际收敛

本节是后续真实开发记录，原文开头“本轮只修改架构文档”属于 V2.0 初次定稿范围；**不表示本轮始终没有源码变更**。当前 S01/S02/S08 已有 P0 增量，不提前创建本文件列出的候选类簇：

1. **S08 事件归一化**：原 `cmd/monitor/app_event.go` 复用既有 `program.Event`/Monitor 日志 Owner，抽取 `formatProgramDaemonEvent` 纯函数，修正正常退出与短时异常退出的错误说明；`app_event_test.go` 增加 Go 正反测试及历史 NDJSON 序列化读取链。**无需新建 AlertEvaluator 或事件数据库**。
2. **SR-03 澄清**：现有 `dealProgramDaemonEvent` 会先写入中文“启动失败”标记，Daemon 的英文原始系统错误不会直接进入 `alerts.Classify`。旧风险“标准路径因英文漏报”未获证实；代码变更解决的是真实退出描述反向问题。对历史非标准记录仍维持明确分类，不假推断活动告警。
3. **S01/S02 心跳年龄**：`web/src/utils/format.ts` 的 `formatHeartbeatAgeAtSnapshot` 使用 Monitor `generatedAt` 观察时间，不使用浏览器本机时钟；`ProgramsView.vue` 显示时间间隔及“非健康判断”，`format.test.ts` 补上时钟偏差/无心跳/非法时间测试。**不引入固定 TTL 或第二健康状态仓**。
4. **验收边界**：源码已提交；GitHub Actions `supervision-migration.yml` 包含 Go 三平台 build/test 与 Web typecheck/Vitest/build，但本次连接只取得空的状态列表，尚无本轮固定 SHA 的 CI 作业通过证据。G1/G2/G3 均须独立核对，不因新增测试源码宣称已通过。
5. **下一个源码重点**：SR-04 原 Gin、pprof 与监控安全边界；然后按已有 Go 守护策略做不触碰真实被管理程序的隔离故障注入。S03～S07 和 S09～S12 在有权威 Provider 时才实现。

**详细逐项完成度与证据继续以[12 大功能域与实施验收矩阵](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md)为唯一索引，本节只记录对象设计如何映射回真实代码。**

## 15. P0 只读查询安全门禁实际实现（2026-10-30）

本节是 V2.0 设计落实的**源码级增量**，仍非 S11 完整身份/RBAC/审计或 B 阶段远程控制完成声明。

- **直接沿用现有类型而不新增全类**：`internal/httpapi.Register` 复用 `security.LocalOnly()`，覆盖 `GET /api/v1/overview`、`/programs`、`/programs/:id`；`events` 与 `alerts/history` 原来已受限。`/api/v1/health` 保留最小 HTTP 活性探针。
- **真实正反覆盖**：`internal/httpapi/routes_test.go` 检验每个敏感 API 拒绝远程来源、Host 伪造、Forwarded/X-Forwarded-For/X-Real-IP、跨域 Origin 和非法 RemoteAddr，并断言拒绝前绝不执行 SnapshotProvider；回环正常请求继续按旧 DTO 返回。无新增 Go Authenticator/Authorizer 的空实现。
- **兼容与残余风险**：`cmd/monitor/web.go` 的 `httpListenMayBeRemote` 识别非回环/未知绑定，`main.go` 对潜在远程 HTTP 监听输出告警。**旧 `/programs_status` 原访问合同仍未认证，旧 UDP 控制路径也未变**；只能依实际绑定、网络 ACL、部署隔离保护。未实装远程鉴权、审计、命令可靠回执，不能标记 S11 完成。
- **H5 行为**：`web/src/api/client.ts` 将 403 转为明确的本机访问限制提示，`AppShell.vue` 区分“权限受限”与“Monitor 断线”，`web/src/api/client.test.ts` 保持 200/403/500 正反回归。跨机器使用新 H5 的真实受控查看需设计经审批的认证方案，禁止删除 LocalOnly 或代理伪造本机来绕过。
- **证据等级**：当前仅为已写入 master 的源码/测试/文档（S）；GitHub Actions Go 三平台、Web typecheck/Vitest/build 的**本轮 SHA 执行结果尚未取得**，不得冒称 G1。隔离现场部署、网络渗透及旧客户兼容测试仍待 G2/G3。

**对象设计影响**：这次完成了 S01/S02/S11 的部分访问控制，而不是额外创建 P0 类簇；现有 130 项设计条目，不代表 130 个实装类。详细功能与实际完成度回填 [12 大功能域与实施验收矩阵 V1.0](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md)。


## 16. Daemon可靠守护与真实隔离测试（2026-10-30）

> **性质**：已提交的 Go 源码增量及隔离测试源码，不代表本轮三平台 CI 已通过。沿用既定 Monitor + Daemon 进程、Program/SQLite、UDP 与旧 HTTP 合同，不创建新的运行或专业真源。

### 16.1 实际对象与调用归属

| Owner / 源文件 | 本轮实装 | 仍待验证 |
|---|---|---|
| internal/daemonc/daemon.go | 保留 RunProgram 原入口；新增 RunProgramContext；DaemonRun 改为 Go 1.19 atomic.Bool；StartWaitTime 与事件发送支持 Context 取消 | 取消重试不等于已运行子进程已终止，PID 终止仍归原 Daemon Main |
| cmd/daemon/main.go | workerCtx 贯通启动、周期恢复、原重启请求，首次 FirstWaitTime 可取消；信号先取消 Worker；退出通知非阻塞；信号订阅释放 | 异步 PID 跟踪和重复 Worker、进程树清理、OS Service 停止 |
| daemonc.startProc | 验证空路径；父进程 stdout/stderr 文件句柄在子进程 Start 后（成功或失败）释放；保留原输出重定向 | Windows/Linux/macOS 真实长期句柄稳定性 |
| program.Event/Status 与 Daemon Main | 启动失败计数先更新再报告、失败 PID 清零；退出事件保留真实 PID，日志耗时按毫秒→秒换算并明确异常短运行 | 长稳状态对账、现场异常退出确认 |
| internal/monitorc.MonC | 创建时保存 IP 和 Port，首次失败后 CheckChannel 不再重连到空 Host 或端口 0 | UDP 真正断连后的重新连接、UdpC 关闭后的接收协程清理 |
| cmd/daemon 初始化 | 自身 Program 读取失败时 Logger 初始化前输出 stderr，不再调用空 Logger | 真实损坏配置/数据库场景 |

### 16.2 守护与停止调用序列

~~~mermaid
sequenceDiagram
 participant Main as Daemon Main
 participant Worker as RunProgramContext
 participant Child as 受管子进程
 participant Monitor as MonC / Monitor
 Main->>Worker: Start(ctx,Program,FindProgram)
 Worker->>Worker: 查配置/阈值/可取消重试延时
 Worker->>Child: startProc(原 stdout/stderr 合同)
 Child-->>Worker: OS Start / Wait / Exit
 Worker->>Main: 原 Program.Event + 正确失败次数
 Main->>Monitor: 原 UDP 上报
 opt 系统退出信号
 Main->>Main: cancelWorkers + DaemonRun.Store(false)
 Main->>Child: 原 PID 管理链尝试停止
 Worker-->>Main: 取消等待和未消费事件发送
 end
~~~

### 16.3 隔离测试与证据等级

1. internal/daemonc/daemon_test.go：空程序路径拒绝、缺失程序启动失败、日志句柄清理、使用当前测试可执行程序作为短生命周期真实子进程、stdout/stderr 重定向、Start/Wait/Exit、失败计数与 MaxError 终止、长等待取消及无消费者事件取消。
2. cmd/daemon/main_test.go：秒/毫秒单位正确性、短时异常退出、正常退出与时钟异常、首次启动延时取消。
3. internal/monitorc/monc_test.go：连接失败时保存远端 Host 和 Port，并对相同目标重试。
4. 需要核验 Go 三平台构建及 go test -count=1 ./...；可用时使用 go test -race -count=1 ./internal/daemonc ./internal/monitorc ./cmd/daemon。对应 GitHub Actions：supervision-migration.yml。
5. 当前仅有源码提交证据（S），本轮尚未取得可核查的 CI 通过与真实隔离运行记录（G1/G2 待验）；不把测试文件存在视为测试成功。

**全类设计的收敛**：无需为上述 P0 能力批量创建新 Manager/Process/Operation 类，先在原 Go 对象上完成正确性与可靠性。遗留风险包括进程树终止及回执、PID 映射重复 Worker 竞态、UdpC 接收协程退出、真实网络断线、S11 认证审计；S01～S12 进度仍以 [12大功能域矩阵](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md)为唯一索引。


## 17. 守护协程唯一归属的实装（2026-10-30）

S02 本轮实装对象为 cmd/daemon/programWorkerGate（见第 5 章 E1；全类共 130 条，现有及复用 53 条，待实现候选 77 条）。该对象**只负责 ProgramId 对应守护 goroutine 的同步占用和最终退出释放**；Program PID、SQLite 配置、专业执行状态仍归原 Owner。初始启动、配置更改、旧重启入口、周期恢复统一调用 launchProgramWorker，避免 PID 未上报或重试延时中重复创建同 ID Worker。

### 17.1 执行与数据边界

~~~mermaid
sequenceDiagram
 participant C as Daemon启动/恢复/配置入口
 participant G as programWorkerGate
 participant W as RunProgramContext
 C->>G: start(ProgramId)
 G->>G: 同步预占 ID
 G->>W: 启动守护协程
 C->>G: 重复 start(相同 ID)
 G-->>C: 已占用，拒绝并发启动
 W-->>G: Worker 真正返回
 G->>G: 释放占用
~~~

周期恢复先检查现有 Worker 是否占用，避免无意义的 FirstWaitTime 阻塞。配置更新、重启与 Daemon 退出前的停止目标检查使用 safeManagedPID，排除 0、负数和 Daemon 自身 PID。它不是完整 PID 身份/代次认证，因此不等同设备安全保证。

### 17.2 验证与后续边界

- cmd/daemon/workers_test.go：128 个相同 64 位 ID 的并发启动、独立 ID、取消、占用期间拒绝、退出释放重入，以及 PID=0 时恢复跳过已有 Worker。
- cmd/daemon/main_test.go：PID 0/负数/自身 PID 拒绝测试。
- 已执行**独立最小工程（go.mod 声明 Go 1.19，实际使用 Go 1.23.2 / Linux amd64） 最小工程**的门禁实现测试，go test -count=20 及 go test -race -count=10 均通过；该工程使用替身依赖，不等于在仓库真实模块中通过。仓库实际 CI/G1 和现场 G2 仍待验。
- 仍未解决：旧 Worker 退出期间立即启用的续接通知；MaxError/MaxCount 跨五分钟恢复周期锁定；PID 代次与进程树停止确认；S11 认证、授权与审计。**不能因此声称 SX-B 全部完成**。

实际完成度仍以 [12大功能域验收矩阵](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md)为唯一索引。


## 18. Daemon终止闭锁与合法续接（2026-10-30）

本章承接第 17 章的唯一 Worker 归属，但将 **Worker 活跃占用**、**上限终止闭锁**、**配置启用待续接**严格分开。三者都在 cmd/daemon/workers.go 中集中治理，不将程序配置、PID、Trace 或业务状态复制成第二套真源。

~~~mermaid
stateDiagram-v2
 [*] --> Idle
 Idle --> Active: start(合法配置)
 Active --> Active: 周期恢复拒绝重复启动
 Active --> Pending: 显式配置启用请求
 Pending --> Active: 旧Worker返回后交接给新Worker
 Active --> Idle: Worker正常返回
 Active --> Latched: MaxError或MaxCount回调
 Pending --> Latched: 终止上限触发，丢弃待续接
 Latched --> Latched: 周期恢复/旧重启/配置启用拒绝
 Latched --> [*]: Daemon进程结束（当前内存闭锁清除）
~~~

**同步边界**：RunProgramContextWithTerminal 在向既有 Program.Event 通道发送 EventMaxError/EventMaxCount 前调用 markTerminal；markTerminal 的互斥锁同时保护 terminal 与 pending。因此即使 Monitor/Daemon 主循环尚未处理终止事件，周期恢复也不能在 Worker 退出间隙重启。新增回调没有修改旧 wire Event 编码，也不改变 RunProgram、RunProgramContext 的原调用合同。

**合法续接**：新增 gate.request，仅供原配置新增或禁用→启用路径；旧 Worker 未返回时记录待续接，真正退出后同步交接 ID 的占用，确保两个 Worker 不会同时执行。更新为禁用/删除会清除待续接。周期恢复 gate.start 绝不创建待续接请求。旧 UDP 仍未认证，此处的“配置合法”指内容/状态检查，**不能代表 S11 身份和操作授权已完成**。

**安全默认**：终止锁不能由旧重启请求或配置重新启用自动清除；后续需要独立认证、授权、审计、幂等和解除原因确认的管理端处理。当前锁仅存 Daemon 内存，Daemon 自身重启后不保留，不具备跨进程重启的持久熔断保证。

**代码与测试**：internal/daemonc/daemon.go、cmd/daemon/workers.go、cmd/daemon/main.go；新增 MaxError/MaxCount 终止回调先于事件发布、同 ID 排队续接无重叠、闭锁跨恢复拒绝、取消/禁用队列、恢复提前跳过禁用和锁定等正反测试。独立最小工程（go.mod 声明 Go 1.19，实际使用 Go 1.23.2 / Linux amd64） 门禁核心隔离工程的 go test -count=30 -race ./... 已成功，但主仓库 Go CI/Windows/Linux/macOS/G1、真实 Daemon/受管进程 G2 还没有本轮通过记录。以 [12 大功能域验收矩阵](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md)作为完成度唯一来源。

**仍需实现**：上限锁的安全持久化与管理授权解锁、Worker 退出后的真实 OS PID 身份与进程树停止回执、受控远程操作认证审计、服务/设备真正 Ready 验证。


## 19. 守护上限跨 Daemon 重启持久闭锁（2026-10-30）

### 19.1 正式对象关系

复用既有 cmd/daemon/programWorkerGate 作为唯一终止锁 Owner，并引入 **terminalLatchFile / readTerminalLatches / writeTerminalLatches**（cmd/daemon/terminal_store.go），它们是 Daemon 私有的轻量 JSON 持久化辅助，不是新一套 Program DB、OS Service、业务状态仓或网络协议。旧内部 Go 进程、原程序表与 EventMaxError/EventMaxCount 数值不变。

~~~mermaid
sequenceDiagram
 participant Child as Managed Program
 participant Worker as RunProgramContextWithTerminal
 participant Gate as programWorkerGate
 participant File as 本地终止锁日志
 participant Main as 下一次Daemon启动
 Child-->>Worker: 连续启动失败或超过阈值
 Worker->>Gate: onTerminal(EventMaxError/MaxCount)
 Gate->>Gate: 同步设置terminal并拒绝新增Worker
 Gate->>File: 原子替换持久快照（同步落盘）
 Worker-->>Main: 旧Program.Event终止告警
 opt 正常重启Daemon
 Main->>File: 严格校验版本/条目及加载
 File-->>Main: ProgramId→锁定原因
 Main->>Gate: enablePersistence(持久状态)
 Main->>Gate: 自动恢复/重启请求
 Gate-->>Main: 被锁定Program拒绝启动
 end
~~~

**可靠性与正确性**：Daemon 首次启动受管程序前必须完成文件读取与一次写入检查。损坏/不支持的文件、权限错误引发 Daemon 初始化失败而不是自动清锁。程序运行中写入故障将 Gate 转为 storageFault 并拒绝后续 Worker 新启动；旧 Worker 不因该故障自动被终止。正向事件同步持久化减少状态与事件之间的竞态，但不能把磁盘不可写、断电时最后一次文件 Sync 或跨文件系统 Rename 当作完全无风险的持久承诺。

### 19.2 可操作流程与后续边界

- 文件位于 Daemon 工作目录的 supervision/daemon-terminal-latches.json；权限/备份由部署人员保证，需保留 64 位 Program ID 的十进制完整精度。
- 只对 MaxError、MaxCount 两个既有终止原因持久化；非终止 EventExit 不落锁。周期恢复、配置启用与原 UDP 重启均不可自动清锁。
- **没有新增未经认证的远程管理接口或自动解锁**。当前若确认修复已完成，必须先停止 Daemon，完整备份日志并留下现场操作记录，再离线审查修改对应 ID，之后重启验证。正式多用户权限、审计、幂等恢复、有效期/解锁凭证将由 S11 和后续可靠操作 Owner 开发。
- 新增 terminal_store_test.go 的跨实例、坏日志、超大 ID 和磁盘失败测试；使用相同存储算法的独立 Go 1.23.2/Linux 镜像工程运行 go test -race -count=30 ./... 成功。这**不代替当前仓库正式构建、Windows/macOS/Linux CI 或现场 G2/G3**。
- Daemon 历史 exitPidFiles 也已加 safeManagedPID 基础保护，但仍需要目标进程真实身份/代次、退出核验与进程树治理；目前仅可称发送 Kill 请求，不能称 OS 或业务层确认完成。

详见 [12大功能域与实施验收矩阵](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md) 第十四章。


## 20. 受管进程停止身份约束与结果分级（2026-10-30）

P0 已在现有 Daemon 内实现第一道 **目标身份验证**：cmd/daemon/pid_identity.go 通过原 gopsutil OS 进程查询与 Go os.SameFile 比对，确认旧 Program.Path 和所选 PID 的真实可执行文件对应同一文件；否则拒绝 Kill，不为此增设另一个独立进程管理中心。

调用点覆盖旧 PID 文件清理、Daemon 退出、配置更新/删除以及原 UDP 重启。更新配置时优先保存旧 Program.Path 作为核验依据，不相信请求中的替换可执行文件路径。日志明确把“拒绝”“发出终止请求”“实际退出已确认”作为不同等级，目前仅有前两类，不能将 OS Kill 返回 nil 视为真正完成停止。

~~~mermaid
sequenceDiagram
 participant Main as Daemon进程停止入口
 participant Config as 原Program配置
 participant OS as OS进程与可执行文件
 participant Worker as 原守护Worker
 Main->>Config: 读取旧Program.Path
 Main->>OS: PID和实际可执行文件身份核验
 alt 身份不确定或文件不一致
 OS-->>Main: 拒绝终止，记录风险
 else 同一文件候选
 Main->>OS: 发出终止请求
 OS-->>Main: 请求接受或失败
 Note over Main,Worker: 尚未拥有可靠进程退出/进程树/业务Ready回执
 end
~~~

仍未关闭的风险：核验之后到实际 Kill 之间存在 TOCTOU 和 PID 重用可能；原子进程句柄代次、Wait() 确认、完整子进程树和工业安全运行许可仍需后续实现。cmd/daemon/pid_identity_test.go 使用自身测试子进程做正反测试源码，**本轮尚无主仓库 Go 三平台 CI 成功证据**。S11 认证、权限、审计和安全解锁仍未开放。

持久闭锁日志已改为最多 1 MiB+1 字节的有界读取，避免恶意/损坏本地文件造成启动内存无限增长。实际证据和风险级别以 [12 大功能域矩阵](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md) 第十四、十五章为准。


## 21. 原守护Worker停止与Wait确认回执（2026-10-30）

### 21.1 源码级落点与单一Owner

旧 Daemon 的配置变更、旧重启和退出路径原先使用 PID 重新查找进程并发送 Kill，无法确认原进程已退出。新增 internal/daemonc/stop.go：StopController 仅持有**本代 RunProgramContextWithStop 已成功启动的 exec.Cmd**，停止操作使用原始 os.Process 句柄，并由唯一 Worker 在 cmd.Wait 返回后调用 finish。其结果为 StopReceipt（只在 Go Daemon 内部使用，不是外部 API）：

- refused：没有对应 Worker 或活动子进程、代次/PID 不匹配、重复停止；
- signal-failed：向原句柄发送终止信号失败；
- unknown：请求已发出但超时前没有原 Worker Wait 确认；
- wait-confirmed：Worker 对同一个 exec.Cmd 的 Wait 已返回且 ProcessState 可用。**不是完整进程树或专业业务 Ready**。

~~~mermaid
sequenceDiagram
 participant Main as Daemon配置/重启/关闭
 participant Gate as programWorkerGate
 participant Stop as StopController（本代）
 participant Child as 原exec.Cmd子进程
 participant Worker as RunProgramContextWithStop
 Main->>Gate: 查询本代停止控制器
 Gate-->>Main: StopController
 Main->>Stop: RequestStop(期望PID)
 alt 无匹配原始句柄
 Stop-->>Main: refused
 else 有本代句柄
 Stop->>Child: 对原始os.Process发送Kill请求
 Child-->>Worker: 子进程退出
 Worker->>Worker: cmd.Wait
 Worker->>Stop: finish(Wait结果)
 Stop-->>Main: wait-confirmed / unknown / signal-failed
 end
~~~

### 21.2 接入范围、守护原则与留存风险

- cmd/daemon/workers.go 门禁为每一代 Worker 关联一个 StopController，队列中的后继代次在旧 Worker 真正返回之前不能替换停止权。对同一 Program ID 仍仅一个活动守护。
- cmd/daemon/owned_stop.go 将本地停止请求异步投递，不阻塞主事件循环；结果在 main.go 中记录，关停时有界等待。未获得 Worker Wait 结果时写 UNKNOWN，避免把 Kill 请求误报成成功。
- Daemon 配置变更、原 UDP 重启、Daemon 自身退出三条运行时停止路径均接入原进程句柄；旧 PID 文件启动清理没有原句柄，因此仅保留保守的文件身份门禁，**仍无 Wait 确认**。
- 受控停止不计为异常短运行，从而避免误触发 MaxError。原 Program/Event、UDP、SQLite、HTTP 兼容；未新增未经认证的远程写操作。
- 仍存在原句柄停止与 Wait 的操作系统差异、PID 复用边缘场景、孤儿进程、进程树回收、Windows Service/systemd/launchd 终止语义、Daemon 进程被强杀时未完成回执，以及专业 Service/Device Ready；必须继续 G2/G3 验证。

### 21.3 正反测试与证据等级

- internal/daemonc/stop_test.go：测试进程自身作为真实受管子进程，验证 Kill/Wait、无效及过期 PID 拒绝、取消、超时 UNKNOWN、受控退出不计为异常。
- cmd/daemon/workers_test.go 和 owned_stop_test.go：新旧 Worker 控制器交接、退出释放、拒绝不存在的守护者，确认与未知的诊断文本严格区分。
- 已实际在本地 Go 1.23.2/Linux amd64 **独立最小模块**运行 StopController 同构核心的 go test -race -count=15 ./...，通过；**并非主仓库完整构建、Go 三平台 CI 或 G1 通过**。需以固定 SHA 检查监督迁移 GitHub Actions。
- 详细追溯以 [12 大功能域验收矩阵](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md) 为准。


## 22. 停止信号失败与关停PID竞态修复（2026-10-30）

### 22.1 两项真实缺陷与 Owner 纠偏

- 停止信号失败误归因：原 StopController.RequestStop 在 Kill 成功之前将 requested 标志置为 true。若 Kill 失败，原 Worker 后续 cmd.Wait 完成时 finish 仍会把这次真实退出归为受控停止，误清 ErrorCount。现在仅在原 os.Process.Kill 成功后、同一个锁区内标记 requested；失败保持 signal-failed，不掩盖真正的崩溃及重试阈值。
- 关停漏停：Daemon 主循环通过 _runProgramIds 列举正在运行的子进程，而 EventStart 尚未被消费时该表没有真实 PID。现在由 programWorkerGate.ownedChildren 在现有守护代次中读取 StopController.ActivePID，拿到已 Attach 的原始 exec.Cmd.Process，再交 queueOwnedStop 检查代次与进程句柄。关闭时不再依赖事件驱动的旧 PID 表扫描或 os.FindProcess 裸 PID 停止。

~~~mermaid
sequenceDiagram
 participant Worker as 原守护Worker
 participant Gate as programWorkerGate
 participant Main as Daemon主循环
 participant Child as exec.Cmd子进程
 Worker->>Child: cmd.Start
 Worker->>Gate: 当前代StopController持有实际子进程
 Note over Worker,Main: EventStart可能仍在事件通道等待处理
 Main->>Gate: ownedChildren(当前原始句柄快照)
 Gate-->>Main: ProgramId与原始活动PID
 Main->>Worker: queueOwnedStop经原代次控制器请求停止
 Worker->>Child: Kill原始os.Process
 Child-->>Worker: 子进程真正退出
 Worker->>Worker: cmd.Wait及finish
 Worker-->>Main: wait-confirmed或unknown/failed
~~~

**类清单无需新增条目**：这些实现属于既有 StopController、programWorkerGate、RunProgramContextWithStop 的职责细化，避免另造进程管理或采样状态真源。现有 136 项全类对象条目数不变。

### 22.2 真实测试与验收层级

- stop_test.go 增加 Kill 失败后不设受控停止、ActivePID 的附着/Wait 生命周期测试。
- workers_test.go 以真实隔离子进程故意堵住 EventStart 消费，验证旧 PID 表尚未更新时仍能发现活动子进程；取消后 Wait 真实清理。
- supervision-migration.yml 的 Linux Go job 添加针对 internal/daemonc 停止路径的 Go -race 测试；Windows/macOS 原编译与单元测试矩阵仍在。
- 当前 Linux/Go 1.23.2 上同步关键算法的独立最小工程执行 go test -race -count=20 ./... 与 go test -count=20 ./... 均成功。**主仓库全部包构建与 CI 状态未取得可验证的完成记录**，按 S 已提交、G1/G2/G3 未验处理。
- 启动前旧 PID 文件清理仍只有保守可执行文件身份校验，不具备原 Worker Wait 确认；业务流程、设备进程树和 S11 权限审计继续待实现。

实施完成度统一追溯 [12 大功能域验收矩阵](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md) 第十七章。


## 23. 停止超时的迟到结果与守护代次交接（2026-10-30）

### 23.1 对象 Owner 与时序

不引入第二套进程管理、业务执行或运维命令中心。复用 `StopController`、`RunProgramContextWithStop`、`programWorkerGate`，把原 Worker 的 `exec.Cmd.Wait` 作为唯一退出事实，原 Go EventExit/UDP/SQLite 合同不变。

~~~mermaid
sequenceDiagram
 participant Main as Daemon主循环
 participant Stop as StopController（原Worker）
 participant OS as 原exec.Cmd子进程
 participant Worker as RunProgramContextWithStop
 participant Log as Daemon原日志
 Main->>Stop: RequestStopWithLateReceipt(ctx, PID)
 Stop->>OS: Kill原始os.Process
 alt Wait在超时前返回
 OS-->>Worker: 退出
 Worker->>Stop: finish(Wait结果)
 Stop-->>Main: wait-confirmed
 else 超时
 Stop-->>Main: unknown（阶段结果）
 OS-->>Worker: 稍后实际退出
 Worker->>Stop: finish(Wait结果)
 Stop->>Log: action/late（最终确认或继续unknown）
 end
 Worker->>Worker: 原EventExit发送后结束旧代次
 Main->>Main: 已确认旧Worker退出时，门禁才许可新代次
~~~

**关键合同**：停机超时返回 unknown 不得等同成功。`RequestStopWithLateReceipt` 在原 Worker 的 finish 与超时竞态间使用互斥锁，回调最多一次且不持锁调用；不重新查裸 PID、不重复 Kill、不启动无限阻塞的“结果等待线程”。Daemon 尚在运行时，晚到结果直接记原日志；关机或磁盘故障造成日志丢失并非此次设计已经解决。

### 23.2 配置更新与旧重启的真实行为

- 旧 `RunProgramContextWithStop` 在受控停止发出 EventExit 后明确返回，不会在仍 Enabled 时再次自行启动；否则会与排队的新 Worker 形成重复控制。
- 配置更新复用 `program.IsSameProgram`：完全相同的配置不重复停止；变更后的启用配置即便旧配置也是启用状态，仍按原 gate 的 pending/activeStop 归属规则衔接；禁用则清理待续接，不把维护停止当作失败次数。
- 旧 UDP 重启不再用收到的 Program.Path 执行新进程，而是使用 Daemon 的本地配置。对于已有受管进程，只有收到本代 StopController 的 `WaitCompleted` 才会交接后续启动；当出现 refused、signal-failed、unknown 时不自动越过状态校验启动。该接口**仍是旧 UDP 未认证控制面**，不是新增安全远程功能或完整 S11。
- 此轮功能仍属于 S02/S08 的旧 Daemon Owner 和 P0 局部完善，不改变 136 项全类条目数量，不宣称全链路权限审计/跨进程可靠回执。

### 23.3 验证与限制

`internal/daemonc/stop_test.go`：新增真实测试子进程的“先 unknown、后由同一 Wait 补一次最终回执”和“原配置保持 Enabled，受控停止后旧 Worker 必须结束”正反回归；`.github/workflows/supervision-migration.yml`：Linux Race 扩展到该两项及 cmd/daemon 代次切换。独立 Go 1.19 最小模块中，同构 StopController 在 Go 1.23.2/Linux amd64 上实际运行 `go test -race -count=20 ./...`，成功。**此证据不覆盖 cx-visual-x3 主仓库正式 Go 构建及 Windows/macOS/Linux CI（尚未获得本轮通过日志）。**

**G1/G2/G3 仍待验**：Daemon 被 SIGKILL/断电时的孤儿进程恢复与真实状态对账、子孙进程树/OS PID 重用、迟到结果可靠持久化、用户/角色/权限/审批/审计、安全在线解锁、专业 Service/Device Ready 均未在本轮实现。详细追溯参见 [12大功能域验收矩阵](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md) 第十八章。


## 24. 遗留PID与孤儿候选的保守隔离（2026-10-30）

### 24.1 实际对象与 Owner

本轮沿用 `cmd/daemon/programWorkerGate`、`program.Program`、`pid_identity.go`，只新建同一个 Go Daemon 目录下的纯观察函数 `scanLegacyPIDFiles`（`orphan_guard.go`）。它将既有 PID 文件转换为可诊断的启动前隔离理由，不接管子进程 OS 真源，也不把历史 PID 当作可发信号的授权令牌。

~~~mermaid
sequenceDiagram
 participant Boot as Daemon首次启动
 participant Legacy as 旧PID文件
 participant OS as OS进程查询
 participant Gate as programWorkerGate
 participant Worker as 原守护Worker
 Boot->>Legacy: 只读查旧pid/<name>.pid
 alt 文件缺失或PID确认已不存在
 Legacy-->>Boot: 无活动上一代证据
 Boot->>Gate: 正常启动资格
 Gate->>Worker: 原RunProgramContextWithStop
 else 文件损坏/身份不确定/仍有存活进程
 Legacy->>OS: 检查PID存在及可执行文件
 OS-->>Boot: 存活或身份无法确认
 Boot->>Gate: quarantineOrphans(ProgramId,原因)
 Gate-->>Boot: 阻止首次启动、恢复、配置续接
 Note over Boot,Gate: 保留原文件、拒绝自动Kill，交现场离线核查
 end
~~~

旧 `exitPidFiles` 会尝试按 PID 文件找到的进程直接 Kill，即使文件身份吻合也缺少创建代次与原 Worker 句柄确认。现在正式启动中已移除该自动 Kill 路径，改成只读扫描；正常无旧 PID 文件时不影响自动运行，未知状态则隔离。对于多个 Program 共用原旧 PID 文件名、损坏/异常/非普通文件、PID 属于 Daemon 自身、PID 存活但身份不同或身份不确定的情况，全部保守拒绝自动重启。对隔离的 Program，现有 Gate 的 start/request/下一代交接均不允许绕过；没有通过旧 UDP、HTTP 或 H5 新增任何在线清隔离操作。

### 24.2 安全与兼容局限

- 本轮扫描仍依赖旧 PID 文件。文件由 Daemon 主循环消费 EventStart 后保存，因此**子进程已成功启动但 Daemon 在 PID 文件保存前遭 SIGKILL/断电**是仍需解决的遗漏窗口；下一步应在原 Worker 的 Start 成功后、事件发送前持久化 Program ID/启动代次凭证。该凭证是子进程归属证据，不应重建 Program DB、Runtime、MDS 或 S11 操作日志。
- PID 文件可能被其他进程复用；扫描只识别/阻断，不承诺进程树实际停止。对确实存活的同一可执行文件也不能断言属于上一代，所以保留隔离和现场核验。
- 原受管进程的单次启动、StopController/Wait 确认与终止上限跨重启闭锁继续使用本文件前章；本次不是 S11 身份、授权、审计或远程恢复完成。
- 新增 `cmd/daemon/orphan_guard_test.go` 的真实测试子进程正反用例，`supervision-migration.yml` 扩展 Linux Race 筛选；当前源码已提交（S），主仓库完整 Go 三平台 CI/G1、工业 G2/G3 均未取得成功证据。详见 [功能域验收矩阵](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md) 第十九章。


## 25. 守护诊断只读展示与H5跨页联动（2026-10-30）

### 25.1 真实 Owner、数据流和安全边界

新增的是**可用的本机诊断能力**，不是 S11 远程授权操作。终止闭锁继续由 Daemon 的 `programWorkerGate` 与 `daemon-terminal-latches.json` 管理；孤儿隔离继续由 Daemon 启动时 `scanLegacyPIDFiles` 和 Gate 负责。诊断展示另生成带时间戳的 `daemon-orphan-observation.json`；该文件不参与启动许可，写失败不能自动清除内存隔离。Monitor 通过 `internal/guardview.Read` 只读装配两份文件，复用 Gin、`security.LocalOnly` 注册 `GET /api/v1/guard`，无新增进程、数据库、认证替身或可写控制 API。

~~~mermaid
sequenceDiagram
 participant Daemon as Go Daemon（唯一守护 Owner）
 participant Files as 本地supervision诊断文件
 participant Monitor as Go Monitor / guardview
 participant API as GET /api/v1/guard（仅回环）
 participant H5 as Vue5页面
 Daemon->>Files: MaxError/MaxCount锁持久化（原文件）
 Daemon->>Files: 启动孤儿扫描原因＋观察时间（只读投影）
 H5->>API: 本机GET 无写操作
 API->>API: security.LocalOnly
 API->>Monitor: guardview.Read(workDir)
 Monitor->>Files: 有界只读/独立校验来源
 Files-->>Monitor: 记录＋有效性＋观察时间
 Monitor-->>API: terminal/orphans（available/error/items）
 API-->>H5: 只读JSON响应
 H5->>H5: 总览、程序详情、诊断、历史事件跨页对应
~~~

### 25.2 前端真实页面与对象

- `GuardView.vue`：新增第五个正式路由“守护诊断”，双来源计数/来源可用性/观察时刻/完整原因、搜索、闭锁与孤儿分组、按 Program ID 跳转程序；一条 Program 同时被两种状态记录时保持独立行。
- `DashboardView.vue`：新增两类守护保护汇总，未知数据不显示数字 0；`ProgramsView.vue`：增加守护保护列、仅保护项筛选、详情中的原始原因、来源失效警告。
- `EventsView.vue`、`AlertsView.vue`：保持 Monitor 原始事件与历史异常事实，改为可点击 Program ID 的跨页面定位；403 拒绝提示与后端 API 实际错误保持一致。
- `AppShell.vue`：统一导航、全局刷新、相关页面轮询、深浅主题；复用 Element Plus、Pinia、Vue Router 和现有 CSS，不引入新 UI 库。`useGuardStore` 失败后保留可见旧证据但展示过期风险。`guardVerdict` 只在双来源均可读且无对应记录时返回“无已记录保护项”，并不推出 OS/Service/Device Ready。

### 25.3 验收、可观测性及不宣称的能力

1. 后端：`internal/guardview/report_test.go` 校验坏文件/缺失/双来源独立性/时间/64 位 ProgramId；`internal/httpapi/guard_test.go` 校验 LocalOnly 403 不调用 Provider、成功 GET 与不开放 POST；`cmd/daemon/orphan_observation_test.go` 校验 Daemon 投影与 Monitor 读取互通、空记录与非法输入。
2. 前端：`web/src/utils/guard.test.ts` 覆盖两来源组合、“无记录”与“未知”的区别及超大 ID；`web/src/api/client.test.ts` 覆盖只读 GET 和远程 403。Go Windows/Linux/macOS、Linux Race 和 Vue Typecheck/Vitest/Build 已在现有 GitHub Actions 中纳入源码门禁，但**本轮 SHA 尚未取得成功执行记录，不是 G1 已验**。
3. Monitor/Daemon 必须使用能读取同一诊断工作目录的部署配置与权限。闭锁观察时刻为文件最近修改时间；孤儿记录是 **上一次 Daemon 启动扫描**，不能把两者叠加成当前“活动故障”或直接显示为进程存活。
4. 历史 Worker `Start` 成功到 PID/代次凭证持久化的遗漏窗口仍未关闭；S11 登录/授权/审计、安全解锁及正式停止回执、服务/专业 Ready、跨节点监控仍是未完成内容。

以 [12大功能域实施验收矩阵](CxVisual_Supervision_12大功能域与实施验收矩阵_V1.0.md) 第20章为交付证据索引。
