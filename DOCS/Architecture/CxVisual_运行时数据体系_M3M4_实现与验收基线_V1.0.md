# CxVisual运行时数据体系：M3/M4实现与验收基线 V1.0

> 日期：2026-10-17。工作仓库：`oudream/cx-visual-x3` / `master`。
>
> **真实状态说明**：本版描述已提交的M3/M4软件能力和测试入口，不把“代码提交”等同于“CI完成”。每次发版/验收必须以最新HEAD的Windows完整解决方案、ArchitectureSmoke、TestCenter及Runtime TDD实际结果为准。对应源码和测试路径列在本文中。

## 1. 架构边界保持不变

运行时数据体系本身只由两项已有工程拥有：

- `CxVisual.Foundation.Data`：通用DataKey、类型描述器、类型安全捕获、不可变Buffer和自定义数据适配器。
- `CxVisual.Mechanism.Data`：DataHeap、DataStack、DataFrame、DataContext、磁盘定位/加载、元数据搜索、数据绑定解析与开发者算法适配。

`Mechanism.Runtime`只在**既有MRK执行路径**上引用已有DataStack、为真实Flow填写RunId，并继续调用原有端口绑定和输出提交机制。不增加Data管理器，不在Runtime另建对象仓库或编排执行器。旧式csproj的新增Compile已登记；原来的28项目组织不改变。所有新增类型遵循`FDT*`、`MDS*`前缀。

## 2. M3 数据存储和类型：实现契约

| 能力 | 实际行为 / 安全边界 | 代码归属 |
|---|---|---|
| 基础CLR类型 | 原有标量、日期、枚举、字符串、decimal安全值读写 | `FDTDataValueSafety` |
| 可写Byte[] | 入库、出库、复制都生成防御性副本；不把可写数组谎称并行只读 | `FDTDataValueSafety` |
| 不可变大Buffer | `FDTReadOnlyBuffer`一次性从可写数组捕获；并行共享同一只读实例；可通过`ToArray`生成独立可写副本 | `FDTReadOnlyBuffer` |
| 自定义DTO / 专业数据 | 每个Flow/Heap使用独立`FDTDataAdapterRegistry`，`Register<T>`注册复制器、不可变承诺和Kind；无适配器时拒绝未知可变对象；冻结后不可修改能力表 | `FDTDataAdapters.cs` |
| 专业类型形态 | 自定义类型可标记Image、Volume、Tensor等数据Kind，`FDTDataTypeDescriptor.Describe(type, registry)`识别专业能力；并不在机制层创建专业Image/CT/Tensor算法类 | Foundation.Data |
| 磁盘文件定位 | `SetFile`只保存绝对路径、大小、最后修改UTC、类型和Loader；不读取实体、不接管源文件删除权 | `MDSDataFiles.cs` |
| 按需加载 | `LoadAsync<T>`有Unloaded→Loading→Loaded/Failed状态；严格类型，取消与源文件前后版本检查，加载失败后允许重试 | `MDSDataHeap` |
| 普通二进制文件 | `MDSLimitedBinaryFileLoader`默认限制64MiB的整文件加载，避免8GB CT文件被无意全量装入内存 | `MDSDataFiles.cs` |
| 专业CT/图像格式 | 专业通过`MDSDataFileLoader<T>`提供被验证的局部/ROI加载；Data机制只接收已经捕获的安全结果；不能宣称内置DICOM/RAW/多格式解码器 | `MDSDataFiles.cs` |
| Unload/Remove | Unload只释放已加载缓存并保留定位记录；Remove只删Heap记录，不删除原磁盘文件；加载中拒绝Unload/Remove/Replace | `MDSDataHeap` |
| Move/Copy磁盘条目 | Move改变Locator条目Owner，并可转交已加载缓存；Copy复制定位记录但默认不复制大文件内容或已加载缓存；同名默认拒绝，显式Replace | `MDSDataHeap` |
| 深复制并发安全 | 自定义Copy在锁外准备独立副本，再在锁内确认源记录未改变；否则Busy且目标不创建半成品 | `MDSDataHeap` |

**重要保留边界**：

- 默认Loader的64MiB阈值不是全Flow的总内存预算；硬性多条目配额、GPU显存配额、持久数据回收和多级缓存策略仍需额外机制，不可宣称已完成。
- FileInfo大小+修改时间是轻量来源检测，**不是具备强抗碰撞性的文件内容哈希或事务式持久化快照**。用户或其他进程可在极端情况下保持大小及修改时间不变地替换内容；如业务要求强一致，应由专业Loader校验哈希/版本。
- 专业`Register<T>`复制器必须由专业自己证明深复制不会共享嵌套可写内存；平台能拒绝同一引用返回，但不能靠一个通用反射器证明未知对象图完整隔离。
- 对专业Loader真实底层GPU/SDK/非托管句柄的释放确认和部分加载生命周期，需要后续受控资源机制，不能简单套托管Byte[]承诺。

## 3. M4 搜索、检查和简单使用：实现契约

| 能力 | 实际行为 | 代码归属 |
|---|---|---|
| 统一元数据模型 | `MDSDataSnapshot`记录名称、CLR类型、来源位置、加载状态、文件定位、大小、版本、RunId与ScopeId；**不持有大数据主体** | `MDSDataQuery.cs` |
| DataHeap查询 | `Heap.Query`和`Heap.Inspect`直接从权威内存/文件条目投影，名称、类型、状态过滤和稳定名称排序 | `MDSDataHeap.Query.cs` |
| 当前节点搜索 | `data.Search`聚合**本调用**Input/Local/Pending与当前FlowHeap；不会自动搜索别的节点未映射输出，也不会意外Load磁盘文件 | `MDSDataContext` |
| 精确Inspect | `data.Inspect(name, location)`返回元信息，同名跨位置需要指定Location | `MDSDataContext` |
| 基本分页 | `MDSDataQuery`具有Skip/Take，默认100、单页最大500；支持NamePrefix、DataType、Location、LoadState、RunId、ScopeId | `MDSDataQueryLogic` |
| 强类型键 | `FDTDataKey<T>`，开发者一行Get/Set/Return，不必先搜索或构造DataRef | Foundation.Data + DataContext |
| 强类型函数入口 | `MDSDataFunctions.Invoke/InvokeAsync`从已映射Input自动读取，执行业务函数，再Return暂存Output；运行仍归原MRK | `MDSDataFunctions.cs` |
| 统一绑定描述 | `MDSDataBinding`描述常量、当前槽、已端口映射的节点输出、Heap、变量、工程资源与表达式，不建第二套全局Port Schema | `MDSDataBindings.cs` |
| 外部Owner接口 | `IMDSExternalBindingResolver`只适配现有变量/工程资源/表达式Owner；无授权解析器则拒绝访问 | `MDSDataBindings.cs` |
| 类型兼容候选 | `CompatibleCandidates`返回当前可见范围内与目标端口兼容且状态可用的元数据，过滤PendingOutput和默认未加载项，不触发IO | `MDSDataBindings.cs` |
| MRK自动输入注入 | 继续使用现有`MRKExecutionInput/StepOutput`端口校验与依赖，进入节点DataContext后由`data.Get<T>`直接获取 | `Mechanism.Runtime`**原有执行路径适配** |

**不重复建设**：本版没有创建第二个变量库、资源库、历史数据库、DataManager、端口表或运行执行器。工程配置端和WPF参数选择器可直接使用`Search/Inspect/CompatibleCandidates`的契约；**实际WPF搜索面板/下拉控件及完整交互联动未在本次两Data项目代码内改造**，不能声称界面已完成。

默认搜索只检查当前Flow可见域。跨Run持久历史检索与工程资源目录由其现有Owner负责，与实时Heap不是同一份真源。Skip/Take提供确定的单次快照排序；**并发修改期间跨多页检索不承诺事务一致性**，后续若需要稳定游标/快照版本再增加。

## 4. 开发者实例（真实候选API，具体CLR类型按自身专业提供）

```csharp
// 1. 同Flow的局部与输出：开发者不需自己管理Heap或Stack
var input = new FDTDataKey<int>("Threshold");
var output = new FDTDataKey<int>("Area");
data.Set(input, 120);
int value = data.Get(input);
data.Return(output, value * 2); // 只是暂存，MRK成功后才提交

// 2. 文件数据：只登记、不加载
data.SetFile("CT.Raw", path);
var summary = data.Inspect("CT.Raw", MDSDataLocation.FlowHeap);
byte[] raw = await data.LoadAsync<byte[]>("CT.Raw", cancellationToken);
data.Unload("CT.Raw");  // 不删除path指向的文件

// 3. 自定义CT体数据：为当前Flow注册专业Loader与安全复制器
data.Adapters.Register<MyVolume>(
    v => DeepCopyVolume(v), kind: FDTDataKind.Volume);
data.SetFile<MyVolume>("CT.VolumeROI", path,
    new MDSDataFileLoader<MyVolume>((file, ct) => ReadVolumeRoiAsync(file, ct)));

// 4. 常规纯计算能力：MRK已将Port映射为Input别名
int result = MDSDataFunctions.Invoke(data,
    new FDTDataKey<int>("ScoreInput"),
    new FDTDataKey<int>("ResultOutput"),
    score => score * 2);

// 5. 编排参数可选项（不加载文件实体）
var candidates = MDSDataBindingResolver.CompatibleCandidates(
    data, typeof(FDTReadOnlyBuffer));
```

以上`MyVolume`、`DeepCopyVolume`、`ReadVolumeRoiAsync`仅为**专业自己提供的示例占位**，不是仓库已实现的专业类或完整CT重建算法。自动绑定仍需要MRK节点的Input/Output声明，不能凭一个普通Func绕开端口契约。

## 5. 已提交测试与真实验收入口

### 5.1 TestCenter源代码测试（既有项目，自动发现）

| 测试ID | 验证场景 |
|---|---|
| TCT.MDS.009 | 专业DTO深复制、未知可变类型拒绝、能力冻结 |
| TCT.MDS.010 | 文件仅登记、查询不加载、Load/Unload、外部文件不删除 |
| TCT.MDS.011 | 文件大小/时间变化导致Load拒绝 |
| TCT.MDS.012 | 跨Heap复制Locator不复制磁盘文件主体 |
| TCT.MDS.013 | 类型、位置、加载状态、稳定排序与分页 |
| TCT.MDS.014 | 异步加载与取消、加载中禁止Unload/Remove、失败可重试 |
| TCT.MDS.015 | 超出64MiB默认整文件加载上限时拒绝 |
| TCT.MDS.016 | 当前Scope搜索与Inspect，其他节点Output不泄漏，Run/Scope过滤 |
| TCT.MDS.017 | 强类型DataKey与现有MRK真实节点端口+函数自动绑定 |
| TCT.MDS.018 | 异步函数已取消不得Return半成品 |
| TCT.MDS.019 | 专业DTO经过MRK多节点、DataOutputs与Flow最终提交 |
| TCT.MDS.020 | 大CT文件只使用专业ROI Loader读取小块 |
| TCT.MDS.021 | 常量、当前Slot、Heap、节点Output、外部变量Owner绑定 |
| TCT.MDS.022 | 专业Copy时并发Replace来源，拒绝半成品并保护目标 |

既有TCT.MDS.001–008及M1/M2 ArchitectureSmoke继续保留，修改后仍需全量回归，不以删除旧测试换通过。

### 5.2 ArchitectureSmoke真实测试

`Tests/ArchitectureSmoke/RuntimeDataStorageTests.cs`由原有`Program.cs`实际调用，覆盖专业类型Copy、磁盘定位、文件版本变化、异步取消、强类型函数贯穿MRK、外部绑定显式Owner；原有`RuntimeDataLifecycleTests.cs`继续覆盖M1/M2。测试不新增csproj。

### 5.3 阶段验收门槛

1. 最新master commit上的`CxVisual Config L1 Windows Build and Smoke`必须`success`，其中包含完整NET48解决方案构建与ArchitectureSmoke。
2. 最新master commit上的`TestCenter Windows .NET48`必须`success`，其中MDS.009–022无一失败/跳过。
3. 涉及MRK运行代码改动的最新master commit上的`CxVisual Unified Runtime Windows TDD`必须`success`。
4. 原有M1/M2的异常、取消、并行与嵌套回归必须继续通过，最新运行SHA和URL要留存。
5. 内存配额、持久文件哈希/事务恢复、专业真实GPU/非托管、工程WPF实际UI交互和工业设备实测**不得从这些模拟/托管测试推断已通过**；这些属于M5专业集成或单独高级特性验收。

## 6. 下一步M5专注验收与界面接入，而不是重新设计数据层

M5应使用真实Vision/CT/Automation场景接入**本版已存在的Data接口**，同时在现有Workbench/WPF参数选择器中接入`MDSDataQuery`、`MDSDataBindingResolver`和MRK端口元数据。如果发现真实专业数据类型的Buffer所有权或性能不够，扩展专业Loader和Adapter而不是新建第三个Data项目。

**本轮所有新增Data能力只归属原有Foundation.Data、Mechanism.Data；没有用“类名存在”替代真实的测试证据。**
