# CxVisual Symbol 体系 #9——开发者指南、Studio 手册与 G01～G08 黄金案例 V2.0

> 日期 2026-10-31；使用手册/实现指导，兼含**真实已存在 API** 与 **未实现目标步骤**。凡代码块标记“现有 API”可按当前源码核实，但本次未运行编译；凡目标步骤，不应直接当已发布 SDK。
> 上位：[总纲](CxVisual_Symbol_00_总体架构与完整需求_V2.0.md)、[#2 公开契约](CxVisual_Symbol_02_公开契约与数据行为_V2.0.md)、[#3 注册发现](CxVisual_Symbol_03_能力注册与发现_V2.0.md)、[#5 协同](CxVisual_Symbol_05_Content专业编排协同_V2.0.md)、[验收矩阵](../Testing/CxVisual_Symbol_完整测试与验收矩阵_V2.0.md)。

## 00. 一张使用路线图

| 身份 | 日常操作 | 不需要做的事 |
|---|---|---|
| 视觉作者 | 选 Group → Preview 提升 → 标注公开属性/端口 → 创建 Symbol → 放置两个实例 | 手写 Guid、复制每个内部 Shape |
| 专业开发者 | 写纯算法/Provider → 描述 Type/Capability → 经可信入口发现 → 接入 Content/Workbench | 给每个专业再写 Studio 资源/Undo/执行引擎 |
| 流程作者 | 从 Content 放置 Symbol 引用 → 绑定公共端口/变量 → 编排 → 构建/运行/Trace | 在 Symbol Group 内偷偷定义第二流程 |
| 测试工程师 | 选 G01～G08 → 正反案例 → 固定 SHA Windows/专业证据 | 把运行模拟器绿灯当设备 HIL |
| 架构维护者 | 从 #8 查源码事实与冲突 → 在 #7 确认 Owner/API → 台账/ADR | 从头发明新的 Registry |

## 01. 现有 API 使用示例 A：两个共享 Symbol 实例

**基于仓库现有 `Examples/VisualAuthoring/EngineeringVisualQuickStart.cs` 的结构和真实方法**。此例建立静态工程对象，不做设备运行。

```csharp
using System;
using CxVisual.Mechanism.Core;

public static class SymbolQuickStart
{
    public static void Create(string filePath)
    {
        using (MEMEngineeringSession project = MEMEngineering.Create("设备看板"))
        {
            MEMPage page = project.AddPage("总览");
            MEMSymbol symbol = project.AddSymbol("电机外观");
            project.AddRectangle(symbol.RootGroup, "机身", 0, 0, 120, 70);
            project.DefineProperty(symbol, new MPMPropertyDescriptor(
                "Caption", MPMValueKind.String, false, true,
                MPMBinding.Literal(MPMValue.String("未绑定"))));

            MRIResourceReference a = project.PlaceSymbol(page.RootContent, symbol, "电机A");
            MRIResourceReference b = project.PlaceSymbol(page.RootContent, symbol, "电机B");
            project.SetOverride(a, "Caption",
                MPMBinding.Literal(MPMValue.String("MOTOR-A")));
            project.SetOverride(b, "Caption",
                MPMBinding.Literal(MPMValue.String("MOTOR-B")));

            if (!project.Validate().IsValid)
                throw new InvalidOperationException("Invalid project");
            project.SaveAs(filePath);
        }
    }
}
```

**这段代码的真实语义**：
- Symbol DefinitionId 只有一份；A/B 是不同的 `MRIResourceReference`。
- `Caption` 是公开可覆写的 MPM 属性，但上面**没有**建立到 Text 元素的公开映射，所以这段代码本身不会自动在外观上显示 A/B 名字。需要支持文本映射时必须处理 #8 A07。
- 真实视觉投影/数据/Canvas 由现有 Studio/MVI 负责；不能从 `SaveAs` 推断动画/设备启动完成。

**测试**：检查公共属性、两引用 TargetId、Overrides 隔离、保存重开、Undo/Redo；故意尝试覆盖未公开属性，应收到 `org.parameter.not_public`。

## 02. 现有 API 使用示例 B：类型 Provider 与工程安装

```csharp
using System.Collections.Generic;
using CxVisual.Mechanism.Core;

public sealed class MotorTypeProvider : IMPMTypeProvider
{
    public IEnumerable<MPMTypeDescriptor> DescribeTypes()
    {
        yield return new MPMTypeDescriptor(
            "factory.hmi.motor", 1, MEMNodeKind.Symbol,
            new[] {
                new MPMPropertyDescriptor("Caption", MPMValueKind.String,
                    false, true, MPMBinding.Literal(MPMValue.String("电机")))
            },
            new[] {
                new MPCPortDescriptor("Running", MPCPortDirection.Input,
                    "boolean", MPCPortKind.State,
                    MPCPortMultiplicity.Many, null, true)
            });
    }
}

// 使用处，当前 API：
// project.RegisterTypes(new MotorTypeProvider()); // 工程写入 + Revision 改变
// MEMSymbol motor = project.AddTypedSymbol("MotorType-1", "factory.hmi.motor");
// 注意 AddTypedSymbol 自动创建 RootGroup、应用类型属性/端口/Aspect，
// 并不自动建立完整 Motor 绘图。图形需要作者/模板在 RootGroup 中创建。
```

**重要**：`RegisterTypes` 将 Provider 结果枚举后调用工程 Edit；如果只做 Studio 类型搜索，应先读取可信 Provider 的只读描述，不要为浏览而注册项目。`MPMTypeDescriptor.SameSchema` 目前**声明顺序敏感**，同 Version 的 Schema 必须按既有版本规则兼容；不能把新方案的顺序无关 Hash 当已实现。

## 03. 现有专业用例 C：HMI Faceplate

```csharp
using System;
using CxVisual.Mechanism.Core;
using CxVisual.Professional.HMI;

// 在真实项目 Session 中：
// MEMPage page = project.AddPage("生产线");
// Guid motorA = PHMFaceplateAuthoring.Place(
//     project, page.Id, PHMFaceplateKind.Motor, "MOTOR-A",
//     "PLC.MotorA.Online", 100, 80);
// Guid motorB = PHMFaceplateAuthoring.Place(
//     project, page.Id, PHMFaceplateKind.Motor, "MOTOR-B",
//     "PLC.MotorB.Online", 320, 80);
```

**可确认的源码行为**：同一类型共享一个 `HMI.Motor.Faceplate.v1` Definition；公开 `Online` 布尔属性、映射运行绿灯的 `visible`；不同实例 `SetOverride(..., MPMBinding.Variable(...))`；标签在 Page 上另建 `MRCTextElement`。这不是已实现“Motor Start/Stop PLC 命令”或“授权安全闭环”。

**扩展到 G02 的实施步骤**：
1. 保留 `Online` 与视觉基态（灰色/离线）；补 `Running/Alarm/Speed` 等确有专业数据来源与类型的公开合同。
2. 由 MDS/专业 Owner 给出 Good/Bad/Stale/Unknown、时间/采集证据，而不是把 false 当 Bad。
3. 对 Start/Stop 只发 Symbol 的请求事件；映射到 Content/专业 CapabilityId/Version，不在 WPF 事件里 new PLC Provider。
4. Software 模式模拟授权允许/拒绝、延迟/断线、Accepted vs Completed；完全可追溯后才进行 HIL。
5. 两实例 Start/Stop 确认不串设备目标；无授权时 PLC 写入调用计数必须为零。

## 04. 目标操作手册：Group 提升为 Symbol（**尚未发布**）

| 步骤 | Studio 用户动作 | 正式 Owner 期望 | 失败时 |
|---|---|---|---|
| 1 | 画布选择 Group 或多个图元 | MVI 取得受权 Scope/Selection/Revision | 选区不合法禁用 |
| 2 | “提升为 Symbol” | Core/MTI 预览包含层级、Transform、Binding/Port/Aspect/Assets | 无法读取依赖提示 |
| 3 | 查看“内部/跨边界”成员 | MPC/MPM 校验公开建议，用户明确选择 IsPublic/只读/端口方向 | 不自动暴露秘密/私有对象 |
| 4 | 设置主值、默认状态和名称 | Type/Schema 与专业约束校验 | TypeId 冲突不可覆盖 |
| 5 | 切换“复制/替换” | 对原世界几何、连接、公共映射和 IdMap 做差异预览 | 不可无损替换拒绝 |
| 6 | 确认创建 | 单次 `Edit(expectedRevision,...)` → MEMSymbol/RootGroup/MRIRef | 异常整个工程回滚 |
| 7 | 撤销、重做、保存重开 | MTC/Store/MTI 统一回归 | Revision/ID/线丢失为失败 |

**工程算法不可偷懒**：源 Group 可能有 Parent 旋转/剪切/嵌套变换，转移后任一子图元的世界位置需满足 `WorldBefore(vertex) ≈ WorldAfter(vertex)`；还要保留 Stroke/Fill/Text/Image Asset 和连接端点，而不只是矩形 BoundingBox 看似相同。

## 05. 目标交互：拖动真实轮廓连接点

- **当前**：四向 `visual.north/east/south/west` 为图元本地矩形中点，MVI 源码没有真实轮廓端点。
- **目标**：端口属性页选择 FixedSide / FixedLocal / FloatingOutline / FreeLocal，鼠标在真实圆/三角/Bezier 轮廓上选择端点；显示世界位置及局部位置/法线预览。
- **语义**：`MPCPort.Id` 是数据/事件/视觉连接的稳定身份；`MPCConnection.Id` 是连线身份；Attachment 是可视求解规则，动图元不换 ID。
- **跨定义**：实例外部只可选择正式公开 Port；私有子图元只能在其 Definition 编辑态连接。穿越多层引用时需逐层公开，不能在一条外部线里设置超过现有 `CheckEndpointScope` 许可的任意 InstancePath。
- **负例**：Path 自交/退化/单位矩阵奇异/曲线方向不稳定不能静默附着到 AABB 中心，要报告并允许用户选择合法固定点。

## 06. 目标 SDK 注册的最小易用性（**候选**）

**首选**：开发者为某专业编写一个 `RegisterSymbols(ISymbolRegistrationContext context)`，只声明 TypeId、Name、Properties/Ports、VisualDefinition 和可选关联 CapabilityId，宿主显式调用、只读归一与校验后才安装。Attribute 只是可选输入方式，必须与 MPM/MOC 同源；复杂 Native/设备 Provider 继续显式注册。

**方便并不意味着不受控**：浏览时只读取可信的静态描述；不会任意扫描 DLL/执行静态初始化；真正设备能力的运行者必须由 Runtime/Host/专业明确授权。

**最少验收三条**：
1. 用入口函数添加一个 HMI Type 和一个 Vision/Automation Type，不为每个专业改 Studio 资源中心分支。
2. 与已有 `IMPMTypeProvider/IMOCCapabilityProvider` 的 TypeId、Port Schema、CapabilityId/Version 对齐；不增加第二目录。
3. 缺 Provider/未 Effective 配置时用户仍可创作可视组件，但“运行”处显示缺依赖，不伪造 Ready。

## 07. G01～G08 的可执行演练脚本

### G01：视觉孵化金样
- 建立三角形/矩形/文本/状态灯多图元 Group，保留外部视觉线、内连线、一个公开参数；记录 Id/Transform/Connection。
- 提升 Preview 中模拟“公开映射类型不符”，应给 Blocking 且 Revision 不变。
- 成功提升、原位替换、Undo/Redo、双页面共享引用、Save/Open、MTI Export/Import，逐次比对 ID/几何/依赖。
- 由测试实际输出“同一 DefinitionId/多个 InstanceId/外部连接无损”；尚未实装时标 NotRun。

### G02：HMI Motor 金样
- 运行现有 `PHMFaceplateAuthoring.Place` 做共享定义/Online 基线。
- 补含 StartRequested 的专业 Event→Content 映射；Host 未授权拒绝，已授权但无回读标 Unknown；设备 HIL 单独签收。
- 断线、Bad/Stale、两个 Motor 不同数据源的可视状态与 Trace 必须独立。

### G03：Vision ROI / Inspection
- ROI Symbol 公开 Geometry/Enable/CoordinateFrame；专业 ROI 算法仍属于 Vision Provider，Content/MOL 组织 Acquire/Detect/Measure/Review。
- SourceId/RunId/原图大小/像素和世界坐标校准一致才可叠加；过期 Run 必须拒绝；测试无图像源时不要伪造检测结果。

### G04：CT3D
- Symbol 提供 MPR 控制/WindowLevel/刀切面工具，VolumeRef/专业 Viewer/坐标和数据租约由 CT3D Owner 处理。
- 运行态 Volume 已卸载/非法 Spacing/GPU 资源不足时可视反馈不可显示“正常三维操作完成”。
- 二维 RootGroup 不得成为第二份 Volume Scene/体素保存真源。

### G05：Automation
- 工位 Symbol 显示点位状态与请求 Start/Stop；Content/MOL 编排实际 PLC/运动命令。
- 校验设备权限/联锁/配置生效和请求幂等；超时/未知 ACK 不等于完成，不重复触发危险动作。

### G06：Simulation
- 复用相同 UI Contract 但明确 Virtual/Replay/Real 运行来源、时钟、种子/模型版本。
- 禁止“虚拟设备信号”未经批准绑定到真实 PLC Command；切换模式需重核权限和数据源。

### G07：外部组件导入
- 外部 MTI 包只读显示 Type/定义/依赖/Hash/版本；人为制造重复 ID、缺 Required Aspect/未知脚本/资源断链。
- 没有可信 Provider 的包仍可按安全策略只读/纯图形使用，但不获实际设备执行权限。

### G08：复杂嵌套与 SourceMap
- 多 Content 复用相同 Symbol，再通过合法公开成员连接一个子流程的输入输出，运行循环/并行/异常由 MRK 处理。
- 同名 Symbol 在不同子 Content InstancePath 的 SourceMap/Run/Trace 必须可区分。
- 画布变化不改变 MOL 执行依赖；禁止把装饰线当任务边。

## 08. 用例证据应该长什么样

```text
Case: G0X
Source SHA: <真实 SHA>
Owner: Core / Visual / Decision / Runtime / Professional
DefinitionId / InstancePath / TypeId / CapabilityId / MemberId:
SourceRevision / SavedConfig / EffectiveConfig / PlanVersion / RunId:
Action: 具体操作步骤
Expected: 状态与错误码、资源变化、物理安全语义
Observed: 实际值、快照、Trace、日志
Headless: Pass/Fail/NotRun + 命令
Windows UIA: Pass/Fail/NotRun + 设备/DPI/截图
Real HIL: Pass/Fail/NotRun + 设备/授权/联锁
Regression: 原有工程/旧四向/包/定义实例/Undo/版本
Open Issues: 真实阻塞与下一操作
```

**判定原则**：案例可解释、可复现、可失败、可回滚，才能作为架构成立的证据；“画出一张漂亮电机图”只是 G01 的视觉子项，不是 G02/G05 的软件与工业验收。

## 09. 复用/学习入口
- `Examples/VisualAuthoring/EngineeringVisualQuickStart.cs`：真实创建 Shape/Group/Symbol/实例和 类型化 Widget。
- `Examples/EngineeringOrganization/H03ContractsQuickStart.cs`：MPM/MPC/类型注册/实例合同。
- `Professional/HMI/PHMFaceplateAuthoring.cs`：共享 HMI Faceplate、Online、参数覆盖。
- `Mechanism/Core/Engineering/MEMEngineeringSession.cs`、`MPMTypeDescriptor.cs`、`MTCEditSession.Contracts.cs`：正式工程 API。
- `Mechanism/Visual/Editing/MVIVisualAnchors.cs`、`MVIVisualEditor.cs`：现有四向连接和编辑。
- `Applications/Studio/StudioShell.SharedResourceCenters.cs`：当前资源/类型/包的真实 UI。
- [#8 源码差异](CxVisual_Symbol_08_真实源码审计与规格冲突_V2.0.md)：审计证据和禁止误判。
