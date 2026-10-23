# CxVisual 统一功能模块体系——配置注解与 TestCenter 创建审查 V0.3

> 2026-10-23。交付范围：四种模块配置实例创建、自动编辑、保存与校验；不是正式 MEM 工程树登记或实机运行验收。

## 1. 现有配置体系审计

| 功能 | 现有真实 Owner/API | 结论 |
|---|---|---|
| POCO 注解 | FCMConfigSectionAttribute / FCMConfigItemAttribute | 已有 |
| Schema/类型与校验 | FCMConfigSchemaBuilder / FCMConfigTypeCatalog / FCMConfigValidationEngine | 已有 |
| 模型注册与实例 | MHSConfigService.RegisterAssembly / ForScope().Open<T>() | 已有 |
| 通用草稿与编辑 | MHSConfigEditorModel<T> | 已有 Undo/Redo/Preview/Commit |
| WPF 自动表单 | MPWConfigEditorControl<T> | 已有枚举、标量、嵌套 POCO、List/Map/Set |
| 通用资源引用选择器 | 当前 ChannelRef、ProtocolRef 等为普通字符串 | 缺动态候选与真实资源查找 UI |
| 参数条件显隐 | 当前 FCMConfigItem 支持 Category/Description/ApplyPolicy，但无统一 VisibleWhen | 缺跨字段显示策略 |
| 模块正式 MEM 工程资源 | 本轮只生成作用域配置 Section 和 MFM 描述 | 未实现正式工程登记 |
| 模块实际运行能力 | TCP/Modbus 还在 Porting，MRK 尚未直接装配模块 | 未完成 |

## 2. 新配置类型（不新增项目、不增加第二个配置管理器）

| 配置模型 | Owner | SectionId |
|---|---|---|
| MFMModuleIdentityConfig | Mechanism.Core.Modules | CxVisual.Module.Identity |
| MCNConnectorEditableConfig | Mechanism.Connectivity.Connectors | CxVisual.Module.Connector |
| PVCComputeEditableConfig | Professional.Vision.Modules | CxVisual.Module.Compute |
| PSMSimulationEditableConfig | Professional.Simulation.Modules | CxVisual.Module.Simulation |
| MLCServiceEditableConfig | Mechanism.Lifecycle.Modules | CxVisual.Module.Service |

各类继承 MFMModuleEditableConfig 的基本显示名称、启用和 MOC 能力 Id 定义。ModuleId 由 MHS Scope 唯一提供，同 Id 的 Kind 已登记后不允许切换种类；不新建 ModuleManager。

## 3. TestCenter 使用路径

专有测试 → 模块创建与配置 → 选择 Connector/Compute/Simulation/Service + ModuleId → 右侧创建/打开 → 中央自动生成 MPW 配置表单 → 预览/验证/保存/申请生效 → 右侧验证模块草稿。

TestCenter 本地配置存储为 LocalApplicationData/CxVisual/TestCenter/ModuleConfigurations，隔离于正式工程。该入口只创建可保存的配置实例，不声称已在 MEM 中建资源或触发设备 I/O。

Connector 支持 Channel、Protocol、Points 三类动态列表、枚举选择及同一跨成员 Validator；错误如重复 Id、未找到 Channel、Modbus TCP/RTU 不兼容、悬空点绑定和并发无效统一反馈 FCMConfigIssue。

## 4. 后续公共配置缺口

1. 在 Foundation.Data / Mechanism.Presentation.Wpf 增强可注册的 TypeId/ResourceRef 选择元数据与搜索下拉，避免为每种模块写一套选择器。
2. 通过 FCM DependsOn 引导条件显隐、参数互斥和按类型展示，使 TCP/UDP/Serial 配置不再拥挤；这属于现有通用编辑器增量增强。
3. 关联 MEM 正式模块资源和 MHS Section 身份，使 Studio 同一工程保存重开、撤销构建一致。
4. 将配置实例转换为对应的真实模块 Provider，并由 MOC/MRK 执行；该运行能力非本次交付。

## 5. 测试与状态

ArchitectureSmoke 新增 ModuleConfigAnnotationSmokeTests：五种 Section 的 Schema/类型往返、Connector 正反校验、MHS Commit→重开与配置身份保持。TestCenter WPF UIA 增加自动打开模块配置、验证动作及截图。

正式 Build/Smoke 是否通过，以本轮提交 SHA 对应 Windows CI 为准；注解/类创建不等于实机验收。
