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


## 9. F17-P0 多客户端定向ID、HTTP/WS合计缓冲与异常退出（2026-10-28）

1. 已有`FTRSubChannelAddress(ChannelId,SubChannelId,Generation)`由HTTP Server为每次请求建立，WS/TCP/Pipe Server为连接建立。`Received.Address`及`MCNConnectorModule.PayloadReceivedFrom`携带来源；HTTP `RespondAsync(address,...)`与WS `SendToPeerAsync`校验身份和代次。运行期ID不是已认证用户ID。
2. 新`FTRBufferedByteBudget`/`FTRBufferedByteLease`属于Foundation.Transport内部；HTTP/WS Server的FCM新字段`TotalBufferedBytes`默认32MiB；原Diagnostics新增BufferedPayloadBytes、PeakBufferedPayloadBytes、RejectedPayloadBuffers及PayloadBufferByteBudget，TestCenter复用原Health只读展示。
3. HTTP聚合请求、扩容新旧缓冲峰值、独立Received上下文副本、响应副本均共享额度，`RespondAsync`成功后租约移交Serve唯一结束路径，重复/过期请求不泄漏额度；全局额度耗尽503，单请求体超过MaxRequestBytes仍413。WS对每个Peer的scratch、聚合、上下文复制统一计账，消息结束释放大缓冲，只关闭超额Peer。
4. `FTRChannelBase.ReceiveBytes`与`FTRMultiChannelBase.ReceiveFrom`兼容地增加有效长度重载；HTTP/WS以`MemoryStream.GetBuffer()`借用源切片，去除`ToArray`整消息副本，同时保持旧Received独立拷贝与SubChannel/Generation。未声明跨MPA/MDS全链路零复制。
5. 新`FTRMultiClientBudgetTests`验证真实Windows HTTP并发双请求、两种预算拒绝与状态码、WS双客户端单播、跨消息分片占用和独立存活。`FTRSharedMemoryCrashTests`新子进程命令`--channel-shm-crash-child`验证异常遗弃锁拒绝旧MMF，并以新Map恢复双向收发；不重播未知业务数据。
6. 均挂入原`--channel-uml-core`、`--channel-transport`，传统.csproj保持不新增项目。上述为源码完成/新CI待验收状态，旧WS预算测试失败证据见正式Testing文档；24h跨PC/现场设备和全量工作集GC SLA仍未达标。


## 10. V3.1 长期驻留Channel与便利接口实施（2026-10-28）

### 10.1 真实复用（不增加Transport Runtime）

在原`FTRChannelBase`保留唯一Socket读者、独立接收与同步Send；`FTRChannelConvenience`只提供扩展方法：`SubscribeReceived`、`SubscribeFrom`、`SubscribeState`、`SubscribeBuffers`、`SubscribeBuffersFrom`、`SendBytes`、`SendBytesAsync`、`SendTo`、`SendToAsync`、`ReplyAsync`。没有队列、线程、连接管理器、业务重试或隐藏的物理Open。

所有订阅方法返回`IDisposable`令牌，Dispose只撤销观察者，**不夺取Channel所有权**；同一个Channel对象正常Close/Open后，未注销的订阅继续有效，SessionId刷新。指定Peer订阅比较`FTRSubChannelAddress`完整地址（含Generation），旧代消息不能进入新代。订阅解除不能取消已进入正在执行的事件回调，调用方需做好并发收尾。MultiPeer单端便利Send主动拒绝，必须显式用`SendTo/ReplyAsync`保留子客户端ID，禁止隐式广播。HTTP的`ReplyAsync`委托原`IFTRRequestResponseIO.RespondAsync`，仅表示响应接纳，非物理交付或业务确认。

消息型`ReceiveBytes`逐个隔离`Received`观察者异常（与池化接收路径一致），避免一个长期驻留插件报错导致其他订阅收不到数据或WS/HTTP误断线。TCP/Pipe/HTTP/WS的Peer级错误通过`FTRFailureInfo.Address`和`FTRChannelFrame.Address`携带`SubChannelId + Generation`。既有三参数故障API、原`IFTRChannel`、`IFTRChannelTransport`与Owner保持兼容。

### 10.2 开发者典型用法（长期驻留由Host控制生命周期）

```csharp
// channel由Host/模块持有，不能在每次Send时临时new/dispose。
IFTRChannelTransport channel = factory.Create(configuration);
IDisposable received = channel.SubscribeReceived(message =>
{
    // 收到message.Address就是回包目标，含SubChannelId和Generation；
    // 对于HTTP使用channel.ReplyAsync(message, response)提交传输级应答。
    HandleMessage(message.Address, message.Data);
});
IDisposable lowCopy = channel.SubscribeBuffers(buffer =>
{
    // Segment仅在回调借用期内有效。异步保存必须Retain并最终Dispose。
    Inspect(buffer.Segment);
});
channel.Open();
// 单端：channel.SendBytes(data);
// 多端：channel.SendTo(message.Address, data);
// 仅在最终退出或不再监听时解除订阅：
received.Dispose();
lowCopy.Dispose();
channel.Close();
channel.Dispose();
```

以上是API用法示例；具体异常恢复/自动重连仍由Host及现有`FTRReconnectController`管理，FTR不得自动重发未知结果的命令。ByteStreamAdapter按Session建立视图，重连需要重新绑定视图，不能误当可无限重用的物理Reader。

### 10.3 可执行质量门禁

- `FTRChannelConvenienceTests`：Virtual长期驻留、观察者异常隔离、重复订阅解除、Host所有权、按接收上下文回复、MultiPeer无广播与旧Generation拒绝。
- `FTRPersistentChannelTests`：真实TCP Server/Client同实例连续四轮Open/Close、Session更新、双向定向数据、原订阅跨代有效、交付线程停止、外部Retain跨Close及池预算归零。
- `FTRMultiClientBudgetTests.HttpConcurrentResponses`：真实HTTP双请求的回复改用便利`ReplyAsync(packet,...)`，验证仍按请求子ID、预算正确。
- `FTRWebSocketAtomicTests.OversizedPeerIsolated`：超限错误的`LastFailure.Address`必须指向真实子客户端和代次。
- 以上增量都接到原`--channel-transport`门禁；**新SHA尚须分别看Windows日志**，不得用早期.csproj编译成功宣称最新长驻、异常隔离与HTTP/WS回归已通过。


### 10.4 长期HTTP Client消息原子性与同步/异步同底层（2026-10-28）

审计发现原`FTRHttpClientChannel`未覆盖`OnSendSynchronous`，基类按`segmentBytes`循环调用`OnSendAsync`，使单次`Send(data,segmentBytes:3)`可能被拆成多次独立POST。现通过`804733c1`改为同一个`PostAsync(byte[],offset,count,ct)`：同步Send等待完整一次POST，异步Send复用同一方法和`_sendGate`；`ByteArrayContent(bytes,offset,count)`在同步借用期内使用调用方源切片，不按网络分段拆分HTTP请求。保留原HTTP响应预算及上层消息边界。

`OnCloseAsync`先Dispose HttpClient中止正在进行的请求，随后`WaitForPendingSends(StopTimeoutMs)`结算已接纳操作；来自HTTP响应`Received`事件内部的同步Close用`IsAdditionalTransportThread`显式拒绝，避免观察者自等待死锁。消息观察者报错继续遵守统一Observer隔离规则。

`3530a41e`新增真实HTTP Client + Server操作系统回环：37字节正文用3字节分段提示同步Send，须恰好接到一次POST且原子正文完整、使用原请求SubChannelId应答；第二次SendBytesAsync复用同一路径。此为**源码及自动化断言已提交**，对应固定SHA Windows CI结束前不得认定通过。对外跨PC HTTP及24h高负载仍需另行验收。


### 10.5 长期复用SessionId与低复制租约身份（2026-10-28）

在`FTRChannelReceiveContext`增加兼容的只读`SessionId`属性，FTR正式接收时填入当前物理Session；外部自行构造上下文默认`Guid.Empty`，不虚构会话身份。池化`FTRReceiveBuffer`同样封存`SessionId`，由原`FTRReceivePipeline.Publish`在接收块Seal时传入，`Retain()`保留原会话ID且不分配字节副本。这样长期驻留Host把旧缓冲保留到新一次Open之后，仍能明确区分旧会话数据，不从活动Channel实时状态推断原缓冲的身份。

源码提交`25e46e4`、`511b892`、`d09cff2`、`f7a7a78`、`932c263`；`FTRChannelConvenienceTests`与`FTRPersistentChannelTests`新增会话ID/跨Close保留断言（`0c012a7`、`5bb3180`）。保持原IFTR公开方法、接收线程和Owner不变。以上新增断言的Windows CI须在固定SHA完成后才可登记通过。

## 2026-10-30 F17增量：HTTP Client响应接收有界与同步回调发送协调

- **现有缺口与Owner**：原FTRHttpClientChannel.PostAsync通过MemoryStream.ToArray再调用Receive，未为HTTP Client响应聚合启用FTRBufferedByteBudget；MemoryStream扩容、完整副本和FTRChannelReceiveContext隔离副本使大响应峰值偏高。接收回调发生在_sendGate内，回调同步Send第二次HTTP请求可能自锁。均属于唯一Foundation.Transport F17。
- **实装**：ConfigureInFlightBudget(_config.ReceiveByteBudget)；接收scratch和MemoryStream每次扩容（新旧数组重叠）预先Reserve，ReadAsync完整检查MaxResponseBytes，交付使用现有ReceiveBytes地址+有效count而非额外ToArray，后者在既有FTR预算下保留legacy Received的隔离快照语义。一次接收阶段总预算包含scratch、缓冲容量、交付隔离副本；额度不足明确抛FTRBudgetExceededException并释放额度，不能声称HTTP业务成功或者远端未执行。HTTP发送门在上层Received之前释放，回调可同步Send下一次请求，HTTP请求/响应仍是一条消息而非按segmentBytes重复POST。
- **配置与诊断**：FTRChannelBase.ConfigureSendAdmission中使既有CapturePayloadEvidence生效，含HTTP/WS消息型实现；CapturePayloadEvidence=false仅省略payload证据拷贝，真实TX/RX字节、故障及预算统计照常。ReceiveByteBudget现在实际约束HTTP Client临时聚合+交付，单条MaxResponseBytes并不承诺等额的低内存预算一定可以交付：应为scratch、旧新容量短时重叠及legacy Received快照留足实际额度。与旧独立TCP池预算并行，不创建第二套Channel、业务队列或Protocol。
- **复用对比**：已对照cx-vision-x2 Common/CommonInterfaces/Channels/TcpClientBase.cs的独立Thread但共用大锁、TcpServerService.cs的ConcurrentQueue以及Visions/CxCTVisionDetection/CtServiceUtils.cs的ArrayPool分块读取。没有搬运业务ReadLine/EOL组帧、无界队列或共享大锁；保留其池/分块理念并使用当前FTR预算与生命周期。
- **源码提交**：`5e3779c93`（HTTP Client主修复）、`00f797528` / `9ce091b6d`（真实回环测试及异常兼容）、`b89141bfc`（配置诊断开关）、`40a018d8e`（峰值与诊断测试）。无新增项目、NuGet和跨层Owner依赖。
- **实际验收入口**：原Tests/ArchitectureSmoke/Transport/FTRMultiClientBudgetTests.Run → HttpClientBoundedResponseAndReentrantSend，覆盖真实HTTP localhost请求/响应、回调内同步再Send、ReceiveByteBudget=512、Rx/Tx诊断无载荷、响应256字节因峰值超额拒绝且会话继续响应小请求。自动进入`--channel-uml-core`和`--channel-transport`，并由`.github/workflows/f17-channel-windows.yml` Windows x64全SLN + 双方向各2GiB验收触发。**本轮新SHA Windows CI仍在排队，不能判定已编译/通过**。
- **未完边界**：HTTP Client流式上层消费和跨专业DataHandle零复制（legacy事件仍按原合同复制）；24h/跨PC/真实Detector/CT设备持续性能；HTTP/WS多Peer公平与现场延迟SLA；TLS/认证新增按用户要求暂缓，未移除既有HTTPS及输入限制。

### F17 HTTP Client请求全程截止追补（2026-10-30）

- 实现`323a4b3b`：原HttpClient.Timeout搭配ResponseHeadersRead不能当作整个Body读完的保证，已在PostAsync外层以链接CancellationTokenSource.CancelAfter(RequestTimeoutMs)覆盖等待原Send门、SendAsync响应头和ReadAsync完整Body；对.NET Framework 4.8部分响应流不主动遵守取消的情形仅Dispose该响应流，不调用全局CancelPendingRequests、不中断其它请求。调用方取消优先保留OperationCanceledException，单次超时明确TimeoutException；诊断由FTR基类原路径记录。
- 真实慢流回归`723c10c4`/`d7ec8b6d`：OS HttpListener返回200和首块后保持Body未结束，运行中850ms总截止必须超时、未交付半包、在途额度归零；先核实服务端首块已经Flush，避免测试退化为“仅连接超时”。`6ae8fd4b`确保Timer上的Dispose异常不逃逸导致Host崩溃。
- F17工作流`43f048ad`配置同master分支并发合并取消旧运行，仅最新源码SHA能充当完整Windows回归证据；不能以取消的run为成功，也不能以本机源码提交替代.NET48构建。
- 后续还需HTTP请求/响应端到端业务追踪、跨PC慢端、TLS认证环境与24小时内存峰值/CPU/GC实测；本阶段不新增TLS/证书/身份认证。

### F17大响应默认配置与HTTP 4MiB真实回环（2026-10-30）

- `88e4f1f99`：FTRHttpClientChannelConfiguration在创建新HTTP Client配置时，将默认ReceiveByteBudget明确设为12MiB以覆盖其默认MaxResponseBytes=4MiB时的scratch、MemoryStream扩容期间新旧数组、Received独立副本的峰值。若FCM已有显式持久的ReceiveByteBudget，则按真实旧值执行，不自动扩大已设置的工业内存上限；超预算明确拒绝。用户可按更大Detector图像尺寸逐项增大MaxResponseBytes和ReceiveByteBudget并作真实峰值验证，不宣称自动零复制。
- `b89a1ba17`：原FTRMultiClientBudgetTests.Run增加真实HTTP 4MiB响应，有效长度及逐4093位置字节校验、FCM配置往返、预算Peak/current、收发诊断、连接清理；同时保留已提交的HTTP小报文/预算拒绝/嵌套同步Send/慢Body超时回归。`6195861e`使两套Windows F17入口都必须包含`F17.HTTP_LARGE_PASS bytes=4194304`日志，不满足即失败。
- 本项是HTTP Client聚合/legacy Received的工业大数据软件基线增量，不代替TCP及NamedPipe双向各2GiB的持续流门禁，也不等同于真正Detector/CT传输到专业DataHandle的24h/零复制性能验收。新增测试仍以最新固定SHA实际Windows结果为准。
