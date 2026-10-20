# CxVisual · 统一运行体系 · 总体架构 — 完整设计与职责划分（初简版）

> **版本**：V1.0（初简版 / 设计讨论稿）  
> **日期**：2026-10-20  
> **定位**：供后续细化、源码归位、验收设计使用；**不是完整规格，也不代表功能已经实现**。  
> **约束**：沿用 CxVisual 既有 Foundation / Mechanism / Professional 与现有项目布局，原则上不新增项目；保持 MEM 唯一工程真源、MOC 唯一构建桥、MRK 统一运行执行、MDS 统一运行时数据；C# 与 Studio 共用语义。  
> **证据说明**：目前仅参考对 x3 `master` 的定向代码查看和历史规格基线；“建议/待实现”不能写成已验证的源码行为。

## 1. 总体定位
统一运行体系（MRK）负责**依据 MOC 产生的计划，创建、驱动、控制、调度、协调并保障运行实例**。它不拥有持久工程逻辑、不代替 MDS 管数据、不代替专业驱动做硬实时控制，也不负责发布审批。

## 2. 六个子体系与主要 Owner
| 子体系 | 明确职责 | 不负责 |
|---|---|---|
| 流程执行 | 步骤推进、分支/循环、子流程、四类执行模型 | MOL 持久逻辑定义 |
| 运行控制 | Start、Pause、Resume、Cancel、单步、状态转换 | PLC/运动卡实时制动 |
| 任务调度 | 排队、优先级、并发、限流、定时、公平与资源申请 | 专业节点业务算法 |
| 运行协同 | 事件、等待、Join、跨流程/专业协作、相关性 | 第二套流程编排定义 |
| 运行管理 | Flow/Run/Task/Session 实例、生命周期、策略、隔离 | MEM 工程事务及版本发布 |
| 运行保障 | 超时、重试、补偿、异常传播、恢复与资源清理 | 硬件安全联锁 |

跨子体系支撑：MDS 提供数据和上下文；运行诊断消费 MRK Trace 并回映工程来源；运行治理负责发布、激活、回滚与权限约束。**不把跨越六个子体系的每项能力单独建新引擎。**

## 3. 从工程到运行的数据关系
```text
MEMProject / MEMContent（工程真源）
   └─ Logic Aspect / MOL（逻辑与规则定义）
       └─ MOC（Validate / Resolve / Compile / SourceMap）
           └─ MRKExecutionPlan（已解析的执行计划）
               └─ MRK Flow/Run Instance
                   ├─ 流程执行、控制、调度、协同、管理、保障
                   ├─ MDS（DataSpace / Frame / Stack / Heap）
                   ├─ Professional（PLC、X-Ray、AI、CT 等能力）
                   └─ Trace → SourceMap → Studio 调试/诊断
MLC / Runtime.Host：发布治理、目标环境与运行装配（贯穿相应阶段）
```
执行计划**不是**工程真源；同一编译计划可产生多个运行实例；同一共享 Content 在不同调用路径下拥有不同运行上下文。

## 4. 核心身份与状态约束
- 工程身份：`ProjectId + Revision + ContentId + BlockId`；工程定义持久且可复用。
- 编译身份：`PlanId + CompiledStepId + SourceMap`；追踪到逻辑定义及调用路径。
- 运行身份：`RunId/FlowId + InvocationPath + AttemptId/StepExecutionId`；每次执行独立。
- 状态：Created → Ready → Running → (Paused/Waiting) → Succeeded/Failed/Canceled；每项转换须有授权与前置条件，**不得假设暂停立即生效**。

## 5. 两种使用方式
**开发者**：创建工程逻辑 → `MOCBuildService.Build(snapshot, contentId, resolver)` → 成功时取得 `MOCCompiledPlan.Plan` 与 `SourceMap` → 由 MRK 创建独立运行实例 → 查询状态、数据、Trace。

**工程编排用户**：Studio 连线并保存 → 构建诊断定位 → 选择仿真或有权限的发布目标 → 启动任务 → 监控、暂停/取消 → 点击 Trace 回到原节点。操作界面不拥有独立执行逻辑。

## 6. 现有源码参考与缺口
已发现 `MRKExecutionEngine`、`MRKExecutionPlan`、`MRKExecutionControls`、`MRKRuntimeCoordination`、`MRKRuntimeSession`、`MRKExecutionJournal`、`MRKExecutionPublishing` 等文件，及 `MOCEngineeringCompiler` 的计划/SourceMap 桥接；这仅证明代码存在，不证明全部语义完成。历史审计提出动态 Region、长期等待、复杂异常/状态机、多专业真实接入仍应验收。

## 7. 最小联合验收
- 同一已构建计划并行运行 10 个不同 SN，数据隔离，完成/失败互不串扰。
- 并发上限 2；取消等待任务不误停止其他任务；资源租约无泄漏。
- 流程中设置重试/超时，运行结果有完整原因链；撤回/回滚不改变已启动实例的固定版本。
- 断点与 Trace 可反向定位共享子流程的不同调用位置；无页面后台运行同样可追踪。
- 实机安全用 PLC/运动卡及专业 Provider 分层验证，CPU 仿真不替代实测。

## 8. 拆分文档及建议顺序
1. 先冻结本文件职责与实例身份；2. 控制/调度/协同/管理/保障五份按契约联动；3. 与现有《流程执行体系 V2.1》合并映射；4. 十大案例和 Windows CI 渐进验收。

---

### 本文档状态与后续要求
- **本文是初简版**：只冻结定位、边界、重点能力与第一批验收要求，未替代现有详细规格，也未完成所有类/接口/枚举列表。
- **后续深化**：以最新 `master` 源码、相关原文档与真实 Windows CI 为依据，逐项形成“需求 → Owner → API/类 → 正反例 → 证据”的闭环。
- **实现分类**：现有声明、部分实现、待实现、模拟验证、Windows CI、实机验收必须分开记录。
