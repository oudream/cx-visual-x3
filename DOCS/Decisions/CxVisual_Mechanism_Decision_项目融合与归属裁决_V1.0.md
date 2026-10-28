# CxVisual · Decision 统一决策与规划体系——项目融合与归属裁决 V1.0

> 日期：2026-10-28；裁决依据：用户已明确选定 **Decision**，并要求 **Logic 与 Orchestration 两项目合并、改名**，先完成文档再做代码。
> 源码审计基线：`oudream/cx-visual-x3` master `f081438986a201382167d95c1130ea9a284bd7e1`。
> 状态：**名称、三层职责方向、两项目融合目标已由用户决定；下列具体类型/API/依赖迁移属于待实施技术设计，须依真实源码、Windows CI 和迁移验收逐项落地**。
> 相关：《统一决策与规划体系#0总体架构与10大能力》《#1对象合同与开发者API》《Decision融合迁移》《Decision验收矩阵》。

## A. 裁决概要

1. 保留三段职责：**Core = 定义/组织/结构/能力描述与约束；Decision = 理解、判断、验证、评估、选择、策略、规划、优化与解释；Runtime = 加载、实际连接、执行、调度、运行状态、资源准入与结算**。
2. 正式名称：**CxVisual 统一决策与规划体系 / Unified Decision & Planning System**；物理工程目标名称 `CxVisual.Mechanism.Decision`，目录 `Mechanism/Decision/`，程序集/默认命名空间同名。Decision 是总体能力；Strategy 是其中的规则/方案能力，不另建 Strategy 引擎。
3. 将 `CxVisual.Mechanism.Logic` 和 `CxVisual.Mechanism.Orchestration` **合并成一个** Decision 项目；不新建第三个平行项目；不保留两个持续编译的独立旧 DLL。现有 `Mechanism/Logic` 几乎只有 `AssemblyInfo`；`Mechanism/Orchestration` 包含 MOC 编译/Build/发布源码，优先作为迁移主干。
4. 如没有其他项目数量调整，原 **28 个项目（Mechanism 9）→ 27 个项目（Mechanism 8）**。这是用户对原 28 项目约束的一次**明确局部例外**；正式迁移时需同步 `CxVisual.Unified.sln`、相关 .csproj、`DOCS/ProjectCatalog.csv`、`DOCS/DirectoryLayout_V1.1.md`、02/03/04 权威规格表与检测脚本。**文档阶段不宣称物理项目数量已经变化**。
5. 基于成熟度与最少破坏原则，目标 Decision 项目 **优先保留原 Orchestration 项目 GUID `{6E9A7006-3AD6-5F5B-A452-1C89847495A0}`**，仅在迁移前审核 SLN/GUID 依赖后最终执行；旧 Logic GUID `{71A7BD54-3D24-50DC-B60D-42A9848E8DB0}` 目标移除，不应同时存在两个同 GUID 工程。
6. **工程真源不动**：`MEMProject / MEMContent / MOLLogicDefinition / MOLControlBlock / MOLWiringDefinition` 仍归 Core；Studio 不维护第二份业务 Graph，Decision 不持久化第二套定义。
7. **运行真源不动**：MRK 唯一执行引擎、Worker/Flow/Task/Session、触发/重试的实际计时、资源租约、Trace 与并发推进仍归 Runtime；MDS 唯一运行值/堆栈与 Frame；专业 Provider 唯一业务算法/硬件动作 Owner。
8. **简单程序不强制经过 Decision**。C# SDK 可直接使用专业能力或 Runtime；复杂工程编辑、发布校验、选型评估及方案生成时按需调用 Decision。
9. **Runtime 最终应消费 Decision 的稳定方案/决策合同**。目前 `Orchestration → Runtime` 真实存在，且 MOC 使用 `MRKExecutionPlanCompiler` 和包含执行委托的 `MRKExecutionPlan`；迁移先解开编译产物与运行绑定的耦合，再实现 `Runtime → Decision` 引用，禁止双向依赖或循环。
10. 静态判断 **不能冒充硬实时安全、设备可用、现场容量或运行许可**：Decision 可拒绝、警告或建议，实际运行资源与安全联锁由 Runtime/Host/专业 Owner 执行；硬约束不可因评分或 AI 建议被覆盖。

## B. 为什么要有 Decision，而非只是 Orchestration 改名

核心三个问题：
- **能不能做**：类型、引用、依赖、状态、能力、权限前置、版本、资源和安全约束是否满足。
- **该不该这样做**：在技术可行前提下，对吞吐、精度、时延、成本、可靠性、风险与授权目标作显式权衡。
- **怎样做更好**：选择能力/Provider，建立协作结构，设置条件策略与备选方案，提供成本/风险证据，必要时生成可执行计划。

Decision 可以只输出 **判断/选择/诊断**，不必每次都编译 Plan。不可静态决定的现场事项一律标明“需要运行时验证”。

## C. 现实与目标对照（源码不等于目标）

| 当前源码事实 | 裁决目标 | 保留边界 |
|---|---|---|
| `Mechanism/Logic/CxVisual.Mechanism.Logic.csproj` 仅编译 AssemblyInfo | 并入 Decision，取消独立旧程序集 | 不伪造 Logic 代码迁移 |
| `Mechanism/Orchestration/MOCEngineeringCompiler.cs` 包含编译、表达式辅助、Flow Builder、SourceMap、Trace View | 分离编辑、语义、编译、诊断及映射内部边界，归唯一 Decision | Core 的 MOL 不复制 |
| `MOCBuildService`、`MOCCapabilityCatalog`、`MOCProjectRelease` | 决策/能力匹配、构建、发布候选验证迁入 Decision | 实际 Stage/Activate 授权由 Runtime.Host/Lifecycle 与运行侧控制 |
| `Mechanism/Runtime/Execution/MRKExecutionCompiler.cs` 实际存在 `MRKExecutionPlanCompiler` | 静态请求/能力匹配/验证/计划编译逐步迁入 Decision | 运行加载/执行器不能随迁移变成第二套内核 |
| `MRKExecutionPlan.cs` 当前 Step 含 `ExecuteAsync` 委托 | 显式区分可声明/可版本化的计划合同与进程内已绑定执行句柄 | 不在 Plan 序列化 Delegate，不伪造可跨机部署 |
| `MRKExecutionEngine`、`MRKExecutionGroups`、`MRKExecutionJournal` | 留 Runtime | 运行实例及资源结算保持唯一 |
| 现有 OOP/GoF/AOP/API、Studio/MOC/MRK 测试 | 逐能力复用/适配，旧用例无退化 | 无形式主义空壳类型 |

## D. 无环工程依赖：必须解决而不能直接改名

**当前事实**（按真实 csproj）：`Orchestration → Runtime → Core/Data`；Logic 工程主要为空，Runtime 不依赖 Orchestration。仅重命名 Orchestration 且让 Runtime 再引用 Decision，会出现环。

**最终目标**（箭头表示“引用依赖”）：
```text
Decision ---> Core / Data / 允许的基础契约
Runtime  ---> Decision（方案/Plan合同） + Core / Data / F19
Host     ---> Runtime + Decision（装配/授权）
Studio   ---> Core + Decision（编辑及校验）
Professional ---> 适配入口/能力契约；不反向引入专业实现到 Decision
```

计划合同的首选 Owner 是 **Decision 中不可变、无执行委托的描述及验证产物**；Runtime 可持有内部已绑定执行计划。为避免迁移阶段反向引用，允许过渡期在 Core 放极薄的数据接口/值合同，但不得把策略、编译或执行行为塞回 Core；具体以接口 DAG 审计定案。

**不得**以“共享类库”之名再新增 DLL 来规避环；必须先把不合理引用拆开，且对旧 Plan/Provider/SourceMap 进行显式适配。

## E. 迁移兼容决策

- x3 仍为未发布新平台，可在有完整迁移门禁的前提下对内部 `MOC*`、`MRK*Compiler` 进行受控类型/命名空间迁移；不得无审计地全局替换字符串。
- 目标前缀候选 `MDC*`（Mechanism Decision）；**正式公开类型命名尚待逐文件映射确认**，暂不将示例视为已存在 API。
- 保存格式、ProjectId/ContentId/BlockId/PlanId、诊断码/SourceMap、已授权发布指纹、序列化身份保持兼容或执行可验证迁移。
- 兼容过渡只能通过 **单向调用的临时 Facade/Adapter** 完成，必须注明移除时间与测试；禁止长期保留第二个编译器、Graph 或 Runtime。
- 旧源码先审计/迁移后删除旧工程；`ProjectCatalog` 的“现状”与“目标”不得混成已经完成。

## F. 反向约束与撤销条件

- 任何 Decision 建议若涉及 PLC/X-Ray/运动控制等不可逆或高危动作，必须有专业 Owner 明确的硬约束、操作许可、现场联锁与 Runtime 最终检查；约束不确定即拒绝或要求人工批准。
- “无候选满足硬约束”必须返回 **不可行**，不得输出评分最高但不安全的方案；无实测性能数据只能给估算及置信度/证据限制。
- 若阶段迁移导致 DAG 循环、行为不一致、旧工程加载失败或回归重要功能，停止后续物理合并并回滚当前变更；文档与状态保留真实证据。

## G. 与旧资料的关系

旧 `DOCS/Architecture/CxVisual_工程与逻辑体系#逻辑体系——完整设计与验收_V1.0.md` 和运行体系早期 MOC/MRK 说明保留历史来源；**本裁决在“Logic+Orchestration → Decision”归属上更新旧目标**，其尚未迁移的源码行为仍需按旧 API 审计。原 28/9 布局及四大电子规格中的对应项目行，应在实际项目迁移同批更新，而不能在文档阶段宣称已变为 27/8。

**当前交付范围仅为正式设计与可执行迁移指导；项目改名、源码转移、构建与 CI 均为后续实施任务。**
