# CxVisual Automation——完整功能、工业安全、性能与HIL验收矩阵 V1.0
> 2026-10-29｜设计目标/未执行测试清单。基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；严禁用静态源码阅读、仿真和文档提交替代真实 Windows CI 或物理 HIL。参考 DOCS/Architecture Automation #00～#08 及 Implementation，逐能力对应 AUT-*/CASE-*。
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
