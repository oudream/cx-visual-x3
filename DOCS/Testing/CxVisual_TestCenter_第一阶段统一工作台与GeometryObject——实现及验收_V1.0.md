# CxVisual.TestCenter 第一阶段：统一WPF工作台与Geometry/Object——实现及验收 V1.0

> 日期：2026-10-21；**唯一技术主规格**：`DOCS/Architecture/CxVisual_测试体系——完整规格 V4.1.md`。设计图：`DOCS/Architecture/TestCenter/01～03`。本文件区分设计、源码、自动验收、真实Windows UIA、人工外观及工业现场，不把源码类数当作完成证据。

## 1 已验证的代码和CI

- 第一阶段固定源码（含开发者文档）Commit：`ab4e85c71574a6c3bda911338a749e07621de5fb`（master）。
- Windows 2022 / .NET Framework 4.8 / x64 TestCenter CI：[39591953493](https://github.com/oudream/cx-visual-x3/actions/runs/39591953493)，**completed/success**，精确匹配上述Commit。
- Catalog发现：**125**个Case，包含工作流新建的3个NUnit测试。隔离执行、NUnit状态负例、Worker超时/崩溃/进程树反证、冷启动Owner回放均通过。
- **三轮并发回归375个Attempt通过**；History/Artifact SHA256校验及Junit/JSON证据由上述工作流实际产出。
- WPF `--ui-smoke`通过，`--ui-automation`执行真实UIA ValuePattern/SelectionItemPattern/InvokePattern，连续运行Geometry与MEM Symbol两个Owner案例、切换ObjectTree页、检验上下文Ribbon并上传4张实际WPF截图。Actions工件：`TestCenter-WPF-UIA`（Workspace、Geometry、Object、Dark）。
- 上述CI不证明人工图形视觉验收，也不证明未接入的Graph/Channel/Runtime或实体工业设备已经验收。

## 2 第一阶段真实功能

| 功能 | 现有源码证据 | 软件执行结果 |
|---|---|---|
| 单一Shell | `MainWindow.xaml(.cs)`、`TCTControls.cs`、AvalonDock 5.0.1 | 8个可停靠面板、动态上下文Ribbon、浅深主题、布局保存/恢复 |
| Explorer/Inspector/Results/Timeline | `TCTControls.cs`、`TCTTestTimelineControl.cs` | 单/多选，Worker Run/Attempt到断言、Owner、源码、Timeline关联；取消/重跑/报告 |
| History/Suite Plan | `TCTTestHistoryControl.cs`、`TCTTestPlanControl.cs` | 存档、完整SHA256回放、双Run对比、Plan保存重开/执行；不建第二Runner |
| Geometry | `TCTGeometryEvidence.cs`、`TCTGeometryEvidenceCanvas.cs`、`TCTGeometryTestView` | 真正FGERect2D/SceneProjection，Input/Expected/Actual/Diff图层、坐标空间/单位、点位容差、缩放平移、重叠实例选中 |
| Object/Symbol | `TCTEngineeringSnapshotCodec.cs`、`TCTObjectReferenceDiagram.cs`、`TCTObjectTestView` | 真正MEMProjectSnapshot，Ownership树/实线、Reference虚线、Symbol双实例MRIObjectAddress定位，双Revision Diff |
| 可信证据比较 | `TCTTestResultRepository.CompareArchived`、`TCTTestDiffControl` | 双档Run/Attempt/Schema/Artifact验证后按TestId+Iteration比对；跨Commit/OS不冒充同环境回归 |
| 正反用例 | `Cases/TCTGeometryEvidenceCases.cs`、`TCTArchiveComparisonCases.cs`、`TCTRealCases.cs` | NaN/非法单位及容差、未知Schema、DTD、损坏SHA、不同实例地址、相同Run拒绝 |

## 3 分层完成状态

| 证据等级 | 状态 | 依据/限制 |
|---|---|---|
| 设计已确定 | 已确定 | V4.1，01～03设计图，三阶段划分 |
| 代码已实现 | 本阶段上述范围已实现 | 精确提交及源码；Geometry/MEM真Owner |
| Windows编译/自动测试 | **通过** | CI 39591953493 |
| Windows WPF自动交互/UIA | **通过（CI声明的自动场景）** | 同次CI真实UIA两Owner及4张截图，不代表全键盘/DPI组合 |
| 人工界面视觉/交互验收 | **待签认** | 对照01～03图核对DPI 100/150/200、拖拽停靠、快捷键、视觉密度、错误引导 |
| 工业设备/HIL | **未开展** | 需要专业Owner、实机授权和现场证据；不在第一阶段硬性软件验收中 |

## 4 下一步放行事项

1. 按`Tests/TestCenter/README.md`末尾步骤核对01～03与Actions四张真实UI截图，包括多实例重叠、Reference边点击、Diff、停靠及主题；人工差距须真实修复而非只改描述。
2. 整体V4.1剩余阶段二：MOL/Graph/Runtime/Channel真实Owner Viewer；阶段三：Fixture/Golden正式授权、Suite/Plan高级编排、八维覆盖、长稳性能、六Workbench/HIL。
3. 继续核对快速变化的`master`：上述成功只对固定代码SHA生效；之后并行Studio/机制提交需以其最新对应CI判断，不能无条件沿用。

**阶段一软件实现与列出的自动验收已达标；阶段一人工界面验收尚待确认，不得标作“全部最终完成”。**
