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
