# ArchitectureSmoke统一测试项目归档与兼容入口 V1.0

本次以 `Tests/ArchitectureSmoke` 为**唯一综合Smoke测试项目**，原`FoundationSmoke`及`ConfigFoundationSmoke`的测试源码完整并入，不新建项目，`FoundationWpf`与`TestCenter`保持不变。

| 目录 | 测试范围 |
|---|---|
| `Foundation/Concurrency` | FCR Worker、调度、独占租约、DedicatedExecutor |
| `Foundation/Logging` | FLD日志、监测、同步日志 |
| `Configuration` | Foundation/Mechanism.Data配置、开发者体验、独立性能 |
| `Engineering/Organization` | MEM H01-H06/反证 |
| `Engineering/Studio` | WPF/Studio 工程交互 |
| `Engineering/Integration` | MEM/逻辑联动 |
| `Engineering/Performance` | 容量/性能 |
| `Visual/Authoring|Editing|Runtime` | 图元与投影 |
| `Orchestration/Compilation` | 流程编译 |
| `Runtime/Execution|Data` | MRK / MDS |
| `Integration/Vision` | Vision/运行集成 |

原有测试断言全部保留；为了测试兼容，原`CxVisual.Tests.FoundationSmoke`和`CxVisual.Tests.ConfigFoundationSmoke`**测试命名空间不变**，只是物理位置与编译目标合一。

## 兼容入口

- 默认全部架构与Foundation：`CxVisual.Tests.ArchitectureSmoke.exe`
- 原F19专项：`CxVisual.Tests.ArchitectureSmoke.exe --concurrency`
- 独立Foundation全量：`CxVisual.Tests.ArchitectureSmoke.exe --foundation`
- 配置核心：`CxVisual.Tests.ArchitectureSmoke.exe --config-foundation`
- 配置性能：`CxVisual.Tests.ArchitectureSmoke.exe --config-performance-smoke/full`
- 原`--organization`、`--organization-wpf`、`--organization-benchmark/stress`及`--config-wpf`继续支持。
- 只编译配置Owner和测试（**不依赖完整Architecture程序集**）：`Build/Build-ConfigIsolation.ps1 -Configuration Release`，产出临时 `Output/Release/x64/CxVisual.ConfigIsolation.exe`。其源文件与Architecture项目使用同一份Configuration代码，不创建第二个正式项目。

工作流中所有旧Exe/项目引用应以新入口代替。统一解决方案由31正式csproj降为29，`CxVisual.Foundation.sln`仍只收录Foundation生产项目。**完整编译/Windows CI必须在对应提交运行后才能宣称通过**。
