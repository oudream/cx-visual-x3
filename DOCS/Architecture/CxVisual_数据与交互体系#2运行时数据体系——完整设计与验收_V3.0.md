# CxVisual 数据与交互体系#2运行时数据体系——完整设计与验收 V3.0

> **统一五份主设计文件之一｜2026-10-26｜Unified Data & Interaction System。**
> 本版完整保留原文的设计、对象/API、决议与历史审计；其历史时点的“当前”只对应原文标明的SHA，不能自动外推到最新master。
> **总体边界**：此处的数据特指运行时变量/堆栈，交互特指消息/命令及其跨模块协作；数据库、工程配置、日志、WPF UI、Channel/Protocol独立承担各自职责。
> 本次是文档归档与命名收敛，**没有因重命名产生新的代码实现、Windows CI或跨进程验收事实**。
> **体系主导航**：[#0 总体架构 V1.5](CxVisual_数据与交互体系%230总体架构——完整设计与验收_V1.5.md) · [#1 变量体系 V1.1](CxVisual_数据与交互体系%231变量体系——完整设计与验收_V1.1.md) · [#2 运行时数据 V3.0](CxVisual_数据与交互体系%232运行时数据体系——完整设计与验收_V3.0.md) · [#3 消息中心 V1.7](CxVisual_数据与交互体系%233消息中心——完整设计与验收_V1.7.md) · [#4 命令中心 V1.0](CxVisual_数据与交互体系%234命令中心——完整设计与验收_V1.0.md)

> **文档性质：统一设计权威入口 + 当前源码审计 + 双角色开发/编排手册。**  
> 依据：《统一数据管理体系 V1.3》（原48项类型、15项枚举、D01～D09、V01～V08、T01～T30、U01～U14）、《运行时数据体系完整设计与实施总文档 V2.0》、M1～M4及核心加固/Vision真实接入记录，以及2026-10-18读取的`cx-visual-x3/master`源码。  
> 维护方式：在V2.0基础上**完整继承并裁决**早期成果，不删除旧文档；本V3.0作为往后代码开发的首选规格，阶段实施记录继续保留真实Git SHA和CI证据。  
> **状态标签：I=源码存在并有对应测试入口；P=保留为待实现设计；R=被当前更简洁对象/已有系统吸收或替代；D=已明确暂缓、不得据此补壳。** “I”≠全部实机验收，“P”≠当前API可直接调用。  
> 审核范围：**运行时数据体系**，不把同项目中的`Configuration/*`类、所有专业算法类或MRK执行器算成MDS核心对象。历史候选和当前源码对象分别计数，不能直接相加得出“已完成类型数”。

## 0. 先回答：我们究竟要怎样的数据体系

不是为了做一套新的数据库、全局变量中心或处处传递`DataRef/Lease`的框架。它是**支撑工业工程节点/流程运行的数据组织、所有权、交接、加载、搜索和诊断机制**。

### 0.1 五句使用原则

1. **开发者：拿到`data`，直接`Get/Set/Return`。** 偶尔需要大数据/共享/缓存，再用`Heap`、`Move/Copy`或`SetFile/Load/Unload`；日常无须接触Stack和Frame。
2. **编排用户：拖能力、连端口、选来源、执行、看结果。** 绝不强迫在业务编排图中画Push、Pop、Lease、Commit、DataHeap控制节点。
3. **所有权：数据堆和数据栈各自拥有自己数据。** Local跟当前节点、成功Output跟Flow栈、显式共享/文件定位归Heap；其他消费者靠有类型的端口获得授权的安全值。
4. **多消费者默认受控只读；修改必须显式复制。** 真不可变Buffer可共享实例；可变`byte[]`防御复制；外部SDK/GPU资源不得只靠一句“共享”绕开所有权。
5. **Move/Copy都是真实数据交接。** Move所有权、Copy独立内容；名字冲突默认拒绝，显式`replace:true`才覆盖。文件Locator可以未加载，查询不得暗中加载。

### 0.2 什么是“自然”：按角色展示，而不是按底层类展示

| 提问 | 软件开发者期望 | 工程编排用户期望 | 实际负责 |
|---|---|---|---|
| 算法拿输入 | `var img = data.Get<Image>("Image");` | 将“采图.Image”拖线连到“算法.Image” | MRK端口映射 + MDS Context |
| 写过程变量 | `data.Set("Score", score);` | 在节点属性/调试器可选局部变量 | 节点Scope |
| 返回多个结果 | `data.Return("Area",area); data.Return("OK",ok);` | 两个Output自动出现，成功才整组可见 | Frame/Stack提交 |
| 一图并行消费 | 两个节点均`Get`到安全值 | Image一拖二，不配置引用计数 | 不可变共享/安全复制 |
| 想修改原图 | 显式`CopyFromHeap`或`ToArray` | “复制数据/生成新图像”专业能力 | Heap+专业图像对象 |
| 数据在堆栈之间转移 | `MoveToHeap/CopyFromHeap` | 高级属性“保留到Flow缓存/复制后修改” | MDS拥有者 |
| CT还没读取 | `SetFile`→`LoadAsync`→`Unload` | 展示“未加载”，点击“加载/ROI预览” | Heap+专业Loader |
| 想找数据 | `data.Search/Inspect` | 数据选择器按类型/来源过滤 | MDS元数据查询 |
| 出错怎么知道 | 带错误码和端口名称 | 标红具体节点/端口，显示原因 | MDS Exception + MRK |
| 运行完成怎么拿结果 | `MRKFlowResult.GetCommittedOutput<T>` | 结果面板展示Final outputs | 现有MRK Flow |

**API展现原则**：基础操作一两行完成；高级能力逐级展开；类型校验在连接和执行前；默认路径不出现“技术概念暴露税”。

## 1. 业务角色要求与验收阈值

### 1.1 R1 软件开发者——以前讨论过的完整期望

- 常规算法编写：`Get`/`Set`/`Return`三种动词足够，输入由运行宿主注入；不手动构建`Heap、Stack、Scope、Frame`。
- 同一种业务对象在同步算法、异步算法、嵌套子流程中采用**同一数据语义**，不能设计两套API。
- `Get<T>`无值和类型错必须区别，`TryGet<T>`仅表示缺失；`Return`是暂存输出，不自动告诉MRK“业务成功”。
- 标量与真不可变对象直接用；专业可变DTO须明确深复制/安全冻结；任何“复制后还共享底层可写数组”的实现都不算通过。
- 需要保留/转移才使用高级Heap方法；高级用户可以配置预算、专业Loader、类型适配器，但不影响三动词易用性。
- 普通C#函数可以直接包装为节点业务回调；`MDSDataFunctions.Invoke/InvokeAsync`不能偷偷建立新的执行器。
- 取消、超时、重试和外部设备的未知结果不能导致旧节点数据写入新Run；Flow结束后不持有失效上下文。
- 诊断时先看元数据，不自动复制一张4300×4300图或读取整8GB CT卷。
- 提供可执行示例和错误反例，避免只有抽象签名没有开发路径。

### 1.2 R3 工程编排用户——以前讨论过的完整期望

- “工程资源/能力目录”中选专业能力；拖到流程图即生成真实输入/输出端口（源于能力/计划契约，不创建影子Port Schema）。
- 连接线就是数据映射，能检查类型/必填/可见域/节点依赖；无需关心`DataRef`。
- 所有输入参数采用统一选择器：**常量 / 当前可见值 / 已连接节点输出 / 共享Heap / 设备变量 / 工程资源 / 授权表达式**。
- 子流程视作普通节点，双击进入专业子流程；每层内部有独立Scope，出口必须显式映射，不用画Commit。
- 查看运行数据时选Run→节点→Input/Local/Pending/Committed/FlowHeap；显示名称、类型、大小、状态、来源、错误；大数据默认只展示元数据。
- 同一专业子流程重复调用、同一Content在不同产线实例化、同名局部变量并发时绝不能串数据。
- 文件定位可显示`Unloaded/Loading/Loaded/Failed`，实际打开需用户命令或专业ROI能力。
- 普通工程用户看到的是“保留缓存”“生成副本”“释放已加载数据”等业务动作，而不是裸`Lease`/堆地址/GC术语。
- 高级“诊断/性能”可显示预算剩余、Copy次数和耗时；但权限保护和生产只读模式优先。

### 1.3 R2/R4/R5/R6简表

| 角色 | 关键关注点 | 必须提供的证据 |
|---|---|---|
| R2 平台机制开发者 | 类型/Owner/并发/原子性/预算/Retry | 跨Run隔离、失败不半提交、源目标不被损坏 |
| R4 专业能力开发者 | Image/Volume/Tensor、SDK所有权和高性能读写 | 真实Vision/CT样例、适配器审查、峰值内存 |
| R5 测试与运维 | SN/Run/Scope追踪、数据查询、文件状态 | TestCenter/Smoke日志、定位到具体端口 |
| R6 架构与维护者 | 两项目Owner、没有影子管理器、迭代兼容 | 项目依赖审查、中文Commit、真实Windows CI |

## 2. 总体组织、运行链条与边界

~~~text
工程模型 (Project / Page / Content / Symbol / Group / Element)
           │  专业能力定义、Input/Output契约、绑定
           ▼
编排/编译 → 现有 MRKExecutionPlan/Step/Ports (唯一执行事实)
           │ 每个Run独立环境，每个Node注入IMDSDataContext
           ▼
Mechanism.Data 的 MDSDataStack
 ├── MDEExecutionDataScope → 每次调用Frame
 │    ├── Input（已绑定，只读）
 │    ├── Local（Set，当前节点）
 │    └── PendingOutput（Return，直到成功整体发布）
 ├── CommittedOutput（Flow栈拥有，Flow寿命）
 ├── FlowHeap（显式保留/Move/Copy/文件定位+缓存）
 ├── MDSDataBudget（托管有效载荷逻辑持有预算）
 └── Search/Inspect/Binding/Loader（从真实Owner投影）
           ▲
Foundation.Data (FDT类型描述、Key、Buffer、Adapter、Size、安全捕获)
           ▲
Professional.* (Vision/CT/Automation/IoT专业类型、算法与设备Owner)
~~~

### 2.1 只能有两个Data Owner项目

- `CxVisual.Foundation.Data`：**数据本身的基础描述/安全计算规则**，其中`Types/*`管理类型、安全捕获、只读Buffer、Adapter、DataKey、尺寸估算。原有`Configuration/*`属于配置体系，不是本体系要重新命名和统计的对象。
- `CxVisual.Mechanism.Data`：**运行中的数据Owner和机制**，`RuntimeData/*`管理Heap/Stack/Frame/Context/预算/查询/加载/绑定；`Execution/MDEExecutionDataScope`保留既有Scope契约，不另建新Scope树。
- `Mechanism.Runtime`：只负责编排已编译计划的运行/调度、端口/依赖/提交触发、结果状态、异常策略；少量接入变更允许，但**不持有第二个DataHeap**。
- `Professional.Vision/CT3D/Automation`：专业数据的对象语义、算法、Loader/Adapter/设备真实操作；机制Data**不**实现阈值检测、CT重建或设备通信。
- `Configuration`、现有变量与工程资源Owner保持唯一真源；`MDSDataBinding`只是“引用哪个来源”的表达，不是新数据库。

### 2.2 数据的六种使用位置

| 位置 | 谁拥有 | 开发者访问 | 工程用户看到 | 终止/释放 |
|---|---|---|---|---|
| FlowInput | 宿主输入，经绑定复制/冻结为节点Input | `Get` | 流程入口参数 | 节点结束 |
| Node Input | 当前调用安全输入 | `Get`，只读 | 入端口 | 当前节点 |
| Node Local | Scope | `Set/Get` | 当前节点局部 | 当前节点 |
| PendingOutput | Frame | `Return`，本节点检查 | 未提交、不能被下游访问 | 成功提交或Abort |
| CommittedOutput | DataStack/Flow | 下游显式绑定后`Get`；最终Flow结果可读 | 已完成节点输出 | Flow结束后释放持有 |
| FlowHeap | Heap | `HeapSet/HeapGet/Move/Copy/SetFile...` | 显式共享缓存/文件定位 | Remove/Unload/Flow结束 |

注：当前`MDSDataQuery.Location`虽然有`CommittedOutput`成员，**`data.Search()`当前只聚合Input/Local/Pending/FlowHeap，尚未把全部已提交Output直接纳入搜索目录**。这是M5 Inspector需要实现的桥接，不应将enum成员解释为完整支持。元数据Query不允许凭名称读到其它节点私有输出。

## 3. 当前源码：完整类型/接口/枚举清单（按Owner和实际形态）

> 本节只列**在本次master源码中实际存在**的运行时数据相关声明。每一项列出用途、主要属性/方法、可见级别、使用者；专业对象和MRK依赖另外说明。文件路径在本节标题所指目录下。

### 3.1 `Foundation/Data/Types`：10项已实现声明

| 编号 | 实际声明及形态 | 关键成员/语义 | 对谁开放 |
|---|---|---|---|
| F-A01 | `FDTDataKind` 枚举 | `Scalar,Buffer,Object,List,Map,Image,Volume,Tensor,Stream` | 公共类型分类 |
| F-A02 | `FDTDataTypeDescriptor` sealed class | `ClrType,Kind,IsSafelyCopyable,IsNullable; Describe(type,adapters)` | 公共 |
| F-A03 | `FDTDataValueSafety` static class | `IsSimpleImmutable, Capture(value,adapters)`；未知可变值拒绝 | 内核/高级开发 |
| F-A04 | `FDTReadOnlyBuffer` sealed class | `Length, this[int], ToArray(), CopyTo()`；入参捕获私有字节副本 | 公共，常规安全共享 |
| F-A05 | `IFDTDataAdapter` interface | `ValueType,IsImmutable,Kind,Capture`；专业复制/冻结契约 | 专业 |
| F-A06 | `IFDTDataSizeEstimator` interface | `TryEstimateBytes(object,out long)` | 专业 |
| F-A07 | `FDTDataAdapter<T>` sealed class | `Capture`、`TryEstimateBytes`，复制器/不可变承诺/Kind/估算器 | 专业 |
| F-A08 | `FDTDataAdapterRegistry` sealed class | `Register<T>, Register, Freeze, TryCapture, TryDescribe, TryEstimateBytes, Supports`；按Flow/Heap注册 | 高级/内核 |
| F-A09 | `FDTDataKey<T>` sealed class | `Name,ValueType`；防误拼的强类型Slot名 | 公共 |
| F-A10 | `FDTDataSize` static class | `TryEstimateBytes`（托管有效载荷估算，非进程真实RSS） | 内核 |

基础Execution兼容契约：`Foundation/Data/Execution/IFDEExecutionDataScope`已存在，`Id,Parent,IsClosed,CreateChild,Put,PutOwned,TryRead,SnapshotLocal`等接口服务于旧运行作用域；本体系复用，不再实现另一套作用域基础接口。此项单列，不掺入上述Types“10项”数量。

### 3.2 `Mechanism/Data/RuntimeData`：当前所有核心声明（含内部类）

| 编号 | 实际声明 | 形态/主要成员 | 使用层 |
|---|---|---|---|
| M-A01 | `IMDSDataContext` | interface；`Get/TryGet/Set/Return`、强类型键、Heap/Move/Copy、`SetFile/LoadAsync/Unload/Search/Inspect` | **R1首要入口** |
| M-A02 | `MDSDataContext` | sealed class；当前节点Scope、显式Input、Pending及Heap关联，`PrepareRetry/AbortOutputs` | 内核构建，业务经接口 |
| M-A03 | `MDSDataHeap` | sealed partial class；`Set/Get/Replace/Remove/Contains/MoveTo/CopyTo/SetFile/LoadAsync/Unload/Query/Inspect`；`Budget,Adapters,Count,RunId` | 高级/Owner |
| M-A04 | `MDSDataEntry` | Heap内private sealed class；`Value,Type,Bytes`，实际托管条目 | **内部**，不新建公共类 |
| M-A05 | `MDSDataStack` | sealed class；`RootScope,FlowHeap,Budget,OpenFrame,TryReadOutput,ActiveFrames` | Runtime/高级 |
| M-A06 | `MDSDataFrame` | sealed class；`InvocationId,Data,CommitOutputs,AbortOutputs,Dispose` | Runtime |
| M-A07 | `MDSDataSafety` | internal static class；`Capture,Snapshot` | 内部 |
| M-A08 | `MDSDataTransfer` | internal static class；Heap↔Scope受控交接，锁外Copy、身份复核 | 内部 |
| M-A09 | `MDSDataBudget` | sealed class；`LimitBytes,RetainedBytes`及内部`Measure/Reserve/Release` | 宿主/Owner |
| M-A10 | `MDSDataLoadState` | enum；`NotFile,Unloaded,Loading,Loaded,Failed` | 公共 |
| M-A11 | `IMDSDataFileLoader` | interface；`ValueType,LoadAsync(path,ct)` | 专业 |
| M-A12 | `MDSDataFileLoader<T>` | sealed class；用专业提供的异步函数加载类型化数据 | 专业 |
| M-A13 | `MDSLimitedBinaryFileLoader` | sealed class；默认单文件64MiB全量载入限制，可自定义上限 | 公共 |
| M-A14 | `MDSDataFileEntry` | internal sealed class；路径、类型、长度、UTC修改时间、Loader、缓存、字节收费与代际 | 内部 |
| M-A15 | `MDSDataLocation` | enum；`Any,FlowHeap,Input,Local,PendingOutput,CommittedOutput`（见实际覆盖限制） | 公共 |
| M-A16 | `MDSDataQuery` | sealed class；`ExactName,NamePrefix,DataType,Location,LoadState,RunId,ScopeId,Skip,Take`（默认100，上限500） | 公共 |
| M-A17 | `MDSDataSnapshot` | sealed class；`Name,DataType,Location,LoadState,Source,SizeBytes,Version,RunId,ScopeId` | 公共 |
| M-A18 | `MDSDataQueryLogic` | internal static class；`Matches,Filter,SizeOf`，内部排序/过滤分页 | 内部 |
| M-A19 | `MDSBindingSource` | enum；`Constant,CurrentSlot,NodeOutput,FlowHeap,Variable,EngineeringResource,Expression` | 公共 |
| M-A20 | `MDSDataBinding` | sealed class；`Source,ValueType,SourceName`、安全常量 | 公共 |
| M-A21 | `IMDSExternalBindingResolver` | interface；`Resolve(source,sourceName,targetType)`，委托给已有外部Owner | 平台/专业适配 |
| M-A22 | `MDSDataBindingResolver` | static class；`Resolve<T>,CompatibleCandidates`，**不创建变量库** | R1工具/编辑器后端 |
| M-A23 | `MDSDataFunctions` | static class；`Invoke,InvokeAsync`，从已绑定Input读取并`Return`输出 | R1 |
| M-A24 | `MDSDataErrorCode` | enum；`InvalidName,NotFound,NameConflict,TypeMismatch,InputReadOnly,OutputNotDeclared,OutputMissing,OutputConflict,Busy,InvalidState,ScopeClosed,UnsafeValue,Unsupported,OperationCancelled,QuotaExceeded` | 公共 |
| M-A25 | `MDSDataException` | sealed class；`Code,Slot,InvocationId` | 公共 |

另有`Mechanism/Data/Execution/MDEExecutionDataScope`（sealed class）以及其`MDEExecutionDataEntry`（private sealed class），负责已有Scope父子关系、Local、关闭与释放回调、Move时验证；**明确复用旧类型，不新增`MDSDataScope`公共树**。`MDSDataHeap`源码使用三个partial文件（`MDSDataHeap.cs / MDSDataFiles.cs / MDSDataHeap.Query.cs`），这是**一个类**，不得按文件数多算。

### 3.3 数据体系涉及的现有其他Owner（不算新Data类）

| Owner | 实际已存在主要对象 | 在本体系中的职责 |
|---|---|---|
| `Mechanism.Runtime.Execution` | `MRKCapabilityInput, MRKResolvedCapability, MRKExecutionPlan, MRKExecutionStep, MRKExecutionInput, MRKInputSource, MRKExecutionEngine, MRKExecutionContext, MRKFlowResult` | 唯一能力输入声明、DataOutputs、编译和运行Owner；已有MRK计划负责端口映射、子流程屏障与最终结果，不创建第二个Data项目 |
| `Professional.Vision` | `CxVisionGrayFrame,CxVisionInspectionResult,CxVisionDefectRegion,CxVisionDataFlow` | 专业图像/结果真实类型与8/16位CPU连通域算法 |
| `Foundation.Infrastructure` | `FCRWorker`及相关并发原语 | Worker调度，不作为数据Lease |
| `已有配置/变量/工程资源` | 相应现有Owner | 提供固定配置版本、设备变量/质量、工程资源定位；Data只登记来源/接收安全值 |

### 3.4 当前核心API精确契约（节选，源码存在）

~~~csharp
public interface IMDSDataContext
{
    T Get<T>(string name);
    bool TryGet<T>(string name, out T value);
    void Set<T>(string name, T value);
    void Return<T>(string name, T value);

    T Get<T>(FDTDataKey<T> key);
    void Set<T>(FDTDataKey<T> key, T value);
    void Return<T>(FDTDataKey<T> key, T value);

    T HeapGet<T>(string name);
    void HeapSet<T>(string name, T value);
    void MoveToHeap(string localName, string heapName = null, bool replace = false);
    void CopyToHeap(string localName, string heapName = null, bool replace = false);
    void MoveFromHeap(string heapName, string localName = null, bool replace = false);
    void CopyFromHeap(string heapName, string localName = null, bool replace = false);

    void SetFile(string name, string filePath);
    void SetFile<T>(string name, string filePath, IMDSDataFileLoader loader);
    Task<T> LoadAsync<T>(string name, CancellationToken cancellation = default(CancellationToken));
    void Unload(string name);

    IList<MDSDataSnapshot> Search(MDSDataQuery query = null);
    MDSDataSnapshot Inspect(string name, MDSDataLocation location = MDSDataLocation.Any);
    IDictionary<string, object> InspectPendingOutputs();
    FDTDataAdapterRegistry Adapters { get; }
}
~~~

### 3.5 关键值、错误和生命周期的实情

- 当前`FDTReadOnlyBuffer`从输入`byte[]`复制一次，之后可供多只读消费者同实例访问；`ToArray`才得到新可写数组。它不是`ReadOnlyMemory<T>`的无复制GPU映射。
- `FDTDataAdapterRegistry.Register<T>`用于自定义专业对象的复制与估算；“`immutable:true`”是专业承诺，必须有测试保证真实不可变，不能任意标记可变DTO。
- `MDSDataBudget`是FlowHeap缓存及DataStack已提交Output**托管逻辑有效载荷预算**，不是全进程RSS/GC/VRAM上界。
- `MDSDataLoadState`描述FileLocator缓存状态；与`MRKFlowState`、`MRKStepState`不同。
- `MDSDataSnapshot.Version`目前只是基础元数据值，不等价于早期设想的`HeapId+DataId+Generation+Version`稳定跨进程身份。
- `MDSDataException`有结构化ErrorCode，但部分接口仍以`InvalidCastException, KeyNotFoundException, IOException, ObjectDisposedException`报告特定情况，未来UI要统一呈现，不假称已有全链路同一种异常。
- `MDSDataBindingResolver`虽然列出七种来源，但`Variable/EngineeringResource/Expression`必须接已有Owner resolver；`NodeOutput`必须先由MRK端口连接后以当前Input别名读取。
- 当前系统没有公开的`MDSDataRef/MDSDataLease`默认契约；不应根据旧V1.3文档在M1～M4回填它们。

## 4. 历史V1.3原48项对象：逐项保留与去向裁决（不删不漏）

本节保持原编号F01～F15、M01～M33，**总数为15+33=48**。旧稿所谓“P0”是当时建议优先级，不代表今日都必须创建类。表内“目标成员”是原V1.3设计，不是当前可调用API。

### 4.1 F01～F15（基础类型15项）

| 原ID | V1.3对象 | 当时完整关键契约 | 今天裁决 / 现有替代 |
|---|---|---|---|
| F01 | `FDTDataTypeId` | `TypeKey,Version; Parse,Equals` | P：跨专业持久Schema确需时再定，当前以CLR Type+Descriptor为基础 |
| F02 | `FDTDataTypeDescriptor` | `ClrType,Fields,Nullable; ValidateValue,CompatibleWith` | **I（简化）**：现有`ClrType,Kind,IsSafelyCopyable,IsNullable,Describe`；字段/兼容矩阵待扩 |
| F03 | `FDTDataShape` | `Dimensions,Strides,ElementType; Validate,ByteCount` | P：CT/多维Tensor真实接入前优先设计；当前图像维度在专业类型中 |
| F04 | `FDTDataValue` | `TypeId,Value,Metadata; Get<T>` | R：基础安全捕获与容器值对象承担，不必为所有标量再包一层 |
| F05 | `FDTDataRecord` | `Id,Version,CreatedAt; WithMetadata` | R/P：`MDSDataSnapshot`是运行摘要；可持久记录由现有结果Owner或后续专业元数据承担 |
| F06 | `FDTDataMetadata` | `Unit,FrameId,ClockDomain,Timestamp,Quality` | P：工业测点/坐标/采样时间确有需要；不能用通用`object`代替质量语义 |
| F07 | `FDTDataProvenance` | `SourceId,WorkId,ConfigRevision,AlgorithmVersion` | P：结果来源在M5阶段按真实Vision/CT链路接入 |
| F08 | `FDTDataBufferDescriptor` | `Length,Alignment,Endian,ReadOnly; ValidateRange` | R/P：现`FDTReadOnlyBuffer`处理普通字节；跨设备布局要描述符时再加 |
| F09 | `IFDTBufferView` | `Length,IsReadOnly; ReadRange,Slice` | R/P：`FDTReadOnlyBuffer`覆盖简单托管只读；专业ROI视图应由专业提供，必要时提契约 |
| F10 | `FDTBinarySlice` | `Offset,Count,Stride; BoundsCheck` | P：文件/体数据分块ROI需时作为值类型引入 |
| F11 | `FDTDataCodecRegistry` | `Register,Encode,Decode` | D：不重复配置系统Codec；先复用各专业原有序列化 |
| F12 | `FDTDataChecksum` | `Compute,Verify` | P：磁盘强一致/工程资源校验用；目前仅源文件Size+MTime轻校验 |
| F13 | `FDTDataSchemaValidator` | `Validate,Diff` | R/P：已有MRK端口类型验证；多维Shape/Unit差异出现后扩充，不建第二份端口Schema |
| F14 | `FDTDataPathUtil` | `ResolveSafePath` | R/P：现FileLocator使用Path.GetFullPath；应用沙盒授权需与工程文件Owner共同落实 |
| F15 | `FDTDataRange` | `Start[],Length[],Stride[]; ValidateAgainst(Shape)` | P：ROI/切片范围，实机CT接入前建立，与专业Shape协作 |

### 4.2 M01～M06（原M09变量与实时测点6项）

| 原ID | V1.3对象 | 当时完整关键契约 | 今天裁决 / 所属边界 |
|---|---|---|---|
| M01 | `MVBVariable<T>` | `Id,Quality,Timestamp; Read,Commit` | R/P：变量真源归现有DeviceVariable/IoT/Automation机制；统一Data不可自建第二测点库 |
| M02 | `MVBVariableStore` | `Read<T>,CommitBatch,Subscribe` | **R：不新建第二套VariableStore**，优先适配已有Owner |
| M03 | `MVBVariableSnapshot` | `Value,OldValue,Quality,Timestamp,Source,Revision` | P：专业变量快照确需统一语义，先确认旧Owner已有数据结构 |
| M04 | `MVBVariableBinding` | `SourceRef,TargetSlot,Converter; Validate,Resolve` | R/I（语义）:现`MDSDataBinding/MDSDataBindingResolver` + `IMDSExternalBindingResolver`提供运行映射，不重建变量表 |
| M05 | `IMVBValueConverter` | `InputType,OutputType; Convert,Validate` | P：只在真实跨类型/单位转换需求出现后引入；类型不匹配默认拒绝 |
| M06 | `MVBVariableSubscription` | `Dispose,Id,Throttle` | R/P：优先复用现有IoT订阅；后续完善MDS适配桥，禁止无故全局轮询 |

### 4.3 M07～M27（原M10堆栈、Ref/Carrier21项）

| 原ID | V1.3对象 | 当时完整关键契约 | 今天裁决 / 替代 |
|---|---|---|---|
| M07 | `MDSDataRef` | `HeapId,DataId,Generation,Version,TypeId` | **D作为日常入口**；仅外部资源/持久身份真需要时再引入路径或定位标识 |
| M08 | `MDSDataHeap` | `Put,Resolve,PublishVersion,Release,GetMetrics` | **I（实际不同）**：`Set/Get/Replace/Remove/MoveTo/CopyTo/SetFile/LoadAsync/Unload/Query/Inspect` |
| M09 | `MDSDataEntry` | `Owner,Carrier,State,Metadata,Versions` | **I（内部）**：`MDSDataHeap.MDSDataEntry`带`Value,Type,Bytes`；文件另有`MDSDataFileEntry` |
| M10 | `MDSDataScope` | `Parent,Input/Local/Pending; CreateChild,Commit,Abort` | **R**：复用`MDEExecutionDataScope`+`MDSDataContext/Frame`，不建平行Scope树 |
| M11 | `MDSDataStack` | `PushFrame,PopFrame,GetFrames` | **I（实际不同）**：以`OpenFrame/Publish/TryReadOutput`管理Flow调用树，不是线程栈 |
| M12 | `MDSDataFrame` | `InvocationId,InputBindings,Locals,PendingOutputs,CommitState` | **I**：`Data,CommitOutputs,AbortOutputs,Dispose` |
| M13 | `MDSDataSlotBinding` | `SlotId,TypeId,Ref,Access; Validate` | **R/I（分工）**：现`MDSDataBinding`描述数据来源；MRK已有Input/Output端口负责执行绑定 |
| M14 | `MDSDataHandle<T>` | `AcquireRead,AcquireWrite` | D：普通数据不引入Handle；GPU/SDK按专业Owner需求再提 |
| M15 | `MDSDataLease` | `DataId,Version,Access,Range; As<T>,Dispose` | D：普通节点不使用；未来外部SDK/SHM精确占用可采用内部Lease机制 |
| M16 | `IMDSDataCarrier` | `Load/Unload,Acquire,Capabilities` | **R/P**：现`IMDSDataFileLoader`覆盖文件按需加载；泛Carrier高级协议延后 |
| M17 | `MDSMemoryCarrier` | `Read,Write,Evict` | **R**：普通托管内存已经由`MDSDataHeap`+基础安全值直接拥有 |
| M18 | `MDSMappedFileCarrier` | `OpenWindow,Close` | D：大型CT真实MMF/ROI验证后再建 |
| M19 | `MDSSharedMemoryCarrier` | `ABI,Generation,Open,Lease,Close` | D：跨进程不能拿现在Loader冒充SHM安全 |
| M20 | `MDSCarrierRegistry` | `Select,Register,Resolve` | R/P：现在`FDTDataAdapterRegistry`注册安全值、`IMDSDataFileLoader`加载文件；无需额外全局Carrier中心 |
| M21 | `MDSDataQuotaPolicy` | `MaxBytes,High/LowWater; Evaluate` | **I（简化）**：现`MDSDataBudget`提供限额/持有计费；驱逐/背压另议 |
| M22 | `MDSDataEvictionPolicy` | `SelectVictims,Retention` | D：当Heap真正需要缓存驱逐时再验证，不得擅自驱逐正在使用的数据 |
| M23 | `MDSDataTransferService` | `TransferAsync,Verify` | **I（内部同步形式）**：`MDSDataTransfer`实现Heap↔Scope Move/Copy，不额外公开TransferService |
| M24 | `MDSDataSnapshot` | `ScopeId,Refs,Versions,Bytes,Owners; Compare` | **I（元信息版）**：`Name,DataType,Location,LoadState,Source,SizeBytes,Version,RunId,ScopeId`；不是深拷贝整个Heap |
| M25 | `MDSDataLifecycleMonitor` | `LiveLeases,Bytes,Evictions; GetSnapshot` | R/P：当前有`Budget.RetainedBytes`、`Inspect`；外部资源/多时点生命周期观察未完整实现 |
| M26 | `IMDSDataContext` | `Set/Get/Put/Ref/Return/AcquireReadAsync/Inspect` | **I（最终收敛）**：`Get/TryGet/Set/Return`+`Heap/Move/Copy/File/Search/Inspect`；普通`Put/Ref/Lease`已退出 |
| M27 | `MDSDataException` | `Code,Slot,DataId,FlowId,Expected/Actual` | **I（实际较小）**：`Code,Slot,InvocationId`；FlowId/原因链由MRK关联 |

### 4.4 M28～M33（原M35/M36历史谱系和通知6项）

| 原ID | V1.3对象 | 当时关键契约 | 今天裁决 |
|---|---|---|---|
| M28 | `MHSDataChangeJournal` | `Append,Query` | R/P：优先复用`MRKExecutionJournal`与真实结果Owner；大值历史不得复制到运行Heap当第二份真源 |
| M29 | `MHSDataLineageIndex` | `Trace,Compare` | P：多专业成果血缘/数据来源可作为可重建索引，但不能做新业务数据库 |
| M30 | `MHSDataCheckpoint` | `Plan,DataRefs,ConfigRevision; Validate,RestoreRefs` | D：与Runtime Checkpoint联合设计，未做一致性快照就不能宣称可恢复 |
| M31 | `MNMDataChangeNotice` | `DataId,Version,ChangeKind` | R/P：优先已有通知/Observer事件体系，数据变化事件需避免大对象负载 |
| M32 | `MNMDataSubscription` | `Filter,Backpressure,Dispose` | P：对可视化数据变化订阅需要真实R3交互用例，不复制IoT订阅机制 |
| M33 | `MNMDataNotificationBridge` | `Publish,Subscribe` | R：使用现有平台通知通道适配，而非新建第二事件总线 |

**核对：F15+M33=48项，逐项均在上面出现。** 其中有些是当前已落地的同名对象，有些是被简单设计吸收，有些仍是专业未来能力；不是“48个Class都必须在M1～M4创建”。

## 5. V1.3原15项枚举：逐项裁决与当前枚举完整性

| 原ID | 原枚举及成员方向 | 现在状态 | 说明 |
|---|---|---|---|
| E01 | `FDTDataKind`：Scalar/Object/List/Map/Buffer/Image/Volume/Tensor/Stream | **I** | 现有9成员 |
| E02 | `FDTByteOrder`：LittleEndian/BigEndian | P | 现在16位RAW专业流程内明确定义小端；通用基础枚举待跨格式 |
| E03 | `FDTDataQuality`：Good/Uncertain/Bad/Stale/Unknown | P | 设备变量质量不能丢失，先核对旧Owner |
| E04 | `FDTTypeCompatibility`：Exact/Compatible/RequiresConversion/Incompatible | R/P | 当前以MRK类型验证为主；单位/Shape转换再补 |
| E05 | `MDSDataSlotKind`：Input/Local/Output/Shared | **R/I** | 现`MDSDataLocation`：Any/FlowHeap/Input/Local/PendingOutput/CommittedOutput |
| E06 | `MDSDataAccessMode`：Read/Write/ReadWrite | D | 当前Input只读、Local可写用运行契约表达 |
| E07 | `MDSDataOwnership`：Copied/Transferred/Borrowed | R/P | Move/Copy显式动词已经表达常规所有权；外部设备Owner再单列 |
| E08 | `MDSDataWritePolicy`：ReadOnly/CopyOnWrite/ExclusiveMutable | R/P | 普通共享只读+显式Copy；不承诺自动隐式COW |
| E09 | `MDSCarrierKind`：Memory/MappedFile/SharedMemory/Gpu/Remote | D | 不因为枚举可写就认为载体已实现 |
| E10 | `MDSCarrierState`：Unloaded/Loading/Ready/Unloading/Failed | **R/I** | 现`MDSDataLoadState`：NotFile/Unloaded/Loading/Loaded/Failed |
| E11 | `MDSDataState`：Registered/Available/Retained/Evicting/Released/Faulted | R/P | 当前局部/已提交/关闭由Scope、Frame和MRK状态表示 |
| E12 | `MDSDataRetention`：Scope/Session/Pinned/Persisted | D | 当前保守Flow级保活；Session/Persisted须具体Owner与权限 |
| E13 | `MDSQuotaAction`：Reject/Wait/EvictEligible | R/P | `MDSDataBudget`已实现Reject；Wait/Evict需背压策略验证 |
| E14 | `MDSDataErrorCode`：NotFound/TypeMismatch/QuotaExceeded等 | **I** | 当前15个成员；上节M-A24列明 |
| E15 | `MNMDataChangeKind`：Created/Published/Updated/Released/QualityChanged | P | 统一观察事件须依托现有通知Owner |

**当前实际上存在的运行时数据枚举**：`FDTDataKind, MDSDataLoadState, MDSDataLocation, MDSBindingSource, MDSDataErrorCode`，其中`MDSBindingSource`为后续新增的统一绑定来源，未列入旧15项；不能把旧15项当成“已实现15个Enum”。`MDSDataLocation.CommittedOutput`当前属于**模型预留位置，搜索端尚未全覆盖**，切勿标绿。

---

## 6. 核心对象规格：不是只有类名，还要职责、边界、关键属性和方法

### 6.1 `IMDSDataContext / MDSDataContext`（P0便捷门面）

**实例来源**：现有`MRKExecutionEngine`运行`MRKExecutionStep`时创建节点Frame，并通过`ctx.Data`注入。业务人员不自行`new MDSDataContext`。上下文以一次真实节点Invocation为边界。

| 操作 | 实际接口 | 输入/结果 | 不变量、反例 |
|---|---|---|---|
| 读 | `Get<T>(name/key)` | 有值或类型异常 | 缺失抛`KeyNotFoundException`，不会暗中找父级Local |
| 可选读 | `TryGet<T>(name,out value)` | 缺失返回false | 类型不匹配不能伪装成“缺失” |
| 写局部 | `Set<T>(name/key,value)` | 当前Local | 当前Input同名不能覆盖；底层安全捕获 |
| 暂存输出 | `Return<T>(name/key,value)` | 本Frame的PendingOutput | 必须在声明的Output内；重复Return拒绝；**未完成Commit** |
| 查Pending | `InspectPendingOutputs()` | 当前帧Pending快照 | 只供当前授权调用诊断，不当Flow结果 |
| Heap读写 | `HeapGet<T>,HeapSet<T>` | 显式FlowHeap | 不等同于Local；Heap即时副作用不能随Node失败自动回滚 |
| 双向交接 | `MoveToHeap/CopyToHeap/MoveFromHeap/CopyFromHeap` | 默认同名拒绝，显式replace | Local属于Scope，Heap属于Flow，Move源将失效 |
| 文件 | `SetFile,LoadAsync<T>,Unload` | 文件定位及托管缓存 | 未加载时`Get`不能自动触发IO；超限/取消/失效不发布 |
| 元数据 | `Search,Inspect` | 轻量Snapshot，不含大内容 | 限当前授权范围，默认不遍历其他节点Committed |
| 专业能力 | `Adapters` | 本Flow注册表 | 未注册可变类型拒绝；不在每个并行节点中重复注册同一类型 |

`CommitOutputs/AbortOutputs/PrepareRetry`属于实现类与Frame/Runtime交接，不是普通`IMDSDataContext`对业务公开的指令。不能建议专业算法内部调用`CommitOutputs`。

**使用优先级**：①`Get/Set/Return`；②`FDTDataKey<T>`与`MDSDataFunctions`；③高级`Heap/Move/Copy/File/Search`；④真正设备外部内存使用专业Owner的安全协议。

### 6.2 `MDSDataHeap`（显式持有者）

**持有状态**：内存条目（对象+CLR类型+载荷大小）、FileLocator（路径、类型、Loader、文件大小/UTC修改时间、`LoadState`、缓存及代际）、每Heap可选Budget、适配器注册表和RunId。

- `Set<T>`：登记新条目，不隐式覆盖。
- `Get<T>`：按安全复制/不可变承诺读取；FileLocator未加载则报错。
- `Replace<T>`：覆盖已存在条目；与Set截然不同。
- `Remove`：移除条目并归还逻辑持有量；移除Locator不会删除源文件。
- `MoveTo/CopyTo`：Heap间受控移动或独立复制，可同Heap改名/派生；同名默认拒绝。
- `SetFile<T>`：登记类型与专业Loader，**不立即读取**。
- `LoadAsync<T>`：合法LoadState转换、源文件前后检查、取消、预算与节点结束屏障。
- `Unload`：清缓存，继续持有文件定位。
- `Query/Inspect`：只读摘要；`Inspect`在Heap可按字典精确获取条目。

**特殊边界**：`CopyTo`对FileLocator默认复制定位描述，不复制几十GB文件；专业对象Copy需要目标Heap同样知道安全复制规则；跨不同Budget移动可能需要重新测量与预留。要把“外部文件持久化”与“Heap拥有缓存”严格区分。

### 6.3 `MDSDataStack / MDSDataFrame / MDEExecutionDataScope`（运行交接）

- 每个真实Flow有一份`MDSDataStack`，其中`FlowHeap`属于该Flow；多Flow之间不应通过“同名Key”共享实例。
- `OpenFrame(invocationId,scope,inputs,declaredOutputs)`为节点创建唯一Frame；`invocationId`不能在同Flow重复。
- Frame的`Data`注入当前Step，Inputs按MRK编译好的端口映射，Local由Scope拥有，Pending在Context内部。
- 业务`Return`只写Pending；当MRK实际执行成功且声明的Output全部满足时调用`frame.CommitOutputs()`，一次性发布到`MDSDataStack`。
- 下游通过MRK`StepOutput`绑定读取已提交数据；实际安全输出的读取可以委托`stack.TryReadOutput<T>(invocationId,outputName,out value)`。
- `Frame.Dispose`和Scope结束时释放Local；已提交Output暂由Flow保活。此处`DataStack`不是线程局部的传统`Stack<T>`。
- `MDEExecutionDataScope`保留旧代码兼容，但**新MDSDataContext托管帧不允许自动继承父Scope Local**；父子只在显式映射端口情况下传数据。
- 嵌套专业子流程靠MRK编译期`InlineSubflow`与`__commit`屏障，不允许父节点读取未导出的子内部输出。

### 6.4 `FDTDataAdapterRegistry`（专业值的安全形态）

~~~csharp
// 真实签名形状：全Flow独立注册，专业方负责Clone是否真正深复制。
var adapters = new FDTDataAdapterRegistry();
adapters.Register<MyResult>(
    source => DeepClone(source),
    kind: FDTDataKind.Object,
    sizeEstimator: source => EstimateBytes(source));
adapters.Freeze();
~~~

- `Capture`是安全值转入/离开容器的唯一核心校验；`byte[]`是防御复制，`FDTReadOnlyBuffer`按只读实例共享。
- `sizeEstimator`是**托管有效载荷计费**，不是.NET精确RSS；不填时有限预算会拒绝无法确定大小的专业类型。
- `Freeze`后不可再注册，适用于Flow的能力注册准备阶段。**当前实际Vision示例在Decode节点注册两个适配器**；未来如多分支可能抢注册，建议将类型注册提前到Flow创建/专业Provider准备阶段（**拟改进，不是当前已提供的MRK配置入口**）。
- `Kind`的Image/Volume/Tensor是类型标记，不代表专业格式编码/解码、变换、深度学习网络自动存在。

### 6.5 `MDSDataBudget`（逻辑保留量而非物理封顶）

| 量 | 当前含义 | 不包含 |
|---|---|---|
| `LimitBytes` | FlowHeap及已提交FlowStack有效载荷上限 | CLR所有托管对象的真实堆峰值 |
| `RetainedBytes` | 目前这些Owner仍持有的估计有效载荷 | 暂存的大数据副本、堆外/SHM/GPU与SDK资源 |
| `QuotaExceeded` | 当写入/Commit/Load的收费超过剩余额度时拒绝 | 自动等待、自动驱逐或实时性能承诺 |

配额默认**可选**，宿主`MRKExecutionEngine(dataPayloadBudgetBytes: ...)`启用。启用后大专业DTO应给尺寸估算器。并发Copy的峰值分配、临时图像算法工作内存仍需真实负载测量；不能由此推断“保证永不OOM”。

### 6.6 `MDSDataQuery / MDSDataSnapshot / MDSDataBinding`（R3后端）

**Snapshot最小字段**：`Name,DataType,Location,LoadState,Source,SizeBytes,Version,RunId,ScopeId`；不含实际像素、原始Buffer、可写对象句柄。

**Query筛选**：`ExactName`或`NamePrefix`、类型、Location、LoadState、RunId、ScopeId、Skip、Take。默认Take=100，上限500；大量条目用分页或限制前缀。元数据查询不可触发文件实际装载。

**Binding描述**：`MDSDataBinding(MDSBindingSource source, Type valueType, string sourceName=null, object constant=null)`：
- 常量：只有值和目标类型，捕获后避免可写数组别名。
- 节点输出：存明确端口来源，但运行前由MRK编译成**当前节点的Input别名**，业务数据上下文只知道别名。
- FlowHeap：显式共享数据。
- Variable/EngineeringResource/Expression：`IMDSExternalBindingResolver`委托给已有业务Owner，机制层不保存一份新真源。
- `CompatibleCandidates`按类型筛选当前可见且状态可用的数据，不自动读取文件。**当前不是工程定义资源和历史Run的全量搜索API。**

### 6.7 结构化错误与可视化错误呈现

| 错误 | 机制现有语义 | 工程界面建议显示 |
|---|---|---|
| `NotFound` / `KeyNotFoundException` | 数据缺失或映射错误 | “节点Inspect缺少必需的Image输入” |
| `TypeMismatch` / `InvalidCastException` | Input/Output类型不兼容 | “阈值应为int，连接结果为string” |
| `NameConflict` | 默认拒绝同名Move/Copy/HeapSet | “目标已有Image；选择新名称或显式覆盖” |
| `InputReadOnly` | 当前Input同名不能Set | “输入由上游只读提供，需要编辑副本” |
| `OutputMissing` / `OutputConflict` | 必需Output少项或重复Return | “节点返回不完整，整组输出未提交” |
| `Busy` | 加载中禁止Unload/Remove或复制源变更 | “数据正在使用/载入，稍后重试” |
| `ScopeClosed` / `ObjectDisposedException` | 当前节点已结束 | “结果迟到，已拒绝写入旧运行实例” |
| `QuotaExceeded` | 逻辑预算不足 | “本Flow已持有数据超过限定额度” |
| `Unsupported` / `NotSupportedException` | 未注册专业安全类型/超出Loader能力 | “缺少专业Loader或复制适配器” |
| `OperationCancelled` / `OperationCanceledException` | 协作取消 | “操作已取消，不提交输出” |

以上“界面建议”尚未实现成统一WPF错误提示，不能误报UI完成。

## 7. R1 软件开发者：完整D01～D09使用方案（真实API与目标能力分别标注）

下面每段都有**业务目的、最小代码、实际边界与验收反例**。标记`[已可调用]`的代码使用目前真实存在的API（需正确的命名空间、上下文和MRK宿主）；`[场景规格]`表示由业务接口/宿主继续实现的部分，**不是能直接复制编译的伪造SDK**。

### D01 小数值算法：真正只写业务的三动词 [已可调用]

~~~csharp
// 位于MRK节点回调内；运行宿主会注入ctx.Data。
int threshold = ctx.Data.Get<int>("Threshold");
ctx.Data.Set("Intermediate", threshold + 5);
ctx.Data.Return("Area", threshold * 2);
return Task.FromResult((object)true);
~~~

- 节点预先声明Input`Threshold:int`、DataOutput`Area:int`。
- `Set("Intermediate")`属于本地，不会污染平行节点。
- `Return("Area")`不等于成功：真正Commit在MRK工作返回、输出校验后执行。
- **反例**：删掉Return、Return错误类型、重复Return；必须不能产生`FlowResult.Area`的半结果。

### D02 强类型Key与普通C#函数 [已可调用]

~~~csharp
var src = new FDTDataKey<int>("Width");
var dst = new FDTDataKey<int>("DoubleWidth");

// 当前节点的Width已通过MRK端口绑定Input
int answer = MDSDataFunctions.Invoke(
    ctx.Data, src, dst, width => checked(width * 2));
return Task.FromResult((object)answer);
~~~

- 当前方法`Invoke<TInput,TOutput>`自动`Get(src)`、运行`Func`、`Return(dst,result)`，**不是新调度器**。
- 异步业务用`InvokeAsync(ctx.Data,src,dst,async (x,token)=>...,ct)`，取消后不得Return。
- **反例**：输出没有在MRK Step的`DataOutputs`声明时，Return必须拒绝。

### D03 一图两算法共享与“修改前复制” [已可调用基础]

~~~csharp
// 来源图像在进入运行前做一次安全只读封装。
FDTReadOnlyBuffer original = new FDTReadOnlyBuffer(rawPixels);

// 在采图/Decode节点，可通过Return发布给两个下游Input：
ctx.Data.Return("Image", original);

// 需要修改时：拿独立可写数据，不直接改original。
byte[] editable = ctx.Data.Get<FDTReadOnlyBuffer>("ImageInput").ToArray();
editable[0] = 0; // 这是派生副本，原图保持不变
ctx.Data.Return("Enhanced", new FDTReadOnlyBuffer(editable));
~~~

这里的来源节点与增强节点是**两个不同的回调片段**，不能在同一节点同时执行或假设输出自己变成自己的Input；连接必须由MRK预先声明。典型结构：`Decode.Image → AI.ImageInput`且`Decode.Image → CV.ImageInput`。AI/CV不是靠全局Heap直接读“同一个名字”。

**反例**：算法A持有原图，算法B修改自己复制得到的派生图；A读到的原始像素不能变化。`FDTReadOnlyBuffer`多消费者可以共用同一安全不可变实例，`byte[]`不能假定同实例传递。

### D04 堆栈数据移动、复制与同名冲突 [已可调用]

~~~csharp
// 本节点Local
ctx.Data.Set("ROI", new byte[] { 1, 2, 3 });
// Move后不应再用Local ROI，Heap新持有者为GlobalROI
ctx.Data.MoveToHeap("ROI", "GlobalROI");
// Copy是独立安全副本；需要可写数组时取出并在本节点另存
ctx.Data.CopyFromHeap("GlobalROI", "WorkingROI");
byte[] working = ctx.Data.Get<byte[]>("WorkingROI");
working[0] = 99;
ctx.Data.Set("ModifiedROI", working);  // 修改结果成为新的Local
~~~

另可直接使用实际Heap API：

~~~csharp
using (var source = new MDSDataHeap())
using (var target = new MDSDataHeap())
{
    source.Set("Result", new byte[] { 1, 2 });
    source.CopyTo("Result", target, "Copy");  // 源仍有Result
    source.MoveTo("Result", target, "Moved"); // 源Result被移除
    // 如果目标已存在Moved，默认拒绝；
    // 确需替换必须传replace: true。
}
~~~

**反例**：把已有名字作为目标、同时竞争Move同一来源、复制器在复制过程中故意阻塞或来源被替换；均不得悄悄覆盖/发布半对象。显式Heap操作发生即生效，不能当成可自动回滚的Return。

### D05 仅定位磁盘，按需加载与卸载 [已可调用]

~~~csharp
ctx.Data.SetFile("CT.Raw", absoluteFilePath);
MDSDataSnapshot meta = ctx.Data.Inspect("CT.Raw", MDSDataLocation.FlowHeap);
// meta.LoadState == Unloaded；这里没有读取文件主体

byte[] bytes = await ctx.Data.LoadAsync<byte[]>("CT.Raw", ct);
ctx.Data.Unload("CT.Raw"); // 只是清缓存，绝不删除文件
~~~

- 默认`MDSLimitedBinaryFileLoader`限制64MiB整文件加载；读取GB级CT需使用专业Loader读取ROI/块，不能凭当前API全量装载8GB。
- 文件被替换、加载中Unload、取消、节点已关闭后迟到结果应失败；失败后Locator仍可检查。
- **反例**：先搜索10000条磁盘Locator，磁盘读取字节不能因Search/Inspect增加；用容量小于文件缓存的Flow执行Load，应`QuotaExceeded`且不保留半缓存。

### D06 自定义专业DTO与预算 [已可调用，专业类型是业务自有]

~~~csharp
// 假设此例的MyDetection是专业项目真实存在的自定义类型。
// 专业应提供可审计的深复制与有效载荷字节估算器。
var adapters = new FDTDataAdapterRegistry();
adapters.Register<MyDetection>(
    x => DeepCopyDetection(x),
    kind: FDTDataKind.Object,
    sizeEstimator: x => EstimateDetectionBytes(x));
adapters.Freeze();

var budget = new MDSDataBudget(100L * 1024 * 1024);
using (var stack = new MDSDataStack(null, adapters, null, budget))
{
    stack.FlowHeap.Set("Result", myDetection);
    // 读取到的专业DTO应符合专业安全复制承诺。
}
~~~

注意`MyDetection/DeepCopyDetection/EstimateDetectionBytes`是**专业开发者必须提供的示例名称，不是仓库已有类**。当业务使用现有MRK Flow时，推荐由宿主设置`dataPayloadBudgetBytes`，而不是业务节点自己建立独立DataStack。

**反例**：专业对象内含可变`byte[]`，复制器返回同一对象或共享同一可写底层数组；要证明隔离。有限预算而没有尺寸估算器，则不得默认为0字节继续。

### D07 真正Vision专业算法进入MRK [已可调用，当前仓库实有对象]

~~~csharp
var options = new CxVisionInspectionOptions
{
    PixelDepth = CxVisionPixelDepth.Bits8,
    GrayThreshold = 200,
    MinDefectArea = 2,
    MaxDefectArea = 100
};
MRKExecutionPlan plan = CxVisionDataFlow.CreateCpuInspectionPlan(options, 17);

var inputs = new Dictionary<string, object>
{
    { "RawPixels", new FDTReadOnlyBuffer(rawPixels) },
    { "Width", width },
    { "Height", height }
};

MRKFlowResult result = await engine.StartFlow(plan, inputs).Completion;
if (result.State != MRKFlowState.Completed)
    throw result.Error ?? new InvalidOperationException("Vision运行失败");

CxVisionInspectionResult imageResult =
    result.GetCommittedOutput<CxVisionInspectionResult>("Inspection");
long graySum = result.GetCommittedOutput<long>("GraySum");
bool passed = result.GetCommittedOutput<bool>("Passed");
~~~

此源码调用的是`CxVisionDataFlow`中的真实5节点链路：Decode→Preprocess→（Inspect∥Statistics）→Decision。支持8/16位灰度RAW、阈值、四邻接连通域、灰度统计和结果合并。**这不是X-Ray工业相机、AI或CT硬件的实机接入**。

**反例**：改输入宽高使RAW长度不匹配、使用不支持的位深、设Flow逻辑持有预算过小、并行运行两个不同输入；应按来源Run隔离，失败时`CommittedOutputs`为空。

### D08 子流程A→B→C，无显式堆栈控制节点 [既有MRK机制，具体编排契约见测试]

~~~text
A主流程（SN、Recipe）
   └─ B采图与检测子流程（Input: SN, Threshold）
         └─ C缺陷检测子流程（Input: Image, Threshold; Output: Defects）
B显式导出C.Defects → A.Defects
~~~

当前确有`MRKExecutionPlan.InlineSubflow(prefix,child,entryDependencies)`及`__commit`屏障；但**不存在旧V1.3示例中可直接使用的`runtime.RunChildAsync("...", data, ...)`通用API**。这点一定要纠正。

实际集成步骤：①用`MRKExecutionStep`定义每层Input/DataOutputs；②子Plan声明`DataExports`；③编译期`InlineSubflow`展开；④外层只绑定子`__commit`后公开的Output；⑤任意C失败时B/A不应获得半结果。

**反例**：外层连接到`nested.privateNode.Secret`应编译拒绝；并行同名C#1和C#2的Local不能串值。

### D09 设备变量、IoT测点与检查器 [现有数据桥+拟对接专业Owner]

~~~csharp
// 以下绑定对象和解析器接口当前真实存在；resolver是原IoT/PLC Owner提供的实现。
var binding = new MDSDataBinding(
    MDSBindingSource.Variable, typeof(double), "Line01.Pressure");

double pressure = MDSDataBindingResolver.Resolve<double>(
    ctx.Data, binding, existingDeviceVariableResolver);

// 当前运行数据元信息，绝不自动载入大文件：
var visible = ctx.Data.Search(new MDSDataQuery {
    RunId = ctx.Data.Inspect("Threshold").RunId,
    NamePrefix = "Camera",
    Take = 100
});
~~~

`existingDeviceVariableResolver`是专业接口实现的示例参数名。实际设备变量Quality/Timestamp、订阅节流与PLC写入ACK必须由相应Owner提供，不能将`MDSDataBindingResolver`说成已经实现了PLC通道或新的IoT数据库。

**反例**：没有外部Resolver时拒绝读取；类型不兼容时拒绝；给未授权节点一个`SourceName`不能绕过能力权限。

### D10（新版补充）运行结束后的输出和并发Flow [已可调用]

~~~csharp
MRKFlowHandle flowA = engine.StartFlow(plan, inputsA);
MRKFlowHandle flowB = engine.StartFlow(plan, inputsB);

MRKFlowResult a = await flowA.Completion;
MRKFlowResult b = await flowB.Completion;
if (a.State == MRKFlowState.Completed)
    Show(a.GetCommittedOutput<CxVisionInspectionResult>("Inspection"));
if (b.State == MRKFlowState.Completed)
    Show(b.GetCommittedOutput<CxVisionInspectionResult>("Inspection"));
~~~

`Show(...)`是界面业务占位函数；Flow输出是已提交结果的安全快照，**不是**跨Run随时可写Heap。测试必须保证`a.FlowId != b.FlowId`、两幅图的实际缺陷数量不同且不会串结果。`MRKFlowResult.Results`属于旧StepResult通道，不能无条件声称任何Object都是新MDS强保护类型。

---

### 7.11 原V1.3开发者D01～D09案例与现行示例逐项对应

> **特别说明**：上面的D01～D10为了按当前最自然的API组织，重新编排了示例顺序。因此它们的D编号不应被误认为直接复刻原V1.3中的D编号。以下保留**原编号及原意**，给出对应现行实例与尚未完成处。

| V1.3原案例 | 原始业务目标 | 本V3.0现行落点 | 仍需兑现的差异 |
|---|---|---|---|
| 原D01 | 3行小值算法 | `7.D01` | 无需引入Ref/Lease |
| 原D02 | X-Ray采图→AI/CV并行→合并 | `7.D03`+`8.V01` | 当前验证是Vision CPU并行统计，真正AI/相机SDK未接 |
| 原D03 | 三层子流程A→B→C的Return/Commit | `7.D08`+`8.V02` | 由MRK InlineSubflow实现，不存在旧示意RunChildAsync通用API |
| 原D04 | 8GB CT Volume仅读小ROI | `7.D05`+`8.V03` | 通用FileLoader可用，但8GB实测与专业只读ROI视图仍待验 |
| 原D05 | IoT测点Quality/Time/Subscribe | `7.D09`+`8.V04` | 外部变量Owner的Quality、订阅接口未在本MDS内部重建 |
| 原D06 | 检测任务固定配置V8，后台保存V9 | **本节7.12新增**+`8.V05` | 由已有Configuration独立负责CaptureForTask，MDS只关联来源 |
| 原D07 | 图像增强产生独立版本，原图不被改写 | `7.D03～D04` | 现行显式Copy保证值隔离；通用版本化COW未实现 |
| 原D08 | 跨进程AI超时但数据不能早释放 | **本节7.13新增**+`8.V06` | 只能列设计契约，SHM/GPU/外部Operation保活尚未实装 |
| 原D09 | 编排之外通过Runtime携Data的普通C#任务 | `7.D07/D10` | 现有`MRKExecutionEngine.StartFlow`可用，不必先画图；任意Task无需强制造Flow |

### 7.12 源案例D06：固定配置版本、算法来源 [原需求保留；Config侧实现需审计]

~~~text
任务开始：配置系统捕获Recipe V8（本次运行唯一快照）
  ├─ Vision检测多个节点都使用V8
  ├─ 后台操作员保存Recipe V9
  ├─ 当前任务不得悄然切换V9
  └─ 下一个Run按既有配置应用策略选择V9
最终结果记录：RunId + ConfigRevision + AlgorithmVersion + Result
~~~

这是**数据与配置各自只有一份权威源**的交接规范：现有MHS/FCM配置体系管理版本捕获与保存；MDS接收输入中明确绑定的配置/版本元数据，MRK记录Flow。不得凭“`data.Set("ConfigRevision",8)`”就宣称任务已经实现全程固定配置——真正的“固定”要由配置Owner和运行入口保证。**验收原U07/T19**要求运行中更新配置后当前任务结果仍关联V8，下一个任务按策略关联新版本。

### 7.13 源案例D08：跨进程AI超时、迟到结果与外部资源 [原需求保留；高级能力未实现]

~~~text
Vision图像Owner（Flow A，SN001）
  -> 专业AI Bridge提交 OperationId + 输入标识 + 版本
  -> 远程AI或独立进程异步执行
      ├─ 收到确认结果：仅在Run/SN/Operation/输入版本均匹配时Return
      └─ 本地等待超时：Run进入Unknown/复判，不代表AI进程已停止
  -> 外部Operation结束/IPC确认之后才可安全释放对应SHM/GPU载体
  -> 任何迟到回包不得进入已关闭Scope或下一个SN
~~~

用户当时的重点是**不早释放**，而不是坚持全业务都必须使用`MDSDataRef/MDSDataLease`。后期定案是：普通托管数据仍由Heap/Stack直接拥有；仅涉及SHM、GPU、SDK缓冲等外部Owner时，专业适配器才可能引入高级稳定标识/Lease。当前已有**托管FileLoader迟到数据拒绝**与MRK`ResultUnknown`的部分测试，但**没有完成远程AI/SHM外部操作的全生命周期协议**。这一区别必须写在Codex验收和UI告警中。

---

## 8. R3 工程编排用户：V01～V08完整操作规格（以“拖→连→填→测→看”为主）

> **状态特别说明**：这些是上一轮明确讨论的**目标用户体验与验收用例**。M4已具备运行数据Query/Binding/Inspect后端，Vision已有真实CPU流程，但`Applications.Studio`现阶段尚未全面提供以下每项WPF编排UI。不能把此章的交互步骤报告为“现有界面已经支持”。后续R3用户测试要求真实点鼠标，不用手改MRK计划源码完成。

### 8.1 所有编排界面只出现一套参数来源选择器

编辑节点“Threshold:int”的统一弹层建议：

~~~text
┌─ 设置：Inspect.Threshold ────────────────────────────┐
│ [来源]  ○ 常量  ○ 上游节点输出  ○ 运行数据/Heap          │
│         ○ 设备变量  ○ 工程资源  ○ 表达式               │
│                                                      │
│ [搜索]  关键字 / 类型int / 允许范围 / 当前可见Scope      │
│ [候选]  名称             类型     来源         状态     │
│         Camera.Score     int      Input        可用     │
│         Recipe.Threshold int      工程参数     可用     │
│         Flow.Calibration int      FlowHeap     可用     │
│                                                      │
│ [检查]  类型一致？有访问权限？执行前来源能否确定？       │
│                         [取消]  [确定]               │
└──────────────────────────────────────────────────────┘
~~~

一套选择器对应一个`MDSDataBinding`（或现有MRK连线端口映射）；不为常量、节点输出、变量分别开发三种互不兼容的数据仓库。设计时资源查找、运行时可见数据Search、跨Run历史追踪是**三种查询域**，各自用原Owner的数据源，统一结果展现但不混成一个Heap。

| 操作 | 编排器需要生成 | 当前已有机制 | 待做的WPF/工程接入 |
|---|---|---|---|
| 用户键入常量120 | `Constant,int,120` | `MDSDataBinding`及常量安全校验 | 控件+序列化、默认值/单位显示 |
| 从Camera.Score连线 | `MRKInputSource.StepOutput` + 目标Input别名 | MRK端口校验/绑定 | 连接线持久化、撤销、错误定位 |
| 当前调用的Local/Heap | `CurrentSlot/FlowHeap` | `data.Search/Inspect/HeapGet` | 动态候选+权限过滤 |
| 从PLC点表拖Point | `Variable` + 原Owner路径 | `IMDSExternalBindingResolver` | 设备变量目录/Quality/Timestamp接线 |
| 工程资源文件/Content | `EngineeringResource`+工程资源Id | Resolver接口 | 与Project/CxNode资源树联动 |
| 表达式 | `Expression`+授权表达式Owner | Resolver接口 | 安全解析/类型预检/求值证据 |

**注意**：目前`MDSDataBinding.SourceName`是字符串；工程侧持久化来源还需采用现有工程稳定ID/资源定义，避免直接把显示名当永久资源标识。Flow运行时可以解析为当前Invocation和端口别名，但**工程资源的权威定义始终不在FlowHeap**。

### V01 X-Ray图像检测：一图双算法再汇合（核心标杆）

~~~text
[采图A] Image ─────┬──→ [AI检测B] DefectsAI ──┐
                   └──→ [传统视觉C] DefectsCV ─┼─→ [缺陷合并D] → [判定E] → [保存/MES F]
~~~

**拖**：从专业能力目录加入采图、AI检测、传统视觉、合并、判定、保存6个节点；算法是专业编排层的能力，不应把平台读写数据的内部机制画进业务图。  
**连**：A.Image一拖二，B/C结果连D；平台显示类型，不允许`byte[]`不经明确转换就充当`Tensor`。  
**填**：选择相机、模型、阈值、检测范围、合并规则与MES目的地；数值输入统一来源选择器。  
**测**：验证必需输入、数据形态、错误出口、容量；故意令AI超时。  
**看**：同一`SN+RunId`下的Image大小、B/C状态、已提交Defects、最终判定和错误原因。  
**完成判据**：图像内容不因C的修改而影响B；B异常不许D误判为良品（除非业务降级明确声明）；当前Vision CPU五节点流程可作为真实局部验证，但**AI和相机实机链路不在这次已实现范围**。

### V02 主流程→采图子流程→检测子流程：三层调用不画Push/Pop

~~~text
主流程A (SN,Recipe)
   └─ 子流程B：AcquireAndInspect (SN → Image)
          └─ 子流程C：Detect (Image,Threshold → Defects)
   B对外明确导出Defects
A得到成功提交后的Defects
~~~

**拖/连**：工程用户把B作为普通能力节点拖入A，双击进入B再拖C，填写Input/Output映射。  
**填**：只编辑业务端口`SN,Recipe,Image,Threshold,Defects`，不选择DataFrame寿命。  
**测**：在C内部注入缺少`Defects`或抛异常，观察B与A的Output不发布；再并行运行两个B实例。  
**看**：树形Run A#01→B#01→C#01，与另一个B#02对比Input、Local、Pending、Committed；同名“Result”不会串。  
**完成判据**：通过MRK`InlineSubflow/__commit`出口屏障，禁止父节点从`B.C.Internal.Result`偷读私有内容；UI一键进入子流程但不暴露Commit节点。

### V03 CT读取、重建、MPR/体绘/检测多消费者

~~~text
[投影文件定位/采图] → [校正] → [重建] Volume ┬→ [MPR取ROI]
                                            ├→ [Volume Render]
                                            └→ [CT缺陷检测] → [判定]
~~~

**拖/连**：重建输出被三路并行连接；每个消费者声明`Volume/ROI`类型，渲染专业对象不要求放Mechanism.Data里。  
**填**：路径、投影几何、重建参数、ROI、窗宽窗位、算法输入，所有配置仍由原Config Owner管理。  
**测**：未加载的`CT.Volume`只显示Path/尺寸/状态；选小ROI后由专业Loader载入，不能因Inspector打开而整卷读入。  
**看**：LoadState、逻辑预算、实际ROI大小、读取耗时、缓存状况。  
**完成判据**：一个MPR退出不导致另一个消费者引用失效；大文件必须量测真实RSS/IO；**当前只具备FileLocator和专业Loader接口，不代表已接CT重建实机或通用CT缓存管理。**

### V04 HMI/SCADA仪表直接绑定IoT/PLC测点

**拖**：从既有设备变量/点表目录选择`Line01.Pressure`，拖入仪表和趋势曲线Value端口。  
**连/填**：使用`MDSBindingSource.Variable`记录来源意图，实际解析归设备变量Owner；阈值/颜色规则采用相同参数选择器。  
**测**：模拟Good→Stale→Bad、断线和重连，查看变量质量与时间戳，不把坏质量的数值当正常。  
**看**：值、单位、Quality、Timestamp、来源设备、最后更新。  
**完成判据**：MDS不创建第二份PLC读写变量Store；订阅释放/节流由原IoT Owner提供；向PLC写控制命令需要专业权限和真实ACK，不能经`Data.Set`偷写设备。

### V05 同一Content、同一流程定义在两条生产线运行

**拖**：同一个检测Content复用到LineA和LineB，不复制工程定义。  
**连**：LineA绑定CameraA、RecipeA，LineB绑定CameraB、RecipeB；各实例真正持有独立Run/Scope。  
**填**：每条线可设置自己的参数和运行输入映射，但ConfigVersion必须显式固定在相应任务。  
**测**：同时处理两个SN，构造相同“Image”名字、不同图像内容；每个Run得到对应结果。  
**看**：RunId、ScopeId、算法步骤、ResultId（若结果Owner已提供）、错误及耗时。  
**完成判据**：不能靠字符串名字跨Run读取局部数据；当前MRK+MDS有真实并行Flow隔离测试，Content→Studio完整组织映射仍待业务UI联调。

### V06 AI超时、迟到回包与复判分支

**拖**：AI检测→成功判定、超时/未知→复判或待确认；保持“未知”和“失败”区别。  
**连**：AI结果与原图、SN、FlowId及OperationId关联，错误分支不允许返回`Passed=true`的伪结果。  
**填**：超时时间、允许降级规则、保留文件期限、复判出口。  
**测**：AI作业被本地取消后继续后台工作，再把旧结果延迟送到已经关闭的Scope。  
**看**：ResultUnknown、当前Run、外部操作是否仍占资源、数据可见性。  
**完成判据**：迟到结果不写入旧Frame或下一SN；**当前已验证的是MDS托管FileLoader迟到保护，不代表外部AI/GPU/SHM Operation的真实释放与ACK已完成。**

### V07 数据检查器：选择一次运行、一个节点，看那次真实数据

~~~text
运行历史 [RunId / SN / 状态 / ConfigRevision]
   └─ 流程树：主流程 → 子流程 → 节点
        ├─ Input [只读]         Name / Type / Source / Size
        ├─ Local [只读检查]     Name / Type / Size
        ├─ Pending [暂存]       Name / Type / 未提交
        ├─ Committed [已提交]   Name / Type / 来源
        └─ FlowHeap [受控查询]  Loaded/Unloaded/Failed
~~~

**拖/连**：不适用；通过调试视图选择Run与Step。  
**填**：按名字、类型、来源、Scope、状态过滤，显示分页；对Image/Volume请求缩略图或小ROI需经专业Provider。  
**测**：准备至少4200条元信息和一个未加载大CT定位；搜索必须不卡UI、不装载主体；同名数据在不同位置需选明确Location。  
**看**：错误对应节点与端口、加载状态、逻辑预算、来源版本。  
**完成判据**：数据查找无跨Run泄漏、不向生产环境提供隐式可写编辑功能。**现有MDS Search后端不直接包含所有历史Run和所有已提交Output；这些视图需要MRK/历史Owner明确接入。**

### V08 Automation→Vision→CT3D→Inspection跨专业链路

**拖**：Automation采图定位、Vision预处理、CT重建、缺陷检测、Inspection判定和结果输出。  
**连**：参数与数据类型不仅校验CLR Type，还必须检查坐标系、像素尺寸、方向、采样时间、传感器标定版本等业务语义。  
**填**：运动轨迹/触发、采样配置、重建/检测参数、SN/MES映射；仍通过相同绑定选择器关联现有Config/设备Owner。  
**测**：错误坐标系和体数据形状不应默默进入算法；真实设备动作必须依据专业安全确认，不以“Data.Return成功”当设备运动停止。  
**看**：最终Result→追踪上游图像/投影/重建/算法配置的来源链。  
**完成判据**：基础MDS负责值安全、Own/Flow与元数据；真实专业语义验证和Lineage、硬件ACK尚需M5以后逐步接入。

## 9. 三种搜索不能混成一个全局Heap

| 搜索类型 | 典型提问 | 权威数据来源 | 返回什么 |
|---|---|---|---|
| **设计态资源/端口搜索** | “哪些Node Output能连接ImageInput？” | 工程组织树、专业能力Catalog、MRK/编排端口定义 | 可选节点输出、类型、资源ID与权限 |
| **运行态当前数据查询** | “这个Run、这个Step现在有什么数据？” | `MDSDataContext`和当前FlowHeap/Stack，配合MRK运行实例 | `MDSDataSnapshot`、Run/Scope/Location、LoadState |
| **历史追溯与结果查找** | “昨天SN001为何判NG？用什么版本？” | 已有运行Journal、专业结果与生产记录Owner | 结果链接、算法/配置版本、来源事件、按权限取样 |

三类结果可以在Workbench展示成同一种列表卡片，但**不得为方便UI又复制出一份可写全局数据真源**。设计态Search只列能连的合法输出；运行态Search不拥有工程资源定义；历史追溯只存标识和元信息、按需读取旧数据。

## 10. 完整十二类场景（继承V1.3，按现行Heap/Stack重新解释）

| 编号 | 场景 | 核心数据挑战 | 现行代码证据 / 后续缺口 |
|---|---|---|---|
| S01 | 多探测器在线X-Ray | SN隔离、大图扇出、并行、背压 | 真实Vision CPU部分有测试；多相机硬件未接入 |
| S02 | 2D离线机复判 | 旧结果保存、文件引用、回滚 | 有文件Locator/安全拷贝；业务复判UI待接 |
| S03 | CT慢扫/大体积重建 | GB级卷、ROI、容量与取消 | FileLoader和逻辑预算已存在；真实CT分块IO待验 |
| S04 | CT MPR/体绘/检测 | Volume多消费者与窗口寿命 | 通用只读共享；GPU/渲染View生命周期待验 |
| S05 | 远程/本机AI推理 | Tensor与SHM、超时、迟到 | 托管迟到Load保护；远端Operation租约未实现 |
| S06 | 运动控制同步采图 | 触发、轨迹、Timestamp、ACK | MRK编排能力已有；实卡安全确认专业接入待做 |
| S07 | HMI/SCADA测点 | Quality/Timestamp、订阅与权限 | Binding Owner桥存在；设备变量模型映射待整合 |
| S08 | IoT边缘网关 | 多规约、断线重连、点表 | 旧IoT Owner应适配，不能另建VariableStore |
| S09 | 三层/跨专业流程 | Scope树、Output屏障、同名隔离 | MRK InlineSubflow及MDS已有自动测试 |
| S10 | 运行数据诊断 | 只读搜索、分页、大小与加载状态 | Search/Inspect后端已实现；WPF Inspector待接 |
| S11 | 自动化测试/仿真 | 注入隔离、回放、不污染生产 | TestCenter+ArchitectureSmoke部分已验收 |
| S12 | 异常/断电/恢复 | 稳定数据身份与Checkpoint一致性 | 当前无完整跨进程数据恢复协议；后续联合设计 |

## 11. 验收规格：原T01～T30完整继承与现行代码映射

> **可追溯性原则**：此处首先完整保留V1.3每个T编号对应的原始“输入/期望/证据”，不能把某个编号借给现行其他测试。`T01～T30`是**规格目标编号**，而`TCT.MDS.001～027`是**实际TestCenter的程序ID**，两者不是同一套编号；必须建立映射但不可互相代替。

### 11.1 V1.3原始T01—T30：完整可执行验收矩阵（保留并强化V1.1）

> 以下为**验收规格**，不是已成功的测试记录。每一条需有测试输入、前置环境、实际调用入口、期望结果、故障反例、日志/计量及Commit SHA；自动化用例测试真实Foundation/Mechanism实现而非自编假存根。涉及Windows、GPU、真实设备者分级记录证据。

| ID | 类型/场景 | 测试输入与操作 | 必须观察到的结果（失败反例） | 证据/门槛 |
|---|---|---|---|---|
| T01 | Type与Shape | 把`Image<uint16>`连到要求`Image<uint8>`的端口；ROI越维 | 编译绑定或调用前拒绝，指出端口和期待类型，不隐式截断 | Schema错误码+端口ID |
| T02 | Scalar一行读写 | `Set("Threshold",120)`后`Get<int>`；再传错误类型 | 正常值正确；错误类型拒绝；不存在Local跨流程共享 | 自动单测 |
| T03 | DataRef固定性 | 创建V1/V2与SHM新Generation，保存旧Ref | 旧Ref始终只指向V1；代际变化后旧Ref失败，不跳到V2 | Ref身份日志 |
| T04 | Slot绑定与权限 | 缺少必填Input、用Output连只读Input、非法Unit/Frame | 逐项拒绝并指向映射来源；父Local无隐式继承 | 验证器报告 |
| T05 | 三层Scope A/B/C | A→B→C映射两个输入；C输出Area | C成功后B能获得显式输出，B成功后A获得；各Local隔离 | Scope树和版本 |
| T06 | Return原子性 | C暂存Area和Score，制造第二项类型错/异常 | **父两项均不更新**；旧版本仍可读；局部安全回收 | 原子提交前后快照 |
| T07 | 双只读Lease | 一份4300×4300图像扇出AI和CV；两者并行延迟 | 两Lease同Version，结束前无提前释放；物理共享可复现 | LiveLease、CopyBytes、错误0 |
| T08 | 并行分支隔离 | B#1、B#2同时使用相同Local键但不同SN | 结果按InvocationId正确归属；未声明Join写同一父Slot拒绝 | 运行Trace/冲突错误 |
| T09 | ROI按需加载 | 8GB Volume指定16MiB ROI并随机扫描窗口 | 只读目标块且无越界；无隐含整8GB常驻 | 峰值RSS/ResidentBytes/IO字节 |
| T10 | 配额/驱逐 | 给小内存预算、活动Lease与未租用缓存 | 仅驱逐合资格数据，或者明确拒绝/等待；绝不驱逐活动Lease | 配额指标与日志 |
| T11 | 背压/丢弃 | 高频X-Ray连续入队并故意慢AI | 已明确可丢弃项按策略记录SN/Flow，其他项不能静默丢失 | 队列深度/拒绝计数 |
| T12 | 读写冲突 | AI持只读V1，CV请求原地写V1 | 拒绝独占写或创建V2/COW，AI读V1不变 | 数据Hash/Version |
| T13 | 变量质量与快照 | Good→Stale→Bad切换并更新OldValue/Time | 新旧值、质量、来源、时间原子一致 | 变量Revision/日志 |
| T14 | 订阅生命周期 | 1000次Subscribe/Dispose，取消Scope | 不再回调已Dispose订阅，不泄漏句柄；历史不丢 | Handler/资源基线 |
| T15 | 血缘追踪 | Image→AI/CV→Judge；配置V8，算法A1 | 结果可反查DataId、上游Ref、Flow/Work和配置算法版本 | Lineage记录 |
| T16 | 只读Inspector | 在8GB Volume处连续打开/关闭数据查看器 | 只返回摘要且无写入/无隐式Pin或整块载入 | 内存差/读盘指标 |
| T17 | WPF UI响应 | Carrier耗时5秒加载/10次刷新 | UI仍可交互、加载可取消，错误显示 | UI事件延迟/手工记录 |
| T18 | 时间空间元数据 | 摄像图像与CT数据不同FrameId/时钟域，单位不符 | 验证指出不兼容；只能使用显式转换能力 | Schema/坐标日志 |
| T19 | 配置任务固定 | Flow捕获V8，Studio保存V9，随后继续执行 | 当前任务V8，下一任务按允许策略用V9 | ConfigRevision对照 |
| T20 | SHM重启代际 | 消费者持Ref，生产者崩溃重启同名SHM | Generation变化，旧Ref拒绝，重开后安全恢复 | 双进程故障注入 |
| T21 | 外部AI迟到 | 超时后人工复判，故意收到旧Operation回包 | 已结束Scope不被写入；新SN不接收旧结果；保留安全Lease直到收敛 | OperationId+Trace |
| T22 | GPU Fence | 异步GPU读数据，CPU先请求释放Carrier | GPU同步点完成前不销毁/复用显存；失败可诊断 | GPU测试/Adapter日志，P1 |
| T23 | COW版本隔离 | AI读取V1同时CV产生增强V2 | 两分支互不修改；结果来源明确指向相应版本 | V1/V2内容Hash |
| T24 | 设备结果与数据分离 | 发PLC/CT停止请求并模拟ACK丢失 | Data流程结束不被误报设备已安全停止 | 专业Mock+实机二级验收 |
| T25 | 快照/回放 | 固定输入Refs、ConfigRevision、随机种子及算法版本 | 能重放确定性部分；外部设备事件要标注不可复现 | Snapshot+预期对比 |
| T26 | 损坏数据恢复 | 载体断电中断/部分存盘/校验不一致 | 不发布损坏DataRef，不把半包当完成结果 | 故障注入日志 |
| T27 | 跨Session/权限 | 用LineA身份尝试读取LineB私有Ref，试越权导出 | `ScopeDenied/PermissionDenied`，敏感元数据受限并审计 | 安全测试报告 |
| T28 | 取消/异常Lease清理 | 1万次短任务+超时/异常/Join，含外部Unknown | 已确认结束的LiveLease回到基线；Unknown保留有界、可审计 | 资源监视趋势/无泄漏 |
| T29 | 全解决方案构建 | Windows、.NET Framework 4.8、Release/x64 | 构建成功、依赖无反向、自动测试结果可复现 | CI日志+Commit SHA |
| T30 | 系统级案例回归 | 5套历史系统与十大案例挑选真实主链、故障入口 | 能配置→运行→验证→归档；模拟/实机状态分别标注 | 工程文件+截图+Trace+实测 |


### 11.2 按原T编号核对当前真实进度（不是改名）

| 原规格范围 | 当前已有的可证明部分 | 保留待验或明确限制 |
|---|---|---|
| T01～T02 类型/基础值 | MRK CLR端口校验、MDS安全Get/Set/Return和类型异常 | Shape/Unit/坐标等语义转换不完整 |
| T03 Ref固定版本 | 当前Run/Scope+FlowStack，以及FileLocator代际检查 | 稳定跨进程Ref/Generation协议暂缓 |
| T04～T06 映射/三层/原子提交 | MRK Port、InlineSubflow和Frame原子Output测试 | UI工程用户三层编辑交互待实测 |
| T07～T08 并行图像/调用隔离 | `FDTReadOnlyBuffer`、真实Vision并行、Stack/Scope生命周期 | 真实4300×4300相机SDK保活未实测 |
| T09～T10 ROI与Quota | 专业文件Loader接口、限额/复制与失败原子性 | 8GB真实CT ROI IO/RSS和自动驱逐未证明 |
| T11～T12 背压/COW冲突 | 默认只读与显式复制、MRK Worker现有能力 | 在线产线高频背压/原位独占写实测待做 |
| T13～T14 变量质量与订阅 | `IMDSExternalBindingResolver`的委托契约 | 旧Variable Owner的Quality/Subscribe真实接入待验 |
| T15～T16 血缘/Inspector | 轻量`Search/Inspect`、Run/Scope查询，文件不暗中Load | 持久Lineage及Workbench真实只读面板待接 |
| T17～T19 UI/元数据/配置固定 | 加载异步、专业配置版本在Vision结果中可追溯 | WPF响应/跨坐标/正式配置快照联调待做 |
| T20～T22 SHM/外部AI/GPU | MDS托管FileLoader迟到输出拒绝，可借鉴MRK ResultUnknown | SHM ABI、外部Operation活跃占用、GPU Fence均未实机验收 |
| T23 COW版本隔离 | 新派生副本与原始安全只读Buffer已测 | 通用版本化COW并不存在，不宣称已实现 |
| T24～T26 设备/回放/坏数据恢复 | MRK运行事件、错误路径、文件版本基本校验 | 真正设备ACK、确定性工业回放、损坏载体恢复仍待实测 |
| T27 跨Session权限 | 当前Node/Run显式输入隔离和子流程屏障 | 跨Session/跨用户授权与脱敏未做完 |
| T28 Lease清理 | 托管FlowStack/Heap逻辑保留与释放测试 | 外部Lease/Operation的1万次GPU/SHM故障尚未覆盖 |
| T29 全解决方案CI | Windows .NET48构建、ArchitectureSmoke、TestCenter有真实CI入口 | 每次需要核对**最新SHA**，不能复用旧成功记录 |
| T30 五大系统与案例 | 真实Vision CPU五节点链路程序化测试 | 五套历史系统与实际工程UI/实机端到端尚未全部验收 |

**原始V1.3的测试状态规范继续适用**：`DESIGNED / CODED / BUILT / AUTOMATED_PASS / MANUAL_PASS / HARDWARE_PASS / BLOCKED`。即便某项基本单测成功，也不能因为“有了对应类”就提升到`HARDWARE_PASS`。

## 12. 易用性专门验收：原U01～U14 + 后续EUX01～EUX12

### 12.1 V1.3原始U01—U14（用户动作及客观检查，保持原编号）

这14项不额外引入新类型，而是从R1/R3实际操作衡量复杂度。

| ID | 用户动作 | 客观检查 |
|---|---|---|
| U01 | 算法读取2个小值并返回1个结果 | 代码不手动new Heap/Push Stack/管理Lease |
| U02 | 缺少输入或类型错误 | 指向具体Input端口，给出可理解原因 |
| U03 | 一张Image扇出两算法 | 编排无需增加技术管理节点 |
| U04 | 插入三层子流程 | 只映射输入输出，自动产生Scope树 |
| U05 | 重入同一子流程N次 | N个Invocation完全隔离，结果无串值 |
| U06 | 子流程C失败 | 父没有半成品Output，旧输出未变化 |
| U07 | 单次运行修改阈值 | 已运行任务版本固定，下一任务按策略生效 |
| U08 | 连接Image与Volume错误端口 | 不执行就报错，提示转换途径 |
| U09 | 对CT选ROI | 可见ROI实际读量与预算，未整卷驻留 |
| U10 | HMI拖测点绑定 | Unit/Quality/Time可见，断线可识别 |
| U11 | 打开数据检查器 | 不显著改变被测Flow行为和内存占用 |
| U12 | 用户取消外部AI | 明确“请求取消”和“已实际结束”两种状态 |
| U13 | 两个Content实例并行 | 完全不同的运行数据作用域 |
| U14 | 导出一次失败用例 | 有输入Ref版本、ConfigRevision、期望、实际、Trace及未验证限制 |

### 12.2 EUX01～EUX12（V1.4易用性讨论补充）

| ID | 额外要求 |
|---|---|
| EUX01 | 输入Output声明后，R1不需额外注册同名全局数据 |
| EUX02 | 支持强类型Key而不是只能写字符串 |
| EUX03 | 普通`byte[]`不能出现可写别名，真正只读Buffer应可高效共享 |
| EUX04 | 可变专业DTO未注册安全规则时明确拒绝，不能默默浅复制 |
| EUX05 | Move/Copy结果清晰，同名冲突默认拒绝、显式覆盖 |
| EUX06 | 文件未加载可查，加载中Busy/取消/失败状态可诊断 |
| EUX07 | 专业Loader迟到时必须被当前Scope生命期拦截 |
| EUX08 | SourceBinding选择七类来源使用一致的数据类型和错误语义 |
| EUX09 | 下游NodeOutput须经MRK端口绑定；不能任意越权查其他节点值 |
| EUX10 | 可视候选按类型/来源/可用状态筛选，而不呈现不可用对象 |
| EUX11 | 编排界面支持“选Run→点节点→看数据”，生产模式默认只读 |
| EUX12 | 真实CPU Vision、CT/Automation逐步端到端跑通，最终需专业实机证据 |

**必须真实登记用例状态**：已存在的`Tests/ArchitectureSmoke/RuntimeDataLifecycleTests.cs`、`RuntimeDataStorageTests.cs`、`RuntimeDataHardeningTests.cs`、`RuntimeVisionDataFlowTests.cs`，以及`Tests/TestCenter/Cases/TCTRuntimeData*`、`TCTRuntimeVisionCases.cs`为实际测试入口。不是所有U/EUX都已在UI自动化中PASS。每个通过结论需给出**Git SHA、工作流、用例ID、真实日志**。

## 13. 面向Codex的分期实施工作包：从已完成M1～M4到真正自然易用

| 期次 | 目标与代码Owner | 可验收成果 | 不许做的事 |
|---|---|---|---|
| W0 对象规格/版本冻结 | 更新本V3.0、比对源码、清点类型 | 48+15状态清楚、现有声明无遗漏、API状态可核 | 不为凑原数量创建空壳 |
| W1 R1开发API补强 | FDT/MDS两个已有Data项目 | D01～D10全部可运行、关键单行API、强类型绑定可靠 | 不增加第三项目/第二执行器 |
| W2 R3真实参数选择器 | 现有Studio/Presentation项目仅**调用MDS后端** | Constant/NodeOutput/Heap/Variable/Resource/Expression一致界面和错误 | 不复制工程资源/PLC变量真源 |
| W3 R3流程节点与数据Inspector | 现有流程UI+MRK + MDS | 真正从UI运行Vision案例、按Run/Step查Input/Local/Committed | 不允许图上手工控制Push/Pop |
| W4 专业CT大数据 | Professional.CT3D+Data专业Loader适配 | 大文件ROI实际IO/RSS记录、取消、异常、共享安全 | 不用通用Heap假装实现CT重建/GPU |
| W5 Automation/IoT及生产验证 | 现有专业Owner+MRK | 质量、时间戳、权限、设备ACK、实机报告 | 不用Data.Return作为运动停止证明 |

**编码纪律**：直接master、中文Commit、保留既有项目结构；先看最新四大架构文档和GitHub代码；每一轮先写实际失败用例，再修复；Windows .NET48构建、ArchitectureSmoke和TestCenter都要有实测记录。若动到运行宿主，还应看统一运行体系TDD。不得只拿早期成功的SHA证明新提交通过。

## 14. 仍有必要明确的七项设计决策（不是擅自补进源码）

1. **数据范围层级**：当前FlowHeap拥有已提交数据；是否确需跨Flow/Session/Project缓存？若确需，必须先定义Owner/授权/关闭规则，不以进程单例绕开。
2. **真正专业不可变值**：对Vision Frame/Volume的冻结/安全发布如何标准化？`immutable:true`是承诺，需要专业实证；不要默认所有`Image`都不可写。
3. **容量保护的真实范围**：当前逻辑保留量预算不能约束瞬时Copy/GC/VRAM；是否需要为工业高负载引入额外峰值预算、分块及背压？
4. **跨进程/SDK资源**：仅在真实相机、共享内存、GPU、远程AI需要时补受控引用/Lease协议，并与外部Operation实际结束绑定。
5. **数据身份/路径**：当前运行时Name+Run/Scope、FileLocator已够基本访问；长期持久引用应采用何种稳定ID、版本/代际，避免只靠可重命名路径？
6. **运行Inspect可见性**：当前DataContext.Search不含所有CommittedOutput；工程界面从MRK只读结果提取多少摘要、留存多久、谁有权限看历史？
7. **数据与变量/历史统一体验**：Value+Quality+Timestamp、单位/坐标/来源血缘哪些字段归FDT基础，哪些仅属于现有专业Owner？先通过IoT和CT两类真实用例反推，不建镜像表。

### 14.1 推荐裁决方向（保留简洁而非重新复杂化）

- 常规业务保持`data.Get/Set/Return`绝对优先。
- Local跟节点，CommittedOutput跟Flow，Heap按显式保留与文件定位；其他Owner在需求明确时再扩。
- 类型、Shape、Quality/Provenance仅为真实工业语义增加必要基础描述，不强行把Vision/CT/Automation的专业类型迁入Data项目。
- 对远程AI/大CT/GPU必须处理真正外部资源生命周期时，**只在高级边界**增加租约或Carrier，不改变常规R1/R3用法。
- R3提供一套来源选择器和一套Inspect；真正重用MDS后端，而不是另造Workbench私有数据系统。

## 15. 文档来源、版本和核对清单

### 15.1 核对的实际文档

1. `DOCS/Architecture/CxVisual_统一数据管理体系 V1.3.md`：**48项历史候选、15枚举、D01～D09、V01～V08、T01～T30、U01～U14的源头**。当时的DataRef/Lease优先方案已被后期所有权决策调整，本版逐项保留去向而不是简单删掉。
2. `DOCS/Architecture/CxVisual_运行时数据体系_完整设计与实施总文档_V2.0.md`：堆栈所有权、五个开发阶段及真实实现边界；本版补齐其对象规格和双角色完整例子。
3. `DOCS/Architecture/CxVisual_运行时数据体系_M1M2_代码实现与验收记录_V1.1.md`：M1/M2实际代码与问题修复。
4. `DOCS/Architecture/CxVisual_运行时数据体系_M3M4_实现与验收基线_V1.0.md`：类型、文件、Query、Binding等真实API。
5. `DOCS/Architecture/CxVisual_运行时数据体系_核心加固与真实Vision闭环_实施验收_V2.1.md`：Flow逻辑预算、堆栈锁优化、真实8/16位Vision CPU执行与测试证据。
6. 当前master实际`Foundation/Data/Types`和`Mechanism/Data/RuntimeData`源文件；`Mechanism/Runtime/Execution`、`Professional/Vision`相关调用代码。

### 15.2 完整性核对（审稿者必须检查）

| 核对项 | 本版落点 | 应怎样验证 |
|---|---|---|
| 两个项目+MRK唯一Owner | `2` | csproj与项目依赖图 |
| 当前实际类/接口/枚举、核心方法 | `3、6` | 对照最新源码逐个查 |
| 历史48项候选，一个不漏 | `4.1–4.4` | F01–F15、M01–M33共48条 |
| 历史15枚举及现有新增枚举 | `5` | E01–E15及5个真实枚举 |
| R1/R3完整需求及六角色 | `1` | 用用户工作任务逐条复核 |
| R1 D01–D09 + D10补充 | `7` | 编译/测试并区分示意专业函数 |
| R3 V01–V08 | `8` | 人工真实Workbench点击验收 |
| 12个工业场景 | `10` | S01–S12 |
| T01–T30与U01–U14 | `11、12.1` | 保留原编号与现行反例 |
| EUX01–EUX12 | `12.2` | 用TestCenter/操作性测试验收 |
| M1～M4和M5边界 | `13、14` | 每项标已实现/待接入 |
| 真实Vision与容量/迟到IO | `7、11、15.1` | 对应源代码和Windows CI |
| 无法由测试证明的实机承诺 | `13、14` | 明确保留“不支持/未验证” |

### 15.3 单一权威规则

本V3.0是**完整类型规格+使用指南**，而V2.0继续保留为历史阶段汇总；二者冲突时，以本V3.0记录的**后期定案和最新源码**为准。每次实现新对象或更名，要同时更新`3`真实对象清单、`4～5`历史映射、`7～8`可用案例、`11～12`验收矩阵与`15`证据；避免再出现“新增一堆代码，总文档却只有摘要”的问题。

**最终产品形态**：开发者最少只需`Get/Set/Return`，工程编排用户最少只需“拖能力、连端口、选来源、运行、Inspect”；严谨的Heap/Stack/安全复制/预算/文件Load/生命周期在两Data项目内部承担复杂度。**易用体验最终必须由真实Vision和Workbench完整操作来证明**，不能由文档本身宣布完成。


## 16. 五维生命周期策略、独立便捷入口与完整验收（2026-10-27）

> **用户已明确提出的完整目标，尚非已实现声明。** 保留现有 Foundation.Data / Mechanism.Data 数据 Owner、MRK 执行器及 MEM 工程真源；不额外建立 DataManager、消息中心、编排引擎或 Scope 树。本节是 V3.0 的补充验收基线，不删除历史 F01～M33 / E01～E15，不通过增加类文件数冒充完工。

### 16.1 必须提供两层入口

- **普通开发者**：保持现有 Get/Set/Return 及按需 Heap/Move/Copy/File；不要求业务代码管理帧、消费数、租约与回收。
- **高级开发者**：五个维度均应有独立、强类型、可发现、可配置、可实际调用的策略类；每一类需具备配置校验、默认值、单独执行方法、状态查询、错误合同和正反例。提供一个组合使用入口，不要求一次配置全部五维。
- **工程编排者**：节点参数面板支持默认/高级两级策略，使用“共享只读、保留到、缓存多久、何时释放”等业务语言；策略与现有工程/能力定义联动，由 MOC/MRK 校验并交给原 MDS Owner 执行，不用业务图元模拟 Push/Pop/Lease。
- **专业开发者**：物理 GPU/SDK/SHM/远程 Operation 的资源释放由专业 Owner 最终负责，MDS 只维护安全使用、引用代次和结果登记；超时/取消绝非外部完成确认。

### 16.2 五维可独立存在的类及操作合同（名称均为候选，并非现有 API）

| 维度及候选类型 | 配置示例 | 独立方法及实际运行效果 | 不得违反的边界 |
|---|---|---|---|
| Ownership：MDSDataOwnershipPolicy | OwnerKind、OwnerId、Scope、是否允许转移 | Assign / TransferOwnership / InspectOwner：确定唯一终身释放责任和合法迁移 | 不创建第二份可写数据，外部物理资源仍由专业 Provider 负责 |
| Lifetime：MDSDataLifetimePolicy | Node / UntilLastConsumer / Flow / Session / ExternalBound；Invocation、Attempt | AcquireUse / CompleteUse / CanAccess：实际消费者保活、旧代次拒绝 | Loop、Join、Retry、子流程和外部异步必须按实际结算，不能单靠静态节点数 |
| Retention：MDSDataRetentionPolicy | None / ExplicitCache / Result / Diagnostic / Persistent / RecoveryProtected；TTL、容量、优先级 | Retain / ReleaseRetention / GetReasons：有界保留、说明保留理由 | 被缓存可驱逐不等于仍在使用的物理数据可释放 |
| Transfer：MDSDataTransferPolicy | Copy / Move / ImmutableShare / ExternalBorrow；冲突处理、目标 Owner | CopyTo / MoveTo / ShareReadOnly / Borrow：复制隔离、转交所有权或受控共享 | 延用原 Heap/Stack；跨进程和 SDK 借用须先实现 Generation 与外部确认 |
| Reclamation：MDSDataReclamationPolicy | Explicit / AfterLastConsumer / OnFlowEnd / AfterTTL / OnPressure / ExternalConfirmed；拒绝/等待/驱逐 | CanRelease / GetBlockers / ReleaseEligible / Sweep：安全判定、执行回收与反馈阻断原因 | 不因内存压力释放仍有消费者、Result/Debug 保留或 GPU Fence/Unknown |

组合策略门面候选 MDSDataLifecyclePolicy：应能单独设置五个独立策略，也能校验五维组合冲突。**策略不拥有副本**；真正数据仍归现有 Heap / Stack / Frame；新增类必须先核对既有契约，允许合并已存在职责，但便利入口不可缺失。

### 16.3 默认及组合行为原则

1. 普通标量和既有 Flow 不自动启用昂贵跟踪，不静默改变当前 Heap.Set/Get/Remove、Stack.Publish/Dispose 公共语义；高级策略可显式 opt-in。平台依据 MRK 的调用和编译依赖自动完成正常生命周期。
2. 安全释放判定：OwnerPermits 且 NoActiveConsumers 且 NoRetentionHold 且 ExternalSafe。未知外部状态必须明确拒绝危险回收并能诊断，而不是超时后强制销毁。
3. Ownership、Lifetime、Retention、Transfer、Reclamation 五个维度可独立配置、单独查询、自由组合；冲突组合（例如外部仍使用且强制回收）必须在编译前或执行前明确拒绝。
4. CommittedOutput 默认保持已有安全合同。若要引入提前回收，先让 MOC/MRK 提供准确的调用/循环/Join/结果导出消费者信息，再由 MDS 按真实终态安全释放；保守持有胜于早释放。
5. 容量应区分逻辑保留量、物理共享内存、在途复制、外部显存/SDK、诊断保留。预算不足只允许驱逐符合资格的缓存、施加背压或拒绝新任务，不能损坏活跃数据。
6. 流程结束、错误、取消、重试、补偿、断点、远程迟到回包、崩溃恢复都要定义独立清理和保留语义；结果输出经安全所有权交接后方可关闭旧 Scope。
7. 数据来源的稳定工程身份、运行时调用身份和外部 Generation 不得混用；内存管理与数据访问授权均应可审计。

### 16.4 开发者体验的目标示例（伪代码，不可当作已有 API）

~~~csharp
// 已有简洁入口不变：
var image = data.Get<MyImage>("Image");
data.Return("Result", Detect(image));

// 高级入口示意：只配置确实需要改变的策略，不要求五维全填。
var policy = MDSDataLifecyclePolicy.Default
    .WithLifetime(l => l.UntilLastConsumer())
    .WithRetention(r => r.CacheFor(TimeSpan.FromMinutes(2)))
    .WithTransfer(t => t.ShareImmutableOnly())
    .WithReclamation(r => r.OnPressureOrExpiry());
// 每维还须可独立设置/查询并实际作用于原 MDS 数据 Owner。
~~~

### 16.5 完整验收清单（不能只交五个 DTO）

| 用例 | 必须真实证明 |
|---|---|
| LC01 标量/节点 | 简洁 Get/Set/Return、错误 Pending 不发布、局部生命周期无泄漏 |
| LC02 两个并行消费者 | 不可变共享零意外修改；其中一人取消，另一人仍能安全读；CopyBytes 可观测 |
| LC03 1000 次 Loop/Foreach/Join | 每个 Invocation、Iteration/Attempt 隔离；可回收数据不会无限积累，也不能提前回收聚合结果 |
| LC04 多层 Subflow/Export | 未公开输出不能窥探；已公开输出直到外层安全接收才释放 |
| LC05 TTL/缓存/结果保留 | 各策略独立生效；有活动读者时压力驱逐拒绝，GetBlockers 能说明原因 |
| LC06 失败/取消/Retry | 不发布半成品、不重复释放；Heap 已有副作用不能假装回滚；迟到旧 Attempt 被拒绝 |
| LC07 CT Volume/ROI | 数 GB 文件只读取请求分块，量测磁盘 I/O、峰值 RSS、卸载与消费者安全 |
| LC08 GPU/SDK/SHM/远程 AI | 资源代次、外部 Fence/ACK/Unknown、旧回包与安全回收，独立于软件取消 |
| LC09 Studio/TestCenter | 五维各自配置、组合设置、运行观察、阻断诊断、UI 自动化和用户操作文档 |
| LC10 工业连续运行 | 长稳、GC、p95/p99、CopyBytes、PeakBytes、LiveConsumers、RetentionReasons、ReleaseBlocked |

每一项分开登记 DESIGNED / CODED / BUILT / AUTOMATED_PASS / UI_PASS / HARDWARE_PASS；通过 Windows x64 全解、专项 ArchitectureSmoke、TestCenter 正反例及对应实机验收后才能标完成。无法取得设备则列 HARDWARE_PENDING，不得伪称完成。

**这份要求是“完好完整”的验收合同，而不是当前五维能力都已落地的声明。**


## 17. 五维生命周期真实API、组合使用与验收台账（2026-10-28）

> 本节区分已经可调用的源码与尚未完成的 V3.0 目标。五维独立类及组合入口已落到 Mechanism.Data 的原有 Heap；是托管 Heap 级真实能力，不等于整个 MDS/MRK/Professional 全类生命周期已完成。

### 17.1 真实开发者入口

~~~csharp
using (var heap = new MDSDataHeap())
{
    heap.Set("Image", new byte[] { 1, 2, 3 });

    // 每维可独立设置。
    heap.Lifecycle.ConfigureOwnership("Image",
        MDSDataOwnershipPolicy.Default.WithMove(false));
    heap.Lifecycle.ConfigureTransfer("Image", MDSDataTransferPolicy.CopyOnly);

    // 也可组合设置：构建时校验冲突。
    var policy = MDSDataLifecyclePolicy.Default
        .WithLifetime(MDSDataLifetimePolicy.UntilLastConsumer())
        .WithRetention(MDSDataRetentionPolicy.CacheFor(TimeSpan.FromMinutes(2)))
        .WithReclamation(MDSDataReclamationPolicy.OnExpiry);
    heap.Lifecycle.Configure("Image", policy);

    using (var use = heap.Lifecycle.AcquireRead<byte[]>("Image"))
    using (var retained = heap.Lifecycle.Retain("Image", "DebugInspector"))
    {
        byte[] image = use.Read();
        var snapshot = heap.Lifecycle.Inspect("Image");
        // snapshot.OwnerId / ActiveConsumers / RetentionReasons / Blockers
    }
    // 真实时间到期可Sweep()；测试可传入明确UTC时间。
    int released = heap.Lifecycle.Sweep();
}
~~~

- 普通算法仍使用 Get / Set / Return，不必配置五维；高级开发者才选按条目配置。
- Ownership、Lifetime、Retention、Transfer、Reclamation 五种 Policy 均可分别配置；组合策略需要相容，未设置ConsumerBound不能使用AfterLastConsumer，未设置TTL不能使用OnExpiry，Pinned不能与自动回收组合。
- AcquireUse、AcquireRead<T>、Retain(reason)、CloseConsumerAdmission、GetPolicy、Inspect、GetBlockers、ReleaseEligible、Sweep 是原Heap附着的便利方法，不另建数据Owner；移动、复制仍用原 Heap.MoveTo/CopyTo。
- 活跃读取/保留会阻止删除、覆盖、转移、卸载、关闭Heap；ConsumerBound尚未关闭入场时还会显示FutureConsumersMayEnter并禁止自动回收。只读Copy允许复制且不使旧值失效。TTL到期的文件缓存只清载荷、保留Locator，之后可按需Load。
- 容量压力：Reclamation.OnPressure或OnExpiryOrPressure必须配合明确的TTL可驱逐缓存；原Heap.Set/Replace及文件Load预算不足时先试着释放**闲置合格缓存**，预算最终仍由MDSDataBudget原子拒绝。MDSDataLifecycleAccess.SweepPressure(requiredBytes)提供独立手动压力入口，返回真实释放的**逻辑载荷字节数**。不会跨其它Heap、GPU、SDK或运行中MRK CommittedOutput强制回收。
- 时间策略的含义：CacheFor从配置生效时或文件重新加载完成时计时。调试日期注入仅用于确定性测试。ExternalBorrow、GPU Fence、SHM与远程AI并未因此获得真实释放安全保证。

### 17.2 实际状态与待做边界

| 归属 | 已提交源码 | 尚未完成 |
|---|---|---|
| Foundation.Data | 保留原类型安全、专业Adapter与只读Buffer | 完整专业外部Carrier合同 |
| Mechanism.Data / Heap | 五维独立与组合配置、校验、消费者保活、具名Retention、Copy/Move拒绝、TTL/手动回收与阻断、预算释放 | Session数据池、跨进程、安全外部借用、精确物理占用 |
| Mechanism.Data / Stack | 原来的Pending/Committed原子提交与Flow结束回收保持 | CommittedOutput按MRK真实消费者自动回收，Loop/Join/Export完整安全 |
| MRK/MOC/MEM | 原有类型端口、Run/Invocation、计划编译 | 生命周期编译决策、动态消费证明、工程策略持久化 |
| TestCenter | 人工工作区 manual.data-lifecycle：五维独立/组合真实WPF按钮、保留与阻断诊断；自动案例TCT.MDS.030/031 | 固定SHA完成全量CI、Studio统一编辑器与生产UI体验 |
| Professional | 原各专业对象与数据Owner继续复用 | CT真实ROI、GPU/SHM/SDK Fence、远程AI Unknown、真实设备长稳 |

第一版策略/Heap与ArchitectureSmoke在固定提交 118cac13 的 Runtime Windows CI #40612892480 验证成功。后续固定提交6cd267a的 Runtime Windows CI #40613614820 成功，包含文件TTL回归。但这些绿灯不能自动覆盖最新的具名保留/强类型使用Lease、完整TestCenter WPF等较新增量；新SHA必须分别核对。

### 17.3 完成判据

继续按上节 LC01～LC10 验收。独立类存在、API可以编译或部分CI绿色均不等于全部完成。只有五维各自可配置且有真实运行效果、组合行为正确、动态MRK消费者/内存资源可安全结算、Studio/TestCenter自然操作、工业外部资源及压力/故障完成对应等级实测，才能称完整完好。实机不可用时列 HARDWARE_PENDING，不伪称通过。


## 18. W3：CommittedOutput逐帧结算与静态DAG自动回收（2026-10-28）

> 本章是**已提交源码与分级验收说明**，不是W3全类完成。默认保留既有Flow作用域、现有MDS单一数据Owner及MRK执行引擎；新机制明确opt-in。

### 18.1 五维协作与真实使用入口

| Owner | 新源码及真实入口 | 已实现行为 |
|---|---|---|
| Mechanism.Data / MDSDataStack | ReleaseCommittedOutputs(invocationId) | 单个已关闭调用帧的所有Committed输出安全释放；活动帧拒绝、不存在返回false、预算按帧归还；其它帧及FlowHeap保持 |
| Mechanism.Data / MDSDataStack | CommittedFrameCount | 已保留调用帧数的只读运行指标 |
| Mechanism.Runtime / MRKExecutionPlan | reclaimCommittedWhenConsumersSettle: true | 在构建静态DAG计划时显式启用；默认false不改变旧流程 |
| Mechanism.Runtime / MRKCommittedOutputReclaimer | 内部真实节点终态结算 | 依赖图按不同消费节点计数；Completed/Skipped/Failed/Cancelled都结算；最后消费者完成且生产Frame已关闭后释放；DataExports保留直到FlowResult成功捕获 |
| Mechanism.Runtime / MRKCodeFlowBuilder | ReclaimConsumedData() | 开发者一行启用，经原MRKFlowCompileDefinition / OrchestrationCompiler生成同一个MRKExecutionPlan |

### 18.2 代码优先的便捷使用（现有API）

~~~csharp
var builder = new MRKCodeRuntimeBuilder()
    .DataCapability("Acquire", (ctx, ct) => {
        ctx.Data.Return("Image", imageBytes);
        return Task.FromResult((object)true);
    }, new Dictionary<string, Type> { { "Image", typeof(byte[]) } })
    .DataCapability("Inspect", (ctx, ct) => {
        ctx.Data.Return("Count", ctx.Data.Get<byte[]>("Input").Length);
        return Task.FromResult((object)true);
    }, new Dictionary<string, Type> { { "Count", typeof(int) } },
       new[] { new MRKCapabilityInput("Input", typeof(byte[])) })
    .Flow("Flow", f => f.ReclaimConsumedData()
       .Step("image", "Acquire")
       .StepData("inspect", "Inspect", new[] {
           new MRKExecutionInput("Input", MRKInputSource.StepOutput,
               "image.Image", typeof(byte[]))
       }, "image")
       .Export("Result", "inspect.Count"));
~~~

普通算法代码依然只用Get/Set/Return，开发者不需管理内部帧及引用计数。仅在完整消费关系由具名StepOutput和DataExports声明时建议显式启用。手动ReleaseCommittedOutputs需由调用方证明所有后续合法访问已结束。

### 18.3 安全边界与缺口

- 当前自动优化严格限定**静态DAG**。开启优化的Plan遇到Loop、Foreach、Try或ControlRegion时明确抛ArgumentException；这些区域仍保留Flow级Committed历史，避免提前破坏CollectLoopOutputs<T>、动态Join与Retry。不能宣称动态场景已闭环。
- 业务若需要未声明的后续历史访问、断点调试或Inspect，应维持保守默认，后续补正式有界诊断保留合同。
- 外部GPU、SDK Buffer、SHM、跨进程AI所需Fence/ACK/Generation不由静态节点结算推断；受控外部资源仍归对应专业Owner。
- 该优化针对MDS已提交的**托管逻辑载荷预算**；不代表总RSS、GC暂存、显存和外部专业资源占用量。
- 长循环逐轮回收、跨Session结果交接、Studio/编排者策略选择、自动回收观测与长稳p95/p99，仍属后续独立工作，不能用目前的类名或短时测试冒充完成。

### 18.4 固定源码验收

- 固定源码SHA fa3a541dcb89：MDS生命周期Windows CI [#40644037833](https://github.com/oudream/cx-visual-x3/actions/runs/40644037833) **Success**；包含.NET48 Release x64全解、RuntimeData存储加固、静态DAG双消费者正反例、TestCenter独立用例与真实WPF测试。按当前FDTDataSize保守逻辑标量每项32B计费，150B测试预算能证明最后消费后释放使流程成功，而默认整Flow保留在相同预算下明确失败。
- 后续MRKCodeFlowBuilder便利入口及Loop拒绝反例为更晚提交，必须在其固定SHA CI通过后另行标注；不得将旧绿灯冒充最新验证。
- 完整Runtime统一回归若在其它模块失败，须区分本阶段MDS专项成功与全解回归未通过。
