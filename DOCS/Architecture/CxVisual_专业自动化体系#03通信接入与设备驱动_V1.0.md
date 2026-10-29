# CxVisual Automation——03 通信接入与设备驱动
> 版本 V1.0｜2026-10-29｜性质：目标架构与开发合同，非功能完成声明；代码基线 `3960b09f18604bbd8a486fe947a6a840e3eb5b4d`；.NET Framework 4.8 / WPF / x64。
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
