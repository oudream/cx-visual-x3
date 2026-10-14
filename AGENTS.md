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

## Git commit policy: B + C (task-level commits + local checkpoints)

Applies to Codex, AI coding agents, chat-driven repository changes, and human contributors working in this repository.

- **Task is the unit of delivery.** Do not commit after each file edit, prompt, build, minor fix, or progress update. A finished, independently reviewable and verified task is the default commit boundary.
- **Plan B: one task, one final commit.** Prefer exactly one meaningful commit for a complete task; a complex task may use 2–3 commits only when each has a separate reviewable purpose. This is a default, not permission to split one task artificially.
- **Plan C: local checkpoints are allowed.** During implementation, modify/build/test as many times as needed. If temporary local commits aid recovery, squash or otherwise clean up only the agent's own unshared task commits before handoff or publication.
- **Do not rewrite shared history.** Never amend, reset, rebase, force-push, or squash already-shared commits without explicit user approval. Do not touch other people's or unrelated commits. If history is shared, prefer a clean pull request with squash merge rather than changing history.
- **Commit authorization.** Unless the user explicitly authorizes commits for the current task or an agreed task-level workflow, leave changes uncommitted and report them. Never push or merge without explicit authorization. Do not commit straight to protected/shared branches as a shortcut.
- **Atomic scope.** Keep the commit focused on the task: relevant code, corresponding tests, configuration and essential docs together. Do not include unrelated files, generated outputs, secrets, or another agent's changes. Inspect staged diffs before committing.
- **Verification before finalizing.** Run relevant compilation and tests, report their actual results, and disclose anything unverified. Do not claim "passed" without evidence; if work remains incomplete, do not manufacture a finished-task commit.
- **Readable history.** Use a distinct, specific message such as `feat(logging): complete instance logging with tests`. Avoid repetitive placeholder commits and purely chronological notes.
- **Parallel work.** Prefer a separate feature branch or worktree per agent/task. Integrate via review and preferably squash merge, yielding a clear task-level commit on the target branch.
- **Task handoff.** Report changed scope, verification status, remaining issues, commit count and SHA(s) if any, and whether anything was pushed or merged.

Guiding principle: **many implementation iterations, few meaningful commits**. The Git history should reflect verified deliverables, not the number of AI actions.
