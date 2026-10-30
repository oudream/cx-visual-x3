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

## 可信签名发布与Host持久防重放验收（2026-10-30）

**真实实现与固定提交**

- [51919181faf8](https://github.com/oudream/cx-visual-x3/commit/51919181faf81f121e4f3ddc4af4e9df7f143480)：`MOCSignedPlanRelease` 提供可导出的规范签名信封；`MOCPinnedRsaReleaseVerifier` 基于 Host 固定的 RSA-2048+ 公钥验签；`MOCTrustedPlanPublisher` 完整校验签名/Scope/静态目录/专业版本之后通过**唯一** MRK Publisher Stage。未设置可信验证器和 Host Admission 不提供发布入口。
- [f7ba82f6a0f3](https://github.com/oudream/cx-visual-x3/commit/f7ba82f6a0f3248e555b82d1c4185ba291585fa2)：将签名测试改为显式 2048 位测试密钥；[f3ac302dceeb](https://github.com/oudream/cx-visual-x3/commit/f3ac302dceebc609051c570de558bdceb9c89f9e) 修复并行 Vision 自测缺少 `CultureInfo` 阻断的编译。
- [Decision Windows 40972808600](https://github.com/oudream/cx-visual-x3/actions/runs/40972808600)，固定 SHA `f3ac302dceeb`：.NET Framework 4.8 x64 全量构建、Decision 专项、默认 ArchitectureSmoke 全部 **Success**；验签、篡改拒绝、跨部署 Scope、重放版本、专业能力版本不一致、Host 安全点未就绪均已进入专项回归。
- [d2c775e8c4d8](https://github.com/oudream/cx-visual-x3/commit/d2c775e8c4d8ac55be09efebe4ced175e8e7e815)：`Applications/Runtime.Host/MRHFileTrustedReleaseAdmission` 真实 FileShare.None 跨进程独占、首次显式 CommissionNew、单调追加、`Flush(true)`、SHA256 链式校验、撕裂记录拒绝；附 CLI 跨进程测试入口，不对外默认放行。
- [Unified Runtime Windows 40972856365](https://github.com/oudream/cx-visual-x3/actions/runs/40972856365)，固定 SHA `d2c775e8c4d8`：全量 Rebuild、编排回归、Runtime Host 功能与新增账本跨进程重启/损坏拒绝全部 **Success**。

**边界与未完成**

- 账本 SHA256 链仅用于发现损坏与意外断裂，不能替代 OS ACL、防删除保护、可信安全存储或外部签名；攻击者若拥有账本目录修改/删除权限，仍可伪造或重置记录。生产 Host 必须确保初始化、ACL、可信备份和恢复链。
- 签名应用于纯 Plan 与声明版本；真实设备授权、安全联锁、资源生命周期、Worker/HIL 及工厂级灾难恢复仍须按专业工程分别验证。不把软件级 Success 误标为设备 HIL 完成。

## 2026-10-30：正式 Runtime Host 的可信公钥与签名包装配

- 新增 `Applications/Runtime.Host/MRHTrustedReleaseHost.cs`。由正式 Host 注入签名者公钥证书路径、单独受保护的 SHA-256 证书指纹、KeyId、部署 Scope、主体授权函数、已委托的专业 Resolver/目录和既有 `MRKExecutionPlanPublisher`。
- 启动时仅接受与预置证书指纹一致、时间有效、RSA-2048+ 的公钥；发布包自带 KeyId 只能在该受信字典中查找，不能带入另一把公钥绕过 Host 信任。
- `StageFile` 有界读取已签名的 Plan 包，调用唯一的 `MOCTrustedPlanPublisher.VerifyAndStage`。另提供**显式** `TryActivate` 并委托 MRK Host 安全点；`StageFile` 绝不自行激活。
- `MRHFileTrustedReleaseAdmission` 仍要求部署前显式可信初始化，正常运行发现缺失、损坏账本直接拒绝，不会重置版本水位。
- 同步加入 `release-trusted-smoke` 真实 Host 端到端测试入口和 Unified Runtime Windows CI：临时测试证书、真正 Decision Plan、已签名归档、专业 Resolver、MRK Stage/Activate、错误证书指纹、篡改、跨实例重放及账本撕裂反证；不使用生产私钥或外设。
- 安全边界：**预置证书指纹与账本所在目录的 OS ACL / 文件删除保护，由运维部署提供**；单纯公钥指纹不保护可由攻击者任意修改的 Host 配置，SHA-256 链不具备抗特权攻击者篡改能力。还未具备机台设备联锁/真实 HIL 或无人值守产线发布许可。

## 2026-10-30：可信Host公开能力与Windows固定提交验收

- [2257901eaa2c](https://github.com/oudream/cx-visual-x3/commit/2257901eaa2c0f99a84fb59d52b1bf770554e3dd)：首次将 Host 证书指纹、签名文件装载、真实 MRK Stage/Activate 及临时证书完整测试接入应用程序集；初次测试暴露无权主体曾在原子预留前查询专业 Resolver 的安全边界，已后续修正。
- [d0080a6ed0ac](https://github.com/oudream/cx-visual-x3/commit/d0080a6ed0ac863b0a8866ad6d87f164ae3b1705)：向 `IMOCTrustedReleaseAdmission` 增加只读 `IsAuthorized`，在 Runtime Binder 和专业 Resolver 访问之前拒绝无权主体；原 `TryAuthorizeAndReserve` 继续在 Stage 前执行可持久的原子二次授权和版本预留。
- 固定 SHA `d0080a6ed0ac` 对应 [Decision Windows 40984207218](https://github.com/oudream/cx-visual-x3/actions/runs/40984207218)、[Unified Runtime Windows 40984207370](https://github.com/oudream/cx-visual-x3/actions/runs/40984207370) 均 **Success**：全量 .NET48 编译、ArchitectureSmoke 专项与默认回归、Host 受信公钥→真实签名 Plan→MRK Stage/安全点 Activate、无权限主体提前拒绝、跨实例水位拒绝、损坏账本拒绝均通过。
- [6c0d072955af](https://github.com/oudream/cx-visual-x3/commit/6c0d072955afce0808ebb9c5304d239f3380d0a3)：正式 Host 的 `StageFile` 移除 `allowUnverifiedLegacyContracts` 开关，强制严格能力合同；对声明 KeyUsage 的证书要求 `DigitalSignature`。对应测试 [3184c76bcf03](https://github.com/oudream/cx-visual-x3/commit/3184c76bcf03331c2f9a5602ee05130fc9783335) 新增仅限密钥加密用途证书的拒绝反例。其 [Unified Runtime Windows 40984569527](https://github.com/oudream/cx-visual-x3/actions/runs/40984569527) **尚待最终 CI 判定**，不能计入上述全绿结论。
- 安全部署前置条件：外部可信配置必须保护 Scope、证书路径、证书 SHA-256 指纹、KeyId、主体授权与安全点策略；账本目录必须施加 OS ACL、防删除及可信恢复。可信证书只提供发布签名者身份，真实机器联锁/设备 ACK/HIL 和长时间故障恢复仍需机台验收。代码并未内置测试私钥作为生产信任锚。
