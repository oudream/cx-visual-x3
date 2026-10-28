# CxVisual · 工程与逻辑体系 · 逻辑体系 — 完整设计与验收（初简版）

> **2026-10-28 新架构裁决提示**：本文件为早期 Logic 独立工程的设计讨论记录，原物理项目归属建议已被 [Decision 融合与归属裁决](../Decisions/CxVisual_Mechanism_Decision_项目融合与归属裁决_V1.0.md) 更新。最新正式目标是 Core 定义、Decision 判断/规划、Runtime 运行，\`Mechanism.Logic\` 与 \`Mechanism.Orchestration\` 合并改名；**当前源码仍未迁移**。本文保留以追溯历史与已做的原实现，不作为新增平行 Logic 工程依据。\n\n
> **版本**：V1.0（初简版 / 设计讨论稿）  
> **日期**：2026-10-20  
> **定位**：供后续细化、源码归位、验收设计使用；**不是完整规格，也不代表功能已经实现**。  
> **约束**：沿用 CxVisual 既有 Foundation / Mechanism / Professional 与现有项目布局，原则上不新增项目；保持 MEM 唯一工程真源、MOC 唯一构建桥、MRK 统一运行执行、MDS 统一运行时数据；C# 与 Studio 共用语义。  
> **证据说明**：目前仅参考对 x3 `master` 的定向代码查看和历史规格基线；“建议/待实现”不能写成已验证的源码行为。

## 1. 目标与定位
逻辑体系回答**“定义什么逻辑、规则如何关联、其语义是什么”**，支持独立逻辑、无界面后台逻辑、图形编排逻辑及跨专业复用。**不承担**流程实例调度、节点推进、设备执行或运行时状态持久化。

## 2. 功能范围（初版）
| 功能域 | 必备能力 | 主要边界 |
|---|---|---|
| 表达式与谓词 | 常量/输入/结果引用、类型比较、组合与求值语义 | 运行值由 MDS/MRK 提供 |
| 控制逻辑 | 顺序、If/Else、Switch、循环、Break/Continue/Return、调用 | 只定义语义；推进归 MRK |
| 事件规则 | 事件声明、过滤条件、触发关系、参数契约 | 订阅、投递和重放归 MRK |
| 状态机 | State、Transition、Guard、Action、初始/终止状态 | 状态机实例与事件消费归 MRK |
| 数据流语义 | 端口类型、连接规则、输入输出映射、数据依赖 | 数据空间与值生命周期归 MDS |
| 可复用逻辑 | 子逻辑、参数化、引用、版本/作用域约束 | 工程资源、事务和存储归 MEM |

四种模型（控制流 / 数据流 / 长流程 / 状态机）的**公共语义统一，执行特性可以不同**。不要用一个“万能步骤类”抹掉专业差异。

## 3. 唯一真源与项目归位
- `CxVisual.Mechanism.Core`：`MEMContent` 的 Logic Aspect、稳定 Block/Port ID、工程编辑事务、持久化。已有 `MOLLogicDefinition`、`MOLControlBlock`、`MOLWiringDefinition` 等工程声明；**暂不搬迁**。
- `CxVisual.Mechanism.Logic`：以后承接可跨工程/专业复用的纯逻辑规则、表达式和状态机**语义服务**；当前项目主要为骨架，不能假称有完整实现。
- `CxVisual.Mechanism.Orchestration`（MOC）：静态验证、引用解析、编译与 SourceMap。
- `CxVisual.Mechanism.Runtime`（MRK）：逻辑执行实例、条件求值、分支推进、状态迁移。
- `CxVisual.Mechanism.Data`（MDS）：运行值、上下文、隔离的变量空间。

**裁决点**：已有部分表达式求值逻辑位于 `MOCEngineeringCompiler.cs`，后续应审计其编译时与运行时职责，必要时抽出共享、无副作用的语义核；严禁为重构再造第二套规则模型。

## 4. 一个贯通例子
场景：`检测图像 → AI判断 → 若 NG 则复判，否则 OK下料`。
1. 开发者以 `MEMContent` 持久表达式 `IsNG(result)`、分支及动作引用；Studio 显示同一份 Logic Aspect，不用另外存图形逻辑。
2. MOC 校验结果端口类型、分支连通性及复判能力是否存在；输出 MRK 执行计划与 BlockId 映射。
3. MRK 使用此次运行数据计算条件并进入对应分支；多产品实例相互隔离。
4. Trace 映射回逻辑 BlockId，用户在 Studio 查看该次实例走了哪条分支。

状态机例子：`Idle --Start[Ready]--> Running --Fault--> Fault`。**状态、事件、Guard 和边由 MOL 定义；具体某台设备的当前状态属于 MRK 运行实例。**

## 5. 建议的对外契约（概念，不表示现有 API）
- `LogicDefinition`：稳定逻辑身份、节点、控制边、数据边、事件、作用域。
- `Validate(definition, capabilityContracts)`：返回带 ContentId/BlockId/PortId 的诊断。
- `Evaluate(expression, readOnlyContext)`：类型安全且无任意 C# 动态执行。
- `ResolveReference(contentId, invocationPath)`：明确共享定义与调用实例区别。

## 6. 初版验收（含反证）
- C# 创建含 If、循环、子流程、状态机定义的工程，保存—关闭—重开后身份/端口/语义不变。
- Studio 对同一逻辑编辑后，C# 读取结果一致；撤销/重做不产生第二树。
- 缺端口、类型不匹配、悬空分支、非法状态迁移、不允许的循环引用均明确报错，定位到工程对象。
- 无需启动 MRK，也可以编辑与静态验证逻辑；编译后 MRK 执行与定义一致。
- 待扩展：复杂异常语义、动态循环、四模型组合、跨版本兼容须逐项测试，不能由一条 CPU 仿真证明。

## 7. 后续细化任务
T1 对照既有 MOL/MEM 类清单补全契约；T2 列出四执行模型的语义差异；T3 写 C#/Studio 双角色完整示例；T4 反证测试与真实构建流水线。

---

### 本文档状态与后续要求
- **本文是初简版**：只冻结定位、边界、重点能力与第一批验收要求，未替代现有详细规格，也未完成所有类/接口/枚举列表。
- **后续深化**：以最新 `master` 源码、相关原文档与真实 Windows CI 为依据，逐项形成“需求 → Owner → API/类 → 正反例 → 证据”的闭环。
- **实现分类**：现有声明、部分实现、待实现、模拟验证、Windows CI、实机验收必须分开记录。
