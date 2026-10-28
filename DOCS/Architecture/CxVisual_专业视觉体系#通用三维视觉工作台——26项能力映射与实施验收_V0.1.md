# CxVisual 通用三维视觉工作台——26项能力映射与实施验收 V0.1（历史快照）

> **已归档：现由整体架构与UML V2.0第10章唯一维护26项能力主矩阵。** [打开当前主文](CxVisual_专业视觉体系%23通用三维视觉工作台——整体架构与UML_V2.0.md)。本V0.1保留初始2026-10-27的源码判断与迁移来历，**不得作为最新实现、Owner、CI或完成度证据**；后续不在本文件重复更新状态。

> 2026-10-27。范围：用户完整开发要求（01～90）；依据 `master` 当前的 FVO/FIM/MVI/MPW/CT3D/Tests、旧 `ReferTo/Vision` 与现有三维 48 行迁移 CSV。**本表是有证据限制的首轮代码映射，不是全部功能已完成声明。**
> 基线SHA：`a5159a931d624299dd78d44e91dd3372eac9cd0e`。源代码层存在 ≠ 编译/功能测试/硬件/工业现场验收；后续每项须持续填写固定SHA与Windows CI。
> DOCS/02～04 Excel 本轮尚未做各Sheet逐项解包比对；正式类型归属有争议时仍以最新规格复核。不得因本表扩充公共Owner或发明第二套架构。

## 26项工作台能力对照（按当前已检查源码审慎分类）

| 编号 | 完整能力 | 现有实现/复用线索 | 本轮真实性与主要缺口 | 初步Owner |
|---|---|---|---|---|
| 01 | 多源输入：RAW/DICOM/NRRD/TIFF/点云/网格/CAD；文件/内存/指针/共享内存/流/批量 | `FVORawVolumeLoader` 文件RAW、`CreatePreviewFromMemory` 原生指针、`TCTManualVolumeViewerFrame.LoadBmpStack` | RAW/BMP已具可执行链；本轮增加有独立原始快照的内存/指针输入。DICOM等尚无本轮有效源码证据 | FVO/专业接入 |
| 02 | 体数据质量、间距、方向、坐标系与标定 | `FVOVolumeData` Spacing/OriginalWidth 等 | Spacing具备；方向矩阵、世界/CAD坐标与质量检查未验 | FVO/F.Geometry/CT3D |
| 03 | 滤波、降噪、增强、重采样、插值、裁剪及变换 | `FVOObliqueSliceBuilder` 插值、`FVORawVolumeLoader` 预览抽样 | 切面/预览插值存在，通用三维处理工具集无验收 | FVO/CT3D |
| 04 | 大数据、分块、缓存、LOD、并行/GPU | `FVOUnmanagedRawVolumeBuffer`、RAW Map、MPW GPU | RAW只读映射及异步缓存存在；LOD/显存和多GB工业数据压力待实机 | FVO/MPW |
| 05 | 体绘、MIP/MinIP、等值面、点云/网格等渲染 | `MPWVolumeOpenGlControl` + `MPWVolumeShaderSources` | GPU Raymarch有源码，实卡画质待验证；其它渲染模式缺完整证据 | MPW/CT3D |
| 06 | MPR、任意切、曲面重切、厚层投影 | `FVOSliceImageBuilder`、`FVOObliqueSliceBuilder` | 三正交与任意切已有自动回归；曲面与厚层未签收 | FVO/CT3D |
| 07 | 多视图、分屏、十字线、三刀与同步定位 | `MVIVolumeViewState`、`TCTManualVolumeViewerFrame` | WPF 2×2/单窗/三刀与PNG有Windows测试 | MVI/MPW |
| 08 | 3D ROI、标注、图层、自由区域及编辑 | `Mechanism.Visual` 现有二维ROI参考 | 三维ROI与编辑数据模型未见完整验收 | MVI/CT3D |
| 09 | 3D/时序动画、时间轴、关键帧、视频 | MPW已有交互相机 | 交互相机不是动画；关键帧保存/播放/视频未签收 | MVI/MPW/CT3D |
| 10 | 三维分割、Mask/连通域、多材料提取 | ReferTo与专业库可进一步审计 | 未发现当前CT3D正式分割算子证据 | CT3D |
| 11 | 表面确定：自适应、梯度、亚体素与质量评价 | 基础体数据及几何能力可复用 | 不可将简单等值面视为全功能；待开发/验收 | CT3D/FVO |
| 12 | 点云/网格：重建、三角化、平滑/修补 | Foundation.Geometry 可继续审计 | 体素转三角网格及点云工具链尚无签收证据 | F.Geometry/CT3D |
| 13 | 几何特征与鲁棒/约束拟合 | Foundation.Geometry / Numerics 待逐类查证 | 平面/圆柱/曲面拟合及误差评价未验 | F.Geometry/F.Numerics/CT3D |
| 14 | 配准：基准、特征、ICP与变换记录 | 现有 Geometry 变换/CT3D待查 | 无 CT↔CAD 配准闭环证据 | CT3D |
| 15 | 几何尺寸测量与多坐标转换 | FVO Spacing/切片坐标基础 | 距离/壁厚等专业测量基准与精度报告未验 | CT3D |
| 16 | GD&T形位公差 | 可依托未来基准/拟合结果 | 尚无可签收完整公差计算链 | CT3D/Inspection |
| 17 | STEP/IGES/Parasolid/SolidWorks CAD | 需核对CAD SDK实际授权/依赖 | 不以SolidWorks文件名等同原生解析；未验收 | CT3D/宿主装配 |
| 18 | CT/点云/网格 ↔ CAD偏差比较 | 配准+CAD几何基线待建立 | 偏差色谱、超差区域及报告缺闭环证据 | CT3D/Inspection |
| 19 | 逆向工程与参数化曲面重建 | F.Geometry与专业组件候选 | 网格/NURBS转换没有当前签收证据 | CT3D |
| 20 | 气孔、裂纹、夹杂等三维缺陷检测 | 旧专业Vision/Inspection可复用线索 | 3D缺陷真值/报告待验证 | CT3D/Inspection |
| 21 | 壁厚、间隙、干涉、装配评价 | 表面/距离算法为前置依赖 | 尚无工业真值验收 | CT3D/Inspection |
| 22 | 多材料、多部件与结构统计 | FVO统一体素是基础 | 材料界面、部件归属与结构统计待做 | CT3D |
| 23 | 算法实验与诊断：输入输出/中间结果/性能 | TestCenter + 统一执行/数据机制 | 测试底座已有；专业算法可视调试整体未验 | Mechanism/TestCenter/CT3D |
| 24 | 批处理、任务模板、自动判定 | Mechanism.Orchestration/Runtime | 共用运行机制，三维任务链与工业HIL未验 | CT3D/Inspection/Runtime |
| 25 | 体数据、切片、网格、视频与报告输出 | `TCTManualVolumeViewerFrame` PNG、任意切Float32 | PNG和切面原值已有专项验收；CAD/网格/视频/报告待做 | FVO/CT3D/Inspection |
| 26 | SDK/插件及VTK/ITK/HALCON/OpenCV/专业算法 | Mechanism.Lifecycle、既有专业库资产 | 插件和SDK不能等同外部库已接入；真实DLL/版本/许可证待查 | Mechanism.Lifecycle/专业Owner |

## 交付与验收顺序

1. **S1 体数据基础**：保留现有FVO唯一数据Owner；补齐输入源、原始值/预览分离、空间标定、文件/缓存生命周期。当前新增 `FVORawVolumeLoader.LoadFromMemory` 明确拷贝原始Byte/UInt16/Float32，退出宿主内存后仍可做高清切片；不复写现有快速借用指针接口。
2. **S2 三维显示交互**：在既有MPW WGL、MVI三刀和WPF四视图上验证GPU真实画质、联动、参数与异常；勿创建第二套Camera/TransferFunction/Undo。
3. **S3 表面与几何**：先核对Geometry/Numerics成熟源，实现可精度评价的表面确定、网格与鲁棒拟合。
4. **S4 计量与CAD**：复用S3实体，形成坐标链、基准、GD&T、真实CAD转换与差异统计。
5. **S5 高级分析与自动化**：缺陷、壁厚、多材料、动画、批量执行、完整数据报告和系统集成。

未实证项不得标记“已完成”。每工作包补 **旧实现→新Owner/API→真实运行→异常/数值回归→Windows CI SHA→工业实测** 证据；AI、4D、FEA按用户要求预留，不冒充当前交付。
