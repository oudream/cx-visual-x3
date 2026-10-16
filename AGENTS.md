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

- For F19/Runtime execution implementation, consult [Unified execution TDD matrix (updated V0.6.1)](DOCS/Testing/UnifiedExecution_TDD_Matrix_V0.1.md). Keep the existing F19 and Runtime.Execution project ownership; process-local resource leases, known-plan inlined subflows and in-memory plan slots have executable tests; do not mistake these for cross-process ownership, dynamic subflow compilation, authorized production publishing, hardware safety confirmation or Studio WPF debug UI. Run the Windows x64 build, mandatory `FoundationSmoke --concurrency`, and ArchitectureSmoke before claiming success.

## 提交与合并规范（ChatGPT、Codex及人工协作均适用）

- **所有新Git提交说明必须使用中文**：标题采用简洁中文概述实际变化，必要时正文以中文说明影响范围、测试结果、兼容性与未验证项；不再默认使用英文 `feat/fix/refactor/docs` 形式的标题。
- **本项目尚未发布：默认直接在 `master` 开发和提交，不创建任何开发分支、不走日常分支合并流程。** 只有用户明确要求或者仓库规则强制时才使用其他分支；每次改动需验证当前master HEAD，提交后核对代码、文件及CI。
- **提交要少、主题要集中、便于审阅**：直接在master进行少量主题明确的中文提交；尽量一次可运行的修改一次提交；CI失败时在master修复并重新验证，不建立临时开发分支。
- **禁止为修改旧英文提交说明而强制改写已共享的 `master` 历史**；确有历史整理需求时应先评估对协作的影响并明确征得同意。
- **提交信息不得冒充验证**：只有实际跑过且有日志的编译、测试、设备验证才能写为“通过”；设计与待测试内容应如实注明。

- For current headless runtime execution source, consult [Runtime Core Implementation and Acceptance V0.3](DOCS/Testing/CxVisual_统一运行体系内核_实现与验收_V0.3.md). Finish reusable F19/Runtime behavior and tests before professional integration, while keeping hardware safety and cross-process features honestly unverified.

- For six-role E01-E18 gap analysis, new execution groups/ports/flow identity/session and outstanding T24-T38 cases, use [Execution architecture gap audit V0.6.1](DOCS/Architecture/CxVisual_统一运行体系_六角色差距审计与内核增构_V0.5.md). Newly implemented compiler/data-scopes/journal/dedicated SDK executor are tested in existing projects; they are NOT evidence for full orchestration translation, crash-safe workflow replay, approved publishing, cross-process ownership or physical interlocks. Do not claim full production readiness from headless smoke tests.

## Start every CxVisual development session here

- Read [DOCS/00_CxVisual_协同开发入口与七仓复用基线.md](DOCS/00_CxVisual_协同开发入口与七仓复用基线.md) first. It maps the seven code repositories, distinguishes production-proven external systems from the new x3 preview, and gives the per-project chat/development workflow.
- The canonical new platform is `cx-visual-x3`; `cx-visual-x2` is the previous CxVisual. The other five existing codebases are production-experienced assets: `cx-vision-x2` (CT/online and offline X-Ray), `gcl3` (distributed ICS), `ccxx` (cross-platform C++), `device_communicator` (IoT), `cx-cct-x3` (CT/3D). Reuse after inspecting real code and compatibility; do not blindly rewrite mature subsystems.
- Before claiming a status, record branch/commit and distinguish specification, source declaration, actual compilation/tests, and historical production evidence.
