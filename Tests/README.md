# CxVisual Tests——统一测试组织、职责边界与协作规范 V1.0

> **文件**：`Tests/README.md`；**日期**：2026-10-22；**性质**：测试组织、入口、协作规则。  
> **核查起点**：`oudream/cx-visual-x3` / `master` / `0c3981a4f936979482f02b9372bbe710c9d2a4b5`。后续执行以最新 HEAD 和实际代码为准。  
> **平台原则**：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**

**核心分工：ArchitectureSmoke 提供独立、快速、可靠的代码及架构回归门禁；TestCenter 提供完整、统一的测试组织、执行、观察和验收，并逐步承载与自动测试明显不同的“人工专项交互实验台”。**

本文是 Tests 目录的**工作入口和协作规则**，不另建测试体系的第二套技术规范。权威技术规格为 [《CxVisual 测试体系——完整规格 V4.1》](../DOCS/Architecture/CxVisual_测试体系——完整规格%20V4.1.md)，状态与真实证据参见 [TestCenter 全局工作包台账](../DOCS/Testing/CxVisual_TestCenter_V4.0_全局工作包与验收台账.md)及 [ArchitectureSmoke 归档记录](../DOCS/Testing/CxVisual_ArchitectureSmoke_归档与独立配置入口_V1.0.md)。主仓的 [AGENTS.md](../AGENTS.md) 与 [工程 Skill](../.agents/skills/cxvisual-engineering/SKILL.md) 仍适用。任何需求/Owner/实现冲突，以最新批准规格及真实代码核查结果处理，不在本文件私自改变已定生产合同。

## 01. 测试目标、三个入口、两类界面

1. **代码可信**：开发中的 Foundation、Mechanism、Professional 与 Applications 能力必须有正常、异常、边界及回归测试。
2. **使用便捷**：软件开发者直接运行专项命令或写 NUnit；人工调试者打开专业实验台，按实际操作流程检查。
3. **证据真实**：区分源码、编译、自动执行、GUI 交互、人工签认、专业/设备现场验收，所有结论可回到 Commit、环境与原始结果。
4. **归属清晰**：测试依赖生产 Owner；生产 Owner 不依赖 Tests。通道、规约、绘图和专业算法的真实能力继续由 F/M/P 及既有应用负责。

| 入口 | 定位 | 人主要在做什么 | 结论性质 |
|---|---|---|---|
| `ArchitectureSmoke.exe` | 自动代码/架构回归 | 开发者直接运行分组/默认测试、CI 自动执行 | 进程 ExitCode 与断言结果 |
| TestCenter · **自动化测试** | 统一测试与验收 | 发现 Case、选择 Suite/Plan、Run/Repeat、查看 Result/History/Diff | Run/Attempt/Assertion 结构化证据 |
| TestCenter · **专项交互实验台** | 人工细节调试与探索 | 连接、收发、协议构造/解析、画图编辑、观察状态、注入故障、记录复现 | **ManualSession / 操作日志**；不是自动 PASS |

**必须区分两种“专业界面”**：

- **测试结果 Viewer**：已有 Geometry、Graph、Channel、Runtime 等只读 Result/Observation 观察器，归自动验收工作区。
- **人工操作 Workbench**：能主动编辑参数、建立会话、发送数据、绘制/操作画布等的交互实验台，归专项交互工作区。二者可共享显示控件和证据格式，但不能共享可变运行状态，更不能由 WPF View 直接生成通过判定。

## 02. 仓库实际结构

~~~text
Tests/
├── README.md                          ← 组织、责任与协作（本文件）
├── ArchitectureSmoke/
│   ├── CxVisual.Tests.ArchitectureSmoke.csproj
│   ├── Program.cs                     ← 分组CLI/默认回归
│   ├── Configuration/
│   ├── Engineering/
│   ├── Foundation/
│   ├── Integration/
│   ├── Orchestration/
│   ├── Runtime/
│   └── Visual/
└── TestCenter/
    ├── CxVisual.TestCenter.csproj     ← WPF + CLI + Worker
    ├── README.md                      ← TestCenter开发者快速上手
    ├── Cases/ Core/ Discovery/ Execution/
    ├── Diagnostics/ Fixtures/ Models/
    ├── NUnitIntegration/ Reports/ UI/
    └── CxVisual_TestCenter_统一主界面与工作区Frame注册——完整设计与验收_V1.1.md
~~~

构建生成的 `obj/`、`bin/` 不属于源码架构。当前正式测试项目保持 **2 个**，整个解决方案保持既定 **28 个项目**。源代码文件与 UI 子目录可按需要组织，但不得“每个子方面一个 csproj”。

> **文档歧义澄清**：V4.1 的“两个测试支撑项目”一处描述的是 `Foundation.Infrastructure` 与 `TestCenter` 的职责分工，**不是**这里 `Tests/` 下 `ArchitectureSmoke` 与 `TestCenter` 两个测试工程；两种统计口径不能混用。

## 03. ArchitectureSmoke 责任与现行执行入口

**责任**：F/M/P 核心不变量、典型代码路径、错误反证、依赖隔离、工程保存重开、编排执行、WPF STA 专项、必要的回归和性能基线。它并非只有一两个“冒烟”用例的轻量空壳；已有测试必须保留。

**源码现状**：`Tests/ArchitectureSmoke/Program.cs` 调度已存在的 `Run()` / `RunAll()` 等测试；`FoundationSmoke` / `ConfigFoundationSmoke` 通过原测试命名空间在同一项目中运行，其中默认 Foundation 回归使用受控子进程隔离静态资源。

在 Windows .NET Framework 4.8、对应配置构建后，从仓库根目录执行：

~~~powershell
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --concurrency
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --foundation
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --organization
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --organization-wpf
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --config-foundation
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --config-performance-smoke
~~~

还支持 `--config-performance-full`、`--config-wpf`、`--organization-benchmark` 和 `--organization-stress`。请以现行 `Program.cs` 和 CI 任务命令为准。

**不承担**：另建 Catalog、通用 Suite 管理、Golden 审批、历史可视化平台、用户人工调试实验台，或直接操作工业设备的 WPF 窗口。默认/专项回归失败必须导致非零退出码，不可仅打印 PASS 文本便当作完整结果。

## 04. TestCenter 责任与现行使用方式

**现有能力**：`Tests/TestCenter` 内 `Cases / Discovery / Execution / NUnitIntegration / Reports / UI` 支撑统一 Catalog、标准 NUnit 与 TCT、Worker 隔离、结构化 Attempt/Observation、历史、Diff、WPF 工作台与 UI 自动化。UI 使用 AvalonDock 5.0.1，已具有 Explorer、DocumentHost、Inspector、Results、Timeline、History、Plan 和 Diff 等面板。

快速使用：

~~~powershell
.\Output\Release\x64\CxVisual.TestCenter.exe --list --report .\Output\Release\x64\catalog.json
.\Output\Release\x64\CxVisual.TestCenter.exe --run --profile Regression --report .\Output\Release\x64\regression.junit.xml --format junit --isolate
.\Output\Release\x64\CxVisual.TestCenter.exe --ui-smoke
.\Output\Release\x64\CxVisual.TestCenter.exe --ui-automation
~~~

普通新增测试在 `Tests/TestCenter/Automated/Cases/` 中首选 NUnit `[Test] / [TestCase]`；需要真实 Owner 结构化证据时使用已有 TCT Context，详见 [TestCenter 开发者 README](TestCenter/README.md)。

**唯一界面设计与验收入口**：[《TestCenter 统一主界面与工作区Frame注册·完整设计与验收 V1.1》](TestCenter/CxVisual_TestCenter_统一主界面与工作区Frame注册——完整设计与验收_V1.1.md)。四根平级、各有图标和总页；自动测试统一复用同一界面按 Catalog Scope/Case 切换；人工专业操作页独立扩展，右侧统一操作、浅色简洁布局。**已有注册/Virtual Channel 源码，全面完成情况以同 SHA 编译和验收为准**。

## 05. 新测试到底放在哪里：判定规则

| 测试需求 | 推荐入口 | 关键理由 |
|---|---|---|
| 关键纯逻辑、Owner 依赖、序列化、并发、机制变化的快速回归 | ArchitectureSmoke 的对应现有领域目录 | 构建后可直接运行；保持独立 CI 门禁 |
| 标准同步/异步/参数化、集成与黑盒验证 | TestCenter / Cases | 标准 NUnit、可选择执行、结果可记录 |
| 需要 Worker、Fixture、证据回放、专业 Viewer、历史比较 | TestCenter / Cases + Models/Reports/UI | 一个 Catalog/Run/Attempt/Result 真源 |
| 人工反复收发与协议字段调试、鼠标精细绘制、可视操作试验 | TestCenter / 专项交互实验台（目标） | 可持续会话、专用工具条/画布，不是只读 Viewer |
| 长稳、可靠性、性能压测 | 复用现有测试入口及专门 CI 任务 | 环境、基线与独立资源门禁，不能受 UI 操作干扰 |
| 真设备/运动/PLC/X-Ray/HIL | 专业 Owner 审核 + TestCenter 的受控用例或实验台 | 必须独立授权、联锁及现场证据；默认不运行真设备 |

**决策顺序**：先确认生产 Owner 和已有测试 → 判断是否只需要快速回归 → 是否需要自动化证据/编排 → 是否必须人工交互 → 选择唯一主用例位置 → 在需要的另一入口使用受控适配或引用证据。

**反重复规则**：

1. 不因需要在两个地方看到结果，就复制同一业务测试断言、算法、运行状态或完整用例实现。
2. ArchitectureSmoke 已有稳定测试不强制迁走；新增测试优先选更合适的入口，兼容既有流水线。
3. 当某能力既需要快速 CI 门禁又需要复杂测试，允许不同测试层次：前者验证最小关键不变量，后者验证端到端/操作/证据；不能“两个名字、同一用例”长期并行维护。
4. TestCenter **直接执行 ArchitectureSmoke 并将其结构化纳入管理**目前属于后续**可选适配**，不是已实现功能。需要时以受控子进程 + 确切 ExitCode/日志/超时/身份元数据接入；未具备逐条结果协议时不得凭 stdout 猜造每条 Attempt 的通过数。
5. 人工实验台的“连接成功、收到一帧、图元已画出”属于操作事实；只有显式预期、断言、测试执行和审核证据齐备时，才能形成正式自动/人工验收结论。

## 06. 人工专项测试的新增边界

**默认不另建项目**。在现有 TestCenter WPF 程序中新增“专项交互实验台”一级工作区，并按功能创建可复用子页面，例如 **Channel / Protocol / Graphics / Vision / CT3D**。使用 **同一个窗口与 AvalonDock**，不同工具各有自己的操作 Tab、配置 Inspector 和实时日志。

- **操作层**：参数输入、连接/断开、收发、单步解析、画图、选中/拖动/变换、保存临时会话、错误注入、暂停/停止等；动作由受控控制器调用真实 Owner 契约。
- **观察层**：TX/RX、Hex、解析字段、画布状态、像素/坐标、Timeline、错误、性能指标等；界面状态与生产持久工程/测试结果状态分离。
- **会话层**：ManualSessionId、工具类型、环境/虚实模式、目标、操作记录、连接状态和清理行为。ManualSession 不等于 TestCenter Run/Attempt，不是第二套 MRK Runtime。
- **证据衔接**：用户显式“保存证据/转为回归用例/提交测试”时，复用现有 TCT 证据格式、真实 Owner 和受控用例执行；不能自动把手工实验标绿。
- **安全边界**：默认 Virtual/Replay；连接物理设备需身份、权限、模式、目标和专业 Owner 安全检查。Worker 退出、TCP 断开、UI 停止不等于物理联锁或真实安全停止。

**独立进程/项目的例外**：只有确有原生 SDK 位数或运行时不兼容、崩溃隔离、独立部署、长期后台运行/高性能资源约束等真实证据，且经 Owner、依赖、交互与结果协议评审后才考虑；即使另起进程，也应优先让 TestCenter 作为入口和观察/证据层，不能直接复制第二套框架。**本轮不审批新 csproj。**

## 07. 主界面设计与专项交互

统一界面、四根测试项目树、Frame 生命周期、Channel/Protocol/Graphics/CT3D 人工操作、ManualSession 与结果证据边界及完整验收，**统一查阅 [V1.1 设计与验收](TestCenter/CxVisual_TestCenter_统一主界面与工作区Frame注册——完整设计与验收_V1.1.md)**。本 README 不再重复主窗口布局及旧双入口方案。

## 08. 各体系测试边界

- **Foundation**：纯数据、几何/数值、配置基础、并发、日志、基础通信能力；禁止倒依赖测试。
- **Mechanism**：MEM 工程、MPM 属性、MTC 事务、MOL 定义、MOC 编译、MRK 运行、MDS 数据及 Connectivity/Visual 等公共机制；Studio/TestCenter 只调用既有对外契约。
- **Professional**：Automation、HMI、Vision、CT3D、Inspection、Simulation 的专业算法、设备/Provider、数据与安全语义；参考 `ReferTo/` 和七仓成熟源码前先核实 API、运行环境与兼容性。
- **Applications**：Studio 与 Runtime.Host 的真实装配、跨模块流程、WPF 行为和真实服务方式。TestCenter 不替它们制造另一份工程或执行引擎。

## 09. CI、人工测试与证据门禁

| 证据层次 | 什么可以认定 | 不能冒充 |
|---|---|---|
| S0 设计 | 需求、规范、用例/界面已设计 | 代码已实现 |
| S1 源码 | 类型、断言、UI/命令入口可定位 | 编译成功 |
| S2 自动执行 | 指定环境真实运行，退出码/断言可核对 | Windows 全量、GUI或实机全部成功 |
| S3 固定 SHA Windows | .NET48/x64 编译和相应 CI 门禁成功 | 未执行的 UI/长稳/现场 |
| S4 WPF/专业人机 | UIA 或人工操作步骤、截图、证据及版本可核对 | 未覆盖场景、设备联锁 |
| S5 实机 | 指定硬件/驱动/授权/联锁及场景现场验收 | 其他设备、其他版本通用放行 |

单独记录 **自动 UIA** 和 **人工签认**；有些 S4 项仅具备其中一项。禁止用 PASS 计数代替测试清单覆盖，禁止把 ManualSession 直接判为 Passed。CI 失败不得静默删除用例或停掉门禁以求绿。

**最小开发闭环**：确认最新 HEAD/Owner → 定位已有测试 → 写/修正功能 → 补充正常及失败反例 → 专项命令 → 必要默认回归与 TestCenter → Windows 同 SHA CI → 记录未验证边界与结果 → 中文主题集中提交（默认 `master`）。

## 10. 当前状态与后续工作

已存在 TestCenter NUnit/TCT、WPF Shell、AvalonDock、四根工作区注册及 Virtual P2P Channel 人工试验源码。**当前 Virtual P2P 不等于真实 TCP/UDP/Serial**；统一自动测试 Frame、人工 Protocol/Graphics/CT3D、报告与场景专项交互仍需根据真实代码及证据逐项实现与验收。具体模块差距及已知 Windows 编译阻断见 [V1.1 第 05～06 章](TestCenter/CxVisual_TestCenter_统一主界面与工作区Frame注册——完整设计与验收_V1.1.md)。

---
**维护说明**：本文件只维护 Test 目录的长期职责与入口；实施报告/CI 仍在 `DOCS/Testing/`，完整 TestCenter 合同只在 V4.1 主规格。对本草案中新的设计决策必须评审后更新权威规格，再进入实际代码开发。