# CxVisual 架构迁移映射

> 2026-09-20：Phase 1-7 已按小步门禁完成。逐阶段结果见 `MIGRATION_STATUS.md`，以下表格保留最初映射依据并补充最终状态。

分类定义：

- **直接升格**：语义正确，进入公共命名与契约。
- **搬迁适配**：实现可用，通过 Adapter 接入新边界。
- **重构复用**：保留核心实现，抽取公共策略或接口。
- **必须重写**：语义错误且无法兼容；第一轮没有发现需要立即重写的核心链。

| 现有对象/目录 | 目标边界 | 分类 | 当前动作/状态 |
|---|---|---|---|
| `StableReference` | References Foundation | 直接升格 | 已迁入 `Platform/References`，Namespace 与序列化行为兼容 |
| `ResourceRef/ObjectRef/PropertyRef` | References Foundation | 搬迁适配 | 已迁入 `Platform/References`，保留原 Namespace |
| `IndustrialTypeSystem` | Types Foundation | 直接升格 | 已迁入 `Platform/Types`，保持唯一类型源 |
| `GraphTypeSystem` | Types Adapter | 重构复用 | 继续桥接 Graph 端口类型，不并立第二套系统 |
| `DomainInvocationRequest` | `InvocationSpec + ExecutionStartRequest` | 搬迁适配 | 已增加双层契约映射，保留 Router |
| `CrossDomainCallPlan` | `InvocationSpec` | 搬迁适配 | 已映射 Target/Input/Output/Timeout/Retry/Failure/Compensation/Resource/Trace |
| `RuntimeContext` | Execution Context | 直接升格 | 复用执行 ID、父 ID、Deadline、Manifest、Correlation |
| `ContextScope` | Execution Scope Context | 搬迁适配 | 暂不改名，避免破坏存量调用 |
| `GraphExecutionPlan` | 公共 `ExecutionPlan` 外壳 | 搬迁适配 | 已保留 NativePlan 与原 PlanHash |
| `GraphRuntimeSession` | `IRuntimeAdapter` | 搬迁适配 | 已由 Adapter 驱动；内部调度拆为四个执行模型 Runtime |
| `GraphCompiler` | `IExecutionModelCompiler` | 搬迁适配 | 公共 `ExecutionCompiler` 通过 Adapter 选择原编译器 |
| `IDomainRuntimeSession` | Runtime Adapter 能力源 | 重构复用 | 先适配，暂不删除旧接口 |
| `GraphRetryPolicy` | `RetryPolicy` | 搬迁适配 | 公共策略已成为运行来源；旧名称只在 Compatibility 保留 |
| `CrossDomainFailurePolicy` | `InvocationFailureMode` | 搬迁适配 | 已完成四种失败语义映射 |
| `ResourceManager/ResourceLease` | Resource Scope + Lease Manager | 重构复用 | 已进入 `Orchestration/Resources` 并保持原行为 |
| `GraphFault` | 公共 Execution Fault | 重构复用 | 已进入 `Orchestration/Policies`，旧名兼容 |
| `Platform/Execution/ExecutionScope` | 超时/取消执行边界 | 直接升格 | 已被 Router/Graph/Action 真实使用；不要与层级实例对象混为一谈 |
| `Platform/Channels` 具体实现 | Channel Foundation + Provider | 重构复用 | 已补公共契约与异步 TCP Channel；旧 Channel 保留 |
| `ProtocolDriverContracts` | Protocol Foundation | 直接升格 | 已补 Protocol/Codec/FrameParser/Session 并接入 Modbus |
| Modbus/S7/MFL | Driver/Provider Plugin 语义 | 搬迁适配 | 依用户约束仍归现有 Platform 项目，不新建独立项目 |
| `Platform.Presentation.Wpf` | Presentation Foundation | 直接升格 | 保持公共 Viewer/Renderer/Workspace |
| 六类 Workbench UI/Runtime | 专业能力层 | 保留 | 不迁入 Platform，不互相 new Runtime |
| `SecurityServices` 与审计链 | Security/Audit Foundation | 重构复用 | 后续按 Identity/Permission/Interlock/Audit 拆薄契约 |

## 拟新增/演进目录

```text
Platform/Orchestration/
  Core/
  Invocation/
  Compatibility/
  Compiler/       # 后续，仅放公共编译协调，不复制 GraphCompiler
  Runtime/        # 后续，仅放 Kernel/Instance/Adapter registry
  Resources/      # 后续，从 Graph 资源实现抽公共契约
  Policies/       # 后续，从 Graph 策略提纯
  Persistence/    # 后续，编排实例检查点与恢复契约
  Diagnostics/    # 后续，统一 Trace/Fault/Health 视图
```

## 后续顺序

1. 抽 Resource/Policy 公共契约，旧 Graph 类型通过 Adapter 保持兼容。
2. 用既有 SCADA→Motion、Inspection→Vision/CT Case 把 `InvocationCoordinator` 接入真实组合根。
3. 在持久化、父关闭和容量治理完成后，再开放 Spawn/MultiInstance 运行模式。
4. 提纯 Channel/Protocol 接口；具体协议实现是否物理迁移，服从“不独立项目”的仓库约束。
5. 最后才逐步淘汰旧入口；每步保持全量门禁通过。
