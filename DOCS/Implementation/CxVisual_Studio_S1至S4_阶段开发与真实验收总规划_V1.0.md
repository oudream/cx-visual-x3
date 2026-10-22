# CxVisual Studio S1～S4：阶段开发、现状审计与真实验收总规划 V1.0

> 日期：2026-10-21；原规划基线 aba211cf；**横向源码与目标审计基线：master @ 7766955e06c23d226ac55fbee5fab6d2e0db0be9（详见第8～13节）**。
> 文档性质：基于已确定 Studio V1.2 设计方向、现有S1实施记录、工程呈现化EP01～EP15/R01～R48台账和当日源码/CI的**阶段实施规划**。阶段范围属于本文件的实施建议，**不伪称已从缺失的V1.2原稿逐字校对**；实际交付状态始终以GitHub最新master与同SHA Windows CI为准。
> 核心原则：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。

## 1. 权威基线、范围与冲突处理

- 架构：仓库AGENTS.md、cxvisual-engineering/SKILL.md、DOCS/00入口、01～04四大统一基线；唯一工程定义/Undo Owner为MEM/MTC，统一Schema由MPM，MOL逻辑定义，MOC构建与SourceMap，MRK执行调试，MDS运行时数据，专业语义留在Professional。WPF/Studio只负责呈现、操作及编辑器协调。
- Studio设计：用户已确定的《CxVisual Studio 统一界面架构与完整设计规范 V1.2》，固定九菜单“文件/编辑/视图/工程/调试/运行/发布/工具/帮助”，专业动态菜单与九项同一行，下方上下文Ribbon，再下方中央文档标签；AvalonDock 5.0.1、.NET Framework 4.8/WPF、浅色TestCenter与VS深灰、资源树和图形结构树分离。
- 工程呈现化：DOCS/Architecture/CxVisual_工程呈现化体系——完整需求_角色期望与使用样例_总体详细设计_V1.0.md（EP01～EP15、A/B/C案例）；DOCS/Implementation/CxVisual_工程呈现化体系_30分钟分轮实施与验收_V1.0.md（R01～R48和G1～G6阶段门）；DOCS/Implementation/CxVisual_Studio_S1_统一Shell与AvalonDock_实施验收.md（S1真实证据）。
- 当前可读取的DOCS/界面设计目录为00～09十个图文件；用户先前要求的V1.2全文及十一场景图未能在所审查仓库路径内完整定位。缺失部分列为**源资料待核**，不得捏造11张图已齐、把早期示意图代替最新设计或另起一套界面规范。
- 文中S1～S4是Studio纵向实施阶段；EP01～EP15是完整功能需求；R01～R48是跨阶段可提交轮次；G1～G6是工程呈现化验收阶段门；其他文档中用S1～S4表示“源码/Studio/CI/实机”的证据等级，不得混为同一编号含义。
- 现有S1 Shell可以验证并不代表S1全部人工交互签收，更不代表Studio工业使用场景完成。所有状态区分“源码已有、Windows已验、人工待验、未实现”。

## 2. 四阶段组织（续建，而非重新设计）

| 阶段 | 定位与目标 | 主范围及EP映射 | 交付验收界限 | 2026-10-21事实状态 |
|---|---|---|---|---|
| S1 统一Shell与工程工作区 | 有效、可靠地进入和切换任何工程资源与编辑器 | EP01、EP15基础：固定+动态菜单/Ribbon、AvalonDock停靠/浮动/自动隐藏、主题、布局、资源导航、Definition/Instance返回、Page/Content/Symbol/Flow独立Tab、Dirty和生命周期 | 实际STA+Windows回归、布局/Revision隔离；工程切换资源失效、关闭/重开/焦点、真实键鼠及DPI | **部分通过**：Page/Content/Symbol/Flow真实Tab与Dirty、SourceMap跳转；[历史Studio 20/20](https://github.com/oudream/cx-visual-x3/actions/runs/39557455651)，但当前审计SHA未自动触发同SHA Studio S1；多编辑器命令/焦点、键鼠与DPI待验 |
| S2 资源管理、配置组合编辑与统一命令 | 从资源树轻便进入真实配置并在所有Editor一致使用命令 | EP01/04/14：MPM Schema类型化1×N/2×N/3×N配置组合面板、定义/实例覆盖、资源/变量/表达式选择、只读混合值、编辑状态/Dirty；统一CommandId/Descriptor、Ribbon/快捷键/门禁及选区上下文 | 两类用户实操：操作资源、配置、校验、保存重开；命令禁用正确且不可误写其他Tab；Windows异常反证 | **部分通过**：R01～R04和P0-3已有类型、混合/实例覆盖及资源Schema的WPF/集成代码；但MPM/MHS通用组合配置、专业自动接入、统一命令仍未闭环 |
| S3 HMI图元、流程编排与交互编辑器 | 用户在同一Studio编辑完整HMI与多层逻辑，不再有基础/完整版两种体验 | EP02/03/05/06/07/08/09：九Shape/Path/Bezier/Group/Layer、Symbol/Content、文本图片安全SVG/Widget、Style/Theme/绑定/状态动画/事件；MOL主/子/子子流程可编辑、连线与参数、SourceMap来源身份 | A图元编辑完整场景、B电机Symbol两个MDS实例隔离与授权反证、主/AI/CV子流创建编排；单MEM历史、保存重开；图元与流程真实鼠键 | Core+SceneView+MOLFlowCanvas已有可复用实现，完整键鼠、画布工具与多Tab一致性、A/B最终签收尚缺 |
| S4 构建、运行调试、专业Viewer及总验收 | 完成工程设计→构建→运行/仿真→观察→调试→定位→修改再构建 | EP10/11/12/13/15：真实MOC构建/诊断/SourceMap、MRK调试/Trace/Watch、MDS监控、Vision/CT3D/Automation/Inspection/Simulation专业Viewer和安全边界；SDK、质量性能 | C X-Ray主/AI/CV子流：真实构建执行、故障注入后子→父→HMI定位、Watch与重建复跑；A+B+C跨场景同SHA Windows及真实人工、实机授权独立门槛 | 各Owner具部分API与回归；Studio端真实闭环、专业Viewer及实机安全未完整验收 |

### 阶段与R轮次的关系

- **S1** 重点承接R05/R06与EP01；接下来优先清理P0回归以及未覆盖的界面恢复/关闭/Dirty/失效对象。
- **S2** 集中收敛已有R01～R04的Schema功能及Studio统一Command/Ribbon，将缺口写为后续Rxx增量，不以“编号已排在前面”推断完成。
- **S3** 主要承接R07～R32（A/B图元/Widget/状态事件与MOL编排），先复用现成Core Editor和Flow Canvas，不重建模型。
- **S4** 主要承接R33～R48（MOC/MRK/MDS/专业Viewer/整体验收），和既有构建、统一运行、工程、测试体系并行，不重复Owner。
- G1～G6与上述S阶段互补：S阶段说明**Studio交付能力**，G阶段说明**完整工程场景闭环**。两者必须均有真实证据才能声称工业场景验收通过。

## 3. Studio的共同用户交互契约

1. 资源树选择是定位资源；双击或“进入”使用类型匹配的配置组合/图元/流程/Viewer Editor。Page是HMI，Content和Symbol都有可独立编辑的定义，流程Content有独立逻辑Tab。图元内部Group/Element只在独立图形结构树，不混入普通资源树。
2. 活动DocumentId + MEM ResourceId/DefinitionId/InstanceId/RunSessionId是UI上下文的稳定身份；不按标签标题、WPF控件实例或索引判定业务对象。Ribbon、快捷键、属性面板、工具箱、画布选择必须使用活动文档的实际上下文并保证命令源唯一。
3. 编辑态和运行态分离：配置/图元/编排修改只通过MEM/MTC；构建调用MOC；仿真/运行调用MRK与MDS；运行值、Trace、Viewport、Theme、停靠布局均不造成MEM Revision。设计态未授权设备命令必须拒绝。
4. 动态Tab独立视口、选择、订阅和生命周期，MEM Revision/Undo/Redo/保存只由同一ProjectSession维护。关闭Tab及时释放、跨工程拒绝已删除/旧会话身份；布局恢复不能改变工程。
5. 同一资源的图元Tab和流程Tab均须显示来自MEM的Dirty状态，引用影响标识（如“~”）只表示间接影响，不伪装成另一个工程定义或全局脏状态。Undo/Redo、Save、Reload、删除、依赖变更必须有正反测试。
6. 第一批交付不为每个专业做独立Shell；专业只贡献Resource Descriptor、Editor/Viewer、Schema、命令、Ribbon及调试适配。同一Shell支撑Automation/HMI/Vision/CT3D/Inspection/Simulation/TestCenter。

## 4. 两类验收用户与三个端到端场景

| 视角 | 具体必须可操作的验收 |
|---|---|
| 软件开发者 | 复用真实MEM/CxVisualEditor、MPM、MOL/MOC/MRK/MDS公共API；简洁创建多页面/Symbol/Content、编辑/校验/撤销/保存；非WPF可单测；WPF只投影，Owner依赖无环 |
| 工程编排用户 | Studio新建并管理资源、打开多个Tab、配置Schema、编辑图元/逻辑、保存重开、构建运行、查看Watch和点击定位，无须写底层代码 |
| A：复杂图元 | 九Shape及Bezier/Path、分组/锁显/对齐/变换、键鼠/快捷键/Undo、保存重开，同对象身份与Revision |
| B：电机Symbol | 一份Symbol两个实例，各自MDS绑定和状态不同；运行观察不改工程；未授权事件不操作设备 |
| C：X-Ray检测线 | 主流程/AI/CV子流程三个真实Tab；MOC计划、MRK运行，AI异常通过SourceMap追溯AI节点→父调用→HMI工位，修复后重建重跑 |

## 5. 交付门槛、证据和状态

- 每一增量轮必须完成：当前master/Owner源码审查 → 最小但完整的真实修改 → 有效与无效两种使用例 → 原有Windows x64 Build及WPF STA/Organization回归 → 需要时TestCenter/性能 → 更新对应实施台账与SHA/CI → 中文Commit。
- 状态仅用“未开始/进行中/已提交待CI/验证失败/部分通过/验收通过/阻塞”。功能类声明、模拟按钮、静态截图和其他SHA的成功均不能替代同SHA测试。
- 自动化通过与人工验收分开：至少执行窗口停靠/浮动/自动隐藏、鼠键、关闭取消、100%/150%/200%DPI、跨显示器以及Viewer HWND互操作等真实Windows桌面场景。涉及实机/授权/PLC/设备联锁必须专业现场证明，CI不能替代。
- 不重做已工作的MEM事务、MOL/MOC/MRK/MDS或独立Studio Shell，不增加项目。不因独立编辑器实例而创建新的持久工程树、Undo栈或运行模型。

## 6. 接下来按优先级连续实施

| 顺位 | 工作包 | 真实执行目标与验收证据 |
|---|---|---|
| 历史已通过 | **S1-P0-Dirty-Undo** | a4e94349已修复并经[17/17 Windows STA](https://github.com/oudream/cx-visual-x3/actions/runs/39553816491)验证；后续只做回归，**不再作为当前待开发P0** |
| P0-当前 | **S1/S2-统一Editor与Command契约** | 以现有CxVisualEditor、SceneView、MOLFlowCanvas与MPM/MHS服务为唯一能力来源；让Page/Content/Symbol/Flow的活动资源、命令、属性、Ribbon/快捷键/工具箱均用同一上下文；真实跨Tab/资源失效/权限反证，不做第二Undo或持久图 |
| P1 | **S2-配置与命令** | 先打通一个典型设备/算法Content的Schema组合配置全过程；再扩至多实例、资源/变量和专业Descriptor，强制真实事务/校验/保存重开 |
| P1 | **S3-A/B编辑闭环** | 把独立资源Tab的九Shape/精细画布接入现有完整Editor，不再长期保留“矩形演示栏→完整版”双界面；随后统一MOL流程、Widget、状态与事件 |
| P2 | **S4-C与跨场景交付** | MOC/MRK/MDS真实观察及调试定位、Vision/CT3D专业Viewer、A+B+C一体化，完成性能和安全门禁 |

## 7. 当前执行事实与待更新

- 最新核查基线为aba211cf1640e18b27ccbc421239c972471728f7；e06c2f26的Studio 16/16 STA及Engineering CI曾通过；之后新增P0-2 Dirty单测的007283b0版本Studio工作流[39551185708](https://github.com/oudream/cx-visual-x3/actions/runs/39551185708)实测失败，失败断言为“Undo产生的新Revision未更新引用影响与Dirty提示”，相同SHA的[Engineering Organization](https://github.com/oudream/cx-visual-x3/actions/runs/39551185744)成功。因此**先修P0，不能把历史的16 PASS冒充最新17 PASS**。
- **本轮P0修复已通过（源码SHA a4e94349）**：在Studio层为已提交MTC操作的TargetId保留稳定资源根身份（切换工程清空、最多65536条，超出后保守标记），用于Undo删除当前已不存在图元时定位直接修改Tab及引用受影响Tab；额外补充Redo不污染无关资源的反证。未修改MEM/MTC事务、历史或Revision语义。 [Studio S1 Windows #39553816491](https://github.com/oudream/cx-visual-x3/actions/runs/39553816491) **success，17/17 WPF STA通过**；[工程组织Windows #39553816541](https://github.com/oudream/cx-visual-x3/actions/runs/39553816541) **success**，同一源码SHA完成编译与回归。文档变更的后继提交不改变上述源码验收SHA。
- V1.2原始全文及用户提及的11张图与仓库内00～09的数量差异仍应后续对照。其缺失不影响沿已确认界面决议修复S1现实回归，但禁止擅自重新设计菜单或主布局。

## 8. 2026-10-21 横向审计：目标、基础文档、当前源码与可信证据

### 8.1 总结与资料边界

**裁决：现有F/M/P + MEM/MTC + MPM/MHS + MOL/MOC/MRK/MDS方向成立，不应推倒重来；但仅按S1～S4 UI功能逐项打勾，不能证明实现宪法要求的统一工业工程与运行平台。需要横向补上资源/能力声明、通用编辑器接入、跨专业执行、版本发布、Case验收四条约束。**

本轮阅读：`AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00...`、`DOCS/01_CxVisual_平台灵魂与架构宪法_V1.0_Review.md`、工程组织V1.1、工程呈现化EP01～EP15、配置体系V2.4、MOC构建V1.2、运行时数据V3.0、运行总体、S1～S4两份实施稿、R01～R48台账；检查`Applications/Studio`、`Applications/Runtime.Host`、`Mechanism/Orchestration`、`Mechanism/Presentation.Wpf`、`Professional`、Studio STA源码与Actions。

**资料缺口**：GitHub树已确认`DOCS/02...xlsx`、`03...xlsx`、`04...xlsx`存在，但本次GitHub连接仅获取其二进制文件元信息，**未完成工作簿逐Sheet/逐类型的单元格审查**；不宣称对02～04全量规格逐项审过。Studio V1.2原始完整文稿仍未在指定路径定位；`DOCS/界面设计`当前为10张00～09 PNG，用户所述第11张待核对。宪法01标注`Review`，为本轮最高架构评审基线，不冒称其全部ADR已定案。

证据分级：**D** 文档目标、**C** 源码实际存在、**T** 指定SHA Windows/STA自动通过、**E** 独立工程用户全链路验收、**F** 现场设备/权限/实时安全实测。**C≠T≠E≠F**。本轮是源码/文档/既有CI审计，**未新执行WPF桌面人工或实机测试**。

### 8.2 真实CI快照与重要盲点

| SHA/来源 | 已核结果 | 不得延伸的结论 |
|---|---|---|
| [Studio S1 / 39557455651](https://github.com/oudream/cx-visual-x3/actions/runs/39557455651)（2a6a31c3） | 历史20/20 WPF STA成功，涵盖R05/R06/P0-2/P0-3/P0-4 | 非本轮7766955e同SHA，且不代表实际键鼠/多屏DPI/工业Viewer |
| [Engineering Organization / 39561224209](https://github.com/oudream/cx-visual-x3/actions/runs/39561224209)（7766955e） | 本轮SHA工作流 success，编译/工程组织回归 | 不等于R01～R48逐项完整UI与现场验收 |
| [Unified Runtime / 39561224357](https://github.com/oudream/cx-visual-x3/actions/runs/39561224357)（7766955e） | 本轮SHA success | 非安全设备实时控制认证或全专业发布流水线 |
| [TestCenter / 39561224220](https://github.com/oudream/cx-visual-x3/actions/runs/39561224220)（7766955e） | 本轮SHA success | 不覆盖A/B/C全部端到端及专业实机 |
| [配置公共底座 / 39561224222](https://github.com/oudream/cx-visual-x3/actions/runs/39561224222)、[Config L1 / 39561224364](https://github.com/oudream/cx-visual-x3/actions/runs/39561224364)（7766955e） | 本轮SHA均success | 不表示MPM+MHS自动配置组合Studio界面完整通过 |

**自动回归盲点**：`.github/workflows/studio-shell-windows.yml`仅对`Applications/Studio/**`、`Mechanism/Presentation.Wpf/**`、`Hello/AvalonDock/**`与其自身变更的push自动触发；对`Mechanism/Core`、`Mechanism/Orchestration`、`Mechanism/Data`等Owner变化**不会因路径本身触发**S1 STA。该工作流还固定断言20项PASS，测试增量必须连同门禁更新；**Release Gate应对目标SHA显式运行Studio、Engineering、Config、Runtime、TestCenter适用测试矩阵**，不要拿不同SHA绿灯拼接为本轮同SHA完结。

### 8.3 纵横交叉差距矩阵

| ID | 宪法/EP目标与使用期望 | 当前真实证据（源码/测试） | 判定/缺口 | 唯一Owner与建议S阶段 |
|---|---|---|---|---|
| A01 单工程资源真源 | MEM Resource/Ref/稳定身份+MTC原子编辑/Undo/存储；多Tab不复制工程 | `Mechanism/Core/Engineering/MEMEngineeringSession.cs`、`StudioShell.ResourceDocuments.cs`；R06 Windows STA | **部分通过T**；跨工程版本包、完整资源类型/实例迁移及长稳仍待 | Core(MEM/MTC)；Studio S1/S2只投影 |
| A02 版本化资源生命周期 | Definition→必要时Resolve/Compile→Runtime→Provider；不同LifecycleKind受控声明 | 工程Resource、MOC/MRK已分阶段；代码库检索`ResourceDescriptor`命中主要为文档，Studio按资源类和字符串路由 | **关键横向缺口D/C**；不能宣称正式ResourceDescriptor/Capability注册与生命周期契约已落地；先查现有Owner接口，再裁决补齐 | Core/生命周期、Orchestration、专业Provider；S2+S4 |
| A03 编辑器资源匹配 | Page/Content/Symbol/Flow/专业资源进入正确Editor，独立Tab同项目历史 | `ResourceDocumentId`目前按MEM具体类型/flow分支；资源Editor复用CxVisualEditor/MOLFlowCanvas | **部分通过T**；相机/PLC/Recipe/CT等专业资源的通用接入仍不足 | Studio+Presentation.Wpf；S1/S2 |
| A04 Command统一且可授权 | 菜单、Ribbon、快捷键、工具箱、属性使用同一CommandId/上下文；不误写他页 | `StudioShell.cs`依据按钮文本`RibbonGroupOf`、`if(group==...)`动态加按钮；非Page资源Tab的旧Ribbon命令被拒绝 | **P0体验与扩展风险**；安全拒绝正确但用户体验不是能力完全接入；不要只把Guard换成另一批字符串分支 | Studio协调UI+各Owner命令；S1/S2 |
| A05 通用Schema/配置 | MPM工程对象属性 vs MHS配置Draft/Saved/Effective应语义区分，统一UI不复制Schema | `StudioViewModel.Engineering.cs`有真实MPM多实例/Mixed/只读等，`MainWindow.xaml.cs`有MHS Config demo；配置V2.4要求深层集合等 | **部分通过C/T**；不能把MPM属性行等价于MHS全配置编辑器；中央1/2/3列、List/Map、来源、申请生效/历史与权限未完整签收 | Core(MPM) / Data.Configuration(MHS) / Presentation.Wpf与Studio；S2 |
| A06 HMI完整图形与Widget | 九Shape+Path精编、Group/Layer/Style/Binding/Widget；任何资源Tab同一完整能力 | `MainWindow.xaml`旧完整编辑面板；`ResourceDocumentContext`局部只提供“添加矩形/完整图元编辑”跳转；底层SceneView等已有 | **部分通过C/T**；存在基础Tab与完整旧编辑器双体验，需按Owner共享编辑命令，不能创建第二编辑器机制 | Mechanism.Visual/Presentation.Wpf + Studio；S3（S1先协调） |
| A07 逻辑与构建 | 单一MOL定义、专业Port/子流程；MOC验证+SourceMap；失败不能生成可执行计划 | `MOCBuildService`、`MOCEngineeringCompiler`、`MOCFlowBuilder`、`MOLFlowCanvas`与工程回归存在 | **部分通过T**；全逻辑Kind、异步/Job调用、跨专业参数/流语义与工程用户编排验收不足 | Core(MOL)、Orchestration(MOC)、专业Compiler/Provider；S3 |
| A08 源码定位与版本一致 | MRK Trace/SourceMap可反查子流程→父→HMI且能重新构建 | `MainWindow.xaml.cs`已有MOC/MRK示例与来源定位；Studio P0-4 20/20测试通过 | **局部通过T**；尚需真实异常路径、版本过期拦截、子父HMI与新计划完整闭环 | MOC/MRK提供可信来源；Studio S3/S4显示 |
| A09 Runtime与Studio独立 | Studio关闭仍能运行已发布稳定计划，不能直接执行Designer可变对象 | `Applications/Runtime.Host/UnifiedRuntimeHost.cs`提供MRK会话/Journal/本地模拟授权；Studio仍有`SimulationResolver`演示 | **机制原型C/T**；独立Host当前是软件模拟蓝图，不等于真实MEM工程Release包进入无人值守现场Host | Runtime.Owner+Lifecycle+Professional；S4（S2提前打最短链） |
| A10 发布/版本回滚 | Resolve Manifest锁定工程、模型、驱动、Provider/Capability版本；Target Precheck/Stage/Verify/Atomic Switch/Rollback | `MOCProjectRelease.cs`有Fingerprint、Prepare、Stage、检查工程Revision与能力版本；`MainWindow.OnPublishConfigDemo`明确提示跨机台发布未接入 | **阻断生产目标**；有发布候选不等于完整Manifest/部署/现场回滚 | MOC/Lifecycle/Runtime Host/专业Owner；S4，必须早期验证 |
| A11 多专业运行语义 | Common Graph可复用，Automation软实时、Vision Typed DataFlow、CT HeavyJob、Inspection Durable Workflow各独立Runtime；共用Invocation契约 | `Professional/Vision`有真实CPU阈值/连通域与配置；`CT3D`仅找到配置模型；`Automation`当前找到配置类；`Inspection/Simulation`目录当前仅项目文件 | **显著专业缺口C**；不把“已建项目”当算法、设备/实时、CT重建、专业流水线可运行 | 各Professional；S4逐专业接入，先两专业验证公共契约 |
| A12 大数据/实时/安全边界 | DataHandle/Queue/Budget、软/硬实时边界、授权Lease、Provider最终裁决；仿真与实机同定义不同Provider | Vision CPU Frame构造时克隆像素；MRK/运行时数据有基本预算/控制/授权；Studio CPU仿真 | **非工业吞吐/安全验收F**；图像体积、跨进程零拷贝、硬件联锁、断网自治和压力仍无本轮现场证据 | Foundation/运行各Owner与专业Provider；S4按实机Gate |
| A13 真实质量与回归 | 六角色/开发者API/工程用户Studio/CasePackage A+B+C及宪法十类架构攻击用例 | S1 20项WPF STA、工程、Config、Runtime、TestCenter CI存在 | **T已建立但E/F不足**；鼠键/高DPI、复杂多工程、权限失败、修改重构建/实机均需逐级验收 | TestCenter+各Owner、Studio显示；贯穿S1～S4 |
| A14 文档单一真源 | 统一接口与计划不出现两个现行进度判断 | 两份`S1至S4`有重复阶段描述，另一份仍引用007283b0失败为“首个必须修复”；`ProjectCatalog.csv`含历史Skeleton状态 | **文档治理P0**；本文件为唯一Studio当前状态主表，另一份保留细目作为静态清单/历史快照；不得以ProjectCatalog静态状态代替源码验证 | DOCS/Implementation治理，所有Owner提供证据 |

### 8.4 基础/机制/专业的职责审查

- **基础Foundation**：提供独立于专业领域的几何/数学/数据类型、线程队列/资源预算、诊断和稳定公共契约。不得把相机曝光、FDK、Robot Move等专业语义塞进Foundation。02～04工作簿细节待逐Sheet核对后再动其对象清单。
- **机制Mechanism**：MEM/MTC唯一工程编辑、MPM工程Schema、MHS配置Draft/Saved/Effective、MOL定义、MOC编译、MRK运行/调试、MDS运行值与DataHandle、Lifecycle发布，各自有唯一Owner。Studio只能协调和显示，不能造自己的Engine、数据堆、工程树、Undo；Common Graph ≠ 所有专业共用一个万能Executor。
- **专业Professional**：贡献可发现资源/能力/运行/Provider/Editor部分，保留Automation、Vision、CT3D、Inspection等语义及设备安全边界。当前还不能以Vision CPU阈值演示推断AI检测线、CT FDK重建或多工位工业处理完成。
- **Studio/Application**：稳定Document/Selection/Command Context，贡献编辑器、Ribbon/菜单、配置组合视图、运行调试/Viewer窗口和SourceMap跳转；不持久化工程真源，不直接代专业运行，不让普通专业接入反复修改Shell if/else。
- **运行Target与TestCenter**：独立Host承接发布固定版本和授权最终校验；TestCenter是独立测试应用/质量工具，不是第七专业Workbench，CasePackage应同时含正反案例、预期指标与可复跑证据。

## 9. 最重要的跨阶段门槛：两个新专业能否自然接入

仅列“有Editor、Schema、MOC、Runtime”等类型没有说明能否自由组合复用。建议在S2就引入**统一专业接入验收**，以两个不同专业资源复用同一公共面板和命令，避免专用按钮堆砌。

| 检验 | 示例及实际动作 | 必须通过的反证 |
|---|---|---|
| D1 开发者API简洁 | 专业开发者提供工业相机/PLC资源描述、Schema、Action/State；另一位提供Vision算法/图像能力；Studio应发现并打开对应Editor | 不改Studio通用菜单的if-else，不为普通字段重写WPF，不加第二工程树 |
| D2 工程用户可完成 | 新建相机/AI/CV资源→中央配置组合面板→主/子流程编排→保存重开→校验/构建→CPU模拟执行→Watch定位 | 不需直接操作底层类；编辑过期、非法值、只读值、未知能力均阻止错误提交 |
| D3 跨Owner贯通 | MEM Revision与MOC计划SourceMap→MRK运行固定版本→MDS观察→Studio源节点定位→修改→重构建 | 旧Trace误指向新工程、运行值改变MEM Revision、未授权真实设备执行均必须拒绝 |
| D4 第二专业证明共性 | 同一接入机制再接CT参数与重建Job/Viewer占位，或Inspection产品判定与流程 | 不允许把Vision专业语义刻进Common；专业只替换Provider/Editor/Compiler，复用通用UI |
| D5 独立Host | 使用真实工程对应的只读已解析/已构建版本经受控Stage到独立Runtime.Host，Studio关闭后运行 | 不能拿当前Host的本地代码Blueprint演示冒充生产发布，不能以软件Interlock冒充硬安全 |
| D6 质量/性能 | 人工键鼠、退订/重复打开、100/150/200%DPI、千对象多Tab、运行高频测点和大图/体积压力 | 句柄/内存单调增长、丢数据无诊断、未授权Action生效、跨Tab误写均判失败 |

## 10. 优化S1～S4推进：阶段保留，纵向可交付+横向验收同步

| 顺序与阶段 | 切片/归属 | 本轮实际完成条件（不仅类和按钮） |
|---|---|---|
| G0 当前审计完成 | DOCS治理 | 明确以本文件为唯一Studio实施状态主表，保留历史规格；指定最新SHA/CI与资料缺口，建立A01～A14横向矩阵 |
| P0 S1→S2 | 统一资源Editor/Command/Document上下文（Studio与Presentation.Wpf） | Page/Content/Symbol/Flow菜单、Ribbon、属性、右键/快捷键不误写且不以跳`doc.visual`替代；单MEM历史和真实画布功能，跨工程/删除/关闭反证 |
| P1 S2 | MPM工程属性+MHS运行配置的**统一视图体验、分离生效Owner** | 一个相机与一个Vision/CT配置的1/2/3列组合面板，类型控件、深层List/Map、权限校验、Draft/Save/Apply边界；单/多实例且可嵌入/独立弹窗；不重建Schema |
| P1 横穿S2/S4 | Resource/Capability/Editor/Provider贡献发现（优先复核现有接口，必要时ADR） | 第二专业接入时不改基础Shell分支，能注册资源描述/能力、类型Port、菜单命令与必要专业Viewer；受控拒绝缺失能力 |
| P1 横穿S2/S4 | 最短工程→MOC→MRK→MDS→Studio回路 | AI/CV两子流程真实MEM资源保存重开→Compile→独立Host使用同一版本/模拟Provider→Trace与SourceMap→修改重构建；工程/运行版本分离和错误反证 |
| P2 S3 | 完整HMI图元+MOL真实鼠键编排 | A复杂Shape精编全部验收；B电机Symbol/两实例/事件权限；C主/子/子子流程节点/类型Port/异常/重试完整行为 |
| P2 S4 | 发布、专业Runtime/Viewer、10类Case门槛 | 已解析Manifest/Package→部署版本核对/Guard/安全点/回滚；Vision/CT3D等按专业成熟度接入，后完成A+B+C联合及宪法十类Case逐步回归 |

**推进裁决**：S1～S4是开发职责分组，**不是等S3所有按钮做完才允许S4的最小运行发布验证**；先做薄而真、可验证的纵向路径，随后扩充编辑器/专业覆盖。R轮次、G阶段门与S阶段不能互相替代。每一阶段必须同时验收：（1）软件开发者自然API；（2）工程编排用户Studio操作；（3）工程身份/运行状态/版本归属；（4）失败/安全反证；（5）同SHA Windows相关CI及人工范围。

## 11. 对现有规划的直接修订决定（避免第二套规划）

1. **保留本文件为Studio阶段、差距、优先级和真实状态的唯一活跃主文档**；`CxVisual_Studio_S1至S4_完整阶段任务与新聊天接力_V1.0.md`只用作S1.01～S4.14的详细验收条款与历史接力材料，不再把其中“007283b0最新红灯”作为现状；R01～R48实施台账独立保留轮次证据。
2. 原有§2/§6/§7包含历史时点；本次已修正可确定的过期S1/S2状态和已完成Dirty-Undo，**以§8的新SHA快照覆盖此前旧进度**。不能删历史CI失败；历史失败是回归证据而不是当下待修。
3. 当前缺口不宜先写全新平台内核，只需先对现有Resource/MPM/MHS/MOL/MOC/MRK/MDS公共接口做接入点审计；确需修改公共契约时通过宪法要求的ADR + 至少两个独立专业Case证明共性。
4. 02～04的工作簿详细对象映射、V1.2文稿和第11张设计图获得可程序化内容后，专门补本矩阵的逐类型与逐图差异；**未复核前禁止据此改变已批准的F/M/P项目与Owner**。
5. 每次代码轮次在本文件记录“目标ID→源码/测试→对应SHA/CI→未验项”，让审计可以随新master演进，而不另建几十份报告。

## 12. 审计边界与阶段完成判定

**本次完成的是静态源码/文档与既有GitHub Actions证据交叉审计，不是新增代码功能或人工工业验收。**

- 当前设计方向：**继续**。现有MEM/MTC、MOL/MOC、MRK/MDS是可用基础，应优先组合；不另造并行内核。
- 当前落地效果：**尚未实现宪法完整工业平台目标**。S1 Shell和部分编辑/构建/模拟执行有真实证据，S2完整配置与低成本专业接入、S3完整同体验、S4生产Release/Target Runtime与全部专业场景仍存在明显差距。
- 下一轮成功要素：优先统一Editor/Command上下文，再以两专业配置/资源、同一工程跨专业模拟执行与SourceMap定位验证。**不要以S1单项20 PASS或所有现有CI success判定A/B/C、设备权限和整个Studio已完成。**

## 13. P0 S1→S2 第一轮真实开发：资源Tab统一绘图插入（2026-10-21）

- **源代码验收SHA**：[de3a024b](https://github.com/oudream/cx-visual-x3/commit/de3a024b4198e5be2dada7de899f7e5cf566afcc)，承接[29247c13](https://github.com/oudream/cx-visual-x3/commit/29247c13f5cecd01317df358d0da11747734cfb9)（最初实施）及[3e49294d](https://github.com/oudream/cx-visual-x3/commit/3e49294d4454fc17af2480b810a7637f393316cf)（STA编译重名修复）。本轮复用原`CxVisualEditor.AddShape/AddText/AddWidget`、`MVIShapeCatalog.CreateDefault`和MEM/MTC事务，未修改基础/机制工程真源及接口；无新增项目。
- **真正接入**：`StudioShell.ResourceDocuments.cs`基于当前活动DocumentId，允许Page、Content、Symbol真实独立Editor插入原九Shape、文字、Widget；复用原工具箱`OnInsertShape`与动态Ribbon操作入口。MOL流程Tab/失效资源明确拒绝插图元；Page的旧`StudioViewModel.ActivePage`仍在命令执行前同步，维持原R06业务协议，不误改固定主视图。
- **反证与Windows证据**：[Studio S1 Windows #39564306075](https://github.com/oudream/cx-visual-x3/actions/runs/39564306075) **success，23/23 WPF STA**；[Engineering Organization Windows #39564306081](https://github.com/oudream/cx-visual-x3/actions/runs/39564306081) **success**，同源码SHA真实编译与回归。新增测试验证九Shape、Widget、Content与Symbol编辑隔离、页面VM检视不误写、共享Undo/Redo、MOL流程拒绝及Tab复用；原R06也保留并通过。早期[Studio #39563979491](https://github.com/oudream/cx-visual-x3/actions/runs/39563979491)确实发现原R06上下文兼容失败，随后修复，不能掩盖历史失败。
- **完成判定**：**P0第一切片通过自动化**，并非S1/S2或完整图元编辑全部完成。现有排列/图层/属性/路径精细编辑及流程Command仍有旧固定Editor依赖；专业命令Descriptor、统一快捷键与右键、真实鼠标DPI、多Tab性能及工程到独立Host尚未完整验收。
- **下一切片**：在同一活动Editor上下文下继续统一对齐/分布、选择与几何修改命令，保持SceneView与图形结构/属性面板身份一致，不新增第二套Command或Undo；再展开S2完整Schema组合面板。

## 14. P0 S1→S2 第二轮：活动资源多选/排列/图层真实命令（2026-10-21）

- **本轮生产源码SHA**：[a8022b66](https://github.com/oudream/cx-visual-x3/commit/a8022b66975821da39b472baa1e4582e6dc07706)。继续使用`ResourceDocumentContext`与原`CxVisualEditor.Align(IReadOnlyList<Guid>,string)`、`SetZOrder(Guid,bool)`、MEM/MTC事务；没有增加工程树、项目、历史和专业执行Owner。
- **Studio有效功能**：Page/Content/Symbol活动资源的`SceneView.SelectedVisualIds`成为“排列”“图层”Ribbon的真实命令选区；新增可操作左/顶对齐、水平/垂直居中、水平分布、顶层/底层Z序。普通固定`doc.visual`仍执行原ViewModel路径；MOL Flow、失效资源、单选对齐及Z序非恰好单选均须拒绝，不能错误写入当前工程的其他资源。
- **完整可重复反证**：多选3图元一次MEM Revision完成对齐，Undo一次恢复原坐标、Redo恢复排列；与其他Page的VM检视状态隔离；层次Z序一次原MEM事务/撤销/重做；单选对齐不改Revision；流程Tab旧按钮不能改工程。同提交保留并行的P0-4真实X-Ray MRK/SourceMap/故障追踪自动回归。
- **同SHA Windows证据**：[Studio Shell CI 39565771432](https://github.com/oudream/cx-visual-x3/actions/runs/39565771432) **success，26/26 WPF STA**，[Engineering Organization CI 39565771340](https://github.com/oudream/cx-visual-x3/actions/runs/39565771340) **success**，[配置公共底座 39565771331](https://github.com/oudream/cx-visual-x3/actions/runs/39565771331) **success**；其他工作流必须独立核实，不替代同SHA全量Gate。
- **未签收**：独立Editor的路径节点精编、旋转/尺寸属性、右键/快捷键统一、复杂图层管理、WPF真实鼠键DPI/多显示器、专业资源可发现Command Descriptor，以及S2通用Schema组合编辑/独立Host发布仍未全闭环。下一轮优先补编辑器路径/几何命令和画布/属性上下文，然后验证专业Schema统一配置。

## 15. P0统一Editor/Command连续推进与代码验收结论（2026-10-21）

> 范围定义：对应本文件§6、§10所称**S1→S2 P0统一Resource Editor/Command/Document上下文**，不是S2通用Schema 1/2/3列配置、生产Release或S3/S4完整功能验收。判定坚持**源码/STA/人工/实机分开**。

### 15.1 已落实到真实源码的操作

| P0子项 | 实际接入点与唯一Owner | 自动化验收 |
|---|---|---|
| P0-A Page/Content/Symbol图元命令 | 活动资源Tab→`CxVisualEditor`：九Shape、文字、Widget、对齐/分布、Z序，单MEM/MTC历史 | [26/26 STA](https://github.com/oudream/cx-visual-x3/actions/runs/39565771432) |
| P0-B 几何/路径/控制柄与拖动 | 右属性面板→活动`CxVisualEditor.SetGeometry/EditPathSegment`；`SceneView.HandleEditRequested`→`EditHandle`；多选拖动→`MoveSelection`；重投影保留本Tab单/多选 | 首版[路径连续编辑失败](https://github.com/oudream/cx-visual-x3/actions/runs/39567391198)后[修复通过28/28](https://github.com/oudream/cx-visual-x3/actions/runs/39567592196) |
| P0-C 独立MOL流程命令 | 当前Content的Flow Tab→现有Capability目录、`MOCFlowBuilder.Step/ArrangeGrid`、Join既有方法；Ctrl+A/Delete/Esc/Ctrl+Z/Y与现有MEM事务 | [29/29 STA](https://github.com/oudream/cx-visual-x3/actions/runs/39568070033) |
| P0-D 多文档操作与关闭生命周期 | 资源Tab“选择/添加矩形”不再默认跳旧`doc.visual`，允许AvalonDock浮动；Ctrl+A对当前Scene；关闭Dispose Editor订阅、重开创建新视口保留原MEM | [30/30 STA](https://github.com/oudream/cx-visual-x3/actions/runs/39568418322) |
| P0-E 属性面板防跨资源误写 | 样式、Widget、状态/事件/动画/变量绑定调用既有VM前，按活动Tab/Scene选中的MEM真实视觉定义刷新选区；受限图片/SVG写当前资源 | [31/31 STA](https://github.com/oudream/cx-visual-x3/actions/runs/39568829529) |
| P0-F 底层变更触发Studio回归 | Studio S1 workflow监听Core、Visual、Orchestration、Runtime、Data、Lifecycle、Foundation、Professional源码变更，不再仅凭Studio/WPF路径触发 | 本行属于工作流触发策略修改；本次Commit对应CI完成后方可确认触发生效 |

**最新实际源码验收SHA**：[c696905c](https://github.com/oudream/cx-visual-x3/commit/c696905cc0418b13030a32ebeedafb1defcbdd7a)；[Studio Windows 39568829529](https://github.com/oudream/cx-visual-x3/actions/runs/39568829529) **31/31 WPF STA success**、[工程组织39568829447](https://github.com/oudream/cx-visual-x3/actions/runs/39568829447) **success**、[配置公共底座39568829522](https://github.com/oudream/cx-visual-x3/actions/runs/39568829522) **success**、[Config L1 39568829396](https://github.com/oudream/cx-visual-x3/actions/runs/39568829396) **success**。这四条针对同一源码SHA；后续仅文档/Workflow更新的提交要独立注明SHA。

### 15.2 不得错误宣称的完成范围

1. **代码与自动验收：P0约定的基础多编辑器路由、单MEM事务、精细编辑、子流程主要Ribbon与错误写入反证已经有实装和STA通过证据。** 当前资源命令仍由部分Shell判断和原VM处理，尚未达到S2.07完整的`CommandId/Descriptor`插件扩展、全专业命令可发现的架构目标，不作为“统一命令体系彻底完成”。
2. **Studio人工E级验收：未执行**。真机Windows桌面至少须实际重复执行：Page/Content/Symbol多窗口鼠标拖动和控制柄、Ctrl多选/右键/快捷键、流程端口连线、在各种工具面板焦点下切换Tab、浮动/停靠/自动隐藏后恢复、跨工程切换/删除失效/关闭取消、浅深主题及各项菜单、布局保存后重启。每项要有人工记录、截图/视频及失败反证。
3. **DPI与硬件F级验收：未执行**。100/150/200%DPI、多显示器/混合DPI、CT/VTK/WinForms/HWND嵌入Viewer、真实PLC授权联锁、长时内存/句柄泄漏、工业数据压力均不能用当前Windows STA代替。
4. **S2/S3/S4并非P0**：MPM/MHS通用Schema配置生成器、通用Descriptor/Provider、完整S3 UI工具和生产发布Runtime Host仍按既有阶段计划推进，不因本轮31项STA变绿就提前签收。

### 15.3 人工验收可复现记录模板

| 记录字段 | 验收要求 |
|---|---|
| 设备/系统 | Windows版本、GPU、显示器数量、分辨率、DPI、输入设备、AvalonDock实际加载版本 |
| 工程与操作 | ProjectId、Revision，Page/Content/Symbol/Flow ID、当前DocumentId；步骤与预期结果 |
| 异常反证 | 故意切换活动Tab/选旧资源/删资源/模拟错误Capability，确认拒绝且Revision不变 |
| 证据 | 截图或短视频、日志路径、测试人员、时间、结果；失败则关联Issue/Commit |
| 结论 | 自动CI A通过/人工E未完成/实机F未完成必须分别标记，禁止合并为“P0工业验收完成” |

**下一轮**：从S2.01～S2.09完善资源种类→Editor/Descriptor注册、MPM/MHS原生Schema组合编辑器与专业贡献发现；此处若因具体真实Studio交互验收暴露P0缺陷，直接回归P0修复，不新增第二套系统。

## 16. P0收敛复审：代码通过不等于全部签收（2026-10-21）

> 本节是截至源码[a78a89f1](https://github.com/oudream/cx-visual-x3/commit/a78a89f1a28b1ea046bbab92ca5d96a02c71ebe1)的**Studio与工程呈现化双范围审计**。P0-1～P0-4属工程呈现化工作包，Studio的P0 Editor/Command是另一个交叉切片，不应误用同一编号推断完成。只引用真实Windows自动化证据，不把尚未人工或现场实施的项目标已验。

### 16.1 收敛判定（目标→实装→剩余）

| 工作包 | 当前状态 | 实际代码与证据 | 不能视为完成的内容／下一道门槛 |
|---|---|---|---|
| 工程呈现化P0-1：工程依赖/重定向影响 | **工程API与T级回归通过；扩大范围待验** | `MEMEngineeringSession.AnalyzeResourceImpact/PreviewRetarget`，既有MEM依赖与MTC事务；[工程Windows 39549728186](https://github.com/oudream/cx-visual-x3/actions/runs/39549728186)成功 | 已构建MOC计划失效、跨Provider真实部署和长期资源迁移仍需集成与异常反证 |
| P0-2：同MEM的多资源独立Editor | **自动化主要闭环，人工待签** | Page/Content/Symbol独立Scene，Content/Flow独立MOL；同MEM Undo/Redo/Dirty/焦点/关闭/保存重开；[Studio 31/31](https://github.com/oudream/cx-visual-x3/actions/runs/39568829529)和[最新33/33](https://github.com/oudream/cx-visual-x3/actions/runs/39576219329) | 浮动/停靠/自动隐藏、真实鼠键、屏幕切换/跨DPI、复杂样式/视觉选区联动未有人工E级实证 |
| P0-3：MPM Schema与真实资源编辑 | **部分完成，不能签全量** | AI/CV/Symbol/Page内Content属性Schema投影、旧Revision拒绝、Undo/重开已有STA；Content动态“绑定变量”刚修复并由第33项证明 | 第二真实专业Provider的Schema/复杂类型/权限与值来源、MPM/MHS Draft/Saved/Effective的操作体验以及统一Command Descriptor需进一步实现/验证；不以初版属性行等于完整通用配置编辑器 |
| P0-4：X-Ray主/AI/CV来源回链 | **工程CPU仿真主链通过，现场未验** | 实际MOC Build/SourceMap→MRK执行和失败→Trace→源Content/Block与父/HMI定位→修改重构建，旧ProjectId/Revision拒绝；[历史25/25](https://github.com/oudream/cx-visual-x3/actions/runs/39565607220)与[最新33/33](https://github.com/oudream/cx-visual-x3/actions/runs/39576219329)均含有关回归 | 多Provider版本/发布包交接、真实工业运行宿主、PLC/机台授权/联锁、故障现场及长稳性能不可由CPU软件仿真签收 |
| Studio P0：Editor/Command/Document统一 | **基础命令自动化通过；统一扩展机制未完成** | 资源Tab九Shape/路径/几何/多选排列、图层、Flow Step/Join/布局、快捷键、Inspector事务与失效拒绝；[最新33/33](https://github.com/oudream/cx-visual-x3/actions/runs/39576219329) | 仍见字符串驱动的Ribbon与旧固定编辑器混合，专业命令可发现、统一`CommandId/Descriptor`、所有右键/Flow Port/运行调试命令没有统一闭环。该差距保留S2 Owner，严禁将其记作“P0全部完成” |

### 16.2 本次发现并修复的证据遗漏

- 原`StudioShell.cs`的动态“绑定变量”在Content/Symbol Tab未启用活动资源路由，被`EnsureActiveStudioPageForRibbon`旧Page Guard拒绝。现复用原`StudioViewModel.BindVisualVariable`/MPM Owner，并在调用前校验活动`SceneView`的真实图元ID；不建第二绑定仓库。
- 原独立Flow Tab“校验编排”按钮同样使用旧Guard；现通过`MOCBuildService.Build(snapshot,活动ContentId,_simulation)`进行非破坏性构建校验，诊断附ProjectId、Revision、ContentId和原Block/Link身份，杜绝把旧主Page当当前资源。
- 修复提交：[a78a89f1](https://github.com/oudream/cx-visual-x3/commit/a78a89f1a28b1ea046bbab92ca5d96a02c71ebe1)；[Studio Windows #39576219329](https://github.com/oudream/cx-visual-x3/actions/runs/39576219329) **33/33 WPF STA成功**，[Engineering #39576219487](https://github.com/oudream/cx-visual-x3/actions/runs/39576219487)、[配置公共底座 #39576219204](https://github.com/oudream/cx-visual-x3/actions/runs/39576219204)同SHA **success**。Config L1应按最终工作流结果单独查验，不推定。
- CI门禁已在[67d3c75b](https://github.com/oudream/cx-visual-x3/commit/67d3c75b42328dba67be8f47e8fce13f33d54eca)扩大监听Core、Visual、Orchestration、Data、Runtime、Lifecycle、Foundation、Professional变更；但路径触发只能证明该工作流**运行时覆盖源码**，不是人工GUI或实机证据。

### 16.3 严格退出条件与后续顺序

**当前可以在工程日志中关闭**：原有明确测试范围的P0-1影响推导API、P0-2多编辑器基础事务、P0-4工程CPU仿真来源回链的已通过子项；关闭时必须附源码SHA/同SHA CI/测试说明。

**当前不能关闭**：工程呈现化P0-3完整专业Schema/通用命令、Studio跨编辑器全命令及人工E级、所有实机F级项目。不能改叫S2就视为原P0自动完成：需要明确迁移后的具体Owner、同等验收用例和追踪编号，保留未验状态。

最小继续工作：
1. 复核现有MPM/MHS与专业Resource Descriptor接入点，选Vision与另一不同专业跑通Schema编辑、保存重开、非法/越权反证；禁止重复Schema或项目。
2. 用至少两种资源Editor验证Ribbon/属性/工具箱/快捷键/右键同一个活动`DocumentKey`与命令状态；修完旧Page Guard造成的全部可复现漏口（如Flow Port等）。
3. 录制一份Windows桌面实际操作证据矩阵：真实鼠标拖动/多选/控制柄、浮动/停靠/自动隐藏、重启布局、100/150/200%DPI/双显示器、跨工程/已删除资源和关闭取消。无证据的格子保持“待验”。
4. P0-4实体机台发布/授权/联锁归后续专业/Runtime安全Owner并设置硬门槛；**软件CPU仿真通过不可转成现场合格**。

**本节最终判定：P0已实现的核心代码与自动测试相当扎实，但“整个P0真正签收完成”仍不成立；需要按上述未验项逐条补证，而不是再增加类似按钮数或盲目提升STA项数。**

## 17. P0流程属性收口与P1首个真实配置双专业场景（2026-10-21）

### 17.1 P0剩余代码漏口：AI/CV Flow Port属性一致性

- 原`MainWindow.SelectedFlowBlock/ApplyFlowProperty/RefreshFlowInspector`继续依赖固定`LogicHost`和`ActivePage.RootContent`，在独立AI/CV Flow Tab使用端口属性时可能错误定位到旧主页面；原“添加Port”Ribbon在非Page Tab又被旧Guard拦截。
- 本轮[b2791c5](https://github.com/oudream/cx-visual-x3/commit/b2791c5ef64014d5b66be18f9841cb45a486d016)按当前活动Document寻找真实`MOLFlowCanvas`和`MEMContent`，复用既有`MOCFlowBuilder.Edit`、MPM Port契约和MEM事务；把属性列表、节点选择、Port修改与撤销重做指向同一子流程，错误图元Tab拒绝。
- [Studio S1 Windows 39579075529](https://github.com/oudream/cx-visual-x3/actions/runs/39579075529) **34/34 WPF STA成功**，同源码SHA[Engineering 39579075624](https://github.com/oudream/cx-visual-x3/actions/runs/39579075624)、[Config L1 39579075690](https://github.com/oudream/cx-visual-x3/actions/runs/39579075690)、[配置公共底座 39579075692](https://github.com/oudream/cx-visual-x3/actions/runs/39579075692)均成功。由此**可关闭已规定的P0 Flow-Port软件代码切片**，但人工键鼠/工业设备不视为完成。

### 17.2 P1已真正启动：Studio doc.config双专业Schema组合编辑

- 以已存在的`MPWConfigEditorControl<T>`与`MHSConfigService`实现，移除中央`doc.config`的四块纯说明占位。面向**同一真实MEM ProjectId对应的MHS Project Scope**并列提供Vision.Inspection与CT3D.Reconstruction两栏真实配置控件，不创建Studio第二套Schema、配置缓存、运行堆或历史。
- 两个专业`CxVisionInspectionOptions`与`CxCtReconstructionOptions`保留各自独立SectionId和字段约束；公共控件负责字段/集合、草稿、预览与校验、Saved/Effective、待生效、历史版本及申请生效。普通配置编辑不会改变MEM工程Revision；Project切换时废弃旧作用域，关闭配置Document后创建新草稿Editor。
- 代码入口：[1f9136d](https://github.com/oudream/cx-visual-x3/commit/1f9136dcc4e5ca4fc462fc58e254f660aafbb089)、[103639e](https://github.com/oudream/cx-visual-x3/commit/103639e1499011bb597dfae70e152387509b9589)。实测源码[48f190ab](https://github.com/oudream/cx-visual-x3/commit/48f190ab96469c61fd190b9531d47b5d7236c25c)：[Studio Windows 39580735263](https://github.com/oudream/cx-visual-x3/actions/runs/39580735263) **36/36 WPF STA success**，[Engineering 39580735294](https://github.com/oudream/cx-visual-x3/actions/runs/39580735294)、[配置L1 39580735210](https://github.com/oudream/cx-visual-x3/actions/runs/39580735210)、[配置公共底座 39580735249](https://github.com/oudream/cx-visual-x3/actions/runs/39580735249) **同SHA success**。新增STA覆盖两个真实Schema、唯一Project Scope、非MEM Revision、Vision NextTask Saved与Effective分离、非法8-bit阈值拒绝及关闭重开。后续增加的切Tab后草稿保留测试需以其最新CI结果为准，不提前宣称通过。
- **P1仍未完成**：这里是两个已有真实专业配置模型的公共组合编辑首个场景，不等同任意专业资源自动发现和`CommandDescriptor`完整体系；尚无按资源实例层级动态聚合1/2/3列、自适应权限守卫全链、跨MHS/MEM参数映射及工程编排用户全流程人工签收。下一轮按现有配置V2.4 Owner契约补专业注册与多个Scope选择、来源/生效门禁，不重建基础服务。
- **现场约束**：软件STA只能验Studio操作契约和MHS保存/生效语义；不能代替运动控制、X-Ray联锁、硬件ACK、生产CT GPU任务、高DPI与长稳的人工作业记录。

**最新阶段判断：P0已定义的软件基础路由与流程Port代码切片均可按SHA+CI关闭；P0-3完整专业发现/统一命令、人工E级、现场F级仍有保留，严禁一笔划掉；P1真实双专业配置UI已开始并通过首轮自动化，但阶段总验收未完成。**

## 18. P1专业Schema配置动态注册与真实三专业验收（2026-10-21）

### 18.1 Owner及可用开发者API
- **实际实现**：`Applications/Studio/StudioShell.cs`继续复用`MHSConfigService`、`MVBConfigScopeResolver`、`MPWConfigEditorControl<T>`，增加`MainWindow.RegisterStudioConfiguration<T>(string title)`（可信宿主使用，`where T:new()`）。注册时通过现有`FCMConfigSchemaBuilder.Describe<T>().SectionId`确定稳定身份，SectionId重复拒绝；开放配置文档期间拒绝新增贡献，避免丢失未保存草稿；允许首次打开前注册，自动建立现有两个默认入口。
- **UI真实效果**：`doc.config`不再固定构造Vision/CT两种控件实例，而是遍历已注册专业贡献创建同一Project Scope下的MHS配置编辑控件；`UniformGrid`依据中央宽度采用**1/2/3列自适应**（源码实现，实际多显示器/混合DPI仍需人工）。出厂注册Vision.Inspection及CT3D.Reconstruction；测试由相同公共API额外接入**Automation设备配置**，证明第三专业无须修改组合UI。未新增项目或第二配置库。
- **真实源码**：[af4ae614](https://github.com/oudream/cx-visual-x3/commit/af4ae614239c5d28b3b0ffaaddf4d619315573bc)、[510ff332](https://github.com/oudream/cx-visual-x3/commit/510ff332a8eca4e72353079902fb738dad5623c7)（Automation第三专业WPF回归）、[c8daa186](https://github.com/oudream/cx-visual-x3/commit/c8daa186affd77bc5d8d2df87856de9bc7bb7ca3)（首次打开前注册默认Schema的初始化顺序）。
- **同一源码SHA Windows验证**：[Studio #39586341224](https://github.com/oudream/cx-visual-x3/actions/runs/39586341224) **38/38 WPF STA成功**；[Engineering #39586341221](https://github.com/oudream/cx-visual-x3/actions/runs/39586341221) **success**；[Config L1 #39586341312](https://github.com/oudream/cx-visual-x3/actions/runs/39586341312) **success**；[配置公共底座 #39586341234](https://github.com/oudream/cx-visual-x3/actions/runs/39586341234) **success**。新增用例验证Automation第三模型同Project Scope、去重、编辑文档打开时的注册拒绝、动态编辑器真正载入及组合容器；历史37项保留。
- **阶段门**：此轮可签收**P1注册驱动的专业配置组合编辑基础切片**，**不等于P1完整完成**。当前宿主显式调用注册API，不是按工程资源自动发现Provider/Command Descriptor；Project Scope已有，资源/产品/机台多Scope选取、授权策略重新验证、独立配置来源解释、真正WPF窗口宽度/DPI交互及用户操作均待实现/验收。专业运行时生效仍不能替代设备联锁与物理ACK。

**下一步优先顺序**：①统一工程资源元数据到配置贡献匹配的只读发现入口；②用户显式选择合法Scope与来源说明，保护Draft/Saved/Effective；③统一Command Descriptor接入主Ribbon/快捷键；④真实Windows多窗口/DPI手工验收。禁止复制MHS状态，禁止以模拟CPU发布签设备生产验收。

### 19. S2专业MHS资源配置生命周期/删除失效反证（2026-10-21，待CI）

- `OnStudioResourceMutation`接收到真实MEM删除时，若当前`StudioConfigScopeSelector`绑定已删除resource ID，关闭配置文档、失效草稿并回到Project Scope；原MHS审计历史不动、工程Undo可恢复同一稳定资源ID。不同MEM工程之间清理旧工程可信机台/产品临时Scope，不默继承旧授权。
- WPF STA新增资源删除时旧配置Tab关闭和Undo后重新发现的正常/负向场景；工作流按实际`CheckStep`计数修正为41项，保留并行CommandId新增测试。此项为CPU/STA边界，人工多屏DPI/实际PLC ACK仍待验收。

## 19. P1全范围退出审计：注册→资源Scope→授权→Command→生命周期（2026-10-21）

> 对照`CxVisual_Studio_S1至S4_完整阶段任务与新聊天接力_V1.0.md`的**S2.01～S2.12退出条件**，不把“配置中心能展示”冒充P1/S2完成。仍以宪法、配置V2.4、工程组织及F/M/P唯一Owner为约束。以下属于**新增可编译源码与真实自动化测试证据**；Windows人工GUI/实机安全另列未验。

### 19.1 实际源码增量及Owner归属

| 子能力 | 真实实现路径 | 关键不变性 |
|---|---|---|
| 注册式专业配置 | `Applications/Studio/StudioShell.cs` 的 `RegisterStudioConfiguration<T>(title,params resourceTypeIds)` 与 `FCMConfigSchemaBuilder.Describe<T>()` | 只登记Schema/Editor贡献，不重建任何字段目录、JSON/历史仓库；同SectionId重复拒绝 |
| 真实资源发现与Scope | 从`MEMProject.Resources`按稳定ID/TypeId枚举，另由可信宿主`RegisterStudioConfigurationScope(kind,id,title)`加入真实机台/产品 | 资源Scope不能由任意输入框伪造；只有显式声明的资源TypeId才匹配；Project/Resource/Machine配置实例身份互不混用 |
| Scope来源及自适应组合 | `doc.config`显示ScopeKind/ScopeId与来源，动态1/2/3列；每次变更均由`MHSConfigService.ForScope`创建原MPW Schema Editor | 配置Saved/Effective/待生效、动态集合、历史与审计仍由MHS唯一Owner负责 |
| 草稿保护 | `IMPWConfigDraftGuard.HasUnsavedDraft` + Scope切换拒绝 + `TryCloseStudioDocument` + Dock Closing保护 | 未保存草稿不允许无提示切换/关闭；明确放弃才释放。MEM资源被删除或工程已替换时旧配置Scope必须强制失效，避免向孤儿资源提交 |
| 项目切换与关闭确认 | `StudioViewModel.BeforeProjectReplacement` + `MainWindow.ConfirmStudioConfigReplacement` | 新建/打开（含discardCurrent路径）与退出均有确认，MEM Dirty与MHS草稿两个Owner独立验证；不把配置Save当MEM Undo |
| 软件授权 | `SetStudioConfigurationAuthorization(principal,policy)`→`MHSConfigService.Authorize`→`MHSConfigAuthorizedSection<T>`→`MPWConfigEditorControl<T>` | 授权拒绝不返回原始Section或暴露字段；每次Read/Edit/Commit/Apply由MHS策略动态检查。未配置授权时仅为可信宿主原有模式，不宣称已经存在企业IAM身份验证 |
| 统一专业Command贡献 | `RegisterStudioCommand(id,label,group,documentId,ownerICommand,key,modifiers)`；Ribbon、右键、快捷键引用同一适配器 | Owner持有真正ICommand；CommandId及快捷键去重；CanExecute与每次Execute检查活动文档。通过`GetStudioCommandContext`在调用时读取MEM ProjectId/Revision、资源与当前配置Scope，不维护第二套工程命令执行器 |

### 19.2 双角色的实际使用示例

**可信专业开发者（只需要现有POCO、MHS Schema/安全策略与Owner ICommand）：**

```csharp
// 所有专业配置仍复用Foundation的FCMConfig Schema与Mechanism的MHS服务。
studio.RegisterStudioConfiguration<CxAutomationDeviceOptions>(
    "Automation设备配置", "core.content");
studio.RegisterStudioConfigurationScope("machine", verifiedMachineId, "设备机台");
// principal与policy由真实可信宿主认证/下发；Studio不会代做认证。
studio.SetStudioConfigurationAuthorization(principal, policy);
studio.RegisterStudioCommand("automation.config.verify", "验证设备配置",
    "配置", "doc.config", existingOwnerCommand, Key.K,
    ModifierKeys.Control | ModifierKeys.Shift);
```

**工程编排者**：打开同一MEM工程→中央“资源配置”→选择工程/真实Content或已登记机台Scope→看到适配专业配置卡与来源身份→修改真实Schema草稿→验证及保存→区分Saved/Effective/待生效→必要时由经过授权的运行宿主申请生效。未保存修改切换Scope/关闭/换工程时必须确认或被拒；无法从没有授权的机台Scope看到配置值。

### 19.3 S2.01～S2.12逐项判定，禁止以“45项通过”代替退出验收

| 编号 | 判定 | 目前实际证据 | 仍缺少的完整退出条件 |
|---|---|---|---|
| S2.01 资源→Editor注册匹配 | **部分T** | Config专用Schema贡献+真实MEM TypeId过滤 | 图元/流程/专业Viewer统一Editor注册/默认候选及四类资源路由仍未全部描述化 |
| S2.02 稳定DocumentKey | **部分T** | MEM身份、活动资源Tab、跨资源隔离有回归 | 文档键尚未完全显式包含工程、Editor、必要实例路径的多编辑器唯一契约 |
| S2.03 类型Schema全覆盖 | **部分T** | MHS通用Schema控件原有Scalar/List/Map/校验，MPM工程属性有独立能力 | MPM工程资源Schema与MHS配置的统一选择/深层多类型表单使用尚未覆盖四个工业案例 |
| S2.04 中央组合编辑 | **基础T** | 真实三专业，响应1～3列，Project/Resource/受信外部Scope | 复杂折叠、搜索、字段来源和混合DPI真实GUI性能与体验待签 |
| S2.05 多选/Mixed/实例覆盖 | **部分T** | 已有MPM实例多选/Mixed/Revision保护及MEM原子事务 | 四类专业配置及多Scope组合的批量原子编辑/恢复默认和零部分提交用户链待验 |
| S2.06 分离Owner/权限/生效 | **主要软件T，现场待验** | MHS Save与Effective分离、权限拒绝、错误配置拒绝、草稿保护 | 真实IAM、生产机台应用边界/ACK、跨Provider合成来源/冲突锁定完整演练 |
| S2.07 命令Id/Owner复用 | **基础T** | 注册Owner ICommand可被Ribbon/右键/快捷键共享，失效拒绝 | 旧硬编码Ribbon/工具箱/所有专业命令全部迁移到一致目录与真实CanExecute状态尚缺 |
| S2.08 九菜单与上下文Ribbon | **部分T** | 现有九静态菜单、局部动态Ribbon与贡献命令 | V1.2全菜单/模式/权限/右键完整逐条审查，人工键鼠验证未完成 |
| S2.09 Dirty与Undo | **主要T** | 原MEM/MTC共享事务及Dirty反证、多资源Tab | MHS草稿（不等于MEM Dirty）在全部退出、回收及复杂焦点下的E级验证 |
| S2.10 生命周期/恢复 | **主要T，人工待验** | 当前Scope切换、Close、Dock取消、工程替换/资源删除失效拦截均有代码/STA | 多显示器、浮窗布局重启、耗时场景、长稳订阅内存泄漏仍须实测 |
| S2.11 四个典型工业案例 | **未签收** | Vision、CT3D、Automation三种真实Schema及Symbol/Content原有工程编辑测试 | 工业相机/PLC/配方/电机Symbol实例四种从API到真实Studio保存→构建→运行的完整用户Case及失败反证 |
| S2.12 全量质量与安全 | **未签收** | Windows Studio/配置/工程CI构成基础门禁 | TestCenter同SHA完整矩阵、人工100/150/200%DPI、多屏、性能与实体设备安全授权证据 |

### 19.4 真实CI与严格边界

- **软件增量关键提交**：配置资源Scope[261ae203](https://github.com/oudream/cx-visual-x3/commit/261ae20381e192801858d279331a752d780c2324)、草稿Guard[bd70a108](https://github.com/oudream/cx-visual-x3/commit/bd70a10838fffb6c4f04912b9eb7b4efec3bd228)、统一命令[8d4260cb](https://github.com/oudream/cx-visual-x3/commit/8d4260cbc6e64befd8d5559fa5d41cd3fdb378ba)、MHS授权[990fb474](https://github.com/oudream/cx-visual-x3/commit/990fb4742fab7e6542d1e09d60c2015d1101ed27)、闭包/工程更换与确认[abb3b566](https://github.com/oudream/cx-visual-x3/commit/abb3b566d083744435cef49d4179987174d891a8)。
- [Studio 44项 Windows STA #39593214170](https://github.com/oudream/cx-visual-x3/actions/runs/39593214170)、[工程组织 #39593214157](https://github.com/oudream/cx-visual-x3/actions/runs/39593214157)、[配置L1 #39593214186](https://github.com/oudream/cx-visual-x3/actions/runs/39593214186)、[公共底座 #39593214275](https://github.com/oudream/cx-visual-x3/actions/runs/39593214275) **均 success，同SHA资源删除草稿隔离之前置基线**。
- 后继工程替换门禁与第45项STA源码[6e2bab40](https://github.com/oudream/cx-visual-x3/commit/6e2bab4016449ae7b237b2893f36778552414b05)，[Studio Windows #39593698537](https://github.com/oudream/cx-visual-x3/actions/runs/39593698537) **45/45成功**；[配置公共底座 #39593698651](https://github.com/oudream/cx-visual-x3/actions/runs/39593698651) **success**。Config L1同SHA如未最终返回结果，必须继续查实后才标“通过”。
- **严格总裁决**：此轮已建立P1核心“可注册、可发现、可授权、可保存、可追溯、可安全切换”的**软件基础链**，能够逐项关闭上述明确通过的**代码切片**；但S2.01/S2.02/S2.05/S2.07/S2.08/S2.11/S2.12仍未满足全部正式退出标准，**不得将整个P1标记为已完整验收**。下一步优先完成统一全Editor/Command迁移、四类专业案例与Windows E级操作，不以重复设计基础库替代。


## 20. P1-A资源→Editor统一发现与DocumentKey软件切片（2026-10-22，待同SHA Windows CI）

- 新增 `StudioShell.ResourceEditors.cs`，通过当前MEM真实资源身份提供 `GetStudioResourceEditors`、`OpenStudioResourceEditor`、`RegisterStudioResourceViewer`。内置Page/Content/Symbol设计与Content流程继续使用原CxVisualEditor/MOLFlowCanvas，不复制工程真源。受信Professional Viewer使用工厂贡献，只持有视图和公开MEM身份，业务状态仍归专业Owner。
- 专业Viewer的DocumentKey显式包括ProjectId、ResourceId、EditorId和可选InstanceId；现存内置Page/Content/Flow旧ContentId为布局兼容保留，同时通过`GetStudioResourceDocumentIdentity`暴露等价强身份。不同Editor/实例不共用视图，关闭后释放可重新打开，工程切换及MEM资源失效立即回收；树双击采用统一候选默认选择。
- WPF STA新增资源/编辑器匹配、非法EditorId、Viewer重复注册、不同实例隔离、活动命令资源身份、关闭重开验证，原46项不移除。**此节为开发记录，Windows构建/测试尚待CI实际回报；不能据此将S2.01/S2.02全部签收。**
- 仍需：真实专业Viewer功能、内置Document ContentId迁移/布局向后兼容、复杂实例属性与配置UI衔接、人工Dock/DPI/双屏/长稳。按现有S2.01～S2.12状态继续追踪。

- P1-A增补：专业Viewer布局恢复保留同一视图与身份、静态XML格式校验与活工程身份校验分离；新增实际MEM Undo删除临时资源后自动失效反证。CI需以最终SHA为准。

### 20.1 S2.07 Editor级Owner ICommand切片（2026-10-22，待同SHA CI）

- 在既有`RegisterStudioCommand`目录中增加`editor:<EditorId>`作用域与`RegisterStudioResourceEditorCommand`，对现有`ICommand`只包装一个活动Document身份校验适配器；同一专业Viewer的不同MEM资源Tab复用统一命令，Ribbon/右键/快捷键动态读取活动ResourceId/Revision，不建立第二套Command真源。
- 追加WPF STA正反场景：两相机实例、未授权CanExecute、快捷键禁用、旧Tab右键调用、Tab切换、主页失焦隔离与关闭。门槛同步为48项；代码/CI自动结果与E级人工/真实设备操作仍独立。

### 20.2 S2.04真实配置组合搜索/折叠（2026-10-22，待同SHA CI）

- 基于原`MHSConfigService`与`MPWConfigEditorControl<T>`，在`doc.config`增加按专业标题/SectionId的即时筛选、配置卡原生`Expander`折叠及显示数量，保持项目/资源/机台Scope、Saved/Effective、草稿/授权不变。筛选只操作WPF可见性；不会重建Editor或写MEM Revision。
- 追加WPF STA大小写搜索、可见性、折叠持久状态、文档身份和Revision反证，第49项；混合DPI/大Schema列表仍需真实桌面性能验收。
