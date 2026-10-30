# CxVisual Automation——02 设备资源与工程对象模型
> 版本 V1.1｜2026-10-29｜性质：目标架构与开发合同，非功能完成声明；代码基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8 / WPF / x64。
> 适用范围：模型、设备、工站、点位、资产及运行身份；按 `AGENTS.md`、工程 Skill、`DOCS/00～04`、最新 Owner/ADR 执行。新增类型前必须审查 DOCS/02～04 XLSX 的原始 Sheet、真实 csproj 与源码。
> 状态规范：【S】本次已读源码，【R】旧仓资产，【T】目标设计，【V】真实CI/测试证据，【H】待HIL；不能以目标接口推断现有实现。
> 重要限制：本次 GitHub 文本接口无法解码 `DOCS/能力图/04Automation.png`、其余 Workbench PNG及 02～04 XLSX 二进制；位置/名称已核实但图内项目和 Sheet 内容尚待原图交叉校验。能力图不作为未经核实的逐条事实。

**总原则：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 1. 统一对象层次：五个不同身份
- **Model/Template**：产品型号的能力集、点表模板、参数 Schema；同型多个设备复用模型而不共用运行状态。
- **Engineering DeviceRef**：MEM 工程中的 Device、Station、Machine、Cell 等资源引用；身份可持久化且禁止被专有 Session 地址取代。
- **Physical Device**：有序列号/硬件标识/位置/安全域的现场实体；一个实体可有多个逻辑映射/协议视图。
- **Logical Device**：PLC数据块、虚拟计数器、机器人逻辑工具等；可无 Channel、无资产号，仍可被工程编排。
- **Runtime Session**：绑定 Provider、Generation、资源租约、Readiness/Health 的瞬时运行事实；重启 Generation 改变，但 DeviceRef 稳定。
## 2. 核心结构UML（角色，非批准类型）
~~~mermaid
classDiagram
 class MachineDef {+MachineId}
 class StationDef {+StationId}
 class EquipmentDef {+EquipmentId +DeviceRefs}
 class DeviceModelDef {+ModelId +Version +CapabilityRefs}
 class DeviceBindingDef {+DeviceRef +ModelRef +PhysicalRef}
 class PointBindingDef {+PointId +StableId +Source +Access +AddressRef}
 class DeviceRuntime {+Generation +Health +Lifecycle}
 class IMFMDriverModule {+DeviceRef}
 class PADDeviceVariableCatalog {+Register() +Bind()}
 MachineDef "1" o-- "*" StationDef
 StationDef "1" o-- "*" EquipmentDef
 EquipmentDef "1" o-- "*" DeviceBindingDef
 DeviceBindingDef --> DeviceModelDef
 DeviceBindingDef "1" o-- "*" PointBindingDef
 DeviceBindingDef --> DeviceRuntime : instance in runtime
 DeviceRuntime --> IMFMDriverModule
 PointBindingDef ..> PADDeviceVariableCatalog
~~~
`IMFMDriverModule` 和 `PADDeviceVariableCatalog` 是已存在对象；其他为建议职责，先查 MEM 现有 Node/Resource/FQN，禁止直接新增平行模型树。
## 3. 数据/配置/运行权责
| 内容 | 真源 | 更新权限 | 典型生存期 |
|---|---|---|---|
| 工程归属、Station层级、DeviceRef | MEM/M01组织资源 | Studio工程编辑/版本控制 | 跨运行 |
| Module/Driver配置/型号差异 | FCM + MFM Schema/专业扩展 | 经校验事务发布 | 跨运行 |
| 规约地址、站号、寄存器、点表绑定 | MCN/MPA | 校验后部署，避免热变更撕裂 | 生效版本 |
| Point/Signal当前值与质量 | MDS | 受限 Provider 提交 | 运行时 |
| Session、SDK句柄、线程/回调 | 专业 Provider + 宿主 MRK 资源 | Session Owner独占 | 一代 Generation |
| Frame/Signal 等大对象 | 专业数据 Owner + MDS Handle | Lease和消费确认 | 一次采集 |
| History/Audit | M35/公共诊断/安全审计 | 追加/保留策略 | 按业务 |
## 4. 模型/实例与点表
复用 device_communicator 的 Product→DeviceModelAttr→DeviceAttr→ChannelDeviceAttr 分离思想，但内部用现有 MEM Resource 和 MDS Definition；`PADDeviceVariableDefinition` 已有 StableId/DeviceName/VariableName/ValueType/Unit/Writable，`PADDeviceVariableCatalog` 已实现重复 ID/地址拒绝、InstallDefinitions、Bind。需要继续补充点表导入、协议地址/类型、原始值到工程单位换算、SourceTimestamp/Quality 及 BindingPlan 版本的可靠性，不能凭 `PAD...` 名字认为已经实现。
## 5. 常见拓扑的反例
1. PLC 一个站下多个 I/O 远程模块：物理资产各自唯一，采样可共享一个 Connector。
2. 一个 X-Ray 系统含 X-Ray Source/Detector/门控 PLC：物理安全域分别可见，控制动作受联锁，不能把三设备伪装成单个 TcpChannel。
3. 一个机器人控制器具有状态与命令两个独立端口：需要两个 Connector，但属于一个逻辑 Robot 设备。
4. 多站 RS-485：单 Serial/Protocol 与多个站号点；StationAddress 是规约地址，不能复制 Channel。
5. 型号升级导致字段改名：转换表与回滚记录，不能无提示覆盖实例的点位定义。
## 6. 设计约束与测试
设备身份和属性保持强类型、可序列化/可迁移、不可被运行快照反向修改；Group 嵌套必须检测环；DeviceRef→Session 一对多历史代次但任一时刻可操作 Owner 唯一；测试 DEV-001～010：创建/重开/复制/移动/删除引用、型号继承覆盖、双协议映射、重复点冲突、孤儿资源、跨实例串话、异常终止与回收。


---

## V1.1｜设备对象全模型、现实映射和变更合同
### 7. 从现实世界到工程对象的双向映射
~~~mermaid
classDiagram
 class ProjectResource {<<MEM existing owner>> +Ref +Version}
 class WorkcellDefinition {+StationRef +LayoutRef}
 class AssetDefinition {+AssetId +SerialNo +InstalledAt}
 class DeviceModel {+ModelRef +SchemaVersion +Capabilities}
 class DeviceDefinition {+DeviceRef +ModelRef +AssetRef +ConfigurationRef}
 class DeviceComponent {+ComponentRef +ParentRef +CapabilityRefs}
 class PointBinding {+StablePointId +DataType +Unit +ProtocolRef}
 class HardwareTopology {+NetworkNodeRef +SafetyZoneRef +PowerRef}
 class DeviceSession {+InstanceId +Generation +Health +Lease}
 ProjectResource <|-- WorkcellDefinition
 WorkcellDefinition o-- DeviceDefinition
 AssetDefinition <-- DeviceDefinition : stable asset link
 DeviceDefinition --> DeviceModel
 DeviceDefinition o-- DeviceComponent
 DeviceDefinition o-- PointBinding
 DeviceDefinition --> HardwareTopology
 DeviceSession --> DeviceDefinition : runtime binding
~~~
**所有新增名为职责候选**：MEM可能已有同义类/Ref，正式FQN先查04的Owner表及已编译源码，不允许凭此图生成平行 `DeviceManager` 或新增项目。
### 8. 18专业实体复用和 AssetId 单一性
| 共享实物 | 主实体归属 | 专业视角（不新增实物） |
|---|---|---|
| 气缸、阀、吸盘 | 设备安装资产/Machine部件 | Motion联动、Fluid行为、IO反馈、Maintenance寿命 |
| Robot、夹爪、托盘 | Robot/Workcell设备资产 | MaterialHandling物料路由、Vision视觉引导、Safety区域 |
| 反应釜/泵/调节阀 | 过程设备资产 | ProcessControl PV/SP/MV、SCADA监控、IoT边缘投影 |
| X-Ray源、Detector、冷却机 | 独立设备资产 | Acquisition流、Automation曝光、CT3D几何、Inspection结果 |
| PLC、远程IO、网络交换机 | 安装硬件资产 | Point/Signal、Protocol Session、维护健康 |
| 安全门/安全PLC/光幕 | 独立安全资产 | MachineSafety只读状态、HMI告警、运行许可 |
同一 AssetId 可引用多个专业能力；一个逻辑设备可由多个物理资产组成；同一通信 Channel 可服务多个逻辑 Device；绝不能强制 Device=Channel=Asset=Session 一对一。
### 9. 专业对象类别与非实体区分
**实体**：Equipment/Machine/Station/PLC/Axis/Robot/Camera/Detector/XRay/DAQ/Sensor/Actuator；**定义**：DeviceModel/Capability/Point/Recipe/Trigger/AlarmRule/TestProcedure；**运行会话**：DeviceSession/AcquisitionRun/MotionOperation/Batch；**证据**：Observation/FrameDescriptor/MeasurementResult/Trace/Calibration；**交互外观**：Faceplate/Panel/Symbol/Viewer，它们只引用专业对象。
### 10. 型号/配置/工程版本生命周期
- `ModelVersion` 描述型号能力及固件范围；`ProjectRevision` 描述设备分组、引用；`ConfigRevision` 描述本次运行参数；`DriverVersion/SDKVersion` 描述执行实现；`RuntimeGeneration` 描述一次会话。严禁把它们压成一个版本字符串。
- 引用稳定性：DeviceRef与 AssetId 不变；复制设备时生成新的 DeviceRef 和动态身份、复制可复用 ModelRef；删除有引用的设备必须拒绝或显式迁移；移动节点更新ParentRef而不改真实资产主身份。
- 生效步骤：编辑 Draft→Schema+引用校验→生产变更审批→冻结 Expected/Manifest→停止受影响 Operation→按安全点切换→Reopen/Readback→Ready；失败保留旧版本并标记现场状态未确认。
### 11. API和序列化示例（数据契约目标，不是现有可编译类型）
~~~text
DeviceModelDefinition {ModelId, SchemaVersion, CapabilityRefs[], DefaultParameterRefs[]}
DeviceInstanceRef {DeviceRef, ModelRef, PhysicalAssetRef?, ParentStationRef}
DevicePointBinding {StablePointId, DeviceRef, ProtocolPointRef?, ValueType, Unit, Access}
RuntimeDeviceBinding {DeviceRef, SessionId, Generation, EffectiveConfigVersion, ProviderTypeId, Health}
~~~
**验证**：原实例的设备编号与父工站可持久化；双设备同型号但触发/通道独立；同资产不同专业引用不复制；供应商 SDK 修改和 Schema 迁移有正反测试；运动与传感器数据能关联同一 DeviceRef。
### 12. 工程师使用体验
设备库（型号/能力搜索）→拖入工作站→选择SDK/Connector或已有通道→生成可编辑点表与绑定建议→检测重复/冲突→保存/预览应用差异→授权部署→实时查看实例/质量/连接/诊断；无 Provider 时仍可保留定义，但不得显示 DeviceReady。
