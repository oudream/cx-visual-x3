# CxVisual.TestCenter - NUnit Engine真实集成PoC（待CI证据）

## 定位
保持单一 WPF/.NET Framework 4.8 测试项目和既有TCT/ITCT工业契约，测试业务Owner不引用NUnit。验证NUnit Engine 3.22.0 + NUnit 4.4.0是否能加载并执行TestCenter.exe内的测试；该验证不代表已有TCT Runner被替换。

## 最小闭环
`CxVisual.TestCenter.exe --nunit-probe positive.xml` 使用 NUnit.Engine.Api 的 TestEngineActivator、TestPackage、ITestRunner.Explore、Run完成5例自动发现与执行：同步、async Task、两行参数化、Setup/TearDown及负例开关（关闭）。

`CxVisual.TestCenter.exe --nunit-probe negative.xml --negative` 显式引入一个 NUnit 真实断言失败并检查XML中FailureMessage，确保外部执行引擎不能把失败换算成通过。

包版本固定：NUnit 4.4.0、NUnit.Engine.Api 3.22.0、NUnit.Engine 3.22.0（正式稳定发行）；全部放在唯一的Tests/TestCenter项目，无新项目/第7 Workbench。

## 进入正式适配前必须验证
1. Windows CI完成恢复、完整解决方案编译，TestCenter原生TCT测试完全保持不变。
2. NUnit Explore和Run能在WPF EXE中成功发现并执行5个case，XML具备真实参数行身份、状态、原始错误。
3. 比较一层 Engine 进程模型与外层TCT Worker，避免重复子进程和无法终止的嵌套执行。
4. 设计 NUnit XML → TCTResult 的一一映射；Skipped/Inconclusive/Failed/Error/Cancelled/Parameterized Row均应保留真实语义。
5. 未经完整验证，不直接替换TCT Runner、断言或Fixture；FrameworkAdapter保持边界分隔。

参考：https://docs.nunit.org/articles/nunit-engine/Test-Engine-API.html
