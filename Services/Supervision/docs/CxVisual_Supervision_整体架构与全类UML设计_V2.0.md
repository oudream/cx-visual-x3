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

表列的“现有”包含 Go 函数与 TS 组件，其语言形态须保留；R 为跨 Owner 正式复用；A 仅为设计候选。候选的初始名称用于讨论职责与避免重复，**在已有 C# FQN、Go 类型和 DOCS/02～04 逐项查重以前不视为公开 API 定案**。以下清单是产品级逻辑全类，不是本期要批量自动创建的文件清单。

### E1 原 Go 守护与监控实体（均已存在）

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
| S | web/src/api | fetchOverview/fetchPrograms/fetchProgram [函数] | 当前只读获取进程数据 | 统一 client.ts |
| S | web/src/api | fetchEvents/fetchAlertHistory [函数] | 事件与历史故障查询 | 原访问限制 |
| S | web/src/stores | useMonitorStore [Pinia] | 程序与总览快照/过期错误态 | 不持有服务真源 |
| S | web/src/stores | useEventsStore [Pinia] | 事件查询页面状态 | 只读 |
| S | web/src/stores | useAlertHistoryStore [Pinia] | 异常历史页面状态 | 不等于活动告警 |
| S | web/src/stores | useThemeStore [Pinia] | 浅色/深色 Token | 保留双主题 |
| S | web/src/views | DashboardView/ProgramsView [Vue] | 运行总览与程序详情 | 已有真实接口 |
| S | web/src/views | EventsView/AlertsView [Vue] | 事件记录与异常历史 | 已有真实接口 |
| S | web/src/components | CpuChart [Vue] | CPU 数据图组件 | 不得伪造趋势数据 |

### C1 Go 观察与跨层关联（建议，在 internal/supervision 扩展）

| 状态 | Owner/包 | 类／接口／函数或组件 | 单一职责 | 不变量／阶段 |
|---|---|---|---|---|
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
 class Program { +int64 ID; +string Path; +int MaxCount; +int MaxError; +int Enabled }
 class Status { +int64 Id; +int Pid; +bool IsRun; +int StartCount }
 class Heartbeat { +int64 ProgramId; +int PId; +int Healthy }
 class ProgramStatus { +DaemonStatus; +HeartbeatStatus; +ProcessStatus }
 class SnapshotProgram { +string id; +bool running; +float cpuPercent; +uint64 memoryRssBytes }
 class Overview { +int total; +int running; +int stopped }
 class MonFrame { +Unmarshal(data); +UMarshal(code,data) }
 class MonC { +SendProgramHeartbeat; +SendProgramEvent; +CheckChannel }
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
 class ObservationSource { <<interface>>; +Describe(ctx); +Snapshot(ctx,scope) }
 class RuntimeObservationSource { +Snapshot(ctx,scope) }
 class ServiceObservationSource { +Snapshot(ctx,scope) }
 class ProfessionalObservationSource { +Snapshot(ctx,scope) }
 class SnapshotCoordinator { +Collect(ctx,scope); +GetCoverage() }
 class ObservationBatch { +source; +revision; +quality; +items }
 class HealthProjection { +Analyze(batch); +UnavailableReasons() }
 class StalenessPolicy { +Evaluate(now,meta) }
 class AlertEvaluator { +Evaluate(observation); +Apply(rule) }
 class AlertJournal { +Append(transition); +LoadActive() }
 class ProcessActionService { +Submit(ctx,request); +Query(id) }
 class Authorizer { <<interface>>; +Allow(principal,scope,action) }
 class ProcessActionGateway { <<interface>>; +Submit(ctx,request) }
 class TargetStateVerifier { +Verify(ctx,receipt) }
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
