# CxVisual 基础图像与体数据体系——成熟源码复用与 OOP / GoF / AOP 实施规范 V1.0

> 日期：2026-10-25；状态：**方案定案，源码分批实施中**。主仓库：`oudream/cx-visual-x3`，开发基准 master（执行时必须核实最新提交）。
> 背景：用户明确要求以 `ReferTo` 现役成熟项目为来源，**ReferTo 只读**；先确定主体再完善细节，应改名就改、应增构就增；全面运用 OOP、GoF、AOP，但**禁止没有必要的 Adapter、Wrapper、Facade 或多级转发框架**。两个 Viewer 暂时作为 TestCenter WPF 验证功能。
> 依据：`AGENTS.md`、工程 skill、`DOCS/00`、`DOCS/01`、现行 F/M/P 工程 Owner；以及 [S0 六工程、213 文件迁移台账](../Migration/CxVisual_基础图像与体数据体系%23成熟源码迁移——S0主体归属与逐文件台账_V1.0.md)。设计与可运行事实不得混淆。

## 1. 主体、唯一 Owner 与物理项目（不新增 csproj）

| 主体 | 工程 | 前缀 | 核心职责 | 明确不拥有 |
|---|---|---|---|---|
| F11/F22 | `Foundation/Imaging` | **FIM** | 图像像素缓冲/位深/Stride/ROI数值/灰度、颜色、基础处理与统计，含来自 HALCON/OpenCV 的**无业务通用计算** | UI控件、HObject/Mat公有契约、检测工艺 |
| F12 | `Foundation/Volume` | **FVO** | 体数据/体素/RAW/切片/插值/传递函数基础数值；切片尽可能复用 FIM 数据类型 | 3D窗口状态、专业CT工艺 |
| M16/M17/M37 | `Mechanism/Visual` | **MVI** | 图像/体视口和会话、Pan/Zoom、工具互斥、ROI编辑、图层投影、三刀联动 | 第二工程树/Undo仓、WPF类型 |
| Test | `Tests/TestCenter` | **TCT** | 可操作的2D ImageViewer与3D VolumeViewer，功能/异常/性能/资源验收 | 正式算法的第二份实现 |
| WPF公共控件（后续） | `Mechanism/Presentation.Wpf` | **MPW** | 经TestCenter验证后真正通用的原生WPF绘制控件 | 业务状态、专业SDK、另一个工程真源 |
| 专业 | `Professional/Vision`、`Professional/CT3D` | **PVC** / CT3D现行约定（实施前查核） | 供应商图像引擎、检测工艺、CT分析与重建、专有算法 | 通用图像/体素数据再建一套 |

**设计裁决**：用户所说“一个基础项目、一个机制项目”适用于本轮 **2D FIM + MVI 的主迁移链**；F12 Volume 是现有冻结项目，保留独立而不物理合并，避免破坏28工程、135子方面 Owner。

## 2. 复用范围扩展：不仅六个 Vision 工程

| 旧来源 | 可抽取的纯基础能力 | 不可直接下沉的部分 | 正式处理 |
|---|---|---|---|
| `CxECTImageWinForms/Services/ImageStatisticsService.cs` | 原始灰度统计、标准差、直方图、LineProfile、取消 | WinForms/HALCON具体像素源 | 迁到 FIM；校正数值稳定性、空ROI、Stride/大图 |
| `CxECTImageWinForms/Services/ImageMeasurementService.cs` + `CalibrationState.cs` | 线长/角度/面积/像素间距、未标定返回null | 实际Vision标定求解 | 纯计算归 FIM/Geometry；标定算法仍归专业 |
| `Halcon/HalconHelper_Gray.cs` | 四角和中心五区域灰度统计、ROI几何、边界裁剪 | HObject创建、ReduceDomain、HOperatorSet | 提取数学语义到 FIM；原生HALCON原方法暂留专业 |
| `Halcon/HalconHelper_Base.cs` | 灰度映射、格式校验、裁剪、拆分/拼接、公共线段几何 | HDevEngine、HObject/Mat互转、显示窗口、专业图像工艺 | 逐方法裁决：FIM / Geometry / PVC；不可整份partial迁走 |
| `Halcon/HalconWindowTools.cs` | 绘图测量算法、ROI选取与操作规则 | 旧WinForms控件、HALCON生命周期 | MVI与TCT；不得把10万字符旧图窗整类移动 |
| `CommonInterfaces/BufferHelper.cs` | 小端/大端、HEX、字节格式 | 和图像业务无必然关系 | 优先 Foundation.Data/Transport/Core已存在Owner，**不塞进 FIM** |
| `CommonInterfaces/MathHelper.cs` | 无 | 当前仅注释/空类 | 不迁移 |
| 旧 Vision/OpenCV 的 `Cv2`、`HObjectToMat` | 可识别的无业务像素/颜色/矩阵算法 | 第三方Mat/HObject资源与许可 | 抽纯算法入 FIM；**不新增转发器**；真实依赖继续由专业直接使用直到SDK方案获验收 |
| `CxECTVolumeCore` | RAW、原始/预览双路、三正交/斜切与数值传递函数 | 旧体视图状态 | FVO与MVI分别直接迁移，不建新数据图 |
| `CxECT2DViewer`/`CxECT3DViewer` | 完整可交互用例 | WinForms启动、Designer、GLControl | 两个 TCT 可操作 WPF Viewer，随后通用绘制再收敛到 MPW |

### “基础算法”和“基础程序集依赖”必须分开理解

判定是否进入 FIM：①不含产品/检测工艺语义；②输入输出的公开类型不是第三方专有句柄；③能复用于Vision和CT3D至少两个场景；④有真实使用例和可验证数值样本；⑤有明确性能/内存所有权。**无业务语义不自动意味着可以把 HALCON/OpenCV SDK 编译引用塞进 FIM**。当前 FIM csproj 只有 System/System.Core，第三方DLL和授权尚未复核，因此首轮确保**零厂商依赖的独立可用图像基础**。后续如确定普遍需要 HALCON/OpenCV 加速，应提交单独依赖与部署评审，不自造Adapter/Provider项目。

## 3. 核心对象（先最小闭环、后扩充）

| 优先 | 预计类型（不是预设必须造类） | 归属 | 概念/算法 |
|---|---|---|---|
| P1-A | `FIMPixelFormat`、`FIMImageBuffer` | FIM/Pixels | Gray8/Gray16/Float、彩色、Stride、拷贝快照/生命周期，严格像素所有权 |
| P1-A | `FIMPixelRegion` | FIM/Regions | 离散闭区间像素ROI，区别FGE连续空间矩形 |
| P1-A | `FIMImageStatistics`、`FIMImageHistogram`、`FIMImageAnalysis`（若合并合理则合并） | FIM/Processing | ROI、Min/Max/Mean/StdDev、直方图、灰度剖面；结果只读 |
| P1-A | `FIMImageOperations` | FIM/Processing | 裁剪、拆分/拼接、位深变换、窗映射；原值不因显示变更 |
| P1-B | `FIMImageMeasurement` | FIM/Processing 或 Geometry | 像素/毫米线长、面积、角度，缺失标定返回null |
| P1-C | 选取有实证价值的滤波/增强/形态学/颜色转换 | FIM/Processing | 先审核旧HALCON/OpenCV纯算法与目标行业需求，不抢先造空类 |
| P2 | MVI已存在类型+少量`MVIImageSession/ViewState` | Mechanism.Visual | 原/结果图、SourceVersion、工具互斥/ROI变更/联动 |
| P3 | FVO既有 VolumeData/RAW/Slice代码 | Foundation.Volume | 体素/切片空间；不引入第二二维像素类型 |
| P4 | 两个`TCT*ViewerFrame` | TestCenter | 真WPF图像/3D可视交互回归+UIA验收 |

### 精确数据语义

- 原始灰度计算与视觉显示窗必须分开；Gray16不会被隐式转成8位；Float像素若含NaN/Infinity必须明确计算错误行为。
- **像素坐标**：X向右、Y向下；`FIMPixelRegion`四条边都包含；连续`FGERect2D`宽度不加1，二者不能无声强转。
- Gray8/Gray16/Float32/Bgr24/Bgra32 要记录字节排列、行步长、像素范围、字节序；所有尺寸乘法使用 checked 防溢出。
- 先实现正确的复制所有权；面向大图的`MemoryOwner`/原生缓冲和跨进程零拷贝需明确资源所有权后再增构，避免悬挂指针。
- 旧源函数`ImageDocument.SourceVersion`防异步旧图覆盖保留到 MVI；不复制 MEM 工程文档。

## 4. OOP、GoF、AOP 运用门禁

**OOP**：值/状态/行为各自Owner，组合优先，公开API不露原生指针；无状态算法用静态计算，不强迫每类都继承Base或拥有接口。避免一个上万行“万能ImageManager”，也避免几十个空“XxxService”。
**GoF**：仅在可观察的变化点使用既有`Observer`事件/订阅（视图同步）、`State`工具互斥（MVI实际有状态转换）、`Command`复用已存在M12机制（持久工程操作），`Composite`复用既有MVI/MRC图元；纯FIM裁剪不需模式。禁止为厂商图像转换编造多级Adapter/Wrapper/Facade链。
**AOP**：利用现有 Infrastructure 的日志、性能指标/异常与TestCenter证据记录；跨线程任务在边界采集耗时/大小/失败，不在每像素循环反射、织入或记录日志。错误和取消保留原始异常、样本、函数名、相关性ID，遵守F19/MRK统一责任边界。

## 5. 实际实施工作包（按阶段一工作包一提交）

| 包 | 文件/动作 | 可核验结果与禁止冒充项 |
|---|---|---|
| **P1-A 本轮** | FIM像素/ROI/统计、直方图/五点区域分析、通用裁剪/纵向拼接、传统`.csproj`显式Compile、ArchitectureSmoke --imaging | 必须跑静态路径/类型/无SDK依赖检查；Windows .NET48 MSBuild + 数值测试仍需Windows真实执行 |
| P1-B | 测量与标定间距、直线剖面、颜色通道、更多灰度处理、异常取消 | 与旧ImageMeasurement、HALCON旧数值可复验 |
| P1-C | 从CommonInterfaces/HALCON/OpenCV **逐方法**细分、最值得移入FIM的算法 | 用实际用例、同样本、同等性能、SDK/内存所有权逐项验收 |
| P2 | MVI ImageSession/ROI/Overlay/ToolMode；原图与结果图 | 状态机互斥、SourceVersion、Undo和MRC唯一工程 |
| P3 | FVO VolumeData/RawVolumeLoader/Original Slice | UInt16/Float RAW、Spacing、XYZ/斜切、巨大体数据生命周期 |
| P4 | TCT两个原生WPF Viewer（含可交互菜单、2D和3D） | UIA、DPI、16位图、MPR同步、旧Viewer功能矩阵 |
| P5 | PVC/CT3D专业算法结合、测试、性能和部署 | 不以无SDK模拟测试冒充工业实机验收 |

## 6. 验收矩阵、可见证据

1. ROI全图/角点/完全越界/部分相交；Gray8/16/Float、BGR、Stride、16位大灰度、不合法负尺寸、溢出、空缓冲。
2. 原图先统计，再修改显示窗，统计数值必须一致；常量图直方图正确；超大灰度数值方差稳定；取消任务可观测。
3. 对 HALCON 五区域采样：左上/右上/左下/右下/中心顺序正确；Margin越界、ROI超过图像时明确报错；同图同ROI与HALCON结果比较。
4. 裁剪/拼接包括高度不能整除分片数，禁止旧算法丢最后几行；原始灰度不能被隐式量化；不会写坏输入数组。
5. `Foundation.Imaging` **不引用**`HalconDotNet`、`OpenCvSharp`、WPF/WinForms/Professional；不添加新工程或二次API转换层。
6. 运行 `CxVisual.Tests.ArchitectureSmoke.exe --imaging`、默认回归、Windows VS2022/MSBuild17 Debug+Release x64；UI用 TestCenter 真实 WPF 操作；需要 HALCON 许可的专用验证另列。
7. **证据层级**：设计确定 / 源码已存在 / 静态审计通过 / Windows编译通过 / 数值测试通过 / UI通过 / 专业SDK和现场验收通过。不能跳级、不能把代码提交当作“验证通过”。

## 7. S0历史台账修订规则

先前 S0 CSV 以旧 `ReferTo/Vision` 六工程为完整范围。新增的 `ReferTo/CommonInterfaces` 属于**额外来源**，必须单独纳入后续迁移审计；不能把S0的“213文件”误称为现阶段所有参考库的完整清单。每个新迁入方法要记录 `旧路径/方法 → 新Owner/类型/方法 → 具体提交 → 编译/测试/GUI验证`；可沿用S0 CSV，但历史快照不可假称已经全部完成。
