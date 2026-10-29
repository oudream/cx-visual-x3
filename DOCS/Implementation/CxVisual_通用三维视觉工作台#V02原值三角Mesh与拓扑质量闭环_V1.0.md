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
