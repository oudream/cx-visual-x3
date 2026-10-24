# CxVisual 统一命令中心——代码增量与验收 V1.0

> 2026-10-24；基于 GitHub master `f9f3f330e4c82007d097eb01167ca7b98b41568c` 审计及后续提交。文档、源代码、实际 Windows CI、设备实测分别分级；不以代码存在冒充全部60条功能已完成。

## 1. Owner 与现存代码

- Foundation.Data/Commands：`IFDCCommandValueBinding`、`FDCCommandValueSnapshot`，复用 `FDTDataTypeDescriptor`、`FDTDataValueSafety`，不另建一个参数数据仓库。
- Mechanism.Logic/Commands：`MCECommandDefinition`、`MCECommandParameterDefinition`、`MCECommandDefinitionCatalog` 是静态命令定义与输入/输出契约。不能把编辑器Definition混成运行中Request。
- Mechanism.Runtime/Commands：`MRKCommandCenter`、`MRKCommandRegistry`、`MRKCommandRouter`、`IMRKCommandHandler`、`MRKCommandExecution` 是一次命令的正式运行入口/跟踪。
- Mechanism.Data：现有 MDS/变量 Owner 通过 `IFDCCommandValueBinding` 提供参数源/输出映射，不新造数据堆。
- Mechanism.Runtime.Execution：复用 `MRKExternalOperationTracker`、`MRKResourceLeaseCoordinator` 管理运行账本与资源租约，不复制流程调度器。
- ReferTo 中 `MessageInterfaces/MessageCenter`、`DeviceVariable` 仅作历史实现参考；实时消息属现有 M36 `MNMMessageCenter`，不把Command当Event广播处理。

## 2. 典型调用

```csharp
MCECommandDefinition definition = new MCECommandDefinition(
    1001, "Calculate", "计算",
    new[] { new MCECommandParameterDefinition("Input", typeof(double), true) },
    new[] { new MCECommandParameterDefinition("Output", typeof(double), true) });
using (MRKCommandCenter center = new MRKCommandCenter())
using (center.Register(new MRKCommandDescriptor("algorithm", definition),
    new MRKDelegateCommandHandler((ctx, input, ct) =>
        Task.FromResult(new MRKCommandHandlerResult(
            new Dictionary<string, object> { { "Output", (double)input["Input"] * 2 } })))))
{
    MRKCommandResponse result = await center.InvokeAsync(
        new MRKCommandRequest("algorithm", "Calculate",
            new Dictionary<string, object> { { "Input", 5.0 } }));
    // 只有 Stage==Accepted 且 State==Completed 说明执行成功；
    // BusinessSuccess 可独立为 false；输出数据仍受快照保护。
}
```

## 3. P0现有功能与本次强化

- 命令注册、注销令牌、启停、目标/类别/能力发现、无参/多参及数组快照；
- 直接/线程池调度、进程内异步请求-响应、批量、并发限流、操作去重、取消/超时与晚到结果查询；
- Handler输出Schema验证、业务结果独立于执行状态、参数数据Owner绑定与输出映射、回调/进度；
- 新增 `Registry.Replace` / `Center.Replace`，热替换Handler不影响已受理旧调用，旧令牌不得删除新代次；
- 新增名称和命令Code发现过滤、进程内 `GetMetrics` 运行账本只读统计；
- 幂等重放返回本次调用的 `RequestId`，保留原 `OperationId`；
- 输入分别复制给拦截器和Handler，避免修改幂等比对的输入快照；输出每次读取隔离复制嵌套可变值；
- 输出绑定名称与重复目标在执行前验证，错误配置不得先触发设备副作用；
- 中心锁外触发取消回调，降低阻塞/重入风险。

## 4. 显式未完成或不得宣称

1. 真正的跨进程/跨PC transport/身份认证：`Remote=true` 当前拒绝并返回 `RemoteUnsupported`，没有隐式退回本地。
2. 完整嵌套可编辑参数树、表达式选择器、复杂DTO序列化及跨机器Schema兼容尚需和既有FCM/MDS/Studio方案结合验收；DTO必须具备值Adapter。
3. 排队调度、优先级、公平性、暂停恢复、流式中间结果、跨Host状态持久化、真正分布式幂等和状态恢复未完成。
4. 设备真实HIL、权限体系完整集成、硬件联锁和急停未做；现有目标侧授权器只是一层明确防线，不代表完整身份安全验证。
5. `InvokeBoundAsync` 的多个输出写入不是跨MDS目标事务。命令执行成功与输出写入失败应分别追踪，不能以输出映射异常认定设备动作未发生。
6. 尚未实现独立`CommandDispatcher`/`CommandExecutor`可替换接口；现有Center组合MRK运行机制，后续如需拆分应先识别独立复用价值。
7. 全部要求完成的判据仍是固定SHA Windows编译、专项 `--command-center`、默认回归、Studio实交互、设备/网络对应HIL。

## 5. 验收

`Tests/ArchitectureSmoke/Integration/CommandCenterTests.cs` 覆盖原有正反用例及新增热替换代次、名称查询、请求/操作身份隔离、参数变异隔离、可变输出保护、映射反证和Metrics。

运行：
```powershell
msbuild CxVisual.Unified.sln /m:1 /t:Rebuild /p:Configuration=Debug /p:Platform=x64
.\Output\Debug\x64\CxVisual.Tests.ArchitectureSmoke.exe --command-center
.\Output\Debug\x64\CxVisual.Tests.ArchitectureSmoke.exe
```

CI状态以对应提交的实际工作流结论为准，未核实结果不写“通过”。
