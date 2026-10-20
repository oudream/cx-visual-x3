# CxVisual Studio — AvalonDock 5.0.1 / .NET Framework 4.8 WPF 验证例程（V1.1 命名空间修复）

> 版本 V1.0 · 2026-10-20 · **独立 PoC，不修改主仓；没有在本环境完成 Windows 编译/运行，因此所有动态测试均标记待验证。**
>
> 本例使用 **Dirkster99/AvalonDock 5.0.1 正式版**，不使用旧的 Xceed 包、未发布的 5.0.2-preview 或 .NET 6/8/9。固定 3 个同版本 NuGet 包，避免引用旧 `AvalonDock.Layout.Serialization` 命名空间。


## V1.1 修复：CS0234 DockingManager（2026-10-20）

- 现象：VS 在 `MainWindow.xaml` 使用 `dock:DockingManager` 时报告 `CS0234: 命名空间 CxVisual.AvalonDock 中不存在 DockingManager`。
- 根因：PoC 原代码命名空间为 `CxVisual.AvalonDock.PoC`；WPF 生成代码中出现的 `AvalonDock.DockingManager` 被解析成了当前应用的 `CxVisual.AvalonDock.DockingManager`。**不是 AvalonDock 5.0.1 缺少 DockingManager**。
- 修复：全部 C# `namespace` 和 `using ...Views`、WPF `x:Class`、项目 `<RootNamespace>` 一致改为 `CxVisual.DockingPoC`。**不改变 NuGet 包或官方 XAML XMLNS**；原 `.sln` / `.csproj` 文件名及 EXE 名保持不变。
- 如果曾打开 V1.0，请用 V1.1 完整源码覆盖旧目录后关闭 VS，删除项目本地 `bin`、`obj` 目录并重新 NuGet Restore / Rebuild。旧 `obj` 的 `MainWindow.g.cs` 不应混入新生成代码。
- 此轮在 Linux 容器仅完成源码与 XML 静态核查；没有执行 Windows MSBuild/WPF，不把编译结果宣称为通过。


## 1. 系统要求

- Windows 10/11 x64；Visual Studio 2022（.NET 桌面开发）与 .NET Framework 4.8 Developer Pack。
- 联网还原 NuGet 包：`Dirkster.AvalonDock` **5.0.1**、`Dirkster.AvalonDock.Serializer.Xml` **5.0.1**、`Dirkster.AvalonDock.Themes.VS2013` **5.0.1**。
- C# **7.3**、传统非 SDK `.csproj`、WPF、x64。其余包通过 NuGet 传递引用，不复制 DLL 进源代码仓。

## 2. 如何启动

打开 `CxVisual.AvalonDock.PoC.sln`，选择 `Debug | x64`，等待自动 NuGet Restore，然后按 **F5**。

或在“VS 2022 Developer PowerShell”中执行：

```powershell
.\BuildAndTest.ps1 -Configuration Release
```

它使用真实 Windows MSBuild 执行 restore/build，再启动真实 WPF STA 自测（10 项），在 `Tests/SelfTestResult.txt` 输出 PASS/FAIL；缺失运行时、包还原失败、异常、零用例均不得写为通过。

只编译：

```powershell
.\BuildAndTest.ps1 -Configuration Debug -BuildOnly
```

也可以双击生成的 `src/CxVisual.AvalonDock.PoC/bin/Release/x64/CxVisual.AvalonDock.PoC.exe`。

**首次联网还原失败：** 检查 NuGet.org 源、企业代理和 VS 的 .NET Framework 4.8 targeting pack。不得将 4.x 的程序集硬拷进去绕过版本。

## 3. 界面与操作

- **中央主工作区**：配置（2×N 卡片）、图元（HMI 示意）、流程（主/AI/CV）。点击标签或工具条「打开配置/图元/流程」。
- **左侧**：资源树、对象树、工具箱；**右侧**：属性/绑定；**底部**：诊断、日志。
- 工具条：资源树自动隐藏、属性浮动/停靠/关闭/重开、保存/恢复/重置布局、主题切换、工程 Revision 隔离测试、DPI 读取。
- 也可鼠标拖拽标签，浮动到独立窗口，或使用面板标题栏图钉与关闭按钮。
- 用户布局保存在 `%APPDATA%\CxVisual\AvalonDock-PoC\workspace.layout.xml`。它**不是工程文件**，不携带真实数据、令牌、运行句柄和工程 Revision。
- 点击「模拟编辑工程 (+Revision)」才会增加演示 Revision；布局的各种操作 **不应**改变这个数字。这里的 Revision 只是一个测试替身，不是另建的 MEM 实现。
- UI 的设备/运动/曝光功能均未接入，所有编辑区域是内容模型示例，**不能当成完整 Studio**。

## 4. 验收证据

`Tests/DockingSelfTests.cs` 由 `--self-test` 在 **真实 Windows WPF STA** 执行，10 项分别覆盖：

T01 九项 ContentId / 多文档；T02 双主题；T03 XML 布局序列化、恢复与 Revision 不变；T04 工具面板关闭重开；T05 多文档关闭重开；T06 浮动/停靠与自动隐藏模型；T07 错误、DTD、超长 XML 拒绝；T08 默认布局重置；T09 布局变化与主动工程 Revision 分离；T10 DPI 查询。

**浮动拖拽、跨显示器 DPI、窗口焦点、工作区视觉、性能以及用户操作习惯，必须按 [`Docs/人工验收与证据矩阵.md`](Docs/人工验收与证据矩阵.md) 进行手测。** 自动化脚本不能证明全部交互通过。

## 5. 与 CxVisual 正式架构的关系

| 本例 | 接入 CxVisual 时真实 Owner |
|---|---|
| DockingManager / UI Layout / 主题 | Applications.Studio 仅存放用户工作区状态 |
| DemoEngineeringState | **仅替身**；正式替换为 MEM Session + MPM 属性，不能作为第二工程树 |
| 配置 / 图元 / 流程页面 | 原 Studio / Mechanism.Visual / Presentation.Wpf / MOC Editor |
| 诊断和日志列表 | 原 MRK Trace / MOC Issue / Foundation logging 的只读视图 |
| 布局 XML | AppData 工作区设置，不是 MEM 工程快照，不参与 Revision、Undo/Redo |

正式接入时，只保留 Docking 视图层与布局仓储，把 `DemoViewFactory` 和 `DemoEngineeringState` 替换为原有实际对象与控件。不要将 `.xaml` 布局当成逻辑编排文件。

## 6. 代码清单

- `MainWindow.xaml`：Docking 布局与操作按钮；`MainWindow.xaml.cs`：真实 UI 操作、ContentId 映射、主题与保存恢复。
- `LayoutPersistence.cs`：独立布局目录、输入限制、拒绝 DTD、原子写文件。
- `WorkspaceIds.cs`：稳定面板/文档 ContentId；`DemoEngineeringState.cs`：业务版本隔离替身。
- `Views/DemoViewFactory.cs`：全新构造 WPF Demo Views；`Tests/DockingSelfTests.cs`：WPF STA 10 项。
- `BuildAndTest.ps1`：Windows 编译和测试；`.github/workflows/avalondock-net48-windows.yml`：独立项目放 GitHub 根目录后可触发 CI。

## 7. 技术依据

- 正式发布： https://www.nuget.org/packages/Dirkster.AvalonDock.Themes.VS2013/5.0.1
- 官方仓库： https://github.com/Dirkster99/AvalonDock
- 快速入门： https://dirkster99.github.io/AvalonDock/getting-started/quick-start/
- 持久化教程： https://dirkster99.github.io/AvalonDock/tutorials/layout-persistence/
- v5 迁移差异： https://dirkster99.github.io/AvalonDock/migration/breaking-changes/
- 主题： https://dirkster99.github.io/AvalonDock/tutorials/styling-and-theming/

开源许可见原官方 NuGet 的 license metadata；分发商用软件时请按实际依赖包逐项审计 MS-PL / Apache 2.0 许可及第三方 Notice。
