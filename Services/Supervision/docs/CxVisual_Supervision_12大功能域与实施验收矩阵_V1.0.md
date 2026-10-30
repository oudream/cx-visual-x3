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


## 十四、Daemon 守护上限闭锁持久化（2026-10-30）

**实际源码**：cmd/daemon/terminal_store.go 新增严格的版本化 JSON 本地守护上限日志，cmd/daemon/workers.go 接入 terminal 锁读写，cmd/daemon/main.go 在首次启动受管进程之前加载并验证。存放位置为工作目录下 supervision/daemon-terminal-latches.json，与旧 Program/SQLite、原 PID/UDP/HTTP 分离；Program ID 使用 64 位整数的 JSON map key 保存为十进制字符串，保持历史 ID 精度。文件临时写入、0600 权限、Sync 后同目录 Rename；不宣称所有 Windows/文件系统组合均已实现完整断电耐久性。

**状态与拒绝规则**：
- MaxError 或 MaxCount 触发时，原同步 terminal callback 在发布旧 Event 前更新 Gate 的终止锁并持久化；旧事件码与消息合同不变。
- Daemon 正常重启时重新读取已存在的锁，自动恢复、配置启用请求与原 UDP 重启入口不能清除锁，也不会自动运行被锁定的受管程序。
- 启动时日志损坏、版本不支持、条目非法、位置不可写，则 Daemon 在管理任何程序前报 Fatal 并退出（当前退出码 -6），而不是清空故障历史自动恢复。
- 正常运行期间若落盘失败，Gate 记录 storageFault 并拒绝所有新的 Worker 启动及排队续接；现有已运行受管进程不因这一错误被自动杀死。**注意**：如果崩溃/断电时终止锁尚未成功落盘，重启时无法从损坏前的旧日志还原该次终止；此时尚无跨故障介质的完整持久化保证，必须通过监控与现场运维治理。
- 尚未开放浏览器、HTTP、原 UDP 的解锁功能。现阶段只有受控现场/离线处置；不因重新启用配置或旧重启命令而清除 MaxError/MaxCount 终止锁。

**隔离测试源码**：cmd/daemon/terminal_store_test.go 覆盖跨 Gate/Daemon 实例载入、两个终止原因、超大 int64 ProgramId、损坏 JSON/无效原因/版本错误/尾随内容、不可写位置、非终止事件及运行中持久化失败后整体拒启；原 workers_test.go 与 daemonc/daemon_test.go 保留并发和终止事件先锁后发测试。独立 Go 最小测试工程使用相同日志序列化/原子替换算法，在 Go 1.23.2 / Linux amd64 执行 go test -race -count=30 ./... 返回 0；**此为隔离算法验证，非主仓库源文件完整编译/G1**。

**部署及离线恢复规程（不等同正式授权解锁）**：生产必须保证 Daemon 工作目录持久且写入权限最小化，备份并纳入设备恢复清单；发生终止锁时先查清根因、修复程序，再由现场有权限人员停机/停止 Daemon 并对完整日志备份及记录变更理由，必要时离线更新对应条目后重新启动并复核。不能在线删除/编辑日志来要求即时清锁，因为内存 Gate 仍保有锁；更不允许把旧 UDP 重启作为解锁。正式多用户授权、不可篡改审计、双人复核和幂等解锁仍是 S11 待开发。

**进程停止安全边界**：同时将旧 exitPidFiles 路径纳入 safeManagedPID，禁止以 PID 0/负值/Daemon 本身为目标。此谓词只做 ID 基础校验，**不提供正 PID 的创建代次、真实可执行文件身份、进程树退出等待或业务 Ready 回执**；Kill 返回成功仅代表信号/终止请求已发出，不得说进程已完成停止。后续需由持有真实子进程句柄的 Worker 及 OS 平台适配完成目标身份与停止结果核实。

**证据等级**：已提交正式源码/测试/文档（S）；此轮 GitHub Windows/Linux/macOS Go CI、真实集成 Daemon、物理工控机和长稳/断电 G1/G2/G3 未证实，必须在固定 SHA 下验收。


## 十五、停止目标可执行文件身份验证及回执分级（2026-10-30）

**真实源码**：cmd/daemon/pid_identity.go 使用已有 gopsutil/v3/process 查询目标 OS 进程的真实可执行文件，配合 Go os.Stat/os.SameFile 与旧 Program.Path 做同一文件身份对比；空路径、PID 0/负数/Daemon 本身、目标不可访问或路径不相符一律拒绝发送 Kill。该判断不是基于进程名的宽松子串相似度。

**已接入的停止入口**：
1. 启动阶段旧 PID 文件遗留程序清理：原 LoadProgramRunPid 仅是候选，必须再核验 OS 可执行文件。
2. Daemon 停止时对 _runProgramIds 的已登记 PID 进行身份校验；未匹配则保留并报警，不盲目 Kill。
3. 配置更新或删除：先保留更新前旧 Program 的 Path，再对旧 PID 校验；不能使用收到的替换配置路径来确认旧进程。
4. 原 UDP 重启：依据当前本地 Program 配置验证被管子进程，不相信请求传入的任意路径；无法确认则拒绝终止。

**结果分类**：日志明确区分 refused/failed、stop request SENT（请求已发送）与退出真实确认。当前仅覆盖拒绝或向 OS 发出请求；**尚无安全可靠的 Wait() 完成回执、原子 PID 代次绑定、完整进程树终止和专业 Service/Device Ready 确认**。通过 os.SameFile 缩小误杀风险，但核验到 Kill 之间仍存在 TOCTOU/PID 复用窗口；绝不可把此次工作宣布为完整进程身份或安全停止验收。

**隔离测试**：cmd/daemon/pid_identity_test.go 的负向测试拒绝无效/本进程 PID、空可执行路径；正反测试只由 Go 测试启动自己的临时受管子进程，验证同一可执行文件可识别、错误文件必拒绝，清理时只结束测试创建的子进程。原仓库此项测试/Go 三平台 CI 尚未取得本轮完成证据，G1 待核实。

**持久日志质量补强**：terminal_store.go 改为有界流读取，最大为 1 MiB+1 字节，避免启动阶段因超大坏文件分配无界内存；terminal_store_test.go 补超长文件拒启、只读快照不可改变锁、运行中写故障后拒绝全部新启动等测试。工作目录下的监督日志需要受保护和备份；上限锁的正式权限审核、审计和在线解除仍是 S11 未来工作。


## 十六、S02原Worker子进程停止确认与回执分级（2026-10-30）

**本轮实际代码**：internal/daemonc/stop.go 新增 StopController、StopReceipt；internal/daemonc/daemon.go 为原 RunProgramContextWithTerminal 提供兼容入口，新增 RunProgramContextWithStop 以原 exec.Cmd 作为唯一进程句柄和 cmd.Wait 调用方。cmd/daemon/workers.go 将每代 Worker 与其 StopController 同步装配；cmd/daemon/owned_stop.go 异步发起本地停止请求并收集回执。cmd/daemon/main.go 把配置修改、原 UDP 重启和 Daemon 退出时的运行中停止操作接入该机制。

**结果分级**：
- refused：没有本代 Worker、目标 PID 不对应原 cmd.Process、重复/已取消请求；
- signal-failed：原始进程句柄的 Kill 请求返回错误；
- unknown：发出请求，但在有界时间内未拿到原 Worker 的 Wait/ProcessState 证据；
- wait-confirmed：同一个 Worker 原来创建的子进程已经完成 cmd.Wait，ProcessState 可用；**不能推出整个进程树、业务流程、设备安全停止或 Service Ready**。
Daemon 关闭时有限时收集本地回执，只有 WaitCompleted 才允许清理对应 PID 文件；未核实的结果明确写 UNKNOWN。受控停止不会当作短时间崩溃增计 ErrorCount，因此避免在主动维护停止时触发错误的守护上限。

**安全与兼容**：运行中受管进程的停止不再使用裸 os.FindProcess(pid).Kill；原 Worker 控制器是唯一有资格停止其子进程的句柄。被配置启用请求排队的新 Worker 在旧 Worker 返回前不得获得旧停止权。Daemon 初始旧 PID 文件清理没有正在运行的本代 Worker，因此仍依赖已有最小的可执行文件身份校验，属于历史残余风险，不得称具备 Wait 确认。旧 UDP 配置与重启路径保持原协议，**没有因此具备权限认证或安全远程启停**。

**新增测试源码**：internal/daemonc/stop_test.go 对真实一次性测试进程验证成功停止、原 Worker Wait 结果确认、过期 PID/无效请求、确认前超时 UNKNOWN、受控退出不计错误次数；cmd/daemon/workers_test.go 和 owned_stop_test.go 检验控制器代次切换、退出释放、无守护者拒绝及日志分级。

**验证等级**：已实际以相同 StopController 核心创建独立 Go 1.19 模块，使用 Go 1.23.2/Linux amd64 运行 go test -race -count=15 ./... 返回成功。仅可记为隔离核心算法验证，**不是原主仓库全部依赖、Windows/Linux/macOS GitHub Actions 或工业现场 G1/G2/G3 成功**。提交源码与测试（S）已完成，主仓库编译和正式 CI 仍需检查固定 SHA。

**后续闭环**：原子 OS 进程身份/启动代次、孤儿进程与完整子进程树、服务/设备真正 Ready、终止许可、S11 身份/授权/审计、可靠跨进程操作回执以及工控机长稳必须继续开发。当前本地 StopReceipt 没有对 Monitor/H5 发布，不能用它冒充完整 S11。


## 十七、原Worker停止回执竞态修正与关停漏停修复（2026-10-30）

**本轮修复两项已核实的真实边界**：

1. internal/daemonc/stop.go 的 StopController.RequestStop 原先在调用 cmd.Process.Kill 之前标记 child.requested=true。当 OS Kill 失败，而子进程随后自行退出时，finish 仍可能认定这是人为停止，导致真正的短时间异常退出不再累计 ErrorCount。现改为仅当 Kill 返回成功后、在同一互斥区内标记 requested；信号失败记为 signal-failed，不冒称受控退出。
2. Daemon 主循环旧关闭路径从 _runProgramIds 枚举目标。该表依赖主循环收到 EventStart 才更新；当原 Worker 已完成 exec.Cmd.Start 但主循环尚未处理事件时，PID 表仍是 0，关停可能漏掉已运行的真实子进程。现新增 StopController.ActivePID 及门禁 programWorkerGate.ownedChildren，从现有 Worker 持有的原始子进程句柄取得活跃 PID；关停只对这些候选调用 queueOwnedStop，后者仍必须再次比对对应 Worker 代次与进程句柄，不回退到旧裸 PID Kill。

**新增真实测试源码**：

- internal/daemonc/stop_test.go：测试子进程已经 Wait 完成但 Controller 尚未清理时，后续 Kill 必须 signal-failed，且 finish 不得将此次退出标记为受控；另验证未附着/活跃/Wait 完成的 ActivePID 生命周期。
- cmd/daemon/workers_test.go：使用真实测试二进制作为隔离子进程，并让原 EventStart 投递到无人接收的无缓冲事件通道；模拟旧 PID 表尚未更新的窗口，验证 ownedChildren 仍能发现实际子进程，取消后由原 Worker 负责 Kill+Wait，释放后不再暴露 PID。
- .github/workflows/supervision-migration.yml：保留 Linux/Windows/macOS 原三平台构建和常规 Go 测试；Linux 且 CGO_ENABLED=1 时额外对 internal/daemonc 的真实停止测试启用 -race，由 CI 对原仓库实际依赖做竞态回归。

**实际执行与验证等级**：当前 Go 1.23.2/Linux amd64 环境中，将已修改的 StopController 关键算法同步至独立 supervisionstopcheck 最小 Go 模块（不含全仓库依赖），执行 go test -race -count=20 ./... 和 go test -count=20 ./...，两者成功。该验证覆盖 Kill 失败不能伪装受控退出、活动 PID 生命周期、真正的原 Worker Wait 确认/未知结果；**但完整主仓库 Go 模块编译、真实 Daemon 主循环集成、GitHub 三平台 CI 尚未取得固定 SHA 通过证据**。本轮代码与测试已提交（S），G1/G2/G3 未冒称完成。

**保留限制**：基于实际句柄的 ownedChildren 比异步 PID 表更可信，但不能保证进程树、外部子孙进程清理，不能解决 OS 极端重用、Daemon 强制退出、客户进程安全关停策略。unknown 不得向业务宣称停止成功；远程启停和安全解锁仍须 S11 的身份、授权、审计及后续状态核验。


## 十八、停止超时迟到确认与原守护代次交接（2026-10-30）

**源码实装**：
1. `internal/daemonc/stop.go` 保留原 RequestStop 入口，新增 RequestStopWithLateReceipt。停止信号已经被 OS 接受，但原 Worker 的 Wait 未在有界时间内返回时，首次回执严格保持 unknown。之后由**同一原始 exec.Cmd 的 Wait**触发最终回调（wait-confirmed 或仍为 unknown）；不发第二次 Kill，不通过裸 PID 重新发现，不创建不受控的等待 goroutine。finish 与超时分支通过同一锁协调，迟到结果一次性补报；回调在锁外执行。
2. `cmd/daemon/owned_stop.go` 把迟到结果写入现有 Daemon 日志，action 附加 /late，保证在主循环不再消费结果队列时仍尝试记录；队列拥塞时直接写完整结果，不伪造成功。但如果 Daemon 进程已被强制退出或日志不可写，**不能承诺跨故障持久审计**。
3. `internal/daemonc/daemon.go`：本代受控停止的 EventExit 后直接结束旧守护 Worker，不再自动进入下一次启动；ErrorCount 不把此类有意停止当作崩溃。
4. `cmd/daemon/main.go`：更新启用→启用的配置差异时在现有门禁下交接 Worker；同一配置使用既有 program.IsSameProgram 防止无意义停启。原 UDP 重启使用**本地现存配置**而非信任请求中提供的 Program 路径，只有原 Worker 的 WaitCompleted 回执才能触发重启；refused、signal-failed、unknown 不能作为重启许可。旧 UDP 的身份认证/授权仍未实现，不能冒称 S11 完成。

**回归用例**：internal/daemonc/stop_test.go 新增 TestOwnedStopLateWaitReconcilesExactlyOnce（UNKNOWN 后原 Wait 只补一次 wait-confirmed）；TestRunProgramControlledStopEndsEnabledWorkerGeneration（仍启用的 Program 受控停止后，旧 Worker 不自行重启）；Linux Go Race CI 增加 cmd/daemon 守护代次交接检查，Windows/macOS/Linux 既有构建和全量 Go 测试继续保留。

**实测范围**：使用独立 Go 最小模块复现此次 StopController 核心逻辑，在 Go 1.23.2/Linux amd64 执行 go test -race -count=20 ./...，返回 ok。**这是独立算法验证，不是本仓库全部 Go 依赖、Daemon 主循环或 Windows/Linux/macOS CI 的通过证明**。本轮为源码/测试已提交（S），主仓库 G1 与真实设备 G2/G3 待核查。

**剩余风险**：Daemon 强制退出/断电后迟到回执不可可靠记录；跨进程操作账本与幂等操作编号、原 UDP 身份授权/审计、进程树/孤儿进程、PID 复用与业务 Ready 验证尚未完成。该功能没有对 H5 开放未经认证的停止/启动接口。


## 十九、Daemon异常退出的遗留进程隔离（2026-10-30）

**实际源码 S02**：`cmd/daemon/main.go` 删除原 `exitPidFiles` 按旧 PID 文件自动 Kill 的启动行为。旧 PID 文件及同名可执行文件不能证明创建代次/原守护归属，自动杀死存在误伤风险。`cmd/daemon/orphan_guard.go` 使用只读 `scanLegacyPIDFiles` 检查既有 pid 文件：文件缺失或 OS 确认 PID 已不存在时，保留原自动启动；其他不安全或无法核实情况均隔离该 Program，包括同一可执行文件仍存活（归属不明）、PID 已被其他进程复用、权限/检查失败、非普通文件、损坏、异常长度、零/负 PID、自身 PID，以及多个 Program 共享旧 PID 文件名。

**唯一归属**：`cmd/daemon/workers.go` 在已有 `programWorkerGate` 增设进程内 orphaned 隔离标记，不取代 MaxError/MaxCount 的持久锁，不创建新的进程管理中心或第二份 Program 数据库。Daemon 在启动任何 Worker 之前完成扫描和隔离登记；初始化启动、周期恢复与配置启用/待续接均经过该门禁拒绝，日志记录 ProgramId 与隔离原因。保留 PID 文件作为排查证据，不自动删除、不因收到旧 UDP 重启而清除隔离。

**工业兼容与现场处置**：原先“启动后自动清理旧 PID 进程”的行为已被保守隔离取代。上线前必须核对真实旧 PID 文件命名、部署路径、权限及监护策略；隔离后先查清设备实际进程与业务运行情况，授权人员在确认设备安全状态后停止 Daemon、备份记录、离线处理遗留 PID，随后重启验证。旧 UDP 控制仍未认证，不允许在线绕过隔离或自动执行 Kill。

**回归源码**：`cmd/daemon/orphan_guard_test.go` 以当前测试二进制创建一次性真实测试子进程，验证扫描既不自动杀死它也不会重复启动同 ID；覆盖空/损坏/非法 PID、共享文件名、无遗留 PID、已退出且 Wait 的历史 PID、隔离的持续性及配置请求拒绝。`.github/workflows/supervision-migration.yml` 扩充 Linux Race 筛选；原三平台 Go build/test 仍保持。

**证据等级**：修改及测试源码已提交（S）。**没有取得本轮固定 SHA 的主仓库 Go 全量测试和 Windows/Linux/macOS CI 成功输出；G1/G2/G3 未验**，不得把测试文件存在等同测试通过。

**残留窗口**：旧 PID 文件仅在 Daemon 主循环接收 EventStart 后写入。如果它在原子进程成功启动、事件尚未消费之前被强制杀死，仍可能漏报。因此后续应在原 Worker `exec.Cmd.Start` 后、发布 EventStart 前持久化绑定 Program ID/原始进程代次的启动凭证，持久化失败则安全处置原始子进程。OS PID 复用、子孙进程树、SIGKILL/断电场景与专业业务 Ready 仍需专项验收。


## 二十、守护诊断只读API与H5多页面联动（2026-10-30）

**实际接通的完整链路**：原 Daemon 仍为终止上限/孤儿隔离唯一运行 Owner。`cmd/daemon/orphan_observation.go` 在启动扫描完成后将 `ProgramId→隔离原因` 写入工作目录 `supervision/daemon-orphan-observation.json`，包含版本与 UTC 观察时间，私有文件权限、同目录临时写、Sync 和原子替换；**此文件仅供诊断展示，不替代内存 programWorkerGate，也不能解除隔离**。原 `daemon-terminal-latches.json` 继续是 MaxError/MaxCount 的持久闭锁文件，没有新增第二个锁库。

`internal/guardview/report.go` 只读两份诊断文件，1 MiB 有界读取、严格版本/字段/64 位 ProgramId 检查、稳定排序，每份来源分别返回 `available / observedAt / error / items`。文件缺失、损坏、读取失败时明确报告 `available=false`，不以空数组表示“没有故障”。终止锁观察时间为文件修改时间，孤儿扫描时间为原 Daemon 启动扫描时刻；**两者不是当下业务或 OS Ready**。`cmd/monitor/web.go` 通过已有共享工作目录注入只读 Provider，`internal/httpapi/guard.go` 提供 `GET /api/v1/guard`；复用 `security.LocalOnly` / `ReadOnlyHeaders`，HTTP 403 必须在执行 Provider 前拒绝，未引入 POST/PUT/DELETE 或登录、解锁、启停路由。

**已提交的H5界面**：`web/src/views/GuardView.vue` 新增“守护诊断”正式页面，展示闭锁/孤儿两个独立数据源的可用性、更新时间、问题计数、完整原因、类别筛选与 Program ID 定位。`DashboardView` 增加闭锁/孤儿只读计数与来源不完整提醒；`ProgramsView` 增加保护项筛选、详情原始原因与未知状态标识；`EventsView` 和 `AlertsView` 的 Program ID 可定位到程序清单，纠正原 HTTP 403 显示条件；`AppShell` 加入新导航和相关页面的定时刷新；现有 Element Plus、Pinia、Vue Router、深浅主题与自适应 CSS 继续复用，不另造 UI 框架。

**关键安全交互**：程序状态来自 Monitor 当前快照；守护状态来自不同时间的 Daemon 文件。UI 的“无已记录保护项”仅在两份文件都能读取、且该 Program 都不在记录时展示；只要任一来源不可用且未查到明确保护项，就显示“来源不可用”，防止误报安全。查询失败保留旧快照但明确提示可能过期。只读界面**不提供启停/重启、旧 UDP 代理、清锁、告警确认或设备操作**。

**回归与CI**：`internal/guardview/report_test.go` 验证缺失/损坏文件、64 位 ID、来源独立性、时间与格式；`internal/httpapi/guard_test.go` 验证远端 IP、Host、Forwarded、跨 Origin 403/零 Provider 调用和本机只读 HTTP 200；`cmd/daemon/orphan_observation_test.go` 检查 Daemon 写入与 Monitor Reader 互通；`web/src/utils/guard.test.ts` 与 `web/src/api/client.test.ts` 检查未知/闭锁/隔离组合、403 与成功 JSON 契约。`supervision-migration.yml` 三系统 Go Build/Test + H5 typecheck/Vitest/build 原门禁不变，Linux Race Test 增加 guardview/httpapi/monitor/daemon 定向检查。

**证据等级**：相关源码、页面、测试和 CI 配置已提交 GitHub master（S）；本轮仍未取得固定 SHA 的主仓库三平台 Go/前端测试成功日志（G1 待核），现场部署、真实浏览器操作、进程树/业务 Ready 仍未验（G2/G3）。不要以页面显示信息替代安全授权或硬件验收。

**残余风险与下阶段**：历史 PID 文件只在 EventStart 被 Daemon 消费后写入，子进程 Start 成功到凭证持久化的崩溃窗口仍在；必须继续推进 Worker 启动前意图与启动后代次证据持久化、OS 真实进程身份/退出回执对账。守护诊断文件不是可写控制或正式审计账本，S11 认证、授权、持久操作审计及受控解锁仍待实现。若 Monitor/Daemon 使用不同的工作目录或权限，UI 应显示来源不可用，不得假设它们自动共享数据。


## 二十一、启动前持久意图与Crash窗口保守隔离（2026-10-30）

**S02/S08 最新实际源码**：internal/launchjournal/journal.go 新增每 Program 独立的启动尝试凭证（version、十进制 ProgramId、随机 AttemptID、intent/started、PID、UTC 时间和原配置路径）；cmd/daemon/workers.go 只在正式守护 Worker 装配 launchjournal.New，daemonc/daemon.go 新增可选 LaunchRecorder 依赖反转合同和 RunProgramContextWithLaunchEvidence，原 RunProgramContextWithStop/RunProgramContextWithTerminal 签名、EventStart/EventExit 的 UDP 消息及现有 Program SQLite 不变。

**必须遵守的时序**：Worker 在 exec.Cmd.Start 前调用 Begin 写入并同步落盘 stage=intent/PID=0；失败即不启动。原 Start 成功后立刻通过 Started 写入 stage=started 和 PID，**早于原 EventStart 消费及旧 PID 文件落盘**。原 Wait() 成功回收后，核对 AttemptID 并 Finished 清算；如果 Start 根本未成功，也可在错误返回后清算无子进程的意图。Started 写盘失败后仅使用自己的 cmd.Process 进行尝试停止/Wait，保留未清算的可核查证据。任何意图写盘/更新/删除失败通过原 Gate.failStorage 阻止后续新 Worker 和配置排队，不默许自动恢复。

**Daemon 重启隔离**：cmd/daemon/scanUnreconciledPrograms 在受管程序启动前合并 legacy PID 文件与新 launch-attempts/<ProgramId>.json。即使旧 PID 文件不存在，上一代 Daemon 已完成 Begin 却因 SIGKILL 没有记录 PID，也会隔离该 Program；只有经原 Worker Wait 后成功清算才不留下凭证。坏/未知 ProgramId 的凭证仍不能静默忽略；无法归属的异常目录导致整个 Daemon 拒绝启动。不自动 Kill 残留子进程，不清除证据、不开放未经认证的解锁功能。

**H5 投影**：仍复用原 cmd/daemon/orphan_observation.go → internal/guardview/report.go → 本机 GET /api/v1/guard → GuardView/DashboardView/ProgramsView。隔离原因中明确携带未清算启动凭证阶段和 PID。页面用“启动隔离（含凭证）”而非仅“遗留 PID”，显示本次扫描时刻、Program ID 和原始原因；文件源缺失仍是 unknown 而不是安全状态。

**回归源码**：internal/launchjournal/journal_test.go 验证意图前后崩溃、跨进程重载、64 位 ProgramId、原 Worker Wait 清账、无子进程 Start 失败清账、损坏/篡改/软链接/重入拒绝；internal/daemonc/launch_evidence_test.go 利用自身真实子进程与未消费的 EventStart 验证启动凭证先于旧 PID 事件、坏盘不能启动；cmd/daemon/orphan_guard_test.go 验证无旧 PID 仍阻止、旧 PID+新凭证合并、未知文件拒启；workers_test.go 验证 journal 故障阻止全局新启动。supervision-migration.yml 的 Go 三平台构建/全量测试与 Linux Race 包含新包和上述用例。

**证据等级**：正式源码、回归测试、H5 提示与 CI 规则已提交（S），但此轮没有固定 SHA 的 Go Windows/Linux/macOS CI 通过日志或现场浏览器／工控机长稳数据，**G1/G2/G3 均待验**。不能把新测试存在误称测试成功。

**本期承诺边界**：针对 Daemon 进程级 SIGKILL，在文件系统正常工作条件下的 Start 前意图窗口进行保守封堵；AttemptID 并非 OS 原生 PID 创建时间，不能证明 PID 未复用、子孙进程树已退出、设备 Ready；突然断电时的磁盘元数据持久性、目录 ACL、只读挂载、跨平台文件系统差异仍待验证。后续是 OS 创建代次与归属验证、持久操作回执/对账及 S11 的可信认证授权审计。


## 二十二、OS创建时间与可执行文件的只读证据核验（2026-10-30）

**来源与实际源码**：`internal/launchjournal/identity.go` 使用仓库既有 `gopsutil/v3/process` 的 `CreateTime` 与 `Exe` 读取 OS 进程创建毫秒时间和真实可执行文件，利用 `os.SameFile` 核对保存的 Program.Path。`journal.Record` 新增可选字段 `processStartMs`，`Started` 在原 Worker 成功启动子进程之后读取并保存；无法获取创建时间时保持 0（未知），旧凭证不需要无依据地补齐。`Scan` 在**已有未清算凭证**的隔离理由中写入 `osBirthMs` 与 `identity`，包括创建时间不一致、时间吻合但文件不一致、证据不可读或观察吻合。

**唯一安全裁决**：**所有未清算凭证一律继续隔离**，绝不因 PID、创建时间、执行文件均观察一致而自动认领、Kill、删除凭证或解锁。PID 与 OS 时间可能在检查后变化（TOCTOU）、进程树可能部分存活；AttemptID 是 Daemon 尝试标识，不是 OS 原生防 PID 重用的安全句柄。OS 获取失败不影响保守隔离。旧 UDP/SQLite 与 H5 GET API 协议不变，不新增写操作。

**前端**：`web/src/utils/guard.ts` 增加 `identityEvidenceLabel`，依据 Daemon 原始 `identity=...` 显示“PID 创建时间不匹配／证据不足／创建时间及文件观察一致但未证明归属”等解释；`GuardView.vue` 新增“进程身份观察（非授权）”列，`ProgramsView.vue` 在详情中显示相同说明，原始 reason 仍保留以供追溯。该列基于**上次 Daemon 启动扫描的静态结果**，不是实时 OS 状态，不能通过页面筛选影响守护 Gate。

**回归与风险**：`identity_test.go` 覆盖同一 PID 的不同 OS 创建时间、创建时间缺失、仅有 intent 没有 PID、旧凭证不自动放行；`guard.test.ts` 覆盖 H5 三类文字及老格式无身份观察的保守说明。Linux Race CI 回归筛选增加创建时间相关测试，原 Go Windows/macOS/Linux 全量 Build/Test 与前端 Typecheck/Vitest/Build 保持。源码提交已确认；当前尚无本轮固定 SHA 的三平台 CI 完成报告，因此本功能 **S（已提交）/G1 待验/G2、G3 待现场验证**，不得宣称 OS 原子进程身份闭环或安全授权已验收。


## 二十三、启动凭证原子独占与停止流水持久对账（2026-10-30）

**本轮先修确定问题**：`internal/launchjournal/Journal.Begin` 原来先 Lstat 不存在，再通过临时文件 Rename 落盘，中间存在检查/写入竞态。现在按 ProgramId 的 `.json` 使用 `O_CREATE|O_EXCL` 独占创建，Sync 成功后才允许 Worker 调用 Start；崩溃留下半写凭证仍必须隔离。`Started` 先重读磁盘意图并核对 AttemptID/ProgramId/stage，确认同一启动尝试才可写 PID；`Finished` 再核对 ID/stage/PID，拒绝清除被替换的其他尝试。`journal_test.go` 增加 32 并发竞争及缺失/篡改/替换凭证测试。保留 OS 创建时间只读核验，不等于跨进程原子 PID 所有权。

**新的可靠操作证据**：`internal/stopjournal` 的原始 `Entry` 由 128 位 OperationID、字符串 ProgramId、PID、动作、状态、WaitCompleted 与 UTC 组成，写入工作目录 `supervision/daemon-stop-operations.ndjson`。`cmd/daemon/queueOwnedStop` 在提交第一次 OS Stop 之前同步追加并 Sync `requested`，失败则不发 Kill；后续 `refused/signal-failed/unknown/wait-confirmed` 与原 Worker 迟到 Wait 共享同一 OperationID，分事件追加，按已观察到的最高可信 Wait 状态合并，避免延迟 unknown 覆盖 wait-confirmed。损坏、空白、部分写入、超限日志阻止新的停止流水追加和对应停止请求，避免产生不可对账的新操作。4MiB 总上限、4KiB 单条与 200 条最近操作读模型是当前实现限制，不是长周期归档策略。

**原架构和UI**：`internal/httpapi/RegisterStopHistory` 复用原 security.LocalOnly/ReadOnlyHeaders 注册 `GET /api/v1/stops/history`，Monitor 从同工作目录读取，不暴露 POST/PUT/DELETE。`web/src/views/StopHistoryView.vue` 新增“停止历史”只读页面；导航、轮询、未结清统计、状态分类、Operation ID、历史 PID、原始错误、Program ID 跳转均已接入原 Element Plus/Pinia/Vue Router。缺失日志与异常日志是 `available=false`；不可把 requested、unknown 或 Kill 返回 nil 误称退出完成；原 Worker WaitConfirmed 仍不等于业务/设备 Ready。

**回归源码与CI**：`stopjournal/journal_test.go` 包含跨读写重载、64位ID、乱序迟到、身份冲突、损坏文件、并发追加、非法 WaitCompleted、旧日志拒绝继续；`httpapi/stop_history_test.go` 包含外部 IP/Host/Forwarded 403 和本地 GET 200、无写路由；`web/src/api/client.test.ts` 包含只读合同/403。原工作流三系统 Go 全量 Build/Test 和 Vue typecheck/Vitest/build 保留，Linux Race Test 增加 stopjournal/httpapi 对应定向回归。

**证据等级**：目前确认源码、测试、文档已提交 GitHub master（S），但**没有本轮固定 SHA 全量 Go/Windows/Linux/macOS CI、前端构建成功或设备长稳/断电验证**，因此 G1/G2/G3 待验。不能将新包或测试存在称为测试已通过。S11 登陆/身份/授权、不可篡改审计、在线安全恢复、可靠停止业务确认仍未实现。

**现场重要边界**：若操作流水已满/损坏，新受管软件停止请求会因无法持久登记而拒绝；该机制**不能替代独立硬件急停或工业安全回路**，运维人员须按设备实际安全规程处理，离线备份归档后恢复日志，不允许在运行中清空追溯记录。Daemon 被强制杀死时仅保留 requested 表示状态未结清，不自动声称真实进程仍存活或已退出。

## 24. 修复后真实验收增量（2026-10-30）

详见[固定基线、失败根因、命令与边界](CxVisual_Supervision_修复与真实验收_20261030.md)。S02/S08/S09新增：跨进程追加和并发读取、进程被强制终止后OS释放锁、同实例启动凭证并发操作、完整身份索引（超出UI200条仍校验）、回执容量预留。反例均保留损坏/来源不可用状态，不伪报健康。

本机实际结果：Windows Go 1.26.10 + CGO 构建/全包测试/vet通过，全包race及关键用例10次重复通过；真实SQLite初始化与int64往返通过；H5 npm ci/typecheck/25项测试/build通过；真实Monitor六页面、双主题、390px及错误恢复通过。新提交三平台CI另行登记；S03/S04/S06/S07/S11/S12的统一接入和现场能力仍待实施或验收，不因本次P0通过而标记完成。


## 25. S05 真实资源性能采样与H5展示（2026-10-30）

**源码（S）**：`internal/resourceview/resource.go` 复用已有 Go gopsutil 采集整机 CPU、物理内存以及 Monitor 工作目录所在文件系统的容量和使用量；CPU 为约 200ms 真实采样。`internal/httpapi/resources.go` 注册本机只读 `GET /api/v1/resources`，由 `cmd/monitor/web.go` 注入工作目录。原 `supervision.Program` CPU/RSS、UDP/SQLite、Daemon 子进程所有权及其它 Owner 不变。`resourceview.Report` 将采样范围明确标为 `host` 和 `work-dir-filesystem`，每项带 `available / sampledAt / error`，缺权限、超界、无配置或路径不存在均不得当作资源使用率 0。错误不向接口返回敏感 OS 原始路径。

**界面（S）**：`web/src/views/ResourcesView.vue` 已加入“资源性能”正式导航，展示真实主机 CPU、主机物理内存和工作目录所在磁盘，独立展示 Monitor 管理的各程序 CPU/RSS，可排序、搜索 Program ID/路径和跳转已有程序详情。`stores/resources.ts` 查询失败保留旧值但标注可能过期。Dashboard 原“监控进程 RSS”更正为“受管程序 RSS 合计”，避免与主机内存混淆。没有长期数据，**不补历史趋势、不合成 GPU/网络读数、不由 ProcessAlive 推断业务/设备 Ready**。

**正反测试源码**：`resourceview/resource_test.go` 覆盖 CPU 数值 NaN/Inf/负数、矛盾容量、时间、真实临时工作目录、未配置与不存在工作目录；`httpapi/resources_test.go` 覆盖外部 IP/伪造 Host/Forwarded 403、不调用未授权采样 Provider、本地 JSON 和禁止 POST。`web/src/api/client.test.ts`、`stores/resources.test.ts` 覆盖来源不可用、403、64位 ID 风险边界之外的真实数值、离线错误和旧快照保留。

**证据等级与验收**：历史 Codex 固定 `5760a30d` 的 Go 三平台、Web 25 测试及浏览器 G1 已有正式记录；**该成果早于本节 S05 新代码，不能将它的 CI 成功挪作本轮新 SHA 的成功证据**。本轮新增文件目前确认在 master（S），新增后的全量 Go Build/Test/Vet/Race 与 Web Typecheck/Vitest/Build 仍待按新固定 SHA 核查。未来 G2/G3 需真实工控机负载、磁盘权限/多盘部署和长期资源采样；不能把单机软件快照称为容量告警、硬件安全或完整健康。

**后续 S05 差距**：持久多时间窗趋势、采样窗口/时间同步质量、GPU/网络/磁盘 IO、阈值告警、负载容量、进程树统计和真实设备/专业性能观测，待有对应来源与实施验收后逐步加入，不预置虚假数据。


## 26. S03 服务Owner只读快照与Ready证据三态（2026-10-30）

**已落地的最小真实能力（S）**：`Mechanism/Core/Modules/Service/MLCServiceModuleBase.cs` 增加 `MLCServiceModuleObservation` 和 `Observe()`，从现有真实 `MFMModuleDescriptor`、`MLCServiceModuleState` 读取模块 Id/Name/Version、Stopped/Starting/Running/Stopping/Faulted 与 UTC 观察时间。不创建另一个持久运行状态仓、ServiceRegistry、Go Service 生命周期或伪装的本机 HTTP 服务。

**最关键的真假约束**：现有 `MLCServiceModuleBase.IsReady` 仅由 Running 计算，在依赖、专业 Provider、实际请求处理器尚未核实前**不足以判定真实 ServiceReady**。新快照 `VerifiedReady` 明确区分 `null=无验证/未知`、`false=真实非就绪`、`true=当前服务Provider实际核验就绪`。Provider 可覆写 `TryObserveReadiness(out bool)`；默认无证据，观察异常亦不转成健康。非 Running 一律非 Ready。状态切换期间二次读生命周期以降低旧状态与新健康值错配风险，仍不作强一致事务/永久存活证明。

**实际本地Service验证**：`Tests/TestCenter/Manual/Module/TCTServiceRequestPanel.cs` 中现有 `TCTLocalEchoService` 使用真实本地请求处理器注册、接入槽和停止状态覆写观察；无外部依赖时启动后提供 Ready 证据、停止后返回非 Ready；有尚未验证的依赖时 Unknown。现有 WPF 面板显示三态结果，`CurrentObservation` 提供真实只读快照。`Tests/TestCenter/Verification/TCTServiceRequestUiSmoke.cs` 新增 UIA 正反断言。`Tests/ArchitectureSmoke/Integration/ModuleFoundationSmokeTests.cs` 新增 `RunServiceObservation`，`Program.cs` 暴露 `--service-observe`；`supervision-migration.yml` 的 Windows Job 构建后运行专项测试，现有 TestCenter Windows UIA 工作流补正确 Core Service 路径触发。

**验收分界**：源码、测试、UI更新、CI配置已提交（S），但这组新提交的固定 SHA Windows 编译、ArchitectureSmoke CLI 和真实 WPF UIA 尚未取得成功日志，G1 必须待新结果。S03 的跨进程只读观察还需从**实际装载服务实例的 Runtime.Host/专业 Host**发布权威 ServiceId/InstanceId、版本、依赖、来源时间、Generation、Ready 证据、Scope；Go Monitor 只聚合并报告 Stale/Unknown，H5 只消费确认有真实数据的接口。无可靠 Host 发布时服务页面不应显示任何“正常”数据，也不能用 Program Running 或 ProcessAlive 推出 ServiceReady。工业 G2/G3 未验。

**下一工作包**：核实 Host 实际 Service 实例所有者、部署模式及原有 IPC/只读通道，复用现有 Transport/Service/Host 的正式 Owner 实现 L1 权威快照到 Go Monitor 的真正端到端接通；在真实生产/测试 Host 可发布后才开放正式 S03 Web 页面与诊断。不要另造模拟生产服务。
