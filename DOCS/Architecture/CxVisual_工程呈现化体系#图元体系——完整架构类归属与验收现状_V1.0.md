# CxVisual_工程呈现化体系#图元体系——完整架构、类归属与验收现状 V1.0

> 日期：2026-10-23  
> 类型：**架构＋真实源码现状＋待验收工作**的持续更新文档  
> 源码审计基线：`master` 的 `418893da03f2a2475f9e5a73d7e4ff4a1a4370b6`（**本文快照**；并行开发后须核对新 HEAD）  
> 主仓库：https://github.com/oudream/cx-visual-x3  
> **实现、编译、Windows CI、Studio用户操作、设备实机验收是不同证据级别；本文件不是“全部完成证明”。**

## 0. 结论、原则与范围

CxVisual 图元体系已具备九种基础几何图元、工程对象模型、场景投影、单/多选、路径几何编辑、八点缩放与旋转、通用视觉连接、端点重连、正交自动布线、人工折点、网格与智能参考线吸附、WPF Studio 基础交互及 MEM/MTC 工程事务等真实源码。它已经越过原型起步阶段，但**图元编辑体系 A、属性/行为体系 B 和工业应用 C 的完整端到端签收仍不能据此宣布完成**。

最高架构原则：**“各方面都尽能力展现，便捷友好尽支撑各专业达到目标。”**

保持：
- **一个工程真源**：MEM Project / Page / Content / Symbol / Group / Element，不增加独立持久化的 Graph。
- **一个编辑与撤销机制**：所有写操作走 MTC/MEM，拖动预览不能写 Revision；一次用户动作尽量一个 Undo。
- **数据/逻辑/视觉区别**：MRC 视觉对象、MPC Visual Connection、MOL 逻辑流程连线有各自语义，不能互相取代。
- **分层复用**：FGE → MRC/MEM/MTC/MPC → MVI → WPF → Studio → Professional，不让 WPF 反向成为工程真源。
- **现有项目优先**：.NET Framework 4.8 / WPF、28 项目约束，不为增加类数或文件数拆新项目。
- **类名前缀规范**：FGE、MRC、MEM、MTC、MPC、MVI、MPW 等沿用已确定 Owner；不要重新引入无模块归属的 Cx 前缀类。

此处的“图元体系”指几何/视觉对象与编辑、连接、Studio 交互、工业专业图形扩展的共性底座。EP01～EP15 为更大的**工程呈现化体系**目标规格，不能把其中所有运行和专业 Viewer 的目标都当成图元体系已经交付。

## 1. 全类/接口/枚举归属——按实际源码的主要类型

> 下表是当前源码中确认的**关键类/类型清单及唯一 Owner 归属**，不是未经扫描得出的“全仓所有类型零遗漏统计”。完整数量级以 DOCS/02～04 和指定提交的源码扫描为准。

### 1.1 Foundation.Geometry / FGE（纯几何）

| 文件 | 已有类型/能力 | 唯一职责 |
|---|---|---|
| `Foundation/Geometry/Primitives2D/FGEGeometry2D.cs` | 点、矩形和二维原语 | 值对象、坐标、几何边界 |
| `Foundation/Geometry/Transforms/FGETransform2D.cs` | FGETransform2D | 仿射变换、坐标映射 |
| `Foundation/Geometry/Paths/FGEPath2D.cs` | FGEPath2D 等 | 九图元共享路径/曲线几何命令 |
| `Foundation/Geometry/Paths/FGEPathEditing.cs` | FGEPathEditing | 控制点、路径段等纯编辑几何算法 |
| `Foundation/Geometry/Operations/FGEGeometryAlgorithms.cs` | 公共几何运算 | 算法，不带工程和 WPF 引用 |

### 1.2 Mechanism.Core / MRC（视觉定义）

| 文件 | 已有关键类型 | 约束 |
|---|---|---|
| `Engineering/MRCVisualItem.cs` | MRCVisualItem、MRCElement、MRCRectangle、MRCGroup | 保持 MEMNode 工程身份；Group 不等同于 Element |
| `Engineering/MRCTransform.cs` | MRCTransform | 工程局部变换、父子矩阵转换 |
| `VisualDefinitions/MRCShapes.cs` | MRCShapeKind、MRCShapeDefinition、MRCShapeElement | Line、Rect、Circle、Ellipse、Polyline、Polygon、Arc、Bezier、Path 九种 |
| `VisualDefinitions/MRCVisualElements.cs` | MRCTextElement、MRCImageElement、MRCSvgElement、MRCWidgetElement、MRCWidgetTypes | 文本、图片、受限 SVG、Widget |
| `VisualDefinitions/MRCVisualAppearance.cs` | MRCColor、MRCBrushDefinition、MRCVisualStyle、MRCVisualStateDefinition、MRCAnimationDefinition、MRCVisualBehaviorDefinition 等 | 样式、状态、动画、受控视觉行为定义 |

### 1.3 Mechanism.Core / MEM、MTC、MPC（复用的工程机制）

| 文件 | 关键对象 | 图元职责 |
|---|---|---|
| `Engineering/MEMNode.cs`、`MEMEngineeringSession*.cs` | MEMNode、资源、定义、引用及工程会话 | 唯一工程组织、Revision、保存/加载 |
| `Engineering/MTCEditSession.cs`、`MTCEditSession.Visual.cs`、`MTCEditSession.Contracts.cs` | MTC 编辑事务 | 创建、更新变换、连接、折点、撤销 |
| `Engineering/MPCPort.cs` | MPCPort、MPCConnection、MPCVisualWaypoint、MPCVisualRouteKind | **视觉连接身份与人工路径的持久化真源** |
| `Engineering/MEMProjectSnapshot.cs`、`MEMFileProjectStore*.cs` | 快照和工程文件机制 | 跨会话保存重开、引用完整性 |

MPC Visual Link **不是** MOL 执行/逻辑 Link。自动路由点只用于当前场景投影；人工 Waypoint 可按端点相对数据保存。不能为了 WPF 再建立第二套永久连接记录。

### 1.4 Mechanism.Visual / MVI（共性编辑和投影）

| 文件 | 类型 | 实际角色 |
|---|---|---|
| `MVISceneItem.cs` | MVISceneItem | 从工程快照派生的只读场景项、世界坐标、对象身份、路由诊断 |
| `MVISceneProjection.cs` | MVISceneProjection | MRC/MPC/MDS 映射到场景数据、连接动态投影 |
| `MVIVisualEditor.cs` | MVIVisualEditor | 统一编辑入口：创建、移动、对齐、重连、折点、多选变换、Undo/Redo |
| `Editing/MVIVisualEditing.cs` | MVIVisualEditing、MVIVisualHandle、MVIVisualHandleKind、MVISelectionTransform | 单图元/路径柄及多选世界变换 |
| `Editing/MVIVisualAnchors.cs` | MVIVisualAnchors、MVIVisualRouting | 固定连接点、法线、当前显示路径与人工点转换 |
| `Editing/MVIOrthogonalRouter.cs` | MVIOrthogonalRouter | 正交自动寻路、AABB 障碍避让、有限分级搜索 |
| `Editing/MVIVisualSnapping.cs` | MVIVisualSnapping、MVIVisualSnapResult | 网格/对象吸附、智能参考线的世界空间计算 |
| `Catalog/MVIShapeCatalog.cs` | MVIShapeCatalog | 可创建 Shape 的通用目录 |
| `Runtime/MVIVisualRuntimeEvaluator.cs`、`MVIVisualEventRouter.cs` | 运行视觉计算与事件适配 | 运行帧不污染工程、专业授权边界 |
| `MVIInteractionEventArgs.cs` | 视觉交互事件 | 将编辑意图传递给原始 Owner |

### 1.5 WPF 与 Studio（仅做显示/用户操作适配）

| 文件 | 当前职责 |
|---|---|
| `Mechanism/Presentation.Wpf/SceneView.cs` | 画布呈现、鼠标键盘、选择/拖动/控制柄/连线/路径折点/临时预览/参考线 |
| `Renderers/MPWShapeRenderer.cs`、`MPWVisualAssetRenderer.cs` | WPF 形状和资产渲染 |
| `Widgets/MPWWidgetPresenter.cs` | 工程 Widget 映射到 WPF 呈现 |
| `MPWVisualHandleEventArgs.cs`、`MPWMoveEventArgs.cs`、`MPWNodeEventArgs.cs` | WPF 交互事件适配 |
| `MOLFlowCanvas.cs` | MOL **专业逻辑画布**，复用共性操作但保持 MOLLink 语义 |
| `Applications/Studio/StudioViewModel*.cs` | 编辑命令、工程状态与提示 |
| `Applications/Studio/MainWindow.xaml[.cs]` | 工具箱、画布、基础属性交互与快捷操作 |
| `Applications/Studio/StudioShell.GraphicsTree.cs` | 当前资源内部对象树、选择和导航 |
| `StudioShell.ResourceDocuments.cs`、`StudioShell.ResourceEditors.cs` | 编辑器选择及资源多文档管理 |

## 2. 图元的完整目标能力与当前边界

### 2.1 基础绘制和变换

九种 Shape 的几何定义、创建 API 已有实现；文字、图像、SVG、Widget 也有基础类型。单选、框选、Ctrl/Shift 多选、移动、八点缩放、旋转、Path/Bezier/Arc 控制柄具有实际源码和相应回归。多选统一外框与世界空间变换已进入 Editor/SceneView。近期又实现网格和对象吸附的统一计算、参考线与方向键微调。

**仍待完善**：等比/中心缩放、反向拖动、严格角度吸附、全套对齐/均匀分布 UI、复制/粘贴与 Duplicate、跨层级选择、复杂 Group 的用户级操作一致性。

### 2.2 编辑状态互斥

真实画布应严格区分：
- **HoverConnect**：悬停连接提示、合法四向端口，不应出现不相关缩放柄。
- **SelectionTransform**：八点缩放 + 外部旋转柄；多选时只有一组共同控制柄。
- **GeometryEdit**：Bezier/Arc/Path 锚点与曲线控制，不和普通对象变换混用。
- **ConnectorEdit**：连接线的起止端点与中间折点，单独命中优先级。
- **Design/Preview/Run/Debug**：编辑态与运行态隔离，Preview 不得误发未经授权的现场设备动作。

取消、Esc、失焦必须丢弃临时预览且不增加 MEM Revision；释放鼠标后的持久化变更应一次工程事务对应一次 Undo。

### 2.3 视觉连接与路径

**现有模式**：Straight、Orthogonal、Manual。

- **创建/重连**：使用 MPC Visual Port/Connection，保留连接 ID。
- **固定端口**：四向边缘锚点；支持出线方向和旋转世界法线。
- **显示及编辑路径**：MVI 计算出场景真实顶点，Editor 使用相同场景路径解释插点/拖点索引，不允许 UI 与 Core 分别按不同折线编辑。
- **自动正交**：实体 AABB、8 世界单位左右的膨胀安全边距、有界分级搜索与出线方向；极端拥挤可能降级，**不能宣称全局无碰撞**。
- **可诊断降级**：存在冲突时通过场景只读 VisualRouteDiagnostic/WPF 警示反映；不得显示为正常避障成功。
- **人工折点**：插入、移动、删除、相对两端存储、保存重开、Undo/Redo。

**待开发**：浮动端口、自定义连接点、线标签、复杂交叉优化、多条连线通道分配、严格端口/障碍轮廓避障、稳定的高密度智能布线和大型画布性能验证。

### 2.4 Group / Symbol / Layer 与专业复用

Group、Symbol、Layer 等有 Core 定义及部分 Studio 结构与测试基础，但不能因存在类与 API 就宣布“完整可交付”。以下仍需要端到端验证：

- Group/Ungroup/嵌套编辑、进入/退出编辑上下文、双向对象树定位。
- Symbol 定义与两个实例的覆盖、样式/变量继承、保存重开及用户从实例跳回定义。
- 锁定/隐藏/排序/跨层移动、多选混合锁定和复制 Group/Symbol 的引用一致性。
- HMI 电机 Symbol 两实例不同 MDS、绑定与 Widget/事件、运行状态与无授权命令拒绝。
- Vision ROI/Overlay、CT3D 测量/MPR/Gizmo 等由 Professional 贡献编辑/Viewer 能力，不创建第二套工程与 Undo。

## 3. 开发成熟度与实际验收

成熟度只作**源码审计**，不当作完成百分比或生产级性能承诺。

| 分项 | 当前结论 | 证据边界 |
|---|---|---|
| FGE 二维路径、变换和几何操作 | 基础较完整 | 有源码和基础回归，仍需极值和退化验证 |
| MRC 视觉定义 + MEM/MTC 事务 | 基础较完整 | 同源定义、Revision、Undo/Redo、持久化 |
| MVI Shape 编辑/多选 | 代表性能力已实现 | 跨 Group、多选、吸附已有代码与测试，但未全场景 E2E |
| 通用 Visual Connection | 代表性能力已实现 | 连接、重连、正交避障、人工折点、告警，有回归 |
| WPF SceneView 编辑 | 基础可操作 | 真实 WPF 测试已有，但不能替代所有系统级鼠标注入 |
| Studio 上下文属性/命令 | 部分完成 | 需统一 Ribbon、Schema、右键菜单与工具栏 |
| HMI/Vision/CT3D 全用户闭环 | 尚未最终签收 | 专业 Workbench、运行/设备安全证据待补 |
| 工业级性能 | 未充分验证 | 缺大型场景分位延迟、长时运行、DPI、多连接压测 |

### 3.1 最新 HEAD/CI 使用约束

本文件编制快照的 HEAD 为 **`418893da03f2a2475f9e5a73d7e4ff4a1a4370b6`**。该 SHA 的工作流在生成文档时仍处于并发推进中；**不能将旧 SHA 的 Windows Success 写成它全部通过**。在每轮继续开发前，重新查：
1. GitHub `master`、提交列表和受影响文件。
2. 对应 SHA 的 Engineering Organization / Studio S1 / ArchitectureSmoke / TestCenter / Config 等 Windows 实际结果。
3. 正例、异常反证、保存重开和失效/锁定/取消情况下的 Revision。
4. UI 操作证据：代码测试、WPF STA、鼠标注入 E2E、工位运行是不同层次。

此前实施审计文档 `DOCS/Implementation/CxVisual_工程可视化_ABC_源码与Windows验收审计_V1.0.md` 以及 `工作包A+B_完整实现与真实验收.md` 的“整体未最终签收”边界应继续保留，直到新的可复现证据替代它。

## 4. 剩余任务：按用户闭环驱动而非按新建类数推进

### P0：正确性与一致性（优先继续）
1. 保持**场景显示、鼠标命中、插点/拖点、自动路由**使用相同路径；针对复杂避障、同侧/旋转端口、端点移动逐项测试。
2. 对自动路由“无法无碰撞寻路”的情况提供可定位诊断和明确 UI 提示；不让冲突路径假冒成功。
3. 对多选世界变换、复杂嵌套 Group、锁定/隐藏、Esc/失捕、吸附与一次 Undo 做持续异常回归。
4. 以最新同一 SHA 完成 Windows CI；把失败修复与证据记录清楚。

### P1：成熟图元编辑器体验
1. Group/Symbol/Layer 编辑闭环、复制粘贴/克隆、完整右键与统一命令。
2. 上下文 Ribbon、Schema 属性编辑、多选混合值及对象树统一选择。
3. Path 多子路径精细操作、键盘微调/参考线/尺寸角度反馈与交互细节。
4. Visual Connection 高级锚点、自动布线交叉优化及布局稳定性。
5. 性能：视口裁剪、空间命中、增量路径重排、图元数/连线数规模与长时稳定性。

### P2：专业与生产验收
1. HMI 工业电机 Symbol 双实例，真实绑定、运行、权限反证及保存重开。
2. Vision ROI / CT3D 专业 Viewer 通过共性接口接入。
3. 大工程压力、真实键鼠 E2E、多 DPI、自动化稳定性、设备授权的独立现场验收。

每个工作单元的最终标准：**源码 API + Studio 用户路径 + 工程保存重开 + Undo/Redo + 异常反证 + 真实 Windows CI（对应 SHA）**。单有类名、通过编译或提交成功不得签收。

## 5. 新聊天完整承接指令

> 请在 `https://github.com/oudream/cx-visual-x3` 的最新 `master` 上连续开发 CxVisual 图元编辑体系。首先读取 `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md` 和 `DOCS/01～04`，再核对本文件、工程呈现化目标规格、A/B/C 审计、最新源码与 Windows CI。遵守 FGE/MRC/MVI/MPW 和 MEM/MTC/MPC 的唯一 Owner，不从零设计、不另起工程或连接模型；不把提交等于交付。围绕 P0 正确性、P1 操作完整性、P2 专业闭环继续直接编码，持续编译、测试和修复。直接提交 master、中文 Commit、中文关键注释、不新增空类；每轮列文件、Commit、实际功能、测试 SHA 与未完成项。

## 6. 权威资料导航

- `DOCS/01_CxVisual_平台灵魂与架构宪法_V1.0_Review.md`
- `DOCS/02_CxVisual_01至05_统一规格基线_V4.9_Foundation_Mechanism_ProfessionalDomain.xlsx`、`DOCS/03...xlsx`、`DOCS/04...xlsx`：所属 Sheet 的规格与唯一 Owner。
- `DOCS/Architecture/CxVisual_工程呈现化体系——完整需求_角色期望与使用样例_总体详细设计_V1.0.md`：EP01～EP15 **目标规格，不代表已实现**。
- `DOCS/Architecture/CxVisual_工程组织体系——完整架构设计与开发使用指南_V1.2.md`：组织身份、Group/Symbol/引用与 Core Owner。
- `DOCS/Implementation/CxVisual_工程可视化_ABC_源码与Windows验收审计_V1.0.md`
- `DOCS/Implementation/CxVisual_工程可视化_工作包A+B_完整实现与真实验收.md`
- [`#图元编辑——完整设计与验收 V1.2`](./CxVisual_工程呈现化体系%23图元编辑——完整设计与验收_V1.2.md)：**十四章、四附录、A～F 操作图、完整几何/连接/事务合同及 GE-01～GE-15 验收**；本文件继续负责类归属与实际现状，不互相复制状态台账。
- `Tests/ArchitectureSmoke/Visual/Editing/VisualShapeEditingTests.cs`
- `Tests/ArchitectureSmoke/Engineering/Studio/OrganizationWpfTests.cs`
- `Mechanism/Visual/`、`Mechanism/Presentation.Wpf/SceneView.cs`、`Applications/Studio/`

**维护规则**：此文为图元体系“完整导航与现状审计”，应在同一个文档持续更新；明确保留不确定和未验证事项。不要复制出多份事实相冲突的新主规格。
