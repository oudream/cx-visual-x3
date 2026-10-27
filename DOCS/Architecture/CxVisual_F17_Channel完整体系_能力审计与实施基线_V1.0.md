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
| UDP Client | FTRUdpChannel | 独立接收、过载可诊断、超时取消、持续流/网络压力 |
| UDP Server | FTRUdpServerChannel | 多Endpoint、资源预算、慢消费者/异常生命周期 |
| UDP Multicast | FTRUdpMulticastChannel | 多网卡真实组播、组成员生命周期、独立接收和网络异常 |
| Serial | FTRSerialChannel | RS232/485真实设备、恢复、参数有效性、独立接收及取消 |
| NamedPipe Client/Server | 已有真实OS Pipe实现 | 后台接收/同步发送统一、双进程大流、多客户端、慢端与停止 |
| HTTP Client | 已有HttpClient配置及实现 | 大数据上传下载/流式读取、取消、独立接收交付 |
| HTTP Server | 无正式实现 | 监听、请求响应、上传下载和流式预算、生命周期、真实回环 |
| WebSocket Client | 已有RFC6455基础实现 | 完整消息/连接管理、大消息分片、控制帧、取消与资源预算 |
| WebSocket Server | 无正式实现 | 真正Server收发、握手、分片、控制帧、多客户端及关闭 |
| SharedMemory | 无正式实现 | MMF版本/长度/序列、跨进程同步、所有权、崩溃恢复与大块验证 |
| Virtual | 已有CreatePair和原始字节收发 | 确定性故障注入、有界收发及离线异常矩阵 |
| Replay | 已有MCCRPL01回放、禁止发送 | 录制/回放完整生命周期、速度/取消/损坏及大文件压力 |
| Custom | 只有配置且未注册会拒绝 | 正式Factory/Provider注册、能力发现、重复/失效令牌及扩展测试 |

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
