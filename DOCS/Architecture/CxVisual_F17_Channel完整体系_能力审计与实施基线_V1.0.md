# Foundation.Transport 完整 Channel 体系：能力审计与实施基线 V1.0

2026-10-27；审计起点 master `b10bbe7053ff333bcd497490c94e581961bb735f`。
本文件承接用户60项完整开发要求，不把一个TCP工作包、配置存在或若干绿灯作为整包完成。
F17唯一Owner与FTR/IFTR命名采用既有下沉裁决；不增加项目、第二套Channel、业务队列或MRK执行器。

## 1. 原始资料及事实等级

- 已核对AGENTS、工程Skill及V1.0规范依赖/编码/构建章节、DOCS/00/01。
- 只读解析DOCS/02 V4.9、03 V2.01、04 V6.4工作簿：Channel Foundation明确缓冲池、健康、背压、重连；04的F_Transport仍含旧候选，唯一Owner由2026-10-27 F17正式裁决覆盖。不修改原工作簿、不把候选类型冒充当前API。
- 正式源：`Foundation/Transport/Channels`，基线56文件、12种内置配置/实现；Connector/Protocol/PointTable/DataMapping仍归Mechanism.Connectivity。
- 已查既有固定SHA Windows证据：`DOCS/Testing/CxVisual_F17_Channel迁移_源码与Windows验收台账_V1.0.md`。新改动须有自己的Windows CI。
- 旧源码：本机`cx-vision-x2` HEAD `8caf9e8cbc6f9e2307a8b4935cc3ad6272712f5e`。`Common/CommonInterfaces/Channels/TcpClientBase.cs`使用独立接收Thread，但连接/发送有共享大锁；`TcpServerService.cs`含无界ConcurrentQueue，不能照搬。
- `Visions/CxCTVisionDetection/CtServiceUtils.ReadLine`复用池化读缓冲、读超时、最大长度防御；EOL/CSV属于Protocol，不能搬到Channel解析。`CxECTStudio/Wire/BulkReadBenchmark.cs`是序列化批量读取计时，不是网络吞吐验收。
- `ReferTo/Channels/TcpClientTransportChannel.cs`已有连接门、代次、重试与生命周期经验。保留为参考；广泛旧通信/大数据路径的逐方法复用审查继续，不声明已复跑生产设备或完成全部旧仓审计。

## 2. 全部通道清单

| 类型 | 基线实码 | 完整目标尚缺或未验证 |
|---|---|---|
| TCP Client | FTRTcpClientChannel | 独立接收、同步发送、实际配置预算与大块持续流为P0；自动重连/长稳仍需闭环 |
| TCP Server | FTRTcpServerChannel、多SubChannel/Generation | 有界多客户端接收模型、发送顺序、全部读者停止协调、大流为P0 |
| UDP Client | FTRUdpChannel；本轮独立接收/同步底层/池化/边界来源/空闲已本机测试 | 持续网络压力、自动恢复与现场 |
| UDP Server | FTRUdpServerChannel；多Endpoint/预算/慢回调/截断拒绝/空闲代次已本机测试 | 真实高负载与网络异常矩阵、现场多机 |
| UDP Multicast | FTRUdpMulticastChannel；独立接收/显式IPv4接口/真实本机组播已测试 | 多网卡/跨PC与IGMP、长时网络异常 |
| Serial | FTRSerialChannel | RS232/485真实设备、恢复、参数有效性、独立接收及取消 |
| NamedPipe Client/Server | 本轮固定后台接收/Accept、逐Peer同步发送、池化、真实跨进程双向2GiB及12对端已本机测试 | 最终SHA远端CI、长期及多机SMB/权限环境矩阵；完整体系其余项不因本类通过而完成 |
| HTTP Client | 已有HttpClient配置及实现 | 大数据上传下载/流式读取、取消、独立接收交付 |
| HTTP Server | 无正式实现 | 监听、请求响应、上传下载和流式预算、生命周期、真实回环 |
| WebSocket Client | 已有RFC6455基础实现 | 完整消息/连接管理、大消息分片、控制帧、取消与资源预算 |
| WebSocket Server | 无正式实现 | 真正Server收发、握手、分片、控制帧、多客户端及关闭 |
| SharedMemory | 无正式实现 | MMF版本/长度/序列、跨进程同步、所有权、崩溃恢复与大块验证 |
| Virtual | 已有CreatePair和原始字节收发 | 确定性故障注入、有界收发及离线异常矩阵 |
| Replay | 已有MCCRPL01回放、禁止发送 | 录制/回放完整生命周期、速度/取消/损坏及大文件压力 |
| Custom | 基线只有配置；本轮增量已有Factory/Provider、真实TCP扩展及Connector集成 | 正式注册凭据/能力校验已本机测试，远端CI待固定SHA；各专业Provider仍需各自验收 |

## 3. 14方面完成度与缺口

| 方面 / 用户条目 | 已有基础与当前差距 | 完成证据要求 |
|---|---|---|
| 通道类型 23 | 12种内置实现；上述缺少3种实现及Custom Provider | 每类实际收发、正常/异常/停止/恢复 |
| 统一模型 24 | Channel、SessionId、Address、SubChannel、Topology、Capability、Descriptor已有；Factory缺 | 唯一契约、发现/创建、跨Owner集成 |
| 生命周期 25 | Open/Close/Opening/Faulted和部分清理已有；释放/通知需补 | 状态事件、Dispose、并发/失败无泄漏 |
| 连接管理 26 | 显式Reconnect和退避配置已有；自动重连/心跳/保活未完整运行 | 确定性断开重连、次数/退避/取消；不发明业务心跳 |
| 数据收发 27 | 异步与同步包装已有；部分真同步底层、切片及流式缺 | 同步/异步共底层、双向/顺序/请求响应 |
| 缓冲队列 28 | 诊断证据有界；生产接收容量/字节预算原先未使用 | 池额度覆盖接收/排队/回调/Retain，过载有反证 |
| 超时取消 29 | 部分配置/取消已有；TCP连接超时原先未使用 | 连接/写入含排队/读取空闲/可靠排空、超时不伪称停止 |
| 并发多端 30 | Generation及路由已有；TCP原先每Peer新Task | 固定接收线程数、读者协调、逐Peer发送顺序 |
| 可靠容错 31 | 打开失败清理、Peer清理已有；策略未完整联通 | 自动恢复/故障分类/隔离/上限、禁止未知动作重发 |
| 诊断监控 32 | 独立计数器和有界证据已有；运行快照未充分接入 | 真吞吐/池水位/队列/错误/延迟、TestCenter可观察 |
| 安全通信 33 | 新TLS/证书/认证按用户要求暂缓 | 保留现有地址/Generation/预算/输入及权限保护 |
| 配置扩展 34 | FCM多态白名单、迁移和UI已有 | 参数真正作用、Factory/Provider、旧配置正反回归 |
| 高性能 35 | 旧版读循环临时数组+上下文复制+Data复制+证据复制 | 池化/借用切片、大图/体数据、CPU/GC/内存/长稳实测 |
| 测试仿真 36 | 真实Socket/Pipe/HTTP/WS基础回环和Virtual/Replay已有 | 故障注入、持续流/弱消费者/压力、专项CI和GUI |

## 4. 收发与所有权合同（P0）

TCP使用独立后台接收线程；Server以一个Socket.Select接收线程按批服务多客户端，不按连接无限增Thread/Task。另一个有界交付线程只负责发布接收块，复杂业务仍交给既有MRK/F19有界机制。

同步Send在连接级发送门内直接Write，接收不持有发送门；异步Send对调用方数据先取独立快照，复用同一同步Write底层。发送超时覆盖发送门等待和所有分块，取消可能关闭连接且不证明远端未接收，不自动重发。

池预算为`min(QueueCapacity,ReceiveByteBudget/实际块大小)`，额度包含读取中、排队、交付中和后级保留的块。额度耗尽阻止继续Socket读取，TCP窗口形成真实背压，不静默丢可靠字节；不会在等待额度时先分配大数组。

`BufferReceived`回调只借用`FTRReceiveBuffer`，Segment是租约期内的只读约定视图。后级跨线程/排队必须Retain，完成后Dispose。最后一个租约才把数组归池；Close后尚有外部租约仍保持预算与内容有效。旧Received.Data继续隔离复制，兼容其持有快照语义。高吞吐可关闭诊断载荷，但真实字节计数保持。

先停止OS读取并加入接收线程，再排空已接纳的数据与Peer终止通知；不能在已排队字节前Reset Protocol。慢回调在停止预算内未结算必须明确超时/Faulted，禁止伪报Closed或回收仍被引用的数组；回调内等待自身Close明确拒绝。

## 5. 原60项要求的追踪与持续顺序

1—6：入口、源码/版本/CI、旧仓复用与Owner审计，持续补充旧源码逐项证据。
7—13、37—44：独立接收/同步发送与大流P0优先；不得以本机8MiB例子代替大图、体数据或长稳。
14—22：逐项完成第2节全部通道；已有实现只补缺，不重写成熟功能。
23—36：第3节逐方面验收，包括新增安全暂缓这个明确边界。
45—51：FTR唯一传输Owner；Protocol、MRK/业务执行和专业SDK保持原Owner；配置参数和共底层需实际测试。
52—60：本审计→真实增量/专项测试→大流/弱端→TCT操作和诊断→Windows x64专项/默认回归→架构/实施/测试台账→中文提交及固定SHA结果。

后续完整交付仍包括：所有其它通道收发模型与资源预算、HTTP/WS Server及SharedMemory、Factory/Provider、自动重连/心跳边界、TestCenter运行观察、全部配置有效性、长时及硬件边界。以上未完成项保持未完成，不将当前P0工作包改名为整包完成。

## 6. Factory/Provider正式增量

`FTRChannelFactory`与`IFTRChannelProvider`属于同一F17 Owner；内置11个单实例类型和显式Virtual对创建、12类型发现、Host作用域注册及注销已实现。Custom配置仅通过正式FCM白名单，SettingsJson为最多65536字符的JSON对象，不据TypeId反射加载代码。弱键创建凭据绑定实例、原配置与能力；未注册、跨Factory、错Id/拓扑、复用已交付实例、创建途中注销均拒绝。Provider在注册锁外运行，旧令牌不能删除同名新注册；注销不夺取现存实例所有权。

Connectivity装配复用F17凭据/类型校验，Protocol能力兼容仍由原Owner判断；没有复制Channel类型体系。真实Custom TCP Connector收发与上述正反例已进入原`--channel-transport`及默认回归。详见[Factory/Provider实施与验收](../Implementation/CxVisual_F17_Channel_Factory与Provider_实施与验收_V1.0.md)。本增量不补全其它通道、自动恢复、全部TestCenter操作及长期/硬件验收。

UDP独立接收、同步发送、有界数据报与来源/代次的正式增量见[UDP实施验收](../Implementation/CxVisual_F17_Channel_UDP独立接收与组播_实施与验收_V1.0.md)。保留UDP固有丢包语义，不把池等待宣称为端到端可靠背压。

NamedPipe固定后台线程、同步底层、跨进程体数据、逐Peer隔离和终止元数据额度增量见[管道实施](../Implementation/CxVisual_F17_NamedPipe独立接收与跨进程大数据_实施验收_V1.0.md)及[验收矩阵](../Testing/CxVisual_F17_NamedPipe跨进程大数据_验收矩阵_V1.0.md)。本轮还使TCP/UDP/管道在首次空闲故障之前登记接收线程身份，终止通知等待统一携带停止Token。

## 7. P0重连事件回调停止可靠性（2026-10-28）

- **对应14能力第9项「可靠容错」及第7项「超时取消」的局部完成证据**：恢复控制器同步事件 `AttemptFailed` / `Reconnected` 回调中再次执行 `StopAsync` / `Dispose`，旧版本可能等待其自身执行的任务而死锁。
- 现已在既有 `FTRReconnectController` 内进行修复：回调内仅取消并延后完成任务/取消源清理；回调外仍严格等待；旧代尚未完全停止时不得重启维护。没有重发协议或业务命令，也没有建立第二个Transport Runtime。
- **固定源码验证**：回归提交 `4d9976ee74560115dfcbcd6e76a7205c33372e5a`，Windows F17 [#40657016715](https://github.com/oudream/cx-visual-x3/actions/runs/40657016715) success，日志包含 `F17.RECONNECT_PASS ... callback-safe stop`、`F17.UML_CORE_PASS` 和双向各2GiB完整性结果。
- **剩余缺口不变**：其它Channel跨会话恢复、断网/崩溃、HTTP/WS慢客户端、SharedMemory不确定槽位、24h长稳、跨PC/HIL、所有端点并发取消场景仍属待验收。此项不得计为14项整体完成。


## 8. V3.0 六项重点能力统一增强（2026-10-28）

本节是在既有F17唯一Channel Owner、核心UML及14项能力台账上做的**增量**，不是另起框架。六个能力：29超时取消、30并发多端、31可靠容错、32诊断监控、34配置扩展、35高性能；其实施顺序可以归并为五组，但需求仍严格保持六项。

### 8.1 OOP / GoF / AOP 责任边界

- **OOP**：现有 `FTRChannelBase` 保持统一生命周期/收发/诊断模板，`FTRSingleChannelBase`/`FTRMultiChannelBase` 负责端点形态，具体TCP/Pipe/HTTP/WS各自持有唯一物理I/O。
- **GoF**：Template Method使用现有 `OnOpenAsync`/`OnCloseAsync`/`OnSendSynchronous`；Factory/Provider、Adapter、Observer和可组合故障决策沿用现有对象，不制造模式空壳。
- **AOP**：采用显式公共执行边界记录Deadline、入场拒绝、错误阶段及观察者异常；不引入反射动态代理/字节级拦截器，不在数据热路径增加逐字节日志。
- **跨Owner纪律**：FTR只判断物理故障与连接是否重试；MPA唯一组帧/协议事务；MDS唯一DataHandle；MRK唯一任务调度。超时/取消/断连后远端业务结果可能未知，绝不由FTR重复命令。

### 8.2 本轮代码与六项能力状态

| 项目 | 已提交的有效增量 | 距完整工业验收仍缺 |
|---|---|---|
| 29 | `FTRDeadline` 单调截止时间用于TCP同步分块发送和排队；Open/Close/Send分类计入诊断 | 其它类型全路径Deadline和取消后物理资源结算矩阵 |
| 30 | WebSocket Client/Server按整条Message持有发送门、按分片发送，测试同Peer多消息不交错 | HTTP/WS慢端、多Peer资源公平、停机竞态与长稳 |
| 31 | `FTRFailureInfo`/`FTRFailureClassifier`；已有`FTRReconnectController`遇权限/非法配置/资源拒绝停止盲目重试，提供累计成功失败与恢复耗时 | 所有类型恢复动作差异、心跳边界、真实网络/HIL |
| 32 | `FTRChannelDiagnosticsSnapshot` 增加类型化最近故障与采样速率；`FTRChannelBase.Health` 暴露只读状态/会话/Peer/原队列；观察者异常标记Observer/Operation | TestCenter实时图表、统一持久历史、Peer级与GC/CPU实时SLA |
| 34 | 沿用已有FCM、Factory/Provider、Capability，不修改身份/配置Schema | 参数逐类型真实生效和完整专业Provider样例 |
| 35 | 异步快照入场被拒绝时计入ResourceExhausted故障；WS发送分片无整条输出拷贝；保留已有池租约/2GiB门禁 | HTTP/WS接收聚合复制、SharedMemory跨重启、CT/图像到MDS的真正低复制与24h资源SLA |

`FTRChannelHealthSnapshot` 暂只作为`FTRChannelBase`公开附加视图，不强制扩展`IFTRChannelTransport`，避免破坏已有第三方Provider实现。健康视图仅表示本机证据；Open不证明远端业务成功，速率采用快照窗口平均值。

### 8.3 本轮提交/验证边界

- `1be8688b`：Deadline、故障分类、速率和Health；`4cb84aa1`：专门的故障/快照回归；对应[Windows F17 #40660269567](https://github.com/oudream/cx-visual-x3/actions/runs/40660269567) success。
- `321b0525`：可恢复性判断、物理重连结果统计和负面测试；对应[Windows F17 #40660492860](https://github.com/oudream/cx-visual-x3/actions/runs/40660492860) success。
- `0892287d`、`c832d99c`、`a0ac099f`、`b6123dc2`：WS双向消息级原子分片和OS回环；`d54928fc`：入场故障分类；`61776e5c`：观察者隔离。**本组较新提交的Windows结果以其对应CI最终状态另行补录；绝不借用更早绿灯**。


### 8.4 追加的资源、配置和TestCenter代码闭环

- `d54928fc`将异步发送入场拒绝记录为`FTRFailureKind.ResourceExhausted / SendAdmission / Operation`，保留原`InvalidOperationException`兼容行为；[F17 Windows #40661070975](https://github.com/oudream/cx-visual-x3/actions/runs/40661070975) success。
- `61776e5c`将状态/数据/帧观察者异常统一标记`Observer / Operation`，避免误报为底层Receive故障。
- `0ddd1f4c`抽离`ConfigureSendAdmission`：原接收池继续复用，HTTP Client/Server、WS Client/Server和Serial无接收池时也执行异步发送数量/字节快照入场预算；WS真实回环新增4097字节超过4096预算时必须拒绝、且同步较大消息仍正常的正反例。
- `489c7f3b`在`FTRReconnectController`构造时冻结实际重连次数、间隔、退避；调用方修改原FCM配置对象不能在运行中悄悄改变策略。
- `bb855563`、`1efbe549`把原TestCenter人工Channel页面接入真实FTR只读健康视图/每秒UI采样，展示状态、Session、收发速率、故障类别、池和背压；`4c398047`补原WPF UIA真实Virtual收发后的健康信息断言。无设备授权时仍只离线选择类型。
- 截至记录时，较新的`489c7f3b` F17和TestCenter Windows完整回归**仍需以其各自固定SHA完成结果验收**；已有WS原子性[专项#40660867450](https://github.com/oudream/cx-visual-x3/actions/runs/40660867450)真实日志已输出`F17.WS_ATOMIC_PASS`，不能替代后续资源预算/界面提交的回归。
