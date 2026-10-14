# CxVisual.Unified repository guidance (merge into existing AGENTS.md)

This repository uses the approved CxVisual F/M/P architecture and engineering standards.

## Required workflow

- For CxVisual solution, `.csproj`, source code, WPF, DLL/SDK, logging, point-check, build, test, migration or deployment work, use **`$cxvisual-engineering`** and read the relevant sections of its bundled V1.0 reference (or a newer approved repo version).
- Observe the agreed 28-project / 135-sub-area Owner map and project dependency DAG; avoid adding projects, APIs or SDK references without a concrete need and approved owner.
- Keep V7.8 source and physical DLL reality distinct from new specifications. Never fabricate SDKs, successful builds, point-check passes or physical hardware validation.
- Do not claim changes are complete without reporting actual tests, their outputs, and unverified constraints.
- Respect the immediate user's task scope; the skill reference is not permission to perform unrelated edits.

See `.agents/skills/cxvisual-engineering/SKILL.md` for the task-specific process.
