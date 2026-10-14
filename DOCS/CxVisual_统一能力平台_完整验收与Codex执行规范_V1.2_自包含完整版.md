# CxVisual 统一能力平台——完整验收与 Codex 执行规范 V1.2（自包含完整版）

> **日期**：2026-09-30  
> **适用代码基线**：`CxVisualX2-V6.6` 及后续演进  
> **文档性质**：架构冻结基线 + 完整类契约 + Codex 实施说明 + 自动化测试规范 + 真实场景验收规范  
> **替代关系**：本文件 **替代 V1.0 与 V1.1 作为后续 Codex 的唯一主指导文件**。  
> **自包含原则**：不得要求 Codex 必须同时打开另一份“类表”才能理解本架构；全量类、属性、方法、测试和 Case 均在本文件中给出。  
> **核心目标**：不是“把类建出来”，而是让 CxVisual 从 Definition 到 Runtime 真正闭环：**能编辑、能保存、能加载、能引用、能连接、能编译、能运行、能调试、能恢复、能扩展、能发布**。

---

# 目录

1. 产品目标与验收范围  
2. Codex 执行红线  
3. 5 个历史系统、6 Workbenches、8 个 Case  
4. 总体架构与三个世界  
5. 六核心对象：Project/Page/Content/Symbol/Group/Element  
6. Ownership / Reference / Connection / Public Contract  
7. CxNode DOM 完整能力  
8. Studio / Editing / Drawing / Diagram  
9. Property / Variable / Script / Command / Event / Time / Security  
10. Channel / Protocol / Driver / Provider 分层  
11. DataHandle / Stream / Resource / Placement  
12. Symbol 功能块、Logic、Invocation 与 Operation Adapter  
13. Validate → Resolve → Compile  
14. Runtime Plan 与 Runtime 核心  
15. Persistence / Version / Plugin / Package / Deploy  
16. 旧功能代码接入的 10 个真实案例  
17. 5 大历史系统回归 Gate  
18. 6 Workbench 验收 Gate  
19. 8 个代表 Case 完整验收  
20. 测试体系  
21. 性能与稳定性 Gate  
22. V6.6 迁移策略  
23. Codex P0～P6 实施计划  
24. Code Review 禁止项  
25. Definition of Done  
26. 全量类、属性、方法表（349 个契约）

---

# 1. 产品目标与验收范围

CxVisual 的产品定位固定为：

> **统一能力展现、统一工程组织、统一资源引用、统一连接、统一专业编排、统一编译、统一运行、统一调试、统一仿真、统一发布的工业工程与运行平台。**

它不是：

- 纯 HMI；
- 纯画图软件；
- 纯 Graph 编辑器；
- 单一 PLC/SCADA 软件；
- 单一 Vision/CT 算法工具；
- 把 Vendor SDK、AI、CT、Motion 全塞进 Common 的“大杂烩”。

CxVisual 必须形成以下稳定主干：

```text
1 Platform
    +
6 Workbenches
    +
统一 Definition DOM
    +
Reference / Port / Connection
    +
Visual / Logic / Data / Collaboration
    +
Validate / Resolve / Compile
    +
Runtime / Resource / Policy / Trace / Recovery
    +
Plugin / Capability / Package / Deploy
```

最终工程链路必须固定为：

```text
Studio 编辑 Project
        ↓
Project / Page / Content / Symbol / Group / Element
        ↓
CxNode Definition DOM
        ↓
Save / Load
        ↓
Bind Parent / Owner / Index
        ↓
Validate
        ↓
Resolve
        ↓
CxResolvedProject
        ↓
Compile
        ↓
CxRuntimePlan
        ↓
CxProjectRuntimeHost
        ↓
ExecutionRuntimeKernel
        ↓
OperationHandler / RuntimeAdapter / Provider
        ↓
既有 C# / C++ / Go 功能代码、Worker、设备、算法
```

**验收不以“能编译”作为完成。必须跑通真实 Case。**

---

# 2. Codex 执行红线

Codex 必须遵守以下冻结规则：

1. **V6.6 是资产，不得推倒重写。**
2. 现有 `PageModel.RootContent`、`ContentDefinition.OwnedContents`、`VisualChildren`、`ContentReferences`、`SymbolReferences`、`SymbolDefinition.RootGroup`、`GroupElementModel.Children`、`PageObjectIndex / ProjectObjectIndex` 必须优先适配和收口。
3. **不得重新建立第二套竞争性 Domain Tree。**
4. **不得恢复“万能 GraphNode”成为工程组织主干。** DAG/Graph/Execution Tree 只能存在于编译态/运行态内部 IR/Plan。
5. `ProjectModel / PageModel / ContentDefinition / SymbolDefinition / ElementModel` 统一继承 `CxNode`；`GroupElementModel : ElementModel : CxNode`。
6. **Ownership / Reference / Connection 永久分离。**
7. `ParentNode` 是运行时导航关系，不单独持久化 `ParentId`。
8. `ChildNodes` 对外只读；结构修改走 Mutation API。
9. Definition 不直接执行设备、算法、线程、GPU、PLC、Camera。
10. DOM Event 与工业 Runtime Event 分开：`CxDomEvent` ≠ `CxRuntimeEvent` / Event Port。
11. 大图像、Projection、Volume、大数组、Stream 不放普通 Property 深复制。
12. 旧功能代码通过 Adapter/Provider/OperationHandler 接入，不为平台化而重写正确功能。
13. Platform 只拥有稳定机制；专业语义留在 Workbench。
14. Channel 只管传输；Protocol 只管规约；Driver 只管专业地址/Vendor 映射；Provider 向上暴露稳定能力。
15. Runtime 不得直接执行 Definition Node。
16. 编译前必须完成 Reference / Port / Type / Capability / Resource / Security / Placement Resolve。
17. 所有公共类必须能说明：它解决了哪一个 5 大系统、6 Workbench 或 8 Case 的真实需求。
18. 所有结构性重构必须有迁移测试与 RoundTrip 测试。
19. 所有阶段必须保持现有项目可构建，不允许“先大爆炸重构，最后再修”。
20. 只有本文 Gate 全过，才可声明“底座完成”。

---

# 3. 验收范围：5 个历史系统 + 6 Workbenches + 8 Case

## 3.1 5 个历史系统

| 系统 | 已做过/已验证的核心能力 | CxVisual 必须抽象出的公共能力 |
|---|---|---|
| 图像检测 | Acquire、增强、传统算法、AI、测量、Judge、结果上传、GPU、大图像 | Typed Port、DataHandle、Operation、FanOut/Join、GPU/Camera Resource、Trace |
| CT 程序 | Motion、ProjectionSeries、Geometry、Calibration、Recon、Volume、MPR、3D、Heavy Job | DataCarrier、Long Job、Progress、Cancel、Placement、ResourceScope |
| 离线机运控 | Home、Move、WaitIO、Trigger、Interlock、Procedure、StateMachine、Pause/Resume | Command/Event/Flow、Decision/Wait/Loop、Interlock、Retry/Timeout、Runtime |
| ICS/SCADA | Channel、Protocol、点表、Variable、Quality、Timestamp、Alarm、Historian、Command | Channel/Protocol、VariableRuntime、Subscription、History、Provider |
| IoT | TCP/UDP/HTTP/MQTT/Serial、多规约、Device/Product/Point、断网补传 | Channel/Protocol、Streaming、StoreForward、Edge Placement、Trigger |

## 3.2 1 Platform + 6 Workbenches

| 一级能力 | 核心定位 | 主要拥有 / 吸纳 | 不应承担 |
|---|---|---|---|
| Common / Platform | 全平台机制与稳定契约 | DOM、Resource、Reference、Type、Property、Variable、Channel、Protocol、Compiler、Runtime、Security、Plugin、Diagnostics | Axis/FDK/AI/Judge 等专业语义 |
| Automation | 机器连接、控制、采集、测量、设备级编排 | PLC/SCADA/IoT、Motion、Robot、IO、Camera/Detector/XRay、DAQ/Test | 2D/3D 算法内部、最终质量判定 |
| Simulation | 虚拟世界、回放、故障、验证 | VirtualDevice、Replay、Clock、Fault、Hybrid、Twin、Scenario | 第二套 Real 工程 |
| HMI | 表现与人机交互 | Page、Symbol、Binding、State、Animation、Action、Dashboard、Trend、Viewer | 专业控制/算法/检测 |
| Vision | 2D 视觉事实与算法编排 | Image、ROI、Calibration、Traditional/AI、Pipeline、VisionResult | Camera 硬件控制、产品最终 OK/NG |
| CT3D | CT/3D 事实与算法编排 | Projection、Geometry、Calibration、Recon、Volume、MPR、3D Analysis、CTResult | XRay/Axis 控制、最终判定 |
| Inspection | 产品质量业务与 Durable Workflow | Product、Plan、Point、Judge、Recheck、Review、Result、Trace、Recovery、MES | Vendor SDK、专业算法实现 |

## 3.3 8 个正式代表 Case

1. 普通 PLC + HMI 自动机；
2. Robot Cell；
3. X-Ray 2D 在线检测；
4. 工业 CT；
5. 气密 / Test / Measurement；
6. SCADA 集中监控；
7. IoT Edge；
8. 多工位检测线。

每个 Case 的最低闭环：

```text
Create/Edit
→ Undo/Redo
→ Save
→ Load
→ Bind Parent/Owner
→ Rebuild Index
→ Validate
→ Resolve
→ Compile
→ Runtime
→ Data/Event/Command/Flow
→ Fault
→ Recovery
→ Trace/Debug
→ Simulation（适用）
→ Result/History
→ Package/Deploy
```

---

# 4. 总体架构与三个世界

CxVisual 必须明确三个世界：

```text
┌─────────────────────────────────────────────┐
│ Definition World                            │
│ Project/Page/Content/Symbol/Group/Element   │
│ Property/Reference/Port/Connection/Aspect   │
└───────────────────┬─────────────────────────┘
                    │ Validate + Resolve
                    ▼
┌─────────────────────────────────────────────┐
│ Resolved / Plan World                       │
│ CxResolvedProject                           │
│ VisualPlan / LogicPlan / DataPlan           │
│ ConnectionPlan / ResourcePlan               │
│ DeploymentPlan                              │
└───────────────────┬─────────────────────────┘
                    │ Instantiate
                    ▼
┌─────────────────────────────────────────────┐
│ Runtime World                               │
│ ProjectRuntimeHost / ExecutionRuntimeKernel │
│ ExecutionInstance / Scope / Context         │
│ DataScope / ResourceScope / EventBus        │
│ RuntimeAdapter / Provider / Worker          │
└─────────────────────────────────────────────┘
```

三者严禁互相污染：

- Definition 保存用户工程意图；
- Resolved/Plan 为机器优化；
- Runtime 保存瞬态状态与资源。

---

# 5. 六核心对象

## 5.1 继承

```text
CxEventTarget
└─ CxNode
   ├─ ProjectModel
   ├─ PageModel
   ├─ ContentDefinition
   ├─ SymbolDefinition
   └─ ElementModel
      └─ GroupElementModel
```

## 5.2 Ownership

```text
Project
├─ Page
│  └─ RootContent
│     ├─ Content
│     ├─ Group
│     └─ Element
├─ Shared Content
└─ Symbol
   └─ RootGroup
      ├─ Group
      └─ Element
```

规则：

- Page 最多 1 个 `RootContent`；
- Content 可拥有 Content 与 Element；
- Group 是 Element 的一种，可拥有 Element/Group；
- Symbol 最多 1 个 `RootGroup`；
- 普通 Element 默认叶子；
- 专业 Element 需要 Children 时必须通过 Policy 明确开放；
- `ContentReference / SymbolReference` 不属于 Child；
- `CxConnection` 不属于 Child。

## 5.3 Project

Project 是类似 DOM Document 的工程根，必须负责：

```text
Pages
Symbols
SharedContents
Resources
Variables
Connections
Workbench/Plugin requirements
RuntimeProfiles
SchemaVersion
ProjectObjectIndex
```

但 Project **不是普通流程功能块**，不应被用户拿来当 Flow Node。

## 5.4 Page

Page 是：

```text
View / Work Surface / Runtime Projection Boundary
```

主要保存：

```text
RootContent
Size / Background / ViewConfig
Navigation
Parameters
```

Page 默认不承担业务执行语义。

## 5.5 Content

Content 是平台最重要的业务组合边界：

```text
Content
├─ OwnedContents
├─ VisualChildren
├─ ContentReferences
├─ SymbolReferences
├─ PublicPorts
├─ Connections
├─ Parameters
├─ Visual
├─ Logic
├─ Data
├─ Collaboration
└─ RuntimePolicy
```

它同时是：

- Composition Boundary；
- Public Contract Boundary；
- Connection Scope；
- Logic Scope；
- Data Scope；
- Runtime Scope。

## 5.6 Symbol

Symbol 不只是图标，必须是：

> **可复用复合视觉/语义/功能块定义。**

固定语义类别：

```text
Visual
Operation
CompositeOperation
Decision
Wait
Trigger
State
Resource
Adapter
```

通过 `CxSymbolKind + Aspect + CxExecutionBinding` 表达，不创建大量 `OperationSymbol/WaitSymbol/...` 公共继承类。

Symbol 必须支持：

```text
RootGroup
PublicProperties
PublicPorts
PropertyMappings
PortMappings
Version
State
Behavior
Logic/Data/Collaboration
ExecutionBinding
```

外部禁止直接连接 Symbol 内部 Element，必须通过 PublicPort/PublicProperty。

## 5.7 Group

Group 只负责轻量视觉组合：

```text
Children
Local Coordinate
Bounds
Transform
Visibility
HitTest
Visual Anchor
```

如果 Group 开始需要正式公共属性、业务 Port、Version、独立逻辑，应升级为 Symbol。

## 5.8 Element

Element 是最小原子 Definition。原子不等于只视觉。

不同 Element 可以通过 `Capabilities / Aspect / ExecutionBinding` 拥有：

```text
Visual
Data
Event
Command
Flow
Resource
Execution
```

但定义类本身不直接执行。

---

# 6. Ownership / Reference / Connection / Public Contract

## 6.1 三种关系

| 关系 | 表达 | 持久化 | 生命周期 | 编译 |
|---|---|---|---|---|
| Ownership | Parent/ChildNodes | 是 | 拥有 | 形成 Definition Tree |
| Reference | CxReference | 是 | 不拥有 | Resolve Target |
| Connection | CxPort + CxConnection | 是 | Scope 拥有 Connection | Compile RuntimeConnection |

## 6.2 Parent

`ParentNode` 必须存在，但：

```text
Child collection = 持久化真源
ParentNode       = Load/Edit 时自动维护
```

禁止同时保存两套真源。

## 6.3 Connection Scope

- Content 内连接：由该 Content 拥有；
- 子 Content A 与 B 连接：经 A.PublicPort / B.PublicPort，由最近共同父 Content 拥有 Connection；
- Symbol 外部连接：只连 Symbol PublicPort；
- Page 不直接承载业务 Connection；
- Project 只允许全局 Service/Resource 类连接。

---

# 7. CxNode DOM 完整能力

CxNode 必须覆盖 System.Xml.XmlNode 的稳定能力，并融合 Web DOM。

## 7.1 Navigation

```text
ParentNode
ChildNodes
FirstChild / LastChild
PreviousSibling / NextSibling
OwnerProject / OwnerPage
HasChildNodes
Root
Address / Path
```

## 7.2 Mutation

```text
AppendChild
PrependChild
InsertBefore
InsertAfter
ReplaceChild
RemoveChild
RemoveAll

Append
Prepend
Before
After
Remove
ReplaceWith
ReplaceChildren
```

所有结构修改必须原子维护：

```text
Parent
Index
Address cache
MutationObserver
Undo/Redo
Dirty
Selection validity
Compile cache invalidation
```

## 7.3 Traversal / Query

```text
Ancestors / AncestorsAndSelf
Descendants / DescendantsAndSelf
ElementsBeforeSelf / ElementsAfterSelf
FindById / Name / Type / Role / Tag
SelectSingleNode / SelectNodes
QuerySelector / QuerySelectorAll
Matches / Closest
TreeWalker
NodeIterator
```

## 7.4 Clone

至少区分：

```text
CloneNode(deep, preserveId)
CloneAsNew(deep)        // Studio复制默认
ClonePreserveIds()      // Template/内部特殊用途
```

默认 Copy/Paste 必须重新生成 Id，并修复内部 Reference。

## 7.5 DOM Event

```text
Capture
Target
Bubble
```

仅用于 Studio/Node/Property/Selection/Structure 类事件。

业务 `Alarm / ImageReady / Completed / ProductArrived` 必须走 Runtime Event Contract/EventPort。

---

# 8. Studio / Editing / Drawing / Diagram

平台必须有独立 Studio 层，不得把 UI 编辑行为塞进 Node。

至少支持：

```text
DocumentHost
Selection
Undo/Redo
Clipboard
Search
Navigation
Dirty
MultiDocument
Ribbon/Toolbox/ContextMenu/Dock Contribution
```

Drawing：

```text
Canvas
Viewport
Geometry
Transform
HitTest
Adorner
Snap
Layer
Overlay
SVG
```

Diagram：

```text
NodePresenter
PortPresenter
EdgePresenter
Routing
AutoLayout
Minimap
RuntimeAdornment
```

WPF Visual Tree / Logical Tree 仅为 Presentation Projection，不是 CxVisual Domain Truth Source。

---

# 9. Property / Variable / Script / Command / Event / Time / Security

## 9.1 Attribute vs Property

`CxAttribute`：轻量结构元数据，类似 XML Attribute。  
`CxProperty`：强类型工业属性。

禁止把 Bounds/Style/Behavior/Operation 大量退化成字符串 Attribute。

## 9.2 Variable

运行变量必须保留：

```text
Value
PreviousValue
Quality
RefreshTimestamp
ChangeTimestamp
Source
Reason
Sequence
OnlineState
```

这是从 SCADA/Measure 实战反推出来的，不得简化成 Name+Value。

## 9.3 Script/Expression

公共层支持：

```text
Expression
Condition
Rule
Script
Sandbox/Security
```

表达式可服务 Decision、Interlock、Binding、Alarm、Judge Rule，但专业算法不写进 Expression。

## 9.4 Command/State/Event/Action

必须有稳定 Contract：

```text
CommandRequest / CommandResult
StateDescriptor / StateSnapshot
EventContract / RuntimeEvent
ActionDescriptor
```

Action 可映射：

```text
Command
Script
Navigation
Invocation
UI action
```

## 9.5 Time

Runtime 不直接到处 `DateTime.Now/Task.Delay`，通过 `ICxClock`：

```text
SystemClock
SimulationClock
ReplayClock
```

这样 Wait/Timeout/Replay/Simulation 才统一。

## 9.6 Security / Interlock / Audit

必须支持：

```text
Identity
Role / Permission
SafetyMode
Interlock
Controlled Write
Audit
```

尤其 Automation/HMI/Inspection 的受控写必须先过权限与 Interlock。

---

# 10. Channel / Protocol / Driver / Provider 分层

调用链冻结：

```text
Workbench Runtime
      ↓
Provider / Capability
      ↓
Driver / Adapter
      ↓
Protocol Session
      ↓
Channel
      ↓
OS / Hardware / Remote Worker
```

职责：

| 层 | 负责 | 禁止 |
|---|---|---|
| Channel | Bytes/Message/Stream、Buffer、Backpressure、Reconnect、Health | PLC寄存器、机器人命令 |
| Protocol | Codec、Frame、Parser、Session、Request/Response、Heartbeat、Error | HMI/Inspection业务 |
| Driver | Vendor/专业地址/设备映射 | 污染 Platform Core |
| Provider | Variable/Command/State/Resource 稳定能力 | 暴露 Socket 细节 |
| Runtime | 专业业务执行 | 直接依赖 Vendor SDK |

Channel 必须支持：

```text
InProc / IPC / TCP / UDP / Serial / SharedMemory / Message / Stream
Open / Close / Send / Receive / RequestResponse / PubSub
Buffer / Backpressure / Reconnect / Heartbeat / Health / Correlation
```

Protocol 必须支持：

```text
Descriptor
Frame/Message Schema
Codec
Parser/Serializer
Correlation/Transaction
Session/Handshake
Request/Response
Notification
Heartbeat
Version
Error Mapping
Capability
```

具体 Modbus/S7/OPC UA/MQTT/IEC104 等可以由插件/Driver 实现。

---

# 11. DataHandle / Stream / Resource / Placement

## 11.1 大数据

图像、ProjectionSeries、Volume 必须：

```text
CxDataHandle
      ↓
ICxDataCarrier
Memory / SharedMemory / File / Remote / Cache
```

连接只传 Handle/Contract，不深复制 GB 对象。

## 11.2 Stream

`CxStreamPolicy` 至少：

```text
Capacity
Backpressure
DropPolicy
BatchSize
Ordering
NoDrop
```

X-Ray 高节拍场景必须验证 NoDrop。

## 11.3 Resource

设备/GPU/Axis/Camera/PLC 等：

```text
CxResourceDefinition
CxResourceRequirement
CxResourceRef
CxResourceScope
CxLeaseHandle
ICxResourceProvider
```

资源生命周期属于 Runtime Scope，不属于 Node 删除逻辑。

## 11.4 Placement

支持：

```text
Local
WorkerProcess
GPUWorker
RemoteMachine
PLC
RobotController
Edge
```

Compile 时解析 Placement；Runtime 只执行 Plan。

---

# 12. Symbol 功能块、Logic、Invocation 与 Operation Adapter

## 12.1 功能代码写法

Definition 中只保存：

```text
Symbol/Element
Properties
Ports
Reference
CxExecutionBinding.OperationId
```

真实实现：

```csharp
public interface ICxOperationHandler
{
    string OperationId { get; }

    Task<CxOperationResult> ExecuteAsync(
        CxOperationContext context,
        CancellationToken cancellationToken);
}
```

Runtime：

```text
ExecutionStep
→ OperationRegistry
→ Handler/Adapter
→ 旧功能类
```

## 12.2 Logic

公共逻辑语义：

```text
ControlFlow
DataFlow
Workflow
StateMachine
ReactiveEvent
```

必须显式表达：

```text
Operation
Decision
Wait Time/Event/Condition
Loop
Break/Continue
Parallel
Join
Trigger
State/Transition
Invocation
MultiInstance
```

循环不得靠“发现图里有环”来猜语义，必须有 `CxLoopDefinition`。

## 12.3 Activity / Sequence 的关系

CxVisual 的编辑态 Flow 很像 UML Activity Diagram；  
Runtime Trace 可以生成类似 Sequence Diagram 的时序观察视图。

Sequence Diagram 不作为主要执行 Definition。

## 12.4 Invocation

Content/Workbench 跨边界调用使用：

```text
CxInvocationSpec
InputMappings
OutputMappings
VersionBinding
Placement
Policy
```

长任务使用 `CxJobHandle`。

---

# 13. Validate → Resolve → Compile

## 13.1 Validate 顺序

```text
Structural
Type
Reference
Port/Connection
Capability
Resource
Security/Safety
Persistence/Durable
Placement
Compatibility
```

## 13.2 Resolve

Resolve 必须产生：

```text
CxResolvedProject
├─ ResolvedNode
├─ ResolvedSymbolInstance
├─ ResolvedConnection
├─ ResolvedResourceBinding
├─ EffectiveProperty
├─ EffectivePort
└─ Placement
```

必须处理：

```text
Symbol实例展开
Property Override
Port Mapping
Reference
Type
Capability
Resource
VersionBinding
Placement
```

## 13.3 Compile

生成：

```text
CxRuntimePlan
├─ PagePlan
├─ ContentPlan
├─ VisualPlan
├─ LogicPlan
├─ DataPlan
├─ ConnectionPlan
├─ ResourcePlan
└─ DeploymentPlan
```

相同 Definition + RuntimeProfile 应生成确定性 Plan Hash。

---

# 14. Runtime Plan 与 Runtime 核心

Runtime 主结构：

```text
CxProjectRuntimeHost
├─ CxPageRuntimeProjection
├─ CxContentRuntimeInstance
│  └─ ExecutionInstance
│     ├─ ExecutionContext
│     ├─ ExecutionScope
│     ├─ CxRuntimeDataScope
│     └─ CxResourceScope
├─ ExecutionRuntimeKernel
│  ├─ ExecutionScheduler
│  ├─ ExecutionStep
│  ├─ RuntimeTransition
│  ├─ ConditionalTransition
│  ├─ LoopRuntime
│  ├─ WaitRuntime
│  └─ ParallelRuntime
├─ CxRuntimeEventBus
├─ CxRuntimeVariableStore
├─ CxRuntimeTrace
├─ CxFaultManager
├─ CxRecoveryManager
└─ CxCheckpointManager
```

Runtime 必须支持：

```text
Initialize
Start
Stop
Pause
Resume
Step
Cancel
Timeout
Retry
Fault
Recovery
Checkpoint
Restore
Trace
Metric
Health
```

---

# 15. Persistence / Version / Plugin / Package / Deploy

至少包括：

```text
CxProjectSerializer
CxNodeSerializer
CxSchemaVersion
CxMigrationManager
CxUnknownNode
CxRawPayload

CxProjectManifest
CxPluginManifest
CxPackageDescriptor
CxPackageBuilder
CxDeployPlan
CxDeploymentService
CxCompatibilityChecker
```

未知插件类型必须保留 RawPayload，不能打开工程就丢数据。

V6.6 → 新版本必须有明确 migration path。

---

# 16. 旧功能代码接入：10 个真实案例

| 系统 | 旧功能类 | 关键方法 | 新入口 | Operation / Provider |
|---|---|---|---|---|
| Vision | `ImageStatisticsService` | `Calculate/GetHistogram/Analyze/GetLineProfile` | OperationHandler | `vision.image.statistics` |
| Vision | `ImageMeasurementService` | `GetDistance/GetAngle/GetArea` | OperationHandler | `vision.measure` |
| CT | `RawVolumeLoader` | `Load/InferInfo/LoadRawBuffer` | CT Handler + DataHandle | `ct.volume.load` |
| CT | `SliceImageBuilder` | `Build/ImageToVoxel/VoxelToImage/GetVoxel` | CT Handler | `ct.slice.build` |
| Motion | `ControllerMethod` | `Home/AbsMove/JogMove/Stop/CheckMove/ReadDI/WriteDO` | Motion RuntimeAdapter | `motion.axis.move` |
| Motion | `Procedure` | `Start/Pause/Resume/Stop/GetProcedureStatus` | Legacy Handler | `legacy.procedure.run` |
| SCADA | `ProtocolBase` | `start/stop/setChannel/writeBytes/isOnline` | Protocol Adapter | `scada.protocol.command` |
| SCADA | `MeasureTemplate<T>` | `value/setValue/quality/time/source/reason` | VariableProvider | Provider |
| IoT | `TcpC` | `Send/Close/IsOpened` | IChannel Adapter | `iot.tcp.send` |
| IoT | `MqttC` | `Deal/UnDeal/Open/Close` | MQTT Adapter | `iot.mqtt.subscribe` |

Gate：测试必须证明 Runtime 真实进入 Handler/Adapter/Provider，不接受只生成假 Plan。

---

# 17. 5 大历史系统回归 Gate

## 17.1 Vision

```text
Acquire
→ Enhance
→ AI Detect || Traditional Detect
→ Merge
→ Measure
→ Judge
→ Upload
```

必须验：

- Image Handle；
- GPU Resource；
- Parallel/Join；
- Timeout/Cancel；
- Result Merge；
- Trace；
- 不深复制。

## 17.2 CT

```text
Home/Move
→ Projection Acquire
→ ProjectionSeries
→ Geometry/Calibration
→ Reconstruction
→ Volume
→ MPR / Analysis
```

必须验：

- Projection 完整性；
- 2~10GB Volume；
- SharedMemory/File Carrier；
- GPU Placement；
- Progress/Cancel；
- Heavy Job；
- Fault/Recovery。

## 17.3 Motion

```text
Home
→ Move
→ Decision
→ WaitIO
→ Trigger
→ Loop / Recovery
```

验：

```text
Manual/Auto
Interlock
Timeout
Pause/Resume
Stop
Retry
Recovery
```

## 17.4 SCADA

```text
Channel
→ Protocol
→ Driver
→ VariableProvider
→ RuntimeVariableStore
→ Alarm/History/HMI/Command
```

验 10k+ Variable 与 Quality/Timestamp/Source/Reason。

## 17.5 IoT

```text
Channel
→ Protocol
→ Telemetry
→ Rule
→ StoreForward
→ Broker/Cloud
```

验断网自治、补传、重连、Twin/Telemetry、Remote Command。

---

# 18. 6 Workbench 验收 Gate

| Workbench | Definition | Editor | Compiler | Runtime | 结果 |
|---|---|---|---|---|---|
| Automation | Device/PLC/IO/Motion/Robot/DAQ/XRay/IoT/Flow | Device/Flow/State/Motion/Robot | ControlFlow/State/Event/Resource | Provider/Driver/Automation Runtime | Variable/Alarm/Trace |
| Simulation | VirtualDevice/Scenario/Replay/Fault/Twin | Scenario/Timeline/Fault | Replay/Virtual/Hybrid | SimulationRuntime | Compare/Replay |
| HMI | Page/Symbol/Binding/Action/Trend/Viewer | Designer | VisualPlan/Binding | PageRuntimeProjection | Dashboard/Trend |
| Vision | Image/ROI/Calibration/Pipeline | Image/ROI/Pipeline | Typed DataFlow | Vision/GPU Worker | VisionResult |
| CT3D | Projection/Geometry/Recon/Volume/MPR | CT/MPR/3D | Heavy DataFlow | CT/GPU Worker | CTResult |
| Inspection | Product/Plan/Point/Judge/Recheck/Review/MES | Plan/Point/Result | Durable Workflow | Inspection Runtime | Product Result/Trace |

每个 Workbench 必须能通过 `ICxWorkbenchModule` 注册：

```text
Types
Capabilities
Editors
Compilers
OperationHandlers
RuntimeAdapters
StudioContributions
```

---

# 19. 8 个代表 Case 完整验收

## Case 1：PLC + HMI 自动机

必须包含：

```text
PLC/IO
Variable
State/Mode
Recipe
Flow
Wait
Alarm
Interlock
HMI Page/Symbol
Command
```

重点：Auto/Manual、受控写、Alarm、Recipe、Recovery。

## Case 2：Robot Cell

必须包含：

```text
Robot
Frame
Tool
Zone
Pose
Move
Vision Guidance
Teach
Resource
Interlock
Simulation
```

## Case 3：X-Ray 2D 在线检测

必须包含：

```text
Motion
Trigger
XRay
Detector
Image Stream
AI + Traditional Parallel
Judge
MES
NoDrop
```

## Case 4：工业 CT

必须包含：

```text
Axis
Detector
Projection
Geometry
Calibration
Recon
Volume
MPR
GPU
Heavy Job
```

## Case 5：气密 / Test Measurement

必须包含：

```text
Valve
Sensor
DAQ
Instrument
Pressurize
Wait
Measure
Curve
Judge
Retry/Recheck
```

## Case 6：SCADA

必须包含：

```text
Channel
Protocol
Point
Variable
Quality
Alarm
Trend
Historian
Command
```

## Case 7：IoT Edge

必须包含：

```text
Device
Channel
Protocol
Telemetry
Twin
Rule
StoreForward
RemoteCommand
```

## Case 8：多工位检测线

必须包含：

```text
Line
Station
Partition
Parallel/MultiInstance
SharedResource
Inspection
MES
Idempotency
Checkpoint
Durable Recovery
```

所有 Case 必须走完整：

```text
Edit → Save → Load → Validate → Resolve → Compile → Run → Fault → Recover → Trace
```

---

# 20. 测试体系

## 20.1 DOM / Tree

```text
Node_ProjectIsRoot
Node_PageOwnsSingleRootContent
Node_ContentOwnsContentAndElements
Node_SymbolOwnsSingleRootGroup
Node_GroupOwnsNestedElements
Node_ElementIsLeafByDefault

Node_ParentRebuiltAfterDeserialize
Node_ParentChangesAfterMove
Node_FirstLastCorrect
Node_PreviousNextSiblingCorrect
Node_AncestorsCorrect
Node_DescendantsCorrect

Node_AppendChildAtomic
Node_PrependChildAtomic
Node_InsertBeforeAtomic
Node_InsertAfterAtomic
Node_ReplaceChildAtomic
Node_RemoveChildAtomic
Node_RemoveAllAtomic

Node_CloneShallow
Node_CloneDeep
Node_CloneAsNewIds
Node_CopyPasteRepairsInternalRefs

Node_QueryByIdNameTypeRoleTag
Node_QuerySelector
Node_Matches
Node_Closest
Node_TreeWalkerOrder
Node_IteratorOrder

DomEvent_CaptureTargetBubble
DomEvent_StopPropagation
DomEvent_PreventDefault
MutationObserver_AddRemoveMoveProperty
```

## 20.2 Editing

```text
Undo_AddRemoveMoveReplace
Undo_GroupUngroup
Undo_Property
Undo_Connection
Clipboard_CopyPaste
Dirty_SaveLoad
Search_RevealNode
Navigation_BackForward
MultiDocument_ContextIsolation
```

## 20.3 Serialization / Migration

```text
Serialization_ParentNotPersisted
Serialization_RuntimeStateNotPersisted
Serialization_V6Loads
Serialization_V6RoundTripNoLoss
Serialization_UnknownNodeRawPayload
Migration_PathDeterministic
Migration_RepeatSafe
```

## 20.4 Reference / Symbol

```text
Reference_Content
Reference_Symbol
Reference_Variable
Reference_Resource
Reference_Cycle
Reference_MissingRequired

Symbol_UniqueInstanceAddress
Symbol_RuntimeStateIsolation
Symbol_PublicPropertyMapping
Symbol_PublicPortMapping
Symbol_InternalRefactorKeepsContract
Symbol_VersionBinding
```

## 20.5 Port / Connection

```text
Port_Direction
Port_Type
Port_Multiplicity
Port_Required
Connection_Scope
Connection_CrossContentPublicPorts
Connection_CrossSymbolPublicPorts
Connection_Condition
Connection_Disabled
Connection_RuntimeCreated
```

## 20.6 Expression / Security

```text
Expression_CompileEvaluate
Condition_TrueFalse
Rule_Priority
Script_Sandbox
Permission_Deny
Interlock_Block
Audit_ControlledWrite
```

## 20.7 Channel / Protocol

```text
Channel_OpenClose
Channel_SendReceive
Channel_RequestResponse
Channel_Reconnect
Channel_Backpressure
Channel_Health
Protocol_CodecRoundTrip
Protocol_FrameFragmentation
Protocol_Correlation
Protocol_Heartbeat
Protocol_ErrorMapping
Layering_NoReverseDependency
```

## 20.8 Compiler

```text
Validate_Structural
Validate_Type
Validate_Reference
Validate_Port
Validate_Capability
Validate_Resource
Validate_Security
Validate_Durability
Validate_Placement

Resolve_Symbol
Resolve_Override
Resolve_PortMapping
Resolve_Resource
Resolve_Placement

Compile_Operation
Compile_Decision
Compile_WaitTime
Compile_WaitEvent
Compile_WaitCondition
Compile_Loop
Compile_BreakContinue
Compile_ParallelJoin
Compile_StateMachine
Compile_Trigger
Compile_Invocation
Compile_MultiInstance
Compile_DataFlow
Compile_Visual
Compile_Deployment
Compile_DeterministicHash
```

## 20.9 Runtime

```text
Runtime_StartStop
Runtime_PauseResume
Runtime_Step
Runtime_CancelPropagation
Runtime_HandlerInvoked
Runtime_AdapterPlacement
Runtime_DataTransfer
Runtime_EventBus
Runtime_Condition
Runtime_Loop
Runtime_WaitTimeout
Runtime_ParallelJoin
Runtime_Invocation
Runtime_MultiInstance
Runtime_ResourceRelease
Runtime_FaultPropagation
Runtime_Retry
Runtime_Recovery
Runtime_SnapshotRestore
Runtime_Idempotency
Runtime_TraceCallChain
```

## 20.10 Data / Variable / History

```text
DataHandle_NoDeepCopy
Carrier_Memory
Carrier_SharedMemory
Carrier_File
Stream_NoDrop
Stream_Backpressure
Variable_ValuePreviousQualityTimeSourceReason
Variable_OfflineOnline
Journal_Replay
Historian_Query
StoreForward_QueueFlush
```

---

# 21. 性能与稳定性 Gate

| 项目 | 最低 Gate |
|---|---|
| Node Tree | 10 万 Node Build/Traverse 无数量级退化 |
| ObjectIndex | 10 万对象 Id 查询近似 O(1) |
| Mutation | 单 Node Move 不允许无条件全工程 Rebuild |
| Variable | 10k+ Runtime Variable 稳定更新 |
| Vision | Image 通过 Handle 传递，不因普通 Connection 深复制 |
| CT | 2~10GB Volume 不进入 JSON/普通 Property 深复制 |
| Stream | NoDrop 必须有 Buffer/Backpressure 策略 |
| Runtime | Stop/Cancel/Fault 后 Lease 全释放 |
| Trace | Trace 开启不改变业务结果 |
| Compile | 相同输入 + Profile 输出相同 Plan Hash |
| Serialization | V6.6 RoundTrip 不丢字段 |
| Plugin | 缺少插件时 UnknownNode 不丢 Payload |

---

# 22. V6.6 迁移策略

必须 Adapter-first。

## P0 阶段不得直接废掉

```text
PageModel.RootContent
ContentDefinition.OwnedContents
ContentDefinition.VisualChildren
ContentDefinition.ContentReferences
ContentDefinition.SymbolReferences
SymbolDefinition.RootGroup
GroupElementModel.Children
PageObjectIndex
ProjectObjectIndex
ElementModel.DeepClone
现有 Logic/Data/Collaboration
现有 ExecutionCompiler / RuntimeKernel
```

做法：

```text
现有强类型集合
   ↓ Adapter/Projection
CxNode ChildNodes
```

禁止同时建立第二份可写 `List<CxNode>`。

所有旧 JSON 字段应优先兼容读取，逐步迁移而不是一次改坏。

---

# 23. Codex P0～P6 实施计划

## P0：CxNode DOM + Editing

实现：

```text
CxEventTarget
CxNode
CxNodeList
TreeBinder
Query/Selector
Walker/Iterator
Mutation
DOM Event
MutationObserver
ObjectIndex
Document/Selection/Undo/Clipboard/Dirty
```

Gate：DOM + Editing + V6 Load。

## P1：六核心对象 + Reference + Property + Port

实现：

```text
Project/Page/Content/Symbol/Element/Group
Property/Metadata
Reference/Resolver
PublicProperty/PublicPort/Mapping
ConnectionService
Variable/Binding
```

Gate：Ownership/Reference/Connection/Symbol 全测。

## P2：Channel / Protocol / Operation / Data / Resource

实现：

```text
Channel Foundation
Protocol Foundation
Operation Registry/Handler
Provider/Adapter
DataHandle/Carrier/Stream
Resource/Lease/Placement
```

Gate：先接通 10 个旧功能代码案例。

## P3：Expression / Logic / Invocation / Compiler

实现：

```text
Expression/Rule
Condition/Wait/Loop/Parallel/State/Trigger
Invocation/Job/MultiInstance
ResolvedProject
Validator/Resolver/Compiler
RuntimePlan
```

Gate：Compiler 全测。

## P4：Runtime / Debug / Recovery

实现：

```text
ProjectRuntimeHost
ExecutionRuntimeKernel
Instance/Context/Scope/Scheduler
Transition/Loop/Wait/Parallel
EventBus/VariableStore
Trace/Debug
Fault/Recovery
Snapshot/Durable/Idempotency
```

Gate：Runtime 全测。

## P5：Drawing / Diagram / Simulation / 6 Workbench 最低专业类

实现：

```text
Drawing
Diagram
RuntimeAdornment
Simulation
Automation minimum model
HMI minimum model
Vision minimum model
CT3D minimum model
Inspection minimum model
```

Gate：6 Workbench 可注册并建工程。

## P6：5 系统 + 8 Case + Release

执行完整回归、Package、Deploy、Compatibility。

每个阶段必须：

```text
Build pass
Unit tests pass
Previous phase regression pass
V6.6 compatibility pass
No forbidden dependency introduced
```

---

# 24. Code Review 禁止项

以下情况 Code Review 直接拒绝：

1. 新建第二套 Project/Page/Content 树；
2. 重新让 GraphNode 成为 Domain 主根；
3. 业务代码直接修改 `ChildNodes/Children.Add()`；
4. ParentId 和 Children 同时作为可持久化真源；
5. Symbol 外部直连内部 Element；
6. Content 跨边界直接连接内部节点；
7. Runtime 直接执行 `ContentDefinition/ElementModel`；
8. ElementModel 增加 `OpenCamera()/MoveAxis()/RunAI()` 等专业执行；
9. 大图/Volume 放 Property 并序列化；
10. Platform 引用具体 Vendor SDK；
11. Workbench Runtime 直接操作 Socket/Serial；
12. Channel 理解 PLC 寄存器语义；
13. Protocol 直接操作 HMI/Inspection；
14. UI/WPF Visual Tree 被当成 Domain Tree；
15. `DateTime.Now/Task.Delay` 散落在可仿真的 Runtime 核心；
16. 无 Cancellation 的长任务；
17. Resource Acquire 无对应 Release；
18. Retry 没有 Idempotency 考虑；
19. Fault 被日志吞掉，不进入 Trace/FaultManager；
20. 修改 Core 却没有增加对应真实 Case/Test Gate。

---

# 25. Definition of Done

只有以下全部满足，才算“统一平台底座完成”：

- CxNode DOM 完整；
- 六核心对象统一；
- Ownership/Reference/Connection 分离；
- Studio Editing 完整；
- Channel/Protocol 分层成立；
- DataHandle/Stream/Variable 成立；
- Operation/Provider/Adapter 能接旧功能；
- Resolve/Compile/RuntimePlan 完整；
- Runtime 生命周期/调度/资源/事件/数据闭环；
- Fault/Recovery/Trace/Debug 完整；
- Security/Interlock/Audit 对关键操作有效；
- Simulation 有 Clock/Replay/Fault/VirtualDevice；
- Plugin/Capability/Manifest/Package/Deploy 有统一契约；
- 5 个历史系统全部回归；
- 6 Workbenches 都能注册并闭环；
- 8 Case 全部从 Edit→Runtime→Recovery 通过；
- V6.6 RoundTrip 无数据丢失；
- 不存在第二套竞争对象模型；
- 新专业域无需修改 CxNode 根即可通过插件/Handler/Provider/Compiler/Adapter 扩展。

---

# 26. 全量类、属性、方法表

> **以下为本规范内置的完整 API 冻结清单，不再依赖外部类表。**
>
> 类表用于：Codex 建类、Code Review、查漏、测试映射、后续版本变更审查。  
> 若新增公共类，必须在本表增加并注明场景来源；若删除/改名，必须提供 migration 说明。

## A. Platform / Workbench / Plugin / Capability

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxPlatformManifest` | `—` | PlatformId, Name, Version, SchemaVersion, WorkbenchIds, RequiredModules, MinRuntimeVersion, Metadata | Validate(), IsCompatibleWith() | 平台/发布根清单 |
| `CxWorkbenchDefinition` | `—` | Id, Name, DisplayName, Description, Capabilities, Editors, Viewers, Commands, SupportedNodeKinds, RuntimeAdapters | Supports(), Validate() | Workbench 自描述 |
| `ICxWorkbenchModule` | `interface` | Definition | RegisterTypes(), RegisterCapabilities(), RegisterEditors(), RegisterCompilers(), RegisterRuntimeAdapters(), RegisterStudioContributions() | Workbench 插件入口 |
| `CxWorkbenchRegistry` | `—` | Items | Register(), Unregister(), Find(), GetAll(), GetByCapability() | Workbench 注册 |
| `CxCapabilityDescriptor` | `—` | Id, Name, Category, Version, ProviderId, SupportedNodeKinds, RequiredResources, Ports, Operations, RuntimePlacements, Metadata | IsCompatible(), Validate() | 能力展示/发现 |
| `CxCapabilityRegistry` | `—` | Descriptors | Register(), Unregister(), Find(), Query(), Resolve() | 能力目录 |
| `CxTypeDescriptor` | `—` | TypeId, ClrType, DisplayName, Category, Properties, Capabilities, SerializerId, CompilerId | CreateInstance(), Validate() | 类型描述 |
| `CxDescriptorRegistry` | `—` | Types, Operations, Ports, Resources | Register(), FindType(), FindOperation(), FindResource(), GetDescriptors() | 统一描述注册 |
| `CxPluginManifest` | `—` | PluginId, Name, Version, EntryAssembly, Dependencies, Capabilities, Permissions, Metadata | Validate(), IsCompatibleWith() | 插件清单 |
| `CxPluginCatalog` | `—` | Manifests | Discover(), Find(), ResolveDependencies() | 插件目录 |
| `CxPluginManager` | `—` | LoadedPlugins, Catalog | Load(), Unload(), Reload(), ResolveDependencies() | 插件生命周期 |

## B. DOM / Object Model

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxEventTarget` | `abstract` | — | AddEventListener(), RemoveEventListener(), DispatchEvent() | Web DOM EventTarget 基础 |
| `CxNode` | `CxEventTarget` | Id, Name, LocalName, NodeType, NamespaceUri, Prefix, Value, ParentNode, ChildNodes, FirstChild, LastChild, PreviousSibling, NextSibling, HasChildNodes, OwnerProject, OwnerPage, Attributes, Properties, References, Ports, Address, Path, IsReadOnly, IsConnected, Metadata, Capabilities | AppendChild(), PrependChild(), InsertBefore(), InsertAfter(), ReplaceChild(), RemoveChild(), RemoveAll(), Append(), Prepend(), Before(), After(), Remove(), ReplaceWith(), ReplaceChildren(), Clone(), CloneNode(), Ancestors(), AncestorsAndSelf(), Descendants(), DescendantsAndSelf(), ElementsBeforeSelf(), ElementsAfterSelf(), SelectSingleNode(), SelectNodes(), QuerySelector(), QuerySelectorAll(), Matches(), Closest(), FindById(), FindByName(), FindByType<T>(), FindByRole(), FindByTag(), Contains(), GetRootNode(), Normalize(), IsSameNode(), IsEqualNode(), CompareDocumentPosition(), GetNamespaceOfPrefix(), GetPrefixOfNamespace(), WriteTo(), WriteContentTo(), Validate() | 统一 Definition DOM 根 |
| `CxNodeList` | `IReadOnlyList<CxNode>` | Count, Indexer | GetEnumerator(), IndexOf() | 只读 ChildNodes |
| `CxNodeCollection` | `internal` | Count | AppendInternal(), PrependInternal(), InsertInternal(), RemoveInternal(), ReplaceInternal(), MoveInternal(), ClearInternal() | Node 内部拥有集合 |
| `CxNodeAddress` | `—` | ProjectId, PageId, NodeIds, InstanceScope | Parse(), TryParse(), Combine(), Parent(), ToString() | 稳定地址 |
| `CxNodePath` | `—` | Segments | Parse(), Combine(), Normalize(), ToString() | 人类可读 Path |
| `CxNodeQuery` | `—` | NodeType, Name, Role, Tag, Predicate | Match() | 强类型查询 |
| `CxSelector` | `—` | Expression, Ast | Parse(), Match(), Select(), SelectFirst() | CSS-like Selector |
| `CxTreeWalker` | `—` | Root, CurrentNode, Filter | ParentNode(), FirstChild(), LastChild(), PreviousSibling(), NextSibling(), PreviousNode(), NextNode() | 树游走 |
| `CxNodeIterator` | `—` | Root, Filter, Current | NextNode(), PreviousNode(), Reset() | 顺序遍历 |
| `CxNodePolicy` | `—` | Rules | CanAddChild(), CanRemove(), CanMove(), CanConnect(), CanReference(), Validate() | 规则 |
| `CxNodeTreeBinder` | `—` | — | Bind(), BindSubtree(), UnbindSubtree(), RebuildParents(), RebuildOwners() | Load 后重建导航 |
| `CxObjectIndex` | `abstract` | ById, ByName, ByType, ByRole, ByTag | Build(), Add(), Remove(), Move(), Refresh(), FindById(), FindByName(), FindByType(), FindByRole(), FindByTag() | 索引基类 |
| `PageObjectIndex` | `CxObjectIndex` | PageId, SpatialIndex | Build(PageModel), HitTest(), Query() | 页面索引 |
| `ProjectObjectIndex` | `CxObjectIndex` | ProjectId | Build(ProjectModel), FindPage(), FindSymbol(), FindContent(), Resolve() | 工程索引 |

## C. Attribute / Property / Metadata / Validation

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxAttribute` | `—` | Name, LocalName, NamespaceUri, Prefix, Value | Clone() | 轻量结构属性 |
| `CxAttributeCollection` | `—` | Count, Indexer | Get(), Set(), Remove(), Contains(), Clear(), GetEnumerator() | Attribute 集合 |
| `CxProperty` | `—` | Name, Value, ValueType, DefaultValue, IsReadOnly, IsVisible, Category, Unit, Metadata | SetValue(), Reset(), ValidateValue() | 强类型属性 |
| `CxPropertyCollection` | `—` | Count, Indexer | Get(), Set(), TryGet(), Contains(), Remove(), Reset() | Property 集合 |
| `CxPropertyDescriptor` | `—` | Name, DisplayName, Category, ValueType, Unit, Range, EnumItems, ReadOnly, Visibility, EditorTemplate, ValidationRules | ValidateValue(), CreateEditorContext() | 属性编辑描述 |
| `CxMetadata` | `—` | Items | Get<T>(), Set<T>(), Remove(), Merge(), Clone() | 元数据 |
| `CxValidationIssue` | `—` | Code, Severity, NodeAddress, PropertyName, Message, Suggestion | — | 验证问题 |
| `CxValidationResult` | `—` | Issues, IsValid, HasWarnings | Add(), Merge(), ThrowIfInvalid() | 验证结果 |
| `ICxValidator<T>` | `interface` | — | Validate(T value, CxValidationContext context) | 通用验证器 |
| `CxValidationContext` | `—` | Project, Node, RuntimeProfile, StrictMode, Services | CreateChild() | 验证上下文 |

## D. Editing Foundation / Studio

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxDocumentHost` | `—` | Documents, ActiveDocument | Open(), Close(), Activate(), Save(), SaveAll(), IsDirty() | 多文档编辑 |
| `CxDocumentContext` | `—` | Project, ActivePage, ActiveResource, Selection, CommandContext, RunMode | SetActivePage(), SetActiveResource() | Studio 上下文 |
| `CxSelectionService` | `—` | SelectedNodes, PrimarySelection | Select(), Add(), Remove(), Clear(), Toggle(), SelectRange() | 选择 |
| `CxUndoService` | `—` | UndoStack, RedoStack, TransactionDepth | BeginTransaction(), Commit(), Rollback(), Execute(), Undo(), Redo(), Clear() | Undo/Redo |
| `ICxUndoableCommand` | `interface` | Name | Execute(), Undo(), Redo(), CanMergeWith(), Merge() | 可逆编辑命令 |
| `CxClipboardService` | `—` | Formats | Copy(), Cut(), Paste(), CanPaste(), SerializeSelection() | 剪贴板 |
| `CxSearchService` | `—` | LastQuery | SearchProject(), SearchPage(), FindNext(), Replace(), ReplaceAll() | 搜索 |
| `CxNavigationService` | `—` | History | NavigateTo(), Back(), Forward(), RevealNode() | 导航 |
| `CxDirtyTracker` | `—` | DirtyResources | MarkDirty(), MarkClean(), IsDirty(), GetDirtyResources() | Dirty |
| `CxStudioCommand` | `—` | Id, Name, CanExecute, Shortcut, Category | Execute() | Studio 命令 |
| `CxCommandRegistry` | `—` | Commands | Register(), Unregister(), Find(), QueryByContext() | 命令注册 |
| `CxStudioContribution` | `—` | RibbonItems, ToolboxItems, ContextMenus, DockPanels, StatusItems, DebugPanels | Apply(), Remove() | Workbench UI 贡献 |
| `CxStudioContributionRegistry` | `—` | Contributions | Register(), Unregister(), BuildForContext() | 动态 Ribbon/面板 |

## E. Core Definition Objects

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `ProjectModel` | `CxNode` | Pages, Symbols, SharedContents, Resources, Variables, Connections, Workbenches, Settings, RuntimeProfiles, ObjectIndex, SchemaVersion, Version | CreatePage(), RemovePage(), FindPage(), FindSymbol(), FindContent(), FindResource(), ResolveReference(), Validate() | 工程根 |
| `PageModel` | `CxNode` | RootContent, Width, Height, Background, Parameters, Navigation, ViewConfig, RuntimePolicy, Tags | SetRootContent(), FindById(), FindByName(), Validate() | 页面/工作视图 |
| `ContentDefinition` | `CxNode` | OwnedContents, VisualChildren, ContentReferences, SymbolReferences, PublicPorts, Connections, Parameters, Visual, Logic, Data, Collaboration, RuntimePolicy, ExecutionModel, Tags | AddConnection(), RemoveConnection(), FindPort(), AddPublicPort(), RemovePublicPort(), Validate() | 业务组合/编排/Scope |
| `SymbolDefinition` | `CxNode` | RootGroup, Version, SymbolKind, PublicProperties, PublicPorts, PropertyMappings, PortMappings, States, Behavior, Logic, Data, Collaboration, ExecutionBinding, Tags | MapProperty(), UnmapProperty(), MapPort(), UnmapPort(), CreateInstanceSpec(), Validate() | 可复用复合功能块 |
| `ElementModel` | `CxNode abstract` | TypeId, LayerId, Bounds, Transform, Style, ZIndex, IsVisible, IsEnabled, IsLocked, IsSelectable, Bindings, Capabilities, Semantics, Behavior, Parameters, ExecutionBinding | DeepClone(), HitTest(), Validate() | 最小原子定义 |
| `GroupElementModel` | `ElementModel` | Children, UseLocalCoordinates, LocalWidth, LocalHeight, LocalBounds, GroupTransform | Group(), Ungroup(), RecalculateBounds(), DeepClone() | 轻量视觉组合 |
| `CxSymbolInstanceSpec` | `—` | InstanceId, DefinitionId, VersionBinding, Overrides, Bindings, PublicPortBindings | Validate(), Clone() | Symbol 实例规格 |
| `CxSymbolKind` | `enum` | Visual, Operation, CompositeOperation, Decision, Wait, Trigger, State, Resource, Adapter | — | Symbol语义 |

## F. Reference / Resource Definition

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxReference` | `—` | Id, Name, ReferenceKind, TargetId, TargetPath, TargetType, Required, VersionBinding | Validate() | 引用基类 |
| `ContentReference` | `CxReference` | Overrides | ResolveContent() | ContentRef |
| `SymbolReference` | `CxReference` | InstanceId, Overrides, Bindings | ResolveSymbol(), CreateInstanceSpec() | SymbolRef |
| `CxVariableReference` | `CxReference` | ExpectedType | ResolveVariable() | VariableRef |
| `CxResourceRef` | `CxReference` | ResourceType, RequiredCapability, PlacementConstraint | ResolveResource() | ResourceRef |
| `CxReferenceCollection` | `—` | Count | Add(), Remove(), Find(), ResolveAll() | 引用集合 |
| `CxReferenceResolver` | `—` | Project, Index | Resolve(), ResolveAll(), DetectCycles(), ExplainFailure() | 引用解析 |
| `CxReferenceIssue` | `—` | Reference, Severity, Code, Message | — | 引用错误 |
| `CxResourceDefinition` | `—` | Id, Name, ResourceType, ProviderId, Capabilities, Configuration, Placement, Tags | Validate() | 资源定义 |
| `CxResourceDescriptor` | `—` | ResourceType, DisplayName, Capabilities, ShareMode, ConfigurationSchema | ValidateConfig() | 资源元数据 |
| `CxResourceRegistry` | `—` | Resources | Register(), Unregister(), Find(), QueryByCapability() | 资源注册 |

## G. Port / Connection / Mapping

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxPort` | `—` | Id, Name, Kind, Direction, ContractType, Multiplicity, Required, VisualInfo, Metadata | Validate(), IsCompatibleWith() | 对象级连接点 |
| `CxPortCollection` | `—` | Count | Add(), Remove(), Find(), FindInput(), FindOutput(), GetByKind() | Port集合 |
| `CxPortAddress` | `—` | NodeAddress, PortId, InstanceScope | Parse(), ToString(), Resolve() | Port地址 |
| `CxConnection` | `—` | Id, Source, Target, Kind, Enabled, Condition, Priority, Metadata | Validate() | 设计态连接 |
| `CxConnectionCollection` | `—` | Count | Add(), Remove(), FindBySource(), FindByTarget(), FindBetween() | Connection集合 |
| `CxConnectionValidator` | `—` | — | Validate(), ValidateDirection(), ValidateType(), ValidateMultiplicity(), ValidateScope(), ValidateCyclePolicy() | 连接验证 |
| `CxConnectionService` | `—` | Validator | Connect(), Disconnect(), Reconnect(), CanConnect(), GetSuggestedTargets() | 连线入口 |
| `CxPortResolver` | `—` | TypeSystem | Resolve(), ResolveAll(), CheckCompatibility() | Port解析 |
| `CxPublicPropertyDefinition` | `—` | Name, DisplayName, ValueType, DefaultValue, ReadOnly, Metadata | Validate() | 公开属性 |
| `CxPropertyMapping` | `—` | PublicProperty, InternalNodePath, InternalProperty, Converter | Resolve(), Apply() | 属性映射 |
| `CxPortMapping` | `—` | PublicPortId, InternalNodePath, InternalPortId, Direction | Resolve(), Validate() | Port映射 |
| `CxPortVisualInfo` | `—` | RelativeX, RelativeY, Side, Orientation, AnchorType | Validate() | 可视连接点 |

## H. Command / State / Event / Action Contracts

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxCommandContract` | `—` | Id, Name, InputContract, ResultContract, RequiredPermissions, Timeout | Validate() | 稳定命令契约 |
| `CxCommandRequest` | `—` | CommandId, Target, Arguments, CorrelationId, RequestedAt | Validate() | 命令请求 |
| `CxCommandResult` | `—` | CommandId, Status, Result, Error, CompletedAt, CorrelationId | Success(), Failure() | 命令结果 |
| `CxStateDescriptor` | `—` | StateId, Name, ValueType, AllowedValues, IsObservable | Validate() | 状态描述 |
| `CxStateSnapshot` | `—` | StateId, Value, Timestamp, Sequence, Quality | — | 状态快照 |
| `CxEventContract` | `—` | Id, Name, PayloadContract, Delivery, Ordering, Durable | Validate() | 运行事件契约 |
| `CxRuntimeEvent` | `—` | ContractId, Source, Timestamp, Sequence, CorrelationId, CausationId, Payload | — | 运行事件 |
| `CxActionDescriptor` | `—` | Id, Name, ActionKind, CommandId, ScriptId, NavigationTarget, Parameters | Validate() | HMI/Studio Action |
| `CxActionExecutor` | `—` | CommandBus, ScriptHost, NavigationService | ExecuteAsync() | 执行Action |

## I. Script / Expression / Rule

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxExpressionDefinition` | `—` | Id, Text, ResultType, Dependencies, Language | Validate(), Compile() | 表达式 |
| `CxCompiledExpression` | `—` | Id, Delegate, Dependencies | Evaluate() | 编译表达式 |
| `CxExpressionContext` | `—` | Variables, Functions, Services, CurrentNode, RuntimeContext | ResolveName(), CallFunction() | 表达式上下文 |
| `CxConditionDefinition` | `—` | Id, Expression, ResultType, Dependencies | Validate(), Compile() | 条件 |
| `CxRuleDefinition` | `—` | Id, Name, Condition, Actions, Priority, Enabled | Validate(), Compile() | 规则 |
| `CxRuleEngine` | `—` | Rules | EvaluateAsync(), AddRule(), RemoveRule() | 规则运行 |
| `ICxScriptHost` | `interface` | Language | Compile(), ExecuteAsync(), ValidateSecurity() | 脚本宿主 |
| `CxScriptDefinition` | `—` | Id, Language, Code, Inputs, Outputs, Permissions | Validate() | 脚本定义 |
| `CxScriptSecurityContext` | `—` | AllowedApis, Permissions, TimeLimit, MemoryLimit | ValidateAccess() | 脚本安全 |

## J. Variable / Binding

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxVariableDefinition` | `—` | Id, Name, DataType, Unit, Min, Max, DefaultValue, Metadata, Persistence | Validate() | 变量定义 |
| `CxVariableRuntimeState<T>` | `—` | Value, PreviousValue, Quality, RefreshTimestamp, ChangeTimestamp, Source, Reason, Sequence, OnlineState | Refresh(), SetValue(), SetQuality(), MarkOffline(), MarkOnline() | 运行变量 |
| `CxVariableStore` | `—` | Definitions | Register(), Remove(), Find(), CreateRuntimeStore() | 变量定义仓 |
| `CxRuntimeVariableStore` | `—` | States | Get<T>(), Set<T>(), Subscribe(), Unsubscribe(), Snapshot(), Restore() | 运行变量仓 |
| `IVariableProvider` | `interface` | ProviderId | ReadAsync(), WriteAsync(), Subscribe(), GetState(), GetHealthAsync() | 外部变量Provider |
| `CxDataBinding` | `—` | SourceRef, TargetRef, Mode, Converter, FallbackValue, UpdateTrigger | Resolve(), Validate(), Apply() | Binding |
| `CxBindingEngine` | `—` | Bindings | Attach(), Detach(), Refresh(), PushSource(), PushTarget() | 绑定引擎 |

## K. Channel Foundation

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `IChannel` | `interface` | Id, Descriptor, State, Health | OpenAsync(), CloseAsync(), SendAsync(), ReceiveAsync(), RequestAsync(), Subscribe(), Unsubscribe() | 统一通道 |
| `IChannelFactory` | `interface` | — | Create(ChannelDescriptor), CanCreate() | 通道工厂 |
| `CxChannelDescriptor` | `—` | Id, Kind, Endpoint, BufferPolicy, ReconnectPolicy, SecurityProfile, Metadata | Validate() | 通道描述 |
| `CxChannelEndpoint` | `—` | Address, Port, Path, Parameters | Validate(), ToString() | 端点 |
| `CxChannelSession` | `—` | SessionId, ChannelId, OpenedAt, State, CorrelationContext | CloseAsync(), SendAsync(), RequestAsync() | 会话 |
| `CxChannelState` | `enum` | Closed, Opening, Open, Reconnecting, Faulted, Closing | — | 状态 |
| `CxChannelHealth` | `—` | State, LastSuccess, LastError, Latency, SendRate, ReceiveRate, ReconnectCount | IsHealthy() | 健康 |
| `CxMessageEnvelope` | `—` | MessageId, CorrelationId, CausationId, Timestamp, Headers, Payload | Clone(), GetHeader() | 消息包络 |
| `CxBufferPolicy` | `—` | Capacity, Backpressure, DropPolicy, BatchSize | Validate() | Buffer |
| `CxReconnectPolicy` | `—` | Enabled, MaxAttempts, Delay, Backoff | GetDelay(), ShouldRetry() | 重连 |
| `CxChannelDiagnostics` | `—` | Frames, Metrics, Errors | TraceSend(), TraceReceive(), TraceError(), Snapshot() | 通道诊断 |

## L. Protocol Foundation

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `IProtocol` | `interface` | Descriptor | CreateSession(), ValidateBinding() | 规约 |
| `IProtocolCodec` | `interface` | — | Encode(), Decode() | 编解码 |
| `IFrameParser` | `interface` | — | PushBytes(), TryReadFrame(), Reset() | 帧解析 |
| `CxProtocolDescriptor` | `—` | Id, Name, Version, Capabilities, MessageSchemas, SessionModel, ErrorMap | Validate() | 规约描述 |
| `CxProtocolFrame` | `—` | FrameId, RawBytes, Timestamp, Direction, Metadata | ValidateChecksum() | 帧 |
| `CxProtocolMessage` | `—` | MessageType, Fields, CorrelationId, Timestamp | GetField(), SetField() | 消息 |
| `CxProtocolSession` | `—` | SessionId, ProtocolId, ChannelSession, State, LastHeartbeat | StartAsync(), StopAsync(), SendAsync(), RequestAsync(), HandleFrameAsync() | 规约会话 |
| `CxProtocolBinding` | `—` | ProtocolId, ChannelRef, Addressing, Parameters | Resolve(), Validate() | Channel/Protocol绑定 |
| `CxProtocolError` | `—` | Code, Category, Message, RawCode, Retryable | — | 规约错误 |
| `IProtocolProvider` | `interface` | ProviderId | StartAsync(), StopAsync(), Encode(), Decode(), ExecuteCommandAsync(), GetHealthAsync() | 专业协议Provider |

## M. Data / Stream / Artifact

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxDataAspect` | `—` | Variables, Bindings, Contracts, Carriers, StreamPolicies | Validate() | Data聚合 |
| `CxDataContract` | `—` | ContractId, DataType, Schema, Unit, Nullable, TransferPolicy | IsCompatible(), Validate() | 数据契约 |
| `CxDataHandle` | `—` | HandleId, DataType, SizeBytes, Location, CarrierId, Lifetime, Version, Checksum | Acquire(), Release(), OpenRead(), Describe() | 大数据Handle |
| `ICxDataCarrier` | `interface` | CarrierId, Capabilities | LoadAsync(), UnloadAsync(), OpenReadAsync(), OpenWriteAsync(), CreateHandle(), GetHealthAsync() | 载体 |
| `CxDataCarrierDescriptor` | `—` | CarrierId, Kind, Location, Capacity, TransferPolicy | Validate() | 载体描述 |
| `CxStreamPolicy` | `—` | Capacity, Backpressure, DropPolicy, BatchSize, Ordering, NoDrop | Validate() | 流策略 |
| `CxStreamHandle` | `—` | StreamId, Contract, Policy, State | WriteAsync(), ReadAsync(), Complete(), Cancel() | 流Handle |
| `CxArtifactRef` | `—` | ArtifactId, Kind, Uri, Size, Checksum, Version, Metadata | OpenReadAsync(), VerifyAsync() | 结果/文件引用 |
| `CxArtifactStore` | `—` | RootUri | PutAsync(), GetAsync(), DeleteAsync(), QueryAsync() | Artifact存储 |
| `CxRuntimeDataScope` | `—` | Parent, Values, Handles | Get(), Set(), TryGet(), CreateChild(), Dispose() | 执行数据Scope |
| `ChangeJournal` | `—` | Entries | Append(), Query(), Replay(), Trim() | 变化流 |
| `IHistoryStore` | `interface` | — | AppendAsync(), QueryAsync(), CompactAsync() | Historian |
| `IStoreForwardService` | `interface` | QueueLength | EnqueueAsync(), FlushAsync(), RetryAsync(), DropExpiredAsync() | 断网补传 |

## N. Time Foundation

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `ICxClock` | `interface` | UtcNow, MonotonicNow | DelayAsync(), CreateTimer(), GetTimestamp() | 统一时钟 |
| `CxSystemClock` | `ICxClock` | — | DelayAsync(), CreateTimer(), GetTimestamp() | 真实时间 |
| `CxSimulationClock` | `ICxClock` | Speed, IsPaused, CurrentTime | Start(), Pause(), Resume(), Step(), SetSpeed(), DelayAsync() | 仿真时间 |
| `CxReplayClock` | `ICxClock` | ReplayTime, Speed | Seek(), Play(), Pause(), Step() | 回放时间 |
| `CxDeadline` | `—` | StartedAt, Timeout, ExpiresAt | IsExpired(), Remaining() | 截止时间 |
| `CxTimerService` | `—` | Clock, Timers | Schedule(), Cancel(), Reschedule() | Timer |

## O. Security / Safety / Audit

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxIdentity` | `—` | UserId, DisplayName, Roles, Claims | HasRole(), HasClaim() | 身份 |
| `CxPermission` | `—` | Id, Name, ResourcePattern, Actions | Matches() | 权限 |
| `CxPermissionPolicy` | `—` | Permissions, Rules | Authorize(), Explain() | 权限策略 |
| `CxSafetyMode` | `enum` | Normal, Manual, Auto, Maintenance, Service, Emergency | — | 运行安全模式 |
| `CxInterlockDefinition` | `—` | Id, Name, Condition, Severity, Latch, ResetPolicy | Validate(), Compile() | 互锁定义 |
| `CxInterlockGuard` | `—` | Definitions, States | Evaluate(), CanExecute(), Reset(), Bypass() | 互锁执行 |
| `CxSecurityContext` | `—` | Identity, Mode, Permissions, SessionId | Authorize(), Demand() | 安全上下文 |
| `CxAuditRecord` | `—` | Timestamp, Actor, Action, Target, Before, After, CorrelationId, Result | — | 审计记录 |
| `CxAuditService` | `—` | Store | Write(), Query(), Export() | 审计 |

## P. Drawing / Presentation / Diagram

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxCanvasHost` | `—` | Viewport, Layers, Selection, Overlay, SnapSettings | Render(), HitTest(), ZoomToFit(), Invalidate() | 画布 |
| `CxViewport` | `—` | Scale, Offset, Rotation, Bounds | WorldToView(), ViewToWorld(), Zoom(), Pan() | 视口 |
| `CxGeometry` | `—` | Bounds | Transform(), HitTest(), Clone() | 几何基类 |
| `CxTransform` | `—` | TranslateX, TranslateY, ScaleX, ScaleY, Rotation, Pivot | Compose(), Invert(), Apply() | 变换 |
| `CxLayerDefinition` | `—` | Id, Name, ZIndex, Visible, Locked | Validate() | 图层 |
| `CxSnapSettings` | `—` | GridEnabled, GridSize, SnapToObjects, SnapToGuides, Tolerance | — | 吸附 |
| `CxSelectionAdorner` | `—` | Selection | Render(), BeginDrag(), UpdateDrag(), EndDrag() | 选中装饰 |
| `CxOverlay` | `—` | Items | Add(), Remove(), Clear(), Render() | Overlay |
| `CxSvgImporter` | `—` | Options | Import(), ImportElement() | SVG导入 |
| `CxSvgExporter` | `—` | Options | Export(), ExportSelection() | SVG导出 |
| `CxDiagramCanvas` | `—` | Nodes, Edges, Router, Layout, Minimap, RuntimeAdornment | Render(), AddNodePresenter(), AddEdgePresenter(), AutoLayout(), RouteEdges() | 编排图 |
| `CxNodePresenter` | `—` | NodeAddress, Bounds, Ports, StateAdornment | Render(), HitTest(), UpdateRuntimeState() | Node视觉 |
| `CxPortPresenter` | `—` | PortAddress, Anchor, Direction, Kind | Render(), HitTest() | Port视觉 |
| `CxEdgePresenter` | `—` | ConnectionId, Route, Style, RuntimeState | Render(), HitTest(), UpdateRuntimeState() | Edge视觉 |
| `CxEdgeRouter` | `—` | Strategy, Obstacles | Route(), Reroute() | 连线路由 |
| `CxAutoLayoutService` | `—` | Algorithm, Options | Layout() | 自动布局 |
| `CxMinimapModel` | `—` | ViewportRect, WorldBounds | Update(), NavigateTo() | 小地图 |

## Q. HMI Presentation Definitions

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxHmiBindingDefinition` | `—` | TargetNode, TargetProperty, SourceRef, Mode, Converter, UpdateTrigger | Resolve(), Validate() | HMI绑定 |
| `CxAnimationDefinition` | `—` | Id, TargetProperty, Trigger, From, To, Duration, Easing, Repeat | Validate(), Compile() | 动画 |
| `CxHmiStateDefinition` | `—` | Id, Name, Condition, PropertyOverrides, Actions | Validate() | 视觉状态 |
| `CxHmiActionDefinition` | `—` | Id, Trigger, ActionDescriptor, Parameters | Validate() | HMI动作 |
| `CxTrendDefinition` | `—` | Series, TimeRange, RefreshInterval, HistorySource | Validate() | 趋势 |
| `CxDashboardDefinition` | `—` | Widgets, Layout, RefreshPolicy | Validate() | Dashboard |
| `CxViewerDefinition` | `—` | ViewerType, DataContract, InteractionMode, Options | Validate() | 专业Viewer定义 |

## R. Logic / Execution Definition / Invocation

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxLogicAspect` | `—` | ExecutionModel, EntryPort, ExitPorts, Transitions, Conditions, Loops, Waits, Parallels, States, Triggers, Invocations | Validate() | 逻辑聚合 |
| `CxExecutionModelKind` | `enum` | None, ControlFlow, DataFlow, Workflow, StateMachine, ReactiveEvent | — | 执行模型 |
| `CxExecutionBinding` | `—` | OperationId, ModelKind, Placement, Timeout, Cancelable, ExecutionPolicyRef | Validate() | Node→Operation绑定 |
| `CxOperationDescriptor` | `—` | OperationId, Name, Category, InputContracts, OutputContracts, RequiredResources, SupportedPlacements, HandlerType, Version | Validate(), IsCompatible() | 功能块描述 |
| `CxOperationRegistry` | `—` | Descriptors, Handlers | Register(), Unregister(), FindDescriptor(), ResolveHandler() | Operation注册 |
| `ICxOperationHandler` | `interface` | OperationId | ExecuteAsync() | 功能代码入口 |
| `CxOperationContext` | `—` | ExecutionInstanceId, NodeAddress, Inputs, Outputs, Properties, DataScope, ResourceScope, TraceContext, EventBus, CancellationToken | GetInput<T>(), SetOutput(), GetProperty<T>(), GetResource<T>(), RaiseEvent(), ReportProgress() | Handler上下文 |
| `CxOperationResult` | `—` | Status, Outputs, Error, Diagnostics | Success(), Failure(), Cancelled() | Operation结果 |
| `CxTransitionDefinition` | `—` | Id, SourcePort, TargetPort, ConditionId, Priority | Validate() | 流程迁移 |
| `CxWaitDefinition` | `—` | Id, Mode, Timeout, EventContract, ConditionId | Validate() | Wait |
| `CxLoopDefinition` | `—` | Id, ConditionId, MaxIterations, Timeout, BodyEntry, ContinueTarget, BreakTarget | Validate() | Loop |
| `CxParallelDefinition` | `—` | Id, BranchEntries, JoinPolicy, Timeout | Validate() | Parallel/Join |
| `CxTriggerDefinition` | `—` | Id, TriggerType, SourceRef, EventContract, Debounce, Throttle | Validate() | Trigger |
| `CxStateDefinition` | `—` | Id, Name, EntryActions, ExitActions, IsInitial, IsFinal | Validate() | State |
| `CxStateTransitionDefinition` | `—` | Id, FromState, ToState, TriggerId, GuardConditionId, Actions | Validate() | 状态迁移 |
| `CxInvocationSpec` | `—` | Id, TargetRef, Mode, InputMappings, OutputMappings, Placement, VersionBinding, Policy | Validate() | 跨Content/Workbench调用 |
| `CxInvocationResult` | `—` | InvocationId, Status, Outputs, Error, Duration | — | 调用结果 |
| `CxJobHandle` | `—` | JobId, State, Progress, ResultRef, Fault | CancelAsync(), WaitAsync(), GetStatusAsync() | 长任务 |
| `CxMultiInstanceDefinition` | `—` | Id, SourceCollection, BodyRef, MaxConcurrency, JoinPolicy | Validate() | 多实例 |

## S. Resource Runtime / Provider

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxResourceRequirement` | `—` | ResourceType, Capability, Quantity, Exclusive, Affinity, Optional | Validate() | 资源要求 |
| `ICxResourceProvider` | `interface` | ProviderId | OpenAsync(), CloseAsync(), AcquireAsync(), ReleaseAsync(), GetHealthAsync() | 资源Provider |
| `CxResourceScope` | `—` | Parent, Leases, Lifetime | AcquireAsync(), Release(), CreateChild(), DisposeAsync() | 运行资源Scope |
| `CxLeaseHandle` | `—` | LeaseId, ResourceId, OwnerExecutionId, AcquiredAt, ExpiresAt | RenewAsync(), ReleaseAsync() | Lease |
| `CxPlacement` | `—` | Kind, TargetId, Process, Machine, WorkerPool, Tags | Matches(), Validate() | 运行位置 |
| `CxPlacementResolver` | `—` | Targets | Resolve(), CanPlace(), Explain() | Placement |
| `CxRuntimeProfile` | `—` | Id, Name, Placements, ResourceBindings, Environment, Policies | Validate() | 运行Profile |
| `ICxRuntimeAdapter` | `interface` | AdapterId, SupportedOperations | InitializeAsync(), ExecuteAsync(), ShutdownAsync(), GetHealthAsync() | 专业Runtime适配 |
| `CxRuntimeAdapterRegistry` | `—` | Adapters | Register(), Unregister(), ResolveForOperation(), ResolveForPlacement() | Adapter注册 |

## T. Validate / Resolve / Compile

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxCompileOptions` | `—` | Strict, TargetRuntimeProfile, ExpandSymbols, OptimizeVisual, EnableIncremental, DiagnosticsLevel | — | 编译选项 |
| `CxTypeResolver` | `—` | Descriptors | ResolveType(), ResolveContract(), CheckAssignable() | 类型解析 |
| `CxValidator` | `—` | Policies, TypeSystem | ValidateProject(), ValidateNode(), ValidateTree(), ValidateReference(), ValidatePort(), ValidateConnection(), ValidateLogic(), ValidateResource(), ValidatePlacement(), ValidateSecurity(), ValidatePersistence() | 总验证 |
| `CxResolvedProject` | `—` | ProjectId, Nodes, SymbolInstances, Connections, ResourceBindings, Placements, Variables, Diagnostics | FindNode(), FindInstance(), FindConnection() | Resolve中间态 |
| `CxResolvedNode` | `—` | SourceNodeId, ResolvedAddress, ResolvedProperties, ResolvedPorts, ResolvedReferences, ExecutionBinding, EffectiveCapabilities | GetProperty(), FindPort() | 解析节点 |
| `CxResolvedSymbolInstance` | `—` | InstanceAddress, DefinitionId, Version, ResolvedChildren, ResolvedProperties, ResolvedPorts, InternalMappings | FindInternalNode() | Symbol实例 |
| `CxResolvedConnection` | `—` | SourcePort, TargetPort, Contract, Condition, Priority | Validate() | 解析连接 |
| `CxResolvedResourceBinding` | `—` | Requirement, ResourceId, ProviderId, Placement | Validate() | 资源绑定 |
| `CxNodeResolver` | `—` | ReferenceResolver, PortResolver, PlacementResolver, TypeResolver | ResolveProject(), ResolveNode(), ResolveSymbolInstance(), ResolveConnections(), ResolveResources(), ResolveBindings() | Resolve |
| `CxNodeCompiler` | `—` | Validator, Resolver, AspectCompilers, OperationCompilers | Compile(ProjectModel), Compile(PageModel), CompileNode(), CompileResolvedProject() | 总编译器 |
| `CxCompileResult` | `—` | Validation, ResolvedProject, RuntimePlan, Diagnostics | ThrowIfFailed() | 编译结果 |
| `ICxAspectCompiler` | `interface` | AspectId | Compile() | Aspect扩展 |
| `ICxOperationCompiler` | `interface` | OperationId | Compile() | Operation扩展 |
| `CxCompileCache` | `—` | Entries | TryGet(), Put(), Invalidate(), Clear() | 增量编译缓存 |

## U. Runtime Plan

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxRuntimePlan` | `—` | ProjectId, PagePlans, ContentPlans, ExecutionPlans, VisualPlan, DataPlan, ConnectionPlan, ResourcePlan, DeploymentPlan, Version, Hash | Validate(), CreateRuntime() | 顶层计划 |
| `CxPagePlan` | `—` | PageId, RootContentPlanId, VisualProjection, Bindings | Validate() | 页面计划 |
| `CxContentPlan` | `—` | ContentId, ScopeId, ChildPlans, PublicPorts, Connections, ExecutionPlanIds | Validate() | Content计划 |
| `CxExecutionPlan` | `—` | PlanId, EntryStep, Steps, Transitions, Policy, Placement, RequiredResources | Validate() | 执行计划 |
| `CxVisualPlan` | `—` | RenderItems, Transforms, Bindings, HitTestIndex | Optimize(), Validate() | 视觉计划 |
| `CxLogicPlan` | `—` | Entry, Steps, Transitions, Loops, Waits, Parallels, StateMachines, Invocations | Validate() | 逻辑计划 |
| `CxDataPlan` | `—` | DataScopes, Bindings, Contracts, Streams, Carriers, Variables | Validate() | 数据计划 |
| `CxConnectionPlan` | `—` | RuntimeConnections | Validate() | 连接计划 |
| `CxResourcePlan` | `—` | Requirements, Bindings, LeasePolicies, Placements | Validate() | 资源计划 |
| `CxDeploymentPlan` | `—` | Targets, Artifacts, Adapters, VersionBindings, Packages | Validate() | 部署计划 |

## V. Runtime Core

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxProjectRuntimeHost` | `—` | Plan, Kernel, Services, ResourceRoot, VariableStore, EventBus, Trace, State, Clock, SecurityContext | InitializeAsync(), StartAsync(), StopAsync(), PauseAsync(), ResumeAsync(), CreateExecution(), DisposeAsync() | 工程Runtime根 |
| `CxPageRuntimeProjection` | `—` | PagePlan, RuntimeBindings, ViewState | Attach(), Detach(), Refresh(), GetNodeRuntimeState() | Page投影 |
| `CxContentRuntimeInstance` | `—` | ContentPlan, ExecutionScope, DataScope, ResourceScope, State, Progress, Fault | StartAsync(), StopAsync(), PauseAsync(), ResumeAsync(), CancelAsync() | Content运行Scope |
| `CxSymbolRuntimeInstance` | `—` | InstanceAddress, DefinitionId, Properties, Ports, State | Bind(), UpdateState(), Dispose() | Symbol运行实例 |
| `CxElementRuntimeHandle` | `—` | NodeAddress, OperationHandler, ProviderHandles, State | InitializeAsync(), ExecuteAsync(), DisposeAsync() | Element运行句柄 |
| `ExecutionRuntimeKernel` | `—` | Schedulers, Adapters, Instances, Clock | StartAsync(), StopAsync(), CreateInstance(), Pause(), Resume(), Cancel(), StepAsync() | Runtime Kernel |
| `ExecutionInstance` | `—` | Id, PlanId, ParentInstanceId, State, StartedAt, CompletedAt, Context, Scope, Result | StartAsync(), CancelAsync(), Complete(), Fault(), Pause(), Resume() | 一次执行 |
| `ExecutionContext` | `—` | InstanceId, DataScope, ResourceScope, Variables, EventBus, Trace, Clock, SecurityContext, CancellationToken, RunMode | CreateChild(), GetData(), GetResource(), EmitEvent(), InvokeAsync() | 执行上下文 |
| `ExecutionScope` | `—` | Parent, Children, Context, Lifetime | CreateChild(), CloseAsync(), CancelChildrenAsync() | 执行Scope |
| `ExecutionScheduler` | `—` | ReadyQueue, RunningSteps, Clock | Schedule(), RunNextAsync(), Suspend(), Resume(), Cancel(), StepAsync() | 调度 |
| `ExecutionStep` | `—` | StepId, NodeAddress, OperationId, Inputs, Outputs, Policy, Placement, State | ExecuteAsync() | 运行步骤 |
| `RuntimeTransition` | `—` | SourceStep, TargetStep, Priority | CanTransit() | 迁移 |
| `ConditionalTransition` | `RuntimeTransition` | Condition | EvaluateAsync() | 条件迁移 |
| `LoopRuntime` | `—` | Definition, Iteration, StartedAt, State | Enter(), NextAsync(), Break(), Continue(), Exit() | 循环 |
| `WaitRuntime` | `—` | Definition, StartedAt, State | WaitAsync(), Cancel() | 等待 |
| `ParallelRuntime` | `—` | Branches, JoinPolicy, State | StartAllAsync(), JoinAsync(), CancelRemaining() | 并行 |
| `CxRuntimeConnection` | `—` | Source, Target, Contract, State, Metrics | TransferAsync(), Enable(), Disable(), DrainAsync() | 运行连接 |
| `CxRuntimeEventBus` | `—` | Subscriptions | PublishAsync(), Subscribe(), Unsubscribe(), ReplayFrom() | 运行事件总线 |
| `CxRuntimeState` | `—` | Status, Progress, Message, StartedAt, UpdatedAt, Fault | SetStatus(), SetProgress(), SetFault() | 通用状态 |

## W. Policy / Fault / Recovery / Diagnostics / Debug

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxExecutionPolicy` | `—` | Retry, Timeout, Cancellation, Durability, Idempotency, FailureAction, Compensation | Validate() | 策略聚合 |
| `CxRetryPolicy` | `—` | MaxAttempts, Delay, Backoff, RetryOn | ShouldRetry(), GetDelay() | 重试 |
| `CxTimeoutPolicy` | `—` | Timeout, OnTimeout | Validate() | 超时 |
| `CxCancellationPolicy` | `—` | PropagateToChildren, GracePeriod, ForceAfter | Validate() | 取消 |
| `CxDurabilityPolicy` | `—` | Enabled, CheckpointMode, PersistenceKind, RestorePolicy | Validate() | 持久执行 |
| `CxIdempotencyPolicy` | `—` | Enabled, KeyExpression, Scope, Window | BuildKey(), Validate() | 幂等 |
| `CxCompensationDefinition` | `—` | OperationRef, InputMappings, TriggerOn | Validate() | 补偿 |
| `CxRuntimeSnapshot` | `—` | RuntimeVersion, ProjectVersion, Instances, Variables, Checkpoints, ResourceRebindInfo, Timestamp | Serialize(), Validate() | 快照 |
| `CxCheckpointManager` | `—` | Store | SaveAsync(), LoadAsync(), DeleteAsync(), ListAsync() | Checkpoint |
| `CxFault` | `—` | Code, Severity, Source, Message, ExceptionInfo, Timestamp, CorrelationId | — | 故障 |
| `CxFaultManager` | `—` | ActiveFaults | Report(), Acknowledge(), Clear(), Escalate(), Query() | Fault管理 |
| `CxRecoveryManager` | `—` | Policies | RecoverAsync(), RollbackAsync(), ResumeAsync(), RebindResourcesAsync() | 恢复 |
| `CxTraceContext` | `—` | TraceId, CorrelationId, CausationId, ExecutionInstanceId, Source | CreateChild() | Trace上下文 |
| `CxRuntimeTrace` | `—` | Records | TraceStepStart(), TraceStepEnd(), TraceEvent(), TraceData(), TraceFault(), GetCallStack(), Export() | 运行Trace |
| `CxDiagnosticRecord` | `—` | Timestamp, Category, Severity, Source, Message, Data | — | 诊断 |
| `CxMetricsCollector` | `—` | Counters, Gauges, Histograms | Increment(), SetGauge(), RecordDuration(), Snapshot() | 指标 |
| `CxHealthState` | `—` | Status, Components, LastUpdated | IsHealthy(), GetDegradedComponents() | 健康 |
| `CxDebugSession` | `—` | RuntimeHost, Breakpoints, Watches, State | Attach(), Detach(), Continue(), Pause(), StepInto(), StepOver(), StepOut(), EvaluateWatch() | 调试 |
| `CxBreakpoint` | `—` | Id, NodeAddress, Condition, HitCount, Enabled | ShouldBreak() | 断点 |
| `CxWatchExpression` | `—` | Expression, LastValue, Error | Evaluate() | Watch |

## X. Persistence / Version / Package / Release / ImportExport

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxProjectSerializer` | `—` | NodeSerializer, SchemaVersion | Save(), Load(), SavePackage(), LoadPackage() | 工程持久化 |
| `CxNodeSerializer` | `—` | TypeRegistry | Serialize(), Deserialize(), WriteNode(), ReadNode() | Node序列化 |
| `CxSchemaVersion` | `—` | Major, Minor, Patch | CompareTo(), Parse(), ToString() | Schema版本 |
| `CxMigration` | `—` | FromVersion, ToVersion, Id | CanApply(), Apply() | 迁移步骤 |
| `CxMigrationManager` | `—` | Migrations | Register(), CanMigrate(), Migrate(), BuildPath() | 迁移管理 |
| `CxUnknownNode` | `CxNode` | OriginalTypeId, RawPayload, OriginalMetadata | TryRehydrate() | 未知插件Node |
| `CxRawPayload` | `—` | Format, Data, Checksum | ValidateChecksum() | 原始载荷 |
| `CxProjectManifest` | `—` | ProjectId, ProjectVersion, SchemaVersion, RequiredPlugins, RequiredCapabilities, Resources, RuntimeTargets | Validate() | 工程清单 |
| `CxPackageDescriptor` | `—` | PackageId, Version, Manifest, Artifacts, Dependencies, Checksum | Validate(), Verify() | 发布包 |
| `CxPackageBuilder` | `—` | — | Build(), AddArtifact(), Sign(), Validate() | 打包 |
| `CxDeployPlan` | `—` | Targets, Package, Steps, RollbackPlan, CompatibilityChecks | Validate() | 部署计划 |
| `CxDeploymentService` | `—` | Targets | DeployAsync(), RollbackAsync(), GetStatusAsync() | 部署 |
| `CxCompatibilityChecker` | `—` | — | CheckProject(), CheckPackage(), CheckRuntime() | 兼容性 |
| `ICxImporter` | `interface` | Format | CanImport(), ImportAsync() | 导入 |
| `ICxExporter` | `interface` | Format | CanExport(), ExportAsync() | 导出 |
| `CxImportExportRegistry` | `—` | Importers, Exporters | Register(), FindImporter(), FindExporter() | 导入导出注册 |

## Y. Template / Notification / History / Baseline

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxTemplateDescriptor` | `—` | Id, Name, Category, SourceRef, Parameters, Version | Validate() | 模板 |
| `CxTemplateService` | `—` | Templates | CreateFromSelection(), Instantiate(), UpdateInstance(), Find() | 模板复用 |
| `CxPreset` | `—` | Id, Name, TargetType, Values | Apply(), Capture() | 预设 |
| `CxWizardDefinition` | `—` | Id, Steps, OutputTemplate | Validate() | 向导 |
| `CxNotification` | `—` | Id, Severity, Title, Message, Source, Timestamp, Actions | — | 通知 |
| `CxNotificationService` | `—` | Active, Subscribers | Publish(), Subscribe(), Dismiss(), Clear() | 通知服务 |
| `CxMessageTopic` | `—` | Name, Contract, Durable | Validate() | 消息Topic |
| `CxMessageBus` | `—` | Topics | PublishAsync(), Subscribe(), Unsubscribe() | 平台消息 |
| `CxSnapshotStore` | `—` | — | SaveAsync(), LoadAsync(), ListAsync(), DeleteAsync() | 快照存储 |
| `CxReplayStore` | `—` | — | AppendAsync(), OpenReplayAsync(), QueryAsync() | 回放存储 |
| `CxBaselineDefinition` | `—` | Id, Name, CreatedAt, ProjectVersion, ArtifactRefs, Metadata | Validate() | Baseline |
| `CxCompareResult` | `—` | Added, Removed, Changed, Metrics | HasChanges() | 比较结果 |
| `CxCompareService` | `—` | — | CompareProjects(), CompareNodes(), CompareResults(), CompareBaselines() | 对比 |

## Z. Simulation Workbench Minimum Classes

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxSimulationProfile` | `—` | Id, Name, Mode, ClockProfile, DeviceBindings, FaultProfile, ReplayRef | Validate() | 仿真Profile |
| `CxVirtualDeviceDefinition` | `—` | Id, DeviceType, StateModel, Capabilities, ResponseModel | Validate(), CreateRuntime() | 虚拟设备 |
| `CxVirtualChannelDefinition` | `—` | Id, ChannelKind, Delay, Jitter, DropRate, DuplicateRate, ReorderRate | Validate() | 虚拟通道 |
| `CxProtocolFaultProfile` | `—` | CorruptRate, TimeoutRate, ErrorCodes, SequenceRules | Validate() | 规约故障 |
| `CxScenarioDefinition` | `—` | Id, Name, InitialState, Steps, Assertions, Duration | Validate() | 场景 |
| `CxScenarioStep` | `—` | At, Action, Target, Parameters | Validate() | 场景步骤 |
| `CxFaultInjectionDefinition` | `—` | Id, TargetRef, FaultType, Start, Duration, Parameters | Validate() | 故障注入 |
| `CxReplayDefinition` | `—` | SourceRef, StartTime, EndTime, Speed, Mapping | Validate() | 回放 |
| `CxTwinDefinition` | `—` | Id, PhysicalRef, ModelRef, SyncPolicy | Validate() | Twin |
| `CxSimulationRuntime` | `—` | Profile, Clock, VirtualDevices, Replay, FaultEngine | StartAsync(), StopAsync(), Pause(), Step(), InjectFault() | Simulation Runtime |

## AA. Automation Workbench Minimum Classes

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxDeviceDefinition` | `—` | Id, Name, DeviceType, ProviderId, ProtocolBinding, Capabilities, Parameters | Validate() | 设备 |
| `CxIoPointDefinition` | `—` | Id, Name, Address, DataType, Access, Scale, VariableRef | Validate() | IO/点表 |
| `CxAlarmDefinition` | `—` | Id, Name, SourceRef, Condition, Severity, Delay, Latch, AckRequired | Validate(), Compile() | 报警 |
| `CxRecipeDefinition` | `—` | Id, Name, Version, Parameters, Steps | Validate() | 配方 |
| `CxAxisDefinition` | `—` | Id, Name, AxisNo, Limits, Units, HomeProfile, MotionProfile, ResourceRef | Validate() | 轴 |
| `CxMotionPointDefinition` | `—` | Id, Name, Position, Velocity, Accel, Decel | Validate() | 点位 |
| `CxRobotDefinition` | `—` | Id, Name, ProviderId, Frames, Tools, Zones, ResourceRef | Validate() | 机器人 |
| `CxRobotPoseDefinition` | `—` | Id, Name, Position, Orientation, FrameRef, ToolRef | Validate() | 机器人姿态 |
| `CxAcquisitionDeviceDefinition` | `—` | Id, DeviceType, ProviderId, TriggerProfile, DataContract, ResourceRef | Validate() | Camera/Detector |
| `CxTriggerProfile` | `—` | Mode, SourceRef, Edge, Delay, Debounce | Validate() | 触发 |
| `CxXRaySourceDefinition` | `—` | Id, ProviderId, VoltageRange, CurrentRange, ExposureProfiles, Interlocks, ResourceRef | Validate() | XRay |
| `CxDaqTaskDefinition` | `—` | Id, Channels, SampleRate, Trigger, Duration, BufferPolicy | Validate() | DAQ |
| `CxMeasurementDefinition` | `—` | Id, Inputs, AlgorithmRef, Unit, Limits, ResultContract | Validate() | 测量 |
| `CxIoTDeviceDefinition` | `—` | Id, ProductId, ProviderId, Points, TwinProfile, StoreForwardPolicy | Validate() | IoT设备 |

## AB. Vision Workbench Minimum Classes

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxImageDescriptor` | `—` | Width, Height, PixelFormat, Stride, CalibrationRef, Metadata | Validate() | 图像描述 |
| `CxRoiDefinition` | `—` | Id, RoiType, Geometry, CoordinateSystem, Enabled, Metadata | Validate(), Transform() | ROI |
| `CxVisionCalibrationDefinition` | `—` | Id, CalibrationType, Parameters, Result, Version | Validate() | 视觉标定 |
| `CxVisionOperationDefinition` | `—` | OperationId, Parameters, InputPorts, OutputPorts, ExecutionBinding | Validate() | 视觉功能块定义 |
| `CxVisionPipelineDefinition` | `ContentDefinition` | InputContracts, OutputContracts, StreamPolicy | Validate() | Vision组合Content |
| `CxVisionResult` | `—` | ResultId, Status, Defects, Measurements, Artifacts, TraceId | Validate() | 视觉结果 |
| `CxDefectDefinition` | `—` | Id, Type, Severity, Thresholds, Metadata | Validate() | 缺陷定义 |
| `CxDefectResult` | `—` | DefinitionId, Found, Score, Region, Features, Metadata | — | 缺陷结果 |

## AC. CT3D Workbench Minimum Classes

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxProjectionSeriesDescriptor` | `—` | Count, Width, Height, PixelFormat, Angles, CarrierRef, Completeness | Validate() | 投影序列 |
| `CxCtGeometryDefinition` | `—` | FOD, FDD, DetectorGeometry, SourceGeometry, Trajectory, CoordinateSystem | Validate() | CT几何 |
| `CxCtCalibrationDefinition` | `—` | Id, GeometryRef, DetectorCorrection, MechanicalCorrection, Result, Version | Validate() | CT标定 |
| `CxReconstructionDefinition` | `—` | AlgorithmId, GeometryRef, VolumeSize, VoxelSpacing, Filter, Parameters, Placement | Validate() | 重建定义 |
| `CxVolumeDescriptor` | `—` | Dimensions, VoxelType, Spacing, Origin, Orientation, DataHandle, Metadata | Validate() | Volume |
| `CxMprViewDefinition` | `—` | Plane, SliceIndex, WindowMin, WindowMax, PseudoColor, Crosshair | Validate() | MPR |
| `CxVolumeRenderDefinition` | `—` | TransferFunction, BlendMode, Clipping, Quality | Validate() | 体绘 |
| `CxCtAnalysisDefinition` | `—` | OperationId, RoiRef, Parameters, OutputContract | Validate() | 3D分析 |
| `CxCtResult` | `—` | Status, VolumeRef, Measurements, Defects, Artifacts, TraceId | Validate() | CT结果 |

## AD. Inspection Workbench Minimum Classes

| 类 | 继承 / 接口 | 属性 | 方法 | 职责 |
|---|---|---|---|---|
| `CxProductDefinition` | `—` | Id, Name, ProductCode, Version, Attributes, InspectionPlanRef | Validate() | 产品 |
| `CxStationDefinition` | `—` | Id, Name, Capabilities, Resources, Points, RuntimeTarget | Validate() | 工位 |
| `CxInspectionPlanDefinition` | `—` | Id, Name, Version, ProductRef, StationRefs, Points, FlowRef, Policies | Validate() | 检测计划 |
| `CxInspectionPointDefinition` | `—` | Id, Name, Position, RequiredCapabilities, OperationRefs, JudgeRef | Validate() | 检测点 |
| `CxJudgeDefinition` | `—` | Id, Inputs, Rule, ResultMapping, SeverityPolicy | Validate(), Compile() | 判定 |
| `CxRecheckDefinition` | `—` | Id, TriggerCondition, MaxCount, Strategy, TargetPointRefs | Validate() | 复测 |
| `CxReviewDefinition` | `—` | Id, EntryCondition, ReviewerPolicy, Timeout, ResultMapping | Validate() | 人工复判 |
| `CxInspectionResult` | `—` | ResultId, ProductId, StationId, Status, PointResults, Defects, Measurements, TraceId, StartedAt, CompletedAt | Validate() | 产品结果 |
| `CxMesBindingDefinition` | `—` | EndpointRef, RequestMapping, ResultMapping, IdempotencyPolicy, RetryPolicy | Validate() | MES |
| `CxInspectionTrace` | `—` | ProductId, Timeline, ExecutionTraceRef, Artifacts | BuildSummary() | 产品Trace |

---

**总计：349 个类 / 接口 / 枚举级契约。**

### 说明

1. `CxNode` 是 Definition DOM 根；不直接运行专业功能。
2. `Project/Page/Content/Symbol/Group/Element` 的 Ownership、Reference、Connection 三种关系分离。
3. 具体专业算法/设备 SDK 不进入 Core，通过 `ICxOperationHandler / ICxResourceProvider / ICxRuntimeAdapter / IChannel / IProtocolProvider` 接入。
4. 该总表用于 Codex 建类、查漏、Code Review、API 冻结；若新增公共类，必须说明它解决哪个 5 大系统/6 Workbench/8 Case 的缺口。

---

# 27. Codex 每次提交的固定输出格式

Codex 每完成一个阶段或一个子模块，必须输出：

```text
1. 本次目标
2. 修改文件清单
3. 新增/修改类
4. 与现有 V6.6 的复用关系
5. 是否改变序列化格式
6. 是否新增依赖
7. 编译结果
8. 单元测试结果
9. 回归测试结果
10. 未完成项
11. 风险
12. 下一步
```

如果出现编译失败，禁止继续堆功能，先恢复编译。

---

# 28. 最终验收报告模板

```text
CxVisual Unified Platform Acceptance

A. Build
[ ] Solution Build PASS
[ ] Warning baseline reviewed

B. Compatibility
[ ] V6.6 project load PASS
[ ] V6.6 roundtrip PASS
[ ] Unknown plugin payload PASS

C. Core
[ ] CxNode DOM
[ ] Ownership
[ ] Reference
[ ] Port/Connection
[ ] Property/Variable
[ ] Editing

D. Infrastructure
[ ] Channel
[ ] Protocol
[ ] Resource
[ ] DataHandle/Stream
[ ] Time
[ ] Security/Audit

E. Compiler
[ ] Validate
[ ] Resolve
[ ] RuntimePlan

F. Runtime
[ ] Start/Stop
[ ] Pause/Resume
[ ] Cancel
[ ] Decision/Wait/Loop/Parallel
[ ] Invocation
[ ] Fault/Recovery
[ ] Snapshot/Restore
[ ] Trace/Debug

G. Workbenches
[ ] Automation
[ ] Simulation
[ ] HMI
[ ] Vision
[ ] CT3D
[ ] Inspection

H. Historical Systems
[ ] Vision
[ ] CT
[ ] Motion
[ ] SCADA
[ ] IoT

I. 8 Cases
[ ] PLC+HMI
[ ] Robot Cell
[ ] X-Ray 2D
[ ] Industrial CT
[ ] Test/Measurement
[ ] SCADA
[ ] IoT Edge
[ ] Multi-station Inspection

J. Release
[ ] Package
[ ] DeployPlan
[ ] Compatibility
[ ] Rollback
```

---

# 29. 最终架构结论

CxVisual 的真正底座不是“模仿 XML”，而是：

```text
CxNode
=
成熟 DOM：
Tree / Parent / Child / Sibling / Query / Mutation / Clone
+
Web DOM：
Event / Selector / Walker / Observer
+
工业 Definition：
Property / Variable / Reference / Port / Connection / Aspect
+
工业基础：
Channel / Protocol / Command / State / Event / Time / Security
+
大数据与资源：
DataHandle / Stream / Resource / Placement
+
编排：
Operation / Decision / Wait / Loop / Parallel / State / Invocation
+
Compiler：
Validate / Resolve / RuntimePlan
+
Runtime：
Instance / Context / Scope / Scheduler / Resource / Trace / Recovery
+
产品化：
Editing / Drawing / Diagram / Plugin / Package / Deploy
```

`Project / Page / Content / Symbol / Group / Element` 是 CxVisual 的统一 Definition 语言。  
`CxResolvedProject` 是链接后的中间态。  
`CxRuntimePlan` 是类似“编译产物”的计划。  
`CxProjectRuntimeHost + ExecutionRuntimeKernel` 是运行环境。  
你过去做过的 C#/C++/Go 代码是专业能力库，通过稳定契约接入。

**Codex 最终交付目标不是“有这些类”，而是这些类共同形成可运行、可观察、可恢复、可扩展的平台闭环。**
