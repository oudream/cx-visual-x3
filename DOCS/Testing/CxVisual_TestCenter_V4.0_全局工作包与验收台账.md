# CxVisual.TestCenter V4.0 全局工作包与验收台账

> 建档：2026-10-17。这是执行台账，不是第二套规范；权威技术定义仍在《CxVisual.TestCenter统一测试体系完整规格V4.0》10章及附录。状态随GitHub master和对应Windows CI更新，不能引用其他Commit的成功数据代替当前验收。

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
| TCT-16 | 持续 | V4.0唯一规范、Owner/代码/TestId/CI证据、任务状态与变更追踪，禁止多份相互冲突标准 |

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