# CxVisual.TestCenter 统一测试体系完整规格 V4.1（唯一技术主规格）

> **文档定位**：V4.1 为唯一现行设计与验收规格；由 V4.0 **完整正文增量融合**，不缩写、不删减历史 250 项类型、43 项核心合同、264 条八维需求索引、42 项界面控件映射、D01～D12/U01～U10 与 ADR-T01～T15。V2.1、V3.2、V4.0 归档追溯，不再并行定义另一套规范。  
> **发布日期**：2026-10-20。目标环境：.NET Framework 4.8、C# 7.3、Windows x64、WPF；仓库 `oudream/cx-visual-x3` 的 `master`。**本文版本 ≠ 代码版本 ≠ 验收通过**。  
> **当前核对的自动化事实基线**：代码 Commit `16482d6810cc093ff480a82691c67b1ace888441`；[TestCenter Windows CI 39410511642](https://github.com/oudream/cx-visual-x3/actions/runs/39410511642) success，Catalog 121，隔离 121/121，三轮并发 363/363，WPF Shell Smoke 与结构化证据回放通过。**核对时 master 已出现更晚的文档提交**，不能把该 CI 当作任意未来 Commit 的通过证明。完整 UIA、六场景人工与工业现场验收未因此完成。  
> **程序归属**：唯一 `Tests/TestCenter/CxVisual.TestCenter.csproj`（WPF Shell + CLI + Worker），不是第七 Workbench、不新增测试项目。**界面设计位置**：`DOCS/Architecture/TestCenter/` 的六张 2560×1440 图及总览；其中 Mock 内容不代表真实被测结果。  
> **动态台账**：`DOCS/Testing/CxVisual_TestCenter_V4.0_全局工作包与验收台账.md` 保存 TCT-00～TCT-16 进度、Commit、CI、失败及验收证据。台账是实施事实，不另行定义技术合同。  
> **阅读顺序**：优先阅读 V4.1 新增的 §03.0、§04.4、§06.6、§07.3、§08.9、§09.6、§10.9～§10.12，再按需使用保留下来的十章全文与附录。原 V4.0 旧“尚未实现/目标未验/S0～S5”表是 2026-10-16 **历史截面**，与现行状态冲突时以 V4.1 裁决和当次实测证据为准。

### 阅读导航（全文只有一套规范）

| 章 | 内容 | 读者优先级 |
|---|---|---|
| **01** | 总体目标与边界 | 所有人先读 |
| **02** | 六种角色的期望与服务入口 | 产品/开发/操作角色 |
| **03** | 开发者最简单的使用方式 | 普通与进阶开发者 |
| **04** | F/M/P职责归属及双项目依赖 | 架构师、工程维护者 |
| **05** | 完整测试能力体系 | 测试框架设计者 |
| **06** | 核心对象、类/接口/枚举契约与身份关系 | C#开发者、Codex |
| **07** | 从发现到归档的完整执行过程 | Runner/Worker作者 |
| **08** | 单一WPF Shell与六场景真实交互 | UI/Viewer作者 |
| **09** | 八维覆盖、D/U验收及正式规格关联 | 测试/质量负责人 |
| **10** | GitHub现状、实施任务、CI证据与版本裁决 | Codex/维护者 |
| **附录A** | 250项完整历史类型裁决 | 核对Owner、去重、不遗漏 |
| **附录B** | 完整性与复核记录 | 264条能力八维记录已在第09章 |

**V4.1 唯一规范原则**：原 10 章的功能范围、原表和编号完整保留以供稽核，新增V4.1裁决对旧歧义具有规范优先级；生产 Owner 的真实职责/安全边界优先于测试假设。合同变动修订此文，进度更新台账；每项声称“通过”必须绑定具体 Commit/CI/真实操作证据。

## 01 总体目标与不可妥协的边界

TestCenter 是 **1 Platform + 6 Workbenches** 的公共测试中心，**不是第7 Workbench**。Foundation / Mechanism / Professional 仍是业务能力Owner；TestCenter对它们作真实黑盒与协议测试、对结果作只读可视呈现，而非代替业务执行内核。

### 01.1 九条不可妥协的验收底线

1. **真实执行**：Result只能由真实Owner代码或显式声明的Virtual/Replay Provider生成；界面不能篡改PASS/FAIL。
2. **开发者简用（V4.1）**：普通测试作者首选标准 NUnit 的 `[Test]`、`[TestCase]` 与 NUnit 断言；需要真实 Owner 的结构化观察与专用证据时沿用 `[TCTTest] + TCTTestContext`。两者进入**同一 Catalog/Worker/Run/Attempt/Result**，均不要求作者掌握 Runner/ReportWriter。
3. **身份唯一**：Definition/TestId、CaseId、RunId、AttemptId、真实ExecutionInstanceId、CorrelationId彼此不可替换。
4. **单一执行真源**：同一编译目录、同一Catalog、同一Worker重建、同一结果模型；不另建DSL Runner。
5. **负例闭环**：无测试、无断言、断言失败、超时、异常、Worker崩溃、清理失败、未授权、数据错版，绝不能伪绿。
6. **证据可回查**：Case→Attempt→AssertionId/SourceLocation/Artifact→Run/History/CI→Viewer，任一断链可解释。
7. **能力验收真实**：通过率≠规格覆盖。官方规格与八维需求未证据化时只能Unmapped/Partial。
8. **UI读事实**：Graph/Channel/Runtime的演示图不能替代缺失的实际Adapter；未知Schema安全降级。
9. **物理安全**：停止/杀掉Worker进程≠PLC/Motion/X-Ray物理安全停止；HIL需要安全Owner授权与独立联锁。

### 01.2 “一个文档”和“两个项目”的含义

- **一个文档**：指 TestCenter 统一测试体系的**唯一规范文件**，含全部功能目标、完整类裁决、UI行为与测试证据。附录属于同一文件，不再形成平行规范。
- **两个项目**：指测试支撑体系仅由 `CxVisual.Foundation.Infrastructure` 与 `CxVisual.TestCenter` 承担通用/测试专用职责，**不是**把全部Foundation/Mechanism/Professional生产能力迁到它们。
- **一个测试中心程序**：原有 `Tests/TestCenter` WPF + CLI/Worker 共用相同Catalog、Request、Attempt、Result，不因六幅设计图拆成六个EXE或六套Runner。
- **设计/代码/验收三状态分离**：目标设计已定、类型源码存在、功能通过CI、GUI交互人工验收、生产能力八维正式验收，这五者都必须分别标注。

## 02 六种角色的期望与服务入口

| 角色 | 不必学习的框架内部细节 | 10分钟内应能完成 | 遇错必须得到 |
|---|---|---|---|
| 普通体系开发者 | Host、Collector、WireCodec、Catalog注册细节 | 新增1个`.cs`，写一个Attribute+Assert；运行并跳转失败位置 | 精确Expected/Actual、异常/源码（PDB缺失需说明） |
| 能力/公共对象开发者 | 额外的Graph/Runtime测试替身 | 引真实Owner对象、录制真实Observation、管理虚拟Fixture | Owner ID、原始数据/版本、专业Viewer |
| 工程编排用户（无C#） | 测试代码与程序集装配 | 从Explorer选择Case/Suite，配置参数/目标，执行与查看进度 | 资源/依赖/前置条件/未接入原因 |
| 调试人员 | 结果存储内部协议 | 同一Attempt追踪Trace/Timeline/Diff并定点重跑 | 真实Correlation、RuntimeInstance、Unknown/Retry |
| 测试平台维护者/CI | 专业算法内部实现 | 建Plan，设门槛，做正式规格八维覆盖、历史回归 | Unmapped、MissingEvidence、不可比原因 |
| 运行/设备安全负责人 | 无关的公共用例实现 | 查看真实设备授权、风险级别、Evidence、审计记录 | Blocked，不能出现绕过互锁的Run按钮 |

### 02.1 用户不应为测试框架内部复杂性付费

普通作者只写 `Attribute+Context+真实被测代码`；进阶作者只增加版本化参数/Fixture及自然 `async Task`；维护者才维护 Suite/Plan/Coverage/CI；工程编排用户完全可以从Explorer选择已定义用例，不需要编译C#。所有入口指向同一 Catalog/Run/Attempt/Result。

## 03 开发者怎样最简单地使用（规范性API）

### 03.0 开发者入口与NUnit/TCT的唯一裁决（规范性）

在当前 TestCenter 中，**一般开发者优先使用原生 NUnit**，而不是强制重新学习一套 TCT 断言。一个新源文件放在 `Tests/TestCenter/Cases/`，由传统 csproj 的受限 `Cases\**\*.cs` 编译规则自动发现；现有 NUnit 适配器支持同步、`async Task`、参数化及正常 NUnit 生命周期。TestCenter 负责统一发现、RunId/AttemptId、Worker 隔离、证据与 JUnit，不要求手改 `.csproj`、Catalog 或 Runner。示例是可以在真实 Owner 可引用前提下编译的表达方式，不是 CI 验收证明：

```csharp
using NUnit.Framework;
using CxVisual.Foundation.Geometry;
[TestFixture]
public sealed class GeometryChecks
{
    [Test]
    public void ContainsPoint()
    {
        CxRect2D rect = new CxRect2D(0, 0, 100, 80);
        Assert.That(rect.Contains(new CxPoint2D(5, 5)), Is.True);
    }
}
```

**进阶作者**需要绑定 `MEMProjectSnapshot`、MRK Journal、MDS 或专业真实 Owner 身份、跨 Worker 结构化 Evidence 时，继续使用**已有 TCT Attribute/Context**、`ObserveStructured`、ArtifactStore。旧的四参 `TCTTestAttribute` 和 TCT 测试用例保持兼容。原第 03 章的“单参特性和二参断言”属于 TCT 简用性目标；不能因此建立第二个通用 Assert 框架。对外只保留真实且最短的作者路径。

**统一执行**：标准 NUnit 和 TCT 不得各自创造平行的 Catalog、RunSession、Attempt、状态判定器或 Worker 协议。测试入口选择精确 ID 后才能进行安全/环境预检；未知 ID、无断言、Worker 崩溃、指纹不符、空集合均不得伪造 Passed。外部 NUnit DLL 仅允许可信文件并校验指纹；真实设备操作要另行授权和外部联锁。

### 3.1 TCT专业作者兼容与目标API（原V4.0目标，非NUnit默认入口）

```csharp
[TCTTest("F.GEO.RECT.CONTAINS.001")]
public static void Contains(TCTTestContext test)
{
    var rect = new CxRect2D(0, 0, 100, 80);
    test.AssertTrue(rect.Contains(new CxPoint2D(25, 30)), "矩形包含");
}
```

- Attribute要求稳定TestId；展示名缺省由方法名确定；Owner/Kind/Group可从类级默认获取，不能假填正式能力ID。
- `AssertTrue(bool, string)`/`AssertFalse(bool, string)`是新目标重载；旧四参继续兼容。
- `async Task(TCTTestContext)`自然await，禁用`async void`；Parameter Case允许额外 `(TCTTestContext,TRow)` 且必须声明可重建数据源。
- 新增Case文件后自动编入Tests/TestCenter项目。显式`--id`绕过默认Smoke *分类过滤*，但**不能绕过**设备安全/SDK/Fixture预检。源码更名不改TestId。

### 3.2 有参数/Fixture作者（目标API，**待S3落地**）

```csharp
[TCTTest("F.GEO.RECT.TABLE")]
[TCTCaseSource("Fixtures/Geometry/rect-cases.json", Version="2", RowKeyField="key")]
public static void ContainsRow(TCTTestContext test, TCTRectRow row)
{
    var rect = new CxRect2D(row.X,row.Y,row.W,row.H);
    test.AssertEqual(row.Expected, rect.Contains(new CxPoint2D(row.Px,row.Py)), "Contains");
}
```

- `TestId`是方法定义ID，`CaseId=TestId#规范化RowKey`；`Iteration`/`RetryIndex`与`CaseId`正交。
- 100行各有独立Result，缺失/重复行键、数据Hash与声明版本不符 → 显式DiscoveryError/Blocked，不静默跳过。
- Fixture从只读版本化源加载，变更后必须重新计算Fingerprint；不加载任意CLR类型名。Golden更新采用Propose→Review→Approve→Commit和审计链，绝不自动更新Expected。

### 3.3 平台维护者（目标API，**待S3落地**）

```csharp
// 目标接口示意，非已存在的代码签名。
TCTTestPlan plan = TCTTestPlan.Load("Foundation-Smoke.json");
TCTTestRunRequest request = plan.PrepareRun(TCTTestRunProfile.CI);
TCTTestRunSummary result = await coordinator.RunAsync(catalog,request,token,onResult);
```

**不承诺**V3.0的`TCTTest.Define(...).Act(...).Build()`作为普通作者入口；此模式在独立Worker中不能自然重建闭包，未经额外协议设计不得假设可靠。

### 03.4 TCT作者最短操作路径（普通NUnit作者优先见§03.0）

1. 在 `Tests/TestCenter/Cases/` 新增一个C#源文件（目标S1须由受限Glob自动包含；当前传统项目尚按显式文件编译，**不能假称已完成**）。
2. 写一个带稳定TestId的 `[TCTTest("...")]`，测试方法内真实调用生产Owner并断言。单参数重载是目标API，现有四参数继续保持兼容。
3. IDE Build或CI编译后发现器创建定义并显示；发现失败与重复ID是错误，不静默丢弃。
4. 通过GUI Explorer或CLI `--run --id <TestId>` 明确选择；单项显式选择**先解析精确ID再预检**，批量Profile过滤不能掩盖用户点选。
5. 实际Worker执行并产生带RunId/CaseId/AttemptId的不可变结果；失败时应显示断言、Expected/Actual、异常、源码位置与Artifact。
6. 一键重跑创建**新的**Attempt；历史失败仍可回查、不可覆盖。

### 03.5 工程编排用户的无代码操作路径

打开TestCenter → 在Explorer中定位能力对象 → 选已有Case/Suite → 填入可编辑的已审参数和Fixture → 显示模拟/真实设备模式及安全预检 → Run → 在DocumentHost看专业Viewer → 在Inspector看真实断言/证据 → 回放与比较。任何“没有生产Provider”的场景只能显示 `Unsupported / NoEvidence`，不能创造示例帧或假执行颜色。

## 04 F/M/P职责归属、两个测试支撑项目与依赖

- `Foundation.Infrastructure`：FLD/FCR生产通用服务及确证复用的候选，**不出现`TCT*`**；Geometry/Numerics/Data等基础算法不因测试需求移入它。
- `Tests/TestCenter`：唯一测试程序及内部功能分层；类型TCT/接口ITCT；被测生产对象继续保留原Owner。
- **普通作者公开**：`TCTTestAttribute` + `TCTTestContext`；有参数时增加`TCTCaseSourceAttribute`。Plan/Request宿主公开，其他Core/Worker/ViewerRegistry内部化但已有public保留兼容，不能立即破坏ABI。
- `TCTTest`多种静态Builder / `TCTTestCaseBuilder<TResult>`候选不首批建设；只维持一条可跨进程重建的测试注册路径。

### 04.1 双项目的唯一职责与依赖规则

| 项目 | 可以放的内容 | 严禁放入 |
|---|---|---|
| `CxVisual.Foundation.Infrastructure` | FLD日志、FCR Worker/并发/系统监测与确有生产复用证据的其他公共底座；`FLD`/`FCR`前缀 | TestCase、断言、Fixture/Golden、Runner、Worker测试协议、测试Viewer；专业Graph/CT/Channel业务 |
| `CxVisual.TestCenter`（`Tests/TestCenter`） | 测试发现/管理/隔离、Suite/Plan、诊断、报告、观察、正式规格覆盖、统一WPF与测试Cases；`TCT/ITCT`前缀 | 生产DataHeap/DataStack/Graph/Channel/Runtime另一个真源 |

**依赖**：Tests → 真实Foundation/Mechanism/Professional Owner；生产工程 ↛ Tests。各生产项目只有存在实际依赖时才引用 `Foundation.Infrastructure`；不得要求所有Foundation/Mechanism一律引用它。

### 04.2 Owner逐项目责任审查表（来自原12表中“Owner职责与依赖”）

| 层 | Owner项目 | 具体职责 | 禁止 | 接口放置原则 | 核查备注 |
| --- | --- | --- | --- | --- | --- |
| Foundation | Foundation.Core | 稳定值对象/ID/Guard | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Foundation | Foundation.Numerics | 数值算法 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Foundation | Foundation.Geometry | 几何/坐标变换 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Foundation | Foundation.Imaging | 底层图像 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Foundation | Foundation.Volume | 基础体数据 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Foundation | Foundation.Data | Schema/配置/执行数据合同 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Foundation | Foundation.Infrastructure | FLD日志/FCR并发监测 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Foundation | Foundation.Transport | 通用传输原语 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Mechanism | Mechanism.Core | Project/Page/Content/Symbol/Group/Element | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Mechanism | Mechanism.Data | Variable/Binding/ExecutionDataScope | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Mechanism | Mechanism.Logic | 表达式/规则/事件 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Mechanism | Mechanism.Connectivity | Channel/Protocol/Session | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Mechanism | Mechanism.Visual | 可视投影/编辑 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Mechanism | Mechanism.Orchestration | Node/Port/Connection/编排定义 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Mechanism | Mechanism.Runtime | Plan/Instance/Journal/Lease | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Mechanism | Mechanism.Lifecycle | 版本/迁移/部署 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Mechanism | Mechanism.Presentation.Wpf | 机制呈现适配 | 不允许引用Tests/TestCenter | 生产Owner各自保存接口与实现 | V2.1 Owner 目标；具体代码需按SHA复核 |
| Tests | CxVisual.TestCenter | 单一WPF项目：全部TCT测试能力 | 允许单向按需依赖生产Owner | 不侵入生产类型 | 唯一测试程序 |

### 04.3 可执行的依赖图门槛

现有 `Build/Verify-OwnerDependencies.ps1` 应审计全部 `.csproj`：Foundation不能向Mechanism/Professional/Tests上依赖，Mechanism不能向Professional/Tests上依赖；不得有依赖环、未知路径或重复引用。TestCenter 仍只保留**一个**物理项目；测试领域功能不下沉到Foundation.Infrastructure。

### 04.4 MEM工程组织、机制与专业 Owner 的现行契约（规范性）

**工程组织只有一份真源**：`Mechanism/Core/Engineering/` 下的 `MEMNode`、`MEMProject`、`MEMPage`、`MEMContent`、`MEMSymbol`、`MRCVisualItem`、`MRCGroup`、`MRCElement`，由 `MEMEngineeringSession` 持有统一编辑事务、Revision、Snapshot、索引、Undo/Redo 与持久化语义。历史 `CxNode/CxProjectDocument` 是旧模型资料，不能为其在 TestCenter 中重新建树。

| 当前对象/子方面 | 真实 Owner | TestCenter 应测试/消费的唯一合同 |
|---|---|---|
| `MEMNode/MEMProject/MEMPage/MEMContent/MEMSymbol` | Mechanism.Core | `Id/Kind/Parent/Children` 属 **Ownership**；Page通过RootContent组织页面；Project可以拥有独立Content和Symbol资源 |
| `MRCVisualItem/MRCGroup/MRCElement/MRCTransform` | Mechanism.Core 定义，Mechanism.Visual 投影 | Group与Element共享VisualItem基类，Group**不是**Element子类；几何/层级/锁定/变换必须以真实场景投影验证 |
| `MRIResourceReference/MRIReferenceSnapshot` | Mechanism.Core | Content对Symbol/Content的使用是 **Reference**；引用与Ownership、Port/Connection是不同关系，不能将Symbol当作Content Owned Child |
| `MRIObjectAddress` | Mechanism.Core | ProjectId + ResourceId + ObjectId + InstancePath定位工程定义及其引用实例；与 `RuntimeInstanceId` 必须独立保留 |
| `MEMProjectSnapshot` / `MEMNodeSnapshot` | Mechanism.Core | 已提交Revision的只读快照；节点携带ParentId/ChildIds、Visual、References、Properties、Ports、Connections、PublicMappings、Aspects，TestCenter只能作证据投影 |
| `MTCEditSession/MTCEditDraft/MEMFileProjectStore` | Mechanism.Core | 工程编辑Preview/Commit/Undo/Redo、保存重开、冲突和拒绝路径，不允许TestCenter替代生产事务 |
| `MPCPort/MPCConnection/MOLLogicDefinition` | 现有机制定义Owner | 属性/端口/连接/结构化Logic属于生产定义，编译、构建、运行各有清晰Owner；测试不造第二套编排图 |
| MRK ExecutionPlan、RuntimeSession、Journal、Lease | Mechanism.Runtime 等真实执行Owner | 可观测执行实例、SourceMap、Unknown/Waiting/Recovery；不能从WPF节点颜色计算生产执行状态 |
| MDS/MDE/Channel/Protocol及专业设备 | 各自Foundation/Mechanism/Professional Owner | TestCenter只能调用真实API或明确授权的Virtual/Replay Provider，生产不能反向引用TCT |

**依赖规则承接V2.1，不需要再保留另一份平行规范**：Foundation→Mechanism/Professional/Tests、Mechanism→Professional/Tests、Professional→Tests均禁止；实际用到基础日志/Worker才按需引用 `Foundation.Infrastructure`，不强制所有项目引用。接口由真实语义Owner拥有，纯计算用静态方法，有状态服务需明确生命周期，宿主负责组合。具体归属审查：找现有Owner→核复用场景→确认接口放置→按需ProjectReference→Owner DAG及CI负例验证。

**工程定义→构建→运行贯通**：MEM/MOL/MPC快照与验证由生产Owner提供；MOC编译及SourceMap、MRK执行与Journal仍由各自真实Owner提供。TestCenter通过只读Evidence桥接，不定义可编辑Graph或另一套Runtime。生产项目如果缺少所需真实Probe/SourceMap，应在对应Owner体系补全，而不是在测试窗口伪造。

## 05 完整测试能力体系：不仅有Case与Runner

| 领域 | 必备公共测试能力 | Owner或依赖 | 阶段 | 当前状态约束 |
|---|---|---|---|---|
| 测试定义/发现 | Attribute、Task、编译发现、稳定ID、指纹、Catalog | TestCenter | S1/S2 | 部分已有；单参/Task/指纹仍需验证实现 |
| Suite/Plan/编排管理 | DAG、筛选、目标、依赖、失败重跑、CI/CLI | TestCenter | S2/S3 | 不复制生产MRK Scheduler |
| 断言/诊断 | Expected/Actual、容差、Exception、源码定位、Trace | TestCenter/真实Owner | S1/S4 | 不能仅靠文本Details建立专业数据 |
| 数据与基准 | Fixture、Dataset、Generator、Golden审批、Sandbox、版本/ArtifactHash | TestCenter；生产数据Owner另有归属 | S3/S4 | Artifact存取已有部分，不等于Golden审批完成 |
| 观察/可视 | Envelope、Schema、Recorder、Timeline/Replay/Diff、五Viewer | TestCenter + 真实Owner Probe | S4/S5 | 未接Provider标NoEvidence |
| 规格覆盖 | 官方版本、能力对象、8维Requirement、EvidenceLink | TestCenter + 已审能力主表 | S4 | Unmapped不计Verified |
| 性能/可靠性 | Benchmark、P50/95/99、吞吐、资源泄漏、长时CI | TestCenter/Owner Monitor | S5 | 环境不可比不能作回归统计 |
| 工业运行边界 | Graph/Port/Channel/Session/Runtime/Resource | Mechanism生产Owner | 随Owner推进 | 测试中心不可先造假的被测能力 |
| 设备现场 | Automation/Vision/CT3D/Inspection/Simulation | 专业Owner | 独立验收 | 必须物理安全Owner确认 |

### 05.1 核心测试能力组的闭环要求

| 功能域 | 开发者期望最小动作 | 必须由框架自动完成 | 必须可审计的错误条件 |
|---|---|---|---|
| 发现与声明 | Attribute定义用例 | 受限源码编译、稳定发现、Catalog快照 | ID重复、非法签名、Worker指纹错 |
| 参数和Fixture | 声明数据源和稳定RowKey | CaseId展开、RowHash、Seed、只读加载 | 缺RowKey、重复行键、篡改Golden |
| Suite与Plan | 加载/选择可复用计划 | 依赖DAG校验、资源预约、无代码配置 | 缺依赖、DAG循环、资源不足 |
| 执行和隔离 | 点击Run/CLI执行 | Worker、异步/取消、Timeout、隔离、收尾 | Worker崩溃、未退出子进程、Cleanup失败 |
| 断言/诊断 | Assert* / Observe | 统一Result、异常、源码定位和Artifact | 无断言、丢证据、乱序/错Attempt |
| 观察和专业Viewer | 选择已有Result | 结构化Observation与可插拔只读Viewer | 未接Provider、未知Schema、跨Run串线 |
| 覆盖率 | 选择正式能力规格 | 能力对象×八维×AssertionEvidence核查 | ID Unmapped、Required维度缺失 |
| 性能/可靠性 | 运行Benchmark/Stress计划 | 样本、P50/P95/P99、CPU/内存及环境证据 | 机器/Commit不可比、长任务泄漏 |
| 现场/HIL | 显式申请授权 | Owner安全互锁检查、模拟默认、证据审计 | 无授权真实设备不得启动 |

**使用优先级**：先把作者最短闭环（S1）做完整，再补身份协议、参数计划、结构化观察，最后才是高级界面与HIL。

## 06 核心对象、关系、类/接口/枚举完整契约

```text
TestSource/Assembly ──Compile──▶ TestDefinition(TestId, Method, Fingerprint)
                                   │
                                   ├── CaseRows[StableRowKey/RowHash] → CaseId
                                   └── OfficialCapabilityRefs (可空/Unmapped)
TestSuite (引用CaseId集合) ──▶ TestPlan (引用Suite/Case、DAG、Policy、Version)
                                   │
                        Validate + Preflight + Select
                                   ▼
TCTTestRunRequest → RunSession(RunId) → Attempt(AttemptId,CaseId,Iteration,RetryIndex)
                                   │
                  IsolationWorker ← CatalogSnapshot/DefinitionFingerprint/FixtureHash
                                   │
                Setup → ExecuteRealOwnerCode → Assert/Observe → Teardown
                                   │
           Diagnostics/Artifact/Security/ProcessExit/Heartbeat
                                   ▼
                  ImmutableCaseResult → RunSummary → History/JUnit/Coverage
                                   │
                  TestExplorer / Inspector / Viewer / Timeline
```

**不可混合**：`Suite ≠ Plan ≠ RunRequest ≠ RunSession ≠ Attempt`。Plan是版本化定义，Run/Attempt是瞬时事实，Result是不可变历史。真实Graph Definition/Runtime Instance还属于Mechanism Owner，不能放进测试结果对象当做可修改源。

### 06.1 三个可见性层次：作者API / 平台维护API / 框架内部合同

- **普通作者公开**：`TCTTestAttribute` 与 `TCTTestContext`；参数化时可增加 `TCTCaseSourceAttribute`。默认写测试不触碰Runner、Catalog或WireCodec。
- **进阶框架扩展**：`ITCTTestCase`、`ITCTAsyncTestCase`、`ITCTTestLifecycle`、`ITCTTestViewer` 等只在明确扩展场景使用。
- **宿主/维护接口与内部合同**：Suite/Plan/RunRequest/Report/CapabilityCatalog/WorkerHandshake/ObservationSchema等，通过相同真源组织，但不要以多个同义Builder暴露给普通作者。

**兼容要求**：现有.NET Framework4.8/C#7.3类型仍存在时，应增量扩展，不应因内部化建议直接破坏旧ABI。

实际来源V3.2附录C记录 **43条详细核心合同**（不能沿用先前文字摘要声称的39条）。下表完整转录这些源头约束；“关键属性/操作”为**目标合同**，不等于已编译存在的方法。

| 类/接口 | 对外可见性 | 状态/生命周期 | 关键属性与输入 | 最小操作/输出 | 依赖与证据 |
|---|---|---|---|---|---|
| TCTTestAttribute | 作者公开 | 编译期Metadata；无运行态状态 | TestId必需；Name/Group/Kind/Owner可选 | 反射生成Descriptor，旧4参兼容 | Attribute；D01/D12，重复ID/不合规签名诊断 |
| TCTCaseSourceAttribute | 作者公开 | 编译期Metadata | Source、Version、RowKeyField | 将一个方法展开为稳定CaseId集合 | 只读Fixture loader；D04/D06，数据Hash一致 |
| TCTTestSourceAttribute | 可选公开 | 类级声明；不改变Owner真值 | 缺省Group/Kind/Owner | 合并到方法级元数据；方法级优先 | Catalog；Unknown与Unmapped保留 |
| TCTTestContext | 作者公开 | per Attempt，不可跨线程共享可变集合 | RunId/AttemptId/CaseId/Seed/Token/Source | Assert* / Observe / SaveArtifact / Diagnostic | 单一AssertionCollector/ArtifactStore；D02/D05/D08 |
| TCTTestCaseDescriptor | 宿主只读，兼容公开 | per Definition，冻结后不可变 | TestId、Kind、Owner、SourceLocation、Fingerprint | 获取定义/声明资源/维度，不包含Result | Discovery；D06，两个Worker同一指纹 |
| TCTTestSourceLocation | 宿主只读 | 值对象 | Assembly, Type, Method, File?, Line?, Availability | Locate/SourceUnavailable；不猜行号 | PDB/StackTrace；D05 |
| TCTCaseDefinitionFingerprint | 宿主内部 | 值对象 | TestId、MVID、MethodSig、RowHash、SchemaVer | Compute / Compare，差异拒绝Worker执行 | Worker handshake；D06 |
| TCTCaseRow | 宿主只读 | 值对象 | StableRowKey、TypedRow、FixtureHash、Version | NormalizeRowKey / VerifyHash | Dataset/Fixture；D04，交换行顺序CaseId不变 |
| TCTTestCatalog | 兼容公开，作者免调用 | 应用内注册一次，读多写少 | Registrations、SnapshotVersion、Definitions | Discover/Register/Query/RejectDuplicate | 单真源；D01/D12 |
| TCTTestCatalogSnapshot | 宿主只读 | 不可变Catalog快照 | SnapshotId、DefinitionHashes、CaseIds | Freeze/VerifyWorkerCatalog | Catalog；D06，主从一致 |
| TCTTestQuery | 宿主内部 | 无状态Query值对象 | Owner/Kind/Capability/Tag/Id/CaseRow | Query(catalog, query)；精确ID与批量区分 | Catalog；D03，过滤原因可查 |
| TCTTestSuite | 维护者公开 | 持久可版本化定义 | SuiteId、引用项、Tags | ResolveCases(catalog) | 不包含任何Worker PID；D10 |
| TCTTestPlan | 维护者公开 | 持久不可变定义 | PlanId、Version、StepIds、Policy | Load/Validate/PrepareRun | Suite/Dependency/Policy；D07/D10 |
| TCTTestPlanStep | 维护者只读 | 定义期不可变 | StepId、CaseRef、DependsOn | ValidateDependency/ResolveCase | Plan DAG；循环/缺引用拒绝 |
| TCTTestRunRequest | 宿主/CI公开 | per Run，调用前校验并冻结 | RunId、SelectedIds、Profile、Repeat、Parallel、Timeout、Target | Validate/ResolveSelection | Coordinator；D03/D10，0用例失败 |
| TCTTestPreflightResult | 宿主只读 | 值对象 | Allow/Blocked/Unsupported、Reason、Requirements | GateBeforeLaunch / ReportReason | Safety/SDK/Fixture；D11 |
| TCTTestExecutionPolicy | 宿主内部 | 不可变策略 | Timeout、Isolation、FailFast、Retry、Resources | ResolvePriority/Validate | Coordinator；不能放宽设备安全授权 |
| TCTTestRunSession | 宿主只读，兼容公开 | per Run有状态机 | RunId、State、Start/Finish | Start/RequestCancel/Finish（单次终结） | Context/Result；D10，非法状态转移拒绝 |
| TCTTestAttempt | 宿主只读，兼容公开 | per Attempt不可复用 | AttemptId、CaseId、RunId、Iteration、RetryIndex、Seed、Hashes | Start/FinalizeEvidence | Worker/Context；D04/D06，重复ID不碰撞 |
| ITCTTestCase | 框架扩展公开 | per定义实例；Context per Attempt | Descriptor | Execute(context) | 真实Owner API；普通作者不用实现 |
| ITCTAsyncTestCase | 框架扩展公开 | per定义实例 | Descriptor | ExecuteAsync(context) | 同一个Runner；D02，禁止第二执行器 |
| ITCTTestLifecycle | 框架扩展公开 | per Attempt Setup/Teardown | Context、Fixture/Resource | SetupAsync/TeardownAsync | Teardown失败Error；D07 |
| TCTTestExecutionCoordinator | 宿主内部/现有兼容公开 | per Run服务实例 | CatalogSnapshot、Policy、Cancellation | RunAsync→Summary；唯一执行编排 | TCTTestRunner/Host/Gate；D03-D10 |
| TCTTestRunner | 兼容公开；逐步内部化 | 一个Attempt真正执行 | TestCase、Context、Token | RunOneAsync→CaseResult | 现有Runner保留唯一真源；无断言Inconclusive |
| TCTTestHostController | 内部 | per Worker有状态句柄 | WorkerArgs、Timeout、ExpectedFingerprint | Start/Wait/Kill/VerifyExit | 同一个EXE子进程；D06/D07，遗留判Error |
| TCTWorkerHandshake | 内部 | per Worker/Attempt | ProtocolVersion、MVID、CatalogHash、CaseId、RowHash | ValidateBeforeExecution | WireCodec；不一致禁止运行 |
| TCTWireCodec | 内部/兼容现有公开 | 无状态转换 | Envelope/Schema/LengthLimits | Read/Write/Validate | 数据长度/哈希，拒绝错AttemptId |
| TCTResourceGate | 内部/兼容公开 | 持有者状态 | ExclusiveKeys、Cancellation | AcquireAsync→Lease | Resource/Fixture；D07，取消释放准确 |
| TCTTestFixtureService | 内部/兼容公开 | per run/路径根 | Root、Manifest、Hash、Policy | ResolveReadOnly/Verify/RejectTraversal | Fixture；D04/D11 |
| TCTBaselineService | 内部/兼容公开 | per版本Golden | GoldenId、Version、ApprovalAudit | Compare / ProposeUpdate / ApproveWithAudit | 不自动覆盖Expected；D08 |
| TCTTestArtifactStore | 内部/兼容公开 | per Run或Attempt证据根 | Hash、ByteSize、Mime、AttemptId | Save/ReadVerify/SecureRetention | ArtifactRef；D08，篡改拒绝 |
| TCTTestObservation | 兼容现有公开DTO | per Observation只读 | Kind/Title/Details/Origin | 映射到Envelope，旧用例兼容 | ObservationHub；不代表断言通过 |
| TCTObservationEnvelope | 内部与Viewer只读合同 | per事件不可变 | SchemaId/Version、SourceObject、Seq、Correlation、Artifact | Validate/Serialize/Replay | Owner Probe；D08，未知Schema保留数据 |
| TCTTestObserverHub | 内部/兼容公开 | per Run有界序列 | Seq、Capacity、AttemptId | Publish/Snapshot/Seal，溢出明确失败 | Recorder唯一入口；D08 |
| TCTTestCaseResult | 宿主只读 | per Attempt不可变 | Status、Assertions、Diagnostics、Artifacts、HostExit | Consume/Export；不能由Viewer写入 | Runner唯一生成；D05/D07 |
| TCTTestRunSummary | 维护者公开只读 | per Run不可变 | 全部Selected+Filtered/Blocked/Missing、CaseResults、Evidence | Summarize/EvaluateExit | Completion validator；D10 |
| TCTReportWriter | 内部/现有兼容公开 | 静态纯格式转换 | RunSummary、Format、Output | WriteJUnit/Json/Text | 与History共用一个Result真源 |
| TCTCapabilityCatalog | 平台维护者只读 | per官方版本持久快照 | OfficialSpecVersion、Capability/Object/Dimensions | LoadApproved/ValidateIds | 正式规格基线；D09，未核查=Unmapped |
| TCTCapabilityCoverageMatrix | 平台维护者只读 | per规格/Run投影 | ObjectId×八维×AssertionEvidence | EvaluateRequiredDimensions | EvidenceLink；D09，缺维不能Verified |
| ITCTTestViewer | 插件作者公开 | per活动文档WPF状态 | Kind/SupportedSchemaVersion | CanDisplay/Bind/Clear | Result/Artifact只读；D08 |
| TCTTestViewerRegistry | 内部 | 应用生命周期，注册后只读 | Kind→Factory/Schema/Capability | Resolve/NoEvidence/VersionMismatch | DocumentHost；六图共Shell |
| TCTTestCenterViewModel | 内部 | UI窗口生命周期 | Selection/RunId/CommandState/Layout | Select/Run/Cancel/Rerun/Navigate | Coordinator + ResultRepository；不直接执行Case |
| TCTTestCommandRouter | 内部 | UI窗口/活动上下文 | SelectedAttempt/ViewerCapabilities/Permissions | CanExecute/RouteCommand | Dispatcher + safety gate；禁无权限运行 |

### 2.1 编码前必须确定的具体签名（C# 7.3 约束；新签名只作规范草案）

```csharp
// S1，仅追加兼容重载，不破坏原4参Attribute
[TCTTest("F.GEO.RECT.CONTAINS.001")]
public static void RectContains(TCTTestContext context) { /*真实Owner + Assert*/ }

// 异步方法允许static Task(TCTTestContext)，显式禁止async void。
// S3参数方法允许static Task/void(TCTTestContext,TRow)，必须声明版本化CaseSource。
// TCTTestContext (per Attempt):
// AssertTrue(bool,string), AssertFalse(bool,string),
// AssertEqual<T>(T,T,string), AssertNear(double,double,double,string)
// Observe(...), SaveArtifact(byte[],string), CancellationToken, RunId,CaseId,AttemptId.
```

**冲突裁决**：V3.0 Fluent DSL和V3.2 Attribute都存在于历史材料，但只承诺Attribute作为默认公共API。旧4参Attribute和现有Core公开成员只能兼容升级，不新建同名功能类；旧TCTCaseResult必须升级关联CaseId和异常来源，而非复制TCTResultV2。

### 06.2 定义、数据版本、运行身份与事件模型

| 模型 | 单一事实来源 | 必备约束 |
|---|---|---|
| TestId / TestDefinition | 发现快照（程序集+静态方法） | 全局稳定、指纹一致；避免类名重命名影响ID |
| CaseId | TestId + StableRowKey | 重排Dataset行不改ID；RowHash需校验 |
| RunId | 发起一次执行生成 | RunRequest/RunSummary/History共用，不等于AttemptId |
| AttemptId | 每次执行/Retry新生成 | 每次只能一个终态，重复执行保留独立证据 |
| SourceLocation | PDB与真实StackTrace | 不可定位时返回显式Unavailable，不猜测行号 |
| ObservationEnvelope | Recorder/生产Owner适配 | SchemaVersion、Sequence、Timestamp、Origin、Owner、Instance/Correlation、ArtifactRef |
| Result / AssertionEvidence | Runner/AssertionCollector | UI只读，不把演示状态计入PASS |
| CoverageEvidenceLink | 经审核的正式能力表 + Result关联 | 八维Required全实证才Verified，Unmapped不计覆盖 |

### 3.1 250项的实施裁决准则

- **保留现有**：沿用生产或TCT既有类，维持前缀与签名；编译事实须核。  
- **保留扩展**：增量加字段/行为，旧调用路径不破坏；仅一个Owner。  
- **必要新增**：只有前置工作包和用户场景真正要求时再创建，不提前生成空壳。  
- **合并/内聚**：下面一行的“归并去向”为唯一新职责Owner；避免第二个Builder/Runner/Reporter。  
- **暂缓/条件**：按明确触发条件（例如生产跨子方面复用证据）批准，否则不创建。

### 06.3 原250项之外补充的10个必要概念（同一类型体系）

| 名称 | 形态 | Owner | 时序 | 必须承担的唯一职责 |
|---|---|---|---|---|
| TCTCaseSourceAttribute | 特性 | Tests/TestCenter | P1 | 静态版本化Dataset声明：SourceId/Version/RowKey |
| TCTTestSourceAttribute | 特性 | Tests/TestCenter | P1 | 测试类级默认Owner/Group/Kind声明（缺省Unknown） |
| TCTCaseRow | 值对象 | Tests/TestCenter | P1 | 稳定RowKey、数据哈希、Fixture版本 |
| TCTCaseDefinitionFingerprint | 值对象 | Tests/TestCenter | P0 | 程序集MVID/测试Method签名/Source版本/数据源哈希构成的定义指纹 |
| TCTTestSourceLocation | 值对象 | Tests/TestCenter | P0 | 文件/行号/PDB可用性与不可定位原因 |
| TCTTestPreflightResult | 值对象 | Tests/TestCenter | P0 | 预检Allow/Blocked/Unsupported/Denied及原因；不等同Result |
| TCTObservationEnvelope | 值对象 | Tests/TestCenter | P1 | RunId/AttemptId/Seq/SchemaVersion/SourceOwner/Correlation/ArtifactRef |
| TCTCoverageEvidenceLink | 值对象 | Tests/TestCenter | P1 | 官方规格版本×能力×对象×维度×断言/产物的不可变关联 |
| TCTTestRerunRequest | 值对象 | Tests/TestCenter | P1 | 重跑原CaseId/RowKey/Seed/DefinitionHash/FixtureHash |
| TCTTestFailureLocation | 值对象 | Tests/TestCenter | P0 | 断言StackFrame与源代码位置，不伪造行号 |


**避免机械相加**：10个新概念不是自动等于10个新.cs文件；有的仅为现有模型字段/Adapter/值对象。新增行为必须先通过S1～S5反证测试。

### 06.4 典型类型依赖与对象生命周期

```text
[TCTTestAttribute] → Discovery/AttributeAdapter → Descriptor/CatalogSnapshot
                   → TCTTestRunRequest → ExecutionCoordinator
                   → TCTTestHostController → [同EXE Worker]
                   → Context + ITCTTestLifecycle + 真实Owner API
                   → Assertions/Observation/Artifact → immutable Result
                   → Report/History/Coverage → Readonly Viewer

Foundation.Infrastructure (FLD/FCR) ← Mechanism.Runtime (真实按需引用)
Foundation/Mechanism/Professional ↛ Tests/TestCenter
Tests/TestCenter → 真实Owner DLL (按需且无环)
```

### 06.5 250 项类型清单并未消失

**所有250项逐条保留在本文件附录A（唯一记录，不是另一个文档）。** 类名称、原状态、原职责、V3.2/V4.0裁决、归并去向及Owner均完整列示。*候选类型数量不是应当创建的源文件数量；建议以实际使用场景批准新类型。*

### 06.6 工程快照、实例地址与证据格式（V4.1规范性）

```text
MEMEngineeringSession.GetSnapshot()
  -> MEMProjectSnapshot(ProjectId,Revision,Nodes[])
     -> MEMNodeSnapshot(Id,ParentId,ChildIds,StorageRootId,Scope)
                       Visual/References/Properties/Ports/Connections/Aspects
  -> [TestCenter 只读编码：TCTEngineeringSnapshotCodec，非工程文件格式]
  -> TCTObservationEnvelope + SHA256 Payload Artifact + Run/Attempt
  -> Worker/Archive/Replay 校验身份、Schema及Hash
  -> TCTObjectTestView（Owned节点 + 独立引用叶）
       -> Inspector(MRIObjectAddress/InstancePath)
       -> 两个真实 Revision 的 Before/After Diff（不是 Golden 审批）
```

**恒定身份**：`TestId/CaseId/RunId/AttemptId` 属测试；`SourceObjectId/DefinitionAddress/MRIObjectAddress` 属工程定义及实例引用；`RuntimeInstanceId/CorrelationId` 属生产运行/追踪。相同Symbol在两个引用位置不能因共享定义ObjectId而混为同一运行实例，测试结果不允许被Viewer二次判定。

**必备安全反证**：SHA256不符、跨Attempt/跨Run、未知Schema、重复Child、Parent反指不一致、孤儿/循环Ownership、Reference指向缺失目标、外部DTD、过大负载，不可渲染为有效对象树；旧文本 `Observation.Details` 只能以Legacy文本展示。当前Codec若未序列化Assets、RegisteredTypes、Bindings等字段，必须说明该证据Schema的覆盖边界，不得宣称其等同完整MEM持久化快照。

**生产SceneProjection的角色**：坐标/拾取、多个Symbol Reference实例展开来自 `Mechanism.Visual/CxSceneProjection`，不是TestCenter重新实现。Graph/Runtime的真实端到端 SourceMap 与Bridge应由MOL/MOC/MRK生产层提供；TestCenter只消费不可变Projection/Trace/Journal并进行结构化Diff。

## 07 完整运行过程：发现→预检→编排→执行→断言→观察→归档

### 4.1 从发现到Worker的确定性身份

- 编译输入为Tests/TestCenter的`Cases/**/*.cs`，不允许Owner程序集也编译这些测试文件。传统NET48项目须取消原有逐Case `<Compile Include>`避免重复编译。
- 主进程生成CatalogSnapshot和DefinitionFingerprint：`TestId + AssemblyMVID + MethodSignature + FixtureSchema/RowHash + Version`；Worker Hello返回Version/MVID/CaseHash/Arch，校验后执行。Hash是**相等性校验**不是防恶意签名。
- Worker自启动同一`CxVisual.TestCenter.exe --worker`，不新建csproj；进程隔离中传RunId/AttemptId/CaseId/Seed/Timeout/TargetMode，结果协议限大小、Seq连续、回传后校验Attempt身份。
- 选中`--id`必须先Resolve精确TestId/CaseId，再做Safety/Environment Preflight；批量Profile按规则过滤，显式Missing/Filtered/Blocked/Unsupported。
- Worker超时或无法确认子进程树退出：Error/InfrastructureLeak；仅`Process.Kill`不能证明设备或孙进程已安全停止。

### 4.2 生命周期和终态优先级

```text
CatalogSnapshot → ResolvePlan/Selection → Preflight
→ ReserveFixture/ArtifactQuota/ResourceKeys → LaunchWorker/Handshake
→ Setup → Execute/await → Assertion/Observation/Diagnostic → Teardown(always attempt)
→ Drain/VerifyAttemptCompleteness → Release/ProcessExitVerify → Persist
→ ImmutableFinalResult → Status/History/UI/Coverage
```

推荐报告状态分类：NotRun、Running、Passed、Failed、Error、TimedOut、Cancelled、Skipped、Blocked、Unsupported、Inconclusive；这些状态不能统一折算Passed。错误优先级：安全/Worker协议错误、Cleanup失败或不可确认泄漏优先于执行中已经通过的断言。一个Attempt只写一次最终状态；Retry创建新Attempt且保留前次证据。Snapshot读取/Redo/关闭跨线程UI不会重新判定结果。

### 4.3 Fixture/Data/安全边界

- 测试输入、数据源、Golden、输出Artifact隔离；每Attempt有自己Sandbox，外部链接不得导致清理越界。
- 参数化采用StableRowKey与数据Hash，运行批次可固定Seed；数据生成需有可重现的GeneratorVersion。
- 测试专用数据栈/集合只能作为Fixture实现细节，**真正DataSpace/DataHeap/DataStack**继续归Foundation.Data/Mechanism.Data的生产Owner。
- TargetMode默认Virtual/Replay；真实HIL需独立SafetyToken/Endpoint白名单、人工审签和Owner的Interlock证据；不提供“Run All”隐式触发真实设备的路径。

### 07.1 全链路的正常过程与反证边界

| 入口 | 必须拒绝/正确区分的情形 | 可观测结果 |
|---|---|---|
| Discovery | 重复TestId、非法签名、缺源/错版RowKey、泛型开放方法 | DiscoveryError，CLI非0；不沉默Skip |
| Catalog/Select | 明确ID不存在；Profile默认过滤不能覆盖单选 | NotFound/Filtered/Blocked分别归档 |
| Preflight | 没有SDK/GPU、Fixture Hash错、未授权真实设备 | Unsupported/Blocked；不可启动不安全操作 |
| Worker | 指纹/MVID/Schema/RowHash不一致、崩溃/挂死 | Error/TimedOut；不记录伪Passed |
| Lifecycle | Setup失败、Teardown失败、Lease未释放 | Cleanup独立诊断，不保留Passed |
| Artifact | 越界路径/哈希错/容量超限/未知Schema | 诊断和部分证据，不吞数据损失 |
| UI/Viewer | 未接Graph/Channel、Origin未知、PDB缺失 | NoEvidence/Unsupported/SourceUnavailable |
| Coverage | Unmapped、要求八维缺口、环境不可比 | Unmapped/Partial/Blocked，不算Verified |

### 07.2 贯通Developer→Worker→Evidence→Viewer的关键控制点

- **Definition阶段**：TestId唯一 + AssemblyMVID + MethodSig + Fixture Schema/RowHash +版本构成Fingerprint；没有定义一致性不能在子进程执行。
- **Preflight阶段**：TestId被精确选择≠允许危险硬件；安全检查不可被Profile/`--inproc`绕过；Missing/Blocked/Unsupported需明确区分。
- **Execution阶段**：RunId、CaseId、AttemptId分别生成；每次Retry产生全新Attempt，执行前后校验资源租约/超时/取消/Teardown。
- **Evidence阶段**：结果Status来自统一Runner；结构化Observation和Artifact有SchemaVersion、AttemptId、Seq、来源；未知视图必须NoEvidence。
- **Completion阶段**：Runner统一终态后持久化，JUnit/History/ViewModel消费不可变Result；UI点击不会重写通过结果。
- **Security阶段**：Worker强制结束不等于X-Ray或PLC安全停机；实际设备授权和Owner互锁是独立必需项。

### 07.3 从真实生产Owner到可交互工作台的验证路径（V4.1）

真实MEM工程通过Session创建Project/Page/Content/独立Symbol、Group与Element以及资源引用，使用正式事务形成Revision；`GetSnapshot()` 生成不可变工程观察。测试作者NUnit/TCT调用真实生产API并断言，必要时通过 `ObserveStructured` 提交证据，Worker输出Run/Attempt与SHA256 Artifact；Archive/Replay核对Manifest与身份。GUI的 Explorer→Results→Viewer→Inspector→Timeline→历史Diff 必须保持相同的RunId/AttemptId，不能在导航时悄悄切换到另一轮Passed结果。

Graph/Runtime模式必须从生产 **定义→校验/编译→Plan/SourceMap→ExecutionInstance/Journal/Trace** 获取数据；Channel必须读真实或明确声明Virtual的生产帧/Session/Codec，不得凭设计图模拟成功。缺接口时使用NoEvidence/Unsupported并记入工作包。UI的停止仅发送取消请求；Worker退出不等于PLC/Motion/X-Ray物理安全停机。

## 08 统一WPF界面：一个Shell、六种模式、真实数据绑定

### 08.1 六图保留的视觉目标与必须修正处

![六场景总览](https://raw.githubusercontent.com/oudream/cx-visual-x3/master/DOCS/Architecture/TestCenter/CxVisual_TestCenter_六场景_预览总览.png)

原稿可复用的蓝白工业工作台风格：顶部菜单/Context Ribbon、左侧Explorer+Fixture、中央专业工作文档、右侧属性/断言、底部Results/Trace/Timeline/Diff/Perf与状态栏。三列推荐`18% / 62% / 20%`，底部约22%，均需允许拖拽、折叠、DPI缩放及布局持久化。默认不能出现空白大面积黑屏或只见Mock数据。

**旧模型修正**：原03图“SymbolDefinition”等早期名字仅作概念标签，不许导入第二套生产Symbol定义；对象真源遵循后续`Project/Page/Content/Symbol/Group/Element`与Ownership/Reference/Connection严格区分。04图不能重建Graph生产Owner；05图演示Modbus帧不能替代生产协议Adapter；06图模拟等待队列不能替代真实MRK租约与Journal。

### 08.2 统一 Shell（所有场景通用）

| 区域 | 现有代码锚点 | 标准交互/事件 | 数据绑定与空态 | 验收 |
|---|---|---|---|---|
| 主菜单/Context Ribbon | `TCTTestRibbonController` | `RunSelected/RunSuite/Cancel/Rerun/Compare/Export/GoToSource`；按Viewer/Selection/权限贡献选项 | 单一CommandRouter，命令可用性由TCTTestCenterViewModel控制 | U01/U02/U05 |
| 左Explorer | `TCTTestExplorerControl` | Foundation/Mechanism → 方面→能力→对象→用例，文本筛选/Tag/Kind正交过滤、右键Run | CatalogQuery + 选中TestId/CaseId，当前Kind与资源安全状态 | U01/U02 |
| 左下Fixture | 目标`TCTTestFixturePanel` | 版本/路径/数据Rows/Golden审批，最小只读预览 | Manifest/Hash/Version，MissingFixture显式 | U04 |
| 中央DocumentHost | `TCTTestDocumentHostControl` | 按ViewerRegistry开多文档，Tab/Close/Compare/Pan/Zoom；不重建六套Window | 统一ObservationEnvelope Schema，未知或空证据NoEvidence | U03/U06 |
| 右Inspector | `TCTTestInspectorControl` | Property/Assertions/Watch/Source/Payload；点击证据定位到场景对象/时间点 | 与SelectedAttempt保持RunId/AttemptId一致，不串其他轮次 | U05/U06 |
| 下Results | `TCTTestResultsControl` | 测试列表→Failed→Details→Rerun；保留每次Attempt | CaseResult/Assertions/Diagnostics，取消或失败非绿色 | U02/U05 |
| 下诊断Tabs | 目标`TCTTestOutputTabsControl` | Output/Trace/Timeline/Diff/Perf/History tabs切换 | Reader/ArtifactRef/Sequence，事件可分页限流 | U06/U07 |
| 状态栏 | `TCTMainWindow` | RunId/AttemptId/Profile/Target/Progress/Failed/Blocked/CancellationRequested | 不将请求取消当作执行已取消 | U08 |

#### 08.2.1 ViewModel / Selection / 命令的唯一真源

```text
TCTMainWindow
 ├─ TCTTestCenterViewModel (唯一UI会话与Selection)
 │    SelectedTestId / CaseId / RunId / AttemptId / SourceObjectId / ActiveViewer
 ├─ TCTTestCommandRouter → Run / Cancel / Rerun / Compare / GoToSource
 │    └─ Host/Coordinator (执行) → immutable Result
 ├─ TCTTestExplorerControl + Inspector + Results
 └─ TCTTestDocumentHostControl → ViewerRegistry → Five Readonly TCT*TestView
```

- `SelectionChanged(Origin, SelectedItem)`带CorrelationId防止Explorer↔Viewer回选造成事件风暴，所有UI事件通过Dispatcher。切换Viewer不会改变底层Run状态。
- 不存在的Run/Attempt跳转返回“历史不可用”，不悄悄选择另一次通过的Run；未知Schema/Protocol版本显式VersionMismatch。
- 中央Viewer可以建立临时交互Selection与Viewport状态，但不持有可变的生产Owner对象作为运行结果唯一真源。
- 关闭/布局持久化只保存UI首选项，不保存设备凭据或更改历史Result。

#### 08.2.2 Context Ribbon 命令表

| 命令 | 条件 | 实际作用 | 禁止/错误行为 |
|---|---|---|---|
| RunSelected | 单个Test/Case存在且预检通过 | 创建新Run及Attempt | 不因默认Smoke把显式选中的非Unit过滤成空 |
| RunSuite/RunPlan | 已审Plan及依赖满足 | 冻结请求、运行预检、调度 | 不绕过安全/SDK/Fixture前置条件 |
| Cancel | RunState=Running或允许取消 | 发CancellationRequested到Coordinator | 不立刻涂绿/灰为已结束，不将Worker Kill当设备安全停机 |
| RerunFailed / RerunAttempt | 旧Attempt/定义/FixtureHash可重建 | 新Run/新Attempt，携带ParentAttemptId | 不覆盖历史、未核版本先说明不可重现 |
| Compare | 两个有Schema/环境/版本元数据的Run/Artifact | Diff服务，可声明不可比 | 不用缩略图外观代替断言 |
| GoToSource | Stack/PDB可用 | 精确到File/Line/Method | 没符号不猜行号 |
| OpenTimeline | 已有真实事件流 | 展示按Seq排序的时间线 | 不能用Mock时间轴冒充真实Journal |
| Export | Run已完成且结果可读 | JUnit/JSON/Text/ArtifactManifest | 不能更改Run的最终状态 |
| UpdateGolden | 审签权限、审查差异、版本有效 | 提交审核后的Golden申请 | 单击自动覆盖Expected属禁止行为 |
| RealHardware | 明确授权+设备Owner联锁已证实 | 仅提交受控动作测试 | 默认禁用，虚拟/重放为正常默认 |

### 08.3 六种场景逐张映射

#### 界面01 测试主界面（Dashboard）

![01 测试主界面](https://raw.githubusercontent.com/oudream/cx-visual-x3/master/DOCS/Architecture/TestCenter/01_TestCenter_测试主界面_2560x1440.png)

- **进入**：Dashboard中点击相关能力/测试、Explorer双击、Result/Diagnostic跳转，打开同一DocumentHost内的专用Tab；不是新主窗体。
- **Viewer锚点**：`TCTMainWindow`；复用/补充组件：TCTTestExplorerControl、TCTTestResultsControl、TCTTestInspectorControl。
- **真实数据契约**：CatalogSnapshot/RunSummary/Plan/Coverage/History；每份数据需RunId、AttemptId、SchemaVersion、Origin、SourceObjectId和时间/序号（大对象ArtifactRef）。
- **用户操作闭环**：筛选→查看结果→选定Run/Attempt→比较/报告；点击断言→定位对应对象/场景/源代码，点击对象→反查Assertion Evidence。
- **实现差距/硬要求**：切换Run、历史、重跑、Progress/Blocked统计、正式覆盖可解释。
- **拒绝状态**：NotRun→提示运行；Missing/Unsupported→显示Provider原因；Error→诊断；NoEvidence→只显示字段说明，不渲染虚假图形；VersionMismatch→拒绝解释未知Schema。
- **验收**：一项真实Owner输入→失败/正常各一次→精确Attempt选择→Viewport→返回Results→历史重开不变；截图必须附RunId/Commit而非仅设计稿。

#### 界面02 Foundation Geometry（Geometry）

![02 Foundation Geometry](https://raw.githubusercontent.com/oudream/cx-visual-x3/master/DOCS/Architecture/TestCenter/02_Foundation_Geometry_2560x1440.png)

- **进入**：Dashboard中点击相关能力/测试、Explorer双击、Result/Diagnostic跳转，打开同一DocumentHost内的专用Tab；不是新主窗体。
- **Viewer锚点**：`TCTGeometryTestView`；复用/补充组件：TCTTestDocumentHostControl、GeometryViewport、Inspector。
- **真实数据契约**：GeometryObservation(Expected/Actual/Transform/Delta/CoordinateUnits)；每份数据需RunId、AttemptId、SchemaVersion、Origin、SourceObjectId和时间/序号（大对象ArtifactRef）。
- **用户操作闭环**：从Explorer选择几何Case→Run→实际坐标→容差比较→源码定位；点击断言→定位对应对象/场景/源代码，点击对象→反查Assertion Evidence。
- **实现差距/硬要求**：平移/缩放/标尺、图层、精确点坐标、NaN/Infinity、误差矢量与Diff。
- **拒绝状态**：NotRun→提示运行；Missing/Unsupported→显示Provider原因；Error→诊断；NoEvidence→只显示字段说明，不渲染虚假图形；VersionMismatch→拒绝解释未知Schema。
- **验收**：一项真实Owner输入→失败/正常各一次→精确Attempt选择→Viewport→返回Results→历史重开不变；截图必须附RunId/Commit而非仅设计稿。

#### 界面03 Content/Symbol对象组织（ObjectTree）

![03 Content/Symbol对象组织](https://raw.githubusercontent.com/oudream/cx-visual-x3/master/DOCS/Architecture/TestCenter/03_Mechanism_Content_Symbol_对象组织_2560x1440.png)

- **进入**：Dashboard中点击相关能力/测试、Explorer双击、Result/Diagnostic跳转，打开同一DocumentHost内的专用Tab；不是新主窗体。
- **Viewer锚点**：`TCTObjectTestView`；复用/补充组件：Tree/ReferenceGraph/PropertyInspector。
- **真实数据契约**：Owner定义树/Ownership/Reference/ContentRef/SymbolRef/Revision；每份数据需RunId、AttemptId、SchemaVersion、Origin、SourceObjectId和时间/序号（大对象ArtifactRef）。
- **用户操作闭环**：对象Case→投影树→选中引用→显示目标+断言→对照；点击断言→定位对应对象/场景/源代码，点击对象→反查Assertion Evidence。
- **实现差距/硬要求**：禁止把Reference误画Ownership；Symbol由Group组成；失效Ref/循环Ownership。
- **拒绝状态**：NotRun→提示运行；Missing/Unsupported→显示Provider原因；Error→诊断；NoEvidence→只显示字段说明，不渲染虚假图形；VersionMismatch→拒绝解释未知Schema。
- **验收**：一项真实Owner输入→失败/正常各一次→精确Attempt选择→Viewport→返回Results→历史重开不变；截图必须附RunId/Commit而非仅设计稿。

#### 界面04 Graph/Port/Execution（Graph）

![04 Graph/Port/Execution](https://raw.githubusercontent.com/oudream/cx-visual-x3/master/DOCS/Architecture/TestCenter/04_Mechanism_Graph_Port_Execution_2560x1440.png)

- **进入**：Dashboard中点击相关能力/测试、Explorer双击、Result/Diagnostic跳转，打开同一DocumentHost内的专用Tab；不是新主窗体。
- **Viewer锚点**：`TCTGraphTestView`；复用/补充组件：GraphCanvas/Diagnostics/RuntimeOverlay。
- **真实数据契约**：Orchestration Definition + TypedPorts + CompileDiagnostics + MRKTrace；每份数据需RunId、AttemptId、SchemaVersion、Origin、SourceObjectId和时间/序号（大对象ArtifactRef）。
- **用户操作闭环**：编排Definition→Validate→Resolve→Compile→Plan→Runtime关联；点击断言→定位对应对象/场景/源代码，点击对象→反查Assertion Evidence。
- **实现差距/硬要求**：当前MRK编译有实现；通用Orchestration图对象仍需要Owner，未实现时显示缺口。
- **拒绝状态**：NotRun→提示运行；Missing/Unsupported→显示Provider原因；Error→诊断；NoEvidence→只显示字段说明，不渲染虚假图形；VersionMismatch→拒绝解释未知Schema。
- **验收**：一项真实Owner输入→失败/正常各一次→精确Attempt选择→Viewport→返回Results→历史重开不变；截图必须附RunId/Commit而非仅设计稿。

#### 界面05 Channel/Protocol（Channel）

![05 Channel/Protocol](https://raw.githubusercontent.com/oudream/cx-visual-x3/master/DOCS/Architecture/TestCenter/05_Mechanism_Channel_Protocol_2560x1440.png)

- **进入**：Dashboard中点击相关能力/测试、Explorer双击、Result/Diagnostic跳转，打开同一DocumentHost内的专用Tab；不是新主窗体。
- **Viewer锚点**：`TCTChannelTestView`；复用/补充组件：TX/RX Timeline/HexView/DecodedFields/SessionInspector。
- **真实数据契约**：生产Channel/Codec/Protocol/Frame + Correlation + Error；每份数据需RunId、AttemptId、SchemaVersion、Origin、SourceObjectId和时间/序号（大对象ArtifactRef）。
- **用户操作闭环**：虚拟Session→发送/接收→错误注入→观察原始帧及字段；点击断言→定位对应对象/场景/源代码，点击对象→反查Assertion Evidence。
- **实现差距/硬要求**：没有真实Provider禁止拿图片中Modbus模拟帧当实际TX/RX。
- **拒绝状态**：NotRun→提示运行；Missing/Unsupported→显示Provider原因；Error→诊断；NoEvidence→只显示字段说明，不渲染虚假图形；VersionMismatch→拒绝解释未知Schema。
- **验收**：一项真实Owner输入→失败/正常各一次→精确Attempt选择→Viewport→返回Results→历史重开不变；截图必须附RunId/Commit而非仅设计稿。

#### 界面06 Runtime/Resource（Runtime）

![06 Runtime/Resource](https://raw.githubusercontent.com/oudream/cx-visual-x3/master/DOCS/Architecture/TestCenter/06_Mechanism_Runtime_Resource_Timeline_2560x1440.png)

- **进入**：Dashboard中点击相关能力/测试、Explorer双击、Result/Diagnostic跳转，打开同一DocumentHost内的专用Tab；不是新主窗体。
- **Viewer锚点**：`TCTRuntimeTestView`；复用/补充组件：ExecutionTree/LeaseQueue/Timeline/Journal/Trace。
- **真实数据契约**：MRKExecutionInstance/Trace/Journal/Lease/Unknown/WaitingReason；每份数据需RunId、AttemptId、SchemaVersion、Origin、SourceObjectId和时间/序号（大对象ArtifactRef）。
- **用户操作闭环**：Run/Attempt→ExecutionInstance→Timeline→事件详情→Journal/Replay；点击断言→定位对应对象/场景/源代码，点击对象→反查Assertion Evidence。
- **实现差距/硬要求**：对Unknown、Timeout、Recovery、资源等待显示事实，不靠节点颜色推断运行结果。
- **拒绝状态**：NotRun→提示运行；Missing/Unsupported→显示Provider原因；Error→诊断；NoEvidence→只显示字段说明，不渲染虚假图形；VersionMismatch→拒绝解释未知Schema。
- **验收**：一项真实Owner输入→失败/正常各一次→精确Attempt选择→Viewport→返回Results→历史重开不变；截图必须附RunId/Commit而非仅设计稿。

### 08.4 结构化Observation/Viewport合同

```text
TCTObservationEnvelope(
  RunId, CaseId, AttemptId, SchemaId, SchemaVersion, SourceOwner,
  SourceObjectId, DefinitionAddress?, RuntimeInstanceId?, CorrelationId?,
  TimestampUtc, Sequence, Origin=[Real|Virtual|Replay], PayloadFormat,
  DataRef?, ArtifactRef?, DiagnosticRef?
)
   ├─ GeometryPayload → ProjectedGeometry / ExpectedActual / Tolerance / Delta
   ├─ ObjectPayload → TreeNode / OwnershipEdge / ReferenceEdge / SymbolRef / ContentRef
   ├─ GraphPayload → DefinitionNode / TypedPort / CompileDiagnostic / RuntimeOverlay
   ├─ ChannelPayload → TxRxFrame / CodecDecode / Session / ProtocolError
   └─ RuntimePayload → ExecutionTree / Lease / WaitingReason / Journal / Timeline
```

- 生产方只提供其真实对象/Trace/Protocol事件，不依赖TestCenter的TCT封套；**Probe**位于Tests转换层。
- 使用稳定SchemaId+Version；未知版必显式拒绝或降级文本，不能把字段猜成设备状态。
- Event Seq per Attempt单调递增；UI按虚拟化、背压、增量窗口处理多万条事件，禁止主线程同步读GB级Artifact。
- Geometry变换对应一个明确坐标系和单位；Expected/Actual/Delta分层，PSNR/Volume等有独立语义而非仅点/线演示。
- Selection保持RunId/AttemptId关联，Viewer子选择（Point/Node/Frame/Lease）不覆盖全局TestResult状态。

### 08.5 六场景以外的同Shell文档/工具窗

不能把六图当作完整测试功能分类，追加可复用工作文档：

| 工作文档/面板 | 目的 | 交互/验收 |
|---|---|---|
| Test Plan Editor | 无C#管理Suite/Plan/依赖图与Run Profile | 引不存在Case/循环依赖拒绝，已审计划可保存重载 |
| Fixture/Dataset Explorer | 数据版本/参数行/Golden审批 | 稳定RowKey、MissingFixture、Hash/审批审计 |
| Coverage Matrix | 规格版本×Owner×能力对象×8维RequirementEvidence | Unmapped和Required缺项显示Partial，不显示虚假百分百 |
| Run History/Diff | 按Commit/RunId/Environment比对 | 版本或设备环境不可比有明确原因 |
| Benchmark Dashboard | P50/P95/P99、内存、吞吐、样本量与环境 | 样本不足或环境不可比不产生“性能提升”结论 |
| Safety/Host Monitor | Worker状态、资源/进程残留、Real硬件授权 | 非授权动作受阻，Worker退出≠物理安全停机 |

### 08.6 设计图到实际代码的验收索引（手工+自动）

| ID | 行为验收 | 正例/反例 |
|---|---|---|
| U01 | 一个Shell在Dashboard→各Viewer间切换 | 切换不产生额外Runner；窗口复用 |
| U02 | Explorer按Owner树/Kind筛选并显式单项Run | 非Unit选中不因Smoke空转；未授权Blocked |
| U03 | DocumentHost 多Tab + ViewerRegistry | 没有Provider显示NoEvidence，未知Schema不崩溃 |
| U04 | Fixture/Golden交互 | Hash篡改/缺Fixture可见，审批前不能更新 |
| U05 | Assertion→源码定位→原数据重跑 | PDB有/无两路，历史Attempt不变 |
| U06 | 三类结构化Viewer Geometry/Object/Runtime | 每个真实Observation能回溯到Owner/Run/Attempt |
| U07 | Graph/Channel实时/回放 | 真Provider接入前显示Unsupported；不可演示假PASS |
| U08 | 取消/超时/关闭窗口/资源释放 | 不误报状态，后台Worker可回收且有证据 |
| U09 | Coverage/History/Diff | Required缺项Partial、跨Commit不可比 |
| U10 | DPI/键盘操作/无障碍/大数据虚拟化 | 125%-200%缩放、只读多Tab不卡主线程 |

### 08.7 WPF具体落地顺序与门槛

1. UI0：现有MainWindow控件绑定统一ViewModel，Selection、命令可用性与错误/空态齐备；不得重写Runner。
2. UI1：Schema化Observation/ViewerRegistry；先Geometry和Object的实际图形与选择交互，再Runtime Timeline/Journal。
3. UI2：Fixture/Plan/History/Coverage工具文档；完善Source定位与历史Diff。
4. UI3：随着Mechanism.Orchestration、Connectivity生产Provider成熟接入Graph/Channel；不能由UI自行伪造业务节点/报文。
5. UI4：大数据虚拟化、DPI/键盘操作、UI自动化、性能和长期稳定性；Windows x64屏幕捕获的真实运行证据与原设计图逐项对照。

所有UI任务同时具备：`TCT*`类名/接口`ITCT*`、唯一Window、Owner无反向引用、同一Run/Attempt上下文、真实数据显示来源、正确状态和UI/CI验收证据。

### 08.8 六场景全控件操作—来源—验收映射（完整42项）

| 图序 | 界面 | 区域/控件 | 工作行为 | 类/接口锚点 | 功能/交互差距 | 真实数据来源 | 验收编号 | 工作包 | 现状可核 | 待验收 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 01 | 测试主界面 | 共用/菜单/Ribbon | Run/Cancel/Rerun/Compare/Source | TCTTestRibbonController | 统一CommandRouter和安全预检 | CatalogSnapshot/RunSummary/Plan/Coverage/History | U01/U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 01 | 测试主界面 | 共用/Test Explorer | 按Owner/Capability/Case/Tag/Kind筛选 | TCTTestExplorerControl | 正式能力树/查询，精确选择单选 | CatalogSnapshot/RunSummary/Plan/Coverage/History | U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 01 | 测试主界面 | 共用/Test DocumentHost | 多Tab专业Viewer宿主 | TCTTestDocumentHostControl | Registry/Schema/NoEvidence/Selection | CatalogSnapshot/RunSummary/Plan/Coverage/History | U03 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 01 | 测试主界面 | 共用/Inspector | 属性/断言/Watch/源码 | TCTTestInspectorControl | AssertionId/AttemptId关联/错误详情 | CatalogSnapshot/RunSummary/Plan/Coverage/History | U05/U06 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 01 | 测试主界面 | 共用/Results/Timeline | 结果、Trace、Timeline、Diff、Perf | TCTTestResultsControl | 底部工具页、虚拟化、历史回放 | CatalogSnapshot/RunSummary/Plan/Coverage/History | U05/U08 | S4-S5 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 01 | 测试主界面 | 共用/Fixture面板 | Dataset、Golden、审批、版本 | 目标TCTTestFixturePanel | 只读数据、明确审批记录 | CatalogSnapshot/RunSummary/Plan/Coverage/History | U04 | S3-S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 01 | 测试主界面 | 专业Viewer | 筛选→查看结果→选定Run/Attempt→比较/报告 | TCTMainWindow | 切换Run、历史、重跑、Progress/Blocked统计、正式覆盖可解释 | CatalogSnapshot/RunSummary/Plan/Coverage/History | U06/U07 | S4-S5 | 有现有TCT Viewer类 | 专业结构化绘制待实现 |
| 02 | Foundation Geometry | 共用/菜单/Ribbon | Run/Cancel/Rerun/Compare/Source | TCTTestRibbonController | 统一CommandRouter和安全预检 | GeometryObservation(Expected/Actual/Transform/Delta/CoordinateUnits) | U01/U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 02 | Foundation Geometry | 共用/Test Explorer | 按Owner/Capability/Case/Tag/Kind筛选 | TCTTestExplorerControl | 正式能力树/查询，精确选择单选 | GeometryObservation(Expected/Actual/Transform/Delta/CoordinateUnits) | U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 02 | Foundation Geometry | 共用/Test DocumentHost | 多Tab专业Viewer宿主 | TCTTestDocumentHostControl | Registry/Schema/NoEvidence/Selection | GeometryObservation(Expected/Actual/Transform/Delta/CoordinateUnits) | U03 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 02 | Foundation Geometry | 共用/Inspector | 属性/断言/Watch/源码 | TCTTestInspectorControl | AssertionId/AttemptId关联/错误详情 | GeometryObservation(Expected/Actual/Transform/Delta/CoordinateUnits) | U05/U06 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 02 | Foundation Geometry | 共用/Results/Timeline | 结果、Trace、Timeline、Diff、Perf | TCTTestResultsControl | 底部工具页、虚拟化、历史回放 | GeometryObservation(Expected/Actual/Transform/Delta/CoordinateUnits) | U05/U08 | S4-S5 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 02 | Foundation Geometry | 共用/Fixture面板 | Dataset、Golden、审批、版本 | 目标TCTTestFixturePanel | 只读数据、明确审批记录 | GeometryObservation(Expected/Actual/Transform/Delta/CoordinateUnits) | U04 | S3-S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 02 | Foundation Geometry | 专业Viewer | 从Explorer选择几何Case→Run→实际坐标→容差比较→源码定位 | TCTGeometryTestView | 平移/缩放/标尺、图层、精确点坐标、NaN/Infinity、误差矢量与Diff | GeometryObservation(Expected/Actual/Transform/Delta/CoordinateUnits) | U06/U07 | S4-S5 | 有现有TCT Viewer类 | 专业结构化绘制待实现 |
| 03 | Content/Symbol对象组织 | 共用/菜单/Ribbon | Run/Cancel/Rerun/Compare/Source | TCTTestRibbonController | 统一CommandRouter和安全预检 | Owner定义树/Ownership/Reference/ContentRef/SymbolRef/Revision | U01/U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 03 | Content/Symbol对象组织 | 共用/Test Explorer | 按Owner/Capability/Case/Tag/Kind筛选 | TCTTestExplorerControl | 正式能力树/查询，精确选择单选 | Owner定义树/Ownership/Reference/ContentRef/SymbolRef/Revision | U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 03 | Content/Symbol对象组织 | 共用/Test DocumentHost | 多Tab专业Viewer宿主 | TCTTestDocumentHostControl | Registry/Schema/NoEvidence/Selection | Owner定义树/Ownership/Reference/ContentRef/SymbolRef/Revision | U03 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 03 | Content/Symbol对象组织 | 共用/Inspector | 属性/断言/Watch/源码 | TCTTestInspectorControl | AssertionId/AttemptId关联/错误详情 | Owner定义树/Ownership/Reference/ContentRef/SymbolRef/Revision | U05/U06 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 03 | Content/Symbol对象组织 | 共用/Results/Timeline | 结果、Trace、Timeline、Diff、Perf | TCTTestResultsControl | 底部工具页、虚拟化、历史回放 | Owner定义树/Ownership/Reference/ContentRef/SymbolRef/Revision | U05/U08 | S4-S5 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 03 | Content/Symbol对象组织 | 共用/Fixture面板 | Dataset、Golden、审批、版本 | 目标TCTTestFixturePanel | 只读数据、明确审批记录 | Owner定义树/Ownership/Reference/ContentRef/SymbolRef/Revision | U04 | S3-S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 03 | Content/Symbol对象组织 | 专业Viewer | 对象Case→投影树→选中引用→显示目标+断言→对照 | TCTObjectTestView | 禁止把Reference误画Ownership；Symbol由Group组成；失效Ref/循环Ownership | Owner定义树/Ownership/Reference/ContentRef/SymbolRef/Revision | U06/U07 | S4-S5 | 有现有TCT Viewer类 | 专业结构化绘制待实现 |
| 04 | Graph/Port/Execution | 共用/菜单/Ribbon | Run/Cancel/Rerun/Compare/Source | TCTTestRibbonController | 统一CommandRouter和安全预检 | Orchestration Definition + TypedPorts + CompileDiagnostics + MRKTrace | U01/U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 04 | Graph/Port/Execution | 共用/Test Explorer | 按Owner/Capability/Case/Tag/Kind筛选 | TCTTestExplorerControl | 正式能力树/查询，精确选择单选 | Orchestration Definition + TypedPorts + CompileDiagnostics + MRKTrace | U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 04 | Graph/Port/Execution | 共用/Test DocumentHost | 多Tab专业Viewer宿主 | TCTTestDocumentHostControl | Registry/Schema/NoEvidence/Selection | Orchestration Definition + TypedPorts + CompileDiagnostics + MRKTrace | U03 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 04 | Graph/Port/Execution | 共用/Inspector | 属性/断言/Watch/源码 | TCTTestInspectorControl | AssertionId/AttemptId关联/错误详情 | Orchestration Definition + TypedPorts + CompileDiagnostics + MRKTrace | U05/U06 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 04 | Graph/Port/Execution | 共用/Results/Timeline | 结果、Trace、Timeline、Diff、Perf | TCTTestResultsControl | 底部工具页、虚拟化、历史回放 | Orchestration Definition + TypedPorts + CompileDiagnostics + MRKTrace | U05/U08 | S4-S5 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 04 | Graph/Port/Execution | 共用/Fixture面板 | Dataset、Golden、审批、版本 | 目标TCTTestFixturePanel | 只读数据、明确审批记录 | Orchestration Definition + TypedPorts + CompileDiagnostics + MRKTrace | U04 | S3-S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 04 | Graph/Port/Execution | 专业Viewer | 编排Definition→Validate→Resolve→Compile→Plan→Runtime关联 | TCTGraphTestView | 当前MRK编译有实现；通用Orchestration图对象仍需要Owner，未实现时显示缺口 | Orchestration Definition + TypedPorts + CompileDiagnostics + MRKTrace | U06/U07 | S4-S5 | 有现有TCT Viewer类 | 专业结构化绘制待实现 |
| 05 | Channel/Protocol | 共用/菜单/Ribbon | Run/Cancel/Rerun/Compare/Source | TCTTestRibbonController | 统一CommandRouter和安全预检 | 生产Channel/Codec/Protocol/Frame + Correlation + Error | U01/U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 05 | Channel/Protocol | 共用/Test Explorer | 按Owner/Capability/Case/Tag/Kind筛选 | TCTTestExplorerControl | 正式能力树/查询，精确选择单选 | 生产Channel/Codec/Protocol/Frame + Correlation + Error | U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 05 | Channel/Protocol | 共用/Test DocumentHost | 多Tab专业Viewer宿主 | TCTTestDocumentHostControl | Registry/Schema/NoEvidence/Selection | 生产Channel/Codec/Protocol/Frame + Correlation + Error | U03 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 05 | Channel/Protocol | 共用/Inspector | 属性/断言/Watch/源码 | TCTTestInspectorControl | AssertionId/AttemptId关联/错误详情 | 生产Channel/Codec/Protocol/Frame + Correlation + Error | U05/U06 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 05 | Channel/Protocol | 共用/Results/Timeline | 结果、Trace、Timeline、Diff、Perf | TCTTestResultsControl | 底部工具页、虚拟化、历史回放 | 生产Channel/Codec/Protocol/Frame + Correlation + Error | U05/U08 | S4-S5 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 05 | Channel/Protocol | 共用/Fixture面板 | Dataset、Golden、审批、版本 | 目标TCTTestFixturePanel | 只读数据、明确审批记录 | 生产Channel/Codec/Protocol/Frame + Correlation + Error | U04 | S3-S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 05 | Channel/Protocol | 专业Viewer | 虚拟Session→发送/接收→错误注入→观察原始帧及字段 | TCTChannelTestView | 没有真实Provider禁止拿图片中Modbus模拟帧当实际TX/RX | 生产Channel/Codec/Protocol/Frame + Correlation + Error | U06/U07 | S4-S5 | 有现有TCT Viewer类 | 专业结构化绘制待实现 |
| 06 | Runtime/Resource | 共用/菜单/Ribbon | Run/Cancel/Rerun/Compare/Source | TCTTestRibbonController | 统一CommandRouter和安全预检 | MRKExecutionInstance/Trace/Journal/Lease/Unknown/WaitingReason | U01/U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 06 | Runtime/Resource | 共用/Test Explorer | 按Owner/Capability/Case/Tag/Kind筛选 | TCTTestExplorerControl | 正式能力树/查询，精确选择单选 | MRKExecutionInstance/Trace/Journal/Lease/Unknown/WaitingReason | U02 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 06 | Runtime/Resource | 共用/Test DocumentHost | 多Tab专业Viewer宿主 | TCTTestDocumentHostControl | Registry/Schema/NoEvidence/Selection | MRKExecutionInstance/Trace/Journal/Lease/Unknown/WaitingReason | U03 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 06 | Runtime/Resource | 共用/Inspector | 属性/断言/Watch/源码 | TCTTestInspectorControl | AssertionId/AttemptId关联/错误详情 | MRKExecutionInstance/Trace/Journal/Lease/Unknown/WaitingReason | U05/U06 | S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 06 | Runtime/Resource | 共用/Results/Timeline | 结果、Trace、Timeline、Diff、Perf | TCTTestResultsControl | 底部工具页、虚拟化、历史回放 | MRKExecutionInstance/Trace/Journal/Lease/Unknown/WaitingReason | U05/U08 | S4-S5 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 06 | Runtime/Resource | 共用/Fixture面板 | Dataset、Golden、审批、版本 | 目标TCTTestFixturePanel | 只读数据、明确审批记录 | MRKExecutionInstance/Trace/Journal/Lease/Unknown/WaitingReason | U04 | S3-S4 | 共有控件原型已部分具备 | 结构数据与交互仍待验收 |
| 06 | Runtime/Resource | 专业Viewer | Run/Attempt→ExecutionInstance→Timeline→事件详情→Journal/Replay | TCTRuntimeTestView | 对Unknown、Timeout、Recovery、资源等待显示事实，不靠节点颜色推断运行结果 | MRKExecutionInstance/Trace/Journal/Lease/Unknown/WaitingReason | U06/U07 | S4-S5 | 有现有TCT Viewer类 | 专业结构化绘制待实现 |

**V4.1图纸位置**：六张2560×1440图和一张总览均在仓库 `DOCS/Architecture/TestCenter/`，上方的图像链接已使用仓库真实资源地址；独立Markdown亦可查看。图只是设计参考，不是WPF真实运行的验收截图。

### 08.9 单一WPF Shell六场景功能与UI验收基线（V4.1）

**只有一个界面项目**：`Tests/TestCenter/CxVisual.TestCenter.csproj`，一个 `TCTMainWindow`。顶部主菜单/动态Context Ribbon、左TestExplorer/Fixture、中DocumentHost专业Viewer、右Inspector/证据、底部Results/Output/Trace/Timeline/Diff/Perf/History与状态栏。工具窗允许多Tab/拖动停靠/布局持久化，界面数据从一个稳定UI Selection/CommandRouter与不可变TestResult读取；不能为了六场景再复制六个Runner或主窗口。

| 模式 | 已有可复用能力（代码存在≠完整验收） | 仍需要完整实现/验收 |
|---|---|---|
| 01 Dashboard/概览 | Catalog、NUnit/TCT结果、WPF Shell、Inspector/Results/Timeline基本绑定 | 真正动态Ribbon、Owner/Capability树、Suite/Plan入口、历史、运行统计、失败重跑、布局保存与交互UIA |
| 02 Geometry | `CxRect2D/CxAffine2D`真实算法用例及基础Viewer | 标准Geometry Payload，平移/缩放/标尺/坐标、Expected/Actual/Delta/容差、图层/选择、异常坐标及源码关联 |
| 03 ObjectTree | 真实MEM Snapshot、Ownership/Reference、节点/引用选择到Inspector、两Revision只读Diff | ReferenceGraph全视觉关系、Content/Symbol实例与连接图、深层高亮/双向定位、历史快照可比性、性能与完整UIA |
| 04 Graph/Port | Viewer入口及生产MOL/MPC定义/部分MRK测试 | 实际TypedPort/Connection/CompileDiagnostic/SourceMap、可交互GraphCanvas、Plan/Runtime Overlay、错误源追踪 |
| 05 Channel/Protocol | Viewer入口、缺Provider时NoEvidence | 真实TX/RX帧/Hex/DecodedFields/Session/Correlation、Virtual注入/回放、错帧反证与可用性诊断 |
| 06 Runtime/Resource | 真实MRK部分Journal/Lease/Trace测试、Timeline基础 | ExecutionInstance/ResourceLease/Waiting/Unknown/Recovery/Journaling与Graph SourceMap双向链接、长数据虚拟化 |

**操作链必须完整**：选择真实Case→预检→Run→Results选Attempt→DocumentHost打开适配Viewer→点图元/对象/事件→Inspector定位该对象、Assertion和SourceLocation→切换历史版本→Compare→Rerun生成新Attempt。任一步断链、未知Schema、无Production Provider、缺PDB都必须给可解释空态；无证据的Tab**不得计为已装载真实Viewer**，不能给伪PASS。所有UI跨Dispatcher线程，长文件/GB Artifact不能同步阻塞WPF主线程。

**完整UIA和人工分层**：`--ui-smoke` 仅证明真实WPF控件的受控事件/绑定/跨Worker回显，**不是**Windows鼠标/键盘UI Automation的可用性、DPI 100～200%交互、停靠拖拽、屏幕截图逐张对照及人工使用验收。六图相关U01～U10均须保留自动和人工证据；真实设备独立HIL授权和现场签认。

## 09 正式能力八维覆盖、开发者与界面验收

### 09.1 正式能力覆盖率判定与测试通过率严格分离

**正式覆盖率**：`Workbench → ProfessionalArea → CapabilityArea → CapabilityId → ObjectId → RequirementDimension → TestId → CaseId → AttemptId → AssertionEvidence / Artifact`。

维度：**合同、正常、边界、异常、并发、生命周期、版本、性能**。每个维度必须明确 `Required / Optional / NA（附理由）`，只有**所有Required维度都有与正式Capability/Object/SpecVersion相匹配的有效证据**才标记Verified。缺项Partial，未映射Unmapped，未执行NotRun，安全或依赖不满足Blocked。通过62个TestCase不等于62个能力对象Verified。Excel原工作表中的 `ROW-xx-x` 是**索引号**而非正式能力ID。

### 09.2 开发者体验 D01～D12 验收门（逐项）

| 验收ID | 用户任务 | 必须拿到的证据 | 执行方式 | V4.0历史审查状态（非V4.1实时状态） | 优先工作包 | 规范来源 |
| --- | --- | --- | --- | --- | --- | --- |
| D01 | 新增一个同步方法 | 只增源文件，编译自动包含、GUI/CLI/Worker对同一TestId可发现 | 需真实执行/CI证据 | 未按V4完整验收 | S1 | V3.2 6.1 |
| D02 | 异步Task自然运行 | 真实await、不阻塞Dispatcher、Task取消与原始异常正确映射 | 需真实执行/CI证据 | 未按V4完整验收 | S1 | V3.2 6.1 |
| D03 | 精确运行被选中Case | 非Unit类型也运行，或明确被安全预检拒绝，绝不静默零Case | 需真实执行/CI证据 | 未按V4完整验收 | S1 | V3.2 6.1 |
| D04 | 参数100行 | 100个CaseId/AttemptResult，RowKey稳定、单行重跑一致 | 需真实执行/CI证据 | 未按V4完整验收 | S3 | V3.2 6.1 |
| D05 | 失败可定位 | 故意断言错值、Expected/Actual/Stack/源码若PDB可用能定位，CLI非零 | 需真实执行/CI证据 | 未按V4完整验收 | S1 | V3.2 6.1 |
| D06 | Worker定义一致 | MVID/DefinitionFingerprint/RowHash匹配，数据或代码被替换时拒绝 | 需真实执行/CI证据 | 未按V4完整验收 | S2 | V3.2 6.1 |
| D07 | 生命周期与资源 | Setup/Teardown总会按规执行，错误与资源释放证据，不能假绿 | 需真实执行/CI证据 | 未按V4完整验收 | S3 | V3.2 6.1 |
| D08 | 结构化观察 | 实际Owner→Envelope→Artifact→Viewer；未知Schema安全降级，不能改PASS | 需真实执行/CI证据 | 未按V4完整验收 | S4 | V3.2 6.1 |
| D09 | 能力正式覆盖 | 未审核ID=Unmapped，合同8维缺项=Partial且可定位哪些缺 | 需真实执行/CI证据 | 未按V4完整验收 | S4 | V3.2 6.1 |
| D10 | CLI与CI | 指定ID/批量Profile一致，0测试/不支持/超时均按规则退出非0 | 需真实执行/CI证据 | 未按V4完整验收 | S1 | V3.2 6.1 |
| D11 | 真实设备安全 | 未授权真实Motion/X-Ray测试被阻断，不以Kill进程视为安全保障 | 需真实执行/CI证据 | 未按V4完整验收 | S3 | V3.2 6.1 |
| D12 | 新增、移动、删除源码 | 无残留注册、重ID显式失败，csproj目录规则可重复验证 | 需真实执行/CI证据 | 未按V4完整验收 | S1 | V3.2 6.1 |

### 09.3 WPF界面 U01～U10 真实交互验收（逐项）

| 验收ID | 工作路径 | 负例/边界 | 阶段 | 状态 |
| --- | --- | --- | --- | --- |
| U01 | 一个Shell六种模式/布局 | 切换时不建立第二Runner或主窗 | S4 | 目标未验 |
| U02 | Explorer精确选择/安全检查 | Benchmark/非Unit单选不静默过滤 | S1/S4 | 目标未验 |
| U03 | DocumentHost+ViewerRegistry | 未知Schema NoEvidence | S4 | 目标未验 |
| U04 | Fixture/Golden | Hash被篡改与未审批阻断 | S3/S4 | 目标未验 |
| U05 | Assertion->源码->Rerun | 原Attempt不覆盖、PDB缺失可解释 | S4 | 目标未验 |
| U06 | Geometry/Object/Runtime真实图形 | Run/Attempt/Owner关联保持 | S4 | 目标未验 |
| U07 | Graph/Channel真实Provider | Provider缺失不模拟通过 | S5 | 依赖生产Owner |
| U08 | Cancel/Timeout/Close | UI状态不伪造; Worker回收证据 | S4/S5 | 目标未验 |
| U09 | Coverage/History/Diff | 环境不可比不能标绿 | S4 | 目标未验 |
| U10 | DPI/键盘/无障碍/大数据 | 100%-200%缩放及性能标准 | S5 | 目标未验 |

### 09.4 通过与覆盖率的原始证据链

每个正式Requirement必须记录审核的SpecVersion、CapabilityId/ObjectId、维度、CaseId、AttemptId、TestId、断言/Artifact地址、Commit、环境、运行Profile、判定与不可比原因。数据导入后未经人工审核的ID保持Unmapped；暂未实现的生产Owner保持Unsupported/Blocked，不得由Mock Screenshot填满表格。

### 09.5 全部能力对象×八维验收行（264条，保留原始单元格内容）

| 矩阵行号 | 方面 | Owner | 能力主题 | 对象/真实API | 维度 | 是否Required | 已知候选TestId | 正式能力与对象ID | AssertionEvidence实际锚点 | 正式验收状态 | 工作包 | 说明/生产依赖 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ROW-001-1 | Foundation | Core | StableId/Guard | CxStableId | 合同 | Required | 待确认 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Owner基础契约 |
| ROW-001-2 | Foundation | Core | StableId/Guard | CxStableId | 正常 | Required | 待确认 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Owner基础契约 |
| ROW-001-3 | Foundation | Core | StableId/Guard | CxStableId | 边界 | Required | 待确认 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Owner基础契约 |
| ROW-001-4 | Foundation | Core | StableId/Guard | CxStableId | 异常 | Required | 待确认 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Owner基础契约 |
| ROW-001-5 | Foundation | Core | StableId/Guard | CxStableId | 并发 | Optional（按Owner评审） | 待确认 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Owner基础契约 |
| ROW-001-6 | Foundation | Core | StableId/Guard | CxStableId | 生命周期 | Required | 待确认 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Owner基础契约 |
| ROW-001-7 | Foundation | Core | StableId/Guard | CxStableId | 版本 | Optional（按Owner评审） | 待确认 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Owner基础契约 |
| ROW-001-8 | Foundation | Core | StableId/Guard | CxStableId | 性能 | Optional（按Owner评审） | 待确认 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Owner基础契约 |
| ROW-002-1 | Foundation | Numerics | 数值容差、向量矩阵 | 基础Numerics类型 | 合同 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 低层纯计算 |
| ROW-002-2 | Foundation | Numerics | 数值容差、向量矩阵 | 基础Numerics类型 | 正常 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 低层纯计算 |
| ROW-002-3 | Foundation | Numerics | 数值容差、向量矩阵 | 基础Numerics类型 | 边界 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 低层纯计算 |
| ROW-002-4 | Foundation | Numerics | 数值容差、向量矩阵 | 基础Numerics类型 | 异常 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 低层纯计算 |
| ROW-002-5 | Foundation | Numerics | 数值容差、向量矩阵 | 基础Numerics类型 | 并发 | Optional（按Owner评审） | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 低层纯计算 |
| ROW-002-6 | Foundation | Numerics | 数值容差、向量矩阵 | 基础Numerics类型 | 生命周期 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 低层纯计算 |
| ROW-002-7 | Foundation | Numerics | 数值容差、向量矩阵 | 基础Numerics类型 | 版本 | Optional（按Owner评审） | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 低层纯计算 |
| ROW-002-8 | Foundation | Numerics | 数值容差、向量矩阵 | 基础Numerics类型 | 性能 | Optional（按Owner评审） | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 低层纯计算 |
| ROW-003-1 | Foundation | Geometry | 二维坐标与变换 | CxRect2D/CxAffine2D | 合同 | Required | TCT.F.GEO.* | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有部分真实测试 |
| ROW-003-2 | Foundation | Geometry | 二维坐标与变换 | CxRect2D/CxAffine2D | 正常 | Required | TCT.F.GEO.* | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有部分真实测试 |
| ROW-003-3 | Foundation | Geometry | 二维坐标与变换 | CxRect2D/CxAffine2D | 边界 | Required | TCT.F.GEO.* | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有部分真实测试 |
| ROW-003-4 | Foundation | Geometry | 二维坐标与变换 | CxRect2D/CxAffine2D | 异常 | Required | TCT.F.GEO.* | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有部分真实测试 |
| ROW-003-5 | Foundation | Geometry | 二维坐标与变换 | CxRect2D/CxAffine2D | 并发 | Optional（按Owner评审） | TCT.F.GEO.* | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有部分真实测试 |
| ROW-003-6 | Foundation | Geometry | 二维坐标与变换 | CxRect2D/CxAffine2D | 生命周期 | Required | TCT.F.GEO.* | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有部分真实测试 |
| ROW-003-7 | Foundation | Geometry | 二维坐标与变换 | CxRect2D/CxAffine2D | 版本 | Optional（按Owner评审） | TCT.F.GEO.* | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有部分真实测试 |
| ROW-003-8 | Foundation | Geometry | 二维坐标与变换 | CxRect2D/CxAffine2D | 性能 | Optional（按Owner评审） | TCT.F.GEO.* | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有部分真实测试 |
| ROW-004-1 | Foundation | Imaging | 基础像素与图像容器 | 像素/图像算法契约 | 合同 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 专业Detector不归公共 |
| ROW-004-2 | Foundation | Imaging | 基础像素与图像容器 | 像素/图像算法契约 | 正常 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 专业Detector不归公共 |
| ROW-004-3 | Foundation | Imaging | 基础像素与图像容器 | 像素/图像算法契约 | 边界 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 专业Detector不归公共 |
| ROW-004-4 | Foundation | Imaging | 基础像素与图像容器 | 像素/图像算法契约 | 异常 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 专业Detector不归公共 |
| ROW-004-5 | Foundation | Imaging | 基础像素与图像容器 | 像素/图像算法契约 | 并发 | Optional（按Owner评审） | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 专业Detector不归公共 |
| ROW-004-6 | Foundation | Imaging | 基础像素与图像容器 | 像素/图像算法契约 | 生命周期 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 专业Detector不归公共 |
| ROW-004-7 | Foundation | Imaging | 基础像素与图像容器 | 像素/图像算法契约 | 版本 | Optional（按Owner评审） | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 专业Detector不归公共 |
| ROW-004-8 | Foundation | Imaging | 基础像素与图像容器 | 像素/图像算法契约 | 性能 | Optional（按Owner评审） | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 专业Detector不归公共 |
| ROW-005-1 | Foundation | Volume | Voxel/Volume基础数据 | 体数据与坐标模型 | 合同 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | CT算法不在此Owner |
| ROW-005-2 | Foundation | Volume | Voxel/Volume基础数据 | 体数据与坐标模型 | 正常 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | CT算法不在此Owner |
| ROW-005-3 | Foundation | Volume | Voxel/Volume基础数据 | 体数据与坐标模型 | 边界 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | CT算法不在此Owner |
| ROW-005-4 | Foundation | Volume | Voxel/Volume基础数据 | 体数据与坐标模型 | 异常 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | CT算法不在此Owner |
| ROW-005-5 | Foundation | Volume | Voxel/Volume基础数据 | 体数据与坐标模型 | 并发 | Optional（按Owner评审） | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | CT算法不在此Owner |
| ROW-005-6 | Foundation | Volume | Voxel/Volume基础数据 | 体数据与坐标模型 | 生命周期 | Required | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | CT算法不在此Owner |
| ROW-005-7 | Foundation | Volume | Voxel/Volume基础数据 | 体数据与坐标模型 | 版本 | Optional（按Owner评审） | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | CT算法不在此Owner |
| ROW-005-8 | Foundation | Volume | Voxel/Volume基础数据 | 体数据与坐标模型 | 性能 | Optional（按Owner评审） | 待实际Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | CT算法不在此Owner |
| ROW-006-1 | Foundation | Data | 配置Schema/校验 | FCMConfigCatalog/Validation | 合同 | Required | TCT.FCM.001;TCT.FCM.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-006-2 | Foundation | Data | 配置Schema/校验 | FCMConfigCatalog/Validation | 正常 | Required | TCT.FCM.001;TCT.FCM.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-006-3 | Foundation | Data | 配置Schema/校验 | FCMConfigCatalog/Validation | 边界 | Required | TCT.FCM.001;TCT.FCM.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-006-4 | Foundation | Data | 配置Schema/校验 | FCMConfigCatalog/Validation | 异常 | Required | TCT.FCM.001;TCT.FCM.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-006-5 | Foundation | Data | 配置Schema/校验 | FCMConfigCatalog/Validation | 并发 | Optional（按Owner评审） | TCT.FCM.001;TCT.FCM.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-006-6 | Foundation | Data | 配置Schema/校验 | FCMConfigCatalog/Validation | 生命周期 | Required | TCT.FCM.001;TCT.FCM.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-006-7 | Foundation | Data | 配置Schema/校验 | FCMConfigCatalog/Validation | 版本 | Optional（按Owner评审） | TCT.FCM.001;TCT.FCM.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-006-8 | Foundation | Data | 配置Schema/校验 | FCMConfigCatalog/Validation | 性能 | Optional（按Owner评审） | TCT.FCM.001;TCT.FCM.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-007-1 | Foundation | Data | 执行Scope基础合同 | IFDEExecutionDataScope | 合同 | Required | TCT.MDATA.004 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 目前有Scope合同，不等于完整堆栈 |
| ROW-007-2 | Foundation | Data | 执行Scope基础合同 | IFDEExecutionDataScope | 正常 | Required | TCT.MDATA.004 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 目前有Scope合同，不等于完整堆栈 |
| ROW-007-3 | Foundation | Data | 执行Scope基础合同 | IFDEExecutionDataScope | 边界 | Required | TCT.MDATA.004 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 目前有Scope合同，不等于完整堆栈 |
| ROW-007-4 | Foundation | Data | 执行Scope基础合同 | IFDEExecutionDataScope | 异常 | Required | TCT.MDATA.004 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 目前有Scope合同，不等于完整堆栈 |
| ROW-007-5 | Foundation | Data | 执行Scope基础合同 | IFDEExecutionDataScope | 并发 | Optional（按Owner评审） | TCT.MDATA.004 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 目前有Scope合同，不等于完整堆栈 |
| ROW-007-6 | Foundation | Data | 执行Scope基础合同 | IFDEExecutionDataScope | 生命周期 | Required | TCT.MDATA.004 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 目前有Scope合同，不等于完整堆栈 |
| ROW-007-7 | Foundation | Data | 执行Scope基础合同 | IFDEExecutionDataScope | 版本 | Optional（按Owner评审） | TCT.MDATA.004 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 目前有Scope合同，不等于完整堆栈 |
| ROW-007-8 | Foundation | Data | 执行Scope基础合同 | IFDEExecutionDataScope | 性能 | Optional（按Owner评审） | TCT.MDATA.004 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 目前有Scope合同，不等于完整堆栈 |
| ROW-008-1 | Foundation | Infrastructure | FLD日志采集 | FLDLogService | 合同 | Required | TCT.FLD.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-008-2 | Foundation | Infrastructure | FLD日志采集 | FLDLogService | 正常 | Required | TCT.FLD.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-008-3 | Foundation | Infrastructure | FLD日志采集 | FLDLogService | 边界 | Required | TCT.FLD.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-008-4 | Foundation | Infrastructure | FLD日志采集 | FLDLogService | 异常 | Required | TCT.FLD.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-008-5 | Foundation | Infrastructure | FLD日志采集 | FLDLogService | 并发 | Optional（按Owner评审） | TCT.FLD.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-008-6 | Foundation | Infrastructure | FLD日志采集 | FLDLogService | 生命周期 | Required | TCT.FLD.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-008-7 | Foundation | Infrastructure | FLD日志采集 | FLDLogService | 版本 | Optional（按Owner评审） | TCT.FLD.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-008-8 | Foundation | Infrastructure | FLD日志采集 | FLDLogService | 性能 | Optional（按Owner评审） | TCT.FLD.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-009-1 | Foundation | Infrastructure | FCR并发与Worker | FCRWorker | 合同 | Required | TCT.FCR.001;TCT.FCR.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-009-2 | Foundation | Infrastructure | FCR并发与Worker | FCRWorker | 正常 | Required | TCT.FCR.001;TCT.FCR.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-009-3 | Foundation | Infrastructure | FCR并发与Worker | FCRWorker | 边界 | Required | TCT.FCR.001;TCT.FCR.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-009-4 | Foundation | Infrastructure | FCR并发与Worker | FCRWorker | 异常 | Required | TCT.FCR.001;TCT.FCR.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-009-5 | Foundation | Infrastructure | FCR并发与Worker | FCRWorker | 并发 | Optional（按Owner评审） | TCT.FCR.001;TCT.FCR.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-009-6 | Foundation | Infrastructure | FCR并发与Worker | FCRWorker | 生命周期 | Required | TCT.FCR.001;TCT.FCR.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-009-7 | Foundation | Infrastructure | FCR并发与Worker | FCRWorker | 版本 | Optional（按Owner评审） | TCT.FCR.001;TCT.FCR.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-009-8 | Foundation | Infrastructure | FCR并发与Worker | FCRWorker | 性能 | Required | TCT.FCR.001;TCT.FCR.002 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-010-1 | Foundation | Transport | 底层Channel/Buffer | 待生产Owner确认 | 合同 | Required | 待真实接口 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 无生产Codec不可声称协议验收 |
| ROW-010-2 | Foundation | Transport | 底层Channel/Buffer | 待生产Owner确认 | 正常 | Required | 待真实接口 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 无生产Codec不可声称协议验收 |
| ROW-010-3 | Foundation | Transport | 底层Channel/Buffer | 待生产Owner确认 | 边界 | Required | 待真实接口 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 无生产Codec不可声称协议验收 |
| ROW-010-4 | Foundation | Transport | 底层Channel/Buffer | 待生产Owner确认 | 异常 | Required | 待真实接口 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 无生产Codec不可声称协议验收 |
| ROW-010-5 | Foundation | Transport | 底层Channel/Buffer | 待生产Owner确认 | 并发 | Optional（按Owner评审） | 待真实接口 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 无生产Codec不可声称协议验收 |
| ROW-010-6 | Foundation | Transport | 底层Channel/Buffer | 待生产Owner确认 | 生命周期 | Required | 待真实接口 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 无生产Codec不可声称协议验收 |
| ROW-010-7 | Foundation | Transport | 底层Channel/Buffer | 待生产Owner确认 | 版本 | Optional（按Owner评审） | 待真实接口 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 无生产Codec不可声称协议验收 |
| ROW-010-8 | Foundation | Transport | 底层Channel/Buffer | 待生产Owner确认 | 性能 | Optional（按Owner评审） | 待真实接口 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 无生产Codec不可声称协议验收 |
| ROW-011-1 | Mechanism | Core | 对象树与引用 | CxProjectDocument | 合同 | Required | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Ownership和Reference分开 |
| ROW-011-2 | Mechanism | Core | 对象树与引用 | CxProjectDocument | 正常 | Required | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Ownership和Reference分开 |
| ROW-011-3 | Mechanism | Core | 对象树与引用 | CxProjectDocument | 边界 | Required | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Ownership和Reference分开 |
| ROW-011-4 | Mechanism | Core | 对象树与引用 | CxProjectDocument | 异常 | Required | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Ownership和Reference分开 |
| ROW-011-5 | Mechanism | Core | 对象树与引用 | CxProjectDocument | 并发 | Optional（按Owner评审） | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Ownership和Reference分开 |
| ROW-011-6 | Mechanism | Core | 对象树与引用 | CxProjectDocument | 生命周期 | Required | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Ownership和Reference分开 |
| ROW-011-7 | Mechanism | Core | 对象树与引用 | CxProjectDocument | 版本 | Optional（按Owner评审） | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Ownership和Reference分开 |
| ROW-011-8 | Mechanism | Core | 对象树与引用 | CxProjectDocument | 性能 | Optional（按Owner评审） | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Ownership和Reference分开 |
| ROW-012-1 | Mechanism | Core | 工程事务/Revision | CxEngineeringSession | 合同 | Required | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Undo/Redo真实测试 |
| ROW-012-2 | Mechanism | Core | 工程事务/Revision | CxEngineeringSession | 正常 | Required | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Undo/Redo真实测试 |
| ROW-012-3 | Mechanism | Core | 工程事务/Revision | CxEngineeringSession | 边界 | Required | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Undo/Redo真实测试 |
| ROW-012-4 | Mechanism | Core | 工程事务/Revision | CxEngineeringSession | 异常 | Required | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Undo/Redo真实测试 |
| ROW-012-5 | Mechanism | Core | 工程事务/Revision | CxEngineeringSession | 并发 | Optional（按Owner评审） | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Undo/Redo真实测试 |
| ROW-012-6 | Mechanism | Core | 工程事务/Revision | CxEngineeringSession | 生命周期 | Required | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Undo/Redo真实测试 |
| ROW-012-7 | Mechanism | Core | 工程事务/Revision | CxEngineeringSession | 版本 | Optional（按Owner评审） | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Undo/Redo真实测试 |
| ROW-012-8 | Mechanism | Core | 工程事务/Revision | CxEngineeringSession | 性能 | Optional（按Owner评审） | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | Undo/Redo真实测试 |
| ROW-013-1 | Mechanism | Data | 配置版本和生效 | MHSConfigSection | 合同 | Required | TCT.MDATA.001;TCT.MDATA.002;TCT.MDATA.007 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-013-2 | Mechanism | Data | 配置版本和生效 | MHSConfigSection | 正常 | Required | TCT.MDATA.001;TCT.MDATA.002;TCT.MDATA.007 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-013-3 | Mechanism | Data | 配置版本和生效 | MHSConfigSection | 边界 | Required | TCT.MDATA.001;TCT.MDATA.002;TCT.MDATA.007 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-013-4 | Mechanism | Data | 配置版本和生效 | MHSConfigSection | 异常 | Required | TCT.MDATA.001;TCT.MDATA.002;TCT.MDATA.007 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-013-5 | Mechanism | Data | 配置版本和生效 | MHSConfigSection | 并发 | Optional（按Owner评审） | TCT.MDATA.001;TCT.MDATA.002;TCT.MDATA.007 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-013-6 | Mechanism | Data | 配置版本和生效 | MHSConfigSection | 生命周期 | Required | TCT.MDATA.001;TCT.MDATA.002;TCT.MDATA.007 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-013-7 | Mechanism | Data | 配置版本和生效 | MHSConfigSection | 版本 | Optional（按Owner评审） | TCT.MDATA.001;TCT.MDATA.002;TCT.MDATA.007 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-013-8 | Mechanism | Data | 配置版本和生效 | MHSConfigSection | 性能 | Optional（按Owner评审） | TCT.MDATA.001;TCT.MDATA.002;TCT.MDATA.007 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-014-1 | Mechanism | Data | 作用域与Owned租约 | MDEExecutionDataScope | 合同 | Required | TCT.MDATA.004;TCT.MDATA.006;TCT.MDATA.009 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-014-2 | Mechanism | Data | 作用域与Owned租约 | MDEExecutionDataScope | 正常 | Required | TCT.MDATA.004;TCT.MDATA.006;TCT.MDATA.009 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-014-3 | Mechanism | Data | 作用域与Owned租约 | MDEExecutionDataScope | 边界 | Required | TCT.MDATA.004;TCT.MDATA.006;TCT.MDATA.009 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-014-4 | Mechanism | Data | 作用域与Owned租约 | MDEExecutionDataScope | 异常 | Required | TCT.MDATA.004;TCT.MDATA.006;TCT.MDATA.009 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-014-5 | Mechanism | Data | 作用域与Owned租约 | MDEExecutionDataScope | 并发 | Optional（按Owner评审） | TCT.MDATA.004;TCT.MDATA.006;TCT.MDATA.009 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-014-6 | Mechanism | Data | 作用域与Owned租约 | MDEExecutionDataScope | 生命周期 | Required | TCT.MDATA.004;TCT.MDATA.006;TCT.MDATA.009 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-014-7 | Mechanism | Data | 作用域与Owned租约 | MDEExecutionDataScope | 版本 | Optional（按Owner评审） | TCT.MDATA.004;TCT.MDATA.006;TCT.MDATA.009 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-014-8 | Mechanism | Data | 作用域与Owned租约 | MDEExecutionDataScope | 性能 | Optional（按Owner评审） | TCT.MDATA.004;TCT.MDATA.006;TCT.MDATA.009 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-015-1 | Mechanism | Data | 统一Heap/Stack/DataSpace | 生产Data Owner待实现 | 合同 | Required | 待生产实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 已有Scope不等于全部数据体系 |
| ROW-015-2 | Mechanism | Data | 统一Heap/Stack/DataSpace | 生产Data Owner待实现 | 正常 | Required | 待生产实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 已有Scope不等于全部数据体系 |
| ROW-015-3 | Mechanism | Data | 统一Heap/Stack/DataSpace | 生产Data Owner待实现 | 边界 | Required | 待生产实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 已有Scope不等于全部数据体系 |
| ROW-015-4 | Mechanism | Data | 统一Heap/Stack/DataSpace | 生产Data Owner待实现 | 异常 | Required | 待生产实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 已有Scope不等于全部数据体系 |
| ROW-015-5 | Mechanism | Data | 统一Heap/Stack/DataSpace | 生产Data Owner待实现 | 并发 | Optional（按Owner评审） | 待生产实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 已有Scope不等于全部数据体系 |
| ROW-015-6 | Mechanism | Data | 统一Heap/Stack/DataSpace | 生产Data Owner待实现 | 生命周期 | Required | 待生产实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 已有Scope不等于全部数据体系 |
| ROW-015-7 | Mechanism | Data | 统一Heap/Stack/DataSpace | 生产Data Owner待实现 | 版本 | Optional（按Owner评审） | 待生产实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 已有Scope不等于全部数据体系 |
| ROW-015-8 | Mechanism | Data | 统一Heap/Stack/DataSpace | 生产Data Owner待实现 | 性能 | Optional（按Owner评审） | 待生产实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 已有Scope不等于全部数据体系 |
| ROW-016-1 | Mechanism | Logic | 规则表达式/事件 | 待真实Owner核验 | 合同 | Required | 待实际Owner | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不能创建测试替身当产品 |
| ROW-016-2 | Mechanism | Logic | 规则表达式/事件 | 待真实Owner核验 | 正常 | Required | 待实际Owner | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不能创建测试替身当产品 |
| ROW-016-3 | Mechanism | Logic | 规则表达式/事件 | 待真实Owner核验 | 边界 | Required | 待实际Owner | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不能创建测试替身当产品 |
| ROW-016-4 | Mechanism | Logic | 规则表达式/事件 | 待真实Owner核验 | 异常 | Required | 待实际Owner | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不能创建测试替身当产品 |
| ROW-016-5 | Mechanism | Logic | 规则表达式/事件 | 待真实Owner核验 | 并发 | Optional（按Owner评审） | 待实际Owner | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不能创建测试替身当产品 |
| ROW-016-6 | Mechanism | Logic | 规则表达式/事件 | 待真实Owner核验 | 生命周期 | Required | 待实际Owner | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不能创建测试替身当产品 |
| ROW-016-7 | Mechanism | Logic | 规则表达式/事件 | 待真实Owner核验 | 版本 | Optional（按Owner评审） | 待实际Owner | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不能创建测试替身当产品 |
| ROW-016-8 | Mechanism | Logic | 规则表达式/事件 | 待真实Owner核验 | 性能 | Optional（按Owner评审） | 待实际Owner | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不能创建测试替身当产品 |
| ROW-017-1 | Mechanism | Visual | 绘图投影和HitTest | CxSceneProjection | 合同 | Required | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-017-2 | Mechanism | Visual | 绘图投影和HitTest | CxSceneProjection | 正常 | Required | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-017-3 | Mechanism | Visual | 绘图投影和HitTest | CxSceneProjection | 边界 | Required | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-017-4 | Mechanism | Visual | 绘图投影和HitTest | CxSceneProjection | 异常 | Required | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-017-5 | Mechanism | Visual | 绘图投影和HitTest | CxSceneProjection | 并发 | Optional（按Owner评审） | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-017-6 | Mechanism | Visual | 绘图投影和HitTest | CxSceneProjection | 生命周期 | Required | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-017-7 | Mechanism | Visual | 绘图投影和HitTest | CxSceneProjection | 版本 | Optional（按Owner评审） | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-017-8 | Mechanism | Visual | 绘图投影和HitTest | CxSceneProjection | 性能 | Optional（按Owner评审） | TCT.MVIS.001 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-018-1 | Mechanism | Visual | 编辑与Undo/Redo | CxVisualEditor | 合同 | Required | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-018-2 | Mechanism | Visual | 编辑与Undo/Redo | CxVisualEditor | 正常 | Required | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-018-3 | Mechanism | Visual | 编辑与Undo/Redo | CxVisualEditor | 边界 | Required | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-018-4 | Mechanism | Visual | 编辑与Undo/Redo | CxVisualEditor | 异常 | Required | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-018-5 | Mechanism | Visual | 编辑与Undo/Redo | CxVisualEditor | 并发 | Optional（按Owner评审） | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-018-6 | Mechanism | Visual | 编辑与Undo/Redo | CxVisualEditor | 生命周期 | Required | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-018-7 | Mechanism | Visual | 编辑与Undo/Redo | CxVisualEditor | 版本 | Optional（按Owner评审） | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-018-8 | Mechanism | Visual | 编辑与Undo/Redo | CxVisualEditor | 性能 | Optional（按Owner评审） | TCT.MVIS.002;TCT.MVIS.003 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-019-1 | Mechanism | Orchestration | Graph TypedPort Connection | 正式Graph/Port契约 | 合同 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Graph Viewer不得取代Owner |
| ROW-019-2 | Mechanism | Orchestration | Graph TypedPort Connection | 正式Graph/Port契约 | 正常 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Graph Viewer不得取代Owner |
| ROW-019-3 | Mechanism | Orchestration | Graph TypedPort Connection | 正式Graph/Port契约 | 边界 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Graph Viewer不得取代Owner |
| ROW-019-4 | Mechanism | Orchestration | Graph TypedPort Connection | 正式Graph/Port契约 | 异常 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Graph Viewer不得取代Owner |
| ROW-019-5 | Mechanism | Orchestration | Graph TypedPort Connection | 正式Graph/Port契约 | 并发 | Optional（按Owner评审） | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Graph Viewer不得取代Owner |
| ROW-019-6 | Mechanism | Orchestration | Graph TypedPort Connection | 正式Graph/Port契约 | 生命周期 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Graph Viewer不得取代Owner |
| ROW-019-7 | Mechanism | Orchestration | Graph TypedPort Connection | 正式Graph/Port契约 | 版本 | Optional（按Owner评审） | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Graph Viewer不得取代Owner |
| ROW-019-8 | Mechanism | Orchestration | Graph TypedPort Connection | 正式Graph/Port契约 | 性能 | Optional（按Owner评审） | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | Graph Viewer不得取代Owner |
| ROW-020-1 | Mechanism | Runtime | MRK计划编译和DAG | MRKExecutionPlanCompiler | 合同 | Required | TCT.MRK.003;TCT.MRK.004;TCT.MRK.005 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-020-2 | Mechanism | Runtime | MRK计划编译和DAG | MRKExecutionPlanCompiler | 正常 | Required | TCT.MRK.003;TCT.MRK.004;TCT.MRK.005 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-020-3 | Mechanism | Runtime | MRK计划编译和DAG | MRKExecutionPlanCompiler | 边界 | Required | TCT.MRK.003;TCT.MRK.004;TCT.MRK.005 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-020-4 | Mechanism | Runtime | MRK计划编译和DAG | MRKExecutionPlanCompiler | 异常 | Required | TCT.MRK.003;TCT.MRK.004;TCT.MRK.005 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-020-5 | Mechanism | Runtime | MRK计划编译和DAG | MRKExecutionPlanCompiler | 并发 | Optional（按Owner评审） | TCT.MRK.003;TCT.MRK.004;TCT.MRK.005 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-020-6 | Mechanism | Runtime | MRK计划编译和DAG | MRKExecutionPlanCompiler | 生命周期 | Required | TCT.MRK.003;TCT.MRK.004;TCT.MRK.005 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-020-7 | Mechanism | Runtime | MRK计划编译和DAG | MRKExecutionPlanCompiler | 版本 | Optional（按Owner评审） | TCT.MRK.003;TCT.MRK.004;TCT.MRK.005 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-020-8 | Mechanism | Runtime | MRK计划编译和DAG | MRKExecutionPlanCompiler | 性能 | Optional（按Owner评审） | TCT.MRK.003;TCT.MRK.004;TCT.MRK.005 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-021-1 | Mechanism | Runtime | Journal/Recovery | MRKFileExecutionJournal | 合同 | Required | TCT.MRK.006;TCT.MRK.007;TCT.MRK.008;TCT.MRK.014 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-021-2 | Mechanism | Runtime | Journal/Recovery | MRKFileExecutionJournal | 正常 | Required | TCT.MRK.006;TCT.MRK.007;TCT.MRK.008;TCT.MRK.014 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-021-3 | Mechanism | Runtime | Journal/Recovery | MRKFileExecutionJournal | 边界 | Required | TCT.MRK.006;TCT.MRK.007;TCT.MRK.008;TCT.MRK.014 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-021-4 | Mechanism | Runtime | Journal/Recovery | MRKFileExecutionJournal | 异常 | Required | TCT.MRK.006;TCT.MRK.007;TCT.MRK.008;TCT.MRK.014 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-021-5 | Mechanism | Runtime | Journal/Recovery | MRKFileExecutionJournal | 并发 | Optional（按Owner评审） | TCT.MRK.006;TCT.MRK.007;TCT.MRK.008;TCT.MRK.014 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-021-6 | Mechanism | Runtime | Journal/Recovery | MRKFileExecutionJournal | 生命周期 | Required | TCT.MRK.006;TCT.MRK.007;TCT.MRK.008;TCT.MRK.014 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-021-7 | Mechanism | Runtime | Journal/Recovery | MRKFileExecutionJournal | 版本 | Optional（按Owner评审） | TCT.MRK.006;TCT.MRK.007;TCT.MRK.008;TCT.MRK.014 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-021-8 | Mechanism | Runtime | Journal/Recovery | MRKFileExecutionJournal | 性能 | Optional（按Owner评审） | TCT.MRK.006;TCT.MRK.007;TCT.MRK.008;TCT.MRK.014 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-022-1 | Mechanism | Runtime | 资源/热切换/执行组 | MRKResourceLeaseCoordinator | 合同 | Required | TCT.MRK.009;TCT.MRK.012;TCT.MRK.013 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-022-2 | Mechanism | Runtime | 资源/热切换/执行组 | MRKResourceLeaseCoordinator | 正常 | Required | TCT.MRK.009;TCT.MRK.012;TCT.MRK.013 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-022-3 | Mechanism | Runtime | 资源/热切换/执行组 | MRKResourceLeaseCoordinator | 边界 | Required | TCT.MRK.009;TCT.MRK.012;TCT.MRK.013 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-022-4 | Mechanism | Runtime | 资源/热切换/执行组 | MRKResourceLeaseCoordinator | 异常 | Required | TCT.MRK.009;TCT.MRK.012;TCT.MRK.013 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-022-5 | Mechanism | Runtime | 资源/热切换/执行组 | MRKResourceLeaseCoordinator | 并发 | Optional（按Owner评审） | TCT.MRK.009;TCT.MRK.012;TCT.MRK.013 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-022-6 | Mechanism | Runtime | 资源/热切换/执行组 | MRKResourceLeaseCoordinator | 生命周期 | Required | TCT.MRK.009;TCT.MRK.012;TCT.MRK.013 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-022-7 | Mechanism | Runtime | 资源/热切换/执行组 | MRKResourceLeaseCoordinator | 版本 | Optional（按Owner评审） | TCT.MRK.009;TCT.MRK.012;TCT.MRK.013 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-022-8 | Mechanism | Runtime | 资源/热切换/执行组 | MRKResourceLeaseCoordinator | 性能 | Optional（按Owner评审） | TCT.MRK.009;TCT.MRK.012;TCT.MRK.013 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-023-1 | Mechanism | Runtime | 外部操作/Correlation | MRKExternalOperationTracker | 合同 | Required | TCT.MRK.010;TCT.MRK.011 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-023-2 | Mechanism | Runtime | 外部操作/Correlation | MRKExternalOperationTracker | 正常 | Required | TCT.MRK.010;TCT.MRK.011 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-023-3 | Mechanism | Runtime | 外部操作/Correlation | MRKExternalOperationTracker | 边界 | Required | TCT.MRK.010;TCT.MRK.011 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-023-4 | Mechanism | Runtime | 外部操作/Correlation | MRKExternalOperationTracker | 异常 | Required | TCT.MRK.010;TCT.MRK.011 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-023-5 | Mechanism | Runtime | 外部操作/Correlation | MRKExternalOperationTracker | 并发 | Optional（按Owner评审） | TCT.MRK.010;TCT.MRK.011 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-023-6 | Mechanism | Runtime | 外部操作/Correlation | MRKExternalOperationTracker | 生命周期 | Required | TCT.MRK.010;TCT.MRK.011 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-023-7 | Mechanism | Runtime | 外部操作/Correlation | MRKExternalOperationTracker | 版本 | Optional（按Owner评审） | TCT.MRK.010;TCT.MRK.011 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-023-8 | Mechanism | Runtime | 外部操作/Correlation | MRKExternalOperationTracker | 性能 | Optional（按Owner评审） | TCT.MRK.010;TCT.MRK.011 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 已有真实测试 |
| ROW-024-1 | Mechanism | Connectivity | Channel/Protocol/Session | 真实Connectivity Adapter | 合同 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 图片中的Modbus帧为模拟概念 |
| ROW-024-2 | Mechanism | Connectivity | Channel/Protocol/Session | 真实Connectivity Adapter | 正常 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 图片中的Modbus帧为模拟概念 |
| ROW-024-3 | Mechanism | Connectivity | Channel/Protocol/Session | 真实Connectivity Adapter | 边界 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 图片中的Modbus帧为模拟概念 |
| ROW-024-4 | Mechanism | Connectivity | Channel/Protocol/Session | 真实Connectivity Adapter | 异常 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 图片中的Modbus帧为模拟概念 |
| ROW-024-5 | Mechanism | Connectivity | Channel/Protocol/Session | 真实Connectivity Adapter | 并发 | Optional（按Owner评审） | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 图片中的Modbus帧为模拟概念 |
| ROW-024-6 | Mechanism | Connectivity | Channel/Protocol/Session | 真实Connectivity Adapter | 生命周期 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 图片中的Modbus帧为模拟概念 |
| ROW-024-7 | Mechanism | Connectivity | Channel/Protocol/Session | 真实Connectivity Adapter | 版本 | Optional（按Owner评审） | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 图片中的Modbus帧为模拟概念 |
| ROW-024-8 | Mechanism | Connectivity | Channel/Protocol/Session | 真实Connectivity Adapter | 性能 | Optional（按Owner评审） | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 图片中的Modbus帧为模拟概念 |
| ROW-025-1 | Mechanism | Lifecycle | Migration/Deploy/Version | 真实Lifecycle Owner | 合同 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 正式发布前强制回归 |
| ROW-025-2 | Mechanism | Lifecycle | Migration/Deploy/Version | 真实Lifecycle Owner | 正常 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 正式发布前强制回归 |
| ROW-025-3 | Mechanism | Lifecycle | Migration/Deploy/Version | 真实Lifecycle Owner | 边界 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 正式发布前强制回归 |
| ROW-025-4 | Mechanism | Lifecycle | Migration/Deploy/Version | 真实Lifecycle Owner | 异常 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 正式发布前强制回归 |
| ROW-025-5 | Mechanism | Lifecycle | Migration/Deploy/Version | 真实Lifecycle Owner | 并发 | Optional（按Owner评审） | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 正式发布前强制回归 |
| ROW-025-6 | Mechanism | Lifecycle | Migration/Deploy/Version | 真实Lifecycle Owner | 生命周期 | Required | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 正式发布前强制回归 |
| ROW-025-7 | Mechanism | Lifecycle | Migration/Deploy/Version | 真实Lifecycle Owner | 版本 | Optional（按Owner评审） | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 正式发布前强制回归 |
| ROW-025-8 | Mechanism | Lifecycle | Migration/Deploy/Version | 真实Lifecycle Owner | 性能 | Optional（按Owner评审） | 待真实Owner实现 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 正式发布前强制回归 |
| ROW-026-1 | Mechanism | Presentation.Wpf | UI表现适配 | WPF Presenter/Controls | 合同 | Required | 待真实Owner核验 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不和TestCenter Viewer混为一谈 |
| ROW-026-2 | Mechanism | Presentation.Wpf | UI表现适配 | WPF Presenter/Controls | 正常 | Required | 待真实Owner核验 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不和TestCenter Viewer混为一谈 |
| ROW-026-3 | Mechanism | Presentation.Wpf | UI表现适配 | WPF Presenter/Controls | 边界 | Required | 待真实Owner核验 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不和TestCenter Viewer混为一谈 |
| ROW-026-4 | Mechanism | Presentation.Wpf | UI表现适配 | WPF Presenter/Controls | 异常 | Required | 待真实Owner核验 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不和TestCenter Viewer混为一谈 |
| ROW-026-5 | Mechanism | Presentation.Wpf | UI表现适配 | WPF Presenter/Controls | 并发 | Optional（按Owner评审） | 待真实Owner核验 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不和TestCenter Viewer混为一谈 |
| ROW-026-6 | Mechanism | Presentation.Wpf | UI表现适配 | WPF Presenter/Controls | 生命周期 | Required | 待真实Owner核验 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不和TestCenter Viewer混为一谈 |
| ROW-026-7 | Mechanism | Presentation.Wpf | UI表现适配 | WPF Presenter/Controls | 版本 | Optional（按Owner评审） | 待真实Owner核验 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不和TestCenter Viewer混为一谈 |
| ROW-026-8 | Mechanism | Presentation.Wpf | UI表现适配 | WPF Presenter/Controls | 性能 | Optional（按Owner评审） | 待真实Owner核验 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 不和TestCenter Viewer混为一谈 |
| ROW-027-1 | Tests | TestCenter | 开发者API及发现 | TCTTestAttribute/Context | 合同 | Required | D01;D02;D03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | S1目标部分待实现 |
| ROW-027-2 | Tests | TestCenter | 开发者API及发现 | TCTTestAttribute/Context | 正常 | Required | D01;D02;D03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | S1目标部分待实现 |
| ROW-027-3 | Tests | TestCenter | 开发者API及发现 | TCTTestAttribute/Context | 边界 | Required | D01;D02;D03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | S1目标部分待实现 |
| ROW-027-4 | Tests | TestCenter | 开发者API及发现 | TCTTestAttribute/Context | 异常 | Required | D01;D02;D03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | S1目标部分待实现 |
| ROW-027-5 | Tests | TestCenter | 开发者API及发现 | TCTTestAttribute/Context | 并发 | Optional（按Owner评审） | D01;D02;D03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | S1目标部分待实现 |
| ROW-027-6 | Tests | TestCenter | 开发者API及发现 | TCTTestAttribute/Context | 生命周期 | Required | D01;D02;D03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | S1目标部分待实现 |
| ROW-027-7 | Tests | TestCenter | 开发者API及发现 | TCTTestAttribute/Context | 版本 | Optional（按Owner评审） | D01;D02;D03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | S1目标部分待实现 |
| ROW-027-8 | Tests | TestCenter | 开发者API及发现 | TCTTestAttribute/Context | 性能 | Optional（按Owner评审） | D01;D02;D03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | S1目标部分待实现 |
| ROW-028-1 | Tests | TestCenter | Worker/隔离/结果 | TCTTestExecutionCoordinator | 合同 | Required | D06;D07;D10 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | 当前CI有部分，S2握手未全实现 |
| ROW-028-2 | Tests | TestCenter | Worker/隔离/结果 | TCTTestExecutionCoordinator | 正常 | Required | D06;D07;D10 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | 当前CI有部分，S2握手未全实现 |
| ROW-028-3 | Tests | TestCenter | Worker/隔离/结果 | TCTTestExecutionCoordinator | 边界 | Required | D06;D07;D10 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | 当前CI有部分，S2握手未全实现 |
| ROW-028-4 | Tests | TestCenter | Worker/隔离/结果 | TCTTestExecutionCoordinator | 异常 | Required | D06;D07;D10 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | 当前CI有部分，S2握手未全实现 |
| ROW-028-5 | Tests | TestCenter | Worker/隔离/结果 | TCTTestExecutionCoordinator | 并发 | Optional（按Owner评审） | D06;D07;D10 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | 当前CI有部分，S2握手未全实现 |
| ROW-028-6 | Tests | TestCenter | Worker/隔离/结果 | TCTTestExecutionCoordinator | 生命周期 | Required | D06;D07;D10 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | 当前CI有部分，S2握手未全实现 |
| ROW-028-7 | Tests | TestCenter | Worker/隔离/结果 | TCTTestExecutionCoordinator | 版本 | Optional（按Owner评审） | D06;D07;D10 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | 当前CI有部分，S2握手未全实现 |
| ROW-028-8 | Tests | TestCenter | Worker/隔离/结果 | TCTTestExecutionCoordinator | 性能 | Required | D06;D07;D10 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S1/S2 | 当前CI有部分，S2握手未全实现 |
| ROW-029-1 | Tests | TestCenter | 数据Fixture与Golden | TCTTestFixtureService | 合同 | Required | D04;D11 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | Golden审签仍是目标 |
| ROW-029-2 | Tests | TestCenter | 数据Fixture与Golden | TCTTestFixtureService | 正常 | Required | D04;D11 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | Golden审签仍是目标 |
| ROW-029-3 | Tests | TestCenter | 数据Fixture与Golden | TCTTestFixtureService | 边界 | Required | D04;D11 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | Golden审签仍是目标 |
| ROW-029-4 | Tests | TestCenter | 数据Fixture与Golden | TCTTestFixtureService | 异常 | Required | D04;D11 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | Golden审签仍是目标 |
| ROW-029-5 | Tests | TestCenter | 数据Fixture与Golden | TCTTestFixtureService | 并发 | Optional（按Owner评审） | D04;D11 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | Golden审签仍是目标 |
| ROW-029-6 | Tests | TestCenter | 数据Fixture与Golden | TCTTestFixtureService | 生命周期 | Required | D04;D11 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | Golden审签仍是目标 |
| ROW-029-7 | Tests | TestCenter | 数据Fixture与Golden | TCTTestFixtureService | 版本 | Optional（按Owner评审） | D04;D11 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | Golden审签仍是目标 |
| ROW-029-8 | Tests | TestCenter | 数据Fixture与Golden | TCTTestFixtureService | 性能 | Optional（按Owner评审） | D04;D11 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | Golden审签仍是目标 |
| ROW-030-1 | Tests | TestCenter | 结构化Observation/Viewer | TCTObservationEnvelope | 合同 | Required | D08;U03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 目标未全实现 |
| ROW-030-2 | Tests | TestCenter | 结构化Observation/Viewer | TCTObservationEnvelope | 正常 | Required | D08;U03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 目标未全实现 |
| ROW-030-3 | Tests | TestCenter | 结构化Observation/Viewer | TCTObservationEnvelope | 边界 | Required | D08;U03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 目标未全实现 |
| ROW-030-4 | Tests | TestCenter | 结构化Observation/Viewer | TCTObservationEnvelope | 异常 | Required | D08;U03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 目标未全实现 |
| ROW-030-5 | Tests | TestCenter | 结构化Observation/Viewer | TCTObservationEnvelope | 并发 | Optional（按Owner评审） | D08;U03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 目标未全实现 |
| ROW-030-6 | Tests | TestCenter | 结构化Observation/Viewer | TCTObservationEnvelope | 生命周期 | Required | D08;U03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 目标未全实现 |
| ROW-030-7 | Tests | TestCenter | 结构化Observation/Viewer | TCTObservationEnvelope | 版本 | Optional（按Owner评审） | D08;U03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 目标未全实现 |
| ROW-030-8 | Tests | TestCenter | 结构化Observation/Viewer | TCTObservationEnvelope | 性能 | Optional（按Owner评审） | D08;U03 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 目标未全实现 |
| ROW-031-1 | Tests | TestCenter | 正式能力Coverage | TCTCapabilityCoverageService | 合同 | Required | D09;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 正式8维Mapping待接入 |
| ROW-031-2 | Tests | TestCenter | 正式能力Coverage | TCTCapabilityCoverageService | 正常 | Required | D09;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 正式8维Mapping待接入 |
| ROW-031-3 | Tests | TestCenter | 正式能力Coverage | TCTCapabilityCoverageService | 边界 | Required | D09;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 正式8维Mapping待接入 |
| ROW-031-4 | Tests | TestCenter | 正式能力Coverage | TCTCapabilityCoverageService | 异常 | Required | D09;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 正式8维Mapping待接入 |
| ROW-031-5 | Tests | TestCenter | 正式能力Coverage | TCTCapabilityCoverageService | 并发 | Optional（按Owner评审） | D09;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 正式8维Mapping待接入 |
| ROW-031-6 | Tests | TestCenter | 正式能力Coverage | TCTCapabilityCoverageService | 生命周期 | Required | D09;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 正式8维Mapping待接入 |
| ROW-031-7 | Tests | TestCenter | 正式能力Coverage | TCTCapabilityCoverageService | 版本 | Optional（按Owner评审） | D09;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 正式8维Mapping待接入 |
| ROW-031-8 | Tests | TestCenter | 正式能力Coverage | TCTCapabilityCoverageService | 性能 | Optional（按Owner评审） | D09;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | S3/S4 | 正式8维Mapping待接入 |
| ROW-032-1 | Tests | TestCenter | 计划/维护者使用 | TCTTestPlan | 合同 | Required | D10;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 需要实际Plan/DAG |
| ROW-032-2 | Tests | TestCenter | 计划/维护者使用 | TCTTestPlan | 正常 | Required | D10;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 需要实际Plan/DAG |
| ROW-032-3 | Tests | TestCenter | 计划/维护者使用 | TCTTestPlan | 边界 | Required | D10;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 需要实际Plan/DAG |
| ROW-032-4 | Tests | TestCenter | 计划/维护者使用 | TCTTestPlan | 异常 | Required | D10;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 需要实际Plan/DAG |
| ROW-032-5 | Tests | TestCenter | 计划/维护者使用 | TCTTestPlan | 并发 | Optional（按Owner评审） | D10;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 需要实际Plan/DAG |
| ROW-032-6 | Tests | TestCenter | 计划/维护者使用 | TCTTestPlan | 生命周期 | Required | D10;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 需要实际Plan/DAG |
| ROW-032-7 | Tests | TestCenter | 计划/维护者使用 | TCTTestPlan | 版本 | Optional（按Owner评审） | D10;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 需要实际Plan/DAG |
| ROW-032-8 | Tests | TestCenter | 计划/维护者使用 | TCTTestPlan | 性能 | Optional（按Owner评审） | D10;U09 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | Unmapped/待正式审核 | 随Owner接入 | 需要实际Plan/DAG |
| ROW-033-1 | Tests | TestCenter | Performance/长时 | TCTBenchmark* | 合同 | Required | 待Benchmark真实用例 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 非正式性能完成 |
| ROW-033-2 | Tests | TestCenter | Performance/长时 | TCTBenchmark* | 正常 | Required | 待Benchmark真实用例 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 非正式性能完成 |
| ROW-033-3 | Tests | TestCenter | Performance/长时 | TCTBenchmark* | 边界 | Required | 待Benchmark真实用例 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 非正式性能完成 |
| ROW-033-4 | Tests | TestCenter | Performance/长时 | TCTBenchmark* | 异常 | Required | 待Benchmark真实用例 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 非正式性能完成 |
| ROW-033-5 | Tests | TestCenter | Performance/长时 | TCTBenchmark* | 并发 | Optional（按Owner评审） | 待Benchmark真实用例 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 非正式性能完成 |
| ROW-033-6 | Tests | TestCenter | Performance/长时 | TCTBenchmark* | 生命周期 | Required | 待Benchmark真实用例 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 非正式性能完成 |
| ROW-033-7 | Tests | TestCenter | Performance/长时 | TCTBenchmark* | 版本 | Optional（按Owner评审） | 待Benchmark真实用例 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 非正式性能完成 |
| ROW-033-8 | Tests | TestCenter | Performance/长时 | TCTBenchmark* | 性能 | Required | 待Benchmark真实用例 | 待核准CapabilityId/ObjectId | 未关联具体AssertionId/AttemptId | 缺真实生产或测试证据 | 随Owner接入 | 非正式性能完成 |

### 09.6 最新实施验收与官方能力覆盖的严格区分（V4.1）

| 里程碑 | 截面源码与自动化实证 | GUI自动化 | 人工/现场 |
|---|---|---|---|
| M0稳定基线 | Windows完整构建/Owner依赖/隔离/并发/空选择反证：匹配Commit CI success | 无完整UIA结论 | 无工业现场结论 |
| M1开发者与证据主链 | NUnit/TCT兼容、Worker隔离、身份、Artifact、Replay既定范围通过；当前基线121/121、363/363 | 基础WPF Smoke | 非六图完整人工验收 |
| M2统一工作台 | MEM Object树、引用、只读双Revision Diff、NoEvidence、Inspector联动已落地并有WPF Smoke | **完整UIA未完成** | 六场景人工未完成 |
| M3复杂运行机制可视 | MRK部分真实黑盒测试存在；Graph/Runtime/Channel完整Viewer尚无整体验收 | 未验 | 未验 |
| M4测试管理与正式能力 | Fixture/Golden基础服务已有部分实现；Suite/Plan、Golden审批与八维正式证据矩阵未整体完成 | 未验 | 正式能力覆盖未验 |
| M5性能/HIL | MRK故障恢复已有生产黑盒测试；长期压测、正式性能基准及真实HIL未完整验收 | 未验 | 真实设备安全未验 |

原 264 行是候选 **Requirement索引**，不是已审全部官方CapabilityId/ObjectId；它们原样保留追溯，不得由121个通过的Case推断某个正式能力具备八维Verified。正式验收须有SpecVersion、批准的Capability/ObjectId、Required/NA理由、TestId/CaseId/AttemptId/AssertionId/ArtifactHash、Commit、环境与审核结论。

## 10 GitHub现状、实施阶段、Codex执行与冲突裁决

### 10.1 V4.0历史代码事实表（2026-10-16快照；V4.1实时事实见§10.9）

| 类型/能力 | GitHub `5284c8a906` 关键观察 | 规范目标与区别 |
|---|---|---|
| 项目物理结构 | `Tests/TestCenter`仅一个WPF net48项目；全解31个csproj | 两测试支撑项目原则成立，不再新建Tests应用csproj |
| 发现API | `TCTTestAttribute`仍要求4参；发现方法只支持`static void(TCTTestContext)` | 单参、Task、数据行签名S1/S3需落地 |
| Cases编译 | csproj逐文件列出8个`Cases\*.cs`，非递归Glob | S1换递归Include且去重 |
| Run/Worker | Run/Attempt/Seed、隔离Worker、报告及测试框架自测有代码 | DefinitionFingerprint/Worker完整Hello/RowHash S2补齐 |
| 证据模型 | `TCTTestObservation`仍以Kind/Title/Details/Origin文本为主 | S4结构化Envelope/Schema/Artifact，不是替代现有Observation而是兼容升级 |
| Viewer | `TCTGeometry/Object/Graph/Channel/RuntimeTestView`已有控件 | 专业结构数据/Viewport/Timeline/Source导航及动态Ribbon不足 |
| 性能/正式规格 | 一些数据快照/基础测试、CoverageService存在 | 无正式规格版本×八维AssertionEvidence验收合同 |
| 当前CI | [https://github.com/oudream/cx-visual-x3/actions/runs/38873704747](https://github.com/oudream/cx-visual-x3/actions/runs/38873704747) 与上述Commit一致，Windows工作流成功 | 仅说明现有CI覆盖的项目编译/用例通过；不宣称V4.0全阶段通过 |

**禁止导出虚假“已实现率”**：所有规划中的新类、计划管理、真实设备HIL、完整Graph/Channel业务适配，均不能因为已有类名或设计图而标记完成。

### 10.2 总体实施顺序、依赖与验收

| 包 | 目标 | 验收不可缺 |
|---|---|---|
| S0 基线 | 锁定Commit、核程序集引用、原CI报告/界面现状、250项映射 | Git SHA + 分类/Owner DAG/已有测试数、无重复源编译 |
| S1 作者API | 单参Attribute、短断言、Task发现、Cases Glob、精确单选、源码定位 | D01、D02、D03、D05、D10、D12全部正反例；GUI和CLI同Catalog |
| S2 身份隔离 | DefinitionFingerprint、CatalogSnapshot、Worker握手、Crash/Timeout与Rerun身份 | D06、错版本/Hash/Case、掉电/超时不假绿 |
| S3 参数/计划 | StableRowKey、CaseSource、Fixture、Suite/Plan、依赖DAG、Golden审批、安全预检 | D04、D07、D11，100行换序身份不变、硬件默认拒绝 |
| S4 证据/覆盖/UI | ObservationEnvelope、Artifact、正式8维Coverage、ViewerRegistry、Geometry/Object/Runtime真绘制 | D08、D09，未知Schema降级、Run/Attempt/Assertion相互定位 |
| S5 复杂模式 | 真正Graph/Channel适配、Timeline/Replay、Benchmark/长时、完整GUI交互 | 六界面人工+自动化、性能可比环境和安全责任确认 |

**硬门槛**：每包结束Windows .NET48 x64全解编译、测试隔离、并发重复、JUnit+JSON、零Case负例、失败定位、资源/Worker进程回收、未实现Owner显示缺口。下一包不得覆盖上一包历史证据；变更`TCT*`公开契约须同步类表+UI映射+矩阵。不得只凭UI图片/文件数量验收。

### 10.3 仍未定案的事项及责任

| 事项 | 决定 | 后续责任 |
|---|---|---|
| 默认开发者路径 | Attribute + Context，Fluent Builder不首发 | TestCenter API Owner |
| 单工程 | 一个WPF EXE、多个目录/Worker进程 | TestCenter负责人 |
| Framework adapter | `ITCTTestFrameworkAdapter`保留扩展合同，不声称已支持NUnit/MSTest原生Test Explorer | TestCenter负责人 |
| 真实DataHeap/DataStack | 生产Owner，测试中心只验证 | Foundation.Data / Mechanism.Data |
| 通信协议/Graph | 真实Owner先实现，TestCenter按正式观察契约接入 | Mechanism.Connectivity / Orchestration |
| 运行历史与证明 | 显式Result/Artifact/Manifest/SHA，官方规格源必须有审核版本 | QA与产品架构负责人 |
| WPF UI详细表现 | 统一Shell、一套Ribbon、一处Selection与ViewerRegistry | TestCenter UI负责人 |
| V4.0设计状态 | 本文正文与同文件附录完成不等于S1～S5代码完成 | Codex/CI/验收人 |

### 10.4 Codex可直接执行的S0～S5 TDD工作包与反例

#### 0. 每次开发前必须做

1. 查看`git rev-parse HEAD`、`AGENTS.md`、`CxVisual.Unified.sln`、`Tests/TestCenter/CxVisual.TestCenter.csproj`、最新五大工作仓库与主规格（与当前任务相关者）。
2. 对应类合同表查`Owner/裁决/阶段`；遇已存在的类型先扩展，不复制新同义服务。Foundation/Mechanism/Professional不依赖TCT。
3. 列出此次修改的源文件、具体TestIds及目标行为；区分正例和反例。迁移旧测试源码不是优先级，必须保留原用例可运行。
4. 先写失败测试、再实现目标功能，修复真实根因，不为了CI绿改弱断言。测试代码不是运行时专业能力替身。
5. 完成后执行Windows NET48 x64 Rebuild、Run TestCenter隔离/并发重复、CI故障注入及UI真实数据查看（若任务涉及UI）。

#### S0：审计和工程不变量

**实现任务**：锁定新Commit和现有用例数量；检查基础工程所有ProjectReference无反向/环；文件夹`Cases`源只有TestCenter参与编译；检查PDB/运行证据、RunReport版本。

**检查命令示意（存在的脚本名以HEAD为准）**：

```powershell
./Build/Verify-Structure.ps1
./Build/Verify-OwnerDependencies.ps1
msbuild CxVisual.Unified.sln /m:1 /t:Rebuild /p:Configuration=Debug /p:Platform=x64
./Output/Debug/x64/CxVisual.TestCenter.exe --run --report Output/Debug/x64/TestCenter.junit.xml --format junit --isolate
```

**反例**：Foundation/Mechanism依赖Tests，重复csproj Include，未通过CI也标Completed。

#### S1：开发者能否简单使用（必须先做）

##### S1.1 目标API

- `TCTTestAttribute(string id)`保持旧4参重载；默认DisplayName=方法名，Aspect/Owner缺省Unknown，不虚构正式CapabilityId。
- `TCTTestContext.AssertTrue(bool,string)` / `AssertFalse(bool,string)`保留原重载；`AssertEqual`等延用唯一Collector。
- Attribute Adapter支持`static void/Task(TCTTestContext)`与真实await；拒绝`async void`、`Task<T>`、实例/开放泛型方法、无Context的非法签名。
- `Cases/**/*.cs`在传统csproj中**一个**glob Include且取消原独立Case Compile；开发者添加/移动/删除Case源不用编辑Runner或csproj。
- `--id`显式单选先Resolve然后Preflight，不受默认Smoke Kind筛选；未知ID/0 Case非0退出，不得只生成0 Case报告后返回绿灯。

##### S1.2 必须增加的反例TestId（示意ID，先查Catalog冲突）

| 场景 | Given / When | Then |
|---|---|---|
| D01 | 在Cases子目录新增`TCTSampleOne.cs`+单参Attribute | MSBuild自动包含、GUI/CLI发现同TestId；无需编辑TCTRegistration |
| D02 | `async Task` 内真实await后抛自定义异常 | 保留异常类型和堆栈，Result=Error，不误为TargetInvocationException |
| D03 | 显式指定Kind=Benchmark且默认Profile=Smoke | 直接执行该Case或由环境/安全预检明确拒绝，不能零用例成功 |
| D05 | 一个Expected≠Actual | 断言唯一失败、数据可见、PDB若有跳准确行 |
| D10 | TestId拼错/重复ID/0个匹配 | 发现/预检明确错误；CI非0退出 |
| D12 | 把Case移动至Cases另子目录，再删除 | 不残留旧编译项、不把定义保留在已构建Catalog |

验收：普通开发者只看一页QuickStart，在不改Runner/Host/Report源码情况下完成以上任务。旧用例仍通过；Windows启动WPF，实际Runner与CLI用同Catalog。

#### S2：身份、Worker、失败定位的唯一证据链

- Catalog冻结`DefinitionFingerprint`、AssemblyMVID、MethodSig、FixtureVersion/Hash；Worker在执行前校验握手（协议版、CaseId、RowHash、Architecture、Commit）。
- 只传可重建的CaseId，不允许仅主进程注册的闭包绕过Worker规则；短时断开/崩溃/残留进程要可定位。
- 独立`RunId/AttemptId`、错误优先级、生命周期、父AttemptId、不可变历史；OneAttemptOnceFinalized。
- `Rerun`必须明确原始数据/当前数据选择，不可修改旧Run；若DefinitionHash不可用，返回NotReproducible。
- 断言SourceLocation从PDB/真实异常栈取，缺符号显示SourceUnavailable。

反例：替换Worker二进制、修改Fixture一字节、Worker无响应、进程退出无Result、Teardown异常、AttemptId交换。全都不能Passed。

#### S3：稳定参数行、Plan/Suite与安全预检

- `TCTCaseSourceAttribute`读版本化只读Manifest，稳定RowKey，RowHash/Schema校验；100行换序仍一一对应；可按CaseId单行重跑。
- Plan是不可变定义，Suite是引用组；Plan DAG循环/缺引用拒绝，RunRequest只承载本次执行参数。依赖/配额/资源门不重造MRK业务编排。
- Golden审批Propose→Review→Approve→Commit，留下签字/版本/Hash证据；未审批不修改。
- `Real`设备动作必须明确授权/安全Owner Interlock，默认Virtual/Replay；不因GUI“Run All”/`--inproc`绕过。

反例：重复RowKey/缺Fixture、篡改Baseline、DAG环、真实X-Ray默认未授权、资源死锁，必须显式Error/Blocked。

#### S4：结构化证据、完整Coverage与Viewer

- 新增统一ObservationEnvelope + Schema registry，保留旧文本Observation兼容转换，只能标LegacyText而非假结构化事件。
- 限速/backpressure、Artifact Hash/Quota、Attempt Sequence校验；不让WPF主线程解码GB级Data。
- `TCTTestViewerRegistry`用只读DTO呈现Geometry/Object/Runtime三类真实观察；源码定位、Timeline、历史Diff归同一Attempt。
- 正式Coverage按审核SpecVersion/CapabilityId/ObjectId/RequirementDimension映射到AssertionEvidence；Required八维齐才Verified，缺维Partial，Unmapped不可算覆盖率。

反例：未知Schema/来源、乱序/重复Seq、Hash篡改、3方Origin混淆、Required维缺证据、跨Commit不可比；界面不能因此假绿。

#### S5：全UI及高级执行、性能/工业适配

- 根据第08章完成DynamicRibbon/CommandRouter/唯一UI Selection、Fixture/Plan/History/Coverage工具页、六种Viewer模式的真实数据绑定。
- Graph与Channel只通过对应机制Owner真实Provider进入；缺Provider明确Unsupported/NoEvidence，不创建测试版Graph/Modbus生产库。
- Benchmark/Stress数据保存SampleCount、P50/P95/P99、内存/CPU/GPU、环境/Commit；环境不可比不出回归判定。
- WPF UI自动化：DPI 100～200%、键盘/Tab、无障碍标签、多Tab、异步关闭/取消、千万级事件分页与性能；真实截图与六原图比对。

#### 所有工作包的提交证据

| 证据 | 最低要求 |
|---|---|
| Commit | 中文说明、master最新合流，附不变量检查 |
| Build | 完整NET48/x64 Debug/Release所需配置的MSBuild结果，程序集引用/DAG不变 |
| Testing | JUnit/JSON中RunId/AttemptId、实际用例数、唯一身份、全部反例、0用例非0退出 |
| Artifacts | Schema版本、FixtureHash、DefinitionHash、环境Git SHA、Artifact Hash与加载校验 |
| UI | 真实Owner Observation/SourceLocation/无证据空态、界面截图与操作录像（若有） |
| Coverage | 能力规格版本、Required维度、未映射清单、对应Case/Assertion证据 |
| Ownership | Production→Tests零依赖，Infra仅确证公用服务，不拆第2个TCT项目 |

禁止：先机械新建250空类、将已有Worker/Reporter改名重复实现、以通过测试数伪造能力覆盖、把概念截图中Mock帧和数字写进CI报告。

### 10.5 V2.0～V3.2历史冲突的唯一裁决（15项）

| 冲突编号 | 旧表述A | 旧表述B | V4.0唯一裁决 | 影响文件/验收 |
|---|---|---|---|---|
| ADR-T01 | 公开Fluent `TCTTest.Define().Build()` | Attribute + Context | **默认且唯一**Attribute/Context；Builder有真实跨进程重建需求再审批 | 01/02；D01-D02 |
| ADR-T02 | 通用公共接口统一迁Infrastructure | 专业/机制接口留Owner | V2.1的Owner规则优先；Infra仅FLD/FCR/确证公用服务 | 01/02/04 Owner DAG |
| ADR-T03 | “单个测试”泛指TestId | 参数行/重复/Retry需要细化身份 | TestId→CaseId→RunId→AttemptId；Iteration/RetryIndex不拼凑TestId | 01/02/04；D04/D06 |
| ADR-T04 | Smoke Kind过滤一切请求 | GUI/CLI显式选中任意Kind | 精确ID先解析，再做**安全预检**；Kind批量过滤只对非显式选择生效 | S1/D03 |
| ADR-T05 | 传统csproj逐个Include | 开发者新增文件无需修改项目 | 仅Cases目录采用受限Glob，移除旧独立Case Compile | S1/D01/D12 |
| ADR-T06 | `void(TCTTestContext)` | 支持`async Task` | 同Attribute完整支持同步/异步；Parameter Case带独立源合同 | S1/D02 |
| ADR-T07 | String Details拼Object树/Geometry点 | 真实结构化Observation | 升级Envelope/SchemaVersion/Artifact，旧文本作为Legacy观察模式 | S4/U03/U06 |
| ADR-T08 | 六张屏幕=六种测试类型 | 仅六种典型Viewer工作模式 | 一个Shell+不同Viewer+额外Plan/History/Coverage/Benchmark文档 | 03/U01/U09 |
| ADR-T09 | 旧独立Graph/SymbolDefinition命名 | 后续统一对象与编排Owner | Owner对象真实现：Ownership≠Reference≠Connection，Graph为生产Orchestration/Runtime映射 | 03/U06/U07 |
| ADR-T10 | Mock“PASS”图 | 正式证据与真实执行 | 图仅作视觉参照，没生产Channel接口不可标TX/RX成功 | 03/04 Evidence |
| ADR-T11 | “测试用例通过率”作为“能力覆盖率” | 八维Required+Capability/Object正式ID | 只有已审规格×真实AssertionEvidence齐备为Verified；Unmapped/Partial不算覆盖 | 04/D09 |
| ADR-T12 | Worker Kill代表运行停止 | 真实设备独立安全回路 | 只能表示测试进程停止，Real设备必须外部授权/Interlock | S3/D11 |
| ADR-T13 | V3.0写144拟新增 | 250条中拟新增145 | V3.0原状态原文和V3.2 250裁决两套分类分别保存，不混算 | 02/04数字核验 |
| ADR-T14 | 摘要称核心契约39 | 实际V3.2附录C有43行 | 以源文本**43条**可核合同为准，不杜撰丢失或重复 | 02/04合同表 |
| ADR-T15 | V2.0开发时还无真实隔离Host | GitHub现有Worker/JUnit/隔离成功CI | 当前状态按`5284c8a906`快照；旧文档只作为历史演化说明 | 01/04/CI |

### 10.6 原S0～S5工作包历史状态（不得覆盖新的M0～M5台账）

| 工作ID | 工作项 | 唯一Owner/模块 | 验收关联 | 阶段 | 必须验证的反例 | 执行状态 |
| --- | --- | --- | --- | --- | --- | --- |
| S0-01 | 最新HEAD/Owner核对 | 生产Owner依赖与源码基线 | Git SHA、DAG、工程文件清单 | S0 | 阻止重复Project/反向引用 | 待实际提交验收 |
| S1-01 | Attribute单参+旧签名兼容 | Discovery | D01;D12 | S1 | 编译旧用例+新用例，重复ID拒绝 | 待实际提交验收 |
| S1-02 | Task async自然发现/await | Discovery/Runner | D02 | S1 | 异常栈真实，禁止async void | 待实际提交验收 |
| S1-03 | Cases Glob+源码定位 | csproj/SourceLocation | D01;D05;D12 | S1 | 增删移不改csproj | 待实际提交验收 |
| S1-04 | 显式单选优先级 | Catalog/Request/Preflight | D03;D10 | S1 | 不可绕过危险授权 | 待实际提交验收 |
| S2-01 | CatalogSnapshot/Fingerprint | Discovery/Worker | D06 | S2 | MVID/RowHash错版拒绝 | 待实际提交验收 |
| S2-02 | Attempt/Retry/Crash协议 | Execution/Reports | D06;D07;D10 | S2 | 无结果/泄漏不能Passed | 待实际提交验收 |
| S3-01 | CaseSource/Fixture/RowKey | Fixture/Dataset | D04 | S3 | 100行换序身份稳定 | 待实际提交验收 |
| S3-02 | Suite/Plan+DAG/安全 | Plan/Preflight | D07;D11 | S3 | 循环计划与Real默认阻断 | 待实际提交验收 |
| S3-03 | Golden Propose→Approve | Fixture/Baseline | D04;D11 | S3 | 无审签不改Expected | 待实际提交验收 |
| S4-01 | ObservationEnvelope/Artifact | Diagnostics/Viewer | D08;U03;U06 | S4 | 未知Schema/背压证据保留 | 待实际提交验收 |
| S4-02 | Coverage八维+Historical Diff | Coverage/Reports | D09;U09 | S4 | Unmapped/Partial与不可比原因 | 待实际提交验收 |
| S4-03 | Shell/CommandRouter+真数据 | UI/ViewerRegistry | U01;U02;U05;U06 | S4 | 复用一个主窗、真实Selection/源码跳转 | 待实际提交验收 |
| S5-01 | Graph/Channel真实Provider | Mechanism Owners + Viewer | U07 | S5 | 没有真实生产Provider先NoEvidence | 待实际提交验收 |
| S5-02 | Perf/Stress/GUI自动化 | Benchmark/UI | U08;U10 | S5 | P95样本/环境一致性、DPI键盘验证 | 待实际提交验收 |

### 10.7 V4.0原始来源及CI（历史快照）

| 项目 | 值/数量 | 含义 | 来源/参考 |
| --- | --- | --- | --- |
| Repository | oudream/cx-visual-x3 | 官方源码 | https://github.com/oudream/cx-visual-x3 |
| Source HEAD | 5284c8a9069273882cae72cb8bd66983cb98e140 | 此次静态审查源码版本 | https://github.com/oudream/cx-visual-x3/tree/5284c8a9069273882cae72cb8bd66983cb98e140 |
| Windows CI run | 38873704747 | 对应同一SHA且最终success | https://github.com/oudream/cx-visual-x3/actions/runs/38873704747 |
| CI gates | 项目结构/Owner DAG/NET48 x64编译/隔离测试/3轮并发/空选择 | 本次已完成的既有CI流程 | 不能推断新S1-S5已过 |
| Original UI | 6 x 2560×1440 | 概念图片（含模拟数据） | assets/ |
| Class decisions | 250 unique IDs | V3.2附录A逐项审计 | 02类型合同 |
| Extra concepts | 10 | V3.2附录B目标合同 | 03新增十项合同 |
| Detailed contracts | 43 | V3.2附录C文本实际条数 | 02核心43合同 |
| Formal capability IDs | 未提供审核版全量映射 | 不得据TestIds自行编造正式编号 | 04_能力八维验收 |
| V4.0 implementation | 尚未开始按S1-S5完整验收 | 现有底座与新增目标严格区分 | 08_Codex工作包 |

### 10.8 V4.0长期维护规则（现行补充优先见§10.9～10.12）

- **唯一更新对象**：修改API、Owner、类合并、Runner或Viewer契约，只提交对本文件相应章节和附录的更新；不再维护多个“均为最新”的并行Markdown。
- **源码事实绑定提交**：任何“已实现/CI已通过”须对应确切CommitSHA、RunId/AttemptId、JUnit/JSON及UI实操证据；master更新后不能复用旧完成率。
- **保留历史编号**：250项类型即便合并/暂缓也保留历史ID及去向，不能删除来缩小规格。
- **界面源图不篡改**：旧SymbolDefinition/Graph名称留在视觉稿，文本合同按Owner真实模型纠正。
- **Codex顺序**：先S1做开发者最短闭环，再做S2定义身份/Worker，再做S3参数化与Plan，再做S4证据与Coverage，最后S5完整Viewer/Perf/HIL；阶段完成必须有失败反例。
- **上线门槛**：系统级硬件安全由现场Owner验收，TestCenter不能因CI绿而承诺生产现场安全。

### 10.9 V4.1现行代码基线、任务与旧阶段映射

本版审阅的源码事实：`master` 于文档编写过程中持续推进；**已确定同一提交成功**的TestCenter基线是 `16482d6810cc093ff480a82691c67b1ace888441`，对应 [Windows CI 39410511642](https://github.com/oudream/cx-visual-x3/actions/runs/39410511642)：结构及DAG、NET48/x64、WPF Smoke、NUnit/Worker、Artifact/Replay、空选择、121例隔离全部通过，三轮363次并发全部成功。最新代码SHA和CI必须在执行时刷新，不能按本文自动认定后续Commit均通过。

**唯一实施工作包台账：TCT-00～16（17项）；六个里程碑 M0～M5。**

| 里程碑 | 工作包 | 唯一交付对象与放行标准 | 本版验证结论 |
|---|---|---|---|
| M0 | TCT-00、TCT-16持续 | Windows基线/项目DAG/中文Commit及变更治理 | 匹配SHA自动化已通过；持续运行 |
| M1 | TCT-01～TCT-05 | 标准NUnit/TCT发现与兼容、Worker/Attempt、结构化Observation、Artifact、Replay正反证 | **既定自动化范围完成**；遇新增合同仍需回归 |
| M2 | TCT-06/07、TCT-14首批 | 真正动态WPF Shell与真实Geometry/Object、Expected/Actual/Diff、Inspector/Timeline/History/重跑、完整UIA | **部分完成**，MEM Object/Revision差异Smoke已通过，完整UIA与六图人工未完成 |
| M3 | TCT-08/09 | 真Graph/Port/Compile、MRK RuntimeInstance/Resource/Journal、Channel真实Frame/Session专业Viewer | **未整体验收**，部分MRK黑盒底座已具备 |
| M4 | TCT-10/11/12、TCT-14完整 | Suite/Plan、Fixture/Golden审签、八维正式Capability证据、管理界面 | **未整体验收** |
| M5 | TCT-13/15/16持续 | Benchmark、P50/P95/P99、长稳、专业六Workbench、受控HIL | **未整体验收**，现场安全单列 |

**旧S阶段映射规则**：S0→M0；S1/S2→M1；S3中的Fixture/Plan/Golden→M4；S4中的Evidence→M1、Viewer→M2、Coverage→M4；S5中的Graph/Runtime/Channel→M3，性能/HIL→M5。旧S0～S5表不再作为实时工作进度，继续留档用于250项类型时序来源。

### 10.10 V4.1新增五项唯一架构裁决

| 裁决 | 历史冲突 | 现行唯一规则 |
|---|---|---|
| ADR-T16 | 原V4.0“普通作者只用TCT Attribute”与实际NUnit M1体验 | **一般作者使用标准NUnit**，专业结构化Owner用TCT兼容入口；统一同一个Catalog/Worker/Result，禁止第二套断言系统 |
| ADR-T17 | 旧CxNode/CxProjectDocument与新版MEM/MRC/MPC组织矛盾 | 当前 `MEMNode/MEMEngineeringSession/MEMProjectSnapshot/MRIObjectAddress` 为测试的工程组织真源，Ownership ≠ Reference ≠ Connection；旧名仅作历史说明 |
| ADR-T18 | S0～S5旧进度与TCT-00～16/M0～M5现行台账不一致 | 动态进度只在TCT-00～16台账；S阶段历史保留而不得篡改已验收事实 |
| ADR-T19 | 六设计稿被误读成六个界面程序或者真实执行证据 | 只有一个TestCenter WPF项目/Window，共享Shell/Selection/Runner，六图供视觉参考，必须真实Owner绑定并独立UIA/人工验收 |
| ADR-T20 | 测试中心自行重写Logic/Graph/Runtime/Channel | MEM/MOL/MPC/MOC/MRK及Connectivity的真实生产Owner提供定义、执行和Probe；TestCenter只采集/校验/展示，不产生第二套生产真源 |

### 10.11 后续连续开发与可检验命令（Codex/开发者适用）

开发前读取 `AGENTS.md`、`.agents/skills/cxvisual-engineering/SKILL.md`、`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md`、四大架构基线、本V4.1与TCT全局台账。以当下 `master` 的源代码/Windows CI判定状态，不凭旧数字或类名。直接master中文Commit，少量有意义提交；并行开发重新读取HEAD，绝不强推覆盖其他人的提交；保持TestCenter原有单项目与生产Owner依赖边界。

优先先做完 **M2完整闭环**：真实Geometry的Expected/Actual/Delta/Viewport，MEM对象Reference/实例选中、高亮与Diff、Explorer/Inspector/Timeline联动、历史重开、重跑、失败源码定位、动态Ribbon/布局；补齐Windows UIA、人工六图截图。随后按生产Owner真正可用的MOL/MPC→构建/SourceMap→MRK与真实Channel/Protocol扩展M3 Viewer。M4完成Suite/Plan/Fixture/Golden审批及正式八维能力矩阵。M5完成长期性能、可靠性、专业安全HIL。

每项必须有正常/异常的TestId、Owner源文件、Attempt/Artifact、Commit和匹配CI，WPF操作涉及UIA/人工验收时须额外记录。系统只可标：**设计已定 / 代码已实现 / 编译已通过 / 自动测试已通过 / WPF UIA已通过 / 人工界面已验收 / 工业现场已验收**；绝不互相替代。Golden未经批准不得更新；HIL无生产安全Owner授权不得启动。

### 10.12 六个可执行的真实端到端验收场景

| 场景 | 真实正例 | 必须反例/阻断 | 最终UI标准 |
|---|---|---|---|
| A 标准NUnit | 新增Cases源→自动发现→选中→Worker→JUnit/History | 重名/坏签名、空选、失败Assert不得Passed | Explorer选同一Case、Results显示原Attempt与错误/源码 |
| B MEM组织实例 | Project/Page/Content/独立Symbol/两个Reference/Element/Port→快照 | Ownership循环、坏Ref、跨Attempt、SHA篡改 | 树与引用关系可见，InstancePath正确，选择联动Inspector |
| C 编辑和双Revision | Preview/Commit/Undo/Redo/持久化重开→Before/After快照 | 不同Project不可比、旧Snapshot不改、未审批Golden不更新 | 真节点差异/版本/证据回查与历史重开 |
| D Geometry | 真实坐标/变换→Expected/Actual/误差→画布操作 | NaN/Inf、坐标/单位错、缺Schema/坏Artifact | 缩放/标尺/选择/Delta与Assertion/Source联动 |
| E MOL/MPC→MRK | 定义→Compile/SourceMap→真实Plan/ExecutionInstance/Journal | 非法Port、Unknown/等待/超时不得伪Completed | Graph和Runtime相互跳转定义/运行实例；无Owner时NoEvidence |
| F Channel/工业 | Virtual或Replay的真实Frame/Codec/Session→错误注入与回放 | 无SDK/未授权Real/帧损坏/错误Correlation | Hex/Decode/Timeline来自生产Provider，真实设备独立审批 |

上述场景是验收标准，不是一次性完成状态。每项有源代码、测试和反证、Windows CI、UIA与人工/现场适用证据，才可以更改台账状态。

## 附录A：原250项类/接口/枚举逐条裁决（规范性，不可省略）

本附录与第06章属于**同一个文件**，保留ID 1～250、V3.0原单一职责与V3.2裁决去向。分类：保留现有61、保留扩展37、必要新增91、合并45、暂缓9、条件保留/暂缓1、条件暂缓6；合计250。原V3.0对“拟新增”条目的计数为145而非摘要中的144，属于不同于裁决统计的分类口径。

下表保持原ID、名称、形态、原状态、最终裁决、归属、时序、去向及原类别；**每一行都是可跟踪裁决**，不意味着每行必然一个源文件。

| ID | 原始模块 | 类型/接口/枚举 | 形态 | 原状态 | V4.0承接裁决 | Owner | 时序 | V3.0原单一职责 | V3.2裁决去向/边界 |
|---:|---|---|---|---|---|---|---|---|---|
| 1 | I. Foundation.Infrastructure | `IFLDLogSink` | 接口 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 日志输出扩展契约 | 沿用生产Owner；不新增TCT依赖 |
| 2 | I. Foundation.Infrastructure | `FLDLogService` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 有界异步日志、Flush/Dispose | 沿用生产Owner；不新增TCT依赖 |
| 3 | I. Foundation.Infrastructure | `FLDDedicatedLogger` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 独立业务日志入口 | 沿用生产Owner；不新增TCT依赖 |
| 4 | I. Foundation.Infrastructure | `FLDLogRecord` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 日志记录值对象 | 沿用生产Owner；不新增TCT依赖 |
| 5 | I. Foundation.Infrastructure | `FLDLogLevel` | 枚举 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 日志级别 | 沿用生产Owner；不新增TCT依赖 |
| 6 | I. Foundation.Infrastructure | `FLDLOG_TYPE` | 枚举/类型 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 旧日志类型兼容 | 沿用生产Owner；不新增TCT依赖 |
| 7 | I. Foundation.Infrastructure | `FLDLogScope` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 日志关联作用域 | 沿用生产Owner；不新增TCT依赖 |
| 8 | I. Foundation.Infrastructure | `FLDLogHelper` | 静态类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 日志便捷方法 | 沿用生产Owner；不新增TCT依赖 |
| 9 | I. Foundation.Infrastructure | `FLDEventLogHelper` | 类/静态类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 事件日志便捷接入 | 沿用生产Owner；不新增TCT依赖 |
| 10 | I. Foundation.Infrastructure | `FLDImportantEventLog` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 重要事件记录 | 沿用生产Owner；不新增TCT依赖 |
| 11 | I. Foundation.Infrastructure | `FLDImportantEventLogEntry` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 重要事件条目 | 沿用生产Owner；不新增TCT依赖 |
| 12 | I. Foundation.Infrastructure | `FLDMemoryLogSink` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 内存日志目标 | 沿用生产Owner；不新增TCT依赖 |
| 13 | I. Foundation.Infrastructure | `FLDFileLogSink` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 文件日志目标 | 沿用生产Owner；不新增TCT依赖 |
| 14 | I. Foundation.Infrastructure | `FLDRollingFileLogSink` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 日志滚动 | 沿用生产Owner；不新增TCT依赖 |
| 15 | I. Foundation.Infrastructure | `FCRWorker` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 通用有界并发Worker | 沿用生产Owner；不新增TCT依赖 |
| 16 | I. Foundation.Infrastructure | `FCRWorkerOptions` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | Worker并发/容量配置 | 沿用生产Owner；不新增TCT依赖 |
| 17 | I. Foundation.Infrastructure | `FCRWorkHandle<T>` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 工作票据及完成状态 | 沿用生产Owner；不新增TCT依赖 |
| 18 | I. Foundation.Infrastructure | `FCRWorkResult<T>` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 工作结果 | 沿用生产Owner；不新增TCT依赖 |
| 19 | I. Foundation.Infrastructure | `FCRWaitObservation<T>` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 等待/观察结果 | 沿用生产Owner；不新增TCT依赖 |
| 20 | I. Foundation.Infrastructure | `FCRWorkState` | 枚举 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 工作生命周期 | 沿用生产Owner；不新增TCT依赖 |
| 21 | I. Foundation.Infrastructure | `FCROverflowPolicy` | 枚举 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 队列溢出策略 | 沿用生产Owner；不新增TCT依赖 |
| 22 | I. Foundation.Infrastructure | `FCRWorkerSnapshot` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 运行队列与吞吐快照 | 沿用生产Owner；不新增TCT依赖 |
| 23 | I. Foundation.Infrastructure | `FCRAsyncGate` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 通用异步门 | 沿用生产Owner；不新增TCT依赖 |
| 24 | I. Foundation.Infrastructure | `FCRPeriodicJob` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 定期任务调度 | 沿用生产Owner；不新增TCT依赖 |
| 25 | I. Foundation.Infrastructure | `FCRSharedLease<T>` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 跨使用方共享资源引用期 | 沿用生产Owner；不新增TCT依赖 |
| 26 | I. Foundation.Infrastructure | `FCRDedicatedExecutor` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 固定专用执行器 | 沿用生产Owner；不新增TCT依赖 |
| 27 | I. Foundation.Infrastructure | `FCRSystemMonitor` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 系统负载采集 | 沿用生产Owner；不新增TCT依赖 |
| 28 | I. Foundation.Infrastructure | `FCRSystemSnapshot` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 系统采样快照 | 沿用生产Owner；不新增TCT依赖 |
| 29 | I. Foundation.Infrastructure | `FCRProcessSnapshot` | 类 | 现有 | 保留现有 | Foundation.Infrastructure | 现有 | 进程性能快照 | 沿用生产Owner；不新增TCT依赖 |
| 30 | II. Foundation.Infrastructure | `IFCRClock` | 接口 | 候选 | 条件暂缓 | Foundation.Infrastructure | 条件P2 | 时间读取/单调计时最小契约；确认Runtime/Infra皆需后建设 | 有两个独立生产需求且边界核实后才进Infrastructure |
| 31 | II. Foundation.Infrastructure | `FCRSystemClock` | 类 | 候选 | 条件暂缓 | Foundation.Infrastructure | 条件P2 | IFCRClock系统实现；不涉及测试虚拟时间 | 有两个独立生产需求且边界核实后才进Infrastructure |
| 32 | II. Foundation.Infrastructure | `IFCRProcessSupervisor` | 接口 | 候选 | 条件暂缓 | Foundation.Infrastructure | 条件P2 | 通用进程启动/树回收；出现两处以上生产复用后再下沉 | 有两个独立生产需求且边界核实后才进Infrastructure |
| 33 | II. Foundation.Infrastructure | `FCRProcessSupervisor` | 类 | 候选 | 条件暂缓 | Foundation.Infrastructure | 条件P2 | 跨生产宿主的可靠进程监督；Windows Job机制不宣称已存在 | 有两个独立生产需求且边界核实后才进Infrastructure |
| 34 | II. Foundation.Infrastructure | `FCRProcessStartOptions` | 类 | 候选 | 条件暂缓 | Foundation.Infrastructure | 条件P2 | 通用进程启动与资源限额参数 | 有两个独立生产需求且边界核实后才进Infrastructure |
| 35 | II. Foundation.Infrastructure | `FCRProcessExitSnapshot` | 类 | 候选 | 条件暂缓 | Foundation.Infrastructure | 条件P2 | 进程树退出、遗留进程与清理证据 | 有两个独立生产需求且边界核实后才进Infrastructure |
| 36 | III. TestCenter | `TCTTestAttribute` | 特性类 | 现有 | 保留现有 | CxVisual.TestCenter | P0 | 一方法标记TestId、类别、能力映射与隔离要求 | 维持当前唯一实现与Owner |
| 37 | III. TestCenter | `TCTTest` | 静态类 | 新增 | 暂缓独立类型 | CxVisual.TestCenter | P2 | 最简注册和Define入口；不是第二个Runner | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 38 | III. TestCenter | `TCTTestCaseBuilder` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 构造测试元数据、Arrange/Fixture/Owner，Act生成泛型阶段 | TCTTestAttribute + TCTTestContext（不增加Builder） |
| 39 | III. TestCenter | `TCTTestCaseBuilder<TResult>` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Typed Actual + Assert/Observe，最终Build为一个标准Case | TCTTestAttribute + TCTTestContext（无额外Result真源） |
| 40 | III. TestCenter | `TCTParameterizedCaseBuilder` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 把表格/参数组合生成稳定带参数ID的Case | TCTCaseSourceAttribute + TCTCaseDataSet（仅参数合同） |
| 41 | III. TestCenter | `TCTScenarioBuilder` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 跨多个实际Owner操作的Given/When/Then场景编排 | TCTTestContext + 普通C# async/using + ITCTTestLifecycle |
| 42 | III. TestCenter | `TCTTestPlanBuilder` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 把选中用例、Fixture、Profile、门槛组成运行计划 | TCTTestPlan加载/保存（Builder非必要） |
| 43 | III. TestCenter | `TCTTestContext` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | P0 | 每Attempt上下文、断言、观察与证据单入口 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 44 | III. TestCenter | `TCTRegistration` | 静态类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 单处组合真实Catalog；不新增第二份注册真源 | 维持当前唯一实现与Owner |
| 45 | III. TestCenter | `TCTTestCaseDescriptor` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | P0 | 不可变TestId、能力ID、ObjectId、Kind、资源/风险/来源 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 46 | III. TestCenter | `TCTTestCaseParameterSet` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | P1 | 一组确定性输入、Seed、Fixture版本和参数哈希 | TCTCaseDataSet / TCTCaseRow（建议正式命名，统一StableRowKey） |
| 47 | III. TestCenter | `TCTTestCaseSource` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P0 | 类/方法/程序集/提交/源码位置可追溯 | 类/方法/程序集/提交/源码位置可追溯 |
| 48 | III. TestCenter | `TCTTestRequirement` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 所需OS/SDK/GPU/设备、安全等级/实现可用性 | 所需OS/SDK/GPU/设备、安全等级/实现可用性 |
| 49 | IV. TestCenter | `ITCTTestCase` | 接口 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 同步用例公开契约 | 维持当前唯一实现与Owner |
| 50 | IV. TestCenter | `ITCTAsyncTestCase` | 接口 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 非阻塞测试接口；由Adapter接入单Runner | 维持当前唯一实现与Owner |
| 51 | IV. TestCenter | `ITCTTestLifecycle` | 接口 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | SetupAsync/TeardownAsync独立生命周期 | 维持当前唯一实现与Owner |
| 52 | IV. TestCenter | `ITCTTestDiscoverer` | 接口 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 发现来源扩展 | 维持当前唯一实现与Owner |
| 53 | IV. TestCenter | `ITCTTestFrameworkAdapter` | 接口 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | NUnit/MSTest等外部测试适配 | 维持当前唯一实现与Owner |
| 54 | IV. TestCenter | `ITCTTestPlanSource` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 代码/JSON/工程资源的测试计划来源 | 代码/JSON/工程资源的测试计划来源 |
| 55 | IV. TestCenter | `ITCTTestRequirementEvaluator` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 解析依赖是否满足；不自动访问生产设备 | 解析依赖是否满足；不自动访问生产设备 |
| 56 | IV. TestCenter | `TCTTestCatalog` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | P0 | 注册、查询、重复ID拒绝、映射关联 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 57 | IV. TestCenter | `TCTReflectionTestDiscoverer` | 类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 程序集反射发现 | 维持当前唯一实现与Owner |
| 58 | IV. TestCenter | `TCTTestDiscoveryService` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 发现合并、冲突诊断、增量刷新 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 59 | IV. TestCenter | `TCTAttributeFrameworkAdapter` | 类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 现有特性适配 | 维持当前唯一实现与Owner |
| 60 | IV. TestCenter | `TCTAttributeTestCase` | 类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | Method到ITCTTestCase适配 | 维持当前唯一实现与Owner |
| 61 | IV. TestCenter | `TCTAsyncTestCaseAdapter` | 类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | Async Case到执行引擎适配 | 维持当前唯一实现与Owner |
| 62 | IV. TestCenter | `TCTTestCatalogSnapshot` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 目录版本一致性、不可变快照 | 目录版本一致性、不可变快照 |
| 63 | IV. TestCenter | `TCTTestQuery` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P0 | 按Owner/能力/对象/Kind/Tag/状态检索 | 按Owner/能力/对象/Kind/Tag/状态检索 |
| 64 | IV. TestCenter | `TCTTestFilter` | 静态类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 无副作用筛选；不得让UI重写筛选语义 | TCTTestQuery（单一查询语义） |
| 65 | IV. TestCenter | `TCTTestOwnerMapping` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 测试ID到被测Owner项目/对象/接口版本的映射 | TCTTestCaseDescriptor.Owner（正式能力映射由Coverage Catalog处理） |
| 66 | IV. TestCenter | `TCTTestAvailability` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 已接入、缺SDK、缺能力、需设备授权的明确可运行状态 | TCTTestRequirementEvaluator结果（Availability只读，不独立服务） |
| 67 | V. TestCenter | `TCTTestSuite` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 复用的测试分组/嵌套Suite，允许跨Owner | 复用的测试分组/嵌套Suite，允许跨Owner |
| 68 | V. TestCenter | `TCTTestPlan` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 一次可保存的测试计划定义，不等于Run实例 | 一次可保存的测试计划定义，不等于Run实例 |
| 69 | V. TestCenter | `TCTTestPlanStep` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Case/Suite引用、参数、依赖与执行门槛 | Case/Suite引用、参数、依赖与执行门槛 |
| 70 | V. TestCenter | `TCTTestDependency` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 前置成功、资源就绪、Fixture依赖 | 前置成功、资源就绪、Fixture依赖 |
| 71 | V. TestCenter | `TCTTestSelection` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 选中的Definition集合和快照版本 | TCTTestRunRequest.SelectedCases（不可变选择快照） |
| 72 | V. TestCenter | `TCTTestRunRequest` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | P0 | 执行期参数、Repeat、Parallel、Seed、隔离、选择 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 73 | V. TestCenter | `TCTTestExecutionPolicy` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P0 | 隔离/失败门槛/超时/资源/Retry边界的归一化策略 | 隔离/失败门槛/超时/资源/Retry边界的归一化策略 |
| 74 | V. TestCenter | `TCTTestRetryPolicy` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 明确自动重试次数、退避、是否只重试宿主错误；保留所有Attempt | 明确自动重试次数、退避、是否只重试宿主错误；保留所有Attempt |
| 75 | V. TestCenter | `TCTTestSchedule` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 选中集合拓扑排序、资源限制与排队理由 | TCTTestExecutionPolicy + Coordinator（实际需要再分离） |
| 76 | V. TestCenter | `TCTTestResourceRequirement` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | GPU/端口/文件/设备占用键和容量声明 | TCTTestCaseDescriptor.Requirements/ExclusiveResourceKeys（现有资源门） |
| 77 | V. TestCenter | `TCTTestSafetyPolicy` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P0 | Simulation/Real端点白名单、操作前授权和紧急条件 | Simulation/Real端点白名单、操作前授权和紧急条件 |
| 78 | V. TestCenter | `TCTTestRunProfile` | 枚举 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | Dev/Smoke/Regression/Stress/Benchmark/CI | 维持当前唯一实现与Owner |
| 79 | V. TestCenter | `TCTTestKind` | 枚举 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | Unit/Integration/Regression/Stress/Benchmark | 维持当前唯一实现与Owner |
| 80 | V. TestCenter | `TCTTestIsolation` | 枚举 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | InProcess/ChildProcess；建议增加Auto、STA专用策略 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 81 | V. TestCenter | `TCTTestPlanStepKind` | 枚举 | 新增 | 条件保留/暂缓 | CxVisual.TestCenter | P2 | TestCase/Suite/Setup/Teardown/Checkpoint | TestCase/Suite/Setup/Teardown/Checkpoint；需求驱动再实体化，不为凑类型数量 |
| 82 | V. TestCenter | `TCTTestDependencyPolicy` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | RequiresPassed/Always/OnFailure | RequiresPassed/Always/OnFailure |
| 83 | V. TestCenter | `TCTTestRetryReason` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | InfrastructureOnly/FlakyDiagnostic/ExplicitNever | InfrastructureOnly/FlakyDiagnostic/ExplicitNever |
| 84 | V. TestCenter | `TCTTestTargetMode` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P0 | Real/Virtual/Replay/Offline；明确安全语义 | Real/Virtual/Replay/Offline；明确安全语义 |
| 85 | V. TestCenter | `TCTTestSafetyLevel` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P0 | Safe/ExternalIO/HIL/Hazardous | Safe/ExternalIO/HIL/Hazardous |
| 86 | VI. TestCenter | `TCTTestRunSession` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 一次Run状态机与启动/结束证据 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 87 | VI. TestCenter | `TCTTestAttempt` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 每次真实执行唯一AttemptId，禁止覆盖重试历史 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 88 | VI. TestCenter | `TCTTestExecutionCoordinator` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 准备、调度、资源锁、取消与FailFast | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 89 | VI. TestCenter | `TCTTestRunner` | 静态/类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 单Case真实调用、断言完成分类 | 维持当前唯一实现与Owner |
| 90 | VI. TestCenter | `TCTTestLifecycleExecutor` | 静态类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | Setup/Execute/Teardown异常归因 | 维持当前唯一实现与Owner |
| 91 | VI. TestCenter | `TCTTestHostController` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 受控子进程启动、超时、终止与报告 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 92 | VI. TestCenter | `ITCTTestIsolationHost` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 进程隔离宿主抽象；仅供TestCenter复用 | 进程隔离宿主抽象；仅供TestCenter复用 |
| 93 | VI. TestCenter | `ITCTTestWorkerTransport` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Worker事件/结果通道，支持逐条流式传输 | Worker事件/结果通道，支持逐条流式传输 |
| 94 | VI. TestCenter | `TCTWorkerProtocolEnvelope` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | IPC版本、RunId/AttemptId/Seq/消息体哈希 | IPC版本、RunId/AttemptId/Seq/消息体哈希 |
| 95 | VI. TestCenter | `TCTWorkerHandshake` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 协议、构建、位数及能力握手 | 协议、构建、位数及能力握手 |
| 96 | VI. TestCenter | `TCTWorkerHeartbeat` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Worker活性心跳、最后确认序号 | Worker活性心跳、最后确认序号 |
| 97 | VI. TestCenter | `TCTWorkerSession` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 每Worker生命周期、反馈、取消与连接管理 | 每Worker生命周期、反馈、取消与连接管理 |
| 98 | VI. TestCenter | `TCTWireCodec` | 静态类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 原XML协议向版本化/限长消息升级，兼容旧结果 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 99 | VI. TestCenter | `TCTResourceGate` | 类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 已实现TestId键/资源序列化锁 | 维持当前唯一实现与Owner |
| 100 | VI. TestCenter | `TCTTestCompletionValidator` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 零断言/遗漏Attempt/宿主Crash/缺Teardown必须拒绝假通过 | TCTTestRunSummary/Coordinator完成校验（内聚同模块） |
| 101 | VI. TestCenter | `TCTTestRunSummary` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | Selected/Executed/NotStarted/状态汇总、可信证据 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 102 | VI. TestCenter | `TCTTestRunState` | 枚举 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | Prepared/Running/CancellationRequested/Completed/Faulted | 维持当前唯一实现与Owner |
| 103 | VI. TestCenter | `TCTTestStatus` | 枚举 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | Passed/Failed/Skipped/Unsupported/TimedOut/Cancelled/Error/Inconclusive | 维持当前唯一实现与Owner |
| 104 | VI. TestCenter | `TCTTestAttemptState` | 枚举 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Discovered/Queued/Preparing/Running/Cleanup/Finished | TCTTestStatus + Attempt生命周期事件（暂不重复状态机） |
| 105 | VI. TestCenter | `TCTWorkerMessageKind` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Hello/Started/Assertion/Observation/Diagnostic/Heartbeat/Completed/Exit | Hello/Started/Assertion/Observation/Diagnostic/Heartbeat/Completed/Exit |
| 106 | VI. TestCenter | `TCTTestExitCode` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Success=0、TestFailure=1、HostError=2、InvalidArgument=3 | Success=0、TestFailure=1、HostError=2、InvalidArgument=3 |
| 107 | VII. TestCenter | `TCTAssertionResult` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 期待/实际/Delta/Tolerance/来源/证据引用 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 108 | VII. TestCenter | `TCTAssert` | 静态类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 纯比较与标准化断言，context负责收集结果；不持有全局状态 | TCTTestContext.Assert*（单一断言Collector） |
| 109 | VII. TestCenter | `TCTAssertionException` | 类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 真实断言失败区别运行异常 | 维持当前唯一实现与Owner |
| 110 | VII. TestCenter | `ITCTTestComparer` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 可插拔图像/树/集合等比较器 | 可插拔图像/树/集合等比较器 |
| 111 | VII. TestCenter | `TCTNumericComparer` | 静态类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 数值绝对/相对容差比较 | 维持当前唯一实现与Owner |
| 112 | VII. TestCenter | `TCTFormat` | 静态类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 测试值序列化/显示格式 | 维持当前唯一实现与Owner |
| 113 | VII. TestCenter | `TCTAssertionPolicy` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | NaN/Inf/浮点容差/字串规范化等显式参数 | TCTComparisonOptions（共享比较选项；不构造第二断言库） |
| 114 | VII. TestCenter | `TCTNumericTolerance` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 单位、绝对/相对误差、特殊值策略 | 单位、绝对/相对误差、特殊值策略 |
| 115 | VII. TestCenter | `TCTStructureComparer` | 静态类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 集合/Map/树/Reference/对象结构语义比较 | 集合/Map/树/Reference/对象结构语义比较 |
| 116 | VII. TestCenter | `TCTGeometryComparer` | 静态类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 几何Expected/Actual的独立比较与误差表达 | 几何Expected/Actual的独立比较与误差表达 |
| 117 | VII. TestCenter | `TCTBufferComparer` | 静态类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 二进制块、Hash、Size、首个不同偏移 | TCTSnapshotDiffService.CompareBytes（已有行为复用） |
| 118 | VII. TestCenter | `TCTImageComparer` | 类 | 新增 | 暂缓独立类型 | CxVisual.TestCenter | P2 | 像素/ROI/PSNR/MAE等可量化结果；实际图像逻辑来自Owner | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 119 | VII. TestCenter | `TCTVolumeComparer` | 类 | 新增 | 暂缓独立类型 | CxVisual.TestCenter | P2 | Spacing/Origin/Direction、层数与值差 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 120 | VII. TestCenter | `TCTTemporalAssert` | 静态类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Eventually/NoEventWithin/StateReached；不隐藏deadline | Eventually/NoEventWithin/StateReached；不隐藏deadline |
| 121 | VII. TestCenter | `TCTAssertionKind` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Boolean/Numeric/Structure/Exception/Eventual/Performance/Artifact | Boolean/Numeric/Structure/Exception/Eventual/Performance/Artifact |
| 122 | VII. TestCenter | `TCTComparisonKind` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Exact/Tolerance/Semantic/Hash/Visual | Exact/Tolerance/Semantic/Hash/Visual |
| 123 | VIII. TestCenter | `ITCTTestFixtureProvider` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Fixture按ID/版本装载，不污染被测Owner | Fixture按ID/版本装载，不污染被测Owner |
| 124 | VIII. TestCenter | `ITCTTestDataGenerator` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 确定性随机/边界/参数组合数据生成 | 确定性随机/边界/参数组合数据生成 |
| 125 | VIII. TestCenter | `TCTTestFixtureService` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 读取Fixture、验证Hash/Schema、分配Attempt沙箱 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 126 | VIII. TestCenter | `TCTTestDataSet` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Fixture版本、输入集合和Artifact关联 | Fixture版本、输入集合和Artifact关联 |
| 127 | VIII. TestCenter | `TCTFixtureManifest` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 数据来源、Schema、Hash、依赖与许可证 | 数据来源、Schema、Hash、依赖与许可证 |
| 128 | VIII. TestCenter | `TCTFixtureReference` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 对Fixture版本/数据ID的稳定引用 | 对Fixture版本/数据ID的稳定引用 |
| 129 | VIII. TestCenter | `TCTFixtureResolver` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 内置/磁盘/只读缓存多来源解析 | 内置/磁盘/只读缓存多来源解析 |
| 130 | VIII. TestCenter | `TCTDataGenerationSpec` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Seed、分布、长度/边界、生成器版本 | Seed、分布、长度/边界、生成器版本 |
| 131 | VIII. TestCenter | `TCTPropertyCaseGenerator` | 类 | 新增 | 暂缓独立类型 | CxVisual.TestCenter | P2 | PropertyBased数据与最小化反例 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 132 | VIII. TestCenter | `TCTTestSandbox` | 类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 受限临时目录与受控删除 | 维持当前唯一实现与Owner |
| 133 | VIII. TestCenter | `TCTTestArtifactStore` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 按内容Hash归档、读取校验与配额 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 134 | VIII. TestCenter | `TCTArtifactRef` | 类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 路径、Mime、Hash、AttemptId及大小 | 维持当前唯一实现与Owner |
| 135 | VIII. TestCenter | `TCTArtifactRetentionPolicy` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 保留失败证据，按Run/大小/天数清理 | 保留失败证据，按Run/大小/天数清理 |
| 136 | VIII. TestCenter | `TCTBaselineService` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | Expected/Actual基准与显式审批 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 137 | VIII. TestCenter | `TCTBaselineManifest` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Golden版本、兼容环境、Hash和审计 | Golden版本、兼容环境、Hash和审计 |
| 138 | VIII. TestCenter | `TCTBaselineUpdateProposal` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 变更提议/理由、审查差异 | 变更提议/理由、审查差异 |
| 139 | VIII. TestCenter | `TCTBaselineApproval` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 审核人、时间、签名/批准记录 | 审核人、时间、签名/批准记录 |
| 140 | VIII. TestCenter | `TCTBaselineStatus` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Proposed/Reviewed/Approved/Rejected/Published | Proposed/Reviewed/Approved/Rejected/Published |
| 141 | VIII. TestCenter | `TCTFixtureSourceKind` | 枚举 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | BuiltIn/File/Generator/Replay/ExternalReadOnly | TCTFixtureReference.UriScheme（先字段） |
| 142 | VIII. TestCenter | `TCTTestDataOwnership` | 枚举 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | SharedImmutable/AttemptLocal/CopyOnWrite/OwnedLease | TCTFixtureReference.Scope（先字段） |
| 143 | IX. TestCenter | `ITCTTestObserver` | 接口 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 测试过程观察器Attach/Detach | 维持当前唯一实现与Owner |
| 144 | IX. TestCenter | `ITCTTestProbe` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 读生产诊断/快照的桥接，不增加生产对TCT依赖 | 读生产诊断/快照的桥接，不增加生产对TCT依赖 |
| 145 | IX. TestCenter | `ITCTTestResultSink` | 接口 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 接收真实Result/事件 | 维持当前唯一实现与Owner |
| 146 | IX. TestCenter | `ITCTObservationSerializer` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 版本化观察Payload编码/解码 | 版本化观察Payload编码/解码 |
| 147 | IX. TestCenter | `TCTTestObservation` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 升级为带Schema/Time/SourceObject/Run/Attempt的统一观测信封 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 148 | IX. TestCenter | `TCTObservationSchema` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Kind+Version+PayloadFormat及校验器 | Kind+Version+PayloadFormat及校验器 |
| 149 | IX. TestCenter | `TCTObservationSchemaRegistry` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 视图发现和版本兼容的单一注册表 | 视图发现和版本兼容的单一注册表 |
| 150 | IX. TestCenter | `TCTTestObservationRecorder` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 有界追加、事件序号、Artifact溢出与写入确认 | TCTTestObserverHub + TCTObservationEnvelope（单一录制链） |
| 151 | IX. TestCenter | `TCTTestObserverHub` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 多Probe事件汇聚、顺序与背压 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 152 | IX. TestCenter | `TCTTestEvent` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | Sequence/Timestamp/Run/Attempt/Correlation/Source | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 153 | IX. TestCenter | `TCTTestEventJournal` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 流式过程事件归档，Crash后保留有效前缀 | 流式过程事件归档，Crash后保留有效前缀 |
| 154 | IX. TestCenter | `TCTTestEventReplay` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 从真实录制回放，不生成模拟通过结果 | 从真实录制回放，不生成模拟通过结果 |
| 155 | IX. TestCenter | `TCTTestDiagnostic` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 归因码/异常/来源/Severity/Attempt | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 156 | IX. TestCenter | `TCTTestEvidence` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | Repo/Commit/OS/GPU/程序集/环境/Fixture/GroundTruth来源 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 157 | IX. TestCenter | `TCTTestEvidenceLink` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 断言⇔对象⇔观察⇔Artifact⇔运行实例的多向证据关系 | TCTAssertionResult / TCTArtifactRef 的关联字段（先字段后实体） |
| 158 | IX. TestCenter | `TCTTestResultCollector` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 校验事件Seq和完成消息，汇总真实结果 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 159 | IX. TestCenter | `TCTTestProbeRegistry` | 类 | 新增 | 暂缓独立类型 | CxVisual.TestCenter | P2 | 按Owner对象/观察Kind选择真实Adapter | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 160 | IX. TestCenter | `TCTTestProbeContext` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Probe绑定真实对象、一次Attempt和访问策略 | TCTTestContext（仅需要独立订阅生命周期时再拆） |
| 161 | IX. TestCenter | `TCTObservationOrigin` | 枚举 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Actual/Virtual/Replay/SyntheticReference | TCTObservationEnvelope.Origin字段（先有限值，需跨版本再枚举） |
| 162 | IX. TestCenter | `TCTObservationPayloadFormat` | 枚举 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Json/Binary/ArtifactReference/Text | TCTObservationEnvelope格式标记（先版本化字符串） |
| 163 | IX. TestCenter | `TCTTestEventKind` | 枚举 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | Started/Assertion/Observation/Diagnostic/Completed/Heartbeat | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 164 | IX. TestCenter | `TCTTestDiagnosticSeverity` | 枚举 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | Info/Warning/Error/Critical | 维持当前唯一实现与Owner |
| 165 | X. TestCenter | `TCTCapabilityCoverageService` | 静态类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 现有仅按官方ID白名单计算；升级为要求维度×证据 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 166 | X. TestCenter | `TCTCapabilityCoverageEntry` | 类 | 现有扩展 | 合并/内聚 | CxVisual.TestCenter | 后置 | 现有Verified/Failed/Unmapped；补Contract/维度缺项 | TCTCapabilityCoverageMatrix.Entry（现有类保留兼容，不另造第二模型） |
| 167 | X. TestCenter | `TCTCapabilityCatalog` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 读取经审核的能力规格版本，不生成假ID | 读取经审核的能力规格版本，不生成假ID |
| 168 | X. TestCenter | `TCTCapabilityRequirement` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 某能力对象最低必测的维度/场景规则 | 某能力对象最低必测的维度/场景规则 |
| 169 | X. TestCenter | `TCTTestEvidenceMapping` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 正式Owner/Capability/Object/TestId/AssertionId映射 | TCTCapabilityCoverageMatrix.EvidenceLink（可审映射） |
| 170 | X. TestCenter | `TCTCapabilityCoverageMatrix` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 能力对象×合同/正常/边界/异常/并发/生命/版本/性能 | 能力对象×合同/正常/边界/异常/并发/生命/版本/性能 |
| 171 | X. TestCenter | `TCTCapabilityCoverageReport` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 覆盖/缺口/失效/证据不足和计算依据 | TCTCapabilityCoverageMatrix + 报表投影（不独立持久化状态） |
| 172 | X. TestCenter | `TCTCapabilityCoverageRule` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 不能只凭有Test或PASS就报完整覆盖 | TCTCapabilityRequirement（统一维度与门槛） |
| 173 | X. TestCenter | `TCTCapabilityMappingValidator` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 失效ID/孤儿用例/重复归属/缺维度诊断 | 失效ID/孤儿用例/重复归属/缺维度诊断 |
| 174 | X. TestCenter | `TCTCoverageEvidenceKind` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | TestCase/Assertion/Observation/Artifact/CI/HIL | TestCase/Assertion/Observation/Artifact/CI/HIL |
| 175 | X. TestCenter | `TCTCoverageDimension` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Contract/Normal/Boundary/Exception/Concurrency/Lifecycle/Version/Performance | Contract/Normal/Boundary/Exception/Concurrency/Lifecycle/Version/Performance |
| 176 | X. TestCenter | `TCTCoverageState` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Unmapped/Unimplemented/Untested/Partial/Verified/Failed/Blocked | Unmapped/Unimplemented/Untested/Partial/Verified/Failed/Blocked |
| 177 | XI. TestCenter | `ITCTTestReporter` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Text/JSON/JUnit/HTML插件式输出 | Text/JSON/JUnit/HTML插件式输出 |
| 178 | XI. TestCenter | `TCTReportWriter` | 静态类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 现有Text/JSON/JUnit写入 | 维持当前唯一实现与Owner |
| 179 | XI. TestCenter | `TCTReportManifest` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 报告格式版本、输入/证据文件索引与Hash | TCTTestRunSummary + TCTArtifactRef（先用同一个manifest契约） |
| 180 | XI. TestCenter | `TCTTestResultRepository` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 持久化Run、查History、可查询索引 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 181 | XI. TestCenter | `TCTStoredTestRun` | 类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 历史运行只读快照 | 维持当前唯一实现与Owner |
| 182 | XI. TestCenter | `TCTTestRunDiff` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 现有Pass↔Fail差异，增加环境可比性/耗时分布 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 183 | XI. TestCenter | `TCTSnapshotDiff` | 类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 最小差异结果 | 维持当前唯一实现与Owner |
| 184 | XI. TestCenter | `TCTSnapshotDiffService` | 静态类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 字节/文本对比；结构/视觉另加比较器 | 维持当前唯一实现与Owner |
| 185 | XI. TestCenter | `TCTRunComparisonPolicy` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Commit/Fixture/Seed/环境差异可比性规则 | Commit/Fixture/Seed/环境差异可比性规则 |
| 186 | XI. TestCenter | `TCTTestHistoryIndex` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 查询Run/Attempt/Commit/Capability的轻量索引 | TCTTestResultRepository索引（持久化内部实现） |
| 187 | XI. TestCenter | `TCTTestFailureFingerprint` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 按断言码、异常栈/Owner聚合重复故障 | 按断言码、异常栈/Owner聚合重复故障 |
| 188 | XI. TestCenter | `TCTTestFailureClusterer` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 重复失败聚合与去噪；不能影响Pass/Fail | TCTTestFailureFingerprint + 查询聚合（以后有数据再考虑分离） |
| 189 | XI. TestCenter | `TCTReportFormat` | 枚举 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Text/Json/JUnit/Html/Sarif | TCTReportWriter格式参数（已有3格式，枚举以后按需要） |
| 190 | XI. TestCenter | `TCTTestComparability` | 枚举 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Comparable/EnvironmentMismatch/BaselineMismatch/Unknown | TCTTestRunDiff.ComparabilityReason（先字段） |
| 191 | XII. TestCenter | `ITCTTestMetricSampler` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 从真实FCRSystemMonitor或SDK采集指标 | 从真实FCRSystemMonitor或SDK采集指标 |
| 192 | XII. TestCenter | `ITCTTestFaultInjector` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Virtual/Replay可控故障注入，不默认触发工业输出 | Virtual/Replay可控故障注入，不默认触发工业输出 |
| 193 | XII. TestCenter | `TCTBenchmarkPlan` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 热身/稳定性/轮次/阈值/资源占用声明 | 热身/稳定性/轮次/阈值/资源占用声明 |
| 194 | XII. TestCenter | `TCTBenchmarkRunner` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 样本采集与隔离，结果按统计口径输出 | TCTTestExecutionCoordinator + Benchmark策略（不设第二个Runner） |
| 195 | XII. TestCenter | `TCTBenchmarkSample` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 一次Latency/CPU/Alloc/Queue样本 | 一次Latency/CPU/Alloc/Queue样本 |
| 196 | XII. TestCenter | `TCTBenchmarkSummary` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | P50/P95/P99/方差/置信/环境约束 | P50/P95/P99/方差/置信/环境约束 |
| 197 | XII. TestCenter | `TCTBenchmarkThreshold` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 超限规则和样本最低数量 | 超限规则和样本最低数量 |
| 198 | XII. TestCenter | `TCTTestStressProfile` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 持续时间、并发梯度、内存/CPU/句柄观察 | 持续时间、并发梯度、内存/CPU/句柄观察 |
| 199 | XII. TestCenter | `TCTTestHealthSnapshot` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 进程/运行资源状态，来自真实监测 | 进程/运行资源状态，来自真实监测 |
| 200 | XII. TestCenter | `TCTFaultInjectionPlan` | 类 | 新增 | 暂缓独立类型 | CxVisual.TestCenter | P2 | 丢包/延迟/进程崩溃/文件损坏等可控方案 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 201 | XII. TestCenter | `TCTTestReliabilityGate` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 可靠性验收门：运行时长、故障恢复与残留资源 | TCTTestExecutionPolicy验收规则（仅内部判定） |
| 202 | XII. TestCenter | `TCTMetricKind` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Latency/Throughput/CPU/Memory/GPU/Queue/Handle | Latency/Throughput/CPU/Memory/GPU/Queue/Handle |
| 203 | XII. TestCenter | `TCTFaultKind` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Timeout/Crash/PacketDrop/Corruption/Disconnection/OutOfMemory | Timeout/Crash/PacketDrop/Corruption/Disconnection/OutOfMemory |
| 204 | XII. TestCenter | `TCTBenchmarkVerdict` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Pass/Regression/Inconclusive/Uncomparable | Pass/Regression/Inconclusive/Uncomparable |
| 205 | XIII. TestCenter | `TCTGeometryObservation` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Input/Expected/Actual/单位/坐标/差异视图DTO | Input/Expected/Actual/单位/坐标/差异视图DTO |
| 206 | XIII. TestCenter | `TCTObjectTreeObservation` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 真实Ownership/Reference/Revision结构化快照 | 真实Ownership/Reference/Revision结构化快照 |
| 207 | XIII. TestCenter | `TCTGraphObservation` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 真实Node/Port/Connection/编译问题的只读投影 | 真实Node/Port/Connection/编译问题的只读投影 |
| 208 | XIII. TestCenter | `TCTChannelObservation` | 类 | 新增 | 暂缓独立类型 | CxVisual.TestCenter | P2 | 真实Frame/Session/TX-RX/Codec解析的只读投影 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 209 | XIII. TestCenter | `TCTRuntimeObservation` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 真实Instance/Scope/Lease/Waiting/Trace的只读投影 | 真实Instance/Scope/Lease/Waiting/Trace的只读投影 |
| 210 | XIII. TestCenter | `TCTTimelineObservation` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 时序事件/跨度、Correlation及关联对象 | 时序事件/跨度、Correlation及关联对象 |
| 211 | XIII. TestCenter | `TCTResourceObservation` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 真实资源占用/待队列/释放快照 | 真实资源占用/待队列/释放快照 |
| 212 | XIII. TestCenter | `TCTTestFrameRecord` | 类 | 新增 | 暂缓独立类型 | CxVisual.TestCenter | P2 | 帧二进制Artifact引用、协议字段与时戳 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 213 | XIII. TestCenter | `TCTTestGraphVisualEdge` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 视觉显示边：Owns/References/Connects/Executes明确区分 | TCTGraphObservation.Edge（只读DTO） |
| 214 | XIII. TestCenter | `TCTTestGraphVisualNode` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 被测节点/实例对应的只读显示元素 | TCTGraphObservation.Node（只读DTO） |
| 215 | XIII. TestCenter | `TCTTestObservationDiff` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 可视化差异结果（结构/数值/时间） | 可视化差异结果（结构/数值/时间） |
| 216 | XIII. TestCenter | `TCTObservationViewKind` | 枚举 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Geometry/Object/Graph/Channel/Runtime/Timeline/Diff/Benchmark | TCTObservationSchema.ViewerId（由注册表决定） |
| 217 | XIII. TestCenter | `TCTGraphVisualRelationKind` | 枚举 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Owns/References/Connects/Instantiates | 生产Owner真实关系契约+只读显示字段（不二次定义领域语义） |
| 218 | XIV. TestCenter | `ITCTTestViewer` | 接口 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 只读观察绑定，支持Schema能力声明 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 219 | XIV. TestCenter | `ITCTTestViewerCommandProvider` | 接口 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 活动Viewer按选择贡献合法命令 | 活动Viewer按选择贡献合法命令 |
| 220 | XIV. TestCenter | `TCTMainWindow` | WPF Window | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 统一Shell，非六个窗口 | 维持当前唯一实现与Owner |
| 221 | XIV. TestCenter | `TCTTestCenterViewModel` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 选择/Run/文档/历史/命令状态唯一会话层 | 选择/Run/文档/历史/命令状态唯一会话层 |
| 222 | XIV. TestCenter | `TCTTestExplorerControl` | WPF控件 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | Owner→能力→对象→Case树、搜索/Tag/Plan筛选 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 223 | XIV. TestCenter | `TCTTestDocumentHostControl` | WPF控件 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 多视图/文档Tab、关闭和布局恢复 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 224 | XIV. TestCenter | `TCTTestInspectorControl` | WPF控件 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 属性/断言/诊断/Watch及来源索引 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 225 | XIV. TestCenter | `TCTTestResultsControl` | WPF控件 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 结果、失败跳转、RunId/AttemptId、批次统计 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 226 | XIV. TestCenter | `TCTTestRibbonController` | 类 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 动态命令上下文，不能硬编码5个固定按钮 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 227 | XIV. TestCenter | `TCTTestViewerRegistry` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Kind/SchemaVersion→Viewer，Unknown安全降级 | Kind/SchemaVersion→Viewer，Unknown安全降级 |
| 228 | XIV. TestCenter | `TCTViewerDescriptor` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Viewer支持的数据模式、命令贡献、权限 | TCTTestViewerRegistry的只读注册记录（保留internal） |
| 229 | XIV. TestCenter | `TCTTestCommandRouter` | 类 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 可用性/执行/撤销规则；统一Shell命令来源 | 可用性/执行/撤销规则；统一Shell命令来源 |
| 230 | XIV. TestCenter | `TCTTestUISelection` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 当前Owner/Case/Result/ObservedObject/时间点统一选择 | TCTTestCenterViewModel.Selection（单一UI状态） |
| 231 | XIV. TestCenter | `TCTTestViewportControl` | WPF控件 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 通用缩放平移、选点、坐标层与截图产物 | 通用缩放平移、选点、坐标层与截图产物 |
| 232 | XIV. TestCenter | `TCTTestStatusBarControl` | WPF控件 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Run、环境、进度、能力映射和故障数 | TCTMainWindow状态栏（无需先建单独控件） |
| 233 | XIV. TestCenter | `TCTTestDashboardControl` | WPF控件 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P2 | 测试主界面、覆盖/缺口/可靠性概览 | 测试主界面、覆盖/缺口/可靠性概览 |
| 234 | XIV. TestCenter | `TCTTestPlanEditorControl` | WPF控件 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Suite/Plan编辑、参数、依赖、预检/执行 | Suite/Plan编辑、参数、依赖、预检/执行 |
| 235 | XIV. TestCenter | `TCTTestHistoryControl` | WPF控件 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 历史Run、失败复现、双运行对比 | 历史Run、失败复现、双运行对比 |
| 236 | XIV. TestCenter | `TCTTestFixtureControl` | WPF控件 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Fixture、Seed、Golden预览/显式审批 | Fixture、Seed、Golden预览/显式审批 |
| 237 | XIV. TestCenter | `TCTTestCoverageControl` | WPF控件 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 正式能力维度与缺证据热区 | 正式能力维度与缺证据热区 |
| 238 | XIV. TestCenter | `TCTTestTimelineControl` | WPF控件 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | 时间轴、进度和点击事件定位 | 时间轴、进度和点击事件定位 |
| 239 | XIV. TestCenter | `TCTTestDiffControl` | WPF控件 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Input/Expected/Actual/Diff并排 | Input/Expected/Actual/Diff并排 |
| 240 | XIV. TestCenter | `TCTTestBenchmarkControl` | WPF控件 | 新增 | 暂缓独立类型 | CxVisual.TestCenter | P2 | P50/95/99、样本、环境与回归对照 | 先使用Attribute/Context/标准C#与既有执行链；不新建第二入口 |
| 241 | XIV. TestCenter | `TCTTestViewBase` | WPF抽象类 | 现有 | 保留现有 | CxVisual.TestCenter | 已有 | 通用观察/断言绘制基类 | 维持当前唯一实现与Owner |
| 242 | XIV. TestCenter | `TCTGeometryTestView` | WPF控件 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 输入/预期/实际/误差叠加与缩放 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 243 | XIV. TestCenter | `TCTObjectTestView` | WPF控件 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 真实Ownership/Reference+Revision/对象高亮 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 244 | XIV. TestCenter | `TCTGraphTestView` | WPF控件 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 真实Node/Port/连接、编译诊断与运行Overlay | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 245 | XIV. TestCenter | `TCTChannelTestView` | WPF控件 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | 虚拟真实帧TX/RX、十六进制、Decoder/Session | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 246 | XIV. TestCenter | `TCTRuntimeTestView` | WPF控件 | 现有扩展 | 保留扩展 | CxVisual.TestCenter | 已有 | Instance树/Resource树/Timeline/Trace/等待理由 | 保持源码兼容；完善失败语义、证据及最小方法，不新建同义类 |
| 247 | XIV. TestCenter | `TCTTestWorkspaceLayout` | 类 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | 可保存布局/窗格大小/打开文档/主题 | TCTTestCenterViewModel的Layout设置（仅需持久化再分类型） |
| 248 | XIV. TestCenter | `TCTTestCommandKind` | 枚举 | 新增 | 必要新增（按批次） | CxVisual.TestCenter | P1 | Run/Cancel/Replay/Compare/Attach/GoToSource等 | Run/Cancel/Replay/Compare/Attach/GoToSource等 |
| 249 | XIV. TestCenter | `TCTTestDocumentKind` | 枚举 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Explore/Geometry/Object/Graph/Channel/Runtime/Benchmark | ITCTTestViewer.Kind（注册标识取稳定字符串） |
| 250 | XIV. TestCenter | `TCTViewerActivationState` | 枚举 | 新增 | 合并/内聚 | CxVisual.TestCenter | 后置 | Available/Unsupported/NoEvidence/VersionMismatch | TCTTestViewerRegistry.Availability（暂不另建枚举） |

## 附录B：内容完整性与复核记录

| 复核项 | 数量 / 结果 | 备注 |
|---|---|---|
| 250项原类型逐条裁决 | **250/250** | ID连续1～250，保留原职责及裁决去向 |
| 详细核心合同 | **43** | 实际源表43条，非旧摘要39条 |
| 原250项以外新增合同 | **10** | 不代表10个独立源文件 |
| 能力八维逐行索引 | **264** | 原矩阵原样转入第09章，ROW为工作行号非正式能力ID |
| 六场景控件/交互映射 | **42** | 原表逐行转入第08章 |
| 开发者体验验收 | **D01～D12** | 原Excel/旧文档目标验收，不代表代码已通过 |
| WPF UI交互验收 | **U01～U10** | 原Excel目标验收，不代表截图即通过 |
| 历史裁决 | **ADR-T01～T15** | 同文第10.5节 |
| 工程事实 | **Git SHA 5284c8a9** | 仅对该Commit及所引用的Windows CI有效 |
| 六张2560×1440视觉图 | **6 + 1预览** | 图为模拟参考，不是实际测试证据 |

### 跨源合流规则

- 原V2.0详细执行技术约束进入第07章，原V2.1唯一Owner和依赖原则进入第04章。
- V3.0的完整候选词表及V3.2的裁决没有删项；第06章＋附录A给出全量去向。
- V3.1的简用标准进入第03章、D01～D12进入第09章。
- 原六场景V1.1的图、控件、交互与禁止Mock规则进入第08章。
- 原V4.0类合同、UI、Codex、来源文档是**编写此单文档的输入**，今后不再作为平行有效规范。

**V4.1检验边界：设计完整≠源码完整；源码可编译≠自动测试通过；Smoke≠WPF完整UIA；UIA≠人工界面或工业现场验收；测试通过数≠正式能力八维覆盖率；Worker退出≠设备安全停止。**

