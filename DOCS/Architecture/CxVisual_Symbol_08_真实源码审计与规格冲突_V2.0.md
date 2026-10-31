# CxVisual Symbol 体系 #8——真实源码审计、架构冲突与待裁决清单 V2.0

> 2026-10-31；审计补篇，证据基线 `master@a338cdb74cef3111badceabd0b565213aaaf8fd0`。仅证实已读源代码，不代表新方案已实现或通过 CI。
> 已核对 DOCS/02～04 Excel **文件名与版本**，但没有完成其 XLSX 的逐 Sheet/行核对；S0 阶段必须完成，不能宣布类表已经签收。本文不推翻已批准上位规范。
> 阅读入口：[总纲](CxVisual_Symbol_00_总体架构与完整需求_V2.0.md)、[对象](CxVisual_Symbol_01_对象模型与四方面_V2.0.md)、[全类 API](CxVisual_Symbol_07_全类UML与API合同_V2.0.md)。

## 01. 已核实源码、边界与缺口

| 号 | 可回溯源码 | 真实事实 | 不能据此宣称 |
|---|---|---|---|
| A01 | `Mechanism/Core/Engineering/MEMNode.cs` | `MEMSymbol` 只有一个 `RootGroup`；另有独立 `References`（MRI）集合 | 完整业务子流程能在 Symbol 内执行 |
| A02 | `Mechanism/Core/Engineering/MRCVisualItem.cs` | `MRCGroup.Accepts` 仅允许 `MRCVisualItem` | Group 能直接包含 ContentRef/SymbolRef |
| A03 | `MEMEngineeringSession.cs` `CheckReference`、`MTCEditSession.cs` `Reference` | Reference owner 可为 MEMContent **或 MEMSymbol**，目标是 Project 下共享 Content/Symbol，检查可达环 | 符号之间多层引用的编辑/渲染/运行已完全贯通 |
| A04 | `MEMEngineeringSession.Contracts.cs` `CheckEndpointScope` | 实例连接端点只接受公开 Port，且对多级实例路径直接连接给出 `org.connection.must_use_public_boundary` | 可以从外面直接穿透任意层的私有端口 |
| A05 | `MPMTypeDescriptor.cs` | `IMPMTypeProvider`、`MPMTypeDescriptor` 和 `RegisterTypes`；浏览类型与写入工程是两件事 | 扫描目录即可无副作用自动安装类型 |
| A06 | `MPMTypeDescriptor.SameSchema` | 当前按输入**位置顺序**逐条比较 Property/Port/Aspect 内容 | 当前已有顺序无关 SchemaFingerprint |
| A07 | `MTCEditSession.Contracts.cs` `ExposeProperty` | 除已定义属性外，内建视觉成员为 x/y/width/height/rotation/scaleX/scaleY/shear/visible/selectable/locked；按 ValueKind 校验 | 已能直接公开任意 Text 文本内容 |
| A08 | 同上 `ExposePort` / `Connect` | 检查 Kind、Direction、TypeId、Contract、Multiplicity 与公开边界 | 视觉线自动就是业务流程 |
| A09 | `MTCEditSession.Visual.cs` | `Group` 要求至少两个同 Parent/Layer 图元；`Ungroup` 对声明/绑定在用会拒绝 | Group 提升可简单复用 Ungroup 直接完成 |
| A10 | `Mechanism/Core/Engineering/MPCPort.cs` | Port/Endpoint/Connection/Waypoints 与路线可持久；没有自由/轮廓附着字段 | 已发布任意点/浮动轮廓接口 |
| A11 | `Mechanism/Visual/Editing/MVIVisualAnchors.cs` | 四向锚点是局部矩形边中点，跟随世界变换 | 三角形/Path 始终真实轮廓吸附 |
| A12 | `Mechanism/Visual/Runtime/MVIVisualEventRouter.cs` | Visual 事件可交给授权回调 | 点击自动等于设备执行完成 |
| A13 | `Mechanism/Decision/MOCCapabilityCatalog.cs` | 离线 MOC Find/Search、AvailabilityKnown；物理目录 Decision，C# namespace 仍 Orchestration | Resolver 未检查就代表设备 NotReady |
| A14 | `Mechanism/Core/Modules/MFMModuleContracts.cs` | 五种 Module：Connector、Compute、Simulation、Service、Driver | Symbol 是第六种模块 |
| A15 | `Applications/Studio/StudioShell.SharedResourceCenters.cs` | 本工程/资源包/注册类型入口及引用覆写 | 已有七页签完整 Symbol 孵化器 |
| A16 | `Professional/HMI/PHMFaceplateAuthoring.cs` | Motor/Valve 使用共享定义、Online 可视映射，双实例不同变量；标签是页面 Text | 已有 Start/Stop/安全联锁/实机回读闭环 |
| A17 | `Mechanism/Core/Engineering/MTIPackages.cs` | `ExtractReusable` 针对共享 Content，MTI 包可预览导入导出 | Group→Symbol 事务提升已完成 |
| A18 | `.github/workflows/testcenter-windows.yml` | 含 .NET48/x64、Verify-Structure/OwnerDependencies、WPF Smoke 等入口 | 本轮新增目标都运行并通过 |

## 02. 必须修正的设计理解

1. **Symbol 的组成不是“仅 RootGroup”**。源码还暴露 `MEMSymbol.References`，它与 `RootGroup.Children` 是不同的存储关系。后续复合设计必须兼容已有 API，不能为使类图好看而删 References，也不能因此让 Group 直接持有业务对象。
2. **允许声明引用不等于允许执行流程**。Content 仍是业务流程/数据作用域与 MOL 的正式 Owner。Symbol 持引用的使用范围（模板依赖、组件组合、外部能力契约）应明确审议，而不是默认无限制递归组件运行。
3. **目前多级实例直连存在明确边界**：`CheckEndpointScope` 限制跨多级路径直连。要通过逐层公开端口与定义映射建立合法访问，不能绕过成字符串查找私有图元。
4. **MPM Schema 比较当前顺序敏感**。#3 中“稳定规范化签名不依赖声明顺序”应列为**未来改造与兼容门禁**，而不是现有行为。实现时要处理历史类型注册顺序冲突。
5. **Group 操作不是提升 API**。对带绑定、端口、Aspect、外部连线的 Group 必须先做依赖/身份预览，然后经同一 MEM Edit 事务迁移，失败全部回滚；调用多次 Session API 不等于“一次 Undo”。
6. **HMI Motor 基础还偏可视**。目前真实 Faceplate 只有 Online 映射，完整 Motor 启停、命令授权/设备回读与 Bad/Stale 应作为 G02 工作包实际开发。
7. **Text 公开是具体缺口**。当前 `ExposeProperty` 不支持把文字内容当作通用内建字符串成员，专业孵化器若要编辑内部标签，需由 Visual/MPM Owner 设计可映射的文本成员并测试版本迁移。

## 03. 三个需要 Owner 裁决的冲突（状态 Proposed）

### C01：Symbol 的 References 允许用于什么
- **代码事实**：`MEMSymbol.References`、`CheckReference` 可保存共享 Content/Symbol 引用，`Reaches` 防止引用环。
- **上位约束**：Group 只负责可视包含；复杂业务编排由 Content 管理；引用不可被误认成子对象拥有。
- **建议**：保留当前 API/持久化真源，明确 SymbolReferences 属受约束的依赖/组合关系；全链 Scene/InstancePath/公开端口/MTI/Runtime 逐项验证。若某业务组合需要 MOL 执行或跨作用域数据，则统一提升到 Content，不靠 Symbol 的 References 自发执行。
- **反例**：直接在 Symbol RootGroup 中拥有 ContentRef；或“任何 Symbol 引用一个 Content 就自动启动该 Content”。

### C02：Group 提升与公共端口无损性
- **代码事实**：`Group/Ungroup` 有同层、数量、声明使用限制；MTI `ExtractReusable` 只有 Content 语义。
- **建议**：`PreviewPromotion`（候选）先计算 IdentityMap/变换/绑定/端点边界/资产闭包/安全，`CommitPromotion`（候选）复用 MTC/MTI 真源；外部连接无法重映射则拒绝。任何非法操作保持原 Revision。
- **反例**：复制了图形却删除原 PortId/ConnectionId，仍向用户提示“成功升级”。

### C03：Type/Definition/Capability 的关联放哪里
- **代码事实**：MPM 类型、MEM 资源、MOC 能力、MFM 模块身份互不相等。
- **建议**：形成单一显式版本化关联 `PublicMember ↔ CapabilityId/Version/PortMap`，优先复用 MPM/Aspect/MTI 既有声明 Owner，由 MOC 做只读关系投影；缺 Provider 时保留静态可见状态。
- **反例**：Studio 维护另一份 SymbolCapability.json，和 MOC CapabilityDescriptor 相互矛盾。

## 04. DOCS/02～04 逐 Sheet 对照的未完成门禁

| 核对对象 | 源规格需要提取的值 | 代码/设计比对 |
|---|---|---|
| M01/M02 工程/图形 | 类、继承、Owner、Group/Symbol Root 关系 | A01～A03 |
| M03 资源与实例 | MRI 引用合法位置、地址、生命周期 | A03/A04、C01 |
| M04 属性/类型/配置 | MPM Type/Property/Aspect/FCM Schema | A05～A07、C03 |
| M05 端口/映射 | MPC Kind/Direction/Attachment、公开映射 | A08/A10/A11、C02 |
| Visual/Geometry | Path/Circle/Arc/变换/精确吸附 Owner | 轮廓求解、新 Schema |
| Decision/MOC | Catalog/Provider/Resolver/模块关联 | A13/A14 |
| Professional | HMI、Automation、Vision、CT3D、Inspection、Simulation | G02～G08 |

每条必须填写 `Sheet/行/类或API/预期Owner/现有源文件/差异/裁决/测试ID`；**只有文档名、版本、GitHub SHA，不能视作完成逐表审查**。相关 XLSX 尚未完成读取，所有候选公共类型仍是 Proposed。

## 05. 评审后的行为与证据顺序

- 对已有已批准架构约束保持遵守；发生冲突时出差异证据并送 Owner 裁决，不静默改掉合理设计。
- 实施时先重新核 master HEAD、具体源文件、DOCS/02～04 每个相关 Sheet 与 CI SHA。`github.com/oudream/cx-visual-x3` 最新文件路径为复核基点。
- 评审提交至少带：对象图/Owner、跨层数据流、正例/失败例、版本/迁移、测试命令、固定 SHA、未测项。
- **本审计不是测试报告**。源文件中已有某种方法，不等于全场景经过 Windows/UIA/HIL 验收。
