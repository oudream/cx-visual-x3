# CxVisual.TestCenter — 类规格与工程边界 V1.0

状态：**TCT第一阶段设计基线 + 最小真实样例**；不代表已完成Windows编译、工业设备验证或六场景全功能。

## 1. Owner / 技术
- 唯一新测试项目：`Tests/CxVisual.TestCenter/CxVisual.TestCenter.csproj`，程序集和项目名 `CxVisual.TestCenter`。
- WPF、.NET Framework 4.8、传统csproj，编译属性复用 `Build/CxVisual.Build.props`，输出至 `Output/<Configuration>/x64/`。
- 命名空间 `CxVisual.TestCenter.*`；自有类/枚举 `TCT...`，接口 `ITCT...`，不重命名已有被测业务类。
- 只允许 Tests 依赖 Foundation/Mechanism，不允许生产项目反向引用 TCT。
- 目前已有 FoundationSmoke/ArchitectureSmoke/FoundationWpf 保留；TestCenter作为交互入口和新增可视用例，不复制/替代现有专项验证。
- 使用独立TestCase+Runner+Results/Observations的接口，UI不生成假Passed。设备/协议/Graph/Runtime尚无真实适配时必须显示 **Unsupported / 未接入**，不允许空方法通过。
- Graph唯一工程真源仍是机制层CxNode体系；TestCenter不维护第二套持久Graph定义树。
- `Foundation.Infrastructure`保留生产基础设施（日志/时间/监控），测试专用Runner、Fixture、断言、Report均留在Tests。

## 2. 对象、主要属性与方法（第1阶段）

| 类/接口 | 世界/生命周期 | 核心属性 | 关键方法 | 责任与边界 |
|---|---|---|---|---|
| `ITCTTestCase` | Test contract / 常驻Catalog | `Descriptor` | `Execute(TCTTestContext)` | 用真实API测试一项能力；不操作WPF |
| `TCTTestCaseDescriptor` | Immutable metadata / 应用 | `Id,Name,Aspect,Group,CapabilityAreaId,CapabilityId,ObjectId` | 构造验证、只读Get | 稳定能力索引，不承载测试执行状态 |
| `TCTTestStatus` | Enum | NotRun/Passed/Failed/Cancelled | — | 未支持项不得伪装Passed |
| `TCTAssertionResult` | Result / 一次用例 | `Name,Expected,Actual,Passed` | 构造 | 精确断言，Fail时必须保存实际值 |
| `TCTTestObservation` | Result / 一次用例 | `Kind,Title,Details` | 构造 | Geometry/Object等Viewer只读消费 |
| `TCTTestContext` | Execution / 一次用例 | `CancellationToken,Assertions,Observations` | `Assert,Observe,ThrowIfCancelled` | 保持结果收集与业务测试隔离 |
| `TCTTestCaseResult` | Result / 一次用例 | `Descriptor,Status,Elapsed,Error,Assertions,Observations` | 构造 | Runner输出唯一真源 |
| `TCTTestRunSummary` | Result / 一次批次 | `Results,Passed,Failed,Cancelled,Duration` | 聚合 | 汇总不会覆盖单项错误 |
| `TCTTestCatalog` | Service / TestCenter应用 | `Cases` | `Register,RegisterDefaults` | 用稳定TestId拒绝重复注册 |
| `TCTTestRunner` | Service / Test Run | — | `RunAsync(cases, token, onResult)` | 执行真实ITCTTestCase，捕获Exception/取消，异步不阻塞UI |
| `TCTReportWriter` | Stateless helper | — | `WriteText(path,summary)` | 提供无UI的机器退出码+报告 |
| `TCTMainWindow` | WPF UI / Window | TestExplorer、结果区 | RunSelection/RunAll/Cancel/SelectResult | 仅展示Runner结果，Dispatcher封送 |
| `TCTRealCases` | 注册工厂 | — | `Register(catalog)` | 使用现有Foundation.Geometry和Mechanism.Core真实API |

### 第一阶段实际真测样例
- Geometry: `CxRect2D.Contains` 边界内外；`CxRect2D.Intersects`；`CxAffine2D.Transform`。
- Mechanism: `CxDocumentFactory.CreateSample` 的Project/Page/Content/Symbol/Group/Element Ownership；`CxProjectDocument.Commit` 原子回滚；`CxDocumentSerializer.Save/Load` RoundTrip。
- 所有结果由实际调用返回或异常产生；不能在UI固定写入绿标记。
- 外部设备与硬实时能力不在本轮验证范围。

## 3. 关系
```text
TCTMainWindow --> TCTTestCatalog (lists/selects)
TCTMainWindow --> TCTTestRunner (RunAsync)
TCTTestCatalog -- owns --> ITCTTestCase[*]
ITCTTestCase -- has --> TCTTestCaseDescriptor
TCTTestRunner -- creates --> TCTTestContext [per case]
TCTTestContext -- collects --> TCTAssertionResult[*], TCTTestObservation[*]
TCTTestRunner -- produces --> TCTTestCaseResult[*]
TCTTestRunSummary -- aggregates --> TCTTestCaseResult[*]
TCTMainWindow -- presents --> TCTTestRunSummary
TCTRealCases -- calls --> Foundation.Geometry / Mechanism.Core
```

## 4. 后续候选接口（**没有实现时不得算完成**）
- `ITCTTestDiscoverer.Discover()`：插件/程序集自动发现和能力筛选（第一版仅显式注册）。
- `ITCTTestViewer`：Geometry Canvas、ObjectTree/Reference/Connection、Graph、Frame、Runtime各自贡献Viewer。
- `ITCTTestObserver`：采集真实Execution/Resource/Trace/Metric/Frame事件，禁止静态演示数据伪装在线。
- `ITCTTestFrameworkAdapter`：现有NUnit/MSTest等测试用例/结果适配；优先利用成熟框架。
- `TCTTestFixtureService`：输入、Golden、Baseline、随机Seed、异常场景。
- `TCTTestHostController`：危险/可能挂起用例必须用**独立进程隔离**；第一阶段Runner仅支持协作式取消，不声称可强杀卡死用例。
- Runtime测试必须消费现有真实ExecutionInstance/Scope/Trace；未具备该生产接口之前不许出绿色测试结果。
- Channel/Protocol必须使用真实或明确标注的Virtual Channel/Protocol Adapter，不能凭演示TX/RX字符串判通过。

## 5. 设计目标 / 验收
1. 传统csproj与`CxVisual.Unified.sln`新增**1个项目**；`CxVisual.Foundation.sln`继续保持只包含Foundation+FoundationSmoke。
2. UI支持能力树、Run All、Run Selection、Cancel、结果/断言/观察/异常、基本过滤。
3. 无UI `--run --report <path>` 返回明确退出码和持久报告；通过/失败由真实断言决定。
4. 核心Runner应独立于WPF，后续可提取而不更改ITCTTestCase行为契约。
5. 维护现有Smoke并在Windows执行`Build/BUILD.cmd`或VS2022 Release/x64验证；当前环境不能做此验证，**只可报告静态审核**。
6. 后续M2/M3：六场景完整Viewer、Graph/Compile与Channel/Runtime诊断、子进程隔离、性能/压测、报告与CI。
