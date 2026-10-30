# CxVisual Automation——04 运动控制与机器人 V1.1
> 版本 V1.1｜2026-10-29｜性质：目标架构与开发合同，非功能完成声明；代码基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8 / WPF / x64。
> 适用范围：Axis/Group/Robot、硬控制器与专业执行合同；按 `AGENTS.md`、工程 Skill、`DOCS/00～04`、最新 Owner/ADR 执行。新增类型前必须审查 DOCS/02～04 XLSX 的原始 Sheet、真实 csproj 与源码。
> 状态规范：【S】本次已读源码，【R】旧仓资产，【T】目标设计，【V】真实CI/测试证据，【H】待HIL；不能以目标接口推断现有实现。
> 重要限制：本次 GitHub 文本接口无法解码 `DOCS/能力图/04Automation.png`、其余 Workbench PNG及 02～04 XLSX 二进制；位置/名称已核实但图内项目和 Sheet 内容尚待原图交叉校验。能力图不作为未经核实的逐条事实。

**总原则：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 1. 专业模型
Axis：AxisRef、ControllerRef、Units、Scale、Home/Limit、Position/Velocity/FollowingError、Mode、Servo/Alarm；AxisGroup：协调资源、坐标系、运动学/插补能力；Robot：Controller、Joint、Tool、Base/Work Frame、Pose、Program、SafetyState；Actuator：Cylinder/Valve/Output。
## 2. 核心角色 UML
~~~mermaid
classDiagram
 class MotionController {+ControllerRef +DriverType +GetHealth()}
 class AxisDefinition {+AxisId +Unit +Limits +HomePolicy}
 class AxisGroupDefinition {+GroupRef +FrameRef +Axes}
 class RobotDefinition {+RobotRef +ToolRef +WorkFrameRef}
 class MotionProvider {<<interface>> +Home() +Move() +Stop() +Readback()}
 class MotionOperation {+CommandId +Deadline +Target +Outcome}
 MotionController "1" --> "*" AxisDefinition
 AxisGroupDefinition "1" o-- "*" AxisDefinition
 RobotDefinition --> MotionController
 MotionController --> MotionProvider
 MotionOperation --> MotionProvider
~~~
**全部是目标职责**，除旧资产类名外，暂不声明本图中的 Automation 新 API 已编译。
## 3. 运动合同
Home/MoveAbs/MoveRel/Jog/Stop、Profile/Velocity/Accel/Decel、GroupMove/Interpolation、TriggerOnPosition、ReadStatus；参数必须标注单位/坐标系/容差、速度上限、控制器能力、资源、期望结果及取消语义。上位机的 Stop 请求不等于设备断能/急停。
**执行阶段**：Authorize→Acquire MRK Lease→Preflight(mode/servo/interlock)→Send→Protocol/SDK ACK→InMotion→PositionInTolerance+ControllerDone→Release；超时进入 Unknown，读取设备真实位置、再判断是否允许恢复。重复 CommandId 必须受幂等政策约束。
## 4. 多轴与机器人时序
~~~mermaid
sequenceDiagram
 actor Op as 操作者
 participant Run as M12/MRK
 participant Safety as 安全PLC/控制器
 participant Motion as MotionProvider
 participant HW as 硬件轴组
 Op->>Run: MoveGroup(target,frame,profile)
 Run->>Safety: 核实模式/许可和物理安全状态
 Safety-->>Run: 可操作性/限制
 Run->>Motion: 已批准 Operation + 资源Lease
 Motion->>HW: 控制器原生轨迹命令
 HW-->>Motion: Ack（非终点）
 loop 有界监测
 Motion->>HW: 读取位置/报警/运动状态
 HW-->>Motion: 反馈
 end
 Motion-->>Run: Completed/Unknown/Faulted + 证据
~~~
## 5. 故障与安全
软限位、硬限位、急停、伺服掉电、手/自动切换、回零前不可定位、实际位置偏差、碰撞/安全门、不满足工艺使能、通信断连均有独立诊断。软件逻辑不替代硬件安全控制器；机器人安全区域、协作限制和示教互锁由对应合格安全系统保证。
## 6. 复用与迁移
本仓 `ReferTo/DeviceVariable/DeviceInterfaces/Controller.cs` 的历史 `ControllerMethod` 存在 JogMove/AbsMove/Home/Stop/InterpolationMove/ReadDI/WriteDO；`Axis.cs` 存在 Axis 与 AxisMethod。这是【R】旧能力，不等于可直接在当前 Automation csproj 编译。要逐函数审计线程、异常/返回值、单位、DLL、反馈、超时和硬件安全，按专业 Provider 融合而非保留旧 DeviceManager 单例。
## 7. 交叉专业
Motion 移动并触发 Camera/X-Ray 时：Motion Owner 输出 Encoder/Position/TriggerId，Acquisition 持有采集同步和 FrameId，Vision/CT3D 持有专业处理结果；只通过公共 DataHandle/Message/Command 协同。完整现场例见 #01 CASE-01/02/03。
## 8. 专项验收
MOT-001～012：单轴回零、负方向JOG、速度及单位、软硬限位、双轴插补、终点位置证明、轨迹中取消、冲突资源、断线/掉电、未知结果恢复、工业控制器同步精度、真实安全门/HIL（G3）；机器人增加工具/坐标变换、模式、程序执行、运动监视、停机。


---

## V1.1｜Motion/CNC、Fluid、Handling、Robot 四域完整扩展
### 8. 四域组合而非同一 AxisBase 继承树
| 领域 | 必须建模的专业对象（角色） | 主要动作 | 现场终态证明 |
|---|---|---|---|
| Motion/CNC | Controller/Drive/Motor/Transmission/Encoder/Axis/Group/Coordinate/Path/Spindle/ToolMagazine | Enable、Home、Jog、MoveAbs/Rel、Cam、Interpolation、Spindle、PSO | InPosition+ControllerDone+ErrorCode/FollowingError |
| Fluid | AirSupply/FRL/ValveIsland/Cylinder/HydraulicUnit/VacuumGenerator/Cup/PressureSensor | Extend/Retract、Clamp/Release、VacuumOn/Off、PressureSet | 双到位/压力稳定/泄漏/超时状态 |
| MaterialHandling | Conveyor/Feeder/Turntable/Buffer/Fixture/Carrier/TransferRoute/StationHandshake | Feed、Convey、Position、Transfer、ReserveSlot、HandOver | CarrierId、到位/占用、交接双方确认 |
| Robot | Robot/Joint/Tool/WorkObject/Frame/Pose/Path/EndEffector/Program | MoveJ/MoveL、Teach、RunProgram、Grip、ChangeTool | 真实Pose/ToolVersion/ProgramDone/安全模式 |
核心分离：`Axis` 是连续几何运动对象，`Cylinder` 是离散/压力执行对象，`Transfer` 是工位物料交接协议，`Robot` 包含多关节运动学和控制器程序；共用资源/命令/监测底座，绝不能所有设备都强行派生Axis。
### 9. 专业 UML：设备组合、信号与动作
~~~mermaid
classDiagram
 class Workcell {+StationRef}
 class MotionAxis {+AxisRef +HomeRef +Unit}
 class ActuationUnit {+DeviceRef +Kind +FeedbackRefs}
 class MaterialTransfer {+TransferId +CarrierRef +FromRef +ToRef}
 class RobotWorkObject {+FrameRef +ToolRef +PoseVersion}
 class DeviceCapability {+CapabilityId +Version}
 class OperationEvidence {+CommandId +ObservedState +Timestamp}
 Workcell o-- MotionAxis
 Workcell o-- ActuationUnit
 Workcell o-- MaterialTransfer
 Workcell o-- RobotWorkObject
 MotionAxis --> DeviceCapability
 ActuationUnit --> DeviceCapability
 MaterialTransfer --> DeviceCapability
 RobotWorkObject --> DeviceCapability
 DeviceCapability --> OperationEvidence : execution
~~~
> 目标UML；MEM工程资源复用/合同名称去重后才可申请正式 FQN。物理 Axis/Fluid/Robot/输送机均经 Equipment AssetRef 关联，身份唯一。
### 10. 运动与夹具/气路安全时序
~~~mermaid
sequenceDiagram
 participant Run as Decision/MRK
 participant PLC as PLC&CertifiedSafety
 participant Pne as PneumaticProvider
 participant Mot as MotionProvider
 participant Feed as MaterialTransfer
 Run->>PLC: 核查模式/保护状态/限位
 Run->>Pne: Clamp
 Pne-->>Run: Clamp command Ack
 Pne->>PLC: 读取两个夹紧检测/压力
 PLC-->>Run: StableClampPhysicalFeedback
 Run->>Mot: Home/Move(target,profile)
 Mot-->>Run: ControllerDone + PositionWithinTolerance
 Run->>Feed: HandOver(CarrierId)
 Feed-->>Run: ReceiverReady+TransferComplete
 Run-->>Run: 工站状态可完成/可追溯
~~~
**负例**：夹具动作Ack但无压力→禁止运动；负限位/跟随误差→停止且Unknown；载具交接两端信息矛盾→不能重复送料；机器人Tool版本或安全模式不符→禁止执行。
### 11. CNC与机器人专业要求
CNC独立关注 G代码/NC程序装载与版本、刀具/刀补、主轴/进给倍率、机床原点与工作坐标、报警与加工进度。CxVisual可管理/验证/监测厂商控制器能力，但不自制未经验证的插补器或数控内核。
Robot独立关注 Joint/Cartesian/Tool/Workobject 坐标系、MoveJ/MoveL路径、姿态奇异点、工作空间、工具快换和附属I/O；运动学计算/坐标变换优先复用 Foundation.Geometry/Numerics。手眼标定与Vision结果由稳定标定/位姿版本引用，实时安全区域仍由合格控制系统实施。
### 12. Fluid/Handling 专用状态机
`Cylinder: Retracted→Extending→Extended→Retracting→Retracted`，并有 `Unknown/Timeout/PressureLow`；动作必须支持至少可选双位置开关、压力与时间窗口校验。真空夹持需监测吸力达标、跌落/泄漏；控制响应不等于夹持成功。
`CarrierTransfer: Request→SourceReady+TargetReady→TransferInProgress→AtTarget→HandoverConfirmed`，双方失联或堵料进入 Fault/Unknown；重复请求携带 CarrierId/TransferId 幂等校验，禁止自动补送造成双料。
### 13. 旧库成熟资产审查清单
`ReferTo/.../Controller.cs` 中 JogMove/AbsMove/Home/Stop/InterpolationMove 与 DI/DO；`Axis.cs` 中的状态属性、`Cylinder.cs`、`Output.cs`、`CoordinateSystem.cs` 都是旧资产真实源码。迁移必须逐函数确认：单位/旋转方向、硬限位、线程亲和、取消停机、异常/返回值、DLL位数、资源释放、软硬反馈、精度和 HIL，不保留全局 DeviceManager 或深层继承耦合。
### 14. 操作面板目标
Motion 工作区同时显示坐标系、命令位置/实际位置、负载、报警、运动中/到位与速度曲线；Fluid/Handling 工作区显示回路/压力/真空/气缸到位、Carrier/Route/Buffer占用及逐步握手；Robot 工作区显示工具/姿态/程序/安全状态和轨迹。操作按钮由 Mode/Permission/Lease 控制；UI Undo仅撤销工程编辑。
