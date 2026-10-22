# CxVisual.TestCenter 目录架构与开发规则（2026-10-22）

**当前单一项目**：`Tests/TestCenter/CxVisual.TestCenter.csproj`，WPF + .NET Framework 4.8。  
**架构划分**：一个公共基础 `Common`、四大工作区 `Manual / Automated / Scenarios / ReportCenter`、自验证 `Verification`。  
**此段描述已执行的物理目录归位**；未落地的专业页面不伪造源码或功能。完整功能/验收基线仍参照[统一主界面与Frame注册 V1.1](CxVisual_TestCenter_统一主界面与工作区Frame注册——完整设计与验收_V1.1.md)。

## 1. 目录总览

```text
Tests/TestCenter/
├─ App.xaml / App.xaml.cs             唯一应用入口及CLI/Worker入口
├─ MainWindow.xaml / MainWindow.xaml.cs  唯一WPF主窗口
├─ CxVisual.TestCenter.csproj         唯一测试中心工程
├─ Common/                           四个工作区共享的基础能力
│  ├─ Framework/                     WorkspaceDescriptor、Registry、Contracts
│  ├─ Navigation/                    统一Explorer/测试项目树
│  ├─ Shell/                         DocumentHost、统一根总界面
│  ├─ Operations/                    右侧上下文操作中心
│  └─ Controls/                      公用Inspector等UI
├─ Automated/                        自动测试体系（真实NUnit/TCT）
│  ├─ Core/                          ITCTTestCase、Assert、Catalog、Run、Runner
│  ├─ Cases/                         自动测试用例（MSBuild递归通配）
│  ├─ Discovery/                     NUnit/TCT发现注册
│  ├─ Execution/                     Child Worker、调度、结果收集、事件
│  ├─ Fixtures/                      Fixture、Artifact、Golden（自动Attempt专属）
│  ├─ Models/                        Run/Attempt/Observation/Evidence模型
│  ├─ NUnitIntegration/              NUnit正式引擎适配
│  ├─ Diagnostics/                   现有TCT TestObserverHub
│  ├─ Reports/                       自动Run结果存储、历史、导出真源
│  └─ UI/
│     ├─ Overview/                   自动测试总览
│     ├─ Plans/                      Suite/Plan界面
│     ├─ Results/                    Results与Attempt Timeline
│     └─ Viewers/                    经过Owner证据核验的只读显示
│        ├─ Shared/                  Viewer基础及GraphRuntime公用行为
│        ├─ Geometry/ Object/ Graph/ Channel/ Runtime/
├─ Manual/                           专有测试（人工操作）工作区
│  └─ Channel/                       已实现Virtual收发操作页（非真实TCP）
├─ Scenarios/                        场景案例工作区，目前仅规划/目录说明
├─ ReportCenter/                     结果报告工作区UI（不重复存储）
│  ├─ History/                       真实历史运行查看
│  └─ Diff/                          已归档Run/Attempt对比
└─ Verification/                     TestCenter自身WPF/UIAutomation
```

**四根界面映射**：专有测试→`Manual`；自动测试→`Automated`；场景案例→`Scenarios`；结果报告→`ReportCenter`。四根使用同一`Common/Framework`注册机制、`Common/Navigation`树和`Common/Shell`工作区；**树节点不等于一份新的业务工程，也不要求一叶一Frame类**。

**未来按需建立，不冒充已实现**：`Common/Diagnostics`（跨工作区诊断合同）；`Manual/Overview、Sessions、Protocol、Graphics、CT3D`；`Scenarios/Overview、Catalog、Details`；`ReportCenter/Overview、Automated、Manual、Scenarios`。相应真实Owner、权限和证据具备时再新增类与目录。

## ArchitectureSmoke 独立程序接入（保持原程序兼容）

在 TestCenter 左侧的 **自动测试 → ArchitectureSmoke（独立架构回归）** 打开调用工作区。
中央选择原程序实际支持的命令预设，包括默认回归、Foundation、Concurrency、Engineering Organization、
Config Foundation/WPF 与已实现的性能回归；右侧操作中心有 **运行当前范围 / 停止执行 / 清空本页输出**。
页面实时显示原 `CxVisual.Tests.ArchitectureSmoke.exe` 的 stdout、stderr、退出码、耗时和异常。
退出码为0只表示 **ArchitectureSmoke 进程整体成功**，没有逐条结果协议时不会虚构NUnit/TCT Attempt、覆盖率或报告。

- **源程序不变**：`Tests/ArchitectureSmoke/Program.cs` 仍支持单独命令行运行，TestCenter仅以受控子进程调用。
- **进程路径**：与 `CxVisual.TestCenter.exe` 同输出目录的 `CxVisual.Tests.ArchitectureSmoke.exe`。未编译或缺失时显示Unsupported并禁用运行；不读取用户输入作为可执行命令。
- **隔离和安全**：`UseShellExecute=false`，stdout/stderr异步读取，固定模式与有界超时；停止时结束受控进程及其子进程。不自动运行真实设备，也不合并两套执行器。
- **证据边界**：当前页面输出在会话内有界显示，不自动把ArchitectureSmoke日志写成TestCenter Run/Attempt或Golden。

## 2. Common 与四大工作区的边界

| 目录 | 职责 | 不负责 |
|---|---|---|
| `Common` | Frame描述/注册、导航、Shell、文档Tab、操作命令、通用Inspector；AvalonDock共用 | 自动Catalog、Worker、测试断言、ManualSession、工程定义 |
| `Automated` | 唯一TCT/NUnit Catalog/Runner/Run/Attempt、Fixture、Artifact、Golden、自动报告及只读专业Viewer | 人工收发、实时设备控制和第二套工程 |
| `Manual` | 各专业可操作的人工测试页、独立ManualSession与操作观察；优先调用生产Owner | 以人工操作成功伪造NUnit Passed或Golden |
| `Scenarios` | 真实场景目录、工程/运行绑定、受控验证入口（后续完善） | 无Owner时制造“已通过”或伪设备行为 |
| `ReportCenter` | 统一报告类型/索引/历史/差异的UI展示 | 再建一份Run/Attempt结果数据库 |
| `Verification` | WPF/UIA、交互及视觉验收脚本 | 测试平台业务的独立Runner |

现有`TCTTestArtifactStore`依赖Run/Attempt，仍归`Automated/Fixtures`。要支持人工证据，应先设计独立ManualSession身份及跨体系证据合同，不得直接把它“搬到Common”并共享可变状态。

## 3. 文件拆分与命名空间政策

- 旧`UI/TCTControls.cs`→ `Common/Navigation` Explorer、`Common/Operations` RibbonController、`Common/Controls` Inspector、`Automated/UI/Results` Results。
- 旧`UI/TCTWorkspaceFrames.cs`→ `Common/Framework` Descriptor/Registry/Contracts、`Common/Shell` RootFrame、`Manual/Channel` ChannelFrame。
- 旧`UI/TCTTestViews.cs`→ `Common/Shell` DocumentHost、`Automated/UI/Viewers`下各专业Viewer与共享基类。
- History/Diff视图位于`ReportCenter`，自动报告生成与历史归档真源仍位于`Automated/Reports`；WPF UIA在`Verification`。
- **本轮保持原C#命名空间和公开类型名不变**（如`CxVisual.TestCenter.UI`、`CxVisual.TestCenter.Core`），避免纯目录迁移同时修改WPF XAML `clr-namespace`、反射发现、已有接口及自动测试身份。未来若规范要求命名空间随目录调整，应分独立兼容任务实施。

## 4. 新增源码及构建规则

1. 所有新NUnit/TCT测试源文件放`Tests/TestCenter/Automated/Cases/`（可分子目录）；传统`.csproj`用`Automated\\Cases\\**\\*.cs`编译通配，**不需要逐个新增项目项**。
2. UI及其他非Cases的C#文件使用显式`<Compile Include="..."/>`；新增/移动必须同步`.csproj`，避免Visual Studio出现CS0246。
3. Windows CI会在`Automated/Cases/`动态生成NUnit/TCT测试源码后全量重建；不准在旧`Tests/TestCenter/Cases/`手工复制同名测试。
4. App/MainWindow XAML保留根目录，工作区`ContentId`、AutomationId、Frame/Case ID及Layout序列化合同应保持兼容。
5. 状态以**对应SHA**的Windows构建、NUnit/TCT、WPF UIA及人工界面分别验收；Windows自动通过不等于DPI/视觉签认或实体设备HIL。旧设计/开发记录中引用`UI/TCTTestViews.cs`等历史路径时，以本节和.csproj现行路径为准。

---

## 5. 开发者快速使用（依据唯一主规格 V4.1）

这是给开发者使用的**一页说明**，不是另一本技术规格；所有设计和 F/M/P Owner 归属仍以[《CxVisual.TestCenter 统一测试体系完整规格 V4.1》](../../DOCS/Architecture/CxVisual_测试体系——完整规格%20V4.1.md)为准。开发者无需手动注册 Catalog、Runner 或 Worker，也不需要修改生产项目引用。

## 最常见：新增一个测试

在本工程 `Tests/TestCenter/Automated/Cases/` 或其子目录新建 `TCTMyChecks.cs`：

```csharp
using NUnit.Framework;
[TestFixture]
public sealed class TCTMyChecks
{
    [Test]
    public void Addition() { Assert.That(2 + 3, Is.EqualTo(5)); }

    [TestCase(2, 3, 5)]
    [TestCase(3, 4, 7)]
    public void Adds(int a, int b, int expected)
    {
        Assert.That(a + b, Is.EqualTo(expected));
    }
}
```

`Automated\\Cases\\**\\*.cs` 在现有 `CxVisual.TestCenter.csproj` 自动包含新文件。无需新建项目，不修改生产 Foundation/Mechanism Owner。

在 Windows x64 下构建当前 `CxVisual.Unified.sln`（.NET Framework 4.8）后：

```powershell
./Output/Debug/x64/CxVisual.TestCenter.exe --list --report ./Output/Debug/x64/catalog.json
./Output/Debug/x64/CxVisual.TestCenter.exe --run --id "NUNIT.TCTMyChecks.Addition" --report ./Output/Debug/x64/one.junit.xml --format junit --isolate
./Output/Debug/x64/CxVisual.TestCenter.exe --run --profile Regression --repeat 3 --parallel 3 --report ./Output/Debug/x64/all.junit.xml --format junit --isolate
```

`catalog.json` 显示 TestId、CaseId、DefinitionId、RowKey、程序集指纹。NUnit 的标准 NUnit Assert/async Task/SetUp/TearDown 完全由 NUnit 引擎负责；别另写一套普通断言框架。

## 外部 NUnit DLL（仅加载可信的测试程序集）

如果工程已经构建独立 DLL，可显式告诉 TestCenter 要加载哪些 DLL，子 Worker 继承同一配置：

```powershell
$env:TCT_NUNIT_ASSEMBLIES = 'D:\tests\TrustedChecks.dll'
./Output/Debug/x64/CxVisual.TestCenter.exe --list --report ./Output/Debug/x64/catalog.json
```

多个路径用 `;` 分隔。外部 ID 是 `NUNIT.<程序集文件名>::<NUnit FullName>`；它与定义ID及参数RowKey分开，按 SHA256 确认父/子 Worker 没有执行不同版本。只加载自己信任的程序集，因为测试会执行其中的任意 C# 代码；危险设备运行另需设备 Owner 许可。

## 真实观察、Evidence与Diff（进阶可选）

已有 TCT 用例在拿到真实生产 Owner 对象结果后，可以调用 `context.ObserveStructured(kind,title,details,sourceObjectId,definitionAddress,runtimeInstanceId,payloadBytes,mime)`。
它自动绑定RunId、AttemptId、UTC及真实Owner，并将小的XML观察包和大负载分别保存为内容寻址 Artifact。
调用 `TCTObservationReplay.Read(result)` 回放会验证 SHA256、Attempt与Schema；`TCTObservationDiffService.Compare(previous,current)` 比较跨运行真实证据。没有 Owner 实际数据的场景不填写虚构的实例ID，UI也不得判为通过。

## 失败与安全

`--run --id ...` 会精确执行选中的Case，失败、Inconclusive、Skipped、Error、取消、超时和空选择都不返回成功退出码。Worker隔离不等于工业设备物理联锁，HIL必须另行安全审批。Golden只能按旧SHA版本经显式批准，测试本身不自动覆盖Golden。

## 历史证据独立回放（M1）

每次Run可指定 `--history-dir`。保存到历史目录的是原始Attempt结果、身份Manifest和经过SHA256核查的内容寻址Artifact副本，原临时Worker目录清理后仍可验证。

```powershell
./Output/Debug/x64/CxVisual.TestCenter.exe --run --id "TCT.M1.OBS.004" --history-dir "./Output/Debug/x64/EvidenceHistory" --report "./Output/Debug/x64/run.json" --format json --isolate
# 从 run.json 读取真实 runId，不要使用示例ID代替
./Output/Debug/x64/CxVisual.TestCenter.exe --replay-history "./Output/Debug/x64/EvidenceHistory" "<真实RunId>" "./Output/Debug/x64/replayed.json"
```

`--replay-history` 在新进程中验证全部记录的Attempt身份、Schema、Artifact大小和SHA256，并输出原始状态与Owner观察。**回放命令返回0表示归档完整，不表示原测试全部通过**，请查看输出中的 `originalPassed`。回放不会重新执行生产设备，也不会更改Golden。


## M2：工程组织快照与六场景界面

在现有 `CxVisual.TestCenter` WPF 应用中直接运行或选择 `M-OBJ-001`（真实工程Ownership）与 `M-OBJ-005`（快照安全性），进入 `ObjectTree` 页。WPF工具栏“场景”可选择 **概览 / Geometry / ObjectTree / Graph / Channel / Runtime**；当前Attempt没有相应专业观察时只显示 `NoEvidence`，不合成虚假执行图。

`TCTRealCases` 使用 `MEMEngineering.Create()` 创建真实组织、`MEMEngineeringSession.GetSnapshot()` 读取已提交版本；`TCTEngineeringSnapshotCodec.Serialize` 仅投影 `MEMProjectSnapshot` 到只读XML证据负载，通过 `context.ObserveStructured("ObjectTree",...,payload,Mime)` 存入每次Attempt的内容寻址Artifact。UI在显示树前调用 `TCTObservationReplay.Read` 核对Envelope/Run/Attempt/Artifact，再读取负载及拒绝非法Parent/Child/Reference/环与DTD；失效或缺失时显示错误，绝不解析 `Observation.Details` 冒充权威组织树。

对象树按 `MEMNodeSnapshot.ParentId/ChildIds` 展开；`MRIReferenceSnapshot` 作为区别于Owned Child的独立引用叶。点击节点在右侧 Inspector“工程对象”页显示身份、定义地址与视觉参数；点击引用则显示带 `InstancePath` 的使用位置地址。右侧同时展示真实端口、连接及Aspect计数，Graph/Runtime完整生产编译和执行映射尚属M3/H07。

运行 `CxVisual.TestCenter.exe --ui-smoke` 可自动验证真实WPF控件事件和跨进程Worker证据回显（**不是**完整Windows UIA及人工外观验收）。`M-OBJ-005` 反证检验提交后旧快照不变、父子伪造、重复拥有、DTD禁用。当前Snapshot格式只用于测试证据，**不替代** MEM工程文件持久化格式、生产编译器和运行实例真源。

界面视觉依据：`DOCS/Architecture/TestCenter/` 中六张2560×1440设计图；权威需求以 `DOCS/Architecture/CxVisual_测试体系——完整规格 V4.1.md` 第08章及全局台账为准。

## 第一阶段：统一WPF工作台 + Geometry/Object（2026-10-21增量）

单一 `CxVisual.TestCenter` 工程已实现以下软件能力，**是否完成验收以同一代码Commit的Windows CI为准**：

- 共用AvalonDock 5.0.1 Shell：Explorer、DocumentHost、Inspector、Results、Timeline、History、Plan、Diff均可停靠；支持主题与工作区保存/恢复。
- Explorer单项/多项、上下文Ribbon、真实Worker执行、取消、失败重跑、源码与对象定位、报告导出。
- Geometry使用真实 `FGERect2D` 和 `CxSceneProjection`，保存版本化Input/Expected/Actual/Diff及定义/实例地址。支持视口缩放平移、四图层、重叠实例列表、坐标系/单位标注和可核查的点位容差；未知单位不能假定毫米或像素。
- Object使用SHA256校验的 `MEMProjectSnapshot`，显示Owner树、已提交双Revision差异、Ownership实线/Reference虚线关系图、Symbol多实例选择及Inspector关联。
- History/Plan可保存重开；两个History Run的Attempt、Artifact、Schema均验证后才计算Status和Observation Diff；不同Commit/OS不冒充同环境回归。
- `--ui-smoke` 检查真实Owner案例与WPF事件；`--ui-automation` 借助Windows UIAutomation实际筛选、选择、运行Geometry/Object、切换页签和上下文Ribbon，并保存工作台/Geometry/Object/深色主题四张真实截图作为Actions工件。

开发者只需要在统一Catalog内增加真实用例，不需要新建第二套Runner、工程树或测试项目。

**范围与证据边界**：软件功能实现不等于人工界面验收。六张设计图中的01～03须在真实窗口按布局、键盘、DPI、停靠和错误路径进行人工核对；04～06 Graph/Channel/Runtime专业Viewer属于第二阶段。正式Fixture/Golden/Suite高级调度/八维覆盖/长稳及专业HIL属于第三阶段。无真实设备与授权时不能标工业现场通过。

## 阶段一正式界面复核步骤（尚需人工签认）

在Windows x64构建通过后运行 `Output/Debug/x64/CxVisual.TestCenter.exe`。与
`DOCS/Architecture/TestCenter/01～03` 三张设计图逐项核对：

1. **01 工作台**：Explorer筛选/勾选两条真实Case→运行、取消与重跑；Results选择Attempt后Timeline、Inspector必须指向同一Run/Attempt；布局停靠浮动、保存重开及浅深主题；History验证两次真实Run并显示Diff，错误SHA不能打开。
2. **02 Geometry**：选择`TCT.M2.GEOMETRY.001`，检查Input/Expected/Actual/Diff独立图层、真实坐标系/单位/容差、缩放平移和图元→Inspector；不同单位无变换时不得显示可比较成功。
3. **03 MEM Object/Symbol**：运行`M-OBJ-007`与`M-OBJ-006`，验证两个共享Symbol的不同InstancePath、Ownership实线/Reference虚线、引用边点击定位、双Revision Diff、图元重叠时仍可从证据列表选不同实例。
4. **反证与证据**：无Owner数据必须NoEvidence；超时/取消/失败不得变绿；归档损坏、跨Attempt引用、非法Schema须显式拒绝；导出与History来源一致。
5. **视觉与交互**：DPI 100%/150%/200%，键盘、鼠标、滚轮、停靠浮窗、窗口大小变化和异常提示；比较Windows Actions工件`TestCenter-WPF-UIA`的Workspace/Geometry/Object/Dark截图与01～03设计图。

自动验收入口：
```powershell
./Output/Debug/x64/CxVisual.TestCenter.exe --ui-smoke
./Output/Debug/x64/CxVisual.TestCenter.exe --ui-automation
```
`--ui-smoke`是WPF真实控件及Owner证据回归；`--ui-automation`是真实桌面UIA操作、连续执行Geometry/Object并保存WPF截图。
两者不能替代上述人工窗口对照与实际工业现场验收，**未签认不得标为人工已验收**。


## 第二阶段：真实Graph/Runtime及Virtual Channel开发者使用

使用现有TestCenter Shell，在Explorer输入以下TestId后运行并打开实际专业Viewer：

- TCT.M3.GRAPHRUNTIME.001：真实MEM/MOL→MOC SourceMap→MRK Flow/Trace，Graph树及Runtime Timeline的CompiledStep/Block定位。
- TCT.M3.GRAPHRUNTIME.003：真实MOL V2 MPC端口/控制连线，保留工程Link身份；与编译Step依赖分开呈现。
- TCT.M3.CHANNEL.001：生产Mechanism.Connectivity的Virtual Channel真实分片、RX、CRC错误、断连与重开。Channel页经SHA256校验显示原始HEX/解码Payload与会话/Correlation，点击Frame定位到Inspector。**不接实际设备，不等于HIL**。
- GRAPHRUNTIME.002与CHANNEL.002包含非法Schema、伪造映射、半包/粘包、CRC、DTD异常反证。

Window x64 .NET Framework 4.8可使用统一命令 --ui-smoke 或 --ui-automation 做实际WPF验证。后者现覆盖Geometry、Object、Graph、Runtime、Virtual Channel，Actions工件期望工作台/Geometry/Object/Dark/Graph/Runtime/Channel共七张截图；以同Commit的TestCenter Windows Actions最终结论为准。

以上只是阶段二的实际垂直切片：尚缺MRKSession/Resource/Journal全面实时观察、工业设备Protocol/Real、Studio端到端六案例与正式第三阶段Fixture/Golden/八维覆盖/性能/HIL。人工设计04～06视觉验收仍需实际签认。


## 第二阶段 RuntimeSession／Journal 实际验证

选择TCT.M3.RUNTIME.001：真实MEM/MOL→MOC Plan→MRKRuntimeSession，测试容量拒绝与结束释放；读取原生MRKFileExecutionJournal的开始、节点、结束和恢复判定；Runtime页只读显示Session/Flow/Step/Journal/Group及Inspector双向定位。
选择TCT.M3.RUNTIME.002：跨Flow Journal、错误Sequence、假Closed、非法状态与DTD反证，均应在现有TCT框架判为拒绝。
Windows执行 --ui-automation 验证MRK Journal→Inspector并上传TestCenter-08-RuntimeJournal.png。实际CI以 https://github.com/oudream/cx-visual-x3/actions/runs/39622306992 最终结论为准；UIA不替代人工06设计图或工业HIL验收。
## Visual Studio 本地同步与编译排错（2026-10-22）

如果更新了 `TCTTestViews.cs`，却在 Visual Studio 中出现 3 条
`CS0246: 找不到 TCTTestOverviewControl`，应先核对工作区**文件与项目清单是否处于同一 Commit**。
`TCTTestOverviewControl` 的唯一源文件是
`Tests/TestCenter/UI/TCTTestOverviewControl.cs`，对应
`CxVisual.TestCenter.csproj` 的 `<Compile Include="UI\\TCTTestOverviewControl.cs" />`。
**不要**在 `TCTTestViews.cs` 临时复制一个同名类，也不要仅下载单个新文件替代完整源码更新。

在仓库根目录的 PowerShell 终端检查：

```powershell
git status --short
git fetch origin master
git pull --ff-only origin master
Test-Path .\Tests\TestCenter\UI\TCTTestOverviewControl.cs
Select-String -Path .\Tests\TestCenter\CxVisual.TestCenter.csproj -Pattern 'TCTTestOverviewControl.cs'
```

如 `git pull --ff-only` 因本地未提交修改、分叉而失败，**先保留并整理本地修改，不要强制 reset/clean 丢失代码**；
必须确保 `.csproj` 与 `UI/` 源码来自同一提交。之后右击 Visual Studio 的
`CxVisual.TestCenter` →「重新加载项目」，执行「重新生成解决方案」。
最新远端 `master` 已包含此类及编译项；远端 Windows x64/.NET48 完整编译通过并不能替代本地工作区同步。

界面验收另外以 `DOCS/Architecture/TestCenter/` 六张 2560×1440 稿为视觉基准。
Geometry、ObjectTree、Graph、Channel、Runtime 专业页只消费真实 Owner 的
Run/Attempt/证据快照；NoEvidence、需要人工审签、资源 Lease 未暴露等待者身份等状态不允许伪造。
