# CxVisual运行时数据体系 W0～W2：统一能力端口与子流程编译实施验收 V1.0

> 适用工程：`oudream/cx-visual-x3`，直接在`master`实施。本文件是《CxVisual_运行时数据体系_完整对象规格与双角色使用指南_V3.0.md》的**实际实施补充**，不是替代规格。  
> 核验规则：代码路径/接口来源于当前仓库；每次交付必须重新核对**最新master SHA对应的Windows全量构建、ArchitectureSmoke、Runtime TDD和TestCenter**，不可引用旧SHA冒充最新全部完成。
>
> **边界**：依然仅有`CxVisual.Foundation.Data`与`CxVisual.Mechanism.Data`拥有运行时数据。`Mechanism.Runtime`补编译契约/代码门面，不创建另一套数据Owner、执行器或工程序列化树；`Professional.Vision`保留算法定义。

## 一、W0基线结论（对象完整性核对）

| 核对对象 | 现行来源 | 实际裁决 |
|---|---|---|
| 旧V1.3 48项类/接口/值类型候选 | V3.0 `4，F01–F15/M01–M33逐项 | 有已实现、已被复用、保留待专业验证和明确暂缓；**不是强制创建48个类** |
| 旧V1.3 15项枚举 | V3.0 `5，E01–E15逐项 | 部分已实现，新增的`MDSBindingSource`是后期真实对象；不按15/15数量宣称完成 |
| 当前实有MDS/FDT对象 | V3.0 `3/6，实际项目cs文件 | `Get/Set/Return/Heap/Stack/File/Budget/Search/Binding`已实现，不另建`DataRef/Lease`默认公共入口 |
| MRK具名数据定义 | `MRKExecutionStep.DataOutputs`、`MRKExecutionInput`、`MRKExecutionPlan.DataExports` | 已有专业程序化实际链路；W1把Resolver、编译器、代码门面补齐到相同契约 |
| WPF编辑器 | `Applications.Studio`、既有Presentation Wpf工程 | W0～W2只做编译和开发者一致性，**不宣称真实拖连界面W3完成** |
| 专业资源 | 真实`CxVisionDataFlow`、既有CT/Automation模型 | Vision有真实CPU算法链路；工业相机/GPU/CT设备实机仍需单独验收 |

**原设计最重要的两类用户体验继续是**：R1软件开发者只需`Get/Set/Return`，高级场景才使用Heap/Move/Copy/SetFile；R3工程编排用户拖能力、看自动端口、连数据、选来源、运行检查，不画堆栈和Commit技术节点。

## 二、W1真正新增的统一端口契约

| 实际类/成员 | 现有项目和文件 | 此次实现的职责 |
|---|---|---|
| `MRKCapabilityInput` | Mechanism.Runtime / Execution / MRKExecutionCompiler.cs | 一项能力的输入端口名称、CLR类型、Required；**只声明输入定义而非输入来源** |
| `MRKResolvedCapability.Inputs`、`HasInputContract`、`ValidateInputs` | 同上 | 需要时自动检查必填端口、错误类型、未知/重复输入；**没有定义Inputs的遗留能力继续兼容** |
| `MRKResolvedCapability.DataOutputs` | 同上 | 具名`Data.Return`输出的类型合同；最终汇入已有`MRKExecutionStep.DataOutputs` |
| `MRKExecutionPlanCompiler.Compile(...,dataExports)` | 同上 | 单层编译同样可以提供最终Flow具名出口，避免只允许手写`MRKExecutionPlan` |
| `MRKFlowCompileDefinition.DataExports` | Execution / MRKExecutionComposition.cs | 子流程定义显式声明哪些`节点.输出名`对外公开 |
| `MRKExecutionOrchestrationCompiler` | 同上 | 同一能力Resolver/Plan/Engine编译，递归展开调用Gate/内部节点/`__commit`/出口屏障 |
| `MRKExecutionPlan.InlineSubflow(...,mappedInputs)` | Execution / MRKExecutionPlan.cs | 子流程FlowInput变成调用Gate的具名输出；类型、依赖、作用域检查，不偷读上层私有值 |
| `MRKCodeRuntimeBuilder.DataCapability` | Execution / MRKDeveloperRuntime.cs | 开发者注册一次委托和输入/输出端口合同，不额外写第二套Runner |
| `MRKCodeFlowBuilder.StepData/Subflow/Export` | 同上 | 代码编排简单表达数据连接、子流程调用输入和最终结果 |
| 既有`MRKExecutionPortBinder` | Execution / MRKExecutionPorts.cs | 仍是运行端口校验真源，保证来源是直接依赖、来源类型兼容、不得绕过子流程访问边界 |

### 2.1 为什么“能力输入”要独立于“调用来源”

`MRKCapabilityInput("Image", typeof(FDTReadOnlyBuffer))`说的是专业能力**需要什么**，不是说“从哪个Camera或FlowHeap来”。不同工程/实例可将同一专业能力的Image分别连接到A.Image、B.Image、工程资源或已有FlowInput。调用侧`MRKExecutionInput("Image",MRKInputSource.StepOutput,"Camera.Image",typeof(...))`才负责**真正连线**。

一份声明提供代码、Workbench、编译器一致的数据端口；不把`MRKCapabilityInput`写成Data项目中另一个`PortRepository`，也不把`MDSDataBinding`改成执行调度器。

### 2.2 数据交接路径

~~~text
R1代码编排 / R3工程编排（不同入口、同一MRK定义）
  │
  ├─ 专业能力的Input声明：MRKCapabilityInput
  ├─ 专业能力的具名输出：MRKResolvedCapability.DataOutputs
  ├─ 当前调用的来源/映射：MRKExecutionInput
  └─ Flow最终显式导出：MRKFlowCompileDefinition.DataExports
                   ↓
          原MRKExecutionOrchestrationCompiler
          ├─ 类型/必需性校验
          ├─ 子流程入口Gate（只发布显式绑定输入）
          ├─ FlowInput→StepOutput (Gate)
          ├─ 内部节点与公开出口权限
          └─ __commit → 调用屏障 → 上层最终DataExports
                   ↓
           原MRKExecutionPlan/Engine
                   ↓
          原MDSDataStack/Heap/Context
                   ↓
        已提交的MRKFlowResult.GetCommittedOutput<T>
~~~

**特别禁止**：子流程直接使用顶层FlowInput同名对象（跳过调用口）、父流程直接读内部节点Output、缺少必需Output仍返回“成功”、子流程Return直接修改父级Local。

## 三、W2软件开发者的自然调用（真实C# API）

以下示例用整数方便阅读，用相同结构替换为`FDTReadOnlyBuffer`、专业`CxVisionGrayFrame`和检测结果即可；不表示已经实现硬件采图。

~~~csharp
var builder = new MRKCodeRuntimeBuilder()
    .DataCapability("Double", (ctx, ct) =>
    {
        int value = ctx.Data.Get<int>("Input");
        ctx.Data.Return("Output", value * 2);
        return Task.FromResult((object)true);
    }, new Dictionary<string, Type> { { "Output", typeof(int) } },
       new[] { new MRKCapabilityInput("Input", typeof(int)) })
    .Flow("Child", f => f
        .StepData("calculate", "Double", new[] {
            new MRKExecutionInput("Input", MRKInputSource.FlowInput,
                "Value", typeof(int))
        })
        .Export("Result", "calculate.Output"))
    .Flow("Main", f => f
        .Subflow("child", "Child", new[] {
            new MRKExecutionInput("Value", MRKInputSource.FlowInput,
                "Request", typeof(int))
        })
        .Export("Final", "child.Result"));

MRKDevelopmentRuntime runtime = builder.Build();
try
{
    MRKFlowResult output = await runtime.RunAsync("Main",
        new Dictionary<string, object> { { "Request", 9 } });
    if (output.State == MRKFlowState.Completed)
        Console.WriteLine(output.GetCommittedOutput<int>("Final")); // 18
}
finally { await runtime.StopAsync(); }
~~~

一般业务算法仍只有两句数据操作：`Get<Input>`、`Return<Output>`，无须自己控制Heap/Stack/Frame。`DataCapability`注册专业输出类型和需要的输入；`StepData`声明当前节点绑定；`Subflow`只暴露明确的输入；`Export`声明外部可见的最终结果；`RunAsync`仍是同一个MRK Engine。

**生产使用边界**：`MRKCodeRuntimeBuilder.Build`是明确的开发/模拟宿主，不替代正式`Publisher/PlanSlot/RuntimeAccessGateway`授权发布与设备安全。正式生产仍走既有发布和安全闸门。

## 四、这次真实失败反例推动的架构修复

1. **三层输出读不到`__commit`**：初始复合编译中，调用屏障没有把子流程`__commit`声明为直接依赖；MRK端口校验拒绝。修正为把`__commit`加入调用边界依赖；不关闭依赖检查。
2. **嵌套子流程Gate作用域不正确**：外层展开时把内层入口Gate当作外层普通数据输出，导致合法内层绑定被误判越界。修正内层`$entry`的访问分组，仍禁止读内部未公开节点。
3. **没有调用映射时隐式借顶层输入**：把编排调用传入的空映射也视为**有明确调用契约**，缺少必需子FlowInput会在编译期拒绝；手动兼容`InlineSubflow(...,null)`仍属于旧底层调用入口，不作为R3自然编排范式。
4. **多余绑定被静默接受**：编排必须拒绝“调用口提供了根本不存在的内部FlowInput”；当前对显式映射集合进行使用核验。
5. **既有编辑器烟测失败**：发现移动未知视觉对象`CxVisualEditor.Move`曾抛出未捕获“Missing interaction target”，不符合原验收`MTCEditResult.Rejected`语义，修正为明确拒绝，保持工程Revision与Undo历史不变。
6. **已有来源映射循环顺序问题**：`MOCSourceMap.Find`原按编译StepId字典序展示循环迭代，可能出现0、2、1；改为按Iteration后按StepId稳定排序，保留真实来源信息。

## 五、测试证据与必须继续覆盖的反例

| 文件/测试 | 需要证明什么 |
|---|---|
| `Tests/ArchitectureSmoke/RuntimeDataCompilerUnificationTests.cs` | 三层真实数据链/两次不同Run输出隔离、漏绑定、错类型、能力输入未声明、旧StepResult兼容 |
| `Tests/TestCenter/Cases/TCTRuntimeDataCompilationCases.cs` | `TCT.MDS.028`两层真实具名输出；`029`未映射子流程不能偷根FlowInput |
| `ExecutionCompilerTests / RuntimeDeveloperExperienceTests` | 已有编译/代码入口功能不回归 |
| `RuntimeDataLifecycle/Storage/HardeningTests` | 原M1～M4的数据所有权、异常和预算不回归 |
| `RuntimeVisionDataFlowTests / TCTRuntimeVisionCases` | 8/16位真实Vision CPU Data Flow不回归 |
| `OrganizationLogicIntegrationTests` | MOL/MOC来源地址及循环Iteration映射仍稳定 |
| `Program.RejectedCommand` | 未知图元移动不崩溃，不影响Undo/Revision |

**状态和CI填报要求**：`CODED`仅表示源代码提交；`BUILT`必须对应最新SHA构建成功；`AUTOMATED_PASS`须对应测试日志；`MANUAL_PASS/HARDWARE_PASS`不得通过这些托管自动化测试虚报。W0～W2只承诺开发者/编译器与托管数据链路，W3是未来真实Workbench端到端UI。

## 六、后续剩余任务

- **W3（工程编排用户）**：在既有Studio、Presentation与CxNode工程组织中，把能力输入/输出合同用于节点自动生成，连接实际`MRKExecutionInput`，统一MDS来源选择器，形成“设计→编译→运行→Inspect”的真实操作和UI测试。不能另建Workbench私有数据仓库。
- **W4（专业反向验证）**：CT真实大文件ROI/Volume专业Adapter，IoT/PLC质量时间戳与原设备变量Owner对接；自动测试和工业设备实测分开记录。
- **W5（可靠性）**：除已有托管逻辑预算和Worker回归外，测真实峰值内存、文件IO取消、设备ACK、GPU/SHM外部操作生命周期和产线节拍；不能把`Data.Return`当作硬件安全停止证明。

**本文件不应被当作最终“所有48+15类型均已实现”或“8个R3界面用例全部通过”的证据。** 后续改变端口契约时同步更新V3.0的当前对象清单、类/接口/枚举的状态、R1/R3使用示例与T/U验收映射。
