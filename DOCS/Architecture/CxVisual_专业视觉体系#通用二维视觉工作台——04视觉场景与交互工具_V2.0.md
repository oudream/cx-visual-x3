# CxVisual 通用二维视觉工作台——04 视觉场景、ROI与交互工具设计 V2.0

> 覆盖V2D-05—09，关联13、14、21、23、24。现有真实锚点：MVIImageSession、MVIImageViewport、MVIImageDisplayState、MVIImageComparisonState、MVIImageTransformState、MVIImageOverlayStore/Item、MVIImageLinePairGesture、MPWImageViewerControl。目标模型参考02 C03/C04、ST01、SQ02。**多产物统一Scene、工程ROI投影和泛化工具控制均属候选，未实现。**

## 1. 场景职责与真源边界

用户看到的二维场景由五个逻辑平面组合：①源图像显示（Image/DisplayLUT）；②派生图、差异和Mask（ResultArtifact）；③临时ROI/测量草稿（MVI OverlayStore）；④正式工程标注投影（MEM/MRC只读ViewProjection）；⑤交互反馈（选中、Hover、控制柄、临时标签）。视口/选择状态**不拥有**工程定义或算法结果本体。MVI负责图像坐标、交互意图与会话，MPW负责鼠标/键盘/DPI/渲染，Foundation只负责像素和通用几何。

~~~mermaid
flowchart TB
  A[Image/Artifact DataRefs] --> V[Scene Projection候选职责]
  B[MEM/MRC工程标注] --> V
  C[MVI临时Overlay] --> V
  D[MVI Viewport/Transform] --> V
  V --> W[MPW各Pane的图层渲染]
  E[鼠标键盘] --> W
  W --> F[MVI手势状态/编辑意图]
  F --> C
  F -. 正式工程操作 .-> B
~~~

**禁止**将结果列表、算法Pipeline或工程Undo数据强行塞入MVIOverlayItem；一个结果可产生多个显示图元，一个正式ROI可以在多个Pane被投影，但数据身份只能有一个。

## 2. 坐标与形状不变量

坐标分为像素索引(x,y)、FGE连续原图点、视口DIP点、物理单位以及配准后的目标图坐标。画布反算需显式考虑中心/缩放/平移/旋转/镜像/DPI，绘制、拾取、面积计算基于原图坐标，不把WPF Point直接当像素整数。像素中心与包含边界的FIMPixelRegion需按运算语义显式取整；非等距Spacing应分X/Y标定，角度/最近点可能因物理变换改变，不能直接套单倍数。

| 对象 | 形状约束 | 编辑规则 |
|---|---|---|
| Rectangle/RotatedRect | 宽高有限、正值、角度规范化 | 8控制柄/旋转手柄、拖动与约束 |
| Ellipse/Circle | X/Y半径有效；物理圆与像素圆不同 | 控制柄改变边界时保留对象ID |
| Polygon/Polyline | 至少相应顶点、闭合与退化校验 | 顶点插入/删除/移动与大顶点数性能 |
| Bezier/自由曲线 | 明确控制点与采样容差 | 曲线显示和实际测量曲线不同 |
| Mask/Region | 值域、原点、孔洞、关联SourceRef | 显式Mask转换及来源版本 |
| Measurement | 端点、算法、单位、质量与结果引用 | 编辑时自动重算且不破坏原图 |
| Text/Defect | 文本安全及可见性、结果身份 | 字体/颜色/图层/锁定与复核区分 |

## 3. 工具状态模型与自然交互

交互状态至少分：Navigate、Drawing、Dragging、Resizing、Rotating、EditingVertex、AwaitingAdditionalPoint、Preview、Complete/Cancelled；不要求实现同名巨大枚举，要针对真实多阶段工具维护最小状态及原图坐标草稿。已有MVIImageLinePairGesture是特例基线，工具接口/共享控制器先通过两种以上真实手势归纳再决定。

| 触发 | 预期 |
|---|---|
| 选中工具 | 状态高亮、指针样式、状态栏提示下一步 |
| 首点/持续拖动 | 预览在正确视口，与正式Store分离 |
| 有效完成 | 一次提交、保留稳定Guid、选中新对象 |
| Esc/工具切换/换图 | 清理Draft和捕获，已有正式对象保持不变 |
| 失去捕获/窗口关闭 | 不产生半成品；释放临时资源 |
| 退化点/非法值/越界 | 拒绝且状态不前进，非模态反馈 |
| 双Pane同一操作 | 回投同一原图点，预览随两个Viewport投影 |
| 锁定/隐藏层 | 不可编辑/拾取，与视觉显示相符 |

## 4. 典型交互流程

~~~mermaid
sequenceDiagram
  actor U as 用户
  participant W as MPW
  participant T as MVI ToolState
  participant S as MVI OverlayStore
  participant E as MEM编辑
  U->>W: 选择测量工具并绘制
  W->>T: 原图坐标+当前输入阶段
  T-->>W: 校验后的草稿
  W-->>U: 预览/标签
  U->>W: 完成
  W->>S: 一次提交临时测量对象
  S-->>W: Changed
  opt 明确保存为工程标注
    W->>E: 版本校验/编辑事务
    E-->>W: 成功修订或拒绝
  end
~~~

Viewer不为算法产生的临时轮廓自动执行工程Commit。组合工具/属性页使用同一选择ID和图层状态；更改原图或选择结果图必须校验叠加的SourceRef，不能把原图ROI误投到不同尺寸的结果图。

## 5. 多图比较与视口协同

比较模式建议包括Single、SideBySide、Blend、Swipe、Difference及FutureRegistered。每个Pane具有独立Viewport或共享参数，可选联动。需要明确同尺寸、坐标空间、是否已配准、物理Spacing与PixelFormat兼容性；不同来源直接融合若无Transform必须拒绝或提示。差异数值分析放FIM/PVC，MPW只负责显示合成；比较对象应具来源身份而非两个裸Buffer指针。

## 6. 渲染分层、性能与交互无障碍

当前MPWImageViewerControl.xaml.cs超2300行、TCTManualImageViewerFrame和布局亦较大；这并不自动说明当前实现无效，而是后续功能扩张的维护风险。优先把真正与WPF无关的状态、坐标、复杂手势归MVI；保留MPW真实WPF渲染与设备输入转换；不为每个工具新建一个独立Manager。大图场景的千万级像素Mask不应转成千万个WPF Shape；优先纹理/位图叠加、轮廓聚合和可见范围裁剪，并以真实性能数据决策。展示提示必须可被UIA读到，参数错误非模态且不可挂无人CI。

## 7. GoF/AOP边界

GoF：Observer驱动Session/Store变更；LinePair按State思想；工程Command归MEM/MTC；复杂组合利用已有MRC Composite；工具切换不强制全类继承层级。AOP：记录工具激活、错误/取消、单次修改数、重绘耗时、相机/源版本；严禁每个鼠标Move写磁盘日志。

## 8. 验收矩阵：D04系列

| Case | 正向与反向验收 |
|---|---|
| D04-01 | 鼠标缩放、平移、旋转、镜像后像素/原图/物理坐标回环 |
| D04-02 | 单Pane和双Pane同一ROI，变换后拾取/标签一致 |
| D04-03 | Ls2Ls四阶段/退化点/Esc/失去捕获/换图不污染Store |
| D04-04 | 多边形数千顶点选择/控制柄、轮廓大批绘制测性能 |
| D04-05 | 属性页锁定/图层隐藏/颜色/线宽/文字/重复ID一致 |
| D04-06 | ROI暂存→正式MEM提交→Undo/Redo→保存重开，来源身份不丢 |
| D04-07 | 非法颜色或几何输入只给非模态错误，无模态阻塞 |
| D04-08 | 125%/150%多DPI、双屏与实际物理鼠标HIL |
| D04-09 | Gray16显示调窗前后原像素不变、Mask锚定正确 |

证据分级：已有Windows WPF合成控件smoke不等于物理鼠标/DPI及现场机台验收。

## 9. 交互场景与正式工程三类所有权（架构深化）

Vision Scene先定义为**工作台投影职责**，不是必须新建的SceneDatabase。图像/Mask/产物来自FIM+Vision Artifact，临时ROI来自MVIOverlayStore，正式ROI/标注来自MEM/MRC，选中和Viewport由MVI持有，MPW负责WPF渲染。一个ResultItem可投影多个Overlay；同一正式ROI可显示在两个Pane，但不复制MEM对象身份。

| 操作 | 合法写入方 | 禁止行为 |
|---|---|---|
| 调窗、Zoom、切图 | MVI会话/视口 | 修改原图、Result证据或工程Revision |
| 多阶段ROI草稿与临时提交 | MVI状态及OverlayStore | 在WPF MouseMove中产生工程事务 |
| 检测轮廓高亮 | Vision Result→MVI显示投影 | 复制ResultId为MRC稳定Id |
| 用户确认保存ROI | MEM/MRC/MTC事务 | OverlayStore.AddRange冒充Undo/工程Commit |
| 多Pane同源图 | 各Viewport对SourceRef投影 | 屏幕DIP直接作为原图/物理坐标 |

需要独立ToolContext或SceneProjection时，先证明两个以上真实工具/宿主存在可复用变化点；否则继续扩展现有MVI状态而非增建空Facade。关键反证是失去鼠标捕获/Esc/切图不会遗留半提交，双Pane坐标/DPI一致，临时Overlay可清除但正式资源与历史Run不受影响。
