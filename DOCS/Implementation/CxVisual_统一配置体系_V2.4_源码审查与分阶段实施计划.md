# CxVisual 统一配置体系 V2.4——源码审查、集中改造计划及验收门禁

> 日期：2026-10-16  
> 范围：配置基础体系，**计划与静态审查，不修改 GitHub 源码**。  
> 目标基线：`CxVisual_统一配置体系_完整设计与验收_V2.4.md`（2026-10-16，六篇20章、附录A—E）。  
> 仓库：`https://github.com/oudream/cx-visual-x3`，`master`，本次核对 HEAD `3c9328e748ed6c52fb937df77a12fc363ee37350`。  
> 证据等级：本文的「源码确认」只表明在该 HEAD 实际文件中找到；「CI 成功」仅用于对应工作流运行的范围；「目标/风险」尚需测试证实。  
> **V2.4 当前位于本次对话生成文件中，尚未提交到 GitHub master。** 下次获准开发后先入库并维护导航。V1.3 原文件尚未找到，V2.4 的 V1.3 需求追溯基于 V2.0、V2.2、V2.3 的引用，不冒充原文逐字核对。

## 1. 结论

保留双项目集中架构：`Foundation/Data/Configuration` 定义 FCM 契约，`Mechanism/Data/Configuration` 实现 MHS/MVB/MNM 机制；现有 `Mechanism/Presentation.Wpf`/Studio 承担 GUI，**不创建新的 ConfigManager/ConfigUI/RuntimeConfig/Migration/Security 项目或第二配置真源**。运行时数据堆、数据栈、节点入参/出参属于运行时数据体系，不归配置管理。跨进程生效 CAS、实时跨进程通知、跨机发布、真实硬件联锁、全格式历史迁移本轮暂缓，保留风险记录。

**建议开发顺序**：准备/证据冻结 G0 → **A 正确性与持久契约** → **B 开发者 API + 动态编辑模型** → **C 自动 UI 四层** → **D 压测与发布验收**。D 的部分可靠性测试随 A/B/C 同步添加，最终做长期复测；每组集中修改相关既有类和测试，不按零散方法逐项提交。

## 2. 本次查到的源码与 CI 事实

| 能力 | 实际落点（master） | 证据/尚需证明 |
|---|---|---|
| Schema/POCO/Attribute/默认值/类型 | `FCMConfigContracts.cs`、`FCMConfigSchemaBuilder.cs`、`FCMConfigTypeCatalog.cs`、`FCMJsonValueCodec.cs` | 存在实际定义、类型解析、验证；复杂类型异常和版本跨代兼容需扩大验证 |
| 统一服务和简单 API | `MHSConfigService.cs`、`MHSConfigSection.cs` | `ForMachine/ForProject/ForScope`、`Open<T>`、`Get/Value/Section`、`SetValueAsync/UpdateAsync`、`Watch/WatchSaved` 等已存在；无需第二 Facade |
| Saved/Effective/Guard/历史 | `MHSConfigSection.cs`、`MHSConfigStorage.cs`、`MHSConfigApply.cs`、`MHSConfigAudit.cs` | 保存写独占锁和比较；生效持久标记/日志；更多失败注入/重启恢复待验收 |
| 多模型原子组 | `MHSConfigAtomicGroup.cs` | 已有同一物理持久单元；`OpenAtomicGroup(id, declare)` 声明重载已存在，不可重复造；字符串成员 key 的便捷性尚可改善 |
| 层级配置 | `MVBConfigLayeredProfile.cs`、`MVBConfigAtomicLayerProfile.cs` | Name/Kind/Id/Priority 与来源合成已有；层契约冻结/升级未具备完整证据，原子档案主要持久化成员内容，改优先级存在静态语义风险 |
| 字段状态、目录、Editor | `FCMConfigCatalog.cs`、`MHSConfigEditorModel.cs` | Static Schema→Draft/Saved/Effective/Undo/Redo；动态 List/Dictionary/Set 键与元素的统一编辑协议不足 |
| 最小权限 | `FCMConfigSecurity.cs`、`MHSConfigAuthorizedSession.cs` | 多数敏感读取、撤权前后、目录脱敏有现有测试；需要审查错误消息、Preview、Diff、授权编辑提交后的动态权限边界 |
| 配置 UI | `Mechanism/Presentation.Wpf/MPWConfigEditorDialogV14.cs` | 当前是浅表 TextBox + 复杂值 JSON 文本，非四层通用编辑器；`ArchitectureSmoke --config-wpf` 当前返回“人工验收待做” |
| 开发者四场景 | `Tests/ArchitectureSmoke/ConfigDeveloperExperienceTests.cs` | `OrdinaryDeveloper`、`XRayMultipleDetectors`、`CtLongTask`、`AtomicProductRecipe` 已接入测试；压力例有 2,000,000 次标量计时循环，但尚非经过阈值认证的完整性能报告 |
| 自动 CI | `.github/workflows/config-foundation-core-ci.yml`、`config-l1-ci.yml` | 同一 HEAD 的两个 Windows 工作流结果 success；L1 内 `Run-SmokeTests.ps1` 配置 `continue-on-error: true`，不能用该项证明所有共享测试均必须通过 |

### 精确缺口及风险解释

**R01 / P0 自定义层级定义冻结**：`MVBConfigAtomicLayerProfile<T>(service, profileId, layers)` 将当前调用者提供的层按 Priority 排序，并注册 `MVBConfigLayerOverrides` 成员；没有找到把完整 `Name/Kind/Id/Priority/SectionId/SchemaVersion/政策` 与 ProfileId 作为已提交版本一同冻结的契约。原持久值不变、调用侧交换层优先级后可能改变合成解释。此为源码推断的风险，需写“同值不同层定义”重启失败测试，不能直接称已复现生产故障。

**R02 / P0 Schema 不兼容时的静默加载风险**：当前 `MHSConfigSection<T>` 启动流程主要为 `Initialize(defaultJson)`、`ReadCurrent()`、`Deserialize<T>()`，通用 `MHSConfigStorage` 未见独立 SchemaVersion manifest/闸门。`SchemaVersion/AliasFrom/SinceSchemaVersion` 存在于定义，但不足以证明跨代 schema 解析与迁移安全。最先加版本识别和 fail-closed；完整保序迁移链后置。

**R03 / P0 敏感权限与结果文本**：`MHSConfigAuthorizedEdit<T>.CommitAsync` 重新要求 `Commit`，是否也需重新要求编辑/敏感读取能力，应先定义权限语义并以撤权场景验证；`MHSConfigSection` 的 `GUARD_ERROR` 返回 `ex.Message`，UI 显示校验 Issue 与异常文本，需防止敏感字符串绕道进入普通显示/日志。属于安全审查项，不能仅因存在原始异常文本就宣称发生了实机泄漏。

**R04 / P0 本地事务失败后的真实语义**：`MHSConfigStorage.Commit` 当前有锁与原子替换流程，`SetEffective` 记录历史并更新生效标记；尚需覆盖每个写点的故障注入，要求失败不污染 Saved/Effective/Journal，不派发伪 Committed/Effective 事件。多进程竞态暂缓，但需在部署限制中明确禁止共享配置根的多个并发生效宿主。

**R05 / P1 编辑器分层**：`MHSConfigEditorModel<T>.DescribeFields()` 遍历 `FCMConfigCatalog.Describe<T>()` 的静态字段；无法仅靠此协议表示运行时 DictKey、ListIndex、SetElement 的完整编辑命令。现有 WPF 使用 `FCMConfigSchemaBuilder.Describe<T>().Members`，复杂值以 JSON 文本填写，缺多层业务配置管理 UI、精细权限、来源和历史闭环。

**R06 / P1 开发者 API 冗余风险**：已有 `UpdateAsync/Watch/WatchSaved/PrepareValue/BeginEdit`、`OpenAtomicGroup(id, declare)`；不要重复新增 `ConfigFacade` 或同义通知对象。真正待收敛的是强类型 group member/层级成员、Explain/Status 的开发者一致性及编辑结果的单一失败语义。

## 3. 阶段 0：固定可复现基线与主文档（首个小提交）

1. 用户授权代码阶段开始后先刷新 `master` HEAD，比较并行变更，阅读最新 `README/AGENTS/DOCS/00` 和现有 `.agents` 工程规则；不创建分支与 PR。
2. 将本 V2.4 放入 `DOCS/Implementation/`，同步 `README` 或 `DOCS/00` 的导航和版本基线；旧 V2.2、V2.3 保留溯源而非复制两套真源。
3. 建立唯一实现矩阵：`需求ID、Gate、Owner、目标行为、当前API/文件、测试文件、CI SHA/URL、状态、剩余风险`；初值不能抄成“已验收”。
4. 跑一次**未改代码**的独立 ConfigFoundationSmoke 和完整 Build/ArchitectureSmoke，固定成功日志、时长、机器与测试入口。若无法在当前环境执行，保留历史 CI 证据并写“本环境未运行”。
5. 先确定 A 包测试的失败预期，才能改生产实现。

**交付**：V2.4 主文件入库、矩阵可定位、baseline 日志。**门禁 G0**：Owner/依赖正确，不新增 `.csproj`，文档与 HEAD 实际符号一致。

## 4. 包 A：正确性、层级持久契约、敏感保护（P0，最先完成）

### A1 层级定义与受控变更

- 先制定“声明层级→固化契约→重启读取→不一致拒绝→预览升级→新修订持久化→按生效边界切换”的严格流程。
- **原子层档案**：优先将不可变 Profile Contract、序列化稳定指纹放入同一 `MHSConfigAtomicGroup` 的权威物理单元；复用其 `Commit/Guard/EffectiveRevision`，不建立 `ConfigProfileManager`。
- **独立层档案**：必须指定唯一权威层级清单及其版本；不得把各层独立更新伪装成跨文件原子迁移；需要全层同时生效的业务明确引导到原子层档案。
- 首次空档案/老档案无契约/已有覆盖值但缺契约的情况分开处理。对旧数据先保守拒绝并提供显式迁移检查，绝不按猜测优先级静默解释。
- 拒绝改名、交换 Priority、更改 Kind/Id、删层、增加已冲突层、切换模型 Schema 的未授权解释；定义升级保留旧版本可审计，已有 pinned 任务继续读旧值。
- 目标测试：V2.4 T19/T20 + `LayerDefinitionFreeze` 正反例、无契约历史升级、原子批量提交/重启/任务不漂移；整个流程失败时 Saved/Effective 保持原值。

### A2 Schema 兼容性与本地崩溃正确性

- `MHSConfigSection/Storage` 在反序列化前判定来源 schema，定义支持的兼容版本、未知字段/不兼容版本策略和可读错误码；不承诺自动完成所有格式迁移。
- 在 `Commit` 与生效 marker/Journal 的各个关键写点做受控故障注入（测试接口不得变成生产配置管理器）。重启后只认可有完整证据的历史链；失败不得发布虚假成功事件。
- 统一 `Success/ErrorCode/Revision/PendingApply` 与 `Applied/Status/Reason` 的开发者反馈文案，先沿用当前公开结果类型和状态字符串，不急于整体破坏性更名。
- 目标测试：T11、T14—T18、T25—T27；Schema 版本变更拒绝与明确恢复、重复重启、篡改/中断 Journal。

### A3 安全与错误脱敏

- 以 `AuthorizedSession/AuthorizedSection/AuthorizedEdit` 为唯一非可信调用入口；明确 Edit/Commit/Apply/ReadSensitive/Audit/Export 动作互相独立还是组合要求，然后逐动作写撤权用例。
- 审查 `Catalog/Explain/Preview/ChangeSet/GetStatus/Watch/WatchSaved/ValidationIssue/GuardError/ExportPackage/审计/备份/WPF MessageBox`；非授权调用不出现敏感值及其可推导内容。
- 将公开诊断和内部错误记录分离：稳定 Code/Path，未经授权只给安全文案；完整内部异常在可信日志保存但不把凭据写入日志。
- 目标测试：T21—T24、嵌套字典敏感父节点、校验抛含敏感文本、取消订阅/权限变更/导出拒绝。

**A 包结束条件**：同一模型/档案重启语义不漂移；异常不会产生假成功状态；敏感字段不可通过相关 API 绕读；四类开发者旧测试不退化；独立配置 CI + 完整解决方案 CI 实际成功。

## 5. 包 B：开发者 API 收敛 + 动态编辑模型（P1）

### B1 简单入口，直接复用旧事务

- 保留 `service.ForMachine(id).Open<T>()`、`cfg.Value/Section/Get/Explain/UpdateAsync/SetValueAsync/Watch/WatchSaved/PrepareValue/CaptureForTask`。
- 对 `Explain`/`ExplainState` 与 `MVBConfigResolved.Explain` 统一人类可读语义（五态/来源/默认/前版/Saved/Effective），优先组合现有返回契约，而不是新建第三个 Status 管理中心。
- `OpenAtomicGroup(id, declare)` **已经存在**；先优化注册后的 typed member handle/类型检查、无字符串散落及错误结果，再考虑有限的重载，不再造重复的一次声明 API。
- 层级编辑给出“名称/优先级/来源/策略一眼可懂”的简洁入口，同时保留独立层与原子层的物理边界。旧公共用法保持可用，新增便捷调用必须给出新旧对照与失败测试。
- 精简评审指标：普通配置 1 个 POCO + 1 行 Open；单字段 1 调用、多字段 1 Action、监听 1 行；多模型/多层一次编辑无需了解持久 Journal。

### B2 Schema 与动态数据编辑协议

- 使用 `FCMConfigTypeCatalog` 明确支持的类型边界；在 Foundation 描述字段、动态节点类型和约束，在 Mechanism `EditorModel` 实现操作而非数据存储新框架。
- 类型化操作覆盖：对象属性、可空值、List 加/删/替换/排序、Dictionary Key 与 Value 编辑/重命名/冲突、Set 增删、嵌套结构6层、校验路径及局部/整体验证。
- Schema 树具稳定路径/元素身份；列表索引变化和字典 key 变化后，撤销、红字错误路径与旧/新值必须仍指向正确元素。
- 处理字段依赖：`FCMConfigDependencyGraph` 已存在；先实现准确的受影响提示/必要重校验，异步校验仍须完整检查最终候选，避免仅增量跳过跨字段规则。
- 目标测试：V2.4 T04、T06—T10、T12、T28、T29，四个开发者完整调用案例新增动态列表/字典成员编辑。

**B 包结束条件**：四类开发者案例用新简单入口调用，异常/版本/通知等语义与 A 保持一致；通用 EditorModel 可完全驱动动态集合，不需要 JSON 文本手工编辑才能通过。

## 6. 包 C：WPF 四层自动配置 UI 完整产品闭环（P1/P2）

UI 代码只进既有 `Mechanism/Presentation.Wpf` 与 Studio，后台模型仍属于 Foundation/Mechanism.Data；可独立窗口、嵌入其他 WPF 程序、Studio 面板三种方式**复用同一组件**。

| 子包 | 内容 | 必须有的真实操作证据 |
|---|---|---|
| C1 第一层自动编辑 | 字符串/数值/布尔/枚举/Nullable/日期/范围、嵌套对象、List/Dictionary/Set、分组/说明/单位/默认值 | 扫描配置类型→自动呈现→动态添加删除、字段校验定位、重新打开值相同 |
| C2 第二层专业级管理 | Draft/Saved/Effective 四种版本状态、预览差异、验证、Undo/Redo、提交结果、待生效/拒绝/成功、历史、权限和敏感提示 | 一条“打开→编辑→预览→保存→Apply→历史”真实流程、无权操作拒绝、机台用例复现 |
| C3 第三层高级组织 | Scope/层级来源树、继承与覆盖、单成员与整组提交、来源链、原子与独立边界提示 | 五层及自定义 3/6 层方案编辑、升级差异提示、保存和生效源版本正确 |
| C4 第四层专业扩展 | 特殊控件替换/注册、ROI 等示例抽象、Studio/可嵌入窗口一致行为 | 宿主只给组件和配置句柄，不写自己的一套 ConfigSave/Undo/Validation |

禁止仅更新截图/XAML 就标记通过；UI 必须接实际配置 EditorModel 和权限入口。既有 `MPWConfigEditorDialog` 是整合迁移起点，而不是保留 TextBox 界面同时再造第二个“正式配置窗口”。UI 回调通过 Dispatcher，异步校验/保存不阻塞窗口；错误显示由安全可读结果驱动。

**门禁 G6**：V2.4 附录 D.2 的 12 个 UI 测试，自动 STA/WPF 回归 + Windows 实际窗口手工核查。旧 `--config-wpf` 待验证提示不等于验收已完成。

## 7. 包 D：性能、并发与可靠性正式验收（P2；随着各包持续运行）

- 现有 `ConfigDeveloperExperienceTests` 的 2,000,000 次 PrepareValue 标量循环可作为基线采集；增加热/冷、`Value`/Pinned 与复杂对象对照，记录 GC、P50/P95/P99、CPU、内存及环境，不预填吞吐门槛。
- 1k/10k 项字典、深层嵌套、100/1000 订阅者、慢与异常消费、长时任务版本不漂移、重复 Capture/Dispose、100/1000 修订审计读取/文件体积。
- 1/4/16 读线程与并行单进程提交/生效，检查消息顺序、无半更新/无幽灵事件、错误可恢复；跨进程写与 Apply 在当前阶段**不作为通过项**。
- 针对磁盘异常、文件截断、修改篡改、残留临时文件、审计/Effective 日志损坏做自动故障注入与实际 Windows 重启，明确修复策略与受影响用户可见结果。
- 固定性能测试机、Windows x64、.NET Framework 4.8 版本、硬件配置、测试样本、记录 SHA；采用以真实业务时序预算为准的阈值审批，不以 Stopwatch 有输出等同性能达标。

**门禁 G7**：产出可复测报告（单位、样本、时间、内存/GC、失败记录）+ 对应 CI；压测不能覆盖原来四类业务场景的功能正确性。

## 8. 建议先落地的测试与对应文件

| 优先 | 应新增/扩大的测试 | 现有测试归宿（不建新 csproj） |
|---|---|---|
| 1 | 相同持久原子层 Profile、交换 Priority / 改 Kind/Id / 少层 / 多层 → 重新打开必须拒绝或显式迁移 | `ConfigArchitectureCompletionTests.cs`、`ConfigDeveloperExperienceTests.cs` |
| 2 | 当前配置模型变动的 Schema 兼容性与原历史保护，初始/损坏 JSON 失败路径 | `ConfigV13ExtensionTests.cs`、`ConfigurationIntegrationTests.cs` |
| 3 | 保存及生效不同阶段故障注入、无幽灵通知、断点重启可恢复 | `ConfigArchitectureCompletionTests.cs` |
| 4 | 只读/敏感/编辑/Commit/Apply 权限组合、撤权、校验或 Guard 携带敏感文本 | `ConfigDeveloperExperienceTests.cs`、`ConfigArchitectureCompletionTests.cs` |
| 5 | 动态字典 Key、新老路径、List 插删重排、Set、不支持类型、Undo/Redo、失败不写入 | 现有四组配置测试增加针对性用例 |
| 6 | 端到端可视化：四层自动生成/保存/生效/嵌入/权限/来源 | 现有 Windows STA/UI 测试入口与 Studio，不新建配置项目 |
| 7 | 4类完整开发案例、长时压力/性能报告 | `ConfigDeveloperExperienceTests.cs` + 既有 Actions 或专门不阻塞日常 CI 的性能入口 |

## 9. 验收与分段交付规则

1. `G0`：架构双项目、源文件 Owner、主文档、源/测试/CI 矩阵一致。
2. `G1–G3`：配置模型、事务、Saved/Effective/Guard/任务快照通过正常+异常+重启测试。
3. `G4`：层级契约冻结、来源正确、权限与敏感数据全链路通过。
4. `G5`：开发者案例全面通过，主流单配置操作更短、无新增第二套服务。
5. `G6`：四层 UI 真正可编辑、可嵌入、可执行且在 Windows WPF 实操验收。
6. `G7`：故障/长时间/性能有量化实测记录。
7. `G8/H`：跨进程、跨机/真设备等**暂停**，不列入当前“底座完成”与“完整 UI 完成”门禁，也不冒称已实现。

**每包提交规则**：用户允许开始执行后，先刷新 HEAD；只在 master 直接进行少量主题明确的中文 Commit，不分支、不 PR，不为通过测试删除测试；优先运行独立配置 Windows CI 及完整解决方案 Windows CI，补跑适用的共享 smoke 与 WPF，处理并行提交后再做最终验收。任何失败在 master 修复并重新验证，更新 V2.4 逐项矩阵及对应 CI URL。

## 10. 本轮优先级与禁止事项

**必须先做**：A1（层级冻结）+ A2（不兼容 Schema/本地可靠性）+ A3（安全），这三项共同决定后面可视化能否正确展示配置与错误。**不能先造完整 UI 再补后端**，否则动态编辑/来源/权限后返工较大。

**可以并行**：B1 的 API 文档/示例审查与 A 的失败测试；C 的交互原型可在 B2 语义冻结后推进。**不能并行冲突**：A/B 同时修改同一个 `MHSConfigSection.cs` 或 `MVBConfigAtomicLayerProfile.cs` 而不协调；所有代码提交前重新检查 master 并审阅差异。

**明确不做**：跨进程配置协同和抢生效、跨机 ReleaseManifest、真实设备 ACK/联锁、完整 YAML/INI 注释无损迁移、为专业单独创建配置框架、新 `.csproj`、绕过 Saved/Effective 的直接改内存、无证据的性能宣称。

## 11. 首次正式开发建议交付物

- 将 V2.4 入库并建立主文档入口、验收矩阵；保存原 V2.2/V2.3 历史文件。
- 补完 A1/A2/A3 相关的**先失败、再实现、再回归**测试；一次性消除同一主题隐患，避免单点零碎提交。
- 在两个现有 Data 项目中集中修复；保留已有开发者最简 API、原子组和多层结构；功能必要的新对象才新增，优先扩充现有类型。
- 给出以 SHA/运行 ID/日志为依据的独立配置 CI + 完整解决方案 CI 结果；同时报告仍未完成的 G5/G6/G7。
- 下一包只在 A 的公开语义稳定后推进动态集合编辑模型和 UI。

**最终目标**：六种角色共享一个配置真源；专业开发者只需声明普通 C# 配置，就能简单读取、变更、订阅、锁定任务版本，工程/维护用户可用统一的可嵌入四层编辑器安全地编辑、保存、生效、诊断、恢复。是否完成必须由 V2.4 需求×源码×测试×CI 的矩阵裁决。