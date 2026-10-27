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


## 5. F17 Connector 全类型收尾与独立专项验收（2026-10-27）

**实施源码：**

- `4e8b889`：`MCNConnectorModule` 的类型匹配覆盖全部 **12 种有内置 Provider 的 FTR Channel**；`Custom` 未注册 Provider 继续拒绝。
- `4f40ec7`、`28eb984`：`ConnectorAllChannelKinds` 测试覆盖 12 种配置/CLR 实例一致性、装配不打开设备、类型不符、LengthFrame 与消息类 I/O 能力不兼容、Custom 未注册拒绝。原有 FTR TCP/UDP/HTTP/NamedPipe/WebSocket/Replay 基础回归保留。
- `91263754`：通用 WPF 配置编辑器从旧 `MCC*` 字符串类型识别改为 `FTRChannelConfiguration` 类型契约识别，恢复具体子类 Schema 和 Channel 三栏编辑器。
- `67a4048`、`ea5ae65`：新增 `ArchitectureSmoke.exe --channel-transport` 独立入口及 F17 Windows 专项工作流。独立门禁检查 56 个源码文件、全解决方案构建与实际 Channel/Connector 回归。

**对应真实 Windows CI：**

| 运行 | SHA | 结论 | 验证范围 |
|---|---|---|---|
| [F17 Channel Windows Acceptance #40477369044](https://github.com/oudream/cx-visual-x3/actions/runs/40477369044) | `ea5ae65d309b1107b67792a545ce795cb6a2dc7c` | **success** | Windows x64 完整解决方案 Rebuild、独立 FTR/Connector 测试及 56 文件唯一 Owner 检查 |
| [TestCenter Windows .NET48 #40476788779](https://github.com/oudream/cx-visual-x3/actions/runs/40476788779) | `91263754c212a09a33111f167e35f27d108a0563` | **success** | Connector FCM Schema 三栏、PointTable/DataMapping UIA 与统一测试平台其他回归 |
| [Graphics Editor Windows Acceptance #40476788786](https://github.com/oudream/cx-visual-x3/actions/runs/40476788786) | `91263754c212a09a33111f167e35f27d108a0563` | **success** | 真实 Graphics Group/Ungroup、Undo/Redo 操作及 WPF 截图 |

上述结果覆盖本阶段**软件源码迁移、12 类内置装配、配置 UI、基础回归与 Windows 验收**，不证明 `Custom` 厂商 Provider 已注册、所有 12 类均经过现场 HIL、跨 PC/工业网络安全、长期高压或旧外部二进制客户端兼容。

**02～04 Excel 文档限制**：原始三个 `.xlsx` 为版本化大型二进制规格，本轮已在正式 `DOCS/CxVisual_统一工程项目代码与资源规范_V1.0.md`、`DOCS/Decisions/CxVisual_F17_Channel下沉与唯一归属裁决_V1.0.md`、`DOCS/00` 记录新 Owner 覆盖关系，但**没有修改那三个原始 Excel 二进制文件**。不将规范映射说明误报为 Excel 工作表已完成更新。

**固定版本原则**：以上仅说明对应源码 SHA 的实测结果；其他并行提交要看自己的 CI，不自动沿用成功结论。
