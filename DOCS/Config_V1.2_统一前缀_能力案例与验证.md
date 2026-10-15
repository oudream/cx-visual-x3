# CxVisual 配置 V1.2 · V6.4 前缀与案例驱动验收

基线：cx-visual-x3 分支 feat/config-prefix-case-hardening-20261015。对照的是目前可访问的《CxVisual_Foundation与Mechanism_统一架构与对象API设计规格_V6.4_对象归属审核.xlsx》，非截图中工作簿原文件。**规格类是参考候选，但前缀及Owner必须遵守**。本轮不新建配置项目、不引入平行ConfigManager。

## 1. 唯一Owner及正式类型

| 方面/项目 | 前缀 | 代码中正式类型 | 边界 |
|---|---|---|---|
| F15 · Foundation.Data | FCM | FCMConfigKey、FCMConfigJsonFile、FCMConfigFileDocument<T>、FCMConfigDocumentHeader | 纯配置键、Schema头及JSON文档，不持有运行态 |
| M04 · Mechanism.Core | MPM | MPMConfigItemAttribute、MPMConfigDescription、MPMConfigProperty、MPMConfigLayerSpec、MPMConfigLayerOverrides、MPMConfigLayeredProfile<T>、MPMResolvedConfig<T> | 属性元数据、业务验证调用、配置层/模板/来源解析 |
| M07 · Mechanism.Core | MTC | MTCConfigManager、MTCConfigChange、MTCConfigSnapshot<T> | 唯一读写/保存/版本/通知/恢复入口 |
| M30 · Mechanism.Runtime | MRK | MRKConfigRuntimeSession<T>、MRKConfigTaskSnapshot<T>、MRKConfigAtomicSlot<T>、IMRKConfigApplier<T>、MRKConfigApplyStatus | 任务快照、专业应用及失败补偿，不能假装设备ACK |
| M39 · Mechanism.Lifecycle | MVD | MVDConfigReleaseManifest、MVDConfigReleaseBuilder、MVDConfigReleaseReceiver、MVDConfigPublisher | 发布、Manifest、Stage、Activate、Rollback |
| WPF适配 · Mechanism.Presentation.Wpf | MPW | MPWConfigEditorDialog | 通用编辑，不持有设备状态 |

改名前缀统一迁移表：
- CxConfigJsonFile → FCMConfigJsonFile
- CxConfigItemAttribute/CxConfigDescription/CxConfigLayeredProfile<T> → MPMConfigItemAttribute/MPMConfigDescription/MPMConfigLayeredProfile<T>
- CxConfigManager/CxConfigSnapshot<T> → MTCConfigManager/MTCConfigSnapshot<T>
- CxConfigRuntimeSession<T>/CxConfigAtomicSlot<T> → MRKConfigRuntimeSession<T>/MRKConfigAtomicSlot<T>
- CxConfigReleaseBuilder/CxConfigReleaseReceiver → MVDConfigReleaseBuilder/MVDConfigReleaseReceiver
- CxConfigEditorDialog → MPWConfigEditorDialog

这是**明确的公开API改名**，并不声称二进制兼容旧类型。下游调用方需一次性迁移，避免两套等价权威类型并存。

### 开发者一行启动

~~~csharp
using CxVisual.Foundation.Data;
using CxVisual.Mechanism.Core;
using CxVisual.Professional.Vision;

FCMConfigKey key = new FCMConfigKey("Vision", "Station01");
MTCConfigManager cfg = MTCConfigManager.Create<CxVisionInspectionOptions>(
    root, key, CxVisionInspectionOptions.Validate);
CxVisionInspectionOptions value = cfg.Get<CxVisionInspectionOptions>(key);
cfg.UpdateAndSave<CxVisionInspectionOptions>(key.Id, x => x.GrayThreshold = 2000);
~~~

普通使用者不再自己写加载器、JSON解析器、保存器、通用配置属性窗口。复杂对象/集合通过MPW通用JSON编辑回退并受相同的验证/版本/保存控制；高级引用选择器与专业树形UI仍可扩展。

## 2. 案例驱动验收（必须实际运行）

| 案例 | 工业使用/攻击点 | 自动验收入口 |
|---|---|---|
| 01 | Vision默认加载→保存→重载 | ConfigL1Tests.DefaultAndReload |
| 02 | Automation多台设备独立参数与验证 | ConfigProfessionalL1Tests |
| 03 | 8位图像阈值256、CT的FDD≤FOD拒绝 | ConfigProfessionalL1Tests.CrossFieldRulesRejectUnsafeOrInconsistentParameters |
| 04 | Machine被Recipe覆盖后编辑、清除覆盖、重载 | ConfigL2L4Tests.ShadowedLayerEditAndUnmask |
| 05 | 模板循环/来源/Revision | ConfigL2L4Tests.LayerInheritanceAndSourceTrace |
| 06 | 旧Schema字段不兼容新T | ConfigL2L4Tests.LegacyIncompatibleSchemaMigration |
| 07 | 并发Value与Revision一次捕获 | ConfigL2L4Tests.AtomicConfigSnapshotConsistency |
| 08 | 文件损坏/缺少安全配置/保存失败 | ConfigL1Tests.CorruptOrUnknownSchemaDoesNotOverwrite / MissingRequiredFileFails / SaveFailureDoesNotPublish |
| 09 | 两个独立进程同时保存Revision | ConfigL1Tests.ExternalConcurrentWriterConflict |
| 10 | NextTask/RestartSession运行边界 | ConfigL2L4Tests.TaskBoundariesAndSafeApplication |
| 11 | 运行参数AtomicSlot多字段一致 | ConfigL2L4Tests.AtomicRuntimeValuesAreConsistent |
| 12 | 自动WPF操作、非法输入拒绝 | ConfigWpfL1Tests.Exercise |
| 13 | 发布Manifest验证、Runtime独立进程回读、回滚 | ConfigL2L4Tests.ReleaseDistributionAndRollback |
| 14 | FCM/MPM/MTC/MRK/MVD/MPW类型/Owner | ConfigV64Cases.PrefixAndOwnerContracts |
| 15 | FCM强类型键+一行启动+重载 | ConfigV64Cases.OneCallStartupAndTypedKeys |
| 16 | 同ReleaseId不能悄然更换Manifest | ConfigV64Cases.ImmutableReleaseManifest |
| 17 | 配置层的字段来源与版本向量 | ConfigV64Cases.SourceVersionVector |
| 18 | Automation/Vision/CT3D专业Applier模拟 | ConfigV64Cases.ProfessionalProviderApplyAndFailureRecovery |
| 19 | 模拟Verify失败、Rollback成功/失败后的故障阻断 | 同18 |
| 20 | WPF编辑List/Dictionary/嵌套对象与非法JSON | ConfigWpfL1Tests.Exercise新增部分 |
| 21 · 待验收 | 真实PLC/探测器/CT设备写入、回读、硬件ACK | 尚无实际硬件Provider，不声称完成 |
| 22 · 待验收 | 多机台签名、mTLS、审批及强一致网络发布 | 未实现，不声称完成 |

## 3. 归属冲突与仍需处理的限制

V6.4候选FCMConfigSchema、FCMConfigMergeUtil、FCMSettingsSnapshot的能力目前已有MPM描述/分层解析/MTC快照部分承担，**先做Owner和契约查重，不为实现表格而增加同义类**。FCMSecretReference、F18通用原子文件工具仍需专项审核。配置层跨多个文件不是全局原子事务。软件ActiveRevision不代表设备ACK；MRK的VerifiedByProvider只代表提供者Verify返回成功，真实设备验证取决于具体Adapter。L4 SHA只能识别内容变更，不是信任签名，跨目标补偿不等于分布式原子提交。

## 4. 验收门禁

Windows Server 2022 + .NET Framework 4.8 + Release/x64整个解决方案，执行ArchitectureSmoke内L1/L2/L3/L4及V6.4案例，执行WPF STA编辑交互和Runtime.Host独立进程验版，必须保存对应Commit SHA和CI记录。F16日志FoundationSmoke文件锁为独立已知故障；CI非阻断诊断即使总体绿色也不能声明F16全部通过。**最终完成以主分支重新验证为准。**
