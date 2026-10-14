# CxVisual.Unified repository guidance (merge into existing AGENTS.md)

This repository uses the approved CxVisual F/M/P architecture and engineering standards.

## Required workflow

- For CxVisual solution, `.csproj`, source code, WPF, DLL/SDK, logging, point-check, build, test, migration or deployment work, use **`$cxvisual-engineering`** and read the relevant sections of its bundled V1.0 reference (or a newer approved repo version).
- Observe the agreed 28-project / 135-sub-area Owner map and project dependency DAG; avoid adding projects, APIs or SDK references without a concrete need and approved owner.
- Keep V7.8 source and physical DLL reality distinct from new specifications. Never fabricate SDKs, successful builds, point-check passes or physical hardware validation.
- Do not claim changes are complete without reporting actual tests, their outputs, and unverified constraints.
- Respect the immediate user's task scope; the skill reference is not permission to perform unrelated edits.

See `.agents/skills/cxvisual-engineering/SKILL.md` for the task-specific process.

## Start every CxVisual development session here

- Read [DOCS/00_CxVisual_协同开发入口与七仓复用基线.md](DOCS/00_CxVisual_协同开发入口与七仓复用基线.md) first. It maps the seven code repositories, distinguishes production-proven external systems from the new x3 preview, and gives the per-project chat/development workflow.
- The canonical new platform is `cx-visual-x3`; `cx-visual-x2` is the previous CxVisual. The other five existing codebases are production-experienced assets: `cx-vision-x2` (CT/online and offline X-Ray), `gcl3` (distributed ICS), `ccxx` (cross-platform C++), `device_communicator` (IoT), `cx-cct-x3` (CT/3D). Reuse after inspecting real code and compatibility; do not blindly rewrite mature subsystems.
- Before claiming a status, record branch/commit and distinguish specification, source declaration, actual compilation/tests, and historical production evidence.
