# CxVisual.Unified repository guidance (merge into existing AGENTS.md)

This repository uses the approved CxVisual F/M/P architecture and engineering standards.

## Required workflow

- For CxVisual solution, `.csproj`, source code, WPF, DLL/SDK, logging, point-check, build, test, migration or deployment work, use **`$cxvisual-engineering`** and read the relevant sections of its bundled V1.0 reference (or a newer approved repo version).
- Observe the agreed 28-project / 135-sub-area Owner map and project dependency DAG; avoid adding projects, APIs or SDK references without a concrete need and approved owner.
- Keep V7.8 source and physical DLL reality distinct from new specifications. Never fabricate SDKs, successful builds, point-check passes or physical hardware validation.
- Do not claim changes are complete without reporting actual tests, their outputs, and unverified constraints.
- Respect the immediate user's task scope; the skill reference is not permission to perform unrelated edits.

See `.agents/skills/cxvisual-engineering/SKILL.md` for the task-specific process.

## Historical codebase dossiers and entry maintenance

- Source audit navigation: [01_ccxx.md](DOCS/Codebases/01_ccxx.md) (cross-platform C++ foundations), [02_device_communicator.md](DOCS/Codebases/02_device_communicator.md) (IoT model, point sheets, protocol sessions and monitoring), [03_gcl3.md](DOCS/Codebases/03_gcl3.md) (distributed ICS/SCADA). Select the dossier relevant to the active task; inspect real source and version before reuse. Do not treat dossier proposals as approved architecture, implemented APIs, or tests.
- When creating, renaming, moving or materially updating a `DOCS/Codebases/` dossier, **in the same task/commit** check and update navigation in `README.md`, this `AGENTS.md`, and `DOCS/00_CxVisual_协同开发入口与七仓复用基线.md` as needed. Validate that links resolve; avoid unnecessary edits when an existing link and description remain accurate.
- Keep `DOCS/00_CxVisual_协同开发入口与七仓复用基线.md` as the full navigation and evidence-status entry, rather than duplicating technical specifications in AGENTS or README.

- For concurrency, asynchronous IO, execution groups, workflow scheduling or runtime debugging discussions, consult [CxVisual unified execution-system discussion V0.1](DOCS/Architecture/CxVisual_统一运行体系_六角色期望与并发异步编排执行_综合设计讨论稿_V0.1.md). It records R1-R6 user goals, E01-E18 candidate requirements and a two-directory implementation direction; **it is not an approved API or implementation status**. Keep any future code changes scoped and source-audited.

## Start every CxVisual development session here

- Read [DOCS/00_CxVisual_协同开发入口与七仓复用基线.md](DOCS/00_CxVisual_协同开发入口与七仓复用基线.md) first. It maps the seven code repositories, distinguishes production-proven external systems from the new x3 preview, and gives the per-project chat/development workflow.
- The canonical new platform is `cx-visual-x3`; `cx-visual-x2` is the previous CxVisual. The other five existing codebases are production-experienced assets: `cx-vision-x2` (CT/online and offline X-Ray), `gcl3` (distributed ICS), `ccxx` (cross-platform C++), `device_communicator` (IoT), `cx-cct-x3` (CT/3D). Reuse after inspecting real code and compatibility; do not blindly rewrite mature subsystems.
- Before claiming a status, record branch/commit and distinguish specification, source declaration, actual compilation/tests, and historical production evidence.
