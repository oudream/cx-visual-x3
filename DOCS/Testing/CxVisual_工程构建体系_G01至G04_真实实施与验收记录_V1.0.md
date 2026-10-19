# CxVisual 工程构建体系 G01～G04 实施与验收记录 V1.0

> 2026-10-19。该记录继承《CxVisual 工程构建体系 · 长远架构问题与详细完成方案 V1.2》。直接在 master 开发，中文提交，不新增工程项目；以固定提交的真实 Windows CI 为依据。
> 本文件记录的是已完成的核心构建纵向闭环，**不是**宣布 32 能力方面、496 项需求、四类编排模型或十大专业案例全部完成。

## 1. 已实现功能与 Owner

| 阶段 | 已落地 | 主要归属 |
|---|---|---|
| G01 自由逻辑编辑 | V2 Content Logic，BlockId/LinkId、Port类型、Control/Data Link、原子Session提交、合法编辑草稿/完整可执行校验分离、添加/删除Port、修改能力、指定入口、Undo/Redo、Revision冲突拒绝、V1兼容 | Mechanism.Core；MOCFlowBuilder在Mechanism.Orchestration |
| G02 能力发现 | IMOCCapabilityProvider、MOCCapabilityDescriptor、MOCCapabilityCatalog；检索/分类/版本号/Port声明/运行Provider可用性，Builder类型化Step入口 | Mechanism.Orchestration；实际能力归Professional和IMRKCapabilityResolver |
| G03 检查构建 | MOCBuildService、MOCBuildResult、MOCDiagnostic、MOCDiagnosticSeverity；拒绝无效草稿/未知能力/不匹配输入；使用既有MOCEngineeringCompiler与MRKPlan，诊断含ContentId、可用时BlockId/LinkId | Mechanism.Orchestration；MRK仍唯一执行Owner |
| G04 Studio编辑 | 搜索目录、选择能力、插入未连线节点、两块选定后选择兼容Port对、Control/Data连线、删除连接/块、撤销重做、验证诊断、CPU仿真及同图状态、保存重开 | Applications.Studio + Mechanism.Presentation.Wpf + Mechanism.Visual |

统一对象根基仍为 MEMProject/Content/Logic Aspect，Visual 仅为可交互投影。MOLBlockEndpoint 不新增 MEMNode；MOLLink 与公开资源间的 MPCConnection 范围不同、互不重复保存；MRK执行状态与MDS运行数据不变。

## 2. 客观测试证据

- G01：工程组织CI成功 https://github.com/oudream/cx-visual-x3/actions/runs/39206224893 。
- G02：工程组织CI成功 https://github.com/oudream/cx-visual-x3/actions/runs/39206603007 。
- G03：工程组织CI成功 https://github.com/oudream/cx-visual-x3/actions/runs/39206723343 。
- G04：包含 Control/Data 真实 WPF Studio 自动化、保存重开与身份检查的工程组织CI成功 https://github.com/oudream/cx-visual-x3/actions/runs/39208117405 ，对应代码提交 `0b5e9a8db5c7f061f11f5d632e72fcdff9a67446`。
- 测试范围：ArchitectureSmoke 的 `OrganizationLogicIntegrationTests`、`OrganizationStudioTests`、`OrganizationWpfTests`，包括 Core 草稿校验、端口正反例、编译MRK/SourceMap、真实文件往返、WPF STA界面操作。
- 相关旧结构化 V1 Logic Codec和编译路径保留，旧工程没有自动重写为新Schema。

## 3. 不得混淆的完成边界

1. V2 首批语义是 **有向无环的 Control/Data Action**；复杂 If/Switch/Loop、动态Region、Join、DataFlow、Workflow、StateMachine、跨模型调用仍需后续专门实现。旧V1结构化语义可继续使用，不能误认为V2已覆盖它们。
2. Studio已能选块匹配类型化Port，但尚非完整鼠标从端口圆点拖线、缩放路由、任意布局、框选多节点/提取子流程/编辑全部控件属性的生产级编辑器。
3. 当前示例是 CPU 模拟 Provider；实际 X-Ray/PLC/运动/CT/MES 等设备与工业安全互锁未在此处完成实机验收。
4. G02的运行可用性只表示 Provider 可解析，**不等于** G07 的发布能力版本、配方/设备依赖锁定和授权。
5. G03错误有Content/Block/Link定位字段，但部分通用校验仍仅能定位Content，不应宣称所有错误已有精确LinkId。
6. 独立的 TestCenter Windows 工作流还需单独检查；工程组织CI成功不等于整个TestCenter和全平台验收成功。

## 4. 下一轮阶段与严格完成标准

- **G04深化（Studio完整编排）**：画布端口几何位置、鼠标拖连线、端口属性/参数编辑、节点排布/缩放/多选/框选、选区提取子流程、诊断定位和逐项Undo/Redo；必须有真实WPF操作、保存重开、错误回退证据。
- **G05a 控制模型**：V2 If/Switch、Join、动态Region、For/While/Foreach、Break/Continue/Try、结构化/自由互转；高迭代数不允许简单静态展开突破MRK预算。
- **G05b 数据流**：具名端口输出、多生产者显式聚合、流式大图、背压/队列/取消/生命周期和跨实例数据隔离。
- **G05c Workflow**：持久等待、人工活动、重试与补偿、进程恢复，故障注入严禁重复危险设备动作。
- **G05d StateMachine**：事件、层级状态、复合状态与并行状态的确定性校验。
- **G06～G08**：真实专业Provider、工程发布/版本锁定、十案例与五历史系统的可重复验收。不要为不同执行语义建立第二棵工程树或第二套调度内核。

**后续工作继续遵守三方面及28个既定项目组织，不新增项目，所有阶段先测后编、CI未通过不得宣称完成。**

## 5. 2026-10-19 G04 深化两批实际实施增补（原 G01～G03 不重做）

### 5.1 G04-A：端口可视化、直接拖连线

- 源码：`Mechanism/Presentation.Wpf/MOLFlowCanvas.cs`、`Mechanism/Visual/MOLFlowLayout.cs`、`Applications/Studio/MainWindow.xaml.cs`。
- 在真实WPF画布中依据原有 `MOLWiringDefinition.Ports` 绘制类型化Port：Control蓝色、Data橙色，拖拽有预览线。输出到输入自动选择明确的PortMemberId；旧的双节点选中+端口列表模式仍保留。
- 画布调用Core现有 `MOLWiringDefinition.Validate()` 做方向、契约、重边、多重度及环路拒绝，随后经 `MOCFlowBuilder.Edit` 及 `MEMEngineeringSession.Edit` 原子保存。不另外保存Graph或WPF状态。
- 真实Studio STA回归覆盖画布端口坐标命中、错误起点/终点拒绝、选择端口精确连接、Undo/Redo。测试入口 `Tests/ArchitectureSmoke/OrganizationStudioTests.cs`；WPF自动化通过与鼠标事件共用的画布交互方法执行，非真实硬件鼠标轨迹注入。
- 第一轮提交 `7cc19d030ae94aacdc498b5571c2accba823dfa0` 缺少LINQ命名空间，首轮Windows编译失败；已以中文提交 `a782d94ff96f154afef2b078a33fda8f2070aad3` 修正并重新验收。
- **真实Windows工程组织CI成功**：[39210904966](https://github.com/oudream/cx-visual-x3/actions/runs/39210904966)。

### 5.2 G04-B：节点拖动、布局投影及工程持久化

- 源码：`Mechanism/Core/Engineering/MOLLogicDefinition.cs`（`MOLBlockPosition`、可选`Positions`、V2 Codec）；`Mechanism/Orchestration/MOCEngineeringCompiler.cs`（`MOCFlowBuilder.Move`）；`Mechanism/Visual/MOLFlowLayout.cs`；`Mechanism/Presentation.Wpf/MOLFlowCanvas.cs`；`Applications/Studio/MainWindow.xaml.cs`。
- 拖动节点时画布有位置预览，连接端口与边跟随。结束拖动后调用公共 `MOCFlowBuilder.Edit(session,content,b=>b.Move(blockId,x,y))`，所有位置写入Content原有Logic Aspect V2；开发者使用同一个Builder API。
- V2块在需要时增加可选 `x/y` 属性；没有坐标的原V2文档继续默认自动排布。位置采用InvariantCulture、有限非负坐标，拒绝NaN、Infinity、越界和未知BlockId。后续增删/连线必须保留其他块的坐标。
- 测试：`OrganizationStudioTests` 中真实WPF画布实例的节点拖动、Undo/Redo和最终文件Save/Open；`OrganizationLogicIntegrationTests` 的兼容Codec、合法/非法位置、Revision不变、Builder增量编辑及实体文件往返。
- 实现提交 `923e177321182876c063157e0cc419464a0d8653`；**真实Windows工程组织CI成功**：[39211521133](https://github.com/oudream/cx-visual-x3/actions/runs/39211521133)。配置L1及统一运行CI对应提交也成功。

开发者实际使用入口（示意，运行时使用同一Content）：

```csharp
MOCFlowBuilder.Edit(session, content, b =>
{
    Guid acquire = b.Step("Acquire");
    Guid inspect = b.Step("AI");
    b.Connect(acquire, "Done", inspect, "Start", MPCPortKind.Control);
    b.Move(inspect, 340, 140);
});
```

### 5.3 G04-A/B时点的未完成范围（历史边界，G04-C/D见第6节）

1. 尚未实现缩放、自动路径避障、节点碰撞规避、框选多选/组合、剪贴板、可复用子Content提取、子流程公开Port及完整参数属性编辑；不能将G04整体标记完成。
2. Port拖拽/节点拖动采用真实WPF控件事件实现，但自动化回归通过公共WPF交互方法模拟坐标，完整鼠标设备注入、DPI/缩放和大图性能仍需后续验收。
3. 布局只是V2 Logic的可选展示坐标，不改变MOC/MRK/MDS行为，不引入第二套工程真源；未迁移或修改V1结构化Logic格式。
4. 两批测试均是CPU与WPF工程验收，不代表真实X-Ray、CT、PLC或运动互锁实机验收。
5. 工程构建体系后续继续G04全量和G05各独立执行语义，不得以现有有向无环Action代替动态循环、数据流、Workflow或StateMachine。

## 6. 2026-10-19 G04-C/D画布编辑与批量工程事务——持续实施与复核

> 接续5.1/5.2的真实代码；不得将此处的编辑组合`MOLSelectionGroup`当作G04-F的可执行子Content。所有工程改动都通过现有MEMEngineeringSession与MOCFlowBuilder.Edit，缩放、滚动与临时选区则不写持久逻辑。

### 6.1 G04-C：画布交互

| 功能 | 归属/实现 | 关键约束 |
| --- | --- | --- |
| Zoom 25%～300%、100%还原、适配画布 | `MOLFlowCanvas.ZoomFactor`、`Studio.OnFlowZoom/Fit` | WPF LayoutTransform，只改变视口，不改变Block坐标及MOC/MRK执行 |
| Ctrl+滚轮缩放、中键拖动平移、选区定位 | `Applications/Studio/MainWindow.xaml[.cs]` | 视口ScrollViewer Offset；不产生MEM Revision |
| 空白框选、Ctrl增减选、Shift追加、全选 | `MOLFlowCanvas.SelectRectangle/SelectBlock/SetSelection` | 仅选择已存在的BlockId；外来ID拒绝 |
| 多节点一起拖动，拖动预览及Esc取消 | `MOLFlowCanvas` + `OnWiringBlocksMoved` | 拖动结束统一`MOCFlowBuilder.MoveMany`，一笔事务；预览不持久化 |
| 对齐、水平/垂直均匀分布、自动矩阵布局 | `MOCFlowBuilder.Align/Distribute/ArrangeGrid`与Studio按钮 | 布局使用实际Port计算高度；位置变化可Undo/Redo；不影响Link |
| 重叠对象鼠标命中 | `MOLFlowCanvas` | 按绘制栈最顶层优先选取；保留原有类型化Port拖线 |

### 6.2 G04-D：剪贴板及组织操作

| 功能 | 归属/实现 | 正反例 |
| --- | --- | --- |
| 多节点复制、粘贴、跨Content复制 | `MOCWiringFragment.Capture/ToPayload/FromPayload`、`MOCFlowBuilder.Paste` | 原Block/LinkId不重复使用；只复制选区内部边；外部边不附带 |
| Windows剪贴板/进程内回退 | Studio `OnFlowCopy/Paste` | V2统一Codec，4MB限额、禁DTD；剪贴板不可用仍可本会话粘贴 |
| Group/Ungroup | `MOLSelectionGroup`、Logic V2可选`Groups`、Builder编辑 | 相互重叠的组拒绝；清除成员时自动收敛组；组不产生额外执行节点 |
| 批量删除与清空草稿 | `MOCFlowBuilder.RemoveMany`、Studio `删除逻辑选区` | 可删到0个块；V2空草稿保存与Undo/Redo可用，MOC构建必拒绝 |
| 组合移动、对齐、分布和Undo/Redo | `MOCFlowBuilder.Edit` + Session | 一次点击一笔原子事务；异常不应增加Revision或破坏工程 |
| 常用快捷键 | Ctrl+A/C/V/Z/Y，Delete，Esc | 仅在逻辑Tab响应，输入框编辑与逻辑快捷键隔离 |

- 核心代码：`Mechanism/Core/Engineering/MOLLogicDefinition.cs`（可选组与空草稿）；`Mechanism/Orchestration/MOCEngineeringCompiler.cs`（剪贴板、复制/批量编辑/对齐、组合）；`Mechanism/Presentation.Wpf/MOLFlowCanvas.cs`（画布）；`Applications/Studio/MainWindow.xaml[.cs]`（命令绑定）。
- 真实测试：`Tests/ArchitectureSmoke/OrganizationLogicIntegrationTests.cs`，`Tests/ArchitectureSmoke/OrganizationStudioTests.cs`。覆盖非法坐标/未知ID/组交叠拒绝、剪贴板恶意负例、跨Content复制重映射、保存重开、WPF拖动、选区缩放、布局和撤销重做。
- 中间提交曾出现失败：布局命令引用错误、测试对布局顺序的错误假设，以及同名`删除选中`误点资源操作；均已修复。不能引用单次中间失败作为最终成败判断。
- 已独立确认`d8402e967fb6a1fe2c76f508b7ec5cc413833c0e`工程组织Windows CI成功：[39224417427](https://github.com/oudream/cx-visual-x3/actions/runs/39224417427)，统一运行体系TDD成功：[39224417460](https://github.com/oudream/cx-visual-x3/actions/runs/39224417460)。
- 后续针对空草稿粘贴恢复及Esc取消预览新增回归（提交`b3eeeb7d`、`dca5d696`、`42c6bd4d`），**新增回归是否通过以这些提交对应的真实Windows CI为准，未通过前不得直接认定其已验收。**

### 6.3 严格边界与后续工作

1. 当前组是**编辑层组合**，不是可执行Content，不存在公开Port、跨Content参数映射或子流程调用；G04-F需单独交付和验收。
2. 尚未以物理鼠标/键盘注入、大规模节点画布性能、复杂缩放DPI组合、自动正交连线路由证明生产级交互质量；现有WPF STA测试通过真实窗口与公共交互入口验证。
3. G04-E参数/Port契约属性面板、G04-F复用子Content、G04-G调试及精确定位、G05a～d执行语义均未因G04-C/D完成而自动通过。
4. 新增可选V2 `Groups`和`x/y`仅为兼容性扩展；老V2无组/位置字段仍应可读取；V1结构化Logic仍走原路径。
