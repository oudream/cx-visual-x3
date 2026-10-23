# CxVisual 统一功能模块体系——四模块主体架构实施与审查 V0.2

> 2026-10-23；依据《CxVisual_统一功能模块体系——完整设计与验收_V1.1.md》。
> 目标 .NET Framework 4.8 / C# 7.3；交付范围仅为框架 G0/G1。正式编译结果以新提交 Windows CI 为准。

## 1. 主体、Owner、范围

| 主体 | 唯一 Owner | 已增构内容 | 明确未实现 |
|---|---|---|---|
| 模块公共 | Mechanism.Core.Modules | MFMModuleComposition、MFMComponentReference、可选类型化 ComputeOperation；复用已有 MFMModuleKind/Descriptor | 额外工程树、全局 ModuleManager |
| Channel | Mechanism.Connectivity.Channels | I/O 行为、三种拓扑、子通道带 Generation 身份、线程安全目录、TCP/UDP/Serial 强类型配置、诊断/重连策略 | 生产 TCP/UDP/Serial 真实传输重构 |
| Protocol | Mechanism.Connectivity.Protocols | 七类能力与可选接口、类型描述、通道/子通道绑定、会话、处理策略、操作结果、路由与诊断 | Modbus 实际收发、F19 分发接线 |
| Connector | Mechanism.Connectivity.Connectors | 模块组件配置、点表、绑定、跨配置引用校验、I/O 兼容性验证 | 真正设备实例装配及真实 Driver |
| Variables | Mechanism.Data.Variables | 按 scope 变量表、类型约束、时间和初始化状态、事件、实例作用域注册、可变字节数组防别名 | 完整 MHS 版本化、专业质量码、复杂数据 Adapter |
| Compute | Professional.Vision + MFM 可选函数 | 专业执行资源意图配置（CPU/GPU、并发、内存、超时） | 额外新算法与独立调度器 |
| Simulation | Professional.Simulation | 仿真定义、seed、固定步长、时钟、模型显式推进及重置 | 仿真线程、真实模型库、回放平台 |
| Service | Mechanism.Lifecycle | 服务选项、互斥 Start/Stop、明确 Ready/Faulted、必须覆盖的真实生命周期回调 | HTTP/IPC 服务具体实现与生产治理 |

## 2. 统一设计决议

1. Channel 自主管理 I/O/连接/子通道，但不要求独占线程。默认异步 I/O，特殊硬件亲和直接复用 F19 Dedicated。
2. Topology 为 SinglePeer、MultiPeer、SharedBus；ChannelId + SubChannelId + Generation 区分运行路由；规约站号不是 SubChannel。
3. Protocol 是解释、读、写、收发、交互、会话、路由和诊断的完整规约，不只是 Codec。
4. Protocol 执行配置含 Inline、Serial、KeyedSerial、Parallel、Dedicated、Runtime；当前是受校验的配置声明，真正调度需要挂已有 F19/MRK，不另建队列执行器。
5. Module/Channel/Protocol/Point/Variable 配置与运行态分离，但可视/工程保存仍由 MEM/MHS 唯一 Owner 管理；本轮的配置类只是组成与校验视图，不建第二棵工程树。
6. PointTable 可选，只管理外部地址和绑定，不储存第二份 MDS 变量值。
7. 继续将 ReferTo 作为只读成熟 C# 来源；Porting 仍只是审查源码，不参加当前编译。禁止增加长期 Legacy Adapter。

## 3. 正反测试与客观完成定义

已增加 ArchitectureSmoke 测试源码，覆盖：模块组件去重、通道类型/参数/拓扑校验、子通道代次防过期路由、复制防别名、Protocol 必需 I/O 验证、无通道 Protocol、悬空 Channel/Point、Session 状态、Sent 不冒充完成、变量类型和作用域及初始化、计算配置、仿真计时、Service 状态转换。

工程验证：原有六个 csproj 加入对应的源码，ArchitectureSmoke 增加 Connectivity、Simulation 项目引用；不增加新项目。提交后逐个核验 Windows CI，不把源文件存在当作编译成功。

## 4. 后续直接成熟源码迁移范围

- Channel：从 ReferTo 对照同步 Thread 与异步 Task 版 TCP、UDP，保留成熟连接/重连行为，直接融合正式 MCCTcp 等类型，移除旧重复合同；继续验证 Virtual 不回退。
- Protocol：以 ReferTo Modbus TCP 为第一种正式实现，整合 Codec/Parser/Session/Driver，保留 ACK、事务 ID、站号、错误与线程语义，不额外套兼容 Adapter。
- Data：完善长期变量生产者更新权限、复杂类型值拷贝与 MDS/MHS 资源接线，重构并逐步移除 Porting。
- Studio/TestCenter：直接调用 Production Owner 的模块创建、通道类型与子通道选择、规约绑定、点表校验，不做模拟型 ModuleManager。

## 5. 当前状态红线

完成：架构主体类与非假实现的配置/状态/数据管理、源代码级正反测试。
待 CI：本提交实际 x64 构建与 ArchitectureSmoke；结果以后续真实工作流输出为准。
未完成：真实 TCP/UDP/Modbus/设备、正式消息/命令体系、Studio/TestCenter 可视页、实机 HIL 与长期运行验收。
