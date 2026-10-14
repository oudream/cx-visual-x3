# CxVisual.Unified — unified solution and executable samples V1.0-preview

**Architecture:** Foundation (8) → Mechanism (9) → Professional Domain (6) → Applications (2), with Intelligence (1) and Tests (2): **28 traditional C# projects**.

**Target platform:** Windows x64 / Visual Studio 2019 or 2022 / .NET Framework 4.8 Developer Pack, MSBuild 15+.
**UI:** WPF and MVVM, no WinForms, no VTK dependency in this sample. `Project/Page/Content/Symbol/Group/Element` lives in `CxVisual.Mechanism.Core`, not Foundation. **Only the specified sample projects contain production-like demonstration behavior.** Other projects are deliberately assembly-only stubs, not invented functionality.

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

The separate document **《CxVisual 统一工程、项目、代码与资源规范 V1.0》 was not accessible as a file** at creation time. This preview follows the most recently approved design decisions recoverable from prior discussions, not a claimed pixel/paragraph exact reproduction. See `DOCS/ComplianceMatrix.md`.

Only structural checks were executed in the current Linux environment; Windows .NET Framework/MSBuild/WPF execution has **not** yet occurred. See `DOCS/Testing/VerificationReport.md`.

## V1.1 补充
磁盘目录已缩短为Foundation/Core等，项目和程序集名称保留CxVisual前缀。详见 DOCS/DirectoryLayout_V1.1.md。`.agents/`已预留，但Codex Skill V1.0 R2因原始文件不可读取暂未导入。
