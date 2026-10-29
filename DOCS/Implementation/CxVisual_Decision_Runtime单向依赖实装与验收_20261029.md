# CxVisual Decision → Runtime 依赖单向化：真实源码迁移与验收记录（2026-10-29）

> 本文记录实际工程迁移，不替代 Decision V2.0 #0～#6 正式架构或动态验收表。代码与 CI 状态以提交 SHA / Windows Run 为准。

## 1. 实现内容

1. `Mechanism/Decision/CxVisual.Mechanism.Decision.csproj` **移除对 Mechanism.Runtime 的 ProjectReference**。保留 Core/Data 基础项目引用；无 MRK 执行委托、Resolver、执行状态的静态源码留在 Decision。
2. 原 `MOCEngineeringCompiler.cs` 真实分拆：
   - `Decision/MOCFlowBuilder.cs`：保留既有 `MOCFlowBuilder`、`MOCWiringFragment`、`MOCBlockAlignment`，继续使用 Core 的 MEM/MOL 工程编辑事务；
   - `Runtime/Execution/DecisionBinding/MOCEngineeringCompiler.cs`：保留原 `MOCEngineeringCompiler` 的编译/Resolver/分支/Loop/嵌套/Trace 运行绑定代码，继续复用唯一 MRK 编译器及已有执行计划；未创建替代运行器。
3. MOCBuildService、MOCProjectRelease、MOCManifestRuntimeBinding、MOCBindingContractInspector 的**原始实现**移动至 Runtime/Execution/DecisionBinding，保留公开 C# 命名空间和方法签名（程序集 Owner 变化）。
4. `MOCCapabilityCatalog` 仍留 Decision 并将只读 `Func<string,bool>` 可用性投影构造器公开。原 `IMRKCapabilityResolver` 构造入口改为 Runtime 侧强类型 `MOCCapabilityCatalogRuntimeFactory.Create(providers,resolver)`。Studio、ArchitectureSmoke 等实际消费者已经改用该工厂；其检查逻辑仍只查询 TryResolve/不执行 Provider。
5. Runtime.csproj 建立**Runtime → Decision** 项目引用。SourceMap 原内部构造继续保持内部可见，只授权 Runtime 的 friend assembly；未开放不必要的公网 API。
6. Decision Windows CI 对**全部** Decision 根目录 C# 源文件及 csproj 执行 Runtime 依赖回流门禁；ArchitectureSmoke --decision 增加程序集 Owner、正反项目引用及 Runtime Adapter 定位断言。

## 2. 兼容边界

- `MOC*` 既有 namespace 保留为 `CxVisual.Mechanism.Orchestration`，避免一次性更换所有公开符号的名称；**程序集物理位置已实际变化**，采用程序集反射或二进制绑定旧 Orchestration DLL 的外部程序需要显式迁移/重编译。
- 旧 `new MOCCapabilityCatalog(providers, IMRKCapabilityResolver)` 构造调用需要替换为 RuntimeFactory.Create；此为明确 API 迁移，不通过反射/dynamic/第二套 Registry 伪装兼容。仓库内已知消费者与测试已同步。
- 真正可序列化纯静态 Plan → MRK Loader 的完整独立绑定、生产发布、复杂跨专业约束推演及设备 HIL **不在此次完成范围**。本次改为真实工程无反向依赖，并不宣称全部 Decision V2.0 十大能力已完成。

## 3. Windows 验收门禁

- `Build/Verify-Structure.ps1` 与 `Build/Verify-OwnerDependencies.ps1`，必须能报告完整项目 DAG 无环。
- `CxVisual.Unified.sln` .NET Framework 4.8 x64 Release、`CxVisual.Tests.ArchitectureSmoke.exe --decision` 和默认回归。
- Studio、ModuleCenter、专业 Vision、TestCenter 相关 CI，用于检查旧 MOC 编译与算法调用方。
- 真实 CI 状态应记录固定 SHA、运行 ID 与结果；如失败必须修复，不能仅凭移动文件声称完成。

## 4. 后续仍需推进

- P1：统一静态 MDCDeclarativePlan、版本、SourceMap、动态控制结构与 Runtime.Loader 的不可变交接；减少 Runtime 编译器对工程快照的直接依赖。
- P1：能力注册单一声明与 MPC/MRK 双签名一致性、Studio Context 选型和约束提示。
- P1/P2：发布授权、Unknown/重试安全、专业 HIL 与长期稳定性。
