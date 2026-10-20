# CxVisual.TestCenter V2.1

## Foundation / Mechanism / Professional 子方面代码归属、公共接口与依赖规则（实施规格）

**文档状态：建议冻结的架构规则 + 现有代码核查 + 迁移/验收指南**  
**日期：2026-10-15**  
**适用：cx-visual-x3、.NET Framework 4.8、C# 7.3、x64、WPF TestCenter**  
**前置设计：《CxVisual.TestCenter 详细架构与类契约规格 V2.0》**  
**本版关注：谁定义接口、谁实现功能、谁引用谁、怎样组装与测试；不声称任何未实际运行的代码已通过验收。**

> **核心结论：子方面实现放在自己的 Owner 项目；跨领域公共能力放到真正归属的 Foundation 或 Mechanism 项目；各项目按需引用，不要求统一引用 Foundation.Infrastructure。高层应用负责组装，测试中心负责验证。**

---

## 1. 本轮正式建议冻结的七项决定

1. **专业归专业，公共归公共。** Geometry、Numerics、Image、Volume、Runtime、Connectivity、Automation 等各自持有其领域算法、模型、接口和测试契约；不要因为多个地方会使用，就一律搬到 Infrastructure。
2. **接口由语义 Owner 管。** `IFLDLogSink` 的语义是基础日志，属于 Foundation.Infrastructure；`IMRKExecutionEngine` 是机制执行器，属于 Mechanism.Runtime；`ITCTTestCase` 属于 TestCenter。接口不因使用者数量而改变所属层。
3. **谁需要才引用谁。** 如 Geometry 不使用日志/并发时无需引用 Infrastructure；Runtime 使用 FCRWorker 时才直接引用 Infrastructure。禁止把 Infrastructure 设置成所有 csproj 的默认 ProjectReference。
4. **实现就近、注入组合。** 纯算法可静态类；有状态的 Logger/Worker/Provider/Runner 为实例对象。接口与核心抽象通常留在所属语义项目；依赖外部 SDK/设备的具体实现可以归具体专业项目或 Adapter，由应用层装配。
5. **依赖方向只向基础/稳定契约下沉。** Foundation 不反向依赖 Mechanism/Professional/Tests；Mechanism 不反向依赖 Professional/Tests；Professional 不依赖 Tests；TestCenter 引用被测程序集。
6. **一份类型真源。** TestCenter 不再创造第二套 Logger/Clock/Geometry/Runtime/Channel/State 对象；只创建测试专用的 Runner、Fixture、Assertion、Result、ObserverBridge、Viewer。
7. **不为追求“公共”提前抽象。** 只有明确的跨项目使用场景、稳定接口和生命周期，才向下提炼；先检查已经存在的公共契约，避免重复 API、无意义 Wrapper 和循环引用。

### 1.1 你提出的两句话应如何理解

| 原始判断 | V2.1 裁决 | 原因 |
|---|---|---|
| “各个子方面的代码应该放自身那” | **原则上正确** | 维护稳定 Owner；算法、参数、模型、实现、测试与其语义靠近 |
| “所以各项目都引用 Foundation.Infrastructure” | **不正确：改为按需直接引用** | 强制引用将造成假依赖、传染式重编译、层次污染和隐藏循环 |
| “然后实现接口或使用公共能力” | **正确，但须分三类** | ①纯静态计算直接调用；②有状态服务经接口注入；③外部设备/SDK经专业 Adapter 接入 |

---

## 2. 分层定位：Foundation 是多个子方面，不等于 Infrastructure

```text
Platform 产品（公共能力的使用与集成）
  ├─ Foundation（基础方面：Core / Numerics / Geometry / Imaging / Volume / Data / Infrastructure / Transport）
  │      └─ 彼此仅在真实需要时单向依赖；不存在“一切经过 Infrastructure”规则
  ├─ Mechanism（对象/数据/逻辑/连接/可视/编排/运行时/生命周期等机制项目）
  │      └─ 按需引用 Foundation 与低层机制契约
  ├─ Professional（Automation / HMI / Vision / CT3D / Inspection / Simulation）
  │      └─ 贡献专业模型、算法、Runtime/Provider/Adapter/Editor
  ├─ Applications（CxVisual.Studio / CxVisual.Runtime.Host）
  │      └─ Composition Root：实例化、注入、选择真实或虚拟实现
  └─ Tests（含唯一新增 CxVisual.TestCenter）
         └─ 按需引用被测项目；TestCenter 的测试契约不进入生产项目
```

**注意：**“基础能力复用”与“程序集引用”是两件事。跨进程/动态插件可以通过契约、IPC、注册或宿主注入获得能力，不一定意味着每个专业项目编译时都直接引用基础设施 DLL。

### 2.1 Foundation 八个项目的建议 Owner（依据当前项目目录；子方面编号需与最新主规格表逐项核对）

| 项目 | 原有责任 / 建议保留的对象 | 不宜放入的内容 |
|---|---|---|
| `Foundation.Core` | 极基础稳定 ID、参数 Guard、最小不带 IO 的契约和值对象（现有 `CxStableId`、`CxNumericGuard`） | Logger 服务、领域 Workflow、TestCase |
| `Foundation.Numerics` | 数值计算、容差数学、插值、矩阵/线性代数等**纯数值** | 测试通过/失败、HMI 图元、设备通讯 |
| `Foundation.Geometry` | 点、矩形、变换、几何谓词、坐标几何等；现有 `CxPoint2D`、`CxRect2D`、`CxAffine2D` | TestViewer、日志 Writer、业务 ROI 检测结果 |
| `Foundation.Imaging` | 像素、图像格式、基础图像处理原语；按需求增补 | 工业 Detector SDK 驱动、最终 Inspection Judge |
| `Foundation.Volume` | Voxel、Volume 低层数据/空间运算、基础 3D 数学 | CT 重建业务 Job/采图控制 |
| `Foundation.Data` | Schema/配置/序列化/数据基类；现有 `FCMConfig*`、`FCMJson*` 代码 | Test Fixture/Golden 管理、MES业务 Payload |
| `Foundation.Infrastructure` | **生产级基础设施**：现有 FLD 日志、FCR 有界异步 Worker/并发与系统监测；按批准需求增补通用时间/进程能力 | Geometry/Graph/Runtime 的业务执行逻辑、TCT 测试接口 |
| `Foundation.Transport` | 底层 Channel/Endpoint/Buffer/传输原语的归属候选；当前 csproj 主要为空壳 | Modbus/S7等工业协议语义、测试协议帧 Viewer |

**现有事实：** `Foundation.Infrastructure` 已包含 `FLDLogService`、`IFLDLogSink`、`FCRWorker`、`FCRWorkHandle<T>`、`FCRWorkerSnapshot`、`FCRSystemMonitor` 等；不应再从零造一套 `TCTWorker` 或 `TCTLogger`。`Foundation.Numerics` 和 `Foundation.Transport` 当前项目文件显示主要为程序集骨架，不能把建议能力写成已经落地。

### 2.2 Mechanism 九个项目及表示适配层（按职责归属；具体类型仍以最新对象表为准）

| 项目 | 应拥有的机制语义 | 可复用的基础能力 |
|---|---|---|
| `Mechanism.Core` | Project/Page/Content/Symbol/Group/Element、Ownership、Reference、Revision、工程事务 | `Foundation.Core` / `Foundation.Data` |
| `Mechanism.Data` | Variable、Binding、Data Scope、数据映射与质量/订阅机制 | `Foundation.Data`，需要时 Infra/Transport |
| `Mechanism.Logic` | Expression、Rule、Condition、Command/Event 的通用语义 | Core/Numerics 等按需 |
| `Mechanism.Connectivity` | 公共 Channel/Protocol/Session 的机制封装与设备无关协作 | Transport/Infrastructure 按需 |
| `Mechanism.Visual` | 机制侧可视对象、连接视觉语义、编辑服务 | Geometry、Core |
| `Mechanism.Orchestration` | Node/Port/Connection、Graph Definition、校验/编排契约 | Core/Data/Logic 按需 |
| `Mechanism.Runtime` | ExecutionPlan、执行上下文、流程运行、资源与状态语义 | `FCRWorker`、Data、Core；**当前确实直接引用 Infrastructure** |
| `Mechanism.Lifecycle` | 版本、迁移、发布、工程生命周期与部署契约 | Core/Data 等按需 |
| `Mechanism.Presentation.Wpf` | WPF Presentation Adapter、Presenter、Controls | Visual/Geometry/必要的WPF程序集 |

以上是**目标归属**而非宣称全部实现。源码中 `Mechanism.Core` 已有对象树与文档事务，`Mechanism.Runtime` 已有 `IMRKExecutionEngine`、`MRKExecutionEngine`，其余若尚是空壳，不得用目标类型冒充已存在的API。

### 2.3 Professional、Applications、Tests 的明确边界

| 层 / 项目 | 该层放什么 | 该层绝不放什么 |
|---|---|---|
| `Professional.Automation` | PLC/IO、Motion、Robot、X-Ray采图等专业 `Provider`/设备/命令与Editor | 通用 FCR Worker 复制版、全平台 TestRunner |
| `Professional.HMI` | 工业画面/Faceplate/Operator、专业属性贡献 | 新的第二套 Drawing 引擎 |
| `Professional.Vision` | 图像检测、ROI、AI、Metrology 的专业算法/管线 | 基础矩阵计算重实现、MES 最终 Judge |
| `Professional.CT3D` | CT Geometry/Calibration/Reconstruction、Volume专业分析 | 取代 Foundation.Volume 底层容器 |
| `Professional.Inspection` | Product/Plan/Defect/Judge/Recheck/ManualReview/MES | Image 算法内核或通用 Runtime Kernel |
| `Professional.Simulation` | Virtual Device/Plant/Scenario/HIL 的专业实现 | 第二份真实设备 Provider 接口 |
| `Applications.Studio / Runtime.Host` | **装配根**；UI启动、服务注册、配置绑定、运行模式选择 | 大量混合到窗口代码的专业算法 |
| `Tests/CxVisual.TestCenter` | `ITCT...`、`TCT...`、Fixture、Assertion、Runner、View、Report、测试 Host Policy | 被测生产接口的唯一来源、全局基础 Logger、生产 Runtime 状态真源 |

---

## 3. 接口怎么放：不是“公共接口统一放 Infrastructure”

### 3.1 四类接口、四种放法

| 接口类别 | Owner规则 | 实现规则 | 示例 |
|---|---|---|---|
| 纯计算契约/基础值对象 | 放对应 Foundation 子方面 | 同项目实现，无状态优先静态类 | Geometry/数值容差、数据格式 |
| 基础设施服务契约 | 放 `Foundation.Infrastructure`（仅限日志/并发/监控等确属此子方面） | 同项目默认实现，应用可注入替身 | 现有 `IFLDLogSink`、`FLDLogService`、`FCRWorker` |
| 机制语义接口 | 放对应 Mechanism 项目 | 同机制项目默认实现；专业域贡献 Strategy/Adapter | `IMRKExecutionEngine` 在 Mechanism.Runtime |
| 测试契约 | 放 TestCenter 单项目内部 `Contracts` | Runner/Viewer/TestCase在 TestCenter 内实现 | `ITCTTestCase`、`ITCTTestViewer`、`ITCTTestObserver` |

**什么时候接口可以放在更低层？** 只有上层应用和多个独立子方面都需要一个**真正稳定、非行业化、无上层依赖**的契约时，才提炼到恰当的低层 Owner；不要为了让所有项目“实现一个统一接口”而人为把它放到 Infrastructure。

### 3.2 编译依赖与运行时注入不要混为一谈

```text
场景 A：Geometry 测试
TestCenter ──直接引用──▶ Foundation.Geometry ──▶ Foundation.Core
                                   不需要 Foundation.Infrastructure

场景 B：Runtime 执行
Application.Runtime.Host ──▶ Mechanism.Runtime ──▶ Foundation.Infrastructure (FCRWorker)
                                   │                 ▲
                                   └──▶ Mechanism.Core / Foundation.Data

场景 C：专业算法工作
Professional.Vision ──▶ 专业算法对象/公共Data/必要的Worker契约
Application负责选择具体算法适配、日志实例、Worker配置

场景 D：测试视图
TestCenter.UI ──▶ TestCenter.ITCTTestViewer ──▶ TCTTestObservation
                                       （只读测试证据）
生产方仅提供自己的诊断/观察契约，完全不知道 TCT 名称
```

**C# 编译原则：** 
- 引用 = 源码在编译时使用到某程序集的公开类型；只为“将来可能用”不增加引用。
- 注入 = 宿主构造某具体实现并传给使用方；注入并不自动消除接口所属项目的编译引用。
- 适配 = 把真实设备/第三方 API 转为稳定专业契约；生产 SDK 不应因公共 Adapter 穿透到 Foundation。
- 最终组合 = `Studio` / `Runtime.Host` / `TestCenter` 入口处组装，不创建静态全局万能 `ServiceLocator`。

### 3.3 依赖准入与禁止清单

**允许：** 同层按真实依赖单向引用；Mechanism→Foundation；Professional→Mechanism/Foundation；Applications/Tests→必要项目。`Mechanism.Runtime` 直接引用 `Foundation.Infrastructure` 使用已存在的并发能力是有依据的。

**禁止：** Foundation→Mechanism/Professional/Tests；Mechanism→Professional/Tests；公共基础设施调用 TestCase、Graph Node、MES、Axis/CT Reconstruction；为了调用日志而让所有类继承某 `InfrastructureBase`；在 TestCenter 重造生产 Runtime；跨项目形成引用环。

**例外处理：** 如果两个机制项目相互调用造成循环，先拆职责或使用既有稳定 Contract/回调注入/事件；只有核实有独立复用需求才研究更低层抽取，不因循环而临时新增第 N 个公共项目。

---

## 4. 类/接口/公共能力归属复核表（TestCenter V2.0 → V2.1）

| 项目/接口 | 唯一建议Owner | 使用/实现方式 | 当前状态或裁决 |
|---|---|---|---|
| `IFLDLogSink` / `FLDLogService` / `FLDLogRecord` | Foundation.Infrastructure | 生产日志接口和服务；测试注入/消费，不复制 | **现有源码确认** |
| `FCRWorker` / `FCRWorkerOptions` / `FCRWorkHandle<T>` | Foundation.Infrastructure | 通用有界Worker，Mechanism.Runtime按需复用 | **现有源码确认** |
| `FCRSystemMonitor` / `FCRWorkerSnapshot` | Foundation.Infrastructure | 系统/工作状态快照，TestCenter可观察 | **现有源码确认** |
| 通用 Clock / Process Monitor 接口 | Foundation.Infrastructure（**待核归属**） | 若确定为生产公共基础服务，按需要补充，不创建TCT同名副本 | **方案候选，不是现有API确认** |
| 数学容差/Vector/Matrix计算 | Foundation.Numerics / Geometry | 底层纯计算归自己，测试 `TCTNumericComparer` 只是验证语义 | Numerics尚有实现缺口 |
| Schema/序列化/通用配置 | Foundation.Data | 复用已有 FCM Config / JSON；测试Fixture独立管理 | FCM已有源码确认 |
| 传输/Channel基础接口 | Foundation.Transport / Mechanism.Connectivity | Transport封装传输；Connectivity封装Session/协议逻辑 | Owner方向建议；目前仍需实际代码审核 |
| `CxProjectDocument` / `CxNode` | Mechanism.Core | 真实对象定义与变更事务，TestCenter只运行验证 | **现有源码确认** |
| `IMRKExecutionEngine` / `MRKExecutionEngine` | Mechanism.Runtime | 真实执行契约/实现，TestCenter未来接 Observer | **现有源码确认** |
| ResourceLease/ExecutionInstance/Timeline | 对应 Mechanism.Runtime 等 Owner | 生产运行事实；不能搬到 Foundation.Infrastructure | 部分为目标规划，逐项核API |
| `ITCTTestCase` / `ITCTTestDiscoverer` | Tests/CxVisual.TestCenter | 仅测试发现/用例契约 | TestCase现有，Discoverer待做 |
| `ITCTTestViewer` / `ITCTTestObserver` | Tests/CxVisual.TestCenter | 测试展示/采集适配，读生产诊断 | 目标设计，尚未接入完整Viewer/Observer |
| `ITCTTestFrameworkAdapter` | Tests/CxVisual.TestCenter | 复用NUnit/MSTest等成熟测试框架的适配器 | 目标设计 |
| `TCTTestCatalog` / `TCTTestRunner` | Tests/CxVisual.TestCenter | 测试注册、执行与结果 | 初版源码已存在 |
| `TCTTestContext` / `TCTTestRunRequest` | Tests/CxVisual.TestCenter | 测试Attempt环境与请求参数 | Context现有；Request待做 |
| `TCTAssertionResult` / `TCTTestObservation` | Tests/CxVisual.TestCenter | 仅测试证据模型 | 初版源码已存在 |
| `TCTTestFixtureService` / `TCTBaselineService` | Tests/CxVisual.TestCenter | 测试数据、Golden版本、安全更新 | 目标设计 |
| `TCTTestHostController` / `ITCTTestIsolationHost` | Tests/CxVisual.TestCenter | 测试独立进程/退出码/超时/杀进程 | 目标设计；通用进程基础功能才可下沉Infra |
| `TCTMainWindow` / Explorer/Inspector/Results | Tests/CxVisual.TestCenter.UI | WPF测试应用表现 | MainWindow现有；完整拆控件待做 |
| `TCTGeometry/Object/Graph/Channel/RuntimeTestView` | Tests/CxVisual.TestCenter.Visualization | 专业测试结果可视化，接口统一 | 目标设计；不得代替专业生产Viewer |

**命名保持：** `TCT` / `ITCT` 只在 TestCenter 中使用；`FLD`、`FCR`、`FCM`、`MRK` 等现有生产前缀一律保持。迁回生产Owner的类型重新按该子方面约定命名，不将 `TCT*` 暴露给生产库。

---

## 5. 代码怎么实现：三种正常模式 + 两种反模式

### 模式1：纯计算子方面直接调用（无需Infrastructure）

```csharp
// 实际已有 API；示例调用方式，不代表本次已编译运行。
using CxVisual.Foundation.Geometry;

CxRect2D rect = new CxRect2D(0, 0, 100, 80);
bool inside = rect.Contains(new CxPoint2D(25, 30));
CxPoint2D moved = CxAffine2D.Identity.Transform(new CxPoint2D(3, 5));
```

`Geometry` 不需要为了输出一个计算结果继承 Logger 或注入 Infrastructure。几何错误要在 Geometry 返回异常/诊断；只有宿主需要记录时，才在宿主或 TestCenter 注入日志。

### 模式2：需要有状态基础Worker时显式依赖它

```csharp
// 现有代码结构的归属说明：
// Mechanism.Runtime 已直接引用 Foundation.Infrastructure。
// Mechanism.Runtime: IMRKExecutionEngine / MRKExecutionEngine
// Foundation.Infrastructure.Concurrency: FCRWorker / FCRWorkerOptions
// 关系：MRKExecutionEngine内部复用FCRWorker；不是把MRK执行语义写进FCRWorker。
```

一个可维护的 `MRKRuntime` 仍须自行定义 ExecutionPlan/State/Context/故障传播，`FCRWorker` 只处理底层排队、Key串行和并发等通用事情。

### 模式3：有状态服务由应用显式装配（契约示意）

```csharp
// 以下只展示接口所属关系，是设计伪代码，非仓库现有构造签名：
// App Composition Root:
//   FLDLogService logger = new FLDLogService(logPath);
//   MRKExecutionEngine engine = new MRKExecutionEngine(workerOptions);
//   TCTTestRunner runner = new TCTTestRunner(...); // 仅在TestCenter入口
// 应用负责创建/释放服务；被测业务仅引用自己的稳定契约。
```

### 反模式 A：全项目默认引入 Infrastructure

```xml
<!-- 禁止：在全局Build.props里自动给所有项目加Infrastructure引用 -->
<ProjectReference Include="Foundation/Infrastructure/..." />
```

构建属性（目标框架、输出目录、平台）可统一；**项目依赖必须明确且最小化**。静态查看当前 `Build/CxVisual.Build.props` 只有全局构建设置，未要求所有项目引用 Infrastructure。

### 反模式 B：为共用而把专用接口搬进基础设施

```text
错误：Infrastructure.ITCTTestCase → Tests实现
错误：Infrastructure.IExecutionInstance → Runtime实现（若接口携带Graph/Runtime语义）
错误：Infrastructure.IAxisController → Automation实现
正确：TestCenter.ITCTTestCase / Mechanism.Runtime的执行契约 / Automation的轴契约
```

---

## 6. 测试中心 V2.1 应怎样调用公共能力

### 6.1 一条完整测试路径

```text
TCTMainWindow / --run
  → TCTTestCatalog（发现/选择）
  → TCTTestRunRequest（参数/超时/Seed/隔离）
  → TCTTestRunner（调度，不实现业务）
  → ITCTTestCase（真实调用对应Owner公开API）
  → 原生产API（如 CxRect2D、CxProjectDocument、IMRKExecutionEngine）
  → TCTAssertionResult / TCTTestObservation / TCTArtifactRef
  → TCTTestRunSummary / Report / WPF Viewer（只读展示）
```

测试代码可以**直接引用目标生产项目**；有日志、时钟、调度或其他基础设施需要时再单独引用相应 Owner。禁止“强制把所有被测方法包装成 Infrastructure 服务”才能测试。

### 6.2 TestCenter本身的代码Owner

| 内部模块（仍只是一份项目） | 应包含 | 不包含 |
|---|---|---|
| `Contracts` | ITCTTestCase/Viewer/Observer/Discoverer/FrameworkAdapter、测试专用Adapter契约 | 生产 IClock/Logger/Runtime接口 |
| `Models` | Descriptor/RunRequest/Attempt/Result/Observation/Artifact | 专业图像/体数据物理容器本体 |
| `Catalog` | 自动发现、能力映射、筛选/分类、重复ID拒绝 | 用例执行 |
| `Execution` | Test Runner/Coordinator、隔离策略、Run状态、Timeout/Cancel | 生产Flow调度、设备互锁语义 |
| `Fixtures` | 数据来源/生成/Golden/临时文件/回收 | 公用 FCM JSON Schema 实现 |
| `Diagnostics` | ResultCollector、TraceBridge、Observer对接、证据分流 | 模拟生产事件后直接标绿 |
| `Visualization` | Geometry/Object/Graph/Channel/Runtime TestView + 只读差异视图 | 第二套生产Renderer |
| `UI` | 一个 WPF 主Shell + Explorer/DocumentHost/Inspector/Results/Ribbon | Runner业务执行实现 |
| `Cases` | 对应Foundation/Mechanism的真实测试 | 公共 Logger/Worker/Clock 的替代实现 |

### 6.3 与V2.0类表的明确对应

保留V2.0的完整测试专用类族：`ITCTTestCase`、`ITCTTestDiscoverer`、`ITCTTestViewer`、`ITCTTestObserver`、`ITCTTestFrameworkAdapter`、`TCTTestCaseDescriptor`、`TCTTestRunRequest`、`TCTTestContext`、`TCTTestCaseResult`、`TCTAssertionResult`、`TCTTestObservation`、`TCTTestRunSummary`、`TCTTestCatalog`、`TCTTestRunner`、`TCTTestFixtureService`、`TCTTestHostController`、`TCTTestResultCollector`、`TCTMainWindow`及各专业 TestView。

**本次仅更改它们对生产公共能力的依赖归属，不删除测试特定生命周期和测试语义。** V2.0附带的断言种类、Run/Attempt状态、子进程隔离协议、Fixture/Baseline审批、真实Runtime观测、六界面规范继续有效；若V2.0文字隐含“所有项目统一引用Infrastructure”，以V2.1本规则修正。

---

## 7. 接口与实现分布：按需装配的判断流程

对每个拟新增的类或接口，按以下顺序裁决：

1. **说清它做什么。** 能不带“测试、设备、工作流、AI”等领域词独立定义？不能则通常留在专业Owner。
2. **寻找已有的Owner与实现。** 当前已有FLD日志/FCR Worker/FCM数据/Mechanism.Core对象树/Mechanism.Runtime执行器；优先复用。
3. **判断是否需要接口。** 只有多实现、独立模拟、可替换 Provider、生命周期注入、跨程序集契约等具体动机时建立接口；纯数学静态类不必接口化。
4. **找到接口语义最低正确层。** 依赖底层不能强迫它理解上层。业务接口不能以“可能复用”为理由下降到Infrastructure。
5. **定义最小依赖。** 把真正使用的ProjectReference写到该项目；不能随意追加全部八个Foundation引用。
6. **在哪里组合？** 具体实现由Application/Runtime.Host/TestCenter入口构造/注入；真实设备SDK Adapter在专业项目。
7. **做反证测试。** 移除不必要引用后该项目仍能构建；Foundation单独可构建；Tests失败不影响生产构建。

判定输出应写入Owner清单：`Type / Interface / OwnerProject / ImplementorProject / ConsumerProjects / StateLifetime / DirectReferences / ReuseEvidence / TestIds / CurrentStatus`。只审议发生变动的类型，避免每次全表翻改。

---

## 8. 迁移与实施顺序（避免一口气重构全部项目）

| 阶段 | 要做什么 | 检查方式 |
|---|---|---|
| M0：依赖事实盘点 | 从所有 `.csproj` 得出当前直接引用图，区分空壳/已实现；逐项核 TestCenter V2.0 所有类Owner | 无新增功能、输出真实依赖清单和归属差异 |
| M1：复用现有基础设施 | 用FLD/FCR/FCM已有API替代重复基础代码；不改公开契约除非有明确冲突 | FoundationSmoke、ArchitectureSmoke、实际x64编译 |
| M2：接口就近归属 | 跨项目抽象只移到正确Owner；保持旧签名兼容或提供Adapter，避免大范围破坏 | 编译依赖无环、二进制/工程引用验证 |
| M3：TestCenter测试闭环 | TestCenter引用实际被测项目；接入Runner、Result、Fixture、Viewer，保持一个项目 | --run真实报告、WPF交互、失败与未知状态正确 |
| M4：隔离与诊断 | 危险测试独立进程；使用生产Runtime/Channel诊断而非UI伪状态 | timeout/Crash/Cancel/资源清理/Timeline验收 |
| M5：自动化与覆盖 | 能力表正式ID回链、性能/回归/CI、审批Baseline | Capability×Test×Result证据可追踪 |

**优先事项：** 先解决现有 TestCenter 的源码闭环与依赖清理，再谈所有 Mechanism/Professional 重构。为减少提交和便于审阅，一个可编译、可验证的主题尽量一个提交；只在本次任务范围内调整受影响的项目。

---

## 9. 验收清单与实际证据要求

- [ ] Foundation/Mechanism/Professional/Applications/Tests 的**实际直接引用图**可以从 `.csproj` 导出，零循环。
- [ ] `Foundation.Core / Geometry / Numerics` 不被强制引入 Infrastructure；无使用者不添加该引用。
- [ ] Foundation 不依赖 Mechanism/Professional/Tests；Mechanism 不依赖 Professional/Tests。
- [ ] 每个需要跨项目共享的接口只保留一个Owner；已存在的FLD/FCR/FCM/MRK契约不重复创造。
- [ ] `TCT/ITCT` 只存在TestCenter，不出现在生产公开API。
- [ ] TestCenter至少可执行真实Geometry、Mechanism.Core测试，保留失败证据、取消状态和异常堆栈。
- [ ] TestCenter WPF/--run同一Runner/Result，`--run`不弹主窗口。
- [ ] `Task.Run`并非硬超时保证；未实现子进程隔离时必须标明限制。
- [ ] 关键Foundation/Mechanism子方面的契约、实现、SourceOwner和TestId能通过规格主表回查。
- [ ] `Build/BUILD.cmd` Windows x64真实编译及Smoke测试日志已保留，方可写“验证通过”。
- [ ] 没有Windows实测时只写“静态审查/设计完整”，禁止标为“实现完成”。

### 9.1 现状核查与未核实边界

本文对照了 GitHub `oudream/cx-visual-x3` 的公开项目源文件及 TestCenter `feature/testcenter-tct-v1` 分支中的类；另以本地 V2.0 文档为前置设计。已确认 **Infra具备 FLD/FCR 代码、Mechanism.Runtime 当前按需引用 Infra、Geometry 当前不引用 Infra、TestCenter 初版只引用 Geometry + Mechanism.Core**。这些是**源码级证据**。尚未取得 Windows 编译、WPF界面运行、子进程隔离或设备联调证据；V2.1为依赖与Owner设计，不冒充验收结果。

## 10. Codex执行指令（可直接复制）

> 在 `cx-visual-x3` 当前 master 上工作。先读 `AGENTS.md`、`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md`、现有最新项目 Owner / API 规格以及本 V2.1。只围绕 `CxVisual.TestCenter` 与实际需要的公共子方面改动，遵守项目的中文提交与少提交规范。首先导出 `.csproj` 当前依赖图并核对已有 FLD/FCR/FCM/MRK 类型，列出“已存在／新增必要／重复待删／待证实”清单。保持 TestCenter 单WPF EXE；不要把所有项目统一引用 Infrastructure，不要将 ITCT 接口移进生产 Foundation；接口在语义Owner、实现就近、应用入口注入。先保证现有7个真实测试用例、真实报告与WPF使用，然后再逐项补Fixture、Viewer、进程隔离、Runtime Observer。所有提交应附真实Windows构建/测试日志（无法执行时如实说明）；不可用演示数据冒充测试PASS。除任务范围所需的工程引用与适配外，不对别的子方面大改。

---

## 附录 A. 与V2.0的变更对照

| V2.0已有设计 | V2.1修订点 | 兼容策略 |
|---|---|---|
| 一项目 TestCenter、TCT统一类 | **继续保留** | 不新增第7 Workbench，不拆大堆Test DLL |
| Runner/Catalog/Fixture/Viewer/Observer | **继续留Tests** | 原测试契约不迁移到生产库 |
| 日志/并发/监控 | **复用实际 FLD/FCR 代码** | 不建重复 Wrapper；根据真实需求引用 |
| 时间/进程管理 | **只有跨生产复用时下沉至正确 Foundation Owner** | 当前候选，不宣称已存在 |
| Runtime/Execution/Resource/Trace | **明确留Mechanism** | TestCenter通过只读观察Bridge获取事实 |
| Geometry/数值/序列化/Transport | **各归Foundation相应子方面** | 不向Infrastructure堆叠 |
| “每个项目都用公共” | **公共能力按需依赖** | 验证不需要者零依赖、无引用环 |
| 初始TestCenter真测案例 | **保持7个已登记用例的源代码事实** | 未经Windows执行不宣称全部PASS |

## 附录 B. 参考来源及事实级别

- **现有源码**：`Foundation/Infrastructure/CxVisual.Foundation.Infrastructure.csproj`、`FLDLogService.cs`、`IFLDLogSink.cs`、`Concurrency/FCRWorkContracts.cs`、`Concurrency/FCRWorker.cs`。
- **现有源码**：`Mechanism/Runtime/CxVisual.Mechanism.Runtime.csproj`、`Execution/MRKExecutionEngine.cs`、`Mechanism/Core/CxVisual.Mechanism.Core.csproj`、`Foundation/Geometry/CxVisual.Foundation.Geometry.csproj`。
- **现有源码**：`Tests/CxVisual.TestCenter/Core/TCTTesting.cs`、`Cases/TCTRealCases.cs`、`CxVisual.TestCenter.csproj`（位于`feature/testcenter-tct-v1`分支；不能据此推定已经合并master）。
- **现有文档**：`DOCS/00_CxVisual_协同开发入口与七仓复用基线.md`、`AGENTS.md`、本地《CxVisual.TestCenter 详细架构与类契约规格 V2.0》；主 Owner 表/大规格 Excel 仍应做对应 Sheet 的最终复核。
- **证据规则**：源码确认 ≠ Windows编译通过 ≠ TestRunner实际PASS ≠ 工业现场验收；本文件仅作设计和静态检查结论。
