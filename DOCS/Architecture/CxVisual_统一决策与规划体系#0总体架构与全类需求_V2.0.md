# CxVisual Decision——#0 总体架构与全类需求 V2.0

> **地位：唯一权威总纲；状态：目标架构规格，不是已实现证据。** 审计起点：`master@0487b050f73fff9b3efe03e561ca017b9a02e16a`；旧[#0 V1.0（含 V1.1）](CxVisual_统一决策与规划体系%230总体架构与10大能力_V1.0.md)为历史来源。现状保持 28 项目、Mechanism 9；计划合并后的 Decision 工程尚未创建。
>
> **理念：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**
>
> **使命：以约束为边界、目标为导向、策略为手段、证据为依据、反馈为校验；让专业开发者通过轻量声明和注册，自然得到编排、运行、观察、调试、模拟与优化支持。**

## 00. 三分钟总览（本图是业务协作图，不是 DLL 引用图）

~~~mermaid
flowchart TB
  DEV["专业开发者：算法/设备/服务/仿真 + 轻声明/注册"] --> CAT["Core/MFM：模块/工程/能力声明真源"]
  USER["工程师：Studio选择Symbol/Content/Capability、连接、配置"] --> CAT
  CAT --> DEC["Decision：能不能做？该不该做？怎样更好？"]
  CTX["目标 + 约束 + 策略 + 环境 + 有效证据"] --> DEC
  DEC --> IDE["编辑期：过滤、引导、即时检查、说明、推荐、路径推演"]
  DEC --> PLAN["构建期：诊断、候选、资源/策略建议、静态描述Plan"]
  IDE --> USER
  USER -->|"确认后Core事务更新"| CAT
  PLAN --> HOST["Host/Lifecycle：审批/发布/版本与授权"]
  HOST --> RUN["MRK/F19：真实绑定、执行、取消、断点、Trace"]
  RUN --> PRO["Professional：Vision/CT3D/Automation/Inspection/HMI/Simulation"]
  RUN --> DATA["MDS：运行数据和结果"]
  RUN --> TRACE["运行事实：Flow/Invocation/Step/状态/Journal"]
  DATA --> TRACE
  TRACE --> VIEW["Studio/SDK：同图定位、诊断、复盘"]
  TRACE -.->|"显式快照，非实时关键路径"| DEC
  VIEW -.-> USER
~~~

**关键边界**：Core 是工程定义/编辑/版本唯一真源；Decision 是静态判断、评估、规划与解释；Runtime 执行与资源/Trace/调试；MDS 管数据；专业拥有算法、设备安全和仿真模型。简单 C# 专业方法可直接调用，不强制通过 Decision、工程模型或 Runtime。静态通过不等于现场授权。

## 01. 为什么需要 Decision：用专业实际需求反推

| 专业 | 轻量声明 | 应获得的帮助 | 绝不由 Decision 重做 |
|---|---|---|---|
| Vision/AI | 图像格式、参数、输出、算法/模型版本 | 兼容节点、自动端口引导、算法候选、节拍/精度评估 | 实际像素运算、AI推理、GPU模型管理 |
| CT3D | 投影/体素几何、单位、内存和计算特性 | FDK/SART等候选匹配、CPU/GPU/分块、资源/误差权衡 | 重建、配准、体数据与GPU分配 |
| Automation | 命令/事件/状态、权限、互斥、幂等、联锁前置 | 连接/顺序/并行校验、重试禁止、替代/恢复建议 | PLC联锁、危险设备动作、实际资源租约 |
| Inspection | 判定规则、结果/证据、NG语义 | 规则检查、目标冲突、可追溯决策理由 | 真实判定、业务报告和产线质量执行 |
| HMI | Symbol/绑定类型、质量/权限 | 适用Symbol推荐、变量绑定检查、命令引导 | WPF控件/显示、用户确认与真实授权 |
| Simulation | 可替代能力合同、虚拟模型、场景、种子 | 情景选择、路径推演、结果分级比较 | 专业虚拟模型与真实MRK执行 |

**价值反证**：纯单次算法/直接调用 MDS/简单 UI 绘制无需 Decision；出现跨对象约束、多个可选方案、目标冲突、复杂策略或变更影响时才有独立价值。

## 02. 三个判断的严谨定义

| 判断 | 必需依据 | 结果 | 严格边界 |
|---|---|---|---|
| **能不能做** | 端口/类型/依赖/版本/能力/权限/安全/资源硬约束 | Allowed、Rejected、Unknown + 可定位原因/必要现场复核 | 有硬条件失败则拒绝；未知不得当通过 |
| **该不该做** | 业务目标、优先级、基线方案、收益/成本/风险、证据质量 | Recommended/NotRecommended/NeedsEvidence + 权衡 | “可实现”不是“值得做”；没目标不能凭空决策 |
| **怎样做更好** | 已知可行候选、资源/性能模型、软目标、场景/可靠性假设 | 推荐/备选/适用前提/Pareto/风险与预期效果 | 不可行候选永不因高分被选中；不假称全局最优 |

约束有明确来源、版本、单位、有效期和三态结果；目标包括可量化收益与软偏好；策略包括候选筛选/排序/规划及运行建议。**决策策略归 Decision，策略的真实执行归 Runtime/Host/专业**。详见[#2](CxVisual_统一决策与规划体系%232约束目标与策略_V2.0.md)。

## 03. 分层使用体验：最低成本、渐进增强

| 使用层 | 用户需要了解 | 实际入口 | 自动获得 |
|---|---|---|---|
| L0 直接专业方法 | 输入与参数 | 专业SDK | 专业结果；无需工程/Decision |
| L1 轻流程 | 能力 + Step/Flow + Export | Code Flow/简化画布 | MRK/MDS统一运行、基本Trace、必要类型检查 |
| L2 工程Studio | 选择/连线/配置/Build | MEM/MOL画布 + 能力目录 | 筛选/说明/推荐/即时校验、撤销/版本、调试定位 |
| L3 工业方案 | 目标、资源、安全、候选和策略 | Studio高级面板/SDK/Host | 多候选比较、策略/资源规划、情景推演、受控发布 |

避免“为了获得基础Trace必须先填写10个Decision表单”，也避免“仅靠一个Attribute就宣称获得真实安全仿真”。

## 04. 十大能力 D01～D10（保留批准分类）

| ID | 核心任务 | 最小产物 | 何时进入工作链 |
|---|---|---|---|
| D01 语义理解与推导 | 类型/单位/作用域/表达式/依赖/副作用语义 | SemanticAssessment | 编辑/连线 |
| D02 合法性与可行性 | 结构/端口/版本/硬约束/授权前置三态 | Assessment+Diagnostics | 即时/Build/发布 |
| D03 目标与约束协调 | 冲突/优先级/可行域/不可满足说明 | TradeoffReport | 设目标/比较 |
| D04 能力匹配与方案选择 | 上下文发现/过滤/排序/备选 | CandidateSelection | 搜节点/换算法 |
| D05 逻辑与协作规划 | DAG/Branch/Loop/Foreach/子流程/SourceMap | StructuralPlan | 编排/编译 |
| D06 策略制定与协调 | 超时/重试/补偿/故障/降级候选 | StrategySet | 故障及资源策略 |
| D07 资源与性能规划 | CPU/GPU/内存/带宽/并发预算与冲突 | ResourceEstimate | CT/AI/并行 |
| D08 预测推演与优化 | 路径、情景、敏感性、Pareto、稳健选择 | ScenarioComparison | 方案改善 |
| D09 验证、仿真与影响 | 静态/Virtual/Replay证据与变化影响集 | VerificationReport | 构建/变更/预演 |
| D10 决策解释与反馈 | 原因码/SourceMap/证据链/实测偏差 | Explanation+Impact | 全阶段 |

十项能力**按需组合而非每次串行十步**：连线仅D01/D02/D10；选算法D02/D03/D04/D07/D10；完整工程再D05/D06/D08/D09。当前MOC/MRK仅有部分D01/D02/D05/D10源码行为，其他完整Decision能力仍属目标；不得用“已有Compile”冒充完成。


## 05. 八大使用价值需求域（不是新增八个引擎）

| 域 | 最低要求 | 专题/验收索引 |
|---|---|---|
| R1 专业能力接入 | 注解、描述、显式注册、端口/参数/版本、Provider能力验证；支持普通函数不建模块实例 | #3 / DEC-REG |
| R2 约束目标策略 | 类型/资源/安全/动态条件、目标冲突、三态未知、规则复用、证据时效 | #2 / DEC-CST |
| R3 上下文编排 | Symbol/Content/Capability 过滤、连线、参数引导、即时校验和路径预览 | #4 / DEC-AUT |
| R4 选择/规划 | 候选评估、流程静态结构、资源/策略、Pareto及方案解释 | #1/#2 / DEC-SEL |
| R5 运行交接 | 静态Plan、Host授权、MRK绑定、版本固定/不受控修改禁止 | #1/#5 / DEC-HAND |
| R6 调试追踪 | RunId/Step/Invocation/SourceMap、诊断、Trace丢失、复现实验 | #5 / DEC-DBG |
| R7 仿真反馈 | 静态推演/Virtual/Replay/HIL区分，影响分析、预测/实测偏差 | #5/#6 / DEC-SIM |
| R8 使用质量 | 轻量SDK、Studio/无头一致、可取消、性能、测试、工业安全 | Testing / DEC-UX |

每条需求应具有 `ReqId/场景/调用者/触发/输入/规则/输出/Owner/接口/测试ID/证据等级/效果指标`，建立双向关系：需求→能力→代码→测试及测试→需求。

## 06. 开发者最想要的一条完整使用链

~~~mermaid
flowchart LR
  P["专业实现"] --> D["一次描述/注册"]
  D --> C["统一Capability元数据"]
  C --> A["编辑期：过滤/引导/检查"]
  A --> B["Core提交的编排定义"]
  B --> K["Decision构建/方案评估"]
  K --> R["Host授权后MRK运行"]
  R --> O["Trace/SourceMap/MDS观察"]
  O --> T["同图调试/问题定位"]
  T --> F["依据证据显式重新评估"]
  F -.-> A
~~~

专业开发者应提供**最低必要事实**而非为每种消费场景重写适配器：方法参数可以推导基础CLR类型，FCM配置注解复用 Schema；单位、物理坐标、数据所有权、副作用/幂等、GPU需求、真实精度/延迟和物理仿真模型必须显式声明或给出可信证据。模块种类固定 Connector/Driver/Compute/Simulation/Service，与六大专业Workbench正交。Symbol 是 MEM 可视复用定义，不等同于 Capability。详情[#3](CxVisual_统一决策与规划体系%233专业能力声明与注册_V2.0.md)、[#4](CxVisual_统一决策与规划体系%234上下文编排与智能引导_V2.0.md)。

## 07. Core/Decision/Runtime 与其他 Owner 的最终界面

- **Core**：Project/Page/Content/Symbol/MOL定义/约束引用、编辑/撤销、唯一工程 Revision；专业/FCM负责原始参数和规则声明。
- **Decision**：不持久化第二份工程树、设备状态或第二个模块目录，只消费快照，计算三态判断、候选、纯结构Plan、解释与未来影响；不得持有真实 ExecuteAsync 委托/DeviceHandle/MDS活Frame。
- **Runtime/Host**：唯一MRK/F19执行、资源租约、取消/运行状态、Breakpoint、Trace/Journal、真实版本/权限/设备安全复核；MDS负责作用域/提交/回收。
- **Professional**：提供真实计算算法、设备安全/许可要求、性能模型、模拟模型和专业结构化输出；Studio/MPW只处理交互呈现。
- **工程依赖目标**：Runtime → Decision → Core/基础合同，Decision ↛ Runtime，Foundation ↛ Mechanism，Mechanism ↛ Professional实现。旧Orchestration → Runtime必须先拆静态编译与运行绑定，不能简单改项目名。

**接口策略**：轻量SDK允许无Core工程 `AssessConnection` / `FindCandidates`；只有完整编排才构建静态Plan。新增 MDC 类型仍标拟议，详见[#1](CxVisual_统一决策与规划体系%231对象合同与开发者API_V2.0.md)。

## 08. 总体验证与架构不变量

1. **正确性**：所有已知违反的硬约束拒绝，未知不放行，非法类型/范围/版本拒绝，安全不由软评分覆盖。
2. **易用性**：每个六专业案例给出纯专业调用、最小注册、Studio使用和高级扩展；测试注册重复代码行数、首次使用步骤、无工程直接调用能力。
3. **编排质量**：选择/连线/配置变化快速返回可定位原因；候选分 `适用/不适用/证据不足`，默认不隐藏可解释的失败项。
4. **运行与调试**：断点/单步由MRK负责；同图Trace定位真实调用实例，Trace缺口用权威快照恢复状态而非伪造事件；不在生产危险设备步骤任意停机。
5. **模拟与优化**：Static路径推演 ≠ 虚拟执行 ≠ Replay ≠ 真实/HIL；优化给出基线、目标、假设、候选、结果、风险、验证计划。
6. **可靠性/性能**：不在WPF UI线程做重评估；输入Revision/能力版本改变立即失效旧推荐；有界CPU/内存/取消/延迟预算；10/100/1000候选与100/1000/10000节点的基准需固定硬件后制定门槛。
7. **长期扩展**：允许插件算法、目标函数/资源估算器和AI候选，但硬安全与可复核决策不交给不可审计模型。
8. **Owner/兼容**：不复制注册中心、执行内核、Schema或State/Trace；旧公开调用的迁移必须有逐文件映射、Golden及无环项目检查。

## 09. 设计的完成程度与禁止误报

- **Design**：文档、UML、角色/接口/约束/用例已明确，不是功能代码。
- **Source**：真实函数与真实调用入口可找到；不能因为有文件就称使用闭环。
- **Build/Automated/Integrated**：对应SHA/命令/Windows CI和产物可核验；不得把不同提交的成功混用。
- **HIL/Field**：真实专业硬件、安全审核、异常场景及长稳证据另行批准。
- 当前已知：MFM/FCM、MOC目录/Build、MRK Code Flow/Trace/Debug和Studio部分消费确有代码；Decision V2注解、一站式SDK、统一约束求解与六专业闭环**仍需实施与验收**。本轮不修改任何 C# 或工程结构。

## 10. 唯一文档目录与阅读顺序

| 编号 | 权威专题 | 入口 |
|---|---|---|
| #0 | 总体理念/需求/边界/十大能力（本文件） | 本文 |
| #1 | 对象合同、请求结果、开发者API、交付 | [#1](CxVisual_统一决策与规划体系%231对象合同与开发者API_V2.0.md) |
| #2 | 硬软约束、目标、策略、证据及算法 | [#2](CxVisual_统一决策与规划体系%232约束目标与策略_V2.0.md) |
| #3 | 注解/注册/描述、五类模块和专业合同 | [#3](CxVisual_统一决策与规划体系%233专业能力声明与注册_V2.0.md) |
| #4 | 上下文工具箱、连线、编排引导/路径推演 | [#4](CxVisual_统一决策与规划体系%234上下文编排与智能引导_V2.0.md) |
| #5 | 编译交接、运行观察、断点/回放/仿真/反馈 | [#5](CxVisual_统一决策与规划体系%235运行调试模拟与反馈_V2.0.md) |
| #6 | 六专业端到端案例与“不接Decision”反证 | [#6](CxVisual_统一决策与规划体系%236专业应用与完整案例_V2.0.md) |

[融合ADR](../Decisions/CxVisual_Mechanism_Decision_项目融合与归属裁决_V1.0.md)仅管正式归属；[源码迁移](../Migration/CxVisual_Decision_Logic与Orchestration融合及Runtime解耦_V1.0.md)管逐文件/类型和DAG；[测试矩阵](../Testing/CxVisual_Decision_功能与迁移验收矩阵_V1.0.md)管测试/指标/证据。V1系列仅保留历史，后续不双线维护；若目标与平台01、02～04规格冲突，先记录差异再定案。

## 11. 验收定义

**文档完善** ≠ **Decision功能实现** ≠ **集成/CI通过** ≠ **工业现场就绪**。每条功能做到“真实用户入口→有效上下文→真实判断→结果被应用→效果被测量”后才能宣布业务能力交付；仅生成了对象和单元测试不算。

---

## 12. V2.0正式反审：需求→能力→对象→使用→验证的双向门禁（2026-10-29）

审查依据：[全类反推与OOP/GoF/AOP架构审评 V1.0](CxVisual_统一决策与规划体系%23V2全类反推与OOP-GoF-AOP架构审评_V1.0.md)；本节为总体架构规则，详细对象与验收分别归#1与Testing。本轮文档修订不代表增加任何源码实现。

**六层闭环**：六专业真实目标 → 两类用户L0～L3的七问使用链 → R1～R8/D01～D10覆盖 → 最小对象/唯一Owner → 真实源码/项目DAG → 正反例/性能/证据。反向检查每个对象是否有真实使用理由，删除空壳抽象；没有入口或数据来源的Dxx不能被判为已交付。

**每个正式功能必须有** `ScenarioId, RequirementId, CapabilityIds(Dxx), Trigger, Caller, InputSnapshot, ConstraintOwner, RequiredEvidence, Output/Consumer, TypeOwner, TestId, EvidenceTier`。缺一项时是架构待确认，不允许因文档“全类”一词将它视为完整。

| 设计域 | 保留的核心 | 反审后增加的硬要求 |
|---|---|---|
| 专业轻接入 | 普通方法→Capability→编排 | 一次声明经同一纯Schema/版本投影被Studio、Decision、MRK消费，不能创建第二注册中心 |
| 约束驱动 | Can/Should/Best | HardFail/Unknown不可放行；Should需要基线；Best只在有证据的可行域内比较 |
| 上下文编排 | Symbol/Content/Capability筛选/引导 | UI局部校验、旧Revision结果失效、Core Edit事务和Undo是强制不变量 |
| 计划与运行 | Decision纯Plan、Runtime真实执行 | 不得把MRK委托/设备句柄/CLR运行Context塞进可序列化Decision Plan |
| 诊断/模拟 | MRK真实Trace和PSM模型 | 静态推演、Virtual、Replay、HIL、Field各自独立，EvidenceTier不可越级 |
| OOP/GoF/AOP | 按需组合/扩展 | 每个新接口/模式/横切有具体变化点、Owner、反例测试；不得为模式添加空壳Manager |

**当前整体评价**：架构方向合理、D01～D10分类暂不变；工程依赖/能力静态-运行绑定属于P0；最小API/三态约束/专业注册/Studio闭环为P1，仍然需要工程证明。不得把P0/P1未消除的V2文档标为“可直接无歧义实现完成”。参见审评报告ARC-01～12与REV-01～12。
