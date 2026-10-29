# V1.1 目录命名调整与R2技能包导入状态

## 原则

- 磁盘目录：`Foundation/Core`、`Mechanism/Visual`、`Professional/Automation` 等，**不在子目录重复CxVisual.*全限定名前缀**。
- 项目文件名、Visual Studio项目名、AssemblyName、RootNamespace和项目GUID均沿用原全限定名称；不得因磁盘目录缩短而改变二进制和命名空间。
- `Intelligence/Core` 是当前单独 `CxVisual.Intelligence` 项目的物理目录。
- 所有 `.sln`、`.csproj` 的ProjectReference路径已迁移，构建属性仍统一导入 `Build/CxVisual.Build.props`。
- `.agents` 仅预留入口；**R2包原始内容尚未导入**，获取R2原始ZIP后再合并，不得视为完成。

## 全部项目目录

| 方面 | 目录 | 项目文件 | AssemblyName |
|---|---|---|---|
| Foundation | `Foundation/Core` | `CxVisual.Foundation.Core.csproj` | `CxVisual.Foundation.Core` |
| Foundation | `Foundation/Data` | `CxVisual.Foundation.Data.csproj` | `CxVisual.Foundation.Data` |
| Foundation | `Foundation/Geometry` | `CxVisual.Foundation.Geometry.csproj` | `CxVisual.Foundation.Geometry` |
| Foundation | `Foundation/Imaging` | `CxVisual.Foundation.Imaging.csproj` | `CxVisual.Foundation.Imaging` |
| Foundation | `Foundation/Infrastructure` | `CxVisual.Foundation.Infrastructure.csproj` | `CxVisual.Foundation.Infrastructure` |
| Foundation | `Foundation/Numerics` | `CxVisual.Foundation.Numerics.csproj` | `CxVisual.Foundation.Numerics` |
| Foundation | `Foundation/Transport` | `CxVisual.Foundation.Transport.csproj` | `CxVisual.Foundation.Transport` |
| Foundation | `Foundation/Volume` | `CxVisual.Foundation.Volume.csproj` | `CxVisual.Foundation.Volume` |
| Mechanism | `Mechanism/Connectivity` | `CxVisual.Mechanism.Connectivity.csproj` | `CxVisual.Mechanism.Connectivity` |
| Mechanism | `Mechanism/Core` | `CxVisual.Mechanism.Core.csproj` | `CxVisual.Mechanism.Core` |
| Mechanism | `Mechanism/Data` | `CxVisual.Mechanism.Data.csproj` | `CxVisual.Mechanism.Data` |
| Mechanism | `Mechanism/Logic` | `CxVisual.Mechanism.Logic.csproj` | `CxVisual.Mechanism.Logic` |
| Mechanism | `Mechanism/Orchestration` | `CxVisual.Mechanism.Orchestration.csproj` | `CxVisual.Mechanism.Orchestration` |
| Mechanism | `Mechanism/Presentation.Wpf` | `CxVisual.Mechanism.Presentation.Wpf.csproj` | `CxVisual.Mechanism.Presentation.Wpf` |
| Mechanism | `Mechanism/Runtime` | `CxVisual.Mechanism.Runtime.csproj` | `CxVisual.Mechanism.Runtime` |
| Mechanism | `Mechanism/Visual` | `CxVisual.Mechanism.Visual.csproj` | `CxVisual.Mechanism.Visual` |
| Professional | `Professional/Automation` | `CxVisual.Professional.Automation.csproj` | `CxVisual.Professional.Automation` |
| Professional | `Professional/CT3D` | `CxVisual.Professional.CT3D.csproj` | `CxVisual.Professional.CT3D` |
| Professional | `Professional/HMI` | `CxVisual.Professional.HMI.csproj` | `CxVisual.Professional.HMI` |
| Professional | `Professional/Inspection` | `CxVisual.Professional.Inspection.csproj` | `CxVisual.Professional.Inspection` |
| Professional | `Professional/Simulation` | `CxVisual.Professional.Simulation.csproj` | `CxVisual.Professional.Simulation` |
| Professional | `Professional/Vision` | `CxVisual.Professional.Vision.csproj` | `CxVisual.Professional.Vision` |
| Applications | `Applications/Runtime.Host` | `CxVisual.Runtime.Host.csproj` | `CxVisual.Runtime.Host` |
| Applications | `Applications/Studio` | `CxVisual.Studio.csproj` | `CxVisual.Studio` |
| Intelligence | `Intelligence/Core` | `CxVisual.Intelligence.csproj` | `CxVisual.Intelligence` |
| Tests | `Tests/ArchitectureSmoke` | `CxVisual.Tests.ArchitectureSmoke.csproj` | `CxVisual.Tests.ArchitectureSmoke` |
| Tests | `Tests/TestCenter` | `CxVisual.TestCenter.csproj` | `CxVisual.TestCenter` |


## 验证

- Linux 环境：XML、XAML解析、项目引用、SLN映射、GUID及依赖无环的静态验证。
- Windows环境：运行 `Build\BUILD.cmd`；执行CLI/WPF及Smoke Tests。
.agents R2的完整性必须在实际导入后补做文件清单比对。
