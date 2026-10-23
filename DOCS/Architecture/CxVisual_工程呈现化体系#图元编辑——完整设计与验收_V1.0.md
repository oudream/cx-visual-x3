# CxVisual_工程呈现化体系#图元编辑——完整设计与验收 V1.0

> **文件定位**：图元编辑的交互语义、工程行为、用户体验及可执行验收主规格（**设计与验收基线，不是“全部实现证明”**）。  
> **编制/融合**：2026-10-23。由此前《CxVisual-架构（图元）》的交互讨论、2026-10-22 图元示意图与 draw.io 对照、后续 P0/P1 开发讨论及本仓真实源码重新整理；**此前未落库的同名原稿未能逐字恢复，本文不冒称原稿原文**。  
> **源码审计基线**：`cx-visual-x3` / `master` / `602fa7fdabfa0eb9c2407498af940943ed2eac78`。该 SHA 是本文编制前的**阅读快照**，不是文档提交 SHA。并行提交后的真实状态、CI 必须按最新 SHA 重新核验。  
> **源头对照**：[图元体系架构与类归属](./CxVisual_工程呈现化体系%23图元体系——完整架构类归属与验收现状_V1.0.md)、[工程呈现化 EP01～EP15 总规范](./CxVisual_工程呈现化体系——完整需求_角色期望与使用样例_总体详细设计_V1.0.md)、[A/B/C 源码审计](../Implementation/CxVisual_工程可视化_ABC_源码与Windows验收审计_V1.0.md)、[A+B 实施验收](../Implementation/CxVisual_工程可视化_工作包A+B_完整实现与真实验收.md)。  
> **图示界限**：聊天中的三栏 A/B/C《CxVisual Studio 图元编辑示意》是**拟采用设计图**，不是现有 X3 可运行界面截图，也不是任何交互已完成的证据。

## 00. 文档分工、目标和阅读规则

本文件只负责“**图元如何被创建、选择、连接、操作、反馈、撤销、复用、验收**”；不再抄录上位《#图元体系》已维护的全类目录/提交台账，也不复制整个 EP01～EP15、Studio S1～S4、MOL/MOC/MRK/MDS 主规格。

- **图元体系（现有文档）**：稳定模型、Owner、关键类归位、源码成熟度与历史证据入口。
- **图元编辑（本文）**：可观察的交互行为、状态互斥、坐标/持久化合同、异常反证和逐条验收。
- **实施/测试台账（已有文档和 GitHub CI）**：某 SHA 的代码、编译、回归、人工操作、性能与专业运行事实。本文只引用，不平行登记第二套“已完成列表”。

核心原则：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。** 共性足够强、使用足够轻，专业只加专业语义。

**证据标签**：`[目标]`、`[已有源码]`、`[自动测试]`、`[需实鼠验收]`、`[需专业/实机验收]`。设计示例里的“应/必须”不表示现有实现；“已有源码”不代表 Studio 全流程完成；CI Success 只代表相应 SHA 和运行的测试范围。

## 01. 范围、编辑对象与唯一 Owner

### 1.1 编辑对象

- **基础 Shape 九种**：Line、Rect、Circle、Ellipse、Polyline、Polygon、Arc、Bezier、Path；共享路径几何，特殊 Shape 有独立的几何参数。
- **可视对象**：Text、Image、受限安全 SVG、Widget、Group、Symbol 实例及资源、Layer/ZOrder。
- **视觉连线**：固定/浮动附着、快捷连接、直线/正交/人工路径、折点、端点重附着、线路诊断。
- **工程复用**：同一 Project/Page/Content/Symbol/Group/Element 工程身份、资源实例覆盖、保存重开、Undo/Redo、工业专业工作台的属性/运行态关联。

### 1.2 Owner 分层（不得重复造轮子）

| Owner | 正式职责 | 禁止 |
|---|---|---|
| `Foundation.Geometry / FGE` | 点/矩形/仿射、路径/曲线、几何命中、纯坐标运算与参数编辑 | 引入 WPF、MEM、设备或 Studio 状态 |
| `Mechanism.Core / MRC` | 形状/视觉对象/样式/状态定义、Group/资源及稳定身份 | 在 WPF 另写持久化对象类型 |
| `Mechanism.Core / MEM、MTC` | 唯一工程、草稿事务、Revision、批处理、Undo/Redo、保存重开、复制重映射 | 第二 Document/Undo 栈 |
| `Mechanism.Core / MPC` | `MPCPortKind.Visual`、`MPCPort`、`MPCConnection`、视觉人工路线 | 再建 VisualEdge/Graph 存储 |
| `Mechanism.Visual / MVI` | 场景只读投影、选区计算、世界坐标变换、吸附、连接编辑/路由/诊断、交互意图 | 保存 UI 控件或专业设备状态 |
| `Mechanism.Presentation.Wpf / MPW` | 命中反馈、手柄/辅助线绘制、鼠标/键盘、捕获/预览、DIP 转换 | 定义工程事实、建立自己的历史 |
| `Applications.Studio` | 资源编辑器、工具箱、上下文 Ribbon、树与属性面板、命令授权与用户操作路径 | 在 MainWindow 堆另一套几何/模型规则 |
| `Professional` | HMI、MOL、Vision ROI、CT3D Overlay 等专业语义和 Viewer 行为 | 复制基础选择/连接/撤销框架 |

**重要区别**：图元之间的视觉连接使用 MPC Visual 端口/连接。MOL 的逻辑端口、`MOLLink` 与流程执行语义继续由 MOL 自身负责；两者可以共享鼠标/绘制技术，不可合并为同一个逻辑模型。

## 02. draw.io 对照后的六类标记：各司其职

> 下列是参考产品的真实交互语义与 CxVisual 目标的**对应关系**，不是声称 X3 已完整实现。颜色只是默认视觉辨认规则，支持主题配置；不能依靠颜色单独区分可操作性。

| 标记 | 主要触发场景 | 用户动作/责任 | CxVisual 归属与状态 |
|---|---|---|---|
| **① 蓝色圆形 8 柄**（四角+四边） | 图元已选中，进入对象变换 | 移动边界以缩放/改变尺寸；Group 多选显示**共用的一组**柄 | MVI 世界变换 + MPW 绘制；已有单/多选基础，细节需回归 |
| **② 右上环形旋转柄** | 图元已选中且允许旋转 | 围绕明确的变换中心旋转，支持角度吸附；**不是连接入口** | MVI 旋转；已有基础/部分修饰键，完整 DPI/混合选区待验 |
| **③ 四向快捷连接箭头** | 可连接图元悬停/连接意图 | 沿方向创建连接；可显示“选择目标/创建并连接”便捷入口；**不是缩放柄** | MPW 显示意图、MVI/MPC 提交；已有四向固定端口基础，快捷创建完善待验 |
| **④ 边缘小 × 固定连接点** | 悬停/显式连接点编辑模式 | 连接端点固定附着在指定端口（随对象移动/旋转保持该点）；可单独进入增加/调整/删除连接点 | MPC Visual Port；已有四向固定点，任意自定义点/管理尚待完整 |
| **⑤ 黄色特殊参数控制柄** | 某种 Shape **具备该参数且处于对应编辑上下文** | 调 Arc 角度、某种形状圆角/几何专有参数等；不默认等同于连接/缩放 | FGE 特殊几何参数 + MRC 定义；通用枚举不应捏造每种 Shape 都有此柄 |
| **⑥ 连接线蓝色节点/折点** | 连接线选中、进入路径编辑 | 拖端点重附着；插/拖/删折点，切换直线、正交/手动；**不修改图元几何尺寸** | MPC Connection + MVI 路由 + MPW 命中；主要基础已有，复杂模式未验收 |

draw.io 官方对照（参考 UX，而非 X3 当前承诺）：
- [移动/旋转/缩放](https://www.drawio.com/docs/manual/shapes/shape-resize/)；
- [固定/浮动连接](https://www.drawio.com/docs/manual/connectors/connector-fixed-vs-floating/)；
- [编辑连接点](https://www.drawio.com/docs/manual/shapes/shape-connection-points-customise/)；
- [快捷方向箭头/克隆并连接](https://www.drawio.com/docs/manual/connectors/connect-shapes/)；
- [线段与 Waypoint](https://www.drawio.com/docs/manual/connectors/waypoints-connectors/)。

**两项必须禁止的交互误解**：
1. **固定连接点不是缩放柄**；固定端口不改变形状的宽高，缩放柄不创建 `MPCConnection`。浮动连接也不等于固定端口。
2. **快捷方向箭头不是缩放柄或固定点**；它是“开始连接/快捷新建并连接”的动作入口。固定/浮动的附着方式在**落点**处决定，不由箭头外观决定。

补充：draw.io 的中心缩放常使用 Ctrl/Cmd；当前 X3 变换代码已有 Shift 等比、Alt 中心及 Shift 旋转吸附参数。**本稿不擅自把参考产品快捷键覆盖现有热键**，冲突要通过 Studio 统一快捷键表与实鼠验收后定案。

## 03. 三幅场景 A/B/C：一个任务、三个互斥编辑状态

聊天示意图的布局保持一致：Studio 顶部**一级菜单一行 + 当前上下文 Ribbon 一行**；左侧工程资源树与当前资源对象/工具箱；中央单个图元画布；右侧属性/绑定/样式/状态/事件；底部可折叠诊断与状态栏。图 A/B/C 是**同一画布的不同操作瞬时状态**，不是同一图元同时叠加全部控制柄。

### 3.1 A：悬停·连接（HoverConnect）

**进入**：画布处于设计可编辑态，鼠标悬停可连接、未锁定且命中许可的图元；或明确按下“连接工具”。

**呈现**：边缘小 × 固定连接点；四向快捷连接箭头；合法目标高亮、非法目标提示；不出现八点缩放柄与旋转柄。已选图元可有轻量选中边框，但不能遮蔽连接命中点。

**动作**：拖固定点 → 在目标固定点结束 = 固定连接；拖箭头 → 悬停目标轮廓结束 = 浮动连接目标（目标规范）；方向箭头点击 → 便捷选择已有/新建图元并连接（P1 目标）。非法目标不生成孤立连接。

**退出**：鼠标离开、Esc、点击空白、改变工作模式、失捕、定义被锁定/删除；仅清理临时状态，不变更 MEM Revision。

### 3.2 B：选中·变换（SelectionTransform）

**进入**：单击选择对象、Ctrl/Shift 多选或框选，当前没有开始连接/路径节点拖动。

**呈现**：八点蓝圆点、右上旋转柄；多选时只显示一组共用外框与控制柄；按需显示智能对齐线、尺寸角度提示、锁定只读标记。**不把快捷连接箭头贴在正在拖动的缩放柄附近。**

**动作**：拖图元 → 移动；拖 8 柄 → 尺寸/仿射；拖旋转柄 → 旋转；键盘方向键精调；对齐/分布/层次/Group/Undo 走统一命令。保持 Shift/Alt 与已有代码约定，允许从中心/等比的明确组合。

**退出**：切换至几何/连线编辑、选区失效、Esc 取消预览。只有完成提交后修改 MEM，Undo 一次回到动作前状态。

### 3.3 C：选中·连线（ConnectorEdit）

**进入**：用户点选视觉连接线，或明确进入“连接线路径编辑”。

**呈现**：线端点、已存在折点、可插点线段与选中线提示；冲突/无解时线型有警告反馈；不出现连接所附图元的尺寸柄。可显示“自动/手工/直线”模式及诊断说明。

**动作**：拖端点重附着（身份不变）；单击/双击相应线段插点；拖已有折点；右键删除/清空折点；在自动/手工之间切换时清楚解释是否保留人工约束。

**退出**：Esc/失捕回滚临时预览；提交一次 MPC/MEM 事务，Undo 与保存重开保持线身份和端点。

### 3.4 状态机（行为规格，优先沿用现有 SceneView 字段）

`Idle → HoverConnect → DragNewConnection → Commit/Cancel → Idle`  
`Idle → SelectedTransform → DragMove/DragResize/DragRotate → Commit/Cancel → SelectedTransform`  
`SelectedTransform → GeometryEdit → DragAnchor/DragControl → Commit/Cancel → GeometryEdit`  
`Idle/SelectedTransform → ConnectorEdit → DragEndpoint/DragWaypoint → Commit/Cancel → ConnectorEdit`

- 同时只可有一种**主动拖动捕获**，悬停反馈是派生态而非第二持久交互会话。
- 专业工作模式 `Design / Preview / Run / Debug` 是更上层的授权/显示模式，与上述**图元编辑瞬时状态正交**；运行态必须禁止无授权工程修改/现场命令。
- WPF 先按**活动拖动句柄 → 连接端点/路径节点 → 已选对象控制柄 → 固定端口/快捷连接 → 图形本体 → 空白画布**计算命中，冲突优先级应以真实源码调用链核定并有覆盖测试；这是目标规则，不是目前已验证的完整命中顺序。
- 任何 Esc、CaptureLost、切换 Tab/页面、窗口失焦、工程关闭，必须取消 preview、释放鼠标捕获并恢复合法状态。不能在 LostCapture 误提交。
- 不应引入一个新的“第二状态机框架”。先集中已有 `SceneView` 的零散布尔状态判定与冲突规则；仅在复杂度已被测试证明不可维护时提取轻量无状态协调器。

## 04. 图元绘制、几何精细编辑与变换契约

### 4.1 基础编辑共同能力

`Create → Select → Move/Resize/Rotate → Align/Distribute → Group/Symbol/Layer → Style/Binding → Save/Open`。

共同的算法语义不应该在 Rect、Bezier、Widget、Group 分别实现一份。各 Shape 的参数化编辑分两类：
- **对象变换模式**：改变整体世界变换与尺寸，统一选区外框控制柄。
- **几何编辑模式**：改变定义内部的局部路径点/控制参数，使用几何专用柄，保持不相关点世界位置和正确闭合/开合。

九 Shape 分组：
- Line、Polyline、Polygon：线段/顶点插删、闭合、多路径语义。
- Arc、Bezier、Path：曲线控制点、二次/三次段转换、精确分割、子路径闭合/断开、合法退化处理。
- Rect、Circle、Ellipse：共用矩形外框变换；专有几何参数才允许出现第⑤类参数柄。
- Text、Image、SVG、Widget：共用布局/变换，内容的显示/焦点/资源安全由适配器承担，不能用路径编辑假装输入控件本身的编辑。

### 4.2 多选、Group、旋转的矩阵规则

- 用户拖动、选区吸附、参考线均在**世界坐标**定义；提交前转换到每个对象的父节点局部坐标；旋转 Group 不允许直接用父局部坐标分别取整造成成员相对间距漂移。
- `MVISelectionTransform` 作为共用变换求解源；WPF 预览和 `MVIVisualEditor.TransformSelection` 以相同输入生成变换。缩放负倍率/退化零宽高须有明确策略；**默认不能无提示把反向拖动转换为镜像**。
- 父子不能同时作为独立选区成员被重复变换；锁定、隐藏、跨不可编辑定义、来源失效应**整组拒绝**，不能只移动可编辑子集。
- Ctrl/Shift 多选、框选、对象树同步、跨 Tab 旧选区拒绝、右侧混合值编辑等不能各保存另一份权威 SelectedIds。
- 智能参考线 `MVIVisualSnapping` 只计算场景投影的吸附位移与辅助线，不写 MEM；网格显示必须和世界网格同源，DIP 容差随 Zoom 换算。现有实现是初阶对齐吸附，未验证 1000 图元时的成本。
- 预览一帧可重新计算，但只有一次用户确认写入 MEM；若位移为零或取消，Revision 不应增加。

### 4.3 命令与右键行为

设计统一**意图/命令语义**，而不是要求强行新增“命令总线项目”。一项功能可以由顶部 Ribbon、右键菜单、键盘、属性面板调用同一 `MVIVisualEditor`/`MTC` 编辑操作，并共享启用条件、锁定与错误提示。保留现有 Studio 命令体系，逐项消灭重复调用路径。

第一批：删除、复制/剪切/粘贴、Duplicate、Undo/Redo、Move/Nudge、Align/Distribute、BringFront/SendBack、Group/Ungroup、Lock/Hide、EditGeometry、EditConnection、EditConnectionPoints。所有项均需实际 Studio 鼠标/键盘测试。

## 05. 视觉连接：身份、端点、路径与失败

### 5.1 五个不同的概念

1. **端口定义**：`MPCPortKind.Visual`、`MPCPort`，归属工程对象/实例关系。
2. **连接身份**：`MPCConnection` 保持 ID 与来源/目标，撤销及保存重开不变。
3. **附着策略**：固定（指定端口/位置）与浮动（轮廓上随布局自动计算的锚点）。**后者为目标扩展，现有四向固定点不等于已完成浮动边缘附着**。
4. **线路由**：Straight / Orthogonal / Manual；自动场景点是投影计算结果，人工 Waypoint 是工程编辑约束。
5. **UI 状态**：连接悬停、正在拖线、端点重附着、连线路径编辑、显示错误告警，全都是投影与输入状态。

### 5.2 从用户操作到工程提交

`指向连接点/箭头 → 拖出临时线 → 目标几何命中/附着候选 → 语义校验 → MVI编辑 → MEM/MPC事务 → 投影刷新`。

- 悬停非法对象（类型不允许、锁定、跨不可连上下文、循环/专业约束不通过）必须反馈且不得私自创建匿名端口。
- 拖动端点只**重附着同一条**连接；不得删旧线建新线冒充原操作。
- 所有“插点”必须读取实际场景渲染的 `VisibleConnectionPoints` 顺序；不允许在 UI 点中一段而核心重新按另一折线计算后插错位置。
- 自动路由失败不能静默走穿障碍：`VisualRouteDiagnostic` 为只读场景诊断，WPF 视觉警示+属性/日志具体原因，诊断不写 MEM Revision。
- 自动模式下用户改折点，须显式转为 Manual 或保存为明确的人工约束；不允许用户修改马上被下帧自动重算覆盖却无提示。
- 拥挤线路的交叉数、间距、通道、端口出入方向和性能需单独验收。现有 `MVIOrthogonalRouter` 的障碍回避和有限搜索不是“全局最优布线”证明。
- 自动路线、人工折点、Group 旋转、端点移动、窗口 Zoom/平移必须共享**同一场景几何真相**，避免渲染/命中/插点不同步。

### 5.3 专业边界

MOL 流程图中的语义连线由 `MOLLink`、专业端口与编排合法性支配；可以选择共享几何路由/连接呈现，但不应把 `MOLLink` 改为 `MPCConnection` 或让普通连接意图绕过 MOL 流程编译规则。

## 06. Group、Symbol、Layer 与实例编辑的真实闭环

- **Group/Ungroup**：复用 `MTCEditSession.Visual` 已有事务。Group 是 `MRCVisualItem`，不从 `MRCElement` 派生；支持进入/退出子编辑上下文、不同变换级别、嵌套、旋转、多选、对象树双向定位。
- **Symbol**：由工程定义/资源引用组成，不另建“绘图 Symbol 库”。定义编辑与实例编辑区分，至少两个实例能独立覆盖属性与 MDS 绑定；从实例跳回定义且返回时保留选区定位。
- **Layer/ZOrder**：图层的可见/可选/锁定以及同级对象绘制次序要一致；对象树与实际绘制前后关系同步。跨层迁移必须保留连接和关联 ID 或明确拒绝并给出原因。
- **复制/剪切/粘贴/Duplicate**：Core 已有 `MEMEngineeringSession.PreviewDuplicate` / `Import` / `MTITransferPlan`。应先审计其视觉对象、Group 子树、引用、端口、内部连接、外部连接策略、坐标平移与安全性，再让 Studio 提供剪贴板和粘贴落点。**MOL 的逻辑片段剪贴板不是视觉复制实现**。不得私下逐属性 new 一个 Shape 代替完整引用重映射。
- **剪切与粘贴**须明确是否原子跨作用域、失败回退及历史边界；不支持时诚实拒绝，不能留下半复制/半删除的工程。

## 07. Studio 用户体验合同

### 7.1 布局与命令

坚持最新 Studio Shell 的整体结构：顶层固定九项一级菜单及当前上下文 Ribbon **两行**；中央资源配置组合面板/图元单画布/流程单画布按编辑器类型切换，不增加不明的第三层“工具标题栏”；资源树不直接混入所有 Group/Element，而是在**当前资源对象树**展示；右侧使用 MPM Schema 属性、绑定、样式、状态、动画、事件与专业参数；底部 Diagnostics/Trace/Watch 按工作台显示。

状态显示应细致但轻：模式名、当前资源、选区数量、坐标/尺寸/角度/缩放、修改提示、路由错误和锁定原因；工具图标清楚、间距适中、面板不肿、浅色/VS 风格深色主题一致。

### 7.2 属性与专业控制柄

- 无选区显示画布属性；单选显示对象及其类型化 Schema；同类多选支持批量属性/混合值，不得把内部 GUID/路径命令索引要求用户手填。
- 鼠标控制点、属性值编辑、Ribbon 命令应作用于同一工程对象及同一历史。
- 黄色特殊控制柄仅由 Shape 类型的**真实可编辑参数能力**决定可见性；PropertyPanel 内同步该参数。无对应几何能力时不绘制假柄。
- Widget 编辑态/运行态焦点切换明确；设计时拖动控件不等于执行其设备事件。
- Visual Connector 选中时只显示线属性、端点和路由参数；不把连接当成 Rect 去变换尺寸。

### 7.3 用户可见错误

锁定/隐藏、非法连接、浮动端点无解、路径退化、不可逆变换、资源缺失、旧对象引用、跨定义不可编辑，都应以短提示+可展开详情说明。错误被拒绝后 Revision 和连接身份必须保持不变。UI 不弹阻塞式重复对话框作为常规反馈。

## 08. 两种角色的可执行使用期望

### 8.1 软件开发者（目标用法，依当前真实 API 收敛）

最少自然调用链：通过 MEM 创建工程/页面/九 Shape 与资源定义；`MVIVisualEditor` 获取场景并调用 Move/Transform/Align/ConnectVisual/SetVisualRoute/折点操作；依赖 MEM 单一 Edit/Undo/Redo/Save/Open；查询只读场景和诊断。不能为了实现一个拖动动作同时手改 WPF 控件位置与持久工程坐标。

**已存在的实际 API 代表**：
- `MEMEngineering.Create(...)`、`AddPage(...)`、`AddRectangle(...)`；
- `MVIVisualEditor.SetPage(...)`、`MoveSelection(ids,worldDx,worldDy,snapToGrid)`、`TransformSelection(...)`；
- `MVIVisualSnapping.Resolve(...)`（纯求解）；
- `MEMEngineeringSession.PreviewDuplicate(...)` / `Import(...)`。

这些只是当前关键入口实例，**不是复制/浮动端点等尚未实现能力的虚构 API 承诺**；签名、Owner 和兼容性以最新源码为准。

### 8.2 工程编排用户（真实 Studio 操作验收）

打开工程 → 打开某 Page/Content 编辑标签 → 工具箱拖九 Shape → A 悬停显示合适的连接提示 → B 选中变换/对齐/Group → C 选线重连/折点 → 保存重开 → Undo/Redo → 定义 Symbol 并在另一 Page 放两个实例 → 配置不同属性绑定 → 预览/运行/调试定位。

用户不必理解 `MPCPort`、`MRCTransform` 或自己输入 GUID、折点索引；这些属于内部实现。**不能用 API 测试成功替代上述鼠标完成路径**。

## 09. 代表性完整案例与验收反证

### E-A：图元编辑（本规格主要签收）

一页：Rect/Circle/Ellipse、Arc/Bezier/Path、Text/Image/SVG/Widget、Group、两条以上视觉连线。

步骤：工具箱放置 → A 悬停（固定 × /方向箭头） → B 单/多选变换（八柄+旋转） → 几何节点模式（Bezier） → C 连接线（端点/折点/自动诊断） → 对齐/图层/复制 → Undo/Redo → Save/Open → 与源码对象身份一致。

反证：选区中含锁定/父子同选；曲线零长段；拖动中 Esc、失捕或页面切换；环形旋转不得发起连接；快捷箭头不得缩放；自动路由碰撞必须警告；复制不得保留重复的工程 GUID。

### E-B：电机 Symbol 双实例（公共能力与专业绑定）

Studio 定义电机 Symbol（外框+转轴+状态显示）→ 放两个实例 → MPM 不同参数/MDS 变量绑定 → 预览状态/动画 → 点击需授权命令 → 未授权禁止设备动作 → 保存重开 → 定位定义及两实例 → 运行/调试轨迹不写工程 Revision。

该案例需与 EP04、EP06～08、EP11～15 以及 HMI 专业 Owner 联验；图元编辑本身通过不等于电机现场能力通过。

### E-C：X-Ray/CT 专业共性复用

MOL 主流程+Vision/AI/传统子流程和 HMI 视觉对象共享工程身份与 SourceMap 追踪；Vision ROI、CT3D Overlay/MPR/标注等通过专业 Viewer 提供附加编辑/坐标语义。专业需要复用选区、命令、Undo、诊断边界，但不能直接让通用 2D SceneView 假装拥有 CT3D 体坐标和测量模型。

## 10. 真实实现与差距：2026-10-23 阅读快照

> 以下是基于本轮读取的实际文件/旧测试与讨论作的**源码状态**，不把参考图、原聊天建议或历史绿色提交冒充最新主仓的最终用户验收。

| 能力 | 当前证据入口 | 谨慎结论 |
|---|---|---|
| 九 Shape/Path/Bezier/Arc 编辑 | `MRCShapes.cs`、`FGEPathEditing.cs`、`MVIVisualEditing.cs`、`VisualShapeEditingTests.cs` | **已有源码/部分自动测试**；完整 Shape 专有柄和极端路径仍需验收 |
| 单选/多选、共用柄、旋转/吸附、键盘精调 | `MVISelectionTransform`、`MVIVisualEditor`、`MVIVisualSnapping`、`SceneView` | **已有源码与相应回归**；全量实鼠、DPI、复杂反向与混合选区未签收 |
| 连接端点/重附着/可见折线路径、人工折点 | `MPCPort.cs`、`MVIVisualEditor`、`MVISceneProjection` | **核心机制已存在**；浮动端口、任意自定义点、复杂协同路由未完成 |
| 正交避障和失败显式警告 | `MVIOrthogonalRouter`、`VisualRouteDiagnostic`、`SceneView` | **已有代码与专项测试**；全局最优/所有无解分类不可宣称 |
| Group/Ungroup/Layer/ZOrder、基础 Symbol | `MTCEditSession.Visual.cs`、`StudioViewModel.Engineering.cs`、`StudioShell` | **工程与部分 UI 有基础**；定义/实例/混合锁定/剪贴板端到端不完整 |
| 复制/剪切/粘贴 | Core `PreviewDuplicate/Import`；MOL 有独立逻辑片段复制 | **可复用内核已存在**；图元 Studio 复制的合法选择/偏移/关系闭环待做 |
| A/B/C 三栏拟议界面 | 聊天中的设计示意图 | **设计目标，不是软件截图** |
| HMI/Vision/CT3D 全用户闭环和性能 | EP01～EP15、相关专业与 TestCenter 文档 | **尚未最终签收** |

**基线 CI 事实**：`602fa7fd` 编写时，Engineering Organization、Studio S1、Config L1、配置底座已成功，TestCenter 尚在执行；该 SHA 未列出同次 Runtime 工作流。本文件提交后只能依新 SHA 重新审计。CI 编译/单测与真实系统级鼠标注入测试是不同证据。

## 11. 开发顺序：复用优先、轻架构持续闭环

### P0：守住已有正确性（不再重复建设）

- 显示线/命中/编辑线同一路径、复杂避障失败诊断；
- 多选世界变换、锁定/隐藏/子父冲突、Esc/失焦/失捕以及单次 Undo；
- 所有 P0 回归随 P1/P2 继续跑，若回归坏先修复。

### P1-1：成熟基本图元编辑

`[优先]` **真正统一选择、吸附、命中与预览/提交一致性** → 完成对齐/均匀分布/智能参考线 → 基于 MEM Transfer 的复制剪贴板 → Shift/Alt/DPI、极小与镜像策略 → 编辑态快捷键、右键/上下文属性联动 → Bezier/Path 特殊几何控制柄。

### P1-2：Group/Symbol/Layer 编辑

进入/退出 Group、Group/Ungroup、Symbol 定义与实例覆盖、复制和重映射、跨层/锁隐、旋转嵌套、对象树定位与磁盘往返。专业参数通过现有 MPM Schema，不制造 Symbol 第二定义。

### P1-3：专业级 Visual Connection

用户编辑固定连接点/浮动附着/快捷克隆连接、多端口和正交/人工模式切换；多线路冲突优化及清晰诊断。先修路由的确定性与增量重算，再考虑更复杂启发式算法。

### P1-4：Studio 统一体验

工具箱、中央画布、属性/Schema、Ribbon、菜单与对象树共用命令语义；状态提示一致。优先从过大的 `SceneView` 中下沉可复用、无状态的几何/命中/交互规则，但**不为“架构好看”拆很多接口或推倒重写**。

### P1-5：完整 Windows UI 和性能

真实 WPF 鼠标 Down/Move/Up、Ctrl/Shift、多柄、端点/折点、Esc/失捕/异常回退；100%、125%、150%、200% DPI；Zoom 多档；100/500/1000 图元及高连接量。记录操作 P50/P95/P99、吞吐、路线重算、内存与对象/连接 ID；先建立实际基线，再依据设备等级制定门限，**不得编造已达性能数字**。

### P2：工业专业闭环

优先 E-B 电机 Symbol 双实例，并用 E-C 证明专业可复用、运行数据和授权不污染图元工程。专业实机门禁独立，软件测试不能替代 PLC/设备安全。

## 12. 验收矩阵（规范编号，可作为测试用例名追踪）

| ID | 用户必须真正做到的动作 | 主要反证/完整完成标准 |
|---|---|---|
| GE-01 | 画布悬停显示固定 × 与四向箭头 | A 不显示八点尺寸柄；鼠标离开清干净 |
| GE-02 | 选中显示八点尺寸柄/旋转柄 | B 旋转不发起连接，多个图元只一组共用柄 |
| GE-03 | Path/Bezier 节点与黄色专有参数柄 | 只有实际支持参数的 Shape 才有黄色柄；进入/退出几何态不改变无关对象 |
| GE-04 | 选择线后重连、加/拖/删折点 | C 不出现对象缩放柄；连接 ID 和端点保持正确 |
| GE-05 | 固定端口/浮动附着/快捷新建 | 三种语义与合法目标反馈清楚，非法不写工程 |
| GE-06 | 直线/正交/手动及自动失败提示 | 编辑可见线段；错误警告不能静默消失 |
| GE-07 | 单/多选世界变换，跨旋转 Group | 相对间距不漂移，整组锁定拒绝，单次 Undo |
| GE-08 | 网格/对象参考线一致 | Zoom/DPI 后显示与求解同源、预览与提交不跳动 |
| GE-09 | 对齐/分布/ZOrder/Lock/Layer | 一条命令一个事务、树/画布/属性一致 |
| GE-10 | Group/Symbol 编辑/实例覆盖 | 两实例独立、关联身份不丢、保存重开 |
| GE-11 | 复制/剪切/粘贴/Duplicate | 引用/端口/内部连接重映射，跨级策略与失败原子性 |
| GE-12 | Esc/失焦/捕获丢失/取消 | 不产生 Revision，UI 恢复无残留状态 |
| GE-13 | 真实鼠标/键盘/右键菜单完整链 | Windows WPF STA + 系统级输入注入及截图/操作记录 |
| GE-14 | 大场景 100/500/1000 与多 DPI | 记录 P50/P95/P99、内存、增量布线、正确性 |
| GE-15 | 电机双实例与专业 Viewer | 工程身份/运行分离/授权拒绝、专业独立真实验收 |

**每条验收证据的强制结构**：场景 + 用户步骤 + 源码 Owner/API + 预期与反证 + 对应测试名 + Windows Run URL + **同一 SHA** + 保存重开/Undo + 是否需要实鼠/HIL + 结论。不得用“类存在/按钮存在/历史旧 SHA 绿”填写 PASS。

### 签收判定

- **Developer PASS**：同一工程、正确统一 API、事务与连接关系可靠，异常拒绝可观测。
- **Studio PASS**：工程用户通过真实鼠标/键盘/属性面板自然完成 A/B/C，不输入底层 GUID/折点索引。
- **Project PASS**：对象身份、端口、连接、层级、Revision、Undo/Redo、保存重开、诊断一致。
- **Professional PASS**：至少 E-B 工业电机闭环，E-C 的专业边界不被破坏；真实设备安全另行签收。
- **Perf PASS**：设备配置及测量方法已固定，达经批准的门限并有可复现数据。
- **CI PASS**：提交 SHA 对应实际 workflow 成功；新 HEAD 须重新确认。

## 13. 防止“架构为了完成而塞代码”

以下方案默认拒绝：
1. 给 SceneView 再写一套世界坐标路由/几何数值算法；
2. 每个 Shape/每个 Professional 自带 Undo、Selection、Clipboard Graph；
3. 为浮动连接新建永久 `VisualEdge`、或把 `MOLLink` 冒充通用 Visual Connection；
4. 每添一个 Ribbon 按钮就复制一遍工程事务；
5. 为“完成功能数”空建 Interface/Manager/Registry 或新增项目；
6. 用固定矩形控制柄模拟连接点、用普通折点模拟 Geometry 参数柄；
7. 把新设计示意图叫成已运行 X3 截图；
8. 用 API/编译通过取代真实鼠标、保存重开、异常反证和专业验收。

**实施判断的固定顺序**：查现有 Owner/类型/测试 → 确认用户操作缺口 → 最小但完整地增强共性机制 → 在 Studio 贯通 → 保证 Preview=Commit → 真实 Windows 验证 → 必要的精简归档。允许合理合并和消除冗余，不因旧接口可调用就保留明显不合理的双实现。

## 14. 继续开发入口及维护约束

1. 进入仓库先读 `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md` 和 `DOCS/01～04` 的相应 Owner 规格。
2. 同时读本文（交互规范）、《#图元体系》（Owner/现状）、EP 主文档（上位需求）、A+B/ABC（真实历史审计）；按当前任务核对 ReferTo 相关源码。
3. 重新检查 `master` SHA、目标代码、当前 CI。当前项目直接在 `master`、少量中文 Commit、增改代码有准确中文注释；并行提交须做非强制更新与冲突核对。
4. 每轮报告“已有/新改/已实际跑过/未验收”，失效回滚与异常反证，不在本规范记成无据的功能百分比。
5. 本文维护交互合同与 GE-01～GE-15 验收规范；`#图元体系` 维护结构与 Owner；实施记录维护 SHA、测试和状态。**任何修改都不得制造两份互相矛盾的主规格。**

**本 V1.0 定位：给图元编辑一个清晰、完整、可落地的交互基线，并把前一聊天讨论成果真正融进仓库；不是宣告 GE-01～GE-15 已完成。**
