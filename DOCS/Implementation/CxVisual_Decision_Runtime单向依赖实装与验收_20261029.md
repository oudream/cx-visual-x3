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

## 4. Windows 固定 SHA 验证证据

- 源码迁移 [75194ba5](https://github.com/oudream/cx-visual-x3/commit/75194ba5681f241d8160ba305335e5c7d1357db1)，兼容编译修复 [a7a8501](https://github.com/oudream/cx-visual-x3/commit/a7a850139004ed567e56c377a530a12f4737e65d)、[bc06f5e](https://github.com/oudream/cx-visual-x3/commit/bc06f5e04ce5024761c05cc0879838d99253e376)。
- [Decision Windows 40861427187](https://github.com/oudream/cx-visual-x3/actions/runs/40861427187)：静态依赖检查 **Success**、.NET48 x64 全量 Build **Success**、`ArchitectureSmoke --decision` **Success**；默认回归 **Failure**。
- [Unified Runtime 40861427163](https://github.com/oudream/cx-visual-x3/actions/runs/40861427163)：全量 Rebuild **Success**，默认架构测试 **Failure**。两项失败均定位到并行 CT3D 专业测试 `CT3DAnalysisPersistenceTests.VerifyRoundtrip` 第 132 行 `Single()` 异常；非此次 Decision 专项检查失败，但不能据此宣称全量回归通过。
- 设备 HIL 与工业场景验收未执行。

## 5. 后续仍需推进

- P1：统一静态 MDCDeclarativePlan、版本、SourceMap、动态控制结构与 Runtime.Loader 的不可变交接；减少 Runtime 编译器对工程快照的直接依赖。
- P1：能力注册单一声明与 MPC/MRK 双签名一致性、Studio Context 选型和约束提示。
- P1/P2：发布授权、Unknown/重试安全、专业 HIL 与长期稳定性。

## 6. P1 版本锁定的 Runtime Binder 补强（实装及固定 SHA 验证）

- 扩展已有 `MOCManifestRuntimeBinding.Bind(...)` 的两个入口，增加可选 `expectedCapabilityVersions`（由 Host 提供，覆盖所有实际使用的能力 ID）。
- 严格模式要求 `IMRKVersionedCapabilityResolver`：缺项、额外项、空/非法版本、不支持版本查询和实际版本不一致均在调用 `TryResolve` **前拒绝**；不会执行专业委托。
- 编译结束再次核对每个已知能力版本，拒绝 Resolver 在编译过程中热切换配置/算法造成的版本漂移。未提供版本锁定时保留原来对老 Provider 的支持，但对能够报告稳定版本的 Provider 仍复核其前后版本。
- ArchitectureSmoke `--decision` 增加：版本一致成功、缺项、过期、Provider 无法证明版本、版本能力不具备、解析期间变更等正反例。
- 该约束是**编译阶段版本栅栏**，绝不是设备执行授权、发布事务或真实安全点。发布 Stage / Activate 仍须再次验证环境版本、权限和专业设备状态。
- 实装提交：[236b889c12a3](https://github.com/oudream/cx-visual-x3/commit/236b889c12a34b0b0b1db18f6593cfa03b5739fe)。
- 固定 SHA Windows CI：
  - [Decision 40862863849](https://github.com/oudream/cx-visual-x3/actions/runs/40862863849)：Decision Runtime-free 架构门禁、.NET Framework 4.8 x64 全量 Build、`--decision`、默认 ArchitectureSmoke 全部 **Success**。
  - [Unified Runtime 40862863962](https://github.com/oudream/cx-visual-x3/actions/runs/40862863962)：Runtime Build + concurrency/orchestration 全部 **Success**。
  - [Engineering Organization 40862863667](https://github.com/oudream/cx-visual-x3/actions/runs/40862863667)：工程组织构建及回归 **Success**。
  - [Module Center CPU RAW 40862863681](https://github.com/oudream/cx-visual-x3/actions/runs/40862863681)：专业 CPU RAW 端到端 **Success**。
- 以上通过不替代持续运行、实际设备、安全联锁/HIL 或所有 CI 的验收。

## 7. P1：不可变 Plan 载荷进入唯一 MRK 编译器（2026-10-29）

- 实际源码提交：[687b58f5db77](https://github.com/oudream/cx-visual-x3/commit/687b58f5db775b7f156061ecbc888f4c460f2373)；C# 参数重名的首次 Windows 编译问题已在 [7cbbdad5b77](https://github.com/oudream/cx-visual-x3/commit/7cbbdad5b7730ba083c01b901711e1da42717bcc) 修复。
- 新增 `Runtime/Execution/DecisionBinding/MOCPlanLogicReader.cs`：使用 `MDCDeclarativePlan.Contents` 的 `CopyCanonicalLogicPayload()` 恢复既有 Core MOL V1/V2 载荷；完整闭包仅按 ContentId 只读查找，遇到计划外 Content 明确拒绝，不创建第二套编辑 Graph。
- `MOCEngineeringCompiler` 增加内部 Plan 入口；V1 根/嵌套执行、V2 根/嵌套编排、数据连接、导出和 SourceMap 所需要的 MOL 定义统一从 Plan Reader 取得。**不再在 Plan 路径二次读取 snapshot 中的 Logic 载荷**。原 MOC 直接编译 API 保留兼容。
- `MOCManifestRuntimeBinding.Bind(project,plan,...)` 先校验工程修订及能力目录指纹，在静态合同验证和版本栅栏后调用新 Plan 入口；`Bind(project,manifest,...)` 兼容原路径。
- 真实 `MEMProjectSnapshot` 目前仍用于项目和 Content 的身份、授权引用、StorageRootId、SourceMap 对象地址；它不再充当 Plan 入口的逻辑载荷真源。这是**来源与执行绑定解耦**，尚不意味着无工程上下文的设备独立执行包或进程外发布已完成。
- ArchitectureSmoke 新增对 V1 动态 Loop、V2 自由连接、MRK PlanId 与 SourceMap 一致性、计划载荷防御性复制、工程修改后失效的用例。动态 Loop/Foreach 仍归原 MRK 编译与执行机制。
- 真实 Windows 验收以固定提交 SHA 对应的 GitHub Actions 为准；提交记录不能替代 CI Success。
