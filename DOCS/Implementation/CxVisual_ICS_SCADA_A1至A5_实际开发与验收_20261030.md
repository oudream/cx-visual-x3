# CxVisual ICS/SCADA-A A1～A5：实际开发、Owner与Windows验收记录

> 2026-10-30｜工程：.NET Framework 4.8、Windows x64；本文件仅记录已有源码/真实证据与缺口。权威：AGENTS、协同00、Automation #00～#10、MHS/FCM、MDS、Connectivity、MRK、MEM及当前master。

## 1. 真实旧仓资产复用分析

从 `cx-visual-x2` 核查的**实际位置**为 `Workbenches/Automation/ICS/IcsRuntime.cs`、`Workbenches/Automation/SCADA/ScadaRuntime.cs`、`ScadaEngineering.cs`、`ScadaAlarmHistoryPump.cs`，不是项目根的 `Automation/ICS` 路径。

- x2的 `IcsRuntimeHost` 管理Provider/Connection/Device并检测ID重复和Provider能力匹配；x3用MEM持久资源身份、MHS生效Connector、PAD Provider及正式Host装配承担相同职责，不能移回旧的并行DeviceRegistry。
- x2的 `ScadaEngineeringProject` 独立保存连接、变量、报警、历史和配方，并用JSON Serializer处理TimeSpan；x3复用MEM+MHS/FCM已有唯一工程和生效版本，不导入第二份可写工程JSON。
- x2的 `ScadaStateCache` 为HMI/视图持有Latest副本；x3严守MDS唯一Current/Quality，不迁入独立可写Cache。由HMI按MDS订阅及快照刷新。
- x2的 `ScadaAlarmHistoryPump` 通过有界队列避免慢磁盘阻塞采样；这属于后续正式公共历史/告警Owner复用目标，当前**没有搬运线程、AlarmHistory表或私有持久化**。

## 2. 第一批源文件与边界

| 任务 | 本批实际源码 | 状态 |
|---|---|---|
| A1 工程装配 | `Applications/Runtime.Host/MRHScadaRuntime.cs` + `Professional/Automation/Scada/PADScadaDeviceSession.cs` | 已提交：正式MEM资源Guid、MHS Effective、MCN配置与MPA映射导出PAD/MDS，缺少MEM Device→Connector专属持久引用及站点拓扑 |
| A2 长采 | `PADPlcPollingSession`、`PADScadaDeviceSession` | 已提交：FCR不重入、周期/连续FC03、停止/重启、故障Stale与版本漂移阻断；断线自动恢复策略尚无 |
| A3 数据 | `MDSVariableTable`、`MDSVariableProviderHost`、`PADScadaDeviceSession.Inspect` | 同进程MDS快照/变更订阅已有；跨进程鉴权数据流/订阅尚未实现，不对HMI公开非授权Pipe |
| A4 命令 | `PADPlcMrkCommandRegistration`、`PADPlcMrkWriteCapabilityProvider` | 已提交：现有MRK双授权、资源租约、FC06+FC03、Unknown、可选Effective版本必填；本次SCADA CLI默认只读，远程HMI授权服务未装配 |
| A5 Host | `Runtime.Host.exe scada-serve`、`--service`、`MRHScadaSmoke` | 已提交：独立无Studio读取进程、保存重开、状态与会话、SIGINT优雅停；SCM Windows Service、可信发布Plan与SCADA Session绑定、跨进程命令认证未完成 |

`MRHScadaRuntime` 不使用原 `MRHSimulationPublishPolicy` 作为设备授权，且不覆盖已有受信Plan发行/Journal系统；当前只读采集尚不构成完整生产SCADA运行内核。任何注册控制命令必须由外部真实身份策略、MRK授权及PAD专业授权同时满足。ACK/回读一致不等于物理机构到位。

## 3. 正反测试与CI

`Applications/Runtime.Host/MRHScadaSmoke.cs` 通过真正 OS `TcpListener` / `FTRTcpClientChannel` / `MPAModbusTcpProtocol` 发真实MBAP/FC03并检查73、99两个寄存器，不模拟协议返回：

1. MEM资源真实保存 `.cxproj` 并重新打开，非法DeviceRef拒绝；
2. MHS Connector配置真实保存、正式生效、重新OpenExisting快照，非法Scope拒绝；
3. 周期连续FC03不少于两次，PAD Provider写入MDS Good、Revision及真实值；首批只读零FC06；
4. Stop后标Stale且不再发报文；Host重启使用同一保存工程重新建立TCP连接和采样；
5. 另一个MHS服务实例改写正式配置后，旧Host周期回读前FailClosed，Completion Faulted、MDS变Stale；
6. 另有PLC ArchitectureSmoke验证MRK写权限/Unknown/OperationId、现增Effective版本失效禁止FC06。

专属CI：`.github/workflows/scada-runtime-host-g1-windows.yml` 进行真实Host项目Restore、Release/x64构建及 `CxVisual.Runtime.Host.exe scada-g1-smoke`。早期版本报过Host缺F17显式引用的CS0234，已修正。**固定成功证据**：[`41022902801`](https://github.com/oudream/cx-visual-x3/actions/runs/41022902801)（基于固定SHA `6139d9ddec470d74ed195ad3bb640ae91cff1229`）。此后的 `--service`、新版PLC版本测试及文档变更必须以对应新SHA CI单独验收，不因为先前成功而预填PASS。

## 4. HMI-B交付与待完成

- 已提交共享文件：`DOCS/Implementation/CxVisual_ICS_SCADA_共享接口与联调契约_V1.0.md`；内容包括真实 `DeviceRef/PointId`、MDS快照/回调、MRK命令Schema、固定Effective版本、Host启停、跨进程边界。
- A专有源码仅改 `Professional/Automation`、`Applications/Runtime.Host`、相关专业测试/文档/Workflow；没有修改B的HMI/Visual/Presentation/Studio。
- 第二段进入：正式MEM Device资源/Station拓扑与Config Owner关联；多设备/多工站共享MRK租约；跨进程授权数据订阅与命令入口；运行Journal、监督服务集成；现场PLC G2/HIL与安全G3。

## 5. A2/A5 后台采集故障监督与同版只读重连（2026-10-30增量）

此次不建立SCADA私有计时器/Transport/数据仓，也不自动重放MRK设备动作。 `PADPlcPollingSession` 原有普通顺序扫描仍允许偶发Partial；增加可选 `maxConsecutiveFailedCycles`（默认0保持旧API），SCADA正式会话设置3。连续三轮全部读取失败、F17 Channel不再Open，或连续FC03网络超时时，抛出明确的 `PADPlcAcquisitionUnavailableException`，由FCR Completion结束该采集世代。协议坏帧 `InvalidDataException`、点表/配置漂移、会话非法等仍保持 **FailClosed、不得自动连接新设备**。

`Applications/Runtime.Host/MRHScadaRecoverySupervisor.cs` 在 **明确指定 `scada-serve ... --service --recover`** 时才启用：每次旧Host完全Dispose后，用原MEM DeviceRef、MHS Connector Module、Scope重新Open；校验最初的工程文件时间和MHS Effective版本相同，建立新Channel Session Generation；恢复只允许 `PADPlcAcquisitionUnavailableException` 或Socket连接故障，最多3次、有界指数退避。超过预算或版本变化必须Faulted向上层传播，不能无声无限重连。默认 `scada-serve` 和 `--service` 不自动重连。所有自动恢复均为 **只读FC03**，没有设备控制回放。

`MRHScadaSmoke` 用原生TCP测试设备不返回真实FC03请求响应，验证旧世代超时、MDS质量不可信、新世代连接及后续成功采样，且全程无FC06；关闭Supervisor后不得遗留后台FC03。该夹具不是PLC硬件验证。

**验收证据**：代码已提交，恢复专项待 <https://github.com/oudream/cx-visual-x3/actions/runs/41028001190> Windows G1实际结束后确认；本段不能提前标记软件G1通过，真实设备G2/HIL G3与无界长稳都尚未执行。Windows Service SCM安装、服务级守护/进程崩溃拉起，应与现有 `Services/Supervision` Owner联调，禁止在Automation再建守护服务。
