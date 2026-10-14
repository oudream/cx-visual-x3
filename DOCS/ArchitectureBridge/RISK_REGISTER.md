# CxVisual 架构收口风险台账

> 2026-09-20 复核：R-05、R-06 已关闭；R-07、R-12、R-13 继续作为兼容约束保留。Phase 结果见 `MIGRATION_STATUS.md`。

| ID | 风险 | 级别 | 证据/影响 | 控制措施与验收 |
|---|---|---:|---|---|
| R-01 | 把 V6.5 仓库按 V6.4 文档重新迁移 | 高 | 已有 205 个基础用例和三阶段门禁，重搬会造成回归 | 以当前代码为事实源；文档作为目标边界；所有迁移走 Adapter |
| R-02 | 建立第二套 Graph Compiler/Runtime | 高 | `GraphCompiler`、`GraphRuntimeSession` 已覆盖编译、调试、超时和运行 | 公共 `ExecutionPlan` 只包裹 NativePlan；`LegacyGraphRuntimeAdapter` 直接复用旧会话 |
| R-03 | `ReactiveFlow` 与现有 `EventDriven` 被误冻为第五模型 | 高 | Word V1.1 与规格表曾有口径差异 | 正式枚举只保留四模型；EventDriven 通过策略接入，并有自动测试锁定 |
| R-04 | `ExecutionScope` 名义冲突 | 中 | 现有 `Platform.Execution.ExecutionScope` 是取消/Deadline 边界；文档中的 Scope 还含层级语义 | 不改旧类；后续层级 Scope 必须组合旧时间边界并明确 Namespace，不复制取消实现 |
| R-05 | Resource/Policy 直接搬迁破坏 Graph 行为 | 高 | ResourceManager 有排序获取、容量、争用诊断；策略参与计划 Hash 与节点执行 | Phase 3 先抽契约和双向映射，再迁调用点；资源并发/超时用例必须保持通过 |
| R-06 | Channel 与具体协议继续耦合 | 中 | 当前有传输实现和协议模块，但缺统一 Channel/Codec/FrameParser 边界 | 先补接口与 Provider 适配；Modbus 断线重连、回读、审计 Case 作为门禁 |
| R-07 | 物理拆项目造成部署和引用震荡 | 中 | 项目为 .NET Framework 4.8，已有 16 项目及 Host 组合关系 | 第一阶段只按目录/Namespace 收口，不一个目录一个 DLL |
| R-08 | 公共 Platform 吸收 Motion/Vision/CT/Inspection 业务语义 | 高 | 专业 Workbench 已有独立 Runtime 与 UI | Platform 仅保存机制和能力接口；Architecture 门禁禁止反向依赖 |
| R-09 | 跨域参数映射方向不一致 | 高 | 旧 `Dictionary<string,string>` 未在类型上表达方向 | 新契约固定 Input 为 caller→callee、Output 为 callee→caller，并以测试锁定 |
| R-10 | 版本/Deadline/Correlation 在桥接中丢失 | 高 | 工业恢复、审计和追踪依赖这些字段 | StableReference revision、RuntimeContext、Deadline 原样透传；自动测试验证 |
| R-11 | Fire-and-forget/多实例只建字段却无治理 | 中 | 若直接开放 UI，会缺少容量、关闭策略和结果聚合 | 第一轮只建立定义态；Runtime 未实现前不宣称可生产使用；后续以多工位/多 ROI Case 验收 |
| R-12 | 协议实现位置与理想分层冲突 | 中 | 当前具体协议位于 `Platform/Protocols`，但用户明确不保留独立 Drivers 项目 | 区分“程序集物理位置”和“架构职责”；先用 Namespace/Provider 边界隔离，暂不强搬 |
| R-13 | 大范围改名破坏序列化与旧工程 | 高 | Stable ID、GraphKind、资源类型和页面数据均有历史资产 | 不批量重命名序列化标识；迁移器/兼容层先行；Golden 与项目回读门禁常开 |
| R-14 | 新桥接层成为万能 Manager | 中 | 文档明确禁止 UniversalService/CommonManager | 接口按 Compiler/Runtime/Invocation/Resource 分责；Kernel 只协调，不含专业执行语义 |
| R-15 | 当前自动化终端的 x64 .NET Framework CLR/JIT 随机元数据加载异常 | 高 | 同一已通过 PEVerify 的程序集会随机报 `MissingMethodException`/`TypeLoadException`，错误位置漂移；x86 CLR 完整门禁稳定通过 | CI 固定并记录架构；当前以 x86 完整门禁验收；生产 x64 发布前必须在干净主机重跑全门禁与长稳测试，不能以本机异常或一次成功代替验证 |

## 当前退出条件

第一轮只有同时满足以下条件才可结束：

- 全解决方案 0 warning / 0 error。
- 原有 Case 行为不变。
- 新增桥接至少证明旧跨域调用可映射、旧 Graph 可经 Adapter 实际运行。
- Architecture / Phase2 / Phase3 门禁全部通过。
- 未删除业务代码，未新增第二套 Reference、TypeSystem、Graph Runtime。
