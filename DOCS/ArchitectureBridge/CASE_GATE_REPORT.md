# Case Gate 报告

更新时间：2026-09-20

## 构建

- `dotnet build .\CxVisualX2.sln -v minimal`
- 16/16 项目成功
- 0 warning / 0 error

## UI Smoke

- WPF Studio 视图已真实创建并完成布局/渲染，输出 `artifacts/ui/phase7-designer.png`（1672×941）。
- 六类 Workbench 工程树、固定/上下文 Ribbon、画布、属性/变量/输出面板与关键命令入口均通过断言。
- 23 个标准运行控件已创建并渲染到 `artifacts/ui/standard-controls-runtime.png`（1100×850）。
- `Runtime.Host` 已完成启动级 Smoke：主窗口资源可加载，HMI Workbench Factory 可创建并启动 Visual Runtime；验证后测试进程已关闭。

## 自动化门禁

| 组 | 结果 | 覆盖重点 |
|---|---:|---|
| 基础回归 | 213/213 | HMI、变量、绑定、Alarm、Historian、Recipe、Graph、Motion、Vision、CT、SCADA、Modbus、持久化 |
| Architecture | 7/7 | Core 技术中立、Runtime UI 中立、无领域反向依赖、兼容外观保持薄层 |
| Phase2 | 21/21 | Graph 编译/运行/调试、资源、Motion/Robot、Vision/CT、跨域调用 |
| Phase3 | 14/14 | Inspection Durable、Checkpoint、MES 幂等、恢复、部署与远程治理 |

## 本轮关键真实场景

- Modbus TCP：真实 Socket、FC02/03/04/05/06/16、SCADA 写后回读、Audit。
- 故障恢复：断线后 Quality=Disconnected，恢复后自动重连并读取最新值。
- Orchestration：Sequence/Branch/Retry/Timeout/Cancellation/Resource Scope。
- Graph：ControlFlow、DataFlow、Workflow、StateMachine 四模型由唯一 Kernel 选择；旧 ControlFlow、TypedDataFlow、Durable Workflow Case 全部通过。
- Channel / Protocol：公共端点与帧契约、Modbus 编解码及真实 TCP Provider 链均有门禁。
- Drawing/Diagram：HMI SceneView 回归与 Graph Designer 绘制/保存/重开通过。
- 专业边界：Motion、Robot、Vision、CT、Inspection 各自 Runtime Case 保持通过。

## 已知测试宿主事项

当前自动化终端的 x64 .NET Framework CLR/JIT 路径会在同一组已通过 PEVerify 的程序集上随机报告不同 BCL/业务方法缺失；故障位置每次漂移，强制 x86 CLR 后完整门禁稳定通过。该现象已登记为宿主环境风险，不能归因为某个业务方法，也不能据此宣称生产 x64 已验证。标准 `dotnet clean` 需要读取本机 Windows SDK 用户目录，在受限沙箱中需提升权限。
