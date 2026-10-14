# CxVisual 统一对象模型与多维能力架构设计 V1.1

> 人类理解版 + Codex 实施基线  
> 日期：2026-09-21  
> 状态：实例验证收敛版；在 V1.0 基础上补充 Symbol / Content 具体落地、Aspect 按需装配与 Runtime 实例边界

---

## 0. 一页结论

本轮讨论经过 Automation、Vision、CT3D、HMI、Inspection、Simulation 六类 Workbench，以及工业 X-Ray 检测、CT 重建、多机器人、工业 IoT、数字孪生、ISA-88 制药、半导体 Cluster Tool、电力 ICS 等案例反向压力测试后，最终收敛为：

**一套主对象组织结构 + 四个正交能力面 + 一个运行态投影。**

主对象组织结构：

```text
Project
└─ Workbench
   └─ Page
      └─ RootContent
         ├─ Owned Content
         ├─ ContentRef  ──► Shared Content
         ├─ SymbolRef   ──► Symbol
         ├─ Group
         └─ Element

Symbol
└─ RootGroup
   ├─ Group
   └─ Element
```

四个正交能力面：

```text
Visual Aspect          怎么显示
Logic Aspect           怎么工作
Data Aspect            数据是什么、如何流转与承载
Collaboration Aspect   怎么引用、调用、协作
```

运行态：

```text
Runtime Projection
= 在同一“运行实例身份”上叠加当前逻辑步骤、数据状态、承载状态、能力调用、资源状态、Trace。

注意：`Content / Symbol` 是定义；真正的 Runtime Projection 属于 `ContentInstance / SymbolInstance / LogicExecutionInstance`，禁止把实例状态写回定义。
```

关键决策：

1. 全平台不再维护“Visual Tree + Graph Tree + Runtime Tree”三套面向用户的设计态组织结构。
2. `Content` 是功能/业务/区域组合核心；`Symbol` 是基础可复用功能视觉组件；`Group` 是纯视觉组合；`Element` 是视觉叶子。
3. `Symbol` 不允许直接包含 `Symbol`；大型复合组件用 `Content + SymbolRef`，需要复用时用 `ContentRef`。
4. Graph 不再是必须存在的顶层业务组织模型；复杂 Logic 仍可使用图拓扑进行编排、调试和运行可视化，但它只是 Logic Aspect 的一种表达方式。
5. 数据提升为一等能力：`DataSlot / DataScope / DataRef / DataCarrier` 必须进入公共底座。
6. 跨能力协同不能只靠 `ContentRef / SymbolRef`；界面/功能复用用 Ref，能力调用使用 `CapabilityRef / ContractRef`。
7. “综合 Workbench”保留为产品概念，但 V1 不引入 `WorkbenchRef`。综合工作环境由 `WorkbenchHost / Registry / Navigation / Context / Layout / CommandRouter` 完成。
8. `InspectionWorkbench` 是自己的业务 Workbench，不是 Automation + Vision + CT3D 的简单拼装；它通过能力契约、服务、ContentRef 等使用其它领域能力。
9. Aspect 采用“按需装配”：简单 Symbol 可以只有 Visual/Data，复杂 Content 才拥有 Logic/Collaboration；禁止为了统一而让所有对象承担四套空壳。
10. Visual / Logic / Data / Collaboration / Runtime 五种 Lens 必须围绕同一个对象或实例 ID 可双向定位，避免设计态双真源。

---

## 1. 目标与非目标

### 1.1 目标

本架构要同时满足：

- HMI 画面与组态编辑；
- Automation 设备、运动、PLC/IO、状态与联锁；
- Vision 图像显示、ROI、算法调试、检测结果；
- CT3D MPR、Volume、重建与分析；
- Inspection 产品检测、判定、复测、MES、追溯；
- Simulation 仿真、方案比较、Shadow Run；
- 大型复合工业对象复用；
- 跨领域能力协同；
- 大体量数据按需加载/卸载；
- 设计态、逻辑态、数据态、协同态和运行态统一可视化；
- 工程保存/加载、版本迁移和旧工程兼容。

### 1.2 非目标

V1 不追求：

- 用一个万能基类承载所有领域语义；
- 让 Group/Element 直接承担复杂业务逻辑；
- 把任何逻辑都强制转成 Graph；
- 让综合 Workbench 形成一棵 Workbench 引用树；
- 用同一对象同时承担定义态、运行态和持久化 DTO；
- 在通用 Runtime 中承担伺服周期、保护跳闸等硬实时核心逻辑。

---

## 2. 统一对象模型

### 2.1 Project

`Project` 是整个工业工程聚合根，负责：

- 工程标识、版本、元数据；
- Workbench 注册；
- Page/Content/Symbol/资源统一索引；
- 全局资源与连接；
- 全局设置；
- 工程级 Logic/Data/Collaboration 能力；
- 保存、加载、迁移入口。

注意：Project 可以维护全工程 Catalog/Index，但业务所有权仍由 Workbench、Page 等对象承担。

### 2.2 Workbench

`Workbench` 是一个领域工作环境，不等于 Page 集合。它至少拥有：

- Pages；
- ToolWindows；
- Navigation；
- Commands / Ribbon Context；
- Domain Services；
- Domain Resources；
- Context；
- Lifecycle；
- 当前领域的 Content/Symbol/Logic/Data 能力入口。

当前典型 Workbench：

```text
Automation
Vision
CT3D
HMI
Inspection
Simulation
```

`Presentation.Wpf` 不属于业务 Workbench，它是表现/编辑/渲染技术层。

### 2.3 Page

`Page` 是页面壳。每个 Page **必须且仅有一个 RootContent**。

Page 主要负责：

- 标识与标题；
- 页面尺寸、背景、导航元数据；
- 页面生命周期；
- RootContent；
- 页面级设计器元数据。

业务逻辑尽量下沉到 RootContent，不让 Page 变成第二个业务容器。

### 2.4 Content

`Content` 是整套模型的核心：**功能组合 / Functional Composition**。

Content 可以直接拥有：

```text
Owned Content
ContentRef
SymbolRef
Group
Element
```

Content 可递归嵌套。它用于表达：

- 业务区域；
- 功能模块；
- 页面区域；
- 复合设备单元；
- 可复用大型组件；
- 多个 Symbol 的组合；
- 多领域能力汇合点。

Content 至少应具有基本空间能力：

```text
Bounds
Transform
Visibility
Enabled
ZOrder
Clip（可选）
Layout（可选）
```

否则 Group 不能装 Symbol，而 Content 又无法整体移动，会导致模型断裂。

### 2.5 ContentRef

`ContentRef` 是“可复用 Content 的实例引用”。它解决大型复合组件复用问题。

建议字段：

```text
InstanceId
TargetContentId
Alias
Transform
Bounds
Visibility
BindingContext
ParameterOverrides
StateOverrides
```

必须支持循环引用验证：

```text
A → B → C → A  禁止
A → A          禁止
```

### 2.6 Symbol

`Symbol` 是**基础可复用功能视觉组件**。

规则固定：

```text
Symbol
└─ exactly 1 RootGroup
   ├─ Group
   └─ Element
```

Symbol 不允许直接包含 Symbol，也不包含 Content。

它可以拥有自身的参数、状态、绑定、事件、Logic、Data、Collaboration 配置，但视觉根必须保持简单、纯粹、易渲染。

典型 Symbol：

```text
Valve
Pump
Motor
Camera
Robot
Axis
Gauge
AlarmLamp
ProcessChamber
LoadLock
```

### 2.7 SymbolRef

`SymbolRef` 表示 Symbol 在 Content 中的一次使用。

建议字段：

```text
InstanceId
TargetSymbolId
Transform
Visibility
BindingContext
ParameterOverrides
StateOverrides
```

复杂复合组件不要升级成“巨型 Symbol”，而应升级成 Reusable Content。

例如半导体：

```text
ClusterToolContent
├─ LoadLock SymbolRef
├─ Robot SymbolRef
├─ Chamber01 SymbolRef
└─ Chamber02 SymbolRef
```

再通过：

```text
FactoryPage
└─ ContentRef → ClusterToolContent
```

实现大型复合设备复用。

### 2.8 Group

`Group` 是纯视觉轻量组合：

```text
Group
├─ Group
└─ Element
```

职责仅包括：

- 一起移动/旋转/缩放；
- 选择、锁定；
- 局部视觉树；
- 绘图层次。

禁止：

```text
Group → Symbol
Group → Content
Group → ContentRef
```

### 2.9 Element

`Element` 是可视叶子对象，不再包含其它对象。

Element 可以是：

- 基础 SVG/Shape；
- Text/Image/Button；
- Chart；
- ImageViewer；
- SliceViewer；
- VolumeView Host；
- Histogram；
- Domain-specific View Host。

Element 可以产生事件，但不直接拥有复杂业务流程。

### 2.10 Layer

Layer 是**正交的显示/编辑管理维度**，不是对象组织主链。

```text
Content
├─ Object Tree
└─ LayerCollection
```

Visual Object 使用：

```text
ParentId  → 对象组织
LayerId   → 显示/编辑层
ZIndex    → 层内顺序
```

一个简单 Page 可以只有一个默认 Layer，全部对象都在里面；复杂 Page 再拆 Background/Equipment/Alarm/Annotation 等层。

---

## 3. 对象允许关系（必须实现为 Validator）

| Parent | 允许直接包含 |
|---|---|
| Project | Workbench、全局资源、共享 Content、Symbol、配置等 |
| Workbench | Page、工具窗、命令、领域服务、领域资源 |
| Page | 1 个 RootContent |
| Content | Owned Content、ContentRef、SymbolRef、Group、Element |
| Symbol | 1 个 RootGroup |
| Group | Group、Element |
| Element | 无 |

必须禁止：

- Symbol 直接包含 Symbol；
- Group 包含 Symbol/Content；
- Element 有 Children；
- Page 有多个 RootContent；
- ContentRef 循环；
- SymbolRef 指向不存在的 Symbol；
- Layer 充当对象父子关系。

---

## 4. 综合 Workbench：保留产品概念，不建立 WorkbenchRef 核心模型

### 4.1 为什么综合 Workbench 有必要

真实产品会出现统一开发/调试环境：

```text
CxVisual 综合工程工作台
├─ Automation
├─ Vision
├─ CT3D
├─ Inspection
├─ Simulation
└─ HMI
```

用户需要：

- 统一工程树；
- 统一登录与权限；
- 统一导航；
- Ribbon 上下文切换；
- ToolWindow/Dock 布局；
- 当前 Machine/Product/Recipe/Selection 上下文共享；
- Workbench 激活/休眠；
- 工作区布局保存与恢复。

### 4.2 V1 为什么不引入 WorkbenchRef

当前六个 Workbench 与八个案例没有真实要求“一个 Workbench 被另一个 Workbench 作为实例引用”。

目前需要的是：

- Workbench 注册与切换；
- Capability/Service 调用；
- ContentRef 复用；
- 共享上下文；
- Shell 统一管理。

因此 V1 实现：

```text
WorkbenchHost
├─ WorkbenchRegistry
├─ WorkbenchManager
├─ NavigationService
├─ ContextService
├─ LayoutService
└─ CommandRouter
```

而不是：

```text
Workbench
└─ WorkbenchRef → Workbench
```

只有未来出现以下真实需求时再引入 WorkbenchRef：

- 同一 Workbench 多实例；
- 用户在设计器中动态搭建 Workbench；
- Workbench 插件版本绑定；
- Workbench 跨进程/远程宿主；
- 每个引用需要独立实例配置与生命周期。

### 4.3 InspectionWorkbench 的正确定位

`InspectionWorkbench` 是自己的业务主体：

```text
InspectionWorkbench
├─ Product
├─ Recipe
├─ InspectionTask
├─ InspectionPoint
├─ Result
├─ Judge
├─ Retest
├─ Trace
├─ MES
└─ Statistics
```

它通过：

```text
Automation Capability
Vision Capability
CT3D Capability
ContentRef
Service / Contract
```

使用其它领域能力。

不要把它建模成：

```text
Inspection = Automation + Vision + CT3D
```

---

## 5. 四个正交能力面

统一对象模型解决“对象是谁、怎么组织”。其它能力不再建立第二套对象树，而是作为 Aspect 附着在同一对象身份上。

### 5.1 Visual Aspect

回答：**怎么显示、怎么编辑。**

典型能力：

```text
Bounds
Transform
Visibility
Enabled
LayerId
ZIndex
Style
Clip
Layout
Rendering
Selection
HitTest
```

主要宿主：Page、Content、Symbol、Group、Element。

### 5.2 Logic Aspect

回答：**怎么工作。**

典型能力：

```text
Event
Action
Rule
Expression
Script
State
Sequence / Flow
Branch / Loop
Parallel / Join
Wait / Timeout / Retry
Sub-flow
Workflow
Data processing logic
```

核心宿主：Project、Workbench、Content、Symbol。

Page 尽量委托 RootContent；Group/Element 不拥有复杂业务 Logic，Element 只作为 Event Source。

### 5.3 Data Aspect

回答：**数据是什么、在哪里、活多久、怎么承载。**

必须支持：

```text
DataSlot
DataScope
DataStack
DataRef
DataCarrier
DataBinding
DataContract
```

这是工业 Vision/CT/AI 平台不可缺少的一等能力。

### 5.4 Collaboration Aspect

回答：**对象/领域之间如何复用与协同。**

包含：

```text
ContentRef
SymbolRef
CapabilityRef
ContractRef
Event / Message
Command / Service contract
Context mapping
```

必须区分：

```text
ContentRef   = 复用一个复合内容/功能区域
SymbolRef    = 复用一个基础功能组件
CapabilityRef= 调用另一个领域的能力
```

ContentRef/SymbolRef 不能替代 Capability 调用。

---


### 5.5 Aspect 按需装配原则

四个 Aspect 是能力维度，不是“每个对象必须各自拥有四个重量级实例”。建议按宿主和场景装配：

| 对象/示例 | Visual | Logic | Data | Collaboration |
|---|---:|---:|---:|---:|
| Pipe / StaticText | ✅ | — | △ | — |
| AlarmLampSymbol | ✅ | — | ✅ | — |
| MotorSymbol | ✅ | ✅ | ✅ | ✅ |
| CameraSymbol | ✅ | ✅ | ✅ | ✅ |
| MachineContent | ✅ | ✅ | ✅ | ✅ |
| Project / Workbench | △ | ✅ | ✅ | ✅ |

实现上优先采用组合与 Capability/Aspect Host，而不是巨型共同基类。

### 5.6 Runtime Projection 属于实例，不属于定义

必须区分：

```text
Definition                         Runtime Instance
──────────────────────────────────────────────────
Symbol          ── SymbolRef ──►  SymbolInstance
Content         ── ContentRef ─►  ContentInstance
LogicDefinition ── execute ────►  LogicExecutionInstance
```

运行态：

```text
MotorSymbol
   ├─ Motor01 Instance → Running / 1450rpm
   ├─ Motor02 Instance → Fault
   └─ Motor03 Instance → Stopped
```

因此禁止 `MotorSymbol.Runtime.State = ...` 这类设计；`RuntimeProjectionService` 应按 `InstanceId/ObjectId` 聚合状态，并投影到 Workbench 的 Runtime Lens。


## 6. 典型 Symbol / Content 实现验证

这一章不是新增抽象，而是用 3 种 Symbol + 3 种 Content 验证“四个正交能力面”是否真正可落地。结论是：**可以落地，但 Aspect 必须按需装配，Runtime 状态必须属于实例而不是定义。**

### 6.1 AlarmLampSymbol：最小型 Symbol

```text
AlarmLampSymbol
├─ RootGroup
│  ├─ CircleElement
│  └─ TextElement
│
├─ VisualAspect
│  ├─ Bounds / Transform
│  └─ StateStyle: Normal / Warning / Alarm
│
├─ DataAspect
│  └─ AlarmState : DataSlot<AlarmState>
│
└─ 不需要 LogicAspect / CollaborationAspect
```

这证明 Aspect 不能设计成“每个对象强制四套完整子系统”。简单状态灯只需要显示 + 数据绑定即可。

运行时不是修改 `AlarmLampSymbol` 定义，而是：

```text
AlarmLampSymbol
    ↓ SymbolRef
AlarmLampInstance #A
    ├─ AlarmState = Alarm
    └─ RuntimeProjection = RedBlinking
```

### 6.2 MotorSymbol：有行为与外部能力的 Symbol

```text
MotorSymbol
├─ RootGroup
│  ├─ MotorBody
│  ├─ RunningLamp
│  ├─ FaultLamp
│  └─ SpeedText
│
├─ VisualAspect
│  └─ Stopped / Running / Fault 视觉状态
│
├─ LogicAspect
│  ├─ Start
│  ├─ Stop
│  ├─ Reset
│  └─ StateLogic
│
├─ DataAspect
│  ├─ Speed      Input
│  ├─ IsRunning  Input
│  ├─ Fault      Input
│  └─ Command    Output
│
└─ CollaborationAspect
   └─ MotionCapabilityRef
      ├─ StartMotor
      ├─ StopMotor
      └─ ResetMotor
```

边界：MotorSymbol 不是 PLC/运动控制器本身。它封装“电机这个功能组件”的视觉、输入输出、逻辑和能力契约；真实控制由 Capability Provider / Adapter 承担。

### 6.3 CameraSymbol：验证 DataRef / DataCarrier

```text
CameraSymbol
├─ RootGroup
├─ VisualAspect
├─ LogicAspect: Open / Close / Acquire
├─ DataAspect
│  ├─ Exposure : DataSlot<double>
│  ├─ Gain     : DataSlot<double>
│  └─ Image    : DataRef
│                ↓
│              DataCarrier
│                ├─ Load
│                ├─ Unload
│                ├─ Pin
│                └─ Release
└─ CollaborationAspect
   └─ CameraCapabilityRef
```

规则：Exposure/Gain 这类小值可以放 DataSlot；Image/Projection/Volume 这类大对象通过 `DataRef → DataCarrier` 承载，禁止在 DataStack 中复制大块数据。

### 6.4 MachineContent：多个 Symbol 的功能组合

```text
MachineContent
├─ SymbolRef → Motor01
├─ SymbolRef → Motor02
├─ SymbolRef → Cylinder01
├─ SymbolRef → AlarmLamp
├─ Group     → 背景/静态图形
│
├─ VisualAspect
│  └─ Bounds / Transform / Visibility
├─ LogicAspect
│  └─ Initialize / Home / Start / Stop / FaultRecovery
├─ DataAspect
│  └─ MachineScope
│     ├─ CurrentRecipe
│     ├─ MachineState
│     ├─ ProductInfo
│     └─ FaultInfo
└─ CollaborationAspect
   ├─ MotionCapability
   ├─ PlcCapability
   └─ AlarmCapability
```

这里能清楚区分：`MotorSymbol` 是组件封装；`MachineContent` 是多个组件组成一个功能单元。

### 6.5 VisionInspectionContent：验证复杂 Logic + 大数据

```text
VisionInspectionContent
├─ SymbolRef → CameraSymbol
├─ Element   → ImageViewerElement
├─ Group     → ROI Group
├─ Content   → ResultContent
│
├─ LogicAspect
│  └─ Acquire → Preprocess → (AI || Traditional) → Merge → Judge
│
├─ DataAspect
│  ├─ RawImageRef → ImageCarrier
│  ├─ RoiList
│  ├─ AiResult
│  ├─ TraditionalResult
│  └─ FinalResult
│
└─ CollaborationAspect
   ├─ CameraCapability
   ├─ AiInferenceCapability
   └─ StorageCapability
```

这里的复杂 Logic 可以在 Workbench 中以 Graph/Topology Lens 展示，但业务模型仍然叫 `LogicDefinition / LogicUnit / LogicLink`，不让 UI `GraphNode` 反向污染业务对象。

### 6.6 InspectionContent：验证跨域综合能力

```text
InspectionContent
├─ ContentRef → Automation.DeviceArea
├─ ContentRef → Vision.InspectionArea
├─ ContentRef → CT3D.MprArea
├─ Content    → ResultContent
├─ Content    → AlarmContent
│
├─ LogicAspect
│  └─ InspectionFlow
├─ DataAspect
│  └─ ProductScope
└─ CollaborationAspect
   ├─ AutomationCapabilityRef
   ├─ VisionCapabilityRef
   ├─ CTCapabilityRef
   └─ MesCapabilityRef
```

同一个 `InspectionContent` 应提供五种 Lens，而不是五套对象：

```text
Visual Lens        Camera / X-Ray / CT / Result
Logic Lens         Move → Acquire → Detect → Judge → MES
Data Lens          ImageRef → ProjectionRef → VolumeRef → ResultRef
Collaboration Lens Automation / Vision / CT3D / MES
Runtime Lens       当前步骤、DataCarrier、GPU/设备、Trace
```

### 6.7 本轮实例验证后的冻结结论

1. `Symbol` 负责组件封装；`Content` 负责功能组合，二者边界成立。
2. Aspect 必须按需装配，不允许为了“统一”制造大量无意义空对象。
3. `Visual/Logic/Data/Collaboration` 属于定义能力；`RuntimeProjection` 属于运行实例。
4. 复杂 Logic 可以用 Graph 可视化，但 Graph 是 Lens/Editor 能力，不是第二套业务组织树。
5. `ContentRef/SymbolRef` 解决对象复用；`CapabilityRef/ContractRef` 解决能力调用，不能混用。
6. 大数据必须走 `DataRef + DataCarrier`，生命周期由 `DataScope` 管理。
7. 一个定义可以有多个运行实例，运行状态绝不能回写定义对象。

## 7. Logic：不绑死 Graph，但保留图式编排能力

### 7.1 为什么仍需要复杂 Logic 能力

复杂工业逻辑要表达：

- 顺序、分支、循环；
- 并行、汇合；
- 状态机；
- 等待、超时、重试；
- 子流程；
- 数据依赖；
- 设备/Vision/CT/MES 协同；
- 调试、Trace、运行监控。

### 7.2 Graph 的最终定位

Graph 不再是与 Content 并列的顶层组织模型。

最终定义：

> Graph/Topology 是 Logic Aspect 对复杂逻辑的一种可视编辑和可视运行表达方式，不是所有 Logic 的强制数据结构。

简单 Logic 可以直接是：

```text
Action
Expression
Rule
Script
Binding
```

复杂 Logic 可以具有拓扑：

```text
LogicDefinition
├─ LogicUnit
├─ LogicLink
├─ Inputs / Outputs
└─ TopologyView（可选）
```

Workbench 可把它画成 Node/Edge，但业务对象不依赖“GraphNode”概念。

### 7.3 统一可视：不要分裂成两个世界

同一个对象提供不同 Lens：

```text
Visual Lens        系统是什么
Logic Lens         系统怎么工作
Data Lens          数据怎么流与承载
Collaboration Lens 跨能力怎么协同
Runtime Lens       现在发生什么
```

例如同一 `InspectionContent`：

```text
Visual:  Camera / X-Ray / CT / Result
Logic:   Move → Acquire → Detect → Judge → MES
Data:    ImageRef → ProjectionRef → VolumeRef → ResultRef
Collab:  Automation / Vision / CT3D / MES
Runtime: Detect Running，Volume 4.2GB Loaded，GPU03 Busy
```

所有 Lens 使用同一对象 ID；禁止复制一套“逻辑对象树”再与 Visual Tree 做镜像同步。

---

## 8. Data Aspect 详细设计

### 8.1 DataSlot

逻辑单元/宿主暴露：

```text
Input
Output
Local
```

不再使用“NodeData”作为公共概念，因为并非所有 Logic 都是 Graph Node。

### 8.2 DataScope / DataStack

`DataScope` 管理生命周期和可见域；`DataStack` 是其运行时存储结构之一。

典型层级：

```text
ProjectScope
└─ WorkbenchScope
   └─ PageScope
      └─ ContentScope
         └─ LogicExecutionScope
```

Stack Item 支持：

```text
Value
Object
List
Map
Ref
```

生命周期策略应能表达：

```text
Project
Workbench
Page
Content
Execution
Temporary
External
```

### 8.3 DataRef

`DataRef` 是稳定引用，不等于数据本体。

它需要表达：

```text
DataId
TypeId
Version
Location / Provider
Metadata
Lifetime hint
```

### 8.4 DataCarrier

`DataCarrier` 负责真正的数据承载与装卸：

```text
Load
Unload
LazyLoad
Pin
Release
Cache
MemoryMap
SharedMemory
Remote
```

典型 CT：

```text
VolumeRef
   ↓
DataCarrier
   ↓
2~10GB Volume
```

不要把 10GB Volume 直接复制进 DataStack。

### 8.5 建议接口

```csharp
public interface IDataCarrier
{
    string CarrierId { get; }
    bool IsLoaded { get; }
    Task LoadAsync(CancellationToken cancellationToken);
    Task UnloadAsync(CancellationToken cancellationToken);
    object GetPayload();
}

public sealed class DataRef
{
    public string DataId { get; set; }
    public string TypeId { get; set; }
    public string CarrierId { get; set; }
}
```

---

## 9. Collaboration Aspect 详细设计

### 9.1 ContentRef / SymbolRef

属于对象复用关系，解决“把什么放进来”。

### 9.2 CapabilityRef / ContractRef

属于能力协同关系，解决“我可以调用什么”。

示例：

```text
InspectionContent
├─ ContentRef    → Vision.ImageViewer
└─ CapabilityRef→ Vision.Detect
```

这是两种不同关系，禁止混用。

### 9.3 Event / Message

跨模块、异步、松耦合场景使用事件/消息，不应让 Content 直接拿到另一个 Workbench 的内部对象实例。

### 9.4 边界原则

- 通过 Contract 暴露能力；
- 通过 Ref 暴露可复用内容；
- 通过 Event/Message 做异步协同；
- 领域内部实现保持隐藏。

---

## 10. Runtime Projection：运行可视化但不再造第二套设计态树

Runtime 要做的是**对运行实例进行投影/叠加**，不是复制组织模型，更不能把实例状态写回 Content/Symbol 定义。

运行时可以有内部执行对象，例如：

```text
LogicExecutionInstance
DataScopeInstance
ResourceHandle
TraceSession
```

但这些是 Runtime 内部状态，不成为用户必须理解的第二套设计对象树。

Workbench Runtime Lens 应能在原对象上显示：

```text
当前 Logic Unit
Running / Waiting / Completed / Fault
Input / Output 数据
DataCarrier Loaded / Unloaded / Size
Capability 调用目标
资源状态
Elapsed
Trace / Log
```

定义 ID + 实例 ID 共同用于：

```text
Visual → Logic → Data → Collaboration → Runtime
```

可互相定位。

---

## 11. 保存 / 加载与序列化

### 11.1 Domain Model 与 Persistence Model 分离

禁止：

```csharp
JsonConvert.SerializeObject(domainObjectGraph)
```

直接把运行服务、缓存、句柄、事件、Resolved 对象一起保存。

正确链路：

```text
Domain Model
   ↓ Mapper
Persistence DTO
   ↓ Serialize
JSON / Assets
   ↓
.cxv Package
```

加载：

```text
Package
→ DTO
→ Schema Migration
→ Reference Resolve
→ Validation
→ Domain Model
```

### 11.2 建议包结构

```text
Project.cxv
├─ manifest.json
├─ project.json
├─ workbenches/
├─ pages/
├─ contents/
├─ symbols/
├─ logic/
├─ data/
├─ resources/
├─ assets/
└─ metadata/
```

其中 `logic/` 可以保存复杂可复用 LogicDefinition，但它不是第二套业务组织树，只是 Project Resource Catalog 中的一类资源。

### 11.3 SchemaVersion

必须从 V1 开始保存：

```text
SchemaVersion
ProductVersion
ProjectId
CreatedAt
ModifiedAt
```

并提供 Migration Pipeline。

---

## 12. 建议代码工程组织

```text
CxVisual.Core.Model
├─ Project
├─ Workbench
├─ Page
├─ Content
├─ ContentRef
├─ Symbol
├─ SymbolRef
├─ Group
├─ Element
└─ Layer

CxVisual.Core.Aspects
├─ Visual
├─ Logic
├─ Data
└─ Collaboration

CxVisual.Workbenches
├─ WorkbenchHost
├─ WorkbenchRegistry
├─ WorkbenchManager
├─ NavigationService
├─ ContextService
├─ LayoutService
└─ CommandRouter

CxVisual.Logic
├─ LogicDefinition
├─ LogicUnit
├─ LogicLink
├─ LogicRuntime
├─ Validation
└─ Debugging

CxVisual.Data
├─ DataSlot
├─ DataScope
├─ DataStack
├─ DataRef
├─ DataCarrier
└─ DataContract

CxVisual.Collaboration
├─ CapabilityRef
├─ ContractRef
├─ Event
├─ Message
└─ Routing

CxVisual.Runtime
├─ RuntimeProjectionService
├─ LogicExecutionInstance
├─ DataScopeInstance
├─ TraceSession
└─ Diagnostics

CxVisual.Persistence
├─ DTO
├─ Mapper
├─ Serializer
├─ Migration
├─ ReferenceResolver
└─ Validator

CxVisual.Presentation.Wpf
├─ VisualDesigner
├─ LogicDesigner
├─ DataInspector
├─ CollaborationView
├─ RuntimeMonitor
└─ PropertyEditor
```

注意：若当前源码已经存在 Graph.Core / Graph.Runtime，不要求一次删除。Codex 应先兼容包装，再逐步把其语义收敛到 Logic 模块。

---

## 13. 主要类骨架（建议）

### 13.1 Content

```csharp
public sealed class Content
{
    public string Id { get; set; }
    public string Name { get; set; }

    public VisualAspect Visual { get; private set; }
    public LogicAspect Logic { get; private set; }
    public DataAspect Data { get; private set; }
    public CollaborationAspect Collaboration { get; private set; }

    public IList<IContentItem> Children { get; private set; }
    public LayerCollection Layers { get; private set; }
}
```

### 13.2 Symbol

```csharp
public sealed class Symbol
{
    public string Id { get; set; }
    public string Name { get; set; }

    public Group RootGroup { get; private set; }

    // Symbol 天然可视；其余 Aspect 按需配置。
    public VisualAspect Visual { get; private set; }
    public LogicAspect Logic { get; private set; }
    public DataAspect Data { get; private set; }
    public CollaborationAspect Collaboration { get; private set; }
}

// 注意：RuntimeProjection 不放在 Symbol 定义中。
// SymbolRef 实例化后，由 SymbolInstance / RuntimeProjectionService 保存运行状态。
```

### 13.3 Group / Element

```csharp
public sealed class Group : VisualObject
{
    public IList<VisualObject> Children { get; private set; }
    // Validator 只允许 Group / Element
}

public abstract class Element : VisualObject
{
    public string ElementTypeId { get; set; }
    // 叶子；可以定义事件源，不直接拥有复杂业务 Logic
}
```

### 13.4 LogicAspect

```csharp
public sealed class LogicAspect
{
    public IList<LogicDefinition> LogicDefinitions { get; private set; }
    public IList<LogicBinding> Bindings { get; private set; }
}
```

### 13.5 DataAspect

```csharp
public sealed class DataAspect
{
    public IList<DataSlot> Slots { get; private set; }
    public DataScopePolicy ScopePolicy { get; set; }
    public IList<DataBinding> Bindings { get; private set; }
}
```

### 13.6 CollaborationAspect

```csharp
public sealed class CollaborationAspect
{
    public IList<CapabilityRef> Capabilities { get; private set; }
    public IList<ContractRef> Contracts { get; private set; }
    public IList<EventBinding> Events { get; private set; }
}
```

---

### 13.7 Runtime 实例边界（必须实现）

建议最小运行态模型：

```csharp
public sealed class SymbolInstance
{
    public string InstanceId { get; set; }
    public string SymbolId { get; set; }
    public string BindingContextId { get; set; }
}

public sealed class ContentInstance
{
    public string InstanceId { get; set; }
    public string ContentId { get; set; }
    public string BindingContextId { get; set; }
}

public sealed class RuntimeProjection
{
    public string InstanceId { get; set; }
    public string State { get; set; }
    public IDictionary<string, object> Metrics { get; private set; }
    public IList<string> ActiveLogicUnits { get; private set; }
}
```

规则：定义可被多个实例复用；实例拥有自己的 DataScope/LogicExecution/RuntimeProjection。保存工程时保存定义与必要实例配置，不序列化活动线程、句柄、GPU/PLC Handle 或临时 Runtime State。

## 14. Workbench 与案例压力测试结论

### 14.1 六类 Workbench

| Workbench | 主要承接方式 |
|---|---|
| HMI | Page + Content + Symbol + Visual/Logic/Data |
| Vision | Image/ROI/Result Content + 专业 Element + DataCarrier |
| CT3D | MPR/Volume Content + 大数据 DataRef/DataCarrier |
| Automation | Device Symbol + Content + Logic + Capability |
| Inspection | 独立业务 Content + 其它领域 Capability/ContentRef |
| Simulation | Scene Content + Simulation Logic/Data + Runtime Projection |

### 14.2 八类案例反推

1. 工业 X-Ray 检测：验证跨 Automation/Vision/CT/MES 协同。
2. CT 重建与分析：验证大数据承载、分布式/异步 Logic、人工审核。
3. 多机器人：验证任务逻辑、状态、资源协同与运行态监控。
4. 工业 IoT：验证 Streaming Data、缓冲、边云协同。
5. 数字孪生：验证仿真时间、Fork/Clone 未来扩展与运行投影。
6. ISA-88：验证深层业务过程与设备/人工/质量协同。
7. 半导体 Cluster Tool：验证大型复合 Content + SymbolRef 复用。
8. 电力 ICS：验证交互/事件/能力契约与安全边界。

结论：这些案例没有要求增加第二套面向用户的对象组织树；差异主要通过 Aspect 与 Runtime Provider 扩展即可。

---

## 15. Codex 实施计划（必须按顺序）

### P0：冻结术语和兼容层

任务：

- 固定本文件中的术语；
- 不继续引入 `CompositeWorkbench`、`WorkbenchRef`、万能 `Graph` 根对象；
- 对现有 Graph 模块建立兼容适配层，不立即破坏旧功能；
- 建立旧类 → 新模型映射表。

验收：旧工程可编译，新模型单元测试可启动。

### P1：统一对象模型

实现：

```text
Project
Workbench
Page
Content
ContentRef
Symbol
SymbolRef
Group
Element
Layer
```

同时实现 `ObjectModelValidator`。

重点测试：

- Page 只能有一个 RootContent；
- Group 只能装 Group/Element；
- Symbol 只能有一个 RootGroup；
- ContentRef 循环检测；
- SymbolRef 不存在目标检测。

### P2：Visual Aspect

实现：

- Bounds/Transform/Visibility/Enabled/ZOrder；
- Layer 与对象父子关系解耦；
- 统一选择、移动、复制、Undo/Redo 接口；
- WPF Renderer 只依赖 Visual Contract。

### P3：Data Aspect

实现：

- DataSlot；
- DataScope/DataStack；
- DataRef；
- IDataCarrier；
- DataBinding；
- 至少实现 Memory / File / SharedMemory 三类 Carrier Adapter。

必须用 CT 2~10GB 体积数据做压力测试。

### P4：Logic Aspect

先支持：

```text
Action
Expression
Rule
State
Sequence/Flow
```

再把现有 Graph Runtime 通过 Adapter 接入复杂 Flow。

要求：

- 不强制所有 Logic 有 Graph；
- LogicDefinition 与 UI GraphNode 解耦；
- 能在 Logic Lens 中可视化；
- 支持断点、单步、Trace 的最小闭环。

### P5：Collaboration Aspect

实现：

- CapabilityRef；
- ContractRef；
- Event/Message；
- Context Mapping；
- Inspection → Vision/Automation/CT3D 的真实调用样例。

### P6：Runtime Projection

实现统一 Runtime Lens：

- Visual 对象状态叠加；
- 当前 Logic Unit；
- DataSlot 值；
- DataCarrier 状态；
- Capability 调用；
- Trace；
- 从 Runtime 定位回设计对象。

禁止建立第二套用户可编辑 Runtime Tree。

### P7：Persistence

实现：

- DTO；
- Mapper；
- Serializer；
- ReferenceResolver；
- Migration；
- SchemaVersion；
- Missing/Unknown Type 占位。

必须验证保存 → 关闭 → 加载 → 引用完整 → 再运行。

### P8：WorkbenchHost 与综合 Workbench 产品能力

实现：

- WorkbenchRegistry；
- Activate/Deactivate；
- Navigation；
- Ribbon Context；
- Context Sharing；
- Dock Layout；
- Workspace Persistence。

不实现 WorkbenchRef。

### P9：八案例回归

用八个案例做自动化和人工回归，记录每个案例使用了哪些：

```text
对象关系
Visual
Logic
Data
Collaboration
Runtime
```

如果出现新需求，先判断是：

```text
公共 Aspect 能力缺失？
Provider/Adapter 缺失？
领域特有能力？
```

禁止第一反应就是新增顶层对象类型。

---

## 16. Codex 禁止事项

Codex 修改代码时必须遵守：

1. 不允许让 `Group` 包含 `Symbol` 或 `Content`。
2. 不允许让 `Symbol` 直接包含 `Symbol`。
3. 不允许给 `Element` 增加 Children。
4. 不允许把 `Layer` 变成对象父子树。
5. 不允许把整个 Graph/Logic 复制进每个 Content/Symbol。
6. 不允许让 UI GraphNode 成为业务模型依赖。
7. 不允许让 `DataStack` 直接持有超大数据副本。
8. 不允许用 ContentRef 代替 Capability 调用。
9. 不允许新增 WorkbenchRef，除非先有真实 V2 场景与架构评审。
10. 不允许直接序列化 Domain Runtime 对象树。
11. 不允许建立设计态“双真源”：Visual Tree 与 Logic Tree 镜像同步。
12. 不允许一次性大爆炸重构；必须保留兼容 Adapter 并分阶段通过测试。

---

## 17. 验收用例

### A. 简单 HMI

一个 Page，一个 RootContent，一个默认 Layer，直接放 Element/Group/SymbolRef，操作闭环。

### B. Symbol 创建

选择纯 Group/Element → 创建 Symbol → 生成 RootGroup → 原位置替换为 SymbolRef。

### C. Cluster Tool

通过 Reusable Content + 多个 SymbolRef 组合，允许多个 Page 用 ContentRef 复用；禁止 Symbol 套 Symbol。

### D. Inspection

Inspection 自己有业务 Page/Content；调用 Vision.Detect、Automation.Motion、CT3D.Reconstruct；复用 Vision Viewer/CT MPR Content；不依赖 WorkbenchRef。

### E. CT 大数据

VolumeRef 在 DataScope 中流转；DataCarrier 支持 Load/Unload/Lazy；切换 Page 不复制体积数据。

### F. 多视图

同一个 Content 在 Visual/Logic/Data/Collaboration/Runtime Lens 中使用同一对象 ID，可双向定位。

### G. 无界面后台逻辑

Project 或 Workbench LogicAspect 可运行后台 Logic，不要求 Page/Content/Element 存在。

### H. 保存加载

保存后重开，所有 ContentRef/SymbolRef/CapabilityRef 解析正确；缺插件/类型时可占位打开而不是工程整体失败。

---

## 18. 最终架构规则（冻结版）

```text
R01  全平台只有一套主对象组织结构。
R02  Workbench 是领域工作环境，Presentation.Wpf 不是业务 Workbench。
R03  Page 只能拥有一个 RootContent。
R04  Content 是功能/业务/区域组合核心。
R05  Content 可拥有 Owned Content / ContentRef / SymbolRef / Group / Element。
R06  ContentRef 用于复用复合 Content，必须做循环检测。
R07  Symbol 是基础可复用功能视觉组件，只拥有一个 RootGroup。
R08  Symbol 不直接包含 Symbol；复杂组合升级为 Content。
R09  Group 只能包含 Group / Element。
R10  Element 是叶子，可以产生事件，但不承担复杂业务 Logic。
R11  Layer 是正交显示/编辑维度，不是对象父子链。
R12  Visual / Logic / Data / Collaboration 是四个正交能力面。
R13  Runtime 是对统一对象的状态投影，不建立第二套用户设计树。
R14  Graph/Topology 仅是复杂 Logic 的可视表达方式，不是强制顶层对象模型。
R15  DataSlot / DataScope / DataRef / DataCarrier 是公共数据底座。
R16  ContentRef/SymbolRef 解决对象复用；CapabilityRef/ContractRef 解决跨能力调用。
R17  Inspection 是独立业务 Workbench，通过能力与 ContentRef 使用其它领域。
R18  综合 Workbench 是产品概念，由 WorkbenchHost 管理；V1 不引入 WorkbenchRef。
R19  定义态、运行态、持久化 DTO 必须分离。
R20  新需求优先扩展 Aspect/Provider/Adapter，谨慎新增顶层对象类型。
R21  Aspect 按需装配；禁止所有对象强制拥有四套重量级能力对象。
R22  RuntimeProjection 必须属于运行实例/运行服务，禁止写入 Content/Symbol 定义。
R23  同一个定义允许存在多个实例；实例状态、DataScope、执行上下文必须隔离。
R24  Visual/Logic/Data/Collaboration/Runtime Lens 必须支持按 ObjectId/InstanceId 双向定位。
```

---

## 19. 给 Codex 的最终工作指令

> 以本文件为当前架构真源。先保持现有工程可编译、可运行，再逐步迁移。任何重构都必须做到：对象身份不丢、引用可解析、旧工程可迁移、运行态不污染定义态。若现有 Graph 代码已经成熟，不要删除其算法/Runtime；把它作为复杂 Logic 的 Provider/Adapter 融入新的 Logic Aspect。所有新增功能必须先判断属于 Visual、Logic、Data、Collaboration、Runtime Projection 中哪一面，禁止重新建立平行的业务对象体系。实现前至少用 AlarmLampSymbol、MotorSymbol、CameraSymbol、MachineContent、VisionInspectionContent、InspectionContent 六个案例回归对象边界；若这些案例需要通过新增顶层类型才能完成，应先暂停编码并重新做架构评审。

