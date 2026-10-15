# CxVisual 配置V1.1 · V6.4类型归属核查、案例驱动及稳定性加固

> 依据：库内《CxVisual_Foundation与Mechanism_统一架构与对象API设计规格_V6.4_对象归属审核.xlsx》的 F14/F15/F18、M39 相关条目；cx-visual-x3实际源文件和Windows CI。截图标题中的“04_CxVisual Foundation与Mechanism 统一架构与对象API设计规格 V6.4.xlsx”未作为可执行附件提供，**不可声称已逐字节核对截图那一份工作簿**。规格的“增构候选·待查重”不等于已批准新增，“原有·待核”不等于迁移已通过。

## 0. 结论

- 当前`CxConfig*`等名字是代码公开名，**并非严格执行V6.4 C列的建议前缀体系**。命名暂时保留以避免破坏L1–L4调用者；不能把已有同义能力再以`FCM*`造一套平行类。
- **类名前缀与类的权威Owner是两个问题**。先用唯一功能契约及源码表建立映射；需要迁移到V6.4命名时，整仓一次性变更/兼容期，不允许两个等价权威实现长期共存。
- L1–L4现有能力不等于V6.4全部设计完成。特别是`FCMConfigKey`、`FCMConfigSchema`、`FCMSettingsSnapshot`、`FCMSecretReference`与实际实现**不是简单一对一等价**。
- 本次V1.1先使用**反例测试**修复Schema版本读取顺序、被高优先级配置遮蔽的低层编辑以及分开读取Value/Revision的并发不一致问题。

## 1. V6.4建议类型 → 当前代码映射与裁决

| V6.4 C列候选 | 建议Owner/性质 | x3真实代码 | 裁决 / 还欠什么 |
|---|---|---|---|
| `FCMConfigKey` | F15逻辑键/作用域基础值 | `CxConfigManager`使用`string id` | **未实现同等身份值**；未来需要固定Id/Scope规范，避免依靠字符串拼接 |
| `FCMConfigLayer` | F15基础层值 | `CxConfigLayerSpec`、`CxConfigLayerOverrides` (Mechanism.Core) | 存在概念对应但不等同；纯数据可下沉F15，管理逻辑仍属M层 |
| `FCMConfigMergeUtil` | F15纯静态合并 | `CxConfigLayeredProfile<T>.ResolveWith` | 目前混合合并计算与Profile管理；下一轮按接口去重后拆纯计算，不双建 |
| `FCMConfigParsingUtil` | F15/F14基础解析 | `CxConfigJsonFile` (Foundation.Data) | 只支持Json路径，不等于多格式ParsingUtil |
| `FCMConfigPathUtil` | F15基础配置键路径计算 | `Path.Combine`与Id检查分散 | 未完整实现；与F18文件路径安全职责分别考虑 |
| `FCMConfigSchema` | F15约束基础描述 | `CxConfigDescription`、`CxConfigItemAttribute` (Mechanism.Core) | **部分重叠**；F15负责通用schema值，M04负责类型/属性元数据与UI映射 |
| `FCMConfigValue` | F15键/值/来源 | 强类型`T`属性+`CxResolvedConfig<T>.Origins` | 功能局部存在；按需求保留强类型，不为所有字段强制装箱 |
| `FCMEnvironmentKeyUtil` | F15环境键静态计算 | 无同等独立类型 | 暂不为名称单独造类 |
| `FCMSecretReference` | F15保密信息引用 | 当前配置DTO允许普通string | **缺失**；未来凭据只能保存引用不能明文进入发布包 |
| `FCMSettingsSnapshot` | F15无状态快照值 | `CxConfigSnapshot<T>` / `CxConfigAtomicSnapshot<T>` | 已有互不相同语义：前者配置值+版本+保存状态，后者高频值+版本；不可混成一个 |
| `FCMConfigHelper/IniFileAdapter/IniParser/KvText` | F15旧代码/适配 | 新JSON管理器不引用旧版 | 旧资产待兼容审查；不应以旧Helper替代统一配置入口 |
| `FFSAtomicFileWriteUtil` | F18通用原子文件写 | `CxConfigJsonFile.Save`和`CxConfigReleaseReceiver.WritePointer`各有一次实现 | 应提取**真实通用**原子写工具，避免各模块重复的文件保障逻辑 |
| `MVDManifest` | M39工程发布清单 | `CxConfigReleaseManifest` | 后者是**配置发布清单**，未来作为工程发布Manifest的可组合资源，不当作全工程唯一Manifest |
| `MVDRollbackPlan` | M39发布回滚计划 | `CxConfigReleaseReceiver.Rollback` | 有实际单目标上版指针切换；未实现完整跨机器RollbackPlan |
| `MVDMigrationProvider` | M39发布/工程迁移扩展 | `Register<T>(..., migrate:...)` | 只覆盖L1单配置文档Schema迁移，不等于工程级迁移Provider |

其他已实现服务的项目归属：
- `CxConfigManager`、`CxConfigLayeredProfile<T>`：Mechanism.Core，统一注册/变更/分层管理（概念涉及M04/M07；最终子方面根据审核表）。
- `CxConfigRuntimeSession<T>`、`CxConfigAtomicSlot<T>`：Mechanism.Runtime，M30。
- `CxConfigReleaseBuilder/Receiver/Publisher`：Mechanism.Lifecycle，M39。
- `CxConfigEditorDialog`：Mechanism.Presentation.Wpf，仅操作草案和受控修改，不持有设备引用。
- Automation/Vision/CT3D：仅业务`Options`类型和`Validate`规则，禁止另建`ConfigHelper`。

## 2. 案例优先：测试不是只有单元方法，也要代表真实场景

| Case ID | 工业场景 / 操作 | 必须看到的结果 | 实际验证点 |
|---|---|---|---|
| CFG-001 | Vision检测初次运行→修改阈值→关闭重新加载 | 无需手工Load/Save，配置与版本一致 | `ConfigL1Tests.DefaultAndReload` |
| CFG-002 | Automation两台设备独立设定站号/IP/端口 | 配置实例隔离、无串值；业务规则拒绝越界 | `ConfigProfessionalL1Tests.ThreeProfessionalTypesShareOneManager` / `MultiMachineIsolationAndReopen` |
| CFG-003 | Vision8位图像阈值设置256 | 拒绝非法组合，版本不改变 | `ConfigProfessionalL1Tests.CrossFieldRulesRejectUnsafeOrInconsistentParameters` |
| CFG-004 | CT重建FDD≤FOD、投影为0 | 拒绝非法几何；不会污染有效快照 | 同上专业交叉校验 |
| CFG-005 | 低优先级Machine层被Recipe覆盖后编辑 | **编辑仍保存到目标Machine层**；移除Recipe覆盖后正确显露 | `ConfigL2L4Tests.ShadowedLayerEditAndUnmask`（V1.1新增） |
| CFG-006 | 模板引用成环 | 直接拒绝，不改已保存版本 | `ConfigL2L4Tests.LayerInheritanceAndSourceTrace` |
| CFG-007 | 历史JSON数据结构与新`T`不兼容 | **先查Schema后触发显式迁移**；保留原文备份 | `ConfigL2L4Tests.LegacyIncompatibleSchemaMigration`（V1.1新增） |
| CFG-008 | 并发修改+高频读取 | 一次快照中Value与Revision属于同一版本、外部修改不影响内部 | `ConfigL2L4Tests.AtomicConfigSnapshotConsistency`（V1.1新增） |
| CFG-009 | 设备安全配置缺失或文件损坏 | fail-closed，不擅自用默认值启动 | `ConfigL1Tests.MissingRequiredFileFails` / `CorruptOrUnknownSchemaDoesNotOverwrite` |
| CFG-010 | 保存故障或两个进程同时编辑 | 老文件保护；发现陈旧Revision拒绝覆盖 | `ConfigL1Tests.SaveFailureDoesNotPublish` / `ExternalConcurrentWriterConflict` |
| CFG-011 | Vision采图/CT重建执行期间改参数 | 当前任务不切换，下一任务或重启边界按策略生效 | `ConfigL2L4Tests.TaskBoundariesAndSafeApplication` |
| CFG-012 | 多字段高频参数一致读取 | 单次原子快照内字段和值的版本一致 | `ConfigL2L4Tests.AtomicRuntimeValuesAreConsistent` |
| CFG-013 | Studio自动生成三专业编辑器、非法输入、保存 | UI通用，无需每专业自己实现普通WPF属性编辑器 | `ConfigWpfL1Tests.Run` (STA) |
| CFG-014 | Studio打包→Runtime独立进程验版→双目标回滚 | Revision、Release ID可核验，篡改包拒绝 | `ConfigL2L4Tests.ReleaseDistributionAndRollback` |
| CFG-015 | 专业真实设备执行ACK与安全联锁 | 只能在实际Provider完成Apply/Verify后确认为硬件生效 | **未实现/未验收**；不得拿软件ActiveRevision代替设备ACK |
| CFG-016 | 多进程多资源事务、可信网络/签名、容灾 | 正式工业发布应可审计、可防篡改、故障补偿 | **未实现/未验收**；独立排期 |

**TDD方式**：先添加CFG-005/007/008反例，再修改Foundation/Mechanism代码；必须使之前L1～L4所有用例全部回归。改进用例实际状态应通过GitHub Actions **该提交SHA**核验，写入文档不能自行视为通过。

## 3. 本次V1.1真正修改了什么

1. `Foundation/Data/CxConfigJsonFile.cs`：`Save<T>`首先检查磁盘文档头的Revision，不在旧Schema迁移保存时对旧Data按新版T强制反序列化；新`ParseDocument<T>(json,path)`在版本匹配后才用。
2. `Mechanism/Core/CxConfigManager.cs`：`Load`先读取原始JSON头，发现旧版则将原始字节文本交给显式`migrate`；添加`CxConfigSnapshot<T>`与`GetSnapshot<T>(id)`，同一锁内捕获Value/Revision/Saved。
3. `Mechanism/Core/CxConfigLayeredProfile.cs`：低优先级层编辑以**该层及其前置层的解析结果**为草案，防止高层覆盖屏蔽本层编辑；`Resolve()`统一专业最终值校验。
4. `Mechanism/Runtime/CxConfigRuntimeSession.cs`：使用`GetSnapshot<T>`统一取得Value/Revision/Saved，不再三次分开查询后人工比较。
5. `Tests/ArchitectureSmoke/ConfigL2L4Tests.cs`：新增3个可独立失败的反例，继续复用Automation/Vision/CT3D三个专业案例。

**重要限制**：`CxConfigSnapshot<T>.Value`是隔离克隆，可在外部修改而不影响Manager；它**不是**持久只读的硬实时指针。不承诺本次修复了跨文件批量事务、跨多个层一次性Snapshot事务、真实设备Apply/ACK或签名鉴权。

## 4. 下一阶段的精确优先级（先测试，再批准新类型）

1. **契约与命名统一（按V6.4）**：评审`FCMConfigKey`、`FCMConfigSchema`、`FCMConfigLayer`、`FCMSettingsSnapshot`与现有对象的**唯一Owner、相同/不同语义、迁移代价**；落在现有项目，不新增平行配置程序集。
2. **配置值解析一致性**：分层跨层一致性/版本向量，在多线程并发更新时保证Resolved最终值可追溯；覆盖规则显式禁止产品配方覆盖机台安全参数。
3. **运行应用回执闭环**：模拟Automation连接参数、Vision下一帧、CT下一任务分别实现`Prepare/Apply/Verify/Commit/Rollback`；在无设备环境测试失败不冒充成功。
4. **编辑器增强**：复杂对象、List/Dictionary、编辑前后Diff/Undo、引用选择及来源查看；专业仅扩展异型编辑器。
5. **发布可信性**：多目标回滚精确恢复先前ReleaseId、权限/签名、来源与审计；生产级多机台部署与设备ACK需要额外验收。

## 5. 验收执行口径

- Windows/.NET Framework 4.8，`.github/workflows/config-l1-ci.yml`：Release/x64完整解决方案编译、`ConfigL1Tests`、`ConfigProfessionalL1Tests`、`ConfigL2L4Tests`、`ConfigWpfL1Tests`、Runtime.Host独立进程命令。
- 最终Commit与主分支CI通过才可写“提交完成”；CI的`Full shared smoke diagnostics`当前为非阻断诊断项，**即使整个CI绿色，F16日志共享烟测仍可能失败**，必须逐项查看日志。
- 如果截图所在的`04_CxVisual Foundation与Mechanism ... V6.4.xlsx`与本次可访问的“对象归属审核”文件并非相同版本，应以**最终上传的实际工作簿**复核列C建议名及M04/M07/M39子方面后再冻结公开类型。
