# CxVisual Automation——04 运动控制与机器人
> 版本 V1.0｜2026-10-29｜性质：目标架构与开发合同，非功能完成声明；代码基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8 / WPF / x64。
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
