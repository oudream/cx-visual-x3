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

旧 CxVisual.Module.Connector SchemaVersion 1 的 Channels/Protocols/Points 集合与新 SchemaVersion 2 的 Channel/Protocol 单数配置不自动兼容。必须明确多组旧数据拆分策略；不可静默丢失或自动假成功。不得建立长期 EditableConfig/LegacyAdapter。旧 Virtual 内部长度帧解析已经迁移到 MPALengthFrameProtocol；TestCenter 与 ArchitectureSmoke 均采用唯一的纯传输 MCCVirtualChannel，不保留第二个 Virtual 类或 MCCCxChannelSession。

## 5. 传输实例的增量实装（Windows CI/HIL 分级）

- 新增 MCCTcpClientChannel、MCCTcpServerChannel：TcpClient、TcpListener/NetworkStream 异步收发；Server 为一个父 Channel + N 个 Generation 子通道，断线移除，不为每个对端创造 Connector。
- 新增 MCCUdpChannel、MCCUdpServerChannel：UdpClient、数据报边界、远端 Endpoint 对应逻辑子通道；UDP 不模拟 TCP Accept。
- 新增 MCCSerialChannel：SerialPort 单物理总线，读写与 Protocol StationAddress 分开；需要设备现场与驱动权限回归。
- Connector 通过实例真实类型与配置 Kind 对应，运行时 StartAsync/StopAsync、PayloadReceived、SendPayloadAsync 支持按实际 SubChannel 路由；没有专业规约的读写行为不能凭空宣称已实现。
- ArchitectureSmoke 增加 TCP、UDP loopback 真 Socket 正反测试（多对端、路由、旧 Generation）。**Loopback != 工厂 PLC、RS-485、采图设备 HIL**；全部以最新 SHA 的 Windows 工作流结果为准。

## 6. 对照架构表的实际类与行为（V1.3 实施增补）

| 分类 | 正式对象 | 代码情况与约束 |
|---|---|---|
| Channel | IMCCChannel, IMCCChannelTransport, MCCChannelBase | 同步兼容 + OpenAsync/CloseAsync/SendAsync、接收事件、生命周期、帧证据；真实网络使用新基类 |
| Channel | MCCSingleChannelBase | 单目标 Send/Receive，显式 ReconnectAsync；重试/退避按 MRK 现有能力执行 |
| Channel | MCCMultiChannelBase, IMCCMultiPeerChannel | 多对端登记、移除、按精确 Generation 路由，拒绝陈旧地址 |
| Channel | MCCTcpClientChannel, MCCTcpServerChannel | .NET TcpClient/TcpListener + 异步流收发；Server 一条父通道、多 TcpClient |
| Channel | MCCUdpChannel, MCCUdpServerChannel | 保留 UDP 报文边界；多 Endpoint 归属同一父通道和 SubChannel |
| Channel | MCCSerialChannel | SerialPort 单一共享总线；多站号配置归 Protocol |
| Channel | MCCVirtualChannel | 原始数据透传；旧内置长度帧解析已直接迁出到 MPALengthFrameProtocol，不再保留第二个 Virtual |
| Channel | MCCSubChannel, MCCSubChannelAddress, MCCSubChannelDirectory | 子通道活动状态、引用、SendAsync 和 Register/Find/Remove；不拥有第二套 Socket |
| Protocol | IMPAProtocol, MPAProtocolBase | 统一 ProcessReceived / PrepareSend / ReadAsync / WriteAsync / SendAsync；Read/Write 是映射的数据行为，不代表远端 Modbus 已响应 |
| Protocol | MPAEmptyProtocol, MPALengthFrameProtocol | 真透传或按 SubChannel 的分帧解析和 CRC16 |
| Protocol | MPAModbusTcpProtocol, MPAModbusRtuProtocol, MPAModbusRegisterOperations | MBAP/RTU 请求帧构建、03/06 响应分帧与校验、站号、CRC16；其他功能码和完整事务轮询待专项完成 |
| Protocol | MPAProtocolSession, MPAProtocolContext | 生命周期、CorrelationId 关联、请求 Timeout/Cancel、子通道路由、数据作用域引用 |
| Protocol | MPAProtocolConfiguration, MPAPointDefinition, MPAPointTable | 唯一规约配置及点地址、站号、数据类型、读写属性；点表为运行索引 |
| Mapping | MPADataMapping, MPADataMappingCollection, MPADataValueTransform | 单点多映射、来源/目标、读写方向与操作、类型转换、查找/增删/校验 |
| Mapping | MPADataMappingExecutor, IMPADataTargetAccess | 入站多目标操作、出站唯一数据源、严格异常传播与必要性策略 |
| MDS | MDSProtocolDataTargetAccess | Variable/Heap/节点 Frame 真读写；Message/Command 挂接实际 Owner 回调，Command 额外要求显式授权检查；无 Owner 时拒绝 |
| Connector | MCNConnectorModule | 一个 Channel/Protocol，Start/Stop、SendPayloadAsync、按 SubChannel 发送、解析接收通知、映射数据操作 |

**证据口径：** 内存虚拟与 Windows Socket Loopback 只能证明本地协议及传输行为，不能替代 PLC/串口工业规约完整主站轮询、X-Ray 厂商 SDK 实机采图和工业设备安全联锁认证。Message/Command 尚待正式统一体系 Owner 装配；未装配返回 NotSupported，授权失败返回 UnauthorizedAccessException。


## 7. 虚拟通道最终收敛

正式类 MCCVirtualChannel 继承 MCCSingleChannelBase，只支持原始字节传输。MCCChannelContracts 单独负责 IMCCChannel、MCCChannelState 和 MCCChannelFrame。旧 MCCVirtualRawChannel 冗余命名和 MCCCxChannelSession 中的旧规约兼容路径均已删除。长度帧解析及 CRC 错误归 Protocol，Channel 在协议 CRC 错误时保持正常连接。TestCenter 自动测试、人工 Virtual 通道界面均复用此新语义，不产生第二份通道生命周期。
