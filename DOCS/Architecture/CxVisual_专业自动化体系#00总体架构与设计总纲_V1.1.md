# CxVisual Automation——00 总体架构与设计总纲
> 版本 V1.1｜2026-10-29｜性质：目标架构与开发合同，非功能完成声明；代码基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8 / WPF / x64。
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


---

## V1.1｜产品宪章：从设备接入升级为可交付的工业工程系统（正式补强）
### 10. 五份基线交叉审查与覆盖规则
- **当前 2026-10-30 审查输入**：DOCS/01 平台宪法，DOCS/02 V4.9 专业域分类，DOCS/03 V2.01 归属，DOCS/04 V6.4 对象/接口，五张核心规格 V2.1；为避免误引，直接取得的原始内容还包括《01～03统一规格基线 V2.2》六张表与《Automation 现实对象映射 V1.3》。
- **已直接核实的 V2.2**：Automation 18 专业方面、105设备实体子类、1409条现实实体映射、40公共能力方面（规划），分别是分类与需求，不是 105 个工程类、1409 个实现类。
- **仍须逐格核验**：GitHub 最新 DOCS/02/03/04 的 XLSX 原始内容与 04Automation.png 原图。它们不可在本轮 GitHub 文本读取接口直接解析；V2.2 是可以读取的辅助规格，不可替代 V4.9 最新裁决。每个新增目标 FQN 都有“待 Sheet 校验”门禁。
- **旧 V2.1 与新裁决冲突**：V2.1 曾建议 Platform 持有 Channel/Protocol 基础并由 Automation.Drivers 实现部分具体规约，且以 Graph Core 为执行主线。现行实现以 **F17 Transport** 为通用Channel唯一Owner、**Connectivity M13～15** 为协议/Connector/PointTable唯一机制Owner、MFM有五类模块、Decision+Runtime+MEM为正式执行与组织体系。V2.1 用来校对功能与双角色体验，不允许恢复被修订的工程归属和独立Graph真源。
### 11. 最终完整能力图：五维×18专业×六Workbench
~~~mermaid
flowchart LR
 subgraph Engineering["工程定义/设计态 · MEM/FCM"]
  Plant["Line · Machine · Station · Equipment"]
  Model["Model · DeviceRef · Point · Capability · Recipe"]
 end
 subgraph Build["验证/发布态 · Decision/Runtime"]
  Plan["约束/计划/版本冻结/资源/授权"]
  Sim["Simulation · Replay · Virtual Commission"]
 end
 subgraph Live["设备运行态 · Automation 专业"]
  Fam["PLC/IO · Motion/CNC · Fluid · Robot · Handling · Process"]
  Acq["Camera/Detector/X-Ray · DAQ/Test · Trigger"]
  Edge["SCADA/ICS · IoT/Edge · Recipe · Maintenance · Safety"]
 end
 subgraph Shared["现有公共机制"]
  FM["F17 Transport · MCN/MPA · MFM · MDS · M12 · MRK · Trace"]
 end
 subgraph Apps["应用与跨专业"]
  Studio["Studio/TestCenter/Developer SDK"]
  Work["HMI · Vision · CT3D · Inspection · Simulation"]
 end
 Plant-->Model-->Plan-->Fam
 Plan-->Acq
 Plan-->Edge
 Sim-->Plan
 FM<-->Live
 Live-->Studio
 Live-->|Result/DataHandle/Command/Trace|Work
~~~
### 12. 五个正交视角不形成五套平台
| 视角 | 问题 | 真源 | 核心守卫 |
|---|---|---|---|
| Definition | 它是什么？ | MEM设备资源/专业定义+FCM Schema | DeviceRef、模型/版本、物理资产不重复 |
| Connection | 如何到达？ | F17/MCN/MPA + 专业Provider | 会话代次、地址、点表、身份和可信状态 |
| Execution | 可以做什么？ | MOC/M12/MRK + 专业能力动作 | 资源租约、许可、互锁、幂等/Unknown |
| Evidence | 实际发生什么？ | MDS/Trace/History + 设备物理回读 | Quality/Clock/Correlation/证据类型不混淆 |
| Experience | 谁如何使用？ | Studio/HMI/SDK/TestCenter + 实际复用合同 | 工程师与开发者使用同一能力和配置 |
### 13. 差异化产品体验与验收场景
- **工业设备模块化**：物理组件、逻辑设备、流程执行单元与Viewer可独立组合；相同 Model 复制十台设备，运行实例/权限/点位/历史不串话。
- **双向生成**：Model+能力 Descriptor 可生成工程设备向导、点表表单、参数校验和操作 UI；但配置生效必须显式提交/版本审计，严禁绕过权限自动写物理设备。
- **深度调试**：命令链可从 Studio 一步展开 Authorize→Lease→Sent→Ack→Observed→Release，各阶段保留 Trace/时间/设备证据；不能把 ACK 当完成。
- **虚实同合同**：同一逻辑工程下 Real/Virtual/Replay 的Provider以有类型的环境选择，模拟状态无法通过校验门禁成为真机物理证据。
- **多专业闭环**：X-Ray工站从设备模板、PLC/轴、安全门、探测器到 Vision/CT3D/Inspection 与 MES 追溯，所有数据带同一工件/TriggerId/运行版本。
- **工业维护复用**：工程版本对比、现场参数 Readback、故障快照、校准/备件/计数证据、FAT/SAT报告、部署回滚；过程安全由独立硬件保持。
### 14. 标准参考与采用边界
| 参考 | Automation借鉴 | 不意味着 |
|---|---|---|
| ISA-95 / IEC 62264 | 企业—工厂—区域—产线—工站—装备的资产/生产信息映射，MES交互 | CxVisual 包办 ERP/MES |
| ISA-88 | Equipment/Unit/Phase/Recipe 的过程与批次概念 | 上位机代替 DCS/PLC 实时 PID |
| OPC UA IEC62541 / Companion | Device/Variable/Event/Method类型化语义、发现、安全与互操作 | 所有厂家SDK都变为OPC UA |
| ISA/IEC62443 | 身份、分区分域、最小权限、审计与安全升级流程 | 自动获得任何认证 |
| IEC62061/ISO13849 | 防护/安全状态接口、验证和风险评估的系统边界 | Windows软件承担认证机器安全功能 |
| ISA18.2 / IEC62682 | 报警状态、抑制、确认、生命周期 | 普通诊断日志等同报警系统 |
原始官方索引：https://www.isa.org/standards-and-publications/isa-standards/isa-95-standard ; https://www.isa.org/standards-and-publications/isa-standards/isa-88-standards ; https://www.isa.org/standards-and-publications/isa-standards/isa-iec-62443-series-of-standards ; https://opcfoundation.org/about/opc-technologies/opc-ua/ua-companion-specifications/ 。
### 15. 审查否决条件和落地顺序
**否决**：①新建同义通用Manager或Graph真源；②自动恢复直接执行危险动作；③修改 F17/MDS/M12/MRK Owner；④物理安全由普通软件模拟；⑤无G2/G3证据称工业就绪；⑥只提供类图无用户操作/异常/资源合同；⑦把IoT/SCADA/设备界面全挤在Automation单DLL。
**先行**：V1.1 统一对象槽位清单+DOCS/02～04实表核准 → PLC/IO端到端 → Axis/Fluid/Handling → Motion+Acquisition → Process/Recipe/SCADA/IoT → 六Workbench完整工站/HIL。
