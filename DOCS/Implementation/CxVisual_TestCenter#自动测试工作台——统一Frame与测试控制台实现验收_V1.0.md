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

## 五、本轮Windows CI真实验收结论

| 对应提交及专项 | 结果 | 验证内容或限制 |
|---|---|---|
| [Imaging Windows #40271326616](https://github.com/oudream/cx-visual-x3/actions/runs/40271326616) | **成功** | 同一真实WPF TestCenter宿主分别打开真实TemplateId的ArchitectureSmoke创建实例、NUnit/TCT计划实例、二维视觉工作区；验证控制台随上下文显示、专业默认隐藏、菜单手动显示/收起和外层文档标题精简；并覆盖原有FIM/MVI/图像功能 |
| [消息中心 UI #40271640550](https://github.com/oudream/cx-visual-x3/actions/runs/40271640550) | **成功** | 专业消息中心不强制显示通用控制台，真实UIA按需通过菜单打开原补充命令并验证实际消息收发 |
| [命令中心 UI #40271640619](https://github.com/oudream/cx-visual-x3/actions/runs/40271640619) | **成功** | 真正UIA访问命令输入/调用/历史，专业页面默认无全局测试侧栏 |
| [TestCenter完整Windows #40271640519](https://github.com/oudream/cx-visual-x3/actions/runs/40271640519) | **部分通过，整体未通过** | 全解决方案Debug x64/.NET48构建、动态NUnit用例发现/执行/JUnit、`--ui-smoke`含布局恢复均通过；完整`--ui-automation`因105秒上限**超时**，全量未验收，不能冒充成功 |

此处将“专项真实回归可用”和“TestCenter全量UIA尚有超时”分开记录。用户所请求的自动测试控制台合并与专业工作区不占位已通过针对性的**真实WPF宿主测试**，未因此推断所有其它功能已通过。

## 六、2026-10-26 最新连续开发与实际Windows验收

本节覆盖并更新第五星期初的历史结论；早期 #40271640519 失败是当时提交的事实，**不是当前最新通过情况**。此节记录的是具体SHA的实际CI结果，不将下一次提交自动标记为通过。

| 提交 | 修改及复现结果 |
|---|---|
| [e2e8d1f](https://github.com/oudream/cx-visual-x3/commit/e2e8d1f0148dee92da15bc81b81bcb722877ee2b) | 补Connector删除阶段日志和异常透传；确认Point、Mapping真实删除后UIA找不到二次Save。 |
| [01759d8](https://github.com/oudream/cx-visual-x3/commit/01759d8de4a6ab814c3bb48bc0018b83cc20a52b) | 同一Workspace重激活时保留手动TestConsole；不同Workspace导航按约定收起，状态按WorkspaceId管理。 |
| [87fdac2](https://github.com/oudream/cx-visual-x3/commit/87fdac2dc11c66904056e06c10afc2bb8def72ba) | UIA实际从概览返回专业页后，先检查控制台已自动隐藏，再通过“视图→显示测试控制台”菜单打开；完整Point/Mapping悬空引用、删除、二次Save、重开断言不删减。 |
| [5863164](https://github.com/oudream/cx-visual-x3/commit/5863164d8a0f7dc3430107fb5a150268c57bdd3f) | 解决两个Connector实例的配置Frame查询误命中；按当前ActiveModuleId和创建时WorkspaceId查询、关闭，禁止Single/First取错实例。 |

**全量真实验收已通过**：[TestCenter Windows .NET48 #40280277938](https://github.com/oudream/cx-visual-x3/actions/runs/40280277938)，对应 **5863164d8a0f**。该次Windows工作流29步全部成功，涵盖：
- Debug x64/.NET48全方案Rebuild、结构及依赖DAG检查。
- NUnit动态源码编译、真实发现/Worker执行/结果与JUnit、外部程序集和指纹。
- WPF Shell、真实Connector UIA完整配置增删存储闭环、全量Windows UIAutomation与截图。
- 独立Worker、超时/异常失败封闭、NUnit失败/Skipped/Inconclusive/Error映射、Owner证据与跨进程历史回放。
- 并行重复执行、结果与历史回归、空选择拒绝。

**后续质量补齐**：切换回Overview或Result页时，`WorkspaceTitle`应随真实中心Tab更新；此前`OnWorkspaceActivated(null)`提前return导致残留前一专业标题。相应源码修复和实际Tab双向切换反证已加入主窗口与UIA。**此后提交的CI必须另行检查，不能沿用5863164的PASS结论。**

OOP：工作区实例采用稳定WorkspaceId/ActiveModuleId，与独立Frame和唯一MHS草稿一一对应。GoF：沿用既有命令对象、UIA真实Invoke及Observer工作区切换；不增加第二套命令状态。AOP：阶段诊断、错误透传与CI测试证据通过现有机制记录，无热路径反射和新增适配项目。

仍未以此次CI验证：真实PLC/X-Ray/相机硬件及联锁、跨PC远程总线、现场HIL、全部分辨率/DPI/多显示器与人工使用体验、真实GPU/专有SDK性能；上述事项按各自Owner和专项环境另验，不能冒充全平台生产可用。

### 本阶段收尾实证：标题隔离及全部专项回归

- [TestCenter源码与UIA修复 c917d0d](https://github.com/oudream/cx-visual-x3/commit/c917d0d6bcafb69ea672bd83b356e0eb012a5978)：`TCTMainWindow.OnWorkspaceActivated(null)`恢复Overview/非专业Tab标题，避免前一个Connector/Driver的标题残留；UIA真实切换Overview→专业页双向断言，并继续包含原删除/配置存储/历史负例。
- [**TestCenter Windows 全量 #40280922151：SUCCESS**](https://github.com/oudream/cx-visual-x3/actions/runs/40280922151)：对应**c917d0d6bcafb69ea672bd83b356e0eb012a5978**，**29个步骤均Success**（其中包含CI环境设置、制品保存等流程步骤）；全方案Debug x64编译、动态NUnit、Connector UIA、全量WPF UIA、独立Worker异常与证据、并行Repeat/History全部通过。
- 同一SHA补充专项： [命令工作区 #40280922359](https://github.com/oudream/cx-visual-x3/actions/runs/40280922359)、[消息中心 #40280922128](https://github.com/oudream/cx-visual-x3/actions/runs/40280922128)、[Config L1 #40280922159](https://github.com/oudream/cx-visual-x3/actions/runs/40280922159)、[配置公共底座 #40280922103](https://github.com/oudream/cx-visual-x3/actions/runs/40280922103)，**均Success**。
- 此处“本阶段全量通过”严格限于 **c917d0d** 的TestCenter自动化与相关专项；之后其他领域的并行提交需要根据对应新SHA及CI另行确认。无物理PLC、X-Ray、跨机命令、真实厂商SDK或工业现场验收证据。
