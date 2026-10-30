# CxVisual Automation——06 工业监控与边缘系统 V1.1
> 版本 V1.1｜2026-10-29｜性质：目标架构/实施合同，非功能已完成声明；核查基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8/WPF/x64。
> 适用范围：PLC/ICS/SCADA/IoT/Edge。权威：AGENTS、开发 Skill、DOCS/00～04、F17裁决、五类模块裁决及实际源码；P01～P18编号最终逐项以二进制 DOCS/02 和 Automation能力图对照。
> 说明：PNG、XLSX 已确认仓内路径，当前工具只能读取 GitHub UTF-8 文本，无法直接解析图中条目或 XLSX Sheet；需将原件另行审查后完成逐条核对。状态【S源码】【R遗产】【T目标】【V自动证据】【H现场证据】不可混用。

**核心原则：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 1. 功能边界
工业监控专业负责工艺遥测/遥信/遥控/遥调、测点语义、现场报警、控制反馈与边缘设备协作；通用 Variable/History/Message/Command 仍由 MDS/M35/M36/M12 持有；HMI 持有趋势/告警界面的控件与可视化，Automation 只声明专业规则和语义。
## 2. 设备模型与点位
复用 `device_communicator` 中 `DeviceBase/DeviceAttrBase/ChannelDevice/ChannelDeviceAttr` 的语义分离，考虑 Product/DeviceModel、属性模板、部署点表、采样映射、通道关联、多租户/边缘身份，但不能直接移植其数据库模型作为 MEM 的第二真源。
复用 `gcl3` 的 `Terminal/Protocol/Worker/Measure`、GeneralAccess/PSM/Bus、RTDB/共享内存及历史流通思路；新平台遵守 F17/M13～15/M09/M12/M35/M36 各自 Owner，禁止把整套 C++ 总线平移到 Automation DLL。
## 3. 监控对象
| 专业对象 | 建议字段/事实 | 真源 |
|---|---|---|
| ScadaPointModel | PointId、工程量、站号、质量规则、Deadband | 专业静态声明+MPA |
| RealtimeObservation | Value、Quality、Source/Receive/Commit Time、Revision、SourceId | MDS |
| AlarmRule | Severity、Condition、Delay、AckPolicy、Suppression、Correlation | 专业规则+公共 Event/History |
| DeviceHealth | Alive/Ready/Healthy、Communications、HardwareFault、LastGoodAt | 专业/Runtime |
| EdgeRoute | DeviceRef、GatewayRef、Topic、QualityPolicy、Origin、Identity | MCN/运行装配 |
| ControlIntent | Principal、Target、ExpectedState、Deadline、Audit、Idempotency | M12/安全Owner |
## 4. 数据/命令双向链
~~~mermaid
flowchart LR
 PLC["PLC/RTU/仪表"] --> F["F17 Channel"]
 F --> P["MPA Protocol / MCN Mapping"]
 P --> D["MDS实时变量"]
 D --> A["Automation专业报警规则"]
 D --> H["HMI监控/趋势"]
 D --> T["公共历史/消息/IoT出口"]
 U["HMI/业务操作者"] --> C["M12 Command"]
 C --> S["授权/联锁/MRK"]
 S --> P
 P --> PLC
 PLC --> R["设备反馈/回读"]
 R --> D
~~~
命令路径必须携带 CommandId/CorrelationId/SessionGeneration/Deadline；本地图表刷新、MQTT 发布 ACK 或 TCP SendSuccess 都不能作为现场执行成功。
## 5. 监控细节
- 采样可用定周期/变化/事件推送/OnDemand，按同设备/同连接有界调度；不同步写屏幕。
- 质量至少 Good/Uncertain/Bad/Disconnected/Stale，异常质量值可显示最后值但必须显式标识，禁止把断线视为正常 0。
- Alarm 有 Active→Acknowledged→Cleared、抑制/维护/人工处理、离线追补；实际报警历史为公共 History 实现，不能新建私有存储。
- Edge Offline/Replay：保留来源时间、代次、去重 Key、消息版本、过期/重放规则；禁止离线恢复后自动重发非幂等危险控制。
- SCADA 与 IoT 的身份/网络安全：TLS/身份认证/最小权限/证书更新/秘钥外部管理/审计；生产网络隔离及合规按部署要求。
## 6. 跨进程边界
`device_communicator` Go 的 Monitor/Daemon 已有生产资产，`Services/Supervision` 是 x3 中的 Go 监控/守护服务迁入方向。Automation 不以 .NET 私有线程复制进程守护；通过稳定的版本化监控/命令接口协同，并区别 Heartbeat/ServiceReady/DeviceReady。
## 7. 工业案例
CASE-SCA：一条产线三个工站多个 PLC→站号/点表→MDS→报警/趋势/HMI→授权遥控→PLC确认/回读→历史审计→断网后质量降级→恢复重同步。
CASE-IOT：型号/测点模板→绑定物理设备/网关→边缘采样→质量和时间→上报→断网缓存→重连/去重→人工重新授权危险命令。
## 8. 验收
SCA-001～012：双站混合、重复点拒绝、量程与死区、品质转换、时戳排序、报警延迟/回落、历史一致、遥控权限、长时间断网、重放/去重、gateway重新启动后资源闭合、SCADA真实设备回读。非安全等级的业务报警不能冒称 SIS/EStop 安全功能。


---

## V1.1｜过程控制、配方、SCADA、IoT与资产健康完整专业链
### 9. 六个专业关注域与统一信息模型
| Domain | 工程对象和数据合同 | 运行行为/用户入口 | 异常与证据 |
|---|---|---|---|
| ScadaIcs | Site/Tag/Telemetry/Control/AlarmSource/SOE | 站点与点表、遥测遥信遥控遥调、报警与趋势 | SourceTime+Quality，ACK/解除/恢复，历史重放 |
| ProcessControl | ProcessUnit/ControlLoop/PV/SP/MV/Phase/Trip | P&ID、过程回路、批次/阶段、受控设定值 | PV死区、质量差、超限Trip、控制器Done |
| RecipeParameterEngineering | Recipe/ParameterSet/Version/TargetBinding/Approval | 配方编辑、差异比较、阶段下发、回读/签收 | 部分下发、回滚失败、参数不一致 |
| IndustrialIoTEdge | Product/Attr/Event/Link/EdgeNode/StoreForward | IoT型号/测点映射、边缘采集、离线缓存、云边状态 | 重连去重、乱序、过期数据、禁止危险命令回放 |
| CommissioningDiagnostics | DiagnosticCheck/Session/SignalScope/FatSatEvidence | 点检清单、线下/在线调试、信号时序、故障链 | 测试权限、机器模式、审批/报告 |
| AssetMaintenance | Asset/BOM/Spare/ConditionMetric/CalibrationRef | 设备寿命、保养、健康趋势、备件、维护工单 | 过期校准/寿命阈值/停机履历 |
上述实体不建立新的公共变量仓、消息中心、Historian 或 JobScheduler；只增加专业语义、规则和视图贡献。
### 10. 过程控制专业合同（ISA-88/ISA-95启发）
- **Equipment & Unit**：工艺罐/锅炉/泵/阀/温控回路，与 MEM 的设备 AssetRef 对齐。
- **ControlLoop**：PV ProcessValue、SP Setpoint、MV ManipulatedValue、Range/Unit/AlarmLimit、Mode Auto/Manual/Cascade、ControlSource；PID控制环的确定性执行应在PLC/DCS/专用控制器，CxVisual用受控 Setpoint/Readback 和监视合同。
- **Phase & Procedure**：配料/加热/保温/冷却/排液等专业步骤，公共 Decision/Runtime 负责步骤编排，控制器负责物理过程；Trip/Interlock只读事实和控制前置条件必须一致。
- **ProcessAlarm**：报警规则的 DomainPolicy 归 Automation，历史/通知和 HMI 呈现复用现有 MDS/M35/M36/HMI；持续工艺历史不只是文本日志。
### 11. 配方四阶段受控应用协议
~~~mermaid
sequenceDiagram
 participant U as Studio/批次操作员
 participant R as Recipe专业能力
 participant M as M12/MRK策略
 participant C as PLC/Controller
 participant A as Audit/History
 U->>R: Select(RecipeId,Revision,TargetStation)
 R->>M: Validate mode/material/tool/version/authorization
 M-->>R: Lease+Approved
 R->>C: Stage parameters (受控分组)
 C-->>R: ProtocolAck
 R->>C: Readback each critical setpoint
 C-->>R: ActualValues+EffectiveVersion
 R->>A: Commit outcome + differential + Trace
 R-->>U: Applied/Partial/Unknown/Rejected
~~~
配方编辑撤销与现场设备回滚严格分离。对没有安全事务支持的 PLC 参数分步下载，必须定义中断后如何确认和恢复；禁止把后续写入默认视为原子事务。材料/工装不匹配时拒绝配方生效。
### 12. SCADA 与报警状态合同
`Normal→ActiveUnacknowledged→ActiveAcknowledged→ReturnedUnacknowledged→Normal` 为常见报警状态模型之一；还要有 Suppressed/Shelved/OutOfService、BadQuality及审计，具体转换按现有报警 Owner 和目标行业规则裁定。SOE必须标识设备来源时标、主机接收时标与时钟同步质量；不能用展示刷新时间覆盖现场事件时间。
### 13. IoT/边缘/云与网络安全
- 沿用 device_communicator 的 Product/Device/Attr/ChannelDeviceAttr 模型分离；经 MEM资源、FCM配置、MCN/MPA接入并映射 MDS，不复制 Go IoT 数据库为平台第二工程真源。
- 保留 Go Monitor/Daemon/Supervision 的服务职责和跨进程接口，边缘心跳只表示 Alive；设备Ready必须从专业Provider/控制器确认。
- Telemetry 可缓存/重放，必须携带 Origin/Sequence/Epoch/Quality/SchemaVersion；命令控制默认不可离线重放，需要权限、幂等键、期限和会话代次重新验证。
- 工业网络分区/防火墙、证书更新、密钥托管、按设备/站点 RBAC与最小权限、审计与远程维护安全参照 ISA/IEC62443；不宣称已认证。
### 14. 资产维护与Commissioning不应沦为日志页
**维护闭环**：AssetRef→Condition/寿命计数→预警→维护工单→隔离/审批→更换备件/重新校准→受控再运行→验证报告。Maintenance WorkOrder 不是物理 Device，必须引用唯一资产。
**联调闭环**：计划(FAT/SAT)→设备电控/IO连接检查→主动测试许可→步进/点动/采图诊断→现场读回与故障证据→签核→发布基线。每一项诊断操作必须关联 Principal、Mode、Lease、设备真实结果与版本，不能因为按钮返回成功就通过点检。
### 15. 领域可视体验
P&ID过程图/Tag/回路及批次显示由HMI的绘制和视觉状态机制承载；Automation贡献工艺拓扑、AlarmSource、ProcessValue与受控操作。SCADA趋势、报警、区域导航等由HMI Renderer显示；Automation不重造WPF绘图控件。查看Point/Alarm/Recipe/Edge时应一键跳转真实 DeviceRef、Origin、ProtocolSession 和 Trace。
