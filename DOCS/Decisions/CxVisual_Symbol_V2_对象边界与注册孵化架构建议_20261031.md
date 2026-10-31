# ADR（建议审评）：CxVisual Symbol 体系 V2.0 对象边界、注册发现与孵化裁决

> 日期 2026-10-31；**Proposed（待负责人批准）**。不替代已有工程组织/MOC/Visual/运行 Owner 裁决；批准后再改为 Accepted 并更新 DOCS/00 及相应上位规格。总设计入口 [#0](../Architecture/CxVisual_Symbol_00_总体架构与完整需求_V2.0.md)。

## 01. 已有确认不再重决的事实
1. `MEMSymbol` 是共享定义，唯一 `RootGroup`；`MRCGroup` 只拥有视觉 Group/Element；实例经 `MRIResourceReference`，工程真源不另存 Graph。
2. Content 用来组织业务/逻辑/数据/协同，MOL/MOC/MRK/MDS 等保留既有职责；Symbol 不成为新执行引擎。
3. Type / Capability / Module 是不同身份，类型注册和能力只读发现分别复用 MPM/MOC 真实 Owner；不增加第二 CapabilityCatalog。
4. Studio 的 Symbol 中心已有工程/包/类型三来源基础，正式编辑由原 MEM/MTC/MTI 处理，不能出现 UI 私有持久仓。
5. **新核实源码事实**：`MEMSymbol.References` 独立于 RootGroup；`CheckReference` 当前允许 Symbol 拥有 MRI 引用记录，但其复合执行/多级公开边界还需专门裁决与验收，见 [#8 C01](../Architecture/CxVisual_Symbol_08_真实源码审计与规格冲突_V2.0.md)。

## 02. 提请审批的新建议
| 号 | 建议裁决 | 必须核对/前提 |
|---|---|---|
| ADR-SYM-01 | 入口函数 `RegisterSymbols` 为首选；可选轻注解/Fluent；显式 Provider 保持全能力路径 | 02～04 对象表、当前寄存 API、可信宿主/插件范围 |
| ADR-SYM-02 | Type↔Capability 使用**单一稳定显式关联**（CapabilityId/Version/PortMap），由原 Core/MPM/专业 Aspect 持久化择优 | 不能在 Studio 再写一种 Schema |
| ADR-SYM-03 | Symbol 孵化器在现有 Symbol 中心与原画布/资源编辑器中扩展七种能力面板 | 不新建工程、文档真源或独立 Undo |
| ADR-SYM-04 | Group→Symbol 先 Preview 再 Commit，支持复制/替换，跨边界公开映射与外部连接无损或拒绝 | MTC/MTI、IdMap、AspectRemapper、几何重映射 |
| ADR-SYM-05 | Visual Attachment 支持旧 FixedSide、FixedLocal、FloatingOutline、FreeLocal；默认轮廓吸附可由工具/专业选择 | MPC 持久化位置待 Core Owner 评估、MVI 真轮廓求解 |
| ADR-SYM-06 | Symbol 主要值为“可选语义”，不限制多个公开辅助端口；不创建新 Symbol 子类层次 | 当前类型/MPM 合同审查 |
| ADR-SYM-07 | Script 分级 R0/R1（纯声明/受限规则）优先；R2 可信 Provider；R3 任意外部脚本在安全方案批准前禁用 | FCM/MHS/Host/专业授权；无空泛沙箱承诺 |
| ADR-SYM-08 | 不自动替换运行中实例版本；定义/Capability/配置/Plan/Data 版本分别跟踪 | 发布与安全 Owner |
| ADR-SYM-09 | 保留现有 `MEMSymbol.References` API；明确其仅为受约束资源引用声明，复杂 MOL 执行仍归 Content；对嵌套公开边界、复制/MTI、Scene/运行进行专项验证 | `CheckReference`/`CheckEndpointScope`、工程组织 V1.2 与 DOCS/04 |

## 03. 风险与取舍
- **不用万能 Symbol**：实现路径更清晰，业务流程仍须明确 Content 组合；收益是不会出现同一逻辑两套执行结果。
- **不自动反射所有 DLL**：开发者需显式安装来源；收益是可信来源可审计、无未知构造器副作用。
- **暂不强制每个 Symbol 注册 Capability**：纯视觉更易学；高级能力绑定明确且受安全门禁。
- **连接点不限四向**：带来新持久 Schema/兼容与几何复杂度；必须先有精度/迁移/Undo 反证，不能仅在 WPF 画四个更多小 ×。
- **版本不静默热更**：用户显式批准升级稍多一步；避免运行中的 PLC/CT 控制意外变化。

## 04. 具体审批/执行顺序
1. 负责人审评 `ADR-SYM-01～08`，记录 Accept / Modify / Reject 与原因；历史对象边界不再讨论从零重构。
2. Core/Decision/Visual Owner 分别审 TYPE↔Capability 关联、Port Attachment 版本、跨定义事务，查 DOCS/04 与真实源后定最终接口名。
3. D0→D1 先用 HMI Motor 和第二专业纵向用例验证“Content 是业务核心、Symbol 是交互入口”；不以静态类图代替实践。
4. 每项新增 API 的测试通过后，才在专项实施台账将 Proposed 改为 Implemented/Verified；本 ADR 不代表用户已经批准所有候选。

## 05. 关系文档
- [架构全貌](../Architecture/CxVisual_Symbol_00_总体架构与完整需求_V2.0.md)
- [注册与发现](../Architecture/CxVisual_Symbol_03_能力注册与发现_V2.0.md)
- [跨体系协同](../Architecture/CxVisual_Symbol_05_Content专业编排协同_V2.0.md)
- [API Owner](../Architecture/CxVisual_Symbol_07_全类UML与API合同_V2.0.md)
- [实施任务](../Implementation/CxVisual_Symbol_实施任务与现状台账_V2.0.md)
- [完整验收](../Testing/CxVisual_Symbol_完整测试与验收矩阵_V2.0.md)
