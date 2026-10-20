# CxVisual 工程构建体系——长远架构收敛、完整类/接口/枚举归位与三阶段开发计划 V1.2

> **用途：下一次直接开始编码的设计/执行基线；当前交付是文档，不是本轮代码已经实现。** 版本：2026-10-19。
>
> 依托文档：V1.0《全量类接口枚举归位设计与代码状态》、V1.1《长远架构问题与详细完成方案》、V0.9《全量需求与案例源码实施矩阵》；保留235个已有相关类型的完整源码归位、原82个候选的逐项处理及32方面需求映射。
>
> 最新在本次阅读中**定向核对**的 x3 master 基线：`f227d8c56dc8fe901f8ca05e62364a4bce1f3682`（2026-10-18）；[代码仓库](https://github.com/oudream/cx-visual-x3/tree/f227d8c56dc8fe901f8ca05e62364a4bce1f3682)。若下次开始编码时 master 已前移，必须先更新类型与契约差异，再开发。旧版x2仅为复用参照，不能以旧实现覆盖最新架构。
>
> **验证边界：** 本文中的“已声明”仅说明源文件中存在类型；“已实现”仅对应具体读取到的行为；“CI通过”只对应匹配 SHA 的工作流，不等于全部构建功能、Studio 人机操作、专业设备或现场验收。四大架构文档指定版本 V4.9/V2.01/V6.4 尚无完整逐表一致性核验，不冒称已冻结全部专业需求。
>
> **范围：** 工程构建体系 = 工程组态 + 自由编排 + 验证/编译/运行接续；只协调现有工程组织(MEM)、配置(MHS)、类型与端口(MPM/MPC)、逻辑(MOL)、编译(MOC)、数据(MDS)、执行(MRK)、视图/Studio、专业 Provider。不新增项目，不建立第二权威工程树、不新造 Runtime、不复制数据堆栈。

---

## 0. 本次修订摘要与完成判定

| 项目 | V1.1 问题 | V1.2 执行结论 | 对代码的含义 |
|---|---|---|---|
| 自由连线 vs 结构化 | 曾以结构化控制块为执行定义、连接为投影 | **自由连线是统一基础能力和新增模型的定义事实；结构化是合法连线的语义分析/具体化结果** | 新版持久 MOL 增加块端点/连接；MOC从连接分析出 Region/控制计划；旧块树迁移兼容 |
| Symbol / Content | 边界模糊 | **Symbol 是单一主要逻辑值的呈现/交互者；Content 是可复用组合** | 不必改变MEM继承；为Symbol设 PrimaryValue 语义，其他事件/命令端口合法 |
| Port 单值或组合 | 单值、数组、多端口混淆 | **一个 Port = 一个明确类型的契约；一次值可为标量/结构/集合/流/Lease** | 保持现有 MPCPort，连接条数 `MPCPortMultiplicity` ≠ 值的形态 |
| 数据传递与流程复用 | 容易将引用理解为数据引用 | **引用/调用的是定义；执行数据按Port映射进入本次MDS Frame；共享大对象按受控资源键领取** | 不在工程中保存运行数据，不复制 MDSFrame，不跨实例泄漏 |
| 专业能力注册/发现 | IMPMTypeProvider被误当成一切能力目录 | **聚合现有类型描述 + 专业注册/执行 Provider 的只读检索视图** | 可发现/可配置/可解析同一身份与版本；不另存第二能力库 |
| 编辑冲突 | 拟议“自动处理冲突” | **不允许带冲突提交；版本不匹配/无权限/断端口/不兼容引用即原子拒绝** | 完整复用 MEM/MTC 事务、预计版本、撤销/重做；不自动合并 |
| 版本与调试 | BuildPlan身份不全 | 工程/能力/依赖/资源配置版本固定，Trace始终映射原构建版本 | MOC SourceMap + MRK 既有发布/运行契约；后续Lifecycle发展 |
| 开发时间 | G00~G08是平台路线 | **先做 3 × 约30分钟的首个自由连线可执行薄切片**；余下另有完整路线，不得冒称90分钟完成全平台 | 每段仅提交一个可验证闭环；超时停止、记录阻塞而非绕测试 |

**状态准则：** [E] 已有源码声明；[P] 已有部分行为；[R] 本轮增强已有；[N] 仅在证明必要时新增；[D] 延期；[X] 取消/并入。所有带 [N] 的名字都是候选，不是已经存在的类。

## 1. 已收敛的长期架构原则（A1～A10）

**A1 唯一工程真源。** Project/Page/Content/Symbol/Group/Element 仍由 MEM 统一组织；已注册资源有稳定身份、作用域、引用及存储；视觉图元不直接表示业务执行对象。

**A2 Symbol = 单主要值的呈现/交互定义。** 它可以由 RootGroup + 多个图元实现复杂外观，并支持按钮/报警等附加事件、命令或元数据端口；所谓“单值”是**对外主要绑定语义**，不是“一个图元”或“只许一个物理Port”。主要值可以是复合类型/设备状态等；不破坏旧Symbol属性和公开映射。

**A3 Content = 多对象多能力的复用组合。** 可组合 Content、Symbol、Group、数据绑定、逻辑流程与专业能力；可无可视界面、可无 Logic。ResourceRef 与 Invocation 关系必须分开；Content 是用户级主子流程/复杂控件的复用边界，不要求为每个能力新建MEMNode子类。

**A4 自由连线优先，连接具有执行语义。** 工程构建提供连接、端口、类型、方向、事件/状态/数据/控制语义、约束和校验。画布是相同连接定义的**可编辑投影**，不是独立Graph数据库。新式自由连线的权威属于其所在 Content 的正式 Logic 定义，布局不改变语义。

**A5 结构化是自由连线的“合法具体化”。** 一张图不是自动得到合法程序：ControlFlow 合法可归约为 Sequence/If/Loop/Parallel/Region；不可归约或歧义连接给出确定错误/要求明确语义；DataFlow/Workflow/StateMachine 以各自规范 IR 编译，不强制伪装成同一 AST。**“静态化”指固定设计时可确定的拓扑、契约和编译计划，不等于把百万轮运行展开为百万个 Step。**

**A6 Port = 一个类型化传输/控制契约。** 值项可以是简单值、组合值、集合、流、受控数据句柄；`MPCPortMultiplicity` 仅是可连接数量。层级公共端口(资源级)和逻辑块端口(内部)共享基础类型语义，但每条关系仅在一种所有权范围内持久化一次。

**A7 数据驱动 = 数据可用 + 控制条件成立 + 资源/权限可用 + 事件/触发生效。** 允许引用子流程定义，却不把产品图像/SN/运行局部变量作为工程定义引用。每次 Invoke/Iteration 独立身份+MDS Frame；全局设备/配方/模型/大对象由资源中心按键、权限、租约领取。大图数据避免不必要复制，用户数据不能写入Logic Aspect/Trace。

**A8 专业注册应一处真实实现，多处发现使用。** 定义发现、版本/类型描述、编辑器属性、端口签名、Provider绑定/仿真要求。Studio目录是只读聚合，不与`IMPMTypeProvider`、`IMRKCapabilityResolver`、Professional Provider保存两套冲突元数据；构建版本固定，缺实现或不兼容即拒绝。

**A9 工程修改必须零冲突提交。** 预期Revision匹配、合法性/接口/引用检查通过、权限正确，整组改动才原子落地；失败全部回滚。撤销/重做也需重新验证当前版本，绝不静默合并、覆盖。已运行实例不随Studio修改被换定义。

**A10 运行/调试/发布各归其位。** MOC校验与具体化，MRK唯一实际运行与受控调试，MDS数据生命周期，Visual/WPF/Studio双态展示，Lifecycle管理冻结与受控部署。构建物/SourceMap绑定工程快照和Provider版本，既有任务锁定自己的版本。

### 1.1 Symbol、Content、Port 示例

```text
Project 工程
├─ Page 监控页面 (RootContent)
│   └─ Content 温控面板 [复合控件]
│       ├─ Symbol 实时温度 [PrimaryValue = double, ℃]
│       │   └─ RootGroup → 数字 / 仪表 / 状态灯 Element
│       ├─ Symbol 设备状态 [PrimaryValue = DeviceState]
│       └─ Content 调温流程 [可执行复用组合]
└─ Content 检测主流程
    ├─ Invoke → Content 采图子流程（引用定义）
    └─ Invoke → Content 算法子流程（引用定义）
                 输入[SN, ImageFrame(Lease), Recipe] → 输出[InspectResult]
```

`InspectResult`可以具有`IsOK/Defects/Score/ImageHandle`字段；**从数据视角仍是某个Port的一项组合值**。如需映射字段，使用类型合法的字段选择/绑定，不复制另一个独立“子端口对象树”。

## 2. 两级连线统一方案（架构核心）

### 2.1 为什么不把每个逻辑块变成MEMNode

`MEMNode`是工程资源/拥有关系树；`MOLControlBlock`当前有稳定`Guid Id`，但不继承MEMNode。直接让每个算法步骤变成工程子节点，会制造Graph平行树、复制编辑事务、导致每个动态控制步骤都出现重量级工程所有权；不采用。

**统一的是连接操作/类型约束/诊断/编排体验，不强制所有场景使用同一个存储类：**

| 范围 | 权威连接记录 | 端点身份 | Owner | 对外公共契约 |
|---|---|---|---|---|
| 工程资源/可复用组件公开接口 | 既有 `MPCConnection`、`MPCPort`、`MRIObjectAddress` | Resource / Reference instance + PortId | MEM/MPM (Core) | Content/Symbol对外公开Port与映射；定义/实例不混淆 |
| Content 内部自由逻辑连接 | 建议 `MOLLink`（原候选`MOLDataLink`的**泛化改名**） | 建议 `MOLBlockEndpoint`：BlockId + PortMemberId | Content的**一个** Logic Aspect (Core) | 复用`MPCPortKind`、`MPCPortDirection`、类型ID、单位/坐标/时间契约与校验 |
| 可视线条 | Visual布局投影（延伸线、折线路由） | 逻辑Link ID/Port ID | Visual (非语义源) | Move/Zoom/Route只改变视图，不改变链接执行 |
| 运行数据关系 | MOC映射成MRK步骤输入/事件/调度 | PlanStep/Invocation/Iteration身份 | MRK/MDS（瞬时） | 不回写持久工程关系 |

这样**同一条连接不会同时存在MPC和MOL两个副本**：如果连接双方是资源级对外Port，归MPC；如果是内部自由步骤/控制信号，归MOL。跨边界仅经公开映射/显式Invoke适配。用户在Studio只看到一个Connect操作，底层根据端点种类进入正确事务。

### 2.2 逻辑块端点与连接最小必要契约（目标设计，不是现有API）

```csharp
// CxVisual.Mechanism.Core — 类型名称为正式建议，待实现
public sealed class MOLBlockEndpoint : IEquatable<MOLBlockEndpoint>
{
    public Guid BlockId { get; }           // MOLControlBlock.Id
    public string PortMemberId { get; }   // 例如 Image、Done、Failed、Out
    // ... 值相等、哈希、不可变身份
}

public sealed class MOLLink
{
    public Guid Id { get; }
    public MOLBlockEndpoint Source { get; }
    public MOLBlockEndpoint Target { get; }
    public MPCPortKind Kind { get; }       // Control/Data/Event/State/Stream...
    public string ConditionId { get; }    // 如需分支，引用本定义中的稳定条件/规则标识
    public int? Order { get; }             // 仅当执行模型声明有顺序时有效
}
```

代码块是**规格草案**，下次按.NET Framework 4.8和仓库C#语言版本补齐合法构造器、字段校验、身份等值、序列化、字段可选规则及错误码后才能编译；不能把它作为现有源码。

**关键：** 如果同一个逻辑块存在多个输出，必须由该块/能力版本定义公开的PortSchema驱动连接；不能凭UI动态编造未经注册的端口。Connection可给出`PortMemberId`，Port的类型/方向/允许连线数要由能力类型契约决定。`Connection`表达的是有向关系，不承载每次运行实际数据。

### 2.3 MOL新旧持久化方案与兼容

- 当前事实：`MOLLogicCodec`使用ProviderId=`cx.logic.structured`、SchemaVersion=`1`，核心定义是`MOLLogicDefinition.Root`（结构化树）。`MOCEngineeringCompiler`能把这类定义展开成MRK计划，部分控制语义已通过CPU测试。
- 建议：**保持现有V1定义可读可编译，不直接就地改坏旧序列化。** 在同一个Content的同一个Logic所有权下提供**新增V2自由连接表示**（逻辑块记录与MOLLink集合）。V1结构化树可转换为V2显式连接；转换后持久化V2为唯一权威，不再额外保留会漂移的V1 AST执行副本。
- **推荐确定最小兼容策略：继续使用同一个 `(MPMAspectKind.Logic, ProviderId='cx.logic.structured')` 槽位，V1 为 `SchemaVersion=1`，V2 为 `SchemaVersion=2`。** 不创建第二个并存的Logic Provider，从源头避免一份Content有两个权威编排定义。`MOLLogicCodec.Get()`按Schema版本解析，旧V1始终可读可编译，明确转换后原子覆盖同一Aspect。
- V2 内容由`MOLLogicDefinition`扩展支持 `Blocks/Links/Entry`（或其内部不可变记录）承载；`Root`仅代表V1结构化模式/可重建IR兼容视图，不允许以V1 Root和V2 Links两份互相不同步的持久执行事实同时存在。**同一Content同一执行入口在某个Revision只能有一种权威序列化表示**。
- 结构化编译结果是可重建的 IR/MRK Plan，不另保存为第二份工程权威。MOC应输出诊断，标注不合法回路、缺分支终点、非法类型、无法确定汇合和未连接的必需Port。
- 修改布局不影响逻辑Hash；修改端口/连线导致正确Revision变化。支持迁移失败原文不丢；不允许碰到未知Schema时静默略过。
- 未来四模型均由此连接与类型机制支撑，但**DataFlow/Workflow/StateMachine必须有原生语义规则**；不可用单一if树冒充全部功能。

### 2.4 什么样的自由连线有合法执行含义

| 连线/形态 | 验证与编译动作 |
|---|---|
| A.Done → B.Start | 顺序依赖；生成可追溯的执行先后约束 |
| A.Image → B.Image | 类型兼容的数据传递；约束为Producer→Consumer |
| A.Done → B.Start 与 A.Done → C.Start | 若声明fork语义，可并行；否则必须显式选择分派行为 |
| B.Done、C.Done → D.Start | 按显式Join策略等待；不猜测AND/OR语义 |
| If.True/If.False →不同子图 | 必须有明确分支条件和收敛/退出语义 |
| Loop.Back → Loop.Entry | 只有显式Loop/Region契约、退出条件及预算才可合法反馈 |
| 普通DataFlow A↔B环路 | 若无State/Delay/Buffer策略则报环依赖，不能暗中变无限循环 |
| Event/State/Workflow links | 各按相应事件、迁移、等待和恢复规则验证，不能当顺序箭头简单执行 |

## 3. 注册、专业能力发现和完整Port契约

**事实：** 现有`IMPMTypeProvider.DescribeTypes()`返回Content/Symbol类型描述；`IMRKCapabilityResolver.TryResolve(id,out MRKResolvedCapability)`提供运行能力；目前两者不能自动组成完整工具箱。它们也不应互相伪装成对方。

**裁决：** 目标`IMOCCapabilityCatalog`（只读聚合与搜索）可做“注册Provider的视图”，统一稳定`CapabilityId`、Schema/ContractVersion、显示名/分类、输入输出Port、值类型/单位/时基、参数、运行模式/仿真、所需设备/权限、Provider定位与兼容状态。目录绝不存储可执行委托的第二副本或替代Runtime解析；编译时重新校验/固定提供者版本。

**建议契约关系：**

```text
IMPMTypeProvider (已有，Content/Symbol类型)
Professional Providers (专业定义与执行注册，需查真实接入情况)
IMRKCapabilityResolver (已有，编译期可执行解析)
              ↓
IMOCCapabilityCatalog + MOCCapabilityDescriptor (候选只读投影)
              ↓
C# Builder搜索/Studio工具箱/校验器/构建器
              ↓
固定实际Provider版本 → MOCEngineeringCompiler → MRK
```

Port语义必须验证`Kind`、`Direction`、`TypeId`、`Multiplicity`、必填性、契约的Unit/CoordinateFrame/TimeBase，以及输入/输出的单值/组合值/集合/Stream/Handle的Schema，兼容转换必须显式声明，不能靠字符串名称相等判定结构兼容。要区分**缺数据**、**数据类型不匹配**、**业务NG**、**能力执行失败**、**设备未知状态**五种错误/业务状态。

## 4. 编辑事务、零冲突、流程提取与数据驱动

### 4.1 零冲突提交

已有`MEMEngineeringSession.BeginEdit(string intent,int expectedRevision)`、`MTCEditDraft`、`MTCEditSession`、`MOLLogicCodec.Set()`及 undo/redo 机制应继续作为唯一写入口。

**必须避免的陷阱：** `MOLLogicCodec.Set(session,content,definition)`当前内部调用`session.SetAspect`，而`session.SetAspect`又会执行`Edit(...)`。将该方法直接嵌套调用在`Edit`回调内会形成嵌套编辑问题。可以提供`MOLLogicCodec.CreateAspect(definition)`这一纯函数或`MOLLogicCodec.Set(MTCEditSession draft,... )`合法重载，外部由 `session.Edit(intent, expectedRevision, e => e.SetAspect(content, aspect))`一次原子提交。

**提交门槛**：检查expectedRevision（含资源/文档共享编辑场景）、授权、唯一BlockId/LinkId、端点存在、类型/多重性、引用契约、旧实例锁定、序列化预算。任何失败不得改变revision、原节点、Port、资源索引及历史；整个事务一次Undo恢复整组状态。无自动合并；用户获得确定的冲突信息并选择重新加载/重做。

### 4.2 逻辑提取复用（复用MTI不重复造Extractor）

- 现有`MEMEngineeringSession.ExtractReusable(roots, owner, name, remapper)`的**实际约束**是 `roots` 为Owner的直接MEM子节点；`MOLControlBlock`不继承MEMNode，故这个现有方法不能直接作为逻辑块选区提取的完整实现。
- 构建体系在MOC/Visual发出选择区域、端口推导、边界输入输出预览，Core/MTC在同一事务内实现：创建复用Content、移动/复制块与内部连线、公开输入输出、原位置替换为显式Invoke、修复外部Link、可撤销。
- **引用定义 + 运行数据具体化**：两个Invoke引用同一Content可以使用不同参数、SN和不同MDS Frame；运行数据不随定义共享。跨资源ContentRef、触发Trigger和Invoke运行调用不得混淆。

### 4.3 数据驱动及资源领取

- 连接是**数据/控制的声明**，不是数据本身。MOC负责编译映射，MRK按条件、事件、调度、权限执行，MDS Frame持本次输入输出；子流程调用前验证必填Port已提供。
- 全局资源（设备、模型、配方、缓存、图像/体数据）通过稳定键向其Owner的资源/数据中心申请，按权限、Lease、作用域与取消释放；不直接给全局堆任何未授权读写入口。
- 大对象默认不进入Aspect/XML/Trace，必要时使用FDTReadOnlyBuffer、FCRSharedLease、MDS预算/堆已有能力，先性能测试再新增句柄类。SN与运行实例的Correlation单独保留。

## 5. 项目/类型的唯一归位规则

| 现有项目 | 永久Owner | 不应侵入 |
|---|---|---|
| `CxVisual.Foundation.Core` / `.Data` / `.Infrastructure` | ID/通用数据Schema/并发及Lease等基础 | 工程文档与专业设备会话 |
| `CxVisual.Mechanism.Core` | MEM组织、MOL唯一持久定义、MPC公开端口、编辑事务、Ref、文件版本 | Studio画布、MRK执行 |
| `CxVisual.Mechanism.Logic` | 纯规则/条件/模型合法性共享算法，确认需求再抽取 | 另一套持久MOL或内核运行引擎 |
| `CxVisual.Mechanism.Orchestration` | MOC连接验证/语义归约/计划桥接、SourceMap、只读能力目录、C#便捷API | 第二套工程树、Scheduler、状态堆 |
| `CxVisual.Mechanism.Runtime` | MRK唯一执行、计划、Region、Journal、调试/访问授权 | 持久Studio Definition、反向依赖MOC |
| `CxVisual.Mechanism.Data` | MDS Frame/Heap/数据绑定、运行态预算、按需租约 | 设备命令/流程编辑 |
| `CxVisual.Mechanism.Visual` | 交互命令、选择/布局/路由、与Core Session对接 | 把画布坐标做成执行真源 |
| `CxVisual.Mechanism.Presentation.Wpf` | WPF流程画布、拖入、连线命中、状态覆盖 | 自己管理工程树/Runtime |
| `CxVisual.Studio` | 工具箱/工程树/属性/连接/问题/运行观察/子流程导航 | 复制工程定义和运行状态 |
| `CxVisual.Mechanism.Connectivity` | 公共通道/协议/设备连接接口 | 取代具体专业Provider |
| `CxVisual.Mechanism.Lifecycle` | 构建物与依赖锁、受控部署与回滚 | 新调度器或数据栈 |
| `Professional.*` | 专业能力/设备/算法/编辑器扩展 | 平行MOC/公共工程模型 |
| `Runtime.Host` | 装配、授权受控执行、工作模式与Provider供给 | 专业能力第二数据库 |
| `Tests/ArchitectureSmoke` / `Tests/TestCenter` | 真代码测试、异常反证、样例与报告 | 以“类存在/截图存在”代替验收 |

## 6. 全量设计类/接口/枚举主干归位：235现有 + 原82候选全部保留可追溯

**重要口径：** 第6.1节完全保留V1.0中235个已有相关类型的逐类型表（带源码相对路径和声明状态），未擅自漏掉类型；第6.2节逐行保留V1.1的82个候选，并联接V1.0的形态/API/Gate。旧`MOLDataLink`改为通用`MOLLink`（候选重命名，非新增旧代码迁移），新增一个确有独立身份需求的`MOLBlockEndpoint`，因此目标候选**83项**，其中大量要合并/延期/取消，不是新增83个类。不要把原候选名称当作已经实现。

### 6.1 现有235个类型全量明细（逐个保留V1.0的原始归位）

> 只列与三个功能域及其直接依赖有关的声明，排除大量无关配置UI和单纯外部库类型。详细关键API与源码链接看Excel「01_已有源码类型」。


#### CxVisual.Foundation.Core（1个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `FOIStableId` | 结构体 | `Foundation/Core/FOIStableId.cs` | 已声明 |

#### CxVisual.Foundation.Data（4个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `IFDEExecutionDataScope` | 接口 | `Foundation/Data/Execution/FDEExecutionDataContracts.cs` | 已声明 |
| `FDTDataKind` | 枚举 | `Foundation/Data/Types/FDTDataTypeDescriptor.cs` | 已声明 |
| `FDTDataTypeDescriptor` | 类 | `Foundation/Data/Types/FDTDataTypeDescriptor.cs` | 已声明 |
| `FDTDataValueSafety` | 类 | `Foundation/Data/Types/FDTDataTypeDescriptor.cs` | 已声明 |

#### CxVisual.Foundation.Infrastructure（7个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `FCRWorkState` | 枚举 | `Foundation/Infrastructure/Concurrency/FCRWorkContracts.cs` | 已声明 |
| `FCROverflowPolicy` | 枚举 | `Foundation/Infrastructure/Concurrency/FCRWorkContracts.cs` | 已声明 |
| `FCRWorkerOptions` | 类 | `Foundation/Infrastructure/Concurrency/FCRWorkContracts.cs` | 已声明 |
| `FCRWorkResult` | 类 | `Foundation/Infrastructure/Concurrency/FCRWorkContracts.cs` | 已声明 |
| `FCRWaitObservation` | 类 | `Foundation/Infrastructure/Concurrency/FCRWorkContracts.cs` | 已声明 |
| `FCRWorkHandle` | 类 | `Foundation/Infrastructure/Concurrency/FCRWorkContracts.cs` | 已声明 |
| `FCRWorkerSnapshot` | 类 | `Foundation/Infrastructure/Concurrency/FCRWorkContracts.cs` | 已声明 |

#### CxVisual.Mechanism.Core（86个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `MEMNodeKind` | 枚举 | `Mechanism/Core/Engineering/MEMNode.cs` | 已声明 |
| `MEMResourceScope` | 枚举 | `Mechanism/Core/Engineering/MEMNode.cs` | 已声明 |
| `IMEMResource` | 接口 | `Mechanism/Core/Engineering/MEMNode.cs` | 已声明 |
| `MEMNode` | 类 | `Mechanism/Core/Engineering/MEMNode.cs` | 已声明 |
| `MEMProject` | 类 | `Mechanism/Core/Engineering/MEMNode.cs` | 已声明 |
| `MEMPage` | 类 | `Mechanism/Core/Engineering/MEMNode.cs` | 已声明 |
| `MEMContent` | 类 | `Mechanism/Core/Engineering/MEMNode.cs` | 已声明 |
| `MEMSymbol` | 类 | `Mechanism/Core/Engineering/MEMNode.cs` | 已声明 |
| `MRIReferenceKind` | 枚举 | `Mechanism/Core/Engineering/MEMNode.cs` | 已声明 |
| `MEMResourceEntry` | 类 | `Mechanism/Core/Engineering/MEMNode.cs` | 已声明 |
| `MRIResourceReference` | 类 | `Mechanism/Core/Engineering/MEMNode.cs` | 已声明 |
| `MTCEditResult` | 类 | `Mechanism/Core/Engineering/MEMEngineeringSession.cs` | 已声明 |
| `MTCChangeSet` | 类 | `Mechanism/Core/Engineering/MEMEngineeringSession.cs` | 已声明 |
| `MVEValidationReport` | 类 | `Mechanism/Core/Engineering/MEMEngineeringSession.cs` | 已声明 |
| `MTCMutation` | 类 | `Mechanism/Core/Engineering/MEMEngineeringSession.cs` | 已声明 |
| `MEMEngineering` | 类 | `Mechanism/Core/Engineering/MEMEngineeringSession.cs` | 已声明 |
| `MEMEngineeringSession` | 类 | `Mechanism/Core/Engineering/MEMEngineeringSession.cs` | 已声明 |
| `MRIUsage` | 类 | `Mechanism/Core/Engineering/MEMEngineeringSession.Contracts.cs` | 已声明 |
| `MEMProjectSnapshot` | 类 | `Mechanism/Core/Engineering/MEMProjectSnapshot.cs` | 已声明 |
| `MEMNodeSnapshot` | 类 | `Mechanism/Core/Engineering/MEMProjectSnapshot.cs` | 已声明 |
| `MPMPropertySnapshot` | 类 | `Mechanism/Core/Engineering/MEMProjectSnapshot.cs` | 已声明 |
| `MRCVisualSnapshot` | 类 | `Mechanism/Core/Engineering/MEMProjectSnapshot.cs` | 已声明 |
| `MRIReferenceSnapshot` | 类 | `Mechanism/Core/Engineering/MEMProjectSnapshot.cs` | 已声明 |
| `IMEMProjectStore` | 接口 | `Mechanism/Core/Engineering/MEMProjectStorage.cs` | 已声明 |
| `MEMSaveSnapshot` | 类 | `Mechanism/Core/Engineering/MEMProjectStorage.cs` | 已声明 |
| `MEMSaveResult` | 类 | `Mechanism/Core/Engineering/MEMProjectStorage.cs` | 已声明 |
| `MEMStoreConflictException` | 类 | `Mechanism/Core/Engineering/MEMProjectStorage.cs` | 已声明 |
| `MEMFileProjectStore` | 类 | `Mechanism/Core/Engineering/MEMFileProjectStore.cs` | 已声明 |
| `MEMAccessOperation` | 枚举 | `Mechanism/Core/Engineering/MEMAccessPolicy.cs` | 已声明 |
| `IMEMAccessPolicy` | 接口 | `Mechanism/Core/Engineering/MEMAccessPolicy.cs` | 已声明 |
| `MEMAccessRequest` | 类 | `Mechanism/Core/Engineering/MEMAccessPolicy.cs` | 已声明 |
| `MEMCloseMode` | 枚举 | `Mechanism/Core/Engineering/MEMAccessPolicy.cs` | 已声明 |
| `MEMAssetReference` | 类 | `Mechanism/Core/Engineering/MEMAssetReference.cs` | 已声明 |
| `MOLControlKind` | 枚举 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已声明 |
| `MOLBranchMode` | 枚举 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已声明 |
| `MOLLoopMode` | 枚举 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已声明 |
| `MOLExitKind` | 枚举 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已声明 |
| `MOLExpressionSource` | 枚举 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已声明 |
| `MOLComparison` | 枚举 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已声明 |
| `MOLExpression` | 类 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已声明 |
| `MOLBranchCase` | 类 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已声明 |
| `MOLLoopDefinition` | 类 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已声明 |
| `MOLControlBlock` | 类 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已有基础/未完备 |
| `MOLLogicDefinition` | 类 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已有基础/未完备 |
| `MOLLogicCodec` | 类 | `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | 已声明 |
| `MPCPortDirection` | 枚举 | `Mechanism/Core/Engineering/MPCPort.cs` | 已声明 |
| `MPCPortKind` | 枚举 | `Mechanism/Core/Engineering/MPCPort.cs` | 已声明 |
| `MPCPortMultiplicity` | 枚举 | `Mechanism/Core/Engineering/MPCPort.cs` | 已声明 |
| `MTUContractReference` | 类 | `Mechanism/Core/Engineering/MPCPort.cs` | 已声明 |
| `MPCPort` | 类 | `Mechanism/Core/Engineering/MPCPort.cs` | 已声明 |
| `MPCEndpoint` | 类 | `Mechanism/Core/Engineering/MPCPort.cs` | 已声明 |
| `MPCConnection` | 类 | `Mechanism/Core/Engineering/MPCPort.cs` | 已声明 |
| `MRCPublicMappingKind` | 枚举 | `Mechanism/Core/Engineering/MPCPort.cs` | 已声明 |
| `MRCPublicMapping` | 类 | `Mechanism/Core/Engineering/MPCPort.cs` | 已声明 |
| `IMPMDefinition` | 接口 | `Mechanism/Core/Engineering/MPMDefinitionState.cs` | 已声明 |
| `MPMAspectKind` | 枚举 | `Mechanism/Core/Engineering/MPMDefinitionState.cs` | 已声明 |
| `MPMAspect` | 类 | `Mechanism/Core/Engineering/MPMDefinitionState.cs` | 已声明 |
| `MPMDefinitionState` | 类 | `Mechanism/Core/Engineering/MPMDefinitionState.cs` | 已声明 |
| `IMPMTypeProvider` | 接口 | `Mechanism/Core/Engineering/MPMTypeDescriptor.cs` | 已声明 |
| `MPCPortDescriptor` | 类 | `Mechanism/Core/Engineering/MPMTypeDescriptor.cs` | 已声明 |
| `MPMTypeDescriptor` | 类 | `Mechanism/Core/Engineering/MPMTypeDescriptor.cs` | 已声明 |
| `MPMValueKind` | 枚举 | `Mechanism/Core/Engineering/MPMValue.cs` | 已声明 |
| `MPMValue` | 类 | `Mechanism/Core/Engineering/MPMValue.cs` | 已声明 |
| `MPMBindingKind` | 枚举 | `Mechanism/Core/Engineering/MPMValue.cs` | 已声明 |
| `MPMBinding` | 类 | `Mechanism/Core/Engineering/MPMValue.cs` | 已声明 |
| `MPMPropertyDescriptor` | 类 | `Mechanism/Core/Engineering/MPMValue.cs` | 已声明 |
| `MPMProperty` | 类 | `Mechanism/Core/Engineering/MPMValue.cs` | 已声明 |
| `MRCTransform` | 结构体 | `Mechanism/Core/Engineering/MRCTransform.cs` | 已声明 |
| `MRIPlacementState` | 类 | `Mechanism/Core/Engineering/MRCTransform.cs` | 已声明 |
| `MRCVisualItem` | 类 | `Mechanism/Core/Engineering/MRCVisualItem.cs` | 已声明 |
| `MRCElement` | 类 | `Mechanism/Core/Engineering/MRCVisualItem.cs` | 已声明 |
| `MRCRectangle` | 类 | `Mechanism/Core/Engineering/MRCVisualItem.cs` | 已声明 |
| `MRCGroup` | 类 | `Mechanism/Core/Engineering/MRCVisualItem.cs` | 已声明 |
| `MRIObjectHandle` | 类 | `Mechanism/Core/Engineering/MRIObjectAddress.cs` | 已声明 |
| `MRIObjectAddress` | 类 | `Mechanism/Core/Engineering/MRIObjectAddress.cs` | 已声明 |
| `MTCChangeKind` | 枚举 | `Mechanism/Core/Engineering/MTCEditDraft.cs` | 已声明 |
| `MTCChangeOperation` | 结构体 | `Mechanism/Core/Engineering/MTCEditDraft.cs` | 已声明 |
| `MTCEditState` | 枚举 | `Mechanism/Core/Engineering/MTCEditDraft.cs` | 已声明 |
| `MTCEditDraft` | 类 | `Mechanism/Core/Engineering/MTCEditDraft.cs` | 已声明 |
| `MTCEditSession` | 类 | `Mechanism/Core/Engineering/MTCEditSession.cs` | 已声明 |
| `MTIExtractionResult` | 类 | `Mechanism/Core/Engineering/MTIPackages.cs` | 已声明 |
| `MTIEngineeringTemplates` | 类 | `Mechanism/Core/Engineering/MTIPackages.cs` | 已声明 |
| `MTIExternalPolicy` | 枚举 | `Mechanism/Core/Engineering/MTITransferPlan.cs` | 已声明 |
| `IMTIAspectRemapper` | 接口 | `Mechanism/Core/Engineering/MTITransferPlan.cs` | 已声明 |
| `MTITransferResult` | 类 | `Mechanism/Core/Engineering/MTITransferPlan.cs` | 已声明 |
| `MTITransferPlan` | 类 | `Mechanism/Core/Engineering/MTITransferPlan.cs` | 已声明 |

#### CxVisual.Mechanism.Data（21个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `MDEExecutionDataScope` | 类 | `Mechanism/Data/Execution/MDEExecutionDataScope.cs` | 已声明 |
| `IMDSDataContext` | 接口 | `Mechanism/Data/RuntimeData/MDSDataContext.cs` | 已声明 |
| `MDSDataContext` | 类 | `Mechanism/Data/RuntimeData/MDSDataContext.cs` | 已声明 |
| `MDSDataSafety` | 类 | `Mechanism/Data/RuntimeData/MDSDataStack.cs` | 已声明 |
| `MDSDataStack` | 类 | `Mechanism/Data/RuntimeData/MDSDataStack.cs` | 已声明 |
| `MDSDataFrame` | 类 | `Mechanism/Data/RuntimeData/MDSDataStack.cs` | 已声明 |
| `MDSDataTransfer` | 类 | `Mechanism/Data/RuntimeData/MDSDataStack.cs` | 已声明 |
| `MDSDataHeap` | 类 | `Mechanism/Data/RuntimeData/MDSDataHeap.cs` | 已声明 |
| `MDSBindingSource` | 枚举 | `Mechanism/Data/RuntimeData/MDSDataBindings.cs` | 已声明 |
| `IMDSExternalBindingResolver` | 接口 | `Mechanism/Data/RuntimeData/MDSDataBindings.cs` | 已声明 |
| `MDSDataBinding` | 类 | `Mechanism/Data/RuntimeData/MDSDataBindings.cs` | 已声明 |
| `MDSDataBindingResolver` | 类 | `Mechanism/Data/RuntimeData/MDSDataBindings.cs` | 已声明 |
| `IMHSConfigInstance` | 接口 | `Mechanism/Data/Configuration/MHSConfigService.cs` | 已声明 |
| `MHSConfigService` | 类 | `Mechanism/Data/Configuration/MHSConfigService.cs` | 已声明 |
| `MVBConfigScopeResolver` | 类 | `Mechanism/Data/Configuration/MHSConfigService.cs` | 已声明 |
| `MHSConfigApplyReview` | 类 | `Mechanism/Data/Configuration/MHSConfigApply.cs` | 已声明 |
| `MHSConfigApplyDecision` | 类 | `Mechanism/Data/Configuration/MHSConfigApply.cs` | 已声明 |
| `MHSConfigApplyResult` | 类 | `Mechanism/Data/Configuration/MHSConfigApply.cs` | 已声明 |
| `MHSConfigEditorField` | 类 | `Mechanism/Data/Configuration/MHSConfigEditorModel.cs` | 已声明 |
| `MHSConfigCollectionItem` | 类 | `Mechanism/Data/Configuration/MHSConfigEditorModel.cs` | 已声明 |
| `MHSConfigEditorModel` | 类 | `Mechanism/Data/Configuration/MHSConfigEditorModel.cs` | 已声明 |

#### CxVisual.Mechanism.Orchestration（8个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `MOCEngineeringCompiler` | 类 | `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | 已有基础/未完备 |
| `MOCCompiledPlan` | 类 | `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | 已声明 |
| `MOCSourceMap` | 类 | `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | 已声明 |
| `MOCStepOrigin` | 类 | `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | 已声明 |
| `MOCObservedStep` | 类 | `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | 已声明 |
| `MOCTraceView` | 类 | `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | 已声明 |
| `MOCExpressions` | 类 | `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | 已声明 |
| `MOCResolver` | 类 | `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | 已声明 |

#### CxVisual.Mechanism.Presentation.Wpf（4个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `MOLBlockSelectedEventArgs` | 类 | `Mechanism/Presentation.Wpf/MOLFlowCanvas.cs` | 已声明 |
| `MOLBlockTemplateSelector` | 类 | `Mechanism/Presentation.Wpf/MOLFlowCanvas.cs` | 已声明 |
| `MOLFlowCanvas` | 类 | `Mechanism/Presentation.Wpf/MOLFlowCanvas.cs` | 已有基础/未完备 |
| `SceneView` | 类 | `Mechanism/Presentation.Wpf/SceneView.cs` | 已声明 |

#### CxVisual.Mechanism.Runtime（76个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `MRKCodeFlowBuilder` | 类 | `Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs` | 已声明 |
| `MRKCodeRuntimeBuildException` | 类 | `Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs` | 已声明 |
| `MRKCodeRuntimeBuilder` | 类 | `Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs` | 已声明 |
| `MRKCodeRuntimeBlueprint` | 类 | `Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs` | 已声明 |
| `MRKDevelopmentRuntime` | 类 | `Mechanism/Runtime/Execution/MRKDeveloperRuntime.cs` | 已声明 |
| `MRKCompensationReport` | 类 | `Mechanism/Runtime/Execution/MRKExecutionCompensation.cs` | 已声明 |
| `MRKCapabilityInput` | 类 | `Mechanism/Runtime/Execution/MRKExecutionCompiler.cs` | 已声明 |
| `MRKResolvedCapability` | 类 | `Mechanism/Runtime/Execution/MRKExecutionCompiler.cs` | 已声明 |
| `IMRKCapabilityResolver` | 接口 | `Mechanism/Runtime/Execution/MRKExecutionCompiler.cs` | 已声明 |
| `MRKStepCompileRequest` | 类 | `Mechanism/Runtime/Execution/MRKExecutionCompiler.cs` | 已声明 |
| `MRKCompileDiagnostic` | 类 | `Mechanism/Runtime/Execution/MRKExecutionCompiler.cs` | 已声明 |
| `MRKCompileResult` | 类 | `Mechanism/Runtime/Execution/MRKExecutionCompiler.cs` | 已声明 |
| `MRKExecutionPlanCompiler` | 类 | `Mechanism/Runtime/Execution/MRKExecutionCompiler.cs` | 已声明 |
| `MRKFlowCompileDefinition` | 类 | `Mechanism/Runtime/Execution/MRKExecutionComposition.cs` | 已声明 |
| `IMRKFlowCompileCatalog` | 接口 | `Mechanism/Runtime/Execution/MRKExecutionComposition.cs` | 已声明 |
| `MRKExecutionOrchestrationCompiler` | 类 | `Mechanism/Runtime/Execution/MRKExecutionComposition.cs` | 已声明 |
| `MRKExecutionTransition` | 类 | `Mechanism/Runtime/Execution/MRKExecutionControls.cs` | 已声明 |
| `MRKTraceBatch` | 类 | `Mechanism/Runtime/Execution/MRKExecutionControls.cs` | 已声明 |
| `MRKExecutionTrace` | 类 | `Mechanism/Runtime/Execution/MRKExecutionControls.cs` | 已声明 |
| `MRKExecutionDebugController` | 类 | `Mechanism/Runtime/Execution/MRKExecutionControls.cs` | 已声明 |
| `MRKExecutionOperations` | 类 | `Mechanism/Runtime/Execution/MRKExecutionControls.cs` | 已声明 |
| `MRKFlowState` | 枚举 | `Mechanism/Runtime/Execution/MRKExecutionEngine.cs` | 已声明 |
| `MRKStepState` | 枚举 | `Mechanism/Runtime/Execution/MRKExecutionEngine.cs` | 已声明 |
| `MRKFlowCapacityExceededException` | 类 | `Mechanism/Runtime/Execution/MRKExecutionEngine.cs` | 已声明 |
| `MRKExecutionContext` | 类 | `Mechanism/Runtime/Execution/MRKExecutionEngine.cs` | 已声明 |
| `MRKFlowResult` | 类 | `Mechanism/Runtime/Execution/MRKExecutionEngine.cs` | 已声明 |
| `MRKFlowHandle` | 类 | `Mechanism/Runtime/Execution/MRKExecutionEngine.cs` | 已声明 |
| `IMRKExecutionEngine` | 接口 | `Mechanism/Runtime/Execution/MRKExecutionEngine.cs` | 已声明 |
| `MRKExecutionEngine` | 类 | `Mechanism/Runtime/Execution/MRKExecutionEngine.cs` | 已声明 |
| `MRKExecutionGroupDefinition` | 类 | `Mechanism/Runtime/Execution/MRKExecutionGroups.cs` | 已声明 |
| `MRKExecutionGroupSnapshot` | 类 | `Mechanism/Runtime/Execution/MRKExecutionGroups.cs` | 已声明 |
| `MRKExecutionGroupLease` | 类 | `Mechanism/Runtime/Execution/MRKExecutionGroups.cs` | 已声明 |
| `MRKExecutionGroupRejectedException` | 类 | `Mechanism/Runtime/Execution/MRKExecutionGroups.cs` | 已声明 |
| `MRKExecutionGroups` | 类 | `Mechanism/Runtime/Execution/MRKExecutionGroups.cs` | 已声明 |
| `MRKFlowMetadata` | 类 | `Mechanism/Runtime/Execution/MRKExecutionIdentity.cs` | 已声明 |
| `MRKStepObservation` | 类 | `Mechanism/Runtime/Execution/MRKExecutionIdentity.cs` | 已声明 |
| `MRKFlowSnapshot` | 类 | `Mechanism/Runtime/Execution/MRKExecutionIdentity.cs` | 已声明 |
| `MRKJournalKind` | 枚举 | `Mechanism/Runtime/Execution/MRKExecutionJournal.cs` | 已声明 |
| `MRKRecoveryDisposition` | 枚举 | `Mechanism/Runtime/Execution/MRKExecutionJournal.cs` | 已声明 |
| `MRKJournalRecord` | 类 | `Mechanism/Runtime/Execution/MRKExecutionJournal.cs` | 已声明 |
| `MRKRecoveryDecision` | 类 | `Mechanism/Runtime/Execution/MRKExecutionJournal.cs` | 已声明 |
| `MRKJournalRecovery` | 类 | `Mechanism/Runtime/Execution/MRKExecutionJournal.cs` | 已声明 |
| `MRKFileExecutionJournal` | 类 | `Mechanism/Runtime/Execution/MRKExecutionJournal.cs` | 已声明 |
| `IMRKExecutionJournal` | 接口 | `Mechanism/Runtime/Execution/MRKExecutionJournal.cs` | 已声明 |
| `MRKExecutionStep` | 类 | `Mechanism/Runtime/Execution/MRKExecutionPlan.cs` | 已声明 |
| `MRKExecutionPlan` | 类 | `Mechanism/Runtime/Execution/MRKExecutionPlan.cs` | 已声明 |
| `MRKExecutionPlanLease` | 类 | `Mechanism/Runtime/Execution/MRKExecutionPlanSlot.cs` | 已声明 |
| `MRKExecutionPlanSlot` | 类 | `Mechanism/Runtime/Execution/MRKExecutionPlanSlot.cs` | 已声明 |
| `MRKExecutionPolicy` | 类 | `Mechanism/Runtime/Execution/MRKExecutionPolicy.cs` | 已声明 |
| `MRKInputSource` | 枚举 | `Mechanism/Runtime/Execution/MRKExecutionPorts.cs` | 已声明 |
| `MRKExecutionInput` | 类 | `Mechanism/Runtime/Execution/MRKExecutionPorts.cs` | 已声明 |
| `MRKExecutionOutput` | 类 | `Mechanism/Runtime/Execution/MRKExecutionPorts.cs` | 已声明 |
| `MRKExecutionPortException` | 类 | `Mechanism/Runtime/Execution/MRKExecutionPorts.cs` | 已声明 |
| `MRKExecutionPortBinder` | 类 | `Mechanism/Runtime/Execution/MRKExecutionPorts.cs` | 已声明 |
| `MRKPlanPublishAction` | 枚举 | `Mechanism/Runtime/Execution/MRKExecutionPublishing.cs` | 已声明 |
| `IMRKPlanPublicationPolicy` | 接口 | `Mechanism/Runtime/Execution/MRKExecutionPublishing.cs` | 已声明 |
| `MRKPlanPublicationReceipt` | 类 | `Mechanism/Runtime/Execution/MRKExecutionPublishing.cs` | 已声明 |
| `MRKExecutionPlanPublisher` | 类 | `Mechanism/Runtime/Execution/MRKExecutionPublishing.cs` | 已声明 |
| `MRKExecutionTrigger` | 类 | `Mechanism/Runtime/Execution/MRKExecutionTrigger.cs` | 已声明 |
| `MRKRuntimeAccessAction` | 枚举 | `Mechanism/Runtime/Execution/MRKRuntimeAccess.cs` | 已声明 |
| `IMRKRuntimeAccessPolicy` | 接口 | `Mechanism/Runtime/Execution/MRKRuntimeAccess.cs` | 已声明 |
| `MRKRuntimeFlowTicket` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeAccess.cs` | 已声明 |
| `MRKRuntimeAccessGateway` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeAccess.cs` | 已声明 |
| `MRKExternalOperationState` | 枚举 | `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | 已声明 |
| `MRKCorrelationState` | 枚举 | `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | 已声明 |
| `MRKResourceLease` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | 已声明 |
| `MRKResourceLeaseCoordinator` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | 已声明 |
| `MRKExternalOperationObservation` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | 已声明 |
| `MRKExternalOperation` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | 已声明 |
| `MRKExternalOperationTracker` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | 已声明 |
| `MRKCorrelationResult` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | 已声明 |
| `MRKCorrelationTicket` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | 已声明 |
| `MRKCorrelationWaiter` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeCoordination.cs` | 已声明 |
| `MRKRuntimeSessionState` | 枚举 | `Mechanism/Runtime/Execution/MRKRuntimeSession.cs` | 已声明 |
| `MRKRuntimeSessionSnapshot` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeSession.cs` | 已声明 |
| `MRKRuntimeSession` | 类 | `Mechanism/Runtime/Execution/MRKRuntimeSession.cs` | 已声明 |

#### CxVisual.Mechanism.Visual（8个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `CxSceneItem` | 类 | `Mechanism/Visual/CxSceneItem.cs` | 已声明 |
| `CxSceneProjection` | 类 | `Mechanism/Visual/CxSceneProjection.cs` | 已声明 |
| `CxVisualEditor` | 类 | `Mechanism/Visual/CxVisualEditor.cs` | 已声明 |
| `MOLBlockLayout` | 类 | `Mechanism/Visual/MOLFlowLayout.cs` | 已声明 |
| `MOLFlowLayout` | 类 | `Mechanism/Visual/MOLFlowLayout.cs` | 已声明 |
| `MVIEventPhase` | 枚举 | `Mechanism/Visual/MVIInteractionEventArgs.cs` | 已声明 |
| `MVIInteractionKind` | 枚举 | `Mechanism/Visual/MVIInteractionEventArgs.cs` | 已声明 |
| `MVIInteractionEventArgs` | 类 | `Mechanism/Visual/MVIInteractionEventArgs.cs` | 已声明 |

#### CxVisual.Professional.Automation（2个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `CxAutomationDeviceOptions` | 类 | `Professional/Automation/CxAutomationDeviceOptions.cs` | 已声明 |
| `CxAutomationDeviceOptionsRules` | 类 | `Professional/Automation/CxAutomationDeviceOptions.cs` | 已声明 |

#### CxVisual.Professional.CT3D（1个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `CxCtReconstructionOptions` | 类 | `Professional/CT3D/CxCtReconstructionOptions.cs` | 已声明 |

#### CxVisual.Professional.Vision（8个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `CxVisionDataFlow` | 类 | `Professional/Vision/CxVisionDataFlow.cs` | 已声明 |
| `CxVisionGrayFrame` | 类 | `Professional/Vision/CxVisionInspectionRuntime.cs` | 已声明 |
| `CxVisionDefectRegion` | 类 | `Professional/Vision/CxVisionInspectionRuntime.cs` | 已声明 |
| `CxVisionInspectionResult` | 类 | `Professional/Vision/CxVisionInspectionRuntime.cs` | 已声明 |
| `CxVisionThresholdPipeline` | 类 | `Professional/Vision/CxVisionInspectionRuntime.cs` | 已声明 |
| `CxVisionInspectionTask` | 类 | `Professional/Vision/CxVisionInspectionRuntime.cs` | 已声明 |
| `CxVisionInspectionRuntime` | 类 | `Professional/Vision/CxVisionInspectionRuntime.cs` | 已有基础/未完备 |
| `CxVisionRawFrameReader` | 类 | `Professional/Vision/CxVisionInspectionRuntime.cs` | 已声明 |

#### CxVisual.Runtime.Host（3个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `MRHSimulationPublishPolicy` | 类 | `Applications/Runtime.Host/UnifiedRuntimeHost.cs` | 已声明 |
| `MRHSimulationAccessPolicy` | 类 | `Applications/Runtime.Host/UnifiedRuntimeHost.cs` | 已声明 |
| `MRHUnifiedRuntimeHost` | 类 | `Applications/Runtime.Host/UnifiedRuntimeHost.cs` | 已声明 |

#### CxVisual.Studio（6个相关类型）

| 类型 | 形态 | 源文件 | 当前状态 |
|---|---|---|---|
| `StudioViewModel` | 类 | `Applications/Studio/StudioViewModel.cs` | 已声明 |
| `StudioPropertyItem` | 类 | `Applications/Studio/StudioViewModel.Engineering.cs` | 已声明 |
| `StudioPortItem` | 类 | `Applications/Studio/StudioViewModel.Engineering.cs` | 已声明 |
| `StudioConnectionItem` | 类 | `Applications/Studio/StudioViewModel.Engineering.cs` | 已声明 |
| `MOLBlockViewModel` | 类 | `Applications/Studio/MOLRuntimeOverlay.cs` | 已声明 |
| `MOLRuntimeOverlay` | 类 | `Applications/Studio/MOLRuntimeOverlay.cs` | 已声明 |


### 6.2 原82候选的统一决议表（完整列：归属/形态/职责/API/原Gate/当前决议）

> “V1.1决议”经过本次A1～A10收敛修正；任何旧候选仍可追溯。表中带`[N]`者需经过实际场景和编译单元测试证明后才独立建类。原候选`MOLDataLink`改名`MOLLink`，不是把两个都加进去。

| 项目 Owner | 候选名称（原名） | 形态 | 必要职责 | API/字段方向（设计示意） | 原计划 | V1.2处理 | 收敛原因/动作 |
|---|---|---|---|---|---|---|---|
| CxVisual.Mechanism.Connectivity | IMCODeviceCapabilityProvider | 接口 | 设备/规约/通道能力提供统一描述/解析 | Describe,Bind,Execute | G06 | 融合 | 优先与现有Provider/Resolver的装配契约适配；不建立第二能力注册表 |
| CxVisual.Mechanism.Core | MOLLogicModelKind | 枚举 | ControlFlow/DataFlow/Workflow/StateMachine辨别语义 | ControlFlow,DataFlow,Workflow,StateMachine | G01 | 延期论证 | 模型种类不应成为统一语义的唯一依据；原MOLControlKind及Content入口属性在第一批够用，直到多模型集成再判断 |
| CxVisual.Mechanism.Core | MOLLogicEditService | 类 | 块增删改/移动/提取时执行唯一Session事务 | Insert/Remove/Move/Replace/Extract；提交MOLLogicCodec.Set | G01 | 增强已有 | 由MEM Session+MTC+MOLLogicCodec提供批量原子Edit、稳定BlockId；没有另一个事务系统 |
| CxVisual.Mechanism.Core | MOLLogicEditDiagnostic | 类 | Logic定义结构约束失败可定位到BlockId | Code,BlockId,Message,FixHint | G01 | 融合 | 归统一MVE/MOCDiagnostic错误；BlockId、PortKey、LinkId、ResourceAddress要能定位 |
| CxVisual.Mechanism.Core | MOLDataFlowDefinition | 类 | 在Content已有属性/Port/Connection基础上定义数据传输语义 | Ports,Links,Validate；不生成第二MEMNode树 | G03 | 延期论证 | DataFlow具体Stream/FanIn/背压需求时引入；第一批用统一自由连线语义覆盖基本数据端口 |
| CxVisual.Mechanism.Core | MOLLink（原 `MOLDataLink`） | 类 | 在Content已有属性/Port/Connection基础上定义数据传输语义 | Ports,Links,Validate；不生成第二MEMNode树 | G03 | 改名保留候选：`MOLLink` | 自由连接必须覆盖Control/Data/Event/State/Stream，不应只叫DataLink；新语义记录按Content Logic范围唯一持久化 |
| CxVisual.Mechanism.Core | MOLWorkflowDefinition | 类 | 持久等待/人工活动/补偿规则的声明与版本语义 | Activities,WaitRule,RetryPolicy,Compensation,Version | G05 | 保留候选 | 具备持久等待/恢复等专属语义后才引入正式定义，先锁定规范和样例 |
| CxVisual.Mechanism.Core | MOLWaitActivity | 类 | 持久等待/人工活动/补偿规则的声明与版本语义 | Activities,WaitRule,RetryPolicy,Compensation,Version | G05 | 保留候选 | 具备持久等待/恢复等专属语义后才引入正式定义，先锁定规范和样例 |
| CxVisual.Mechanism.Core | MOLCompensationSpec | 类 | 持久等待/人工活动/补偿规则的声明与版本语义 | Activities,WaitRule,RetryPolicy,Compensation,Version | G05 | 保留候选 | 具备持久等待/恢复等专属语义后才引入正式定义，先锁定规范和样例 |
| CxVisual.Mechanism.Core | MOLStateMachineDefinition | 类 | 层级状态/正交区/迁移/事件定义 | States,Transitions,Validate,InitialState | G05 | 保留候选 | 状态/事件/层级模型需明确本身语义，不能由单纯连线自动推断 |
| CxVisual.Mechanism.Core | MOLState | 类 | 层级状态/正交区/迁移/事件定义 | States,Transitions,Validate,InitialState | G05 | 保留候选 | 层级状态、并行区域、迁移需专属数据结构；使用Core持久化，不建第二对象树 |
| CxVisual.Mechanism.Core | MOLTransition | 类 | 层级状态/正交区/迁移/事件定义 | States,Transitions,Validate,InitialState | G05 | 保留候选 | 层级状态、并行区域、迁移需专属数据结构；使用Core持久化，不建第二对象树 |
| CxVisual.Mechanism.Core | MOLWaitKind | 枚举 | Wait种类/状态机触发种类 | Event,Time,Condition,Manual | G05 | 保留候选 | 随Workflow和StateMachine正式建模；不要提前把枚举语义硬塞给ControlFlow |
| CxVisual.Mechanism.Core | MOLTransitionTriggerKind | 枚举 | Wait种类/状态机触发种类 | Event,Time,Condition,Manual | G05 | 保留候选 | 随Workflow和StateMachine正式建模；不要提前把枚举语义硬塞给ControlFlow |
| CxVisual.Mechanism.Core | MOLInvocationContract | 类 | 输入输出/版本/被调内容/边界类型；复用既有引用与端口 | TargetContentId,InputMap,OutputMap,VersionConstraint | G02 | 增强已有 | 以MRIReference/MPC映射/Logic Invoke形成调用契约，数据每次执行传入MDS Frame |
| CxVisual.Mechanism.Core | MOLInvocationMode | 枚举 | Embedded/Invoke/Trigger调用区别 | Embedded,Invoke,Trigger | G02 | 保留候选 | 显式区分引用定义、Invoke和Trigger，优先使用现有引用Kind+Invoke语义，避免重复枚举 |
| CxVisual.Mechanism.Core | MOLLayoutHints | 类 | 布局/折叠等非执行属性，不混入运行模型 | BlockId,Geometry,Collapsed；版本化保存 | G04 | 融合 | 布局位置保留在Visual Aspect，不能改变Logic的执行语义 |
| CxVisual.Mechanism.Data | MDSInvocationFrame | 类 | Iteration/Call的作用域隔离，生命周期释放与审计 | CreateChild,Get,Set,Dispose | G05 | 融合 | 使用既有MDSDataFrame/MDSDataStack，并由MRK管理InvocationId |
| CxVisual.Mechanism.Data | MDSStreamHandle | 类 | XRay影像/CT体数据零拷贝数据引用+生命周期 | Lease,Acquire,Release,Metadata | G06 | 延期论证 | 先验证FDTReadOnlyBuffer/FCRSharedLease/MDS数据安全契约是否足够，勿先另造大对象句柄 |
| CxVisual.Mechanism.Lifecycle | MLCBuildArtifact | 类 | 冻结工程定义/插件/配方/依赖版本与摘要 | Snapshot,LockDependencies,Verify | G07 | 保留候选 | 长期必须锁定计划/SourceMap/Provider版本，首个90分钟先保留协议规则 |
| CxVisual.Mechanism.Lifecycle | MLCDependencyLock | 类 | 冻结工程定义/插件/配方/依赖版本与摘要 | Snapshot,LockDependencies,Verify | G07 | 保留候选 | 与MLCBuildArtifact统一出品，后续工业发布阶段落地 |
| CxVisual.Mechanism.Lifecycle | MLCReleaseService | 类 | 将已验证固定版交给MRK受控发布策略 | Package,Stage,Activate,Rollback | G07 | 保留候选 | 发布必须具有不可变构建物与签名摘要，归Lifecycle；不要与MRKExecutionPlan混淆 |
| CxVisual.Mechanism.Logic | IMOLExpressionEvaluator | 接口 | HMI/编排/规则可复用的表达式规则求值 | Evaluate(typedExpression,dataContext) | G05 | 保留候选 | 表达式求值跨专业共用时抽出纯语义接口，保持无工程/运行状态 |
| CxVisual.Mechanism.Logic | MOLRuleEvaluator | 类 | 共享规则与状态迁移验证，不持久第二Logic | EvaluateRule,ValidateTransition | G05 | 增强已有 | 优先把MOCExpressions可复用语义梳理出来；不得在Logic项目并行再建一套解释器 |
| CxVisual.Mechanism.Logic | MOLStateTransitionValidator | 类 | 共享规则与状态迁移验证，不持久第二Logic | EvaluateRule,ValidateTransition | G05 | 融合 | 可作为状态机Validator内部行为，除非独立使用者证明需要公开类型 |
| CxVisual.Mechanism.Orchestration | MOCFlowAuthoring | 类 | EditFlow的高层易用门面，真正落入MOL + Core Session | EditFlow(session,content,Action<Builder>),Validate() | G02 | 融合 | 公开一个MOCFlowBuilder+Edit入口，不重复Facade |
| CxVisual.Mechanism.Orchestration | MOCFlowBuilder | 类 | Step/If/Parallel/ForEach/Invoke/Connect fluent式变更 | Step,If,Parallel,Invoke,Connect,Commit | G02 | 保留候选 | 同一持久Logic的C#编辑薄门面，借助MTC单事务写入，禁止生成独立持久树 |
| CxVisual.Mechanism.Orchestration | IMOCCapabilityCatalog | 接口 | 向开发者和Studio按专业、版本与搜索返回能力契约 | Search,Describe,Resolve,Validate | G02 | 保留候选 | 只读聚合，不依赖另存一份Provider事实源，设计/编译版本必须一致 |
| CxVisual.Mechanism.Orchestration | MOCCapabilityDescriptor | 类 | 标准目录项身份/参数/端口/用途/编辑器贡献 | Id,Version,Name,Ports,Parameters,Category | G02 | 保留候选 | 需统一搜索/分类可视信息；必须聚合IMPMTypeProvider与运行能力契约，不能保有第二权威能力库 |
| CxVisual.Mechanism.Orchestration | MOCBuildRequest | 类 | 多层验证/解析/快照锁定/诊断带来源 | Validate,Resolve,Compile；Code,Severity,Address,BlockId | G03 | 保留候选 | 构建输入、结果、错误是跨C#/Studio的稳定协议；随G03一次性设计 |
| CxVisual.Mechanism.Orchestration | MOCBuildResult | 类 | 多层验证/解析/快照锁定/诊断带来源 | Validate,Resolve,Compile；Code,Severity,Address,BlockId | G03 | 保留候选 | 构建输入、结果、错误是跨C#/Studio的稳定协议；随G03一次性设计 |
| CxVisual.Mechanism.Orchestration | MOCDiagnostic | 类 | 多层验证/解析/快照锁定/诊断带来源 | Validate,Resolve,Compile；Code,Severity,Address,BlockId | G03 | 保留候选 | 构建输入、结果、错误是跨C#/Studio的稳定协议；随G03一次性设计 |
| CxVisual.Mechanism.Orchestration | MOCFlowTypeChecker | 类 | 校验Port/连接/参数/单位与数据安全 | Check(snapshot,entry,providers) | G03 | 增强已有 | 在MOCEngineeringCompiler验证层中校验两个范围的Port/Link/Provider约束，独立服务按消费者再拆 |
| CxVisual.Mechanism.Orchestration | MOCModelCompiler | 类 | 依据LogicModelKind分派语义编译，复用MRK | CompileFlowModel -> MRKCompileRequests | G05 | 增强已有 | 在MOCEngineeringCompiler增量处理Wiring→结构化IR→MRK；不新起Compiler层级 |
| CxVisual.Mechanism.Orchestration | MOCDataFlowPlanner | 类 | 确定生产消费依赖/Buffer策略/FanIn/FanOut | PlanLinks,ValidateBackpressure | G05 | 保留候选 | 只在真实DataFlow背压、FanIn、时间窗验收开始时实现Planner |
| CxVisual.Mechanism.Orchestration | MOCWorkflowPlanner | 类 | 长任务/等待/恢复/人工任务的编译策略 | PlanActivities,ResolveVersion | G05 | 保留候选 | 只在Workflow长事务/恢复案例开始时实现Planner |
| CxVisual.Mechanism.Orchestration | MOCStateMachinePlanner | 类 | 事件/状态转移/守卫/复合状态的编译策略 | CompileTransitions,ValidateRegions | G05 | 保留候选 | 只在StateMachine层级状态/事件案例开始时实现Planner |
| CxVisual.Mechanism.Orchestration | MOCSubflowExtractor | 类 | 选区→外部输入输出→预览→事务替换调用 | Preview,Extract,PublishPorts,ReplaceWithInvoke | G04 | 增强已有 | 复用MTI资源级提取事务，但新增对MOL逻辑选区的边界端口推导，不能直接复用现有roots参数 |
| CxVisual.Mechanism.Orchestration | MOCExtractionPreview | 类 | 选区→外部输入输出→预览→事务替换调用 | Preview,Extract,PublishPorts,ReplaceWithInvoke | G04 | 保留候选 | 用于预览改变前的输入输出及影响范围，若MTI预览已有足够则合并 |
| CxVisual.Mechanism.Orchestration | MOCFlowRefactoring | 类 | 对调用方/参数映射的影响分析及迁移 | FindCallers,PreviewImpact,ApplyFix | G04 | 延期论证 | 先用现有资源引用查找与变更预览，跨版本大规模重构才引入独立组件 |
| CxVisual.Mechanism.Orchestration | IMOCBuildValidator | 接口 | 专业/插件输入更多静态校验而不侵入Core | Validate(context)->Diagnostics | G03 | 保留候选 | 专业静态校验扩展点可能必要；接口只接收只读快照、输出诊断 |
| CxVisual.Mechanism.Orchestration | IMOCBuildContributor | 接口 | 在统一构建流程提供目录/验证/编译/预览贡献 | Describe,Validate,Contribute | G07 | 融合 | 不要建设大而全注册接口；按类型描述/Resolver/Validator分别贡献 |
| CxVisual.Mechanism.Orchestration | MOCBuildFacade | 类 | Validate,Build,RunDevelopment统一入口；只编排已有Owner | Validate,Build,RunDevelopmentAsync | G03 | 保留候选 | 提供统一Validate/Build/RunDevelopment流程；运行操作仍委托MRK授权API |
| CxVisual.Mechanism.Presentation.Wpf | MPWFlowEditor | 类 | 拖入、连接、选择、框选、快捷键、仿真覆盖 | DependencyProperties/Commands/EventHandlers | G04 | 保留候选 | 先增强已有MOLFlowCanvas与Studio命令，若WPF交互职责确实超大再提独立编辑器 |
| CxVisual.Mechanism.Presentation.Wpf | MPWFlowToolbox | 类 | 工具箱/端口连接/提示；细分控件仅需要时添加 | Binding/DragDrop/ValidationTooltip | G04 | 延期论证 | 优先在已有WPF资源面板/控件中实现，按复用需求才拆独立控件 |
| CxVisual.Mechanism.Presentation.Wpf | MPWFlowPortConnector | 类 | 工具箱/端口连接/提示；细分控件仅需要时添加 | Binding/DragDrop/ValidationTooltip | G04 | 延期论证 | 优先在已有WPF资源面板/控件中实现，按复用需求才拆独立控件 |
| CxVisual.Mechanism.Runtime | MRKControlRegion | 类 | 100万规模循环复用BodyPlan而不展开海量Step | ExecuteRegionAsync,FrameFactory,RequestCancel | G05 | 保留候选 | 动态循环运行需复用BodyPlan的结构区，经过内存/取消/背压PoC再定最终对象形态 |
| CxVisual.Mechanism.Runtime | MRKRegionBodyPlan | 类 | 100万规模循环复用BodyPlan而不展开海量Step | ExecuteRegionAsync,FrameFactory,RequestCancel | G05 | 保留候选 | 动态循环运行需复用BodyPlan的结构区，经过内存/取消/背压PoC再定最终对象形态 |
| CxVisual.Mechanism.Runtime | MRKInvocationContext | 类 | 每次子流程调用/迭代的独立身份与栈/结果 | InvocationId,CallPath,Iteration,DataFrame | G05 | 增强已有 | MRKExecutionContext/MRKFlowMetadata先补InvocationId/调用路径，不先造重复上下文 |
| CxVisual.Mechanism.Runtime | MRKInvocationSnapshot | 类 | 每次子流程调用/迭代的独立身份与栈/结果 | InvocationId,CallPath,Iteration,DataFrame | G05 | 保留候选 | 多实例观察需要不可变身份快照；优先复用MRKFlowSnapshot |
| CxVisual.Mechanism.Runtime | MRKRegionExitKind | 枚举 | Break/Continue/Return在Region退出的执行语义 | Break,Continue,Return | G05 | 保留候选 | 由真实Region的Break/Continue/Return流程测试驱动 |
| CxVisual.Mechanism.Runtime | MRKStreamChannel | 类 | 有界队列/容量/背压/完成与取消 | WriteAsync,ReadAsync,Complete,Cancel | G06 | 延期论证 | 先评估既有FCR调度队列/容量和MDS数据缓冲，避免第三种队列基础设施 |
| CxVisual.Mechanism.Runtime | MRKBackpressurePolicy | 枚举 | 阻塞/丢弃/覆盖/拒绝的执行策略 | Block,DropLatest,DropOldest,Reject | G06 | 延期论证 | 先评估既有FCR调度队列/容量和MDS数据缓冲，避免第三种队列基础设施 |
| CxVisual.Mechanism.Runtime | MRKDurableWaitHandle | 类 | 长期Wait与恢复，在已有Journal基础上记录安全状态 | Suspend,Resume,Recover,ResolveUnknown | G06 | 保留候选 | 长任务真实挂起/重启后恢复的身份句柄，先确认MRK Journal约束 |
| CxVisual.Mechanism.Runtime | MRKWorkflowRecovery | 类 | 长期Wait与恢复，在已有Journal基础上记录安全状态 | Suspend,Resume,Recover,ResolveUnknown | G06 | 增强已有 | 优先复用MRKJournalRecovery并扩充Workflow状态，不做另一个恢复引擎 |
| CxVisual.Mechanism.Runtime | MRKStateMachineInstance | 类 | 每个运行实例的活动状态、事件队列及原子转换 | Dispatch,Transition,Snapshot | G06 | 保留候选 | 活跃状态集、事件队列、原子迁移需要每实例状态；正式运行验收开始时落地 |
| CxVisual.Mechanism.Runtime | IMRKExternalCompletionProbe | 接口 | 设备动作未知/已提交/真实完成的确认契约 | ProbeAsync(ticket)->PhysicalCompletion | G06 | 保留候选 | 真实硬件未知状态不得自动重试；需要专业端提供物理完成核验/探测 |
| CxVisual.Mechanism.Runtime | MRKBuildRunHandle | 类 | 组合Plan+Flow+Trace+SourceMap，便捷Observe/Cancel | Completion,Trace,CancelAsync | G03 | 取消 | 不能归Runtime引用MOCSourceMap，聚合句柄置于MOC/Host且受权限控制 |
| CxVisual.Mechanism.Visual | MVIFlowEditController | 类 | 节点/连线增删移动/布局命令，共用Session | Insert,Connect,Move,Delete,Undo,Redo | G04 | 保留候选 | 统一可视编辑命令与Core Session事务，既服务WPF也允许代码编辑 |
| CxVisual.Mechanism.Visual | MVIFlowRoute | 类 | 路由/选择/折叠/缩放/多视图导航 | Route,Selection,Zoom,Collapsed | G04 | 延期论证 | 先把自动排版、选择、路由视图状态作为MOLFlowLayout增强或轻量值对象 |
| CxVisual.Mechanism.Visual | MVIFlowSelection | 类 | 路由/选择/折叠/缩放/多视图导航 | Route,Selection,Zoom,Collapsed | G04 | 延期论证 | 先把自动排版、选择、路由视图状态作为MOLFlowLayout增强或轻量值对象 |
| CxVisual.Mechanism.Visual | MVIFlowViewState | 类 | 路由/选择/折叠/缩放/多视图导航 | Route,Selection,Zoom,Collapsed | G04 | 保留候选 | 编辑布局/折叠/缩放需独立视图状态，明确是否随工程持久化 |
| CxVisual.Mechanism.Visual | MVIFlowDiagnosticProjection | 类 | 编译问题和运行Trace到画布块的关联 | GetVisualAnchor(address,blockId) | G04 | 增强已有 | 复用MOCSourceMap及MOLRuntimeOverlay，必要时增加纯投影工具 |
| CxVisual.Professional.Automation | CxAutomationCaptureProvider | 类 | 将采图/命令/ACK安全态映射为运行能力 | TryResolve,Acquire,WaitPhysicalAck | G06 | 保留候选 | 专业能力实现归专业项目，复用驱动或旧代码并通过既有MRK Resolver暴露 |
| CxVisual.Professional.Automation | CxAutomationPlcCommandProvider | 类 | 将采图/命令/ACK安全态映射为运行能力 | TryResolve,Acquire,WaitPhysicalAck | G06 | 保留候选 | 专业能力实现归专业项目，复用驱动或旧代码并通过既有MRK Resolver暴露 |
| CxVisual.Professional.CT3D | CxCtReconstructionProvider | 类 | 通过专业接口接入重建、MPR和体绘 | Describe,ReconstructAsync | G06 | 保留候选 | 专业能力实现归专业项目，复用驱动或旧代码并通过既有MRK Resolver暴露 |
| CxVisual.Professional.HMI | CxHmiComponentCatalog | 类 | 工业监控控件/绑定编辑器的专业贡献 | DescribeComponents,BindTag | G06 | 保留候选 | HMI构件发现与专业编辑能力可提供元数据视图，复用MPM定义 |
| CxVisual.Professional.Inspection | CxInspectionDecisionProvider | 类 | 融合、规则、复判、追溯 | Inspect,Judge,RequestReview | G06 | 保留候选 | 专业能力实现归专业项目，复用驱动或旧代码并通过既有MRK Resolver暴露 |
| CxVisual.Professional.Inspection | CxInspectionReviewActivity | 类 | 融合、规则、复判、追溯 | Inspect,Judge,RequestReview | G06 | 保留候选 | 专业能力实现归专业项目，复用驱动或旧代码并通过既有MRK Resolver暴露 |
| CxVisual.Professional.Simulation | CxSimulationCapabilityProvider | 类 | 设备仿真、样本、故障注入、回放 | Describe,RunScenario,InjectFault | G06 | 保留候选 | 专业能力实现归专业项目，复用驱动或旧代码并通过既有MRK Resolver暴露 |
| CxVisual.Professional.Vision | CxVisionAlgorithmProvider | 类 | 已有检测/AI/传统算法组合为可复用专业能力 | Describe,ExecuteAsync,Test | G06 | 保留候选 | 专业能力实现归专业项目，复用驱动或旧代码并通过既有MRK Resolver暴露 |
| CxVisual.Runtime.Host | MRHCapabilityBootstrap | 类 | 组合全部专业Provider,IMPMTypeProvider,IMRKCapabilityResolver | RegisterProviders,ValidateDependencies | G06 | 增强已有 | 优先在MRHUnifiedRuntimeHost装配已有Providers，不为注册再建立一套全局单例 |
| CxVisual.Studio | StudioEngineeringWorkspace | 类 | 资源导航、画布、属性面板、多个专业编辑器 | OpenResource,ActivateEditor,FindObject | G04 | 增强已有 | 优先在已有StudioViewModel拆分职责为局部工作区服务，按大小决定是否独立 |
| CxVisual.Studio | StudioFlowEditorViewModel | 类 | 可视编辑、搜索能力、参数连接与导航 | InsertStep,EditParameter,ExtractFlow,Search | G04 | 保留候选 | 需要可测试的Studio编排VM，操作MVI/Core命令不直改Logic对象 |
| CxVisual.Studio | StudioCapabilityCatalogViewModel | 类 | 可视编辑、搜索能力、参数连接与导航 | InsertStep,EditParameter,ExtractFlow,Search | G04 | 保留候选 | 目录UI使用MOC只读目录接口，不拥有第二套能力元数据 |
| CxVisual.Studio | StudioProblemListViewModel | 类 | 错误列表/SourceMap追踪/断点/Probe/迭代实例切换 | NavigateTo,ShowTrace,Watch,FilterByInstance | G04 | 保留候选 | 统一校验错误+BlockId跳转，避免专业插件各自建问题列表 |
| CxVisual.Studio | StudioRuntimeInspectorViewModel | 类 | 错误列表/SourceMap追踪/断点/Probe/迭代实例切换 | NavigateTo,ShowTrace,Watch,FilterByInstance | G04 | 保留候选 | 统一运行实例/调用路径/迭代/Trace的观察入口，必须经过授权受控API |
| CxVisual.Studio | StudioPublishViewModel | 类 | Stage/Activate/Rollback确认与可审计的发布结果 | Review,Publish,Rollback | G07 | 延期论证 | 先明确真实发布治理与审批接口，暂不做仅演示按钮的发布VM |
| CxVisual.TestCenter | TCTBuildRealCases | 类 | C01~C10及设备模拟/实机分级证据、恶意/失效场景 | Run,VerifyEvidence | G06-G08 | 增强已有 | 归现有TestCenter真实场景/故障案例，不以增加测试类数量代表验收 |
| CxVisual.TestCenter | TCTBuildAdversarialCases | 类 | C01~C10及设备模拟/实机分级证据、恶意/失效场景 | Run,VerifyEvidence | G06-G08 | 增强已有 | 归现有TestCenter真实场景/故障案例，不以增加测试类数量代表验收 |
| CxVisual.Tests.ArchitectureSmoke | TCTBuildCoreSmoke | 类 | 保存编译/分支/实例/Trace/错误回归 | Run | G01-G05 | 增强已有 | 扩展ArchitectureSmoke现有Logic/Compiler测试，按实际工作包可拆文件 |
| CxVisual.Tests.ArchitectureSmoke | TCTBuildCompilerSmoke | 类 | 保存编译/分支/实例/Trace/错误回归 | Run | G01-G05 | 增强已有 | 扩展ArchitectureSmoke现有Logic/Compiler测试，按实际工作包可拆文件 |
| CxVisual.Mechanism.Core | MOLBlockEndpoint（V1.2补充） | 类/不可变值对象 | 逻辑块端点：BlockId + PortMemberId，支持同一Content内的可验证自由连接 | BlockId,PortMemberId,Equals/GetHashCode | 第1阶段 | 保留候选 | 与MRIObjectAddress区别：逻辑块不是MEMNode；不从ResourcePort生成第二个所有权树 |

### 6.3 完整32方面到现有项目与类型的覆盖映射（原496条细节保留外部表引用）

| ID | 方面 | 需求数 | Owner | 主要已有/候选类型 | 代码现状 |
|---|---|---:|---|---|---|
| 01 | 目标与模型边界 | 10 | Mechanism.Core | MEMNode/MEMContent/MPMAspect/MOLLogicDefinition | MEM基础有；四模型正式复合声明待设计 |
| 02 | 工程资源与生命周期 | 14 | Mechanism.Core | MEMEngineeringSession/IMEMResource/MRIResourceReference | 工程组织基础已有；大规模工程验收继续 |
| 03 | 工具箱与能力目录 | 12 | Mechanism.Orchestration | IMPMTypeProvider/MPMTypeDescriptor/IMOCCapabilityCatalog(候选) | 元数据已有；完整目录/工具箱缺失 |
| 04 | 编排编辑器交互 | 17 | Mechanism.Visual | CxVisualEditor/MOLFlowLayout/MOLFlowCanvas/MVIFlowEditController(候选) | 图元已有；逻辑画布仅展示与示例 |
| 05 | 基础步骤与控制流 | 17 | Mechanism.Orchestration | MOLControlBlock/MOCEngineeringCompiler/MRKExecutionEngine | 小规模结构语义已有；复杂Try/Wait/Exit未完 |
| 06 | 数据流与流式处理 | 16 | Mechanism.Orchestration | MPCPort/MPCConnection/MOCDataFlowPlanner(候选)/MRKStreamChannel(候选) | 端口/执行数据接口已有，持续流完整编排缺失 |
| 07 | Workflow长流程 | 15 | Mechanism.Orchestration | MOLWorkflowDefinition(候选)/MRKFileExecutionJournal/Recovery | Journal已有；Workflow Designer/Executor未完整 |
| 08 | 状态机与反应式逻辑 | 14 | Mechanism.Logic | MOLStateMachineDefinition(候选)/MRKStateMachineInstance(候选) | Logic项目骨架；完整状态机缺失 |
| 09 | 事件与触发 | 14 | Mechanism.Orchestration | MRKExecutionTrigger/MOCBuildValidator(候选) | Runtime触发基础存在；全量策略待补 |
| 10 | 子流程与调用契约 | 17 | Mechanism.Orchestration | MOLControlBlock.Invoke/MRIResourceReference/MTIEngineeringTemplates | Content Invoke及资源复用已有；完整端口映射待补 |
| 11 | 异构执行模型组合 | 13 | Mechanism.Orchestration | MOCModelCompiler(候选)/MOLLogicModelKind(候选) | 只有基础ControlFlow已实装 |
| 12 | 并行与多实例 | 15 | Mechanism.Runtime | MRKExecutionEngine/MRKExecutionGroups/MRKControlRegion(候选) | MRK并发有；Region/动态实例语义欠缺 |
| 13 | 资源与上下文 | 15 | Mechanism.Runtime | MDSDataContext/MDSDataStack/MRKResourceLeaseCoordinator | 共享机制已有；并行安全隔离待验 |
| 14 | 属性参数与数据绑定 | 15 | Mechanism.Core | MPMProperty/MPMBinding/MDSDataBindingResolver | 多种契约已有；Studio简洁配置欠缺 |
| 15 | 端口连线与契约 | 16 | Mechanism.Orchestration | MPCPort/MPCConnection/MRKExecutionPortBinder | 定义与运行Port有；MOL步骤接口联动待补 |
| 16 | 错误取消超时重试 | 17 | Mechanism.Runtime | MRKExecutionPolicy/MRKCompensationReport/MRKFileExecutionJournal | 运行基础已有，长流程补偿与现场检查待验 |
| 17 | 设备动作与工业安全 | 15 | Professional.Automation | IMRKCapabilityResolver/CxAutomationDeviceOptions/MRKExternalOperationTracker | 机械/运动等真实Driver接入未完成 |
| 18 | 专业视觉/AI/CT算法编排 | 19 | Professional.Vision / Professional.CT3D | CxVisionInspectionRuntime/CxCtReconstructionOptions | Vision部分算法有；CT重建专业API仅配置骨架 |
| 19 | 专业IoT/自动化与集成 | 17 | Professional.Automation | CxAutomationDeviceOptions/Connectivity项目 | Connectivity当前仅骨架；专业接入未完成 |
| 20 | 人机协同与业务审批 | 12 | Professional.Inspection | MOLWorkflowDefinition(候选)/CxInspectionReviewActivity(候选) | Inspection仅骨架；长任务语义待补 |
| 21 | 开发者代码体验 | 16 | Mechanism.Orchestration | MEMEngineeringSession/MOCFlowAuthoring(候选) | 现有API链路实装，统一易用门面缺失 |
| 22 | 工程编排用户体验 | 16 | Applications.Studio | StudioViewModel/MPWFlowEditor(候选) | Studio现有资源与示例，专业完整工作区欠缺 |
| 23 | 验证解析与编译 | 18 | Mechanism.Orchestration | MOCEngineeringCompiler/MRKExecutionPlanCompiler | 现有MOC编译有效；四模型综合诊断待补 |
| 24 | 调试与诊断 | 18 | Mechanism.Runtime | MOCSourceMap/MRKExecutionTrace/MRKExecutionDebugController | Trace与SourceMap已有；多实例可视Probe不足 |
| 25 | 仿真回放与场景 | 16 | Professional.Simulation | MRHUnifiedRuntimeHost/CxSimulationCapabilityProvider(候选) | CPU示例已有；专业Simulation仅骨架 |
| 26 | 运行观测与控制 | 17 | Mechanism.Runtime | MRKRuntimeSession/MOLRuntimeOverlay/StudioRuntimeInspectorViewModel(候选) | 基础已有；专业完整监控不足 |
| 27 | 持久化版本与协作 | 16 | Mechanism.Core | MEMFileProjectStore/MEMProjectSnapshot/MTIEngineeringTemplates | 基础有；复杂部署迁移与插件缺失校验不足 |
| 28 | 部署发布与运行接续 | 16 | Mechanism.Lifecycle | MRKExecutionPlanPublisher/MLCReleaseService(候选) | MRK发布存在；Lifecycle项目骨架 |
| 29 | 权限安全与审计 | 14 | Mechanism.Lifecycle | IMEMAccessPolicy/IMRKRuntimeAccessPolicy/IMRKPlanPublicationPolicy | 已有策略接口；高危设备授权现场门禁待验 |
| 30 | 扩展与插件机制 | 15 | Mechanism.Lifecycle | IMPMTypeProvider/IMOCBuildContributor(候选) | 类型Provider已有；Lifecycle插件机制未完成 |
| 31 | 性能与规模 | 16 | Mechanism.Runtime | MRKExecutionGroups/FCRWorkerOptions/MDSDataBudget | 基础存在；生产规模目标未经过完整测试 |
| 32 | 可测性与证据 | 18 | Tests.TestCenter | OrganizationLogicIntegrationTests/ArchitectureSmoke/TestCenter | 基础CI本提交成功；专业全部案例没有证据 |



### 6.4 首批实际要改的关键类型与公开方法归位（区别于“全量声明表”）

| 状态 | 项目 Owner | 类/接口/枚举 | 已有/拟议的关键API | 责任与禁区 | 目标阶段 |
|---|---|---|---|---|---|
| E/R | Mechanism.Core | `MEMEngineeringSession` | **已有：**`BeginEdit(intent, expectedRevision)`、`AddContent`、`SaveAs`、`GetSnapshot`、`Undo/Redo`；**增强：**允许一次逻辑变更批次保存 | 事务、版本、授权和唯一索引；不得持有MRKEngine | 1 |
| E/R | Mechanism.Core | `MTCEditDraft / MTCEditSession` | **已有：**Change/Inverse、`SetAspect`、`DefinePort`、`Connect`；**增强：**一次Action批次原子提交MOL V2 | 不允许嵌套`Edit`/绕过Revision；失败整组回滚 | 1 |
| E/R | Mechanism.Core | `MOLLogicDefinition` | **已有：**`Root`、`Validate()`；**增强：**V2`Blocks/Links/EntryBlockId`与区分Schema的行为 | 每个Content单执行入口只有一份权威Logic；不保留与之冲突的AST副本 | 1 |
| E/R | Mechanism.Core | `MOLControlBlock / MOLControlKind` | **已有：**`Action/Sequence/Parallel/If/Repeat/Each`；**增强：**V2动作块稳定Id、PortSchema映射、Legacy模式区分 | 块仍不是MEMNode；不把用户画布坐标序列化进逻辑 | 1 |
| E/R | Mechanism.Core | `MOLLogicCodec` | **已有：**`Set(session,content,logic)`、`Get(snapshot)`；**增强：**`SchemaVersion=1/2`读写、纯`CreateAspect`或合法MTC重载 | 保持同一ProviderId、同一Aspect槽位；旧工程兼容 | 1 |
| N | Mechanism.Core | `MOLBlockEndpoint` | `BlockId`、`PortMemberId`、`Equals/GetHashCode` | 内部块端点，不伪造MRIObjectAddress；不可变 | 1 |
| N（旧候选改名） | Mechanism.Core | `MOLLink` | `Id`、`Source/Target`、`Kind`、`ConditionId/Order` | Control/Data/Event/State等自由边；只在Content Logic中保存 | 1 |
| E/R | Mechanism.Core | `MPCPort / MPCPortDescriptor / MPCPortKind / MPCPortMultiplicity` | 已有类型、方向、数量、Unit/Frame/Time契约；必要时抽纯验证函数 | 对外Port仍MPC；`Multiplicity`不代表复合值/列表值 | 1/2 |
| E | Mechanism.Core | `MPCConnection / MPCEndpoint / MRIObjectAddress` | 已有`Source/Target`、Resource+Object+Port身份 | 不扩成第二个持久内部Link；保留资源引用兼容 | 1/2 |
| E | Mechanism.Core | `IMEMResource / MEMContent / MEMSymbol / MRCGroup / MRCElement` | 已有资源/图元接口；未来Symbol`PrimaryValue`为语义标注 | Content组合、Symbol主值呈现；不更改继承结构 | 不强制首批改 |
| E/R | Mechanism.Orchestration | `MOCEngineeringCompiler` | **已有：**`Compile(snapshot, entry, resolver, instancePath)`；**增强：**`V2连接→验证/结构化IR→Plan` | 不另建Scheduler；无法归约的连接拒绝 | 2 |
| E/R | Mechanism.Orchestration | `MOCSourceMap / MOCStepOrigin / MOCCompiledPlan` | **已有：**`TryResolve`、`Find`、`ReadTrace`；**增强：**新增LinkId诊断来源 | 绑定源Block/Link/调用路径/固定版本，不反向修改工程 | 2 |
| E | Mechanism.Runtime | `IMRKCapabilityResolver / MRKExecutionPlanCompiler / MRKExecutionEngine` | **已有：**`TryResolve`、`Compile`、`StartFlow`、`StopAsync` | 本轮复用CPU运行，不再新造执行器 | 2 |
| E | Mechanism.Data | `MDSDataFrame / MDSDataStack / MDSDataContext` | 已有独立Frame/输入输出、Heap引用 | 本轮验证实例隔离，不另造Frame类 | 2 |
| N（按需） | Mechanism.Orchestration | `MOCFlowBuilder` | `Edit(session, content, Action<Builder>)`、`Step/Connect` | 持久工程编辑薄门面，自动生成并提交同一Logic，不复制MRKCodeRuntimeBuilder | 3 |
| E/R | Mechanism.Visual | `MOLFlowLayout / CxVisualEditor` | 已有`Build(definition)`、`Undo/Redo`；增强Link路径/连接编辑命令 | 无WPF，可复用Core Session事务，拖动坐标不影响编译 | 3 |
| E/R | Mechanism.Presentation.Wpf | `MOLFlowCanvas / SceneView` | 已有绘制/选中，目标新增端点命中与`ConnectRequested`等事件 | 提交只经Studio/MVI/Core，WPF不执行设备 | 3 |
| E/R | Applications.Studio | `StudioViewModel / MOLRuntimeOverlay` | 已有工程资源/CPU示例/Trace，目标新增最小连线编辑 + 保存/观察 | 不自建Flow数据模型，不直接持有无授权生产执行权 | 3 |
| E | Tests.ArchitectureSmoke / TestCenter | `OrganizationLogicIntegrationTests`及现有测试基座 | 新增`WiringV2Roundtrip`、`WiringCompileCpu`、`EditorSharedDefinition`等行为用例（名称为提议） | 真实新旧工程文件、CPU运行与错误反例，不能用Mock冒充集成 | 1/2/3 |

**Owner唯一性规则：** `MOLBlockEndpoint/MOLLink`即使为了“连线功能公共化”而使用MPC同一类型语义，也不能让持久化逻辑连接同时被Core的`MPCConnection`索引和Logic Aspect写入；一条连接只在一个范围内拥有唯一ID和存储事实。

### 6.5 明确长期延期类型及启动条件

- `MRKControlRegion/BodyPlan`：只有真的出现`>4096`动态循环或明显性能压力且PoC证明可行时进入执行内核；G05a。
- `MOLDataFlowDefinition`、`MOCDataFlowPlanner`、Stream缓冲相关类型：只有具可重复FanIn/FanOut/背压/预算测试后独立建模；G05b。
- `MOLWorkflowDefinition`、`MRKDurableWaitHandle`：只有完成长等待/恢复/补偿/外部命令Unknown不重复执行的故障测试后实现；G05c。
- `MOLStateMachineDefinition/MOLState/MOLTransition`：必须在多层状态/并行正交区域案例中确定原生语义，而不能靠ControlFlow随意连线推断；G05d。
- `MLCBuildArtifact/DependencyLock/ReleaseService`：真正生产发布时必须有不可变构建身份、依赖锁、Stage/Activate/Rollback；G07。前3个30分钟先将身份字段约束写入注释和测试，不强行实现整个发布体系。

## 7. 两类用户的目标调用与完整对象边界

### 7.1 软件开发者最简入口（**拟议API，不是假称已可编译**）

```csharp
// 目标用法。真正实施时由一个MOCFlowBuilder完成临时编辑，最终一次Commit至MEM工程事务。
MEMEngineeringSession project = MEMEngineering.Create("在线检测工程");
MEMContent main = project.AddContent(project.Project, "主流程");

MOCFlowBuilder.Edit(project, main, flow => flow
    .Step("采图", "XRay.Acquire")
    .Step("AI", "Vision.Inspect")
    .Step("结果判定", "Inspection.Decide")
    .Connect("采图.Image", "AI.Image")
    .Connect("AI.Result", "结果判定.Result"));

project.SaveAs("Inspection.cxproj");
// buildService.Validate/Build/RunDevelopment委托MOC/MRK，不能由Core直接调用MRK。
```

**语义要求：** `Step(name, capabilityId)`返回可寻址块，块有永久BlockId；`Connect`只创建一条MOLLink；能力端口约束来自已注册签名；Edit回调统一获得一组变更并一次Core Commit，拒绝嵌套SetAspect事务；`SaveAs`保存的是逻辑定义，不是执行委托。真实API以第一批测试能编译的签名为准，示例不作为已实现声明。

### 7.2 工程编排用户自然路径

新建/打开Project → 选Content/新建流程 → 工具箱搜索专业能力 → 拖入已注册Block → 拖Port自由连线（如字段连接必须选类型化字段）→ 设置条件/循环/并行/子流程引用 → 验证并定位问题 → 保存重开 → 编译/仿真 → Trace覆盖在**同一图** → 受控发布。

用户可以从多个节点“提取为Content子流程”，系统自动建议输入输出Port和外部连接重接，用户确认后原子提交。触发未允许的循环、未连必需数据、重复ID或改变已发布组件签名时必须得到明确错误，不能以自动补线猜测用户意图。

### 7.3 运行时的数据具体化

```text
Invoke#1: MainFlow[SN=A] --引用同一AlgorithmFlow定义--> Frame#A(inputs:ImageLeaseA,RecipeA) → InspectResultA
Invoke#2: MainFlow[SN=B] --引用同一AlgorithmFlow定义--> Frame#B(inputs:ImageLeaseB,RecipeB) → InspectResultB
                                 ↑
               必需共享配置/设备/模型：按ID从Owner资源中心授权获取Lease
```

不得把Frame#A的值序列化进Content定义或复用给Frame#B。发生NG是业务结果；能力抛异常是执行错误；设备完成未知必须核对现场状态而不能自动重发危险命令。

## 8. 十六个真正架构问题最终裁决/延期边界

| ID | 架构问题 | V1.2决议 | 代码约束/验证 |
|---|---|---|---|
| Q01 | 自由连线与结构化谁是基础？ | **自由连线为新增流程定义的权威基础；结构化为合法连线具体化**。旧V1结构树只为兼容或显式结构化DSL | V2连接持久化唯一定义；旧流程编译结果不变；连线不能被画布坐标决定 |
| Q02 | 四种模型如何共享？ | 共享MEM/MPC/MOL端点基础，DataFlow/Workflow/StateMachine有不同语义规则 | 不能靠一个ModelKind枚举宣称四模型已运行 |
| Q03 | Content是一个还是多个逻辑入口？ | 首批一Content单默认入口；后续多入口要显示入口ID和版本 | 无Logic的HMI仍可工作；避免含糊默认 |
| Q04 | MPCConnection与MOLLink重复吗？ | **不是同一范围**：MPC公开资源间连线；MOL内部逻辑块Port连线；每条关系只存一处 | 内外端口映射显式，合法性跨层一致，删除无悬空边 |
| Q05 | 注册与发现是否一件事？ | 现有类型Provider+专业Capability Resolver是权威，Catalog只读聚合 | 类型/参数/Provider版本不同步时拒绝Build |
| Q06 | 两种编辑入口如何共源？ | C# Builder和Studio共用Core MEM/MTC事务，唯一Logic Aspect | Build源与Studio保存重开完全同一Block/Link ID |
| Q07 | 如何做到“绝不带冲突”？ | Revision、权限、索引、连接、引用、数据类型一致才允许提交 | 冲突拒绝、不自动合并、不默默更改运行中的工程 |
| Q08 | 子流程如何抽取并数据具体化？ | 定义引用复用；逻辑选区提取补充MTI机制；数据映射进入独立MDS帧 | 2次调用相同Definition得到隔离帧/SN和独立追踪 |
| Q09 | 动态循环如何不展开百万Step？ | MRK原生Region/重复BodyPlan在后续阶段完成；首批对旧4096上限保守报错 | 不能虚称百万轮已经支持；等待取消、内存预算测试 |
| Q10 | 巨型图像/三维体如何安全传？ | MDS预算/Lease/资源中心按标识领取，不放入工程XML/日志正文 | FanOut资源释放、取消、超大内存及多SN隔离 |
| Q11 | 旧运行实例如何映射旧定义？ | SourceMap携带Project+Build+Block+Invocation身份，发行版本锁定 | 编辑V2后，V1 Trace仍指向V1定义 |
| Q12 | 构建版本够不够严？ | 项目Revision不够，依赖能力/类型/算法/配方等版本锁定 | Provider替换不可静默改变同版本Plan行为 |
| Q13 | 断电后流程恢复安全吗？ | 复用MRK Journal/Recovery，未知物理动作必须探测/人工核对 | XRay/PLC命令无不安全重复执行 |
| Q14 | Studio调试是否越权？ | 编辑、观察、调试三态共图；调试/取消经过MRK权限Gateway | 未授权只读，生产运行不被调试画布自动影响 |
| Q15 | 布局能否影响语义？ | 布局/折叠/线路由由Visual维护并独立保存 | Move节点前后Logic语义Hash相同 |
| Q16 | 发布切换安全与否？ | Lifecycle固定BuildArtifact/依赖并调用现有MRK PlanPublisher | 正在执行任务继续旧Plan、可审计阶段切换和回滚 |

**本轮正式裁决** Q01～Q08 的基础边界和 Q15 的布局不影响执行；**基础协议预留、后续PoC** Q09～Q14、Q16 的完整执行策略。所有未实现部分标记为待开发，而非“冻结即完成”。

## 9. 三个约30分钟的下次编码工作包（不强行赶工）

> **准确理解时间：** 三个30分钟是下次一次工作会话的**时间盒目标**，不是承诺90分钟建完32方面496需求、四个执行模型、完整Studio或工业上线。阶段划分优先形成**一个CPU/虚拟能力可执行的“自由连线→具体化→运行/追踪”纵向切片**。如果测试/CI超过时间盒，不牺牲正确性，不宣称阶段完成；原计划G05~G08继续长期路线。
>
> **提交规则：** 在最新master工作，中文Commit、少量可验收提交，不新建csproj；尽量按当前28项目组织与已有测试框架。开始前读取最新四大文档中已可获得的材料、V1.2及源码；缺原件列风险，出现重大矛盾时停下来确认。

### 第1阶段（约30分钟）：Core自由连接**数据定义+原子持久化**

**目标：** 不替换老工程定义，支持最小可保存/重开的 Content 内自由块连接。

| 文件/项目 | 类/接口/枚举（已有/目标） | 本阶段精确工作 |
|---|---|---|
| `Mechanism/Core/Engineering/MOLLogicDefinition.cs` | `[R] MOLLogicDefinition、MOLControlBlock、MOLLogicCodec`；`[N] MOLLink、MOLBlockEndpoint` | 设计新版V2序列化块/连线、Entry/出口ID；旧V1 Codec仍可加载与编译；生成稳定BlockId/LinkId；新增同Content端点校验。支持最小 Action + Control/Data；其余模型不伪实现。 |
| `Mechanism/Core/Engineering/MPCPort.cs` | `[R] MPCPortKind / MPCPortDirection / MPCPortMultiplicity / MPCPortDescriptor(在MPMTypeDescriptor.cs)` | **优先不改已有MPCConnection格式**，复用端口类型/方向契约来校验MOLLink；必要时仅加纯校验工具。 |
| `Mechanism/Core/Engineering/MTCEditSession* / MEMEngineeringSession*` | `[R] MTCEditDraft、MTCEditSession、MEMEngineeringSession` | 提供原子Logic更新和expectedRevision检查；可新增纯`MOLLogicCodec.CreateAspect()`，一次Edit写入，不在Edit回调内嵌套`MOLLogicCodec.Set(session)`；违规更改彻底回滚。 |
| `Tests/ArchitectureSmoke` | 复用现有OrganizationLogicIntegrationTests | 新建3个Block和2个Link，保存/重开后端点、类型、BlockId/LinkId完全一致；旧V1工程往返继续工作。 |

**接口级目标：** `DefineBlock(capabilityId, id)`、`Connect(blockId,portKey,blockId,portKey,kind)`、`RemoveLink(linkId)`、`SaveAs/Open`。核心是**在一个EditSession批次内提交**，不要求第一批完成Studio拖线UI。

**反证测试（任何一项失败即不通过）：** 重复BlockId/LinkId；跨Content假端点；Input→Input；非法类型；缺必需Port；expectedRevision过期；Codec遇到旧V1定义或未知Schema。连接修改不污染原版本运行计划。

**阶段完成定义：** 真实源码和自动化测试能够证明“内存自由定义→合法性检查→原子提交→工程文件保存→重新打开→同等定义”；不依赖只在内存里成功的Mock。

### 第2阶段（约30分钟）：MOC连接分析、具体化和MRK CPU执行

**目标：** 把第1阶段真实持久定义编译为现有MRK可执行Plan，而不是另造第二引擎。

| 文件/项目 | 类/接口/枚举 | 本阶段精确工作 |
|---|---|---|
| `Mechanism/Orchestration/MOCEngineeringCompiler.cs` | `[R] MOCEngineeringCompiler、MOCCompiledPlan、MOCSourceMap、MOCStepOrigin` | 接受新V2连线式Logic；完成入口、可达性、Control顺序、基本数据类型与必要输入检查；对合法线性/简单分支图具体化成MRK Step/依赖，保留BlockId/LinkId诊断定位，旧V1原编译路径不退化。 |
| `Mechanism/Runtime/Execution/MRKExecution*` | `[E] MRKExecutionPlanCompiler、MRKExecutionEngine、IMRKCapabilityResolver` | **默认不改执行内核**，使用现有CPU Provider Resolver，执行并取得Trace；如扩展只允许必要的SourceMap身份互通。 |
| `Mechanism/Data/RuntimeData/MDS*` | `[E] MDSDataFrame、MDSDataStack` | 只验证现有每次调用数据隔离，暂不创建新Frame类或大对象资源中心。 |
| `Tests/ArchitectureSmoke` | 复用现有Logic集成测试 | 三节点顺序/最小分支、输入输出值在同一个run传递；连接冲突、环路、缺能力编译失败且不执行Provider；SourceMap能由Step追踪Block。 |

**最低执行闭环：** `Content(MOL V2 Blocks+Links)` → `Snapshot` → `MOC.Compile` → `MRK.StartFlow(CpuResolver)` → `Trace` → `MOCSourceMap` → `BlockId`。控制/数据连接必须真正参与执行而不是只画线；If/复杂Join若超出本阶段则明确拒绝，不能偷偷只按名字串行运行。

**阶段完成定义：** 一个保存重开的真实自由连线工程可无界面CPU执行、返回正确结果和Trace；无效连线拒绝编译，兼容V1测试保持通过。

### 第3阶段（约30分钟）：开发者薄API + Studio最小可编辑接续

**目标：** 两种使用者写同一持久定义，形成可演示的最小可用构建体验。由于UI工程改造可能超时，**优先保证C# API及Core/MOC回归，再实现最小WPF交互**；绝不把只读演示冒称拖放编辑器完整。

| 文件/项目 | 类/接口/枚举 | 本阶段精确工作 |
|---|---|---|
| `Mechanism/Orchestration` | `[N] MOCFlowBuilder（可选最小单类）` | `Edit(session,content,Action<Builder>)`、`Step`、`Connect`、`Commit`或等价便捷方法；内部只生成新定义并用Core一次事务保存；避免同时实现MOCFlowAuthoring。 |
| `Mechanism/Visual/MOLFlowLayout.cs`、`CxVisualEditor.cs` | `[R] MOLFlowLayout / CxVisualEditor` | 新连线真实布局投影、端点选择与连接命令，复用Core共享撤销/重做，不在Visual保存第二持久图。 |
| `Mechanism/Presentation.Wpf/MOLFlowCanvas.cs`、`Applications/Studio/StudioViewModel.Engineering.cs` | `[R] MOLFlowCanvas、StudioViewModel、MOLRuntimeOverlay` | CPU示例最小交互：增加Action→选择起止Port→请求连接→保存/重开→点击CPU仿真→在同图看到Trace（必要时拆成后续任务，不跳过测试宣称完成）。 |
| `Tests/ArchitectureSmoke / Tests/TestCenter` | 扩展现有测试 | C#创建、Studio同一工程定义读取后修改、重开再次编译；真实STA/WPF至少确认能选择/绘制Link。确认没有独立Studio `MOLLogicDefinition`私有存储。 |

**阶段完成定义：** 开发者通过自然API创建并连接三个Action，Studio可打开保存后的工程、显示连线并进行最基本编辑和运行观察；其修改再被代码加载/编译，BlockId/LinkId不丢。若WPF交互尚未全部达到，则阶段标**部分完成**并列残留工作，不以“三十分钟”替代验收。

### 9.1 每阶段统一验收及提交证据

每阶段须提供：修改文件/公开API及样例、单元测试及故障反例、实际构建命令/输出、Windows CI运行URL与Commit SHA（如CI正在运行写“待验”）、本阶段尚未覆盖的语义清单、回滚/兼容风险。建议阶段结束各一次中文提交；如果只生成了文件而真实测试没跑，不标“完成”。严禁自动部署真实设备或触发射线源/PLC危险命令。

### 9.2 本三阶段**不承诺**的完整功能（持续在既有路线图）

- 全功能拖放、自动排版、跨多屏Studio、子流程逻辑选区自动提取与批量接口推导；
- 大循环原生Region（非4096静态展开）、Stream缓冲/背压、大体积Lease性能验证；
- Workflow长事务/人工复判/掉电后安全恢复、StateMachine层级状态/并行正交区域；
- AI/CT/PLC/X-Ray真实硬件、专业能力完整注册/设备联锁/授权生产调试；
- Lifecycle依赖锁、BuildArtifact/签名/灰度发布/回滚，十案例实机验收。

这些属于G04～G08的独立长期工作包，仍在本文功能与类型台账中保留完整归位，不能因首个90分钟切片不实现而删掉需求。

## 10. 长期完成计划：G00～G08不丢、先薄切片后拓展

| 原Gate | 长期内容 | 阶段交付/真实验收 |
|---|---|---|
| G00 | A1～A10模型与83候选去重，源代码/依赖边界定案 | 本V1.2为执行建议基线；任何四大文档实质冲突须修订记录 |
| G01 | 完整MOL自由编辑、Block与Link稳定性、事务、undo/redo | 当前三阶段的第1段提供最小子集，随后扩至移动/复制/Extract及端口全验证 |
| G02 | C#自然API、专业能力统一发现、逻辑复用与子流程调用 | 第3段提供最小Builder，后续加目录/子流程/参数映射 |
| G03 | FreeWiring→语义规则→MRK计划/SourceMap/Build诊断 | 第2段提供ControlFlow薄切片，后续加四模型专用Planner与版本锁定 |
| G04 | Studio全功能组态/编排、三态、专业编辑器 | 第3段最小交互；后续完整C01 CPU/虚拟PLC+HMI流程 |
| G05a | MRK Region大循环及Invocation/Iteration | 非静态海量展开；取消/内存/隔离/性能证据 |
| G05b | DataFlow持续数据/Stream、FanIn/FanOut/背压 | C04图像与C05体数据预算/Lease/吞吐/长跑验收 |
| G05c | Workflow长等待、人工活动、Journal/Recovery | 断电Unknown外部动作不自动重试，真正恢复/人工核对 |
| G05d | StateMachine事件、层级并行、迁移 | 确定性、时序冲突、受控动作测试 |
| G06 | 六Professional项目和Runtime.Host/Connectivity真实接入 | C01/C04/C08/C05逐案例可复现，模拟与实机明确分级 |
| G07 | Lifecycle锁依赖、签名、Stage/Activate/Rollback | V1在途实例不被V2新计划污染 |
| G08 | 五个历史系统+十个案例、TestCenter完整验证 | 真工程文件/CI/报告/实机证据各独立判断，不宣称全部已完成 |

## 11. 源码状态与验证等级（本轮阅读结论，不是新执行结果）

| 主链路 | 当前事实 | 待实现/风险 |
|---|---|---|
| MEM/MPM/MPC | 工程、Page/Content/Symbol、资源公开Port/Connection、Session编辑事务已存在 | 新自由逻辑块Endpoint不能直接使用`MRIObjectAddress` |
| MOL | `MOLLogicDefinition.Root`树、SchemaV1和`MOLLogicCodec.Set/Get`已存在 | 自由Link持久源缺失；从合法连线具体化仍需实现 |
| MOC | `MOCEngineeringCompiler.Compile(snapshot,entry,resolver)`和SourceMap已存在 | 当前主要处理结构化块树，缺新连线V2验证与转换；编译步骤预算4096 |
| MRK/MDS | MRKEngine/Plan/Trace及MDSDataFrame/Stack有真实实现 | 不假定完整Workflow/Region/Stream模型已经完成 |
| Visual/WPF/Studio | `MOLFlowLayout`、`MOLFlowCanvas`、`StudioViewModel`有显示/CPU模拟 | 自由拖线与完整双向编排编辑仍待补 |
| 注册/Provider | `IMPMTypeProvider`与`IMRKCapabilityResolver`分别有真实契约 | 需要只读聚合目录及Version约束，不应双真源 |
| 发布/Lifecycle | MRKPublisher有Plan切换基础；Lifecycle目前骨架为主 | 发布包与依赖锁/签名/审计未完成 |

**匹配当前基线的CI参考（历史执行证据，非本轮新跑）：** [工程组织Windows](https://github.com/oudream/cx-visual-x3/actions/runs/39135030463)；[统一运行Windows](https://github.com/oudream/cx-visual-x3/actions/runs/39135030512)。正式开发后必须在新提交上重新跑。

## 12. 代码前置条件、拒绝/停工规则和交付清单

1. 读取并固定新的 `master` SHA；先检查已修改文件是否有其他并发工作。用户允许直接master提交，但不得覆盖别人未提交变动。
2. 确认严格零冲突：现有`MEMEngineeringSession.BeginEdit(expectedRevision)`、`MTCEditDraft`、`MTCEditSession`是唯一通路；不能绕过API直接改Children/AspectBytes。
3. 先在ArchitectureSmoke加入当前V1结构化编译/工程保存测试的回归断言。改变Codec前必须有旧格式用例。
4. 开发者门面、WPF/Studio与Host只使用单一正式Port/Link/能力身份；每次Save/Load必须保持ID与语义。
5. 分三段按“测试→实现→编译→运行→补文档”推进；每次20～30分钟只尽力完成一个可验收切片。遇到重大架构不一致、遗留工程不可读、CI失败、错误关联设备、凭空新增csproj时立即暂停并报告。
6. 最新标准：**只有API真实编译、工程文件重开一致、CPU运行输出正确且SourceMap可追溯，并有相应测试证据，才可称首个自由连线构建闭环完成**。
7. 首次开发交付：真实代码与变更清单、3个时间盒的进度和测试记录、构建回归、失败反证、Windows CI URL/SHA、遗留问题、后续G01～G08工作包拆分建议。

## 13. 给下一轮Codex的执行提示（可直接复制）

> 请依据本文件《CxVisual 工程构建体系——长远架构收敛、完整类/接口/枚举归位与三阶段开发计划 V1.2》，在 `https://github.com/oudream/cx-visual-x3` 最新 `master` 直接实施第9章三个约30分钟的最小纵向闭环工作包，**严格串行**执行。首选复用已有MEM/MOL/MPC/MOC/MRK/MDS/Visual/Studio项目及类型，新增仅最必要的MOL逻辑块端点/内部Link值对象；支持V1结构化工程兼容与V2自由连线持久化。每个阶段遵守事务/版本零冲突、真实TDD、CI证据及30分钟时间盒：测试不通过不得宣称完成，不得牺牲核心一致性赶工。第3阶段结束停止、汇报代码/测试/回归/未完项，不擅自进入G05～G08或动真实硬件。项目归属、已有235类和83候选处理以本文第5、6章为准。直接master提交，中文Commit，不创建分支或新csproj。

---

### 本文档可追溯来源

- `CxVisual_工程构建体系_全量类接口枚举_归位设计与代码状态_V1.0.md`：235项真实声明、82项候选、32方面映射、当时CI链接。
- `CxVisual_工程构建体系_长远架构问题与详细完成方案_V1.1_评审稿.md`：16问题、原82候选逐项分类、长期Gates。
- `CxVisual_工程构建体系_全量需求与案例源码实施矩阵_V0.9.xlsx`：原496条细项及双角色/十案例反证（其自身仍需四大文档原件交叉核验）。
- x3固定源码：`Mechanism/Core/Engineering/{MEMNode,MOLLogicDefinition,MPCPort,MTCEditSession,...}.cs`、`Mechanism/Orchestration/MOCEngineeringCompiler.cs`、`Mechanism/Runtime/Execution/*`、`Mechanism/Visual/MOLFlowLayout.cs`、`Mechanism/Presentation.Wpf/MOLFlowCanvas.cs`、`Applications/Studio/*`。
