# CxVisual.TestCenter · 代码实施与验证基线 V3.0

## 定位与不可违反的边界
- 唯一测试项目：`Tests/TestCenter`；WPF、.NET Framework 4.8、C# 7.3、x64。
- TestCenter 是Platform的测试工具，不是第七个Workbench；Foundation/Mechanism/Professional不能反向依赖测试项目。
- 所有测试中心**类型**统一 TCT，**接口**统一 ITCT；真实被测对象使用已有原生前缀。
- Owner规则：Geometry/Math/数据等在其Foundation子方面；公共日志F*Log和FCRWorker在Foundation.Infrastructure；Graph/Runtime在各Mechanism Owner；TestCase/Runner/Fixture/Assertions/Viewer留在TestCenter。**各项目仅按需要引用Foundation.Infrastructure**，不全局强制。
- GUI不是判定真源；真实通过/失败/Unsupported/Timeout由TestRunner及断言返回。
- Graph、Protocol、Runtime三个专业数据Viewer已提供可插拔呈现，但当前无生产Observer Adapter时不产生伪造观察数据。测试框架建设已落地，不代表专业对象/设备真实业务接入完成。

## 项目内逻辑模块与Owner
| 模块 | 主要类型 | 责任 |
|---|---|---|
| Core | ITCTTestCase, ITCTAsyncTestCase, ITCTTestDiscoverer, ITCTTestObserver, ITCTTestFrameworkAdapter, TCTTestRunRequest, TCTTestContext, TCTTestCatalog, TCTTestRunner, TCTTestRunSummary | 单点真源；测试描述、状态、断言、执行与汇总 |
| Discovery | TCTReflectionTestDiscoverer, TCTTestDiscoveryService, TCTTestAttribute, TCTAttributeFrameworkAdapter | 类型/属性发现、稳定ID拒绝重复；NUnit/MSTest未来可按接口接入 |
| Execution | TCTTestExecutionCoordinator, TCTTestHostController, TCTWireCodec, TCTTestResultCollector | InProcess/ChildProcess、超时终止、取消、Worker协议、结果收集 |
| Fixtures | TCTTestFixtureService, TCTBaselineService | 数据输入路径隔离、Golden比较和明确审批更新 |
| Reports | TCTReportWriter | Text/JSON/JUnit，保留实际断言与错误证据 |
| UI | TCTMainWindow, TCTTestExplorerControl, TCTTestInspectorControl, TCTTestResultsControl, TCTTestRibbonController, TCTTestDocumentHostControl | 单一WPF Shell，动态上下文、结果与断言 |
| Viewer | ITCTTestViewer, TCTGeometryTestView, TCTObjectTestView, TCTGraphTestView, TCTChannelTestView, TCTRuntimeTestView | **只读**真实Result/Observation，不判定测试状态 |
| Cases | TCTRealCases, TCTFrameworkCases, TCTRegistration | Foundation.Geometry / Mechanism.Core真实测试；测试框架自身回归 |

## 运行方式
GUI：`Output/Debug/x64/CxVisual.TestCenter.exe`

无UI：`CxVisual.TestCenter.exe --run --report TestCenter.junit.xml --format junit --isolate`

JSON：`CxVisual.TestCenter.exe --run --report TestCenter.json --format json --filter GEO`

用例：`CxVisual.TestCenter.exe --run --id TCT.CORE.001 --report one.txt --format text --timeout-ms 30000 --repeat 10 --seed 42`

- `--isolate` 默认启用：每个用例启动同EXE的 `--worker <id> <result> <seed>` 子进程；超时/取消可终止Worker。
- `--inproc` 为可信场景调试入口，**无法硬性终止死锁线程**；默认生产验收不可用它替代隔离。
- 工具退出码：0 全部执行并通过、1 失败/Unsupported/无用例、2 参数/宿主异常。Worker固定退出0只说明结构化结果已写，实际状态必须读XML。
- 保存/加载的Fixture、Golden和报告不得改变被测Definition；报告有唯一Schema版本。Golden更新需要显式 `APPROVE_BASELINE`。
- 在WPF主线程只做导航/显示，测试执行通过协调器异步完成，子进程回传的断言/观察不能由UI覆盖。

## 契约成熟度与验收状态
| 项目 | 当前源代码 | 正式验收 |
|---|---|---|
| CLI/WPF项目、单项目结构、TCT命名 | 已写入 | 必须在Windows x64运行 |
| 测试发现、断言、Result/Status、筛选/重复执行 | 已写入 | 检查覆盖用例实际运行 |
| 隔离Worker、结果协议、超时、取消 | 已写入 | 必须执行超时/崩溃/取消系统级注入回归 |
| Text/JSON/JUnit、Fixture/Golden | 已写入 | 格式解析与边界回归 |
| Geometry/Object专业观察 | 有真实样例 | GUI人工观察坐标、树语义 |
| Graph/Protocol/Runtime Viewer | 数据驱动界面 | 等真实生产Observer契约接入，暂不声明专业功能通过 |
| NUnit/MSTest完整Adapter | 接口已预留；本次有本地Attribute适配器 | 外部框架未接入、不可声称完整兼容 |
| 大量并发压力、长任务稳定性 | 可用Repeat/Worker继续扩充 | 长时间基准与硬件验收未执行 |

## 门槛
1. Windows VS/MSBuild、NET48+x64：`Build/Verify-Structure.ps1`、`Build/BuildAndTest.ps1` 全部成功并保留Build.log。
2. TestCenter CLI返回0、有JUnit/JSON报告；每个用例必须有断言，无断言为Inconclusive，Unknown/Unsupported不算PASS。
3. 故意注入失败、超时、重复ID、非法Fixture等必须给出准确状态。
4. GUI能切换Foundation/Mechanism、显示真实断言、异常、结果，Viewer没有数据时显示缺口。
5. 未获得实际Windows日志前不得写“已编译/已完整验收”。
