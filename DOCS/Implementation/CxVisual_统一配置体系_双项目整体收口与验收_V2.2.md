# CxVisual 统一配置体系：双项目架构收口与真实验收 V2.2

> 原始基线：《CxVisual_统一配置体系_双项目架构_完整API与案例审查_V1.3.md》。本文件只记录代码实际具备的机制、源码入口、自动验证与剩余边界，不以类名存在或示例代码作为功能完成证明。
> 当前是 Foundation/Mechanism 底座阶段，不以真实机台、旧文件迁移或 Studio 窗口为前置条件。

## 一、物理归属及核心对象

- Foundation/Data/Configuration：FCMConfigIdentity、FCMConfigRevisionVector、FCMConfigValueState、FCMConfigChangeSet、FCMConfigDependencyGraph、FCMConfigTypeCatalog、FCMConfigRolePolicy、FCMConfigReference、Schema、Codec、Validation。
- Mechanism/Data/Configuration：MHSConfigService、MHSConfigSection、MHSConfigStorage、MHSConfigAtomicGroup、MVBConfigLayeredProfile、MVBConfigAtomicLayerProfile、MHSConfigEditorModel、MHSConfigAuthorizedSession、MHSConfigReferenceRegistry、MHSConfigCommitFeed、MNMConfigChangePublisher。
- 两个项目以外不建配置专用管理器、注册表或第二套配置文件真源；专业程序将来仅负责 POCO、验证规则、设备接口适配。

## 二、架构的十项不变量

1. 配置定义与具体实例不同：一个 SectionId 可在不同 Scope 中形成独立实例；实例键长度前缀编码，不能由模糊拼接产生碰撞。
2. SchemaVersion、SavedRevision、EffectiveRevision 和任务版本各有职责；多层合成使用 RevisionVector 记录确切来源。
3. Missing、ExplicitNull、Defaulted、Present、Invalid 必须可区分，不能以 default(T) 假装历史值存在。
4. 修改必须通过草稿、校验、乐观版本比较、原子文件替换和审计；失败不改变已有保存与生效真源。
5. Immediate 也需要经过统一生效路径。授权会话控制的修改在未获 Apply 权限前只能进入待生效状态。
6. 独立来源层的合成读不等于跨文件事务；真正成组原子提交使用一个物理聚合实例 MHSConfigAtomicGroup。
7. MVBConfigAtomicLayerProfile 复用聚合事务，使多层配置一次提交和一次切换；长期任务快照不随新版本漂移。
8. 持久 Commit 和 Effective 日志用修订游标恢复。提交日志与生效日志有不同的证据，不能将保存当作生效。
9. 角色策略默认拒绝；敏感字段包括嵌套集合中的敏感子属性；非可信调用方不得直接拿到底层原始 Service。
10. TypeCatalog 仅承诺明确可往返的类型。超大 CT/图像内容使用 ResourceRef/DataHandle，而非配置文件内复制完整数据。

## 三、27 职责组对照

| V1.3 分组 | 已有可测试实现 | 仍缺失或尚无对应证据 |
|---|---|---|
| A01–A03 定义、规则、Schema | Attribute、MemberId、Order、SchemaCatalog、自动发现 | 真实插件热更新全部边界 |
| A04 配置身份 | FCMConfigIdentity、PathUtil、Reference | 跨网络对象定位协议 |
| A05 来源层级 | 独立层级、原子层级、来源解释 | 完整来源锁定和冲突优先规则 |
| A06 版本 | 保存/生效修订、RevisionVector、任务版本 | 远端发布版本与兼容矩阵 |
| A07 值状态 | 五态值语义、前值、默认、历史 | 所有来源类型的完整诊断 |
| A08 读视图 | 隔离副本、单值/截面/历史、任务固定 | 工业硬实时性能基准 |
| A09–A10 校验 | Required、Range、Regex、自定义函数、同步异步、校验副作用检测 | 增量依赖调度和异步资源预算 |
| A11 类型 | TypeCatalog、Converter、精度和集合回归 | 更复杂 CLR 类型的受控扩展 |
| A12 编辑差异 | ChangeSet、Preview、Undo/Redo、事务提交 | 全类型动态编辑器建议 |
| A13 通知 | Old/New/Default、序号、时间、事务ID、历史游标、后台消费 | 外部消息总线/跨进程推送 |
| A14 生效 | Immediate/NextTask/Restart、Guard、EffectiveJournal | 完整审批状态机和设备 Readback |
| A15 扩展端口 | Validator、Converter、AccessPolicy、ResourceProvider | Release/跨机存储完整端口 |
| B01–B02 统一服务/注册 | 强类型 Open、Discover、冲突拒绝、在用卸载保护 | 非可信动态 DLL 沙箱 |
| B03–B04 解析/读快照 | 多层合成、原子层、单实例与组合快照 | 跨远程资源固定版本 |
| B05 编辑事务 | 单实例事务、多模型聚合事务、原子五层事务 | 任意独立物理后端分布式事务 |
| B06 校验执行 | 同步异步/深层路径/聚合前验证 | 超时强制取消、增量校验 |
| B07–B08 文档/迁移 | JSON保序、别名/锚点基础 | YAML/INI 注释无损、完整迁移链（当前后置） |
| B09 存储历史 | 不可变修订、审计哈希、保存/生效日志、恢复 | 大规模历史压缩策略 |
| B10 更改通知 | 有序本进程发布、持久分页、后台有界拉取/续传 | 跨进程实时分发中心 |
| B11 生效机制 | Guard、版本检查、原子档案整体切换、有效前驱链 | 跨设备审批/ACK/发布回滚 |
| B12 健康诊断 | 源版本、待生效差异、校验、审计完整性 | Studio 完整交互和遥测看板 |

## 四、24 项需求的结论

| 编号 | 需求 | 当前状态 |
|---|---|---|
| 01–04 | 声明、初始化、完整/截面/单值读取、事务修改 | 基础闭环已测试；不含旧机迁移 |
| 05–06 | 运行参数与自动恢复 | 任务快照与原子持久化已测；性能/灾难演练不足 |
| 07 | 自动通用配置 UI | EditorModel 已测，Studio UI 未接入 |
| 08–09 | 统一订阅与生效 | 本机通知、提交重放、有效历史和 Guard 已测试 |
| 10 | 发布 | 有配置包与版本证据，ReleaseManifest/远程发布未完成 |
| 11–12 | 多层继承与来源诊断 | 独立层级、原子五层、来源链已测试 |
| 13–14 | 自动发现、搜索 | 注册/卸载保护、目录、字段搜索已测试 |
| 15–16 | 批量修改、差异与撤销 | 聚合事务、EditorModel、Undo/Redo 已测试 |
| 17–19 | 联动、模板、引用 | 依赖图、来源继承、跨实例引用和资源诊断已测试 |
| 20 | 任务固定快照 | 单实例、聚合、原子层快照已测试 |
| 21 | 跨进程协同 | 持久游标和重启重放已测；实时跨进程传输未做 |
| 22 | 访问权限、审计 | 默认拒绝策略、敏感属性、动态重检、持久审计已测 |
| 23 | 调试与测试 | 独立配置 CI、完整解决方案 CI；压力/故障矩阵待扩大 |
| 24 | 自动文档与扩展 | Schema文档、TypeCatalog、Converter已测；专业 Editor 需后续接入 |

## 五、12 案例反向验收

| 案例 | 现阶段公共底座证据 | 未验收边界 |
|---|---|---|
| S01 算法配置 | POCO/验证/保存/订阅 | 真实 Vision 接入 |
| S02 旧机升级 | 历史、JSON保序、恢复 | 多格式无损迁移 |
| S03 多探测器 | 字典深层读写/动态Key/精度 | 大规模设备性能 |
| S04 CT长任务 | 整组快照、版本向量和原子切换 | 实机长任务吞吐 |
| S05 运控联锁 | 授权/生效Guard/拒绝不影响有效版 | 真实板卡/PLC 联锁 |
| S06 SCADA/IoT | Scope隔离、持久事件重放 | 网络同步与跨进程推送 |
| S07 HMI工程 | 搜索、EditorModel、来源诊断 | 实际 Studio UI |
| S08 产品配方 | 五层优先级、来源链、原子层级档案 | 分布式后端原子提交 |
| S09 插件升级 | 自动发现、冲突、卸载保护 | 插件升级迁移/沙箱 |
| S10 密钥资源 | SecretRef、DataHandle、敏感字段及资源Provider | 真 Vault 和内存服务适配 |
| S11 外部手改文件 | CAS、差异、审计完整性 | 自动三方合并及全格式支持 |
| S12 跨机发布 | 配置包和受控生效 API | ReleaseManifest、目标ACK、网络断线回退 |

## 六、关键真实调用入口

- 单实例：MHSConfigService.Open<T>(FCMConfigIdentity)、BeginEdit、PreviewAsync、CommitAsync、Explain、AtSavedVersion、CaptureForTask、TryApplyPendingAtBoundary。
- 聚合多模型：OpenAtomicGroup、Register<T>、Seal、BeginEdit、Set<T>、CommitAsync、CaptureForTask、TryApplyPendingAtBoundary。
- 层级原子配置：new MVBConfigAtomicLayerProfile<T>(service, profileId, specs)、BeginEdit、Set/Remove、ResolveSaved/ResolveEffective、CaptureForTask。
- 权限：service.Authorize(principal, policy).Open<T>(identity)，策略 FCMConfigRolePolicy 默认拒绝；含敏感字段需 ReadSensitive。
- 引用：new MHSConfigReferenceRegistry(service)、Register<T>、Resolve(FCMConfigReference)、RegisterDependency、RegisterResourceProvider。
- 重放：ReadCommittedAfter(cursor,count)、ReadEffectiveAfter(cursor,count)、SubscribeCommittedFrom(cursor,consumer,batchSize)。消费者成功处理后须保存 Cursor 并按 Revision 幂等。
- 类型与维护：FCMConfigTypeCatalog.Describe<T>()、FCMConfigCatalog.Describe/Search、MHSConfigSection.GetStatus、MHSConfigEditorModel<T>。

## 七、测试与宣布完成的边界

- 独立回归：Tests/ConfigFoundationSmoke；核心和扩展测试：Tests/ArchitectureSmoke/ConfigurationIntegrationTests.cs、ConfigV13ExtensionTests.cs、ConfigArchitectureCompletionTests.cs。
- 重点故障场景：非法模型、校验器异常及修改、事务版本冲突、原子组任一成员无效、未授权、敏感字典嵌套、资源缺失/循环依赖、历史游标重启、失效/篡改审计、失效/篡改生效日志、后台消费者异常与恢复。
- CI 须同时检查 Foundation.Data+Mechanism.Data 的独立 .NET Framework 4.8 编译/测试和完整解决方案 Windows x64 编译/测试。
- **当前可认为：公共配置的核心开发调用链和单持久单元的成组语义已经有端到端运行证据。**
- **当前不可认为：V1.3 24项在所有场景、所有文件格式、真实工业设备和多机部署均已完全交付。**

本阶段保留两个项目的集中架构，后续不允许因为某个 Case 产生新的专用 ConfigManager。
