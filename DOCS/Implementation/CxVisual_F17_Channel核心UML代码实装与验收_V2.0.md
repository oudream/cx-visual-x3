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
