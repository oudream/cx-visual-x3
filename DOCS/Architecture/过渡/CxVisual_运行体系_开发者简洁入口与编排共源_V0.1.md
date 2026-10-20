# CxVisual 统一运行体系：开发者简洁入口与编排共源 V0.1

> 基于 C# 7.3、.NET Framework 4.8、WPF；主库 `Mechanism/Runtime`、共享 F19 Worker；本文件只说明本轮**真实可执行**的开发端入口与一致性约束。
>
> 本文不等于 Studio 视觉编排编辑器完成，不覆盖真实硬件安全、持久 Plan Repository、Checkpoint 数据快照、授权崩溃接管。本文以代码、Windows CI 和具体测试共同验收。

## 一、问题与方案

开发者以前要同时实现 `IMRKCapabilityResolver`、`IMRKFlowCompileCatalog`，亲自构造 `MRKStepCompileRequest` / `MRKExecutionPlan`、建立 Engine 才能跑简单工作。这不是统一运行体系期望的业务调用入口。

本轮提供**开发侧方便入口**：

- `MRKCodeRuntimeBuilder` 注册真实能力与子流程定义，`Flow(...)` 支持普通节点和多级子流程，`Add(MRKStepCompileRequest)` 保留现有高级策略、类型端口、条件等原有能力；
- `MRKCodeRuntimeBlueprint = builder.Freeze()`：冻结并验证**全部**流程，使用现有编排编译器；编译失败给出 `MRKCodeRuntimeBuildException.FlowId/Diagnostics`，**不启动 Worker**；
- `blueprint.BuildForDevelopment()` 返回 `MRKDevelopmentRuntime`，多流程共享同一个 `MRKExecutionEngine` 与 F19 Worker。也可直接 `builder.Build()`；
- `runtime.RunAsync("CT.Scan", request)` 自然接收一个请求对象，配合 `CapabilityForRequest<TRequest,TResult>`；返回 `Task<MRKFlowResult>`，业务**必须检查** `State`、`Error`、`Results`；
- `publisher.CompileAndStageBlueprint(principal, flowId, blueprint, revision)`：同一份 Blueprint 在正式发布端走原有 `CompileAndStageOrchestration`、授权策略、保护 PlanSlot 和安全点。**代码简化不降低生产权限门槛**。

## 二、软件开发者：简洁单流程（真实API）

```csharp
var runtime = new MRKCodeRuntimeBuilder()
    .CapabilityForRequest<int, int>("Inspect", async (request, token) =>
    {
        await Task.Delay(1, token);
        return request + 100;
    })
    .Flow("Demo.Inspect", f => f.Step("inspect", "Inspect"))
    .Build();

try
{
    MRKFlowResult result = await runtime.RunAsync("Demo.Inspect", 23);
    if (result.State != MRKFlowState.Completed)
        throw new InvalidOperationException("检测流程未成功完成", result.Error);

    int value = (int)result.Results["inspect"]; // 123
}
finally
{
    await runtime.StopAsync();
}
```

`RunAsync` 不会将执行失败虚报成成功，`MRKFlowState.Failed`、`Cancelled` 仍以返回结果表达。仅开发模式能直接使用底层取消 token/handle；生产不得将这条便捷入口当作授权发布机制。

更复杂的输入数据和高级节点契约可由 `Flow("...", f => f.Add(new MRKStepCompileRequest(...)))` 继续使用现有端口、资源、条件、执行组等接口，不在新门面里另造执行策略。

## 三、开发者：子流程 + 一次定义

```csharp
MRKCodeRuntimeBlueprint blueprint = new MRKCodeRuntimeBuilder()
    .Capability<int>("Acquire", (context, token) => Task.FromResult(5))
    .Flow("ScanPart", f => f.Step("acquire", "Acquire"))
    .Flow("CT.Scan", f => f.Subflow("scanPart", "ScanPart"))
    .Freeze();

var runtime = blueprint.BuildForDevelopment();
try
{
    MRKFlowResult result = await runtime.RunAsync("CT.Scan");
    if (result.State != MRKFlowState.Completed)
        throw new InvalidOperationException("扫描失败", result.Error);
    int acquired = (int)result.Results["scanPart.acquire"];
}
finally
{
    await runtime.StopAsync();
}
```

同一个 `blueprint` 可以交给正式 Publisher：

```csharp
// publisher 必须由可信Host提前绑定受保护PlanSlot、外部授权与安全策略。
MRKPlanPublicationReceipt receipt = publisher.CompileAndStageBlueprint(
    principal, "CT.Scan", blueprint, nextRevision);
if (!receipt.Accepted)
    throw new InvalidOperationException("编排发布编译失败");
// 只有具备授权且不存在在途Plan租约、满足Host安全点，才可能激活。
bool activated = publisher.TryActivate(principal);
```

`MRKCodeRuntimeBlueprint` 只是该编排的一次**进程内定义快照**；不是持久化 Plan Repository，不能把它误当工程文件权威。未来 CxNode/Content/逻辑 Aspect 持久工程仍是定义 Owner，按其原有身份和编译入口适配同一个 `IMRKFlowCompileCatalog`。

## 四、工程编排用户：已具备与欠缺

**已在内核具备：** DAG 依赖、执行并发、Group、条件 Guard、子流程内联、补偿与异常归档、运行日志、编译诊断、软件级发布授权和在途版本固定、运行会话及状态查询。

**尚未在 Studio 提供完整的编排操作：**

1. 从工程资源创建 Flow/子流程实例和端口；
2. 能力库搜索、节点拖拽、连线/分支/循环，数据绑定与属性面板；
3. CxNode 的权威逻辑 Aspect 编辑、保存、加载、撤销、修改检测；
4. 编译诊断在图形节点上的定位、授权发布与回滚操作；
5. 运行时可视覆盖、断点暂停/单步/恢复、可视资源和数据观察。

Studio 当前主要是工程示例与绘图/配置操作；**本文件没有交付 Flow 画布与可视界面**。这些功能应消费同一 `MRKExecutionOrchestrationCompiler`、Publisher、Runtime，而不是另建 GUI Executor。

## 五、验证与已知限制

真实代码：`Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs`、`MRKExecutionComposition.cs`、`MRKExecutionPublishing.cs`、`Applications/Runtime.Host/UnifiedRuntimeHost.cs`。

真实测试：`Tests/ArchitectureSmoke/RuntimeDeveloperExperienceTests.cs`。用例覆盖注册/复杂子流程、两个流程共享 Worker、单请求型能力、缺能力与循环拒绝、冻结快照隔离、只读主体发布被拒、正式保护Slot上的发布/激活等；Runtime.Host 使用 Blueprint 而非手写 Resolver/Catalog。

持续验收：`.github/workflows/unified-runtime-windows.yml` 完整Windows Release/x64构建、FoundationSmoke 并发、ArchitectureSmoke 以及 Host 运行、重新启动、Journal 扫描的端到端执行；另保留 TestCenter 和现有 Config 工作流。

**仍须开发**：真实 CxNode 工程编排定义适配与持久化；子流程输入/输出跨层 Schema 与可视绑定；非平凡条件循环活动图；可视编排 UI；Plan 版本持久库；可信身份/硬件安全/Checkpoint 和授权恢复。DataSpace、DataStack、大 Buffer 生命周期只由 `Foundation.Data`/`Mechanism.Data` 拥有实现。

## 六、下一批严格落地目标

1. 在 `Mechanism.Orchestration` 的边界内连接工程 `CxNode` 的权威编排定义与编辑事务，**先讨论稳定逻辑Aspect/ContentRef映射**，不挪动工程对象真源。
2. 为编排定义提供类型化参数、结果、依赖、条件循环的可验证协议；编译为**同一个** `MRKExecutionPlan`。
3. Studio 使用统一定义打开、保存和编辑编排资源；UI 验证/运行/发布只调用上述统一接口。
4. 以最少三层子流程、CT模拟、多资源并行与错误/停止/恢复边界案例做 TDD，任何未验收的生产能力不得标注完成。
