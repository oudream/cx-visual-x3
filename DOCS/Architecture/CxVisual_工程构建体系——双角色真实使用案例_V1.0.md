# CxVisual 工程构建体系：软件开发者与工程编排用户各一个真实使用案例 V1.0

> 2026-10-18。以 GitHub `oudream/cx-visual-x3` 最新 master 的实际源码与测试为依据。本文件不把候选 API 说成已实现。源码存在、测试已编写、Windows 真正通过与设备现场验证是不同证据级别。
>
> 两个案例采用相同工程主线：MEMProject / MEMContent / Logic Aspect / MOL 控制块 / MOC 编译 / MRK 执行 / MOC SourceMap / Studio 观察。开发者案例使用可复制的当前生产代码；工程用户案例既展示当前能操作的按钮，也提出尚未实现的整机组态用户旅程。

## A. 软件开发者：最小 X-Ray CPU 模拟检测工程（当前真实 API）

### 用户要做什么

创建一个工业检测工程，包含一个监控 Page、一个共享的主流程 Content、一个状态灯 Symbol；逻辑是「Acquire → [AI 与传统 CV 并行] → 根据外部传入 accepted 标志走 OK 或 NG」。将正式逻辑保存进 Content，关闭重开，再以现有 MOC 编译成 MRK Plan，CPU 模拟运行，读取映射回逻辑块的 Trace。

注意：这个示例没有真实图像采集与 AI 模型；`accepted` 是演示用输入而不是自动由 AI/CV 结果计算。实际系统还需专业 Provider、判定融合、类型化数据映射、设备安全和发布授权。它演示的是**真实工程构建机制能否贯通**，不声称已经实现工业 X-Ray 设备。

### 完整、独立 C# 源码

下文的 `.cs` 文件与本 Markdown 一并交付：[CxVisual_工程构建体系_开发者真实代码示例.cs](CxVisual_工程构建体系_开发者真实代码示例.cs)。使用 C# 7.3、.NET Framework 4.8，并引用当前 Core、Orchestration、Runtime 和 Foundation.Infrastructure 的程序集。可在已有的 Windows VS2022 项目中作为入口编译运行。

```csharp
// CxVisual 工程构建：真实现有 API 的 CPU 仿真闭环示例
// 目标：C# 7.3 / .NET Framework 4.8；引用现有 Foundation.Infrastructure、Mechanism.Core、
//       Mechanism.Orchestration、Mechanism.Runtime 程序集及其已有依赖。
// 此案例使用 CPU 模拟能力，不控制射线源、PLC、运动控制器或实际检测算法。
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CxVisual.Foundation.Infrastructure.Concurrency;
using CxVisual.Mechanism.Core;
using CxVisual.Mechanism.Orchestration;
using CxVisual.Mechanism.Runtime.Execution;

public static class EngineeringConstructionDemo
{
    // 真实 IMRKCapabilityResolver 契约；正式项目应从专业 Provider/能力目录解析，
    // 不要求工程用户逐个手写这个适配器。
    private sealed class CpuCapabilities : IMRKCapabilityResolver
    {
        public bool TryResolve(string id, out MRKResolvedCapability capability)
        {
            switch (id)
            {
                case "Acquire": case "AI": case "CV":
                case "OutputOK": case "OutputNG":
                    string name = id;
                    capability = new MRKResolvedCapability((context, token) =>
                        Task.FromResult((object)name));
                    return true;
                default:
                    capability = null;
                    return false;
            }
        }
    }

    public static int Main()
    {
        string file = Path.Combine(Path.GetTempPath(),
            "cx-construction-" + Guid.NewGuid().ToString("N") + ".cxproj");
        Guid flowId;

        // 1. 工程组织、可视页面、可复用组件、持久逻辑：都是一个工程 Session。
        using (var project = MEMEngineering.Create("X-Ray 在线检测 CPU 仿真"))
        {
            MEMPage page = project.AddPage("运行监控");
            MEMContent flow = project.AddContent(project.Project, "检测主流程");
            flowId = flow.Id;
            project.PlaceContent(page.RootContent, flow, "检测流程引用");

            MEMSymbol symbol = project.AddSymbol("检测状态灯");
            project.AddRectangle(symbol.RootGroup, "状态矩形", 10, 10, 120, 35);
            project.PlaceSymbol(page.RootContent, symbol, "检测状态");

            MOLLogicDefinition definition = new MOLLogicDefinition(
                MOLControlBlock.Sequence(
                    MOLControlBlock.Action("Acquire"),
                    MOLControlBlock.Parallel(
                        MOLControlBlock.Action("AI"),
                        MOLControlBlock.Action("CV")),
                    MOLControlBlock.If(MOLExpression.InputTrue("accepted"),
                        MOLControlBlock.Action("OutputOK"),
                        MOLControlBlock.Action("OutputNG"))));
            MOLLogicCodec.Set(project, flow, definition);
            project.SaveAs(file);
        }

        // 2. 关闭重开同一持久工程：固定快照编译进入原有 MRK，不重建编排图。
        using (var project = MEMEngineering.Open(file))
        {
            MEMProjectSnapshot snapshot = project.GetSnapshot();
            if (MOLLogicCodec.Get(snapshot.Find(flowId)) == null)
                throw new InvalidOperationException("保存重开后逻辑丢失");

            MOCCompiledPlan compiled = new MOCEngineeringCompiler().Compile(
                snapshot, flowId, new CpuCapabilities());
            var engine = new MRKExecutionEngine(new FCRWorkerOptions(4, 128));
            try
            {
                MRKFlowHandle run = engine.StartFlow(compiled.Plan,
                    new Dictionary<string, object> { { "accepted", true } });
                MRKFlowResult result = run.Completion.GetAwaiter().GetResult();
                if (result.State != MRKFlowState.Completed)
                    throw new InvalidOperationException("仿真流程失败", result.Error);

                MOCTraceView trace = compiled.ReadTrace(run, 0);
                if (trace.LostCount != 0)
                    throw new InvalidOperationException("Trace 缺口：应恢复完整状态快照");
                foreach (MOCObservedStep step in trace.Events)
                    Console.WriteLine(step.Origin.BlockId.ToString("N") + " / " +
                        step.Origin.Role + " : " + step.Transition.State);
                Console.WriteLine("PASS 工程 → 持久化 → 编译 → MRK → 来源追踪：" + file);
            }
            finally
            {
                engine.StopAsync(true).GetAwaiter().GetResult();
            }
        }
        return 0;
    }
}
```

### 关键类、接口、枚举及方法（均为当前真实 API）

| 角色 | 真实类型/API | 所属项目 | 具体用途 |
|---|---|---|---|
| 统一工程入口 | `MEMEngineering.Create/Open` | Mechanism.Core | 创建/打开工程；返回 `MEMEngineeringSession` |
| 工程操作 | `MEMEngineeringSession.AddPage/AddContent/AddSymbol/AddRectangle/PlaceContent/PlaceSymbol/SaveAs/GetSnapshot` | Mechanism.Core | 单工程真源、只读资源定义和持久化 |
| 对象/资源 | `MEMProject`, `MEMPage`, `MEMContent`, `MEMSymbol`, `MRCGroup`, `MRCRectangle`, `IMEMResource` | Mechanism.Core | 同一工程组织、独立资源与嵌入元素 |
| 逻辑定义 | `MOLLogicDefinition`, `MOLControlBlock.Sequence/Action/Parallel/If`, `MOLExpression.InputTrue` | Mechanism.Core | 定义顺序、并行、条件，不依赖 WPF 图元 |
| 逻辑持久化 | `MOLLogicCodec.Set/Get` + `MPMAspectKind.Logic` | Mechanism.Core | 在 Content 的 Logic Aspect 存储与恢复 |
| 控制种类 | `MOLControlKind`, `MOLBranchMode`, `MOLLoopMode`, `MOLExitKind` | Mechanism.Core | 已定义逻辑种类；某些种类目前只有定义/校验而无完整执行 |
| 能力适配 | `IMRKCapabilityResolver.TryResolve`, `MRKResolvedCapability` | Mechanism.Runtime | 动态解析真实专业能力（这里使用 CPU 模拟） |
| 编译桥接 | `MOCEngineeringCompiler.Compile` → `MOCCompiledPlan.Plan/SourceMap` | Mechanism.Orchestration | 固定工程快照编译至既有 MRK 计划；保留来源映射 |
| 执行/追踪 | `MRKExecutionEngine.StartFlow`、`MRKFlowHandle.Completion`、`MOCCompiledPlan.ReadTrace`、`MRKFlowState` | Runtime + Orchestration | 运行、检查结果，Trace 映射回 Content 与 Block |

### 从这个例子推导“期望的极简 API”（以下**均未实现**）

当前过程虽然已经贯通，但常用用户还需知道 `MOLLogicDefinition`、`MOLLogicCodec.Set`、`IMRKCapabilityResolver`、`FCRWorkerOptions` 等内部构件。期望在不改变 Owner 和数据真源的前提下提供一层薄便利入口：

```csharp
// 纯目标 API，用于讨论最少要增加哪些公共门面；当前不可直接编译。
var project = MEMEngineering.Create("检测工程");
var flow = project.AddContent(project.Project, "检测流程");
project.EditFlow(flow, f => f
    .Step("采图", "XRay.Acquire")
    .Parallel(p => p.Step("AI检测", "AI.Inspect")
                    .Step("传统检测", "Vision.Inspect"))
    .If("accepted", yes => yes.Step("OK", "Output.OK"),
                    no => no.Step("NG", "Output.NG")));
project.SaveAs("Inspection.cxproj");
var run = await buildService.RunDevelopmentAsync(project, flow, request);
```

其中 `project.EditFlow` 仅是 Core 编辑事务/Logic Aspect 的便利操作，不能偷偷创建平行 Graph；`buildService` 应属于 Orchestration/Host，不能让 Core 反向依赖 Runtime；`request` 需真实类型、输入映射与版本。能力注册与插件激活应由专业开发者做一次，普通软件开发者不需要为每个流程重新手写 Resolver。只有用实际 TDD 验证后才能将这些目标 API 宣称为已实现。

## B. 工程编排用户：C04 X-Ray 2D 在线检测完整工程（真实业务使用旅程）

### 工程用户实际目标

为一条支持多探测器、多位置、多产品在途的在线 X-Ray 设备构建、调试并部署工程。在一个工程中配置：设备与工位、产品/配方、HMI 页面、主工艺流程、图像采集子流程、检测判断子流程、算法流程、MES 通信与追溯。不能只画主流程；必须能保存、定位、仿真、验证错误路径、发布和运行监视。

### 希望用户看到的项目导航（逻辑分组，不增加第二棵工程持久树）

```text
Project：XRay.OnlineInspection
├─ 页面（Page）
│  ├─ MainHMI：设备与节拍
│  ├─ Results：图像、缺陷与复判
│  └─ Maintenance：调试与报警
├─ 内容/流程（共享 Content）
│  ├─ MainFlow：主工艺
│  ├─ AcquireFlow：采图子流程
│  ├─ InspectFlow：检测判断子流程
│  └─ AlgorithmFlow：算法编排
├─ 组件（Symbol）
│  ├─ DetectorStatus
│  ├─ StationState
│  └─ DefectView
└─ 其他工程资源/引用（按既有 Owner/索引展示）
   ├─ 设备/通道/PLC/探测器
   ├─ 变量、配方、产品、ROI、模型、MES
   └─ 样本、专业能力与测试配置
```

### 用户实际操作和对应代码接口

| 操作步骤 | 用户在 Studio 的期望操作 | 底层真实类型和方法 / 当前可用程度 | 本步骤最重要的验收 |
|---|---|---|---|
| 1 新建工程 | 新建→在线检测模板，选产线/工位 | `MEMEngineering.Create`、`MEMEngineeringSession`；新建/保存 GUI 已有，完整行业向导待开发 | 空工程/模板工程都可保存重开 |
| 2 资源规划 | 在工程树添加三个 Page、四个共享 Content、三个 Symbol | `AddPage/AddContent/AddSymbol`、`MEMProject.Resources`、`IMEMResource`；核心 API 已有，完整可分类树待增强 | 资源唯一、可独立打开和查询，不因重命名断引用 |
| 3 专业绑定 | 引入探测器、射线源、PLC、工位、变量、配方和算法 Provider | `MPMProperty/MPMBinding`、`MPCPort/MPCConnection`、`IMPMTypeProvider`；基础声明已有，真实专业 Provider/设备操作待集成 | 端口类型、版本、设备来源与权限可校验 |
| 4 编主流程 | 从能力库拖入进料、到位、采图、检测、分流、MES | `MOLLogicDefinition`、`MOLControlBlock`、`MOLLogicCodec.Set`；逻辑定义/示例画布已有，拖放式逻辑编辑未完成 | 保存重载保持执行语义而不仅是布局 |
| 5 子流程 | 选中采图逻辑→提取子流程，绑定 3 个探测器；双击进入 | `MEMContent`、`MRIResourceReference`、`ExtractReusable`、`MOLControlKind.Invoke`；资源提取与调用基础已有，任意逻辑选择提取需补齐 | 一次定义、多次调用，参数与运行实例相互独立 |
| 6 算法链 | `AcquireFlow`→预处理→`AI || CV`→融合→判断 | `MOLControlBlock.Parallel/Sequence`、`MPCPortKind.Data` 等；基础并行真实可编译，专业融合/数据契约待集成 | AI 超时不能按产品 NG 处理，融合前不得作最终判定 |
| 7 HMI 设计 | 放探测器组件、状态灯、结果查看器，绑定变量/事件 | `MEMPage/MEMSymbol/MRCVisualItem`、`PlaceSymbol/SetBinding`；基本组件实例、布局、参数覆盖已有 | 双实例可有不同参数，数据变量变更驱动显示（专业绑定未全部完成） |
| 8 检查与仿真 | 点验证、编译/仿真，单击流程块查看状态 | `GetSnapshot`、`MOCEngineeringCompiler.Compile`、`MRKExecutionEngine.StartFlow`、`MOCCompiledPlan.ReadTrace`；CPU 示例按钮已存在，完整验证向导待做 | OK、NG、设备超时、MES 故障、并行竞争五类分支可复现 |
| 9 调试/诊断 | 原流程图 Run/Skipped/Failed 覆盖、子流程下钻、来源追踪 | `MOCSourceMap/MOCStepOrigin`、`MOLFlowCanvas/MOLRuntimeOverlay`；运行叠加示例已有，完整 Watch/断点/UI 流转未完成 | 用 InstancePath、Iteration、StepId 找对“具体一次执行” |
| 10 发布/运维 | 生成固定工程版本，授权部署、运行、回滚，监控设备与产品 | `MRKExecutionPlanPublisher` 有底层授权安全点；Studio 端完整发布/生产机台能力待建设 | 旧在途任务用原 Plan/配置快照；硬件联锁由专业/PLC 保证 |

### 专业流程要具备怎样的完整性

```text
MainFlow
  进料 → 到位 → 定位 → AcquireFlow(三探测器采图)
                         └→ 校验帧/SN/工位/点位、超时、断连、重试上限
      → InspectFlow
         └→ AlgorithmFlow
               图像读取 → 校正/增强 → Parallel(AI, 传统 CV) → Merge/过滤
            → 业务判定(OK/NG/不确定/执行失败分别处理)
            → 可选复判（等待/超时/取消/结果回写）
      → OK 放行 / NG 剔除 / 故障停机或受控重试
      → 本地记录 → MES上传（幂等补传） → 完成
```

这里的采图、算法、MES 和设备动作是专业 Provider 目标，不应把 CPU 示例模拟结果称为真实检测。

### 当前 Studio 可以实际点击验证什么

`Applications/Studio/MainWindow.xaml` 当前包含“新建工程”“添加页面”“共享内容”“新建组件”“放置实例”“编辑共享定义”“添加矩形”“新增数值输入/输出”“连接端口”“应用”“撤销/重做”“保存工程/打开工程”，以及“创建 X-Ray 逻辑示例”“仿真执行并追踪”。

`MainWindow.xaml.cs` 当前的“创建 X-Ray 逻辑示例”调用 `MOLLogicCodec.Set`，将 `Acquire → Parallel(AI, CV) → If(accepted)` 写入当前页根 Content；“仿真执行并追踪”使用 `MOCEngineeringCompiler.Compile(GetSnapshot(), rootContentId, SimulationResolver)` 和 `MRKExecutionEngine.StartFlow`，把 Trace 投影到 `MOLFlowCanvas`。这是真实源码的一个最小集成路径；当前不是上述十步中所有操作都可通过拖拽完成。

### 从工程用户反推的必须补齐能力

一套编辑器要让用户自然完成「创建→拖入→连接→配置→提取→保存→校验→仿真→定位→发布」，仍需逻辑块拖放/改变结构、参数编辑、自动公开子流程端口、数据绑定检查、完整 Invocation 契约、真正的可循环 Region/Frame、专业能力目录、可视调试和发布入口。UI 修改必须进入同一个 MEM Session，不另存一套画布权威流程。实现阶段与验收应受《临时聊天3》已定 §7.4 和 C04 八界面设计约束。

## 代码/证据来源

- `Mechanism/Core/Engineering/MOLLogicDefinition.cs`：MOL 模型与 Codec。
- `Mechanism/Core/Engineering/MEMEngineeringSession.cs`：统一 Session 创建、资源、编辑、存储。
- `Mechanism/Orchestration/MOCEngineeringCompiler.cs`：快照编译、SourceMap、Trace。
- `Mechanism/Runtime/Execution/MRKExecutionEngine.cs`：已有执行引擎。
- `Applications/Studio/MainWindow.xaml(.cs)`、`MOLFlowCanvas.cs`：Studio 逻辑示例操作与画布。
- `Tests/ArchitectureSmoke/OrganizationLogicIntegrationTests.cs`：保存重开、并行分支、循环次数、子流程、Trace 映射；这里是已编写的测试代码，本轮没有 Windows .NET Framework 4.8 编译/运行验证，实际 CI 请另核对。
- `DOCS/Testing/CxVisual_逻辑编排运行调试_实现与验收_V0.1.md`：真实实现范围与明确缺口。
