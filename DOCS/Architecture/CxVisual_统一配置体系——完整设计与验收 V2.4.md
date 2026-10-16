# CxVisual 统一配置体系——完整设计、API、开发者体验、可视化编辑与验收 V2.4

> **文档性质**：统一架构与开发验收基线（设计定案 + 源码事实 + 待实现契约，严格分层）。  
> **版本**：V2.4 · 2026-10-16 · 讨论定案整合稿  
> **主仓库**：[`oudream/cx-visual-x3`](https://github.com/oudream/cx-visual-x3)，分支 `master`  
> **本轮核实 HEAD**：`3c9328e748ed6c52fb937df77a12fc363ee37350`  
> **平台**：Windows x64、.NET Framework 4.8、传统 `.csproj`、现有 WPF/Studio 体系  
> **本轮交付**：规范文档；**未修改、编译或提交仓库中的任何源码**，未执行新的 Windows 编译/测试。  
> **重要说明**：V1.3 原始《CxVisual_统一配置体系_双项目架构_完整API与案例审查_V1.3.md》未在本次主仓文件树或可检索资料中找到。因此文中“V1.3 27 组/24 项/12 案例”来自 V2.0、V2.2、V2.3 的明确引述与对应整理，**不是对 V1.3 原文逐字核对**；恢复原稿后必须逐项复核，不可擅称无遗漏。

## 阅读导引与证据约定

这是一份面向**软件开发者、能力开发者、工程编排用户、调试人员、操作人员、维护人员**的长期规范，同时供 Codex 按测试驱动开展开发。全文将三类内容区分：

- **【定案】**：本轮用户已确认的目标和边界；可以用于后续设计裁决，不自动等于代码已实现。
- **【现状】**：在上述 master HEAD 阅读到的真实类型、方法或测试入口；需结合 CI 证据区分“源码存在”和“运行验证”。
- **【目标】/【待验收】/【暂缓】**：未来需补的功能、尚无足够证据的能力、明确排除在当前阶段之外的事项。

### 权威资料与冲突优先级

1. 当前用户明确确认的架构边界和本 V2.4 最新决议。
2. 当前仓库 `README.md`、`AGENTS.md`、`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md` 和 `.agents/skills/cxvisual-engineering/SKILL.md`；实际 API、行为以**对应 commit 真实源码**为准。
3. `DOCS/Implementation/CxVisual_统一配置体系_双项目整体收口与验收_V2.2.md`，`CxVisual_统一配置体系_开发者体验审核与API收敛_V2.3.md`，以及 `统一配置体系_整体架构收口_V2.0.md`。
4. V1.3 原始规范（缺失，待补后复核）；仓内历史 `Config_L1_*`、`Config_L2_L4_*` 和 `Configuration_V14_Replacement_20261015.md` 只用于追溯旧实现，不得将其中已删除的 `CxConfigManager` 等原型 API 当作现有接口。
5. `Tests/ArchitectureSmoke/ConfigurationIntegrationTests.cs`、`ConfigV13ExtensionTests.cs`、`ConfigArchitectureCompletionTests.cs`、`ConfigDeveloperExperienceTests.cs`；`Tests/ConfigFoundationSmoke/Program.cs`；三个 GitHub Actions 工作流。

### 文档目录

- **第一篇（01—03）**：目标、角色、双项目架构与边界。
- **第二篇（04—09）**：完整配置能力及真实/目标 API 体系。
- **第三篇（10—12）**：最简开发者使用、四个真实案例、工程编排使用。
- **第四篇（13—15）**：自动生成配置界面的四层能力、交互闭环及 WPF 扩展。
- **第五篇（16—18）**：持久化、正确性、安全、并发与性能。
- **第六篇（19—20）**：27 组职责、24 项需求、12 案例追踪与严格验收。
- **附录 A—E**：完整类型/API 索引、可运行示例、场景表、故障测试矩阵、版本与 Codex 规范。

---

# 第一篇：目标、角色与架构

## 01. 体系定位与最终目标

### 1.1 使命【定案】

**统一配置体系是一种让专业开发者像使用普通 C# 对象一样配置复杂工业软件的公共能力。** 开发者不再为每个专业开发独立的“加载/保存/校验/版本/通知/回滚/编辑器”。配置应统一定义、统一读取、统一编辑、统一验证、统一保存、受控生效、可追踪，并能自动呈现通用编辑 UI。

用户评价标准不是类型数量，而是：**简单、可靠、完整、稳定、可验证、可扩展**。

### 1.2 主要职责与非职责

| 统一配置体系负责 | 不负责 / 仅通过接口协同 |
|---|---|
| 配置定义、默认值、Schema、类型、字段元数据 | 运行时数据堆/数据栈、节点入参出参、临时内存数据 |
| 配置实例身份、范围、层级来源、覆盖、版本 | 工程树第二套定义、Graph 持久对象树 |
| 读取、事务、校验、保存、受控生效与历史 | 直接执行设备运动、触发 X-Ray、调用 CT GPU 重建 |
| 订阅、诊断、审计、权限、自动编辑器模型 | 专业设备真实 ACK/Readback、现场安全认证 |
| 配置型资源引用、配置包本地导入导出 | 把 CT 体素/原始 X-Ray 图像直接塞进配置 JSON |

**【定案】与统一数据管理体系没有架构归属关系。** `Foundation.Data/Configuration` 和 `Mechanism.Data/Configuration` 坚持其配置职责；运行时数据体系负责内存数据、入参、出参、节点栈、全局堆等，二者不以“统一数据”名义合并、不共享另一套持久化配置真源。业务调用时可以将固定配置值作为一次执行输入，但这只是调用边界，不是配置管理由运行数据体系承担。

### 1.3 成功标准

从软件开发者角度，一次简单配置使用只需：一个 POCO、共享服务实例、`Open<T>()`、`Value/Get()`、`UpdateAsync()`、`Watch()`。从工程编排用户角度，配置编辑、预览、保存、切换生效、回退、历史、来源、权限均可由公共机制/界面完成。从运行角度，长任务只读固定版本，不读到部分参数属于旧版本、部分属于新版本的撕裂状态。

### 1.4 核心不变量（不可退化）

**I01** 一个配置 Schema 可以有多个 Scope 实例；实例唯一身份为 `(SectionId, ScopeKind, ScopeId)`，持久键不能因拼接而碰撞。  
**I02** `SchemaVersion`、`SavedRevision`、`EffectiveRevision`、`TaskPinnedRevision` 分离；多层合成需有明确来源向量。  
**I03** `Missing`、`ExplicitNull`、`Defaulted`、`Present`、`Invalid` 均为独立含义。  
**I04** 未通过验证、版本检查或持久化的候选，不得污染现有 Saved/Effective。  
**I05** `Immediate` 也必须走统一的生效检查、持久化、状态切换与通知路径，不能绕过必需 Guard。  
**I06** `Committed` 是已保存事件，`Effective` 是真正生效事件，两者订阅语义绝不混用。  
**I07** 长任务固定快照创建后不因新提交或新生效而漂移，读取复杂对象返回隔离副本。  
**I08** 独立层组合的版本向量**不是**跨多个物理文件原子事务；需要同一提交/生效边界时，必须复用一个物理聚合实例。  
**I09** 敏感数据与授权不因 Catalog、Editor、ChangeSet、错误信息、事件及历史入口而旁路。  
**I10** 基础层不依赖 WPF、专业 DLL 或设备 SDK；机制层不持有专业硬件真源。  
**I11** 已持久化的层级定义、优先级、成员类型和策略不能静默漂移后仍按原版本解释。此项属于**新确认的目标约束，现状未完成**。  
**I12** 所有未经过可重复测试的“完整”能力必须留缺口，不得因编译/类名而提前验收。

## 02. 六种角色的期望、入口与使用边界

| 角色 | 最主要诉求 | 日常最短路径 | 应给出的证据 | 不要求他做的事 |
|---|---|---|---|---|
| R1 软件开发者 | 简单定义、初始化、读取/修改、版本、通知、错误可理解 | POCO+Attribute → `Open<T>` → `Value`/`UpdateAsync`/`Watch` | 单/多字段调用演示、失败不污染、性能数据 | 自行写文件 IO、JSON 事务、专用配置窗口 |
| R2 能力开发者 | 为算法/设备能力声明可发现的参数及校验规则 | 注册类型、约束/规则、Schema、资源引用 | 类型契约、校验、按能力组配置示例 | 建另一套参数管理框架 |
| R3 工程编排用户 | 配置模板/机台/产品/方案，理解覆盖和运行版本 | 选择范围/层级 → 编辑 → 预览 → 保存 → 申请生效 → 固定到任务 | 来源链、版本向量、原子边界、运行快照 | 理解 JSON 文件布局和内部 Journal |
| R4 调试人员 | 快速找到谁改了什么、何时保存/生效、为什么失败 | 搜索 → 对比 → 追踪 → 诊断 → 回退草稿 | Revision/ChangeSet/Issues/审计/事件 | 手工反查所有文件差异 |
| R5 操作人员 | 在授权范围内安全调整少量参数 | 通用表单/参数页 → 校验 → 保存/生效提示 | 权限提示、影响说明、当前生效/待生效 | 修改隐蔽文件、绕过设备联锁 |
| R6 维护人员 | 恢复配置、审查来源、调查故障、备份/审计 | 目录、状态、导出、历史、验证、恢复 | 恢复链完整性、故障演练、审计记录 | 直接编辑数据库/删除历史来回退 |

**六角色验收原则**：同一配置真源在六类入口表达一致；不同角色只是授权、视图和操作能力不同，不得生成各自独立的一套保存/生效状态。

### 2.1 R1 首次接入体验

要求专业仅提供强类型 POCO，宿主提供 `configRoot` 和共享 `MHSConfigService`；若使用标准属性可一行打开，不要求每个专业声明自定义存储 Provider。默认值不应成为设备安全参数必然合格的证据：对于安全关键项必须有明确的启动必备配置/人工验证策略，不能凭构造函数缺省值允许真实设备动作。【目标，需要专业安全策略契约与测试】

### 2.2 R3 工程编排体验

编辑态显示 Saved 候选，执行态明确绑定 Effective 或 Captured；配置值可被工程选择并作为某节点的启动参数，但节点数据栈的生命周期与配置版本是两件不同事情。允许一次编排将多个配置字段映射到参数端口；执行时记录配置身份、RevisionVector 和 TaskId，便于复现。

## 03. 双项目架构、Owner 与依赖

### 3.1 物理位置【不可更改】

```text
Foundation/Data/Configuration/
    FCMConfigContracts.cs
    FCMConfigArchitecture.cs
    FCMConfigSchemaBuilder.cs
    FCMConfigCatalog.cs
    FCMConfigTypeCatalog.cs
    FCMConfigSecurity.cs
    FCMConfigReference.cs / FCMConfigReferences.cs
    FCMConfigPathDiffUtil.cs
    FCMJsonValueCodec.cs / FCMOrderedJsonDocument.cs

Mechanism/Data/Configuration/
    MHSConfigService.cs / MHSConfigSection.cs / MHSConfigStorage.cs
    MHSConfigApply.cs / MHSConfigAudit.cs / MHSConfigCommitFeed.cs
    MHSConfigAtomicGroup.cs / MVBConfigLayeredProfile.cs
    MVBConfigAtomicLayerProfile.cs / MHSConfigEditorModel.cs
    MHSConfigAuthorizedSession.cs / MHSConfigReferenceRegistry.cs
    MHSConfigStatus.cs / MHSConfigPackage.cs
    MNMConfigChangePublisher.cs

Mechanism/Presentation.Wpf/MPWConfigEditorDialogV14.cs  # 现有 UI 适配器，非第三套配置真源
Applications/Studio/StudioDemoConfig.cs                # 现有示例宿主
Tests/ArchitectureSmoke/Config*Tests.cs                # 现有测试
Tests/ConfigFoundationSmoke/                           # 现有独立测试入口
```

所有公共配置基础**只能**存在于以上两个项目的 `Configuration` 子目录。禁止新增 `ConfigManager`、`RuntimeConfig`、`Migration`、`Security`、`Publisher` 等独立项目/并行框架。命名遵循 Foundation `FCM`，Mechanism `MHS/MVB/MNM` 等**现有** Owner 前缀。

### 3.2 依赖规则

```text
专业 POCO / 专业服务 ──────────┐
Studio / 其他 WPF 界面 ────────┼──► Mechanism.Data.Configuration ──► Foundation.Data.Configuration
Mechanism.Presentation.Wpf ───┘                ▲
                                  只有公共机制，不引用专业实现或 WPF
```

现有通用编辑窗口可以改进、重构或嵌入其他 WPF 应用，但必须复用 Mechanism 的 `MHSConfigEditorModel<T>` 等公共编辑语义，**绝不**在 UI 内自行维护第二套版本、存储、Undo/Redo 或设备联锁。

### 3.3 单真源与组合对象

```text
FCMConfigSchema (定义、类型、规则、MemberId、SchemaVersion)
          │ 绑定一组 (SectionId, ScopeKind, ScopeId)
          ▼
MHSConfigSection<T> (一个持久化配置实例)
  ├─ Saved / Effective / History / Audit / Notification
  ├─ MHSConfigEdit<T> / MHSConfigSnapshot<T>
  └─ MHSConfigStorage / Apply Guard
          │
          ├─ MHSConfigAtomicGroup (一个物理聚合配置实例，多个模型共享修订)
          ├─ MVBConfigLayeredProfile<T> (多个独立实例组合读取)
          └─ MVBConfigAtomicLayerProfile<T> (层级覆盖作为一个物理聚合单元)
```

**三种使用模式的边界**：普通单实例最少抽象；独立层模式优先灵活性但不许宣称跨文件原子提交；原子层模式以一个物理实例保障一次事务、一版生效、一份任务快照。不能用“依次调用多个 `CommitAsync`”冒充原子事务。

---
# 第二篇：完整配置能力与对象 API

## 04. 配置定义、Schema、类型与自动发现

### 4.1 POCO 与 Attribute【现状】

配置源自普通 C# 公共类，不要求继承专用基类。`[FCMConfigSection("...")]` 提供唯一 SectionId，可声明 `SchemaVersion`；`[FCMConfigItem("...")]` 可声明 `MemberId`、`Order`、`BeforeId`、`AfterId`、`AliasFrom`、`SinceSchemaVersion`、`Unit`、`Category`、`Description`、`ApplyPolicy`；`[FCMConfigDependsOn("...")]` 表达依赖；数据注解包括 `[Required]`、`[Range]`、`[RegularExpression]`；专门的验证器用 `[FCMConfigValidator]`、`[FCMConfigCustomValidator]`；可往返转换用 `[FCMConfigConverter]`；敏感字段用 `[FCMConfigSensitive]`。

Schema/目录入口：`FCMConfigSchemaBuilder.Describe<T>()/Describe(Type)`、`Members(Type)`、`FCMConfigCatalog.Describe<T>()/Describe(Type)`、`Search`、`ExportMarkdown`（具体重载以源码为准）。`MHSConfigService.RegisterAssembly(Assembly)` 自动发现标注 Section 的类型，校验重复 SectionId，维护在用类型卸载保护；`GetRegisteredSections()` 和 `FindRegisteredSection(sectionId)` 用于工具浏览。**程序集发现并不等于动态 DLL 安全沙箱或热升级迁移完成。**

### 4.2 默认值及非默认值

默认值是 `new T()` 后字段值的配置契约，在 `Defaulted` 状态下可解释为模型构造默认；“字段没有写入”和“字段显式写入等于默认值”必须保持来源不同，不能只比较数值相等推导显式性。字段名更改、`MemberId` 或 `AliasFrom` 不能自行被当成完整 Schema 迁移策略；持久 JSON 中旧字段、未知字段和保序规则必须另行审查。

### 4.3 支持类型与边界

| 类型大类 | 现状/规则 | 自动编辑目标 |
|---|---|---|
| `bool`、数值、字符串、枚举 | 受 `FCMConfigTypeCatalog` 和 Codec 可往返约束 | Checkbox、数字框/区间、文本、下拉 |
| `int?` 等 Nullable、显式 null | 需区分 Missing 与 ExplicitNull | 空值切换、置空、恢复继承 |
| 嵌套 POCO、深层对象 | 路径/反射/递归校验，序列化隔离 | 可折叠属性树、子截面编辑 |
| Dictionary | 支持动态键持久化和路径 | 增删键/重命名键/按键定位错误 |
| List、Set、复杂组合集合 | 已有编解码/深层类型回归 | 列表增删/按需排序、集合去重，不给 Set 虚构位置 |
| decimal、ulong、DateTimeOffset、Converter | 现有丰富类型回归涵盖部分边界 | 高精度/时区输入及明确显示规则 |
| SecretRef、ResourceRef、DataHandle | 保存引用/句柄，不直接保存外部大资源或明文密钥 | 引用浏览、验证、受控跳转、敏感遮盖 |
| 不支持的 CLR 类型/复杂循环对象图 | 必须由 TypeCatalog 拒绝或受控扩展 | 拒绝自动编辑，显示明确契约问题 |

**【目标】注册前做完整 Schema、类型和依赖校验，失败的模型不能带病进入可写服务；校验器不能通过修改候选对象产生隐式参数变更。** 严禁用 `ToString()` 作为通用存储格式、用 `double` 中转全部数值导致 decimal/ulong 精度丢失。

### 4.4 规则与文档生成

`FCMConfigValidationEngine.Validate<T>(value, scope)` 与 `ValidateAsync<T>(value, scope, token)` 使用现有规则，产生 `FCMConfigValidationReport` 和 `FCMConfigIssue(Path, Code, Message, Severity)`；自定义同步/异步规则返回实际问题及路径，异常必须转可诊断失败。自动文档从同一 Schema 生成，不得因为“展示完整默认值”而泄露敏感嵌套字段。【现状】Catalog 已对敏感子属性及其父节点做部分脱敏；**待补齐**错误文本、导出、差异、事件等统一策略。

## 05. Identity、Scope、自定义层级、覆盖与来源

### 5.1 配置身份

`FCMConfigIdentity(sectionId, scopeKind, scopeId)` 表示**一个配置类型在特定范围内的实例**。例如 `(Vision.Capture, machine, Machine-A)` 与 `(Vision.Capture, machine, Machine-B)` 是不同真源；`CanonicalKey` 采用长度前缀组合，不能以未经分隔/转义的字符串拼接替代。

`service.ForMachine("M-A").Open<T>()`、`ForProject("P-A").Open<T>()`、`ForScope(kind,id).Open<T>()` 为现有简便路径。专业可定义语义合理的 Scope，不应把任意字符串当作另一配置系统的全局单例键。

### 5.2 自定义层级【本轮正式定案】

**层级数量、名称、范围与优先级由专业按需声明，模板/工程/机台/产品/运行覆盖五层只是典型组合，不是强制五层。** 普通单实例根本不需要层级。可定义例如：

- Vision：Template → Project → Machine → Product → Override；
- CT3D：Base → Scanner → ScanPlan → ReconstructionPlan → Session；
- Automation：Base → DeviceModel → Machine → Process；
- HMI：Default → Project → Page → Operator；
- IoT：DeviceType → Device → Site。 

专业层级名称和 ID 不强行翻译成五个固定枚举，也不允许出现重复层名/优先级或无解释的隐式排序。每层应有：`Name`、`ScopeKind`、`ScopeId`、`Priority`、所属模型 `SectionId`、`SchemaVersion`、用途、来源策略、锁定策略（如适用）。优先级高的**显式设置字段**覆盖低层；“没有设置/删除覆盖”沿继承链回退，而“显式 null”按类型和允许的 null 策略处理，不能一律视作缺失。

示例：Base 曝光 80，Machine 显式 120，Product 未定义，最终 120；产品显式 150，最终 150；移除 Product 覆盖后返回 Machine 的 120。对于同一层级名称、相同优先级、无法解释的锁定冲突，必须确定性拒绝而不是依赖字典迭代顺序。

### 5.3 两种层级物理语义

| 模式 | 现有对象 | 物理事务边界 | 优点 | 不得承诺 |
|---|---|---|---|---|
| 独立层组合 | `MVBConfigLayeredProfile<T>` | 每个 `MHSConfigSection<MVBConfigLayerOverrides>` 独立提交 | 可以分别保存与生效，每层有自己的版本 | 多层跨文件原子提交/切换 |
| 原子层档案 | `MVBConfigAtomicLayerProfile<T>` | 一个 `MHSConfigAtomicGroup` 承载全部层 | 多层一个提交/生效边界、固定同一个版本 | 远端跨机器原子提交 |

`MVBConfigLayerSpec(name, kind, id, priority)` 是现有运行时层级描述。`ResolveEffective/ResolveSaved` 得到 `MVBConfigResolved<T>`，其 `Explain` 提供来源链，`RevisionVector` 标识本次读的一组来源修订。

### 5.4 层级定义冻结、升级与兼容【本轮新增目标；未实现完整证据】

一次配置档案被创建时，必须确定并持久化**层级契约清单**：`ProfileId`、模型 `SectionId/SchemaVersion`、按确定性顺序的 `Name/Kind/Id/Priority`、生效/锁定策略、契约指纹。重启载入发现调用方新定义与持久契约不一致时：

1. **拒绝静默解释**，返回明确的 `CONFIG_LAYER_DEFINITION_MISMATCH`（**目标错误码，非现有 API 承诺**）。
2. 不改变 Saved/Effective，不悄悄重排优先级，也不删除“未知层”原有数据。
3. 只经显式“检查差异 → 预览受影响字段 → 校验新合成结果 → 确认升级 → 新版本持久化 → 按边界生效”的迁移流程处理；旧版仍可审计、可重放。
4. 已启动的长期任务继续使用旧 `RevisionVector` 固定值；升级只影响后来创建的任务。
5. **独立层**没有天然全局原子定义文件，需确认定义清单所在的唯一权威位置；**原子层**优先写入现有聚合物理文档/同一持久单元，不另建影子层级真源。

这项与“SchemaVersion 不同时静默载入旧 JSON”的治理是两类不同检查，均必须存在。测试包括改名、改 ID、改 Priority、交换顺序、删层、插层、不同成员 SchemaVersion、重启后冲突及旧任务固定。

### 5.5 来源诊断/继承锁定【现状+目标】

现有 `MVBConfigResolved<T>.Explain(selector)` 返回值、`EffectiveSource`、`SourceChain`；单实例 `cfg.Explain(selector)` 则返回默认/保存/生效/前驱/版本等。二者含义不同；【目标】统一用户层展示语义，使页面能解释“最终值为何是 150、是谁覆盖了 120、哪些字段未显式设定、某来源层为什么被禁止覆盖”。来源层“锁定/冲突优先规则”仍需单独契约与验收，不得声称已全面实现。

## 06. 读取、值状态、历史和任务固定快照

### 6.1 读取入口（当前源码）

| 入口 | 返回含义 | 注意事项 |
|---|---|---|
| `cfg.Get()` | 当前 Effective 整体的隔离副本 | 复杂对象克隆，非硬实时热路径 |
| `cfg.Section(x => x.Acquisition)` | 当前 Effective 的指定截面 | 修改返回副本不应改变真源 |
| `cfg.Value(x => x.ExposureMs)` | 当前 Effective 的字段 | 表达式可能重复编译，循环热点请预编译 |
| `cfg.GetLatestSaved()` | 最近成功保存的模型副本 | 可能仍未生效 |
| `cfg.Explain(x => x.F)` | 默认/上次保存/上次有效/当前保存/有效/来源/版本 | `FCMConfigValueInfo<T>` |
| `cfg.ExplainState(x => x.F)` | 五态值状态投影 | 缺键不伪装成 `default(T)` |
| `cfg.AtSavedVersion(version)` | 祖先保存修订模型副本 | 不是自动撤销/回写 |
| `cfg.AtSavedVersionView(version)` | 历史只读快照句柄 | 用 `using` 管理生命周期 |
| `cfg.PrepareValue(selector)` | 编译一次、每次取**最新 Effective** 值 | **不是**任务固定快照；每次按当前实例锁取值 |
| `cfg.CaptureForTask(taskId)` | 捕获一次 Effective + Version | 长任务期间不随之后更新漂移 |
| `snapshot.PrepareValue(selector)` | 固定任务版本的预编译读取 | 快照 Dispose 后不能继续读取 |

### 6.2 五态规则和不同版本

- **Missing**：给定路径或历史字段不存在；禁止把不存在的 `int` 当作明确的 0。
- **ExplicitNull**：字段明确存在且持久写为 JSON `null`，需要保留和 Missing 不同的诊断。
- **Defaulted**：尚无显式覆盖、从模型/基础默认获得的值；值等于默认数字不意味着自动是 Defaulted。
- **Present**：具有有效的显式赋值来源，即使其数值恰好等于工厂默认。
- **Invalid**：模型/来源无效时必须给出来源、Issue，不可继续向设备默默使用未经确认的候选。

`FCMConfigValueInfo<T>` 的 `Has*`、`*IsExplicitNull`、`*Value` 属性应作为完整信息，不应通过 `Value == null` 代替“存在性”。读取历史若已失效/遭篡改，必须明确失败；“上一版保存”与“上一版生效”不是一个概念。

### 6.3 任务固定读规则

长期 CT 扫描、重建和 AI 检测在**任务启动边界**调用 Capture；记录 `TaskId`、配置身份及版本。运行中如新配方保存并切换，有效版本可升级，但既有扫描/重建不会夹杂新旧参数。任务释放快照后不得再访问其 getter；取消、异常和资源结束都必须 Dispose。对于超大图像/体数据，快照固定的是**配置中引用的资源身份/版本**，并不自动固定外部资源实际内容，资源提供方必须另承担数据生命周期。

## 07. 编辑、验证、差异、撤销与事务

### 7.1 最小修改用法【已存在】

```csharp
// 多字段一次事务：一次候选、一次验证、一次保存。
MHSConfigCommitResult saved = await cfg.UpdateAsync(model =>
{
    model.ExposureMs = 120;
    model.Acquisition.Group.TimeoutMs = 650;
}, "更新采图参数", cancellationToken);

// 单字段：强类型选择器。
MHSConfigCommitResult one = await cfg.SetValueAsync(
    x => x.ExposureMs, 125, "调整曝光", cancellationToken);
```

失败时必须检查 `saved.Success`、`saved.ErrorCode`、`saved.Validation.Issues`；成功时检查 `saved.Revision`、`saved.ChangeSet`、`saved.PendingApply`。`Success=true` 意味着提交已保存，不必然意味着 Effective 已更新。

### 7.2 显式草稿及预览【已存在】

`cfg.BeginEdit(reason)` 返回 `MHSConfigEdit<T>`，包含 `WorkingCopy`、`Preview()`、`PreviewAsync(token)`、`CommitAsync(token)` 和 Dispose；EditorModel 提供 `Modify`、`Undo/Redo`、`DescribeFields`、`AffectedBy` 等。版本冲突、取消、校验错误必须保持旧真源；不为 UI 实现另一套写盘逻辑。

### 7.3 校验顺序与错误契约

目标执行链：`接收候选 → 类型/可往返检查 → 字段与对象校验 → 同步/异步专业校验 → 依赖影响分析 → 预览差异 → 乐观版本检查 → 原子提交 → Committed 通知`。生效阶段另执行 `Validate → Guard → 版本比较 → 持久生效 → 状态交换 → Effective 通知`。其中**增量依赖调度、异步预算/超时、完整错误码集合尚是目标**，不得当作当前实现细节。

校验必须记录 Path、机器识别 Code 和人类可读但可脱敏 Message；异常、取消与权限拒绝应可区分。对字典 `Detectors["D-02"].Gain`，错误必须定位到动态 Key 和嵌套字段；禁止验证器修改候选数据后仍宣布无副作用。

### 7.4 原子边界

- 单实例：一个 `MHSConfigSection<T>` 的 Saved/Effective 修订；
- 多模型：`MHSConfigAtomicGroup` 的一个成员字典文档，一个持久化修订；
- 原子层：复用聚合文档保存五层/任意层覆盖，一个持久化修订；
- 独立层：每层自己的 Section，**一次修改多层并非原子**。 

【目标】“成员 key 强类型、成员配置一次声明和重启一致性检查”让开发者少写字符串；优先改进现有 `Register<T>()`、`Seal()` 和 `OpenAtomicGroup(groupId, declare)`，不新造 Facade/Registry 真源。

### 7.5 Undo/Redo 的限定

`MHSConfigEditorModel<T>.Undo/Redo` 是**未提交草稿**的本地历史，不等同于让磁盘回退到旧版本。已提交历史恢复用 `RestoreSavedVersionAsync(version, reason, token)` **创建新的修订**；不得删除以前历史或直接回退 `current.json` 指针。

## 08. 保存、生效、订阅、审计与回退

### 8.1 状态机：一条公共用户操作链【V2.4 定案】

```text
打开配置/选择 Scope → 加载 Saved 和 Effective → 修改 Draft
       ↓
规则校验/来源与权限检查 → Preview(ChangeSet/影响范围/生效策略)
       ├─ 失败：显示字段错误；Saved/Effective 保持不变
       └─ 成功：提交 Saved + 审计 + Committed 通知
                    ↓
             显示“待生效”及所需边界
                    ↓
          请求 Apply → Guard/版本/安全策略检查
             ├─ 拒绝：保存保留、有效不变、原因可诊断
             └─ 同意：持久化 Effective → 切换内存有效快照
                              ↓
                       Effective 通知/任务读取
                              ↓
                 历史比对/恢复（新 Saved 修订）
```

允许 `Immediate` 自动尝试走该链的后半部分；**绝不等于跳过 Guard 或设备适配的安全条件**。主界面应明确展示“草稿、已保存但未生效、真正生效、任务固定”四种语义，不能只用一个“保存成功”的绿灯表示设备已使用新参数。

### 8.2 生效策略与 Guard

`FCMConfigApplyPolicy` 实有：`Immediate`、`NextFrame`、`NextTask`、`Reconnect`、`Restart`。`cfg.ConfigureApplyGuard(...)` 注册必要的生效检查；`TryApplyPendingAtBoundary(boundary, approval)` 返回 `MHSConfigApplyResult.Applied/Status/Reason/SavedVersion/EffectiveVersion/RequiredBoundary`。`NextTask` 的实际触发时机由专业运行宿主负责，而不是基础配置系统自行猜测设备何时空闲。

### 8.3 通知语义

`cfg.Watch(selector, callback)` 是 `Effective` 变化；`cfg.WatchSaved(selector, callback)` 是 `Committed` 变化。低级 `Subscribe(selector, FCMConfigChangeStage, callback)`、`SubscribeAll`、`SubscribeSection` 可用于诊断/编辑器。事件携带 Old/New/Default、字段路径、Revision、事务 ID、序列和 UTC 时间；本地事件顺序不能被误认为跨进程全局总序。

```csharp
using (IDisposable sub = cfg.Watch(x => x.ExposureMs, change =>
{
    // 只有真正生效后才进这里。
    Console.WriteLine(change.OldValue + " -> " + change.NewValue);
}))
{
    // 执行期订阅；保存但未生效时不应通知运行线程。
}
```

`ReadCommittedAfter(cursor,count)`、`ReadEffectiveAfter(cursor,count)` 和 `SubscribeCommittedFrom(cursor,consumer,batchSize)` 是现有持久事件重放入口。应用消费者负责成功消费后可靠保存 cursor 与幂等；`MHSConfigCommitFeed<T>` 提供 `Cursor`、`LastError`、`Resume()`、`Dispose()`。**现有重放不等于可靠跨进程实时推送。**

### 8.4 审计与维护

`ListSavedVersions()`、`ListAudit()`、`ReadCommittedAfter()`、`ReadEffectiveAfter()`、`AtSavedVersion*` 提供审计/历史；`GetStatus()` 汇总当前版本、待生效差异、Saved/Effective 验证和审计完整性；`ExportPackage()` 创建本地带哈希配置包，`ImportPackageAsync` 需要 Schema/Hash/业务校验。**带哈希不等于具备签名、跨设备身份授权、不可抵赖和分布式回滚能力。**

### 8.5 版本与失败语义

建议正式建立稳定分类：`CONFIG_VALIDATION`、`CONFIG_REVISION_CONFLICT`、`CONFIG_WRITE_CONFLICT`、`CONFIG_PACKAGE_SCHEMA_MISMATCH`、`CONFIG_PACKAGE_HASH_MISMATCH` 等当前真实代码可见的错误；对 `NOT_PENDING`、`BOUNDARY_NOT_REACHED`、`APPLY_VERSION_CONFLICT`、`GUARD_ERROR` 等生效状态明确哪类可重试、哪类需人工干预。【目标】统一结果对象和错误枚举/文档，但应先复用现有 `ErrorCode/Status`，**不为“统一错误”再造一套管理框架**。

## 09. 完整类型/接口/枚举体系及 API 规则

### 9.1 对象责任与新增准入

`Foundation.Data.Configuration` 只放契约和无运行态基础；`Mechanism.Data.Configuration` 集中服务、存储、事务、生效、订阅及编辑模型。任何新增类必须先证明现有类型无法表达该职责；优先增加方法/内部组合。禁止为了满足文档分组机械新增 27 个或 349 个类。

### 9.2 典型公开签名（均已在当前源码确认；省略 using 与部分重载）

```csharp
// 基础定义与读取
FCMConfigSchema FCMConfigSchemaBuilder.Describe(Type type);
IList<FCMConfigFieldInfo> FCMConfigCatalog.Describe(Type modelType);
FCMConfigTypeContract FCMConfigTypeCatalog.Describe(Type type);

// 服务、实例及注册
new MHSConfigService(string rootDirectory);
MVBConfigScopeResolver MHSConfigService.ForMachine(string machineId);
MVBConfigScopeResolver MHSConfigService.ForProject(string projectId);
MVBConfigScopeResolver MHSConfigService.ForScope(string scopeKind, string scopeId);
MHSConfigSection<T> MHSConfigService.Open<T>(FCMConfigIdentity identity);
int MHSConfigService.RegisterAssembly(System.Reflection.Assembly assembly);
IList<FCMConfigSchema> MHSConfigService.GetRegisteredSections();
FCMConfigSchema MHSConfigService.FindRegisteredSection(string sectionId);

// Section 的常用操作
T MHSConfigSection<T>.Get();
T MHSConfigSection<T>.GetLatestSaved();
TValue MHSConfigSection<T>.Value<TValue>(Expression<Func<T,TValue>> accessor);
TPart MHSConfigSection<T>.Section<TPart>(Expression<Func<T,TPart>> accessor);
Func<TValue> MHSConfigSection<T>.PrepareValue<TValue>(Expression<Func<T,TValue>> selector);
FCMConfigValueInfo<TValue> MHSConfigSection<T>.Explain<TValue>(Expression<Func<T,TValue>> accessor);
FCMConfigValueState<TValue> MHSConfigSection<T>.ExplainState<TValue>(Expression<Func<T,TValue>> accessor);
MHSConfigEdit<T> MHSConfigSection<T>.BeginEdit(string reason);
Task<MHSConfigCommitResult> MHSConfigSection<T>.UpdateAsync(Action<T> change, string reason, CancellationToken token);
Task<MHSConfigCommitResult> MHSConfigSection<T>.SetValueAsync<V>(Expression<Func<T,V>> selector, V value, string reason, CancellationToken token);
MHSConfigSnapshot<T> MHSConfigSection<T>.CaptureForTask(string taskId);
IDisposable MHSConfigSection<T>.Watch<TValue>(Expression<Func<T,TValue>> accessor, Action<FCMConfigChange<TValue>> callback);
IDisposable MHSConfigSection<T>.WatchSaved<TValue>(Expression<Func<T,TValue>> accessor, Action<FCMConfigChange<TValue>> callback);
MHSConfigApplyResult MHSConfigSection<T>.TryApplyPendingAtBoundary(FCMConfigApplyPolicy boundary, Func<MHSConfigApplyReview<T>, MHSConfigApplyDecision> approval);
MHSConfigStatus MHSConfigSection<T>.GetStatus();

// 多模型原子聚合
MHSConfigAtomicGroup MHSConfigService.OpenAtomicGroup(string groupId);
MHSConfigAtomicGroup MHSConfigService.OpenAtomicGroup(string groupId, Action<MHSConfigAtomicGroup> declare);
void MHSConfigAtomicGroup.Register<T>(string key);
void MHSConfigAtomicGroup.Seal();
Task<MHSConfigCommitResult> MHSConfigAtomicGroup.UpdateAsync(Action<MHSConfigAtomicEdit> change, string reason, CancellationToken token);
MHSConfigGroupSnapshot MHSConfigAtomicGroup.CaptureForTask(string taskId);

// 两种层级模型
new MVBConfigLayerSpec(string name, string kind, string id, int priority);
new MVBConfigLayeredProfile<T>(MHSConfigService service, params MVBConfigLayerSpec[] specs);
new MVBConfigAtomicLayerProfile<T>(MHSConfigService service, string profileId, params MVBConfigLayerSpec[] layers);
MVBConfigResolved<T> MVBConfigAtomicLayerProfile<T>.ResolveEffective();
MVBConfigResolved<T> MVBConfigAtomicLayerProfile<T>.ResolveSaved();

// 公开编辑模型
new MHSConfigEditorModel<T>(MHSConfigSection<T> section, string reason, int maxUndoStates = 64);
void MHSConfigEditorModel<T>.Modify(Action<T> change);
bool MHSConfigEditorModel<T>.Undo();
bool MHSConfigEditorModel<T>.Redo();
IList<MHSConfigEditorField> MHSConfigEditorModel<T>.DescribeFields();
```

> 上述代码块是**签名索引**而非可直接复制编译的完整程序：实例方法、泛型约束与类型命名空间应参考源码。实际演示代码见第三篇/附录 B；非公开方法不能由此推导为可调用 API。

### 9.3 API 契约必须明确的六个维度

每个公共入口在最终源码文档中都应交代：①参数必填及 Scope/Path 规则；②返回值、失败码、异常和取消语义；③线程与重入规则；④引用/快照是否隔离及 Dispose 时机；⑤ Saved/Effective/历史中读取哪个版本；⑥对外部文件 IO、敏感信息和订阅的权限要求。类的存在不等于六项完备。

### 9.4 两类目标 API（不能冒充现状）

**【目标 G-API-01】** 聚合成员不再靠散落的字符串 key，支持一次声明、重启合同校验和简洁成员获取，仍复用 `MHSConfigAtomicGroup`。  
**【目标 G-API-02】** 多层档案从同一个可发现入口打开；支持读取层定义、冻结指纹、版本升级预览，内部保留独立层与原子层的不同物理语义。  
**【目标 G-API-03】** 提供一致诊断投影（字段 `Presence/Source/Default/PreviousSaved/PreviousEffective/Saved/Effective/Revision`），内部不应重复保存真源。  
**【目标 G-API-04】** 对 Dictionary/List/Set 提供由类型与 Schema 驱动的集合编辑命令，避免 UI 手工解析 JSON。  
**【目标 G-API-05】** 安全可扩展的字段编辑器描述与权限裁剪，位于 Mechanism 数据模型与 WPF 呈现间的明确接口；不向 Foundation 注入 WPF。  
**【目标 G-API-06】** 兼顾 `PrepareValue` 的高频读取与固定快照读的低开销，基准和安全优先于过早引入复杂缓存。  

---
# 第三篇：开发者、工程编排用户与四类真实案例

## 10. 开发者最简用法和完整用法

### 10.1 最小完整接入：四步闭环【当前 API】

以下示例所需 `using`：`System`、`System.ComponentModel.DataAnnotations`、`System.Threading`、`System.Threading.Tasks`、`CxVisual.Foundation.Data.Configuration`、`CxVisual.Mechanism.Data.Configuration`。`configRoot` 由宿主选取可写目录，`token` 为有效的取消令牌；示例模型适合教程，不应把默认曝光值当作生产安全许可。

```csharp
[FCMConfigSection("Vision.Capture", SchemaVersion = 1)]
public sealed class CaptureConfig
{
    [FCMConfigItem("曝光时间", Unit = "ms", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
    [Range(1, 1000)]
    public int ExposureMs { get; set; } = 80;

    [FCMConfigItem("启用校正")]
    public bool EnableCorrection { get; set; } = true;
}

// 宿主初始化，专业使用同一个服务，勿每次读都创建新的 Service。
MHSConfigService service = new MHSConfigService(configRoot);
MHSConfigSection<CaptureConfig> cfg = service.ForMachine("Machine-A").Open<CaptureConfig>();
int effective = cfg.Value(x => x.ExposureMs);

using (IDisposable watch = cfg.Watch(x => x.ExposureMs, change =>
    Console.WriteLine("Effective " + change.OldValue + " -> " + change.NewValue)))
{
    MHSConfigCommitResult result = await cfg.UpdateAsync(x =>
    {
        x.ExposureMs = 120;
        x.EnableCorrection = false;
    }, "调整采集参数", token);
    if (!result.Success)
        throw new InvalidOperationException("配置保存失败：" + result.ErrorCode);

    // 不能把 Save 当成 Apply；运行宿主到 NextTask 边界时再切换。
    MHSConfigApplyResult applied = cfg.TryApplyPendingAtBoundary(
        FCMConfigApplyPolicy.NextTask, null);
    if (!applied.Applied) Console.WriteLine("尚未生效：" + applied.Status);
}
```

**使用口径**：`UpdateAsync` 已进入源码，明确的 `Success` 判断不可省；调用 `TryApplyPendingAtBoundary` 的宿主必须负责适当边界及安全 Guard。若采用用户权限会话，应使用 `service.Authorize(...).Open<T>(identity)` 而非将裸 `cfg` 暴露给非可信插件。

### 10.2 开发者常用操作速查

| 目的 | 最简现有调用 | 何时改用详细方式 |
|---|---|---|
| 整体/截面/字段 | `Get()` / `Section(...)` / `Value(...)` | 高频使用 `PrepareValue`；长任务用 `CaptureForTask` |
| 字段来源、默认及前一版 | `Explain(...)` / `ExplainState(...)` | 系统健康与审计用 `GetStatus` |
| 改一个字段 | `SetValueAsync(...)` | 动态字典复杂修改用 `UpdateAsync` |
| 一次改多个字段 | `UpdateAsync(model => {...})` | 需要人手预览草稿用 `BeginEdit` / `MHSConfigEditorModel<T>` |
| 订阅运行值 | `Watch(...)` | 维护工具订阅保存用 `WatchSaved` |
| 控制生效 | `TryApplyPendingAtBoundary(...)` | 有 Guard 时明确拒绝/故障原因 |
| 历史查看/恢复 | `AtSavedVersion` / `RestoreSavedVersionAsync` | 发布回滚/远端审批当前不支持 |
| 配置包 | `ExportPackage` / `ImportPackageAsync` | 网络签名分发不在当前范围 |

### 10.3 授权调用范例

```csharp
FCMConfigIdentity identity = new FCMConfigIdentity(
    "Vision.Capture", "machine", "Machine-A");
FCMConfigRolePolicy policy = new FCMConfigRolePolicy();
policy.Grant("operator", FCMConfigAccessAction.Read,
    identity.SectionId, identity.ScopeKind, identity.ScopeId);
MHSConfigAuthorizedSection<CaptureConfig> limited = service.Authorize(
    new FCMConfigPrincipal("operator-A", new[] { "operator" }), policy).Open<CaptureConfig>(identity);
int readOnlyExposure = limited.Value(x => x.ExposureMs);
```

权限实例由可信宿主管理；非可信插件不得同时拿到原始 `service` / `MHSConfigSection<T>`。授权调用必须在每次读取、订阅事件和提交/生效时重新校验，不能仅在创建 UI 窗口时校验一次。【现状】已有授权会话和动态权限检查；【目标】对所有导入导出/日志/差异/错误文本实施完备脱敏。

### 10.4 “简单”不等于隐藏复杂结果

**简单入口负责减少样板代码，错误仍必须透明。** 保存与生效的结果不能合并为一个模糊布尔；错误至少应有 Code、Path（适用时）、Revision、PendingApply、用户可理解的状态文本。新封装不得跳过事务锁、验证器、审计、持久化和 Guard。

## 11. 四个开发者验收案例（完整调用链）

本节为**使用合同**；同名业务在 `Tests/ArchitectureSmoke/ConfigDeveloperExperienceTests.cs` 中已有实际自动测试，覆盖 OrdinaryDeveloper、XRayMultipleDetectors、CtLongTask、AtomicProductRecipe、SecurityAndCatalog、PreparedReadAndConcurrency。下面的示例使用专用教程 POCO，真实测试模型以仓库 `V13ConfigSample`、`V13AsyncConfig` 为准，**不可将示例运行视为本轮又执行过测试**。

### 11.1 案例 D01：普通应用配置

**目标**：首次初始化 → 默认值 → 修改两个字段 → 校验 → 保存 → 分时生效 → 读取 → 重启仍能读取显式来源。

**前提模型**：上节 `CaptureConfig`。操作：

1. `Open<CaptureConfig>()` 初始 `ExposureMs=80` 且 `ExplainState` 应为 `Defaulted`。
2. 只修改 `EnableCorrection`，曝光字段来源仍为 `Defaulted`；不能由“整个文档已经有新 SavedRevision”推断所有字段均显式设置。
3. 调用 `UpdateAsync` 将 `ExposureMs=120`；因为该字段为 NextTask，`Saved=120` 而 `Value(ExposureMs)` 仍为 80，`Watch` 尚无 Effective 通知。
4. 运行宿主到任务边界应用新配置；`Value`=120，`Explain` 中默认仍为 80，Watch 只产生一次真正生效的事件。
5. 重新创建宿主服务打开同一个身份，来源仍为显式设置；历史版本可读且未被原地重写。
6. 修改为不合法的曝光 `0`，预期验证失败，Saved/Effective/Revision 不变。

**失败验证**：不合法 Range、自定义规则异常、版本冲突、无写权限目录、订阅回调抛异常。 **运行证据**：`ConfigDeveloperExperienceTests.OrdinaryDeveloper` 及 `ConfigurationIntegrationTests`。

### 11.2 案例 D02：X-Ray 多探测器配置

完整模型示例：

```csharp
public sealed class DetectorOptions
{
    [Range(1, 10)]
    public int Gain { get; set; } = 1;
}

[FCMConfigSection("Vision.MultiDetector")]
public sealed class MultiDetectorConfig
{
    [FCMConfigItem("曝光时间", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
    public int ExposureMs { get; set; } = 80;
    public System.Collections.Generic.Dictionary<string, DetectorOptions> Detectors { get; set; }
        = new System.Collections.Generic.Dictionary<string, DetectorOptions>();
}

MHSConfigSection<MultiDetectorConfig> cfg = service.ForMachine("XRay-3FPD")
    .Open<MultiDetectorConfig>();
MHSConfigCommitResult saved = await cfg.UpdateAsync(x =>
{
    x.Detectors["D-01"] = new DetectorOptions { Gain = 2 };
    x.Detectors["D-02"] = new DetectorOptions { Gain = 3 };
    x.ExposureMs = 95;
}, "多探测器成组参数", token);
if (!saved.Success) throw new InvalidOperationException(saved.ErrorCode);
cfg.TryApplyPendingAtBoundary(FCMConfigApplyPolicy.NextTask, null);
Func<int> readGainD2 = cfg.PrepareValue(x => x.Detectors["D-02"].Gain);
int currentGainD2 = readGainD2();
```

**预期**：动态键和深层字典可靠序列化；修改返回的字典副本不能改变真源；Prepared getter 在新修订真正生效前仍读旧值，生效后读新值；一个提交涉及多个探测器时若任一字段非法则全体拒绝。实际设备配置是否同步到探测器、Gain 能否物理设置、设备 ACK 等属于专业联调，不归配置底座自动完成。

**附加 UI 验收**：可由 Schema/集合契约生成 D-01/D-02 明细表；增删键、数字编辑、错误定位、批量保存、敏感项裁剪，不得要求编辑 JSON 字符串才能完成普通操作。【UI 目标，未完成】

### 11.3 案例 D03：CT 长任务的多模型原子配置

**目标**：采图参数与重建参数共同提交、生效，持续运行中的 CT 任务固定相同版本；后续修改只影响新任务。

```csharp
[FCMConfigSection("CT.ScanOptions")]
public sealed class CtScanOptions
{
    public int ProjectionCount { get; set; } = 360;
}
[FCMConfigSection("CT.ReconstructionOptions")]
public sealed class CtReconstructionOptions
{
    public int Iterations { get; set; } = 10;
}

MHSConfigAtomicGroup group = service.OpenAtomicGroup("CT.ScanAndRebuild", members =>
{
    members.Register<CtScanOptions>("Scan");
    members.Register<CtReconstructionOptions>("Reconstruction");
});

MHSConfigCommitResult result = await group.UpdateAsync(edit =>
{
    CtScanOptions scan = edit.Get<CtScanOptions>("Scan");
    scan.ProjectionCount = 720;
    edit.Set("Scan", scan);
    CtReconstructionOptions recon = edit.Get<CtReconstructionOptions>("Reconstruction");
    recon.Iterations = 20;
    edit.Set("Reconstruction", recon);
}, "扫描与重建联合配置", token);
if (!result.Success) throw new InvalidOperationException(result.ErrorCode);
MHSConfigApplyResult applied = group.TryApplyPendingAtBoundary(
    FCMConfigApplyPolicy.NextTask, null);
if (!applied.Applied) throw new InvalidOperationException(applied.Status);

using (MHSConfigGroupSnapshot pinned = group.CaptureForTask("CT-Scan-001"))
{
    int scanProjections = pinned.Get<CtScanOptions>("Scan").ProjectionCount;
    int iterations = pinned.Get<CtReconstructionOptions>("Reconstruction").Iterations;
    // 这里开始实际任务之前，还需专业运控/设备就绪和真实资源检查。
}
```

**预期**：两个成员由**一个物理配置文档**提交，无论重启、历史恢复或固定快照均共享聚合修订；任一成员验证失败时整组不提交；当后来将 Iterations 改为 30 并生效，旧 `pinned` 仍返回 20。`Register<T>/Seal` 的简写仅已有 `OpenAtomicGroup(groupId, declare)`，字符串 key 强类型化仍属下一阶段目标。

### 11.4 案例 D04：多层产品工艺配方

**目标**：默认/工程/机台/产品/临时覆盖按字段继承；用户可移除覆盖回退上层；全部层级在一个原子档案提交/切换；记录来源及任务版本。

```csharp
MVBConfigLayerSpec[] layers = new[]
{
    new MVBConfigLayerSpec("Template", "template", "Base", 10),
    new MVBConfigLayerSpec("Project", "project", "Job", 20),
    new MVBConfigLayerSpec("Machine", "machine", "Line-1", 30),
    new MVBConfigLayerSpec("Product", "product", "Cell-A", 40),
    new MVBConfigLayerSpec("Override", "runtime", "Session", 50)
};
MVBConfigAtomicLayerProfile<CaptureConfig> recipe =
    new MVBConfigAtomicLayerProfile<CaptureConfig>(service, "Recipe-A", layers);
MHSConfigCommitResult saved = await recipe.UpdateAsync(edit =>
{
    edit.Set("Template", x => x.ExposureMs, 80);
    edit.Set("Machine", x => x.ExposureMs, 120);
    edit.Set("Product", x => x.ExposureMs, 150);
}, "产品配方调整", token);
if (!saved.Success) throw new InvalidOperationException(saved.ErrorCode);
MHSConfigApplyResult apply = recipe.TryApplyPendingAtBoundary(
    FCMConfigApplyPolicy.NextTask, null);
if (!apply.Applied) throw new InvalidOperationException(apply.Status);
int resolved = recipe.ResolveEffective().Value(x => x.ExposureMs); // 150
string source = recipe.ResolveEffective().Explain(x => x.ExposureMs).EffectiveSource; // Product
```

**预期**：`Template=80`、`Machine=120`、`Product=150`，最终为 150；清除 Product 覆盖后变为 120；Override=130 时为 130；Product 没有定义某个字段时继承低优先级来源；Save 与 Effective 分离；`CaptureForTask` 在未来层级切换后值不漂移。**重启后的层级定义漂移拒绝和完整锁定策略为目标，不能按本例运行成功就算已完成。**

### 11.5 跨案例使用体验评分

| 维度 | 当前最少可写代码 | V2.4 验收门槛 |
|---|---|---|
| 普通读取与更新 | POCO + `Open` + `Value`/`UpdateAsync` | 不需手写 IO/JSON；失败详情明确 |
| 运行订阅 | `Watch` 一行注册、Dispose 一次 | Saved 与 Effective 无混淆，无订阅泄漏 |
| CT 多模型 | 声明成员 + 一次 Update/Apply | 不要求开发者自行实现原子存储；成员键可加强类型化 |
| 产品配方 | `LayerSpec` + 一次 Update/Apply | 来源可解释，保存/生效分离；层级定义可冻结 |
| UI | 当前部分简单窗口仍需专业完善 | 普通 POCO 自动编辑，高级集合与四层能力闭环 |

## 12. 工程编排用户、运行宿主与配置界限

### 12.1 工程编排流程（人类视角）

```text
工程资源/配置目录 → 选择配置类型 → 选择/创建 Scope 或 Profile
    → 查看实际 Effective 版本与来源 → 在权限允许下编辑草稿
    → 实时校验与影响项提示 → 预览（哪些字段变化、由谁覆盖）
    → 保存为 Saved → 待生效队列/策略提示 → 在允许边界 Apply
    → 任务启动绑定 TaskId + Snapshot/RevisionVector → 执行记录持有固定身份
```

配置参与任务的是一个**已固定的输入合同**，不是流程节点数据栈的父对象。可以在编排中引用 `ConfigIdentity`/`ProfileId` 和选择版本模式（仅允许 Effective/TaskPinned 等明确语义）；严禁节点任意读 Draft、擅自根据磁盘文件变化切换正在运行的机台配置。

### 12.2 工程 UI 交互的五类对象

1. **目录节点**：配置 Section/实例/Profile，以及引用、Scope、历史、权限信息；资源树不混为 Page/Content/Element 的编辑画布对象。
2. **编辑对象**：Schema 字段树、Draft 和 SourceChain；动态集合是其孩子，不能脱离 Schema 类型约束。
3. **版本对象**：SavedRevision、EffectiveRevision、PinnedVersion、RevisionVector；可点击对比。
4. **操作对象**：Preview、Commit、Apply、Restore、Export/Import；操作走授权机制。
5. **证据对象**：校验报告、审计、提交/生效事件、Guard 拒绝、任务固定版本。

### 12.3 生效与工业设备边界

基础配置机制只决定某版本**被软件配置真源认可生效**；不声称已真正下发 PLC、运动控制板、X-Ray 源或探测器。专业 Runtime/Device Provider 对外部设备 ACK、回读校验、安全互锁和执行保护负责。后续需要“配置软件 Effective”与“实际设备确认版本”双状态时，应在现有机制与专业适配之间建立关联证据，不能让公共配置直接调用所有设备 SDK。【设备验收暂缓】

---

# 第四篇：自动配置界面的四层能力与交互

## 13. 第一层：所有受支持 POCO 的通用自动编辑

### 13.1 目标及当前差距

**【定案】** 普通配置类型无需专业写专用页面，即可生成一个可用的配置编辑界面；同一编辑器既能由 Studio 打开，也能作为可嵌入的 WPF 组件/独立窗口使用。只有 ROI、曲线、扫描轨迹等特别的专业编辑能力才需要自定义控件。

**【现状】** `MHSConfigEditorModel<T>` 已提供草稿、Schema 字段、Preview、Undo/Redo 和提交；`Mechanism/Presentation.Wpf/MPWConfigEditorDialogV14.cs` 存在初级弹窗，主要按成员生成文本框，复杂对象可能以 JSON 方式编辑。`Tests/ArchitectureSmoke/Program.cs` 的 `--config-wpf` 分支明确提示人工验收待完成、返回非成功码，**不能标记为完整通用 WPF 编辑器或手工验收已通过**。

### 13.2 字段类型到默认控件映射

| 字段类型 | 默认控件 | 至少支持的编辑/错误行为 |
|---|---|---|
| bool | CheckBox | 可按权限只读、默认/来源徽标 |
| 枚举 | ComboBox/Radio（取决于数量） | 人类标签与底层枚举值分开 |
| 整数、浮点、decimal | NumericInput（可带滑杆） | 范围、精度、单位、无效输入即时反馈，禁止静默截断 |
| string | TextBox | 长度、Regex、描述、是否敏感；SecretRef 不能文本明显展示 |
| Nullable<T> | 空值/非空切换 + 内部编辑器 | 区分未显式覆盖、显式 null、数值等于默认值 |
| 嵌套 POCO | 属性组/可折叠树 | 按 MemberId/Order 排列并定位错误路径 |
| Dictionary<K,V> | Key-Value 表/树 | 动态新增/删除/改名键、键冲突、深层 V 编辑 |
| List<T> | 动态元素列表 | 增删/移动/插入、按索引校验，更新旧/新 JSON 差异 |
| Set<T> | 不重复项目集合 | 增删/去重，不假定位置、不可凭重排改变语义 |
| 资源引用 | 类型化资源选择器 | 显示版本与存在性、失败诊断、有效引用校验 |
| 不支持/有歧义类型 | 显示“无法编辑/不支持” | 给出 TypeCatalog Issue；不偷偷改用纯文本代替 |

**编辑控件必须按契约选型**：不是 UI 用反射猜一个 TextBox，然后向 `PropertyInfo.SetValue` 强制转换全部字段。对未知集合成员，需先通过通用集合编辑命令保留原有对象复制/校验/事务语义。

### 13.3 通用配置表单应包含的主要区域

```text
┌ 配置标题：SectionId / Scope / Profile / 当前 Effective / Saved ─────┐
│ 左：字段类别与搜索      右：通用字段编辑区                        │
│   采集/重建/设备/...        曝光时间 [ 120 ] ms  [来源 Machine]       │
│   字典成员层次树           探测器列表 ▾ D-01 / D-02 [增/删/改]      │
│                           嵌套参数 ▾ Group.TimeoutMs               │
│ 底部：校验错误列表/差异预览/来源/历史/权限说明                       │
│ [撤销] [重做] [恢复草稿] [预览] [保存] [申请生效] [历史]             │
└─────────────────────────────────────────────────────────┘
```

普通字段生成不是把“保存”直接等价于“应用”。编辑页可明确显示待生效数、所需边界、已保存版本及生效版本。

### 13.4 动态集合的统一编辑模型【目标】

底层需要**无 UI 的集合描述/编辑能力**：`Path`、Key/Index、元素类型、是否可重排、允许增加/删除、是否显式设置、默认值及来源、错误路径、修改前后差异。实现优先扩展 `FCMConfigFieldInfo` / `MHSConfigEditorModel<T>` 和既有 Path/Codec，避免新建多套集合编辑子系统。编辑 Dictionary 中元素时必须处理特殊符号 Key 的编码、大小写/比较器语义、并发编辑同一 Key、空列表/空字典、复杂对象隔离。集合校验应遍历动态子项而非只停在父属性。

## 14. 第二层、第三层：专业级管理与层级/多模型组织

### 14.1 第二层：配置管理动作完整闭环【定案】

**必须支持**：

- Schema/类型显示、默认值、当前候选、保存值、有效值、来源、上次保存/生效、差异；
- 可编辑权限与只读权限、字段/父节点敏感裁剪，用户没有权限时不能从 JSON 编辑器、Tooltip 或历史页面看到秘密；
- 规则错误精确定位、跨字段联动提示、异步校验忙/失败/取消显示；
- BeginEdit、Modify、Undo、Redo、Preview、Commit、Apply、历史恢复全由统一服务/EditorModel 驱动；
- Guard 拒绝、待生效状态、重试建议、版本冲突重新加载/比较，而不是简单弹窗“保存失败”；
- `ChangedPaths` 与 `ChangeSet` 显示旧/新字段并执行脱敏；
- 长时间编辑时明确提示“他人已提交新版本”，避免无提示覆盖。

**操作按钮语义**：`保存`只允许产生 Saved；`申请生效`必须先通过权限和 Guard 并在允许边界执行；`恢复`应基于已提交历史新建 Saved；`撤销`只作用未保存 Draft；`刷新`不得暗中抛弃用户修改，必须提示冲突。任何操作都须有权限预检查和执行时二次检查。

### 14.2 第三层：层级与高级组织【定案】

分层编辑器应允许：创建/选择 Profile、预览层级结构、比较来源字段、设置或删除覆盖、查看最终合成值、切换 Saved/Effective 模式、观察来源链和版本向量；单一配置若不需要层级，则完全不应让用户配置五层模板。

原子层档案应能一次修改多个来源并一次提交；独立层模式则明确标注“此操作仅修改当前来源层”，多层操作不得显示“全部已原子保存”。重新打开 Profile 时检查冻结层级契约是否变化；若不一致，UI 进入只读冲突诊断/升级预览状态，而非直接按新顺序解释旧数据。【目标，冻结/升级机制未实现】

高级组织也包括多模型聚合编辑、模板选择、配置搜索、历史对比、配置包验证、诊断报告、任务固定版本查看，不要求每一个专业重写这些工具。

### 14.3 保存—生效 UI 状态表

| Draft | Saved | Effective | UI 应显示 | 可执行动作 |
|---|---|---|---|---|
| 无修改 | v1 | v1 | 已同步/生效 v1 | 打开编辑/历史 |
| 修改中 | v1 | v1 | 未保存草稿 | 校验/预览/保存/撤销 |
| 无草稿 | v2 | v1 | 已保存 v2，待生效 v1→v2 | 请求生效/查看差异/继续编辑 |
| 无草稿 | v2 | v1，Guard 拒绝 | 待生效且被拒绝，提示原因 | 修正问题/稍后重试/按权限恢复 |
| 无草稿 | v2 | v2 | 生效 v2 | 固定任务版本/审计 |
| 版本冲突 | v3（外部） | v2 | 草稿基于旧 v1 或 v2 | 刷新、差异对比、重新编辑；不得静默合并 |

### 14.4 六角色 UI 权限投影

操作人员默认只见自己相关字段/页面；调试人员可见诊断但未必有 Apply；维护人员可见审计/恢复但不必有敏感明文读取；专业开发者可在测试环境查看完整 Schema/规则。UI 隐藏按钮只是便利，不是安全边界，最终授权在 `MHSConfigAuthorizedSession` 内逐操作复核。

## 15. 第四层：专业控件扩展、Studio 与嵌入式 UI

### 15.1 允许扩展的内容

Vision 的 ROI 编辑、图像路径/遮罩、探测器阵列布局；CT 的扫描轨迹/几何位置/重建算子方案；Automation 的轴参数曲线与工艺节拍；HMI 的样式/颜色；IoT 的地址表/点表，均可由专业提供**某字段或某个子组的编辑器**，但字段值必须回写统一 Draft 并经过相同验证/事务/生效路径。

### 15.2 三种嵌入形态【定案】

1. **Studio 集成**：从当前工程/专业上下文创建适当 Scope/Profile，打开同一通用配置编辑器。
2. **专业程序嵌入**：例如独立 X-Ray/CT 程序在自己的 WPF 页面放置配置编辑控件，不必运行 Studio。
3. **独立弹窗**：用于维护工具、临时调试、配置目录浏览和诊断。 

这三种只是 WPF 呈现形态，应绑定一个 Mechanism 编辑会话，不得复制一份服务真源。不能将 WPF 依赖塞入 `Mechanism.Data`/`Foundation.Data`，也不能为了嵌入另建 `ConfigStudio` 专项项目。

### 15.3 目标编辑器的最小公共合同（设计示意，**非当前 API**）

- 输入：`Schema + ConfigIdentity/ProfileIdentity + AuthorizedEditContext + 当前 Saved/Effective + EditorModel`。
- 输出：字段树/表单、只读/可写/隐藏、校验定位、Preview/Commit/Apply/历史动作与结果；以现有 `MHSConfigCommitResult` 和 `MHSConfigApplyResult` 反馈。
- 扩展：按数据类型、字段路径、`MemberId`、专业上下文选择编辑控件；可声明优先级，但不可绕过校验和授权。
- 生命周期：打开→修改→保存/取消→释放资源；订阅句柄 Dispose；跨 UI 线程派发必须通过 WPF Dispatcher，不以订阅回调直接触碰 UI 控件。

### 15.4 UI 完成定义

不能用“一张窗口截图”验收。必须完成：实际可编译 WPF 控件；自动发现任意合法 POCO；深层 6 级嵌套、动态 Dictionary/List/Set；Nullable；自定义规则和验证错误定位；Undo/Redo；Saved/Effective、来源、权限、敏感数据；Studio 嵌入/专业嵌入/独立弹窗至少三种调用；正常与异常操作；Windows STA 自动或明确记录的人工操作记录。

当前初级 `MPWConfigEditorDialog` 已存在，但**以上四层目标尚未整体通过**，不得在验收矩阵中标为已完成。

---
# 第五篇：正确性、安全、恢复与性能

## 16. 持久化、Schema 兼容、恢复和一致性

### 16.1 当前本地存储事实【现状】

在 `MHSConfigStorage` 中，典型配置实例使用：

```text
<instance-folder>/
    current.json
    effective.txt
    previous-effective.txt
    effective-journal-origin.txt
    write.lock
    history/
        r00000001.json
        r00000002.json
        r00000002.parent
        r00000002.audit.json
        r00000002.effective.json
        ...
```

上述是代码约定的文件类型示意，具体某一版本是否生成 `.parent/.audit/.effective` 取决于初始化、提交、生效历史；不得把所有示例文件视为每次必然存在。当前 `Commit()` 进行文件比较、独占写锁、修订保存、审计与 parent 记录，再替换 `current.json`；`SetEffective()` 写生效 Journal/前驱与 marker；`ReadEffectiveHistory()` 沿确认的有效前驱链重放。`ReadCommittedAfter` 沿已提交历史及审计记录恢复。

### 16.2 必须维持的持久化不变量

**S01** `current.json` 应仅标识完整、可解析、校验通过的已提交修订；失败前旧版本可恢复。  
**S02** `history/rXXXX.json` 不可因用户“撤销”被原地覆盖。  
**S03** 未完成的审计、`parent` 或 effective 记录不能静默被当作新的成功提交/生效。  
**S04** Saved/Effective 文件、内存副本、事件记录须有因果关系；持久化生效失败绝不能派发成功通知。  
**S05** 启动读取遇到损坏、类型不匹配或无有效源，不可把“默认配置”当作无风险恢复。  
**S06** 文件写入成功不等于断电持久性已完整验证；须做故障注入和真正重启/崩溃测试。  
**S07** 跨文件多层合成不得用单文件 `File.Replace` 证明整个 Profile 原子性。

### 16.3 当前发现的风险与裁决

代码核查发现：`MHSConfigStorage.Commit()` 对 Saved 写入使用跨进程独占 `write.lock` 和比较机制，然而 `SetEffective(...)` 在核查版本中尚未采用相同的跨进程生效 CAS。**这是静态风险发现，不表示已复现故障**。用户已明确“跨进程配置管理暂时不需要”，故本阶段仅要求**单进程内并发版本安全、启动恢复正确、无假成功通知**；跨进程生效 CAS 纳入长期兼容风险清单，不作为本阶段阻断项。若未来开放多进程共享目录写/Apply，必须先补预期 Effective/当前 Saved/锁/重启一致性测试。

另一项明确缺口：`MVBConfigAtomicLayerProfile` 构造时传入 `MVBConfigLayerSpec[]`，现有聚合文档保存成员和覆盖值，但没有充分证据证明已将完整层名/优先级/Scope/目的/策略定义作为冻结合同持久化；这与本轮定案直接相关，应作为**当前优先改造项**。

### 16.4 Schema 升级与字段保序

【现状】有 `SchemaVersion`、字段 `AliasFrom/SinceSchemaVersion/BeforeId/AfterId` 等元数据和 JSON patch/保序能力；【未完整】跨版本变更、删除/重命名嵌套字段、类型转换、旧格式 YAML/INI 注释无损迁移与未知字段保留的**完整链式迁移**。

目标升级规则：旧文件先被识别为源版本；记录将要执行的迁移链/丢失风险；先在候选上验证；出错不动 Saved/Effective；成功产生新提交并记录来源/目标 SchemaVersion；未映射字段不得无提示丢弃。若源版本高于宿主支持版本，必须拒绝写入而不是降级覆盖。

### 16.5 本阶段与长期边界

本阶段需完成本地 JSON 原子单元、持久历史、单进程事务/生效正确性、层级定义保护、错误提示、单进程崩溃故障测试。**跨进程实时通知、跨进程生效竞争、跨机发布、分布式原子提交、完整 YAML/INI 无损迁移及灾备 SLA** 保留为后续阶段，仍在需求追踪表中出现，但标明“暂缓”。

## 17. 权限、敏感字段与审计诊断

### 17.1 权限原则

**默认拒绝、最小能力授予、动态复核**。`FCMConfigRolePolicy`、`FCMConfigPrincipal`、`IFCMConfigAccessPolicy`、`FCMConfigAccessRequest` 等提供角色/动作/身份的权限契约；`MHSConfigAuthorizedSession/AuthorizedSection/AuthorizedEdit` 是本地授权调用界面。非可信调用方只应收到授权句柄，禁止同时得到可绕行的裸 Service/Section。

授权不因“成功打开一次 Section”、“已准备一个 Func getter”、“已经订阅一个回调”而永久有效。撤权后后续读取/提交/生效/通知均须按当前权限拒绝。已产生的旧本地对象引用能否被撤销，需在宿主能力分发层明确，不以 UI 隐藏字段代替权限策略。

### 17.2 敏感信息的全链路策略

| 表面 | 目标处理 | 当前确认情况 |
|---|---|---|
| Catalog/自动文档 | 敏感字段及嵌套集合父项不泄露真实默认 JSON | 已有相应脱敏测试 |
| Value/Get/PrepareValue | 读取指定路径检查 ReadSensitive；全模型含敏感内容受限 | 授权 Section 具备路径/整体判定 |
| Watch/WatchSaved | 注册时及每次回调时校验订阅、读取权限 | 已有授权回调机制 |
| Preview/ChangeSet/Status | 对 OldJson/NewJson 和错误报告按字段路径脱敏；不以 Audit 绕过读取 | 状态/审计已加访问检查，细粒度脱敏尚需完整证明 |
| 异步校验错误 | 禁止把凭据或数据体直接放在异常 Message | **目标，待审查/测试** |
| 包导出/导入 | Export 需要专门权限和 ReadSensitive；未经授权不输出明文 | 有权限入口，需组合回归 |
| 审计/历史/备份 | 读取历史与日志权限分离，敏感内容不意外暴露 | 部分检查，完整导出/备份策略待核实 |
| Studio/WPF/诊断 | Tooltip、复制、筛选、对比、Undo 历史均不可绕权 | 完整 UI 未实现 |

**密钥应尽量存引用**（如 `FCMConfigSecretRef`），而非凭证内容；配置体系不代替外部 Vault、操作系统密钥管理、网络身份认证。专业安全/外部密钥服务属于适配与专项验收。

### 17.3 安全错误码与用户反馈

系统内部错误应保留可诊断的 Code 和 Path；公开反馈必须避免回显敏感值。权限不足需提示“无权读取字段”而不是拼接字段的真实值；校验器异常不得因为抛出的原始 `Exception.Message` 含密码而直接展示给无权限用户。日志、审计必须能追踪哪一个 `Principal` 提交/申请生效（需要已有结果/审计契约进一步覆盖），但记录主体 ID 不等于可以记录其口令。

## 18. 单进程并发、生命周期、高频读与性能

### 18.1 并发边界【本阶段】

单进程多线程下，编辑基于修订，提交采用乐观 CAS；失败者应收到冲突并重新取版本，而不是隐式重试覆盖更新。生效 Guard 在锁外审查时，保存/有效版本变化必须拒绝这次 Apply。通知回调可重入、抛异常或消费缓慢，不能使已经完成的事务回滚或使事件顺序混乱。

**跨进程协同暂缓**：本阶段的线程安全与持久化校验不意味着允许多个独立宿主同时写同一配置根目录、共享生效安全版本。此边界必须在部署文档中清楚标注。

### 18.2 高频读取两种模式

1. **实时关注最新生效值**：`Func<V> f = cfg.PrepareValue(selector)`，编译表达式一次，每次读取当前 Effective，仍受锁/复制策略约束，可能读到任务中途的新版本。
2. **任务内固定值**：`using (var pin = cfg.CaptureForTask(taskId)) { Func<V> f = pin.PrepareValue(selector); ... }`，不随任务中途的版本发布变化。对高频采图/重建循环优先使用固定快照标量读取；不得把不可变资源与完整 CT 像素缓存强行存进配置系统。

要明确 `Value/Get` 的 JSON 复制成本，复杂字段返回深拷贝而不是内部可变引用。高频读取不能通过公开 `_effective` 可写指针取巧。

### 18.3 性能基准合同

真实基准须记录 Windows 版本、CPU、内存、磁盘类型、.NET Framework 4.8 Runtime、进程位数、配置字段数/深度/序列化大小、线程数量、冷/热启动、GC 次数与耗时分位数。至少做：

- `PrepareValue` 标量循环 2,000,000 次（当前 `ConfigDeveloperExperienceTests` 已包含可运行的计时环节）；
- 普通 `Value` 与固定 Snapshot getter 对照；
- 大字典 1k/10k 元素的读/改/校验/提交/重启；
- 1/4/16 并发读线程 + 1 提交线程 + 1 生效线程，观察结果一致性；
- 订阅者 1/100/1000，慢回调、异常回调、订阅取消；
- 100/1000 次提交后的历史查询和文件体积增长；
- 长任务读取 10 分钟以上、反复 Capture/Dispose 与内存稳定性。

**不预填伪造的 QPS/P99 指标。** 目标数值应以目标机台的周期预算与 baseline 实测决定，报表必须填入实测数字、统计口径、结果文件、提交 SHA；“有 Stopwatch 代码”不等于性能达标。

### 18.4 生命周期准则

宿主创建 Service，专业在许可范围内共享；每次打开同一身份应避免产生多个互相竞争的进程内真源。编辑器关闭释放编辑会话；配置 Watch/Feed 解除订阅；任务结束 Dispose pinned snapshot；UI 后台通知切回 WPF Dispatcher；异常路径也不得泄露处理器或活动编辑锁。动态插件卸载时，存在活动 Section/订阅/任务快照应有明确定义，不为强制卸载制造悬空类型引用。

---

# 第六篇：实现核查、需求追踪与验收

## 19. 完整职责、需求、来源与状态矩阵

### 19.1 五级证据分级

| 状态标签 | 含义 | 允许的结论 |
|---|---|---|
| D：已定案/已设计 | 有清晰目标、边界、接口合同但未核实代码 | 只能说“目标已确认” |
| S：源码存在 | 读到真实类型、行为或检查 | 不能单凭源码说“测试已通过” |
| T：自动测试覆盖 | 有可执行用例，核对相应 HEAD CI 运行成功 | 可说“所述用例在该构建中通过”，不外推工业现场 |
| A：场景验收完成 | 真实宿主、操作路径、失败案例、环境、运行证据均齐全 | 仅限相应场景和边界 |
| H：暂缓 | 用户明确不要求本阶段交付 | 保留追踪，不能宣称已完成 |

此处的 `S/T` 是**根据本次读取源码及 Actions 在 HEAD 的成功状态作出的代码级摘要**；对每个字段/功能仍需按真实测试断言逐项核对，不把整个测试程序集绿灯转换为所有条目 A。

### 19.2 27 组职责逐项追踪（A01—A15 + B01—B12）

> V1.3 原始命名未找到，以下分组语义根据 V2.0/V2.2 的合并条目恢复，必要时采用可读名称；**恢复原文后逐项对照并修正官方分组标题**。实现类型列为典型 Owner，非该组全部唯一 API。

| ID | 基础/机制职责 | 主要真实 Owner/API | V2.4 判断与关键缺口 |
|---|---|---|---|
| A01 | 配置声明/模型 | `FCMConfigSectionAttribute`、`FCMConfigItemAttribute` | S/T：基本声明；插件多版本升级待核 |
| A02 | 规则元数据/依赖 | `FCMConfigDependsOnAttribute`、Validator/CustomValidator | S/T：规则存在；增量调度和资源预算待做 |
| A03 | Schema 与目录契约 | `FCMConfigSchemaBuilder`、`FCMConfigCatalog` | S/T：元数据/自动文档；动态集合字段树待做 |
| A04 | 身份、Scope、Path | `FCMConfigIdentity`、`FCMConfigPathUtil` | S/T：长度前缀与深层路径；跨网络定位 H |
| A05 | 层级与来源契约 | `MVBConfigLayerSpec`、`MVBConfigLayerOrigin`（机制） | S/T：五层/自定义基础；**层定义冻结 D**、锁定规则 D |
| A06 | Revision/VersionVector | `FCMConfigRevision`、`FCMConfigRevisionVector` | S/T：Saved/Effective/组合向量；跨机版本 H |
| A07 | 五态值契约 | `FCMConfigValueState<T>`、`FCMConfigValueInfo<T>` | S/T：五态基本语义；全部复杂来源诊断待补 |
| A08 | 读视图/快照 | `IFCMConfigView<T>`、`MHSConfigSnapshot<T>` | S/T：任务固定；硬实时性能预算待测 |
| A09 | 基础字段校验 | `FCMConfigValidationEngine`、DataAnnotations | S/T：Range/Regex/Required；动态深层 UI 错误链待补 |
| A10 | 复合、自定义及异步校验 | `IFCMConfigValidator<T>`、`IFCMAsyncConfigValidator<T>` | S/T：异步/副作用测试；限额/超时 D |
| A11 | 类型契约、Converter | `FCMConfigTypeCatalog`、`IFCMConfigValueConverter<T>` | S/T：可往返类型；复杂受控扩展待验 |
| A12 | 编辑差异/ChangeSet | `FCMConfigChangeSet`、`FCMConfigFieldChange` | S/T：差异、草稿、撤销；全类型动态操作 D |
| A13 | 通知/变更证据 | `FCMConfigChange<T>`、`FCMConfigChangeStage` | S/T：Committed/Effective；跨进程实时 H |
| A14 | 生效策略/状态 | `FCMConfigApplyPolicy`、`MHSConfigApplyDecision` | S/T：软件 Guard/策略；设备 ACK H |
| A15 | 扩展端口 | AccessPolicy、ResourceProvider、Converter | S/T：主要接口；远程发布/认证 H |
| B01 | 统一服务/注册 | `MHSConfigService` | S/T：Open/Scope；生命周期规范继续收敛 |
| B02 | 自动发现/配置目录 | `RegisterAssembly`、`GetRegisteredSections` | S/T：注册与卸载保护；不等于安全热加载 |
| B03 | 多来源解析 | `MVBConfigLayeredProfile<T>` | S/T：独立层解析；层级冻结和锁定 D |
| B04 | 快照/聚合读 | `MVBConfigResolved<T>`、`CaptureForTask` | S/T：版本向量；远程资源固定 H |
| B05 | 编辑事务与聚合 | `MHSConfigEdit<T>`、`MHSConfigAtomicGroup` | S/T：单持久单元原子；强类型成员 D |
| B06 | 校验执行 | `FCMConfigValidationEngine`、聚合提交前验证 | S/T：同步异步；增量调度/预算 D |
| B07 | 文档处理/保序 | `FCMOrderedJsonDocument`、Codec | S/T：JSON patch；非 JSON 格式 H |
| B08 | Schema 演进/迁移 | Alias/Version 等元数据 | D：完整保序迁移链未交付；旧机迁移 H |
| B09 | 保存/有效历史 | `MHSConfigStorage`、Audit/Effective Journal | S/T：本地持久链；故障注入需扩大 |
| B10 | 通知/重放/消费 | `MNMConfigChangePublisher<T>`、`MHSConfigCommitFeed<T>` | S/T：本地与游标；跨进程实时 H |
| B11 | 生效机制 | `TryApplyPendingAtBoundary`、Apply Guard | S/T：单进程语义；跨进程 Apply CAS H |
| B12 | 健康/诊断/编辑驱动 | `MHSConfigStatus`、`MHSConfigEditorModel<T>` | S/T：无 UI 模型；四层完整 UI D |

### 19.3 24 项需求逐项追踪

| ID | 需求名称 | 验收要点 | 当前判定 | 追踪入口 |
|---|---|---|---|---|
| RQ01 | 声明普通配置 | POCO/Attribute，无基类、可发现 | S/T | `FCMConfigContracts`、`ConfigV13ExtensionTests` |
| RQ02 | 自动初始化与实例隔离 | 首次默认、Scope 隔离、重开稳定 | S/T | `MHSConfigService`、`ConfigurationIntegrationTests` |
| RQ03 | 完整/截面/单字段读 | 类型正确、副本隔离、深层路径 | S/T | `MHSConfigSection`、`ConfigDeveloperExperienceTests` |
| RQ04 | 简单及事务修改 | 多字段同一提交、验证、冲突拒绝 | S/T | `UpdateAsync`、`MHSConfigEdit` |
| RQ05 | 运行时参数使用 | Effective 与任务固定，运行不撕裂 | S/T 软件层 | `CaptureForTask`、CT 案例 |
| RQ06 | 自动持久化及恢复 | 版本与历史，异常后不造假 | S/T 部分 | `MHSConfigStorage`、故障矩阵待扩大 |
| RQ07 | 自动通用配置 UI | 四层能力、三种嵌入、深层集合 | **D，未完成** | `MHSConfigEditorModel` + 现有 WPF Dialog |
| RQ08 | 保存/生效通知 | WatchSaved/Watch 区分，Old/New/Default | S/T | `MNMConfigChangePublisher` |
| RQ09 | 生效策略/Guard | Immediate/NextTask 等、拒绝有效值不变 | S/T 软件层 | `MHSConfigApply`、`ConfigArchitectureCompletionTests` |
| RQ10 | 版本发布 | 导出包哈希、目标发布/回退 | 本地 S/T，**跨机 H** | `MHSConfigPackage`，ReleaseManifest H |
| RQ11 | 多层继承 | 自定义优先级、移除覆盖 | S/T 基础；冻结 D | `MVBConfigLayeredProfile`、`AtomicLayerProfile` |
| RQ12 | 来源诊断 | 每个字段 SourceChain、RevisionVector | S/T 部分 | `MVBConfigResolved.Explain` |
| RQ13 | 自动发现注册 | 程序集发现、重复身份拒绝 | S/T | `RegisterAssembly` |
| RQ14 | 配置搜索导航 | Section 查询、字段搜索、可定位实例 | S/T 后端；UI D | `FCMConfigCatalog`、Service |
| RQ15 | 多字段/多模型批量 | 单实例/原子聚合/原子层级一次提交 | S/T | `MHSConfigAtomicGroup` |
| RQ16 | 差异与撤销 | Preview/Undo/Redo；历史恢复新增修订 | S/T 草稿；完整 UI D | `MHSConfigEditorModel` |
| RQ17 | 字段依赖联动 | DependsOn 图、影响分析、环拒绝 | S/T 基础；增量执行 D | `FCMConfigDependencyGraph` |
| RQ18 | 模板与覆盖 | 模板实例、层优先级和回退 | S/T；层定义冻结 D | `MVBConfigLayerSpec` |
| RQ19 | 配置/资源引用 | 强身份引用、循环/缺失诊断 | S/T 软件模拟；真实 Provider H | `MHSConfigReferenceRegistry` |
| RQ20 | 任务固定快照 | 同任务旧版不漂移、Dispose 后拒绝 | S/T | `CaptureForTask`、CT 案例 |
| RQ21 | 跨进程协同 | 持久游标 vs 实时网络通知区分 | 游标 S/T；**协同 H** | `ReadCommittedAfter`、`ReadEffectiveAfter` |
| RQ22 | 访问控制与审计 | 默认拒绝、敏感嵌套、动态撤权 | S/T 部分；全链路 D | `MHSConfigAuthorizedSession` |
| RQ23 | 调试、自动测试、性能 | 可重复 CI、故障注入、性能门槛 | CI T；故障/性能 D | ConfigFoundationSmoke、ArchitectureSmoke |
| RQ24 | 自动文档与扩展 | Catalog/Markdown/Converter，专业编辑器 | 文档 S/T；编辑器 D | `FCMConfigCatalog`、WPF |

### 19.4 当前已验证证据与明确不能推断的事情

**GitHub Actions 证据（同一 HEAD）**：

- [`配置公共底座独立编译与验收` 运行 #38855992061](https://github.com/oudream/cx-visual-x3/actions/runs/38855992061)：查询结果显示 `completed / success`，HEAD `3c9328e7...`。工作流编译 `Tests/ConfigFoundationSmoke/CxVisual.Tests.ConfigFoundationSmoke.csproj`、运行对应 CLI，入口依次执行四个配置测试组。
- [`CxVisual Config L1 Windows Build and Smoke` 运行 #38855992076](https://github.com/oudream/cx-visual-x3/actions/runs/38855992076)：查询结果显示 `completed / success`，同一 HEAD；工作流编译完整 `CxVisual.Unified.sln` 并运行 Foundation/F19、ArchitectureSmoke、Runtime demo/Vision demo 等命令。
- 当前没有读取本次这些运行中每条测试的逐行日志，也**没有在本轮执行新的构建/测试**；本文件将 Actions 总体成功与“特定未涵盖边界仍待测试”并列记录。
- `testcenter-windows.yml` 存在，但本次没有查到同 HEAD 的该工作流通过记录；不据历史绿色结果推定本次完整 UI 或硬件验收。

**禁止外推**：以上 Windows CI 成功并不证明完整 Studio 配置界面、跨进程实时订阅、真实探测器/X-Ray/PLC 联锁、CT 多机资源、跨机审批回滚、全部 Schema 迁移或现场稳定性。

## 20. 测试门禁、阶段验收与发布判定

### 20.1 Gate 定义（代码证明与产品证明分离）

| Gate | 内容 | 本阶段是否阻断 | 必要证据 |
|---|---|---|---|
| G0 架构/Owner | 双项目、无第二套真源、依赖 DAG、规范/源码一致 | 是 | 文件/引用检查、API 审计 |
| G1 基础模型 | POCO/类型/身份/值状态/Schema、默认、深层类型 | 是 | 基础自动测试+正反例 |
| G2 事务与校验 | 更新、版本 CAS、差异、聚合、动态规则、异常不污染 | 是 | 集成测试/故障注入 |
| G3 生效与快照 | Saved/Effective、策略、Guard、Watch、长任务固定 | 是 | 运行用例与前后版本证据 |
| G4 层级与安全 | 自定义层级、**定义冻结**、来源、权限、脱敏 | 是 | 重启漂移/错误路径/撤权测试 |
| G5 开发者体验 | 四类案例、简单 API、强类型层级/聚合用法 | 是 | 完整调用与简洁度回归 |
| G6 自动 UI | 四个能力层次、Studio/嵌入/弹窗、STA 行为 | **整套 V2.4 产品验收时阻断**；纯底座阶段可单列进行中 | UI 自动/人工操作记录 |
| G7 性能可靠性 | 高频、长任务、压测、故障注入、恢复 | 基础门槛是；目标数值需另核准 | 报告/机器/耗时/内存/结果 |
| G8 跨进程/跨机/设备 | 跨进程 Apply/通知、远端发布、真实联锁 | **暂缓 H** | 将来另立专项门禁，不冒充完成 |

### 20.2 正式验收顺序

1. **固定代码基线**：记录 `git rev-parse HEAD`，读取最新 AGENTS、DOCS/00、V2.4，检查 `master` 是否被其他任务更新。
2. **审查物理 Owner 和依赖**：只改配置两个项目与必要的现有 UI/测试文档，不新建并行模块。
3. **运行独立配置 Windows 构建与测试**：使用 `.github/workflows/config-foundation-core-ci.yml` 的实际命令和已配置的 x64 Release 环境。
4. **运行完整解决方案构建与架构回归**：使用 `.github/workflows/config-l1-ci.yml`，注意尾部共享 Smoke 是 `continue-on-error: true`，不能将其自动视为所有测试门禁强制通过。
5. **执行 UI 验收**：Windows WPF/STA 真窗口打开、字段输入、校验提示、提交/生效及嵌入样例；无真实人工/自动窗口结果必须标“待验”。
6. **执行故障注入与性能试验**：区分模拟文件故障、进程强制结束、系统断电、设备断连；每类证据独立。
7. **生成机器可读验收矩阵**：每行有要求、Owner、代码文件、测试文件、运行 ID、Actual、未完成、负责人/版本、下一步。
8. **更新文档并中文提交**：仅在用户明确授权代码/仓库变更的开发阶段执行；直接 master、不分支不 PR，提交前后都比较 HEAD，不覆盖并行开发。

### 20.3 判断“公共配置体系完成”的两个层级

**基础配置底座验收**：G0—G5 和必要的 G7 通过；文档和测试记录与 HEAD 一致；不拿完整 UI 或实机作为基础机制阶段的强行前置，但未完成项必须可见。  
**V2.4 全套配置产品验收**：除了上述要求，G6 四层 UI、三种使用形态、专业扩展、操作流程和配套真实用例全部通过。  
**工业现场验收**：需要专业 Runtime、设备 ACK、安全互锁、性能及长期现场测试，另行单列，不因 V2.4 产品验收而自动宣称完成。

### 20.4 完成标准（Definition of Done）

- 功能行为在仓库中有唯一 Owner，可用公开 API 调用；
- 正常、异常、边界、重启、权限、订阅等至少有明确测试；
- 运行记录含真实 SHA、测试命令、运行环境和可定位的日志/报告；
- 不破坏 .NET Framework 4.8、传统 csproj、Windows x64 构建；
- 开发者代码足够简单，复杂能力无二次机制；
- 文档状态同步，不以类型数代替验收；
- 不宣称未验证的 WPF/设备/多进程功能已完成。

---
# 附录 A：Foundation / Mechanism 类型与公开 API 索引

> **范围**：基于当前 HEAD 中全部 11 个 Foundation Configuration 源文件和 15 个 Mechanism Configuration 源文件审阅的主要公开类/接口/枚举。列出权威 Owner、典型职责和最重要入口；若方法签名变动，必须以指定提交源码为准重生成索引。这里不为“完整”填充不存在的类。

## A.1 Foundation.Data.Configuration 类型索引

| 文件（位于 `Foundation/Data/Configuration/`） | 已有公开类型/关键成员 | 作用 |
|---|---|---|
| `FCMConfigContracts.cs` | `FCMConfigApplyPolicy`、`FCMConfigChangeStage`、`FCMConfigIssueSeverity` | 生效策略、变化阶段、问题级别 |
| 同上 | `FCMConfigSectionAttribute`、`FCMConfigItemAttribute`、`FCMConfigDependsOnAttribute` | 声明 Section、字段元数据、依赖 |
| 同上 | `FCMConfigValidatorAttribute`、`FCMConfigCustomValidatorAttribute`、`FCMConfigConverterAttribute` | 专业校验/自定义函数/转换器元数据 |
| 同上 | `IFCMConfigValueConverter<T>`、`IFCMConfigValidator<T>`、`IFCMAsyncConfigValidator<T>` | 转换器、同步和异步校验协议 |
| 同上 | `FCMConfigIssue`、`FCMConfigValidationContext`、`FCMConfigValidationReport` | 校验路径、错误码及报告 |
| 同上 | `FCMConfigMemberDescriptor`、`FCMConfigSchema` | 强类型字段描述和 Schema |
| 同上 | `FCMConfigValueInfo<T>`、`FCMConfigChange<T>` | 多版本读取诊断与变化事件 |
| `FCMConfigArchitecture.cs` | `IFCMConfigView<T>`、`FCMConfigIdentity` | 只读视图合同、唯一身份 |
| 同上 | `FCMConfigRevision`、`FCMConfigRevisionVector` | 版本和组合版本指纹 |
| 同上 | `FCMConfigValuePresence`、`FCMConfigValueState<T>` | 五态值及来源 |
| 同上 | `FCMConfigFieldChange`、`FCMConfigChangeSet` | 原始字段旧/新变化 |
| 同上 | `FCMConfigDependencyGraph` | 注册依赖、拓扑顺序、受影响字段 |
| `FCMConfigSchemaBuilder.cs` | `FCMConfigSchemaBuilder`、`FCMConfigValidationEngine` | 类型反射、描述、同步/异步验证 |
| `FCMConfigCatalog.cs` | `FCMConfigFieldInfo`、`FCMConfigCatalog` | 可视化字段列表、字段搜索、文档及脱敏 |
| `FCMConfigSecurity.cs` | `FCMConfigAccessAction`、`FCMConfigPrincipal`、`FCMConfigAccessRequest`、`FCMConfigAccessDecision` | 动作/身份/权限请求与决策 |
| 同上 | `IFCMConfigAccessPolicy`、`FCMConfigRolePolicy`、`FCMConfigSensitiveAttribute`、`FCMConfigSensitivity` | 默认拒绝的角色策略及嵌套敏感判定 |
| `FCMConfigTypeCatalog.cs` | `FCMConfigTypeKind`、`FCMConfigTypeIssue`、`FCMConfigTypeContract`、`FCMConfigTypeCatalog` | CLR 类型能力与可往返检查 |
| `FCMConfigReference.cs` | `FCMConfigReference`、`IFCMConfigResourceReferenceProvider` | 跨配置字段引用、资源存在性接口 |
| `FCMConfigReferences.cs` | `FCMConfigSecretRef`、`FCMConfigSecretRefCodec`、`FCMConfigDataHandle`、`FCMConfigResourceRef` | 密钥引用、体数据句柄、资源标识 |
| `FCMConfigPathDiffUtil.cs` | `FCMConfigPathUtil`、`FCMConfigDiffUtil` | 强类型表达式字段路径和差异 |
| 同上 | `FCMConfigCodecCapabilities`、`IFCMConfigDocumentCodec`、`FCMOrderedJsonCodec` | 文档能力、JSON Codec 端口 |
| `FCMOrderedJsonDocument.cs` | `FCMOrderedJsonDocument` | 保序 JSON Patch |
| `FCMJsonValueCodec.cs` | `FCMJsonValueCodec` | 强类型值 JSON 往返/复制 |

**注意**：上表不表示 Foundation 中所有 public 方法均已逐个写出完全签名；文档正式发布前可由 Roslyn/反射导出 public API 清单作为固定附属产物。该导出工具本身不需要建立新项目。

## A.2 Mechanism.Data.Configuration 类型索引

| 文件（位于 `Mechanism/Data/Configuration/`） | 已有公开/核心类型 | 作用 |
|---|---|---|
| `MHSConfigService.cs` | `MHSConfigService`、`MVBConfigScopeResolver` | 服务宿主、Scope、注册与 Open；`OpenAtomicGroup` |
| `MHSConfigSection.cs` | `MHSConfigSection<T>`、`MHSConfigEdit<T>`、`MHSConfigSnapshot<T>` | 当前/历史读、工作副本、保存、生效、任务快照 |
| 同上 | `MHSConfigPreview`、`MHSConfigCommitResult` | 预览和提交结果；Success、ErrorCode、Revision 等 |
| `MHSConfigStorage.cs` | `MHSConfigStorage`（internal） | 本地原子文件、修订历史、有效记录、崩溃恢复 |
| `MHSConfigApply.cs` | `MHSConfigApplyReview<T>`、`MHSConfigApplyDecision`、`MHSConfigApplyResult` | 生效前审查、允许/拒绝、原因与状态 |
| `MHSConfigAudit.cs` | `MHSConfigCommittedRecord`、`MHSConfigEffectiveRecord`、`MHSConfigAuditEntry` | 提交/生效持久审计与游标 |
| `MHSConfigCommitFeed.cs` | `MHSConfigCommitFeed<T>` | 有界批量提交订阅与游标恢复 |
| `MHSConfigAuthorizedSession.cs` | `MHSConfigAuthorizedSession`、`MHSConfigAuthorizedSection<T>`、`MHSConfigAuthorizedEdit<T>` | 受权限裁剪的读取、编辑、生效和订阅 |
| `MHSConfigAtomicGroup.cs` | `MHSConfigAtomicGroup`、`MHSConfigAtomicEdit`、`MHSConfigGroupSnapshot` | 多模型单物理原子事务、成员读取与任务快照 |
| 同上 | `MHSConfigGroupDocument`、`MHSConfigGroupEntry` | 聚合文档与成员 SchemaVersion/Json |
| `MHSConfigReferenceRegistry.cs` | `MHSConfigReferenceRegistry`、`MHSConfigReferenceValue` | 跨配置/资源引用解析、依赖、问题报告 |
| `MHSConfigEditorModel.cs` | `MHSConfigEditorModel<T>`、`MHSConfigEditorField` | 无 UI 草稿/字段/差异/撤销/提交模型 |
| `MHSConfigStatus.cs` | `MHSConfigStatus`、`MVBConfigLayerHealth`、`MVBConfigCompositeStatus<T>` | 保存、有效、审计、来源层健康 |
| `MHSConfigPackage.cs` | `MHSConfigPackage` | 离线配置包、Schema、Hash、导入验证 |
| `MNMConfigChangePublisher.cs` | `MNMConfigChangePublisher<T>` | 本地变更发布、序列及订阅隔离 |
| `MVBConfigLayeredProfile.cs` | `MVBConfigLayerSpec`、`MVBConfigLayerOverrides`、`MVBConfigLayerOrigin` | 层级声明、显式字段覆盖与来源 |
| 同上 | `MVBConfigLayeredProfile<T>`、`MVBConfigResolved<T>`、`MVBConfigTaskSnapshot<T>`、`MVBConfigLayerValueInfo<T>`、`MVBConfigLayerChange<T>` | 独立层组合、来源链、版本向量和任务固定 |
| `MVBConfigAtomicLayerProfile.cs` | `MVBConfigAtomicLayerProfile<T>`、`MVBConfigAtomicLayerEdit<T>` | 聚合存储的原子层级档案、Set/Remove/Resolve/Apply |

**UI 不是第三个配置底座**：`Mechanism/Presentation.Wpf/MPWConfigEditorDialogV14.cs` 的 `MPWConfigEditorDialog` 为已有 WPF 适配器，允许改进但不得承担保存、事务或来源解析的新真源。

## A.3 关键不变方法的真实归属

- `Service.Open<T>(identity)` / `ForMachine/ForProject/ForScope` → `MHSConfigService.cs`。
- `Get/Value/Section/PrepareValue/Explain/ExplainState/GetStatus` → `MHSConfigSection.cs`。
- `BeginEdit/UpdateAsync/SetValueAsync/PreviewAsync/CommitAsync` → Section/Edit，同一写入真源。
- `Watch/WatchSaved/Subscribe/SubscribeAll` → Section + `MNMConfigChangePublisher.cs`。
- `CaptureForTask/AtSavedVersion/RestoreSavedVersionAsync` → Section/快照/存储。
- `ReadCommittedAfter/ReadEffectiveAfter/SubscribeCommittedFrom` → Section + Storage/Feed。
- `Register<T>/Seal/UpdateAsync/TryApplyPendingAtBoundary` → `MHSConfigAtomicGroup.cs`。
- `ResolveEffective/ResolveSaved/SetOverrideAsync/RemoveOverrideAsync` → 两类 LayerProfile 根据物理语义选择，不能混为一种跨文件原子操作。
- `Modify/Undo/Redo/DescribeFields/AffectedBy/CommitAsync` → `MHSConfigEditorModel.cs`。
- `Authorize(...).Open<T>()` → `MHSConfigAuthorizedSession.cs`；安全关键调用不暴露原始 Section。

---

# 附录 B：四类案例的测试工程接入与完整示例

## B.1 如何执行真实已有测试（优先路径）

**推荐首先执行仓内现有 `ConfigDeveloperExperienceTests.cs`**，其六组方法恰好覆盖普通/X-Ray/CT/原子配方/权限/性能场景，避免为文档重新编写不一致测试。独立回归由 `Tests/ConfigFoundationSmoke/Program.cs` 调用：

```powershell
$msbuild = (Get-Command msbuild.exe -ErrorAction Stop).Source
& $msbuild .\Tests\ConfigFoundationSmoke\CxVisual.Tests.ConfigFoundationSmoke.csproj `
  /m:1 /t:Rebuild /p:Configuration=Release /p:Platform=x64 /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw '配置独立编译失败' }
& .\Output\Release\x64\CxVisual.Tests.ConfigFoundationSmoke.exe
if ($LASTEXITCODE -ne 0) { throw '配置自动验收失败' }

& $msbuild .\CxVisual.Unified.sln /m:1 /t:Rebuild `
  /p:Configuration=Release /p:Platform=x64 /nologo /v:minimal
if ($LASTEXITCODE -ne 0) { throw '全解决方案编译失败' }
& .\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe
if ($LASTEXITCODE -ne 0) { throw '架构回归失败' }
```

在非 Windows/.NET Framework 4.8 工具链下，不应声称该命令实际运行过。输出目录、引用依赖、运行时日志应按当前仓库 `Build/` 约定核查。

## B.2 可粘贴到现有测试程序集的参考代码

下列是**参考测试代码**，用于说明怎样进一步验证四种模式，使用现有正式 API；它尚未写进仓库、尚未在本轮编译，不作为新测试通过证据。可以加入现有 ArchitectureSmoke 测试工程编译，前提是该工程已引用 Foundation.Data/Mechanism.Data。

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CxVisual.Foundation.Data.Configuration;
using CxVisual.Mechanism.Data.Configuration;

[FCMConfigSection("Docs.V24.Scan")]
public sealed class V24ScanOptions
{
    [FCMConfigItem("曝光", ApplyPolicy = FCMConfigApplyPolicy.NextTask)]
    public int ExposureMs { get; set; } = 80;
    public Dictionary<string, V24DetectorOptions> Detectors { get; set; }
        = new Dictionary<string, V24DetectorOptions>();
}
public sealed class V24DetectorOptions
{
    public int Gain { get; set; } = 1;
}
[FCMConfigSection("Docs.V24.Recon")]
public sealed class V24ReconOptions
{
    public int Iterations { get; set; } = 10;
}

public static class V24DeveloperAcceptance
{
    private static void Need(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    public static async Task Run(string root)
    {
        MHSConfigService service = new MHSConfigService(root);
        CancellationToken token = CancellationToken.None;

        // D01 普通配置：默认、提交、生效、来源、订阅。
        MHSConfigSection<V24ScanOptions> ordinary = service.ForMachine("Ordinary")
            .Open<V24ScanOptions>();
        Need(ordinary.ExplainState(x => x.ExposureMs).Presence ==
            FCMConfigValuePresence.Defaulted, "D01 default source");
        int notified = 0;
        using (IDisposable watch = ordinary.Watch(x => x.ExposureMs,
            change => { if (change.NewValue == 120) notified++; }))
        {
            MHSConfigCommitResult saved = await ordinary.UpdateAsync(
                x => x.ExposureMs = 120, "D01 变更", token);
            Need(saved.Success && ordinary.HasPendingApply && notified == 0,
                "D01 saved must not mean effective");
            Need(ordinary.TryApplyPendingAtBoundary(
                FCMConfigApplyPolicy.NextTask, null).Applied, "D01 apply");
            Need(ordinary.Value(x => x.ExposureMs) == 120 && notified == 1,
                "D01 effective read and event");
        }

        // D02 X-Ray 动态探测器：一个事务多个 Dictionary Key。
        MHSConfigSection<V24ScanOptions> xray = service.ForMachine("XRay")
            .Open<V24ScanOptions>();
        MHSConfigCommitResult xraySaved = await xray.UpdateAsync(x =>
        {
            x.Detectors["D-01"] = new V24DetectorOptions { Gain = 2 };
            x.Detectors["D-02"] = new V24DetectorOptions { Gain = 3 };
        }, "D02 探测器参数", token);
        Need(xraySaved.Success, "D02 saved");
        Need(xray.TryApplyPendingAtBoundary(
            FCMConfigApplyPolicy.NextTask, null).Applied, "D02 apply");
        Func<int> gainD2 = xray.PrepareValue(x => x.Detectors["D-02"].Gain);
        Need(gainD2() == 3, "D02 gain");
        Dictionary<string, V24DetectorOptions> copy = xray.Value(x => x.Detectors);
        copy["D-02"].Gain = 99;
        Need(gainD2() == 3, "D02 detached dictionary");

        // D03 CT 原子多模型：同一物理版本与任务固定。
        MHSConfigAtomicGroup group = service.OpenAtomicGroup("CT-Example", g =>
        {
            g.Register<V24ScanOptions>("Scan");
            g.Register<V24ReconOptions>("Recon");
        });
        MHSConfigCommitResult joint = await group.UpdateAsync(edit =>
        {
            V24ScanOptions scan = edit.Get<V24ScanOptions>("Scan");
            scan.ExposureMs = 150;
            edit.Set("Scan", scan);
            V24ReconOptions recon = edit.Get<V24ReconOptions>("Recon");
            recon.Iterations = 20;
            edit.Set("Recon", recon);
        }, "D03 CT 联合参数", token);
        Need(joint.Success, "D03 saved");
        Need(group.TryApplyPendingAtBoundary(
            FCMConfigApplyPolicy.NextTask, null).Applied, "D03 apply");
        using (MHSConfigGroupSnapshot pin = group.CaptureForTask("CT-001"))
        {
            Need(pin.Get<V24ScanOptions>("Scan").ExposureMs == 150 &&
                pin.Get<V24ReconOptions>("Recon").Iterations == 20,
                "D03 pinned scan and recon");
            MHSConfigCommitResult next = await group.UpdateAsync(edit =>
            {
                V24ReconOptions recon = edit.Get<V24ReconOptions>("Recon");
                recon.Iterations = 30;
                edit.Set("Recon", recon);
            }, "D03 new recon", token);
            Need(next.Success, "D03 future commit");
            Need(group.TryApplyPendingAtBoundary(
                FCMConfigApplyPolicy.NextTask, null).Applied, "D03 future apply");
            Need(pin.Get<V24ReconOptions>("Recon").Iterations == 20,
                "D03 old task remains pinned");
        }

        // D04 配方原子层级：单聚合事务与来源覆盖。
        MVBConfigLayerSpec[] layers =
        {
            new MVBConfigLayerSpec("Template", "template", "Base", 10),
            new MVBConfigLayerSpec("Machine", "machine", "Line-A", 30),
            new MVBConfigLayerSpec("Product", "product", "Cell-A", 40)
        };
        MVBConfigAtomicLayerProfile<V24ScanOptions> profile =
            new MVBConfigAtomicLayerProfile<V24ScanOptions>(service, "Recipe-B", layers);
        MHSConfigCommitResult recipeSaved = await profile.UpdateAsync(edit =>
        {
            edit.Set("Template", x => x.ExposureMs, 80);
            edit.Set("Machine", x => x.ExposureMs, 120);
            edit.Set("Product", x => x.ExposureMs, 150);
        }, "D04 原子配方", token);
        Need(recipeSaved.Success, "D04 saved");
        Need(profile.TryApplyPendingAtBoundary(
            FCMConfigApplyPolicy.NextTask, null).Applied, "D04 apply");
        Need(profile.ResolveEffective().Value(x => x.ExposureMs) == 150 &&
            profile.ResolveEffective().Explain(x => x.ExposureMs).EffectiveSource == "Product",
            "D04 source priority");
        MHSConfigCommitResult removed = await profile.UpdateAsync(
            edit => edit.Remove("Product", x => x.ExposureMs),
            "D04 取消产品覆盖", token);
        Need(removed.Success, "D04 remove saved");
        Need(profile.TryApplyPendingAtBoundary(
            FCMConfigApplyPolicy.NextTask, null).Applied, "D04 remove apply");
        Need(profile.ResolveEffective().Value(x => x.ExposureMs) == 120,
            "D04 fallback to machine");

        Console.WriteLine("V2.4 四类参考场景完成");
    }
}
```

**代码审查提示**：本示例有意不模拟安全互锁或网络发布；实际生产应由宿主配置 Guard 和权限。`D04` 层定义持久冻结、`D02` 动态字典自动 UI 仍是目标，不能拿该参考代码判定这两项已交付。

---
# 附录 C：12 类应用场景的反向验收（S01—S12）

> 这些案例用于从业务反向检查统一公共底座是否真的可用；**软件模拟通过不等于已完成实际设备/网络验收**。案例编号和基本方向按 V2.2 保持。

| 场景 | 最小业务操作链 | 底座应该提供 | 当前软件层证据 | 保留缺口 |
|---|---|---|---|---|
| S01 算法配置 | 定义算法参数→验证→保存→运行订阅→新任务生效 | POCO/规则/事务/Watch/任务快照 | D01、源码/CI | 实际算法/模型生命周期接入 |
| S02 旧机升级 | 读旧版本→识别 Schema→迁移预览→提交新修订→仍可查原版 | JSON 解析/历史/保序/版本检查 | 历史与保序已存在 | 旧格式全链式迁移【暂缓】 |
| S03 X-Ray 多探测器 | 配置多个 FPD→动态 Key→多字段联合修改→保存/切换 | 深层 Dictionary、验证、任务快照、批量修改 | D02、丰富类型回归 | 设备 SDK ACK/实机性能【暂缓】 |
| S04 CT 长任务 | 扫描与重建模型一起设定→整组生效→扫描固定旧版本→新任务采用新版 | 多模型原子、Revision、Snapshot | D03、原子组测试 | 真实长扫描/重建资源吞吐【待专业验收】 |
| S05 运控联锁 | 调整轴/速度参数→Guard 判定→拒绝或应用 | 权限/Guard/生效不污染 | 纯软件 Guard 测试 | 真实 PLC/控制卡安全互锁【暂缓】 |
| S06 SCADA/IoT | 多设备/点表配置→身份隔离→审计游标读取 | Scope、引用、日志和历史重放 | Scope/事件测试 | 跨进程网络实时同步【暂缓】 |
| S07 HMI 工程 | 定义视图配置→通用表单编辑→查看来源→保存→生效/回退 | Catalog/EditorModel、四层 WPF 编辑 | EditorModel 存在 | 完整 UI 尚未完成【本 V2.4 目标】 |
| S08 产品配方 | 模板→机台→产品→临时覆盖→修改若干层→原子切换→回退来源 | 自定义层、来源、原子层档案、冻结契约 | D04、原子层测试 | **层级冻结/定义变更保护尚未完成** |
| S09 插件升级 | 发现 Section→拒绝重复→卸载保护→新类型升级 | Assembly 注册/TypeCatalog | 注册/卸载测试 | 非可信 DLL 沙箱、完整 Schema 升级【后续】 |
| S10 密钥与大资源 | 配置秘钥引用/CT 数据句柄→检查资源存在→授权读取 | SecretRef/ResourceRef/DataHandle、Provider、权限 | 相关类型与软件资源测试 | Vault、跨进程数据句柄、真实资源持久性【后续】 |
| S11 外部手工修改 | 开始编辑→外部文件/并发修改→CAS 拒绝→保留原状态 | Saved 冲突/审计完整性/异常诊断 | CAS 与篡改检查源码/回归 | 三方自动合并与所有外部格式【暂缓】 |
| S12 跨机配置发布 | 包校验→多目标 Stage→授权发布→ACK→回退 | 版本、包、发布清单、目标回执 | 仅本地包/软件生效基础 | 跨机 ReleaseManifest/回滚【暂缓】 |

### C.1 共用的验收证据表模板

每个场景验收时至少记录：`CaseId`、`ConfigType/SectionId`、`Scope/ProfileId`、`Saved/Effective/PinnedRevision`、执行动作、前置权限、输入参数与文件、预期响应、实际响应、错误码/日志、是否设备模拟、CPU/磁盘/软件环境、测试用例/运行 URL、Commit SHA、结论、残余风险。缺任何关键字段时只能记“待补证据”，不能仅凭标题或截图通过。

### C.2 从五大历史工业系统吸收而不复制管理器

工业视觉、CT 重建、ICS/SCADA、IoT、运控/离线检测五类既有系统提供有价值的现场需求与数据/设备约束，但**不是因为历史程序已经使用多年，新配置底座就自动通过了它们的集成验收**。各专业要对接配置时应适配其原有驱动、校准和运行安全，不将老的 ConfigManager 直接移入 Foundation 或 Mechanism 形成第二套体系。

---

# 附录 D：故障注入、并发、性能与 UI 测试矩阵

## D.1 本阶段必须执行的功能及异常矩阵

| ID | 输入/攻击方式 | 正确输出与不变量 | 对应 Gate |
|---|---|---|---|
| T01 | 首次 Open 合法配置 | 默认值/Schema/Scope 正确 | G1 |
| T02 | 同一 Section 不同 Machine | 身份隔离、不串扰 | G1 |
| T03 | 名称含特殊字符并组合身份 | CanonicalKey 不碰撞 | G1 |
| T04 | Missing 字典 Key 和显式 null | 不伪装成 default(T)，五态可解释 | G1 |
| T05 | 仅修改 Note，查询另一个默认字段 | 未改字段仍是 Defaulted | G1 |
| T06 | ulong/decimal/DateTimeOffset/Nullable/复杂集合往返 | 无静默数值损失 | G1 |
| T07 | 不支持的 CLR Type、循环引用等 | TypeCatalog/校验拒绝，旧状态不变 | G1/G2 |
| T08 | Range/Regex/Required 违规 | 带路径 Issue，Saved/Effective 不变 | G2 |
| T09 | 异步验证器异常、改变候选、取消 | 显式失败/取消，无部分提交 | G2 |
| T10 | 跨字段 DependsOn 环和未知 Path | 定义/注册被明确拒绝 | G2 |
| T11 | 两个草稿基于同一 SavedVersion 提交 | 一次成功、过时草稿版本冲突 | G2 |
| T12 | 聚合多模型一成员非法 | 整组提交拒绝，无成员半更新 | G2 |
| T13 | 原子层级一次修改多层 | 单修订/单提交/一次有效切换 | G2/G4 |
| T14 | NextTask 保存但尚未 Apply | `WatchSaved` 有事件，`Watch` 无事件 | G3 |
| T15 | Guard 拒绝 Immediate 和 NextTask | Effective 不变，无有效成功事件 | G3 |
| T16 | Guard 审查期间另一提交更新版本 | Apply 失败或按正确最新版本复核，不误用旧候选 | G3 |
| T17 | CT 任务固定后又保存/生效新版 | Pinned 快照仍为旧整组版本 | G3 |
| T18 | Snapshot.Dispose 后继续 getter | 明确生命周期异常 | G3 |
| T19 | 层优先级/来源链、删除覆盖 | 字段来源及最终值一致 | G4 |
| T20 | 重启改变层级 Priority/Name/Scope/类型 | 阻止静默漂移，安全预览升级 | G4，**目标测试** |
| T21 | 只读用户打开混合敏感 Section，读普通字段 | 允许普通字段，不泄露敏感父项 | G4 |
| T22 | 无 ReadSensitive 的 Read/GetStatus/审计导出 | 无敏感值/错误文本泄露 | G4 |
| T23 | Prepared getter 创建后撤销读取权限 | 下次 getter 调用拒绝 | G4 |
| T24 | Watch 已注册后撤销权限 | 后续事件不能继续泄露 | G4 |
| T25 | 保存前/替换中/审计后故障注入 | 回滚/恢复到可证实的一致状态 | G7 |
| T26 | 生效日志故障/篡改 | 无伪生效/伪事件，错误可读 | G7 |
| T27 | 老版本恢复操作 | 创建新的 SavedRevision，原历史仍可查 | G2/G7 |
| T28 | 订阅者异常/重入/慢处理 | 不使成功事务回滚，其他订阅者可继续 | G3/G7 |
| T29 | 持久游标消费失败→Resume→重启 | 无漏读、受控重复、幂等策略可执行 | G3/G7 |
| T30 | 高频读取、并发写入和 Capture | 无半更新、分位耗时/GC 有报告 | G7 |

## D.2 自动配置 UI 测试矩阵

| ID | UI 交互场景 | 验收条件 |
|---|---|---|
| U01 | 任意合法 POCO 自动加载界面 | 不写专业 XAML 也可看到全部公共字段及标题/单位 |
| U02 | 6 级嵌套对象、字典动态键 | 可浏览、增删改并在具体元素处显示错误 |
| U03 | List 插入/重排、Set 去重、Nullable 空值 | 操作正确、不误改集合语义和来源 |
| U04 | 数值范围、Regex、异步验证 | 校验未通过不能 Save；取消/错误状态可见 |
| U05 | Undo/Redo、修改/重新修改 | 草稿历史正确，已保存版本不随 Undo 改动 |
| U06 | 保存后状态提示 | 显示 Saved 新版，Effective 仍旧版；Watch 语义一致 |
| U07 | Guard 拒绝 Apply | 显示理由、保留待生效，设备未被认为已使用新版 |
| U08 | 历史版本对比/恢复 | 需明确确认，恢复作为新的 Saved 修订 |
| U09 | 敏感字段、审计、复制、Tooltip、JSON 视图 | 受限用户无任一路径可见明文 |
| U10 | 层级优先级、来源链、原子/非原子模式 | 用户看得懂最终来源及提交边界 |
| U11 | Studio/嵌入专业 WPF/独立窗口 | 同一机制可复用；Close 正确 Dispose |
| U12 | WPF Dispatcher、并发通知和窗口关闭 | 不发生跨线程异常/悬空回调/泄漏 |

## D.3 性能与耐久测试结果记录模板

```text
BenchmarkId:
CommitSHA:
OS / Windows build:
.NET Framework version / x64:
CPU / memory / disk type:
Data shape (field count, JSON bytes, depth, dynamic keys):
Warmup / repetitions / concurrency:
Throughput / p50 / p95 / p99 / max / GC / allocated bytes:
Correctness assertions / errors:
Baseline commit and regression ratio:
Test command and raw log URL:
Decision: PASS / FAIL / INCONCLUSIVE / NOT_RUN
```

`NOT_RUN` 不能填 0ms、0 Error 冒充已跑。若无现场周期预算，不先发明“必须 1us”“必须 100 万次/秒”等未经需求支持的性能阈值；先实测，确定目标机台最坏情况后再固化回归门槛。

## D.4 暂缓专项单列，不能在 G0—G7 中偷偷宣传完成

- X01 多独立进程同时提交和生效（含写入锁、有效标记 CAS、掉电恢复）；
- X02 跨进程或网络订阅的实时事件、断线重连与高可靠重放；
- X03 多机模板发布、目标 ACK、签名/权限/灰度/回滚；
- X04 完整 YAML/INI 注释保序与跨 Schema 版本自动迁移；
- X05 真实 X-Ray 源、探测器、PLC/运动控制卡联锁及 Readback；
- X06 商用品安全评审、长周期现场可靠性与硬实时操作系统指标。

---

# 附录 E：版本沿革、未决问题与 Codex 执行约束

## E.1 版本与来源

| 基线 | 贡献 | V2.4 如何保留 |
|---|---|---|
| V1.3《双项目架构_完整API与案例审查》 | 六种角色、27 组职责、24 项需求、12 案例、公共底座集中 | 通过 V2.0/V2.2/V2.3 引述恢复，原文缺失需补后核对 |
| V2.0《统一配置体系_整体架构收口》 | 唯一真源、关键不变量、配置定义/实例/事件/快照 | 第一、二、五篇不变量与对象边界 |
| V2.2《双项目整体收口与验收》 | 真实 API、27/24/12 对照、单持久单元原子性与缺口 | 第六篇和附录 A/C/D 的证据框架 |
| V2.3《开发者体验审核与API收敛》 | `UpdateAsync/Watch/WatchSaved`、安全修复、仍待简化 API | 第 10、11 章调用，第 09 章后续目标 |
| V2.4（本文件） | 自定义层级与冻结、自动配置 UI 四层能力/三形态、配置与运行数据分离、跨进程暂缓、操作闭环 | 用户新增定案，进入 Gate G4/G6，并调整长期优先级 |

## E.2 V2.4 新确认的架构结论

**D-01 双项目唯一 Owner**：只在 `CxVisual.Foundation.Data/Configuration` 与 `CxVisual.Mechanism.Data/Configuration` 实现公共配置底座。  
**D-02 层级自定义**：五层为典型方案；专业可组合不同数量和语义的来源，普通配置无需层级。  
**D-03 定义冻结**：一旦持久化，层级合同、优先级、身份、成员 Schema 不许静默改变。  
**D-04 UI 四层**：基础自动编辑、专业级管理、高级层级/多模型组织、专业控件扩展都纳入目标。  
**D-05 UI 三态装配**：Studio、可嵌入 WPF、独立窗口，统一 Edit/Save/Apply 真源。  
**D-06 运行时数据明确分离**：节点栈、全局堆、运行时输入输出数据独立于配置。  
**D-07 跨进程管理暂缓**：不在当前开发阶段实现跨进程生效、实时通知和跨机发布，但不得误报支持。  
**D-08 端到端操作链定案**：打开→编辑→校验→差异预览→保存→待生效→申请生效→结果→历史恢复。  
**D-09 少抽象、少重复**：只改造现有类、机制和 WPF 适配器，新增 API 必须缩短真实开发流程。  
**D-10 证据优先**：最终验收由源码、测试、CI 和必要的真实 UI/设备证据共同决定。

## E.3 当前可见问题优先级（以用户本轮调整为准）

| 优先级 | 任务组 | 目标落点 | 不得引入 |
|---|---|---|---|
| P0 | 层级定义持久冻结、漂移检测与受控升级预览 | `MVBConfigAtomicLayerProfile`、现有聚合文档/存储、回归测试 | 第二套 Profile 存储真源 |
| P0 | 敏感值全链路安全及权限复核 | Foundation Security、AuthorizedSession、Catalog、EditorModel、事件/导出 | 专用 Security 配置项目 |
| P0 | 本地保存/生效/错误语义一致性与异常恢复 | `MHSConfigSection/Storage/Apply`、现有测试 | Publisher/RuntimeConfig 平行框架 |
| P1 | Explain/Status 诊断收敛、强类型成员与调用简化 | `MHSConfigSection`、`MHSConfigAtomicGroup`、Catalog | 新 Facade 真源 |
| P1 | Schema 及动态集合编辑模型、规则执行健壮性 | Foundation schema/validation + Mechanism editor | JSON 文本编辑器伪装全类型通用编辑 |
| P1 | 第一/二层通用 WPF 配置 UI 与可嵌入调用 | 现有 `Mechanism.Presentation.Wpf`/Studio | ConfigStudio 单独项目 |
| P2 | 第三/四层 UI、专业扩展示例 | 同一 WPF 组件 + 现有编辑协议 | 专业再造统一配置保存机制 |
| P2 | 高强度基准、内存与崩溃恢复 | 现有配置测试 + CI 脚本 | 没有日志的性能口头结论 |
| H | 跨进程、跨机、完整旧格式迁移、真设备 Readback | 未来独立验收阶段 | 当前误标已完成 |

### E.4 需后续与项目负责人确认的少量工程细节

1. 自定义 Profile 合同的持久化究竟嵌入现有原子聚合文档的哪一字段，如何与老配置兼容；需先列变更前后 JSON 样例与迁移/拒绝规则。
2. “来源层锁定”在各专业是否只需只读层标记、禁止覆盖指定字段，还是需要角色限制；在未定案前不要猜测实现全部策略。
3. 动态集合编辑器“支持哪些 Key 类型/自定义比较器、Set 元素是否可编辑、无序集合显示顺序”需按 `TypeCatalog` 真正可往返范围冻结合同。
4. 完整 UI 的首个实际验收宿主：建议优先使用已有 Studio 示例与 `MPWConfigEditorDialog`，再验证一款独立 WPF 宿主；不要创建新的项目仅用于截图。
5. 性能阈值、机台时序预算、历史保留时间及容量上限，需要真实目标工作站/CT任务负载数据，不在架构稿中预造数值。
6. 若找回 V1.3 原文，逐条检查本文恢复的 27 组、24 项、12 场景名称/未决项，明确列出差异后更新，不静默覆盖历史来源。

## E.5 Codex 实施规则：每次集中完成一个相关主题

1. **进仓**：`git fetch`、读取最新 `master` HEAD、`README.md`、`AGENTS.md`、`DOCS/00`、本 V2.4、现有配置测试、相关 csproj/工作流；检查其他任务已提交变化。
2. **先找事实**：按 Owner 对照 API 与测试，列出本主题已存在、缺少、重复、语义冲突；不要沿用前一聊天的假设。V1.3 缺失部分必须声明，不能杜撰签名。
3. **约束变更**：只改与目标直接相关的两个配置目录、必要现有 UI/测试文件；优先复用和整合现有类型；不得新增平行系统、空壳类、伪兼容接口或额外 `.csproj`。
4. **先写失败测试**：一个完整任务至少有正常、异常、边界、重启/历史、权限/版本冲突；对层级冻结必须覆盖定义变化；对 UI 必须覆盖实际窗口行为。
5. **代码实现**：保持传统 .NET Framework 4.8、x64 和构建配置兼容；不得加入未经审查的新第三方 DLL；不可破坏 Vision/Runtime 其他正在开发的业务代码。
6. **集中集成**：确保 Source→Schema→Edit→Validate→Commit→Apply→Watch→Status→UI 全路径语义一致；失败不得污染已有 Saved/Effective。
7. **真实验证**：独立配置 CI、完整解决方案 CI 和涉及的 WPF/异常用例全跑；无 Windows 环境只能记“未验证”，不能自己给自己打勾。
8. **直接 master、中文少量提交**：仅在用户再次授权仓库改动时生效。提交前刷新 master 并比较改动，防止覆盖并行任务；不建日常分支、不创建 PR；每次集中改造一次主题提交，失败按仓库规则修复，不篡改已有共享提交历史。
9. **同步矩阵**：每项更新 Owner、API、源码路径、测试路径、CI URL、结论、未验证部分、风险与下一步；旧文档不因新标题而自动删除，避免断开历史证据。
10. **不宣称超出范围**：代码+自动测试通过是软件层证据；完整 WPF UI、实机安全、多进程/多机发布分别有独立证据门禁。

### E.6 下一次代码开发的建议一次性交付包（非本轮执行）

**包 A：配置可靠性与来源定义收口**：层级契约冻结/迁移拒绝 + Saved/Effective 状态一致性 + 敏感值全链路审查 + 直接相关的故障测试。  
**包 B：开发者 API 与动态编辑模型收口**：Explain/Status、强类型组成员/简化 Profile 入口、Dictionary/List/Set 编辑与增量规则，四类示例回归。  
**包 C：通用配置 UI 完整产品闭环**：第一到第四层功能，Studio/嵌入/弹窗三模式，权限/历史/来源/生效状态机和真实 Windows WPF 验收。  
**包 D：长期负载与容错验收**：不同数据规模、并发读写、慢订阅、重启/损坏、长任务性能，形成稳定可比较报告。  
**包 H：跨进程和跨机/实机专项**：按用户当前决议暂缓，后续明确要求时再单独实施。

---

## 最终核对清单（V2.4 文档冻结前）

- [x] 统一配置与运行时数据的边界清晰；双项目单真源；不新增配置专用项目。
- [x] 六种角色的体验、三种配置模式、自定义层级、定义冻结的目标已写明。
- [x] 四层自动编辑界面、Studio/嵌入/弹窗及统一操作链已写明。
- [x] Saved/Effective、五态值、长任务固定、原子/非原子区别已写明。
- [x] 现有 API 与目标 API 有明确区分；复杂类型、权限、故障、性能有追踪。
- [x] 27 组职责、24 项需求、12 案例有独立索引；六篇二十章、五个附录齐全。
- [ ] V1.3 原始全文尚待获取并逐项核对；本文件已显式声明这一证据缺口。
- [ ] 本轮文档中的 B.2 参考程序未编译运行；必须纳入工程后实测。
- [ ] 新定义冻结/集合编辑/完整 UI 尚未代码交付；不可标记完成。
- [ ] 完整 UI 人工操作与真实设备安全验收尚未执行。

**V2.4 结论**：配置架构与开发者使用目标可以冻结并指导下一轮工作；源代码现状、Windows CI 现有证据和仍待实现的 UI/定义保护/故障范围必须分开陈述。没有要求再次进行 Github 提交前，本文件仅为可下载审查稿，不改变仓库任何既有事实。
