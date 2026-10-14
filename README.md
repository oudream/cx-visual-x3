# CxVisual.Unified — unified solution and executable samples V1.0-preview

**Architecture:** Foundation (8) → Mechanism (9) → Professional Domain (6) → Applications (2), with Intelligence (1) and Tests (2): **28 traditional C# projects**.

**Target platform:** Windows x64 / Visual Studio 2019 or 2022 / .NET Framework 4.8 Developer Pack, MSBuild 15+.
**UI:** WPF and MVVM, no WinForms, no VTK dependency in this sample. `Project/Page/Content/Symbol/Group/Element` lives in `CxVisual.Mechanism.Core`, not Foundation. **Only the specified sample projects contain production-like demonstration behavior.** Other projects are deliberately assembly-only stubs, not invented functionality.

## 文档入口与历史代码资产

- **协同开发主入口**：[DOCS/00_CxVisual_协同开发入口与七仓复用基线.md](DOCS/00_CxVisual_协同开发入口与七仓复用基线.md)；工程规则见 [AGENTS.md](AGENTS.md)，Codex任务规范见 [.agents/skills/cxvisual-engineering/SKILL.md](.agents/skills/cxvisual-engineering/SKILL.md)。
- **七仓技术资产档案（按任务选择阅读，不要求每次通读）**：[01_ccxx.md](DOCS/Codebases/01_ccxx.md)（C/C++基础库）、[02_device_communicator.md](DOCS/Codebases/02_device_communicator.md)（IoT设备模型、点表、通信、监控）、[03_gcl3.md](DOCS/Codebases/03_gcl3.md)（ICS/SCADA、终端、实时数据与Bus）。
- **重要边界**：这些档案包含源代码事实、历史工程经验及**可修正设计提案**，不是新CxVisual已实现/已批准的类型或测试结果；以现行DOCS规格、真实源码与经验证结果裁决。
- **入口同步规则**：新增、重命名或实质更新`DOCS/Codebases/`档案时，检查并按需同步本README、`AGENTS.md`和`DOCS/00_`导航；只保留一个专项主文档，避免重复事实源。

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
├── CxVisual.Unified.sln            # all 28 projects
├── CxVisual.Foundation.sln         # 8 foundation projects + 1 test
├── Foundation/                     # 8 projects
├── Mechanism/                      # 9 projects incl. WPF adapter
├── Professional/                   # 6 domain workbench projects
├── Applications/                   # Studio (WPF) / Runtime.Host (CLI)
├── Intelligence/                   # 1 project
├── Tests/                          # FoundationSmoke / ArchitectureSmoke
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

Remaining **18 projects** have real `.csproj` and assembly metadata only. This is intentional; they are not yet implemented or tested domain capabilities.

## Spec/source status

The V1.0 preview was originally created before a fully verified specification-based implementation. The repository now contains `DOCS/CxVisual_统一工程项目代码与资源规范_V1.0.md`; use its current contents and `DOCS/ComplianceMatrix.md` when reviewing spec-to-code differences. The existence of the specification does **not** imply that the preview implements all planned capabilities.

Only structural checks were executed in the current Linux environment; Windows .NET Framework/MSBuild/WPF execution has **not** yet occurred. See `DOCS/Testing/VerificationReport.md`.

## V1.1 补充
磁盘目录已缩短为Foundation/Core等，项目和程序集名称保留CxVisual前缀。详见 DOCS/DirectoryLayout_V1.1.md。现已存在 `.agents/skills/cxvisual-engineering/SKILL.md` 及其规范引用；以仓内最新规范、实际源码和测试结果为准，Skill快照不是已实现证明。
