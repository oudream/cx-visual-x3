# CxVisual F17 Channel 迁移——源码与 Windows 验收台账 V1.0

> 2026-10-27；迁移基线 `a46705c3`，最终 master SHA、CI 结论以后续真实运行记录为准。不把尚未运行的测试写成成功。

## 1. 本轮源码变化

- `Foundation.Transport/Channels` 按指定 **56 个 FTR/IFTR 主类型文件**落位；旧 `Mechanism.Connectivity/Channels` 的 **27 个正式编译源文件**移出。
- Channel 和 FCM 配置共用唯一程序集；Mechanism 保留 Connector/Protocol，工程引用从 M→M.Core/F.Data 及 M→F.Transport 组合；没有 Foundation→Mechanism 反向依赖。
- 保留多对端 Generation、诊断有界证据、协议 Reset 通知、原字节格式、`MCCRPL01` Replay 魔数、旧 FCM Kind 迁移。
- 非编译历史 Porting 文件只进入 ReferTo。
- 核对测试修改：`ModuleChannelTransportSmokeTests`、`ModuleFoundationSmokeTests`、`ModuleConfigAnnotationSmokeTests`、`TCTModuleConfigurationCases`、`TCTChannelEvidenceCases`、TestCenter 人工 Channel。

## 2. 必须验证

| 检查项 | 方法 | 状态 |
|---|---|---|
| F→M 单向依赖 / 56 源文件唯一编译 | .csproj 编译项和依赖 DAG 核对 | 源码静态核查；Windows 需确认 |
| Foundation.Transport 单项目编译 | Windows x64 / .NET Framework 4.8 MSBuild | 待真实 CI |
| 完整解决方案编译 | Windows x64 MSBuild | 待真实 CI |
| 基础 Channel, Virtual, TCP/UDP | ArchitectureSmoke ModuleChannelTransport | 待真实 CI |
| NamedPipe / HTTP / WebSocket / Replay | ArchitectureSmoke + TestCenter | 待真实 CI |
| FCM Kind 子类、旧 JSON、拒绝无效配置 | ModuleConfigAnnotationSmoke + TestCenter | 待真实 CI |
| Connector+Modbus TCP 请求响应及数据映射 | Windows Socket Loopback | 待真实 CI |
| TestCenter 统一工作区和 WPF UIA | TestCenter Windows 工作流 | 待真实 CI |
| PLC/串口实机、跨 PC、WSS、组播、高负载 | HIL/安全/现场验收 | 未验证，不可据此宣称通过 |

## 3. CI 证据规则

提交后用其固定 SHA 的 `.github/workflows` 运行记录、Windows 构建/测试 stdout 和失败定位更新此台账；不得把历史 MCC CI 的绿色状态代替本轮 FTR 成功。


## 4. 固定提交 Windows CI 实际结果（2026-10-27）

**源码提交：** `91dc7ce8092452f866bfa429902ac42767adff01`。该 SHA 的 10 条 push 工作流均已结束：**8 成功、2 失败**，不能宣称全部 Windows 验收通过。

| 工作流 | Run ID | 实际结论 |
|---|---|---|
| 配置公共底座独立编译与验收 | [40467329607](https://github.com/oudream/cx-visual-x3/actions/runs/40467329607) | success |
| MDS Variable System Windows Acceptance | [40467329539](https://github.com/oudream/cx-visual-x3/actions/runs/40467329539) | success |
| M36 Message UI Windows Acceptance | [40467329574](https://github.com/oudream/cx-visual-x3/actions/runs/40467329574) | success |
| Studio S1 Windows Shell Acceptance | [40467329548](https://github.com/oudream/cx-visual-x3/actions/runs/40467329548) | success |
| Command Workspace Windows Acceptance | [40467329572](https://github.com/oudream/cx-visual-x3/actions/runs/40467329572) | success |
| Unified Runtime Windows TDD | [40467329561](https://github.com/oudream/cx-visual-x3/actions/runs/40467329561) | success |
| Engineering Organization Windows Acceptance | [40467329566](https://github.com/oudream/cx-visual-x3/actions/runs/40467329566) | success |
| Config L1 Windows Build and Smoke | [40467329550](https://github.com/oudream/cx-visual-x3/actions/runs/40467329550) | success |
| TestCenter Windows .NET48 | [40467329665](https://github.com/oudream/cx-visual-x3/actions/runs/40467329665) | failure：Graphics WPF `TCTGraphicsFrameSmoke.Run` 的真实 Group 解组按钮未启用 |
| FIM MVI Imaging Windows Acceptance | [40467329560](https://github.com/oudream/cx-visual-x3/actions/runs/40467329560) | failure：ImageViewer `TCTImageViewerSmoke.Run` 的矩形工具激活反馈失败 |

### 4.1 与本次 Channel 迁移直接相关的已通过证据

- 所有工作流中的完整 `CxVisual.Unified.sln` Windows x64 构建步骤未因 FTR 类型/项目引用失败。
- Imaging 工作流的 ArchitectureSmoke 默认回归已明确输出 **`CHANNEL.TRANSPORT: TCP/UDP loopback MultiPeer and stale generation PASS`**；包含 FTR 程序集身份断言、Channel 行为、Connector/Modbus 真实本机 Socket 通路等既有代码路径。
- 配置独立工作流和 Config L1 已成功，证明 FCM 多态配置的编译及其既有测试在相应环境下没有因 Channel 迁移失败；不替代完整现场数据兼容验证。

### 4.2 失败归因和独立问题

- TestCenter Graphics 错误已在迁移前父提交 `ce626007` 的 [Windows TestCenter #40465563490](https://github.com/oudream/cx-visual-x3/actions/runs/40465563490) 同样出现，属于已存在的独立 WPF Group 操作缺陷；不在本次 F17 修改范围。
- ImageViewer 的失败位置在二维 WPF 矩形工具激活反馈，Channel 变更未编辑此工具的源码；**尚未建立独立的根因证明**，不宣称它已修复或属于 Channel 的通过项。
- 串口/RS-485 HIL、真实 PLC、跨机器 NativePipe/鉴权、WSS/组播网络、长时高压和外部二进制客户端重编译均未验证。

该 CI 状态针对 **`91dc7ce`** 源码提交，不自动代表任何后续改动的通过结论。
