# F17 Channel P0：独立接收、同步发送与有界大数据实施记录 V1.0

2026-10-27；起点`b10bbe7`，在原FTR通道上增量改造，没有新项目/第三方包/Mechanism反向依赖。
完整范围与未完能力见[14方面审计](../Architecture/CxVisual_F17_Channel完整体系_能力审计与实施基线_V1.0.md)。本工作包不等于完整Channel目标已完成。

## 正式实现

| Owner/源文件 | 实际行为 |
|---|---|
| FTRTcpClientChannel | 独立后台Thread执行阻塞Read；ConnectTimeoutMs实际控制连接；接收空闲超时；Open调用者Token不成为长期运行Token |
| FTRTcpServerChannel | 一个Socket.Select接收线程；1024个Socket为批轮转，多客户端不逐个新建Thread/Task；Peer终止通知排在其已接纳字节后，Protocol仍是唯一分帧Owner |
| FTRTcpIO | 单连接发送门与同步Write，接收不持有发送门；发送超时覆盖排队和全部分块；同步切片不整块复制，异步发送独立快照后复用该Write |
| FTRReceiveBuffer/FTRReceivePipeline | 固定字节/块数量额度，先取得额度才读Socket；后台有界FIFO交付；Retain/Dispose跨线程共享池块，最后一份租约释放才归还 |
| FTRChannelBase/现有IFTR契约 | 实际诊断计数、状态事件、低复制BufferReceived、同步目标切片、Dispose；TCP异步发送快照数量/字节额度与拒绝计数；保留旧Received.Data快照与ReadFrames证据界限 |
| FTRReceiveQueueSnapshot/原DiagnosticsSnapshot | 池容量、实际分配/借用/峰值、队列、背压等待、收发字节及独立线程身份；不是业务执行器或第二数据堆 |
| FTRChannelConfiguration/原Converter | ReceiveBufferBytes、CapturePayloadEvidence、SendTimeoutMs、StopTimeoutMs、ReceiveIdleTimeoutMs、SendQueueCapacity、SendByteBudget的FCM注解、旧格式迁移和Kind切换保持 |
| TestCenter原ArchitectureSmokeProfiles | 原11入口保留，增加--channel-transport、--channel-bulk、--channel-bulk-long三个正式CLI配置；沿用进程隔离/有界输出/取消和测试控制台，没有另建测试框架 |

## 所有权、停止与兼容

- Segment只在租约有效期内供只读访问；不得修改数组或在释放后使用。后级排队/跨线程参与者分别Retain并最终Dispose，同一租约不能与读取并发Dispose，保留租约持续占用原池额度。原Received仍给隔离快照，不因低复制API改变其持有期。
- 关闭先取消/关闭OS读取并等待接收线程，再在同一停止时限内排空交付；不能回收慢回调或外部仍持有的内存。未结算明确超时/Faulted，释放回调后可重试Close。成功Close清空空闲池，只保留外部租约实际占用。
- 接收/交付回调内等待自身Close明确拒绝；订阅者异常计入诊断而不杀死后继订阅者或IO。Frame/状态观察器异常不能破坏传输生命周期。
- 显式移除SubChannel后拒绝旧地址；正常EOF则先交付已接纳字节再移除代次并清理Protocol。离线目录登记/移除保留TestCenter既有行为。
- TCP异步发送在复制/任务入场/实际Write前同时检查快照数量和字节额度；满额或单次过大明确拒绝并增加RejectedSends，不创建无限ThreadPool任务和数据副本。同步切片借用调用方数据，适合大图/体数据分块；异步大块需要匹配预算或由调用方分块。Close在同一停止时限内等待已入场发送结算，不把取消/超时当成远端未执行。
- IFTRChannel新增同步切片/Dispose，IFTRChannelTransport公开BufferReceived/状态/诊断。当前仓内实现均来自同一FTRChannelBase并整体重编译；外部实现/二进制客户端需要重编译适配，不声称接口二进制兼容。
- 新接收/写入预算本包在TCP真正生效；其它旧通道的独立接收、预算、自动重连等仍需后续逐类闭环。旧IoMode为兼容序列化保留，TCP同步/异步入口均使用同一独立接收和同步Write路径，不声称已提供两套独立IO引擎。

## 验证入口

Windows .NET Framework 4.8/x64：完整方案编译；`ArchitectureSmoke --channel-transport`包含原有全通道/Connector与新增短大流；`--channel-bulk`独立运行40MiB双向及异常；`--channel-bulk-long`运行双向各2GiB；默认回归与`--concurrency`保留。

新增真实反证：逐字节/顺序、12对端精确回送、接收/交付独立后台线程、两块预算下慢消费者与反向发送、Retain跨Close/重复Dispose、异常订阅者和状态观察器隔离、慢回调Close不得假Closed、原有通道接口兼容、并发分块Send不可交错、异步输入快照/容量及字节满额不得调用底层、取消Open Token后持续可用、重连与Dispose禁止重开、Client/Server空闲超时、FCM往返/非法参数。

专项CI替换旧的“固定56文件”断言为更强的逐源文件唯一Compile/名称/Owner核对，保留至少56个已有源文件、原通道回归和全SLN Rebuild；新增必须实际传完双向2GiB并输出字节数与CPU/GC/内存/池指标的门禁，日志保留artifact。

本地初测：40MiB双向逐字节校验成功，接收池各262144字节；2GiB双向校验成功，8827ms端到端、CPU36718.8ms、managed delta15944536、working set44302336、GC0=14/GC2=1，接收池仍各262144。该计时包含数据生成/逐字节校验，是本机软件测量，不是工业SLA、网络带宽认证或长期稳定性验收。正式Windows CI只能按后续固定源码SHA的真实运行结果记录。

## 未完（继续执行完整目标）

其余通道统一收发/所有预算、HTTP/WS Server/SharedMemory实装、Custom Factory/Provider、自动重连/保活、细粒度故障分类、TestCenter全部通道人工操作、持久录制/故障仿真、长时压力与RS232/485/多网卡/跨PC等HIL。新增TLS/证书/身份认证按用户要求暂缓，既有保护未移除。
