# Rendering 模块结构

体渲染代码分为两层，业务页面只依赖公开的渲染设置与控件，不直接操作 OpenGL。

## CxECTVolumeCore.Rendering

- `Configuration/RenderingSettings.cs`：显示开关、材质与采样配置。
- `TransferFunctions/RenderingTransferFunctions.cs`：传递函数模型与一维纹理烘焙。
- `TransferFunctions/RenderingPresets.cs`：基础灰阶预设。
- `Diagnostics/RenderingHistogram.cs`：编辑器使用的抽样直方图。
- `Diagnostics/RenderingIntensityProfile.cs`：按背景灰度上限和高低分位参数确定默认灰阶显示范围。

## CxECTVolumeWinForms.Rendering

- `Controls/RenderingVolumeControl.cs`：编排绘制通道与 GPU 资源，不保存 Shader 源码。
- `Controls/RenderingTransferFunctionEditor.cs`：传递函数编辑 UI。
- `Controls/RenderingOrientationIndicatorControl.cs`：方向轴叠加 UI。
- `Camera/RenderingCamera.cs`：轨迹球、缩放和物理尺寸自适应。
- `OpenGl/RenderingOpenGlProgram.cs`：Shader 编译、链接与 uniform 写入。
- `OpenGl/RenderingShaderSources.cs`：GPU 算法源码。
- `OpenGl/RenderingGeometry.cs`：静态包围盒几何。

## 第一阶段渲染链

`VolumeData` 上传为单通道 3D 纹理；默认预设先根据可调的背景灰度上限、低灰分位和高灰分位展开低对比度区间，再把颜色与透明度在 CPU 侧烘焙为一维纹理。默认值为背景 `0/255`、低分位 `10%`、高分位 `99.5%`。Slicer 预设自带一条基础不透明度曲线，其倍率由 `RenderingIntensitySettings.PresetOpacityScale` 控制；启用自定义“标量透明度”后由用户曲线完全接管，不再叠加预设倍率。`RenderingSettings.VolumeOpacity` 是两种路径共同使用的最终整体不透明度。自定义颜色、标量透明度和阈值仍使用原始归一化标量，不受自动显示范围影响。体渲染 Shader 根据实际纹理尺寸计算射线步长，执行采样距离透明度修正和屏幕空间抖动，再用可配置材质完成梯度光照。渐进式预览替换高清纹理时重新计算显示范围但保持相机状态。

后续新增渲染功能时，应优先放入对应职责目录；只有需要协调多个职责时才修改 `RenderingVolumeControl`。
