# CxVisual · 统一运行体系 · 运行管理 — 完整设计与验收（初简版）

> **版本**：V1.0（初简版 / 设计讨论稿）  
> **日期**：2026-10-20  
> **定位**：供后续细化、源码归位、验收设计使用；**不是完整规格，也不代表功能已经实现**。  
> **约束**：沿用 CxVisual 既有 Foundation / Mechanism / Professional 与现有项目布局，原则上不新增项目；保持 MEM 唯一工程真源、MOC 唯一构建桥、MRK 统一运行执行、MDS 统一运行时数据；C# 与 Studio 共用语义。  
> **证据说明**：目前仅参考对 x3 `master` 的定向代码查看和历史规格基线；“建议/待实现”不能写成已验证的源码行为。

## 1. 定位
运行管理管理**实际发生的一次执行**：实例创建、所有者、会话、状态、查询、策略绑定、保留及结束清理。它不编辑工程定义、不进行审批发布、不管理软件包版本仓库。

## 2. 生命周期与身份
- 工程：`ProjectId / EngineeringRevision / ContentId`（MEM）。
- 计划：`PlanId / SourceMap`（MOC）。
- 运行：`RunId / FlowId / TaskId / InvocationPath / AttemptId`（MRK，正式名称需与源码核对）。
- 数据：`DataSpace / Frame / Stack / Heap`（MDS）。
- 观测：`TraceSequence / Timestamp / Origin`（MRK→诊断）。

运行实例从不可变计划/修订创建；已经启动的实例不能因为 Studio 编辑同一 Content 而悄悄改变语义。

## 3. 最小功能
| 范畴 | 要求 |
|---|---|
| 创建 | 校验计划可执行、权限、执行参数、唯一 RunId |
| 查询 | 按工程、SN、时间、状态查活动或历史实例 |
| 状态 | Created/Ready/Running/Waiting/Paused/Terminal，转换原子一致 |
| 会话 | 工作者生命周期、断开/重连、相关资源闭环 |
| 策略 | 超时、保留期、并发、优先级、隔离边界的实例级固定快照 |
| 收尾 | 清理资源、完成结果、日志关联、历史存档 |

## 4. 例子
两个 SN 在同一个 Content 逻辑定义上执行：`Run-A` 发生 AI 超时，`Run-B` 成功。工程定义相同、计划可相同，但 `RunId`、MDS 上下文、控制命令、Trace、结果完全独立。用户取消 A 不得取消 B。

## 5. 项目归位与已有资产
主 Owner：`Mechanism.Runtime`；复用 `MRKRuntimeSession`、`MRKExecutionIdentity`、`MRKExecutionEngine`、`MRKExecutionJournal`；运行数据由 `Mechanism.Data` 提供，历史归档接口可由治理/诊断读取。Studio 管理台只是 ViewModel。

## 6. 验收与反证
- 多实例并行，RunId 与输入输出强隔离；重复 RunId 拒绝。
- 工程编辑/重新构建/发布新版本均不能修改既有运行实例的固定计划。
- 关闭 UI 后 Headless 运行不停止（除非明确配置绑定到 UI 会话）。
- 任务结束后 Lease/Frame/订阅可回收；历史记录可追溯而不是无限常驻内存。
- 失联与已失败、取消与已完成须区分；查询状态与 Trace 一致。

## 7. 待细化
统一名词 Run/Task/Work/Flow/Session；定义持久恢复及保留周期，避免让同一概念在 MOC、MRK、Studio 拥有三套 ID。

### 相关体系交叉引用
- 《CxVisual_统一运行体系#总体架构——完整设计与职责划分_V1.0.md》
- 《CxVisual_工程与逻辑体系#逻辑体系——完整设计与验收_V1.0.md》

---

### 本文档状态与后续要求
- **本文是初简版**：只冻结定位、边界、重点能力与第一批验收要求，未替代现有详细规格，也未完成所有类/接口/枚举列表。
- **后续深化**：以最新 `master` 源码、相关原文档与真实 Windows CI 为依据，逐项形成“需求 → Owner → API/类 → 正反例 → 证据”的闭环。
- **实现分类**：现有声明、部分实现、待实现、模拟验证、Windows CI、实机验收必须分开记录。
