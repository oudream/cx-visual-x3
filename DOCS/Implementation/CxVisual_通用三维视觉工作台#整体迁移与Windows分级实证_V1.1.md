# CxVisual 通用三维视觉工作台——整体迁移与 Windows 分级实证 V1.1

> 日期：2026-10-26。工作范围：`ReferTo/Vision/CxECTVolumeCore`、`CxECTVolumeWinForms`、`CxECT3DViewer`。**ReferTo只读**；本次没有新增 `.csproj`、Adapter、Wrapper、Facade 或第二工程树。每项状态以固定 SHA 的 Windows CI 与当前源码为准。

## 一、原代码整体迁移

- `fcf00bef`：25 个主要成熟 C# 文件按 Owner 原样迁入 FVO/MVI/MPW/Professional.CT3D/TCT，其中未参与编译的文件不冒称 WPF 实现。
- 在对应目标目录中直接更名、调整类型职责并接入正式项目编译：当前 **15 个旧文件已经正式归属和专项 CI 验证**；另外 **10 个旧源文件仍是未编译、待重构的原始拷贝**；23 条为项目配置、参考文档、生成资源及外部依赖。
- [48行逐文件CSV](../Migration/CxVisual_通用三维视觉工作台%23成熟源码整体迁移——逐文件归属与保真验收清单_V1.0.csv) 维护旧路径、新目录、重构状态、固定 SHA、Windows 证据、风险和下一步验收点。

## 二、已有真实源码与 Windows 证据

| 体系 | 新正式实现 | 专项 Windows 结果 | 证据级别 |
|---|---|---|---|
| FVO 核心 | `FVORawVolumeLoader`、`FVOUnmanagedRawVolumeBuffer`、`FVOVolumeData`、`FVOObliqueSliceDefinition` 等 | [40264752336 PASS](https://github.com/oudream/cx-visual-x3/actions/runs/40264752336) | 编译、RAW/原始16bit数值、取消、内存、异常 |
| FVO 切片 | `FVOSliceImageBuilder`（FIM输出） | [40265179437 PASS](https://github.com/oudream/cx-visual-x3/actions/runs/40265179437) | 3正交取样、原标量、空间坐标、非等距 |
| MVI 视图 | `MVIVolumeViewState` | [40265366125 PASS](https://github.com/oudream/cx-visual-x3/actions/runs/40265366125) | 三刀索引、状态变更、视口和任意切定义 |
| FVO 分析 | `FVOVolumeHistogram`、`FVOVolumeIntensityProfile` | [40265545896 PASS](https://github.com/oudream/cx-visual-x3/actions/runs/40265545896) | 预览标量/梯度统计及自动范围 |
| FVO/CT3D | `FVOTransferFunctionSet`、`CT3DRenderingSettings`、`CT3DRenderingPresets` | [40266122454 PASS](https://github.com/oudream/cx-visual-x3/actions/runs/40266122454) | RGBA8/R8 LUT、阈值、Slicer/线性预设、倍率覆盖 |
| MPW GPU 源 | `MPWVolumeShaderSources`、`MPWVolumeGeometry` | [40266385055 PASS](https://github.com/oudream/cx-visual-x3/actions/runs/40266385055) | **仅源码参与WPF程序集编译及GLSL/几何契约**，无真实GPU运行证据 |

### 真实数值变更

旧 `VolumeData.GetScalarByOriginalIndex` 在原始16位 RAW 缓存存在时仍用8位结果回推标量；当前 `FVOVolumeData` 改为直接按原始 RAW 格式从非托管缓冲取得浮点标量，并用专测覆盖末端值1511。GPU预览仍使用8位归一化体以保护交互性能。

旧 `SliceImageBuilder` 的正交空间、物理比例和伪彩算法迁入 `FVOSliceImageBuilder`，输出 `FIMImageBuffer` 而不使用 `Bitmap`；数据与显示映射分开。

## 三、明确未交付

1. **WPF GPU 真正宿主**：旧 `RenderingOpenGlProgram/RenderingVolumeControl/RenderingCamera` 已原样拷贝，但尚未替换 OpenTK WinForms GLControl、未创建和验证真实 OpenGL 上下文、纹理、Shader GPU 编译与资源释放。
2. **可操作四视图界面**：旧 `FourViewLayoutControl/HalconSliceViewControl/SliceViewControl`、传递函数编辑器、方向轴和 `MainForm` 仍待转为原生 WPF，未注册可运行的 TCT Volume 工作区。
3. **GPU、HALCON、实际 CT/ECT 数据与设备**：尚未进行同 RAW 新旧截屏、帧时、显存、原始高位深切片、交互大体积/关闭重开、许可与 HIL 验证。
4. **TestCenter总体Windows UIA**：同期总体工作流有 `Verify real Windows UI Automation user workflow` 步骤失败；Imaging 专项 PASS 不等于 TestCenter 总体验收绿色。

## 四、继续实施的明确路径

下一实际工作包需先用真实Windows GPU测试选择**无需多级Adapter/Wrapper且满足现有Owner DAG**的 WPF 原生 OpenGL 宿主，再把已迁入的 `RenderingOpenGlProgram`、`RenderingVolumeControl`、`RenderingCamera`、`RenderingTransferFunctionEditor`、`FourViewLayoutControl` 等直接重构成可交互 MPW/TCT 实现。

必须保留：OpenGL Raymarch、Slicer灰阶/双不透明度、材质光照/采样抖动、斜切、正交三切面、完整RAW+预览缓存、单MVI状态、旧截图与编辑行为。只有**真实3D WPF窗口启动+加载/交互+GPU截图+内存释放/重开正反验收**才允许宣称三维工作台完成首轮。
