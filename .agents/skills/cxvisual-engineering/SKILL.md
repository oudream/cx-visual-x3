---
name: cxvisual-engineering
description: Apply CxVisual.Unified engineering, C# coding, project layout and migration standards to CxVisual implementation tasks. Use when creating or editing the current 26-project .NET Framework 4.8 solution, Foundation/Mechanism/Professional code, WPF Studio, DLL dependencies, logging, health checks, build/test scripts, versioned deployment, or migrating V7.8 and CxECTStudio patterns. 用于 CxVisual 解决方案、项目、子方面、WPF、V7.8迁移、日志、点检、构建和部署规范。
---

# CxVisual engineering standard (V1.0)

Use this skill only for CxVisual project work. Keep the user-requested scope; do not start a migration, dependency upgrade, or external action merely because the skill mentions it.

## Source of truth

- Read the bundled **verbatim** source: [CxVisual 统一工程、项目、代码与资源规范 V1.0](references/CxVisual_统一工程项目代码与资源规范_V1.0.md). Preserve its decided-versus-pending status.
- The bundled document is a **snapshot of V1.0**. If the repository contains a newer approved edition under `DOCS/`, compare versions and use the latest approved engineering norm, reporting conflicts instead of silently guessing.
- For requirements and cross-project API/Owner disputes, consult the four project baselines named in **V1.0 §0.2** if they are available. Do not pretend unavailable documents were read.
- For third-party DLL/SDK facts, inspect **actual V7.8 project references and the local physical DLL directory**; avoid assuming that a mention or a screenshot makes a binary present. NuGet packages are not contents of `DLL/`.
- For logging and point-check source behavior, refer to the `CxECTStudio` file locations in **V1.0 Appendix A** and check current code before porting.

## Route to relevant chapters

Load only the sections needed for the current task; avoid quoting the whole 41-KB reference into every change.

| Work | Chapters |
|---|---|
| Bootstrap SLN, `.csproj`, areas, paths, outputs | §0–§4, §12–§14 |
| Write/change WPF Studio, themes, XAML and MVVM | §4–§6, §8 |
| Implement C# classes, interfaces, public API, diagnostics | §3, §4, §6, §9 |
| Restore/copy/ship third-party libraries | §7, §8, §11 |
| Implement logging, audit, log viewer | §6, §8–§9 |
| Adapt CxECTStudio point-check framework | §4, §9–§10, §12 |
| Build, versioned rollout, compatibility, test | §3, §7–§8, §11–§14 |

## Essential implementation invariants

1. Preserve **current 26 projects / 135 planned sub-areas** (Foundation 8/22, Mechanism 7/40, Professional 6/73, Applications 2, Intelligence 1, Tests 2; historical 28 included now-retired Lifecycle and merged Logic/Orchestration) and unique sub-area Owner. Use `CxVisual.Unified.sln` and `CxVisual.Foundation.sln`. Do not create one `.csproj` per sub-area, or reintroduce top-level `Adapters`/`Presentation` project groups.
2. Keep **one CxNode engineering truth**. `Project/Page/Content/Symbol/Group/Element`, references and connections must not generate a second persistent Graph tree. Keep Foundation independent of Mechanism/Professional; Mechanism independent of Professional implementation; keep Visual UI-agnostic and Runtime headless.
3. New UI is WPF (XAML + code-behind + ViewModel). Migrate the necessary WinForms behavior; never rename `.Designer.cs` to simulate a WPF conversion. `Mechanism.Presentation.Wpf` does not own engineering state or need `Foundation.Volume`.
4. Use traditional `.csproj`, .NET Framework 4.8 and the V1.0 VS2022 build conventions. **C# 12 is a candidate pending real target-environment validation**, not a proven universal compatibility guarantee; inspect §3.2 before fixing `LangVersion`.
5. Separate source `DLL/` physical runtime libraries from NuGet references, `Output/` build results, and versioned `App-Vx.y/` releases. Reuse exact V7.8 DLL versions only where actually justified and available. Preserve shared `AppData/`, and treat `AppData/Cases/` as **test case data**.
6. Logging keeps **module/category, severity, ordinary and important-event sinks, history, live subscription**. Avoid fragile filename-based module inference and unbounded global state; retain evidence and exception details.
7. Point checks use **module registration → item definition → professional execution → result → manual confirmation → overview**. Adapt into session-based reusable machinery where appropriate. An unimplemented or unsupported check **must never return Passed by default**. Never replace hardware validation with simulated success.
8. When editing, distinguish planned design, source declaration, compilation, test execution and real device acceptance. Do not claim a test passed without an actual command/result. Do not change a user-chosen scope because another V1.0 section remains pending.

## Execution procedure

1. Identify requested change, affected `csproj`, Owner sub-area, APIs and dependencies. Read the relevant V1.0 chapters; confirm availability of other required sources.
2. Review current code/configuration before editing. Preserve existing public contracts and serialized identities unless the requested task includes their approved migration.
3. Make the smallest complete implementation that fulfills the user task, including UI/resource file build actions if relevant. Do not generate empty placeholder implementations to satisfy counts.
4. Run available focused validation: project graph, `msbuild`/Windows x64 builds, tests, WPF resource/XAML checks, DLL presence/architecture, and appropriate normal/abnormal workflow tests. Report unavailable Windows tools or hardware explicitly.
5. Summarize files changed, affected Owner, third-party DLL/NuGet changes, compatibility implications, tests actually run and pending validations. For skeleton creation, distinguish **G0 engineering structure** from later **G1 functional acceptance**.

## Avoid stale assertions

V1.0 is a designed engineering baseline, not proof the new 28-project solution or industrial checks already compile or run. Refer to §14 for pending verification items. If a newer approved document conflicts with V1.0, report the issue and use the current approved source for that domain; do not silently rewrite the bundled reference.
