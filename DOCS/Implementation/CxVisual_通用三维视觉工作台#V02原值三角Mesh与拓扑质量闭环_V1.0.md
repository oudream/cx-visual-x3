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

## 五、质量门禁加固（2026-10-28 增量）

- FGE`FGETriangleMesh3D`新增`NonManifoldVertexCount`：逐顶点构造相邻三角面link图，检测两片封闭面壳仅在一个顶点相触的bow-tie拓扑；仅边计数正常不能视为流形。闭合判断现在同时检查边界边、非流形边、bow-tie顶点与不一致绕序。
- 反例`CT3DVoxelMeshTests`使用**两只共享一个顶点的闭合四面体**，即使各边被使用两次、方向一致，也必须给出一个非流形顶点并拒绝封闭体积；普通开放三角片不得误报。
- CT3D的`HasCalibratedClosedGeometry`增加正有向体积条件；这仍只是来源标定/拓扑条件，不代表工业计量已签收。
- CT3D Mesh与离散球拟合调用共享`CT3DRawScalarGuard.ReadFinite`拒绝Float32 RAW中的NaN/Infinity，防止浮点非有限值因比较行为被误判为实心材料。ArchitectureSmoke补充Float32 NaN反证；这些改动必须按**对应新SHA的Windows --volume**回归判定，不借用此前UInt16测试通过。
- 所有新增能力继续使用FGE/CT3D既定Owner、现有工程与Windows自动化入口。尚未完成三角Mesh多连通分量拓扑修补、完整非流形自交检测、亚体素面、真实GPU像素/深度遮挡和工业标准件误差评定。

## 六、固定SHA专项验证升级（2026-10-28）

- 固定提交`e77cae8d9`（祖先包含FGE bow-tie顶点诊断及双四面体反例）对应的Windows Imaging Run `40687637635`已完成 **SUCCESS**，其`Build all existing projects on Windows x64`与`Execute FIM MVI standalone and default smoke`均为SUCCESS，`--volume`日志再次输出`PASS CT3D UInt16 physical voxel mesh, topology, mirrored normals and negative cases`；故这批**共享顶点、开放边、两个封闭壳单顶点接触、旋转/镜像和正有向体积**的测试可以标为`自动化通过（固定SHA）`。
- Float32非有限值的输入保护与测试在更新的`6ac270c3`、`baf51b6c`、`084bc6ad`提交中落地，**不属于上述e77已通过的源码范围**。等待对应后续Windows全量/专项`--volume`完成后才提升证据等级。
- UIA的Channel Viewer缺失和并行二维Imaging测试编译问题，应按对应日志归属，不能把它们写成CT3D Mesh数值失败或通过；真正GPU面片颜色/深度和工业标准件依然待实机。

## 七、原值非有限值验证与多面壳升级（2026-10-28）

- **Float32非有限值专项已通过**：`4d6c558cddf78e8c9af367e27aca731b03bf59aa`（含`084bc6ad` Float32 NaN负例）对应Windows Imaging `40687957944`为`SUCCESS`；其.NET Framework 4.8 Release x64编译、`--volume`及默认Smoke均通过，包含CT3D三角Mesh专项输出。它证明代码防御与合成负例通过，**不等于真实CT Float32工业体标定/测量通过**。
- **多对象拓扑判定新增源码、待验收**：FGE`ConnectedFaceComponentCount`利用三角面共享边构造连通分量，将仅在同一顶点接触的两个闭合面壳判定为两个面壳；CT3D`HasCalibratedClosedGeometry`现在要求**单一面壳**、无边/顶点非流形、外向有向体积和已标定不截断。新增两个互不相邻体素构成两独立闭壳的合成用例。此阶段单个来源含多工件时须按产品/材料实例分离后再报告单工件量值。
- TestCenter的Mesh状态提示现包含三角面数、面积及独立面壳数；`Mesh`显示仍由MVI Scene引用，UI不成为Mesh真源；本轮多对象测试的Windows结果以新提交工作流为准，不能引用`4d6c558c`历史PASS代替验收。

## 八、V03 局部Mesh平面拟合增量（2026-10-28）

> **范围**：V2.0 R4能力13；是从已完成FVO原值→FGE Mesh进展到CT3D几何特征分析，不改变MEM/MVI/MPW/FNU Owner；**尚非GD&T、基准建立、亚体素或工业精度认证**。

| Owner | 正式源码/API | 已实现 |
|---|---|---|
| FNU | `Foundation/Numerics/LinearAlgebra/FNULinearAlgebra.cs`：`FitPlaneBySvd` / `FNUPcaPlaneResult` | 复用现有Math.NET SVD，对Nx3点去质心并计算正交最小二乘平面；支持任意姿态，拒绝共线/零尺度/非有限数据与超条件数；输出质心、单位法向、RMS、最大正交距离、法向/切向奇异值比 |
| CT3D | `Professional/CT3D/Analysis/CT3DMeshPlaneAnalysis.cs`：`FitSelectedVertices` | 从来源标定、未截断、单连通正向闭合Mesh的**局部顶点ROI**获取点，不把整个工件误称为一个平面；拒绝重复索引、越界、退化和超过平面度比例；结果保留SourceRef、SourceRevision、SourceMeshResultId、结果身份及毫米残差 |
| TestCenter/MVI | `TCTManualVolumeProfessional.AnalyzeCurrentMeshPlane`、`AnalyzeCurrentMeshPlaneTopZ` | 真正调用CT3D，登记MVI Measurement引用并显示误差。+Z端面为明确标识的快速示例，依据原始Frame选择网格端面，**不是通用三维ROI拾取**；重新提取Mesh或更换Volume时自动撤销依赖旧Mesh的平面结果 |
| Headless/WPF | `CT3DVoxelMeshTests`、`TCTVolumeViewerSmoke` | 真实UInt16立方体顶端9点、法向/Z坐标和RMS、任意倾斜平面、共线、重复ROI、全立方体非平面拒绝、WPF真实窗口结果关联与Mesh更新失效 |

**点到面偏差补充**：`CT3DPlaneFitResult.SignedDistanceMm(pointMm, pointFrame)` 与 `AbsoluteDistanceMm`现在提供同一`FVOVolumeSpatialFrame`内的点到局部平面距离（毫米），通过引用身份拒绝未经配准的跨Frame直接量测；有符号方向由PCA法向的规范化约定决定，**不等同CAD内外侧正负偏差或GD&T平面度**。Headless增补±1mm正负例、绝对距离及跨Frame拒绝；源码`24497fe2`及`3a27e161`的Windows CI待按对应SHA核实。

**核心合同：** 平面拟合为最小正交距离（PCA/SVD，理论上平面点阵秩2），不是 `z=ax+by`，不依赖特定坐标轴或在FGE中内建毫米。CT3D把来源Frame与mm语义加在专业结果上；法向是**无向几何法向**，其符号按最大绝对分量为正进行确定性规范化，非自动外法线。平面质量只包含样本RMS/最大残差、条件数和法向/次切向奇异值比，真实设备不确定度、抽样策略、离群值与基准特征仍待专业验证。

**证据划分**：FGE Mesh原有固定SHA Windows通过记录仍有效；本轮平面新源码提交`62e2f3e8`（FNU）、`77421a34`（CT3D）、`f5d228f6`（初版Headless测试）及后续WPF与回归修复`e2ae3798`，**必须以这些源码后续实际Windows Build/--volume/窗口级Job结果升级验收，不借用旧SHA成功结论**。曾在早期中间提交出现CS0819/CS0136，已在`e2ae3798`修复；真实GPU、CAD、GD&T仍未验。
