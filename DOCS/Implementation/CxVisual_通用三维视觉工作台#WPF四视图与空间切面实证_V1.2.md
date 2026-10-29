# CxVisual 通用三维视觉工作台——WPF四视图与空间切面实证 V1.2

> 日期：2026-10-26；基线：`cx-visual-x3/master`。本轮在既有 FVO / MVI / MPW / TCT 的正规 Owner 内继续实现，`ReferTo` **只读**。文中“通过”仅指所列 GitHub Actions 专项 Windows 测试；**三维空间切面不等于 OpenGL Raymarch 体渲染**。

## 1. 本轮真实提交与验收

| 工作包 | Git SHA | 实际交付 | Windows证据 |
|---|---|---|---|
| 可操作四视图 | `3c27decac2c27a3b69c22e2be9f78314fd1746db` | `MPWVolumeSliceSceneControl` 原生 WPF 3D贴图切面，`TCTManualVolumeViewerFrame` XY/YZ/XZ/3D布局、RAW/BMP、三刀、窗映射与相机交互 | 编译/运行已在后续修复后验证 |
| 生命周期纠正 | `27de5afe`、`63c42ad` | 同步 `ITCTWorkspaceFrame.OnWorkspaceClosed`，订阅解绑、FVO Dispose 和切面资源清理；消除并行修改造成的重复方法 | 后续专项工作流通过 |
| 截图工件修复 | `5946830fec396685fc05a4f2008f33dd6e0a5dea` | 修复Artifact名称的非法转义 | [Windows Imaging #40270816787：PASS](https://github.com/oudream/cx-visual-x3/actions/runs/40270816787)，含真WPF窗口和PNG产物 |
| 十字线与交互 | `654d969ef03b3ff43a868a436e8183e5b5736ade` | 三切面六条十字线；Ctrl+点击从图像像素反算三刀；Shift+滚轮更换切片；保持MPW原有缩放/平移 | [Windows Imaging #40271273307：PASS](https://github.com/oudream/cx-visual-x3/actions/runs/40271273307)，增加跨视图位置/关闭资源断言 |

## 2. 实际源码与职责（无新工程/中转层）

- `Foundation/Volume`：`FVOVolumeData`、`FVORawVolumeLoader`、`FVOSliceImageBuilder` 仍是唯一体数据/切片数值来源。大RAW保留原始值路径和降采样预览路径。
- `Mechanism/Visual/Volume/MVIVolumeViewState.cs`：唯一 X/Y/Z 索引与视口状态；无独立 TestCenter 状态副本。
- `Mechanism/Presentation.Wpf/Volume/Views/MPWVolumeSliceSceneControl.cs`：`Viewport3D` 三张真实正交切片的几何/纹理；依据原始尺寸与物理 Spacing 计算空间比例；支持旋转、缩放、重置。只接收 FIM 图像和数值，不依赖 FVO 或 CT3D 项目。
- `Tests/TestCenter/Manual/Volume/TCTManualVolumeViewerFrame.cs`：原生 WPF 2×2 组合、文件操作、3轴控制及显示设置；正式 `manual.ct3d` 工作区。
- `Tests/TestCenter/Manual/Volume/TCTManualVolumeCrosshairs.cs`：三张 MPW 图片上十字线绘制、坐标点击及滚轮交互，用既有 FVO ImageToVoxel 映射，不自建数学转换/包装库。
- `Tests/TestCenter/Manual/Volume/TCTVolumeViewerSmoke.cs`：实际创建 WPF Window、装载合成体、真实3张FIM切片和3张3D空间贴图、移动/复位、十字线定位、关闭清理、PNG截图、BMP空目录反例；使用 GitHub Windows runner，不是假截图。

**运行方式**：
```powershell
# 在 Windows x64 .NET Framework 4.8 构建后
.\Output\Release\x64\CxVisual.TestCenter.exe --volume-viewer-smoke
# 正常启动 TestCenter 后进入「专有测试（人工操作）→ CT/3D测试」
```
对应 `.github/workflows/imaging-foundation-windows.yml` 已自动运行，截图工件位于上面的 Windows CI 页。自测试体是合成样本，不能当作工业 CT 图像的实测渲染结果。

## 3. 仍未完成及下一关键包

1. **最核心的旧 OpenGL GPU 体绘未接入**：`RenderingOpenGlProgram`、`RenderingVolumeControl`、`RenderingCamera` 为旧源码原样拷贝，未接入正式 GPU 资源创建和真实着色器驱动编译/执行。现有 `MPWVolumeShaderSources` 仅是已编译入 C# 程序集的 GLSL 字符串，不能作为 GPU 绘制通过证据。
2. 原旧版颜色、阈值、标量/梯度透明度、材质光照、任意切面**完整可视编辑器**尚未在 WPF 四视图工作台恢复；FVO/CT3D 数值 LUT 通过不代表 UI/GPU 效果通过。
3. 本轮 BMP 文件夹能力尚需真实正例回归、大体数据和取消/性能验收；RAW 整套实际机台采图、HALCON 许可、显存与多 PC 仍待独立验收。
4. 旧 `MainForm.Designer.cs`、`FourViewLayoutControl` 等参考代码仍保留未编译版本用于外观/交互对照，新WPF只恢复四视图主体和部分操作，不应宣称旧版全功能迁移完毕。
5. TestCenter 总体验收中的其它 UIA 失败不因 Imaging 专项 PASS 而自动消除，必须看各自 CI。

**下一可执行工作包**：先核实真实可部署的 OpenTK/OpenGL 版本与原生 WPF OpenGL 上下文实现方案，保证不引入 WinForms GLControl 或多层 Adapter/Wrapper；将既有 `RenderingVolumeControl` 的体纹理、Raymarch、shader/uniform、透明度、光照、资源释放迁为一套正式 MPW/CT3D 可运行流水线。以真实 Windows GPU 上下文、旧新同 RAW 对照截图、帧时/显存/重复加载/释放/异常反证为验收口径。


## 4. B工作包：多对象MVI组织/结果定位增量（2026-10-29）

- `MVI3DSceneState`沿用既有不可变`MVI3DSceneItem`，新增可视`LayerKey/GroupKey`、`SetOrganization`、`SnapshotLayer`、`SetGroupVisibility`、`SetGroupOpacity`与`TrySelectSource`；变更只影响会话可视引用，不复制RAW、CAD/Mesh实体、不另建MEM资源存储。空图层、不合法透明度拒绝；整组批量修改只发一次Revision，隐藏已选成员时清理Selection；同SourceRef多个可见Scene实例拒绝无歧义自动选择。
- 对应源提交：`e1da3e4`（多对象图层分组）、`8ffdf95`（Headless组合反证）、`6cc8f14`/`49390bd`（真实WPF Surface与Measurement整组显隐）、`2ea1552`/`a9639a3`（整组透明度及非法值验证）、`acd8498`/`ab23524`（专业来源反向选择与歧义拒绝）。
- **证据边界**：上述提交均已推送GitHub，最新固定SHA Windows CI在记录时仍处于queued/in_progress；未能宣布G1/G3通过。仍待统一组层次的WPF可视编辑入口、MEM工程场景样式保存重开、真正Mesh/Volume混合透明深度与对象级GPU拾取、资源预算/Context恢复实证、三维ROI四视图精准交互。因此B**未完成**，不得将MVI状态单测当作完整工业三维工作台。
