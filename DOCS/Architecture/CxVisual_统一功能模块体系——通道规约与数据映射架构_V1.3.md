# CxVisual 统一功能模块体系——通道规约与数据映射架构 V1.3

> 2026-10-23；本文件覆盖 V1.2 中 Connector 默认一组、Advanced 可多组的旧决定。五类模块 Connector/Driver/Compute/Simulation/Service 不变；ReferTo 只读；只使用 FCM/MHS 正式配置。源代码、CI、实机验收分别记录。

## 1. 不可变的架构规则

1. Connector 恰有一个 Channel 和一个 Protocol：MCNConnectorConfiguration.Channel / .Protocol。都必须存在。无解析需求用正式 MPAEmptyProtocol，不用 EmptyChannel；不再持有 Channels/Protocols 集合和 Standard/Advanced。
2. Channel 的单对端/多对端分别由 MCCSingleChannelBase、MCCMultiChannelBase 管理，均继承 MCCChannelBase。TcpServer/UdpServer 多客户端属于内部 SubChannel、Generation；RS-485 一条串口共享总线，站号属于 Protocol.Points。
3. Protocol 负责 ByteStream 分帧、读写、发送、数据解释、会话、点表和映射。MPAProtocolBase/MPAEmptyProtocol/MPALengthFrameProtocol；按 SubChannel 隔离帧解析，不在 Channel 预解析报文。
4. 唯一规约配置 MPAProtocolConfiguration：类型、执行参数、Points、Mappings。MPAPointDefinition 只有规约地址/站号/类型/权限，MPAPointTable 是运行索引而非配置镜像。
5. MPADataMapping 独立于 Point，支持一对多目标。TargetKind 为 Variable/DataHeap/DataStack/Message/Command/ProfessionalData；Direction 为 Inbound/Outbound/Both；Operation 为 ReadValue/WriteValue/PublishMessage/InvokeCommand。出站必须明确唯一源。
6. MPADataMappingExecutor 执行映射，IMPADataTargetAccess 是现有 Owner 行为契约，MDSProtocolDataTargetAccess 直接访问 MDS Variable/Heap/节点 DataContext。消息和命令必须交给已有真实 Owner；未注入时明确拒绝，不增加 MessageManager/CommandManager 或虚假成功。
7. Driver 独立管理厂商 SDK、采图、运动等专业职责，不要求外部 Channel；专业内部资源可复用 Channel/Protocol，但不强迫工程用户配置多条链路。
8. Channel 异步收发、Protocol 可以同步解析；并发异步工作由已有 F19/MRK 负责，不另造调度；工业命令受授权/安全联锁约束。

## 2. 核心接口与主要方法

| Owner | 类型 | 方法或属性 |
|---|---|---|
| Channel | IMCCChannelTransport, MCCChannelBase | OpenAsync, CloseAsync, SendAsync, Received, State |
| Channel | MCCSingleChannelBase / MCCMultiChannelBase | Receive / RegisterPeer, RemovePeer, FindSubChannel, SnapshotSubChannels, SendToPeerAsync |
| Channel | MCCSubChannelAddress / Directory | ChannelId, SubChannelId, Generation, Register/Remove/Find |
| Protocol | IMPAProtocol / MPAProtocolBase | PrepareSend, ProcessReceived |
| Protocol | MPAEmptyProtocol / MPALengthFrameProtocol | 透传；按对端分帧、Reset |
| Protocol | MPAProtocolConfiguration / MPAPointTable | Validate, ValidateCompatibility, Add/Find/Remove/Snapshot |
| Protocol | MPADataMappingExecutor | ApplyInboundAsync, ReadOutboundAsync |
| MDS | MDSProtocolDataTargetAccess | ReadAsync, WriteAsync, PublishAsync, InvokeAsync |
| Connector | MCNConnectorModule / Configuration | Channel, Protocol, Validate, ValidateProtocolCapabilities, SendPayload |

## 3. 验收反例与边界

- 配置不允许缺少 Channel/Protocol，允许 Empty Protocol；Modbus TCP/RTU 必须匹配 TCP/Serial；非法站号、重复点、悬空映射必须拒绝。
- Virtual Raw 的 ByteStream 分段交给 LengthFrame Protocol 解析；Channel 不把发送成功当成规约事务完成。
- MDS Variable、Heap、DataStack 活动帧须能够做真实写入及读取；Message/Command 尚无 Owner 时返回明确 NotSupported。
- UI 仍由 FCM 注解生成，TestCenter/Studio 不重建配置体系；正式源码回归、Windows CI 和真实硬件验收独立记录。

## 4. 版本迁移要求

旧 CxVisual.Module.Connector SchemaVersion 1 的 Channels/Protocols/Points 集合与新 SchemaVersion 2 的 Channel/Protocol 单数配置不自动兼容。必须明确多组旧数据拆分策略；不可静默丢失或自动假成功。不得建立长期 EditableConfig/LegacyAdapter。旧 MCCVirtualChannel 内含长度帧解析的测试兼容路径须另行迁移后删除；正式新测试路径用纯传输 MCCVirtualRawChannel + MPALengthFrameProtocol。

## 5. 传输实例的增量实装（Windows CI/HIL 分级）

- 新增 MCCTcpClientChannel、MCCTcpServerChannel：TcpClient、TcpListener/NetworkStream 异步收发；Server 为一个父 Channel + N 个 Generation 子通道，断线移除，不为每个对端创造 Connector。
- 新增 MCCUdpChannel、MCCUdpServerChannel：UdpClient、数据报边界、远端 Endpoint 对应逻辑子通道；UDP 不模拟 TCP Accept。
- 新增 MCCSerialChannel：SerialPort 单物理总线，读写与 Protocol StationAddress 分开；需要设备现场与驱动权限回归。
- Connector 通过实例真实类型与配置 Kind 对应，运行时 StartAsync/StopAsync、PayloadReceived、SendPayloadAsync 支持按实际 SubChannel 路由；没有专业规约的读写行为不能凭空宣称已实现。
- ArchitectureSmoke 增加 TCP、UDP loopback 真 Socket 正反测试（多对端、路由、旧 Generation）。**Loopback != 工厂 PLC、RS-485、采图设备 HIL**；全部以最新 SHA 的 Windows 工作流结果为准。
