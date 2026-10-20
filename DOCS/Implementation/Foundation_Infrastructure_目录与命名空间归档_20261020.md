# Foundation.Infrastructure 目录与命名空间整理（2026-10-20）

## 范围

本次为源文件归档、命名空间调整和调用方迁移；保持原 `CxVisual.Foundation.Infrastructure.csproj`、程序集名、TargetFramework v4.8、C# 7.3、工程 GUID、既有职责和公共方法签名。不新增项目、不改变并发原语、日志写入或系统采样的实现行为。

| 目录 | 完整命名空间 | 职责 |
| --- | --- | --- |
| `Concurrency/` | `CxVisual.Foundation.Infrastructure.Concurrency` | 原有并发/Worker（未迁移） |
| `Logging/` | `CxVisual.Foundation.Infrastructure.Logging` | 公共日志入口、服务、记录、级别、作用域 |
| `Logging/Sinks/` | `CxVisual.Foundation.Infrastructure.Logging.Sinks` | 文件、内存、滚动日志输出 |
| `Logging/Dedicated/` | `CxVisual.Foundation.Infrastructure.Logging.Dedicated` | 独立同步专有日志 |
| `Monitoring/` | `CxVisual.Foundation.Infrastructure.Monitoring` | 系统、进程采样和快照 |

16 个现有源文件移动到相应目录，项目 `Compile Include` 同步更新；Studio、Runtime.Host、WPF 控件、FoundationSmoke、FoundationWpf、TestCenter 的 `using` 同步迁移。FoundationSmoke 加入命名空间反证，避免以后文件位置/Owner 漂移。

## 向后兼容与迁移

- 二进制程序集名称仍为 `CxVisual.Foundation.Infrastructure.dll`，但**公开类型的 CLR 全限定名变更**；这是显式的源代码/API 与二进制兼容性变更。
- 下游调用方须添加对应 `using`（或修改完全限定名），重新编译；旧 DLL 编译的外部客户端不能只替换 DLL，应更新并重新发布，必要时采取独立兼容方案。
- `FLDLogLevel` 位于 Logging；同步 `FLDImportantEventLogEntry` 位于 Logging.Dedicated；`FLDRollingFileLogSink` 位于 Logging.Sinks。
- CxVisual 既有 `Concurrency` 命名空间保持不变。

## 验证

- 以此次提交 SHA 对应的 Windows x64 Actions 验证真实编译与 FoundationSmoke / ArchitectureSmoke / WPF / TestCenter。
- 建议执行 `Build/Verify-Structure.ps1`、`Build/Verify-OwnerDependencies.ps1`、`Output/Release/x64/CxVisual.Tests.FoundationSmoke.exe`，并复核现有公共/专有日志及系统监控正常与异常用例。
- 若本次环境未能运行 Windows/MSBuild，不得把静态文件验证写为已通过 Windows 实测。
