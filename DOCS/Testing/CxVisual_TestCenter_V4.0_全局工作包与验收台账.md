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