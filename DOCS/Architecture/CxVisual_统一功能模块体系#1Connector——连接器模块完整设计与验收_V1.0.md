# CxVisual 统一功能模块体系#1Connector——连接器模块完整设计与验收 V1.0

> **文档类型**：专业模块完整目标规格；**生成日期**：2026-10-26；**来源基线**：旧完整规格 V1.2（承接 V1.1 六篇十八章及附录）、五类裁决 V1.2、Connector 裁决 V1.3、公共交互 V1.4。  
> **性质**：完整目标设计与验收规范，不是源码已实现声明；代码事实须以最新 `master`、实际 Windows CI 和设备 HIL 为准。  
> **原则**：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**  
> **范围**：一 Channel + 一 Protocol / PointTable + Mapping。  
> **平台**：`cx-visual-x3`，.NET Framework 4.8 / C# / WPF；保持 Foundation → Mechanism → Professional 单向依赖及现有项目 Owner。  
> **状态标识**：【裁决】架构已确认；【继承要求】原规格完整保留；【目标】待实现；【源码已有】需以对应 SHA 核实；【待验】缺 CI/HIL/长稳证据。  

## 文档来源、优先级与版本冲突

1. `CxVisual_统一功能模块体系——完整设计与验收_V1.2.md`（个人文件库，约 226 KB，继承 V1.1 全文）：保留需求编号 `UM-*`、`TFM-*`、`TFC-*`、`TFD-*`、能力/类型表、两角色案例与所有质量边界。
2. `DOCS/Architecture/CxVisual_统一功能模块体系——五类模块与设备驱动架构裁决_V1.2.md`：正式五类、Driver 归位，无第六模块。
3. `DOCS/Architecture/CxVisual_统一功能模块体系——通道规约与数据映射架构_V1.3.md`：**优先覆盖 V1.2 的 Connector 单组/Advanced 旧条款**；当前只有 `MCNConnectorConfiguration.Channel` 和 `.Protocol` 各一个；多对端属 SubChannel。
4. `DOCS/Architecture/CxVisual_公共机制体系#统一交互——四大能力与双层总线完整设计及验收_V1.4.md`：Variable/Data/Message/Command 独立 Owner、全局交互、双层总线，**并非已完成代码的证明**。
5. `AGENTS.md`、工程 Skill、`DOCS/00～04`：工程约束与类型查重；实现前必须逐对象核对 DOCS/02～04 表格及最新真实源码。

**覆盖规则**：五类优先于早期四类；V1.3 的“一 Connector 一 Channel 一 Protocol”优先于 V1.2 的 `Standard/Advanced`、多个 `Channels/Protocols`；驱动 SDK 归 Driver 优先于早期“全部归 Connector”；来源附录为了完整追溯而保留旧表述，**冲突时以本文件前部裁决条款为准**，不能从历史引用恢复已被否决的结构。

## 专业模块的通用操作与对外能力

任何模块均应明确提供：**模块身份**（ModuleId/Kind/SchemaVersion）、**提供者能力**（CapabilityId/版本/Ports/Traits/Inputs/Outputs）、**可配置性**（唯一 FCM Section）、**运行生命周期**（确有实例才创建）、**诊断**（Status/Health/Error/Trace）、**依赖**（Resource/Owner）、**产生/处理接口**（Produces/Handles 四类交互）。

**不要求各模块新建通用 `IModuleBus` 或巨大 `Execute(object)`；具体签名优先复用现有接口。** 设计示例中的新增接口、事件、字段必须经 `DOCS/02～04` 查重、源码核实和 ADR 冻结，不能直接宣称当前可编译。

## 01. 核心责任、唯一配置与正式类

**【裁决】Connector 恰有一个 `Channel` 与一个 `Protocol`**。两者都必须存在；无需解析时使用 `MPAEmptyProtocol`，**不能**创建假的 EmptyChannel；禁止 `Channels[]`、`Protocols[]` 和旧 `Standard/Advanced` 多组模型。多个独立故障域用多个 Connector，经 MEM/MOL/MRK 组合。一个 TcpServer/UdpServer 内部有 N 个 `SubChannel`（含 `Generation`），RS-485 站号由同一个 Protocol 的 PointTable 描述。

| 对象 | 类型或主要方法 | 唯一职责 | 状态口径 |
|---|---|---|---|
| `MCNConnectorConfiguration` | `.Channel/.Protocol`、`Validate` | 一个正式配置对象、FCM SchemaVersion 2 | 【源码已有】 |
| `MCNConnectorModule` | `StartAsync/StopAsync/SendPayloadAsync/RequestAsync/ReadDevicePointAsync/WriteDevicePointAsync` | 唯一运行装配、Payload 路由 | 【源码已有】；需核实新 Kind 接入 |
| `MCCChannelBase` | `OpenAsync/CloseAsync/SendAsync/Received/State` | 真实 I/O 与生命周期 | 【源码已有】 |
| `MCCSingleChannelBase/MCCMultiChannelBase` | 子通道、会话代次、路由 | 单/多远端 | 【源码已有】 |
| `MPAProtocolBase/IMPAProtocol` | `PrepareSend/ProcessReceived` | 分帧、编码、解码、会话及交互 | 【源码已有】 |
| `MPAPointDefinition/MPAPointTable` | `StationAddress/Address/Type/Read/Write` | 外部规约点地址与运行索引 | 【源码已有】 |
| `MPADataMapping/MPADataMappingExecutor` | `ApplyInboundAsync/ReadOutboundAsync` | Point↔正式 Owner 数据映射 | 【源码已有】 |
| `IMPADataTargetAccess/MDSProtocolDataTargetAccess` | 读写/发布/调用目标 | 访问四大 Owner，不保存第二份业务真值 | 【源码已有】；消息/命令缺 Owner 时必须拒绝 |

### 配置类型不能混装

配置独立为 Virtual、TCP Client/Server、UDP Client/Server、Serial、HTTP Client、NamedPipe Client/Server、WebSocket Client、Replay、UDP Multicast、Custom；当前代码已有对应配置，但 **配置存在不等于该类型已可被 `MCNConnectorModule` 装配或已达到生产可靠性**。USB、CAN、SharedMemory、HTTP Server、WebSocket Server、文件等必须核对是否存在真实 Owner 和依赖后逐种落地，禁止空类充数。

Channel/Protocol/Point 定义不得重复保存用户数据和运行 State。传输是原始 ByteStream/Datagram/Message/RequestResponse/SharedBuffer/Listener 能力；MQTT、SOAP、Modbus 是上层 Protocol/Service 语义，不可因名字类似在 Channel 中再造 MQTT 业务堆栈。

## 02. 双向数据处理与四大交互

- **外部→内部**：Channel 收包 → Protocol 分帧/解码/会话验证 → Point/Message/Command 意图 → `MPADataMappingExecutor` → M09/MDS/M36/M12 等正式 Owner → 返回各阶段结果。子通道仅标识传输对端；Station/UnitId 是协议地址。
- **内部→外部**：M09 ValueChanged/Committed、MDS DataAvailable、M36 消息通知、M12 Command 调用由运行装配按配置匹配给 Protocol 的实际可选处理能力 → Protocol 编码/事务 → Channel 发出 → 按规约校验响应 → 分别报告发送、交付、设备反馈、现场确认。
- 映射描述 `Source/Trigger/Filter/Transform/Target/Operation/Policy/Result`；优先增补现有 `MPADataMapping`，不另外增加第二张 Mapping 表。直接映射不做复杂条件/等待/补偿，这类流程由 MOL/MRK 管。
- **禁止回环**：设备点入站→变量提交→同源变量出站又写回设备，须检查 Origin/Writer/Generation/Correlation/Direction；禁止用 Message wildcard 绕过 Command 授权。
- **命令真实语义**：Modbus 06 回显、HTTP 200 和 TCP Sent 都不是电机移动/曝光完成；设备或独立状态反馈由专业 Provider/MRK 确认。

## 03. 通道可靠性与工业门禁

必须设计有界收发队列/字节预算和背压，且与同步 Protocol 解析线程隔离；禁止用无限 Queue 或 Receive 同步回调阻塞工业采集。应支持连接/断开、超时、取消、重连退避、心跳、Session Reset、Generation 隔离、并发发送串行化、接收帧/Datagram 边界、安全关停、资源/句柄释放与真实异常码。

近期代码审计曾发现 `MCCChannelConfiguration.QueueCapacity/ReceiveByteBudget/Reconnect*` 与 I/O 核心实际生效存在缺口，且部分新增 Channel 尚未在 `MCNConnectorModule` Kind 校验分支装配；**必须按最新 HEAD 重新复核**，不得拿配置字段或 Socket loopback 作为工业已验依据。

| 传输 | 关键正例 | 必须失败/拒绝的反例 |
|---|---|---|
| TCP Client/Server | 真 Socket、乱序/粘拆包交给 Protocol，N 客户端 Generation | 半开连接、发送竞态、对端断线、超时重连 |
| UDP Client/Server/Multicast | Datagram 边界、组播接口/TTL/回环 | 丢包、无路由、过大 Datagram、错误远端 |
| Serial/RS-485 | 真端口、波特率/校验、站号轮询串行 | 拔插、断电、冲突站号、迟到回应 |
| HTTP/HTTPS | 真请求响应、状态/Body 预算、证书策略 | 超时、TLS/代理、非 2xx、重复非幂等 POST |
| WebSocket | 升级握手、消息聚合、Close/Heartbeat | 半消息、太大消息、断网、取消 |
| NamedPipe | 进程内/跨进程多端、代次、权限 | ACL/拒绝访问、Server 退出、读写竞态 |
| Replay | 文件标识、顺序回放、只读 | 损坏/截断/越限、不可伪造真实设备写入 |
| SharedMemory（后续） | MMF/版本/CRC/Lease、进程崩溃恢复 | 裸 IntPtr 跨进程、写者异常、越界 |

## 统一交互：谁产生、谁处理、如何装配

| 输入/输出 | 产生者与处理者 | 由谁登记/通知/调度 | 关键安全及成功语义 |
|---|---|---|---|
| Variable Commit/Changed | 设备/业务/规约提交，HMI/Protocol/流程处理 | M09 原子提交与订阅；运行装配按映射连接 | 质量、SourceId/WriterId、Previous/Revision；变化不同于提交 |
| DataAvailable | Camera/AI/CT 创建，合法消费者按引用读取 | MDS/专业 Owner；Run/Generation/DataHandle 与租约 | 提醒为元信息，不广播 Image/Volume 本体 |
| Message | 任意合法模块发 Topic+Payload，多订阅者处理 | M36 发布/订阅；订阅取消和背压 | Publish/Deliver/Handle 分开；消息不可私自转成动作 |
| Command | HMI/MRK/业务发起，唯一 Handler 处理 | M12 注册精确目标；MRK Operation 协调 | 授权/资源/幂等/执行/现场确认各有结果，超时为 Unknown |

- 同一个模块可同时生产、消费、处理并再产生对象；处理关系**必须**有明确配置、代码注册、订阅、绑定或命令 Target。
- 直接映射留在对应 Owner：`MPADataMappingExecutor`、M36 订阅、M12 Handler、MDS/MOL 绑定各有正式职责；不建立万能 MappingManager。
- **动态能力发现**：`MFMModuleDescriptor.CapabilityIds`、`MOCCapabilityCatalog`、`IMRKCapabilityResolver` 等已有描述与目录为首选；模块可选择专业 Provider，但实际可用性应检查配置、依赖、Provider 状态和执行时权限。
- Connector/Protocol 可处理四大变化：变量订阅出站、数据发布引用出站、消息订阅出站、Command Handler 定向出站；入站由 Protocol 解析后调用真实 Owner。装配由运行宿主按映射创建并在 Stop 释放订阅，**不能让 Connectivity → Mechanism.Data 产生循环依赖**。

## 双角色使用合同

| 环节 | 软件开发者 | 工程编排用户（Studio/TestCenter） | 平台落实方 |
|---|---|---|---|
| 定义能力 | 提供 `CapabilityId/Version` 与参数/结果契约、真实处理者 | 浏览五类模块和 Produces/Handles | MFM/MOC/MEM |
| 配置 | 定义一份正式 FCM Schema 和专业校验 | 分类表单创建、验证、保存、加载、比较 | FCM/MHS/MPW |
| 交互 | 用现有 Owner API 发布值/消息、数据句柄或受控命令 | 选择来源、目标、触发、参数和订阅关系 | M09/MDS/M36/M12 + 绑定 |
| 调用 | L0 原生函数；L1 强类型 Provider；L2 模块能力；L3 工程运行调用 | 在 MOL 中拖入能力，MOC 校验后 MRK 执行 | 专业 Owner / MRK |
| 诊断 | 收到有意义的错误码、Operation、Trace、版本 | 查看状态、时序、故障原因及证据 | 已有日志/Trace/TestCenter |
| 安全 | 声明副作用、幂等、取消、资源及物理确认能力 | 有授权才执行真实设备动作，离线可仿真 | Target Runtime 与专业 Provider |

## 专项 TestCenter 验收清单（建立在原 `TFC-001～037` 之上）

| ID | 场景 | 可观察成功条件 |
|---|---|---|
| CN-P01 | Connector 配置由 Virtual→TCP→Serial→再保存重开 | 字段专属、保存恢复、Undo/Redo 不丢值，不显示其它种类字段 |
| CN-P02 | Kind、Provider、Protocol IO 兼容匹配 | 配置、真实 Provider、运行能力三者一致；不支持时拒绝 |
| CN-P03 | 多 TCP Peer + 两个并发响应 + 断线代次复用 | 同一个父 Channel，无串包、不向旧 Generation 发送 |
| CN-P04 | RS-485 多站号 | 一条串口、一个 Protocol、不同 Point.StationAddress |
| CN-P05 | Variable 变化→出站和 Point→入站 | 真 Owner、无回环、逐映射结果可追踪 |
| CN-P06 | Message 和 Command 进入 Protocol | 无真实 Owner 明确 Unsupported；未授权执行明确拒绝 |
| CN-P07 | 网络半开/断线、重连退避、关闭重开 | 无僵尸收包、无内存泄漏、恢复后新会话身份 |
| CN-P08 | 连续收发/下游阻塞/Queue 满 | 容量生效，明确背压/拒绝/丢帧策略，不允许无界 |
| CN-P09 | 真实设备与故障模拟 | PLC/RTU/组播/TLS/串口设备 HIL 分开出证据 |

## 与总体规范的关系及修改门禁

- **共性模型、两角色、版本、能力发现、统一交互、总体测试**以 `CxVisual_统一功能模块体系#0总体架构——完整设计与验收_V2.0.md` 为唯一解释。
- 本文是该模块专有行为与验收的权威文件；遇到旧源稿的相反描述，必须按文首覆盖表处理，并在文档里保留来源追溯。
- 任何新增正式接口/类名前须检查 `DOCS/02～04`、最新源码、现有 Owner、`ReferTo`；不能用概念示意代码替代编译测试。修改后同步更新总体文档的能力归属表与本模块 TestCenter 专项验证。


## 原 V1.2 来源条款、需求与案例（逐节承接）

> **来源历史提示**：下列原文保留其需求编号、专业能力和案例，便于追踪。凡涉及旧 `Advanced` 多组、历史“相机 SDK Connector”、未查重 API/状态的文字均只是历史上下文；**本文件前部的现行裁决优先**。

## 08 ConnectorModule——接入模块

> **详细依据**：会话已形成的《CxVisual_统一接入体系#ConnectorModule——完整设计与验收_V1.0.md》（本地配套稿），包含五仓复用、50 项待查重候选类型、通道/规约、专业驱动与 TestCenter 用例。**此章规定统一模块侧边界；接入专有细节由该文档展开，不复制第二套公共配置/能力/运行定义。**

### 08.1 正式职责

面向一个独立接入目的，**默认配置 1 个 Channel + 1 个 Protocol（或仅其中必需部分）**；Server 多客户端属于一个父 Channel 下的多个 SubChannel，RS-485 多站号属于一个规约点表。仅真实冗余、多链路共同事务等场景允许显式高级组合。设备特有 SDK/采图/运动驱动归 DriverModule；内部仍可直接复用 Channel/Protocol，但不把它们强迫暴露给工程编排用户。

### 08.2 专项需求 UM-CN01～CN10

| ID | 需求 | 目标行为 / 使用场景 | 验收与典型反例 |
|---|---|---|---|
| UM-CN01 | Channel 可选及多形态 | TCP/UDP/Serial/HTTP/IPC/SDK/SHM/None；无通道业务 Connector 不需要假的 Socket | `Channel=0` 运行；未连接不能伪造发送成功 |
| UM-CN02 | Protocol/Driver 分离 | 复用通用帧 Codec、工业规约与专用设备 SDK，实现可配置组合 | 错版帧拒绝、错误协议不可解析、厂商异常可定位 |
| UM-CN03 | 数据读写与点表 | Read/Write、周期采集、变量映射、来源、质量、时标、单位 | 缺地址/类型不匹配报错，旧点值不伪装最新 |
| UM-CN04 | 事件与订阅 | 消息订阅、SDK 回调、设备报警、事件过滤/推送 | 重连不得重复注册导致双事件；退订释放资源 |
| UM-CN05 | 命令与反馈 | Read、Write、Command、ACK、Readback、PhysicalCompleted 分开 | Send 成功≠设备完成；未知结果禁盲重试 |
| UM-CN06 | 图像/媒体/大数据 | 相机/X-Ray 采图、帧序、丢帧、FrameId/TriggerId、Buffer Lease | SDK 指针失效、无界队列、像素尺寸错误明确失败 |
| UM-CN07 | 生命周期与健康 | Connect/Initialize、重连、故障、降级、关闭、线路统计 | 断线状态可见；重连副作用受控 |
| UM-CN08 | 复杂拓扑 | 多通道角色、复合外设、通道共享、分流路由 | 同一连接复用不会争抢帧或错误关闭共享资源 |
| UM-CN09 | 服务/业务适配 | MES、ERP、DB、REST、原生 API 请求/事件与结果映射 | 业务错误不同于网络错；上传幂等与审计 |
| UM-CN10 | 安全和现场诊断 | 权限、写禁令、远程调用、协议帧、地址/命令错误定位 | 未授权真实 Write/Move/X-Ray 必须阻断 |


### 08.2.1 【V1.1新增】Connector 专项需求 UM-CN11～CN20

| ID | 目标需求 | 主要行为 / 验收要点 |
|---|---|---|
| UM-CN11 | 三类通道拓扑 | SinglePeer/MultiPeer/SharedBus 对连接模型、SubChannel/ProtocolAddress、默认调度有明确规则；TFC-004/009 |
| UM-CN12 | 子通道稳定身份 | ChannelId/SubChannelId/Generation 完整传递到接收、规约及回复；旧会话拒绝；TFC-005/006 |
| UM-CN13 | Channel 自主 I/O | 连接/监听/异步接收/重连不依赖 MRK 业务执行；停止可回收；TFC-003/029/031 |
| UM-CN14 | 同步写逻辑、灵活处理调度 | Protocol 可同步 Process，默认 Serial；按配置选择 KeyedSerial/Parallel/Dedicated/MRK；TFC-018～021 |
| UM-CN15 | 统一处理七类 Protocol 能力 | 解帧、访问、传输、交互、会话路由、数据绑定、诊断均能按需发布；不强制空实现；TFC-011～017 |
| UM-CN16 | 规约数据对象多元 | 变量/堆栈/消息/命令/媒体按合法 Owner 读取、解释和提交；TFC-024～028 |
| UM-CN17 | 配置引用及运行隔离 | Module/Channel/ProtocolBinding/Point/Variable 由统一配置持久化，动态 Peer/Socket 不入工程；TFC-001/030 |
| UM-CN18 | 安全有界背压 | TCP 限流、UDP 统计丢包、SDK BufferLease、安全写入不静默丢失；TFC-022/023 |
| UM-CN19 | 多规约共享收流安全 | 单个 Channel 有唯一原始流消费者；显式 Demux 可复用，否则配置拒绝；TFC-012 |
| UM-CN20 | 成熟源码直迁 | ReferTo 不变；直接改名/合并成熟逻辑、删除重复管理层，功能与性能回归；TFC-032/AR-08 |

### 08.3 Connector 与 Compute 的边界判据

- `ReadPLC`、通用接入层的 `Send/Receive/Read/Write`、`CallMesApi` 可归 Connector；`GrabDetectorFrame`、专有机器人运动动作、厂商 SDK 生命周期优先归 **Driver**。
- `FilterFrame`, `JudgeDefect`, `BuildTrajectory`, `ParseAlreadyOwnedText` 是 Compute：对已拥有的数据计算出结果；仅纯内存输入时不应引入 Connector。
- `ReadInternalVariable` 若只是查询 MDS，直接使用已有 MDS API；若“对外暴露工业点表协议”，外部适配部分才算 Connector。
- `BusinessDriver` 可以无 Channel，但必须有可解释的**适配边界与行为**，不能为了分类把普通规则处理都放入 Connector。

### 08.4 五仓关键复用与不复制项

- `ccxx`: `CxChannelBase`、TCP/UDP/串口、`CxChannelNone`、Buffer/Thread/SHM——借鉴独立 Channel、None 模式、IO 资源；旧 C++ 单例等不得无条件迁入。
- `gcl3`: `TerminalManager`、`ProtocolBase`、地址 Worker、Measure、PSM——借鉴配置装配、点表、质量、连接和控制反馈；不复制其旧工程树。
- `device_communicator`: `CommunicatorCustom` 与 Channel/Protocol/Sheet——借鉴创建、停止、定期采集和边缘服务集成；不沿用全局易变单例。
- `cx-vision-x2`: `DriverModuleBase/PointSheet`、`IRayDetectorDriver`、`IRayXRayDriver`、S7/Modbus 驱动——借鉴真实设备行为；SDK 句柄/图像回调需要新的所有权边界。
- `cx-cct-x3`: `module_api/`、`shm_api/`、重建与检测 Bridge——借鉴跨语言 ABI、引用与大数据本体分离、Job 关联；不将原生指针序列化。

### 08.5 两角色最终验收

开发者能配置型创建 PLC Connector 和 SDK 型探测器 Driver，均通过一致的能力描述被发现；工程用户能配置连接、点表/媒体输出、在线诊断，并在仿真与生产之间受控切换。TestCenter 最少覆盖虚拟字节帧、实际 Provider（授权时）、掉线重连、重复回调、丢帧/背压、坏地址、命令未知状态、无通道业务输入输出。

### 08.6 【V1.1定案】Channel 不是线程，不等于 Protocol，也不等于设备

- Channel：**传输资源**，负责连接、监听、收发、端点/子通道、IO 运行与诊断；既可以是 TCP 字节流，也可以是 UDP Datagram、Serial、IPC、Virtual、SHM，具体 I/O 能力按需实现。
- Protocol：**数据解释和数据交互处理**，不只是基础数字/寄存器规约：可解释、读/写、发送、请求响应、订阅事件、处理变量/运行时数据/消息/命令，必要时绑定 Channel/SubChannel；不持有第二套数据中心。
- ConnectorModule：默认围绕**一组** Channel/Protocol 与可选 PointTable 提供接入；仅冗余/专业复合场景显式使用高级多组件组合。没有通道的内部数据规约仍可存在，但**设备专有 SDK/采图封装应优先归 DriverModule**。
- Point/PointTable：只负责需要设备地址映射的接入，**不是所有规约的必经层**；JSON 消息、命令请求、SDK 回调可不用传统 Point。
- Variable 与 Heap/Stack 归 MDS；Message/Command 后续归各自独立机制（当前只预留语义需求，不定义第二套完成状态/消息队列）。

### 08.7 Channel 按传输资源与 I/O 行为两轴分类

| 技术资源类别 | 示例 | 默认 IO 行为 | 子通道/拓扑 | 边界说明 |
|---|---|---|---|---|
| 网络字节流 | TCP Client、TCP Server | ByteStream | Client SinglePeer；Server MultiPeer | TCP 只提供无帧边界字节流，帧解释一般归 Protocol |
| 无连接报文 | UDP Client/Server/Multicast | Datagram | MultiPeer/Group | 每包含 Endpoint，IP Multicast 不等于可靠连接 |
| 串口/总线 | Serial、RS-485、CAN（视设备） | ByteStream 或 Frame | SharedBus | 站号/报文 ID 优先是规约/总线地址，不冒充物理 TCP 子连接 |
| 进程间 | Named Pipe、IPC Socket、SHM | ByteStream/Message/Buffer | 按连接或流 | SHM 的句柄/租约及同步机制必须明确定义 |
| 消息/应用级交互 | MQTT、WebSocket、HTTP、RPC | Message 或 RequestResponse | 订阅/会话/端点 | 当库已经封装应用层传输，不必再拆成原始 TCP 字节流 |
| 文件/模拟 | File、Replay、Virtual、Loopback | Stream/Frame/Message（按类型） | 0..N | 模拟来源标记不能冒充实机证据 |
| 厂商 SDK | Camera、X-Ray、Robot、Motion SDK | 直接 Provider 能力 | Channel 可为 0 | 不强制造 `SDKChannel` 或 `EmptyChannel`，SDK 本身有独立通讯句柄时按专业需要表达 |

**I/O 合同按需要组合**：共同通道生命周期 `IMCCChannel`；字节流 `IMCCByteStream`、数据报 `IMCCDatagramIO`、具有消息边界的 `IMCCMessageIO`、传输自带事务的 `IMCCRequestResponseIO`、共享数据 `IMCCSharedBufferIO`、监听 `IMCCChannelListener`。不得把全体方法挤进 `IMCCChannel`，也不强制 TCP 实现 RequestResponse（后者常是规约事务）。

### 08.8 一对一、一对多、共享总线及 SubChannel

| Topology | 情境 | 子通道建模 | 默认处理方式 | 重要反证 |
|---|---|---|---|---|
| `SinglePeer` | TCP Client 对服务端 | 单默认端点可无显式子通道对象 | `Serial` | 远端更换与重连 Generation 不可复用旧路由 |
| `MultiPeer` | TCP Server 多客户端、UDP 多目标 | 动态或配置型 SubChannel，各有稳定运行身份 | `KeyedSerial` | A 的回包不能发送给 B；断开重连不能错路由 |
| `SharedBus` | RS-485 多 Modbus 站，CAN 共享总线 | ProtocolAddress 与 SubChannel 分开；总线发送受序列化约束 | `Serial` | 不能把站号拆成数条同时接收的物理串口 |

**推荐寻址**：`MCCChannelAddress(ChannelId, SubChannelId?, Generation?)` 指真实通信路由；`MPAProtocolAddress` 表示 UnitId、Station、Register、Topic、设备命令码等规约目标；`ProtocolSessionId` 表示规约解析/事务状态。三者不能混用。

**父子生命周期**：父通道拥有 Socket/Listener/总线；子通道管理对端连接或逻辑端点，支持 `Find/List/Close/SendTo`；动态子通道来自 Accept/Receive 并在断开时释放，不能全部写回工程。按需在 `IMCCSubChannel` 表达 `ParentChannelId,Id,Generation,Endpoint,State`；简单 SinglePeer 无须分配繁琐子对象。

### 08.9 Protocol 的正式定义与七大能力（全面超出 Codec/Parser）

| 能力 | 能做的事 | 典型入/出 | 核心能力接口候选 | 由谁保有状态/结果 |
|---|---|---|---|---|
| P1 数据解释 | 编码、解码、Frame 解析、长度/CRC/字段校验、语义转换 | `RawData→Frame→TypedData` | `IMPAProtocolCodec`、`IMPAFrameParser`、`IMPADataInterpreter`、`IMPADataValidator` | ProtocolSession 的流式 Parser；结构化数据经 FDT/MDS |
| P2 数据访问 | 读设备地址、写设备地址、读写合法运行数据源 | `Point/Address+Value` | `IMPAProtocolReader`、`IMPAProtocolWriter`、`IMPAProtocolDataHandler` | 设备交互归 Protocol/Driver；内部值归 MDS |
| P3 数据传输 | 向某通道/子通道发送、接收、回复 | `Payload+Route→Channel` | `IMPAProtocolSender/Receiver/Responder` | 原始 IO 始终归 Channel |
| P4 交互处理 | Request/Response、Ack、订阅、事件、消息、命令 | `Request/Correlation→Response/Outcome` | `IMPAProtocolRequester`、`IMPAProtocolEventHandler`、`IMPAProtocolDataSubscriber` | 事务归 Session，命令最终状态归 Command Owner |
| P5 会话与路由 | 子通道选择、对端寻址、状态隔离、请求关联、超时 | `ChannelAddress+ProtocolAddress` | `IMPAProtocolRouter`、`IMPAProtocolSessionResolver`、`IMPAProtocolTransactionMatcher` | Channel 管子通道，Protocol 管会话 |
| P6 数据映射 | Point/Variable/Heap/Message/Command 的转换及目标选择 | `Field→MDSBinding/Message/Command` | `IMPAProtocolDataMapper`、已有 MDS Binding | Point 归 Connector，数据归 MDS，未来两体系待落实 |
| P7 运行诊断 | 生命周期、健康、计数、错误、处理策略和速率 | `State/Trace/Stats` | `IMPAProtocolHealthProvider`、`MPAProtocolDiagnostics` | 统计来自实际 Owner，F19 提供执行指标 |

**结论**：不是单一庞大 `IProtocol` 包含七类所有方法，也不分立 TransportProtocol/DataProtocol/CommandProtocol 三个体系；保留**统一 Protocol 身份与能力声明**，复杂实现通过内部可选接口/类组合。简单规约一个类足矣；Modbus 可有 Codec/Parser/Session/Point 读写；原生 SDK 无传统 Protocol 时直接由 Connector/Provider 提供能力。

### 08.10 Protocol 与 SubChannel、ProtocolAddress、Session 的三重身份

| 名称 | 必要字段 | 生命周期 | 谁创建/销毁 | 用途 |
|---|---|---|---|---|
| Channel 地址 | `ChannelId`, `SubChannelId?`, `Generation?` | 通道运行 | Channel | 定向发送及来源定位 |
| Protocol 地址 | `UnitId/Station/Area/Offset/Topic/自定义字段` | 配置或单次操作 | Protocol 定义/Binding | 规约寻址和匹配 |
| Protocol Binding | `BindingId`, `ProtocolTypeId`, `ChannelRef?`, `Selector`, `Options`, `DataBindings` | MEM 配置生效版本 | MEM/配置机制 | 将具体规约绑定到目标资源 |
| Protocol Session | `SessionId`, `BindingId`, `ChannelAddress`, `State`, `Parser`, `Transactions` | 连接/交互运行 | Protocol 实例/宿主 | 按流或 Peer 隔离有状态解释器 |
| Request/Response 关联 | `CorrelationId`, `TransactionId`, `Deadline`, `ExpectedPeer` | 一次操作 | Protocol Session | 验证迟到帧与答复归属 |

**不要规定“每个 SubChannel 固定一个独立 OS Thread/Protocol 实例”**。多个 SubChannel 可复用同一不可变 ProtocolDefinition，按 SessionKey 建不同的 Parser/状态。一个 TCP 流只能有明确的**单一入流读取所有者**，多规约共用时在 Parser 之前先解复用；无法可靠区分协议时必须在配置校验阶段拒绝。

### 08.11 Module / Channel / SubChannel / Protocol / Point / Variable 配置归属

| 对象 | 配置内容 | 运行内容 | 权威保存/作用域 | 必须校验 |
|---|---|---|---|---|
| `ConnectorModule` | 模块类型、版本、能力引用、组件引用、Ownership/策略 | 模块实例、聚合健康、专业状态 | MEM 资源/配置 | 同 ID、引用缺失、循环依赖、权限 |
| `Channel` | TypeId、Topology、IO 类型、端点、超时、重连、子通道策略 | Socket/Listener、接收缓冲、Rx/Tx 计数、Generation | 通道定义为 MEM/配置；运行由 Connectivity | 端口、IP、容量、创建/借用资源关系 |
| `SubChannel` | 静态选择规则、远端白名单、容量上限（如需） | 动态 peer/connection 实例、收发状态 | 配置态只保存规则，**不保存动态 Socket 身份** | 来源可信、连接上限、断线后 ID/Generation |
| `Protocol` | 类型、版本、功能、解析和交互参数 | 编解码、事务状态、协议错误 | 规约类型不可变；绑定属 MEM/配置 | I/O 能力匹配、Frame/数据 Schema |
| `ProtocolBinding` | ChannelRef、SubChannelSelector、ProtocolAddress、分发/重试、DataBindings | 解析缓冲及 Session 实例 | 工程定义 | 路由匹配、共享收流冲突、并发能力 |
| `PointTable` | 设备点 ID/地址/格式/单位/权限/批量采集计划 | 原始观测可经 MDS 存储；不保留第二份变量库 | Connector 配置，专业地址规则 | 地址范围、读写权限、重复映射、转换 |
| `VariableDefinition` | 类型、标识、单位、作用域、质量策略 | 值、时间、质量、来源与订阅 | MDS | 名称/类型/作用域/访问权限 |
| 数据堆栈 | 绑定到现有 MDS 运行上下文 | Run/Flow/Step 生命周期的值与 PendingOutput | MDS | 只有合法运行上下文才可写；不越权直接写任意 Run 栈 |
| Message/Command | 未来 Schema/Target/Policy 引用 | 未来统一 Owner 的消息、命令及阶段结果 | 分别由未来体系 | 不重复创建独立 Queue/CommandRunner |

**不要为每行新建一个 ConfigManager**。每种类型可以具有强类型 Options 和 Schema，保存/Undo/校验/版本生效全部由现有配置 Owner 承担。现有 `MCCChannelProfile` 的 `Address+Port+Dictionary<string,string>` 是骨架，不能作为所有通道统一配置真源；TCP/UDP/Serial/SHM 各自的 Options 必须支持类型、默认值、单位及条件可见性。

### 08.12 三种推荐的配置关系

**例 A：TCP Server + 多 SubChannel + 自定义 Protocol**

```text
ConnectorModule Server01 (ModuleRef)
 ├─ Channel TCPServer01 {Type=TcpServer,Topology=MultiPeer,ListenPort=9000,Io=Async}
 │    └─ DynamicSubChannel {Id/Generation by Channel, MaxClients=1000}
 ├─ ProtocolBinding Custom01
 │    ├─ ProtocolType=CustomMessageProtocol
 │    ├─ ChannelRef=TCPServer01; SubChannelSelector=DynamicPeer
 │    └─ Dispatch=KeyedSerial, Key=SubChannel, MaxConcurrency=8
 └─ DataMappings: ParsedMessage→MessageOwner（预留）; DeviceValue→MDS Variable
```

**例 B：RS-485 + Modbus RTU 多站号**

```text
ConnectorModule PLC_BUS
 ├─ Channel Serial01 {Port=COM3, Baud=115200, Topology=SharedBus}
 ├─ ProtocolBinding ModbusRtu01 {ChannelRef=Serial01, Dispatch=Serial}
 ├─ ProtocolAddress UnitId=1 / 2 / 3 （不是三个独立物理 SubChannel）
 └─ PointTable {station:1 register:100→MDS.Variable PLC1.Temp,
                station:2 coil:10→MDS.Variable PLC2.Running}
```

**例 C：厂商相机 SDK，无 Channel、无传统 Protocol**：`ConnectorModule Detector01 → Professional.Vision.Driver/SDK → FrameHandle/TriggerId → MDS/专业数据 Owner`。无 `EmptyChannel`、无伪 Protocol/PointSheet，厂商线程亲和由 F19 Dedicated 保障。

### 08.13 Protocol 接收至 MDS/消息/命令的完整处理链

```text
Channel async Receive / Accept
  → 快速标注 ChannelId/SubChannelId/Generation/Size/Timestamp（安全复制或 Lease）
  → 有界入站队列（按照 Connection/Session 串行，不是每个包新建线程）
  → ProtocolSession（Frame/校验/解释/事务关联；可同步 Process）
  → Mapper/Route（Point、Variable、Heap/Stack、Message、Command 的合法目标）
  → MDS 当前作用域 / 未来消息或命令 Owner / Professional 能力
  → 需要业务流程时通过 MOC/MRK 执行；返回状态/结果
  → Protocol 编码回复 → Channel.SendTo(ChannelId+SubChannelId+Generation)
```

**所有跨执行边界传递的字节或 Frame 必须拥有明确安全生命周期**：复制、不可变对象或受控 BufferLease，不得把下一次 Socket Read 会覆盖的公用 buffer 传给异步任务。真正的 Zero-copy 需以专业数据所有权/共享内存租约验证。

#### 典型数据对象映射

| 原始输入 | 规约动作 | 交付目标 | 关键边界 |
|---|---|---|---|
| `holding register:100` | 解码 UInt16/缩放 | `MDSVariableTable.Update` | 时间、质量、来源、作用域 |
| Frame/Message JSON | schema 校验，解释字段 | 未来 `Message` 发布能力 | 没有消息体系时仅做解析/预留，不返回假成功 |
| `MOVE_AXIS` 指令帧 | 权限/参数识别→命令请求 | 未来 `Command` 能力/专业设备 Owner | Sent、Ack、Complete、Unknown 区分，硬件安全独立 |
| Node 需要的输入 | 按已有 `MDSDataBinding` 从当前运行读取/回写 | 当前合法 `IMDSDataContext` | 禁止跨 Run 越权读写 Heap/Stack |
| 探测器 Frame | FrameId/TriggerId/Shape/Stride 等解析 | 专业 FrameHandle/MDS 引用 | 大内存租约与 SDK 回调寿命 |

### 08.14 Configuration/Run 两种模式与版本切换

- **设计态**：用户从能力目录选 Module/Channel/Protocol，配置端点/拓扑/子通道、点表、绑定、线程/调度策略，校验成功后保存到 MEM/统一配置 Owner；无 Socket、无 SDK 实例。
- **运行态**：从固定已生效配置创建 Channel、加载 Protocol Session、分配 Point/Variable 数据访问、订阅运行事件；每次运行记录 `ConfigurationVersion, ProtocolBindingId, SessionId, ChannelId, SubChannelId/Generation`。
- **切换**：参数生效按 Owner 策略进行 Pause/Drain/Replace；当前流程和在途设备请求不得隐式换到新的地址/客户端。失败可回滚至上一次生效版本，必须可追溯。
- **外部凭据**：密码、证书、设备授权使用现有 Secret/治理机制，主配置中只保存安全引用，TestCenter 日志不输出原文。

### 08.15 ReferTo 直接重构时的合并与裁撤表（非 Adapter）

| 成熟来源 | 直接迁入正式目标 | 必须保留 | 应消除的冗余 |
|---|---|---|---|
| `Channels/TcpClientBase.cs`、`TcpClientChannel.cs`、`TcpClientTransportChannel.cs` | `MCCTcpClientChannel`（唯一正式实现） | 连接重试、状态、读写、异常/断开、诊断 | 旧 `IChannel` 中两代动词、重复 Socket/线程状态、外层 LegacyAdapter |
| `Channels/TcpServerChannel.cs`、`TcpServerService.cs` | `MCCTcpServerChannel` + 真实 SubChannel 管理 | Accept/ClientConnected/Disconnected/定向发送 | 每客户端 OS Thread 默认方案、独立业务队列 Worker/假子通道 |
| `UdpClientChannel.cs`、`UdpServerChannel.cs` | `MCCUdpChannel`（客户端/服务端参数可选） | Endpoint 来源、数据报边界、状态/诊断 | 仅因“Client/Server”创建两套相似线程处理模型 |
| `Protocols/Contracts/CxProtocolSession.cs`、`ProtocolFoundationContracts.cs` | `MPAProtocolDefinition/Binding/Session` 的唯一合同 | 响应关联、CRC/帧校验、Session 生命周期、事务串行 | 双层 Session、Channel 中 Request 与 Protocol Request 混杂、重复 ProviderManager |
| `Protocols/ModbusTcpModule/*` | MPA Codec/Parser + Automation Modbus Driver | MBAP/TID/UnitId/功能码、寄存器读写 | 把 Modbus UnitId 伪装成独立 TCP SubChannel |
| `DeviceVariable/VariableInterfaces/{VariableTable,VariableInfo,VariableManager}.cs` | `MDSVariableTable` 与必要的实例作用域 | 线程安全、Value/Updated/Initial、订阅通知 | static 全局表/后台线程、重复 VariableValue 缓存 |
| `DeviceVariable/DeviceInterfaces/{Point,PointSheet,DriverModuleBase}.cs` | Connector PointTable/PointBinding 与专业 Driver | 地址、点查询、专业设备方法/读写 | 空壳 PointSheet、跨专业全局 DeviceManager、重复 Connector DriverBase |

`ReferTo` 一律只读；先复制目标需要的 C# 代码再**合并、删减、改名并加测试**，最终删除临时 `Porting/` 审查目录；迁移前实际已有的项目成熟行为应有可复现对照，不因新架构损害正确性、吞吐、异常恢复或设备安全。

#### B. Channel 定义、I/O 与诊断

| 类型 / 类型类别 | Owner | 主要方法 / 属性（目标） | 定位 / 状态 |
|---|---|---|---|
| `IMCCChannel`（Interface） | Connectivity.Channels | `Id,State,Open/Close,Send/ReadFrames`（现有）；目标补异步生命周期 | 【实】现有合同待原位统一；勿建第二版 `IChannel` |
| `MCCChannelProfile`（Class） | Connectivity.Channels | `Id,Kind,Address,Port,Settings`（现有）；目标 `TypeId,Options,Validate` | 【实】配置骨架待类型化 |
| `MCCChannelTypeDescriptor`（Class） | Connectivity.Channels | `TypeId,Capabilities,Topology,ConfigSchema` | 【候】用于 Studio/TestCenter 类型选择 |
| `MCCChannelOptions`（Class） | Connectivity.Channels | `Timeout,IoMode,BufferPolicy,ReconnectPolicy,Validate()` | 【候】可与类型 Schema 合并 |
| `MCCChannelEndpoint`（Class） | Connectivity.Channels | `Scheme,Host,Port,Path,ToString()` | 【迁】ReferTo `CxChannelEndpoint` |
| `MCCChannelHealth`（Class） | Connectivity.Channels | `State,LastError,ChangedUtc,IsHealthy()` | 【迁】ReferTo Health |
| `MCCChannelDiagnostics`（Class） | Connectivity.Channels | `GetSnapshot(),TraceSend(),TraceReceive(),TraceError()` | 【迁】ReferTo 收发统计 |
| `MCCBufferPolicy`（Class） | Connectivity.Channels | `CapacityItems,CapacityBytes,OverflowPolicy,Validate()` | 【迁/候】ReferTo 配置 + F19 背压避免重复 |
| `MCCReconnectPolicy`（Class） | Connectivity.Channels | `Delay,MaxAttempts,Backoff,ShouldRetry(n)` | 【迁】保留成熟重连规则 |
| `MCCChannelFrame`（Class） | Connectivity.Channels | `ChannelId,SubChannelId?,Generation?,Bytes,Timestamp,Correlation` | 【实】已有帧，目标扩展身份 |
| `IMCCChannelFactory`（Interface） | Connectivity.Channels | `CanCreate(type),Create(profile)` | 【候】仅工厂的具体能力有价值时 |
| `MCCChannelFactory`（Class） | Connectivity.Channels | `Create(profile),Validate(profile)` | 【迁】合并旧工厂，不创建第二注册中心 |
| `IMCCByteStream`（Interface） | Connectivity.Channels.IO | `ReadAsync(buffer,offset,count,ct),WriteAsync(...)` | 【候】TCP、Serial、Pipe |
| `IMCCDatagramIO`（Interface） | Connectivity.Channels.IO | `SendToAsync(endpoint,data,ct),ReceiveFromAsync(ct)` | 【候】UDP |
| `IMCCMessageIO`（Interface） | Connectivity.Channels.IO | `PublishAsync(topic,message,ct),SubscribeAsync(...)` | 【候】仅消息型传输真正需要时 |
| `IMCCRequestResponseIO`（Interface） | Connectivity.Channels.IO | `RequestAsync(request,ct)` | 【候】仅 HTTP/RPC/SDK 自带事务语义，TCP 不强制实现 |
| `IMCCSharedBufferIO`（Interface） | Connectivity.Channels.IO | `AcquireAsync(handle,ct),PublishAsync(handle,ct),Release()` | 【候】MDS/CT 专业租约 Owner 优先 |
| `IMCCChannelListener`（Interface） | Connectivity.Channels.IO | `StartListeningAsync(ct),AcceptAsync(ct),StopListeningAsync(ct)` | 【候】TCP/WebSocket Server |
| `IMCCChannelObservable`（Interface） | Connectivity.Channels.IO | `Subscribe(handler),Unsubscribe(id),StateChanged` | 【候】避免独立事件总线 |
| `IMCCChannelDiagnostics`（Interface） | Connectivity.Channels | `GetHealth(),GetDiagnostics()` | 【候】仅跨类型调用要求时 |

#### C. Channel 子通道与具体实现

| 类型 / 类型类别 | Owner | 主要方法 / 属性（目标） | 定位 / 状态 |
|---|---|---|---|
| `IMCCSubChannel`（Interface） | Connectivity.Channels.SubChannels | `Id,ParentChannelId,Generation,Endpoint,State` | 【候】重点，子通道只归 Channel 管理 |
| `MCCSubChannel`（Class） | 同上 | `CloseAsync(ct),GetHealth()` | 【候】可合并进具体 TcpClientConnection |
| `MCCSubChannelAddress`（Class/Struct） | 同上 | `ChannelId,SubChannelId?,Generation?` | 【候】防错误定向、旧连接误用 |
| `IMCCSubChannelManager`（Interface） | 同上 | `Find(id),List(),CloseAsync(id,ct)` | 【候】复杂 MultiPeer Channel 按需 |
| `MCCSubChannelCollection`（Class） | 同上 | `Add(),Remove(),Find(),Snapshot()` | 【候】仅属于父通道，不是全局注册中心 |
| `MCCSubChannelProfile`（Class） | 同上 | `Selector,AllowedPeers,Capacity,Validate()` | 【候】配置型子通道规则，动态端点不写 MEM |
| `MCCSubChannelEvent`（Class） | 同上 | `ChannelId,SubChannelId,Generation,Kind,Timestamp` | 【候】创建/断开/故障观测 |
| `MCCSubChannelDiagnostics`（Class） | 同上 | `RxBytes,TxBytes,LastError,LastActive` | 【候】可合并到 Channel Health 快照 |
| `IMCCSubChannelSelector`（Interface） | 同上 | `Select(context),Validate()` | 【候】按收发身份选择路径 |
| `MCCChannelReceiveContext`（Class/Struct） | Connectivity.Channels | `Address,Timestamp,Payload,CorrelationId` | 【候】带来源和 BufferLease 的安全交接 |
| `MCCTcpClientChannel`（Class） | Connectivity.Channels | `OpenAsync(),ReadAsync(),WriteAsync(),CloseAsync()` | 【迁】ReferTo 三份 TCP Client 收敛成唯一正式实现 |
| `MCCTcpServerChannel`（Class） | Connectivity.Channels | `StartListeningAsync(),AcceptAsync(),SendToAsync(),StopAsync()` | 【迁】ReferTo Server 与 Service 合并，非每连接一线程 |
| `MCCTcpClientSubChannel`（Class） | Connectivity.Channels | `ReadAsync(),WriteAsync(),CloseAsync()` | 【候】只在 TCP Server 的客户端子连接需要时 |
| `MCCUdpChannel`（Class） | Connectivity.Channels | `SendToAsync(),ReceiveFromAsync()` | 【迁】ReferTo UDP Client/Server 去重复 |
| `MCCSerialChannel`（Class） | Connectivity.Channels | `OpenAsync(),ReadAsync(),WriteAsync()` | 【迁/候】具体接口以 ReferTo 现有实现审核为准 |
| `MCCCanChannel`（Class） | Professional.Automation / Connectivity 扩展 | `SendFrameAsync(),ReceiveFrameAsync()` | 【候】必须有真硬件 Provider 才落地 |
| `MCCUsbChannel`（Class） | 同上 | `OpenAsync(),ReadAsync(),WriteAsync()` | 【候】不强迫 USB 设备使用字节流 |
| `MCCNamedPipeChannel`（Class） | Connectivity.Channels | `OpenAsync(),ReadAsync(),WriteAsync()` | 【候】IPC |
| `MCCSharedMemoryChannel`（Class） | Connectivity + 数据 Owner | `Open(),Acquire(),Publish(),Release()` | 【候】须与 CT/MDS SHM 统一资源所有权 |
| `MCCWebSocketChannel`（Class） | Connectivity.Channels | `ConnectAsync(),SendAsync(),ReceiveAsync()` | 【候】按实际接口决定 |
| `MCCMqttChannel`（Class） | Connectivity.Channels | `PublishAsync(),SubscribeAsync()` | 【候】可直接由 SDK Provider 提供，不必强制存在 Channel |
| `MCCFileChannel`（Class） | Connectivity 或专业 Owner | `Open(),Read(),Write(),Seek()` | 【候】离线场景按需 |
| `MCCReplayChannel`（Class） | Simulation/Connectivity | `Load(),Start(),Pause(),Step(),Seek()` | 【候】Replay 时间归 Simulation，不另建 Worker |
| `MCCVirtualChannel`（Class） | Connectivity.Channels | `CreatePair(),Open(),Send(),Close(),ReadFrames()` | 【实】现有虚拟点对点，业务帧解析边界需审查 |
| `MCCLoopbackChannel`（Class） | Connectivity.Channels | `Send(),Receive(),Reset()` | 【候】测试专用 |

#### D. Protocol 基础、定义、配置、会话

| 类型 / 类型类别 | Owner | 主要方法 / 属性（目标） | 定位 / 状态 |
|---|---|---|---|
| `IMPAProtocol`（Interface） | Connectivity.Protocols | `Id,Descriptor,Capabilities` | 【候】与现有 `IMPAProtocolDefinition` 二选一/合并 |
| `IMPAProtocolDefinition`（Interface） | Connectivity.Protocols | `Id,RequiredChannelKind,Codec`（现有） | 【实】必须按七大能力扩展并避免超大接口 |
| `MPAProtocolDescriptor`（Class） | Connectivity.Protocols | `Id,Name,Version,Capabilities,SupportedIO` | 【迁】ReferTo `CxProtocolDescriptor` |
| `MPAProtocolOptions`（Class） | Connectivity.Protocols.Configuration | `Parameters,Timeout,RetryPolicy,Validate()` | 【候】复用统一配置 Schema |
| `MPAProtocolBinding`（Class） | Connectivity.Protocols | `ProtocolRef,ChannelRef,SubChannelSelector,Validate()` | 【迁】ReferTo `CxProtocolBinding`；与 `MCNProtocolBinding` 合并 |
| `IMPAProtocolSession`（Interface） | Connectivity.Protocols.Sessions | `Id,State,StartAsync(),StopAsync()` | 【候】仅需要状态的规约 |
| `MPAProtocolSession`（Class） | 同上 | `ProcessAsync(),HandleReceivedAsync(),Dispose()` | 【迁】合并 ReferTo 两代 Session |
| `MPAProtocolContext`（Class） | Connectivity.Protocols | `BindingId,ChannelAddress,ProtocolAddress,CorrelationId,DataContext` | 【候】一次处理上下文，不是第二数据堆栈 |
| `MPAProtocolSessionContext`（Class） | 同上 | `SessionId,ChannelAddress,State` | 【候】若 Context 可承载则不建 |
| `MPAProtocolTransaction`（Class） | Connectivity.Protocols.Sessions | `RequestId,CorrelationId,Deadline,Complete(),Fail()` | 【候】请求响应事务 |
| `MPAProtocolTransactionManager`（Class） | 同上 | `Register(),MatchResponse(),Cancel(),Expire()` | 【候】复杂规约专用；不建全局管理器 |
| `IMPAProtocolSessionResolver`（Interface） | 同上 | `Resolve(binding,address),Release(sessionId)` | 【候】Peer/Binding 到 Session 的定位 |
| `MPAProtocolSessionCollection`（Class） | 同上 | `Find(),Add(),Remove(),Clear()` | 【候】复杂 Server 可内置集合 |
| `MPAProtocolPeerState`（Class） | 同上 | `Address,LastSeen,Sequence,State` | 【候】有状态 Peer |
| `MPAProtocolReceiveContext`（Class） | Connectivity.Protocols | `Address,Bytes,Timestamp,CorrelationId` | 【候】优先合并为统一 Context |
| `MPAProtocolSendContext`（Class） | Connectivity.Protocols | `Route,ProtocolAddress,CorrelationId` | 【候】优先合并为统一 Context |
| `MPAProtocolFactory`（Class） | Connectivity.Protocols | `Create(definition,context)` | 【迁】ReferTo 工厂与 Provider 合并 |
| `IMPAProtocolProvider`（Interface） | Connectivity.Protocols | `Describe(),CreateSession()` | 【候】不要复制一份能力目录/MOC Provider |

#### E. Protocol 七大能力接口

| 类型 / 类型类别 | Owner | 主要方法（目标） | 功能和裁决 |
|---|---|---|---|
| `IMPAProtocolCodec`（Interface） | Connectivity.Protocols | `Encode(payload),Decode(frame)` | 【实】字节编解码仅为 P1 子能力 |
| `IMPAFrameParser`（Interface） | Connectivity.Protocols | `PushBytes(),TryReadFrame(),Reset()` | 【迁】ReferTo FrameParser 直接整理，状态按流隔离 |
| `IMPADataInterpreter`（Interface） | Connectivity.Protocols | `InterpretAsync(input,context,ct)` | 【候】P1 规约语义解释 |
| `IMPADataFormatter`（Interface） | Connectivity.Protocols | `FormatAsync(data,context,ct)` | 【候】P1 专业格式化可并 Codec |
| `IMPADataValidator`（Interface） | Connectivity.Protocols | `Validate(data,context)` | 【候】P1 Schema/字段/校验 |
| `IMPAChecksum`（Interface） | Connectivity.Protocols | `Calculate(bytes),Verify(bytes,checksum)` | 【候】只在独立校验算法有复用价值时 |
| `IMPAProtocolReader`（Interface） | Connectivity.Protocols | `ReadAsync(address,context,ct)` | 【候】P2 远端/规约数据读 |
| `IMPAProtocolWriter`（Interface） | Connectivity.Protocols | `WriteAsync(address,value,context,ct)` | 【候】P2 远端/规约数据写，不能等同 MDS Set |
| `IMPAProtocolDataHandler`（Interface） | Connectivity.Protocols | `ProcessInboundAsync(...),ProcessOutboundAsync(...)` | 【候】P2 非必备聚合处理接口 |
| `IMPAProtocolDataMapper`（Interface） | Connectivity.Protocols | `MapInbound(),MapOutbound(),ValidateMapping()` | 【候】P6 尽量复用 MDS DataBinding/FDT 转换 |
| `IMPAProtocolDataSubscriber`（Interface） | Connectivity.Protocols | `SubscribeAsync(),UnsubscribeAsync()` | 【候】P4 规约订阅，不是新消息中心 |
| `IMPAProtocolSender`（Interface） | Connectivity.Protocols | `SendAsync(data,context,ct)` | 【候】P3 规约编码后的定向发送 |
| `IMPAProtocolReceiver`（Interface） | Connectivity.Protocols | `ReceiveAsync(),HandleReceivedAsync()` | 【候】P3 数据接收，消费端不得二次读取 Socket |
| `IMPAProtocolRequester`（Interface） | Connectivity.Protocols | `RequestAsync(request,context,ct)` | 【候】P4 请求及响应关联 |
| `IMPAProtocolResponder`（Interface） | Connectivity.Protocols | `RespondAsync(response,context,ct)` | 【候】P3 回复给原 SubChannel/Generation |
| `IMPAProtocolEventHandler`（Interface） | Connectivity.Protocols | `HandleEventAsync(event,context,ct)` | 【候】P4 规约事件转交统一消息机制 |
| `IMPAProtocolStreamHandler`（Interface） | Connectivity.Protocols | `HandleStreamAsync(stream,context,ct)` | 【候】P4 连续数据处理 |
| `IMPAProtocolRouter`（Interface） | Connectivity.Protocols.Routing | `ResolveRoute(),ValidateRoute()` | 【候】P5 规约内选择真实 ChannelAddress |
| `IMPAProtocolChannelBinder`（Interface） | Connectivity.Protocols | `Bind(),Unbind(),ValidateBinding()` | 【候】P5 配置/运行边界，不再包旧 Channel Adapter |
| `IMPAProtocolTransactionMatcher`（Interface） | Connectivity.Protocols.Sessions | `RegisterRequest(),MatchResponse()` | 【候】P5 响应按 TID/Peer/会话匹配 |
| `IMPAProtocolAddressResolver`（Interface） | Connectivity.Protocols.Routing | `ResolveAddress(),ValidateAddress()` | 【候】P5 UnitId/地址解析 ≠ SubChannel |
| `IMPAProtocolLifecycle`（Interface） | Connectivity.Protocols | `StartAsync(),StopAsync()` | 【候】P7 可选有状态规约 |
| `IMPAProtocolHealthProvider`（Interface） | Connectivity.Protocols | `GetHealth(),GetDiagnostics()` | 【候】P7 健康与诊断 |
| `MPAProtocolDiagnostics`（Class） | Connectivity.Protocols.Diagnostics | `RecordRequest(),RecordResponse(),RecordError(),GetSnapshot()` | 【迁】P7 ReferTo 已有计数和错误 |
| `MPAProtocolError`（Class） | Connectivity.Protocols.Diagnostics | `Code,Category,Message,Retryable` | 【迁】P7 错误模型，严禁未知完成盲重试 |
| `MPAProtocolRetryPolicy`（Class） | Connectivity.Protocols | `CanRetry(operation,error)` | 【候】P7 优先使用 MRK/治理策略共同裁决 |
| `MPAProtocolExecutionPolicy`（Class） | Connectivity.Protocols | `Timeout,Ordering,Concurrency` | 【候】P7 合并到 DispatchOptions |

#### F. Protocol 消息、路由、结果与执行参数

| 类型 / 类型类别 | Owner | 主要方法/属性（目标） | 定位/状态 |
|---|---|---|---|
| `MPAProtocolFrame`（Class） | Connectivity.Protocols | `RawBytes,Timestamp,Metadata,ValidateChecksum()` | 【迁】ReferTo `CxProtocolFrame` |
| `MPAProtocolMessage`（Class） | Connectivity.Protocols | `MessageType,Fields,GetField(),SetField()` | 【迁】规约内部消息，不替代未来 MessageEnvelope |
| `MPAProtocolData`（Class） | FDT/MDS | `DataType,Value,Metadata` | 【候】若 FDT 已有值类型，则**不得新增** |
| `MPAProtocolAddress`（Class） | Connectivity.Protocols | `Station,Area,Offset,Parameters` | 【候】仅规约寻址，非物理子通道 |
| `MPAProtocolRoute`（Class） | Connectivity.Protocols.Routing | `ChannelAddress,ProtocolAddress,BindingId` | 【候】一次交互的目标 |
| `MPAProtocolRouteRule`（Class） | Connectivity.Protocols.Routing | `Match(),Resolve()` | 【候】高级路由，简单场景无此类 |
| `MPAProtocolOperationRequest`（Class） | Connectivity.Protocols | `OperationId,Kind,Target,Data,CorrelationId` | 【候】尽量重用现有调用/命令合同 |
| `MPAProtocolOperationResult`（Class） | Connectivity.Protocols | `Success,Data,Error,Stage` | 【候】发送/确认/完成阶段不得合并 |
| `MPAProtocolDataTarget`（Class） | MDS/Connectivity | `SourceKind,TargetRef,ValueType` | 【候】已有 `MDSDataBinding` 优先 |
| `MPAProtocolActionDefinition`（Class） | MOC | `ActionId,OperationKind,InputSchema,OutputSchema` | 【候】优先用 `MOCCapabilityDescriptor` |
| `MPAProtocolSubscription`（Class） | Connectivity.Protocols | `SubscriptionId,Route,StopAsync()` | 【候】有持续订阅的协议才需要 |
| `MPAProtocolEvent`（Class） | Connectivity.Protocols | `EventType,Data,Context` | 【候】未来交给统一消息体系 |
| `MPAProtocolDispatchOptions`（Class） | Connectivity.Protocols + 现有 F19 | `ProcessingMode,QueueCapacity,ByteBudget,Concurrency,OrderingKey,ExecutionGroupRef` | 【候】**配置型**，不得持有第二调度器 |
| `MPAProtocolDispatchContext`（Class） | Connectivity.Protocols | `ChannelAddress,SessionId,CorrelationId,Cancellation` | 【候】可并入 `MPAProtocolContext` |
| `MPAProtocolSessionScope`（Enum） | Connectivity.Protocols | `Channel,SubChannel,ProtocolAddress,Binding` | 【候】按实际 Parser/事务隔离粒度 |

#### G. PointTable / Variable / MDS

| 类型 / 类型类别 | Owner | 主要方法/属性（目标） | 状态与防重 |
|---|---|---|---|
| `MCNPointDefinition`（Class） | Connectivity.Connectors | `VariableName,Address,ValueType,CanRead,CanWrite` | 【实】已有，进一步补 PointId/规约地址/质量绑定 |
| `MCNPointTable`（Class） | Connectivity.Connectors | `Add(),Remove(),Find(),Query(),Validate()` | 【迁】ReferTo PointSheet；真正需要索引/批量采集才独立建表 |
| `IMCNPointTable`（Interface） | Connectivity.Connectors | `GetPoint(),FindPoints()` | 【候】一个具体类足够时删接口 |
| `MCNPointAddress`（Class） | Connectivity.Connectors | `Area,Station,Offset,Length` | 【候】专业字段可用 `MPAProtocolAddress` |
| `MCNPointBinding`（Class） | Connectivity.Connectors | `PointRef,DataBinding,Direction` | 【候】使用已有 MDS 来源定义 |
| `MCNPointAccessPolicy`（Class） | Connectivity.Connectors | `CanRead,CanWrite,CanControl,Validate()` | 【候】可并入 PointDefinition |
| `MCNPointConversion`（Class） | Connectivity.Connectors | `Scale,Offset,DataType,Convert()` | 【候】优先 FDT/Compute 已有转换 |
| `MCNPointGroup`（Class） | Connectivity.Connectors | `AddPoint(),RemovePoint(),Find()` | 【候】按读周期/设备组 |
| `MCNPointReadPlan`（Class） | Connectivity.Connectors/Professional.Automation | `Points,Interval,Build(),Validate()` | 【迁/候】成熟 Modbus 批量读取优化 |
| `MCNPointWritePlan`（Class） | 同上 | `Points,Build(),Validate()` | 【候】必须核对设备写命令顺序安全 |
| `MCNPointUpdate`（Class） | Connectivity / MDS | `PointId,Value,Timestamp,Quality` | 【候】优先复用 MDS Sample |
| `IMDSVariableTable`（Interface） | Mechanism.Data.Variables | `Get<T>(),TryGet<T>(),Update<T>(),VariableChanged` | 【实】接口骨架 |
| `MDSVariableTable`（Class） | Mechanism.Data.Variables | `Add(),Remove(),Get(),Update(),Find(),Subscribe()` | 【迁】ReferTo 成熟线程安全变量表 |
| `MDSVariableDefinition`（Class） | Mechanism.Data.Variables | `Id,Name,Type,Unit,Access` | 【候】核查 Foundation/Data/DataSpace |
| `MDSVariableInfo<T>`（Class） | Mechanism.Data.Variables | `Value,HasInitialValue,LastUpdated` | 【迁】ReferTo VariableInfo |
| `MDSVariableSample<T>`（Class） | Mechanism.Data.Variables | `Value,UpdatedUtc,HasInitialValue` | 【实】骨架，质量/来源待扩展 |
| `MDSVariableScope`（Class/Enum） | Mechanism.Data | `Project,Module,Device,Session` | 【候】依已有 DataSpace 规则定案 |
| `MDSVariableSubscription`（Class） | Mechanism.Data | `SubscriptionId,Stop()` | 【候】可能并入已有通知机制 |
| `MDSVariableQuery`（Class） | Mechanism.Data | `NamePattern,Tags,Type` | 【候】按需要索引查询 |
| `MDSVariableChange`（Class） | Mechanism.Data | `OldValue,NewValue,Timestamp,Source` | 【候】现有数据事件合并优先 |
| `MDSVariableQuality`（Enum） | Mechanism.Data | `Good,Bad,Uncertain,Stale` | 【候】查重，不建第二质量码体系 |
| `MDSVariableRegistry`（Class） | Mechanism.Data | `FindTable(),RegisterTable(),RemoveTable()` | 【迁】清除旧静态全局 Manager，作用域归工程/Host |
| `MDSDataBinding`（Class） | Mechanism.Data.RuntimeData | `Source,ValueType,SourceName` | 【实/复】绑定唯一事实 |
| `IMDSDataContext`（Interface） | Mechanism.Data.RuntimeData | `Get<T>(),Set<T>(),Return<T>(),HeapGet<T>(),HeapSet<T>()` | 【复】Run/Flow/Step 数据 Owner |
| `MDSDataBindingResolver`（Class） | Mechanism.Data.RuntimeData | `Resolve<T>()` | 【实/复】不得二次创建 DataBinding 管理器 |
| `IMDSExternalBindingResolver`（Interface） | Mechanism.Data.RuntimeData | `Resolve()` | 【实/复】专业/工程数据来源协作 |

#### H. Connector / Driver / 装配与配置

| 类型 / 类型类别 | Owner | 主要方法/属性（目标） | 状态/建议 |
|---|---|---|---|
| `IMCNConnectorModule`（Interface） | Connectivity.Connectors | `Channels,Protocols,Points`（现有） | 【实】运行视图须与 ModuleDefinition/Binding 区分 |
| `MCNConnectorDefinition`（Class） | MEM+Connectivity | `ModuleId,ChannelRefs,ProtocolBindings,PointRefs` | 【候】先查 MEM 工程资源容器 |
| `MCNConnectorInstance`（Class） | Connectivity.Connectors | `StartAsync(),StopAsync(),GetState()` | 【候】仅资源型接入需要 |
| `MCNConnectorContext`（Class） | Connectivity.Connectors | `ModuleId,InstanceId,DataContext,Resources` | 【候】复用 MRK/MDS 运行身份 |
| `MCNConnectorAssembler`（Class） | Connectivity.Connectors | `Validate(),Create(),Bind()` | 【候】最小装配；不是第二 Runner |
| `MCNChannelBinding`（Class） | Connectivity.Connectors | `ChannelRef,Ownership,Options` | 【候】优先用 Module 组件引用 |
| `MCNProtocolBinding`（Class） | Connectivity.Connectors | `ProtocolRef,ChannelRef,SubChannelSelector` | 【候】**必须与 MPAProtocolBinding 合并** |
| `MCNDataMapping`（Class） | Connectivity/MDS | `Source,Target,Direction,Transform` | 【候】优先 MDS Binding |
| `MCNConnectorHealth`（Class） | Connectivity.Connectors | `State,Components,LastError` | 【候】优先诊断聚合 |
| `MCNConnectorDiagnostics`（Class） | Connectivity.Connectors | `GetChannelStats(),GetProtocolStats(),GetPointStats()` | 【候】只读投影，不建独立统计仓 |
| `IMCNConnectorProvider`（Interface） | Connectivity/Professional | `Describe(),Create()` | 【候】复用 MOC 能力 Provider |
| `IMCNDeviceDriver`（Interface） | Professional.Automation | `ReadAsync(),WriteAsync(),ExecuteAsync()` | 【迁】DeviceInterfaces DriverModuleBase 职责 |
| `PACConnectorDriverBase`（Abstract Class） | Professional.Automation | `CreatePoint(),ReadPoint(),WritePoint()` | 【迁】仅确实共享生命周期的设备驱动使用基类 |
| `PACModbusTcpDriver`（Class） | Professional.Automation | `ReadRegisters(),WriteRegisters(),StartPolling(),StopPolling()` | 【迁】现 Porting 未编译，成熟实现待正式直接迁入 |
| `PACModbusRtuDriver`（Class） | Professional.Automation | `ReadRegisters(),WriteRegisters()` | 【候/迁】以实际 ReferTo 代码审核后决定 |
| `PACDeviceDefinition`（Class） | Professional.Automation | `DeviceId,DriverType,PointTableRef` | 【候】复用原专业设备类型 |
| `PACDeviceInstance`（Class） | Professional.Automation | `Initialize(),Read(),Write(),GetState()` | 【候】避免第二 DeviceManager |
| `MCNChannelBindingOwnership`（Enum） | Connectivity.Connectors | `Owned,Borrowed,Shared` | 【候】防错误关闭共享 Channel，使用已有资源租约语义 |

### CASE-FM-A：PLC / ICS / IoT 数据采集与控制

**目标**：PLC 测点采集、单位换算、阈值计算及数据服务；涉及 Connector、Compute、Service，Simulation 用于离线联调。

| 阶段 | 开发者做什么 | 工程用户做什么 | 关键行为与证据 |
|---|---|---|---|
| 定义 | 复用通道、规约 Provider 和点表类型；公开 `Read/Write/Subscribe` | 选 PLC、设 IP/站号、导入点表、配置周期 | 配置版本、通道可用性、变量类型、质量时标可核 |
| 计算 | 定义 `Scale(raw,unit)`、报警规则能力 | 将 `Temperature` 接到换算与阈值判定 | 计算输出与上游测点质量/时间相关联 |
| 服务 | 提供只读监控查询/订阅 | 将处理后的变量映射到服务输出 | 订阅资源可释放、流控/超时可观察 |
| 验收 | 用虚拟设备、真实 PLC（单独授权）分别测试 | 在 Studio 查看帧/地址/变量/报警 | 错地址、断线、非法控制、质量异常不伪绿 |

**硬件安全**：PLC 控制写入与状态读取是不同能力；上位机写失败/超时，不能未经确认重复执行可能产生机械动作的指令。

### 16.8 本轮新增的四个贯穿完整场景（S-CN01～S-CN04）

#### S-CN01 TCP Client + Modbus TCP PLC

定义：一个 `TcpClient Channel`（SinglePeer/AsyncIO）；一个 `Modbus TCP ProtocolBinding`（Serial）；含 UnitId/寄存器地址的 PointTable 与 MDS VariableTable。开发者能直接调用协议操作，工程用户可选择通道/规约/点表并保存；异步 I/O 不强制专用线程。测试连接恢复、TID/UnitId 匹配、错误站号、错帧、VariableValue 时间/质量、重复 Write 禁止盲重试。**已迁移审查中的 Modbus Porting 文件目前不代表实际联通 PLC**。

#### S-CN02 TCP Server + 三个动态 SubChannel

一个 TcpServer Channel 监听端口 9000；动态识别 SubChannel A/B/C 并有各自 Generation，ProtocolDefinition 可共用，FrameParser/会话按流隔离；处理配置为 KeyedSerial，`MaxConcurrency=3`（用户显式设值，仅为示例）。A 连续发送帧 A1/A2，B 发送 B1，C 断开重连为 C′；断言 A 处理顺序不变，B 不被 A 长任务无界阻塞，A 回复不去 B，旧 C Session 请求不得发送到 C′。队列满必须显式拒绝/背压，Stop 后无僵尸客户端及残留线程。

#### S-CN03 RS-485 共享总线 + 三个 Modbus RTU 站号

一个 Serial Channel，Topology 为 SharedBus，ProtocolBinding Serial；`UnitId=1/2/3` 以 ProtocolAddress 表示，不创建三个 Socket/接收线程；全总线写有序、请求响应正确匹配。测试串口断线、站号超时、异步命令与自动重连、取消后迟到响应；不可将操作超时当作设备一定未执行。

#### S-CN04 无 Channel 的相机 SDK + C# 直接计算
一个无通道、无传统 Protocol 的 Camera Connector，SDK 必须同线程时使用 F19 DedicatedExecutor；触发与图像在专业 Driver 中管理，交付安全 `FrameHandle`；后续 Compute 直接调用标准算法或 MOC/MRK。TestCenter 明确 Live/Virtual/HIL 证据；源指针不因跨线程异步交付失效；无真实 SDK 时不能让假相机“Passed”。

### CASE-FM-G（V1.2）：工业机器人控制与状态拆分、Server 多客户端及 RS-485 多站号

| 案例 | 正确模块组织 | 不允许的替代方案 | 验收 |
|---|---|---|---|
| 工业机器人控制/状态分流 | `Connector(robot.command)` + `Connector(robot.status)`，由 MEM/MOL 组合 | 为图省事放在一个普通 Connector 两通道/双规约 | 独立配置/异常/生命周期、能力聚合准确 |
| TCP Server N 客户端 | **一个** TcpServer Channel + N 个 SubChannel | N 个独立 Connector/TCP Server Channel | 回包路由正确，断开重连 Generation 防串客户 |
| UDP Server N 远端 | **一个** UdpServer Channel + N 个 Peer/逻辑子通道 | 按远端重复启动 UDP Server 或重复 bind 端口 | Endpoint 身份、Datagram 边界、丢包统计 |
| RS-485 两站号 | 一个 Serial Channel、一个 Modbus RTU Protocol，PointTable 中 `StationAddress=1,2` | 两个 Serial、两个独立 Protocol 或把站号充当 SubChannel | 总线读写串行、帧站号正确 |
| SDK 探测器 | 一个 Driver，内部 SDK/必要传输为专业实现私有资源 | 强制外部组合两个 Connector 或 EmptyChannel | 配置与驱动生命周期简单，采图/安全另作 G2 验收 |

## 文档维护与验收工作包

- 主仓库：`https://github.com/oudream/cx-visual-x3`。
- 先看 `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00`；接口归位以 `DOCS/01～04` 为准。
- 新代码必须核实真实 Owner/源码、查看 `ReferTo` 原始成熟代码，**可直接重构融入，不加永久 Legacy/Porting Adapter**；每轮一个主题性中文提交。
- 验收分别记录 **G0 设计/类型、G1 Windows 编译与自动测试、G2 真实工业 I/O/HIL、G3 长期现场稳定与安全证据**。G0/G1 不能推导 G2/G3。

**待执行工作包**：逐类对象归属核查 → 现有源码能力差距 → 最小变更开发 → 单测/ArchitectureSmoke → Windows CI → TestCenter 双角色操作 → 设备/长稳证据 → 更新文档版本与工作台账。
