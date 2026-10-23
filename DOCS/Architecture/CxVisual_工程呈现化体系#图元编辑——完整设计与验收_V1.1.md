# CxVisual_工程呈现化体系#图元编辑——完整设计与验收 V1.1

> **定位**：CxVisual 通用图元编辑的**唯一交互设计与验收规格**。解决“是什么、如何出现、如何操作、什么数据改变、如何失败、怎样证明”。**不是源码完成证明，不是 Studio Shell 重新设计，不建立第二套工程/连接/运行/撤销模型。**
>
> **版本**：V1.1，2026-10-23；取代原《#图元编辑 V1.0》（旧版仅留在 Git 历史）。融合此前《CxVisual-架构（图元）B》、P0/P1 连续开发、draw.io 官方行为、V1.0 审查及当前源码。未取得的旧聊天文档原文不冒称逐字恢复。
>
> **阅读源码快照**：`oudream/cx-visual-x3` / `master` / `9a48bf1215d237fe1aa737d6c381bf7fb66e1fe6`；**此 SHA 是审查快照，不是本文提交 SHA，也不是最新 CI 的保证**。并行开发后按实际最新 master 重审。
>
> **权威上位与分工**：[《#图元体系——完整架构、类归属与验收现状》](./CxVisual_工程呈现化体系%23图元体系——完整架构类归属与验收现状_V1.0.md)维护 Owner 和实现现状；[工程呈现化总体规格](./CxVisual_工程呈现化体系——完整需求_角色期望与使用样例_总体详细设计_V1.0.md)维护 EP01～EP15；[工程组织 V1.2](./CxVisual_工程组织体系——完整架构设计与开发使用指南_V1.2.md)维护组织/引用/事务；[A/B/C 源码审计](../Implementation/CxVisual_工程可视化_ABC_源码与Windows验收审计_V1.0.md)和[工作包 A+B](../Implementation/CxVisual_工程可视化_工作包A+B_完整实现与真实验收.md)维护历史实证。本文件**不复制**类清单、CI 时间线、全部工程呈现化或专业运行规格。
>
> **读法**：`既有`=查到真实源码；`目标`=本规格规定的用户行为；`待验`=尚缺相应测试/真实操作证明；`待裁决`=跨 Owner 数据细节需按工程组织与真实代码批准，不能擅自作为已定 API。本文的 UX 参考图/文本图**不是现有 X3 软件截图**。

## 1. 总目标、边界及核心裁决

**最高原则：“各方面都尽能力展现，便捷友好尽支撑各专业达到目标。”**

一位软件开发者可用简单统一 API 完成图元、连接、布局、属性和事务；一位工程编排用户不需要输入内部 GUID/路径序号，在 Studio 中以鼠标、键盘、菜单及属性面板完成设计、保存、观察和调试定位。目标是成熟工业可视编辑，不是画几个矩形的 Demo。

### 1.1 五个不变量

1. **一套工程身份**：Project / Page / Content / Symbol / Group / Element、引用及对象地址属于 MEM/MRC；实例地址与定义 ID 不得混为一谈。UI 场景投影可重建，不另存 Graph。
2. **一套编辑与历史**：所有持久修改通过 MEM/MTC 原子事务，一次可确认动作至多一个 Undo 记录；悬停/选区高亮/临时预览/路由诊断/运行帧不得写 Revision。
3. **一套视觉连接身份**：端口和线由 `MPCPortKind.Visual`、`MPCPort`、`MPCConnection` 承担。MOL 的逻辑端口及 `MOLLink` 拥有独立专业语义，不能被通用视觉线替换。
4. **能力与类型分离**：基础 Shape 不派生“静态矩形/动态矩形”等重复类型；几何、参数、连接、样式、绑定、状态、动画、事件是不同能力维度，专业只扩展本职语义。Control Handle 是**能力的 UI 投影**，不是新工程对象。
5. **编辑态与运行态隔离**：Design 能授权修改定义；Preview/Run/Debug 的运行值和设备动作遵循 MDS/MOC/MRK/Professional 既有门禁。拖一个 Widget 不应发设备命令；未经授权的动作必须被拒绝。

### 1.2 Owner（只列调用边界，不重复类目录）

| Owner | 交给图元编辑的能力 | 不能做 |
|---|---|---|
| Foundation.Geometry / FGE | 几何、曲线、求交、路径、矩阵、包围界、命中与坐标算法 | 依赖 WPF/MEM/业务 |
| Mechanism.Core / MRC、MEM、MTC | 视觉对象、Group/Symbol、资源/引用/属性、工程操作、版本、复制转移与历史 | 让 View 成为持久化真源 |
| Mechanism.Core / MPC | 视觉端口、端点、连接 ID、路线模式、人工 Waypoint | 独立再建永久 VisualEdge/Graph |
| Mechanism.Visual / MVI | 工程到场景投影，编辑命令、变换、吸附、连接附着几何、路由/诊断 | 保存 WPF 控件或直接执行设备 |
| Mechanism.Presentation.Wpf / MPW | 画布渲染、命中反馈、DIP、鼠标捕获、预览、键盘输入 | 自己持久化 Undo、顶点/端口或专业数据 |
| Applications.Studio | 上下文命令、资源编辑器、对象树、属性/帮助/状态提示、工作区切换 | 复制 MVI 算法或改造工程模型 |
| Professional | HMI、MOL、Vision ROI、CT3D Viewer 等的专业编辑扩展/校验 | 再建一套公共选择/事务/剪贴板 |

真实代码入口：`MRCVisualItem.cs`、`MRCShapes.cs`、`MPCPort.cs`、`MEMEngineeringSession.Transfer.cs`、`MVIVisualEditor.cs`、`Editing/MVIVisualEditing.cs`、`MVIVisualAnchors.cs`、`MVIVisualSnapping.cs`、`MVISceneProjection.cs`、`SceneView.cs`、`StudioShell.cs`。若 API/Owner 随并行提交变化，以最新源码核准。

## 2. 图元对象、能力矩阵和六种标记

### 2.1 图元不等于“所有图元都有同一套操作”

| 对象族 | 整体几何变换 | 进入内部编辑后的专有能力 | 对连接/状态的约束 |
|---|---|---|---|
| Line | 位移、旋转、边界变换 | 两端点、长度/方向；不和“视觉连接线”混淆 | 普通几何线默认不是 `MPCConnection` |
| Rect | 八柄与旋转 | 圆角等仅在**对应形状参数确实存在**时显示专有控制柄 | 可连接性由能力/权限确定 |
| Circle / Ellipse | 八柄与旋转；Circle 保持圆约束 | 中心、半径/长短轴参数；Circle 是否允许非等比转 Ellipse 必须由类型合同明确 | 不用矩形 AABB 代替真实轮廓吸附 |
| Polyline / Polygon | 共性变换 | 顶点增加/移动/删除、开闭边界；Polygon 闭合语义不应丢 | 顶点与 Visual Connection 的 Waypoint 不同 |
| Arc | 共性变换 | 圆心、半径、起止角、方向/扫角，特殊参数柄 | 极短弧、整圆、跨角连续性须反证 |
| Bezier | 共性变换 | Anchor、Control1、Control2、二次/三次与精确分割 | 拖一柄不应无故移动其他锚点世界位置 |
| Path | 共性变换 | 多子路径、线段/曲线转换、闭合/断开、插删点 | 保留轮廓及命令的身份/顺序，不能只做包围盒命中 |
| Text | 布局/旋转/尺寸 | 文本内容、格式、编辑态光标/选择 | 编辑文本不等于移动图元；状态样式归 MPM |
| Image / SVG | 布局/旋转/尺寸 | 资源选择、裁剪/显示参数；SVG 使用既有安全解析 | 失联资源仅诊断/占位，禁止任意脚本外链 |
| Widget | 共性布局/选中 | 控件自己的焦点/输入/交互配置 | Design 操作与授权 Runtime 事件必须分离 |
| Group / Symbol | 组/实例整体变换、编辑范围 | 进入组内；Symbol 定义/引用/实例属性覆盖 | 不复制工程树，身份/绑定/连接随保存重开 |
| Visual Connector | **不是普通九 Shape 的缩放操作** | 两端重附着、固定/浮动策略、折点/模式/标签 | 一条 MPC 线身份，不能用 Shape.Resize 修改路线 |

**既有事实**：九种基础 Shape 已见 `MRCShapeKind` 等定义；`MVIVisualHandleKind` 已有 Anchor、Control1/2、Resize、Rotate、ArcCenter、ArcRadiusX/Y、ArcStart/End 等语义。**不据此宣称所有专有参数柄、Circle 圆约束及完整 Studio 鼠标操作均已完工。**

能力出现的判断顺序：对象是否存在 → 当前工程作用域是否可编辑 → 锁定/可见/可选 → 当前工具/编辑模式 → Shape 真正支持的参数 → 统一命中/绘制。尽量由已有 MRC 类型/几何能力、MPM Schema 与 MVI 规则派生，不预先增加空的 `IShapeCapabilityManager` 等工程体系。

### 2.2 六类控制标记：绝不混成一个柄

| 标记与视觉识别 | 出现时机 | 触发动作 | 修改内容 | 禁止误认 |
|---|---|---|---|---|
| **H1 八个蓝色圆点**（四角/四边） | B 选中·变换 | 拖动整体边界或多选共用外框 | MRC 尺寸/矩阵；一次 MTC 事务 | 不是固定端口 |
| **H2 右上环形箭头** | B 可旋转 | 旋转、角度吸附、可选中心点 | MRC 世界旋转映射到父局部 | 不是连接箭头 |
| **H3 四向蓝色快捷箭头** | A 悬停可连对象或专用连接工具 | 拖线、点击连接已有图元、选择/克隆并连接 | MPC 连接及必要的合法图元创建 | 不是缩放柄/持久端口 |
| **H4 轮廓附近小 ×** | A 悬停或“编辑连接点” | 从固定点拖出/吸附/增删改固定连接点 | MPC Visual Port 的固定附着定义 | 不是 H1；不是浮动接点 |
| **H5 黄色专有参数柄** | GeometryEdit 且 Shape 明确提供参数 | Arc 的圆心/角度、真实圆角/曲线参数等 | 形状定义内部几何参数 | 非每种 Shape 都有；不是普通折点 |
| **H6 连接线蓝点**（端点与 Waypoint） | C 选中线路径 | 端点重附着、折点插入/移动/删除 | 同一 MPCConnection 端点/Manual Waypoint | 不应拉伸关联图元 |

设计允许浅/深主题变化；颜色只是辅助，必须有图标形状、鼠标样式、悬停提示和键盘可达性。鼠标控制柄命中范围按**屏幕 DIP**而非固定世界单位计算，Zoom 和 DPI 只影响投影，不应改变操作含义。

### 2.3 参考产品对照（不等于 X3 现有实现）

- [draw.io 形状缩放与中心缩放](https://www.drawio.com/docs/manual/shapes/shape-resize/)：八个缩放控制点、保持比例与中心缩放；
- [draw.io 旋转](https://www.drawio.com/docs/manual/shapes/shape-rotate/)：右上旋转柄、输入角度；
- [draw.io 固定和浮动连接](https://www.drawio.com/docs/manual/connectors/connector-fixed-vs-floating/)：两端可分别选择固定或浮动；
- [draw.io 自定义连接点](https://www.drawio.com/docs/manual/shapes/shape-connection-points-customise/)：独立的连接点编辑；
- [draw.io 快捷连接与克隆](https://www.drawio.com/docs/manual/connectors/connect-shapes/)：方向箭头可连接已有对象或选择/克隆并连接；
- [draw.io 多选控制柄配置](https://www.drawio.com/docs/reference/configure-diagram-editor/)：多选共用控制柄属于**可配置**行为，不能称为所有 draw.io 实例默认都启用。

**CxVisual 的选择**：多选采用一组共用控制柄；当前已引入 Shift 等比、Alt 中心、Shift 旋转 15° 吸附及键盘微调，保留这些既有约定作为本版操作目标；统一 Studio 快捷键管理器若存在冲突再调整。官方 draw.io 使用的 Ctrl/Cmd 中心缩放不自动套用到 X3。

## 3. 交互规则：选择、悬停、工具、手势和工程模式分离

### 3.1 不建立“六套状态机”

以下四个维度**正交**而非互相覆盖：

1. **持久选择 Selection**：当前编辑资源作用域中的一或多个稳定对象地址；跨实例必须区分定义 ID 与 `MRIObjectAddress`。UI 的 SelectedIds 为只读/受控投影，不是第二工程对象。
2. **瞬时反馈 Hover**：光标下图元/连接/端口、合法连接目标、诊断高亮。鼠标移开即消失；可在“选中 A”同时悬停另一个 B，但**同一对象同一时刻只绘制符合优先级的控制标记**。
3. **当前工具 Tool / Scope**：选择、绘制 Shape、连接、GeometryEdit、ConnectorEdit、Group 内编辑等，允许返回上一个编辑上下文。
4. **活跃手势 Gesture**：Idle、Move、Resize、Rotate、GeometryHandle、Connect、Rewire、Waypoint、Marquee、Pan。**一次仅能持有一个鼠标捕获手势**，其余都是派生 UI 状态。

另有 Design / Preview / Run / Debug **上层工作模式**，负责决定是否可以变更工程及执行事件；不是又一套图元持久状态。实现优先复用 `SceneView` 当前的拖动标志和 MVI 交互边界，先集中互斥判定和 Cancel 行为，确有复杂性证据再抽取小型交互协作对象，**禁止为模式改造再建新项目/引擎**。

### 3.2 A/B/C 与 GeometryEdit 四个用户情境

| 情境 | 如何进入 | 只应展示 | 主要操作 | 退出/回退 |
|---|---|---|---|---|
| **A 悬停·连接** | 设计态悬停合法可连图元，或选连接工具 | H3/H4、可连目标/非法反馈；**不显示对象变换柄** | 拖固定点建固定连接；拖箭头向目标轮廓建浮动目标（目标）；点击箭头连接现有/克隆（目标） | MouseLeave、Esc、失捕、目标失效；未释放成功不写 Revision |
| **B 选中·变换** | 单击/框选/Ctrl-Shift 多选 | H1/H2、共用外框、大小/角度提示；有需要才显示对齐线 | 移动、等比/中心缩放、旋转、对齐/分布、Group | Esc 仅取消进行中的预览；完成动作提交一次 |
| **D 几何内部编辑** | 双击/上下文“编辑几何”且图元支持 | Anchor/Control/H5；不显示 H1/H3 | 插删锚点、调控制点、路径开闭/圆弧参数 | Esc/退出编辑范围；不污染对象变换态 |
| **C 选中·连线路径编辑** | 选择视觉连接线或“编辑线路径” | H6、线路模式/冲突诊断；不显示关联图元 H1 | 改起终点、插/拖/删折点、模式切换、线标签 | Esc/失捕回滚；线 ID 保持不变 |

> 三幅 A/B/C 参考图是**同一个 Studio 中央画布的三次独立截图预期**，不是在同一个矩形上同时叠加 H1～H6。需要保存独立高清设计图时由设计资产统一维护，并在本文件关联版本；不能将聊天示意图标成实际可运行 X3 截图。

### 3.3 命中与切换优先级

只在**适用当前 Tool 和当前编辑 Scope**的候选中比较，同一帧顺序按：

`已捕获 Gesture → 当前操作柄/线路端点/折点 → 当前选区控制柄 → 明确连接工具的固定点或方向箭头 → 几何实际轮廓/填充命中 → 空白画布`。

- 仅 **GeometryEdit** 时 H5/锚点优先于对象外框；仅 **ConnectorEdit** 时 H6 优先于线段插点；只在 **HoverConnect/连接工具** 时 H3/H4 可被按下。不要让“点击旋转柄”走到连接逻辑。
- 线端点与中间折点同坐标、两条连接线重叠、选中 Group 内孩子、Widget 焦点与鼠标捕获冲突时，使用明确的当前编辑对象/前后顺序/命中最近距离决策；相同距离必须**稳定确定**（不能因 Scene 枚举顺序不稳定每帧跳线）。
- 矩形包围盒可以作粗筛，Bezier/Arc/Path、旋转图元、连接线必须使用真正可见几何进行最终命中，不能把 AABB 粗筛当成完整命中。
- 命中允许屏幕 DIP 容差，须记录“鼠标点位置、Zoom、DPI、候选类别、最终选中 ID”，但不能写 MEM 事件历史。
- 编辑 Scope 变化（Page/Content/Group/Symbol 定义、切 Tab、关闭资源）、对象/端口被删除、Revision 在拖动间改变，需终止非法手势、清理预览并可定位提示；不可对旧页面/旧对象提交。

### 3.4 一次交互的强制合同

`PointerDown → 识别候选与权限 → Capture → PointerMove: 计算 World 预览 → PointerUp: 校验对象/版本/策略 → MVI → MEM/MTC 一次事务 → Scene 重新投影 → 选区/提示同步`

- **Preview=Commit**：预览的变换、吸附点、路由段、折点索引及候选端点，应和 MouseUp 提交使用同一计算合同、同一世界参考系。若最后指针或修饰键变化，先更新预览再提交，不能走隐藏的另一套算法。
- 取消：Esc、LostMouseCapture、窗口失活、Page/Tab 切换、对象失效、异常/权限拒绝，释放捕获，清除所有临时线/辅助线/虚框/变量；**不增 Revision**，不留孤立端口或半提交节点。若已成功提交，Esc 不能秘密撤销历史提交；需明确使用 Undo。
- 防止点击触发无意义写入：鼠标移动距离按 DIP 门限；吸附后为零位移也不得创建事务。右键菜单/键盘和鼠标要走同一个变更入口。
- 绘制/撤销/运行帧更新应在正确的 WPF UI 线程；重计算和 UI 帧频避免阻塞，但不能异步竞态把过时的路由/选区投影提交。

## 4. 坐标、变换、路径及对齐：一套可证明的算法合同

### 4.1 四种坐标身份

`Local Geometry → Parent/Group Local → World Scene → Screen DIP（再由 WPF 映射物理像素）`。

- 工程保存父局部变换；`MRCTransform.Compose/Inverse` 用于父子复合；MVI 的 `MVISceneItem.WorldTransform` 用于绘制/命中/吸附；SceneView 的 Zoom/Pan 仅控制 Screen DIP 映射。
- 目标对象的世界新矩阵 `W' = ΔWorld × W`，将其映射回父对象的局部矩阵 `Local' = Inverse(ParentWorld) × W'`（公式以项目矩阵乘法约定验证）；不得按某个旋转父 Group 的局部 X/Y 直接取整世界吸附值。
- 拖动起始和释放使用**同一个** Page/Resource、Zoom/Pan、MRC 对象身份及 MEM Revision 语义。跨定义或不可逆父变换拒绝，不凭 NaN/Infinity 继续提交。

### 4.2 选区变换、约束与锁定

| 功能 | 用户操作合同 | 负例 |
|---|---|---|
| 移动 | 单选/多选同一世界位移；保持成员相对布局 | 混入锁定/隐藏/不可编辑实例，整组拒绝 |
| 八点缩放 | 角点二维、边中点单轴；中心缩放可选、等比可选 | 零宽高、负尺度跨零、极短选区，不得暗中生成奇异/镜像矩阵 |
| 旋转 | 明确中心（默认选区中心）；Shift 吸附 15°；世界→局部稳定 | 重复使用原对象角度导致旋转叠加错误 |
| 多选变换 | **一组共用外框**；过滤父子重复选择、跨作用域混合选择 | 父与孩子同选不可重复变换，非法整组不产生部分提交 |
| 锁定/隐藏 | 与祖先权限、图层/容器策略叠加；命令禁用、反馈原因 | 单个非法目标不可被“跳过继续移动剩余项” |
| 键盘精调 | 当前实现入口为方向键 1 世界单位、Shift+方向键 10 单位；按用户命令提交 | 输入框正在输入/运行模式/焦点不在画布时不得误移动 |
| 镜像 | 默认不隐式镜像；若将来明确提供“水平/垂直翻转”单独命令再启用 | 鼠标越过相对缩放锚点不能无提示反相 |

同一用户动作应具有一致的错误码/提示；不得因为调用来自 Ribbon 或 SceneView，获得不同的编辑授权。

### 4.3 网格吸附、智能参考线和分布

**既有**：`MVIVisualSnapping.Resolve` 提供世界坐标网格/对象吸附和参考线结果；SceneView 预览使用这些结果。网格原点、间距和显示必须与计算一致；容差由 DIP/Zoom 转换。

目标规则：
- 单选与多选以**选区世界外框**为参考，分别评价左/中心/右及顶/中心/底；候选排除同选区、连接线及不可参与吸附的隐藏/锁定对象。移动轴独立求解。
- 同距离候选确定性：先最近距离，再按明确的对象层级/绘制次序/稳定 ID 排序；无合格对象参考线才使用网格。Ctrl/Alt 若有关闭吸附快捷操作，应由 Studio 统一定义，不能与中心缩放/专业工具冲突。
- 只显示被采用的辅助线，离开门限自动消失；执行键盘微调时不应意外跳到 25 单位网格。
- **Align**：左/右/上/下/水平中心/垂直中心基于可见世界范围；选择参考基准（边界、首项、最后项、Key Object）要统一，避免 UI 把平均中心当成“以主对象对齐”。
- **Distribute** 必须命名清楚：`均分左边缘坐标`、`均分中心坐标`、`形状边界等间距`不是同一公式。对排序后的不同宽度图元，边界等间距公式为 `gap=(rightmost.Right-leftmost.Left-sum(widths))/(n-1)`，保持两端对象位置，按各自宽度累加间距；若 gap 负值须预览重叠/拒绝或显式允许，不能悄悄冒称均匀间距。
- **源码差异必须公开**：`MVIVisualEditor.Align` 目前已有 left/top/centerX/centerY/distributeX，其中 `distributeX` 实现为**左边缘等距**；不能写“完整左右上下+等间距已完成”。完善右/下/纵向分布时优先扩展同一个 MVI 方法，不引入第二布局计算器。

### 4.4 精细 Geometry Edit

- Bezier Anchor/Control1/Control2、ArcCenter/Radius/Start/End、Path 多轮廓在局部几何定义中修改；被旋转/嵌套时指针由 World→Local 精确逆变换。曲线插点按参数精确分割，避免将曲线近似成多段后保存为错误模型。
- 对 Line/Polyline/Polygon，明确插点前后的命令索引、开闭合、共线与重复点清理；对 Path 多子路径，闭合指向**指定轮廓**，不能误把全部子路径闭合。
- 对 Circle/Rect 等专有参数，黄色柄由真实存在的参数才显示，拖动操作等价于 Schema 的该属性编辑；不另设看似可动而不能保存的 UI 参数。
- 几何点编辑过程中不同时暴露整体 Resize/Rotate。退出 GeometryEdit 返回普通选区，内部编辑前后选区/对象身份保持不变。
- 不允许要求用户在 UI 输入“路径命令索引第 N 项”才能删点；内部命令索引只出现在开发者 API/日志，UI 应通过真实命中与可见右键命令处理。

## 5. Visual Connection 完整行为及持久化合同

### 5.1 持久化身份、附着、路线、显示四层分离

| 层次 | 责任与真源 | 当前实现/目标 |
|---|---|---|
| **连接对象身份** | `MPCConnection.Id`、OwnerId、Source/Target `MPCEndpoint`；端点带对象地址和 PortId | **已有** |
| **端口身份和规则** | `MPCPortKind.Visual`、`MPCPort` 属于图元定义与实例寻址链，校验合法连接 | 四向 `visual.north/east/south/west` **已有** |
| **两端附着策略** | 每端独立 FixedPoint 或 FloatingOutline；固定点按端口附着，浮动从**真实轮廓**动态求解 | **目标/数据细节待裁决**：不能只存 MouseDrop 的世界坐标 |
| **路线与 Waypoint** | Straight / Orthogonal / Manual，手动 `MPCVisualWaypoint` 保存相对两端的约束 | **已有主要机制** |
| **显示/诊断** | MVI 投影出来的真实折线、路由碰撞/非法端点信息及 WPF 提示 | **已有基础**，复杂分类/协同优化待完成 |

**关键架构裁决**：浮动/固定属于同一 `MPCConnection` 的**每一端策略**，不是两类独立的 Connector 类。两端可分别固定/浮动；浮动轮廓锚点会随目标移动、缩放、旋转、路径路由变化而重新求解，连接 ID 不变。**不把 “floating” 定义成 PortId 为 Guid.Empty 的非法 MPCEndpoint**（现有构造器明确拒绝空 PortId）。

**模型扩展约束（跨 Core/MVI 待裁决）**：
1. 保持现有 `MPCConnection`、`MPCEndpoint` 身份。若需要在 MPCConnection 增加每端的附着参数，必须通过 Core Owner 和现有序列化/迁移/导入/Undo 链统一落位；不在 SceneView 偷存独立状态。
2. 固定点的合法描述（预置四向、用户自定义点，局部坐标/归一化坐标/参数化曲线位置）由同一个端口定义扩展，稳定 PortId；删除被连接点必须先提供**拒绝或显式重附着**策略。
3. 浮动端点仍需合法视觉端口引用；可考虑稳定的“轮廓附着入口”端口 + connection 端点策略，而不是拖一次新增一个匿名永久端口。**这只是优选方案，不是已经发布的具体类/字段名**。
4. 缺少附着策略字段的旧工程必须按**历史固定连接语义**加载；版本迁移、复制重映射、磁盘来回、撤销与失联诊断必须有独立测试。禁止迁移后老四向固定连线无提示改为浮动。
5. 对实例引用、Group 和嵌套世界矩阵的轮廓求交由 MVI 统一求解，真正数据引用还是 MPC。无法求得合法交点，明确回报原因、保持前一个已提交工程状态，不能凭最近的 AABB 中点假装成功。

### 5.2 A：快捷箭头与固定点的三个动作

1. **拖固定 × → 放在目标固定 ×**：建立双端固定连接，或根据明确选择转换单端策略；两端都进入合法性校验。
2. **拖方向箭头 → 放在目标轮廓**：目标浮动；如果初始动作取自四向固定点，则来源固定，另一端可浮动。不要因为是箭头就把最终目标也强制固定四向。
3. **单击快捷箭头**：目标方向存在可连接对象时自动尝试连接；无对象时展示轻量的图元候选，选择后“新建图元+建立连接”应是**同一 MEM 事务**；克隆并连接调用既有 Transfer/Clone。取消候选列表时不得新增节点或连线。

鼠标悬停小 ×、快捷箭头、现有连接端点在空间重合时，按当前 Tool/命中类别显示区别和悬停说明。隐藏/锁定、专业不许连接、已失效端口、引用跨无效作用域应禁用且清晰提示。

### 5.3 C：线端点与折点编辑

- **选线**显示两端 H6、已有折点和当前模式；不要给线一个八柄矩形外框。拖端点到另一个合法对象/端口，使用原连接的身份执行重附着；失败则端点原状保留。
- **插点**点击画布**正在显示的那一线段**；Editor 必须复用 `VisibleConnectionPoints` 及匹配索引，不能重新找一条简化正交路径导致插到别的段。重复端点、短线、共线、段重叠、正交转手动有明确归并规则。
- **人工点**插/移/删统一转换为 Manual 模式；从 Manual 改回 Orthogonal/Straight 时应提示人工点将失去编辑约束并允许 Undo，不能下一帧“悄悄消失”。
- **自定义固定连接点编辑**是“端口管理”上下文，不是正在修改连线 Waypoint；连接点的新增/拖动/删除需审计所有引用它的 MPCConnection。
- **标签/箭头样式**（若提供）作为同一连接对象的属性/呈现能力，支持居中/近源/近目标位置与随路由重投影；不得另造一个永久独立图形连接表。未有正式样式定义前标记为目标。
- 连接可在选中、重连、拖节点时给出端口类型/ID 的开发者级诊断，但工程用户看到的是“不能连接：端口已失效/方向不匹配”等文字，不要求输入 PortId。

### 5.4 路由、避障、失败与性能

| 模式 | 路径责任 | 守恒要求 |
|---|---|---|
| Straight | 当前两端的可见锚点连接 | 不假冒绕障 |
| Orthogonal | `MVIOrthogonalRouter` 基于端口方向和障碍尝试确定性寻路 | 不宣称一定无碰撞；碰撞必须有 `VisualRouteDiagnostic` |
| Manual | 端点 + 人工 Waypoint，在端点变动时按既有相对约束更新 | 不能被自动路由不提示覆盖 |

- 端口进出线方向优先，复杂旋转/缩放需根据世界法线；障碍保留安全间距，避免走过对象本体。自动路由 **P0 已有**碰撞诊断，不等于支持所有无解情境或多连线全局最优。
- 多连接协同优化依序处理**合法性 → 无碰撞 → 稳定性/确定性 → 交叉/平行通道 → 视觉美观**。应有控制最大迭代/超时/退化可诊断的机制，不能因少一次交叉引入大幅跳线或卡死 UI。
- 障碍移动/Zoom/隐藏、端点更新、Group 嵌套后，只有依赖受影响几何的连接需要重新投影；先用真实 100/500/1000 压力测量定位成本，再选择局部缓存/空间索引，不提前建立庞大 RouteManager。
- 路由的**渲染线段、命中线段、插点目标**必须同一个场景几何结果。路由警示/读取不修改 MEM Revision；解除障碍后只读诊断消失，连接 ID 和持久化数据仍保持。
- 与 MOL 共享的仅是纯几何和可能的 UI 手势；MOL 专业连接由 `MOLLink` 负责定义、合法性和 MOC 校验。

## 6. Group、Symbol、Layer 与剪贴板：统一工程操作

### 6.1 Group/嵌套编辑上下文

- **已有约束要写在 UI 帮助中**：`MTCEditSession.Visual.Group` 当前要求至少两个、**同父节点且同 Layer** 的图元；解组不能使绑定/声明依赖失效。跨父层组合并非已实现，可提供“先移动到同一父 Scope”引导，不用悄悄跨层重组造成视觉漂移。
- 组内进入/退出不是创建新工程树，而是改变当前选择与可编辑**导航范围**；双击 Group→进入、面包屑→返回、从资源对象树直接定位；Group 变换和孩子局部变换叠加后要保存重开无漂移。
- Group 外部选中优先选择整体，组内模式优先命中孩子；不允许同一操作同时选父与孩子两次。退出组内仍能定位原 Group，Esc 取消正在拖动而不是毫无反馈地丢弃导航上下文。
- Group 缩放时孩子是否同步缩放属于明确的行为选项：X3 当前实现必须按已存在的 MTC 世界变换测定，**不能把 draw.io 的可选“Resize Children”照搬为已实现**；如需新增配置，应通过统一属性/变换机制支持并验证保存迁移。

### 6.2 Symbol 的定义、引用与覆盖

- Symbol 由现有工程定义 + MRI 资源引用实例构成。定义编辑改变共享定义；实例编辑只改变允许覆盖的属性/绑定，不应在实例选择时偷偷修改共享定义。
- 两个实例同一 Symbol 定义，各自不同参数和 MDS 地址；从实例跳回定义再回到实例，保留 `MRIObjectAddress` 完整路径、选区与调试定位。改变定义后的实例刷新只做场景投影，无第二套资源副本。
- 非法循环引用、跨权限覆盖、解除引用、符号内部端口暴露/映射失败需明确拒绝/诊断，不因可视上画出来就称为运行可用。

### 6.3 Layer/ZOrder

- 图层策略：可见、可选、锁定的有效值由自身 + 祖先/所属 Layer（若当前工程模型支持）合并；隐藏对象不被误命中；锁定对象可按产品规则查看属性，但不能编辑；同层绘制前后顺序与对象树保持一致。
- BringFront/Back、Forward/Backward、跨层变更先确认父作用域是否合法，变更走 MTC；连接渲染次序可按投影处理，但连接身份/端点不更换。
- 目前 `MVIVisualEditor.SetLayer/SetZOrder` 是基础操作；**完整 Layer 管理 UI、合成锁定语义、跨层迁移与树同步待验**。

### 6.4 复制、剪切、粘贴、Duplicate：复用 MEM Transfer

**既有内核**：`MEMEngineeringSession.PreviewDuplicate`、`Import`、`Duplicate`、`MTITransferPlan` 已定义身份重映射和工程复制入口。Studio 的图元剪贴板只是**暂存用户选区/来源项目/目标落点**，不产生独立永久复制模型；MOL 逻辑片段剪贴板不等于 Visual Clipboard。

目标原子操作：
1. **Copy**：仅记录可复制根对象（自动消除父子重复选中）、来源工程/资源/版本、原始世界外框及必要依赖闭包；只读，不产生 Revision；
2. **Paste Preview**：在合法目标 Parent/Layer 下创建 `MTITransferPlan` 候选，显示偏移位置与要保留/断开/重附着的外部引用；不可编辑/版本失配不写工程；
3. **Paste Commit**：同一事务创建完整对象树、内部端口/连接/引用、坐标换算和必要样式/绑定依赖，新 ID 重映射无重复，Undo 一次撤回全部；
4. **Duplicate**：当前作用域默认采用可见偏移并保留源，走相同 Transfer 链；
5. **Cut**：不得“先删除，再粘贴失败”。同会话可在合法事务内转移/剪切；跨工程无法保证原子时，先成功完成目的端再由明确确认删除来源，或明确不支持并提示；
6. **Boundary Connections**：内部两端都在复制闭包的线复制并重映射；只选一端的边界连接应有明确 `Pin/Disconnect/IncludeClosure` 策略，不悄悄连回不期望的原对象；
7. **粘贴防错**：源工程关闭、跨版本、无效外部资源、受保护父 Scope、锁定/失效对象、粘贴位置不可表示，拒绝并保持原工程与剪贴板可再次使用。

粘贴落点、Offset、Scene 预览由 MVI 计算；ID/引用映射与序列化由 Core 主导。不要逐属性 `new Shape` 来逃避完整工程复制与历史。

## 7. Studio 统一交互：成熟但不臃肿

### 7.1 与 Studio Shell 的分工

按批准的 Studio Shell 统一布局：**九个一级菜单、上下文 Ribbon、左资源树/当前资源对象树与工具箱、中央 Page 单画布、右 MPM Schema 属性/样式/绑定/状态/事件、底部诊断与状态栏**；支持浅色 TestCenter 白与 VS 深灰主题。配置资源可以是 1×N / 2×N / 3×N 中央配置组合面板，图元/流程编辑则各自单画布；**本规格不重新定义全局 Docking/停靠系统**。

设计示意三幅图必须分别表达：
- **A｜HoverConnect**：图元周围展示小 × 固定点和四向箭头；中央画布其它图元保持普通样式；
- **B｜SelectionTransform**：一对象或多选共用外框的八点+旋转；无需同时显示四向连接提示；
- **C｜ConnectorEdit**：有至少两图元与一条有折点的 MPC Visual Connection；只突出线端点、折点及路径警告；
- 图 A/B/C 的右属性面板随当前选择/工具不同显示有效控件；状态栏显示实际坐标、Zoom、编辑作用域。**图为设计目标，不得打上“X3 已实现”字样**。

### 7.2 工具箱、对象树、属性面板

- 工具箱分类 Shape/Text/Image/SVG/Widget/Group/Symbol/Connection，由既有 Shape Catalog/资源类型生成，不为每一种类型独立写死一套编辑状态。
- 左资源树**不混入所有 Group/Element**；打开具体 Page/Content 后由当前资源对象树展示 Group/Element、层次与连接，支持树选↔画布选双向同步。多实例对象要以完整地址区分。
- 未选显示画布/页面属性；单选显示对应 MPM Schema；多选显示共同字段与混合值；线路显示端点、模式、折点与诊断；Symbol 实例显示允许覆盖的属性并可跳转定义。右侧字段不可要求用户手填 GUID、连接索引或端口 MemberName。
- 异常提示“短提示 + 详情诊断 + 可定位对象”，只读错误反馈不污染工程。自动路由冲突用颜色/线型/文本多重提示，不能只靠红色。
- Design 时 Widget 通常由画布编辑手势接管；需要真实控件交互时明确切换预览/运行/专门的交互试用方式，设备动作仍受授权。

### 7.3 统一命令语义与快捷键

| 意图 | 默认入口与行为 | 约束 |
|---|---|---|
| Select/多选/框选 | 单击、Ctrl/Shift、空白框选 | 统一选区/当前 Scope；编辑框/嵌套命中不能漂移 |
| Move/Nudge/Resize/Rotate | 拖动、方向键、H1/H2、属性数值 | 同一个 MVI 变换和 MTC 事务 |
| Delete/Undo/Redo | 键盘、菜单、Ribbon | 不在控件层另外维护撤销历史 |
| Copy/Cut/Paste/Duplicate | 快捷键、右键、Ribbon（**目标**） | MEM Transfer、引用与 ID 重映射 |
| Align/Distribute/ZOrder/Layer | Ribbon/右键/属性 | 与 MVI 同一计算合同、共享可执行条件 |
| Group/Ungroup/Edit Group | 对象树、双击、Ribbon、右键 | 父 Scope、同层与编辑权限前置检查 |
| GeometryEdit/ConnectionEdit | 双击、上下文命令、明确工具 | 当前显示 H5 或 H6，不混用 H1/H3 |
| Connect/CloneConnect | 悬停箭头、固定点、连接工具 | 同一 MPC 连接语义；用户取消不生成工程 |
| Save/Open/定位 | Studio 原有入口 | 无效对象、跨 Page、Dirty 和 Undo 一致 |

**不得把上表当成所有快捷键已实现。** 新命令先检索 `StudioShell` 注册命令、键盘路由和 WPF 事件，再统一贡献 Enabled/ToolTip/Execute/Error；不要在 `SceneView`、`StudioViewModel`、`MainWindow` 各复制一份编辑业务逻辑。快捷键冲突由 Studio 按焦点/当前编辑器/Tool 优先级解决，输入框优先接收文本方向键。

## 8. 开发者和工程用户的完整使用路径

### 8.1 软件开发者：由真实 API 形成最少自然调用

**已有具体入口**（需以最新源文件核签，非凭空设计 API）：
- `MEMEngineering.Create`、`MEMEngineeringSession.AddPage/AddRectangle` 等建立 Project/Page/Shape；
- `MVIVisualEditor.SetPage/Select/Move/MoveSelection/TransformSelection/Align/ConnectVisual/SetVisualRoute/InsertVisualWaypoint/MoveVisualWaypoint/RemoveVisualWaypoint/ReconnectVisual`；
- `MVIVisualSnapping.Resolve` 提供与工程提交解耦的纯计算；
- `MEMEngineeringSession.Group/Ungroup/PreviewDuplicate/Import/Duplicate` 复用工程定义、ID/引用映射和原子历史。

开发者完整行为：创建页面/图元 → 获取 Scene 与对象地址 → 按世界位置发起选区编辑 → 连接图元并读取路由诊断 → 一次事务批量修改 → Undo/Redo → 保存/重开 → 验证关系。真正示例位置为 `Examples/VisualAuthoring/EngineeringVisualQuickStart.cs`；不得添加不存在的 `AddFloatingVisualPort(...)` 示例来伪装当前已有。

### 8.2 工程编排用户：不看源码也能完成 A/B/C

`新建/打开工程 → 打开 Page → 工具箱拖图元 → A 悬停选择固定/浮动/快捷连接 → B 单选多选和几何专有参数 → Group/Symbol/Layer/对齐/剪贴板 → C 选线折点与端点 → 编辑属性和绑定 → Undo/Redo → 保存重开 → 查看诊断`。

用户点击位置和当前图元决定编辑对象，**绝不要求输入内部 Guid、MemberId、折点下标来做基本操作**。若某命令只在 API 中可用而无 Studio 路径，应明确记录为“开发者可用，Studio 未签收”。

### 8.3 真实工业代表性扩展

- **E-A 图元场景（首个完整签收）**：九 Shape、Text/Image/SVG、Widget、嵌套 Group、两条固定/浮动混合连接、贝塞尔、自动避障、对象树与属性面板；重复三次 A/B/C，完成 Cancel/Undo/Redo/SaveOpen。
- **E-B HMI 电机 Symbol 双实例**：用户建立电机 Symbol、两个实例、不同 MDS 绑定/状态/动画、联动权限；运行态设备动作未授权拒绝且 Revision 不变，进入定义编辑返回实例身份正确。MDS 与现场设备 Owner 仍按上位体系独立验收。
- **E-C MOL/Vision/CT3D**：MOL 流程线保持 MOLLink；Vision ROI、CT3D Overlay/MPR/标注按专业 Viewer 自有坐标、数据和 SourceMap 处理，**复用**通用选择/变换/命令/工程事务，但不把 2D 画布当作 CT3D 体坐标编辑器。

## 9. 质量、验收矩阵与签收门禁

### 9.1 不改变原 GE-01～GE-15 的稳定编号

| 编号 | 用户操作正例（必须由真正用户动作触发） | 失败反证与工程不变量 | 证据层级 |
|---|---|---|---|
| **GE-01** | A 悬停合法图元显示 H3/H4，离开消失 | H1/H2 不误显示；不可连/隐藏/锁定不得开始连接；Revision 不变 | WPF STA + 实鼠 |
| **GE-02** | B 单选/多选显示八柄旋转柄并完成缩放/旋转 | 点击旋转不得建线；多个图元仅一组共用柄；取消不改 Revision | Core + STA + 实鼠 |
| **GE-03** | D Arc/Bezier/Path 控制柄、插删节点/参数变更 | 不支持的 Shape 无假黄色柄；退化路径可恢复，取消不变更无关点 | FGE/MVI + WPF |
| **GE-04** | C 拖连接端点、插/移/删折点及 Undo/Redo | 线 ID 不变，UI 编辑的必须是可见线段，非法重附着不写入 | Core + STA + 实鼠 |
| **GE-05** | 固定/浮动两端混接、自定义点、快捷连接/克隆 | 固定点不浮动；旧四向固定迁移不变；取消不生成端口/图元 | Core 迁移 + 实鼠 |
| **GE-06** | Straight/Orthogonal/Manual/避障冲突告警及恢复 | 无解不假装成功，修障后诊断自动消失且不增 Revision | 路由 Core + STA |
| **GE-07** | 多选跨旋转 Group 的世界变换、等比/中心/角度吸附 | 父子重复选择/混锁/零尺寸全部拒绝；单次 Undo | Core + WPF |
| **GE-08** | Zoom/Pan/DPI 改变后网格、参考线、预览、提交吻合 | 吸附点不跳动，6DIP 等容差屏幕尺度一致；纯预览不写工程 | Core + 多 DPI 实鼠 |
| **GE-09** | Left/Right/Top/Bottom/Center、水平/垂直真正等间距、Layer/ZOrder | 不同宽度间距正确、图层拒绝不半提交、对象树同序 | Core + Studio |
| **GE-10** | Group 进入/退出、Ungroup、Symbol 定义及两实例覆盖 | 同父层约束、嵌套变换、实例地址、保存重开和引用一致 | Core + Studio |
| **GE-11** | Copy/Cut/Paste/Duplicate 含组、端口与内部连接 | ID 唯一、边界引用策略确定，失败原子回滚、一次 Undo | MEM Transfer + Studio |
| **GE-12** | Esc、LostCapture、WindowLostFocus、Tab/Resource 切换 | 无影子预览、无鼠标捕获残留、Revision/连接身份未变 | WPF STA + 系统注入 |
| **GE-13** | 工具箱→A/B/C→右键/快捷键/属性→保存重开 | 不能靠直接触发私有 handler 或手写 GUID 代替真实交互 | 系统级键鼠 + 操作录像 |
| **GE-14** | 100/500/1000 图元及多线，Zoom/DPI/缩放/拖动/重连 | 记录 P50/P95/P99 操作时延、峰值内存、重算数量、错误/身份漂移 | Windows 性能压测 |
| **GE-15** | E-B HMI 电机双实例+E-C MOL/Vision/CT3D 专业协同 | 未授权设备不动作、Runtime 值不改 MEM、MOLLink 不被 MPC 取代 | 跨体系 + 实机安全 |

**GE-01～15 每项必须有**：测试名称 → 对应 Owner/源码 → Studio 具体操作步骤 → 正例结果 → 失败/取消反证 → Undo/Redo → Save/Open（适用时）→ Windows workflow URL/同一 SHA → 测试环境 → 未验证事实。**不得仅填写“界面有按钮 / 已提交 / 老版本 CI 绿”作为 PASS。**

### 9.2 可复现的 A/B/C 完整交互链

**A｜固定/浮动/快捷连接**
1. 选择矩形源与目标，悬停源，验证 H3/H4 位置与提示；
2. 从固定点拖到目标固定点，保存一条连接，读两端合法 MPCPort；
3. 另一条从方向箭头拖至目标轮廓，验证“固定/浮动”两端策略及形状重定位后附着变化（未实现则标待验，不允许冒充通过）；
4. 单击快捷箭头选择/克隆新图元，确认新增对象和连线仅一次 Undo；
5. 对非法目标、Esc、失捕、删除端口分别反证无半连接。

**B｜选中/变换/几何编辑**
1. 单选 Rect，八个控制柄/旋转柄；Ctrl/Shift 多选两图元，变为一组共同控制柄；
2. 在旋转 Group 内拖动和对齐、Shift 等比、Alt 中心、Shift 旋转吸附，检查预览=提交；
3. 双击 Bezier/Path 进入 D，拖真实几何节点，退出后恢复 B，检查其他子路径和链接不受损；
4. 含锁定成员/父子同选/极小尺寸/失效源时拒绝整组，Undo/Redo 和磁盘往返一致；
5. 带焦点 TextBox 按方向键只移动光标，不误移动工程元素。

**C｜连线端点和路径**
1. 选择 MPC 视觉连接线，出现 H6、不出现图元 H1；切换 Straight/Orthogonal；
2. 放置多障碍，复现可见路由；在用户点击的那条线段插点，随后移动/删除 Waypoint；
3. 拖动端点重附着至另一个合法视觉端口，连接 ID 与外部引用保持；
4. 堵死路由，检测警告线和诊断；移开障碍后警告消失且纯投影不改 Revision；
5. Esc/失捕/错误恢复不造成半连、断连或无意义 Undo。

### 9.3 Windows、多 DPI、性能与工业边界

- **功能自动化**：现有 `Tests/ArchitectureSmoke/Visual/Editing/VisualShapeEditingTests.cs`、`Tests/ArchitectureSmoke/Engineering/Studio/OrganizationWpfTests.cs` 是真实回归入口；需要补系统级鼠标 Down/Move/Up + Capture、上下文菜单/键盘焦点覆盖，STA 直接调用 handler **不能冒充外部实鼠注入**。
- **显示测量**：Zoom 多档，100%/125%/150%/200% DPI、混合屏幕与不同主题；验证 H1～H6 的命中 DIP 容差、世界点一致、失焦/切窗与坐标稳定。
- **性能测量**：固定 CPU/GPU/RAM/系统配置、工程构造方式、图元/连接密度和绘制模式，记录吞吐、P50/P95/P99（含首次/热缓存）、GC/托管+非托管内存、路由重算范围、UI 帧停顿与异常。**先建立真实基线和验收环境，再批准具体阈值，不虚构毫秒或内存合格数字**。
- **项目一致性**：同一工程对象/Port/Connection ID，复制后新 ID 可映射，SourceMap/运行追踪可回溯；资源重开无路径漂移、Undo/Redo 一步一动作、异常中无未提交污染。
- **工业安全**：设计预览和 Studio 触发的设备命令必须走实际权限/联锁/模拟通道，未经授权不向 PLC/设备下发；软件 CI Success 不能取代实机安全验证。
- **版本证据**：只有 SHA 对应真实触发的 Workflow 的完成结果可签收；未触发 = 未验，该 SHA 之后的并行 HEAD 必须另行核对。

### 9.4 六种正式签收门槛（不可互相替代）

**Developer**：单一自然 API / Owner 正确。  
**Studio**：用户真实键鼠、对象树、属性/Ribbon 低摩擦操作。  
**Engineering**：MEM 身份、引用、事务、保存重开、异常回滚。  
**WPF**：真实鼠标焦点与 DPI、截图/录屏及无残留操作状态。  
**Performance**：规模测试及批准的阈值，有复现日志。  
**Professional**：电机双实例、跨专业编辑复用、授权运行与真实设备安全独立签收。

## 附录 A｜事实与目标边界：避免为“完成”塞代码

| 能力 | 审查快照的实际状态 | **不能据此宣称** |
|---|---|---|
| 九 Shape、Geometry/Bezier/Arc/Path、变换 | 已见 MRC/FGE/MVI 核心源码与测试 | 所有特殊几何参数柄、所有极端路径已完成 |
| 多选、旋转/缩放、网格及对象吸附 | 已见 `MVISelectionTransform`、`MVIVisualSnapping`、`SceneView` 等 | 全实鼠/所有 DPI、零宽高/镜像/混组都完成 |
| MPC 四向固定点、路径插删、端点重连、避障诊断 | 已见 `MVIVisualAnchors`、`MVIVisualEditor`、`VisualRouteDiagnostic` | 浮动策略、任意自定义固定点、多连接全局优化已存在 |
| Group/Ungroup、Symbol 基础与 MEM Duplicate/Import | Core 已有结构及事务/API，Studio 部分入口存在 | 当前可对任意跨父/跨 Layer 组进行合法操作，或全套剪贴板已完成 |
| Studio 工具箱/Ribbon/对象树及部分 WPF STA | 部分真实源码与测试 | GE-01～GE-15 全部已通过真实系统鼠标操作 |
| HMI 双实例与专业流程、运行数据 | 有专门 A/B/C 和 EP 文档/部分软件回归 | 实机控制与完整 P2 已签收 |

后续最小合理开发顺序：**(1) 互斥与取消/预览一致性 → (2) 完整连接附着的 Core+MVI 合同 → (3) Group/Symbol 与 Transfer 跨实例闭环 → (4) Studio 统一命令/属性 → (5) 多 DPI、系统级实鼠和性能 → (6) 专业/设备独立验收**。已有实现的单元只补反证，不重复做一份。

禁止方案：WPF 新持久化 Graph；另建 Undo/Selection/Port/Runtime 仓库；浮动连线用无 ID 临时点冒充工程事实；每 Shape 专门建一套编辑器；每个 Ribbon 入口复制同一事务；为满足接口数量空造 Manager/Registry；以截图证明可运行/以代码编译证明现场设备安全。

## 附录 B｜来源、Owner 导航、维护方式

1. 主规范：`DOCS/01～04`、`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md`、`AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`。
2. 本文件：**图元编辑 UX/行为/验收唯一规格**。与《#图元体系》协作，后者归属/源码现状，避免互抄状态、流水账与 API 目录。
3. 最新工程组织、MPC 连接、Studio Shell、MOL/MOC/MRK/MDS 文档的 Owner 决议优先；新增端点附着字段必须先与现有 Core Owner/序列化定案。
4. 不按文件创建新层级：优先修改现有 FGE/MRC/MVI/MPW/Studio 和 Tests；改动有中文注释（职责、坐标、原理、边界与失败行为）。直接 `master`、中文有意义提交、并行提交防冲突，一轮一提交优先。
5. 历史 P0/P1 代码 SHA、Windows CI、逐轮修复应只记录在 Implementation/Testing/GitHub，不在本设计规格反复更新“今日已完成”；设计修订请标明哪些是**新增裁决**、哪些仍为**待裁决**。
6. 参考 draw.io 仅用于可复现交互习惯；**引用并不改变 CxVisual 的工程、MPC、MOL、工业权限结构**。示意图若需附仓，应存于批准的 `DOCS/界面设计/` 并标“概念设计/非 X3 实机”，本文保持相对引用。

**V1.1 签署范围**：完成完整、可执行的图元编辑设计与 GE-01～GE-15 验收规格；**不签署尚未发生的 Core 开发、真实 Windows 测试、全 P1/P2 或实机运行结果**。
