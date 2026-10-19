# CxVisual 工程可视化体系 · 工作包 A+B 完整实现与真实验收

**审查日期**：2026-10-19  
**主仓库**：`oudream/cx-visual-x3` / `master`  
**审查代码基线**：`47f34cd3adf158eb4e70aa5f16c1ba9bf79ad60c`，动态分支持续有其他Owner的提交，所有测试必须按实际提交SHA判定。  
**签收结论**：**尚未达到A+B整体最终验收**。本记录是唯一的A+B综合实现/验收入口，不以建类、提交或单次部分CI成功冒充完整交付。此前第一、二、三轮及A阶段文档保留为历史实施证据；其未实现列表需以当前源码和本文更新为准。

## 1. 不可改变的约束与Owner

- `Foundation.Geometry`：纯二维几何、精确曲线及路径变换，不依赖WPF。
- `Mechanism.Core`：MEM工程唯一真源，组织Project/Page/Content/Symbol/Group/Element及实例引用；MPM属性、绑定、行为、资源；MTC事务、Undo/Redo、保存、加载、Revision。禁止另建Document、Graph持久化库或Undo栈。
- `Mechanism.Visual`：几何编辑/场景投影/运行帧求值/事件请求。运行帧不写回工程。
- `Mechanism.Presentation.Wpf`：真实SceneView鼠标交互、路径渲染、打包图片与受限SVG、真实WPF Widget。仅显示/输入，不直接驱动设备。
- `Applications.Studio`：工程用户工具箱、属性操作、运行预览与回调；MDS数据和命令授权均由现有Owner注入。
- 工业设备逻辑/命令校验/联锁归专业和现有MRK/MOC机制；3D单独维护。
- 并行工程构建、统一运行、TestCenter、Config等已有边界不得覆盖。

## 2. 本次实际已提交源码（均直接到master，中文提交）

| SHA | 核心修改 | 归属 | 新增或强化的证据 |
|---|---|---|---|
| `6d1a9e0919a0` | 二次Bezier精确升阶为三次，Path指定轮廓闭合/断开；Studio按钮，Undo/Redo | A：Geometry/Visual/Studio | VisualShapeEditingTests、OrganizationStudioTests |
| `c764f6d62067` | WPF Widget程序更新防止伪ValueChanged；预览退出后清除残留状态；Image/SVG资源类型严格匹配 | B：Visual/WPF | OrganizationWpfTests STA反证 |
| `45a0586ed73b` | Ctrl/Shift多选取消单选回调覆盖，场景选择集合/属性面板同步 | A：SceneView/Studio | OrganizationWpfTests集合选择 |
| `ce8db7aabf7e` | 电机Symbol双实例以不同MDS变量绑定；运行值隔离、Revision不变、工程保存/重开 | B：Runtime | VisualRuntimeProjectionTests |
| `693fb1762500` | Widget事件由扁平选择ID改为完整MRIObjectAddress；宿主能辨认共享定义下不同实例，未授权不分发 | B：Visual/WPF/Studio | VisualRuntimeProjectionTests实例事件 |
| `738b34656ef7` | 修正临时测试工程Dispose时的未保存保护：测试完成显式放弃，而非绕过MEM | B：测试 | 统一Runtime、Config L1恢复通过 |
| `47f34cd3adf1` | TestCenter新增 `TCT.MVIS.006` 双实例事件、宿主授权、保存重开验证 | B：TestCenter | TestCenter Windows .NET48当前HEAD运行 |

相关实际提交：  
- https://github.com/oudream/cx-visual-x3/commit/6d1a9e0919a04afee3c046c7c461652a848825a8
- https://github.com/oudream/cx-visual-x3/commit/c764f6d620674076c5a492db0f572af5f093c034
- https://github.com/oudream/cx-visual-x3/commit/45a0586ed73b2b7fb57f8992df5e8f3687e63ef7
- https://github.com/oudream/cx-visual-x3/commit/ce8db7aabf7e91005bc0e21599082acaccbd649c
- https://github.com/oudream/cx-visual-x3/commit/693fb176250015e61802a5b742a1251946307ef0
- https://github.com/oudream/cx-visual-x3/commit/738b34656ef7c16cd5ad5a4026235863a48b7b7d
- https://github.com/oudream/cx-visual-x3/commit/47f34cd3adf158eb4e70aa5f16c1ba9bf79ad60c

## 3. 软件开发者真实调用示例

以下调用基于现有生产Core/Visual API；工程保存、公开属性和多实例引用均由MEM负责，不引入第二份内存数据仓库：

```csharp
using CxVisual.Foundation.Geometry;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Visual;

using (var document = MEMEngineering.Create("工业监控"))
{
    var page = document.AddPage("电机监控");
    var definition = document.AddSymbol("Motor");
    var lamp = document.AddShape(definition.RootGroup, "状态灯",
        MRCShapeDefinition.Circle(new FGEPoint2D(20, 20), 20), 0, 0);
    document.DefineProperty(definition,
        new MPMPropertyDescriptor("Online", MPMValueKind.Boolean, false, true,
            MPMBinding.Literal(MPMValue.Boolean(true))));
    document.ExposeProperty(definition, "Online",
        document.GetAddress(lamp), "visible");
    var motorA = document.PlaceSymbol(page.RootContent, definition, "电机A");
    var motorB = document.PlaceSymbol(page.RootContent, definition, "电机B");
    document.SetOverride(motorA, "Online", MPMBinding.Variable("Motor.A.Ready"));
    document.SetOverride(motorB, "Online", MPMBinding.Variable("Motor.B.Ready"));
    document.SaveAs("motors.cxproj");
}
// 运行帧使用已有IMDSExternalBindingResolver数据源投影，绝不能回写document。
// Widget事件经MVIVisualEventRouter.Dispatch(document, item.Address, ...,
//     authorizedHost)请求已有专业/MRK宿主授权；默认无宿主返回0。
```

已有更完整的开发者可运行示例：`Examples/VisualAuthoring/EngineeringVisualQuickStart.cs`。九形状分别通过`MRCShapeDefinition`工厂创建；编辑通过`CxVisualEditor`和原MTC事务实现。

## 4. Studio用户已提供的操作入口与边界

- 图元：九种Shape工具箱、文字、六类Widget入口、工程打包Image/SVG入口。WPF场景渲染现有形状的精确曲线而非统一矩形占位。
- 形状编辑：锚点/控制柄/Bezier拆分与段类型、路径闭合断开、Arc参数控制柄、缩放/旋转、世界坐标嵌套Group；一次手势提交一个MEM事务。
- 组织：当前Page/Symbol定义上下文、选择/多选/框选、水平/顶部对齐、水平分布、图层调整、原工程Undo/Redo。
- B属性：颜色、描边、透明度、文字大小、Widget选项/范围、MDS变量绑定、状态、动画、事件声明、预览交互。
- 运行预览：外部MDS帧只读投影、样式/位置/透明度与Widget值按配置变化，UI退出预览恢复工程定义；事件默认拒绝设备命令，只有宿主显式授权才可能执行。
- SVG是**受限子集**：基础rect/circle/ellipse/line/path/polyline/polygon与g，有限颜色、透明度、viewBox；禁止外链/脚本/DTD，拒绝未支持元素和属性；不能称为完整SVG。
- 实际鼠标外部系统注入端到端测试和完整用户工效验证尚未完成，现有WPF STA、UI Automation Peer及生产事件函数级测试**不能替代**此项。

## 5. 验收矩阵（任何行不得以源码存在视为Pass）

| 功能 | 开发者API | Studio操作 | 保存重开 | 运行表现 | 异常反证 | Windows当前证据/缺口 |
|---|---|---|---|---|---|---|
| 九Shape与Bezier拆分 | 已实装 | 已接入画布/按钮 | 已测试 | WPF精确绘制 | 无效控制柄拒绝 | ArchitectureSmoke与WPF测试，有历史Success |
| Path多轮廓闭合/断开 | 已提交 | 已接按钮 | 基于MEM事务；专项跨重开不足 | 静态绘制 | 空轮廓/重复闭合处理 | VisualShapeEditing + Studio UI测试；须跟最新HEAD继续跑 |
| Ctrl/Shift多选、视口 | 已提交 | SceneView代码入口 | 选择本身不应持久化 | 编辑态 | 不应意外触发单选 | WPF共享选择函数回归通过；外部鼠标注入待补 |
| 参数几何、图层、对齐 | 已有接口 | 部分控件 | 原MEM事务 | 编辑态 | 实例定义边界 | 仍缺全面Studio回归 |
| 资产PNG/JPEG/BMP/受限SVG | 已有 | 已有文件导入 | MEM资产包 | WPF真实解码 | MIME和SVG危险输入拒绝 | WPF STA回归曾成功；大文件/恶意SVG边界仍需加强 |
| Widget控件及属性 | 已有 | 已有六类入口 | 已有Widget回归 | WPF真实控件 | 程序更新无伪事件 | TestCenter `TCT.MVIS.005`、WPF STA |
| 数据、状态和动画 | 已有 | 已有UI配置 | 属性/声明保存 | MDS外源只读运行帧 | 绑定错误拒绝 | Runtime TDD历史Success；复杂触发/停止/Theme仍不足 |
| Symbol两实例MDS | 已提交 | Studio仍缺完整电机设计演示 | 真实工程保存重开测试 | 变量各自独立 | Revision不受刷新影响 | VisualRuntimeProjectionTests；实际完整UI还未签收 |
| Symbol内Widget授权事件 | 已提交 | 事件回调走实例地址 | TestCenter新增往返验证 | 无宿主不执行 | 引用ID歧义不执行 | `TCT.MVIS.006`新增，须匹配本次HEAD的实际CI |
| 完整A+B工业电机端到端 | 不全 | 不全 | 尚未完成真实全流程 | 不全 | 压测/故障注入不全 | **未验收** |

## 6. GitHub Windows CI：实证与历史区分

- `6d1a9e09` 对应 [TestCenter #39243665334](https://github.com/oudream/cx-visual-x3/actions/runs/39243665334) **Success**；Config L1、统一Runtime及配置独立基线也成功，但工程组织验收失败。
- `c764f6d6` 对应 [TestCenter #39244176873](https://github.com/oudream/cx-visual-x3/actions/runs/39244176873) **Success**；其统一Runtime和Config通过、工程组织失败。
- `45a0586e` 对应 [TestCenter #39244671983](https://github.com/oudream/cx-visual-x3/actions/runs/39244671983) **Success**；另外的三组Runtime/Config通过、工程组织失败。
- `693fb176` 的新增测试首次触发了MEM未保存工程保护，不能计为功能失败；`738b3465` 修正测试清理后，对应 [统一Runtime #39245958950](https://github.com/oudream/cx-visual-x3/actions/runs/39245958950) **Success**、[Config L1 #39245958974](https://github.com/oudream/cx-visual-x3/actions/runs/39245958974) **Success**。
- 当前代码基线 `47f34cd3` 的 [TestCenter #39246094395](https://github.com/oudream/cx-visual-x3/actions/runs/39246094395)、[Config L1 #39246094361](https://github.com/oudream/cx-visual-x3/actions/runs/39246094361)、[配置独立 #39246094403](https://github.com/oudream/cx-visual-x3/actions/runs/39246094403)，状态以GitHub实际完成结果为准。*此文档编写时TestCenter尚未报告完成，不得当作通过。*
- 工程组织 Windows Acceptance 多次仍失败：真正错误是 `OrganizationStudioTests` 中 G04 Studio 参数编辑链路 `moc.wiring.parameter_port_missing`，不是 Visual 几何源码的编译失败。可追踪 [#39244177019](https://github.com/oudream/cx-visual-x3/actions/runs/39244177019) 或 [#39245741137](https://github.com/oudream/cx-visual-x3/actions/runs/39245741137)。必须由工程构建 Owner对齐后在新HEAD重跑；**不得通过跳过测试取得绿色**。
- 并行提交导致各workflow在部分HEAD上触发条件不同；任何旧SHA Success只能证明对应SHA/测试范围，不得标成最新HEAD全量Success。

## 7. 当前剩余的真正签收门槛

1. **A**：Rect/Circle/Ellipse的更完善参数化控制柄、Path多轮廓极端路径/退化段/闭合连接一致性、完整选区变换与全部图层操作、视口/控制柄/Widget对齐、外部真实鼠标键盘注入的全套Studio测试。
2. **B**：Theme、完整渐变/样式继承与状态优先级、可靠动画触发/停止/重复、更多SVG安全解析边界、Widget属性类型Schema统一、图形状态与多实例工程级配置工具、MDS失联诊断界面、运行事件专业宿主完整命令权限/联锁集成。
3. **端到端**：真实电机Symbol由Studio用户可视化创建、配置两个实例、加载不同MDS、运行交互、撤销/重做、保存重开、异常注入、性能刷新稳定性，必须全部连续跑通；仅有单独API/STA测试不可签收。
4. **CI**：解决并行G04参数端口回归，确保工程组织、TestCenter、统一Runtime、Config L1、配置独立都在最终同一HEAD经过真实Windows验证；不能以近似SHA验收。

**本文件只记录真实结果，不把实现阶段当作完成结论。**
