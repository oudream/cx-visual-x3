# CxVisual ICS/SCADA ↔ HMI-B 共享接口与联调契约 V1.0

> 维护 Owner：ICS/SCADA-A（Professional.Automation、Applications/Runtime.Host）；消费方：HMI-B。2026-10-30。目标：给 UI 开发直接可核实的真实类型与调用路径，而非新建总线/第二变量仓。
> 核对要求：每次联调先读取当前 `master` 及 CI；标记「已实现」「G1验证」「等待Host授权通道」。**软件G1不证明PLC/机构的物理动作和安全。**
> 不修改 `Professional/HMI`、`Mechanism/Visual`、`Mechanism/Presentation.Wpf`、`Applications/Studio` 源码。

## 1. 工程身份、设备与点位

- 工程的持久 `MEMNode.Id` (Guid) 是本阶段的 `DeviceRef`。由 `MEMEngineering.Open(path)` 和 `session.Find(nodeGuid)` 验证它存在、且实现 `IMEMResource`；`Page/Content/Symbol` 均可持有持久资源身份，当前 **尚未强制成独立Device类型/Station类型**，物理设备SN/型号关联未完成。
- PLC Connector 属于正式 `MFMModuleKind.Connector`，使用 `MCNConnectorConfiguration` (`[FCMConfigSection("CxVisual.Module.Connector")]`)。Host 调 `MHSConfigService.OpenExisting<MCNConnectorConfiguration>(new FCMConfigIdentity("CxVisual.Module.Connector","module",moduleId))`，从 `CaptureForTask(...)` 取得经校验的 **Effective** 和版本。
- 当前设备变量稳定点ID来自 `MPAPointDefinition.Name`（例如 `word100`），点表的地址/站号、读写权限仍在 MPA 配置中，不在 HMI 重复建表。
- `MPADataMapping` 定义 `plc/Station.Word`（ScopeId/变量名）。`PADScadaDeviceSession` 按持久生效配置生成 `PADDeviceVariableCatalog` 与 `PADPlcPointBinding`，不是硬编码寄存器。现阶段支持 Holding `UInt16/Int16`，每设备上限64点。
- 同一 Host 的 `DeviceRef/ScopeId/ModuleId/EffectiveVersion` 要由工程发布/装配者一起固定；缺少任一项拒绝启动。首阶段只读Host将MEM资源与MHS模块通过装载参数关联，**MEM中持久 Device→ConnectorRef 专用引用合同尚待补全**。

## 2. 运行数据：现有唯一 MDS

真实类型：`MDSVariableTable`、`MDSVariableRegistry`、`MDSVariableProviderHost`、`IMDSVariableSnapshot`，以及 `PADScadaDeviceSession`。

```csharp
// 同进程受权运行上下文的示例；HMI不得自行创建第二套MDS。
PADScadaDeviceSession device = hostOwnedDevice; // Host注入已启动实例
foreach (PADDeviceVariableDefinition p in device.PointDefinitions)
{
    IMDSVariableSnapshot sample = device.Inspect(p.StableId);
    // p.StableId, p.Address, p.ValueType, p.Unit, p.Writable
    // sample.CurrentObject, sample.HasValue, sample.Quality,
    // sample.RefreshUtc, sample.Revision, sample.Source
}
using (IDisposable subscription = device.Subscribe(
    changes => {
        // 既有MDS有界LatestOnly分发；按VariableId/Revision判新旧。
        // 回调不在WPF Dispatcher线程，HMI自行切换到UI线程刷新视图。
    },
    new[] { "word100", "input101" }))
{
    // 关闭视图时Dispose，取消订阅；勿持有另一份实时Current。
}
```

上述 `Subscribe` 实际复用 `MDSVariableTable.CreateDispatcher(1024)` 的有界 LatestOnly 机制；合并变更适合HMI实时状态，**不保证逐笔事件全部交付**，审计/报警历史必须由正式History Owner单独实现。

以上没有新增第二份 `Current`。断线/停止/版本漂移时，已采样值保留、`Quality=Stale`，只有真实FC03采样才可恢复Good。变量快照有MDS自身的Source/Revision/时间；Channel SessionId使用 `device.SessionGeneration`，不能将二者伪装成同一个Gen。

**跨进程现状**：现有 `Runtime.Host.exe scada-serve` 是独立进程，支持其本地控制台 `status`、`point <StableId>`、`stop`。**还没有为HMI正式公开且授权的数据远程订阅服务/API**；B不可从另一个进程直接持有 `DataTable`，不得将控制台文字输出当正式IPC协议。待A按现有安全Transport/订阅 Owner 提供认证、版本、Generation、背压与重放合同后，B接入远程绑定。

## 3. 授权命令与版本拒绝

复用现有 `MRKCommandCenter`、`PADPlcMrkCommandRegistration`、`MRKCommandRequest`，绝无第二命令中心。已装配的受信Host必须在创建Provider时提供真实的 `Func<string,PADDeviceVariableDefinition,bool>` 授权器、使用带 `authorize` 策略的MRK命令中心，并在Session启动**前**调用 `device.RegisterWriteCommands(hostCenter,commandId)`。未设置专业授权时，默认只读且此注册明确拒绝。

配置了SCADA固定版本的命令有额外必填 `EffectiveVersion:string`；原非SCADA历史注册保持原Schema。

```csharp
MRKCommandRequest request = new MRKCommandRequest(
    PADPlcMrkCommandRegistration.TargetForDevice(deviceRef),
    PADPlcMrkCommandRegistration.WriteUInt16Code,
    new Dictionary<string,object> {
        {"StableId",stableId},
        {"Value",(ushort)91},
        {"EffectiveVersion",approvedEffectiveVersion}
    },
    caller: trustedHostPrincipal, operationId: Guid.NewGuid(),
    expectedSchemaVersion: 1);
MRKCommandResponse response = await hostCommandCenter.InvokeAsync(request);
// 必须联合 Stage / State / BusinessSuccess / BusinessCode 判断，
// 不能把 State=Completed / FC06 ACK / FC03寄存器相等当成实体动作PhysicalDone。
```

- `MRKCommandStage`: `Rejected / Accepted / Unknown`；`MRKCommandState` 有 `Completed / Failed / Unknown` 等。写回读一致且业务通过才 `BusinessCode=PLC_REGISTER_READBACK_CONFIRMED`。
- 写已发送后回读结果不明：`BusinessSuccess=false`、`PLC_REGISTER_OUTCOME_UNKNOWN`，**不得自动重试非幂等写**。
- 版本不匹配：在实际发FC06前抛出 `scada.command.effective_version_mismatch`，MRK结算为失败而不是设备成功。需经Host重新装配且重新授权，不能仅靠HMI界面刷新解除。
- Host提供的 `caller` 应经外部身份认证；任何 UI 用户输入的字符串本身都不可信。MRK的ResourceLease只在当前运行宿主域内有效，不冒称跨进程硬件联锁。
- **A的首批 headless `scada-serve` 暂无外部认证服务，故不开放命令输入**。HMI-B不能直连PLC或绕过MRK向现场发FC06。

## 4. 会话启动/停止/重连与Host独立性

实际类型 `PADPlcPollingSession`（FCRPeriodicJob，不重入）、`PADScadaDeviceSession`、`MRHScadaRuntime`。受权宿主调用 `StartAsync(CancellationToken)`、`StopAsync()`、`Dispose()`，并读 `IsRunning/CompletedCycles/SuccessfulCycles/PartialCycles/Completion/SessionGeneration`。

独立运行（正式CLI，不依赖Studio或HMI窗口）：

```powershell
CxVisual.Runtime.Host.exe scada-serve <saved.cxproj> <mhsConfigRoot> <MEMDeviceGuid> <connectorModuleId> <scopeId> <intervalMs> [--contiguous] [--service] [--recover]
```

`MHS` 是持久配置/Effective真源；`MEM` 是工程身份真源。周期每轮前复核现存正式生效配置（跨Studio进程重开MHS），若版本变化或工程文件变化便FailClosed、MDS Stale、等待宿主明确重装。旧Session **不得自动重连并回放写命令**。新Session必须建立新Channel SessionId和新的MDS Provider Generation，重装期间HMI旧命令必须拒绝。

**运行方式提醒**：`--service` 为真正不依赖stdin的独立控制台长驻模式，Ctrl+C触发协作Stop；未指定`--service`时支持`status/point/stop`本地命令。这还不是已经安装到SCM的Windows Service，部署守护/受信IPC仍需后续实施。

**新有界恢复合同（A侧新增，B不需要改驱动代码）**：只有 `--service --recover` 会自动重建只读Session。原有 FCR 采集连续全失败3轮或Network超时后，A关闭旧Channel/Provider/MDS Scope，再验证原MEM工程与MHS Effective未漂移，建立不同 `SessionGeneration`；最多3次退避重连。B必须认为旧Generation上的订阅/操作上下文全部失效，重新授权并拿到新Snapshot。配置版本漂移/无效协议报文/写回读Unknown均不得作为自动重连或自动重发依据。该功能的G1仍以对应新CI证据为准；不是安全复位、不是SCM进程守护。

**新增正式多设备部署合同**：`Professional.Automation.Scada.PADScadaDeploymentConfiguration` 标注 `[FCMConfigSection("CxVisual.Automation.ScadaDeployment",SchemaVersion=1)]`。唯一配置身份为 `new FCMConfigIdentity("CxVisual.Automation.ScadaDeployment","project",MEMProject.Id.ToString("N"))`，使用现有 `MHSConfigService.ForProject(projectId).Open<PADScadaDeploymentConfiguration>()` 保存并生效；B可复用自己的通用配置编辑器，不需要复制数据表。每条 `PADScadaDeviceInstallation` 包含 `DeviceNodeGuid`、`ConnectorModuleId`、`ScopeId`、`IntervalMs`、`Contiguous`、`Enabled`，**不包含PLC地址/点值**。Host统一校验DeviceRef、Scope、Module及ChannelId互不重复，部署Effective在每轮采集前重新核验。

`Runtime.Host.exe scada-group-serve <saved.cxproj> <mhsRoot>` 是多设备只读独立进程入口：从MEM ProjectId读取真正已生效的SCADA部署，再按各Module读取MCN/MPA点表及各自MDS Scope。一个工站Stop/断线不会共享另一工站的Channel/Current。首批多设备Group默认单点设备不自动重连、单个不可恢复设备故障会使长驻Host退出要求外部监督；工业多设备运行恢复与工位控制仍待下一阶段。B不要在Studio内维护私有设备运行表。

## 5. B负责的UI接入及共同联调门禁

| 消费需求 | A交付的事实/接口 | B可立即做的工作 |
|---|---|---|
| 展示设备与点表 | `DeviceRef`、`PointDefinitions`、MDS Scope | 基于这些标识定义设备树/点表行，不写死寄存器 |
| 读数/质量/时间 | `Inspect`、`VariableChanged`（同进程） | WPF Dispatcher与订阅释放；跨进程先等A正式Transport |
| 发控制命令 | MRK target/code + Schema + 结果枚举 | 实现禁用态/Unknown态/版本门禁UI，不直连PLC |
| 启停/重连 | 独立Host会话方法、只读CLI | 设计控制命令工作流，等待A跨进程认证入口 |
| 宿主故障 | Completion/Faulted、SessionGeneration、MDS Stale | 明确离线、未知和最后值，不把Stale显示成Good |
| 版本隔离 | MEM NodeGuid、MHS EffectiveVersion、SessionGeneration | 控制操作提交时带版本，旧版本按钮应拒绝 |

**未交付且不得伪称已完成**：跨进程认证数据订阅与远程操作、生产Journal与SCADA状态绑定、SCADA正式设备模型拓扑/站点资产号、历史报警/报警确认、软件看门狗/进程恢复、真实硬件G2及安全HIL G3。

## 6. 验收基线与Owner

- `Tests/ArchitectureSmoke/Integration/AutomationPlcPointTests.cs`：历史真实TCP Modbus G1，FC03/FC06、PAD→MDS、MRK与Vision编排。
- `Applications/Runtime.Host/MRHScadaSmoke.cs` + `.github/workflows/scada-runtime-host-g1-windows.yml`：MEM保存重开、MHS Effective、Headless Socket采样、Stop/Restart/配置漂移G1；**以当次固定SHA Actions成功日志为准**。
- Studio/HMI的真实UIA由B提交，与A的Host合同分界；本文件只由A维护。并行修改以Owner为准。


## 7. A3 同账户Windows Pipe本机只读观察协议（2026-10-30）

正式入口：CxVisual.Runtime.Host.exe scada-group-serve <project.cxproj> <mhsRoot> --read-pipe <pipeName>。只在已生效的MHS项目部署装配成功后监听，不指定时没有IPC。实现位于 Applications/Runtime.Host/MRHScadaReadOnlyPipe.cs；仅允许启动Host的同一Windows账户访问。绝不开放未授权跨账户/公网写入，也不取代正式跨进程鉴权平台。

线协议v1：每连接一次请求、一次回复；little-endian Int32长度前缀 + UTF-8 JSON。请求不超过4096字节，响应不超过1048576字节，读写超时3秒。仅有：
- discover: 请求 {"SchemaVersion":1,"Operation":"discover"}，返回Status=Ok、DeploymentVersion、Devices[]的DeviceRef/ScopeId/ConfigVersion/Generation/Running；Points为空。
- snapshot: 请求 {"SchemaVersion":1,"Operation":"snapshot","DeviceRef":"<真实MEM Guid>","ExpectedDeploymentVersion":"<项目Effective>","ExpectedConfigVersion":"<Connector Effective>","ExpectedGeneration":"<本次Session Generation>"}。返回Devices[0].Points，每项包含StablePointId、VariableId、Type、Unit、HasValue、Value（InvariantCulture字符串）、Quality、RefreshUtc/ChangeUtc（有真实样本时为UTC ISO 8601）、SourceId、Revision。数值只读取唯一MDS Inspect；非跨点原子事务，不产生第二份Current。
- 版本缺失或漂移、设备不运行或未批准时返回Status=Rejected及ErrorCode；可见DEPLOYMENT_VERSION_MISMATCH、SESSION_VERSION_MISMATCH、SESSION_NOT_APPROVED等。全部其他操作包括write均为READ_ONLY_OPERATION，不可借该Pipe提交控制、重放设备写入或回避MRK授权。

HMI-B可按以上合同用Windows NamedPipeClientStream连接本机同账户Host并做受控只读Snapshot；运行Generation或Effective改变后必须重新discover，不复用旧控制上下文。该功能不提供可靠持续订阅、跨账户身份/角色、命令、告警历史或远程控制，后续需由现有公共Owner实施。Windows G1使用双PLC真实本机TCP FC03、MDS Good/Revision，再经真实Pipe验证设备发现、Snapshot、旧Generation拒绝与零FC06；CI及硬件级别必须按实际固定SHA分别登记。

**Owner后续收敛点**：F17 已存在 NamedPipe Channel，但当前 F17 Server 配置尚无可显式审查的同账户 ACL 注入合同；本版只读Pipe属于 Host 的受限本机观察控制面，不用于现场 PLC/设备连接，也不注册通用 Channel。正式跨账户/远程和流式订阅接入前，须由 F17/安全 Owner 审查是否将 ACL、身份/流控及消息封装归一到现有 Transport，禁止在 SCADA 扩展第二套通用通信框架。


## 8. HMI-B实际客户端联调差异（2026-10-30，非越权改代码）

只读核查聊天B新提交的 Professional/HMI/PHMScadaReadOnlyPipeClient.cs 后，A侧将两个必须遵守的v1协议条件记录为正式联调阻断，而不是修改B的源码：
1. .NET Framework 4.8 的 PipeStream 不能依赖继承的 Stream.ReadTimeout / Stream.WriteTimeout 属性（默认 CanTimeout=false）。B的 Exchange 内目前设置了这两个属性；应由B Owner用3秒连接/单次操作截止、CancellationToken注册关闭NamedPipeClientStream句柄的方式处理，不在同步读写上直接设置Timeout属性。A的Host服务端已使用 CancelAfter + Dispose 关闭阻塞读取，避免坏客户端占用监听。
2. MDS首次尚未获得PLC真实FC03采样时 HasValue=false；A的v1 Point.Value、Point.RefreshUtc、Point.ChangeUtc可为null，Quality初始可能Uncertain；B的 PHMScadaPointObservation 目前无条件 DateTime.TryParse(RefreshUtc)，应先检查 HasValue，仅在存在真实采样时解析非空UTC时间。不能把DateTime.MinValue或当前系统时间冒充设备采样时间。

已有真实采样时A提供ISO8601 RefreshUtc/ChangeUtc；MDS质量降级为Stale仍保留上一笔真实采样时间。以上是 **A与B接口语义对齐要求**，不表示B代码已修复或完成联合Windows CI。其他共享点：SchemaVersion=1、同Windows用户ACL、三重版本锁、无远程写命令均不变。


## 9. 多设备组恢复对HMI-B的会话语义（2026-10-30）

A新增可选 `scada-group-serve ... --recover [--read-pipe <pipeName>]`；当前失败后**整个组**关闭并重新从同版MEM+MHS Effective装配，所有Device Generation同时换代，Pipe可能在退避期间断开。B应在Pipe不可达/旧Generation拒绝时清除旧Good显示，待新Discover成功后主动重新建立绑定；不得凭相同DeviceRef/Scope假设旧Session仍可控制或继续显示实时值。非通信错误、部署版本变更不得自动恢复。此版本仍只读，没有HMI远程MRK写入口。


## 10. 单PLC隔离恢复联调合同（2026-10-30）

Host新增可选 `scada-group-serve <project.cxproj> <mhsRoot> --recover-device [--read-pipe <pipeName>]`：仅通信异常影响的DeviceRef被摘除和重装，其余DeviceRef/Scope/Generation和MDS采集继续。**同一Pipe不退出**：故障PLC暂时不在Discover.Devices中，新世代恢复成功后以同DeviceRef/Scope、新Generation重新出现。原有客户端旧Generation请求要么返回DEVICE_NOT_FOUND（恢复空档），要么SESSION_VERSION_MISMATCH（恢复完成），均必须清除上一次Good并重新Discover后绑定；其余健康设备订阅可继续使用原会话版本。A在设备替换期间锁住读端快照与旧设备Dispose，保证快照不会引用已释放的MDS。

本版只允许本机同账户只读Snapshot；尚无跨账户授权、可靠推送、SCADA远程可信控制或工业硬件身份判定。HMI-B保持公共Owner及原Pipe v1协议，不应将PLC reconnect认为现场执行器状态已确认。A的双PLC真TCP回归新增A断包重连而B不停采的反证，CI状态以提交SHA对应实际Windows运行日志为准。


## 11. A2只读采集诊断计数（2026-10-30）

A侧Runtime.Host的v1 Pipe discover/snapshot中，Devices[]增加仅附加的long字段：CompletedCycles、SuccessfulCycles、PartialCycles。它们直接透传同一个设备现有FCR/PADPlcPollingSession统计，只表示**当前SessionGeneration**已完成的FC03轮次、全成功轮次及部分失败轮次，不是设备物理执行动作或可持久趋势。断线替换后新Generation计数重新开始；HMI不得跨代次累计后认为是同一次稳定会话。旧B客户端无需以这些字段作为必填项；DataContractJsonSerializer对额外字段可忽略，SchemaVersion仍为1，旧字段、ACL、版本拒绝及只读操作保持不变。新的G1会验证两台PLC设备的独立SuccessCount>=2、PartialCycles=0与无写报文。


## 12. A5 Runtime.Host工程预检及A/B部署接入合同（2026-10-30）

- A正式CLI：`CxVisual.Runtime.Host.exe scada-project-check <saved.cxproj> <mhsRoot>`；成功Stdout单行UTF-8 JSON，SchemaVersion=1、Status=EngineeringValidated、DeploymentVersion及Devices[]；调用进程必须同时核验退出码为0。无数据表明设备已连通：`DeviceCommunicationValidated=false`。
- Devices的DeviceRef固定于MEM工程、ScopeId对应MDS，ConnectorModuleId/ConnectorEffectiveVersion来自MHS，ChannelId来自F17正式配置；ScanIntervalMs和Contiguous来自保存的项目部署Effective。Points[]列出StablePointId、VariableAddress、ValueType、Unit、WritableDefinition；不得据WritableDefinition让HMI直接对PLC写寄存器。
- HMI-B需要在Studio保存并生效MHS后调用此CLI预检时，应显示它仅是**工程配置有效/设备未验证**。实时是否连通和数据质量，仍通过已存在的Runtime.Host Pipe的Discover/Snapshot（含SessionGeneration、MDS Quality及FCR循环计数）读取，而不是反向从工程预检推断“设备在线”。
- 失败时CLI返回非零及stderr错误说明，不输出成功JSON。Go Supervision可由其自己的进程管理Owner选择接入，此文仅交付合同，不代表已修改Daemon/Monitor或完成受信服务装配。


## 13. Host只读Pipe独立进程探针及A/B联合验收边界（2026-10-30）
- A提供只读CLI：`CxVisual.Runtime.Host.exe scada-pipe-probe <pipeName> discover`和`CxVisual.Runtime.Host.exe scada-pipe-probe <pipeName> snapshot <DeviceRef> <DeploymentEffective> <ConnectorEffective> <Generation>`。调用时必须是当前Host允许的同Windows用户，不依赖Studio，仍使用既有v1 Pipe/授权ACL，返回JSON和退出码（Ok=0、Rejected=3），不提供控制或FC06。
- 实际G1在运行PLC双工站的Host测试进程之外另外启动一个**真实Windows子进程**，通过Pipe验证MDS Good/Revision、真实原值、Generation FailClosed；不共享进程内的MDS/Connector实例。此Probe是A的诊断工具，不替代B专业的`PHMScadaReadOnlyPipeClient`。B仍由自己的Owner调用现有v1 discover/snapshot，并保持原先的登录、版本及显示语义。
- 本测试证明Host IPC服务与独立本机进程互通，尚未证明真实WPF应用直接连接实际PLC Host、远程跨账户用户授权或可信MRK控制；需后续双Owner联合Windows端到端门禁。


## 14. A4已存在MRK命令与版本双栅栏（2026-10-30）

**真实代码**：`Professional/Automation/Plc/PADPlcMrkCommandRegistration.cs`（强类型MRK注册）和`PADPlcMrkWriteCapabilityProvider.cs`（MOC/Decision Provider）。两种版本不能混淆：
1. `capabilityVersion`：Decision Plan绑定和可信发布的专业能力版本，经`IMRKVersionedCapabilityResolver.TryGetCapabilityVersion`验证。
2. `commandEffectiveVersion`：该PLC Connector从正式MHS捕获的`EffectiveVersion`，必须作为**独立参数**注入Provider构造器，再由Provider写入`MRKCommandRequest.Inputs["EffectiveVersion"]`，由`RegisterUInt16Write(... approvedVersion, effectiveStillApproved)`在实际FC06以前校验。

正式构造签名：`new PADPlcMrkWriteCapabilityProvider(center, deviceRef, trustedCaller, capabilityVersion, fallback: null, commandEffectiveVersion: approvedConnectorEffectiveVersion)`。旧的四/五参数构造保持兼容未固定版本的历史Command注册；**不能将此兼容入口当成生产受控命令装配**。受信Host仍须自行提供已认证的Caller与MRKCommandCenter授权回调、设备租约、实际模式/安全链和专业点级授权，不允许HMI以Caller字符串冒充登录认证。

`MRHScadaRuntime.Open(..., additionalApproval: projectDeploymentStillApproved, authorizeWrite: trustedProfessionalAuthorization)`是Host内部唯一显式写授权装配入口：未提供项目级Deployment审批回调时拒绝创建可写会话；`host.RegisterTrustedWriteCommands(center, commandId)`必须在`StartAsync`前调用，默认`scada-serve/scada-group-serve`仍注入`authorizeWrite:null`且没有任何写Pipe/CLI。被注册的Handler在发FC06前再次核验采集Session活跃与MHS配置未漂移；Stop/重连后的旧Session不可继续写。

**正式输入**：`DeviceRef`路由到`Automation/PLC/{DeviceRef}`、CommandCode=`WriteUInt16`；输入`StableId:string`、`Value:ushort`、`EffectiveVersion:string`；输出`RegisterReadbackMatched:bool`。调用由真正MRK`OperationId`唯一约束、资源Lease和`MaxAttempts=1/Idempotent=false`管理。写入FC06及FC03读回一致的`BusinessCode=PLC_REGISTER_READBACK_CONFIRMED`只代表**寄存器回读**；`PLC_REGISTER_OUTCOME_UNKNOWN`或`MRKCommandState.Unknown`必须显式显示未知并人工/可信状态复核，不能自动重发。MRK已存在`MRKCommandPipeServer/Client`与OS Peer身份读取，但SCADA正式运行CLI**尚未基于生产认证/模式/安全策略开放远程写口**；不得误用本项目只读Pipe承接命令。

A增加真实OS Modbus TCP的`--automation-plc` G1：Decision/MRK正式Plan发版本锁定命令得到FC06+FC03，旧版/缺版/运行配置漂移不发FC06。源码SHA `0ead2604`、`7ee0bcdf`、`58662b7e`、`bcd338d3`、`abbdc7a7`；**是否G1通过取决于对应Windows运行结果，不因提交源码直接判定PASS**。HMI-B可据此完成有授权接入的操作按钮和Unknown UI，但仍不得自行创建命令授权、重放超时写入。


## 15. A4正式SCADA会话代次必填与HMI控制端安全栅栏（2026-10-30）

对真正`PADScadaDeviceSession.RegisterWriteCommands(hostCenter,commandId)`生成的MRK目标，输入现在**同时必填**：`StableId:string`、`Value:ushort`、`EffectiveVersion:string`、`SessionGeneration:string`。Provider在注册时提供实际`Func<string> currentSessionGeneration`，命令执行前校验客户端Generation与当前F17 Channel SessionId一致，并检查Session仍处于运行状态、Connector/Project Effective未漂移。旧历史通用`RegisterUInt16Write`若没有显式传该Generation检查委托，则维持原Schema兼容；**不允许把历史注册当正式SCADA Host控制方案**。

HMI-B必须通过只读Pipe`discover`先取得`DeviceRef/ConfigVersion/Generation`，且来自当前批准的项目`DeploymentVersion`；HMI发送命令时应同时携带对应`SessionGeneration`及`EffectiveVersion`。旧版、旧代次、字段缺失、权限/模式/专业点授权失败必须直接拒绝并显示，不得由UI补猜、重试或直接访问F17/PLC。Runtime.Host当前普通CLI仍只读，A仅提供**受信宿主内部**正式`MRHScadaRuntime.Open(...,additionalApproval,authorizeWrite)` + `RegisterTrustedWriteCommands`组合；并未向HMI启用一个未经认证的写Pipe。跨进程控制继续复用公共MRKCommandPipeServer/Client，须由Host的真实操作员授权、模式、安全联锁、审计与现场部署审核满足后才实际开放。

在同一OS TCP Modbus G1中检查MRK通过的真实FC06+FC03、错误Effective、缺失Effective、错误/过期Generation、MOC Plan旧会话、Stop后旧Host以及ACK后读回不一致的`PLC_REGISTER_OUTCOME_UNKNOWN`。本轮相关源码`dd086170`、`7767f8f0`、`02b0cdca`、`08e29cbc`、`9c9fe459`；是否通过以相应固定SHA的Windows结果为准，不把未验证代码说成G1通过。寄存器读回一致不代表实际机械动作完成。


## 16. A4受信MRK Windows Pipe宿主组合器（内部显式启用）（2026-10-30）

`Applications/Runtime.Host/MRHScadaTrustedCommandPipe.cs` 将**既有** `MRKCommandCenter`、`MRKCommandPipeServer`和`PADScadaDeviceSession.RegisterWriteCommands`组合为一个必须显式启动的Host控制端点。构造时必须传入真实已批准且带`authorizeWrite`的`MRHScadaRuntime`、受控PipeName、CommandId、`Func<string,bool> authorizePeer`和`Func<MRKCommandContext,bool> authorizeCommand`；任何授权委托缺失即拒绝。先注册命令、后启动采集会话，只有`device.IsRunning && device.IsStillApproved`才允许`Start()`受信Pipe。

MRK服务端以Windows命名管道ACL限制为宿主Windows账户，并由OS `GetImpersonationUserName()`取得真实调用方标识，再执行Host提供的Peer认证与MRK命令授权；**不使用客户端传入的`Caller`字符串作为身份**。专业PAD Provider仍有独立点级写授权、Connector EffectiveVersion及SessionGeneration门禁；MRK继续提供统一ResourceLease、OperationId去重及`QueryAsync`。控制前置条件还需要真正Host提供的操作员角色、控制模式、设备Ready/物理联锁政策；不可把Windows同账户当成生产权限许可。

**供HMI-B消费的公共API**：使用既有`MRKCommandPipeClient(".", approvedPipeName)`，执行`InvokeAsync(new MRKCommandRequest(PADPlcMrkCommandRegistration.TargetForDevice(deviceRef), PADPlcMrkCommandRegistration.WriteUInt16Code, new Dictionary<string,object>{{"StableId",stableId},{"Value",(ushort)value},{"EffectiveVersion",configEffective},{"SessionGeneration",sessionGeneration}}, operationId:operationId, options:new MRKCommandOptions(remote:true), expectedSchemaVersion:1))`。对端的真实Caller从Windows peer获取；`QueryAsync(operationId)`只查询同一身份的受控操作，**Unknown时仅查询、不可自动重发**。当Session/Effective改变，旧命令被拒绝；场景释放时必须`StopAsync`并注销命令。

受信Host还必须显式注入第三个动态委托`Func<bool> currentControlGuard`（生产模式/设备Ready/物理联锁的实时审批来源）；它不只是MRK入口权限，必须在设备Handler即将发FC06前再次检查。G1另加入“MRK已受理后控制模式立即撤销”的真实OS TCP反例，要求严格零FC06。

首轮实际Windows Socket G1在`MRHScadaSmoke.RunAuthorizedDevice`中新增独立的OS NamedPipe收发、FC06+FC03、Query、错误Generation、命令模式撤权、Peer撤权及Stop拒写；关联实现SHA `a0aa314a`、`c5687ad5`、`7aeb1253`。**这些增量尚须对应SHA的Windows CI成功才算G1通过。** 本Host内部装配**没有默认生产CLI参数或配置自动启用控制**，原`scada-serve/scada-group-serve`继续只读，也未完成真正HMI窗口跨进程联合执行、Windows SCM、实机安全/HIL。
