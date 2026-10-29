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

## 固定提交与 Windows 真实验收

- MEM 脱离编译：[717c44c32bfc](https://github.com/oudream/cx-visual-x3/commit/717c44c32bfc3b9fd1e23531216cdf00d66ad4a4)，[Decision Windows 40886959550](https://github.com/oudream/cx-visual-x3/actions/runs/40886959550) **Success**；同 SHA 的 Unified Runtime、Engineering Organization、Studio S1、Module Center CPU RAW 工作流均已成功。
- 持久化归档：[337c41ae9ca4](https://github.com/oudream/cx-visual-x3/commit/337c41ae9ca40ade256bd4b89eb3131abb05ae88)，编译期局部重名修复：[80b27ff27868](https://github.com/oudream/cx-visual-x3/commit/80b27ff278683bd6bae1c26847c0eded526befa1)。[Decision Windows 40888831599](https://github.com/oudream/cx-visual-x3/actions/runs/40888831599) **Success**：静态边界检查、.NET Framework 4.8 x64 全量 Build、`ArchitectureSmoke --decision`、默认 ArchitectureSmoke 均成功。
- 对应 ArchitectureSmoke 已真实执行版本化二进制确定性、关闭工程后归档重载、V1 Loop / V2 Shared Content 编译 SourceMap 对齐、载荷损坏与截断拒绝测试。
- 以上证据不等于 Host 可信发布签名、跨进程独立服务部署、设备 HIL 或生产安全互锁验收；这些仍为后续必须交付的独立门禁。

## 可信签名发布门禁（2026-10-30 实装）

- 新增 `MOCSignedPlanRelease` 版本化、固定顺序、领域分隔的待签名字节及签名信封 `Export/Import`；签名覆盖部署 `Scope`、受信 `KeyId`、发布修订号、完整能力版本锁、Decision 不可变 Plan 二进制归档。私钥仅由 Host/签名基础设施持有。
- `MOCPinnedRsaReleaseVerifier` 实施受信公钥环 RSA-2048+ SHA-256 签名验证；**不信任归档内自带的公钥**；未知 KeyId、跨部署 Scope、签名错误必须在专业 Resolver 调用前拒绝。
- `MOCTrustedPlanPublisher` 统一验证签名、重载 Plan、校验静态目录及 Host 版本锁、复核编译期间算法版本，并调用现有 `MRKExecutionPlanPublisher.StagePrecompiled`。MRK 原有权限策略、安全点、运行租约和修订号继续作为唯一运行切换所有者。
- `IMOCTrustedReleaseAdmission` 由可信 Host 注入，负责**主体授权及持久化递增修订号的原子预留**，拒绝跨进程重放；不提供默认放行或内存版生产实现。预留早于 Stage 以保证故障后不倒退，Stage 失败会消耗当前修订号，需要 Host 重新签发更高修订号。
- 测试只使用临时生成的非生产 RSA 密钥及测试型防重放存根；包含签名被修改、跨部署、重复发布、Host 安全点禁止激活、可信签名却版本过期等拒绝场景。
- **仍未宣称完成**：生产 Host 可靠持久账本/密钥管理、独立进程部署、实际设备安全连锁和 HIL 长期试验；只有这些独立门禁满足后，才是工业生产完整发布闭环。
