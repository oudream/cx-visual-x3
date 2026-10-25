# CxVisual_基础图像与体数据体系#成熟源码迁移——S0主体归属与逐文件台账 V1.0

> **日期**：2026-10-25；**审计仓库**：`oudream/cx-visual-x3`；**基准 master SHA**：`5c9fedd0cb152993cc52a06d9545763c06605b35`。  
> **状态**：S0 源码目录与六工程编译清单审计完成；**新平台迁移状态仍为规划**。本轮未改动 `ReferTo`、正式 C# 源码、项目引用，未执行 Windows 编译/GUI/实机测试。  
> **设计共识**：用户已确认“成熟代码优先、归属确定后直接迁移并原地重构、应改名就改名、应增构就增构、无需新增 Adapter/Wrapper/中转体系”；先确定主体，再完善细节。  
> **重要**：下列目标路径是 S1-S5 的**建议新文件/归属**，并非已创建。实际开发前查重，允许合并和删减，不以类数为验收依据。

## 0. 阅读方式与审计覆盖范围

1. **完整逐文件 CSV**：同目录 [S0文件映射清单 V1.0](./CxVisual_基础图像与体数据体系%23成熟源码迁移——S0文件映射清单_V1.0.csv)；覆盖 `ReferTo/Vision/` 此快照下所有 **213 个 Git blob 文件**，记录原路径、旧编译项、迁移方式、候选新 Owner/文件及代码 SHA。目录本身不是文件；**源文件未复制**。
2. 本次项目源直接基于六份 `.csproj` 的 `Compile/Reference` 列表，并核对完整递归 Git 树（未截断）。旧 C# 源码 **92 个**；PNG **95 张**。逐文件分类完整不等于旧功能逐方法运行验证完成。
3. 非代码文件 `.resx/.config/.settings/.png/.xlsx/.md` 逐文件入账，但仅择需迁移；旧软件的程序依赖、Windows Forms Designer、程序集名及第三方 DLL 不整体进入新解决方案。
4. [旧 2D 工具功能清单工作簿](../../ReferTo/Vision/CxECTImageWinForms/2D看图工具类功能清单_ImageToolbox.xlsx) 已确认存在，**未在本轮逐 Sheet 解析**；因此“功能完整性验收基准”在 S0 尚有一个待核项目，不能声称逐功能全部完成。后续需与以下源码 API 合并核对。

| 旧工程（均在 ReferTo/Vision 下） | Blob文件数 | C#文件数 | 源码作用 |
|---|---:|---:|---|
| CxECTImageWinForms | 133 | 32 | 2D图像Viewer、状态、HALCON图窗、统计、ROI、控件和大量图标 |
| CxECT2DViewer | 10 | 6 | WinForms二维应用入口 |
| CxECTVolumeCore | 15 | 14 | RAW体数据、采样、切片、传递函数 |
| CxECTVolumeWinForms | 16 | 11 | OpenTK/OpenGL体渲染、三视图、旧HALCON/GDI图窗 |
| CxECT3DViewer | 13 | 6 | CT三维WinForms应用入口及历史需求文档 |
| Halcon (HalconDriverModule) | 26 | 23 | HALCON操作、图窗、序列化、HDev/C#测试支撑 |
| **合计** | **213** | **92** | **包括 按需重构:17、不迁移:19、WPF重写:16、保留参考:2、待解析:1、整类迁移:8、拆分+WPF重写:3、重构迁移:12、合并重构:2、拆分重构:5、合并已有:1、合并/增构:2、合并既有MVI:1、合并既有样式:1、资源逐项甄选:95、归属调整:2、整类迁移+整理:5、择优合并:1、合并+WPF重写:2、分组重构迁移:5、测试迁移:10、待裁决:2、不直接迁移:1** |

## 1. 主体冻结：既有项目 / 子方面 Owner

| 所属 | 现有项目与子方面 | 唯一职责 | 不承担 |
|---|---|---|---|
| Foundation | `Foundation/Imaging`（F11/F22） | Image数据/像素格式、缓冲与寿命、坐标纯计算、颜色映射、统计/测量数值、基础2D采样 | WPF控件、HALCON句柄、工程资源编辑历史 |
| Foundation | `Foundation/Volume`（F12） | 体素、RAW、数据布局/Spacing、体数据所有权、切片提取和通用采样/传递函数数值 | CT采集/重建工艺、渲染窗口状态、WinForms/OpenTK界面 |
| Foundation | `Foundation/Geometry`（F05-F07）与 Numerics | 共用点/矩阵/几何、采样数值方法；已有则合并 | 重复ImagePoint/Rectangle/3D矢量系列 |
| Mechanism | `Mechanism/Visual`（M16/M17/M37） | 通用Viewer会话、平移缩放工具规则、叠加投影、交互状态、视图联动 | WPF对象生命周期、第二工程树/第二Undo仓 |
| Mechanism | `Mechanism/Presentation.Wpf` | ImageViewer、SliceViewer、GPU显示控件、键鼠输入、XAML | 工程真源、专业HALCON SDK、直接拥有F.Volume业务数据 |
| Professional | `Professional/Vision`（P30-P42） | HALCON算子/算法、Vision标定及业务测量、专业数据结果 | 基础ImageBuffer/通用场景树 |
| Professional | `Professional/CT3D`（P43-P53） | CT数据语义、MPR/重建/体渲染专业参数、空间交互意图 | 公共像素/文件算法的重复实现 |
| Applications/Tests | `Applications/Studio`、`Tests/ArchitectureSmoke`、`Tests/TestCenter` | 专业工作台组合、开发者测试、GUI/端到端验收 | 第二套平台工程定义 |

**职责冲突裁决**：先前讨论“Foundation无显示状态”表述不够精准。F11/F22 仍拥有**纯视口坐标/颜色/2D呈现数学原语**，而 `Pan/Zoom/WW/WL/SliceX/Y/Z` 等当前会话可变状态放到 MVI；旧 `VolumeState` 既含数据尺寸相关逻辑也含会话状态，需要拆分。图像ROI不是天然的工程资源：临时Viewer测量/Overlay可在MVI；需持久化、引用、Undo的定义必须使用MEM/MRC既有工程真源。

## 2. 固定的直接依赖方向和三项架构门禁

```text
Studio (工程资源/专业页面组合)    Tests (验收)
   │                                │
   ├── Professional.Vision ───────→ Foundation.Imaging
   ├── Professional.CT3D ────────→ Foundation.Volume ───→ Foundation.Imaging（拟增依赖，须实审）
   ├── Mechanism.Visual ─────────→ Foundation.Geometry + Foundation.Imaging
   └── Mechanism.Presentation.Wpf → Mechanism.Visual + Foundation.Imaging
                                   （不直接引用Foundation.Volume/专业SDK）
Foundation.* ─X→ Mechanism/Professional
Mechanism.* ─X→ Professional
```

- **不增加Adapter/Wrapper/Facade或第二数据访问链**。工业图像可直接用单一原生 `ImageBuffer` 或已有HObject专业计算；跨Owner共享只经必要的稳定数据契约，不套数层转发。旧文件名 `Tests/HalconEngineAdapter.cs` 是历史测试代码，不迁为新类。
- 3D GPU显示在 MPW 内编译的通用绘制/Shader技术部分与 CT3D 专业参数严格分开；按现行工程规范 MPW **不直接引用 Foundation.Volume**。Studio/CT3D 通过现有业务装配驱动二维Slice画面与三维绘制，具体GPU上下文/内存交接须做**技术验证后裁决**，禁止为满足图形渲染暗建复杂桥接层。
- 不新增 `.csproj`、WPF第二工程树、独立图像Undo/命令总线；旧 `HObject`、`HWindowControl`、`System.Drawing.Bitmap` 和 `OpenTK.GLControl` **不允许渗入Foundation公共API**。

## 3. 旧代码重点迁移决议与首批对象

| 旧源码与真实功能 | 新位置/建议处理 | 不允许退化的行为 | 验收编号 |
|---|---|---|---|
| `ImageStatisticsService` / `IImagePixelSource` | F.Imaging/Processing+Pixels，算法保留，类型按目标规范整理 | ROI像素统计、直方图、一次扫描、取消、原始像素语义 | IM-01/02 |
| `ImageMeasurementService`、`CalibrationState` | F.Imaging/Processing+Images，专业标定算法留Vision | 物理距离在未标定时不伪造；旋转/缩放不改变测量值 | IM-03/05 |
| `ImagePoint/ImageSize/ImageRectangle` | 查重FGE后合并，像素闭区间边界单独正确表达 | Right/Bottom含最后一个像素，不能与连续矩形无声混用 | IM-04 |
| `ImageTransformState`、`ImageViewportState`、`ImageDisplayState` | 视图可变态→MVI；纯像素映射→F.Imaging | 旋转逆变换、镜像、Fit、WW/WL、Gamma分层正确 | MVI-02 |
| `ImageDocument`、`ImageSlot`、`ImageComparisonState` | MVI/ImageSession与F.Imaging/ImageMetadata | SourceVersion拒绝过期异步图；原图/结果图切换融合不丢 | MVI-01/03 |
| `OverlayStore/Item/Palette` | 先与MVI现有投影/MRC样式合并，专业缺陷结果留Vision | 同一图元/标注身份，临时叠加不生第二持久对象 | MVI-05 |
| `ImageViewerControl`、`HalconImageCanvas` | 拆交互计算入MVI，原生WPF控件放MPW | ROI创建/编辑、鼠标工具互斥、图层、Zoom、快捷键、标尺 | UI-01～07 |
| `RawVolumeLoader`、`VolumeData`、`UnmanagedRawVolumeBuffer` | F.Volume/IO+Volume+Storage | Byte/UInt16/Float32原值、原尺寸、Spacing、原生内存释放 | VO-01/02 |
| `SliceImageBuilder`、`ObliqueSliceDefinition` | F.Volume/Slicing；Slice像素结果复用F.Imaging | XY/XZ/YZ/斜切、原始精度、边界/插值、物理比值 | VO-03 |
| `VolumeState` | 体数据元信息→F.Volume；三刀和Viewport→MVI | 2D十字线与3D切面共享当前XYZ坐标；绝不复制独立状态 | MVI-06 |
| `RenderingTransferFunctions`、`RenderingSettings` | 纯数值→F.Volume；CT预设及采样参数→Professional.CT3D | 传递函数及可调参数保持；原始物理灰度范围不丢 | VO-04/CT-01 |
| `RenderingOpenGlProgram/Geometry/ShaderSources/Camera` | 保留算法与GLSL，原生WPF宿主评估后入MPW | 3D raymarch、方向、色表、裁剪/性能、资源释放 | UI-10～12 |
| `FourViewLayoutControl`、`HalconSliceViewControl`、`SliceViewControl` | Studio编排+MPW统一Slice控件，替换旧WinForms | 四视图、十字线、窗宽窗位、伪彩、3D彩色切面、截图 | UI-13～15 |
| `HalconHelper_*`、`HalconDictIOHelper`、HALCON测试 | Profession.Vision专业运算+TestCenter真实专项 | 旧HALCON算法/字典/HDev运行行为保留，不把第三方引用带进F/M | PV-01～04/TEST-03 |

> **对象命名规则**：旧类若契约正确可保留名字；需合并直接合并到已有对象，不机械制造“一个旧类=一个新类”，不要求所有新类改同一三字母前缀。CSV中 `A+B` 表示一个旧文件的多职责拆分建议，**不是允许依赖链穿透**。

## 4. 迁移步骤：先主体，再细节；每阶段一提交一验收

| 阶段 | 核心工作包 | 新平台验收门槛 |
|---|---|---|
| **S0** | 本源代码/资源快照+全文件台账，确定Owner、边界、已有能力和未裁决项 | Git tree/csproj路径实核；审计文档入库。**不得称Windows已通过** |
| **S1** | F.Imaging：ImageBuffer、像素/PixelFormat、统计/直方图、测量、坐标纯算法；旧方法优先 | F.Imaging独立MSBuild + FoundationSmoke/ArchitectureSmoke与旧结果同样本一致，异常/取消反证 |
| **S2** | MPW 2D ImageViewer + MVI ViewSession/ROI/Overlay/工具；旧2DViewer UI对照 | 真WPF鼠标/键盘、ROI/测量/显示/保存/结果比较、内存/DPI、Windows UIA |
| **S3** | F.Volume：RAW/原始精度/降采样/数据寿命/切片与通用transfer | Byte/UInt16/Float32和非等距体素数值校验，原始切片不错误转8位，释放回归 |
| **S4** | MPW 3D GPU + Studio CT3D四视图组合 + P.CT3D专业状态/参数 | XY/XZ/YZ联动及3D切面同坐标，纯WPF工作台、真实GPU/WPF技术验证、体渲染性能 |
| **S5** | Vision/CT3D HALCON及检测集成、Studio/TestCenter端到端 | 既有专业结果不退化，所有旧功能逐项对照，独立UI/性能/必要HIL；未授权不得Passed |

**每工作包工作流**：选功能/旧入口 → 核验旧方法/依赖 → 核验现有目标Owner与重复类型 → 成熟方法直接迁入/合并 → 补最小必要增构 → 真Windows build + 正反验收 → 按旧样本回归 → 单轮中文提交。未通过可继续同工作包修复，不能为赶进度标为完成。并行开发仅修改本工作包所属文件，推送前检查 master HEAD，禁止覆盖其他团队提交。

## 5. 完整功能追溯与验收用例编号

| ID | 功能/类型 | 必测正反例与可观测证据 |
|---|---|---|
| IM-01 | 2D像素格式与所有权 | Gray8/16、BGR/RGB、Stride/ROI、大小端和Buffer生命周期；越界、坏大小和并发释放 |
| IM-02 | 统计、直方图、灰度曲线 | ROI均值/方差/最值/直方图与旧软件同输入比对；窗宽位变化不改变原始统计 |
| IM-03 | 像素/物理测量 | 角度/长度、像素间距、未标定空值；几何变换保持世界测量 |
| IM-04 | 点/矩形/像素坐标 | 旋转/翻转正逆映射、闭区间Right/Bottom、空ROI/极大坐标 |
| IM-05 | Image Metadata与标定间距 | 无图、空图、多通道/位深、不同XY mm/pixel、非法尺度 |
| MVI-01 | 2D Document/Session | SourceVersion过期异步加载不得覆盖当前原图，异常取消和结果图切换 |
| MVI-02 | 视口/色阶/变换 | Fit/Actual/Pan/Zoom、任意旋转、Gamma、反色、WW/WL与坐标一致 |
| MVI-03 | 图像比较 | 单图/原结果切换、透明融合和同步视口，尺寸不同/缺图行为 |
| MVI-04 | ROI、标注工具状态 | Pan/ROI/Measure互斥、取消、MouseCapture丢失、撤销规则 |
| MVI-05 | Overlay/专业缺陷层 | 图层开关、编辑/删除、浮动坐标与持久工程身份不重复 |
| MVI-06 | Volume三刀状态 | 切片XYZ、Slice Viewport、Crosshair，滚轮/拖拽更新全局状态 |
| VO-01 | RAW加载 | 8/16/Float、尺寸猜测边界、类型、文件长度、多端序/损坏流 |
| VO-02 | Volume储存与所有权 | 原始/降采样两路，Spacing/标量范围一致，持有/Dispose/GC压力、巨量体数据 |
| VO-03 | 三正交/斜切 | XYZ方向映射、切面取值、插值、非等距体素、边缘越界 |
| VO-04 | Transfer/Volume统计 | 色表/灰度窗、opacity曲线、真实标量范围与预览采样区别 |
| UI-01～07 | 二维WPF浏览 | 加载/异步/缩放/旋转/镜像/复制保存/ROI测量/直方图/灰度曲线/标尺，实际Windows UI |
| UI-10～12 | 原生WPF三维渲染 | Shader/Camera/GL上下文、orientation、transfer编辑、显存回收，WPF宿主真实编译 |
| UI-13～16 | 4视图及旧HALCON画面交互 | 同一点联动、最大化/复位、伪彩/切面开关、截图、像素/物理坐标 |
| PV-01～04 | Vision HALCON算子 | HObject生命周期、真实算法结果、I/O字典、缺陷归属待裁决 |
| TEST-03 | HALCON/HDev专项测试 | 真实许可SDK、Windows x64、好/坏样本、失败明确；不允许默认为Passed |

## 6. 风险、待裁决问题（S0不越权确定）

1. **旧功能表未解析**：`2D看图工具类功能清单_ImageToolbox.xlsx` 待逐Sheet与旧Viewer工具菜单核对。此项完成前不能说“完整功能矩阵已关闭”。
2. **跨组件语义**：F.Imaging的像素闭区间、Geometry连续坐标、F.Volume世界毫米/体素坐标需固定明确的换算约定；不为互通再造独立图像模型。
3. **WPF GPU落地**：现有OpenTK 3.3.3 / GLControl依赖 WinForms。GPU宿主选择与性能需要专项真实Windows Spike；不迁移GLControl为默认Viewer，不先创建抽象RendererAdapter链。
4. **HALCON依赖**：HALCON XL、hdevenginedotnetxl、OpenCvSharp与运行许可/实体DLL均由旧工程声明，不代表x3目标机现已具备；保留专业边界与软件许可证合规。
5. **专业对象归属**：`NGResultAreaSerializeStructs.cs` 与 `UserDefectContentHelper.cs` 涉及Inspection输出可能性；已在CSV明示Owner待核，不能误标为通用Foundation。
6. **配置与性能**：旧UI存在共享静态回调和大量WinForms交互状态。需实测大图(如3072×1536、4300×4300)、16位、2GB以上RAW、多线程加载、GPU释放；不能只证明示例能打开。
7. **历史基线**：`DOCS/ProjectCatalog.csv` 对多个项目仍写 Assembly-only skeleton，是早期台账；本次已直接查 `Foundation/Imaging` 与 `Foundation/Volume` 两项目的 `.csproj` 仍只有AssemblyInfo，不代表所有MVI/Studio还空白。下轮逐目标文件查重，以源码为准。
8. **资料优先级**：`DOCS/01` Review有旧抽象术语，`DOCS/02～04` 巨型工作簿未在本轮逐Sheet阅读；Owner用现行工程规范、真实csproj和用户确认的迁移原则收敛；若工作簿后续细项有异议，先提裁决而非静默改名。
9. **不增加中转架构**：只有真实可测的性能、所有权或运行时边界才考虑新增公开抽象。GoF=必要变化点的局部策略/观察/组合；AOP=复用已有日志/性能/异常/生命周期基础，不追加新框架。

## 7. 交付与验收记录

- **本轮实际成果**：固定SHA源目录递归树全 213 文件归类、六份旧csproj编译清单、92 个C#源码映射、95 个PNG资产去向、目标Owner与阶段/反证测试编号。
- **不属于本轮成果**：原代码移植、WPF显示、第三方SDK实测、Windows编译通过或CI绿色、现场验证。
- **后续最小可交付工作包 S1-1**：先审核旧 `ImageStatisticsService.cs`、`IImagePixelSource.cs`、`ImageMeasurementService.cs`、`ImageRectangle.cs` 与FGE已存在点/矩形；迁入F.Imaging并补最小测试。完成后按同一CSV将对应行从“规划/未迁移”改为实证状态。
- **与上轮聊天补丁区别**：旧 P0 ZIP 不在最新 master 正式实现中，不能当作已验收基线，且本方案遵循“不增 Adapter/Wrapper”的收敛要求。
