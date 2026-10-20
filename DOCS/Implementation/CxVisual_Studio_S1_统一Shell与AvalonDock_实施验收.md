# CxVisual Studio S1：统一 Shell 与 AvalonDock 接入（实施与验收）

## 1. 依据、范围及事实等级

- 需求：`CxVisual Studio 统一界面架构与完整设计规范 V1.2`；`DOCS/界面设计` 中 00～09 原图。视觉图不是已实现功能证明。
- 复用：`Hello/AvalonDock` 已有5.0.1 XAML/主题/安全布局XML/10项WPF自测思路。Hello仓库记录仅静态核查，不将其等同主程序Windows验收。
- 本阶段：沿用 `Applications/Studio/MainWindow`、`StudioViewModel`、`SceneHost` 和 `LogicHost`；新增顶部九固定项+动态菜单同一行、下一行Ribbon、AvalonDock工作区、HMI/流程文档、可停靠工具、主题、布局独立持久化。
- 底层：保留既有MEM工程事务，MOL自由连线，MOC/MRK/MDS由原Owner控制。布局/主题/文档激活不得产生新的工程Revision。
- 下阶段：Schema自动配置2×N真实生成器、所有专业Ribbon命令、完善首页/工程文件页、细分专业Viewer、不在S1宣称完成。

## 2. 代码归属

| 路径 | 内容 | 约束 |
|---|---|---|
| `Applications/Studio/MainWindow.xaml` | 统一Shell、菜单、Ribbon、工具面板、原HMI/流程编辑器 | 原编辑器继续使用实际SceneView/MOLFlowCanvas |
| `Applications/Studio/StudioShell.cs` | LayoutDocument/Anchorable、ID、主题、布局保存恢复、菜单上下文 | 无任何新的MEM工程模型或运行引擎 |
| `Applications/Studio/StudioShellSelfTests.cs` | Windows真STA自测 S1-01～08 | 失败必须进程ExitCode!=0 |
| `Applications/Studio/StudioViewModel.Shell.cs` | Shell级故障文本接入已有Status | 不修改Revision |
| `Applications/Studio/App.xaml.cs` | 非交互 self-test 入口 | 日常启动仍进入MainWindow |
| `.github/workflows/studio-shell-windows.yml` | Windows完整解决方案 + S1 STA + 结构检查 | 以对应commit的workflow实测为准 |

## 3. 使用方式

1. Windows VS2022 + .NET Framework 4.8 Developer Pack。打开 `CxVisual.Unified.sln`，Restore NuGet依赖 `Dirkster.AvalonDock`、`Dirkster.AvalonDock.Serializer.Xml`、`Dirkster.AvalonDock.Themes.VS2013`（均5.0.1）。
2. 启动 `CxVisual.Studio.exe`。顶部是固定9项 + 文档动态菜单，中央主页/HMI/流程在同一DockingManager下，不出现第二层固定导航。
3. 打开“视图”可操作工具面板、保存/恢复/重置布局；点击右上“浅色/深色”切换主题。工作区保存在 `%APPDATA%\CxVisual\Studio\workspace.layout.xml`，**不在工程保存中**。
4. HMI/流程仍使用原ViewModel/Scene/Logic命令；资源配置 S1 页面仅展示配置服务入口，真实Schema分组编辑列入S2，禁止当作已交付。

## 4. 自动验收

执行 `CxVisual.Studio.exe --shell-self-test`，检查同目录 `StudioShellSelfTest.txt`：

- S1-01 三中央文档、四工具面板、ContentId唯一。
- S1-02 固定菜单/视觉与流程动态上下文。
- S1-03 重开文档唯一。
- S1-04 双主题不改变工程Revision。
- S1-05 布局保存恢复不改变工程Revision。
- S1-06 关闭重开工具面板唯一。
- S1-07 拒绝DTD、超限XML、未知ContentId。
- S1-08 重置初始布局。

真实编译、测试、CI结果务必单独记录。代码提交≠验收成功。

## 5. 人工验收（CI无法替代）

- Windows 10/11 @ 100%、150%、200% DPI；双显示器不同缩放的浮动拖拽。
- 左资源、右属性、底输出的停靠/浮动/自动隐藏/分页/关闭重开。
- HMI图元移动、Undo/Redo、保存重开与流程端口连线/MOC编译/MRK仿真的旧功能回归。
- 深灰色主题下：浮动标题栏、右侧属性、ScrollViewer、ToolTip、禁用按钮无看不清的问题。
- 关闭窗口时Dirty工程取消退出，不能错误保存布局或丢工程。
- 100次多文档切换与50次布局保存恢复的性能及视图引用复用。

## 6. 风险及待验项

- `Hello/AvalonDock` 文档目前记录Windows动态验收待执行；首次正式集成应以本工作流及主程序手测为准。
- 既有Studio内部仍有历史操作按钮，为防止回归S1未删除全部专业内嵌工具；S2再根据V1.2的Ribbon注册体系逐个收敛。
- CT/VTK/WinForms/HWND混合Viewer、多屏浮窗未在本阶段完成验证；不得把DockingManager存在视为所有专业已通过。
