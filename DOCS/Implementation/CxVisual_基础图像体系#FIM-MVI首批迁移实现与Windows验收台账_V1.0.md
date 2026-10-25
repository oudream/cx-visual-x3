# CxVisual_基础图像体系#FIM-MVI首批迁移实现与Windows验收台账 V1.0

> 日期：2026-10-25。依照 [OOP-GoF-AOP实施规范](../Architecture/CxVisual_基础图像与体数据体系%23成熟源码复用与OOP-GoF-AOP实施规范_V1.0.md) 和 [S0 213 文件映射](../Migration/CxVisual_基础图像与体数据体系%23成熟源码迁移——S0主体归属与逐文件台账_V1.0.md) 实施。
> **证据状态**：GitHub源码/项目文件/测试源码已提交；静态路径与依赖检查已确认；**没有可引用的Windows构建和测试通过记录，不应标为正式通过**。ReferTo全程只读。

## 1. 真实提交与范围

| 包 | 真实提交 | 具体源码文件 | 当前证据 |
|---|---|---|---|
| S0 | `94cb2c1` | 六旧工程213文件CSV、主体归属 | 源码树审计 |
| 架构 | `ad88b0e` | 统一的FIM/FVO/MVI/TCT前缀、OOP/GoF/AOP及算法复用 | 文档已提交 |
| P1-A | `60849f9` | `Foundation/Imaging/Pixels/FIMPixelFormat.cs` / `FIMImageBuffer.cs` / `Regions/FIMPixelRegion.cs` / `Processing/FIMImageAnalysis.cs` / `FIMImageOperations.cs`；`Tests/.../FIMImagingTests.cs` | 源码及项目接线已提交、Windows待跑 |
| P1-B | `1b1eafe` | `Images/FIMPixelSpacing.cs`、`Processing/FIMImageMeasurement.cs`、补测量正反例，复用`Foundation.Geometry.FGEPoint2D` | 源码及项目接线已提交、Windows待跑 |
| P2-A | `3d9be3b` | `Mechanism/Visual/Imaging/MVIImageSlot.cs` / `MVIImageSession.cs`，`Tests/.../MVIImageSessionTests.cs` | 源码及项目接线已提交、Windows待跑 |

本轮新增**FIM 7个源码文件**、**MVI 2个源码文件**、**2个测试源码**，更新现有 `.csproj` 和 `Program.cs` 的运行入口。**没有新增任何项目或厂商SDK引用**；没有把旧WinForms程序复制进正式工程。

## 2. 精确旧→新方法映射与重构事实

| 旧方法/对象 | 现行新方法/对象 | 迁移特点 | 尚待比对 |
|---|---|---|---|
| `ImageStatisticsService.Calculate` | `FIMImageAnalyzer.Calculate` | 保留原始像素ROI统计与取消；用Welford计算总体方差，像素计数long | 与旧HALCON同样本像素结果/性能 |
| `ImageStatisticsService.GetHistogram/Analyze` | `FIMImageAnalyzer.GetHistogram/Analyze` | 双遍扫描、稳定分箱、结果只读 | 大图运行时间 |
| `ImageStatisticsService.GetLineProfile` | `FIMImageAnalyzer.GetLineProfile` | 沿线离散采样、原值，不受WW/WL影响 | 亚像素插值扩充 |
| `HalconHelper_Gray.Calc5BoxMeanGray` | `FIMImageAnalyzer.CalculateFiveRegions` | 提取通用4角1中心矩形选择和均值；独立于HObject/HTuple | 旧偶数尺寸/边界中心取整差异 |
| `ImageRectangle` | `FIMPixelRegion` | 离散像素闭区间、Width/Height含两端；无重复`FGEPoint2D` | 双坐标系转换 |
| `ImageMeasurementService.GetDistancePixels/GetDistanceMillimeters/GetAngleDegrees/GetArea*` | `FIMImageMeasurement` 同义方法 | 复用`FGEPoint2D`，`FIMPixelSpacing?`表示可选标定 | 与专业标定数值对照 |
| `CalibrationState.PixelToWorld` 的纯计算部分 | `FIMPixelSpacing` + `FIMImageMeasurement` | 未标定返回nullable而非默认1.0；不复制标定求解 | 真实标定对象绑定 |
| `HalconHelper_Base.VerticalPartVector/VerticalMergeVector` | `FIMImageOperations.SplitVertically/MergeVertically` | 用行复制保留原位深/Stride，整除余数不丢末行 | HALCON RGB/UInt16/大图表现 |
| `HalconHelper_Base.SetImageWL` 部分灰度窗语义 | `FIMImageOperations.WindowToGray8` | 显式产生新Gray8副本，源图不变 | 旧显示动态范围/量化差别 |
| `ImageDocument.SourceVersion/Original/Result/ActiveSlot` | `MVIImageSession` + `MVIImageSlot` | 版本令牌禁止旧异步图覆盖；锁内状态/锁外事件 | UI真实多线程调度 |

## 3. OOP / GoF / AOP 实际运用

- **OOP**：`FIMImageBuffer` 持有并保护原始数据；`FIMPixelRegion` 明确像素闭区间；`FIMPixelSpacing` 不持有专业标定业务；`MVIImageSession` 只持有当前浏览会话，不冒充 MEM 工程文档。图像点位直接使用既有 `FGEPoint2D`。
- **GoF Observer**：`MVIImageSession.Changed` 通知当前View，更新在锁内执行、通知在锁外；**State**：当前原图/结果图切换并校验存在性；不新增Adapter/Wrapper/Facade、第二Undo/消息系统。
- **AOP**：在未来TestCenter任务边界复用既有日志、耗时及异常诊断；FIM热循环不引入反射、代理、频繁日志。**本轮无额外AOP框架，也不虚报运行时性能指标**。

## 4. 现有测试源码与真实运行入口

- `CxVisual.Tests.ArchitectureSmoke.exe --imaging`：运行 `FIMImagingTests.Run()` 与 `MVIImageSessionTests.Run()`。
- ArchitectureSmoke 无参数默认回归同样调用上述两组测试（并保留旧测试原顺序）。
- FIM验证覆盖：灰度8/16/float NaN、BGR、Stride、输入快照保护、ROI、方差与直方图、5区域、线剖面、分拆拼接不丢余数行、16位窗口不改变源数据、错误尺寸、取消，以及物理像素间距/角度/面积。
- MVI验证覆盖：异步旧图拒绝、最新提交接受、同步加载取消旧令牌、原结果切换、Observer通知次数、清空及无图非法选择。
- `.github/workflows/imaging-foundation-windows.yml` 是后续正式Windows CI门禁定义；只把实际有结果的运行记为通过。

### Windows验收命令（需要VS2022+MSBuild17及.NET48目标包）

```powershell
$msbuild = (Get-Command msbuild.exe -ErrorAction Stop).Source
& $msbuild .\CxVisual.Unified.sln /t:Restore,Build /m:1 /nr:false /p:Configuration=Release /p:Platform=x64 /v:minimal
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& .\Build\Verify-Structure.ps1
& .\Build\Verify-OwnerDependencies.ps1
& .\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe --imaging
if ($LASTEXITCODE -ne 0) { throw 'Imaging smoke failed' }
& .\Output\Release\x64\CxVisual.Tests.ArchitectureSmoke.exe
if ($LASTEXITCODE -ne 0) { throw 'Default smoke failed' }
```

## 5. 仍未开发或验证（不能标为完成）

- HALCON/OpenCV其余纯基础算子逐方法归属、OpenCV滤波/形态学、零拷贝/大图分块/外部缓冲所有权、平台化缓存、高位深更多像素格式和真实SDK算法等价性。
- MVI视图Pan/Zoom/ROI手势、Overlay/Undo统一集成；FVO RAW/体素/斜切；TCT两个真正可用的原生WPF Viewer/四视图/图形界面与UIA。
- 当前连接未返回上述源码提交的任何Windows工作流运行，不能将Git提交、源码路径或自带的断言视为Windows构建/运行已通过。
- `ReferTo/CommonInterfaces` 不属于S0当时213文件统计；其中`BufferHelper` 等通用工具仍须按Foundation.Data/Transport/Infrastructure原Owner审计，不能因为引用视觉工具而整包迁FIM。
