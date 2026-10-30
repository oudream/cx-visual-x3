# CxVisual 通用二维视觉工作台 V2.0——架构整改与完整实施验收 V1.0

> **文件性质**：正式实施统筹/任务执行与验收总控；不替代既有00～08架构规格、01的260项功能契约或测试矩阵。  
> **首次建立**：2026-10-30；**审查基线**：`cx-visual-x3/master`，读取时HEAD `3cdb62992c98ceb648626dc9c93d797a8931657d`。此SHA只代表本文件编写前审查时点，不是本方案的验收SHA。  
> **项目现实**：26工程（F8/M7/P6/应用2/智能1/测试2）；Mechanism.Logic/Orchestration现已合并为Mechanism.Decision，Lifecycle不是独立工程。历史28工程描述仅作追溯。  
> **用户裁决**：保留已经确立的F/M/P架构，按“OOP对象职责清楚、GoF变化扩展受控、AOP跨专业共用治理”整改；以真实功能和验收证据为导向，不推翻合理现有设计。  
> **核心原则**：各方面都尽能力展现，便捷友好尽支撑各专业达到目标。  
> **状态声明**：这是指导后续研发的V1.0统筹，新增任务ID、拟拆分对象及通过标准为实施要求，不代表本次已经改动业务代码、执行CI或完成工业验收。

## 0. 如何使用本文件（每轮开发必须遵循）

1. 首先读取仓库`AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md`；核对最新master HEAD、相关工程.csproj、真实代码与GitHub Actions。不得以本文件旧SHA推断新HEAD状态。
2. 按当前任务ID打开本文件第3节“问题—责任—完成定义”、第4节“技术方案”、第5节“执行批次”，然后读取第1节列出的对应原始权威文档；再查看`ReferTo/`、已有单元/集成/STA测试。先复用再增加类型。
3. 区分五种内容：**已定架构**、**当前已有源码**、**历史固定SHA成功证据**、**尚未实施的目标**、**尚未验证的工业能力**。不得将本统筹中“拟实现”写成现有API；不得把历史CI迁移到最新SHA。
4. 每项任务按“取证 → 明确Owner与变化点 → 最小完整实现 → 正反例 → Windows/CI → UI/专业验收 → 登记证据”的方式闭环。若发现架构冲突，提交差异分析/ADR并提请Owner裁决，不在单专业内私自改平台宪法。
5. 本文件只维护实施总控、依赖、验收状态和跨任务决策；具体算法合同仍在01/03～08，UML在02，源码事实在原Implementation台账，测试证据在Testing；避免复制几份会漂移的独立“真源”。
6. 未经证据证明的问题标为“设计缺口/扩展风险/待验证”，不得误称线上Bug；问题关闭必须说明原有公共API兼容、项目依赖、性能和回归结果。

## 1. 依据文档、代码与事实范围

### 1.1 权威依据及优先次序

| 层次 | 依据 | 用途与注意 |
|---|---|---|
| 协同、工程规范 | 根目录`AGENTS.md`、工程Skill、`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md` | 提交/依赖/当前26工程/真实验收纪律 |
| 平台宪法与Owner | `DOCS/01～04`、最新Architecture/Owner决议及真实.csproj DAG | 解决F/M/P唯一职责和跨工程依赖；Sheet级内容每次按需重新核对，不以本文件替代 |
| Imaging功能与设计 | `DOCS/Architecture/CxVisual_专业视觉体系#通用二维视觉工作台——00总体架构与设计总纲_V2.0.md`、同前缀01～08 | 6群26类260二级能力、UML、数据/交互/算法/结果/运行/工作台契约 |
| 源码实施事实 | `DOCS/Implementation/CxVisual_专业视觉体系#通用二维视觉工作台——V2.0实施源码与迁移台账.md` | 已实现文件、历史提交、遗留事项；以真实master修正 |
| 严格验收 | `DOCS/Testing/CxVisual_专业视觉体系#通用二维视觉工作台——V2.0全类验收与测试矩阵.md` | V2D编号、G0～G6、CASE-01～08、正反例与实测日志 |
| 复用来源 | `ReferTo/`及七仓参考源码，现有Tests/和CI | 仅作复用候选，未经适配验收不宣称已经集成 |

**冲突处理**：最新用户明确裁决和有效平台Owner/工程事实优先；Imaging专题不得复写平台所有权。旧文档中的28工程、候选类签名、“设计已通过”等字句不得提升为2026-10-30之后的当前实装状态。升级正式公开API须检查DOCS/02～04相关Sheet与版本，并同步相关UML/ADR。

### 1.2 已确认的真实代码锚点（非完整实现清单）

| Owner | 现有源码及职责 |
|---|---|
| FIM | `Foundation/Imaging/`：`FIMImageBuffer`、`FIMImageOperations`、`FIMImageAnalyzer`、`FIMRawImageWriter`等；原像素、通用计算、数据导出 |
| MVI | `Mechanism/Visual/Imaging/`：`MVIImageSession`、`MVIImageViewport`、`MVIImageLinePairGesture`、`Overlays/`；无WPF会话、坐标与临时叠加 |
| MPW | `Mechanism/Presentation.Wpf/Imaging/MPWImageViewerControl.xaml.cs`；WPF输入、显示、投影和视觉反馈 |
| PVC | `Professional/Vision/`：`CxVisionDataFlow`、`CxVisionRawCpuRunner`、`CxVisionRoiEngineering`、`CxVisionRunSummary`、`CxVisionRunCsvExporter`、`CxVisionImageInterop`及Provider |
| 公共机制 | 现有MEM/MRC/MTC、MPM/MPC/MOL、MOC、MRK/MDS/F19、FLD与权限/配置机制；不得在Vision复制 |
| 实际宿主/测试 | `Applications/Studio/`、`Tests/TestCenter/Manual/Imaging/`、`Tests/ArchitectureSmoke/Integration/Vision/` |
| CI | `.github/workflows/imaging-foundation-windows.yml`、`module-center-cpu-windows.yml`、`studio-shell-windows.yml`、`unified-runtime-windows.yml`、`testcenter-windows.yml`等；每项必须记录具体run/SHA/Job |

当前已存在真实ROI→FIM/MRK预览、MEM Data/Visual单事务保存重开和版本/Hash拒绝、若干Run结果与CSV、WPF入口及自动测试源码。**但R1同一Studio完整用户路径尚需以固定SHA STA/UIA和实际结果复验，整体260项、设备HIL和工业长稳均不能宣称完成。**

## 2. 架构不变量：OOP、GoF、AOP是工程硬约束

### 2.1 四逻辑平面与单一Owner

| 平面 | 归属 | 写入权/边界 |
|---|---|---|
| Definition | MEM/MRC/MTC、MPM/MOL | 工程Page/Content/Group/资源、ROI/流程定义、Revision、Undo/Redo、Save/Open；编辑必须显式事务 |
| Execution | MOC/MRK/MDS/F19与PVC Provider | 类型检查/解析/编译、发布策略、任务执行、Pending/Commit/Abort与取消；运行中固定版本 |
| Evidence | PVC结果语义+现有数据/Artifact机制 | 一次Run独立身份、多ResultItem、来源/版本/状态、Trace；成功后只读，旧Run不可覆写 |
| Presentation | MVI/MPW + Studio/TestCenter | 图像会话、视口、手势、临时Overlay、结果投影；不把Viewer当结果仓或正式工程数据库 |

贯通链：`ResourceRef/DefinitionRevision → SourceRef/FrameId → Invocation/RunId → ResultItemId/ArtifactRef → Review/ReportRef`，并以CorrelationId关联诊断。每一层只拥有本层状态，可通过明确引用和命令协作，禁止靠可变共享对象反向修改其他层。

### 2.2 OOP验收

- 每个新增/修改类必须记录：**责任、唯一Owner、身份、可变性、线程/生命周期、输入/输出、错误语义、测试消费方**；保证单一职责、封装不泄漏可变数组、组合优先。
- 基础纯数学/无状态图像运算保留静态函数；无需为了“面向对象”对每个FIM函数创建接口或Manager。
- 拆分MPW时按真实行为边界迁移，不按文件行数机械分拆；旧公共API、AutomationId、WPF绑定、已有测试必须兼容。
- 设计候选类/接口只有经两处真实调用变化点、Owner、反例和依赖DAG审查后才能批准；新增跨Owner对象须有ADR。

### 2.3 GoF使用裁决

| 模式 | 推荐落点 | 禁止 |
|---|---|---|
| State | 复杂ROI/测量/编辑手势的Begin/Update/Commit/Cancel、失捕处理 | 为每个简单按钮生成空State类 |
| Observer | MVI会话/Overlay变化、结果选择后投影通知 | 后台线程直接修改WPF或监听器异常导致假回滚 |
| Strategy | 两个及以上真实可替换CPU/SDK/算法/显示策略 | 单一算法先建空策略继承层 |
| Factory/Registry | 已有Capability/Provider/MPM/MOC目录 | Vision另造第二插件注册中心 |
| Adapter | FIM↔专业输入/第三方SDK/跨Workbench数据协议 | 基础像素改成SDK句柄或层层空包装 |
| Command | MEM/MTC正式编辑命令、Undo/Redo | MPW/MVI建立第二持久Undo堆栈 |
| Composite | MEM Content/Group及MRC视觉对象、多ROI组合 | 在PVC再造持久对象树/私有ROI数据库 |
| Template Method（思想） | 统一调用校验/执行/结果装配的步骤顺序；优先组合已有服务 | 强迫无关算法继承巨型基类/替换MRK执行器 |

### 2.4 AOP不是另建AOP框架

横切规则统一挂在已有执行、工程事务、发布和数据租约边界：日志FLD、Trace/CorrelationId、权限与发布授权、配置/模型/参数版本、取消、超时、异常码、预算、内存/显存/SDK占用、用户审计。专业只提供业务字段；不能用UI按钮隐藏代替Runtime授权；热像素循环不得反射织入或逐像素记日志。若某项公共能力不存在，先向公共Owner提交最小增量，不在PVC做平行实现。

### 2.5 明确禁止

- 不增第27个项目、不新建Vision专属工程树/GraphExecutor/DataStack/Heap/CommandCenter/UndoStack/通用JobScheduler。
- 不将Vision候选缺陷直接当Inspection最终合格判定，不把软件Plan发布当设备安全互锁或硬实时。
- 不把`MDSBoundedByteStream`当完整工业相机流；现有MDS预算不是CPU+GPU+SDK全部峰值的证据。
- 不因测试小图、已写UML、绿色历史CI而宣称大图性能、全类能力或设备FieldPassed。
- 不将正式ROI、结果Overlay、屏幕DIP坐标与原图像素/物理坐标混为同一身份。

## 3. 全量问题与任务台账（18项，唯一任务号）

**状态字典**：`Open`需补实装或证据；`InProgress`实施中；`Blocked`有明确依赖；`Accepted-Gx`按第6节达到固定证据级别；`Deferred`经Owner裁决延期。下表初始均为`Open`，不抹掉已有局部实现/历史测试。每一任务完成时在本表或其第7节追踪记录中补具体链接和SHA。

| ID | 优先级 | 问题/当前观察 | 唯一主Owner / 实际触点 | 交付物与“完成定义”（DoD） |
|---|---|---|---|---|
| IMG-01 | P0 | MPW显示、输入、控制柄、坐标和状态耦合；2300+行 | MPW；`MPWImageViewerControl.xaml.cs` | WPF事件仅负责输入/Presenter；可迁移的无WPF规则移MVI；现有API/自动化ID/双Pane无回归 |
| IMG-02 | P0 | 多布尔交互状态可交错，失捕/取消复杂 | MVI交互Owner；`MVIImageLinePairGesture`、MPW事件 | 统一合法状态迁移与清理，Begin/Update/Commit/Cancel；Esc、失捕、切图、非法点零脏提交 |
| IMG-03 | P0 | `SourceVersion`还混含会话Result修改，不能独自代表原图Revision | MVI会话+宿主请求边界 | 区分原图身份/源代次、ROI编辑代次、参数/发布版本、请求Epoch；晚到结果禁止更新当前视图 |
| IMG-04 | P0 | Run证据已有局部Item但缺完整状态/来源/生产者/Artifact合同 | PVC结果语义，MDS/FDT数据Owner | 同Run多类型可追溯、Status/Validity分离，已完成不可原地覆写；错源/缺Artifact/取消/失败区别处理 |
| IMG-05 | P0 | R1 CASE-07真实单Studio闭环未最终验收 | Studio宿主+PVC/MEM/MTC | 打开源图→选ROI→正式Commit→Undo/Redo→Save/Open→重绑同源→运行→列表/叠加→CSV/RAW导出；正反例及STA证据 |
| IMG-06 | P1 | 运行后短期结果与长期Artifact/复核引用未定型 | 平台MDS/FDT与PVC Evidence协作 | 明确ResultItem引用/保留/过期/清理/重读/审计合同；不新建堆/仓，审计复核不改变原始Run |
| IMG-07 | P1 | 直接MRK预览、MEM/MOC正式工程、多Provider可能各自演化 | PVC Provider；MOC/MRK底座 | 快速预览与正式编排同一算法与类型/参数/数值语义，正式工程依MOC编译、MRK执行、MDS提交 |
| IMG-08 | P1 | 专业参数、端口、能力发现和Provider版本未全类一致 | MPM/MPC/MOC公共Owner+PVC注册 | 可发现/可验证/可组合；非法TypeId/格式/单位/端口拒绝且有定位错误，绝不建Vision注册中心 |
| IMG-09 | P1 | `CxVisionGrayFrame`、RAW Provider等多次安全复制及临时数组峰值 | FIM/FDT/MDS资源Owner，PVC调用方 | 防御复制默认不退化；有实测副本/峰值表，验证后引入显式只读Owner/Lease、容量及释放 |
| IMG-10 | P1 | FrameId/采集时间/有界背压/掉帧/回收不足以表示真实在线流 | Automation采集与MDS/Transport，PVC帧合同 | 字节+帧数预算、Backpressure/Drop/NoDrop、乱序/取消/失联有确定语义和计数；真实设备另验 |
| IMG-11 | P1 | CorrelationId、FLD、权限、版本、资源统计跨宿主不完整 | FLD/权限/配置与MOC/MRK公共Owner | Studio/无头/Provider同一Invocation诊断链；失败、取消、拒绝、超预算有Trace+错误码+来源 |
| IMG-12 | P1 | 多图/Mask/ROI/结果在Source、坐标系和Revision错配时有误投影风险 | MVI+PVC SourceRef与坐标合同 | 图A Mask不得叠到图B；Zoom/Flip/Rotate/Spacing/DPI可逆；未标定不输出mm/mm² |
| IMG-13 | P1 | Studio正式工作台选择、参数、结果面板和命令未全部通用化 | Studio/MPW与公共命令机制 | 工程/图像/ROI/算法/Run选择上下文一致；CanExecute/取消/失败反馈正确；TCT不是唯一入口 |
| IMG-14 | P2 | 已有单ROI配方，复杂组合和复用不足 | MEM/MRC/MTC + PVC规则 | 多ROI/Group稳定ID、形状/孔洞/规则可组合，正式事务/Undo/Save/Open，不建私有列表真源 |
| IMG-15 | P2 | 匹配配准、拟合、专业增强、AI等260项仍有功能覆盖缺口 | FIM基础、PVC专业及相应Provider | 逐V2D ID补真实算法/API/输入输出和正反例；AI无模型/许可时明确Unavailable，不伪装已实现 |
| IMG-16 | P2 | 大量Overlay、数千顶点、多Pane和DPI性能/体验待真实验证 | MPW Presenter + MVI投影 | 渲染/拾取性能基线，必要时缓存/批绘/局部重绘；125/150% DPI和大图实测无坐标退化 |
| IMG-17 | P2 | Vision↔Inspection/CT3D/Automation/HMI跨专业合同仍需扩展 | 各专业唯一Owner+平台Invocation | 统一TypedRef/来源/版本/坐标/权限；Vision输出候选事实，Inspection持最终产品判定 |
| IMG-18 | P0（持续门禁） | 26类260项尚无逐项G0～G6闭合证据 | Tests与每项实际Owner | 260个V2D逐项有实现位置、正反例、宿主/API、SHA/CI、工业适用性；未通过项不勾选 |

**优先级解释**：P0表示先收敛的结构风险/交付门禁，不等于现有程序必然发生故障；IMG-18贯穿所有批次，不是最后一次“补测试”。主Owner为实施定位，不能替代公共Owner对跨项目API的正式裁决。

## 4. 六条整改主线——对象设计与实施步骤

### A. 交互与显示：IMG-01/02/12/16

**设计目标**：MPW作为薄WPF输入/Presenter；MVI负责视口、图元会话和可测试的无WPF手势；FIM/FGE负责纯图像/几何运算。保留已存在的`MVIImageLinePairGesture`，不引入第二Viewer。

**实施步骤**
1. 从`MPWImageViewerControl.xaml.cs`逐功能列出字段/事件/操作/写入Store与UI动作，按显示/变换/Overlay渲染/命中/手势/选中分组；建立现有事件行为的回归基线。
2. 对连续多阶段且确有冲突风险的交互先建立无WPF状态合同：Idle、Active、Committed、Cancelled等**语义**，不是预先批准所有CLR状态类。把Begin、Update、Commit、Cancel及换图/失捕转移放到MVI可测试逻辑；事件路由保留在MPW。
3. Observer变更通知只在状态正确提交后触发。选中/拖动/预览不可误产生正式MEM事务；已有控制柄、图层、锁定、双Pane复用现有Store。
4. SourceRef匹配和像素中心/边界/视口/物理坐标明确。错误身份不画Mask，不因UI坐标缩放改变原始ROI。Overlay渲染与工具状态判定逐步分离。
5. 最后依据实测决定缓存和分批绘制，不以“减少文件行数”本身为验收目标。

**必须反证**：Esc、失鼠标捕获、释放在图外、锁定后继续拖、切源、两个Pane、双屏DPI、无效多边形、极大顶点集、结果切换。旧公开API/AutomationId不得破坏。

### B. 结果与证据：IMG-03/04/06

**设计目标**：在现有`CxVisionRunSummary`/`CxVisionResultItem`上扩充**必要**的来源与证据语义，不建立第二ResultStore。原始Run只读，复核结果为派生记录。

**实施步骤**
1. 审查现有Run/Item字段、MDS CommittedOutputs、MRK FlowId、Studio/TCT消费方，输出实际字段缺口表。梳理RunId与ItemId如何保持稳定、Image/Mask内存快照怎么封装。
2. 固定最小合同：RunId、Status、SourceRef、Definition/Config/Provider版本、CompletedUtc、ResultItemId、Kind、ProducerNode/OutputPort、CoordinateSpace（适用时）、Unit/CalibrationRef（适用时）、Trace/Error（适用时）。缺失字段明确Unknown或NotApplicable，禁止填伪值。
3. Status与Item Validity分离：`Completed+Empty`不等于`Failed`，`Cancelled/Rejected/Stale/MissingArtifact`不能误画成有效数据。对直接预览与编排执行的身份映射用Adapter/组合扩展。
4. Run提交后不得由UI修改原始数值、坐标和证据。人工审核、二次判定/报告复用既有专业/数据Owner，以引用记录原Run，不在Vision做Inspection最终判定。
5. 优先复用MDS/FDT预算及资产机制，明确长期Artifact归属、只读访问和失效策略；首次验收只要求当前真实产物闭环，不伪造持久文件库。

**必须反证**：空结果/失败/取消/中途改参数/换源/丢失Artifact/反复导出/历史Run重读，所有结果类型和版本不串线。

### C. 专业算法、编排与API：IMG-07/08/13/15

**设计目标**：算法本身只有一个专业实现真源；预览和正式Run共用输入校验、Provider和结果语义，差异只在是否持久化、发布授权、调度策略及保留期限。

**实施步骤**
1. 梳理`CxVisionDataFlow`（预览MRK）与`CxVisionRawCpuRunner`（MEM/MOC/MRK正式链）、`CxVisionSeparatedProviders`及Studio/TCT调用图，明确同算法重复代码与必须存在的模式差异。
2. 参数/TypeId/端口优先复用MPM/MPC/MOC现有描述和Resolver；多提供者时用真实Strategy；CPU/SDK适配通过Professional Provider，不把HALCON/AI设备句柄放进FIM。
3. 用一条Gray16真实处理链证明：FIM/Image → 增强 → Mask → Region → Measurement → TypedOutputs/Trace；预览可不落工程，但正式链必须依MEM/MOL定义、MOC编译、MRK运行、MDS提交。
4. 工作台以已有Studio和MPW组合“图像/ROI、参数/算法、结果/Trace、工程”上下文；命令需可发现、可禁用、可取消，无头API不依赖WPF Dispatcher。
5. 后续每项新算法由260项功能编号驱动，先复用FIM/ReferTo成熟代码并做同图真值测试；禁止通过空Provider占位宣称功能完成。

**必须反证**：非法参数/端口/格式、Provider不可用、改变配置版本、取消、异步晚到、源码替换Provider后重跑，预览与正式运行数值一致或报告明确差异原因。

### D. 大图、所有权与FrameStream：IMG-09/10

**设计目标**：默认安全复制不倒退，优先测出真实峰值和复制瓶颈，再由公共Owner提供必要的安全Lease/缓存策略；工业流不等于单个byte[]队列。

**实施步骤**
1. 列出FIM输入、`CxVisionImageInterop`、`CxVisionGrayFrame`、`CxVisionRawCpuRunner.RawProvider`、Mask、WPF Bitmap及MDS中每次复制/分配/释放，建立每Run实际字节预算和峰值测量方法。
2. 用4300×4300、6144×3072 Gray16及代表生产图片，记录Load/Decode、Compute、Commit、Display各段P50/P95、WorkingSet/GC、额外副本；先量化再选优化策略，不预设“必然零拷贝”。
3. 已共享的不可变像素可优先复用引用；引入借用必须有Owner、只读访问、Lease/Dispose、过期与异常释放规则，适配MDS/FDT；资源超限明确拒绝而不是无限排队。
4. 对真实连续帧补Source/Frame/Sequence/Timestamp/Lease及队列字节预算。PreviewLatest允许丢弃过时显示，但生产NoDrop必须有上游背压或持久化能力；DropOldest/Newest须有政策、计数和Trace。
5. GPU/SDK阻塞、探测器断线、队列溢出由公共调度与专业设备Owner协作，不伪造强行中断或硬实时保证。

**必须反证**：连续取消、过载、乱序、输入缓冲复用、UI关闭后任务释放、多帧持续长稳、缺GPU/SDK降级或拒绝；现场HIL另登记。

### E. 全平台AOP：IMG-11/17（同时约束01～18）

**设计目标**：沿现有FLD/配置/权限/MOC/MRK/MDS/事务边界贯穿CorrelationId、RunId、版本、资源及授权，而非引入新的通用日志或AOP代理工程。

**实施步骤**
1. 整理公共Owner已有日志Scope、MRK Trace、运行结果、权限发布、MDS预算、资源诊断接口清单；区分实际源码能力与待补足跨域字段。
2. Invocation/Run与日志关联；记录开始/完成/取消/错误/权限拒绝/版本冲突/资源超额，包含SourceRef、Node/Port、CorrelationId、RunId、配置及Provider版本。
3. UI到执行端采用同一个正式授权机制；UI隐藏操作不代表授权。将审计放在MEM Commit、Plan Publish、真正Run和人工Review的相应公共/专业Owner。
4. 禁止把所有异常都转成“检测NG”；错误代码、业务无缺陷、设备互锁、取消分别表达；日志要脱敏、限频且不泄漏像素数据。
5. 为跨专业调用制定最小稳定TypedRef/Invocation映射并先在Vision→Inspection或Vision↔CT3D真实场景中验证，不让其它专业直接引用Vision私有UI状态。

**必须反证**：同一请求跨Studio→MOC/MRK→Provider→结果→报告能按关联ID查询；异常/中止/权限失败有明确首错和不可变证据。

### F. 工程复用、多ROI与交付：IMG-05/14/18

**设计目标**：用已有MEM Content/Group/MRC与MTC唯一工程事务承接正式ROI与规则，完成R1，再自然演进多ROI和完整用户工作台。

**实施步骤**
1. 基于已存在`CxVisionRoiEngineering.Commit/Open/RunSavedAsync`核对现有Data/Visual双Aspect、SHA256/ROI身份/ConfigRevision，补真实Studio一键一链STA回归。
2. 用一份有意义的工程走完整CASE-07，包括显式保存/重开后重绑同一源图、同Revision重算、导出同Run证据和工程Dirty状态；仅同尺寸不同像素必须拒绝。
3. 先保留单ROI既有API；通过MEM Content/Group/MRC组合逐步实现多ROI，每个ROI稳定ID及规则/参数/坐标，事务与现有Undo/Redo一致；禁止扩展为Vision私有ROI长期列表。
4. 将每个能力的数值、UI、工业证据映射`V2D-XX.YY`，落实CASE-01～08；逐项注明已实现、阻塞及未验证，不把按钮存在等同功能交付。

**必须反证**：同一次操作只增一次工程Revision、Undo/Redo/Save/Open一致、重开错源拒绝、取消无Dirty、旧Run保留、新Run独立、不同宿主/项目无交叉污染。

## 5. 实施批次、依赖和交付规则

以能证明架构的**真实使用闭环**为优先，而不是先大规模“类重构”。每个批次可分小提交，完成一个可运行的纵向切片后才扩大范围。

| 批次 | 顺序及任务 | 前置条件 | 本批交付与退出门槛 |
|---|---|---|---|
| **R1 核心闭环** | IMG-03 → IMG-04（最小Run合同）→ IMG-05；IMG-18同步 | 当前MEM/ROI/Run与Studio源码审查，固定样本和现有测试 | 同一Studio真实ROI工程Save/Open/重绑/重跑/结果/CSV或RAW，旧结果防污染；固定SHA Windows G4/G5证据 |
| **R2 对象合理化** | IMG-01 → IMG-02；并行IMG-12，随后IMG-07 → IMG-08 → IMG-13 | R1基线测试稳定；公开API及Owner映射明确 | 交互状态独立可测、双Pane/坐标正确、Provider/端口/工作台一致，不回归已有功能 |
| **R3 平台治理** | IMG-06、IMG-09、IMG-10、IMG-11 | R1 Evidence字段稳定、R2调用边界稳定；公共Owner共同裁决 | 产物/资源生命周期、复制/峰值与有界流、AOP关联证据；缺设备场景按G4/Blocked实记 |
| **R4 多场景扩展** | IMG-14、IMG-15、IMG-16、IMG-17 | R1～R3形成稳定接口和数据预算 | 多ROI/专业算法/大图多Pane/跨Workbench典型Case逐项可验收；IMG-18持续 |

**依赖门禁**：
- R1不要求先完成“未来全部结果/Artifact体系”，只批准支撑当前CASE-07的最小Run合同；IMG-06再深化持久资产，不得阻塞已完成的小闭环。
- IMG-01/02不能为了重构一次性替换全部已稳定图元行为；每迁移一种手势先取得对应正反测试。
- IMG-09/10/11跨公共Owner，先确认现有机制与API，再提交最小公共增量；没有独立第二通用库。
- IMG-15（完整260项）分功能滚动推进；IMG-18在所有批次同时维护，不允许最后统一补虚构测试。
- R1～R4的阶段完成是**功能与证据验收**，不是按提交数、类数、页面数或文档长度判断。

### 5.1 第一可执行切片：R1-01（开始开发时直接做）

**目标**：对已存在的ROI工程闭环进行完整STA用户验收，先发现缺失的真实交互段再补最小源码，不盲目预设需要重写Run类。

1. 读取当前HEAD的`CxVisionRoiEngineering.cs`、`CxVisionRunSummary.cs`、`StudioShell.ModuleCenter.cs`、`MPWImageViewerControl.xaml.cs`、`RuntimeVisionArtifactsTests.cs`和已有Studio Shell/UI测试；列出当前已由源码覆盖的每一步、尚无调用的步骤。
2. 固定一张Gray16（含Stride）或已登记真实Gray8+复杂ROI样本，记录SHA-256与原始数值、尺寸、位深、ROI的Guid/几何、MHS Revision及预期缺陷像素面积。
3. 在**同一个**Studio工程内创建、编辑、正式提交ROI；执行Undo/Redo、SaveAs、关闭重开；显式重绑源图，按保存Revision调用MRK；结果面板选择Run/Item，查看Mask/Region/Area及原图Overlay；导出CSV与RAW。
4. 用一个完整负面脚本覆盖：同尺寸错像素Hash拒绝、源切换或ROI编辑晚到拒绝、工程Revision冲突、取消、坏参数、空区域、缺Provider/拒绝授权。验证无脏提交、无虚假Passed、不覆盖历史Run。
5. 在测试/Implementation登记**固定Commit SHA、测试命令、样本Hash、结果数值、STA/UIA日志及截图位置、失败修复提交**。若当前环境不能运行Windows/WPF，准确停在G2或G3，绝不写“已验收”。
6. 只有R1-01闭环后，才评估是否需要扩充IMG-04公用结果Schema或调整IMG-07调用链；不能为了“未来目标”先建立空结果框架。

**最小DoD**：一个真实工程、一个真实输入、至少一个ROI、一个新Run、Image/Mask/Region/Measurement证据及Trace、两个用户导出动作、保存重开和必需负例都可重复执行，且不依赖TCT演示流程。

## 6. 测试与工业验收协议

### 6.1 七级证据：逐V2D项和本文件任务双重登记

| 等级 | 必须证据 | 不能冒充 |
|---|---|---|
| G0 需求 | 对应V2D-XX.YY、用户场景、输入输出和Owner | 不能叫已实现 |
| G1 设计 | UML/对象职责/模式/边界/风险与反例通过复审 | 不是C#公共API |
| G2 源码 | 当前master真实源文件、csproj纳入、实际调用入口 | 不是编译通过 |
| G3 编译 | 固定SHA的Windows VS2022/.NET Framework4.8 Release x64成功 | 不是自动测试 |
| G4 自动 | 数值/无效输入/取消/版本/内存与回归实际运行通过 | 不是人工真实操作 |
| G5 UI | 同工程Studio/TestCenter真实WPF STA/UIA与必要人工交互 | 不是设备/工业生产 |
| G6 工业 | 代表性生产数据、SDK/许可证/设备HIL、性能/长稳及记录 | 合成4×4与UI截图不足 |

每项至少**正例、反例、真实调用入口**；异步/执行项补取消和版本冲突；工程项补Save/Open/Undo；大图/工业项补峰值/恢复/设备适用性。TestCenter用于检验共享能力，不拥有独立业务算法。未具备硬件时如实Blocked或不适用（附理由），不编造G6。

### 6.2 建议保留的既有自动化测试与工作流

- `Tests/ArchitectureSmoke/Integration/Vision/RuntimeVisionArtifactsTests.cs`、`RuntimeVisionDataFlowTests.cs`、`RuntimeVisionImageInteropTests.cs`、`RuntimeVisionResultTests.cs`。
- 现有图窗WPF/TCT STA回归与Studio Shell CPU/模块中心自测；检查实际项目中的Test文件、入口名称及自动化ID。
- Windows CI：`imaging-foundation-windows.yml`、`module-center-cpu-windows.yml`、`studio-shell-windows.yml`、`unified-runtime-windows.yml`，以及与修改范围相关的`testcenter-windows.yml`和默认架构回归。
- 既有命令候选：`CxVisual.Tests.ArchitectureSmoke.exe --imaging`、`--vision-results`、`CxVisual.Studio.exe --module-center-cpu-self-test`、`--shell-self-test`、`CxVisual.TestCenter.exe --imaging-viewer-smoke`。**执行前必须从当前Workflow/Program入口再次确认真实参数、工作目录、超时和输出路径。**
- 涉及工程/DAG/Plan发布时追加工程组织、Decision/Runtime相应Windows门禁；不能为绿色结果跳过原有默认回归。
- 性能基准从CPU/内存/GC/图像规格/显卡/SDK版本/P50/P95及峰值等真实量出发；阈值需与用户/现场条件确认，不宣称未经测量的FPS/GB/秒。

### 6.3 八个纵向场景与任务映射

| 场景 | 相关问题ID | 必须保持的关键事实 |
|---|---|---|
| CASE-01 单图/ROI/导出 | 01/02/03/12/13/18 | Gray16原像素不因LUT、ROI、导出而改变 |
| CASE-02 算法实验 | 04/06/07/08/13/15/18 | Enhance→Mask→Region→Measure及Trace同源、同版本 |
| CASE-03 双图配准比较 | 01/12/15/16/18 | 变换可逆、错误坐标/SourceRef拒绝 |
| CASE-04 批量序列 | 03/09/10/11/18 | Frame顺序、取消、队列预算、乱序/坏帧 |
| CASE-05 在线视觉 | 07/09/10/11/17/18 | 可用设备帧/背压、失联/恢复、Inspection只接候选事实 |
| CASE-06 X-Ray/CT投影 | 08/09/12/17/18 | Frame/CalibrationRef/投影几何不混淆 |
| CASE-07 正式工程 | 03/04/05/06/13/14/18 | 同Studio显式ROI事务、Undo/Save/Open/重跑/导出 |
| CASE-08 AI预留 | 07/08/11/15/17/18 | 模型缺失/许可证不满足明确拒绝，不伪装成功 |

## 7. 后续迭代记录格式（每次提交填写）

每次完成一项或一个有意义的纵向切片，在本节追加一条记录，并同步更新对应原Implementation和Testing文档。建议追加如下字段，禁止只填“已完成”：

- **日期/任务ID/批次/负责人/裁决**：YYYY-MM-DD、IMG-XX、R1-XX，相关V2D-XX.YY和CASE；
- **前置代码SHA**：读到的master SHA、Owner/文件定位、ReferTo复用检验；
- **变更与设计证据**：实际类/接口/职责、OOP/GoF/AOP应用、涉及的UML/ADR/公共API兼容；
- **实际提交**：中文Git commit SHA/URL、受影响文件、是否变更.csproj/DLL/NuGet；
- **测试环境**：Windows版本/VS/.NET48/x64/关键SDK、样本Hash/位深/Stride/尺寸；
- **测试**：命令、返回码、Windows Actions workflow/run/job、日志/截图、数值期待/实测、正反例与取消；
- **性能/现场**：CPU/GPU/WorkingSet/内存峰值/P50/P95、设备/许可证及HIL证据，若无则写“未执行”；
- **结果**：G0～G6、Accepted/Blocked/回滚、遗留问题、下个最小切片；
- **纠错**：旧结论被新证据推翻时新加一条订正，不修改旧SHA历史证据为新运行。

初始状态：`IMG-01～IMG-18 = Open（统筹已建立；既有局部源码和历史CI由原台账保持）`。此状态只表示整改任务未逐项关闭，不否认已有成熟功能。

### 7.1 R1-01 首批正式源码：重开工程原图重绑、Run/导出与版本防串线（2026-10-30）

- **任务/Case**：IMG-03（来源与请求代次，部分）、IMG-05（Studio正式工程闭环，部分）、IMG-12（错源投影，部分）、IMG-18（新增测试门禁）；CASE-01/02/07。**状态**：源码与正式Windows测试定义已提交，G2；对应Windows CI尚未返回固定SHA成功证明，G3/G4/G5/G6不得标通过。
- **真正解决的用户操作缺口**：关闭重开MEM工程后，原Studio只允许在一次已有`displayedRun`上点击“运行工程ROI”，没有直接重绑现存ROI的原始文件入口。现在增设`Studio.ModuleCenter.BindSavedRoiRaw`及`BindSavedRoiStride`，明确从**当前工程页MEM双Aspect ROI**获得尺寸/位深/原图Hash；用`FIMRawImageReader.LoadFile`在后台读取、`CxVisionRoiEngineering.VerifySource`核验；不创建新检测页、不伪造Run、不隐式加载MHS。
- **运行/结果改进**：重绑成功后原图进入同一MPW/MVI；`RunSavedRoi`使用MEM中真实ROI与固定参数/Revision恢复MRK，成功后展开多类型结果、激活CSV及RAW导出；`ExportRunRaw`可导出已选Image/Mask或未选时原图，调用FIM无损RAW Writer保留Stride，CSV继续使用唯一PVC导出器；恢复按钮与取消关联。`hasCurrentRun`额外验证ProjectId、PageId、Revision及原图引用；当前工程页/版本变化不允许将旧Run当作当前证据。
- **并发防护**：重绑前验证MEM ROI存在，重绑后核对同一工程会话、当前Page、Revision及PreviewEpoch；旧异步完成不会覆盖新会话。普通RAW和正式ROI Run互斥；MRK取消不发布新Run。基础校验与幂等身份仍归PVC/MEM/MVI Owner，不添加第二运行/数据体系。
- **新增STA测试源码**：Studio `VerifyStudioSavedRoiRebind` 用4×4 Gray16+Stride10真实文件、保存关闭重开、指纹验源、错误行步长/改像素/预取消反例、MRK配置Revision=43及6px²、MPW Mask原图关联、CSV/RAW回读同字节验证。独立CPU闭环测试新增强制第二PASS；同步更新`.github/workflows/module-center-cpu-windows.yml`验收规则，不放宽失败数或绕过旧测试。
- **涉及源码提交**：[`b837f0a`](https://github.com/oudream/cx-visual-x3/commit/b837f0a9ee301afcbc7055fe27a516a32049f31a) PVC源校验；[`d6afefa`](https://github.com/oudream/cx-visual-x3/commit/d6afefafeffe17736106b3023a36883ffaaa4c15) Studio原图重绑；[`9dd1375`](https://github.com/oudream/cx-visual-x3/commit/9dd137547a810c086be26f3fcc9bcdc9f91f0b24) STA正反测试；[`94a8814`](https://github.com/oudream/cx-visual-x3/commit/94a8814ef0cea4591d3914a7403b156149b0524a) 修正像素Hash反例及自动化ID；[`a344b25`](https://github.com/oudream/cx-visual-x3/commit/a344b2570263d0a3e42cb27f2cbb1fee4f15fd8d) CPU双PASS强制CI；[`c5a99f2`](https://github.com/oudream/cx-visual-x3/commit/c5a99f21f92526c3ee11db449df3583eff3b8051) RAW出口；[`89b11a5`](https://github.com/oudream/cx-visual-x3/commit/89b11a5cc1ed2a65e0e8d8ff4984442d26ac9194) RAW控件测试；[`1d90c68`](https://github.com/oudream/cx-visual-x3/commit/1d90c6838e2d44517f0787c31bb31f306d327bed) 当前结果页面与Revision验证。
- **验收待办**：核对最终对应SHA Windows编译、`--module-center-cpu-self-test`双PASS、Studio `--shell-self-test`、Imaging/默认回归；补真实Windows STA完整弹框操作/截图与多次保存重开；实测6144×3072大图性能、设备输入/许可证HIL。Headless/STA自动调用真实MPW控件不等于用户实际弹窗与设备G5/G6。当前RAW重绑仅支持明确指定Little-Endian Gray8/16与Stride，未实现通用设备/网络源绑定。
- **下一实施切片**：在固定SHA CI确认后，补齐真实Studio鼠标/文件对话框UIA端到端和重新运行后Contour/ROI显示的交互回归；逐项升级IMG-03/04/05的Run结果合同，保留原公共API。

### 7.2 R1-01 固定SHA Windows执行证据补录（2026-10-30）

- **证据SHA**：[`1d90c6838e2d44517f0787c31bb31f306d327bed`](https://github.com/oudream/cx-visual-x3/commit/1d90c6838e2d44517f0787c31bb31f306d327bed)。**Windows CI**：[`CxVisual Module Center CPU RAW End-to-End Windows Acceptance #40995860566`](https://github.com/oudream/cx-visual-x3/actions/runs/40995860566)；Job `raw-cpu-closure`完成状态`success`；其中“Build actual .NET Framework 4.8 solution”及“Execute actual RAW MHS MEM MOC MRK Vision closed loop in Studio WPF STA”均为`success`。
- **可升格范围**：此SHA具备Release/x64实际编译（G3）及受控Studio WPF STA两条真实自动测试（G4）：历史CPU链与新增正式ROI重绑的Gray16/Stride10、MEM保存重开、错误源拒绝、MRK复算、MPW产物展示及CSV/RAW导出测试。CI在源码里强制**恰好两项PASS且零FAIL**。
- **仍不关闭**：IMG-03/05/12/18继续`InProgress`；这是固定SHA的自动验证，不等于在真实Studio产品界面人工点击文件对话框或UIA全路径（G5），也不能替代设备HIL及大图/长稳（G6）。同SHA的Studio S1/Imaging专项尚无成功结论时继续列为待验收。
- **下轮要求**：补可复验UIA或人工完整工程操作记录；在确认跨工程最新HEAD与CI后收敛IMG-04最小Run合同，不因本次单样本G4把26类260项标记完成。

### 7.3 R1-01 附加数值/取消回归（2026-10-30，待最新SHA验证）

- [`b9bae60`](https://github.com/oudream/cx-visual-x3/commit/b9bae60a8c018cd78ca405d041c4119b60cc8fd8) 在Studio自动测试中固定4×4 Gray16有效像素SHA-256：`d89466b0fdf25b41a4f4a59603c59bc4dee524fd4912d19e3fab793a3a3a1e73`（16个像素各为小端数值210，不含每行Padding）。
- [`3a8fc2e`](https://github.com/oudream/cx-visual-x3/commit/3a8fc2edd1de81ec579263dd7aa44b8b69a4793b) 补“只修改Stride Padding不构成原像素变更、修改有效像素必须拒绝”双向真值反证；不把文件存储布局与图像有效内容混为一谈。
- [`27e1381`](https://github.com/oudream/cx-visual-x3/commit/27e138115f87e77025b3be48f0e1c30eb85b4fad) 将RAW重绑加入已有取消按钮；取消或页面失效时拒绝显示晚到原图、保留之前的有效图像/Run，不新建任务中心。原FIMRawImageReader读取操作不是可中断块IO，取消承诺限定为任务完成前的验证和发布阻止，后续大图流式中断归IMG-09/10。
- 以上修改后，`27e138115f87e77025b3be48f0e1c30eb85b4fad`的新CI为[Module Center CPU #40996427478](https://github.com/oudream/cx-visual-x3/actions/runs/40996427478)、[Studio S1 #40996427543](https://github.com/oudream/cx-visual-x3/actions/runs/40996427543)、[Imaging #40996427532](https://github.com/oudream/cx-visual-x3/actions/runs/40996427532)，记录时尚未完成；不得自动继承1d90c683历史成功为本SHA通过。

### 7.4 IMG-03/04：原图Generation与Run正式ROI证据（2026-10-30）

- **本次目标**：正式落实四平面证据链中原图变化与结果槽变化的不同语义。不得把MVI临时会话`SourceVersion`（同时包含Result操作）作为MEM源图/工程版本的唯一判断，也不把未绑定ROI的普通Preview虚构为正式工程结果。
- **MVI Owner**：[`f12bf974`](https://github.com/oudream/cx-visual-x3/commit/f12bf974a6ef79603407ff9eb0e17eaa8b033d40) 为现有`MVIImageSession`增`OriginalGeneration`，仅新原图加载开始/成功、新原图设置或Clear改变；不改变既有`SourceVersion`（保留结果会话并发CAS行为与旧调用兼容）。[`fde5cac8`](https://github.com/oudream/cx-visual-x3/commit/fde5cac8ef8740ebf41e177ca561015d384345d7) 补独立代次、Observer与旧异步结果拒绝反例；[`5dcb2caf`](https://github.com/oudream/cx-visual-x3/commit/5dcb2caf71dd3d80ca41e9950bd3459ff53cbfee) 将Studio正式ROI复算和Contour投影的原图失效检查改用新代次，同时继续通过Run引用、PageId、Revision及PreviewEpoch管控其他变化。
- **PVC Owner**：[`e3182a1`](https://github.com/oudream/cx-visual-x3/commit/e3182a1e4d0600f412635f69869bb960fecf7638) 现有`CxVisionRunSummary`补可选`ValidatedSourceHash`、`DefinitionRoiId`、`DefinitionRevision`和只读派生`WithValidatedRoiEvidence`；普通Run三字段为null，**不伪造**工程来源。[`1518cadf`](https://github.com/oudream/cx-visual-x3/commit/1518cadfe9f66a73f46d9333c96445091d5ef8a5) 在MEM原图Hash和工程Revision实际校验之后绑定这组证据，不修改MRK真实Flow/RunId/Items。Contour只读派生必须携带相同来源，原Run不被覆写。
- **反证源码**：[`2640a9e8`](https://github.com/oudream/cx-visual-x3/commit/2640a9e811d8ecfaa74f89c9c45453388b421259) 验证双Run新身份、ROI来源Hash/工程Revision、Contour派生保留证据；[`71b0550f`](https://github.com/oudream/cx-visual-x3/commit/71b0550fb20b52eefc2f37a840c32c2f662cd5ee) 验证普通MRK预览不会带假MEM证据；[`e2778676`](https://github.com/oudream/cx-visual-x3/commit/e27786766550770d6dee1d1b4b695df407966552) 验证真实Studio复算结果身份、来源Hash及Revision。
- **状态**：G2已提交，Windows固定SHA G3/G4待CI；后续保留真实用户文件对话框/UIA及工业HIL验收为G5/G6待完成。上述不是完整RunRecord状态机或持久Artifact/Review模型，IMG-04仍为`InProgress`，不能提前关闭。
- **后续切片**：先确认相关Windows`imaging-foundation-windows`、`studio-shell-windows`、`module-center-cpu-windows`及`unified-runtime-windows`的固定SHA结果；再补Run Status/Validity分离的最小已批准合同以及Studio原Run失效提示。

### 7.5 IMG-04 状态与结果有效性 + 跨体系CI阻塞修复（2026-10-30）

- **CI回溯**：本次核对上一轮IMG-03/04的固定SHA成果：[Module Center CPU #40998235432](https://github.com/oudream/cx-visual-x3/actions/runs/40998235432)、[Studio S1 #40998235407](https://github.com/oudream/cx-visual-x3/actions/runs/40998235407)、[FIM MVI Imaging #40998229101](https://github.com/oudream/cx-visual-x3/actions/runs/40998229101) 已显示成功。这些run分别属于固定SHA，不能当作整个新HEAD的证据。
- **全量编译阻塞**：后续并行CT3D提交令TestCenter `TCTVolumeViewerSmoke.cs(1236)` 引用`Run()`局部的`roiEditCommand`，真实Windows全工程编译报CS0103，复现于[Imaging #40999568629](https://github.com/oudream/cx-visual-x3/actions/runs/40999568629)。[`96c20942`](https://github.com/oudream/cx-visual-x3/commit/96c2094223dda15cccf07db0bb74b73122b1f740)在`VerifyProfessionalPersistence`内部使用`frame.Commands.Any(...)`重新检索现有工作区命令，保留相同CanExecute验收，不跳过测试。
- **默认Smoke阻塞与Owner**：该编译修正后，[Imaging #41003691514](https://github.com/oudream/cx-visual-x3/actions/runs/41003691514)构建成功，但默认架构Smoke报`PROTOCOL_IO_MISMATCH`，堆栈指向`AutomationPlcPointTests.Run`。Automation Owner已在[`5552a931`](https://github.com/oudream/cx-visual-x3/commit/5552a931ff9a)提交独立协议/真实TCP回环修复；此项不能归咎Imaging结果合同。
- **Run状态Owner**：[`be335c67`](https://github.com/oudream/cx-visual-x3/commit/be335c67626fcc2538b0e6529d855d8974548332)沿用`MRKFlowState`作为原执行真源：`CxVisionRunSummary.ExecutionState`只表示已发布Completed Run，MRK失败/取消/拒绝仍从既有MRK结果与Trace取得，不制造虚假的成功产物。新增`CxVisionResultValidity`（Valid/Empty/Stale/MissingArtifact/Invalid）和只读`AssessItemValidity`，根据Item所属Run、源ID、可选SHA、MEM Revision和当前值判断可投影性；Empty表示正常完成但候选数为零，Stale不修改历史证据。
- **测试与Studio接入**：[`2e870e4e`](https://github.com/oudream/cx-visual-x3/commit/2e870e4ee0f39b9707a8a463543e6fd76b80dda4)检查Completed+Empty、跨Run Invalid、错来源 Stale及失败MRK不发布；[`6f940672`](https://github.com/oudream/cx-visual-x3/commit/6f940672d7b329cb7c4cb371cf60f20153b81c78)检查正式ROI哈希/Revision失效；[`adbe2740`](https://github.com/oudream/cx-visual-x3/commit/adbe2740213cfc2019e44c813f54f01e48f270a9)在Studio结果项选择时执行统一有效性判定，区分空结果与旧图错源，原项目/页面/Revision/原图引用门禁仍保留。
- **实际Windows证据**：[`Module Center CPU #41003894207`](https://github.com/oudream/cx-visual-x3/actions/runs/41003894207)（SHA `adbe2740`）成功，包含Release/x64统一解决方案编译和双CPU STA闭环，可登记该SHA G3和对应CPU路径G4；新增`RuntimeVisionResultTests`及`RuntimeVisionArtifactsTests`的完整Imaging/Runtime G4必须依对应Workflow结果另行核验，不以CPU Job替代。
- **仍未完成**：完整失败/拒绝Run的独立审计持久合同、缺失Artifact的真实磁盘场景、真正Studio文件弹框UIA/截图及G6工业设备/大图长稳。IMG-04继续InProgress；本增量属于最小OOP/已有MRK状态真源组合而非第二状态机。

### 7.6 IMG-04 最新Windows集成复验（2026-10-30）

- 前一阶段`adbe2740213cfc2019e44c813f54f01e48f270a9`的[Module Center CPU #41003894207](https://github.com/oudream/cx-visual-x3/actions/runs/41003894207)与[Studio S1 #41003894214](https://github.com/oudream/cx-visual-x3/actions/runs/41003894214)已真实`success`；此两项可确认为对应SHA的Windows G3和受控STA G4。
- 同SHA的[Imaging #41003894164](https://github.com/oudream/cx-visual-x3/actions/runs/41003894164)在完成Windows构建后，默认ArchitectureSmoke因`AutomationPlcPointTests`触发`PROTOCOL_IO_MISMATCH`失败；日志为确切堆栈而非Imaging算法失败。Automation Owner后续以`5552a931ff9a`修复测试中的TCP回环通道配置。
- 为重新覆盖经过跨专业修复的当前代码，在`RuntimeVisionResultTests`补充“Empty候选但有效零统计仍为Valid、未提供当前源必须Stale”测试（`86aa81a5`，`f57ccda8`修正按唯一输出名取值）。[Imaging最新SHA `f57ccda8` #41004342250](https://github.com/oudream/cx-visual-x3/actions/runs/41004342250)和[Unified Runtime #41004342241](https://github.com/oudream/cx-visual-x3/actions/runs/41004342241)触发时均在队列，不能提前宣布完整G4。
- **保留判据**：结果Kind=Statistic且值为0属于有效统计，不因数值为零自动Empty；只有候选计数为0的DetectionSummary标记Empty。Run执行失败、Cancelled、Rejected的证据仍由原MRK状态Owner负责，不能在PVC制造新成功Run。

### 7.7 IMG-01/02 第一切片：单Owner图元编辑State与取消回滚（2026-10-30）

- **目标/风险**：既有MPW多布尔标志控制Move/Resize/Rotate/PolygonVertex，编辑途中向唯一MVI OverlayStore写入不可变新对象，Esc、意外失捕、切工具/图像之前没有统一回滚路径。此为已观察到的结构风险，本切片不声称已经移除所有MPW状态字段或关闭18项。
- **GoF State/OOP**：[`32716947`](https://github.com/oudream/cx-visual-x3/commit/32716947dd0dee3a704876354c925e687a4880b0)新增无WPF `MVIImageOverlayEditGesture`，只持有`MVIImageOverlayStore`引用、编辑前快照和最后一次自身写入；接口`Begin/TryUpdate/Complete/Cancel`，单次仅一对象；取消恢复同一稳定Guid的原始几何，而外部已经删除/锁定/替换时不覆盖新状态。[`5bae2ef1`](https://github.com/oudream/cx-visual-x3/commit/5bae2ef16bb239c2a7f183ba3611e96dfd72948b)加入原MVI .NET4.8 csproj，不建新项目、持久Undo或复制Store。
- **MPW协作**：[`65744af0`](https://github.com/oudream/cx-visual-x3/commit/65744af09f3dca99790fac9dfafd24b9f50b884c)现有`MPWImageViewerControl`四类图元鼠标编辑使用同一State合同，`CancelTransientGesture`承接工具切换、ShowOriginal、Clear及帧更新；[`b0824bb5`](https://github.com/oudream/cx-visual-x3/commit/b0824bb51edd0fef93339c90432476e3f96ff27e)双Pane注册Escape/LostMouseCapture；[`ae623a9a`](https://github.com/oudream/cx-visual-x3/commit/ae623a9a9086e3c64f82b391fd435684d33a24cc)按手势来源Pane提交，避免右Pane鼠标Up结束左Pane编辑；[`ab4564e5`](https://github.com/oudream/cx-visual-x3/commit/ab4564e51b4a1f484627da07a767a4e27a8af911)避免两个Pane重叠拖动。
- **反证**：[`eba9d445`](https://github.com/oudream/cx-visual-x3/commit/eba9d4453635a5cbdbf3bd3ecdddbd7ab1919e13)在`MVIImageOverlayTests`增加独占Begin、非法类型/GUID、Cancel恢复同一对象、Complete后取消幂等、外部样式改动/锁定/删除不回滚覆盖；保留既有矩形/多边形/八柄/旋转/双Pane及图元状态测试。
- **架构限制**：手势更新遵循`MVIImageOverlayStore`原有“UI Owner线程串行调用”约束，没有虚报跨线程CAS；正式MEM/MTC提交及Undo未改变。WPF路径新加Esc/失捕事件接线，但真实鼠标拖动/键盘操作G5须单独验证，不能用SDK测试代替。
- **初始状态**：IMG-01/02=`InProgress`；新代码和反例已提交=G2；针对最新源码的Imaging Windows/Studio/TestCenter CI G3/G4应附SHA和run实际结论，尚未完成时不能写Passed。后续按行为逐类取消MPW重复标志，先保证现有工具正反和PNG可视测试稳定，再扩展复杂ROI。

### 7.8 IMG-01/02 首次绿色Windows及后续待验（2026-10-30）

- [Imaging Windows #41006943702](https://github.com/oudream/cx-visual-x3/actions/runs/41006943702)在提交`65744af09f3dca99790fac9dfafd24b9f50b884c`显示`success`，包括全解决方案Release/x64构建、结构/DAG、真实WPF图窗PNG回归、FIM/MVI/default smoke和三维切片WPF回归。这证明首次MVI图元编辑State与MPW集成的**对应固定SHA G3/G4**，不表示稍后新Esc/LostMouseCapture XAML以及新增MVI反例已验证。
- [`0c6af07b`](https://github.com/oudream/cx-visual-x3/commit/0c6af07b9e30bdada5e0904f017bd8f57b27d3fe)在正式`TCTImageViewerSmoke.AssertVisualLayout`增加真正WPF主/副Pane和Viewer必须Focusable的断言，以确保Esc取消输入有可获取焦点的宿主；最新[Imaging #41007403424](https://github.com/oudream/cx-visual-x3/actions/runs/41007403424)、[TestCenter #41007403416](https://github.com/oudream/cx-visual-x3/actions/runs/41007403416)触发时仍排队。
- [`d92b8d32`](https://github.com/oudream/cx-visual-x3/commit/d92b8d32296ee1ccbcbb3c3b1d1a2d558c4e7502)补实际WPF捕获失败安全分支：所有主/副Pane的`CaptureMouse()`均检查返回值，无法取得鼠标捕获时立即`CancelTransientGesture`；禁止留下无MouseUp可到达的悬挂编辑State。
- **本轮审慎结论**：公共源码与新增自动测试已完成G2，首次集成SHA已通过G3/G4；完整Esc、鼠标失捕、双Pane鼠标竞态真实UIA/人工G5和所有后续SHA的Windows G3/G4需按对应运行记录单独升级。IMG-01/02仍为InProgress。

### 7.9 IMG-02 WPF Esc路由自动验收与跨Owner构建障碍（2026-10-30）

- **前置CI核查**：最新回查`f57ccda83004864c373b54e43b3bc5e968e29d69`的[Imaging #41004342250](https://github.com/oudream/cx-visual-x3/actions/runs/41004342250)及[Unified Runtime #41004342241](https://github.com/oudream/cx-visual-x3/actions/runs/41004342241)均成功；`eba9d4453635a5cbdbf3bd3ecdddbd7ab1919e13`的[MVI手势测试 #41007034955](https://github.com/oudream/cx-visual-x3/actions/runs/41007034955)成功。后续并行编译失败不得误归结为这些历史SHA的Imaging单测。
- **阻塞根因**：早期[Imaging #41007403424](https://github.com/oudream/cx-visual-x3/actions/runs/41007403424)和[TestCenter #41007403416](https://github.com/oudream/cx-visual-x3/actions/runs/41007403416)的Windows日志报`PADPlcPointBridge.cs(111,141)` CS1061：基础`IFTRChannel`不提供`StateChanged`；现有`IFTRChannelTransport`才提供状态订阅，与`MCNConnectorModule`的做法相同。[`0a4cef4d`](https://github.com/oudream/cx-visual-x3/commit/0a4cef4d01fe0df931acc67c0acb5cc988d37fef)最小修正PLC桥接：要求可执行`IFTRChannelTransport`，通过它订阅/解除状态事件；保留断线MDS Stale处理，未修改读写或设备安全策略。该修正属于跨项目构建恢复，不算IMG专业新能力。
- **WPF输入验证**：[`8d94fa04`](https://github.com/oudream/cx-visual-x3/commit/8d94fa0469bcce27fe0ab554582b5b31b3ba7511)在`TCTImageViewerSmoke`新增实控件的`PreviewKeyDown(Escape)`路由回归：加载Gray16原图、切换两线工具、输入首点观察WPF草稿、发送实际RoutedEvent、验证Esc拦截并清除草稿、Store无误提交、重新输入从第一点开始、再切工具草稿归零。它是受控WPF STA合成输入，不冒充物理键盘/鼠标UIA G5。
- **本轮门禁**：[Imaging #41010129867](https://github.com/oudream/cx-visual-x3/actions/runs/41010129867)及[TestCenter #41010129932](https://github.com/oudream/cx-visual-x3/actions/runs/41010129932)绑定`8d94fa04`；文档写入时尚未取得最终成功，必须在其执行完成后登记G3/G4与真实失败细节，不能沿用旧绿色状态。
- **剩余**：真实双Pane手势拖动/失捕/鼠标焦点的全程UIA及工业大图仍需G5/G6；完整MPW字段收敛仍属IMG-01/02 InProgress。


### 7.10 IMG-02 TestCenter WPF验收与Decision专业类型映射修复（2026-10-30）

- **确认固定SHA，明确测试Owner**：[`8d94fa0469bcce27fe0ab554582b5b31b3ba7511`](https://github.com/oudream/cx-visual-x3/commit/8d94fa0469bcce27fe0ab554582b5b31b3ba7511) 的[Imaging #41010129867](https://github.com/oudream/cx-visual-x3/actions/runs/41010129867)中`Build all existing projects on Windows x64`和`Execute real WPF ImageViewer headless window and PNG roundtrip`两个Step均为`success`；后一Step实际运行`CxVisual.TestCenter.exe --imaging-viewer-smoke`，由`Tests/TestCenter/App.xaml.cs`进入`TCTImageViewerSmoke.Run()`，因此新增Esc事件路由的受控WPF G3/G4可以确认。该**整条Imaging Job为failure**，不能标全量通过。另[TestCenter Windows #41010129932](https://github.com/oudream/cx-visual-x3/actions/runs/41010129932)的独立Job亦`success`，但不能将其作为Esc单项通过的直接归因；物理键盘/鼠标G5与工业G6未执行。
- **同SHA Imaging失败真实原因**：[Imaging #41010129867](https://github.com/oudream/cx-visual-x3/actions/runs/41010129867)构建通过，但默认ArchitectureSmoke在`moc.bind.contract_unverified:vision.cpu.threshold.inspect:MDC_BIND_TYPE_UNKNOWN:OUTPUT:Inspection`失败。原因是后续新B2/B3独立Plan测试调用`MOCManifestRuntimeBinding.Bind`时未按Host职责提供`vision.cpu.inspection.result → CxVisionInspectionResult`的`professionalTypes`显式映射。同仓库MDC检查本就要求Unknown专业TypeId保持不可默许。
- **修复不降安全门禁**：[`10e9b1f3`](https://github.com/oudream/cx-visual-x3/commit/10e9b1f30d97ef646dd5d0e474b17be5628b29d2)仅对真实独立Plan运行与版本漂移拒绝两条Host测试调用增加专业类型映射，**不修改Inspector、MOC/RMK授权及Unknown拒绝行为**；保留Plan升级必须拒绝的反例。
- **补图层隐藏反证**：[`9944b63b`](https://github.com/oudream/cx-visual-x3/commit/9944b63b23b8ded0a6b9dc5da91ae03f1d524d24)在`MVIImageOverlayTests`增加隐藏层途中`TryUpdate`拒绝、不能再写入隐藏图元、重新显示后允许独立编辑。相应[Imaging #41010672790](https://github.com/oudream/cx-visual-x3/actions/runs/41010672790)和[Unified Runtime #41010672735](https://github.com/oudream/cx-visual-x3/actions/runs/41010672735)触发时尚在执行，须更新实际结论。
- **状态**：IMG-01/02 InProgress；自动WPF Esc路由当前确认TestCenter G4，完整主副Pane鼠标手势G5、最新全量Imaging/Runtime G4需继续闭环。


### 7.11 IMG-02 隐藏层禁用与跨专业MDS质量修订反例（2026-10-30）

- [`9944b63b`](https://github.com/oudream/cx-visual-x3/commit/9944b63b23b8ded0a6b9dc5da91ae03f1d524d24)的[Imaging #41010672790](https://github.com/oudream/cx-visual-x3/actions/runs/41010672790)、[Unified Runtime #41010672735](https://github.com/oudream/cx-visual-x3/actions/runs/41010672735)均已完成，但**整条Job为failure**。两个Job均在默认架构Smoke因`AUT-PLC-G1`失败；不是IMG-02代码或G4断言失败，不能将此固定SHA记为全量通过。
- **精确根因**：`PADPlcPointBridge.ReadAndMapAsync`对点表漂移/读回失败调用`MarkStale`，因此已有MDS数据质量更新为`Stale`且**质量Revision应递增**，原采样时间`RefreshUtc`、`ChangeUtc`和`CurrentObject`不得变化；旧`AutomationPlcPointTests`错误要求`Revision`完全不变，和公共MDS质量-only合同冲突。[`d4710675`](https://github.com/oudream/cx-visual-x3/commit/d4710675c59c93af6d808cb3989a4c8d9eba6b0a)改为`Stale+Revision递增+两个时间不变+原值不变`同时验证，不删除反例、不允许旧回包作为新Good采样；运行时PLC/设备安全逻辑保持不变。
- **进一步图元反证**：[`9431d358`](https://github.com/oudream/cx-visual-x3/commit/9431d358f3312afeab2e1619b8089c49252683f3)在已有MVI State测试补“隐藏ROI图层不得Begin新Move手势”；先前“编辑中隐藏层则TryUpdate拒绝”仍必须通过。此提交触发[Imaging #41011126920](https://github.com/oudream/cx-visual-x3/actions/runs/41011126920)和[Runtime #41011126974](https://github.com/oudream/cx-visual-x3/actions/runs/41011126974)完整新回归，记录时在运行中，不能预报成功。
- **验证级别**：本轮WPF Esc事件在`8d94fa04`的Imaging Job中构建及`--imaging-viewer-smoke`两个Step已success；完整Job被后续其它默认Smoke阻断。IMG-01/02仍为InProgress；G5人手鼠标/键盘与工业G6继续未验。


## 8. 变更控制、回滚和“不得完成”规则

1. **进入任务前**：对照真实master和涉及的csproj、正式Owner、References与现有测试，明确最小边界；并行更改同文件/公共API时先核对最新HEAD避免覆盖别人的提交。
2. **开发中**：每个提交应可审、作用集中、中文提交说明；首先保护已用API/序列化身份/运行版本语义。涉及公共Owner或依赖方向变化必须审核并更新相应UML/ADR和Tests。
3. **失败时**：提交失败原因、复现输入及CI日志；在master按仓库规则修复。不得因某批重构撤掉已通过功能、放宽数值断言或永久跳过相关CI。
4. **版本化结果**：不得复用旧RunId覆盖历史结果；工程变更/算法重跑是新Revision或新Run；取消/失败不得发布Pending数据或把空结果冒充失败。
5. **交付宣称**：未拿到固定SHA的编译日志不得写G3；仅源码测试存在不得写G4；模拟UI按钮不得写G5；无真实设备不得写G6；本文件无权越过这些证据门禁。
6. **本统筹自身版本化**：只在工作范围、优先级、依赖或Owner裁决发生变化时升级V1.x；按任务实施进展追加第7节记录，避免每次改动造成“方案重写”。

## 9. 完成目标与持续维护

**短期目标（R1）**：Studio用户可以在同一真实工程完整完成原图/ROI→正式工程→MRK运行→结构化多结果→原图准确显示→导出→关闭重开的可复现闭环，同时面对错源、取消、冲突等不会污染有效状态。

**中期目标（R2/R3）**：任何新增工具或算法不必修改一个巨型WPF状态机；各专业遵守统一的Provider/Result/Frame/资源/Trace/权限边界；大图性能和工业时序可量化，公共能力可复用。

**长期目标（R4与260项）**：6群26类260二级功能按V2D编号实际逐项实现、验证及标记现场适用性，支撑Vision、CT3D、Automation、Inspection、HMI、Simulation协同。功能越多，核心对象和公共机制应越稳定，而不是增加平行体系。

> **落地判断**：以“职责明确、变更隔离、共享治理、用户完整可用、固定证据可复验”五项共同决定是否完成。任何一个方向不满足，不能只凭架构图漂亮或CI一项绿色就关闭任务。
