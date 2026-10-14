# 统一对象与多 Aspect V1.2：代码迁移状态

基线：`CxVisual_统一对象与多Aspect架构设计基线_V1.2.md`，以及仓库内 `Archs/统一对象模型与多维能力架构` 的 V1.1 文档与图。状态按代码和测试判定，不把设计图当成已实现功能。

## 平台基础阶段进展（2026-09-22）

| V1.2 约束 | 当前代码状态 |
|---|---|
| `Project → Workbench → Page` | `ProjectModel.Workbenches`、`PageModel.WorkbenchId`；工程保存/加载与归属校验已覆盖。旧工程没有 Workbench 注册时继续按旧格式加载。 |
| `Page → Content` | 可选 `PageModel.RootContent`；旧 `Elements` 页格式继续读写，新页面不同时序列化第二份元素树。 |
| Content 所有权与复用 | `OwnedContents` 是所有权；`ContentReference` 是实例引用；共享定义单独存于 `contents/*.json`，按稳定 ID 校验目标和循环。 |
| Symbol 定义与实例 | `SymbolReference` 持有定义 ID、实例 ID、版本和实例差异值；不复制 Symbol 定义。新模型校验被引用定义的 Root Group。 |
| Content 空间属性 | Bounds、Transform、Visibility、Enable、ZIndex、ClipToBounds 已建模；非法数值被拒绝。布局与裁剪的 UI/渲染实现尚未完成。 |
| 可选 Logic Aspect | `ContentDefinition.Logic` 与 `SymbolDefinition.Logic` 可保存 `LogicDefinition / LogicUnit / LogicLink / ExecutionPolicy`；校验端点、ID 与策略；无 Logic 的对象不强制创建。 |
| 后台 Aspect | `ProjectModel` 与 `ProjectWorkbenchDefinition` 可按需持有 Logic/Data/Collaboration，不虚构隐藏页面。 |
| Data / Collaboration 定义 | `DataSlot/DataReference/DataBinding` 与 `ContractReference/CapabilityReference/EventRoute` 按需附着；工程级 DataBinding 已校验来源对象、Slot、类型和变量 ID，本地引用校验与工程回读已覆盖。对象复用引用与能力调用引用分离；显式绑定的 CapabilityRef 可解析为公共 InvocationSpec。 |
| 数据运行边界 | `RuntimeDataScope` 以实例地址隔离值与 DataRef；`IDataCarrier` 的内存、文件映射和共享内存载体提供 Load/Unload 与租用延迟释放。尚未做 GB 级压测。 |
| Logic 编译边界 | 新 `LogicDefinitionCompiler` 将 LogicDefinition 冻结为内部 `LogicPlan`，拒绝未标注 Loop 的循环；不依赖公共 GraphNode/Edge。尚未连接执行 Adapter。 |
| Runtime Projection 边界 | `UnifiedRuntimeProjectionIndex` 将页面根、Owned Content、ContentRef、SymbolRef 和视觉子对象投影为实例地址与绑定上下文；`RuntimeProjectionService` 只写实例状态，复用定义多实例隔离。 |
| 新建与复制 | 显式使用新模型的页面只有一个 RootContent；复制页面保留 Workbench、Owned Content、引用、Aspect，并重映射 Content、Element、Logic、Data 与 Collaboration 的局部 ID 和对应局部引用。旧 Studio 的 Symbol 创作仍写 SymbolInstanceElement，故暂不把普通新建页面默认切成 RootContent；旧页面继续按 Elements 格式读取。 |
| 格式版本 | 新工程格式 V6；V5→V6 迁移不隐式改造旧页面，防止旧 SymbolInstance/行为丢失。 |
| 旧 Graph 边界 | 旧内核隔离到 `Platform/Compatibility/LegacyGraph`，保留原命名空间和加载/运行适配；Studio 不再允许新建 `graph.workflow` 顶层资源。此举不是 Graph 语义迁移完成。 |
| Logic 输入输出契约 | `LogicDefinition` 与 `LogicUnit` 有可选类型化输入/输出端口，`LogicLink` 保存端口键并校验类型；DeepClone、页面复制、持久化和内部计划 Hash 保留端口语义。旧 `Graph` 的线性控制流可作定义态结构投影；复杂分支、子图、数据流、参数等明确阻断，未自动删源资源，也未宣称执行等价。 |
| 旧页面显式迁移 | 非空旧 `Elements` 不再由 RootContent setter 暗中合并；显式迁移保留图元 ID，遇到旧 SymbolInstance（包括嵌套 Group）原子拒绝。 |
| 设计态自有 Content 索引 | 工程/页面对象索引覆盖 OwnedContents 的图元，并保留直属 ContentId；不把复用定义 ID 误当运行实例地址。 |

## 尚未完成，不得对外宣称闭环

1. 新 `LogicDefinition` 已可编译为内部计划，但尚未接入 Operation Provider、调度、断点/单步和同一 Logic View 的设计/运行投影。旧 `Graph` 仍承担已交付资源的运行兼容；新业务代码不得继续把它当公共组织模型。
2. `Data Aspect` 定义与基本载体已建立；DataBinding 目前只做工程定义级断链/类型校验，复用对象的实例上下文运行解析、与既有 `DataHandle`/变量运行链的整合、共享内存跨进程验证及 CT/图像 GB 级压力测试尚未完成。
3. `Collaboration Aspect` 目前是可持久化且可校验的契约声明，尚未通过统一对象引用绑定实际 Invocation/事件总线、安全与 Trace。
4. `RuntimeProjectionService` 尚未接入现有执行会话与 Studio 运行视图。Content 的 Layout/Clip 已建模，WPF 的真实布局、渲染与选择尚未接通。
5. Studio 仍主要编辑旧 Page/Element 与旧专业资源；`RootContent`、`ContentRef`、`SymbolRef` 和 Logic View 尚无完整可视化创作、撤销、调试界面。
6. 现有 8 个工业案例是 8 个可加载的示例工程与外部场景资源；尚未合并为单工程，也未达到图示全部工业级 UI/真实设备运行闭环。
7. 旧 Graph 仍有真实依赖：Studio 旧资源编辑器与浏览，Automation/Motion、Vision、CT3D、Inspection 的节点/领域模块，以及 Graph 运行会话。它们现在属于兼容链，不能因目录搬迁就宣称已被统一对象吸纳。`LogicDefinition` 当前仅表达单元、连接、策略，尚不足以无损覆盖旧 Graph 的类型化端口、参数、子图、分支、数据流和调试语义；在契约、执行适配和迁移回读完善前不得批量删除旧实现。

## 后续迁移顺序与验收

1. 完成统一对象在 Studio 中的创建、浏览、选择、编辑、撤销/重做和稳定引用解析；旧 Page 通过显式迁移转换，不能隐式丢失 Symbol 实例或行为。
1a. 先定义 Logic 的输入/输出、条件、数据/事件、子流程及同一 Logic View 的必要语义；建立旧 Graph → Logic 的逐资源迁移报告，只迁移可无损表达的资源。旧资源先保持兼容只读/可打开，迁移后对照执行 Trace 与输出，再移除旧领域模块及公开 API。
2. 建立 LogicDefinition 到现有公共 ExecutionPlan/Runtime 的适配，并用同一 Logic View 展示设计、调试、运行状态；再迁移旧 Graph 资源，保持旧工程可加载。
3. 连接 Data/Collaboration Aspect 与数据句柄、变量、能力调用、安全、Trace，并做资源隔离和异常恢复测试。
4. 每段以整套解决方案构建、全量回归、旧工程迁移回读、Studio 实际编辑/运行作为门禁；门禁未过不切换默认格式。
5. 基础平台闭环后，将 8 个案例组织进一个工程：每个案例独立页面与场景资源，按 Workbench 归属；共享定义通过稳定引用复用，运行实例、变量作用域、Trace 与发布版本相互隔离。不要将场景写死在 Studio 代码里。

优先级：V1.2 是当前统一对象和 Graph 边界的准则。早期《平台灵魂》中的“公共 Graph Model”仅作为已有资源/算法的兼容描述，不再指导新领域建模。

当前验证：`CxVisualX2.sln` Debug 16 项目构建成功（0 warning / 0 error）；基础回归 237/237，Architecture 7/7，Phase2 21/21，Phase3 14/14。较早一次全量运行遇到风险台账 R-15 的本机 CLR 间歇性 MissingMethod，后续全量运行通过。构建和测试结果不代表 V1.2 全部目标完成。
