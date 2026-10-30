# CxVisual Automation——10 完整类、接口、枚举字典与 API 合同 V1.0
> 2026-10-30｜**完整架构候选类清单，不是已编译类数量或下达立即新建类指令**。与 [#09 UML](CxVisual_专业自动化体系%2309全类总体架构UML与对象交互合同_V1.0.md) 对照。
> 源：Automation #00～#08 V1.1、五张核心规格 V2.1、01～03统一规格 V2.2 的18专业、105物理子类/1409实体映射（设备现实分类），本次 `Professional/Automation/*.csproj`及相关真实源码。未直接解码当前GitHub DOCS/02 V4.9/03 V2.01/04 V6.4/04Automation.png；完整Pxx映射保留Gate。
> 类型状态：`E`已核对现行源码并在正式编译清单/引用中；`O`公共Owner已有职责；`C`此稿仅为建议的专业候选，尚未批准或编译。`C`类型是否独立生成由业务实装时查重与Owner裁决。Legacy/ReferTo源码不可算E。

## A. 标准与设计约束
1. 表中 C 候选**只在指定 Professional.Automation 领域命名空间中有设计身份**；不是要求一次生成 `.cs`。枚举可按技术与兼容性改用已有状态/Kind，纯值对象可内嵌或裁撤。
2. 同一个硬件 Asset/DeviceRef 只建立一次；Model/Binding/Session/Point/Value/Command/Evidence 相互分开；没有公共 Manager/CommandCenter/History/Graph/Channel 的候选。
3. 建议 TypeId/Name/Namespace/FQN 同时查重，方法调用与安全/资源/诊断必须复用机制。`WPxx` 是 Implementation 工作包，`AUT-*` 是 Testing 测试编号。
4. 类型Kind：`class`是静态配置/结果/快照等数据类，`interface`仅在多供应商/平台与SDK隔离确需，`enum`仅表达专业语义。生产代码遵守 .NET Framework4.8 的已有 C# 编码与序列化约束。

## B. 现有公共类 + Automation已编译类（事实清单）

## 00 现有正式类型（事实核对，不改名）

**建议/现有命名空间**：`CxVisual.Professional.Automation`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | 实现Owner或验收 |
|---|---|---|---|
| `CxAutomationDeviceOptions` | E·class | Host/Port/Station/Timeout；配置与校验 | 保留现有FCM接入；AUT-DE-001 |
| `PADDeviceVariableDefinition` | E·class | StableId/DeviceName/VariableName/ValueType/Unit/Writable/Address | 现有MDS定义映射；AUT-PI-001 |
| `PADDeviceVariableCatalog` | E·class | Register/FindById/Find/Snapshot/InstallDefinitions/Bind | 不保存Current；AUT-DE-002 |
| `PADDeviceVariableProvider` | E·class | Start/Stop/Emit/WriteAsync/Dispose | WriteAsync(bool)不等于物理完成；AUT-PI-002 |
| `IPADDeviceVariableWriteBackend` | E·interface | WriteAsync(definition,value,cancellationToken) | 已有后端注入，缺省不允许写；AUT-PI-002 |
| `PADDeviceVariableMetadataAdapter` | E·static class | ToRuntimeDefinition(definition) | MDS单向适配；AUT-DE-003 |
| `PADLegacyVariableChange` | E·class | DeviceName/VariableName | 旧通知不含旧值快照；AUT-DE-004 |
| `PADLegacyVariableManagerBridge` | E·class | AcceptChanges(events) -> int | 只桥接LatestOnly；AUT-DE-004 |

## 00 公共机制已有类型（只复用，不算Automation新增）

**建议/现有命名空间**：`CxVisual.Foundation / Mechanism`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | 实现Owner或验收 |
|---|---|---|---|
| `IFTRChannel` | O·interface | 底层Open/Send/Receive/Close/Diagnostics | F17唯一Channel；AUT-IC-001 |
| `IMCNConnectorModule` | E·interface | RequestAsync/ReadDevicePointAsync/WriteDevicePointAsync | Mechanism.Connectivity；AUT-IC-001 |
| `MPAPointDefinition` | E·class | 专业点表与地址语义 | MPA管理协议点，不与PAD定义混淆 |
| `MPAProtocolOperationRequest` | E·class | 规约Request/Correlation | 不可替代M12设备命令 |
| `MFMDriverModuleConfiguration` | E·class | DriverTypeId/DeviceRef/Backend/InternalResourceRefs | Mechanism.Core中公共Driver配置 |
| `IMFMDriverModule` | E·interface | DeviceRef与IMFMModule | MFM公共合同 |
| `MFMDriverBackendKind` | E·enum | Sdk/InternalTransport/Hybrid | 驱动实现类型 |
| `IMDSVariableProvider` | E·interface | Start/Stop/ProviderId | MDS唯一变量值 |
| `MDSVariableTable` | E·class | Define/Definitions/Contains | MDS工程变量定义 |
| `MDSVariableProviderSample` | E·class | value/quality+provider sample | 不能将源时间凭空假定已具备 |
| `MDSVariableQuality` | E·enum | Good等质量状态 | 真实成员按代码核对 |
| `TCTDriverReadinessPanel` | E·WPF class | Driver/SDK/Port probes | 现有TestCenter，未实机就绪 |

## 01 DeviceEngineering 设备工程与生命周期

**建议/现有命名空间**：`CxVisual.Professional.Automation.Devices`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | 实现Owner或验收 |
|---|---|---|---|
| `DeviceModelSpec` | C·sealed class | ModelId/ModelVersion/CapabilityIds/ParameterSchemaRef | Model模板，不复制MEM工程树；WP01 |
| `DeviceBindingSpec` | C·sealed class | DeviceRef/ModelRef/AssetRef/StationRef/ConfigRef | 持久引用/校验；WP01 |
| `EquipmentTopology` | C·sealed class | ParentRef/ComponentRefs/NetworkRefs/SafetyZoneRefs | 工站结构视图；WP01 |
| `HardwareAssetIdentity` | C·sealed class | AssetId/SerialNumber/Vendor/FirmwareVersion/Installation | 资产身份与运行分离；WP01 |
| `DeviceCapabilitySpec` | C·sealed class | CapabilityId/InputSchema/OutputSchema/SafetyRequirements | 复用MOC Descriptor；WP01 |
| `DeviceComponentBinding` | C·sealed class | ComponentRef/DeviceRef/AssetRef/CapabilityRef | 同一物理资产多专业引用；WP01 |
| `DeviceRuntimeBinding` | C·sealed class | DeviceRef/SessionGeneration/ProviderTypeId/EffectiveVersion | 运行关联，不得写回MEM；WP01 |
| `PADeviceSession` | C·sealed class | OpenAsync/ProbeAsync/GetHealth/StopAsync/Dispose | 实例级资源/回调所有权，复用MRK生命周期；WP01 |
| `IDeviceRuntimeBackend` | C·interface | ProbeAsync/ExecuteAsync/GetReadiness | 只有真实SDK/多实现需要此接口；WP01 |
| `DeviceReadinessEvidence` | C·sealed class | DeviceRef/Quality/ProtocolReady/HardwareReady/ObservedAt | 区分Socket/Device/Physical；WP01 |
| `DeviceFaultDescriptor` | C·sealed class | DeviceRef/VendorCode/Severity/RecoveryHint | 专业故障编码，不复制日志中心；WP01 |
| `DeviceOperatingMode` | C·enum | Auto/Manual/Teach/Maintenance/Disabled | 专业模式，不复刻Runtime状态；WP01 |

## 02 PlcIoFieldbus PLC与IO

**建议/现有命名空间**：`CxVisual.Professional.Automation.Plc`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | 实现Owner或验收 |
|---|---|---|---|
| `PlcControllerSpec` | C·sealed class | DeviceRef/CpuModel/Firmware/ScanCycle/ProtocolRefs | PLC专业硬件模型；WP01 |
| `PlcRackSpec` | C·sealed class | RackId/SlotRefs/BackplaneRef | PLC机架，关联既有资产；WP01 |
| `PlcModuleSpec` | C·sealed class | Slot/ModuleType/ChannelRefs/Revision | RemoteIO/本地IO；WP01 |
| `IoSignalDescriptor` | C·sealed class | StablePointId/Direction/Type/Unit/QualityRule | 不存当前值；WP01 |
| `PlcPointMapping` | C·sealed class | SignalId/ProtocolPointRef/Station/BitIndex/Endian | MPA地址的专业引用；WP01 |
| `PlcScanPolicy` | C·sealed class | PollInterval/Deadband/BatchLimits/Timeout | 低频软件轮询不承诺硬实时；WP01 |
| `IPlcPointOperations` | C·interface | ReadAsync/RequestWriteAsync/ReadbackAsync | 按M12和MCN实现，不复造命令中心；WP02 |
| `PlcWriteParameters` | C·sealed class | PointRef/TypedValue/ExpectedOldValue/Deadline | 敏感点/只读/类型校验；WP02 |
| `PlcReadbackEvidence` | C·sealed class | PointRef/RawValue/EngineeringValue/Quality/DeviceTime | ACK≠Observed；WP02 |
| `PlcControllerStatus` | C·sealed class | RunState/FaultCode/ProtocolHealth/Mode | 控制器快照；WP02 |
| `IoSignalDirection` | C·enum | DI/DO/AI/AO/Counter/Temperature | 只表示专业方向；WP01 |

## 03 ScadaIcs 工业监控与SCADA

**建议/现有命名空间**：`CxVisual.Professional.Automation.Scada`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | 实现Owner或验收 |
|---|---|---|---|
| `ScadaSiteSpec` | C·sealed class | SiteRef/AreaRefs/StationRefs/TimeZone | 组织映射，MEM是真源；WP06 |
| `ScadaTagSpec` | C·sealed class | TagId/DeviceRef/PointRef/Unit/Deadband | 标记与物理测点分开；WP06 |
| `TelemetryBinding` | C·sealed class | TagId/SourcePointRef/QualityPolicy/TimePolicy | MDS观测来源绑定；WP06 |
| `ScadaControlSpec` | C·sealed class | ControlPointRef/Mode/SetpointLimits/PermitPolicy | 遥控遥调约束；WP06 |
| `ScadaAlarmSourceSpec` | C·sealed class | SourceRef/Severity/ConditionRef/Delay/AckPolicy | 报警来源，不复制M35；WP06 |
| `ScadaAlarmStateSnapshot` | C·sealed class | Active/Acknowledged/Suppression/SourceTime/Quality | 报警状态，避免等同诊断日志；WP06 |
| `ScadaSoeEvent` | C·sealed class | SourceEventId/DeviceTime/ReceiveTime/Quality/Sequence | SOE时序和来源；WP06 |
| `ScadaHistorianMapping` | C·sealed class | TagRefs/RetentionPolicyRef/StoreRef | 历史储存由公共M35执行；WP06 |
| `IScadaTelemetryProvider` | C·interface | PublishObservationAsync/ReadStatusAsync | 必要时将专业Tag绑定到MDS；WP06 |
| `ScadaAlarmTransition` | C·enum | Raised/Acknowledged/Returned/Suppressed | 不引入独立全局EventBus；WP06 |

## 04 MotionCnc 运动控制与CNC

**建议/现有命名空间**：`CxVisual.Professional.Automation.Motion`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | 实现Owner或验收 |
|---|---|---|---|
| `MotionControllerSpec` | C·sealed class | ControllerRef/AxisRefs/Capabilities/SdkOrProtocolRef | 绑定控制器而非复制设备资产；WP03 |
| `AxisSpec` | C·sealed class | AxisRef/Unit/Resolution/SoftLimits/HomeRef | 运动参数与版本；WP03 |
| `AxisGroupSpec` | C·sealed class | GroupRef/AxisRefs/CoordinateFrameRef | 同组资源独占/协调；WP03 |
| `MotionCoordinateFrameRef` | C·sealed class | FrameRef/CalibrationVersion | 坐标变换调用Foundation.Geometry；WP03 |
| `MotionProfileSpec` | C·sealed class | Speed/Acceleration/Deceleration/Jerk/Unit | 不得静默变更控制器轨迹；WP03 |
| `HomeParameters` | C·sealed class | AxisRef/Mode/Direction/Speed/Offset | 真实限位和原点；WP03 |
| `AxisMoveParameters` | C·sealed class | AxisRef/Target/FrameRef/ProfileRef/Tolerance | MoveAbs/Rel请求的专业参数；WP03 |
| `MotionPathSpec` | C·sealed class | Waypoints/FrameRef/ToolRef/BlendOptions | 轨迹提交给硬控制器；WP03 |
| `PositionCompareTriggerSpec` | C·sealed class | AxisRef/Position/Edge/TriggerRef | 硬件位置比较触发；WP04 |
| `CncProgramSpec` | C·sealed class | ProgramId/Revision/ToolTableRef/WorkOffsetRef | 控制器程序版本/刀补；WP03 |
| `IMotionDeviceOperations` | C·interface | HomeAsync/MoveAsync/StopAsync/ReadStatusAsync | 专业Provider，同M12/MRK组合；WP03 |
| `MotionStatusSnapshot` | C·sealed class | ActualPos/CommandedPos/FollowingError/ServoState | 基于控制器真实反馈；WP03 |
| `MotionCompletionEvidence` | C·sealed class | Target/Actual/DoneCode/WithinTolerance/ObservedAt | 物理到位≠Send成功；WP03 |
| `MotionOperationKind` | C·enum | Home/Jog/MoveAbs/MoveRel/Interp/Stop | 动作选择；WP03 |
| `MotionExecutionState` | C·enum | Idle/Moving/InPosition/Unknown/Faulted | 专业子状态；WP03 |

## 05 FluidPowerActuation 气液压/真空

**建议/现有命名空间**：`CxVisual.Professional.Automation.Fluid`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | 实现Owner或验收 |
|---|---|---|---|
| `FluidCircuitSpec` | C·sealed class | CircuitRef/SupplyRef/PressureLimits/Topology | 空气/液压回路专业配置；WP03 |
| `CylinderSpec` | C·sealed class | CylinderRef/ExtendFeedback/RetractFeedback/ValveRef | 双到位与超时；WP03 |
| `ValveIslandSpec` | C·sealed class | ValveRefs/IoMappings/FailsafePolicy | 通过PLC/IO控制；WP03 |
| `VacuumUnitSpec` | C·sealed class | GeneratorRef/CupRefs/VacuumThreshold/LeakPolicy | 物理吸附/脱落证据；WP03 |
| `HydraulicPowerUnitSpec` | C·sealed class | PumpRef/PressureRange/Temperature/ReservoirRef | 液压站状态；WP03 |
| `CylinderActionParameters` | C·sealed class | CylinderRef/TargetPosition/Deadline | 伸缩专用动作；WP03 |
| `FluidFeedbackSpec` | C·sealed class | SensorRefs/PressureBand/StableDuration | 可选压力+开关组合证明；WP03 |
| `IFluidActuationOperations` | C·interface | ExtendAsync/RetractAsync/VacuumOnAsync/ReadStatusAsync | 离散执行与轴分离；WP03 |
| `FluidActuationEvidence` | C·sealed class | FeedbackStates/ActualPressure/StableAt/Quality | 指令应答非动作成功；WP03 |
| `FluidActuationState` | C·enum | Retracted/Extending/Extended/Retracting/Unknown/Faulted | 专业状态图；WP03 |
| `FluidCircuitKind` | C·enum | Pneumatic/Hydraulic/Vacuum | 设备族分类；WP03 |

## 06 RobotEngineering 工业机器人

**建议/现有命名空间**：`CxVisual.Professional.Automation.Robotics`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | 实现Owner或验收 |
|---|---|---|---|
| `RobotSpec` | C·sealed class | RobotRef/ControllerRef/JointRefs/ToolRefs/FrameRefs | 物理资产单一引用；WP05 |
| `RobotJointSpec` | C·sealed class | JointId/Type/Limits/DriveRef | Robot轴语义不重复Motion物理轴；WP05 |
| `RobotToolSpec` | C·sealed class | ToolRef/TcpFrameRef/Payload/Version | 工具与负载；WP05 |
| `RobotWorkObject` | C·sealed class | WorkObjectRef/FrameRef/CalibrationVersion | 工件基准与标定；WP05 |
| `RobotPoseSpec` | C·sealed class | FrameRef/Position/Orientation/JointValues | 几何运算复用Foundation；WP05 |
| `RobotPathSpec` | C·sealed class | MotionKind/Waypoints/ToolRef/Blend | 轨迹控制器执行；WP05 |
| `RobotProgramRef` | C·sealed class | ProgramId/Revision/ControllerRef | 机器人程序资源引用；WP05 |
| `RobotActionParameters` | C·sealed class | TargetPose/FrameVersion/Speed/ToolRef | 模式与版本前置校验；WP05 |
| `IRobotDeviceOperations` | C·interface | MoveJAsync/MoveLAsync/RunProgramAsync/GetPoseAsync | SDK/厂商Provider多实现才建接口；WP05 |
| `RobotExecutionEvidence` | C·sealed class | ActualPose/ToolVersion/ProgramStatus/ObservedAt | 控制器真实反馈；WP05 |
| `RobotOperationKind` | C·enum | MoveJ/MoveL/RunProgram/Gripper/Stop | 专业命令类型；WP05 |
| `RobotRunMode` | C·enum | Auto/Manual/Teach/SafeStopped | 与机器安全反馈区分；WP05 |

## 07 MaterialHandlingSystems 搬运供料

**建议/现有命名空间**：`CxVisual.Professional.Automation.Handling`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | 实现Owner或验收 |
|---|---|---|---|
| `ConveyorSpec` | C·sealed class | DeviceRef/Segments/SpeedLimit/SensorRefs | 输送段与速度，复用Motion设备；WP03 |
| `FeederSpec` | C·sealed class | FeederRef/Capacity/LevelSignalRef/FeedMode | 缺料/卡料；WP03 |
| `BufferSlotSpec` | C·sealed class | SlotRef/OccupiedPoint/CarrierRef | 站点占用；WP03 |
| `FixtureSpec` | C·sealed class | FixtureRef/ClampRefs/PartTypeRefs | 夹具与Fluid交接；WP03 |
| `CarrierIdentity` | C·sealed class | CarrierId/PartId/RfidOrBarcode/Revision | 物料身份与追踪；WP03 |
| `MaterialTransferSpec` | C·sealed class | SourceStation/TargetStation/Route/HandshakePoints | 工位握手定义；WP03 |
| `MaterialTransferParameters` | C·sealed class | CarrierId/TransferId/Deadline/ExpectedSlot | 重复料/双料检测；WP03 |
| `IMaterialTransferOperations` | C·interface | ReserveAsync/TransferAsync/ObserveHandoverAsync | 专业行为，Runtime负责流程；WP03 |
| `MaterialHandoverEvidence` | C·sealed class | SourceReleased/TargetAccepted/CarrierId/ObservedAt | 双端已确认才完成；WP03 |
| `MaterialTransferState` | C·enum | Requested/Ready/InTransit/Received/Unknown/Blocked | 专业状态；WP03 |

## 08 ProcessControl 连续/顺序过程

**建议/现有命名空间**：`CxVisual.Professional.Automation.Process`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | 实现Owner或验收 |
|---|---|---|---|
| `ProcessUnitSpec` | C·sealed class | UnitRef/AssetRefs/OperationRefs/ParameterRefs | 釜/炉/泵/阀工艺模型；WP06 |
| `ProcessVariableSpec` | C·sealed class | PvPointRef/SpPointRef/MvPointRef/Unit/Range | PV/SP/MV 分离；WP06 |
| `ProcessLoopSpec` | C·sealed class | LoopRef/ControllerRef/AlarmRules/ControlMode | PID实时执行留PLC/DCS；WP06 |
| `ProcessPhaseSpec` | C·sealed class | PhaseId/Preconditions/CompletionCriteria/Timeout | 专业Phase，公共Decision编排；WP06 |
| `ProcessSequenceSpec` | C·sealed class | PhaseRefs/Transitions/RecipeRef | 不建独立流程引擎；WP06 |
| `ProcessInterlockSpec` | C·sealed class | ConditionRefs/TripRef/Mode/Severity | 非认证软件阻断与真实SIS边界；WP06 |
| `ProcessSetpointParameters` | C·sealed class | LoopRef/Setpoint/Unit/ExpectedMode/Deadline | 受控遥调；WP06 |
| `IProcessControlOperations` | C·interface | StageSetpointAsync/ReadbackAsync/ObservePhaseAsync | 实际控制器驱动完成；WP06 |
| `ProcessPhaseEvidence` | C·sealed class | PhaseId/ObservedPv/ControllerStatus/TimeRange | 可追溯阶段事实；WP06 |
| `ProcessControlMode` | C·enum | Manual/Auto/Cascade/Maintenance | 与安全模式不同；WP06 |


## C. 其余10个专业领域的全类合同

### 09 AcquisitionTrigger 相机/探测器/触发

**候选命名空间**：`CxVisual.Professional.Automation.Acquisition`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | Owner与阶段 |
|---|---|---|---|
| `AcquisitionDeviceSpec` | C·sealed class | DeviceRef/SensorKind/TriggerModes/FrameLayout | 相机/Detector/Grabber设备描述；WP04 |
| `CameraOpticsSpec` | C·sealed class | CameraRef/LensRef/FocalLength/WorkingDistance/CalibrationRef | 几何/光学参数，算法归Vision；WP04 |
| `LightingDeviceSpec` | C·sealed class | LightRef/ChannelRefs/IntensityRange/TriggerBinding | 光源设备触发及定标；WP04 |
| `TriggerSourceSpec` | C·sealed class | TriggerId/SourceKind/Edge/Delay/Timebase | 硬/软/编码器触发；WP04 |
| `TriggerRouteSpec` | C·sealed class | SourceRef/SinkRefs/ClockDomain/Delay/Generation | 多设备同步拓扑；WP04 |
| `AcquisitionProgramSpec` | C·sealed class | Exposure/Rate/Roi/BufferBudget/DropPolicy | 采集计划参数；WP04 |
| `AcquisitionFrameMetadata` | C·sealed class | FrameId/TriggerId/DeviceRef/Sequence/SourceClock/Quality | 大数据走现有DataHandle；WP04 |
| `AcquisitionFrameLease` | C·sealed class | HandleRef/OwnerGeneration/ReleaseDeadline | 优先复用现有公共DataHandle租约；WP04 |
| `IAcquisitionDeviceOperations` | C·interface | ConfigureAsync/ArmAsync/TriggerAsync/StopAsync | SDK专有采集能力；WP04 |
| `AcquisitionHealthSnapshot` | C·sealed class | FrameRate/DroppedFrames/Temperature/BufferQueue | SDK时序/缓冲/健康；WP04 |
| `TriggerSourceKind` | C·enum | Software/Hardware/Encoder/PositionCompare/ExternalClock | 只描述真实触发模式；WP04 |
| `AcquisitionRunState` | C·enum | Configured/Armed/Capturing/Stopping/Unknown/Faulted | 专业采集状态；WP04 |

### 10 XraySourceDetector 射线源与探测器

**候选命名空间**：`CxVisual.Professional.Automation.Xray`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | Owner与阶段 |
|---|---|---|---|
| `XraySourceSpec` | C·sealed class | SourceRef/GeneratorRef/KvRange/MaRange/FocalSpotModes | 独立光源资产；WP04 |
| `XrayDetectorSpec` | C·sealed class | DetectorRef/PixelLayout/FrameRate/CalibrationRefs | 独立Detector资产；WP04 |
| `XrayExposureProgram` | C·sealed class | RequestedKv/Ma/ExposureDuration/WarmupMode | 安全门控外的专业请求；WP04 |
| `XrayWarmupSpec` | C·sealed class | SourceModel/Stages/TemperatureConstraints | 设备自带热机程序引用；WP04 |
| `XrayCoolingSpec` | C·sealed class | CoolantFlowRef/TemperatureRef/FanRef/FaultThreshold | 配套冷却与联锁观察；WP04 |
| `XrayCalibrationReference` | C·sealed class | DarkRef/FlatRef/BadPixelRef/Revision | 只引用厂商标定/图像数据；WP04 |
| `IXraySourceOperations` | C·interface | PrepareAsync/ExposeAsync/RequestStopAsync/ReadStatusAsync | 不得绕过独立辐射硬联锁；WP04 |
| `IXrayDetectorOperations` | C·interface | ArmAsync/CaptureAsync/ReadFramesAsync/StopAsync | 强制帧身份/租约；WP04 |
| `XrayExposureEvidence` | C·sealed class | ObservedKv/ObservedMa/ExposureActive/PhysicalStoppedAt | 不以Sent/ACK证明已停止出束；WP04 |
| `XraySourceState` | C·enum | Off/Warming/Ready/Exposing/Cooling/Fault/Unknown | 必须来自受信控制器；WP04 |

### 11 InstrumentationDaq 仪器与采样

**候选命名空间**：`CxVisual.Professional.Automation.Measurement`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | Owner与阶段 |
|---|---|---|---|
| `InstrumentSpec` | C·sealed class | InstrumentRef/InstrumentClass/Capabilities/CalibrationRef | 万用表/示波器/PXI等；WP04 |
| `DaqDeviceSpec` | C·sealed class | DaqRef/ChannelRefs/ClockCapabilities/DmaConstraints | 设备采集卡；WP04 |
| `DaqChannelSpec` | C·sealed class | ChannelRef/SignalKind/Range/Unit/Scaling | 采集通道参数；WP04 |
| `SensorSpec` | C·sealed class | SensorRef/Sensitivity/PhysicalUnit/CalibrationRef | 量值单位与量程；WP04 |
| `SignalConditioningSpec` | C·sealed class | AmplifierRef/Gain/Filter/Excitation | 信号调理与激励；WP04 |
| `DaqTaskSpec` | C·sealed class | TaskRef/Channels/SampleRate/TriggerRef/BufferBudget | 任务定义复用Runtime实例；WP04 |
| `SampleBatchMetadata` | C·sealed class | BatchId/ChannelRef/SampleCount/SourceTime/DropCount | DataHandle引用而非复制波形；WP04 |
| `MeasurementUncertaintySpec` | C·sealed class | Measurand/Distribution/Uncertainty/Confidence/CalibrationRef | 工业计量不确定度；WP04 |
| `CalibrationCertificateRef` | C·sealed class | CertificateId/ValidFrom/ValidTo/TraceabilityRef | 资产证书引用；WP04 |
| `IInstrumentDeviceOperations` | C·interface | ConfigureAsync/AcquireAsync/SelfCheckAsync | SCPI/SDK设备后端；WP04 |
| `MeasurementQuality` | C·enum | Valid/OutOfRange/Uncalibrated/Uncertain/Invalid | 专业量测，不重建MDS质量；WP04 |

### 12 TestMeasurement 工业试验执行

**候选命名空间**：`CxVisual.Professional.Automation.Testing`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | Owner与阶段 |
|---|---|---|---|
| `TestStationSpec` | C·sealed class | StationRef/FixtureRefs/InstrumentRefs/SafetyZoneRef | 试验工站专业布置；WP04 |
| `TestFixtureSpec` | C·sealed class | FixtureRef/ClampRefs/SensorRefs/DutConstraints | 治具和DUT物理交接；WP04 |
| `DutBindingSpec` | C·sealed class | DutId/CarrierRef/FixtureRef/ProductRevision | DUT与物料身份追溯；WP04 |
| `TestProcedureSpec` | C·sealed class | ProcedureId/StepRefs/Version/CriteriaRefs | 由Decision/Runtime执行步骤；WP04 |
| `TestStimulusSpec` | C·sealed class | StimulusRef/Amplitude/Duration/Unit/SafetyLimit | 测试激励和安全限制；WP04 |
| `TestMeasurementSpec` | C·sealed class | SampleWindow/Measurand/RequiredCalibration/ResultSchema | 稳定等待和量测模型；WP04 |
| `ITestMeasurementOperations` | C·interface | PrepareAsync/RunStepAsync/CollectEvidenceAsync | 专业执行动作，公共Runtime调度；WP04 |
| `TestExecutionEvidence` | C·sealed class | ProcedureVersion/RawMeasurementRefs/Conditions/Trace | 最终OK/NG交Inspection；WP04 |
| `TestProcedureState` | C·enum | Prepared/Stimulating/Stabilizing/Measuring/Completed/Unknown | 试验专业状态；WP04 |

### 13 IndustrialConnectivity 工业接入专业语义

**候选命名空间**：`CxVisual.Professional.Automation.Connectivity`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | Owner与阶段 |
|---|---|---|---|
| `IndustrialEndpointSpec` | C·sealed class | EndpointRef/HostOrBus/Station/NetworkZone/IdentityRef | F17端点资源引用不拥有socket；WP01 |
| `IndustrialProtocolBinding` | C·sealed class | ProtocolRef/DeviceRef/PointMapRef/SessionPolicyRef | 针对设备的规约装配；WP01 |
| `ProtocolAddressSemantics` | C·sealed class | NativeAddress/Namespace/RegisterKind/Endian/Unit | 先查MPA现有同义对象；WP01 |
| `FieldbusNodeSpec` | C·sealed class | BusRef/NodeAddress/ModuleRefs/SyncRequirement | EtherCAT/IO-Link硬件拓扑；WP06 |
| `IndustrialGatewaySpec` | C·sealed class | GatewayRef/UpstreamRefs/DownstreamRefs/RoutePolicy | 多端/协议网关不复制MCN；WP06 |
| `IndustrialNetworkTimeSpec` | C·sealed class | ClockDomain/PtpGrandmasterRef/MaxDrift | 设备时间源与质量；WP06 |
| `ConnectivityHealthEvidence` | C·sealed class | Endpoint/ProtocolSession/Generation/LastGoodAt/Quality | 不只依赖IsConnected；WP01 |

### 14 IndustrialIoTEdge 工业IoT与边缘

**候选命名空间**：`CxVisual.Professional.Automation.Edge`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | Owner与阶段 |
|---|---|---|---|
| `IoTProductModelSpec` | C·sealed class | ProductId/Version/AttributeRefs/EventRefs/ServiceRefs | 融合device_communicator产品模型；WP06 |
| `IoTDeviceAttributeSpec` | C·sealed class | AttrId/Type/Unit/Access/QualityPolicy | 映射MDS，不造第二Current；WP06 |
| `IoTDeviceEventSpec` | C·sealed class | EventId/SourceRefs/TriggerRule/SchemaVersion | 设备事件专业语义；WP06 |
| `IoTDeviceLinkSpec` | C·sealed class | DeviceRef/ChannelRef/PointBindingRefs | 一设备多通道、一通道多设备；WP06 |
| `EdgeNodeSpec` | C·sealed class | NodeRef/HardwareAssetRef/DeploymentRef/CapabilityRefs | Go边缘服务可独立进程；WP06 |
| `TelemetryRouteSpec` | C·sealed class | Origin/Target/QoS/Schema/Filtering/QualityPolicy | 消息路由由公共中心承载；WP06 |
| `StoreForwardPolicy` | C·sealed class | Capacity/Ttl/DedupKey/ReplayMode | 危险控制禁止自动重放；WP06 |
| `DeviceTwinProjection` | C·sealed class | ReportedStateRef/DesiredStateRef/Revision | 影子仅投影，不是权威物理状态；WP06 |
| `IEdgeTelemetryOperations` | C·interface | PublishAsync/GetNodeHealthAsync/SyncMetadataAsync | 可跨进程Go服务，通过稳定合同；WP06 |
| `EdgeCommandPolicy` | C·sealed class | AllowedOperations/Principal/Deadline/NoReplay | 远控权限与审计；WP06 |
| `EdgeSyncState` | C·enum | Online/Degraded/Buffering/Resynchronizing/Offline | 设备网络与业务状态正交；WP06 |

### 15 RecipeParameterEngineering 配方与参数

**候选命名空间**：`CxVisual.Professional.Automation.Recipes`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | Owner与阶段 |
|---|---|---|---|
| `RecipeSpec` | C·sealed class | RecipeId/Revision/MaterialRefs/ProcessConstraints | 配方版本，不复制FCM配置底座；WP06 |
| `ParameterSetSpec` | C·sealed class | SetId/ParameterEntries/UnitRanges/SchemaVersion | 参数值类型与目标映射；WP06 |
| `RecipeTargetBinding` | C·sealed class | ParameterRef/DeviceRef/PointOrCapabilityRef/Transform | 参数目标可以是PLC/驱动/过程；WP06 |
| `RecipeApprovalSpec` | C·sealed class | PrincipalRoles/ApprovalRecordRef/EffectiveWindow | 审批本身依赖公共授权；WP06 |
| `RecipeApplyParameters` | C·sealed class | RecipeRef/ExpectedDeviceVersion/Deadline/StageMode | Stage/Commit业务选择；WP06 |
| `IRecipeApplyOperations` | C·interface | StageAsync/ReadbackAsync/CommitAsync/ResolvePartialAsync | 机制命令与设备写动作分离；WP06 |
| `RecipeReadbackEvidence` | C·sealed class | Expected/Observed/Quality/EffectiveVersion/Partial | 逐参数验证/Unknown；WP06 |
| `RecipeApplyState` | C·enum | Draft/Validated/Staged/Applied/Partial/Unknown/Rejected | 不是事务原子成功保证；WP06 |

### 16 CommissioningDiagnostics 调试与联调

**候选命名空间**：`CxVisual.Professional.Automation.Commissioning`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | Owner与阶段 |
|---|---|---|---|
| `CommissioningPlanSpec` | C·sealed class | PlanId/CheckRefs/TargetDeviceRefs/ModeRequirements | 点检任务定义与版本；WP07 |
| `CommissioningSessionSpec` | C·sealed class | SessionRef/Principal/StationRef/Environment/AssetRefs | 受控现场调试会话；WP07 |
| `DiagnosticCheckSpec` | C·sealed class | CheckId/Preconditions/ExpectedFeedback/Timeout/Mode | 不允许默认通过；WP07 |
| `DiagnosticTestParameters` | C·sealed class | CheckRef/TestSignal/ExpectedRange/Principal | 主动点动/采集需要授权；WP07 |
| `SignalTimingTraceSpec` | C·sealed class | SignalRefs/ClockSource/SamplingPolicy/Correlation | 专业IO/运动时序；WP07 |
| `ICommissioningCheckProvider` | C·interface | ProbeAsync/RunCheckAsync/CollectEvidenceAsync | M08测试底座+专业检验；WP07 |
| `CommissioningCheckEvidence` | C·sealed class | CheckId/Actual/Expected/Time/Source/Reviewer | FAT/SAT/人工确认；WP07 |
| `FatSatReportSpec` | C·sealed class | ReportId/DeviceManifest/ResultRefs/Approvals | 真实签核；WP07 |
| `CommissioningCheckState` | C·enum | NotRun/Passed/Failed/Blocked/RequiresReview | 诊断报告状态；WP07 |

### 17 AssetMaintenance 资产维护与健康

**候选命名空间**：`CxVisual.Professional.Automation.Maintenance`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | Owner与阶段 |
|---|---|---|---|
| `AssetComponentSpec` | C·sealed class | AssetRef/BomRef/LifecycleCounterRefs/InstalledAt | 引用MEM资产；WP07 |
| `MaintenanceConditionSpec` | C·sealed class | ConditionId/MetricRefs/Thresholds/Severity | 温升/振动/使用寿命规则；WP07 |
| `ConditionMonitorSnapshot` | C·sealed class | AssetRef/Metrics/HealthScore/Quality/SourceTime | 结果不代替MDS历史；WP07 |
| `MaintenancePlanSpec` | C·sealed class | PlanId/AssetRefs/Interval/ModeRequirements | 维护计划复用公共调度；WP07 |
| `MaintenanceWorkOrderSpec` | C·sealed class | WorkOrderId/AssetRef/ActionRefs/ApprovalRef | 工单引用公共资源/身份；WP07 |
| `SparePartUsageSpec` | C·sealed class | PartId/ReplacedAssetRef/Serial/Lot/InstallTime | 换件后校准与复验；WP07 |
| `IMaintenanceCheckOperations` | C·interface | EvaluateConditionAsync/PerformCheckAsync/ReadEvidenceAsync | 不自建调度器；WP07 |
| `MaintenanceEvidence` | C·sealed class | AssetRef/WorkOrderRef/CalibrationRef/Acceptance/Trace | 维保闭环与复核；WP07 |
| `MaintenanceActionKind` | C·enum | Inspect/Lubricate/Replace/Calibrate/Verify | 专业工单类型；WP07 |

### 18 MachineSafetyIntegration 机器安全接口

**候选命名空间**：`CxVisual.Professional.Automation.Safety`

| 类型名 | 身份/Kind | 最小属性及关键API合同 | Owner与阶段 |
|---|---|---|---|
| `SafetyDeviceRef` | C·sealed class | AssetRef/SafetyControllerRef/CertifiedDeviceType | 只引用实际物理安全装置；WP07 |
| `SafetyZoneSpec` | C·sealed class | ZoneRef/DeviceRefs/ProtectionRefs/ModeRequirement | 危险区/安全关联；WP07 |
| `SafetySignalBinding` | C·sealed class | SafetySignalRef/ReadOnlyPointRef/TrustedSource/QualityRule | 只读观测，不取代硬件链；WP07 |
| `MachineSafetyStatusSnapshot` | C·sealed class | Door/EStop/STO/SS1/SLS/Quality/ObservedAt | 不得软件伪造安全放行；WP07 |
| `MachinePermitEvidence` | C·sealed class | ControllerRef/SourceTime/ObservedStatus/Generation | 软件授权仍由M12/MRK；WP07 |
| `ControlledSafetyResetParameters` | C·sealed class | SafetyDeviceRef/Principal/Mode/Reason | 仅请求外部控制器Reset；WP07 |
| `ISafetyStatusGateway` | C·interface | ReadSafetyStatusAsync/RequestResetAsync | 认证安全逻辑独立运行；WP07 |
| `SafetyProtectionKind` | C·enum | EStop/GuardDoor/LightCurtain/LaserScanner/STO/SLS | 只是显示分类，不是PLC安全程序；WP07 |
| `SafetyObservationQuality` | C·enum | Trusted/Stale/Unknown/Invalid | 未知状态禁危险动作；WP07 |

## D. 专业执行接口合同（示意签名；必须查重既有M12/MFM/MRK）
```csharp
// 以下仅示范可测试的“专业动作”接口，绝不创建第二套命令、租约与执行引擎。
public interface IMotionDeviceOperations
{
    Task<MotionCompletionEvidence> HomeAsync(HomeParameters request, CancellationToken cancellationToken);
    Task<MotionCompletionEvidence> MoveAsync(AxisMoveParameters request, CancellationToken cancellationToken);
    Task<MotionStatusSnapshot> ReadStatusAsync(string axisRef, CancellationToken cancellationToken);
    Task RequestStopAsync(string axisRef, CancellationToken cancellationToken);
}
public interface IFluidActuationOperations
{
    Task<FluidActuationEvidence> ExtendAsync(CylinderActionParameters request, CancellationToken cancellationToken);
    Task<FluidActuationEvidence> RetractAsync(CylinderActionParameters request, CancellationToken cancellationToken);
    Task<FluidActuationEvidence> ReadStatusAsync(string deviceRef, CancellationToken cancellationToken);
}
public interface IAcquisitionDeviceOperations
{
    Task ConfigureAsync(AcquisitionProgramSpec program, CancellationToken cancellationToken);
    Task ArmAsync(string triggerRef, CancellationToken cancellationToken);
    Task<AcquisitionFrameMetadata> TriggerAsync(string triggerId, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
// 上层必须经过已有M12授权/互锁+MRK资源，PhysicalEvidence由现场确认，不能直接将任何Task完成当设备完成。
```

## E. 类型去重、生命周期和发布评审
| 对象分类 | 归属/默认生命周期 | 允许的专业扩展 | 禁止 |
|---|---|---|---|
| Model/Spec/Definition | MEM引用+FCM Schema 持久定义态 | 强类型专业参数、验证和版本 | 保存Socket/当前值/线程 |
| Session/Backend | Runtime装配的实例态、Generation、Dispose | SDK句柄、专业状态、取消与回调隔离 | 单例全局DeviceManager或无界Thread |
| Point/Signal | MPA地址/静态专业语义，MDS当前值与Quality | 值转换、采样策略、量程/标定 | 两份Current、私有事件总线 |
| Operation | M12/MRK身份/授权/资源/时限 | TypedParameters与专有Readback/Evidence | 每专业独立CommandCenter/FlowEngine |
| Frame/Sample/History | MDS公共数据句柄/历史 | FrameId、TriggerId、测量/标定证据 | 每帧无限复制、无界队列 |
| Studio Editor | 现有Studio/Presentation.Wpf贡献 | 专业图标/面板/Schema视图 | 专业设备DLL引用WPF |

### E.1 类型状态与发布Gate
- **E类**：本次确认存在编译输入或现有公共正式声明；仍不意味着其工业能力已验收。
- **C类**：概念候选，必须先通过 `ClassName/FQN/ExistingCandidate/Owner/Reason/SourceMethod/TestId` 的去重裁决；不能批量生成空 `.cs`。
- **嵌入候选**：`...State`、`...Spec`、`...Evidence` 不一定需要独立公开类型，可按兼容性、API需求设为 internal/嵌套或复用现有M12/MDS合同。
- **接口候选**：只保留真实多实现、插件边界或可测替换的 `I...Operations`；若专业只有一处实现，优先直接在MFM/MOC既有合同中声明Capability，不增无用抽象。
- **相同物理件**：105个历史设备子类和1409条实体映射应由 TypeId、ModelTemplate、Capability组合、AssetRef、Binding与参数承载，不能强制各生成一个硬件子类。

### E.2 真实代码的优先迁移清单
| 源代码 | 目标 | 方法级验收门禁 |
|---|---|---|
| `ReferTo/DeviceVariable/DeviceInterfaces/Device.cs`, `Controller.cs`, `Axis.cs`, `Cylinder.cs`, `PointSheet.cs` | 设备专业Definition、Motion、Fluid、Point绑定 | 旧功能与线程/坐标/错误/资源对照，不保留旧全局管理器 |
| `ReferTo/Protocols/ModbusTcpModule`, `cx-vision-x2/Drivers/ModbusTcpDriverModule/S7DriverModule` | 复用现有MPA/MCN与Automation PLC | 不复制协议/Channel；实际读写/异常帧/协议反馈 |
| `cx-vision-x2/Drivers/IRayXRayDriverModule/IRayDetectorDriverModule` | Xray、Acquisition专业Backend | x64 SDK/许可/回调/Buffer/硬件安全与设备HIL |
| `cx-vision-x2/CxECTStudio/Business/CheckServices/*` | Commissioning与健康点检 | 真实硬件条件+人工签核，不默认PASS |
| `gcl3`的Terminal/Protocol/Worker/Measure/RTDB，`device_communicator`产品/点/网关/Monitor，`ccxx`基础通信 | 正式F17/Connectivity/MDS/M35/Services + 专业Device/SCADA/Edge | 逐函数Owner拆分；多语言服务保留进程接口，不强制改写C# |

### E.3 六大工业案例和可实施性检查
1. PLC+IO+Fluid+Motion+Handling：点位、夹具到位、轴真实到位、Carrier交接两端证据与Stop恢复。
2. Robot+Camera+Vision：Tool/WorkObject/CalibrationVersion、硬触发、FrameId、真实位姿、故障/迟到帧处理。
3. X-Ray Source+Detector+CT3D/Inspection：物理安全许可、HV状态、TriggerId/FrameRef、投影与质量、停止出束验证。
4. DAQ+TestMeasurement+Inspection：仪器标定、受控激励、波形/量值/不确定度和最终判定相分离。
5. SCADA+Process+Recipe+HMI：PV/SP/MV、遥控遥调、报警/SOE、配方Readback、历史一致性。
6. IoT+Edge+Maintenance：产品/测点、边缘失联补偿/去重、资产状态/工单与不重放危险命令。

### E.4 待裁决范围与文档状态
当前真实 `Professional/Automation` 项目编译输入只有少数设备配置与PAD变量文件。表内候选没有完成 C# 编译、Windows CI 或真实设备HIL；`DOCS/02 V4.9`、`03 V2.01`、`04 V6.4` 原Sheet和 `04Automation.png` 图内标注仍需逐条核验。任何P编号/FQN均以正式Owner批准为准。

## F. 现实设备的105子类（不是105个必建 C# 继承子类）
> 来源：直接读取《01～03统一规格基线 V2.2》的 `01B_Automation实体分类全表`。本表分类共108行，其中3行是跨专业复用提示、不计新设备；实体主归属105个子类、1,409条现实物理实体映射。具体设备以 ModelTemplate/Capability/Asset/Configuration 组织。

| 专业领域 | 主归属设备子类 | 现实对象类别（可作为型号模板目录） |
|---|---:|---|
| DeviceEngineering | 6 | 整机与生产装备；检测与测试整机；装配与搬运设备；生产线与站点；设备子系统；柜体与配套 |
| PlcIoFieldbus | 7 | PLC控制核心；数字输入输出；模拟与温度模块；专用工艺模块；现场总线站点；现场开关与输出；柜内组件 |
| ScadaIcs | 6 | 公用事业实体；生产过程实体；现场采控单元；现场执行设备；现场感知设备；监控设施 |
| MotionCnc | 15 | 运动控制核心；总线与扩展；驱动器与变频器；电机与旋转执行；直线传动件；旋转传动件；运动机构与模组；气缸与气动执行；气路控制与气源；真空与吸附；液压与流体执行；反馈与状态检测；触发与同步；安全与制动；工装与工艺辅助 |
| FluidPowerActuation | — | 引用MotionCnc/其他设备已有资产，增加Fluid能力语义；不创建第二资产 |
| RobotEngineering | 7 | 工业机器人本体；特殊移动机器人；机器人关节与执行；末端执行器；机器人感知；控制与安全；工装与周边 |
| MaterialHandlingSystems | — | 引用设备/Robot/Motion资产，增加物料流/交接语义 |
| ProcessControl | 7 | 容器与反应装备；泵与输送装备；阀门与执行器；温度与换热；配料与物料处理；检测与现场仪表；过程安全与公用设施 |
| AcquisitionTrigger | 7 | 工业面阵相机；工业线扫与特殊相机；探测器与采集硬件；镜头与光学附件；照明与曝光附件；触发与同步附件；扫描运动配套 |
| XraySourceDetector | 6 | X射线发生系统；探测器系统；采集与校准器材；曝光与安全；冷却与环境；成像定位机械 |
| InstrumentationDaq | 8 | 通用电子测试仪表；射频与通信测试；DAQ机箱与采样；电量与温度模块；力学传感器；环境与过程传感器；精密计量仪器；采样信号附件 |
| TestMeasurement | 7 | 气密与流体测试；电气安全测试；电池与新能源测试；机械与材料测试；环境可靠性试验；功能与终检工装；校准与溯源实物 |
| IndustrialConnectivity | 6 | 工业网络设备；串行与总线转换；工业协议接入设备；现场网络物理层；现场设备终端；网络诊断与调试实物 |
| IndustrialIoTEdge | 6 | 边缘计算终端；网关与采集器；无线与物联感知；工业网络与安全；现场接入资产；边缘运维附件 |
| RecipeParameterEngineering | 4 | 配方控制的生产设备；配方控制的工艺单元；配方作用的现实对象；工艺执行及核验硬件 |
| CommissioningDiagnostics | 6 | 控制器与执行调试；电气调试仪表；网络与通信诊断；运动与机械校正；安全与工艺调试；现场调试辅助 |
| AssetMaintenance | 7 | 旋转机械资产；直线及传动易损件；气液系统资产；电气控制资产；工艺与成像资产；维护检测设备；物理备件与耗材 |
| MachineSafetyIntegration | — | 安全设备引用现有PLC/Robot/X-Ray/工站资产；提供只读安全接口，不复制物理实体 |
| **合计** | **105** | 物理设备分类与型号目录的设计输入，非 Automation 程序集类数量 |

## G. 总数、粒度与开发优先级
- 本稿类型记录数：**206 条** = 已核对现有/公共引用 **20 条** + Automation专业候选 **186 条**；候选分布在18领域。数字是**架构词汇集合**，不得直接变成186个新文件、186个接口或186项已完成能力。
- 纯数据/枚举类先审查能否与现有FCM/MOC/MDS约定合并；同一对象若分别出现在设备、历史与UI，优先保留一份稳定Ref和一份事实来源。
- **最小先实现集合**：DeviceModelSpec、DeviceBindingSpec、PADeviceSession、PlcControllerSpec、IoSignalDescriptor、PlcPointMapping、IPlcPointOperations及必要的PlcReadbackEvidence；仅当现有类型不可承接时才新增。其他候选按真实 WP02～07 场景驱动落地。
- 建议类清单每个实现项进入代码评审时必须新增一行 `状态：设计→批准→编译→G1→G2→G3`，关联对应测试与固定SHA；禁止将设计表中C项无依据批量改成E。

## H. 待核验的上游权威与修改入口
本次类清单以V2.2直接读取的18个领域/105子类和最新已核对源码为设计输入；GitHub主仓内 `DOCS/02 V4.9`、`03 V2.01`、`04 V6.4` 原始 XLSX，以及 `DOCS/能力图/04Automation.png` 仍需图表逐项对照。若发现最新冻结Owner与候选不同，以新ADR + 文档修订方式更新本清单，不能直接改公共项目或引入新独立csproj。
