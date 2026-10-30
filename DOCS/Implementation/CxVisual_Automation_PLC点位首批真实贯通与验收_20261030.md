# CxVisual Automation——PLC/IO真实链路 WP01～WP02 首批实施与验收记录（2026-10-30）

> 状态：本次**正式源码/自动测试编写完成，Windows CI和实机验收以实际运行结果为准**。不是 PLC 产品功能完整、不是硬件HIL、不是已实现全部18专业领域。

## 一、实施目的与Owner

依据 Automation #00～#10、DOCS/01 统一架构，实装一条真实软件协议事务链：

`PAD稳定变量ID → MCNConnectorModule → MPAModbusTcpProtocol → FTRVirtualChannel/真实TCP → 回应与数据映射 → MDS唯一变量表`。

控制操作：`PADDeviceVariableProvider.WriteAsync(含宿主授权) → 临时请求值 → MPA 06协议确认 → MPA 03现场寄存器回读 → MDS提交事实`。协议已应答但回读超时/值不一致必须报告 `PADPlcOutcomeUnknownException`，不得无理由自动重发或宣称物理动作完成。

Owner保留：F17拥有Channel，MCN/MPA拥有Connector/Protocol/点地址，MDS拥有Current，PAD拥有设备变量定义与专业操作桥接。**不创建新项目、新协议栈、新CommandCenter、新变量仓。**

## 二、本轮正式文件

| 文件 | 真正新增/修改内容 |
|---|---|
| `Professional/Automation/Plc/PADPlcPointBridge.cs` | `PADPlcPointBinding`、`PADPlcPointBridge`、`PADPlcOutcomeUnknownException`；现有PAD写授权+MPA真实点表/读回 |
| `Professional/Automation/CxVisual.Professional.Automation.csproj` | 编译PLC桥接；增加对既有 Mechanism.Connectivity 与 Foundation.Transport 的单向直接项目引用 |
| `Tests/ArchitectureSmoke/Integration/AutomationPlcPointTests.cs` | 真实虚拟F17 Channel和Modbus MBAP/03/06的正反协议测试、MDS映射、未授权、回读不符、超时Unknown、恢复 |
| `Tests/ArchitectureSmoke/Program.cs` | `--automation-plc` 专项入口及默认回归入口 |
| `.github/workflows/automation-plc-g1-windows.yml` | Windows 2022/.NET48 Release x64 编译+专项G1，同 SHA 保留真实构建/测试证据 |

## 三、能力边界与工业安全

本轮仅实现 Modbus TCP Holding 单寄存器的 **Int16/UInt16** 读/写/回读逻辑；点表必须存在唯一的必需入站 MDS Variable Mapping，写点必须存在唯一且同变量的出站 Mapping，且地址/类型/读写权限匹配。首次读取与回读经 `MDSProtocolDataTargetAccess` 更新原有MDS，不在Automation复制 `Current`，临时写值不预先写入MDS。

写入权限仍必须由可信宿主的 `PADDeviceVariableProvider` 授权回调决定。现场部署时其上游须接入**既有 M12/MRK 身份、角色、设备互锁、独占资源租约**；本轮桥接本身**不具备认证安全控制和完整的跨宿主资源仲裁**。桥接仅验证 Holding寄存器的回读一致，不表示电机已到位、气缸已夹紧、X-Ray已停止出束。非幂等/危险控制不得直接以本次写函数取代现场安全控制与专业命令。

连接断开、协议超时、回读矛盾和设备代次变化均不得返回“物理完成”。自动G1采用 **VirtualChannel中的真实 Modbus FC03/FC06 报文及协议解析**，不将其称为实机G2/HIL。TestCenter工业设备面板和真实 PLC、Motion、X-Ray G2/G3 均仍待实施。

## 四、验收条目与证据等级

| 用例 | 正反场景 | 状态/验收 |
|---|---|---|
| AUT-PI-001 | 按 MPA 点地址读取真实 03 响应并映射 MDS | G1待 Windows CI |
| AUT-PI-002 | 授权操作后 06 写入+03 回读一致才成功 | G1待 Windows CI |
| AUT-PI-003 | 错误身份/错误类型不允许发送写命令 | G1待 Windows CI |
| AUT-PI-004 | FC06已应答但设备未修改：结果 Unknown、不自动重试 | G1待 Windows CI |
| AUT-PI-005 | 已写入但回读超时：Unknown、不能虚构最新MDS值 | G1待 Windows CI |
| AUT-PI-006 | 恢复后重新读取现场事实；停止后禁止再读 | G1待 Windows CI |
| AUT-PI-007 | 真实PLC型号/固件/网络/线缆/故障与读回 | G2未执行 |
| AUT-PI-008 | 实机安全链、工位互锁、负载与长稳 | G3未执行 |

执行：

```powershell
msbuild.exe Tests/ArchitectureSmoke/CxVisual.Tests.ArchitectureSmoke.csproj /t:Build /p:Configuration=Release /p:Platform=x64
Output/Release/x64/CxVisual.Tests.ArchitectureSmoke.exe --automation-plc
```

以上是后续现场/CI的测试命令，不表示在当前环境已经执行。

## 五、下一批任务

1. 若 Windows CI 报错，先修复当前小包，不放大为全专业空接口。
2. 承接当前 M12/MRK 的实装授权、资源租约和真实 Operation 结果，使非幂等命令跨进程/多设备都受正式控制。
3. 读点采样策略、断线 Bad/Stale 与现场PLC诊断/重连、点表导入/版本保存重开。
4. TestCenter/Studio 的真实 PLC/IO 专业操作页，先复用通用设备/变量视图和模块中心。
5. 再推进真实 Motion、气动/真空、搬运与 X-Ray/Detector 的多设备专业动作，G2/G3依设备而定。

**注意：文档 #09/#10 的206项类型是架构词汇，本包只新增必要的PLC桥接对象，没有批量生成候选类。**

## 六、2026-10-30 Windows CI 复盘与修复

首批提交 `fa907c14017b4281ff854d8b3ff2a4416f2c8953` 在 Windows 已成功编译 Automation、ArchitectureSmoke 与全解决方案，但三个依赖默认 ArchitectureSmoke 的 CI 因相同原因失败：`PROTOCOL_IO_MISMATCH：Modbus TCP 只能使用 TCP Channel`。问题出在 G1 把虚拟字节通道直接装配到 Modbus TCP Connector；这是测试装配错误，**不能削弱 MCN 的 TCP/Modbus 兼容性检验**。

修订采用本机 `TcpListener` 的确定性单寄存器 PLC 测试端与正式 `FTRTcpClientChannel`，通过真实 OS socket 收发 MBAP/FC03/FC06 帧，保留 MPA Protocol、MCN Connector 和 MDS 的正式调用。测试覆盖虚拟通道不被非法装配、03读取、06回显、现场回读矛盾、读回超时 Unknown 与恢复；无任何实机 PLC/G2/HIL 证明。

本节记录实际已观察的首批失败原因，修复后的 Windows CI 结论须读取修复提交 SHA 对应 Actions 才能填写，不预填 PASS。

## 七、运行态点表映射防串写（2026-10-30）

- 已在 `PADPlcPointBridge` 的初始绑定检查中要求：每个只读点恰有一条唯一的必需MDS入站映射；每个可写点恰有一条额外的授权出站映射，目标与类型一致，不接受隐藏的Message/Command/其他变量目标。
- 在构造时记录已批准的协议地址、站号、类型、读写属性与完整映射快照；每次读写开始前检查当前配置是否漂移。运行中改变寄存器地址、增加跨站变量或命令映射会拒绝执行，避免继续按未经批准的配置发送请求。
- 新增正反测试：恶意额外MDS映射、隐藏命令映射、运行期地址修改、运行期映射修改。测试使用真实本机TCP回环，不绕开MCN的协议/Channel类型约束。
- **保留风险**：这一层是“操作开始前的配置一致性”守卫，不是对运行期可变配置的原子版本冻结；Connector与MPA真实请求过程中的并发配置变更，仍需在公共配置/运行Owner实施不可变Session快照和安全切换，不应由Automation另建一套配置系统。跨实例设备资源仲裁仍须正式M12/MRK租约。

本批新的Windows G1以及完整回归结果，必须关联修订提交SHA审查；未见结论前不得改为PASS。

## 八、WP02：运行态可信读回与质量恢复（持续开发）

本批继续修改真实代码，区分 **协议响应到达** 与 **允许将值发布到 MDS**：

1. `PADPlcPointBridge.ReadConfirmedAsync` 先将 FC03 入站映射写到单次请求局部的 `StagedReadAccess`，核验连接 `SessionId`、点表签名、映射结果和值类型后，才通过现有 `IMPADataTargetAccess.WriteAsync` 提交 MDS。不新建持久Current仓。
2. 在测试 TCP 服务器中暂停已收到的 FC03 请求，并在设备回包前修改工程点表地址，检查失败请求没有改变 MDS 值与 Revision；随后恢复原地址并重新采样。该测试是实际 TCP 往返，不模拟 MPA/MCN 的成功结果。
3. MDS 增加 Provider Host 专用的 QualityOnly 样本；仅 `Bad/Uncertain/Stale` 可无值更新质量，绝不允许凭无值质量事件恢复 `Good`。已有采样值/RefreshUtc/ChangeUtc 必须保留；没有初始采样时不制造值。
4. `PADDeviceVariableProvider.EmitQuality` 使用 Host 原有 ProviderId/PointId/Scope/Generation 绑定。PLC读回超时/写请求结果不明时标记已知样本为 Stale；只有新的真实读回才能恢复 Good。
5. `PADPlcPointBridge` 订阅 F17 `StateChanged`：关闭、Faulted、Opening、Closing时把已采样 PLC 点标为 Stale，`Dispose` 解除订阅；不新建状态轮询线程和第二份设备健康仓。

**工业边界**：Stale 是“该源的最后数据不再可用”的质量告警，不是机器设备已经安全停止的证据；PLC写FC06＋读FC03仍只是寄存器回读，不是Motion/Fluid/XRay等动作PhysicalDone。配置校验不是跨线程原子冻结，完整执行资源互锁仍由 M12/MRK 正式机制承接。

### 回归证据与状态

- `bcf6f3342d13639ada81412e42404af197085dd5`：临时读值暂存/双重会话校验的 Windows G1 **通过**。
- `60da269d5cfa26eb748442ae0b16912350806bea`：在途 FC03 回包点表漂移的 Windows G1 及 Config L1 **通过**。
- 后续质量Only、断线Stale以及最新测试的 G1：**按对应 GitHub Actions SHA 确认，尚不可预填 PASS**。
- 真实 PLC 设备固件、异常断网和工位安全系统的 G2/G3：**未执行**。

下一步优先：①M12/MRK真实资源租约与操作身份上下文；②PLC多测点/批量扫描与Bad/Stale分级；③Studio/TestCenter真实PLC点表和调试面板；④真正现场设备G2/HIL。

## 九、PLC只读输入与Provider唯一数据源（2026-10-30）

在 G1 源码审核中发现：`MDSProtocolDataTargetAccess.WriteAsync` 的普通 `UpdateBoxed` 遵从工程变量 `Writable`，所以只读DI点的真实采样不能直接走普通写变量API。现已修复专业桥接：经过 Modbus FC03、会话与点表复核后，使用已注册 `PADDeviceVariableProvider.Emit` → `MDSVariableProviderHost` 接纳物理采样，允许 `Writable=false` 的变量更新但禁止无授权控制写入。最终数值和质量仍由 MDS 唯一拥有。

G1增加 `holding:101` 只读点、FC03真实TCP回包、MDS HasValue/Good、写入拒绝、Channel断链 Stale。桥接不再保留未使用的 `IMPADataTargetAccess dataOwner` 构造参数。**运行合同要求先创建/注册/绑定/启动PAD Provider Host，再读取设备点**；否则操作应拒绝，而不是宣称数据提交成功。由于当前 ProviderHost 接收回调不返回逐点提交ACK，批量采样的可观察确认还应在正式MDS Owner中进一步完善，不以单个Emit布尔值代表最终下游存储/观察者都成功。

本段代码及本批正反测试已提交；必须以新SHA的 Windows CI为准判断G1，G2/G3仍未执行。

## 十、WP02→WP03：PLC 寄存器写入接入现有 MRK Command/Lease（2026-10-30）

正式程序集新增 `Professional/Automation/Plc/PADPlcMrkCommandRegistration.cs`，并由 `CxVisual.Professional.Automation.csproj` 引用现有 `Mechanism.Runtime`。没有新建 CommandCenter/Queue/ResourceRegistry：

- `RegisterUInt16Write(MRKCommandCenter, PADDeviceVariableProvider, DeviceRef, CommandId)` 利用现有 `FDCCommandDefinition` 声明 `StableId:string`、`Value:ushort` 和 `RegisterReadbackMatched:bool` 的版本化 Schema，注册 `MRKCommandKind.Device`；设备目标为 `Automation/PLC/{DeviceRef}`。
- 通过 `MRKCommandPolicy(ResourceId = Automation.PLC.Device/{DeviceRef}, MaxConcurrent=1, SupportsCancellation=false, MaxAttempts=1, Idempotent=false)` 使用 Host 的 `MRKResourceLeaseCoordinator`。同一设备的其他命令必须选择相同资源键；这只是同一 Runtime Host 进程内的软件租约，不等于硬件 Safety/跨进程互锁。
- `MRKCommandCenter` 的授权器不可为 null，Host必须对真实身份、角色、工站、模式和设备联锁进行验证；之后 PAD Provider 还执行专业写入授权。直接信任外部 Caller 字符串不构成身份认证。
- `PADPlcOutcomeUnknownException` 映射为 `MRKCommandHandlerResult(BusinessSuccess=false, BusinessCode=PLC_REGISTER_OUTCOME_UNKNOWN, RegisterReadbackMatched=false)`；当前 MRK 命令 State=Completed 表示**Handler已结算**，绝不意味着物理动作已完成。不能只检查 State，必须联合 BusinessSuccess/BusinessCode/专业物理证据判断。
- 设备写入没有自动重试；重复相同 OperationId 由 MRK 正式账本去重，同资源租约及拒绝未授权的逻辑在 MRK 正式 Owner 实施，不在 Automation 重复。

新增 `Tests/ArchitectureSmoke/Integration/AutomationPlcPointTests.cs` 的真实 TCP/HMI 无关 G1 场景：无 Host 授权拒绝、Guest 拒绝、外部持有设备资源租约时不应发送写报文、释放后 FC06+FC03 回读确认、同 OperationId 不重发，以及写寄存器 ACK 后读回不一致时 BusinessCode=UNKNOWN 且只发送一次。相关 G1/Config L1 结论需要以**本批固定 Commit SHA** 的 Actions 日志为准。

**未关闭**：MRK 层当前返回 Completed+业务未知，而不是全局 CommandState.Unknown；如需统一改变状态语义，必须由 MRK 唯一 Owner 决议和全系统回归，不可只在专业库更改。设备回读不是运动到位/光源安全停止的证明。Studio/TestCenter 设备调试页、跨进程 Host 认证、跨宿主资源协调与现场 G2/G3 仍待后续开发。

## 十一、WP02 PLC有界多点顺序扫描与逐点质量（2026-10-30）

本轮在正式 `PADPlcPointBridge` 中新增 `ScanAsync(IEnumerable<string>, CancellationToken)`，并以 `PADPlcScanResult` / `PADPlcPointScanResult` 给开发者返回**独立的逐点结果**，不复制 MDS Current、不新建扫描线程或另外的缓存/命令系统。

- **能力**：一次 1～64 个已绑定 PLC 点；先对全部稳定ID、重复、点表版本和MDS定义进行预检。无效集合在发送任何PLC报文前拒绝。
- **执行**：复用已有 `_writeGate` 与 `ReadAndMapAsync` 的FC03真实读回、单次暂存、连接代次/配置再校验及ProviderHost入MDS。按入参顺序逐点发出正式MPA/MCN事务，不生成第二套调度器。
- **失败**：单点 `TimeoutException`、`IOException`、`PADPlcOutcomeUnknownException` 记录明确的 `FailureCode`，该点经原有ProviderHost置Stale、不修改最后一个真实值/RefreshUtc；其余点继续采样，`AllSucceeded=false`。配置漂移、未授权、取消或连接代次变化属于整体fail-closed，立即中止，不伪装部分成功。
- **取消与一致性**：取消之前成功采样的点已在MDS，不能回滚；本API不是跨点原子采样、不是Modbus FC03合并寄存器批量读取，也不保证外部共享TCP设备的实时确定性。多设备并发/跨Host抢占仍由现有MRK全局资源约束和PLC控制器策略负责。
- **实测G1用例**：两个寄存器 `input101`（只读）与 `word100`（可写）经真实TCP分别读回，验证输入顺序/数值/Good；重复/空/未知点及取消必须在发包前拒绝；设备模拟端只丢弃一次 `input101` 的FC03应答，验证该点Stale且未伪刷新采样时间、后续 `word100` 仍为Good，真实恢复后输入再次Good。

**验收等级**：本批代码已提交至master并由 `.github/workflows/automation-plc-g1-windows.yml` 正式Windows专项验收；结果只可依据该具体变更SHA的真实CI日志。现场真实PLC G2、硬件安全/高负荷G3、单报文连续寄存器合并优化、周期调度和Studio/TestCenter交互仍未完成。

## 十二、WP02/PI-013：连续Holding寄存器单FC03合并批读（2026-10-30）

本批遵循现有F/M/P Owner在正式源码中完成以下实装，而非新增协议栈、设备采样仓或调度系统：

- **Mechanism.Connectivity**：`IMPAProtocolContiguousPointReader.ReadContiguousPointsAsync` 是可选规约能力；`MPAModbusTcpProtocol` 限定相同Unit、地址严格连续、1～125个Holding Int16/UInt16点，使用现有 `RequestAsync` MBAP事务、FC03数量与协议响应校验；返回按点顺序解码的局部对象数组，**不越权提交MDS**。MCN Connector仅以现有Channel/Session验证暴露方法。
- **Professional.Automation**：`PADPlcPointBridge.ScanContiguousAsync` 以1～64个稳定点作为专业输入，复用运行配置快照校验及已有互斥；拿到完整、可信的本地回包后检查Session代次、映射签名和值类型，才通过已绑定 `PADDeviceVariableProvider` 将各点逐一提交唯一MDS。通信/解码/配置失败不发布半批Good，并保留旧值且质量变Stale。
- **真实Windows G1新增测试**：本机PLC TCP端按FC03请求的数量生成单个相应PDU；检查 `holding:100/101` 两点在一次MBAP事务内读取、原先的顺序两帧扫描保持可用、非法反向点序/重复/空集合不应发包；异常回包数据数量不符拒绝整批、两点MDS值和RefreshUtc不变且质量Stale，只有新的真实FC03可恢复Good。
- **能力边界**：FC03单请求不等于跨多点MDS原子提交。一个FC03成功后的每点MDS写回仍经现有ProviderHost逐点发生；真实时间一致性、间隔周期调度、回包迟到/设备重连、安全PLC、硬实时性能及G2/G3现场还没有验收。Modbus 125寄存器上限在MPA；当前专业桥接主动限定64点，需调整时须与配置/性能基线共同评审。

新增测试编号 `AUT-PI-013` 软件G1，必须引用本轮准确SHA的Actions日志确认结果，未执行G2/G3前不得填写硬件通过。
