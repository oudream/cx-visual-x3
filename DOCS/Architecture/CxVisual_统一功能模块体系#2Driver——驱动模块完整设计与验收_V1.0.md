# CxVisual 统一功能模块体系#2Driver——驱动模块完整设计与验收 V1.0

> **文档类型**：专业模块完整目标规格；**生成日期**：2026-10-26；**来源基线**：旧完整规格 V1.2（承接 V1.1 六篇十八章及附录）、五类裁决 V1.2、Connector 裁决 V1.3、公共交互 V1.4。  
> **性质**：完整目标设计与验收规范，不是源码已实现声明；代码事实须以最新 `master`、实际 Windows CI 和设备 HIL 为准。  
> **原则**：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**  
> **范围**：真实设备 SDK / 设备生命周期与能力。  
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

## 01. Driver 不是另一种 Connector

**【裁决】Driver 负责真实硬件的专业能力、SDK 生命周期、安全及完整动作结果**；通用通信解析归 Connector，设备厂商特定硬件行为/SDK、专用采图/运动归 Driver。Driver 可以 Sdk / InternalTransport / Hybrid 三种实现，不要求公开 Channel/Protocol，更不得虚构已连接的 EmptyChannel。多个设备 Owner 的调度由 MRK 资源租约协作，不通过 Driver 自建调度器。

| 概念/对象 | 核心成员或行为 | 边界 |
|---|---|---|
| `MFMModuleKind.Driver` | 稳定模块分类 | 【源码已有】 |
| `IMFMDriverModule` | 描述关联的 DeviceRef/能力 | 【源码已有】接口按最新 HEAD 核实 |
| `MFMDriverModuleConfiguration` | `DriverTypeId/DeviceRef/Backend/InternalResourceRefs` | FCM 唯一正式 Section，不用 Editable 镜像 |
| `MFMDriverBackendKind` | `Sdk/InternalTransport/Hybrid` | 连接方式 ≠ 功能分类 |
| 专业 `Provider` | 初始化、曝光/采图、移动/回零、状态与关闭 | 各专业 Owner 实现；命名非现有通用 API 承诺 |
| 专业数据 Owner | Frame/Projection/Signal 的受控 Buffer/Lease | MDS/专业 Imaging/CT，非私有全局 ImageManager |

## 02. 状态、采图、控制与实际结果

1. **发现与初始化**：枚举设备、ID/序列号、驱动/固件版本、配置、授权、SDK DLL 与位数、缺省设备状态；任何缺失应返回明确不可用。
2. **采集**：触发模式（硬触发/软触发/自由运行）、像素格式/位深、帧尺寸/ROI、采图队列、时间戳、触发关联、帧丢失/重复/超时、回调线程隔离；帧以受控 Handle/Lease 提供消费者，不把 SDK 临时指针交下游长期使用。
3. **运动与危险动作**：绝对/相对运动、回零、停止、急停状态等由专业 Handler 声明安全边界；命令鉴权、模式/互锁/运动资源/操作追踪与物理完成反馈分层；上位软件不能替代安全 PLC、控制器或硬实时闭环。
4. **设备生命周期**：Initialize→Ready→Active/Busy→Stopping→Stopped/Faulted→Release；资源所有权、重入、取消、异常与回调注销全受控。重连后重新校准或复核的设备不能自动宣称 Ready。
5. **专业配置**：曝光、电压、电流、增益、相机帧率、分辨率、几何标定、SDK 安装路径、设备型号、触发模式等由专业配置和校验承载，保留版本与生效边界；敏感/危险设置受权限控制。

## 03. 原始数据的 Produces / Handles

| 方向 | 示例 | 正式语义 |
|---|---|---|
| Produces Variable | Camera.Ready/Busy、XRay.State、Axis.Position、Fault | M09 真正 State/Quality/Time，不以消息当永久变量 |
| Produces Data / Stream | Frame、ProjectionSeries、Encoder Signal | 专业 Owner+MDS Handle，持续流必须有背压与泄漏控制 |
| Produces Message | ImageAcquired、MotionCompleted、InterlockChanged | 轻量事件仅携带 Ref/Metadata，不能伪造现场动作 |
| Handles Command | Camera.Capture、Axis.MoveAbs、XRay.Expose、Stop | M12 唯一 Handler + MRK Operation + 专业联锁与物理确认 |

## 04. HIL 可靠性与安全闭环

必须验证 SDK 的 callback 异常、断开重连、拔 USB/网线、设备上电复位、DMA/共享缓冲持续采图、采集节拍、流量过载、超温、安全门、急停与 PLC 联锁，以及故障后状态未知/不可用。**模拟结果不能当作现场设备结果**；任何 Reset/Stop/Capture 成功需实际证据，命令超时默认 UnknownOutcome，不盲重试危险动作。历史 `ReferTo/DeviceVariable/DeviceInterfaces` 和成熟采图项目仅作直迁审查，不保留旧 DeviceManager 和适配层。

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

## 专项 TestCenter 验收清单（继承原 `TFD-001～012`）

| ID | 场景 | 必要证据 |
|---|---|---|
| DR-P01 | 厂商 DLL 缺失/位数不匹配/设备不在线 | 不得进入 Ready，不创建伪 Channel |
| DR-P02 | 单次采图/持续采图/多探测器 | FrameId/TriggerId、图像位深/Stride、Buffer Lease 可追踪 |
| DR-P03 | 相机软/硬触发与硬件动作 | 定时/同步误差、触发到帧时间、物理结果证据 |
| DR-P04 | 回零、运动、曝光、急停模式 | 资源租约、授权、安全 PLC 联锁、现场状态确认 |
| DR-P05 | SDK callback 重入、设备拔插、断电复位 | 无 UAF、无迟到回调操作已释放设备 |
| DR-P06 | 多设备共享硬件/GPU/接口 | 实例隔离、资源仲裁、关闭顺序正确 |
| DR-P07 | 真实 HIL + 现场长稳 | 设备序列号、固件、运行参数、Trace、故障恢复时间 |

## 与总体规范的关系及修改门禁

- **共性模型、两角色、版本、能力发现、统一交互、总体测试**以 `CxVisual_统一功能模块体系#0总体架构——完整设计与验收_V2.0.md` 为唯一解释。
- 本文是该模块专有行为与验收的权威文件；遇到旧源稿的相反描述，必须按文首覆盖表处理，并在文档里保留来源追溯。
- 任何新增正式接口/类名前须检查 `DOCS/02～04`、最新源码、现有 Owner、`ReferTo`；不能用概念示意代码替代编译测试。修改后同步更新总体文档的能力归属表与本模块 TestCenter 专项验证。


## 原 V1.2 来源条款、需求与案例（逐节承接）

> **来源历史提示**：下列原文保留其需求编号、专业能力和案例，便于追踪。凡涉及旧 `Advanced` 多组、历史“相机 SDK Connector”、未查重 API/状态的文字均只是历史上下文；**本文件前部的现行裁决优先**。

## 08A DriverModule——专用设备驱动模块（V1.2 新增正式章节）

### 08A.1 定位、边界与适用场景

DriverModule 提供**设备级能力**：设备识别与初始化、参数设置、采集/曝光/运动等专业动作、工作状态、硬件故障、SDK 事件、采集结果与设备资源所有权。典型实例：X-Ray 探测器、工业相机、运动控制卡、机器人专有 SDK、厂商采集卡。它不是单纯的“没有通道的 Connector”，**也不管理另一套 MDS/Message/Command/MRK**。

| 工作/行为 | 负责方 | 为什么 |
|---|---|---|
| TCP/UDP/Serial IO、Server Peer/SubChannel | MCC Channel | 真实传输/端点、缓冲、IO 状态 |
| 通用报文处理、点表读写、规约站号 | MPA Protocol / MCN Point | 通用数据解释和路由，Driver 可内部复用 |
| 相机连接与曝光、触发采图、帧回调、设备复位、驱动诊断 | DriverModule / Professional.Vision | 厂商设备语义、资源生命周期及授权 |
| 电机专有动作、机器人 Move/Enable/Home、硬件停机 | DriverModule / Professional.Automation + 物理控制器 | 设备操作与真正硬件安全 |
| 多个 Driver/Connector 的依赖和操作顺序 | MEM/MOL/MOC/MRK | 唯一工程身份、编排、构建和执行 |
| 设备运行值和流程堆栈 | MDS | 不再创建 DriverVariableManager |
| SDK/设备可操作性检查与 UI 配置 | FCM/MHS/MPW + TestCenter | 一份正式配置，不生成 EditableConfig 镜像 |

### 08A.2 三种驱动实现方式

1. **SDK-only**：不需要 Channel，例如厂商相机 SDK 自己管理数据链路/句柄。没有真实传输就**没有 EmptyChannel**；SDK 初始化成功不等于探测器已完成采图。
2. **InternalTransport**：专业驱动内部创建/使用经过授权的 Channel/Protocol 对象，按设备需要维护自己的控制与图像通路；工程编排者默认只配置设备相关参数，不得被迫理解子通道/数据帧。
3. **Hybrid**：SDK 控制 + TCP/UDP 状态或图像等混合；每条真实通信的资源所有权、关闭/重连、回调/缓冲租约都应明确，不重复创建隐藏传输层或包装 Adapter。

**示例**：X-Ray 探测器 Drive 配置包含设备编号、曝光配置、采图模式、提供者 TypeId，可按设备专业 Schema 追加网络或 SDK 具体参数；由 Driver 内部决定一个或多个通信 I/O。`ReadFrame/StartCapture/StopCapture` 通过 MOC 暴露为能力，真实启动走 MRK/宿主已批准路径；帧由专业 Owner 交给 MDS/共享内存/Compute，不复制 Buffer 仓库。

### 08A.3 驱动公共定义、身份与配置

- `MFMModuleKind.Driver` **枚举追加**，保留四类旧数值；`MFMModuleDescriptor` 与 `MFMModuleConfiguration` 仍为通用公共契约。
- `IMFMDriverModule : IMFMModule` 提供设备资源引用 `DeviceRef`，Driver 能力向唯一 `MOCCapabilityCatalog` 发布，不另建 DriverCapabilityCatalog。
- `MFMDriverModuleConfiguration` 为 `CxVisual.Module.Driver` 唯一 FCM Section：`DriverTypeId` 必填、`DeviceRef` 可选、`Backend` = Sdk/InternalTransport/Hybrid、`InternalResourceRefs` 可选且不可重复。
- 专业驱动可以**从上述正式配置派生专业 FCM Schema**（例如 X-Ray 曝光、相机序列号、触发模式），但每个设备配置实例只持有一种最终正式 Schema；不得再生成重复 Editable/Options/Adapter。专业类型可继续从 `ReferTo/DeviceVariable/DeviceInterfaces/DriverModuleBase.cs` 迁入核心能力，去除旧的全局 DeviceManager/VariableManager。
- MFM Driver 配置创建和保存只是工程/测试配置事实。没有可解析 Provider、不具备操作授权、未连接设备时，必须阻断真实运行操作；不可把保存成功冒充连接/采图成功。

### 08A.4 软件开发者使用期望

开发者可以编写一个原生 C# 驱动类型，直接管理 SDK/通信资源，实现 `Initialize/StartAcquisition/GetFrame/StopAcquisition/Shutdown` 等专业能力；普通 SDK Driver **不需要**构造空 Channel。具体 API 由专业驱动和已有 MOC/MRK 能力合同决定，不强行把所有摄像头/运动卡装入统一巨大 DriverBase。示意配置（已按本轮源代码目标命名；专业 Provider/采图函数仍需单独实现）：

```csharp
MFMDriverModuleConfiguration detector = new MFMDriverModuleConfiguration
{
    DisplayName = "X-Ray 探测器",
    DriverTypeId = "vision.detector.vendor-sdk",
    DeviceRef = "device.detector01",
    Backend = MFMDriverBackendKind.Sdk
};
detector.Validate();  // 仅配置有效；不代表 SDK 已加载或设备已连接
```

### 08A.5 工程编排用户使用期望

TestCenter/Studio 进入模块测试工作区 → 选择 Driver → 选择真实 Provider/设备资源 → 自动生成带注解的专业配置 → 保存/重开/验证 → 运行时按能力显示初始化、采图、停止、诊断等真实命令。**缺少 Provider 时配置校验必须失败；专业 Provider 未接入时禁用设备操作按钮。** 不显示一堆必须填写的通用 TCP/UDP/Modbus 字段。

### 08A.6 完整验收与反证（新增 TFD-001～012）

| ID | 内容 | 必须通过的行为/反证 |
|---|---|---|
| TFD-001 | Driver 为第五类模块且四旧枚举值不变 | 旧持久身份不受影响 |
| TFD-002 | Driver 正式 FCM Section 与自动编辑 | 与 MHS/MPW 共用唯一配置及 Schema |
| TFD-003 | 缺 DriverTypeId | 保存/结构验证拒绝，不伪造 Provider |
| TFD-004 | SDK-only 无 Channel | 允许创建配置，无 EmptyChannel 和虚假连通 |
| TFD-005 | InternalTransport 私有通道 | 驱动对真实底层资源负责，终止时确定性释放 |
| TFD-006 | Hybrid 控制与采图 | 控制/帧分别保留来源和所有权，异常互不误归因 |
| TFD-007 | 专业能力对 MOC 发布 | 设备动作与通用 Connector Read/Write 不混淆 |
| TFD-008 | 缺设备/权限/SDK | 不可启动真实硬件；界面展示拒绝原因 |
| TFD-009 | 相机采图缓冲 | 帧编号、触发、Lease 生命周期可查证，禁止数据伪造 |
| TFD-010 | 设备复位/停机 | 发出≠ACK≠PhysicalCompleted；需专业/物理安全证据 |
| TFD-011 | 配置保存/重开与版本 | Provider/DeviceRef/Backend 与实际配置一致 |
| TFD-012 | 源码直迁与 Windows CI/HIL | 从 ReferTo 直接重构、不过度 Adapter、G0/G1/G2 分级报告 |

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

### CASE-FM-B：X-Ray 在线检测（Driver + Compute + Service，可选 Simulation）

**目标**：真实或虚拟 Detector 帧进入图像预处理、AI/传统缺陷计算、规则融合、结果服务。`Driver(Detector)` → `Compute(Preprocess,AI,CV,Judge)` → `Service(Result)`；`Simulation(Detector/Trigger)` 是可选虚拟源。

**开发者**：保留原 `IRayDetectorDriver` SDK 回调的真实采图行为，对 Frame/Trigger/Time/BufferLease 作适配；复用已有 `CxVisionDataFlow` CPU 路径作为最低闭环；AI Provider 在模型/显存/版本/精度上独立验收；结果服务可查询并通过 Connector 上报 MES。

**工程用户**：选机台、探测器模式、产品检测规则、算法模型版本；画布将图像输出连到检测能力，将检测结果连判定/复检/上报。实时显示帧序、延迟、丢帧、SN 和结果，同时能从 Trace 回到原工程节点。

**失败链**：某帧 SDK 指针释放、尺寸/位深错误或 GPU OOM → 当前帧的计算失败，结果不误判 OK；后续 SN/Run 不串数据。MES 失败不会改变已确认检测结果，是否重发按幂等与业务审批。

**验收**：真实数据路径、回调有效期、类型化多消费者、图像预览、内存峰值、吞吐量、Windows x64 真构建、实机采图与 X-Ray 联锁分别证据化。

### CASE-FM-C：机器人 / 运动控制与仿真调试

**目标**：`Compute(Trajectory,CoordinateTransform)`、`Simulation(VirtualRobot/Axis)`、`Connector(RobotSDK/MotionCard)`，可选 `Service(MotionStatus)`，流程由 MOL/MRK 负责。

开发者先用普通算法单测逆运动学/坐标与轨迹约束，专业 Provider 负责 SDK 会话、命令/ACK/Readback、设备回零/联锁；Simulation 实现虚拟关节、速度、加速度、限位和故障。工程用户在同一流程绑定虚拟或生产目标，先 Virtual 运行并查看轨迹/时标，再申请 HIL/实机权限。

**验收必须分开**：轨迹数学正确、软件流程正确、Virtual 预期响应、真实设备实际位置和安全联锁。命令超时可能已在机械系统执行；没有可靠 Readback 不能做“重试同一运动命令”。

### CASE-FM-D：CT 采图、重建及体数据服务

**目标**：Connector 接收投影与运控反馈，Compute 调用 C++ CT Reconstruction / Image Processing，Simulation 提供投影/轨迹虚拟源，Service 对外发布 Job 状态和结果浏览定位。

1. **配置**：工程引用探测器/扫描几何/重建算法/体素尺寸/能力版本，逐任务固定快照；坐标、单位、数据布局与各 ABI 参数明示。
2. **扫描**：由专业运动控制器或采图触发链提供硬件级同步，上位 MRK 只做任务级组织；触发记录、FrameId、角度/位置映射可追踪。
3. **交接**：大块 Projection/Volume 通过 `cx-cct-x3` 的受控 SHM/ABI，元数据和 DataHandle 独立，跨进程引用不包含原始指针；过期句柄拒绝访问。
4. **计算**：重建算法报 JobId、进度、错误、取消和资源使用；成功才提交有效 Volume/Result，任务失败仍保留诊断且释放资源。
5. **服务**：按 SN/TaskId 查询状态和结果定位；Image/Volume Viewer 按需读取，不把整 8GB Volume 复制进 HMI 属性面板。
6. **仿真**：利用固定投影回放/模拟扫描验证几何与重建输出；Virtual 结果不得用作真实投影质量认证。
7. **反证**：未对齐的几何版本、丢角度、SHM 失效、GPU OOM、Native ABI 错版或取消未确认都不伪造成功。

**来源边界**：`cx-cct-x3/module_api`、`shm_api`、`service/*bridge` 为可复用的真实原生资产，现有 x3 未因此自动取得 CT 模块级验收。

## 文档维护与验收工作包

- 主仓库：`https://github.com/oudream/cx-visual-x3`。
- 先看 `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00`；接口归位以 `DOCS/01～04` 为准。
- 新代码必须核实真实 Owner/源码、查看 `ReferTo` 原始成熟代码，**可直接重构融入，不加永久 Legacy/Porting Adapter**；每轮一个主题性中文提交。
- 验收分别记录 **G0 设计/类型、G1 Windows 编译与自动测试、G2 真实工业 I/O/HIL、G3 长期现场稳定与安全证据**。G0/G1 不能推导 G2/G3。

**待执行工作包**：逐类对象归属核查 → 现有源码能力差距 → 最小变更开发 → 单测/ArchitectureSmoke → Windows CI → TestCenter 双角色操作 → 设备/长稳证据 → 更新文档版本与工作台账。
