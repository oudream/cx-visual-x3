# CxVisual Automation——00 总体架构与设计总纲
> 版本 V1.0｜2026-10-29｜性质：目标架构与开发合同，非功能完成声明；代码基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8 / WPF / x64。
> 适用范围：Automation P01～P18全局边界；按 `AGENTS.md`、工程 Skill、`DOCS/00～04`、最新 Owner/ADR 执行。新增类型前必须审查 DOCS/02～04 XLSX 的原始 Sheet、真实 csproj 与源码。
> 状态规范：【S】本次已读源码，【R】旧仓资产，【T】目标设计，【V】真实CI/测试证据，【H】待HIL；不能以目标接口推断现有实现。
> 重要限制：本次 GitHub 文本接口无法解码 `DOCS/能力图/04Automation.png`、其余 Workbench PNG及 02～04 XLSX 二进制；位置/名称已核实但图内项目和 Sheet 内容尚待原图交叉校验。能力图不作为未经核实的逐条事实。

**总原则：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 0. 三分钟认识
**一句话**：Automation 把现实世界的设备、信号、动作、测量和状态组织为可复用的专业能力，由 CxVisual 既有工程、交互、编排与运行机制组合，供 Studio、TestCenter、嵌入式产品与开发者 API 使用。
**不做**：第二个工程树/变量仓/消息总线/任务调度器/通用 Channel/通用 Driver 合同；不以 PC 软件代替 PLC/运动控制器的安全与硬实时。
**本轮事实**：项目编译输入目前为 `CxAutomationDeviceOptions.cs`、`PADDeviceVariableCatalog.cs`、`PADDeviceVariableProvider.cs`；`Connectors/Porting/PACModbusTcpDriver.cs` 标注 NOT INCLUDED IN PROJECT，不是正式可用 Driver。已有 MFM Driver 公共配置、Connectivity Connector/Protocol、F17 Channel、MDS Provider；专业物理设备能力需逐项实现与验收。
## 1. 产品能力全景（候选域，不取代 DOCS/02 P01～P18 原始名称）
| 功能域 | 典型能力 | 首选真实验收 |
|---|---|---|
| Device/Equipment | 型号、模板、物理/逻辑实例、机器/工站拓扑、资源、状态、生命周期 | 同型号多个实例隔离，离线可组工程 |
| PLC/IO/Signal | PLC、DI/DO、AI/AO、点表、映射、采样、读回、订阅 | Modbus PLC→MDS→UI→授权命令→回读 |
| Motion/Robot | Axis/Group/Coordinate、回零、JOG、定位、轨迹、机器人姿态、I/O | 原点/限位/反馈/取消与不可确认状态 |
| Acquisition/Measurement | X-Ray/探测器/相机/DAQ、软硬触发、编码器、帧/样本/测量结果 | TriggerId、FrameId、数据租约/背压与时间源 |
| SCADA/IoT/Edge | 遥测遥信遥控遥调、事件/趋势/报警、边缘模型/网关 | 断网补偿、质量变差、历史连续性 |
| Automation Operation | 配方参数、状态机、动作序列、工位协同、故障处置/诊断 | 一次完整现场任务，含拒绝和恢复 |
| Simulation/Testing | 虚拟设备、回放、故障注入、虚实切换 | Real/Virtual 身份可区分，仿真绝不冒充设备完成 |
## 2. 逻辑分层图（依赖下层，调用不代表程序集互相引用）
~~~mermaid
flowchart TB
 U["开发者API / Studio Automation Workbench / TestCenter / 嵌入应用"]
 P["Professional.Automation: Device · PLC/IO · Motion/Robot · XRay/Detector · DAQ · SCADA/IoT"]
 S["其他Professional: Vision · CT3D · Inspection · HMI · Simulation"]
 M["Mechanism.Core(MEM/MFM) · Decision(MOC/M12) · Runtime(MRK) · Data(MDS) · Connectivity(MCN/MPA)"]
 F["Foundation.Transport(F17) · Data/FCM · Infrastructure/Numerics"]
 HW["PLC / IPC / Motion Controller / Camera / X-Ray / DAQ / Edge"]
 U --> P
 P --> M
 S --> M
 M --> F
 P --> HW
 M --> HW
 P -. "能力组合（通过正式合同/运行宿主）" .-> S
~~~
**程序集约束**：现有 Automation csproj 只直接引用 Mechanism.Data、Mechanism.Core、Foundation.Data；图中的 M12/MRK/Connectivity 通过宿主装配与合同协作，实施时不能为了便利创建 Mechanism→Professional 的反向引用或循环依赖。F17 唯一持有 Channel，MCN/MPA 持有 Connector/Protocol/Point。
## 3. 六个正交关系
1. **工程身份**：Machine/Station/DeviceDefinition 与 Project/Content/Resource 引用由 MEM 组织，物理设备拓扑由 Automation 表述，禁止第二工程树。
2. **静态声明**：Model/Template、PointDefinition、Capability Descriptor、FCM Schema 均有 SchemaVersion/StableId。
3. **动态事实**：DeviceSession、Health、Current/Quality/Timestamp、FrameHandle、OperationResult；运行值交 MDS，Session 由宿主资源所有权管理。
4. **操作与事件**：Command 只交唯一 M12 Handler；事件经公共 Message，实时 Signal 经 MDS，采集内容经 Data Handle/Stream；不能混成同一数据类型。
5. **执行计划**：编排/条件/循环/子流程复用 Decision/Runtime，不另建 AutomationFlowEngine。
6. **外部系统**：真实动作与安全证据来自控制器/设备，软件不是唯一可信来源。
## 4. 设备结构总览（概念UML：候选角色，不是新增类指令）
~~~mermaid
classDiagram
 class EngineeringDeviceRef {+StableId +ModelRef +StationRef}
 class DeviceModel {+ModelId +Capabilities +ParameterSchema}
 class DeviceDefinition {+DeviceRef +Topology +Bindings}
 class PointDefinition {+PointId +Type +Unit +Access +ProtocolAddressRef}
 class DeviceCapability {+CapabilityId +Version +InputSchema +OutputSchema}
 class DeviceSession {+Generation +Lifecycle +Health +DeviceRef}
 class Provider {<<interface>> +Describe() +Invoke() +GetHealth()}
 class DeviceOperation {+CommandId +CorrelationId +Deadline +Result}
 EngineeringDeviceRef --> DeviceModel
 EngineeringDeviceRef --> DeviceDefinition
 DeviceDefinition "1" o-- "*" PointDefinition
 DeviceModel "1" o-- "*" DeviceCapability
 DeviceDefinition --> DeviceSession : run binding
 DeviceSession --> Provider : owns bounded lifetime
 DeviceCapability --> Provider : resolved by existing registry
 DeviceOperation --> DeviceSession : requires lease + authorization
~~~
## 5. 核心事实状态机
~~~mermaid
stateDiagram-v2
 [*] --> Defined
 Defined --> Configured: 校验
 Configured --> Initializing: 分配资源
 Initializing --> Ready: 能力和现场检查
 Initializing --> Faulted: 失败
 Ready --> Busy: 获授权的动作
 Busy --> Ready: 有物理证据的完成
 Busy --> Uncertain: 超时/回执丢失
 Ready --> Degraded: 质量下降/断连
 Degraded --> Recovering: 重连+重新验证
 Recovering --> Ready: 设备重新确认
 Uncertain --> Recovering: 回读/人工介入
 Faulted --> Recovering: 人工/受控复位
 Ready --> Stopping: Stop
 Degraded --> Stopping: Stop
 Stopping --> Released: 注销回调/归还租约
~~~
**互锁和急停**：上位机可请求受控停止，急停/限位/危险能量隔离由认证安全 PLC/控制器硬件保证；任何软件放行不能替代物理安全链。
## 6. 唯一 Owner / 工程边界
| Owner | 持有 | 不得重复到 Automation |
|---|---|---|
| MEM / Mechanism.Core | Project/Page/Content/Node/Resource、模块身份 MFM | 自动化自建工程树和 MFM Driver 契约 |
| F17 / Foundation.Transport | Channel、IO、SubChannel、字节队列/诊断 | AutomationTcpChannel 等通用二次实现 |
| MCN/MPA / Mechanism.Connectivity | Connector、Protocol、Session、PointTable、规约数据映射 | 第二套 Modbus/Connector 管理器 |
| MDS / Mechanism.Data | Variable/Quality/Time、数据句柄、订阅、历史 | DeviceVariable.Current 全局副本 |
| Decision/Runtime | Capability、Command、Plan、Trigger、MRK operation、租约、任务调度 | Automation 独立流程/任务/命令平台 |
| Automation | 设备型号/设备族、专有驱动和动作、现场闭环、专业安全约束 | 通用 HMI/渲染/检验算法的另一个实现 |
| HMI/Vision/CT3D/Inspection/Simulation | 展现、图像、重建、检测、虚拟环境等各自专业事实 | Automation 对这些专业的私有镜像 |
## 7. 使用体验
开发者 L0 原生设备/SDK；L1 强类型 Provider；L2 MOC 能力与命令；L3 工程资源和 Studio 拖拽编排。工程师流程：选择模板→设备和连接→导入/校验点表→连接/采样→调试/授权→编排→运行/诊断→保存与版本发布。
**必须有六个业务 CASE**：PLC+IO+Motion、Robot+Camera、X-Ray 采集、DAQ 测量、ICS/SCADA、IoT 边缘。详情见 #01 与测试矩阵。
## 8. 设计质量门禁
完整性（六域/六案例）→ UML 可读性 → Owner无重叠 → 设备闭环 → 资源释放 → 失败确定性/Unknown → 双角色易用性 → Windows CI/HIL。设计评审检查是否有巨型Manager、重复Adapter、深继承、伪Ready、丢失质量、隐藏线程、总线循环和跨专业依赖反转。
## 9. 权威与待定
正式优先级：最新 OWNER ADR、AGENTS/Skill、DOCS/00～04、Driver V1.1/Connector V1.3/统一交互 V1.5、真实源码。待决：P01～P18 与本总图逐项对齐（二进制图/表待打开）、复杂设备多协议拓扑在一个物理设备和多个 Connector 之间的绑定合同、物理命令结果与状态持久化具体签名；写新的 ADR 才能改变已决 Owner。
