# CxVisual.TestCenter V4.0 全局工作包与验收台账

> 建档：2026-10-17；现行主规格更新：2026-10-20。本文件**仅是动态实施台账**，名称沿用V4.0以保持历史链接和TCT编号连续；**唯一现行技术主规格**是 [《CxVisual.TestCenter 统一测试体系完整规格 V4.1》](../Architecture/CxVisual_测试体系——完整规格%20V4.1.md)。本文下方历史阶段条目的“V4.0为唯一规范”等表述保留为当时记录，**不再具有现行规范效力**。所有实现/测试状态必须对应明确提交及CI，不能借用旧Commit的成功记录。

## 架构边界

1. 测试支撑只涉及 CxVisual.Foundation.Infrastructure（仅真正跨生产模块的基础服务）和 CxVisual.TestCenter（TCT/ITCT测试框架、证据、管理、WPF）。不增加第七Workbench和第二套生产Runtime/Graph。
2. Foundation/Mechanism/Professional生产对象仍属于原Owner；生产项目不反向依赖NUnit/TCT，并非所有项目都需要引用Infrastructure。
3. NUnit负责常规断言、参数化、异步、Fixture；保留TCT历史兼容与工业观察扩展。六张设计图是一套WPF Shell的六种工作模式，界面不创造Pass/Fail。
4. 测试通过率、正式能力八维覆盖、UI交互验收、设备HIL四种完成状态独立管理。真实Owner缺失只能标Unsupported/Blocked，不能用Mock填绿。
5. 直接在master开发、中文Commit，真实Windows CI和失败反证为放行依据；遇并行提交先合流，绝不强推。

## 17项工作包（工作包ID不是正式CapabilityId或TestId）

| ID | 优先 | 具体目标和放行门槛 |
|---|---|---|
| TCT-00 | P0 | 当前master结构/DAG/Windows全量编译/隔离及重复回归、零用例和故障反证；当前Commit必须绿 |
| TCT-01 | P0 | 开发者新建NUnit.cs→不修改项目文件→自动发现→运行同步/异步/参数化→看失败证据并重跑 |
| TCT-02 | P0 | NUnit跨程序集稳定发现、CaseId/行Key/版本指纹、Worker重建、Skipped/Error/Inconclusive/Cancelled映射不得假绿 |
| TCT-03 | P0 | Run/Attempt/Worker进程树、取消/超时/Crash、资源锁和并发重复：无悬挂子进程、未完成不得标通过 |
| TCT-04 | P0 | 版本化结构化Observation与真实Owner Probe：Run/Attempt/对象/Definition/Instance→事件→Artifact→回放→Viewer |
| TCT-05 | P0 | Artifact、Snapshot、Golden/Actual Diff、历史Run比较、SHA256/损坏/缺失拒绝，禁止自动批准Golden |
| TCT-06 | P0 | 唯一WPF Shell、Explorer/动态Ribbon/DocumentHost/Inspector/Results/Timeline/Selection，完整操作无UI卡死 |
| TCT-07 | P0 | Geometry和Content/Symbol/Object Viewer：真实Expected/Actual/Diff、坐标、Ownership≠Reference、选中及定位 |
| TCT-08 | P1/Owner | Graph/Runtime Viewer：真实Port/Connection/编译诊断、ExecutionInstance/Lease/WaitingReason/Trace，定义与运行实例分开 |
| TCT-09 | P1/Owner | Channel/Protocol Viewer：真实Codec、Channel/Session、Frame TX/RX、损坏/超时/注入/回放，虚拟通道先行 |
| TCT-10 | P1 | Fixture/Dataset/Golden版本、明确审批、身份/权限、并发冲突、只读和审计回放，不再用旧口令直接覆盖 |
| TCT-11 | P1 | Suite/Plan/筛选/参数/资源依赖/失败重跑/预检与保存复现，不自研第二套Runner |
| TCT-12 | P1 | SpecVersion/正式CapabilityId/ObjectId/Requirement与八维Case/Attempt/AssertionEvidence/Commit映射，缺证据不算覆盖；不恢复264行占位表 |
| TCT-13 | P1/P2 | Benchmark P50/P95/P99、CPU内存GPU、事件背压、崩溃与长期泄漏/压力报告，环境可复现 |
| TCT-14 | P1 | WPF UIA自动操作+人工验收：选择/运行/取消/失败定位/Timeline过滤/重跑/历史/布局，不能拿Smoke代替全交互 |
| TCT-15 | 后续 | 六Workbench专业与安全HIL；当前只准备接口和验收清单，不能将业务设备接入冒充已完成 |
| TCT-16 | 持续 | V4.1唯一规范、Owner/代码/TestId/CI证据、任务状态与变更追踪，禁止多份相互冲突标准 |

## M0/M1 实际验收记录（2026-10-17）

**完成状态：M0 与 M1 既定自动化交付范围已经完成，状态不等于整个 TestCenter 或 GUI/工业现场均完成。**

- **验收代码 Commit：** `951a45e1654abe4cc60977fcd551573a917249ea`
- **Windows CI：** https://github.com/oudream/cx-visual-x3/actions/runs/39016695155 （`success`，匹配该代码 Commit）
- **结果：** 109/109 单次隔离通过；327/327 三轮并发重复通过；Failed=0、Error=0、Skipped=0。
- **CI 对照源码：** `Tests/TestCenter`、`.github/workflows/testcenter-windows.yml`。
- **继承原则：** GitHub 后续继续提交时必须重新以新 HEAD 的 CI 验收，不能把本次成功记录当成未来提交自动通过。

| 工作包 | 验收结论 | 具体实现与反证 |
|---|---|---|
| TCT-00 M0基线 | **完成（当前代码基线）** | Windows 完整解决方案、层级依赖、NUnit包恢复、单轮、三轮重复、空选择不假绿，全部成功 |
| TCT-01 开发者最短链 | **完成（NUnit默认作者入口）** | CI 动态创建NUnit `.cs`，不编辑csproj；NUnit `async Task` 和两行参数数据均发现，显式TestId在独立Worker执行，JUnit校验通过；旧TCT入口兼容 |
| TCT-02 NUnit身份与状态 | **完成（受支持的本机/可信外部程序集路径）** | `DefinitionId/CaseId/RowKey`、程序集SHA256、Worker同一版本核对；外部DLL身份；Failed、Skipped、Error、Inconclusive反证；跨宿主多设备部署不在本阶段声明 |
| TCT-03 Worker故障治理 | **完成（Windows受控子进程验证范围）** | Run/Attempt与独占资源、取消/超时/崩溃、进程树终止，超时/异常继续保留真实partial事件证据；任何故障不计Passed |
| TCT-04 Schema Observation | **完成（M1数据生产/传输/回放合同）** | `TCTObservationEnvelope` V1、真实CxRect2D/MDS Owner、Run/Attempt/SourceObject/Definition/RuntimeInstance、PayloadRef；Worker XML与SHA256 Artifact；跨Attempt/DTD/Schema/篡改反证。Viewer的完整交互属于M2/M3 |
| TCT-05 Artifact与历史 | **完成（M1内容寻址与可复现回放）** | SHA256、Artifact负载库存、归档Manifest/Attempt/summary一致性、跨Run Observation Diff、原临时目录删除后新进程回放、损坏/交叉身份拒绝；Golden的企业级认证/权限/多用户审批属于M4 |
| TCT-16 持续治理 | **持续开展** | V4.0为唯一技术规格，台账只管执行状态；保留上述CI和Commit，后续版本按实际证据更新 |

**不能越界宣称完成：** WPF Smoke 已通过，但六场景交互、真实 Geometry/Object 专业 Viewer 是 M2；Graph/Channel/Runtime 专业可视化是 M3；正式八维覆盖率、Suite/Plan 与具认证的 Golden 审批是 M4；性能长跑与工业HIL属 M5/专业Owner。通过的用例数量不代表上述功能完成或业务能力已经正式覆盖。

## 六个交付里程碑与依赖

| 阶段 | 工作包 | 真实完成条件 |
|---|---|---|
| M0 基线恢复 | 00/16 | 解决最新HEAD红色CI，保留构建与测试完整证据 |
| M1 作者与证据主链 | 01/02/03/04/05 | 新NUnit源码最短运行链、真实Owner结构化观察跨Worker保存和回放 |
| M2 一个可用工作台 | 06/07/14首批 | 单Shell在Geometry/Object上真实选择、运行、Diff、Inspector/Timeline联动 |
| M3 复杂运行通信可视 | 08/09与04/05扩展 | Graph/Runtime/Channel以生产Owner实际事件为真源，缺Owner时明确Blocked |
| M4 管理与能力验收 | 10/11/12/14完整版 | 可复现Suite/Plan，Golden治理，八维覆盖由真实证据计算，完整WPF操作验收 |
| M5 可靠性与专业准备 | 13/15/16 | 长时性能/资源回收证据；HIL由具备条件的Owner和真实设备另行审批验收 |

依赖：M0→M1数据/身份/证据协议→M2统一UI；M3的Graph/Channel/Runtime各自依赖生产Owner可观测合同；M4的Coverage依赖M1可靠证据；M5长时与专业验证以稳定平台为基础。TCT-16全程贯穿。

## 首批立即推进

A. 当前HEAD CI闭环；先修真实编译或回归失败。
B. 以Geometry和Mechanism.Data的真实测试贯通Schema Observation、Artifact、Snapshot、Replay，先从端到端一例做精。
C. 同步用这些真实数据做Shell与Geometry/Object的全交互，打通结果选中、断言、Timeline Attempt、失败定位、Diff、重跑。
D. 然后做Suite/Plan最小保存/回放与后续Graph/Channel接入；现场HIL不在本阶段强行展开。

每个工作包必须包含Owner、源文件/Commit、正反用例TestId、Run/Attempt和Artifact、Windows CI链接、GUI人工/自动状态、未完成阻塞。仅类名存在、Mock截图、测试总数增加，都不属于完成证据。

2026-10-17曾有经验证快照38989347097（79/79独立、237/237重复），但其后master持续变动且曾出现Runtime编译失败，不能引用为实时HEAD验收。

## M2 首批真实交互闭环（2026-10-17）

> **本段为增量执行记录，不表示 M2 整体验收完成。** 本阶段不引入新的生产 Owner、第二套 Graph/Runtime/断言框架或新的工程项目。

- **当前M2代码 Commit：** `47263c7def4c167f359e44a8ea7db32a2a54ccc5`
- **当前代码的 Windows .NET Framework 4.8 / x64 CI：** https://github.com/oudream/cx-visual-x3/actions/runs/39041466941 （`success`，匹配上述 Commit）
- **运行结果：** 完整解决方案编译、项目结构/DAG、既有 NUnit + TCT + Worker + 证据回放步骤、WPF Shell绑定/内存交互Smoke均通过；Catalog发现114例，3轮并发重复342个Attempt成功；CI已核查Attempt身份及失败/超时/空选择反证。
- **TCT-06（进行中）：** 唯一 `TCTMainWindow` 将专业Viewer的观察选择路由到 Inspector；Results选中Attempt时 `TCTTestTimelineControl.FocusAttempt` 现在实际切换到该Attempt过滤范围，清空焦点恢复全部事件。未声明动态Ribbon全量、布局持久化或完整工作台验收。
- **TCT-07（进行中）：** `TCTTestViewBase` 从真实 `TCTTestObservation` 提供可点击观察项；Inspector显示Observation原文、Schema、RunId、AttemptId、SourceObjectId、DefinitionAddress、RuntimeInstanceId与Evidence/Payload SHA256（只读，不推导Pass/Fail）。有结构化证据时显示原有Owner身份；没有Envelope的旧记录明确为Legacy，不伪造身份。
- **TCT-14（首批Smoke通过，完整交互验收未完成）：** `App.xaml.cs --ui-smoke` 新增真实 `TCT.M1.OBS.001`（Foundation.Geometry/CxRect2D跨Artifact观察）、`M-OBJ-001`（Mechanism.Core/CxProjectDocument Ownership）、Viewer按钮事件→Inspector选中及结果Attempt→Timeline过滤的WPF进程内回归。该Smoke是实际WPF控件事件验证，**不是**Windows UI Automation全流程回归或人工验收。
- **待补：** V4.0技术主规格原文需要在统一可访问位置归档以便逐项核对；Geometry Expected/Actual/Diff更广泛数据合同、对象Reference区分、历史选择/重跑、完整UIA、人工截图验收、M3/M4/M5及工业HIL保持未完成。六场景参考图不是代码实现证据。
- **修改范围：** `Tests/TestCenter/UI/TCTTestViews.cs`、`TCTControls.cs`、`TCTTestTimelineControl.cs`、`MainWindow.xaml.cs`、`App.xaml.cs`。Foundation/Mechanism/Professional生产源码未被修改。


## M2 MEM统一工程组织与WPF六场景真实交互增量（2026-10-18）

> 以下是 **M2 的真实增量验收**，不是 M2/六界面/M3～M5 的完整完成声明。生产 Owner 使用最新 `MEMNode/MEMEngineeringSession/MEMProjectSnapshot/MRCVisualItem/MRIObjectAddress`，原 `CxNode/CxProjectDocument` 旧组织测试不再作为当前权威真源。

- **验收代码 Commit：** `a6d3bdfd21b9ae062e4ded5820715954369af69e`（`master`）。
- **Windows CI：** https://github.com/oudream/cx-visual-x3/actions/runs/39114226490 （`success`，匹配上述代码）。Windows .NET Framework 4.8 / x64 全方案编译、Worker/NUnit、WPF UI Smoke、结构化Evidence/Replay、空选择及异常反证、3轮并发测试全部成功。Catalog 116，重复Attempt 348/348。
- **TCT-04/05（M1合同增量）：** `TCTEngineeringSnapshotCodec` 将真实已提交 `MEMProjectSnapshot` 的节点、Parent/Child、独立Reference、Visual、Property/Port/Connection/Aspect 生成受限、版本化、只读XML负载；用已有 `TCTObservationEnvelope` 与 SHA256/Attempt/Run Artifact跨Worker传输。反证包括不存在父节点、重复Child、非法引用、断开子树、循环、DTD、跨Project比较拒绝，不能把纯文本冒充工程结构。
- **TCT-06（M2进行中）：** 单一 `TCTMainWindow` 与已有 RibbonTray 加入“概览/Geometry/ObjectTree/Graph/Channel/Runtime”模式选择；不具真实Owner观察的模式显示 `NoEvidence`，不计真实Viewer；Results/DocumentHost/Inspector/Timeline沿用统一Shell，不新增项目和第七Workbench。
- **TCT-07（M2进行中）：** `TCTObjectTestView` 不再解析 `Observation.Details` 的箭头文本构树，改为从经过归档核验的 `MEMProjectSnapshot` 展示真实Ownership、Reference、节点身份/Visual以及Property/Port/Connection/Aspect。点击Object/Reference进入Inspector“工程对象”，后者含定义地址和引用 `InstancePath`。支持同工程双已提交Revision的Before/After节点差异，只对证据事实显示，不能替代正式Golden审批。
- **TCT-14（已通过进程内WPF控件事件Smoke，完整UIA未完成）：** `--ui-smoke` 验证真实 `M-OBJ-001` 的Snapshot/Artifact、跨进程Worker→Viewer、树节点→Inspector、六模式切换NoEvidence、不计虚假Viewer、`M-OBJ-006` 双Revision差异。该验证不是人工截图对照、鼠标级Windows UI Automation或六场景完整交互验收。
- **测试用例与修复：** `M-OBJ-005` 验证MEM固定快照/非法Owner证据，`M-OBJ-006` 验证基于稳定NodeId的真实Revision变化。由于并行开发已令生产 `CxSceneProjection` 展开Symbol引用实例，旧 `TCT.MVIS.001` 固定场景元素数导致CI失败；现已根据真实Owned图元与Reference实例数量核算，并验证 `MRIObjectAddress.InstancePath`，修正错误的“Content owns Symbol”文本。失败CI归档不删除，最终通过CI如上。
- **项目与源码：** 新增 `Tests/TestCenter/Models/TCTEngineeringSnapshotCodec.cs`，修订 `CxVisual.TestCenter.csproj`、`Cases/TCTRealCases.cs`、`Cases/TCTMechanismActualCases.cs`、`UI/TCTTestViews.cs`、`UI/TCTControls.cs`、`MainWindow.xaml.cs`、`App.xaml.cs` 及 `README.md`。未创建生产第二套工程树、Graph、Runtime或新的TestCenter项目。
- **保持未完成：** M2完整Geometry/Content/Symbol的Expected/Actual/Golden Diff、真正动态的Context Ribbon全命令、历史GUI浏览与失败重跑、完整UIA与人工六图验收；M3工程H07→MRK正式翻译/SourceMap及Graph/Runtime/Channel结构化Viewer；M4 Suite/Plan、正式八维覆盖与审签Golden；M5 Benchmark/长跑/专业HIL。不得以这次成功CI冒称这些功能完成。


## 第一阶段软件闭环实际验收（2026-10-21）

三阶段重排后的**第一阶段：统一工作台 + Geometry/Object**已完成现有软件实现及对应自动CI验证，人工界面仍待签认。详见：[第一阶段统一工作台与GeometryObject——实现及验收 V1.0](CxVisual_TestCenter_第一阶段统一工作台与GeometryObject——实现及验收_V1.0.md)。

- 代码SHA：`ab4e85c71574a6c3bda911338a749e07621de5fb`；Windows CI：[39591953493](https://github.com/oudream/cx-visual-x3/actions/runs/39591953493) **success**。
- NUnit/TCT Catalog 125，三轮并发375个Attempt；Owner结构化Artifact/Replay、History双Run Diff、错误证据反证通过。
- 统一AvalonDock Shell、动态Ribbon、单多选、Suite/Plan最小保存执行、Geometry Expected/Actual/Diff/单位容差、MEM Ownership/Reference/Symbol InstancePath、历史回放、WPF/UIA两Owner真实操作与四张截图已验证。
- **未完成状态独立保留**：设计01～03人工视觉验收、全DPI键鼠/真实用户验收；阶段二Graph/Channel/Runtime；阶段三正式Golden/八维覆盖/可靠性和专业HIL。
- 每次后续master变更仍须重新匹配代码SHA与CI，不能把此快照标为永久最新验收。


## 第二阶段Graph、Runtime、Channel真实接入记录（2026-10-21）

此节为V4.1唯一技术主规格下的实际增量台账，沿用TCT-00～TCT-16编号；**绝不是第二阶段整体完成声明**。Graph、Runtime和Channel均由原生产Owner计算/编解码，TestCenter仅做校验过的Run/Attempt Artifact只读投影。

| 工作包及结果 | 真实源码、Owner与范围 | 对应Windows验收及未完成 |
|---|---|---|
| TCT-08 Graph→Runtime V1 | MEM快照、MOL原Block、MOCBuild/CompiledPlan/SourceMap、MRK Flow/StepState/Trace；WPF只读Graph Tree与Runtime Timeline，Step→Block→Inspector | 21f62994 CI [39615126778](https://github.com/oudream/cx-visual-x3/actions/runs/39615126778) success；d15ff59d CI [39615472553](https://github.com/oudream/cx-visual-x3/actions/runs/39615472553) success。完整MRK Session/Journal/Lease/Wait并未验收 |
| TCT-08 Graph V2/MPC Port+Link | 实际持久的MOLWiringDefinition/MPCPortDescriptor/MOLLink，保持Port类型、方向、BlockId、LinkId与MOC Step分别可追踪；真实MOC→MRK正例和伪造映射反证 | e8402c92 CI [39617372824](https://github.com/oudream/cx-visual-x3/actions/runs/39617372824) success。更复杂的数据端口、跨Content、失败编译诊断UI待继续 |
| TCT-14 Graph/Runtime真实Windows UIA | Explorer搜索→Selection→Run→两Viewer→CompiledStep→Inspector；截图01～06 | 6ea1bc3b CI [39615910115](https://github.com/oudream/cx-visual-x3/actions/runs/39615910115) 初次failure；修复17f5b4d9 CI [39616272134](https://github.com/oudream/cx-visual-x3/actions/runs/39616272134) success，保留失败历史 |
| TCT-09生产Virtual Channel | 原Mechanism.Connectivity/M13-M14补IMCCChannel、MCCVirtualChannel、MCCCxChannelSession、IMPAProtocolCodec、CRC16长度帧及增量Parser；真实字节分包/粘包/校验失败/断连/重开；TX与RX raw、decoded payload区分 | e5f212c9 CI [39618049072](https://github.com/oudream/cx-visual-x3/actions/runs/39618049072) success；171c39aa CI [39618318357](https://github.com/oudream/cx-visual-x3/actions/runs/39618318357) success。Virtual不是TCP、PLC或现场HIL |
| TCT-09 Channel正反例和Viewer | TCT.M3.CHANNEL.001真实Virtual TX/RX/Error/恢复和SHA256观察；.002半包/粘包、CRC及Schema/DTD反证；WPF显示HEX、Session、Correlation、Parser结果，Frame→Inspector | 31e9a1b6 CI [39618617090](https://github.com/oudream/cx-visual-x3/actions/runs/39618617090) success；66d814ae CI [39618814456](https://github.com/oudream/cx-visual-x3/actions/runs/39618814456) success；GUI全面诊断仍需补强 |
| TCT-14 Channel真实Windows UIA | 02ea37cc增加Explorer→Channel Owner运行→UIA实际选帧→Inspector，新增07-Channel截图和七张截图门禁 | [39619024972](https://github.com/oudream/cx-visual-x3/actions/runs/39619024972) 已观察UIA步骤success；**完整CI仍须以该Run最终conclusion为准** |

新增测试TestId：TCT.M3.GRAPHRUNTIME.001（MOL V1到MRK）、.002（非法证据拒绝）、.003（MOL V2 MPC typed Port/Link）；TCT.M3.CHANNEL.001（生产Virtual分包、故障及恢复）、.002（Parser半包/粘包和恶意证据拒绝）。归档读取仍采用原TCTObservationReplay与Artifact SHA256，未知Schema/跨Attempt不得伪造Viewer成功。

**第二阶段仍未完成：** MRKRuntimeSession、资源Lease及等待队列、Journal、完整Trace/实时刷新及Run回放；Graph MOC错误定位、多Content/多实例的更完整关联；生产真实TCP/UDP/串口Channel/工业Protocol与完整安全授权；Studio→MEM→MOL→MOC→MRK→MDS→TestCenter六场景闭环；异常/多线程/长稳/全部UIA与人工设计图04～06验收。

**第三阶段仍未完成：** 正式Suite/Plan依赖/资源、Fixture/Dataset/Seed、Golden审批与安全审计、正式CapabilityId/Requirement八维Evidence、Benchmark P50/P95/P99、六Workbench专业扩展、受控Real/HIL及AB C综合验收，不能借用上述成功CI标绿。

| 状态级别 | 第一阶段 | 第二阶段本次Graph/Runtime/Virtual Channel切片 | 整个第二/第三阶段 |
|---|---|---|---|
| 设计确定 | 已 | 已 | 主规格V4.1已确定 |
| 代码实现 | 对应第一阶段已 | 上述增量已提交master | **部分/未完成** |
| Windows自动测试 | 既有成功证据 | 上表已匹配的Commit成功；最后UIA提交需终态复核 | **未完成** |
| Windows UIA | Geometry/Object已过 | Graph/Runtime成功；Channel步骤已过，等待最终Run结论 | **未完成** |
| 人工界面 | **未签认** | **未签认** | 未签认 |
| 工业设备/HIL | 不适用 | **Virtual不等于Real/HIL** | 未验收 |
