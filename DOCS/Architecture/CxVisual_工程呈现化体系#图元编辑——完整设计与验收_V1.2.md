# CxVisual_工程呈现化体系#图元编辑——完整设计与验收 V1.2

> **状态：设计与验收权威规格。** 本文完成图元编辑的目标、职责、使用路径、关键算法/操作合同、失败边界及可执行验收定义；不等于全部功能已经开发、CI 通过或现场设备获准控制。
>
> **日期：2026-10-23。** 承接《CxVisual-架构（图元）A/B》、此前 P0/P1 源码开发及 V1.1 审查成果，继承 GE-01～GE-15 稳定编号。**源码阅读快照**：`cx-visual-x3/master`，当前具体 SHA 以实施审计为准，禁止将 V1.2 当成动态 CI 台账。
>
> **总原则**：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**
>
> **文档权威分工**：《#图元编辑 V1.2》（本文）唯一负责**图元的交互、几何编辑与验收合同**；[《#图元体系——类归属与验收现状》](./CxVisual_工程呈现化体系%23图元体系——完整架构类归属与验收现状_V1.0.md)负责 Owner/源码成熟度；[工程呈现化总体详细设计](./CxVisual_工程呈现化体系——完整需求_角色期望与使用样例_总体详细设计_V1.0.md)负责 EP01～EP15；[工程组织 V1.2](./CxVisual_工程组织体系——完整架构设计与开发使用指南_V1.2.md)负责 Project/Page/Content/Symbol/Group/Element 和引用生命周期；[A/B/C 源码审计](../Implementation/CxVisual_工程可视化_ABC_源码与Windows验收审计_V1.0.md)负责历史实证。Studio Shell、MOL/MOC/MRK/MDS 等相邻体系仍以各自最新正式规范为准。
>
> **阅读约定**：〔既有源码〕表示真实入口可查；〔已有测试〕表示有相应测试案例而非全面交付；〔设计目标〕表示尚需实现/接入；〔待裁决〕表示跨 Owner 存储/兼容细节需批准后改变代码。所有 UX 图标和模拟图只用于说明，不是已运行 X3 截图。

## 总览图与阅读路径

![CxVisual 图元编辑核心操作总览：A悬停连接、B选中变换、C连线路径、D几何精细、E Group/Symbol/Layer、F属性与命令](./CxVisual_工程呈现化体系%23图元编辑_核心操作总览.svg)

> **图 0-1（设计总览图）**：图中 A～F 是**同一编辑器中可以切换的不同操作情境或功能区域**，不是把所有控制柄同时叠放在一张被选图元上。另提供同名专题高清 PNG《CxVisual_工程呈现化体系#图元编辑.png》（聊天交付资产，1491×1055）；设计图均不得冒称软件实机截图。A 连接参看第 5、8 章；B 变换参看第 5、7 章；C 连线路径参看第 8 章；D 曲线参看第 6 章；E 工程组织参看第 9 章；F 统一操作参看第 10～12 章。

**14 章 / 4 附录阅读顺序**：
- **基础定义（01～04）**：目标/Owner → 对象能力 → 几何坐标与场景 → 呈现性能；
- **编辑核心（05～08）**：交互与控制柄 → 九 Shape 精细编辑 → 世界变换与布局 → MPC 连接；
- **统一使用（09～12）**：Group/Symbol/Layer/Clipboard → 属性与动态能力 → MEM 命令事务 → Studio 用户体验；
- **证明闭环（13～14）**：开发者/专业使用 → GE-01～15 真正可复现的验收；
- **附录 A～D**：能力总矩阵、输入/状态判定表、源代码 API 边界、需求/用例/验收追溯。

# 第一部分｜对象、几何与场景

## 01. 目标、范围、边界与架构原则

### 1.1 两种使用者的完整期望

**软件开发者**期望：通过一套自然 API 创建图元和页面，描述样式/资源/绑定，完成组合、连接、路由、批量修改、Undo/Redo 与持久化，无需掌握 WPF 内部数据结构；任意失败都明确、原子且可诊断。

**工程编排用户**期望：打开 Studio 工程后，用工具箱拖入图元，在同一 Page 的画布中通过鼠标、快捷键、右键、Ribbon 和 Schema 面板直观编辑；在 A 悬停连接、B 选择变换、C 连线路径、D 曲线编辑之间自然切换；需要进入 Group/Symbol 定义时有明确导航与返回；完整操作无需手工填写 GUID、端口 MemberName、Waypoint 索引或代码。

**验收主体**：用户能完成完整的创建 → 编辑 → 组合 → 连接 → 复制 → 保存重开 → Undo/Redo → 专业绑定 → 运行观察/调试定位。代码提交、窗口截图和单个绿灯均不能替代全部链路。

### 1.2 强制不变量与职责

1. **唯一工程真源**：MEM/MRC 中的 Project/Page/Content/Symbol/Group/Element、端口和连接保持对象身份；MVI Scene 只是随工程和运行状态重新投影的只读表象，WPF 不是第二工程模型。
2. **统一修改链**：`UserIntent → MVI 校验/求解 → MEM/MTC 原子 Edit → Revision/Undo → 重投影`；Hover、Preview、Guide、诊断及 MDS 运行帧不得修改工程 Revision。
3. **统一视觉连接**：`MPCPortKind.Visual`、`MPCPort`、`MPCConnection` 唯一承担视觉连接身份，`MOLLink` 继续承担专业逻辑流程语义，互不冒用。
4. **Foundation 不依赖机制/专业；Mechanism 不依赖具体 Professional**；MRC 描述数据，FGE 负责纯几何，MVI 提供交互编辑，MPW 只提供呈现与输入。
5. **专业扩展正交**：专业可以提供“能否编辑、如何呈现专业属性、专业工具反馈及运行授权”，不得独自重写公共选择/Undo/Clipboard/路由模型。
6. **已有体系先用尽**：不为每一种 Shape 新建 Editor、Manager、CommandBus 或独立项目；有确凿测试需求时，才将稳定可复用算法下沉至已有项目。

| Owner | 图元编辑使用 | 不负责 |
|---|---|---|
| Foundation.Geometry / FGE | 点线面/路径/Bezier/Arc、矩阵、求交、容差、几何命中、插删分割 | WPF、工程持久化、设备 |
| Mechanism.Core / MRC + MPM | 视觉定义、Shape、资源、样式、参数、声明、实例属性/绑定 | View 层临时控制柄 |
| Mechanism.Core / MEM + MTC | 唯一工程树、对象地址、事务、权限、Undo、复制/导入、保存 | 重新造 Canvas 历史 |
| Mechanism.Core / MPC | Visual Port/Endpoint/Connection、Route/Waypoint、引用身份 | 定义 MOL 执行连线 |
| Mechanism.Visual / MVI | 投影、选择/命中规则、变换、吸附、路由、操作意图/诊断 | 专业设备命令、WPF 控件持久化 |
| Mechanism.Presentation.Wpf | DIP 显示、输入映射、捕获、绘制/预览/辅助线、焦点 | 第二次计算不同的工程几何 |
| Applications.Studio | Shell 入口、工作台上下文、Schema、Ribbon、命令、树与画布联动 | 分叉的 Core Edit 算法 |
| Professional | HMI/MOL/Vision/CT3D 的专业约束/Viewer 操作 | 复制共性图元与历史体系 |

### 1.3 需求边界及冲突处理

- EP02/03 是图元核心，EP04/05/06/07/08 仅在图元编辑调用的交界处定义合同。完整 Schema、运行绑定、工业流程、CT3D 体渲染和 Studio Docking 不是本文重设计对象。
- 修改涉及 MPC 端点序列化、MEM Transfer 引用或 Symbol 定义时，需先查看权威 Owner 与真实源码/兼容测试；文中的备选字段并非自动批准新增的公共 API。
- 功能完成必须同时具有：核心 API、Studio 操作、保存重开、反证、相应 Windows 测试证据；专业设备安全另行验证。

## 02. 图元对象与完整能力体系

### 2.1 组织结构与图元能力正交

- **Shape** 是几何定义，不应把“旋转的矩形”“带动画的矩形”提升为全新工程对象类型；
- **Element/VisualItem** 是实际可编辑的工程图元，负责可见、可选、锁定、层级/位置、引用；
- **Group** 是 `MRCVisualItem` 系的容器（**不是** `MRCElement` 的子类），复用统一变换；
- **Symbol** 是可复用工程定义；Instance 是稳定资源引用及允许覆盖的属性/绑定，不制造定义副本；
- **Layer/ZOrder** 是同一工程中的显示/编辑组织关系，不能混淆 Layer 与 Parent/Content 身份；
- **Visual Connector** 属于 MPC 连接图，其投影可作为 SceneItem，但不能拿普通 Shape Resize 编辑端点。

适合采用**操作能力视图**而不是机械地给所有类型加接口：`CanTransform / CanEditGeometry / CanConnect / CanEditContent / CanOverride / CanReceiveEvent` 由真实类型、已有 Schema、当前 Tool、授权和编辑 Scope 派生；同一能力对不同类型调用既有通用入口，不强制新建公共能力接口。

### 2.2 九种基本 Shape 及其它可视对象

| 对象 | 常用操作 | 精细操作 | 类型特有约束/反证 |
|---|---|---|---|
| Line | 创建、端点布局、移动、旋转 | 几何线端点 | 普通 Line ≠ MPC Visual Connector |
| Rect | 八柄、旋转、尺寸/边框 | 支持时编辑圆角等真实参数 | 无参数不能显示可拖黄色柄 |
| Circle | 创建、移动、统一尺度 | 圆心/半径 | Circle 保圆；不等比行为（拒绝或转 Ellipse）需显式规则 |
| Ellipse | 移动、缩放、旋转 | 两半轴、中心 | 不能用 AABB 取代轮廓命中 |
| Polyline | 变换、节点选中 | 线段插点、拖点、删点 | 开放折线，无自动隐式闭合 |
| Polygon | 变换、节点编辑 | 增删顶点、改变轮廓 | 闭合、顶点不足/自交策略明确 |
| Arc | 变换 | 圆心、半径、起止角、扫角 | 跨 0/2π、极短扫角、整圆退化 |
| Bezier | 变换 | Anchor、Control1/2、曲线分割 | 保证曲线形状/连续性，不存屏幕采样线 |
| Path | 变换 | 多子路径、命令类型转换、闭合/断开 | 子路径分界/洞/绘制规则保持 |
| Text | 位置与边界 | 文本光标、格式、换行、对齐 | 输入焦点下箭头不移动图元 |
| Image/SVG | 布局、裁剪、旋转 | 资源/可视样式 | 文件安全、失联占位与可定位诊断 |
| Widget | 设计时布局 | Widget 参数/数据/事件配置 | Design 不执行设备操作 |
| Group/Symbol | 组/实例变换 | 进入上下文、子对象、实例覆盖 | 父子同选不能重复变换 |
| Visual Connector | 点线编辑 | 端点/折点/路由/标签 | 线身份由 MPC，禁止 Shape 八柄拉线 |

### 2.3 六类控制柄及其权限

**H1** 蓝色八柄：整体边界变换；**H2** 右上旋转柄：旋转/角度吸附；**H3** 四向快捷箭头：连接意图或快捷创建；**H4** 边缘小 ×：固定视觉连接点；**H5** 黄色形状专有参数柄：真实几何参数；**H6** 线路蓝色端点/折点：编辑 MPC 线。

规则：**H1 不连接、H3 不缩放、H4 不等于 H1、H5 不等于 H6**。触点尺寸按屏幕 DIP，颜色只是辅助；高对比/深浅主题还要由形状/光标/提示辨认，操作反馈不能只用颜色。

“有选区”不意味着所有柄同时显示；操作能力 + 当前工具/手势/作用域决定可见的那一种。见 `05。

### 2.4 绘制资源的安全与生命周期

Image 的源资源与缓存应以授权的工程资产 ID 寻址，贴图失效显示可诊断占位；SVG 按既有安全解析与限制载入（脚本、外链、字体与大路径资源不得绕过权限）；Text 和 Widget 在设计/编辑/运行之间明确焦点占用与事件边界。专业扩展不得自行把任意在线地址当成可信资源键。

## 03. 几何、坐标与统一场景投影

### 3.1 四层坐标和矩阵

`P_local → P_parent → P_world → P_screen(DIP) → physical pixels`。

- `W = ParentWorld ∘ LocalTransform`；
- `P_world = W · P_local`；
- `P_screen = Zoom · P_world + Pan`（WPF 输入先以 DIP 为单位）；
- 用户世界增量 `Δworld` 需映射回父局部，不能对旋转 Group 的局部 X、Y 分别取整；
- 目标 World 矩阵 `W' = ΔWorld ∘ W`，提交局部矩阵 `L' = Inverse(ParentWorld) ∘ W'`；矩阵实际乘法约定以 `MRCTransform.Compose`/`FGETransform2D` 单测对齐，不能靠排版公式猜行列存储。

**必须具备的边界**：NaN、Infinity、不可逆矩阵（行列式绝对值低于可配置容差）、奇异零尺寸、巨量坐标、极高 Zoom/极小 Zoom，以及旋转/剪切叠加，失败不能静默生成错乱位置。

### 3.2 统一 Scene Projection 的真相

输入 `MEM/MRC + MPC + (可选) MDS RuntimeFrame`，输出只读 `MVISceneItem`：对象 ID/地址、真实世界变换、Shape Geometry、绘制顺序、交互状态、VisualRouteDiagnostic。**同一份投影**同时服务：

`绘制线形 / 实际几何命中 / 控制柄锚点 / 连接端点 / 路由可见顶点 / 插折点段序号 / 属性定位 / 调试高亮`。

不得出现“画出来的是避障后的线路，折点插入却基于另一条简化路线”这种数据分叉。若投影临时缓存因工程 Revision、运行可视值、Zoom/主题或资源失效而变化，应明确区分**几何缓存失效**和**纯视觉缓存失效**。

### 3.3 精确命中与空间索引

基础策略：画布逆坐标变换 → 投影可见元素空间候选 → 世界边界粗筛 → 局部几何精确命中（Stroke/Fill/Path/Curve/Connector）→ DIP 最近距离排序 → 当前工具/选区规则决策。旋转图元 AABB 只做粗筛；空心图形不得把内部空白视为描边命中；开放 Path 的 Fill 语义有明确开闭条件。

优先用线性扫描验证正确性和真实性能；超过测得阈值才引入网格空间哈希/R-tree 等按 Revision/几何边界维护的**可重建非持久索引**。索引不改变工程身份、选择权威和渲染顺序。

### 3.4 控制柄与虚框

控制柄逻辑位置必须由图元/选区**世界真实几何变换**确定，再转 ScreenDIP 绘制。对旋转对象：选中框的方式需明确区分 `axis-aligned selection bounds` 与 `oriented local bounds`；现有选区世界 AABB 不能冒充支持任意方向旋转框。默认以世界 AABB 支撑多选共用外框，专业需要 oriented bounds 时先补几何与命中单测。

## 04. 图形呈现、渲染和性能边界

### 4.1 渲染流水线

`工程/运行帧 → 场景投影 → 可见范围/图层排序 → 样式/资源解析 → Shape/Widget/Connector 绘制 → 选择/辅助线/诊断叠层`。

静态 Shape、Text、Image/SVG、Visual Connector、Widget 设计外框以及临时 Overlay 应有清晰绘制顺序；连接选中状态覆盖画线但不改原线样式定义；路由碰撞用虚线/图标/文本多种手段提示，不能只靠红色区分。

### 4.2 局部刷新和缓存原则

- **工程几何改变**：修改该对象投影、空间索引局部区域、依赖它端点/障碍的连接；按受影响世界区域重绘；
- **样式/主题改变**：重绘样式及必要缓存，不触发无谓的工程 Revision 与全量路由；
- **Zoom/Pan**：坐标与屏幕缓存改变；若几何世界状态未改，路由的**世界几何**不得仅因视口缩放而被写回工程；
- **RuntimeFrame 改变**：仅刷新绑定影响的可视内容，不影响工程编辑事务；
- **资源变更**：显式清理 Image/SVG/Brush 缓存生命周期，避免工程切换后显示旧资源。

先验证 100/500/1000 图元下真实瓶颈，再引入空间候选过滤、文本图片缓存和增量路由；不要在 P1 阶段预造独立 RenderEngine/SceneGraph。

### 4.3 多 DPI 与可访问性

WPF DIP（1/96 英寸）与物理像素区别必须在系统级鼠标注入场景验证；容差/柄半径用 DIP，工程几何用 World。实测 100%、125%、150%、200% 及屏间移动后，控制柄、轮廓、连线命中/位置和屏幕截图相符。支持键盘替代操作、状态栏数据文本及高对比主题；无障碍支持按 Studio Shell 规范统一实现。

# 第二部分｜统一图元编辑核心

## 05. 交互、状态、命中与操作生命周期

### 5.1 四个正交维度，只有一个活跃 Gesture

| 维度 | 代表值 | 生命周期 | 工程是否持久 |
|---|---|---|---|
| Selection | 一项/多项/空，含实例地址与编辑 Scope | 资源编辑上下文内保持 | 否；持久对象身份由 MEM 拥有 |
| Hover | 图元/端口/连接/控制柄/空 | 光标移动即时派生 | 否 |
| Tool | Select、Draw、Connect、GeometryEdit、ConnectorEdit、Pan | 明确切换工具/编辑范围 | Studio 可保存偏好，不是工程真相 |
| Gesture | Idle、Marquee、Move、Resize、Rotate、GeometryHandle、CreateConnect、Rewire、Waypoint、Pan | PointerDown～Up/Cancel | 否 |

Design/Preview/Run/Debug 是上层授权/可视状态，不能把运行态视为可编辑模式。只允许 **一个** Gesture 占有鼠标捕获，不要并列起多个“临时移动/临时连接/临时变换”编辑会话；同一对象可以持续被选中，同时被悬停，但控制柄根据当前工具与活跃手势优先级呈现。

### 5.2 A/B/C/D 互斥的手柄显示矩阵

| 情境 | H1 八柄 | H2 旋转 | H3 快捷箭头 | H4 固定 × | H5 几何参数 | H6 线端折点 |
|---|---|---|---|---|---|---|
| **A 悬停连接** | 隐藏 | 隐藏 | 显示 | 显示 | 隐藏 | 隐藏 |
| **B 选中变换** | 显示 | 允许时显示 | 不覆盖当前对象的柄 | 连接工具时再显示 | 隐藏 | 隐藏 |
| **C 连线路径** | 隐藏 | 隐藏 | 隐藏 | 选择目标时可提示 | 隐藏 | 显示 |
| **D 几何精细编辑** | 隐藏 | 隐藏 | 隐藏 | 隐藏 | 存在该参数才显示 | 隐藏 |
| 多选共用选框 | **只一组** | 一组 | 隐藏 | 隐藏 | 隐藏 | 隐藏 |
| 锁定/隐藏/运行 | 不可编辑 | 不可编辑 | 按连接权限禁用 | 禁用编辑 | 禁用 | 只读诊断 |

**A/B/C/D 是用户情境，不是四个互不复用的 Editor 类**。当前 `SceneView` 已有 `_geometryEditMode`、`_selectionTransformDragging`、`_connectDragging`、`_rewireDragging`、`_waypointDragging` 等；先抽出可复用的优先级与 Cancel 规则，禁止仅为“漂亮状态图”重新建设第二套手势状态机。

### 5.3 命中优先级与冲突解决

按**工具适用性**筛候选后：

`已捕获活跃句柄 → 线路端点/折点（在 C）或几何点（在 D） → B 的选框控制柄 → A 的固定端口/快捷箭头 → 元素真实几何 → 空白/Marquee`。

两个句柄空间重叠按“当前 Tool + 当前选中对象 + 最近屏幕 DIP 距离 + 稳定 ZOrder/ID”处理；不能因 Scene 遍历次序偶变，鼠标不动却命中另一个对象。右键应命中当前对象但**不先强行移动对象**；连接工具优先连接，GeometryEdit 不自动切到连接工具。

### 5.4 MouseDown → Preview → Commit/Cancel

1. **PointerDown**：取当前 Page/Content/Group/Symbol 编辑 Scope、对象地址、起始 Revision、初始矩阵/线路投影、指针 World/Screen、Modifier 与可执行条件；
2. **Capture**：确认唯一本次 Gesture，按工具争用关系拒绝其它拖动；
3. **PointerMove**：由同一纯算法求世界位置/路线/吸附，预览只写内存临时数据；一帧只绘制目标状态，不修改 MEM；
4. **PointerUp**：用最后实际指针和修饰键重新执行相同求解，校验对象仍存在、作用域/Revision/权限未失效，原子提交一次 MTC/MEM；
5. **Finish**：释放捕获、清 Preview/Guide/Diagnostics 的临时态、重新 Scene 投影并反馈成功或具体失败；
6. **Cancel**：Esc、LostCapture、WindowDeactivate、切换 Tab/资源/工程、删除对象、改变权限、提交异常，必须统一取消并清理；失败 Revision 不变，不生成残余图元/端口/折点。

缩放/平移后的世界坐标和鼠标 DIP 有一处变化也不能悄悄换算法。若已经提交，Esc 不能偷偷撤销；用户应通过 Undo 历史主动恢复。

### 5.5 命令焦点规则

TextBox/Geometry 的文本输入状态下方向键移动光标，不能触发画布 Nudge；Shift/Ctrl/Alt 状态由当前工具和 Studio 已注册快捷键处理。历史默认方向键 1 世界单位、Shift+方向键 10 单位是当前基础实现；其它快捷键应先查 `StudioShell` 的统一注册表，避免 WPF/菜单/Ribbon 三套拦截。

## 06. 九 Shape 与 Geometry 精细编辑：实现级算法

### 6.1 共用几何编辑合同

“整体 Layout 变换”和“局部 ShapeGeometry 修改”是两种操作：前者通过对象世界/局部变换；后者先逆矩阵将鼠标世界点映射到局部几何坐标，再由原 Shape 的合法参数或路径命令计算新定义。几何编辑成功后重投影；不让拖一个贝塞尔控制点无故改变其它锚点世界位置。

统一约束：Anchor/Control/Segment 命中基于实际曲线段；路径点索引为**内部实现**，用户通过可视点/上下文命令插删，不输入整数命令索引。

### 6.2 折线、多边形、Path

- **Polyline**：空路径不合法；两顶点为基础非闭合线；线段插点产生 `P_i, P_new, P_{i+1}`；删除点造成端点/线段不足时拒绝或显式删除对象，不能静默变形。
- **Polygon**：必须保证闭合状态和合法最少顶点；自交是否允许应可通过 Shape 规则验证，填充遵循定义的 winding/fill rule，不默认自动修复成其它 Polygon。
- **Path**：包含多个子路径与 Line/Curve/Close 命令；“闭合当前子路径”不能一口气闭合其它轮廓；反向、合并、拆分、节点平滑/尖角转换需保留命令与局部位置语义；编辑时可选择某个子路径并保持该 Selection Context。
- 退化：重复点、零长度段、非常小的闭合环、极大坐标、数值非法与自交，全部给出确定的拒绝或可视警示，不要让坏数据进入持久化。

### 6.3 Bezier 算法示例

对三次 Bezier `B(t)=(1-t)^3P0+3(1-t)^2tP1+3(1-t)t^2P2+t^3P3`，在 `t∈(0,1)` 上精确分割（de Casteljau）：

`A=lerp(P0,P1,t), B=lerp(P1,P2,t), C=lerp(P2,P3,t)`；
`D=lerp(A,B,t), E=lerp(B,C,t), F=lerp(D,E,t)`；
新两段分别为 `(P0,A,D,F)` 与 `(F,E,C,P3)`。

这保持原曲线形状；**不能把屏幕离散采样点当作新的永久几何定义**。移动 Anchor 可明确选择“保持切线/镜像控制柄/独立控制柄”三种操作意图；Ctrl/Shift 模式经 Studio 快捷键冲突审查后生效，不静默自作平滑。Bezier 控制点的拖动在世界→局部坐标中计算，旋转 Group 下仍保持其余不相关控制点。

### 6.4 Arc、Rect、Circle 与特殊参数

- **Arc**：中心、Rx/Ry、StartAngle、SweepAngle 是几何参数；跨零角按连续有向扫角处理，弧长趋零和整圆边界按允许策略，不能靠显示角度 359°→0° 把几何抖到另一侧。ArcCenter、ArcRadiusX/Y、ArcStart/End 等现有 `MVIVisualHandleKind` 可复用。
- **Rect**：Radius 参数若未在实际 Shape Definition 注册，就**不显示**黄色圆角手柄；矩形整体缩放与圆角局部调参不能混淆。
- **Circle**：缩放若保持圆类型，半径统一倍率；非等比应拒绝或由明确 UI 命令转换成 Ellipse，**不能既声称 Circle 又保存不同 X/Y 半径**。
- **Line/Polyline**：端点和折点是 Shape 几何点，不创建 MPCConnection；仅用户使用 Visual Connect 工具时才创建真正视觉连接。

### 6.5 命中容差与失败

命中距离先在 ScreenDIP 定门限，再映射世界，避免 Zoom 变化后点选难度飘移；曲线需通过递归细分/求最近点等有限误差算法，且必须有迭代上限。遇到退化/逆矩阵失败/自相交不应生成 NaN Geometry；可显示“暂无法编辑该点”的明确诊断。

## 07. 选区变换、对齐、分布和智能辅助

### 7.1 共用选框、世界变换与 Undo

- 单选/多选只显示当前编辑 Scope 内一组共同控制柄；父子同时选中只保留合法顶层根，或整组拒绝，不重复应用世界矩阵；
- 世界变换 `W' = Δ · W` 映射至各个父节点局部变换；若父局部不可逆、选区含锁定/隐藏/不可编辑实例，**整组拒绝**，不能仅移动合法成员；
- 多选一次操作 = 一个 MEM Edit/Undo；预览与提交调用同一 `MVISelectionTransform.Delta`；
- 控制柄八个位置和右上旋转柄的定位以场景真实世界 bounds 计算，不使用每个对象本地 AABB 混和。

### 7.2 缩放、旋转、镜像和最小选择

- 八柄：四角双轴缩放、四边单轴缩放；Shift 按已实现约定保持等比；Alt 中心缩放；Shift 旋转角度吸附 15°；
- 小于容差的 Width/Height 不得生成奇异矩阵；反向拖动默认最小有效倍率并不自动镜像。需要镜像应提供**显式 FlipHorizontal/FlipVertical 操作**且在 MVI 定义和 Undo 测试清楚；
- 多选包含旋转 Group 的成员时，空间参考统一为 World；若需要旋转有向 OBB 选框，必须明确变换中心/锚点及对 API 的影响，不能把已有世界 AABB 宣称为完整 OBB。
- 当前单选与多选若行为差异需通过测试收敛，而不是先增加更多布尔参数形成互斥组合。

### 7.3 吸附与智能参考线

`MVIVisualSnapping.Resolve(scene, ids, Δx, Δy, zoom, snapGrid, snapObjects)` 为现有纯计算入口；移动吸附依据世界选区包围界，候选边/中心线对齐，容差如 6 DIP/Zoom；没有对象候选才使用统一世界网格。结果返回世界位移、GuideX、GuideY，不改变 Revision。

待深化：
- 吸附候选排除选区自身、隐藏/不可选、无意义的连接折线；平行对象同距离时按“优先规则、ZOrder、稳定身份”排序，保证每帧确定；
- 吸附线随 Drag 实时出现，Esc 消失；缩放/旋转吸附和移动吸附需分别定义，不把 Move 的网格校正无脑用在 Rotate；
- 多对象接近、密集参考线、1000 元素时先量测再引入可重建空间索引；小移动键盘 Nudge 不强制跳 25 世界单位。

### 7.4 对齐与均匀分布不是一个算法

| 操作名 | 几何含义 | 说明 |
|---|---|---|
| AlignLeft/Right | 世界 Bounds 的左/右边相等 | 参考对象/Selection bounds 规则明确 |
| AlignTop/Bottom | 上/下边相等 | 旋转对象以当前实际世界 Bounds 为准 |
| AlignCenterX/Y | 水平/垂直中心一致 | Key Object 优先，默认使用合并选区的中心 |
| DistributeOriginX | 左边缘坐标等差 | 不是形状之间的可视等间距 |
| DistributeCenterX/Y | 中心点坐标等差 | 宽高不同则边距可能不同 |
| DistributeGapX/Y | **几何边界间距相等** | 不同宽高时最贴近工程用户预期 |

不同宽度对象按世界左边缘排序（保持首尾固定），目标边缘间隔为：

`g = (rightmost.Right - leftmost.Left - Σ_i width_i)/(n-1)`；
`left_{i+1} = left_i + width_i + g`。

当对象重叠导致 `g<0`，需选择“允许重叠/显式提示拒绝”策略，不能显示等间距却暗中排序跳跃。纵向用 Height 类推。计算采用一次世界布局计划、一次 MTC 事务、一次 Undo。**源码事实**：当前 `MVIVisualEditor.Align` 已有 left/top/centerX/centerY/distributeX，`distributeX` 是左边缘均分，不能冒充 DistributeGapX 已完成。

### 7.5 布局与交互反证

等比/中心、跨旋转 Group、退化尺寸、双选/1000 选、锁定组/祖先组、复制后重叠、DPI/Zoom/拖动中失捕，均应在 MVI 核心及 WPF 真实输入两级留证。每个测试检查成功前后对象 ID、世界 Bounds、一次 Undo 的 Revision/历史，非法操作拒绝时工程不变。

## 08. 视觉连接、固定/浮动附着、路由与线路操作

### 8.1 四个层次不能混淆

1. `MPCConnection`：持久身份、Owner、两端 Endpoint、路由模式及人工 Waypoint；
2. `MPCPortKind.Visual` 与 `MPCPort`：可连接的视觉端口身份、能力/方向/资源实例地址；
3. **附着策略**：固定局部点或随真实轮廓/连线角度变化的浮动轮廓点，允许两端分别配置；
4. **Scene VisualRoute**：可见折线、命中段、路由诊断、交叉/箭头/标签的只读结果。

当前 `MVIVisualAnchors` 提供 north/east/south/west 四向固定锚点，`MPCVisualRouteKind` 有 Straight/Orthogonal/Manual，`MPCVisualWaypoint` 为相对端点的人工折点；没有理由另建永久 `VisualEdge` 或在 WPF 私存端点。

### 8.2 附着数据合同：既明确目标，也避免越权新增字段

**目标**：一条连接两端各有 `Fixed` 或 `FloatingOutline` 意图，保存时保留同一连接 ID；Fixed 端在图元自身 Local 位置/参数下附着，Floating 端由 MVI 按变换后的真实轮廓交点求解。旋转/缩放/改变路线不改变 Endpoint 所属图元身份。

**兼容性裁决框架**：
- `MPCEndpoint(Address, PortId)` 当前明确要求非空 PortId，**不允许用 Guid.Empty 伪造浮动**；
- 可在现有 MPC Port/Connection 元数据内为两端补最小附着描述（例：`AttachmentKind / LocalPosition / ContourRule / PreferredNormal`），但字段归属、默认值、复制映射、序列化版本须先由 Core Owner 按现有快照与 Edit 契约审核；上述词只是**待裁决数据语义，非发布 API 名称**；
- 旧四向连接默认保持原固定行为；旧工程打开/保存/再次打开的端口与连接身份不丢；
- 浮动点以稳定的合法 Visual Port 身份参与 Endpoint；建议评估“轮廓入口端口 + 连接每端策略”，避免每次拖放永久新增匿名端口；
- 删除正在使用的固定点要明确拒绝或提供“全部重附着”原子命令，禁止把线路留在不可解析端点。

### 8.3 固定/浮动几何求解

**固定**：`Pworld = WorldTransform · Plocal`；出线方向由局部法线经世界线性变换得到。不能在对象旋转后仍假定 east 永远向屏幕右。

**浮动**：通过源/目标合法轮廓与期望出入方向的射线交点选择锚点。优先真实 Shape Geometry/Stroke 可见边界，其次才是明示的受限退化策略；不可用 AABB 中点代替复杂 Path 的轮廓点并宣称无误。多个交点按距离、法线对齐/通道代价、稳定排序确定；退化/空轮廓返回明确失败，不落盘临时世界点。

### 8.4 A 连接情境的四种动作

| 用户动作 | 工程效果 | 取消/反证 |
|---|---|---|
| 拖固定 × → 固定 × | 两端固定，同一 Visual Connection | 目标失效则不生成线 |
| 拖方向箭头 → 目标轮廓 | 一端固定/另一端浮动或按当前连接工具确定 | 不得强制把目标改成最近四向端口 |
| 单击箭头 → 连接已有图元 | 使用命中方向与合法性优先级生成连接 | 多候选有可选列表，不偷偷连错误对象 |
| 单击箭头 → 创建/克隆并连接 | 使用 MEM Transfer/创建 + Connect 的**一个事务** | 取消列表不生成新节点和孤立端口 |

连接方向、MPC 数据类型/许可、同资源/跨资源范围、专业门禁、运行态授权，应先校验再提交；鼠标移离/失焦只销毁 Preview。

### 8.5 C 连接线路径编辑

- 选线仅显示 H6：两端编辑柄、已有人工折点、可插点线段和当前线路诊断；不显示原图元八个尺寸柄。
- 端点重附着通过 `MVIVisualEditor.ReconnectVisual` / MPC Endpoint 原子修改，线 `Id` 不变；非法来源/目标不造成“先删除后重建”的身份变化。
- `InsertVisualWaypoint` 必须使用场景投影的可见线段点列 `VisibleConnectionPoints`；插点后转换 Manual 时保持用户刚看见的路线形状，而不是另一套简化计算点。
- 拖动和删除 Waypoint、自动→人工、人工→自动/直线都需明确“人工约束将丢失”的操作反馈并支持 Undo；拖到零长度/重复段遵守确定性合并或拒绝规则。
- 线标签（源、目标、居中、自定义相对路径位置）、线箭头/样式、线悬停说明应通过现有样式/属性投影扩展；不能创造与连接身份分离的第二条持久线。

### 8.6 自动正交、避障与多线协同

现有 `MVIOrthogonalRouter` 已提供基于障碍、端口方向的寻路及失败诊断。深化时按以下目标优先级求解：

`合法端口与连接身份 > 硬障碍无碰撞 > 路径随局部变更稳定 > 转角数/总长度 > 多线交叉/通道间距 > 美观`。

**建议的算法阶段**：
1. 端口处生成合法出口短段，按变换后的法线约束初始方向；
2. 构造源/目标/障碍角点及通道候选的正交可见图或栅格候选，障碍膨胀最小安全间距；
3. 有限状态空间搜索，代价 `C=α·Length+β·Bends+γ·Intersections+δ·DeviationFromPrevious`，其中碰撞属于硬约束；代价系数由真实测量选取而非本文虚构固定数；
4. 简化共线冗余节点，保留合法进出线方向，维持路径索引与可见线一致；
5. 路由无解时返回**可定位 Diagnostic**、冲突高亮及可操作建议，不以“画一条穿障碍线”默默兜底。

多线协同不是为每条线独立寻路后立刻宣称最优：需处理同源多出口、平行通道、重叠线分离、桥跨/交叉标识、已有人工路线优先级及跨 Group 边界。此为明确目标，当前无足够证据断言已交付。

### 8.7 增量重算与稳定性

对象几何改变后只重算受影响端点或障碍范围内的连接；视口 Zoom 本身不改变世界连线定义。缓存仅由 Scene Revision 和依赖范围/路线配置决定，重建不改工程。对 100/500/1000 图元与不同连接密度测量路由调用次数、交叉数、失败率、P50/P95/P99 和 UI 卡顿，再决定是否引入空间索引或复合通道缓存。

### 8.8 典型反证

目标删除、Port 锁定/隐藏、旋转 Group 包围、端口方向不匹配、路由堵死、两线重叠、人工 Waypoint 越界、非法连接、旧工程缺附着字段、复制端口重映射、Esc 与失捕：检查 Connection ID、Endpoint、Route、MEM Revision、真实画线、诊断文字和 Undo 均保持一致。

# 第三部分｜统一工程编辑与 Studio 使用

## 09. Group、Symbol、Layer 与复制剪贴板

### 9.1 Group 操作与编辑上下文

**既有** `MEMEngineeringSession.Group/Ungroup` 和 `MTCEditSession.Visual.Group/Ungroup`；当前 Group 要求同 Parent、同 Layer 的至少两个有效图元，且不会任意破坏绑定声明依赖。Studio 应清楚解释拒绝原因，不为满足按钮动作就偷偷把对象搬到另一 Parent。

- 组整体选中显示一组控制柄；组内编辑由导航 Scope 控制，支持双击进入、面包屑/对象树退出、当前 Page/Group 与选中对象的稳定同步；
- Group Transform 的世界/局部组合必须保持所有子项世界位置正确，Ungroup 过程保留可表达的剪切/旋转/缩放；遇到不可逆父矩阵/超范围属性明示拒绝；
- 父与子不能一起当独立选区成员被重复变换；进入定义/退出组后选择要恢复正确 Resource + MRIObjectAddress；
- “Resize Children”是否单独提供为用户可选模式需测试真实对象参数、组缩放语义和序列化影响，不将 draw.io 设置当成 X3 已有功能。

### 9.2 Symbol 定义、实例、覆盖和专业绑定

Symbol 共享一份定义，实例通过 `MRIResourceReference/MRIObjectAddress` 表达引用。编辑**定义**意味着全部实例刷新；编辑**某实例覆盖**只作用于该实例允许覆盖的属性/绑定，不把实例移动/旋转错误地写回共享定义。

代表场景：一个电机 Symbol，两个实例 A/B；分别覆盖设备名/状态变量/MDS 地址；A 正常/B 故障可按运行数据呈现不同样式；返回定义编辑改变底座图形，两实例同步；保存重开引用身份、属性覆盖与调试定位不丢。缺少授权的设备事件必须拒绝，Widget/动画不直接越权控制设备。

### 9.3 Layer/ZOrder 与锁定

Layer 控制绘制顺序、可见/可选/锁定规则；对象树位置和画布上真实 ZOrder 同步；同层 BringFront/SendBack/Forward/Backward 与跨层移动的操作策略须分清。隐藏元素不误命中，锁定元素允许只读查看但不能被移动；祖先 Group/资源 Scope 的锁定应共同参与判断。若当前实现尚无完整“Layer 编辑器”，规范不能假装已经存在。

### 9.4 复制/剪切/粘贴与 MEM Transfer

**已有源码**：`MEMEngineeringSession.PreviewDuplicate`、`PreviewImport`、`Import`、`Duplicate`、`MTITransferPlan`；它们是身份重映射与工程事务的唯一入口。Studio 的 Visual Clipboard 只是缓存用户选区/来源/意图，不另持久保存工程图。

| 操作 | 精确定义 | 失败/历史 |
|---|---|---|
| Copy | 选择可复制顶层根，排除重复子节点，确定所需内部依赖与外部连接策略 | 只读，不增加 Revision |
| Paste Preview | 对目标 Parent/Layer/实例作用域构造 TransferPlan，验证 Parent、引用、权限和目标世界位置 | 预览不污染工程 |
| Paste Commit | 应用已批准 Plan、ID/Port/Connection/资源引用重映射及相对几何变换 | 一个事务，一个 Undo |
| Duplicate | 在合法同 Scope 内复制并采用固定可见位移（如 10～20 DIP 映射 World，数值应经 UX 定案） | 同一 Transfer 底座 |
| Cut | 保证“粘贴成功再确认源删除”；可原子同会话迁移时使用 MTC 事务 | 不允许先删源，后粘贴失败 |
| Boundary links | 内部两端均复制时重映射；仅一端被复制需 Pin/断开/引入依赖闭包的明示策略 | 禁止悄悄连回原不期望对象 |

复制完整 Group 与 Symbol 实例时，新对象工程 ID 必须唯一、实例引用可解释、内部端口关系正确，Undo 后无残余临时 Port/Asset。MOL 逻辑 Clipboard 仍由 MOL 负责，不借来替代通用视觉工程复制。

## 10. 属性、样式、内容与动态能力

### 10.1 图元 Schema 与状态边界

属性面板以 MPM Schema 为真源：Common（名称/可见/锁定/几何）、ShapeGeometry（参数）、Style（填充/描边/透明度/资源）、Connection（端点/路径/诊断）、Binding/State/Animation/Event（所授权能力）。无选区显示 Page/Canvas 属性；单选显示完整对象属性；多选显示交集与混合值，只有经许可的共同字段可批量提交。

属性面板输入、拖动控制柄、Ribbon 命令必须调用**同一工程 Edit**，不能让输入 X/Y 与拖动走不同矩阵或 Undo 分支。属性无效值/外部资源不可用时拒绝或显示占位，不制造半更新。

### 10.2 样式与资源优先级

建议样式求值次序遵循现有资源/定义/实例层约定：工程 Theme/StyleResource → 图元定义样式 → 允许的实例覆盖 → Preview/Runtime 只读状态样式 → Selection/Diagnostic Overlay（仅显示不存储）。具体优先级冲突以当前 MPM/Visual Style Owner 规格裁决；Studio 不能永久覆写运行态临时颜色到 Shape 定义。

### 10.3 Text、Image、SVG、Widget 内容编辑

Text 双击进入实际文本输入，不挪用画布 Delete/方向键；Image 编辑应在工程资源权限范围内支持替换/适配/裁剪（未实现标为目标），SVG 仅解析安全子集；Widget Design 移动/缩放只改布局，Preview/Run 下焦点进入控件时事件由专业门禁控制，不能因为单击被选中图元就下发设备操作。

### 10.4 MDS 状态/动画及授权事件

设计态保留可编辑定义，运行值来自 MDS，动画源来自授权状态映射；调试模式可显示 Value/SourceMap 追踪但不得改设计定义。事件必须通过 MRK/专业权限与联锁通道鉴权；未授权/未连接/模拟模式下须可诊断拒绝而非静默触发设备。

## 11. 统一命令、编辑事务与失败回滚

### 11.1 从用户意图到唯一提交

`StudioCommand → Selection/Scope/Authorization → MVI 计算 Preview → MTC Edit(transaction) → MEM Revision/Undo → SceneProjection + UI refresh`。

同一命令从右键/Ribbon/键盘/属性发起应共享 CanExecute、权限、错误码、Undo 名称、参数合法性；不在 MainWindow、SceneView 和 MVI 三处独立写几何变换。Ctrl+Z/Y 仅调用唯一 MEM Undo/Redo（在当前编辑器焦点合法时）。

### 11.2 Preview 与提交合同

Preview 输出应携带：当前 Resource Scope、对象 ID/地址、起始 Revision、世界输入位置、WorldMatrix/RouteWaypoints、SnapResult、Diagnostic、PreviewValid；这些是**一次手势的短命对象**，不是永久工程存储。提交时验证来源一致，不一致拒绝或重新建立预览；取消释放所有资源与捕获，Revision 原样。

**不要为了以上字段立刻引入全新接口族**：先检查 `MTCEditSession.Contracts`、现有 MVI 编辑结果/SceneView 字段是否足够；测试出现重复或不一致时才提炼最小统一交互结构。

### 11.3 异常反证

锁定/隐藏、过期引用、跨 Page/定义、不可逆矩阵、无合法连接、重复端口、粘贴闭包不全、失焦/切页、Undo 后旧句柄引用，以及修改过程中对象被删，都不能半提交。一条工程变更出错时：对象、端口、连接、Layer/顺序、Revision、Undo 历史保持一致；错误反馈可定位、简洁且可恢复。

### 11.4 工程版本与保存重开

保存工程包含定义/实例关系、几何参数、视觉端口、连接路线/人工点、样式/绑定资源和可迁移版本；不能包含鼠标悬停、DPI、Preview、临时参考线、路由错误可视点阵或 MDS 当前帧作为持久工程真源。读回同一工程后需核查身份与几何数值，版本升级明确迁移旧固定端口。

## 12. Studio 工作区与自然高效的用户操作

### 12.1 布局服从 Studio Shell

使用已经定案的**一行一级菜单 + 一行上下文 Ribbon**；左资源树、当前资源对象树/工具箱，中间 Page/Content/Flow 单主画布（配置类资源可为 1×N/2×N/3×N 组合面板），右 Schema/样式/绑定/事件/专业参数，底部诊断/运行信息。本文不另设计 Docking 引擎，也不因图元编辑把 Group/Element 全塞进资源树。

### 12.2 操作系统一览

| 用户动作 | 统一反馈 | 工程结果 |
|---|---|---|
| 工具箱点击/拖入 Shape | 半透明 Ghost + World 网格位置 | Create，单次 Undo |
| 单击/Ctrl/Shift/框选 | 高亮/共用柄/计数 | 只改 Selection，不写工程 |
| Move/八柄/旋转 | 世界 Preview、吸附线、尺寸角度 | 结束时变更一次 |
| 双击 Path/Bezier/Arc | D Geometry 专用柄/属性 | 仅对应几何定义修改 |
| A 悬停固定点/箭头 | 目标类型/合法性/可连接提示 | 释放成功才创建 MPC 线 |
| C 选线/折点 | 真实可见路径、Waypoints 和告警 | 原 MPCConnection 身份更新 |
| Group/Symbol/Layer | 面包屑/对象树/当前作用域 | 唯一 MEM 事务与引用 |
| Copy/Paste/Align/分布 | Ghost、KeyObject、禁用原因 | 完整 Transfer/布局事务 |
| Esc/失焦/切页 | 清理 Ghost/柄/提示/捕获 | 无 Revision 变化 |

### 12.3 Context Ribbon 与命令联动

上下文可分“图元普通、几何编辑、连接线、Group/Symbol、Viewer 专业”。每个 Ribbon 项/右键菜单/快捷键都是同一命令声明的不同展示入口；菜单启用状态随着 Selection、Scope、Tool、权限更新。右侧属性面板按当前选择类型改变，不展示无法实现的黄色参数柄、浮动端口字段或专业按钮作为假功能。

### 12.4 用户体验细则

- 对象重叠时轮换选择/前后层命中策略要可解释；Space/中键 Pan 与 Ctrl/Shift 多选冲突要有唯一键表；
- 提示语可操作（“此连接端口已锁定，请解除后重试”）而非内部异常堆栈；高级信息可在诊断窗展开；
- 操作历史明确显示人类可理解的描述，例如“移动 3 个图元”“修改连接折点”“组合 4 个对象”；
- 浅白与深灰主题对比正确，三色控制柄不靠色彩单独传递含义；
- 不要求用户点中像素级极小对象才能操作，Zoom/DPI 下保持可用 DIP 目标大小。

### 12.5 Studio / TestCenter 共用图元操作面板（2026-10-27 架构落点）

- **操作语义 Owner**：`Mechanism.Visual/MVIVisualEditor` 及既有 MEM/MTC/MPC，控制柄、路由和 Undo 继续复用同一工程；按钮不制造独立的 `GraphicsDocument`、`UndoStack` 或 `Graph`。
- **唯一公共 WPF 入口**：`Mechanism/Presentation.Wpf/Visual/MPWVisualOperationPanel.cs` 负责九 Shape、Text、Widget 和基本编辑/布局/线路命令入口。它只发出 `OperationRequested(string)` 语义动作，按钮 AutomationId 稳定为 `MPW.Visual.<action>`；宿主负责 CanExecute、权限、异常反馈和与正式命令中心的映射。画布仍复用 `SceneView.cs`，不建立测试专用 Canvas。
- **Studio 装配**：`Applications/Studio/MainWindow.xaml(.cs)` 在原工具箱位置复用公共面板，仍沿现有 Studio 资源 Scope、Ribbon、ViewModel 和 Undo 提交。Studio 业务操作不得下沉到 MPW 的按钮 Click 处理代码。
- **TestCenter 装配**：`Tests/TestCenter/Manual/Graphics/TCTManualGraphicsFrame.cs` 在 `manual.graphics` 注册真实 Factory，创建短命测试 MEM 工程及 MVI 编辑器，将公共操作面板与 `SceneView` 的交互意图转发给同一编辑入口。工作区关闭应取消预览并释放临时会话；ManualSession 与测试结果/Attempt 是 TestCenter 自己的 Owner。
- **测试与事实边界**：`--graphics-viewer-smoke` 使用 Windows STA 构建真实 Frame、触发公共矩形 Button.Click、检查 Scene/Revision、执行 Undo/Redo；对应 `.github/workflows/testcenter-windows.yml`。这是 WPF 按钮事件和工程事务回归，**不是** GE-01～15 全部系统级实鼠操作或工业设备验收。没有执行测试的场景不显示 Passed。
- **后续深化**：公共面板目前以功能入口共用为主，MPM Schema 专业属性、多选状态、完整权限/Schema驱动的按钮可执行性、快捷键焦点和浮动端点仍应继续按 GE 矩阵接入。不得将 Studio 专有 Docking / TestCenter 专有 Run/Attempt 迁入 MPW。

### 12.6 工程 Group 选中与 SceneView 可视选区：同一身份，两种呈现

- **工程事实**：`MRCGroup` 属于 `MRCVisualItem` 容器，承载父子变换、层级与权限；`MVISceneProjection` 只把实际可绘制的 Element/Visual Connection 投影为 `MVISceneItem`。不要为了给 Group 显示操作按钮，额外造一条虚假的 Shape SceneItem。
- **对象树与画布语义**：TestCenter/Studio 对象树通过 MEM Group.Id 和场景 Item.Id 做统一身份投影。普通图元可以用 SceneView 的真实可视多选；单个 Group 容器允许在对象树中显式选中，但不与多个普通图元合成一套隐含选区。选 Group 不得污染鼠标 Hover、画布选区或工程 Revision。
- **命令可用性**：`CanUngroup` 必须同时检查 `MEM.Find(SelectedId) is MRCGroup`、作用域及编辑授权；不能仅凭 `SceneView.SelectedVisualIds.Count` 决定按钮是否可用。Group/Ungroup/ZOrder 等命令仍委托 MEM/MVI 的原事务和 Undo。控制柄仅服务确实可投影的对象。
- **Group 属性编辑**：对象树选中 Group 后，可使用既有 `MVIVisualEditor.SetGeometry` 修改布局 X/Y/Width/Height/Rotation；不得另存测试专用变换。Group 自身的变换按 MRC/父子世界矩阵生效，其子图元身份不变；失效、锁定、非法几何值必须拒绝。直接在 Group 外框上进行完整鼠标拖动/缩放/旋转的高级交互另按 GE-07、GE-10 验收，不能以属性编辑通过代替。
- **可复现证据**：`TCTGraphicsFrameSmoke` 在真实 WPF 窗口中执行双图元创建、对象树选择、Group、属性移动、Undo、Ungroup、工程保存重开和失败回退；截图来自 WPF Window `RenderTargetBitmap`，独立于设计总览图。按钮事件回归仍不等于系统级鼠标注入测试。
- **架构原则**：对象树投影是 UI View Model，不是第二工程树；公共操作面板不持有 Group 或 Revision；所有事实与边界由 MEM/MTC/MVI 提供。

# 第四部分｜使用样例、专业扩展与验收

## 13. 开发者 API、两个使用者与专业扩展闭环

### 13.1 软件开发者——基于真实源码的 C# 最小例子

以下 API 签名已在阅读快照查见；场景是**示意调用链，不是宣称这段独立代码已在 Windows 新编译运行**。绘图编辑与工程创建必须共享同一个 `MEMEngineeringSession`：

```csharp
using (MEMEngineeringSession session = MEMEngineering.Create("可视化工程"))
using (MVIVisualEditor editor = new MVIVisualEditor(session))
{
    MEMPage page = session.AddPage("工艺页");
    MRCShapeElement a = session.AddShape(page.RootContent, "工位A",
        MRCShapeDefinition.Rect(120, 80), 20, 30);
    MRCShapeElement b = session.AddShape(page.RootContent, "工位B",
        MRCShapeDefinition.Rect(120, 80), 320, 30);

    editor.SetPage(page.Id);
    // 两个合法图元共用同一世界位移。结果必须检查 Success；
    // 选择、拖动预览本身并非持久事务。
    MTCEditResult<bool> moved =
        editor.MoveSelection(new[] { a.Id, b.Id }, 25, 10, true);
    if (!moved.Success)
        throw new InvalidOperationException(moved.ErrorCode + ": " + moved.Message);

    MTCEditResult<bool> connected = editor.ConnectVisual(
        a.Id, "east", b.Id, "west");
    if (!connected.Success)
        throw new InvalidOperationException(connected.ErrorCode + ": " + connected.Message);

    // 真实项目可随后调用 session.SaveAs(合法路径)，
    // 再通过相应工程 Open API 验证身份和线路往返。
}
```

注意：`ConnectVisual` 当前是四向固定连接 API；**没有**发布的浮动端点 API，不应借示例把 `ConnectFloating` 之类不存在的函数当成既有成果。`MoveSelection` 限制至少两个合法且不同 ID 的对象。更完整的基础工程示例见 `Examples/VisualAuthoring/EngineeringVisualQuickStart.cs`（HMI Symbol 双实例与状态/动画定义）；其 `SaveAs`、绑定等代码需以该文件真实版本核对。

### 13.2 工程编排用户——一页完成 A/B/C/D

1. 创建 Page，拖 Rect/Circle/Bezier/Path/Text/Widget，检查选中/工具箱/资源树；
2. A 悬停可连接对象，固定点/方向箭头各自显示，固定-固定、固定-浮动、点击新建并连接按目标合同测试，非法不创建；
3. B 单/多选八柄、旋转、智能参考线，混合锁定/旋转 Group 反证；
4. D 选 Bezier/Path 锚点/控制点，Arc 特有柄只在存在参数时可用；
5. C 选 Visual Connection 进行端点重附着、可见折点插删、路由报警；
6. 组合 Group、建立 Symbol、两个实例覆盖、跨 Layer/ZOrder、复制粘贴；
7. Schema 改 X/Y/尺寸/样式、绑定变量，保存重开，Undo/Redo 后形状与线身份不丢；
8. Preview/Run/Debug 下只读观察/定位，不暗中改设计数据，未授权操作拒绝。

### 13.3 HMI / MOL / Vision / CT3D 四种扩展

| 专业 | 复用公共编辑 | 必须保持专业自己的规则 |
|---|---|---|
| HMI | Shape、Symbol、数据/状态样式、编辑命令、Group/Undo | PLC/授权/状态转换，禁止未授权下发 |
| MOL | 几何画布手势/缩放/吸附、选区/诊断呈现 | `MOLLink`、逻辑端口、分支/循环编排、MOC 编译 |
| Vision ROI | 选区变换、精细几何、Overlay、Undo、相同命令入口 | Pixel/Image/Camera 坐标、ROI 参数和检测逻辑 |
| CT3D Overlay | Overlay/标注控件、选区/属性与用户操作体验 | 体数据的 Patient/World/IJK、MPR、切片、测量/标注坐标语义 |

公共二维 Visual Scene 不能冒充三维体绘制/ROI 的全部几何 Owner；专业扩展只在其专业坐标合同里添必要适配，仍通过统一工程、运行和调试身份链。

## 14. GE-01～GE-15 完整验收体系

### 14.1 统一记录格式与证据等级

每条用例应有：**目标/资源/测试数据 → 操作路径 → 预期 UI → 预期 MEM/MPC 变更 → 必测拒绝和恢复 → Save/Open/Undo → 测试代码/Windows Run URL → 测试 SHA/环境/DPI/设备等级 → 已验结论**。证据等级为 `DesignOnly / SourceLocated / CompilePass / CoreTestPass / WpfStaPass / SystemInputPass / PersistencePass / PerformancePass / ProfessionalPass / HardwarePass`；不能用低等级代替高等级。

### 14.2 核心验收矩阵

| ID | 核心正例（Studio 可观察） | 工程不变量及强制反证 | 最低证据 |
|---|---|---|---|
| **GE-01** | A 悬停仅出现 H3/H4、合法目标提示、鼠标离开清除 | 非法/锁定/隐藏不可连；悬停 Revision 不变 | WPF STA+系统鼠标 |
| **GE-02** | B 单选/多选八柄、右上旋转、Shift/Alt 变换 | H2 不发起连接；多选只一组柄；Esc 不提交 | MVI+WPF+系统鼠标 |
| **GE-03** | D Arc/Bezier/Path 专用柄及精确插删 | 伪参数柄不可出现；退化/多子路径反证 | FGE/MVI+WPF |
| **GE-04** | C 端点重连、VisibleConnectionPoints 插删折点 | Connection ID 不变，错误和取消不改端点 | MPC/MVI+WPF |
| **GE-05** | 自定义固定/浮动混接、快捷新建/克隆连接 | 端点身份有效，迁移兼容，取消无孤点 | Core迁移+系统鼠标 |
| **GE-06** | Straight/Orthogonal/Manual、复杂路由冲突告警消失 | 无解不可静默碰撞；只读诊断不增 Revision | 路由 Core+WPF |
| **GE-07** | 多选跨旋转 Group 等比/中心/角度变换 | 父子同选、锁定、极小矩阵不产生部分提交 | Core+WPF+Undo |
| **GE-08** | 网格/参考线/Preview=Commit、多 Zoom/DPI | DIP 命中与实际世界位置一致，不跳动 | Core+系统 DPI |
| **GE-09** | 左右上下/中心、GapX/Y 等间距、ZOrder/Layer | 不同宽高间距精确，失败完整回滚 | Core+Studio |
| **GE-10** | Group 内编辑、Ungroup、Symbol 定义/双实例覆盖 | 地址/嵌套矩阵/引用/保存重开正确 | MEM+Studio |
| **GE-11** | 含内部连接的 Copy/Cut/Paste/Duplicate | ID、端口和引用映射无重复，原子 Undo | Transfer+Studio |
| **GE-12** | Esc/失捕/失焦/切 Page、过期对象取消 | 无 Ghost、无捕获残留、Revision 不变 | STA+系统注入 |
| **GE-13** | 工具箱→A/B/C/D→右键/键盘/属性→保存重开 | 不得靠私有 handler 直接调用冒充 UI | 系统级操作记录 |
| **GE-14** | 100/500/1000 图元+多连线压力、变换/命中/路由 | 有 P50/P95/P99、内存/失效次数/交叉及错误率 | Windows 性能测量 |
| **GE-15** | HMI 双电机、MOL 流程、Vision ROI、CT3D Overlay 复用 | Runtime不改MEM，未授权设备不动作 | 跨体系+实机独立 |

### 14.3 必须拆分到子验收编号的重点

- **GE-01.1/01.2/01.3**：可连图元悬停/显示固定端口/箭头目标，离开/隐藏/锁定；
- **GE-02.1～02.5**：单选、Ctrl/Shift 多选、八柄、旋转、Shift/Alt 与 DPI；
- **GE-03.1～03.5**：Polyline/Polygon、Bezier 精确分割、Arc 边界、Path 多子路径、特殊黄色参数；
- **GE-05.1～05.6**：固定端、浮动端、两端混接、自定义固定点、快捷新建克隆、旧版迁移；
- **GE-06.1～06.5**：直线、正交、人工、障碍堵出口、多人线交叉/通道；
- **GE-07.1～07.5**：普通 Group、旋转 Group、跨层/锁定、极小尺寸/反向拖、一次 Undo；
- **GE-08.1～08.4**：世界网格、智能参考线、100/200% Zoom、四档 DPI；
- **GE-09.1～09.4**：基本对齐、左右上下、边界等间距、Layer/ZOrder；
- **GE-10.1～10.5**：分组/解组、进入退出、嵌套变换、Symbol 双实例、重开身份；
- **GE-11.1～11.5**：单对象、Group 子树、内部线路、边界链接/跨工程、失败回滚；
- **GE-12.1～12.5**：Esc、CaptureLost、WindowDeactivated、TabSwitch、对象失效；
- **GE-14.1～14.4**：100、500、1000 图元及连接密度/缓存/耗时/内存；
- **GE-15.1～15.4**：HMI、MOL、Vision ROI、CT3D Overlay。

子编号扩展不会重排既有 GE-01～15，旧测试和提交证据保持可追溯。

### 14.4 三条完整系统级输入验收链

**链 A（连接）**：新建双图元→鼠标悬停固定点→拖线到固定点→Undo/Redo→第二条固定-浮动→旋转/移动目标→快捷克隆连接→保存重开；非法目标/删除端口/失焦必须回退（尚无的功能标 `DesignOnly`，不可填成功）。

**链 B（整体/内部几何）**：选 Rect→八柄与旋转→Shift/Alt→Ctrl/Shift 多选→旋转 Group→Bezier 节点分割→退出 D→属性修改→Undo/Redo→重开；相同 World 坐标/矩阵与选择规则。

**链 C（路径/诊断）**：选线→正交路由→移动障碍→点真实可见线段插折点→拖端点到另一图元→Manual/Orthogonal 切换→堵塞出口→警告显示→移开障碍→警告消失；诊断不改工程 Revision。

### 14.5 性能与门限

固定 Windows OS/.NET48/WPF/DPI/CPU/GPU/RAM、工程构造数据与图元复杂度、独立首次/热缓存流程，记录：`Select/Drag/Zoom/Projection/Route` P50/P95/P99、UI 最大卡顿、临时 GC、托管与非托管峰值、可见/总对象数、实际重算线路数、障碍冲突与错误率。**基线数字由真实 CI 或测试机产生，验收阈值须由负责人审定，本文不伪造性能指标。** 系统级键鼠录屏和 WPF STA 自动单测分别存证，不互相替代。

### 14.6 六道签收门禁

Developer（统一自然 API）、Studio（真正键鼠/属性/树/Ribbon）、Engineering（身份/事务/持久化）、WPF（取消/焦点/DPI）、Performance（测量合格）、Professional/Hardware（专业运行/设备联锁）。只有全部目标门禁证据可用时才可写“完整图元编辑达标”；某 CI Workflow 未在当前 SHA 触发就标“未触发”，不能引用旧 SHA 绿灯冒名。

# 附录 A｜对象能力—控制柄—数据变更完整索引

| 对象 | A连接 H3/H4 | B变换 H1/H2 | C线路 H6 | D专用几何 H5/Anchor | 主要持久化 Owner |
|---|---|---|---|---|---|
| Rect | 能力允许时 | 是 | 否 | 圆角存在时 | MRC Shape/MTC |
| Circle/Ellipse | 能力允许时 | 是，圆约束另判 | 否 | Center/Radius 视类型 | MRC/FGE/MTC |
| Line/Polyline/Polygon | 根据能力 | 是 | 否 | 顶点/线段/闭合 | MRC/FGE/MTC |
| Arc/Bezier/Path | 根据能力 | 是 | 否 | Anchor/Control/Arc 参数 | MRC/FGE/MTC |
| Text/Image/SVG | 根据能力 | 是 | 否 | 内容/资源编辑专用 UI | MRC/Assets/MPM |
| Widget | 设计态判断 | 是 | 否 | Widget 专业内容 | MRC/MPM/Professional |
| Group | 根据暴露的端口 | 是 | 否 | 进入子对象 | MEM/MRC |
| Symbol Instance | 根据定义公开端口 | 实例规则下 | 否 | 进入定义/实例属性 | MRI/MEM/MPM |
| MPC Visual Connection | 非图元源点 | **否** | **是** | **否** | MPC/MTC |
| MOL Flow Link | 遵守 MOL 专用工具 | **不照搬** | 使用专业端点 | **否** | MOL |

**核对规则**：支持相应能力不等于默认显示柄；需叠加 Tool、Scope、锁定、Hidden、权限。所有跨专业例外要以本表为起点扩展，而非维护另一套“基础图元解释”。

# 附录 B｜输入、命中与取消的确定性判定

| 输入或变化 | 预期优先处理 | 不能发生 |
|---|---|---|
| A Hover/Motion | 合法端口、方向箭头、目标高亮 | 创建 MPCConnection |
| B 点击 H1/H2 | 进入 Resize/Rotate，屏幕 DIP 到 World | 激活快捷连接 |
| C 点线端/折点 | Rewire/Waypoint Edit | 同时移动链接图元 |
| D 点 Anchor/Control/参数 | Geometry Edit | 把锚点当选区缩放角 |
| 单击普通图元 | Select/当前 Tool | 任意创建端口 |
| 空白拖动 | Marquee/Pan，取当前工具 | 画出幽灵线路 |
| Key Escape | Cancel 当前 Gesture，清 Preview | 修改 MEM Revision |
| CaptureLost/WindowFocusLost | 取消拖动与预览，记录诊断 | 半提交或残留鼠标捕获 |
| Revision 在 Drag 中改变 | 取消旧 Preview 或安全重新计算 | 用旧 ID/矩阵提交 |
| TAB/Page/Resource/定义切换 | 结束 Gesture，恢复合法选区 | 将移动提交到别的 Page |
| Ctrl/Shift/Alt | 先尊重 Text 输入/Studio 命令焦点 | 多套快捷键同时生效 |
| Preview/Run/Debug | 显示运行只读场景 | 无授权编辑/设备动作 |

推荐采用 TestCenter 中的确定性输入轨迹测试：输入世界/屏幕点、Tool/Selection/Scope、Modifier → 断言唯一 Gesture、命中对象与错误码 → 对照 MEM Revision/对象身份。

# 附录 C｜现有核心 API/源码 Owner 映射（非新接口清单）

| 既有类型或文件 | 真实可复用能力 | V1.2 应扩展而非新造的部分 |
|---|---|---|
| `MRCVisualItem.cs`、`MRCShapes.cs` | Shape/Group 基础工程定义 | Shape 真正参数能力、实例布局 |
| `MRCTransform.cs` 与 FGE | 矩阵、世界局部变换、几何求交 | 非奇异边界、曲线精确分割/命中 |
| `MEMEngineeringSession.Transfer.cs` | `PreviewImport/PreviewDuplicate/Import/Duplicate` | Visual Clipboard 的闭包、外部引用策略、目标落点 |
| `MTCEditSession.Visual.cs` | Group/Ungroup、Lock、Layer、Transform | 复杂嵌套、Undo/锁定反证 |
| `MPCPort.cs` | `MPCPortKind.Visual`、`MPCConnection`、Route/Waypoint | 浮动/固定每端附着语义、旧工程迁移 |
| `MVIVisualEditor.cs` | Create、Connect、Move、TransformSelection、Align、Route、Waypoints、Undo | 垂直/边距分布、连接端口管理与统一错误 |
| `Editing/MVIVisualEditing.cs` | 几何 Handle/Preview、选区 Delta | Arc/Path 全专有参数、OBB 与边界测试 |
| `Editing/MVIVisualSnapping.cs` | 世界网格、对象边/中心、只读 Guide | 稳定候选排序、规模与 DPI 验收 |
| `Editing/MVIVisualAnchors.cs`、`MVIOrthogonalRouter.cs` | 四向固定点、法线与障碍避让 | FloatingOutline、交叉优化、依赖增量 |
| `MVISceneProjection.cs`、`MVISceneItem.cs` | 唯一只读投影、VisualRouteDiagnostic | 投影一致性、局部失效和缓存 |
| `SceneView.cs`、`StudioShell.cs` | WPF 输入/预览与菜单/命令上下文 | 互斥手势、外部鼠标、统一命令 |
| `VisualShapeEditingTests.cs`、`OrganizationWpfTests.cs` | Core 与 WPF STA 回归入口 | GE 子编号覆盖与系统级输入/性能 |

**跨 Owner 强制规则**：必要的新字段要先追溯 MRC/MPC/MEM 快照与序列化、Clone/Transfer 和 Undo；不能只修改单一个 UI ViewModel 就写“功能支持”。

# 附录 D｜需求追溯、边界、文档维护

| 本文章节 | 上位需求 | 代表验收 | 代表工作 |
|---|---|---|---|
| 01～04 | EP02、EP05、EP14/15 | GE-03/08/13/14 | FGE、MRC、MVI 投影与 WPF |
| 05～07 | EP02/03、EP04 | GE-01/02/03/07/08/09/12 | 交互、几何、变换与事务 |
| 08 | EP02/03、EP15 | GE-01/04/05/06/12 | MPC Visual Connection |
| 09 | EP01/02/03 | GE-07/10/11 | MEM 组织、引用、Transfer |
| 10～12 | EP04～EP08、EP13 | GE-09/12/13 | MPM/Studio 入口与运行门禁 |
| 13～14 | EP13～EP15 | GE-13/14/15 | 专业复用、Windows/实机验收 |

- **本文唯一维护**交互行为与数学/数据/验收合同；《#图元体系》维护类归位/现状； Implementation/Testing/GitHub Actions 维护某 SHA 的实际测试和缺口，不在设计主文件每次同步动态“通过几组 CI”。
- 修改一个行为时，先核查是否冲突既有 Owner/EP 需求，再修改本规范对应章节/GE 子号/实际源码/Windows 验收；提交版本必须能区分“批准目标/已实现/已测试”。
- PNG 高清图约定名字为 `CxVisual_工程呈现化体系#图元编辑.png`；本文所嵌仓库图为 A～F 的轻量矢量示意，聊天单独提供原版 1491×1055 PNG。两者都是**拟议设计图**，不是 X3 真实运行截图。
- 下一轮落地顺序遵循“复用已存在的 Owner → 减少重复求解 → 贯通 Studio → 真实工程/鼠标/异常/Windows 验收 → 再优化性能”。禁止空造第二套工程、连接、Undo、Clipboard、交互/运行体系。

**V1.2 设计完成条件**：十四章、四附录、A～F 大图、GE-01～15 及子场景，主要操作具备完整目标/用户动作/Owner/算法/持久化/失败/测试合同；**V1.2 功能完成条件**则依真实源码、同 SHA Windows CI、真实鼠标与工业专业验收另行签收。