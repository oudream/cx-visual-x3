# CxVisual.Unified — unified solution and executable samples V1.0-preview

**Architecture:** Foundation (8) → Mechanism (9) → Professional Domain (6) → Applications (2), with Intelligence (1) and Tests (5): **31 traditional C# projects** (includes FoundationWpf, ConfigFoundationSmoke and TestCenter).

**Target platform:** Windows x64 / Visual Studio 2019 or 2022 / .NET Framework 4.8 Developer Pack, MSBuild 15+.
**UI:** WPF and MVVM, no WinForms, no VTK dependency in this sample. `Project/Page/Content/Symbol/Group/Element` lives in `CxVisual.Mechanism.Core`, not Foundation. **Only the specified sample projects contain production-like demonstration behavior.** Other projects are deliberately assembly-only stubs, not invented functionality.

## 文档入口与历史代码资产

- **统一工程组织体系 V1.1**：[完整架构设计与开发使用指南](DOCS/Architecture/CxVisual_统一工程组织体系_完整架构设计与开发使用指南_V1.1.md)。保留六角色/八案例，深化x2复用、H00待裁决及H01～H07实施验收；设计待审，不自动编码。

- **统一配置 V2.4 阶段 C/D**：[自动配置 UI、Windows 性能验收与自检查矩阵](DOCS/Implementation/CxVisual_统一配置体系_V2.4_阶段C与D实施和复核验收矩阵.md)。

- **统一配置体系 V2.4 阶段 B**：[开发者 API 与动态编辑验收矩阵](DOCS/Implementation/CxVisual_统一配置体系_V2.4_阶段B开发者API与动态编辑验收矩阵.md)。

- **阶段 A 的实际验收矩阵**：[V2.4 层级定义、Schema、恢复、安全改造与 CI 证据](DOCS/Implementation/CxVisual_统一配置体系_V2.4_阶段A实施与验收矩阵.md)。

- **统一配置体系 V2.4 主规范**：[完整设计、API、自动配置界面与验收](DOCS/Implementation/CxVisual_统一配置体系_完整设计与验收_V2.4.md)；[阶段 A-D 实施计划](DOCS/Implementation/CxVisual_统一配置体系_V2.4_源码审查与分阶段实施计划.md)。基础只在 Foundation.Data / Mechanism.Data 两个项目，功能完成状态须以 CI/验收矩阵为准。

- **协同开发主入口**：[DOCS/00_CxVisual_协同开发入口与七仓复用基线.md](DOCS/00_CxVisual_协同开发入口与七仓复用基线.md)；工程规则见 [AGENTS.md](AGENTS.md)，Codex任务规范见 [.agents/skills/cxvisual-engineering/SKILL.md](.agents/skills/cxvisual-engineering/SKILL.md)。
- **七仓技术资产档案（按任务选择阅读，不要求每次通读）**：[01_ccxx.md](DOCS/Codebases/01_ccxx.md)（C/C++基础库）、[02_device_communicator.md](DOCS/Codebases/02_device_communicator.md)（IoT设备模型、点表、通信、监控）、[03_gcl3.md](DOCS/Codebases/03_gcl3.md)（ICS/SCADA、终端、实时数据与Bus）。
- **重要边界**：这些档案包含源代码事实、历史工程经验及**可修正设计提案**，不是新CxVisual已实现/已批准的类型或测试结果；以现行DOCS规格、真实源码与经验证结果裁决。
- **入口同步规则**：新增、重命名或实质更新`DOCS/Codebases/`档案时，检查并按需同步本README、`AGENTS.md`和`DOCS/00_`导航；只保留一个专项主文档，避免重复事实源。

- **统一运行体系综合讨论**：[六角色期望、并发异步与可视化编排执行 V0.1](DOCS/Architecture/CxVisual_统一运行体系_六角色期望与并发异步编排执行_综合设计讨论稿_V0.1.md)。这是阶段性共识和功能需求，不是已批准API或已实现引擎；两处集中实现方向为 F19 Concurrency 与 Runtime Execution。

- **执行体系TDD（持续更新至V0.6.1）**：[UnifiedExecution_TDD_Matrix_V0.1.md](DOCS/Testing/UnifiedExecution_TDD_Matrix_V0.1.md)；F19有界Keyed Worker、Runtime轻量DAG执行引擎，按G0～G4区分已实现/待验收。

- **统一运行内核V0.6.1已实现能力与验证边界**：[内核实现与验收说明](DOCS/Testing/CxVisual_统一运行体系内核_实现与验收_V0.3.md)（条件循环、事件/定时触发、断点单步、资源仲裁、执行策略、有限Trace及代码测试；不表示跨进程或实机验收）。

- **六角色差距审计与运行内核V0.6.1**：[逐项需求、新增执行概念、未闭合契约及下一轮测试](DOCS/Architecture/CxVisual_统一运行体系_六角色差距审计与内核增构_V0.5.md)。实际新增ExecutionGroup、端口绑定、Flow身份/Session、条件Join、计划编译、作用域数据、Journal、预计字节背压与专用SDK线程；保留正式发布、持久恢复、跨进程和授权差距。

## Build and verify on Windows

1. Open `CxVisual.Unified.sln` in Visual Studio, select **Debug | x64**, **Rebuild Solution**.
2. Or run `Build\BUILD.cmd` (compiles the entire solution then runs independent CLI smoke tests).
3. `Build\BUILD.cmd -ShowGUI` also launches `CxVisual.Studio.exe` for manual acceptance.
4. `CxVisual.Foundation.sln` is a smaller Foundation-only solution with its smoke test.
5. All built binaries go to `Output\<Configuration>\x64\`. Intermediate `obj` files remain project-local.

A manual graphical acceptance procedure is in `Examples/WPF/README.md`. The CLI commands are in `Examples/CLI/README.md`.

## Layout

```text
CxVisual.Unified/
├── CxVisual.Unified.sln            # all 31 projects
├── CxVisual.Foundation.sln         # 8 foundation projects + 1 test
├── Foundation/                     # 8 projects
├── Mechanism/                      # 9 projects incl. WPF adapter
├── Professional/                   # 6 domain workbench projects
├── Applications/                   # Studio (WPF) / Runtime.Host (CLI)
├── Intelligence/                   # 1 project
├── Tests/                          # FoundationSmoke / ArchitectureSmoke / FoundationWpf / ConfigFoundationSmoke / TestCenter
├── DOCS/                           # decisions / testing / migration / catalog
├── DLL/x64/                        # actual manually staged runtime binaries only
├── Examples/{CLI,WPF,Cases}/       # demos + test fixtures
├── Legacy/{V7.8,App-V1.1,App-V1.2,App-V2.3}/
├── Build/                          # MSBuild props / scripts
└── Output/{Debug,Release}/x64/     # centralized built assemblies and logs
```

## Exactly which projects have behavior in this sample?

- `CxVisual.Foundation.Core`: pure stable identifier / numeric guard.
- `CxVisual.Foundation.Geometry`: rectangle and 2D affine operations.
- `CxVisual.Foundation.Infrastructure`: categorized instance-based log sinks.
- `CxVisual.Mechanism.Core`: **canonical engineering tree, atomic batch, revision conflict and XML roundtrip**.
- `CxVisual.Mechanism.Visual`: UI-independent projection, edit history, undo/redo, hit-testing.
- `CxVisual.Mechanism.Presentation.Wpf`: WPF painting and pointer input adapter only.
- `CxVisual.Runtime.Host`: CLI demo/save/load executable.
- `CxVisual.Studio`: interactive WPF application, command toolbar and project state panel.
- `CxVisual.Tests.FoundationSmoke` and `CxVisual.Tests.ArchitectureSmoke`: independent CLI tests, exit 0/1.
- `CxVisual.Tests.FoundationWpf`: standalone WPF test harness for F16 logging and F19 monitoring; requires manual GUI acceptance.

Remaining **18 projects** have real `.csproj` and assembly metadata only. This is intentional; they are not yet implemented or tested domain capabilities.

## Spec/source status

The V1.0 preview was originally created before a fully verified specification-based implementation. The repository now contains `DOCS/CxVisual_统一工程项目代码与资源规范_V1.0.md`; use its current contents and `DOCS/ComplianceMatrix.md` when reviewing spec-to-code differences. The existence of the specification does **not** imply that the preview implements all planned capabilities.

Only structural checks were executed in the current Linux environment; Windows .NET Framework/MSBuild/WPF execution has **not** yet occurred. See `DOCS/Testing/VerificationReport.md`.

## V1.1 补充
磁盘目录已缩短为Foundation/Core等，项目和程序集名称保留CxVisual前缀。详见 DOCS/DirectoryLayout_V1.1.md。现已存在 `.agents/skills/cxvisual-engineering/SKILL.md` 及其规范引用；以仓内最新规范、实际源码和测试结果为准，Skill快照不是已实现证明。

## F16/F19基础能力本轮补充

- `Foundation.Infrastructure`：统一日志记录、`FLDLogService`有界队列、专有文件简单日志、重要事件、UTC滚动日志和保留策略；`FCRSystemMonitor`系统与进程指标。
- `Mechanism.Presentation.Wpf`：可复用 `LogViewerControl` 与 `SystemMonitorControl`；不把WPF依赖带入Foundation。
- `Tests/FoundationWpf`：独立窗口，三页签，日志操作、监控、压力/异常；只写入系统临时目录。
- CLI验收 `CxVisual.Tests.FoundationSmoke.exe` 扩充F16/F19正常与异常测试；GUI人工验收：`Build\BUILD.cmd -ShowGUI`，或手动启动 `Output\Debug\x64\CxVisual.Tests.FoundationWpf.exe`。

**验证边界：** 此实现基于源码审阅与结构检查；只有Windows VS2022/MSBuild17 + .NET Framework 4.8环境执行成功后，才能写“编译/自动测试/WPF运行通过”。见 `DOCS/Implementation/Foundation_F16F19_Milestone.md`。


## F16命名修订与实时日志（V6.4对齐）
公开日志基础类型：`FLDLogHelper`（静态便捷入口）、`FLDLogLevel`、`FLDLogRecord`、`FLDLogScope`、`FLDLogEventHandler`；状态服务：`FLDLogService`及`FLDDedicatedLogger`等为运行实现候选，已统一FLD前缀。原`Cx...`开发预览名不作为正式API。
示例：`FLDLogHelper.Initialize(logRoot);` → `FLDLogHelper.Info("启动");` / `FLDLogHelper.ForModule("CT3D.Recon").Information("完成");` / `FLDLogHelper.ForDedicated("M13", "PLC01").Warning("超时");` → `FLDLogHelper.Stop();`。
`FLDLogHelper.LogEvent`推送实时记录，WPF直接订阅`FLDLogService.RecordReceived`，后台线程回调应Dispatcher切换。
按`CallerFilePath`+规则匹配自动补模块，显式module优先，保留`CallerMemberName`。F19原规范只有FCR并发对象；本次`FCRSystemMonitor`等属于**待补录候选**，不能说已在V6.4表中定案。

## F16同步专有日志（与普通实时订阅独立）

新增`FLDEventLogHelper`/`FLDImportantEventLog`同步文件接口，支持MES/Panda/PLC等分类、自定义类别、按本地日期独立文件、流Flush、类别级门限和只清理当前类别历史文件；默认不进入`FLDLogHelper`异步队列，也不触发`LogEvent`，详见`DOCS/Implementation/F16_SynchronousDedicatedFileLogging_V1.md`。

CLI自动测试已登记在`FoundationSmoke/SynchronousLogTests.cs`；FoundationWpf提供同步MES/Panda测试按钮。当前尚未有Windows MSBuild/.NET Framework 4.8实编译及动态验收记录，请不要将源码静态检查误认为已通过运行测试。

## TestCenter (TCT)

The platform owns a single WPF/.NET Framework 4.8 test application at `Tests/TestCenter` (not a seventh Workbench). It provides isolated Worker execution, timeout/cancel, assert/observation viewers, reflection/attribute discovery, fixture/baseline management, parallel/repeat execution, and JUnit/JSON/text reports. TCT/ITCT types stay within Tests; production Foundation and Mechanism never depend on TCT.

Windows x64: build `CxVisual.Unified.sln`, then run `Output/Debug/x64/CxVisual.TestCenter.exe --run --report TestCenter.junit.xml --format junit --isolate`. Source is staged for Windows verification; never infer a completed build from a project file.
