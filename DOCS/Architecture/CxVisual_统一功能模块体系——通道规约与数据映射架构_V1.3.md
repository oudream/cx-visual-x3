# CxVisual 统一功能模块体系——通道规约与数据映射架构 V1.3

> **2026-10-27 新增唯一 Owner 裁决（覆盖旧类名/旧归属，不改变 V1.3 既定行为）**：Channel 全部正式类型已目标迁移至 `Foundation.Transport / F17`，名称为 `FTR*` / `IFTR*`；`Mechanism.Connectivity / M13～M15` 保留 `MCN*` Connector、`MPA*` Protocol、PointTable、Mapping。下文提及的 `MCC*` 只是此前版本历史类型名，现行 API 以 [F17 裁决](../Decisions/CxVisual_F17_Channel下沉与唯一归属裁决_V1.0.md) 和实际源码为准。发布/编译结果须另查测试台账。

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


## 运行期安全收敛补充：Channel 生命周期与多对端

- `MCCChannelState` 保留旧 `Closed/Open/Faulted` 枚举值，追加 `Opening/Closing`。Channel 在启动接收任务前进入 Opening；Opening 内到达的第一包可以被接纳。Open 异常必须清理已经打开的资源，Faulted 状态允许通过 Close 恢复到 Closed。
- 网络 Open 的取消只用于连接阶段：不能把外部调用方的 `CancellationToken` 作为 Socket 整个使用周期的关闭 Token。后台接收使用通道自己的生命周期取消源。
- TCP Server 的 `RemovePeer` 必须同步移除代次路由和关闭原 Socket；一个客户端断开不应该导致整个监听通道进入 Faulted。
- UDP Server 按远端 Endpoint 创建逻辑 SubChannel，不模拟 TCP Accept，并使用 `MaxClients` 限制新对端数量；移除后旧 `Generation` 不能继续路由。
- `ModuleChannelTransportSmokeTests` 新增打开过程首包、异常清理和故障恢复反例；TCP/UDP Loopback 只验证本机通信，不代表现场设备 HIL。


## 子通道与规约会话生命周期收敛

- MultiPeer Channel `PeerRemoved` 向 Connector 通知被移除的具体 `MCCSubChannelAddress`（含 Generation），由唯一 Protocol 按地址 Reset 分帧缓存；Connector Stop 时统一 ResetAll。
- Connector 通过 `PayloadReceivedFrom` 同时提供来源地址与解码数据，允许 Server 依据 SubChannel 定向响应；旧无地址 `PayloadReceived` 仍提供兼容观察，但不作为多对端定向业务依据。
- `MPALengthFrameProtocol`、`MPAModbusTcpProtocol`、`MPAModbusRtuProtocol` 实现 `IMPAProtocolSessionCleanup`；RTU 作为 SharedBus 使用整体清空，不人为创造多个站号子通道。
- OS TCP/UDP Loopback 加强为一个 Server 同时接入两个 Client 的验证，并验证不同 Endpoint / SubChannel 及准确回送；这仍不是外部工业设备/高并发/长时压力验收。


## 8. Channel 有界诊断证据（本轮源码增量，CI 以对应 SHA 为准）

- `MCCChannelBase` 的 `ReadFrames()` 是诊断证据缓存，不是生产接收队列。之前仅限制 10000 帧，无累计字节上限；单条大报文会导致诊断缓存扩张。
- 统一基类使用 FIFO 有界证据队列，最多 10000 帧、保留字节合计最多 4 MiB；单条报文超过 256 KiB 时，仅省略诊断载荷副本，`MCCChannelFrame.Error` 显式附带原始长度及省略原因，不改动 `Received` 事件和真实发包内容。
- `ModuleChannelTransportSmokeTests.BoundedEvidence` 增加大报文原样接收、元数据省略、连续收包后累计留存上限与最新帧保留反证。该测试为进程内机制回归，不代表实际 2D/CT 图像大数据通道性能或硬件 HIL；使用大型图像时仍应走既有数据 Handle 与背压约束。
- 本轮只收敛诊断留存内存边界；`QueueCapacity` / `ReceiveByteBudget` 对生产收包背压的实际契约、`ConnectTimeoutMs` 的运行应用、Serial 设备 HIL 和完整 Modbus 事务均未因本改动宣称完成。


## 9. Modbus TCP 请求—响应事务增量（提交后以对应 SHA 的 CI 为准）

- `MPAModbusTcpProtocol` 正式实现 `IMPAProtocolRequester.RequestAsync`：仅支持已明确验证的 03 读保持寄存器与 06 写单寄存器 PDU，不把发出报文当作执行成功。按 `ChannelAddress`（含 Generation）+ MBAP `TransactionId` 寻址，再经现有 `MPAProtocolSession` 的 `CorrelationId`、超时与取消返回任务结果；校验 Unit、功能码、03 数据长度与 06 回显，Modbus 异常响应及时失败。
- `MCNConnectorModule.RequestAsync` 使用现有唯一 Channel 的接收订阅及 Protocol，不创建每次请求的新 Socket/Adapter。直接使用 Protocol 时，调用方必须将 Channel 的 Received 交给 `ProcessReceived`；Connector 启动后已统一接管。
- `Reset(address)`、`ResetAll()` 对失败、断线及停止时清理待处理会话，旧子通道代次不可借新连接完成旧事务；超时、断开、响应不合法均不能返回 Completed。
- `ModuleFoundationSmokeTests.ModbusTcpTransactionSmoke` 使用正式 Virtual 纯传输双端，完成 03/06 发送确认、异常码、错误 Unit、超时和不支持功能码反证；这不等于外部 PLC 或 RS-485 HIL。
- **尚未完成**：PointTable 驱动的自动寄存器值转换、03/06 到 MDS 的自动映射、更多 Modbus 功能码、RTU SharedBus 排队与 T3.5 静默间隔、工业 HIL/压力；并未借本轮冒充全部 P2/P3 达标。

- 有效数据事件额外收敛：已匹配待处理事务但校验失败的 Modbus TCP 响应，只经 `MPAProtocolSession.FailResponse` 让请求失败，不再进入 Connector 的成功 Payload 观察事件。正反测试明确验证错误 Unit/异常码不增加已接受 Payload 数量。

- Socket 级集成反证：`ModuleChannelTransportSmokeTests.ConnectorModbusTcpLoopback` 直接装配正式 `MCNConnectorModule` + `MCCTcpClientChannel` + `MPAModbusTcpProtocol`，真实 Windows TCP Server 回送同一 MBAP TransactionId/Unit，检查 03 请求完成与点值 PDU，不以 Virtual 单元测试替代 OS Socket 收发。服务端只是本机受控模拟设备，G2 仍未验证。


## 10. 16 位 Holding Point → MDS 数据映射闭环（阶段性支持）

- Modbus TCP 具体规约实现可选 `IMPAProtocolPointAccess`，从 `MPAProtocolConfiguration.Points` 解析 `StationAddress`、`holding:地址`、`ValueType` 与 CanRead/CanWrite；只对 `UInt16/Int16` 的单寄存器 03/06 提供显式 `ReadDevicePointAsync` / `WriteDevicePointAsync`，超出范围返回 NotSupported，不把未知类型随意编码。
- 接收 03 响应先核对 MBAP 事务/数量，再按点类型解释 16 位数据，经既有 `MPADataMappingExecutor.ApplyInboundAsync` 更新多个正式 MDS 目标；向设备写入时从唯一 Outbound `ReadValue` 目标读取数据，按 point 地址发起 06，请求获得合法设备回显后才返回 Completed。
- `MCNConnectorModule` 只是对唯一 Protocol PointAccess 注入现有 Channel、正式配置和 MDS Owner，未重新实现变量管理、寄存器表或调度。Windows ArchitectureSmoke 的 TCP Socket Loopback 验证一对多 MDS 写入、出站变量发送、真实设备确认与 CanWrite 拒绝。
- 本增量不代表完整 Modbus（01/02/04/05/10、32 位寄存器顺序、轮询计划、RTU SharedBus、现场授权/HIL 均仍待分阶段实装）。
