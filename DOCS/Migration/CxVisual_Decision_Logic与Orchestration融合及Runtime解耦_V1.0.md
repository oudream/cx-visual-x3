# CxVisual Decision——Logic/Orchestration 融合、Runtime 解耦与项目改名迁移台账 V1.0

> 目标：把 `Mechanism.Logic`、`Mechanism.Orchestration` 合并改名为 **`Mechanism.Decision`**，使 Core 定义、Decision 判断与规划、Runtime 运行形成真实的单向职责及无环依赖。
> **当前状态：规划/逐文件迁移清单，尚未更改 .sln/.csproj、程序集、DLL 名或任何公开类型。** 真实基础：2026-10-28 master `f081438986a201382167d95c1130ea9a284bd7e1`。
> 上级裁决：[Decision融合ADR](../Decisions/CxVisual_Mechanism_Decision_项目融合与归属裁决_V1.0.md)；验收：[Decision测试矩阵](../Testing/CxVisual_Decision_功能与迁移验收矩阵_V1.0.md)。

## 1. 目标工程形态

```text
Mechanism/
├── Core/                         原工程与MOL定义，保留
├── Decision/                     新物理目录，单个 DLL（原MOC+Logic归并）
│   ├── CxVisual.Mechanism.Decision.csproj
│   ├── Properties/
│   ├── Contracts/                 计划描述/判断/诊断/策略等必要公共合同
│   ├── Semantics/ Validation/     逐需落地，不一次建空类
│   ├── Evaluation/ Planning/     目标/候选/资源/流程规划
│   ├── Strategies/ Compilation/  策略、规范化、不可变计划
│   ├── Diagnostics/               编译错误、映射、Explain
│   └── Adapters/                  仅过渡期兼容
├── Data/                         MDS，不动唯一运行数据Owner
├── Runtime/                      MRK真实执行、调度/资源/控制/Trace
└── Lifecycle/                    服务/发布治理，不扩散到Decision
```

原 `Mechanism/Logic/`、`Mechanism/Orchestration/` 在迁移完成、无引用且回归通过后**不再作为项目存在**。最终目标为 27 个项目，Mechanism 从9变8，40个机制子方面应重新映射归属但不是删掉目标能力。

## 2. 已确认的真实项目/依赖问题

| 工程 | 当前真实状态 | 迁移目标 |
|---|---|---|
| `Mechanism/Logic/CxVisual.Mechanism.Logic.csproj` | 目前仅 AssemblyInfo 业务源码为空；引用 Foundation.Data；GUID `{71A7BD54-3D24-50DC-B60D-42A9848E8DB0}` | 去掉独立工程及SLN入口；不存在可迁移的“旧Logic算法” |
| `Mechanism/Orchestration/CxVisual.Mechanism.Orchestration.csproj` | MOC四份主源码；目前引用 Core、Data 和 **Runtime**；GUID `{6E9A7006-3AD6-5F5B-A452-1C89847495A0}` | 主干迁入 Decision；推荐沿用这一 GUID；最终不得引用 Runtime |
| `Mechanism/Runtime/CxVisual.Mechanism.Runtime.csproj` | 工程引用 Core、Data、Foundation.Data、F19；包含 MRKExecutionPlanCompiler、执行器及 Commands | 编译静态责任逐步收敛至 Decision；最终 Runtime 可以引用 Decision，不得反向依赖 |
| `CxVisual.Unified.sln` | 仍登记 Core/Logic/Orchestration/Runtime 四个物理工程 | 删除两个旧项目条目、加入一个 Decision，更新项目配置/引用并静态验无环 |

**立即改 `Orchestration.csproj` 名字而不解开 `Orchestration → Runtime` 会产生潜在循环依赖。严禁先改依赖再想办法修编译。**

## 3. 逐文件/类型审计映射（目标/未搬）

| 当前来源 | 主要类型/职责（源码可见） | 目标与处理方式 | 风险/测试 |
|---|---|---|---|
| `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | MOL定义、表达式、控制块、逻辑持久化 | **保留 Core**；Decision只读快照及引用 | 工程保存/重开、ID/Revision |
| `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | `MOCEngineeringCompiler`、`MOCFlowBuilder`、MOCExpressions、MOCSourceMap、MOCTraceView 等集中实现 | **分割职责**：纯语义/编译/SourceMap/Builder外观在Decision；运行Trace的读取与映射接口明确分别归Runtime/Decision。Core编辑事务仍唯一 | 文件较大；谨慎分拆，不更改已有分支/循环结果 |
| `Mechanism/Orchestration/MOCBuildService.cs` | `MOCBuildService`、诊断、Build结果 | Decision 验证/编译门面；提出较简洁的 Assess/Plan API | Diagnostics与SourceMap定位 |
| `Mechanism/Orchestration/MOCCapabilityCatalog.cs` | 能力元数据目录/发现 | Decision 候选匹配与Snapshot；不拥有专业真实行为 | CapabilityId、Version、Schema |
| `Mechanism/Orchestration/MOCProjectRelease.cs` | 发布候选及 `MOCProjectRelease.Stage`，当前直接依赖MRK发布控制 | **必须拆分**：候选/指纹/验证归Decision；Stage/Activate/授权及操作门禁留Lifecycle/Runtime.Host及运行Owner；过渡适配要单向 | 不可误把Stage调用动作当成纯决策 |
| `Mechanism/Runtime/Execution/MRKExecutionCompiler.cs` | `MRKExecutionPlanCompiler`、`IMRKCapabilityResolver`、输入/诊断/编译Request/Result | 静态请求、契约、验证与编译逐步迁入Decision；真正进程内绑定移至Runtime Loader | 现有MOC依赖MRK类型，迁移顺序敏感 |
| `Mechanism/Runtime/Execution/MRKExecutionPlan.cs` | `MRKExecutionStep`、`MRKExecutionPlan`、Try/LoopRegion、委托 | 分割**可版本化纯描述合同**和**运行已绑定执行对象**；运行对象留MRK，不直接序列化Delegate | 取消、Try/Finally、动态Loop |
| `Mechanism/Runtime/Execution/MRKExecutionComposition.cs` | 编排编译/Builder 与运行入口混合 | 按职责拆分静态组织/计划 vs 动态执行/装配；保留旧代码调用兼容段 | 三层嵌套子流程、数据传递 |
| `Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs` | 开发者方便入口 | 运行Facade继续属于 Runtime；仅移出其中静态的规则/Plan组合职责 | 代码式体验不退化 |
| `Mechanism/Runtime/Execution/MRKExecutionPolicy.cs` | 执行策略与运行行为 | **策略声明/静态合法性**可被Decision消费；运行重试/超时/计时/取消仍MRK | 幂等、异常传播、取消 |
| `Mechanism/Runtime/Execution/MRKExecutionEngine.cs`、`MRKExecutionGroups.cs`、`MRKExecutionTrigger.cs` | 唯一执行、并发、触发 | **全部留 Runtime** | 不建立第二个Worker、触发器 |
| `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs`、`MRKExecutionJournal.cs`、`MRKExecutionPlanSlot.cs` | 运行协同、Journal、计划槽与激活相关 | 运行状态/事务/激活留 Runtime；输入方案合同读取Decision | 生产中Plan版本稳定 |
| `Mechanism/Runtime/Commands/*` | CommandCenter、命令注册/路由/执行 | **保持既有 Runtime Owner**；Decision只验证声明与策略 | 不颠覆已批准命令中心裁决 |
| `Mechanism/Data/*` | MDS数据与交互 Owner | **原地保留**；Decision只作需求分析 | Flow/Frame/Heap唯一真源 |
| `Applications/Studio`、`Tests/TestCenter` | 工程编辑、操作与观察 | 入口替换及状态解释；复用旧MOC行为，不造新流程数据 | WPF/Headless同义 |

每一行实施时必须补：**原类型/方法签名、调用项目、迁后唯一签名、兼容方式、提交SHA、对应测试、CI链接、剩余风险**。没有这些证据不得从“规划”改成“已迁移”。

## 4. 无环依赖改造的分阶段实际顺序

### P0——基线锁定与风险清单
- 固定master SHA，扫描整个解决方案的旧Logic/MOC/MRK引用、`using`/反射/配置字符串/序列化/测试，生成真实符号表与调用关系。
- 原工程运行：`--organization`、`--organization-wpf`、`--concurrency`、`--command-center`、默认ArchitectureSmoke；记录SHA与失败归因。
- 与并行Owner协作：禁止在迁移时静默覆盖其他分支的新源码。

### P1——定义新的“可声明决策/计划”合同
- 确认 Decision 核心 Assessment/Strategy/Plan 的**不含可执行委托**的最小合同，按现有调用场景实现，先用原 MOC/MRK 兼容入口校验。
- 禁止为了满足理想图而一口气把 Core 或 Runtime 全部类型移动；优先拆出静态语义/类型/版本验证部分。
- 现有 Runtime 可在过渡期继续接受原MRKExecutionPlan；新增计划合同待有单向Adapter后接入。

### P2——把静态决策/编译逻辑从MRK逐步迁出
- 移动并验证 `MRKExecutionPlanCompiler` 的可静态部分、MOC 编译分支与解析，保持原 Plan 执行结果一致。
- 能力解析分成**仅元数据/合约校验**与**真实执行Delegate绑定**两个阶段；不允许 Decision 引用 MRK Context 执行设备。
- Build/Compile结果与SourceMap的错误码、身份映射保持不变或显式迁移。

### P3——固定运行端 Loader 与单向引用
- 最终 `Decision → Core`、`Runtime → Decision`，移除Decision对Runtime的全部程序集/类型依赖；用纯DTO/受控绑定处理暂时需要的旧行为。
- 单独 `Runtime.LoadAndValidate` 进行真实能力版本/权限/环境/资源复核，再将描述式计划转换为只在进程内有效的已绑定计划。
- 并行、动态Loop/Foreach、Try/Catch/Finally、命名数据端口和取消语义保持一致。

### P4——项目实际合并与重命名
- 把Orchestration中的真实源码迁入 `Mechanism/Decision/`，按 P1～P3 的分割结果整理目录；项目名/AssemblyName/RootNamespace改为 `CxVisual.Mechanism.Decision`。
- 更新 SLN、全部引用 .csproj、AssemblyInfo、GUID/ProjectConfigurationPlatforms、Build脚本、Owner目录和既有文档；删除旧Logic与Orchestration项目及不再需要的空目录。
- 更新 `DOCS/ProjectCatalog.csv`、`DOCS/DirectoryLayout_V1.1.md`、02/03/04电子规格中所涉原项目条目，建立旧新Owner映射；子方面数量逐项复核。
- 源码引用、产物输出和VS解决方案中**只能有一个** Decision 项目；剩余MOC旧符号仅允许明示过渡兼容层。

### P5——功能深化与现场验收
- 在正确的唯一Owner上逐步实现D03～D10中的目标协调、策略/资源/性能规划、评估、仿真与解释，不为凑数量新增空壳。
- 迁移后的Studio/TestCenter提供原因说明、候选比较、Plan/SourceMap可视化及自动化反例；真实设备安全/跨PC/长稳由专业Owner验收。

## 5. 必须严格检验的迁移风险

| 风险 | 处理 |
|---|---|
| 依赖环 | 项目 DAG 每个阶段验证；不能通过新建空 DLL 掩盖 |
| 二套计划/执行器 | 先建立清晰的声明Plan vs 运行绑定Plan的唯一转换边界；只允许一个MRK运行引擎 |
| API/序列化/命名空间破坏 | 逐类迁移映射、源ID/诊断码保真、旧工程打开-重新构建 |
| 运行期间工程修改 | 已授权运行方案保持版本锁定，重新编译产物需走发布授权和安全点 |
| 运行时数据错乱 | 复用MDS Frame/Stack/Heap、消费者计数与提交回收测试 |
| 非幂等设备动作重试 | Decision仅验证策略合法；专业及Runtime受控执行 |
| 并行协作冲突 | 只在当前master上增量提交，确认文件版本和旧新Owner，不覆盖他人的改动 |

## 6. 停止线与完成定义

- **P0～P3未通过时，不应宣称 P4 改名融合完成。**
- 任一P阶段出现功能回退、旧工程无法保存重开、失去SourceMap、重复执行设备动作、跨层循环引用或Windows构建失败，暂停扩展并修复当前阶段。
- 仅在**真实 SLN 中仅有一个 Decision DLL、旧 Logic/Orchestration 不再编译、Runtime不再反向承担静态编译、无循环依赖、关键集成测试全部通过**后，才可宣布“项目融合完成”；新十项能力中的尚未实装部分仍须标明“规划”。
