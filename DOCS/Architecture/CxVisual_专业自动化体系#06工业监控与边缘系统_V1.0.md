# CxVisual Automation——06 工业监控与边缘系统
> 版本 V1.0｜2026-10-29｜性质：目标架构/实施合同，非功能已完成声明；核查基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8/WPF/x64。
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
