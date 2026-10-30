# CxVisual Automation——01 完整能力目录与需求契约
> 版本 V1.1｜2026-10-29｜性质：目标架构与开发合同，非功能完成声明；代码基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8 / WPF / x64。
> 适用范围：全域功能与可追踪验收；按 `AGENTS.md`、工程 Skill、`DOCS/00～04`、最新 Owner/ADR 执行。新增类型前必须审查 DOCS/02～04 XLSX 的原始 Sheet、真实 csproj 与源码。
> 状态规范：【S】本次已读源码，【R】旧仓资产，【T】目标设计，【V】真实CI/测试证据，【H】待HIL；不能以目标接口推断现有实现。
> 重要限制：本次 GitHub 文本接口无法解码 `DOCS/能力图/04Automation.png`、其余 Workbench PNG及 02～04 XLSX 二进制；位置/名称已核实但图内项目和 Sheet 内容尚待原图交叉校验。能力图不作为未经核实的逐条事实。

**总原则：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 1. 需求编排规则
需求ID=AUT-域-序号；每条必须登记 Owner、状态、接口/对象、正反用例、设备/仿真证据。优先级 P0/P1/P2 指开发顺序，**不是 P01～P18 专业子方面编号**。状态 G0结构/G1可运行自动测试/G2真实协议或SDK/G3实机工艺/HIL；不把用户提出的目标标记为已实现。
## 2. 完整能力目录
| ID | 能力/合同 | 优先级 | 最小验收 |
|---|---|---|---|
| AUT-DEV-01 | 厂商/型号/设备模板、版本、实例、序列号、物理/逻辑身份 | P0 | 两设备共享型号但配置与状态隔离 |
| AUT-DEV-02 | Machine/Line/Station/Equipment/Controller/Device/Group 拓扑 | P0 | 保存/重开引用稳定；环形关联拒绝 |
| AUT-DEV-03 | 设备能力、参数、资源、依赖、连接和配置生效版本 | P0 | 缺 Provider、不兼容 Schema 明确拒绝 |
| AUT-DEV-04 | 生命周期、会话、Health、Disconnect/Restart、关闭 | P0 | 无资源泄漏，迟到回调无效 |
| AUT-DEV-05 | 热插拔/发现/固件差异/校准有效期/维护点检 | P1 | 设备更换重新核对 Ready |
| AUT-PLC-01 | PLC点位 DI/DO、AI/AO、站号、寄存器、位/字节序 | P0 | 全四区读取和类型转换用例 |
| AUT-PLC-02 | 批量采样、轮询频率、死区、去抖、时间戳、质量 | P0 | 异常质量不继续显示 Good |
| AUT-PLC-03 | 授权的单点/批量写入与回读、写冲突仲裁 | P0 | ACK≠Applied；超时 Unknown |
| AUT-PLC-04 | IO模块、传感器、执行器、原点/限位与安全域区分 | P1 | 假反馈不得进入成功 |
| AUT-PLC-05 | S7/FINS、Modbus RTU/TCP 等多协议设备组合 | P1 | 与 MCN/MPA 一份真实规约实现 |
| AUT-MOT-01 | Axis/AxisGroup、坐标系、机械单位与限位 | P1 | 单位和转换误用被拒 |
| AUT-MOT-02 | Home/MoveAbs/MoveRel/Jog/Stop、位置速度加速度 | P1 | 有位置反馈且终点容差合格 |
| AUT-MOT-03 | 连续/插补/多轴协同、触发同步、行程防护 | P2 | 控制器执行轨迹，上位仅监控 |
| AUT-MOT-04 | 运动占用/超时/取消/故障与人工复位 | P1 | 多客户端不同时控制同一轴 |
| AUT-ROB-01 | Robot/Tool/Frame/Pose/Program/IO | P2 | 坐标变换、工具版本和反馈一致 |
| AUT-ROB-02 | 状态/模式/程序、运动请求与位置证明 | P2 | 隔离模式下拒绝危险操作 |
| AUT-ACQ-01 | Camera/Detector/X-Ray/DAQ 能力与触发模式 | P1 | 厂商 SDK 缺失不得 Ready |
| AUT-ACQ-02 | 参数、电压电流、帧率/曝光/增益、授权与联锁 | P1 | 物理互锁不通过不得出束 |
| AUT-ACQ-03 | 触发 ID、Frame/Signal 样本序列、时间源与同步 | P1 | 丢帧重复帧/时间漂移可诊断 |
| AUT-ACQ-04 | 缓冲租约、背压、Release、跨专业数据交接 | P1 | Stop/取消不 UAF 不丢所有权 |
| AUT-MEA-01 | 测量设备、单位、标定版本、不确定度、量程 | P1 | 越量程/过期标定明确 Invalid |
| AUT-MEA-02 | 快照/窗口/触发采样、统计结果、记录与追溯 | P1 | 重复采样/乱序有处理政策 |
| AUT-SCA-01 | 遥测/遥信/遥控/遥调、质量/来源、报警及恢复 | P1 | 断连质量和报警轨迹可追溯 |
| AUT-SCA-02 | 趋势、历史、事件日志和外部系统服务 | P2 | HMI共享公共数据，不建新 Historian |
| AUT-IOT-01 | Device Model/Attr/Event/Link、模板/点表导入 | P1 | 同一设备不同通道绑定一致 |
| AUT-IOT-02 | Edge Gateway、离线缓存、重连、去重/重放 | P2 | 断网后不重放非幂等控制 |
| AUT-FLW-01 | 状态/条件/等待/并行/循环/子流程/资源仲裁 | P1 | 复用 Decision/Runtime，取消确定 |
| AUT-FLW-02 | 自动/手动/维护模式、版本冻结、操作审计 | P1 | 模式切换和版本变化阻断越权动作 |
| AUT-SAF-01 | 软件授权、互锁检查与物理安全状态观察 | P0 | 安全链不可被软件越过 |
| AUT-SAF-02 | Stop/EStop/Reset 边界、未知结果与现场复核 | P0 | 无物理反馈不报告完成 |
| AUT-DIA-01 | Connection/Protocol/Device/Command/Trace/Performance | P0 | 关联ID穿透全链 |
| AUT-DIA-02 | 故障分类、恢复策略、隔离与长期监控 | P1 | 断连/重连不自称已校准 |
| AUT-SIM-01 | Virtual/Replay/Real 绑定和版本、故障注入 | P1 | 模拟 ACK 不计作 G2/G3 |
| AUT-UX-01 | Studio工作区/测试操作面板/嵌入 API | P1 | 人可见状态、质量、执行阶段 |
| AUT-UX-02 | 模板、设备编辑、点表映射、调试、日志、保存与重开 | P1 | 工程师无需编辑 C# 源码 |
## 3. 六个最低落地案例
| Case | 工程结构 | 跑通条件 |
|---|---|---|
| AUT-CASE-01 PLC+IO+Motion | Line→Station→PLC/IO/Axis；PLC Connector + Motion Driver | 点位实时变化→命令→轴到位/限位/停止→MDS/Trace |
| AUT-CASE-02 Robot+Camera | Robot/Tool/Camera + 同步资源 | 姿态就绪→安全许可→位置触发→FrameId→Vision算法 |
| AUT-CASE-03 X-Ray | Source+Detector+PLC+安全门 | 门关闭且许可→曝光→触发采图→数据租约→CT3D/Inspection |
| AUT-CASE-04 DAQ | Sensor/DAQ/Calibration | 采样窗→单位与不确定度→Measurement→Inspection |
| AUT-CASE-05 ICS/SCADA | Gateway→多Station→点表→HMI | 心跳、遥测/告警/历史、授权遥控、断网降级 |
| AUT-CASE-06 IoT | DeviceModel→Gateway→Channel/Sheet→Cloud/Edge | 型号导入、边缘采集、重连及审计，不自动重发危险命令 |
## 4. 验收要求
正例+反例必须成对；每个 Case 均需 Definition/FCM 配置样本、操作输入/输出、State/Quality/Time、CorrelationId、资源租约、异常路径、Studio/TestCenter 界面证据以及 G1/G2/G3 分级报告。真实安全级别和性能阈值由具体机器风险评估与设备能力共同确定，不假填毫秒数。
## 5. 对 P01～P18 的映射门禁
目前文本资产明确 P01 设备、P02 PLC、P03 SCADA、P09 相机、P13 专业工业规约；其余编号及完整名称必须以 DOCS/02 原始 Sheet 和 04Automation 图片核准，**本稿不把章节领域直接冒认原编号**。后续需要逐行补充唯一 Owner/候选 FQN/实测状态。


---

## V1.1｜18 专业方面×完整行为合同（本章优先于 V1.0 中的局部能力目录）
> 以下专业名称与分类来自已读取《01～03 统一规格基线 V2.2》的 `01_Workbench专业方面` 与 `01A_Automation现实对象映射`。V4.9当前原Sheet尚待复核，故使用稳固源名/语义键而不是擅自编造 P01～P18 编号；正式编号由开发者核准时补入 Owner 台账。
### 5. 专业全集—业务义务、专用Editor、运行合同、验收
| 专业键/分类名 | 不可遗漏的专业行为 | 专用 Editor（工程师） | Provider/实际行为（开发者） | 专项验收前缀 |
|---|---|---|---|---|
| DeviceEngineering 工业设备模型与生命周期 | 设备型号/模板/资产/组件、物理拓扑、柜体、设备会话、配置版本/备件 | 设备树、组件/安装拓扑、能力/模型配置 | 设备能力、Generation、健康与资源释放 | DE |
| PlcIoFieldbus PLC/远程IO与现场总线 | PAC/PLC、DI/DO/AI/AO、高速计数、总线节点、扫描、Readback、质量 | PLC硬件/IO点表/总线拓扑、站号配置 | 点位读/写、批量采样、设备回应/受控输出 | PI |
| ScadaIcs SCADA/ICS监控与控制 | 遥测/遥信/遥控/遥调、SOE、报警、趋势、受控远程控制、质量恢复 | 站点/Tag/Alarm/SOE/Historian映射 | 采样/报警源/受控指令/事件时标 | SC |
| MotionCnc 运动控制与CNC | 单轴/多轴、JOG/Homing、插补、电子凸轮、PSO、主轴/刀库 | 轴/驱动/坐标/路径/Cam/PSO | Home/Move/Stop/Status/ControllerDone | MO |
| FluidPowerActuation 气动/液压/真空及非轴执行 | 气缸/阀岛/液压站/吸盘/夹紧，压力/真空到位、超时互锁 | 气动/液压回路、气缸阀与真空单元 | Extend/Retract/Clamp/Suction/Release/PressureObserve | FL |
| RobotEngineering 工业机器人与末端执行器 | Tool/Frame/TCP/Joints/Pose/Programs/夹爪/AMR控制器 | Robot/Tool/Workcell/示教资源与安全区关联 | MoveJ/MoveL/Program/RobotMode/IO反馈 | RO |
| MaterialHandlingSystems 物料输送、供料、定位与搬运 | Conveyor/Feeder/Buffer/Transfer/夹具、物料/载具身份、工站握手 | 输送/供料/分拣布局、交接路由 | StartTransfer/ReadyToReceive/Handover/Blocked | MH |
| ProcessControl 连续过程与顺序控制 | 罐釜/泵/阀/换热、PV/SP/MV、PID/级联、工艺阶段、Trip、Batch | P&ID、控制回路、Phase/Sequence、联锁映射 | ProcessMonitor/Setpoint/PhaseRun/TripObserve | PC |
| AcquisitionTrigger 相机、探测器采集与硬件触发 | 面/线/TDI相机、光源/镜头、ROI/帧、Encoder/硬触发/时钟 | 光学/照明、触发时序、采集及多传感同步 | Arm/Trigger/Acquire/FrameLease | AT |
| XraySourceDetector X-Ray光源、探测器与曝光控制 | kV/mA/热机冷却、出束/剂量接口、暗平场、曝光/HV互锁 | Source/HV/Detector/曝光配置、冷却状态 | Prep/Expose/ReadRadiationState/StopEvidence | XR |
| InstrumentationDaq 工业仪器、传感器与DAQ | 计量仪器/激励/通道、速率/量程、校准/不确定度/波形 | DAQ任务/通道/信号调理/标定 | AcquireSamples/Calibrate/MeasureRecord | ID |
| TestMeasurement 工业试验与计量执行 | 气密/耐压/电池/材料/环境、DUT/夹具、步骤/激励/稳定判据 | TestProcedure/Fixture/限值/计量证据 | TestExecutor/MeasurementArtifact；OK/NG仍交Inspection | TM |
| IndustrialConnectivity 工业连接与专业规约适配 | 多端点/网关、Modbus/S7/OPC UA/工业以太网、地址映射与诊断 | Endpoint/协议绑定/网络拓扑/时间同步 | 专业规约元数据；底层实现复用F17/MCN/MPA | IC |
| IndustrialIoTEdge 工业IoT、网关与边缘接入 | Product/Attr/Event/Link、远程采集、离线缓存/补传/设备影子 | Edge Node/设备产品点表、遥测与存储转发 | Gateway/Telemetry/StoreForward/DeviceShadow | IE |
| RecipeParameterEngineering 工业配方与参数工程 | 参数集、配方/物料/工装映射、版本、审批、下载/回读/回滚 | Recipe/ParameterSet/VersionDiff/受控应用 | Validate/Stage/Commit/Readback/rollback条件 | RP |
| CommissioningDiagnostics 设备调试、联调与故障诊断 | PLC/驱动/轴/相机握手、点动、信号示波、FAT/SAT、报告 | Commissioning Checklist/信号与时序/Trace | DiagnosticCheck/ControlledTest/证据报告 | CD |
| AssetMaintenance 设备维护与健康管理 | 健康/寿命、计数、润滑/备件/点检/工单、校准过期 | Asset BOM/Condition Rule/MaintenancePlan | ConditionMetric/MaintenanceRecord/受控维护操作 | AM |
| MachineSafetyIntegration 机器安全与防护接口 | SafetyPLC/光幕/门锁、STO/SS1/SLS状态、区域/复位条件/审计 | Safety Status/Zone/安全接口映射与验证 | ReadOnlySafetyGateway/受控Reset申请，不实现认证安全逻辑 | MS |
### 6. 六条专业边界不变量
1. **同一物理资产一个 AssetId**：Motion与Fluid共享气缸/阀，不复制两个设备；Robot与Handling共享机器人；X-Ray源/探测器可以在一台工站内具备独立设备会话。
2. **信号≠协议地址≠变量当前值**：Signal/Point 的领域定义归专业，Address由MCN/MPA，Current/Quality/History由MDS/M35；典型自底向上映射 `DeviceAttr→PointBinding→ProtocolAddress→Observation`。
3. **控制≠业务结果**：TestMeasurement生成测量事实，Inspection生成产品级OK/NG/复检；ProcessControl管理设备工艺阶段，Decision/Runtime管理公共执行实例。
4. **专业编辑器≠独立运行器**：StateMachine、ControlFlow、Recipe、MotionProgram等由专业贡献执行语义，统一编排与Run/Scope/Resource用已有Decision+Runtime；不得恢复平行Graph真源。
5. **Safety≠Security**：机器安全的物理状态与网络安全的认证/授权/证书/区划是两组正交要求；普通C#软件不作认证安全功能保证。
6. **设备工作与工程操作分离**：撤销工程编辑不能自动撤销已发出的运动、出束、配方下载或遥控。
### 7. 每一项专业能力的十维可执行需求合同
按 `ProfessionalAreaKey/CapabilityId` 记录：①设备/工艺实物与引用②Definition+Version③FCM/输入约束④输出/数据与单位⑤Trigger/执行模式⑥授权/安全/资源⑦状态/取消/物理完成⑧Provider/SDK/通信和故障⑨Studio与开发者API⑩TestID、G0～G3、现场证据。某维度不适用必须写理由；缺任何必要维度不得称“完整”。
### 8. 横向能力全覆盖：把六大Workbench真正连起来
| 能力类别 | Automation具体贡献 | 公共/其他专业Consumer |
|---|---|---|
| 工程定义 | Device/Workcell/Model/Point/Recipe/Topology 资源 | MEM、Studio、HMI |
| 接入与数据 | Signal/Event/Frame/Sample、时间源、质量、Unit | F17/MCN/MPA、MDS、Vision/CT3D |
| 操作和编排 | 设备能力、专业状态/工艺守卫、物理反馈 | M12/MOC/MRK、Inspection |
| 时间与空间 | Trigger/Encoder/Frame、Pose/Tool/Camera/Robot | Foundation Geometry/Time、Vision、CT3D |
| 维护与安全 | Safety状态/工单/诊断/生命周期与审计 | Runtime、HMI、Inspection |
| 交付 | 版本冻结、插件兼容、仿真、现场部署/回滚 | Studio、Simulation、TestCenter |
### 9. 六个标杆用例的专业联动约束
- `CASE-A01`：PLC+DI/DO+Fluid+Motion+MaterialHandling：来料→夹具夹紧反馈→运动回零/定位→工站交接；堵料、失压、限位的故障路径均需证据。
- `CASE-A02`：Robot+Camera+MaterialHandling：Robot工具/工作坐标→工件/载具→光源与触发→FrameId→Vision抓取位姿→Robot动作，Trace带标定版本。
- `CASE-A03`：X-Ray+PLC+Motion+Detector→Vision/CT3D/Inspection：门控、曝光、采集、DataHandle和工件级结果独立确认。
- `CASE-A04`：DAQ+TestMeasurement+Recipe：校准与激励→采样→量值/不确定度→工艺判定，由Inspection负责最终判定。
- `CASE-A05`：SCADA+ProcessControl+HMI：PV/SP/MV、报警/SOE、状态与审计、受控遥调、历史一致性，现场PID归PLC/DCS。
- `CASE-A06`：IoT+Edge+Maintenance：设备模型→网关→遥测/健康→边缘失联→补发/去重、工单及条件维护，危险命令不得自动重放。
### 10. 完整性检查表
每个专业键都必须有：至少一个现实设备或过程、一个核心工程 Definition、一个 Capability/Provider 合同、一个调试/编辑入口、正常/超时/拒绝/恢复四路时序，以及与其匹配的 TestCenter 案例。**18个专业方面 × 六种验收工件 = 108 个最低可追踪槽位**；槽位是需求，不是必须生成108个C#类。
