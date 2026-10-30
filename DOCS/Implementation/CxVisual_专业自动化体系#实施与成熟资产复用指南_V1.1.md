# CxVisual Automation——真实源码映射、成熟资产复用与实施指南 V1.1
> 日期 2026-10-30；目标性质：阶段实施合同（非代码完成或 HIL 通过）；源码核查基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`。主仓 `cx-visual-x3`，C#/.NET Framework 4.8/WPF/x64。
> 严格保留 AGENTS/Skill、DOCS/00～04、26项目/135子方面、F17 Channel Owner、Decision 合并及五类模块裁决；提交仅增加文档。PNG能力图及 DOCS/02～04 XLSX 因 GitHub UTF-8 接口限制未能读取二进制内容，本文不虚报逐项对齐。
## 0. 一分钟实施结论
优先 **真实 PLC+IO 设备闭环**：工程定义+点表/连接+采样→MDS→订阅/健康→授权命令→协议应答→物理回读/未知结果→TestCenter/G1→PLC/G2；再做 Motion/Robot/Acquisition/SCADA/IoT。先复用现有 MFM/FCM/MCN/MPA/F17/MDS/M12/MRK，避免重新发明公共框架。
## 1. 代码事实与验收状态
| 源码 | 状态 | 真实责任/已知边界 | 下一动作 |
|---|---|---|---|
| `Professional/Automation/CxVisual.Professional.Automation.csproj` | 【S】工程存在，.NET 4.8；引用 Core、Data、Foundation.Data | 当前 Compile 仅 `CxAutomationDeviceOptions.cs`、`DeviceVariables/PADDeviceVariableCatalog.cs`、`PADDeviceVariableProvider.cs` 等 | 按新增合同接入，不能直接假设已有 Motion/XRay |
| `CxAutomationDeviceOptions.cs` | 【S】FCM Device 配置实例 | Host/Port/Station/Timeout/Retry 校验，不含设备安全状态 | 核对唯一 Schema 后收敛专业配置 |
| `PADDeviceVariableCatalog.cs` | 【S】StableId/地址/类型注册、InstallDefinitions/Bind | 不存 Current，不含协议点表或完整 Device Model | 添加映射合同/来源ID/Schema版本，尽量复用原类 |
| `PADDeviceVariableProvider.cs` | 【S】IMDSVariableProvider、Emit、授权后端 WriteAsync、LegacyBridge | Writer/授权缺省拒绝；返回 bool 不意味着物理闭环 | 由 M12 统一承接动作结果/回读 |
| `Mechanism/Core/Modules/MFMDriverModuleConfiguration.cs` | 【S】公共 Driver 配置、SDK/InternalTransport/Hybrid | 公共合同不等于硬件 Provider 已解析 | 专业实现引用而不克隆 |
| `Mechanism/Connectivity/Connectors/MCNConnectorModule.cs` | 【S】Connector 请求、读写点 | 执行/现场能力需 G1/G2 测试 | 复用并核查链路 |
| `Mechanism/Connectivity/Protocols/MPAModbusProtocols.cs` | 【S】现有 Modbus 规约源码 | 不能仅因类存在宣称完整 PLC 产品级协议覆盖 | 校验功能码/地址/异常/并发与回读 |
| `Foundation/Transport/Channels/**` | 【S】Channel实现/可靠性/诊断目录 | F17唯一通用 Channel Owner | 用真实传输测试 |
| `Professional/Automation/Connectors/Porting/PACModbusTcpDriver.cs` | 【R-迁移候选，未编译】文件注释 NOT INCLUDED IN PROJECT | 含旧 CommonInterfaces/Platform 依赖，不可当现行 Driver | 先做能力差异，再删除旧依赖，避免第二Modbus |
| `Tests/TestCenter/Manual/Module/TCTDriverReadinessPanel.cs` | 【S】Driver/SDK/OS观测探针 | ProviderResolved/DeviceReady/PhysicalActionVerified 当前 false | 继续端到端真实能力验证 |
## 2. 成熟资产：目标Owner与复用策略
| 来源仓/原类或路径 | 历史能力【R】 | 目标唯一Owner | 方式与门禁 |
|---|---|---|---|
| `ReferTo/DeviceVariable/DeviceInterfaces/Device.cs`、DeviceManager | 设备属性/配置/变量与全局注册 | MEM工程身份+Automation设备语义+MDS值 | Refactor；禁止迁回静态全局 Current |
| `ReferTo/.../Controller.cs`、`Axis.cs` | Jog/AbsMove/Home/Stop/Interpolation/DI/DO，轴与控制器 | Automation.Motion 专业 Provider | 方法级迁移，检查硬件完成/坐标/安全/线程 |
| `ReferTo/.../PointSheet.cs`、DriverModuleBase | 点表、属性映射、动态采样与专用设备 | MPA点表 + Automation专业元数据 + MDS | 分割职责；不照搬旧Manager和DriverBase继承树 |
| `ReferTo/Protocols/ModbusTcpModule`、S7Module、RTU | PLC通道/规约/读写 | F17/MPA/MCN，Automation绑定语义 | 与已有 MPAModbus 先查重，逐功能对比 |
| `cx-vision-x2/Drivers/ModbusTcpDriverModule`、S7DriverModule | 现场 PLC 能力 | MCN/MPA + Automation设备 | 经许可/测试迁移，不复制造轮子 |
| `cx-vision-x2/Drivers/IRayXRayDriverModule`、IRayDetectorDriverModule | XRay Source/Detector SDK，含 IRay/Dalsa | Automation.Acquisition Provider | SDK版本/x64/设备固件/帧租约/HIL |
| `cx-vision-x2/CxECTStudio/Business/MotionExecutionService.cs`、DetectorImageService、CheckServices | 运动流程/采图/点检 | Automation专业动作+M08验证+Studio UI | 提取行为/异常测试，杜绝 WPF 直接持有 SDK |
| `gcl3/src/protocol/modbus_protocol.*`、iec104、plc_fins_* | ICS/SCADA 规约与报文 | MPA机制 + Automation.P13专业规则 | C++方法和状态逐项比对，不重复底层 IO |
| `gcl3 Terminal/Worker/Measure/Bus/RTDB` | 工业采样、消息、共享数据、历史 | MEM/MCN/MDS/M36/M35 各自唯一Owner | 按行为拆分，保持值/命令/事件不同语义 |
| `device_communicator/hy/common/model/device_base.go` 等 | 型号/实例/测点/设备通道关系 | MEM资产引用+Automation DeviceModel | 复用模型分离思想；避免新数据库权威树 |
| `device_communicator/hy/communicator`、`hy/monitor`、`hy/daemon` | Edge采集/控制/监控守护 | 现有Connectivity/Services.Supervision+Automation Edge | Go 服务可保留，不强制C#迁写 |
| `ccxx/include/ccxx/cxchannel*.h` 等 | Channel/Factory/路由/IO线程/日志 | F17/Mechanism等公共Owner | 参照成熟边界，不以C++源码替换已实现.NET功能 |
| `cx-cct-x3`/现有 CT 采集接口 | CT作业/共享数据/同步/重建 | Automation仅采集设备；CT3D专业算法 | 以 TriggerId/数据引用/资源租约稳定合同交接 |
## 3. 跨仓源码固定基线
`cx-vision-x2@2c19afe336bedcd4e950b700878c288d4328a180`；
`gcl3@511ea4af40f0184686fda9b904254d9372019e2a`；
`ccxx@26cecfb02e861ce6b821b33350493bac4793e997`；
`device_communicator@18b5313d8c0c350f3a8cc723f57120cf9e551156`；
`cx-cct-x3@c6e9df88fce887d3c798dd6cf447073e973cecd7`；
`cx-visual-x2@2626734d0fd5e67ac50fd0621532173cec39b939`。仅确认入口/代表类与档案，**不是七仓全方法审计已结束**。
## 4. 阶段工作包（每包必须包含运行闭环）
| 包 | 目标 | 修改范围/Owner（规划） | 退出验收 |
|---|---|---|---|
| AUT-WP00 | DOCS/02～04原始Sheet + 04Automation图解读，P01～18逐项冻结 | Architecture/Implementation/Decisions | 功能→原编号→FQN/Owner→测试编号逐条可追 |
| AUT-WP01 | Device/Point/PLC/IO 模型与配置、MCN/MPA装配 | Automation + 既有MEM/FCM/MCN | G1 设备保存重开、点表校验、虚拟PLC |
| AUT-WP02 | PLC采样与 MDS订阅、质量/时标/历史，命令/回读闭环 | Automation + MDS/M12/MRK正式合同 | G1负例+G2真实Modbus设备 |
| AUT-WP03 | 单轴/轴组/IO 与状态、资源/联锁 | Automation Motion + Runtime | 控制器Stop/Home/Move/HIL分级 |
| AUT-WP04 | Detector/XRay/Camera/DAQ/Trigger/数据句柄 | Automation Acquisition +专业其他Owner | G1有界租约 + G2 SDK + G3联锁/采集 |
| AUT-WP05 | Robot+Camera、Station工艺组合 | Automation + Decision/Runtime/Vision | 真实位置与Frame关联/故障处理 |
| AUT-WP06 | SCADA/IoT/边缘与统一监控工作台 | Automation/HMI/Services.Supervision | 断网变质、历史、权限、恢复 |
| AUT-WP07 | 全六案例现场HIL、长期稳定、性能/安全回归 | Tests/Studio/相应设备工程 | 按设备/版本证据签收 |
## 5. 每个包的提交门槛
先固定 SHA → 搜索现有FQN/Owner/基线Sheet → 来源函数/API+许可/SDK/x64+线程/内存审计 → 根据既有契约实施 → 加正/反测试 → Windows Release x64和TestCenter → 能连接真机的G2/HIL分开报告 → 中文提交。不能以新类数量、配置保存、离线仿真或成功编译宣称工业闭环。
工程结构不增加 `.csproj`；若确需新增专业文件，明确编入原 Automation 项目，不在 Mechanism 创建反向依赖；保留旧资产导入数据格式的版本迁移和回滚。
## 6. 开发者与工程师各走一遍
开发者：设备能力 Schema→可选 Provider→注册 MOC/命令 Handler→请求 Operation→观察物理结果/质量→释放。
工程师：建立 Machine/Station→添加模型设备与点表→检查连接配置→受控联机→监看质量→运行 Flow→查看实际动作证据→安全停机。
需做到两个角色使用同一份配置/Descriptor/运行状态和测试证据。
## 7. 六案流水线与负例
CASE-01 PLC+IO+Motion：设备定义→PLC点表→PLC读取→IO写→Motion Home/Move→终点回读；断线、限位、操作冲突、超时 Unknown。
CASE-02 Robot+Camera：Tool/Frame→机器人位姿→位置就绪→TriggerId→Frame→Vision；坐标版本/时序错配拒绝。
CASE-03 X-Ray：安全门→出束许可→探测器 Arm/Trigger→Projection→CT3D；设备拔线与高压安全链不可假成功。
CASE-04 DAQ：校准→配置采样→缓冲/丢样→测量与不确定度→Inspection；超量程/时间漂移/采样断续。
CASE-05 ICS/SCADA：多点质量→报警/趋势/历史→授权遥控→回读；断网/重复事件/历史补偿。
CASE-06 IoT：产品/型号→网关/点表→边缘采样→离线/重连→版本与去重；危险命令禁止自动回放。
## 8. 本轮证据与限制
从仓库读取：`Professional/Automation` 及依赖 csproj、`PAD*`、`MFMDriverModuleConfiguration`、`MCNConnectorContracts`、`ReferTo/DeviceInterfaces` 代表类、旧仓顶层与核心目录、`DOCS/Codebases/01～03`、Driver V1.1/五类裁决。Windows Actions 最近 HEAD `3960b09f...` 的 Config L1 与配置基础 workflow 标为 success，不代表 Automation 端到端或实机HIL；同时较早 `ea7e7ec35cbb` 的 Imaging Windows 失败，测试状态按 SHA 对应分别陈述。
**待补证据**：PNG能力图内容、02～04 XLSX Sheet、各厂商函数级线程/SDK许可、完整 P01～P18 编号、设备读写性能、HIL与长期稳定性。文档本身不新增CI工作流，要求后续实施同包补测。


---

## V1.1｜全量工程交付：18专业 / 现有FQN / 七仓资产 / 开发依赖矩阵
### 9. 最重要的源码事实（必须防止回滚架构）
- `Professional/Automation/CxVisual.Professional.Automation.csproj` 当前直接编译的是 `CxAutomationDeviceOptions.cs`、`DeviceVariables/PADDeviceVariableCatalog.cs`、`PADDeviceVariableProvider.cs`；直接引用 `Mechanism.Core`、`Mechanism.Data`、`Foundation.Data`。不把未列入 Compile 的旧驱动称为已实装。
- `PADDeviceVariableCatalog` 有 Register/Find/InstallDefinitions/Bind，静态定义/映射无需第二份Current；`PADDeviceVariableProvider` 有受控 `WriteAsync`、`Emit`、停止隔离，但仅有 bool WriteBackend ≠ 物理完成链。
- `MFMDriverModuleConfiguration` 的 DriverTypeId/DeviceRef/Backend/InternalResourceRefs 是现行公共Driver Schema；`IMCNConnectorModule`/MPA Modbus 和 F17 Channel 承担通用 Connector/Protocol/IO。
- `PACModbusTcpDriver.cs`、`PACConnectorDriverBase.cs` 位于未编译的 Porting 目录，禁止直接插入 project 并声称成熟；必须从 ReferTo 的真实调用/异常与测试抽取有价值行为。
- `Tests/TestCenter/Manual/Module/TCTDriverReadinessPanel.cs` 中 ProviderResolved/DeviceReady/PhysicalActionVerified 的缺省假值不是可用能力；相关 CI 不代表现场HIL。
### 10. 18 专业方面实施 Owner / 旧仓来源 / 开发工作包
| 专业键 | 主要真正Owner/复用 | 可复用来源（需方法级核实） | 首批专业实现/测试 |
|---|---|---|---|
| DeviceEngineering | MEM资产身份 + Automation设备模型 + MFM | ReferTo Device/DeviceTemplate/DeviceManager（去全局）; device_communicator model | WP01 设备/型号/生命周期/版本/点表 |
| PlcIoFieldbus | F17+MCN/MPA + Automation PLC/IO | ReferTo ModbusTcp/RTU/S7; cx-vision-x2/Drivers; gcl3 | WP01～02真实点读取/授权写与回读 |
| ScadaIcs | MDS/M35/M36 + Automation SCADA语义、HMI呈现 | gcl3 Terminal/Measure/PSM/RTDB；IoT RT | WP06 遥测/遥控/报警/SOE |
| MotionCnc | Automation Motion + MRK + Geometry/Numerics | ReferTo Controller/Axis/CoordinateSystem；CxECTStudio MotionExecutionService | WP03 Axis/Group/Home/Move/PSO + HIL |
| FluidPowerActuation | Automation Actuator + PLC IO/安全状态 | ReferTo Cylinder/Output/BitStatusSensor/Analog | WP03气缸/真空/阀/压力的物理反馈 |
| RobotEngineering | Automation Robot + MRK；Vision标定共享 | ReferTo Motion/机器人相关成熟实现（需核具体路径） | WP05 Tool/Frame/Pose/程序/Robot+Camera |
| MaterialHandlingSystems | Automation Handling + MEM/Runtime；PLC/Motion/Robot复用 | 设备交接及控制业务（需从cx-vision-x2逐方法核实） | WP03～05 Carrier/Buffer/Transfer |
| ProcessControl | Automation Process + Decision/MDS | gcl3 现场ICS过程/报警；device_communicator测点 | WP06 PV/SP/MV/PID接口/Phase |
| AcquisitionTrigger | Automation Acquisition + FIM/MDS/Time | cx-vision-x2 Detector/Camera SDK；CxECTStudio DetectorImageService | WP04硬触发/FrameLease/TriggerId |
| XraySourceDetector | Automation X-Ray+Detector + CT3D输入 | cx-vision-x2 IRayXRay/IRayDetector/Dalsa | WP04 HV/曝光/源探测器真实反馈 |
| InstrumentationDaq | Automation Instrument+DAQ + MDS/Time | ReferTo Instrument/历史气密/测试模块，需核SDK | WP04数据采集/采样/标定 |
| TestMeasurement | Automation TestExecutor + Inspection最终判定 | cx-vision-x2/CxAirSeal、CheckServices等 | WP04测试过程/试验曲线/量值 |
| IndustrialConnectivity | F17/MCN/MPA唯一基础 Owner，专业协议贡献 | gcl3 modbus/fins/iec104；IoT各种协议；ccxx channel | WP01/06多协议真实合同 |
| IndustrialIoTEdge | Automation Edge + Services.Supervision + MDS/M36 | device_communicator Go/gateway/daemon/monitor | WP06 DeviceModel/断网去重/安全控制 |
| RecipeParameterEngineering | Automation Recipe语义 + FCM/M12/MRK | 历史设备配置/工艺参数，需审真实来源 | WP06参数版本/审批/分步Readback |
| CommissioningDiagnostics | Automation专业点检 + M08/M33/Studio TestCenter | CxECTStudio Business/CheckModuleRegistry/CheckServices | WP07 FAT/SAT/信号时序/报告 |
| AssetMaintenance | Automation资产维保语义 + MEM/M35 | 旧设备资产与CheckService，IoT健康 | WP07 Counter/Calibration/BOM/Workorder |
| MachineSafetyIntegration | Automation只读安全状态/授权操作 + 安全PLC | 旧安全门/光源/XRay/运动点检；厂商安全控制器 | WP03～07防护边界/安全状态/HIL |
**注意**：具体 P01～P18 编号、Class FQN、现有04 V6.4 同义对象，须在 WP00 按现行 DOCS/02/03/04 原始 Sheet 落地“追踪矩阵”；此表暂以可核实的 V2.2 ProfessionalAreaName 为主键，绝不编造 P 编号。
### 11. 全类对象合同（分类与最小字段：初审候选，不直接创建新类）
| 分类 | 实体/值/接口角色 | 必备字段/行为 | 优先复用/去重 |
|---|---|---|---|
| Engineering | Machine/Line/Station/Equipment/DeviceRef | StableId、Parent、ModelRef、Version、资源拓扑 | MEM Node/Content/Resource |
| Model | DeviceModel/Template/Component/CapabilityDescriptor | TypeId、SchemaVersion、CapabilityRefs、DefaultParams | FCM/MFM/MOC |
| Hardware | PhysicalAsset/Controller/Axis/Robot/Camera/XRay/DAQ | Serial、Firmware、InstalledAt、SafetyDomain | MEM + 专业Definition |
| Data | Signal/PointBinding/Observation/Measurement/FrameHandle | PointId、Unit、Quality、SourceTime、CalibrationRef、Lease | MPA/PAD/MDS/FIM |
| Connection | Endpoint/Session/ProtocolBinding/Connector/Driver | Generation、TransportId、State、PointMapVersion | F17/MCN/MPA/MFM |
| Operation | Motion/Actuator/Robot/Exposure/Test/Recipe Operation | CommandId、Target、Deadline、Guard、Outcome、EvidenceRef | M12/MRK；专业实现语义 |
| Control | ProcessLoop/Phase/Recipe/CarrierTransfer/Trigger | PV/SP/MV、Mode、Version、Transition、Timeout | 专业+Decision/Runtime |
| Safety | SafetyZone/SafetyDeviceStatus/InterlockEvidence | SafetyAssetRef、TrustedSource、ObservedAt、Quality | 专业状态 + M28/safety hardware |
| Diagnosis | Fault/Health/Calibration/Maintenance/CheckResult | AssetRef、Severity、Evidence、Trace、Revision | M08/M33/M35/Studio |
| Studio | 专业Editor/OperationCard/DebugPanel | Schema、ViewModel/State、CommandFeedback、UIA Id | Studio/Presentation.Wpf |
每个新FQN需要附 `ExistingFQN / ExistingAssembly / Owner / SemanticsDiff / ClassKind / API / Tests`；无差异时禁止创建。**不要把上表角色名直接按行全部变成.cs**，先从现有源对象和业务案例反推最少的必要类。
### 12. 全链实施阶段、单包验收与依赖顺序
- **WP00 规格冻结**：直接读取 04Automation.png、原始 XLSX 02 V4.9/03 V2.01/04 V6.4 的相关 Sheet，生成18子方面与当前类型的双向映射（Owner争议只写ADR），审查旧Graph/Protocol边界；无该表不得大批新增公开API。
- **WP01 Device/PLC/IO垂直闭环**：物理/逻辑设备与Model、PointTable/Station、MCN/MPA/Transport、MDS质量、Studio设备/点表Editor、Config保存重开、虚拟PLC G1。
- **WP02 真实命令/回读/异常**：M12/MRK授权/租约、Provider写入、ACK+readback、断线/时限Unknown与Trace、实际 Modbus G2。
- **WP03 Motion/Fluid/Handling**：Axis、Cylinder、Vacuum、Transfer、限位/压力/交接握手、统一专业状态、真实控制器/阀岛 G2/G3。
- **WP04 Source/Detector/DAQ/Test**：光源/平板SDK、TriggerId/FrameLease、Sampler/Measurand/Calibration、Vision/CT3D/Inspection跨专业契约、辐射与数据安全 HIL。
- **WP05 Robot+Camera**：Tool/Frame/位姿/标定版本、触发与FrameId、机器人/夹爪/载具联动及人工恢复。
- **WP06 SCADA/Process/Recipe/IoT**：遥测/Alarm/SOE、PV/SP/MV、配方Stage+Readback、边缘缓存/去重，HMI/边缘服务受控协同。
- **WP07 Maintenance/Commissioning/Release**：资产寿命/点检/工单、测试录制回放、FAT/SAT、工程打包/升级/回滚与6案例G1/G2/G3实证。
每个 WP 应交付 `OwnerDiff + BehaviorSource + DomainContract + RealCode + DeveloperExample + StudioUI + Positive&NegativeTests + WindowsCILog + DeviceHILStatus`；不接受空接口、Demo、仅PPT和未验证声明。
### 13. 变更规范与安全门禁
- 新增代码仅修改既有 26 项目恰当Owner，不开新Automation工程；SDK依赖登记来源/许可/版本/x64/拷贝/部署方式；严禁伪造同名DLL。
- 迁移 ReferTo 源代码按方法审计：输入/输出、运行线程、软硬反馈、异常类型、内存/句柄、终止、用例、许可证；保留成熟行为，去旧静态Manager/同步阻塞/重复协议/Wrapper。
- 工业控制安全交给经验证的控制器和实际现场设施；所有危险动作采用最小授权、物理信号复核、未知结果处理和人工恢复；自动化CI只用安全仿真器。
- 工程编辑保存不等于部署成功；Driver Probe端口可用不等于物理Ready；测试调用成功不等于生产环境可用。
### 14. CI事实与持续集成策略
既有 workflows：`testcenter-connector-loop-ui-windows.yml`、`testcenter-driver-readiness-windows.yml`、`variable-system-windows.yml`、`testcenter-module-g1-windows.yml` 等，后续优先扩展现有测试入口；确有必要新增Automation专门Job时先统一测试Owner与证据格式。
V1.0生成时 `3960b09f...` 的Config L1/Foundation成功只代表该SHA那些job；新修订提交以后仍须按其自己SHA检查Actions，文档本身不等于 Windows Build/HIL 通过。
### 15. 文档验收及现实缺口清单
WP00原始主基线图表与所有 P 编号仍需逐格核对；某些 Vendor/SCADA/Robot 旧代码尚未做方法级验证；真实协议/设备精度、压力/位置传感器、机器功能安全认证均未因文档更新而“已实现”。缺口必须归属具体WP/测试编号，不能隐藏在“待优化”。
