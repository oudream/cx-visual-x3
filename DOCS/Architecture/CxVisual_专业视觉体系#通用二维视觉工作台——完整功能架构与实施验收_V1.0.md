# CxVisual 专业视觉体系——通用二维视觉工作台完整功能架构与实施验收 V1.0

> 日期：2026-10-25。Owner：F11/F22 `Foundation.Imaging` (FIM)、M16/M17/M37 `Mechanism.Visual` (MVI)、`Mechanism.Presentation.Wpf` (MPW)、`Professional.Vision` (PVC)、`Tests/TestCenter` (TCT)。
>
> **阶段划分**：当前为 **P1首个真实WPF工作台纵向闭环已落地**，不是用户 60 项需求全面完成。编译/测试以本文件所列 GitHub Actions 具体运行证据为准。**ReferTo全程只读**。
>
> 架构来源：`AGENTS.md`、工程 Skill、`DOCS/00`、`DOCS/01` Review及此前FIM/FVO/MVI源代码/实现台账。`DOCS/02～04` 是大型XLSX：本轮工具中**尚未完成逐Sheet内容解析**；Owner细节如有冲突需在正式迁移前继续核对，不能冒充已全面阅读。
>
> **根本原则**：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。OOP高内聚、组合优先；GoF仅用于实际变化点；AOP复用日志/耗时/异常与测试；不构造第二套工程树、执行器、适配层、Wrapper、Facade或图元库存。保留现役ReferTo真实功能、坐标语义和结果，逐项验证。

## 1. 工程与交付形态

| 层/Owner | 唯一职责 | 已有实现或约束 |
|---|---|---|
| FIM | 像素、原始图像缓冲、ROIs数值、灰度分析、基本图像处理、工业RAW | `FIMImageBuffer/FIMRawImageReader/FIMImageAnalyzer/FIMImageOperations/FIMImageFilters/FIMImageMeasurement` |
| MVI | 无WPF的图像会话、状态与工具交互、ROI/叠加、原结果图联动 | `MVIImageSession` + `MVIImageViewport` 已有；通用对象工具尚待 |
| MPW | 原生WPF图像呈现、键鼠输入与控制界面；**可嵌入其它应用** | `Imaging/MPWImageViewerControl.xaml(.cs)` 已有 |
| TCT | 可独立启动的TestCenter人工工作区、脚本及验收入口 | `manual.imaging` -> `TCTManualImageViewerFrame`；`--imaging-viewer-smoke` |
| PVC | HALCON/OpenCV的真实 SDK/图像工艺/算法和专业Pipeline | 保留专业Owner；基础算法吸收后不得复制同名数学实现 |
| 其他 | MEM/MRC唯一工程真源、MRK/MOL/MDS任务、统一消息命令、诊断/日志 | 不创建图像专属第二套流程、Undo和任务中心 |

独立程序由现有 `CxVisual.TestCenter.exe` 人工工作区入口承担；嵌入与开发者API由MPW UserControl和FIM/MVI公开类型提供。不再增加第三个“独立Viewer壳工程”。将来正式Vision Workbench可装配同一MPW控件，而不是复制TCT页面源码。

## 2. ReferTo旧成熟代码到当前新Owner

| 源路径/旧能力 | 已使用新类型/方法 | 后续需完成 |
|---|---|---|
| `Vision/CxECTImageWinForms/Services/ImageStatisticsService.cs` | `FIMImageAnalyzer.Calculate/Analyze/GetHistogram/GetLineProfile` | 高级ROI灰度剖面可视、实图等价比对 |
| `Vision/CxECTImageWinForms/Services/ImageMeasurementService.cs` | `FIMImageMeasurement` | UI画线、角度工具、标定配置/结果输出 |
| `Vision/CxECTImageWinForms/Models/ImageDocument.cs` | `MVIImageSession`保留SourceVersion防过期图 | 独立会话保存/恢复与更多比较态 |
| `Vision/CxECTImageWinForms/Models/ImageViewportState.cs` | `MVIImageViewport` Fit/Pan/Actual/ZoomAt | 旋转/镜像下精确光标变换和视图同步 |
| `Vision/CxECTImageWinForms/Controls/ImageViewerControl.cs`、`Halcon/HalconImageCanvas.cs` | `MPWImageViewerControl` 原生WPF浏览、矩形ROI和状态栏 | 大量工具、Overlay/Annotation/批量操作尚缺 |
| `Vision/Halcon/HalconHelper_Gray.cs` | `FIMImageAnalyzer.CalculateFiveRegions` | 与真实HALCON同样本和边界舍入对照 |
| `Vision/Halcon/HalconHelper_Base.cs` | `FIMImageOperations` 裁剪/拆分、窗映射、归一化、融合、Gray16->BGR | 完整算法与HObject/Mat所有权审计，专用算子保留PVC |
| `Vision/CxECT2DViewer` | `TCTManualImageViewerFrame` 和MPW嵌入 | 功能菜单逐项对照，不只对照打开/保存 |
| `ReferTo/CommonInterfaces` | 通用字节/数学工具仍需按Foundation现有Owner核对 | 不能整项目纳入FIM，也不能复制空MathHelper |

历史 S0 的213条CSV是**原文件快照**，非实时完成台账；不能用迁入几个算法就将对应旧工程整体标成Completed。

## 3. 按用户完整60项需求的实证矩阵（均是当前源码事实，非目标宣告）

| 功能方面 | 当前已交付的真实功能 | 缺失或未验证能力 | 状态 |
|---|---|---|---|
| 图像输入/接入（13-15） | 内存FIMImageBuffer、BMP/PNG/JPG/TIFF的WPF本地读取、指定宽高格式的RAW读入、8/16/32浮点与BGR/BGRA | 相机、网络、共享内存、外部指针所有权、图像序列、批量导入、多页TIFF选择 | **部分实现** |
| 显示与导航（16-18） | MPW Fit、1:1、鼠标缩放平移、像素查询、RAW灰度窗 | 旋转后视口坐标映射、镜像原生视图、伪彩、分屏/多图同步、标尺/导航图 | **部分实现** |
| 图像处理（19-21） | FIM裁剪/拼接/归一化/旋转镜像/阈值/差分；均值、中值、形态学腐蚀膨胀、高斯、反遮罩锐化；TCT实际调用 | 更多颜色空间、对比度/伽马、几何插值、处理序列与输入输出参数规范、大图实时性能 | **部分实现** |
| ROI与标注（22-24） | MPW鼠标矩形ROI框选、FIM离散像素矩形 | 点、线、圆/椭圆、旋转矩形、多边形、自由曲线、文字、缺陷框、持久化样式图层、编辑与撤销 | **部分实现** |
| 测量（25-27） | FIM像素/毫米距离、角度、矩形面积；未标定不虚造物理值 | 用户直接绘图测量、圆直径、周长/圆弧、轮廓、标定配置与完整输出 | **仅基础API** |
| 分析（28-30） | FIM ROI灰度统计、直方图、线剖面、五区域均值，TCT展示32档结果 | 连通域、边缘、形状、纹理、频谱、多维颜色统计、配置化结果输出 | **部分实现** |
| 多图比较（31-33） | MVI原/结果图、FIM绝对差分、同类型均值融合 | 透明叠合、滑动对比、注册配准、联动分屏、多视图相同坐标体系 | **部分实现** |
| 算法实验（34-38） | 手工按钮选择算法、FIM输入输出、可视处理结果、CPU耗时和异常信息 | 参数Schema、可复用Pipeline、逐步调试、中间产物树、统一Trace/日志收集与复现 | **初始功能** |
| 批量/实时（39-40） | 文件加载Task+MVI SourceVersion防过时覆盖；FIM计算支持取消参数 | 设备帧流、有界队列/背压、批次、并发调度、增量显示、取消按钮及性能压力验收 | **仅基础路径** |
| 输出（41-42） | MPW截图/当前显示PNG、剪贴板、ROI对象和统计API | 多格式ROI/标注/测量报告、数据及图像序列输出 | **部分实现** |
| 专业集成（43-44） | FIM原始类型与公共MPW控件可复用，Vendor-free，无伪SDK | HALCON/OpenCV真实依赖及算法同步、相机、Inspection/CT3D等专业接入 | **未集成验证** |
| 工程与体验（45-47） | TCT注册到既有人工工作区；工具按钮、参数、ROI、状态栏；MPW单独UserControl | 正式Vision工作区、资源保存、统一Undo、属性/图层管理及快捷键体系 | **部分实现** |
| AI后续（48-51） | 保留普通算法数据输入输出边界 | AI去噪/超分/模型/检测等 | **本轮明确不开发** |

## 4. 完整性与OOP、GoF、AOP

### OOP
- FIM缓冲封装原始像素所有权；内存源构造复制避免引用被修改；FIMPixelRegion专门表示离散像素闭区间，不滥用Geometry连续矩形。
- MVI仅保存可变会话/视口，不把图像的厂商句柄放入模型；MPW只负责WPF像素呈现与键鼠；TestCenter只是这些能力的使用者。
- 公共操作采用现有FIMImageOperations/Filters静态算法，不额外造空ImageManager/Provider层。2D工具栏调用正式API，不再实现第二份过滤器或像素循环。

### GoF
- Observer：MVI会话改变事件通知MPW；对原图、结果图的操作是明确状态转换。工具互斥目前只有`IsRegionTool`最小功能，**不得称已经实现完整State模式/命令体系**。
- 后续ROI对象复用现有MRC图元和现有统一Command/Undo，确实需要不同交互状态时扩展同一工具机制；不可另外建立一套图形/工程树/GraphExecutor。

### AOP
- TCT当前实际在文件加载及FIM处理操作边界用Stopwatch观察耗时，错误由用户工作区状态栏与显式异常处理记录；现行共性日志与Diagnostics的正式接线仍待做。
- 后续在**任务边界**统一记录模块/操作、输入输出维度、时长/取消/异常与关联ID，不在像素热循环插入代理/反射/逐像素日志；复用F19/MRK的调度和现有监控Owner。
- **不增加任何Adapter/Wrapper/Facade中转链**，第三方HALCON/OpenCV原生实现留在Professional，由可用的共享像素/结果契约直接调用。

## 5. 当前真实API与操作入口

```csharp
// FIM原始数据；无WPF/HALCON依赖
FIMImageBuffer image = new FIMImageBuffer(width, height, FIMPixelFormat.Gray16, rawPixels);
FIMImageStatistics stats = FIMImageAnalyzer.Calculate(image);
FIMImageBuffer filtered = FIMImageFilters.GaussianBlur(image, 1.0, 2);
FIMImageBuffer mask = FIMImageOperations.Threshold(image, 1000, 20000);

// MPW原生WPF控件（在UI线程创建）
MPWImageViewerControl viewer = new MPWImageViewerControl();
viewer.ShowOriginal(image);
viewer.ShowResult(filtered);
viewer.FitToWindow();

// TCT可操作的工作区；由现有Registry注册 manual.imaging
TCTManualImageViewerFrame frame = new TCTManualImageViewerFrame();
frame.SetOriginal(image);
```

**CLI验收：**

```powershell
.\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --imaging
$p = Start-Process -FilePath '.\Output\Release\x64\CxVisual.TestCenter.exe' -ArgumentList '--imaging-viewer-smoke' -Wait -PassThru
if ($p.ExitCode -ne 0) { throw 'WPF ImageViewer did not pass' }
```

实际人工操作：运行 `CxVisual.TestCenter.exe`，展开“专有测试（人工操作）” → “二维工业视觉工作台”。允许按像素格式加载文件/RAW，测试处理、ROI、统计与导出；不自动标记任何相机、SDK或设备Passed。

## 6. 工作包顺序（逐功能完成，不允许以数量代替质量）

| 包 | 必须补齐 | 需要的客观证据 |
|---|---|---|
| P1 | 文件/RAW/内存 + MPW基础显示与TCT手工工作台 | Windows全编译、FIM/MVI专项、真实WPF控件及PNG回环（首批已交付） |
| P2 | 旧2D Viewer余下Viewport/Display/Comparison完整能力、图元和全部ROI交互 | WPF人工和UIA，旧菜单逐项对照，真实8/16位数据与重复打开性能 |
| P3 | 图像处理/测量/分析算法库扩充、通用Pipeline输入输出与调试 | 旧HALCON及OpenCV同样本差异、算法结果、异常取消、性能基准 |
| P4 | 批量/实时流、异步加载取消、设备/共享内存、大图内存所有权 | 2GB+/4300²/10GBe等真实目标环境压力、背压、任务隔离、资源释放 |
| P5 | 结果及标注工程化、格式化导出、Studio专业工作台与统一Undo/配置/命令 | MEM保存再开、MRC复用和TestCenter+Studio用户操作 |
| P6 | 工业应用整体功能与SDK验收 | AOI/X-Ray/CT投影/Inspection场景、多厂商配置和HIL，未授权不得Passed |

## 7. Windows CI证据与未通过项

- **已确认**：`f058499` 提交的 <https://github.com/oudream/cx-visual-x3/actions/runs/40205769512> Imaging专项Windows x64全解决方案编译、结构检查、`--imaging`及默认回归、真实WPF窗口PNG往返均成功。
- **已确认**：TestCenter的长期UIA工作流存在单独超时问题，不能拿Imaging专项成功推断整个TestCenter UIA通过。
- **最新新增**：RAW、旋转/镜像/差分、滤波与TCT操作和错误显示属于本轮后续提交，其Windows验收必须以**对应最新SHA实际工作流**为准；不能简单沿用 `f058499` 的结果。
- **未完成**：大图实时性能、HALCON/OpenCV硬件环境、驱动SDK、工业安全及用户列出的完整60项功能，不以本文档或单个Smoke称为正式完成。
