# CxVisual Automation——完整功能、工业安全、性能与HIL验收矩阵 V1.1
> 2026-10-30｜设计目标/未执行测试清单。基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；严禁用静态源码阅读、仿真和文档提交替代真实 Windows CI 或物理 HIL。参考 DOCS/Architecture Automation #00～#08 及 Implementation，逐能力对应 AUT-*/CASE-*。
## 1. 证据分级与测试格式
**G0** 文件/Owner/Schema与类型编译存在；**G1** Windows x64真实 TestCenter/自动正反用例、同 SHA 日志；**G2** 实际 Channel/PLC/SDK 协议或设备级验证，含型号、固件、原始帧；**G3** 机器/工站现场HIL、物理安全系统、性能、工艺与稳定性。越级不标完成。
每条结果字段：CaseId/RequirementIds/Owner/SourceSHA/TestSHA/WorkflowURL/DeviceModel/SN/Firmware/SDK/Environment/Principal/ConfigVersion/Generation/Input/Expected/Actual/Quality/PhysicalEvidence/Timing/Error/Trace/Outcome/Reviewer。
状态只允许 NotRun、Pass、Fail、Blocked、NotApplicable(有理由)；同时给 G0/G1/G2/G3。
## 2. 专项矩阵（案例必须正反成对）
| ID | 范围 | 正例与反例重点 | 最低级 |
|---|---|---|---|
| DEV-001～004 | 工程/型号/实例 | 保存/重开/移动、版本/DeviceRef、型号继承和双实例隔离 | G1 |
| DEV-005～008 | 设备引用 | 多Connector与单设备、RS485多站号、重复点/孤儿资源拒绝 | G1 |
| DEV-009～010 | 生命周期 | Generation、Stop/Dispose、回调迟到不污染新实例 | G1/G2 |
| CON-001～004 | Channel/Protocol | TCP/RTU连接、MBAP长度/CRC、粘拆包、异常码 | G1/G2 |
| CON-005～008 | 多端与会话 | 事务ID匹配、多子会话隔离、串行命令/跨会话并发 | G1/G2 |
| CON-009～012 | 恢复 | 断线、重连、限时、队列上限、无自动写重放 | G1/G2 |
| PNT-001～004 | 点表 | ID/地址/类型/站号校验、模板导入、单位/字序/缩放 | G1 |
| PNT-005～008 | MDS | 批量采样、质量/时间戳/Revision、订阅、停止 | G1/G2 |
| CMD-001～004 | 控制 | 未授权/只读/越界拒绝、Mode/Interlock、独占租约 | G1/G3 |
| CMD-005～008 | 执行语义 | Sent/Ack/Applied/Observed/Unknown、超时不盲重试 | G1/G2 |
| CMD-009～012 | 安全 | 恢复后的再次授权、断线时禁止危险操作、物理反馈 | G3 |
| MOT-001～004 | Motion | Home/Jog/MoveAbs/Rel、坐标单位/容差/实际位置 | G2/G3 |
| MOT-005～008 | 轴组 | 插补/位置比较/IO联动、资源冲突、Stop与报警 | G3 |
| MOT-009～012 | 异常 | 限位、伺服断电、未知位置、故障后人工恢复 | G3 |
| ROB-001～004 | Robot | Tool/Frame/Pose变换、模式/程序/IO状态 | G2/G3 |
| ROB-005～008 | Robot+Camera | 位姿Trigger、FrameId、误触发/过期标定拒绝 | G3 |
| ACQ-001～004 | Camera/Detector | SDK缺失、位深/Stride/ROI、软硬触发、DeviceTick | G1/G2 |
| ACQ-005～008 | 数据资源 | 帧丢失/重复、乱序/缓冲租约/慢消费者 | G1/G2 |
| ACQ-009～012 | X-Ray | 物理门控/曝光反馈/停机/异常、数据传递 | G3 |
| ACQ-013～014 | 同步 | 编码器位置触发时间/精度、持续采集长稳 | G3 |
| MEA-001～006 | DAQ/测量 | 单位、标定、量程、统计采样窗、丢样、结果不确定度 | G1/G2 |
| SCA-001～004 | SCADA | 遥测/遥信/报警/ACK/历史一致性 | G1/G2 |
| SCA-005～008 | 监控品质 | Disconnected/Stale、重连、误告警、趋势丢段 | G1/G2 |
| IOT-001～006 | 边缘 | 模型-点表、网关多路、断网、去重、重放/不重发写 | G1/G2 |
| RUN-001～004 | Flow | 分支、循环、并行/子流程、取消/资源租约 | G1 |
| RUN-005～008 | 安全 | 权限/模式/联锁/停机/不可确认结果 | G1/G3 |
| RUN-009～014 | 诊断恢复 | 主机崩溃、设备重启、SDK迟到、Generation、Trace链 | G1/G3 |
| SIM-001～006 | 虚实 | 虚拟时间、故障注入、回放、Real/Virtual标识、不可越权 | G1 |
| UX-001～006 | Studio/TestCenter | 配置、点表、监控、操作结果、诊断、保存重开 | G1 |
| UX-007～014 | UI质量 | 多DPI、UIA、热键、无障碍、虚拟化、大点表性能 | G1 |
## 3. 六个端到端证据包
| 场景 | 输入与拓扑 | 终态成功证据 | 反例与真实环境 |
|---|---|---|---|
| CASE-A01 PLC+IO+Motion | 设备/点表/权限/运动配方 | MDS Good、读回、轴终点/限位控制器反馈、Trace | PLC断开/错位/冲突/超时 Unknown；实际PLC+Motion卡 |
| CASE-A02 Robot+Camera | Frame/Tool/Robot/Camera Trigger | 姿态/拍照时间对齐、FrameId及工件关联 | 模式冲突、迟到帧/失配；实机联调 |
| CASE-A03 X-Ray采集 | XRay/Detector/PLC/安全门 | 物理许可、出束状态、帧、受控关闭/数据租约 | 门异常/掉线/SDK崩溃；安全PLC+物理源 |
| CASE-A04 DAQ测量 | 采样模式/仪表/标定/单位 | 带量程/不确定度的结果和采样元数据 | 越量程、缺采样、标定失效 |
| CASE-A05 ICS/SCADA | PLC/RTU/报警/History/HMI | 遥测/报警/历史/控制回读与会话Trace | 断网、质量Bad、重复/过时事件 |
| CASE-A06 IoT/Edge | Product/Device/Gateway/Sheet | 本地/云事件一致、重连去重、健康和审计 | 断网/身份过期/危险命令拒绝自动重放 |
## 4. 性能门禁（基准先测后定目标）
必须记录 PLC 点数/轮询量/实际周期分布/P50,P95,P99、不同并发会话数、峰值队列、CPU、内存、GC、句柄、SDK缓冲租约峰值、Frame吞吐/带宽/丢帧、触发偏差、Stop回收延迟、断网恢复时间。阈值由需求、设备规格、HIL基准认可后冻结；本稿不写虚构的“1ms硬实时”。
按十分钟、小时级、班次、连续多日阶梯回归；无内存/句柄/文件队列增长；长稳必须留趋势/失败事件，不能仅凭手工观察。
## 5. 安全门禁
严格要求设备工况、急停链、门控、放射源法规、双授权、限位、人员防护、恢复与人工确认等由实际安全设计/认证负责；普通 PC 软件测试不宣称任何安全完整性等级。现场测试必须有设备安全许可/隔离环境，不在一般 CI 下自动出束/运动。
危险命令在 Sim 与 Real 切换时默认拒绝，明确授权后方可执行；按身份、连接代次和策略版本检查，不允许旧 Token/回执复用。
## 6. Windows CI 证据区分
截至本次文本核查的主仓 HEAD `3960b09f...`，`CxVisual Config L1 Windows Build and Smoke`（Run 40894742337）和配置基础 Run 40894742277 为 success，**不覆盖整个Automation体系**。对 `ea7e7ec35cbb...` 的 Imaging Run 40894147647 标记 failure，应在专业跨域回归中跟踪；不把其他 SHA 工作流成功当本轮文档提交 SHA 的成功。
待以后实施时，在原 `testcenter-connector-loop-ui-windows.yml`、`testcenter-driver-readiness-windows.yml`、`variable-system-windows.yml` 等现有工作流上扩展/修正，依增量测试需求，而不是新建第二套Runner。
## 7. 失败处理规范
CI失败/设备无法连接/厂家SDK不可提供，结果记 Blocked 或 Fail，不能造替身DLL/假数据/把 ProviderResolved 设置 true。需要修复业务代码时归属于后续开发包，**本轮仅文档，未运行构建、未测试现场、未改变工程**。
## 8. 完整关闭条件
- [ ] DOCS/能力图/04Automation 视觉内容、六Workbench PNG、DOCS/02～04 原始Sheet逐条审阅；
- [ ] P01～P18 全子方面需求↔类/Owner↔来源↔测试ID映射冻结；
- [ ] 六案例均有G1/G2/G3实际证据，无严重未闭合风险；
- [ ] 授权/联锁/UnknownOutcome/设备资源清理全部通过真实反例；
- [ ] 性能阈值已实测并审核；
- [ ] Windows发布x64、真实SDK/操作系统兼容、设备HIL和变更回滚全部记录。


---

## V1.1｜18专业×功能+场景+现场证据：逐项可执行验收契约
### 9. 测试结构：四层证据，禁止跨级冒充
- **T0 静态/设计**：FQN唯一Owner、API Schema/版本/依赖、目标输入输出与必要时UML检查；不称“已实现”。
- **G1 Windows功能/集成**：Release x64真实编译，ArchitectureSmoke、TestCenter真实按钮/后台逻辑、正反例、保存重开、隔离/并发与日志，固定SHA。
- **G2 设备协议/SDK**：真实Modbus/S7/OPC/设备SDK/实时回执、固件/位数/故障帧与命令回读；安全仿真器只可G1，不冒充G2。
- **G3 真实工业现场**：真实PLC、运动卡、机器人、光源/探测器、DAQ、工位安全体系；经批准的工况、节拍、精度、故障、FAT/SAT、长稳和实机验收。
- **T4 生产交付与维护**：打包/环境前提/校准/配方/版本/回滚/资产履历/权限/数据留存/长期恢复证据，交付后可维护。T4是交付视角，不表示超过G3的功能安全认证。
每条测试必须标识 Mode=Virtual/Replay/Real、Windows/SDK/硬件环境及从 DefinitionVersion 到 RuntimeGeneration 的版本映射。
### 10. 18领域对应测试编号：每项正/反/关闭三联
| Area Key | 正例（P） | 反例（N） | 验收证据（E）与级别 |
|---|---|---|---|
| DeviceEngineering | AUT-DE-001 同模型多个设备、保存重开、组件拓扑稳定 | AUT-DE-002 重复AssetRef/悬空引用/版本漂移拒绝 | AUT-DE-003 DeviceRef+ModelVersion+DeviceGeneration；G1/G2 |
| PlcIoFieldbus | AUT-PI-001 多站点DI/DO/AI/AO与合法写回读 | AUT-PI-002 地址/位宽/质量/权限错误及断链 | AUT-PI-003 原始帧+PointId+ACK与反馈；G1/G2 |
| ScadaIcs | AUT-SC-001 多站遥测/遥信+SOE/Alarm/History | AUT-SC-002 重复乱序/BadQuality/遥控未授权 | AUT-SC-003 事件时标+报警状态/历史；G1/G2 |
| MotionCnc | AUT-MO-001 Home/Move/Group/PSO/PositionCompare | AUT-MO-002 限位/跟随误差/伺服掉电/超时 | AUT-MO-003 ControllerDone+ActualPosition；G2/G3 |
| FluidPowerActuation | AUT-FL-001 夹具伸缩/真空吸附/压力稳定 | AUT-FL-002 夹紧ACK无到位/失压/泄漏 | AUT-FL-003 双传感器+压力+限时证据；G1/G3 |
| RobotEngineering | AUT-RO-001 Tool/Pose/Frame/Program/抓取反馈 | AUT-RO-002 错模式/坐标系漂移/安全区阻断 | AUT-RO-003 Tool版本+ActualPose+控制器程序结果；G2/G3 |
| MaterialHandlingSystems | AUT-MH-001 Carrier/Buffer占用与双工站交接 | AUT-MH-002 堵料/接收未就绪/重复送料 | AUT-MH-003 CarrierId/TransferId与两端握手；G1/G3 |
| ProcessControl | AUT-PC-001 PV/SP/MV、工艺Phase切换/控制器Readback | AUT-PC-002 PV Bad/超限Trip/批次中断 | AUT-PC-003 Loop/Phase/Controller状态与Trace；G1/G3 |
| AcquisitionTrigger | AUT-AT-001 硬触发/位置触发→帧与TriggerId关联 | AUT-AT-002 丢帧/乱序/重复/回调迟到 | AUT-AT-003 Frame/Trigger/Clock/Lease元数据；G1/G2 |
| XraySourceDetector | AUT-XR-001 授权曝光→探测器帧→设备停止 | AUT-XR-002 安全门失效/源故障/出束未知 | AUT-XR-003 安全控制器+曝光状态+FrameRef；G3 |
| InstrumentationDaq | AUT-ID-001 量程/同步采样/波形/计量不确定度 | AUT-ID-002 越量程/校准过期/采样丢包 | AUT-ID-003 原始Sample+单位+标定证书版本；G1/G2 |
| TestMeasurement | AUT-TM-001 气密/耐压等DUT/Fixture/Procedure/Measure | AUT-TM-002 试件丢失/环境不稳定/激励错误 | AUT-TM-003 测量曲线+原始事实→Inspection；G2/G3 |
| IndustrialConnectivity | AUT-IC-001 多规约/多站点/会话有序复用 | AUT-IC-002 CRC/PDU/重连、子会话冲突、旧写重放 | AUT-IC-003 F17+MCN/MPA真实报文/SessionGeneration；G1/G2 |
| IndustrialIoTEdge | AUT-IE-001 Edge型号点表/遥测/断网补传去重 | AUT-IE-002 不可信来源/过期令牌/危险写重放 | AUT-IE-003 Origin/Epoch/Sequence/Trace；G1/G2 |
| RecipeParameterEngineering | AUT-RP-001 审批→Stage→多参数Readback→生效版本 | AUT-RP-002 部分失败/物料不符/断连后未知 | AUT-RP-003 参数差异、操作者、EffectiveVersion；G1/G3 |
| CommissioningDiagnostics | AUT-CD-001 PLC/轴/采集联调FAT/SAT报告 | AUT-CD-002 ModeDenied/未授权点动/校准过期 | AUT-CD-003 检查项输入/时间/设备证据和签核；G1/G3 |
| AssetMaintenance | AUT-AM-001寿命/点检/备件/校准记录→受控恢复 | AUT-AM-002过期校准/部件已换未复检 | AUT-AM-003 AssetId+BOM/工单/新Version；G1/G3/T4 |
| MachineSafetyIntegration | AUT-MS-001只读安全状态与受控许可检查 | AUT-MS-002未闭合门/未知安全状态/误复位 | AUT-MS-003来自合格控制器的物理状态、风险评估；G3 |
**测试前缀来自 V2.2 `ProfessionalAreaName`，不意味着 P01～P18 已逐格复核完成。**
### 11. 一个能力的最小自动测试包
每个 `CapabilityId` 至少构造：ValidParameters、InvalidSchema/Unit、PermissionDenied、InterlockUnknown、DeviceNotReady、LeaseBusy、ProtocolAckOnly、PhysicalDone、TimeoutUnknown、CancellationStop、RestartGeneration、DisposeLateCallback、ConfigurationSaveReopen、EnvironmentVirtualCannotSignPhysical。不能一套用例硬套所有设备；不适用给理由和专有替代用例。
### 12. 六大标杆真实闭环+统一追踪
| Case | 正向完整链 | 最关键异常 | 真实现场最低证据 |
|---|---|---|---|
| AUT-E2E-01 PLC/IO/Fluid/Motion/搬运 | 料到→夹紧压力→回零/定位→搬运交接→工位签收 | 丢料、压力不足、错误限位、未知位置、重复交接 | 真实 PLC+轴/阀+传感器 |
| AUT-E2E-02 Robot/Camera/Vision | 工具/手眼版本→Pose就绪→硬触发→Vision结果→机器人执行 | 标定过期、Tool变更、帧延迟、模式切换 | 实际Robot+Camera与时间/位姿 |
| AUT-E2E-03 X-Ray/Detector/CT3D/Inspection | 门/高压许可→源曝光→触发→帧/投影→重建→缺陷结果 | 门失效、出束状态未知、帧无TriggerId、停机异常 | 安全PLC+源探测器+成像实测 |
| AUT-E2E-04 DAQ/Test/Recipe/Inspection | 配方/工装→激励→稳定→采样→不确定度→判定 | 越量程、工装错误、结果孤儿、校准失效 | 仪器/夹具/DUT与测量原始数据 |
| AUT-E2E-05 SCADA/Process/HMI | 多站遥测→PV/SP/MV→报警/SOE→历史→受控遥调 | 断链/错误时标/操作拒绝/历史缺段 | 实际 PLC/DCS/站点报文+历史 |
| AUT-E2E-06 IoT/Edge/Maintenance | 产品模型→网关/点表→遥测→离线去重→维护单→恢复 | 重放危险指令、跨站混叠、设备版本漂移 | 真实Gateway/Edge+重连证据 |
每包记录 `ProjectRevision/DeviceRef/AssetRef/SessionGeneration/OperationId/TriggerId?/ProductId?/TraceId/RealEvidenceId/Result`，形成一条可点击的工作台时序与测试产物。
### 13. 软件性能目标：先建立可靠基准再冻结阈值
| 指标 | 采集维度 | 回归场景 |
|---|---|---|
| Point吞吐、SampleAge | 点数/站数/扫描间隔、P50/P95/P99 SampleAge与Bad/Stale率 | 高密度PLC实时趋势+重连 |
| Command Latency | Authorize/Lease/Tx/ACK/PhysicalDone分别P95/P99 | 小时级、多会话、写冲突 |
| TriggerSync | 触发→帧分位偏移/漂移/最大抖动、丢帧 | 高速线扫/运动触发 |
| Memory/Handles | Managed/Native/SDK Buffer/租约/订阅数和增长率 | 8h/24h/72h连续采集与重启 |
| Recovery | 重连到ProtocolReady/DeviceReady、超时Unknown与恢复时长 | 断网/掉电/SDK crash |
| Studio Responsiveness | 点表规模、过滤/编辑/载入/告警刷新UI延迟、DPI | 规模扩展/键盘/UIA |
实际业务阈值按工艺节拍、设备SDK和HIL基准批准；不得仅凭 `Thread.Sleep` 或普通PC软件宣称硬实时。
### 14. 安全/可靠/网络安全负例矩阵
- **机器安全**：EStop、门锁、光幕、STO/SS1/SLS只读取合格现场系统状态；故障注入设备安全状态未知时，软件拒绝危险指令；安全功能认证由有资质人员与设备体系评估。
- **网络安全**：无效证书/身份、跨站越权、M2M令牌过期、固件版本漂移、MQTT事件重放、敏感报文/日志脱敏；参考 ISA/IEC62443，不能自证认证。
- **功能稳健**：掉电、设备突然重启、晚到ACK、SDK跨线程回调、重复调用Stop、GC与Native资源释放；不能有悬挂锁/幽灵Ready/假PhysicalDone。
- **数据完整性**：设备原始时戳/主机时戳/质量与序列版本、重复帧/归零/时间漂移，跨Workbench关联时不得丢 DeviceRef/TriggerId/FrameId/校准版本。
### 15. G1 Windows CI 运行与报告模板
测试必须与当前一次提交 SHA 精确绑定，输出：`RunUrl/JobName/CommitSHA/TestId/Owner/Fixture/Environment/Expected/Actual/PassFail/TraceFile/Preconditions/References`。优先复用现有 Connector Loop、Driver Readiness、Variable System、TestCenter、ArchitectureSmoke；不能只核文件名、模拟按钮、文字匹配。
G2/G3 报告另记 `Model/SN/Firmware/ControllerSafeStatus/CalibrationId/SDKx64/Operator/License/PhysicalTrace`。无工厂/设备许可或硬件不在场时必须显示 Blocked/NotRun 而非 Pass。
### 16. 全部验收结束门槛
- 18专业方面都有定义/能力/专业Editor/API/状态/异常/Test ID，互相Owner不冲突；
- 六大完整案例在模拟G1和规定设备G2/G3具有有效证据，产品级结果由Inspection负责；
- 无危险命令的ACK即成功、旧Generation重放、质量差执行、伪Ready、内存泄漏/线程残留；
- 模型、点表、配方、Runner版本冻结/保存重开/升级与回滚均经过真实回归；
- 现场验证和 Windows CI 单独归档，不夸大通过范围，全部重大风险有Owner/措施/期限。

### 17. PLC多点扫描 G1 固定证据（2026-10-30）

- **源码**：`Professional/Automation/Plc/PADPlcPointBridge.cs`（`ScanAsync`、`PADPlcScanResult`、`PADPlcPointScanResult`）；正式项目仍为 `CxVisual.Professional.Automation`，无独立Scanner工程/协议/变量仓。
- **自动用例**：`Tests/ArchitectureSmoke/Integration/AutomationPlcPointTests.cs`，原生Windows TCP回环+F17+MCN/MPA+PAD ProviderHost+唯一MDS：两个已绑定寄存器逐点03真实报文、返回顺序及UInt16值、1～64约束、重复/未知/空集合/取消在发包前拒绝、单点设备超时 Stale 且保留真实值/RefreshUtc、后续点Good、恢复后实际重新读回Good。
- **固定证据**：`279a45cc6fcdb604a325720d57e4e98cbddcbbb9`，Windows G1 [Actions run 41013766404](https://github.com/oudream/cx-visual-x3/actions/runs/41013766404)：**success**。源码在 `06f1c1b4dee9a3225f499768d768ec62cbad0275`，原验收矩阵不把新API误标为Modbus单报文合并批读。
- **证据解释**：此批只达到G1真实Windows编译+软件协议/状态集成，逐点分离的FC03应答不能证明现场PLC硬件读数、采样速率、跨点原子一致性或物理安全。G2/G3尚无证据；连续块FC03批读、动态采样调度、Studio/TestCenter点表面板和性能 P95/P99 仍未验收。
- **后续归档**：AUT-PI-009（双点正常）、AUT-PI-010（非法预检与取消）、AUT-PI-011（单点超时隔离/恢复）、AUT-PI-012（设备代次变更/PLC断开多点）、AUT-PI-013（连续地址合并性能优化与真实设备确认）。其中009～011已有G1运行成功，012～013尚待专有场景及性能/硬件证据。

### 18. AUT-PI-013 连续FC03真实软件集成与批读失败门禁（2026-10-30）

**本轮源码与API**：`IMPAProtocolContiguousPointReader`（Mechanism.Connectivity可选合同）、`MPAModbusTcpProtocol.ReadContiguousPointsAsync`（真实MBAP RequestAsync、校验站号/连续地址/Count=1～125/Int16-UInt16/响应长度）、`MCNConnectorModule.ReadContiguousPointsAsync`（Session/Channel合法性）、`PADPlcPointBridge.ScanContiguousAsync`（1～64个稳定ID与MDS ProviderHost）。数据值唯一Owner依旧是MDS。

**正例**：真实本机TCP Loopback向holding:100/101发一次 `FC03 start=100 count=2`；返回两个强类型值，核对请求数量仅+1、BlockReads+1、两点MDS Good，普通 `ScanAsync` 的逐点独立事务原有用例保持通过。

**负例**：反向地址、重复StableId、空集合必须在发包前拒绝；针对FC03 count=2，设备返回长度仅1个寄存器的PDU，应被正式MPA校验为 `InvalidDataException`，本次读取**不发布任何半批Good值**，旧数据及RefreshUtc保留且两点质量Stale，恢复后必须再次真实FC03才能恢复Good。

**测试与实际通过状态**：固定提交 `0db3c0b9484cc8e1c6ca96d1097df9a8e02fbaed`；[Automation PLC Windows G1](https://github.com/oudream/cx-visual-x3/actions/runs/41015075474)、[Config L1](https://github.com/oudream/cx-visual-x3/actions/runs/41015075572)。**本条初次填写时均为Queued，CI有明确Success后才能标识G1 Passed；不能将“已提交”算作“已验收”。** G2真实PLC、跨设备时间一致性及G3安全现场均未执行。

**非目标**：没有进行工业硬实时宣称、跨点MDS原子事务、周期调度、独立线程循环或Studio/TestCenter专业UI。需要时先复用已实现的通道诊断/变量订阅/TestCenter展示组件。

### 19. AUT-PI-014 TestCenter PLC只读点表操作与五项UIA（2026-10-30）

- **真实UI入口**：`TCTManualModuleFrame` 复用MPW模块中心的Connector专业分页 `connector-plc`，`TCTPlcReadOnlyPanel` 强制MHS Effective、草稿未保存拒绝、`modbus.tcp`/`TcpClient`类型检查、操作者显式连接/点名选择/读取/停止。网络未连接不产生假结果，不提供FC06或旁路设备写权限。
- **实际执行**：正式 `FTRTcpClientChannel` → `MCNConnectorModule` → `MPAModbusTcpProtocol` → `ReadContiguousPointsAsync`；按单帧FC03读取1～64个同站相邻寄存器。界面只保留当次 `TCTPlcPointReadEvidence` 诊断值，不生成另一份MDS Current/Quality；失败时清空上一次结果。
- **可自动操作点位**：`TCT.Module.Page.connector-plc`、`TCT.Plc.Connect`、`TCT.Plc.PointNames`、`TCT.Plc.Read`、`TCT.Plc.Stop`、`TCT.Plc.ReadEvidence`、`TCT.Plc.Status`。
- **真实UIA测试**：`TCTPlcReadOnlyUiSmoke` 在实际WPF窗口中从MHS保存生效Connector、通过TCP测试设备读取holding:100/101并验证90/99及UTC、仅一帧、非法反序不发包且清除旧结果、显式断链。原有Compute/Simulation/Service/Connector四项UIA仍要同时通过。固定运行时需上传五份同SHA报告。
- **证据限制**：本次新增WPF UIA采用Windows TCP本机夹具，是软件G1而非真实PLC G2；现场设备身份/安全/固件及真实MDS Provider采样需另外核实。源码和UIA测试已提交，五项新工作流结论以新SHA的Actions为准；未完成前不得填PASS。

### 20. AUT-PI-015 Host周期采样、故障传播与停止（2026-10-30）

**专业代码**：`Professional/Automation/Plc/PADPlcPollingSession.cs`。组合唯一`Foundation.Infrastructure.Concurrency.FCRPeriodicJob`，不创建额外Timer/Worker或MDS数据仓，生命周期由显式Host控制。配置：1～64个唯一点、20ms～1h有效间隔，普通`ScanAsync`逐点或`ScanContiguousAsync`连续FC03，两者仍走真实MCN/MPA/F17/MDS ProviderHost。

**G1自动验收点**：
- `AUT-PI-015A`：80ms间隔轮询真实TCP Modbus两个寄存器不少于2轮，`SuccessfulCycles`与实物请求数一致，无主动写入FC06；
- `AUT-PI-015B`：连续批读模式每轮单个FC03 Count=2，保留原协议地址/站号校验；
- `AUT-PI-015C`：丢弃一次只读输入FC03回包，得到`PartialCycles>=1`且该点Stale，恢复后的下一轮真实设备采样才恢复Good；
- `AUT-PI-015D`：重复启动、重复点、非法周期、未启动零网络副作用；`StopAsync`等待当前轮退出，超过周期后无剩余网络报文，旧会话不得重启；
- `AUT-PI-015E`：运行期篡改点表地址，延后首轮在发包前Faulted、网络请求不增长、MDS旧值和RefreshUtc保持且Quality变Stale；修复后必须经真实FC03才能再得Good。

**证据等级**：正式代码提交 `30cbba9f8e2fbbb34c5da319e76c53967fc6c3e8`，测试提交 `60e434fdb00085a4fefcc18ae16c7aa0789ac501` 与增强 `1815b07383bcc61eafc9d2aa26d1135e7b2e9af8`。对应Windows G1 [41020212133](https://github.com/oudream/cx-visual-x3/actions/runs/41020212133)；填写时尚为Queued，只有Actions明确success才能改成软件G1 PASS。Windows本机TCP夹具≠实机PLC、HIL、安全、实时抖动验证。
