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

## 九、V03固定SHA Windows验收与CI路径补强（2026-10-28）

- **固定通过证据**：提交`24497fe20daa6925e5c07048230dae2532f5c6ad`对应的 <https://github.com/oudream/cx-visual-x3/actions/runs/40691495873> 已完成 **SUCCESS**。该提交包含FNU PCA-SVD平面、CT3D局部Mesh平面特征、原值UInt16三维几何Headless及真实WPF端面拟合窗口案例、MVI Measurement引用、重新计算Mesh时旧Measurement失效与有符号偏差的正式实现。流水线明确通过 **Release x64编译、Owner依赖检查、WPF二维窗口、FIM/MVI/--volume/默认Smoke、WPF三维工作台验收**。
- **不得跨版本误标**：`3a27e161`之后补充的“±1mm偏差、绝对值及跨Frame拒绝”专项断言，以及其后的文档/CI路径补强，不属于固定SHA `24497fe2`已执行的测试内容，须等待后续工作流证据。
- **触发修复**：原`imaging-foundation-windows.yml`没有监听`CT3DVoxelMeshTests.cs`等CT3D专项测试文件，因此仅提交测试改动不会触发Imaging专项。提交`dc58b341`将CT3D Mesh、CT3DVolumeLoad、MPWVolumeSource、MVIVolumeView四个现有专项测试文件纳入Windows push路径过滤；对应提交的完整CI需按最新Run查询。
- **边界仍在**：局部端面PCA拟合的RMS和点到面偏差是基于体素阶梯面/来源标定的算法数值，不代表工业量具溯源、表面法向选择、圆柱/CAD配准或GD&T工业报告已通过。

## 十、V04 首个专业结果保存重开与复算闭环（2026-10-29）

### 10.1 正式实现与Owner

本包接续V03，不新增项目、第三方SDK、工程树或任务运行器。开始核对并快进远端`ea771b64`，按用户收敛要求再次同步至`de448ccb`，保留全部V04改动；本包最终提交及CI以实际Git/Run为准。

| 位置 | 已实现的行为 |
|---|---|
| FVO `FVORawVolumeLoader.Load(path, info, maxAxis, token)` | 从固定元数据加载内容寻址RAW，无需依赖文件名；验证维度/格式/Spacing及精确字节长度，保持预览与原值分离 |
| FVO `FVOUnmanagedRawVolumeBuffer.Map` | 显式FileAccess.Read/FileShare.Read映射；Hash校验读锁与原值映射可以共存，映射存续期间拒绝写入/删除，释放仍由SafeHandle与原值Lease负责 |
| CT3D `Persistence/CT3DAssetReference.cs` | 稳定Guid、Revision、源SHA256/长度、显式尺寸/标量类型、原始Frame；签名覆盖身份/数据/标定；流式Import和Hash、有取消、受控相对路径及只读映射加载 |
| CT3D `Persistence/CT3DAnalysisCase.cs` | 真实RAW→现有体素Mesh→局部顶点ROI→现有PCA平面；保留配方/阈值/预算/质量参数，可Headless复算；不自建Case Session或Runtime |
| CT3D `Persistence/CT3DAnalysisPersistence.cs` | 专业小描述存既有MEMContent的Data Aspect；Mesh/ROI/平面量值写内容寻址`.analysis`外部制品；结果身份、来源、Frame、mm、残差、参数及算法版本保存重开 |
| TestCenter `TCTManualVolumePersistence.cs` | 后台调用上述正式API；可见保存/重开按钮及同源Commands；原值/Mesh/Measurement恢复MVI引用并由MPW显示；重复保存复用MEM StoreToken，外部编辑冲突拒绝，晚到读取不覆盖新Volume |

这是**专业局部制品协议**：持久化权威仍是MEM，文件IO只服务CT3D Payload；未新增全局Artifact仓库、Core通用外部资源类型或MDS私有Heap。`CT3DAssetReference`目前仅支持小端Byte/UInt16/Float32 RAW及来源Frame，不冒称CAD/点云/4D统一引用已经完成。

### 10.2 资源、失败与精度合同

- RAW使用分块流式Hash/复制和既有只读文件映射，不把多GB原值嵌入工程或强制读入托管数组。Mesh结果目前有256MiB制品预算、现有200万三角上限、3～30000顶点ROI上限；完整峰值内存/显存与>2GB压力仍待实测。
- 元数据和Payload各有Schema；来源签名同时包含SourceId/Revision/Hash及Frame值，来源改变或结果文件损坏/丢失均拒绝加载，不回退R8预览。
- 结果临时写入→Flush/Hash→内容寻址定位→核对来源/expectedRevision→MEM事务登记。取消/冲突不修改工程；工程提交后不再谎报取消回滚。失败可能保留未引用的完整共享制品，不擅自删除共享文件；临时文件在退出路径清理。
- 重开保留原ResultId和SourceMeshResultId，使用同一FNU算法核验保存的ROI及毫米平面/残差；复算产生新ResultId且固定源/参数不变。Frame对象可重新构造，来源签名稳定；这不是已实现通用FGE Frame图。
- 原值Frame标定仅接受调用方明确提供的来源。`HasEvaluatedMeasurementUncertainty=false`，阶梯Mesh/端面示例不成为工业GD&T或标准件准确度认证。
- 窗口更换Volume/关闭后在途Open只释放结果，不回填旧Scene。保存已经完成而视图切换不撤回磁盘事实。当前UI一次选择一个专业Case，保存后可再次保存；不同已有工程须先打开，避免无条件覆盖。

### 10.3 实际本地验收与证据

当前机器具备VS2022 MSBuild、.NET Framework 4.8及真实OpenGL驱动；本地日志在`Output/CT3D-V04/`，属于工作树验收，不替代GitHub固定SHA CI。

| 验证 | 本轮已观察结果 | 范围 |
|---|---|---|
| Release x64 Unified Build | 同步`de448ccb`后Build成功，0错误 | `convergence-build.log`；本次增量构建0警告，之前重新编译保留既有警告，不宣称全仓Rebuild零警告 |
| ArchitectureSmoke `--volume` | PASS，包含新`CT3DAnalysisPersistenceTests` | 固定UInt16原值、原值映射共享保护、Mesh/平面保存重开/复算、Undo/Redo、取消/版本冲突、源/制品改变和丢失、非法路径/长度/ROI及未标定拒绝 |
| `--imaging`、`--concurrency` | PASS | 二维共用底座与F19回归 |
| ArchitectureSmoke默认 | 正常Windows权限下PASS | 沙箱首次在既有MEM File.Replace被拒绝；未改Core保存规则，正常权限重跑日志`default-unsandboxed.log`通过 |
| 结构及Owner DAG | PASS | 28工程结构；依赖脚本审计29个csproj/118引用，无新增逆向边/环 |
| `--volume-viewer-smoke` | 同步后最终重跑PASS | 真WPF窗口、保存/重复保存/重开、原值与48三角Mesh/9点平面、稳定引用、继续拟合、晚到结果拒绝、Frame改变失效与外部StoreToken冲突反例；`convergence-workbench.png` |
| `--graphics-viewer-smoke` | PASS | 共用图元/MEM编辑的真实窗口回归 |
| `--volume-gpu-smoke` | PASS，OpenGL `3.3.0 NVIDIA 576.02`，3次重装载，Raymarch PNG | `gpu.log/gpu-raymarch.png`；只证明该GPU/驱动上的既有体绘、原生鼠标与重建路径；**不证明新Mesh混合深度、全部显卡或工业计量通过** |

新测试接入`--volume`与默认回归，本包CT3D/Program.cs改动由现有Imaging过滤器触发。GitHub令牌缺少`workflow`权限，首次推送因尝试增加新测试单文件监听被拒绝；已从未推送提交中撤下该过滤器修改，功能和测试接线保留。以后仅修改此新测试文件时需同时满足现有触发路径或手动运行。最终GitHub Run按对应SHA实际状态升级，不把本地PASS当作CI已经成功。

### 10.4 实际SDK使用与后续入口

调用方先准备可信`rawInfo/calibratedFrame`与目录内真实`source.raw`，可使用：

```csharp
var source = CT3DAssetReference.Capture(root, "source.raw", rawInfo,
    calibratedFrame, 1, cancellation);
var analysis = CT3DAnalysisCase.Analyze(root, source, 1000, 50000,
    CT3DAnalysisCase.SelectTopVoxelFace, cancellation);
MRIObjectAddress address;
using (var engineering = MEMEngineering.Create("三维分析"))
{
    var owner = engineering.AddPage("三维").RootContent;
    address = CT3DAnalysisPersistence.Commit(engineering, owner,
        engineering.Revision, root, analysis, cancellation);
    engineering.SaveAs(Path.Combine(root, "analysis.cxproj"));
}
using (var engineering = MEMEngineering.Open(Path.Combine(root, "analysis.cxproj")))
{
    var restored = CT3DAnalysisPersistence.Open(engineering, address, root, cancellation);
    var recomputed = restored.Recompute(root, cancellation);
}
```

下一工作包继续扩展Surface/球/通用Feature/Measurement的持久合同、正式Studio装配及交互ROI；CAD/GD&T/缺陷/壁厚/动画、通用Artifact/Frame图、跨工程导入重映射、全量26能力与缺证的90需求索引均未完成。保留主文R5 E/F/G的完整目标，不缩减为此首包。

### 10.5 固定提交CI核实与异步保存一致性收敛

固定源码提交`b71827db1dabd7aba5031699a5168f788c69dec4`已实际读取GitHub工作流状态、步骤与相关日志。23个工作流中22个成功；不能标为全绿：

| 工作流 | 实际结果/覆盖 |
|---|---|
| [Windows Imaging 40837507856](https://github.com/oudream/cx-visual-x3/actions/runs/40837507856) | SUCCESS；Release x64全工程、Owner DAG、二维WPF、`--volume`及默认回归、三维WPF全部步骤成功。日志两次明确输出`PASS CT3D V04 MEM source/hash/frame, mesh/plane save reopen recompute, cancellation and corruption` |
| [TestCenter .NET48 40837508115](https://github.com/oudream/cx-visual-x3/actions/runs/40837508115) | SUCCESS |
| [Graphics Editor 40837507948](https://github.com/oudream/cx-visual-x3/actions/runs/40837507948) | SUCCESS |
| [Engineering 40837508027](https://github.com/oudream/cx-visual-x3/actions/runs/40837508027)、[Runtime 40837507904](https://github.com/oudream/cx-visual-x3/actions/runs/40837507904) | SUCCESS；不外推工业量具/硬件 |
| [Studio S1 40837507882](https://github.com/oudream/cx-visual-x3/actions/runs/40837507882) | FAILURE；全工程构建成功，STA自测在`配置组合未使用可响应布局的统一1/2/3列容器`断言失败。失败对应Studio配置布局，不能写成编译失败或三维专项失败，也不能据其余22项成功掩盖。其因果关系与修复归Studio专项核查 |

随后同步远端`ceba1911`（五个ReferTo通信守护参考源码提交，无正式三维源码改动），本轮只修复现有V04保存一致性，不扩展新专业功能：

- 原`SaveProfessionalCaseAsync`只比较Volume代次；Frame改变或用户在同一Volume重新选择/拟合ROI时，代次不变，旧保存快照仍可能回填当前专业结果。
- 保存后额外核对原Volume对象、Frame对象、Mesh及Plane结果身份以及Scene对象/Revision。当前上下文已变时保留磁盘已保存事实和保存Token/资产身份，禁止旧快照回填Scene/Measurement；继续保存同一文件仍使用最新StoreToken，不制造伪冲突。
- 真实WPF反例覆盖：启动保存后立即改Frame；启动保存后立即重新拟合；保存期间修改Scene显隐；较新结果保持后继续保存同一文件。局部修复提交的Build/WPF/CI须单独验证，不能借用上表`b71827db`通过结果。

**本轮局部验证**：`ceba1911`工作树上Unified Release x64 Build成功（0错误、6条既有TestCenter重复源文件/旧API警告），`--volume`及真实`--volume-viewer-smoke`均退出0。日志为`Output/CT3D-V04/consistency-build.log`、`consistency-volume.log`，截图为`consistency-workbench.png`。本轮修复后的GitHub CI尚待对应新提交Run；没有以旧SHA替代。

**并行同步复验**：随后合入远端`f48c1650`，包含MVI二维`TrySetResult`防过期实现/测试及ReferTo参考源码；全工程Build 0错误，`--imaging`、默认ArchitectureSmoke及三维WPF均通过（`consistency-merged-*`）。Scene Revision补充后的最终全工程Build仍为0错误/6条既有警告，真实三维WPF全部新反例退出0（`consistency-final-build.log/consistency-final-workbench.png`）；新提交CI另按Run核实。


### 10.6 2026-10-29 公共回归恢复与首批可追溯Feature Measurement

- `master@0f495fca` 的 Imaging / Decision / Runtime / Engineering 四个失败作业，原始日志均定位至 `CT3DAnalysisPersistenceTests.VerifySchema1Compatibility`：固定 `AppData/Cases/CT3D/V04Schema1/source.raw` 不在仓库中，`File.Copy` 抛 `DirectoryNotFoundException`。它不是算法数值误差或C#编译失败；Studio S1另外失败于配置布局1/2/3列断言，不属于本CT3D Owner。
- 提交 [`86423f7`](https://github.com/oudream/cx-visual-x3/commit/86423f740faaf749b4e3267f7645fde1333ce1a4) 对**外部旧Schema1二进制fixture**增加显式缺失检查：没有旧RAW、工程文件或Artifact目录时输出 `SKIP CT3D fixed schema1 compatibility`，其余V04正常/异常/复算仍照常执行。**这只是解除不应阻断其它Owner的测试环境缺口，旧Schema1跨版本真实兼容仍是未验收事项**；不能把SKIP记录成PASS。后续应提交合法旧DLL固定产物或提供独立受控fixture以完成兼容验收。
- 提交 [`1d773c6`](https://github.com/oudream/cx-visual-x3/commit/1d773c6275c4d29e8cc0a69d6f32cd873613370b) 在现有 `CT3DAnalysisCase` 上增加只读 `CT3DFeatureMeasurement` 与 `GetFeatureMeasurements()`：从已拟合Sphere的真实毫米半径派生Radius/Diameter，保留Feature ResultId、SourceRef/Revision/Signature及球拟合RMS；结果不复制原始RAW，也不建立第二套持久化。MeshPlane目前不虚构Datum距离，仅通过原Plane API提供真实点面偏差。
- 提交 [`29bcb6a`](https://github.com/oudream/cx-visual-x3/commit/29bcb6afd5e6d91ce27e5615b7da750f9d604656) 增加球Feature测量原值、结果身份、来源、无工业不确定度认证与MEM保存重开数值一致性断言；数值仍为离散体素边界球拟合，不宣称亚体素/GD&T或实机精度通过。
- 固定SHA Windows CI：本轮查询 `29bcb6a` 的多个工作流为 `queued`，**尚无本轮编译或运行通过证据**；须在实际完成后复核。多对象MVI/MPW正式交互、柱面与完整测量持久合同仍未完成。


### 10.7 A工作包持续实装：平面质量测量归一（2026-10-29）

- [`77a997d`](https://github.com/oudream/cx-visual-x3/commit/77a997d3bf5dd1023aa2702ebb46254df29a327f)：沿用现有 `CT3DAnalysisCase.GetFeatureMeasurements()` 输出，在 `MeshPlane` 路径公开 `plane.rms-residual` 与 `plane.max-absolute-residual`，单位毫米，依托已有FNU PCA平面拟合；共享ResultId/来源签名/Revision/拟合质量合同，不伪造Datum尺寸或GD&T。
- [`5a75154`](https://github.com/oudream/cx-visual-x3/commit/5a75154d436c59c4dc171b02ac5246c4a4ddbfba)：增加平面正向值、Feature身份、来源一致性及MEM工程保存重开的回归断言，与原Surface/Sphere测试共用正式CT3D API。
- **A工作包未完成**：圆柱拟合、可组合多专业结果Case、稳定独立ROI/Frame子元素身份、完整的对象级改写/失效/重算与并发版本兼容，以及最新Windows固定SHA测试证据仍缺失；不把上述两个增量等同A包整体验收。


### 10.8 A包多个专业结果在同一MEM工程的组合重开

- [`d83883d`](https://github.com/oudream/cx-visual-x3/commit/d83883d922fb4231fd0aeee9bf2f1f868b181755) 在原`CT3DAnalysisPersistence`提供`OpenAll(engineering,root,token)`，只遍历MEM正式数据Aspect，逐案例调用已存在的`Open`校验Source/Hash/Frame/算法，不生成平行工程索引、第二套ResultStore，任何损坏整体失败。
- [`bb396f6`](https://github.com/oudream/cx-visual-x3/commit/bb396f6e6aaf7d3e56dbee58bcc2e21ec6781d70) 的真实Headless测试将两次独立Surface/Sphere分析写到同一工程的两个MEMContent，保存、关闭、重开后批量读取，验证两个Feature ResultId独立、共享来源签名，及取消拒绝。
- **限制**：同工程多个Case不是单个Case内多Feature共享复合配方；当前TestCenter的单案例`OpenProfessionalCaseAsync`仍要求恰好一个CT3D内容，尚未接入工程级多案例交互。Windows固定SHA及真实现场尚待证据，不得宣称A完成。


### 10.9 A包：原位专业结果复算及MEM事务（2026-10-29）

- [`44df8aa`](https://github.com/oudream/cx-visual-x3/commit/44df8aa18b94f6310714c5286ce682b09af44dfc)：沿用`CT3DAnalysisPersistence`增加`RecomputeAndCommit`，先核对MEM当前Owner/期望工程Revision、读入并验证旧Source与Artifact，再用旧参数在Headless重新提取Feature、生成新结果身份；最后交由既有`Commit`登记到同一个MEMContent。没有新增工程树或UndoStore。
- [`14d8586`](https://github.com/oudream/cx-visual-x3/commit/14d85866dfc6da885ae672f7d0cd034f2ef6ba69)及[`4294b32`](https://github.com/oudream/cx-visual-x3/commit/4294b32bf49af36ac18bd32f164a919f5fc745e1)：加入原位结果身份切换、来源签名保持、旧Revision冲突、取消不改工程、MEM Undo/Redo反证；测试结束恢复原分析，保持已存在V04保存重开断言的基线。
- **验收限制**：提交时CI处于queued，暂无这一SHA的Release x64/ArchitectureSmoke成功证据；本提交仍未实现圆柱、参数编辑与新ROI选择后的完全失效级联、稳定子元素ID、多Case WPF完整工作流以及旧Schema1外部fixture实证，因此仍不应标A包全部完成。


### 10.10 A包：约束轴向圆柱基础专业拟合

- [`bab4a27`](https://github.com/oudream/cx-visual-x3/commit/bab4a2710fbf900a3b8b6a5fa36deb396851d70c)在既有`CT3DSurfaceSphereAnalysis.cs`增加`CT3DSurfaceCylinderAnalysis.FitCylinder`与`CT3DCylinderFitResult`。输入复用已验证的原值`CT3DSurfaceSamples`，指定物理轴向，正交投影后使用已有FNU SVD最小二乘拟合二维圆，输出轴线上点、单位轴向、半径、直径、毫米RMS/最大残差、条件数、Feature/Surface身份及来源Frame/Revision。拒绝零轴、未标定/截断数据、退化拟合，支持取消。
- [`6335cd2`](https://github.com/oudream/cx-visual-x3/commit/6335cd28db2b64c20a4cf5341cd9fa48676a0be6)增加真实UInt16 RAW圆柱体素样本与轴向拟合测试，包含毫米尺寸范围、来源/Feature身份、非法轴向与预取消。**此算法处理6邻域离散表面，采样可能包含端面；工业侧壁ROI及剔除端盖仍未闭合**，不能声明通用自动圆柱拟合或工业计量精度通过。
- 待续：将圆柱纳入Case/Artifact Schema完整保存重开和结果Measure统一入口；工程级跨Case编辑失效、ROI子元素稳定身份及真实Windows构建、UI/GPU/HIL门禁仍需实证。当前CI在该SHA检查时为`in_progress`。


### 10.11 A包：圆柱Case/Measurement/MEM制品组合闭环

- [`b4b7cce`](https://github.com/oudream/cx-visual-x3/commit/b4b7ccea8939e13a2ba2b613d26131d7f9fd65c5)将固定轴向圆柱纳入`CT3DAnalysisKind.SurfaceCylinder`、`AnalyzeCylinder`、`GetFeatureMeasurements`（Radius/Diameter）和`Recompute`。与原球/平面共用资产身份、量值和回归机制。
- [`9cab710`](https://github.com/oudream/cx-visual-x3/commit/9cab710282c37077ead2e27510569b2c3a61e124)在既有`CT3DAnalysisPersistence`的V2制品Kind判别下增加圆柱写入/读回/使用原FNU计算复核并恢复原ResultId；未新建外部Repository。
- [`d9a7bb0`](https://github.com/oudream/cx-visual-x3/commit/d9a7bb0169cbd5eab0b5bf939cf371f45c155c98)添加圆柱完整RAW→Surface→Feature→Measurement→MEM Save/Open→同源复算的专项断言；复算保持量值、更新Feature身份。
- **数值边界**：当前轴向由用户/算法明确给定，边界采样仍混入端面，不将此版本冒充完整圆柱侧面ROI分离或工业级计量。固定SHA Windows测试在查询时`queued`，G1/G2须以随后日志确认。


### 10.12 A包专业结果组合、来源治理与WPF复用（本轮增量）

- [`ec60c69`](https://github.com/oudream/cx-visual-x3/commit/ec60c693b4cec86ed337ca3f1ad8541ad8180be1)修正原位复算后存在多个内容寻址制品时旧测试对`*.analysis`调用`Single()`的错误：记录首次提交制品的实际路径，旧结果仍可精确损坏/缺失反证。对应[Unified Runtime 40862439377](https://github.com/oudream/cx-visual-x3/actions/runs/40862439377)为**SUCCESS**，仅证明该固定SHA的相应工作流。
- [`44aa27a`](https://github.com/oudream/cx-visual-x3/commit/44aa27a64dc73e1693f5964198ea8c99c4ae3e8c)、[`849bc2a`](https://github.com/oudream/cx-visual-x3/commit/849bc2a71e7f40162201eb1a0b2ae0addc3faf6d)：原`CT3DAnalysisCase`增加稳定`RoiSignature`和`ParameterSignature`（来源签名、算法版本、参数、物理ROI或圆柱轴），不是随机ResultId，重算和保存重开保持一致；原值/ROI改变后签名不同。配合[`eb455dd`](https://github.com/oudream/cx-visual-x3/commit/eb455ddf3def801050d131e8422db4f7d66f0c6f)实现编辑局部Mesh ROI或圆柱轴向后从RAW形成新Feature，[`32e3685`](https://github.com/oudream/cx-visual-x3/commit/32e3685196e49908e250d70a2c767b2812ae2748)增反证。
- [`bf4dd06`](https://github.com/oudream/cx-visual-x3/commit/bf4dd06341c4ef96195e48faed260a9e1a7e6873)新增`CT3DAnalysisPersistence.CommitAll`，多Case一次MEM Edit事务整体登记、来源逐一校验、单工程Revision与既有Undo；[`869c100`](https://github.com/oudream/cx-visual-x3/commit/869c100d7ecf7cbfd2cd244b3b9cd967704412a4)增原子提交、重复Owner、过期Revision及整体Undo/Redo回归。制品仍在事务前发布，可能留完整未引用文件，不能虚称跨磁盘原子提交。
- [`02f8ee6`](https://github.com/oudream/cx-visual-x3/commit/02f8ee6833e15573a41ee8c2bf48b74396612d5d)和[`f0a1919`](https://github.com/oudream/cx-visual-x3/commit/f0a1919398ca80e5b519be1eb8e3366838f3ee58)为每条`CT3DFeatureMeasurement`增加算法版本、参数/ROI签名、Frame和来源完整信息，并验证三类Feature。
- [`ffda2b2`](https://github.com/oudream/cx-visual-x3/commit/ffda2b237c8a78240cab05ab6e85c121ffaf678e)、[`b186992`](https://github.com/oudream/cx-visual-x3/commit/b18699290c146aa581060dce21663af1ba176ec7)、[`03b9091`](https://github.com/oudream/cx-visual-x3/commit/03b9091c08ee620cbabd50d01e91e894853860df)：原TestCenter复用正式圆柱Case保存重开、MVI/MPW投影、原值计算和球/圆柱切换失效；三维真实WPF Smoke增加显示/旧测量清理断言。它是可运行验收入口，不冒充全量Studio多Case工作台。
- 最新工程为26项目，R5仍为唯一总纲；工业Surface侧壁分离与计量认证、独立子元素持久ID、多案例完整UI、旧Schema1真实外部样本及最新固定SHA全绿仍未闭合。


### 10.13 A包完整专业合同续进与Windows证据（2026-10-29）

- **专业拟合/ROI**：固定物理轴向圆柱已进入`CT3DAnalysisCase`、持久化及TestCenter；`CT3DSurfaceCylinderAnalysis.FitCylinder`支持显式侧壁点索引，`CT3DAnalysisPersistence`在Schema3读写ROI且兼容V2圆柱无显式ROI旧制品，复算保留同样的ROI顺序。对应提交`376c597`、`36deb37`、`d75f5fc`、`de419f7`、`f07b9bd`、`60c4be2`。
- **稳定身份与专业证据**：`CT3DRoiVertexReference`公开原Mesh ROI顶点索引、物理毫米几何及稳定签名，`CT3DAssetReference.FrameSignature/SpatialFrameRef/MatchesSpatialFrame`独立标定引用用于跨MEM重开与不同源拒绝，`CT3DPlaneFitResult.SignedDistanceFromAssetMm`允许同资产同标定但不同实例的毫米测距；`CT3DQualityMetric`汇总平面/球/圆柱的RMS、最大残差、条件数及平面退化比，均注明非工业精度认证。对应`4646454`、`5a2ffe6`、`ae4a715`、`3a79390`、`16e5dd1`及正反测试。
- **多Case**：`RecomputeAllAndCommit`与`CommitAll`重用一次MEM修订，提供批量原值复算、冲突、取消和整体Undo/Redo；提交累计制品预算256MiB，OpenAll最多128个；TestCenter已支持按索引打开/再保存同一工程的不同Case，WPF Smoke验证所选Case与未选择Case不相互覆盖。对应`b05968c`、`fef01ef`、`3e186a3`、`7f06375`、`3d4f82c`。
- **已定位并修复的回归**：旧批量测试误插到单Case作用域造成CS0103，提交`5343458`修复；Undo后MEM拒绝脏会话关闭，提交`f6f699c`显式Discard；多Case WPF切换后恢复旧Volume的Frame对象导致误失效，提交`d7dcab1`改用当前Case Frame。未改变MEM事务、CT3D失效语义。
- **固定成功证据**：`f6f699c`的 [Engineering 40864641611](https://github.com/oudream/cx-visual-x3/actions/runs/40864641611)、[Runtime 40864641858](https://github.com/oudream/cx-visual-x3/actions/runs/40864641858)、[Decision 40864641767](https://github.com/oudream/cx-visual-x3/actions/runs/40864641767)均SUCCESS；`9899cb5` [Engineering 40865223526](https://github.com/oudream/cx-visual-x3/actions/runs/40865223526)、[Runtime 40865223564](https://github.com/oudream/cx-visual-x3/actions/runs/40865223564)、[Decision 40865223502](https://github.com/oudream/cx-visual-x3/actions/runs/40865223502)亦SUCCESS。证明对应SHA的Release x64/ArchitectureSmoke，不等于最新A包所有修改或G5通过。
- **待验阻点**：最新WPF修复及Schema3测试还要以其对应Windows SHA核实；旧Schema1外部历史二进制fixture实际缺失（当前仅显式SKIP）；GPU/真实工业样本G4/G5未作新增验收；大型工业圆柱侧面识别与不确定度评估不属于此离散拟合的已认证能力。持续以R5唯一主文收敛，不另建项目/通用工程树。


### 10.14 多Case WPF Frame竞态修复与固定验收（2026-10-29）

- [`d7dcab1`](https://github.com/oudream/cx-visual-x3/commit/d7dcab135a6d5c2d84f272aef2c3fbea3f807e46) 修复WPF多Case回归中的错误测试状态恢复：重新打开不同Case会创建当前`FVOVolumeData/FVOVolumeSpatialFrame`，测试在异步保存Frame变化后错误恢复旧案例的Frame对象，触发专业ROI过期保护。改为恢复当前Case捕获的Frame，未削弱正式失效检测。对应[Imaging 40865537994](https://github.com/oudream/cx-visual-x3/actions/runs/40865537994) **SUCCESS**，覆盖实际Windows 3D WPF验证；[TestCenter Command 40865538105](https://github.com/oudream/cx-visual-x3/actions/runs/40865538105) **SUCCESS**；全量TestCenter .NET48同时仍需记录其最终独立UIA结论。
- [`3e186a3`](https://github.com/oudream/cx-visual-x3/commit/3e186a3487a34a4315761c864ecc55d3f6b70bc0)后续Windows全工程编译通过，但其WPF Smoke因上述Frame测试逻辑失败；不得将其标为全绿。修复以后按上述固定SHA升级。
- 仍未将`AppData/Cases/CT3D/V04Schema1`外部旧版固定二进制样本误写为已验证；仍无实际工业量具G5精度认证。


### 10.15 A包严格验收门禁追踪（2026-10-29）

- [`422242c`](https://github.com/oudream/cx-visual-x3/commit/422242ce9fcc3e164a03ffb53a9a37a100332e4b)新增`CXVISUAL_CT3D_REQUIRE_LEGACY_FIXTURE=1`：执行正式Schema1跨版本验收时，如旧DLL生成的RAW、`schema1.cxproj`及`artifacts/ct3d`任一缺失，抛`FileNotFoundException`明确失败；普通ArchitectureSmoke仍明确SKIP，不能称兼容通过。严禁新算法伪造旧DLL固定二进制资产。
- 对应`master@0a06f4e`的Config公共底座与Config L1均SUCCESS（Runs `40865796814`/`40865796654`），但这些不是G5，也不覆盖工业准确度验收。`d7dcab1`的Imaging SUCCESS（`40865537994`），TestCenter Windows .NET48 run `40865538045`失败于一般`--ui-automation`超时，最后阶段为`InteractManualModule`；其`--connector-ui-automation`、Graphics窗口与前置部分已通过。属于MFM/TestCenter公共UIA待排，不可冒充A包算法失败，也不能标整体UIA成功。
- 继续区分：A包源码闭环、G1/G2/G3固定SHA已取得部分成功；严格历史格式fixture、真实工业标准件（G5）及Studio完整专业场景操作仍未取得证据。禁止标记完整验收。

### 10.16 收敛与新聊天交接（2026-10-29）

- 快进同步到`119ccf41`，按最新26产品项目执行；本轮收敛修改后的Unified Debug/Release x64均0错误，结构检查26清单一致，DAG审计27项目/112引用无非法边或环。本机证据位于`Output/CT3D-Handoff/`，不替代固定提交CI。
- 默认回归实际发现Decision `ImmutablePlanExecutionLoweringParity`的旧源错误码断言失败：`RequireCurrent`重建新源Manifest时会先触发新图的结构异常，掩盖已确定的修订过期。现先检查ProjectId/Revision/Catalog，再执行同版本内容Fingerprint校验，不降低同版本完整内容检测，也不提前调用Resolver。修复后默认ArchitectureSmoke退出0，`--imaging`和`--concurrency`分别退出0。
- **旧格式证据补齐**：此前本机已在改Schema前用`2386b7d4`正式旧DLL生成Schema1工程/RAW/制品，但这些样本未入Git，因此其它工作站和CI缺失。现将`AppData/Cases/CT3D/V04Schema1`真实固定文件、当时生成入口及SHA256摘要纳入版本控制；测试额外断言制品头为V1，再执行当前读取/复算/新Schema另存重开。`CXVISUAL_CT3D_REQUIRE_LEGACY_FIXTURE=1 --volume`和默认回归均通过，不能再将这次结果写成缺样本SKIP。旧CI缺样本记录保留其历史语境；完整兼容性仍只覆盖该固定合成样本。
- **本机WPF仍有未通过门禁**：带截图`--volume-viewer-smoke`退出2，四视图PNG 2482字节且整窗亦黑；祖先可见性、Opacity和布局尺寸检查正常。Render队列及软件模式试验未消除，临时修改均已撤回，不关闭原PNG断言。未设置截图输出变量时同一入口全部行为检查退出0，仅为功能定位，不算完整G3通过。会话/硬件呈现原因与产品导出原因尚需区分。
- **当前真实GL单独通过**：`--volume-gpu-smoke`退出0，日志`3.3.0 NVIDIA 576.02; replay=3; elapsedMs=762`。此入口实际检查原生鼠标、Raymarch像素、FGE Mesh上传像素差、两VBO独立清理、MVI绑定、样式零重传及隐藏/删除释放。截图`gpu-raymarch.png`为实际GL framebuffer。透明混合完整顺序、拾取和工业G5未据此验收。
- **需求原文补齐**：用户目标附件完整R01～R90已归档到架构Assets，原附件SHA256及格式去转义说明保留；R5附录C回填原先缺失36条，需求原文存在性和实现/验收分离。唯一总体架构仍是原V2.0 R5，没有新建V3总纲。
- 后续执行从[新聊天续接与大工作包交接](CxVisual_通用三维视觉工作台%23新聊天续接与大工作包交接_20261029.md)进入：先核实本提交CI和关闭WPF截图门禁，再承接A/B包现有成果，推进交互ROI→专业结果→组合显示→保存重开的完整业务包；不重复现有圆柱、多Case或多Mesh。完整26能力/90条要求仍未完成。

### 10.17 固定CI、Studio断言修正及本机WPF重复验证

- `5a8a9eec`实际10个工作流9成功：CT3D B [40876838837](https://github.com/oudream/cx-visual-x3/actions/runs/40876838837)的结构/DAG、Release、Headless（明确含Schema1 PASS）、真实WPF及截图、Debug全部成功；TestCenter `40876838827`、Decision `40876838889`、Runtime `40876838810`、Engineering `40876838787`均成功。Studio S1 `40876838916`失败不掩盖，实际编译成功，失败是旧配置三卡断言。
- Studio默认现有Vision、CT3D与MDS生命周期三项配置，测试另注册Automation后应为四卡。修正旧`Children.Count==3`为四卡，并额外确认真实MDS Editor存在，仍要求1～3列响应式容器，不降低为任意数量。完整本机`--shell-self-test`退出0，**61 PASS、0 FAIL**，`studio-self-test.log`已记录；生产配置注册行为未变。
- 本机三维PNG曾在ApplicationIdle延后执行后一次通过，整窗178931字节、四视图135806字节，已查看实际切片/十字线/设置内容；但**重复验证仍出现全黑**，显式等待ContentRendered也未解决。不能据这一次通过宣称根因已定位或稳定修复；所有临时App启动/首帧等待代码均已撤回，保留原像素门禁。本机渲染/会话因素仍待诊断，与上列固定SHA CI真实G3成功分别记录，亦不替代独立GL framebuffer门禁。
- 本轮正式修改只修正Studio已有配置贡献的测试预期，新提交Windows CI另按SHA查询。R5全部26能力/90需求、工业精度、完整ROI/Studio/CAD/动画仍需后续真实交付，不能据收敛修复标记平台完成。

**固定修复CI已核实**：并行master合入新增可视树遍历/真实二维流程测试后，最终修复源码为`28e1576f6b6160ec540c73d1124a932fde50ff0b`。[Studio S1 40886708549](https://github.com/oudream/cx-visual-x3/actions/runs/40886708549) SUCCESS，job `123492262961`日志含61条真实STA PASS、无FAIL，外加26清单与27项目DAG两条PASS；编译及上传视觉证据步骤成功。相同SHA的Graphics `40886708492`、Module CPU `40886708737`、Engineering `40886708483`亦成功。此结果关闭先前旧三卡断言的CI阻点，不关闭本机三维PNG不稳定或工业G5缺口。

### 10.18 B包首条真实交互ROI→原值Mesh→毫米平面→MEM交付（2026-10-30）

源码已由提交`6f7a9b5cea79d686b8709d2f22b008ecc18e5581`纳入master；中断恢复后核对并同步至`9a05144c`，承接并行FIM RAW导出及MPW悬停原点/方向修正，没有重写旧架构或重复提交已有代码。

- **MVI真源**：`MVIVolumeViewState.SelectedSliceRectangle/SelectRectangleRoi/ClearRectangleRoi`记录唯一会话选区，非法框选不替换有效选区；FVO仍负责像素采样到物理四角。不是第二Case/工程或数据副本。
- **真实可操作MPW**：Shift拖动在既有二维Viewer上预览和提交金色矩形；缩放/平移消费同一图像变换；离开所选切片隐藏轮廓，返回后恢复静态物理位置；解绑取消交互并解除旧状态订阅。未标定等交互错误经既有工作区反馈，SDK入口保留异常。
- **专业选择与拟合**：`CT3DMeshPlaneAnalysis.SelectSlicePrismVertices`以矩形所在切片为零点，按切片索引增加方向给定毫米偏移范围。原始Frame逆变换支持旋转、镜像及各向异性；横向使用采样角点范围并扩半个原始体素，法向在体素坐标下比较容差，不使用固定毫米误差吞掉微小Spacing。明确拒绝不同Frame、退化矩形、非法/溢出毫米范围、不足3点、取消及超过30000顶点；后续仍由原`FitSelectedVertices`/FNU判定平面秩、残差和条件数。
- **工作台及保存闭环**：真实毫米上下限输入、按钮与`ct3d.mesh.plane-slice-roi`命令共用正式算法。新选区立即清除旧Plane/Measurement，不能继续保存旧平面；有效索引ROI、来源、参数、毫米量值和质量继续进入原CT3D Case/MEM Artifact。保存期间再次框选时保留新ROI和失效状态，磁盘保存启动时快照；同工程其它Case隔离保持。
- **测试覆盖**：三正交切片在旋转/镜像/各向异性/大原点Frame下均选出9点端面；空区/退化/错Frame/非法范围/预取消；真实80³实心块的闭合Mesh超过30000顶点时明确失败，不能截断。Headless完整Save/Open/Recompute用切片ROI选择器作为实际入口；WPF验证轮廓、切层、真实输入及按钮生成新结果、ROI失效、保存竞争、多Case、重开与继续使用。`slice-roi-plane.png`增加真实金色像素断言，不以布尔状态代替可见轮廓。

**固定Windows证据**：`6f7a9b5c`的[CT3D B G1～G3 40892609609](https://github.com/oudream/cx-visual-x3/actions/runs/40892609609)，job `123513142718`全部步骤SUCCESS，包含Release、Headless、WPF/PNG（执行ROI金色像素门禁）、Debug。相同SHA的Runtime `40892609143`、Decision `40892609459`、Graphics `40892609730`、Engineering `40892609521`、Command `40892609177`、Studio `40892609052`成功。12个工作流中另两项失败：Imaging `40892609085`的二维WPF缺拆分式入口（同Run的Headless/default与三维WPF成功）；TestCenter `40892608935`的通用UIA超时于`InteractModuleKind / Connector`，后续步骤跳过。不能标全部通过。

**恢复后本机证据**：最新源码Release/Debug均0错误；原32位MSBuild曾JIT/访问冲突退出，切换实际存在的同安装`Bin/amd64/MSBuild.exe`后构建成功，未改语言/依赖绕过。默认ArchitectureSmoke、严格`--volume`、三维无PNG行为回归及真实输入按钮均退出0；本机带PNG仍在早期四视图导出全黑检查失败，未假报G3。日志为`Output/CT3D-Handoff/roi-resume-*`、`roi-button-*`。

**二维门禁收敛修正**：独立Mask/Region控件确实存在于“变换”页，旧自测先切到“输出”验收RAW后未切回，再按视觉树查找控件而失败。现按真实用户路径切回“变换”，仍要求按钮/半径输入可见，补充失败诊断，不改算法或用逻辑树访问隐藏控件冒充操作；完整本机`--imaging-viewer-smoke`退出0。新增毫米输入/真实按钮及此导航修正的新SHA CI须另核。

**尚未交付**：矩形和法向偏移交互配方未独立持久化，重开保留的是有效Mesh顶点ROI/量值而非原矩形草稿；通用3D ROI形状/控制柄、正式Studio装配、跨资产ROI与Frame图、工业亚体素面/不确定度仍缺。继续扩展该业务链，不将此首条闭环当作完整B包或26能力验收。
