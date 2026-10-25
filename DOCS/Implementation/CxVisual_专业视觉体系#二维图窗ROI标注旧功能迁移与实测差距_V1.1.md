# CxVisual 专业视觉体系——二维图窗 ROI/标注成熟功能迁移与实测差距 V1.1

> 日期：2026-10-26，仓库 `oudream/cx-visual-x3`，`ReferTo` **只读**。先恢复成熟旧功能、再优化结构和补缺，不能以按钮/枚举/空类型冒充完整功能。
> 本文是 P2 真实迁移记录，基线为 [二维视觉工作台60项目标](../Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——完整功能架构与实施验收_V1.0.md)。最终以最新Windows Actions的实际结果为准。

## 1. 本轮复用哪些旧现役文件

| ReferTo旧文件 | 新归属 / 真实代码 | 保留、改造内容 | 当前等级 |
|---|---|---|---|
| `ReferTo/Vision/CxECTImageWinForms/Models/Overlays/ImageOverlayItem.cs` | `Mechanism/Visual/Imaging/Overlays/MVIImageOverlayKind.cs`, `MVIImageOverlayItem.cs` | 17个ROI/测量/标注/缺陷旧种类、四图层、Guid标识、Bounds/Points、文字、外部ID、ARGB/线宽；以FGEPoint2D/FGERect2D复用现有几何，而非复制旧ImagePoint；快照式WithBounds/Translate/WithStyle保证通知可见 | 源码已提交 |
| `ReferTo/Vision/CxECTImageWinForms/Models/Overlays/OverlayStore.cs` | `Mechanism/Visual/Imaging/Overlays/MVIImageOverlayStore.cs` | 唯一Owner列表、Add/Remove/Find/Clear、SetLayerVisible/IsLayerVisible/GetVisibleItems；补Replace事件与快照，禁止重复ID | 源码已提交 |
| `ReferTo/Vision/CxECTImageWinForms/Models/ImageToolMode.cs` | `Mechanism/Visual/Imaging/MVIImageToolMode.cs` | 保留互斥工具思想，只开放已有对应交互行为的类型，不因旧枚举包含全部模式就宣称全部已实现 | 源码已提交 |
| `ReferTo/Vision/CxECTImageWinForms/Halcon/HalconImageCanvas.cs` | `Mechanism/Presentation.Wpf/Imaging/MPWImageViewerControl.xaml(.cs)` | 不搬运HALCON窗体句柄。用WPF原生Canvas/Shape真实绘制覆盖层，保持原图坐标，支持缩放重绘/图层过滤、几何拾取、鼠标移动和删除 | WPF源码已提交；部分Smoke已通过 |
| `ReferTo/Vision/CxECTImageWinForms/Controls/ImageViewerControl.cs` | `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerFrame.cs` | 直接使用MPW工具模式、叠加项Store和可见图层；真实列表、选择、删除、移动和显示开关 | UI源码已提交 |

源旧模型的 Mutable `Points` 和随意属性赋值容易使 WPF 绘制漏更新；本轮保留能力语义但升级为可更新快照，并在Store层发出`Changed`事件（GoF Observer）。既有MEM/MRC是工程持久真源；MVI仅承担图像**临时浏览覆盖状态**，**绝不建立第二工程/Undo仓库**。

## 2. 真实 WPF 功能细分

- **直接鼠标绘制**：导航/平移、矩形ROI、圆/椭圆ROI、两点距离（px）、点/文字/箭头标注、缺陷框；另有鼠标几何命中、覆盖项移动/删除。
- **API呈现支持**：多边形ROI、矩形与多边形标注、直线剖面/角度等旧模型分类可存储。未完成实际对应交互/测量的类别，**不得标成已完整支持**。
- **统一图层**：ROI/Measurement/Annotation/Defect各自显隐；隐藏层不参与几何拾取。覆盖项不可变式平移/缩放同步Bounds与Points、稳定Guid；错误参数/重复Guid拒绝。
- **导出不同含义**：`MPWImageViewerControl.SavePng`只写当前原始或处理图像像素；`SaveViewPng`按当前WPF视口导出包含ROI、测量、缺陷和标注的截图，不能互相冒充。
- **TestCenter**：使用现有 `manual.imaging`，工具下拉、标注文字参数、图层列表，列表的删除/位移/隐藏/恢复/清空，不复制另一套覆盖项。

## 3. 本轮真实开发提交

| 提交 | 功能/代码 |
|---|---|
| `c71806d` | 从旧OverlayItem/OverlayStore抽取17分类+四图层+快照，并接ArchitectureSmoke |
| `5b607d2`, `a51888e` | MPW WPF Shape叠加投影及命名空间冲突修复 |
| `8f08e8f` | TestCenter真正可操作的工具/叠加项列表 |
| `0b07ef1` | WPF视口截图、四类示例覆盖项及显示/移动/清空回归 |
| `7d242f0` | Bounds缩放同步顶点、加入鼠标移动与删除工具模式 |
| `3db8c8b` | 几何拾取/鼠标拖动/点击删除、边界约束 |
| `729c1a3` | WPF真实窗口几何命中、顶层优先和隐藏图层反证 |

## 4. 验证门禁与现实结果

- **已通过**：针对提交 `0b07ef1` 的 [Imaging Windows专项运行40244449790](https://github.com/oudream/cx-visual-x3/actions/runs/40244449790)，全解决方案.NET48 Windows x64构建、结构与依赖检查、FIM/MVI专项及默认ArchitectureSmoke、WPF控件/PNG+覆盖项截图回环全部成功。
- **需要最新版单独确认**：`7d242f0`、`3db8c8b`、`729c1a3` 对应几何缩放/鼠标移动/拾取的 CI 仍需以**各自**工作流结果确认为准；在此之前只认定源码已提交，不将 `0b07ef1` 旧通过记录冒充最新通过。
- **专业与工业现场**：HALCON/OpenCV句柄、产线采图、数万张批次与4320²大图性能、重复打开内存释放未验收。
- **未补齐的P2**：旋转矩形、完整多边形鼠标编辑、Bezier自由曲线、测量角度/直径的编辑与输出、文本样式、八控制柄/旋转/锁定/图层排序、持久化、复用已有MEM/MRC Undo/Redo。旧代码已有成熟模块的，要逐方法迁移并核对，而不是重新简单写一套。
- **P3/P4/P5**：高级图像算法、Pipeline调试、实时并行、专业HALCON/OpenCV与Studio仍没有完整闭环，不能提高完成等级。

## 5. 下一阶段必须按成熟功能逐项验收

1. 旧`ImageTransformState`与`ImageDisplayState`对应源码直接迁入MVI，先做可逆坐标映射/自动窗宽、Gamma/亮度/对比度、旋转/镜像的**纯显示不改变原图**。
2. 逐项迁移`HalconImageCanvas`成熟交互：多边形、绘制/移动/缩放控制柄、标注文字/箭头/缺陷、测量回显；复用FGE/MRC现成的几何、编辑、唯一Undo机制。
3. 恢复旧`ImageRulerControl`、`HistogramPlotControl`、`LineProfilePlotControl`的显示能力，转为真正的WPF专用控件，补旧新同样本输出和鼠标实际验收。
4. 用旧`ImageComparisonState`恢复原结果图、并排、叠合、联动、差异；用至少一套工业8/16位实际图像与原程序并行验收。
5. 任何缺失必须标明阶段/Owner/源文件，不准仅以生成一批接口、测试小图片就标记P2/P3完成。
