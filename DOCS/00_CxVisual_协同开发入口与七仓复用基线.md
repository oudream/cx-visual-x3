# CxVisual 协同开发工作入口与七仓复用基线 V1.0

> **位置**：`cx-visual-x3/DOCS/00_CxVisual_协同开发入口与七仓复用基线.md`  
> **日期**：2026-10-14  
> **定位**：给新的 ChatGPT 对话、Codex、人类开发者提供最短、可验证、持续更新的工作入口；它是导航和事实基线，不替代详细规格、实际源码或构建结果。  
> **状态**：仓库组织和现有文档路径已核对；项目多年生产使用情况由项目负责人明确提供，实际部署清单/现场测试记录需逐项目查证。

## 0. 进仓库后先读这里（30 秒恢复上下文）

1. 阅读仓库根目录的 `AGENTS.md`、本文件、以及 `.agents/skills/cxvisual-engineering/SKILL.md`（后者主要给 Codex 的工程作业流程）。
2. 判断本次是「讨论定案」「源码审核」「代码实现」「迁移」「测试验收」中的哪一种。**不因阅读入口文件而擅自改代码、改依赖或扩展本轮任务范围**。
3. 以 `cx-visual-x3` 的当前分支、HEAD commit 和实际源文件作为**新平台现状**；以 `DOCS/` 最新批准规范作为**目标与 Owner 基线**；以另外六仓的实际实现作为**复用来源**。记录读取日期、仓库分支/commit。
4. 聚焦本轮指定的一个项目/子方面；只按需阅读相关规格 Sheet、真实代码、历史实现与测试，**不需每次通读所有大型文档**。
5. 任何结论标记证据等级：**用户明确事实 / 规格规划 / 代码声明 / 编译通过 / 自动测试通过 / 多项目生产实践 / 本次环境未核实**。这些等级不能彼此替代。

## 1. 七大代码仓库及不可混淆的身份

以下“开发经历/已用于实际项目”来自项目负责人 2026-10-14 说明，**不得因为当前仓库缺少 CI 记录就误称成熟代码为 Demo；也不得把历史现场运行事实直接当作新平台集成后的验收结果**。

| 仓库 | 身份、工程历史与主要能力 | 在 CxVisual 的使用原则 |
|---|---|---|
| [cx-visual-x3](https://github.com/oudream/cx-visual-x3) | **CxVisual 新架构唯一主开发仓库**；28 项目、F/M/P 三方面目标结构、WPF Studio/Runtime | 新设计、代码变更、文档、测试的主落点。当前 README 明确是 V1.0-preview：部分功能示例，不能误报为 135 子方面已实现 |
| [cx-visual-x2](https://github.com/oudream/cx-visual-x2) | **CxVisual 旧版**；以前的工程、界面、编辑、运行框架 | 迁移与行为对照来源；保持历史兼容需要时才做适配，不把旧 Graph/项目树不加区分地搬回新工程 |
| [cx-vision-x2](https://github.com/oudream/cx-vision-x2) | **生产软件资产**：CT 程序、在线 X-Ray 检测、离线 X-Ray 检测；负责人说明相关系统长期在多个项目使用 | 优先挖掘工业设备集成、检测、采集、CT 流程、日志、点检和真实业务规则；逐模块核对实际代码所在路径、DLL 与调用契约 |
| [gcl3](https://github.com/oudream/gcl3) | **生产软件资产**：分布式 ICS/SCADA；负责人投入一年多开发，已经多项目应用 | 分布式实时数据、终端/规约、通信、工程运行、调度、监控及故障处理；不能随意抹掉已验证的分布式运行边界 |
| [ccxx](https://github.com/oudream/ccxx/tree/master/ccxx) | **生产软件资产**：跨平台 C++ 基础库；负责人投入约一年开发，长期多项目使用 | 基础对象、线程/事件/定时器、系统/网络/共享内存等；优先保留原生成熟行为，通过明确 ABI/IPC/Adapter 集成，避免为了 C# 化全部重写 |
| [device_communicator](https://github.com/oudream/device_communicator) | **生产软件资产**：物联网通信平台；负责人投入一年多开发，长期多项目使用 | IoT/边缘设备管理、点表、通道、规约、守护、服务端集成；适合保留 Go 服务进程时，不强制搬成 C# |
| [cx-cct-x3](https://github.com/oudream/cx-cct-x3) | **生产软件资产**：CT 重建、三维处理与跨语言集成；负责人将除 CxVisual 外的既有系统整体归为多年多项目应用的成熟软件 | 复用 C++ 算法与 C ABI、共享内存、坐标/体数据契约；明确数组布局、空间坐标、内存所有权、版本和错误码 |

**铁律：生产成熟 ≠ 可不经审核直接移植；新平台预览 ≠ 旧项目质量低。** 迁移时兼顾对旧项目的兼容性与新平台的长期正确性。

## 2. 权威文档与入口顺序（仓库路径已核对）

1. `DOCS/01_CxVisual_平台灵魂与架构宪法_V1.0_Review.md`：平台理念与高层约束；“Review”表明仍有待裁决项。
2. `DOCS/02_CxVisual_01至05_统一规格基线_V4.9_Foundation_Mechanism_ProfessionalDomain_轻量黑底彩色边框.xlsx`：能力需求、对象及属性方法目标；**规划数量不代表已实现**。
3. `DOCS/03_CxVisual_三方面逐阶段 · 原结构归属完善 V2.01.xlsx`：三方面/子方面、来源、实施阶段和旧结构归属记录；注意文件名为“原结构归属完善”，不等同于所有未来归属问题已定案。
4. `DOCS/04_CxVisual Foundation与Mechanism 统一架构与对象API设计规格 V6.4.xlsx`：Foundation/Mechanism 项目边界、类型身份、Owner、API 候选及待核状态；讨论 F/M 项目时优先看所属 Sheet。
5. `DOCS/CxVisual_统一工程项目代码与资源规范_V1.0.md`：统一解决方案/目录/工程构建/WPF/SDK 资源与迁移规范。
6. `DOCS/ProjectCatalog.csv`、`DOCS/DirectoryLayout_V1.1.md`、`DOCS/Decisions/`、`DOCS/Migration/`、`DOCS/Testing/`：按任务读取具体 Owner、目录决策、迁移证据、实际验证报告。
7. `.agents/skills/cxvisual-engineering/SKILL.md`：Codex 执行规范；它不能代替真实源码或实际测试。

**配套文件**：《CxVisual_FMP_唯一类型体系_归属复用重构新增全量审核_V5.2.xlsx》用于唯一类型身份与复用/增构裁决；本次未在上述 `DOCS/` 根目录确认到它的确切仓内路径。需要时先搜索仓库/用户提供的文件，不要编造路径或当作已读。

**冲突处理**：当前用户明确指示与最新批准决议优先；目标归属/需求以相应最新规格为准；实际实现与 DLL/版本以源代码、构建文件及实体文件为准。出现冲突，列出差异和证据后裁决，不能直接静默选择一个版本覆盖另一个。


### Numerics 基础数值计算体系（F03/F04/F10/F13）

- [开发者需求、全类候选与架构 V1.0](Architecture/CxVisual_基础数值计算体系%23开发者全类与框架_V1.0.md)：一行/组合调用、52个**候选**类型槽位及 Owner、Math.NET 数值后端和实施阶段；不能把清单当作已实现数量。
- [测试验收矩阵 V1.0](Testing/CxVisual_基础数值计算体系%23测试验收矩阵_V1.0.md)：N01～N20 正反用例及固定SHA的 Windows 验收证据。F03插值/统计、F04 SVD最小二乘、F10移动平均/FFT已有实测；F13与其它规划不冒称实现。

### 通用三维视觉工作台（CT3D）——唯一总体架构入口

- **[《整体架构与 UML V2.0（R4收敛评审版）》](Architecture/CxVisual_专业视觉体系%23通用三维视觉工作台——整体架构与UML_V2.0.md)**：**唯一维护的整体总纲**；开头5分钟认知→六大域26项→实际F/M/P依赖/Owner→数据/Scene/算法/GPU→C01～C08接口合同→四条工业流程→26项状态与90条需求来源索引→CI/工业验收与ADR。**文档收敛不等于ADR批准或产品能力验收。**
- 真实性证据查 [ReferTo保真矩阵](Testing/CxVisual_通用三维视觉工作台%23ReferTo功能保真审计与验收矩阵_V1.0.md)、`DOCS/Migration/`、`DOCS/Implementation/` 与 [首个ChangeSet实装记录](Implementation/CxVisual_通用三维视觉工作台%23V2.0架构源码差距与渲染变化集首包_V1.0.md)。原速览仅保留跳转页，26项V0.1仅作历史快照，不再另维护状态表。

### 图元编辑规范（工程呈现化子体系）

- [图元编辑完整设计与验收 V1.2](Architecture/CxVisual_工程呈现化体系%23图元编辑——完整设计与验收_V1.2.md)：十四章、四附录、A～F 核心操作图；包含六类控制柄、场景投影、数学变换、固定/浮动视觉连接、Group/Symbol/剪贴板、Studio 使用合同及 GE-01～GE-15。**设计合同，不是已实现/已验收的宣告**。
- [图元体系架构与归属现状](Architecture/CxVisual_工程呈现化体系%23图元编辑——完整设计与验收_V1.2.md)：维护实际类归属、源码状态；具体实施/CI 见 Implementation/Testing。避免维护两份设计主规格。

### 已完成的七仓技术资产档案：01～03（源码证据及复用提案，不是正式定案）

| 档案 | 内容与应读时机 | 本档案引用的旧仓源码基线 |
|---|---|---|
| **[01_ccxx.md](Codebases/01_ccxx.md)** | C/C++基础类型、Channel、IO线程、Application/Timer、日志、描述及公共设施；开发Foundation/通信/线程机制时重点查阅 | `oudream/ccxx`：`26cecfb02e861ce6b821b33350493bac4793e997` |
| **[02_device_communicator.md](Codebases/02_device_communicator.md)** | IoT型号/设备、点表（attr/event/link）、Channel/Protocol/Communicator、实时数据、控制、Monitor/Daemon；审核M04/M09/M12/M15及Automation时重点查阅 | `oudream/device_communicator`：`18b5313d8c0c350f3a8cc723f57120cf9e551156` |
| **[03_gcl3.md](Codebases/03_gcl3.md)** | ICS/SCADA终端、规约、测点、PSM/Bus、共享内存、RTDB、触发、守护及现场部署；审核运行机制、消息和实时数据时重点查阅 | `oudream/gcl3`：`511ea4af40f0184686fda9b904254d9372019e2a` |

三份文档采用不同的研究重点，**均以事实与建议分离为原则**：源码事实/历史经验/设计提案/真实测试不得混同。按任务阅读相关档案，不要求每次全量读完；后续新增其他代码资产档案，应在此索引补入。

### Decision 统一决策与规划体系（V2.0正式架构；物理合并未实施）

**理念与目标**：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。Decision以约束为边界、目标为导向、策略为手段、证据为依据、反馈为校验，让六大专业的能力通过轻量声明/注册进入上下文编排、验证、方案选择、运行协同、同图调试、虚拟模拟及优化。**一份总纲、六份专题、裁决/迁移/验收三类工程文档；不复制现有Core/FCM/MFM/MOC/MRK/MDS或专业Owner。**

**权威架构（阅读顺序）**：
1. [Decision #0 总体架构与全类需求 V2.0](Architecture/CxVisual_统一决策与规划体系%230总体架构与全类需求_V2.0.md)：唯一总纲、三大根本判断、十大能力、八个使用价值域、六专业需求、完整使用链与状态。
2. [#1 对象合同与开发者API V2.0](Architecture/CxVisual_统一决策与规划体系%231对象合同与开发者API_V2.0.md)：最小SDK/纯Plan/三态结果/版本/SourceMap/执行交接，所有MDC签名拟议。
3. [#2 约束目标与策略 V2.0](Architecture/CxVisual_统一决策与规划体系%232约束目标与策略_V2.0.md)：约束来源、硬软目标、冲突/未知、可行域/Pareto、选择/执行策略与算法验收。
4. [#3 专业能力声明与注册 V2.0](Architecture/CxVisual_统一决策与规划体系%233专业能力声明与注册_V2.0.md)：C#注解/Fluent/Descriptor、五类模块、唯一Schema/目录、L0～L3渐进接入及六专业合同。
5. [#4 上下文编排与智能引导 V2.0](Architecture/CxVisual_统一决策与规划体系%234上下文编排与智能引导_V2.0.md)：Symbol/Content/Capability过滤、端口连线/参数引导、即时校验、情景路径预演与Studio/SDK。
6. [#5 运行调试模拟与反馈 V2.0](Architecture/CxVisual_统一决策与规划体系%235运行调试模拟与反馈_V2.0.md)：MRK/MDS/Trace/SourceMap/断点、静态与Virtual/Replay/HIL分级、故障诊断和版本化改进。
7. [#6 专业应用与完整案例 V2.0](Architecture/CxVisual_统一决策与规划体系%236专业应用与完整案例_V2.0.md)：Vision、CT3D、Automation、Inspection、HMI、Simulation 及跨专业产线端到端案例。

**架构反推与OOP/GoF/AOP审评（2026-10-29）**：[Decision V2.0全类反推与OOP/GoF/AOP架构审评 V1.0](Architecture/CxVisual_统一决策与规划体系%23V2全类反推与OOP-GoF-AOP架构审评_V1.0.md)——以六专业真实任务与七问使用链反审#0～#6，列出ARC-01～12架构缺口、REV-01～12反向测试及P0/P1门禁；**性质为审评报告，不是第八份权威技术规格或已实装证据**。已将关键结论回填各#0～#6，并与迁移/测试矩阵关联；当前若未解除MOC→MRK依赖、未实测SDK/CI与设备HIL，不得称为工业就绪。

**配套工程权威**：
- [Decision 融合与Owner裁决 V1.0（含V2衔接）](Decisions/CxVisual_Mechanism_Decision_项目融合与归属裁决_V1.0.md)：项目正式名称与无环依赖归属。
- [Logic+Orchestration融合/Runtime解耦迁移 V1.0（含V2门禁）](Migration/CxVisual_Decision_Logic与Orchestration融合及Runtime解耦_V1.0.md)：逐文件类型和P0～P5迁移，目标28→27、Mechanism9→8。
- [Decision功能与迁移验收矩阵 V1.0（含V2验收索引）](Testing/CxVisual_Decision_功能与迁移验收矩阵_V1.0.md)：D-T、M、REG/CST/AUT/RUN/CASE/UX的正反例、性能与证据等级。



**Decision V2.0 最新实施覆盖与未完成验收（2026-10-29）**：[Decision实施覆盖与未完成验收台账](Implementation/CxVisual_Decision_V2_实施覆盖与未完成验收台账_20261029.md)；汇总D01～D10真实源码/固定SHA/Windows运行记录，区分已自动测试的静态能力、局部Studio集成、尚未开发的安全策略/纯Plan/模拟联动及27项目融合P0。原[#0～#6](Architecture/CxVisual_统一决策与规划体系%230总体架构与全类需求_V2.0.md)仍为目标设计权威；台账只是动态事实入口，不替代测试母表或设备验收。

**Decision首批源码实装（2026-10-29）**：[P0离线能力发现与静态约束筛选实现验收](Implementation/CxVisual_Decision_P0离线能力发现与静态约束筛选_实现验收_V1.0.md)：沿用现有MOC目录/MPC端口，增加无Runtime Resolver的离线发现、`AvailabilityKnown`和`MDCDecisionService`静态端口三态/候选筛选，新增ArchitectureSmoke `--decision`测试。当前仅是**过渡源码切片**，未迁移物理工程、未完全解耦MOC→MRK，也未实现Studio界面或复杂优化；Windows CI证据以实际run为准。

**历史与现状**：原[#0 V1.0](Architecture/CxVisual_统一决策与规划体系%230总体架构与10大能力_V1.0.md)、[#1 V1.0](Architecture/CxVisual_统一决策与规划体系%231对象合同与开发者API_V1.0.md)仅保留历史可追溯，不再双线维护。最新设计**不等于源码交付**：目前仍是 `Mechanism/Logic`、`Mechanism/Orchestration`、`Mechanism/Runtime` 既有工程，MOC→MRK编译时依赖未断，Decision新项目/新MDC接口/统一注解/约束求解尚未实施；只有真实CI与现场证据可证明功能与安全。对于DOCS/02～04 XLSX需要实施前逐Sheet比对Owner/签名，不得将当前文本性摘要冒称表格审计。

### 运行体系专项讨论稿（六角色期望 → 统一能力 → 执行模型）

- **[统一运行体系综合设计讨论稿 V0.1](Architecture/过渡/CxVisual_运行体系_六角色期望与并发异步编排执行_综合设计讨论稿_V0.1.md)**：整合R1～R6六类用户对代码SDK、专业Provider、编排、调试、操作和维护的期望；E01～E18需求、统一任务/流程/上下文/执行组模型、非阻塞异步、队列/背压、归并/资源/生命周期、三进程与Vision/IoT/CT/AI验收。**目前属于可修正的设计讨论稿，不是正式Owner/API定案，更不是运行代码已实现证明。**
- 当前收敛的实现维护方向是：Foundation.Infrastructure / Concurrency（F19）与 Mechanism.Runtime / Execution（统一执行引擎）两个集中目录；其他Orchestration/Data/Connectivity/Professional仍保有本职契约和定义，不能因此产生第二套执行器。

### 运行体系：测试驱动架构与首个代码闭环

- **[统一执行体系TDD矩阵（持续更新V0.6.1）](Testing/UnifiedExecution_TDD_Matrix_V0.1.md)**：从R1～R6、E01～E18倒推出T01～T20的测试用例、首批F19/Runtime实际类与接口、已覆盖和未覆盖项。首批T01～T10及后续资源租约、请求关联、分支归并、子流程、软件Plan版本等测试接入既有FoundationSmoke/ArchitectureSmoke；Windows CI编译及通过状态以对应提交的实际日志为准。已实现的进程内资源租约、已知计划子流程、软件PlanSlot与正式发布、跨进程/跨进程资源仲裁、实机安全严格区分；后者明确保留为后续测试，不把设计稿当作已运行实现。

- **[统一运行内核实现与验收 V0.6.1](Testing/CxVisual_统一运行体系内核_实现与验收_V0.3.md)**：只围绕F19 Concurrency及Runtime.Execution，完整列出当前可执行基础API、事件与定时触发、条件/循环、调试暂停、优先级、资源等待、数据租约、停止排空和测试入口；明确尚未交付的跨进程、持久恢复及实机安全。

- **[统一运行体系六角色差距审计与内核增构V0.6.1](Architecture/过渡/CxVisual_运行体系_六角色差距审计与内核增构_V0.5.md)**：重读六角色原文，比较E01～E18与最新源码；新增执行组、类型端口、流程身份与会话、真实条件Join、能力计划编译、两层Data作用域、持久Journal、按字节背压与专用SDK线程；附T24～T38实测进展及剩余Owner职责。设计可修正，完成度以Windows CI为准。

### 统一工程组织体系：完整架构与开发使用

- [H01实现与验收](Testing/CxVisual_工程组织体系_H01_实现与验收_V1.0.md)：用户已确认MRCVisualItem共同基类；只记录真实模型/编辑/保存加载及兼容回归，完整H03～H07未完成。

- **当前主规格**：[工程组织体系完整架构设计与开发使用指南 V1.2](Architecture/CxVisual_工程组织体系——完整架构设计与开发使用指南_V1.2.md)：保持六角色、八案例、28项需求与H00～H07历史，正式将MEMContent/MOL逻辑定义归Core唯一真源，MOC编译、MRK执行、MDS数据、Studio投影分工及最新正反例验收；旧[V1.1](Architecture/CxVisual_工程组织体系——完整架构设计与开发使用指南_V1.1.md)保留可追溯。
- [x2 → x3 工程组织复用映射 V1.1](Architecture/过渡/CxVisual_统一工程组织体系_x2至x3复用映射_V1.1.md)：固定x2 master源码，R01～R50关键属性/方法/真实测试与复用决策，不把静态审查当成已移植/已复跑。
- [H00～H07 分阶段实施与验收指导 V1.1](Architecture/过渡/CxVisual_统一工程组织体系_H00至H07分阶段实施与验收指导_V1.1.md)：各阶段类型/Owner、依赖/顺序、案例/TDD/客观验收；H01必须真实保存重开，H06/H07保持原Visual/Studio/运行/专业Owner。
- [H00 架构待裁决清单 V1.1](Architecture/过渡/CxVisual_统一工程组织体系_架构待裁决清单_V1.1.md)：D01～D04已于2026-10-17由用户明确确认，采用MRCVisualItem为共同视觉基类、MEMContent/MEMSymbol为资源定义。H01已授权实施；D05～D07扩展不冒充已验收。

## 3. 新平台总边界（恢复上下文用）

- 顶层架构：**Foundation / Mechanism / Professional Domain**；当前目标是 **22 + 40 + 73 = 135 个子方面**。
- 28 个项目：Foundation 8、Mechanism 9、Professional 6、Applications 2、Intelligence 1、Tests 2。解决方案 `CxVisual.Unified.sln` 与 `CxVisual.Foundation.sln`。
- 新平台基线：Windows x64、传统 `.csproj`、.NET Framework 4.8、WPF Studio；真实构建以当前目标环境验证为准。
- 唯一工程定义主线：`Project / Page / Content / Symbol / Group / Element`；统一 CxNode 和可视化/逻辑/数据/协同四方面。**不再建立第二套持久化 Graph 工程树**。
- Foundation 是无 UI/无设备/无工程运行状态的可复用基础；Mechanism 负责公共工程/变量/编辑/编排/执行等机制；Professional 负责六个专业 Workbench。Studio 负责装配与 WPF 呈现，Runtime Host 负责独立执行。
- 严禁循环依赖、隐式跨专业反向引用、重复权威对象、为了接口数量创建空壳。工业设备会话/运行服务不为“静态优先”而强制变全局静态单例。
- **先保留功能和业务正确性，再处理模块归属和代码风格**。原生算法、工业规约或成熟服务可以继续独立运行，不要求物理上塞入 28 个 C# 项目。
- 文档中“原有”“待查重”“规划候选”“静态检查通过”均不代表当前新主仓已达到运行闭环。

## 4. 各项目聊天开发的统一过程

每轮只聚焦一个约定范围的项目/子方面，严格按以下顺序：

1. **明确目标**：本轮是讨论、定案还是实现；指出主仓项目路径、子方面 ID、预期专业使用场景。
2. **源码现状**：读取新主仓的 `.csproj`、实际类型、方法、引用、测试；列出与规格不一致之处。
3. **七仓复用审计**：按功能搜索另外六仓（允许零结果），记录仓库、路径、具体类型/函数、分支/commit；明确是否适合直接复用、封装、适配、保持外部服务或新增。
4. **对象 Owner 与契约**：逐类裁决唯一归属；明确属性、完整签名、返回/异常、线程、单位/坐标、生命周期、序列化、版本兼容、性能与安全门禁。
5. **实施决策**：先提供清晰可审查的结论，区分“本轮确认”与“待讨论”。未收到代码任务时，不随意推送重构。
6. **实现与测试**：代码尽量一次闭环，包含正例/异常/边界/兼容迁移测试；可用环境运行编译和测试后附日志及命令。没有 Windows/MSBuild/真实设备则如实标注“未验证”。
7. **留下可复用证据**：将关键决议写入 `DOCS/Decisions/`，旧新 API/Owner 映射写入 `DOCS/Migration/`，测试结果写入 `DOCS/Testing/`；必要时更新本入口的事实与导航。

### 每个项目的建议输出（便于阅读）

- **项目定位**：职责、依赖、明确排除项、子方面与核心专业用例；
- **现状核验**：已有实现/成熟复用/待补齐/需要迁移；
- **对象/API 清单**：唯一 Owner、类/接口/枚举、具体属性/签名/算法；
- **设计裁决**：冲突、退化情况、兼容性与优先级；
- **实施与验收**：目录变更、编译和测试证据、剩余风险与下一步。

## 5. 第一个项目：CxVisual.Foundation.Geometry

- 目标：F05 二维几何、F06 三维几何、F07 坐标框架与空间变换。
- 当前主仓位置：`Foundation/Geometry/`；当前已有 `CxPoint2D.cs`、`CxRect2D.cs`、`CxAffine2D.cs` 和 `CxVisual.Foundation.Geometry.csproj`。这些是新平台示例性基础实现，不等同于 F05/F06/F07 完整落地。
- 优先核对：V6.4 Geometry Sheet、V4.9基础几何对象、旧 `cx-visual-x2`／旧CxVisual 2D几何逻辑，以及 `cx-cct-x3/module_api/cct_3d_common.h` 的 C ABI 点/向量/变换/坐标空间约定。
- 特别注意：`Point2` vs `Point2D`、`Rect2` vs `Bounds2D` 的权威统一；依赖 `ElementModel` 的 Quadtree 不能直接塞入纯 Foundation；F03 Numerics 的矩阵/向量 Owner 与 F12 Volume 的网格 Owner 不得重复。
- 目前讨论中的裁决尚需最终确认；**不要把此处的建议当作已批准的迁移计划**。

## 6. 长期维护及“即看即所得”的使用方式

- 每次讨论架构定案时，记录日期和项目 Owner；有新批准规格时更新“版本与入口”，旧规格保留追溯。
- 每次任务开始核对 Git HEAD，不假设 GitHub 代码与上一次聊天时相同；特别区分 `cx-visual-x2` 旧代码和 `cx-visual-x3` 新主仓。
- 这个文件只负责**长期稳定事实 + 导航 + 工作步骤**，详细对象清单仍放四大规格和项目级裁决，避免第二套不一致的规格表。
- 对现有产品沉淀，应以“真实应用的行为与兼容性”为重要验收素材，但不能凭多年运行事实跳过本次新平台编译、协议兼容与集成验收。
- GitHub 仓库文件**不会保证在每一个新的 ChatGPT 聊天中自动被读取**。启动新聊天时可直接发以下一句话，请助手先实时读取再工作。

> **启动指令**：请读取 `https://github.com/oudream/cx-visual-x3/blob/master/AGENTS.md` 和 `https://github.com/oudream/cx-visual-x3/blob/master/DOCS/00_CxVisual_协同开发入口与七仓复用基线.md`，并按任务查看四大文档与相关七仓最新源码。接下来继续审核／开发 `【填写项目名】`，先给出现状与差距，再逐类定案和实施。

- **项目未发布期间的Git工作方式（用户2026-10-15明确要求）**：不创建开发分支，默认直接在 `master` 修改、运行CI并提交；所有新提交的标题和正文使用中文，少量、主题清晰且便于审阅。失败在master继续修复验证；不强推改写已经共享的旧提交。只有用户明确要求或仓库保护强制时才例外。

## 7. 本文件维护规则

由项目负责人或经其同意的协同开发过程维护。**用户长期事实**、**仓库可核实事实**、**设计决定**、**新版本测试结果** 分开描述；如果仍存在疑问，就保留“待确认”而不是写成已完成。修改本文件后，检查根目录 `AGENTS.md` 指向此入口的链接是否仍有效。**新增、移动、重命名或实质更新 `DOCS/Codebases/` 专项档案时，同步检查 `README.md`、`AGENTS.md` 和本文件的导航，按需更新链接、概述、源码基线；尽量与专项文档形成同一次小提交。** 文档中的设计建议仍然是可调整提案，不应在入口写成定案。

---

**V1.0 初版说明（2026-10-14）**：依据用户提供的七仓角色、工程经验及目前可读取的 `cx-visual-x3` README、AGENTS、SKILL 和 DOCS 目录建立；本次未宣称重新编译/测试七仓。
## 统一配置体系主文档（V2.4）

- [V2.4 完整统一配置体系设计与验收](Implementation/CxVisual_统一配置体系_完整设计与验收_V2.4.md)：需求与 API、六角色、27 组职责、24 项需求、12 案例、四层通用自动配置界面。
- [V2.4 源码审查与阶段 A—D 实施计划](Implementation/CxVisual_统一配置体系_V2.4_源码审查与分阶段实施计划.md)：按层级/Schema 正确性、安全、开发者体验、WPF、性能逐组闭环。当前唯一主规范为 V2.4，旧 V2.2/V2.3 保留历史证据，不重复建立配置框架。
- 实现 Owner 仅为 `Foundation/Data/Configuration` 与 `Mechanism/Data/Configuration`；WPF 仅承载编辑显示，不产生第二配置真源。

- [V2.4 阶段 A 实施与验收矩阵](Implementation/CxVisual_统一配置体系_V2.4_阶段A实施与验收矩阵.md)：按实际提交与测试追踪，不将 UI/实机/远端能力虚报已完成。

- [统一配置 V2.4 阶段 B 实现与验收](Implementation/CxVisual_统一配置体系_V2.4_阶段B开发者API与动态编辑验收矩阵.md)：类型安全原子组、动态集合操作与条目、真实 CI 证据、阶段 C/D 限制。

- [统一配置体系 V2.4 阶段 C/D 实施与复核验收](Implementation/CxVisual_统一配置体系_V2.4_阶段C与D实施和复核验收矩阵.md)：十二项 UI、独立 Windows D 压测、CI 证据和未验证边界。


## 统一功能模块体系 V1.2（2026-10-23）

- [五类模块与设备驱动架构裁决 V1.2](Architecture/CxVisual_统一功能模块体系——五类模块与设备驱动架构裁决_V1.2.md)：原四类扩为 Connector/Driver/Compute/Simulation/Service；默认一组接入，Advanced 才允许多 Channel/Protocol；TcpServer/UdpServer 属于一对多父通道，RS-485 多站号属于 Protocol 点表；设备 SDK/采图归专业 Driver。
- 本次变化仅增加既有 Mechanism.Core 的 Driver 正式 FCM 配置及对应 TestCenter/ArchitectureSmoke；不新增项目、第二配置库、Channel Adapter、任务调度器或消息/数据仓库。
- **证据分级**：配置与源码可按 Windows CI 验收；真实 TCP/UDP Server、Modbus RTU 多站和 X-Ray 厂商采图需要正式驱动迁入与 HIL，不以配置存在冒充设备通过。

## 统一消息中心 双项目 V1.7（2026-10-25）

- [统一消息中心完整设计与全类API V1.7](Architecture/CxVisual_公共机制体系%23消息中心——完整设计与全类API_V1.7.md)：对照MQTT Topic与进程内Queue/Shared/QoS/Retain/Request-Reply语义，逐类列明Owner、对象关系、真实API、异常与线程模型。
- [统一消息中心实施与Windows验收台账 V1.7](Implementation/CxVisual_公共机制体系%23消息中心——实施与Windows验收台账_V1.7.md)：源码落位、运行命令、可用与未实现边界、CI证据。
- [统一消息中心功能并发异常测试矩阵 V1.7](Testing/CxVisual_公共机制体系%23消息中心——功能并发异常验收矩阵_V1.7.md)：55项分级候选验收，V1.3明确Foundation.Data和Mechanism.Data两个既有项目归属，同时覆盖有界ThreadPool分发、参数快照、Codec和延迟指标；源码单元/并发、UI及性能分开记录。
- **实际Owner**：M36位于 Mechanism.Data；当前P0不需要Broker，不能代替M09变量/MDS数据Owner/M12命令或MRK执行。消息中心发布订阅的真实验收以具体提交的Windows CI为准。

## 统一命令中心 双项目归属（2026-10-24）

- [命令中心双项目Owner裁决 V1.0](Decisions/CxVisual_统一命令中心双项目归属裁决_V1.0.md)：命令基础定义、参数Schema、快照统一在 `Foundation.Data.Commands`；命令运行机制、MDS绑定适配统一在 `Mechanism.Runtime.Commands`。原Logic/MCE及Mechanism.Data命令专属文件已物理迁出，不建立额外项目。
- [命令中心实现与验收](Implementation/CxVisual_公共机制体系%23命令中心——进程内实现与验收_V1.0.md)：进程内实现、调用示例、Windows专项验收与明确未完成的跨进程及硬件边界。
- [统一命令中心完整类架构与功能验收 V1.0](Architecture/CxVisual_统一命令中心——完整类架构与功能验收_V1.0.md)：统一类图、时序、两个项目归属、真实API、设备/算法/流程用法及原60项需求的实现/待验收对应表，禁止以源码存在冒充实机或远程验收。

## 统一交互机制 V1.5（2026-10-24）

- [四大能力与双层总线完整设计及验收 V1.5](Architecture/CxVisual_公共机制体系%23统一交互——四大能力与双层总线完整设计及验收_V1.5.md)：变量高速、消息轻巧、命令受控执行、运行数据与大数据；进程内逻辑总线先行，进程外 MQTT/SHM/文件服务按需桥接；双寻址、分层订阅、Owner、反馈语义；**V1.3 明确 MQTT/IoT 风格消息/命令；V1.4 追加41个对象职责角色及OO/GoF/AOP、管理者和流转生命周期完整设计（不意味着新增41个类）；**V1.5 定案消息 Topic、Payload 四类、MQTT 原生 `+/#` 过滤，以及命令参数范围、单位和默认超时的源码契约**。
- [进程内总线分阶段实施与源码台账 V1.3](Implementation/CxVisual_公共机制体系%23统一交互——进程内总线分阶段实施与源码台账_V1.3.md)：P0 各阶段源码对照、已实现与未实现差距，不把接口候选当已交付。
- [TestCenter与Windows验收矩阵 V1.4](Testing/CxVisual_公共机制体系%23统一交互——TestCenter与Windows验收矩阵_V1.4.md)：正反例、并发和可靠性测试计划，现为 139 条候选验收；尚未声明测试已通过。
- [V1.1审查与V1.2修订报告](Architecture/CxVisual_公共机制体系%23统一交互——V1.1文档审查与V1.2修订报告.md)：历史问题、修订原因及待复核事项。

> 当前文档是设计与实施基线，尚需结合 DOCS/02～04 细核 Owner/API；不代表进程内功能、跨进程通信或现场设备验证已完成。


## 基础图像与体数据成熟源码迁移（2026-10-25）

- [FIM/FVO/MVI成熟源码复用与OOP-GoF-AOP实施规范 V1.0](Architecture/CxVisual_基础图像与体数据体系%23成熟源码复用与OOP-GoF-AOP实施规范_V1.0.md)：全局Owner、前缀、HALCON/OpenCV/CommonInterfaces通用算法提取、GoF/AOP门禁、TCT双Viewer路线。
- [FIM/MVI首批实现与Windows验收台账 V1.0](Implementation/CxVisual_基础图像体系%23FIM-MVI首批迁移实现与Windows验收台账_V1.0.md)：`60849f9`、`1b1eafe`、`3d9be3b` 真实源码，`--imaging`和默认回归，未通过项不冒充成功。
- [S0六工程213文件逐文件迁移台账](Migration/CxVisual_基础图像与体数据体系%23成熟源码迁移——S0主体归属与逐文件台账_V1.0.md)：ReferTo只读，历史快照，不包含额外CommonInterfaces文件。

- [二维视觉工作台 OOP/GoF/AOP UML实施设计 V1.1](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——OOP-GoF-AOP与UML实施设计_V1.1.md)：基于FIM/MVI/MPW/PVC、现有MEM/MRC的六张UML及真实Owner门禁；首轮四点State、Overlay原子批次、AOP边界日志的代码/待验收分级；不把候选接口当成现成能力。
- [二维工业视觉工作台完整功能架构与实施验收 V1.0](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——完整功能架构与实施验收_V1.0.md)：按用户60项需求跟踪FIM/MVI/MPW/PVC/TCT实际实现、旧类功能迁移、UI/API/Windows CI证据及剩余P2～P6工作包；当前首批WPF可运行链不等于完整工业交付。

- [通用二维工业视觉工作台P1真实Windows验收与缺口](Testing/CxVisual_专业视觉体系%23通用二维视觉工作台——P1真实Windows验收与缺口_V1.0.md)：对应`98b664f`、Imaging Windows CI run 40206188476 的真实全编译/专项/默认回归/WPF Window+PNG回环证据，区分未验收的完整TCT UIA、厂商SDK和工业性能。

- [旧ROI与标注功能迁移及实测差距 V1.1](Implementation/CxVisual_专业视觉体系%23二维图窗ROI标注旧功能迁移与实测差距_V1.1.md)：逐旧文件映射Overlay模型/4图层/鼠标绘制拾取/删除/WPF截图与最新CI，对17种旧分类区分已支持交互和待迁移部分。

- [二维视觉工作台P2显示/分析/旧功能迁移实证 V1.0](Implementation/CxVisual_专业视觉体系%23二维视觉工作台P2显示变换与成熟功能迁移实证_V1.0.md)：核对旧ImageDisplay/Transform/Comparison、直方图/灰度剖面/标尺/Polygon/Angle到FIM-MVI-MPW-TCT真实代码与Windows专项运行，区分尚未完成的SideBySide、完整编辑与工业验收。

- [二维视觉工作台TestCenter界面优化与真实Windows验收 V1.0](Implementation/CxVisual_专业视觉体系%23二维视觉工作台TestCenter界面优化与Windows验收_V1.0.md)：第6张图对照前五张成熟图窗，单排WPF矢量图标、上下10页签、可调侧栏、TestCenter辅助Inspector按专业工作区收起/切回恢复；以Imaging Windows #40259838886通过记录和两张CI截图为证，完整TestCenter UIA超时单列未解决。

- [二维工业视觉工作台 P2 功能恢复与 Windows 实测 V1.0](Implementation/CxVisual_专业视觉体系%23二维视觉工作台P2功能恢复与实测_V1.0.md)：针对旧第6图功能缺口，新增水平/垂直/直径/矩形及多边形面积测量、滑动比较、剖面线、灰阶滑块、可取消大图统计和ROI/测量CSV，附实测#40262549868及未完成差距。

- [TestCenter自动测试工作台统一Frame与测试控制台实装验收 V1.0](Implementation/CxVisual_TestCenter%23自动测试工作台——统一Frame与测试控制台实现验收_V1.0.md)：NUnit/TCT计划和ArchitectureSmoke共用同一中央可调宽测试控制台，专业工作台默认全宽；旧全局Inspector退场、旧布局保留并提示重存，验证需按对应Windows CI判定。


## 数据与交互体系——五份统一架构主设计（2026-10-26）

> **正式名称**：CxVisual 数据与交互体系（Unified Data & Interaction System）。现有Foundation/Mechanism/Professional项目归属不变，不新增程序集。原「公共机制体系#统一交互」及独立命令/数据旧命名文件保留历史，以下五份为统一权威导航；实施与测试记录仍以所在目录和固定SHA的实际结果为准。

- [#0 总体架构 V1.5 ](Architecture/CxVisual_数据与交互体系%230总体架构——完整设计与验收_V1.5.md)
- [#1 变量体系 V1.1 ](Architecture/CxVisual_数据与交互体系%231变量体系——完整设计与验收_V1.1.md)
- [#2 运行时数据 V3.0 ](Architecture/CxVisual_数据与交互体系%232运行时数据体系——完整设计与验收_V3.0.md)
- [#3 消息中心 V1.7 ](Architecture/CxVisual_数据与交互体系%233消息中心——完整设计与验收_V1.7.md)
- [#4 命令中心 V1.0 ](Architecture/CxVisual_数据与交互体系%234命令中心——完整设计与验收_V1.0.md)

**完成度分级**：#0继承四大能力与双层总线V1.5；#1新增独立变量主规格并明确当前MDSVariableTable/Registry已存在与计划扩展的差别；#2保留运行时数据V3.0全部对象与双角色用例；#3保留消息中心V1.7全部API；#4保留命令中心V1.0类职责、IPС/验收边界。规格不等于源码实现、最新Windows测试或实机验收。



## F17 Channel 唯一归属裁决（2026-10-27）

- 新的正式 Channel 全部位于 `Foundation/Transport/Channels/`，统一 `CxVisual.Foundation.Transport` 命名空间和 `FTR*`、`IFTR*` 类型；不再由 `Mechanism.Connectivity` 编译 `MCC*` 通道类型。
- Connector、Protocol、PointTable、DataMapping 继续位于 `Mechanism.Connectivity`；严格一个 Connector 对应一个 Channel 和一个 Protocol，原有项目数不变。
- [完整Channel 14方面审计与实施基线](Architecture/CxVisual_F17_Channel完整体系_能力审计与实施基线_V1.0.md)：覆盖用户60项要求、全部通道、旧仓源码证据与完整缺口；不把TCP单包进展当作整包完成。
- [P0独立接收与有界大数据实现](Implementation/CxVisual_F17_Channel_P0独立接收与有界大数据_实施记录_V1.0.md)、[对应验收矩阵](Testing/CxVisual_F17_Channel_P0大数据与线程模型_验收矩阵_V1.0.md)：TCP独立接收、同步发送、有界租约、异步发送预算、40MiB/2GiB双向实际数据完整性与TestCenter正式入口；其它通道完整性与现场HIL分开验收。
- [Factory与Provider实施验收](Implementation/CxVisual_F17_Channel_Factory与Provider_实施与验收_V1.0.md)：正式内置创建、Host作用域注册、弱键创建凭据、FCM扩展设置与原Connector/Protocol能力集成；创建不代表已打开设备。
- [UDP独立接收与组播实施验收](Implementation/CxVisual_F17_Channel_UDP独立接收与组播_实施与验收_V1.0.md)：三类UDP复用有界租约、同步底层与独立接收；报文边界/来源、截断/满额诊断、空闲代次与真实本机组播；网络丢包及多网卡现场边界单独验收。
- [NamedPipe入场饱和与读者结算修复](Implementation/CxVisual_F17_NamedPipe入场饱和与读者结算_修复验收_V1.0.md)：创建OS实例前限流、满额不杀Listener、历史完成Task剔除及关闭末次Accept结算；独立接收/大数据模型另行闭环。
- [NamedPipe独立接收与跨进程体数据实现](Implementation/CxVisual_F17_NamedPipe独立接收与跨进程大数据_实施验收_V1.0.md)、[验收矩阵](Testing/CxVisual_F17_NamedPipe跨进程大数据_验收矩阵_V1.0.md)：固定接收线程、12对端写入隔离、双向各2GiB、池化租约及终止通知预算；承接前项入场修复，不代表完整Channel目标结束。
- 依据：[Channel 迁移及唯一 Owner 裁决](Decisions/CxVisual_F17_Channel下沉与唯一归属裁决_V1.0.md)；[旧新类型兼容映射](Migration/CxVisual_Channel_MCC至FTR迁移映射_V1.0.md)；[本轮验收证据台账](Testing/CxVisual_F17_Channel迁移_源码与Windows验收台账_V1.0.md)。上述裁决覆盖旧规范中 M13～M15 拥有 Channel 具体类的条款，但不改历史文件本身的日期和历史事实。

## 统一功能模块体系：五大模块六份正式规格（2026-10-28）

- [#0 总体架构 V2.0](Architecture/CxVisual_统一功能模块体系%230总体架构——完整设计与验收_V2.0.md)——共性、五类边界、能力发现、使用与统一验收。
- [#1 Connector V1.0](Architecture/CxVisual_统一功能模块体系%231Connector——连接器模块完整设计与验收_V1.0.md)——严格一个 Connector 对应一个 Channel 和一个 Protocol；本轮未改写。
- [#2 Driver V1.1](Architecture/CxVisual_统一功能模块体系%232Driver——驱动模块完整设计与验收_V1.1.md)——设备 Provider、动作确认、安全与 HIL。
- [#3 Compute V1.1](Architecture/CxVisual_统一功能模块体系%233Compute——计算模块完整设计与验收_V1.1.md)——Numerics、图像、AI、CT/3D、强类型/质量与性能。
- [#4 Simulation V1.1](Architecture/CxVisual_统一功能模块体系%234Simulation——仿真模块完整设计与验收_V1.1.md)——模型、时钟、故障、Replay、虚实与确定性。
- [#5 Service V1.1](Architecture/CxVisual_统一功能模块体系%235Service——服务模块完整设计与验收_V1.1.md)——服务响应、Ready、运行治理与集成。


- [功能模块中心与三入口统一体验 V1.0](Architecture/CxVisual_统一功能模块体系——功能模块中心与三入口统一体验完整设计及验收_V1.0.md)——Developer API、Studio、TestCenter 共用 MFM/MOC/MRK/FCM/MHS/MPW 的能力发现、配置、使用、诊断及 MC-A01～A32 专项验收；当前为设计目标，不代表完整界面和 HIL 已实现。

四篇 V1.0 历史原文保留，四篇 V1.1 分别继承全部正文和编号并增补执行合同及 X 系列专项测试。F17 Channel 后继 Owner/命令中心双项目 Owner/统一交互 V1.5 优先于过时历史表述。文档是目标，不能替代 Windows 编译、TestCenter、真实设备 HIL 或工业长稳。

- [Module Center P0共享入口源码与Windows验收台账 V1.0](Implementation/CxVisual_统一功能模块体系%23ModuleCenter_P0共享入口源码实施与Windows验收台账_V1.0.md)：固定SHA真实提交、公共控件、Studio/TestCenter入口、CI结果与未完成Provider/HIL；按证据分级，不将设计和部分G1冒充工业完成。

## 通用二维视觉工作台——V2.0全类与UML主设计（2026-10-28）

> **用户已确认的设计基线：** 3份核心主文档、6份专题详细设计、实施与测试台账共**11份**。六大能力群、26个一级类、260个二级稳定能力ID（V2D-XX.YY），**26张关键UML关系图**（3用例关系、3组件、8类、4状态、6时序、2部署）。以上是设计范围和目标追踪，不是已实现260项或26张图已通过CI。本轮文档提交未修改C#源码；旧V1.0功能审计和V1.1图元UML保留历史证据，不重复定义第二套Owner/工程/执行/Undo。

**权威阅读顺序：**
- [00总体架构与设计总纲 V2.0](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——00总体架构与设计总纲_V2.0.md)：产品定位、平台宪法和Owner/生命周期。 
- [01全类能力目录与功能契约 V2.0](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——01全类能力目录与功能契约_V2.0.md)：260项完整目标能力及稳定编号。 
- [02UML总体模型与设计模式 V2.0](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——02UML总体模型与设计模式_V2.0.md)：UML对象关系、模式、真实E/目标N状态。 
- [03图像数据与生命周期 V2.0](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——03图像数据与生命周期_V2.0.md)：该专题的对象合同、线程/资源/失败边界与反证。 
- [04视觉场景与交互工具 V2.0](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——04视觉场景与交互工具_V2.0.md)：该专题的对象合同、线程/资源/失败边界与反证。 
- [05算法能力与专业流程 V2.0](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——05算法能力与专业流程_V2.0.md)：该专题的对象合同、线程/资源/失败边界与反证。 
- [06结果测量与工程资源 V2.0](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——06结果测量与工程资源_V2.0.md)：该专题的对象合同、线程/资源/失败边界与反证。 
- [07运行时与跨专业协作 V2.0](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——07运行时与跨专业协作_V2.0.md)：该专题的对象合同、线程/资源/失败边界与反证。 
- [08统一工作台与开发者API V2.0](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——08统一工作台与开发者API_V2.0.md)：该专题的对象合同、线程/资源/失败边界与反证。 
- [V2.0实施源码与迁移台账](Implementation/CxVisual_专业视觉体系%23通用二维视觉工作台——V2.0实施源码与迁移台账.md)：Owner、真实源码和ReferTo保真、工作包和SHA；不存在的对象不能记为已编码。
- [V2.0全类验收与测试矩阵](Testing/CxVisual_专业视觉体系%23通用二维视觉工作台——V2.0全类验收与测试矩阵.md)：G0—G6证据级别、26类矩阵、8个端到端Case、数值/WPF/性能/SDK现场测试门禁。

**约束及现况：** 以最新master/真实源码/匹配SHA的Windows CI判断完成度；FIM/MVI/MPW/PVC仍为图像与专业Owner，MEM/MRC/MTC唯一工程与Undo，MOC编译，MRK/MDS/F19执行和运行数据，FLD诊断；不可从V2.0 UML候选推断类型已存在。DOCS/02—04大型XLSX需在具体Owner/API新建前核对相关Sheet。优先闭合图像→算法→结构化结果→工程/运行的实际端到端证据。

### 二维工作台V2.0总体架构深化（2026-10-28）

- 在既有11份V2.0设计文档内完成深化，不另建工作台框架；[00总纲§11](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——00总体架构与设计总纲_V2.0.md)明确四逻辑平面、12项裁决及四道设计Gate。
- [02 UML§11—12](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——02UML总体模型与设计模式_V2.0.md)在26张初始关键图之后新增X01—X07七张真实Owner锚点UML（合计33张），细化MEM/MOL/MPM/MOC/MRK/MDS与FIM/PVC/MVI/MPW的唯一真源。
- 01全类目录和03—08专题及实施、测试台账同步记录候选复用裁决；原VisionPipelineDefinition、VisionTypedPort、VisionRunResult等概念并非已批准公开类。专业Pipeline优先采用MEMContent+MOL，MRK/MDS复用，不建第二工程/执行/数据机制。
- **本阶段是设计深化**，并未据此实现260项、修改业务算法代码或执行本轮Windows CI；真实功能进展始终按最新源码与匹配SHA验收。

### 二维工作台V2.0三核心契约继续深化（2026-10-28）

- [00总体架构§12](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——00总体架构与设计总纲_V2.0.md)：全类体系的Data/Operation/Evidence三核心合同、已定/待决/禁止清单、CASE-ARCH-01完整用户闭环。
- [02 UML§13](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——02UML总体模型与设计模式_V2.0.md)：新增Y01—Y04四张跨体系关系图；在已有33张图基础上目前共**37张关键关系图**（具体图数/语法以文件复核为准）。
- [03数据契约§9](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——03图像数据与生命周期_V2.0.md)、[05算法契约§11](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——05算法能力与专业流程_V2.0.md)、[06结果契约§10](Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——06结果测量与工程资源_V2.0.md)：像素/Frame/Mask/Artifact身份，MPM TypeId与MRK CLR Type映射，RunResult/ResultItem与MEM正式资源分离。
- [Implementation§11](Implementation/CxVisual_专业视觉体系%23通用二维视觉工作台——V2.0实施源码与迁移台账.md)列出已有源码与先行工作包；[Testing§10](Testing/CxVisual_专业视觉体系%23通用二维视觉工作台——V2.0全类验收与测试矩阵.md)定义CASE-ARCH-01的20项正/反测试。
- **状态警示**：本阶段仅更新总体架构、详细契约及UML/实施验收文档；候选FrameRef、VisionRunRecord、ResultItem和TypeId示例未被宣称已编码/已注册；没有本轮Windows CI或设备HIL通过的断言。
