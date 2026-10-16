# CxVisual.TestCenter 双项目完整测试体系与开发者 API 收敛规格 V3.2

**状态：架构规范（设计收敛版）／可用于分阶段开发的合同基线；不代表所有能力已实现。**  
**定版日期：2026-10-16**  
**适用工程**：`oudream/cx-visual-x3`，TestCenter WPF / .NET Framework 4.8 / C# 7.3 / x64。  
**资料来源**：V2.0《详细架构与类契约规格》、V2.1《FMP子方面归属与公共接口依赖设计》、V3.0《开发者优先双项目完整类接口枚举规划》、V3.1《开发者简用性复审与 API 收敛建议》、六张 TestCenter 界面原设计，以及 2026-10-16 仓库 master 的关键源码。  
**区别**：章节“现状”是已经核对的源码事实；“目标签名/目标行为”是本次设计决定，须通过未来代码+CI才算落地。

## 0. 管理结论及冻结范围

**冻结的不是250个独立类，而是“一条默认开发者路径 + 同一目录与Worker真源 + 一个Run/Attempt证据模型 + 两项目唯一边界”。** V3.0 的 250 项全量保留为审计追踪，在附录逐项裁决；不得机械全量生成C#空类。

核心决策：

1. **只有两个测试支撑项目**：`CxVisual.Foundation.Infrastructure`（真实生产日志/并发/监控；不可含测试概念）与 `Tests/TestCenter/CxVisual.TestCenter`（**一个** WPF测试应用程序集）。生产领域对象仍在它们自身的 Foundation、Mechanism、Professional Owner，测试项目按需引用；不要求所有项目引用 Infrastructure。此两项目指测试支撑收敛，而非替换现有生产子项目。
2. **普通作者默认且唯一入口**：`[TCTTest("TestId")]` + `TCTTestContext` + 标准 C# Arrange/Act/Assert；普通作者无需 Suite、Plan、Builder、Catalog、Worker、ResultCollector。属性式构建支持同步 `void`、异步 `Task`。
3. **不冻结 Fluent `.Build()` API**：V3.0 的 `TCTTest.Define/Scenario/Parameters/Benchmark` 只保留候选规划，未提供跨进程可重建的安全方案前不得作为生产承诺。常规场景用 Attribute/Context；批量输入新增单一 `TCTCaseSourceAttribute`。
4. **将 TestId、CaseId、RunId、AttemptId 区分**，参数行由稳定 RowKey 派生CaseId，重跑保持Definition/Fixture/Seed一致；Worker验证程序集与定义指纹，不允许仅凭进程内闭包注册声称可隔离重建。
5. **“单个明确选中”具有确定语义**：显式 `--id` 精确匹配优先于默认Smoke分类筛选，但不能绕过设备安全/环境要求；找不到/被阻断给出明确失败与预检原因，0次实际测试必须非零退出。
6. **能力通过与正式覆盖验收分离**：一个Case可在Unmapped条件下通过；正式能力验收必须有已审核规格版本、Capability/ObjectId、合同维度及真实AssertionEvidence映射，缺项=未覆盖，而不是统计Pass数量。
7. **UI只读**：六种工作模式共用一个Shell；Viewer不得修改TestResult；需要基于SchemaVersion结构化Observation/Artifact才能输出真实图形。没有生产Graph/Channel接入时明确NoEvidence/Unsupported。
8. **隔离/安全不混淆**：Worker Kill只作用测试进程，不代表PLC、X-Ray、运动控制或外部操作被安全停止。真实设备一律显式授权，默认Virtual/Replay。

### 0.1 设计目标的可量化验收

| 指标 | 通过门槛 |
|---|---|
| 开发者零配置 | 新建1个方法/1个源文件 + 1个断言，不修改 Catalog/Runner/Host/Report代码 |
| 编译与发现 | 在约定Cases目录新增源码后自动包含；GUI、CLI和Worker对TestId、定义指纹一致 |
| 同步和异步 | `void`与`Task`同入口；真实await、超时、异常栈、取消状态正确 |
| 单项运行 | Integration/Regression/Benchmark显式ID不被默认Smoke静默过滤，预检阻断有解释 |
| 参数化 | 一方法+100稳定RowKey，恰好100 CaseId、100结果；失败行可单独重跑 |
| 可信结果 | 0断言Inconclusive、0用例非0退出、Worker崩溃/超时/清理失败绝不Passed |
| 证据链 | CaseId→AttemptId→Assertion/Artifact/SourceLocation→Run/History/Viewer可相互定位 |
| Owner边界 | 生产项目不依赖TestCenter，Foundation不向上依赖，项目依赖无环 |

### 0.2 本次与先前文档关系

- V2.0 完整测试范围及六图设计：继续有效，作为测试工作台/UI能力目标；**不认为已全部实现**。
- V2.1 唯一Owner/依赖规则：继续有效且优先；强制“所有工程引用 Infrastructure”的推断一律无效。
- V3.0 250项：维持候选索引，由本版逐项给出裁决（附录），不以规模衡量质量。
- V3.1 U01-U12：作为易用性核验的必须项目，本版将其转化为目标合同及验收场景。

## 1. 开发者真实使用：先冻结三个层次，不混用第二套DSL

### 1.1 普通开发者：一个Attribute、一个Context

**目标API（待实现，不能直接照抄当作已可编译签名）：**

```csharp
[TCTTest("F.GEO.RECT.001")]
public static void RectContains(TCTTestContext test)
{
    CxRect2D rect = new CxRect2D(0, 0, 100, 80);
    test.AssertTrue(rect.Contains(new CxPoint2D(25, 30)), "矩形包含");
}
```

- `TCTTestAttribute(string testId)`；可选Name、OwnerProject、Group、Kind、Tags、CapabilityId、ObjectId。若未声明Owner/Aspect，存为Unknown/Unmapped；方法名作显示名，Group取声明类或显式元数据。**不得按文件路径推断正式CapabilityId**。
- `TCTTestContext.AssertTrue(bool, string)` 和 `AssertFalse(bool, string)` 自动生成结构化Expected/Actual；原有四参重载保留兼容；失败抛 `TCTAssertionException`，同一Collector记录一次断言。
- 当前源码事实：`TCTTestAttribute`仍强制四个构造参数，Attribute发现器只接受 `static void(TCTTestContext)`；旧 `.csproj`逐项声明Cases源文件。所示二参断言与单参特性属于本版设计目标。
- 旧用例不改仍可运行，不以全量迁移作为先决条件。

### 1.2 异步开发者：只改返回Task，真正await

```csharp
[TCTTest("M.RUNTIME.PLAN.001", Kind = TCTTestKind.Integration)]
public static async Task RuntimePlanAsync(TCTTestContext test)
{
    // 调用真实Mechanism.Runtime API，不使用测试版MRKEngine。
    MRKFlowResult result = await RunRealRuntimeFlowAsync(test.CancellationToken);
    test.AssertEqual(MRKFlowState.Completed, result.State, "执行完成");
}
```

**合同**：发现方法只能是`public/internal static void/Task (TCTTestContext)`；拒绝`async void`、泛型开放方法、任意参数、`Task<T>`(除非未来独立签名合同)；返回Task由统一Executor等待；异常解包、取消、超时与执行身份透传。`ConfigureAwait(false)`应放在内部Worker执行链而非要求测试作者学习特殊调度。**同步上下文不能依赖WPF Dispatcher**；需STA UI测试时由单独宿主策略负责。

### 1.3 参数数据：100行只写一次测试逻辑

```csharp
// 目标API；待实现Version/RowKey/数据反序列化与Worker校验
[TCTTest("F.GEO.RECT.TABLE")]
[TCTCaseSource("Fixtures/Geometry/rect-cases.json", Version = "2", RowKeyField = "key")]
public static void RectTable(TCTTestContext test, TCTRectRow row)
{
    CxRect2D actual = new CxRect2D(row.X, row.Y, row.Width, row.Height);
    test.AssertEqual(row.Expected, actual.Contains(new CxPoint2D(row.Px, row.Py)), "Contains");
}
```

参数化另有**独立允许的签名**：`static void/Task (TCTTestContext, TRow)`，必须有 `TCTCaseSourceAttribute` 且数据行可确定性重建。Loader只读且受限路径，禁止类型名任意反序列化；重复RowKey、数据哈希不一致、缺字段必须阻断发现或执行并留下诊断，不能跳过错误行伪造通过。

### 1.4 平台维护者：Plan/Suite由管理端使用，普通作者不填

```csharp
// 示意：JSON/GUI的一个持久Plan对象；不要求Case作者写Builder。
TCTTestPlan plan = TCTTestPlan.Load("Foundation-Smoke.json");
TCTTestRunRequest request = plan.PrepareRun(TCTTestRunProfile.CI);
TCTTestRunSummary summary = await coordinator.RunAsync(catalog, request, token, onResult);
```

`TCTTestPlan.Load/PrepareRun`是目标签名，**不是现有源码**。Plan只描述不可变步骤与约束；每次Run有自己的RunId和执行环境，不把结果存回Plan。多Owner运行只借助已解析的TestId/正式能力ID，不在TestCenter重新创建生产调度器。

### 1.5 谁需要学哪几个类型？

| 角色 | 必需使用类型 | 次级扩展 |
|---|---|---|
| 普通开发者 | `TCTTestAttribute`、`TCTTestContext` | `TCTCaseSourceAttribute`、`TCTTestSourceAttribute` |
| 需要参数/Fixture的作者 | 上述+`TCTCaseSourceAttribute` | `TCTFixtureReference`、`TCTTestDataSet`（通常由框架提供） |
| 能力开发者 | Attribute/Context，生产Owner API | `ITCTTestProbe`（需要实时诊断才用） |
| 工程编排用户 | GUI选择Capability/TestCase、配置输入、运行、查看证据 | 不要求写C# |
| 调试人员 | GUI断言/Trace/Timeline/源码跳转/重跑 | 可读取Run/Attempt，但无须创建Runner |
| CI/平台维护者 | `TCTTestPlan`、`TCTTestRunRequest`、`TCTTestRunSummary` | 覆盖率门槛、Suite/安全策略 |
| 设备/安全负责人 | 真实Target/Resource/Safety审批与只读证据 | 没有授权默认不可HIL |

## 2. 单一测试真源：编译、发现、Worker与重跑

### 2.1 两项目与目录最终定案

```text
Foundation/Infrastructure/CxVisual.Foundation.Infrastructure  # 真实生产通用基础设施
  ├─ FLD Logging
  ├─ FCR Concurrency
  ├─ FCR Monitoring
  └─ 仅通过生产复用验证才考虑SystemClock/ProcessSupervisor

Tests/TestCenter/CxVisual.TestCenter  # 单个WPF NET48 x64 EXE，TCT/ITCT前缀
  ├─ DeveloperAPI/            TCTTestAttribute/TCTCaseSourceAttribute等
  ├─ Core/                    Descriptor/Context/Catalog/断言
  ├─ Discovery/               编译式测试发现、Catalog指纹
  ├─ Plans/                   Suite/Plan/Policy（维护者）
  ├─ Execution/               Coordinator/Worker/Resources/Safety
  ├─ Fixtures/                DataSet/Golden/Artifact/Sandbox
  ├─ Diagnostics/             Observer/Envelope/Evidence
  ├─ Coverage/                已审规格ID映射与维度规则
  ├─ Reports/                 JUnit/JSON/Text/History/Diff
  ├─ Visualization/           Geometry/Object/Graph/Channel/Runtime DTO
  ├─ UI/                      单Shell/Viewer/Timeline/Inspector
  └─ Cases/                   真实Owner黑盒用例（支持子目录）
```

这是**逻辑分包/组织结构**，不是另拆十几个 .csproj。`Mechanism.Data`的真实MDE作用域、`Mechanism.Runtime`的MRK执行器仍归自己；Production不得编译 TCT代码。

### 2.2 传统 csproj 的实际变更原则

在原有 `.csproj` 中仅对 `Cases` 使用 `Compile Include="Cases\**\*.cs"`（MSBuild项目实际写为普通单反斜杠），**并移除所有逐个列出的 Cases Compile 项**；其余 WPF XAML、App.xaml、Resources的构建项维持显式包含。CI检查编译输入不重复、移动/删除Case无残留、生产项目没有误编译Owner目录的测试链接文件。

若把测试源码放Owner旁边：明确该文件**仅被TestCenter Link编译**，不能由Owner csproj编译、不能拥有第二份物理拷贝；先验证存量项目的链接路径与PDB，再开放该模式。

### 2.3 发现与定义指纹

```text
source (.cs) ──MSBuild──> TestCenter.exe [compile-time tests]
                ↓
TCTTestAttribute + reflection -> TCTTestCaseDescriptor -> TCTTestCatalogSnapshot
                ↓                           ↓
Default run or selected --id           SnapshotFingerprint
                ↓                           ↓
TCTTestRunRequest (CaseId/Version/Seed/FixtureHash) ──> Worker launch
                ↓
Worker同exe/同程序集MVID加载Catalog ──>校验 DefinitionFingerprint/RowHash
                ↓
ITCTTestCase (owner API) -> assertions/observations -> immutable result
                ↓
RunSummary / JUnit/History / UI Explorer-Inspector-Viewer
```

- **TestDefinitionId/TestId**：方法定义的全局稳定ID，不用源码行号作主键。
- **CaseId**：无参数等于TestId；有参数则`TestId + "#" + StableRowKey`，RowKey须经过规范化转义，禁止同文件重名。
- **DefinitionFingerprint**：TestId、方法签名、程序集MVID、可选数据源Version/Hash、目标Schema版本共同确定（快照）；**用于身份/版本相等验证，不把Hash当防恶意签名**。
- **RunId**：一次提交运行的GUID；**AttemptId**：每次执行/重试的独立GUID；**Iteration**为重复轮次；**RetryIndex**为某行的补偿重试序号。所有证据携带RunId+AttemptId+CaseId。
- **Worker**：主进程和子进程按同一CatalogSnapshot和参数行Hash重建，不接受仅父进程存在的Lambda闭包。Hello/Handshake必须报告可执行程序集哈希/MVID、协议版、CaseId与RowHash；不一致为Error/Blocked而非Skipped。
- **重跑**：保留原数据/Seed/执行配置和DefinitionFingerprint（如字节代码不在本机，则报不可重现）；默认不重复外部不可逆操作；重跑属于新RunId与AttemptId，引用原始AttemptId。
- **SourceLocation**：从PDB/可用调试符号和异常StackTrace提取；缺符号时显示`SourceUnavailable`，**绝不猜测文件行号**。
- **安全**：命令行参数、临时文件和进程间消息不含真实密钥；任何资料输入进入工厂前校验路径、格式、大小和哈希；默认ChildProcess。

### 2.4 选中运行与运行档位冲突的规范

1. `--id ExactCaseId` 或 GUI选中运行：**先精确解析存在性，再应用环境/权限/危险度预检**，不受默认Smoke对Kind的筛选；显式`--profile Benchmark`等仍可决定额外门槛（是否拦截需要明确报错），不得悄悄变成0 Case。
2. `--profile Smoke/Regression/Stress/Benchmark/CI` 的批量运行：正常按Profile Policy筛选，生成`Selected / Filtered / Blocked / Missing`四组解释，不把Filtered算Passed。
3. 无匹配、未知CaseId、被拒授权或不具备必要SDK：保留机器可读原因；无测试真正执行时 CLI 必须非0退出，不能产生绿色Build。
4. TargetMode默认为Virtual/Replay；真实硬件请求需显式`--target Real` +有授权的安全预检；不因设置 `--inproc` 自动放行。

### 2.5 执行状态与生命周期（唯一流程）

```text
CatalogSnapshot -> ResolvePlanOrIds -> Preflight -> ReserveFixture/Resource
-> SpawnWorker -> ValidateHandshake -> Setup -> Execute/AsyncAwait
-> Assert/Observe/Artifact -> Teardown(always attempted) -> DrainObservation
-> VerifyExit/Leaks/AttemptCompleteness -> Persist -> CommitImmutableResult
```

- Setup失败仍尝试Teardown；清理失败不能Passed（Error优先于此前断言通过）。
- `Cancelled`仅表示该Attempt已明确取消，`TimedOut`表示实际超时且子进程回收结果记录完毕；若Kill无法确认则`Error/InfrastructureLeak`，不得假称安全退出。
- `Skipped`仅能表示前置依赖失效/FailFast/未运行；`Unsupported`是缺生产能力/SDK；`Blocked`是安全/权限预检拒绝（建议独立诊断或新增状态）；不允许将这几种情况合计为Passed。
- 一个Attempt仅有一个终态；历史重试的每一Attempt必须保留，不覆盖旧结果；重跑失败允许引入新的Run而不修改既有Run数据。
- 进程隔离不保证实体硬件停止，设备测试额外要求外部Interlock、人工确认和预设白名单。

## 3. 对外最小契约（建议冻结接口形态）

以下为**目标公开 API 的签名草案**（不是完整可编译的类实现；构造函数和方法省略了实现体），C#7.3/NET48语义为目标。新成员尚未在master声明，不得视为现有实现：

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TCTTestAttribute : Attribute
{
    public TCTTestAttribute(string testId);
    public TCTTestAttribute(string testId, string displayName,
        TCTTestAspect aspect, string group); // 兼容现有形式
    public string Name { get; set; }
    public string OwnerProject { get; set; }
    public TCTTestKind Kind { get; set; }
    public string CapabilityId { get; set; }
    public string ObjectId { get; set; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TCTCaseSourceAttribute : Attribute
{
    public TCTCaseSourceAttribute(string source);
    public string Version { get; set; }
    public string RowKeyField { get; set; }
}

// 目标：统一每次Attempt的断言/观察，旧重载保留。
public sealed class TCTTestContext
{
    public CancellationToken CancellationToken { get; }
    public string CaseId { get; }
    public string RunId { get; }
    public string AttemptId { get; }
    public int Seed { get; }
    public void AssertTrue(bool condition, string message);
    public void AssertFalse(bool condition, string message);
    public void AssertEqual<T>(T expected, T actual, string message);
    public void AssertNear(double expected, double actual, double tolerance, string message);
    public void Observe(string kind, string title, string description);
    public TCTArtifactRef SaveArtifact(byte[] data, string mime);
}

// 这里只展示维护者边界；实现仍汇入原有Coordinator。
public sealed class TCTTestPlan
{
    public string PlanId { get; }
    public string Version { get; }
    public IReadOnlyList<TCTTestPlanStep> Steps { get; }
    public TCTTestRunRequest PrepareRun(TCTTestRunProfile profile);
}
```

**兼容原则**：现有`ITCTTestCase`、`ITCTAsyncTestCase`、`TCTTestRunner`、`TCTTestExecutionCoordinator`、`TCTWireCodec`维持各自唯一Owner。新Attribute适配到已有Case接口；不用再增一个Runner或强制所有开发者实现生命周期接口。C#7.3目标签名中`IReadOnlyList`可用，但传统NET48所需引用/构造应在实施时验证。

### 3.1 最小公开 API — 以调用角色划分

| 角色 | 必须可见的API | 应默认internal的实现 |
|---|---|---|
| 普通Case作者 | `TCTTestAttribute`、`TCTTestContext`、便捷断言 | Catalog/Runner/Worker/WireCodec/Reporter |
| 数据Case作者 | 前二者+`TCTCaseSourceAttribute`；可选Fixture轻量入口 | 数据Provider/反序列化器/RowHash解析 |
| 框架扩展作者 | `ITCTTestProbe`、`ITCTTestViewer`（受Schema版本约束） | Hub/Registry/WorkerTransport策略 |
| 平台宿主/CI | `TCTTestRunRequest`、`TCTTestRunSummary`、`TCTTestPlan` | Scheduler/ExecutionCoordinator内部组合 |
| GUI交互者 | 命令/导航/Viewer（不必学C#） | ViewModel/Ribbon/Selection同步逻辑 |

### 3.2 测试目录自适应和自动发现的错误合同

- 无Attribute：文件可以正常编译但**不进入测试目录**；是否提示未标记方法可由开发工具诊断，不把所有静态方法自动变成测试。
- 重复TestId、签名错误、参数数据RowKey重复/未知Schema：发现失败、非零退出或明确DiscoveryError；不能悄悄跳过。
- 依赖DLL缺失/SDK缺失：能发现但预检`Unsupported/Blocked`；若缺的是测试宿主必须的程序集导致整体无法加载则为HostError。
- 数据源失效：单行缺失不等同Skipped；根据Manifest标志阻断全部依赖行并显示MissingFixture，不能报告Pass。
- Attribute测试方法抛异常：必须解包真实`InnerException`，保留SourceLocation和StackTrace；不报伪的反射`TargetInvocationException`为根因。

## 4. 参数化、Fixture、Plan与覆盖率的完整数据模型

### 4.1 Case / Attempt 身份与参数数据（P1，作为冻结合同）

| 对象 | 最小不可变字段 | 责任边界 |
|---|---|---|
| TCTTestCaseDescriptor | TestId、Name、Kind、Owner、Group、CapabilityId、ObjectId、SourceLocation、Requirements、DefinitionFingerprint | 定义一次、多个Run可引用，不存结果 |
| TCTCaseRow（新增） | RowKey、DataHash、FixtureVersion、TypedRow | 参数行唯一性及数据校验；不写结果 |
| TCTTestAttempt | CaseId、RunId、AttemptId、Iteration、RetryIndex、Seed、DefinitionFingerprint/FixtureHash、ParentAttemptId | 一次真实执行，只允许一次终态 |
| TCTTestCaseResult | AttemptId、Status、Assertions、Observations、Diagnostics、Artifacts、HostExit/Leak | 结果不可变；保留原始Exception证据 |
| TCTTestRunSummary | RunId、PlanId(可空)、Environment、每个Selected Case完整Attempt/Result、Filter/Blocked统计 | 汇总不等于覆盖率 |

行身份不使用 `rowIndex`；行次序可变、RowKey不可重复。数据源修改后Case定义/参数Hash随之变化，旧Run历史仍保留旧Hash用于可比性判断。

### 4.2 计划/依赖/资源

`Suite = 分类与复用引用`；`Plan = 可保存/审核/版本化执行定义`；`RunRequest = 运行时实际选中集合与执行限制`；`RunSession = 一次运行状态`。四者**不混为一个类**。

| 阶段 | 必须验证 |
|---|---|
| Plan Load | 版本/依赖/引用ID/循环检查、Owner/Case是否仍存在 |
| Preflight | 资源白名单、SDK/OS/GPU能力、设备安全授权、Fixture存在且未篡改 |
| Schedule | 前置依赖Passed后启动，互斥资源排序，FailFast不吞未执行用例 |
| Execution | 独立Worker、Async、Cancellation、Timeout、Setup/Teardown、Retry明确缘由 |
| Finalize | 确保未启动Case为Skipped/Blocked及理由，Attempt完整性和证据大小检查 |

**安全门槛**：真实设备用例除Owner公开的功能检查外，TestCenter必须有额外TargetMode/授权/互锁/紧急停机确认合同；这些属于TestCenter预检接口，但Interlock物理事实由设备安全Owner负责。

### 4.3 Evidence / Coverage

```text
OfficialSpecVersion -> Workbench -> ProfessionalArea -> CapabilityArea
 -> CapabilityId -> ObjectId -> RequirementDimension
 -> (TestId, CaseId, AttemptId, AssertionId, ArtifactRef)
```

八维建议保持：合同、正常、边界、异常、并发、生命周期、版本、性能。每维定义`Required/Optional/NotApplicable+理由`；**只有要求的维度全部有已核验并成功的实际证据且环境/定义版本匹配，才可标为Verified**；否则是Unmapped/Partial/Blocked/Failed/NotRun，不汇总为“完整验收”。专业方面仅用来组织上下文，不能替代能力对象与合同映射。

这里的`CapabilityArea`、`CapabilityId`、`ObjectId`必须来自**经过审核的正式规格表**；未核准时一律Unmapped，禁止以示例中的字符串充当正式ID。

### 4.4 真实Observation到六图的只读桥梁

`TCTObservationEnvelope { RunId, AttemptId, CaseId, SchemaId, SchemaVersion, SourceOwner, SourceObjectId, CorrelationId, TimestampUtc, Sequence, PayloadFormat, ArtifactRef }`为统一封套。小数据可内联；大图像/CT体积/二进制帧通过SHA256 ArtifactRef。Recorder可限制最大字节/条数、背压失败需单独诊断，UI不自行补造生产状态。

| 图 | Viewer输入只读Schema | 使用规范 |
|---|---|---|
| 01 Dashboard | RunSummary/Plan/History/Coverage | 真实已选/已运行/未接入按状态区分 |
| 02 Geometry | Geometry/Expected/Actual/Transform/Delta | 坐标一致、缩放、标尺、不同容差显示 |
| 03 Object | Owner对象树与Reference边 | Ownership≠Reference≠Connection；Symbol不再引入废弃的SymbolDefinition |
| 04 Graph | 生产编排对象与MRK编译/运行诊断 | Definition→Compile→Plan→ExecutionInstance有独立映射，Viewer不创造Graph Owner |
| 05 Channel | 真正TX/RX原始帧/Codec/Session/错误事件 | 无真实Connectivity Adapter只能NoEvidence，不得做Modbus演示假成功 |
| 06 Runtime | ExecutionInstance/Journal/Trace/Lease/WaitingReason | 真实Timeline可回放；Unknown外部操作不推断为Failed/Confirmed |

## 5. 250项候选清单逐项裁决规则

**检查规则**：每个V3.0条目保留原名/原分类/原状态作为审计锚点，增加`V3.2裁决`与`归并去向/理由`；标成“合并”**不表示删除现有代码**，而是阻止新增重复公开类。需要已存在的名字仍保留兼容，P0表示行为必须通过，不表示必须新建同名源文件。

| 裁决 | 数量 | 解释 |
|---|---:|---|
| 保留现有 | 61 | 已有实现继续复用，必要时改进内部 |
| 保留扩展 | 37 | 保留原API，增加证据/易用性合同 |
| 必要新增（按批次） | 91 | 达到行为门槛再实体化，不在P0全生成 |
| 合并/内聚 | 45 | 避免Facade/Runner/状态真源重复 |
| 暂缓独立类型 | 9 | 非默认开发者最短路径，须证明必要性 |
| 条件保留/暂缓 | 1 | 真实第二应用场景出现再批准 |
| 条件暂缓 | 6 | Infrastructure下沉必须跨生产复用 |
| **总数** | **250** | 与V3.0逐行一一对应 |

原V3.0数量更正：250总条目=61现有+38现有待扩展+6条件候选+**145拟新增**。原摘要写144应以145为准；V3.2此表的“合并/暂缓”等是新的**设计裁决**，不可直接与现有源码数量相减当作“已实现”。

## 6. 工作包与验收门槛（先证明简单再做大型UI）

| 阶段 | 不可缺失的成果 | 自动化验收 | 人工验收 |
|---|---|---|---|
| S1（P0入口） | 单参Attribute、二参断言、`void/Task`、Cases递归编译、精确单选、缺失ID错误 | D01-D03、D05、D10、D12；不同Kind用例明确单选可执行 | 开发者一页指南新增Case、看到断言/源码位置 |
| S2（P0身份） | 定义快照/MVID、Worker握手、CaseId/AttemptId、回放参数与失败重跑 | D06、失败/崩溃/取消、零用例/未知ID反证 | 单条失败Attempt可直接重跑且结果可追溯 |
| S3（P1数据） | 数据源RowKey、Fixture/Golden受控审签、Plan/Suite/依赖/安全预检 | D04、D07、D11、100行数据跨进程校验/设备默认拒绝 | 不写C#也能从界面选择、传参、比较 |
| S4（P1证据） | Observation Envelope、Artifact、ViewerRegistry、Coverage正式映射与历史Diff | D08、D09、Event Seq/Hash/容量/未知Schema、Run比对 | Geometry/Object/Runtime真数据可视化，未知Provider有解释 |
| S5（P1/P2） | Graph/Channel真Provider接入、Benchmark/Stress/长任务、WPF自动化 | 复杂并发/回收/指标可比性与回归 | 六图目标完成动态Ribbon、Timeline、Diff、交互与导出 |

所有阶段必须通过：**Windows NET48/x64实际编译、JUnit/JSON结果一致、0用例非零退出、Attempt统计完整、Resource/Process无遗留、Code/Fixture/Environment证据可查**。首批不能把“已有62个真实测试”当作S1通过：旧测试用例验证了Runner/Owner，未验证从零新增Case的完整开发者旅程。

### 6.1 开发者D01-D12收口矩阵（作为独立审查门）

| ID | 目标 | 必须提供的可核证证据 |
|---|---|---|
| D01 | 新增一个同步方法 | 只增源文件，编译自动包含、GUI/CLI/Worker对同一TestId可发现 |
| D02 | 异步Task自然运行 | 真实await、不阻塞Dispatcher、Task取消与原始异常正确映射 |
| D03 | 精确运行被选中Case | 非Unit类型也运行，或明确被安全预检拒绝，绝不静默零Case |
| D04 | 参数100行 | 100个CaseId/AttemptResult，RowKey稳定、单行重跑一致 |
| D05 | 失败可定位 | 故意断言错值、Expected/Actual/Stack/源码若PDB可用能定位，CLI非零 |
| D06 | Worker定义一致 | MVID/DefinitionFingerprint/RowHash匹配，数据或代码被替换时拒绝 |
| D07 | 生命周期与资源 | Setup/Teardown总会按规执行，错误与资源释放证据，不能假绿 |
| D08 | 结构化观察 | 实际Owner→Envelope→Artifact→Viewer；未知Schema安全降级，不能改PASS |
| D09 | 能力正式覆盖 | 未审核ID=Unmapped，合同8维缺项=Partial且可定位哪些缺 |
| D10 | CLI与CI | 指定ID/批量Profile一致，0测试/不支持/超时均按规则退出非0 |
| D11 | 真实设备安全 | 未授权真实Motion/X-Ray测试被阻断，不以Kill进程视为安全保障 |
| D12 | 新增、移动、删除源码 | 无残留注册、重ID显式失败，csproj目录规则可重复验证 |

### 6.2 六种用户角色验收（不重造六套界面）

| 用户角色 | 10分钟内应该能完成的任务 | 失败时必须看见 |
|---|---|---|
| 普通开发者 | Attribute标记、断言、一次运行、点击源码 | Expected/Actual/源码符号/PDB不可用原因 |
| 能力开发者 | 给真实Owner对象配置样例数据，看到真实差异 | Owner对象Id、版本、Artifact来源 |
| 工程编排用户 | GUI建简单Plan，拖入多个Case、设输入后执行 | 依赖未满足/资源排队/未支持Provider |
| 调试人员 | 同一Attempt打开Graph/Runtime Timeline，追踪Correlation | Unknown/Retry/Recovery与原始Journal/Trace |
| 平台测试维护者 | 选CI Profile、验正式8维覆盖、生成历史对比 | Unmapped、MissingEvidence、环境不可比原因 |
| 操作/安全负责人 | 只读监视、审查真实设备授权状态、导出结果 | Blocked安全拒绝，不提供无权限运行入口 |

## 7. 依赖与类型可见性具体规则

- Production Foundation/Mechanism/Professional不得编译或引用任何`TCT*`类型；只由 `Tests/TestCenter` **向下**引用生产Owner DLL；底层工程按需引用 `Foundation.Infrastructure`。
- 保持当前 `FLD`日志、`FCR`并发/监控、`FCM`配置、`MDE`作用域、`MRK`运行/编译等生产前缀及既有代码；测试数据管理 `TCTFixture...`不迁成Production通用容器。
- 测试专有公开入口最少：**普通作者2个** (`TCTTestAttribute`、`TCTTestContext`)；只有数据来源时增加`TCTCaseSourceAttribute`。Catalog/Result/Plan模型由Host/CI/GUI可见，Runner、Worker协议与UI状态管理默认内部化；兼容现有公开类可先保留`public`，不立刻破坏源码引用。
- 能独立完成工作的纯函数优先 `static class`；有状态、租约、资源/生命周期的Runner/Repository/Viewer/Executor保持实例对象；不为追求静态化引入全局可变单例。
- 不在TestCenter建立第二套GraphDefinition、DataHeap、DataStack、Channel或Runtime；需要生产能力时向对应Owner提交独立实现任务，TestCenter只建设诊断/观察适配和验收Case。
- 外部NUnit/MSTest集成属适配合同，不宣称已有官方适配。自定义Attribute不等于Visual Studio标准Test Explorer必然识别。

## 8. 冻结后不得含糊的工程问题（明确选择）

1. **单Project约束与源文件自动编译**：TestCenter项目内自动包含`Cases/**/*.cs`，Owner旁源码仅受控Link，不能自动从所有生产路径递归Include。
2. **属性名与命名空间**：所有新测试类型TCT/ITCT前缀，方法名与用例组名由反射可重复确定，属性缺失时Unknown/Unmapped，不偷偷填虚构能力ID。
3. **Worker**：单一程序集入口和单一Catalog快照真源，协议里必须有版本、定义Hash、FixtureHash、AttemptId。只在内存注册的委托不作为默认Worker Case。
4. **策略优先级**：显式ID选择高于默认Smoke过滤，但低于安全授权/SDK条件/资源独占；批量选择按Profile。
5. **失败闭环**：用户点击具体失败Assertion→选中具体Attempt→显示Evidence/SourceLocation→重跑“当前数据版”或“原始数据版”（不同则须确认），不能使用当前版本覆盖旧Run记录。
6. **条件功能**：无正式数据契约时不造假的Viewer/Benchmark；缺数据清楚显示Unsupported/NoEvidence/VersionMismatch。
7. **审批边界**：Golden审批与实际硬件授权都不是“确认”按钮的UI装饰；必须留审计、操作者与版本证据；对真实不可逆操作要求设备Owner的物理安全策略。

## 9. 当前GitHub实现差距：事实 / 设计 / 验收分开

截至本次核对的master `ff24085207119fc3717522fd1d6ca8403e0c4c7f`，关键源码显示：

| 能力 | 当前已核对事实 | 本版新增目标 |
|---|---|---|
| Attribute | `TCTTestAttribute(string id,string name,TCTTestAspect aspect,string group)` | 增加单参、Owner/显示名默认；保留四参 |
| Attribute发现 | `TCTAttributeFrameworkAdapter`限制`void(TCTTestContext)` | 支持`Task(TCTTestContext)`与参数数据版 |
| 代码编译 | 传统csproj明确列出`Cases\\TCT*.cs` | 递归受限Cases自动编译并检查重复 |
| CLI/Worker | `--run/--worker`用同EXE，Worker按TestId查询 | 完整DefinitionFingerprint/RowHash强校验 |
| 请求Profile | `TCTTestRunRequest`默认Smoke且Select执行Profile过滤 | 明确ID单选优先，但危险设备预检不被绕过 |
| 断言 | `AssertTrue`现有四参、`AssertEqual`等 | 简化二参、SourceLocation/AssertionId |
| 结果证据 | Run/Attempt、JUnit/JSON、部分Artifact/Diagnostics现有 | 参数行CaseId、源位置、Schema化观测与8维覆盖链 |
| 界面 | WPF统一Shell与几种Viewer基础呈现已存在 | 命令、Inspector、真实结构化Viewport及Source导航 |
| 能力覆盖 | 已有CoverageService和Unmapped语义 | 正式规格版×对象×合同维度×AssertionEvidence审签 |

这些“目标”都尚需代码与CI证明；**本次文档定案不应被误称为代码已实现**。

## 10. Codex实施指令（简明但可执行）

```text
以本文件V3.2为TestCenter新的设计基线；先核 master HEAD、README、AGENTS、协同入口、
测试源码和Owner接口。禁止新增测试csproj，保持TCT/ITCT前缀与Production->Tests零依赖。
第一批只落S1：TCTTestAttribute单参数兼容、TCTTestContext二参数断言、
反射支持static Task与异常解包、Cases受限自动Include、显式ID选择确定性、
测试源定位及D01-D03/D05/D10/D12回归。不得把250项机械创建为空类。
S1全绿后实现S2定义指纹+Worker握手、S3参数+Fixture/Plan、S4证据+Coverage/Viewer，
按CI和真实生产Owner成熟度推进S5。每阶段必须Windows NET48 Debug/x64编译、
隔离/并发/空Case/冲突ID测试，提交JUnit/JSON与可复现证据。遇未实现Owner
只给Unsupported/CapabilityGap记录，不造fake业务对象或假成功UI。
```

---

# 附录A：V3.0 250项类/接口/枚举 → V3.2 逐一裁决（可追溯）

**表中“V3.0原状态”不等于本轮重新逐类编译核验的结论。** 完整保留250项原型名和对应职责，决策主要针对是否成为**新增独立类型**，不等于立刻修改或删除已有源码。


### I. Foundation.Infrastructure

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 1 | `IFLDLogSink` | 接口 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 2 | `FLDLogService` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 3 | `FLDDedicatedLogger` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 4 | `FLDLogRecord` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 5 | `FLDLogLevel` | 枚举 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 6 | `FLDLOG_TYPE` | 枚举/类型 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 7 | `FLDLogScope` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 8 | `FLDLogHelper` | 静态类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 9 | `FLDEventLogHelper` | 类/静态类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 10 | `FLDImportantEventLog` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 11 | `FLDImportantEventLogEntry` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 12 | `FLDMemoryLogSink` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 13 | `FLDFileLogSink` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 14 | `FLDRollingFileLogSink` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 15 | `FCRWorker` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 16 | `FCRWorkerOptions` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 17 | `FCRWorkHandle<T>` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 18 | `FCRWorkResult<T>` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 19 | `FCRWaitObservation<T>` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 20 | `FCRWorkState` | 枚举 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 21 | `FCROverflowPolicy` | 枚举 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 22 | `FCRWorkerSnapshot` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 23 | `FCRAsyncGate` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 24 | `FCRPeriodicJob` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 25 | `FCRSharedLease<T>` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 26 | `FCRDedicatedExecutor` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 27 | `FCRSystemMonitor` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 28 | `FCRSystemSnapshot` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |
| 29 | `FCRProcessSnapshot` | 类 | 现有 | **保留现有** | Foundation.Infrastructure | 现有 | 沿用生产Owner；不新增TCT依赖 |

### II. Foundation.Infrastructure

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 30 | `IFCRClock` | 接口 | 候选 | **条件暂缓** | Foundation.Infrastructure | 条件P2 | 有两个独立生产需求且边界核实后才进Infrastructure |
| 31 | `FCRSystemClock` | 类 | 候选 | **条件暂缓** | Foundation.Infrastructure | 条件P2 | 有两个独立生产需求且边界核实后才进Infrastructure |
| 32 | `IFCRProcessSupervisor` | 接口 | 候选 | **条件暂缓** | Foundation.Infrastructure | 条件P2 | 有两个独立生产需求且边界核实后才进Infrastructure |
| 33 | `FCRProcessSupervisor` | 类 | 候选 | **条件暂缓** | Foundation.Infrastructure | 条件P2 | 有两个独立生产需求且边界核实后才进Infrastructure |
| 34 | `FCRProcessStartOptions` | 类 | 候选 | **条件暂缓** | Foundation.Infrastructure | 条件P2 | 有两个独立生产需求且边界核实后才进Infrastructure |
| 35 | `FCRProcessExitSnapshot` | 类 | 候选 | **条件暂缓** | Foundation.Infrastructure | 条件P2 | 有两个独立生产需求且边界核实后才进Infrastructure |

### III. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 36 | `TCTTestAttribute` | 特性类 | 现有 | **保留现有** | CxVisual.TestCenter | P0 | 维持当前唯一实现与Owner |
| 37 | `TCTTest` | 静态类 | 新增 | **暂缓独立类型** | CxVisual.TestCenter | P2 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 38 | `TCTTestCaseBuilder` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestAttribute + TCTTestContext（不增加Builder） |
| 39 | `TCTTestCaseBuilder<TResult>` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestAttribute + TCTTestContext（无额外Result真源） |
| 40 | `TCTParameterizedCaseBuilder` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTCaseSourceAttribute + TCTCaseDataSet（仅参数合同） |
| 41 | `TCTScenarioBuilder` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestContext + 普通C# async/using + ITCTTestLifecycle |
| 42 | `TCTTestPlanBuilder` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestPlan加载/保存（Builder非必要） |
| 43 | `TCTTestContext` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | P0 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 44 | `TCTRegistration` | 静态类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 45 | `TCTTestCaseDescriptor` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | P0 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 46 | `TCTTestCaseParameterSet` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | P1 | TCTCaseDataSet / TCTCaseRow（建议正式命名，统一StableRowKey） |
| 47 | `TCTTestCaseSource` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P0 | 类/方法/程序集/提交/源码位置可追溯 |
| 48 | `TCTTestRequirement` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 所需OS/SDK/GPU/设备、安全等级/实现可用性 |

### IV. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 49 | `ITCTTestCase` | 接口 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 50 | `ITCTAsyncTestCase` | 接口 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 51 | `ITCTTestLifecycle` | 接口 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 52 | `ITCTTestDiscoverer` | 接口 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 53 | `ITCTTestFrameworkAdapter` | 接口 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 54 | `ITCTTestPlanSource` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 代码/JSON/工程资源的测试计划来源 |
| 55 | `ITCTTestRequirementEvaluator` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 解析依赖是否满足；不自动访问生产设备 |
| 56 | `TCTTestCatalog` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | P0 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 57 | `TCTReflectionTestDiscoverer` | 类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 58 | `TCTTestDiscoveryService` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 59 | `TCTAttributeFrameworkAdapter` | 类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 60 | `TCTAttributeTestCase` | 类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 61 | `TCTAsyncTestCaseAdapter` | 类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 62 | `TCTTestCatalogSnapshot` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 目录版本一致性、不可变快照 |
| 63 | `TCTTestQuery` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P0 | 按Owner/能力/对象/Kind/Tag/状态检索 |
| 64 | `TCTTestFilter` | 静态类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestQuery（单一查询语义） |
| 65 | `TCTTestOwnerMapping` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestCaseDescriptor.Owner（正式能力映射由Coverage Catalog处理） |
| 66 | `TCTTestAvailability` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestRequirementEvaluator结果（Availability只读，不独立服务） |

### V. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 67 | `TCTTestSuite` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 复用的测试分组/嵌套Suite，允许跨Owner |
| 68 | `TCTTestPlan` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 一次可保存的测试计划定义，不等于Run实例 |
| 69 | `TCTTestPlanStep` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Case/Suite引用、参数、依赖与执行门槛 |
| 70 | `TCTTestDependency` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 前置成功、资源就绪、Fixture依赖 |
| 71 | `TCTTestSelection` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestRunRequest.SelectedCases（不可变选择快照） |
| 72 | `TCTTestRunRequest` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | P0 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 73 | `TCTTestExecutionPolicy` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P0 | 隔离/失败门槛/超时/资源/Retry边界的归一化策略 |
| 74 | `TCTTestRetryPolicy` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 明确自动重试次数、退避、是否只重试宿主错误；保留所有Attempt |
| 75 | `TCTTestSchedule` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestExecutionPolicy + Coordinator（实际需要再分离） |
| 76 | `TCTTestResourceRequirement` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestCaseDescriptor.Requirements/ExclusiveResourceKeys（现有资源门） |
| 77 | `TCTTestSafetyPolicy` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P0 | Simulation/Real端点白名单、操作前授权和紧急条件 |
| 78 | `TCTTestRunProfile` | 枚举 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 79 | `TCTTestKind` | 枚举 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 80 | `TCTTestIsolation` | 枚举 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 81 | `TCTTestPlanStepKind` | 枚举 | 新增 | **条件保留/暂缓** | CxVisual.TestCenter | P2 | TestCase/Suite/Setup/Teardown/Checkpoint；需求驱动再实体化，不为凑类型数量 |
| 82 | `TCTTestDependencyPolicy` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | RequiresPassed/Always/OnFailure |
| 83 | `TCTTestRetryReason` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | InfrastructureOnly/FlakyDiagnostic/ExplicitNever |
| 84 | `TCTTestTargetMode` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P0 | Real/Virtual/Replay/Offline；明确安全语义 |
| 85 | `TCTTestSafetyLevel` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P0 | Safe/ExternalIO/HIL/Hazardous |

### VI. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 86 | `TCTTestRunSession` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 87 | `TCTTestAttempt` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 88 | `TCTTestExecutionCoordinator` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 89 | `TCTTestRunner` | 静态/类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 90 | `TCTTestLifecycleExecutor` | 静态类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 91 | `TCTTestHostController` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 92 | `ITCTTestIsolationHost` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 进程隔离宿主抽象；仅供TestCenter复用 |
| 93 | `ITCTTestWorkerTransport` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Worker事件/结果通道，支持逐条流式传输 |
| 94 | `TCTWorkerProtocolEnvelope` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | IPC版本、RunId/AttemptId/Seq/消息体哈希 |
| 95 | `TCTWorkerHandshake` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 协议、构建、位数及能力握手 |
| 96 | `TCTWorkerHeartbeat` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Worker活性心跳、最后确认序号 |
| 97 | `TCTWorkerSession` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 每Worker生命周期、反馈、取消与连接管理 |
| 98 | `TCTWireCodec` | 静态类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 99 | `TCTResourceGate` | 类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 100 | `TCTTestCompletionValidator` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestRunSummary/Coordinator完成校验（内聚同模块） |
| 101 | `TCTTestRunSummary` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 102 | `TCTTestRunState` | 枚举 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 103 | `TCTTestStatus` | 枚举 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 104 | `TCTTestAttemptState` | 枚举 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestStatus + Attempt生命周期事件（暂不重复状态机） |
| 105 | `TCTWorkerMessageKind` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Hello/Started/Assertion/Observation/Diagnostic/Heartbeat/Completed/Exit |
| 106 | `TCTTestExitCode` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Success=0、TestFailure=1、HostError=2、InvalidArgument=3 |

### VII. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 107 | `TCTAssertionResult` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 108 | `TCTAssert` | 静态类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestContext.Assert*（单一断言Collector） |
| 109 | `TCTAssertionException` | 类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 110 | `ITCTTestComparer` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 可插拔图像/树/集合等比较器 |
| 111 | `TCTNumericComparer` | 静态类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 112 | `TCTFormat` | 静态类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 113 | `TCTAssertionPolicy` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTComparisonOptions（共享比较选项；不构造第二断言库） |
| 114 | `TCTNumericTolerance` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 单位、绝对/相对误差、特殊值策略 |
| 115 | `TCTStructureComparer` | 静态类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 集合/Map/树/Reference/对象结构语义比较 |
| 116 | `TCTGeometryComparer` | 静态类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 几何Expected/Actual的独立比较与误差表达 |
| 117 | `TCTBufferComparer` | 静态类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTSnapshotDiffService.CompareBytes（已有行为复用） |
| 118 | `TCTImageComparer` | 类 | 新增 | **暂缓独立类型** | CxVisual.TestCenter | P2 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 119 | `TCTVolumeComparer` | 类 | 新增 | **暂缓独立类型** | CxVisual.TestCenter | P2 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 120 | `TCTTemporalAssert` | 静态类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Eventually/NoEventWithin/StateReached；不隐藏deadline |
| 121 | `TCTAssertionKind` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Boolean/Numeric/Structure/Exception/Eventual/Performance/Artifact |
| 122 | `TCTComparisonKind` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Exact/Tolerance/Semantic/Hash/Visual |

### VIII. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 123 | `ITCTTestFixtureProvider` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Fixture按ID/版本装载，不污染被测Owner |
| 124 | `ITCTTestDataGenerator` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 确定性随机/边界/参数组合数据生成 |
| 125 | `TCTTestFixtureService` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 126 | `TCTTestDataSet` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Fixture版本、输入集合和Artifact关联 |
| 127 | `TCTFixtureManifest` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 数据来源、Schema、Hash、依赖与许可证 |
| 128 | `TCTFixtureReference` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 对Fixture版本/数据ID的稳定引用 |
| 129 | `TCTFixtureResolver` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 内置/磁盘/只读缓存多来源解析 |
| 130 | `TCTDataGenerationSpec` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Seed、分布、长度/边界、生成器版本 |
| 131 | `TCTPropertyCaseGenerator` | 类 | 新增 | **暂缓独立类型** | CxVisual.TestCenter | P2 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 132 | `TCTTestSandbox` | 类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 133 | `TCTTestArtifactStore` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 134 | `TCTArtifactRef` | 类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 135 | `TCTArtifactRetentionPolicy` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 保留失败证据，按Run/大小/天数清理 |
| 136 | `TCTBaselineService` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 137 | `TCTBaselineManifest` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Golden版本、兼容环境、Hash和审计 |
| 138 | `TCTBaselineUpdateProposal` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 变更提议/理由、审查差异 |
| 139 | `TCTBaselineApproval` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 审核人、时间、签名/批准记录 |
| 140 | `TCTBaselineStatus` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Proposed/Reviewed/Approved/Rejected/Published |
| 141 | `TCTFixtureSourceKind` | 枚举 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTFixtureReference.UriScheme（先字段） |
| 142 | `TCTTestDataOwnership` | 枚举 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTFixtureReference.Scope（先字段） |

### IX. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 143 | `ITCTTestObserver` | 接口 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 144 | `ITCTTestProbe` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 读生产诊断/快照的桥接，不增加生产对TCT依赖 |
| 145 | `ITCTTestResultSink` | 接口 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 146 | `ITCTObservationSerializer` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 版本化观察Payload编码/解码 |
| 147 | `TCTTestObservation` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 148 | `TCTObservationSchema` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Kind+Version+PayloadFormat及校验器 |
| 149 | `TCTObservationSchemaRegistry` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 视图发现和版本兼容的单一注册表 |
| 150 | `TCTTestObservationRecorder` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestObserverHub + TCTObservationEnvelope（单一录制链） |
| 151 | `TCTTestObserverHub` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 152 | `TCTTestEvent` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 153 | `TCTTestEventJournal` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 流式过程事件归档，Crash后保留有效前缀 |
| 154 | `TCTTestEventReplay` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 从真实录制回放，不生成模拟通过结果 |
| 155 | `TCTTestDiagnostic` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 156 | `TCTTestEvidence` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 157 | `TCTTestEvidenceLink` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTAssertionResult / TCTArtifactRef 的关联字段（先字段后实体） |
| 158 | `TCTTestResultCollector` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 159 | `TCTTestProbeRegistry` | 类 | 新增 | **暂缓独立类型** | CxVisual.TestCenter | P2 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 160 | `TCTTestProbeContext` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestContext（仅需要独立订阅生命周期时再拆） |
| 161 | `TCTObservationOrigin` | 枚举 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTObservationEnvelope.Origin字段（先有限值，需跨版本再枚举） |
| 162 | `TCTObservationPayloadFormat` | 枚举 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTObservationEnvelope格式标记（先版本化字符串） |
| 163 | `TCTTestEventKind` | 枚举 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 164 | `TCTTestDiagnosticSeverity` | 枚举 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |

### X. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 165 | `TCTCapabilityCoverageService` | 静态类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 166 | `TCTCapabilityCoverageEntry` | 类 | 现有扩展 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTCapabilityCoverageMatrix.Entry（现有类保留兼容，不另造第二模型） |
| 167 | `TCTCapabilityCatalog` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 读取经审核的能力规格版本，不生成假ID |
| 168 | `TCTCapabilityRequirement` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 某能力对象最低必测的维度/场景规则 |
| 169 | `TCTTestEvidenceMapping` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTCapabilityCoverageMatrix.EvidenceLink（可审映射） |
| 170 | `TCTCapabilityCoverageMatrix` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 能力对象×合同/正常/边界/异常/并发/生命/版本/性能 |
| 171 | `TCTCapabilityCoverageReport` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTCapabilityCoverageMatrix + 报表投影（不独立持久化状态） |
| 172 | `TCTCapabilityCoverageRule` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTCapabilityRequirement（统一维度与门槛） |
| 173 | `TCTCapabilityMappingValidator` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 失效ID/孤儿用例/重复归属/缺维度诊断 |
| 174 | `TCTCoverageEvidenceKind` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | TestCase/Assertion/Observation/Artifact/CI/HIL |
| 175 | `TCTCoverageDimension` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Contract/Normal/Boundary/Exception/Concurrency/Lifecycle/Version/Performance |
| 176 | `TCTCoverageState` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Unmapped/Unimplemented/Untested/Partial/Verified/Failed/Blocked |

### XI. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 177 | `ITCTTestReporter` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Text/JSON/JUnit/HTML插件式输出 |
| 178 | `TCTReportWriter` | 静态类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 179 | `TCTReportManifest` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestRunSummary + TCTArtifactRef（先用同一个manifest契约） |
| 180 | `TCTTestResultRepository` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 181 | `TCTStoredTestRun` | 类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 182 | `TCTTestRunDiff` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 183 | `TCTSnapshotDiff` | 类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 184 | `TCTSnapshotDiffService` | 静态类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 185 | `TCTRunComparisonPolicy` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Commit/Fixture/Seed/环境差异可比性规则 |
| 186 | `TCTTestHistoryIndex` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestResultRepository索引（持久化内部实现） |
| 187 | `TCTTestFailureFingerprint` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 按断言码、异常栈/Owner聚合重复故障 |
| 188 | `TCTTestFailureClusterer` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestFailureFingerprint + 查询聚合（以后有数据再考虑分离） |
| 189 | `TCTReportFormat` | 枚举 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTReportWriter格式参数（已有3格式，枚举以后按需要） |
| 190 | `TCTTestComparability` | 枚举 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestRunDiff.ComparabilityReason（先字段） |

### XII. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 191 | `ITCTTestMetricSampler` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 从真实FCRSystemMonitor或SDK采集指标 |
| 192 | `ITCTTestFaultInjector` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Virtual/Replay可控故障注入，不默认触发工业输出 |
| 193 | `TCTBenchmarkPlan` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 热身/稳定性/轮次/阈值/资源占用声明 |
| 194 | `TCTBenchmarkRunner` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestExecutionCoordinator + Benchmark策略（不设第二个Runner） |
| 195 | `TCTBenchmarkSample` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 一次Latency/CPU/Alloc/Queue样本 |
| 196 | `TCTBenchmarkSummary` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | P50/P95/P99/方差/置信/环境约束 |
| 197 | `TCTBenchmarkThreshold` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 超限规则和样本最低数量 |
| 198 | `TCTTestStressProfile` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 持续时间、并发梯度、内存/CPU/句柄观察 |
| 199 | `TCTTestHealthSnapshot` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 进程/运行资源状态，来自真实监测 |
| 200 | `TCTFaultInjectionPlan` | 类 | 新增 | **暂缓独立类型** | CxVisual.TestCenter | P2 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 201 | `TCTTestReliabilityGate` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestExecutionPolicy验收规则（仅内部判定） |
| 202 | `TCTMetricKind` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Latency/Throughput/CPU/Memory/GPU/Queue/Handle |
| 203 | `TCTFaultKind` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Timeout/Crash/PacketDrop/Corruption/Disconnection/OutOfMemory |
| 204 | `TCTBenchmarkVerdict` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Pass/Regression/Inconclusive/Uncomparable |

### XIII. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 205 | `TCTGeometryObservation` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Input/Expected/Actual/单位/坐标/差异视图DTO |
| 206 | `TCTObjectTreeObservation` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 真实Ownership/Reference/Revision结构化快照 |
| 207 | `TCTGraphObservation` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 真实Node/Port/Connection/编译问题的只读投影 |
| 208 | `TCTChannelObservation` | 类 | 新增 | **暂缓独立类型** | CxVisual.TestCenter | P2 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 209 | `TCTRuntimeObservation` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 真实Instance/Scope/Lease/Waiting/Trace的只读投影 |
| 210 | `TCTTimelineObservation` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 时序事件/跨度、Correlation及关联对象 |
| 211 | `TCTResourceObservation` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 真实资源占用/待队列/释放快照 |
| 212 | `TCTTestFrameRecord` | 类 | 新增 | **暂缓独立类型** | CxVisual.TestCenter | P2 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 213 | `TCTTestGraphVisualEdge` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTGraphObservation.Edge（只读DTO） |
| 214 | `TCTTestGraphVisualNode` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTGraphObservation.Node（只读DTO） |
| 215 | `TCTTestObservationDiff` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 可视化差异结果（结构/数值/时间） |
| 216 | `TCTObservationViewKind` | 枚举 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTObservationSchema.ViewerId（由注册表决定） |
| 217 | `TCTGraphVisualRelationKind` | 枚举 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | 生产Owner真实关系契约+只读显示字段（不二次定义领域语义） |

### XIV. TestCenter

| # | 原类型/接口/枚举 | 形态 | V3.0状态 | V3.2最终裁决 | 归属 | 时序 | 归并去向/边界 |
|---:|---|---|---|---|---|---|---|
| 218 | `ITCTTestViewer` | 接口 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 219 | `ITCTTestViewerCommandProvider` | 接口 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 活动Viewer按选择贡献合法命令 |
| 220 | `TCTMainWindow` | WPF Window | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 221 | `TCTTestCenterViewModel` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 选择/Run/文档/历史/命令状态唯一会话层 |
| 222 | `TCTTestExplorerControl` | WPF控件 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 223 | `TCTTestDocumentHostControl` | WPF控件 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 224 | `TCTTestInspectorControl` | WPF控件 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 225 | `TCTTestResultsControl` | WPF控件 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 226 | `TCTTestRibbonController` | 类 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 227 | `TCTTestViewerRegistry` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Kind/SchemaVersion→Viewer，Unknown安全降级 |
| 228 | `TCTViewerDescriptor` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestViewerRegistry的只读注册记录（保留internal） |
| 229 | `TCTTestCommandRouter` | 类 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 可用性/执行/撤销规则；统一Shell命令来源 |
| 230 | `TCTTestUISelection` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestCenterViewModel.Selection（单一UI状态） |
| 231 | `TCTTestViewportControl` | WPF控件 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 通用缩放平移、选点、坐标层与截图产物 |
| 232 | `TCTTestStatusBarControl` | WPF控件 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTMainWindow状态栏（无需先建单独控件） |
| 233 | `TCTTestDashboardControl` | WPF控件 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P2 | 测试主界面、覆盖/缺口/可靠性概览 |
| 234 | `TCTTestPlanEditorControl` | WPF控件 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Suite/Plan编辑、参数、依赖、预检/执行 |
| 235 | `TCTTestHistoryControl` | WPF控件 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 历史Run、失败复现、双运行对比 |
| 236 | `TCTTestFixtureControl` | WPF控件 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Fixture、Seed、Golden预览/显式审批 |
| 237 | `TCTTestCoverageControl` | WPF控件 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 正式能力维度与缺证据热区 |
| 238 | `TCTTestTimelineControl` | WPF控件 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | 时间轴、进度和点击事件定位 |
| 239 | `TCTTestDiffControl` | WPF控件 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Input/Expected/Actual/Diff并排 |
| 240 | `TCTTestBenchmarkControl` | WPF控件 | 新增 | **暂缓独立类型** | CxVisual.TestCenter | P2 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 241 | `TCTTestViewBase` | WPF抽象类 | 现有 | **保留现有** | CxVisual.TestCenter | 已有 | 维持当前唯一实现与Owner |
| 242 | `TCTGeometryTestView` | WPF控件 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 243 | `TCTObjectTestView` | WPF控件 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 244 | `TCTGraphTestView` | WPF控件 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 245 | `TCTChannelTestView` | WPF控件 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 246 | `TCTRuntimeTestView` | WPF控件 | 现有扩展 | **保留扩展** | CxVisual.TestCenter | 已有 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 247 | `TCTTestWorkspaceLayout` | 类 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestCenterViewModel的Layout设置（仅需持久化再分类型） |
| 248 | `TCTTestCommandKind` | 枚举 | 新增 | **必要新增（按批次）** | CxVisual.TestCenter | P1 | Run/Cancel/Replay/Compare/Attach/GoToSource等 |
| 249 | `TCTTestDocumentKind` | 枚举 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | ITCTTestViewer.Kind（注册标识取稳定字符串） |
| 250 | `TCTViewerActivationState` | 枚举 | 新增 | **合并/内聚** | CxVisual.TestCenter | 后置 | TCTTestViewerRegistry.Availability（暂不另建枚举） |

## 附录B：V3.2 新增的必要概念（不计入V3.0的250项）

此前V3.0主要是类型目录，一些保障“简用闭环”的关键合同缺席。以下是本轮补充的**设计型**概念，绝非已经落地的新增代码，且不要求每项都拥有独立.cs文件：

| 新概念/目标类型 | 形态 | Owner | 时序 | 唯一职责 |
|---|---|---|---|---|
| `TCTCaseSourceAttribute` | 特性 | Tests/TestCenter | P1 | 静态版本化Dataset声明：SourceId/Version/RowKey |
| `TCTTestSourceAttribute` | 特性 | Tests/TestCenter | P1 | 测试类级默认Owner/Group/Kind声明（缺省Unknown） |
| `TCTCaseRow` | 值对象 | Tests/TestCenter | P1 | 稳定RowKey、数据哈希、Fixture版本 |
| `TCTCaseDefinitionFingerprint` | 值对象 | Tests/TestCenter | P0 | 程序集MVID/测试Method签名/Source版本/数据源哈希构成的定义指纹 |
| `TCTTestSourceLocation` | 值对象 | Tests/TestCenter | P0 | 文件/行号/PDB可用性与不可定位原因 |
| `TCTTestPreflightResult` | 值对象 | Tests/TestCenter | P0 | 预检Allow/Blocked/Unsupported/Denied及原因；不等同Result |
| `TCTObservationEnvelope` | 值对象 | Tests/TestCenter | P1 | RunId/AttemptId/Seq/SchemaVersion/SourceOwner/Correlation/ArtifactRef |
| `TCTCoverageEvidenceLink` | 值对象 | Tests/TestCenter | P1 | 官方规格版本×能力×对象×维度×断言/产物的不可变关联 |
| `TCTTestRerunRequest` | 值对象 | Tests/TestCenter | P1 | 重跑原CaseId/RowKey/Seed/DefinitionHash/FixtureHash |
| `TCTTestFailureLocation` | 值对象 | Tests/TestCenter | P0 | 断言StackFrame与源代码位置，不伪造行号 |


## 附录C：核心类型可直接实施的合同矩阵（避免只列类名）

下面的**属性与方法为设计合同**，不是对全部现有代码签名的断言。标“兼容公开”表示已有 public 声明暂不可破坏；“宿主公开”只面向平台维护者，不代表普通 Case 作者必须调用；“内部”表示目标应限制在同一 TestCenter 程序集。现有方法名不匹配时，按增量适配而不是直接重命名。

| 类型 / 接口 | 对外可见性 | 生命周期 / 是否有状态 | 关键属性或输入 | 最小操作 / 输出 | 直接依赖及验收证据 |
|---|---|---|---|---|---|
| `TCTTestAttribute` | **作者公开** | 编译期Metadata；无运行态状态 | TestId必需；Name/Group/Kind/Owner可选 | 反射生成Descriptor，旧4参兼容 | `Attribute`；D01/D12，重复ID/不合规签名诊断 |
| `TCTCaseSourceAttribute` | **作者公开** | 编译期Metadata | Source、Version、RowKeyField | 将一个方法展开为稳定CaseId集合 | 只读Fixture loader；D04/D06，数据Hash一致 |
| `TCTTestSourceAttribute` | 可选公开 | 类级声明；不改变Owner真值 | 缺省Group/Kind/Owner | 合并到方法级元数据；方法级优先 | Catalog；Unknown与Unmapped保留 |
| `TCTTestContext` | **作者公开** | **per Attempt**，不可跨线程共享可变集合 | RunId/AttemptId/CaseId/Seed/Token/Source | Assert* / Observe / SaveArtifact / Diagnostic | 单一AssertionCollector/ArtifactStore；D02/D05/D08 |
| `TCTTestCaseDescriptor` | 宿主只读，兼容公开 | **per Definition**，冻结后不可变 | TestId、Kind、Owner、SourceLocation、Fingerprint | 获取定义/声明资源/维度，不包含Result | Discovery；D06，两个Worker同一指纹 |
| `TCTTestSourceLocation` | 宿主只读 | 值对象 | Assembly, Type, Method, File?, Line?, Availability | Locate/SourceUnavailable；不猜行号 | PDB/StackTrace；D05 |
| `TCTCaseDefinitionFingerprint` | 宿主内部 | 值对象 | TestId、MVID、MethodSig、RowHash、SchemaVer | Compute / Compare，差异拒绝Worker执行 | Worker handshake；D06 |
| `TCTCaseRow` | 宿主只读 | 值对象 | StableRowKey、TypedRow、FixtureHash、Version | NormalizeRowKey / VerifyHash | Dataset/Fixture；D04，交换行顺序CaseId不变 |
| `TCTTestCatalog` | 兼容公开，作者免调用 | 应用内注册一次，读多写少 | Registrations、SnapshotVersion、Definitions | Discover/Register/Query/RejectDuplicate | 单真源；D01/D12 |
| `TCTTestCatalogSnapshot` | 宿主只读 | 不可变Catalog快照 | SnapshotId、DefinitionHashes、CaseIds | Freeze/VerifyWorkerCatalog | Catalog；D06，主从一致 |
| `TCTTestQuery` | 宿主内部 | 无状态Query值对象 | Owner/Kind/Capability/Tag/Id/CaseRow | Query(catalog, query)；精确ID与批量区分 | Catalog；D03，过滤原因可查 |
| `TCTTestSuite` | 维护者公开 | 持久可版本化定义 | SuiteId、引用项、Tags | ResolveCases(catalog) | 不包含任何Worker PID；D10 |
| `TCTTestPlan` | 维护者公开 | 持久不可变定义 | PlanId、Version、StepIds、Policy | Load/Validate/PrepareRun | Suite/Dependency/Policy；D07/D10 |
| `TCTTestPlanStep` | 维护者只读 | 定义期不可变 | StepId、CaseRef、DependsOn | ValidateDependency/ResolveCase | Plan DAG；循环/缺引用拒绝 |
| `TCTTestRunRequest` | 宿主/CI公开 | **per Run**，调用前校验并冻结 | RunId、SelectedIds、Profile、Repeat、Parallel、Timeout、Target | Validate/ResolveSelection | Coordinator；D03/D10，0用例失败 |
| `TCTTestPreflightResult` | 宿主只读 | 值对象 | Allow/Blocked/Unsupported、Reason、Requirements | GateBeforeLaunch / ReportReason | Safety/SDK/Fixture；D11 |
| `TCTTestExecutionPolicy` | 宿主内部 | 不可变策略 | Timeout、Isolation、FailFast、Retry、Resources | ResolvePriority/Validate | Coordinator；不能放宽设备安全授权 |
| `TCTTestRunSession` | 宿主只读，兼容公开 | **per Run有状态机** | RunId、State、Start/Finish | Start/RequestCancel/Finish（单次终结） | Context/Result；D10，非法状态转移拒绝 |
| `TCTTestAttempt` | 宿主只读，兼容公开 | **per Attempt不可复用** | AttemptId、CaseId、RunId、Iteration、RetryIndex、Seed、Hashes | Start/FinalizeEvidence | Worker/Context；D04/D06，重复ID不碰撞 |
| `ITCTTestCase` | 框架扩展公开 | per定义实例；Context per Attempt | Descriptor | Execute(context) | 真实Owner API；普通作者不用实现 |
| `ITCTAsyncTestCase` | 框架扩展公开 | per定义实例 | Descriptor | ExecuteAsync(context) | 同一个Runner；D02，禁止第二执行器 |
| `ITCTTestLifecycle` | 框架扩展公开 | per Attempt Setup/Teardown | Context、Fixture/Resource | SetupAsync/TeardownAsync | Teardown失败Error；D07 |
| `TCTTestExecutionCoordinator` | 宿主内部/现有兼容公开 | **per Run服务实例** | CatalogSnapshot、Policy、Cancellation | RunAsync→Summary；唯一执行编排 | TCTTestRunner/Host/Gate；D03-D10 |
| `TCTTestRunner` | 兼容公开；逐步内部化 | 一个Attempt真正执行 | TestCase、Context、Token | RunOneAsync→CaseResult | 现有Runner保留唯一真源；无断言Inconclusive |
| `TCTTestHostController` | 内部 | **per Worker**有状态句柄 | WorkerArgs、Timeout、ExpectedFingerprint | Start/Wait/Kill/VerifyExit | 同一个EXE子进程；D06/D07，遗留判Error |
| `TCTWorkerHandshake` | 内部 | per Worker/Attempt | ProtocolVersion、MVID、CatalogHash、CaseId、RowHash | ValidateBeforeExecution | WireCodec；不一致禁止运行 |
| `TCTWireCodec` | 内部/兼容现有公开 | 无状态转换 | Envelope/Schema/LengthLimits | Read/Write/Validate | 数据长度/哈希，拒绝错AttemptId |
| `TCTResourceGate` | 内部/兼容公开 | **持有者状态** | ExclusiveKeys、Cancellation | AcquireAsync→Lease | Resource/Fixture；D07，取消释放准确 |
| `TCTTestFixtureService` | 内部/兼容公开 | per run/路径根 | Root、Manifest、Hash、Policy | ResolveReadOnly/Verify/RejectTraversal | Fixture；D04/D11 |
| `TCTBaselineService` | 内部/兼容公开 | per版本Golden | GoldenId、Version、ApprovalAudit | Compare / ProposeUpdate / ApproveWithAudit | 不自动覆盖Expected；D08 |
| `TCTTestArtifactStore` | 内部/兼容公开 | per Run或Attempt证据根 | Hash、ByteSize、Mime、AttemptId | Save/ReadVerify/SecureRetention | ArtifactRef；D08，篡改拒绝 |
| `TCTTestObservation` | 兼容现有公开DTO | per Observation只读 | Kind/Title/Details/Origin | 映射到Envelope，旧用例兼容 | ObservationHub；不代表断言通过 |
| `TCTObservationEnvelope` | 内部与Viewer只读合同 | per事件不可变 | SchemaId/Version、SourceObject、Seq、Correlation、Artifact | Validate/Serialize/Replay | Owner Probe；D08，未知Schema保留数据 |
| `TCTTestObserverHub` | 内部/兼容公开 | per Run有界序列 | Seq、Capacity、AttemptId | Publish/Snapshot/Seal，溢出明确失败 | Recorder唯一入口；D08 |
| `TCTTestCaseResult` | 宿主只读 | **per Attempt不可变** | Status、Assertions、Diagnostics、Artifacts、HostExit | Consume/Export；不能由Viewer写入 | Runner唯一生成；D05/D07 |
| `TCTTestRunSummary` | 维护者公开只读 | **per Run不可变** | 全部Selected+Filtered/Blocked/Missing、CaseResults、Evidence | Summarize/EvaluateExit | Completion validator；D10 |
| `TCTReportWriter` | 内部/现有兼容公开 | 静态纯格式转换 | RunSummary、Format、Output | WriteJUnit/Json/Text | 与History共用一个Result真源 |
| `TCTCapabilityCatalog` | 平台维护者只读 | per官方版本持久快照 | OfficialSpecVersion、Capability/Object/Dimensions | LoadApproved/ValidateIds | 正式规格基线；D09，未核查=Unmapped |
| `TCTCapabilityCoverageMatrix` | 平台维护者只读 | **per规格/Run投影** | ObjectId×八维×AssertionEvidence | EvaluateRequiredDimensions | EvidenceLink；D09，缺维不能Verified |
| `ITCTTestViewer` | 插件作者公开 | per活动文档WPF状态 | Kind/SupportedSchemaVersion | CanDisplay/Bind/Clear | Result/Artifact只读；D08 |
| `TCTTestViewerRegistry` | 内部 | 应用生命周期，注册后只读 | Kind→Factory/Schema/Capability | Resolve/NoEvidence/VersionMismatch | DocumentHost；六图共Shell |
| `TCTTestCenterViewModel` | 内部 | UI窗口生命周期 | Selection/RunId/CommandState/Layout | Select/Run/Cancel/Rerun/Navigate | Coordinator + ResultRepository；不直接执行Case |
| `TCTTestCommandRouter` | 内部 | UI窗口/活动上下文 | SelectedAttempt/ViewerCapabilities/Permissions | CanExecute/RouteCommand | Dispatcher + safety gate；禁无权限运行 |

### C.1 状态真源与失败优先级（交给Codex的合约）

- **只能由执行/预检层决定结果**：`CaseResult.Status` 不允许UI/Viewer、Reporter、Coverage回写。选择不存在、Worker版本不一致、缺Fixture分别由预检/宿主产生可归档FailureReason。
- **完成前门槛**：所有选中Case有且只有一个初次Attempt或明确的未启动理由；每个开始的Attempt都必须有最终结果、退出证据、Setup/Teardown和诊断快照；缺证据至少是Error，不得Passed。
- **失败优先级不是简单覆盖旧错误**：通过后Teardown异常→Error；原本断言Failed且Teardown又失败→保留两者完整证据，最终Error，附带AssertFailed原因；用户可以查出最早的实际业务断言失败。
- **重试不能把失败历史洗掉**：`RetryIndex` 单调；最终Run可根据政策计算“最终通过/不稳定/失败”，但所有AttemptResult需永久可查；性能基准默认不自动重试以免污染样本。
- **超时语义**：InProcess只能协作式Cancellation，不可宣称强杀线程；ChildProcess若未确认Worker退出及资源回收，报`Error`并标资源遗留，而不是`TimedOut`绿灯。
- **安全优先于体验**：明确选择非Unit测试可越过默认Smoke**类别过滤**，不得越过设备安全、系统SDK、授权与环境预检。真实硬件需要生产Interlock证据；任何未知设备状态拒绝重复危险动作。

### C.2 开发者体验的自动验证脚手架（目标）

```text
D01: 新建 Tests/TestCenter/Cases/Geometry/TCTNewRectCase.cs → 不改csproj → 编译 → --run --id F.GEO.NEW.001 → JUnit恰好1Case
D02: 把该方法改为 async Task → Compiler/Worker均接受 → 故意抛异常，报告保留真实堆栈
D03: 明确选中Kind=Integration → 即使默认Smoke仍运行或返回安全预检明确拒绝理由
D04: Fixture准备含RowKey的100条，重排文件行 → CaseId不变化，旧失败行仍能精确重跑
D05: 制造False断言 → Failed + Expected/Actual + Source（PDB缺失时显示Unavailable）
D06: 故意让Worker加载不同MVID/RowHash → 执行前Error，实际算法不被调用
D07: Setup异常+Teardown异常 → 两个错误完整保留；资源租约归还
D08: 发布未知SchemaVersion的Observation → 原始Artifact可读，Viewer显示VersionMismatch不报Passed
D09: 未审核CapabilityId的Passed测试 → Matrix为Unmapped；八维缺1项则Partial
D10: --id 不存在 / --filter 结果0项 → 非零退出，JUnit保留失败原因/空选证据
D11: 请求Real X-Ray设备但无授权 → 预检拒绝，Worker不得访问设备
D12: 移动/删除Case源文件 → 自动重新编译且旧TestId不存在，非静默命中缓存
```

## 附录D：术语与最终裁决词典

- **保留现有**：GitHub或V3.0现有类继续使用，保持兼容；无须为V3.2重新新建同名文件。
- **保留扩展**：同一代码真源补字段/方法及自动化回归；不允许有第二套Collector/Result/Runner。
- **必要新增（按批次）**：能力确有稳定语义，但只在指定验收阶段实现；不得为追求类型数量先制造空类。
- **合并/内聚**：不新建同义公开类，行为统一进已有对象或一个版本化不可变DTO；若旧类已存在先兼容后内部委托。
- **条件暂缓**：尤其Infrastructure候选，没有跨生产Owner复用证据就不下沉。
- **暂缓独立类型**：非开发者最短路，若将来需求产生再做架构变更；不视为能力永久删除。
- **现状事实 vs 目标签名**：本文代码示例涉及新重载/新特性时统一标“目标”，需要CI与可复现实例才能称“已完成”。

## 附录E：来源与版本校验

1. 本地来源文件：`CxVisual_TestCenter_详细架构与类契约规格_V2.0.md`、`CxVisual_TestCenter_FMP子方面归属与公共接口依赖设计_V2.1.md`、`CxVisual_TestCenter_开发者优先_双项目完整类接口枚举规划_V3.0.md`、`CxVisual_TestCenter_V3.1_开发者简用性复审与API收敛建议.md`。
2. GitHub：`https://github.com/oudream/cx-visual-x3`，读取master `ff24085207119fc3717522fd1d6ca8403e0c4c7f` 关键源码。此状态仅为核对时快照，Codex开始工作前必须重读master HEAD。
3. UI参照：`CxVisual_TestCenter_6个高清测试界面_测试架构_V1.1.zip`；图中旧版SymbolDefinition/独立Graph假设按现行CxVisual对象架构修正，不直接复制。
4. 不使用250候选项作为开发量或完成比例，更不引用历史CI里62/62当作新开发者API已通过的证据。
