# Decision V2.0：不可变 Plan 脱离 MEM 工程的真实 Runtime 编译闭环（2026-10-29）

## 目标与唯一真源
- `MDCDeclarativePlan` 保留 Core 生成的 V1/V2 规范载荷，并冻结每个 Content 的 StorageRootId 与引用身份；不持有可变 MEMNode 或专业执行 Delegate。
- 新增 `MOCProjectView` 只读统一来源适配：Legacy 使用 MEMProjectSnapshot；Detached 使用 MDCDeclarativePlan，绝不伪造 MEM Snapshot 或新建第二套 Graph。
- 原 `MOCEngineeringCompiler` 共用唯一 MRK 编译/SourceMap 管线，提供 `Compile(MDCDeclarativePlan, IMRKCapabilityResolver)`；Plan 模式使用 `MOCPlanLogicReader`，不读取当前工程 Logic。
- `MOCManifestRuntimeBinding.Bind(plan,catalog,resolver,...)` 是无 MEMProjectSnapshot 的 Runtime 入口；必须提供 Host 全能力版本锁定，静态目录不一致/版本缺失或变化在 Resolver 执行前拒绝。Host 的运行授权、PlanSlot Stage/Activate、设备安全互锁仍为 MRK/专业的独立职责。
- `MDCDeclarativePlan.RequireIntegrity` 按与 Core Manifest 相同的 SHA256 内容合同核验每个 Logic Aspect，并校验 Plan 闭包及被引用 Content 关系。避免纯运行模式因载荷不一致使用错误的对象与语义。

## 真实回归范围
- 在 Core 工程会话关闭后，直接对冻结的 V1 动态 Loop 和含共享 V2 Child 的 Plan 进行独立 MRK 编译，与原 MEM 路径比较 PlanId、SourceMap 和原 BlockId。
- 反证：无版本锁定、能力目录变化、专业版本漂移，一律在执行委托前拒绝。
- CI 门禁：`Decision Windows .NET48 Acceptance` 的 `--decision`、默认 ArchitectureSmoke；`Unified Runtime Windows TDD` 与工程组织 Windows CI。

## 剩余工业级门禁
- 纯 Plan 的 **可持久化有版本发布包**及签名/防回滚、离线跨进程加载、MRK Host 权限与设备资源租约、故障恢复、HIL 安全验收仍未完成，不得用此一次编译通过来取代上述工作。
- SourceMap 仍沿用原 MRIObjectAddress 的项目/资源身份合同；Detached 是脱离 MEM 实时快照，不是跨项目对象身份重写。

## 版本化 Plan 归档（后续实装）
- `MDCPlanArchiveCodec.Serialize/Deserialize` 提供固定格式 v1 的有界二进制载荷，内容包括 Core/Decision 来源 Manifest、完整 V1/V2 MOL 规范逻辑、Content 资源地址、引用身份与专业能力使用声明。
- Import 后重建同一个 `MDCDeclarativePlan`，由 Runtime 的独立 Binder 接收，关闭 MEM Session 后仍能得到实际 MRK Plan/SourceMap。
- 归档采用 SHA-256 检测意外损坏，并重新计算来源 Manifest 指纹，逐 Content 核验 Logic 字节、引用及能力声明闭包；对截断、版本错误、格式越界和来源不匹配拒绝加载。
- **SHA-256 不是发布者签名。** 工业生产发布必须由 Host 附加可信签名、反回滚、权限、设备互锁与 Stage/Activate 授权；当前不宣称这部分已完成。
- ArchitectureSmoke 在关闭工程会话之后执行确定性归档、加载、V1 Loop/V2 共享 Content 语义与 SourceMap 验证，以及损坏/截断载荷负例。
