# CxVisual 工程可视化——第二轮真实WPF编辑与Widget垂直闭环

**日期**：2026-10-19  
**仓库**：`oudream/cx-visual-x3` / `master`  
**判定原则**：最终目标是软件开发者自然调用 API、工程用户在 Studio 中设计、编辑、编排、构建、运行、调试的可用系统；“第几轮”只表示进度，不是发布或完成判据。

## 1. 实际交付（源码）

| Owner | 源码 | 职责与验收 |
|---|---|---|
| Mechanism.Core | `VisualDefinitions/MRCShapes.cs`、`MRCVisualAppearance.cs`、`MRCVisualElements.cs` | 唯一持久化定义，九种 Shape、样式、文本、Widget、状态/动画/事件声明 |
| Mechanism.Visual | `CxSceneItem.cs`、`CxSceneProjection.cs`、`Catalog/MVIShapeCatalog.cs`、`CxVisualEditor.cs` | 从MEM项目快照解析曲线/文字/样式/实例地址，精确命中；九种基础图元Catalog；创建事务 |
| Mechanism.Presentation.Wpf | `Renderers/MPWShapeRenderer.cs`、`Widgets/MPWWidgetPresenter.cs`、`SceneView.cs` | StreamGeometry原生曲线与弧、样式适配；真实WPF Button/TextBox/CheckBox/Slider等Widget实例；设计/交互预览隔离 |
| Applications.Studio | `MainWindow.xaml(.cs)`、`StudioViewModel.cs` | 九种Shape工具箱、文字及按钮Widget、填充/线宽编辑、事件声明、交互预览 |
| Tests/ArchitectureSmoke | `OrganizationWpfTests.cs`、`OrganizationStudioTests.cs` | 真实Windows STA、九种Shape绘制和圆形非AABB命中；点击 Studio 工具按钮后保存重开、Undo/Redo、Widget呈现与预览事件 |

保持 `Foundation.Geometry` 无 WPF 依赖，`Core` 无 UI 实例；复用 `MEM/MTC/MPM` 作为唯一工程真源，不建立平行 Document 或 Undo 栈。

## 2. Studio用户实际流程

1. 打开 **视觉设计**，选择“直线、矩形Shape、圆形、椭圆、折线、多边形、圆弧、贝塞尔、自由路径、文字图元、按钮控件”之一。
2. 新对象进入当前活动 Page 或当前打开的 Symbol 定义；工具箱新增自动选中。使用鼠标点击与拖拽，松开后提交一次原工程事务。
3. 使用“应用填充”输入 ARGB（例如 `#FF4A90E2`）和线宽，立即更新预览并可撤销/重做。
4. 使用“绑定事件”填写 EventId、ActionId。这里保存工程行为定义，但**不擅自执行设备命令**。
5. 点击“预览交互”让真实 WPF Widget 接收输入，仅通过 `WidgetEventRequested` 向宿主发送事件；点击“返回设计”恢复编辑。
6. 保存 `.cxproj`、重新打开、检查 Shape、Style、Text、Widget、Symbol 引用和实例参数。

## 3. 开发者API

```csharp
MEMEngineeringSession session=MEMEngineering.Create("工程");
MEMPage page=session.AddPage("监控页");
MRCShapeElement lamp=session.AddShape(page.RootContent,"灯",
    MRCShapeDefinition.Circle(new FGEPoint2D(20,20),20),50,60);
session.SetVisualStyle(lamp,
    MRCVisualStyle.Solid(MRCColor.Parse("#FF27B478")));
session.SetBinding(lamp,"visible",MPMBinding.Variable("Device.Ready"));
session.SetVisualBehavior(lamp,new MRCVisualBehaviorDefinition(
    eventActions:new[]{new MRCVisualEventAction("Click","hmi.open.details")}));
session.SaveAs("monitor.cxproj");
```

`MVIShapeCatalog.CreateDefault(kind)` 提供九种可编辑图元默认几何；`CxVisualEditor.AddShape` 和 `AddWidget` 使用原MEM事务和历史。

## 4. 测试边界与明确未完成项

已编写但必须等待当前HEAD **Windows CI Success** 才能签收的测试：真实WPF Render Geometry、九种工具箱Click、编辑样式/事件Undo、保存重开、真实Widget子控件与预览事件。

不能将下列内容写成已经完成：
- Image/SVG真实资产加载与安全解析；当前定义可保存，但最终Viewer还需专业资产解码。
- 自由路径编辑锚点/控制柄、裁剪布尔、复杂几何选择工具、多人编辑冲突。
- Widget完整模板/控件种类、枚举参数自动配置及动态MDS绑定求值。
- State/Animation实际统一求值调度、事件动作权限与MRK分发、安全联锁。
- 专业HMI设备Symbol组合、Vision标注及ROI，独立3D Viewer接入。
- Studio对全部用户场景的构建、运行观察、断点、单步、性能分析闭环。
- 大规模图元空间索引、增量局部重绘与WinForms/WPF混合工控部署压力。

**运行控制边界**：Widget WPF事件不直接执行 PLC/X-Ray/运控；发布Plan、命令权限、安全互锁须走现有MOC/MRK/专业机制。编辑态数据与运行态值必须隔离。

## 5. 源码与验收管理

直接在 master 开发，中文提交；每次以对应提交SHA的 Windows CI为准。若出现其他并行开发引入的失败，报告其真实测试名称与文件，不得以“此前提交成功”代替最新验收。仅在所有必需真实CI及人工体验通过后给出阶段闭环结论。

## 6. Windows 实际验收证据（补记）

**代码验收基准：** `13237211`（该提交已经包含上述可视化功能及同期工程构建修复）。

- [工程组织 Windows Acceptance](https://github.com/oudream/cx-visual-x3/actions/runs/39207988478)：**Success**，真实 STA/WPF；日志包含 `PASS Visual M0`、`PASS H03 visual`、`PASS H06 Studio real WPF automation`、`PASS H01 WPF real STA`。
- [统一 Runtime Windows TDD](https://github.com/oudream/cx-visual-x3/actions/runs/39207988482)：**Success**。
- [Config L1 Windows 构建与冒烟](https://github.com/oudream/cx-visual-x3/actions/runs/39207988447)：**Success**。
- [配置公共底座独立编译验收](https://github.com/oudream/cx-visual-x3/actions/runs/39207988451)：**Success**。
- [TestCenter Windows .NET48](https://github.com/oudream/cx-visual-x3/actions/runs/39207573796)：**Success**，但这是较早的 `60e8a115` 提交，**不能当成当前HEAD的专项TestCenter通过**。

自动化验证覆盖真实按钮可视化和设计态/预览态事件隔离，并非真实工业设备联调。由于工程持续并行开发，后续改动必须继续检查最新 Windows CI。
