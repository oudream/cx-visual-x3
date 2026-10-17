# CxVisual运行时数据体系：核心加固与真实Vision闭环实施验收 V2.1

> 更新：2026-10-17。仓库：oudream/cx-visual-x3，直接在master提交，中文Git Commit，未新增csproj。  
> **本阶段目标**：把原定M5前置的两项任务合流做完：①M1～M4当前托管P0核心加固；②用真实CPU Vision专业流程反推、验证该架构。  
> **证据规则**：以下描述的是已编入源码的行为；CI结果须在交付时引用对应SHA与GitHub Actions真实状态，不能因为“新增了类”就判定通过。此文档是V2.0总文档的实施补充，旧V1.3/V1.8的候选API不是所有已实现类。

## 1. 架构不变量

1. **所有运行时数据Owner仍只落在两个已有项目**：`CxVisual.Foundation.Data`和`CxVisual.Mechanism.Data`；未新增Data项目或第二个管理器。
2. 运行事实和节点端口仍由现有`MRKExecutionEngine/Plan/Ports`持有，专业算法仍属于`Professional.Vision`。
3. 普通节点A.Output→B.Input直连，不要求进入Heap；Heap仅负责显式共享、Move/Copy和磁盘Locator。
4. 多消费者只读共享；`FDTReadOnlyBuffer`和已证明不可变的专业`CxVisionGrayFrame`可以物理共享；`byte[]`仍作防御复制。
5. Move是逻辑Owner交接，Copy要建立可变独立值。同名默认拒绝，`replace:true`明确覆盖。
6. `Return`仅Pending，全部Output成功、端口类型通过及运行完成事实成立才原子Publish；失败/cancel/超时不向FlowResult导出半成品。
7. 文件`SetFile`只持有路径和描述；`Search/Inspect`不隐式Load；`Unload`与Heap.Dispose不删除外部原始文件。
8. **预算并非内存物理总量**：现阶段精准约束的是FlowHeap托管有效载荷与DataStack已提交Outputs的**逻辑持有量**。节点Local、暂存复制峰值、CLR对象头、GPU显存、SDK句柄、非托管缓冲仍不纳入该数字，不能以此宣称整进程RSS强上界。

## 2. 实际新增及增构源码

| 归属 | 代码 | 本轮可验证功能 |
|---|---|---|
| Foundation.Data | `Types/FDTDataSize.cs` | `FDTDataSize.TryEstimateBytes`，对标量、`byte[]`、只读Buffer及专业适配器估算托管载荷 |
| Foundation.Data | `Types/FDTDataAdapters.cs` | 专业`Register<T>(copier, immutable, kind, sizeEstimator)`；未知专业类型在有限预算下没有尺寸估算器就拒绝 |
| Mechanism.Data | `RuntimeData/MDSDataBudget.cs` | `MDSDataBudget`，有`LimitBytes`、`RetainedBytes`，安全Reserve/Release |
| Mechanism.Data | `RuntimeData/MDSDataHeap.cs` | Set、Copy/Move、Replace、Remove、Dispose容量计费；专业Copy准备移出锁，提交前复核来源令牌 |
| Mechanism.Data | `RuntimeData/MDSDataStack.cs` | 已提交Outputs与Heap共享一个预算；成功释放时归零；具名Output读取不在Stack锁内执行专业复制器 |
| Mechanism.Data | `RuntimeData/MDSDataFiles.cs` | 加载缓存计费、Unload释放；有限预算拒绝超过负荷的FileLoad；托管节点迟到结果不能在节点结算后发布 |
| Mechanism.Data | `RuntimeData/MDSDataContext.cs` | Context结算与FileLoad发布屏障同步，防止节点结束后IO污染仍活动的Flow |
| Mechanism.Data | `RuntimeData/MDSDataHeap.Query.cs` / `MDSDataQuery.cs` | 精确Inspect使用直接Heap条目定位；搜索支持ExactName、已有Run/Scope和分页；Heap查询在生成元数据前过滤 |
| 现有MRK执行器 | `MRKExecutionEngine.cs` | 新增可选`dataPayloadBudgetBytes`，每个Flow有独立预算；不改变原Worker、DAG和端口Owner |
| Professional.Vision | `CxVisionDataFlow.cs` | **真实**CPU五节点图像流程；8/16位小端RAW、阈值与连通域、并行统计、最终判断、原子Flow导出 |
| Professional.Vision | `CxVisionInspectionRuntime.cs` | 真实CPU算法在像素阈值循环及连通域BFS中定期检查CancellationToken，不把令牌请求当设备ACK |

旧`MDEExecutionDataScope`仍是Scope Owner，增加内部条目令牌做安全Copy快照校验；没有对外要求开发者创建DataRef/Lease。

## 3. 数据预算：规则和示例

~~~csharp
// 业务开发者保持简单。只有希望限制Flow已持有数据量的宿主才配置：
var engine = new MRKExecutionEngine(
    new FCRWorkerOptions(4, 16),
    dataPayloadBudgetBytes: 128L * 1024 * 1024);

MRKFlowResult result = await engine.StartFlow(plan, inputs).Completion;

// 专业类型若要在有限预算中持有，必须明确提供尺寸估算：
data.Adapters.Register<MyVolume>(
    DeepCopyVolume,
    kind: FDTDataKind.Volume,
    sizeEstimator: v => v.EstimatedResidentBytes);
~~~

这不是用户必须手工控制Allocate/Free的方案。容量不足抛`MDSDataException(QuotaExceeded)`，Set/Move/Copy不创建半条目；Stream/设备/显存不假借这个预算管理。Copy期间的临时分配是实际使用的额外内存，逻辑预算不对它提供物理上界；后续真实大图压力和分块能力应分别量测峰值。

**收费与释放状态**：
- Heap.Set / Replace / Move / Copy / Remove：变化只在完整安全提交后生效；
- File.Load成功：缓存才入账；Failed/Cancelled/超预算不产生缓存；Unload释放已入账缓存；
- DataStack.Publish：全部声明输出安全快照和容量通过后整体入账；
- Flow结束：已提交Stack输出及Heap条目的收费均释放；
- 预算本身线程安全；不同Run应使用独立`MDSDataBudget`，避免各个生产SN串账。

**性能与安全**：
- Heap/Stack的普通Get及具名Output读取，复杂专业Adapter复制在容器锁外进行；
- Copy先复制，随后检查原来源条目Token，变化即Busy，原目标不变；
- FileLoader异步完成前检查文件定位仍有效、当前节点仍有发布权；
- 查询完全不读大Buffer或加载磁盘文件，Heap精确Inspect不需要遍历全部数据。

## 4. 真实Vision五节点案例

~~~text
FlowInputs
  RawPixels: FDTReadOnlyBuffer（8-bit或16-bit小端RAW）
  Width, Height: int
      |
      v
  Decode      RAW解码 → CxVisionGrayFrame（真正不可变专业图像）
      |
      v
  Preprocess  像素背景值清理；返回新的专业图像
      |
      +------------------------+
      v                        v
  Inspect                 Statistics
  阈值+四邻接连通域       全图灰度统计
  Result                  GraySum
      +------------------------+
                   |
                   v
  Decision    检测结果与采图质量汇合
                   |
                   v
  Flow.DataExports:
    Inspection: CxVisionInspectionResult
    GraySum: long
    Passed: bool
~~~

**原本的专业算法没有被重新模拟**：实际调用`CxVisionThresholdPipeline.Inspect`，采用真实`ushort[]`像素阈值、四邻接区域追踪、面积筛选；`CxVisionGrayFrame`负责只读像素所有权；`CxVisionInspectionResult`由专业安全复制器深复制Region，避免最终结果可写别名。

**单个Run真实数据接口**：

~~~csharp
CxVisionInspectionOptions options = new CxVisionInspectionOptions {
    PixelDepth = CxVisionPixelDepth.Bits8,
    GrayThreshold = 200,
    MinDefectArea = 2,
    MaxDefectArea = 100
};

MRKExecutionPlan plan = CxVisionDataFlow.CreateCpuInspectionPlan(options, revision: 17);
var inputs = new Dictionary<string, object> {
    ["RawPixels"] = new FDTReadOnlyBuffer(rawPixels),
    ["Width"] = width,
    ["Height"] = height
};
MRKFlowResult result = await engine.StartFlow(plan, inputs).Completion;
if (result.State == MRKFlowState.Completed) {
    CxVisionInspectionResult detected =
        result.GetCommittedOutput<CxVisionInspectionResult>("Inspection");
    bool passed = result.GetCommittedOutput<bool>("Passed");
}
~~~

RAW 16位以小端字节对解析；既有Vision算法可处理8/16位强度。这个案例**不**接入X-Ray硬件、相机SDK、AI推理模型、实际采集触发或CT重建，不宣称具备设备安全确认。

### 为什么这条真实场景能反推底层

- 图像至少需要两个下游并行只读消费节点，促使DataStack读取不长时间持锁复制；
- 图像与缺陷结果属于两种不同专业类型，促使自定义`Copy/Kind/SizeEstimator`清晰分离；
- 多次`Return`与节点成功事实必须分离，失败图像不产生完整Flow结果；
- 超大图像要求准实时取消与容量拒绝机制，而不能让Worker对全部像素永久无响应；
- 多Run同时运行同一Plan且输入不同，必须具有独立FlowStack、Heap、Adapter表与预算。

## 5. 真实自动测试入口

### ArchitectureSmoke（既有Tests项目）

| 文件/方法 | 实际场景 |
|---|---|
| `RuntimeVisionDataFlowTests.RealPixelDetectionAndFanout` | 8位真实缺陷检测：像素阈值、面积、灰度和、质量判定、专业结果防御复制 |
| `RealPixelNoDefectAndFailure` | 切换阈值导致结果实际变化；错误尺寸不能导出半结果 |
| `SixteenBitRealPixelInspection` | 16位RAW小端真实阈值计算；截断文件失败 |
| `ConcurrentFlowOwnershipIsolation` | 两个不同输入的Run并行，同一Plan不交叉 |
| `RuntimeDataHardeningTests.HeapQuotaAndAtomicCopy` | Set/Copy超额拒绝、Replace/Remove归还预算 |
| `SharedFlowStackAndHeapBudget` | 同一FlowHeap与CommittedOutput共用限额，失败无半提交 |
| `CrossHeapQuotaAndUnknownDto` | 跨Heap Move超额目标不变，缺尺寸估算的专业DTO拒绝 |
| `FileLoadOverBudgetNoCache` | 文件缓存超过预算不入库、不删磁盘文件 |
| `LateNodeFileLoadMustNotPublish` | Frame结束后迟到的文件IO不得提交 |
| `RealVisionBudgetFailClosed` | 真正专业Vision流程限额不足不得导出半结果；限额足够可成功 |
| `InspectAcrossLargePage / MetadataLargeScaleSearch` | 560、4200个元数据的精确Inspect、分页、检索性能 |
| `SlowAdapterCannotHoldStackLock` | 专业深复制被人为阻塞时，其他输出仍可读取 |

原有`RuntimeDataLifecycleTests`及`RuntimeDataStorageTests`继续参与完整回归。

### TestCenter（既有项目，TCT自动发现）

- `TCT.MDS.023`：真实8位Vision五节点流程；
- `TCT.MDS.024`：真实16位Vision流程与错误数据失败；
- `TCT.MDS.025`：真实Vision超容量失败不能发布部分结果；
- 以前的`TCT.MDS.001～022`仍保留，不删除、不以新的演示代替原有回归。

## 6. 本轮明确完成与未完成

**已开发的本轮托管P0目标**：

- 安全容器、节点端口、原子输出、子流程交接及独立Run隔离（承接M1/M2）；
- 已提交Output与Heap缓存有共享预算；Quota失败不能破坏源/目标；
- 复制器移出Stack读取与Heap/Stack转移关键锁，并按来源条目Token复核；
- 加载中/迟到IO以及文件Unload、取消与原始路径所有权得到明确保护；
- Run/Scope+类型元数据、精确Inspect、搜索过滤和有限规模分页；
- 已存在Vision真实CPU算法端到端接入，8/16位图像与并行统计实证。

**尚未宣称完成的生产级内容**：

1. GPU/相机SDK/共享内存/非托管Buffer的精确释放和跨进程安全，不可用托管测试伪装。
2. 全进程RSS/GC LOH峰值预算；目前`MDSDataBudget`只计入已提交Stack/Heap持有有效载荷，临时副本与SDK内存未计入。
3. 数GB CT文件强Hash/分块可恢复性和真实重建；专业ROI Loader接口并不等于CT算法已完整提供。
4. Workbench实际运行数据选择器/Inspector WPF交互；属于后续M5界面与工程编排接入。
5. 工业相机触发、运动控制卡、X-Ray设备ACK、超时后硬件动作停止确认及跨专业产线节拍性能。

## 7. 后续开发顺序

下一轮**不建议再独立建设Data系统新项目**。建议：
1. 把已存在`MDSDataBindingResolver`/`Search`/`Inspect`真正接到Studio参数编辑与流程运行观测面板；
2. 使用本次的真实`CxVisionDataFlow`演示工程用户选择输入、连线、运行、Inspect五节点结果；
3. 之后CT分块大数据/真实Vision相机SDK/Automation PLC逐一加入专业Owner及实机故障注入；
4. 做真实节拍下的峰值内存、后台IO取消、卸载和多Flow压力基准。

验收和文档持续回写`CxVisual_运行时数据体系_完整设计与实施总文档_V2.0.md`，最新源码、测试用例、Git SHA与CI是最终判定依据，而非这份文字本身。
