# F17 UDP独立接收、有界数据报与组播实施验收 V1.0

2026-10-28。承接已通过Windows CI的TCP P0和Factory增量，继续完整Channel目标的UDP Client/Server/Multicast，不增加项目、外部依赖、业务分帧或第二执行体系。

## 正式Owner与行为

- 原`FTRUdpChannel`、`FTRUdpServerChannel`、`FTRUdpMulticastChannel`各用一个专属后台接收Thread，原接收Task/逐报文临时数组移除。共用`FTRUdpIO`同步Socket读取与发送，异步Send在原有快照入场预算后调用相同同步底层，不另写一套协议。
- 复用P0的接收池与有界交付线程；每个数据报对应一个池块，保留边界、空报文和真实来源。`FTRReceiveBuffer.RemoteEndpoint`与原`FTRChannelReceiveContext.RemoteEndpoint`同时可见，Retain保留来源和租约所有权；TCP原字节流语义不变。UDP允许合法零长度发送，TCP等原不允许的类型仍拒绝。
- 同步切片直接`Socket.Send/SendTo`，不会为整块创建副本；发送门的等待和写入共用总期限。异步仍先受数量/字节预算，再复制；取消中的Socket关闭会使接收故障，不把取消等同远端未接收，不自动重发。65507字节以上报文与将UDP当流拆段的请求明确拒绝。
- Windows的`MessageSize`错误表示原报文超过配置池块，整条丢弃并诊断，不交付截断片段；Server超过MaxClients或来源处于代次结束过程中也明确诊断。单Peer的ICMP reset不杀死Server监听。其它不可恢复的Socket错误仍进入Faulted。
- UDP Server以真实来源Endpoint登记唯一SubChannel，MaxClients有实际上下限；空闲超时排入同一FIFO，在旧数据/回调后移除和通知Protocol。目录和来源表在同一门内原子清理，外部观察器在门外通知；异常观察器被诊断且不阻断后继通知。重新出现的同Endpoint取得更高Generation。
- UDP Client/组播空闲可配置为Faulted，Server空闲只清理相应Peer；Stop取消Socket读取、加入接收线程、等待发送、排空交付，共用一个StopTimeout。回调未结算必须超时/Faulted；外部Retain跨Close仍有效，最终Dispose才释放其预算。
- 组播新增正式FCM字段`LocalInterface`（默认0.0.0.0，兼容原系统默认路由），指定IPv4接口参与加入组和发出组播；保留组地址/TTL/Loopback设置、离组/Close/重开。没有新增TLS/认证。
- `FTRTransportThread`只抽取原TCP的截止时间和Join代码，TCP/UDP复用，未增加调度或业务队列。

## UDP可靠性与资源边界

池额度覆盖读取中、排队、回调和外部保留的数据，受QueueCapacity和ReceiveByteBudget双重限制。池满时停止本机Socket读取，`BackpressureWaits`在UDP表示**本机池等待次数**，不表示UDP发送方得到TCP式可靠流控。OS socket缓冲、网络丢包/乱序/重复仍是UDP固有边界；没有序号协议便不能测出所有内核/网络丢包。显式诊断只覆盖软件可观察的截断、逻辑对端拒绝、Socket错误和本机池水位，不能冒称精确全网丢包率。可靠业务必须由原Protocol Owner处理，不在Channel内擅自分包或重发。

## 可执行测试与证据

`Tests/ArchitectureSmoke/Transport/FTRUdpThreadTests.cs`进入原`--channel-transport`及默认回归：

| 方法 | 真实行为/反例 |
|---|---|
| DatagramBoundariesAndSource | UDP Client/Server完整0/1/17/4096/65507字节报文与反向回送、异步入口、来源兼容、独立线程/池上限/计数；超大与流式拆包拒绝 |
| TruncationAndPeerBudget | 8字节接收块接收100字节报文，必须诊断且不交付/注册；下一完整报文仍可用；MaxClients=1拒绝第二真实来源且监听继续Open |
| SlowConsumerStopAndLeases | 两块32字节池、真实池等待、慢回调StopTimeout而非假Closed；回调放行后重试Close，跨Close租约/来源保持，最终归还释放 |
| IdleGenerationAndObserver | 接收回调阻塞时空闲通知只能排队，不能提前Reset代次；观察器故意异常不阻断后继清理；同Endpoint重现代次增加；Client空闲真正Faulted |
| MulticastLoopback | 127.0.0.1接口上真实IPv4组播加入/发送/接收、来源与独立线程、Close/重开及FCM往返 |

本地Windows .NET48/x64完整方案Build成功、通道专项exit0并输出`F17.UDP PASS`；原TCP/NamedPipe/HTTP/WS/Replay/Connector、Factory与40MiB双向测试仍通过。默认ArchitectureSmoke exit0；共用池/线程期限的双向各2GiB回归exit0（7123ms，含生成和逐字节校验，两端接收池各262144字节）。远端CI按最终固定SHA补录。多网卡、跨PC、交换机组播/IGMP、防火墙策略、长时网络压力及真实外设仍未验收；本机组播不是工业现场证明。

完整目标仍包括其它通道后台接收/有效预算、HTTP/WS Server、SharedMemory、自动恢复/保活、完整TestCenter操作与诊断及长期/HIL，保持继续开发。
