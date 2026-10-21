# CxVisual Studio S1～S4：阶段开发、现状审计与真实验收总规划 V1.0

> 日期：2026-10-21；核查基线：cx-visual-x3/master @ aba211cf1640e18b27ccbc421239c972471728f7。
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
| S1 统一Shell与工程工作区 | 有效、可靠地进入和切换任何工程资源与编辑器 | EP01、EP15基础：固定+动态菜单/Ribbon、AvalonDock停靠/浮动/自动隐藏、主题、布局、资源导航、Definition/Instance返回、Page/Content/Symbol/Flow独立Tab、Dirty和生命周期 | 实际STA+Windows回归、布局/Revision隔离；工程切换资源失效、关闭/重开/焦点、真实键鼠及DPI | 基础Shell与多资源Tab已有代码；R06新增的第17项Dirty/Undo回归在007283b0的S1 CI失败；人工仍待 |
| S2 资源管理、配置组合编辑与统一命令 | 从资源树轻便进入真实配置并在所有Editor一致使用命令 | EP01/04/14：MPM Schema类型化1×N/2×N/3×N配置组合面板、定义/实例覆盖、资源/变量/表达式选择、只读混合值、编辑状态/Dirty；统一CommandId/Descriptor、Ribbon/快捷键/门禁及选区上下文 | 两类用户实操：操作资源、配置、校验、保存重开；命令禁用正确且不可误写其他Tab；Windows异常反证 | R01～R04已有部分Schema投影与测试，配置组合面板和全命令收敛仍缺，未完整验收 |
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
| P0-立即 | **S1-P0-Dirty-Undo** | 修复最新Studio S1红色CI：保存后Undo一个已删除图元，TargetId不在MEM当前索引，必须从编辑时的稳定资源归属投影识别对应Tab为“*”并更新引用方“~”；Redo与无关Tab反证；绝不改MEM通知协议或伪造Revision |
| P0-后续 | **S1-P0-生命周期与命令收敛** | 补全部Tab保存重载、关闭释放订阅、旧资源失效、Dirty/Save、快捷键一致性；中心焦点与Ribbon不会跨页误编辑。独立Editor仍复用既有Scene/Flow真实能力 |
| P1 | **S2-配置与命令** | 先打通一个典型设备/算法Content的Schema组合配置全过程；再扩至多实例、资源/变量和专业Descriptor，强制真实事务/校验/保存重开 |
| P1 | **S3-A/B编辑闭环** | 把独立资源Tab的九Shape/精细画布接入现有完整Editor，不再长期保留“矩形演示栏→完整版”双界面；随后统一MOL流程、Widget、状态与事件 |
| P2 | **S4-C与跨场景交付** | MOC/MRK/MDS真实观察及调试定位、Vision/CT3D专业Viewer、A+B+C一体化，完成性能和安全门禁 |

## 7. 当前执行事实与待更新

- 最新核查基线为aba211cf1640e18b27ccbc421239c972471728f7；e06c2f26的Studio 16/16 STA及Engineering CI曾通过；之后新增P0-2 Dirty单测的007283b0版本Studio工作流[39551185708](https://github.com/oudream/cx-visual-x3/actions/runs/39551185708)实测失败，失败断言为“Undo产生的新Revision未更新引用影响与Dirty提示”，相同SHA的[Engineering Organization](https://github.com/oudream/cx-visual-x3/actions/runs/39551185744)成功。因此**先修P0，不能把历史的16 PASS冒充最新17 PASS**。
- **本轮P0修复已通过（源码SHA a4e94349）**：在Studio层为已提交MTC操作的TargetId保留稳定资源根身份（切换工程清空、最多65536条，超出后保守标记），用于Undo删除当前已不存在图元时定位直接修改Tab及引用受影响Tab；额外补充Redo不污染无关资源的反证。未修改MEM/MTC事务、历史或Revision语义。 [Studio S1 Windows #39553816491](https://github.com/oudream/cx-visual-x3/actions/runs/39553816491) **success，17/17 WPF STA通过**；[工程组织Windows #39553816541](https://github.com/oudream/cx-visual-x3/actions/runs/39553816541) **success**，同一源码SHA完成编译与回归。文档变更的后继提交不改变上述源码验收SHA。
- V1.2原始全文及用户提及的11张图与仓库内00～09的数量差异仍应后续对照。其缺失不影响沿已确认界面决议修复S1现实回归，但禁止擅自重新设计菜单或主布局。
