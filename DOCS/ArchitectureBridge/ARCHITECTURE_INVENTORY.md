# CxVisual 架构现状清单

更新时间：2026-09-20  
起始基线：`master` / `5223097`（仓库已是 V6.5 演进态，不是待从零迁移的 V6.4）

## 1. 解决方案与依赖方向

解决方案共有 16 个项目：

- 基础：`Platform/Platform.csproj`、`Platform.Presentation.Wpf/Presentation.Wpf.csproj`、`Platform.WinFormsHost/WinFormsHost.csproj`
- Workbench：Automation、Simulation、HMI、Vision、CT3D、Inspection、Presentation.Wpf
- Host：Studio、Runtime.Host、Station.Agent、Operations
- 验证：Tests、UiSmoke

当前主依赖方向为：

```text
Platform
  ↑
Platform.Presentation.Wpf / 六类 Workbench
  ↑
Host.Studio / Host.Runtime
  ↑
Tests / UiSmoke
```

`Platform` 没有反向项目引用专业 Workbench。`Workbenches/Presentation.Wpf` 是六类专业 UI 的组合层，会引用各 Workbench；它不是公共 Platform UI。

## 2. 关键能力分布

| 能力 | 当前主位置 | 现状判断 |
|---|---|---|
| Stable Reference | `Platform/References/StableReference.cs` | 已物理升格并保持原 Namespace/序列化行为 |
| Resource/Object/Property Ref | `Platform/References/ResourceReferences.cs` | 已物理升格；旧调用签名保持兼容 |
| Variable Ref/Identity | `Platform/Variables`、`Platform/VariableObjects`、`Platform/Core/Abstractions/Variables.cs` | 已具备通用变量与旧设备变量适配 |
| Type/Schema | `Platform/Types/IndustrialTypeSystem.cs` | `IndustrialTypeSystem` 是唯一基础类型源 |
| Graph 类型适配 | `Platform/Graph/Core/GraphModels.cs` | `GraphTypeSystem` 应继续作为适配层，不另建类型系统 |
| 调用上下文 | `RuntimeContext`、`ContextScope`、`CorrelationContext` | 已覆盖执行标识、父子关系、Deadline、Correlation |
| 旧跨域调用 | `DomainInvocationRequest`、`DomainInvocationRouter`、`CrossDomainCallPlan` | 行为成熟，需通过公共编排桥接收口 |
| Graph 编译 | `Platform/Graph/Runtime/GraphCompiler.cs` | Definition/Compile 已分离，计划 Hash 稳定 |
| Graph 运行 | `Platform/Graph/Runtime/GraphRuntimeSession.cs`、`GraphExecutionModelRuntimes.cs` | 会话负责生命周期/调试/节点执行；四模型 Runtime 负责各自调度语义 |
| 通用异步/条件/周期 | `Platform/Execution` | 已覆盖取消、Deadline、条件等待、周期调度与看门狗 |
| Graph 资源与策略 | `Platform/Orchestration/Resources`、`Platform/Orchestration/Policies` | 公共语义已抽离，Graph 旧名通过 Compatibility 映射 |
| Channel | `Platform/Channels/Contracts`、`Platform/Channels/Transport` | 已具备 `IChannel`、端点、状态、健康、诊断及 TCP 实现 |
| Protocol | `Platform/Protocols/Contracts` 与具体 Modbus/S7/MFL 模块 | 公共 Codec/Parser/Session 契约已建立；具体协议按 Provider 边界隔离，暂不拆 DLL |
| Runtime 数据服务 | `Platform/Runtime` | Variable、Binding、Alarm、Historian、Recipe、Action 等已形成闭环 |
| 公共 Presentation | `Platform.Presentation.Wpf/Drawing`、`Diagram`、`Viewer` | Drawing/Diagram 公共投影已被 HMI SceneView 与 Graph Designer 复用 |
| 专业 UI/Runtime | `Workbenches/*` | 六类页面可创建，Automation 已覆盖 SCADA/Motion/ICS/Robot 等场景 |
| Security/Audit | `Platform/Runtime/Integration/SecurityServices.cs` 及写入/配方审计链 | 已有真实场景能力，尚未形成完整独立 Foundation |
| Persistence/Deploy | `Platform/Persistence`、`Platform/Deploy`、`Host/Station.Agent` | 版本、发布、回滚、远程 Agent 已有门禁覆盖 |

## 3. 正式编排模型结论

正式一级模型冻结为：

```text
ControlFlow / DataFlow / Workflow / StateMachine
```

- `EventDriven` 是旧 GraphKind，暂不升格为第五种正式模型。
- Event/Reactive 进入 Trigger、Subscription、Debounce、Throttle、Window 等策略面。
- Streaming 是 DataFlow 的 Profile（Buffer/Backpressure），不是新一级模型。

`Platform/Orchestration` 已建立公共契约和旧运行时适配，不复制现有 `GraphCompiler`、`GraphRuntimeSession`、`DomainInvocationRouter`。Graph 会话内部再由唯一 `GraphExecutionModelKernel` 选择四类调度模型。

## 4. 验证基线

- 解决方案：16 项目编译成功，0 warning / 0 error。
- 基础测试：改造前 205/205；Phase 1—7 完成后 213/213。
- Architecture 门禁：7/7。
- Phase2 门禁：21/21。
- Phase3 门禁：14/14。
- 已有真实场景门禁包括：Modbus TCP SCADA 读写回读与审计、断线重连、Motion 并行程序、安全资源、Inspection 跨域链、六 Workbench 页面创建与 Simulation 运行。

## 5. 兼容边界

本轮未执行破坏性 Namespace 改名、按目录拆 DLL、重写 Graph Runtime。旧入口只作为 ABI/工程兼容面保留；新 Host 已改从 HMI Workbench Factory 组合 Visual Runtime，Inspection 内重复且无人引用的 MFL 副本已删除。

## 6. Phase 2 目标反向验收

| 目标 | 状态 | 证据 |
|---|---|---|
| `IExecutionUnit / ExecutionContract` | 已达到 | 通用身份、模型、输入输出和能力契约 |
| `InvocationSpec / ExecutionPolicySet / MultiInstanceDefinition` | 已达到定义态 | 四种调用模式及完整策略维度；未实现模式会明确拒绝 |
| `IExecutionModelCompiler / ExecutionCompiler` | 已达到 | 公共协调器通过 `LegacyGraphCompilerAdapter` 使用原 GraphCompiler |
| `ExecutionPlan` | 已达到 | 包裹原计划并保持 StableReference、版本和 PlanHash |
| `ExecutionInstance` | 已达到 | 只记录运行状态、时间、上下文和结果，不执行专业语义 |
| `ExecutionScope` | 已达到 | 形成父子树，组合原取消/Timeout/Deadline 实现 |
| `ResourceScope` | 已达到桥接态 | 可继承和传播资源身份；租约实现仍由旧 ResourceManager 管理 |
| `ExecutionRuntimeKernel` | 已达到桥接态 | 只选择 Adapter、建立作用域和记录状态 |
| `InvocationCoordinator` | 已达到 CallAndWait 闭环 | 输入/输出映射、Retry、总超时、Deadline、Correlation 均有测试 |
| 旧 Graph Case 经 Legacy Adapter 运行 | 已达到 | 公共编译→公共计划→Kernel→旧 GraphRuntimeSession 实际产生输出 |

结论：**执行说明中的 Phase 0—7 已按兼容迁移方式落地并通过门禁。** `Spawn/FireAndForget/MultiInstance` 仍只有定义契约，尚未作为生产调度能力开放；删除旧 ABI 与拆分具体 Driver DLL 需要独立主版本迁移，不在本轮伪装成已完成能力。
