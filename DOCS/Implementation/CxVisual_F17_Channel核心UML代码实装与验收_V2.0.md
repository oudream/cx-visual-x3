# CxVisual F17 Channel 核心 UML 代码实装与验收 V2.0

> 2026-10-28；唯一 Owner：`Foundation.Transport`；约束：不新增项目、不复制 Channel/Protocol/MRK/MDS。固定 Windows 验收源码：`63ab4dc0ea345fb41d46d55bbeb13a1b6f205b2c`。

## 1. 核心 UML：实际类与依赖

| UML 角色 | 实际类型 | 状态与职责 |
|---|---|---|
| Channel / 扩展契约 | `IFTRChannel`、`IFTRChannelTransport` | 已有：Open/Close/Send、状态、低复制事件 |
| Channel 基类 | `FTRChannelBase`、`FTRSingleChannelBase`、`FTRMultiChannelBase` | 已有：状态机、单/多端点与 Generation |
| Factory / Provider | `FTRChannelFactory`、`IFTRChannelProvider`、`FTRChannelConfiguration` | 已有：FCM白名单、创建凭据、发现与注册 |
| TCP/UDP | `FTRTcpClientChannel`、`FTRTcpServerChannel`、`FTRUdpChannel`、`FTRUdpServerChannel` | 已有原生 I/O，不能再建立第二个物理读取者 |
| HTTP Server | `FTRHttpServerChannel`、`FTRHttpServerChannelConfiguration`、`FTRHttpRequestInfo`、`IFTRRequestResponseIO` | 新增：实际 HTTP 监听、请求 SubChannel、有限响应/容量/超时 |
| WebSocket Server | `FTRWebSocketServerChannel`、`FTRWebSocketServerChannelConfiguration`、`IFTRMessageIO` | 新增：真实 RFC6455 握手、二进制消息、多端收发与停止 |
| SharedMemory | `FTRSharedMemoryChannel`、`FTRSharedMemoryRegion`、`FTRSharedMemoryChannelConfiguration`、`IFTRSharedBufferIO` | 新增：Windows 命名 MMF 双向环槽、独立接收、同步发送和有界背压 |
| ByteStream 易用视图 | `FTRByteStreamAdapter : IFTRByteStream` | 新增：消费 FTR 的 `BufferReceived`，有界队列、Retain/Dispose、按字节读写 |
| Datagram 易用视图 | `FTRDatagramAdapter : IFTRDatagramIO` | 新增：不抢 UDP 读取者，保留报文边界、来源地址及多端定向 |
| 传输缓冲 / 诊断 | `FTRReceivePipeline`、`FTRReceiveBuffer`、`FTRChannelDiagnosticsSnapshot` | 沿用现有池化与追踪，未新增第二个业务队列 |

**UML 精度裁决：** `IFTRByteStream` / `IFTRDatagramIO` 应由逻辑适配器实现，不能要求 TCP/UDP 再新增一个直读 Socket 的同名方法，与已有独立接收循环争抢字节。共享内存侧的 `IFTRSharedBufferIO` 则由具体 Channel 实现。接口符合传输语义，图上的直接实现箭头应按真实归属更新。

## 2. 软件验收证据

- [F17 专项 #40650051724](https://github.com/oudream/cx-visual-x3/actions/runs/40650051724) success：完整 Windows x64 Build、15内置类型矩阵、真实 HTTP/WS/SharedMemory 和跨进程、全部 Channel 回归与双向 2GiB 传输门禁；同时修复旧 NamedPipe 子进程退出握手。
- [F17 专项 #40650681417](https://github.com/oudream/cx-visual-x3/actions/runs/40650681417) success：ByteStreamAdapter 真 TCP 分段读写、旧标准通道和 2GiB 门禁。
- [F17 专项 #40650946570](https://github.com/oudream/cx-visual-x3/actions/runs/40650946570) success：DatagramAdapter 真 UDP 来源与定向回送、完整基础回归及 2GiB 门禁。
- `ArchitectureSmoke.exe --channel-uml-core` 已输出 `F17.UML_CORE_PASS`；`--channel-transport` 是完整 Connector 与 Channel 回归。
- 串口硬件、PLC、外部多机、24h 负载、跨重启 SharedMemory 恢复及实际工业吞吐 SLA 不在上述证据范围。安全新增 TLS/认证按用户要求暂缓。

## 3. 下一层 14 项能力的未完成内容

- HTTP 与 WebSocket 的**长期高并发、流式大消息与慢客户端预算**、完整心跳与自动恢复仍需专项实现/验收；当前 HTTP/WS Server 只允许本地回环前缀，不能作为对外生产 Web 服务直接部署。
- SharedMemory **跨进程基础双向交换已真实验证**，但异常崩溃后重启/版本升级、多进程竞占、长时吞吐与共享区域独占仍需进一步设计与验证；不宣称生产级崩溃恢复。
- 核心便捷 Adapter 用于中小规模代码接口和兼容，不代表零复制专业数据链路。大图像/CT 应优先对接 `FTRReceiveBuffer` 租约、MPA 分块和 MDS/专业 DataHandle。
- 自动重连、超时、缓冲背压、诊断配置需持续按**每类通道**审计真实生效情况；不得将 TCP/UDP/NamedPipe 已有验收推广到未测试的 Serial/HTTP/WS/共享内存环境。
- 保留 `Foundation.Transport` 唯一 Channel、`Mechanism.Connectivity` 唯一 Protocol/Connector、`MDS` 数据所有权、`MRK` 业务调度的边界，不创建第二套统一运行器。

**阶段界限：** 本次完成的是用户第二张图所指的核心 FTR 类图与关键能力接口的可运行增量，并不是第一张图 14 大方面的全部最终交付。

## 4. 2026-10-28 可靠性、会话与TestCenter增量

- `FTRReconnectController`、`FTRReconnectSnapshot`：显式启用的传输连接自动恢复，执行已有FCM重试次数、间隔及指数退避，支持取消、诊断与最大次数；**只Close/Open物理Channel，不重复未知业务命令**。确定性失败/次数上限及真实TCP断线恢复用例已入`FTRReconnectControllerTests`。
- `FTRSharedMemoryRegion`与`FTRSharedMemoryChannel`：同一Map+Side由独立接收线程持有Windows命名Mutex；重复实例必须拒绝，不应破坏单生产者环。重复端点拒绝及原始双端收发回归见`SharedMemoryLoopback`。**此补丁不是崩溃后环槽幂等恢复或无丢失承诺**。
- `FTRChannelBase.SessionId`：每次Open刷新并以锁读取，避免物理重建后的旧会话身份复用；`MCNConnectorModule`在Opening/Faulted委托正式Protocol清理解析器、未完成事务和旧半包，不把协议逻辑下沉到FTR。
- `ModuleChannelTransportSmokeTests.ChannelSessionIsolation`：使用正式Virtual/LengthFrame通道送入半包，重开后验证新SessionId与完整新帧解析，明确防止跨连接污染。
- TestCenter人工Channel工作区不再维护六类型私有清单：使用正式15内置Kind及Factory创建MultiPeer离线目录；UIA逐项验证拓扑与未经授权不创建监听/设备连接。
- 以上增量的新Windows CI应按**各提交固定SHA**逐次认定；既有`63ab4dc0`等成功证据不能冒充新提交通过。后续仍需跨机现场、Serial设备、24h寿命及SHM崩溃恢复专门验收。


## 5. 重连事件回调安全停止补丁（2026-10-28）

- 源码提交 `11584be92a40a8e355fae570f3f1fb96544cf76d`；回归提交 `4d9976ee74560115dfcbcd6e76a7205c33372e5a`。
- 代码审计发现：原 `FTRReconnectController.StopAsync()` 在 `AttemptFailed`、`Reconnected`、`Exhausted` 同步回调中被调用并阻塞等待时，存在恢复任务等待自身的死锁风险。
- 新增恢复任务上下文区分：**回调内仅同步请求取消并委托继续清理；回调外 `StopAsync()` 仍等待恢复任务结束与取消源释放**。上轮停止尚未完成时，拒绝重新 `Start()`，避免两代恢复任务重叠。
- `FTRReconnectControllerTests.StopWithinRecoveryCallbacks` 补充失败回调内同步 `StopAsync()`、成功回调内同步 `Dispose()`、外部停止等待、停止后不再重连的回归用例；原真实 TCP 重连、重试上限和停止取消用例保留。
- **Windows 固定 SHA 验收**：[F17 #40656980087](https://github.com/oudream/cx-visual-x3/actions/runs/40656980087)（`11584be9`）与 [F17 #40657016715](https://github.com/oudream/cx-visual-x3/actions/runs/40657016715)（`4d9976ee`）工作均为 `success`。后者的实际日志输出 `F17.RECONNECT_PASS ... callback-safe stop`，同时 `F17.UML_CORE_PASS`、`CHANNEL.TRANSPORT`、TCP/NamedPipe 各向 2GiB 完整性门禁通过；此为软件 Windows 回归，不代表 24h 长稳或现场设备验收。


## 6. V3.0 六能力S1/S2与WS消息原子性代码实施（2026-10-28）

- **执行控制**：新 `Channels/IO/FTRDeadline.cs` 使用 `Stopwatch.GetTimestamp`；TCP持同一截止时间覆盖进入发送门及所有`NetworkStream.Write`分块。NamedPipe原有`FTRTransportThread.Remaining`继续保持同一总时间预算，其他Channel尚需逐类型复核。
- **故障事实**：`FTRFailureInfo` 分Kind（Timeout/Cancelled/RemoteClosed/NetworkUnavailable/PermissionDenied/InvalidConfiguration/ResourceExhausted/TransportIO/Unknown）、Scope（Operation/SubChannel/Channel）、Stage、Message、TimestampUtc；`FTRFailureClassifier`处理嵌套IO/Socket异常，不据文本推断故障。异常分类属于诊断，不许可业务重发。
- **诊断**：原`FTRChannelDiagnostics`延伸类型化最近故障与按需速率采样；原`FTRChannelDiagnosticsSnapshot`增量字段；`FTRChannelBase.Health`生成只读`FTRChannelHealthSnapshot`。Observer异常限于Operation作用域，不能冒充物理Receive故障。异步预算拒绝先于clone，并记录`SendAdmission/ResourceExhausted`；兼容原InvalidOperationException的catch。
- **重连**：保留`FTRReconnectController`唯一执行主体。按分类停止权限/非法配置/资源耗尽/非主动取消重试，既有可恢复异常仍遵守次数和退避；`FTRReconnectSnapshot`增累计失败/成功、最后恢复耗时/UTC和上次故障事实，不重放未知执行结果的业务命令。
- **WebSocket**：Client的`OnSendSynchronous`和Server的同名覆盖都调用各自同一个`WriteMessageAsync`：先取得单连接/单Peer发送门，最多16KiB默认分片或显式SegmentBytes，只有最后一片EndOfMessage=true，然后释放发送门。由于.NET WebSocket只有async底层API，同步Send是阻塞等待一次完整原子消息操作，不宣称使用不存在的同步WebSocket OS API。原异步Send复用同一物理发送。
- **兼容**：没有更改`IFTRChannel`、`IFTRChannelTransport`签名、FTR/MPA/MCN/MDS/MRK Owner、FCM旧字段和15种Channel的类型身份；新类已逐个加入传统Transport.csproj，ArchitectureSmoke测试仍由项目原通配Compile编译。

验证见原[测试矩阵](../Testing/CxVisual_F17_Channel_P0大数据与线程模型_验收矩阵_V1.0.md)，**六项仍为逐步推进，不代表全类型P0完成或软件工业验收完成**。


### 6.1 预算、运行配置、TestCenter后续代码

- 在`FTRChannelBase`补`ConfigureSendAdmission`（原`StartBufferedReceive`内部复用）；不要求没有池化Reader的HTTP/WS/Serial额外创建无意义的接收线程。异步发送先验证容量再clone；同步Send仍遵守源数组借用的原API语义。
- HTTP/WS/Serial分别在实际Open成功路径启用新配置；此处限制的是**异步发送快照入场**，不宣称限制HTTP业务响应Pending载荷的整个生命周期或WS接收多Peer聚合内存。后者仍须独立的端到端总预算设计。
- `FTRReconnectController`冻结有效重连策略，诊断累积计数仍持续；不存在复制的Recovery Runtime。
- `TCTManualChannelFrame`每秒WPF DispatcherTimer读取现有`FTRChannelBase.Health`，`OnWorkspaceClosed`停止计时；新增`channel.health`手动操作与`TCT.Channel.Health`自动化标识。不暗中打开非Virtual设备，不建立第二套Socket读取者。


## 8. V3.0 末轮代码增量：Peer故障范围与异步切片单复制

- `54ac4bef`：`FTRChannelBase.Health`在同锁中采样State/Session，只有Open才计ActivePeers。离线TestCenter登记的目录Peer不冒充在线物理连接。
- `7735ef1e`：保留`RecordReceiveError(Exception)`委托签名，新增加`RecordReceiveError(Exception,FTRFaultScope)`；TCP/NamedPipe/HTTP/WS多端接收故障指定SubChannel，HTTP/WS超报文预算使用明确的`FTRBudgetExceededException`，HTTP仍按413处理，Server监听与错误Peer相互隔离。观察者异常仍独立归Observer/Operation。
- `b95ff48a`：真实WS双客户端OS回环覆盖一个超额Peer退出、另一Peer正常交付且Server Open，见测试矩阵。
- `27d934b5`：`FTRChannelBase.SendAsync(byte[],offset,count,address,ct)`为新增重载，不变更`IFTRChannel`，先占用原异步数量/字节额度，再`Buffer.BlockCopy`调用方切片一次并保留直到发送结算；原三参数Async API转用此实现。
- `3a549615`：`FTRByteStreamAdapter.WriteAsync`检测正式`FTRChannelBase`时走单快照切片API，第三方IFTRChannelTransport仍沿用原接口回退，不额外建立OS接收者。`8fb97eb9`补按有效count结算与源缓冲后修改不污染发送的回归。
- `56fc61cf`：原无池化Reader的HTTP Client新增入场拒绝负例，证明超预算时不建立实际网络请求，不复制传入数组。
- 这些是六项能力的增量，不等于消除HTTP/WS接收端MemoryStream/ToArray、跨MPA/MDS零复制以及现场24h长稳的完整任务。Windows CI必须逐固定SHA记录，不能借旧版本成功冒充后续代码通过。
