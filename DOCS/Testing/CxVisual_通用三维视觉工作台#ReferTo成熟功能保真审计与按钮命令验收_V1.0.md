# CxVisual 通用三维视觉工作台——ReferTo 成熟功能保真审计与按钮命令验收 V1.0

> 核对日期：2026-10-26；核对基线：`master / f99f0e0520c563e9800fdf658495ef648019f495`。
> 核查来源：`ReferTo/Vision/{CxECT3DViewer,CxECTVolumeWinForms,CxECTVolumeCore,CxECTImageWinForms,CxECT2DViewer,Halcon}` 的实际源码，新平台 FVO / MVI / MPW / CT3D / TestCenter 源码、编译项和专用 Windows 验收。
> 证据口径：**旧成熟源码 ≠ 新版已接通 ≠ Windows 自动化通过 ≠ 实卡画质通过**。ReferTo 保持只读；新代码不引入 WinForms 宿主或 Adapter。

## 一、审核结论及保护规则

旧三维主体（`CxECT3DViewer` / `CxECTVolumeCore` / `CxECTVolumeWinForms`）的真实 GPU Raymarch、相机、三刀切面、原始 RAW、各种传递函数、直方图和原生 UI 操作具有成熟源码，须作为**行为保真基线**。新平台的 FVO/MVI/MPW/CT3D 分责已存在，不能为了“旧版恢复”另建一份 VolumeState、RenderingSettings、TransferFunctionSet 或 Windows Forms 宿主。

特别提醒：`Tests/TestCenter/Manual/Volume/MainForm.cs` 和 `MainForm.Designer.cs` 是旧源码保留副本，**没有列入 TestCenter 的 csproj 编译项**。它们不是新 WPF 功能实现或 Windows 通过证据；功能以 `TCTManualVolumeViewerFrame` 及其 partial 和正式 MPW/FVO 类为准。

旧 `CxECT2DViewer`、`CxECTImageWinForms`、`Halcon` 包含比本工作台更广泛的二维视觉、SDK 和 ROI 能力，**不能因为三维工作台的 MPW 二维切片能显示，就宣布整套 Vision 均迁移完毕**。专业 HALCON SDK 保留专业 Owner，不混进 FVO 或通用 MPW 公开契约。

## 二、旧能力 → 新 Owner → 真实性/缺口

| ID | ReferTo 成熟能力与源码 | 新平台责任与源文件 | 本次审核判定/需验收 |
|---|---|---|---|
| V01 | `CxECTVolumeCore/Volume/RawVolumeLoader.cs`，RAW 8/16/float、元数据、抽样预览 | `Foundation/Volume/IO/FVORawVolumeLoader.cs`，`TCTManualVolumeLegacyLoad.cs` | 已有实际解码和后台预览代码；异常/取消/不同位深须独立反证 |
| V02 | `UnmanagedRawVolumeBuffer.cs`，完整精度切片缓存 | `FVOUnmanagedRawVolumeBuffer.cs` 与 FVO 原始体素读取 | 已有小数据非托管/大数据只读文件映射；多GB时内存、缓存失效与文件生命周期未实机压测 |
| V03 | `VolumeData.cs`，Spacing、原始尺寸/标量 | `FVOVolumeData.cs` | 已实现；各种原始标量读写与几何坐标须验证 |
| V04 | `VolumeState.cs`，三刀唯一状态、联动 | `MVIVolumeViewState.cs`，`TCTManualVolumeCrosshairs.cs` | 已实现共享 MVI，Windows WPF 有状态回归 |
| V05 | `SliceImageBuilder.cs`，XY/XZ/YZ 与伪彩、窗宽 | `FVOSliceImageBuilder.cs`，`MPWImageViewerControl.xaml.cs` | 已实现；换层须保持放大/平移、高清原始切片非降采样；已补回归 |
| V06 | `ObliqueSliceDefinition.cs`，任意切定义/取样 | `FVOObliqueSliceDefinition.cs`、`FVOObliqueSliceBuilder.cs` | 已实现独立中心、旋转、插值、RAW/PNG 输出；大型真实体响应待验 |
| V07 | `RenderingHistogram.cs` 和 `RenderingIntensityProfile.cs` | `FVOVolumeHistogram.cs`、`FVOVolumeIntensityProfile.cs` | 有实际数值代码；UI 直方图异步更新，需做图形值范围比对 |
| V08 | `RenderingPresets.cs` / `RenderingTransferFunctions.cs` | `CT3DRenderingSettings.cs`、`FVOVolumeTransferFunctions.cs` | LUT/参数链已存在；**当前 CI 未证明每组参数导致实卡像素差异** |
| V09 | `RenderingShaderSources.cs`、`RenderingGeometry.cs`，GLSL Raymarch | `MPWVolumeShaderSources.cs`、`MPWVolumeGeometry.cs`、`MPWVolumeOpenGlControl.cs` | OpenGL 3.3 真正的 GPU 路径存在；**Windows 一般 runner 不等于 NVIDIA 实卡** |
| V10 | `RenderingOpenGlProgram.cs`，GPU资源、Shader 编译 | MPW WGL/OpenGL、Program/VAO/VBO/LUT生命周期 | 有真实代码与 WPF 回退；GL显存、上下文释放及厂商驱动兼容必须实机验 |
| V11 | `RenderingCamera.cs`，拖动旋转/滚轮缩放/复位 | `MPWVolumeOpenGlControl` 原生 HWND 鼠标 + `MPWVolumeSliceSceneControl` | 已修复并给 WPF 相机状态回归；**真正 GPU HWND 消息用独立 `--volume-gpu-smoke` 验** |
| V12 | `RenderingOrientationIndicatorControl.cs`，XYZ方向标及双击复位 | MPW GPU 原生 GL 方向轴 + WPF 后备方向控件 | 本次补 GPU 轴与“显示3D方向坐标”开关联动；GL实际外观待专用实卡截图 |
| V13 | `RenderingTransferFunctionEditor.cs`，阈值/色彩/标量/梯度控制点 | `TCTVolumeCurveEditor.cs`、`TCTVolumeThresholdEditor.cs`、`TCTManualVolumeTransferPanels.cs` | 实际拖动/双击/删除/颜色写回已有；要逐项验证边界点/无效参数和 GPU LUT 结果 |
| V14 | `HalconSliceViewControl.cs` / `SliceViewControl.cs`，SetPart、缩放、平移、三刀十字线 | MPW ImageViewer + `TCTManualVolumeCrosshairs.cs` | 已有事件/数据路径；与原 HALCON 不同的像素、鼠标、物理坐标行为需 A/B 实机验证 |
| V15 | `FourViewLayoutControl.cs`，四视图比例与单窗切换 | `TCTManualVolumeLegacyUi.cs` 与 `MPWVolumeSliceSceneControl.cs` | 实现原生 WPF 2×2 与窗口最大化；在 TestCenter 嵌入、DPI/窗口尺寸下需 UIA |
| V16 | `CxECT3DViewer/MainForm.cs`，RAW、参数、快照、切片/场景一体操作 | `TCTManualVolumeViewerFrame.cs` 及 5 个 partial | 原命令入口仅 3 项；本轮补 20 项工作区命令，需避免“按钮可见、命令不存在” |
| V17 | `MainForm.Designer.cs`，旧工作区的视觉基准 | TestCenter WPF 上/下双层选项、3D 图窗与 430px 可调侧栏 | 界面已呈现，但尺寸、标签和完整鼠标 UX 尚未做到旧版逐像素等同 |
| V18 | `Halcon/*` 与 `CxECTImageWinForms/*` 专业/通用 2D 工具全集 | 专业 Vision、Foundation.Imaging / Mechanism.Visual、MPW Imaging | **不在本次三维 Viewer 完成声明范围内**；另按二维工作台清单审计 |

## 三、按钮、命令、事件可用性反证

新的 20 项工作区命令与按钮复用一个函数入口：打开 RAW / BMP、加载演示体、清空、适合四视图、三刀居中、相机复位、应用窗宽窗位与灰阶、保存四视图/单视图、GPU 开关/截图、3D 缩放与平移、任意切更新与两种输出。

特别检查：
1. 没有体数据时，`CanExecute` 必须禁止清空、截图、窗口参数与相关三维操作；调用者即便直接执行也应得到明确失败文本。
2. UI 命令入口输入错误须呈现操作失败，不允许未处理异常杀死 TestCenter；验证非法窗宽之后修正输入仍可继续工作。
3. 用真实 WPF `Slider.ValueChanged` 修改切片、渲染滑杆；不得仅调用无视图的内部方法冒充用户验收。
4. 切换 XY/YZ/XZ 层后不重置已放大/平移的 MPW 图窗；用户明确打开“每次换层自动适合视图”时例外。
5. 3D 以真实GPU体绘为优先；OpenGL失败时须标识回退、保留 WPF 可见切面；回退模式不冒充 GPU 渲染画质。
6. “显示3D方向坐标”应同时影响 WPF 后备控件和原生 GPU framebuffer，不可只修改当前隐藏的 WPF 控件。
7. 体渲染阈值、颜色、标量/梯度透明度必须由真实 FVO LUT 和 GPU 纹理读取；算法和 UI 不能各存第二份定义。
8. 更换 RAW、取消载入、工作区关闭，应取消后台任务并先释放 GPU 资源、再释放 FVO 原始缓存。
9. PNG 导出须写出实际文件并校验尺寸/有效负载；HwndHost 因 Airspace 不能直接由 WPF RenderTargetBitmap 冒充实际 GPU 内容。
10. 独立 `--volume-gpu-smoke` 在真实 Windows GL 3.3 机器上执行：GLSL、体绘可见帧、Win32 鼠标旋转/平移/缩放、方向标、反复装载及上下文释放。
11. 用户拖动光照、全局不透明度、采样步长及切面显示滑杆，只能更新GPU uniform/显示状态；只有体数据、预设、自动灰阶和传递曲线确有变化时才计算/上传LUT，禁止每次重新扫描体素。硬件专项以实际LUT上传计数反证。

## 四、验收状态与尚未闭环的风险

- **已有证据**：Windows Imaging 专项的 .NET48 编译、FVO/MVI 数值及 WPF 四视图/曲线/切片/文件导出自动验收。
- **本轮新增的源代码和测试**：20 工作区命令、统一错误回报、GPU方向标与可见性开关、窗口命令异常反证。只有新一轮 Windows CI 实际返回成功后，才可标记“本轮编译和自动化通过”。
- **尚无充分证据**：在目标 RTX GPU 上对比旧 WinForms 与新 WPF 的画面像素、每项渲染参数实际画面变化、2GB以上 RAW 高分辨率交互延迟、不同 DPI 下每个设置栏控件真实点击，以及整套 `CxECTImageWinForms/Halcon` 专业功能全迁移。
- **不能通过**：仅有类名/目录，按钮能显示，GitHub 提交成功，或者没有 GPU 的 CI 上以 WPF 空间三切面代替完整 Raymarch。

## 五、持续开发不可退化条件

- **ReferTo/Vision 原始文件只读**；不用 Adapter、不增加项目、不建立第二套体数据/传递函数/状态。
- 从旧源码查出的功能缺口，要么实现并给编译+交互反证，要么记录为明确未验项；**不允许静默删除或宣布功能完成**。
- 每轮按 `旧功能→新函数→UI事件/工作区命令→状态及结果→测试→Windows CI→RTX实机` 留证，功能对照表随重要改动更新。
