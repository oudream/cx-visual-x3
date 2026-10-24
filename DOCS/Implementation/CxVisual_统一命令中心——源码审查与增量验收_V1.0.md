# CxVisual 统一命令中心——代码增量与验收 V1.0

> 2026-10-24；基于 GitHub master `f9f3f330e4c82007d097eb01167ca7b98b41568c` 审计及后续提交。文档、源代码、实际 Windows CI、设备实测分别分级；不以代码存在冒充全部60条功能已完成。

## 1. Owner 与现存代码

- Foundation.Data/Commands：`IFDCCommandValueBinding`、`FDCCommandValueSnapshot`，复用 `FDTDataTypeDescriptor`、`FDTDataValueSafety`，不另建一个参数数据仓库。
- Foundation.Data/Commands：`FDCCommandDefinition`、`FDCCommandParameterDefinition`、`FDCCommandDefinitionCatalog` 是静态命令定义与输入/输出契约。不能把编辑器Definition混成运行中Request。
- Mechanism.Runtime/Commands：`MRKCommandCenter`、`MRKCommandRegistry`、`MRKCommandRouter`、`IMRKCommandHandler`、`MRKCommandExecution` 是一次命令的正式运行入口/跟踪。
- Mechanism.Data：现有 MDS/变量 Owner 通过 `IFDCCommandValueBinding` 提供参数源/输出映射，不新造数据堆。
- Mechanism.Runtime.Execution：复用 `MRKExternalOperationTracker`、`MRKResourceLeaseCoordinator` 管理运行账本与资源租约，不复制流程调度器。
- ReferTo 中 `MessageInterfaces/MessageCenter`、`DeviceVariable` 仅作历史实现参考；实时消息属现有 M36 `MNMMessageCenter`，不把Command当Event广播处理。

## 2. 典型调用

```csharp
FDCCommandDefinition definition = new FDCCommandDefinition(
    1001, "Calculate", "计算",
    new[] { new FDCCommandParameterDefinition("Input", typeof(double), true) },
    new[] { new FDCCommandParameterDefinition("Output", typeof(double), true) });
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

## 6. 正式MDS绑定适配器（增量）

`Mechanism/Data/Variables/MRKCommandMdsBinding.cs` 新增生产级薄适配器，复用 `MDSVariableTable`、`IMDSDataContext`，提供 `BindVariable<T>`、`BindNode<T>`、`BindHeap<T>`、`Unbind`。地址只作命令入/出参别名；MDS中的变量、节点作用域、堆、类型与读写权限仍然由其现有Owner管理。绑定生命周期与数据Owner生命周期独立；Unbind/Dispose仅释放地址索引。

测试用正式 `MDSVariableTable` 检查进出参数映射、只读拒绝、重复地址拒绝、类型不符拒绝、解绑后不存在拒绝。节点与Heap直接使用各自 `IMDSDataContext`，仍需对应运行Session更完整的集成场景。

## 7. 结构化参数 Schema 验证（增量）

`FDCCommandParameterDefinition` 保持向后兼容的构造函数，新增长尾可选参数 `allowNull`、`readOnly`、`formatPattern`、`children`。使用同一个输入/输出描述对象，嵌套的命名Map支持递归必填、类型、数值范围、字符串Regex（限50ms）及未知成员拒绝，输出同样校验。安全快照仍由Foundation.Data统一处理，深度/容量受到限制。

`ReadOnly` 是Studio呈现元数据，不能代替目标侧认证；自定义DTO跨PC序列化以及复杂Graph Schema、数组元素Schema、数据单位转换仍属后续专门验收。新增的CommandCenterTests覆盖嵌套正常/超范围/格式/必填/未知键/结果隔离六种路径。

## 8. 同步与广播调用（进程内正式增量）

`MRKCommandCenter.Invoke` 是SDK的同步等待入口，UI线程不可调用此阻塞入口；`InvokeBroadcastAsync` 使用唯一Registry发现并逐目标创建独立请求（最多256），复用 `InvokeBatchAsync` 和各目标受理、授权、限流、超时语义。默认禁止任何设备广播，显式开启也必须逐目标通过Host授权，不允许以广播规避安全门禁。没有订阅目标时返回零项，不伪造成功目标。

### 运行关系与类图

```mermaid
classDiagram
    class FDCCommandDefinition {
      +CommandId
      +CommandCode
      +SchemaVersion
      +Inputs
      +Outputs
      +BindInputs(values)
      +ValidateOutputs(values)
    }
    class FDCCommandParameterDefinition {
      +Code
      +DataType
      +AllowNull
      +FormatPattern
      +Children
      +ValidateValue(value)
    }
    class MRKCommandCenter {
      +Register(descriptor,handler)
      +Replace(descriptor,handler)
      +InvokeAsync(request)
      +InvokeBroadcastAsync(code,inputs)
      +InvokeBoundAsync(request,binding)
      +Query(operationId)
      +Cancel(operationId)
      +GetMetrics()
    }
    class MRKCommandRegistry {
      +Register()
      +Replace()
      +Find()
      +Discover()
      +SetEnabled()
    }
    class MRKCommandExecution {
      +OperationId
      +Completion
      +Progress
      +Snapshot()
    }
    class IMRKCommandHandler {
      +ExecuteAsync(context,inputs,ct)
    }
    class IFDCCommandValueBinding {
      +ReadAsync(address)
      +WriteAsync(address,value)
    }
    class MRKCommandMdsBinding {
      +BindVariable()
      +BindNode()
      +BindHeap()
      +Unbind()
    }
    FDCCommandDefinition "1" o-- "*" FDCCommandParameterDefinition
    MRKCommandCenter --> MRKCommandRegistry
    MRKCommandRegistry --> IMRKCommandHandler
    MRKCommandRegistry --> FDCCommandDefinition
    MRKCommandCenter --> MRKCommandExecution
    MRKCommandCenter ..> IFDCCommandValueBinding
    MRKCommandMdsBinding ..|> IFDCCommandValueBinding
```

### 命令时序

```mermaid
sequenceDiagram
    participant Studio as SDK或Studio/MOL
    participant Center as MRKCommandCenter
    participant Schema as FDCCommandDefinition
    participant Registry as MRKCommandRegistry
    participant Owner as MDS绑定Owner
    participant Handler as Professional Handler
    Studio->>Center: InvokeBoundAsync(Request)
    Center->>Owner: ReadAsync(绑定参数)
    Owner-->>Center: 输入值快照
    Center->>Registry: Resolve(Target,CommandCode)
    Center->>Schema: BindInputs + Validate
    Center->>Center: 权限/并发/Operation去重
    Center-->>Studio: Submit时Accepted，Invoke时等待
    Center->>Handler: ExecuteAsync(隔离输入)
    Handler-->>Center: BusinessResult + Outputs
    Center->>Schema: ValidateOutputs
    Center->>Owner: WriteAsync(输出映射)
    Center-->>Studio: Completed/Failed/Unknown + OperationId
```

**设备场景**：`Automation` 注册轴移动Handler，固定的Definition指定位置/速度约束，Host必须注入授权和专业安全校验。不能把超时、取消视为轴停止。

**算法场景**：`Vision` 可注册图像检测/参数化计算Handler，返回缺陷/分数或受控Handle，图片或大数据不能当作普通可变对象随意深拷贝。

**流程场景**：`MOL` 运行节点编译为MRK调用目标Command，参数从节点Input/FlowHeap映射，结果通过MDS返回；不得构造第二套Graph Executor。

这些为正式API使用方式与待覆盖场景边界，未完成的专业设备/实际Studio拖拽操作不得冒称已HIL验收。

> **2026-10-24 新Owner裁决**：命令相关代码正式收敛至 `Foundation.Data.Commands`（FDC参数/定义/契约）与 `Mechanism.Runtime.Commands`（MRK中心/执行/MDS适配）两个已有项目；历史M12/MCE归属不再是当前实现。详见 `DOCS/Decisions/CxVisual_统一命令中心双项目归属裁决_V1.0.md`。
