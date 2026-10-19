# CxVisual 工程可视化工作包 A：路径精细编辑、变换与工程事务

**日期**：2026-10-19  
**实施方式**：直接维护 `master`，中文提交，不增加新项目，不改变工程对象真源。  
**目标基线**：软件开发者易用 API，工程用户 Studio 实际拖动九图元/Bezier 路径、缩放旋转、撤销重做、保存重开。工作包 A 是工程可视化体系的一部分，不代表全平台已完成。

## 一、完整调用链（真实代码）

```text
Foundation.Geometry
  Paths/FGEPath2D.cs                 原不可变Move/Line/Quadratic/Cubic/Arc/Close
  Paths/FGEPathEditing.cs            锚点/控制点精确操作（纯数学）
             ↓
Mechanism.Core
  VisualDefinitions/MRCShapes.cs     MRCShapeDefinition + MRCShapeElement
  Engineering/MTCEditSession.cs      SetShape + Resize + SetTransform 同一事务
             ↓
Mechanism.Visual
  Editing/MVIVisualEditing.cs        当前Scene世界坐标柄、命中、布局补偿、旋转缩放
  CxVisualEditor.cs                  EditHandle(...) → 既有MEM工程事务
             ↓
Mechanism.Presentation.Wpf
  MPWVisualHandleEventArgs.cs        只承载手势，不持有工程定义
  SceneView.cs                       控制柄绘制/命中、拖动、Esc、实时Bezier预览
  Renderers/MPWShapeRenderer.cs      曲线路径预览与正式绘制使用相同Geometry渲染
             ↓
Applications.Studio
  StudioViewModel.cs                 EditVisualHandle(...)
  MainWindow.xaml.cs                 HandleEditRequested → 编辑器，更新场景、Undo/Redo
```

## 二、已开发能力与边界

- **路径节点**：Move/Line/Quadratic/Cubic 的末端锚点；Quadratic 的 Control1；Cubic 的 Control1 和 Control2，按命令索引稳定定位。
- **变换**：每个可选图元有紫色缩放手柄和橙色旋转手柄；GeometryToWorld 和原父子 Transform 复用，适配 Group 内旋转图元；旋转绕图元中心不漂移。
- **拖动预览**：WPF 按住 Control 手柄时绘制完整更新后的 StreamGeometry 虚线路径，直接由精确FGEPath2D副本生成，松开才提交原MEM工程。
- **事务**：一次路径点修改可能同时影响几何包围盒、layout宽高和父坐标位置。统一 `Document.Edit` 内连续 `SetShape`、`Resize`、`SetTransform`，是一个 Revision，一个 Undo/Redo 记录。保留其他控制点的世界位置不意外漂移。
- **锁定和引用**：遵循原MEM/MTC对锁定/不可编辑的限制；Symbol实例引用应进入定义再修改控制点，不直接偷改共享定义。
- **基础图元**：完整九种Shape仍可绘制与保存；Arc/Circle/Ellipse目前是整体缩放旋转，不声称支持独立编辑旋转椭圆半径/弧起止角控制柄。
- **WPF输入**：首选已有SceneView，在当前选择上优先检测控制柄，支持鼠标移动预览、松开提交、Escape取消；预览模式不进入控制柄编辑。

## 三、开发者示例

```csharp
using (var editor = new CxVisualEditor(session))
{
    CxSceneItem selected = editor.Scene.First(s => s.Id == shape.Id);
    MVIVisualHandle control = MVIVisualEditing.Handles(selected)
        .First(h => h.Kind == MVIVisualHandleKind.Control1);
    // 所有坐标均为当前场景世界坐标，而不是屏幕截图偏移。
    MTCEditResult<bool> result = editor.EditHandle(
        control, control.WorldPoint.X + 20, control.WorldPoint.Y - 10);
    if (!result.Success) throw new InvalidOperationException(result.Message);
    editor.Undo();
    editor.Redo();
    session.SaveAs(fileName);
}
```

Studio操作：在“视觉设计”页插入Bezier/自由路径，单击图元选中后，拖动白色锚点、黄色曲线控制点；紫色缩放、橙色旋转。可用原工具栏撤销/重做、保存和重开工程。

## 四、回归验收矩阵（不可只凭类名标记完成）

| 验证 | 代码与检查 |
|---|---|
| 几何纯算法 | `VisualShapeEditingTests`：编辑只改变指定路径段，原定义不变，不合法柄拒绝 |
| 嵌套变换 | Group+Shape双层旋转，世界路径点拖动到目标坐标 |
| 路径边界补偿 | 控制点移动导致Bounds变化，但没有编辑的末端世界坐标不漂移 |
| 单次Revision | 一个世界坐标手势=一个Document.Edit，原工程Undo一次回退 |
| 缩放旋转 | 缩放使用世界坐标映射到元素局部尺寸，旋转中心固定 |
| 持久化 | 单次Undo/Redo后保存并重开，核对Bezier参数、尺寸和旋转 |
| WPF真实STA | `OrganizationWpfTests` 检查渲染树控制柄EllipseGeometry和Path预览StreamGeometry |
| Studio真实实例 | `OrganizationStudioTests` 从WPF场景中选择Bezier柄、调用UI层编辑入口，验证Undo/Redo |
| TestCenter | 新增 `TCT.MVIS.004`，真实Owner路径、Revision与重开 |

**每条验收必须核对对应HEAD的 GitHub Windows CI 和日志。** 历史成功结果不能自动代表最新提交通过。

## 五、工作包A余项（继续阶段，不夸称已全部完成）

1. Arc 起止角/圆心/半径手柄、椭圆半径、矩形圆角，以及复杂路径节点的插入/删除、切换直线/曲线、闭合/断开。
2. 画布缩放/平移、框选多选、吸附/标尺/对齐分布、图层排序与可视层管理（目前已存在部分底层机制，须做界面闭环）。
3. 参数化尺寸/旋转/坐标/路径点的真正属性编辑器，统一类型Schema。
4. 极端退化曲线、多轮廓、变换奇异性及大场景频繁绘制压力。
5. 单独对真实鼠标驱动事件的Windows GUI端到端输入验证；当前有真实WPF渲染和Studio命令链验证，但不能替代所有手势的外部UI自动化。

完成以上工作包A余项后再推进原计划B的Image/SVG/Widget完整属性、绑定、动画和事件，以及C的可视化构建运行调试统一工作台。


## 六、本轮实际Windows验收证据（2026-10-19）

本节依据真实 Actions Job 日志，不能仅凭测试类或汇报表推定成功。

- [工作包A当前源码提交 `6fd59bed` 的 Windows 工程组织/WPF验收](https://github.com/oudream/cx-visual-x3/actions/runs/39219600170)：**Success**，包含 `PASS Visual editing A`、`PASS H06 Studio real WPF automation`、`PASS H01 WPF real STA`，控制柄编辑、World稳定、Undo/Redo和工程往返均实际执行。
- [同版本 Config L1 Windows 构建和冒烟](https://github.com/oudream/cx-visual-x3/actions/runs/39219600189)：**Success**。
- [同版本配置公共底座独立验收](https://github.com/oudream/cx-visual-x3/actions/runs/39219600116)：**Success**。
- [`038f7d06` 的统一Runtime Windows TDD](https://github.com/oudream/cx-visual-x3/actions/runs/39219415122)：**Success**，包含控制柄编辑及 Visual 前述逻辑；后续仅增加UI结果反馈。
- [`c50e7b6c` TestCenter Windows .NET48](https://github.com/oudream/cx-visual-x3/actions/runs/39219037056)：**Success**，119项、失败0、错误0、跳过0，包含新的 `TCT.MVIS.004` 生产Owner控制点场景及原有用例。后续若发生Core改动，应重新以更近提交的TestCenter CI验证。
- 路径拖动涉及的近边界Arc不允许单独修改受约束的Move起点；`MVIInteractionKind.Move` 仍经过捕获、目标、冒泡并支持 `PreventDefault`；UI失败应显示真实MEM拒绝原因。

**通过范围**：本轮A的“路径/Bezier控制柄、变换、实时预览、Undo/Redo、保存重开”实现闭环；尚非A的全部编辑能力或整个平台开发完成。真实鼠标操作的独立Windows系统注入测试仍需补上。
