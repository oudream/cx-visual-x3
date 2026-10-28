# CxVisual 通用三维视觉工作台——V02 原值三角Mesh与拓扑质量闭环 V1.0

> 2026-10-28。唯一权威总体架构：`DOCS/Architecture/CxVisual_专业视觉体系#通用三维视觉工作台——整体架构与UML_V2.0.md`，对应能力11/12/05/08。本文只追踪已提交的源码和证据，不另创架构/项目/专业Owner。

## 一、核心分工与真实代码

| Owner | 源码入口 | 已落地 |
|---|---|---|
| FGE | `Foundation/Geometry/Meshes/FGETriangleMesh3D.cs` | 通用双精度3D点、向量、只读共享索引三角Mesh；输入防御拷贝；面积、包围盒、面积加权顶点法向、边界/非流形/绕序质量、仅闭合时的带符号/绝对体积 |
| CT3D | `Professional/CT3D/Analysis/CT3DVoxelBoundaryMeshExtractor.cs` | 对FVO UInt16/Byte/Float32真实RAW租约阈值分割，按外露六邻域面生成共享角点与外向绕序；保留SourceRef/Revision、Frame、Threshold、触边状态，按三角预算明确拒绝部分Mesh |
| FVO | `FVOOriginalScalarLease`和`FVOVolumeSpatialFrame`（既有） | 原始标量+受保护SafeHandle、Origin/Direction/Spacing和双精度体素角点到毫米坐标；不可由GPU R8反推 |
| MPW | `MPWVolumeSurfaceProjection.ProjectMesh` | 将FGE毫米Mesh以体素角点`(i+0.5)/N-0.5`投影到渲染外框，按最大三角面数拒绝未定义的LOD截断；独立于计量真值 |
| MPW WPF | `MPWVolumeSliceSceneControl.SetSurfaceMesh` | 在既有WPF 3D场景用真实索引`MeshGeometry3D`绘制，Y镜像后修正面片绕序；保留三刀/点云与Camera |
| MPW GL | `MPWVolumeOpenGlControl.SetSurfaceMesh` | 在原WGL framebuffer内独立VAO/VBO绘制`GL_TRIANGLES`，切换Volume与Context时清理资源；继续保留Raymarch和原来的Surface点层 |
| MVI | `MVI3DSceneState`（既有） | 仅维护Mesh对象引用、选择/显隐/Revision，专业网格仍由CT3D/FGE拥有，不创建第二套MEM工程树 |
| TestCenter | `TCTManualVolumeProfessional`、工作台Toolbar/Commands、GPU恢复与UI Smoke | 真实RAW→Mesh→MVI引用→WPF/GL双显示；同步小数据验收入口、异步取消、体切换过期结果拒绝与显隐 |
| 自动化 | `Tests/ArchitectureSmoke/Integration/CT3DVoxelMeshTests.cs`、`MPWVolumeSourceTests.cs`、`TCTVolumeViewerSmoke.cs` | 解析真值、转动/镜像坐标、UIA窗口级显示与异常/撤销案例 |

## 二、数学与质量合同

- 体素中心`(i,j,k)`，边界面角点`(i±0.5,j±0.5,k±0.5)`，使用来源`FVOFrame.VoxelToPhysical`得到双精度世界点；正交Direction的det为负时翻转三角绕序，保持物理外法向。
- 仅阈值内实心体素的6邻域外露面生成三角网格；相邻面共享角点，三角索引不重复维护每面顶点；面法向从外向三角绕序求出，顶点法向为面积加权和。
- `BoundaryEdgeCount`、`NonManifoldEdgeCount`、`InconsistentWindingEdgeCount`、`SignedEnclosedVolume`均保留诊断；仅闭合一致定向时报告体积。触及源Volume边界需单独记录，不可据闭合指标擅自宣称完整工业工件。
- `HasCalibratedClosedGeometry`是**几何条件**，不是精度认证或GD&T判断；仍需材料界面、部分容积、表面偏移、设备标定标准件、可追溯性和不确定度。
- 当前`binary-voxel-face-6-neighbor`由体素面构成的**阶梯表面**，非Marching Cubes、非亚体素面、非自由曲面/NURBS、非CAD B-Rep。面片Lod/简化、法向平滑与真实GPU透明遮挡深度仍待实现与实测。

## 三、验收证据与保留项

1. **已通过证据（固定SHA）**：`f7a9c7714566854814315e3efdee25eefb39d54d` 的 Windows Imaging workflow `40681318710`，Release x64与`--volume`输出`PASS CT3D UInt16 physical voxel mesh, topology, mirrored normals and negative cases`；验证UInt16 2×2×2实心块48三角形、26唯一角点、面积24mm²、体积8mm³，镜像法向/索引异常/预算拒绝/取消/触边等正反例。
2. **后续追加源码**：各向异性2×3×4mm/旋转真值（应为面积208mm²、体积192mm³）、带符号体积、真实WPF索引Mesh及GL VAO/VBO、MVI显隐与TestCenter切换清理代码已提交。**这些后续提交须重新取得对应SHA的Windows CI通过才能升级状态**。
3. 全量TestCenter某些较早版本因Channel UIA `viewer absent`失败，日志指出Connectivity Viewer未找到，不能据此误判三维Mesh数值失败；最新全量结果仍需按各Run区分。
4. NVIDIA/AMD/Intel实体驱动上的三角片透明遮挡、朝向、设备重建、截图、性能和GPU内存回收未做工业现场实测；不把编译成功/源码渲染入口当作GPU像素验收。

## 四、下阶段优先任务

- 真正的亚体素等值面、正确定义的CT表面材料界面与工业误差；同一FGE网格提供拓扑修补、法向重算/简化/LOD，谨慎对待非流形拓扑。
- MPW实现正确的Volume与Mesh深度混合、GPU Shader着色和透明度/选中高亮；GPU硬件验收与资源泄漏压力测试。
- 将专业Mesh与其质量、来源签名、Frame、算法参数注册到既有MEM持久Resource/Artifact，支持重开、复算、结果导出；MVI只引用。
- 在现有FNU数值模块基础上扩展平面/圆柱拟合、CAD配准与实际GD&T，绝不将离散体素面面积或体积自动作为工业尺寸合格判定。
