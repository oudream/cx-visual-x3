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


### 图元编辑规范（工程呈现化子体系）

- [图元编辑完整设计与验收 V1.2](Architecture/CxVisual_工程呈现化体系%23图元编辑——完整设计与验收_V1.2.md)：十四章、四附录、A～F 核心操作图；包含六类控制柄、场景投影、数学变换、固定/浮动视觉连接、Group/Symbol/剪贴板、Studio 使用合同及 GE-01～GE-15。**设计合同，不是已实现/已验收的宣告**。
- [图元体系架构与归属现状](Architecture/CxVisual_工程呈现化体系%23图元体系——完整架构类归属与验收现状_V1.0.md)：维护实际类归属、源码状态；具体实施/CI 见 Implementation/Testing。避免维护两份设计主规格。

### 已完成的七仓技术资产档案：01～03（源码证据及复用提案，不是正式定案）

| 档案 | 内容与应读时机 | 本档案引用的旧仓源码基线 |
|---|---|---|
| **[01_ccxx.md](Codebases/01_ccxx.md)** | C/C++基础类型、Channel、IO线程、Application/Timer、日志、描述及公共设施；开发Foundation/通信/线程机制时重点查阅 | `oudream/ccxx`：`26cecfb02e861ce6b821b33350493bac4793e997` |
| **[02_device_communicator.md](Codebases/02_device_communicator.md)** | IoT型号/设备、点表（attr/event/link）、Channel/Protocol/Communicator、实时数据、控制、Monitor/Daemon；审核M04/M09/M12/M15及Automation时重点查阅 | `oudream/device_communicator`：`18b5313d8c0c350f3a8cc723f57120cf9e551156` |
| **[03_gcl3.md](Codebases/03_gcl3.md)** | ICS/SCADA终端、规约、测点、PSM/Bus、共享内存、RTDB、触发、守护及现场部署；审核运行机制、消息和实时数据时重点查阅 | `oudream/gcl3`：`511ea4af40f0184686fda9b904254d9372019e2a` |

三份文档采用不同的研究重点，**均以事实与建议分离为原则**：源码事实/历史经验/设计提案/真实测试不得混同。按任务阅读相关档案，不要求每次全量读完；后续新增其他代码资产档案，应在此索引补入。

### 运行体系专项讨论稿（六角色期望 → 统一能力 → 执行模型）

- **[统一运行体系综合设计讨论稿 V0.1](Architecture/CxVisual_统一运行体系_六角色期望与并发异步编排执行_综合设计讨论稿_V0.1.md)**：整合R1～R6六类用户对代码SDK、专业Provider、编排、调试、操作和维护的期望；E01～E18需求、统一任务/流程/上下文/执行组模型、非阻塞异步、队列/背压、归并/资源/生命周期、三进程与Vision/IoT/CT/AI验收。**目前属于可修正的设计讨论稿，不是正式Owner/API定案，更不是运行代码已实现证明。**
- 当前收敛的实现维护方向是：Foundation.Infrastructure / Concurrency（F19）与 Mechanism.Runtime / Execution（统一执行引擎）两个集中目录；其他Orchestration/Data/Connectivity/Professional仍保有本职契约和定义，不能因此产生第二套执行器。

### 运行体系：测试驱动架构与首个代码闭环

- **[统一执行体系TDD矩阵（持续更新V0.6.1）](Testing/UnifiedExecution_TDD_Matrix_V0.1.md)**：从R1～R6、E01～E18倒推出T01～T20的测试用例、首批F19/Runtime实际类与接口、已覆盖和未覆盖项。首批T01～T10及后续资源租约、请求关联、分支归并、子流程、软件Plan版本等测试接入既有FoundationSmoke/ArchitectureSmoke；Windows CI编译及通过状态以对应提交的实际日志为准。已实现的进程内资源租约、已知计划子流程、软件PlanSlot与正式发布、跨进程/跨进程资源仲裁、实机安全严格区分；后者明确保留为后续测试，不把设计稿当作已运行实现。

- **[统一运行内核实现与验收 V0.6.1](Testing/CxVisual_统一运行体系内核_实现与验收_V0.3.md)**：只围绕F19 Concurrency及Runtime.Execution，完整列出当前可执行基础API、事件与定时触发、条件/循环、调试暂停、优先级、资源等待、数据租约、停止排空和测试入口；明确尚未交付的跨进程、持久恢复及实机安全。

- **[统一运行体系六角色差距审计与内核增构V0.6.1](Architecture/CxVisual_统一运行体系_六角色差距审计与内核增构_V0.5.md)**：重读六角色原文，比较E01～E18与最新源码；新增执行组、类型端口、流程身份与会话、真实条件Join、能力计划编译、两层Data作用域、持久Journal、按字节背压与专用SDK线程；附T24～T38实测进展及剩余Owner职责。设计可修正，完成度以Windows CI为准。

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

## 统一消息中心 双项目 V1.5（2026-10-24）

- [统一消息中心完整设计与全类API V1.5](Architecture/CxVisual_公共机制体系%23消息中心——完整设计与全类API_V1.5.md)：对照MQTT Topic与进程内Queue/Shared/QoS/Retain/Request-Reply语义，逐类列明Owner、对象关系、真实API、异常与线程模型。
- [统一消息中心实施与Windows验收台账 V1.5](Implementation/CxVisual_公共机制体系%23消息中心——实施与Windows验收台账_V1.5.md)：源码落位、运行命令、可用与未实现边界、CI证据。
- [统一消息中心功能并发异常测试矩阵 V1.5](Testing/CxVisual_公共机制体系%23消息中心——功能并发异常验收矩阵_V1.5.md)：47项分级候选验收，V1.3明确Foundation.Data和Mechanism.Data两个既有项目归属，同时覆盖有界ThreadPool分发、参数快照、Codec和延迟指标；源码单元/并发、UI及性能分开记录。
- **实际Owner**：M36位于 Mechanism.Data；当前P0不需要Broker，不能代替M09变量/MDS数据Owner/M12命令或MRK执行。消息中心发布订阅的真实验收以具体提交的Windows CI为准。

## 统一命令中心 双项目归属（2026-10-24）

- [命令中心双项目Owner裁决 V1.0](Decisions/CxVisual_统一命令中心双项目归属裁决_V1.0.md)：命令基础定义、参数Schema、快照统一在 `Foundation.Data.Commands`；命令运行机制、MDS绑定适配统一在 `Mechanism.Runtime.Commands`。原Logic/MCE及Mechanism.Data命令专属文件已物理迁出，不建立额外项目。
- [命令中心实现与验收](Implementation/CxVisual_公共机制体系%23命令中心——进程内实现与验收_V1.0.md)：进程内实现、调用示例、Windows专项验收与明确未完成的跨进程及硬件边界。

## 统一交互机制 V1.5（2026-10-24）

- [四大能力与双层总线完整设计及验收 V1.5](Architecture/CxVisual_公共机制体系%23统一交互——四大能力与双层总线完整设计及验收_V1.5.md)：变量高速、消息轻巧、命令受控执行、运行数据与大数据；进程内逻辑总线先行，进程外 MQTT/SHM/文件服务按需桥接；双寻址、分层订阅、Owner、反馈语义；**V1.3 明确 MQTT/IoT 风格消息/命令；V1.4 追加41个对象职责角色及OO/GoF/AOP、管理者和流转生命周期完整设计（不意味着新增41个类）；**V1.5 定案消息 Topic、Payload 四类、MQTT 原生 `+/#` 过滤，以及命令参数范围、单位和默认超时的源码契约**。
- [进程内总线分阶段实施与源码台账 V1.3](Implementation/CxVisual_公共机制体系%23统一交互——进程内总线分阶段实施与源码台账_V1.3.md)：P0 各阶段源码对照、已实现与未实现差距，不把接口候选当已交付。
- [TestCenter与Windows验收矩阵 V1.4](Testing/CxVisual_公共机制体系%23统一交互——TestCenter与Windows验收矩阵_V1.4.md)：正反例、并发和可靠性测试计划，现为 139 条候选验收；尚未声明测试已通过。
- [V1.1审查与V1.2修订报告](Architecture/CxVisual_公共机制体系%23统一交互——V1.1文档审查与V1.2修订报告.md)：历史问题、修订原因及待复核事项。

> 当前文档是设计与实施基线，尚需结合 DOCS/02～04 细核 Owner/API；不代表进程内功能、跨进程通信或现场设备验证已完成。
