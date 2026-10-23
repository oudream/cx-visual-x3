# CxVisual 四模块唯一配置模型收敛与注解核验 V0.4

> 2026-10-23 | master | 一套配置定义、多种使用方式；ReferTo 只读、不增加项目、Adapter 或配置管理器。
> 源码完成及 CI 状态必须分开；Windows 编译/UIA 结果以本次提交 SHA 的 Actions 为准。

## 1. 重复配置收敛表

| 重复或不完整的配置 | 唯一正式配置类型 | 裁决 |
|---|---|---|
| MCNConnectorConfiguration + MCNConnectorEditableConfig | MCNConnectorConfiguration | FCM Section、List 及跨成员 Validator 直接归正式类，删除 Editable 镜像 |
| MCCChannelProfile + MCCChannelConfiguration Options + MCNEditableChannel | MCCChannelConfiguration | 同一类保存 Id/Kind、TCP/UDP/Serial、子通道、线程、缓冲及重连配置；ChannelTypeDescriptor 仅保留类型元数据 |
| MPAProtocolBinding + DispatchOptions + MCNEditableProtocol | MPAProtocolBinding | 通道/子通道、规约地址、调度参数归一个配置；Session/Descriptor 归运行态 |
| MCNPointDefinition/Binding + MCNEditablePoint | MCNPointDefinition | 一条点保存地址、规约引用、类型和数据目标；MCNPointTable 是运行索引 |
| PVCComputeExecutionOptions + PVCComputeEditableConfig | PVCComputeModuleConfiguration | 唯一计算配置，运行 Profile 引用此配置 |
| PSMSimulationDefinition + PSMSimulationEditableConfig | PSMSimulationModuleConfiguration | 唯一仿真模型与时间配置，运行模型和时钟仍独立 |
| MLCServiceModuleOptions + MLCServiceEditableConfig | MLCServiceModuleConfiguration | 唯一服务配置，服务基类直接引用 |
| MFMModuleEditableConfig | MFMModuleConfiguration | 公共注解配置基类，保留启用/名称及能力引用 |
| MCCReconnectPolicy | MCCChannelConfiguration | 重连参数并入 Channel 正式配置，不保留孤立的第二配置对象 |

## 2. FCM 注解与配置完整性

- 四种模块正式类型持有 FCMConfigSection；内嵌 Channel/Protocol/Point 成员均具有 FCMConfigItem 注解。
- 标量范围使用 Range，必要身份使用 Required；Connector 跨成员校验使用现有 FCMConfigValidator。
- 工程资源身份来自 MHS/MEM Scope；MFMModuleIdentityConfig 是唯一模块类型身份锁而非另一份模块参数集合。
- Descriptor、Session、Clock、运行索引属于运行对象/只读元数据，不能当成需要 FCM 注解的持久配置。
- MDSVariableDefinition 目前以 CLR Type 作为运行元数据；后续如需作为工程配置，应由 MDS 定义稳定类型 Id，不能直接序列化 System.Type。

## 3. 兼容与边界

- 四种 SectionId 保持 CxVisual.Module.Connector / Compute / Simulation / Service，不另设 Editable Section。
- Connector 字段路径 Channels[].Kind、Protocols[].Kind、Points[].Address/ValueType 等保留，减少旧 TestCenter 配置的迁移成本。
- 本次源文件类型更名及原子配置语义调整对历史 MHS 文件的完全兼容仍需使用历史配置 fixture 验证，不冒充已经生产迁移。
- Studio/TestCenter 仍使用原 FCM → MHS → MPW 自动界面，不再存在 Editable → Options/Definition 二次转换。
- 仍待完善：通用资源引用下拉、条件显隐、专业类型目录选择，以及正式 MEM Module 工程资源登记。
- 本轮不声称 TCP/UDP/Modbus、业务运行、消息/命令或 HIL 实机功能已经完成。

## 4. 验收

- 同步更新 ArchitectureSmoke：构造与校验直接使用唯一正式配置，覆盖重复 Channel、悬空 Protocol/Point、I/O 不兼容、并发非法、变量隔离、仿真和服务状态。
- FCM 五个 Section 的 Schema/TypeCatalog 往返、MHS Commit 并重新开启、TestCenter WPF UIA 均以实际 Windows CI 为准。
- 不新增项目，不修改 ReferTo，待通过 Windows CI 后才可称本批功能验收通过。
