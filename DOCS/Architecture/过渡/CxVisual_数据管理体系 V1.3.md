# CxVisual 统一数据管理体系 V1.3

## 软件开发者与工程编排用户易用性 · 完整类型体系 · 数据堆栈运行契约与验收

> 版本：**V1.3 设计讨论稿**｜日期：2026-10-16｜继承 V1.0 → V1.1 → V1.2。  
> 目标：**使用简单、内部可靠、两层归属清晰、对象体系完整、案例能验证**。  
> 约束：**不增加解决方案项目**；数据类型/编码等在 `CxVisual.Foundation.Data`，运行数据机制在 `CxVisual.Mechanism.Data`；复用已有 Runtime/Config/Orchestration/Visualization，不建第二套。  
> 注意：本文的公开 API 均为**目标签名/伪代码**，不表示已经在 x3 实现或通过编译。**48 个类/接口/值类型 + 15 个枚举是完整候选清单，不是强制新建数量**；定案前要与 V6.4、现有源码和历史复用资产逐项去重。

## 0. 一页结论：先易用，再保证语义

**软件开发者**希望把数据视为注入到能力中的 `data`：读小值、写局部、返回结果一行完成；图像/CT/张量只拿固定版本引用，短期读取用 `Lease`。无需自己 `new Heap`、调用 `Push/Pop`，也无需管理线程切换。

**工程编排用户**希望拖能力、连有类型的端口、配置少量专业参数、运行看结果；子流程输入输出自动映射，分支并行只读共享，失败/超时有清晰出口。画布不出现 Heap、Stack、Lease 等技术节点。

**框架内部**：

```text
专业能力代码 / Studio图形编排
          └─────── 同一份能力/端口契约 ───────┐
                                            ↓
                                IMDSDataContext（轻量入口）
                                   ↓           ↓
                           MDSDataScope/Frame  MDSDataHeap/Entry
                           Input/Local/Output   Ref/Version/Owner
                                   └──────┬────┘
                                   Binding/Ref/Lease
                                          ↓
                       Carrier（Memory / MappedFile / SHM / ...）
                                          ↓
                        History / Notice / Inspector（非权威副本）
                ↑ MRK Runtime：执行/Join/异常/取消/资源安全边界
                ↑ MHS Configuration：配置真源与任务版本
```

三项本版必须定清的契约：**统一简单入口；子流程输出原子提交；异步并行引用不早释放**。另外锁定第四项：**配置、变量、数据、运行状态、历史各自只有一个权威真源**。

## 1. 两类用户的明确期望与验收口径

### 1.1 R1：软件开发者——尽可能只写业务，不写数据框架

| 开发动作 | 最理想的代码 | 平台默认完成 | 错误必须显式暴露 |
|---|---|---|---|
| 读阈值 | `data.Get<int>("Threshold")` | 从已验证Input/Local读取 | 未声明、缺失、类型不兼容 |
| 写临时值 | `data.Set("Score", 0.96)` | 作用域、Schema、实例隔离 | 跨Scope越权写 |
| 交付结果 | `data.Return("Result", result)` | 先暂存Output，成功后统一提交 | 输出未声明、重复写冲突 |
| 得到图像引用 | `data.Ref("Image")` | 固定DataId/Generation/Version | 旧版本或无权限 |
| 登记新大数据 | `data.Put("Image", image)` | 默认安全所有权与只读共享 | 非托管Buffer所有权不明确 |
| 读取大数据ROI | `await data.AcquireReadAsync(ref, roi, ct)` | 读租约、范围校验、预算、载入 | 越界、内存不足、载体故障 |
| 处理子流程 | `await runtime.RunChildAsync(...)` | Input/Output映射与成功提交 | 部分输出、失败半写入 |
| 并行检测 | `await Task.WhenAll(...)` | 读共享、Join前有效 | 取消、外部迟到、隐式可变别名 |
| 观察数据 | `data.Inspect()` | 轻量、只读、可追踪 | 无权限查看、超量数据泄露 |

**易用性等级**：L0 参数/值/返回只1行；L1 大数据额外显式 `Lease`；L2 子流程/并行只需声明映射；L3 COW、跨进程和独占写使用高级Options；L4 自定义Carrier才接触底层。

### 1.2 R3：工程编排用户——只做业务，不管理栈和堆

| 用户操作 | UI应如何呈现 | 平台背后的自动机制 | UI验收 |
|---|---|---|---|
| 拖“采图” | 自带Image输出端口 | 从能力契约读取Shape/Type/Unit | 不手工维护第二份端口定义 |
| 拖“AI检测”连Image | 数据连线可直接连接 | Schema/版本/权限验证 | 错类型即时拒绝并解释 |
| 插入子流程 | 自动出现Input/Output映射框 | 调用帧、独立Scope、输入映射、提交 | 无需Push/Pop节点 |
| 两路检测并行 | 从同一个Image端口扇出 | 版本固定、提前取得读租约 | 不复制两张原图（如载体支持共享） |
| 失败/超时处理 | 从失败/超时端口连接处置 | Abort局部输出、延迟回执隔离 | 不输出伪合格、不串SN |
| 打开数据检查器 | Input/Local/PendingOutput/CommittedOutput | Snapshot、血缘、占用、Lease | 打开窗口不加载8GB全量CT |
| 复用Content/Symbol | 两个站点同一可复用定义 | 每个实例独立Scope/InstanceId | 两台机器不串配方和缺陷结果 |

界面主流程：**拖节点 → 连端口 → 填业务参数 → 验证 → 运行 → 对比/追溯**。“高级设置”中才允许改数据驻留位置、超时保留、写策略、内存预算、跨进程传递模式。

### 1.3 倒推优先级：行为比类型数量重要

| 优先级 | 必须证明的价值 | 对外最多接触 | 内部必需行为 |
|---|---|---|---|
| P0 | 小值1行，三层子流程不串值，Return不漏出半成品 | `IMDSDataContext` | Scope/Frame、SlotSchema、AtomicOutputCommit |
| P0 | 单图AI/CV并行读，不早释放 | `MDSDataRef`/`MDSDataLease` | 版本、Heap/Entry、Lease、Owner、Join规则 |
| P0 | 同一个编排可代码执行也可UI执行 | 统一端口契约 | Orchestration编译映射到DataSlot，不造第二工程树 |
| P0 | 参数改版不污染执行中任务 | 旧 `MHSConfigSnapshot<T>` | 固定ConfigRevision，来源绑定 |
| P1 | CT ROI与跨进程AI高吞吐 | `AcquireReadAsync`/高级选项 | Carrier、配额、Generation、外部Operation持有 |
| P1 | 运行数据诊断与复盘 | `Inspect()` | 脱敏、Version、Lineage、只读摘要 |

## 2. V1.3 公共API使用门面与最小契约

### 2.1 开发者面向的接口（候选伪C#）

```csharp
// 所有API均为V1.3拟定签名，不表示仓库已实现。
public interface IMDSDataContext
{
    T Get<T>(string slot);
    bool TryGet<T>(string slot, out T value); // 仅“未找到”返回false，类型/越权仍报错
    void Set<T>(string slot, T value);        // Local/授权输出；不偷改父级
    MDSDataRef Put<T>(string slot, T value);   // 默认安全复制或可靠托管，公开只读版本
    MDSDataRef Ref(string slot);              // 固定版本，非指向“最新值”的活动指针
    void Return<T>(string output, T value);   // 暂存Output；不是立即向父可见
    Task<MDSDataLease> AcquireReadAsync(
        MDSDataRef reference, FDTDataRange range, CancellationToken cancellation);
    MDSDataSnapshot Inspect();               // 只读轻量摘要，不加载全量载体
}
```

**默认逻辑：** `Get/Set`优先强类型小值；`Put/Ref/Lease`用于Image/Volume/Tensor/Buffer以及长寿命共享数据；`Return`只暂存已声明的Output。配置、Runtime、设备控制不通过本接口实现。

**显式高级机制**由适配器/宿主提供：`Put(..., Ownership, WritePolicy, CarrierOptions)`；独占写/COW；Buffer所有权转移；长期保留；跨进程迁移与显存Fence。普通开发者不需要看到这些API。

### 2.2 统一运行入口不属于Data项目

```csharp
// 目标使用式：以下 RunChildAsync 尚不是现有MRKExecutionEngine公开API。
// 子流程名+Input映射+Output结果；其调度、取消、结束状态归MRK Runtime。
MRKFlowResult result = await runtime.RunChildAsync(
    "Inspect.SubFlow", data,
    new Dictionary<string, object> { { "Threshold", threshold } },
    cancellationToken);

int area = result.GetCommittedOutput<int>("Area"); // 目标扩展方法，仅访问已提交输出
```

上面复用现有 `MRKFlowResult` 作为目标返回形态，`RunChildAsync` 和 `GetCommittedOutput<T>` 均为**拟补的运行时API**，不在48候选之外新增M10权威实体。正式实现应扩展**仅包含已提交Output的只读视图**，不要直接把当前 `MRKFlowResult.Results`（节点执行结果快照）冒充已提交子流程输出。当子流程失败、取消或提交校验失败时必须抛明确异常或返回Failure，父级不得取得未提交Output。

### 2.3 反例：本版明确废弃的含糊用法

```csharp
using (IMDSDataContext c = parent.CreateChild("C", "C-01"))
{
    c.Return("Area", Compute());
}
int area = parent.Get<int>("Area"); // 错误假设：Dispose自动代表执行成功、自动提交
```

理由：`Dispose`代表资源回收，不应暗含业务成功。**必须由运行引擎在子流程正常完成、输出Schema验证、资源交接准备成功后统一 Commit**，才能让父级读取。异常/取消/超时始终 Abort。

### 2.4 不变量（需要代码强制而非文档口号）

1. `MDSDataRef = HeapId + DataId + Generation + Version + TypeId`；固定版本，不是“最新值”的可变指针。
2. 输入默认只读，只有显式映射的父输入可见；父级Local不自动继承。
3. Output先暂存、再**全部验证与准备**、最后单次发布为父级可见；提交失败不得出现部分结果。
4. Scope关闭只回收局部所有权；在读租约/外部操作未安全解除时不释放底层Buffer。
5. 多消费者默认只读共享；写入需独占或产生新版本（COW）；`Get<T>`不得暴露可变引用越过约束。
6. 数据引用可来自内存、文件、SHM或GPU，但载体能力不同；不承诺跨进程/跨GPU自动零拷贝。
7. 所有共享数据受配额/释放策略约束；超限不能静默驱逐正在使用的对象。
8. Flow/Work/ConfigVersion/ResultId关联一致；异步迟到结果无法修改已完成Scope。
9. HMI/IOT/设备变量真源归M09；配置真源归MHS Configuration；DataHeap不是第二套设备测点数据库。
10. `Inspect`不读大数据主体、不改变Lease/Pin状态，并有权限、脱敏和限流。

## 3. 完整类、接口、枚举候选（V1.2逐项继承、V1.3增强契约）

### 3.1 标记与归属

- **F14/F18/F21** → `CxVisual.Foundation.Data`：只读值、类型、Schema、Buffer、元数据、基础编码/文件工具；**不持有运行时全局状态**。
- **M09/M10/M35/M36** → `CxVisual.Mechanism.Data`：变量；Heap/Scope/Stack/Carrier；历史与谱系；通知。
- `公`=必要公共契约，`内`=可为internal/实现细节，`后`=保留设计位、按需求后续实现；P0首个闭环、P1增强、P2高级。
- 前缀 `FDT/MVB/MDS/MHS/MNM` 属于**待 V6.4 最终核准的工作名**。`MHSConfig*`、`MNMConfig*` 已存在，以下Data类型**不得重建同义配置能力**。

### 3.2 Foundation.Data——原14项完整保留 + 新增范围类型（15项）

| # | 类型 | 形态/暴露 | 关键属性或方法 | Owner/阶段 |
|---|---|---|---|---|
| F01 | `FDTDataTypeId` | 值类型·公 | `TypeKey,Version`; `Parse,Equals` | F14/P0 |
| F02 | `FDTDataTypeDescriptor` | 元数据类·公 | `ClrType,Fields,Nullable`; `ValidateValue,CompatibleWith` | F14/P0 |
| F03 | `FDTDataShape` | 值类型·公 | `Dimensions,Strides,ElementType`; `Validate,ByteCount` | F14/P0 |
| F04 | `FDTDataValue` | 不可变小值·公 | `TypeId,Value,Metadata`; `Get<T>` | F14/P0 |
| F05 | `FDTDataRecord` | 不可变描述·公 | `Id,Version,CreatedAt`; `WithMetadata` | F14/P0 |
| F06 | `FDTDataMetadata` | 元数据·公 | `Unit,FrameId,ClockDomain,Timestamp,Quality` | F21/P0 |
| F07 | `FDTDataProvenance` | 来源记录·公 | `SourceId,WorkId,ConfigRevision,AlgorithmVersion` | F21/P0 |
| F08 | `FDTDataBufferDescriptor` | 描述符·公 | `Length,Alignment,Endian,ReadOnly`; `ValidateRange` | F14/P0 |
| F09 | `IFDTBufferView` | 接口·公 | `Length,IsReadOnly`; `ReadRange,Slice` | F14/P0 |
| F10 | `FDTBinarySlice` | 值类型·内/后 | `Offset,Count,Stride`; `BoundsCheck` | F14/P1 |
| F11 | `FDTDataCodecRegistry` | 注册服务·后 | `Register,Encode,Decode`（**勿与FCM配置Codec重建同义类**） | F14/P1 |
| F12 | `FDTDataChecksum` | 静态工具·内/后 | `Compute,Verify` | F18/P1 |
| F13 | `FDTDataSchemaValidator` | 纯校验·公 | `Validate,Diff` | F14/P0 |
| F14 | `FDTDataPathUtil` | 静态工具·内/后 | `ResolveSafePath` | F18/P1 |
| F15* | `FDTDataRange` | 范围值类型·公 | `Start[],Length[],Stride[]`; `ValidateAgainst(Shape)` | F14/P0 |

`FDTDataRange` 是V1.1示例实际用到但原14项未正式列入的补项；不在 Foundation.Data 中实现新的Image/Volume类型，实体仍由 Foundation.Imaging / Foundation.Volume 管理。

### 3.3 Mechanism.Data / M09——变量、实时测点（原5+补1，共6项）

| # | 类型 | 形态/暴露 | 关键属性或方法 | 阶段 |
|---|---|---|---|---|
| M01 | `MVBVariable<T>` | 实体·内/公 | `Id,Quality,Timestamp`; `Read,Commit` | P0 |
| M02 | `MVBVariableStore` | 会话服务·公 | `Read<T>,CommitBatch,Subscribe` | P0 |
| M03 | `MVBVariableSnapshot` | 不可变快照·公 | `Value,OldValue,Quality,Timestamp,Source,Revision` | P0 |
| M04 | `MVBVariableBinding` | 绑定语义·公 | `SourceRef,TargetSlot,Converter`; `Validate,Resolve` | P0 |
| M05 | `IMVBValueConverter` | 接口·后 | `InputType,OutputType`; `Convert,Validate` | P1 |
| M06* | `MVBVariableSubscription` | `IDisposable`订阅凭证·公 | `Dispose`、订阅Id、取消回调、限流配置 | P0 |

`MVBVariableSubscription` 为代码体验补项：不等于M36的全平台通知订阅。变量的Quality与Timestamp不得因传入数据堆而丢失。

### 3.4 Mechanism.Data / M10——堆、栈、引用与载体（原19+补2，共21项）

| # | 类型 | 形态/暴露 | 关键属性或方法 | 阶段 |
|---|---|---|---|---|
| M07 | `MDSDataRef` | 不可变引用·公 | `HeapId,DataId,Generation,Version,TypeId`；验证/序列化稳定标识 | P0 |
| M08 | `MDSDataHeap` | 实例服务·内 | `Put,Resolve,PublishVersion,Release,GetMetrics` | P0 |
| M09 | `MDSDataEntry` | 内部记录·内 | `Owner,Carrier,State,Metadata,Versions` | P0 |
| M10 | `MDSDataScope` | 作用域对象·内/公 | `ScopeId,Parent,Input/Local/PendingOutput`; `CreateChild,Resolve,StageOutput,CommitOutputs,Abort` | P0 |
| M11 | `MDSDataStack` | 调用路径视图·内 | `PushFrame,PopFrame,GetFrames`；**非全局线程栈** | P0 |
| M12 | `MDSDataFrame` | 每次调用帧·内 | `InvocationId,InputBindings,Locals,PendingOutputs,CommitState` | P0 |
| M13 | `MDSDataSlotBinding` | 绑定记录·内 | `SlotId,TypeId,Ref,Access`; `Validate` | P0 |
| M14 | `MDSDataHandle<T>` | 类型化句柄·公/高级 | `AcquireRead,AcquireWrite`（高级调用；版本固定） | P0 |
| M15 | `MDSDataLease` | `IDisposable`凭证·公 | `DataId,Version,Access,Range`; `As<T>,Dispose` | P0 |
| M16 | `IMDSDataCarrier` | 存储适配接口·公/高级 | `Load/Unload,Acquire,Capabilities` | P0 |
| M17 | `MDSMemoryCarrier` | 内存实现·内 | `Read,Write,Evict` | P0 |
| M18 | `MDSMappedFileCarrier` | 映射文件实现·后 | `OpenWindow,Close` | P1 |
| M19 | `MDSSharedMemoryCarrier` | 共享内存实现·后 | `ABI,Generation,Open,Lease,Close` | P1 |
| M20 | `MDSCarrierRegistry` | 适配注册表·内/后 | `Select,Register,Resolve` | P1 |
| M21 | `MDSDataQuotaPolicy` | 配额策略·内/配置 | `MaxBytes,High/LowWater`; `Evaluate` | P0 |
| M22 | `MDSDataEvictionPolicy` | 驱逐策略·内/后 | `SelectVictims,Retention` | P1 |
| M23 | `MDSDataTransferService` | 迁移服务·后 | `TransferAsync,Verify` | P2 |
| M24 | `MDSDataSnapshot` | 只读摘要·公 | `ScopeId,Refs,Versions,Bytes,Owners`; `Compare` | P0 |
| M25 | `MDSDataLifecycleMonitor` | 诊断服务·内/高级 | `LiveLeases,Bytes,Evictions`; `GetSnapshot` | P0 |
| M26* | `IMDSDataContext` | **首要便捷接口·公** | `Set/Get/Put/Ref/Return/AcquireReadAsync/Inspect`；内部由Scope完成输入绑定与提交 | P0 |
| M27* | `MDSDataException` | 结构化异常·公 | `Code,Slot,DataId,FlowId,Expected/Actual`; 不吞错 | P0 |

**数目核对**：M10原V1.1列表实际为 **19 项**（MDSDataRef至MDSDataLifecycleMonitor），加2项为21。普通用户默认只见 `IMDSDataContext/MDSDataRef/MDSDataLease`，而不是21个新入口。`MDSDataHandle<T>`是否公开可待实际性能测试决定。

### 3.5 Mechanism.Data / M35、M36——历史谱系与通知（原6项完整保留）

| # | 类型 | 形态/暴露 | 关键属性或方法 | 阶段 |
|---|---|---|---|---|
| M28 | `MHSDataChangeJournal` | 历史变化服务·内/公 | `Append,Query`，序列号、持久化策略 | P1 |
| M29 | `MHSDataLineageIndex` | 谱系索引服务·内/公 | `Trace,Compare`，生产者、父数据、ConfigRevision | P1 |
| M30 | `MHSDataCheckpoint` | 恢复点记录·后 | `Plan,DataRefs,ConfigRevision`; `Validate,RestoreRefs` | P2 |
| M31 | `MNMDataChangeNotice` | 事件契约·公 | `DataId,Version,ChangeKind`; `Validate` | P0 |
| M32 | `MNMDataSubscription` | 订阅凭证·公/高级 | `Filter,Backpressure,Dispose` | P1 |
| M33 | `MNMDataNotificationBridge` | 发布适配·内 | `Publish,Subscribe`；复用既有消息基础设施 | P1 |

**合计**：原 V1.1 的14 F +30 M = **44项**；本稿明确增加 `FDTDataRange`、`MVBVariableSubscription`、`IMDSDataContext`、`MDSDataException` **4项**，为**48项类/接口/值类型候选**。这不是开发时必须创建48个文件的指令；同义类型优先合并到现有对象。

### 3.6 必须定义的枚举体系（建议15项；名称与成员待定案）

| 编号 | 枚举/Owner | 最小成员及语义 | 使用者 |
|---|---|---|---|
| E01 | `FDTDataKind` / F14 | `Scalar,Object,List,Map,Buffer,Image,Volume,Tensor,Stream` | 类型Schema |
| E02 | `FDTByteOrder` / F14 | `LittleEndian,BigEndian` | 二进制/跨进程 |
| E03 | `FDTDataQuality` / F21 | `Good,Uncertain,Bad,Stale,Unknown` | 值/变量质量 |
| E04 | `FDTTypeCompatibility` / F14 | `Exact,Compatible,RequiresConversion,Incompatible` | 端口验证 |
| E05 | `MDSDataSlotKind` / M10 | `Input,Local,Output,Shared` | Scope/Frame |
| E06 | `MDSDataAccessMode` / M10 | `Read,Write,ReadWrite` | Binding/Lease |
| E07 | `MDSDataOwnership` / M10 | `Copied,Transferred,Borrowed` | Put/Carrier |
| E08 | `MDSDataWritePolicy` / M10 | `ReadOnly,CopyOnWrite,ExclusiveMutable` | Put/修改 |
| E09 | `MDSCarrierKind` / M10 | `Memory,MappedFile,SharedMemory,Gpu,Remote` | Carrier选择；Gpu/Remote先预留 |
| E10 | `MDSCarrierState` / M10 | `Unloaded,Loading,Ready,Unloading,Failed` | 载体观察 |
| E11 | `MDSDataState` / M10 | `Registered,Available,Retained,Evicting,Released,Faulted` | 生命周期 |
| E12 | `MDSDataRetention` / M10 | `Scope,Session,Pinned,Persisted` | 保留策略 |
| E13 | `MDSQuotaAction` / M10 | `Reject,Wait,EvictEligible` | 内存预算 |
| E14 | `MDSDataErrorCode` / M10 | `NotFound,TypeMismatch,ScopeDenied,StaleGeneration,LeaseConflict,QuotaExceeded,CarrierUnavailable,Corrupt,Expired,Cancelled` | 结构化错误 |
| E15 | `MNMDataChangeKind` / M36 | `Created,Published,Updated,Released,QualityChanged` | 通知/追溯 |

约定：这里的 `Gpu/Remote` 只是 Carrier 类型枚举的**未来扩展值**，不是承诺已经存在跨GPU、CUDA或跨网络实现。`MDSDataWritePolicy` 与 `MDSDataOwnership` 正交：前者是共享/修改策略，后者回答底层Buffer由谁负责释放。`Put` 的普通重载使用明确的默认Ownership；接管外部采图Buffer等场景须用高级重载或Options显式指定，且在Carrier中核实生命周期。正式API可采用单一Options类型合并参数，但语义不可丢。

### 3.7 既有体系对接（**不新建同名类型**）

| 已有边界/实例 | 继续承担 | 数据体系只做什么 |
|---|---|---|
| `MHSConfigService` / `MHSConfigSection<T>` / `MHSConfigSnapshot<T>`（配置V1.4） | 强类型配置、版本、保存、任务固定 | 仅把 `ConfigRevision` 链接到数据来源；不做第二配置仓 |
| `MRKExecutionContext` / `MRKExecutionEngine`（Runtime） | Flow/Work身份、调度、执行状态、取消与Join | Runtime**持有或注入**唯一DataContext；旧Inputs/Results兼容迁移、禁止长期双写 |
| `Mechanism.Orchestration` / `Mechanism.Core` | 能力输入输出定义、端口、连接、Content/Symbol工程定义 | 编排器生成运行时SlotBinding/Scope，不在M10持久化第二工程树 |
| `FCRWorker`、`FCRSharedLease`（Foundation.Infrastructure） | 线程/队列/基础并发原语 | 复用已有安全原语；数据Lease与硬件资源Lease不同 |
| `Foundation.Imaging / Volume` | 像素、体素、基础算法/数据结构 | Data仅持有描述、引用和可用载体，不复制领域类型 |
| `Professional.*` 与设备Provider | 算法/设备协议、安全互锁/ACK | 提交结构化结果或DataRef；不以数据完成冒充硬件执行确认 |


## 4. 数据栈/Scope：执行中到底发生什么（P0契约）

### 4.1 它不是线程栈，而是“调用树 + 当前路径视图”

```text
Flow A (SN001, Scope=A, 配方版本=V8)
 ├─ B#01 Acquire（输入SN，局部相机参数）
 │    └─ C#01 Detect（输入ImageRef和Threshold，暂存Defects）
 └─ B#02 ExtraInspect（输入相同ImageRef，独立局部参数）

ScopeTree：保存父子关系、权限与生命期，支持并行分支。
DataStack：某个调用链 A→B#01→C#01 的Frame观察视图，不能以线程ID索引。
```

| 层次 | 数据内容 | 能访问什么 | 生命周期 |
|---|---|---|---|
| Flow A | SN、产品信息、ConfigRevision、输入Ref | 自己声明的输入/局部/已提交输出 | 整次Flow |
| B#01 | 采集请求、局部状态、Image输出草案 | 显式映射的A输入、B本地 | 当前子流程调用 |
| C#01 | Threshold、ROI、AI结果草案 | 显式映射的B输入、C本地 | 当前节点/子流程 |
| B#02 | 复检参数、复检结果 | 仅自己映射的Input | 并行的另一个Scope |

- `Input`：在创建Frame时绑定，默认只读；小值防共享可变引用，大对象固定 `DataRef` 版本。
- `Local`：当前Scope可写，仅本调用拥有；并行调用同名Local互不影响。
- `Output`：必须在能力契约里声明，写入先成为 `PendingOutput`；执行正常结束后才整体可见。
- `Shared`：只适用于有明确授权与生命期的对象；不因为同一名字、同一Flow就自动共享。
- `Return`：**声明输出意图**，不是 `Commit`，也不是普通`Set`到父Scope。
- `Get`：按照“当前Local → 当前已绑定Input → 当前已提交可见Output”策略解析；父Local不隐式搜索。真正槽优先级由编译期SlotId绑定固定，不靠模糊名字动态猜测。

### 4.2 作用域状态（逻辑规则，不必新增独立枚举）

`Created → Bound → Executing → OutputsStaged → PreparingCommit → Committed → Closed`；发生异常、取消、超时、校验失败则 `→ Aborting → Aborted → Closed`。`Committed`之后禁止修改当前Invocation的输出。

父Scope看不到子Scope的 `Local` 和 `PendingOutput`，只能看到子流程成功提交后的显式Output绑定；同一个子流程可多实例，并发调用各自具备唯一InvocationId，禁止只按子流程名称做Scope键。

### 4.3 子流程输出原子交接：六步提交协议

| 步骤 | 由谁负责 | 动作 | 失败怎么处理 |
|---|---|---|---|
| 1. Bind | Orchestration→Data | 检查输入映射、Type/Shape/Unit、访问权 | 拒绝创建/启动；父不变 |
| 2. Execute | MRK Runtime | 执行业务，`Return`写PendingOutput | Error/Cancel → Abort |
| 3. Prepare | MDSDataScope | 验证所有必需输出、类型、权限、版本以及Lease/Owner转移可行性 | 完整撤销准备，不触及父可见集合 |
| 4. Reserve | Heap/Owner | 为即将移交的大对象预留Owner或有效Lease，确定配额 | 失败则释放预留，父不变 |
| 5. Publish | MDSDataScope | **以父Scope为单位单次不可分割地发布Output映射/版本** | 外界只能见旧快照或完整新快照 |
| 6. Close | Runtime→Data | 释放子Local和非转移资源，保留已经交出的有效所有权 | Close异常记录诊断，不回滚已经发布的父输出 |

**原子性范围**：这里保证的是**进程内父Scope输出可见性的逻辑原子性**；不暗示已实现跨多进程、磁盘、多数据库或硬件动作的分布式原子事务。流程可选择全部必需输出到齐后一次提交；如果需要流式逐项输出，须明确标成 `Stream/Event` 端口，不能借用普通Return伪装增量提交。

**命名与冲突**：父目标Slot在提交前必须已声明映射；两个子流程同时写同一父Slot时，默认拒绝（`OutputConflict`）；允许的合并只能经显式 `Join/Reduce` 节点、版本条件或单写策略。默认不使用“最后写入覆盖”。

**幂等性**：提交按 `(FlowId, InvocationId, OutputCommitId)` 幂等确认；同一次成功重复通知不得发布第二个版本，过期Invocation不得提交。这里的OutputCommitId可属于现有Frame内部字段，不必额外建立对外类。

### 4.4 输入映射和输出映射是两张表，不是直接指针

```text
工程定义（Orchestration/Core）：
  A.Image → B.Input.Image
  B.Output.Defects → A.Input.DefectsFromB
运行实例（Data / Runtime）：
  (A#01, A.Image, Version=3) → (B#17, Input.Image)
  (B#17, Output.Defects, Committed=4) → (A#01, DefectsFromB)
```

工程保存的是端口、类型契约、绑定表达式/稳定标识；运行时生成 `MDSDataSlotBinding`。**不能把可复用的Content定义、Symbol定义中的节点Id当成每次执行的实例Id**。配置与结果应分别记录来源版本。

### 4.5 成功、失败、取消、超时的结果语义

| 情况 | 返回值/父数据 | 子Scope资源 | 外部操作 |
|---|---|---|---|
| 全部成功且Commit成功 | 父取得所有声明输出 | Local清理，已移交Owner继续有效 | 必要时关联确认记录 |
| 算法抛异常 | 父没有新输出 | 清理已退出执行的Lease和Local | 失败Trace留证据 |
| 缺少必填输出 | 整次Commit失败，无部分输出 | 回收草案 | 明确指出缺失端口 |
| 用户取消，所有工作确认停止 | Abort、父不变 | 可释放已安全结束的资源 | 已确认停止后结束外部保留 |
| 调用方超时，但AI/硬件仍可能在运行 | 父不接纳迟到输出 | 外部Operation持有的资源**不能盲释放** | 状态设Unknown/Running，做隔离与收敛 |
| 两个分支合并失败 | Join不提交总输出 | 各分支安全回收/证据保留 | 禁止默认判OK |

## 5. DataHeap/DataRef/Lease/Carrier：数据不早释放（P0契约）

### 5.1 关键身份、引用和物理载体

| 概念 | 精确定义 | 反例 |
|---|---|---|
| DataEntry | 同一逻辑DataId的管理记录；跟踪所有权、版本、载体、大小、状态 | 用裸`object`字典当堆，无法知道谁仍在用 |
| DataRef | 指向**特定**HeapId/DataId/Generation/Version/TypeId | 总是读取“最新值”导致运行结果前后不一致 |
| Owner | 决定一份数据**为何仍需保留**的主体；可能为Scope、Session、外部操作 | 子节点一结束就Dispose外部传输中的Buffer |
| ReadLease | 仅允许对特定Ref版本/区域读取、作用期间阻止不安全回收 | 把Ref直接当永久有效的内存地址 |
| WriteLease | 明确Exclusive或COW；提交成为新版本 | AI和CV同时修改同一帧图像 |
| Carrier | 数据如何驻留/加载：托管内存、映射文件、共享内存等 | 把所有8GB CT对象强行搬进托管大数组 |
| Retention | 决定何时可退出缓存或需要持久化 | `Pinned`当永久不释放开关使用 |

**一个Ref不是一个Lease**：Ref能跨节点长期保存元数据，但使用前仍需 `AcquireRead...` 取得有效Lease；在不支持位置透明的场景会复制或拒绝，绝不能默默让跨机器裸指针生效。

### 5.2 “先占用、后投递、再执行”避免并发早释放

```text
Frame已持有ImageRef
  ① 预先取得分支A和B的有效ReadLease（也可由Runtime先预留）
  ② 同时提交AI/CV工作，两份Lease分别由分支持有
  ③ 等待完成/失败/安全取消；Join校验分支结果
  ④ 完成的分支归还Lease
  ⑤ Join完成后释放不再需要的Flow Owner
  ⑥ 计数/Retention允许时才驱逐Carrier
```

如果 `Task` 已进入 `Running` 且调用方仅超时返回，**不能以取消令牌触发就直接销毁Lease**；需交由仍存活的任务/Provider持有，直到实际操作确认停止、隔离退出或安全恢复策略完成。注意若外部设备已执行但ACK丢失，要保留 `OperationId` 的状态，不可用本地取消代替现场确认。

### 5.3 共享/可变/所有权矩阵

| 输入数据 | 默认使用 | 需要修改时 | 释放条件 |
|---|---|---|---|
| int/string/不可变业务小值 | `Get<T>`或不可变值快照 | 写本Scope新值 | Frame结束，已Return的结果由父接管 |
| List/Map可变对象 | 默认只读/隔离快照 | COW或独占对象副本 | 最后Owner/Lease结束 |
| Camera图像Buffer | `Put`后产生固定只读Ref | 新版本/COW；接管需显式声明 | 不能早于采集SDK释放约束和消费者结束 |
| CT Volume/ROI | 固定Ref+按需窗口Lease | 新ROI结果、新Volume版本 | 不驱逐有活动Lease的窗口/块 |
| SHM跨进程 | 带Generation/ABI校验的Ref映射 | 独立写协议、提交版本 | 接收方确认释放且Owner可回收 |
| GPU Tensor | 带显存上下文/Fence的载体 | 由GPU Adapter管理版本 | GPU操作完成事件/同步点确认后 |

**必须区分** `Copied`/`Transferred`/`Borrowed` 与 `ReadOnly`/`CopyOnWrite`/`ExclusiveMutable`。`Borrowed`数据只能在外部Owner保证存活的窗口内借用，不能无限期留在Heap。公共 `Put(value)` 默认为能安全掌控生命周期的方式；对非托管相机Buffer要求业务层显式选择且由Adapter验收。

### 5.4 版本与状态机

- `Registered → Available → Retained / Evicting → Released`，`Loading/Unloading`属于物理Carrier状态，不与逻辑对象状态混用；错误进入Faulted且诊断可查。
- `DataId`标识逻辑对象，`Version`标识内容快照，`Generation`标识载体身份的代际（例如SHM重启），“最新版本”查询需另走明确API，现有Ref永不自动跳版本。
- 新版本发布和旧版本读租约可以并存；只回收没有Owner/Lease、且Retention允许的版本。读写竞争默认拒绝或COW，不进行未告知的就地覆盖。
- 别把“引用数为零”当作唯一销毁依据：缓存策略、Session Owner、历史取证、跨进程Lease以及GPU Fence可能影响回收。

### 5.5 配额、背压和数据清理

为内存/映射文件/共享内存/显存分别建立预算，统计 `ResidentBytes, LogicalBytes, PinnedBytes, LeasedBytes, PeakBytes, Evictions, CopyBytes`；不要求每个对象初期都有独立Tracker，可由Heap/Carrier合成快照。

高水位到达时按 `Reject/Wait/EvictEligible` 执行；**不可丢弃正在执行、已有外部副作用或不允许丢弃的工作**。正在等待的任务不应无界占用Buffer；数据配额拒绝必须携带SN、FlowId、DataId及明确失败原因。

## 6. 与 Configuration V1.4 / Runtime / Orchestration 的唯一真源及迁移

| 体系 | 已有Owner/代码 | 数据机制应做的事 | 严禁重复 |
|---|---|---|---|
| 配置 | `MHSConfigService`、`MHSConfigSection<T>`、`MHSConfigSnapshot<T>` | 把任务固定ConfigVersion写入来源；需要配置时直接使用Config快照 | 再建ConfigManager、修改配置权威字段 |
| Runtime | `MRKExecutionContext/Engine/Plan` | 注入DataContext；创建Flow/Invocation作用域、驱动Commit/Abort、关联Trace | 再建Work调度器/第二套运行状态机 |
| Orchestration / Core | 统一能力契约、输入输出端口、Content/Symbol、工程对象树 | 编译/运行时创建DataSlot绑定 | 在M10再存一棵Graph树、另一份端口定义 |
| M09变量 | `MVBVariableStore`目标 | 设备测点值+Quality+Timestamp+Source为权威 | 让Heap变成平行实时数据库 |
| M35历史/M36通知 | ChangeJournal、Notification（候选） | 记录完成事实和变化摘要 | 让历史/通知异步副本代表事务成功 |
| Imaging/Volume | 图像/体素结构与专业能力 | 数据类型、Ref和Carrier资源承载 | 重新定义图像/体数据算法库 |
| F19 | `FCRWorker`等基础并发 | 复用线程队列机制 | M10自行起一套调度系统 |

**MRK接入迁移三阶段：** ① 添加只读 `DataContext` 访问，旧 `Inputs/Results` 维持兼容观察；② 把运行路径统一转向MDS，旧字段成为MDS派生视图（只读、不双写）；③ 全回归/旧调用迁移完成后逐步移除旧可变字典入口。任何阶段都应保证**同一输入和结果只有一个权威数据版本**。

**Config接入规则：** 每次Flow/Task开始捕获MHS快照，随Flow/Invocation传递版本及受控强类型值；即使Studio后续保存新Revision，既有任务直到约定边界都不能切换参数。Data系统只存 `ConfigRevision`/`ConfigRef` 的来源关联，而不是复制实现配置事务。

**基础项目依赖**：`Foundation.Data`不得引用 `Mechanism.*` / WPF / 专业SDK；`Mechanism.Data`可依赖 `Foundation.Data`，但不得因为 DataContext 注入而反向引用 `Mechanism.Runtime`（推荐由Runtime依赖Mechanism.Data，接口/装配在上层）。测试和UI通过Adapter访问公开契约。


## 7. R1 软件开发者：8个从输入到输出的最小代码案例

> 所有代码属于**目标体验伪API**；`Camera/AI/CV/CT/runtime`为示例Provider对象。当前x3尚未实现这些统一数据入口，不能直接复制编译。示例默认 `data` 与 `CancellationToken token` 由 Runtime/测试宿主注入，已根据能力契约编译出类型化端口。

### D01 单个数值算法——3行完成

```csharp
int threshold = data.Get<int>("Threshold");
int defectArea = MeasureDefectArea(image, threshold);
data.Return("DefectArea", defectArea);
```

**只需声明**Input `Threshold:int`、`Image:ImageRef`和Output `DefectArea:int`；输出在此次执行成功后自动Commit。非法阈值/缺少端口直接报到节点。*简化重点：不暴露Heap/Stack/Schema操作。*

### D02 X-Ray采图 → AI/CV并行 → 合并结果

```csharp
// 目标接口：相机返回可安全受管的图像载体；裸SDK Buffer需显式高级所有权配置。
MDSDataRef imageRef = data.Put("Image", capturedImage);
FDTDataRange all = ImageRanges.All(capturedImage); // 业务示例，不是新增F.Data类型
using (MDSDataLease aiRead = await data.AcquireReadAsync(imageRef, all, token))
using (MDSDataLease cvRead = await data.AcquireReadAsync(imageRef, all, token))
{
    // 两份读租约在投递给两个并行任务之前取得；由各Task持有到安全结束。
    Task<AiDefects> aiJob = ai.DetectAsync(aiRead, token);
    Task<CvDefects> cvJob = cv.DetectAsync(cvRead, token);
    await Task.WhenAll(aiJob, cvJob);
    data.Return("Defects", Merge(aiJob.Result, cvJob.Result));
}
```

**业务结果**：Image只有一个受管理的DataId/Version，可共享则两算法读取同一底层Buffer；Join全部成功才产生Defects。一路抛异常，父流程得不到部分Defects。注意 `ImageRanges.All` 是示意函数，可用已验证的Shape构造`FDTDataRange`，不能视为已经存在。

**取消注意**：外部异步AI即使调用方本地超时，其Provider可能仍在执行；实际集成必须让Provider继续安全持有Lease或已独立持有相应资源，不能简单按超时释放共享内存。

### D03 三层子流程 A → B → C，结果在成功后才可见

```csharp
// A：已有 data.Get<string>("SN") 和 data.Get<int>("Threshold")
MRKFlowResult b = await runtime.RunChildAsync( // Runtime目标接口，尚未实装
    "AcquireAndInspect", data,
    new Dictionary<string, object> { { "SN", data.Get<string>("SN") },
                                     { "Threshold", data.Get<int>("Threshold") } }, token);
if (b.State != MRKFlowState.Completed) throw new InvalidOperationException("B未完成");
data.Return("FinalArea", b.GetCommittedOutput<int>("Area")); // 只读B已提交Output（目标扩展）

// B的实现回调：
MRKFlowResult c = await runtime.RunChildAsync(
    "DetectArea", bData,
    new Dictionary<string, object> { { "Threshold", bData.Get<int>("Threshold") },
                                     { "Image", bData.Ref("Image") } }, token);
if (c.State != MRKFlowState.Completed) throw new InvalidOperationException("C未完成");
bData.Return("Area", c.GetCommittedOutput<int>("Area")); // 暂存B输出（目标扩展）

// C的实现回调：
int area = CalculateArea(cData.Ref("Image"), cData.Get<int>("Threshold"));
cData.Return("Area", area); // 暂存C输出；仅当C成功终止才能提交给B
```

**正确理解**：以上展示三个相互独立的回调片段，不是连续在一个方法中执行；`bData/cData`由Runtime创建。C的`Return`不会自动出现在B.Local；C成功Commit后，按预先声明的 `C.Area → B.<映射槽>` 把结果暴露给B。失败不出现半成品，也不会把其它同时运行的B#02结果串进B#01。

### D04 CT：读取8GB体数据的ROI，不整块复制

```csharp
MDSDataRef volume = data.Ref("CTVolume");
FDTDataRange roi = BuildRoi(300, 400, 100, 256, 256, 128); // 示例辅助函数
using (MDSDataLease window = await data.AcquireReadAsync(volume, roi, token))
{
    IVolumeView view = window.As<IVolumeView>(); // 专业只读视图；由Volume适配器提供
    data.Return("VoidRate", CalculateVoidRate(view));
}
```

**验证**：2000×2000×1000、16位单通道裸体素是8,000,000,000 bytes ≈7.45GiB；本次读取的ROI约16MiB（256×256×128×2字节），加上实现的分块/边界开销。Carrier必须证明没有因小ROI自动整块载入8GB，失效ROI明确报错。`IVolumeView`属于专业视图占位名，不在本数据子系统48项候选内强制新增。

### D05 机台变量/IoT测点：质量、时间与订阅

```csharp
MVBVariableSnapshot point = variables.Read("Line01.Pressure");
Display(point.Value, point.Quality, point.Timestamp);
using (MVBVariableSubscription sub = variables.Subscribe(
           "Line01.Pressure", x => UpdatePressure(x)))
{
    await WaitUntilStoppedAsync(token);
}
```

**验证**：传感器断线时Quality变为Stale/Bad，显示灰化并显示最后可信时间；变量数据真源在 `MVBVariableStore`，历史和通知为派生，不因HMI订阅而产生第二个可写测点仓。遥控现场设备需要独立的专业授权与ACK，不是`variables.Read`的反向写入。

### D06 当前检测任务固定配置V8，后台保存V9

```csharp
using (MHSConfigSnapshot<InspectOptions> cfg = configSection.CaptureForTask())
{
    InspectOptions options = cfg.Get();
    data.Set("ConfigRevision", cfg.Version);
    InspectResult r = await InspectAsync(data.Ref("Image"), options, token);
    data.Return("Result", r);
}
```

MHS保存到V9后，**此任务**仍按V8执行。Config系统管理`CaptureForTask`、更新与快照，Data负责在结果来源保留关联，而非重新解析配置。每个实际AI请求还要关联 `FlowId/OperationId/ImageRef.Version`，防止迟到回包串产品。

### D07 同一张图希望增强，但不能覆盖检测中的原图

```csharp
MDSDataRef original = data.Ref("Image");
using (MDSDataLease lease = await data.AcquireReadAsync(original, fullRoi, token))
{
    ImageBuffer enhanced = Enhance(lease.As<IReadOnlyImage>()); // 输出为新受管载体
    MDSDataRef improved = data.Put("Enhanced", enhanced);
    data.Return("EnhancedImage", improved); // 新DataRef；Original保持不可变
}
```

增强后的图可以是新DataId或相同逻辑对象的新Version（需在能力契约里选定），但不能暗中修改AI正在使用的`original`。Buffer交由Heap管理前须确认所有权、生命周期和异常释放。静态小值优化不替代共享可变图像的COW规则。

### D08 跨进程AI推理：请求超时，但共享图像不能被误释放

```csharp
MDSDataRef imageRef = data.Ref("Image");
// aiBridge负责打包DataRef、分配跨进程可读Carrier、持有外部Operation租约。
AiRequestTicket request = await aiBridge.SubmitAsync(imageRef, token);
AiReply reply = await aiBridge.WaitAsync(request, TimeSpan.FromSeconds(3), token);
if (reply.IsConfirmed && reply.FlowId == flowId && reply.ImageVersion == imageRef.Version)
    data.Return("AIResult", reply.Result);
else
    throw new TimeoutException("推理结果未确认，进入复判或错误分支");
```

**关键**：调用方超时≠外部AI已经取消；Bridge必须独立追踪Operation、SHM Generation、数据租约与迟到结果，在安全确认后释放。该例中的`AiRequestTicket/AiReply`是专业通信示意，不是再新增两个M10核心类型。

### D09（补充）编排以外普通代码直接提交带数据上下文的任务

```csharp
MRKFlowHandle handle = runtime.StartFlow(compiledPlan, inputs);
MRKFlowResult done = await handle.Completion;
if (done.State != MRKFlowState.Completed) HandleFailure(done);
else ShowResult(done.GetCommittedOutput<Decision>("FinalDecision")); // 目标扩展，非现有API
```

这是当前 `MRKExecutionEngine` 类似的已有入口形状（以实际程序集签名为准），未来应从底层接到统一的DataContext，并给`MRKFlowResult`补充只读CommittedOutputs/`GetCommittedOutput<T>`；开发者无需为了使用数据管理机制而先画流程图，Runtime亦不需为普通 `Post` 创建完整Flow。

## 8. R3 工程编排用户：8个完整“拖→连→填→测→看”案例

### V01 X-Ray六节点检测：一图双算法、合流后判定

```text
相机采图.Image ──┬──> AI检测.DefectsAI ──┐
                └──> 传统视觉.DefectsCV ─┤→ 合并缺陷 → 判定 → 保存/MES
```

1. 拖“相机、AI检测、传统视觉、合并、判定、保存”六个节点；平台自动显示每个Node的Input/Output及类型。
2. 从Image扇出两个连接；只需填AI模型、传统参数、判定阈值、错误出口。
3. 点击“验证”，若Image不是所需像素格式/ROI越界/输入缺失，标红对应端口并说明具体原因。
4. 点击“模拟运行”，看到SN、ImageRef版本、两路耗时、实际缺陷与判定来源。
5. 强制AI超时，必须转错误/复判，**不能在传统检测通过时默默忽略AI失败而判OK**（除非配置明示允许的降级规则）。

**反推**：类型/端口属于Foundation.Data与原Orchestration；Heap/Lease/OutputCommit属于M10；MRK负责Join与异常分支；M35负责结果来源。**可验收**：原图只被Heap登记一次，AI/CV两个租约均有效，最后一位使用者结束后可回收。

### V02 主流程→采图子流程→检测子流程：不用画Push/Pop

1. 主流程输入 `SN, Recipe`；插入“采图子流程B”，自动弹出Input映射视图。
2. B内部插入“检测子流程C”，将 `Image → C.Image`、`Recipe.Threshold → C.Threshold`连接。
3. 将 `C.Defects → B.Defects → A.Defects`映射成Output；UI只显示业务端口。
4. 运行后数据检查器展示 `A#01/B#01/C#01`；各Frame可见Input/Local/PendingOutput、调用状态。
5. 中途让C抛异常：C.Output不会显示在B，B也不会错误提交到A；再同时运行B#02证明同名数据不串值。

**反推**：编排器产生静态映射；运行时生成ScopeTree； `StageOutput → Prepare/Reserve → Publish/Close` 在内部完成。

### V03 CT重建：一个Volume供MPR、体绘制、检测三路使用

1. 拖Projection Loader → CT Reconstruct，获取 `Volume` 输出。
2. 从Volume分别连MPR、Volume Render、Inspection；只配置重建算法、ROI、窗宽窗位和检测算法。
3. 点击MPR的一个小ROI，检查只加载对应窗口；关闭MPR窗口不影响正在运行的Inspection。
4. 观察面板显示VolumeId/Version、数据驻留位置、已加载范围、Lease数量和估算字节；不会预读取完整体数据。
5. 模拟内存预算不足：必须返回明确错误或等待，而不是无上限抢内存导致进程崩溃。

**反推**：Range/Shape、Carrier/Lease/Quota/Retention、只读Inspector；实际3D渲染仍在CT3D专业层。

### V04 IoT/SCADA仪表绑定：从点表直接拖变量

1. 从设备/点表拖 `Line01.Pressure` 到仪表Value端口；Unit/Quality/Timestamp自动带入。
2. 同一测点绑定曲线和报警；设置采样与更新节流即可。
3. 模拟断线：仪表灰化并显示过期时间，历史记录保留最后可信样本；远程控制按钮必须另作权限及设备ACK流程。
4. 恢复连接：展示新Revision及来源，不把旧Stale静默显示为Good。

**反推**：M09 VariableStore/Binding/Subscription，M35 Journal，M36 Notice；Connector/Provider仍归原平台边界。

### V05 同一Content在两条产线复用

1. 通过`ContentRef`分别实例化检测工位`LineA`与`LineB`。
2. 每个实例绑定各自Camera、Recipe、Product/SN；不复制一份工程定义。
3. 同时运行两个产品，查看 `InstanceId/ScopeId/FlowId`不同。
4. 故意把LineA的局部缺陷结果引用连接给LineB，编译期拒绝越权，不因名字都叫`Result`就串值。

**反推**：Core的统一对象定义保持唯一；M10仅负责实例态数据和Ref；Runtime/Studio分别提供执行和查看。

### V06 AI失败复判：处理迟到结果和未知状态

1. 检测图编排为“AI初判 → [成功] 合并 → 判定”；另有 `[超时/未知] → 人工复判`。
2. 对同一SN的AI请求，编排器自动关联OperationId与ImageRef版本。
3. AI超时后流转到复判；三秒后迟到的AI结果不得写入已关闭的旧Scope，也不得串到下一个SN。
4. 若人工需要复盘，按保留策略继续拥有该图的受控数据引用，查看前仍须AcquireLease。

**反推**：MDSLease/Retention、MRK ExternalOperation/Correlation，MHSDataLineageIndex，专业AI桥。

### V07 运行调试：点节点就看“当时那次”的数据

1. 在测试工作台选Flow/SN → 选Step → 打开数据检查器。
2. 默认显示：Input、Local、PendingOutput、CommittedOutput、数据类型/版本/来源/时间戳、内存/ROI、Access、异常原因。
3. 选择某个CT数据时先显示Shape与预览缩略，不自动载入8GB；点击“取样ROI”需经受控读取及权限检查。
4. 生产只读模式下不能修改DataScope；模拟模式的测试数据注入应创建隔离Scope，不覆盖真实运行态。

**反推**：DataSnapshot、LifecycleMonitor、Lineage、WPF只读视图；输出状态必须区分Staged与Committed。

### V08 跨专业：Automation→Vision→CT3D→Inspection

1. 拖Automation扫描节点、Vision图像处理、CT3D重建、Inspection判定和MES保存。
2. 连接时平台检查业务对象ID、坐标系、ClockDomain、Image/Volume类型和版本，不兼容处要求显式转换能力。
3. 一次运行记录 `SN/FlowId/ConfigRevision/ImageVersion/VolumeVersion/ResultId`，可从最终结果反查上游输入、算法及运行版本。
4. 通过“验证”可提前发现无效的图像空间、引用越权、未连接的必需端口，而不是等产线出错才知道。

**反推**：FDTDataMetadata/Provenance/Shape/Schema，M10绑定，M35来源，专业能力Provider不被Data层复制。

## 9. 12类工业业务场景覆盖（保留V1.1范围）

| 编号 | 真实场景 | 主要验证数据机制 | 最能发现的失败 |
|---|---|---|---|
| S01 | X-Ray 2D在线双/三探测器 | 每产品SN隔离、图像Refs共享、背压 | 误释放、串产品、静默丢图 |
| S02 | 2D离线检测与人工复判 | 结果引用、版本固定、失败证据保留 | 修改历史结果导致不可追溯 |
| S03 | CT慢扫/大体积重建 | 大Buffer、分块、ROI、预算 | 无意整卷加载、OOM |
| S04 | CT MPR/体绘/分割/测量 | 一Volume多消费者、ReaderLease | 关闭窗口造成另一路崩溃 |
| S05 | 工业AI GPU/远程推理 | Tensor、SHM/IPC、Operation租约 | 超时早释放、迟到串单 |
| S06 | 运动控制/采图同步 | 轨迹配置版本、数据来源、时间域 | 数据完成冒充设备动作确认 |
| S07 | HMI / SCADA实时测点 | Quality、Timestamp、订阅、变化事件 | 断线显示正常值 |
| S08 | IoT边缘多协议网关 | 设备点表映射、变量共享、缓存 | 按设备名串变量或丢质量 |
| S09 | 三层/跨专业流程编排 | Frame/Scope、Output原子提交 | 半成品、并发同名覆盖 |
| S10 | 生产运行态诊断 | 轻量Inspector、Trace、内存租约 | 点开调试面板压垮产线 |
| S11 | 自动化测试/仿真 | 测试数据注入、隔离、重放 | 测试写入生产数据 |
| S12 | 断电/异常/流程恢复 | Checkpoint、已提交输出、代际与完整性 | 恢复半状态、陈旧Handle使用 |

**覆盖关系**：六大专业Workbench可以用S01～S12组合验证；先以S01/S03/S07/S09四条主链做纵向闭环，再逐步接五个历史系统与十大案例。性能、实机安全、跨进程ABI不能用纯单元测试“通过”替代。


## 10. T01—T30：完整可执行验收矩阵（保留并强化V1.1）

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

### 10.1 还必须额外跑的14项易用性验收（U01—U14）

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

### 10.2 通过标准、量化和不伪造的原则

- 功能的正确性按断言判定；涉及吞吐/延迟/峰值内存/复制量的性能目标应先测现有基线，再由现场约束制定阈值，不凭空规定“必须xx毫秒”。
- 强制收集 `FlowId, InvocationId, WorkId, InputRef, OutputRef, DataVersion, ConfigRevision, CopyBytes, LeasedBytes, PeakResidentBytes, Outcome`。
- 每条用例区分 **DESIGNED / CODED / BUILT / AUTOMATED_PASS / MANUAL_PASS / HARDWARE_PASS / BLOCKED**；不可把“文档写有测试”当作“验证已通过”。
- 资源回收测试要覆盖正常、异常、取消、超时、进程退出、外部结果Unknown六种路径。
- 对难以在本地模拟的GPU/SHM/工控设备项目，先有Mock/FaultInjection再有物理验收，分别记录，不越级宣称成功。

## 11. 完整类型体系的审核与裁决：保留48+15，但控制真正公开面

### 11.1 最小公开面、内部面和后续扩展

| 级别 | 类型范围 | 为什么这样分 |
|---|---|---|
| L0：普通开发者 | `IMDSDataContext`（常用方法），小值类型由编译器推断 | 避免一开始学习几十个框架类 |
| L1：大数据/并行 | `MDSDataRef`, `MDSDataLease`, `FDTDataRange` | 必须显式保证读取范围与占用寿命 |
| L2：专业数据和变量 | `FDTDataTypeDescriptor/Shape/Metadata`、`MVBVariableSnapshot/Store/Subscription` | 类型描述、质量和时间不能丢 |
| L3：能力作者/适配器 | `IMDSDataCarrier`, `MDSDataHandle<T>`，高级写策略 | 只有管理第三方SDK/SHM/GPU时使用 |
| Internal：平台维护者 | `MDSDataHeap/Entry/Scope/Stack/Frame/SlotBinding/Quota...` | 不向普通用户泄漏流程执行复杂度 |
| 后续扩展 | `MappedFile/SHM/Transfer/Checkpoint/CodecRegistry` | 先完成可验证P0闭环，再依工况扩展 |

**不能直接以48+15为“已完成”考核**。必须先从源码/规格查重，决定每个候选是 `Existing / Extend / New / Merge / Defer / Drop`；同义类不得在Foundation.Data和Mechanism.Data双重落地。`Drop`意味着语义被合并到更合适的现有类型，不能没有替代说明就删除能力。

### 11.2 P0核心类型的最低规格细则

| 候选类型 | 必须保留的字段/契约 | 关键方法语义 | 并发与错误边界 |
|---|---|---|---|
| `FDTDataTypeId/Descriptor` | TypeId、SchemaVersion、Compatibility | Validate/CompatibleWith | 类型未知不得隐式反序列化为object |
| `FDTDataShape/Range` | Dimensions/ElementType/Stride/ROI | ByteCount/ValidateAgainst | 溢出校验、越界拒绝 |
| `FDTDataMetadata/Provenance` | Unit/FrameId/Time/Quality/Source/Algorithm/ConfigRevision | Validate/With | 源信息不能在跨专业调用中丢失 |
| `MDSDataRef` | HeapId、DataId、Generation、Version、TypeId | Validate/Equals/Serialize | 不作为原始内存指针；旧Generation拒绝 |
| `MDSDataHeap/Entry` | Entry状态、VersionSet、OwnerSet、Carrier、Logical/ResidentBytes | Put/Resolve/Reserve/Release | 互斥保护/快照可见性，不持有UI对象 |
| `MDSDataScope/Frame` | ScopeId、Parent、InvocationId、Inputs、Locals、PendingOutputs | Bind/Stage/Prepare/Commit/Abort | 同一次提交整体可见；重复Commit幂等 |
| `MDSDataStack` | InvocationPath/Frame链 | Push/Pop/GetFrames | 仅逻辑路径视图，异步换线程不变 |
| `MDSDataSlotBinding` | SourceSlotId、TargetSlotId、Access、TypeId、Version | Validate/Resolve | 不继承父Local，显式映射 |
| `IMDSDataContext` | 当前Scope身份、已验证可见槽 | Get/Set/Put/Ref/Return/AcquireReadAsync/Inspect | 严格报告类型、权限、已关闭上下文 |
| `MDSDataLease` | DataId/Version/Range/Access/Owner | As/Dispose | 重复Dispose安全，Disposed后再读必须失败 |
| `IMDSDataCarrier/MemoryCarrier` | Location/Capabilities/State/Bytes | Acquire/Load/Unload/Release | 不支持ROI须报能力限制或可见代价 |
| `MDSDataQuotaPolicy` | Max/High/Low/Action | Evaluate | 无界队列与强行驱逐禁止 |
| `MDSDataSnapshot/LifecycleMonitor` | ScopeTree摘要、Lease/Bytes、版本与权限 | Inspect/GetSnapshot | 无副作用、脱敏、限速 |
| `MDSDataException` | ErrorCode、Slot、DataId、Flow/Invocation、Expected/Actual | ToDiagnostic | 不吞异常、不只返回false |
| `MVBVariableStore/Snapshot` | Value/Quality/Time/Source/Revision | Read/CommitBatch/Subscribe | 实时值只有一个真源，批次原子一致 |

**枚举增补而不另建枚举**：V1.2的E14 `MDSDataErrorCode` 至少应补 `OutputNotDeclared, OutputConflict, OutputMissing, InvalidState, CommitRejected, PermissionDenied, OperationPending`。这些是本版原子交接与外部操作必须区分的错误原因。注意 `OperationPending` 与实际硬件安全确认仍属于Runtime/专业Provider的事实，不应由Data错误码单方面决定现场动作。

### 11.3 两个Data项目内部目录建议（不新增csproj）

```text
Foundation/Data/
 ├─ Configuration/              已有；保留FCM V1.4
 ├─ Types/                      FDT Type/Shape/Value/Range（候选）
 ├─ Metadata/                   FDT Metadata/Provenance（候选）
 ├─ Buffers/                    FDT BufferView/Slice（候选）
 ├─ Serialization/              FDT Codec/Checksum（候选）
 └─ CxVisual.Foundation.Data.csproj
Mechanism/Data/
 ├─ Configuration/              已有；保留MHS/MNM V1.4
 ├─ Variables/                  M09（候选）
 ├─ RuntimeData/                M10 Scope/Frame/Heap/Ref/Lease（候选）
 ├─ Carriers/                   M10存储载体/预算（候选）
 ├─ History/                    M35（候选）
 ├─ Notification/               M36（候选）
 └─ CxVisual.Mechanism.Data.csproj
```

`.NET Framework 4.8` 旧式csproj需显式Compile包含新文件；增改后同时检查ProjectReference方向和Release/x64构建，不能因为目录创建成功就声明项目成功。

### 11.4 暂缓/必须在实现前作出的裁决

| 议题 | 建议决策 | 为什么 |
|---|---|---|
| DataContext一个入口 | **同意**；日常代码只Get/Set/Put/Ref/Return | 易用性有唯一门面 |
| Return提交时机 | **Runtime驱动提交**；Return只暂存 | 避免Dispose自动Commit或半写 |
| 输入的父子继承 | **仅显式Input映射** | 防并发跨Scope串数据 |
| 并行分支共享模式 | **固定版本只读；写用COW或独占** | 默认无竞态 |
| 子流程输出冲突 | **默认失败，显式Join/Reduce** | 不随机last-writer-wins |
| 大数据默认拷贝 | **默认遵守所有权，不能承诺总零拷贝** | 第三方SDK、跨进程生命周期各异 |
| 同步与异步载入 | **小值同步、大数据建议AcquireReadAsync** | 避免UI/Worker阻塞 |
| 跨进程优先级 | **先Memory+MappedFile验证，再做SHM；GPU由Adapter** | 减小物理ABI验证风险 |
| Inspector默认行为 | **只读轻量摘要，明确点选ROI才读样本** | 不因调试改变生产性能 |
| 唯一真源 | **MRK状态、MHS配置、M09变量、M10数据各自单一权威** | 降低双写与难恢复风险 |

## 12. 实施阶段、验收证据与给Codex的任务入口

| 阶段 | 任务边界（尽量一个任务一个闭环） | 真实可操作结果 | 必须先通过的测试 |
|---|---|---|---|
| M1：最小数据运行 | FDT基础+DataContext+Scope/Frame+原子OutputCommit | D01、D03、V02可通过真实代码运行 | T01-06、T08、U01-06 |
| M2：Heap共享与并行 | Ref/Entry/MemoryCarrier/Lease/Quota，接MRK Join | D02、D07、V01可运行，输出无早释放 | T07、T10-12、T23、T28 |
| M3：大数据与观察 | ROI/Range/Mapped、Quota、Snapshot、Inspector | D04、V03/V07可运行 | T09、T15-18、U09/U11 |
| M4：变量/历史/跨进程 | M09、M35/M36、SHM或AI Bridge、External Operation | D05/D08、V04/V06 | T13-14、T20-21、T27 |
| M5：完整工业验证 | Config V1.4/正式MRK/专业Provider对接、异常恢复 | S01-S12关键主链、五系统/十大案例映射 | T19、T22、T24-26、T29-30 |

**每个Codex任务必须交付**：实际源码差异清单、更新的项目引用、最小使用示例、实际编译和测试命令/输出、失败反例、内存/Lease指标、Commit数量与SHA，以及未覆盖的设备或进程边界。未经明确授权不Commit/Push/Merge；若授权，遵守B+C：**一个完整任务默认一个正式Commit，复杂任务才2—3个独立意义提交**。

### 12.1 需求—类型—测试追踪示例（首批必须做到）

| 需求 | 核心类型/项目 | 场景 | 测试 |
|---|---|---|---|
| 一行返回算法结果 | IMDSDataContext/MDSDataScope/MDSDataFrame | D01 | T02、U01 |
| 子流程Output原子性 | Scope/Frame/SlotBinding/Heap Reserve | D03/V02 | T05、T06、U06 |
| 图像AI/CV并行共享 | DataRef/Lease/Heap/MemoryCarrier | D02/V01 | T07、T08、T28 |
| CT ROI占用受控 | Shape/Range/Carrier/Quota | D04/V03 | T09、T10 |
| 配置任务固定 | 现有MHSConfigSnapshot + DataContext | D06 | T19 |
| IoT质量与历史 | MVB Variable/Store/Snapshot、MHSJournal | D05/V04 | T13、T14 |
| 异步迟到隔离 | DataLease、MRKExternalOperation/Correlation | D08/V06 | T20、T21 |
| 调试可见而不侵入 | DataSnapshot/Lifecycle/Lineage | V07 | T15、T16、T17 |

## 13. 已有实现/候选/验证状态必须区分

基于最近一次x3仓库静态检查：`Foundation/Data/Configuration`已有FCM V1.4文件；`Mechanism/Data/Configuration`已有MHS配置服务；`Mechanism/Runtime/Execution`已有MRK ExecutionPlan/Engine/Context；`Foundation/Infrastructure/Concurrency`已有FCR Worker/Lease基础。**这些已存在的代码不证明本版MDS DataContext、DataHeap和DataStack已落地。**

| 来源 | 可作为依据的内容 | 不能凭它宣称 |
|---|---|---|
| `DOCS/Implementation/Configuration_V14_Replacement_20261015.md` | 已重构Config层和迁移限制 | 数据堆栈已经实现 |
| `DOCS/Architecture/CxVisual_统一运行体系_六角色期望与并发异步编排执行_综合设计讨论稿_V0.1.md` | 统一执行语义、R1-R6角色需求、E01-E18 | 数据Scope原子提交已通过测试 |
| `DOCS/ProjectCatalog.csv` | Foundation.Data/Mechanism.Data既有Owner与子方面ID | 目录存在就代表功能完整 |
| `DOCS/Codebases/02_device_communicator.md`、`03_gcl3.md` | IoT设备变量/点表、SCADA实时值、SHM历史技术资产 | 旧实现已经在.NET48新平台迁移成功 |
| V1.0/V1.1/V1.2文档 | 概念、案例、44→48候选清单、验收建议 | 正式API已经冻结 |
| 规格《CxVisual Foundation与Mechanism ... V6.4》 | 最终需要核对的Owner/前缀及对象去重依据 | 本稿已逐条审完V6.4所有单元格 |

## 14. 仍需与用户讨论的6个必要问题（V1.3之后）

1. **子流程成功返回的时间**：同意 `Return`先暂存、整个子流程成功时统一发布吗？建议同意；流式产出另走Stream/Event语义。
2. **Output冲突**：同一父Slot多个并行子流程同时写，默认拒绝，再由Join/Reduce显式合并吗？建议同意。
3. **普通小值的“父可见”时机**：父级必须等子流程Commit后才Get到值，还是可以显式监听进度事件？建议前者，进度另走通知/流式端口。
4. **图像默认所有权**：`Put(image)`对常见受管类型采用受控默认；相机/SDK裸Buffer必须显式Borrow/Transfer，并严格限制寿命，是否接受？建议接受。
5. **CT Carrier扩展优先级**：Memory＋MappedFile先做到ROI验收，SharedMemory按已有x2/gcl3物理ABI复用，GPU留适配接口，是否接受？建议接受。
6. **正式类型冻结**：先拿V6.4逐项对48+15做 `Existing/Extend/New/Merge/Defer/Drop` 审核，再决定代码类名数量，是否接受？建议必须执行。

**阶段性结论**：V1.3已将“使用简单”与“执行中数据严格正确”连接：开发者只面对少量方法，编排用户只操作专业端口，内部通过Scope/Frame、原子OutputCommit、固定DataRef版本、Lease/Owner和Carrier完成安全交接。下一版不应只是加更多类型，而应以D01/D02/D03、V01/V02、T01—T08先实现真实闭环，并对V6.4逐项去重。

---

**参考文件与版本**：
- 《CxVisual_统一数据管理体系_六角色需求与双Data架构设计_V1.0.docx》
- 《CxVisual_统一数据管理体系_六角色易用性与双Data架构设计_V1.1.docx》
- 《CxVisual_统一数据管理体系_双角色易用性与完整类型清单_V1.2_讨论稿.md》
- 仓库 `https://github.com/oudream/cx-visual-x3`，既有F/M Data、MRK Runtime、配置V1.4与运行体系V0.1文档
- 其它规格和历史资产：统一规格V4.9、三方面逐阶段V2.01、F/M API V6.4、五大代码技术档案

