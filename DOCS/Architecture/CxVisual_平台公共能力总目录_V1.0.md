# CxVisual 平台公共能力总目录 V1.0

> **文件定位**：CxVisual 的平台公共能力、公共中心、Project 根目录与专业 Workbench 的统一导航及职责边界文档。本文回答“平台有什么公共能力、从哪里进入、怎样进入 Project、如何与专业组合”，不复制各子体系详细规格。
>
> **日期**：2026-10-30；**主仓库**：oudream/cx-visual-x3；**核对起点**：master@2b2428d810a7e9ed6377d465ac5f347602199d37（并行提交期间可能继续变化）。
>
> **裁决状态**：【确认】用户认可 Project → 独立资源 → 内嵌对象的导航原则；Content/Symbol 各自有加载、管理与组合入口，加入 Project 后再配置、共享；数据与交互既有各自中心，也可将定义/引用加入 Project。【目标】统一公共能力目录与 Studio 体验。【源码】仅以逐项列出的真实 API/文件为准。【验收】本文件为设计整理，不代表功能、Windows CI 或设备 HIL 已通过。
>
> **最高原则**：**各方面都尽能力展现，便捷友好尽支撑各专业达到目标。**
>
> **实施红线**：保持 Foundation / Mechanism / Professional 及唯一 Owner；优先复用当前 26 工程与已有 Studio、TestCenter；不因用户视图出现“中心/目录”就另建 Registry、数据仓、配置仓、工程树、消息队列、命令执行器或运行调度器。

## 0. 三分钟全貌

**一个平台、六个专业、若干公共中心、一个 Project 定义根、多个受控运行作用域。**

1. **平台公共能力（Platform Common）**：所有专业可发现和使用的类型、算法、配置、数据与交互、模块能力、诊断和治理机制。
2. **平台公共中心（Platform Centers）**：面向人类的管理、组合、观察、调试入口；中心只投影或操作原 Owner。
3. **工程定义根（Project）**：MEMProject 是**当前工程**资源、引用、组合、版本和事务的唯一持久化根；不是“整个平台唯一 Project”，也不是全进程静态变量。
4. **专业工作台（Professional Workbenches）**：Automation、HMI、Vision、CT3D、Inspection、Simulation 调用公共能力，负责各自专业语义与最终应用。
5. **运行实例（Runtime）**：Host/Session/Run/Flow/Scope/Frame 等由各自 Runtime/Data Owner 持有；工程定义与运行状态不混存。

```text
CxVisual Platform
├─ 公共中心（开发、配置、组合、运行、运维的统一入口）
│  ├─ 工程资源与共享库 | Content中心 | Symbol中心
│  ├─ 统一配置 | 数据与交互（变量/运行数据/消息/命令）
│  ├─ 功能模块与能力 | 公共工具 | 决策与构建
│  └─ 运行控制与诊断 | 发布与服务 | 安全审计 | TestCenter
├─ Project A / Project B / ...（各自唯一 MEMProject 定义根）
│  ├─ Pages / Contents / Symbols / Flow资源 / 模块引用
│  ├─ Data&Interaction Definitions / Config Bindings
│  └─ Assets / Cases / Dependencies
├─ 专业工作台：Automation | HMI | Vision | CT3D | Inspection | Simulation
└─ Runtime Host(s)：各自的 Scope、运行数据与执行状态
```

**重要区别**：平台能力全局可用 ≠ 平台内只有一个可写状态实例 ≠ 某工程永久拥有所有外部资源。导航目录 ≠ 新持久化对象树。

## 1. “全局”的四层语义与资源类型

| 分类 | 含义 | 典型例子 | 约束 |
|---|---|---|---|
| G0 平台公共 | 所有专业复用的契约、机制或无状态函数 | FCM/MHS、MDS/MNM/MRK、Numerics、Geometry | 不默认跨进程共享可写实例 |
| G1 系统/宿主级 | 系统安装、Host、进程或部署环境层面的实例和管理 | 系统配置、模块装载、Host 服务、日志策略 | 明确 HostId/ScopeId、权限、生命周期 |
| G2 工程级 | 某个 MEMProject 共享的定义和资源 | Page、可复用 Content、Symbol、工程配置绑定、能力引用 | 由 MEM 身份、事务和版本控制；多工程隔离 |
| G3 运行/调用级 | 当前 Run/Flow/Frame/节点的值与现场状态 | 变量快照、FlowHeap、DataStack、Operation、Trace | 运行 Owner；不直接写回工程树 |

**额外区分**：
- **纯静态工具**：无状态纯运算，不必有 Project Resource、模块实例或运行中心；仅当需要可发现、可配置、可编排时，薄适配为 Capability。
- **工程 Resource**：可以独立创建、保存、引用、版本化与复用的定义。
- **EmbeddedObject**：属于 Resource 内部的节点、图元、端口、ROI 等，不能因为可选中就自动升级为顶层 Resource。
- **外部资产/Artifact**：静态文件、模型、构建制品或大数据成果，用受控引用和版本/散列管理；不把体数据直接写入普通工程定义。

## 2. 平台公共能力总目录（正式分类，不等于新增工程）

状态说明：**实**=发现对应真实类型/入口；**局**=已有基础但不构成完整中心；**目**=本目录目标设计/待验收。下表状态仅说明本轮只读代码审查，不宣称最新 master 同 SHA 测试通过。

| 编号 | 体系/中心 | 核心责任与用户动作 | 唯一 Owner / 复用入口 | 当前判断 | Studio 建议 |
|---|---|---|---|---|---|
| P01 | 工程组织与资源中心 | Project、资源/引用、页面、事务、Undo/Redo、导入导出、资源树 | Mechanism.Core：MEM / MTC / MRI / MTI；Studio资源视图 | 实/局 | 一级公共入口、左侧资源导航 |
| P02 | **Content 管理中心** | 加载可用类型/Content制品、浏览、组合、编辑、发布/引用、版本 | MEMContent/MPM/MTI + 来源 Provider；MEM唯一工程真源 | 局/目 | 独立内容管理页及编辑器 |
| P03 | **Symbol 管理中心** | 加载图元/组件类型与制品、组合、属性/端口、预览、版本共享 | MEMSymbol/MRCGroup/MPM/MTI + 已有 Visual | 局/目 | 独立组件中心与设计器 |
| P04 | 统一配置中心 | 系统、用户、Host、工程、模块/设备配置；Schema、Saved/Effective/TaskPinned、回退 | Foundation.Data.Configuration（FCM）+ Mechanism.Data.Configuration（MHS） | 实/局 | 全局配置中心 + 工程配置绑定 |
| P05 | 数据与交互中心 | 变量、Heap/Stack、消息、命令；定义、观察、调试、追踪 | MDS Variables/RuntimeData、MNM Messages、FDC/MRK Commands | 实/局 | 一个总入口，四个正式能力页 |
| P06 | 功能模块与能力中心 | Connector/Driver/Compute/Simulation/Service、Capability发现、参数、引用 | Mechanism.Core.Modules（MFM）+ Decision/MOC能力目录 + 专业 Provider | 实/局 | 沿用已有功能模块中心 |
| P07 | 公共工具中心 | 数值、几何、图像/体数据基础函数、路径、校验、通用转换 | Foundation.Numerics/Geometry/Imaging/Volume 等 | 实/局 | 按类别检索/调用；非全部工程资源 |
| P08 | 决策与构建中心 | 能力匹配、约束/方案、静态校验、Plan编译、构建报告 | Mechanism.Decision（含现有MOC能力） | 局/目 | 统一构建/决策/诊断入口 |
| P09 | 运行与任务中心 | Task/Flow/Job、调度、执行控制、资源租约、状态/异常 | Mechanism.Runtime / Runtime.Host / F19 | 实/局 | 运行控制台，按 Host/Run 查找 |
| P10 | 资源与资产库 | Content/Symbol 包、模板、脚本、静态文件/模型、外部资产、依赖 | MEM资产与 MTI 导入导出/引用；专业资产 Owner | 局/目 | 共享库导航；不是第二个 Project |
| P11 | 诊断与监控中心 | Logs/Trace/Metrics、性能、异常、SourceMap、跨体系定位 | Foundation.Infrastructure + MRK/MDS/MNM等原 Owner | 实/局 | 公共诊断中心 + 上下文跳转 |
| P12 | 发布、部署与服务中心 | 发布版本、Host装载、生命周期、健康、守护、恢复 | Runtime.Host + Services.Supervision + 现行构建发布Owner | 局/目 | 发布/服务/运维工作区 |
| P13 | 安全、权限与审计 | 身份、授权、审计、敏感数据、设备安全边界 | 各现有 Owner 的跨切面策略；无新增万能安全真源 | 局/目 | 各中心一致应用，不建第二命令通道 |
| P14 | 测试与验证中心 | TestCenter、正反回归、场景Case、性能、CI、UIA/HIL证据 | Tests/TestCenter / ArchitectureSmoke / 各专业测试 | 实/局 | 独立应用入口及证据回链 |

上述 P02/P03 与 P10 是“**管理编辑中心**”和“**资源库**”的不同视角：Content/Symbol 中心负责组成和编辑；P10提供跨工程的版本、包、模板、依赖与资产检索，三者不能各自保存第二份可写定义。

## 3. Content / Symbol 的来源、加载、编辑、加入工程与共享（本次新增核心裁决）

### 3.1 两级身份与三种对象必须分开

| 对象 | 本质 | 谁拥有 | 例子 |
|---|---|---|---|
| **系统已注册的能力/类型**（Type/Capability Descriptor） | C#代码、配置 Schema、端口、属性、专业 Provider 的稳定契约 | 注册来源与现行类型/能力目录 Owner | 图像测量、按钮图元、设备状态组件 |
| **Content / Symbol 独立定义** | 使用这些系统能力**编辑组合后产生的可复用资源定义** | MEM/MPM；独立资源版本与库来源受控 | 工艺步骤组合 Content、报警 Symbol |
| **Project 内的引用/使用实例** | 引用定义 + 位置/参数/绑定/权限等实例覆盖 | 当前 MEMProject 的引用及配置绑定 | LineA 使用某报警组件，LineB 另配参数 |

**不能将“系统注册的类”直接等同于某个 Content/Symbol 工程定义；也不能将一个引用实例反向写坏共享定义。**

### 3.2 源头与装配路径（定案目标）

```text
专业代码 / 公共代码（类、算法、图元、服务）
    └─ 已有注解/显式Descriptor/Provider注册（按类别）
         └─ 统一类型/能力目录投影（TypeId、CapabilityId、Schema、Version、Ports、Properties）
              ├─ Content 管理中心：加载 → 拖选能力 → 组合逻辑/数据/可视/协同 → 预览/验证
              │     └─ 保存为可复用 MEMContent 定义/版本
              └─ Symbol 管理中心：加载 → 拖选图元与能力 → 组合Group/Element/属性/交互
                    └─ 保存为可复用 MEMSymbol 定义/版本
                                  ↓
                         资源库 / 导入导出 / 依赖校验
                                  ↓
                        在 Project 中“添加 / 引用”
                                  ↓
                    实例配置、绑定、授权、必要的覆盖
                                  ↓
                         共享调用 / 构建 / 运行
```

**“加载”必须区分**：(a) 装载已注册的类型/能力描述；(b) 导入/打开可复用 Content/Symbol 定义包；(c) 在 Project 内创建引用/实例；(d) Runtime 实例化与执行。不能将四者塞进一个“新建”动作，也不能把装载等同于设备运行就绪。

### 3.3 Content 中心：核心职责

- 加载**已注册**的可复用能力及 Content 定义，按系统/专业、类别、能力、版本、来源、兼容性检索。
- 在中央编辑器中组合逻辑、数据、视觉及协同方面；遵守现有 MEMContent/MPM/MOL 组织约束，不恢复第二份 Graph 持久化真源。
- 编辑对外属性、公开端口、参数/配置契约、引用依赖，支持校验、预览、版本比较、导入导出。
- 向 Project 添加已有定义或引用；默认保持定义与使用位置分离，按版本/共享策略决定是否复制或固定引用。
- **项目内二次配置**：通过已有 MEM 属性/绑定、MHS 配置身份与专业参数进行覆盖；不直接修改共享定义本体。运行时由 MOC/Decision 验证、MRK 装配。

### 3.4 Symbol 中心：核心职责

- 浏览系统注册图元/组件类型及已保存的 Symbol 定义，支持图形预览、属性、样式、行为/绑定发现。
- 在原有 **MEMSymbol → RootGroup → MRCGroup/MRCElement** 结构中编辑视觉组合和相应属性/交互；RootGroup 唯一，不将 SymbolReference 作为任意 Group 孩子。
- 组合生成复用 Symbol；能通过稳定身份/版本引用到多个 Page/Content，使用实例独立配置样式、绑定与参数覆盖。
- 设计时编辑共享定义须明确提示影响范围；若只改单个使用位置，则只保存覆盖或派生定义，不隐式改全部引用者。
- 图元/组件的“行为”来自现有正式能力/命令/消息/绑定契约，不在 Symbol 中造一套运行引擎。

### 3.5 已核实的真实基础及未完成边界

| 真实源码 | 已能说明什么 | 不能据此声称 |
|---|---|---|
| Mechanism/Core/Engineering/MEMNode.cs | MEMProject、MEMContent、MEMSymbol、Page.RootContent、Symbol.RootGroup、Resources、Reference | 已有独立平台级 Content/Symbol 管理中心 |
| Mechanism/Core/Engineering/MPMTypeDescriptor.cs | IMPMTypeProvider、MPMTypeDescriptor、RegisterTypes、AddTypedContent、AddTypedSymbol；**类型注册当前依附工程 Session** | 已实现注解自动扫描所有程序集并统一绑定真实执行 Provider |
| Mechanism/Core/Engineering/MTIPackages.cs | 资源包 Export/Import/Template、ExtractReusable 的既有入口 | 已完成远程版本库、依赖锁、所有跨工程共享场景 |
| Mechanism/Core/Engineering/MPMDefinitionState.cs | 现有 Content/Symbol 的属性、端口、连接、映射、Aspect 定义 | 已有无需验证即可动态执行全部专业功能 |
| Mechanism/Core/Modules 与 Mechanism/Decision | MFM模块/能力关联及已有 MOC 描述/静态发现 | 注解→Schema→Capability→Runtime的单次注册完全一致 |
| FCMConfigSectionAttribute 等已有配置注解 | 配置 Schema 可由明确注解/类型构建 | Content/Symbol 的通用能力注解自动发布已实现 |

**架构要求**：以现有 IMPMTypeProvider/MPMTypeDescriptor、MFM/MOC、FCM 属性、专业 Provider 为基础**归一系统类型目录的只读投影**；如需补注解/注册辅助，先核对已有契约，绝不再建平行 TypeRegistry 或 CapabilityCatalog。

### 3.6 关于“加入 Project”的两条合法路线

| 路线 | 适用情况 | 规则 |
|---|---|---|
| **引用 Shared Definition（优先）** | 同一个版本的 Content/Symbol 多项目复用 | Project 保存稳定身份、来源、版本/指纹、实例覆盖与依赖；源定义变更不得无提示影响已发布工程 |
| **导入独立副本** | 需要离线、定制、独立维护或不兼容版本 | 走 MTI 映射/冲突校验/明确拷贝语义；新身份、依赖、版本可追溯 |

**不得让 Project 直接“持有外部加载器或 CLR 工厂引用”作为工程真源；工程文件保存的是已批准契约、资源引用、绑定/版本，不是运行委托或运行内存。**

## 4. 数据与交互：工程入口 + 四个独立中心（本次新增核心裁决）

### 4.1 两种入口必须并存

1. **Project → 数据与交互定义**：当前工程可持久化的**声明、定义、引用和绑定**，包括变量定义/点位映射引用、消息类型与 Topic 约定、命令 Schema 与 Target 绑定、专业数据输入输出契约及必要的 Heap 使用策略。
2. **平台 → 数据与交互中心**：四个各自可独立进入的能力中心，按 Host/Project/Scope/Run 切换真实实例，完成维护、观察、查询、调试、诊断；从专业工作台和工程资源可直接跳转。

**四中心**：
- **变量中心**：定义→订阅→实际值/前值/质量/来源/Revision/变化历史→Provider/Mapping；默认只读，授权才可写运行变量。
- **运行数据中心**：Heap/对象定位/预算/所有权/Move-Copy/文件加载状态；DataStack/Frame/节点 Local/Pending/Committed 仅在对应 Run/Flow 调试上下文展现。
- **消息中心**：Topic/Schema/发布/订阅/过滤/队列/投递结果/取消订阅/诊断；小消息与大数据引用分开。
- **命令中心**：定义/参数/目标/授权/调用/Operation/执行结果/现场确认；不能把消息发布或变量赋值视为设备命令成功。

### 4.2 不进入工程树的运行数据

| 可持久化到工程的内容（按当前 Owner） | 不得作为普通工程资源直接持久化的内容 |
|---|---|
| 变量定义、绑定来源、配置身份、质量策略声明 | 高频变量当前值、真实设备活跃连接 |
| 消息类型/Topic 约定、订阅策略定义 | 正在排队的消息、订阅回调及临时投递状态 |
| 命令 Schema、目标引用、输入来源、执行策略定义 | 活跃 Operation、设备执行句柄、现场回执 |
| Flow 端口与 Data Binding、Heap 生命周期策略定义 | 当前 Heap/Stack/Frame 内容、临时 Image/Volume Buffer |

**特别裁决**：现有 MDSDataStack 和 FlowHeap 属于 Flow/Run 所有权语义；“数据中心全局可用”不能推导出“全进程只有一个共享可写 Heap”。工程级/Host级共享大数据若确有业务需求，必须单独定义 Owner、Scope、生命周期、访问控制、预算及跨进程桥接，而不是简单更名 FlowHeap 为 GlobalHeap。

### 4.3 Studio 的统一上下文与跨中心联动

- 顶部选择 **Project / Host / Scope / Run**，状态显示“未连接、只读、可编辑、运行中、Stale/Unknown”等真实来源。
- 工程资源上右键“打开数据/消息/命令定义”，运行节点上右键“查看绑定/数据/Trace/Operation”，从失败的 Operation 跳到相关命令与消息。
- 共用 CorrelationId、OperationId、RunId、ScopeId、ResourceId 与 Trace/SourceMap（适用时）；不能因“同名”把两个运行实例的数据汇在一起。
- Studio 只是 Host 实例的投影与可授权操作入口；**不创建第二个 MDSVariableRegistry、MNMMessageCenter、MRKCommandCenter 或 MDSDataHeap 作为业务真源**。
- TestCenter 的演示/测试实例不得冒充 Studio 的真实生产中心。跨进程和跨 PC 桥接必须独立标出验收等级。

## 5. 其他公共中心的入口与 Project 关系

| 公共中心或体系 | 平台入口必须具备 | Project 入口允许持久化 | 不应该做 |
|---|---|---|---|
| 工程资源中心 | 新建/打开/保存、查找、版本/引用/依赖 | 整个 MEMProject 定义、资源清单与事务 | 第二棵可写工程树 |
| 统一配置中心 | 统一 Section/Scope 浏览、Schema、Saved/Effective/历史、审批/回退 | 配置引用、工程层覆盖、版本身份 | 复制 MHS 配置仓；Saved 自动变 Effective |
| 功能模块中心 | 五类模块与能力目录、可用性/兼容性、Provider 来源 | 模块/能力引用与参数绑定 | 新增模块执行器或万能 ModuleManager |
| 公共工具中心 | 搜索纯函数、示例与适用范围，必要时“添加为能力” | 仅持久化实际使用的参数/表达式/能力引用 | 把每个 static 方法都做独立 Project 节点 |
| 决策与构建中心 | 定义验证、冲突说明、构建/版本、规划建议 | 可发布的定义与构建引用/证据；由原 Owner 决定 | MOC/Decision 直接持有第二工程定义 |
| 运行中心 | Host/Run/Task 状态、启动/停止/排空、资源/异常 | 部署/运行策略与目标引用 | 将活跃线程、Lease、Session 直接序列化为工程定义 |
| 诊断中心 | 日志/Trace/Metrics、过滤、关联、SourceMap 定位 | 可选诊断策略及证据引用 | UI 本地缓存变成唯一日志真源 |
| 发布与服务中心 | Host/版本/阶段激活/健康/恢复 | 发布目标、版本/依赖、激活策略 | Studio 关闭导致现场生产停止 |
| 安全与审计 | 角色/Scope/审计、敏感字段遮盖、授权轨迹 | 安全策略/身份引用，敏感值使用 SecretRef | UI 绕过 Guard、日志泄露密钥、以软件取消充当硬件急停 |
| TestCenter | 案例与运行历史、自动化证据和反例 | 正式 Case/验证制品及其引用（须遵从现有实现边界） | 把示例按钮点击当作自动测试或 HIL 通过 |

## 6. Studio 信息架构：公共入口与工程树不是一回事

**建议保持 1 Platform + 6 Professional Workbenches**，平台提供用户功能中心，不将横切体系升格为第七个专业 Workbench。

```text
CxVisual Studio
├─ 顶部：Project / Host / Scope / Run 上下文 + 搜索 + 当前身份权限
├─ 平台公共入口
│  ├─ 工程资源管理
│  ├─ Content 管理中心 / Symbol 管理中心（分别可直达）
│  ├─ 数据与交互中心 → 变量 / 运行数据 / 消息 / 命令（分别可直达）
│  ├─ 配置中心 / 功能模块中心 / 资源与资产库 / 工具中心
│  └─ 决策构建 / 运行 / 诊断 / 部署服务 / TestCenter
├─ 专业：Automation / HMI / Vision / CT3D / Inspection / Simulation
└─ 工作区：AvalonDock 中央文档 + 工程资源树 + 属性/结构/输出/诊断面板
```

### 6.1 左侧 Project 资源管理器推荐目录（**导航投影，不改 MEMNode 类层级**）

```text
[Demo Project] MEMProject
├─ 页面 / Pages
├─ 共享内容 / Contents             ← 来自 Content 中心导入/创建，Project 引用或独立定义
├─ 共享组件 / Symbols              ← 来自 Symbol 中心导入/创建，Project 引用或独立定义
├─ 流程 / 逻辑资源                 ← 类型视图；保存于 MEM/MOL 批准的资源/Aspect
├─ 设备与功能模块                 ← 设备/能力的工程定义与引用
├─ 数据与交互定义                 ← 变量、消息/命令契约、Flow数据绑定
├─ 工程配置与绑定                 ← MHS Section 引用与工程覆盖
├─ 资源库与外部资产               ← 包、模板、模型、文件、依赖
└─ 用例与验证资源                 ← 按正式 Case 资源契约展示
```

**限制**：当前真实 MEMProject 子节点只直接接受 MEMPage/MEMContent/MEMSymbol（另有资产引用），其它显示分组由分类索引、资源身份及能力/Aspect 投影实现。不能为了目录美观把 MEMNode 直接改成九类根子节点；真正新增独立资源种类须经 MEM Owner 审查和数据迁移裁决。

### 6.2 三种操作都要可用

- **浏览中心 → 添加到 Project**：浏览平台能力或共享制品，选择引用/导入、版本、依赖、目标 Scope、初始配置。
- **Project 资源 → 打开来源中心**：定位 Content/Symbol 的注册来源、类型、共享版本及依赖；数据定义定位到对应变量/消息/命令中心。
- **编辑运行 → 回到来源定义**：从 Content/Symbol 编辑器或 MRK Trace 找到公开端口、Capability/Schema/配置引用；不得因 UI 便捷跳转绕过编辑事务/授权。

## 7. OOP / GoF / AOP 的最小架构规则

- **OOP**：现有 MEMProject、MEMContent、MEMSymbol、MPMTypeDescriptor、MRIResourceReference 等保持各自职责；定义/引用/运行实例不以继承关系混成一个万能类型。中心 ViewModel 只持只读快照/授权操作合同，不拥有业务资源。
- **GoF**：**Composite** 用于 Content/Group 的合法树组合；**Factory/Registry** 复用现有类型 Provider 与 Descriptor 装配，不重复注册；**Strategy** 用于版本选择、引用/复制、专业参数解析；**Observer** 用于变量/消息变化的可取消订阅；**Facade** 用于 Studio 导航与便捷调用而非第二业务中心；**Command** 模式不能取代 MRK 真实命令受理和授权。
- **AOP/横切关注点**：身份/Scope/Generation、权限、审计、版本、异常/超时、性能指标与 Trace 应集中复用既有机制；每一个中心/专业都必须遵守一致切面，不复制 UI 中的简易安全检查作为最终权限来源。
- **依赖方向**：Foundation → Mechanism → Professional；Studio 可组合已有服务，Mechanism.Data 不依赖 WPF 或专业 DLL，Foundation 不依赖工程运行状态。需要新接口时优先核实原接口能否扩展。

## 8. 需求与验收门禁（平台级目录的实际完成定义）

| ID | 验收要求 | 必须提供的证据 | 反例/失败保护 |
|---|---|---|---|
| CAT-01 | 14 类公共入口有唯一导航；关闭/重开布局仍正确 | Studio 真实 WPF/UIA | 相同 ContentId 或未知页面造成布局破坏 |
| CAT-02 | 工程资源树只是一份 MEMProject 的投影 | 保存/重开、Revision、Undo/Redo、资源身份测试 | UI 维护第二套写入目录或变更 Revision 不一致 |
| CAT-03 | Content 中心从已有类型/定义装载、组合、保存/导入、引用/复制、覆盖 | 固定版本/ID/依赖/端口/Aspect 测试 + Studio UIA | 引用实例误改原定义、引用环、丢失 Provider |
| CAT-04 | Symbol 中心加载图元/组件、视觉组合、共享、覆盖 | RootGroup/引用/视觉编辑/重开/版本测试 | 把 Group 当业务容器或共享 Symbol 被意外覆盖 |
| CAT-05 | 显式 Descriptor/Provider 与现有 FCM/MPC/MFM/MOC 类型合同一致 | 静态类型/端口/Schema/执行绑定审计 | 重复 TypeId、同 Id 异 Schema 静默覆盖；冒称注解自动发现已实现 |
| CAT-06 | Project 数据定义和四个运行中心双入口可用 | 变量/Heap/消息/命令各自端到端 UIA+Smoke | Project 树存实时队列/图像 Buffer |
| CAT-07 | 同名变量、Topic、命令在不同 Host/Scope/Run 正确隔离 | 作用域、权限、订阅释放、迟到响应、重连反例 | 静态全局单例串数据 |
| CAT-08 | 配置 Saved/Effective/Pinned 严格分离 | 保存/生效/任务快照/回滚正反用例 | 保存即改变正在运行任务 |
| CAT-09 | 纯静态工具无需资源实例，需编排时复用 Capability | 直接 C# API + Studio 能力调用测试 | 为每个算法强制生成模块/节点 |
| CAT-10 | 跨中心跳转可从异常定位到原定义/Run/Trace | 项目资源→绑定→Operation→SourceMap 联动验收 | 展示无来源或无法定位的伪 Trace |
| CAT-11 | Runtime 与 Studio 生命周期隔离 | 关闭 Studio/断连后 Host 按已发布计划运行 | UI 关闭使工控生产依赖中断 |
| CAT-12 | 清楚区分设计、源码、CI、UIA、实机 HIL、长稳证据 | 固定 SHA、测试命令、Run URL、日志及局限 | 有类/有按钮即宣称“工业完成” |

工程权限、控制硬件和安全联锁的完整验收必须独立验证；Windows CI、Studio 软件测试与真实设备 HIL 不能互相替代。

## 9. 建议的实施次序（仅提供依赖顺序，不替代开发任务）

1. **G1 公共总目录与导航映射**：把本目录作为平台参考基线；不先改底层对象树。
2. **G2 Content/Symbol 中心复用与分层**：核实类型注册/资源包/引用/编辑链，先把真实已有能力用起来，缺项再增量补齐。
3. **G3 数据与交互四中心统一体验**：先绑定真实 Variable/MDS/MNM/MRK，支持工程定义双入口和 Scope 选择，再完成联动。
4. **G4 配置/模块/静态工具协作**：归一搜索和“添加到工程”动作，但不建立新 Owner。
5. **G5 运行/诊断/发布/TestCenter 证据闭环**：工业多机、多 Scope、多专业案例，按实际 CI/UIA/HIL 等级验收。

**第一优先级不是创建更多框架类，而是打通“已注册能力 → 可管理的 Content/Symbol → Project 引用与配置 → 专业运行 → 诊断追踪”这一条完整用户链。**

## 10. 与现行权威文档的关系

1. [平台灵魂与架构宪法 V1.0](../01_CxVisual_平台灵魂与架构宪法_V1.0_Review.md)：Platform+6 Workbenches、Resource/Embedded、跨专业边界、Runtime独立性。
2. [工程组织完整设计 V1.2](CxVisual_工程组织体系——完整架构设计与开发使用指南_V1.2.md)：MEM 唯一根、Content/Symbol、引用/事务/版本及用户模型。
3. [统一功能模块总纲 V2.0](CxVisual_统一功能模块体系%230总体架构——完整设计与验收_V2.0.md)：五类模块、Descriptor、Capability、专业装配。
4. [Decision 专业能力声明与注册 V2.0](CxVisual_统一决策与规划体系%233专业能力声明与注册_V2.0.md)：注解/Fluent/Descriptor 的目标及现状差距。
5. [统一配置体系 V2.4](CxVisual_配置体系——完整设计与验收 V2.4.md)：FCM/MHS Scope、Schema、Saved/Effective/TaskPinned。
6. [数据与交互 #0 总纲 V1.5](CxVisual_数据与交互体系%230总体架构——完整设计与验收_V1.5.md)及 #1变量 V1.1、#2运行时数据 V3.0、#3消息 V1.7、#4命令 V1.0：四中心各自唯一 Owner。
7. [统一运行体系 #0](CxVisual_统一运行体系%230总体架构——完整设计与职责划分_V1.0.md)、[运行诊断](CxVisual_运行诊断体系%23监控与调试——完整设计与验收_V1.0.md)、[部署生命周期](CxVisual_运行治理体系%23部署与生命周期——完整设计与验收_V1.0.md)：部分仍为讨论稿，其历史 Lifecycle 项目归属不得覆盖当前已取消独立项目的裁决。
8. 项目协同/实际证据：[DOCS/00](../00_CxVisual_协同开发入口与七仓复用基线.md)、[AGENTS.md](../../AGENTS.md)、[开发 Skill](../../.agents/skills/cxvisual-engineering/SKILL.md)、DOCS/Implementation 与 DOCS/Testing。

## 11. 后续决策问题与边界

以下是**需要实施时验证并可能形成 ADR 的问题**，本目录不预先发明最终 API：

- 内容/组件跨 Project 的共享库是仓库文件包、工程包、外部资产库还是多种组合？先以现有 MTI 能力与稳定版本引用实现最小闭环。
- 系统注册目录与 Project 内 MPMTypeDescriptor 怎样同步 Schema 和版本？现有 RegisterTypes 是工程 Session 内事务，跨工程平台目录尚需明确唯一来源与可用性。
- Content 与 Symbol 的“中心加载”有哪些资源包/Provider；用户可否在项目外编辑未加入任何 Project 的制品？若需要，必须清晰定义独立定义包的唯一持久化 Owner，不允许 Studio 私下持有永久可写真源。
- 旧文档里的 Mechanism.Logic/Orchestration/Lifecycle 以及 Runtime 依赖图，需按当前 Mechanism.Decision 与已取消独立 Lifecycle 的真实工程更新，不因此重做已完成的结构合并。
- DOCS/02～04 现行为 XLSX 目标基线；本次未完成其每张 Sheet 的逐行对照，正式新增类型/Owner/API 之前必须按具体子方面核实并记录冲突。

---

**版本记录**：V1.0（2026-10-30）——建立平台公共能力总目录，落实用户新增 Content/Symbol 独立管理中心与代码注册组合路径、Project 二次配置和共享、数据与交互工程入口与独立中心双入口；将现行代码依据、目标和待验证内容分开。
