# CxVisual Supervision——12 大功能域与实施验收矩阵 V1.0

> 2026-10-29。目标：统一运行监督与运维入口，而不是第二套 Runtime、Command、Transport、专业设备状态真源。证据来源：当前 Supervision 源码、CxVisual Runtime/Service 文档及旧 device_communicator 资产档案。
>
> **状态口径**：已实现=源码存在，测试通过=真实CI证据，待完成=无闭环，现场验收须有设备记录。规划不能冒充实现。每项修改都须核对最新 master。

## 一、五级监督架构与 Owner

| 层级 | 真正 Owner | Supervision 的职责 |
| --- | --- | --- |
| L0 进程 | Go Daemon / Monitor | PID、进程存活、运行状态、资源快照、事件与故障恢复 |
| L1 服务 | Service、Runtime.Host 和独立专业服务 | 观察 Live/Ready/Degraded、依赖和可用性，不复制服务生命周期 |
| L2 运行 | MRK、MDS、Decision | 观察 Flow/Run/Task、进度、Trace、SourceMap；不自造执行器 |
| L3 专业 | Automation/Vision/CT3D/AI/Inspection 等 Owner | 观察设备/算法健康与质量指标，不接管硬件安全 |
| L4 业务 | Inspection 和具体产线 Owner | 观察 SN、MES、检测结果及业务绩效，不修改业务判定 |

前端：Studio 白底蓝色浅色主题 / 科技蓝深色主题；H5 和 Studio 消费同一数据语义。服务端：保留 Go Monitor + Daemon 两个进程，HTTP/H5 是 Monitor 内部能力。只有现有 Owner 可以决定其状态和执行结果。

## 二、12 大功能域与现有/计划差距

| ID | 功能域 | 完整功能目标 | 当前 Supervision 实况 | 优先级 |
| --- | --- | --- | --- | --- |
| S01 | 运行总览 | 进程、服务、任务、事件及资源摘要；点选下钻 | 进程总览已实现；其余尚未接入 | P0 |
| S02 | 程序与进程 | 清单/详情、PID、守护策略、启停/重启、历史 | Daemon/Monitor 已有底层实现；新增只读详情页面；远程写操作未开放 | P0 |
| S03 | 服务与模块 | 服务发现、Ready/Degraded、依赖、生命周期 | CxVisual 有 Service 能力，Supervision 未接入 | P1 |
| S04 | 运行任务 | Run/Flow/Task、队列、进度、时序、结果 | MRK 有部分能力；Supervision 尚未接入 | P1 |
| S05 | 资源与性能 | CPU/RSS/磁盘/GPU/网络、趋势、阈值、容量 | 目前只有 CPU/RSS 当前快照；不伪造趋势 | P0 |
| S06 | 设备与通信 | PLC/X-Ray/相机/运动、Channel、协议健康、质量 | 专业 Owner 有部分实现，尚未接入监控 | P1 |
| S07 | 专业能力 | Vision/CT3D/AI/Inspection 的吞吐、延迟、成功率 | 待接入专业接口 | P2 |
| S08 | 事件与告警 | 历史事件、告警级别/确认/恢复、关联、通知 | 已有 NDJSON 原始事件；本轮再实现异常事件有界筛选及只读历史页面；告警生命周期/确认未实现 | P0 |
| S09 | 日志与诊断 | 日志过滤、RunId、Trace、SourceMap 与异常定位 | 基础日志存在；统一查询未接入 | P1 |
| S10 | 部署与版本 | 启动部署、配置、版本、升级、回滚、环境核对 | 程序定义已有；发布治理尚未闭环 | P2 |
| S11 | 权限与审计 | 身份、角色、操作授权、审计、白名单、可靠回执 | 仅安全响应头；事件查询本机限制；认证/授权未实现 | P0前置 |
| S12 | 跨机器管理 | 节点注册、分组、集中查看/告警、远程运维 | 尚未实现 | P3 |

## 三、P0 真实开发验收

| ID | 能力 | 验收条件及防误判 |
| --- | --- | --- |
| P0-01 | 真实进程总览与详情 | 仅展示 Monitor 当前快照；网络断开显示旧快照提示；ID 支持 int64 字符串 |
| P0-02 | 原事件历史查询 | 沿用 `program.event.list`；最多读取末尾 2 MiB，查询限制 1～100 条，缺文件为空，无假事件 |
| P0-03 | 安全隔离 | 事件 API 仅允许回环访问；远程拒绝 403；未完成身份体系前不开放高危写操作 |
| P0-04 | 事件类型 | 对应旧 `EventStart/Login/Heartbeat/Exit/MaxError/MaxCount`；修正新心跳误用登录码；不篡改旧文件 |
| P0-05 | 双主题与易读 | 浅色桌面蓝白、深色科技蓝；主题保存，表格/图表同步；首页减少信息密度 |
| P0-06 | 程序当前资源 | CPU/RSS 真实采样，不把 CPU 合计写成整机指标；无历史不画趋势 |
| P0-07 | CI 和测试 | Go Windows/macOS/Linux、Vue typecheck/Vitest/build，含恶意/错误/超界用例 |
| P0-08 | 后续安全操作 | 只有认证→授权→审计→命令ID→Daemon回执→状态核验后可开启动停/重启 |
| P0-09 | 后续正式告警 | 规则、去重、确认、恢复、通知、保留周期；“心跳缺失”不冒充“业务失败” |
| P0-10 | 后续稳态与现场 | Windows Service/systemd/launchd、断电/异常退出、真实SQLite/前端联合实测 |

## 四、界面结构

只启用真实页面：**运行总览**、**程序与服务（实际以进程为主）**、**事件记录**。未实现的运行任务、专业能力、权限操作不提供假数据和空白菜单。一个首页只展示核心计数、5条进程和一张真实CPU采样图，详情另开抽屉或专题页面。

主题切换的 Owner 为前端 Store + CSS Token。浅色视觉参考 CxVisual Studio 蓝白工程风格；深色为深海军蓝和高亮科技蓝。值的真源永远在后端而不在 H5。

## 五、分阶段实施

- **A / P0 本机只读**：进程状态、详情、事件历史、主题、错误/空态和构建回归。
- **B / P0 安全运维**：鉴权、授权、审计、进程启停/重启确认、告警规则和历史。
- **C / P1 CxVisual 联通**：Runtime.Host、MRK 运行、Service、专业健康的只读观察适配与证据链。
- **D / P2-P3 工业扩展**：跨专业故障、发布升级、多机集中治理；以真实设备和部署需求触发。

**本矩阵是动态目标与验收索引，功能是否完成以实际提交和 CI/现场证据为准。**

## 六、P0 异常历史查询切片（2026-10-30）

真实实现：`internal/alerts` 从原 `program.event.list` **2 MiB 有界尾读**范围内筛选 EventMaxError（失败次数上限）、EventMaxCount（启动次数上限）、明确包含“启动失败”的 EventStart。筛选先于最大 100 条结果限制，避免心跳事件挤占故障记录。

- 新接口：`GET /api/v1/alerts/history?limit=30`（1～100），返回原事件码、Program ID 十进制字符串、PID、Unix 毫秒、事件分类、严重程度及原文。
- 新 H5：独立「异常历史」页，搜索、仅严重项筛选、异常记录及本机限定提示；不向总览增加虚假告警数字。
- 原 `/api/v1/events` 复用 `internal/security.LocalOnly`：检查直接回环来源、可信本机 Host，拒绝转发头及跨域 Origin；无真正认证，**不是远程安全网关**。
- 当前接口只表达“有界日志窗口内的历史故障信号”，**不能表达仍在故障/已确认/已恢复**。没有记录不等于系统安全，源日志超过尾读窗口可能遗漏更早事件。
- 未实装：活动告警状态机、确认/消音/恢复通知、用户登录、权限和危险命令审计。继续作为 P0-B 验收目标。

---

## 七、完整能力期望与验收深化（2026-10-30，设计增补 R1）

> **配套架构与全类设计**：参阅 [Supervision 整体架构与全类 UML 设计 V2.0](CxVisual_Supervision_整体架构与全类UML设计_V2.0.md)。本文件仍是 S01～S12 的需求/证据索引，V2.0 负责类级职责、依赖、接口、UML 及实施顺序；设计候选不代表已经实现。

> **性质：目标规格与核查意见，并非新增功能已实现或测试已通过。** 本节延续前六节的五级监督、S01～S12、P0 当前事实及 A～D 分期，不替代 Go/Web 工程规范。依据：[整体架构](CxVisual_Supervision_整体架构与目录设计_V1.0.md)、[Go 规范](CxVisual_Supervision_Go编码与工程规范_V1.0.md)、[Web 规范](CxVisual_Supervision_Web前端架构与编码规范_V1.0.md)、[Service V1.1](../../../DOCS/Architecture/CxVisual_统一功能模块体系%235Service——服务模块完整设计与验收_V1.1.md)、[Automation 监控/边缘 V1.1](../../../DOCS/Architecture/CxVisual_专业自动化体系%2306工业监控与边缘系统_V1.1.md)、[device_communicator 资产档案](../../../DOCS/Codebases/02_device_communicator.md)、[统一交互 V1.5](../../../DOCS/Architecture/CxVisual_公共机制体系%23统一交互——四大能力与双层总线完整设计及验收_V1.5.md)、[命令中心 Owner 裁决](../../../DOCS/Decisions/CxVisual_统一命令中心双项目归属裁决_V1.0.md)。开发前须结合当前 master、DOCS/02～04 的原表及真实源码复查所有新增类型/接口，不能从本节示例自动生成第二套公共框架。

### 7.1 定位与六项最终期望

**Supervision = 跨进程、跨服务、跨专业、跨设备、按需跨机器的统一运行监督、故障诊断、可靠守护与安全运维平台。** 它不是第二套 Runtime、Scheduler、CommandCenter、Transport、SCADA 控制器或业务数据仓。

1. **看得全**：机器、产线、站点 → 进程 → 服务 → Run/Task → 设备/算法 → 业务结果，可逐层下钻。
2. **看得准**：Alive、Heartbeat、Ready、Degraded、Faulted、Stale、Unknown 的来源与含义不同，不能用一个绿灯替代。
3. **查得深**：一次故障能够关联 Program/Service/Task/Device、Trace、命令、版本与日志，时间、来源、证据可复核。
4. **管得住**：身份认证、角色授权、审批、审计、命令 ID、执行结果、现场状态回读分层表达，不将 HTTP 成功冒充动作完成。
5. **恢复得稳**：故障检测→策略→有界恢复→依赖检查→真实 Ready/业务状态确认，禁止危险控制自动重放。
6. **扩得开**：单机可靠运行是基础；Windows/macOS/Linux 跨平台部署、多 PC 多站点与集中治理通过增量验证扩展。

**终局检验**：发生故障时能够准确回答“哪里出问题、为何出问题、影响了什么、怎样安全处置、恢复是否真正完成”，而非仅增加监控页面和指标。

### 7.2 五级监督及 Owner 边界（保留前文正式分层）

| 层级 | 观察范围 | 原始事实 Owner | Supervision 的边界 |
|---|---|---|---|
| L0 进程 | PID、进程生存、资源、心跳、退出、重启次数、恢复策略 | Go Monitor/Daemon/Program | 汇总并展示进程事实、执行原 Daemon 守护策略；不以 PID 代表业务健康 |
| L1 服务 | ServiceId、InstanceId、依赖、Liveness/Readiness/Degraded、并发与请求 | Core Service 契约、Runtime.Host、专业 Service Provider | 观察服务生命周期与运行事实，不新建 ServiceRegistry 或重复 Host |
| L2 运行 | Flow、Run、Task/Job、队列、进度、结果、Trace、数据引用 | MRK、MDS、Decision、专业执行者 | 下钻、关联及诊断，不复制 JobManager、Scheduler、DataHeap |
| L3 专业 | Automation、Vision、CT3D、AI、Inspection、通道和设备健康 | Professional 与 F17/MCN/MPA 对应 Owner | 观察真实设备/算法质量及运行指标，不直接绕过设备安全 |
| L4 业务 | SN、Station、批次、检测结果、MES 业务确认、产线绩效 | Inspection/具体业务 Owner | 展示健康和影响，不重新生成或修改业务判定 |

**部署历史裁决说明**：旧 device_communicator 资产档案中“编排／运行／AI 三主进程”是当时的 CxVisual 主进程候选部署背景；当前 Supervision 专项已裁决继续保留两个成熟的 Go Monitor、Daemon 进程。二者所指边界不同：**不要用旧候选拓扑撤销已经迁入的 Go 进程，也不把这两个 Go 进程计入 C# 项目数**。

### 7.3 面向人类审阅的协作框架（逻辑示意，虚线属目标）

~~~mermaid
flowchart TB
  UI["Web H5 / Studio / TestCenter<br/>观察、诊断、受控操作"]
  MON["Go Monitor + Gin API<br/>唯一进程状态源、只读快照与事件"]
  DEM["Go Daemon<br/>受管进程启动、守护及恢复"]
  RUN["Runtime.Host / MRK / MDS / Decision<br/>执行、数据、进度、Trace 真源"]
  SER["Core Service / 专业 Service<br/>就绪、依赖、请求能力真源"]
  PRO["Professional + F17/MCN/MPA<br/>设备、协议、算法真源"]
  BUS["Inspection / MES<br/>业务结果与业务确认真源"]
  UI <--> MON
  MON <--> DEM
  MON -.->|"待实现：显式只读观察接口"| RUN
  MON -.->|"待实现：显式只读观察接口"| SER
  MON -.->|"待实现：显式只读观察接口"| PRO
  MON -.->|"待实现：显式只读观察接口"| BUS
  RUN --> SER
  RUN --> PRO
  PRO --> BUS
~~~

- Monitor 复用原 Gin 服务端、UDP/SQLite/Program 合同；Web 是客户端，不增加第三个 H5 后端进程。
- 此图显示目标 Owner 与逻辑方向，**不表示 Runtime/Service/Professional 已实现跨进程对接**。
- Web/Studio 的进程级运维命令未来由 Monitor 安全受理并交给 Daemon；业务命令必须转给目标 Runtime 的 M12/专业 Handler，目标端再次核验安全与物理联锁。
- 不为观察接口另建统一工程真源、全局数据仓、第二条消息总线；SSE/WS、Bridge、跨机 Gateway 均按需求与实证单独实施。

### 7.4 S01～S12 能力期望及具体通过判据

下表“当前基础”是**已有代码/旧资产或专项文档证据**，不代表本轮 CI 与现场验证。

| ID | 最终用户能力 | 当前基础 | 最小闭环验收 |
|---|---|---|---|
| S01 运行总览 | 站点/机台、服务、任务、资源、异常、业务摘要；定位影响并下钻 | 进程总览及真实快照 | 断网保留最后快照并标过期；异常跳转真实来源 |
| S02 程序进程 | 清单、进程树、策略、PID/心跳/资源、启停重启、自动恢复与历史 | 原 Monitor/Daemon/Program，H5 只读详情 | 崩溃、异常退出、上限停机、受控恢复、真实状态确认 |
| S03 服务模块 | 服务发现/能力、依赖拓扑、Ready/Degraded/请求错误与版本 | C# Service 目标契约，未接入 | 依赖缺失不能 Ready；同进程存活与服务就绪独立 |
| S04 运行任务 | Flow/Run/Task/Job 状态、耗时、队列、进度、取消、错误与 Trace | MRK/MDS/Decision 既有职责 | RunId 定位执行节点和 SourceMap；未知不伪成功 |
| S05 资源性能 | CPU、RSS、磁盘、GPU、网络、句柄、吞吐、P95/P99、历史与容量 | 当前只有 CPU/RSS 快照 | 指标单位、对象和采样时间正确；无历史不画趋势 |
| S06 设备通信 | PLC、X-Ray、相机、运动、Channel/Protocol、时延与质量 | 专业及传输 Owner，未统一接入 | 设备失联和 Stale 标记可复现，观察不执行危险动作 |
| S07 专业能力 | AI/Vision/CT3D/Inspection 作业、算法/模型版本、成功率与性能 | 专业 Provider，未接入 | 指标与真实任务/模型/结果关联，分母口径可解释 |
| S08 事件告警 | 事件、活动告警规则、分级、ACK、恢复、抑制、通知、留存 | 原 NDJSON 和有界异常历史 | 历史故障与活动告警明确分开；确认/恢复有证据 |
| S09 日志诊断 | 跨进程日志、RunId/TraceId、SourceMap、时间线、检索/导出 | 基础事件日志 | 同一故障关联多个 Owner；敏感数据脱敏、读取受限 |
| S10 部署版本 | 环境与基线、配置/模型/程序版本、审批、部署、升级、回滚 | 原 Program 定义 | 固定 SHA、部署版本、运行结果、失败回滚与审计 |
| S11 权限审计 | 登录/RBAC、审批、操作授权、命令 ID、审计、拒绝与可靠回执 | 仅本机接口约束和响应头 | 越权绝不触达 Daemon/专业 Handler；回执完整 |
| S12 跨机器 | 节点/站点分组、集中查看、故障汇总、远程受控维护 | 未实现 | 单机失联仍安全运行；重连去重和 Scope 隔离 |

### 7.5 四大端到端闭环（SX-A～SX-D）

| ID | 使用路径 | 操作员应得到的证据 | 必须否决的伪完成 |
|---|---|---|---|
| SX-A 异常发现/根因定位 | 发现异常→健康/数据质量→Program/Service→Run/Device→Trace/日志/版本→原因及影响 | 故障来源、时间、范围、关联对象、是否仍活跃及导出证据 | 只显示 PID；没有数据却写“正常” |
| SX-B 安全故障恢复 | 告警→策略→授权/安全确认→对应 Owner 执行→结果和设备/服务回读→恢复确认→审计 | Accepted/Executing/Rejected/Unknown/Verified 分开，执行者及人工接管明确 | UDP 发送成功即写“重启完成”；无限快速重启或重放危险动作 |
| SX-C 性能问题分析 | 资源异常→真实趋势→Queue/任务耗时→Provider/设备负荷→根因→对比 | 对象、时间窗、P95/P99、模型/配置版本以及 Run 关联 | 子进程 CPU 之和当作整机 CPU，或造假历史线 |
| SX-D 发布与回滚 | 发布申请→版本/配置和兼容检查→授权→受控发布→服务/业务验证→确认或回滚→审计 | 变更版本、目标机器、实际状态、回滚原因和验收证据 | HTTP 200 或进程存在即宣布发布成功 |

SX-A/SX-C 由 Supervision 聚合展示，但事实由各级 Owner 提供；SX-B/SX-D 由具有真实执行权限的 Owner 完成操作，Supervision 负责安全入口、观察、审计和确认。以上闭环是**验收目标，目前未整体实现**。

### 7.6 四类使用者的最短操作路径

| 角色 | 最短路径 | 用户体验红线 |
|---|---|---|
| 现场操作人员 | 选择产线/工位→看异常及影响→点开故障→阅读原因与安全处置建议 | 首页少量可信摘要，不堆指标，Unknown/失联不展示为正常 |
| 运维人员 | 找进程/服务→看策略/依赖→认证与确认→恢复→检查回执与健康 | 每一步清楚知道“谁、何时、对谁、做了什么、实际结果” |
| 软件/算法工程师 | 输入 ProgramId/RunId/SN→查看 Trace、任务、设备、版本和关联日志 | 不必手工拼多个系统日志；问题有可重现证据 |
| 测试/调试人员 | TestCenter/隔离环境→故障注入→观察状态→安全拒绝/恢复验证→输出报告 | 仿真、集成与现场分别标注；不让未实现点检返回 Passed |

完整界面导航建议为：运行总览、程序与服务、运行任务、设备与专业、资源性能、事件告警、日志诊断、部署版本、安全审计、跨机器。**正式 H5 当前仅使用确有真实 API 的运行总览、程序与服务（进程）、异常历史、事件记录四个页面**；其余待后端真实闭环后启用。浅色蓝白与深色科技蓝共用语义、状态、错误与空态。

### 7.7 状态、身份、时间、质量与结果的不变量

1. ProcessAlive（进程存在）、Heartbeat（收到心跳）、ServiceReady（服务依赖就绪）、DeviceReady（专业控制器确认）、BusinessHealthy（业务可用）**不能互相推出**。
2. Stale/Disconnected/Unknown/BadQuality 明确区分；失联可保留最后值并附过期标识，不可默认当 0 或正常。
3. 关联对象按适用范围携带 ProgramId、ServiceId、InstanceId、Scope、RunId、TaskId、DeviceRef、TraceId/CorrelationId、配置版本、算法/模型版本；时间区分来源/接收/处理/展示时间和时钟同步质量。
4. 以上字段是**跨 Owner 关联清单，不是许可创建 UniversalDto、第二套身份或状态仓库**；正式 API 需复用当前 MDS/MRK/MOC/专业 Provider 的现有 FQN/Schema。
5. 事件、历史异常与活动告警不同。当前 alerts/history 是最多 2 MiB 日志尾窗内的历史故障线索；无记录≠当前健康，不具备确认、已恢复或消音状态。
6. 操作成功分层：请求已收到→认证授权→已受理/拒绝→执行中→执行结果→真实目标确认/业务确认。超时或断网时无法证明未执行则为 Unknown；危险命令不自动盲目重发。
7. Variable/Data/Message/Command 沿用现有 MDS/M36/M12/MRK、F17/MCN/MPA 等 Owner；设备读回由专业接口提供，命令不能通过普通事件绕开联锁。

### 7.8 工业可靠性和安全硬约束

- 保留旧 Program ID、SQLite 模式、UDP 消息码/CRC、序列化、配置键、事件码和 /programs_status；变更须单独迁移并回归。
- 原 DaemonRun 包级运行开关、休眠可取消性、退出判定、重启次数和退避需先用隔离假受管进程测试，再渐进治理；CI 不可直接杀宿主关键进程。
- 远程启停/重启/配置写入必须满足**认证→授权→审计→CommandId→Daemon/目标执行回执→状态验证**，否则不开放接口或假按钮；危险 X-Ray/运动操作须由真实目标 Runtime/专业 Provider 再次校验。
- 原 Gin 监听地址、pprof、可信 Origin、代理头、请求限额、超时、身份凭据、日志路径及内容需要安全审核；CRC 不是鉴权。
- Daemon 自身通过 OS 服务管理器可靠运行（Windows Service、systemd、launchd）；Studio/H5 崩溃不得令 Runtime 失去安全运行能力。
- 指标必须标明采样主体和单位；程序 CPU 求和不等于整机 CPU，RSS 是字节；无持续采样/历史库时不绘制趋势。
- 网络分区、重连、时钟漂移、日志滚动、磁盘满、进程树异常退出、限流、资源泄漏、权限失效等必须进入负面测试。跨平台构建成功≠生产现场守护验收。

### 7.9 当前差距与审评问题台账（待实施重新核实）

| ID | 源码/文档证据 | 影响与建议 | 优先 |
|---|---|---|---|
| SR-01 | 旧资产档案 08.4 三主进程候选与现行 Go 双进程设计 | 在部署图明确“CxVisual 主进程”和“Supervision Go 两进程”分别指什么，保持迁移成果 | P0 文档 |
| SR-02 | /api/v1/health 只表示 Monitor HTTP 存活；HeartbeatAtMs > 0 只表示有心跳记录 | 不能推断 Service/Device Ready；补正式健康来源和过期判断 | P0 |
| SR-03 | 已查实 `Daemon.RunProgram → Monitor.dealProgramDaemonEvent → logProgramDaemonEvent → alerts.Classify`：Monitor 原先会为失败事件写入“启动失败”，英文原始错误不会直接导致此标准链漏报 | 已完成源码确认，并补启动成功/英文原始失败/空命令/历史 NDJSON 持久读取正反测试（`app_event_test.go`）；测试通过以当前 GitHub CI 为准，历史非标准日志仍按明确标签分类 | P0 正反测试待 CI |
| SR-04 | 已核实 Monitor `setupHttpServer` 未注册 pprof；`NewHttpServer(..., false)` 不启用通用 CORS；HTTP 监听由配置 Host/Port 决定；旧 UDP 控制协议仍存在 | pprof/跨域不能误报成当前已公开；重点核对部署实际绑定地址、只读 API 的暴露范围、UDP 控制链认证/ACL、生产隔离；远程写接口保持关闭 | P0 安全待验 |
| SR-05 | 有界日志的异常历史不是活动告警状态机 | 后续建立规则、生命周期、通知、ACK/恢复真实状态和保留周期 | P0 |
| SR-06 | Service、MRK、MDS、专业健康未被 Supervision 汇聚 | 先做只读 Provider 及 Scope/Trace/Version 对齐，禁止复制 Registry | P1 |
| SR-07 | 旧 Go 跨平台守护与 sqlite3/CGO | 分别验证 Windows Service/systemd/launchd、信号、SQLite、崩溃恢复及资源闭合 | P1 |
| SR-08 | Web H5 当前为四个只读页面，有 Vue/Go 构建入口 | 补浏览器真实 API、断网、403/500、窄屏、前后端与现场验证 | P1 |

### 7.10 分期实施、验收证据和后续文档治理

| 阶段 | 优先交付的真实工作闭环 | 不得提前宣称 |
|---|---|---|
| A / P0 本机只读 | 原进程守护、真实快照、资源/事件/故障历史、访问隔离、错误空态、Go 和 Web 回归 | 完整活动告警、权限控制、现场设备与长期运行 |
| B / P0 安全运维 | 认证、授权、审计、可靠进程命令、幂等/Unknown、Daemon 回执、状态确认、活动告警 | 未做设备回读时宣称真实恢复或安全控制 |
| C / P1 平台贯通 | Core Service、MRK/MDS/Decision、专业状态的只读接口、Trace/Scope/Version 关联 | 仅 UI 看见 Service 就声称专业闭环 |
| D / P2-P3 工业治理 | 发布升级与回滚、跨机集中观察、趋势/容量、真正站点运维和长期稳定 | 仅 Build/Go test 就宣称跨平台现场可靠 |

**固定证据等级**：G0=设计、Owner 和接口合同；S=源码存在（不自动代表测试）；G1=构建、正反自动测试及固定提交 SHA；G2=隔离跨进程集成/真实设备 HIL；G3=长稳、异常恢复、性能与安全验收。每轮记录 commit、环境、命令、日志、结果、覆盖范围、待验项。禁止由 G0/S/G1 推断 G2/G3。

**文档边界**：本文件保持为 Supervision **五级监督、S01～S12 和完整需求/验收的唯一索引**；具体路由/目录归整体架构，Go/Web 约束归其各自规范，Service 长驻能力、Runtime、MDS、Automation 工业监控与设备安全仍归各自 Owner。后续实际代码提交时回填本矩阵证据，不复写平行状态台账。

**后续开发核查清单**：每轮核对当前 master 和受影响的正式代码、ReferTo、对象 Owner 及 DOCS/02～04；SR-03 的标准事件通路已核实并增加回归，Go/前端 CI 和隔离运行结果须单独登记；下一个重点仍为 SR-04 安全边界与 P0 心跳时效语义，沿 A/B/C/D 以实际代码闭环推进。


## 八、P0 实际源码推进记录（2026-10-30）

- **实际更改**：`cmd/monitor/app_event.go` 抽出纯函数 `formatProgramDaemonEvent`，保留原 Daemon EventStart/EventExit/MaxError/MaxCount 编码及持久化字段，修正 EventExit 的“短时间退出”与“运行时长”文本反向判定；成功退出不输出无意义的 `<nil>`。
- **测试增加**：`cmd/monitor/app_event_test.go` 覆盖启动成功、英文原始启动失败、`cmd is nil`、短时异常退出、正常退出、重启上限，以及 Monitor 规范化→原 `ProgramEventRecord` NDJSON→`alerts.ReadHistory` 的完整读取链；不把普通退出归类为活动告警。
- **事实纠偏**：旧 SR-03 的“英文错误直接进入 alerts.Classify 导致漏报”不适用于现有标准 Monitor 记录路径，因为 `dealProgramDaemonEvent` 先增加了统一中文失败描述。真正已修复的是 EventExit 语义反向。
- **兼容性**：Go 双进程、`Program` SQLite、UDP 帧、事件码、原 `/programs_status`、`GET /api/v1/alerts/history` JSON 结构不变；新代码没有新增服务、数据库或公共模块 Owner。
- **验收等级**：源码已提交（S）；本轮连接环境不能直接克隆仓库到本地运行完整 Go 测试，GitHub Actions 推送结果尚未证实。因此 **G1 未宣称通过**；真实进程/设备长稳属于 G2/G3 待验。触发工作流：`.github/workflows/supervision-migration.yml`，应核对最新提交上的 Go Windows/macOS/Linux 及 Web 作业后回填真实结果。


## 九、P0 心跳快照解释与界面可信性（2026-10-30）

- **改动**：`web/src/utils/format.ts` 新增纯函数 `formatHeartbeatAgeAtSnapshot`，使用 Monitor 的 `generatedAt` 与 `heartbeatAtMs` 计算距快照观察间隔，避免浏览器与监控机器时钟差异；在 `ProgramsView.vue` 的程序列表及详情中显示，明确“心跳时间不代表 Service Ready”。
- **异常语义**：`heartbeatAtMs<=0` 为“从未上报”，非法生成时间标明“快照时间未知”，来源心跳时间晚于快照标明“时钟可能不同步”。不引入未经审批的固定心跳超时阈值，不把年龄转换成 Alive/Ready/Healthy 的权威判断。查询失败沿用已有 Store 的旧快照保留与错误提示。
- **验证入口**：`web/src/utils/format.test.ts` 增加 0/负数/NaN、非法 `generatedAt`、15 秒/125 秒/3665 秒、未来来源时间等正反测试；Vue 的 `typecheck`、Vitest 和构建以相应 GitHub Actions SHA 为准，**本轮未报告 CI 已通过**。
- **兼容性**：前后端现有 API 路径、返回 Schema、旧心跳计数、原 Go 数据与 Daemon 均不变。S01/S02 的完整健康判断仍需 C 阶段权威 Service/Device Provider、来源质量和可配置的探针周期，避免以本次时间间隔提示冒充业务健康。


### SR-04 安全复核补记（2026-10-30）

源码检查 `cmd/monitor/web.go` 只注册原 `/programs_status`、版本化只读 API 及历史异常路由，没有调用 `channels.GinRegister`，故虽然 `common/channels/pprof.go` 定义调试路由，**不能因此宣称生产 Monitor 已开放 pprof**。当前 `cmd/monitor/main.go` 通过 `config.Monitor.Http.Host/Port` 决定监听地址，创建 HTTP Server 时传入 `useCors=false`。生产部署安全仍需验证真实配置、端口暴露、网络访问控制、旧 UDP 控制面与本机诊断端点，不把代码里存在通用 CORS 工具推断为实际已启用。

## 十、P0 敏感进程查询安全门禁与兼容性（2026-10-30）

- **实际代码**：`internal/httpapi/routes.go` 中 `/api/v1/overview`、`/programs`、`/programs/:id` 新增 `security.LocalOnly()`；`/api/v1/health` 保持最小存活探针；原 `events`、`alerts/history` 已沿用同一限制。禁止路径必须先拒绝再调用 Provider，不能泄漏进程 ID、路径、资源与心跳。
- **正反测试**：`internal/httpapi/routes_test.go` 为三个 API 增加直接外网地址、外部 Host、Forwarded/X-Forwarded-For/X-Real-IP、跨域 Origin、无效地址正反测试及拒绝时零 Provider 调用；`cmd/monitor/web_security_test.go` 验证 HTTP 绑定风险提示；前端 `web/src/api/client.test.ts` 区分 403、500、正常 200。
- **生产可见行为**：非本机访问 H5 可能加载静态页面，但受保护 API 会收到 403，UI 标记“监控查询权限受限”；Monitor HTTP 绑定疑似外网时明确警告旧 `/programs_status` **尚未认证**。为保证旧调用方兼容，保留该 API 的旧访问合同；**远程安全闭环未完成，不能作为公网部署依据**。
- **回归及部署门禁**：旧 `/programs_status`、UDP、SQLite、Go Monitor+Daemon、所有 JSON Schema 未更改。新查询安全行为确实可能限制远程旧 H5 使用新的 `/api/v1` 路由，须按 [部署文档](../deploy/README.md) 用回环地址及防火墙或另经审批的认证方案部署，禁止客户端绕过。
- **证据等级**：当前为已提交源码/测试（S）；本轮环境未取得推送 SHA 的 Go 三平台测试、前端 typecheck/Vitest/build 完成证据，G1 待 CI；隔离网络安全验证、旧系统兼容现场测试仍属于 G2/G3 待完成。S11 正式认证授权/审计与 B 期可靠 Daemon 操作仍是未完成目标。

## 十一、P0 Daemon进程守护与故障恢复实装记录（2026-10-30）

> **状态**：本节说明真实代码增量与对应测试源码，不把“已提交”称为 Go 三平台编译通过、隔离真实进程验收或生产长稳。延续 S02/S08/S11/SR-07 及 V2.0 类架构，不另建进程 Manager、命令中心或状态仓。

| 对象/链路 | 本轮已提交的改动 | 证据/风险 |
|---|---|---|
| `internal/daemonc/daemon.go` | `RunProgram` 原签名兼容保留；新增 `RunProgramContext`，包级 `DaemonRun` 改为 `atomic.Bool`；重试 `StartWaitTime` 使用可取消 Timer，退出时发送事件不因无人消费无限阻塞 | 现有 Daemon 主循环使用 Context；已运行子进程仍由原 Daemon PID 控制链终止，**Context 本身不保证等待中的子进程被杀** |
| `cmd/daemon/main.go` | Daemon 启动/恢复/历史重启调用统一传入 `workerCtx`；系统信号先取消等待与事件发送，首次 `FirstWaitTime` 也可中断；退出通知通道缓冲且使用非阻塞发送；信号订阅释放 | 还需真实 SIGTERM/Windows 服务停止、快速重启/重复 Worker、异步事件确认与断电场景集成验收 |
| `daemonc.startProc` | 空可执行路径拒绝；子进程 `cmd.Start()` 成功或失败后关闭**父进程**的日志文件描述符；不改变现有子进程标准输出去向 | `daemon_test.go` 使用测试程序自身的隔离子进程验证 stdout/stderr、日志释放与启动失败；仍需长稳句柄/FD 监测 |
| Daemon 启动/退出事件 | 启动失败先增加 `ErrorCount`/`StartFailCount` 再发送 `EventStart`，失败 PID 清零；保留实际退出 PID 用于事件诊断；Daemon 侧退出耗时统一毫秒→秒，并纠正短时异常退出文案 | `daemon_test.go` 以缺失可执行文件验证两次启动失败→阈值告警、计数/事件次序；`cmd/daemon/main_test.go` 验证正常/异常/时钟回退 |
| `monitorc.MonC` | `NewMonC(ip,port,...)` 保存 `IP/Port`，首次 UDP Dial 失败后 `CheckChannel` 不再使用空地址/端口零重连 | `internal/monitorc/monc_test.go` 验证初始化失败时原目标保留和相同目标重试；不等于完整断线恢复闭环 |
| Daemon 初始化诊断 | `GetProgramById` 失败时，Logger 未初始化前使用标准错误输出路径（`fmt.Printf`），避免空指针调用 | 未以真实异常数据库执行端到端启动验证 |

**隔离测试范围**：`go test -count=1 ./internal/daemonc ./internal/monitorc ./cmd/daemon` 作为本轮重点验证命令，完整 Go 与 Web 构建仍按原 `supervision-migration.yml` 的 Ubuntu/Windows/macOS 矩阵。**本环境 GitHub 连接可提交文件，但没有完整本地 checkout，未获得本轮 Actions 的真实作业通过记录；G1 尚待证据**。

**兼容边界**：旧 Program/SQLite、UDP 帧/CRC/事件码、路径、进程 ID、`/programs_status` 与 H5 API 均未修改。Go 最低版本仍为 1.19（支持 `atomic.Bool`）；该内部 Go 包全局停止开关的访问由普通 bool 改为 `Load/Store`，项目现有调用点已同步迁移，属于进程内源码兼容调整。仍无远程认证、操作审计与安全启停 API。

**明确留存的风险**：原 `common/channels/UdpC` 在底层读异常/关闭时的 goroutine 回收与投递阻塞需单独核查；Monitor 端口真正失联后自动重连与状态对账尚未全验；`_runProgramIds` 异步映射和恢复程序重复启动竞争尚未完整关闭；Daemon 退出时只能根据已跟踪 PID 终止现存子进程，没有完整进程树/服务安全终止回执。不得把本轮 Context 修复说成 SX-B 全链路完成。

**下一交付焦点**：先核查上述隔离测试与 GitHub CI 的固定 SHA，再以真实假受管进程测试单次启动归属、退出竞态和 Daemon/Monitor 断线恢复；只有安全审计与回执、验证均成熟后再开远程控制。


## 十二、S02守护协程唯一归属及停止目标防误判（2026-10-30）

**源码实装**：cmd/daemon/workers.go 新增 programWorkerGate 和 launchProgramWorker；初始启动、周期恢复、配置变更和旧重启入口统一同步占用 ProgramId，Worker 真正结束才释放。与旧 _runProgramIds 的进程 PID 状态不同，新增对象只管理守护 goroutine 的占用，不建立第二份运行状态、Program 数据库或进程管理器。

**具体故障根因**：旧 recoveryPrograms 用 PID 是否大于零判断是否需要启动。在第一次 EventStart 尚未处理、进程退出后重启延时、Daemon 正在清理时，PID=0 不代表 Worker 已退出；旧路径可能重复启动同 ID。新版在处理首次启动等待之前检查 gate.has(ProgramId)，已有 Worker 时直接跳过恢复，降低主循环被无谓 FirstWaitTime 阻塞的风险。

**停止安全**：Daemon 退出、配置更新、旧重启控制三条路径在 FindProcess/Kill 前先通过 safeManagedPID 验证，排除零 PID、负 PID 和 Daemon 自身 PID，避免将不合法的 PID 当作受管子进程。正 PID 仍须后续验证进程身份/代次，不能仅凭整数认定安全。

**正反测试源码**：
- workers_test.go：128 个请求并发争抢相同 int64 ProgramId，只能一个取得守护权；不同 ID 可并行；取消、非法 ID、空 Worker 拒绝；旧 Worker 结束后可重入。
- workers_test.go 的 TestRecoveryProgramsSkipsReservedGuardian：PID=0 且 Worker 仍占用时，周期恢复无需等待其配置的 3600 秒首次延时，也不启动第二个 Worker。
- main_test.go 的 TestSafeManagedPID：零、负数和 Daemon 自身 PID 必须拒绝。
- **有限实测**：以同一门禁算法搭建独立最小工程（go.mod 声明 Go 1.19，实际使用 Go 1.23.2 / Linux amd64） 最小工程、使用 stub 的 Program/Daemon 依赖，go test -count=20 和 go test -race -count=10 均执行成功。该结果**不覆盖**主仓库的正式依赖、真实 recoveryPrograms 集成测试、Go 三系统 CI 或现场设备。

**交付级别与未完风险**：GitHub 上源码及测试已提交（S）；本轮没有完整仓库 Go/Windows/macOS CI 的真实通过记录，原仓库 G1 仍待核实。旧 Worker 退出前配置启用可能被暂时拒绝并延后到下次恢复；原 MaxError/MaxCount 上限仍可能在后续周期重建 Worker 后被重置；正 PID 身份和进程树停止回执尚未关闭。当前只是同一 Program 的并发守护占用修复，不代表 S11 认证授权、完整 SX-B 故障恢复或远程写操作已验收。


## 十三、S02守护上限跨恢复周期闭锁与配置续接（2026-10-30）

**真实源码**：internal/daemonc/daemon.go 新增 RunProgramContextWithTerminal；原 RunProgramContext/RunProgram 保留，旧 EventMaxError、EventMaxCount 数值、JSON、发送渠道不变。到达阈值时，在事件发送之前同步调用终止钩子，cmd/daemon/workers.go 的 programWorkerGate.markTerminal 先锁定 ProgramId，避免 Worker 已退出而事件消费者尚未处理时的恢复竞态。

**闭锁语义**：gate.terminal 是本 Daemon 进程内的 ProgramId→终止原因小型状态表；周期恢复、旧重启请求和配置启用都不能自动清锁，待续接意图也在终止时移除。旧 Program/Status/SQLite、UDP 不新增字段。**尚无安全的远程解锁接口**。Daemon 进程重新启动会丢失本次内存锁，不得声称跨 Daemon 重启的持久闭锁。

**及时续接**：gate.request 为配置新增或禁用→启用记录待接续的 Worker，当旧 Worker 正在退出时，只能在旧 RunProgramContext 真正返回后、继续持有同 ID 占用权的情况下启动新 Worker；周期恢复仍用 gate.start 不排队。配置禁用/删除清空待续接，终止上限触发也清空。现有配置消息通道尚无 S11 正式身份认证，**配置状态满足条件不等于请求已获安全授权**。

**恢复保护**：recoveryPrograms 在 FirstWaitTime 前检查 Program 是否 Enabled 和已终止锁，禁用/锁定程序不等待、不创建守护协程。

**新增测试源码**：workers_test.go 覆盖 MaxError/MaxCount 跨恢复拒绝、配置启用排队交接无重叠、终止时丢弃队列、无关事件不闭锁、禁用/闭锁无延时；daemonc/daemon_test.go 检查终止钩子发生在原终止事件发布前。旧 TestRecoveryProgramsSkipsReservedGuardian 已补 Enabled 前置条件，避免因禁用短路造成虚假的防重通过。

**实测与等级**：本地独立最小工程（go.mod 声明 Go 1.19，实际使用 Go 1.23.2 / Linux amd64） 最小门禁工程执行 go test -count=30 -race ./... 成功（返回码 0），但该工程使用简化依赖，只证明门禁核心并发算法，不代表完整 cx-visual-x3、正式 RunProgramContext 集成测试或 GitHub Windows/Linux/macOS CI 已通过。仓库源码和测试已提交（S），G1/G2/G3 待真实作业/现场证据。

**剩余风险**：Daemon 进程重启清除锁；人工认证解锁及持久化未实现；旧 UDP 配置与重启仍无可信身份；正 PID 身份、进程树终止、核验回执与活动告警仍未完成，不能宣称 SX-B 全链路验收。
