# CxVisual 统一配置体系 V2.4 · 阶段 A 实施与可验证验收矩阵

> 日期：2026-10-16。Owner：Foundation.Data.Configuration + Mechanism.Data.Configuration。分支：master，直接中文提交，不新增项目或平行配置框架。
> 本页是 [V2.4 主规范](CxVisual_统一配置体系_完整设计与验收_V2.4.md) 在阶段 A 的**实际实现证据**，不是以文档自我宣告通过。主规范原稿中记录的旧 HEAD 是编制时快照；此处按最新源码和 Actions 更新。
> A 包范围：层级定义持久化、Schema 类型契约、文件事务与生效恢复、访问权限和敏感信息防护；跨机器发布/远程实时通知/真实设备联锁/完整 GUI 不属于 A 的验收范围。

## A 包目标与改动记录

| ID | 原目标与修复行为 | 实际源文件 | 自动测试入口 | 判定与剩余边界 |
|---|---|---|---|---|
| A0 | 完整 V2.4 文档入库、协同入口同步、保留 V2.2/V2.3 历史 | `DOCS/Implementation/*V2.4*.md`、`README.md`、`DOCS/00_*` | GitHub 路径核查 | 已入库；V1.3 原始独立文件尚未找到，不冒充逐字合并 |
| A1.1 | 原子层档案随同一物理聚合修订保存定义；更换 Name/Kind/Id/Priority/模型版本不静默重解释 | `MHSConfigAtomicGroup.cs`、`MVBConfigAtomicLayerProfile.cs` | `VerifyPhaseAContracts`、`VerifyAtomicLayers` | 代码及回归已进入配置 CI；只保证单组物理原子性 |
| A1.2 | 原子层显式升级：预览合成、验证，移除有值的层必须映射，保留旧任务快照；新修订按 Apply 才生效 | `MVBConfigAtomicLayerProfile.cs` | `VerifyPhaseAContracts` | 已覆盖重排优先级/拒绝丢值；真实跨机迁移和设备生效不在 A |
| A1.3 | 独立层持久化权威定义并检测漂移；不谎称跨独立文件原子升级 | `MVBConfigLayeredProfile.cs` | `VerifyPhaseAContracts` + 原有层级测试 | 推荐显式 `profileId`；旧构造函数保留首层 Scope 作为兼容身份锚点，跨 Scope 重组应使用原子档案 |
| A2.1 | Section 身份/SchemaVersion/字段元数据/递归深层集合模型指纹，已有数据缺契约或不匹配则拒绝 | `MHSConfigSection.cs`、`MHSConfigStorage.cs` | `VerifyPhaseAContracts`、`ConfigurationIntegrationTests` | 保序 JSON 底座保持；不声称任意老 Schema 自动转换 |
| A2.2 | 离线无版本 JSON 显式接管：外部给出原文件哈希、强类型验证、备份原件、建立 Version/Effective/Schema | `MHSConfigService.cs`、`MHSConfigStorage.cs` | `ConfigurationIntegrationTests` | 只适用于无独立历史与无 Effective 标记的旧 JSON；完整 Schema 迁移链另列长期任务 |
| A2.3 | 提交与生效 CAS、持久 Journal、一致的版本错误结果；旧会话不能错误生效或发通知 | `MHSConfigStorage.cs`、`MHSConfigSection.cs` | `VerifyPhaseAContracts`、`VerifyEffectiveJournal` | 本机同盘共享存储并发；跨网络发布仍暂缓 |
| A2.4 | 文件写故障、缺失有效标记、孤立历史修订：不自动晋升未知修订；失败后不污染真源 | `MHSConfigStorage.cs`、`MHSConfigSection.cs` | `VerifyPhaseAFaults`、`VerifyDurableEvents` | 故障注入为文件锁和遗留文件模拟；物理断电/磁盘损坏实测尚无证据 |
| A3.1 | 授权 Preview/Commit/Import/Restore 逐次复核权限；撤销 ReadSensitive 或 Edit 不留后门 | `MHSConfigAuthorizedSession.cs` | `VerifyPhaseASecurity`、`VerifySecurity` | 仅当宿主不向非可信模块泄漏裸 Service/Section 才形成权限边界 |
| A3.2 | 未经 ReadSensitive 时 Apply 的 Guard 错误、校验异常文本不得透出凭据 | `MHSConfigAuthorizedSession.cs`、`FCMConfigSchemaBuilder.cs` | `VerifyPhaseASecurity`、`SecurityAndCatalog` | 全链路脱敏需要持续以新的自定义验证器、插件和设备返回文本做攻击测试 |

## 证据、操作与门禁

- 独立入口：`Tests/ConfigFoundationSmoke/Program.cs`，执行 `ConfigurationIntegrationTests`、`ConfigV13ExtensionTests`、`ConfigArchitectureCompletionTests`、`ConfigDeveloperExperienceTests`。
- 完整入口：`CxVisual.Unified.sln` Release/x64 与 `Tests/ArchitectureSmoke/Program.cs`，兼容 .NET Framework 4.8。
- 最近一次所有三项 Windows CI 均成功的代码 SHA：`53cbef4bf260e5090f62c63c6ac589265583d9ed`，独立配置 [运行记录](https://github.com/oudream/cx-visual-x3/actions/runs/38869250430)、完整解决方案 [运行记录](https://github.com/oudream/cx-visual-x3/actions/runs/38869250462)、TestCenter [运行记录](https://github.com/oudream/cx-visual-x3/actions/runs/38869250364)。
- `39af66a098e942fad69a96574d9fbfb7c2a634c9` 的独立配置 [CI](https://github.com/oudream/cx-visual-x3/actions/runs/38869510907) 与完整 [CI](https://github.com/oudream/cx-visual-x3/actions/runs/38869510952) 均成功，增加了递归 Schema 指纹。后续提交和本矩阵的最新 CI 需以对应运行页面为准，**不能继承别的 SHA 的通过结论**。
- `config-l1-ci.yml` 中通用共享烟测标注 `continue-on-error: true`，不是强制全门禁；故不能把绿灯解读为所有工作流全部通过。
- 测试不证明 GUI 手工交互、跨机/跨进程消息、真实设备联锁、Schema 全格式保序迁移链、物理断电持久性及长期性能稳定性；这些保留为后续独立 Gate。

## 开发者兼容性及操作约束

1. 现有配置底座仅在两个 Data 项目中实现，不新增 ConfigManager。若遇 `CONFIG_SCHEMA_METADATA_MISSING` 或 `CONFIG_SCHEMA_MISMATCH`，不要删除当前文件、历史或 marker 重试；原数据必须备份并由明确的升级程序处理。
2. 只有确认为无版本、无历史的旧 JSON，才能由可信宿主调用 `MHSConfigService.AdoptUnversionedSection<T>(identity, expectedSourceSha256)`，使用预先核实的原文件 SHA-256。此操作不是对任意旧格式的自动迁移。
3. 原子层配置应使用完整 `profileId`；显式升级通过 `UpgradeDefinitionAsync`，保存成功后需按生效边界切换，然后用新定义重新打开档案。独立层档案只保证定义本身的提交，不保证所有来源文件同时切换。
4. 工业敏感值必须由授权会话读取，不能把裸 `MHSConfigService` 暴露给不可信插件。验证器异常仅返回稳定错误文案，不把原始异常文本放入普通错误报告。
5. 接下来 B/C/D 包按主规范开展；本矩阵任何新版本应更新实际 SHA、CI 链接、失败与恢复证据、明确未验证边界，禁止以新增类型个数代替验收。

## 特别说明

此文件是阶段 A 持续更新的验收基线。最终门禁以**同一个最后提交 SHA** 的独立 Config CI、完整构建及其测试日志为准；如果 CI 未结束或失败，必须先修复，不能将 A 标记为“最终验收通过”。
