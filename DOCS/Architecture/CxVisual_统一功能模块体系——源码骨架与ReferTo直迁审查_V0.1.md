# CxVisual 四种模块架构骨架与 ReferTo 直迁审查 V0.1

> 2026-10-22｜仅供设计审查（Review）｜从 `ReferTo/` 的 C# 成熟代码复制，`ReferTo/` 不做任何修改。
>
> **状态边界**：8 个公开契约文件已编入对应现有项目；9 份旧源码已复制、重命名并移入各 Owner 下的 `Porting/`，但**尚未加入 .csproj**，其旧依赖尚未清理，不能称为可运行驱动；不引入 Adapter 兼容层。测试与 CI 需要以本次提交实际结果为准。

## 1. 一张图看总体职责

```text
MEM 工程定义/引用 + 既有配置 Schema
       ↓
四种 MFMModuleKind（Connector / Compute / Simulation / Service）
       ├─ Connector：Mechanism.Connectivity (Channel + Protocol + PointBinding)
       │                   └─ 设备与业务驱动：Professional.Automation / Vision
       ├─ Compute  ：普通函数、各专业计算 Provider（无需统一继承大基类）
       ├─ Simulation：Professional.Simulation（虚拟时间、状态、模型）
       └─ Service ：Mechanism.Lifecycle 长服务合同 + 既有 Runtime.Host
       ↓
MOCCapabilityCatalog（唯一能力发现） → MOL/MOC/MRK（唯一编排、构建、执行）
       ↕
MDS（运行时 DataContext/Heap/Stack + 长寿命 Variables 作用域）
       ↓
Studio / TestCenter（投影、验收，不建设第二套执行/数据）
```

## 2. 八个编译契约（不承诺实际功能）

| 源码路径 | 当前审查类型 | 设计边界 |
|---|---|---|
| `Mechanism/Core/Modules/MFMModuleContracts.cs` | `MFMModuleKind`、`MFMModuleDescriptor`、`IMFMModule` | 四类及能力 Id；真实 Capability/端口仍由 MOC/MPC 拥有 |
| `Mechanism/Connectivity/Channels/MCCChannelProfile.cs` | `MCCChannelProfile` | 通道配置；已有 `IMCCChannel`/Virtual 是正式通道基础 |
| `Mechanism/Connectivity/Protocols/MPAProtocolContracts.cs` | `IMPAProtocolDefinition` | 组合已有 `IMPAProtocolCodec`，不叠旧协议运行壳 |
| `Mechanism/Connectivity/Connectors/MCNConnectorContracts.cs` | `MCNPointDefinition`、`IMCNConnectorModule` | 0..N Channel/Protocol/Point，允许无通道 |
| `Mechanism/Data/Variables/MDSVariableContracts.cs` | `MDSVariableSample<T>`、`IMDSVariableTable` | 长期变量值/更新时间；不复制 Flow Heap/Stack |
| `Professional/Vision/Modules/PVCComputeModuleContracts.cs` | `IPVCComputeModule` | 可选复杂计算；纯函数无需实现它 |
| `Professional/Simulation/Modules/PSMSimulationModuleContracts.cs` | `IPSMSimulationModule` | 仿真模型、虚拟时间与演化，不复制运行调度 |
| `Mechanism/Lifecycle/Modules/MLCServiceModuleContracts.cs` | `IMLCServiceModule` | 长服务可选生命周期，不新建第二 Host |

## 3. ReferTo 已直接复制的 C# 源码与重命名

| 参考源 | 目标位置（按 Owner） | 关键类型名 |
|---|---|---|
| `ReferTo/Channels/TcpClientBase.cs` | `Mechanism/Connectivity/Channels/Porting/MCCTcpClientBase.cs` | `MCCTcpClientBase` |
| `ReferTo/Channels/TcpClientChannel.cs` | `Mechanism/Connectivity/Channels/Porting/MCCTcpClientChannel.cs` | `MCCTcpClientChannel` |
| `ReferTo/Protocols/ModbusTcpModule/ModbusTcpProtocol.cs` | `Mechanism/Connectivity/Protocols/Porting/MPAModbusTcpProtocol.cs` | `MPAModbusTcpProtocol`、`MPAModbusTcpFrameCodec` |
| `ReferTo/Protocols/ModbusTcpModule/ModbusTcpDriver.cs` | `Professional/Automation/Connectors/Porting/PACModbusTcpDriver.cs` | `PACModbusTcpDriver` |
| `ReferTo/DeviceVariable/VariableInterfaces/VariableInfo.cs` | `Mechanism/Data/Variables/Porting/MDSVariableInfo.cs` | `MDSVariableInfo<T>` |
| `ReferTo/DeviceVariable/VariableInterfaces/VariableTable.cs` | `Mechanism/Data/Variables/Porting/MDSVariableTable.cs` | `MDSVariableTable` |
| `ReferTo/DeviceVariable/VariableInterfaces/VariableManager.cs` | `Mechanism/Data/Variables/Porting/MDSVariableRegistry.cs` | `MDSVariableRegistry` |
| `ReferTo/DeviceVariable/DeviceInterfaces/Point.cs` | `Mechanism/Connectivity/Connectors/Porting/MCNPointItem.cs` | `MCNPointItem` |
| `ReferTo/DeviceVariable/DeviceInterfaces/DriverModuleBase.cs` | `Professional/Automation/Connectors/Porting/PACConnectorDriverBase.cs` | `PACConnectorDriverBase` |

说明：`Porting` 是**临时迁移审查目录**而非运行时兼容层。审查后将整合到正式文件，消除 Porting；保留直接搬迁成熟行为，不靠 Adapter 间接委托。保留原旧代码的项目日志、旧依赖、旧注册模型等问题是为了防止未经审核的“格式替换”改坏业务语义。

## 4. 检查出的需要直接重构和删减的地方

1. **通道双合同**：`ReferTo/Channels/Contracts/IChannel` 同时含 `OpenAsync/CloseAsync/RequestAsync/Subscribe` 与 `ConnectAsync/DisconnectAsync/SendAsync/ReceiveAsync/Close` 两代动词；正式版只保留一套清晰语义，与现有 `IMCCChannel` 合并，而不是再加第三套。
2. **协议会话重复**：旧 `CxProtocolProvider`、`CxProtocolSession`、`ProtocolSession` 与具体 `ModbusTcpDriver` 分别承担相似的连接/收发/请求串行化职责；应按协议帧、协议请求、具体设备行为直接分拆后合并。
3. **变量双 Owner 风险**：`VariableTable` 保存设备长期状态（时间/初始化/变更），有意义；但 `VariableManager` 使用静态全局容器和后台线程，不适合不同工程及多会话隔离。正式唯一 Owner 是 `Mechanism.Data.Variables`，并保留 MDS 一次运行数据上下文的不同作用域语义。
4. **PointSheet 的外壳问题**：`PointSheet` 只提供按设备/变量的 List 查询，按功能并入 Connector 的 PointDefinition/PointCollection 即可；保留外部地址映射与专业设备对象语义，不把变量值复制第二份。
5. **设备两套管理**：`DriverModuleManager`、`DriverModuleFactoryCenter`、`DeviceManager` 都有静态表；后续按 MEM 工程资源身份、专业 Provider 和 Host 实例作用域收敛。已有 `MOCCapabilityCatalog` 是唯一能力发现目录。
6. **命令与数据质量**：`ModbusTcpDriver` 对 ACK、接收关联和轮询的成熟细节不能丢，直接重构为具体驱动；`VariableInfo` 的时间戳/初始状态应继承，后续补设备质量码。传输成功不得冒充设备命令完成。
7. **发现一处应回归审查的旧实现**：`DriverModuleBase.ReadPointValue<T>` 在读到 ACK 后更新变量，却从方法返回旧的 `defalutValue`；实际值返回行为需要加回归测试，而不是照搬。先记录，未修改 ReferTo。

## 5. GOF 与“不得额外套 Adapter”的判断

- **Composition**：Connector 组合通道、规约、点映射，不因没通道而创建假通道。
- **Strategy**：协议帧解析和具体厂商行为按接口选择直接实现，不再产生旧版 wrapper。
- **Factory Method**：待合并原 `DriverModuleFactoryCenter` 和项目统一资源/Provider 解析后，只保留一处工厂装配职责。
- **Template Method**：确有共享生命周期/资源管理行为时，可在各 Owner 添加精简基类；不强制所有计算函数继承统一大基类。
- **Observer**：变量变化通知直接归统一表/发布机制，不能创建另一套全局消息中心。
- **无第二 Adapter**：不出现 `LegacyChannelAdapter`、`OldVariableBridge`、`ProtocolCompatibilityWrapper` 等长期平行实现；跨 C++/厂商 SDK 的不可避免 P/Invoke/边界封装另行限定，不视为两套 C# 业务契约。

## 6. 审查结论待定

- [ ] 四种 Module 应仅有 `MFMModuleDescriptor` 而不持久化一棵新 Module 对象树？
- [ ] 变量长寿命 `MDSVariableTable` 与单次流程 Heap/Stack 的作用域划分是否合理？
- [ ] 标准 PointDefinition 直接属于 Connector，专业设备对象在 Automation/Vision？
- [ ] `IMCCChannel` 与参考 `IChannel` 如何择优重构，公开异步 API 与同步 API 是否应统一？
- [ ] 标准 Protocol 是否保留独立 Session，或委托 Driver 的请求上下文？
- [ ] Service 合同放在 `Mechanism.Lifecycle`，是否满足内部服务及独立进程服务？
- [ ] 通过本次审核后移除 Porting 中旧依赖，**选一个唯一正式实现，回归测试，再加入编译**。

**当前不包含设备 SDK、Socket 新实现、Modbus 正式集成、变量 Manager 数据迁移、Studio 或 HIL 验收。** 这些全部属于后续迁入正式实现的开发任务。