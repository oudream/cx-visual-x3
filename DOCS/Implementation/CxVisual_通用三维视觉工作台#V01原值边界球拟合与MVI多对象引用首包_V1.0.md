# CxVisual 通用三维视觉工作台——V01 原值边界球拟合与MVI多对象引用首包 V1.0

> 2026-10-28。架构依据：DOCS/Architecture/《通用三维视觉工作台——整体架构与UML V2.0》R4。此文是实现证据，不是第二份总体架构；提交的真实 Windows CI/工业设备结果以 Actions 的固定SHA为准，不能预先宣称已通过。

## 一、实际源码与职责

| Owner | 正式类/文件 | 已实现行为 |
|---|---|---|
| FVO | `FVOOriginalScalarLease` + `FVOVolumeSpatialFrame` | 原始Byte/UInt16/Float32只读租约、物理mm体素索引转换与标定标识，禁止从R8预览反推 |
| CT3D | `Professional/CT3D/Analysis/CT3DSurfaceSphereAnalysis.cs` | Headless六邻域阈值边界采样、取消、最大点预算、边界截断标记；调用已有FNU SVD完成代数球拟合 |
| Numerics | `Foundation/Numerics/LinearAlgebra/FNULinearAlgebra.cs`（原有） | 复用SVD满秩最小二乘与条件数上限，CT3D工程直接项目引用；没有新数值求解器 |
| MVI | `Mechanism/Visual/Volume/MVI3DSceneState.cs` | Session场景只存Volume/Surface/Mesh/CAD/Mask/Roi/Measurement/Defect引用、源Revision、显隐、透明度、选择、不可变Snapshot |
| MVI ViewSession | `MVIVolumeViewState.Scene3D` | 将场景引用与既有相机/三刀共享同一会话，不新建第二MEM持久工程树 |
| TestCenter | `TCTManualVolumeViewerFrame.LoadVolume` | 加载时以sourceFilePath或临时session身份登记Volume可视引用，不持有第二份Volume |
| 测试 | `CT3DVolumeLoadTests`、`MVIVolumeViewTests`、`TCTVolumeViewerSmoke` | 合成标准球→原值边界→物理mm→SVD拟合→MVI Surface引用，异常/撤销选择/不合法参数/不可变快照反例 |

## 二、第一条专业计算合同

1. 输入：由FVO实际持有的原始RAW；CT3D调用者提供`SourceRef`和`SourceRevision`，以及明确的阈值、采样步长、最大点数、取消令牌。
2. `SampleBoundary`：在原始索引采样，每个阈值内点检查六个轴向邻点；按FVO Frame输出双精度物理点，带SourceRef/Revision、边界截断标识和采样方法标识。不是Mesh、亚体素表面或完整工业表面确定。
3. `FitSphere`：必须空间已明确标定、不触碰体边界、有至少6个点；居中构建设计矩阵，由FNU已有SVD求最小二乘，返回球心、半径、直径、RMS误差、最大残差、条件数、采样数和临时ResultId。
4. MVI只将`ct3d:result:<ResultId>`作为会话Surface引用；不复制CT3D专业结果或FVO原始体素。ResultId尚未接入MEM资源注册/工程保存，不能宣称可重放持久结果。
5. 非法阈值、原值缺失、未标定空间、超过点预算、仅部分物体、几何退化、已取消任务必须失败或显式标记；未经现场标准件对照不能自动转为合格尺寸/GD&T判定。

## 三、局限和下一步

- 离散体素中心的阈值边界存在分辨率/部分容积系统误差；没有亚体素等值面、法向、网格拓扑、鲁棒拟合或不确定度建模。球拟合的代数解与几何最优解不同；绝不能声明“已实现工业精密球测量”。
- 当前只实现球体Feature初步拟合，不代表平面/圆柱/自由曲面、ICP、CAD/STEP、形位公差、缺陷/壁厚或报告已完成。
- 目前MVI Scene是可视引用/选择状态，尚未将Surface点云上传MPW绘制，也未接入MEM提交/Undo持久保存；下一步需要真实多对象GPU呈现/结果定位/工程化。
- Windows .NET Framework 4.8 Release x64、ArchitectureSmoke `--volume`、真实WPF/GPU、长时间RAW负载及工业真值检查应逐步升级证据；不可将源码存在或历史CI通过替代本提交验收。
- 仓库中还存在并行Transport/模块工程编译阻断；具体失败归属应对照各Workflow日志判断，不以此误称CT3D测试失败或通过。
