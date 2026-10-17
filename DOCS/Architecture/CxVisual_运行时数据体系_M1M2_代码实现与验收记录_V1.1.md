# CxVisual 运行时数据体系：M1/M2代码实现与验收记录 V1.1

> 更新：2026-10-17（V1.1）；原始实施：2026-10-16。工程：`oudream/cx-visual-x3`，直接在 `master` 开发，所有Git Commit中文。
>
> 设计依据：`CxVisual_运行时数据体系_完整设计收敛与实施基线_V1.8.md`及仓库`DOCS/Architecture/CxVisual_统一数据管理体系 V1.3.md`。
>
> **证据原则**：本记录说明实际编码范围和自动测试覆盖。CI须以最终代码提交SHA上的Windows工作流结论为准；不能用中间提交或仅有类名证明全部规格已完成。

## 1. 约束与归属

- 数据体系仅在**原有** `CxVisual.Foundation.Data`、`CxVisual.Mechanism.Data`两个项目建设，没有新增csproj、没有建立第二个数据管理器。
- 原有`Mechanism.Runtime`只增加必要引用、现有MRK执行计划及端口/引擎**适配**；所有数据真实Owner仍在Mechanism.Data。
- Foundation基础类型为`FDT*`，Mechanism运行数据为`MDS*`，继续复用已有`MDEExecutionDataScope`和`IFDEExecutionDataScope`。
- .NET Framework 4.8 / WPF架构保持，所有新增源码已加入对应旧式csproj显式Compile清单，原有测试工程不新增项目。

## 2. M1基础核心：已编码的最小可靠闭环

| 代码位置 | 内容与边界 |
|---|---|
| `Foundation/Data/Types/FDTDataTypeDescriptor.cs` | `FDTDataKind`、`FDTDataTypeDescriptor`、`FDTDataValueSafety`；标量/byte[]的明确安全存取与复制，未知可变对象拒绝 |
| `Mechanism/Data/RuntimeData/MDSDataHeap.cs` | 容器具名Set/Get、Contains、Search前缀、Remove、显式Replace、Heap之间Move/Copy、Dispose；拒绝同名冲突 |
| `Mechanism/Data/RuntimeData/MDSDataStack.cs` | 一个运行Flow一个`MDSDataStack`，每次节点一个`MDSDataFrame`，FlowHeap为Stack拥有；具名输出整体发布/快照读/活跃帧防早关闭 |
| `Mechanism/Data/RuntimeData/MDSDataContext.cs` | `IMDSDataContext`统一入口，Get/TryGet/Set/Return、Pending/Commit/Abort，明确输入映射，只读边界 |
| `Mechanism/Data/RuntimeData/MDSDataException.cs` | 前缀化结构错误码/异常，保留兼容既有InvalidOperationException捕获 |
| `Mechanism/Data/Execution/MDEExecutionDataScope.cs` | 原Scope增构：移动、复制、冲突和重试所需的受控本地操作，无第二套Scope |
| `Mechanism/Data/CxVisual.Mechanism.Data.csproj` | 已加入相关Compile；其对Foundation.Data的原有单向项目引用保持 |

M1数据类型当前安全支持：标量、枚举、字符串、日期时间/Guid/decimal、`byte[]`防御复制、`FDTReadOnlyBuffer`真正不可变共享；`IntPtr/UIntPtr`不接受为安全标量。**复杂DTO、非托管SDK Buffer、Image/Volume/Tensor专业承载未被伪装为已支持**，留给M3适配。

## 3. M2运行交接与生命周期：已编码的最小可靠闭环

| 能力 | 行为 |
|---|---|
| 节点直连 | 原有`MRKExecutionInput`增加`StepOutput`来源（`节点ID.输出名`），不强制通过Heap中转 |
| 声明与验证 | `MRKExecutionStep.DataOutputs`支持一个节点多个具名输出，编译期类型/依赖校验 |
| 严格Data入口 | 节点明确启用`EnableDataContext`后，Get只可见已映射Input与自身Local；原DataScope父继承路径暂保留旧兼容 |
| 输出提交 | Return暂存，节点实际成功+输出齐全+校验通过才向Flow级DataStack一次性发布全部Output |
| 扇出只读 | 对`byte[]`采用独立安全快照，AI/CV两分支不能通过可写数组互相篡改；**不承诺零拷贝** |
| 生命周期 | 节点本地Scope安全释放；已交付Output由Flow DataStack保活至整个Flow结算；活跃Frame阻止Stack Dispose |
| Move/Copy | Heap↔Heap、Stack↔FlowHeap双向交接，固定锁顺序；Move成功撤源，Copy独立；冲突默认拒绝、显式Replace |
| 占用保护 | 子Scope仍活动的Local、由PutOwned带释放回调的Local不能直接Move |
| 幂等重试 | 重试清理上一尝试Local/Pending；已发生显式Heap写入时拒绝隐式重复执行 |
| 多节点子流程 | `InlineSubflow`在有显式`DataExports`时插入`__commit`屏障；全部内部步骤成功后才公布导出 |
| 三层嵌套 | 支持内层屏障交给中层屏障，中层再交给父层；内部输出禁止跨屏障被父流程直接绑定 |
| 完整Flow结果 | `MRKFlowResult.CommittedOutputs/GetCommittedOutput<T>`仅在成功时提供导出快照，失败取消时不暴露 |
| 取消与超时 | 节点完成事实由原MRK Runtime和Policy保证；取消后不可发布PendingOutput，超时策略等待原任务实际结算 |

特别说明：`MoveToHeap/CopyToHeap`是**显式即时Heap副作用**，不冒充与`Return`一并原子回滚的业务事务；上层业务应在确认可公开数据时调用。未知可变类和外部GPU/SHM等在M3/P2验证前拒绝或不提供。

## 4. 验收入口、真实失败场景和回归

所有M1/M2新增测试集中在**既有**`Tests/ArchitectureSmoke/RuntimeDataLifecycleTests.cs`，并由`Program.cs`实际调用，不新增测试项目。

| 测试方法 | 实际检查 |
|---|---|
| `FoundationTypeSafetyContract` | 类型安全标志和byte[]复制反例 |
| `StackFrameCommitAndAbort` | Output缺项失败、原子可见、Abort、关闭前活跃帧 |
| `HeapStackMoveCopyConflictAndOwnedRejection` | 堆栈双向Move/Copy、默认冲突、显式Replace、Owned拒绝、Heap关闭后不可读 |
| `MultiOutputFanoutAndTypeIsolation` | AI/CV并行扇出、可变数组隔离、多输出绑定 |
| `ConcurrentFlowsNeverShareDataStack` | 两个Run同时复用一个Plan，Result与FlowId不串 |
| `MissingOutputMustAbortEverything` | 子节点缺Output时依赖者不能执行 |
| `CancelledStepMustNotPublish` | 请求取消、迟到完成后仍不可将暂存结果公布 |
| `RetryMustDiscardFailedAttemptData` | 上一次失败的Local/Pending不得污染下一次成功尝试 |
| `InlineSubflowOutputIsMapped` | 旧兼容内联子流程正确绑定 |
| `MultiStepSubflowCommitBarrier` | 多节点子流程有失败时不导出、父级不能绕过屏障 |
| `ThreeLevelSubflowCommit` | 三级嵌套逐层整体成功后导出 |
| `FlowResultExposesOnlyCommittedExports` | 成功Flow导出和失败Flow不泄漏、快照防御复制 |
| `ExplicitInputCannotInheritParent` | 新Data入口父Local隔离，旧DataScope功能兼容 |

原有`ExecutionDataScopeTests`、`ExecutionPortTests`、`ExecutionPolicyTests`及其他ArchitectureSmoke用例继续运行，覆盖资源逆序释放、并行分支、端口类型、超时等旧行为。

验证命令为Windows MSBuild构建`CxVisual.Unified.sln`（.NET Framework 4.8，x64，Release/Debug），再运行`Output/Release/x64/CxVisual.Tests.ArchitectureSmoke.exe`；另有GitHub Actions `CxVisual Config L1 Windows Build and Smoke`、`TestCenter Windows .NET48`和`配置公共底座独立编译与验收`。**每次验收记录应绑定最新Commit SHA、运行ID、状态、失败日志，不能引用旧成功当作新成功。**

## 5. 明确不属于M1/M2且不应谎称已完成

- M3：磁盘`SetFile/LoadAsync/Unload`、大文件版本校验、存储容量预算、复杂Image/Volume/Tensor/DTO适配、深复制策略扩展、ROI分块读取。
- M4：完整DataQuery/Search索引、权限过滤分页、Inspector UI、五类Binding选择器、强类型DataKey和自动能力函数绑定。
- M5：产线实机/跨专业五大历史系统、性能容量基准、外部GPU/SHM、可靠持久恢复和硬件安全验收。
- **M2已支持托管不可变Buffer只读共享**，以及安全托管条目Move的所有权转移；对未知可变对象、非托管/SHM/GPU及大型外部Buffer，仍未承诺零拷贝、精确最后消费者回收或全部类型共享。当前Flow内已提交数据保守保留至Flow结束。
- 旧MRK`StepResult`支持的任意可变CLR对象不是自动获得MDS只读保护；专业新能力应使用已声明的`DataOutputs`和安全数据适配。
- 使用`MDSDataContext(IFDEExecutionDataScope)`兼容构造器时，旧Scope的父级继承仍然存在；**严格输入隔离属于经MRK创建的托管Frame模式**。

## 6. 阶段判断方法

**M1/M2是否达标必须以最后一个代码提交上的完整Windows ArchitectureSmoke成功、结构校验成功、回归不退化为依据**。若最终SHA上的工作流仍处于Queued/InProgress或失败，只能记录为`CODED/BUILT/PENDING`，不得标为`AUTOMATED_PASS`。上述P0能力范围之外的M3–M5验收没有被提前宣称通过。

**后续开发顺序：M3存储/类型 → M4搜索与工程编辑易用性 → M5真实跨业务/设备验收。**

## 7. V1.1 深度对抗审核和修复（2026-10-17）

### 7.1 确认修复的问题

| 风险 | 修复方式 | 验收入口 |
|---|---|---|
| `CommittedOutputs`是只读字典但byte[]可改 | 每次读取返回经过防御性复制的快照；`GetCommittedOutput`读取内部权威快照 | `FlowOutputsAndInputsHaveNoMutableAlias` |
| 外部调用者修改StartFlow输入数组 | 在Flow启动时捕获byte[]副本，读取与绑定时做必要保护 | `FlowOutputsAndInputsHaveNoMutableAlias` |
| 顶层`DataExports`可直接导出嵌套子流程内部节点 | `MRKExecutionPlan`检查来源`SubflowAccessGroup`，只允许经子流程导出屏障 | `SubflowExportsCannotBypassCommitBarrier` |
| 旧式子Scope存活时DataStack先释放FlowHeap | 数据栈关闭前预检原`MDEExecutionDataScope`是否仍有子Scope | `InactiveScopeMustNotReleaseDataStack` |
| `MDSDataFrame.Dispose`被活动子Scope拒绝却清除ActiveFrame | 只有Scope真正关闭才清理帧占用；拒绝时可以待子级完结后重试 | `PendingFrameWithChildMustRemainTracked` |
| 存储null时丢失声明类型 | 数据Heap和托管Scope保留声明类型，错误`Get<T>`必须拒绝 | `HeapNullTypeCannotBeChanged`、`StrictLocalNullAndLegacyMoveOwnership` |
| CLR IntPtr/UIntPtr被误认安全immutable标量 | `FDTDataValueSafety`禁止原始指针进安全值白名单 | `HeapNullTypeCannotBeChanged` |
| 受控只读扇出被简化为多份可写数组 | 新增`FDTReadOnlyBuffer`，入库一次隔离，节点读者可共享同一个不可变实例；旧`byte[]`保留防御复制 | `ReadOnlyBufferCanShareWithoutCopying`、TestCenter `TCT.MDS.005` |
| Move对已由MDS接管的数组额外Clone | Scope条目标记`ManagedOwned`，在Scope/Heap同一受控临界区转移真实Owner，Copy独立复制；旧Scope外部裸数组先Clone | `StrictLocalNullAndLegacyMoveOwnership`、TestCenter `TCT.MDS.006` |
| 双向并发Move有循环锁风险 | Heap间按稳定容器顺序加锁；Heap/Stack遵循Scope→Heap顺序 | `ParallelHeapMovesDoNotDeadlockOrLoseData`、TestCenter `TCT.MDS.008` |

### 7.2 与真实已有测试中心整合

既有`Tests/TestCenter/Cases/TCTRuntimeDataCases.cs`此前已有四项实际Owner测试：

- `TCT.MDS.001` Heap Move/Copy及别名
- `TCT.MDS.002` Frame原子提交
- `TCT.MDS.003` 缺失输出与Abort
- `TCT.MDS.004` Heap外部副作用阻止不安全重试

V1.1新增`TCT.MDS.005`只读Buffer真共享、`TCT.MDS.006`类型与旧Scope安全转移、`TCT.MDS.007`遗留子Scope保活、`TCT.MDS.008`双向并行Move。全部使用已有`Tests/TestCenter`项目及自动发现机制，**没有新增测试项目**。

### 7.3 封闭范围与正确表述

- **M1/M2范围内**：普通标量、托管byte[]及不可变Buffer；Heap、Stack、Context、Frame/Scope基础；具名端口、Return/原子发布、单/并行消费、嵌套导出、可取消/重试清理、Flow输出、Move/Copy/冲突、进程内生命周期保护、Windows自动测试。
- **M3及之后**：丰富DTO/Image/Volume/Tensor类型适配、磁盘未加载/Load/Unload、ROI、GPU/SHM、外部Operation占用保活、资源配额和更复杂的Carrier；不以M1/M2结论代替其验收。
- 生产级实机运行、恢复、跨进程的证据必须分别验收；Windows模拟测试不会自动证明上述能力。
- M1/M2状态必须跟随**最后有效master Commit SHA**上的**CxVisual Config L1 Windows Build and Smoke、TestCenter Windows .NET48及结构/运行相关工作流**。本记录不写入未经实测的PASS结论。

## 8. 实施后的关键入口

```csharp
// M1基础：数据归属于当前Frame和FlowHeap
data.Set("Threshold", 120);
int threshold = data.Get<int>("Threshold");
data.Return("Area", 32); // 暂存，等MRK成功和端口校验后才原子提交

// M2安全扇出：数据进入不可变视图，只读共享，无每消费者重复复制
var pixels = new FDTReadOnlyBuffer(rawImageBytes);
data.Return("Image", pixels);

// M2显式跨容器移动与复制，默认同名冲突拒绝
data.MoveToHeap("LocalImage", "SharedImage");
data.CopyFromHeap("SharedImage", "WorkingImage");
// 修改可变byte[]需要用户取得安全副本
```

子流程具名导出只通过`InlineSubflow`的`__commit`屏障；普通MRK旧节点`StepResult`仍保持既有兼容行为，但不能将旧任意可变CLR引用宣称为受控只读共享。

**验收约束：设计中的T01–T30和U01–U14属于跨阶段总体验收矩阵；M1/M2这里只声称上述实际测试入口覆盖的子集。**
