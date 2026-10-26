# CxVisual 数据与交互体系 #1 变量体系——重构后全类架构与归属方案 V1.2（讨论稿）

基于本轮对 `cx-visual-x2`、`cx-visual-x3` 和 `ReferTo/DeviceVariable` 的源码核查，我建议最终形成：

**一个变量内核、五大能力、统一管理、多个专业接入。**

不新建程序集，继续使用现有 Foundation、Mechanism、Professional 架构。下面列出建议保留、迁移、增构和新增的完整类型清单。

- 01 通用变量对象：类型 · 当前值 · 前值 · 默认值
- 02 变量组织树：Group · Table · Registry
- 03 运行状态：质量 · 来源 · 时间 · 快照
- 04 变化处理：检测 · 合并 · 派发 · 订阅
- 05 Provider 接入：设备 · 算法 · 流程 · 远程

## 一、最终项目归属总览

以下 68 项包括现有类、接口、枚举、DTO、迁移候选和测试类型，不代表要新增 68 个类文件。后续可以将内部辅助类型合并。

| 工程项目 | 类型数 | 核心职责 |
|---|---:|---|
| `Foundation.Data` | 3 | 基础数据类型、值适配 |
| `Mechanism.Core` | 3 | 工程变量定义与对象引用 |
| `Mechanism.Data / Variables` | 21 | 变量对象、树、运行值、状态 |
| `Mechanism.Data / Variables/Dispatch` | 8 | 变化检测、派发与订阅 |
| `Mechanism.Data / Variables/Providers` | 8 | Provider 注册、装配与接入 |
| `Mechanism.Connectivity` | 4 | 点表、规约、数据映射 |
| `Professional.Automation` | 5 | DeviceVariable 生产代码接入 |
| Runtime / Orchestration / Messages | 3 | 命令、工程编译、消息协作 |
| Presentation.Wpf / Studio | 7 | 编辑、监视、诊断界面 |
| ArchitectureSmoke / TestCenter | 6 | 正反例、性能及 UI 验证 |
| **合计** | **68** | **完整目标类型清单** |

说明：**现有**表示 x3 可查到的实现；**迁移**表示主要从 x2 或 ReferTo 吸收；**增构**表示在 x3 现有对象上扩展；**新增**表示目标候选类型。迁移和新增均未视为已经实现。

## 二、Foundation 与工程定义层（6 项）

### 1. Foundation.Data（3 项）

| 类 / 类型 | 职责 | 来源与处理 |
|---|---|---|
| `FDTDataTypeDescriptor` | 通用数据类型描述 | 现有，复用 |
| `FDTDataAdapters` | 类型及安全值适配 | 现有，扩充需要的变量适配 |
| `FDVVariableKey` | Scope + VariableId 的轻量跨模块定位值 | 新增候选 |

其中 `FDVVariableKey` 是地址值，不是第二套变量身份。应关联现有 MEM 工程对象 ID 与运行实例 Scope。

### 2. Mechanism.Core（3 项）

| 类 / 类型 | 职责 | 来源与处理 |
|---|---|---|
| `MEMResourceEntry` | 持久化资源定义及元数据 | 现有，复用 |
| `MRIObjectAddress` | 工程资源及对象地址 | 现有，复用 |
| `MPMBinding` | 变量与工程属性、参数绑定 | 现有，复用 |

**不建议新建 `VariableProject`、`VariableEngineeringTree` 或第二套持久化变量工程模型。**

变量定义从 MEM 的唯一工程真源进入运行时；MDS 中的定义只是编译或加载后的只读运行契约。

## 三、Mechanism.Data——变量核心（21 项）

建议统一命名空间：

`CxVisual.Mechanism.Data.Variables`

| 类 / 类型 | 主要职责 | 处理方式 |
|---|---|---|
| `MDSVariableQuality` | Good、Bad、Uncertain、Stale 等质量状态 | 现有，增构 |
| `IMDSVariableTable` | 现有变量表公共接口 | 保留并兼容扩展 |
| `MDSVariableDefinition` | ID、名称、数据类型、默认值、读写及元数据 | 现有，增构 |
| `MDSVariableNode` | 变量树通用节点，Id、Path、OwnerRef | x2 迁移 |
| `MDSVariableGroup` | 分组、层级、子节点与移动 | x2 迁移 |
| **`MDSVariable<T>`** | **单个完整变量对象，统一读写入口** | x2 迁移并整合 |
| `MDSVariableState<T>` | 单变量内部唯一运行状态 | 从 x2 RuntimeState 吸收 |
| `MDSVariableSnapshot<T>` | 完整且一致的只读状态快照 | x2 迁移 |
| `MDSVariableSample<T>` | 现有轻量采样 API | 保留兼容 |
| **`MDSVariableTable`** | **变量集合及旧设备变量表兼容门面** | x3 现有，重构内部 |
| **`MDSVariableRegistry`** | **变量与作用域总入口、ID/Path 检索** | x3 现有，增构 |
| `MDSVariableScope` | 工程、Host、设备、运行会话作用域 | 新增 |
| `MDSVariableHandle<T>` | 高频读写的强类型预绑定句柄 | 新增 |
| `MDSVariableUpdateOptions` | 来源、质量、序列、时间、期望版本 | 新增 |
| `MDSVariableUpdateResult` | 提交是否成功、变化种类及版本 | 新增 |
| `MDSVariableBatch` | 批量变量更新请求 | 新增 |
| `MDSVariableBatchResult` | 批量提交结果及失败原因 | 新增 |
| `MDSVariableStoreSnapshot` | 某 Scope 的整体运行时快照 | x2 迁移 |
| `MDSVariableValueRules` | 类型、比较、范围、值复制与校验 | x2 迁移，内部类 |
| `MDSVariableIndex` | 变量 ID、Path、别名高速索引 | x2 迁移，内部类 |
| `MDSVariableAccessPolicy` | 更新来源、权限、只读及授权判断 | 新增 |

### 最关键的对象关系

```text
MDSVariableRegistry
    │
    ├── MDSVariableScope
    │       │
    │       ├── MDSVariableGroup
    │       │       ├── MDSVariable<T>
    │       │       ├── MDSVariable<T>
    │       │       └── MDSVariableGroup
    │       │                  └── MDSVariable<T>
    │       │
    │       └── MDSVariableTable
    │                   （现有 API 兼容视图）
    │
    └── MDSVariableIndex
```

一个 `MDSVariable<T>` 对应一个权威运行值及其状态属性。

`MDSVariableTable` 和 `MDSVariableRegistry` 不能重新复制变量的 CurrentValue；它们只组织、索引、管理现有变量对象。

而 `MDSVariableState<T>` 应尽量作为 `MDSVariable<T>` 的内部实现，不公开第二个可写对象。

## 四、变化处理体系（8 项）

建议目录：

`Mechanism/Data/Variables/Dispatch/`

| 类 / 类型 | 职责 | 来源与处理 |
|---|---|---|
| `MDSVariableChange` | 一次变化的不可变记录（前值、新值、版本） | 新增 |
| `MDSVariableChangeBatch` | 一批变化事件及连续序列信息 | 新增 |
| `MDSVariableSubscriptionOptions` | 变化类型、过滤、合并、派发策略 | 新增 |
| `MDSVariableSubscription` | 订阅注册、取消及生命周期 | x2 迁移 |
| **`MDSVariableChangeDispatcher`** | **统一异步变化派发** | x2 Dispatcher + RuntimeStore 整合 |
| `MDSVariableDispatchBuffer` | 有界缓存、LatestOnly 合并与可靠队列策略 | 迁移并增构，内部类 |
| `MDSVariableDispatchMetrics` | 处理量、合并量、丢弃量、延迟 | 新增 |
| `MDSVariableDispatchPolicy` | Immediate、Batch、LatestOnly、Reliable 等模式 | 新增枚举 |

这一部分我建议作为 **变量体系的内部变化处理机制**，而不是建立第五个公共中心。

### 四种不同的处理策略

| 策略 | 语义 | 典型场景 |
|---|---|---|
| Immediate | 立即提交并通知，需满足延迟和线程约束 | 轻量本地控制状态 |
| Batch | 合并一批事件统一通知 | 普通设备状态和业务批量更新 |
| LatestOnly | 按变量身份只保留最新状态 | HMI、SCADA 状态展示 |
| Reliable | 不得静默漏掉必要事件；按明确策略处理容量不足 | 必须逐笔处理的业务变化 |

这些策略并非天然同等级的可靠传输保证；Reliable 必须定义容量不足时的拒绝、等待、故障恢复和订阅者确认语义。

**高频热路径与通知派发必须分离。** 对 LatestOnly 观察者，队列满不应反过来拒绝已经合法的实时值更新。

## 五、Provider 接入体系（8 项）

建议目录：

`Mechanism/Data/Variables/Providers/`

| 类 / 类型 | 职责 | 来源与处理 |
|---|---|---|
| `IMDSVariableProvider` | 统一 Provider 读写、订阅及生命周期契约 | x2 迁移 |
| `MDSVariableProviderDescriptor` | Provider 身份、能力与元数据 | x2 迁移 |
| `MDSVariableProviderRegistry` | Provider 注册、查找与注销 | 新增 |
| `MDSVariableProviderHost` | Provider 启停、断线、恢复、资源释放 | x2 迁移 |
| `MDSVariableProviderHealth` | 连接、质量、故障、更新时间和性能诊断 | x2 迁移 |
| `MDSVariableProviderBinding` | Provider 变量与正式 MDS 变量的映射 | 新增 |
| `MDSProtocolDataTargetAccess` | 现有 MPA 与 MDS 数据目标接入 | **x3 现有，继续增构** |
| `MDSVariableTypedPointBinding` | 预编译或预绑定点表高速访问，减少逐次反射 | 新增 |

Provider 可以提供设备采样、算法输出、仿真值和外部系统状态，但进入 MDS 后只应有一个权威变量值。

Provider 的缓存不是独立变量仓库；它只能是桥接过程中的缓存、只读镜像或明确的临时状态。

## 六、外围相关项目及适配类（12 项）

这些能力不应全部搬入 `Mechanism.Data`。

### 1. Mechanism.Connectivity（4 项）

| 现有类型 | 职责 |
|---|---|
| `MPAPointDefinition` | 规约点位定义 |
| `MPAPointTable` | 点位运行索引 |
| `MPADataMapping` | 数据目标映射 |
| `MPADataMappingExecutor` | 映射执行 |

这些是已有 Connectivity 能力，继续归原 Owner。

**PointTable 负责规约点位，不再维护第二份业务变量真值。**

### 2. Professional.Automation（5 项）

建议将 DeviceVariable 的工业专用适配集中在这里，而非引入新的程序集。

| 目标类 / 接口 | x2 来源 |
|---|---|
| `PADDeviceVariableCatalog` | `DeviceVariableCatalog` |
| `PADDeviceVariableProvider` | `DeviceVariableProvider` |
| `PADLegacyVariableManagerBridge` | `DirectVariableManagerAdapter` |
| `PADDeviceVariableMetadataAdapter` | `DeviceVariableMetadataAdapter` |
| `IPADDeviceVariableWriteBackend` | `IDeviceVariableWriteBackend` |

这些 `PAD` 名称是建议的 x3 适配命名，尚未定案或创建。

它们保留旧 `DeviceVariable` 的工业实践和对真实设备的受控读写，但不拥有 x3 的通用运行变量仓库。

### 3. Runtime、Orchestration 与 Messages（3 项）

| 类 | 归属 | 处理 |
|---|---|---|
| `MRKCommandMdsBinding` | `Mechanism.Runtime` | 现有，保留 |
| `MOCEngineeringCompiler` | `Mechanism.Orchestration` | 现有，扩展变量编译映射 |
| `MDSVariableMessageBridge` | `Mechanism.Data` | 新增候选，用于按需转发轻量变化事件 |

最后一项只是适配器，必须调用现有 `MNMMessageCenter`，不建设第二套消息总线。

## 七、界面与测试类（13 项）

### 1. Presentation.Wpf / Studio（7 项）

| 目标类 | 归属 | 职责 |
|---|---|---|
| `UVariableTreeViewModel` | Presentation.Wpf | 变量树浏览、搜索、筛选 |
| `UVariableEditorViewModel` | Presentation.Wpf | 定义、默认值、权限与属性编辑 |
| `UVariableWatchViewModel` | Presentation.Wpf | 实时值、前值、质量、来源监视 |
| `UVariableChangesViewModel` | Presentation.Wpf | 变化事件、批次、合并与诊断 |
| `UVariableProviderViewModel` | Presentation.Wpf | Provider 列表、连接、状态 |
| `UVariableMappingViewModel` | Presentation.Wpf | Point 与 Variable 映射 |
| `StudioVariableWorkspace` | Applications.Studio | 将上述面板装配到 Studio |

复用现有 WPF 控件、主题、停靠、属性编辑器和命令设施，不要求每个 ViewModel 都创建全新的独立控件体系。

### 2. ArchitectureSmoke / TestCenter（6 项）

| 目标测试类 | 归属 | 验收重点 |
|---|---|---|
| `VariableModelSmokeTests` | ArchitectureSmoke | 单变量、变量树、稳定 ID、路径 |
| `VariableStateSmokeTests` | ArchitectureSmoke | 前值、默认值、质量、版本、快照 |
| `VariableChangeDispatcherTests` | ArchitectureSmoke | 批量、顺序、合并、订阅及反例 |
| `VariableProviderIntegrationTests` | ArchitectureSmoke | Provider、点表、命令与来源隔离 |
| `VariableThroughputBenchmarks` | TestCenter | 百万级吞吐、并发、GC、p99 |
| `TCTVariableWorkbenchFrame` | TestCenter | 人工交互、性能及诊断测试页面 |

测试类名也是本次建议名称；实际集成时应先检查已有测试入口和 Case ID，避免重复建设。

## 八、哪些 x2 类不应该原样搬过来？

| x2 类 | 建议 |
|---|---|
| `Variable<T>` | 吸收为 `MDSVariable<T>` |
| `CxVariableRuntimeState<T>` | 将完整状态语义整合到单变量，不留双真源 |
| `VariableNode / VariableGroup` | 主要迁移对象组织实现 |
| `VariableRegistry` | 与现有 `MDSVariableRegistry` 合并 |
| `CxVariableStore` | 保留其定义、校验等能力，但由 MEM 工程真源和 MDS 运行定义承接 |
| `CxRuntimeVariableStore` | 迁移快照、恢复、订阅、容量等成熟机制，**不再单独保留第二个运行仓库** |
| `DeviceVariableChangeDispatcher` | 改造为通用 `MDSVariableChangeDispatcher` |
| `DeviceVariableProvider` | 工业适配部分进入 Professional.Automation |
| `VariableManager` 静态类 | 不恢复全局静态单例，仅保留必要旧接口兼容适配 |
| `VariableObject` | 留在专业变量对象/设备对象层，不当作全部变量的统一基类 |

## 九、需要优先确定的三个架构决策

**第一：单变量是最小权威运行对象。**

`MDSVariable<T>` 组合一个内部状态对象，VariableTable、Registry、Provider 不得复制另一份可写 CurrentValue。

**第二：工程组织与运行变量树是不同视图。**

MEM 保存定义与稳定引用；MDS 根据工程定义建立运行树。运行树可以快速索引、移动与管理活动节点，但不能悄悄变成第二套项目工程。

**第三：百万级热路径必须有独立性能设计。**

普通强类型变量读写不能默认经过反射、JSON、全表锁或同步订阅回调。`MDSVariableHandle<T>` 应作为高频固定点位的优化路径；普通开发者仍可用简单的 `Get/Update/Subscribe` API。

---

### 当前建议定案

我认为这份 **68 项目标类型清单可以作为 V1.2 类职责与归属审查基线**，但不能简单按 68 项创建文件。应先把现有 x3 类型、x2 可迁移类与新候选逐一对照，消除重复职责，再形成最终实际文件清单。

建议下一步优先正式裁决的核心对象是：

`MDSVariable<T>`、`MDSVariableGroup`、`MDSVariableTable`、`MDSVariableRegistry`、`MDSVariableChangeDispatcher`、`IMDSVariableProvider`。

这六个对象的职责一旦稳定，其余类型就可以围绕它们增量落位，而不会再次出现多个变量真源和多个变化中心。

本轮仍然只是目标架构与归属方案，未修改仓库。