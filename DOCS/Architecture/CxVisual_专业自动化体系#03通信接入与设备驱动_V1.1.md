# CxVisual Automation——03 通信接入与设备驱动 V1.1
> 版本 V1.1｜2026-10-29｜性质：目标架构与开发合同，非功能完成声明；代码基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8 / WPF / x64。
> 适用范围：Channel/Protocol/Connector/Driver/PointTable；按 `AGENTS.md`、工程 Skill、`DOCS/00～04`、最新 Owner/ADR 执行。新增类型前必须审查 DOCS/02～04 XLSX 的原始 Sheet、真实 csproj 与源码。
> 状态规范：【S】本次已读源码，【R】旧仓资产，【T】目标设计，【V】真实CI/测试证据，【H】待HIL；不能以目标接口推断现有实现。
> 重要限制：本次 GitHub 文本接口无法解码 `DOCS/能力图/04Automation.png`、其余 Workbench PNG及 02～04 XLSX 二进制；位置/名称已核实但图内项目和 Sheet 内容尚待原图交叉校验。能力图不作为未经核实的逐条事实。

**总原则：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

## 1. 三条实现路径
**Connector 路径**：常规 PLC/Modbus、工业规约、工业 IoT，使用 F17 Channel → MPA Protocol/Session/PointTable → MCN Connector → 专业数据/动作；通用规约不再包装成一个设备 SDK Driver。
**Driver 路径**：厂家 SDK 的相机、探测器、控制卡；使用 `MFMDriverModuleConfiguration` 的 Sdk/InternalTransport/Hybrid，缺少真实通道时不创建 EmptyChannel。
**组合路径**：一个逻辑设备同时需要多个独立连接/SDK；由 MEM/MFM/MRK 组合多个模块与资源，不在 Driver 内自建另一个 TaskEngine。
## 2. 责任交接图
~~~mermaid
sequenceDiagram
 actor Eng as 工程师/宿主
 participant MEM as MEM/FCM
 participant MCN as MCN Connector
 participant FTR as F17 Channel
 participant MPA as MPA Protocol
 participant PAD as Automation Provider
 participant MDS as MDS
 participant HW as PLC/设备
 Eng->>MEM: 选择设备+Schema/点表+校验
 MEM->>MCN: 装配已有 Connector 配置
 MCN->>FTR: Open(实际端点)
 FTR->>HW: 连接/IO字节
 HW-->>FTR: 帧
 FTR-->>MPA: 解析/匹配会话
 MPA-->>MCN: 类型化点值
 MCN-->>PAD: 映射设备稳定ID/单位
 PAD->>MDS: Commit(value,quality,time)
 MDS-->>Eng: 快照/订阅/可视监控
~~~
## 3. 模块边界
- F17：`IFTRChannel`、实际 TCP/UDP/Serial/NamedPipe、SubChannel、收发/关闭与底层诊断；通用传输唯一Owner。
- Mechanism.Connectivity：`IMCNConnectorModule`、`RequestAsync`、`ReadDevicePointAsync`、`WriteDevicePointAsync`、MPA Modbus、PointTable、Session/Mapping。当前 csproj **真实引用** Foundation.Transport、Foundation.Data、Mechanism.Core。
- Mechanism.Core：`MFMModuleKind.Driver`、`IMFMDriverModule`、`MFMDriverModuleConfiguration`、`MFMDriverBackendKind`；五类模块 Connector/Driver/Compute/Simulation/Service。
- Professional.Automation：设备专业 Parameter/Provider/Session/Control 策略；当前 PAD Provider 的 WriteAsync 注入 WriteBackend + authorizeWrite，未注入时默认拒绝；实际硬件执行完成仍未因此得到证明。
## 4. 点表与写入
点表字段建议：StablePointId、DeviceRef、Direction、LogicalType、Unit、Scale、CalibrationRef、Address/Station、Endian/WordOrder、Interval、QualityPolicy、Writable/AuthPolicy、Version。地址、站号由规约 Owner 承载；`PADDeviceVariableCatalog` 仅管理专业变量定义和 MDS 映射，不复制 MPA PointTable。
写入阶段至少区分 `Denied/Prepared/Sent/ProtocolAcknowledged/Applied/Observed/UnknownOutcome/Rejected`，**业务成功**至少需要现场回读或受信的动作完成证据；超时动作不得盲目自动重发。
## 5. 关键回归
- Modbus TCP：功能码、事务 ID、异常帧、MBAP长度、粘拆包、乱序回执、断线、超时、重复地址/越界、字节/字序/批量轮询。
- Modbus RTU：CRC、站号1～247、485多站共享链路、互斥请求、半双工超时。
- 多端：一个 TCP/UDP Server 父通道管理 SubChannel，Generation 隔离；多个独立规约优先多个 Connector。
- SDK：DLL缺失、x64不匹配、许可、设备不在线、回调重入、DMA帧所有权、热拔插、Reinitialize、Close顺序。
- 性能：量化吞吐/延迟/积压、背压、资源、CPU；不可凭IO回调声明硬实时。
## 6. 旧资产复用结论
`ReferTo/Protocols/ModbusTcpModule`、`ReferTo/DeviceVariable/DeviceInterfaces/DriverModuleBase.cs`、`cx-vision-x2/Drivers/S7DriverModule` 等是历史行为证据；`Professional/Automation/Connectors/Porting/PACModbusTcpDriver.cs` 标记 NOT INCLUDED IN PROJECT，不计 G1。优先迁移功能与测试，删除旧 `CommonInterfaces` 全局管理器依赖；先核查 Connectivity 是否已实现相等行为，避免第二份协议栈。


---

## V1.1｜多层协议与专业驱动的完整运行合同
### 7. 三种模块和六种身份不混淆
| 概念 | 现行 Owner/类别 | 身份 | 作用 |
|---|---|---|---|
| Channel | Foundation.Transport/F17 | ChannelId、Endpoint、Generation、SubChannelId | 字节/帧传输，不懂PLC业务 |
| Protocol | Mechanism.Connectivity/MPA | ProtocolTypeId/Version/Session/PointTable | 报文、站号、读写关联、类型映射 |
| Connector | Mechanism.Connectivity/MCN；MFM Connector | ModuleId/ConfigVersion | 一个逻辑协议连接的可配置组合 |
| Driver | MFM公共合同、Automation专有Provider | DriverTypeId/DeviceRef | 厂商SDK/设备动作、生命周期/状态、物理完成 |
| Device | MEM+Automation | DeviceRef/AssetRef/ModelRef | 设备、工站、能力拓扑，不等于连接 |
| Operation | M12/MRK+Automation结果 | CommandId/LeaseId/CorrelationId | 有权限、资源和物理反馈的动作 |
**现行裁决**：一个普通Connector只配置一个父Channel+一个Protocol，多对端为SubChannel；RS485多站复用同Serial+ModbusRTU Protocol，点按StationAddress区分；复合/多路径设备由工程组合多个Connector/Driver。非厂商SDK的纯Modbus TCP不可为获得“一设备一Driver”的假抽象而复制整个协议栈。
### 8. 可用性状态与 Provider 生命周期
`Discovered→Validated→Configured→Connecting→ProtocolReady→DeviceReady→Busy→Degraded/Faulted→Stopping→Released`；连接已建立不等于 DeviceReady，驱动 DLL 存在不等于 ProviderResolved，心跳健康不等于现场模式许可。
严格区分 Alive / Reachable / ProtocolReady / DeviceReady / SafeToCommand / PhysicalCompleted。设备授权和物理条件必须在命令时重新核验，不能将上次Ready无限沿用。Generation变化导致所有旧请求/回调/命令结果失效或进入Unknown等待核实。
### 9. PointMap/Import/Converter 完整合同
`PointDefinition{StablePointId,DeviceRef,LogicalType,Unit,Access,QualityPolicy,SamplePolicy}`；`ProtocolPointBinding{ConnectorRef,ProtocolRef,StationAddress,NativeAddress,NativeType,Endian,WordOrder,BitOffset,Scale,Offset,Version}`；`PointObservation{Value,Quality,SourceTime,ObservedTime,CommittedTime,Revision,SourceId}`。以上为概念槽位，正式类型先查 MPA/MDS/PAD。
导入 CSV/Excel 流程：解析→候选预览→重复ID与重叠寄存器/站号校验→字序/单位/量程校验→前后配置差异→生成不可变绑定Plan→确认应用→版本冻结。不同设备独立的原生通讯配置可共享型号模板，但禁止共享有状态请求ID或错误缓存。
### 10. 协议族完整度矩阵（支持级别必须有源证据）
| 族 | 专业能力/地址 | 底层复用 | 现场测试关键点 |
|---|---|---|---|
| Modbus TCP/RTU | Holding/Input/Coils/Discrete、寄存器类型、站号 | F17+MPA | MBAP/CRC、Exception、批量、并发 |
| S7/FINS/Mitsubishi PLC | 数据区/位/字、诊断/访问权限 | F17+MPA插件 | 厂商版本、PDU、断连重试与写回读 |
| OPC UA | NodeId/Namespace、Browse、订阅/Method/事件 | Channel/Protocol插件，标准信息模型 | 证书、Session/Subscription、质量与时标 |
| EtherCAT/PROFINET/IO-Link等 | 站点/模块/设备诊断和参数 | 专用控制器/板卡SDK，可通过Driver | 普通PC不保证总线硬实时，ESI/GSD/IODD映射与版本 |
| EtherNet/IP、CAN/CANopen | 对象字典/Assembly/EDS、状态 | F17/专用SDK按实际适配 | 长帧/连接异常/模式与互锁 |
| SCPI、MQTT、HTTP/IEC104 | 仪器指令、遥测/事件、站点控制 | F17/MCN/MPA/Edge | 应答/会话/消息QoS不冒充物理执行 |
标为“规划/待查重”，绝不因列表出现就宣称实现任何标准的互通性认证。
### 11. 统一操作阶段与回读
~~~mermaid
sequenceDiagram
 actor H as HMI/SDK
 participant C as M12 Command
 participant R as MRK Lease+Policy
 participant A as Automation Professional Provider
 participant P as MCN/MPA
 participant IO as F17
 participant HW as PLC/Hardware
 H->>C: Request(CommandId, Target, Preconditions)
 C->>R: Authorize+Reserve
 R->>A: Invoke with Cancellation/Generation
 A->>P: WritePoint/DeviceAction
 P->>IO: Tx Request
 IO->>HW: Bytes
 HW-->>P: ProtocolAck
 P-->>A: ProtocolAcknowledged
 A->>P: Readback/DeviceDone
 P-->>A: Actual state+Timestamp
 A-->>C: Applied/Observed or Unknown
 C-->>H: Final status+evidence
~~~
`PADDeviceVariableProvider.WriteAsync()` 当前仅返回 bool 后端写调用结果；不要将它当成完整物理结果。具体结果以 M12/MRK 的既有 Command/Operation 类型承接，禁止新增第二套公共 CommandCenter。
### 12. 边界负例与兼容
- 设备离线/地址重叠/只读/权限缺失/过期Schema→明确拒绝；断线导致MDS Quality变差，但最后值保留且显式标识。
- 旧 `PACModbusTcpDriver.cs` 是未编译 Porting 参考，不直接并入项目；先比较现行 MPA Modbus 与原类能力，再选择按方法迁移。
- 自动重连只能恢复连接并重验证Ready，不能自动重发危险或非幂等动作；所有 Session/Buffer/Callback 在 Stop 有界释放，重新连接 Generation 必变。
- 真实安全 PLC/运动控制网络与一般企业通信分区部署，审计记录不泄漏厂家密码/访问令牌。
