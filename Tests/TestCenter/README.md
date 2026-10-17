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