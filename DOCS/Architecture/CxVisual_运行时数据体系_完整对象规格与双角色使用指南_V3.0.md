# CxVisual 运行时数据体系——完整对象规格与双角色使用指南 V3.0

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
| `Mechanism.Runtime.Execution` | `MRKExecutionPlan, MRKExecutionStep, MRKExecutionInput, MRKInputSource, MRKExecutionEngine, MRKExecutionContext, MRKFlowResult` | 唯一节点调度与Input/Output映射、成功提交、子流程屏障、最终Flow结果 |
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

