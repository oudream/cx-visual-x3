# CxVisual Studio S1：统一 Shell 与 AvalonDock 接入（实施与验收）

> **实测进度（2026-10-21）**：最新源码SHA a4e94349的S1自动验收已通过（17/17，含R05/R06、多资源P0-2）；同一SHA的Windows工程组织回归已通过。以下仅陈述相应 SHA 的真实 CI，人工视觉/浮动/DPI/长稳仍待验证，不能将其等同完整工业Studio交付。

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
| `Applications/Studio/StudioShell.ResourceTree.cs` | 投影MEM页面、Content、Symbol和其它资源，支持筛选/选中/双击导航及刷新去重 | 不复制工程真源；Group/Element不进入普通资源树 |
| `Applications/Studio/StudioShell.GraphicsTree.cs` | 当前图元定义下独立Layer/Group/Element树、MEM身份同步、展开状态保留 | 不复制工程实体；选择不改Revision |
| `Applications/Studio/StudioShellSelfTests.cs` | Windows真STA自测 S1-01～11 + R07 Join | 失败必须进程ExitCode!=0 |
| `Applications/Studio/StudioViewModel.Shell.cs` | Shell级故障文本接入已有Status | 不修改Revision |
| `Applications/Studio/App.xaml.cs` | 非交互 self-test 入口 | 日常启动仍进入MainWindow |
| `.github/workflows/studio-shell-windows.yml` | Windows完整解决方案 + S1 STA + 结构检查 | 以对应commit的workflow实测为准 |

## 3. 使用方式

1. Windows VS2022 + .NET Framework 4.8 Developer Pack。打开 `CxVisual.Unified.sln`，Restore NuGet依赖 `Dirkster.AvalonDock`、`Dirkster.AvalonDock.Serializer.Xml`、`Dirkster.AvalonDock.Themes.VS2013`（均5.0.1）。
2. 启动 `CxVisual.Studio.exe`。顶部是固定9项 + 文档动态菜单，中央主页/HMI/流程在同一DockingManager下，不出现第二层固定导航。
3. 打开“视图”可操作工具面板、保存/恢复/重置布局；点击右上“浅色/深色”切换主题。工作区保存在 `%APPDATA%\CxVisual\Studio\workspace.layout.xml`，**不在工程保存中**。
4. HMI/流程仍使用原ViewModel/Scene/Logic命令；资源配置 S1 页面仅展示配置服务入口，真实Schema分组编辑列入S2，禁止当作已交付。
5. 左侧“工程资源管理器”按照真实MEM对象显示“工程 → 页面 / 共享内容(Content) / 共享组件(Symbol) / 其它资源”；支持名称筛选、单击页/资源、双击打开HMI定义。普通树不展开Group、Element；图形结构另由右侧独立可停靠面板展开，单纯选择对象不重建整棵图元树。

## 4. 自动验收

执行 `CxVisual.Studio.exe --shell-self-test`，检查同目录 `StudioShellSelfTest.txt`：

- S1-01 三中央文档、五工具面板、ContentId唯一。
- S1-02 固定菜单/视觉与流程动态上下文。
- S1-03 重开文档唯一。
- S1-04 双主题不改变工程Revision。
- S1-05 布局保存恢复不改变工程Revision。
- S1-06 关闭重开工具面板唯一。
- S1-07 拒绝DTD、超限XML、未知ContentId。
- S1-08 重置初始布局。
- S1-09 层级资源树与MEM工程身份一致，不改Revision、不包含普通Element节点。
- S1-10 图形结构与右侧属性独立同时可见，Layer/Group/Element基于真实MEM对象。
- S1-11 画布改变选中对象后，图形结构树不重建根节点且Revision不变。
- R07 现有MEM工程Join的保存重开及非法输入回滚（由并行开发加入）。

### 本轮真实CI证据（按Commit定位）

| 版本 | Windows任务 | 实测结果 |
|---|---|---|
| [0eb5cf69](https://github.com/oudream/cx-visual-x3/commit/0eb5cf69a250cd3c74aa3268de68deef066948ea) | [Studio S1 / 39525077173](https://github.com/oudream/cx-visual-x3/actions/runs/39525077173) | **成功**：Release x64编译、12/12 STA自测、结构与Owner依赖检查 |
| 同一提交 | [工程组织 / 39525077202](https://github.com/oudream/cx-visual-x3/actions/runs/39525077202) | **成功**：真实WPF工程组织、编排、撤销/重做、开发者用例回归 |
| [7b4fd149](https://github.com/oudream/cx-visual-x3/commit/7b4fd1493b573d74fdcdd1a356849100a41954bc) | [Studio S1 / 39524531721](https://github.com/oudream/cx-visual-x3/actions/runs/39524531721) | **成功**：修正S1测试缺少AvalonDock Layout命名空间导致的编译失败 |
| [813df07](https://github.com/oudream/cx-visual-x3/commit/813df07bd8c9a3fb203dcee1c4434e1a3e2478be) | [Studio S1 / 39515710269](https://github.com/oudream/cx-visual-x3/actions/runs/39515710269) | **成功**：完整Release x64编译、10/10 WPF STA自测、结构与Owner依赖检查 |
| 同一提交 | [工程组织 / 39515710245](https://github.com/oudream/cx-visual-x3/actions/runs/39515710245) | **成功**：工程组织、兼容性、真WPF交互、编排与开发者例程 |
| [11cbc3a](https://github.com/oudream/cx-visual-x3/commit/11cbc3ab90efdca70d9ef42d442a5b36a461a434) | [工程组织 / 39514735744](https://github.com/oudream/cx-visual-x3/actions/runs/39514735744) | **成功**：修复原测试依赖旧TabItem的问题，不跳过原有断言 |

初版失败事实：S1一度使用`DockingManager.ActiveContent as LayoutContent`误判动态菜单；工程测试将旧TabItem当成新AvalonDock文档；树刷新重复触发NotifyScene导致GUI无响应。以上均依真实CI复现和修复。过去的失败不能由最新成功覆盖其技术原因。

## 5. 人工验收（CI无法替代）

- Windows 10/11 @ 100%、150%、200% DPI；双显示器不同缩放的浮动拖拽。
- 左资源、右属性、底输出的停靠/浮动/自动隐藏/分页/关闭重开。
- HMI图元移动、Undo/Redo、保存重开与流程端口连线/MOC编译/MRK仿真的旧功能回归。
- 深灰色主题下：浮动标题栏、右侧属性、ScrollViewer、ToolTip、禁用按钮无看不清的问题。
- 关闭窗口时Dirty工程取消退出，不能错误保存布局或丢工程。
- 100次多文档切换与50次布局保存恢复的性能及视图引用复用。

## 6. 剩余S1实际差距与下一轮优先级

| 优先级 | 待完成 | 当前真实状态 |
|---|---|---|
| P0 | HMI图形结构独立面板（Layer/Group/Element），只在图元文档使用 | **已有**独立面板、基础身份同步及恢复展开；更复杂的跨层选择、实例路径、选中画布位置仍须完成真实键鼠验证 |
| P0 | 资源点击/双击导航的真实端到端键鼠验收、停靠与布局恢复后事件回连 | 现有树身份及刷新自动测试通过，完整用户键鼠链未覆盖 |
| P0 | WPF真实桌面拖拽/浮动/自动隐藏、多屏DPI、关闭取消与焦点测试 | **未实测** |
| P1 | 配置资源Schema驱动1×N/2×N/3×N真实组合面板 | 当前只有入口与基础展示，未提供完整自动编辑 |
| P1 | V1.2唯一CommandId目录、动态页签Descriptor与权限门控 | 当前采用既有真实命令+Shell分组绑定，不声称完全覆盖 |
| P1 | 11个设计场景（含文件Backstage、主流程、子流程、运行调试、CT3D等） | 尚未全面落地，设计图不能当功能截图 |

**下一步先闭环P0的键鼠选择、跨资源Definition/Instance返回、切换工程和浮动/自动隐藏/重启布局恢复，再进入S2的Schema生成器和专业上下文扩展。** 不为界面另建工程树或运行真源。

## 7. 风险及待验项

- `Hello/AvalonDock` 文档目前记录Windows动态验收待执行；首次正式集成应以本工作流及主程序手测为准。
- 既有Studio内部仍有历史操作按钮，为防止回归S1未删除全部专业内嵌工具；S2再根据V1.2的Ribbon注册体系逐个收敛。
- CT/VTK/WinForms/HWND混合Viewer、多屏浮窗未在本阶段完成验证；不得把DockingManager存在视为所有专业已通过。

## 8. R06 页面资源独立Tab（2026-10-21）

- 按MEMPage稳定ID打开唯一的AvalonDock页面Tab，真实SceneView+CxVisualEditor，每个Tab独立场景选择和视口；MEM工程真源、版本、编辑事务、Undo/Redo共享。关闭文档回收Editor订阅，不重建第二套项目。
- 资源导航双击Page打开对应Tab；doc.visual仍保留旧高级图元编辑器，doc.flow仍保留原MOL流程编辑器。更换工程时只清理旧页面Tab，不可把仍在活动的固定流程文档抢回主页。
- 新资源文档与布局XML联动；反序列化校验当前工程存在Page ID、非法及跨工程ID拒绝。布局选择不修改工程Revision。
- 本轮源码提交：[773450c7](https://github.com/oudream/cx-visual-x3/commit/773450c7bd76c9c80f6cadfa8876c68c14e6fe09)，[S1 14/14 Windows STA](https://github.com/oudream/cx-visual-x3/actions/runs/39533807317) **success**；[Engineering WPF回归](https://github.com/oudream/cx-visual-x3/actions/runs/39533807345) **success**。
- **R06补充验收（同SHA 4770a698）**：修复资源树检视页面B、活动Tab仍为A时的动态Ribbon命令错位：真实“圆”操作必须写入A、不得误写B；关闭活动页面Tab保持`LayoutRoot.ActiveContent`与Ribbon上下文一致；切换工程在清理旧Tab之前记录活动文档，防止AvalonDock焦点变化导致错误回退。更新`Applications/Studio/StudioShell.cs`、`StudioShell.ResourceDocuments.cs`和`StudioShellSelfTests.cs`，不改MEM Owner与事务语义。
- **Windows验收证据**：[修复提交 4770a698](https://github.com/oudream/cx-visual-x3/commit/4770a698b1f89324769914ca8e09bec3af7eb757)，[Studio S1 Windows / 39548325060](https://github.com/oudream/cx-visual-x3/actions/runs/39548325060) **success，14/14 WPF STA通过**；[Engineering Organization Windows / 39548325067](https://github.com/oudream/cx-visual-x3/actions/runs/39548325067) **success，工程组织回归通过**。首轮新增反证曾触发失败，已按真实日志修复并复测；人工键鼠、DPI及全资源Tab仍待验。
- **当前R06范围已扩展**：Page/Content/Symbol以及主/AI/CV子流程多资源Tab均已有真实SceneView/MOLFlowCanvas和MEM事务相关WPF STA回归；但完全统一Ribbon/快捷键、复杂图元完整Editor体验、真实键鼠、多屏DPI及长稳仍未整体签收。不要沿用旧版“只有页面Tab”的状态描述。

## 9. S1～S4实施总规划与最新P0验收

- [S1～S4四阶段开发与真实验收总规划 V1.0](CxVisual_Studio_S1至S4_阶段开发与真实验收总规划_V1.0.md)：四阶段职责、EP/R/G映射、统一Editor交互契约、开发者/编排者与A/B/C场景及继续开发顺序。部分V1.2原稿及第11张设计图待核对，本规划不能替代V1.2原稿。
- 2026-10-21 源码提交：[a4e94349](https://github.com/oudream/cx-visual-x3/commit/a4e943493e0748b5e8b357dcd1d08a5371f3bfe3)。从真实S1红色CI [39551185708](https://github.com/oudream/cx-visual-x3/actions/runs/39551185708) 中定位 Undo 删除图元丢失归属，保留仅UI用途的变更目标→MEM资源根缓存，补Redo及无关资源不受影响断言。
- **同源码SHA实测：** [Studio S1 Windows 39553816491](https://github.com/oudream/cx-visual-x3/actions/runs/39553816491) **success，17/17 STA通过**；[Engineering Organization Windows 39553816541](https://github.com/oudream/cx-visual-x3/actions/runs/39553816541) **success**。人工浮动/键鼠/DPI/多屏/实机验收尚待。

## 10. S1/S2交界：独立资源Tab绘图命令实装（2026-10-21）

- 源码：[de3a024b](https://github.com/oudream/cx-visual-x3/commit/de3a024b4198e5be2dada7de899f7e5cf566afcc)。Page/Content/Symbol动态Ribbon和原工具箱已路由至各自`CxVisualEditor`，直接使用现有九Shape Catalog、Text和Widget，保留同一MEM/MTC历史；流程Tab与失效资源被拒绝；恢复Page旧ViewModel活动上下文以兼容R06。
- 同SHA [Studio S1 Windows #39564306075](https://github.com/oudream/cx-visual-x3/actions/runs/39564306075) **success，23/23 WPF STA**；[工程组织Windows #39564306081](https://github.com/oudream/cx-visual-x3/actions/runs/39564306081) **success**。新增用例为“P0 Editor跨资源九Shape/Widget与统一MEM历史路由”，旧R06及其余测试未删除。
- **仍未完成**：整个九Shape精细操作（多选对齐/节点/图层/快捷键）尚未全面接入各独立资源Editor；完整专业Schema、跨专业命令Descriptor、人工鼠键/DPI/运行调试最终闭环继续按[唯一Studio S1～S4主规划](CxVisual_Studio_S1至S4_阶段开发与真实验收总规划_V1.0.md)推进。

## 11. S1/S2独立编辑器第二轮：多选对齐分布及Z序（2026-10-21）

- [a8022b66](https://github.com/oudream/cx-visual-x3/commit/a8022b66975821da39b472baa1e4582e6dc07706) 从Page/Content/Symbol的真实`SceneView.SelectedVisualIds`调用原`CxVisualEditor.Align/SetZOrder`，动态“排列”“图层”Ribbon支持左/顶对齐、水平/垂直居中、水平分布、置顶/置底；固定`doc.visual`仍沿旧ViewModel。流程Tab及无效选区拒绝写入。
- [Studio Windows #39565771432](https://github.com/oudream/cx-visual-x3/actions/runs/39565771432) **success，26/26 STA**；[工程组织 #39565771340](https://github.com/oudream/cx-visual-x3/actions/runs/39565771340) **success**；同SHA无新增MEM Owner/项目。新增测试验收一次事务和Undo/Redo/单选拒绝/跨资源隔离，并保持X-Ray P0-4测试。
- **边界**：这只是多选排列与图层Z序接通，尚未覆盖路径控制柄精修、完整几何编辑、键鼠/DPI及运行态授权、所有专业配置与Viewer。
