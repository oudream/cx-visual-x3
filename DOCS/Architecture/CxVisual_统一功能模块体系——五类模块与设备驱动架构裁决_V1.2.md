# CxVisual 统一功能模块体系——五类模块与设备驱动架构裁决 V1.2

> 日期：2026-10-23；状态：正式架构裁决，源代码和 Windows CI 完成度分开记录。
>
> 本文件与完整全文《CxVisual 统一功能模块体系——完整设计与验收 V1.2》共同构成基线。V1.2 保留 V1.1 六篇十八章/附录 A～E，并新增 08A Driver、14.8 类型归位、16 专项案例、17.9 测试。原文中“四类模块”“设备 SDK 全部归 Connector”“多通道多规约是常规”等旧描述由本裁决覆盖。

## 一、正式五类模块

| ModuleKind | 职责 | 主要 Owner | 常规配置 |
|---|---|---|---|
| Connector | 通用 Channel/Protocol/PointTable、数据收发交互 | Mechanism.Connectivity | 默认一个父 Channel 和一个 Protocol |
| Driver | 真实设备动作、厂商 SDK、采图/运动、设备生命周期和诊断 | MFM 公共契约归 Mechanism.Core；专业实现归 Professional | DriverTypeId/DeviceRef/Backend，无强制 Channel |
| Compute | 算法、计算、规则求值和数据处理 | 专业 Compute Owner / MOC/MRK | 普通无状态函数可无模块实例 |
| Simulation | 仿真模型、时钟、回放、虚拟设备 | Professional.Simulation | 模型与时间配置 |
| Service | 长驻服务、请求响应、业务入口 | Mechanism.Lifecycle + 专业 | 依赖、并发、生命周期 |

不能因增加 Driver 再建立第二套工程/流程/数据/配置/运行/测试框架；复用 MEM、MOL、MOC、MRK/F19、MDS、FCM/MHS/MPW、TestCenter。Logic/Task/Composite/State/Control/Data 不升格为第六类模块。

## 二、五项边界修正

1. **X-Ray/相机/运动设备**：由 Driver 封装设备 SDK 或私有 TCP/UDP/控制/采图通道；工程用户面对设备参数和专业能力，不要求手工组装多个 Connector。没有实际 Channel 就不建 EmptyChannel，也不伪造已连接。
2. **工业机器人控制与状态**：常规拆分为两个 Connector（独立规约、连接和故障域），由 MEM/MOL/MRK 组合。如通过厂家 SDK 发 Move/Home 等专业动作，则可以使用专业 Driver。
3. **TCP Server 与 UDP Server 一对多**：属于同一个父 Channel 管理多个子连接/Endpoint。SubChannelId/Generation 区别会话；客户端数量不是父 Channel 数量。目前 UdpServer 仅有类型和能力描述，真实 I/O 需另做专业回归。
4. **RS-485 多站号**：一个 Serial + 一个 Modbus RTU Protocol + 一个 PointTable；不同点使用 MCNPointDefinition.StationAddress。站号是规约地址，不是通道子通道，也不必为多个站重复创建 Connector。
5. **Connector 默认简洁**：MCNConnectorCompositionMode.Standard 默认最多一个 Channel/Protocol；仅冗余或紧耦合复合接入显式启用 Advanced。若多个规约同 Channel/SubChannel 争抢收流，配置应报错；多个独立通信目的优先用多个模块和已有工程编排。

## 三、Driver 设计（设备职责不等于无通道）

**SDK**：厂商 DLL 自管理底层连接，Driver 不含通用 Channel。**InternalTransport**：Driver 内部使用现成 Channel/Protocol，无额外 Adapter。**Hybrid**：SDK 与 TCP/UDP/共享内存共同存在，资源所有权、缓冲 Lease、回调、设备关闭必须明确。

Driver 的主要行为是 Initialize、StartAcquisition、GetFrame、StopAcquisition、Shutdown、GetHealth（均为专业提供者能力示例，不声明为当前已编译公共 SDK 方法）。状态、权限与结果由专业 Driver 负责，MOC 只负责唯一能力发现，MRK 负责工程执行，MDS 负责数据管理。发送成功≠设备完成，尤其是 X-Ray 曝光和机械运动。

公共代码：MFMModuleKind.Driver（枚举追加，旧四种数值保持不变）；IMFMDriverModule（DeviceRef）；MFMDriverModuleConfiguration（一个正式 FCM 配置）。后者有 DriverTypeId、DeviceRef、Backend、InternalResourceRefs，不强制外部 Channel/Protocol。专业 Provider 可以继承正式配置表达曝光、图像尺寸、设备序列号等专有参数，但不能再造 EditableConfig 或其它双份配置真源。设备对象本身仍归 MEM/Professional。

## 四、配置与代码归位

| 类型或变动 | Owner | 配置或职责 |
|---|---|---|
| MFMModuleKind.Driver | Mechanism.Core.Modules | 追加五分类，兼容原四项整数值 |
| MFMDriverModuleConfiguration | Mechanism.Core.Modules | FCM Section：CxVisual.Module.Driver；必须有 DriverTypeId，资源引用不重复 |
| MFMDriverBackendKind | Mechanism.Core.Modules | Sdk / InternalTransport / Hybrid |
| MCNConnectorCompositionMode | Mechanism.Connectivity.Connectors | Standard / Advanced，默认为 Standard |
| MCCChannelKind.UdpServer | Mechanism.Connectivity.Channels | 一对多 Datagram 类型，保留旧枚举值 |
| MCCSubChannelAddress | Mechanism.Connectivity.Channels | 父子通道身份、Generation |
| MCNPointDefinition.StationAddress | Mechanism.Connectivity.Connectors | 规约点站号，Modbus 1～247 |
| MCNConnectorConfigurationValidator | 原 Connector Owner | 多组件门禁、双规约同路径冲突、站号非法等正反验证 |
| TCTManualModuleFrame | Tests/TestCenter | 一个 Frame 增加 Driver 页，复用 FCM/MHS/MPW 和统一操作中心 |

开发者使用：采图 Driver 只需要真实 ProviderTypeId、设备身份及实际专业参数；默认不产生假 Channel。工程编排者通过 TestCenter/Studio 选择 Driver 或 Connector：Connector 配通信与点表，Driver 配设备专有能力；点击保存只表示持久化配置，不代表设备已连接或采图成功。

## 五、验收及边界

新增 TFD-001～012：五类身份/配置注解；缺 Provider 拒绝；无 Channel SDK；私有/混合通信的资源释放；能力发现；安全权限；采图缓冲；设备复位完成反馈；配置保存重开；成熟驱动直迁及现场验收。

新增 TFC-033～037：标准一组；高级受控多组和同路径冲突；TCP/UDP Server 多对端；RS-485 多站号合法/非法；Driver SDK 无通道且缺 Provider 不伪成功。

G0：类、Owner、代码与结构存在；G1：FCM/MHS、ArchitectureSmoke、Windows CI 正反测试；G2：真实 TCP/UDP/Modbus、相机采图/运动设备及 HIL。绝不以 G0/G1 冒充 G2。

**复用原则**：ReferTo/DeviceVariable/DeviceInterfaces/DriverModuleBase.cs 为成熟 C# 来源，具有 PointSheet、读写属性、动态采集、专有设备等能力；后续按 Professional Owner 直接重构，不能抄回旧全局 DeviceManager、VariableManager 或为了迁移增加一层 Legacy Adapter。GCL3 Terminal/Protocol 的职责分离经验可参考，但不照搬复杂旧继承链。

**兼容提示**：Connector 旧多通道配置若没有 Advanced 标记，升级后须经显式迁移/审查，不能静默误判为 Standard；历史 MHS 配置文件需有真实旧文件 Fixture 回归，当前源码结构校验不能代替迁移成功证据。
