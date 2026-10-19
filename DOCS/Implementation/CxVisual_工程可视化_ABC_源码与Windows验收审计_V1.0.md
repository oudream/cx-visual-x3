# CxVisual 工程可视化 A/B/C：源码与 Windows 验收审计 V1.0

> 日期：2026-10-19。依据：用户 A/B/C 工作包和验收案例、仓库内 `CxVisual_工程可视化_工作包A+B_完整实现与真实验收.md`、原 Platform 宪法、实际代码与 Windows CI。
>
> **规格可得性限制**：仓库完整 git tree 中未找到实施文档引用的《CxVisual_统一工程可视化与图元体系_完整架构设计_V1.0》原文，当前可访问项目文件及文件库也没有同名全文。本文可做已知工作包及仓库现有基线的源码/测试审计，**不能冒称已按缺失原文完成最终逐条合规签收**。获得原文后须按原文逐条补全规格 ID、偏差和具体证据。

## 0. 总结与不可变边界

**A 未最终签收、B 未最终签收、C 未最终签收**。不能以按钮存在、单次 Windows Success、单独 WPF STA 或代码中的类名宣称完整工业闭环。
- 工程真源：MEM Project/Page/Content/Symbol/Group/Element、MPM/Aspect/Binding、MTC 原子事务和 Undo/Redo/Save/Open。
- 绘制/输入：Foundation.Geometry 纯值；Mechanism.Visual 视图投影和编辑事务；Presentation.Wpf 仅绘制与用户交互；Studio 组合这些机制。
- 逻辑/构建/运行/数据：MOL（原 Content Logic）、MOC（编译/SourceMap）、MRK（Worker/Debug/Trace）、MDS（运行数据）。禁止新建并行工程树、二次编排格式、全局运行值仓库或设备授权捷径。
- 与用户总原则一致：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标**。开发者自然调用，工程用户不需要手填内部 GUID。

## 1. A：真正可用的图元编辑器

| 验收点 | 当前实现/证据位置 | 现状 |
|---|---|---|
| 九 Shape、Text、Image、受限 SVG、Widget 绘制 | MRCShapes、MVIShapeCatalog、MPWShapeRenderer、SceneView、OrganizationWpfTests | 基础能力已实装、测试覆盖 |
| 路径锚点/控制点、Bezier 精确拆分、Arc、缩放旋转、多轮廓 | FGEPathEditing、MVIVisualEditing、VisualShapeEditingTests | 核心已实装；极端路径/完整实鼠测试不足 |
| 多选、框选、Ctrl/Shift、多对象拖动 | SceneView/CxMoveEventArgs、CxVisualEditor.MoveSelection、StudioViewModel.DragVisualSelection | 已补原子批量世界坐标移动，含嵌套旋转 Group 的专项 Undo/Redo/往返测试 |
| 图层/绘制次序 | CxVisualEditor.SetLayer / SetZOrder、Studio 置顶置底按钮、VisualShapeEditingTests | 已有基本编辑与撤销；图层锁定、隐藏、对象树顺序控制仍需全面 UI 测试 |
| 原子编辑与保存重开 | MEM Session.Edit、Undo/Redo/Serializer | 已复用；完整拖拽设备级实鼠注入测试尚缺 |
| 真实软件工程用户操作 | OrganizationStudioTests WPF STA + UI Automation Peer | 只覆盖部分按钮/视图回调，不能代替真实系统鼠标键盘注入 E2E |

A 仍须重点加固：不同缩放下 Handle 与选区变换、复杂可见层次/锁定、Symbol 双实例编辑边界、跨 UI 真实鼠标注入、恶劣/退化路径和性能压力。禁止将简单命中包围盒当作曲线节点命中。

## 2. B：属性、样式、资源与行为

| 验收点 | 当前实现/证据位置 | 现状 |
|---|---|---|
| MPM 类型/属性/绑定模型 | MRCVisualAppearance、StudioViewModel.Engineering、MEM Aspects | 基础已实装；专业统一 Schema 编辑器尚不完整 |
| 颜色、描边、渐变、资源画刷 | MRCBrushDefinition、MPWShapeRenderer、SceneView.ResourceBrushResolver | 本轮补 WPF 本地 Theme/资源画刷提供者，受限于宿主授权，不自动外部加载 |
| 安全图片/SVG | MPWVisualAssetRenderer、OrganizationWpfTests | 已补 SVG 严格属性/Namespace 白名单、几何非法尺寸拒绝、真实圆角与继承 stroke-width |
| Widget 六种 WPF 实控件 | MPWWidgetPresenter、Studio 配置、TestCenter | 已有基础行为；类型化 Schema、双向验证和复杂状态仍待完整签收 |
| MDS 动态绑定/状态/动画 | MVIVisualRuntimeEvaluator、CxSceneProjection | 核心只读帧与基本动画；状态优先级、触发边界/停止/Theme 规则和失联观察仍须补齐 |
| 事件安全授权 | MVIVisualEventRouter + 专业授权宿主 | 无授权默认拒绝；真实专业命令联锁全链路未签收 |
| 工业电机 Symbol 双实例 | VisualRuntimeProjectionTests、TCT.MVIS.006 | 定义/实例/不同 MDS 运行绑定与持久化基础有测试；完整 Studio 创建设计及现场运行演示未签收 |

B 必须向**统一属性面板（MPM/Schema 驱动）**收敛，而不是不断追加零散输入框。资源与变量选择、绑定源类型、状态/动画/事件/权限、运行时缺失诊断要统一可发现、可理解、可验证。不得在 WPF控件中保存第二份源属性。

## 3. C：同一工程设计/构建/运行/调试

| 验收点 | 当前实现/证据位置 | 现状 |
|---|---|---|
| Studio 可视化自由编排、Port连线、搜索与参数 | MOLFlowCanvas、MOCFlowBuilder、MainWindow、OrganizationStudioTests | 已有有效基础；业务模板/复杂编排完整覆盖仍不足 |
| 工程构建、结构化诊断 | MOCBuildService、MOCSourceMap、FlowDiagnosticList | 已接入；不能把失败草稿当成功工程 |
| MRK 仿真、暂停、继续、单步、断点、取消 | MRKExecutionEngine / MRKExecutionDebugController、Studio | 核心已接入；真实设备/权限/恢复/Watch 与全数据观察待继续验证 |
| 真实 X-Ray 主流程与两个可调用子 Content | Studio `创建完整X-Ray双子流程`、OrganizationLogicIntegrationTests、OrganizationStudioTests | 新增主线与 AI/CV 子流程真实引用、MOC构建及MRK执行测试；现场完整联动待签 |
| MRK 轨迹同步 HMI 状态灯 | Studio MOLVisualTraceBindingResolver + 既有 IMDSExternalBindingResolver / MVIVisualRuntimeEvaluator | 已有按MRK Running/Completed/Failed只读投影，退出清理工程运行覆盖 |
| 轨迹定位父 Invoke 和 HMI 状态灯 | SourceMap CallPath → Studio FlowTraceList → MOLFlowCanvas、FindVisualForFlowBlock | 新增子流程轨迹父调用定位和 HMI 图元反向定位；更深层次内容导航仍不足 |
| 保存/恢复/可追踪及生产授权 | 原 MEM/MOC/MRK/MDS 分工 | 部分验证；完整实机权限、设备异常、全版本回退不视为完成 |

C 的交付标准不止“有两页可切换”：必须在**同一工程、同一对象身份、同一编排真源和同一运行追踪**下完成编辑、验证、构建、启动、观察实时端口/状态、断点、暂停、单步、错误定位、修复、重新构建、恢复/保存重开，且不在运行时把状态覆盖写回工程。

## 4. 本轮新增代码与回归位置

- A：`SceneView.cs`、`CxMoveEventArgs.cs`、`CxVisualEditor.cs`、`StudioViewModel.cs`、`MainWindow.xaml/.cs`；`VisualShapeEditingTests.cs`新增嵌套 Group 批量世界坐标拖动、图层次序和单步 Undo/Redo/磁盘测试。
- B：`MPWVisualAssetRenderer.cs` SVG白名单、合法圆角和 inherited stroke；`SceneView.cs` 本地 Resource Brush；`OrganizationWpfTests.cs` 可视绘制/恶意输入反证。
- C：`MainWindow.xaml.cs` MRK SourceMap 子 Content 的 CallPath 回溯；`OrganizationStudioTests.cs` 补子流程调用节点与对应HMI定位；前序 C 还包括 `MOLRuntimeOverlay.cs` 运行态绑定、`OrganizationLogicIntegrationTests.cs` X-Ray 主/双子流程执行。

每条验收应具备四类证据：**真实 API/源文件、Studio 用户路径、保存重开或运行现场结果、可复现 Windows CI 与反例**。

## 5. Windows 验收规则与未签项

- 只承认同一个实际提交 SHA 的 Windows CI；旧 SHA 只作为历史证据，不能替代最新 HEAD。
- 工程组织 Windows Acceptance 包括真实 WPF STA/Automation Peer，但**外部鼠标注入 E2E**仍未覆盖。
- 统一 Runtime、Config L1、配置独立编译、TestCenter 与工程组织均须核对。非本次提交触发的 workflow 不视作该 SHA 成功。
- 故障应保留真实堆栈并修复，禁止禁用/跳过测试或伪造成功输出。
- 《统一工程可视化与图元体系 · 完整架构设计》原文进入仓库后，需要补建每条规格的 ID/原文/所属项目/代码/测试/运行证据/结论矩阵。

**审计裁决：A/B/C 有真实进展，但尚不能签署“完整完成”或“完全符合缺失的最新架构文档”。**
