# CxVisual Studio S1～S4 完整阶段任务与新聊天接力 V1.0

> 文档类型：Studio四阶段的完整实施/验收/接力清单。**此前聊天确立了S1～S4四阶段，但仓库旧S1记录没有完整定义后续S2～S4**，本文补全。S阶段不是工程呈现化的EP01～EP15需求、G1～G6阶段门、R01～R48开发轮次的替代。
> 核实快照：2026-10-21，主仓库 https://github.com/oudream/cx-visual-x3 ，当时master 007283b06c3a465d40229ea132b2746b001284c3。新聊天必须查询**最新**HEAD。
> 编号说明：表中的S1.01等为本次新增的阶段明细编号；和StudioShellSelfTests中的S1-01等测试名称不是一一等价，不能用数量对应推定完成状态。

## 0. 开发基础与不能突破的原则

先读 AGENTS.md、.agents/skills/cxvisual-engineering/SKILL.md、DOCS/00_CxVisual_协同开发入口与七仓复用基线.md、DOCS/01～04四大统一架构；随后读Studio V1.2批准规范、DOCS/Architecture/有关呈现化/工程组织及运行基线、DOCS/Implementation/和DOCS/Testing/的实际最新记录，检查真正的源码及同SHA CI。

- **架构**：Foundation/Mechanism/Professional；原28项目优先，不另建项目、工程树、流程模型、Undo仓库或运行仓库。MEM提供唯一工程对象与稳定身份，MTC负责事务与Undo，MPM提供Schema，MHS提供配置生效语义，MOL流程定义，MOC编译/SourceMap，MRK执行调试，MDS运行数据。Studio只是协调和交互。
- **技术/协作**：C#、.NET Framework 4.8、WPF；AvalonDock 5.0.1；直接master开发、中文有意义Commit、一轮尽量一提交；每轮实做、实编译、真Windows WPF/CI，不用静态代码冒充验收。
- **界面**：固定9个一级菜单“文件、编辑、视图、工程、调试、运行、发布、工具、帮助”；动态专业菜单**紧随同一行右边**，下方**一条**当前Ribbon；中央文档Tab在Ribbon以下，左树到Project/Page/Content/Symbol，不把Group/Element混入；HMI独立图形结构树Layer/Group/Element；白色TestCenter浅主题、Visual Studio中性深灰色。
- **设计图**：DOCS/界面设计/中仓库实际是10个PNG：00文件页、01主页、02 HMI/Symbol、03主流程、04专业子流程、05运行监控调试、06 Vision、07 CT3D、08独立TestCenter、09构建发布。另有先前讨论的工业相机配置编辑场景；不得凭记忆编造第11张仓库图片。
- **总目标**：开发者公共API简单自然，工程用户Studio能够完成设计→编辑→编排→构建→运行→观察→调试→故障定位→修改重建，A/B/C场景分别及联合真实通过。布局、选中与运行数据不能修改工程Revision；现场动作要求权限/联锁；TestCenter是独立测试应用，不是专业Workbench。

## 1. S1：统一Shell、工作区与多编辑器基础

**完成目标**：Studio整体界面可用，原HMI和MOL编排不回退；导航、编辑器、窗口生命周期与同一个MEM工程保持一致。

| 编号 | 必须开发/完善的完整功能 | 真实验收 |
|---|---|---|
| S1.01 | 原MainWindow增量改造统一Shell：标题/状态栏、资源、文档、属性、输出 | Windows运行启动；原ViewModel/Editor继续服务 |
| S1.02 | 9固定一级菜单+随资源变化的动态上下文菜单**同一行**，下面仅当前Ribbon按钮带 | HMI/Flow/配置切换时真实按钮可用，不多加一层菜单 |
| S1.03 | AvalonDock停靠、浮动、隐藏、自动隐藏、工具分页、分屏、布局保存/恢复/重置 | 同一ContentId唯一；关闭后重开，安全XML/未知ID拒绝 |
| S1.04 | 浅色TestCenter白、深色VS灰主题及浮动窗一致视觉 | 双主题切换不改工程Revision；对比度与高DPI人工复核 |
| S1.05 | 左工程资源树Project/Page/Content/Symbol/资源搜索、选择、双击、更新 | 使用MEM投影；不包含Group/Element，不引入第二树 |
| S1.06 | 独立Layer/Group/Element图形结构树与画布、属性的选择同步及展开保持 | 同一对象ID；选中不修改MEM Revision |
| S1.07 | 主页、配置、HMI、MOL文档宿主；左工具箱、右属性/绑定、底输出 | 旧HMI图元/流程真实操作保留 |
| S1.08 | Page/Content/Symbol和Definition/Instance往返；失效引用拒绝 | R05实例→定义→原页面，原选择可恢复 |
| S1.09 | Page/Content/Symbol/Flow每资源独立真实Tab，重复打开复用、独立Scene/视口/选中 | R06两页面互不串改但共用一个MEM Undo/保存 |
| S1.10 | 关闭、工程切换、布局恢复与跨工程资源身份校验，订阅回收与正确文档焦点 | 不泄漏、不打开旧工程Tab、不抢原MOL主画布焦点 |
| S1.11 | 多Tab Dirty与引用影响范围同步；共享定义更改、Undo/Redo、保存重开 | 撤销后新Revision的影响与Dirty准确刷新，不能静态标记 |
| S1.12 | 文件页Backstage基本闭环：新建、打开、保存、另存、最近工程、模板入口、Dirty退出提示 | 未保存取消退出不丢数据，非法工程拒绝，未实现项禁用 |
| S1.13 | 原HMI/MOL/MOC真实命令兼容，不让新页面轻量Editor遮挡原高级编辑器 | 整套工程组织Windows WPF旧回归 |
| S1.14 | Windows x64 Release完整编译、STA、自测、工程组织CI、Dock和100/150/200%DPI/双屏人工验收 | 同SHA成功并有证据才能宣告S1完成 |

**退出条件**：基本界面和资源工作区完整可运行，多页/多类型文档、工程切换、布局、导航、共享Undo、Dirty正确，旧能力无退化；同SHA S1 + 工程组织回归全部成功；浮动/多屏/DPI真实桌面验证另行记录。

## 2. S2：统一资源编辑、属性配置、统一操作命令

**完成目标**：专业开发者只描述资源/Schema/命令，工程用户自然获得一致的中央配置页面、右属性与操作入口。

| 编号 | 必须开发/完善的完整功能 | 验收 |
|---|---|---|
| S2.01 | 资源种类→Editor注册匹配（配置/图元/流程/专业Viewer），默认和候选Editor | 相机、PLC、配方、Symbol自动打开正确Editor |
| S2.02 | 稳定DocumentKey包含工程、资源、编辑器、必要实例路径 | 同资源多Editor不串状态；多资源Tab协调 |
| S2.03 | 复用MPM类型Schema生成Boolean/Number/String/Enum/Color/Brush/List/Map/资源引用/专业字段 | 范围、必填、只读、默认、单位、错误提示 |
| S2.04 | 中央Schema组合面板：自适应1×N、默认2×N、可选3×N，折叠/查询/验证 | 真Schema驱动，不能止于S1四张固定卡片 |
| S2.05 | 多选、Mixed、批量原子应用、定义/实例覆盖、恢复默认 | 非法批量零部分提交、Undo一次撤销 |
| S2.06 | 工程属性和设备/运行配置体验一致但Owner语义分离：MEM/MPM/MTC vs MHS/Professional | 草稿、校验、提交、生效、版本、回滚、授权 |
| S2.07 | 统一CommandId/CommandDescriptor/命令状态与Owner路由，不复制业务命令 | 菜单/Ribbon/快捷键/右键共用真实ICommand |
| S2.08 | 9一级菜单的二级命令和专业动态Ribbon按V1.2完整审查；命令禁用/权限/模式 | 不使用按钮显示文字硬编码路由作为最终方案 |
| S2.09 | 工程Dirty、Undo/Redo、对象修改影响在各Editor传播 | 统一MTC历史、跨Tab状态一致 |
| S2.10 | Editor资源生命周期、关闭确认、验证冲突、订阅回收、布局/工程恢复 | 正反操作、失败恢复、内存泄漏反证 |
| S2.11 | 四种真实案例：工业相机、PLC、检测配方、电机Symbol实例（可扩AI/CT） | 开发者API + 工程用户Studio全操作 |
| S2.12 | Windows WPF/TestCenter/手工操作与性能安全验收 | 不伪造设备配置生效和权限结果 |

**退出条件**：四种资源均能通过已有Owner编辑、验证、保存重开，Schema/多选/权限/事务正确；专业无需为常规表单和命令重复写Shell。

## 3. S3：HMI图元、Widget、MOL流程与MOC构建

| 编号 | 必须开发/完善的完整功能 | 验收 |
|---|---|---|
| S3.01 | 九Shape（Line/Rect/Circle/Ellipse/Polyline/Polygon/Arc/Bezier/Path），Text/Image/安全SVG | 真几何、真实WPF渲染、安全输入 |
| S3.02 | 图元单/多选、框选、移动/缩放/旋转、手柄、参考线吸附和视口 | 鼠键真实交互；MEM原子事务/Undo |
| S3.03 | Arc/Bezier/Path节点/切线/段转换/多轮廓精确编辑 | Geometry不变量、异常退化反证 |
| S3.04 | Layer、Group、锁显、顺序、嵌套变换、剪贴板 | 图形结构一致及保存重开 |
| S3.05 | Symbol定义/实例、不同实例覆盖、来源跳转 | 双实例独立与共享传播 |
| S3.06 | 样式/主题/渐变/字体/图片/受限SVG素材、资产引用和丢失提示 | 样式优先级、恶意输入拒绝 |
| S3.07 | 六基础Widget、扩展Widget、焦点/Tab、绑定、运行/预览状态 | 不混淆用户事件与程序刷新 |
| S3.08 | MOL主/子/子子及跨专业Content，节点/能力/端口自由连线/参数映射 | 单一逻辑真源，不重建Graph |
| S3.09 | 分支/循环/并行Join/等待/重试/异常可视编排 | 控制与数据端口语义正确 |
| S3.10 | HMI图元事件/状态与流程节点双向关联 | 实际MEM身份与保存重开 |
| S3.11 | MOC构建、结构类型校验、进度诊断、SourceMap与过期版本提示 | 真错误可回到源流程 |
| S3.12 | X-Ray主流程+AI/CV双专业子流程真实可视化与构建 | 控制与数据分流、合流和错误反证 |
| S3.13 | A图元案例完整、B电机Symbol设计部分、C编排构建部分 | 用户实际操作/开发者API/保存重开 |
| S3.14 | 画布性能、工程历史、Windows回归 | 真实WPF/CI数据，旧功能无损 |

**退出条件**：A完整通过，B的设计与安全边界、C的双子流程编排构建部分通过，真实编辑不止于绘制效果图。

## 4. S4：运行调试、专业Viewer及完整联合验收

| 编号 | 必须开发/完善的完整功能 | 验收 |
|---|---|---|
| S4.01 | Design/Preview/Run/Debug界面行为与最终批准模式命名对齐；Build/Publish分离 | 设计不触发设备动作、运行安全 |
| S4.02 | MEM Revision、MOC计划、SourceMap与MRK运行实例版本关联 | 旧Trace不能定位新版工程 |
| S4.03 | MRK启动/停止/暂停/继续/取消/并发实例 | 真实控制与调度状态 |
| S4.04 | MDS Heap/Stack/Frame/端口/输入输出/Watch只读观察 | 质量、时间戳、失联、无Revision污染 |
| S4.05 | HMI/Widget运行状态动画、订阅、限频、实例隔离及关闭释放 | 运行数据与工程定义隔离 |
| S4.06 | 断点、条件命中、真实单步、栈、异常处理与安全保护 | 真实MRK调试而非模拟UI |
| S4.07 | Trace/Timeline/SourceMap子→父→HMI/专业资源双向定位 | 报错→修改→构建→重新运行 |
| S4.08 | Vision算法/ROI/AI/CV专业编辑诊断工作区 | 专业Provider真实能力及缺失提示 |
| S4.09 | CT3D专业Viewer：MPR/切片/体绘/窗宽窗位，Volume句柄缓存 | 避免图像大数据反复复制，释放资源 |
| S4.10 | Automation/Inspection/Simulation等专业Editor/Viewer注册SDK | 不重复公共Shell/工程/运行 |
| S4.11 | 构建、版本冻结、Release、部署、回滚和权限分离 | 构建成功不等于现场获授权运行 |
| S4.12 | 独立TestCenter兼容视觉规范、专业与公共回归 | 不把TestCenter算第七专业Workbench |
| S4.13 | 大场景、长时运行、线程/内存/句柄/DPI/双屏/安全输入 | 真实测量与故障反证 |
| S4.14 | A、B、C独立及跨场景联合完整验收 | 同SHA Windows、WPF、TestCenter、必要现场证据 |

**退出条件**：从设计到运行、诊断定位、修改重构建完整闭环；A+B+C分别和联合通过，仿真与现场设备联锁严格区分。

## 5. 与工程呈现化EP/G/R的对应（交集不是替代）

| Studio阶段 | 对应EP核心 | 对应阶段门/轮次交集 |
|---|---|---|
| S1 | EP01、EP15 | G1，R05～R06 |
| S2 | EP01/04/05部分/08命令/14 | G1/G3/G6，R01～R04及后续配置切片 |
| S3 | EP02/03/05/06/07/08/09/10 | G2/G3/G4，R07～R22、R29～R36 |
| S4 | EP07/08/10/11/12/13/14/15 | G3/G5/G6，R23～R28、R37～R48 |

实际R01～R48的定义、实施状态及G1～G6阶段门唯一参考：DOCS/Implementation/CxVisual_工程呈现化体系_30分钟分轮实施与验收_V1.0.md。上述S阶段是Studio项目管理分层，不能声称仓库原始文档已经如此划分。

## 6. 编写时真实进度和首个必须修复的问题

- 在编写时master=007283b06c3a465d40229ea132b2746b001284c3。**并行开发会推动master，新聊天先更新状态**。
- S1目前已实际实现大量Shell、AvalonDock、9菜单、主题、ResourceTree、GraphicsTree、Page/Content/Symbol/Flow独立Tab、R05/R06部分闭环。最近还有P0-2多Tab引用影响和Dirty工作；**S1不能认定已完整签收**。
- **最新同SHA Studio S1 Windows CI 39551185708失败**：16项WPF STA PASS，第17项FAIL“Undo产生的新Revision未更新引用影响与Dirty提示”。链接：https://github.com/oudream/cx-visual-x3/actions/runs/39551185708
- 同SHA工程组织 Windows CI 39551185744成功：https://github.com/oudream/cx-visual-x3/actions/runs/39551185744 ，两项配置Windows也成功；**不覆盖Studio的失败**。
- 历史Studio R06 16 PASS：https://github.com/oudream/cx-visual-x3/actions/runs/39549457531 仅证明旧SHA，当期成功不等于最新SHA成功。
- **第一开发任务**：查MEM ChangeSet/变更影响闭包和Undo/Redo事件、Studio ResourceDocuments与Tab Dirty投影。先核实其他并行提交是否修复；未修则修“Undo新Revision后影响/Dirty未刷新”，保留原断言、补反例、同SHA重新跑Studio及工程组织CI。不得改期望值或写假Revision。
- S2有MPM/MHS/R01～R04等历史代码，但中央完整Schema 1/2/3列组合面板与通用CommandId未完成；S3图元/MOL/MOC有真实基础但A/B/C未全部签收；S4 MRK/MDS/专业Viewer与A+B+C联合未全部签收。

## 7. 给新聊天直接复制的开发指令

> 继续开发CxVisual Studio。主仓库 https://github.com/oudream/cx-visual-x3 ，直接master开发，中文Commit，保持.NET Framework 4.8/WPF和现有28项目。**不要从零设计，不要只给计划。以最新源码/权威文档/真实Windows CI为准。**
>
> 首先读AGENTS.md、.agents/skills/cxvisual-engineering/SKILL.md、DOCS/00_CxVisual_协同开发入口与七仓复用基线.md及DOCS/01～04四大统一架构；再读本文件DOCS/Implementation/CxVisual_Studio_S1至S4_完整阶段任务与新聊天接力_V1.0.md、DOCS/Implementation/CxVisual_Studio_S1_统一Shell与AvalonDock_实施验收.md、DOCS/Implementation/CxVisual_工程呈现化体系_30分钟分轮实施与验收_V1.0.md、当前相关Architecture/Testing及DOCS/界面设计原图，并核对已批准Studio V1.2规范是否能取得。
>
> Studio四阶段及全部细项按本文件§1～4：S1 Shell/Dock/多资源工作区；S2资源/Schema配置/统一命令；S3图元/Widget/MOL/MOC；S4 MRK/MDS调试与Vision/CT等专业工作区、A+B+C联合验收。S阶段与EP01～EP15、G1～G6、R01～R48不同层级，不能互相替代；不新建第二套MEM/MOL/MTC/MRK仓库。
>
> **第一件事查最新master及同SHA CI**：此文编写时SHA 007283b06c3a 的Studio CI 39551185708失败，第17项“Undo产生的新Revision未更新引用影响与Dirty提示”。工程组织CI 39551185744成功。先看是否已被并行修复；若未修则真正修代码并做正反WPF测试，提交并重跑Windows同SHA。严禁仅改断言或把旧SHA测试通过当最新通过。
>
> 修复后持续完成S1剩余的文件Backstage、多Tab Dirty、资源编辑器生命周期、命令焦点、Dock浮动/自动隐藏、多DPI/多屏等真实功能；达到S1退出门槛后按S2→S3→S4推进。每轮交付代码修改、中文Commit SHA、CI链接、实际通过/失败、仍待人工和下一轮计划。最终验收开发者API和工程用户Studio两视角，工程保存重开、非法反证、Windows CI/TestCenter及A+B+C真实闭环。

## 8. 文档维护

本文只负责**S1～S4的完整阶段定义**，不是新增第二实施台账；S1已实测证据继续由S1专项记录，R01～R48由工程呈现化总台账维护。任何完成断言须绑定同一SHA，必须记录失败与人工未验项，避免状态污染。
