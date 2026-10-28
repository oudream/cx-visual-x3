# CxVisual 通用三维视觉工作台——V2.0源码差距与渲染变化集首包 V1.0

> 日期：2026-10-28；类型：实施/审计记录；本轮只在既有CT3D设置和ArchitectureSmoke中落地强类型变化分类，不创建工程或新的Volume/Camera/Scene真源。固定实施SHA以本文件所属Git提交为准，Windows结果请核对该提交实际工作流。\n

## 1. 以架构V2.0审查的主要事实

| 能力/Owner | 已有源代码 | 差距/裁决 |
|---|---|---|
| FVO体数据 | `FVOVolumeData`、`FVORawVolumeLoader`、无损缓冲及MPR | 原值/预览分离已存在，但`GetScalarByOriginalIndex`在缺原值缓存时可回退显示样本，**不得直接用于计量真值**；后续必须明确精度级别与数据租约 |
| FGE/FNU | `FGEGeometry2D`、`FNULinearAlgebra`、`FNUWeightedLeastSquares` | Geometry当前主要为2D；3D坐标/曲面/拟合未作为完成项；先复核02～04 Owner和精度契约 |
| MVI状态 | `MVIVolumeViewState`：三刀、任意切、切片视口 | 尚未有统一3D Camera真源、完整场景/选取/动画；与现有二维`MVISceneItem`区分 |
| MPW呈现 | `MPWVolumeOpenGlControl`、`MPWVolumeSliceSceneControl`、四视图 | WGL、GPU纹理/Shader、相机/释放集中；GPU启动协调仍在`TCTManualVolumeGpu` |
| CT3D | `CT3DRenderingSettings`、`CT3DVolumeLoadService` | 参数批量验证和LUT判断已有，缺可表达版本/分类的变化结果；本包补齐该入口 |
| 专业链 | 表面、拟合、配准、测量、CAD/缺陷/AI | 26项产品目标的后续专业能力；不以类名或参考库存在冒充已实现 |

## 2. 本次可编译API增量（正式Owner：Professional.CT3D）

- `CT3DRenderingChangeFlags`：`TransferLut / Uniforms / Scene`；仅涵盖CT3D渲染参数变化，**不**涵盖FVO体数据/切片或MVI Camera Revision。
- `CT3DRenderingChangeSet`：包含单个设置实例内单调递增的`Revision`、`Flags`和分类查询属性；不是持久化工程版本、Case版本或GPU已呈现版本。
- `ApplyParametersWithChangeSet(values)`：先全量验证，逐字段分类比较；零变化零Revision；全部合法后一次提交。
- `ApplyParameters(values)`：保留老API/返回布尔LUT失效口径，内部复用新分类机制。
- `NotifyTransferFunctionsChanged()`：兼容现有外部直接编辑FVO控制点的入口；**尚未统一自动绑定**。后续应由专业会话或正式编辑操作调用。

现有Settings的子对象/标量属性仍可直接写入，故本阶段Revision仅统计通过上述两个新增入口的提交；**不能将其冒充全局唯一变化版本真源**。现有TCT仍按兼容旧方法工作，未完成P2的调度集成。

## 3. 设计上不做的事

- 不让MPW引用Professional.CT3D、不让CT3D引用MPW。
- 不在MPW渲染循环引入新的专业算法引擎、通用AOP代理或全局消息总线。
- 不将Camera、Volume、Slice、Geometry的不同Owner版本硬塞进本参数ChangeSet。
- 不因为增加枚举就提前建立RenderBackend策略接口、空的Scene子类和新的Undo体系。

## 4. 本包验证与后续门禁

已新增ArchitectureSmoke/Integration/CT3DRenderingTests的独立断言：完全相同输入、Scene单独变化、LUT+Uniform同时变化、非法值无部分提交、外部曲线手工失效。原有Slicer LUT和无效输入测试应保持不变。

**本轮提交后需以实际GitHub Actions结果认定，未运行不能写PASS。** 映射：`imaging-foundation-windows.yml`自动进行Release x64/.NET48解决方案编译、Owner依赖检查、`ArchitectureSmoke --volume`及WPF真实切片回归。真实OpenGL硬件画质和GPU显存仍需要独立`--volume-gpu-smoke`及现场显卡。

### 下轮实施建议

1. P0：FVO“原始数据可用于计量”的明确契约、空间frame/units精度与Case/ViewSession引用边界；先核查02～04 Excel。
2. P1：MPW GPU生命周期State、资源释放/重建及TCT转为消费者，保持现有WGL行为。
3. P2：CT3D变更通知与MPW刷新分类对接、旧事件合并和异步版本筛除。
4. P3：唯一MVI Camera与轻量3D Scene，逐步让GPU/WPF共享，不新增第二工程树。
5. P4：真实显卡、工业RAW、几何真值、重复装载、性能与长时间运行。
