# CxVisual 专业视觉体系——二维图窗 ROI/标注成熟功能迁移与实测差距 V1.1

> 日期：2026-10-26，仓库 `oudream/cx-visual-x3`，`ReferTo` **只读**。先恢复成熟旧功能、再优化结构和补缺，不能以按钮/枚举/空类型冒充完整功能。
> 本文是 P2 真实迁移记录，基线为 [二维视觉工作台60项目标](../Architecture/CxVisual_专业视觉体系%23通用二维视觉工作台——完整功能架构与实施验收_V1.0.md)。最终以最新Windows Actions的实际结果为准。

## 1. 本轮复用哪些旧现役文件

| ReferTo旧文件 | 新归属 / 真实代码 | 保留、改造内容 | 当前等级 |
|---|---|---|---|
| `ReferTo/Vision/CxECTImageWinForms/Models/Overlays/ImageOverlayItem.cs` | `Mechanism/Visual/Imaging/Overlays/MVIImageOverlayKind.cs`, `MVIImageOverlayItem.cs` | 17个ROI/测量/标注/缺陷旧种类、四图层、Guid标识、Bounds/Points、文字、外部ID、ARGB/线宽；以FGEPoint2D/FGERect2D复用现有几何，而非复制旧ImagePoint；快照式WithBounds/Translate/WithStyle保证通知可见 | 源码已提交 |
| `ReferTo/Vision/CxECTImageWinForms/Models/Overlays/OverlayStore.cs` | `Mechanism/Visual/Imaging/Overlays/MVIImageOverlayStore.cs` | 唯一Owner列表、Add/Remove/Find/Clear、SetLayerVisible/IsLayerVisible/GetVisibleItems；补Replace事件与快照，禁止重复ID | 源码已提交 |
| `ReferTo/Vision/CxECTImageWinForms/Models/ImageToolMode.cs` | `Mechanism/Visual/Imaging/MVIImageToolMode.cs` | 保留互斥工具思想，只开放已有对应交互行为的类型，不因旧枚举包含全部模式就宣称全部已实现 | 源码已提交 |
| `ReferTo/Vision/CxECTImageWinForms/Halcon/HalconImageCanvas.cs` | `Mechanism/Presentation.Wpf/Imaging/MPWImageViewerControl.xaml(.cs)` | 不搬运HALCON窗体句柄。用WPF原生Canvas/Shape真实绘制覆盖层，保持原图坐标，支持缩放重绘/图层过滤、几何拾取、鼠标移动和删除 | WPF源码已提交；部分Smoke已通过 |
| `ReferTo/Vision/CxECTImageWinForms/Controls/ImageViewerControl.cs` | `Tests/TestCenter/Manual/Imaging/TCTManualImageViewerFrame.cs` | 直接使用MPW工具模式、叠加项Store和可见图层；真实列表、选择、删除、移动和显示开关 | UI源码已提交 |

源旧模型的 Mutable `Points` 和随意属性赋值容易使 WPF 绘制漏更新；本轮保留能力语义但升级为可更新快照，并在Store层发出`Changed`事件（GoF Observer）。既有MEM/MRC是工程持久真源；MVI仅承担图像**临时浏览覆盖状态**，**绝不建立第二工程/Undo仓库**。

## 2. 真实 WPF 功能细分

- **直接鼠标绘制**：导航/平移、矩形ROI、圆/椭圆ROI、两点距离（px）、点/文字/箭头标注、缺陷框；另有鼠标几何命中、覆盖项移动/删除。
- **API呈现支持**：多边形ROI、矩形与多边形标注、直线剖面/角度等旧模型分类可存储。未完成实际对应交互/测量的类别，**不得标成已完整支持**。
- **统一图层**：ROI/Measurement/Annotation/Defect各自显隐；隐藏层不参与几何拾取。覆盖项不可变式平移/缩放同步Bounds与Points、稳定Guid；错误参数/重复Guid拒绝。
- **导出不同含义**：`MPWImageViewerControl.SavePng`只写当前原始或处理图像像素；`SaveViewPng`按当前WPF视口导出包含ROI、测量、缺陷和标注的截图，不能互相冒充。
- **TestCenter**：使用现有 `manual.imaging`，工具下拉、标注文字参数、图层列表，列表的删除/位移/隐藏/恢复/清空，不复制另一套覆盖项。

## 3. 本轮真实开发提交

| 提交 | 功能/代码 |
|---|---|
| `c71806d` | 从旧OverlayItem/OverlayStore抽取17分类+四图层+快照，并接ArchitectureSmoke |
| `5b607d2`, `a51888e` | MPW WPF Shape叠加投影及命名空间冲突修复 |
| `8f08e8f` | TestCenter真正可操作的工具/叠加项列表 |
| `0b07ef1` | WPF视口截图、四类示例覆盖项及显示/移动/清空回归 |
| `7d242f0` | Bounds缩放同步顶点、加入鼠标移动与删除工具模式 |
| `3db8c8b` | 几何拾取/鼠标拖动/点击删除、边界约束 |
| `729c1a3` | WPF真实窗口几何命中、顶层优先和隐藏图层反证 |

## 4. 验证门禁与现实结果

- **已通过**：针对提交 `0b07ef1` 的 [Imaging Windows专项运行40244449790](https://github.com/oudream/cx-visual-x3/actions/runs/40244449790)，全解决方案.NET48 Windows x64构建、结构与依赖检查、FIM/MVI专项及默认ArchitectureSmoke、WPF控件/PNG+覆盖项截图回环全部成功。
- **最新版已确认**：`729c1a3` 对应 [Imaging Windows专项run 40244798348](https://github.com/oudream/cx-visual-x3/actions/runs/40244798348) **成功**：全解决方案.NET48 x64构建、依赖/结构、MVI覆盖项专项与默认回归、真实WPF视口叠加绘制/命中与截图步骤均通过。此前 `7d242f0`、`3db8c8b` 的内容已包含在此后续提交中；这仍不是人工逐工具/现场SDK/工业性能的全面验收。
- **专业与工业现场**：HALCON/OpenCV句柄、产线采图、数万张批次与4320²大图性能、重复打开内存释放未验收。
- **未补齐的P2**：旋转矩形、完整多边形鼠标编辑、Bezier自由曲线、测量角度/直径的编辑与输出、文本样式、八控制柄/旋转/锁定/图层排序、持久化、复用已有MEM/MRC Undo/Redo。旧代码已有成熟模块的，要逐方法迁移并核对，而不是重新简单写一套。
- **P3/P4/P5**：高级图像算法、Pipeline调试、实时并行、专业HALCON/OpenCV与Studio仍没有完整闭环，不能提高完成等级。

## 5. 下一阶段必须按成熟功能逐项验收

1. 旧`ImageTransformState`与`ImageDisplayState`对应源码直接迁入MVI，先做可逆坐标映射/自动窗宽、Gamma/亮度/对比度、旋转/镜像的**纯显示不改变原图**。
2. 逐项迁移`HalconImageCanvas`成熟交互：多边形、绘制/移动/缩放控制柄、标注文字/箭头/缺陷、测量回显；复用FGE/MRC现成的几何、编辑、唯一Undo机制。
3. 恢复旧`ImageRulerControl`、`HistogramPlotControl`、`LineProfilePlotControl`的显示能力，转为真正的WPF专用控件，补旧新同样本输出和鼠标实际验收。
4. 用旧`ImageComparisonState`恢复原结果图、并排、叠合、联动、差异；用至少一套工业8/16位实际图像与原程序并行验收。
5. 任何缺失必须标明阶段/Owner/源文件，不准仅以生成一批接口、测试小图片就标记P2/P3完成。

## 6. P2 连续实施：八方向ROI编辑、对象锁定与层叠排序（2026-10-26）

**源码与证据最新基线**：功能提交`0b14df18ba73f0a2c8ba840826d1ed4d227ddd5e`。历史第4节“八控制柄/锁定/图层排序未完成”属于此前快照，**本节更新这些单项的状态**，但不能将整项P2、旋转矩形或完整编辑器视为完成。

| ReferTo原实现/功能 | 当前正式Owner/方法 | 用户入口/数值效果 | 当前等级 |
|---|---|---|---|
| `ImageViewerControl.ResizeRoi`、`ImageToolMode.ResizeRoi` | `MVIImageOverlayItem.WithResizedRoiHandle` + `MVIImageResizeHandle` 八方向 | 原图连续坐标四边/角拖动，钳位于图像范围，最小0.25px，不允许倒置；形状、顶点及对象GUID保留 | 已实现/编译/自动测试 |
| `ImageOverlayItem.Bounds/Points` 和 `OverlayStore` | MVI现有`WithBounds`、`MVIImageOverlayStore.Replace` | 矩形、圆/椭圆ROI、多边形按照BoundingBox编辑；多边形顶点按比例一起更新，不复制对象Store | 已实现/验证，圆/自由曲线的完整几何编辑仍需后续 |
| `HalconImageCanvas` 交互控制柄及缩放重绘思想 | `MPWImageViewerControl` 同一控件的`DrawResizeHandles`、`BeginResize`、`UpdateResize` | 左右视图都按自己的MVI视口投影8个9DIP控制柄；拖拽事件使用显示坐标逆映射到原图（含90°、镜像）；同一Guid随Store通知更新 | WPF窗口控件、模式和编辑柄真实渲染已验证；Windows物理鼠标端到端手势尚待人工/输入注入专项 |
| 成熟Viewer工具互斥和叠加项列表 | `MVIImageToolMode.ResizeOverlay`、`TCTManualImageViewerFrame/Layout` | TestCenter区域页“八柄缩放”按钮、列表选中联动；两视口使用唯一OverlayStore | 真实WPF按钮/选中及可见句柄测试通过 |
| 旧图层/对象显示与层叠 | `MVIImageOverlayStore.MoveZOrder`、`SetLocked`、MVI对象`IsLocked` | 上移/下移单步调整叠加顺序，直接影响Canvas绘制与几何命中；锁定/解锁状态在原Store，列表显示锁标识；锁定后禁止缩放、移动、样式修改、普通删除及工作台清空 | Windows MVI及WPF测试通过；不是跨工程持久化锁 |
| 原Viewer重载/清图 | 原`ShowOriginal`/`Clear`依然调用唯一Store清理 | 普通用户`ClearOverlays`尊重锁定；新图替换与关闭会话时允许清除旧对象，以避免上一张图ROI泄漏到新图 | 源码路径已核对，实机长时间资源验证仍待完成 |

### 6.1 实际代码与Windows证据

- [e28051a](https://github.com/oudream/cx-visual-x3/commit/e28051a709b4cae1bdf631549605ec2c07c70fdf)：MVI八向原图几何/边界/顶点。
- [3c32577](https://github.com/oudream/cx-visual-x3/commit/3c32577ba5793da32b3db93a3a84553afa38178a)：MPW双视口控制柄+真实鼠标输入处理+TCT区域页和列表。
- [2ab29d7](https://github.com/oudream/cx-visual-x3/commit/2ab29d7a2c3fee1a837da45ea1d54824b858ee92)：真实WPF控件/区域按钮/列表绑定、原图尺寸边界、多边形、旋转镜像、隐藏及双视图反证。
- [a10c478](https://github.com/oudream/cx-visual-x3/commit/a10c478536e7a89c491f53d37d0f37cddc8fc5eb)、[9bb9242](https://github.com/oudream/cx-visual-x3/commit/9bb92424da870363b449142287aa48ded7824fe0)、[0b14df1](https://github.com/oudream/cx-visual-x3/commit/0b14df18ba73f0a2c8ba840826d1ed4d227ddd5e)：锁定、Z序、UI点击与删除保护。
- **最新最终代码Imaging专项**：[Windows FIM/MVI Imaging #40318876174](https://github.com/oudream/cx-visual-x3/actions/runs/40318876174) **SUCCESS（13步骤）**：Windows .NET48/x64全方案Build、结构/DAG、MVI数值与默认回归、STA真实WPF ImageViewer+截图/ROI编辑验证。
- **最新最终代码完整TestCenter**：[Windows TestCenter #40318876250](https://github.com/oudream/cx-visual-x3/actions/runs/40318876250) **SUCCESS（29步骤）**：真实UIA、隔离Worker、历史/并发重复与旧专业工作区兼容回归成功。
- **独立其他阻塞**：[Engineering Windows #40318876351](https://github.com/oudream/cx-visual-x3/actions/runs/40318876351) 仍为**FAIL**，日志为H01 Studio WPF宿主未找到已存在的`MPWInputStyle`资源（Studio公共主题加载/测试宿主初始化），并非ROI几何或Viewer smoke失败。该跨专业问题须在公共MPW/Studio测试Owner另行修复并复跑，**不能把完整平台所有CI称为绿色**。

### 6.2 仍未完成与后续顺序

本轮仅关闭成熟Viewer的一个P2交互包：ROI八方向外接框缩放、锁定和前后单步层叠。尚缺旋转矩形、顶点级多边形编辑/自由曲线、旋转控制柄、特定圆半径与形状约束、用户快捷键、多个对象批量选择与属性面板、统一MRC Undo/Redo和持久化。大规模Overlay对象拖动的WPF重绘压力、跨DPI、真实X-Ray高位深图、直接物理鼠标拖柄测试与工业现场均未验收。未来继续围绕唯一FGE/MVI/MPW，不复刻旧HALCON设备句柄或另建Undo/Object Store。

## 7. 2026-10-26 多边形逐顶点编辑及线性复杂度回归

本节是第6节之后的**实际代码增量与独立验收结果**。第6.2节曾列“顶点级多边形编辑”未完成；本节把**单顶点移动这一闭环**更新为已实现/已自动验收，不能因此把添加删除顶点、旋转矩形、Bezier曲线和整套P2认定为完成。

| 成熟功能对照 | 唯一正式Owner与代码 | 实际使用及测试 | 结论 |
|---|---|---|---|
| `ReferTo/CxECTImageWinForms/Models/Overlays/ImageOverlayItem.cs` 的Points/Bounds/Id/样式 | `MVIImageOverlayItem.WithMovedPolygonVertex` | 按索引编辑一个顶点，其余点和Guid、样式/图层/锁不变；重算FGERect2D；坐标钳位原图；退化、绕向反转、自交、越权、非法索引拒绝 | **已实现，MVI数值回归通过** |
| 既有`MVIImageOverlayStore.Replace` Observer | 不新增多边形状态仓库 | 每次有效顶点修改使用唯一Store快照替换并通知MPW，错误几何不落库 | **已验证** |
| 原成熟Viewer多边形编辑意图 / WPF矢量Canvas | `MPWImageViewerControl`现有`DrawOverlaysOn` + `DrawPolygonVertexHandles`、`BeginPolygonVertexEdit`、`UpdatePolygonVertexEdit` | 顶点呈金色11DIP手柄；各自Viewport做原图正逆坐标变换；左/右视图共用同一个多边形；切换工具、隐藏或锁定清掉编辑柄 | **真实WPF控件及双Pane测试通过** |
| 专业使用入口 | `MVIImageToolMode.EditPolygonVertex`、`TCTManualImageViewerLayout/Frame` | TestCenter“区域”面板“逐顶点编辑”，与对象列表及八柄缩放互斥；开发者可调用`MoveSelectedPolygonVertex`获得更新后的MVI对象 | **真实WPF按钮操作+数值/异常反证通过** |
| 多顶点工业ROI性能 | MVI只检查受编辑顶点影响的2条边，其余边拓扑不变 | 单次求交由全边两两比较`O(n²)`降至`O(n)`；新增2048顶点圆轮廓移动真值验证 | **线性算法与长轮廓功能已测试；尚无大图帧率压测** |
| 前轮对象锁/快照 | `WithVisibility`、`WithStyle` | 修复快照路径遗漏`IsLocked`，避免锁状态在新对象构造时丢失；非法编辑维持原Store状态 | **已编译/回归** |

### 7.1 实际提交、CI与异常反证

- [228be6c](https://github.com/oudream/cx-visual-x3/commit/228be6c3bdb47fd0e2cf1cf5b2c92277dc3a056f)：MVI原图多边形逐顶点几何、身份、边界及失败拒绝。该首提交有一次测试整数常量`uint`/ `int`编译错误，**在后续提交中已修复**，不可将这次失败说成绿色。
- [5d017bf](https://github.com/oudream/cx-visual-x3/commit/5d017bfda83681a36c95abc77e2ed42a5482a40a)：WPF金色顶点柄、左右视口拖动和TestCenter入口；修正编译常量。
- [3935c41](https://github.com/oudream/cx-visual-x3/commit/3935c4186972f1c2676598664a4ebf24f25b0d43)：实际WPF按钮、隐藏层、双视口3顶点投影、退化拒绝及模式往返自动化。
- **最终功能提交**：[f99f0e0](https://github.com/oudream/cx-visual-x3/commit/f99f0e0520c563e9800fdf658495ef648019f495)：线性复杂度自交检测、2048点轮廓回归。
- [Windows Imaging #40322777275](https://github.com/oudream/cx-visual-x3/actions/runs/40322777275)：**SUCCESS，13步**；现有Windows x64/.NET48解决方案构建、FIM/MVI及STA实际WPF图窗/ROI/叠加验证。
- [完整TestCenter Windows #40322777171](https://github.com/oudream/cx-visual-x3/actions/runs/40322777171)：**SUCCESS，29步**；原测试控制台、真实WPF UI Automation、NUnit/Worker、结果历史与并行重跑等回归通过。
- [Engineering #40322777244](https://github.com/oudream/cx-visual-x3/actions/runs/40322777244)：**FAIL**，仍是H01独立Studio测试宿主找不到公共`MPWInputStyle`资源，属于此前公共视觉资源集成问题；不可把本轮二维成功误写成七仓全面绿灯。

### 7.2 未闭合的P2能力及边界

**仍待完成**：在多边形边上插入顶点、删除顶点、完整自交输入修复；旋转矩形与旋转手柄；多选、样式面板；Studio唯一Undo/Redo、工程持久化；远程设备流与工业高位深/大图验证。现有Windows WPF覆盖真实控件、模式、按钮与数值状态，**不等价于Win32物理鼠标拖柄/多DPI和现场HIL验收**。复杂度降为O(n)不等于已证明大规模ROI逐帧GPU/CPU渲染时延。

下一组先复用已有FGE几何及MVI Overlay快照，把旋转矩形的几何Owner、交互控制柄、输出/测量和实际Windows测试闭环完成；不创建第二套Undo或SDK对象仓库。

## 8. 旋转矩形ROI完整角度与尺寸编辑（2026-10-26）

> **事实边界**：对照 `ReferTo/Vision/CxECTImageWinForms` 可找到现役矩形ROI绘制、ResizeRoi、图元图层、鼠标交互，但未找到可直接复用的**完整旋转矩形ROI类型及正交角点编辑机制**。本节是在成熟旧Viewer交互思想及x3现有MVI/MPW上增构，**不是宣称旧代码原样迁移**。上一节中的“旋转矩形未完成”为历史时间快照，以下按实际代码及CI更新该单项状态。

| 功能及来源 | 新平台Owner/真实类 | 操作、输入与输出 | 实际验证 |
|---|---|---|---|
| 旧Viewer矩形ROI与显示坐标编辑；新增倾斜矩形类型 | `MVIImageOverlayKind.RotatedRectangleRoi` + `MVIImageOverlayItem.CreateRotatedRectangle/WithRotatedRectangleAngle` | 四个**原图角点**而非轴对齐Bounds为几何真值；顺时针角度、真实宽高和面积；修改角度不改变图元ID/样式/面积，重新计算包围盒 | MVI数值正反例及Geometry/Owner回归通过 |
| 成熟Viewer的ResizeRoi工具思想 | `WithResizedRotatedHandle`、MPW原有`ResizeOverlay` | 局部正交XY八柄，角/边各固定对侧锚点；宽高/面积改变，保持角度与垂直性；0.25原图像素最小尺寸、图像范围约束 | MVI固定边、角点、45°倾斜及越界/锁定测试通过 |
| 旧工具互斥 + 独立MVI图像状态 | `MVIImageToolMode.RotatedRectangleRoi/RotateOverlay`、`MPWImageViewerControl` | 区域页“旋转矩形”鼠标拖动绘制初始矩形，“旋转角柄”选中对象后拖动金色圆点；对象列表显示原图角度、宽高 | TestCenter真实WPF按钮、主/副Canvas投影/状态验证通过 |
| 已有左/右视口与ROI对象Store | `DrawRotationHandle`/ `ResizeHandlePoint`/ `IsHit`，共用`MVIImageOverlayStore` | 四点Polygon真实描边，`PointInPolygon`精确命中，不以包围盒代替；双视图、浏览旋转镜像独立坐标投影；隐藏或锁定时不绘可编辑手柄 | STA WPF ImageViewer Smoke +现有回归通过 |
| SDK输入和非法几何 | `MVIImageOverlayItem`构造器/工厂与Store.Replace | 直接构造伪造非正交四角、Bounds不一致、零宽高、越界角度/尺寸均抛出异常，不污染原对象，锁定不能绕开 | ArchitectureSmoke反证通过；不引入专业SDK依赖 |
| Windows持续验收 | 既有`.github/workflows/imaging-foundation-windows.yml` | 将`MVIImageOverlayTests.cs`加到Imaging自动触发路径，避免仅修改该数值测试却不运行该专项 | 当前已核对新SHA运行 |

**本轮最终验收实证**：[Windows Imaging #40341442965](https://github.com/oudream/cx-visual-x3/actions/runs/40341442965) **SUCCESS（13步骤）**，对应功能和工作流提交 `f29ef75f7bc4a718a66f4915352f335ffac6bf49`。涵盖Windows x64/.NET48全方案编译、FIM/MVI数值及默认Smoke、实际STA/WPF图像浏览、旋转ROI状态和双Pane的合成样本验证。最早WPF断言曾因将浮点角度与45做精确比较失败；已改用1e-8容差并保留原有“锁定时拒绝变更”的反证，不用删测试掩盖问题。

**仍未完成，不能升格为用户60项全部验收**：旋转矩形真实鼠标物理输入的系统级注入/多DPI、自由曲线及曲率控制、添加/删除多边形顶点、跨工程持久化和平台Undo/Redo、强度分割及复合ROI掩膜、批量流/共享内存、实际X-Ray现场大图性能/HIL与第三方HALCON同样本结果对照。此次交付是通用二维工作台P2的**一个可操作专业几何闭环**，而非整台工作台完整交付。

### 8.1 同代码基线的TestCenter回归与跨体系风险

- [TestCenter Windows #40341200552](https://github.com/oudream/cx-visual-x3/actions/runs/40341200552) **SUCCESS（29步骤）**，对应 `5524893`，覆盖到MVI/MPW旋转矩形四角构造约束及测试中心功能回归；此后追加的数值反证和CI路径调整以 `f29ef75` Imaging专项成功作为最新代码证据，**不能将5524893的TestCenter结果自动归属更晚的master**。
- 曾有 [TestCenter #40341073706](https://github.com/oudream/cx-visual-x3/actions/runs/40341073706) 的Parallel Repeat/History阶段失败，因期望成功Attempt数为426而实际收到441；随后上述5524893结果为Success。该失败属并行用例基数/断言稳定性边界，需由TestCenter Owner继续复核，不能抹去历史失败。
- Engineering Organization Windows在部分提交仍发生Studio宿主资源`MPWInputStyle`加载失败，和旋转ROI源码不可等同；对应Master与专项SHA分别判断。

## 9. 多边形顶点插入、删除与复杂ROI编辑（2026-10-27）

> **范围与来源**：继续承接成熟 `ReferTo/Vision/CxECTImageWinForms/Halcon/HalconImageCanvas.cs` 的Polygon绘制/原图坐标与 `ImageViewerControl` / `OverlayStore` 交互思想。旧源码包含多边形创建与可视覆盖层，但此次未找到可直接复用的“在任意边插入、删除单个顶点并做拓扑校验”的现成方法。这些是**现有 MVI/MPW 能力上的增构**，不是复制另一个对象仓库；`ReferTo`全程只读。

| 目标功能 | 真实Owner/实现 | 用户入口及输出 | 验证状态 |
|---|---|---|---|
| 按边插点 | `MVIImageOverlayItem.WithInsertedPolygonVertex` | 传入零基边索引与原图 `FGEPoint2D`；新增顶点放在此边后，外接Bounds重算；保持ID、图层和样式 | 有边界、重合点、外图、穿边、自交反证 |
| 按索引删点 | `WithRemovedPolygonVertex` | 指定原顶点索引；必须保留3个顶点、原绕向/非退化/非自交，原对象不可变 | 有三角形最少点、无效索引、退化反证 |
| 不创建第二套状态 | `MVIImageOverlayStore.Replace`、MPW `InsertSelectedPolygonVertex`、`RemoveSelectedPolygonVertex`、`InsertSelectedPolygonEdgeMidpoint` | 所有修改复用已有唯一OverlayStore通知和左/右视口；隐藏、删除和锁定不准编辑 | Store事件及原快照/Guid验证 |
| 鼠标交互 | `MPWImageViewerControl.TryPolygonTopologyGesture` | 在“逐顶点编辑”模式下 **Shift＋左键点击边线插入**、**Alt＋左键点击金色顶点删除**，双视口各自用WPF投影拾取、存回原图点；错误反馈直接显示像素状态 | 代码已提交；物理键鼠和多DPI属于待验收项 |
| TestCenter实际入口 | 区域页 `PolygonVertexIndex` + `InsertPolygonVertex` / `RemovePolygonVertex` | 零基索引与“边中点插入”“删除此顶点”按钮；图像控件直接调用MPW API；列表和控制柄刷新 | 真实WPF Button.Click/数量、几何、双视图、失败不落库专项已覆盖 |
| 大轮廓性能 | 单点编辑只重新核对受影响边与其余非邻边 | 插入/删除算法拓扑验证是 `O(n)`，不在每次操作扫描所有边对 `O(n²)` | 2048顶点长轮廓插→删回环已新增；真实高频拖动/渲染耗时尚未量化 |

**代码推进记录**：`a9cabd0`（MVI插删），`47eb3a1`（数值正反例），`d6b28adc`（MPW API），`3d9477e`（双视口Shift/Alt手势），`66feb5c`、`3dd6121`（TestCenter索引按钮及提示），`07bde033`（真实WPF Button.Click及双视口回归），`bb299c2`（2048顶点长轮廓）。均为直接补强现有工程，不新增项目、不引入HALCON硬件依赖。

**已确认CI**：[Windows Imaging #40421113920](https://github.com/oudream/cx-visual-x3/actions/runs/40421113920) **SUCCESS（13步）**，对应 `07bde033`，实际全方案.NET48/x64编译、MVI测试、默认Smoke、真实WPF Imaging窗口与原图/处理图验证；**长轮廓最终代码** `bb299c2` 的 [Windows Imaging #40421287277](https://github.com/oudream/cx-visual-x3/actions/runs/40421287277) **SUCCESS（13步）**。 [TestCenter完整Windows #40421113924](https://github.com/oudream/cx-visual-x3/actions/runs/40421113924) **SUCCESS（29步）**，对应 `07bde033` 的完整NUnit/TCT、真实UIA、独立Worker、并行与历史回归。后续`397aa8c`的CSV回环与`4b1fbc3`的中途取消加固为更新的代码提交，**必须分别重新检查它们对应的Windows CI，不借用前一SHA验收**。

**未完成**：复杂多边形自交原始输入修复、自由曲线/样条/控制点、孔洞/复合ROI和像素掩膜、批量选择、平台唯一MRC Undo/Redo、工程持久化、真实系统级键鼠注入与多DPI、工业大图长时间压力。下一步优先结合旧`HalconImageCanvas`自由线/区域编辑行为评估实际差距，不把用户60项剩余功能虚报为已完成。


### 9.1 交互安全与输出闭环补强

- `Tests/TestCenter/Manual/Imaging/TCTImageViewerSmoke.cs` 增加“索引输入 → Button.Click插点 → 同Guid/四个控制柄 → CSV导出实际新增顶点 → Button.Click删点 → 双Pane返回三点”的正向闭环。CSV重用已有`ExportMeasurementsCsv`，未创建另一个导出库。
- `MPWImageViewerControl.CanContinueOverlayGesture` 在每次鼠标拖动写入前核对原图匹配、对象仍存在、可见、未锁定。逐顶点、普通及旋转ROI八柄、旋转角柄若中途失效，就清理对应手势状态并写入诊断文本，不让旧快照越权替换现有锁定对象。插点/删点只响应鼠标左键；未将键鼠物理注入测试冒充Windows验收。
- **待完成**：完整的真实Shift/Alt物理组合键事件注入、多DPI、图像重载中途绘制竞态、高密度ROI刷新性能、统一MRC Undo/Redo与工程持久化。

### 9.2 新版Windows加固验收（2026-10-27）

- [Windows Imaging #40421723586](https://github.com/oudream/cx-visual-x3/actions/runs/40421723586)：**SUCCESS（13步骤）**，对应功能提交 `4b1fbc383a8aa79f2aa8ba4145ee0d1bbee6381c`，覆盖Windows x64编译、MVI插点删点与长期轮廓回归、真实WPF图像Smoke；包括拖动过程中对象锁定/隐藏/删除时安全停止、仅左键启动插删快捷手势的源码加固。真实物理组合键注入仍未专项做。
- [Windows Imaging #40421287277](https://github.com/oudream/cx-visual-x3/actions/runs/40421287277)：**SUCCESS（13步骤）**，对应 `bb299c2`，2048顶点插删回环数值测试已落地。
- [TestCenter Windows #40421113924](https://github.com/oudream/cx-visual-x3/actions/runs/40421113924)：**SUCCESS（29步骤）**，对应 `07bde033`，包含图窗WPF区域页插删按钮及双视口回归。后续`397aa8c` CSV回环与`4b1fbc3` 拖动状态安全加固须在其对应TestCenter运行中进一步核实，不能将07b版本成功无条件覆盖到后续全平台。
- 专业扩展下一项需要核对现有 `Foundation/Geometry/Paths/FGEPath2D.cs` 和 `Mechanism/Core/VisualDefinitions/MRCShapes.cs` 中的路径/Bezier能力，优先复用现有通用几何Owner，不在Imaging重新构造Bezier基础体系。
