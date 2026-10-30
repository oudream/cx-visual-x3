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

## 6. A1/A2 双设备独立Scope与原生TCP多工站装配（2026-10-30增量）

新增 `Applications/Runtime.Host/MRHScadaRuntimeGroup.cs`，该Host装配对象不持有第二数据仓/协议或TaskEngine，仅复用 `MRHScadaRuntime.Open` 对每一条 `MRHScadaDeviceLaunch` 的MEM NodeGuid/MHS Connector Module/Scope/周期执行正式装配。最多32个实例；重复DeviceRef、MDS Scope、ModuleId或已经装配的F17 ChannelId必须FailClosed，未通过组预检前不打开任何设备网络。启动时若任一设备失败，调用现有 Stop/Dispose 对已启动实例回收；停止时尝试全部设备，不能因一台PLC故障遗留另一台后台采集。

`MRHScadaSmoke.RunTwoDevices` 从一个真实保存的MEM工程加载两个持久工站资源，在MHS持久存两份Connector/MPA点表，连两个独立OS TCP端点分别读取值73和45。两个MDS Scope拥有各自Revision、Quality与Generation；停止PLC-A后Quality变Stale，PLC-B继续取得Good，完成组Stop以后两个端点不再收到采样报文；没有任何FC06。负例包括重复资源、Scope、Module、ChannelId。测试实际通过与否以新SHA的Windows工作流为准：<https://github.com/oudream/cx-visual-x3/actions/runs/41028585366>。

**当前交付边界**：`MRHScadaRuntimeGroup` 是Host内的可编译装配与生命周期API，而不是Studio生成的正式部署编目/SCM服务；设备→ConnectorRef与采集组在MEM工程内的持久发布绑定尚待Owner进一步收敛。两设备各自拥有MDS Provider/Registry（按Scope隔离），不满足同一进程跨设备共享值一致性/同步快照或跨进程控制事务。不能以本机TCP夹具冒称现场PLC G2/G3。

## 7. A1 工程Project级SCADA部署正式FCM段与多设备独立Host（2026-10-30增量）

- **配置真源**：`Professional/Automation/Scada/PADScadaDeploymentConfiguration.cs`，SectionId `CxVisual.Automation.ScadaDeployment`、ScopeKind `project`、ScopeId `MEMProject.Id.ToString("N")`；其中仅保存每个设备的 `DeviceNodeGuid / ConnectorModuleId / ScopeId / IntervalMs / Contiguous / Enabled`。通过MHS的 `UpdateAsync`、`ApplyPendingAtTaskBoundary`、`CaptureForTask` 生成、保存、生效和冻结，不创建旁路JSON/站点树。MPA仍唯一保存FC03地址/单位/站号等，MDS仍唯一保存Current。
- **运行装载**：`MRHScadaRuntimeGroup.OpenFromEngineering(projectPath,configRoot)` 从磁盘MEM工程取得ProjectId，再打开MHS已保存Effective SCADA部署，枚举已启用设备并按各自MHS Connector Effective调用正式 `MRHScadaRuntime.Open`。项目部署及每个Connector生效版本都在每次实际采集前重新核验；任一漂移拒绝继续采样并降级Stale。检验设备GUID、MDS Scope、ModuleId、F17 ChannelId在一组内唯一。
- **正式CLI**：`CxVisual.Runtime.Host.exe scada-group-serve <saved.cxproj> <mhsRoot>`，无需Studio/HMI进程或手工传每个PLC寄存器地址。多设备共用同一独立Host进程，各自持有不同的MCN Channel、PAD桥接和MDS Scope，组Stop遍历全部设备。不另建Timer/数据缓存/命令总线；首批组进程默认只读，某个采集出现不可恢复错误时FailClosed、外部Supervision后续联调。
- **真实测试**：`MRHScadaSmoke.RunTwoDevices` 保存MEM双Station资源及项目级MHS SCADA Deployment，两套Connector Effective向独立本机TCP夹具发送真实FC03，分别读73与45；A单独Stop后B继续读Good。拒绝重复DeviceRef、Scope、Module和ChannelId，验证修改项目级部署版本时两个旧采样会话进入Faulted并将原Quality置Stale，整个测试不发送FC06。
- **交付状态**：上述源码与测试已提交到master；新运行批次 <https://github.com/oudream/cx-visual-x3/actions/runs/41029431596> 的实际Windows成功是最终G1验收门禁，不可把较早单设备成功冒充新双设备/FCM Schema通过。现场PLC身份、生产部署SCM、跨进程认证IPC、分布式租约和真实机台安全仍未验收。


## 8. A2/A5 项目级多PLC显式有界只读恢复（2026-10-30增量）

源文件 `Applications/Runtime.Host/MRHScadaGroupRecoverySupervisor.cs`；新入口 `scada-group-serve <saved.cxproj> <mhsRoot> --recover [--read-pipe <pipeName>]`，原有非恢复入口完全保留。仅在一个PLC周期采集真实通信不可用或Socket连接失败时，先Stop/Dispose当前整组MDS/Connector/Provider/Channel，再重新以MEM工程和MHS正式Effective打开新组；检查项目保存时间、项目部署Effective、各设备Connector Effective与DeviceRef集合均未变，并要求每台设备SessionGeneration必须换代。允许最多3次有界重试及指数退避；业务/点表/配置/版本漂移、不可确认的写入和其它异常均不会自动恢复。**当前是组级重建，非单PLC无中断热切换；其余PLC在恢复期间暂时中断。** Pipe若显式启用，则跟随组世代停启；旧HMI Generation不能用于新会话，必须重新Discover。没有建立第二套定时器、协议、可写变量仓或独立运行平台。

`MRHScadaSmoke.RunTwoDevices` 增加OS原生TCP丢失一笔FC03响应、两PLC新SessionGeneration、旧HMI请求拒绝、恢复后真实FC03采样、零FC06与Stop后无残留报文反例。测试源码已提交时应标记为待Windows G1结果，不能把本地模拟器等同现场G2/G3；仍缺单设备无中断切换、生产SCM、跨账户安全IPC、告警历史及设备HIL。
