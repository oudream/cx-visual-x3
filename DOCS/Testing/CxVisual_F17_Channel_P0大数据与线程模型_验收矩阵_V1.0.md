# F17 P0 大数据与线程模型验收矩阵 V1.0

2026-10-27。范围为完整Channel目标中的TCP P0增量；其它通道和长期/现场门禁不因本矩阵通过而完成。
对应[完整14方面审计](../Architecture/CxVisual_F17_Channel完整体系_能力审计与实施基线_V1.0.md)、[正式实现与所有权合同](../Implementation/CxVisual_F17_Channel_P0独立接收与有界大数据_实施记录_V1.0.md)。

## 可执行用例与实际判据

`Tests/ArchitectureSmoke/Transport/FTRTcpBulkTests.cs`，`--channel-bulk`；原`--channel-transport`和默认回归也执行短专项。

| 用例 | 执行方法 | 正例与失败判据 |
|---|---|---|
| F17-P0-01 | ConfigRoundtrip | FCM新限制往返、Kind切换保持、非法块大小拒绝 |
| F17-P0-02 | AsyncSendAdmission | 入场前数量/字节预算；拒绝不得调用底层或创建快照；已入场快照不受源数组修改；结算释放额度 |
| F17-P0-03 | DuplexBulk | 双向各40MiB逐字节、顺序和总数；非线程池后台交付、接收线程独立；池与证据预算、完整诊断计数 |
| F17-P0-04 | RetainedBackpressure | 两块131072字节预算；消费者阻塞产生真实背压；反向发送不受接收锁阻塞；不丢1MiB数据；跨Close保留租约、归还与重复Dispose |
| F17-P0-05 | MultiPeerThreadBudget | 一个Server同时12个真实TCP对端、独立SubChannel、精确回送；共享接收/交付线程与有限缓冲 |
| F17-P0-06 | StopAndObserverIsolation | 异常订阅者不吞后继交付；慢回调Close必须超时/Faulted；回调释放后重新Close才结算并释放池 |
| F17-P0-07 | SynchronousOrderAndAsyncSnapshot | 并发两个1MiB同步Send，8191/4001分块不允许交错；异步64KiB发送后修改源数组不污染网络数据 |
| F17-P0-08 | LifecycleAndCallbackClose | 回调不能等待自身关闭；状态观察器异常不破坏状态流；Open的Token取消后仍可收发；显式重连、Dispose幂等及禁止重开 |
| F17-P0-09 | IdleTimeouts | Client读空闲变Faulted并有诊断；Server单Peer读空闲清理而Listener保持Open |
| F17-P0-10 | HalfCloseTailDelivery | 在回调被阻塞时真实Shutdown(Send)，确认EOF通知已排队；Protocol不能提前重置；最终尾包仍能回应然后再移除Peer |
| F17-P0-11 | RunVolumeScale / --channel-bulk-long | 同一连接双向各2147483648字节，不分配整卷数组，逐字节校验；每端池<=262144字节，输出环境、墙钟、CPU、内存、GC、背压和线程 |
| F17-P0-12 | 原ModuleChannelTransportSmokeTests | 原12类装配、Virtual/TCP/UDP/NamedPipe/HTTP/WS/Replay、非法配置/代次、Connector/Modbus实际回环全部保留 |
| F17-P0-13 | TCT.UI.ARCHSMOKE.001 | TestCenter真实隔离Worker检查14个固定CLI预设，保留原11个，3个F17入口可在原测试控制台操作，伪造参数仍拒绝 |

以上是软件可执行路径，不把Serial/RS485、PLC、跨PC、外部网络和硬件ACK验收列为已通过。

## 本地Windows实测

- 完整.NET48 Debug/x64方案编译成功，`UseSharedCompilation=false`用于避免本机编译Server连接等待，不改变语言/目标框架。
- `--channel-transport`实际exit0，输出原`CHANNEL.CONNECTOR: 12 built-in FTR kinds`及`CHANNEL.TRANSPORT: TCP/UDP loopback`成功证据。
- `--channel-bulk`实际exit0；40MiB双向、异步入场、慢消费者、12对端及生命周期正反用例运行。
- `--channel-bulk-long`初测exit0：双向各2GiB，第一轮8827ms，随后一轮7724ms；后一轮CPU33328.1ms、managed delta15904048、working set44195840、GC0=14/GC2=1，两端接收池各262144字节，峰值4块。吞吐包含数据生成和逐字节验证，不是纯网络带宽或产线SLA。
- ArchitectureSmoke默认回归与`--concurrency`已实际运行成功；TestCenter `--run --id TCT.UI.ARCHSMOKE.001 --isolate`产生JUnit：tests=1、failures=0、errors=0。
- 提交前最终复核：默认回归第一次在既有`ExecutionTriggerTests.PeriodicTriggerNeverLaunchesUnboundedFlows`的周期触发时序断言失败；未修改该测试和Runtime，原代码完整重跑exit0，随后`--concurrency`exit0。该波动保留为待观察项，不以重跑覆盖首次失败记录。
- 本机沙箱默认权限拒绝NamedPipe连接与File.Replace，正常Windows权限重跑后真实通过；不为环境权限问题放宽测试或改传输实现。
- 日志位于`Output/Debug/x64/f17-p0-*.log`及`F17-TestCenterProfiles.junit.xml`，不提交生成物。

本地证据对应本轮工作代码；正式源码提交与远端CI必须按最终固定SHA另行核对，不能把以上当作远端通过。

## Windows CI门禁

现有`.github/workflows/f17-channel-windows.yml`保留完整Release/x64 Rebuild、原通道回归和唯一Owner检查，并必须执行双向2GiB，检查实际完成字节标记。Owner检查逐个核对60个现有FTR源文件均有唯一Compile项，不再以过时固定56限制真实增构；旧通道源码不得回流Mechanism。体数据指标日志作为artifact保存。

## 固定源码的远端验收补录（2026-10-28）

- 正式实现提交`edbdefbe7180517af20b98ae655025c705d10df2`；与并行master正常合并后，实际CI源码`549e7ed2be43a32952536b286e8c5ce771461631`。
- [F17 Windows专项40540778043](https://github.com/oudream/cx-visual-x3/actions/runs/40540778043) success；完整Release/x64方案Rebuild、原通道/Connector/OS回环、短专项、60源文件唯一Compile/Owner全部成功。
- 真实CI双向各2147483648字节完成；9450ms，CPU32515.6ms，managed delta14995640，working set40742912，GC0=14/GC2=1，两端接收池各262144字节/峰值4块，含生成与逐字节校验的总吞吐433.43MiB/s；`F17.BULK_VOLUME_PASS`存在。指标是该4逻辑CPU runner的软件样本，不是工业SLA。
- [完整TestCenter Windows 40540777960](https://github.com/oudream/cx-visual-x3/actions/runs/40540777960) success；图元WPF/工程Undo、三维GPU与UIA、默认142用例及Regression的152唯一用例×3=456次并行尝试全部通过。阴性探针中的故意失败由CI逐项核对其失败语义，不计作真实套件意外失败。
- 上述证据只覆盖固定SHA；后续Factory/Provider增量需另按自身源码SHA验收。

## 完整目标尚未验收

24小时及更长持续负载、工业网络丢包/断网、多机网卡与组播环境、真实RS232/485/PLC/采集设备HIL、所有通道独立接收与有效预算、HTTP/WS Server、SharedMemory、Custom Provider、自动重连/心跳/保活及全部TestCenter手动操作仍按完整审计继续。不以2GiB软件样本替代这些门禁。新增TLS/证书/认证暂缓，现有安全保护保留。

## P0-RC-01 恢复事件回调内停止/释放（2026-10-28 补充）

| 用例 | 入口 | 验收要求 | 证据 |
|---|---|---|---|
| F17-P0-RC-01 | `FTRReconnectControllerTests.StopWithinRecoveryCallbacks`，由 `--channel-transport` 调用 | 失败事件内同步 `StopAsync` 不发生自等待死锁；成功事件内同步 `Dispose` 亦不死锁；外部停止实际等待任务资源清理；停止后没有新一轮自动连接 | [F17 Windows #40657016715](https://github.com/oudream/cx-visual-x3/actions/runs/40657016715) success，固定SHA `4d9976ee`，日志 `F17.RECONNECT_PASS ... callback-safe stop` |

涉及 `FTRReconnectController.cs`，未修改 Protocol/Connector/MDS/MRK 或业务执行语义。此专项回归不覆盖强杀进程、24h长稳、现场网络或PLC设备。


## V3.0 故障、健康与WebSocket原子性验收增量（2026-10-28）

| 证据编号 | 实际入口 | 断言及阴性边界 | 证据状态 |
|---|---|---|---|
| F17-S1-01 | `FTRChannelHealthTests.ClassifiedDiagnostics`，由`--channel-transport`调用 | Cancellation/Timeout/PermissionDenied/嵌套Socket错误按Kind/Scope/Stage分类；健康速率有真实采样窗口 | [F17 #40660269567](https://github.com/oudream/cx-visual-x3/actions/runs/40660269567) success，源码`4cb84aa1` |
| F17-S2-01 | `FTRChannelHealthTests.ChannelLifecycleAndHealth` | 发送失败、通道Fault、Open失败清理和会话/状态/Peer快照 | 同上 |
| F17-S2-02 | `FTRChannelHealthTests.ObserverIsolation` | StateChanged观察者报错不污染物理链路状态，归Observer/Operation | 新增`61776e5c`，待固定SHA验证 |
| F17-S1-02 | `FTRReconnectControllerTests.FatalFailureStopsRetries` | 权限拒绝只能尝试一次即停止，次数、最后故障类型、失败与成功统计准确 | [F17 #40660492860](https://github.com/oudream/cx-visual-x3/actions/runs/40660492860) success，源码`321b0525` |
| F17-S3-01 | `FTRWebSocketAtomicTests.Run`，`--channel-uml-core`与`--channel-transport` | 真实WS Client同步4KiB分片只生成一条消息；Server同Peer并发多分片无交错、完整双向字节校验 | 新增`b6123dc2`，待固定SHA验证 |
| F17-S5-01 | `FTRTcpBulkTests.AsyncSendAdmission`，`--channel-transport` | 拒绝入场不克隆、不执行物理发送，计数与ResourceExhausted/SendAdmission/Operation一致 | 新增`d54928fc`，待固定SHA验证 |

保留原`F17.UML_CORE_PASS`、`F17.RECONNECT_PASS`、`F17.BULK_VOLUME_PASS`和`F17.PIPE_VOLUME_PASS`双向各2147483648字节检查；另需检查新增`F17.HEALTH_PASS`及`F17.WS_ATOMIC_PASS`实际日志。旧成功CI不替代新固定SHA。后续欠缺24小时持续流、跨PC、工业网络/硬件、HTTP/WS多客户端总内存预算、现场CPU/GC SLA、共享内存崩溃恢复等。


### V3.0 后续新增正反例与验收条件

| 增量 | 用例 | 状态 |
|---|---|---|
| `d54928fc` | `FTRTcpBulkTests.AsyncSendAdmission` 校验ResourceExhausted与入场故障作用域，拒绝前不分配快照 | [F17 #40661070975](https://github.com/oudream/cx-visual-x3/actions/runs/40661070975) success |
| `61776e5c` | `FTRChannelHealthTests.ObserverIsolation`，观察者异常不导致Channel假故障 | 新增代码已提交，固定SHA独立CI待核实 |
| `0ddd1f4c` | `FTRWebSocketAtomicTests` 真实HTTP Upgrade回环：4097字节异步发送在4096预算下预先拒绝；仍允许大块同步分片和并发完整性 | 新增代码已提交，固定SHA独立CI待核实 |
| `489c7f3b` | `FTRReconnectControllerTests.BackoffAndLimit` 修改原FCM配置后原控制器仍使用构造时的2次上限和短退避 | 新增代码已提交，固定SHA独立CI待核实 |
| `4c398047` | TestCenter WPF UIA 在Virtual真实收发后检查`State=Open`、Session、TX/RX健康视图，并保持离线通道不连接实体设备 | 新增代码已提交，TestCenter Windows最终结果待核实 |

所有“待核实”只能在对应固定SHA的CI真正结束并有日志后改为通过；仍不覆盖24h长稳、跨PC、真实硬件、HTTP/WS跨层全链路总内存及工业SLA。


## V3.0 新增Peer隔离、单快照及配置稳定性追踪（2026-10-28）

| 代码固定SHA | 测试用例 | 反证与状态 |
|---|---|---|
| `489c7f3b` | `FTRReconnectControllerTests.BackoffAndLimit` | 外部修改原FCM重连次数/间隔不能改变控制器已冻结的有效策略；[F17 #40661871142](https://github.com/oudream/cx-visual-x3/actions/runs/40661871142) success |
| `54ac4bef` | `FTRChannelHealthTests.ChannelLifecycleAndHealth` | 未Open TCP Server的离线目录Peer不能算在线；新固定SHA待验证 |
| `b95ff48a` | `FTRWebSocketAtomicTests.OversizedPeerIsolated` | 一个WS客户端发送超消息上限字节后记录SubChannel/ResourceExhausted，另一客户端完整收发且Server仍Open；新固定SHA待验证 |
| `56fc61cf` | `FTRChannelHealthTests.HttpClientAdmissionWithoutNetwork` | 不使用池化接收的HTTP Client异步发送在快照与网络前按字节预算拒绝；新固定SHA待验证 |
| `8fb97eb9` | `FTRTcpBulkTests.AsyncSliceSnapshot` | 新增Async切片重载只复制一次，源修改不污染已接纳数据，预算按有效count而非整个源数组结算；新固定SHA待验证 |
| `3a549615` | `ModuleChannelTransportSmokeTests.ByteStreamAdapterLoopback` | ByteStreamAdapter基类路径复用单次异步切片快照，Custom Provider接口保持兼容；新固定SHA待验证 |

对应代码包括`RecordReceiveError`旧签名+SubChannel重载、HTTP/WS接收预算分类、`FTRChannelBase.SendAsync(byte[],int,int,...)`新增兼容重载、`FTRByteStreamAdapter.WriteAsync`去重复制。原`--channel-transport`及`--channel-bulk-long`继续保留，不因局部成功宣布跨PC、24h长稳或CT专业零复制链路通过。


## F17-P0 多客户端ID、HTTP/WS合计在途额度与共享区崩溃回归（2026-10-28）

| 测试 | 实际代码路径 | 验收目标 |
|---|---|---|
| HTTP两个请求分别回应 | `FTRMultiClientBudgetTests.HttpConcurrentResponses` | `SubChannelId+Generation`不同；大响应占总额度，第二大响应拒绝，小响应成功；无交叉回复，结束时租约归零 |
| HTTP总额度和413/503 | `HttpAggregateOverload` | 全局额度满返回503，单请求体过大返回413；其他已接纳请求不受损 |
| WS两客户端定向路由 | `WebSocketTwoClientsRouted` | 不同SubChannelId+Generation；分别向原连接单播，不广播 |
| WS慢客户端/预算 | `WebSocketFragmentedPeer` | 未完成分片持续占用额度；另一Peer总量超限被隔离，原Peer仍完成消息，峰值受限 |
| FCM | `SharedBudgetLease` | HTTP/WS `TotalBufferedBytes`配置往返、错误预算拒绝 |
| SharedMemory崩溃 | `FTRSharedMemoryCrashTests.Run` | OS子进程异常退出遗弃Side Mutex；旧共享区禁止复用；新Map/Session双向完整 |
| 低复制兼容 | `ReceiveBytes/ReceiveFrom`新增Count重载 | HTTP/WS直接借用MemoryStream有效切片，Received独立快照在额度内，原接口不变 |

本轮旧版本Windows CI [#40664424966](https://github.com/oudream/cx-visual-x3/actions/runs/40664424966)、[#40664609330](https://github.com/oudream/cx-visual-x3/actions/runs/40664609330)、[#40664714152](https://github.com/oudream/cx-visual-x3/actions/runs/40664714152) 因WS测试低估消息交付时峰值而失败；后续调整测试额度、移除整消息ToArray复制、增加接收上下文额度。新版须按固定SHA Windows CI验证后方能改为通过。当前尚无24h、跨PC、工业硬件及FTR→MPA→MDS端到端内存峰值SLA证据。


## V3.1 长驻Channel、便利API和子端错误追踪验收补充（2026-10-28）

| 场景 | 实码/提交 | 正反断言 | 当前证据 |
|---|---|---|---|
| 消息观察者隔离 | `FTRChannelBase.ReceiveBytes`，`652d30c6` | 一个Received订阅抛错后其余订阅仍收到消息，故障记Observer而非物理故障 | [F17 #40672585446](https://github.com/oudream/cx-visual-x3/actions/runs/40672585446) success，仅证实此固定源码及此前门禁 |
| 长驻便利接口 | `FTRChannelConvenienceTests`，`cdc7d7a`—`350bab0` | 单端快捷发送、Observer令牌幂等解除、跨Open/Close订阅、MultiPeer必须带子ID、旧Generation回包拒绝 | 源码已提交，包含测试的固定SHA Windows CI待验收 |
| 低复制便利订阅 | `FTRChannelConvenience.SubscribeBuffers/SubscribeBuffersFrom`，`1ce4f2f` | 不创建新物理Reader，保留租约回调期借用与跨线程Retain/Dispose纪律 | 源码已提交，新固定SHA CI待验收 |
| 长驻TCP真实回环 | `FTRPersistentChannelTests`，`d298ec1`、`247fb07` | 原TCP Client/Server对象四次重开、Session更新、双向收发、子ID、后台线程停止、Retain跨Close、释放后BorrowedBuffers为0 | 新固定SHA Windows CI待验收 |
| HTTP便利回复 | `FTRMultiClientBudgetTests.HttpConcurrentResponses`，`bf52ca2` | 两个请求直接用`ReplyAsync(packet,...)`，保留原请求Address、预算、响应数据完整性 | 新固定SHA Windows CI待验收 |
| 客户端级故障定位 | `FTRFailureInfo.Address` + TCP/Pipe/HTTP/WS错误路径，`f90bc0c`、`ee7f7d5`、`9262eb4` | WS超限故障必须给出Kind=ResourceExhausted、Scope=SubChannel及非空完整Address/Generation | 新固定SHA Windows CI待验收 |

原F17完整`--channel-transport`仍同时执行UML、协议集成、SharedMemory崩溃回归、真实HTTP/WS双端预算和TCP/NamedPipe容量测试；`--channel-bulk-long`独立双向各2GiB。新便捷层不会代替Connection/Protocol/MDS各自Owner、也不会推断远端业务已确认。

**现场缺口持续保留**：所有15类型参数生效矩阵、HTTP/WS峰值内存与GC/CPU SLA、CT/X-Ray到专业DataHandle低复制闭环、24小时和跨PC真实设备及断线竞争测试。代码已提交并不等于全部验收通过。


### V3.1 便利健康视图与HTTP分段消息原子性补强（2026-10-28）

- `804733c1`、`3530a41e`：HTTP Client的`Send(data,segmentBytes:n)`不能拆成多条POST；同步/异步统一`PostAsync`、单次请求正文带offset/count；真实回环37字节、segmentBytes=3严格验证一条POST与两次同步/异步各一条请求，关闭时等待PendingSends。
- `46d8a101`、`a20a3f4e`：对`IFTRChannelTransport`开放`ReadHealth()`便利调用，无需Factory返回者强转具体类型；真实FTR基类重用原Health，Custom Provider回退标准状态、会话、Peer目录、Diagnostics，测试覆盖Virtual和MultiPeer。
- `0445dc9b`、`10f5e418`：PeerRemoved单个观察者异常应记`Observer/Operation`，其余观察者仍得到通知，父Channel仍Open；该正反例已纳入便利测试。
- `eee8cd6a`：TestCenter健康监控增加`LastFailure.Address`的子客户端ID与Generation呈现，使用正式诊断而非独立记录。
- **此组最新固定SHA Windows CI尚未结束**；新测试不因更早`350bab076`成功而自动记为通过。全部工控/CT/设备长稳SLA仍待单独验收。

## 2026-10-30 F17 HTTP Client资源预算/回调发送新G1门禁（待Windows结果）

| 用例 | 正式入口 | 真实验证断言 | 状态 |
|---|---|---|---|
| HTTP-CLI-01 | FTRMultiClientBudgetTests.HttpClientBoundedResponseAndReentrantSend | 真实HttpListener两个POST：第一个Received回调内同步调用同一Client.Send，第二个响应完整到达；若发送门在回调内持有即死锁；6秒有界等待 | 源码已提交，Windows新SHA待验 |
| HTTP-CLI-02 | 同上 | ReceiveByteBudget=512，ReceiveBufferBytes=16；真实256字节成功HTTP响应必须因缓冲容量+交付隔离副本峰值超过512而拒绝，禁止交付；额度归0、峰值不超过512、拒绝计数增加 | 源码已提交，Windows新SHA待验 |
| HTTP-CLI-03 | 同上 | 拒绝后的同一FTR HTTP客户端仍为Open，继续接收下一次小POST回复；业务结果未知不自动重放 | 源码已提交，Windows新SHA待验 |
| HTTP-CLI-04 | 同上 | CapturePayloadEvidence=false时实际Received/Sent计数仍增长，ReadFrames中Rx/Tx的Bytes为空，不保留诊断大报文副本 | 源码已提交，Windows新SHA待验 |
| HTTP-CLI-05 | 已有HttpClientSegmentedPost | segmentBytes=3单个37字节同步Send仍仅一次HTTP POST，异步复用相同底层单消息；原合同回归不变 | 旧固定SHA已有Windows证据，新SHA待验 |

变更路径：Foundation/Transport/Channels/Implementations/HTTP/FTRHttpClientChannel.cs、IO/FTRChannelBase.cs、Tests/ArchitectureSmoke/Transport/FTRMultiClientBudgetTests.cs；实施及x2对照见DOCS/Implementation/CxVisual_F17_Channel核心UML代码实装与验收_V2.0.md。
已有P0 TCP与NamedPipe双向各2GiB和池化Retain跨Close回归保持原入口；本批HTTP不是完整无复制流式DataHandle或24小时/工业现场验收。

### HTTP-CLI-06：真正响应体慢流截止新增反证（2026-10-30）

- 用例：Tests/ArchitectureSmoke/Transport/FTRMultiClientBudgetTests.HttpClientBodyDeadline()，由`--channel-uml-core`/`--channel-transport`自动调用；本机HttpListener真实OS回环已发送HTTP 200及首字节，持续2200ms不结束分块响应体。Client配置RequestTimeoutMs=850，必须在2900ms内返回TimeoutException、Received次数为0、FTR临时缓冲额度归0，禁止把未完成响应作为有效数据交付。
- 直接覆盖接收慢Body、取消与资源释放；配合HTTP-CLI-01～05的同门Send重入、受限预算、诊断Payload禁用与继续服务回归。两个入口及Windows全SLN Rebuild/各方向2GiB仍在`.github/workflows/f17-channel-windows.yml`。
- 代码固定增量：`323a4b3b`、`723c10c4`、`d7ec8b6d`、`6ae8fd4b`；**对应最新版固定SHA的Windows CI运行完成并出具日志之前，全部维持“已提交，待自动验收”**。

### HTTP-CLI-07：新建配置下4MiB合法响应的全程真实内存预算（2026-10-30）

- 入口：FTRMultiClientBudgetTests.HttpClientMultiMegabyteResponse；仍自动包含在`--channel-uml-core`和`--channel-transport`。
- FCM：HTTP Client默认MaxResponseBytes=4194304、ReceiveByteBudget=12582912（12MiB），校验Converter往返；不修改既有显式历史持久预算。HTTP Server真实HttpListener回环，发送4MiB确定性字节序列；客户端按长度及全部4194304字节检查完整顺序、单次收包、真实ReceivedBytes、PeakBufferedPayloadBytes<=预算、完成后BufferedPayloadBytes=0。
- CI：`.github/workflows/f17-channel-windows.yml`两套Channel软件入口都必须输出`F17.HTTP_LARGE_PASS bytes=4194304`。保持原TCP/NamedPipe双向2GiB基准与Windows x64全SLN；本项在其固定SHA Actions真正结束前仍是**已提交、待验证**。


### 2026-10-30 F17 Serial P0与消息型无订阅者大报文回归

| 编号 | 真实测试入口 | 验收断言 | 证据 |
|---|---|---|---|
| F17-HTTP-08 | FTRMultiClientBudgetTests.HttpClientMultiMegabyteResponse；原--channel-uml-core/--channel-transport | 真实4MiB HTTP响应在未订阅Legacy Received时仍完整收包计数，7MiB预算不触发额外Context副本，峰值不超预算且结束清零；有订阅者12MiB默认预算仍逐字节完整且独立快照 | `50926a25`、`e1b11deb` 已提交，Windows待验 |
| F17-HTTP-09 | FTRMultiClientBudgetTests.HttpClientBoundedResponseAndReentrantSend | 同一HTTP Client收包回调同步再次Send，小响应在512字节短时重叠预算下不因初始化过大分配假拒绝；256B响应超预算时明确拒绝，之后健康小消息仍可完成 | `e89be960` 已提交，Windows待验 |
| F17-SERIAL-01 | FTRSerialP0Tests.Run由--channel-uml-core和--channel-transport调用 | FCM配置往返、共享总线身份、无硬件连接副作用、非法数据位拒绝、Closed Send拒绝、Open预取消拒绝 | `e597280a`、`faa1a507` 已提交，Windows待验 |
| F17-SERIAL-02 | 同上；Windows真实SerialPort的COM65534不可用路径 | 无COM口时必须Faulted，显式Close可恢复Closed；同实例再次Open失败与清理无永久残留。**不等于实物串口收发** | `e597280a` 已提交，Windows待验 |
| F17-SERIAL-HIL | 现场RS-232/485、长时间半包与慢接收、断线重连、Send/Receive互不阻塞 | 实体测试需上位机/串口仪、线缆和驱动，验证实际后台接收Thread、StopTimeoutMs、数据可靠性、RS-485方向控制及工业负载 | 尚未执行；禁止报告通过 |

原F17 Windows workflow不仅要过F17.HTTP_LARGE_PASS，还必须输出F17.SERIAL_P0_PASS；全部自动回归及两种2GiB大流门禁仍是软件G1条件。CI取消的旧运行不得代替新SHA通过。

### F17-SERIAL-03 流控配置门禁（2026-10-30）

正式FCM配置可选None、XOnXOff、RequestToSend及RequestToSendXOnXOff；新建默认None保持旧行为。FTRSerialP0Tests验证RequestToSend配置往返、非法值拒绝。串口Open使用原生SerialPort.Handshake。源码提交eed989ce、d163a951、d24d70c，Windows固定SHA待验证；RS-232/485设备实际握手、持续流及断线恢复尚未测试。
