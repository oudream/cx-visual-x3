# CxVisual 通用二维视觉工作台——P2 显示变换与成熟功能迁移实证 V1.0

> 日期：2026-10-26。主仓库 `oudream/cx-visual-x3`，`ReferTo` 保持只读；继续原 Foundation.Imaging / Mechanism.Visual / Mechanism.Presentation.Wpf / TestCenter 项目，不新增工程和Adapter/Wrapper。**当前仍属P2部分能力完成，不是用户60条完整验收。**
>
> 本文实证来自当前提交的源码及GitHub Actions；不将旧成熟代码的历史生产经验当作x3的设备验收。

## 1. 本轮直接承接的旧类/功能

| 原成熟源码 | 当前Owner/实际新类型 | 正式落地及真实使用路径 | 不得虚报的缺口 |
|---|---|---|---|
| `ReferTo/Vision/CxECTImageWinForms/Models/ImageDisplayState.cs`、`Halcon/ImagePresentationProcessor.cs` | MVI `MVIImageDisplayState`、FIM `FIMImageToneMapping` | 原始窗口、Gamma、亮度/对比度、反色、自动对比度；MPW订阅显示变化，TCT直接调整参数 | 需与HALCON GammaImage等同工业图像逐样本/速度对照，不是bit-exact迁移 |
| `Models/ImageTransformState.cs` | MVI `MVIImageTransformState`、现有FIMImageOperations | 0/90/180/270浏览旋转、双向镜像、边界坐标正逆投影；MPW所有ROI/叠加项/拾取共用相同映射；TestCenter可操作 | 旧任意角度旋转、亚像素ROI投影及性能未完成 |
| `Models/ImageComparisonState.cs` | MVI `MVIImageComparisonState`、FIM `FIMImageBlend` | 同位深Gray8/16、BGR/BGRA原图/结果图透明加权，保留源和Alpha；MPW真实显示，TCT设定比例 | `SideBySide` 是**未启用**模式，真正双视图、同步Pan/Zoom、滑动幕帘与配准尚缺 |
| `Controls/Visualization/HistogramPlotControl.cs`、`LineProfilePlotControl.cs` | MPW `MPWImagePlotControl` | 原生WPF双实例绘制曲线、刻度、网格；FIM给出原始灰度Histogram/LineProfile，TestCenter多标签显示 | 没有实现高级交互十字线、频谱/连通域与全场景分析 |
| `Controls/Visualization/ImageRulerControl.cs` | MPW `MPWImageRulerControl`、FIM `FIMPixelSpacing` | WPF双轴标尺绑定MVI视口；非等距mm/px、旋转/镜像方向、未标定拒绝mm；TCT能设定间距和恢复px | 未与现场相机/CT完整几何标定、畸变校正链集成 |
| `Models/ImageToolMode.cs`、`Halcon/HalconImageCanvas.cs`、`Models/Overlays/*` | MVI `MVIImageToolMode`、`MVIImageOverlayStore`、MPW WPF Canvas | 图层、框/圆/椭圆/多边形ROI、点/文字/箭头/缺陷、距离与三点角度交互；多边形双击完成，角度第三点完成，原图坐标贯通到统计/拾取 | 旋转矩形、自由曲线、完整控制柄/缩放/锁定/层排序、MRC唯一Undo/工程持久化尚缺 |

**不可混淆**：正式像素操作 `FIMImageOperations.Rotate90Clockwise` 会生成新结果图；`MVIImageTransformState.Rotate90Clockwise` **只改变显示状态**、原图像素与测量坐标不变。两者在TestCenter分别有明确按钮。

## 2. 本轮提交范围与测试

| 主题 | 相关提交及源码 |
|---|---|
| 显示 | `9514f46` MVI显示状态/FIM ToneMapping；`6faeb5f` MPW呈现接线；`3b85138` TestCenter操作；`a5590dd` WPF显示窗口反例修正 |
| 图像几何 | `ebd5e0e` MVI四向正逆映射/FIM Rotate180；`5300459` MPW源图与覆盖项统一映射；`376f5b9` TCT操作与WPF Gray16转向回环 |
| 比较 | `9b75838` MVI比较模式/FIM原始位深 Blend；`ff98f7a` MPW叠合；`4c70d3a` TCT真实混合比例/结果图回环 |
| 图形分析 | `788b18a` MPW曲线控件；`4b0969e` TCT分析页；`1949cb8` WPF曲线专项回归 |
| Polygon/Angle | `a2a92d6` MVI两工具；`b68ef1d` WPF布点绘制与角度值；`877dee2` WPF真实窗口正反例 |
| 双轴标尺 | `cb655b8` MPW标尺控件；`1c4a6d2` 与MPW旋转视口接线；`5c8699d` 新图自动清理标定；`cd55430` TCT操作；`f61e5a0` WPF标尺/镜像反证；`68f887a` 未标定显示mm防御 |

### 最近已取得的真实成功证据

- [Imaging专项 40252379651](https://github.com/oudream/cx-visual-x3/actions/runs/40252379651)：`a5590dd` 显示状态FIM数值+WPF PNG+原图未变完成。
- [Imaging专项 40252653002](https://github.com/oudream/cx-visual-x3/actions/runs/40252653002)：`376f5b9` 旋转/镜像、ROI与WPF正逆变换完成。
- [Imaging专项 40252886406](https://github.com/oudream/cx-visual-x3/actions/runs/40252886406)：`4c70d3a` 混合16位原图/结果PNG回环完成。
- [Imaging专项 40253127564](https://github.com/oudream/cx-visual-x3/actions/runs/40253127564)：`1949cb8` WPF直方图/线剖面数值传入及全解决方案编译完成。
- [Imaging专项 40253465120](https://github.com/oudream/cx-visual-x3/actions/runs/40253465120)：`877dee2` 多边形/三点角度WPF工作区、计算结果和退化反证完成。
- **物理标尺阶段**：本轮最新提交的成功/失败必须由最新提交对应CI运行证明；不能以此前绿色运行代替。

所有上述Imaging CI的验收范围是Windows x64全解决方案编译、项目结构/依赖、FIM/MVI专项和默认ArchitectureSmoke以及单独STA WPF窗口PNG与图形结果验证。它**不等于**TestCenter全部UIA、HALCON真实SDK、生产设备、百万帧持续运行、4300²大图性能或完整功能60项验收。

## 3. 正式使用入口与验证示例

```csharp
// C# .NET Framework 4.8 / WPF UI线程
MPWImageViewerControl viewer = new MPWImageViewerControl();
viewer.ShowOriginal(imageGray16);
viewer.DisplayState.SetGamma(1.8);
viewer.DisplayState.SetInverted(true);
viewer.RotateViewClockwise(); // 浏览态：原图16位数据不变
viewer.PixelSpacing = new FIMPixelSpacing(0.1, 0.12);
viewer.ShowPhysicalRulers = true;
viewer.ShowResult(processedGray16); // 同尺寸同位深
viewer.BlendOriginalAndResult(0.5);  // 严格校验
viewer.SaveViewPng("with-overlays.png");
// 分析：FIMImageAnalyzer.GetHistogram/GetLineProfile，MPWImagePlotControl展示
```

TestCenter路径：`Tests/TestCenter/Manual/Imaging/TCTManualImageViewerFrame.cs`，手工工作区 `manual.imaging`；`--imaging-viewer-smoke` 为真实WPF窗口专项测试。工程类/运行类使用原来统一Owner，不建立另一套持久图像工程、历史命令中心或数据流水线。

## 4. 接下来必须关闭的差距

1. P2：真正 `SideBySide` 多图、同步缩放/平移/配准与透明滑动比较；旧旋转矩形/多边形编辑手柄/文本与画笔/全套ROI，图层排序、源坐标几何与唯一Undo持久化；任意角旋转与亚像素。
2. P3：边缘/连通域/形状、轮廓/周长/拟合、完整测量和分析参数/输入输出、旧HALCON/OpenCV数值比对。
3. P4：文件序列、相机/网络/共享内存、异步批量背压、性能测量/缓存/零拷贝；不得把当前纯CPU逐像素路径当成高速产线指标。
4. P5/P6：设备/专业SDK、Studio/MRC配置完整闭环、工程保存重开、UIA操作截图、人机工程和实机连续运行。

**最后裁决**：成熟旧功能优先恢复，确有对象职责不妥才原位重构。采用OOP清楚Owner，GoF用于MVI可变状态与通知，AOP由现有任务日志/耗时/异常边界复用；禁止为了功能勾选量新建空类、第二工程树、重复算法或Adapter中转层。

## 5. 2026-10-26 连续开发：SideBySide 左右双视口与旧成熟功能实证

> 本节为后续实际源码与CI增量记录，**更新上方较早的“SideBySide未启用”状态快照**，不代表P2全部完成。当前代码闭环为 `9200dcd8acae3fd1c89788a538f16c257961d276`，真实Windows Imaging专项 [#40286643954](https://github.com/oudream/cx-visual-x3/actions/runs/40286643954) **SUCCESS**。

### 5.1 旧文件/行为 → 新唯一Owner → UI → 真实测试

| ReferTo 原始位置及旧行为 | 本轮x3 Owner/具体方法 | 实际操作/输入输出 | Windows验证与状态 |
|---|---|---|---|
| `CxECTImageWinForms/Models/ImageComparisonState.cs` 的 `SideBySide` / `SetSyncView` | 现有 `MVIImageComparisonState`；`MPWImageViewerControl.SideBySideOriginalAndResult` | 同一Session的原图在左、结果图在右，未向FIM写入任何投影像素 | **本轮功能闭环：已编码/已编译/已测试** |
| `CxECTImageWinForms/Models/ImageViewportState.cs` 的缩放、中心及平移状态 | 现有 `MVIImageViewport` 的 `SynchronizeFrom`，MPW内各一个视口状态 | 两个不同面板尺寸按图像坐标中心/倍率对齐；左/右鼠标滚轮与拖移可双向联动；关闭同步后分别浏览 | MVI数学正反例 + WPF双向同步、独立平移；**通过** |
| `CxECTImageWinForms/Controls/ImageViewerControl.cs:SetSyncView/RenderComparisonImages` | `MPWImageViewerControl.xaml(.cs)` 中现有控件内的 `ViewportRoot` / `SecondaryViewportRoot` / 双 `PixelImage` | 真正两个WPF视口、左原右结果、GridSplitter及状态、灰度窗口/旋转镜像沿用单一显示管线 | Windows WPF源图与结果图Gray16原始像素校验；**通过** |
| `CxECTImageWinForms/Models/Overlays/OverlayStore.cs` + 原Viewer ROI投影 | 唯一 `MVIImageOverlayStore` / `MPWImageViewerControl.DrawOverlaysOn` | 两套WPF Canvas只是同一原图坐标ROI/测量/缺陷的不同视口投影；对象ID不复制 | WPF双图Canvas对象及原Store身份验证；**通过** |
| `CxECTImageWinForms/Controls/Visualization/ImageRulerControl.cs` | 现有 `MPWImageRulerControl` 的主横纵标尺与第二横标尺 | 左/右横标尺按各自视口缩放和平移显示，当前右侧纵标尺未补齐 | 双横尺已编码；双纵尺/多DPI人工验收**待完成** |
| `ReferTo/Vision/CxECT2DViewer/MainForm.cs` 的真实图像比较/重置/保存入口 | 已有 `TCTManualImageViewerFrame` + `TCTManualImageViewerLayout` | `输出`页“左右双视图”按钮、“同步缩放/平移”复选框和“退出叠合”；嵌入控件API可独立调用 | 真正WPF按钮/复选框Click、状态/输出回环，见#40286643954；**通过** |
| `CxECTImageWinForms/Controls/ImageViewerControl.cs:ResizeRoi/SetRotation/ResetPan/SetZoomRange` | 当前MVI/MPW已有基础视口和MoveOverlay，未全部恢复旧编辑操作 | ResizeRoi手柄、任意角、精细Zoom/Pan重置等需迁移同一OverlayStore/Viewport，不建第二套体系 | **仍待完成，未虚报为P2已交付** |
| `CxECTImageWinForms/Models/ImageToolMode.cs` 旧20类工具 | 当前 `MVIImageToolMode` + MPW实际交互 | 已有矩形/圆/多边形/多类测量；旧ResizeRoi、旋转矩形/自由曲线、完整对象编辑仍缺 | **部分迁移；本次SideBySide验收不能替代ROI验收** |

### 5.2 真正的操作链及异常反证

- 正例：先加载同宽高原图与结果图，选择“输出 → 左右双视图”；检查两个WPF图像缓冲及Gray16原始数值；按滚轮/拖移同步浏览、取消同步可独立平移，再恢复同步；ROI/标注仍使用唯一MVI对象列表；可保存包含两个面板的视口截图。真实Windows窗口的 `workbench-side-by-side.png` 使用合成图生成，仅在配置截图环境变量时输出。
- 反例：缺少原图/结果、两图宽高不一致禁止启用；虚假的SideBySide枚举不会被误认为已有界面；退出模式释放第二视口的BitmapSource引用；旧图像源像素不得因为显示同步而改变。外部相机、HALCON私有HObject生命周期不在本阶段实证内。
- GoF/Owner：比较策略由既有MVI状态通知，两个MVIViewport为**浏览投影**而非两份图像数据Owner；`DrawOverlaysOn`共享唯一OverlayStore和原图坐标。AOP诊断、耗时/取消继续复用现有机制，本次未新建Facade/Adapter或图像仓库。

### 5.3 本阶段交付与剩余P2边界

代码提交：[5e3276b](https://github.com/oudream/cx-visual-x3/commit/5e3276b012660ff97b6d8fbe9dd8bed20977737d)、[bdc300e](https://github.com/oudream/cx-visual-x3/commit/bdc300e0206bf4624aadc8257ae455d0d406b060)、[9200dcd](https://github.com/oudream/cx-visual-x3/commit/9200dcd8acae3fd1c89788a538f16c257961d276)。

**下一组P2应继续**：ROI角点缩放/旋转与细调、编辑柄、图层排序锁定和统一Undo；任意角与亚像素、全双轴双视口标尺；序列与多图比较、独立图像配准及真机长时间性能。在真正CI/用户操作完成前，均保持“未完成/未验证”，不得把单个双视图成功说成60项全部交付。

## 6. 本轮最终代码验证：双视图整幅导出与TestCenter全面回归

- **本轮经过最终代码CI验证的提交**：[af86f5f54c7e](https://github.com/oudream/cx-visual-x3/commit/af86f5f54c7e1d74fcbc90ecb31b947a38059cc8)；这是功能实现SHA，后续仅文档提交不自动取代其证据身份。
- [Imaging Windows x64 #40287304124](https://github.com/oudream/cx-visual-x3/actions/runs/40287304124)：**SUCCESS，13个工作流步骤成功**。包含28项目原方案构建及结构DAG、FIM/MVI数值、默认ArchitectureSmoke、实际STA WPF按钮与Grid双图、Gray16、同步开关、ROI/Overlay、宽高不符拒绝、PNG视口导出和截图回环。
- [完整 TestCenter Windows .NET48 #40287304071](https://github.com/oudream/cx-visual-x3/actions/runs/40287304071)：**SUCCESS，29个工作流步骤成功**。包括真实WPF UIA、Connector编辑与删除、NUnit/Worker实际执行、异常/历史证据、并发重复回归，不以Imaging的局部绿灯代替。
- 同SHA专项：[Command UI #40287304081](https://github.com/oudream/cx-visual-x3/actions/runs/40287304081)、[Studio S1 #40287304096](https://github.com/oudream/cx-visual-x3/actions/runs/40287304096)、[Engineering Organization #40287304092](https://github.com/oudream/cx-visual-x3/actions/runs/40287304092)、[配置公共底座 #40287304020](https://github.com/oudream/cx-visual-x3/actions/runs/40287304020)均成功；其它专项是否通过仍按各自运行查看。
- `MPWImageViewerControl.SaveViewPng` 已改为：单图导出主Viewport、SideBySide时导出整个`ComparisonLayout`，包含左原、右结果与叠加项；真实PNG尺寸测试拒绝只截左侧的退化行为。注意`SavePng`仍保存左侧呈现像素，不等于双图组合或原始未经ToneMapping的FIM缓冲。
- 成功范围限于合成样本及Windows CI：**尚未**覆盖旧WinForms全部鼠标工具（ResizeRoi/旋转柄/层锁定/层排序）、双图完整右侧工具交互、外部配准、多图序列/设备流、真实HALCON SDK的同样本数值与HObject生命周期、4300²大图延时/资源连续压测、生产工业现场设备。它们保留P2～P6工作包，不得归档为Completed。

**后续恢复优先顺序仍按用户指定**：继续用现有MVI OverlayStore和MPW Canvas恢复旧ROI完整编辑柄/几何，随后补任意角与多图完整导航、精确区域分析与结构化算法；所有工作须用户入口+真值输出+异常反证+Windows CI。Referenced `ReferTo` 仅只读对照。
