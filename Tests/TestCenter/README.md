# CxVisual.TestCenter 开发者快速使用（V4.0 M1）

这是给开发者使用的**一页说明**，不是另一本技术规格；所有设计和 F/M/P Owner 归属仍以《CxVisual TestCenter 统一测试体系完整规格 V4.0》为准。开发者无需手动注册 Catalog、Runner 或 Worker，也不需要修改生产项目引用。

## 最常见：新增一个测试

在本工程 `Tests/TestCenter/Cases/` 或其子目录新建 `TCTMyChecks.cs`：

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

`Cases\**\*.cs` 在现有 `CxVisual.TestCenter.csproj` 自动包含新文件。无需新建项目，不修改生产 Foundation/Mechanism Owner。

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

界面视觉依据：`DOCS/Architecture/TestCenter/` 中六张2560×1440设计图；权威需求以 `DOCS/Architecture/CxVisual_统一测试体系——完整规格 V4.0.md` 第08章及全局台账为准。当前完整Geometry Expected/Actual/Diff、Graph/Channel/Runtime专业结构化Viewer、历史浏览/重跑联动、Golden审批、Suite/Plan、正式覆盖率、完整UIA和HIL仍未验收，不允许以本段Smoke冒充完成。
