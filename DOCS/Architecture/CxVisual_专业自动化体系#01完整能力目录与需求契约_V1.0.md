# CxVisual Automation——01 完整能力目录与需求契约
> 版本 V1.0｜2026-10-29｜性质：目标架构与开发合同，非功能完成声明；代码基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8 / WPF / x64。
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
