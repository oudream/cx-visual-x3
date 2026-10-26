# CxVisual_专业视觉体系#二维视觉工作台TestCenter界面优化与Windows验收 V1.0

> 2026-10-26；以用户提供的原图1～5成熟二维图窗与第6张TestCenter截图为视觉/功能对照。当前源码基线：`cd24c300dee73ea27c1984548e854b6a01e2ab9e`。只重构测试工作区布局和宿主焦点，不改变FIM、MVI、MPW算法Owner，不改变ReferTo，只读源保持原样。

## 一、界面已实现的修正

| 用户截图指出问题 | 实际源码修改和可见结果 | 保留的使用能力 |
|---|---|---|
| 中央顶部按钮四行挤满、字体小 | **单排**水平滚动高频图标工具条，WPF蓝色矢量线形Icon、双行文字、统一尺寸；窄窗口横向滚动不裁切文字 | 打开、Fit、1:1、缩放、导航、ROI、测距、观察旋转与镜像、原图/结果、统计、保存 |
| 右侧成熟面板没迁入 | 两组可拖动高度的真正TabControl，上段**显示/窗位/灰阶/测量/标注**，下段**区域/统计/剖面/变换/输出**；保留各自上下滚动区 | 旧FIM/MVI功能不复制，不增加“第二套算法” |
| 图像区过小 | 中间单一主视口+可拖动侧栏（默认344px，边界280～500）；视口保留标尺、ROI、图层和坐标状态 | MPW Viewer及所有原图/显示变换仍只有一份 |
| 测试宿主重复右栏 | 打开`manual.imaging`时，AvalonDock将`test.inspector`自动收起；切换其他页面时恢复 | 仍可通过View/停靠区访问测试Inspector、Owner/诊断 |
| 额外“测试视图 / Viewer”重复标题 | 宿主只保留紧凑的AvalonDock**工作区**标签，以维护真实停靠和多文档功能；内部工作区分页也变小 | 不粗暴删除AvalonDock文档宿主、不破坏多Tab和布局保存 |
| 图层操作散乱 | 显示页集成四类图层颜色与显隐；区域页集成ROI绘制及真正对象列表/编辑；测量/标注页显示真正可用动作 | 尚未实现的水平/垂直测量、直径/面积编辑等**不放置假按钮** |
| 参数找不到 | 窗位、Gamma/亮度/对比度、真实X/Y标定；变换页集中像素生成、ROI裁剪、滤波；输出页汇总RAW、图像保存、带图层截图、融合 | 参数、异常、统计和日志复用既有函数/字段，不绕开正式API |

对应实现：
- `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerFrame.cs`：保留同一用户工作区与所有实际操作方法，仅让构造器委托到同一类的布局方法。
- `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerLayout.cs`：同一`partial TCTManualImageViewerFrame`拆开视觉布局与操作代码；没有新增模型Owner、Adapter、命令中心或算法项目。
- `Tests/TestCenter/Common/Shell/TCTTestDocumentHostControl.cs`：原宿主分页变紧凑；仍支持选中/关闭工作区。
- `Tests/TestCenter/MainWindow.xaml(.cs)`：缩减顶部/面包屑间距，专有二维工作区收起测试辅助操作中心，切页恢复。
- `Tests/TestCenter/Manual/Imaging/TCTImageViewerSmoke.cs`：真实WPF页签/按钮可达性及完整AvalonDock宿主验收，上传人工造图而非工业私图。

## 二、OOP / GoF / AOP 在本轮怎样应用

1. **OOP与Owner唯一**：继续用唯一FIM原始图像、MVI会话/叠加项/显示状态、MPW UserControl；UI分为布局和操作两个partial实现文件，仍然是**同一个工作区对象**。没有复制业务状态、增加Viewer代理或另造工程/流程。
2. **GoF**：MVI状态变化与图层Store的Observer机制直接通知MPW和侧栏，工具按钮使用TestCenter现有动作回调，形成统一行为。绝不声称此轮凭工具栏就完整实现GoF Command/Undo。
3. **AOP交叉关注**：保留`AddButton`边界异常诊断与状态栏可见性、FIM处理和文件加载Stopwatch耗时，以及已有MVI异步版本防过期机制；UI改布局不在热路径插入反射或分层代理。Windows专项验收覆盖异常/真实UI，不把无异常等于实机通过。

## 三、Windows验证

- **已通过**：[Imaging专项运行 #40259838886](https://github.com/oudream/cx-visual-x3/actions/runs/40259838886)，针对`cd24c300`：VS2022 Windows x64 NET48完整解决方案编译、结构/依赖、FIM/MVI专项与默认回归、真实WPF窗口、上下五页签/高频按钮、PNG回环，以及真正TestCenter AvalonDock Shell进入视觉页面收起右Inspector、切走恢复。
- 已生成并上传**合成图**界面截图（在该运行Artifacts）：`workbench.png`为独立视觉工作区，`testcenter-workbench.png`为完整测试宿主；只是Windows虚拟桌面上的截图，测试图数据均是合成，**不是实际X-Ray工业图像或现场交付**。
- **尚未通过**：[TestCenter整体UIA运行 #40259838804](https://github.com/oudream/cx-visual-x3/actions/runs/40259838804)：一般UIA用户操作脚本约105秒超时；此工作流的失败既不能说成绿色，也不否定独立Imaging与完整AvalonDock Shell专项成功。整体UIA需按自身测试脚本另行排除。
- 屏幕人工体验/不同DPI/真实6K×3K及长期运行性能未验收；上线使用前继续测试。UI不声称已补完原60条P2～P6功能。

## 四、开发者与用户入口

```
CxVisual.TestCenter.exe
→ 专有测试（人工操作）
→ 二维工业视觉工作台
→ 顶部快速工具 / 右侧主功能五页 / 下段区域分析五页
```

可执行回归：
```powershell
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --imaging
$proc = Start-Process -FilePath '.\Output\Release\x64\CxVisual.TestCenter.exe' -ArgumentList '--imaging-viewer-smoke' -PassThru -Wait
if ($proc.ExitCode -ne 0) { throw '二维视觉真实WPF工作台验收失败' }
```

**后续功能扩展**：在这套界面和正式FIM/MVI/MPW上补SideBySide多图、旋转矩形/手柄/图层排序、全测量与复用统一Undo；不再堆回顶部，不重新发明新Viewer。
