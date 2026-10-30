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


### B包来源失效与可视版本管理续进

- `9fecb6b`/`4131ef1`：`MVI3DSceneState.RemoveOlderSourceRevisions`按SourceRef、当前专业Revision一次性撤销旧Surface/Measurement等场景引用，保留最新来源实例，自动清理旧选择并仅发布一次Changed事件。重复无效化保持零变更；不删除MEM真源或物理资产。ArchitectureSmoke增加正反测试。
- 这些仅属于MVI公共Scene消费端的失效控制，实际专业结果的变更由CT3D/MEM既有Owner发起；未来Studio/MPW须在结果提交与重开恢复路径调用此统一方法，而非另建Scene版本栈。
- 固定SHA Windows CI及真实GPU混合深度、像素精确拾取、场景样式MEM保存重开、ROI跨切片变换、LOD/预算仍待验证。不得标B工作包完成。


## 5. B包 2026-10-30 多对象交付增量与验收状态（基于master实码）

**正式生产路径**：继续由FVO持有原值/Frame、FGE持有double几何、MVI持有Scene/选择/分组、MPW持有WPF/GL显示缓存、CT3D持有Feature/Measurement、MEM持有工程事务。没有新建工程树或独立GPU状态系统。

- **Studio用户作业**：`StudioCT3DDocument`已有实际MEM打开、ROI复算、待提交预览、丢弃、Scene与Data原子保存；`3b3e37f7`/`cc13f3c0`修正隐藏Measurement后复算/丢弃不能重新选中不可见对象，真实Studio S1固定SHA成功（例如`cc13f3c0`的Windows Studio S1）。
- **真实PointCloud**：`e8cc4ace`追加MVI PointCloud枚举且保持旧Scene Schema1枚举编号；`aaf15011`/`1c39c183`增加FGE不可变double点云及.NET48工程编译，保留原值索引、确定性显示抽样、物理射线最近点拾取；`dfb688ba`/`ecb73a34`/`7ab7cc11`将原值射线拾取与场景可见实例选择/版本竞争连接，含Headless正反测试。
- **WPF/GL点云**：`ec280345`/`4ff3e91b`/`591ef848`提供每Scene独立点云Visual、材质透明度、MVI显隐/删除和缓存；`82d4653c`/`b4e05bc0`/`9dcd46f2`建立GL点云独立VBO和GPU硬件专用回归。上屏可视与原值点号分离，不冒充亚体素或CAD计量。
- **统一物理显示**：`f2b69ae6`/`f9f9e8fe`将Surface/PointCloud投影对齐原Mesh的`(voxel+0.5)/N`体空间规则，`MPWVolumeDisplayPointCloud`保留原点索引；原投影旧断言造成的固定`f2b69ae6` G2失败已在后续测试修正。WPF测试`8dce1f68`改为从真实原值Frame投影，不再手写虚拟屏幕点。
- **真实GPU反投影/性能**：`7d62a267`/`548ff22d`使System.Numerics投影反算一致，中央与右侧像素方向有CPU解析断言；`ce78c9df`/`1db48b8f`让WPF Mesh/CAD仅来源修订时重建，组/透明度修改零重复解析；`110a97f6`在GL多Mesh替换前检查全局三角预算并先释放旧VBO。
- **可见3D ROI与Mask**：`f24b2bed`/`49b643ac`将二维物理矩形加毫米法向范围映射为12三角面的FGE棱柱，并验证旋转Frame、非法厚度；`1e161d8b`/`ef6c32f6`使MVI Roi/Mask复用WPF/GL真实索引三角面路径，支持对象级透明显隐及释放；`80af8c83`增加真实GPU硬件专项。

**固定成功证据**：
- [CT3D B G1–G3 `40994784012`](https://github.com/oudream/cx-visual-x3/actions/runs/40994784012)：`ce78c9df`，SUCCESS，包含Release/Debug x64、Headless、真实WPF。
- [CT3D B G1–G3 `40994645435`](https://github.com/oudream/cx-visual-x3/actions/runs/40994645435)：`7ab7cc11`，SUCCESS。
- [CT3D B G1–G3 `40994429709`](https://github.com/oudream/cx-visual-x3/actions/runs/40994429709)：`7d62a267`，SUCCESS。
- 本节后续物理ROI/Mask最新SHA仍为queued，不将旧SHA成功外推为全部通过。

**B包剩余硬门禁**：跨Volume/Roi/Mask/Mesh/CAD/PointCloud的真实GL深度及半透明排序（当前深度仅几何叠加，不代表Raymarch可写入正确逐像素深度）、真实GPU工作站新PointCloud/ROI组合G4、Context丢失与显存压力上限的全量反证、正式CAD和多对象独立专业资产在MEM工程中的可靠保存重开、真实用户鼠标操作及大规模稳定性。不得把B标为完整验收。历史第三节描述的是2026-10-27初始阶段，不再代表2026-10-30源码状态。


## 2026-10-30 B包：ROI编辑、Data/Visual一致性及Studio对象身份续进

- 原有`CT3DAnalysisPersistence.Commit`已能将专业Data与Scene Visual单事务写入。此次补全`RecomputeSliceRoiAndCommit`的Visual重绑定合同（`d624990`）：MEM中存在`mvi.scene3d.layout`时，不能只更新新Mesh/Plane ResultId并留下旧Scene来源；必须在同一事务提交合法的MVI Visual Aspect。Headless测试`e8d9ad5`核对旧结果拒绝、更新后来源有效、Undo/Redo后Data与Visual均准确恢复。
- `MVI3DSceneLayoutPersistence.CreateReboundAspect`（`d08a65f`）保留原Scene实例Id、组、透明度、显隐和选择，只映射专业结果的SourceRef，不重复创建MEM或专业资产。
- `CT3DAnalysisPersistence.CommitAll/RecomputeAllAndCommit/RecomputeAndCommit`（`585dc95`）支持可选配套Visual Aspect及批量专业结果的原子MEM事务；存在旧MVI Visual而未重绑定时拒绝提交，避免静默数据/可视分裂；对应双Case同步复算、Undo/Redo测试`ab701da`。
- TestCenter正式窗口`SaveEditedSliceRoiAsync`（`86cec07`）复用上述CT3D/MVI/MEM合同，检查StoreToken、Frame、旧ResultId、当前Case索引与异步场景Epoch；提交后仅当视图仍一致才恢复新结果。工作区命令`ct3d.case.roi.commit`与实际ROI按钮（`ed9729e`/`e1dc25b`）调用同一入口，WPF实测`d480795`/`62c94ad`检查磁盘Data/Visual双Aspect和入口可用性，异常时显式关闭Dirty会话（`cc3346c`）。
- `MVI3DSceneState.RemapSources`与StudioCT3DDocument复用（`43a2d52`/`65c5a90`）：局部专业ROI预览、丢弃和再次计算不清空重建Scene实例，而在同一来源Revision/Frame下原位重绑定专业ResultId；批量先校验再一次Changed。Studio的窗口回归`15119ec`/`1a183f8`核对原Measurement实例Id、选择、显隐在异步分析、放弃、重算、MEM保存重开后保持一致；Headless MVI原子拒绝反证`0dc059b`。
- **门禁声明**：上述增量均已提交至master，但相应CT3D B G1–G3与Studio S1固定SHA Windows工作流在本次检查时仍为queued，严禁沿用历史成功Run替代最新G1/G2/G3。真实GPU多对象渲染/显存/Context恢复的G4及实际工业标准样本G5尚无本轮通过证据。B工作包保持【实施中，未完成最终验收】。
