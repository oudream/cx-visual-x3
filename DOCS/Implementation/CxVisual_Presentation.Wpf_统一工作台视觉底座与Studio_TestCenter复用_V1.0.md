# CxVisual Presentation.Wpf ——统一工作台视觉底座与 Studio/TestCenter 复用 V1.0

> 日期：2026-10-26。主仓库：cx-visual-x3；本轮明确聚焦 WPF 外观 Owner，不重构正式工程、配置或测试运行内核。

## 1. 架构裁决

- `Mechanism/Presentation.Wpf` 是**共用 WPF 外观与呈现能力 Owner**：浅/深主题语义、字体/间距、控件基础样式与模板、可复用矢量图标、文档/工具页签外观。
- `Applications/Studio` 仍唯一管理 MEM 工程、资源树、Ribbon 命令语义、工程编辑与调试、Studio 动态编辑文档和 AvalonDock 布局。
- `Tests/TestCenter` 仍唯一管理四大根/已创建实例、ManualSession、Catalog/Run/Attempt、模块 MHS 配置、测试命令、结果/证据及自身 AvalonDock 布局。
- 此视觉底座**不依赖 AvalonDock 5.0.1**：Dock Header 使用只读取 `Title`、`IsSelected` 的数据模板；具体 DockingManager 与主题包继续由应用持有。
- `Presentation.Wpf` 仅接受 WPF 的 `ResourceDictionary` 与显示参数，**不允许反向引用 Studio/TestCenter，且不拥有业务模型、状态机、运行线程或持久化项目**。
- 现阶段只面向 .NET Framework 4.8 / Windows WPF；不能据此声称 WPF 已可运行于 Linux/macOS。

## 2. 当前唯一的公共资源入口

| 文件/类型 | 职责 |
|---|---|
| `Mechanism/Presentation.Wpf/Themes/MPWVisualPalette.xaml` | 浅色基准色、字体、前景/背景/边框/选中/告警视觉令牌 |
| `Mechanism/Presentation.Wpf/Themes/MPWWorkspaceControls.xaml` | Button、TextBox、ComboBox、TreeView、DataGrid、TabItem 的具名基准样式；AvalonDock 数据模板 |
| `Mechanism/Presentation.Wpf/Themes/MPWWorkspaceVisuals.cs` | `ApplyTheme(ResourceDictionary, bool)` 统一更新 MPW 令牌；`TreeHeader(...)` 创建字体无关矢量标题 |

XAML 加载顺序：

```xml
<ResourceDictionary.MergedDictionaries>
  <ResourceDictionary Source="/CxVisual.Mechanism.Presentation.Wpf;component/Themes/MPWVisualPalette.xaml"/>
  <ResourceDictionary Source="/CxVisual.Mechanism.Presentation.Wpf;component/Themes/MPWWorkspaceControls.xaml"/>
</ResourceDictionary.MergedDictionaries>
```

关键稳定 Key：`MPWBodyTextStyle`、`MPWSectionHeadingStyle`、`MPWTreeStyle`、`MPWTreeItemStyle`、`MPWButtonStyle`、`MPWInputStyle`、`MPWComboStyle`、`MPWDataGridStyle`、`MPWDocumentTabStyle`、`MPWDockHeaderTemplate`。

## 3. 两应用真实接入方式

- Studio 的 `App.xaml` 首先导入以上共享字典，随后加载 `Studio/Colors.xaml` 与 `Studio/Styles/Controls.xaml`。已有 Studio 自定义 Hero/Ribbon、工作区内容和主题别名仍保留，Button/TextBox/TreeView 原生基础样式 `BasedOn` 公共 MPW。
- Studio Dock 的 Document 与 Anchorable Header 引用同一 `MPWDockHeaderTemplate`；Studio 资源树继续从 MEM 投影，Header 由公共矢量工厂绘制，`Tag` 是原有资源身份，不创建第二棵工程树。
- TestCenter 删除 `App.xaml` 中重复的 Tab/Dock 模板声明，改为加载上述字典，并通过隐式继承把标准 Button、TextBox、ComboBox、TreeView、DataGrid 等接入共享样式。文档 Tab 直接绑定 `MPWDocumentTabStyle`。
- TestCenter 树的类型、右键菜单、创建实例/保存/重开/删除逻辑仍由本项目维护，公共图标只接收标签与类别语义。
- 两端浅/深主题入口调用 `MPWWorkspaceVisuals.ApplyTheme(Application.Current.Resources, dark)`，再分别更新应用级特有画布、Ribbon、Dock 第三方主题；视觉改变绝不写入工程/测试数据。

## 4. UIA 与视觉验收边界

必做检查：

1. Presentation.Wpf 在 Windows .NET48 编译通过，Studio/TestCenter 不增加项目，也不引入反向依赖。
2. Studio S1 STA：两主题切换、公共 MPW 色值、Document/Anchorable Header 模板共享、工程 Revision 与 ContentId 不变。
3. TestCenter Windows：公共样式真正加载，Overview 与多实例 Tab/右键/保存重开操作不回退，真实 UIA 与完整自动回归通过。
4. WPF 布局：100/125/150% DPI、ClearType、标签选中/未选中/关闭/拖拽、长中文标题截断、焦点与 UIA。
5. 必须用实际 Windows 截图逐页和目标图人工对比；CI 通过不等于“视觉效果最终被用户接受”。

## 5. 明确不迁移的专业内容

Studio 的专业 Ribbon 图标选择、MEM 资源编辑面板、二维/三维专业 Viewer、Visual Geometry 画布及其 Command；TestCenter 的 Catalog、Run/Attempt、MHS 配置编辑语义、Manual Channel/Module 和测试证据等，**不是可复用视觉模板，不能为了合并而迁进 MPW。**

当前 `MPWConfigEditorControl<T>`、`MPWImageViewerControl`、体绘制等原本就在 Presentation.Wpf，继续原位复用。其内部仍有部分动态 C# 视图代码；后续外观精修应优先改进正式控件的 DataTemplate/Style，而不是在 Studio/TestCenter 另外写一份表单。

## 6. 防回退原则

共享颜色、图标、按钮、文本框、工作区 Tab **不得再分别复制**到 Studio 或 TestCenter；新增专业布局仅组合公共视觉组件，仍由应用 Owner 决定操作可用性。缺失真实 Owner 或未执行的 Test 不得显示伪状态、伪结果。每次涉及公共样式变更必须至少执行 Studio S1 与 TestCenter Windows 两项 CI，并保存实际截图证据。
