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
