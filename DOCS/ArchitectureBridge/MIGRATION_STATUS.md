# CxVisual V6.4 架构收口迁移状态

更新时间：2026-09-20

本文记录 `CxVisual_V6.4_架构收口与公共底座迁移_Codex执行说明_V1.0.md` 的实际落地状态。代码仓库是事实源；设计文档用于约束目标边界。

| Phase | 状态 | 已落地内容 | 兼容策略 |
|---|---|---|---|
| 1 References / Types | 完成 | `StableReference`、资源引用、`IndustrialTypeSystem` 已物理归入 `Platform/References` 与 `Platform/Types` | 保持原 Namespace、序列化 ID 和调用签名 |
| 2 Orchestration Bridge | 完成 | 四执行模型、Execution Contract/Plan/Instance/Scope、Invocation、Compiler、Runtime Kernel、Legacy Graph Adapter | 旧 `GraphExecutionPlan/GraphRuntimeSession` 作为 NativePlan/Adapter 继续运行 |
| 3 Resource / Policy | 完成 | Resource Manager/Lease/Requirement 与 Retry/Fault/SideEffect 进入公共 Orchestration | 旧 Graph 名称在 Compatibility 中保留映射 |
| 4 Channel / Protocol | 完成 | `IChannel`、Endpoint/Health/Diagnostics、Protocol/Codec/Parser/Session；Modbus Provider 已走 TCP Channel 与公共帧编解码 | 原点表驱动保留；新 Provider 链已真实运行 |
| 5 Drawing / Diagram | 完成 | 公共 Viewport/Coordinate/Selection/HitTest/Transform/Adorner/Snap；`GraphCanvas`、Node/Port/Edge Presenter、ConnectionInteraction | `SceneView` 与旧 Graph Designer 外观不变，内部改走公共投影 |
| 6 Runtime 解耦 | 完成 | `ControlFlowRuntime`、`DataFlowRuntime`、`WorkflowRuntime`、`StateMachineRuntime` 由 `GraphExecutionModelKernel` 唯一选择 | `GraphRuntimeSession` 保留生命周期/调试外壳，旧 Case 行为不变 |
| 7 专业污染清理 | 完成（兼容保留） | 删除 Inspection 内无人引用的 MFL 协议副本；Runtime Host 改由 HMI Workbench Factory 贡献 Visual Runtime 组合；平台无 Workbench 反向引用 | `CxVisualRuntime` 及 Visual Runtime 旧公共类型作为 ABI 兼容壳保留，不再作为新 Host 的组合入口 |

## 当前边界

- Platform：身份、类型、变量、数据句柄、执行、编排、资源、策略、通道、协议、持久化与公共呈现机制。
- Automation：ICS/SCADA、Motion、Robot、生产运行组合。
- HMI：HMI 资源语义与 Visual Runtime 的 Workbench 组合入口。
- Vision / CT3D / Inspection / Simulation：各自保留专业 Compiler、Runtime、Worker 与 UI。
- Host：只做 Composition Root，不实现协议、Graph 模型或专业 Runtime。

## 验收结果

- 解决方案 16/16 项目构建成功，0 warning / 0 error。
- 基础回归 213/213。
- Architecture 7/7、Phase2 21/21、Phase3 14/14。
- Modbus TCP 真实 Socket、SCADA 读写回读、断线重连、Motion/Robot、Vision/CT、Inspection Durable、六 Workbench 页面与 Simulation 场景均保持通过。

## 仍保留但不得新增依赖的兼容面

- `CxVisual.Runtime.Core.CxVisualRuntime`
- `CxVisual.Runtime.Core.VisualRuntimeSession` 中的旧 Visual 聚合 API
- `CxVisual.Graph.Core.GraphRetryPolicy/GraphFault/GraphSideEffectKind`
- 旧 `TcpClientBase/TcpClientChannel`

这些类型的删除需要独立的主版本迁移与工程格式升级，不属于本轮保持旧工程可运行的收口任务。
