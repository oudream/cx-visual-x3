# CxVisual 专业视觉体系——二维视觉工作台 P2 功能恢复与实测 V1.0

> 2026-10-26；针对用户第6张TestCenter旧截图指出的**「只调整界面、旧功能未真正优化」**问题进行功能闭环补强。验证源码基线：`7c1ec6408343e73b08e17f91667c8b1902fec0b4`。
>
> 依据：`AGENTS.md`、工程Skill、`DOCS/00～01`及最新功能架构规格、当前x3源码；对照`ReferTo/Vision/CxECTImageWinForms/Controls/ImageViewerControl.cs`（11万余字）、`Models/ImageToolMode.cs`、`Services/ImageMeasurementService.cs`。ReferTo始终只读。**旧生产功能不等于新平台已迁移；新平台按钮可见不等于可用。**

## 1. 从旧Viewer到真实功能的新旧对应

| 项目/旧功能 | 本轮已实现与新代码Owner | 使用方式及验收 | 未完成项 |
|---|---|---|---|
| 水平/垂直距离 | `MVIImageToolMode.HorizontalDistance/VerticalDistance`，`FIMImageMeasurement.GetHorizontal/VerticalDistance...`，`MPWImageViewerControl` | 测量页选工具→原图拖拽→保留对象ID、测量线/值；X、Y非等距标定分开算，未标定只给px | 端点控制柄精调未完成 |
| 圆形直径 | `MVIImageToolMode.Diameter`、`MVIImageOverlayKind.DiameterMeasurement` | 拖拽圆框→原图尺寸和像素直径；物理校准下同时列出X/Y直径，避免把非等距像素误当物理圆 | 真正轮廓拟合圆径未完成，不将手绘圆冒充拟合 |
| 矩形/多边形面积 | `MVIImageToolMode.Area/PolygonArea`、`FIMImageMeasurement.GetRectangle/PolygonArea...` | 拖框或逐点双击→WPF面积标签，鞋带公式独立于浏览旋转镜像；精确mm²需要显式标定 | 自交多边形验证、完整周长/区域编辑柄未完成 |
| 灰度剖面 | `MVIImageToolMode.LineProfile`，`FIMImageAnalyzer.GetLineProfile`、现有MPW曲线图 | 点击「绘制剖面线」→鼠标拖线→MVI原图线对象→显示灰度剖面；重新分析优先最近绘制线 | 多条曲线叠加/导出剖面CSV未完成 |
| Gamma/亮度/对比度 | 旧`ImageDisplayState`对应已有`MVIImageDisplayState` + 新真实WPF Slider | 松开滑块/按键后应用，不在拖动中反复生成大图缓冲；可输入数值并复位，原像素不变 | 连续拖拽实时GPU预览、LUT缓存性能仍待优化 |
| 大图统计 | 旧Viewer的`latestAnalysisRequest`/取消语义，现`TCTManualImageViewerFrame.Calculate` + `FIMImageAnalyzer.Analyze` | 256档原始灰度直方图和Min/Max/Mean/StdDev；后台线程计算、切图/变ROI/关工作区取消、过期结果隔离、耗时显示 | 多种非矩形ROI精确掩模统计/批量与实时仍未实现 |
| 原/结果滑动比较 | `FIMImageBlend.Swipe`、`MVIImageComparisonMode.Swipe`、MPW`SwipeOriginalAndResult` | 同尺寸同格式图像共用同一Viewport，滑动比例0～1；支持8/16位、彩色、Float32；保留原图与叠加坐标 | **真正SideBySide两独立视图**及配准尚未完成 |
| 测量与标注数据输出 | `TCTManualImageViewerFrame.ExportMeasurementsCsv` | 所有MVI对象ID、图层、可见性、Bounds、顶点、显示值、文字输出为UTF8 BOM CSV；文本转义，公式前缀防护 | MRC唯一工程持久化/撤销与标准化报告待完成 |

所有功能归既有**Foundation.Imaging (FIM)、Mechanism.Visual (MVI)、Mechanism.Presentation.Wpf (MPW)、Tests/TestCenter (TCT)**。不增加项目，不改ReferTo，不建Facade/Adapter/Wrapper/第二图像对象仓库。

## 2. 完整链的正反验证

1. FIM：水平3px/垂直4px、非等距0.2×0.4mm/px、3×4矩形与3-4三角形面积；非法三角形顶点数/默认标定、Gray16透明与滑动比较边界、两图格式不一致等反证。
2. MVI：新增工具模式、对象ID、图层唯一Owner；观察旋转/镜像后的原图坐标不写回。
3. MPW+TCT：Windows STA真实图窗添加对象，验证0.6mm/1.2mm水平垂直、圆的0.8/1.6mm双径、4.5px²→0.36mm²多边形、CSV中保留GUID/顶点；16位滑动PNG左原右结果回环、原图不变；灰度剖面按钮生成真实曲线。
4. UI功能截图在CI Artifacts中包含`workbench-measure-profile.png`和`workbench-gray-histogram.png`，由Windows真实WPF控件与**合成测试图像**输出，非静态UI设计图、非客户私有样本。

**真实CI**：[Imaging Windows专项 #40262549868](https://github.com/oudream/cx-visual-x3/actions/runs/40262549868) **Success**（`7c1ec640`）。覆盖全解决方案Windows x64/.NET48构建、结构和Owner依赖、`--imaging`与默认回归、原生WPF/截图及PNG+CSV功能回环。相关命令工作区Windows专项本阶段对应运行亦通过。

**不能宣称**：整个TestCenter Windows UI Automation已经通过、六张参考图全部旧工具100%迁移、大图长期性能达标、HALCON硬件/现场相机/设备已经验收。TestCenter完整UIA另有超时问题，归其独立验收线追查。

## 3. OOP、GoF和AOP与性能决策

- OOP：FIM只负责原始数值、几何统计和像素合成；MVI继续管理互斥输入/State和唯一OverlayStore；MPW复用一个原图坐标Viewport处理对比、ROI、标签、尺标。TCT只是使用者，partial拆分不制造第二份真源。
- GoF：继续使用既有Observer（会话、图层改变触发UI）、工具互斥状态与比较策略按模式选择。Swipe使用**同一Viewport**自然同步，避免两套Viewer同步器和额外中转层。下阶段唯一Undo正式复用MRC机制，不在MVI重造Command仓库。
- AOP：图像统计按CancellationToken、Epoch、原图身份做取消与过期回写防护；操作异常/耗时在TCT边界处理，核心像素循环无反射/每像素日志。GPU实时性能未验收，不应宣传“已实时”。

## 4. 尚待继续完成的旧功能（不要视为本轮已通过）

- 真正**并排双图同步**、图像配准、差异可视与更完整多视图比较。
- ROI完整拖拽控制柄、移动/缩放/旋转/编辑、区域掩模统计、图层排序、工程级撤销/重做/保存。
- 测量的工业轮廓/圆/直线拟合、弧长周长及更丰富校准；视觉算法Pipeline、调试、中间图像和更全面统计。
- 大图GPU图像显示与操作延迟、共享内存/相机/实时批量/网络、HALCON/OpenCV专业SDK和生产设备验收。

**真实验收原则**：继续优先复用旧成熟代码，功能要从用户操作到输入→计算→显示→输出→错误和测试完整闭环；不要再将“界面做好”当作功能完成。
