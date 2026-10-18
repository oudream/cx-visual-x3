# CxVisual 运行时数据体系——完整设计与实施总文档 V2.0

> **完整规格升级提示（2026-10-18）**：本V2.0原稿仅是M1～M5设计与实现的阶段汇总，**未完整继承早期V1.3的48项类型/15项枚举与R1/R3全案例**。现已补成[《运行时数据体系——完整对象规格与双角色使用指南 V3.0》](CxVisual_运行时数据体系_完整对象规格与双角色使用指南_V3.0.md)，包含当前源码全对象规格、早期48+15逐项去向、D01～D10开发者示例、V01～V08编排用例、12工业场景、T01～T30/U01～U14/EUX01～EUX12验收及未实现边界。**请以后以V3.0为完整规格入口**；V2.0保留作阶段历史记录，不继续误标为唯一完整权威源。


> 日期：2026-10-17。适用仓库：`oudream/cx-visual-x3` / `master`。  
> 读者：平台与专业软件开发者、工程编排用户、代码审查者、Codex执行者。  
> **定位**：现行决策、已实现接口、实现边界、验收路径的统一入口。实际正确性以最新源码和对应SHA的真实Windows CI为准。  
> **复核快照**：截至本次复核，`master`为`2bd1e87d38c4b19ea31fd1dea02ce470933e1897`，该SHA的完整构建/ArchitectureSmoke、独立编译与TestCenter工作流均为成功。仓库变更后需重新检查。  

**重要历史说明**：仓库内《统一数据管理体系 V1.3》是早期设计讨论稿，其中列出了48个类/接口/值类型和15个枚举候选，包括以`DataRef/Lease/Carrier`为中心的方案。此后架构已收敛为：**数据由堆、栈自身拥有；普通节点Output→Input直接传递；共享/长期保留/移动复制/磁盘未加载数据才显式进入Heap；不以DataRef为日常入口。** 本总文档以**后期确定规则与已存在代码**为准，绝不把旧稿中的候选类型当成已实现。早前实现记录提及V1.8，但本次在仓库当前DOCS目录未找到V1.8正文，故不能声称已逐字核对该原件。

---

## 0. 最重要的十条确定规则

1. **仅两个已有Data项目**：`CxVisual.Foundation.Data`和`CxVisual.Mechanism.Data`。不新增第三个数据项目，不建第二个数据管理器。
2. **角色容易用**：软件开发者主要面对`Get/Set/Return`；工程编排用户主要面对“拖专业能力→连数据端口→选参数→运行检查”，不必管理堆栈。
3. **节点直连为默认**：A的输出直接绑定到B的输入；不是每个输出都先进FlowHeap。
4. **所有权在容器**：`MDSDataStack`拥有一次Flow的已提交输出；`MDSDataHeap`拥有显式存入的内存条目、文件定位记录和已经加载的缓存。
5. **生命周期明确**：Local随节点Scope结算；Output先暂存，成功发布后由Flow保活；当前采用保守Flow级保活，不虚称已经实现“最后消费者精确回收”。
6. **多消费者默认受控只读**：真正不可变Buffer可共享同一实例；可变`byte[]`采取安全防御复制；有写入意图先明确复制。
7. **Move/Copy均支持双向容器交接**：Move移交所有权，Copy生成独立值；同名默认拒绝，显式`Replace/replace:true`才覆盖。
8. **文件可不加载**：`SetFile`只登记定位信息；`Search/Inspect`不引发加载；`LoadAsync`按需载入；`Unload`卸载缓存但绝不删除用户磁盘原文件。
9. **Return不是Commit**：缺少必需输出、取消、失败、超时后无法完整确认时不得交出部分Output；多节点子流程有导出屏障。
10. **查询不是权限绕道**：`Search`只看当前授权Input/Local/Pending/Heap元数据；已提交的其他节点输出必须经过既有MRK端口绑定，不能任意越权检索主体。

---

## 1. 用户价值与角色要求

| 角色 | 期望 | 最容易验证的结果 |
|---|---|---|
| 软件开发者 | 算法只写业务、代码轻便，复杂数据另做高级设置 | 标量一行`data.Get<int>(...)`；结果一行`data.Return(...)` |
| 平台机制开发者 | 生命周期、所有权、异常、并发安全不含糊 | 多Flow不串值；未提交Output不泄露；Move与Copy有确定结果 |
| 工程编排用户 | 不需要理解Heap、Stack、Lease就能编排 | 有类型的端口、合法候选过滤、节点数据检查 |
| Vision/CT/Automation专业开发者 | 常规图像、CT ROI、SDK数据有可扩展接口 | 可注册自定义安全复制器与文件Loader，未注册对象不冒险裸共享 |
| 运维/测试人员 | 知道是哪次Run、哪个Scope、哪个名称出问题 | 快速Inspect、错误明确、元数据查询不装载8GB内容 |
| 架构负责人 | 不出现第二套配置、变量、资源、执行管理器 | 符合现有两项目Owner与MRK/Config/工程资源边界 |

### 1.1 对开发者的最小API

~~~csharp
// data由当前MRK节点注入，普通算法不用new Heap或手动Push/Pop。
int threshold = data.Get<int>("Threshold");
data.Set("LocalScore", 0.98);
data.Return("Area", 120);
~~~

`Get`找不到数据时异常；`TryGet`仅不存在返回false。类型不兼容或作用域非法不能默默返回默认值。`Set`写当前Local，不隐式修改父Scope。`Return`写入Pending，必须由MRK确认节点成功后才发布。

### 1.2 对编排用户的默认流程

~~~text
创建工程/子流程
 → 从能力目录拖节点
 → 从已有能力与端口契约生成输入/输出端口
 → 连线并进行类型检查
 → 参数从【常量、映射端口、当前值、Heap、变量、工程资源】选择
 → 运行，由Runtime创建Frame并完成Return的成功提交
 → 用Inspect查看当前Run/Scope数据摘要
 → 如发现未加载文件，可显式执行Load或专业ROI预览
~~~

这里描述目标UI行为；**当前M4后端已存在，实际Workbench参数选择器和Inspector WPF交互并未全部完成接入**。

---

## 2. 两个Data项目的真实归属

| 项目/位置 | 主要已存在对象 | Owner职责 |
|---|---|---|
| `Foundation/Data/Types` | `FDTDataTypeDescriptor`、`FDTDataKind`、`FDTDataValueSafety`、`FDTReadOnlyBuffer`、`IFDTDataAdapter`、`FDTDataAdapter<T>`、`FDTDataAdapterRegistry`、`FDTDataKey<T>` | 通用类型描述、安全复制/冻结、不可变Buffer与强类型键；不管理运行状态 |
| `Mechanism/Data/RuntimeData` | `MDSDataHeap`、`MDSDataStack`、`MDSDataFrame`、`IMDSDataContext`、`MDSDataContext`、`MDSDataException` | Heap/Stack实际所有权、Frame、局部数据、Pending/Committed、受控转移 |
| 同上 | `MDSDataQuery`、`MDSDataSnapshot`、`MDSDataLocation`、`MDSDataHeap.Query` | 对权威运行容器投影元数据；不是第二份数据实体库 |
| 同上 | `IMDSDataFileLoader`、`MDSDataFileLoader<T>`、`MDSLimitedBinaryFileLoader`、`MDSDataLoadState` | 磁盘Locator、默认限制加载、专业按需Loader、Unload |
| 同上 | `MDSDataFunctions`、`MDSDataBinding`、`MDSDataBindingResolver`、`IMDSExternalBindingResolver` | 轻便C#函数入口、来源绑定及现有外部Owner适配 |
| `Mechanism/Data/Execution` | **复用**`MDEExecutionDataScope` | 已存在的Scope树、Local释放、已有旧行为兼容 |
| `Mechanism/Runtime/Execution`（**非Data Owner**） | 既有`MRKExecutionPlan/Engine/Ports`、`MRKFlowResult` | 运行事实、端口校验、调度、原子输出触发、子流程导出屏障，注入DataContext |

ConfigRevision归已有Config管理，PLC/IoT变量归既有变量Owner，工程资源归工程资源Owner。不能因M4有`Binding`就另建一套变量数据库或自动化执行器。基础Data类型用`FDT`前缀、机制Data类型用`MDS`前缀；已存在的`MDEExecutionDataScope`保持已有命名、以扩展复用为主。

### 2.1 结构示意

~~~text
专业能力 / Workbench流程图
    | 编排定义的 Input/Output 端口契约
    v
现有MRK ExecutionPlan/Engine（唯一执行事实）
    | 为每次节点调用注入 IMDSDataContext
    v
Mechanism.Data
    +-- Scope + Frame：Local、Input、Pending
    +-- DataStack：Flow已成功提交的Output
    +-- FlowHeap：显式共享值、文件Locator与缓存
    +-- DataQuery / DataBinding / FileLoader
    |
    v
Foundation.Data：类型、复制/冻结安全、Key、只读Buffer
~~~

---

## 3. 数据位置与生命周期

| 数据位置 | 内容 | 谁拥有 | 哪些主体可以观察 | 结束规则 |
|---|---|---|---|---|
| Input | MRK验证后映射的端口参数 | 当前节点安全快照；原始权威由上游持有 | 当前节点，只读 | 当前节点结算 |
| Local | 当前算法过程值 | 当前节点Scope | 当前节点 | 节点安全结束时清理 |
| PendingOutput | `Return`暂存数据 | 当前Frame/DataContext | 当前调用；不对父发布 | 成功整体Commit；失败Abort |
| CommittedOutput | 已成功提交的节点端口 | 当前Flow的DataStack | 正确声明依赖的下游和Flow导出 | Flow结束后安全释放 |
| FlowHeap | 需要显式共享/保留/转移的条目 | Flow的MDSDataHeap | 授权当前Flow调用；显式读写 | Flow结束、Remove、文件Unload仅卸缓存 |
| FileLocator | 外部文件位置、类型、加载器与状态 | Heap拥有定位记录，不拥有磁盘文件 | 元数据可查，读取要Load | Remove不删除磁盘源文件 |

### 3.1 节点直接交接，不强制Heap中转

~~~text
    采图A.Image ─────→ AI检测B.ImageInput
           |
           +─────────→ CV检测C.ImageInput

   B.Defects ──┐
               +─────→ 判定D
   C.Defects ──┘
~~~

正常路径是A成功`Return(Image)`→Flow DataStack整体提交→B/C的Input绑定。Heap不是每个节点结果的必经站；仅确有共享缓存、跨容器Move/Copy、长期保留或磁盘Locator时显式使用。

### 3.2 Scope不是CLR线程栈

一个Flow可以有多个并行节点，同名Local完全隔离。以RunId/InvocationId/Scope追踪实例，而不能因为“同一个Content定义/同一节点名/同一个线程”就共享数据。当前MDS模型保守把已提交Output持有到Flow结束，因此不假称已有每个Buffer最后消费者精确引用计数，也不因取消令牌触发就假称已终止外部GPU或硬件动作。

### 3.3 失败、取消、重试

- 节点或子流程`Return`只写Pending；完整成功才原子发布。
- 必需的第二输出缺失、类型错、运行失败：一个也不发布，不把半结果放入Flow导出。
- 多节点子流程经`__commit`屏障整体成功后对父导出；父不可越过屏障读取内部Output。
- 重试前清理上次Local/Pending；显式Heap操作是立即副作用，发生后不能假装能隐式幂等回滚。
- 已结束Scope不可由超时后的迟到响应再次写入；物理设备操作结果必须由其专业Owner确认。

---

## 4. 数据Heap、Stack的Move/Copy

**用户已确定的两条核心规则：默认受控只读共享；同名冲突默认拒绝，覆盖必须显式声明。**

| 操作 | 来源变化 | 目标变化 | 内容复制 | 同名策略 |
|---|---|---|---|---|
| Heap→Heap `MoveTo` | 成功后移除源 | 目标接管数据/Locator | 受控所有权转移，不默认复制主体 | 默认拒绝；`replace:true` |
| Heap→Heap `CopyTo` | 保留源 | 目标独立复制的数据/Locator | 普通可变值独立；文件仅复制定位记录 | 同上 |
| Local→Heap `MoveToHeap` | 移走当前Local | FlowHeap接管 | 对已由MDS安全持有者转移所有权；旧裸值先安全接管 | 同上 |
| Local→Heap `CopyToHeap` | 保留Local | FlowHeap生成副本 | 需要安全复制器 | 同上 |
| Heap→Local `MoveFromHeap` | 源Heap移除 | 目标Local接管 | 所有权移动 | 同上 |
| Heap→Local `CopyFromHeap` | 源Heap保留 | 目标Local副本 | 独立安全复制 | 同上 |

~~~csharp
var heap1 = new MDSDataHeap();
var heap2 = new MDSDataHeap();
heap1.Set("Result", 30);
heap1.CopyTo("Result", heap2, "ResultCopy"); // 原数据保留
heap1.MoveTo("Result", heap2, "Moved");       // 移交所有权
// 若目标名已存在，会明确拒绝；覆盖需显式replace:true。

// 在具有FlowHeap的当前节点Context内：
data.Set("Image", pixels);
data.MoveToHeap("Image", "SharedImage");
data.CopyFromHeap("SharedImage", "EditableImage");
~~~

**注意**：这些显式Heap写入不是`Return`业务事务的一部分。文件Locator`Copy`也**不**复制源磁盘文件或强制整文件装入内存。外部SDK/非托管/GPU句柄不能凭普通Move方法就得到正确生命周期。

---

## 5. M3 类型与安全复制

### 5.1 支持层次

| 类别 | 当前机制 |
|---|---|
| 标量、字符串、枚举、时间、Guid、decimal | 基础安全值类型处理 |
| `byte[]` | 入库/读取/Copy防御性复制，避免暴露可写内部别名 |
| `FDTReadOnlyBuffer` | 创建时接管安全副本，多消费者可以共享同一真正不可修改的实例；修改需`ToArray()` |
| 自定义DTO、专业Image/Volume/Tensor | 注册`FDTDataAdapterRegistry`适配器；未注册的未知可变类型默认拒绝 |
| 原生Camera指针/共享内存/GPU Tensor | 不因类型名存在就认为有零拷贝或安全资源释放；须专业Owner和进一步验收 |

~~~csharp
var registry = new FDTDataAdapterRegistry();
registry.Register<MyVolume>(
    x => DeepCopyVolume(x),
    kind: FDTDataKind.Volume);
registry.Freeze();

using (var heap = new MDSDataHeap(registry))
{
    heap.Set("CT.Volume", myVolume);
    MyVolume independent = heap.Get<MyVolume>("CT.Volume");
}
~~~

`MyVolume`和`DeepCopyVolume`是示例专业符号，并不是Data项目内建的工业CT格式算法。平台能拒绝复制器返回同一对象引用，但对嵌套可写资源是否真正深复制，专业适配器仍须有自己的测试证据。不同Heap互相转移专业类型时，目标也要具备安全类型适配能力。

---

## 6. M3 磁盘定位、延迟加载和卸载

默认数据堆可同时管理**内存值**和**未加载文件定位记录**：

~~~text
SetFile -> Unloaded -> LoadAsync -> Loading -> Loaded
                         |              |
                         | 失败/取消     +-- Unload -> Unloaded
                         v
                       Failed --(重试LoadAsync)--> Loading
~~~

`MDSDataLoadState`包括`NotFile / Unloaded / Loading / Loaded / Failed`。它是数据载体状态，不是MRK任务状态。

~~~csharp
data.SetFile("CT.Raw", filePath); // 只登记路径，完全不读取大文件

var info = data.Inspect("CT.Raw", MDSDataLocation.FlowHeap);
byte[] raw = await data.LoadAsync<byte[]>("CT.Raw", cancellationToken);

data.Unload("CT.Raw");  // 丢弃Heap缓存，保留FileLocator和用户磁盘原文件
~~~

- 内置`MDSLimitedBinaryFileLoader`默认限制**64MiB**整文件加载，防止误把数GB CT原始数据全读入内存。
- 专业CT可以注册`MDSDataFileLoader<T>`，由专业按需读取ROI、投影切片或局部重建块；平台接收经适配器管理的安全对象。
- 加载前后核对文件大小与最后修改UTC；取消时不能发布迟到的数据；加载中拒绝Unload、Remove、非法覆盖；异常留Failed便于诊断或重试。
- `Remove/Dispose`操作Heap记录和缓存，**不删除**文件路径指向的外部原文件。
- 现有大小/修改时间校验不是强Hash，不保证外部进程保持元信息不变地篡改文件时仍能发现；高可靠保存/恢复另需加强。
- 当前已有专业小ROI读取的可执行测试，但**并不等于通用DICOM或所有CT RAW格式解码器已内置**，也未证明生产环境总内存配额和多级缓存已全部完成。

---

## 7. M4 数据搜索和Inspect

M4搜索**只查元信息**而不读取大实体：

~~~csharp
var found = data.Search(new MDSDataQuery
{
    NamePrefix = "Camera",
    DataType = typeof(byte[]),
    Location = MDSDataLocation.FlowHeap,
    LoadState = MDSDataLoadState.Loaded,
    Take = 50, Skip = 0
});

var item = data.Inspect("Camera.Image", MDSDataLocation.FlowHeap);
~~~

- 条目含名称、CLR类型、位置、文件加载状态、来源、可能大小、版本、RunId、ScopeId。
- `DataContext.Search`只查当前Input、Local、Pending和当前FlowHeap；**不**暴露其他节点未绑定的Output，**不**执行跨Run历史全文查询。
- `MDSDataQuery`具备NamePrefix、DataType、Location、LoadState、RunId、ScopeId、Skip/Take；默认每页100，单页上限500。
- 名称/位置排序明确，但**并发修改下跨多页不承诺事务一致的整体快照**。
- 查询元信息不触发Load、不隐式驻留8GB文件、不获取裸可写Buffer。
- `Inspect`同名且跨多个Location有歧义时需要明确指定位置。

**UI边界**：数据Search/Inspect后端已经实现并有测试；Workbench实际数据面板、选择弹窗、跨Run交互式Inspector仍应作为M5的真实界面接入工作，不要把后端API误写成“全部UI完成”。

---

## 8. M4 绑定与普通C#函数易用性

### 8.1 七种来源语义

| `MDSBindingSource` | 语义 |
|---|---|
| `Constant` | 已验证的常量 |
| `CurrentSlot` | 本次调用Local或显式输入别名 |
| `NodeOutput` | 已经通过MRK真实StepOutput端口映射进当前Input的上游数据 |
| `FlowHeap` | 明确登记在本FlowHeap的值 |
| `Variable` | 已有PLC/IoT/Automation变量Owner提供 |
| `EngineeringResource` | 已有工程资源Owner提供 |
| `Expression` | 已有授权表达式Owner提供 |

`MDSDataBindingResolver`负责当前运行值解析与`CompatibleCandidates`兼容候选筛选。变量/工程资源/表达式必须通过`IMDSExternalBindingResolver`委托给原Owner，**不在数据机制内再建一套变量/配置/工程资源仓库**。未授权解析器一律拒绝。

### 8.2 强类型开发者接口

~~~csharp
var input  = new FDTDataKey<int>("ScoreInput");
var output = new FDTDataKey<int>("ResultOutput");

// 已由MRK按端口契约把ScoreInput映射为本次Input
int score = data.Get(input);
data.Set("Local", score + 1);
data.Return(output, score * 2);
~~~

`FDTDataKey<T>`减少字符串写错和类型歧义。纯业务函数可由既有`MDSDataFunctions.Invoke/InvokeAsync`使用当前已映射Input并Return输出；不新增第二个调度器。**同一个Output不可在同次调用重复Return**，所以上例和如下算法适配是两种独立写法，不应同时执行：

~~~csharp
int result = MDSDataFunctions.Invoke(
    data,
    new FDTDataKey<int>("ScoreInput"),
    new FDTDataKey<int>("ResultOutput"),
    x => x * 2);
~~~

---

## 9. 原子交接的可执行契约

1. **Bind**：MRK依据既有`MRKExecutionInput`和`DataOutputs`编译/校验输入来源、依赖关系和类型。
2. **Execute**：MRK调度节点，当前DataContext允许`Get/Set/Return`；Return只写Pending。
3. **Validate**：任务真实成功且所有声明的必需输出齐全；类型、取消事实和生命周期合法。
4. **Publish**：DataFrame通过DataStack一次性发布具名输出，其他节点只见完整已提交集合。
5. **Subflow barrier**：多节点子流程全部成功并通过`__commit`屏障后，父级才可得到声明的导出；不得越权直接读内部节点。
6. **Close**：局部Scope清理；已提交数据由Flow持有；失败/取消Abort，不产生半输出。
7. **Flow output**：`MRKFlowResult.CommittedOutputs/GetCommittedOutput<T>`仅供成功Flow取得已提交导出；不能把遗留`StepResult`的任意可变引用声称为安全只读共享。

### 9.1 并行与幂等

- 同一Plan并发启动两个Run，各自拥有独立DataStack、FlowHeap和结果。
- Scope不按CLR线程索引，同名Local不互相污染。
- 对真正不可变Buffer可同实例只读扇出；对可变数组防御复制。
- Retry清空上一次尝试的Local/Pending；有Heap即时副作用时不可假装已事务回滚。
- 超时和设备ACK丢失是不同概念：MDS不替代Automation/CT设备Owner作物理安全确认。

---

## 10. 真实案例、适用范围与仍需专业验证

| 场景 | 用到的现有M1–M4能力 | M5必须额外验证 |
|---|---|---|
| 在线X-Ray A采图→AI+CV→结果判定 | Output直连、只读共享、Flow保活、Return原子发布 | 相机SDK真实Buffer、误释放防护、实际节拍与SN隔离 |
| CT大文件→重建/ROI→MPR和检测 | FileLocator、受限Loader、专业Volume Kind、安全复制器、元数据检索 | 真实CT格式、GB级容量与ROI IO、显存/驱动生命周期 |
| 运动控制与采图 | 现有MRK和DataContext结合，输出/失败状态分离 | 硬件触发实时性、运动卡执行确认、SafeStop |
| IoT/SCADA | Binding委托原有点表/变量Owner | Quality/Timestamp、离线/断连、订阅与权限 |
| 多层专业子流程 | Frame/Scope、三层导出屏障、Output整体成功提交 | 工程编排UI、真实能力Catalog与跨专业错误追踪 |
| 运行数据检查 | Run/Scope搜索、未加载文件元数据、类型过滤 | Workbench实际Inspector、用户权限/脱敏与性能 |

这些是**能力映射**，不等于六条工业实机案例已经验收通过。早期V1.3的D01–D09、V01–V08、S01–S12、T01–T30、U01–U14仍有参考价值，但涉及默认`Ref/Lease`的写法须按现行裁决重写，且不可把全部测试标为已PASS。

---

## 11. 错误、安全和限制表

| 行为 | 要求 | 禁止 |
|---|---|---|
| 读不存在的数据 | 明确NotFound/`TryGet`返回false | 无声明仍默默取得父级Local |
| 类型不匹配 | 抛类型异常/结构化数据异常 | 静默强转或丢失Type |
| Input同名Set | 明确只读拒绝 | 改写上游真值 |
| Output少一项/失败 | 完整Abort，不能发布一部分 | 把返回意图当提交成功 |
| Heap/Stack Move/Copy目标同名 | 默认拒绝，显式覆盖 | 默认最后写入者胜出 |
| 复杂DTO无Adapter | 拒绝未知可变对象 | 把裸可变对象假装不可变 |
| FileLocator未加载 | 元数据可查、Get实体应报未加载 | Search偷偷触发全量Load |
| 文件正在加载 | 受控Busy/取消/失败状态 | Remove/Unload使结果落到失效Owner |
| 子流程内部Output | 只能通过导出屏障 | 父流程直连内部未导出的结果 |
| 已关闭Scope | 拒绝继续访问 | 迟到结果写入后继Run |
| SHM/GPU/SDK资源 | 专业另有明确安全协议和验收 | 用托管模拟PASS宣称已实机安全 |

平台当前有`MDSDataErrorCode`与`MDSDataException`，也会抛CLR/IO/MRK错误。完整的工业告警映射与Workbench提示文案尚需M5整合。

---

## 12. 五大阶段及真实进度

| 阶段 | 原定交付 | 截至本次复核的状态 | 明确未完成/需补验收 |
|---|---|---|---|
| **M1 数据基础核心** | Heap、Stack、Context、Scope、Frame | **托管P0核心已编码，有真实Windows测试** | V1.3所有候选类型不等于已落地 |
| **M2 数据流转与生命周期** | 端口直连、Move/Copy、原子交接、并行 | **托管P0核心已编码，有真实Windows测试** | 外部GPU/SHM/SDK精确最后消费者回收未验收 |
| **M3 数据存储与类型** | 自定义数据、文件定位、Load/Unload | **通用适配+磁盘Locator+受控Loader已编码，有测试** | 完整CT文件解码、强哈希、统一Quota/多层Cache、通用ROI引擎 |
| **M4 数据搜索与易用性** | Search/Inspect/Binding/自动注入 | **数据后端及MRK开发者入口已编码并测试** | Workbench完整搜索弹窗/Inspector/业务UI交互尚待集成 |
| **M5 系统整合与验收** | 真实专业场景、设备、压力、回归 | **尚未完成全部范围** | Vision/CT/Automation实机和多专业端到端、容量基线、外部资源安全 |

**裁决**：应基于真实用例完善不足，而非为了凑“48+15”空壳类提前增加项目和技术复杂度。M4的后端完成不代表其编辑器UI已完成；M5的硬件/容量/恢复验收也不能用小型Unit Test替代。

---

## 13. 实际测试和CI证据

| 测试来源 | 当前覆盖 |
|---|---|
| `Tests/ArchitectureSmoke/RuntimeDataLifecycleTests.cs` | M1/M2原子Output、Scope生命周期、取消、重试、跨Run隔离、子流程屏障、Move/Copy等 |
| `Tests/ArchitectureSmoke/RuntimeDataStorageTests.cs` | M3/M4自定义类型、磁盘定位、文件变化、取消、安全绑定、强类型函数及MRK流程 |
| `Tests/TestCenter/Cases/TCTRuntimeDataCases.cs` | `TCT.MDS.001–008`：Heap/Stack/Frame/并发/只读共享 |
| `Tests/TestCenter/Cases/TCTRuntimeDataStorageCases.cs` | `TCT.MDS.009–015、019、020、022`：DTO、文件Loader、CT大文件ROI示例、并发Copy |
| `Tests/TestCenter/Cases/TCTRuntimeDataUsabilityCases.cs` | `TCT.MDS.016–018、021`：Search/Inspect、Run/Scope、DataKey、Binding及取消 |

在检查到的`master=2bd1e87d...`上，以下CI返回`completed/success`：

- `CxVisual Config L1 Windows Build and Smoke`：完整解决方案及ArchitectureSmoke。
- `配置公共底座独立编译与验收`。
- `TestCenter Windows .NET48`。

核对方式：进入 [GitHub Actions](https://github.com/oudream/cx-visual-x3/actions)，指定最新Commit SHA核对运行结论、步骤、失败日志与Artifact。**禁止只引用一个较早成功SHA证明后续新提交通过。** 某轮F19调度测试曾首次失败、复跑成功，不能据此断言所有时序偶发问题已根治。

需要补充的工业级证据：实际4300×4300图像并行负载、8GB CT ROI IO及峰值内存、外部SDK持有/释放、SHM跨进程可靠恢复、硬件停止ACK、真实Workbench运行数据查看、持续生产稳定性。

---

## 14. M5后续执行顺序（给Codex和架构审查）

**M5-A：界面真实接入。** 在现有Workbench能力属性/参数选择器和调试数据查看器调用`MDSDataQuery`、`MDSDataBindingResolver`、MRK已有端口元数据。展示当前Run/Scope/Name/Type/State；选择兼容值；未加载CT只看摘要。**不新增第二份绑定来源仓库**。

**M5-B：Vision与在线检测真实链路。** 采图→AI+CV并行→判定；真实Buffer、产品SN隔离、超时/异常、硬件Owner安全回收，附性能与故障注入结果。

**M5-C：CT真实大数据。** 专业CT格式Loader与ROI/块处理；证明不会无意整卷加载；内存/显存峰值、文件版本正确性、多个消费者读取及安全Unload。

**M5-D：Automation与IoT/SCADA。** 接现有PLC/设备变量Owner；绑定后的Quality/Timestamp和设备运行语义不得丢失；断线和权限错误可追踪。

**M5-E：系统级可靠性。** 真实CI与TestCenter、异常回放、外部操作迟到、跨专业多Flow、配额/背压及资源回收证据；必要时再按真实用例扩展Carrier/Lease，不推翻现行Heap/Stack归属。

每个工作包提交源码路径、运行测试ID、失败反例、Windows编译与TestCenter/Runtime结果、Git SHA、风险和未通过项。不得以删测试、伪造结果或声明类已存在代替验收。

---

## 15. 来源与后续维护

以下是仓库内**确实存在并已读取**的三份主要前置文档：

1. [统一数据管理体系 V1.3（早期设计与候选清单）](https://github.com/oudream/cx-visual-x3/blob/master/DOCS/Architecture/CxVisual_%E7%BB%9F%E4%B8%80%E6%95%B0%E6%8D%AE%E7%AE%A1%E7%90%86%E4%BD%93%E7%B3%BB%20V1.3.md)
2. [运行时数据体系 M1/M2代码实现与验收记录 V1.1](https://github.com/oudream/cx-visual-x3/blob/master/DOCS/Architecture/CxVisual_运行时数据体系_M1M2_代码实现与验收记录_V1.1.md)
3. [运行时数据体系 M3/M4实现与验收基线 V1.0](https://github.com/oudream/cx-visual-x3/blob/master/DOCS/Architecture/CxVisual_运行时数据体系_M3M4_实现与验收基线_V1.0.md)

**文档维护规则**：以后优先更新**本V2.0总文档**中的“十条裁决、现有接口、两个项目Owner、阶段真实状态、测试/CI证据、M5缺口”，再用分阶段实施记录解释本次改变了什么。旧V1.3只保留需求和历史候选信息，不能让已废弃的Ref中心设计重新成为实现依据。

**最终结论**：CxVisual运行时数据体系已形成可运行的“Heap/Stack → 端口直连与原子提交 → 自定义数据/延迟加载 → Search/Inspect/Binding”核心链路。下一步的价值在**Workbench真实界面、Vision/CT/Automation真实业务对接和工业验证**，不在重新发明第三套Data工程。
