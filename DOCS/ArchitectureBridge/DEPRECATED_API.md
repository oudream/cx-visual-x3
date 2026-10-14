# 兼容 API 与淘汰路线

下列 API 仍可用，但新代码不得继续扩展。为避免仓库启用“警告即错误”后阻断旧调用，本表代替对全部类型立即添加 `[Obsolete]`。

| 兼容 API | 新代码入口 | 删除前置条件 |
|---|---|---|
| `CxVisualRuntime` | `RuntimePlatformHost` + Workbench `IDomainRuntimeFactory` | Runtime Host、Studio Preview、第三方 Host 全部迁移并发布主版本 |
| `GraphRetryPolicy` | `CxVisual.Platform.Orchestration.Policies.RetryPolicy` | 旧插件重新编译 |
| `GraphFault/GraphFaultKind` | `ExecutionFault/ExecutionFaultKind` | 旧 Graph 诊断格式完成迁移 |
| `GraphSideEffectKind` | 公共 `SideEffectKind` | 旧节点描述器完成迁移 |
| `TcpClientBase/TcpClientChannel` | `IChannel/TcpClientTransportChannel` | 旧点表驱动完成 Adapter 化 |
| 直接 `GraphRuntimeSession` | `ExecutionRuntimeKernel` + `LegacyGraphRuntimeAdapter` | 调试、断点和热更新入口全部接到公共 Kernel |

## 已删除冗余

- `Workbenches/Inspection/External/MFLProtocol/*`：无人引用且与 `Platform/Protocols/MFLModule` 重复。统一使用平台协议实现。

## 禁止重新引入

- Workbench 内复制 Channel/Protocol 源码。
- Host 或 Workbench 直接 `new TcpClient` 实现工业协议。
- Platform 引用 Automation/HMI/Vision/CT3D/Inspection 实现程序集。
- 新建第二套 Reference、TypeSystem、Graph Compiler 或万能 Runtime。
