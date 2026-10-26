# CxVisual TestCenter——自动测试工作台统一 Frame 与测试控制台实现验收 V1.0

> 2026-10-26。适用范围为现有 `Tests/TestCenter`（.NET Framework 4.8/WPF/AvalonDock 5.0.1）。该文档记录真实实现范围，严格区分源码和当前Windows验收。ReferTo保持只读。

## 一、定案与用户操作

将 TestCenter 曾经全局停靠的`操作中心 / Operations`明确更名为**测试控制台 / Test Console**，直接放入中央唯一`AutomatedWorkbenchFrame`（本轮为MainWindow中同一个Grid Frame，**不是第二套DockManager**）。

- 自动测试：**NUnit/TCT 计划实例**（TemplateId=`auto.catalog`）、**ArchitectureSmoke独立回归实例**（TemplateId=`auto.architecture-smoke`）均使用同一大 Frame 中右侧的测试控制台；可以拖动中间分隔条调节宽度。自动测试根目录及模板直接打开时也生效。
- 计划用例：保留原Catalog/Run/Attempt和`TCTTestPlanControl`，测试控制台可使用`TCTTestRibbonController`统一Run/Cancel/Export/断言/证据和计划专有`plan.run`、`plan.runselected`、`plan.clear`。
- 架构回归：继续使用原有`TCTArchitectureSmokeFrame`与`TCTArchitectureSmokeProcessRunner`，只显示独立进程`architecture.run`、`architecture.stop`、`architecture.clear`等真正可执行命令；CLI退出码不冒充NUnit/TCT逐条Attempt。
- **二维视觉、三维、通道、消息、模块等专业工作台**：默认全宽、无全局测试控制台。需要结果/诊断时，可点击`视图→显示测试控制台`，也可在面板右上角点击`收起`。这仅改变UI可见性，不会停止运行任务。
- 自动Run切换到概览和结果展示时保持控制台可见，保留取消/结果/源码追踪，不因临时切页让用户失去操作入口。
- `test.documents`仍是AvalonDock唯一文档身份，内部保留既有多实例Tab、Overview、Result viewers，不建立第二套TCT Run仓库或工程树。

## 二、Owner与源文件

| 类型/文件 | 职责及本轮变更 |
|---|---|
| `Tests/TestCenter/MainWindow.xaml` | 去掉全局`test.inspector` LayoutAnchorablePane；中央`AutomatedWorkbenchFrame`直接组合`DocumentHost`及可调宽`TestConsolePanel`、`Inspector`、两个动作Host |
| `Tests/TestCenter/MainWindow.xaml.cs` | 新的`UpdateTestConsole`与`SetTestConsoleVisible`根据WorkspaceId/TemplateId切换，保留`_automatedRunContext`防中途刷新导致控制台消失；原主窗口Run/Cancel/导出仍是唯一实现 |
| `Tests/TestCenter/Verification/TCTWpfAutomationHarness.cs` | 真正的UIA按新策略验收概览默认不显示→菜单打开→RunSelected InvokePattern→收起释放区域，不再假设全局常驻 |
| `Tests/TestCenter/Manual/Imaging/TCTImageViewerSmoke.cs` | 真实WPF宿主测试两个已创建自动测试实例、专业图窗隐藏、手动显隐和菜单/命令；截图只使用合成图 |
| 原`TCTTestInspectorControl`、`TCTTestPlanControl`、`TCTArchitectureSmokeFrame` | **功能保持原Owner原类**，只重组其外部UI呈现，不复制对象、不发明Adapter |

## 三、OOP / GoF / AOP的应用

- OOP：一个中央Grid Frame统一图窗与测试控制台的层次关系，Run/Plan/Smoke/Inspector实际组件只有原本的Owner。自动与专业工作区通过明确上下文选择展示，减少相互干扰。
- GoF：现有`ITCTWorkspaceFrame.Commands`体现命令定义与动作策略，`StateChanged`与`WorkspaceActivated`继续以Observer方式刷新命令与界面。架构进程保持独立，不额外包装一层Dispatcher。
- AOP：现有错误状态、取消请求、日志/证据/源码/时长机制保持不动；UI重排不插入热路径或创建第二个事务/运行日志库。

## 四、旧布局兼容性与验证门禁

原`test.inspector`在旧版保存的AvalonDock XML中属于全局独立停靠项。新版不再把它放回Dock树；对旧XML不强行反序列化为另一个父对象：**保留旧文件不改，使用新默认布局，并提示用户重新保存。** 其它布局序列化的ContentId保持一致；`test.documents`的反序列化内容应是整个`AutomatedWorkbenchFrame`而不只是内部DocumentHost，否则WPF有父对象冲突风险。

必须以最新版Windows真实结果逐项判定：
1. TestCenter Debug x64/.NET48 MSBuild全编译；
2. `--ui-smoke`：真实窗口初始化、主题切换、恢复默认布局和NUnit/TCT原有测试；
3. `--imaging-viewer-smoke`：WPF实际宿主，自动Smoke/Plan共用Frame，专业Viewer无全局控制台，菜单开/关均生效；
4. `--uia`：真正运行UIAutomation InvokePattern及子界面动作；阴性错误和超时需独立记录；
5. 设备/生产现场验收不属于本轮目标。

**没有以上最新CI绿色之前，仅声明代码提交；历史CI不冒充本次通过。**
